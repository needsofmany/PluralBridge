// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Controllers;

/// <summary>
/// One member card produced from the profile procedure.
/// </summary>
/// <param name="MemberId">The shared member id.</param>
/// <param name="DisplayName">The shared display name.</param>
/// <param name="Pronouns">The shared pronouns.</param>
/// <param name="Description">The shared description.</param>
/// <param name="Color">The shared color.</param>
/// <param name="VisibilityTypeId">The member-level visibility type id.</param>
/// <param name="VisibilityTypeCode">The member-level visibility type code.</param>
/// <param name="VisibilityTypeName">The member-level visibility type name.</param>
/// <param name="IsFronting">The shared fronting state. An open interval from the fronting set replaces the profile column.</param>
/// <param name="PlacementVisibilityMode">The placement visibility mode. Inherit when this anchor has no stored visibility setting.</param>
/// <param name="PlacementVisibilityTypeId">The placement visibility type.</param>
/// <param name="PlacementPermissionMode">The placement permission mode. Inherit when this anchor has no stored permission setting.</param>
/// <param name="PlacementPermissionCode">The placement permission code.</param>
/// <param name="ResolvedVisibilityMode">The visibility mode after branch inheritance.</param>
/// <param name="ResolvedVisibilityTypeId">The visibility type after branch inheritance.</param>
/// <param name="ResolvedPermissionMode">The permission mode after branch inheritance.</param>
/// <param name="ResolvedPermissionCode">The permission code after branch inheritance.</param>
/// <param name="AvatarUrl">The avatar route. Null when this response must not start an image request.</param>
internal sealed record AnchorMemberCard(
	Guid MemberId,
	string DisplayName,
	string? Pronouns,
	string? Description,
	string? Color,
	int? VisibilityTypeId,
	string? VisibilityTypeCode,
	string? VisibilityTypeName,
	bool IsFronting,
	string? PlacementVisibilityMode,
	int? PlacementVisibilityTypeId,
	string? PlacementPermissionMode,
	string? PlacementPermissionCode,
	string ResolvedVisibilityMode,
	int? ResolvedVisibilityTypeId,
	string ResolvedPermissionMode,
	string? ResolvedPermissionCode,
	string? AvatarUrl);

/// <summary>
/// Maps a profile bundle to card rows. Placement stays on the anchor. Fronting and profile text stay on the member.
/// </summary>
internal static class AnchorMemberProfileMapper
{
	/// <summary>
	/// Projects the bundle in procedure order, or in <paramref name="memberOrder"/> when that list is supplied.
	/// </summary>
	/// <param name="bundle">The six-set profile bundle.</param>
	/// <param name="memberOrder">Member ids to keep, in response order. Null keeps every profile row.</param>
	/// <param name="includeAvatar">False leaves <see cref="AnchorMemberCard.AvatarUrl"/> null. The avatar route is not required for the card text.</param>
	/// <returns>The card rows. A requested id with no profile row is omitted.</returns>
	internal static IReadOnlyList<AnchorMemberCard> Project(
		AnchorProfileBundle bundle,
		IReadOnlyList<Guid>? memberOrder,
		bool includeAvatar)
	{
		var placementByMember = bundle.Placements.ToDictionary(
			row => row.MemberId,
			row => new AnchorMemberProfileSave.MemberPlacementRow(
				row.MemberId,
				row.VisibilityMode,
				row.VisibilityTypeId,
				row.PermissionMode,
				row.PermissionCode));
		var openFronting = bundle.Fronting.ToDictionary(row => row.MemberId, row => row.IsFronting);
		var avatarPaths = includeAvatar
			? bundle.Avatars.ToDictionary(row => row.MemberId, row => MeController.AvatarReferenceUrl(row.MemberId, row.UpdatedAtUtc))
			: new Dictionary<Guid, string>();
		var branchNodes = bundle.BranchValid
			? bundle.BranchAnchors
				.Select(anchor => new AnchorMemberProfileSave.BranchNode(anchor.ObjectType, anchor.ObjectId))
				.ToArray()
			: Array.Empty<AnchorMemberProfileSave.BranchNode>();
		var anchorSettings = bundle.BranchValid
			? bundle.BranchAnchors
				.Select(anchor => new AnchorMemberProfileSave.AnchorSetting(
					anchor.ObjectType,
					anchor.ObjectId,
					anchor.VisibilityMode ?? "unset",
					anchor.VisibilityTypeId,
					anchor.PermissionMode ?? "unset",
					anchor.PermissionCode))
				.ToArray()
			: Array.Empty<AnchorMemberProfileSave.AnchorSetting>();
		var profilesById = new Dictionary<Guid, AnchorMemberProfile>();
		foreach (var profile in bundle.Profiles)
		{
			profilesById.TryAdd(profile.MemberId, profile);
		}

		var cards = new List<AnchorMemberCard>();
		if (memberOrder is null)
		{
			foreach (var profile in bundle.Profiles)
			{
				cards.Add(MapOne(profile, placementByMember, openFronting, avatarPaths, branchNodes, anchorSettings, bundle.BranchValid, includeAvatar));
			}
		}
		else
		{
			foreach (var memberId in memberOrder)
			{
				if (profilesById.TryGetValue(memberId, out var profile))
				{
					cards.Add(MapOne(profile, placementByMember, openFronting, avatarPaths, branchNodes, anchorSettings, bundle.BranchValid, includeAvatar));
				}
			}
		}

		return cards;
	}

	/// <summary>
	/// Projects workspace cards from one occurrence resolution. Null placement sides use that resolution. Stored sides stay as stored.
	/// </summary>
	/// <param name="bundle">The live profile bundle. Its branch anchors are not walked.</param>
	/// <param name="memberOrder">Direct member ids for this occurrence, already capped by the context.</param>
	/// <param name="defaultVisibilityMode">The occurrence visibility mode. Used only when a member visibility mode is null.</param>
	/// <param name="defaultVisibilityTypeId">The occurrence visibility type. Null unless the default mode is explicit.</param>
	/// <param name="defaultPermissionMode">The occurrence permission mode. Used only when a member permission mode is null.</param>
	/// <param name="defaultPermissionCode">The occurrence permission code. Null unless the default mode is explicit.</param>
	/// <returns>The card rows. A requested id with no profile row is omitted. Avatar URLs stay null.</returns>
	/// <remarks>
	/// A null stored mode is returned as placement inherit so an untouched control does not save the resolved value as an override.
	/// The resolved fields carry the occurrence default for that null side. Explicit and unset placements are copied onto both the placement and resolved fields.
	/// A stored inherit stays inherit and does not take the occurrence default. This method does not write settings.
	/// </remarks>
	internal static IReadOnlyList<AnchorMemberCard> ProjectWithOccurrenceDefaults(
		AnchorProfileBundle bundle,
		IReadOnlyList<Guid> memberOrder,
		string defaultVisibilityMode,
		int? defaultVisibilityTypeId,
		string defaultPermissionMode,
		string? defaultPermissionCode)
	{
		var placementByMember = new Dictionary<Guid, AnchorMemberPlacement>();
		foreach (var row in bundle.Placements)
		{
			placementByMember.TryAdd(row.MemberId, row);
		}

		var openFronting = bundle.Fronting.ToDictionary(row => row.MemberId, row => row.IsFronting);
		var profilesById = new Dictionary<Guid, AnchorMemberProfile>();
		foreach (var profile in bundle.Profiles)
		{
			profilesById.TryAdd(profile.MemberId, profile);
		}

		var cards = new List<AnchorMemberCard>();
		foreach (var memberId in memberOrder)
		{
			if (!profilesById.TryGetValue(memberId, out var profile))
			{
				continue;
			}

			placementByMember.TryGetValue(memberId, out var placement);
			var visibility = PrepareSide(
				placement?.VisibilityMode,
				placement?.VisibilityTypeId,
				null,
				defaultVisibilityMode,
				defaultVisibilityTypeId,
				null);
			var permission = PrepareSide(
				placement?.PermissionMode,
				null,
				placement?.PermissionCode,
				defaultPermissionMode,
				null,
				defaultPermissionCode);
			var isFronting = openFronting.TryGetValue(profile.MemberId, out var openInterval)
				? openInterval
				: profile.IsFronting;
			cards.Add(new AnchorMemberCard(
				profile.MemberId,
				profile.DisplayName,
				profile.Pronouns,
				profile.Description,
				profile.Color,
				profile.VisibilityTypeId,
				profile.VisibilityTypeCode,
				profile.VisibilityTypeName,
				isFronting,
				visibility.PlacementMode,
				visibility.PlacementTypeId,
				permission.PlacementMode,
				permission.PlacementCode,
				visibility.ResolvedMode,
				visibility.ResolvedTypeId,
				permission.ResolvedMode,
				permission.ResolvedCode,
				null));
		}

		return cards;
	}

	/// <summary>One card side after the occurrence default has been applied only to a null stored mode.</summary>
	/// <param name="PlacementMode">The stored mode, or inherit when nothing was stored.</param>
	/// <param name="PlacementTypeId">The stored visibility type for an explicit placement. Null otherwise.</param>
	/// <param name="PlacementCode">The stored permission code for an explicit placement. Null otherwise.</param>
	/// <param name="ResolvedMode">The mode shown as the branch result.</param>
	/// <param name="ResolvedTypeId">The resolved visibility type.</param>
	/// <param name="ResolvedCode">The resolved permission code.</param>
	private readonly record struct PreparedSide(
		string PlacementMode,
		int? PlacementTypeId,
		string? PlacementCode,
		string ResolvedMode,
		int? ResolvedTypeId,
		string? ResolvedCode);

	/// <summary>Keeps a stored side. A null stored mode uses the occurrence default for the resolved fields only.</summary>
	/// <param name="storedMode">The member placement mode. Null or blank has no stored setting.</param>
	/// <param name="storedTypeId">The stored visibility type. Ignored unless the stored mode is explicit.</param>
	/// <param name="storedCode">The stored permission code. Ignored unless the stored mode is explicit.</param>
	/// <param name="defaultMode">The occurrence mode for a null side.</param>
	/// <param name="defaultTypeId">The occurrence visibility type for a null side.</param>
	/// <param name="defaultCode">The occurrence permission code for a null side.</param>
	/// <returns>The placement fields and the resolved fields for this side.</returns>
	private static PreparedSide PrepareSide(
		string? storedMode,
		int? storedTypeId,
		string? storedCode,
		string defaultMode,
		int? defaultTypeId,
		string? defaultCode)
	{
		if (string.IsNullOrWhiteSpace(storedMode))
		{
			return new PreparedSide(
				"inherit",
				null,
				null,
				defaultMode,
				defaultMode == "explicit" ? defaultTypeId : null,
				defaultMode == "explicit" ? defaultCode : null);
		}

		var explicitSide = storedMode == "explicit";
		var typeId = explicitSide ? storedTypeId : null;
		var code = explicitSide ? storedCode : null;
		return new PreparedSide(storedMode, typeId, code, storedMode, typeId, code);
	}

	/// <summary>Maps one profile row. The first placement and fronting row for that member are used.</summary>
	private static AnchorMemberCard MapOne(
		AnchorMemberProfile profile,
		IReadOnlyDictionary<Guid, AnchorMemberProfileSave.MemberPlacementRow> placementByMember,
		IReadOnlyDictionary<Guid, bool> openFronting,
		IReadOnlyDictionary<Guid, string> avatarPaths,
		IReadOnlyList<AnchorMemberProfileSave.BranchNode> branchNodes,
		IReadOnlyList<AnchorMemberProfileSave.AnchorSetting> anchorSettings,
		bool branchValid,
		bool includeAvatar)
	{
		placementByMember.TryGetValue(profile.MemberId, out var placement);
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			placement,
			branchNodes,
			anchorSettings,
			branchValid);
		var isFronting = openFronting.TryGetValue(profile.MemberId, out var openInterval)
			? openInterval
			: profile.IsFronting;
		avatarPaths.TryGetValue(profile.MemberId, out var avatarUrl);
		// A missing mode is inherit on the card. A stored explicit value keeps its type or code.
		var visibilityMode = AnchorMemberProfileSave.PlacementModeOrInherit(placement?.VisibilityMode);
		var permissionMode = AnchorMemberProfileSave.PlacementModeOrInherit(placement?.PermissionMode);
		return new AnchorMemberCard(
			profile.MemberId,
			profile.DisplayName,
			profile.Pronouns,
			profile.Description,
			profile.Color,
			profile.VisibilityTypeId,
			profile.VisibilityTypeCode,
			profile.VisibilityTypeName,
			isFronting,
			visibilityMode,
			visibilityMode == "explicit" ? placement?.VisibilityTypeId : null,
			permissionMode,
			permissionMode == "explicit" ? placement?.PermissionCode : null,
			resolved.VisibilityMode,
			resolved.VisibilityTypeId,
			resolved.PermissionMode,
			resolved.PermissionCode,
			includeAvatar ? avatarUrl : null);
	}
}
