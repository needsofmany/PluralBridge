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

	[Fact]
	public void MapBundle_ReadsSixSetsIndependently()
	{
		var memberId = Guid.Parse("55555555-5555-5555-5555-555555555555");
		var anchorId = Guid.Parse("66666666-6666-6666-6666-666666666666");
		var updatedAt = new DateTime(2026, 11, 13, 7, 1, 2, 100);
		var bundle = AnchorMemberProfileReader.MapBundle(SixSets(
			[
				[memberId, "Ada", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false]
			],
			true,
			[
				[0, "group", anchorId, "inherit", DBNull.Value, "explicit", "viewer"]
			],
			[
				[memberId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]
			],
			[
				[memberId, true]
			],
			[
				[memberId, updatedAt]
			]));

		Assert.True(bundle.BranchValid);
		Assert.Equal("Ada", bundle.Profiles[0].DisplayName);
		Assert.False(bundle.Profiles[0].IsFronting);
		Assert.Null(bundle.Placements[0].VisibilityMode);
		Assert.Null(bundle.Placements[0].PermissionMode);
		Assert.True(bundle.Fronting[0].IsFronting);
		Assert.Equal(updatedAt, bundle.Avatars[0].UpdatedAtUtc);
		Assert.Equal(
			"/api/me/members/55555555-5555-5555-5555-555555555555/avatar?v=2026-11-13T07%3A01%3A02.100",
			MeController.AvatarReferenceUrl(memberId, updatedAt));
	}

	[Fact]
	public void MapBundle_AllowsEmptyMemberLists()
	{
		var bundle = AnchorMemberProfileReader.MapBundle(SixSets([], false, [], [], [], []));

		Assert.Empty(bundle.Profiles);
		Assert.False(bundle.BranchValid);
		Assert.Empty(bundle.BranchAnchors);
		Assert.Empty(bundle.Placements);
		Assert.Empty(bundle.Fronting);
		Assert.Empty(bundle.Avatars);
	}

	[Fact]
	public void MapBundle_KeepsFrontingAndAvatarWhenPlacementRowsAreAbsent()
	{
		var memberId = Guid.Parse("77777777-7777-7777-7777-777777777777");
		var updatedAt = new DateTime(2026, 11, 13, 8, 0, 0, 0);
		var bundle = AnchorMemberProfileReader.MapBundle(SixSets(
			[
				[memberId, "Ada", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false]
			],
			false,
			[],
			[],
			[
				[memberId, true]
			],
			[
				[memberId, updatedAt]
			]));

		Assert.Empty(bundle.Placements);
		Assert.True(bundle.Fronting[0].IsFronting);
		Assert.Equal(updatedAt, bundle.Avatars[0].UpdatedAtUtc);
	}

	[Fact]
	public void MapBundle_RejectsIncompleteContract()
	{
		var exception = Assert.Throws<AnchorProfileContractException>(() =>
			AnchorMemberProfileReader.MapBundle(
			[
				ResultSet(ProfileColumns(), []),
				ResultSet([["BranchValid", "bit"]], [[false]]),
				ResultSet(BranchColumns(), [])
			]));

		Assert.Contains("3 result sets", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void MapBundle_RejectsDuplicatePlacementRows()
	{
		var memberId = Guid.Parse("88888888-8888-8888-8888-888888888888");
		var exception = Assert.Throws<AnchorProfileContractException>(() =>
			AnchorMemberProfileReader.MapBundle(SixSets(
				[
					[memberId, "Ada", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, false]
				],
				true,
				[],
				[
					[memberId, "unset", DBNull.Value, "unset", DBNull.Value],
					[memberId, "explicit", 1, "unset", DBNull.Value]
				],
				[
					[memberId, false]
				],
				[])));

		Assert.Contains("duplicated", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Decide_AllowsDirectOwnerOnTheExample10HouseholdRoot()
	{
		var household = Guid.Parse("EDB5FBA0-8773-46C6-B8A2-3BCCF923EFB4");
		var decision = AnchorWriteAuthorization.Decide(
			"group",
			household,
			[
				Fact("group", household, "inherit", owner: true, linkedToNext: true, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.Allowed, decision);
	}

	[Fact]
	public void Decide_AllowsMainHouseToInheritHouseholdOwner()
	{
		var household = Guid.Parse("EDB5FBA0-8773-46C6-B8A2-3BCCF923EFB4");
		var mainHouse = Guid.Parse("449F4A5D-8F31-4710-AD7A-776A8ACB0325");
		var decision = AnchorWriteAuthorization.Decide(
			"system",
			mainHouse,
			[
				Fact("system", mainHouse, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("group", household, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.Allowed, decision);
	}

	[Fact]
	public void Decide_AllowsTheHarborToInheritHouseholdOwner()
	{
		var household = Guid.Parse("EDB5FBA0-8773-46C6-B8A2-3BCCF923EFB4");
		var harbor = Guid.Parse("D83AADAF-B096-4AC2-AA23-88561BA8F5F3");
		var decision = AnchorWriteAuthorization.Decide(
			"system",
			harbor,
			[
				Fact("system", harbor, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("group", household, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.Allowed, decision);
	}

	[Fact]
	public void Decide_AllowsInheritedOwnerThroughAGenericMultiLevelBranch()
	{
		var root = Guid.Parse("10000000-0000-0000-0000-000000000001");
		var middle = Guid.Parse("10000000-0000-0000-0000-000000000002");
		var selected = Guid.Parse("10000000-0000-0000-0000-000000000003");
		var decision = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("group", middle, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("system", root, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.Allowed, decision);
	}

	[Fact]
	public void Decide_DeniesAGenericBranchWithNoOwnerGrant()
	{
		var root = Guid.Parse("10000000-0000-0000-0000-000000000001");
		var selected = Guid.Parse("10000000-0000-0000-0000-000000000003");
		var decision = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("system", root, "inherit", owner: false, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.OwnerRequired, decision);
	}

	[Fact]
	public void Decide_StopsAGenericBranchWhenAnAncestorDoesNotInherit()
	{
		var root = Guid.Parse("10000000-0000-0000-0000-000000000001");
		var middle = Guid.Parse("10000000-0000-0000-0000-000000000002");
		var selected = Guid.Parse("10000000-0000-0000-0000-000000000003");
		var explicitStop = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("group", middle, "explicit", owner: false, linkedToNext: true, accountChild: false),
				Fact("system", root, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);
		var notSetStop = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("group", middle, null, owner: false, linkedToNext: true, accountChild: false),
				Fact("system", root, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.OwnerRequired, explicitStop);
		Assert.Equal(AnchorWriteDecision.OwnerRequired, notSetStop);
	}

	[Fact]
	public void Decide_DoesNotTreatAnOwnerPermissionCodeAsARoleGrant()
	{
		var household = Guid.Parse("EDB5FBA0-8773-46C6-B8A2-3BCCF923EFB4");
		var decision = AnchorWriteAuthorization.Decide(
			"group",
			household,
			[
				Fact("group", household, "explicit", owner: false, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.OwnerRequired, decision);
	}

	[Fact]
	public void Decide_RejectsAGenericInvalidBranchAndAnUnrelatedParentGrant()
	{
		var root = Guid.Parse("10000000-0000-0000-0000-000000000001");
		var otherParent = Guid.Parse("10000000-0000-0000-0000-000000000009");
		var selected = Guid.Parse("10000000-0000-0000-0000-000000000003");
		var brokenLink = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: true, linkedToNext: false, accountChild: false),
				Fact("system", otherParent, "inherit", owner: true, linkedToNext: false, accountChild: true)
			]);
		var unrelatedGrant = AnchorWriteAuthorization.Decide(
			"place",
			selected,
			[
				Fact("place", selected, "inherit", owner: false, linkedToNext: true, accountChild: false),
				Fact("system", root, "inherit", owner: false, linkedToNext: false, accountChild: true)
			]);

		Assert.Equal(AnchorWriteDecision.InvalidBranch, brokenLink);
		Assert.Equal(AnchorWriteDecision.OwnerRequired, unrelatedGrant);
		Assert.False(AnchorWriteAuthorization.TryReadBranch(
			"place:" + otherParent.ToString("D") + ",system:" + root.ToString("D"),
			"place",
			selected,
			out _));
	}

	private static AnchorWriteFact Fact(
		string objectType,
		Guid objectId,
		string? permissionMode,
		bool owner,
		bool linkedToNext,
		bool accountChild)
	{
		return new AnchorWriteFact(objectType, objectId, permissionMode, owner, linkedToNext, accountChild);
	}

	private static AnchorMemberProfileResultSet[] SixSets(
		object[][] profiles,
		bool branchValid,
		object[][] anchors,
		object[][] placements,
		object[][] fronting,
		object[][] avatars)
	{
		return
		[
			ResultSet(ProfileColumns(), profiles),
			ResultSet([["BranchValid", "bit"]], [[branchValid]]),
			ResultSet(BranchColumns(), anchors),
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["VisibilityMode", "nvarchar"],
					["VisibilityTypeId", "int"],
					["PermissionMode", "nvarchar"],
					["PermissionCode", "nvarchar"]
				],
				placements),
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["IsFronting", "bit"]
				],
				fronting),
			ResultSet(
				[
					["MemberId", "uniqueidentifier"],
					["UpdatedAtUtc", "datetime2"]
				],
				avatars)
		];
	}

	private static string[][] BranchColumns()
	{
		return
		[
			["Ordinal", "int"],
			["ObjectType", "nvarchar"],
			["ObjectId", "uniqueidentifier"],
			["VisibilityMode", "nvarchar"],
			["VisibilityTypeId", "int"],
			["PermissionMode", "nvarchar"],
			["PermissionCode", "nvarchar"]
		];
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
