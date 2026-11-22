// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	/// <summary>
	/// Creates a pending account, stores the password hash, and issues the registration code in one transaction.
	/// Mail is sent after that transaction commits. This method does not sign the caller in.
	/// </summary>
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
				// The account and code row stay committed. The message must not say that registration was rolled back.
				return AccountServiceResult<AccountOperationResponse>.Failed(
					AccountReasonCodes.DeliveryFailed,
					Globals.accountRegistrationDeliveryFailed);
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

}
