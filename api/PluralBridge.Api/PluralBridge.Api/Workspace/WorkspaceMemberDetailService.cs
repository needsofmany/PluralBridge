// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Resolves the signed-in account and reads anchor profiles through the installed procedure.
/// </summary>
/// <remarks>The retained context does not implement this contract. A fake is used by tests so they do not open a database.</remarks>
internal interface IWorkspaceProfileGateway
{
	/// <summary>Resolves the caller. Null means the session has no account.</summary>
	/// <param name="user">The authenticated principal.</param>
	/// <param name="cancellationToken">Cancels account resolution.</param>
	/// <returns>The account, or null.</returns>
	Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

	/// <summary>Runs the live profile procedure. The caller has already derived the anchor and branch.</summary>
	/// <param name="email">The resolved account email.</param>
	/// <param name="objectType">system, group, or place.</param>
	/// <param name="anchorName">The anchor name from the occurrence.</param>
	/// <param name="branch">The branch derived from the occurrence.</param>
	/// <param name="cancellationToken">Cancels the procedure.</param>
	/// <returns>The six-set bundle.</returns>
	Task<AnchorProfileBundle> ReadProfilesAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		CancellationToken cancellationToken);

	/// <summary>Resolves the caller and records connection and resolution durations when a profile is present.</summary>
	/// <param name="user">The authenticated principal.</param>
	/// <param name="cancellationToken">Cancels account resolution.</param>
	/// <param name="profile">The first-page profile. Null records nothing.</param>
	/// <returns>The account, or null.</returns>
	Task<AccessContextHelper.Account?> ResolveProfiledAsync(ClaimsPrincipal user, CancellationToken cancellationToken, IWorkspaceGatewayTiming? profile)
		=> ResolveAsync(user, cancellationToken);

	/// <summary>Runs the live profile procedure and records its durations when a profile is present.</summary>
	/// <param name="email">The resolved account email.</param>
	/// <param name="objectType">system, group, or place.</param>
	/// <param name="anchorName">The anchor name from the occurrence.</param>
	/// <param name="branch">The branch derived from the occurrence.</param>
	/// <param name="cancellationToken">Cancels the procedure.</param>
	/// <param name="profile">The first-page profile. Null records nothing.</param>
	/// <param name="profileCaller">member or avatar when <paramref name="profile"/> is present. Null leaves the procedure untimed beyond the authorization total.</param>
	/// <returns>The six-set bundle.</returns>
	Task<AnchorProfileBundle> ReadProfilesProfiledAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		CancellationToken cancellationToken,
		IWorkspaceGatewayTiming? profile,
		string? profileCaller = null)
		=> ReadProfilesAsync(email, objectType, anchorName, branch, cancellationToken);
}

/// <summary>
/// Connection and authorization durations shared by the member-detail and avatar profiles.
/// </summary>
internal interface IWorkspaceGatewayTiming
{
	/// <summary>Records the account-resolution connection.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	void SetResolveConnectionOpen(long elapsedMilliseconds);

	/// <summary>Records account resolution.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	void SetAccountResolution(long elapsedMilliseconds);

	/// <summary>Records the profile-read connection.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	void SetProfileConnectionOpen(long elapsedMilliseconds);

	/// <summary>Records profile authorization and read.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	void SetProfileAuthorization(long elapsedMilliseconds);

	/// <summary>Records a profiled procedure read. The default ignores it. A disabled profile is never given a capture.</summary>
	/// <param name="capture">Argument fingerprint, result-set counts, and reader timings. It does not contain argument text.</param>
	void SetProfileRead(AnchorProfileReadCapture capture)
	{
	}

	/// <summary>Records creation of the avatar graph-copy table. The default ignores it.</summary>
	/// <param name="elapsedMilliseconds">The client elapsed time.</param>
	void SetGraphCopyTable(long elapsedMilliseconds)
	{
	}
}

/// <summary>The SQL implementation of <see cref="IWorkspaceProfileGateway"/>.</summary>
internal sealed class SqlWorkspaceProfileGateway(IConfiguration configuration) : IWorkspaceProfileGateway
{
	/// <inheritdoc />
	public Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
	{
		return ResolveProfiledAsync(user, cancellationToken, null);
	}

	/// <inheritdoc />
	public async Task<AccessContextHelper.Account?> ResolveProfiledAsync(
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		IWorkspaceGatewayTiming? profile)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await MeasureAsync(profile, static (timing, elapsed) => timing.SetResolveConnectionOpen(elapsed), async () =>
		{
			await connection.OpenAsync(cancellationToken);
		});
		AccessContextHelper.Account? account = null;
		await MeasureAsync(profile, static (timing, elapsed) => timing.SetAccountResolution(elapsed), async () =>
		{
			account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, user, cancellationToken);
		});
		return account;
	}

	/// <inheritdoc />
	public Task<AnchorProfileBundle> ReadProfilesAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		CancellationToken cancellationToken)
	{
		return ReadProfilesProfiledAsync(email, objectType, anchorName, branch, cancellationToken, null);
	}

	/// <inheritdoc />
	public async Task<AnchorProfileBundle> ReadProfilesProfiledAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		CancellationToken cancellationToken,
		IWorkspaceGatewayTiming? profile,
		string? profileCaller = null)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await MeasureAsync(profile, static (timing, elapsed) => timing.SetProfileConnectionOpen(elapsed), async () =>
		{
			await connection.OpenAsync(cancellationToken);
		});
		AnchorProfileBundle? bundle = null;
		var capture = profile == null || profileCaller == null ? null : new AnchorProfileReadCapture(profileCaller);
		try
		{
			await MeasureAsync(profile, static (timing, elapsed) => timing.SetProfileAuthorization(elapsed), async () =>
			{
				bundle = await AnchorMemberProfileReader.ExecuteAsync(connection, email, objectType, anchorName, branch, cancellationToken, capture);
			});
		}
		finally
		{
			if (capture != null)
			{
				profile?.SetProfileRead(capture);
			}
		}

		return bundle ?? throw new AnchorProfileContractException("The profile procedure did not return the six-result-set contract.");
	}

	/// <summary>Runs one stage. A null profile performs the work and stores no duration.</summary>
	/// <param name="profile">The profile receiving the duration. Null stores nothing.</param>
	/// <param name="record">Writes the elapsed milliseconds onto the profile.</param>
	/// <param name="action">The stage work.</param>
	/// <returns>The task completes when the stage finishes.</returns>
	private static async Task MeasureAsync(
		IWorkspaceGatewayTiming? profile,
		Action<IWorkspaceGatewayTiming, long> record,
		Func<Task> action)
	{
		if (profile == null)
		{
			await action();
			return;
		}

		var watch = Stopwatch.StartNew();
		try
		{
			await action();
		}
		finally
		{
			watch.Stop();
			record(profile, watch.ElapsedMilliseconds);
		}
	}
}

/// <summary>The result of one workspace member-detail request.</summary>
internal abstract record WorkspaceMemberDetailResult
{
	/// <summary>The session has no account.</summary>
	internal sealed record MissingAccount : WorkspaceMemberDetailResult;

	/// <summary>The context is missing, expired, evicted, or owned by another account.</summary>
	internal sealed record ContextUnavailable : WorkspaceMemberDetailResult;

	/// <summary>The path is not an anchor occurrence in the acquired context.</summary>
	/// <param name="Error">The rejection text.</param>
	internal sealed record OccurrenceRejected(string Error) : WorkspaceMemberDetailResult;

	/// <summary>The live procedure succeeded. The cards are the direct members of the occurrence, capped at the batch size.</summary>
	/// <param name="Cards">Card rows. Avatar URLs are null.</param>
	internal sealed record Ready(IReadOnlyList<AnchorMemberCard> Cards) : WorkspaceMemberDetailResult;
}

/// <summary>
/// Loads direct member cards for one anchor occurrence in a retained workspace context.
/// </summary>
/// <remarks>
/// Account ownership comes from the context store. The occurrence and branch come from that context.
/// The profile procedure performs the current authorization check. Retained access facts are not consulted.
/// Inheritance for this batch is one resolution of the selected occurrence. It does not read the database again and it does not walk once per member.
/// </remarks>
internal sealed class WorkspaceMemberDetailService(WorkspaceContextStore store, IWorkspaceProfileGateway gateway)
{
	/// <summary>
	/// Validates the context and occurrence, then reads the direct member cards.
	/// </summary>
	/// <param name="user">The authenticated principal.</param>
	/// <param name="contextId">The opaque context id from the opening response.</param>
	/// <param name="occurrence">The selected anchor path.</param>
	/// <param name="skip">Unique direct members to pass over. Zero is the first page. Negative is rejected before the profile procedure.</param>
	/// <param name="cancellationToken">Cancels account resolution or the profile procedure.</param>
	/// <param name="profile">The first-page profile. Null, and any skip other than zero, records no durations.</param>
	/// <returns>A result that does not include image bytes or avatar routes.</returns>
	internal async Task<WorkspaceMemberDetailResult> LoadAsync(
		ClaimsPrincipal user,
		string contextId,
		string? occurrence,
		CancellationToken cancellationToken,
		int skip = 0,
		WorkspaceMemberDetailProfile? profile = null)
	{
		if (skip != 0)
		{
			profile = null;
		}

		var account = await gateway.ResolveProfiledAsync(user, cancellationToken, profile);
		if (account is null)
		{
			return new WorkspaceMemberDetailResult.MissingAccount();
		}

		var acquired = store.Acquire(account.AccountId, contextId);
		if (!acquired.Available || acquired.Context is null)
		{
			return new WorkspaceMemberDetailResult.ContextUnavailable();
		}

		if (skip < 0)
		{
			return new WorkspaceMemberDetailResult.OccurrenceRejected("The member batch skip must be zero or greater.");
		}

		var preparedDetail = default(WorkspaceAnchorDetail);
		var prepared = false;
		var prepareError = "";
		Measure(profile, static (timing, elapsed) => timing.SetPrepareOccurrence(elapsed), () =>
		{
			prepared = acquired.Context.TryPrepareAnchorDetail(occurrence, skip, out var detail, out prepareError);
			preparedDetail = detail;
		});
		if (!prepared)
		{
			return new WorkspaceMemberDetailResult.OccurrenceRejected(prepareError);
		}

		var bundle = await gateway.ReadProfilesProfiledAsync(
			account.Email,
			preparedDetail.ObjectType,
			preparedDetail.AnchorName,
			preparedDetail.Branch,
			cancellationToken,
			profile,
			profile == null ? null : AnchorProfileReadCapture.MemberCaller);
		profile?.BeginResponsePreparation();
		try
		{
			if (!acquired.Context.TryResolvePlacement(occurrence, null, null, out var defaultVisibility, out var defaultPermission, out var resolveError))
			{
				return new WorkspaceMemberDetailResult.OccurrenceRejected(resolveError);
			}

			if (!WorkspaceAnchorSettingCodes.TryDescribeVisibility(defaultVisibility, out var visibilityMode, out var visibilityTypeId)
				|| !WorkspaceAnchorSettingCodes.TryDescribePermission(defaultPermission, out var permissionMode, out var permissionCode))
			{
				return new WorkspaceMemberDetailResult.OccurrenceRejected("The selected occurrence settings could not be prepared.");
			}

			return new WorkspaceMemberDetailResult.Ready(
				AnchorMemberProfileMapper.ProjectWithOccurrenceDefaults(
					bundle,
					preparedDetail.DirectMemberIds,
					visibilityMode,
					visibilityTypeId,
					permissionMode,
					permissionCode));
		}
		finally
		{
			profile?.EndResponsePreparation();
		}
	}

	/// <summary>Runs one in-memory stage. A null profile performs the work and stores no duration.</summary>
	/// <param name="profile">The profile receiving the duration. Null stores nothing.</param>
	/// <param name="record">Writes the elapsed milliseconds onto the profile.</param>
	/// <param name="action">The stage work.</param>
	private static void Measure(WorkspaceMemberDetailProfile? profile, Action<WorkspaceMemberDetailProfile, long> record, Action action)
	{
		if (profile == null)
		{
			action();
			return;
		}

		var watch = Stopwatch.StartNew();
		try
		{
			action();
		}
		finally
		{
			watch.Stop();
			record(profile, watch.ElapsedMilliseconds);
		}
	}

	/// <summary>Reads the member-detail skip query. A missing value is the first page.</summary>
	/// <param name="text">The raw skip query. Null or blank selects zero.</param>
	/// <param name="skip">The accepted skip. Zero when the method returns false.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when the text is empty or a zero-or-greater integer.</returns>
	/// <remarks>A sign, a fraction, and any other non-digit text are rejected. This does not authorize the request.</remarks>
	internal static bool TryParseMemberSkip(string? text, out int skip, out string error)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			skip = 0;
			error = "";
			return true;
		}

		if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out skip) || skip < 0)
		{
			skip = 0;
			error = "The member batch skip must be zero or greater.";
			return false;
		}

		error = "";
		return true;
	}
}
