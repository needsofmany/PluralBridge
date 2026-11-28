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

/// <summary>
/// Checks anchor profile HTTP responses, the six-set reader contract, and inherited Owner decisions.
/// </summary>
/// <remarks>
/// The HTTP cases use a temporary account. The mapping and decision cases do not call the database.
/// Example 10 ids are the installed Household group and its sibling systems. Other ids are generic chains.
/// </remarks>
[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AnchorMemberProfileEndpointTests
{
	private const string Route = "/api/me/anchor-member-profiles";

	/// <summary>
	/// An anonymous profile read is rejected before anchor lookup.
	/// </summary>
	/// <returns>The task completes when the response is HTTP 401.</returns>
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

	/// <summary>
	/// A signed-in account cannot request profiles for an object type other than system, group, or place.
	/// </summary>
	/// <returns>The task completes when the response is HTTP 400 and reports that the type must be one of those three.</returns>
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

	/// <summary>
	/// A signed-in account cannot request profiles with a blank anchor name.
	/// </summary>
	/// <returns>The task completes when the response is HTTP 400 and says the type and name are required.</returns>
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

	/// <summary>
	/// A signed-in account receives not found for an anchor name that is not on its map.
	/// </summary>
	/// <returns>The task completes when the response is HTTP 404. A query email does not switch the account.</returns>
	/// <remarks>The extra email query checks that the route uses the session account rather than a caller-supplied address.</remarks>
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

	/// <summary>
	/// The single profile set keeps row order and leaves null pronouns, description, color, and visibility fields null.
	/// </summary>
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

	/// <summary>
	/// Map accepts only the legacy one-set shape. Two profile sets are a contract failure.
	/// </summary>
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

	/// <summary>
	/// A profile set that stops before description, color, visibility, and fronting is rejected.
	/// </summary>
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

	/// <summary>
	/// A profile set that has text columns but no color column is rejected.
	/// </summary>
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

	/// <summary>
	/// A profile set that includes color but omits visibility and fronting is rejected.
	/// </summary>
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

	/// <summary>
	/// A null fronting bit is not treated as false. The row fails the single-set map.
	/// </summary>
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

	/// <summary>
	/// The six sets stay independent: the profile fronting bit, the open-interval bit, null placement modes, and the avatar timestamp are each preserved.
	/// </summary>
	/// <remarks>
	/// Set 1 says this member is not fronting. Set 5 says the open interval exists.
	/// Null placement modes stay null, and the avatar time remains the raw datetime2 value. The URL version is formatted separately.
	/// </remarks>
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

	/// <summary>
	/// An invalid branch with no member rows is a valid empty bundle, not a missing contract.
	/// </summary>
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

	/// <summary>
	/// An empty placement set does not drop the fronting or avatar sets.
	/// </summary>
	/// <remarks>An empty placement set must not be treated as missing fronting or avatar storage.</remarks>
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

	/// <summary>
	/// Three result sets, the older branch procedure shape, are rejected.
	/// </summary>
	/// <remarks>The current API does not accept that shape as a profile list.</remarks>
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

	/// <summary>
	/// Two placement rows for one member are a broken contract rather than a row the reader chooses.
	/// </summary>
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

	/// <summary>
	/// Example 10's Household root grants write authority because the account has an active Owner role there.
	/// </summary>
	/// <remarks>Household is a group, not a system. Inherit on that root still reaches its Owner role.</remarks>
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

	/// <summary>
	/// Main House inherits Household's Owner role through its own parent link.
	/// </summary>
	/// <remarks>Main House is a system placed directly under Household. It does not pass through The Harbor.</remarks>
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

	/// <summary>
	/// The Harbor inherits Household's Owner role as Main House's sibling.
	/// </summary>
	/// <remarks>Its branch is Harbor then Household, not Harbor then Main House.</remarks>
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

	/// <summary>
	/// A generic three-node branch allows the write when only the account child has an Owner role and every node inherits.
	/// </summary>
	/// <remarks>These ids are not Example 10. They only form a place, group, and system chain.</remarks>
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

	/// <summary>
	/// Inheritance reaches the account boundary and still denies the write when no node has an Owner role.
	/// </summary>
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

	/// <summary>
	/// An ancestor whose mode is explicit or null stops the walk before a later Owner role is reached.
	/// </summary>
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

	/// <summary>
	/// Decide returns OwnerRequired when the supplied fact uses explicit mode and HasActiveOwner is false.
	/// </summary>
	/// <remarks>No permission-code value is passed to Decide. This test does not verify database translation from a permission code to a role.</remarks>
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

	/// <summary>
	/// Decide rejects a broken link even when the supplied Owner flags are true, denies a supplied branch that has no Owner, and the parser rejects a branch whose first node is not the selected anchor.
	/// </summary>
	/// <remarks>The facts are supplied directly. This test does not create or query a separate off-branch database grant.</remarks>
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

	/// <summary>
	/// Builds one branch fact for a decision test.
	/// </summary>
	/// <param name="objectType">system, group, or place.</param>
	/// <param name="objectId">Anchor id for the fact.</param>
	/// <param name="permissionMode">inherit, explicit, or null for Not set.</param>
	/// <param name="owner">True when the fact has an active Owner role.</param>
	/// <param name="linkedToNext">True when the fact proves its link to the next node.</param>
	/// <param name="accountChild">True when the fact is a direct child of the account.</param>
	/// <returns>The fact passed to Decide.</returns>
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

	/// <summary>
	/// Builds the six result sets in procedure order.
	/// </summary>
	/// <param name="profiles">Rows for the nine-column profile set.</param>
	/// <param name="branchValid">The single BranchValid bit.</param>
	/// <param name="anchors">Branch anchor rows. An empty array is a valid empty set.</param>
	/// <param name="placements">Placement rows. An empty array does not remove the later sets.</param>
	/// <param name="fronting">Fronting rows.</param>
	/// <param name="avatars">Avatar timestamp rows.</param>
	/// <returns>Profiles, BranchValid, anchors, placements, fronting, and avatars.</returns>
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

	/// <summary>
	/// Returns the branch-anchor column contract in procedure order.
	/// </summary>
	/// <returns>Ordinal, object type and id, and the visibility and permission columns.</returns>
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

	/// <summary>
	/// Returns the nine profile columns in procedure order.
	/// </summary>
	/// <returns>Member identity, text, color, visibility, and the required fronting bit.</returns>
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

	/// <summary>
	/// Builds the profile GET route for one anchor type and name.
	/// </summary>
	/// <param name="objectType">Query object type. The tests also pass unsupported values.</param>
	/// <param name="anchorName">Query anchor name, including blank names.</param>
	/// <returns>The escaped profile route without an account email.</returns>
	private static string ProfileRoute(string objectType, string anchorName)
	{
		return Route
			+ "?objectType=" + Uri.EscapeDataString(objectType)
			+ "&anchorName=" + Uri.EscapeDataString(anchorName);
	}

	/// <summary>
	/// Wraps column names and cell rows as one raw procedure result set.
	/// </summary>
	/// <param name="columns">Name and SQL type pairs, in ordinal order.</param>
	/// <param name="rows">Cell rows. An empty array is an empty set, not a missing set.</param>
	/// <returns>The result set passed to the reader.</returns>
	private static AnchorMemberProfileResultSet ResultSet(string[][] columns, object[][] rows)
	{
		return new AnchorMemberProfileResultSet
		{
			Columns = columns.Select(column => new AnchorMemberProfileColumn(column[0], column[1])).ToArray(),
			Rows = rows.Select(static row => (IReadOnlyList<object>)row).ToArray()
		};
	}

	/// <summary>
	/// Builds a unique runtime account so the HTTP cases do not share a username.
	/// </summary>
	/// <param name="testId">Identifier included in the username and email.</param>
	/// <returns>The username, email, and display name used for registration.</returns>
	private static RuntimeAccountValues CreateAccountValues(string testId)
	{
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}anchor_profiles_{testId}";

		return new RuntimeAccountValues(
			username,
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}",
			$"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Anchor Profiles");
	}

	/// <summary>
	/// Registers the runtime account and marks it active so the following login can establish a session.
	/// </summary>
	/// <param name="client">Test host client.</param>
	/// <param name="account">Account values created for this test.</param>
	/// <returns>The task completes when registration succeeded and the account is active.</returns>
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

	/// <summary>
	/// Signs the test client in as the runtime account.
	/// </summary>
	/// <param name="client">Test host client that stores the session cookie.</param>
	/// <param name="username">Username created for this test.</param>
	/// <returns>The task completes when login succeeded.</returns>
	private static async Task LoginAsync(HttpClient client, string username)
	{
		var loginResponse = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.Login,
			new LoginRequest(username, AccountTestGlobals.TestAccounts.DefaultPassword));

		Assert.True(
			loginResponse.IsSuccessStatusCode,
			await loginResponse.Content.ReadAsStringAsync());
	}

	/// <summary>
	/// Username, email, and display name for one temporary HTTP test account.
	/// </summary>
	/// <param name="Username">Unique runtime username.</param>
	/// <param name="Email">Email derived from that username.</param>
	/// <param name="DisplayName">Display name sent at registration.</param>
	private sealed record RuntimeAccountValues(
		string Username,
		string Email,
		string DisplayName);
}
