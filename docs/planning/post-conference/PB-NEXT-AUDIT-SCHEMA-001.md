# PB-NEXT-AUDIT-SCHEMA-001 — Payload-Free Audit Event Schema Implementation Plan

## 1. Purpose

This planning document defines the implementation plan for the first durable audit event schema in PB-NEXT.

The audit schema must support security, privacy, accountability, troubleshooting, and future evidence-grade review without storing imported payloads or private Simply Plural source records inside audit events.

This lane is planning/docs only. It does not change app, API, database runtime behavior, import behavior, upload behavior, Azure runtime, website, Airmeet, release, or outreach behavior.

## 2. Design Principle

Audit events record that a meaningful system action occurred, who or what caused it, what authority was used, what object or boundary was affected, and what the outcome was.

Audit events do not store private source payloads.

Audit events do not duplicate domain records.

Audit events do not become a shadow import archive.

Audit events should work like a SACL trail rather than an application log dump: structured, scoped, reviewable, and tied to an access decision or lifecycle transition.

## 3. Schema Boundary

The audit event schema belongs to the PB-NEXT accountability layer.

It is separate from:

- operational logging
- import source records
- import source id mapping
- member profile storage
- consent records
- relationship records
- authorization policy definitions
- user notification records

Operational logs may help diagnose failures.

Audit events must explain meaningful actions and decisions.

The two streams may reference a common correlation id, but they should not be collapsed into one table.

## 4. Payload-Free Rule

Audit events must not contain Simply Plural raw JSON, uploaded file contents, avatar bytes, note bodies, private member descriptions, private front history text, custom field values, or other imported payload material.

When audit events need to identify a source item, they should use stable internal identifiers, import job identifiers, source mapping identifiers, object type names, object ids, counts, status values, and decision metadata.

For privacy-sensitive values, the default is reference by id, count, category, or bucket rather than content.

## 5. Core Event Shape

The initial audit event shape should support these fields conceptually:

- AuditEventId
- SystemId
- AccountId when an authenticated account is involved
- ActorType
- ActorId
- EventType
- EventAction
- TargetType
- TargetId
- TargetSystemId when different from the event SystemId
- ImportJobId when tied to an import
- CorrelationId
- AuthorityType
- AuthorityId
- AuthorityVersionId
- DecisionOutcome
- DecisionReasonCode
- OccurredAtUtc
- CreatedAtUtc
- RequestId when available
- IpHash or client fingerprint only if later approved by privacy planning
- MetadataJson for bounded non-payload metadata only

The shape may be split during implementation if the database design calls for event headers plus typed detail tables.

The planning intent is a narrow durable skeleton first.

## 6. Required Partition Anchor

SystemId is the primary partition anchor for audit events.

Every audit event that concerns System-owned data should carry SystemId directly.

This keeps audit review aligned with the PB-NEXT data planning decision that SystemId is the dominant privacy and partition boundary.

AccountId is an actor or lifecycle reference, not the primary partition key for System-owned data.

## 7. Actor Model

The actor model must allow several actor forms without forcing all actions into a human account shape.

Initial actor types should include:

- Account
- SystemProcess
- ImportJob
- SupportOperator
- FutureExternalClient

ActorId should reference the relevant internal id when one exists.

For system processes, ActorId may be a stable process identifier or service principal name chosen during implementation.

The goal is to preserve who or what initiated the action without inventing fake users.

## 8. Authority References

Audit events should record the authority used for the action or decision.

Authority references may point to:

- membership records
- relationship grants
- consent grants
- policy versions
- import ownership records
- account lifecycle state
- system process authority

AuthorityType and AuthorityId identify the authority source.

AuthorityVersionId should be used where the authority source can change over time.

Authority references should be stable enough to explain past decisions after later edits, revocations, closure, or deletion planning.

## 9. Event Type Taxonomy

The first audit schema should use a small controlled event taxonomy.

Initial event families should include:

- AccountLifecycle
- SystemLifecycle
- Membership
- Relationship
- Consent
- AuthorizationDecision
- ImportJob
- ImportMapping
- PrivacyBoundary
- DataMutation
- DeletionPlanning
- SupportAccess
- SystemProcess

EventType should identify the family.

EventAction should identify the specific action within that family.

Examples:

- AccountLifecycle.EmailValidationStarted
- AccountLifecycle.EmailValidationCompleted
- Membership.MemberAdded
- Membership.MemberRoleChanged
- Relationship.GrantCreated
- Relationship.GrantRevoked
- Consent.ConsentGranted
- Consent.ConsentRevoked
- AuthorizationDecision.AccessAllowed
- AuthorizationDecision.AccessDenied
- ImportJob.ImportStarted
- ImportJob.ImportCompleted
- ImportJob.ImportFailed
- DeletionPlanning.SoftDeleteRequested
- DeletionPlanning.ExpirationSweepCompleted

The implementation should prefer explicit names over overloaded generic actions.

## 10. Decision Outcome Model

Audit events that represent access checks, authorization decisions, consent checks, import eligibility, or lifecycle gates should include a structured outcome.

Initial outcomes should include:

- Allowed
- Denied
- Completed
- Failed
- Skipped
- Revoked
- Expired
- Superseded

DecisionReasonCode should be controlled text.

Examples:

- AccountEmailNotValidated
- MembershipMissing
- RelationshipGrantMissing
- ConsentMissing
- ConsentExpired
- SystemClosed
- ImportJobAlreadyFinalized
- DeleteWindowPending
- SupportAccessNotApproved

Reason codes should support diagnosis without exposing payloads.

## 11. Metadata JSON Boundary

MetadataJson is allowed only for bounded, non-payload metadata.

Allowed metadata examples:

- count values
- object type names
- before and after status names
- import parser version
- schema version
- feature flag name
- validation rule name
- batch size
- elapsed milliseconds
- high-level failure category

Disallowed metadata examples:

- raw Simply Plural JSON
- uploaded file contents
- member names
- note bodies
- avatar data
- custom field values
- private fronting text
- user-entered freeform descriptions
- tokens
- passwords
- email validation secrets

MetadataJson should be treated as a small structured envelope, not a dumping ground.

## 12. Import Audit Events

Import audit events should explain the import lifecycle without preserving the private source payload inside the audit table.

The audit stream should record:

- import job creation
- upload accepted for processing
- parser selected
- validation started
- validation completed
- mapping started
- mapping completed
- domain write started
- domain write completed
- import completed
- import failed
- import cancelled when supported

Import audit events may reference ImportJobId, SourceRecordId, SourceIdMapId, object type, object id, count, and status.

They must not store source record payloads.

Payload inspection belongs in the import pipeline and source record storage rules, not in audit event text.

## 13. Membership And Relationship Audit Events

Membership and relationship changes are security-relevant and should be audited.

Membership audit events should cover:

- member invited
- member accepted
- member removed
- role assigned
- role changed
- role removed
- owner changed
- account linked to System
- account unlinked from System

Relationship audit events should cover:

- grant created
- grant changed
- grant revoked
- grant expired
- relationship authority used for an access decision

These events should reference stable internal ids and authority versions where applicable.

They should avoid recording private display names or freeform labels in the audit event body.

## 14. Consent Audit Events

Consent events should be versioned and durable.

Consent audit events should cover:

- consent requested
- consent granted
- consent changed
- consent revoked
- consent expired
- consent used for an action
- consent missing during a denied action

Consent audit events should include the relevant consent record id and version id.

They should include the consent scope identifier, not the private payload governed by the consent.

Consent revocation must leave enough audit history to explain later denied actions.

## 15. Authorization Decision Audit Events

Authorization decision audit events should be recorded for meaningful gates, especially where data access crosses account, membership, relationship, consent, support, import, or deletion boundaries.

The first implementation should avoid auditing every trivial UI read.

Initial durable authorization audit should focus on:

- import ownership checks
- System membership checks
- relationship grant checks
- consent checks
- account lifecycle gates
- support access gates
- destructive action gates
- deletion and restoration gates

Authorization audit events should include outcome, reason code, authority reference, target reference, and correlation id.

A denied event should be as explainable as an allowed event.

## 16. Retention And Review Model

Audit retention policy should be explicit before implementation.

The first implementation plan should assume audit records are durable security records with longer retention than operational logs.

Deletion planning must define whether audit events are retained, redacted, tombstoned, or cryptographically severed from deleted objects.

Audit review should support scoped queries by SystemId, AccountId, event type, target type, target id, import job id, consent id, authority id, occurred date range, outcome, and correlation id.

The review path should favor structured filters over text search.

Audit storage should remain useful after domain records are edited, revoked, closed, or queued for deletion.

## 17. Schema Versioning

Audit events should carry an audit schema version.

The initial version can be a simple integer or stable text value.

Schema versioning allows future code to interpret older audit records after the event shape changes.

Versioning should cover:

- event envelope shape
- metadata boundary rules
- reason code vocabulary
- authority reference model
- retention interpretation
- redaction behavior

Audit schema changes should be additive unless a later migration plan explicitly handles older records.

## 18. Correlation Model

CorrelationId links audit events, operational logs, import job activity, request handling, and background processing.

CorrelationId should be generated at the boundary where a user request, import job, scheduled process, or support action begins.

The same CorrelationId should follow the action through authorization checks, domain writes, import mapping, notification planning, and audit records.

CorrelationId must not encode private payload content.

CorrelationId is a diagnostic join key, not an authorization secret.

## 19. Privacy And Redaction Rules

Audit records should minimize stored personal data.

The initial plan should prefer stable internal identifiers over names, descriptions, email addresses, imported text, or source payload fragments.

Email addresses should not be copied into audit events unless a later account lifecycle plan explicitly approves a hashed or normalized reference.

IP address handling should remain deferred until privacy planning approves a policy.

If audit records need redaction later, redaction should preserve the event fact, time, outcome, authority category, and target category where possible.

## 20. Failure Event Rules

Failure events should explain the failure class without exposing payloads.

Failure audit metadata may include:

- failure category
- validation rule identifier
- parser stage
- object type
- count affected
- retryable flag
- exception category
- elapsed milliseconds
- import job status

Failure audit metadata must not include exception text if the exception text may contain payload excerpts, tokens, filenames with private meaning, or user-entered values.

Operational logs may carry deeper diagnostics under separate logging rules.

Audit failure events carry the accountability record.

## 21. Query And Index Planning

The first database implementation should plan indexes around review paths rather than speculative analytics.

Likely query paths include:

- SystemId plus OccurredAtUtc
- SystemId plus EventType
- SystemId plus TargetType and TargetId
- AccountId plus OccurredAtUtc
- ImportJobId
- CorrelationId
- AuthorityType plus AuthorityId
- DecisionOutcome plus DecisionReasonCode
- Consent record id or consent version id
- deletion planning target id

Indexes should support scoped review without encouraging broad unbounded searches across all Systems.

Cross-System audit review should be an administrative capability with explicit authorization planning.

## 22. Non Goals

This lane does not design a full audit viewer.

This lane does not design support tooling.

This lane does not implement database tables.

This lane does not change runtime authorization behavior.

This lane does not add logging.

This lane does not alter import storage.

This lane does not decide final audit retention duration.

This lane defines the schema implementation plan and the privacy boundaries that implementation must respect.

## 23. Initial Implementation Slice

The first implementation slice should be intentionally narrow.

Recommended first durable events:

- AccountLifecycle.EmailValidationStarted
- AccountLifecycle.EmailValidationCompleted
- ImportJob.ImportStarted
- ImportJob.ImportCompleted
- ImportJob.ImportFailed
- Membership.MemberAdded
- Membership.MemberRoleChanged
- AuthorizationDecision.AccessDenied
- Consent.ConsentGranted
- Consent.ConsentRevoked
- DeletionPlanning.SoftDeleteRequested

This set gives PB-NEXT useful accountability coverage across account lifecycle, import, membership, authorization, consent, and deletion planning.

Additional events should be added as their owning lanes reach implementation.

## 24. Open Questions

Open questions before implementation:

- What is the exact database table shape for the first audit event store?
- Should typed detail tables be created immediately, or deferred behind MetadataJson?
- What retention period applies to audit records in early production?
- What redaction model applies when a System is closed or queued for deletion?
- What fields are safe for support review?
- Should IpHash be included in the first slice or deferred?
- Which authorization decisions are important enough for durable audit in the first release?
- What admin capability model governs cross-System audit queries?
- What migration policy applies when event names or reason codes change?

These questions should be resolved before schema migration work begins.
