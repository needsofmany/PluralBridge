// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Account;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Tests.Account;

namespace PluralBridge.Api.Tests;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class PluralMapEndpointTests
{
	private const string PluralMapRoute = "/api/me/plural-map";

	[Fact]
	public async Task UnauthenticatedRequest_Returns401()
	{
		await using var factory = AccountTestHost.CreateFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync(PluralMapRoute);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task SeparateRuntimeAccounts_ReceiveTheirOwnContext()
	{
		var testId = Guid.NewGuid().ToString("N");
		var accountA = CreateAccountValues("a", testId);
		var accountB = CreateAccountValues("b", testId);

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
			await LoginAsync(clientA, accountA.Username);
			await LoginAsync(clientB, accountB.Username);

			using var mapA = await ReadMapAsync(clientA);
			using var mapB = await ReadMapAsync(clientB);

			Assert.Equal(accountAId, ReadOnlyContextAccountId(mapA.RootElement));
			Assert.Equal(accountBId, ReadOnlyContextAccountId(mapB.RootElement));
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task AccountWithoutSystemMembership_Succeeds()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues("none", testId);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

			var accountId = await RegisterAndActivateAsync(client, account);
			await LoginAsync(client, account.Username);

			var response = await client.GetAsync(PluralMapRoute);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			using var map = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
			Assert.Equal(accountId, ReadOnlyContextAccountId(map.RootElement));
			Assert.Equal(JsonValueKind.Array, map.RootElement.GetProperty("graphNodeCounts").ValueKind);
			Assert.Equal(JsonValueKind.Array, map.RootElement.GetProperty("graphEdges").ValueKind);
			Assert.Equal(JsonValueKind.Array, map.RootElement.GetProperty("hierarchyPreview").ValueKind);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task HttpOutput_MatchesDirectProcedureOutput()
	{
		var testId = Guid.NewGuid().ToString("N");
		var account = CreateAccountValues("direct", testId);

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

			using var map = await ReadMapAsync(client);
			var procedure = await ReadProcedureAsync(account.Email);

			Assert.Equal(4, procedure.Count);
			AssertRowsMatch(map.RootElement.GetProperty("context"), procedure[0]);
			AssertRowsMatch(map.RootElement.GetProperty("graphNodeCounts"), procedure[1]);
			AssertRowsMatch(map.RootElement.GetProperty("graphEdges"), procedure[2]);
			AssertRowsMatch(map.RootElement.GetProperty("hierarchyPreview"), procedure[3]);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public void Map_PreservesReturnedValuesAndOrder()
	{
		var accountId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
		var parentId = Guid.Parse("11111111-2222-3333-4444-555555555555");
		var childId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
		var sets = CreateResultSets(
			accountId,
			[
				["GraphNodeCounts", "Group", 2],
				["GraphNodeCounts", "Place", 1]
			],
			[
				["GraphEdges", "System", parentId, "Group", childId, 4, "group-membership"]
			],
			[
				["HierarchyPreview", "first-line", "groups|00000000-0000-0000-0000-000000000000"],
				["HierarchyPreview", "second-line", "tt|000001|Place|" + childId.ToString("D")]
			]);

		var map = PluralMapReader.Map(sets, accountId);

		Assert.NotNull(map);
		Assert.Equal("Context", map.Context[0].Section);
		Assert.Equal("person@example.test", map.Context[0].Email);
		Assert.Equal(["Group", "Place"], map.GraphNodeCounts.Select(row => row.ObjectType));
		Assert.Equal([2, 1], map.GraphNodeCounts.Select(row => row.NodeCount));
		Assert.Equal("group-membership", map.GraphEdges[0].EdgeSource);
		Assert.Equal(parentId, map.GraphEdges[0].ParentObjectId);
		Assert.Equal(childId, map.GraphEdges[0].ChildObjectId);
		Assert.Equal(
			["first-line", "second-line"],
			map.HierarchyPreview.Select(row => row.TreeLine));
		Assert.Equal(
			[
				"groups|00000000-0000-0000-0000-000000000000",
				"tt|000001|Place|" + childId.ToString("D")
			],
			map.HierarchyPreview.Select(row => row.PathKey));
	}

	[Fact]
	public void DerivePreview_ReadsLabelsAndAttachesDirectSystemGroupToItsHeader()
	{
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var groupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		var systemPath = "30|000001|system|" + systemId.ToString("D").ToUpperInvariant();
		var headerPath = systemPath + ".15|000000|groups|00000000-0000-0000-0000-000000000000";
		var groupPath = systemPath + ".20|002001|group|" + groupId.ToString("D").ToUpperInvariant();
		var memberPath = systemPath + ".10|000001|member|" + memberId.ToString("D").ToUpperInvariant();

		var derived = PluralMapReader.DerivePreview(
		[
			Preview("\u251c\u2500\u2500 System: Harbor", systemPath),
			Preview("    \u251c\u2500\u2500 Member: Alex", memberPath),
			Preview("    \u2514\u2500\u2500 Groups", headerPath),
			Preview("        \u251c\u2500\u2500 Group: Dancers", groupPath)
		]);

		Assert.Equal(
			[systemPath, memberPath, headerPath, groupPath],
			derived.Select(row => row.PathKey));
		Assert.Equal("System: Harbor", derived[0].NodeLabel);
		Assert.Equal("system", derived[0].ObjectType);
		Assert.Equal(systemId, derived[0].ObjectId);
		Assert.Null(derived[0].ParentPathKey);
		Assert.False(derived[0].Header);

		Assert.Equal("Member: Alex", derived[1].NodeLabel);
		Assert.Equal("member", derived[1].ObjectType);
		Assert.Equal(memberId, derived[1].ObjectId);
		Assert.Equal(systemPath, derived[1].ParentPathKey);

		Assert.Equal("Groups", derived[2].NodeLabel);
		Assert.Equal("groups", derived[2].ObjectType);
		Assert.Equal(Guid.Empty, derived[2].ObjectId);
		Assert.Equal(systemPath, derived[2].ParentPathKey);
		Assert.True(derived[2].Header);

		Assert.Equal("Group: Dancers", derived[3].NodeLabel);
		Assert.Equal("group", derived[3].ObjectType);
		Assert.Equal(groupId, derived[3].ObjectId);
		Assert.Equal(headerPath, derived[3].ParentPathKey);
		Assert.False(derived[3].Header);
	}

	[Fact]
	public void DerivePreview_KeepsRepeatedAppearancesOnTheirOwnParentOccurrence()
	{
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var groupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var firstSystem = "30|000001|system|" + systemId.ToString("D").ToUpperInvariant();
		var secondSystem = "30|000002|system|" + systemId.ToString("D").ToUpperInvariant();
		var firstHeader = firstSystem + ".15|000000|groups|00000000-0000-0000-0000-000000000000";
		var secondHeader = secondSystem + ".15|000000|groups|00000000-0000-0000-0000-000000000000";
		var firstGroup = firstSystem + ".20|002001|group|" + groupId.ToString("D").ToUpperInvariant();
		var secondGroup = secondSystem + ".20|002001|group|" + groupId.ToString("D").ToUpperInvariant();
		var nestedMember = firstGroup + ".10|000001|member|" + memberId.ToString("D").ToUpperInvariant();

		var derived = PluralMapReader.DerivePreview(
		[
			Preview("\u251c\u2500\u2500 System: Harbor", firstSystem),
			Preview("    \u2514\u2500\u2500 Groups", firstHeader),
			Preview("        \u251c\u2500\u2500 Group: Dancers", firstGroup),
			Preview("            \u2514\u2500\u2500 Member: Alex", nestedMember),
			Preview("\u2514\u2500\u2500 System: Harbor", secondSystem),
			Preview("    \u2514\u2500\u2500 Groups", secondHeader),
			Preview("        \u2514\u2500\u2500 Group: Dancers", secondGroup)
		]);

		var first = derived.Single(row => row.PathKey == firstGroup);
		var second = derived.Single(row => row.PathKey == secondGroup);
		var member = derived.Single(row => row.PathKey == nestedMember);

		Assert.NotEqual(first.PathKey, second.PathKey);
		Assert.Equal(groupId, first.ObjectId);
		Assert.Equal(groupId, second.ObjectId);
		Assert.Equal(firstHeader, first.ParentPathKey);
		Assert.Equal(secondHeader, second.ParentPathKey);
		Assert.Equal(firstGroup, member.ParentPathKey);
	}

	[Fact]
	public void SelectFirstAnchor_ChoosesTheFirstTopLevelAnchorInReturnedOrder()
	{
		var placeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var placePath = "40|000001|place|" + placeId.ToString("D").ToUpperInvariant();
		var systemPath = "30|000001|system|" + systemId.ToString("D").ToUpperInvariant();
		var memberPath = "10|000001|member|" + memberId.ToString("D").ToUpperInvariant();

		var derived = PluralMapReader.DerivePreview(
		[
			Preview("\u251c\u2500\u2500 Member: Alex", memberPath),
			Preview("\u251c\u2500\u2500 Place: Shore", placePath),
			Preview("\u2514\u2500\u2500 System: Harbor", systemPath)
		]);

		var selected = PluralMapReader.SelectFirstAnchor(derived);

		Assert.NotNull(selected);
		Assert.Equal(placePath, selected.PathKey);
		Assert.Equal("place", selected.ObjectType);
		Assert.Equal("Place: Shore", selected.NodeLabel);
	}

	[Fact]
	public void ImmediateChildren_MatchTheSelectedAnchorInReturnedOrder()
	{
		var placeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var groupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		var placePath = "40|000001|place|" + placeId.ToString("D").ToUpperInvariant();
		var memberPath = placePath + ".10|000001|member|" + memberId.ToString("D").ToUpperInvariant();
		var groupPath = placePath + ".20|002001|group|" + groupId.ToString("D").ToUpperInvariant();

		var derived = PluralMapReader.DerivePreview(
		[
			Preview("\u2514\u2500\u2500 Place: Shore", placePath),
			Preview("    \u251c\u2500\u2500 Member: Ada", memberPath),
			Preview("    \u2514\u2500\u2500 Group: House", groupPath)
		]);
		var selected = PluralMapReader.SelectFirstAnchor(derived);
		Assert.NotNull(selected);

		var children = PluralMapReader.ImmediateChildren(derived, selected.PathKey);

		Assert.Equal("place", selected.ObjectType);
		Assert.Equal("Place: Shore", selected.NodeLabel);
		Assert.Equal(["Member: Ada", "Group: House"], children.Select(row => row.NodeLabel));
		Assert.Equal(["member", "group"], children.Select(row => row.ObjectType));
		Assert.Equal([memberPath, groupPath], children.Select(row => row.PathKey));
	}

	[Fact]
	public void DerivePreview_LeavesUnrecognizedPathsWithoutIdentity()
	{
		var derived = PluralMapReader.DerivePreview(
		[
			Preview("first-line", "groups|00000000-0000-0000-0000-000000000000"),
			Preview("second-line", "tt|000001|Place|66666666-7777-8888-9999-aaaaaaaaaaaa")
		]);

		Assert.All(derived, row =>
		{
			Assert.Null(row.NodeLabel);
			Assert.Null(row.ObjectType);
			Assert.Null(row.ObjectId);
			Assert.Null(row.ParentPathKey);
			Assert.False(row.Header);
		});
		Assert.Null(PluralMapReader.SelectFirstAnchor(derived));
	}

	[Fact]
	public void ApplyAnchorNames_UsesStoredNamesAndKeepsPreviewOrder()
	{
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var repeatedSystemId = systemId;
		var groupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		var placeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		var memberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var unnamedSystemId = Guid.Parse("55555555-5555-5555-5555-555555555555");
		var rows = new[]
		{
			NamedPreview("system", systemId, "30|000001|system|" + systemId.ToString("D"), "System: Harbor (partner)", null, false),
			NamedPreview("member", memberId, "10|000001|member|" + memberId.ToString("D"), "Member: Ada", null, false),
			NamedPreview("groups", Guid.Empty, "15|000000|groups|00000000-0000-0000-0000-000000000000", "Groups", "parent", true),
			NamedPreview("group", groupId, "20|002001|group|" + groupId.ToString("D"), "Group: Dancers (east)", "parent", false),
			NamedPreview("place", placeId, "40|000001|place|" + placeId.ToString("D"), "Place: Shore (north)", null, false),
			NamedPreview("system", repeatedSystemId, "30|000002|system|" + repeatedSystemId.ToString("D"), "Child System: Harbor (partner)", null, false),
			NamedPreview("system", unnamedSystemId, "30|000003|system|" + unnamedSystemId.ToString("D"), "System: (missing system)", null, false)
		};

		var names = new Dictionary<PluralMapReader.AnchorNameKey, string?>
		{
			[new PluralMapReader.AnchorNameKey("system", systemId)] = "Harbor (west)",
			[new PluralMapReader.AnchorNameKey("group", groupId)] = "Dancers (east)",
			[new PluralMapReader.AnchorNameKey("place", placeId)] = "Shore (north)",
			[new PluralMapReader.AnchorNameKey("system", unnamedSystemId)] = null,
			[new PluralMapReader.AnchorNameKey("group", systemId)] = "Not the system name"
		};

		var named = PluralMapReader.ApplyAnchorNames(rows, names);

		Assert.Equal(rows.Select(row => row.PathKey), named.Select(row => row.PathKey));
		Assert.Equal(rows.Select(row => row.NodeLabel), named.Select(row => row.NodeLabel));
		Assert.Equal(rows.Select(row => row.TreeLine), named.Select(row => row.TreeLine));
		Assert.Equal(rows.Select(row => row.Section), named.Select(row => row.Section));
		Assert.Equal(rows.Select(row => row.ObjectType), named.Select(row => row.ObjectType));
		Assert.Equal(rows.Select(row => row.ObjectId), named.Select(row => row.ObjectId));
		Assert.Equal(rows.Select(row => row.ParentPathKey), named.Select(row => row.ParentPathKey));
		Assert.Equal(rows.Select(row => row.Header), named.Select(row => row.Header));
		Assert.Equal(
			["Harbor (west)", null, null, "Dancers (east)", "Shore (north)", "Harbor (west)", null],
			named.Select(row => row.AnchorName));
	}

	[Fact]
	public void Map_RejectsMismatchedAccountId()
	{
		var sets = CreateResultSets(Guid.NewGuid(), [], [], []);

		var map = PluralMapReader.Map(sets, Guid.NewGuid());

		Assert.Null(map);
	}

	[Fact]
	public void Map_RejectsMissingContextRow()
	{
		var accountId = Guid.NewGuid();
		var sets = CreateResultSets(accountId, [], [], []);
		sets[0] = new PluralMapRawResultSet
		{
			Columns = sets[0].Columns,
			Rows = []
		};

		var map = PluralMapReader.Map(sets, accountId);

		Assert.Null(map);
	}

	[Fact]
	public void Map_RejectsDuplicateContextRows()
	{
		var accountId = Guid.NewGuid();
		var sets = CreateResultSets(accountId, [], [], []);
		sets[0] = new PluralMapRawResultSet
		{
			Columns = sets[0].Columns,
			Rows = [sets[0].Rows[0], sets[0].Rows[0]]
		};

		var map = PluralMapReader.Map(sets, accountId);

		Assert.Null(map);
	}

	[Fact]
	public void Map_RejectsMissingResultSet()
	{
		var accountId = Guid.NewGuid();
		var sets = CreateResultSets(accountId, [], [], []);

		var map = PluralMapReader.Map(sets.Take(3).ToArray(), accountId);

		Assert.Null(map);
	}

	[Fact]
	public void Map_RejectsExtraResultSet()
	{
		var accountId = Guid.NewGuid();
		var sets = CreateResultSets(accountId, [], [], []);
		sets.Add(sets[3]);

		var map = PluralMapReader.Map(sets, accountId);

		Assert.Null(map);
	}

	[Fact]
	public void Map_RejectsMalformedResultSet()
	{
		var accountId = Guid.NewGuid();
		var sets = CreateResultSets(accountId, [], [], []);
		var columns = sets[0].Columns.ToArray();
		columns[2] = new PluralMapRawColumn("AccountId", "int");
		sets[0] = new PluralMapRawResultSet
		{
			Columns = columns,
			Rows = sets[0].Rows
		};

		var map = PluralMapReader.Map(sets, accountId);

		Assert.Null(map);
	}

	private static async Task<JsonDocument> ReadMapAsync(HttpClient client)
	{
		var response = await client.GetAsync(PluralMapRoute);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
	}

	private static Guid ReadOnlyContextAccountId(JsonElement map)
	{
		var context = map.GetProperty("context");
		Assert.Equal(1, context.GetArrayLength());
		return context[0].GetProperty("accountId").GetGuid();
	}

	private static async Task<IReadOnlyList<IReadOnlyList<IReadOnlyDictionary<string, string>>>> ReadProcedureAsync(
		string email)
	{
		await using var connection = new SqlConnection(AccountTestDatabase.GetConnectionString());
		await connection.OpenAsync();
		await using var command = new SqlCommand("dbo.pb_show_tree_step4_members_by_email", connection)
		{
			CommandType = CommandType.StoredProcedure
		};
		command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;

		var resultSets = new List<IReadOnlyList<IReadOnlyDictionary<string, string>>>();
		await using var reader = await command.ExecuteReaderAsync();
		do
		{
			var rows = new List<IReadOnlyDictionary<string, string>>();
			while (await reader.ReadAsync())
			{
				var row = new Dictionary<string, string>(StringComparer.Ordinal);
				for (var index = 0; index < reader.FieldCount; index++)
				{
					row.Add(JsonName(reader.GetName(index)), SqlValue(reader, index));
				}

				rows.Add(row);
			}

			resultSets.Add(rows);
		}
		while (await reader.NextResultAsync());

		return resultSets;
	}

	private static void AssertRowsMatch(
		JsonElement httpRows,
		IReadOnlyList<IReadOnlyDictionary<string, string>> procedureRows)
	{
		Assert.Equal(procedureRows.Count, httpRows.GetArrayLength());

		for (var rowIndex = 0; rowIndex < procedureRows.Count; rowIndex++)
		{
			var httpRow = httpRows[rowIndex];
			foreach (var (name, procedureValue) in procedureRows[rowIndex])
			{
				var httpValue = httpRow.GetProperty(name);
				var rendered = httpValue.ValueKind == JsonValueKind.Number
					? httpValue.GetInt32().ToString(CultureInfo.InvariantCulture)
					: httpValue.GetString();

				Assert.Equal(procedureValue, rendered);
			}
		}
	}

	private static string JsonName(string columnName)
	{
		return columnName switch
		{
			"Section" => "section",
			"Email" => "email",
			"AccountId" => "accountId",
			"ObjectType" => "objectType",
			"NodeCount" => "nodeCount",
			"ParentObjectType" => "parentObjectType",
			"ParentObjectId" => "parentObjectId",
			"ChildObjectType" => "childObjectType",
			"ChildObjectId" => "childObjectId",
			"EdgeRank" => "edgeRank",
			"EdgeSource" => "edgeSource",
			"TreeLine" => "treeLine",
			"PathKey" => "pathKey",
			_ => columnName
		};
	}

	private static string SqlValue(SqlDataReader reader, int index)
	{
		var typeName = reader.GetDataTypeName(index);
		if (typeName.Equals("int", StringComparison.OrdinalIgnoreCase))
		{
			return reader.GetInt32(index).ToString(CultureInfo.InvariantCulture);
		}

		if (typeName.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
		{
			return reader.GetGuid(index).ToString("D");
		}

		return reader.GetString(index);
	}

	private static List<PluralMapRawResultSet> CreateResultSets(
		Guid accountId,
		object[][] nodeCountRows,
		object[][] edgeRows,
		object[][] previewRows)
	{
		return
		[
			ResultSet(
				[Column("Section"), Column("Email"), Column("AccountId", "uniqueidentifier")],
				[["Context", "person@example.test", accountId]]),
			ResultSet(
				[Column("Section"), Column("ObjectType"), Column("NodeCount", "int")],
				nodeCountRows),
			ResultSet(
				[
					Column("Section"),
					Column("ParentObjectType"),
					Column("ParentObjectId", "uniqueidentifier"),
					Column("ChildObjectType"),
					Column("ChildObjectId", "uniqueidentifier"),
					Column("EdgeRank", "int"),
					Column("EdgeSource")
				],
				edgeRows),
			ResultSet(
				[Column("Section"), Column("TreeLine"), Column("PathKey")],
				previewRows)
		];
	}

	private static PluralMapRawResultSet ResultSet(
		PluralMapRawColumn[] columns,
		object[][] rows)
	{
		return new PluralMapRawResultSet
		{
			Columns = columns,
			Rows = rows.Select(static row => (IReadOnlyList<object>)row).ToArray()
		};
	}

	private static PluralMapPreviewRow NamedPreview(
		string objectType,
		Guid objectId,
		string pathKey,
		string nodeLabel,
		string? parentPathKey,
		bool header)
	{
		return new PluralMapPreviewRow(
			"HierarchyPreview",
			nodeLabel,
			pathKey,
			nodeLabel,
			objectType,
			objectId,
			parentPathKey,
			header,
			null);
	}

	private static PluralMapPreviewRow Preview(string treeLine, string pathKey)
	{
		return new PluralMapPreviewRow(
			"HierarchyPreview",
			treeLine,
			pathKey,
			null,
			null,
			null,
			null,
			false,
			null);
	}

	private static PluralMapRawColumn Column(string name, string sqlTypeName = "nvarchar")
	{
		return new PluralMapRawColumn(name, sqlTypeName);
	}

	private static RuntimeAccountValues CreateAccountValues(string label, string testId)
	{
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}plural_map_{label}_{testId}";

		return new RuntimeAccountValues(
			username,
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}",
			$"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Plural Map {label}");
	}

	private static async Task<Guid> RegisterAndActivateAsync(
		HttpClient client,
		RuntimeAccountValues account)
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
		return accountState.AccountId;
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
