// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;

namespace PluralBridge.Api.Messaging
{
	/// <summary>
	/// The only terminal outcomes for an accepted work message.
	/// </summary>
	/// <remarks>
	/// Completed means every registered obligation was discharged before the work message's TTL elapsed.
	/// It does not mean the result code was successful. Poisoned means the TTL elapsed first.
	/// A work message receives exactly one of these values. Retirement during poison sets Poisoned directly
	/// and does not pass through the zero-outstanding check that produces Completed.
	/// </remarks>
	public enum TerminalKind
	{
		/// <summary>
		/// Every registered obligation was discharged before TTL expiration.
		/// </summary>
		Completed = 1,

		/// <summary>
		/// TTL elapsed before completion. Remaining obligations were retired.
		/// </summary>
		Poisoned = 2
	}

	/// <summary>
	/// Why an accounting operation was refused.
	/// </summary>
	/// <remarks>
	/// A refusal does not discharge an obligation and does not clear a terminal outcome.
	/// A lifetime check in the same call can still retire an expired work message before the refusal is returned.
	/// Validation failures, such as a missing identity or a non-positive TTL, are exceptions rather than values of this enum.
	/// </remarks>
	public enum MessagingRejection
	{
		/// <summary>The identity is already used by a work message or a completion report.</summary>
		DuplicateIdentity = 1,

		/// <summary>
		/// The named message is not in this core.
		/// AcceptCompletion also returns this when the completion report names a work message that was never published.
		/// </summary>
		UnknownMessage = 2,

		/// <summary>The named obligation is not registered on that work message.</summary>
		UnknownObligation = 3,

		/// <summary>The work message already has a terminal outcome.</summary>
		TerminalMessage = 4,

		/// <summary>The obligation was already discharged.</summary>
		ObligationAlreadyDischarged = 5,

		/// <summary>The obligation identity is already registered on that work message.</summary>
		DuplicateObligation = 6,

		/// <summary>The completion report's own TTL has elapsed. Elapsed time greater than or equal to its TTL is enough.</summary>
		ExpiredCompletionReport = 7,

		/// <summary>
		/// This completion-report identity was already consumed by an acceptance, an expiry, or a final unknown-target or unknown-obligation rejection.
		/// A later attempt does not discharge and does not revive the parent.
		/// </summary>
		DuplicateCompletionReport = 8
	}

	/// <summary>
	/// Work envelope accepted into the core. The core captures the send instant during <c>Publish</c>.
	/// </summary>
	/// <remarks>
	/// The caller chooses the TTL. The core does not extend it. At least one obligation is required for this slice.
	/// The payload is opaque. This record is the caller's input, not a retained snapshot.
	/// </remarks>
	/// <param name="MessageId">Unique message identity.</param>
	/// <param name="MessageType">Caller-defined type name.</param>
	/// <param name="SubjectKey">Caller-defined subject or key.</param>
	/// <param name="Payload">Caller-defined payload. The core does not interpret it.</param>
	/// <param name="TimeToLive">Publisher-chosen lifetime. Must be positive.</param>
	/// <param name="ObligationIds">Initial outstanding obligations. At least one is required.</param>
	public sealed record WorkPublication(
		string MessageId,
		string MessageType,
		string SubjectKey,
		string? Payload,
		TimeSpan TimeToLive,
		IReadOnlyCollection<string> ObligationIds);

	/// <summary>
	/// Completion-report envelope. It is a typed message with its own identity and TTL.
	/// </summary>
	/// <remarks>
	/// <c>PublishCompletion</c> captures its send instant and does not discharge.
	/// <c>AcceptCompletion</c> discharges the named obligation only while this report is still inside its TTL
	/// and the work message is still active. An error <see cref="ResultCode"/> can discharge normally.
	/// The report does not register an obligation of its own. Publishing it back as lifecycle work is outside this slice.
	/// </remarks>
	/// <param name="MessageId">Unique completion-report identity.</param>
	/// <param name="MessageType">Caller-defined type name.</param>
	/// <param name="TimeToLive">Publisher-chosen lifetime. Must be positive.</param>
	/// <param name="TargetMessageId">Work message whose obligation is reported.</param>
	/// <param name="ObligationId">Obligation identity on that work message.</param>
	/// <param name="ResultCode">Result carried by the report. An error code can discharge normally.</param>
	public sealed record CompletionReport(
		string MessageId,
		string MessageType,
		TimeSpan TimeToLive,
		string TargetMessageId,
		string ObligationId,
		string ResultCode);

	/// <summary>
	/// Immutable copy of one obligation. Changing the copy does not change the core.
	/// </summary>
	/// <remarks>
	/// Discharged and Retired are mutually exclusive in the core's transitions.
	/// Poison retirement sets Retired without a result code and without setting Completed.
	/// </remarks>
	/// <param name="ObligationId">Obligation identity.</param>
	/// <param name="Discharged">True when an accepted completion report accounted for it.</param>
	/// <param name="Retired">True when poison cleanup closed it without a completion result.</param>
	/// <param name="ResultCode">Result from the accepted completion report, when discharged.</param>
	/// <param name="CompletionReportId">Completion report that discharged it, when discharged.</param>
	public sealed record ObligationObservation(
		string ObligationId,
		bool Discharged,
		bool Retired,
		string? ResultCode,
		string? CompletionReportId);

	/// <summary>
	/// Immutable snapshot of one work message, copied under the accounting gate.
	/// </summary>
	/// <remarks>
	/// <see cref="TimeToLive"/> and <see cref="SentAt"/> are the publication values. Later operations do not change them.
	/// <see cref="Obligations"/> is a read-only copy. <see cref="Terminal"/> is null while the message is active.
	/// </remarks>
	/// <param name="MessageId">Message identity.</param>
	/// <param name="MessageType">Type supplied at publication.</param>
	/// <param name="SubjectKey">Subject supplied at publication.</param>
	/// <param name="Payload">Payload supplied at publication.</param>
	/// <param name="TimeToLive">TTL captured at publication.</param>
	/// <param name="SentAt">Monotonic send instant captured at publication.</param>
	/// <param name="Terminal">The one terminal outcome, or null while the message is active.</param>
	/// <param name="Obligations">Obligations registered on this message.</param>
	public sealed record MessageObservation(
		string MessageId,
		string MessageType,
		string SubjectKey,
		string? Payload,
		TimeSpan TimeToLive,
		TimeSpan SentAt,
		TerminalKind? Terminal,
		ReadOnlyCollection<ObligationObservation> Obligations);

	/// <summary>
	/// Immutable evidence of one accounting transition. These records are not messages.
	/// </summary>
	/// <remarks>
	/// The core appends them to retained history and returns a read-only copy after releasing the gate.
	/// They carry no TTL and are not published back into the core. Doing that would require the deferred
	/// bounded-feedback policy. Nested collections are <see cref="ReadOnlyCollection{T}"/> wrappers over copies.
	/// </remarks>
	/// <param name="MessageId">The work message or completion report this evidence describes.</param>
	public abstract record LifecycleOutcome(string MessageId);

	/// <summary>A work message was accepted and its initial obligations were registered.</summary>
	public sealed record WorkAcceptedOutcome(
		string MessageId,
		string MessageType,
		TimeSpan TimeToLive,
		TimeSpan SentAt,
		ReadOnlyCollection<string> ObligationIds) : LifecycleOutcome(MessageId);

	/// <summary>An additional obligation was registered on an active work message.</summary>
	public sealed record ObligationRegisteredOutcome(string MessageId, string ObligationId) : LifecycleOutcome(MessageId);

	/// <summary>An accepted completion report discharged one obligation.</summary>
	public sealed record ObligationDischargedOutcome(
		string MessageId,
		string ObligationId,
		string CompletionReportId,
		string ResultCode) : LifecycleOutcome(MessageId);

	/// <summary>All registered obligations were discharged before TTL expiration.</summary>
	public sealed record MessageCompletedOutcome(
		string MessageId,
		ReadOnlyCollection<ObligationObservation> Results) : LifecycleOutcome(MessageId);

	/// <summary>
	/// TTL expired before completion. The retired identities were closed by poison cleanup.
	/// That cleanup records Poisoned only. It does not record Completed when the outstanding count falls to zero.
	/// </summary>
	public sealed record MessagePoisonedOutcome(
		string MessageId,
		ReadOnlyCollection<string> RetiredObligationIds) : LifecycleOutcome(MessageId);

	/// <summary>A completion-report envelope was accepted and its TTL started.</summary>
	public sealed record CompletionReportPublishedOutcome(
		string MessageId,
		string MessageType,
		TimeSpan TimeToLive,
		TimeSpan SentAt,
		string TargetMessageId,
		string ObligationId,
		string ResultCode) : LifecycleOutcome(MessageId);

	/// <summary>A completion report was refused. No obligation was discharged by this attempt.</summary>
	public sealed record CompletionReportRejectedOutcome(
		string MessageId,
		MessagingRejection Rejection,
		string? TargetMessageId,
		string? ObligationId) : LifecycleOutcome(MessageId);

	/// <summary>
	/// Immutable result of one core operation, assembled after the accounting gate is released.
	/// </summary>
	/// <remarks>
	/// <see cref="Outcomes"/> is a read-only copy of the evidence appended during the call.
	/// <see cref="Message"/> is a snapshot, or null when the call did not involve a stored work message.
	/// Rejected calls leave prior discharges and the single terminal outcome unchanged.
	/// </remarks>
	/// <param name="Accepted">False when the operation was rejected.</param>
	/// <param name="Rejection">The refusal, when <see cref="Accepted"/> is false.</param>
	/// <param name="Message">Work-message snapshot after the operation, when one was involved.</param>
	/// <param name="Outcomes">Lifecycle observations produced by this operation.</param>
	public sealed record MessagingResult(
		bool Accepted,
		MessagingRejection? Rejection,
		MessageObservation? Message,
		ReadOnlyCollection<LifecycleOutcome> Outcomes);
}
