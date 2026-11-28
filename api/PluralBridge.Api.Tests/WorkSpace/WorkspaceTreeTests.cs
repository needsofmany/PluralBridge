// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Claims;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Focused checks for the workspace opening plan, context store, and browser state transitions.
/// </summary>
/// <remarks>
/// These tests do not open a database connection. The SQL session is replaced by a fake.
/// A passing store test shows the accounting for that case. It does not prove live render parity.
/// </remarks>
public sealed class WorkspaceTreeTests
{
	/// <summary>The opening plan uses snapshot isolation, one installed procedure sequence, and does not call step 4.</summary>
	[Fact]
	public void OpeningPlan_UsesSnapshotAndOneProcedureSequence()
	{
		Assert.Equal(System.Data.IsolationLevel.Snapshot, WorkspaceOpeningPlan.Isolation);
		var procedures = WorkspaceOpeningPlan.Commands
			.Where(command => command.Kind == WorkspaceOpeningCommandKind.StoredProcedure)
			.Select(command => command.Text)
			.ToArray();
		Assert.Equal(
			[
				WorkspaceOpeningPlan.SeedProcedure,
				WorkspaceOpeningPlan.WalkerProcedure,
				WorkspaceOpeningPlan.ExpansionProcedure,
				WorkspaceOpeningPlan.LabelerProcedure
			],
			procedures);
		Assert.Equal(
			WorkspaceOpeningPlan.RenderProcedure,
			Assert.Single(WorkspaceOpeningPlan.Commands, command => command.Kind == WorkspaceOpeningCommandKind.ReadRender).Text);
		Assert.DoesNotContain(
			WorkspaceOpeningPlan.Commands,
			command => command.Text.Contains(WorkspaceOpeningPlan.Step4Procedure, StringComparison.Ordinal));
		Assert.Equal(WorkspaceOpeningCommandKind.ReadAnchorNames, WorkspaceOpeningPlan.Commands[^2].Kind);
		Assert.Equal(WorkspaceOpeningCommandKind.Commit, WorkspaceOpeningPlan.Commands[^1].Kind);
	}

	/// <summary>Render rows map to the preview contract, including the synthetic Groups header.</summary>
	[Fact]
	public void RenderOutput_MapsTheCapturedContractAndGroupsHeader()
	{
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var path = "30|000001|system|" + systemId.ToString("D");
		var header = path + ".15|000000|groups|00000000-0000-0000-0000-000000000000";
		var output = PluralMapReader.MapRenderOutput(
		[
			Set(
				[Column("Section", "nvarchar"), Column("ObjectType", "nvarchar"), Column("NodeCount", "int")],
				[new object[] { "counts", "system", 1 }]),
			Set(
				[
					Column("Section", "nvarchar"),
					Column("ParentObjectType", "nvarchar"),
					Column("ParentObjectId", "uniqueidentifier"),
					Column("ChildObjectType", "nvarchar"),
					Column("ChildObjectId", "uniqueidentifier"),
					Column("EdgeRank", "int"),
					Column("EdgeSource", "nvarchar")
				],
				[new object[] { "edges", "system", systemId, "member", Guid.NewGuid(), 1, "placement" }]),
			Set(
				[Column("Section", "nvarchar"), Column("TreeLine", "nvarchar"), Column("PathKey", "nvarchar")],
				[
					new object[] { "preview", "\u2514\u2500\u2500 System: Harbor", path },
					new object[] { "preview", "    \u2514\u2500\u2500 Groups", header }
				])
		]);

		Assert.NotNull(output);
		Assert.Equal(systemId, Assert.Single(output.GraphEdges).ParentObjectId);
		Assert.False(output.HierarchyPreview[0].Header);
		Assert.True(output.HierarchyPreview[1].Header);
		Assert.Equal(Guid.Empty, output.HierarchyPreview[1].ObjectId);
		Assert.Null(PluralMapReader.MapRenderOutput([]));
	}

	/// <summary>The context keeps membership identity and does not treat the Groups zero guid as an access object.</summary>
	[Fact]
	public void Context_PreservesIdentitiesAndOmitsTheZeroGuid()
	{
		var accountId = Guid.NewGuid();
		var membershipId = Guid.NewGuid();
		var anchorId = Guid.NewGuid();
		var context = WorkspaceContext.Create(
			accountId,
			[new WorkspaceGraphEdge("system", anchorId, "member", Guid.NewGuid(), 1, "placement")],
			[new WorkspaceOccurrence("account", accountId, "system", anchorId, 0, "path-key")],
			[new WorkspaceNodeLabel("system", anchorId, "Harbor")],
			[new WorkspaceSystemEdgeLabel(anchorId, "west")],
			[],
			[
				new WorkspaceAccessFact(membershipId, "system", anchorId, 1, "Active", true),
				new WorkspaceAccessFact(Guid.NewGuid(), "system", anchorId, 2, "Inactive", false),
				new WorkspaceAccessFact(Guid.NewGuid(), "groups", Guid.Empty, 1, "Active", true)
			]);

		Assert.Equal("path-key", Assert.Single(context.Occurrences).PathKey);
		Assert.Equal(membershipId, context.AccessFacts[0].ObjectMembershipId);
		Assert.True(context.HasSeedActiveStatus("system", anchorId));
		Assert.True(context.HasDuplicateMembership("system", anchorId));
		Assert.False(context.HasSingleProfileActiveMembership("system", anchorId));
		Assert.DoesNotContain(context.AccessFacts, fact => fact.ObjectId == Guid.Empty);
		Assert.Throws<NotSupportedException>(() => ((IList<WorkspaceOccurrence>)context.Occurrences).Add(
			new WorkspaceOccurrence("account", accountId, "system", anchorId, 0, "other")));
	}

	/// <summary>Lookup and release succeed only for the account that owns the context.</summary>
	[Fact]
	public void Store_BindsLookupAndReleaseToTheAccount()
	{
		var store = NewStore(out _, out var settings);
		var accountId = Guid.NewGuid();
		var context = store.Publish(Sample(accountId));
		var acquired = store.Acquire(accountId, context.ContextId);
		Assert.True(acquired.Available);
		Assert.Same(context, acquired.Context);

		var other = store.Acquire(Guid.NewGuid(), context.ContextId);
		Assert.False(other.Available);
		Assert.Equal(WorkspaceContextResults.Unavailable, other.Result);
		Assert.False(store.Release(Guid.NewGuid(), context.ContextId));
		Assert.True(store.Acquire(accountId, context.ContextId).Available);
		Assert.True(store.Release(accountId, context.ContextId));
		Assert.False(store.Release(accountId, context.ContextId));
		Assert.Equal(WorkspaceContextResults.Unavailable, store.Acquire(accountId, context.ContextId).Result);
	}

	/// <summary>Two openings remain separate. Releasing one does not remove the other.</summary>
	[Fact]
	public void Store_KeepsIndependentContexts()
	{
		var store = NewStore(out _, out _);
		var accountId = Guid.NewGuid();
		var first = store.Publish(Sample(accountId, "one"));
		var second = store.Publish(Sample(accountId, "two"));
		Assert.True(store.Release(accountId, first.ContextId));
		Assert.False(store.Acquire(accountId, first.ContextId).Available);
		Assert.True(store.Acquire(accountId, second.ContextId).Available);
	}

	/// <summary>Idle time removes a context, and a successful use before that deadline renews it.</summary>
	[Fact]
	public void Store_ExpiresByIdleTimeAndRenewsOnUse()
	{
		var store = NewStore(out var clock, out var settings);
		settings.IdleTimeout = TimeSpan.FromTicks(10);
		var context = store.Publish(Sample(Guid.NewGuid()));
		clock.Advance(TimeSpan.FromTicks(9));
		Assert.True(store.Acquire(context.AccountId, context.ContextId).Available);
		clock.Advance(TimeSpan.FromTicks(9));
		Assert.True(store.Acquire(context.AccountId, context.ContextId).Available);
		clock.Advance(TimeSpan.FromTicks(10));
		Assert.Equal(WorkspaceContextResults.Unavailable, store.Acquire(context.AccountId, context.ContextId).Result);
	}

	/// <summary>Raising the idle timeout after expiration does not make the removed context available again.</summary>
	[Fact]
	public void Store_DoesNotReviveAnExpiredContextWhenTheTimeoutGrows()
	{
		var store = NewStore(out var clock, out var settings);
		settings.IdleTimeout = TimeSpan.FromTicks(10);
		var context = store.Publish(Sample(Guid.NewGuid()));
		clock.Advance(TimeSpan.FromTicks(10));
		Assert.False(store.Acquire(context.AccountId, context.ContextId).Available);
		settings.IdleTimeout = TimeSpan.FromTicks(100);
		Assert.Equal(WorkspaceContextResults.Unavailable, store.Acquire(context.AccountId, context.ContextId).Result);
		Assert.Equal(0, store.Count);
	}

	/// <summary>A shorter timeout expires an existing context at the new deadline. It does not lengthen one already stored.</summary>
	[Fact]
	public void Store_AppliesAShorterTimeoutWithoutExtendingExistingContexts()
	{
		var store = NewStore(out var clock, out var settings);
		settings.IdleTimeout = TimeSpan.FromTicks(30);
		var context = store.Publish(Sample(Guid.NewGuid()));
		settings.IdleTimeout = TimeSpan.FromTicks(5);
		clock.Advance(TimeSpan.FromTicks(5));
		Assert.False(store.Acquire(context.AccountId, context.ContextId).Available);
	}

	/// <summary>One context larger than the budget is rejected. Reclamation removes expired contexts before least-recently-used ones.</summary>
	[Fact]
	public void Store_RejectsAContextLargerThanTheBudgetAndEvictsExpiredThenLeastRecentlyUsed()
	{
		var store = NewStore(out var clock, out var settings);
		var accountId = Guid.NewGuid();
		var first = store.Publish(Sample(accountId));
		var size = first.EstimatedBytes;
		settings.BudgetBytes = size;
		var tooLarge = WorkspaceContext.Create(
			accountId,
			[],
			Enumerable.Range(0, 1000).Select(index => new WorkspaceOccurrence("account", accountId, "system", Guid.NewGuid(), index, "path-" + index.ToString())).ToArray(),
			[],
			[],
			[],
			[]);
		Assert.True(tooLarge.EstimatedBytes > settings.BudgetBytes);
		Assert.Throws<WorkspaceContextTooLargeException>(() => store.Publish(tooLarge));
		Assert.Equal(1, store.Count);

		settings.BudgetBytes = size * 2;
		settings.IdleTimeout = TimeSpan.FromTicks(10);
		var second = store.Publish(Sample(accountId));
		clock.Advance(TimeSpan.FromTicks(9));
		Assert.True(store.Acquire(accountId, second.ContextId).Available);
		clock.Advance(TimeSpan.FromTicks(1));
		var third = store.Publish(Sample(accountId));
		Assert.False(store.Acquire(accountId, first.ContextId).Available);
		clock.Advance(TimeSpan.FromTicks(1));
		Assert.True(store.Acquire(accountId, third.ContextId).Available);
		settings.IdleTimeout = TimeSpan.FromMinutes(30);
		var fourth = store.Publish(Sample(accountId));
		Assert.False(store.Acquire(accountId, second.ContextId).Available);
		Assert.True(store.Acquire(accountId, third.ContextId).Available);
		Assert.True(store.Acquire(accountId, fourth.ContextId).Available);
	}

	/// <summary>Overlapping publishes stay inside the budget. The test does not open a database connection.</summary>
	[Fact]
	public async Task Store_ConcurrentPublishesStayWithinTheBudget()
	{
		var store = NewStore(out _, out var settings);
		var sample = Sample(Guid.NewGuid(), "budget");
		settings.BudgetBytes = sample.EstimatedBytes * 4;
		var tasks = Enumerable.Range(0, 20).Select(index => Task.Run(() => store.Publish(Sample(Guid.NewGuid(), "item-" + index.ToString())))).ToArray();
		await Task.WhenAll(tasks);
		Assert.InRange(store.Count, 1, 4);
		Assert.InRange(store.EstimatedBytes, 1, settings.BudgetBytes);
	}

	/// <summary>
	/// A budget that fits either context, but not both, evicts the stored context and leaves the acquired copy readable.
	/// </summary>
	/// <remarks>
	/// The budget is the larger of the two actual estimates. The path strings differ in length, so copying the first
	/// estimate would make the second context alone exceed the budget and reject it before eviction.
	/// </remarks>
	[Fact]
	public void Store_KeepsAnAcquiredCopyAfterEviction()
	{
		var store = NewStore(out _, out var settings);
		var accountId = Guid.NewGuid();
		var held = Sample(accountId, "held");
		var replacement = Sample(accountId, "replacement");
		settings.BudgetBytes = Math.Max(held.EstimatedBytes, replacement.EstimatedBytes);
		Assert.True(held.EstimatedBytes <= settings.BudgetBytes);
		Assert.True(replacement.EstimatedBytes <= settings.BudgetBytes);
		Assert.True(held.EstimatedBytes + replacement.EstimatedBytes > settings.BudgetBytes);

		var context = store.Publish(held);
		var acquired = store.Acquire(accountId, context.ContextId);
		Assert.True(acquired.Available);

		var published = store.Publish(replacement);
		Assert.Equal(1, store.Count);
		Assert.True(store.Acquire(accountId, published.ContextId).Available);

		var evicted = store.Acquire(accountId, context.ContextId);
		Assert.False(evicted.Available);
		Assert.Equal(WorkspaceContextResults.Unavailable, evicted.Result);
		Assert.Equal(context.ContextId, acquired.Context!.ContextId);
		Assert.Equal("held", Assert.Single(acquired.Context.Occurrences).PathKey);
	}

	/// <summary>Accounting messages contain counts and estimates and do not contain the context id.</summary>
	[Fact]
	public void Store_LogsCountsWithoutTheContextId()
	{
		var logger = new ListLogger();
		var store = new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), logger);
		var context = store.Publish(Sample(Guid.NewGuid(), "quiet"));
		Assert.NotEmpty(logger.Messages);
		Assert.DoesNotContain(logger.Messages, message => message.Contains(context.ContextId, StringComparison.Ordinal));
	}

	/// <summary>A ready build is published. A missing account, a snapshot failure, and cancellation publish nothing.</summary>
	[Fact]
	public async Task Service_PublishesOnlyACommittedBuild()
	{
		var store = NewStore(out _, out _);
		var session = new FakeOpeningSession();
		var service = new WorkspaceTreeService(session, store);
		var accountId = Guid.NewGuid();
		session.Result = new WorkspaceOpeningResult.Ready(Build(accountId, "kept"));
		var response = await service.OpenAsync(new ClaimsPrincipal(), CancellationToken.None);
		Assert.NotNull(response);
		Assert.True(store.Acquire(accountId, response.ContextId).Available);

		session.Result = new WorkspaceOpeningResult.MissingAccount();
		Assert.Null(await service.OpenAsync(new ClaimsPrincipal(), CancellationToken.None));
		Assert.Equal(1, store.Count);

		session.Result = null;
		session.Error = new WorkspaceSnapshotUnavailableException();
		await Assert.ThrowsAsync<WorkspaceSnapshotUnavailableException>(() => service.OpenAsync(new ClaimsPrincipal(), CancellationToken.None));
		Assert.Equal(1, store.Count);

		session.Error = null;
		session.Result = new WorkspaceOpeningResult.Ready(Build(accountId, "cancelled"));
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.OpenAsync(new ClaimsPrincipal(), cancelled.Token));
		Assert.Equal(1, store.Count);
	}

	/// <summary>Legacy selection uses the profile route. Workspace selection uses the context detail route. A late response does not paint.</summary>
	[Fact]
	public void View_WorkspaceSelectionRequestsDetailsAndIgnoresStaleResponses()
	{
		var view = new WorkspaceTreeViewSession();
		var legacy = view.BeginLoad();
		Assert.True(view.CompleteLegacyLoad(legacy, false, ["old"]));
		view.Select("old");
		Assert.True(view.DetailRequested);
		Assert.Contains("GET profile", view.Requests);
		Assert.DoesNotContain(view.Requests, request => request.StartsWith("GET member-details ", StringComparison.Ordinal));

		view.SetWorkspaceTreeEnabled(true);
		Assert.Empty(view.Tree);
		Assert.False(view.DetailRequested);
		var generation = view.BeginLoad();
		Assert.Contains("GET workspace", view.Requests);
		Assert.False(view.CompleteWorkspaceLoad(generation - 1, true, "late", ["late"]));
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["tree-a"]));
		view.Select("tree-a");
		Assert.True(view.DetailRequested);
		Assert.Contains("GET member-details context-a tree-a 0", view.Requests);
		Assert.True(view.PaintDetail(generation, "tree-a"));
		var refresh = view.BeginLoad();
		Assert.False(view.PaintDetail(generation, "tree-a"));
		Assert.True(view.CompleteWorkspaceLoad(refresh, true, "context-a", ["tree-a"]));
		view.Select("other");
		Assert.False(view.PaintDetail(refresh, "tree-a"));

		view.SetWorkspaceTreeEnabled(false);
		Assert.False(view.CompleteWorkspaceLoad(generation, true, "context-b", ["tree-b"]));
		Assert.Empty(view.Tree);
		view.Select("fallback");
		Assert.Contains("GET profile", view.Requests);
		Assert.DoesNotContain(view.Requests, request => request.Contains(" fallback", StringComparison.Ordinal));
	}

	/// <summary>
	/// The second window is appended. Cards already shown keep their edits and expansion.
	/// A short pane starts that next window once, without waiting for another scroll.
	/// </summary>
	[Fact]
	public void View_AppendsTheNextMemberWindowAndKeepsExistingCards()
	{
		var view = OpenMemberView("anchor");
		view.RetainMemberPreview(MemberPreview("anchor", 40));
		view.PaneNeedsAnotherBatch = true;
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		var first = view.MemberCards[0];
		first.State = "edited";
		first.Expanded = true;
		Assert.Equal(["GET member-details context-a anchor 0", "GET member-details context-a anchor 20"], view.Requests.Where(request => request.StartsWith("GET member-details ", StringComparison.Ordinal)));
		Assert.True(view.DetailInFlight);
		Assert.Equal(20, view.MemberCards.Count);

		view.PaneNeedsAnotherBatch = false;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 20)));
		Assert.Same(first, view.MemberCards[0]);
		Assert.Equal("edited", first.State);
		Assert.True(first.Expanded);
		Assert.Equal(Ids(1, 40), view.MemberCards.Select(card => card.MemberId).ToArray());
		Assert.False(view.RequestNextPage());
	}

	/// <summary>
	/// A repeated preview id does not move the window. A missing profile is not a card and does not change the next skip.
	/// </summary>
	[Fact]
	public void View_AdvancesSkipByThePreviewWindowWhenProfilesAreMissing()
	{
		var view = OpenMemberView("anchor");
		var rows = new List<WorkspaceTreeViewSession.MemberPreviewRow>
		{
			new("anchor", "group", Ids(1, 1)[0]),
			new("other", "member", Ids(99, 1)[0]),
			new("anchor", "member", Guid.Empty.ToString("D")),
			new("anchor", "member", Ids(1, 1)[0]),
			new("anchor", "member", Ids(1, 1)[0])
		};
		rows.AddRange(MemberPreview("anchor", 24).Skip(1));
		view.RetainMemberPreview(rows);
		Assert.Equal(24, view.DirectMemberCount("anchor"));

		var generation = view.DetailGeneration;
		var firstWindow = Ids(1, 20).Where(id => id != Ids(5, 1)[0]).ToArray();
		Assert.True(view.AcceptMemberDetails(generation, "anchor", firstWindow.Append(Ids(21, 1)[0]).ToArray()));
		Assert.Equal(firstWindow, view.MemberCards.Select(card => card.MemberId).ToArray());
		Assert.Equal(20, view.DetailSkip);
		Assert.True(view.RequestNextPage());
		Assert.False(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(5, 1).Concat(Ids(21, 4)).ToArray()));
		Assert.Equal(Ids(1, 4).Concat(Ids(6, 19)).ToArray(), view.MemberCards.Select(card => card.MemberId).ToArray());
		Assert.Equal(40, view.DetailSkip);
		Assert.Equal(1, view.MemberCards.Count(card => card.MemberId == Ids(1, 1)[0]));
	}

	/// <summary>A second scroll while a window is in flight does not request another window.</summary>
	[Fact]
	public void View_IgnoresOverlappingMemberScroll()
	{
		var view = OpenMemberView("anchor");
		view.RetainMemberPreview(MemberPreview("anchor", 40));
		Assert.False(view.RequestNextPage());
		Assert.Equal("GET member-details context-a anchor 0", Assert.Single(view.Requests, request => request.StartsWith("GET member-details ", StringComparison.Ordinal)));
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", Ids(1, 20)));
		Assert.True(view.RequestNextPage());
		Assert.False(view.RequestNextPage());
		Assert.Equal(2, view.Requests.Count(request => request.StartsWith("GET member-details ", StringComparison.Ordinal)));
	}

	/// <summary>A response for the previous anchor does not append cards or move the new anchor's skip.</summary>
	[Fact]
	public void View_IgnoresMemberDetailsAfterTheAnchorChanges()
	{
		var view = OpenMemberView("anchor-a");
		view.RetainMemberPreview(MemberPreview("anchor-a", 25).Concat(MemberPreview("anchor-b", 5)).ToArray());
		var generation = view.DetailGeneration;
		view.Select("anchor-b");
		Assert.False(view.AcceptMemberDetails(generation, "anchor-a", Ids(1, 20)));
		Assert.Empty(view.MemberCards);
		Assert.Equal(0, view.DetailSkip);
		Assert.True(view.DetailInFlight);
		Assert.True(view.AcceptMemberDetails(generation, "anchor-b", Ids(1, 5)));
		Assert.Equal(Ids(1, 5), view.MemberCards.Select(card => card.MemberId).ToArray());
		Assert.False(view.PaintDetail(generation, "anchor-a"));
	}

	/// <summary>
	/// The last partial window completes the list. A failed window keeps the cards and skip, and the next scroll retries that same window.
	/// </summary>
	[Fact]
	public void View_StopsAtTheLastMemberWindowAndRetriesAFailedWindow()
	{
		var view = OpenMemberView("anchor");
		view.RetainMemberPreview(MemberPreview("anchor", 25));
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 5)));
		Assert.True(view.DetailExhausted);
		Assert.False(view.RequestNextPage());
		Assert.Equal(2, view.Requests.Count(request => request.StartsWith("GET member-details ", StringComparison.Ordinal)));

		view.Select("anchor");
		view.PaneNeedsAnotherBatch = true;
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", Ids(1, 20)));
		Assert.Equal("GET member-details context-a anchor 20", view.Requests[^1]);
		var kept = view.MemberCards.ToArray();
		Assert.True(view.FailMemberDetails(view.DetailGeneration, "anchor"));
		Assert.Equal("The member profiles could not be loaded.", view.DetailError);
		Assert.Equal(20, view.DetailSkip);
		Assert.Equal(kept, view.MemberCards);
		Assert.False(view.DetailInFlight);
		Assert.Equal("GET member-details context-a anchor 20", view.Requests[^1]);
		Assert.True(view.RequestNextPage());
		Assert.Equal("GET member-details context-a anchor 20", view.Requests[^1]);
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", Ids(21, 5)));
		Assert.Equal(Ids(1, 25), view.MemberCards.Select(card => card.MemberId).ToArray());
	}

	/// <summary>
	/// The heading count is every unique direct member. A duplicate, another parent, a non-member, and an empty id are not added.
	/// </summary>
	[Fact]
	public void View_DetailHeadingCountsEveryUniqueDirectMember()
	{
		var view = OpenMemberView("system-a");
		var rows = new List<WorkspaceTreeViewSession.MemberPreviewRow>();
		rows.AddRange(MemberPreview("system-a", 47));
		rows.Add(new WorkspaceTreeViewSession.MemberPreviewRow("system-a", "member", Ids(1, 1)[0]));
		rows.Add(new WorkspaceTreeViewSession.MemberPreviewRow("system-b", "member", Ids(90, 1)[0]));
		rows.Add(new WorkspaceTreeViewSession.MemberPreviewRow("system-a", "group", Ids(91, 1)[0]));
		rows.Add(new WorkspaceTreeViewSession.MemberPreviewRow("system-a", "member", Guid.Empty.ToString("D")));
		view.RetainMemberPreview(rows);

		view.Select("system-a", "System: Needs of the Many");

		Assert.Equal(47, view.DirectMemberCount("system-a"));
		Assert.Equal("System: Needs of the Many (47)", view.DetailHeading);
	}

	/// <summary>System, group, and place headings each use that occurrence's own direct-member count.</summary>
	[Fact]
	public void View_DetailHeadingFollowsTheSelectedAnchor()
	{
		var view = OpenMemberView("system-a");
		var rows = new List<WorkspaceTreeViewSession.MemberPreviewRow>();
		rows.AddRange(MemberPreview("system-a", 47));
		rows.AddRange(MemberPreview("group-a", 3));
		rows.AddRange(MemberPreview("place-a", 1));
		view.RetainMemberPreview(rows);

		view.Select("system-a", "System: Needs of the Many");
		Assert.Equal("System: Needs of the Many (47)", view.DetailHeading);
		view.Select("group-a", "Group: House");
		Assert.Equal("Group: House (3)", view.DetailHeading);
		view.Select("place-a", "Place: Shore");
		Assert.Equal("Place: Shore (1)", view.DetailHeading);
	}

	/// <summary>Loading later member windows does not change the heading count.</summary>
	[Fact]
	public void View_PagingDoesNotChangeTheDetailHeadingCount()
	{
		var view = OpenMemberView("system-a");
		view.RetainMemberPreview(MemberPreview("system-a", 47));
		view.Select("system-a", "System: Needs of the Many");
		var heading = view.DetailHeading;

		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "system-a", Ids(1, 20)));
		Assert.Equal(20, view.MemberCards.Count);
		Assert.Equal(heading, view.DetailHeading);
		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "system-a", Ids(21, 20)));
		Assert.Equal(40, view.MemberCards.Count);
		Assert.Equal("System: Needs of the Many (47)", view.DetailHeading);
	}

	/// <summary>A failed replacement keeps the previous tree. Sign-out still completes when release fails.</summary>
	[Fact]
	public void View_FailedRefreshKeepsThePreviousTreeAndReleaseFailureStillSignsOut()
	{
		var view = new WorkspaceTreeViewSession();
		view.SetWorkspaceTreeEnabled(true);
		var first = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(first, true, "context-a", ["tree-a"]));
		var refresh = view.BeginLoad();
		Assert.True(view.FailLoad(refresh, true, true, "replacement failed"));
		Assert.Equal("tree-a", Assert.Single(view.Tree));
		Assert.Equal("context-a", view.ContextId);
		Assert.Equal("replacement failed", view.Error);
		Assert.Equal(2, view.Requests.Count(request => request.StartsWith("GET ", StringComparison.Ordinal)));

		view.SignOut(true);
		Assert.True(view.LoggedOut);
		Assert.Null(view.ContextId);
		Assert.Contains("DELETE context-a", view.Requests);
		Assert.Equal("POST logout", view.Requests[^1]);
	}

	/// <summary>Another account cannot read the context, and an anchor occurrence supplies its own placement.</summary>
	[Fact]
	public async Task Details_IsolatesAccountsAndKeepsPlacementOnTheSelectedAnchor()
	{
		var store = NewStore(out _, out _);
		var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
		var memberId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
		var context = TwoAnchorContext(owner, memberId);
		store.Publish(context);
		var gateway = new FakeProfileGateway(other);
		var service = new WorkspaceMemberDetailService(store, gateway);

		var denied = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None);
		Assert.IsType<WorkspaceMemberDetailResult.ContextUnavailable>(denied);
		Assert.Equal(0, gateway.Reads);

		gateway.AccountId = owner;
		var first = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None);
		var ready = Assert.IsType<WorkspaceMemberDetailResult.Ready>(first);
		var card = Assert.Single(ready.Cards);
		Assert.Equal(memberId, card.MemberId);
		Assert.Equal("explicit", card.PlacementVisibilityMode);
		Assert.Equal("viewer", card.PlacementPermissionCode);
		Assert.Null(card.AvatarUrl);
		Assert.Equal("system", gateway.LastObjectType);
		Assert.Equal("Harbor", gateway.LastAnchorName);
		Assert.Equal("system:11111111-1111-1111-1111-111111111111", gateway.LastBranch);

		var second = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-b", CancellationToken.None);
		var otherCard = Assert.Single(Assert.IsType<WorkspaceMemberDetailResult.Ready>(second).Cards);
		Assert.Equal(memberId, otherCard.MemberId);
		Assert.Equal("inherit", otherCard.PlacementVisibilityMode);
		Assert.Equal("system:22222222-2222-2222-2222-222222222222", gateway.LastBranch);
	}

	/// <summary>Only direct members are returned, and a member row is not an anchor occurrence.</summary>
	[Fact]
	public async Task Details_FiltersDirectMembersAndRejectsAMemberOccurrence()
	{
		var store = NewStore(out _, out _);
		var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var directId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
		var nestedId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
		var context = NestedContext(owner, directId, nestedId);
		store.Publish(context);
		var gateway = new FakeProfileGateway(owner);
		var service = new WorkspaceMemberDetailService(store, gateway);

		Assert.IsType<WorkspaceMemberDetailResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "missing-path", CancellationToken.None));
		Assert.IsType<WorkspaceMemberDetailResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "groups-header", CancellationToken.None));
		Assert.Equal(0, gateway.Reads);

		var ready = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(directId, Assert.Single(ready.Cards).MemberId);

		var rejected = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "member-direct", CancellationToken.None);
		Assert.IsType<WorkspaceMemberDetailResult.OccurrenceRejected>(rejected);
		Assert.Equal(1, gateway.Reads);
	}

	/// <summary>The detail batch keeps the first 20 direct members and still asks the live reader when none are direct.</summary>
	[Fact]
	public async Task Details_CapsTheBatchAndStillAuthorizesAnEmptyAnchor()
	{
		var store = NewStore(out _, out _);
		var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var context = ManyMembersContext(owner, 21);
		store.Publish(context);
		var gateway = new FakeProfileGateway(owner);
		var service = new WorkspaceMemberDetailService(store, gateway);
		var ready = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(20, ready.Cards.Count);
		Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), ready.Cards[0].MemberId);
		Assert.DoesNotContain(ready.Cards, card => card.MemberId == Guid.Parse("00000000-0000-0000-0000-000000000015"));

		var emptyContext = EmptyAnchorContext(owner);
		store.Publish(emptyContext);
		var empty = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), emptyContext.ContextId, "system-empty", CancellationToken.None));
		Assert.Empty(empty.Cards);
		Assert.Equal(2, gateway.Reads);
	}

	/// <summary>
	/// Skip selects the next unique direct members in preview order. A negative skip does not call the profile procedure.
	/// </summary>
	[Fact]
	public async Task Details_PagesDirectMembersInPreviewOrder()
	{
		var store = NewStore(out _, out _);
		var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var context = ManyMembersContext(owner, 45);
		store.Publish(context);
		var gateway = new FakeProfileGateway(owner);
		var service = new WorkspaceMemberDetailService(store, gateway);

		var first = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var second = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 20));

		Assert.Equal(20, first.Cards.Count);
		Assert.Equal(20, second.Cards.Count);
		Assert.Equal(PagedMember(1), first.Cards[0].MemberId);
		Assert.Equal(PagedMember(20), first.Cards[19].MemberId);
		Assert.Equal(PagedMember(21), second.Cards[0].MemberId);
		Assert.Equal(PagedMember(40), second.Cards[19].MemberId);
		Assert.Empty(first.Cards.Select(card => card.MemberId).Intersect(second.Cards.Select(card => card.MemberId)));

		var partialContext = ManyMembersContext(owner, 25);
		store.Publish(partialContext);
		var partial = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), partialContext.ContextId, "system-a", CancellationToken.None, 20));
		Assert.Equal(Enumerable.Range(21, 5).Select(PagedMember).ToArray(), partial.Cards.Select(card => card.MemberId).ToArray());

		var beyond = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), partialContext.ContextId, "system-a", CancellationToken.None, 40));
		Assert.Empty(beyond.Cards);
		Assert.Equal(4, gateway.Reads);

		var readsBeforeInvalid = gateway.Reads;
		var rejected = Assert.IsType<WorkspaceMemberDetailResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), partialContext.ContextId, "system-a", CancellationToken.None, -1));
		Assert.Equal("The member batch skip must be zero or greater.", rejected.Error);
		Assert.Equal(readsBeforeInvalid, gateway.Reads);
		Assert.False(WorkspaceMemberDetailService.TryParseMemberSkip("-1", out _, out _));
		Assert.False(WorkspaceMemberDetailService.TryParseMemberSkip("abc", out _, out _));
		Assert.False(WorkspaceMemberDetailService.TryParseMemberSkip("1.5", out _, out _));
		Assert.True(WorkspaceMemberDetailService.TryParseMemberSkip(null, out var omitted, out var omittedError));
		Assert.Equal(0, omitted);
		Assert.Equal("", omittedError);
	}

	/// <summary>A member under another occurrence is not part of this page, and a repeated member id does not move the page.</summary>
	[Fact]
	public async Task Details_ExcludesOtherOccurrencesAndIgnoresDuplicateMemberIds()
	{
		var store = NewStore(out _, out _);
		var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var systemA = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var systemB = Guid.Parse("22222222-2222-2222-2222-222222222222");
		var otherMember = PagedMember(45);
		var rows = new List<PluralMapPreviewRow>
		{
			Preview("system-a", "system", systemA, null, false, "Harbor"),
			Preview("system-b", "system", systemB, null, false, "Night"),
			Preview("other-member", "member", otherMember, "system-b", false, "Member: Other")
		};
		var duplicated = PagedMember(1);
		rows.Add(Preview("member-dup", "member", duplicated, "system-a", false, "Member: Dup"));
		for (var index = 1; index <= 22; index++)
		{
			rows.Add(Preview("member-" + index.ToString(), "member", PagedMember(index), "system-a", false, "Member: " + index.ToString()));
		}

		var context = WorkspaceContext.Create(
			owner,
			[],
			[
				new WorkspaceOccurrence("account", owner, "system", systemA, 0, "system-a"),
				new WorkspaceOccurrence("account", owner, "system", systemB, 0, "system-b")
			],
			[],
			[],
			rows,
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemA, 1, "Active", true)]);
		store.Publish(context);
		var service = new WorkspaceMemberDetailService(store, new FakeProfileGateway(owner));

		var first = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var page = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 20));

		Assert.Equal(20, first.Cards.Count);
		Assert.Equal(PagedMember(1), first.Cards[0].MemberId);
		Assert.Equal(PagedMember(20), first.Cards[19].MemberId);
		Assert.Equal(new[] { PagedMember(21), PagedMember(22) }, page.Cards.Select(card => card.MemberId).ToArray());
		Assert.DoesNotContain(first.Cards, card => card.MemberId == otherMember);
		Assert.DoesNotContain(page.Cards, card => card.MemberId == otherMember);
		Assert.Equal(1, first.Cards.Count(card => card.MemberId == duplicated));
	}

	/// <summary>Builds a store on the manual clock. The logger records accounting text and does not connect.</summary>
	private static WorkspaceTreeViewSession OpenMemberView(string path)
	{
		var view = new WorkspaceTreeViewSession();
		view.SetWorkspaceTreeEnabled(true);
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", [path]));
		view.Select(path);
		return view;
	}

	private static string[] Ids(int start, int count)
	{
		return Enumerable.Range(start, count)
			.Select(index => Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")).ToString("D"))
			.ToArray();
	}

	private static WorkspaceTreeViewSession.MemberPreviewRow[] MemberPreview(string path, int count)
	{
		return Ids(1, count)
			.Select(id => new WorkspaceTreeViewSession.MemberPreviewRow(path, "member", id))
			.ToArray();
	}

	private static Guid PagedMember(int index)
	{
		return Guid.Parse("00000000-0000-0000-0000-0000000000" + index.ToString("X2"));
	}

	private static WorkspaceContextStore NewStore(out ManualWorkspaceClock clock, out MutableWorkspaceContextSettings settings)
	{
		clock = new ManualWorkspaceClock();
		settings = new MutableWorkspaceContextSettings();
		return new WorkspaceContextStore(clock, settings, new ListLogger());
	}

	private static WorkspaceContext Sample(Guid accountId, string path = "path")
	{
		var anchorId = Guid.NewGuid();
		return WorkspaceContext.Create(
			accountId,
			[new WorkspaceGraphEdge("system", anchorId, "member", Guid.NewGuid(), 1, "placement")],
			[new WorkspaceOccurrence("account", accountId, "system", anchorId, 0, path)],
			[new WorkspaceNodeLabel("system", anchorId, "Harbor")],
			[new WorkspaceSystemEdgeLabel(anchorId, null)],
			[],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", anchorId, 1, "Active", true)]);
	}

	private static WorkspaceCapturedBuild Build(Guid accountId, string path)
	{
		var anchorId = Guid.NewGuid();
		return new WorkspaceCapturedBuild(
			accountId,
			[new WorkspaceGraphEdge("system", anchorId, "member", Guid.NewGuid(), 1, "placement")],
			[new WorkspaceOccurrence("account", accountId, "system", anchorId, 0, path)],
			[new WorkspaceNodeLabel("system", anchorId, "Harbor")],
			[new WorkspaceSystemEdgeLabel(anchorId, null)],
			[],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", anchorId, 1, "Active", true)]);
	}

	private static PluralMapRawResultSet Set(PluralMapRawColumn[] columns, object[][] rows)
	{
		return new PluralMapRawResultSet
		{
			Columns = columns,
			Rows = rows.Select(row => (IReadOnlyList<object>)row).ToArray()
		};
	}

	private static PluralMapRawColumn Column(string name, string sqlType)
	{
		return new PluralMapRawColumn(name, sqlType);
	}

	private static PluralMapPreviewRow Preview(string path, string? type, Guid? id, string? parent, bool header, string? name)
	{
		return new PluralMapPreviewRow("preview", path, path, name, type, id, parent, header, name);
	}

	private static WorkspaceContext TwoAnchorContext(Guid accountId, Guid memberId)
	{
		var systemA = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var systemB = Guid.Parse("22222222-2222-2222-2222-222222222222");
		return WorkspaceContext.Create(
			accountId,
			[],
			[
				new WorkspaceOccurrence("account", accountId, "system", systemA, 0, "system-a"),
				new WorkspaceOccurrence("account", accountId, "system", systemB, 0, "system-b")
			],
			[],
			[],
			[
				Preview("system-a", "system", systemA, null, false, "Harbor"),
				Preview("member-a", "member", memberId, "system-a", false, "Member: Jane"),
				Preview("system-b", "system", systemB, null, false, "Night"),
				Preview("member-b", "member", memberId, "system-b", false, "Member: Jane")
			],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemA, 1, "Active", true)]);
	}

	private static WorkspaceContext NestedContext(Guid accountId, Guid directId, Guid nestedId)
	{
		var systemA = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var groupA = Guid.Parse("33333333-3333-3333-3333-333333333333");
		return WorkspaceContext.Create(
			accountId,
			[],
			[
				new WorkspaceOccurrence("account", accountId, "system", systemA, 0, "system-a"),
				new WorkspaceOccurrence("system", systemA, "group", groupA, 1, "group-a")
			],
			[],
			[],
			[
				Preview("system-a", "system", systemA, null, false, "Harbor"),
				Preview("groups-header", "group", null, "system-a", true, "Groups"),
				Preview("member-direct", "member", directId, "system-a", false, "Member: Direct"),
				Preview("group-a", "group", groupA, "system-a", false, "Group: Inner"),
				Preview("member-nested", "member", nestedId, "group-a", false, "Member: Nested")
			],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemA, 1, "Active", true)]);
	}

	private static WorkspaceContext ManyMembersContext(Guid accountId, int count)
	{
		var systemA = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var rows = new List<PluralMapPreviewRow>
		{
			Preview("system-a", "system", systemA, null, false, "Harbor")
		};
		for (var index = 1; index <= count; index++)
		{
			rows.Add(Preview(
				"member-" + index.ToString(),
				"member",
				Guid.Parse("00000000-0000-0000-0000-0000000000" + index.ToString("X2")),
				"system-a",
				false,
				"Member: " + index.ToString()));
		}

		return WorkspaceContext.Create(
			accountId,
			[],
			[new WorkspaceOccurrence("account", accountId, "system", systemA, 0, "system-a")],
			[],
			[],
			rows,
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemA, 1, "Active", true)]);
	}

	private static WorkspaceContext EmptyAnchorContext(Guid accountId)
	{
		var systemId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		return WorkspaceContext.Create(
			accountId,
			[],
			[new WorkspaceOccurrence("account", accountId, "system", systemId, 0, "system-empty")],
			[],
			[],
			[Preview("system-empty", "system", systemId, null, false, "Harbor")],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
	}

	private sealed class FakeProfileGateway : IWorkspaceProfileGateway
	{
		public FakeProfileGateway(Guid accountId)
		{
			AccountId = accountId;
		}

		public Guid AccountId { get; set; }

		public int Reads { get; private set; }

		public string? LastObjectType { get; private set; }

		public string? LastAnchorName { get; private set; }

		public string? LastBranch { get; private set; }

		public Task<AccessContextHelper.Account?> ResolveAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				AccountId,
				"owner@example.com",
				"Owner",
				1,
				new AccessContextHelper.AccountStatus(1, "Active", "Active", 1, true),
				DateTime.UtcNow,
				null));
		}

		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			Reads++;
			LastObjectType = objectType;
			LastAnchorName = anchorName;
			LastBranch = branch;
			var mode = anchorName == "Night" ? "inherit" : "explicit";
			var profiles = new List<AnchorMemberProfile>();
			var placements = new List<AnchorMemberPlacement>();
			var ids = new List<Guid>
			{
				Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
				Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
				Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")
			};
			for (var index = 1; index <= 45; index++)
			{
				ids.Add(Guid.Parse("00000000-0000-0000-0000-0000000000" + index.ToString("X2")));
			}

			foreach (var id in ids)
			{
				profiles.Add(new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false));
				placements.Add(new AnchorMemberPlacement(id, mode, mode == "inherit" ? null : 1, "explicit", mode == "inherit" ? null : "viewer"));
			}

			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], placements, [], []));
		}
	}

	private sealed class FakeOpeningSession : IWorkspaceOpeningSession
	{
		public WorkspaceOpeningResult? Result { get; set; }

		public Exception? Error { get; set; }

		public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			if (Error != null)
			{
				return Task.FromException<WorkspaceOpeningResult>(Error);
			}

			return Task.FromResult(Result ?? new WorkspaceOpeningResult.MissingAccount());
		}
	}

	private sealed class ListLogger : ILogger<WorkspaceContextStore>
	{
		public List<string> Messages { get; } = new();

		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull
		{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Messages.Add(formatter(state, exception));
		}
	}

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
		}
	}
}
