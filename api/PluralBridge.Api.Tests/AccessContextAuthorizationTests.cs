using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

public sealed class AccessContextAuthorizationTests
{
	[Fact]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipAccessIsEmpty()
	{
		var systemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		var systemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[],
			new AccessContextHelper.CurrentSystem(
				systemId,
				null,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipSystemIdDoesNotMatchCurrentSystem()
	{
		var currentSystemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		var wrongMembershipSystemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var systemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					wrongMembershipSystemId,
					systemMembershipId,
					isActive: true,
					statusName: "Active")
			],
			new AccessContextHelper.CurrentSystem(
				currentSystemId,
				null,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipIdDoesNotMatchCurrentSystem()
	{
		var systemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		var currentSystemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");
		var wrongMembershipId = Guid.Parse("22222222-2222-2222-2222-222222222222");

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					systemId,
					wrongMembershipId,
					isActive: true,
					statusName: "Active")
			],
			new AccessContextHelper.CurrentSystem(
				systemId,
				null,
				currentSystemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.False(isAuthorized);
	}

	[Theory]
	[InlineData(false, "Active")]
	[InlineData(true, "Suspended")]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipStatusIsNotActive(
		bool isActive,
		string statusName)
	{
		var systemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		var systemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive,
					statusName)
			],
			new AccessContextHelper.CurrentSystem(
				systemId,
				null,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_ReturnsTrue_WhenMembershipMatchesCurrentSystemAndIsActive()
	{
		var systemId = Guid.Parse("826d77cf-8b1a-a301-4efe-1113e5a17e88");
		var systemMembershipId = Guid.Parse("7f7f0d8c-08d4-42df-9f0a-8db13d2d0009");

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: "Active")
			],
			new AccessContextHelper.CurrentSystem(
				systemId,
				null,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.True(isAuthorized);
	}

	private static AccessContextHelper.Account CreateAccount()
	{
		return new AccessContextHelper.Account(
			Guid.Parse("8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001"),
			"demo@thepluralbridge.local",
			"PluralBridge Demo Account",
			1,
			new AccessContextHelper.AccountStatus(
				1,
				"Active",
				"Account is active and may authenticate and use granted system memberships.",
				10,
				true),
			DateTime.UtcNow,
			null);
	}

	private static AccessContextHelper.SystemMembership CreateMembership(
		Guid systemId,
		Guid systemMembershipId,
		bool isActive,
		string statusName)
	{
		return new AccessContextHelper.SystemMembership(
			systemMembershipId,
			Guid.Parse("8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001"),
			systemId,
			1,
			new AccessContextHelper.MembershipStatus(
				1,
				statusName,
				"Membership status for authorization test.",
				10,
				isActive),
			[
				new AccessContextHelper.Role(
					1,
					"Owner",
					"Full control of the system.",
					10,
					true)
			],
			DateTime.UtcNow,
			null);
	}

	[Fact]
	public void OwnerMembershipCanAccessCurrentSystem()
	{
		var accountId = Guid.NewGuid();
		var systemId = Guid.NewGuid();
		var systemMembershipId = Guid.NewGuid();

		var accountStatus = new AccessContextHelper.AccountStatus(
			AccountStatusId: 1,
			StatusName: "Active",
			StatusDesc: "Active",
			DisplayOrder: 1,
			IsActive: true);

		var account = new AccessContextHelper.Account(
			AccountId: accountId,
			Email: "demo@thepluralbridge.local",
			DisplayName: "Demo Owner",
			AccountStatusId: 1,
			AccountStatus: accountStatus,
			CreatedAtUtc: DateTime.UtcNow,
			UpdatedAtUtc: null);

		var membershipStatus = new AccessContextHelper.MembershipStatus(
			MembershipStatusId: 1,
			StatusName: "Active",
			StatusDesc: "Active",
			DisplayOrder: 1,
			IsActive: true);

		var ownerRole = new AccessContextHelper.Role(
			RoleId: 1,
			RoleName: "Owner",
			RoleDesc: "Owner",
			DisplayOrder: 1,
			IsActive: true);

		var membership = new AccessContextHelper.SystemMembership(
			SystemMembershipId: systemMembershipId,
			AccountId: accountId,
			SystemId: systemId,
			MembershipStatusId: 1,
			MembershipStatus: membershipStatus,
			Roles: [ownerRole],
			CreatedAtUtc: DateTime.UtcNow,
			UpdatedAtUtc: null);

		var currentSystem = new AccessContextHelper.CurrentSystem(
			SystemId: systemId,
			SystemName: "Test System",
			SystemMembershipId: systemMembershipId);

		var accessContext = new AccessContextHelper.AccessContext(
			CurrentAccount: account,
			MembershipAccess: [membership],
			CurrentSystem: currentSystem);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.True(isAuthorized);
	}

}
