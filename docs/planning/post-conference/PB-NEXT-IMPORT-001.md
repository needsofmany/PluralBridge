# PB-NEXT-IMPORT-001 — Upload, Import Job Ledger, And Source Record Processing

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`, `PB-NEXT-REB-001.md`, `PB-NEXT-CONSENT-001.md`, `PB-NEXT-AUDIT-001.md`  
Scope: Upload boundaries, import job lifecycle, source record preservation, idempotency, consent, authorization, audit, status reporting, and first implementation gate before import behavior is reopened.  
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the seventh PB-NEXT foundation lane: how PluralBridge should model upload and import processing before implementation resumes.

The current app, API, database, import, upload, and Azure runtime behavior remain frozen unless explicitly reopened.

This document does not implement import. It defines the planning boundary for reopening import safely.

## 2. Working Decision

PluralBridge should treat import as a controlled processing pipeline with explicit records for:

1. upload intake
2. import batch identity
3. source record preservation
4. parse and validation status
5. dry-run planning
6. commit status
7. source-to-target mapping
8. warnings and errors
9. authorization authority
10. consent authority
11. audit evidence
12. user-facing status
13. completion notification

Import must be System-centered.

An Account may upload a file. A System receives the imported data. Authorization, consent, audit, and source mapping must all preserve that distinction.

## 3. System-Centered Import Boundary

The target of import is a System.

The importing Account is an actor.

The import batch is a processing record scoped to the target System.

The source file is temporary or quarantined input.

The normalized records belong under System-scoped domain tables.

Candidate core identities:

1. `SystemId`
2. `ImportBatchId`
3. `ImportJobId`
4. `UploadedFileId`
5. `SourceSystem`
6. `SourceRecordId`
7. `SourceRecordStableKey`
8. `SourceRecordHash`
9. `TargetResourceType`
10. `TargetResourceId`
11. `ActorAccountId`

The import pipeline must not collapse Account identity into System ownership.

## 4. Import Lifecycle

Candidate import lifecycle states:

1. Uploaded
2. Quarantined
3. AcceptedForProcessing
4. ParseStarted
5. ParseFailed
6. Parsed
7. ValidationStarted
8. ValidationFailed
9. ValidatedWithWarnings
10. Validated
11. DryRunStarted
12. DryRunCompleted
13. CommitStarted
14. CommitFailed
15. CommitCompleted
16. CompletedWithWarnings
17. Cancelled
18. Expired
19. Purged

State transitions should be recorded in the import job ledger.

Important state transitions should be audit-compatible.

## 5. Upload Intake Boundary

Upload intake should handle file receipt without immediately trusting or committing contents.

Candidate upload intake responsibilities:

1. receive file
2. assign uploaded file identity
3. bind upload to Account actor
4. bind intended target System
5. record file metadata
6. record file size
7. record content hash
8. store or quarantine raw upload
9. reject unsupported file type
10. reject oversized file
11. prevent accidental logging of file contents
12. create import batch record

Raw upload content should not be copied into diagnostic logs or audit records.

## 6. Import Batch Record

Candidate import batch fields:

1. `ImportBatchId`
2. `SystemId`
3. `ActorAccountId`
4. `UploadedFileId`
5. `SourceSystem`
6. `SourceExportVersion`
7. `FileNameOriginal`
8. `FileSizeBytes`
9. `FileHash`
10. `Status`
11. `StatusReasonCode`
12. `ProcessingPurpose`
13. `ConsentId`
14. `ConsentVersion`
15. `PolicyVersion`
16. `CreatedAt`
17. `StartedAt`
18. `CompletedAt`
19. `ExpiresAt`
20. `PurgedAt`

The import batch record should not store private source payload directly.

## 7. Import Job Ledger

The import job ledger records pipeline progress and operator/user-visible status.

Candidate job ledger fields:

1. `ImportJobEventId`
2. `ImportBatchId`
3. `SystemId`
4. `Stage`
5. `State`
6. `Severity`
7. `MessageCode`
8. `SafeMessage`
9. `DiagnosticCorrelationId`
10. `CreatedAt`

The ledger may contain safe status messages.

The ledger should not contain raw private source payload.

## 8. Source Record Preservation

PluralBridge should preserve source record identity and mapping without relying on raw payload as the authority.

Candidate source record fields:

1. `SourceRecordId`
2. `SystemId`
3. `ImportBatchId`
4. `SourceSystem`
5. `SourceType`
6. `SourceStableKey`
7. `SourceRecordHash`
8. `TargetResourceType`
9. `TargetResourceId`
10. `ImportAction`
11. `Status`
12. `CreatedAt`

The source record layer supports:

1. idempotency
2. duplicate detection
3. replay safety
4. troubleshooting without exposing raw payload
5. audit references
6. source-to-target traceability

## 9. Source ID Map

The source ID map links external source identities to PluralBridge target identities.

Candidate map fields:

1. `SourceIdMapId`
2. `SystemId`
3. `ImportBatchId`
4. `SourceSystem`
5. `SourceType`
6. `SourceId`
7. `SourceStableKey`
8. `TargetResourceType`
9. `TargetResourceId`
10. `MappingStatus`
11. `CreatedAt`
12. `UpdatedAt`

The source ID map should be System-scoped.

The same source ID from two Systems must not collide.

## 10. Idempotency And Replay Safety

Import should be safe to retry.

Candidate idempotency rules:

1. Use `SystemId` plus source system plus source stable key as the natural idempotency boundary.
2. Use import batch identity for replay visibility.
3. Use source record hash for change detection where useful.
4. Avoid creating duplicate members, groups, notes, fronts, custom fields, or avatars on retry.
5. Treat repeated import of unchanged source records as no-op or already imported.
6. Treat changed source records as update candidates only when the import policy allows updates.
7. Record skipped, updated, created, failed, and warning states.

Replay safety should be designed before write imports are reopened.

## 11. Validation And Dry Run

Import should support validation before commit.

Validation should answer:

1. Is the file structurally readable?
2. Is the source export type supported?
3. Which source record types are present?
4. Which records can be imported?
5. Which records are unsupported?
6. Which records are malformed?
7. Which records would create new target records?
8. Which records would update existing target records?
9. Which records would be skipped?
10. Which warnings should be visible to the user?

Dry run should produce a safe import plan without committing target domain records.

## 12. Commit Boundary

Commit is the boundary where normalized domain records are created or updated.

Commit should require:

1. authenticated actor
2. active Account
3. valid target System
4. sufficient System relationship or membership
5. required consent or authority
6. import batch in a commit-eligible state
7. audit recording path
8. idempotency protection
9. rollback or recovery policy
10. safe user-facing status handling

Commit should fail closed when authorization, consent, or mandatory audit recording fails.

## 13. Authorization Integration

Import actions must pass through the central authorization decision point.

Candidate import actions:

1. `Import.Upload`
2. `Import.Parse`
3. `Import.Validate`
4. `Import.DryRun`
5. `Import.Commit`
6. `Import.Cancel`
7. `Import.ViewStatus`
8. `Import.Purge`
9. `Import.DownloadReport`

Authorization should evaluate:

1. actor Account
2. target System
3. action
4. import batch
5. relationship or membership authority
6. consent authority
7. processing purpose
8. policy version
9. audit obligation

## 14. Consent Integration

Import is a processing purpose.

Candidate consent requirements:

1. consent to process uploaded export data
2. consent to create or update System records
3. consent to preserve source mapping records
4. consent to retain import status records
5. consent to generate import reports
6. consent to send completion notification
7. consent to retain audit evidence

Revocation should block future processing where consent is required.

Revocation should not erase past audit evidence or completed disclosure evidence.

## 15. Audit Integration

Import should emit payload-free audit events.

Candidate audit events:

1. import upload accepted
2. import upload rejected
3. import parse started
4. import parse failed
5. import validation completed
6. dry run completed
7. commit started
8. commit completed
9. commit failed
10. import cancelled
11. import purged
12. import report viewed or downloaded
13. import denied due to authorization
14. import denied due to missing, expired, or revoked consent

Audit records should reference `ImportBatchId`, `SystemId`, actor, action, authority references, consent references, policy version, and result.

Audit records should not copy source payload.

## 16. Diagnostic Logging Integration

Diagnostic logs may help debug import failures.

Diagnostic logs should contain:

1. request ID
2. correlation ID
3. stage
4. safe exception category
5. safe message code
6. timing
7. service health
8. record counts where safe

Diagnostic logs should avoid:

1. raw upload content
2. source JSON snippets
3. member names
4. notes
5. descriptions
6. avatar content
7. private fronting content
8. consent text
9. access tokens or secrets

## 17. User-Facing Status

Import should expose safe status without leaking private payload.

Candidate user-facing status fields:

1. import batch ID
2. current stage
3. current state
4. created timestamp
5. completed timestamp
6. counts by source type
7. counts by result type
8. safe warnings
9. safe errors
10. next available action

Candidate result counts:

1. records parsed
2. records validated
3. records created
4. records updated
5. records skipped
6. records failed
7. warnings

## 18. Email Notification

Import may send completion notification.

Notification should be minimal and safe.

Candidate email contents:

1. project name
2. import completion status
3. safe count summary
4. link to view status
5. warning that private data is not included in the email

Email should not include imported private data.

Email notification should respect account notification preferences and consent requirements.

## 19. Error Handling

Import errors should be structured.

Candidate error categories:

1. unsupported file
2. malformed export
3. unsupported source version
4. validation failed
5. authorization denied
6. consent missing
7. consent expired
8. consent revoked
9. audit required but unavailable
10. duplicate detection conflict
11. commit failed
12. storage failure
13. internal error

Error records should use safe codes and safe messages.

Private payload should remain out of errors, logs, audit records, and email.

## 20. Privacy Buckets And Alpha Data

DB Alpha includes:

1. Groups
2. Notes
3. Privacy buckets
4. Current fronter
5. Opt-in text notification support for current-fronter changes
6. Avatars
7. Minimal custom-front support where needed for current-front/front-history integrity

Import planning must respect privacy buckets before protected writes are reopened.

Source records touching notes, front history, avatars, custom fields, and privacy bucket assignments require careful mapping and audit-safe references.

## 21. Bravo Data

DB Bravo includes:

1. Custom fronts, full support
2. Comments
3. Polls
4. Automated or repeated timers
5. Filters or saved member views

Bravo data should not be allowed to destabilize the Alpha import gate.

Unsupported Bravo source records should be preserved or reported safely according to import policy.

## 22. Raw Upload Retention And Purge

Raw upload retention must be explicitly bounded.

Candidate retention rules:

1. raw upload quarantined during processing
2. raw upload purged after successful commit after a defined retention window
3. raw upload purged after failed import after a defined retention window
4. source mapping retained without raw payload
5. audit retained without raw payload
6. user-visible status retained according to policy

Retention should align with account closure, System closure, consent revocation, and erasure planning.

## 23. Security And Abuse Boundaries

Upload and import can become abuse surfaces.

Candidate controls:

1. file size limits
2. file type validation
3. content parsing limits
4. request throttling
5. per-account import limits
6. per-System import limits
7. malware scanning consideration
8. storage quarantine
9. secret detection
10. safe parser behavior
11. no execution of uploaded content
12. no external fetch from uploaded content

Security decisions belong in implementation planning, but the import boundary must reserve space for them.

## 24. First Implementation Gate Contribution

PB-NEXT-IMPORT-001 contributes these gate requirements:

1. Import is System-centered.
2. Account actor and target System remain separate.
3. Upload intake does not automatically commit data.
4. Import batch identity exists.
5. Import job ledger exists.
6. Source records and source ID mapping are System-scoped.
7. Import supports validation and dry run before commit.
8. Commit requires authorization, consent, and audit readiness.
9. Import status is user-visible and payload-safe.
10. Completion notification is payload-safe.
11. Raw upload retention and purge are explicit.
12. Idempotency and replay safety are designed before write import resumes.

## 25. Open Design Questions

1. What is the smallest import batch table needed for the first implementation slice?
2. What is the smallest import job ledger needed for the first implementation slice?
3. Which source record types are Alpha import requirements?
4. Which Bravo source record types should be preserved, skipped, or reported?
5. Which import stages require audit?
6. Which import stages require authorization checks?
7. Which import stages require consent checks?
8. Which status messages are safe for user display?
9. Which error messages are safe for diagnostic logs?
10. How long should raw upload files be retained?
11. When should raw upload files be purged?
12. Should dry run be mandatory before commit?
13. Which import actions should be idempotent?
14. Which source record changes should update existing target records?
15. How should failed partial commits be recovered?
16. What should email notification include?
17. Which upload abuse controls are required for the first public implementation?
18. Which parser limits are required before import is reopened?
19. Which import report fields are safe?
20. Which import records survive account closure or System deletion?

## 26. Explicit Non-Goals

Out of scope for this document:

1. Implementing upload
2. Implementing import
3. Implementing parser behavior
4. Implementing validation
5. Implementing dry run
6. Implementing commit
7. Implementing email notification
8. Implementing storage retention
9. Implementing Azure runtime changes
10. Implementing app UI
11. Implementing API endpoints
12. Implementing database tables
13. Implementing release promotion
14. Website changes
15. Airmeet changes
16. Outreach changes

Those decisions belong to later PB-NEXT planning documents and implementation slices.
