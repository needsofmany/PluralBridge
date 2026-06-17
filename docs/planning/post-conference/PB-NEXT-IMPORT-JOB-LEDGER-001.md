# PB-NEXT-IMPORT-JOB-LEDGER-001 — Import Job Ledger Implementation Plan

## 1. Purpose

This planning document defines the implementation plan for the PB-NEXT import job ledger.

The import job ledger is the durable control plane for uploaded Simply Plural exports as they move through validation, parsing, mapping, domain write planning, completion, failure, cancellation, retry, and review.

This lane is planning/docs only. It does not change app, API, database runtime behavior, import behavior, upload behavior, Azure runtime, website, Airmeet, release, or outreach behavior.

## 2. Design Principle

An import job is a lifecycle object.

The ledger records the lifecycle of that object, the System it belongs to, the Account or process that initiated it, the current state, the state transitions, the parser and schema versions involved, and the durable identifiers needed to explain what happened later.

The ledger should behave like a transaction journal for import control, not a private data archive.

It records import progress and accountability without copying raw Simply Plural payloads into job history rows.

## 3. Ledger Boundary

The import job ledger is separate from:

- uploaded file storage
- raw source record storage
- source id mapping
- domain tables
- operational logs
- audit events
- user notifications
- support notes

The ledger owns import lifecycle state.

Source records own imported source payload handling under their own privacy rules.

Audit events own durable accountability records for meaningful import actions and decisions.

Operational logs own diagnostics.

Notification records own user-facing delivery state.

These streams may share ImportJobId and CorrelationId.

## 4. Payload-Free Ledger Rule

Import job ledger rows must not store raw Simply Plural JSON, uploaded file contents, avatar bytes, note bodies, member descriptions, custom field values, private fronting text, or user-entered freeform source payloads.

Allowed ledger data includes:

- ImportJobId
- SystemId
- AccountId
- UploadId or file reference id
- source application name
- source export version when available
- parser version
- import schema version
- job status
- status reason code
- object type counts
- validation summary counts
- mapping summary counts
- domain write summary counts
- timestamps
- retry counts
- cancellation reason code
- CorrelationId

Counts and controlled reason codes are preferred over payload excerpts.

## 5. Required Partition Anchor

SystemId is required on import job ledger records.

The import job belongs to a System because the imported data is System-owned data.

AccountId identifies the account that initiated, owns, or administers the import action where applicable.

AccountId does not replace SystemId as the ledger partition anchor.

All review, retry, cancellation, and cleanup operations should be scoped through SystemId first.

## 6. Import Job Identity

ImportJobId should be a stable internal identifier generated when the import job is accepted for processing.

ImportJobId should be used by:

- upload acceptance
- validation steps
- parser steps
- source record creation
- source id mapping
- domain write planning
- audit events
- operational logs
- user notifications
- support review
- retry and cancellation handling

ImportJobId should remain stable through retries.

A retry should create a new attempt record or transition record rather than replacing the original job identity.

## 7. Actor And Ownership Model

The import job ledger should record who or what initiated the job and who currently owns operational responsibility for it.

Initial actor references should include:

- AccountId for user-initiated imports
- SystemId for the import target
- ActorType for Account or SystemProcess
- ActorId for the initiating principal
- CorrelationId for request or background processing continuity

The ledger should support future background processing without pretending the background worker is a human user.

If support tooling later touches an import job, support access should be represented through audit events and controlled support authority records rather than editable ledger text.

## 8. Status Model

The first import job status model should be explicit and controlled.

Initial statuses should include:

- Created
- UploadAccepted
- ValidationQueued
- Validating
- ValidationFailed
- ValidationCompleted
- MappingQueued
- Mapping
- MappingFailed
- MappingCompleted
- DomainWriteQueued
- DomainWriting
- DomainWriteFailed
- Completed
- CancelRequested
- Cancelled
- RetryQueued
- Superseded
- Expired

A job should have one current status and a durable transition history.

Status names should describe lifecycle state rather than UI wording.

## 9. Status Transition History

The import job ledger should preserve a durable status transition history.

Each transition should record:

- ImportJobTransitionId
- ImportJobId
- SystemId
- FromStatus
- ToStatus
- ReasonCode
- ActorType
- ActorId
- OccurredAtUtc
- CorrelationId
- AttemptNumber when applicable
- bounded metadata when approved

Transition rows should explain how the job moved through the import pipeline.

The current status may live on the import job header for efficient review, but the transition table should remain the authoritative history.

Transitions should be append-only except for narrowly defined repair or administrative correction flows.

## 10. Attempt Model

Retries should be represented explicitly.

An import job may have one or more attempts.

Each attempt should record:

- ImportJobAttemptId
- ImportJobId
- SystemId
- AttemptNumber
- StartedAtUtc
- CompletedAtUtc when finished
- AttemptStatus
- ParserVersion
- ImportSchemaVersion
- FailureReasonCode when failed
- CorrelationId

Attempt records allow the same ImportJobId to survive retry, cancellation, or resumed processing.

The attempt model should prevent later retries from erasing the first failure.

Attempt metadata must follow the payload-free ledger rule.

## 11. Validation Ledger

Validation should produce structured ledger results without storing payload excerpts.

Validation ledger data should include:

- ImportJobId
- SystemId
- ImportJobAttemptId
- validation status
- validation rule set version
- total source record count
- accepted source record count
- rejected source record count
- warning count
- error count
- controlled validation reason codes

Validation failures should point to rule identifiers and counts.

Private source values should remain outside the ledger.

A later UI may present friendly validation messages, but the ledger should store controlled diagnostic facts.

## 12. Mapping Ledger

Mapping converts source identities and source records into stable PB internal references.

The mapping ledger should record:

- ImportJobId
- SystemId
- ImportJobAttemptId
- source object type
- mapped object type
- mapped count
- skipped count
- duplicate count
- conflict count
- mapping rule version
- mapping status
- controlled reason codes

Mapping rows may reference SourceIdMapId where needed.

They should not store source names, member display names, note text, custom field values, or private source payload fragments.

The mapping ledger should explain cardinality and conflict decisions.

## 13. Domain Write Ledger

Domain write planning and execution should have explicit ledger coverage.

The domain write ledger should record:

- ImportJobId
- SystemId
- ImportJobAttemptId
- target domain area
- planned insert count
- planned update count
- planned skip count
- completed insert count
- completed update count
- completed skip count
- failed count
- write status
- controlled reason codes

Target domain areas may include members, groups, notes, front history, privacy buckets, custom fields, avatars, and relationship-owned future objects.

Domain write ledger records should describe what class of domain data changed and how many records were affected.

They must not copy domain payloads into the ledger.

## 14. Cancellation Retry And Supersession

Import jobs should have explicit lifecycle handling for cancellation, retry, and supersession.

Cancellation should record:

- cancel request time
- cancel actor
- cancel reason code
- status at cancel request
- final cancellation status

Retry should record:

- retry request time
- retry actor
- retry reason code
- prior attempt id
- new attempt id
- retry eligibility result

Supersession should record when a newer import job replaces an earlier import job for the same System and source scope.

Supersession should not delete the older ledger history.

It should mark the earlier job as no longer current for the affected import scope.

## 15. Reason Code Vocabulary

Reason codes should be controlled and stable.

Initial import job reason code families should include:

- UploadAccepted
- UploadRejected
- ParserSelected
- ParserUnsupported
- ValidationPassed
- ValidationFailed
- MappingCompleted
- MappingConflict
- DomainWriteCompleted
- DomainWritePartialFailure
- DomainWriteFailed
- CancelRequestedByUser
- CancelledBeforeDomainWrite
- RetryRequested
- RetryNotEligible
- SupersededByNewImport
- ExpiredBeforeProcessing

Reason codes should support operator review, user support, and audit correlation without exposing private data.

Freeform reason text should be avoided in the durable ledger.

## 16. Audit And Log Integration

The import job ledger should integrate with audit events and operational logs through identifiers, not duplicated payloads.

Audit events should be emitted for meaningful lifecycle actions such as import started, import completed, import failed, cancellation requested, retry requested, and destructive or privacy-sensitive domain write decisions.

Operational logs may capture execution diagnostics under separate logging policy.

Shared identifiers should include:

- ImportJobId
- ImportJobAttemptId
- SystemId
- AccountId when present
- CorrelationId
- RequestId when available

The ledger, audit stream, and logs should form a three-part diagnostic circuit: lifecycle state, accountability record, and operational trace.

## 17. Review And Query Model

The import job ledger should support scoped review by System first.

Primary review paths should include:

- SystemId
- ImportJobId
- ImportJobAttemptId
- AccountId
- job status
- status reason code
- source application name
- parser version
- import schema version
- created date range
- completed date range
- CorrelationId

Review queries should allow a System owner to understand whether an import is pending, completed, failed, cancelled, retried, superseded, or expired.

Support review, if later allowed, should require explicit support authority and audit coverage.

The ledger should support clear operator answers without exposing source payloads.

## 18. Index Planning

The first database implementation should plan indexes around lifecycle and review paths.

Likely index candidates include:

- SystemId plus CreatedAtUtc
- SystemId plus CurrentStatus
- SystemId plus CompletedAtUtc
- ImportJobId
- ImportJobAttemptId
- AccountId plus CreatedAtUtc
- CorrelationId
- CurrentStatus plus UpdatedAtUtc
- SystemId plus SourceApplicationName

Transition history indexes should support ImportJobId plus OccurredAtUtc.

Attempt indexes should support ImportJobId plus AttemptNumber.

Indexes should preserve System-scoped access patterns and avoid encouraging broad cross-System scans.

## 19. Retention And Expiration

Import job ledger retention should be defined separately from uploaded file retention and source payload retention.

The ledger may need to outlive temporary uploaded files.

The ledger may also need to outlive source records that are expired, redacted, or removed through deletion planning.

Retention policy should define:

- how long completed job headers are retained
- how long transition history is retained
- how long failed validation summaries are retained
- how retry and supersession history is retained
- how ledger references behave after System closure
- how ledger records behave after account closure

Expiration should preserve enough controlled lifecycle history to explain prior decisions and failures.

## 20. Security And Authorization Gates

Import job ledger operations should be protected by explicit authorization gates.

Required gates include:

- create import job
- view import job
- view import job history
- cancel import job
- retry import job
- supersede import job
- expire import job
- support review of import job
- administrative repair of import job state

Each gate should evaluate System membership, account lifecycle status, relationship authority if applicable, consent if applicable, and support authority if applicable.

Meaningful allowed and denied decisions should produce audit events.

Ledger access should be treated as access to privacy-sensitive operational history.

## 21. Idempotency And Concurrency

Import job processing should be safe across retries, duplicate requests, browser refreshes, background worker restarts, and partial failures.

The ledger should support idempotency through:

- stable ImportJobId
- stable UploadId or upload reference
- AttemptNumber
- CorrelationId
- controlled state transitions
- unique constraints where appropriate
- append-only transition records

Concurrent workers should not be able to advance the same job into conflicting terminal states.

Terminal statuses should be explicit.

Completed, Cancelled, Superseded, Expired, and unretryable failed states should have clear transition rules.

## 22. User-Facing State

The ledger should provide enough structured data for a future user-facing import status view.

A future status view may show:

- import created time
- current status
- high-level status reason
- object type counts
- warning count
- error count
- completed time
- retry availability
- cancellation availability

The user-facing layer should translate controlled statuses and reason codes into readable text.

The durable ledger should remain structured and controlled.

User-facing messages should not be stored as the authoritative ledger state.

## 23. Initial Implementation Slice

The first implementation slice should be narrow.

Recommended first durable ledger coverage:

- import job header
- current status
- SystemId
- AccountId
- source application name
- parser version
- import schema version
- created timestamp
- updated timestamp
- completed timestamp
- failure reason code
- CorrelationId
- transition history
- attempt number

The first slice can defer detailed validation, mapping, and domain write summary tables until the import pipeline reaches those implementation lanes.

The first slice should still preserve the payload-free ledger rule.

## 24. Open Questions

Open questions before implementation:

- What exact tables represent job header, transition history, attempts, validation summary, mapping summary, and domain write summary?
- Which statuses are terminal in the first implementation?
- Which status transitions are allowed?
- Which transitions require audit events in the first release?
- What retention period applies to completed, failed, cancelled, superseded, and expired jobs?
- What job data may support personnel view under approved support access?
- Should upload file expiration happen independently from ledger retention?
- What idempotency key should protect duplicate upload acceptance?
- What background worker model will own queued import execution?
- What repair path exists for a job stuck in a non-terminal processing state?

These questions should be resolved before schema migration work begins.
