// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal static class AnchorMemberProfileSave
{
	internal const int DisplayNameMaxLength = 510;
	internal const int PronounsMaxLength = 510;
	internal const int ColorMaxLength = 64;

	internal const int NoMembership = 51011;
	internal const int MultipleMemberships = 51012;
	internal const int InactiveMembership = 51013;
	internal const int OwnerRequired = 51014;
	internal const int MemberMissing = 51015;
	internal const int NotStructuralChild = 51016;

	internal sealed record SavedProfile(
		Guid MemberId,
		string DisplayName,
		string? Pronouns,
		string? Description,
		string? Color,
		DateTime UpdatedAtUtc);

	internal sealed record PreparedSave(
		string AnchorObjectType,
		Guid AnchorObjectId,
		bool SetDisplayName,
		string? DisplayName,
		bool SetPronouns,
		string? Pronouns,
		bool SetDescription,
		string? Description,
		bool SetColor,
		string? Color);

	internal static bool TryPrepare(
		AnchorMemberProfileSaveRequest? request,
		out PreparedSave? prepared,
		out string? error)
	{
		prepared = null;
		error = null;

		if (request is null)
		{
			error = "A member profile is required.";
			return false;
		}

		var anchorObjectType = request.AnchorObjectType?.Trim();
		if (anchorObjectType is not ("system" or "group" or "place"))
		{
			error = "Anchor object type must be system, group, or place.";
			return false;
		}

		if (!Guid.TryParse(request.AnchorObjectId, out var anchorObjectId) || anchorObjectId == Guid.Empty)
		{
			error = "Anchor object id must be the selected anchor id.";
			return false;
		}

		if (!TryReadText(request.DisplayName, DisplayNameMaxLength, "Name", out var setDisplayName, out var displayName, out error))
		{
			return false;
		}

		if (!TryReadText(request.Pronouns, PronounsMaxLength, "Pronouns", out var setPronouns, out var pronouns, out error))
		{
			return false;
		}

		if (!TryReadText(request.Description, null, "Description", out var setDescription, out var description, out error))
		{
			return false;
		}

		if (!TryReadText(request.Color, ColorMaxLength, "Color", out var setColor, out var color, out error))
		{
			return false;
		}

		if (setDisplayName && string.IsNullOrWhiteSpace(displayName))
		{
			error = "Name is required.";
			return false;
		}

		if (!setDisplayName && !setPronouns && !setDescription && !setColor)
		{
			error = "No profile fields were submitted.";
			return false;
		}

		prepared = new PreparedSave(
			anchorObjectType,
			anchorObjectId,
			setDisplayName,
			displayName,
			setPronouns,
			pronouns,
			setDescription,
			description,
			setColor,
			color);
		return true;
	}

	internal static async Task CreateGraphTablesAsync(
		SqlConnection connection,
		CancellationToken cancellationToken)
	{
		const string sql = """
			CREATE TABLE #TopologyEdges
			(
			    ParentObjectType nvarchar(32) NOT NULL,
			    ParentObjectId uniqueidentifier NOT NULL,
			    ChildObjectType nvarchar(32) NOT NULL,
			    ChildObjectId uniqueidentifier NOT NULL,
			    EdgeRank int NOT NULL,
			    EdgeSource nvarchar(64) NOT NULL
			);

			CREATE TABLE #SystemEdgeLabels
			(
			    SystemId uniqueidentifier NOT NULL PRIMARY KEY,
			    EdgeLabel nvarchar(max) NULL
			);

			CREATE TABLE #WalkTopology
			(
			    ParentObjectType nvarchar(32) NOT NULL,
			    ParentObjectId uniqueidentifier NOT NULL,
			    ObjectType nvarchar(32) NOT NULL,
			    ObjectId uniqueidentifier NOT NULL,
			    Depth int NOT NULL,
			    PathKey nvarchar(max) NOT NULL
			);

			CREATE TABLE #GraphEdges
			(
			    ParentObjectType nvarchar(32) NOT NULL,
			    ParentObjectId uniqueidentifier NOT NULL,
			    ChildObjectType nvarchar(32) NOT NULL,
			    ChildObjectId uniqueidentifier NOT NULL,
			    EdgeRank int NOT NULL,
			    EdgeSource nvarchar(64) NOT NULL
			);
			""";

		await using var command = new SqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task<SavedProfile> ExecuteAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		Guid memberId,
		PreparedSave save,
		string? requestTraceId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SET NOCOUNT ON;
			SET XACT_ABORT ON;

			DECLARE @MembershipCount int;
			DECLARE @ObjectMembershipId uniqueidentifier;
			DECLARE @StatusName nvarchar(64);
			DECLARE @StatusIsActive bit;
			DECLARE @MemberExists int = 0;
			DECLARE @MemberSystemId uniqueidentifier;

			SELECT
			    @MembershipCount = COUNT(*),
			    @ObjectMembershipId = MIN(om.ObjectMembershipId)
			FROM dbo.pb_object_memberships AS om WITH (UPDLOCK, HOLDLOCK)
			WHERE om.AccountId = @AccountId
			  AND om.ObjectType COLLATE Latin1_General_100_BIN2
			      = @AnchorObjectType COLLATE Latin1_General_100_BIN2
			  AND TRY_CONVERT(uniqueidentifier, om.ObjectId) = @AnchorObjectId;

			IF @MembershipCount = 0
			    THROW 51011, 'No access membership exists for this account on the selected anchor.', 1;

			IF @MembershipCount > 1
			    THROW 51012, 'More than one access membership matches this account and anchor.', 1;

			SELECT
			    @StatusName = s.StatusName,
			    @StatusIsActive = s.IsActive
			FROM dbo.pb_object_memberships AS om
			LEFT JOIN dbo.pb_system_membership_statuses AS s
			    ON s.MembershipStatusId = om.MembershipStatusId
			WHERE om.ObjectMembershipId = @ObjectMembershipId;

			IF @StatusName IS NULL
			   OR @StatusName <> N'Active'
			   OR @StatusIsActive IS NULL
			   OR @StatusIsActive <> 1
			    THROW 51013, 'The access membership for this account on the selected anchor is not active.', 1;

			IF NOT EXISTS
			(
			    SELECT 1
			    FROM dbo.pb_object_membership_roles AS mr WITH (UPDLOCK, HOLDLOCK)
			    INNER JOIN dbo.pb_roles AS r
			        ON r.RoleId = mr.RoleId
			    WHERE mr.ObjectMembershipId = @ObjectMembershipId
			      AND r.RoleName = N'Owner'
			      AND r.IsActive = 1
			)
			    THROW 51014, 'An active Owner role is required on the selected anchor.', 1;

			SELECT
			    @MemberExists = 1,
			    @MemberSystemId = m.SystemId
			FROM dbo.pb_members AS m WITH (UPDLOCK, HOLDLOCK)
			WHERE m.MemberId = @MemberId;

			IF @MemberExists = 0
			    THROW 51015, 'The member was not found.', 1;

			DECLARE @LockedGroupMembers int;
			DECLARE @LockedPlaceMembers int;

			SELECT @LockedGroupMembers = COUNT(*)
			FROM dbo.pb_group_members WITH (UPDLOCK, HOLDLOCK)
			WHERE MemberId = @MemberId;

			SELECT @LockedPlaceMembers = COUNT(*)
			FROM dbo.pb_place_members WITH (UPDLOCK, HOLDLOCK)
			WHERE MemberId = @MemberId;

			EXEC dbo.pb_show_tree_step5_topology_seed_builder
			    @AccountId = @AccountId;

			EXEC dbo.pb_show_tree_step5_topology_walker
			    @AccountId = @AccountId;

			EXEC dbo.pb_show_tree_step5_graph_expansion
			    @AccountId = @AccountId;

			IF NOT EXISTS
			(
			    SELECT 1
			    FROM #GraphEdges AS ge
			    WHERE ge.ParentObjectType COLLATE Latin1_General_100_BIN2
			          = @AnchorObjectType COLLATE Latin1_General_100_BIN2
			      AND ge.ParentObjectId = @AnchorObjectId
			      AND ge.ChildObjectType = N'member'
			      AND ge.ChildObjectId = @MemberId
			)
			    THROW 51016, 'The member is not a structural child of the selected anchor.', 1;

			UPDATE dbo.pb_members
			SET
			    DisplayName = CASE WHEN @SetDisplayName = 1 THEN @DisplayName ELSE DisplayName END,
			    Pronouns = CASE WHEN @SetPronouns = 1 THEN @Pronouns ELSE Pronouns END,
			    Description = CASE WHEN @SetDescription = 1 THEN @Description ELSE Description END,
			    Color = CASE WHEN @SetColor = 1 THEN @Color ELSE Color END,
			    UpdatedAtUtc = @UpdatedAtUtc
			WHERE MemberId = @MemberId;

			INSERT INTO dbo.pb_member_write_audit
			(
			    SystemId,
			    AccountId,
			    SystemMembershipId,
			    MemberId,
			    Operation,
			    RequestTraceId,
			    AnchorObjectType,
			    AnchorObjectId
			)
			VALUES
			(
			    @MemberSystemId,
			    @AccountId,
			    NULL,
			    @MemberId,
			    N'member.edit',
			    @RequestTraceId,
			    @AnchorObjectType,
			    CONVERT(nvarchar(36), @AnchorObjectId)
			);

			SELECT
			    MemberId,
			    DisplayName,
			    Pronouns,
			    Description,
			    Color,
			    UpdatedAtUtc
			FROM dbo.pb_members
			WHERE MemberId = @MemberId;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		command.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
		command.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = save.AnchorObjectType;
		command.Parameters.Add("@AnchorObjectId", SqlDbType.UniqueIdentifier).Value = save.AnchorObjectId;
		command.Parameters.Add("@SetDisplayName", SqlDbType.Bit).Value = save.SetDisplayName;
		command.Parameters.Add("@DisplayName", SqlDbType.NVarChar, DisplayNameMaxLength).Value =
			(object?)save.DisplayName ?? DBNull.Value;
		command.Parameters.Add("@SetPronouns", SqlDbType.Bit).Value = save.SetPronouns;
		command.Parameters.Add("@Pronouns", SqlDbType.NVarChar, PronounsMaxLength).Value =
			(object?)save.Pronouns ?? DBNull.Value;
		command.Parameters.Add("@SetDescription", SqlDbType.Bit).Value = save.SetDescription;
		command.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value =
			(object?)save.Description ?? DBNull.Value;
		command.Parameters.Add("@SetColor", SqlDbType.Bit).Value = save.SetColor;
		command.Parameters.Add("@Color", SqlDbType.NVarChar, ColorMaxLength).Value =
			(object?)save.Color ?? DBNull.Value;
		command.Parameters.Add("@UpdatedAtUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow;
		AddNullableString(command, "@RequestTraceId", requestTraceId, 100);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			throw new InvalidOperationException("The member profile save did not return the saved row.");
		}

		return new SavedProfile(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3),
			reader.IsDBNull(4) ? null : reader.GetString(4),
			reader.GetDateTime(5));
	}

	private static bool TryReadText(
		JsonElement value,
		int? maxLength,
		string label,
		out bool present,
		out string? text,
		out string? error)
	{
		present = false;
		text = null;
		error = null;

		if (value.ValueKind is JsonValueKind.Undefined)
		{
			return true;
		}

		present = true;

		if (value.ValueKind is JsonValueKind.Null)
		{
			return true;
		}

		if (value.ValueKind is not JsonValueKind.String)
		{
			error = label + " must be text.";
			return false;
		}

		var trimmed = value.GetString()?.Trim();
		if (string.IsNullOrEmpty(trimmed))
		{
			text = null;
			return true;
		}

		if (maxLength.HasValue && trimmed.Length > maxLength.Value)
		{
			error = label + " must be " + maxLength.Value + " characters or fewer.";
			return false;
		}

		text = trimmed;
		return true;
	}

	private static void AddNullableString(SqlCommand command, string name, string? value, int size)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
		parameter.Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
	}
}

public sealed class AnchorMemberProfileSaveRequest
{
	public string? AnchorObjectType { get; init; }

	public string? AnchorObjectId { get; init; }

	public JsonElement DisplayName { get; init; }

	public JsonElement Pronouns { get; init; }

	public JsonElement Description { get; init; }

	public JsonElement Color { get; init; }
}
