# PB-NEXT-UPLOAD-INTAKE-001 — Upload Intake Implementation Plan

## 1. Purpose

This planning document defines the implementation plan for the PB-NEXT upload intake boundary.

Upload intake is the first controlled handoff point for user-supplied Simply Plural export files before validation, parsing, import job processing, source record handling, source id mapping, and domain write planning.

Scope is limited to planning/docs. Runtime behavior remains unchanged.

## 2. Design Principle

Upload intake accepts a file, binds it to an Account and System, performs early safety and eligibility checks, creates durable intake references, and hands the work to the import job ledger.

Upload intake should behave like a controlled receiving dock.

It records enough facts to support security, retry, cleanup, user status, audit correlation, and operator review while keeping payload handling behind a narrow storage and retention boundary.

## 3. Intake Boundary

Upload intake is separate from:

- import job ledger
- source record storage
- source id mapping
- parser implementation
- domain write planning
- audit event schema
- operational logging
- user notification delivery

Upload intake owns the initial upload request, accepted file reference, basic file facts, early rejection decisions, and handoff to import job creation.

The import job ledger owns lifecycle state after intake acceptance.

The parser owns content interpretation.

Source record storage owns imported payload handling after parser acceptance.

## 4. Accepted Inputs

The first upload intake implementation should accept one Simply Plural export file per intake request.

Initial accepted input facts should include:

- UploadId
- SystemId
- AccountId
- original client filename when approved by privacy planning
- normalized file extension
- content type reported by client
- content type detected by server
- file size in bytes
- upload received timestamp
- upload storage reference
- intake status
- rejection reason code when rejected
- CorrelationId
- RequestId when available

Original filenames may contain private meaning.

The implementation plan should decide whether to store the original filename, store a redacted form, or store only the extension and generated upload reference.

## 5. Required Partition Anchor

SystemId is required on upload intake records.

The uploaded export represents System-owned data.

AccountId identifies the account that initiated the upload request.

All intake review, cleanup, import handoff, and rejection handling should be scoped through SystemId first.

Upload intake records should align with the PB-NEXT data decision that SystemId is the dominant privacy and partition boundary.

## 6. Upload Identity

UploadId should be a stable internal identifier generated when the upload request is accepted for intake handling.

UploadId should be referenced by:

- import job ledger
- audit events
- operational logs
- user-facing upload status
- cleanup processing
- support review if later approved
- retry or reprocessing planning when applicable

UploadId should identify the received upload artifact.

ImportJobId should identify the lifecycle object created from that upload.

A single UploadId should usually feed one ImportJobId in the first implementation slice.

## 7. Intake Status Model

The first upload intake status model should be controlled and explicit.

Initial statuses should include:

- Received
- Rejected
- Stored
- IntakeValidationFailed
- IntakeValidationPassed
- ImportJobCreated
- Expired
- Deleted

Received means the request reached the intake boundary.

Stored means the upload artifact has a durable storage reference.

IntakeValidationPassed means basic eligibility checks completed.

ImportJobCreated means handoff to the import job ledger succeeded.

Expired and Deleted support cleanup and retention planning.

## 8. Early Rejection Rules

Upload intake should reject files before import job creation when basic safety or eligibility checks fail.

Initial rejection rule families should include:

- empty file
- file too large
- unsupported extension
- unsupported content type
- unreadable upload stream
- upload storage failure
- account lifecycle gate failed
- System membership gate failed
- System closed
- duplicate upload rejected by idempotency policy

Rejection records should use controlled reason codes.

Rejection records should avoid storing payload excerpts, parser errors, source field values, or user-entered freeform content.

## 9. Intake Validation

Upload intake validation should remain shallow.

It should determine whether the upload can safely enter the import pipeline.

Initial intake validation should check:

- request authentication
- account lifecycle eligibility
- System membership eligibility
- System state eligibility
- file presence
- file size
- file extension
- detected content type
- storage write success
- duplicate request handling
- import job creation eligibility

Deep Simply Plural schema validation belongs to the parser and import validation lanes.

Intake validation should reject unsafe or ineligible uploads early without interpreting private payload content.

## 10. Storage Reference Model

Upload intake should store a durable reference to the uploaded artifact, not the file bytes inside the intake record.

The storage reference should support:

- lookup by UploadId
- cleanup by SystemId
- expiration by retention policy
- import job handoff
- audit and log correlation
- future quarantine or rejection handling

Storage references should avoid exposing user-controlled filenames.

A generated object key should be preferred for storage.

The database record should carry enough controlled metadata to locate and manage the artifact without storing private payload data in ledger fields.

## 11. File Name And Content Type Policy

Client-provided filenames and content types should be treated as untrusted.

The first implementation should prefer server-generated storage names.

The plan should decide whether the original filename is:

- not stored
- stored only in operational logs under strict policy
- stored in redacted form
- stored as a hashed value
- stored as user-visible metadata after explicit approval

Detected content type should be recorded separately from the client-reported content type.

Content type mismatch should produce a controlled reason code.

Filename and content type facts should never be used as the only validation signal.

## 12. Size Limits And Quotas

Upload intake should enforce explicit file size limits.

The first implementation should define:

- maximum single upload size
- maximum pending uploads per System
- maximum pending uploads per Account
- cleanup window for abandoned uploads
- retry behavior after size rejection
- user-facing rejection reason

Quota enforcement should be System-scoped first.

Account-level throttles may be added to prevent abuse.

Size and quota decisions should use controlled reason codes and should be eligible for audit when the decision blocks import creation.

## 13. Idempotency And Duplicate Handling

Upload intake should tolerate duplicate browser submissions, network retries, and user refreshes.

The first implementation should define an idempotency policy for upload acceptance.

Possible inputs include:

- AccountId
- SystemId
- request idempotency key
- generated UploadId
- file hash if approved by privacy planning
- upload size
- received timestamp window

File hashing may have privacy implications and should be explicitly approved before implementation.

Duplicate handling should prevent accidental double imports while preserving a clear path for intentional new imports.

## 14. Import Job Handoff

A successful upload intake should create or request creation of an import job.

The handoff should pass controlled references:

- UploadId
- SystemId
- AccountId
- source application name when known
- storage reference id
- file size
- detected content type
- CorrelationId
- RequestId when available

The handoff should not pass raw payload content through durable ledger fields.

The import job ledger should become the lifecycle authority after handoff.

Upload intake should retain enough status to explain whether the handoff succeeded or failed.

## 15. Audit And Log Integration

Upload intake should integrate with audit events and operational logs through stable identifiers.

Meaningful audit events may include:

- upload received
- upload rejected
- upload stored
- intake validation failed
- intake validation passed
- import job created
- upload expired
- upload deleted

Shared identifiers should include:

- UploadId
- ImportJobId when created
- SystemId
- AccountId
- CorrelationId
- RequestId when available

Operational logs may carry diagnostics under separate logging policy.

Audit events should carry accountability facts without storing uploaded payload content.

## 16. Cleanup And Retention

Upload intake requires explicit cleanup and retention planning.

Cleanup policy should define:

- retention period for accepted upload artifacts
- retention period for rejected upload artifacts
- retention period for intake records
- cleanup behavior after import completion
- cleanup behavior after import failure
- cleanup behavior after cancellation
- cleanup behavior after System closure
- cleanup behavior after account closure

The upload artifact retention window may be shorter than the import job ledger retention window.

Cleanup should preserve enough controlled intake history to explain import lifecycle decisions after the upload artifact is removed.

## 17. Security And Authorization Gates

Upload intake operations should be protected by explicit authorization gates.

Required gates include:

- start upload intake
- store upload artifact
- create import job from upload
- view upload intake status
- cancel pending intake when supported
- expire upload artifact
- delete upload artifact
- support review of upload intake state
- administrative repair of intake state

Each gate should evaluate account lifecycle status, System membership, System state, relationship authority when applicable, consent when applicable, and support authority when applicable.

Allowed and denied decisions for meaningful gates should produce audit events.

Upload intake should be treated as access to privacy-sensitive System data before parsing begins.

## 18. Threat Handling

Upload intake should include a basic threat handling plan before runtime implementation.

The plan should define:

- accepted file extensions
- accepted server-detected content types
- maximum file size
- upload stream handling
- storage isolation
- quarantine behavior when later needed
- rejection behavior for unreadable uploads
- cleanup behavior for rejected uploads
- audit behavior for security-sensitive rejection decisions

Threat handling should remain payload-aware at the storage boundary and payload-minimizing in durable intake records.

If malware scanning is added later, scan results should use controlled status and reason codes.

Scanner output should be reviewed before any durable storage of scanner message text.

## 19. Privacy And Redaction Rules

Upload intake records should minimize stored personal data.

The intake record should prefer generated identifiers, controlled statuses, counts, timestamps, and storage references.

Privacy-sensitive fields include:

- original filename
- client-reported content type
- IP address
- user agent
- freeform error text
- storage object key if it embeds user-supplied values
- file hash if it can become a durable fingerprint of private exports

The first implementation should define which of these fields are stored, redacted, hashed, deferred, or restricted to operational logs.

Durable intake records should remain useful after the uploaded artifact is expired or deleted.

## 20. User-Facing State

Upload intake should provide enough structured state for a future user-facing import flow.

A future status view may show:

- upload received
- upload accepted
- upload rejected
- upload stored
- import job created
- upload expired
- upload deleted
- high-level rejection reason
- next available action

The user-facing layer should translate controlled statuses and reason codes into readable text.

The durable intake record should remain structured.

User-facing message text should be generated from controlled state rather than stored as the authority of record.

## 21. Failure Modes

Upload intake should define clear failure handling.

Initial failure modes include:

- unauthenticated request
- account lifecycle gate failed
- System membership gate failed
- System closed
- missing file
- empty file
- file too large
- unsupported extension
- unsupported detected content type
- storage write failed
- import job creation failed
- duplicate upload policy rejected the request
- cleanup failed

Each failure should map to a controlled reason code.

Failure handling should preserve enough information to explain the result without copying payload content into intake records.

## 22. Index Planning

The first database implementation should plan indexes around System-scoped review and cleanup.

Likely index candidates include:

- SystemId plus CreatedAtUtc
- SystemId plus IntakeStatus
- SystemId plus UploadId
- AccountId plus CreatedAtUtc
- ImportJobId
- CorrelationId
- IntakeStatus plus UpdatedAtUtc
- ExpirationAtUtc
- StorageReferenceId

Cleanup jobs should be able to find expired upload artifacts without scanning unrelated System data.

Support or administrative review should use explicitly authorized query paths.

## 23. Initial Implementation Slice

The first implementation slice should be narrow.

Recommended first durable intake coverage:

- UploadId
- SystemId
- AccountId
- IntakeStatus
- storage reference id
- normalized extension
- detected content type
- file size in bytes
- created timestamp
- updated timestamp
- expiration timestamp
- rejection reason code
- ImportJobId after handoff
- CorrelationId
- RequestId when available

The first slice can defer advanced quota handling, malware scanning, filename retention, file hashing, quarantine, and support review.

The first slice should still preserve the payload-free intake record rule.

## 24. Open Questions

Open questions before implementation:

- What exact table stores upload intake records?
- What storage provider and object key pattern should hold uploaded artifacts?
- Should original filenames be stored, redacted, hashed, or discarded?
- What maximum file size applies to the first release?
- What detected content types are accepted?
- What retention window applies to accepted upload artifacts?
- What retention window applies to rejected upload artifacts?
- What idempotency key protects duplicate browser submissions?
- Should file hashes be computed in the first slice?
- What cleanup process removes expired upload artifacts?
- Which upload intake decisions require durable audit events?

These questions should be resolved before schema migration or runtime upload work begins.
