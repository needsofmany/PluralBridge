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

	/// <summary>Workspace mode does not request profiles. A late generation or a changed mode does not paint.</summary>
	[Fact]
	public void View_WorkspaceModeDoesNotRequestProfilesAndIgnoresStaleResponses()
	{
		var view = new WorkspaceTreeViewSession();
		var legacy = view.BeginLoad();
		Assert.True(view.CompleteLegacyLoad(legacy, false, ["old"]));
		view.Select("old");
		Assert.True(view.DetailRequested);
		Assert.Contains("GET profile", view.Requests);

		view.SetWorkspaceTreeEnabled(true);
		Assert.Empty(view.Tree);
		Assert.False(view.DetailRequested);
		var generation = view.BeginLoad();
		Assert.Contains("GET workspace", view.Requests);
		Assert.False(view.CompleteWorkspaceLoad(generation - 1, true, "late", ["late"]));
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["tree-a"]));
		view.Select("tree-a");
		Assert.False(view.DetailRequested);
		Assert.Equal("tree-a", Assert.Single(view.Tree));

		view.SetWorkspaceTreeEnabled(false);
		Assert.False(view.CompleteWorkspaceLoad(generation, true, "context-b", ["tree-b"]));
		Assert.Empty(view.Tree);
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

	/// <summary>Builds a store on the manual clock. The logger records accounting text and does not connect.</summary>
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
