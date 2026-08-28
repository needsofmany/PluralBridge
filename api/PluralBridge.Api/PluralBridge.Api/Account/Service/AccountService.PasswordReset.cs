// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountOperationResponse>> ForgotPasswordAsync(
		ForgotPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Password reset uses the same enumeration-resistant public response as username recovery.
		if (!AccountText.HasText(request.UsernameOrEmail))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountPasswordResetInstructionsSent);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var account = await AccountRepository.ReadPasswordResetAccountAsync(
			connection,
			normalizedIdentifier,
			cancellationToken);

		if (account is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRequested,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				null,
				null,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountPasswordResetInstructionsSent),
				Globals.accountPasswordResetInstructionsSent);
		}

		var resetCode = AccountCodeService.CreateNumericCode();
		var resetHash = _passwordHasher.HashPassword(resetCode);

		// The reset code is scoped to the account's verified normalized email, not arbitrary request text.
		await AccountCodeService.InsertPasswordResetCodeAsync(
			connection,
			account.AccountId,
			account.NormalizedEmail,
			resetHash,
			correlationId,
			cancellationToken);

		var passwordResetCodeDelivered = await TryDeliverCodeAsync(
			new AccountCodeDeliveryCommand(
				account.AccountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				account.NormalizedEmail,
				resetCode,
				correlationId),
			AccountAuditEvents.PasswordResetRejected,
			Globals.accountOperationPasswordReset,
			account.AccountId,
			correlationId,
			cancellationToken);

		if (!passwordResetCodeDelivered)
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountPasswordResetInstructionsSent);
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountPasswordResetInstructionsSent),
			Globals.accountPasswordResetInstructionsSent);
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> ResetPasswordAsync(
		ResetPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Validate shape before any account/code lookup. Public errors remain generic.
		if (!AccountText.HasText(request.UsernameOrEmail)
			|| !AccountText.HasText(request.Code)
			|| !AccountText.HasText(request.NewPassword)
			|| request.NewPassword.Length < 12)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

		// Reset verification mirrors registration verification: select latest code, reject stale
		// states first, then hash-check the submitted plaintext code.
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var codeRecord = await AccountCodeService.ReadLatestPasswordResetCodeAsync(
			connection,
			normalizedIdentifier,
			cancellationToken);

		if (codeRecord is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				null,
				null,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		if (codeRecord.ConsumedAtUtc is not null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Consumed,
				AccountReasonCodes.ConsumedCode,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ConsumedCode,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		if (codeRecord.ExpiresAtUtc <= DateTime.UtcNow)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Expired,
				AccountReasonCodes.ExpiredCode,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ExpiredCode,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		if (codeRecord.AttemptCount >= codeRecord.MaxAttempts)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Blocked,
				AccountReasonCodes.RateLimited,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		var codeAccepted = _passwordHasher.VerifyPassword(
			request.Code.Trim(),
			codeRecord.CodeHash,
			codeRecord.CodeHashAlgorithm,
			codeRecord.CodeHashVersion);

		if (!codeAccepted)
		{
			await AccountCodeService.IncrementCodeAttemptAsync(
				connection,
				codeRecord.AccountCodeId,
				cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

		// Password update and code consumption must commit together.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

		try
		{
			await AccountCodeService.ConsumeCodeAsync(
				connection,
				transaction,
				codeRecord.AccountCodeId,
				cancellationToken);

			await AccountRepository.UpdatePasswordCredentialAsync(
				connection,
				transaction,
				codeRecord.AccountId,
				newPasswordHash,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationPasswordReset,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountPasswordResetCouldNotBeCompleted);
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetCodeAccepted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeConsumed,
			AccountOutcomes.Consumed,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetCompleted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationPasswordReset,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountPasswordResetCompleted),
			Globals.accountPasswordResetCompleted);
	}
}
