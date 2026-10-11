// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PluralBridge.Api;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class MembersWriteAuthorizationTests
{
	[Fact]
	public async Task MemberCreate_WithOwnerRole_Succeeds()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues("owner_create", testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var accountId = await RegisterAndActivateAsync(client, account);
			var fixture = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountId,
				account.SystemName,
				account.MemberDisplayName,
				TestGlobals.AccessContextAuthorization.OwnerRoleName);

			await LoginAsync(client, account.Username);

			var response = await client.PostAsJsonAsync(
				BuildMembersRoute(fixture.SystemId),
				new
				{
					DisplayName = $"Created Member {testId}"
				});

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task MemberEdit_WithOwnerRole_Succeeds()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues("owner_edit", testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var accountId = await RegisterAndActivateAsync(client, account);
			var fixture = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountId,
				account.SystemName,
				account.MemberDisplayName,
				TestGlobals.AccessContextAuthorization.OwnerRoleName);

			await LoginAsync(client, account.Username);

			var updatedDisplayName = $"Updated Member {testId}";

			var response = await client.PutAsJsonAsync(
				BuildMemberRoute(fixture.SystemId, fixture.MemberId),
				new
				{
					DisplayName = updatedDisplayName
				});

			var responseText = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains(updatedDisplayName, responseText);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task MemberWrite_WithActiveMembershipButViewerRole_IsForbidden()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues("viewer_forbidden", testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var accountId = await RegisterAndActivateAsync(client, account);
			var fixture = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountId,
				account.SystemName,
				account.MemberDisplayName,
				"Viewer");

			await LoginAsync(client, account.Username);

			var createResponse = await client.PostAsJsonAsync(
				BuildMembersRoute(fixture.SystemId),
				new
				{
					DisplayName = $"Should Not Create {testId}"
				});

			var editResponse = await client.PutAsJsonAsync(
				BuildMemberRoute(fixture.SystemId, fixture.MemberId),
				new
				{
					DisplayName = $"Should Not Edit {testId}"
				});

			Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
			Assert.Equal(HttpStatusCode.Forbidden, editResponse.StatusCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task MemberWrite_CrossSystem_IsForbidden()
	{
		var testId = Guid.NewGuid().ToString("N");
		var accountA = CreateAccountValues("cross_a", testId);
		var accountB = CreateAccountValues("cross_b", testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var clientA = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
			using var clientB = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var accountAId = await RegisterAndActivateAsync(clientA, accountA);
			var accountBId = await RegisterAndActivateAsync(clientB, accountB);

			_ = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountAId,
				accountA.SystemName,
				accountA.MemberDisplayName,
				TestGlobals.AccessContextAuthorization.OwnerRoleName);

			var fixtureB = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountBId,
				accountB.SystemName,
				accountB.MemberDisplayName,
				TestGlobals.AccessContextAuthorization.OwnerRoleName);

			await LoginAsync(clientA, accountA.Username);

			var createResponse = await clientA.PostAsJsonAsync(
				BuildMembersRoute(fixtureB.SystemId),
				new
				{
					DisplayName = $"Cross System Create {testId}"
				});

			var editResponse = await clientA.PutAsJsonAsync(
				BuildMemberRoute(fixtureB.SystemId, fixtureB.MemberId),
				new
				{
					DisplayName = $"Cross System Edit {testId}"
				});

			Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
			Assert.Equal(HttpStatusCode.Forbidden, editResponse.StatusCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task MemberWrite_Unauthenticated_IsUnauthorized()
	{
		var testId = Guid.NewGuid().ToString("N");

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var systemId = Guid.NewGuid();
			var memberId = Guid.NewGuid();

			var createResponse = await client.PostAsJsonAsync(
				BuildMembersRoute(systemId),
				new
				{
					DisplayName = $"Unauthenticated Create {testId}"
				});

			var editResponse = await client.PutAsJsonAsync(
				BuildMemberRoute(systemId, memberId),
				new
				{
					DisplayName = $"Unauthenticated Edit {testId}"
				});

			Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
			Assert.Equal(HttpStatusCode.Unauthorized, editResponse.StatusCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	private static RuntimeAccountValues CreateAccountValues(
		string label,
		string testId)
	{
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}members_write_{label}_{testId}";

		return new RuntimeAccountValues(
			username,
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}",
			$"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Members Write {label}",
			$"RUNTIME_TEST_SYSTEM_WRITE_{label}_{testId}",
			$"Runtime Test Member Write {label} {testId}");
	}

	private static async Task<Guid> RegisterAndActivateAsync(
		HttpClient client,
		RuntimeAccountValues account)
	{
		var registerRequest = new RegisterAccountRequest(
			account.Username,
			account.Email,
			account.DisplayName,
			AccountTestGlobals.TestAccounts.DefaultPassword);

		var registerResponse = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Register,
			registerRequest);

		Assert.True(
			registerResponse.IsSuccessStatusCode,
			await registerResponse.Content.ReadAsStringAsync());

		var accountState = await AccountTestDatabase.ReadAccountStateAsync(account.Username)
			?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

		await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountState.AccountId);

		return accountState.AccountId;
	}

	private static async Task LoginAsync(HttpClient client, string username)
	{
		var loginRequest = new LoginRequest(
			username,
			AccountTestGlobals.TestAccounts.DefaultPassword);

		var loginResponse = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Login,
			loginRequest);

		Assert.True(
			loginResponse.IsSuccessStatusCode,
			await loginResponse.Content.ReadAsStringAsync());
	}

	private static string BuildMembersRoute(Guid systemId)
	{
		return Globals.membersRoute
			.Replace(
				"{systemId:guid}",
				systemId.ToString(),
				StringComparison.OrdinalIgnoreCase)
			.Replace(
				"{systemId}",
				systemId.ToString(),
				StringComparison.OrdinalIgnoreCase);
	}

	private static string BuildMemberRoute(Guid systemId, Guid memberId)
	{
		return BuildMembersRoute(systemId) + "/" + memberId;
	}

	private sealed record RuntimeAccountValues(
		string Username,
		string Email,
		string DisplayName,
		string SystemName,
		string MemberDisplayName);
}
