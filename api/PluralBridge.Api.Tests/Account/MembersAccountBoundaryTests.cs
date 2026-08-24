// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class MembersAccountBoundaryTests
{
	[Fact]
	public async Task AuthenticatedAccounts_ReadOnlyMembersFromTheirOwnSystems()
	{
		var testId = Guid.NewGuid().ToString("N");
		var accountA = CreateAccountValues("A", testId);
		var accountB = CreateAccountValues("B", testId);

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

			var fixtureA = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountAId,
				accountA.SystemName,
				accountA.MemberDisplayName);

			var fixtureB = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountBId,
				accountB.SystemName,
				accountB.MemberDisplayName);

			await LoginAsync(clientA, accountA.Username);
			await LoginAsync(clientB, accountB.Username);

			var responseA = await clientA.GetAsync(BuildMembersRoute(fixtureA.SystemId));
			var responseB = await clientB.GetAsync(BuildMembersRoute(fixtureB.SystemId));

			var responseAText = await responseA.Content.ReadAsStringAsync();
			var responseBText = await responseB.Content.ReadAsStringAsync();

			Assert.True(responseA.IsSuccessStatusCode, responseAText);
			Assert.True(responseB.IsSuccessStatusCode, responseBText);

			Assert.Contains(accountA.MemberDisplayName, responseAText);
			Assert.DoesNotContain(accountB.MemberDisplayName, responseAText);
			Assert.Contains(accountB.MemberDisplayName, responseBText);
			Assert.DoesNotContain(accountA.MemberDisplayName, responseBText);

			var crossSystemResponse = await clientA.GetAsync(
				BuildMembersRoute(fixtureB.SystemId));

			Assert.Equal(HttpStatusCode.Forbidden, crossSystemResponse.StatusCode);
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
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}members_{label}_{testId}";

		return new RuntimeAccountValues(
			username,
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}",
			$"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Members {label}",
			$"RUNTIME_TEST_SYSTEM_{label}_{testId}",
			$"Runtime Test Member {label} {testId}");
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

	private static async Task LoginAsync(
		HttpClient client,
		string username)
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

	private sealed record RuntimeAccountValues(
		string Username,
		string Email,
		string DisplayName,
		string SystemName,
		string MemberDisplayName);
}
