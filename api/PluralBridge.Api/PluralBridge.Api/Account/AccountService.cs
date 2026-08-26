// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;
using PluralBridge.Api;
using System.Text.Json;

namespace PluralBridge.Api.Account;

// Coordinates the account workflows and keeps the controllers thin.
// Persistence, hashing, auditing, and code delivery stay behind narrow helper interfaces.
public sealed class AccountService : IAccountService
{
	private const int PendingEmailVerificationStatusId = 2;

	private readonly string _connectionString;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IAccountAuditWriter _auditWriter;
	private readonly IAccountCodeDelivery _codeDelivery;

	// ReSharper disable once ConvertToPrimaryConstructor
	public AccountService(
		IConfiguration configuration,
		IPasswordHasher passwordHasher,
		IAccountAuditWriter auditWriter,
		IAccountCodeDelivery codeDelivery)
	{
		_connectionString = configuration.GetConnectionString(AccountConfigurationKeys.ConnectionStringName)
		                    ?? throw new InvalidOperationException(Globals.missingConnStringDetail);

		_passwordHasher = passwordHasher;
		_auditWriter = auditWriter;
		_codeDelivery = codeDelivery;
	}

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

			await AccountCodeService.InsertVerificationCodeAsync(
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

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationCreated,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				Globals.accountOperationRegistration,
				cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeIssued,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
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

	public async Task<AccountServiceResult<LoginResponse>> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Login failures intentionally collapse to generic invalid/unavailable responses.
		// The audit path is where operations can distinguish validation, status, and hash failures.
		if (!AccountText.HasText(request.UsernameOrEmail) || !AccountText.HasText(request.Password))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				null,
				null,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

		// The repository handles either username or email lookup using the normalized identifier.
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var account = await AccountRepository.ReadLoginAccountAsync(
			connection,
			normalizedIdentifier,
			cancellationToken);

		if (account is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				null,
				null,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		if (account.AccountStatusId != 1 || !account.IsEmailVerified)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountLoginCouldNotBeCompleted);
		}

		// Password comparison stays inside the hasher so algorithm/version checks remain centralized.
		var passwordAccepted = _passwordHasher.VerifyPassword(
			request.Password,
			account.PasswordHash,
			account.PasswordHashAlgorithm,
			account.PasswordHashVersion);

		if (!passwordAccepted)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		await AccountRepository.UpdateLastLoginAsync(
			connection,
			account.AccountId,
			cancellationToken);

		var accountResponse = new AccountResponse(
			account.AccountId,
			account.Username,
			account.Email,
			account.DisplayName,
			account.IsEmailVerified,
			account.AccountStatusName,
			account.CreatedAtUtc,
			account.UpdatedAtUtc,
			DateTime.UtcNow);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.LoginSucceeded,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationLogin,
			cancellationToken);

		return AccountServiceResult<LoginResponse>.Success(
			new LoginResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountLoginCompleted,
				accountResponse),
			Globals.accountLoginCompleted);
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> ForgotUsernameAsync(
			ForgotUsernameRequest request,
			CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Username recovery is account-enumeration resistant. Unknown email addresses receive the
		// same public success message as known addresses.
		if (!AccountText.HasText(request.Email))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.UsernameRecoveryRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationUsernameRecovery,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var accountId = await AccountRepository.ReadAccountIdByNormalizedEmailAsync(
			connection,
			normalizedEmail,
			cancellationToken);

		if (accountId is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.UsernameRecoveryRequested,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				null,
				null,
				correlationId,
				Globals.accountOperationUsernameRecovery,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountUsernameRecoveryInstructionsSent),
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		var recoveryCode = AccountCodeService.CreateNumericCode();
		var recoveryHash = _passwordHasher.HashPassword(recoveryCode);

		// Store the recovery code hash first, then deliver the plaintext code through the provider.
		await AccountCodeService.InsertUsernameRecoveryCodeAsync(
			connection,
			accountId.Value,
			normalizedEmail,
			recoveryHash,
			correlationId,
			cancellationToken);

		var usernameRecoveryCodeDelivered = await TryDeliverCodeAsync(
			new AccountCodeDeliveryCommand(
				accountId.Value,
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail,
				recoveryCode,
				correlationId),
			AccountAuditEvents.UsernameRecoveryRejected,
			Globals.accountOperationUsernameRecovery,
			accountId.Value,
			correlationId,
			cancellationToken);

		if (!usernameRecoveryCodeDelivered)
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountUsernameRecoveryInstructionsSent),
				Globals.accountUsernameRecoveryInstructionsSent);
	}
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

	private async Task<bool> TryDeliverCodeAsync(
		AccountCodeDeliveryCommand command,
		string failureAuditEventName,
		string operationName,
		Guid accountId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		try
		{
			// Provider-specific delivery exceptions are contained here so workflows can return a
			// controlled service result and still write a failure audit row.
			await _codeDelivery.DeliverAsync(command, cancellationToken);

			return true;
		}
		catch
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				failureAuditEventName,
				AccountOutcomes.Failed,
				AccountReasonCodes.DeliveryFailed,
				accountId,
				accountId,
				correlationId,
				operationName,
				cancellationToken);

			return false;
		}
	}
}
