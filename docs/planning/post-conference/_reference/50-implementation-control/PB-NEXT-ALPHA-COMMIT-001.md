# PB-NEXT-ALPHA-COMMIT-001 — Smallest Safe Alpha Commit Plan

## 1. Purpose

This planning document defines the smallest safe Alpha commit plan for PB-NEXT import implementation.

Alpha commit means the first controlled path that takes an eligible dry run and performs durable PB-NEXT domain writes.

This lane is planning/docs only. It does not change app, API, database runtime behavior, import behavior, upload behavior, Azure runtime, website, Airmeet, release, or outreach behavior.

## 2. Design Principle

The first committed import should be narrow, reversible where practical, auditable, System-scoped, and explainable.

The goal is a safe first current-carrying circuit from upload intake through import job ledger, dry run, authorization gates, audit events, and durable domain writes.

Alpha commit should favor a small successful end-to-end slice over broad Simply Plural coverage.

It should prove the commit boundary without pretending every future import object is ready.

## 3. Alpha Commit Boundary

Alpha commit begins after a dry run reaches CommitReady or another approved commit-eligible state.

Alpha commit ends after durable domain writes, source id mapping updates, import job ledger transitions, and audit events are completed or safely failed.

Alpha commit is separate from:

- upload intake
- dry run validation
- parser implementation
- source payload storage
- operational logging
- user notification delivery
- support tooling
- broad migration tooling

Alpha commit owns the controlled conversion from planned operations into durable PB-NEXT state.

## 4. Required Preconditions

Alpha commit should require these preconditions:

- authenticated Account
- eligible account lifecycle state
- active System
- valid System membership
- commit authority for the System
- current ImportJobId
- current UploadId
- current DryRunId
- dry run not expired
- dry run commit eligibility satisfied
- parser version still approved
- import schema version still approved
- required consent checks satisfied where applicable
- no incompatible import job terminal state

A failed precondition should block commit with a controlled reason code and audit event.

## 5. Commit Identity

Alpha commit should create a stable CommitRunId or equivalent commit execution identifier.

The commit identity should reference:

- SystemId
- AccountId
- UploadId
- ImportJobId
- ImportJobAttemptId
- DryRunId
- parser version
- import schema version
- CorrelationId
- RequestId when available

CommitRunId identifies one attempt to turn an eligible dry run into durable writes.

Retries should create explicit attempt records or transitions rather than rewriting the original commit history.

## 6. Commit Status Model

The first commit status model should be controlled and explicit.

Initial statuses should include:

- Created
- AuthorizationChecking
- AuthorizationFailed
- ReadyToWrite
- Writing
- WriteCompleted
- WritePartialFailure
- WriteFailed
- Completed
- Cancelled
- Expired

Terminal statuses should be clearly defined before implementation.

Completed means all approved Alpha-scope writes succeeded and ledger/audit updates completed.

WritePartialFailure should be treated as a serious state requiring explicit repair planning before broad implementation.

## 7. Alpha Domain Scope

The first Alpha commit should use a narrow domain scope.

Candidate Alpha domain areas:

- privacy buckets
- members
- groups
- notes metadata
- source id mappings
- import ledger completion records

The first Alpha commit should avoid domain areas that require additional policy or user-facing design before safe write behavior is clear.

Deferred areas may include:

- avatars
- full note bodies
- current fronter notification behavior
- advanced front history interpretation
- relationship-owned future objects
- user-editable conflict resolution

The exact Alpha set should be selected for safe end-to-end proof, not maximum coverage.

## 8. Transaction Boundary

Alpha commit should define a transaction boundary before implementation.

The transaction should protect durable domain writes and required mapping records as one coherent unit where practical.

The implementation plan should decide whether audit events and import job ledger transitions are written inside the same transaction, immediately adjacent to it, or through an outbox pattern.

The first Alpha commit must avoid a state where domain records are written but the import job ledger cannot explain what happened.

The commit path should be designed for explicit failure recovery.

## 9. Planned Operation Contract

Alpha commit should consume planned operations produced by the approved dry run.

A planned operation should conceptually include:

- PlannedOperationId
- DryRunId
- ImportJobId
- SystemId
- DomainArea
- OperationType
- SourceRecordId when applicable
- SourceIdMapId when applicable
- TargetType
- TargetId when known before write
- PlannedStatus
- CommitEligibilityStatus
- ReasonCode
- OperationOrder

OperationType should be controlled.

Initial operation types should include:

- Insert
- Update
- Skip
- Link
- PreserveExisting
- CreateMapping
- MarkImported

Alpha commit should process only planned operations that are in scope for the Alpha slice.

Out-of-scope planned operations should remain skipped, deferred, or unsupported with controlled reason codes.

## 10. Source ID Mapping Commit Rules

Source ID mapping is required for committed imports.

Alpha commit should create or update mapping records that connect source identifiers to PB-NEXT internal identifiers.

Mapping commit rules should define:

- source application
- source object type
- source identifier
- PB target object type
- PB target identifier
- ImportJobId
- DryRunId
- CommitRunId
- SystemId
- mapping status
- mapping reason code

Mappings should support repeatable imports, conflict detection, skip decisions, and later audit review.

A mapping should never depend on private display text as its durable identity.

Mapping records should be committed with the domain writes they explain where practical.

## 11. Idempotency And Repeat Commit Protection

Alpha commit should protect against duplicate commit attempts.

Duplicate commit risks include:

- browser refresh
- repeated button press
- network retry
- background worker restart
- delayed response after successful write
- manual retry after uncertain failure

Commit protection should use stable identifiers such as DryRunId, ImportJobId, CommitRunId, planned operation ids, and controlled terminal states.

The implementation should prevent the same dry run from creating duplicate Alpha domain records.

A repeated commit request after success should return the existing completed state where possible.

A repeated commit request after partial failure should enter an explicit repair path rather than running blind writes.

## 12. Authorization And Consent Checks

Alpha commit should run final authorization and consent checks immediately before durable writes.

Required checks include:

- account lifecycle eligibility
- active System state
- System membership authority
- commit authority for the System
- dry run currentness
- import job currentness
- consent scope where applicable
- relationship authority where applicable
- deletion or closure blocks

Commit authorization should be evaluated through the central authorization decision shell.

Allowed and denied decisions should include authority references and controlled reason codes.

Meaningful denied decisions should produce durable audit events.

A commit should fail closed when authority cannot be established.

## 13. Audit Events

Alpha commit should emit durable audit events for meaningful lifecycle points.

Likely audit events include:

- commit requested
- commit authorization allowed
- commit authorization denied
- commit started
- domain write started
- domain write completed
- domain write failed
- source mapping created
- source mapping conflict found
- commit completed
- commit failed
- repair required

Audit events should reference:

- CommitRunId
- DryRunId
- ImportJobId
- UploadId
- SystemId
- AccountId
- CorrelationId
- authority references
- outcome
- reason code

Audit events should remain payload-free and should use stable ids, counts, categories, and controlled codes.

## 14. Ledger Transitions

Alpha commit should update the import job ledger as the commit advances.

Important ledger transitions may include:

- CommitRequested
- CommitAuthorizationChecking
- CommitAuthorizationFailed
- CommitReadyToWrite
- CommitWriting
- CommitWriteCompleted
- CommitWritePartialFailure
- CommitWriteFailed
- CommitCompleted
- CommitRepairRequired

The ledger should explain the import job lifecycle even when the final domain state requires deeper investigation.

Ledger transitions should include CommitRunId and CorrelationId.

Import job state should never imply success when durable writes failed or only partially completed.

Terminal import job states should be explicit and controlled.

## 15. Failure And Repair Model

Alpha commit should define failure handling before runtime work begins.

Initial failure categories include:

- authorization failed
- consent failed
- dry run expired
- import job state mismatch
- planned operation mismatch
- source mapping conflict
- database write failed
- transaction failed
- audit write failed
- ledger update failed
- partial write detected
- unexpected processing failure

Each failure should map to a controlled reason code.

Repair planning should define which failures are retryable, which require administrative repair, and which require user-visible restart from dry run.

Partial failure should be treated as a first-class state with explicit evidence and repair instructions.

## 16. User Confirmation Boundary

Alpha commit should require a deliberate user confirmation before durable writes.

The confirmation screen or action should be based on the dry run result and commit eligibility.

A future UI may show:

- import job identity
- dry run status
- Alpha domain areas included
- Alpha domain areas deferred
- warning count
- conflict count
- planned insert count
- planned update count
- planned skip count
- commit eligibility
- high-level consequences of commit

The durable record should store the controlled confirmation fact and identifiers.

User-facing confirmation text should be generated from stable state and policy rather than stored as the authority of record.

Alpha commit should preserve a clear boundary between preview and durable write.

## 17. Alpha Domain Selection Criteria

Alpha commit domain scope should be chosen by safety, explainability, and repairability.

Selection criteria should include:

- simple ownership boundary
- clear SystemId partitioning
- stable source identifiers
- low conflict risk
- low user-facing ambiguity
- clear rollback or repair path
- audit-ready write behavior
- dry run summary available before commit
- source id mapping available after commit

The first Alpha domain set should prove the commit architecture without dragging every Simply Plural feature through the first durable write path.

A smaller committed slice with clean ledger, audit, mapping, and repair semantics is preferred over a broad slice with ambiguous failure behavior.

## 18. Data Mutation Rules

Alpha commit should define data mutation rules before runtime implementation.

Mutation rules should specify:

- whether Alpha creates only new records
- whether Alpha may update existing records
- whether Alpha may preserve existing user-edited records
- whether Alpha may skip records with conflicts
- whether Alpha may create placeholder records
- whether Alpha may mark source records as imported
- whether Alpha may link imported records to existing PB records

Updates should require stronger evidence than inserts.

Conflict cases should be skipped, deferred, or marked for review unless the dry run and commit plan can explain the decision with controlled reason codes.

Imported data should never silently overwrite user-authored PB-NEXT data.

## 19. Rollback And Compensation

Alpha commit should define rollback and compensation expectations before implementation.

A transaction rollback should be used where the database boundary supports it.

If committed records survive after a partial failure, the system should enter a controlled repair state rather than hiding the failure.

Compensation planning should define:

- which records can be safely removed
- which mappings can be invalidated
- which ledger transitions explain repair
- which audit events record repair actions
- which user-facing state appears during repair
- which failures require administrative intervention

Rollback and compensation should preserve accountability.

Repair actions should leave an audit trail rather than erasing the historical commit attempt.

## 20. Retention And Deletion Interaction

Alpha commit should define how committed import records interact with deletion planning.

The first implementation plan should decide:

- whether imported records carry ImportJobId
- whether imported records carry CommitRunId
- whether source mappings survive deletion planning
- whether audit events survive deletion planning
- whether import ledger records survive deletion planning
- how soft delete affects imported records
- how expiration sweeps interact with import evidence

Deletion planning should avoid orphaning audit, ledger, and mapping references.

Committed domain data may be deleted or redacted according to future policy, while controlled evidence records may need longer retention.

## 21. Index And Query Planning

Alpha commit should plan indexes around committed import review, idempotency, and repair.

Likely query paths include:

- SystemId plus ImportJobId
- SystemId plus CommitRunId
- SystemId plus DryRunId
- SystemId plus domain area
- ImportJobId plus commit status
- DryRunId plus commit status
- CommitRunId plus operation status
- SourceIdMapId
- CorrelationId
- CreatedAtUtc

Indexes should preserve System-scoped access patterns.

Repair and review paths should find the affected commit run without broad scans across unrelated Systems.

## 22. User-Facing State

Alpha commit should support clear user-facing state.

A future UI may show:

- commit requested
- authorization failed
- commit ready
- commit running
- commit completed
- commit failed
- repair required
- Alpha domain areas imported
- Alpha domain areas deferred
- controlled warning and error counts
- next available action

User-facing text should be generated from controlled state, reason codes, and policy.

Durable commit records should remain structured and stable.

The user should be able to distinguish a completed Alpha import from a dry run preview.

## 23. Initial Implementation Slice

The first implementation slice should be narrow.

Recommended first durable Alpha commit coverage:

- CommitRunId
- DryRunId
- ImportJobId
- UploadId
- SystemId
- AccountId
- CommitStatus
- Alpha domain area list
- planned insert count
- planned update count
- planned skip count
- completed insert count
- completed update count
- completed skip count
- failure reason code
- created timestamp
- completed timestamp
- CorrelationId

The first slice may defer broad rollback tooling, advanced conflict resolution, support repair UI, avatar writes, full note body import, and relationship-owned future objects.

The first slice should prove the commit boundary, idempotency guard, ledger transition, audit event, and source id mapping behavior.

## 24. Open Questions

Open questions before implementation:

- Which exact domain areas belong in the first Alpha commit?
- Are Alpha writes insert-only, or are controlled updates allowed?
- What transaction boundary includes domain writes, mappings, ledger transitions, and audit events?
- Does Alpha commit need an outbox pattern in the first implementation?
- What failures are retryable?
- What failures require administrative repair?
- What user-facing state appears during partial failure?
- What source id mapping conflicts block commit?
- What dry run warnings require explicit confirmation?
- What deletion planning references must be written during Alpha commit?
- What minimum audit events are required before Alpha commit can ship?

These questions should be resolved before schema migration or runtime Alpha commit work begins.
