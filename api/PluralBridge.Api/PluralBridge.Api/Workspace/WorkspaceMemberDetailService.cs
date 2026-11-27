// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

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
}

/// <summary>The SQL implementation of <see cref="IWorkspaceProfileGateway"/>.</summary>
internal sealed class SqlWorkspaceProfileGateway(IConfiguration configuration) : IWorkspaceProfileGateway
{
	/// <inheritdoc />
	public async Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		return await AccessContextHelper.ResolveCurrentAccountAsync(connection, user, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<AnchorProfileBundle> ReadProfilesAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		CancellationToken cancellationToken)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		return await AnchorMemberProfileReader.ExecuteAsync(connection, email, objectType, anchorName, branch, cancellationToken);
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
	/// <returns>A result that does not include image bytes or avatar routes.</returns>
	internal async Task<WorkspaceMemberDetailResult> LoadAsync(
		ClaimsPrincipal user,
		string contextId,
		string? occurrence,
		CancellationToken cancellationToken,
		int skip = 0)
	{
		var account = await gateway.ResolveAsync(user, cancellationToken);
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

		if (!acquired.Context.TryPrepareAnchorDetail(occurrence, skip, out var detail, out var error))
		{
			return new WorkspaceMemberDetailResult.OccurrenceRejected(error);
		}

		var bundle = await gateway.ReadProfilesAsync(
			account.Email,
			detail.ObjectType,
			detail.AnchorName,
			detail.Branch,
			cancellationToken);
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
				detail.DirectMemberIds,
				visibilityMode,
				visibilityTypeId,
				permissionMode,
				permissionCode));
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
