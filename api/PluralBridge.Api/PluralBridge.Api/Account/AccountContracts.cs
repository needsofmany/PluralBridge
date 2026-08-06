namespace PluralBridge.Api.Account;


internal sealed record AccountCodeRecord(
	Guid AccountCodeId,
	Guid AccountId,
	byte[] CodeHash,
	string CodeHashAlgorithm,
	int CodeHashVersion,
	DateTime ExpiresAtUtc,
	DateTime? ConsumedAtUtc,
	int AttemptCount,
	int MaxAttempts);

internal sealed record LoginAccountRecord(
	Guid AccountId,
	string Username,
	string Email,
	string DisplayName,
	int AccountStatusId,
	string AccountStatusName,
	bool IsEmailVerified,
	DateTime CreatedAtUtc,
	DateTime? UpdatedAtUtc,
	DateTime? LastLoginAtUtc,
	byte[] PasswordHash,
	string PasswordHashAlgorithm,
	int PasswordHashVersion);



public sealed record RegisterAccountRequest(
	string Username,
	string Email,
	string DisplayName,
	string Password);

public sealed record VerifyRegistrationRequest(
	string Email,
	string Code);

public sealed record LoginRequest(
	string UsernameOrEmail,
	string Password);

public sealed record ForgotUsernameRequest(
	string Email);

public sealed record ForgotPasswordRequest(
	string UsernameOrEmail);

public sealed record ResetPasswordRequest(
	string UsernameOrEmail,
	string Code,
	string NewPassword);

public sealed record ChangePasswordRequest(
	string CurrentPassword,
	string NewPassword);

public sealed record UpdateAccountProfileRequest(
	string DisplayName);

public sealed record UpdateAccountContactRequest(
	string Email);

public sealed record AccountResponse(
	Guid AccountId,
	string Username,
	string Email,
	string DisplayName,
	bool IsEmailVerified,
	string AccountStatus,
	DateTime CreatedAtUtc,
	DateTime? UpdatedAtUtc,
	DateTime? LastLoginAtUtc);

public sealed record AccountOperationResponse(
	bool Succeeded,
	string Outcome,
	string ReasonCode,
	string Message);

public sealed record LoginResponse(
	bool Succeeded,
	string Outcome,
	string ReasonCode,
	string Message,
	AccountResponse? Account);

public sealed record AccountServiceResult<T>(
	bool Succeeded,
	string Outcome,
	string ReasonCode,
	T? Value,
	string Message)
{
	public static AccountServiceResult<T> Success(T value, string message = "OK")
	{
		return new AccountServiceResult<T>(
			true,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			value,
			message);
	}

	public static AccountServiceResult<T> Rejected(string reasonCode, string message)
	{
		return new AccountServiceResult<T>(
			false,
			AccountOutcomes.Rejected,
			reasonCode,
			default,
			message);
	}

	public static AccountServiceResult<T> Failed(string reasonCode, string message)
	{
		return new AccountServiceResult<T>(
			false,
			AccountOutcomes.Failed,
			reasonCode,
			default,
			message);
	}
}

public static class AccountOutcomes
{
	public const string Succeeded = "succeeded";
	public const string Rejected = "rejected";
	public const string Denied = "denied";
	public const string Expired = "expired";
	public const string Consumed = "consumed";
	public const string Blocked = "blocked";
	public const string Failed = "failed";
	public const string NoOp = "no_op";
}

public static class AccountReasonCodes
{
	public const string None = "none";
	public const string InvalidRequest = "invalid_request";
	public const string InvalidCredentials = "invalid_credentials";
	public const string InvalidCode = "invalid_code";
	public const string ExpiredCode = "expired_code";
	public const string ConsumedCode = "consumed_code";
	public const string PurposeMismatch = "purpose_mismatch";
	public const string PasswordPolicyFailed = "password_policy_failed";
	public const string DuplicateAccountIdentifier = "duplicate_account_identifier";
	public const string AccountUnavailable = "account_unavailable";
	public const string ContactUnavailable = "contact_unavailable";
	public const string OwnershipRequired = "ownership_required";
	public const string MembershipRequired = "membership_required";
	public const string RoleRequired = "role_required";
	public const string SystemUnavailable = "system_unavailable";
	public const string RateLimited = "rate_limited";
	public const string CsrfRejected = "csrf_rejected";
	public const string SessionRequired = "session_required";
	public const string ValidationFailed = "validation_failed";
	public const string StorageFailed = "storage_failed";
	public const string DeliveryFailed = "delivery_failed";
	public const string UnexpectedFailure = "unexpected_failure";
}

public static class AccountAuditEvents
{
	public const string RegistrationRequested = "account.registration.requested";
	public const string RegistrationRejected = "account.registration.rejected";
	public const string RegistrationCreated = "account.registration.created";
	public const string RegistrationVerified = "account.registration.verified";
	public const string RegistrationVerificationRejected = "account.registration.verification_rejected";

	public const string CodeIssued = "account.code.issued";
	public const string CodeAccepted = "account.code.accepted";
	public const string CodeRejected = "account.code.rejected";
	public const string CodeConsumed = "account.code.consumed";

	public const string LoginSucceeded = "account.login.succeeded";
	public const string LoginRejected = "account.login.rejected";
	public const string LogoutSucceeded = "account.logout.succeeded";
	public const string SessionRejected = "account.session.rejected";

	public const string UsernameRecoveryRequested = "account.username_recovery.requested";
	public const string UsernameRecoveryIssued = "account.username_recovery.issued";
	public const string UsernameRecoveryRejected = "account.username_recovery.rejected";
	public const string UsernameRecoveryCompleted = "account.username_recovery.completed";

	public const string PasswordResetRequested = "account.password_reset.requested";
	public const string PasswordResetIssued = "account.password_reset.issued";
	public const string PasswordResetCodeAccepted = "account.password_reset.code_accepted";
	public const string PasswordResetRejected = "account.password_reset.rejected";
	public const string PasswordResetCompleted = "account.password_reset.completed";

	public const string PasswordChangeRequested = "account.password_change.requested";
	public const string PasswordChangeRejected = "account.password_change.rejected";
	public const string PasswordChangeCompleted = "account.password_change.completed";

	public const string ProfileUpdated = "account.profile.updated";
	public const string ProfileRejected = "account.profile.rejected";

	public const string ContactUpdated = "account.contact.updated";
	public const string ContactRejected = "account.contact.rejected";

	public const string OwnershipBootstrapStarted = "account.ownership.bootstrap_started";
	public const string OwnershipSystemCreated = "account.ownership.system_created";
	public const string OwnershipMembershipCreated = "account.ownership.membership_created";
	public const string OwnershipRoleAssigned = "account.ownership.role_assigned";
	public const string OwnershipBootstrapCompleted = "account.ownership.bootstrap_completed";
	public const string OwnershipBootstrapRejected = "account.ownership.bootstrap_rejected";

	public const string OwnedSystemProfileUpdated = "system.owned_profile.updated";
	public const string OwnedSystemProfileRejected = "system.owned_profile.rejected";
	public const string OwnedSystemStatusChanged = "system.owned_status.changed";
	public const string OwnedSystemStatusRejected = "system.owned_status.rejected";
}

public sealed record AccountAuditCommand(
	string EventName,
	string Outcome,
	string ReasonCode,
	Guid? ActorAccountId,
	Guid? TargetAccountId,
	Guid? SystemId,
	Guid? MembershipId,
	string CorrelationId,
	string Source,
	string? SafeSubject,
	string? SafeDetailJson);

public interface IAccountAuditWriter
{
	Task WriteAsync(AccountAuditCommand command, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
	PasswordHashResult HashPassword(string password);

	bool VerifyPassword(string password, byte[] passwordHash, string algorithm, int version);
}

public sealed record PasswordHashResult(
	byte[] PasswordHash,
	string Algorithm,
	int Version);

public interface IAccountService
{
	Task<AccountServiceResult<AccountOperationResponse>> RegisterAsync(
		RegisterAccountRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountOperationResponse>> VerifyRegistrationAsync(
		VerifyRegistrationRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<LoginResponse>> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountOperationResponse>> ForgotUsernameAsync(
		ForgotUsernameRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountOperationResponse>> ForgotPasswordAsync(
		ForgotPasswordRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountOperationResponse>> ResetPasswordAsync(
		ResetPasswordRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountOperationResponse>> ChangePasswordAsync(
		Guid actorAccountId,
		ChangePasswordRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountResponse>> UpdateProfileAsync(
		Guid actorAccountId,
		UpdateAccountProfileRequest request,
		CancellationToken cancellationToken);

	Task<AccountServiceResult<AccountResponse>> UpdateContactAsync(
		Guid actorAccountId,
		UpdateAccountContactRequest request,
		CancellationToken cancellationToken);
}