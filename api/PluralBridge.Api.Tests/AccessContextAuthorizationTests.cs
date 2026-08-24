// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

public sealed class AccessContextAuthorizationTests
{
	[Fact]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipAccessIsEmpty()
	{
		var systemId = TestGlobals.AccessContextAuthorization.SystemId;
		var systemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;

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
		var currentSystemId = TestGlobals.AccessContextAuthorization.SystemId;
		var wrongMembershipSystemId = TestGlobals.AccessContextAuthorization.WrongSystemId;
		var systemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					wrongMembershipSystemId,
					systemMembershipId,
					isActive: true,
					statusName: TestGlobals.AccessContextAuthorization.ActiveStatusName)
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
		var systemId = TestGlobals.AccessContextAuthorization.SystemId;
		var currentSystemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;
		var wrongMembershipId = TestGlobals.AccessContextAuthorization.WrongMembershipId;

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					systemId,
					wrongMembershipId,
					isActive: true,
					statusName: TestGlobals.AccessContextAuthorization.ActiveStatusName)
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
	[InlineData(false, TestGlobals.AccessContextAuthorization.ActiveStatusName)]
	[InlineData(true, TestGlobals.AccessContextAuthorization.SuspendedStatusName)]
	public void IsAuthorizedForCurrentSystem_ReturnsFalse_WhenMembershipStatusIsNotActive(
		bool isActive,
		string statusName)
	{
		var systemId = TestGlobals.AccessContextAuthorization.SystemId;
		var systemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;

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
		var systemId = TestGlobals.AccessContextAuthorization.SystemId;
		var systemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(),
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: TestGlobals.AccessContextAuthorization.ActiveStatusName)
			],
			new AccessContextHelper.CurrentSystem(
				systemId,
				null,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.True(isAuthorized);
	}

	[Fact]
	public void OwnerMembershipCanAccessCurrentSystem()
	{
		var accountId = TestGlobals.AccessContextAuthorization.AccountId;
		var systemId = TestGlobals.AccessContextAuthorization.SystemId;
		var systemMembershipId = TestGlobals.AccessContextAuthorization.SystemMembershipId;

		var accessContext = new AccessContextHelper.AccessContext(
			CreateAccount(
				accountId,
				TestGlobals.AccessContextAuthorization.DemoOwnerDisplayName),
			[
				CreateMembership(
					systemId,
					systemMembershipId,
					isActive: true,
					statusName: TestGlobals.AccessContextAuthorization.ActiveStatusName,
					accountId)
			],
			new AccessContextHelper.CurrentSystem(
				systemId,
				TestGlobals.AccessContextAuthorization.CurrentSystemName,
				systemMembershipId));

		var isAuthorized = AccessContextHelper.IsAuthorizedForCurrentSystem(
			accessContext);

		Assert.True(isAuthorized);
	}

	private static AccessContextHelper.Account CreateAccount(
		Guid? accountId = null,
		string? displayName = null)
	{
		return new AccessContextHelper.Account(
			accountId ?? TestGlobals.AccessContextAuthorization.AccountId,
			TestGlobals.AccessContextAuthorization.AccountEmail,
			displayName ?? TestGlobals.AccessContextAuthorization.DemoAccountDisplayName,
			TestGlobals.AccessContextAuthorization.ActiveStatusId,
			new AccessContextHelper.AccountStatus(
				TestGlobals.AccessContextAuthorization.ActiveStatusId,
				TestGlobals.AccessContextAuthorization.ActiveStatusName,
				TestGlobals.AccessContextAuthorization.AccountActiveDescription,
				TestGlobals.AccessContextAuthorization.DisplayOrder,
				true),
			DateTime.UtcNow,
			null);
	}

	private static AccessContextHelper.SystemMembership CreateMembership(
		Guid systemId,
		Guid systemMembershipId,
		bool isActive,
		string statusName,
		Guid? accountId = null)
	{
		return new AccessContextHelper.SystemMembership(
			systemMembershipId,
			accountId ?? TestGlobals.AccessContextAuthorization.AccountId,
			systemId,
			TestGlobals.AccessContextAuthorization.ActiveStatusId,
			new AccessContextHelper.MembershipStatus(
				TestGlobals.AccessContextAuthorization.ActiveStatusId,
				statusName,
				TestGlobals.AccessContextAuthorization.MembershipStatusDescription,
				TestGlobals.AccessContextAuthorization.DisplayOrder,
				isActive),
			[
				new AccessContextHelper.Role(
					TestGlobals.AccessContextAuthorization.ActiveStatusId,
					TestGlobals.AccessContextAuthorization.OwnerRoleName,
					TestGlobals.AccessContextAuthorization.OwnerRoleDescription,
					TestGlobals.AccessContextAuthorization.DisplayOrder,
					true)
			],
			DateTime.UtcNow,
			null);
	}
}