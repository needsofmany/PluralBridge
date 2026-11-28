// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Display inheritance for a member placement that has no stored setting.
/// These tests do not open a database connection. The insert default is checked from the save statement.
/// </summary>
public sealed class PlacementInheritanceTests
{
	private static readonly Guid Harbor = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid Night = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid MemberId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

	/// <summary>A missing placement inherits an explicit anchor on each side.</summary>
	[Fact]
	public void MissingPlacement_InheritsAnExplicitAnchor()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, null, null),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: true);

		Assert.Equal("explicit", resolved.VisibilityMode);
		Assert.Equal(1, resolved.VisibilityTypeId);
		Assert.Equal("explicit", resolved.PermissionMode);
		Assert.Equal("viewer", resolved.PermissionCode);

		var card = Map(null, null, null, null, ("system", Harbor, "explicit", 1, "explicit", "viewer"));
		Assert.Equal("inherit", card.PlacementVisibilityMode);
		Assert.Null(card.PlacementVisibilityTypeId);
		Assert.Equal("inherit", card.PlacementPermissionMode);
		Assert.Null(card.PlacementPermissionCode);
		Assert.Equal("explicit", card.ResolvedVisibilityMode);
		Assert.Equal("viewer", card.ResolvedPermissionCode);
	}

	/// <summary>A missing placement walks through an inherit anchor to an explicit parent.</summary>
	[Fact]
	public void MissingPlacement_WalksThroughAnInheritAnchor()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, null, null),
			Branch(("group", Harbor), ("system", Night)),
			Anchors(
				("group", Harbor, "inherit", null, "inherit", null),
				("system", Night, "explicit", 3, "explicit", "editor")),
			branchValid: true);

		Assert.Equal("explicit", resolved.VisibilityMode);
		Assert.Equal(3, resolved.VisibilityTypeId);
		Assert.Equal("editor", resolved.PermissionCode);
	}

	/// <summary>A missing ancestor is unset and stops that side.</summary>
	[Fact]
	public void MissingPlacement_StopsWhenTheAncestorSettingIsMissing()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, null, null),
			Branch(("system", Harbor), ("system", Night)),
			Anchors(("system", Harbor, "inherit", null, "inherit", null)),
			branchValid: true);

		Assert.Equal("unset", resolved.VisibilityMode);
		Assert.Null(resolved.VisibilityTypeId);
		Assert.Equal("unset", resolved.PermissionMode);
		Assert.Null(resolved.PermissionCode);
	}

	/// <summary>Every inherit anchor leaves the resolved value as inherit.</summary>
	[Fact]
	public void MissingPlacement_StaysInheritWhenTheBranchInherits()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, null, null),
			Branch(("group", Harbor), ("system", Night)),
			Anchors(
				("group", Harbor, "inherit", null, "inherit", null),
				("system", Night, "inherit", null, "inherit", null)),
			branchValid: true);

		Assert.Equal("inherit", resolved.VisibilityMode);
		Assert.Null(resolved.VisibilityTypeId);
		Assert.Equal("inherit", resolved.PermissionMode);
		Assert.Null(resolved.PermissionCode);
	}

	/// <summary>Stored unset does not take the anchor value. Stored explicit ignores the anchor.</summary>
	[Fact]
	public void StoredUnsetAndExplicit_KeepTheirOwnModes()
	{
		var unset = AnchorMemberProfileSave.ResolvePlacement(
			Placement("unset", null, "unset", null),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: true);
		Assert.Equal("unset", unset.VisibilityMode);
		Assert.Equal("unset", unset.PermissionMode);

		var explicitPlacement = AnchorMemberProfileSave.ResolvePlacement(
			Placement("explicit", 2, "explicit", "manager"),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: true);
		Assert.Equal(2, explicitPlacement.VisibilityTypeId);
		Assert.Equal("manager", explicitPlacement.PermissionCode);

		var card = Map("unset", null, "explicit", "owner", ("system", Harbor, "explicit", 1, "explicit", "viewer"));
		Assert.Equal("unset", card.PlacementVisibilityMode);
		Assert.Null(card.PlacementVisibilityTypeId);
		Assert.Equal("explicit", card.PlacementPermissionMode);
		Assert.Equal("owner", card.PlacementPermissionCode);
	}

	/// <summary>One side can inherit while the other keeps a stored mode.</summary>
	[Fact]
	public void Sides_ResolveIndependently()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, "explicit", "owner"),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: true);

		Assert.Equal("explicit", resolved.VisibilityMode);
		Assert.Equal(1, resolved.VisibilityTypeId);
		Assert.Equal("explicit", resolved.PermissionMode);
		Assert.Equal("owner", resolved.PermissionCode);

		var otherSide = AnchorMemberProfileSave.ResolvePlacement(
			Placement("unset", null, null, null),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 3, "explicit", "editor")),
			branchValid: true);
		Assert.Equal("unset", otherSide.VisibilityMode);
		Assert.Equal("editor", otherSide.PermissionCode);
	}

	/// <summary>The same member keeps the placement of the anchor being resolved.</summary>
	[Fact]
	public void SameMember_KeepsEachAnchorPlacement()
	{
		var harbor = Map("explicit", 1, "explicit", "viewer", ("system", Harbor, "inherit", null, "inherit", null));
		var night = Map(null, null, null, null, ("system", Night, "explicit", 3, "explicit", "editor"));

		Assert.Equal(MemberId, harbor.MemberId);
		Assert.Equal(MemberId, night.MemberId);
		Assert.Equal("explicit", harbor.PlacementVisibilityMode);
		Assert.Equal(1, harbor.ResolvedVisibilityTypeId);
		Assert.Equal("inherit", night.PlacementVisibilityMode);
		Assert.Equal(3, night.ResolvedVisibilityTypeId);
		Assert.Equal("editor", night.ResolvedPermissionCode);
	}

	/// <summary>An invalid branch does not walk. A missing placement stays inherit.</summary>
	[Fact]
	public void InvalidBranch_DoesNotWalkAnInheritingPlacement()
	{
		var resolved = AnchorMemberProfileSave.ResolvePlacement(
			Placement(null, null, null, null),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: false);

		Assert.False(resolved.BranchValid);
		Assert.Equal("inherit", resolved.VisibilityMode);
		Assert.Null(resolved.VisibilityTypeId);
		Assert.Equal("inherit", resolved.PermissionMode);
		Assert.Null(resolved.PermissionCode);

		var stored = AnchorMemberProfileSave.ResolvePlacement(
			Placement("unset", null, "explicit", "manager"),
			Branch(("system", Harbor)),
			Anchors(("system", Harbor, "explicit", 1, "explicit", "viewer")),
			branchValid: false);
		Assert.Equal("unset", stored.VisibilityMode);
		Assert.Equal("manager", stored.PermissionCode);
	}

	/// <summary>The first insert of one side stores inherit for the side the request did not set.</summary>
	[Fact]
	public void FirstInsert_PreservesInheritOnTheOmittedSide()
	{
		var source = File.ReadAllText(SourcePath("AnchorMemberProfileSave.cs"));
		Assert.Equal(6, CountOf(source, "ELSE N'inherit'"));
		Assert.DoesNotContain("ELSE N'unset'", source, StringComparison.Ordinal);
		Assert.Equal(3, CountOf(source, "CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE N'inherit'"));
		Assert.Equal(3, CountOf(source, "CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE N'inherit'"));
		Assert.Contains("ELSE VisibilityMode END", source, StringComparison.Ordinal);
		Assert.Contains("ELSE PermissionMode END", source, StringComparison.Ordinal);
	}

	/// <summary>A requested unset is the parameter value, not the omitted-side default.</summary>
	[Fact]
	public void RequestedUnset_RemainsTheStoredMode()
	{
		var source = File.ReadAllText(SourcePath("AnchorMemberProfileSave.cs"));
		Assert.Contains("CASE WHEN @SetVisibility = 1 THEN @VisibilityMode ELSE N'inherit'", source, StringComparison.Ordinal);
		Assert.Contains("CASE WHEN @SetPermission = 1 THEN @PermissionMode ELSE N'inherit'", source, StringComparison.Ordinal);
		Assert.DoesNotContain("THEN N'inherit'", source, StringComparison.Ordinal);
	}

	/// <summary>The card sends visibility and permission only after those controls are marked.</summary>
	[Fact]
	public void UntouchedInheritedControls_AreOmittedFromTheSaveRequest()
	{
		var script = File.ReadAllText(AppPath("member-card.js"));
		var body = FunctionBody(script, "function profilePreviewSaveRequest");
		var sharingMark = body.IndexOf("marked.indexOf(sharingButton) >= 0", StringComparison.Ordinal);
		var visibilityMode = body.IndexOf("payload.visibilityMode", StringComparison.Ordinal);
		var permissionMark = body.IndexOf("marked.indexOf(permissionControl) >= 0", StringComparison.Ordinal);
		var permissionMode = body.IndexOf("payload.permissionMode", StringComparison.Ordinal);

		Assert.True(sharingMark >= 0 && visibilityMode > sharingMark);
		Assert.True(permissionMark >= 0 && permissionMode > permissionMark);
		Assert.DoesNotContain("payload.visibilityMode", body[..sharingMark], StringComparison.Ordinal);
		Assert.DoesNotContain("payload.permissionMode", body[..permissionMark], StringComparison.Ordinal);
	}

	private static AnchorMemberProfileSave.MemberPlacementRow Placement(
		string? visibilityMode,
		int? visibilityTypeId,
		string? permissionMode,
		string? permissionCode)
	{
		return new AnchorMemberProfileSave.MemberPlacementRow(
			MemberId,
			visibilityMode,
			visibilityTypeId,
			permissionMode,
			permissionCode);
	}

	private static IReadOnlyList<AnchorMemberProfileSave.BranchNode> Branch(
		params (string Type, Guid Id)[] nodes)
	{
		return nodes.Select(node => new AnchorMemberProfileSave.BranchNode(node.Type, node.Id)).ToArray();
	}

	private static IReadOnlyList<AnchorMemberProfileSave.AnchorSetting> Anchors(
		params (string Type, Guid Id, string Visibility, int? TypeId, string Permission, string? Code)[] nodes)
	{
		return nodes.Select(node => new AnchorMemberProfileSave.AnchorSetting(
			node.Type,
			node.Id,
			node.Visibility,
			node.TypeId,
			node.Permission,
			node.Code)).ToArray();
	}

	private static AnchorMemberCard Map(
		string? visibilityMode,
		int? visibilityTypeId,
		string? permissionMode,
		string? permissionCode,
		params (string Type, Guid Id, string Visibility, int? TypeId, string Permission, string? Code)[] anchors)
	{
		var anchorRows = anchors.Select(node => new AnchorBranchAnchor(
			node.Type,
			node.Id,
			node.Visibility,
			node.TypeId,
			node.Permission,
			node.Code)).ToArray();
		var bundle = new AnchorProfileBundle(
			[new AnchorMemberProfile(MemberId, "A", null, null, null, null, null, null, false)],
			true,
			anchorRows,
			[new AnchorMemberPlacement(MemberId, visibilityMode, visibilityTypeId, permissionMode, permissionCode)],
			[],
			[]);
		return Assert.Single(AnchorMemberProfileMapper.Project(bundle, [MemberId], includeAvatar: false));
	}

	private static string FunctionBody(string script, string signature)
	{
		var start = script.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0);
		var next = script.IndexOf("function canonicalVisibilityId", start, StringComparison.Ordinal);
		Assert.True(next > start);
		return script[start..next];
	}

	private static int CountOf(string text, string value)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += value.Length;
		}

		return count;
	}

	private static string SourcePath(string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"Controllers",
			fileName));
	}

	private static string AppPath(string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"app",
			"src",
			"js",
			fileName));
	}
}
