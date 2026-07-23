using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed class AccountService : IAccountService
{
	private const int PendingEmailVerificationStatusId = 2;

	private readonly string _connectionString;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IAccountAuditWriter _auditWriter;

	public AccountService(
		IConfiguration configuration,
		IPasswordHasher passwordHasher,
		IAccountAuditWriter auditWriter)
	{
		_connectionString = configuration.GetConnectionString("DefaultConnection")
			?? throw new InvalidOperationException("DefaultConnection is not configured.");

		_passwordHasher = passwordHasher;
		_auditWriter = auditWriter;
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> RegisterAsync(
		RegisterAccountRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.RegistrationRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			null,
			null,
			correlationId,
			"registration",
			cancellationToken);

		if (!IsValidRegistrationRequest(request))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				"registration",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"Registration could not be completed.");
		}

		var normalizedUsername = AccountText.NormalizeUsername(request.Username);
		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

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
					"registration",
					cancellationToken);

				return AccountServiceResult<AccountOperationResponse>.Rejected(
					AccountReasonCodes.DuplicateAccountIdentifier,
					"Registration could not be completed.");
			}

			var accountId = Guid.NewGuid();
			var passwordHash = _passwordHasher.HashPassword(request.Password);
			var verificationCode = AccountCodeService.CreateNumericCode();
			var verificationHash = _passwordHasher.HashPassword(verificationCode);

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

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationCreated,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				"registration",
				cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.CodeIssued,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				accountId,
				accountId,
				correlationId,
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					"Registration was accepted. Verification is required before login."),
				"Registration was accepted.");
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				null,
				null,
				correlationId,
				"registration",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				"Registration could not be completed.");
		}
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> VerifyRegistrationAsync(
			VerifyRegistrationRequest request,
			CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		if (!AccountText.HasText(request.Email) || !AccountText.HasText(request.Code))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"The code could not be accepted.");
		}

		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var codeRecord = await AccountCodeService.ReadLatestRegistrationCodeAsync(
			connection,
			normalizedEmail,
			cancellationToken);

		if (codeRecord is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				null,
				null,
				correlationId,
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				"The code could not be accepted.");
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
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ConsumedCode,
				"The code could not be accepted.");
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
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ExpiredCode,
				"The code expired or could not be accepted.");
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
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				"The code could not be accepted.");
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
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				"The code could not be accepted.");
		}

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
			await transaction.RollbackAsync(cancellationToken);

			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				codeRecord.AccountId,
				codeRecord.AccountId,
				correlationId,
				"registration_verification",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				"The request could not be completed.");
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeAccepted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"registration_verification",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeConsumed,
			AccountOutcomes.Consumed,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"registration_verification",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.RegistrationVerified,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"registration",
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"Registration verification completed."),
			"Registration verification completed.");
	}

	public async Task<AccountServiceResult<LoginResponse>> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		if (!AccountText.HasText(request.UsernameOrEmail) || !AccountText.HasText(request.Password))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				null,
				null,
				correlationId,
				"login",
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				"Login could not be completed.");
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

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
				"login",
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				"Login could not be completed.");
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
				"login",
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				"Login could not be completed.");
		}

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
				"login",
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				"Login could not be completed.");
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
			"login",
			cancellationToken);

		return AccountServiceResult<LoginResponse>.Success(
			new LoginResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"Login completed.",
				accountResponse),
			"Login completed.");
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> ForgotUsernameAsync(
			ForgotUsernameRequest request,
			CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		if (!AccountText.HasText(request.Email))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.UsernameRecoveryRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				"username_recovery",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"If the account can be found, recovery instructions will be sent.");
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
				"username_recovery",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					"If the account can be found, recovery instructions will be sent."),
				"If the account can be found, recovery instructions will be sent.");
		}

		var recoveryCode = AccountCodeService.CreateNumericCode();
		var recoveryHash = _passwordHasher.HashPassword(recoveryCode);

		await AccountCodeService.InsertUsernameRecoveryCodeAsync(
			connection,
			accountId.Value,
			normalizedEmail,
			recoveryHash,
			correlationId,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"username_recovery",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"username_recovery",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"username_recovery",
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"If the account can be found, recovery instructions will be sent."),
				"If the account can be found, recovery instructions will be sent.");
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> ForgotPasswordAsync(
		ForgotPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		if (!AccountText.HasText(request.UsernameOrEmail))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"If the account can be found, password reset instructions will be sent.");
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var accountId = await AccountRepository.ReadAccountIdByNormalizedIdentifierForPasswordResetAsync(
			connection,
			normalizedIdentifier,
			cancellationToken);

		if (accountId is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.PasswordResetRequested,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				null,
				null,
				correlationId,
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					"If the account can be found, password reset instructions will be sent."),
				"If the account can be found, password reset instructions will be sent.");
		}

		var resetCode = AccountCodeService.CreateNumericCode();
		var resetHash = _passwordHasher.HashPassword(resetCode);

		await AccountCodeService.InsertPasswordResetCodeAsync(
			connection,
			accountId.Value,
			normalizedIdentifier,
			resetHash,
			correlationId,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"password_reset",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"password_reset",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			"password_reset",
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"If the account can be found, password reset instructions will be sent."),
			"If the account can be found, password reset instructions will be sent.");
	}
	public async Task<AccountServiceResult<AccountOperationResponse>> ResetPasswordAsync(
		ResetPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"Password reset could not be completed.");
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				"Password reset could not be completed.");
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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ConsumedCode,
				"Password reset could not be completed.");
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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ExpiredCode,
				"Password reset could not be completed.");
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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				"Password reset could not be completed.");
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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCode,
				"Password reset could not be completed.");
		}

		var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

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
				"password_reset",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				"Password reset could not be completed.");
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetCodeAccepted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"password_reset",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeConsumed,
			AccountOutcomes.Consumed,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"password_reset",
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordResetCompleted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			codeRecord.AccountId,
			codeRecord.AccountId,
			correlationId,
			"password_reset",
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"Password reset completed."),
			"Password reset completed.");
	}

	public async Task<AccountServiceResult<AccountOperationResponse>> ChangePasswordAsync(
		Guid actorAccountId,
		ChangePasswordRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N");

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordChangeRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			actorAccountId == Guid.Empty ? null : actorAccountId,
			actorAccountId == Guid.Empty ? null : actorAccountId,
			correlationId,
			"password_change",
			cancellationToken);

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
				"password_change",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				"Password change could not be completed.");
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
				"password_change",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				"Password change could not be completed.");
		}

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
				"password_change",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				"Password change could not be completed.");
		}

		var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

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
				"password_change",
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				"Password change could not be completed.");
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.PasswordChangeCompleted,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			"password_change",
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				"Password change completed."),
			"Password change completed.");
	}

	public Task<AccountServiceResult<AccountResponse>> UpdateProfileAsync(
		Guid actorAccountId,
		UpdateAccountProfileRequest request,
		CancellationToken cancellationToken)
	{
		throw new NotImplementedException("Later Account step will implement profile update.");
	}

	public Task<AccountServiceResult<AccountResponse>> UpdateContactAsync(
		Guid actorAccountId,
		UpdateAccountContactRequest request,
		CancellationToken cancellationToken)
	{
		throw new NotImplementedException("Later Account step will implement contact update.");
	}

	private static bool IsValidRegistrationRequest(RegisterAccountRequest request)
	{
		return AccountText.HasText(request.Username)
			&& AccountText.HasText(request.Email)
			&& AccountText.HasText(request.DisplayName)
			&& AccountText.HasText(request.Password)
			&& request.Username.Trim().Length <= 100
			&& request.Email.Trim().Length <= 320
			&& request.DisplayName.Trim().Length <= 200
			&& request.Password.Length >= 12;
	}

}
