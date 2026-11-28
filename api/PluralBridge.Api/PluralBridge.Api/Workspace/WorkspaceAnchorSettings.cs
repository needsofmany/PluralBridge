// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Byte values for one account's stored anchor visibility or permission.
/// </summary>
/// <remarks>
/// <para>
/// Visibility and permission each use their own byte. The same number in the two bytes does not mean the same stored value.
/// A missing row, stored unset, stored inherit, and stored explicit stay distinct. Explicit keeps the stored type id or permission code.
/// </para>
/// <para>
/// Visibility: 0 missing, 1 unset, 2 inherit, 17 explicit type 1 (Only you), 18 explicit type 2 (Trusted friends), 19 explicit type 3 (All friends).
/// The explicit visibility byte is 16 plus the stored <c>VisibilityTypeId</c>.
/// </para>
/// <para>
/// Permission: 0 missing, 1 unset, 2 inherit, 16 explicit viewer, 17 explicit editor, 18 explicit manager, 19 explicit owner.
/// The word owner here is the stored permission code. It is not a membership role and it does not grant access.
/// </para>
/// <para>These bytes do not walk parent anchors and do not replace a mode with another mode.</para>
/// </remarks>
internal static class WorkspaceAnchorSettingCodes
{
	/// <summary>No settings row was stored for this anchor.</summary>
	internal const byte Missing = 0;

	/// <summary>The stored mode is unset. This is Not set, not a missing row.</summary>
	internal const byte Unset = 1;

	/// <summary>The stored mode is inherit. The parent is not consulted while the tree is loaded.</summary>
	internal const byte Inherit = 2;

	/// <summary>Explicit visibility type 1, Only you. The byte is 16 plus the stored type id.</summary>
	internal const byte ExplicitOnlyYou = 17;

	/// <summary>Explicit visibility type 2, Trusted friends.</summary>
	internal const byte ExplicitTrustedFriends = 18;

	/// <summary>Explicit visibility type 3, All friends.</summary>
	internal const byte ExplicitAllFriends = 19;

	/// <summary>Explicit permission code viewer.</summary>
	internal const byte ExplicitViewer = 16;

	/// <summary>Explicit permission code editor.</summary>
	internal const byte ExplicitEditor = 17;

	/// <summary>Explicit permission code manager.</summary>
	internal const byte ExplicitManager = 18;

	/// <summary>Explicit permission code owner. This is stored placement data, not a role.</summary>
	internal const byte ExplicitOwner = 19;

	/// <summary>Reads a resolved visibility byte back into the card mode and type id.</summary>
	/// <param name="value">A byte produced for a visibility side. Missing is not a prepared result.</param>
	/// <param name="mode">unset, inherit, or explicit. Empty when the method returns false.</param>
	/// <param name="typeId">1, 2, or 3 for explicit visibility. Null for unset and inherit.</param>
	/// <returns>True when the byte is one of the visibility codes.</returns>
	internal static bool TryDescribeVisibility(byte value, out string mode, out int? typeId)
	{
		mode = "";
		typeId = null;
		switch (value)
		{
			case Unset:
				mode = "unset";
				return true;
			case Inherit:
				mode = "inherit";
				return true;
			case ExplicitOnlyYou:
				mode = "explicit";
				typeId = 1;
				return true;
			case ExplicitTrustedFriends:
				mode = "explicit";
				typeId = 2;
				return true;
			case ExplicitAllFriends:
				mode = "explicit";
				typeId = 3;
				return true;
			default:
				return false;
		}
	}

	/// <summary>Reads a resolved permission byte back into the card mode and code.</summary>
	/// <param name="value">A byte produced for a permission side. Missing is not a prepared result.</param>
	/// <param name="mode">unset, inherit, or explicit. Empty when the method returns false.</param>
	/// <param name="code">viewer, editor, manager, or owner for explicit permission. Null for unset and inherit. This code is not a role.</param>
	/// <returns>True when the byte is one of the permission codes.</returns>
	internal static bool TryDescribePermission(byte value, out string mode, out string? code)
	{
		mode = "";
		code = null;
		switch (value)
		{
			case Unset:
				mode = "unset";
				return true;
			case Inherit:
				mode = "inherit";
				return true;
			case ExplicitViewer:
				mode = "explicit";
				code = "viewer";
				return true;
			case ExplicitEditor:
				mode = "explicit";
				code = "editor";
				return true;
			case ExplicitManager:
				mode = "explicit";
				code = "manager";
				return true;
			case ExplicitOwner:
				mode = "explicit";
				code = "owner";
				return true;
			default:
				return false;
		}
	}
}

/// <summary>
/// One stored anchor settings row before it is encoded.
/// </summary>
/// <param name="ObjectType">system, group, or place.</param>
/// <param name="ObjectId">The anchor id on that row.</param>
/// <param name="VisibilityMode">The stored visibility mode. Null is not a missing row.</param>
/// <param name="VisibilityTypeId">The stored type id. Explicit visibility uses 1, 2, or 3. Unset and inherit store null.</param>
/// <param name="PermissionMode">The stored permission mode. Null is not a missing row.</param>
/// <param name="PermissionCode">The stored permission code. Explicit permission uses viewer, editor, manager, or owner.</param>
internal sealed record WorkspaceStoredAnchorSetting(
	string ObjectType,
	Guid ObjectId,
	string? VisibilityMode,
	int? VisibilityTypeId,
	string? PermissionMode,
	string? PermissionCode);

/// <summary>
/// Visibility and permission retained for one anchor when the workspace tree is loaded.
/// </summary>
/// <param name="ObjectType">system, group, or place.</param>
/// <param name="ObjectId">The anchor id.</param>
/// <param name="Visibility">The account's stored visibility, encoded by <see cref="WorkspaceAnchorSettingCodes"/>.</param>
/// <param name="Permission">The account's stored permission, encoded by <see cref="WorkspaceAnchorSettingCodes"/>.</param>
/// <remarks>
/// Repeated occurrences of this anchor share this record. The bytes are not an authorization decision.
/// </remarks>
internal sealed record WorkspaceAnchorSetting(
	string ObjectType,
	Guid ObjectId,
	byte Visibility,
	byte Permission);

/// <summary>
/// Copies stored anchor settings onto every system, group, and place in one opening build.
/// </summary>
internal static class WorkspaceAnchorSettings
{
	/// <summary>
	/// Builds one settings record for every anchor in the copied tree.
	/// </summary>
	/// <param name="nodeLabels">Copied graph nodes. Account and member nodes are not anchors.</param>
	/// <param name="occurrences">Copied walk occurrences. Their order is not changed.</param>
	/// <param name="hierarchyPreview">Derived navigation rows. Headers are not anchors.</param>
	/// <param name="storedRows">Settings rows already limited to the authenticated account.</param>
	/// <returns>One record per anchor, in first-seen node, occurrence, then preview order.</returns>
	/// <exception cref="WorkspaceOpeningException">A stored row is outside the visibility and permission vocabulary, or one anchor has two rows.</exception>
	/// <remarks>
	/// An anchor with no stored row receives missing on both sides. A stored unset stays unset.
	/// A row for an object that is not an anchor in this tree is not added to the tree.
	/// </remarks>
	internal static IReadOnlyList<WorkspaceAnchorSetting> Retain(
		IReadOnlyList<WorkspaceNodeLabel> nodeLabels,
		IReadOnlyList<WorkspaceOccurrence> occurrences,
		IReadOnlyList<PluralMapPreviewRow> hierarchyPreview,
		IReadOnlyList<WorkspaceStoredAnchorSetting> storedRows)
	{
		var anchors = new List<(string Type, Guid Id)>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var label in nodeLabels)
		{
			AddAnchor(anchors, seen, label.ObjectType, label.ObjectId);
		}

		foreach (var occurrence in occurrences)
		{
			AddAnchor(anchors, seen, occurrence.ObjectType, occurrence.ObjectId);
		}

		foreach (var row in hierarchyPreview)
		{
			if (!row.Header)
			{
				AddAnchor(anchors, seen, row.ObjectType, row.ObjectId);
			}
		}

		var stored = new Dictionary<string, WorkspaceStoredAnchorSetting>(StringComparer.Ordinal);
		foreach (var row in storedRows)
		{
			var key = Key(row.ObjectType, row.ObjectId);
			if (!stored.TryAdd(key, row))
			{
				throw new WorkspaceOpeningException("The account has more than one stored setting for one anchor.");
			}
		}

		var retained = new List<WorkspaceAnchorSetting>(anchors.Count);
		foreach (var anchor in anchors)
		{
			var key = Key(anchor.Type, anchor.Id);
			if (!stored.TryGetValue(key, out var row))
			{
				retained.Add(new WorkspaceAnchorSetting(anchor.Type, anchor.Id, WorkspaceAnchorSettingCodes.Missing, WorkspaceAnchorSettingCodes.Missing));
				continue;
			}

			retained.Add(new WorkspaceAnchorSetting(
				anchor.Type,
				anchor.Id,
				EncodeVisibility(row),
				EncodePermission(row)));
		}

		return retained;
	}

	private static void AddAnchor(List<(string Type, Guid Id)> anchors, HashSet<string> seen, string? objectType, Guid? objectId)
	{
		if (objectType is not ("system" or "group" or "place") || objectId is not Guid id || id == Guid.Empty)
		{
			return;
		}

		if (seen.Add(Key(objectType, id)))
		{
			anchors.Add((objectType, id));
		}
	}

	private static string Key(string objectType, Guid objectId)
	{
		return objectType + ":" + objectId.ToString("D");
	}

	private static byte EncodeVisibility(WorkspaceStoredAnchorSetting row)
	{
		if (row.VisibilityMode == "unset" && row.VisibilityTypeId is null)
		{
			return WorkspaceAnchorSettingCodes.Unset;
		}

		if (row.VisibilityMode == "inherit" && row.VisibilityTypeId is null)
		{
			return WorkspaceAnchorSettingCodes.Inherit;
		}

		if (row.VisibilityMode == "explicit" && row.VisibilityTypeId is >= 1 and <= 3)
		{
			return (byte)(16 + row.VisibilityTypeId.Value);
		}

		throw new WorkspaceOpeningException("A stored anchor visibility setting is outside the visibility vocabulary.");
	}

	private static byte EncodePermission(WorkspaceStoredAnchorSetting row)
	{
		if (row.PermissionMode == "unset" && row.PermissionCode is null)
		{
			return WorkspaceAnchorSettingCodes.Unset;
		}

		if (row.PermissionMode == "inherit" && row.PermissionCode is null)
		{
			return WorkspaceAnchorSettingCodes.Inherit;
		}

		var code = row.PermissionMode == "explicit" ? row.PermissionCode : null;
		var encoded = code switch
		{
			"viewer" => WorkspaceAnchorSettingCodes.ExplicitViewer,
			"editor" => WorkspaceAnchorSettingCodes.ExplicitEditor,
			"manager" => WorkspaceAnchorSettingCodes.ExplicitManager,
			"owner" => WorkspaceAnchorSettingCodes.ExplicitOwner,
			_ => (byte?)null
		};
		if (encoded is byte value)
		{
			return value;
		}

		throw new WorkspaceOpeningException("A stored anchor permission setting is outside the permission vocabulary.");
	}
}
