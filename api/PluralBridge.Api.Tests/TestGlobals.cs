namespace PluralBridge.Api.Tests;

internal static class TestGlobals
{
	internal static class Projects
	{
		public const string Api = "PluralBridge.Api";
		public const string ApiTests = "PluralBridge.Api.Tests";
	}

	internal static class Environments
	{
		public const string Development = "Development";
		public const string Production = "Production";
	}

	internal static class Configuration
	{
		public const string UserSecretsNotConfigured = "User secrets or test configuration is not configured.";
	}

	internal static class Diagnostics
	{
		public const string RuntimeTestAccountWasNotCreated = "Runtime test account was not created.";
		public const string RuntimeTestAccountWasNotFound = "Runtime test account was not found.";
	}

	internal static class AccessContextAuthorization
	{
		internal static readonly Guid AccountId = Guid.Parse("8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001");
		internal static readonly Guid SystemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		internal static readonly Guid SystemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");
		internal static readonly Guid WrongSystemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		internal static readonly Guid WrongMembershipId = Guid.Parse("22222222-2222-2222-2222-222222222222");

		public const int ActiveStatusId = 1;
		public const int DisplayOrder = 10;

		public const string ActiveStatusName = "Active";
		public const string SuspendedStatusName = "Suspended";

		public const string AccountEmail = "demo@thepluralbridge.local";
		public const string DemoAccountDisplayName = "PluralBridge Demo Account";
		public const string DemoOwnerDisplayName = "Demo Owner";

		public const string AccountActiveDescription = "Account is active and may authenticate and use granted system memberships.";
		public const string MembershipStatusDescription = "Membership status for authorization test.";

		public const string OwnerRoleName = "Owner";
		public const string OwnerRoleDescription = "Full control of the system.";

		public const string CurrentSystemName = "Test System";
	}
}
