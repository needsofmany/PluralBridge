// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Outcome of the inherited Owner walk: the write is allowed, an Owner role is required, or the branch is not a valid parent chain.
/// </summary>
internal enum AnchorWriteDecision
{
	Allowed,
	OwnerRequired,
	InvalidBranch
}

/// <summary>
/// One locked branch node: its anchor permission mode, whether this account has an active Owner role on it, and whether the source rows prove the next link.
/// </summary>
/// <param name="ObjectType">system, group, or place.</param>
/// <param name="ObjectId">The anchor id for this node.</param>
/// <param name="PermissionMode">inherit, explicit, or another stored mode. Null is treated as not inheriting.</param>
/// <param name="HasActiveOwner">True when the account has an active Owner role on this node. A permission code of owner does not set this.</param>
/// <param name="LinkedToNext">True when the locked source rows show this node is a child of the next node. True for the last node, which has no next node.</param>
/// <param name="AccountChild">True when this node is a direct child of the account.</param>
internal readonly record struct AnchorWriteFact(
	string ObjectType,
	Guid ObjectId,
	string? PermissionMode,
	bool HasActiveOwner,
	bool LinkedToNext,
	bool AccountChild);

/// <summary>
/// Shared write gate for profile Save and immediate Fronting Save.
/// An active Owner role on the selected anchor is enough. Otherwise only PermissionMode inherit follows the selected branch, and only an active Owner role on that chain grants authority.
/// </summary>
/// <remarks>
/// A permission code of owner is not a role. Explicit and Not set stop the walk. The account boundary denies when the root itself has no Owner grant.
/// </remarks>
internal static class AnchorWriteAuthorization
{
	/// <summary>
	/// Decides whether the supplied branch facts grant an active Owner role for the selected anchor.
	/// </summary>
	/// <param name="selectedType">The anchor type being written. It must match the first fact.</param>
	/// <param name="selectedId">The anchor id being written. It must match the first fact.</param>
	/// <param name="branch">Facts ordered from the selected anchor upward. Null is not accepted. Fewer than 1 or more than 32 nodes is invalid.</param>
	/// <returns>Allowed when an Owner role is reached, OwnerRequired when inheritance stops or the account boundary has no grant, or InvalidBranch when the shape or links fail.</returns>
	/// <remarks>
	/// The node's own grant is checked before its mode, so Inherit on a root still reaches that root's Owner role. A node without a grant continues only while its mode is inherit.
	/// </remarks>
	internal static AnchorWriteDecision Decide(
		string selectedType,
		Guid selectedId,
		IReadOnlyList<AnchorWriteFact> branch)
	{
		if (branch.Count is < 1 or > 32
			|| !string.Equals(branch[0].ObjectType, selectedType, StringComparison.Ordinal)
			|| branch[0].ObjectId != selectedId)
		{
			return AnchorWriteDecision.InvalidBranch;
		}

		// Index 0 is the selected anchor. Each later node must be the locked
		// source parent of the previous one, and the last node must be a
		// child of the account. A repeated anchor or a missing link is not
		// a branch, even when the selected anchor itself has Owner.
		var seen = new HashSet<string>(branch.Count);
		for (var index = 0; index < branch.Count; index++)
		{
			var node = branch[index];
			if (node.ObjectType is not ("system" or "group" or "place")
				|| !seen.Add(node.ObjectType + ":" + node.ObjectId.ToString("D"))
				|| (index < branch.Count - 1 && !node.LinkedToNext)
				|| (index == branch.Count - 1 && !node.AccountChild))
			{
				return AnchorWriteDecision.InvalidBranch;
			}
		}

		// Check the node's own grant before its mode, so Inherit on a root
		// still reaches that root's Owner role. A node without a grant
		// continues only while its mode is inherit.
		for (var index = 0; index < branch.Count; index++)
		{
			if (branch[index].HasActiveOwner)
			{
				return AnchorWriteDecision.Allowed;
			}

			if (!string.Equals(branch[index].PermissionMode, "inherit", StringComparison.Ordinal))
			{
				return AnchorWriteDecision.OwnerRequired;
			}
		}

		return AnchorWriteDecision.OwnerRequired;
	}

	/// <summary>
	/// Locks the selected branch and throws inside the caller's transaction unless that branch grants an active Owner role.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/>.</param>
	/// <param name="transaction">The open write transaction. Locks are held until it commits or rolls back.</param>
	/// <param name="accountId">The authenticated account. Grants and links are read for this account only.</param>
	/// <param name="selectedType">Anchor type being written. It must be the first branch node.</param>
	/// <param name="selectedId">Anchor id being written. It must be the first branch node.</param>
	/// <param name="branch">Browser branch string, or null or whitespace, which is an invalid branch.</param>
	/// <param name="cancellationToken">Cancels the lock read or the thrown SQL error. Cancellation does not commit.</param>
	/// <returns>The task completes when the branch is authorized. An unauthorized or invalid branch faults the task with a SQL error.</returns>
	/// <remarks>
	/// Branch links are not taken from #GraphEdges. That snapshot is used only to test that the member sits under the selected anchor.
	/// </remarks>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">51021 when the branch cannot be read or locked as submitted. 51014 when the walk finds no active Owner role.</exception>
	internal static async Task RequireAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		string selectedType,
		Guid selectedId,
		string? branch,
		CancellationToken cancellationToken)
	{
		// Caller supplies the open write transaction. Locks taken below are
		// held until that transaction commits or rolls back. Branch links
		// are not taken from #GraphEdges. That snapshot is used only to
		// test that the member sits under the selected anchor.
		if (!TryReadBranch(branch, selectedType, selectedId, out var nodes))
		{
			await ThrowAsync(connection, transaction, AnchorWriteDecision.InvalidBranch, cancellationToken);
			return;
		}

		var facts = await ReadFactsAsync(connection, transaction, accountId, nodes, cancellationToken);
		// A short or reordered lock result is not the submitted branch.
		var decision = facts.Count == nodes.Count
			? Decide(selectedType, selectedId, facts)
			: AnchorWriteDecision.InvalidBranch;
		for (var index = 0; decision == AnchorWriteDecision.Allowed && index < nodes.Count; index++)
		{
			if (!string.Equals(facts[index].ObjectType, nodes[index].ObjectType, StringComparison.Ordinal)
				|| facts[index].ObjectId != nodes[index].ObjectId)
			{
				decision = AnchorWriteDecision.InvalidBranch;
			}
		}
		if (decision != AnchorWriteDecision.Allowed)
		{
			await ThrowAsync(connection, transaction, decision, cancellationToken);
		}
	}

	/// <summary>
	/// Parses the browser branch into nodes from the selected anchor upward.
	/// </summary>
	/// <param name="branch">Comma-separated type:id pairs. Null, whitespace, an empty segment, or more than 32 nodes is rejected.</param>
	/// <param name="selectedType">Required type of the first node.</param>
	/// <param name="selectedId">Required id of the first node.</param>
	/// <param name="nodes">The parsed nodes when the method returns true. Empty when it returns false.</param>
	/// <returns>True when every segment is a system, group, or place with a non-empty id, none repeat, and the first node is the selected anchor.</returns>
	/// <remarks>One unreadable segment rejects the whole string. The chain is not shortened to a readable prefix.</remarks>
	internal static bool TryReadBranch(
		string? branch,
		string selectedType,
		Guid selectedId,
		out IReadOnlyList<(string ObjectType, Guid ObjectId)> nodes)
	{
		nodes = Array.Empty<(string, Guid)>();
		if (string.IsNullOrWhiteSpace(branch))
		{
			return false;
		}

		var parts = branch.Split(',');
		if (parts.Length is < 1 or > 32)
		{
			return false;
		}

		var parsed = new List<(string ObjectType, Guid ObjectId)>(parts.Length);
		var seen = new HashSet<string>(parts.Length);
		for (var index = 0; index < parts.Length; index++)
		{
			var split = parts[index].Trim().Split(':', 2, StringSplitOptions.TrimEntries);
			if (split.Length != 2
				|| split[0] is not ("system" or "group" or "place")
				|| !Guid.TryParse(split[1], out var objectId)
				|| objectId == Guid.Empty
				|| !seen.Add(split[0] + ":" + objectId.ToString("D")))
			{
				return false;
			}

			parsed.Add((split[0], objectId));
		}

		if (!string.Equals(parsed[0].ObjectType, selectedType, StringComparison.Ordinal)
			|| parsed[0].ObjectId != selectedId)
		{
			return false;
		}

		nodes = parsed;
		return true;
	}

	/// <summary>
	/// Reads locked fact rows for the submitted nodes, in ordinal order.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/>.</param>
	/// <param name="transaction">Write transaction that holds the UPDLOCK and HOLDLOCK hints until it ends.</param>
	/// <param name="accountId">Account whose grants, memberships, and placements are locked.</param>
	/// <param name="nodes">Branch from the selected anchor upward. The next node is treated as the parent.</param>
	/// <param name="cancellationToken">Cancels the read. The caller's transaction remains open.</param>
	/// <returns>The locked facts when the task completes. The query starts from the submitted OPENJSON nodes and left-joins settings, so a missing link or Owner grant is a false flag. A missing source object does not by itself remove a submitted node. The caller still rejects a result whose count or identity does not match the submitted nodes.</returns>
	/// <remarks>
	/// A parent link is a placement, a legacy system relationship between two active memberships, or a group under its resolved parent group or under its system when that resolved parent is null.
	/// The account edge is a placement whose parent is the account. The legacy system-root alternative applies only when this account has no placement roots.
	/// </remarks>
	private static async Task<IReadOnlyList<AnchorWriteFact>> ReadFactsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		IReadOnlyList<(string ObjectType, Guid ObjectId)> nodes,
		CancellationToken cancellationToken)
	{
		// One set-based read. UPDLOCK, HOLDLOCK keeps the grant, the anchor
		// setting, and the source link stable through the write. A parent
		// link is a placement, a legacy system relationship between two
		// active memberships, or a group under its system or resolved
		// parent group. The account edge is a placement whose parent is
		// the account. The legacy system-root alternative applies only
		// when this account has no placement roots.
		const string sql = """
			SELECT
			    node.Ordinal,
			    node.ObjectType,
			    node.ObjectId,
			    setting.PermissionMode,
			    CASE WHEN EXISTS
			    (
			        SELECT 1
			        FROM dbo.pb_object_memberships AS om WITH (UPDLOCK, HOLDLOCK)
			        INNER JOIN dbo.pb_system_membership_statuses AS status
			            ON status.MembershipStatusId = om.MembershipStatusId
			        INNER JOIN dbo.pb_object_membership_roles AS mr WITH (UPDLOCK, HOLDLOCK)
			            ON mr.ObjectMembershipId = om.ObjectMembershipId
			        INNER JOIN dbo.pb_roles AS role
			            ON role.RoleId = mr.RoleId
			        WHERE om.AccountId = @AccountId
			          AND om.ObjectType COLLATE Latin1_General_100_BIN2
			              = node.ObjectType COLLATE Latin1_General_100_BIN2
			          AND TRY_CONVERT(uniqueidentifier, om.ObjectId) = node.ObjectId
			          AND status.StatusName = N'Active'
			          AND status.IsActive = 1
			          AND role.RoleName = N'Owner'
			          AND role.IsActive = 1
			    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasActiveOwner,
			    CASE
			        WHEN node.ParentObjectId IS NULL THEN CAST(1 AS bit)
			        WHEN EXISTS
			        (
			            SELECT 1
			            FROM dbo.pb_account_object_placements AS placement WITH (UPDLOCK, HOLDLOCK)
			            WHERE placement.AccountId = @AccountId
			              AND placement.ParentObjectType COLLATE Latin1_General_100_BIN2
			                  = node.ParentObjectType COLLATE Latin1_General_100_BIN2
			              AND TRY_CONVERT(uniqueidentifier, placement.ParentObjectId) = node.ParentObjectId
			              AND placement.ChildObjectType COLLATE Latin1_General_100_BIN2
			                  = node.ObjectType COLLATE Latin1_General_100_BIN2
			              AND TRY_CONVERT(uniqueidentifier, placement.ChildObjectId) = node.ObjectId
			        )
			        OR EXISTS
			        (
			            SELECT 1
			            FROM dbo.pb_system_relationships AS relationship WITH (UPDLOCK, HOLDLOCK)
			            INNER JOIN dbo.pb_system_memberships AS childMembership WITH (UPDLOCK, HOLDLOCK)
			                ON childMembership.AccountId = @AccountId
			               AND childMembership.SystemId = relationship.SystemId
			            INNER JOIN dbo.pb_system_membership_statuses AS childStatus
			                ON childStatus.MembershipStatusId = childMembership.MembershipStatusId
			               AND childStatus.StatusName = N'Active'
			               AND childStatus.IsActive = 1
			            INNER JOIN dbo.pb_system_memberships AS parentMembership WITH (UPDLOCK, HOLDLOCK)
			                ON parentMembership.AccountId = @AccountId
			               AND parentMembership.SystemId = relationship.ParentSystemId
			            INNER JOIN dbo.pb_system_membership_statuses AS parentStatus
			                ON parentStatus.MembershipStatusId = parentMembership.MembershipStatusId
			               AND parentStatus.StatusName = N'Active'
			               AND parentStatus.IsActive = 1
			            WHERE node.ObjectType = N'system'
			              AND node.ParentObjectType = N'system'
			              AND relationship.SystemId = node.ObjectId
			              AND relationship.ParentSystemId = node.ParentObjectId
			              AND relationship.TargetSystemId IS NULL
			        )
			        OR EXISTS
			        (
			            SELECT 1
			            FROM dbo.pb_groups AS childGroup WITH (UPDLOCK, HOLDLOCK)
			            LEFT JOIN dbo.pb_groups AS sourceParent WITH (UPDLOCK, HOLDLOCK)
			                ON sourceParent.SystemId = childGroup.SystemId
			               AND sourceParent.SourceGroupId = childGroup.ParentSourceGroupId
			            WHERE node.ObjectType = N'group'
			              AND childGroup.GroupId = node.ObjectId
			              AND (
			                    (node.ParentObjectType = N'group'
			                     AND COALESCE(childGroup.ParentGroupId, sourceParent.GroupId) = node.ParentObjectId)
			                    OR (node.ParentObjectType = N'system'
			                        AND childGroup.SystemId = node.ParentObjectId
			                        AND COALESCE(childGroup.ParentGroupId, sourceParent.GroupId) IS NULL)
			                  )
			        ) THEN CAST(1 AS bit)
			        ELSE CAST(0 AS bit)
			    END AS LinkedToNext,
			    CASE
			        WHEN EXISTS
			        (
			            SELECT 1
			            FROM dbo.pb_account_object_placements AS placement WITH (UPDLOCK, HOLDLOCK)
			            WHERE placement.AccountId = @AccountId
			              AND placement.ParentObjectType = N'account'
			              AND TRY_CONVERT(uniqueidentifier, placement.ParentObjectId) = @AccountId
			              AND placement.ChildObjectType COLLATE Latin1_General_100_BIN2
			                  = node.ObjectType COLLATE Latin1_General_100_BIN2
			              AND TRY_CONVERT(uniqueidentifier, placement.ChildObjectId) = node.ObjectId
			        ) THEN CAST(1 AS bit)
			        WHEN node.ObjectType = N'system'
			         AND NOT EXISTS
			         (
			             SELECT 1
			             FROM dbo.pb_account_object_placements AS placementRoot WITH (UPDLOCK, HOLDLOCK)
			             WHERE placementRoot.AccountId = @AccountId
			               AND placementRoot.ParentObjectType = N'account'
			               AND placementRoot.ChildObjectType IN (N'group', N'system', N'place')
			         )
			         AND EXISTS
			         (
			             SELECT 1
			             FROM dbo.pb_system_memberships AS membership WITH (UPDLOCK, HOLDLOCK)
			             INNER JOIN dbo.pb_system_membership_statuses AS status
			                 ON status.MembershipStatusId = membership.MembershipStatusId
			             WHERE membership.AccountId = @AccountId
			               AND membership.SystemId = node.ObjectId
			               AND status.StatusName = N'Active'
			               AND status.IsActive = 1
			         )
			         AND NOT EXISTS
			         (
			             SELECT 1
			             FROM dbo.pb_system_relationships AS relationship WITH (UPDLOCK, HOLDLOCK)
			             WHERE relationship.SystemId = node.ObjectId
			               AND relationship.ParentSystemId IS NOT NULL
			               AND relationship.TargetSystemId IS NULL
			         ) THEN CAST(1 AS bit)
			        ELSE CAST(0 AS bit)
			    END AS AccountChild
			FROM OPENJSON(@BranchNodes)
			WITH
			(
			    Ordinal int '$.ordinal',
			    ObjectType nvarchar(32) '$.objectType',
			    ObjectId uniqueidentifier '$.objectId',
			    ParentObjectType nvarchar(32) '$.parentType',
			    ParentObjectId uniqueidentifier '$.parentId'
			) AS node
			LEFT JOIN dbo.pb_anchor_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
			    ON setting.AccountId = @AccountId
			   AND (
			        (node.ObjectType = N'system' AND setting.SystemId = node.ObjectId)
			        OR (node.ObjectType = N'group' AND setting.GroupId = node.ObjectId)
			        OR (node.ObjectType = N'place' AND setting.PlaceId = node.ObjectId)
			   )
			ORDER BY node.Ordinal;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		command.Parameters.Add("@BranchNodes", SqlDbType.NVarChar, -1).Value = BranchNodesJson(nodes);
		var facts = new List<AnchorWriteFact>(nodes.Count);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			facts.Add(new AnchorWriteFact(
				reader.GetString(1),
				reader.GetGuid(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.GetBoolean(4),
				reader.GetBoolean(5),
				reader.GetBoolean(6)));
		}

		return facts;
	}

	/// <summary>
	/// Builds the JSON array consumed by the lock query. Each node names the following node as its parent.
	/// </summary>
	/// <param name="nodes">Branch from the selected anchor upward. The last node has no parent fields.</param>
	/// <returns>A JSON array of ordinal, object type, object id, and, except for the last node, parent type and parent id.</returns>
	private static string BranchNodesJson(IReadOnlyList<(string ObjectType, Guid ObjectId)> nodes)
	{
		var json = new StringBuilder();
		json.Append('[');
		for (var index = 0; index < nodes.Count; index++)
		{
			if (index > 0)
			{
				json.Append(',');
			}

			json.Append("{\"ordinal\":");
			json.Append(index);
			json.Append(",\"objectType\":\"");
			json.Append(nodes[index].ObjectType);
			json.Append("\",\"objectId\":\"");
			json.Append(nodes[index].ObjectId.ToString("D"));
			json.Append('"');
			if (index + 1 < nodes.Count)
			{
				json.Append(",\"parentType\":\"");
				json.Append(nodes[index + 1].ObjectType);
				json.Append("\",\"parentId\":\"");
				json.Append(nodes[index + 1].ObjectId.ToString("D"));
				json.Append('"');
			}

			json.Append('}');
		}

		json.Append(']');
		return json.ToString();
	}

	/// <summary>
	/// Raises the SQL error for a failed write decision inside the caller's transaction.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/>.</param>
	/// <param name="transaction">The write transaction. This method does not commit or roll it back.</param>
	/// <param name="decision">InvalidBranch raises 51021. Any other decision raises 51014.</param>
	/// <param name="cancellationToken">Cancels the THROW statement.</param>
	/// <returns>The task does not complete successfully. The SQL error faults it.</returns>
	/// <remarks>MeController maps 51014 and 51021. This method does not commit.</remarks>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">51021 when <paramref name="decision"/> is InvalidBranch. 51014 otherwise.</exception>
	private static async Task ThrowAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		AnchorWriteDecision decision,
		CancellationToken cancellationToken)
	{
		var sql = decision == AnchorWriteDecision.InvalidBranch
			? "THROW 51021, 'The selected branch is not valid.', 1;"
			: "THROW 51014, 'An active Owner role is required on the selected anchor.', 1;";
		await using var command = new SqlCommand(sql, connection, transaction);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
