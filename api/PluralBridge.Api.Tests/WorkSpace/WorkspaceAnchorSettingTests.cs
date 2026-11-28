// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Claims;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Retained visibility and permission bytes for anchors loaded with the workspace tree.
/// These tests do not open a database connection.
/// </summary>
public sealed class WorkspaceAnchorSettingTests
{
	private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid Harbor = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid Inner = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid Shore = Guid.Parse("44444444-4444-4444-4444-444444444444");
	private static readonly Guid MemberId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

	/// <summary>
	/// The factory path loads three nested systems through production construction.
	/// Stored inherit becomes byte 2 on each side. The test does not encode those bytes itself.
	/// </summary>
	[Fact]
	public async Task FactoryLoad_RetainsInheritForThreeNestedSystems()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(useDatabase: false, new ClaimsPrincipal(), CancellationToken.None);

		Assert.NotNull(context);
		Assert.Equal(
			[
				WorkspaceTreeTestDataFactory.OuterSystemId,
				WorkspaceTreeTestDataFactory.MiddleSystemId,
				WorkspaceTreeTestDataFactory.InnerSystemId
			],
			context.AnchorSettings.Select(setting => setting.ObjectId).ToArray());
		Assert.All(context.AnchorSettings, setting =>
		{
			Assert.Equal("system", setting.ObjectType);
			Assert.Equal((byte)2, setting.Visibility);
			Assert.Equal((byte)2, setting.Permission);
		});
		Assert.Equal(
			[
				new WorkspaceOccurrence("account", WorkspaceTreeTestDataFactory.AccountId, "system", WorkspaceTreeTestDataFactory.OuterSystemId, 0, "outer"),
				new WorkspaceOccurrence("system", WorkspaceTreeTestDataFactory.OuterSystemId, "system", WorkspaceTreeTestDataFactory.MiddleSystemId, 1, "outer/middle"),
				new WorkspaceOccurrence("system", WorkspaceTreeTestDataFactory.MiddleSystemId, "system", WorkspaceTreeTestDataFactory.InnerSystemId, 2, "outer/middle/inner")
			],
			context.Occurrences);
	}

	/// <summary>
	/// A member with no placement at the inner system resolves inherit on both sides.
	/// The factory stores inherit for every anchor. This test does not walk the chain itself.
	/// </summary>
	[Fact]
	public async Task AllInherit_ResolvesInheritFromTheInnerOccurrence()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(useDatabase: false, new ClaimsPrincipal(), CancellationToken.None);

		Assert.NotNull(context);
		var resolved = context.TryResolvePlacement("outer/middle/inner", null, null, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)2, visibility);
		Assert.Equal((byte)2, permission);
	}

	/// <summary>
	/// Middle and Inner inherit, so the outer explicit values are the resolved placement.
	/// The factory stores the modes. This test does not encode them or walk the chain itself.
	/// </summary>
	[Fact]
	public async Task ExplicitOuter_SuppliesTheInheritedValueAtTheInnerOccurrence()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(
			useDatabase: false,
			new ClaimsPrincipal(),
			CancellationToken.None,
			WorkspaceTreeTestDataFactory.ThreeNestedSystemsWithExplicitOuter);

		Assert.NotNull(context);
		var resolved = context.TryResolvePlacement("outer/middle/inner", null, null, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)17, visibility);
		Assert.Equal((byte)16, permission);
	}

	/// <summary>
	/// A stored member placement is kept at the inner system. The outer explicit values are not used.
	/// The member bytes are the non-null inputs. This test does not walk the chain itself.
	/// </summary>
	[Fact]
	public async Task MemberPlacement_OverridesTheAnchorsAtTheInnerOccurrence()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(
			useDatabase: false,
			new ClaimsPrincipal(),
			CancellationToken.None,
			WorkspaceTreeTestDataFactory.ThreeNestedSystemsWithExplicitOuter);

		Assert.NotNull(context);
		var resolved = context.TryResolvePlacement("outer/middle/inner", (byte)18, (byte)17, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)18, visibility);
		Assert.Equal((byte)17, permission);
	}

	/// <summary>
	/// Inner inherits and Middle is explicit, so resolution stops there. Outer is inherit and is not the result.
	/// The factory stores the modes. This test does not walk the chain itself.
	/// </summary>
	[Fact]
	public async Task ExplicitMiddle_StopsTheWalkAtTheInnerOccurrence()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(
			useDatabase: false,
			new ClaimsPrincipal(),
			CancellationToken.None,
			WorkspaceTreeTestDataFactory.ThreeNestedSystemsWithExplicitMiddle);

		Assert.NotNull(context);
		var resolved = context.TryResolvePlacement("outer/middle/inner", null, null, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)17, visibility);
		Assert.Equal((byte)16, permission);
	}

	/// <summary>
	/// Visibility stops at Middle. Permission continues through Middle and stops at Outer.
	/// The factory stores the modes. This test does not walk the chain itself.
	/// </summary>
	[Fact]
	public async Task IndependentSides_StopAtDifferentAnchors()
	{
		var context = await LoadIndependentSides();

		var resolved = context.TryResolvePlacement("outer/middle/inner", null, null, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)17, visibility);
		Assert.Equal((byte)16, permission);
	}

	/// <summary>
	/// The member visibility is kept. Permission is null, so it still resolves from Outer.
	/// </summary>
	[Fact]
	public async Task IndependentSides_MemberVisibilityDoesNotChangePermission()
	{
		var context = await LoadIndependentSides();

		var resolved = context.TryResolvePlacement("outer/middle/inner", (byte)18, null, out var visibility, out var permission, out var error);

		Assert.True(resolved);
		Assert.Equal("", error);
		Assert.Equal((byte)18, visibility);
		Assert.Equal((byte)16, permission);
	}

	/// <summary>Every loaded anchor keeps both bytes, and the tree order stays the same.</summary>
	[Fact]
	public void TreeLoad_RetainsBothBytesWithoutChangingStructure()
	{
		var edges = new[]
		{
			new WorkspaceGraphEdge("account", AccountId, "system", Harbor, 1, "placement"),
			new WorkspaceGraphEdge("system", Harbor, "group", Inner, 1, "placement"),
			new WorkspaceGraphEdge("group", Inner, "place", Shore, 1, "placement")
		};
		var occurrences = new[]
		{
			new WorkspaceOccurrence("account", AccountId, "system", Harbor, 0, "harbor-west"),
			new WorkspaceOccurrence("system", Harbor, "group", Inner, 1, "inner"),
			new WorkspaceOccurrence("group", Inner, "place", Shore, 2, "shore"),
			new WorkspaceOccurrence("account", AccountId, "system", Harbor, 0, "harbor-east")
		};
		var labels = new[]
		{
			new WorkspaceNodeLabel("account", AccountId, "Account"),
			new WorkspaceNodeLabel("system", Harbor, "System: Harbor"),
			new WorkspaceNodeLabel("group", Inner, "Group: Inner"),
			new WorkspaceNodeLabel("place", Shore, "Place: Shore"),
			new WorkspaceNodeLabel("member", MemberId, "Member: Jane")
		};
		var preview = new[]
		{
			Preview("harbor-west", "system", Harbor, null, false),
			Preview("groups-header", "group", null, "harbor-west", true),
			Preview("inner", "group", Inner, "harbor-west", false),
			Preview("shore", "place", Shore, "inner", false),
			Preview("harbor-east", "system", Harbor, null, false),
			Preview("member", "member", MemberId, "harbor-west", false)
		};
		var stored = new[]
		{
			Stored("system", Harbor, "explicit", 1, "unset", null),
			Stored("group", Inner, "inherit", null, "explicit", "editor"),
			Stored("place", Shore, "unset", null, "inherit", null),
			Stored("system", Guid.Parse("99999999-9999-9999-9999-999999999999"), "explicit", 3, "explicit", "owner")
		};

		var context = WorkspaceContext.Create(AccountId, edges, occurrences, labels, [], preview, [], stored);

		Assert.Equal(edges, context.Edges);
		Assert.Equal(occurrences, context.Occurrences);
		Assert.Equal(labels, context.NodeLabels);
		Assert.Equal(preview, context.HierarchyPreview);
		Assert.Equal(["system", "group", "place"], context.AnchorSettings.Select(setting => setting.ObjectType).ToArray());
		Assert.Equal([Harbor, Inner, Shore], context.AnchorSettings.Select(setting => setting.ObjectId).ToArray());
		Assert.DoesNotContain(context.AnchorSettings, setting => setting.ObjectId == MemberId);
		Assert.DoesNotContain(context.AnchorSettings, setting => setting.ObjectType == "account");

		var harbor = Assert.Single(context.AnchorSettings, setting => setting.ObjectId == Harbor);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitOnlyYou, harbor.Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.Unset, harbor.Permission);
		Assert.Equal(harbor, Assert.Single(context.AnchorSettings, setting => setting.ObjectId == occurrences[0].ObjectId));
		Assert.Equal(harbor, Assert.Single(context.AnchorSettings, setting => setting.ObjectId == occurrences[3].ObjectId));

		var group = Assert.Single(context.AnchorSettings, setting => setting.ObjectType == "group");
		Assert.Equal(WorkspaceAnchorSettingCodes.Inherit, group.Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitEditor, group.Permission);

		var place = Assert.Single(context.AnchorSettings, setting => setting.ObjectType == "place");
		Assert.Equal(WorkspaceAnchorSettingCodes.Unset, place.Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.Inherit, place.Permission);
	}

	/// <summary>A missing settings row stays missing on both sides.</summary>
	[Fact]
	public void MissingSettings_StayMissing()
	{
		var context = WorkspaceContext.Create(
			AccountId,
			[],
			[new WorkspaceOccurrence("account", AccountId, "system", Harbor, 0, "harbor")],
			[new WorkspaceNodeLabel("system", Harbor, "Harbor")],
			[],
			[],
			[]);

		var setting = Assert.Single(context.AnchorSettings);
		Assert.Equal(WorkspaceAnchorSettingCodes.Missing, setting.Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.Missing, setting.Permission);
	}

	/// <summary>Each explicit visibility type and permission code keeps its own byte.</summary>
	[Fact]
	public void ExplicitSettings_KeepTheStoredTypeAndCode()
	{
		var night = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var context = WorkspaceContext.Create(
			AccountId,
			[],
			[],
			[
				new WorkspaceNodeLabel("system", Harbor, "Harbor"),
				new WorkspaceNodeLabel("group", Inner, "Inner"),
				new WorkspaceNodeLabel("place", Shore, "Shore"),
				new WorkspaceNodeLabel("system", night, "Night")
			],
			[],
			[],
			[],
			[
				Stored("system", Harbor, "explicit", 2, "explicit", "viewer"),
				Stored("group", Inner, "explicit", 3, "explicit", "manager"),
				Stored("place", Shore, "inherit", null, "explicit", "owner"),
				Stored("system", night, "unset", null, "explicit", "editor")
			]);

		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitTrustedFriends, Setting(context, Harbor).Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitViewer, Setting(context, Harbor).Permission);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitAllFriends, Setting(context, Inner).Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitManager, Setting(context, Inner).Permission);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitOwner, Setting(context, Shore).Permission);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitEditor, Setting(context, night).Permission);
		Assert.Equal(4, context.AnchorSettings.Count);
	}

	/// <summary>An unrecognized stored value is not rewritten as unset, inherit, or explicit.</summary>
	[Fact]
	public void UnrecognizedStoredValues_AreNotCollapsed()
	{
		var labels = new[] { new WorkspaceNodeLabel("system", Harbor, "Harbor") };
		Assert.Throws<WorkspaceOpeningException>(() => WorkspaceContext.Create(
			AccountId, [], [], labels, [], [], [], [Stored("system", Harbor, "public", null, "inherit", null)]));
		Assert.Throws<WorkspaceOpeningException>(() => WorkspaceContext.Create(
			AccountId, [], [], labels, [], [], [], [Stored("system", Harbor, "explicit", 9, "unset", null)]));
		Assert.Throws<WorkspaceOpeningException>(() => WorkspaceContext.Create(
			AccountId, [], [], labels, [], [], [], [Stored("system", Harbor, "unset", 1, "unset", null)]));
		Assert.Throws<WorkspaceOpeningException>(() => WorkspaceContext.Create(
			AccountId, [], [], labels, [], [], [], [Stored("system", Harbor, "inherit", null, "explicit", "admin")]));
		Assert.Throws<WorkspaceOpeningException>(() => WorkspaceContext.Create(
			AccountId,
			[],
			[],
			labels,
			[],
			[],
			[],
			[
				Stored("system", Harbor, "inherit", null, "inherit", null),
				Stored("system", Harbor, "unset", null, "unset", null)
			]));
	}

	/// <summary>The opening read is limited to this account's stored settings and does not read roles.</summary>
	[Fact]
	public void OpeningRead_UsesTheAccountOwnedSettingsTable()
	{
		var command = Assert.Single(
			WorkspaceOpeningPlan.Commands,
			item => item.Kind == WorkspaceOpeningCommandKind.ReadAnchorSettings);
		Assert.True(command.PassesAccountId);
		Assert.Contains("p.AccountId = @AccountId", command.Text, StringComparison.Ordinal);
		Assert.Contains("dbo.pb_anchor_visibility_permissions", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.VisibilityMode", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.VisibilityTypeId", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.PermissionMode", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.PermissionCode", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.SystemId", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.GroupId", command.Text, StringComparison.Ordinal);
		Assert.Contains("p.PlaceId", command.Text, StringComparison.Ordinal);
		Assert.DoesNotContain("pb_roles", command.Text, StringComparison.Ordinal);
		Assert.DoesNotContain("pb_object_membership_roles", command.Text, StringComparison.Ordinal);
		Assert.Equal(WorkspaceOpeningCommandKind.ReadAnchorNames, WorkspaceOpeningPlan.Commands[^2].Kind);
	}

	/// <summary>A committed build publishes the encoded settings and leaves the preview order unchanged.</summary>
	[Fact]
	public async Task Service_PublishesTheRetainedSettings()
	{
		var store = new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger());
		var occurrences = new[]
		{
			new WorkspaceOccurrence("account", AccountId, "system", Harbor, 0, "west"),
			new WorkspaceOccurrence("account", AccountId, "system", Harbor, 0, "east")
		};
		var preview = new[]
		{
			Preview("west", "system", Harbor, null, false),
			Preview("east", "system", Harbor, null, false)
		};
		var build = new WorkspaceCapturedBuild(
			AccountId,
			[],
			occurrences,
			[new WorkspaceNodeLabel("system", Harbor, "Harbor")],
			[],
			preview,
			[],
			[Stored("system", Harbor, "explicit", 3, "explicit", "viewer")]);
		var session = new ReadySession(build);
		var response = await new WorkspaceTreeService(session, store).OpenAsync(new ClaimsPrincipal(), CancellationToken.None);

		Assert.NotNull(response);
		Assert.Equal(preview, response.HierarchyPreview);
		var context = store.Acquire(AccountId, response.ContextId).Context;
		Assert.NotNull(context);
		Assert.Equal(occurrences, context.Occurrences);
		var setting = Assert.Single(context.AnchorSettings);
		Assert.Equal(Harbor, setting.ObjectId);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitAllFriends, setting.Visibility);
		Assert.Equal(WorkspaceAnchorSettingCodes.ExplicitViewer, setting.Permission);
		Assert.Equal(setting, Assert.Single(context.AnchorSettings, item => item.ObjectId == occurrences[0].ObjectId && item.ObjectType == occurrences[0].ObjectType));
		Assert.Equal(setting, Assert.Single(context.AnchorSettings, item => item.ObjectId == occurrences[1].ObjectId && item.ObjectType == occurrences[1].ObjectType));
	}

	private static async Task<WorkspaceContext> LoadIndependentSides()
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(
			useDatabase: false,
			new ClaimsPrincipal(),
			CancellationToken.None,
			WorkspaceTreeTestDataFactory.ThreeNestedSystemsWithIndependentSides);
		Assert.NotNull(context);
		return context;
	}

	private static WorkspaceAnchorSetting Setting(WorkspaceContext context, Guid objectId)
	{
		return Assert.Single(context.AnchorSettings, setting => setting.ObjectId == objectId);
	}

	private static WorkspaceStoredAnchorSetting Stored(
		string objectType,
		Guid objectId,
		string? visibilityMode,
		int? visibilityTypeId,
		string? permissionMode,
		string? permissionCode)
	{
		return new WorkspaceStoredAnchorSetting(objectType, objectId, visibilityMode, visibilityTypeId, permissionMode, permissionCode);
	}

	private static PluralMapPreviewRow Preview(string path, string? type, Guid? id, string? parent, bool header)
	{
		return new PluralMapPreviewRow("preview", path, path, path, type, id, parent, header, path);
	}

	/// <summary>Fails if the factory path opens the database session.</summary>
	private sealed class GuardSession : IWorkspaceOpeningSession
	{
		public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The factory path must not open the database session.");
		}
	}

	private sealed class ReadySession(WorkspaceCapturedBuild build) : IWorkspaceOpeningSession
	{
		public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<WorkspaceOpeningResult>(new WorkspaceOpeningResult.Ready(build));
		}
	}

	private sealed class SilentLogger : Microsoft.Extensions.Logging.ILogger<WorkspaceContextStore>
	{
		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull
		{
			return Empty.Instance;
		}

		public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
		{
			return false;
		}

		public void Log<TState>(
			Microsoft.Extensions.Logging.LogLevel logLevel,
			Microsoft.Extensions.Logging.EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
		}

		private sealed class Empty : IDisposable
		{
			internal static readonly Empty Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
