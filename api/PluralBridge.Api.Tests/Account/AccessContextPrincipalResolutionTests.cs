// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Account;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccessContextPrincipalResolutionTests
{
	[Theory]
	[InlineData("A")]
	[InlineData("B")]
	public async Task ResolveCurrentAccessAsync_UsesNameIdentifierToResolveTheAccountMembershipAndSystem(
		string fixtureLabel)
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}access_{fixtureLabel}_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Access {fixtureLabel}";
		var systemName = $"RUNTIME_TEST_SYSTEM_{fixtureLabel}_{testId}";
		var memberDisplayName = $"Runtime Test Member {fixtureLabel} {testId}";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			var accountId = await RegisterAndActivateAccountAsync(
				username,
				email,
				displayName);

			var fixture = await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountId,
				systemName,
				memberDisplayName);

			var principal = CreatePrincipal(accountId.ToString());
			var connectionString = AccountTestDatabase.GetConnectionString();

			await using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync();

			var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(
				connection,
				principal);

			Assert.NotNull(accessContext);
			Assert.Equal(accountId, accessContext.CurrentAccount.AccountId);
			Assert.Equal(fixture.SystemId, accessContext.CurrentSystem.SystemId);
			Assert.Equal(
				fixture.SystemMembershipId,
				accessContext.CurrentSystem.SystemMembershipId);

			var membership = Assert.Single(accessContext.MembershipAccess);

			Assert.Equal(accountId, membership.AccountId);
			Assert.Equal(fixture.SystemId, membership.SystemId);
			Assert.Equal(fixture.SystemMembershipId, membership.SystemMembershipId);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResolveCurrentAccountAsync_ReturnsNull_WhenNameIdentifierIsMissing()
	{
		await using var connection = new SqlConnection();
		var principal = new ClaimsPrincipal(new ClaimsIdentity());

		var account = await AccessContextHelper.ResolveCurrentAccountAsync(
			connection,
			principal);

		Assert.Null(account);
	}

	[Fact]
	public async Task ResolveCurrentAccountAsync_ReturnsNull_WhenNameIdentifierIsMalformed()
	{
		await using var connection = new SqlConnection();
		var principal = CreatePrincipal("not-a-guid");

		var account = await AccessContextHelper.ResolveCurrentAccountAsync(
			connection,
			principal);

		Assert.Null(account);
	}

	private static ClaimsPrincipal CreatePrincipal(string accountId)
	{
		var identity = new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, accountId)
			],
			"Test");

		return new ClaimsPrincipal(identity);
	}

	private static async Task<Guid> RegisterAndActivateAccountAsync(
		string username,
		string email,
		string displayName)
	{
		await using var factory = AccountTestHost.CreateFactory();
		using var client = factory.CreateClient();

		var request = new RegisterAccountRequest(
			username,
			email,
			displayName,
			AccountTestGlobals.TestAccounts.DefaultPassword);

		var response = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Register,
			request);

		Assert.True(
			response.IsSuccessStatusCode,
			await response.Content.ReadAsStringAsync());

		var account = await AccountTestDatabase.ReadAccountStateAsync(username)
			?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

		await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

		return account.AccountId;
	}
}
