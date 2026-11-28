// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;
using System.Collections.ObjectModel;
using PluralBridge.Api.Messaging;

namespace PluralBridge.Api.Tests.Messaging;

/// <summary>
/// Focused tests for the isolated in-memory core.
/// </summary>
/// <remarks>
/// Each test uses <see cref="ManualMessagingClock"/> and advances it by ticks. None sleep.
/// The core is not connected to a host, database, or subscriber. Flood testing is a later task.
/// Permitted terminal evidence is one Completed outcome or one Poisoned outcome, never both.
/// </remarks>
public sealed class InMemoryMessagingCoreTests
{
	/// <summary>
	/// Two obligations may be discharged in either order. The message stays active after the first
	/// and becomes Completed only after the second, with both result codes kept.
	/// </summary>
	[Fact]
	public void TwoObligations_CompleteInEitherOrder()
	{
		var first = RunTwoObligations("a", "b");
		var second = RunTwoObligations("b", "a");

		AssertCompleted(first, "a", "success", "b", "success");
		AssertCompleted(second, "b", "success", "a", "success");
	}

	/// <summary>
	/// An error result code discharges the obligation and still allows Completed.
	/// The publication TTL and send instant stay at the values captured before the clock is touched.
	/// </summary>
	[Fact]
	public void ErrorResult_StillPermitsCompleted()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(50), "only");
		var sent = core.Find("m");
		Assert.NotNull(sent);

		Accept(core, "c1", "m", "only", "error", TimeSpan.FromTicks(10));

		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		var obligation = Assert.Single(message.Obligations);
		Assert.Equal("error", obligation.ResultCode);
		Assert.True(obligation.Discharged);
		Assert.False(obligation.Retired);
		Assert.Equal(sent.TimeToLive, message.TimeToLive);
		Assert.Equal(sent.SentAt, message.SentAt);
		Assert.Equal(TimeSpan.Zero, clock.MonotonicTime);
	}

	/// <summary>
	/// Repeating the same report, and sending another report after the parent is Completed, does not discharge twice.
	/// The second identity receives TerminalMessage because the parent is already terminal. Exactly one discharge and one Completed outcome are retained.
	/// </summary>
	[Fact]
	public void DuplicateCompletion_DoesNotDischargeTwice()
	{
		var core = NewCore(out _);
		PublishWork(core, "m", TimeSpan.FromTicks(50), "only");
		PublishReport(core, "c1", "m", "only", "success", TimeSpan.FromTicks(20));

		var first = core.AcceptCompletion("c1");
		var second = core.AcceptCompletion("c1");
		PublishReport(core, "c2", "m", "only", "success", TimeSpan.FromTicks(20));
		var afterCompletion = core.AcceptCompletion("c2");

		Assert.True(first.Accepted);
		Assert.Equal(MessagingRejection.DuplicateCompletionReport, second.Rejection);
		Assert.Equal(MessagingRejection.TerminalMessage, afterCompletion.Rejection);
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		Assert.Equal("c1", Assert.Single(message.Obligations).CompletionReportId);
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
		Assert.Single(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
	}

	/// <summary>
	/// A second report for an obligation that is already discharged returns ObligationAlreadyDischarged while another obligation is still open.
	/// The parent stays active. There is one discharge and no terminal outcome.
	/// </summary>
	[Fact]
	public void SecondReport_WhileAnotherObligationRemains_IsObligationAlreadyDischarged()
	{
		var core = NewCore(out _);
		PublishWork(core, "m", TimeSpan.FromTicks(50), "done", "open");
		Accept(core, "c1", "m", "done", "success", TimeSpan.FromTicks(20));
		PublishReport(core, "c2", "m", "done", "success", TimeSpan.FromTicks(20));

		var second = core.AcceptCompletion("c2");

		Assert.Equal(MessagingRejection.ObligationAlreadyDischarged, second.Rejection);
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Null(message.Terminal);
		Assert.Contains(message.Obligations, item => item.ObligationId == "done" && item.CompletionReportId == "c1" && item.Discharged);
		Assert.Contains(message.Obligations, item => item.ObligationId == "open" && !item.Discharged);
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
	}

	/// <summary>
	/// The same obligation identity on two work messages is not one obligation.
	/// Discharging it on the left message leaves the right message active and undischarged.
	/// </summary>
	[Fact]
	public void Completion_DoesNotDischargeAnotherMessagesObligation()
	{
		var core = NewCore(out _);
		PublishWork(core, "left", TimeSpan.FromTicks(50), "shared");
		PublishWork(core, "right", TimeSpan.FromTicks(50), "shared");

		Accept(core, "c-left", "left", "shared", "success", TimeSpan.FromTicks(10));

		var left = core.Find("left");
		var right = core.Find("right");
		Assert.NotNull(left);
		Assert.NotNull(right);
		Assert.Equal(TerminalKind.Completed, left.Terminal);
		Assert.Null(right.Terminal);
		Assert.False(Assert.Single(right.Obligations).Discharged);
	}

	/// <summary>
	/// A completion report whose own TTL has elapsed, including the equal-tick boundary, does not discharge the work obligation.
	/// The work message remains active because its longer TTL has not elapsed.
	/// </summary>
	[Fact]
	public void ExpiredCompletionReport_DoesNotDischargeOriginalWork()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(100), "only");
		PublishReport(core, "c1", "m", "only", "success", TimeSpan.FromTicks(5));

		clock.Advance(TimeSpan.FromTicks(5));
		var accepted = core.AcceptCompletion("c1");

		Assert.Equal(MessagingRejection.ExpiredCompletionReport, accepted.Rejection);
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Null(message.Terminal);
		Assert.False(Assert.Single(message.Obligations).Discharged);
		Assert.Equal(TimeSpan.FromTicks(100), message.TimeToLive);
	}

	/// <summary>
	/// Supervision at the work TTL boundary records Poisoned once. A second pass adds no further poison and no Completed outcome.
	/// Send instant and TTL remain the publication values.
	/// </summary>
	[Fact]
	public void OriginalExpiration_ProducesPoisonedExactlyOnce()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(8), "only");
		var sent = core.Find("m");
		Assert.NotNull(sent);

		clock.Advance(TimeSpan.FromTicks(8));
		var first = core.Supervise();
		var second = core.Supervise();

		Assert.Single(first.Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.Empty(second.Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Poisoned, message.Terminal);
		Assert.Equal(sent.SentAt, message.SentAt);
		Assert.Equal(sent.TimeToLive, message.TimeToLive);
	}

	/// <summary>
	/// Poison cleanup retires the remaining obligation after one obligation was already discharged.
	/// The terminal outcome is Poisoned. Completed is absent even though one obligation had reached a result.
	/// </summary>
	[Fact]
	public void PoisonCleanup_NeverProducesCompleted()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(4), "one", "two");
		Accept(core, "c1", "m", "one", "success", TimeSpan.FromTicks(4));

		clock.Advance(TimeSpan.FromTicks(4));
		var supervised = core.Supervise();

		Assert.Empty(supervised.Outcomes.OfType<MessageCompletedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
		var poisoned = Assert.Single(supervised.Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.Equal("two", Assert.Single(poisoned.RetiredObligationIds));
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Poisoned, message.Terminal);
		Assert.Contains(message.Obligations, item => item.ObligationId == "one" && item.Discharged && !item.Retired);
		Assert.Contains(message.Obligations, item => item.ObligationId == "two" && item.Retired && !item.Discharged);
	}

	/// <summary>
	/// A completion report that is still inside its own TTL cannot discharge or revive a work message that supervision has already poisoned.
	/// The rejection is TerminalMessage.
	/// </summary>
	[Fact]
	public void LateCompletion_CannotReviveTerminalMessage()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(6), "only");
		PublishReport(core, "c1", "m", "only", "success", TimeSpan.FromTicks(30));
		clock.Advance(TimeSpan.FromTicks(6));
		core.Supervise();

		var late = core.AcceptCompletion("c1");

		Assert.Equal(MessagingRejection.TerminalMessage, late.Rejection);
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Poisoned, message.Terminal);
		Assert.False(Assert.Single(message.Obligations).Discharged);
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
	}

	/// <summary>
	/// An obligation registered while the message is active survives completion of the older obligation.
	/// The message becomes Completed only after the added obligation is discharged too.
	/// </summary>
	[Fact]
	public void AdditionalRegistration_DuringActiveProcessing_RemainsAccountedFor()
	{
		var core = NewCore(out _);
		PublishWork(core, "m", TimeSpan.FromTicks(40), "first");
		var registered = core.RegisterObligation("m", "second");
		Accept(core, "c1", "m", "first", "success", TimeSpan.FromTicks(10));

		Assert.True(registered.Accepted);
		var midway = core.Find("m");
		Assert.NotNull(midway);
		Assert.Null(midway.Terminal);
		Assert.Contains(midway.Obligations, item => item.ObligationId == "second" && !item.Discharged);

		Accept(core, "c2", "m", "second", "success", TimeSpan.FromTicks(10));
		var done = core.Find("m");
		Assert.NotNull(done);
		Assert.Equal(TerminalKind.Completed, done.Terminal);
		Assert.Equal(2, done.Obligations.Count);
		Assert.All(done.Obligations, item => Assert.True(item.Discharged));
	}

	/// <summary>
	/// Unknown-target rejection consumes that report identity. A later acceptance is DuplicateCompletionReport and does not discharge.
	/// A different report still has to pass its own TTL and the work message's TTL. An expired report does not discharge.
	/// A report that meets an already expired work message returns TerminalMessage. A new report against a new active message can complete it.
	/// </summary>
	[Fact]
	public void UnknownTarget_RejectionIsFinal_AndANewReportObeysBothTtlChecks()
	{
		var core = NewCore(out var clock);
		PublishReport(core, "c-missing", "m", "only", "success", TimeSpan.FromTicks(40));
		var missing = core.AcceptCompletion("c-missing");
		Assert.Equal(MessagingRejection.UnknownMessage, missing.Rejection);

		PublishWork(core, "m", TimeSpan.FromTicks(30), "only");
		var retry = core.AcceptCompletion("c-missing");
		Assert.Equal(MessagingRejection.DuplicateCompletionReport, retry.Rejection);
		Assert.False(Assert.Single(core.Find("m")!.Obligations).Discharged);

		PublishReport(core, "c-expired", "m", "only", "success", TimeSpan.FromTicks(2));
		clock.Advance(TimeSpan.FromTicks(2));
		var expiredReport = core.AcceptCompletion("c-expired");
		Assert.Equal(MessagingRejection.ExpiredCompletionReport, expiredReport.Rejection);
		Assert.False(Assert.Single(core.Find("m")!.Obligations).Discharged);

		PublishReport(core, "c-late", "m", "only", "success", TimeSpan.FromTicks(50));
		clock.Advance(TimeSpan.FromTicks(28));
		var expiredWork = core.AcceptCompletion("c-late");
		Assert.Equal(MessagingRejection.TerminalMessage, expiredWork.Rejection);
		var poisoned = core.Find("m");
		Assert.NotNull(poisoned);
		Assert.Equal(TerminalKind.Poisoned, poisoned.Terminal);
		Assert.False(Assert.Single(poisoned.Obligations).Discharged);
		Assert.Empty(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
		Assert.Single(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());

		PublishWork(core, "later", TimeSpan.FromTicks(20), "only");
		Accept(core, "c-new", "later", "only", "success", TimeSpan.FromTicks(10));
		Assert.Equal(TerminalKind.Completed, core.Find("later")!.Terminal);
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
	}

	/// <summary>
	/// Unknown-obligation rejection consumes that report identity even after the obligation is registered later.
	/// A new report identity can discharge the added obligation while the original obligation is still open.
	/// Expiry of the report or of the work message blocks discharge, and poison does not produce Completed.
	/// </summary>
	[Fact]
	public void UnknownObligation_RejectionIsFinal_AndANewReportObeysBothTtlChecks()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(40), "keep");
		PublishReport(core, "c-missing", "m", "added", "success", TimeSpan.FromTicks(40));
		var missing = core.AcceptCompletion("c-missing");
		Assert.Equal(MessagingRejection.UnknownObligation, missing.Rejection);

		core.RegisterObligation("m", "added");
		var retry = core.AcceptCompletion("c-missing");
		Assert.Equal(MessagingRejection.DuplicateCompletionReport, retry.Rejection);
		var afterRetry = core.Find("m");
		Assert.NotNull(afterRetry);
		Assert.Null(afterRetry.Terminal);
		Assert.Contains(afterRetry.Obligations, item => item.ObligationId == "added" && !item.Discharged);
		Assert.Contains(afterRetry.Obligations, item => item.ObligationId == "keep" && !item.Discharged);

		PublishReport(core, "c-expired", "m", "added", "success", TimeSpan.FromTicks(3));
		clock.Advance(TimeSpan.FromTicks(3));
		var expiredReport = core.AcceptCompletion("c-expired");
		Assert.Equal(MessagingRejection.ExpiredCompletionReport, expiredReport.Rejection);
		Assert.Contains(core.Find("m")!.Obligations, item => item.ObligationId == "added" && !item.Discharged);

		Accept(core, "c-new", "m", "added", "success", TimeSpan.FromTicks(10));
		var discharged = core.Find("m");
		Assert.NotNull(discharged);
		Assert.Null(discharged.Terminal);
		Assert.Contains(discharged.Obligations, item => item.ObligationId == "added" && item.CompletionReportId == "c-new");
		Assert.Contains(discharged.Obligations, item => item.ObligationId == "keep" && !item.Discharged);

		PublishReport(core, "c-late", "m", "keep", "success", TimeSpan.FromTicks(50));
		clock.Advance(TimeSpan.FromTicks(37));
		var expiredWork = core.AcceptCompletion("c-late");
		Assert.Equal(MessagingRejection.TerminalMessage, expiredWork.Rejection);
		var poisoned = core.Find("m");
		Assert.NotNull(poisoned);
		Assert.Equal(TerminalKind.Poisoned, poisoned.Terminal);
		Assert.Contains(poisoned.Obligations, item => item.ObligationId == "keep" && !item.Discharged && item.Retired);
		Assert.Single(core.SnapshotOutcomes().OfType<ObligationDischargedOutcome>());
		Assert.Single(core.SnapshotOutcomes().OfType<MessagePoisonedOutcome>());
		Assert.Empty(core.SnapshotOutcomes().OfType<MessageCompletedOutcome>());
	}

	/// <summary>
	/// Returned outcome lists, obligation lists, and history wrappers reject mutation.
	/// A second history snapshot still shows the original obligation identities and the poison retirement.
	/// </summary>
	[Fact]
	public void ReturnedObservations_CannotMutateRetainedHistory()
	{
		var core = NewCore(out var clock);
		var published = PublishWork(core, "m", TimeSpan.FromTicks(20), "one", "two");
		Assert.IsType<ReadOnlyCollection<LifecycleOutcome>>(published.Outcomes);
		Assert.IsType<ReadOnlyCollection<ObligationObservation>>(published.Message!.Obligations);
		var accepted = Assert.Single(published.Outcomes.OfType<WorkAcceptedOutcome>());
		Assert.IsType<ReadOnlyCollection<string>>(accepted.ObligationIds);
		Assert.Throws<NotSupportedException>(() => ((IList)published.Outcomes).Add(accepted));
		Assert.Throws<NotSupportedException>(() => ((IList)accepted.ObligationIds).Add("extra"));
		Assert.Throws<NotSupportedException>(() => ((IList)published.Message.Obligations).Add(published.Message.Obligations[0]));

		Accept(core, "c1", "m", "one", "success", TimeSpan.FromTicks(10));
		clock.Advance(TimeSpan.FromTicks(20));
		var supervised = core.Supervise();
		var poisoned = Assert.Single(supervised.Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.IsType<ReadOnlyCollection<string>>(poisoned.RetiredObligationIds);
		Assert.Throws<NotSupportedException>(() => ((IList)poisoned.RetiredObligationIds).Add("extra"));
		Assert.Throws<NotSupportedException>(() => ((IList)supervised.Outcomes).Add(poisoned));

		var history = core.SnapshotOutcomes();
		Assert.IsType<ReadOnlyCollection<LifecycleOutcome>>(history);
		Assert.Throws<NotSupportedException>(() => ((IList)history).Add(poisoned));
		var again = core.SnapshotOutcomes();
		Assert.Equal(history.Count, again.Count);
		Assert.Equal(new[] { "one", "two" }, again.OfType<WorkAcceptedOutcome>().Single().ObligationIds);
		Assert.Equal(new[] { "two" }, again.OfType<MessagePoisonedOutcome>().Single().RetiredObligationIds);
		Assert.Equal(2, core.Find("m")!.Obligations.Count);
	}

	/// <summary>
	/// Registration after poison returns TerminalMessage and does not add the obligation.
	/// </summary>
	[Fact]
	public void RegistrationAgainstTerminalMessage_IsRejected()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(3), "only");
		clock.Advance(TimeSpan.FromTicks(3));
		core.Supervise();

		var rejected = core.RegisterObligation("m", "later");

		Assert.Equal(MessagingRejection.TerminalMessage, rejected.Rejection);
		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(TerminalKind.Poisoned, message.Terminal);
		Assert.DoesNotContain(message.Obligations, item => item.ObligationId == "later");
	}

	/// <summary>
	/// Final completion, extra registration, and expiration run concurrently.
	/// </summary>
	/// <remarks>
	/// A TaskCompletionSource releases the three workers without blocking a thread on a wait handle.
	/// The test then awaits Task.WhenAll.
	/// The clock then advances again and supervision runs once more so an in-flight race still reaches a terminal value.
	/// The message may be Completed, with every obligation discharged, or Poisoned, with each obligation either discharged or retired.
	/// Exactly one of those terminal outcomes is retained. Obligation identities are unique and the TTL is unchanged.
	/// </remarks>
	[Fact]
	public async Task ConcurrentFinalCompletionRegistrationAndExpiration_ProduceOneTerminalOutcome()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "m", TimeSpan.FromTicks(20), "last");
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var workers = new[]
		{
			Task.Run(async () =>
			{
				await start.Task;
				PublishReport(core, "c-last", "m", "last", "success", TimeSpan.FromTicks(20));
				core.AcceptCompletion("c-last");
			}),
			Task.Run(async () =>
			{
				await start.Task;
				core.RegisterObligation("m", "extra");
			}),
			Task.Run(async () =>
			{
				await start.Task;
				clock.Advance(TimeSpan.FromTicks(20));
				core.Supervise();
			})
		};

		start.TrySetResult();
		await Task.WhenAll(workers);

		clock.Advance(TimeSpan.FromTicks(20));
		core.Supervise();

		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.NotNull(message.Terminal);
		var outcomes = core.SnapshotOutcomes();
		var completed = outcomes.OfType<MessageCompletedOutcome>().Count(item => item.MessageId == "m");
		var poisoned = outcomes.OfType<MessagePoisonedOutcome>().Count(item => item.MessageId == "m");
		Assert.Equal(1, completed + poisoned);
		if (message.Terminal == TerminalKind.Completed)
		{
			Assert.Equal(1, completed);
			Assert.Equal(0, poisoned);
			Assert.All(message.Obligations, item => Assert.True(item.Discharged && !item.Retired));
		}
		else
		{
			Assert.Equal(TerminalKind.Poisoned, message.Terminal);
			Assert.Equal(0, completed);
			Assert.Equal(1, poisoned);
			Assert.All(message.Obligations, item => Assert.True(item.Discharged ^ item.Retired));
		}

		Assert.Equal(message.Obligations.Select(item => item.ObligationId).Distinct(StringComparer.Ordinal).Count(), message.Obligations.Count);
		Assert.Equal(TimeSpan.FromTicks(20), message.TimeToLive);
	}

	/// <summary>
	/// Supervision alone, with no completion or registration after publication, retires the expired obligation as Poisoned.
	/// </summary>
	[Fact]
	public void ExplicitSupervision_RetiresExpiredWorkWithoutOtherTraffic()
	{
		var core = NewCore(out var clock);
		PublishWork(core, "quiet", TimeSpan.FromTicks(7), "only");
		clock.Advance(TimeSpan.FromTicks(7));

		var supervised = core.Supervise();

		var poisoned = Assert.Single(supervised.Outcomes.OfType<MessagePoisonedOutcome>());
		Assert.Equal("quiet", poisoned.MessageId);
		Assert.Equal("only", Assert.Single(poisoned.RetiredObligationIds));
		Assert.Equal(TerminalKind.Poisoned, core.Find("quiet")!.Terminal);
	}

	/// <summary>
	/// Registration, an error discharge, clock advancement, and poison leave the original send instant and TTL unchanged.
	/// </summary>
	[Fact]
	public void TtlAndSendInstant_RemainUnchangedThroughout()
	{
		var core = NewCore(out var clock);
		var published = PublishWork(core, "m", TimeSpan.FromTicks(15), "one", "two");
		var sentAt = published.Message!.SentAt;
		var ttl = published.Message.TimeToLive;

		clock.Advance(TimeSpan.FromTicks(4));
		core.RegisterObligation("m", "three");
		Accept(core, "c1", "m", "one", "error", TimeSpan.FromTicks(9));
		clock.Advance(TimeSpan.FromTicks(11));
		core.Supervise();

		var message = core.Find("m");
		Assert.NotNull(message);
		Assert.Equal(sentAt, message.SentAt);
		Assert.Equal(ttl, message.TimeToLive);
		Assert.Equal(TerminalKind.Poisoned, message.Terminal);
		Assert.Equal(TimeSpan.FromTicks(15), message.TimeToLive);
	}

	/// <summary>Builds a core on a manual clock so each test controls monotonic time.</summary>
	/// <param name="clock">The clock the test advances.</param>
	/// <returns>The isolated core.</returns>
	private static InMemoryMessagingCore NewCore(out ManualMessagingClock clock)
	{
		clock = new ManualMessagingClock();
		return new InMemoryMessagingCore(clock);
	}

	/// <summary>Publishes one work message and asserts acceptance.</summary>
	/// <param name="core">The core under test.</param>
	/// <param name="messageId">Work identity.</param>
	/// <param name="ttl">Positive TTL.</param>
	/// <param name="obligationIds">Initial obligations. At least one.</param>
	/// <returns>The publication result, including the send snapshot.</returns>
	private static MessagingResult PublishWork(InMemoryMessagingCore core, string messageId, TimeSpan ttl, params string[] obligationIds)
	{
		var result = core.Publish(new WorkPublication(messageId, "work", "subject", "payload", ttl, obligationIds));
		Assert.True(result.Accepted);
		return result;
	}

	/// <summary>Publishes one completion report without discharging it, and asserts acceptance.</summary>
	/// <param name="core">The core under test.</param>
	/// <param name="reportId">Completion-report identity.</param>
	/// <param name="targetId">Work message named by the report.</param>
	/// <param name="obligationId">Obligation named by the report.</param>
	/// <param name="resultCode">Result code, including an error code when the test says so.</param>
	/// <param name="ttl">Positive report TTL. It starts at this publication.</param>
	private static void PublishReport(
		InMemoryMessagingCore core,
		string reportId,
		string targetId,
		string obligationId,
		string resultCode,
		TimeSpan ttl)
	{
		var result = core.PublishCompletion(new CompletionReport(reportId, "completion", ttl, targetId, obligationId, resultCode));
		Assert.True(result.Accepted);
	}

	/// <summary>Publishes a report and accepts it immediately, before the clock advances, and asserts discharge.</summary>
	/// <param name="core">The core under test.</param>
	/// <param name="reportId">Completion-report identity.</param>
	/// <param name="targetId">Work message named by the report.</param>
	/// <param name="obligationId">Obligation named by the report.</param>
	/// <param name="resultCode">Result code stored on the discharge.</param>
	/// <param name="ttl">Positive report TTL.</param>
	private static void Accept(
		InMemoryMessagingCore core,
		string reportId,
		string targetId,
		string obligationId,
		string resultCode,
		TimeSpan ttl)
	{
		PublishReport(core, reportId, targetId, obligationId, resultCode, ttl);
		var accepted = core.AcceptCompletion(reportId);
		Assert.True(accepted.Accepted);
	}

	/// <summary>Discharges obligations "a" and "b" in the given order and returns the final snapshot.</summary>
	/// <param name="first">The obligation discharged first.</param>
	/// <param name="second">The obligation discharged second.</param>
	/// <returns>The Completed snapshot.</returns>
	private static MessageObservation RunTwoObligations(string first, string second)
	{
		var core = NewCore(out _);
		PublishWork(core, "m", TimeSpan.FromTicks(30), "a", "b");
		Accept(core, "c-" + first, "m", first, "success", TimeSpan.FromTicks(10));
		var midway = core.Find("m");
		Assert.NotNull(midway);
		Assert.Null(midway.Terminal);
		Accept(core, "c-" + second, "m", second, "success", TimeSpan.FromTicks(10));
		var done = core.Find("m");
		Assert.NotNull(done);
		return done;
	}

	/// <summary>Asserts Completed and that both named results were retained.</summary>
	/// <param name="message">The final snapshot.</param>
	/// <param name="firstId">First obligation identity.</param>
	/// <param name="firstResult">Result expected for <paramref name="firstId"/>.</param>
	/// <param name="secondId">Second obligation identity.</param>
	/// <param name="secondResult">Result expected for <paramref name="secondId"/>.</param>
	private static void AssertCompleted(MessageObservation message, string firstId, string firstResult, string secondId, string secondResult)
	{
		Assert.Equal(TerminalKind.Completed, message.Terminal);
		Assert.Equal(2, message.Obligations.Count);
		Assert.Contains(message.Obligations, item => item.ObligationId == firstId && item.ResultCode == firstResult && item.Discharged);
		Assert.Contains(message.Obligations, item => item.ObligationId == secondId && item.ResultCode == secondResult && item.Discharged);
	}
}
