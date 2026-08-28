// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountOperationResponse>> RegisterAsync(
		RegisterAccountRequest request,
		CancellationToken cancellationToken)
	{
		// Every externally visible account operation gets its own correlation id.
		// This is the bridge between Swagger/API results, logs, audit rows, and email delivery telemetry.
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Keep the API response generic while audit records the decision category.
		// Field-specific validation detail can be added to SafeDetailJson without changing the public contract.
		if (!IsValidRegistrationRequest(request))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistration,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountRegistrationCouldNotBeCompleted);
		}

		var normalizedUsername = AccountText.NormalizeUsername(request.Username);
		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		// Registration creates three durable facts together: account, credential, and verification code.
		// Email delivery happens after this transaction commits, so provider failure cannot leave
		// an open database transaction behind.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

		var transactionCommitted = false;

		try
		{
			if (await AccountRepository.AccountIdentifierExistsAsync(connection, transaction, normalizedUsername, normalizedEmail, cancellationToken))
			{
				await transaction.RollbackAsync(cancellationToken);

				await AccountInfrastructure.WriteAuditAsync(_auditWriter,
					AccountAuditEvents.RegistrationRejected,
					AccountOutcomes.Rejected,
					AccountReasonCodes.DuplicateAccountIdentifier,
					null,
					null,
					correlationId,
					Globals.accountOperationRegistration,
					cancellationToken);

				return AccountServiceResult<AccountOperationResponse>.Rejected(
					AccountReasonCodes.DuplicateAccountIdentifier,
					Globals.accountRegistrationCouldNotBeCompleted);
			}

			var accountId = Guid.NewGuid();
			var passwordHash = _passwordHasher.HashPassword(request.Password);
			var verificationCode = AccountCodeService.CreateNumericCode();
			var verificationHash = _passwordHasher.HashPassword(verificationCode);

			// Store only the hash of the verification code. The plaintext value exists only long enough
			// to deliver the email through the configured account-code delivery provider.
			await AccountRepository.InsertAccountAsync(
				connection,
				transaction,
				accountId,
				request.Username.Trim(),
				normalizedUsername,
				request.Email.Trim(),
				normalizedEmail,
				request.DisplayName.Trim(),
				PendingEmailVerificationStatusId,
				cancellationToken);

			await AccountRepository.InsertCredentialAsync(
				connection,
				transaction,
				accountId,
				passwordHash,
				cancellationToken);

			var verificationCodeIssue = await AccountCodeService.InsertVerificationCodeAsync(
				connection,
				transaction,
				accountId,
				normalizedEmail,
				verificationHash,
				correlationId,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			transactionCommitted = true;

			// Delivery failures are converted into account-service results by TryDeliverCodeAsync.
			// This prevents provider/configuration exceptions from escaping through the API request.
			var registrationCodeDelivered = await TryDeliverCodeAsync(
				new AccountCodeDeliveryCommand(
					accountId,
					AccountCodePurposes.RegistrationVerification,
					AccountDestinationTypes.Email,
					normalizedEmail,
					verificationCode,
					correlationId),
				AccountAuditEvents.RegistrationRejected,
				Globals.accountOperationRegistration,
				accountId,
				correlationId,
				cancellationToken);

			if (!registrationCodeDelivered)
			{
				return AccountServiceResult<AccountOperationResponse>.Failed(
					AccountReasonCodes.DeliveryFailed,
					Globals.accountRegistrationCouldNotBeCompleted);
			}

			var registrationCreatedDetailJson = CreateRegistrationCreatedAuditDetailJson(
				accountId,
				normalizedUsername,
				normalizedEmail,
				PendingEmailVerificationStatusId);

			var codeIssuedDetailJson = CreateRegistrationCodeIssuedAuditDetailJson(
				accountId,
				normalizedEmail,
				verificationCodeIssue);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationCreated,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				Globals.accountOperationRegistration,
				registrationCreatedDetailJson,
				cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeIssued,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				codeIssuedDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountRegistrationAcceptedVerificationRequired),
				Globals.accountRegistrationAccepted);
		}
		catch
		{
			// A delivery failure after commit is handled above. This catch is for storage failures
			// while the registration transaction is still open.
			if (!transactionCommitted)
			{
				await transaction.RollbackAsync(cancellationToken);
			}

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistration,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountRegistrationCouldNotBeCompleted);
		}
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> VerifyRegistrationAsync(
		VerifyRegistrationRequest request,
		CancellationToken cancellationToken)
	{
		// Verification gets a new correlation id because it is a separate request from registration.
		// The account/code row links the verification back to the issued code.
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Do not disclose whether the email is known or whether a code exists.
		// SafeDetailJson records which required fields were present.
		if (!AccountText.HasText(request.Email) || !AccountText.HasText(request.Code))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				JsonSerializer.Serialize(new
				{
					emailProvided = AccountText.HasText(request.Email),
					codeProvided = AccountText.HasText(request.Code)
				}),
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountCodeCouldNotBeAccepted);
		}

		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		// The submitted email is the lookup key. A different plus-tagged address selects a different
		// account/code row, which is exactly what happened during the manual Swagger test.
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var codeRecord = await AccountCodeService.ReadLatestRegistrationCodeAsync(
			connection,
			normalizedEmail,
			cancellationToken);

		var nowUtc = DateTime.UtcNow;

		// Null means no registration code exists for this normalized email. We still audit the
		// submitted lookup key, but never the submitted plaintext code.
		if (codeRecord is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				CreateRegistrationVerificationAuditDetailJson(
					normalizedEmail,
					null,
					nowUtc),
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountCodeCouldNotBeAccepted);
		}

		var safeDetailJson = CreateRegistrationVerificationAuditDetailJson(
			normalizedEmail,
			codeRecord,
			nowUtc);

		// The checks below run before hash verification. That means an expired/consumed/rate-limited
		// row can be rejected without revealing whether the submitted code text was correct.
		if (codeRecord.ConsumedAtUtc is not null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Consumed,
				AccountReasonCodes.ConsumedCode,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				safeDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ConsumedCode,
				Globals.accountCodeCouldNotBeAccepted);
		}

		if (codeRecord.ExpiresAtUtc <= nowUtc)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Expired,
				AccountReasonCodes.ExpiredCode,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				safeDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ExpiredCode,
				Globals.accountCodeExpiredOrCouldNotBeAccepted);
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
				Globals.accountOperationRegistrationVerification,
				safeDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				Globals.accountCodeCouldNotBeAccepted);
		}

		// Verification uses the same password hasher interface because account codes are stored as
		// salted hashes, not plaintext. Invalid attempts increment the stored attempt counter.
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
				Globals.accountOperationRegistrationVerification,
				safeDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				Globals.accountCodeCouldNotBeAccepted);
		}

		// Once the code is accepted, consuming the code and activating the account must be atomic.
		// A committed transaction means the code cannot be reused and the account is active together.
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

		try
		{
			await AccountCodeService.ConsumeCodeAsync(
				connection,
				transaction,
				codeRecord.AccountCodeId,
				cancellationToken);

			await AccountRepository.ActivateAccountAsync(
				connection,
				transaction,
				codeRecord.AccountId,
				cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			// Storage failure after a correct code keeps the public response generic while preserving
			// selected code/account detail for operations.
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				safeDetailJson,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountRequestCouldNotBeCompleted);
		}

		// These three audit events intentionally describe the same successful transition from
		// different operational angles: code accepted, code consumed, registration verified.
		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeAccepted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationRegistrationVerification,
			safeDetailJson,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeConsumed,
			AccountOutcomes.Consumed,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationRegistrationVerification,
			safeDetailJson,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.RegistrationVerified,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			Globals.accountOperationRegistration,
			safeDetailJson,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountRegistrationVerificationCompleted),
			Globals.accountRegistrationVerificationCompleted);
	}

	private static bool IsValidRegistrationRequest(RegisterAccountRequest request)
	{
		// Registration validation is intentionally local and cheap. Detailed diagnostics belong in
		// SafeDetailJson, while the public response stays stable and non-enumerating.
		return AccountText.HasText(request.Username)
			&& AccountText.HasText(request.Email)
			&& AccountText.HasText(request.DisplayName)
			&& AccountText.HasText(request.Password)
			&& request.Username.Trim().Length <= 100
			&& request.Email.Trim().Length <= 320
			&& request.DisplayName.Trim().Length <= 200
			&& request.Password.Length >= 12;
	}

	private static string CreateRegistrationCreatedAuditDetailJson(
		Guid accountId,
		string normalizedUsername,
		string normalizedEmail,
		int accountStatusId)
	{
		return JsonSerializer.Serialize(new
		{
			accountId,
			normalizedUsername,
			normalizedEmail,
			accountStatusId
		});
	}

	private static string CreateRegistrationCodeIssuedAuditDetailJson(
		Guid accountId,
		string destinationNormalized,
		AccountCodeIssueRecord codeIssue)
	{
		return JsonSerializer.Serialize(new
		{
			accountId,
			codePurpose = AccountCodePurposes.RegistrationVerification,
			destinationType = AccountDestinationTypes.Email,
			destinationNormalized,
			accountCodeId = codeIssue.AccountCodeId,
			expiresAtUtc = codeIssue.ExpiresAtUtc
		});
	}

	private static string CreateRegistrationVerificationAuditDetailJson(
		string submittedEmailNormalized,
		AccountCodeRecord? codeRecord,
		DateTime nowUtc)
	{
		// This is safe operational detail: lookup key, selected row, and timing state.
		// It deliberately excludes the plaintext verification code and request body.
		return JsonSerializer.Serialize(new
		{
			submittedEmailNormalized,
			codePurpose = AccountCodePurposes.RegistrationVerification,
			destinationType = AccountDestinationTypes.Email,
			selectedAccountCodeId = codeRecord?.AccountCodeId,
			selectedAccountId = codeRecord?.AccountId,
			expiresAtUtc = codeRecord?.ExpiresAtUtc,
			consumedAtUtc = codeRecord?.ConsumedAtUtc,
			attemptCount = codeRecord?.AttemptCount,
			maxAttempts = codeRecord?.MaxAttempts,
			nowUtc
		});
	}
}
