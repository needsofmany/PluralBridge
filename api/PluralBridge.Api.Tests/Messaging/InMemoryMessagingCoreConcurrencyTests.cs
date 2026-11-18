// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using PluralBridge.Api.Messaging;

namespace PluralBridge.Api.Tests.Messaging;

/// <summary>
/// Stops the tests in this file from running at the same time as one another.
/// </summary>
/// <remarks>
/// This is test scheduling only. It does not change the core's accounting gate and it does not
/// prove that every interleaving occurred.
/// </remarks>
[CollectionDefinition(InMemoryMessagingCoreConcurrencyCollection.Name, DisableParallelization = true)]
public sealed class InMemoryMessagingCoreConcurrencyCollection
{
	/// <summary>Collection name shared by the concurrency and flood tests.</summary>
	public const string Name = "InMemoryMessagingCoreConcurrency";
}

/// <summary>
/// Concurrent and flood checks for the isolated in-memory core.
/// </summary>
/// <remarks>
/// The manual clock is the only control used to arrange TTL. Tests do not sleep to expire messages.
/// A wall-clock watchdog fails a stuck wait. That limit is test supervision, not a message TTL and not a throughput target.
/// Eight workers pull indexes from a shared counter. The tests do not create one task per message.
/// Shuffles use seed 8801. Assertions compare the core with a ledger recorded when each publication was accepted.
/// Repeating a race does not prove every interleaving. A passing mixed workload shows progress for that workload only.
/// Flood waits use a three-minute watchdog. Other waits use 45 seconds. Both limits detect a stuck test.
/// Database durability and lifecycle republication stay outside this file.
/// </remarks>
[Collection(InMemoryMessagingCoreConcurrencyCollection.Name)]
public sealed class InMemoryMessagingCoreConcurrencyTests
{
	/// <summary>Fixed shuffle seed. Failure text includes it so a reordered workload can be repeated.</summary>
	private const int Seed = 8801;

	/// <summary>Bounded worker count. Indexes are pulled from one counter rather than creating one task per message.</summary>
	private const int WorkerCount = 8;

	/// <summary>Identified obligations on each flood message. The flood keeps this number small and fixed.</summary>
	private const int ObligationsPerMessage = 2;

	/// <summary>Obligation identities used by the flood. The first result is success and the second is error.</summary>
	private static readonly string[] ObligationNames = { "a", "b" };

	/// <summary>Hang limit for a flood phase. It is test supervision, not message TTL and not a throughput target.</summary>
	private static readonly TimeSpan FloodWatchdog = TimeSpan.FromMinutes(3);

	/// <summary>Hang limit for a smaller concurrent group. It is test supervision, not message TTL.</summary>
	private static readonly TimeSpan OperationWatchdog = TimeSpan.FromSeconds(45);

	/// <summary>
	/// Publishes and completes 100, 1,000, or 10,000 messages while the clock stays still.
	/// </summary>
	/// <param name="messageCount">The flood size.</param>
	/// <remarks>
	/// Publication of work, publication of reports, and acceptance are separate concurrent phases.
	/// Completion does not start until publication has settled, so a report cannot be consumed as an unknown target
	/// before its work message exists. Every eighth obligation is also submitted with a second result code.
	/// Exactly one of those submissions may win. The ledger records the winner. Duplicate acceptance must not discharge again.
	/// </remarks>
	[Theory]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public async Task Flood_CompletesEveryAcceptedObligation(int messageCount)
	{
		await RunSteadyFlood(messageCount);
	}

	/// <summary>
	/// Samples outcome history only after each flood phase and checks it against the independent ledger.
	/// </summary>
	/// <remarks>
	/// The hot loops do not copy the history. This smaller run exists so the phase-boundary rule has its own test.
	/// The larger sizes are the theory above. Neither run claims an exhaustive interleaving.
	/// </remarks>
	[Fact]
	public async Task Evidence_IsCapturedAtPhaseBoundaries()
	{
		await RunSteadyFlood(64);
	}

	/// <summary>
	/// Eight workers publish one work identity. Exactly one payload and TTL remain.
	/// </summary>
	/// <remarks>
	/// The clock stays at zero, so the send instant does not distinguish the winner. Payload and TTL do.
	/// A later rejected publish must not replace the accepted values.
	/// </remarks>
	[Fact]
	public async Task DuplicateWorkIdentity_AcceptsExactlyOne()
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var startedAt = clock.MonotonicTime;
		var accepted = new ConcurrentBag<MessagingResult>();
		await RunIndexWorkers(WorkerCount, WorkerCount, index =>
		{
			var result = core.Publish(new WorkPublication(
				"same-work",
				"work",
				"subject",
				"payload-" + index.ToString(),
				TimeSpan.FromTicks(100 + index),
				new[] { "only" }));
			if (result.Accepted)
			{
				accepted.Add(result);
			}
			else
			{
				Assert.True(
					result.Rejection == MessagingRejection.DuplicateIdentity,
					Diagnostic(WorkerCount, "same-work", "unexpected work rejection " + result.Rejection));
			}

			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(WorkerCount, "same-work", "duplicate work publication"));

		var winner = Assert.Single(accepted);
		Assert.NotNull(winner.Message);
		var replacement = core.Publish(new WorkPublication(
			"same-work",
			"work",
			"subject",
			"replacement-payload",
			TimeSpan.FromTicks(1),
			new[] { "only" }));
		Assert.Equal(MessagingRejection.DuplicateIdentity, replacement.Rejection);
		var stored = core.Find("same-work");
		Assert.NotNull(stored);
		Assert.Equal(winner.Message.Payload, stored.Payload);
		Assert.Equal(winner.Message.TimeToLive, stored.TimeToLive);
		Assert.Equal(winner.Message.SentAt, stored.SentAt);
		Assert.Equal(startedAt, stored.SentAt);
		Assert.Equal("only", Assert.Single(stored.Obligations).ObligationId);
		Assert.Null(stored.Terminal);
	}

	/// <summary>
	/// Eight workers publish one completion-report identity. The accepted result is the one that discharges.
	/// </summary>
	/// <remarks>
	/// The work message is published first and the clock does not advance, so report TTL is not the distinguishing field.
	/// The result code is. A later duplicate publish must leave that result in place.
	/// </remarks>
	[Fact]
	public async Task DuplicateCompletionIdentity_AcceptsExactlyOne()
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var work = core.Publish(new WorkPublication("parent", "work", "subject", "payload", TimeSpan.FromTicks(500), new[] { "only" }));
		Assert.True(work.Accepted, Diagnostic(WorkerCount, "parent", "parent publication failed"));
		var accepted = new ConcurrentBag<CompletionReportPublishedOutcome>();
		await RunIndexWorkers(WorkerCount, WorkerCount, index =>
		{
			var result = core.PublishCompletion(new CompletionReport(
				"same-report",
				"completion",
				TimeSpan.FromTicks(200 + index),
				"parent",
				"only",
				"result-" + index.ToString()));
			if (result.Accepted)
			{
				accepted.Add(Assert.Single(result.Outcomes.OfType<CompletionReportPublishedOutcome>()));
			}
			else
			{
				Assert.True(
					result.Rejection == MessagingRejection.DuplicateIdentity,
					Diagnostic(WorkerCount, "same-report", "unexpected report rejection " + result.Rejection));
			}

			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(WorkerCount, "same-report", "duplicate report publication"));

		var winner = Assert.Single(accepted);
		var later = core.PublishCompletion(new CompletionReport(
			"same-report",
			"completion",
			TimeSpan.FromTicks(9),
			"parent",
			"only",
			"replacement-result"));
		Assert.Equal(MessagingRejection.DuplicateIdentity, later.Rejection);
		var discharged = core.AcceptCompletion("same-report");
		Assert.True(discharged.Accepted, Diagnostic(WorkerCount, "same-report", "winning report did not discharge"));
		var obligation = Assert.Single(core.Find("parent")!.Obligations);
		Assert.Equal(winner.ResultCode, obligation.ResultCode);
		Assert.Equal("same-report", obligation.CompletionReportId);
		Assert.Equal(work.Message!.Payload, core.Find("parent")!.Payload);
		Assert.Equal(work.Message.TimeToLive, core.Find("parent")!.TimeToLive);
		Assert.Equal(work.Message.SentAt, core.Find("parent")!.SentAt);
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
	}

	/// <summary>
	/// Checks a work message one tick before its TTL, at the TTL, and after the TTL.
	/// </summary>
	/// <remarks>
	/// Each boundary uses its own core so the advances do not interfere. Expiration is an advance of the manual clock.
	/// At the equal tick, and after it, supervision poisons the message once and a completion report does not discharge it.
	/// </remarks>
	[Fact]
	public void WorkTtl_BeforeAtAndAfterExpiration()
	{
		AssertWorkCompletesBeforeDeadline(TimeSpan.FromTicks(10), TimeSpan.FromTicks(9));
		AssertWorkPoisonsAtOrAfterDeadline(TimeSpan.FromTicks(10), TimeSpan.FromTicks(10));
		AssertWorkPoisonsAtOrAfterDeadline(TimeSpan.FromTicks(10), TimeSpan.FromTicks(11));
	}

	/// <summary>
	/// Checks a completion report one tick before its TTL, at the TTL, and after the TTL.
	/// </summary>
	/// <remarks>
	/// The work TTL is longer than the report TTL, so an expired report leaves that work active and undischarged.
	/// A report accepted one tick early still discharges.
	/// </remarks>
	[Fact]
	public void CompletionReportTtl_BeforeAtAndAfterExpiration()
	{
		AssertReportDischargesBeforeDeadline(TimeSpan.FromTicks(10), TimeSpan.FromTicks(9));
		AssertReportExpiresWithoutDischarge(TimeSpan.FromTicks(10), TimeSpan.FromTicks(10));
		AssertReportExpiresWithoutDischarge(TimeSpan.FromTicks(10), TimeSpan.FromTicks(11));
	}

	/// <summary>
	/// Registers an extra obligation before the last original obligation is discharged.
	/// </summary>
	/// <remarks>
	/// The parent stays active after the original obligation completes. It becomes Completed only after the added obligation is discharged.
	/// </remarks>
	[Fact]
	public void RegistrationBeforeCompletion_AccountsForTheAddedObligation()
	{
		var core = NewCore(out _);
		PublishParent(core, "ordered", TimeSpan.FromTicks(50), "base");
		var registered = core.RegisterObligation("ordered", "extra");
		Assert.True(registered.Accepted, Diagnostic(1, "ordered", "early registration was rejected"));
		PublishAndAccept(core, "ordered", "base", "c-base", "success", TimeSpan.FromTicks(40));
		Assert.Null(core.Find("ordered")!.Terminal);
		PublishAndAccept(core, "ordered", "extra", "c-extra", "success", TimeSpan.FromTicks(40));
		var message = core.Find("ordered");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		Assert.Contains(message.Obligations, item => item.ObligationId == "base" && item.Discharged && !item.Retired);
		Assert.Contains(message.Obligations, item => item.ObligationId == "extra" && item.Discharged && !item.Retired);
		Assert.DoesNotContain(message.Obligations, item => item.ObligationId != "base" && item.ObligationId != "extra");
		Assert.Single(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
	}

	/// <summary>
	/// Completes the parent before a later registration. The registration adds nothing.
	/// </summary>
	/// <remarks>
	/// The rejection is TerminalMessage. Repeating the original report does not discharge again or add a second Completed outcome.
	/// </remarks>
	[Fact]
	public void CompletionBeforeRegistration_RejectsTheLateObligation()
	{
		var core = NewCore(out _);
		PublishParent(core, "finished", TimeSpan.FromTicks(50), "base");
		PublishAndAccept(core, "finished", "base", "c-base", "success", TimeSpan.FromTicks(40));
		var rejected = core.RegisterObligation("finished", "extra");
		Assert.Equal(MessagingRejection.TerminalMessage, rejected.Rejection);
		var retry = core.AcceptCompletion("c-base");
		Assert.Equal(MessagingRejection.DuplicateCompletionReport, retry.Rejection);
		var message = core.Find("finished");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		Assert.Equal("base", Assert.Single(message.Obligations).ObligationId);
		Assert.Single(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
	}

	/// <summary>
	/// Repeats a race among final completion, extra registration, and expiration.
	/// </summary>
	/// <remarks>
	/// Forty iterations share one asynchronous start signal per iteration and three workers.
	/// The clock advance happens inside the expiration worker, not through a sleep.
	/// After the workers finish, the test advances one more tick and supervises so an unfinished parent still reaches a terminal value.
	/// Completed is legal when every accepted obligation was discharged. Poisoned is legal otherwise.
	/// This repetition does not prove that both winners were observed, and it does not prove every interleaving.
	/// </remarks>
	[Fact]
	public async Task CompletionRegistrationAndExpiration_RepeatedRaces()
	{
		const int iterations = 40;
		for (var iteration = 0; iteration < iterations; iteration++)
		{
			await RunOneRace(iteration);
		}
	}

	/// <summary>
	/// Several supervisors retire one already expired message, and a second wave does not retire it again.
	/// </summary>
	/// <remarks>
	/// The manual clock advances to the work TTL before either wave starts. Eight workers share one start signal.
	/// The watchdog is the 45-second hang limit. Exactly one Poisoned outcome is legal. The obligation is retired and not discharged.
	/// </remarks>
	[Fact]
	public async Task ConcurrentSupervision_PoisonsOnce()
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		PublishParent(core, "quiet", TimeSpan.FromTicks(5), "only");
		clock.Advance(TimeSpan.FromTicks(5));
		await RunIndexWorkers(WorkerCount, WorkerCount, _ =>
		{
			core.Supervise();
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(WorkerCount, "quiet", "first supervision wave"));

		Assert.Single(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		await RunIndexWorkers(WorkerCount, WorkerCount, _ =>
		{
			core.Supervise();
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(WorkerCount, "quiet", "second supervision wave"));

		Assert.Single(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
		Assert.Equal(TerminalKind.Poisoned, core.Find("quiet")!.Terminal);
		Assert.True(Assert.Single(core.Find("quiet")!.Obligations).Retired);
	}

	/// <summary>
	/// Expires unfinished messages while other messages complete, including fresh work published after the clock moves.
	/// </summary>
	/// <remarks>
	/// Twenty-four messages are left unfinished with a short TTL. Twenty-four are completed first.
	/// After the clock reaches the short TTL, supervision runs beside publication and completion of a fresh group.
	/// Another fresh group is then completed with no further clock movement. A pass means this workload made progress.
	/// It does not mean the core is free of deadlocks or has unlimited throughput.
	/// </remarks>
	[Fact]
	public async Task MixedHealthyAndPoisoned_LeavesTheCoreUsable()
	{
		const int groupSize = 24;
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var unfinished = await PublishGroup(core, "old", groupSize, TimeSpan.FromTicks(10), "unfinished-payload");
		var healthy = await PublishGroup(core, "healthy", groupSize, TimeSpan.FromTicks(100), "healthy-payload");
		await CompleteGroup(core, healthy, "success", TimeSpan.FromTicks(80));
		clock.Advance(TimeSpan.FromTicks(10));
		var fresh = new ConcurrentDictionary<string, ExpectedWork>(StringComparer.Ordinal);
		await Task.WhenAll(
			FinishWithin(
				RunIndexWorkers(WorkerCount, WorkerCount, _ =>
				{
					core.Supervise();
					return Task.CompletedTask;
				}, OperationWatchdog, Diagnostic(groupSize, "mixed", "supervision beside fresh work")),
				OperationWatchdog,
				Diagnostic(groupSize, "mixed", "supervision watchdog")),
			FinishWithin(PublishInto(core, fresh, "fresh", groupSize, TimeSpan.FromTicks(50), "fresh-payload"), OperationWatchdog, Diagnostic(groupSize, "fresh", "fresh publication")));

		await CompleteGroup(core, fresh, "success", TimeSpan.FromTicks(40));
		var after = core.SnapshotOutcomes();
		AssertPoisonedUnfinished(core, after, unfinished, groupSize);
		AssertCompletedGroup(core, after, healthy, "success", groupSize);
		AssertCompletedGroup(core, after, fresh, "success", groupSize);

		var afterward = await PublishGroup(core, "later", groupSize / 2, TimeSpan.FromTicks(50), "later-payload");
		await CompleteGroup(core, afterward, "error", TimeSpan.FromTicks(40));
		var finalHistory = core.SnapshotOutcomes();
		AssertCompletedGroup(core, finalHistory, afterward, "error", groupSize / 2);
		AssertPoisonedUnfinished(core, finalHistory, unfinished, groupSize);
		Assert.Equal(clock.MonotonicTime, TimeSpan.FromTicks(10));
	}

	/// <summary>
	/// Rejects unknown, repeated, expired, and late reports without changing unrelated accounting.
	/// </summary>
	/// <remarks>
	/// Sixty-four parents are split into four groups of sixteen. Unknown-target and unknown-obligation rejections consume the report identity.
	/// The obligation group then registers added. keep and added each receive their own accepted report while the clock is still zero.
	/// A later clock advance of four ticks expires the short reports without discharging their targets. A further advance of sixteen ticks
	/// reaches the poison group's TTL of twenty. Reports after completion or poison return TerminalMessage. Unrelated lifetimes stay unchanged.
	/// </remarks>
	[Fact]
	public async Task RejectedAndLateReports_DoNotChangeUnrelatedAccounting()
	{
		const int groupSize = 16;
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var unknownTargets = await PublishGroup(core, "target", groupSize, TimeSpan.FromTicks(200), "target-payload");
		var unknownObligations = await PublishGroup(core, "obligation", groupSize, TimeSpan.FromTicks(200), "obligation-payload");
		var completed = await PublishGroup(core, "done", groupSize, TimeSpan.FromTicks(200), "done-payload");
		var poisoned = await PublishGroup(core, "poison", groupSize, TimeSpan.FromTicks(20), "poison-payload");

		var unknownTargetIds = unknownTargets.Keys.OrderBy(item => item, StringComparer.Ordinal).ToList();
		var unknownObligationIds = unknownObligations.Keys.OrderBy(item => item, StringComparer.Ordinal).ToList();
		await RunIndexWorkers(WorkerCount, unknownTargetIds.Count, index =>
		{
			var messageId = unknownTargetIds[index];
			var published = core.PublishCompletion(new CompletionReport(ConsumedTargetReport(messageId), "completion", TimeSpan.FromTicks(30), "missing-" + messageId, "keep", "success"));
			Assert.True(published.Accepted, Diagnostic(groupSize, messageId, "unknown-target report was not published"));
			var rejected = core.AcceptCompletion(ConsumedTargetReport(messageId));
			Assert.Equal(MessagingRejection.UnknownMessage, rejected.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "target", "unknown targets"));

		await RunIndexWorkers(WorkerCount, unknownObligationIds.Count, index =>
		{
			var messageId = unknownObligationIds[index];
			var published = core.PublishCompletion(new CompletionReport(ConsumedObligationReport(messageId), "completion", TimeSpan.FromTicks(30), messageId, "missing", "success"));
			Assert.True(published.Accepted, Diagnostic(groupSize, messageId, "unknown-obligation report was not published"));
			var rejected = core.AcceptCompletion(ConsumedObligationReport(messageId));
			Assert.Equal(MessagingRejection.UnknownObligation, rejected.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "obligation", "unknown obligations"));

		await RunIndexWorkers(WorkerCount, unknownTargetIds.Count, index =>
		{
			var retry = core.AcceptCompletion(ConsumedTargetReport(unknownTargetIds[index]));
			Assert.Equal(MessagingRejection.DuplicateCompletionReport, retry.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "target", "repeated unknown target"));
		await RunIndexWorkers(WorkerCount, unknownObligationIds.Count, index =>
		{
			var retry = core.AcceptCompletion(ConsumedObligationReport(unknownObligationIds[index]));
			Assert.Equal(MessagingRejection.DuplicateCompletionReport, retry.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "obligation", "repeated unknown obligation"));

		foreach (var messageId in unknownObligationIds)
		{
			Assert.False(Assert.Single(core.Find(messageId)!.Obligations).Discharged);
			var registered = core.RegisterObligation(messageId, "added");
			Assert.True(registered.Accepted, Diagnostic(groupSize, messageId, "added obligation was rejected"));
		}

		// keep and added are separate registered obligations. Each needs its own accepted report.
		await CompleteGroup(core, unknownObligations, "success", TimeSpan.FromTicks(30));
		await CompleteGroup(core, unknownObligations, "success", TimeSpan.FromTicks(30), "added", "c-added-");
		await RunIndexWorkers(WorkerCount, unknownTargetIds.Count, index =>
		{
			var messageId = unknownTargetIds[index];
			var published = core.PublishCompletion(new CompletionReport("c-short-" + messageId, "completion", TimeSpan.FromTicks(4), messageId, "keep", "success"));
			Assert.True(published.Accepted, Diagnostic(groupSize, messageId, "short report was not published"));
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "target", "short report publication"));
		clock.Advance(TimeSpan.FromTicks(4));
		await RunIndexWorkers(WorkerCount, unknownTargetIds.Count, index =>
		{
			var rejected = core.AcceptCompletion("c-short-" + unknownTargetIds[index]);
			Assert.Equal(MessagingRejection.ExpiredCompletionReport, rejected.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "target", "expired reports"));
		foreach (var messageId in unknownTargetIds)
		{
			Assert.False(Assert.Single(core.Find(messageId)!.Obligations).Discharged, Diagnostic(groupSize, messageId, "expired report discharged work"));
		}

		await CompleteGroup(core, unknownTargets, "success", TimeSpan.FromTicks(40));
		await CompleteGroup(core, completed, "error", TimeSpan.FromTicks(40));
		var completedIds = completed.Keys.ToList();
		await RunIndexWorkers(WorkerCount, completedIds.Count, index =>
		{
			var messageId = completedIds[index];
			var published = core.PublishCompletion(new CompletionReport("c-late-" + messageId, "completion", TimeSpan.FromTicks(40), messageId, "keep", "success"));
			Assert.True(published.Accepted, Diagnostic(groupSize, messageId, "late report was not published"));
			var rejected = core.AcceptCompletion("c-late-" + messageId);
			Assert.Equal(MessagingRejection.TerminalMessage, rejected.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "done", "late reports after completion"));

		clock.Advance(TimeSpan.FromTicks(16));
		var supervised = core.Supervise();
		var retiredNow = supervised.Outcomes.OfType<MessagePoisonedOutcome>().Where(item => poisoned.ContainsKey(item.MessageId)).ToList();
		Assert.Equal(poisoned.Count, retiredNow.Count);
		var poisonedIds = poisoned.Keys.ToList();
		await RunIndexWorkers(WorkerCount, poisonedIds.Count, index =>
		{
			var messageId = poisonedIds[index];
			var published = core.PublishCompletion(new CompletionReport("c-after-poison-" + messageId, "completion", TimeSpan.FromTicks(40), messageId, "keep", "success"));
			Assert.True(published.Accepted, Diagnostic(groupSize, messageId, "post-poison report was not published"));
			var rejected = core.AcceptCompletion("c-after-poison-" + messageId);
			Assert.Equal(MessagingRejection.TerminalMessage, rejected.Rejection);
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(groupSize, "poison", "late reports after poison"));

		var history = core.SnapshotOutcomes();
		AssertCompletedGroup(core, history, unknownTargets, "success", groupSize);
		AssertCompletedGroup(core, history, completed, "error", groupSize);
		foreach (var messageId in unknownObligationIds)
		{
			var message = core.Find(messageId);
			Assert.NotNull(message);
			Assert.Equal(TerminalKind.Completed, message.Terminal);
			Assert.Contains(message.Obligations, item => item.ObligationId == "keep" && item.Discharged && item.ResultCode == "success");
			Assert.Contains(message.Obligations, item => item.ObligationId == "added" && item.Discharged && item.CompletionReportId == "c-added-" + messageId + "/added");
		}

		AssertPoisonedUnfinished(core, history, poisoned, groupSize);
		Assert.All(unknownTargets.Values, item => AssertUnchangedLifetime(core, item, groupSize));
		Assert.All(completed.Values, item => AssertUnchangedLifetime(core, item, groupSize));
	}

	/// <summary>
	/// Runs one steady-clock flood and checks the ledger at the publication and completion boundaries.
	/// </summary>
	/// <param name="messageCount">How many work messages to accept.</param>
	/// <remarks>
	/// The clock is never advanced. Each message has obligations a and b, with success on a and error on b.
	/// Every eighth obligation is also submitted once more with the same report identity and a losing result code.
	/// History is copied after work publication, after report publication, and after acceptance settles.
	/// The settlement requires one discharge per accepted obligation, one Completed outcome, and no Poisoned outcome.
	/// </remarks>
	private static async Task RunSteadyFlood(int messageCount)
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var startedAt = clock.MonotonicTime;
		var ttl = TimeSpan.FromTicks(100_000);
		var works = new ConcurrentDictionary<string, ExpectedWork>(StringComparer.Ordinal);
		await RunIndexWorkers(WorkerCount, messageCount, index =>
		{
			var messageId = WorkId(index);
			var result = core.Publish(new WorkPublication(messageId, "work", "subject", Payload(index), ttl, ObligationNames));
			Assert.True(result.Accepted, Diagnostic(messageCount, messageId, "work publication was not accepted"));
			Assert.NotNull(result.Message);
			Assert.True(works.TryAdd(messageId, new ExpectedWork(result.Message)), Diagnostic(messageCount, messageId, "work ledger already contained this identity"));
			return Task.CompletedTask;
		}, FloodWatchdog, Diagnostic(messageCount, "*", "work publication flood"));

		for (var index = 0; index < Math.Min(WorkerCount, messageCount); index++)
		{
			var messageId = WorkId(index);
			var rejected = core.Publish(new WorkPublication(messageId, "work", "subject", "replacement-payload", TimeSpan.FromTicks(3), ObligationNames));
			Assert.Equal(MessagingRejection.DuplicateIdentity, rejected.Rejection);
			AssertUnchangedLifetime(core, works[messageId], messageCount);
			Assert.Equal(works[messageId].Payload, core.Find(messageId)!.Payload);
		}

		var afterWork = core.SnapshotOutcomes();
		Assert.Equal(works.Count, afterWork.OfType<WorkAcceptedOutcome>().Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count());
		Assert.Empty(afterWork.OfType<ObligationDischargedOutcome>());
		Assert.Empty(afterWork.OfType<MessageCompletedOutcome>());
		Assert.Empty(afterWork.OfType<MessagePoisonedOutcome>());

		var attempts = new List<ReportAttempt>();
		var sequence = 0;
		foreach (var work in works.Values.OrderBy(item => item.MessageId, StringComparer.Ordinal))
		{
			for (var obligation = 0; obligation < ObligationsPerMessage; obligation++)
			{
				var obligationId = ObligationNames[obligation];
				var reportId = ReportId(work.MessageId, obligationId);
				attempts.Add(new ReportAttempt(reportId, work.MessageId, obligationId, obligation == 0 ? "success" : "error"));
				if (sequence % 8 == 0)
				{
					attempts.Add(new ReportAttempt(reportId, work.MessageId, obligationId, "duplicate-loser"));
				}

				sequence++;
			}
		}

		Shuffle(attempts, Seed);
		var acceptedReports = new ConcurrentDictionary<string, ReportAttempt>(StringComparer.Ordinal);
		await RunIndexWorkers(WorkerCount, attempts.Count, index =>
		{
			var attempt = attempts[index];
			var result = core.PublishCompletion(new CompletionReport(
				attempt.ReportId,
				"completion",
				ttl,
				attempt.MessageId,
				attempt.ObligationId,
				attempt.ResultCode));
			if (result.Accepted)
			{
				Assert.True(
					acceptedReports.TryAdd(attempt.ReportId, attempt),
					Diagnostic(messageCount, attempt.ReportId, "two publications were accepted for one report"));
			}
			else
			{
				Assert.True(
					result.Rejection == MessagingRejection.DuplicateIdentity,
					Diagnostic(messageCount, attempt.ReportId, "unexpected report rejection " + result.Rejection));
			}

			return Task.CompletedTask;
		}, FloodWatchdog, Diagnostic(messageCount, "*", "report publication flood"));

		Assert.Equal(works.Count * ObligationsPerMessage, acceptedReports.Count);
		var afterReports = core.SnapshotOutcomes();
		Assert.Equal(acceptedReports.Count, afterReports.OfType<CompletionReportPublishedOutcome>().Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count());
		Assert.Empty(afterReports.OfType<ObligationDischargedOutcome>());

		var acceptOrder = acceptedReports.Values.ToList();
		Shuffle(acceptOrder, Seed);
		await RunIndexWorkers(WorkerCount, acceptOrder.Count, index =>
		{
			var attempt = acceptOrder[index];
			var result = core.AcceptCompletion(attempt.ReportId);
			Assert.True(result.Accepted, Diagnostic(messageCount, attempt.ReportId, "accepted report did not discharge " + attempt.MessageId));
			return Task.CompletedTask;
		}, FloodWatchdog, Diagnostic(messageCount, "*", "completion flood"));

		var duplicateAccepts = acceptOrder.Where((_, index) => index % 8 == 0).ToList();
		await RunIndexWorkers(WorkerCount, duplicateAccepts.Count, index =>
		{
			var attempt = duplicateAccepts[index];
			var result = core.AcceptCompletion(attempt.ReportId);
			Assert.True(
				result.Rejection == MessagingRejection.DuplicateCompletionReport,
				Diagnostic(messageCount, attempt.ReportId, "repeated acceptance returned " + result.Rejection));
			return Task.CompletedTask;
		}, FloodWatchdog, Diagnostic(messageCount, "*", "duplicate acceptance"));

		Assert.Equal(startedAt, clock.MonotonicTime);
		AssertSteadySettlement(core, core.SnapshotOutcomes(), works, acceptedReports, messageCount);
	}

	/// <summary>
	/// Runs one completion, registration, and expiration race, then checks the legal terminal outcome.
	/// </summary>
	/// <param name="iteration">The race number, included in identities and failure text.</param>
	/// <remarks>
	/// Three workers wait on one asynchronous start signal. One publishes and accepts the base report.
	/// One registers extra. One advances the manual clock by the work TTL and supervises.
	/// After they finish, the test thread advances one more tick and supervises so an unfinished parent still reaches a terminal value.
	/// The 45-second watchdog fails the iteration if those workers stay stuck. It does not expire the message.
	/// </remarks>
	private static async Task RunOneRace(int iteration)
	{
		var messageId = "race-" + iteration.ToString();
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		PublishParent(core, messageId, TimeSpan.FromTicks(10), "base");
		MessagingResult? registration = null;
		MessagingResult? completion = null;
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var workers = new[]
		{
			Task.Run(async () =>
			{
				await start.Task;
				var published = core.PublishCompletion(new CompletionReport(ReportId(messageId, "base"), "completion", TimeSpan.FromTicks(100), messageId, "base", "success"));
				Assert.True(published.Accepted, Diagnostic(iteration, messageId, "race report was not published"));
				completion = core.AcceptCompletion(ReportId(messageId, "base"));
			}),
			Task.Run(async () =>
			{
				await start.Task;
				registration = core.RegisterObligation(messageId, "extra");
			}),
			Task.Run(async () =>
			{
				await start.Task;
				clock.Advance(TimeSpan.FromTicks(10));
				core.Supervise();
			})
		};
		start.TrySetResult();
		await FinishWithin(Task.WhenAll(workers), OperationWatchdog, Diagnostic(iteration, messageId, "race"));
		clock.Advance(TimeSpan.FromTicks(1));
		core.Supervise();
		if (registration == null || completion == null)
		{
			Assert.Fail(Diagnostic(iteration, messageId, "a race worker did not record its result"));
			return;
		}

		AssertRaceOutcome(core, messageId, completion.Accepted, registration.Accepted, iteration);
	}

	/// <summary>
	/// Checks that the race left exactly one legal terminal outcome and that later calls do not revive it.
	/// </summary>
	/// <param name="core">The core for this iteration.</param>
	/// <param name="messageId">The raced parent.</param>
	/// <param name="baseDischarged">True when this test's completion call was accepted.</param>
	/// <param name="extraAccepted">True when this test's registration call was accepted.</param>
	/// <param name="iteration">The race number, for the failure text.</param>
	/// <remarks>
	/// An accepted registration is present. A rejected registration adds nothing.
	/// Completed is legal only when extra was not accepted and base was discharged. Every obligation is then discharged and not retired.
	/// Poisoned is the other legal terminal value. Each accepted obligation is then either discharged or retired, not both and not neither.
	/// A parent whose only obligation was discharged must not be Poisoned. A later accept or registration cannot revive the parent or close it again.
	/// </remarks>
	private static void AssertRaceOutcome(InMemoryMessagingCore core, string messageId, bool baseDischarged, bool extraAccepted, int iteration)
	{
		var history = core.SnapshotOutcomes();
		var message = core.Find(messageId);
		Assert.NotNull(message);
		Assert.NotNull(message.Terminal);
		Assert.Single(TerminalOutcomes(history, messageId));
		var extra = message.Obligations.SingleOrDefault(item => item.ObligationId == "extra");
		if (extraAccepted)
		{
			Assert.True(extra != null, Diagnostic(iteration, messageId, "accepted registration is missing"));
		}
		else
		{
			Assert.True(extra == null, Diagnostic(iteration, messageId, "rejected registration was stored"));
		}

		var baseObligation = Assert.Single(message.Obligations, item => item.ObligationId == "base");
		Assert.True(baseObligation.Discharged == baseDischarged, Diagnostic(iteration, messageId, "base discharge does not match the accepted completion"));
		Assert.True(baseObligation.Retired != baseDischarged, Diagnostic(iteration, messageId, "base retirement does not match the accepted completion"));
		if (message.Terminal == TerminalKind.Completed)
		{
			Assert.False(extraAccepted, Diagnostic(iteration, messageId, "Completed parent still had an undischarged accepted obligation"));
			Assert.True(baseDischarged, Diagnostic(iteration, messageId, "Completed parent had no accepted discharge"));
			Assert.All(message.Obligations, item => Assert.True(item.Discharged && !item.Retired));
			Assert.IsType<MessageCompletedOutcome>(Assert.Single(TerminalOutcomes(history, messageId)));
		}
		else
		{
			Assert.Equal(TerminalKind.Poisoned, message.Terminal);
			Assert.False(baseDischarged && !extraAccepted, Diagnostic(iteration, messageId, "a fully discharged parent was Poisoned"));
			Assert.All(message.Obligations, item => Assert.True(item.Discharged ^ item.Retired));
			Assert.IsType<MessagePoisonedOutcome>(Assert.Single(TerminalOutcomes(history, messageId)));
		}

		var before = Describe(message);
		var repeated = core.AcceptCompletion(ReportId(messageId, "base"));
		Assert.Equal(MessagingRejection.DuplicateCompletionReport, repeated.Rejection);
		var later = core.RegisterObligation(messageId, "later");
		Assert.Equal(MessagingRejection.TerminalMessage, later.Rejection);
		var after = core.Find(messageId);
		Assert.NotNull(after);
		Assert.Equal(before, Describe(after));
		Assert.Single(TerminalOutcomes(core.SnapshotOutcomes(), messageId));
		Assert.DoesNotContain(after.Obligations, item => item.ObligationId == "later");
	}

	/// <summary>
	/// Publishes a group concurrently and records each accepted snapshot.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="prefix">Identity prefix for this group.</param>
	/// <param name="count">How many messages to publish.</param>
	/// <param name="ttl">Positive work TTL.</param>
	/// <param name="payloadPrefix">Payload prefix recorded in the ledger.</param>
	/// <returns>The accepted messages, keyed by identity.</returns>
	private static async Task<ConcurrentDictionary<string, ExpectedWork>> PublishGroup(
		InMemoryMessagingCore core,
		string prefix,
		int count,
		TimeSpan ttl,
		string payloadPrefix)
	{
		var works = new ConcurrentDictionary<string, ExpectedWork>(StringComparer.Ordinal);
		await PublishInto(core, works, prefix, count, ttl, payloadPrefix);
		return works;
	}

	/// <summary>
	/// Publishes into an existing ledger. Callers that already started other workers use this directly.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="works">The ledger that receives accepted snapshots.</param>
	/// <param name="prefix">Identity prefix.</param>
	/// <param name="count">How many messages to publish.</param>
	/// <param name="ttl">Positive work TTL.</param>
	/// <param name="payloadPrefix">Payload prefix.</param>
	/// <remarks>
	/// Each message starts with one obligation, keep. The ledger stores the snapshot returned at acceptance.
	/// Eight workers share one start signal. The caller arranges the clock before this method runs.
	/// </remarks>
	private static Task PublishInto(
		InMemoryMessagingCore core,
		ConcurrentDictionary<string, ExpectedWork> works,
		string prefix,
		int count,
		TimeSpan ttl,
		string payloadPrefix)
	{
		return RunIndexWorkers(WorkerCount, count, index =>
		{
			var messageId = prefix + "-" + index.ToString();
			var result = core.Publish(new WorkPublication(messageId, "work", "subject", payloadPrefix + "-" + index.ToString(), ttl, new[] { "keep" }));
			Assert.True(result.Accepted, Diagnostic(count, messageId, "group publication failed"));
			Assert.NotNull(result.Message);
			Assert.True(works.TryAdd(messageId, new ExpectedWork(result.Message)), Diagnostic(count, messageId, "group ledger collision"));
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(count, prefix, "group publication"));
	}

	/// <summary>
	/// Completes one obligation on every message in a ledger. The report identity is derived from the message identity.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="works">Messages this test has accepted.</param>
	/// <param name="resultCode">The result code that must remain attached to the obligation.</param>
	/// <param name="reportTtl">Positive report TTL.</param>
	/// <param name="obligationId">The obligation to discharge. The default is keep.</param>
	/// <param name="reportPrefix">The report identity prefix. The default is c-. The identity is that prefix, the message identity, a slash, and the obligation identity.</param>
	/// <remarks>
	/// One call discharges one obligation. A parent with two registered obligations needs two calls and two report identities.
	/// The report is published and accepted in the same worker step, so its elapsed time is the clock value already arranged by the caller.
	/// </remarks>
	private static Task CompleteGroup(
		InMemoryMessagingCore core,
		ConcurrentDictionary<string, ExpectedWork> works,
		string resultCode,
		TimeSpan reportTtl,
		string obligationId = "keep",
		string? reportPrefix = null)
	{
		var ids = works.Keys.OrderBy(item => item, StringComparer.Ordinal).ToList();
		Shuffle(ids, Seed);
		return RunIndexWorkers(WorkerCount, ids.Count, index =>
		{
			var messageId = ids[index];
			var reportId = (reportPrefix ?? "c-") + messageId + "/" + obligationId;
			var published = core.PublishCompletion(new CompletionReport(reportId, "completion", reportTtl, messageId, obligationId, resultCode));
			Assert.True(published.Accepted, Diagnostic(ids.Count, reportId, "group report was not published"));
			var accepted = core.AcceptCompletion(reportId);
			Assert.True(accepted.Accepted, Diagnostic(ids.Count, reportId, "group report did not discharge"));
			return Task.CompletedTask;
		}, OperationWatchdog, Diagnostic(ids.Count, obligationId, "group completion"));
	}

	/// <summary>
	/// Starts a fixed worker count. Each worker takes the next index until <paramref name="itemCount"/> is exhausted.
	/// </summary>
	/// <param name="workerCount">The bounded worker count.</param>
	/// <param name="itemCount">How many indexes to hand out.</param>
	/// <param name="work">The operation for one index. It must not copy the whole outcome history.</param>
	/// <param name="watchdog">How long the whole group may run before the test fails as stuck.</param>
	/// <param name="diagnostic">Seed, size, and identity text included if the watchdog or a worker fails.</param>
	/// <remarks>
	/// Every worker waits on one asynchronous start signal, then takes the next index with an interlocked increment.
	/// The caller does not block on a per-message task. The watchdog covers the whole group.
	/// </remarks>
	private static async Task RunIndexWorkers(int workerCount, int itemCount, Func<int, Task> work, TimeSpan watchdog, string diagnostic)
	{
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var next = -1;
		var workers = new Task[workerCount];
		for (var worker = 0; worker < workerCount; worker++)
		{
			workers[worker] = Task.Run(async () =>
			{
				await start.Task;
				while (true)
				{
					var index = Interlocked.Increment(ref next);
					if (index >= itemCount)
					{
						return;
					}

					await work(index);
				}
			});
		}

		start.TrySetResult();
		await FinishWithin(Task.WhenAll(workers), watchdog, diagnostic);
	}

	/// <summary>
	/// Fails the test when <paramref name="work"/> does not finish before <paramref name="limit"/>.
	/// </summary>
	/// <param name="work">The operation being supervised.</param>
	/// <param name="limit">The wall-clock hang limit. It is not a message TTL.</param>
	/// <param name="diagnostic">Context included in the failure.</param>
	/// <remarks>
	/// The wait is <see cref="Task.WhenAny(Task, Task)"/> against a delay. The delay is wall-clock hang detection.
	/// It does not advance the manual clock and it does not decide message expiration.
	/// </remarks>
	private static async Task FinishWithin(Task work, TimeSpan limit, string diagnostic)
	{
		var finished = await Task.WhenAny(work, Task.Delay(limit));
		if (!ReferenceEquals(finished, work))
		{
			Assert.Fail("Watchdog expired after " + limit.TotalSeconds.ToString("0") + "s. This is test supervision, not message TTL. " + diagnostic);
		}

		await work;
	}

	/// <summary>
	/// Reorders <paramref name="items"/> with a fixed seed.
	/// </summary>
	/// <typeparam name="T">The list element.</typeparam>
	/// <param name="items">The list to reorder in place.</param>
	/// <param name="seed">The shuffle seed.</param>
	private static void Shuffle<T>(IList<T> items, int seed)
	{
		var random = new Random(seed);
		for (var index = items.Count - 1; index > 0; index--)
		{
			var swap = random.Next(index + 1);
			(items[index], items[swap]) = (items[swap], items[index]);
		}
	}

	/// <summary>
	/// Checks a completed flood against the ledger and against one history snapshot.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="history">History captured after completion settled.</param>
	/// <param name="works">Snapshots recorded when work publication was accepted.</param>
	/// <param name="reports">Report attempts recorded when report publication was accepted.</param>
	/// <param name="messageCount">The flood size, for failure text.</param>
	private static void AssertSteadySettlement(
		InMemoryMessagingCore core,
		IReadOnlyList<LifecycleOutcome> history,
		ConcurrentDictionary<string, ExpectedWork> works,
		ConcurrentDictionary<string, ReportAttempt> reports,
		int messageCount)
	{
		var discharges = history.OfType<ObligationDischargedOutcome>()
			.GroupBy(item => item.MessageId + "/" + item.ObligationId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
		var completed = history.OfType<MessageCompletedOutcome>()
			.GroupBy(item => item.MessageId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
		Assert.Empty(history.OfType<MessagePoisonedOutcome>());
		foreach (var work in works.Values)
		{
			var message = core.Find(work.MessageId);
			Assert.NotNull(message);
			Assert.Equal(TerminalKind.Completed, message.Terminal);
			Assert.Equal(work.Payload, message.Payload);
			Assert.Equal(work.TimeToLive, message.TimeToLive);
			Assert.Equal(work.SentAt, message.SentAt);
			Assert.Equal(work.ObligationIds.Length, message.Obligations.Count);
			foreach (var obligationId in work.ObligationIds)
			{
				var report = reports[ReportId(work.MessageId, obligationId)];
				var obligation = Assert.Single(message.Obligations, item => item.ObligationId == obligationId);
				Assert.True(obligation.Discharged && !obligation.Retired, Diagnostic(messageCount, work.MessageId, obligationId + " was not discharged once"));
				Assert.Equal(report.ResultCode, obligation.ResultCode);
				Assert.Equal(report.ReportId, obligation.CompletionReportId);
				Assert.Single(discharges[work.MessageId + "/" + obligationId]);
			}

			Assert.True(completed.TryGetValue(work.MessageId, out var completedCount) && completedCount == 1, Diagnostic(messageCount, work.MessageId, "Completed outcome count was " + (completed.TryGetValue(work.MessageId, out var seen) ? seen.ToString() : "0")));
		}
	}

	/// <summary>
	/// Checks that every ledger message is Completed with the expected result and original lifetime.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="history">History captured after the phase settled.</param>
	/// <param name="works">Accepted publication snapshots.</param>
	/// <param name="resultCode">The result code this test submitted.</param>
	/// <param name="size">The group size, for failure text.</param>
	private static void AssertCompletedGroup(
		InMemoryMessagingCore core,
		IReadOnlyList<LifecycleOutcome> history,
		ConcurrentDictionary<string, ExpectedWork> works,
		string resultCode,
		int size)
	{
		foreach (var work in works.Values)
		{
			var message = core.Find(work.MessageId);
			Assert.NotNull(message);
			Assert.Equal(TerminalKind.Completed, message.Terminal);
			AssertUnchangedLifetime(core, work, size);
			var obligation = Assert.Single(message.Obligations, item => item.ObligationId == "keep");
			Assert.True(obligation.Discharged && !obligation.Retired, Diagnostic(size, work.MessageId, "completed group obligation was not discharged"));
			Assert.Equal(resultCode, obligation.ResultCode);
			Assert.Single(TerminalOutcomes(history, work.MessageId).OfType<MessageCompletedOutcome>());
			Assert.Empty(TerminalOutcomes(history, work.MessageId).OfType<MessagePoisonedOutcome>());
		}
	}

	/// <summary>
	/// Checks that unfinished messages were poisoned once and their original obligation was retired rather than discharged.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="history">History captured after supervision settled.</param>
	/// <param name="works">Messages this test left unfinished.</param>
	/// <param name="size">The group size, for failure text.</param>
	private static void AssertPoisonedUnfinished(
		InMemoryMessagingCore core,
		IReadOnlyList<LifecycleOutcome> history,
		ConcurrentDictionary<string, ExpectedWork> works,
		int size)
	{
		foreach (var work in works.Values)
		{
			var message = core.Find(work.MessageId);
			Assert.NotNull(message);
			Assert.Equal(TerminalKind.Poisoned, message.Terminal);
			AssertUnchangedLifetime(core, work, size);
			var obligation = Assert.Single(message.Obligations);
			Assert.True(obligation.Retired && !obligation.Discharged, Diagnostic(size, work.MessageId, "poisoned obligation was discharged or left open"));
			Assert.Single(TerminalOutcomes(history, work.MessageId).OfType<MessagePoisonedOutcome>());
			Assert.Empty(TerminalOutcomes(history, work.MessageId).OfType<MessageCompletedOutcome>());
		}
	}

	/// <summary>
	/// Compares the current snapshot's TTL and send instant with the snapshot taken at publication.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="expected">The publication snapshot.</param>
	/// <param name="size">The workload size, for failure text.</param>
	private static void AssertUnchangedLifetime(InMemoryMessagingCore core, ExpectedWork expected, int size)
	{
		var message = core.Find(expected.MessageId);
		Assert.NotNull(message);
		Assert.True(message.TimeToLive == expected.TimeToLive && message.SentAt == expected.SentAt, Diagnostic(size, expected.MessageId, "TTL or send instant changed"));
	}

	/// <summary>
	/// A work message one tick inside its TTL can still be completed, and poison is absent.
	/// </summary>
	/// <param name="ttl">The work TTL.</param>
	/// <param name="elapsed">The advance, which is one tick short of <paramref name="ttl"/>.</param>
	private static void AssertWorkCompletesBeforeDeadline(TimeSpan ttl, TimeSpan elapsed)
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var published = PublishParent(core, "before", ttl, "only");
		clock.Advance(elapsed);
		Assert.Null(core.Find("before")!.Terminal);
		PublishAndAccept(core, "before", "only", "c-before", "success", TimeSpan.FromTicks(30));
		var message = core.Find("before");
		Assert.Equal(TerminalKind.Completed, message!.Terminal);
		Assert.Equal(published.SentAt, message.SentAt);
		Assert.Equal(published.TimeToLive, message.TimeToLive);
		Assert.Empty(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
	}

	/// <summary>
	/// A work message at or past its TTL is poisoned once and a later report does not discharge or complete it.
	/// </summary>
	/// <param name="ttl">The work TTL.</param>
	/// <param name="elapsed">The advance, equal to or greater than <paramref name="ttl"/>.</param>
	private static void AssertWorkPoisonsAtOrAfterDeadline(TimeSpan ttl, TimeSpan elapsed)
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var published = PublishParent(core, "due", ttl, "only");
		clock.Advance(elapsed);
		Assert.Single(core.Supervise().Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.Empty(core.Supervise().Outcomes.OfType<MessagePoisonedOutcome>());
		var publishedReport = core.PublishCompletion(new CompletionReport("c-due", "completion", TimeSpan.FromTicks(30), "due", "only", "success"));
		Assert.True(publishedReport.Accepted, Diagnostic(1, "due", "poison-boundary report was not published"));
		var rejected = core.AcceptCompletion("c-due");
		Assert.Equal(MessagingRejection.TerminalMessage, rejected.Rejection);
		var message = core.Find("due");
		Assert.Equal(TerminalKind.Poisoned, message!.Terminal);
		Assert.False(Assert.Single(message.Obligations).Discharged);
		Assert.Equal(published.SentAt, message.SentAt);
		Assert.Equal(published.TimeToLive, message.TimeToLive);
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		Assert.Single(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
	}

	/// <summary>
	/// A report one tick inside its own TTL discharges a still-active parent.
	/// </summary>
	/// <param name="reportTtl">The report TTL.</param>
	/// <param name="elapsed">The advance, which is one tick short of <paramref name="reportTtl"/>.</param>
	private static void AssertReportDischargesBeforeDeadline(TimeSpan reportTtl, TimeSpan elapsed)
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		PublishParent(core, "parent", TimeSpan.FromTicks(100), "only");
		var published = core.PublishCompletion(new CompletionReport("c-early", "completion", reportTtl, "parent", "only", "error"));
		Assert.True(published.Accepted, Diagnostic(1, "c-early", "early report was not published"));
		clock.Advance(elapsed);
		var accepted = core.AcceptCompletion("c-early");
		Assert.True(accepted.Accepted, Diagnostic(1, "c-early", "report inside its TTL did not discharge"));
		var message = core.Find("parent");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		var obligation = Assert.Single(message.Obligations);
		Assert.Equal("error", obligation.ResultCode);
		Assert.True(obligation.Discharged && !obligation.Retired);
		Assert.Empty(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
	}

	/// <summary>
	/// A report at or past its own TTL does not discharge a parent whose work TTL is still open.
	/// </summary>
	/// <param name="reportTtl">The report TTL.</param>
	/// <param name="elapsed">The advance, equal to or greater than <paramref name="reportTtl"/>.</param>
	private static void AssertReportExpiresWithoutDischarge(TimeSpan reportTtl, TimeSpan elapsed)
	{
		var clock = new ManualMessagingClock();
		var core = new InMemoryMessagingCore(clock);
		var published = PublishParent(core, "parent", TimeSpan.FromTicks(100), "only");
		var report = core.PublishCompletion(new CompletionReport("c-expired", "completion", reportTtl, "parent", "only", "success"));
		Assert.True(report.Accepted, Diagnostic(1, "c-expired", "expiring report was not published"));
		clock.Advance(elapsed);
		var rejected = core.AcceptCompletion("c-expired");
		Assert.Equal(MessagingRejection.ExpiredCompletionReport, rejected.Rejection);
		var message = core.Find("parent");
		Assert.Null(message!.Terminal);
		Assert.False(Assert.Single(message.Obligations).Discharged);
		Assert.Equal(published.TimeToLive, message.TimeToLive);
		Assert.Equal(published.SentAt, message.SentAt);
		Assert.Empty(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
	}

	/// <summary>
	/// Publishes one parent and returns the ledger entry captured from the acceptance snapshot.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="messageId">The work identity.</param>
	/// <param name="ttl">The positive work TTL.</param>
	/// <param name="obligationId">The single initial obligation.</param>
	/// <returns>The publication snapshot.</returns>
	private static ExpectedWork PublishParent(InMemoryMessagingCore core, string messageId, TimeSpan ttl, string obligationId)
	{
		var result = core.Publish(new WorkPublication(messageId, "work", "subject", "payload", ttl, new[] { obligationId }));
		Assert.True(result.Accepted, Diagnostic(1, messageId, "parent publication failed"));
		Assert.NotNull(result.Message);
		return new ExpectedWork(result.Message);
	}

	/// <summary>
	/// Publishes a report and accepts it immediately. The caller has not advanced the clock since the report's send.
	/// </summary>
	/// <param name="core">The core under test.</param>
	/// <param name="messageId">The work identity.</param>
	/// <param name="obligationId">The obligation identity.</param>
	/// <param name="reportId">The report identity.</param>
	/// <param name="resultCode">The result that must be stored.</param>
	/// <param name="reportTtl">The positive report TTL.</param>
	private static void PublishAndAccept(
		InMemoryMessagingCore core,
		string messageId,
		string obligationId,
		string reportId,
		string resultCode,
		TimeSpan reportTtl)
	{
		var published = core.PublishCompletion(new CompletionReport(reportId, "completion", reportTtl, messageId, obligationId, resultCode));
		Assert.True(published.Accepted, Diagnostic(1, reportId, "controlled report was not published"));
		var accepted = core.AcceptCompletion(reportId);
		Assert.True(accepted.Accepted, Diagnostic(1, reportId, "controlled report did not discharge"));
	}

	/// <summary>Creates a core and the manual clock that test will advance.</summary>
	/// <param name="clock">The clock, still at zero.</param>
	/// <returns>The isolated core.</returns>
	private static InMemoryMessagingCore NewCore(out ManualMessagingClock clock)
	{
		clock = new ManualMessagingClock();
		return new InMemoryMessagingCore(clock);
	}

	/// <summary>Selects terminal evidence for one parent from a history snapshot.</summary>
	/// <param name="history">A history snapshot. Callers capture this at a phase boundary.</param>
	/// <param name="messageId">The parent identity.</param>
	/// <returns>Completed and Poisoned evidence for that parent.</returns>
	private static List<LifecycleOutcome> TerminalOutcomes(IReadOnlyList<LifecycleOutcome> history, string messageId)
	{
		return history.Where(item =>
			(item is MessageCompletedOutcome completed && completed.MessageId == messageId)
			|| (item is MessagePoisonedOutcome poisoned && poisoned.MessageId == messageId)).ToList();
	}

	/// <summary>Formats the terminal value and obligation flags so a later call can be compared without relying on history order.</summary>
	/// <param name="message">The snapshot to describe.</param>
	/// <returns>A stable description.</returns>
	private static string Describe(MessageObservation message)
	{
		var obligations = message.Obligations
			.OrderBy(item => item.ObligationId, StringComparer.Ordinal)
			.Select(item => item.ObligationId + ":" + item.Discharged.ToString() + ":" + item.Retired.ToString() + ":" + item.ResultCode + ":" + item.CompletionReportId);
		return message.Terminal + "|" + string.Join(",", obligations);
	}

	/// <summary>Builds failure text that includes the fixed seed, the workload size, and the identity under test.</summary>
	/// <param name="size">The workload or iteration size.</param>
	/// <param name="identity">The message or report identity.</param>
	/// <param name="detail">What failed.</param>
	/// <returns>The diagnostic text.</returns>
	private static string Diagnostic(int size, string identity, string detail)
	{
		return "seed=" + Seed.ToString() + "; size=" + size.ToString() + "; id=" + identity + "; " + detail;
	}

	/// <summary>Flood work identity for one index.</summary>
	/// <param name="index">The publication index.</param>
	/// <returns>The identity m- followed by that index.</returns>
	private static string WorkId(int index)
	{
		return "m-" + index.ToString();
	}

	/// <summary>Flood payload for one index. A rejected duplicate must not replace it.</summary>
	/// <param name="index">The publication index.</param>
	/// <returns>The payload recorded in the ledger.</returns>
	private static string Payload(int index)
	{
		return "payload-" + index.ToString();
	}

	/// <summary>Report identity shared by a flood obligation and by the race's base report.</summary>
	/// <param name="messageId">The work identity.</param>
	/// <param name="obligationId">The obligation identity.</param>
	/// <returns>The message identity, a slash, and the obligation identity.</returns>
	private static string ReportId(string messageId, string obligationId)
	{
		return messageId + "/" + obligationId;
	}

	/// <summary>Report identity consumed by an unknown-target rejection.</summary>
	/// <param name="messageId">The live parent that the report does not name.</param>
	/// <returns>The consumed report identity.</returns>
	private static string ConsumedTargetReport(string messageId)
	{
		return "c-missing-target-" + messageId;
	}

	/// <summary>Report identity consumed by an unknown-obligation rejection.</summary>
	/// <param name="messageId">The parent whose missing obligation the report names.</param>
	/// <returns>The consumed report identity.</returns>
	private static string ConsumedObligationReport(string messageId)
	{
		return "c-missing-obligation-" + messageId;
	}

	/// <summary>A work snapshot taken when publication was accepted. Later checks use this instead of the core's current bookkeeping.</summary>
	private sealed class ExpectedWork
	{
		/// <summary>Copies the publication snapshot.</summary>
		/// <param name="snapshot">The snapshot returned by the accepted publication.</param>
		public ExpectedWork(MessageObservation snapshot)
		{
			MessageId = snapshot.MessageId;
			Payload = snapshot.Payload;
			TimeToLive = snapshot.TimeToLive;
			SentAt = snapshot.SentAt;
			ObligationIds = snapshot.Obligations.Select(item => item.ObligationId).ToArray();
		}

		/// <summary>Work identity captured at acceptance.</summary>
		public string MessageId { get; }

		/// <summary>Payload captured at acceptance. Later accounting must not replace it.</summary>
		public string? Payload { get; }

		/// <summary>TTL captured at acceptance. Later accounting must not replace it.</summary>
		public TimeSpan TimeToLive { get; }

		/// <summary>Send instant captured at acceptance. Later accounting must not replace it.</summary>
		public TimeSpan SentAt { get; }

		/// <summary>Obligation identities present when publication was accepted.</summary>
		public string[] ObligationIds { get; }
	}

	/// <summary>One report submission. The flood ledger keeps the submission whose publication was accepted.</summary>
	/// <param name="ReportId">The report identity.</param>
	/// <param name="MessageId">The work identity named by the report.</param>
	/// <param name="ObligationId">The obligation identity named by the report.</param>
	/// <param name="ResultCode">The result code submitted with this attempt.</param>
	private sealed record ReportAttempt(string ReportId, string MessageId, string ObligationId, string ResultCode);
}
