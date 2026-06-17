# PB-NEXT-DRYRUN-001 — Validation And Dry Run Import Plan

## 1. Purpose

This planning document defines the PB-NEXT validation and dry run import plan.

A dry run import validates an uploaded Simply Plural export, parses it, maps source records to planned PB-NEXT domain operations, reports controlled results, and stops before durable domain writes.

This lane is planning/docs only. It does not change app, API, database runtime behavior, import behavior, upload behavior, Azure runtime, website, Airmeet, release, or outreach behavior.

## 2. Design Principle

Dry run is the inspection gate between upload intake and committed import.

It gives the System owner a controlled preview of what PB-NEXT believes the export contains and what would happen during commit.

Dry run should produce structured facts, counts, warnings, errors, planned operation summaries, and controlled reason codes.

Dry run should avoid copying private payload content into durable review records, logs, audit events, or status text.

## 3. Dry Run Boundary

Dry run is separate from:

- upload intake
- import job ledger
- source record payload storage
- source id mapping persistence
- domain write execution
- audit event storage
- operational logging
- user notification delivery

Upload intake owns the received artifact.

The import job ledger owns lifecycle state.

Dry run owns validation, parse assessment, mapping assessment, planned operation summaries, and commit readiness decisions.

Committed import owns durable domain writes.

## 4. Required Partition Anchor

SystemId is required for dry run records and results.

The dry run evaluates an export for a specific System.

AccountId identifies the initiating account where applicable.

All dry run review, retry, expiration, and commit eligibility checks should be scoped through SystemId first.

Dry run results should align with the SystemId partition boundary used by the import job ledger, audit schema, upload intake, and domain data planning.

## 5. Dry Run Identity

DryRunId should be a stable internal identifier generated when dry run processing begins.

DryRunId should reference:

- SystemId
- AccountId
- UploadId
- ImportJobId
- ImportJobAttemptId when applicable
- parser version
- import schema version
- CorrelationId
- RequestId when available

DryRunId identifies one validation and planning pass.

A later retry or re-run may create a new DryRunId while preserving the same ImportJobId.

## 6. Status Model

The first dry run status model should be controlled and explicit.

Initial statuses should include:

- Created
- Queued
- Running
- ValidationFailed
- ValidationPassed
- MappingFailed
- MappingPassed
- PlanCreated
- CommitReady
- CommitBlocked
- Cancelled
- Expired
- Failed

A dry run should have one current status and a durable transition path through the import job ledger.

CommitReady means the dry run completed all required validation and planning checks.

CommitBlocked means the dry run completed enough analysis to explain why commit should not proceed.

## 7. Validation Scope

Dry run validation should include the checks needed before commit eligibility.

Initial validation areas should include:

- export file readability
- supported Simply Plural export shape
- supported source application marker when available
- parser version compatibility
- required top-level sections
- source record count sanity checks
- duplicate source id handling
- privacy bucket interpretability
- member record interpretability
- front history interpretability
- custom field interpretability
- avatar reference interpretability when present
- notes and groups interpretability when present

Validation should produce controlled results and counts.

Validation should avoid durable storage of private payload excerpts.

## 8. Result Summary Model

Dry run should produce a structured result summary.

Initial result summary fields should include:

- DryRunId
- ImportJobId
- SystemId
- AccountId
- DryRunStatus
- ParserVersion
- ImportSchemaVersion
- TotalSourceRecordCount
- AcceptedSourceRecordCount
- RejectedSourceRecordCount
- WarningCount
- ErrorCount
- PlannedInsertCount
- PlannedUpdateCount
- PlannedSkipCount
- PlannedConflictCount
- CommitEligibilityStatus
- CommitBlockReasonCode
- CreatedAtUtc
- CompletedAtUtc
- CorrelationId

The summary should be useful for user review, operator review, audit correlation, and implementation gating.

It should remain payload-minimizing.

## 9. Validation Result Categories

Dry run validation should use controlled result categories.

Initial categories should include:

- Passed
- Warning
- Error
- Blocked
- Skipped
- Unsupported
- Conflict
- RequiresReview

Passed means the validation rule succeeded.

Warning means the dry run may continue, but the result should be visible before commit.

Error means a rule failed and may contribute to commit blocking.

Blocked means commit is not allowed until the condition is resolved.

Skipped means the rule did not apply to the uploaded export.

Unsupported means the export contains a shape or source feature PB-NEXT cannot safely handle yet.

Conflict means mapping or planning found competing interpretations.

RequiresReview means the dry run can complete, but commit should wait for explicit user confirmation or a later policy decision.

## 10. Rule And Reason Code Model

Dry run should record validation and planning outcomes through stable rule identifiers and controlled reason codes.

A dry run rule result should conceptually include:

- DryRunRuleResultId
- DryRunId
- SystemId
- RuleId
- RuleVersion
- ResultCategory
- ReasonCode
- SourceObjectType when applicable
- AffectedCount
- OccurredAtUtc

Rule identifiers should be stable enough for support review, future documentation, and repeatable tests.

Reason codes should explain outcomes without embedding private payload content.

Freeform validation messages should be generated by the UI from controlled rule and reason code data.

## 11. Mapping Preview

Dry run should preview source-to-domain mapping without committing durable domain writes.

Mapping preview should summarize:

- source object type
- planned target object type
- accepted count
- skipped count
- duplicate count
- conflict count
- unsupported count
- requires review count
- mapping rule version
- mapping reason codes

Mapping preview may reference internal source record ids and source id map planning ids when those records exist.

Mapping preview should avoid storing member names, note bodies, custom field values, fronting text, avatar bytes, or user-entered descriptions in durable dry run summaries.

The preview should explain structure and cardinality.

It should not become a payload mirror.

## 12. Planned Operation Model

Dry run should produce a planned operation summary before commit.

Initial planned operation categories should include:

- Insert
- Update
- Skip
- Conflict
- Unsupported
- Deferred
- RequiresReview

Planned operations should be grouped by domain area.

Initial domain areas should include:

- members
- groups
- notes
- front history
- privacy buckets
- custom fields
- avatars
- source id mappings
- import ledger records

The first implementation may store grouped summaries before storing per-record planned operations.

Per-record operation plans should be introduced only where needed for safety, replay, or user review.

## 13. Commit Eligibility

Dry run should produce a clear commit eligibility result.

Initial commit eligibility statuses should include:

- Eligible
- EligibleWithWarnings
- Blocked
- RequiresReview
- Expired
- Failed

Commit eligibility should be based on validation results, mapping results, authorization checks, consent checks where applicable, System state, account lifecycle state, and import job state.

CommitBlockedReasonCode should be controlled and stable.

Examples include:

- UnsupportedExportShape
- RequiredSectionMissing
- ParserVersionUnsupported
- MappingConflict
- TooManyValidationErrors
- SystemClosed
- AccountNotEligible
- MembershipMissing
- ConsentMissing
- DryRunExpired
- ImportJobNotCurrent

Commit eligibility should be explainable from structured dry run records.

## 14. User Review Boundary

Dry run should support a future user review screen without storing private payload excerpts in durable review state.

The future review screen may show:

- high-level source summary
- object type counts
- warning count
- error count
- planned insert count
- planned update count
- planned skip count
- conflict count
- commit eligibility
- controlled explanations
- next available action

If the UI displays private source details during review, those details should come from the authorized source record or parser view under the proper privacy boundary.

The durable dry run result should remain a structured summary.

## 15. Audit And Ledger Integration

Dry run should integrate with the import job ledger and audit event schema.

Import job ledger transitions should cover dry run lifecycle movement such as queued, running, validation failed, validation passed, plan created, commit ready, commit blocked, expired, and failed.

Audit events should be emitted for meaningful gates and outcomes.

Likely audit events include:

- dry run started
- dry run completed
- dry run failed
- commit eligibility granted
- commit eligibility blocked
- dry run expired
- authorization denied for dry run review
- authorization denied for commit attempt

Shared identifiers should include:

- DryRunId
- ImportJobId
- ImportJobAttemptId
- UploadId
- SystemId
- AccountId
- CorrelationId
- RequestId when available

## 16. Expiration And Replay

Dry run results should expire.

Expiration protects against committing stale plans after account state, System state, consent state, relationship authority, parser rules, or import policy changes.

Expiration policy should define:

- dry run result lifetime
- whether warning-only dry runs expire differently from blocked dry runs
- whether a user may re-run validation from the same UploadId
- whether a re-run creates a new DryRunId
- whether older dry run summaries remain reviewable
- whether expired dry runs can be used for support analysis

Replay should create a new dry run result tied to the same import job where appropriate.

Replay should not overwrite the prior dry run history.

## 17. Authorization Gates

Dry run operations should be protected by explicit authorization gates.

Required gates include:

- start dry run
- view dry run summary
- view dry run warnings
- view dry run errors
- re-run dry run
- expire dry run
- attempt commit from dry run
- support review of dry run state
- administrative repair of dry run state

Each gate should evaluate account lifecycle status, System membership, System state, relationship authority when applicable, consent when applicable, import job state, and support authority when applicable.

Denied decisions for meaningful gates should produce audit events.

Dry run review should be treated as access to privacy-sensitive import planning state.

## 18. Privacy And Payload Rules

Dry run records should minimize stored personal data.

Durable dry run records should prefer:

- generated identifiers
- controlled statuses
- controlled reason codes
- object type names
- counts
- timestamps
- parser and rule versions
- planned operation categories

Durable dry run records should avoid:

- member names
- note bodies
- group names
- avatar bytes
- custom field values
- fronting text
- user-entered descriptions
- raw Simply Plural JSON
- uploaded file contents
- freeform exception text

If private details are displayed during an authorized review, they should come from the correct source record boundary rather than copied into dry run summaries.

## 19. Failure Modes

Dry run should define clear failure handling.

Initial failure modes include:

- upload artifact missing
- upload artifact expired
- parser unsupported
- parser failed
- required source section missing
- source record shape unsupported
- validation rule failure
- mapping conflict
- dry run storage failure
- import job state mismatch
- authorization denied
- consent missing where required
- System closed
- account lifecycle gate failed
- unexpected processing failure

Each failure should map to a controlled reason code.

Failure handling should preserve enough structure to explain the result without copying payload content into dry run records.

## 20. Index Planning

The first database implementation should plan indexes around System-scoped review, commit eligibility, and cleanup.

Likely index candidates include:

- SystemId plus CreatedAtUtc
- SystemId plus DryRunStatus
- SystemId plus CommitEligibilityStatus
- DryRunId
- ImportJobId
- ImportJobAttemptId
- UploadId
- AccountId plus CreatedAtUtc
- CorrelationId
- ExpirationAtUtc

Rule result indexes should support DryRunId plus ResultCategory.

Planned operation summary indexes should support DryRunId plus domain area.

Indexes should preserve System-scoped access patterns.

## 21. Commit Handoff

Dry run should define the controlled handoff to committed import.

A commit attempt should reference:

- DryRunId
- ImportJobId
- ImportJobAttemptId
- UploadId
- SystemId
- AccountId
- commit eligibility status
- parser version
- import schema version
- mapping rule version
- CorrelationId
- RequestId when available

Commit should be blocked when the dry run is expired, no longer current, no longer eligible, or tied to an import job that has moved into an incompatible state.

Commit should create its own import job ledger transitions and audit events.

Dry run should remain the planning evidence for why commit was allowed or blocked.

## 22. User-Facing State

Dry run should support understandable user-facing status without making UI text the durable authority.

A future user-facing screen may show:

- dry run running
- validation passed
- validation failed
- warnings found
- conflicts found
- commit ready
- commit blocked
- dry run expired
- re-run available
- commit available

The UI should translate controlled statuses and reason codes into readable text.

The durable state should remain structured and stable.

User-facing messages may change without rewriting historical dry run facts.

## 23. Initial Implementation Slice

The first implementation slice should be narrow.

Recommended first durable dry run coverage:

- DryRunId
- ImportJobId
- UploadId
- SystemId
- AccountId
- DryRunStatus
- parser version
- import schema version
- warning count
- error count
- planned insert count
- planned update count
- planned skip count
- planned conflict count
- commit eligibility status
- commit block reason code
- created timestamp
- completed timestamp
- expiration timestamp
- CorrelationId

The first slice can defer per-record planned operations, detailed rule result tables, advanced review screens, and support tooling.

The first slice should still preserve the payload-minimizing dry run boundary.

## 24. Open Questions

Open questions before implementation:

- What exact tables store dry run header, rule results, mapping preview, and planned operation summaries?
- Which validation rules block commit in the first release?
- Which warnings allow commit with explicit confirmation?
- How long does a dry run remain eligible for commit?
- What event invalidates a dry run before normal expiration?
- Can a user re-run dry run from the same UploadId after upload artifact cleanup?
- Which dry run decisions require durable audit events?
- What private source details may appear in an authorized review UI?
- Does the first implementation need per-record planned operations?
- What commit handoff contract connects dry run to Alpha commit?

These questions should be resolved before schema migration or runtime dry run work begins.
