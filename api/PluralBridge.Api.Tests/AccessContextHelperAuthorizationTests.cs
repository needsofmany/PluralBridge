// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Tests;

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
					statusName: TestGlobals.AccessContextHelperAuthorization.ActiveStatusName)
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
					statusName: TestGlobals.AccessContextHelperAuthorization.ActiveStatusNameLowercase)
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
					statusName: TestGlobals.AccessContextHelperAuthorization.ActiveStatusName)
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
					statusName: TestGlobals.AccessContextHelperAuthorization.ActiveStatusName)
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
					statusName: TestGlobals.AccessContextHelperAuthorization.ActiveStatusName)
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
					statusName: TestGlobals.AccessContextHelperAuthorization.InactiveStatusName)
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
			TestGlobals.AccessContextHelperAuthorization.AccountEmail,
			TestGlobals.AccessContextHelperAuthorization.DemoAccountDisplayName,
			TestGlobals.AccessContextHelperAuthorization.ActiveStatusId,
			new AccessContextHelper.AccountStatus(
				TestGlobals.AccessContextHelperAuthorization.ActiveStatusId,
				TestGlobals.AccessContextHelperAuthorization.ActiveStatusName,
				TestGlobals.AccessContextHelperAuthorization.ActiveStatusName,
				TestGlobals.AccessContextHelperAuthorization.DisplayOrder,
				true),
			DateTime.UtcNow,
			null);

		var currentSystem = new AccessContextHelper.CurrentSystem(
			currentSystemId,
			TestGlobals.AccessContextHelperAuthorization.CurrentSystemName,
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
			TestGlobals.AccessContextHelperAuthorization.ActiveStatusId,
			statusName,
			statusName,
			TestGlobals.AccessContextHelperAuthorization.DisplayOrder,
			isActive);

		return new AccessContextHelper.SystemMembership(
			systemMembershipId,
			Guid.NewGuid(),
			systemId,
			TestGlobals.AccessContextHelperAuthorization.ActiveStatusId,
			membershipStatus,
			[],
			DateTime.UtcNow,
			null);
	}
}
