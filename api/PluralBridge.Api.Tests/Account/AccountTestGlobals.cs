using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

internal static class AccountTestGlobals
{
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
	}

	internal static class TestAccounts
	{
		public const string UsernamePrefix = "runtime_test_";
		public const string EmailDomain = "example.test";
		public const string DefaultPassword = "RuntimeTestPassword001!";
		public const string ChangedPassword = "RuntimeTestPassword002!";
		public const string DisplayNamePrefix = "Runtime Test ";
	}

	internal static class TestNames
	{
		public const string Startup = "AccountApiHost_Starts";
		public const string DatabaseCanConnect = "AccountTestDatabase_CanConnect";
		public const string Cleanup = "AccountTestCleanup_RemovesRuntimeTestAccount";
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
}
