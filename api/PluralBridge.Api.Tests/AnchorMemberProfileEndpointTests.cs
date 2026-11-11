// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PluralBridge.Api.Account;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Tests.Account;

namespace PluralBridge.Api.Tests;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AnchorMemberProfileEndpointTests
{
	private const string Route = "/api/me/anchor-member-profiles";

	[Fact]
	public async Task UnauthenticatedRequest_Returns401()
	{
		await using var factory = AccountTestHost.CreateFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync(ProfileRoute("system", "Missing Anchor"));

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task UnsupportedObjectType_Returns400()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues(testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			await RegisterAndActivateAsync(client, account);
			await LoginAsync(client, account.Username);

			var response = await client.GetAsync(ProfileRoute("member", "Missing Anchor"));
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

			using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
			Assert.Equal(
				"Anchor object type must be system, group, or place.",
				body.RootElement.GetProperty("error").GetString());
			Assert.False(body.RootElement.GetProperty("canWrite").GetBoolean());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task BlankAnchorName_Returns400()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues(testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			await RegisterAndActivateAsync(client, account);
			await LoginAsync(client, account.Username);

			var response = await client.GetAsync(ProfileRoute("system", " "));
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

			using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
			Assert.Equal(
				"Anchor object type and anchor name are required.",
				body.RootElement.GetProperty("error").GetString());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task AnchorMissingFromAccountMap_Returns404()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues(testId);
		var anchorName = "Missing Anchor " + testId;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			await RegisterAndActivateAsync(client, account);
			await LoginAsync(client, account.Username);

			var response = await client.GetAsync(
				ProfileRoute("place", anchorName) + "&email=other@example.test");
			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

			using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
			Assert.Equal(
				"The requested anchor was not found.",
				body.RootElement.GetProperty("error").GetString());
			Assert.False(body.RootElement.GetProperty("canWrite").GetBoolean());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public void Map_PreservesOrderAndNullableProfileFields()
	{
		var firstId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var secondId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(
				ProfileColumns(),
				[
					[firstId, "Ada", DBNull.Value, "Shore guide", "#336699", 1, "ONLY_YOU", "Only you", true],
					[secondId, "Bea", "they/them", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false]
				])
		]);

		Assert.NotNull(profiles);
		Assert.Equal(
			[
				new AnchorMemberProfile(firstId, "Ada", null, "Shore guide", "#336699", 1, "ONLY_YOU", "Only you", true),
				new AnchorMemberProfile(secondId, "Bea", "they/them", null, null, null, null, null, false)
			],
			profiles);
	}

	[Fact]
	public void Map_RejectsMoreThanOneResultSet()
	{
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(ProfileColumns(), []),
			ResultSet(ProfileColumns(), [])
		]);

		Assert.Null(profiles);
	}

	[Fact]
	public void Map_RejectsMissingProfileColumn()
	{
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["DisplayName", "nvarchar"],
					["Pronouns", "nvarchar"]
				],
				[])
		]);

		Assert.Null(profiles);
	}

	[Fact]
	public void Map_RejectsProfileResultWithoutColor()
	{
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["DisplayName", "nvarchar"],
					["Pronouns", "nvarchar"],
					["Description", "nvarchar"]
				],
				[])
		]);

		Assert.Null(profiles);
	}

	[Fact]
	public void Map_RejectsProfileResultWithoutVisibilityOrFronting()
	{
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["DisplayName", "nvarchar"],
					["Pronouns", "nvarchar"],
					["Description", "nvarchar"],
					["Color", "nvarchar"]
				],
				[])
		]);

		Assert.Null(profiles);
	}

	[Fact]
	public void Map_RejectsNullFrontingFlag()
	{
		var memberId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		var profiles = AnchorMemberProfileReader.Map(
		[
			ResultSet(
				ProfileColumns(),
				[
					[memberId, "Ada", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]
				])
		]);

		Assert.Null(profiles);
	}

	private static string[][] ProfileColumns()
	{
		return
		[
			["MemberId", "uniqueidentifier"],
			["DisplayName", "nvarchar"],
			["Pronouns", "nvarchar"],
			["Description", "nvarchar"],
			["Color", "nvarchar"],
			["VisibilityTypeId", "int"],
			["VisibilityTypeCode", "nvarchar"],
			["VisibilityTypeName", "nvarchar"],
			["IsFronting", "bit"]
		];
	}

	private static string ProfileRoute(string objectType, string anchorName)
	{
		return Route
			+ "?objectType=" + Uri.EscapeDataString(objectType)
			+ "&anchorName=" + Uri.EscapeDataString(anchorName);
	}

	private static AnchorMemberProfileResultSet ResultSet(string[][] columns, object[][] rows)
	{
		return new AnchorMemberProfileResultSet
		{
			Columns = columns.Select(column => new AnchorMemberProfileColumn(column[0], column[1])).ToArray(),
			Rows = rows.Select(static row => (IReadOnlyList<object>)row).ToArray()
		};
	}

	private static RuntimeAccountValues CreateAccountValues(string testId)
	{
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}anchor_profiles_{testId}";

		return new RuntimeAccountValues(
			username,
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}",
			$"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Anchor Profiles");
	}

	private static async Task RegisterAndActivateAsync(HttpClient client, RuntimeAccountValues account)
	{
		var registerResponse = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Register,
			new RegisterAccountRequest(
				account.Username,
				account.Email,
				account.DisplayName,
				AccountTestGlobals.TestAccounts.DefaultPassword));

		Assert.True(
			registerResponse.IsSuccessStatusCode,
			await registerResponse.Content.ReadAsStringAsync());

		var accountState = await AccountTestDatabase.ReadAccountStateAsync(account.Username)
			?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

		await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountState.AccountId);
	}

	private static async Task LoginAsync(HttpClient client, string username)
	{
		var loginResponse = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Login,
			new LoginRequest(username, AccountTestGlobals.TestAccounts.DefaultPassword));

		Assert.True(
			loginResponse.IsSuccessStatusCode,
			await loginResponse.Content.ReadAsStringAsync());
	}

	private sealed record RuntimeAccountValues(
		string Username,
		string Email,
		string DisplayName);
}
