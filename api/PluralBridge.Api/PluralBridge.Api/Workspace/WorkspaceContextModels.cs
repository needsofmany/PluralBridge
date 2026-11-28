// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>The result later lookups return for every unavailable context.</summary>
internal static class WorkspaceContextResults
{
	/// <summary>Missing, expired, evicted, and wrong-account lookups all use this value.</summary>
	internal const string Unavailable = "context_unavailable";
}

/// <summary>
/// One access-membership row captured with the opening build.
/// </summary>
/// <param name="ObjectMembershipId">The membership identity.</param>
/// <param name="ObjectType">The anchor type: system, group, or place.</param>
/// <param name="ObjectId">The anchor identity. The zero guid is not stored.</param>
/// <param name="MembershipStatusId">The status id, when the row had one.</param>
/// <param name="StatusName">The status name. Seed reachability uses Active.</param>
/// <param name="IsActive">The status flag. The profile procedure also requires this to be true.</param>
/// <remarks>
/// Several rows for one object are retained so a later check can see a duplicate membership.
/// These facts are not a profile authorization decision and they are not avatar reachability.
/// </remarks>
internal sealed record WorkspaceAccessFact(
	Guid ObjectMembershipId,
	string ObjectType,
	Guid ObjectId,
	int? MembershipStatusId,
	string? StatusName,
	bool? IsActive);

/// <summary>One copied graph edge.</summary>
internal sealed record WorkspaceGraphEdge(
	string ParentObjectType,
	Guid ParentObjectId,
	string ChildObjectType,
	Guid ChildObjectId,
	int EdgeRank,
	string EdgeSource);

/// <summary>One walk occurrence. <paramref name="PathKey"/> stays opaque.</summary>
internal sealed record WorkspaceOccurrence(
	string ParentObjectType,
	Guid ParentObjectId,
	string ObjectType,
	Guid ObjectId,
	int Depth,
	string PathKey);

/// <summary>One node label copied from the graph.</summary>
internal sealed record WorkspaceNodeLabel(string ObjectType, Guid ObjectId, string NodeLabel);

/// <summary>One system edge label copied before the connection closes.</summary>
internal sealed record WorkspaceSystemEdgeLabel(Guid SystemId, string? EdgeLabel);

/// <summary>
/// The anchor identity derived from one validated occurrence.
/// </summary>
/// <param name="ObjectType">system, group, or place.</param>
/// <param name="ObjectId">The anchor id stored on that occurrence.</param>
/// <param name="AnchorName">The anchor name stored on that occurrence. The profile procedure matches this name.</param>
/// <param name="Branch">Type and id pairs from the occurrence toward the account, comma-separated. At most 32 nodes.</param>
	/// <param name="DirectMemberIds">One page of direct member occurrences, in preview order. At most <see cref="WorkspaceContext.DetailBatchSize"/>.</param>
/// <remarks>Access facts are not part of this value. They do not grant the profile read.</remarks>
internal readonly record struct WorkspaceAnchorDetail(
	string ObjectType,
	Guid ObjectId,
	string AnchorName,
	string Branch,
	IReadOnlyList<Guid> DirectMemberIds);

/// <summary>
/// One avatar retained for a member inside a single workspace context.
/// </summary>
/// <param name="MemberId">The member whose image this context holds.</param>
/// <param name="Bytes">The bytes returned by the avatar load. The context does not copy or decode them.</param>
/// <param name="ContentType">The content type returned with those bytes.</param>
/// <remarks>
/// Repeated occurrences of the member share this entry. Holding it does not authorize a later read, and another context cannot see it.
/// </remarks>
internal sealed record WorkspaceRetainedAvatar(Guid MemberId, byte[] Bytes, string ContentType);

/// <summary>
/// The context retained for one opening build.
/// </summary>
/// <remarks>
/// Later detail and image requests must not treat this copy as current authorization until a freshness contract is approved.
/// Database freshness detection is not part of this object. The opening copy does not include profile text, fronting, or image bytes.
/// A later load may retain one image per member on this context. That entry is not an access grant.
/// Anchor visibility and permission bytes are the account's stored settings. They are not an access decision.
/// </remarks>
internal sealed class WorkspaceContext
{
	private readonly object _avatarGate = new();
	private readonly Dictionary<Guid, WorkspaceRetainedAvatar> _avatars = [];

	private WorkspaceContext(
		string contextId,
		Guid accountId,
		IReadOnlyList<WorkspaceGraphEdge> edges,
		IReadOnlyList<WorkspaceOccurrence> occurrences,
		IReadOnlyList<WorkspaceNodeLabel> nodeLabels,
		IReadOnlyList<WorkspaceSystemEdgeLabel> systemEdgeLabels,
		IReadOnlyList<PluralMapPreviewRow> hierarchyPreview,
		IReadOnlyList<WorkspaceAccessFact> accessFacts,
		IReadOnlyList<WorkspaceAnchorSetting> anchorSettings,
		long estimatedBytes)
	{
		ContextId = contextId;
		AccountId = accountId;
		Edges = edges;
		Occurrences = occurrences;
		NodeLabels = nodeLabels;
		SystemEdgeLabels = systemEdgeLabels;
		HierarchyPreview = hierarchyPreview;
		AccessFacts = accessFacts;
		AnchorSettings = anchorSettings;
		EstimatedBytes = estimatedBytes;
	}

	/// <summary>The most direct member occurrences one detail response includes.</summary>
	public const int DetailBatchSize = 20;

	/// <summary>The opaque context id.</summary>
	public string ContextId { get; }

	/// <summary>The account this build belongs to.</summary>
	public Guid AccountId { get; }

	/// <summary>Copied graph edges.</summary>
	public IReadOnlyList<WorkspaceGraphEdge> Edges { get; }

	/// <summary>Copied walk occurrences.</summary>
	public IReadOnlyList<WorkspaceOccurrence> Occurrences { get; }

	/// <summary>Copied node labels.</summary>
	public IReadOnlyList<WorkspaceNodeLabel> NodeLabels { get; }

	/// <summary>Copied system edge labels.</summary>
	public IReadOnlyList<WorkspaceSystemEdgeLabel> SystemEdgeLabels { get; }

	/// <summary>The derived navigation preview, including synthetic Groups headers.</summary>
	public IReadOnlyList<PluralMapPreviewRow> HierarchyPreview { get; }

	/// <summary>Membership rows for system, group, and place nodes in this build.</summary>
	public IReadOnlyList<WorkspaceAccessFact> AccessFacts { get; }

	/// <summary>
	/// One visibility byte and one permission byte for every system, group, and place in this build.
	/// </summary>
	/// <remarks>
	/// Missing, unset, inherit, and explicit stay distinct. Repeated occurrences share the account-owned record.
	/// These values do not authorize access.
	/// </remarks>
	public IReadOnlyList<WorkspaceAnchorSetting> AnchorSettings { get; }

	/// <summary>
	/// A conservative retained-size estimate, in bytes.
	/// </summary>
	/// <remarks>
	/// Each string is counted as a 24-byte object header plus two bytes per character.
	/// Each row adds a fixed header. This is an upper-leaning accounting figure, not a measured GC size,
	/// and it does not include allocator fragmentation. Retained avatar bytes are not part of this figure.
	/// </remarks>
	public long EstimatedBytes { get; }

	/// <summary>
	/// Retains the image a load returned for one member of this context.
	/// </summary>
	/// <param name="memberId">The member id. Every occurrence of this member in this context shares one entry.</param>
	/// <param name="loaded">The result of the common avatar load. Null means the member has no image.</param>
	/// <returns>The retained entry. Null when <paramref name="loaded"/> is null. A member that already has an entry keeps that entry.</returns>
	/// <remarks>
	/// The byte array is stored as returned. A missing image does not create an entry and does not remove one.
	/// This method does not authorize the member, and it does not read or write another context.
	/// </remarks>
	internal WorkspaceRetainedAvatar? RetainAvatar(Guid memberId, (byte[] Bytes, string ContentType)? loaded)
	{
		if (loaded is null)
		{
			return null;
		}

		lock (_avatarGate)
		{
			if (_avatars.TryGetValue(memberId, out var existing))
			{
				return existing;
			}

			var retained = new WorkspaceRetainedAvatar(memberId, loaded.Value.Bytes, loaded.Value.ContentType);
			_avatars.Add(memberId, retained);
			return retained;
		}
	}

	/// <summary>
	/// Returns the image this context has already retained for the member.
	/// </summary>
	/// <param name="memberId">The member id.</param>
	/// <returns>The retained entry, or null when this context has no image for that member.</returns>
	/// <remarks>A miss is not an authorization result. An entry retained by another context is not visible here.</remarks>
	internal WorkspaceRetainedAvatar? FindAvatar(Guid memberId)
	{
		lock (_avatarGate)
		{
			return _avatars.TryGetValue(memberId, out var retained) ? retained : null;
		}
	}

	/// <summary>Copies the captured rows into read-only lists and assigns a new context id.</summary>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="edges">Graph edges.</param>
	/// <param name="occurrences">Walk occurrences.</param>
	/// <param name="nodeLabels">Node labels.</param>
	/// <param name="systemEdgeLabels">System edge labels.</param>
	/// <param name="hierarchyPreview">Derived navigation rows.</param>
	/// <param name="accessFacts">Membership facts. The zero guid is omitted.</param>
	/// <param name="anchorSettingRows">Stored settings for this account. An anchor with no row is retained as missing.</param>
	/// <returns>The immutable context.</returns>
	public static WorkspaceContext Create(
		Guid accountId,
		IReadOnlyList<WorkspaceGraphEdge> edges,
		IReadOnlyList<WorkspaceOccurrence> occurrences,
		IReadOnlyList<WorkspaceNodeLabel> nodeLabels,
		IReadOnlyList<WorkspaceSystemEdgeLabel> systemEdgeLabels,
		IReadOnlyList<PluralMapPreviewRow> hierarchyPreview,
		IReadOnlyList<WorkspaceAccessFact> accessFacts,
		IReadOnlyList<WorkspaceStoredAnchorSetting>? anchorSettingRows = null)
	{
		var contextId = Guid.NewGuid().ToString("N");
		var copiedFacts = accessFacts.Where(fact => fact.ObjectId != Guid.Empty).ToArray();
		var anchorSettings = WorkspaceAnchorSettings.Retain(
			nodeLabels,
			occurrences,
			hierarchyPreview,
			anchorSettingRows ?? []);
		var context = new WorkspaceContext(
			contextId,
			accountId,
			Freeze(edges),
			Freeze(occurrences),
			Freeze(nodeLabels),
			Freeze(systemEdgeLabels),
			Freeze(hierarchyPreview),
			Freeze(copiedFacts),
			Freeze(anchorSettings),
			0);
		return new WorkspaceContext(
			context.ContextId,
			context.AccountId,
			context.Edges,
			context.Occurrences,
			context.NodeLabels,
			context.SystemEdgeLabels,
			context.HierarchyPreview,
			context.AccessFacts,
			context.AnchorSettings,
			Estimate(context));
	}

	/// <summary>True when any retained fact for that object has status name Active. This is the seed's status check, not the profile rule.</summary>
	/// <param name="objectType">The anchor type.</param>
	/// <param name="objectId">The anchor id.</param>
	/// <returns>True when a matching Active status name was captured.</returns>
	public bool HasSeedActiveStatus(string objectType, Guid objectId)
	{
		return AccessFacts.Any(fact => SameObject(fact, objectType, objectId)
			&& string.Equals(fact.StatusName, "Active", StringComparison.Ordinal));
	}

	/// <summary>
	/// True when the captured rows contain exactly one membership and that row is Active and IsActive.
	/// </summary>
	/// <param name="objectType">The anchor type.</param>
	/// <param name="objectId">The anchor id.</param>
	/// <returns>True for the profile procedure's single active-membership shape. This does not authorize a request.</returns>
	public bool HasSingleProfileActiveMembership(string objectType, Guid objectId)
	{
		var matches = AccessFacts.Where(fact => SameObject(fact, objectType, objectId)).ToArray();
		return matches.Length == 1
			&& string.Equals(matches[0].StatusName, "Active", StringComparison.Ordinal)
			&& matches[0].IsActive == true;
	}

	/// <summary>True when more than one membership row was captured for the object.</summary>
	/// <param name="objectType">The anchor type.</param>
	/// <param name="objectId">The anchor id.</param>
	/// <returns>True when duplicate memberships were captured.</returns>
	public bool HasDuplicateMembership(string objectType, Guid objectId)
	{
		return AccessFacts.Count(fact => SameObject(fact, objectType, objectId)) > 1;
	}

	/// <summary>
	/// Derives the profile-procedure arguments for one anchor occurrence in this opening.
	/// </summary>
	/// <param name="pathKey">The occurrence path. A blank path, a header, a member, or a row without an id or name is rejected.</param>
	/// <param name="detail">The derived anchor, branch, and the first page of direct member ids. Empty when the method returns false.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when this context contains that anchor occurrence. This does not authorize the account.</returns>
	/// <remarks>
	/// The branch walks parent paths the same way the browser does, and stops at a repeated path, a missing parent, or 32 nodes.
	/// A Groups header has no object id, so the walk stops before adding it. Direct members are children whose type is member.
	/// This overload returns the first page. Retained access facts are not read here.
	/// </remarks>
	public bool TryPrepareAnchorDetail(string? pathKey, out WorkspaceAnchorDetail detail, out string error)
	{
		return TryPrepareAnchorDetail(pathKey, 0, out detail, out error);
	}

	/// <summary>
	/// Derives the profile-procedure arguments and one page of direct members for an anchor occurrence.
	/// </summary>
	/// <param name="pathKey">The occurrence path. A blank path, a header, a member, or a row without an id or name is rejected.</param>
	/// <param name="skip">Unique direct members to pass over before the page. Zero selects the first page. Negative is rejected.</param>
	/// <param name="detail">The derived anchor, branch, and member page. Empty when the method returns false.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when this context contains that anchor occurrence. This does not authorize the account.</returns>
	/// <remarks>
	/// Direct members are children whose type is member, in preview order. A repeated member id is kept once, at its first row, before skip is applied.
	/// The page contains at most <see cref="DetailBatchSize"/> ids. A skip past the last unique member leaves the page empty.
	/// Members under another parent path are not included. Retained access facts are not read here.
	/// </remarks>
	public bool TryPrepareAnchorDetail(string? pathKey, int skip, out WorkspaceAnchorDetail detail, out string error)
	{
		detail = default;
		if (string.IsNullOrWhiteSpace(pathKey))
		{
			error = "The selected occurrence is required.";
			return false;
		}

		PluralMapPreviewRow? match = null;
		foreach (var row in HierarchyPreview)
		{
			if (string.Equals(row.PathKey, pathKey, StringComparison.Ordinal))
			{
				match = row;
				break;
			}
		}

		if (match is null)
		{
			error = "The selected occurrence is not in this workspace.";
			return false;
		}

		var rowMatch = match;
		if (rowMatch.Header
			|| rowMatch.ObjectType is not ("system" or "group" or "place")
			|| rowMatch.ObjectId is not Guid objectId
			|| objectId == Guid.Empty)
		{
			error = "The selected occurrence is not an anchor.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(rowMatch.AnchorName) || rowMatch.AnchorName.Length > 510)
		{
			error = "The selected anchor has no stored name.";
			return false;
		}

		if (skip < 0)
		{
			error = "The member batch skip must be zero or greater.";
			return false;
		}

		var members = new List<Guid>();
		var seen = new HashSet<Guid>();
		foreach (var row in HierarchyPreview)
		{
			if (!string.Equals(row.ParentPathKey, pathKey, StringComparison.Ordinal)
				|| !string.Equals(row.ObjectType, "member", StringComparison.Ordinal)
				|| row.ObjectId is not Guid memberId
				|| memberId == Guid.Empty
				|| !seen.Add(memberId))
			{
				continue;
			}

			members.Add(memberId);
		}

		var page = new List<Guid>(DetailBatchSize);
		for (var index = skip; index < members.Count && page.Count < DetailBatchSize; index++)
		{
			page.Add(members[index]);
		}

		detail = new WorkspaceAnchorDetail(
			rowMatch.ObjectType,
			objectId,
			rowMatch.AnchorName,
			BuildBranch(rowMatch),
			page);
		error = "";
		return true;
	}

	/// <summary>Builds the profile branch from this row through its parents.</summary>
	/// <param name="start">The selected anchor row.</param>
	/// <returns>Comma-separated type:id pairs. A row without a type or id ends the walk.</returns>
	private string BuildBranch(PluralMapPreviewRow start)
	{
		var chain = new List<string>(8);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var current = start;
		while (chain.Count < 32)
		{
			if (current.ObjectType is not ("system" or "group" or "place") || current.ObjectId is not Guid objectId || objectId == Guid.Empty)
			{
				break;
			}

			if (!string.IsNullOrEmpty(current.PathKey) && !seen.Add(current.PathKey))
			{
				break;
			}

			chain.Add(current.ObjectType + ":" + objectId.ToString("D"));
			if (string.IsNullOrEmpty(current.ParentPathKey))
			{
				break;
			}

			PluralMapPreviewRow? parent = null;
			foreach (var row in HierarchyPreview)
			{
				if (string.Equals(row.PathKey, current.ParentPathKey, StringComparison.Ordinal))
				{
					parent = row;
					break;
				}
			}

			if (parent is null)
			{
				break;
			}

			current = parent;
		}

		return string.Join(',', chain);
	}

	/// <summary>
	/// Resolves a member placement from this account's retained tree.
	/// </summary>
	/// <param name="occurrencePath">The selected anchor occurrence. The path identifies the branch.</param>
	/// <param name="memberVisibility">The member's stored visibility byte, or null when that side has no placement value.</param>
	/// <param name="memberPermission">The member's stored permission byte, or null when that side has no placement value.</param>
	/// <param name="visibility">The resolved visibility byte. Zero when the method returns false.</param>
	/// <param name="permission">The resolved permission byte. Zero when the method returns false.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when both sides resolved from this context.</returns>
	/// <remarks>
	/// <para>
	/// A non-null member byte is the result for that side and is not walked. Null starts at the selected occurrence.
	/// The anchor byte is read from <see cref="AnchorSettings"/>. Inherit (2) follows that occurrence's parent path.
	/// The first other byte stops the side. Stored unset (1) stops. Missing (0) stops and resolves as unset (1); it is not inherit.
	/// A top anchor that is still inherit resolves as inherit (2). Explicit bytes are returned unchanged.
	/// </para>
	/// <para>
	/// Visibility and permission walk separately and stop at different ancestors. The parent step uses <see cref="PluralMapPreviewRow.ParentPathKey"/>
	/// for the current occurrence. A structural header is skipped and does not end the chain. The parent anchor is not selected by anchor id.
	/// The occurrence's own parent type and id must name that parent anchor. A repeated path, a missing parent, or more than one row for a path is rejected.
	/// This method does not write the context, cache a resolved byte by anchor id, read the database, or authorize access.
	/// Another account's settings are not in this context and are not consulted.
	/// </para>
	/// </remarks>
	public bool TryResolvePlacement(
		string? occurrencePath,
		byte? memberVisibility,
		byte? memberPermission,
		out byte visibility,
		out byte permission,
		out string error)
	{
		visibility = 0;
		permission = 0;
		if (!TrySelectedAnchor(occurrencePath, out var start, out var startOccurrence, out error)
			|| start is null
			|| startOccurrence is null)
		{
			return false;
		}

		if (memberVisibility is byte storedVisibility)
		{
			visibility = storedVisibility;
		}
		else if (!TryWalkPlacement(start, startOccurrence, visibilitySide: true, out visibility, out error))
		{
			visibility = 0;
			permission = 0;
			return false;
		}

		if (memberPermission is byte storedPermission)
		{
			permission = storedPermission;
		}
		else if (!TryWalkPlacement(start, startOccurrence, visibilitySide: false, out permission, out error))
		{
			visibility = 0;
			permission = 0;
			return false;
		}

		error = "";
		return true;
	}

	/// <summary>Finds the one preview row and matching occurrence for the selected path.</summary>
	/// <param name="occurrencePath">The caller-selected path.</param>
	/// <param name="row">The anchor preview row. Null when the method returns false.</param>
	/// <param name="occurrence">The occurrence with that path. Null when the method returns false.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when the path is one anchor occurrence in this context.</returns>
	private bool TrySelectedAnchor(
		string? occurrencePath,
		out PluralMapPreviewRow? row,
		out WorkspaceOccurrence? occurrence,
		out string error)
	{
		row = null;
		occurrence = null;
		if (string.IsNullOrWhiteSpace(occurrencePath))
		{
			error = "The selected occurrence is required.";
			return false;
		}

		if (!TrySinglePreview(occurrencePath, out row, out error) || row is null)
		{
			return false;
		}

		if (row.Header
			|| row.ObjectType is not ("system" or "group" or "place")
			|| row.ObjectId is not Guid objectId
			|| objectId == Guid.Empty)
		{
			row = null;
			error = "The selected occurrence is not an anchor.";
			return false;
		}

		if (!TrySingleOccurrence(occurrencePath, out occurrence, out error) || occurrence is null)
		{
			row = null;
			return false;
		}

		if (!string.Equals(occurrence.ObjectType, row.ObjectType, StringComparison.Ordinal)
			|| occurrence.ObjectId != objectId)
		{
			row = null;
			occurrence = null;
			error = "The selected occurrence parent chain is broken.";
			return false;
		}

		error = "";
		return true;
	}

	/// <summary>
	/// Walks one side from the selected anchor until a non-inherit byte or the top of this branch.
	/// </summary>
	/// <param name="start">The selected anchor preview row.</param>
	/// <param name="startOccurrence">The occurrence for <paramref name="start"/>.</param>
	/// <param name="visibilitySide">True reads visibility. False reads permission.</param>
	/// <param name="resolved">The stopping byte.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when the branch resolved. False for a broken or repeating chain.</returns>
	private bool TryWalkPlacement(
		PluralMapPreviewRow start,
		WorkspaceOccurrence startOccurrence,
		bool visibilitySide,
		out byte resolved,
		out string error)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var current = start;
		var currentOccurrence = startOccurrence;
		for (var step = 0; step < 32; step++)
		{
			if (string.IsNullOrEmpty(current.PathKey) || !seen.Add(current.PathKey))
			{
				resolved = 0;
				error = "The selected occurrence parent chain repeats.";
				return false;
			}

			if (!TryAnchorByte(current, visibilitySide, out var stored, out error))
			{
				resolved = 0;
				return false;
			}

			if (stored == WorkspaceAnchorSettingCodes.Missing)
			{
				resolved = WorkspaceAnchorSettingCodes.Unset;
				error = "";
				return true;
			}

			if (stored != WorkspaceAnchorSettingCodes.Inherit)
			{
				resolved = stored;
				error = "";
				return true;
			}

			if (!TryParentAnchor(current, seen, out var parent, out error))
			{
				resolved = 0;
				return false;
			}

			if (parent is null)
			{
				if (currentOccurrence.ParentObjectType is "system" or "group" or "place")
				{
					resolved = 0;
					error = "The selected occurrence parent chain is broken.";
					return false;
				}

				resolved = WorkspaceAnchorSettingCodes.Inherit;
				error = "";
				return true;
			}

			if (!string.Equals(currentOccurrence.ParentObjectType, parent.ObjectType, StringComparison.Ordinal)
				|| currentOccurrence.ParentObjectId != parent.ObjectId
				|| string.IsNullOrEmpty(parent.PathKey)
				|| !TrySingleOccurrence(parent.PathKey, out var parentOccurrence, out error)
				|| parentOccurrence is null
				|| !string.Equals(parentOccurrence.ObjectType, parent.ObjectType, StringComparison.Ordinal)
				|| parentOccurrence.ObjectId != parent.ObjectId)
			{
				resolved = 0;
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			current = parent;
			currentOccurrence = parentOccurrence;
		}

		resolved = 0;
		error = "The selected occurrence parent chain is too long.";
		return false;
	}

	/// <summary>Reads the account-owned byte for one anchor. Two rows for the same anchor are rejected.</summary>
	/// <param name="row">The anchor preview row.</param>
	/// <param name="visibilitySide">True reads visibility. False reads permission.</param>
	/// <param name="stored">The retained byte, or missing when this anchor has no settings row.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when zero or one settings row matched.</returns>
	private bool TryAnchorByte(PluralMapPreviewRow row, bool visibilitySide, out byte stored, out string error)
	{
		stored = WorkspaceAnchorSettingCodes.Missing;
		WorkspaceAnchorSetting? found = null;
		foreach (var setting in AnchorSettings)
		{
			if (!string.Equals(setting.ObjectType, row.ObjectType, StringComparison.Ordinal) || setting.ObjectId != row.ObjectId)
			{
				continue;
			}

			if (found is not null)
			{
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			found = setting;
		}

		if (found is not null)
		{
			stored = visibilitySide ? found.Visibility : found.Permission;
		}

		error = "";
		return true;
	}

	/// <summary>
	/// Finds the parent anchor of this occurrence by parent path. Headers are passed through.
	/// </summary>
	/// <param name="current">The anchor whose parent is required.</param>
	/// <param name="seen">Paths already entered on this walk, including headers skipped by this step.</param>
	/// <param name="parent">The parent anchor. Null when this anchor is the top of the branch.</param>
	/// <param name="error">The rejection text. Empty when the method returns true.</param>
	/// <returns>True when the parent path is a single chain. False when a path is missing or repeats.</returns>
	private bool TryParentAnchor(
		PluralMapPreviewRow current,
		HashSet<string> seen,
		out PluralMapPreviewRow? parent,
		out string error)
	{
		parent = null;
		var path = current.ParentPathKey;
		while (!string.IsNullOrEmpty(path))
		{
			if (!TrySinglePreview(path, out var row, out error) || row is null)
			{
				parent = null;
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			if (row.Header)
			{
				if (string.IsNullOrEmpty(row.PathKey) || !seen.Add(row.PathKey))
				{
					error = "The selected occurrence parent chain repeats.";
					return false;
				}

				path = row.ParentPathKey;
				continue;
			}

			if (row.ObjectType is not ("system" or "group" or "place") || row.ObjectId is not Guid || row.ObjectId == Guid.Empty)
			{
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			parent = row;
			error = "";
			return true;
		}

		error = "";
		return true;
	}

	/// <summary>Returns the one preview row with this path.</summary>
	/// <param name="pathKey">The occurrence path.</param>
	/// <param name="match">The row. Null when the method returns false.</param>
	/// <param name="error">Not-in-workspace when none match, or a broken chain when several match.</param>
	/// <returns>True when exactly one row has the path.</returns>
	private bool TrySinglePreview(string pathKey, out PluralMapPreviewRow? match, out string error)
	{
		match = null;
		foreach (var row in HierarchyPreview)
		{
			if (!string.Equals(row.PathKey, pathKey, StringComparison.Ordinal))
			{
				continue;
			}

			if (match is not null)
			{
				match = null;
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			match = row;
		}

		if (match is null)
		{
			error = "The selected occurrence is not in this workspace.";
			return false;
		}

		error = "";
		return true;
	}

	/// <summary>Returns the one occurrence with this path.</summary>
	/// <param name="pathKey">The occurrence path.</param>
	/// <param name="match">The occurrence. Null when the method returns false.</param>
	/// <param name="error">Not-in-workspace when none match, or a broken chain when several match.</param>
	/// <returns>True when exactly one occurrence has the path.</returns>
	private bool TrySingleOccurrence(string pathKey, out WorkspaceOccurrence? match, out string error)
	{
		match = null;
		foreach (var occurrence in Occurrences)
		{
			if (!string.Equals(occurrence.PathKey, pathKey, StringComparison.Ordinal))
			{
				continue;
			}

			if (match is not null)
			{
				match = null;
				error = "The selected occurrence parent chain is broken.";
				return false;
			}

			match = occurrence;
		}

		if (match is null)
		{
			error = "The selected occurrence is not in this workspace.";
			return false;
		}

		error = "";
		return true;
	}

	private static bool SameObject(WorkspaceAccessFact fact, string objectType, Guid objectId)
	{
		return fact.ObjectId == objectId
			&& string.Equals(fact.ObjectType, objectType, StringComparison.Ordinal);
	}

	private static ReadOnlyCollection<T> Freeze<T>(IReadOnlyList<T> values)
	{
		return new ReadOnlyCollection<T>(values.ToArray());
	}

	/// <summary>
	/// Conservative size used for the process budget. It is a string-header and row estimate, not a measured heap size.
	/// </summary>
	/// <param name="context">The context being sized.</param>
	/// <returns>The estimated bytes.</returns>
	private static long Estimate(WorkspaceContext context)
	{
		long bytes = 128;
		bytes += Text(context.ContextId);
		foreach (var edge in context.Edges)
		{
			bytes += 96 + Text(edge.ParentObjectType) + Text(edge.ChildObjectType) + Text(edge.EdgeSource);
		}

		foreach (var occurrence in context.Occurrences)
		{
			bytes += 96 + Text(occurrence.ParentObjectType) + Text(occurrence.ObjectType) + Text(occurrence.PathKey);
		}

		foreach (var label in context.NodeLabels)
		{
			bytes += 64 + Text(label.ObjectType) + Text(label.NodeLabel);
		}

		foreach (var label in context.SystemEdgeLabels)
		{
			bytes += 48 + Text(label.EdgeLabel);
		}

		foreach (var row in context.HierarchyPreview)
		{
			bytes += 128
				+ Text(row.Section)
				+ Text(row.TreeLine)
				+ Text(row.PathKey)
				+ Text(row.NodeLabel)
				+ Text(row.ObjectType)
				+ Text(row.ParentPathKey)
				+ Text(row.AnchorName);
		}

		foreach (var fact in context.AccessFacts)
		{
			bytes += 64 + Text(fact.ObjectType) + Text(fact.StatusName);
		}

		foreach (var setting in context.AnchorSettings)
		{
			bytes += 48 + Text(setting.ObjectType);
		}

		return bytes;
	}

	/// <summary>Estimates one string as a 24-byte header plus two bytes for each character.</summary>
	/// <param name="value">The captured text. Null adds nothing.</param>
	/// <returns>The estimated bytes.</returns>
	private static long Text(string? value)
	{
		return value == null ? 0 : 24L + (value.Length * 2L);
	}
}

/// <summary>The navigation body returned to the browser.</summary>
/// <param name="ContextId">The new opaque context id.</param>
/// <param name="AccountId">The authenticated account.</param>
/// <param name="HierarchyPreview">The derived tree, including synthetic Groups headers.</param>
internal sealed record WorkspaceTreeResponse(
	string ContextId,
	Guid AccountId,
	IReadOnlyList<PluralMapPreviewRow> HierarchyPreview);

/// <summary>A lookup that either returns the immutable copy or the shared unavailable result.</summary>
/// <param name="Available">True when the account matches a context that is still stored.</param>
/// <param name="Result">available, or <see cref="WorkspaceContextResults.Unavailable"/>.</param>
/// <param name="Context">The immutable copy when available.</param>
internal readonly record struct WorkspaceContextAcquisition(bool Available, string Result, WorkspaceContext? Context)
{
	/// <summary>The same unavailable result for every failed lookup.</summary>
	/// <returns>An unavailable acquisition.</returns>
	public static WorkspaceContextAcquisition Unavailable()
	{
		return new WorkspaceContextAcquisition(false, WorkspaceContextResults.Unavailable, null);
	}

	/// <summary>A successful account-bound lookup.</summary>
	/// <param name="context">The retained copy.</param>
	/// <returns>An available acquisition.</returns>
	public static WorkspaceContextAcquisition Found(WorkspaceContext context)
	{
		return new WorkspaceContextAcquisition(true, "available", context);
	}
}

/// <summary>The opening build was cancelled or failed before a context was published.</summary>
/// <remarks>
/// Snapshot, render-contract, and budget failures derive from this type.
/// Callers can catch the base when any opening failure should stay out of the store.
/// </remarks>
internal class WorkspaceOpeningException : Exception
{
	/// <summary>Creates the failure.</summary>
	/// <param name="message">A message that does not include connection text or row content.</param>
	public WorkspaceOpeningException(string message)
		: base(message)
	{
	}
}

/// <summary>SNAPSHOT isolation could not be started. The build does not continue under another isolation level.</summary>
internal sealed class WorkspaceSnapshotUnavailableException : WorkspaceOpeningException
{
	/// <summary>Creates the isolation failure.</summary>
	public WorkspaceSnapshotUnavailableException()
		: base("Snapshot isolation is not available. The workspace opening build was not retried.")
	{
	}
}

/// <summary>The render result did not match the captured column contract.</summary>
internal sealed class WorkspaceRenderContractException : WorkspaceOpeningException
{
	/// <summary>Creates the contract failure.</summary>
	public WorkspaceRenderContractException()
		: base("The workspace tree render result did not match the captured contract.")
	{
	}
}

/// <summary>One context is larger than the whole process budget.</summary>
internal sealed class WorkspaceContextTooLargeException : WorkspaceOpeningException
{
	/// <summary>Creates the budget failure.</summary>
	/// <param name="estimatedBytes">The rejected estimate.</param>
	/// <param name="budgetBytes">The process budget.</param>
	public WorkspaceContextTooLargeException(long estimatedBytes, long budgetBytes)
		: base("The workspace context estimate of " + estimatedBytes.ToString() + " bytes exceeds the " + budgetBytes.ToString() + " byte budget.")
	{
	}
}
