// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountResponse>> UpdateProfileAsync(
		Guid actorAccountId,
		UpdateAccountProfileRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Profile update is limited to display-name changes in this workflow.
		if (actorAccountId == Guid.Empty
			|| !AccountText.HasText(request.DisplayName)
			|| request.DisplayName.Trim().Length > 200)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ProfileRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				correlationId,
				Globals.accountOperationProfile,
				cancellationToken);

			return AccountServiceResult<AccountResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountProfileUpdateCouldNotBeCompleted);
		}

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var account = await AccountRepository.ReadAccountProfileAsync(
			connection,
			actorAccountId,
			cancellationToken);

		if (account is null
			|| account.AccountStatusId != 1
			|| !account.IsEmailVerified)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ProfileRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				actorAccountId,
				actorAccountId,
				correlationId,
				Globals.accountOperationProfile,
				cancellationToken);

			return AccountServiceResult<AccountResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountProfileUpdateCouldNotBeCompleted);
		}

		try
		{
			await AccountRepository.UpdateAccountProfileAsync(
				connection,
				account.AccountId,
				request.DisplayName.Trim(),
				cancellationToken);
		}
		catch
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ProfileRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationProfile,
				cancellationToken);

			return AccountServiceResult<AccountResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountProfileUpdateCouldNotBeCompleted);
		}

		// Re-read after update so the response reflects database state, including timestamps.
		var updatedAccount = await AccountRepository.ReadAccountProfileAsync(
			connection,
			account.AccountId,
			cancellationToken);

		if (updatedAccount is null)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ProfileRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationProfile,
				cancellationToken);

			return AccountServiceResult<AccountResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountProfileUpdateCouldNotBeCompleted);
		}

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.ProfileUpdated,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			updatedAccount.AccountId,
			updatedAccount.AccountId,
			correlationId,
			Globals.accountOperationProfile,
			cancellationToken);

		return AccountServiceResult<AccountResponse>.Success(
			new AccountResponse(
				updatedAccount.AccountId,
				updatedAccount.Username,
				updatedAccount.Email,
				updatedAccount.DisplayName,
				updatedAccount.IsEmailVerified,
				updatedAccount.AccountStatusName,
				updatedAccount.CreatedAtUtc,
				updatedAccount.UpdatedAtUtc,
				updatedAccount.LastLoginAtUtc),
			Globals.accountProfileUpdateCompleted);
	}
}
