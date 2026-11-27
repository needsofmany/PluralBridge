// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

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
}

/// <summary>The database avatar reader. It does not choose a source from configuration.</summary>
internal sealed class SqlWorkspaceAvatarReader(IConfiguration configuration) : IWorkspaceAvatarReader
{
	/// <inheritdoc />
	public async Task<(byte[] Bytes, string ContentType)?> ReadAsync(
		Guid accountId,
		Guid memberId,
		CancellationToken cancellationToken)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		return await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: true,
			accountId,
			memberId,
			cancellationToken,
			connection,
			new AnchorMemberProfileSave.AvatarReadTiming());
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
	IWorkspaceAvatarReader avatars)
{
	/// <summary>
	/// Authorizes the occurrence, then returns images for the direct-member page.
	/// </summary>
	/// <param name="user">The authenticated principal.</param>
	/// <param name="contextId">The opaque context id from the opening response.</param>
	/// <param name="occurrence">The selected anchor path.</param>
	/// <param name="skip">Unique direct members to pass over. Zero is the first page. Negative is rejected before the profile procedure.</param>
	/// <param name="cancellationToken">Cancels account resolution, the profile procedure, or an image read. A cancelled request is not a batch result.</param>
	/// <returns>The authorized page. An empty page is empty after the procedure succeeds. A member with no image has null image fields.</returns>
	/// <remarks>
	/// Page membership uses the same preview order, deduplication, and skip as member details.
	/// A page member the procedure does not return is omitted. Only an image the context does not hold is loaded, and only a loaded image is retained.
	/// </remarks>
	internal async Task<WorkspaceAvatarBatchResult> LoadAsync(
		System.Security.Claims.ClaimsPrincipal user,
		string contextId,
		string? occurrence,
		CancellationToken cancellationToken,
		int skip = 0)
	{
		var account = await gateway.ResolveAsync(user, cancellationToken);
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
		var bundle = await gateway.ReadProfilesAsync(
			account.Email,
			detail.ObjectType,
			detail.AnchorName,
			detail.Branch,
			cancellationToken);
		var authorized = new HashSet<Guid>();
		foreach (var profile in bundle.Profiles)
		{
			authorized.Add(profile.MemberId);
		}

		var images = new List<WorkspaceAvatarBatchItem>(detail.DirectMemberIds.Count);
		foreach (var memberId in detail.DirectMemberIds)
		{
			if (!authorized.Contains(memberId))
			{
				continue;
			}

			var retained = acquired.Context.FindAvatar(memberId);
			if (retained is not null)
			{
				images.Add(new WorkspaceAvatarBatchItem(memberId, retained.ContentType, retained.Bytes));
				continue;
			}

			(byte[] Bytes, string ContentType)? loaded;
			try
			{
				loaded = await avatars.ReadAsync(account.AccountId, memberId, cancellationToken);
			}
			catch (SqlException exception) when (exception.Number == 208 && !cancellationToken.IsCancellationRequested)
			{
				return new WorkspaceAvatarBatchResult.AvatarStorageMissing();
			}

			if (loaded is null)
			{
				images.Add(new WorkspaceAvatarBatchItem(memberId, null, null));
				continue;
			}

			var stored = acquired.Context.RetainAvatar(memberId, loaded);
			images.Add(new WorkspaceAvatarBatchItem(
				memberId,
				stored?.ContentType ?? loaded.Value.ContentType,
				stored?.Bytes ?? loaded.Value.Bytes));
		}

		return new WorkspaceAvatarBatchResult.Ready(images);
	}
}
