// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal enum AnchorWriteDecision
{
	Allowed,
	OwnerRequired,
	InvalidBranch
}

internal readonly record struct AnchorWriteFact(
	string ObjectType,
	Guid ObjectId,
	string? PermissionMode,
	bool HasActiveOwner,
	bool LinkedToNext,
	bool AccountChild);

internal static class AnchorWriteAuthorization
{
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

	internal static async Task RequireAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		string selectedType,
		Guid selectedId,
		string? branch,
		CancellationToken cancellationToken)
	{
		if (!TryReadBranch(branch, selectedType, selectedId, out var nodes))
		{
			await ThrowAsync(connection, transaction, AnchorWriteDecision.InvalidBranch, cancellationToken);
			return;
		}

		var facts = await ReadFactsAsync(connection, transaction, accountId, nodes, cancellationToken);
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

	private static async Task<IReadOnlyList<AnchorWriteFact>> ReadFactsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		IReadOnlyList<(string ObjectType, Guid ObjectId)> nodes,
		CancellationToken cancellationToken)
	{
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
