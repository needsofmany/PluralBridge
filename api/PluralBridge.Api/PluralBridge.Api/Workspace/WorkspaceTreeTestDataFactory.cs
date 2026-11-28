// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// The tree source used when loading does not open a database.
/// </summary>
/// <remarks>
/// This factory returns stored mode text. It does not encode visibility or permission bytes and it does not build a context.
/// </remarks>
internal static class WorkspaceTreeTestDataFactory
{
	/// <summary>The account that owns the three nested systems.</summary>
	internal static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

	/// <summary>The outer system. Its parent is the account.</summary>
	internal static readonly Guid OuterSystemId = Guid.Parse("11111111-1111-4111-8111-111111111111");

	/// <summary>The middle system. Its parent is <see cref="OuterSystemId"/>.</summary>
	internal static readonly Guid MiddleSystemId = Guid.Parse("22222222-2222-4222-8222-222222222222");

	/// <summary>The inner system. Its parent is <see cref="MiddleSystemId"/>.</summary>
	internal static readonly Guid InnerSystemId = Guid.Parse("33333333-3333-4333-8333-333333333333");

	/// <summary>
	/// Three nested system anchors. Each stored visibility mode and permission mode is inherit.
	/// </summary>
	/// <returns>The captured build, in outer, middle, then inner occurrence order.</returns>
	internal static WorkspaceCapturedBuild ThreeNestedSystems()
	{
		return NestedSystems(
			Inherit("system", OuterSystemId),
			Inherit("system", MiddleSystemId),
			Inherit("system", InnerSystemId));
	}

	/// <summary>
	/// The same three nested systems. Outer is stored as explicit Only you and viewer. Middle and Inner are stored as inherit.
	/// </summary>
	/// <returns>The captured build. Settings are still mode text, not encoded bytes.</returns>
	internal static WorkspaceCapturedBuild ThreeNestedSystemsWithExplicitOuter()
	{
		return NestedSystems(
			new WorkspaceStoredAnchorSetting("system", OuterSystemId, "explicit", 1, "explicit", "viewer"),
			Inherit("system", MiddleSystemId),
			Inherit("system", InnerSystemId));
	}

	/// <summary>
	/// The same three nested systems. Middle is stored as explicit Only you and viewer. Outer and Inner are stored as inherit.
	/// </summary>
	/// <returns>The captured build. Settings are still mode text, not encoded bytes.</returns>
	internal static WorkspaceCapturedBuild ThreeNestedSystemsWithExplicitMiddle()
	{
		return NestedSystems(
			Inherit("system", OuterSystemId),
			new WorkspaceStoredAnchorSetting("system", MiddleSystemId, "explicit", 1, "explicit", "viewer"),
			Inherit("system", InnerSystemId));
	}

	/// <summary>
	/// The same three nested systems. Visibility stops at Middle. Permission stops at Outer.
	/// </summary>
	/// <returns>The captured build. Settings are still mode text, not encoded bytes.</returns>
	/// <remarks>
	/// Outer visibility is inherit and its permission is explicit viewer.
	/// Middle visibility is explicit Only you and its permission is inherit.
	/// Inner is inherit on both sides.
	/// </remarks>
	internal static WorkspaceCapturedBuild ThreeNestedSystemsWithIndependentSides()
	{
		return NestedSystems(
			new WorkspaceStoredAnchorSetting("system", OuterSystemId, "inherit", null, "explicit", "viewer"),
			new WorkspaceStoredAnchorSetting("system", MiddleSystemId, "explicit", 1, "inherit", null),
			Inherit("system", InnerSystemId));
	}

	/// <summary>
	/// The independent-sides tree plus direct member rows under the inner occurrence.
	/// </summary>
	/// <param name="directMemberIds">Member ids in batch order. They are not anchors and they do not change anchor settings.</param>
	/// <returns>The captured build. Settings are still mode text, not encoded bytes.</returns>
	internal static WorkspaceCapturedBuild ThreeNestedSystemsWithIndependentSidesAndMembers(IReadOnlyList<Guid> directMemberIds)
	{
		var build = ThreeNestedSystemsWithIndependentSides();
		var preview = build.HierarchyPreview.ToList();
		foreach (var memberId in directMemberIds)
		{
			var path = "member-" + memberId.ToString("N");
			preview.Add(new PluralMapPreviewRow("preview", path, path, "Member", "member", memberId, "outer/middle/inner", false, null));
		}

		return build with { HierarchyPreview = preview };
	}

	/// <summary>Builds the nested system tree with the supplied stored settings.</summary>
	/// <param name="outer">The stored row for the outer system.</param>
	/// <param name="middle">The stored row for the middle system.</param>
	/// <param name="inner">The stored row for the inner system.</param>
	/// <returns>The captured build, in outer, middle, then inner occurrence order.</returns>
	private static WorkspaceCapturedBuild NestedSystems(
		WorkspaceStoredAnchorSetting outer,
		WorkspaceStoredAnchorSetting middle,
		WorkspaceStoredAnchorSetting inner)
	{
		return new WorkspaceCapturedBuild(
			AccountId,
			[
				new WorkspaceGraphEdge("account", AccountId, "system", OuterSystemId, 1, "placement"),
				new WorkspaceGraphEdge("system", OuterSystemId, "system", MiddleSystemId, 1, "placement"),
				new WorkspaceGraphEdge("system", MiddleSystemId, "system", InnerSystemId, 1, "placement")
			],
			[
				new WorkspaceOccurrence("account", AccountId, "system", OuterSystemId, 0, "outer"),
				new WorkspaceOccurrence("system", OuterSystemId, "system", MiddleSystemId, 1, "outer/middle"),
				new WorkspaceOccurrence("system", MiddleSystemId, "system", InnerSystemId, 2, "outer/middle/inner")
			],
			[
				new WorkspaceNodeLabel("system", OuterSystemId, "System: Outer"),
				new WorkspaceNodeLabel("system", MiddleSystemId, "System: Middle"),
				new WorkspaceNodeLabel("system", InnerSystemId, "System: Inner")
			],
			[],
			[
				new PluralMapPreviewRow("preview", "outer", "outer", "System: Outer", "system", OuterSystemId, null, false, "Outer"),
				new PluralMapPreviewRow("preview", "outer/middle", "outer/middle", "System: Middle", "system", MiddleSystemId, "outer", false, "Middle"),
				new PluralMapPreviewRow("preview", "outer/middle/inner", "outer/middle/inner", "System: Inner", "system", InnerSystemId, "outer/middle", false, "Inner")
			],
			[],
			[outer, middle, inner]);
	}

	/// <summary>One stored inherit row. The type id and permission code stay null, as the settings table requires.</summary>
	/// <param name="objectType">The anchor type.</param>
	/// <param name="objectId">The anchor id.</param>
	/// <returns>The stored row, still in mode text.</returns>
	private static WorkspaceStoredAnchorSetting Inherit(string objectType, Guid objectId)
	{
		return new WorkspaceStoredAnchorSetting(objectType, objectId, "inherit", null, "inherit", null);
	}
}
