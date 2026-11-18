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
/// The immutable context retained for one opening build.
/// </summary>
/// <remarks>
/// Later detail and image requests must not treat this copy as current authorization until a freshness contract is approved.
/// Database freshness detection is not part of this object. Profile text, fronting, and image bytes are not stored.
/// </remarks>
internal sealed class WorkspaceContext
{
	private WorkspaceContext(
		string contextId,
		Guid accountId,
		IReadOnlyList<WorkspaceGraphEdge> edges,
		IReadOnlyList<WorkspaceOccurrence> occurrences,
		IReadOnlyList<WorkspaceNodeLabel> nodeLabels,
		IReadOnlyList<WorkspaceSystemEdgeLabel> systemEdgeLabels,
		IReadOnlyList<PluralMapPreviewRow> hierarchyPreview,
		IReadOnlyList<WorkspaceAccessFact> accessFacts,
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
		EstimatedBytes = estimatedBytes;
	}

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
	/// A conservative retained-size estimate, in bytes.
	/// </summary>
	/// <remarks>
	/// Each string is counted as a 24-byte object header plus two bytes per character.
	/// Each row adds a fixed header. This is an upper-leaning accounting figure, not a measured GC size,
	/// and it does not include allocator fragmentation.
	/// </remarks>
	public long EstimatedBytes { get; }

	/// <summary>Copies the captured rows into read-only lists and assigns a new context id.</summary>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="edges">Graph edges.</param>
	/// <param name="occurrences">Walk occurrences.</param>
	/// <param name="nodeLabels">Node labels.</param>
	/// <param name="systemEdgeLabels">System edge labels.</param>
	/// <param name="hierarchyPreview">Derived navigation rows.</param>
	/// <param name="accessFacts">Membership facts. The zero guid is omitted.</param>
	/// <returns>The immutable context.</returns>
	public static WorkspaceContext Create(
		Guid accountId,
		IReadOnlyList<WorkspaceGraphEdge> edges,
		IReadOnlyList<WorkspaceOccurrence> occurrences,
		IReadOnlyList<WorkspaceNodeLabel> nodeLabels,
		IReadOnlyList<WorkspaceSystemEdgeLabel> systemEdgeLabels,
		IReadOnlyList<PluralMapPreviewRow> hierarchyPreview,
		IReadOnlyList<WorkspaceAccessFact> accessFacts)
	{
		var contextId = Guid.NewGuid().ToString("N");
		var copiedFacts = accessFacts.Where(fact => fact.ObjectId != Guid.Empty).ToArray();
		var context = new WorkspaceContext(
			contextId,
			accountId,
			Freeze(edges),
			Freeze(occurrences),
			Freeze(nodeLabels),
			Freeze(systemEdgeLabels),
			Freeze(hierarchyPreview),
			Freeze(copiedFacts),
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
