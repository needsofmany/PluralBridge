// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace PluralBridge.Api.Messaging
{
	/// <summary>
	/// Isolated in-memory messaging core. One instance owns its work messages, completion reports, and outcome history.
	/// </summary>
	/// <remarks>
	/// Send is acceptance through <see cref="Publish"/> or <see cref="PublishCompletion"/>.
	/// The send instant is the monotonic sample taken under the gate during that call. It is not revised.
	/// Elapsed time is that later sample minus the send instant. Elapsed time greater than or equal to TTL is expiration.
	/// Infrastructure accounting is the dictionaries guarded by <c>_gate</c>. Callers never mutate those dictionaries.
	/// The gate covers only the state transition. No subscriber, callback, delivery, or I/O runs while it is held.
	/// A discharge writes one obligation once. Poison sets <see cref="TerminalKind.Poisoned"/> without consulting the Completed path.
	/// This type is not registered with the application host, database, or controllers.
	/// Database durability, lifecycle republication, and subscriber behavior are deferred.
	/// </remarks>
	public sealed class InMemoryMessagingCore
	{
		private readonly IMessagingClock _clock;
		private readonly object _gate = new object();
		private readonly Dictionary<string, WorkState> _work = new Dictionary<string, WorkState>(StringComparer.Ordinal);
		private readonly Dictionary<string, CompletionState> _completions = new Dictionary<string, CompletionState>(StringComparer.Ordinal);
		private readonly List<LifecycleOutcome> _history = new List<LifecycleOutcome>();

		/// <summary>
		/// Creates a core that reads send time and expiration from <paramref name="clock"/>.
		/// </summary>
		/// <param name="clock">Monotonic clock. Required. The core does not advance it.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="clock"/> is null.</exception>
		public InMemoryMessagingCore(IMessagingClock clock)
		{
			_clock = clock ?? throw new ArgumentNullException(nameof(clock));
		}

		/// <summary>
		/// Accepts a work message and registers its initial obligations atomically.
		/// </summary>
		/// <param name="publication">The work envelope. Its TTL must be positive and it must name at least one obligation.</param>
		/// <returns>
		/// Acceptance with a <see cref="WorkAcceptedOutcome"/>, or <see cref="MessagingRejection.DuplicateIdentity"/>
		/// when the identity is already a work message or a completion report.
		/// </returns>
		/// <remarks>
		/// The send instant and TTL stored here are the lifetime for every later check. Registration, completion, and supervision do not change them.
		/// Blank identities, a non-positive TTL, or a missing obligation throw before the gate is taken.
		/// </remarks>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="publication"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown when an identity or the obligation list is missing or contains duplicates.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the TTL is not positive.</exception>
		public MessagingResult Publish(WorkPublication publication)
		{
			if (publication == null)
			{
				throw new ArgumentNullException(nameof(publication));
			}

			RequireIdentity(publication.MessageId, nameof(publication.MessageId));
			RequireText(publication.MessageType, nameof(publication.MessageType));
			RequireText(publication.SubjectKey, nameof(publication.SubjectKey));
			RequirePositive(publication.TimeToLive, nameof(publication.TimeToLive));
			var obligationIds = RequireObligations(publication.ObligationIds);

			List<LifecycleOutcome> produced;
			MessageObservation? snapshot;
			MessagingRejection? rejection;

			lock (_gate)
			{
				produced = new List<LifecycleOutcome>();
				if (_work.ContainsKey(publication.MessageId) || _completions.ContainsKey(publication.MessageId))
				{
					rejection = MessagingRejection.DuplicateIdentity;
					snapshot = _work.ContainsKey(publication.MessageId) ? Snapshot(_work[publication.MessageId]) : null;
				}
				else
				{
					var sentAt = _clock.MonotonicTime;
					var state = new WorkState(
						publication.MessageId,
						publication.MessageType,
						publication.SubjectKey,
						publication.Payload,
						publication.TimeToLive,
						sentAt);
					foreach (var obligationId in obligationIds)
					{
						state.Obligations.Add(obligationId, new ObligationState(obligationId));
					}

					_work.Add(state.MessageId, state);
					produced.Add(new WorkAcceptedOutcome(state.MessageId, state.MessageType, state.TimeToLive, state.SentAt, Freeze(obligationIds)));
					Remember(produced);
					rejection = null;
					snapshot = Snapshot(state);
				}
			}

			return Finish(rejection == null, rejection, snapshot, produced);
		}

		/// <summary>
		/// Registers another obligation on an active work message.
		/// </summary>
		/// <param name="messageId">Work message identity.</param>
		/// <param name="obligationId">New obligation identity on that message.</param>
		/// <returns>
		/// Acceptance, <see cref="MessagingRejection.UnknownMessage"/>, <see cref="MessagingRejection.DuplicateObligation"/>,
		/// or <see cref="MessagingRejection.TerminalMessage"/> after a lifetime check.
		/// </returns>
		/// <remarks>
		/// The lifetime is checked under the same gate as the insert. If the TTL has already elapsed, the message is poisoned
		/// and the new obligation is not added. A terminal message rejects the registration. The caller decides whether to publish a new message.
		/// A rejected registration is not silent: the result carries the rejection, and a poison outcome is retained when expiration caused it.
		/// </remarks>
		/// <exception cref="ArgumentException">Thrown when either identity is blank.</exception>
		public MessagingResult RegisterObligation(string messageId, string obligationId)
		{
			RequireIdentity(messageId, nameof(messageId));
			RequireIdentity(obligationId, nameof(obligationId));

			List<LifecycleOutcome> produced;
			MessageObservation? snapshot;
			MessagingRejection? rejection;

			lock (_gate)
			{
				produced = new List<LifecycleOutcome>();
				if (!_work.TryGetValue(messageId, out var state))
				{
					rejection = MessagingRejection.UnknownMessage;
					snapshot = null;
				}
				else
				{
					var now = _clock.MonotonicTime;
					ExpireWorkIfDue(state, now, produced);
					if (state.Terminal != null)
					{
						rejection = MessagingRejection.TerminalMessage;
					}
					else if (state.Obligations.ContainsKey(obligationId))
					{
						rejection = MessagingRejection.DuplicateObligation;
					}
					else
					{
						state.Obligations.Add(obligationId, new ObligationState(obligationId));
						produced.Add(new ObligationRegisteredOutcome(state.MessageId, obligationId));
						rejection = null;
					}

					Remember(produced);
					snapshot = Snapshot(state);
				}
			}

			return Finish(rejection == null, rejection, snapshot, produced);
		}

		/// <summary>
		/// Accepts a completion-report envelope and captures its send instant.
		/// </summary>
		/// <param name="report">The completion envelope. Its TTL must be positive. It does not add an obligation.</param>
		/// <returns>Acceptance with a <see cref="CompletionReportPublishedOutcome"/>, or <see cref="MessagingRejection.DuplicateIdentity"/>.</returns>
		/// <remarks>
		/// This call does not discharge. <see cref="AcceptCompletion"/> does that only while the report's own TTL has not elapsed.
		/// Splitting the calls lets a test advance the clock between send and discharge. At the send sample, elapsed time is zero.
		/// </remarks>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown when an identity or the result code is blank.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the TTL is not positive.</exception>
		public MessagingResult PublishCompletion(CompletionReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			RequireIdentity(report.MessageId, nameof(report.MessageId));
			RequireText(report.MessageType, nameof(report.MessageType));
			RequirePositive(report.TimeToLive, nameof(report.TimeToLive));
			RequireIdentity(report.TargetMessageId, nameof(report.TargetMessageId));
			RequireIdentity(report.ObligationId, nameof(report.ObligationId));
			RequireText(report.ResultCode, nameof(report.ResultCode));

			List<LifecycleOutcome> produced;
			MessagingRejection? rejection;

			lock (_gate)
			{
				produced = new List<LifecycleOutcome>();
				if (_work.ContainsKey(report.MessageId) || _completions.ContainsKey(report.MessageId))
				{
					rejection = MessagingRejection.DuplicateIdentity;
				}
				else
				{
					var sentAt = _clock.MonotonicTime;
					var state = new CompletionState(
						report.MessageId,
						report.MessageType,
						report.TimeToLive,
						sentAt,
						report.TargetMessageId,
						report.ObligationId,
						report.ResultCode);
					_completions.Add(state.MessageId, state);
					produced.Add(new CompletionReportPublishedOutcome(
						state.MessageId,
						state.MessageType,
						state.TimeToLive,
						state.SentAt,
						state.TargetMessageId,
						state.ObligationId,
						state.ResultCode));
					Remember(produced);
					rejection = null;
				}
			}

			return Finish(rejection == null, rejection, null, produced);
		}

		/// <summary>
		/// Discharges the obligation named by a published completion report, exactly once.
		/// </summary>
		/// <param name="completionMessageId">Completion-report identity returned by <see cref="PublishCompletion"/>.</param>
		/// <returns>
		/// The discharge and, when it was the last outstanding obligation before TTL expiry, <see cref="TerminalKind.Completed"/>.
		/// Otherwise a rejection. An expired work message in the same call is <see cref="TerminalKind.Poisoned"/> and is not discharged.
		/// </returns>
		/// <remarks>
		/// The report TTL is checked first. Elapsed time greater than or equal to that TTL consumes the report as
		/// <see cref="MessagingRejection.ExpiredCompletionReport"/> and does not discharge.
		/// Unknown target and unknown obligation also consume the report. A later call with the same identity returns
		/// <see cref="MessagingRejection.DuplicateCompletionReport"/> even if the target or obligation appears afterward.
		/// A different report identity can still discharge an active obligation, subject to both TTLs.
		/// An already discharged obligation on an active message returns <see cref="MessagingRejection.ObligationAlreadyDischarged"/>.
		/// After the parent is terminal, another report returns <see cref="MessagingRejection.TerminalMessage"/> and does not revive it.
		/// An error result code is stored like any other result code.
		/// </remarks>
		/// <exception cref="ArgumentException">Thrown when <paramref name="completionMessageId"/> is blank.</exception>
		public MessagingResult AcceptCompletion(string completionMessageId)
		{
			RequireIdentity(completionMessageId, nameof(completionMessageId));

			List<LifecycleOutcome> produced;
			MessageObservation? snapshot;
			MessagingRejection? rejection;

			lock (_gate)
			{
				produced = new List<LifecycleOutcome>();
				if (!_completions.TryGetValue(completionMessageId, out var report))
				{
					rejection = MessagingRejection.UnknownMessage;
					snapshot = null;
				}
				else if (report.Consumed)
				{
					rejection = MessagingRejection.DuplicateCompletionReport;
					snapshot = _work.TryGetValue(report.TargetMessageId, out var existing) ? Snapshot(existing) : null;
				}
				else
				{
					var now = _clock.MonotonicTime;
					if (LifetimeElapsed(report.SentAt, report.TimeToLive, now))
					{
						report.Consumed = true;
						rejection = MessagingRejection.ExpiredCompletionReport;
						produced.Add(new CompletionReportRejectedOutcome(report.MessageId, rejection.Value, report.TargetMessageId, report.ObligationId));
						if (_work.TryGetValue(report.TargetMessageId, out var expiredTarget))
						{
							ExpireWorkIfDue(expiredTarget, now, produced);
							snapshot = Snapshot(expiredTarget);
						}
						else
						{
							snapshot = null;
						}
					}
					else if (!_work.TryGetValue(report.TargetMessageId, out var target))
					{
						report.Consumed = true;
						rejection = MessagingRejection.UnknownMessage;
						produced.Add(new CompletionReportRejectedOutcome(report.MessageId, rejection.Value, report.TargetMessageId, report.ObligationId));
						snapshot = null;
					}
					else
					{
						ExpireWorkIfDue(target, now, produced);
						if (target.Terminal != null)
						{
							report.Consumed = true;
							rejection = MessagingRejection.TerminalMessage;
							produced.Add(new CompletionReportRejectedOutcome(report.MessageId, rejection.Value, report.TargetMessageId, report.ObligationId));
						}
						else if (!target.Obligations.TryGetValue(report.ObligationId, out var obligation))
						{
							report.Consumed = true;
							rejection = MessagingRejection.UnknownObligation;
							produced.Add(new CompletionReportRejectedOutcome(report.MessageId, rejection.Value, report.TargetMessageId, report.ObligationId));
						}
						else if (obligation.Discharged)
						{
							report.Consumed = true;
							rejection = MessagingRejection.ObligationAlreadyDischarged;
							produced.Add(new CompletionReportRejectedOutcome(report.MessageId, rejection.Value, report.TargetMessageId, report.ObligationId));
						}
						else
						{
							report.Consumed = true;
							obligation.Discharged = true;
							obligation.ResultCode = report.ResultCode;
							obligation.CompletionReportId = report.MessageId;
							produced.Add(new ObligationDischargedOutcome(target.MessageId, obligation.Id, report.MessageId, report.ResultCode));
							CompleteIfClear(target, now, produced);
							rejection = null;
						}

						snapshot = Snapshot(target);
					}
				}

				Remember(produced);
			}

			return Finish(rejection == null, rejection, snapshot, produced);
		}

		/// <summary>
		/// Retires expired work messages and expired completion reports when no other operation arrives.
		/// </summary>
		/// <returns>The poison and rejection evidence produced by this pass. A second pass does not repeat a terminal outcome.</returns>
		/// <remarks>
		/// This is the supervision entry point for tests and a future host. It does not choose a timer interval and it does not start a background service.
		/// Work that is already Completed or Poisoned is left unchanged. An expired completion report is consumed and cannot discharge later.
		/// </remarks>
		public MessagingResult Supervise()
		{
			List<LifecycleOutcome> produced;
			lock (_gate)
			{
				produced = new List<LifecycleOutcome>();
				var now = _clock.MonotonicTime;
				foreach (var state in _work.Values)
				{
					ExpireWorkIfDue(state, now, produced);
				}

				foreach (var report in _completions.Values)
				{
					if (!report.Consumed && LifetimeElapsed(report.SentAt, report.TimeToLive, now))
					{
						report.Consumed = true;
						produced.Add(new CompletionReportRejectedOutcome(
							report.MessageId,
							MessagingRejection.ExpiredCompletionReport,
							report.TargetMessageId,
							report.ObligationId));
					}
				}

				Remember(produced);
			}

			return Finish(true, null, null, produced);
		}

		/// <summary>
		/// Returns a read-only snapshot of a work message.
		/// </summary>
		/// <param name="messageId">Work message identity.</param>
		/// <returns>The snapshot, or null when that work message was never accepted.</returns>
		/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is blank.</exception>
		public MessageObservation? Find(string messageId)
		{
			RequireIdentity(messageId, nameof(messageId));
			lock (_gate)
			{
				return _work.TryGetValue(messageId, out var state) ? Snapshot(state) : null;
			}
		}

		/// <summary>
		/// Copies retained lifecycle evidence into a read-only collection.
		/// </summary>
		/// <returns>A wrapper over a copy of the history. Mutating the wrapper does not change the retained evidence.</returns>
		/// <remarks>The copy is taken under the gate. The wrapper is what the caller receives.</remarks>
		public ReadOnlyCollection<LifecycleOutcome> SnapshotOutcomes()
		{
			lock (_gate)
			{
				return Freeze(_history);
			}
		}

		/// <summary>
		/// Poisons an active work message when <paramref name="now"/> is at or past its deadline.
		/// </summary>
		/// <param name="state">The work message. A terminal message is ignored.</param>
		/// <param name="now">The monotonic sample for this operation.</param>
		/// <param name="produced">Evidence appended when poison runs.</param>
		private void ExpireWorkIfDue(WorkState state, TimeSpan now, List<LifecycleOutcome> produced)
		{
			if (state.Terminal != null)
			{
				return;
			}

			if (LifetimeElapsed(state.SentAt, state.TimeToLive, now))
			{
				Poison(state, produced);
			}
		}

		/// <summary>
		/// Retires every undischarged obligation and records Poisoned.
		/// </summary>
		/// <param name="state">The active work message.</param>
		/// <param name="produced">Evidence for this transition.</param>
		/// <remarks>
		/// Already discharged obligations keep their result and are not marked retired.
		/// The method sets the terminal value directly. It does not call the Completed check,
		/// so a cleanup that leaves zero outstanding obligations cannot manufacture Completed.
		/// A second call finds the terminal value and returns without another outcome.
		/// </remarks>
		private static void Poison(WorkState state, List<LifecycleOutcome> produced)
		{
			if (state.Terminal != null)
			{
				return;
			}

			var retired = new List<string>();
			foreach (var obligation in state.Obligations.Values)
			{
				if (!obligation.Discharged)
				{
					obligation.Retired = true;
					retired.Add(obligation.Id);
				}
			}

			state.Terminal = TerminalKind.Poisoned;
			produced.Add(new MessagePoisonedOutcome(state.MessageId, Freeze(retired)));
		}

		/// <summary>
		/// Records Completed only when every obligation is already discharged and the TTL has not elapsed.
		/// </summary>
		/// <param name="state">The work message just discharged.</param>
		/// <param name="now">The same monotonic sample used to authorize that discharge.</param>
		/// <param name="produced">Evidence for Completed, or for Poisoned when the deadline has been reached.</param>
		/// <remarks>
		/// If the deadline has been reached, this calls <see cref="Poison"/> instead of Completed.
		/// One undischarged obligation leaves the message active. The terminal value is written at most once.
		/// </remarks>
		private static void CompleteIfClear(WorkState state, TimeSpan now, List<LifecycleOutcome> produced)
		{
			if (state.Terminal != null)
			{
				return;
			}

			if (LifetimeElapsed(state.SentAt, state.TimeToLive, now))
			{
				Poison(state, produced);
				return;
			}

			foreach (var obligation in state.Obligations.Values)
			{
				if (!obligation.Discharged)
				{
					return;
				}
			}

			state.Terminal = TerminalKind.Completed;
			produced.Add(new MessageCompletedOutcome(state.MessageId, SnapshotObligations(state)));
		}

		/// <summary>
		/// Appends this operation's evidence to the retained history.
		/// </summary>
		/// <param name="produced">Evidence created under the gate. The caller's returned collection is a later copy.</param>
		private void Remember(List<LifecycleOutcome> produced)
		{
			if (produced.Count > 0)
			{
				_history.AddRange(produced);
			}
		}

		/// <summary>
		/// Reports whether monotonic time from <paramref name="sentAt"/> has reached <paramref name="timeToLive"/>.
		/// </summary>
		/// <param name="sentAt">Send instant captured at publication.</param>
		/// <param name="timeToLive">Publisher-chosen TTL. Callers pass a positive value.</param>
		/// <param name="now">Current monotonic sample.</param>
		/// <returns>True when <paramref name="now"/> minus <paramref name="sentAt"/> is greater than or equal to the TTL.</returns>
		/// <remarks>Tick subtraction avoids a <see cref="TimeSpan"/> overflow on the comparison. A backward sample does not count as elapsed.</remarks>
		private static bool LifetimeElapsed(TimeSpan sentAt, TimeSpan timeToLive, TimeSpan now)
		{
			return now.Ticks - sentAt.Ticks >= timeToLive.Ticks;
		}

		/// <summary>
		/// Copies one work message into an immutable observation.
		/// </summary>
		/// <param name="state">Retained work state. The copy does not share its obligation dictionary.</param>
		/// <returns>The snapshot returned to callers.</returns>
		private static MessageObservation Snapshot(WorkState state)
		{
			return new MessageObservation(
				state.MessageId,
				state.MessageType,
				state.SubjectKey,
				state.Payload,
				state.TimeToLive,
				state.SentAt,
				state.Terminal,
				SnapshotObligations(state));
		}

		/// <summary>
		/// Copies obligations into a read-only list. Each element is a new observation record.
		/// </summary>
		/// <param name="state">Retained work state.</param>
		/// <returns>A wrapper the caller cannot use to edit the core or the retained history.</returns>
		private static ReadOnlyCollection<ObligationObservation> SnapshotObligations(WorkState state)
		{
			var observations = new List<ObligationObservation>(state.Obligations.Count);
			foreach (var obligation in state.Obligations.Values)
			{
				observations.Add(new ObligationObservation(
					obligation.Id,
					obligation.Discharged,
					obligation.Retired,
					obligation.ResultCode,
					obligation.CompletionReportId));
			}

			return observations.AsReadOnly();
		}

		/// <summary>
		/// Wraps the operation evidence in a read-only result after the gate has been released.
		/// </summary>
		/// <param name="accepted">True when the requested accounting change was applied.</param>
		/// <param name="rejection">The refusal, or null when <paramref name="accepted"/> is true.</param>
		/// <param name="snapshot">Work snapshot, or null when no work message was involved.</param>
		/// <param name="produced">Evidence from this call. History already holds these instances.</param>
		/// <returns>The public result.</returns>
		private static MessagingResult Finish(
			bool accepted,
			MessagingRejection? rejection,
			MessageObservation? snapshot,
			List<LifecycleOutcome> produced)
		{
			return new MessagingResult(accepted, rejection, snapshot, Freeze(produced));
		}

		/// <summary>
		/// Copies <paramref name="items"/> into a list and returns a read-only wrapper around that copy.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="items">Source sequence. The source itself is not retained.</param>
		/// <returns>A collection the caller cannot add to.</returns>
		private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> items)
		{
			return new List<T>(items).AsReadOnly();
		}

		/// <summary>Rejects a blank identity before any accounting change.</summary>
		/// <param name="value">The identity to check.</param>
		/// <param name="name">The parameter name for the exception.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
		private static void RequireIdentity(string value, string name)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new ArgumentException("An identity is required.", name);
			}
		}

		/// <summary>Rejects a blank type, subject, or result code before any accounting change.</summary>
		/// <param name="value">The text to check.</param>
		/// <param name="name">The parameter name for the exception.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
		private static void RequireText(string value, string name)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new ArgumentException("A value is required.", name);
			}
		}

		/// <summary>Rejects a TTL that is zero or negative. Every envelope in this slice has a positive TTL.</summary>
		/// <param name="timeToLive">The publisher-chosen lifetime.</param>
		/// <param name="name">The parameter name for the exception.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeToLive"/> is not positive.</exception>
		private static void RequirePositive(TimeSpan timeToLive, string name)
		{
			if (timeToLive <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(name, "TTL must be positive.");
			}
		}

		/// <summary>Copies the initial obligation identities and rejects an empty or duplicate set.</summary>
		/// <param name="obligationIds">Identities from the publication. This slice requires at least one.</param>
		/// <returns>The distinct identities in input order.</returns>
		/// <exception cref="ArgumentException">Thrown when the set is missing, empty, blank, or contains duplicates.</exception>
		private static IReadOnlyList<string> RequireObligations(IReadOnlyCollection<string> obligationIds)
		{
			if (obligationIds == null || obligationIds.Count == 0)
			{
				throw new ArgumentException("At least one obligation is required.", nameof(obligationIds));
			}

			var distinct = new List<string>(obligationIds.Count);
			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var obligationId in obligationIds)
			{
				if (string.IsNullOrWhiteSpace(obligationId))
				{
					throw new ArgumentException("An obligation identity is required.", nameof(obligationIds));
				}

				if (!seen.Add(obligationId))
				{
					throw new ArgumentException("Obligation identities on a publication must be unique.", nameof(obligationIds));
				}

				distinct.Add(obligationId);
			}

			return distinct;
		}

		/// <summary>
		/// Retained work message. <see cref="TimeToLive"/> and <see cref="SentAt"/> have no setters.
		/// <see cref="Terminal"/> is written once, by poison or by completion.
		/// </summary>
		private sealed class WorkState
		{
			/// <summary>Stores the publication values. TTL and send time cannot be replaced later.</summary>
		/// <param name="messageId">Work identity.</param>
		/// <param name="messageType">Caller-defined type.</param>
		/// <param name="subjectKey">Caller-defined subject.</param>
		/// <param name="payload">Opaque payload.</param>
		/// <param name="timeToLive">Positive TTL captured at send.</param>
		/// <param name="sentAt">Monotonic send instant.</param>
		public WorkState(string messageId, string messageType, string subjectKey, string? payload, TimeSpan timeToLive, TimeSpan sentAt)
			{
				MessageId = messageId;
				MessageType = messageType;
				SubjectKey = subjectKey;
				Payload = payload;
				TimeToLive = timeToLive;
				SentAt = sentAt;
			}

			public string MessageId { get; }

			public string MessageType { get; }

			public string SubjectKey { get; }

			public string? Payload { get; }

			public TimeSpan TimeToLive { get; }

			public TimeSpan SentAt { get; }

			public TerminalKind? Terminal { get; set; }

			public Dictionary<string, ObligationState> Obligations { get; } = new Dictionary<string, ObligationState>(StringComparer.Ordinal);
		}

		/// <summary>
		/// One outstanding or closed obligation. Discharge and retirement are separate flags so poison cleanup
		/// can close what remains without treating an earlier result as a new completion.
		/// </summary>
		private sealed class ObligationState
		{
			/// <summary>Creates an outstanding obligation with no result.</summary>
			/// <param name="id">Obligation identity within the parent work message.</param>
			public ObligationState(string id)
			{
				Id = id;
			}

			public string Id { get; }

			public bool Discharged { get; set; }

			public bool Retired { get; set; }

			public string? ResultCode { get; set; }

			public string? CompletionReportId { get; set; }
		}

		/// <summary>
		/// Retained completion report. <see cref="Consumed"/> becomes true when the report discharges,
		/// expires, or is finally rejected for an unknown target or unknown obligation.
		/// Its TTL and send instant are fixed at publication.
		/// </summary>
		private sealed class CompletionState
		{
			/// <summary>Stores the report envelope. Consumed starts false.</summary>
			/// <param name="messageId">Report identity.</param>
			/// <param name="messageType">Caller-defined type.</param>
			/// <param name="timeToLive">Positive report TTL.</param>
			/// <param name="sentAt">Monotonic send instant of the report.</param>
			/// <param name="targetMessageId">Work message named by the report.</param>
			/// <param name="obligationId">Obligation named by the report.</param>
			/// <param name="resultCode">Result that a later acceptance would store.</param>
			public CompletionState(
				string messageId,
				string messageType,
				TimeSpan timeToLive,
				TimeSpan sentAt,
				string targetMessageId,
				string obligationId,
				string resultCode)
			{
				MessageId = messageId;
				MessageType = messageType;
				TimeToLive = timeToLive;
				SentAt = sentAt;
				TargetMessageId = targetMessageId;
				ObligationId = obligationId;
				ResultCode = resultCode;
			}

			public string MessageId { get; }

			public string MessageType { get; }

			public TimeSpan TimeToLive { get; }

			public TimeSpan SentAt { get; }

			public string TargetMessageId { get; }

			public string ObligationId { get; }

			public string ResultCode { get; }

			public bool Consumed { get; set; }
		}
	}
}
