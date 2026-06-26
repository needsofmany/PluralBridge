using Xunit;

// ReSharper disable once CheckNamespace
namespace PluralBridge.Api.Controllers;

public sealed class AccessContextHelperAuthorizationTests
{
	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_true_for_active_matching_membership()
	{
		var systemId = Guid.NewGuid();
		var systemMembershipId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			systemId,
			systemMembershipId,
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: "Active")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.True(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_true_for_active_matching_membership_with_status_name_case_difference()
	{
		var systemId = Guid.NewGuid();
		var systemMembershipId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			systemId,
			systemMembershipId,
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: "active")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.True(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_false_when_membership_access_is_empty()
	{
		var accessContext = CreateAccessContext(
			Guid.NewGuid(),
			Guid.NewGuid(),
			[]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_false_when_system_id_does_not_match()
	{
		var systemMembershipId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			Guid.NewGuid(),
			systemMembershipId,
			[
				CreateMembership(
					Guid.NewGuid(),
					systemMembershipId,
					isActive: true,
					statusName: "Active")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_false_when_system_membership_id_does_not_match()
	{
		var systemId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			systemId,
			Guid.NewGuid(),
			[
				CreateMembership(
					systemId,
					Guid.NewGuid(),
					isActive: true,
					statusName: "Active")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_false_when_membership_status_is_not_active()
	{
		var systemId = Guid.NewGuid();
		var systemMembershipId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			systemId,
			systemMembershipId,
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: false,
					statusName: "Active")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.False(isAuthorized);
	}

	[Fact]
	public void IsAuthorizedForCurrentSystem_returns_false_when_membership_status_name_is_not_active()
	{
		var systemId = Guid.NewGuid();
		var systemMembershipId = Guid.NewGuid();

		var accessContext = CreateAccessContext(
			systemId,
			systemMembershipId,
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: "Inactive")
			]);

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext);

		Assert.False(isAuthorized);
	}

	private static AccessContextHelper.AccessContext CreateAccessContext(
		Guid currentSystemId,
		Guid currentSystemMembershipId,
		IReadOnlyList<AccessContextHelper.SystemMembership> membershipAccess)
	{
		var account = new AccessContextHelper.Account(
			Guid.NewGuid(),
			"demo@thepluralbridge.local",
			"Demo Account",
			1,
			new AccessContextHelper.AccountStatus(
				1,
				"Active",
				"Active",
				1,
				true),
			DateTime.UtcNow,
			null);

		var currentSystem = new AccessContextHelper.CurrentSystem(
			currentSystemId,
			"Current System",
			currentSystemMembershipId);

		return new AccessContextHelper.AccessContext(
			account,
			membershipAccess,
			currentSystem);
	}

	private static AccessContextHelper.SystemMembership CreateMembership(
		Guid systemId,
		Guid systemMembershipId,
		bool isActive,
		string statusName)
	{
		var membershipStatus = new AccessContextHelper.MembershipStatus(
			1,
			statusName,
			statusName,
			1,
			isActive);

		return new AccessContextHelper.SystemMembership(
			systemMembershipId,
			Guid.NewGuid(),
			systemId,
			1,
			membershipStatus,
			[],
			DateTime.UtcNow,
			null);
	}
}