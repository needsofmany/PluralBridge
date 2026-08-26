// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

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
		public const string ChangePassword = "/api/account/change-password";
		public const string Profile = "/api/account/profile";
		public const string Contact = "/api/account/contact";
		public const string VerifyContact = "/api/account/verify-contact";
		public const string HtmlLogin = "/login";
		public const string App = "/app/";
		public const string Me = "/api/me";
	}

	internal static class FormFields
	{
		public const string UserName = "userName";
		public const string Password = "password";
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
		public const string ChangePasswordValidUsernameSegment = "change_password_valid_";
		public const string ChangePasswordValidDisplayName = DisplayNamePrefix + "Change Password Valid";
		public const string ChangePasswordWrongCurrentUsernameSegment = "change_password_wrong_current_";
		public const string ChangePasswordWrongCurrentDisplayName = DisplayNamePrefix + "Change Password Wrong Current";
		public const string WrongPassword = "RuntimeTestWrongPassword001!";
		public const string ChangePasswordOldPasswordUsernameSegment = "change_password_old_password_";
		public const string ChangePasswordOldPasswordDisplayName = DisplayNamePrefix + "Change Password Old Password";
		public const string ChangePasswordShortPasswordUsernameSegment = "change_password_short_password_";
		public const string ChangePasswordShortPasswordDisplayName = DisplayNamePrefix + "Change Password Short Password";
		public const string TooShortPassword = "Short1!";
		public const string ChangePasswordAuditRowsUsernameSegment = "change_password_audit_rows_";
		public const string ChangePasswordAuditRowsDisplayName = DisplayNamePrefix + "Change Password Audit Rows";
		public const string ChangePasswordUnavailableUsernameSegment = "change_password_unavailable_";
		public const string ChangePasswordUnavailableDisplayName = DisplayNamePrefix + "Change Password Unavailable";
		public const string ProfileUpdateUsernameSegment = "profile_update_";
		public const string ProfileUpdateDisplayName = DisplayNamePrefix + "Profile Update";
		public const string UpdatedDisplayName = "Updated Runtime Test Profile";
		public const string ProfileInvalidUsernameSegment = "profile_invalid_";
		public const string ProfileInvalidDisplayName = DisplayNamePrefix + "Profile Invalid";
		public const string ProfileUnavailableUsernameSegment = "profile_unavailable_";
		public const string ProfileUnavailableDisplayName = DisplayNamePrefix + "Profile Unavailable";
		public const string ProfileAuditUsernameSegment = "profile_audit_";
		public const string ProfileAuditDisplayName = DisplayNamePrefix + "Profile Audit";
		public const string ContactUpdateUsernameSegment = "contact_update_";
		public const string ContactUpdateDisplayName = DisplayNamePrefix + "Contact Update";
		public const string HtmlLoginUsernameSegment = "html_login_";
		public const string HtmlLoginDisplayName = DisplayNamePrefix + "HTML Login";
		public const string HtmlLoginSystemNamePrefix = "RUNTIME_TEST_SYSTEM_HTML_LOGIN_";
		public const string HtmlLoginMemberDisplayNamePrefix = "Runtime Test HTML Login Member ";
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
		public const string ResponseBodyWasNotReturned = " response body was not returned.";
		public const string PasswordResetCodeDeliveryWasNotFound = "Password-reset code delivery was not found.";
		public const string PasswordResetResponseBodyWasNotReturned = "Password-reset" + ResponseBodyWasNotReturned;
		public const string LoginResponseBodyWasNotReturned = "Login" + ResponseBodyWasNotReturned;
		public const string ChangePasswordResponseBodyWasNotReturned = "Change-password" + ResponseBodyWasNotReturned;
		public const string ProfileResponseBodyWasNotReturned = "Profile" + ResponseBodyWasNotReturned;
		public const string ContactResponseBodyWasNotReturned = "Contact" + ResponseBodyWasNotReturned;
	}
}
