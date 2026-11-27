// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;

namespace PluralBridge.Api.Workspace;

/// <summary>One command in the opening build, in execution order.</summary>
/// <param name="Kind">How the session executes the command.</param>
/// <param name="Text">SQL text or a procedure name.</param>
/// <param name="PassesAccountId">True when the command receives the authenticated account id.</param>
internal sealed record WorkspaceOpeningCommand(WorkspaceOpeningCommandKind Kind, string Text, bool PassesAccountId);

/// <summary>The execution shape for one opening command.</summary>
internal enum WorkspaceOpeningCommandKind
{
	/// <summary>A SQL batch, including caller-owned temp table creation.</summary>
	Text,

	/// <summary>An installed stored procedure.</summary>
	StoredProcedure,

	/// <summary>Read the three render result sets.</summary>
	ReadRender,

	/// <summary>Copy graph, walk, node, and system-edge rows.</summary>
	ReadCopies,

	/// <summary>Read membership facts for anchors in the copied nodes.</summary>
	ReadMemberships,

	/// <summary>Read this account's stored visibility and permission rows for anchors in the copied nodes.</summary>
	ReadAnchorSettings,

	/// <summary>Read anchor names inside the same transaction.</summary>
	ReadAnchorNames,

	/// <summary>Commit the snapshot transaction. Nothing in the plan follows this marker.</summary>
	Commit
}

/// <summary>
/// The one opening sequence. The SQL session executes this list and does not call step 4.
/// </summary>
internal static class WorkspaceOpeningPlan
{
	/// <summary>The required transaction isolation. The session does not substitute another level.</summary>
	internal const IsolationLevel Isolation = IsolationLevel.Snapshot;

	/// <summary>The existing plural-map procedure. It must not appear in <see cref="Commands"/>.</summary>
	internal const string Step4Procedure = "dbo.pb_show_tree_step4_members_by_email";

	internal const string SeedProcedure = "dbo.pb_show_tree_step5_topology_seed_builder";
	internal const string WalkerProcedure = "dbo.pb_show_tree_step5_topology_walker";
	internal const string ExpansionProcedure = "dbo.pb_show_tree_step5_graph_expansion";
	internal const string LabelerProcedure = "dbo.pb_show_tree_step5_final_graph_walk_and_node_labeler";
	internal const string RenderProcedure = "dbo.pb_show_tree_step5_render_layer";

	/// <summary>SQL Server error when snapshot isolation is not allowed. The build stops.</summary>
	internal const int SnapshotNotAllowedError = 3952;

	private static readonly WorkspaceOpeningCommand[] CommandList =
	[
		new(WorkspaceOpeningCommandKind.Text, TempTables, false),
		new(WorkspaceOpeningCommandKind.StoredProcedure, SeedProcedure, true),
		new(WorkspaceOpeningCommandKind.StoredProcedure, WalkerProcedure, true),
		new(WorkspaceOpeningCommandKind.StoredProcedure, ExpansionProcedure, true),
		new(WorkspaceOpeningCommandKind.StoredProcedure, LabelerProcedure, true),
		new(WorkspaceOpeningCommandKind.ReadRender, RenderProcedure, false),
		new(WorkspaceOpeningCommandKind.ReadCopies, CopyRows, false),
		new(WorkspaceOpeningCommandKind.ReadMemberships, MembershipRows, true),
		new(WorkspaceOpeningCommandKind.ReadAnchorSettings, AnchorSettingRows, true),
		new(WorkspaceOpeningCommandKind.ReadAnchorNames, "anchor-names", false),
		new(WorkspaceOpeningCommandKind.Commit, "commit", false)
	];

	/// <summary>The opening commands, ending with the commit marker.</summary>
	internal static IReadOnlyList<WorkspaceOpeningCommand> Commands => CommandList;

	/// <summary>
	/// Caller-owned temp tables. The column lists match the tables step 4 creates today.
	/// The caller creates them so they survive the installed procedures until this connection closes.
	/// </summary>
	private const string TempTables = """
		CREATE TABLE #TopologyEdges
		(
		    ParentObjectType NVARCHAR(32) NOT NULL,
		    ParentObjectId UNIQUEIDENTIFIER NOT NULL,
		    ChildObjectType NVARCHAR(32) NOT NULL,
		    ChildObjectId UNIQUEIDENTIFIER NOT NULL,
		    EdgeRank INT NOT NULL,
		    EdgeSource NVARCHAR(64) NOT NULL
		);
		CREATE TABLE #SystemEdgeLabels
		(
		    SystemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
		    EdgeLabel NVARCHAR(MAX) NULL
		);
		CREATE TABLE #WalkTopology
		(
		    ParentObjectType NVARCHAR(32) NOT NULL,
		    ParentObjectId UNIQUEIDENTIFIER NOT NULL,
		    ObjectType NVARCHAR(32) NOT NULL,
		    ObjectId UNIQUEIDENTIFIER NOT NULL,
		    Depth INT NOT NULL,
		    PathKey NVARCHAR(MAX) NOT NULL
		);
		CREATE TABLE #GraphEdges
		(
		    ParentObjectType NVARCHAR(32) NOT NULL,
		    ParentObjectId UNIQUEIDENTIFIER NOT NULL,
		    ChildObjectType NVARCHAR(32) NOT NULL,
		    ChildObjectId UNIQUEIDENTIFIER NOT NULL,
		    EdgeRank INT NOT NULL,
		    EdgeSource NVARCHAR(64) NOT NULL
		);
		CREATE TABLE #WalkGraph
		(
		    ParentObjectType NVARCHAR(32) NOT NULL,
		    ParentObjectId UNIQUEIDENTIFIER NOT NULL,
		    ObjectType NVARCHAR(32) NOT NULL,
		    ObjectId UNIQUEIDENTIFIER NOT NULL,
		    Depth INT NOT NULL,
		    PathKey NVARCHAR(MAX) NOT NULL
		);
		CREATE TABLE #GraphNodes
		(
		    ObjectType NVARCHAR(32) NOT NULL,
		    ObjectId UNIQUEIDENTIFIER NOT NULL,
		    NodeLabel NVARCHAR(MAX) NOT NULL
		);
		""";

	private const string CopyRows = """
		SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource FROM #GraphEdges;
		SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey FROM #WalkGraph;
		SELECT ObjectType, ObjectId, NodeLabel FROM #GraphNodes;
		SELECT SystemId, EdgeLabel FROM #SystemEdgeLabels;
		""";

	private const string MembershipRows = """
		SELECT
		    om.ObjectMembershipId,
		    om.ObjectType,
		    TRY_CONVERT(uniqueidentifier, om.ObjectId) AS ObjectId,
		    om.MembershipStatusId,
		    s.StatusName,
		    s.IsActive
		FROM dbo.pb_object_memberships AS om
		LEFT JOIN dbo.pb_system_membership_statuses AS s
		    ON s.MembershipStatusId = om.MembershipStatusId
		INNER JOIN #GraphNodes AS n
		    ON n.ObjectType = om.ObjectType
		   AND n.ObjectId = TRY_CONVERT(uniqueidentifier, om.ObjectId)
		WHERE om.AccountId = @AccountId
		  AND n.ObjectType IN (N'system', N'group', N'place')
		  AND n.ObjectId <> '00000000-0000-0000-0000-000000000000';
		""";

	/// <summary>
	/// Stored visibility and permission for anchors already copied into this build.
	/// A missing row is not returned. The loader records that absence as missing.
	/// This read does not walk parent anchors and does not read roles.
	/// </summary>
	private const string AnchorSettingRows = """
		SELECT
		    n.ObjectType,
		    n.ObjectId,
		    p.VisibilityMode,
		    p.VisibilityTypeId,
		    p.PermissionMode,
		    p.PermissionCode
		FROM dbo.pb_anchor_visibility_permissions AS p
		INNER JOIN #GraphNodes AS n
		    ON (
		        (n.ObjectType = N'system' AND n.ObjectId = p.SystemId)
		        OR (n.ObjectType = N'group' AND n.ObjectId = p.GroupId)
		        OR (n.ObjectType = N'place' AND n.ObjectId = p.PlaceId)
		    )
		WHERE p.AccountId = @AccountId
		  AND n.ObjectType IN (N'system', N'group', N'place')
		  AND n.ObjectId <> '00000000-0000-0000-0000-000000000000';
		""";
}
