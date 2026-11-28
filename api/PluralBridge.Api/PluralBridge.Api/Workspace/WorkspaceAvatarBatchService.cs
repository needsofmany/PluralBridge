// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Reads one member image through the common avatar loader.
/// </summary>
/// <remarks>
/// The production reader uses the database. A test reader uses the same loader with a factory.
/// This contract does not decide whether the caller may receive the image.
/// </remarks>
internal interface IWorkspaceAvatarReader
{
	/// <summary>Loads one member image.</summary>
	/// <param name="accountId">The account passed to the loader.</param>
	/// <param name="memberId">The member whose image is requested.</param>
	/// <param name="cancellationToken">Cancels the load. Cancellation is not a missing image.</param>
	/// <returns>The loader result. Null when that member has no image.</returns>
	Task<(byte[] Bytes, string ContentType)?> ReadAsync(
		Guid accountId,
		Guid memberId,
		CancellationToken cancellationToken);

	/// <summary>Loads one member image and records its read timings when a profile is present.</summary>
	/// <param name="accountId">The account passed to the loader.</param>
	/// <param name="memberId">The member whose image is requested.</param>
	/// <param name="cancellationToken">Cancels the load. Cancellation is not a missing image.</param>
	/// <param name="profile">The first-page profile. Null records nothing. The image bytes are unchanged.</param>
	/// <returns>The loader result. Null when that member has no image.</returns>
	Task<(byte[] Bytes, string ContentType)?> ReadProfiledAsync(
		Guid accountId,
		Guid memberId,
		CancellationToken cancellationToken,
		WorkspaceAvatarBatchProfile? profile)
		=> ReadAsync(accountId, memberId, cancellationToken);

	/// <summary>
	/// Loads the images for one miss list. An empty list does no work.
	/// The default calls <see cref="ReadAsync"/> once per id so an existing reader keeps working.
	/// That default does not copy one read's timings onto every member.
	/// </summary>
	/// <param name="accountId">The account passed to the loader.</param>
	/// <param name="memberIds">Authorized page members the context does not already hold, in preview order.</param>
	/// <param name="cancellationToken">Cancels the load. Cancellation is not a missing image.</param>
	/// <param name="profile">The first-page profile. Null records nothing. The image bytes are unchanged.</param>
	/// <returns>Images the loader found, keyed by member id. A missing key means that member has no image.</returns>
	async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(
		Guid accountId,
		IReadOnlyList<Guid> memberIds,
		CancellationToken cancellationToken,
		WorkspaceAvatarBatchProfile? profile = null)
	{
		// The default reader has no shared SQL stages to record. A batch reader records them itself.
		_ = profile;
		var images = new Dictionary<Guid, (byte[] Bytes, string ContentType)>();
		foreach (var memberId in memberIds)
		{
			var image = await ReadAsync(accountId, memberId, cancellationToken);
			if (image is { } found && !images.ContainsKey(memberId))
			{
				images.Add(memberId, found);
			}
		}

		return images;
	}
}

/// <summary>The database avatar reader. It does not choose a source from configuration.</summary>
internal sealed class SqlWorkspaceAvatarReader(IConfiguration configuration) : IWorkspaceAvatarReader
{
	/// <inheritdoc />
	public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
		Guid accountId,
		Guid memberId,
		CancellationToken cancellationToken)
	{
		return ReadProfiledAsync(accountId, memberId, cancellationToken, null);
	}

	/// <inheritdoc />
	public async Task<(byte[] Bytes, string ContentType)?> ReadProfiledAsync(
		Guid accountId,
		Guid memberId,
		CancellationToken cancellationToken,
		WorkspaceAvatarBatchProfile? profile)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		long? connectionOpenMs = null;
		if (profile == null)
		{
			await connection.OpenAsync(cancellationToken);
		}
		else
		{
			var connectionWatch = Stopwatch.StartNew();
			await connection.OpenAsync(cancellationToken);
			connectionWatch.Stop();
			connectionOpenMs = connectionWatch.ElapsedMilliseconds;
		}

		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: true,
			accountId,
			memberId,
			cancellationToken,
			connection,
			timing);
		profile?.RecordRead(memberId, connectionOpenMs, timing);
		return image;
	}

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(
		Guid accountId,
		IReadOnlyList<Guid> memberIds,
		CancellationToken cancellationToken,
		WorkspaceAvatarBatchProfile? profile = null)
	{
		_ = accountId;
		_ = memberIds;
		_ = cancellationToken;
		_ = profile;
		throw new InvalidOperationException("An avatar batch reads images on the profile connection after the profile procedure copies the graph.");
	}
}

/// <summary>The result of one workspace avatar batch.</summary>
internal abstract record WorkspaceAvatarBatchResult
{
	/// <summary>The session has no account.</summary>
	internal sealed record MissingAccount : WorkspaceAvatarBatchResult;

	/// <summary>The context is missing, expired, evicted, or owned by another account.</summary>
	internal sealed record ContextUnavailable : WorkspaceAvatarBatchResult;

	/// <summary>The path is not an anchor occurrence in the acquired context, or the skip is negative.</summary>
	/// <param name="Error">The rejection text.</param>
	internal sealed record OccurrenceRejected(string Error) : WorkspaceAvatarBatchResult;

	/// <summary>The image table is not installed. Members already retained stay retained.</summary>
	internal sealed record AvatarStorageMissing : WorkspaceAvatarBatchResult;

	/// <summary>The profile procedure authorized this page.</summary>
	/// <param name="Images">Authorized page members, in preview order. A null image was not retained.</param>
	internal sealed record Ready(IReadOnlyList<WorkspaceAvatarBatchItem> Images) : WorkspaceAvatarBatchResult;
}

/// <summary>
/// One authorized member's image, or the fact that the member has none.
/// </summary>
/// <param name="MemberId">The member id from the preview page.</param>
/// <param name="ContentType">The retained or loaded content type. Null when the member has no image.</param>
/// <param name="Bytes">The retained or loaded bytes. Null when the member has no image. The array is not copied.</param>
internal sealed record WorkspaceAvatarBatchItem(Guid MemberId, string? ContentType, byte[]? Bytes);

/// <summary>
/// Loads avatar bytes for one page of direct members in a retained workspace context.
/// </summary>
/// <remarks>
/// Account ownership comes from the context store. The occurrence and page come from that context.
/// The profile procedure authorizes the page before any image is returned, including an image this context already holds.
/// A retained image is not an access grant. This service does not replace, delete, or evict images.
/// </remarks>
internal sealed class WorkspaceAvatarBatchService(
	WorkspaceContextStore store,
	IWorkspaceProfileGateway gateway,
	IWorkspaceAvatarReader avatars,
	IAvatarBatchGraphSessions? graphSessions = null)
{
	/// <summary>
	/// Authorizes the occurrence, then returns images for the direct-member page.
	/// </summary>
	/// <param name="user">The authenticated principal.</param>
	/// <param name="contextId">The opaque context id from the opening response.</param>
	/// <param name="occurrence">The selected anchor path.</param>
	/// <param name="skip">Unique direct members to pass over. Zero is the first page. Negative is rejected before the profile procedure.</param>
	/// <param name="cancellationToken">Cancels account resolution, the profile procedure, or an image read. A cancelled request is not a batch result.</param>
	/// <param name="profile">The first-page profile. Null, and any skip other than zero, records no durations.</param>
	/// <returns>The authorized page. An empty page is empty after the procedure succeeds. A member with no image has null image fields.</returns>
	/// <remarks>
	/// Page membership uses the same preview order, deduplication, and skip as member details.
	/// A page member the procedure does not return is omitted. Only an image the context does not hold is loaded, and only a loaded image is retained.
	/// When the graph-copy session is present, profile authorization and the image query share one connection. That query uses the copied graph and does not build another one.
	/// No image command runs when every authorized image is already retained. The caller-owned temporary tables are dropped before the connection is released.
	/// </remarks>
	internal async Task<WorkspaceAvatarBatchResult> LoadAsync(
		System.Security.Claims.ClaimsPrincipal user,
		string contextId,
		string? occurrence,
		CancellationToken cancellationToken,
		int skip = 0,
		WorkspaceAvatarBatchProfile? profile = null)
	{
		if (skip != 0)
		{
			profile = null;
		}

		var account = await gateway.ResolveProfiledAsync(user, cancellationToken, profile);
		if (account is null)
		{
			return new WorkspaceAvatarBatchResult.MissingAccount();
		}

		var acquired = store.Acquire(account.AccountId, contextId);
		if (!acquired.Available || acquired.Context is null)
		{
			return new WorkspaceAvatarBatchResult.ContextUnavailable();
		}

		if (skip < 0)
		{
			return new WorkspaceAvatarBatchResult.OccurrenceRejected("The member batch skip must be zero or greater.");
		}

		if (!acquired.Context.TryPrepareAnchorDetail(occurrence, skip, out var detail, out var error))
		{
			return new WorkspaceAvatarBatchResult.OccurrenceRejected(error);
		}

		// The procedure is the current authorization check. Retention cannot skip it.
		IAvatarBatchGraphSession? session = null;
		try
		{
			AnchorProfileBundle bundle;
			if (graphSessions != null)
			{
				session = await graphSessions.OpenAsync(profile, cancellationToken);
				bundle = await session.ReadProfilesAsync(
					account.Email,
					detail.ObjectType,
					detail.AnchorName,
					detail.Branch,
					profile,
					cancellationToken);
			}
			else
			{
				bundle = await gateway.ReadProfilesProfiledAsync(
					account.Email,
					detail.ObjectType,
					detail.AnchorName,
					detail.Branch,
					cancellationToken,
					profile,
					profile == null ? null : AnchorProfileReadCapture.AvatarCaller);
			}

			var authorized = new HashSet<Guid>();
			foreach (var memberProfile in bundle.Profiles)
			{
				authorized.Add(memberProfile.MemberId);
			}

			var loop = profile == null ? null : Stopwatch.StartNew();
			try
			{
				var images = new List<WorkspaceAvatarBatchItem>(detail.DirectMemberIds.Count);
				var missIds = new List<Guid>();
				var missSlots = new List<int>();
				var scan = profile == null ? null : Stopwatch.StartNew();
				foreach (var memberId in detail.DirectMemberIds)
				{
					if (!authorized.Contains(memberId))
					{
						continue;
					}

					var retained = acquired.Context.FindAvatar(memberId);
					if (retained is not null)
					{
						profile?.RecordHit(memberId);
						images.Add(new WorkspaceAvatarBatchItem(memberId, retained.ContentType, retained.Bytes));
						continue;
					}

					profile?.BeginMiss(memberId);
					missSlots.Add(images.Count);
					missIds.Add(memberId);
					images.Add(new WorkspaceAvatarBatchItem(memberId, null, null));
				}

				if (scan != null)
				{
					scan.Stop();
					profile?.SetMissScan(scan.ElapsedMilliseconds);
				}

				if (missIds.Count == 0)
				{
					return new WorkspaceAvatarBatchResult.Ready(images);
				}

				IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)> loaded;
				try
				{
					loaded = session == null
						? await avatars.ReadManyAsync(account.AccountId, missIds, cancellationToken, profile)
						: await session.ReadImagesAsync(account.AccountId, missIds, profile, cancellationToken);
				}
				catch (SqlException exception) when (exception.Number == 208 && !cancellationToken.IsCancellationRequested)
				{
					return new WorkspaceAvatarBatchResult.AvatarStorageMissing();
				}

				var retain = profile == null ? null : Stopwatch.StartNew();
				for (var index = 0; index < missIds.Count; index++)
				{
					var memberId = missIds[index];
					if (!loaded.TryGetValue(memberId, out var image))
					{
						profile?.CompleteMiss(memberId, null);
						continue;
					}

					profile?.CompleteMiss(memberId, image.Bytes.Length);
					var stored = acquired.Context.RetainAvatar(memberId, image);
					images[missSlots[index]] = new WorkspaceAvatarBatchItem(
						memberId,
						stored?.ContentType ?? image.ContentType,
						stored?.Bytes ?? image.Bytes);
				}

				if (retain != null)
				{
					retain.Stop();
					profile?.SetRetain(retain.ElapsedMilliseconds);
				}

				return new WorkspaceAvatarBatchResult.Ready(images);
			}
			finally
			{
				if (loop != null)
				{
					loop.Stop();
					profile?.SetImageLoop(loop.ElapsedMilliseconds);
				}
			}
		}
		finally
		{
			if (session != null)
			{
				await session.DisposeAsync();
			}
		}
	}
}
