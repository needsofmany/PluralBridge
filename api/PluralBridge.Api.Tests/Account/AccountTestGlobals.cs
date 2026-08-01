using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

internal static class AccountTestGlobals
{
	internal static class Formats
	{
		public const string CompactGuid = "N";
	}

	internal static class Routes
	{
		public const string Swagger = "/swagger";
		public const string Register = "/api/account/register";
		public const string VerifyRegistration = "/api/account/verify-registration";
		public const string Login = "/api/account/login";
		public const string ForgotUsername = "/api/account/forgot-username";
		public const string ForgotPassword = "/api/account/forgot-password";
		public const string ResetPassword = "/api/account/reset-password";
	}

	internal static class TestAccounts
	{
		public const string UsernamePrefix = "runtime_test_";
		public const string EmailDomain = "example.test";
		public const string DefaultPassword = "RuntimeTestPassword001!";
		public const string ChangedPassword = "RuntimeTestPassword002!";
		public const string DisplayNamePrefix = "Runtime Test ";
		public const string ResetPasswordValidCodeUsernameSegment = "reset_password_valid_code_";
		public const string ResetPasswordValidCodeDisplayName = DisplayNamePrefix + "Reset Password Valid Code";
		public const string ResetPasswordConsumesCodeUsernameSegment = "reset_password_consumes_code_";
		public const string ResetPasswordConsumesCodeDisplayName = DisplayNamePrefix + "Reset Password Consumes Code";
		public const string ResetPasswordInvalidCodeUsernameSegment = "reset_password_invalid_code_";
		public const string ResetPasswordInvalidCodeDisplayName = DisplayNamePrefix + "Reset Password Invalid Code";
		public const string ResetPasswordExpiredCodeUsernameSegment = "reset_password_expired_code_";
		public const string ResetPasswordExpiredCodeDisplayName = DisplayNamePrefix + "Reset Password Expired Code";
		public const string ResetPasswordConsumedCodeUsernameSegment = "reset_password_consumed_code_";
		public const string ResetPasswordConsumedCodeDisplayName = DisplayNamePrefix + "Reset Password Consumed Code";
		public const string ResetPasswordAttemptLimitUsernameSegment = "reset_password_attempt_limit_";
		public const string ResetPasswordAttemptLimitDisplayName = DisplayNamePrefix + "Reset Password Attempt Limit";
		public const string ResetPasswordOldPasswordUsernameSegment = "reset_password_old_password_";
		public const string ResetPasswordOldPasswordDisplayName = DisplayNamePrefix + "Reset Password Old Password";
		public const string ResetPasswordAuditRowsUsernameSegment = "reset_password_audit_rows_";
		public const string ResetPasswordAuditRowsDisplayName = DisplayNamePrefix + "Reset Password Audit Rows";
	}

	internal static class Database
	{
		public const string DefaultConnectionName = AccountConfigurationKeys.ConnectionStringName;
	}

	internal static class CodePurposes
	{
		public const string RegistrationVerification = "registration_verification";
	}

	internal static class DestinationTypes
	{
		public const string Email = "email";
	}

	internal static class Collections
	{
		public const string AccountDatabase = "Account database";
	}

	internal static class Diagnostics
	{
		public const string PasswordResetCodeDeliveryWasNotFound = "Password-reset code delivery was not found.";
		public const string PasswordResetResponseBodyWasNotReturned = "Password-reset response body was not returned.";
		public const string LoginResponseBodyWasNotReturned = "Login response body was not returned.";
	}
}
