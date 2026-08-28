// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountOperationResponse>> UpdateContactAsync(
		Guid actorAccountId,
		UpdateAccountContactRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Contact changes are two-step: issue a verification code here, apply the email later
		// in VerifyContactAsync after the code is accepted.
		if (actorAccountId == Guid.Empty
			|| !AccountText.HasText(request.Email)
			|| request.Email.Trim().Length > 320)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				correlationId,
				Globals.accountOperationContact,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

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
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				actorAccountId,
				actorAccountId,
				correlationId,
				Globals.accountOperationContact,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		// No-op contact changes are rejected so the audit trail records intent rather than a fake update.
		if (string.Equals(
				AccountText.NormalizeEmail(account.Email),
				normalizedEmail,
				StringComparison.Ordinal))
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidRequest,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContact,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidRequest,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		// Prevent moving an email address onto an account when another account already owns it.
		if (await AccountRepository.AccountEmailExistsForOtherAccountAsync(
				connection,
				account.AccountId,
				normalizedEmail,
				cancellationToken))
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.DuplicateAccountIdentifier,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContact,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.DuplicateAccountIdentifier,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		var verificationCode = AccountCodeService.CreateNumericCode();
		var verificationHash = _passwordHasher.HashPassword(verificationCode);

		// The new email is not written to the account yet. It is stored as the destination on
		// a contact-verification code row until the user proves mailbox control.
		try
		{
			await AccountCodeService.InsertEmailChangeVerificationCodeAsync(
				connection,
				account.AccountId,
				normalizedEmail,
				verificationHash,
				correlationId,
				cancellationToken);
		}
		catch
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContact,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		var contactVerificationCodeDelivered = await TryDeliverCodeAsync(
			new AccountCodeDeliveryCommand(
				account.AccountId,
				AccountCodePurposes.ContactVerification,
				AccountDestinationTypes.Email,
				normalizedEmail,
				verificationCode,
				correlationId),
			AccountAuditEvents.ContactRejected,
			Globals.accountOperationContact,
			account.AccountId,
			correlationId,
			cancellationToken);

		if (!contactVerificationCodeDelivered)
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountContactUpdateCouldNotBeCompleted);
		}

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationContactVerification,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountContactUpdateVerificationRequired),
			Globals.accountContactUpdateVerificationRequired);
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> VerifyContactAsync(
	Guid actorAccountId,
	VerifyAccountContactRequest request,
	CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Contact verification requires both an authenticated account and the pending email/code pair.
		if (actorAccountId == Guid.Empty
			|| !AccountText.HasText(request.Email)
			|| !AccountText.HasText(request.Code))
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				actorAccountId == Guid.Empty ? null : actorAccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		var normalizedEmail =
			AccountText.NormalizeEmail(request.Email);

		// Contact verification is account-scoped. The same destination on another account must not
		// satisfy this account's pending contact change.
		await using var connection =
			new SqlConnection(_connectionString);

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
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				actorAccountId,
				actorAccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		var codeRecord =
			await AccountCodeService.ReadLatestContactVerificationCodeAsync(
				connection,
				account.AccountId,
				normalizedEmail,
				cancellationToken);

		if (codeRecord is null)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		// The selected contact code is checked for stale states before the submitted code is hashed.
		if (codeRecord.ConsumedAtUtc is not null)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Consumed,
				AccountReasonCodes.ConsumedCode,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ConsumedCode,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		if (codeRecord.ExpiresAtUtc <= DateTime.UtcNow)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Expired,
				AccountReasonCodes.ExpiredCode,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ExpiredCode,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		if (codeRecord.AttemptCount >= codeRecord.MaxAttempts)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Blocked,
				AccountReasonCodes.RateLimited,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				Globals.accountContactVerificationCouldNotBeCompleted);
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

			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		// Re-check duplicate ownership inside the transaction, immediately before applying the change.
		await using var transaction =
			(SqlTransaction)await connection.BeginTransactionAsync(
				cancellationToken);

		try
		{
			if (await AccountRepository.AccountEmailExistsForOtherAccountAsync(
					connection,
					transaction,
					account.AccountId,
					normalizedEmail,
					cancellationToken))
			{
				await transaction.RollbackAsync(cancellationToken);

				await AccountInfrastructure.WriteAuditAsync(
					_auditWriter,
					AccountAuditEvents.ContactRejected,
					AccountOutcomes.Rejected,
					AccountReasonCodes.DuplicateAccountIdentifier,
					account.AccountId,
					account.AccountId,
					correlationId,
					Globals.accountOperationContactVerification,
					cancellationToken);

				return AccountServiceResult<AccountOperationResponse>.Rejected(
					AccountReasonCodes.DuplicateAccountIdentifier,
					Globals.accountContactVerificationCouldNotBeCompleted);
			}

			await AccountCodeService.ConsumeCodeAsync(
				connection,
				transaction,
				codeRecord.AccountCodeId,
				cancellationToken);

			await AccountRepository.UpdateAccountContactAsync(
				connection,
				transaction,
				account.AccountId,
				request.Email.Trim(),
				normalizedEmail,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.ContactRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationContactVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountContactVerificationCouldNotBeCompleted);
		}

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.CodeAccepted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationContactVerification,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.CodeConsumed,
			AccountOutcomes.Consumed,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationContactVerification,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.ContactUpdated,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationContact,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountContactVerificationCompleted),
			Globals.accountContactVerificationCompleted);
	}
}
