// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Validates and writes member profile fields, the selected placement, one avatar, and immediate Fronting.
/// </summary>
/// <remarks>
/// Profile, placement, and avatar share the caller's transaction and one authorization. Fronting uses its own transaction and the same branch gate.
/// Display inheritance is calculated here and does not grant write authority.
/// </remarks>
internal static class AnchorMemberProfileSave
{
	internal const int DisplayNameMaxLength = 510;
	internal const int PronounsMaxLength = 510;
	internal const int ColorMaxLength = 64;

	internal const int NoMembership = 51011;
	internal const int MultipleMemberships = 51012;
	internal const int InactiveMembership = 51013;
	internal const int OwnerRequired = 51014;
	/// <summary>
	/// The selected branch was missing, unreadable, or not a parent chain in the locked source rows. This is not the same failure as 51014.
	/// </summary>
	internal const int InvalidBranch = 51021;
	internal const int MemberMissing = 51015;
	internal const int NotStructuralChild = 51016;
	internal const int PlacementMissing = 51017;
	internal const int AvatarStorageMissing = 51018;
	internal const int FrontingStorageMissing = 51019;
	internal const int DuplicateOpenFront = 51020;
	internal const int AvatarEdgeMax = 2048;
	internal const int AvatarBytesMax = 2 * 1024 * 1024;

	/// <summary>
	/// The member row returned after the profile update. Fields that were not submitted keep their stored values.
	/// </summary>
	internal sealed record SavedProfile(
		Guid MemberId,
		string DisplayName,
		string? Pronouns,
		string? Description,
		string? Color,
		DateTime UpdatedAtUtc);

	/// <summary>
	/// Profile text accepted for one save. A false set flag means that field was omitted and must not be written.
	/// </summary>
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

	/// <summary>
	/// Checks the selected anchor and the profile text fields before any database call.
	/// </summary>
	/// <param name="request">The save body. Null fails. Omitted JSON fields are not changes. A submitted blank name fails.</param>
	/// <param name="prepared">The anchor and the fields that were present when the method returns true. Null when it returns false.</param>
	/// <param name="error">The validation message when the method returns false. Null when it returns true.</param>
	/// <returns>True when the anchor type and id are usable and at least one profile field was submitted.</returns>
	internal static bool TryPrepare(
		AnchorMemberProfileSaveRequest? request,
		out PreparedSave? prepared,
		out string? error)
	{
		prepared = null;
		error = null;

		// Anchor identity is required even when the text fields are otherwise valid.
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

	/// <summary>
	/// Creates the temporary graph tables on this connection before a transaction starts.
	/// </summary>
	/// <param name="connection">Open connection. The tables are session-scoped and are not created inside the later write transaction.</param>
	/// <param name="cancellationToken">Cancels the table creation.</param>
	/// <returns>The task completes when #TopologyEdges, #SystemEdgeLabels, #WalkTopology, and #GraphEdges exist on <paramref name="connection"/>.</returns>
	/// <remarks>Callers that write must create these tables before BeginTransaction. A second graph is not built for authorization.</remarks>
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

	/// <summary>
	/// Authorizes the selected branch, then updates the submitted profile fields and writes the audit row.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/> and the temporary graph tables.</param>
	/// <param name="transaction">The profile transaction. This method does not commit it.</param>
	/// <param name="accountId">Authenticated account whose access membership and Owner grant are required.</param>
	/// <param name="memberId">Member to update. A missing member raises 51015.</param>
	/// <param name="save">Anchor identity and the profile fields whose set flags are true.</param>
	/// <param name="branch">Selected occurrence chain. Null or an unreadable chain raises 51021.</param>
	/// <param name="requestTraceId">Audit trace. Null or whitespace is stored as null. The caller limits the length.</param>
	/// <param name="cancellationToken">Cancels either command. Cancellation does not commit.</param>
	/// <returns>The stored profile row when the task completes.</returns>
	/// <remarks>Placement and avatar commands later in this same transaction do not authorize again.</remarks>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">Membership, member, branch, or structural-child failures raised by the first command or by the write gate.</exception>
	/// <exception cref="InvalidOperationException">The update did not return the member row.</exception>
	internal static async Task<SavedProfile> ExecuteAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		Guid memberId,
		PreparedSave save,
		string? branch,
		string? requestTraceId,
		CancellationToken cancellationToken)
	{
		// First command in the caller's transaction: lock the selected
		// anchor's access membership and this member's structural rows,
		// build the account graph once, and require the member to be a
		// child of the selected anchor. No profile row is written here.
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

			SELECT
			    @MemberExists = 1,
			    @MemberSystemId = m.SystemId
			FROM dbo.pb_members AS m WITH (UPDLOCK, HOLDLOCK)
			WHERE m.MemberId = @MemberId;

			IF @MemberExists = 0
			    THROW 51015, 'The member was not found.', 1;

			-- The counts are unused. The locked reads hold this member's group
			-- and place membership rows until the transaction ends, so the
			-- graph snapshot cannot outlive those source rows.
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
			""";

		await using (var prepare = new SqlCommand(sql, connection, transaction))
		{
			prepare.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
			prepare.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
			prepare.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = save.AnchorObjectType;
			prepare.Parameters.Add("@AnchorObjectId", SqlDbType.UniqueIdentifier).Value = save.AnchorObjectId;
			await prepare.ExecuteNonQueryAsync(cancellationToken);
		}

		// Branch links are locked here, before the profile update. Placement
		// and avatar commands later in this same transaction do not authorize
		// again.
		await AnchorWriteAuthorization.RequireAsync(
			connection,
			transaction,
			accountId,
			save.AnchorObjectType,
			save.AnchorObjectId,
			branch,
			cancellationToken);

		// Update only the submitted fields, then return the stored row for the response.
		const string writeSql = """
			SET NOCOUNT ON;
			SET XACT_ABORT ON;

			DECLARE @MemberSystemId uniqueidentifier;
			SELECT @MemberSystemId = m.SystemId
			FROM dbo.pb_members AS m WITH (UPDLOCK, HOLDLOCK)
			WHERE m.MemberId = @MemberId;

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

		await using var command = new SqlCommand(writeSql, connection, transaction);
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

	/// <summary>
	/// Reads one optional JSON text field. Undefined means the field was omitted.
	/// </summary>
	/// <param name="value">The JSON value. Undefined is absent. Null and blank text are present with a null string. Any other non-string fails.</param>
	/// <param name="maxLength">Maximum trimmed length, or null when the field has no maximum.</param>
	/// <param name="label">Field name used in the validation message.</param>
	/// <param name="present">True when the JSON value was not undefined.</param>
	/// <param name="text">Trimmed text, or null for null, blank, or an omitted field.</param>
	/// <param name="error">The validation message when the method returns false. Null otherwise.</param>
	/// <returns>True when the value is omitted, null, blank, or text within <paramref name="maxLength"/>.</returns>
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

	/// <summary>
	/// Adds an nvarchar parameter, storing whitespace as database null.
	/// </summary>
	/// <param name="command">Command that receives the parameter.</param>
	/// <param name="name">Parameter name, including the leading @.</param>
	/// <param name="value">Text to store. Null or whitespace becomes DBNull.</param>
	/// <param name="size">Maximum nvarchar length. Use -1 for nvarchar(max).</param>
	private static void AddNullableString(SqlCommand command, string name, string? value, int size)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
		parameter.Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
	}

	/// <summary>
	/// Placement changes to apply. A false set flag preserves the stored mode and value for that side.
	/// </summary>
	internal sealed record PreparedPlacement(
		bool SetVisibility,
		string? VisibilityMode,
		int? VisibilityTypeId,
		bool SetPermission,
		string? PermissionMode,
		string? PermissionCode);

	/// <summary>
	/// Placement values returned after the update. The set flags still say which side this request changed.
	/// </summary>
	internal sealed record StoredPlacement(
		bool SetVisibility,
		string? VisibilityMode,
		int? VisibilityTypeId,
		bool SetPermission,
		string? PermissionMode,
		string? PermissionCode);

	/// <summary>
	/// Checks visibility and permission independently. Omitting both is success with no placement to write.
	/// </summary>
	/// <param name="request">Save body. Undefined mode fields are omitted. A mode must be unset, inherit, or explicit.</param>
	/// <param name="prepared">The changes when at least one side was submitted. Null when both were omitted or validation fails.</param>
	/// <param name="error">The validation message when the method returns false. Null when it returns true, including when nothing was submitted.</param>
	/// <returns>True when both sides are omitted or each submitted side is a valid mode and value.</returns>
	/// <remarks>Omitting one side preserves that side. Inherit and Not set cannot carry a stored type or permission code.</remarks>
	internal static bool TryPreparePlacement(
		AnchorMemberProfileSaveRequest request,
		out PreparedPlacement? prepared,
		out string? error)
	{
		prepared = null;
		error = null;

		if (!TryReadMode(request.VisibilityMode, out var setVisibility, out var visibilityMode, out error))
		{
			return false;
		}

		if (!TryReadVisibilityType(request.VisibilityTypeId, setVisibility, visibilityMode, out var visibilityTypeId, out error))
		{
			return false;
		}

		if (!TryReadMode(request.PermissionMode, out var setPermission, out var permissionMode, out error))
		{
			return false;
		}

		if (!TryReadPermissionCode(request.PermissionCode, setPermission, permissionMode, out var permissionCode, out error))
		{
			return false;
		}

		// Visibility and permission are independent. Omitting both leaves
		// the placement row untouched; omitting one preserves the other.
		if (!setVisibility && !setPermission)
		{
			return true;
		}

		prepared = new PreparedPlacement(
			setVisibility,
			visibilityMode,
			visibilityTypeId,
			setPermission,
			permissionMode,
			permissionCode);
		return true;
	}

	/// <summary>
	/// Updates the member's visibility and permission row for the selected system, group, or place placement.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/>.</param>
	/// <param name="transaction">The profile transaction. This method does not authorize or commit.</param>
	/// <param name="memberId">Member whose placement on <paramref name="anchor"/> is updated.</param>
	/// <param name="anchor">Selected anchor. The member must already have that system, group, or place membership row.</param>
	/// <param name="placement">Sides to write. An omitted side keeps the stored value, or unset when the settings row is inserted.</param>
	/// <param name="cancellationToken">Cancels the command. Cancellation does not commit.</param>
	/// <returns>The stored placement when the task completes, retaining the request's set flags.</returns>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">51017 when the member has no placement row on the selected anchor.</exception>
	/// <exception cref="InvalidOperationException">The settings statement did not return a row.</exception>
	internal static async Task<StoredPlacement> ApplyPlacementAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid memberId,
		PreparedSave anchor,
		PreparedPlacement placement,
		CancellationToken cancellationToken)
	{
		// The structural membership identifies the settings row. It is not an access membership.
		const string sql = """
			SET NOCOUNT ON;
			SET XACT_ABORT ON;

			DECLARE @TargetMemberId uniqueidentifier;
			DECLARE @TargetGroupMemberId uniqueidentifier;
			DECLARE @TargetPlaceMemberId uniqueidentifier;

			IF @AnchorObjectType = N'system'
			BEGIN
			    SELECT @TargetMemberId = m.MemberId
			    FROM dbo.pb_members AS m
			    WHERE m.MemberId = @MemberId
			      AND m.SystemId = @AnchorObjectId;
			END
			ELSE IF @AnchorObjectType = N'group'
			BEGIN
			    SELECT @TargetGroupMemberId = gm.GroupMemberId
			    FROM dbo.pb_group_members AS gm
			    WHERE gm.GroupId = @AnchorObjectId
			      AND gm.MemberId = @MemberId;
			END
			ELSE IF @AnchorObjectType = N'place'
			BEGIN
			    SELECT @TargetPlaceMemberId = pm.PlaceMemberId
			    FROM dbo.pb_place_members AS pm
			    WHERE pm.PlaceId = @AnchorObjectId
			      AND pm.MemberId = @MemberId;
			END

			IF @TargetMemberId IS NULL AND @TargetGroupMemberId IS NULL AND @TargetPlaceMemberId IS NULL
			    THROW 51017, 'The member has no placement row on the selected anchor.', 1;

			IF @TargetMemberId IS NOT NULL
			BEGIN
			    UPDATE dbo.pb_member_visibility_permissions
			    SET
			        VisibilityMode = CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE VisibilityMode END,
			        VisibilityTypeId = CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE VisibilityTypeId END,
			        PermissionMode = CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE PermissionMode END,
			        PermissionCode = CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE PermissionCode END,
			        UpdatedAtUtc = SYSUTCDATETIME()
			    WHERE MemberId = @TargetMemberId;

			    IF @@ROWCOUNT = 0
			        INSERT INTO dbo.pb_member_visibility_permissions
			            (MemberId, VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode)
			        VALUES
			            (@TargetMemberId,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE N'unset' END,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE NULL END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE N'unset' END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE NULL END);
			END
			ELSE IF @TargetGroupMemberId IS NOT NULL
			BEGIN
			    UPDATE dbo.pb_member_visibility_permissions
			    SET
			        VisibilityMode = CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE VisibilityMode END,
			        VisibilityTypeId = CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE VisibilityTypeId END,
			        PermissionMode = CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE PermissionMode END,
			        PermissionCode = CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE PermissionCode END,
			        UpdatedAtUtc = SYSUTCDATETIME()
			    WHERE GroupMemberId = @TargetGroupMemberId;

			    IF @@ROWCOUNT = 0
			        INSERT INTO dbo.pb_member_visibility_permissions
			            (GroupMemberId, VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode)
			        VALUES
			            (@TargetGroupMemberId,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE N'unset' END,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE NULL END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE N'unset' END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE NULL END);
			END
			ELSE
			BEGIN
			    UPDATE dbo.pb_member_visibility_permissions
			    SET
			        VisibilityMode = CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE VisibilityMode END,
			        VisibilityTypeId = CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE VisibilityTypeId END,
			        PermissionMode = CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE PermissionMode END,
			        PermissionCode = CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE PermissionCode END,
			        UpdatedAtUtc = SYSUTCDATETIME()
			    WHERE PlaceMemberId = @TargetPlaceMemberId;

			    IF @@ROWCOUNT = 0
			        INSERT INTO dbo.pb_member_visibility_permissions
			            (PlaceMemberId, VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode)
			        VALUES
			            (@TargetPlaceMemberId,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE N'unset' END,
			             CASE WHEN @SetVisibility = 1 THEN @VisibilityTypeId ELSE NULL END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE N'unset' END,
			             CASE WHEN @SetPermission = 1 THEN @PermissionCode ELSE NULL END);
			END

			SELECT
			    VisibilityMode,
			    VisibilityTypeId,
			    PermissionMode,
			    PermissionCode
			FROM dbo.pb_member_visibility_permissions
			WHERE (@TargetMemberId IS NOT NULL AND MemberId = @TargetMemberId)
			   OR (@TargetGroupMemberId IS NOT NULL AND GroupMemberId = @TargetGroupMemberId)
			   OR (@TargetPlaceMemberId IS NOT NULL AND PlaceMemberId = @TargetPlaceMemberId);
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
		command.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = anchor.AnchorObjectType;
		command.Parameters.Add("@AnchorObjectId", SqlDbType.UniqueIdentifier).Value = anchor.AnchorObjectId;
		command.Parameters.Add("@SetVisibility", SqlDbType.Bit).Value = placement.SetVisibility;
		command.Parameters.Add("@VisibilityMode", SqlDbType.NVarChar, 16).Value = (object?)placement.VisibilityMode ?? DBNull.Value;
		command.Parameters.Add("@VisibilityTypeId", SqlDbType.Int).Value = (object?)placement.VisibilityTypeId ?? DBNull.Value;
		command.Parameters.Add("@SetPermission", SqlDbType.Bit).Value = placement.SetPermission;
		command.Parameters.Add("@PermissionMode", SqlDbType.NVarChar, 16).Value = (object?)placement.PermissionMode ?? DBNull.Value;
		command.Parameters.Add("@PermissionCode", SqlDbType.NVarChar, 16).Value = (object?)placement.PermissionCode ?? DBNull.Value;

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			throw new InvalidOperationException("The placement settings save did not return the saved row.");
		}

		return new StoredPlacement(
			placement.SetVisibility,
			reader.GetString(0),
			reader.IsDBNull(1) ? null : reader.GetInt32(1),
			placement.SetPermission,
			reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3));
	}

	/// <summary>
	/// Reads a visibility or permission mode.
	/// </summary>
	/// <param name="value">JSON value. Undefined is omitted. Any other non-string, or a string other than unset, inherit, or explicit, fails.</param>
	/// <param name="present">True when the value was not undefined.</param>
	/// <param name="mode">The accepted mode, or null when the field was omitted.</param>
	/// <param name="error">The validation message when the method returns false. Null otherwise.</param>
	/// <returns>True when the field is omitted or is one of the three modes.</returns>
	private static bool TryReadMode(
		JsonElement value,
		out bool present,
		out string? mode,
		out string? error)
	{
		present = false;
		mode = null;
		error = null;
		if (value.ValueKind is JsonValueKind.Undefined)
		{
			return true;
		}

		present = true;
		if (value.ValueKind is not JsonValueKind.String)
		{
			error = "Visibility and Permissions modes must be unset, inherit, or explicit.";
			return false;
		}

		mode = value.GetString();
		if (mode is not ("unset" or "inherit" or "explicit"))
		{
			error = "Visibility and Permissions modes must be unset, inherit, or explicit.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Reads the visibility type id required by an explicit visibility mode.
	/// </summary>
	/// <param name="value">JSON value. Undefined or null is accepted for unset and inherit. Explicit requires an integer from 1 through 3.</param>
	/// <param name="setVisibility">False skips the value because visibility was omitted.</param>
	/// <param name="mode">The accepted visibility mode, or null when visibility was omitted.</param>
	/// <param name="typeId">1, 2, or 3 for explicit visibility. Null for unset, inherit, or an omitted field.</param>
	/// <param name="error">The validation message when the method returns false. Null otherwise.</param>
	/// <returns>True when no visibility type is allowed, or the explicit type is Only you, Trusted friends, or All friends.</returns>
	private static bool TryReadVisibilityType(
		JsonElement value,
		bool setVisibility,
		string? mode,
		out int? typeId,
		out string? error)
	{
		typeId = null;
		error = null;
		if (!setVisibility)
		{
			return true;
		}

		if (mode is "unset" or "inherit")
		{
			if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
			{
				return true;
			}

			error = "Inherit and Not set cannot store a visibility type.";
			return false;
		}

		if (value.ValueKind is not JsonValueKind.Number || !value.TryGetInt32(out var id) || id is < 1 or > 3)
		{
			error = "An explicit visibility must be Only you, Trusted friends, or All friends.";
			return false;
		}

		typeId = id;
		return true;
	}

	/// <summary>
	/// Reads the permission code required by an explicit permission mode.
	/// </summary>
	/// <param name="value">JSON value. Undefined or null is accepted for unset and inherit. Explicit requires viewer, editor, manager, or owner.</param>
	/// <param name="setPermission">False skips the value because permission was omitted.</param>
	/// <param name="mode">The accepted permission mode, or null when permission was omitted.</param>
	/// <param name="code">The explicit code, or null for unset, inherit, or an omitted field.</param>
	/// <param name="error">The validation message when the method returns false. Null otherwise.</param>
	/// <returns>True when no code is allowed, or the explicit code is one of the four stored codes.</returns>
	/// <remarks>This code is display and placement data. It does not create an Owner role.</remarks>
	private static bool TryReadPermissionCode(
		JsonElement value,
		bool setPermission,
		string? mode,
		out string? code,
		out string? error)
	{
		code = null;
		error = null;
		if (!setPermission)
		{
			return true;
		}

		if (mode is "unset" or "inherit")
		{
			if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
			{
				return true;
			}

			error = "Inherit and Not set cannot store a permission.";
			return false;
		}

		if (value.ValueKind is not JsonValueKind.String)
		{
			error = "An explicit permission must be viewer, editor, manager, or owner.";
			return false;
		}

		code = value.GetString();
		if (code is not ("viewer" or "editor" or "manager" or "owner"))
		{
			error = "An explicit permission must be viewer, editor, manager, or owner.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// One member's stored placement settings, used as the start of display inheritance.
	/// </summary>
	internal sealed record MemberPlacementRow(
		Guid MemberId,
		string? VisibilityMode,
		int? VisibilityTypeId,
		string? PermissionMode,
		string? PermissionCode);

	/// <summary>
	/// Avatar bytes accepted for storage. Base64 has been decoded, the length is within 2 MiB, and a PNG, JPEG, or WebP signature supplied the header dimensions. The complete image has not been decoded.
	/// </summary>
	internal sealed record PreparedAvatar(string ContentType, byte[] Bytes, int Width, int Height);

	/// <summary>
	/// Accepts an optional avatar from the save body without writing it.
	/// </summary>
	/// <param name="request">Save body. Null, or both avatar fields undefined, means no avatar change. One field without the other fails.</param>
	/// <param name="avatar">The accepted bytes, declared type, and header dimensions when validation succeeds. Null when no avatar was submitted or validation fails.</param>
	/// <param name="error">The validation message when the method returns false. Null when it returns true.</param>
	/// <returns>True when no avatar was submitted, or the base64 decodes to a non-empty payload of at most 2 MiB whose declared-format signature yields header dimensions of at most 2048 pixels on an edge. The complete image is not decoded.</returns>
	internal static bool TryPrepareAvatar(
		AnchorMemberProfileSaveRequest? request,
		out PreparedAvatar? avatar,
		out string? error)
	{
		avatar = null;
		error = null;
		if (request is null
			|| request.AvatarContentType.ValueKind is JsonValueKind.Undefined
			&& request.AvatarImageBase64.ValueKind is JsonValueKind.Undefined)
		{
			return true;
		}

		if (request.AvatarContentType.ValueKind is not JsonValueKind.String
			|| request.AvatarImageBase64.ValueKind is not JsonValueKind.String)
		{
			error = "An avatar must include its image type and bytes.";
			return false;
		}

		var contentType = request.AvatarContentType.GetString()?.Trim().ToLowerInvariant();
		if (contentType is not ("image/png" or "image/jpeg" or "image/webp"))
		{
			error = "An avatar must be a PNG, JPEG, or WebP image.";
			return false;
		}

		var encoded = request.AvatarImageBase64.GetString() ?? "";
		var marker = encoded.IndexOf(',');
		if (encoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && marker >= 0)
		{
			encoded = encoded[(marker + 1)..];
		}

		// A data URL prefix is ignored. The declared type must still match the decoded bytes.
		byte[] bytes;
		try
		{
			bytes = Convert.FromBase64String(encoded);
		}
		catch (FormatException)
		{
			error = "The avatar image could not be read.";
			return false;
		}

		if (bytes.Length == 0 || bytes.Length > AvatarBytesMax)
		{
			error = "An avatar must be larger than empty and no more than 2 MiB.";
			return false;
		}

		if (!TryReadImage(bytes, contentType, out var width, out var height))
		{
			error = "The avatar image does not match PNG, JPEG, or WebP, or its dimensions could not be read.";
			return false;
		}

		if (width < 1 || height < 1 || width > AvatarEdgeMax || height > AvatarEdgeMax)
		{
			error = "An avatar must be at most 2048 pixels on its longest edge.";
			return false;
		}

		avatar = new PreparedAvatar(contentType, bytes, width, height);
		return true;
	}

	/// <summary>
	/// Inserts or replaces the one avatar stored for the member.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/>.</param>
	/// <param name="transaction">The profile transaction. This method does not authorize or commit.</param>
	/// <param name="memberId">Member whose single avatar row is replaced.</param>
	/// <param name="avatar">Bytes already checked for length and header dimensions. This method stores them and does not decode the image.</param>
	/// <param name="cancellationToken">Cancels the command. Cancellation does not commit.</param>
	/// <returns>The task completes when the avatar row has been updated or inserted.</returns>
	/// <remarks>The profile read returns the timestamp only. These bytes stay on the image GET.</remarks>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">51018 when pb_member_avatars is not installed.</exception>
	internal static async Task ApplyAvatarAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid memberId,
		PreparedAvatar avatar,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SET NOCOUNT ON;
			IF OBJECT_ID(N'dbo.pb_member_avatars', N'U') IS NULL
			    THROW 51018, 'Avatar storage is not installed.', 1;

			UPDATE dbo.pb_member_avatars
			SET
			    ContentType = @ContentType,
			    Width = @Width,
			    Height = @Height,
			    ByteSize = @ByteSize,
			    ImageBytes = @ImageBytes,
			    UpdatedAtUtc = SYSUTCDATETIME()
			WHERE MemberId = @MemberId;

			IF @@ROWCOUNT = 0
			BEGIN
			    INSERT INTO dbo.pb_member_avatars
			    (
			        MemberId,
			        ContentType,
			        Width,
			        Height,
			        ByteSize,
			        ImageBytes
			    )
			    VALUES
			    (
			        @MemberId,
			        @ContentType,
			        @Width,
			        @Height,
			        @ByteSize,
			        @ImageBytes
			    );
			END;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
		command.Parameters.Add("@ContentType", SqlDbType.NVarChar, 32).Value = avatar.ContentType;
		command.Parameters.Add("@Width", SqlDbType.Int).Value = avatar.Width;
		command.Parameters.Add("@Height", SqlDbType.Int).Value = avatar.Height;
		command.Parameters.Add("@ByteSize", SqlDbType.Int).Value = avatar.Bytes.Length;
		command.Parameters.Add("@ImageBytes", SqlDbType.VarBinary, -1).Value = avatar.Bytes;
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Opens or closes the member's one live fronting interval after the same branch gate used by profile Save.
	/// </summary>
	/// <param name="connection">Open connection that owns <paramref name="transaction"/> and the temporary graph tables.</param>
	/// <param name="transaction">The fronting transaction. This method does not commit it. It is separate from profile Save.</param>
	/// <param name="accountId">Authenticated account whose access membership and Owner grant are required.</param>
	/// <param name="memberId">Member whose open interval is changed. SystemId may be null on the inserted row.</param>
	/// <param name="anchorObjectType">Selected anchor type: system, group, or place.</param>
	/// <param name="anchorObjectId">Selected anchor id.</param>
	/// <param name="fronting">True opens an interval when none is open. False closes the one open interval. The same state writes no interval.</param>
	/// <param name="branch">Selected occurrence chain. Null or an unreadable chain raises 51021.</param>
	/// <param name="requestTraceId">Audit trace. Null or whitespace is stored as null.</param>
	/// <param name="cancellationToken">Cancels either command. Cancellation does not commit.</param>
	/// <returns>True when an open live interval exists after the write. False when none remains.</returns>
	/// <exception cref="Microsoft.Data.SqlClient.SqlException">Membership, member, branch, storage, or duplicate-open-interval failures.</exception>
	internal static async Task<bool> ApplyFrontingAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		Guid memberId,
		string anchorObjectType,
		Guid anchorObjectId,
		bool fronting,
		string? branch,
		string? requestTraceId,
		CancellationToken cancellationToken)
	{
		// Lock the selected access membership and require this member under that anchor before an interval changes.
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

			IF @StatusName IS NULL OR @StatusName <> N'Active' OR @StatusIsActive IS NULL OR @StatusIsActive <> 1
			    THROW 51013, 'The access membership for this account on the selected anchor is not active.', 1;

			SELECT @MemberExists = 1, @MemberSystemId = m.SystemId
			FROM dbo.pb_members AS m WITH (UPDLOCK, HOLDLOCK)
			WHERE m.MemberId = @MemberId;

			IF @MemberExists = 0
			    THROW 51015, 'The member was not found.', 1;

			IF OBJECT_ID(N'dbo.pb_front_history', N'U') IS NULL
			    THROW 51019, 'Fronting storage is not installed.', 1;

			EXEC dbo.pb_show_tree_step5_topology_seed_builder @AccountId = @AccountId;
			EXEC dbo.pb_show_tree_step5_topology_walker @AccountId = @AccountId;
			EXEC dbo.pb_show_tree_step5_graph_expansion @AccountId = @AccountId;

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
			""";

		await using (var prepare = new SqlCommand(sql, connection, transaction))
		{
			prepare.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
			prepare.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
			prepare.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = anchorObjectType;
			prepare.Parameters.Add("@AnchorObjectId", SqlDbType.UniqueIdentifier).Value = anchorObjectId;
			await prepare.ExecuteNonQueryAsync(cancellationToken);
		}

		await AnchorWriteAuthorization.RequireAsync(
			connection,
			transaction,
			accountId,
			anchorObjectType,
			anchorObjectId,
			branch,
			cancellationToken);

		// An unchanged fronting state still audits the attempt, but it does not insert or close an interval.
		const string writeSql = """
			SET NOCOUNT ON;
			SET XACT_ABORT ON;

			DECLARE @MemberSystemId uniqueidentifier;
			SELECT @MemberSystemId = m.SystemId
			FROM dbo.pb_members AS m WITH (UPDLOCK, HOLDLOCK)
			WHERE m.MemberId = @MemberId;

			DECLARE @OpenCount int;
			SELECT @OpenCount = COUNT(*)
			FROM dbo.pb_front_history WITH (UPDLOCK, HOLDLOCK)
			WHERE MemberId = @MemberId
			  AND EndTimeMs IS NULL
			  AND IsLive = 1;

			IF @OpenCount > 1
			    THROW 51020, 'This member has more than one open fronting interval.', 1;

			IF @Fronting = 1 AND @OpenCount = 0
			BEGIN
			    INSERT INTO dbo.pb_front_history
			    (
			        SystemId,
			        MemberId,
			        StartTimeMs,
			        EndTimeMs,
			        IsLive,
			        IsCustom,
			        ImportedAtUtc,
			        CreatedAtUtc
			    )
			    VALUES
			    (
			        @MemberSystemId,
			        @MemberId,
			        DATEDIFF_BIG(MILLISECOND, '1970-01-01T00:00:00', SYSUTCDATETIME()),
			        NULL,
			        1,
			        0,
			        SYSUTCDATETIME(),
			        SYSUTCDATETIME()
			    );
			END;

			IF @Fronting = 0 AND @OpenCount = 1
			BEGIN
			    UPDATE dbo.pb_front_history
			    SET
			        EndTimeMs = DATEDIFF_BIG(MILLISECOND, '1970-01-01T00:00:00', SYSUTCDATETIME()),
			        IsLive = 0,
			        UpdatedAtUtc = SYSUTCDATETIME()
			    WHERE MemberId = @MemberId
			      AND EndTimeMs IS NULL
			      AND IsLive = 1;
			END;

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

			SELECT CASE
			    WHEN EXISTS
			    (
			        SELECT 1
			        FROM dbo.pb_front_history
			        WHERE MemberId = @MemberId
			          AND EndTimeMs IS NULL
			          AND IsLive = 1
			    ) THEN CAST(1 AS bit)
			    ELSE CAST(0 AS bit)
			END;
			""";

		await using var command = new SqlCommand(writeSql, connection, transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		command.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
		command.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = anchorObjectType;
		command.Parameters.Add("@AnchorObjectId", SqlDbType.UniqueIdentifier).Value = anchorObjectId;
		command.Parameters.Add("@Fronting", SqlDbType.Bit).Value = fronting;
		AddNullableString(command, "@RequestTraceId", requestTraceId, 100);
		var result = await command.ExecuteScalarAsync(cancellationToken);
		return result is bool value ? value : result is int number ? number != 0 : result is byte flag && flag != 0;
	}

	/// <summary>
	/// One node of the selected branch, ordered from the member's containing anchor toward the account.
	/// </summary>
	internal sealed record BranchNode(string ObjectType, Guid ObjectId);

	/// <summary>
	/// Visibility and permission settings for one branch anchor. A missing mode is treated as unset by the resolver.
	/// </summary>
	internal sealed record AnchorSetting(
		string ObjectType,
		Guid ObjectId,
		string VisibilityMode,
		int? VisibilityTypeId,
		string PermissionMode,
		string? PermissionCode);

	/// <summary>
	/// Display values after inheritance. BranchValid false leaves an inheriting member on inherit rather than inventing a parent.
	/// </summary>
	internal sealed record ResolvedSetting(
		bool BranchValid,
		string VisibilityMode,
		int? VisibilityTypeId,
		string PermissionMode,
		string? PermissionCode);

	/// <summary>
	/// Converts the browser branch string into the JSON array the profile procedure validates.
	/// </summary>
	/// <param name="branch">Comma-separated type:id pairs. An empty string is invalid and returns an empty array.</param>
	/// <returns>A JSON array of object type and id, or "[]" when a segment is malformed, the type is not system, group, or place, the id is missing, invalid, or empty, or the branch has more than 32 nodes. Duplicate nodes and parent links are not checked.</returns>
	/// <remarks>One bad segment returns "[]" so the procedure rejects the whole branch. The chain is not shortened to the readable prefix.</remarks>
	internal static string BranchJson(string branch)
	{
		var parts = branch.Split(',');
		if (parts.Length == 0 || parts.Length > 32)
		{
			return "[]";
		}

		var json = new System.Text.StringBuilder();
		json.Append('[');
		for (var index = 0; index < parts.Length; index++)
		{
			var split = parts[index].Trim().Split(':', 2, StringSplitOptions.TrimEntries);
			if (split.Length != 2
				|| split[0] is not ("system" or "group" or "place")
				|| !Guid.TryParse(split[1], out var objectId)
				|| objectId == Guid.Empty)
			{
				return "[]";
			}

			if (index > 0)
			{
				json.Append(',');
			}

			json.Append("{\"objectType\":\"");
			json.Append(split[0]);
			json.Append("\",\"objectId\":\"");
			json.Append(objectId.ToString("D"));
			json.Append("\"}");
		}

		json.Append(']');
		return json.ToString();
	}

	/// <summary>
	/// Resolves the member's visibility and permission for display along the validated selected branch.
	/// </summary>
	/// <param name="placement">The member's own settings. Null or a blank mode starts as unset. Explicit keeps the member's type or code.</param>
	/// <param name="branch">Containing anchor, then its parent anchors. An empty list cannot resolve inherit.</param>
	/// <param name="anchors">Settings for those anchors. A node with no row is unset and stops inheritance.</param>
	/// <param name="branchValid">False keeps an inheriting value as inherit. It does not walk an invalid branch.</param>
	/// <returns>The display modes and values. This result does not grant write authority.</returns>
	/// <remarks>Unset does not inherit. Explicit stops. A root that is still Inherit stays Inherit. Visibility and permission are resolved separately.</remarks>
	internal static ResolvedSetting ResolvePlacement(
		MemberPlacementRow? placement,
		IReadOnlyList<BranchNode> branch,
		IReadOnlyList<AnchorSetting> anchors,
		bool branchValid)
	{
		var visibilityMode = string.IsNullOrWhiteSpace(placement?.VisibilityMode) ? "unset" : placement!.VisibilityMode!;
		int? visibilityTypeId = visibilityMode == "explicit" ? placement?.VisibilityTypeId : null;
		var permissionMode = string.IsNullOrWhiteSpace(placement?.PermissionMode) ? "unset" : placement!.PermissionMode!;
		string? permissionCode = permissionMode == "explicit" ? placement?.PermissionCode : null;

		// Each side stops at its own first explicit or unset anchor.
		if (visibilityMode == "inherit")
		{
			(visibilityMode, visibilityTypeId) = ResolveInheritedVisibility(branch, anchors, branchValid);
		}

		if (permissionMode == "inherit")
		{
			(permissionMode, permissionCode) = ResolveInheritedPermission(branch, anchors, branchValid);
		}

		return new ResolvedSetting(branchValid, visibilityMode, visibilityTypeId, permissionMode, permissionCode);
	}

	/// <summary>
	/// Walks visibility from the member's containing anchor toward the account.
	/// </summary>
	/// <param name="branch">Selected branch. An invalid branch is not walked.</param>
	/// <param name="anchors">Anchor settings. A missing row is unset.</param>
	/// <param name="branchValid">False returns inherit without a type id.</param>
	/// <returns>The first non-inherit mode and its type id, or inherit when every walked node inherits.</returns>
	private static (string Mode, int? TypeId) ResolveInheritedVisibility(
		IReadOnlyList<BranchNode> branch,
		IReadOnlyList<AnchorSetting> anchors,
		bool branchValid)
	{
		if (!branchValid)
		{
			return ("inherit", null);
		}

		foreach (var node in branch)
		{
			var match = FindAnchor(anchors, node);
			var mode = match?.VisibilityMode ?? "unset";
			if (mode != "inherit")
			{
				return mode == "explicit" ? ("explicit", match?.VisibilityTypeId) : ("unset", null);
			}
		}

		return ("inherit", null);
	}

	/// <summary>
	/// Walks permission from the member's containing anchor toward the account.
	/// </summary>
	/// <param name="branch">Selected branch. An invalid branch is not walked.</param>
	/// <param name="anchors">Anchor settings. A missing row is unset.</param>
	/// <param name="branchValid">False returns inherit without a permission code.</param>
	/// <returns>The first non-inherit mode and its code, or inherit when every walked node inherits.</returns>
	private static (string Mode, string? Code) ResolveInheritedPermission(
		IReadOnlyList<BranchNode> branch,
		IReadOnlyList<AnchorSetting> anchors,
		bool branchValid)
	{
		if (!branchValid)
		{
			return ("inherit", null);
		}

		foreach (var node in branch)
		{
			var match = FindAnchor(anchors, node);
			var mode = match?.PermissionMode ?? "unset";
			if (mode != "inherit")
			{
				return mode == "explicit" ? ("explicit", match?.PermissionCode) : ("unset", null);
			}
		}

		return ("inherit", null);
	}

	/// <summary>
	/// Finds the settings row for one branch node.
	/// </summary>
	/// <param name="anchors">Rows returned for the validated branch. Order is not significant.</param>
	/// <param name="node">Type and id to match. A different type with the same id does not match.</param>
	/// <returns>The matching settings, or null when this node has no row.</returns>
	private static AnchorSetting? FindAnchor(IReadOnlyList<AnchorSetting> anchors, BranchNode node)
	{
		foreach (var setting in anchors)
		{
			if (setting.ObjectType == node.ObjectType && setting.ObjectId == node.ObjectId)
			{
				return setting;
			}
		}

		return null;
	}

	/// <summary>
	/// Returns the member's avatar bytes when the account graph contains that member.
	/// </summary>
	/// <param name="connection">Open connection. This method creates the temporary graph tables and builds the graph on it.</param>
	/// <param name="accountId">Account whose graph is built. Owner is not required to read an avatar.</param>
	/// <param name="memberId">Member whose image is requested.</param>
	/// <param name="timing">Stage timings for this read. A failed stage is recorded here before its exception continues to the caller.</param>
	/// <param name="cancellationToken">Cancels graph creation or the image read.</param>
	/// <returns>The bytes and content type when the task completes and the member appears in the graph. Null when no avatar row matches.</returns>
	/// <remarks>This read rebuilds the graph. It does not use the profile transaction or the selected branch. The timings do not change the SQL or the returned bytes.</remarks>
	/// <exception cref="ArgumentNullException"><paramref name="timing"/> is null.</exception>
	internal static async Task<(byte[] Bytes, string ContentType)?> ReadAvatarAsync(
		SqlConnection connection,
		Guid accountId,
		Guid memberId,
		AvatarReadTiming timing,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timing);

		// The image is visible when the member is anywhere in this account's graph.
		await MeasureAvatarStageAsync(
			timing,
			AvatarReadTiming.GraphTablesStage,
			() => CreateGraphTablesAsync(connection, cancellationToken));

		await MeasureAvatarStageAsync(timing, AvatarReadTiming.TopologySeedStage, async () =>
		{
			await using var seed = new SqlCommand("EXEC dbo.pb_show_tree_step5_topology_seed_builder @AccountId = @AccountId;", connection);
			seed.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
			await seed.ExecuteNonQueryAsync(cancellationToken);
		});

		await MeasureAvatarStageAsync(timing, AvatarReadTiming.TopologyWalkerStage, async () =>
		{
			await using var walker = new SqlCommand("EXEC dbo.pb_show_tree_step5_topology_walker @AccountId = @AccountId;", connection);
			walker.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
			await walker.ExecuteNonQueryAsync(cancellationToken);
		});

		await MeasureAvatarStageAsync(timing, AvatarReadTiming.GraphExpansionStage, async () =>
		{
			await using var expansion = new SqlCommand("EXEC dbo.pb_show_tree_step5_graph_expansion @AccountId = @AccountId;", connection);
			expansion.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
			await expansion.ExecuteNonQueryAsync(cancellationToken);
		});

		(byte[] Bytes, string ContentType)? image = null;
		var avatarQuery = Stopwatch.StartNew();
		try
		{
			await using var command = new SqlCommand(
				"""
				SELECT a.ImageBytes, a.ContentType
				FROM dbo.pb_member_avatars AS a
				WHERE a.MemberId = @MemberId
				  AND EXISTS
				  (
				      SELECT 1
				      FROM #GraphEdges AS ge
				      WHERE ge.ChildObjectType = N'member'
				        AND ge.ChildObjectId = @MemberId
				  );
				""",
				connection);
			command.Parameters.Add("@MemberId", SqlDbType.UniqueIdentifier).Value = memberId;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (await reader.ReadAsync(cancellationToken))
			{
				var bytes = (byte[])reader.GetValue(0);
				var contentType = reader.GetString(1);
				timing.ByteLength = bytes.Length;
				image = (bytes, contentType);
			}
		}
		catch
		{
			avatarQuery.Stop();
			timing.FailStage(AvatarReadTiming.AvatarQueryStage, avatarQuery.ElapsedMilliseconds);
			throw;
		}

		avatarQuery.Stop();
		timing.CompleteStage(AvatarReadTiming.AvatarQueryStage, avatarQuery.ElapsedMilliseconds);
		return image;
	}

	/// <summary>
	/// Runs one avatar-read stage and stores its elapsed milliseconds.
	/// </summary>
	/// <param name="timing">Stage timings for this avatar read. A failure records the stage name before the exception continues.</param>
	/// <param name="stage">Stage name stored when this attempt finishes or fails.</param>
	/// <param name="action">The stage work. Cancellation and SQL behavior stay in the action.</param>
	/// <returns>The task completes when the stage finishes.</returns>
	/// <remarks>The stopwatch includes the awaited work and command disposal. It does not include later stages.</remarks>
	/// <exception cref="Exception">An exception from <paramref name="action"/> is stored on <paramref name="timing"/> and thrown again.</exception>
	private static async Task MeasureAvatarStageAsync(
		AvatarReadTiming timing,
		string stage,
		Func<Task> action)
	{
		var watch = Stopwatch.StartNew();
		try
		{
			await action();
			watch.Stop();
			timing.CompleteStage(stage, watch.ElapsedMilliseconds);
		}
		catch
		{
			watch.Stop();
			timing.FailStage(stage, watch.ElapsedMilliseconds);
			throw;
		}
	}

	/// <summary>
	/// Reads header width and height for the declared image format.
	/// </summary>
	/// <param name="bytes">Image bytes. The selected reader checks a format signature and header dimensions, not the complete image.</param>
	/// <param name="contentType">image/png and image/jpeg select those readers. Every other value, including image/webp and an unsupported type, is passed to the WebP reader. That dispatch does not itself fail. TryPrepareAvatar allows only PNG, JPEG, and WebP before calling this method.</param>
	/// <param name="width">Header width when the method returns true. Zero when it returns false.</param>
	/// <param name="height">Header height when the method returns true. Zero when it returns false.</param>
	/// <returns>True when the selected reader accepts the signature and reads positive header dimensions.</returns>
	private static bool TryReadImage(byte[] bytes, string contentType, out int width, out int height)
	{
		width = 0;
		height = 0;
		if (contentType == "image/png")
		{
			return TryReadPng(bytes, out width, out height);
		}

		if (contentType == "image/jpeg")
		{
			return TryReadJpeg(bytes, out width, out height);
		}

		return TryReadWebp(bytes, out width, out height);
	}

	/// <summary>
	/// Reads the IHDR dimensions of a PNG.
	/// </summary>
	/// <param name="bytes">PNG bytes. A missing signature or IHDR fails.</param>
	/// <param name="width">IHDR width when the method returns true. Zero when it returns false.</param>
	/// <param name="height">IHDR height when the method returns true. Zero when it returns false.</param>
	/// <returns>True when the signature, IHDR, and positive dimensions are present.</returns>
	private static bool TryReadPng(byte[] bytes, out int width, out int height)
	{
		width = 0;
		height = 0;
		ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
		if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(signature))
		{
			return false;
		}

		if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
		{
			return false;
		}

		width = ReadInt32Big(bytes, 16);
		height = ReadInt32Big(bytes, 20);
		return width > 0 && height > 0;
	}

	/// <summary>
	/// Reads dimensions from the first JPEG start-of-frame marker.
	/// </summary>
	/// <param name="bytes">JPEG bytes. A missing SOI or a truncated segment fails. The scan itself is not accepted as dimensions.</param>
	/// <param name="width">Frame width when the method returns true. Zero when it returns false.</param>
	/// <param name="height">Frame height when the method returns true. Zero when it returns false.</param>
	/// <returns>True when a supported start-of-frame marker contains positive dimensions.</returns>
	private static bool TryReadJpeg(byte[] bytes, out int width, out int height)
	{
		width = 0;
		height = 0;
		if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
		{
			return false;
		}

		var index = 2;
		while (index + 8 < bytes.Length)
		{
			if (bytes[index] != 0xFF)
			{
				return false;
			}

			while (index < bytes.Length && bytes[index] == 0xFF)
			{
				index++;
			}

			if (index >= bytes.Length)
			{
				return false;
			}

			var marker = bytes[index];
			index++;
			if (marker == 0xD9 || marker == 0xDA)
			{
				return false;
			}

			if (index + 1 >= bytes.Length)
			{
				return false;
			}

			var segmentLength = (bytes[index] << 8) + bytes[index + 1];
			if (segmentLength < 2 || index + segmentLength > bytes.Length)
			{
				return false;
			}

			if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
			{
				if (segmentLength < 7)
				{
					return false;
				}

				height = (bytes[index + 3] << 8) + bytes[index + 4];
				width = (bytes[index + 5] << 8) + bytes[index + 6];
				return width > 0 && height > 0;
			}

			index += segmentLength;
		}

		return false;
	}

	/// <summary>
	/// Reads dimensions from a VP8X, lossy VP8, or lossless VP8L WebP chunk.
	/// </summary>
	/// <param name="bytes">WebP bytes. An extended, lossy, or lossless header that is too short fails.</param>
	/// <param name="width">Canvas width when the method returns true. Zero when it returns false.</param>
	/// <param name="height">Canvas height when the method returns true. Zero when it returns false.</param>
	/// <returns>True when the RIFF WebP header and one supported chunk contain positive dimensions.</returns>
	private static bool TryReadWebp(byte[] bytes, out int width, out int height)
	{
		width = 0;
		height = 0;
		if (bytes.Length < 30
			|| bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F'
			|| bytes[8] != (byte)'W' || bytes[9] != (byte)'E' || bytes[10] != (byte)'B' || bytes[11] != (byte)'P')
		{
			return false;
		}

		var chunk = System.Text.Encoding.ASCII.GetString(bytes, 12, 4);
		if (chunk == "VP8X" && bytes.Length >= 30)
		{
			width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
			height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
			return width > 0 && height > 0;
		}

		if (chunk == "VP8 " && bytes.Length >= 30 && bytes[23] == 0x9D && bytes[24] == 0x01 && bytes[25] == 0x2A)
		{
			width = (bytes[26] | (bytes[27] << 8)) & 0x3FFF;
			height = (bytes[28] | (bytes[29] << 8)) & 0x3FFF;
			return width > 0 && height > 0;
		}

		if (chunk == "VP8L" && bytes.Length >= 25 && bytes[20] == 0x2F)
		{
			var bits = bytes[21] | (bytes[22] << 8) | (bytes[23] << 16) | (bytes[24] << 24);
			width = (bits & 0x3FFF) + 1;
			height = ((bits >> 14) & 0x3FFF) + 1;
			return width > 0 && height > 0;
		}

		return false;
	}

	/// <summary>
	/// Reads a big-endian 32-bit integer from a PNG IHDR field.
	/// </summary>
	/// <param name="bytes">Buffer that contains the four bytes at <paramref name="offset"/>.</param>
	/// <param name="offset">Index of the first byte.</param>
	/// <returns>The integer value of those four bytes.</returns>
	private static int ReadInt32Big(byte[] bytes, int offset)
	{
		return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
	}

	/// <summary>
	/// Elapsed milliseconds for one avatar GET's graph build and byte read.
	/// </summary>
	/// <remarks>
	/// A stage that did not run stays null. The first failed stage name is kept. The byte length is set only after the image bytes have been materialized.
	/// This object carries timings to the controller. It does not log them.
	/// </remarks>
	internal sealed class AvatarReadTiming
	{
		internal const string GraphTablesStage = "graphTables";

		internal const string TopologySeedStage = "topologySeed";

		internal const string TopologyWalkerStage = "topologyWalker";

		internal const string GraphExpansionStage = "graphExpansion";

		internal const string AvatarQueryStage = "avatarQuery";

		internal long? GraphTablesMilliseconds { get; private set; }

		internal long? TopologySeedMilliseconds { get; private set; }

		internal long? TopologyWalkerMilliseconds { get; private set; }

		internal long? GraphExpansionMilliseconds { get; private set; }

		internal long? AvatarQueryMilliseconds { get; private set; }

		internal string? FailedStage { get; private set; }

		internal int? ByteLength { get; set; }

		/// <summary>
		/// Stores the elapsed time for a stage that finished.
		/// </summary>
		/// <param name="stage">One of the avatar-read stage names.</param>
		/// <param name="elapsedMilliseconds">Elapsed time for that stage.</param>
		/// <returns>This method does not return a value.</returns>
		/// <remarks>A stage that already failed is not replaced by a later completion.</remarks>
		internal void CompleteStage(string stage, long elapsedMilliseconds)
		{
			Assign(stage, elapsedMilliseconds);
		}

		/// <summary>
		/// Stores the elapsed time and keeps the first failed stage name.
		/// </summary>
		/// <param name="stage">The stage that threw.</param>
		/// <param name="elapsedMilliseconds">Elapsed time before the exception left the stage.</param>
		/// <returns>This method does not return a value.</returns>
		/// <remarks>A later failure does not replace the first failed stage.</remarks>
		internal void FailStage(string stage, long elapsedMilliseconds)
		{
			FailedStage ??= stage;
			Assign(stage, elapsedMilliseconds);
		}

		/// <summary>
		/// Writes one stage elapsed time onto the matching property.
		/// </summary>
		/// <param name="stage">Stage name. An unrecognized name is ignored.</param>
		/// <param name="elapsedMilliseconds">Elapsed time to store.</param>
		/// <returns>This method does not return a value.</returns>
		private void Assign(string stage, long elapsedMilliseconds)
		{
			switch (stage)
			{
				case GraphTablesStage:
					GraphTablesMilliseconds = elapsedMilliseconds;
					break;
				case TopologySeedStage:
					TopologySeedMilliseconds = elapsedMilliseconds;
					break;
				case TopologyWalkerStage:
					TopologyWalkerMilliseconds = elapsedMilliseconds;
					break;
				case GraphExpansionStage:
					GraphExpansionMilliseconds = elapsedMilliseconds;
					break;
				case AvatarQueryStage:
					AvatarQueryMilliseconds = elapsedMilliseconds;
					break;
			}
		}
	}
}

/// <summary>
/// Body for a member profile Save. JSON fields use undefined to mean the control was not submitted.
/// </summary>
/// <remarks>
/// Anchor type, anchor id, and branch identify the selected occurrence. Profile text, placement, and avatar are optional parts of the same transaction.
/// Fronting is not accepted on this body.
/// </remarks>
public sealed class AnchorMemberProfileSaveRequest
{
	public string? AnchorObjectType { get; init; }

	public string? AnchorObjectId { get; init; }

	public string? Branch { get; init; }

	public JsonElement DisplayName { get; init; }

	public JsonElement Pronouns { get; init; }

	public JsonElement Description { get; init; }

	public JsonElement Color { get; init; }

	public JsonElement VisibilityMode { get; init; }

	public JsonElement VisibilityTypeId { get; init; }

	public JsonElement PermissionMode { get; init; }

	public JsonElement PermissionCode { get; init; }

	public JsonElement AvatarContentType { get; init; }

	public JsonElement AvatarImageBase64 { get; init; }
}

/// <summary>
/// Body for an immediate Fronting save. It does not carry profile, placement, or avatar changes.
/// </summary>
/// <remarks>Anchor type, anchor id, and branch are the selected occurrence. Fronting null is rejected by the endpoint.</remarks>
public sealed class MemberFrontingSaveRequest
{
	public string? AnchorObjectType { get; init; }

	public string? AnchorObjectId { get; init; }

	public string? Branch { get; init; }

	public bool? Fronting { get; init; }
}
