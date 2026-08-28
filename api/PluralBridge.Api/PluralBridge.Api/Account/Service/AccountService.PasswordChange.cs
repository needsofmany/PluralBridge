// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountOperationResponse>> ChangePasswordAsync(
		Guid actorAccountId,
		ChangePasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Authenticated account changes start with a requested event so abandoned/rejected attempts
		// are visible in audit.
		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordChangeRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			actorAccountId == Guid.Empty ? null : actorAccountId,
			actorAccountId == Guid.Empty ? null : actorAccountId,
			correlationId,
			Globals.accountOperationPasswordChange,
			cancellationToken);

		// Empty actor id means the caller never resolved to an authenticated account.
		if (actorAccountId == Guid.Empty
			|| !AccountText.HasText(request.CurrentPassword)
			|| !AccountText.HasText(request.NewPassword)
			|| request.NewPassword.Length < 12)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordChangeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				correlationId,
				Globals.accountOperationPasswordChange,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountPasswordChangeCouldNotBeCompleted);
		}

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var account = await AccountRepository.ReadPasswordChangeAccountAsync(
			connection,
			actorAccountId,
			cancellationToken);

		if (account is null || account.AccountStatusId != 1 || !account.IsEmailVerified)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordChangeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				actorAccountId,
				actorAccountId,
				correlationId,
				Globals.accountOperationPasswordChange,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountPasswordChangeCouldNotBeCompleted);
		}

		// Current password verification protects the change even when the session is valid.
		var currentPasswordAccepted = _passwordHasher.VerifyPassword(
			request.CurrentPassword,
			account.PasswordHash,
			account.PasswordHashAlgorithm,
			account.PasswordHashVersion);

		if (!currentPasswordAccepted)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordChangeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationPasswordChange,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountPasswordChangeCouldNotBeCompleted);
		}

		var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

		// Only the credential update is transactional here; there is no account-code row involved.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

		try
		{
			await AccountRepository.UpdatePasswordCredentialAsync(
				connection,
				transaction,
				account.AccountId,
				newPasswordHash,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordChangeRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationPasswordChange,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountPasswordChangeCouldNotBeCompleted);
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordChangeCompleted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationPasswordChange,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountPasswordChangeCompleted),
			Globals.accountPasswordChangeCompleted);
	}
}
