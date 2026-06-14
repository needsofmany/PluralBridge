# PluralBridge Post-Conference Blueprint Crosswalk

Status: Planning draft  
Lane: PB-NEXT-001  
Scope: Post-conference planning and design-doc crosswalk  
Implementation status: No application, API, database, import, upload, Azure runtime, website, release, Airmeet, or outreach implementation work is authorized by this document.

## 1. Source Documents

This crosswalk is based on:

1. `pluralbridge_post_conference_design_blueprint_all_32_chapters(1).docx`
2. `Post advice 1st user r slash software architecture.txt`

`Do I Pass.txt` is explicitly out of scope and is not a planning source for PB-NEXT-001.

## 2. Baseline Blueprint Decisions Preserved

The 32-chapter post-conference blueprint remains the governing planning baseline. The r/softwarearchitecture feedback corrects foundation assumptions before implementation begins; it does not replace the blueprint.

Preserved decisions:

1. Implement in vertical slices.
2. Pull the minimum required work from each lane for the current slice.
3. Narrow the slice before compromising the build.
4. Preserve the DB Alpha / DB Bravo coverage split.
5. Preserve import job ledger discipline.
6. Preserve the separation between diagnostic logging and audit/evidence records.
7. Preserve privacy-sensitive handling of imported Simply Plural data.
8. Preserve the app/API/database/import/upload/Azure runtime freeze from v0.7.3 unless explicitly reopened.

## 3. Architecture Correction from r/softwarearchitecture

The prior simplified model treated System as the practical boundary for too many concerns.

Corrected model:

System is the primary isolation boundary and protected domain container.

The following concerns must be modeled separately:

1. Tenancy / isolation
2. Resource ownership
3. Authorization
4. Consent
5. Audit
6. Partitioning / data-layer isolation
7. Data subject / controller / accessor relationships

A login account authenticates a person or user. A System remains the core domain container. Account and System are separate concepts.

A System contains members, groups, privacy buckets, notes, current fronter, front history, custom fronts, avatars, custom fields, import/source records, and audit records.

The authorization and audit model must handle both state-like and record-like data:

1. Current fronter is state-like.
2. Front history is record-like.
3. Custom fronts are record-like, although they can behave like remembered state in the user experience.

## 4. New PB-NEXT Lane Order

The new PB-NEXT planning order is:

1. PB-NEXT-DATA-001 — Partition key and data-layer isolation decision
2. PB-NEXT-USER-001 — Account / System membership model
3. PB-NEXT-AUTHZ-001 — Central authorization decision point
4. PB-NEXT-REB-001 — Relationship-based authorization investigation
5. PB-NEXT-CONSENT-001 — Versioned consent and revocation records
6. PB-NEXT-AUDIT-001 — Payload-free audit event schema with authority references
7. PB-NEXT-IMPORT-001 — Import/export as processing purposes

PB-NEXT-DATA-001 comes first because partition key and data-layer isolation affect every later table and cannot be treated as an implementation detail.

## 5. Blueprint Chapter Crosswalk

| Blueprint Chapter | Existing Planning Role | PB-NEXT Addendum / Correction |
| --- | --- | --- |
| Chapter 3 — Planning Model | Defines the planning discipline and vertical-slice approach. | Add the rule that foundation decisions are resolved before implementation slicing begins. |
| Chapter 4 — Top-Level Lanes | Establishes planning lanes. | Add the new security/ownership foundation lanes ahead of feature implementation lanes. |
| Chapter 5 — User Base Management | Covers users, testers, and stewardship concerns. | Split login account, System membership, resource ownership, and support/helper access. |
| Chapter 6 — ID and Token Terminology | Clarifies identifiers and token language. | Add stable resource IDs for audit references and separate identity-bearing records from sensitive payload where useful. |
| Chapter 7 — Import Lane | Covers import behavior and data intake. | Treat import/export/reporting/support access as explicit processing purposes. |
| Chapter 8 — Import Flow State Model | Covers import workflow state. | Tie import state transitions to authorization, consent, and audit authority references. |
| Chapter 9 — Import Job Ledger | Tracks import jobs and resumability/evidence. | Preserve ledger discipline while keeping sensitive payload out of audit-style evidence records. |
| Chapter 10 — Database Coverage | Establishes database coverage planning. | Add partition key and data-layer isolation as the first database design decision. |
| Chapter 11 — DB Alpha | Defines first-priority database coverage. | Preserve DB Alpha while checking every table for System isolation, membership, consent, and audit references. |
| Chapter 12 — Privacy Buckets | Covers privacy grouping and visibility. | Model privacy behavior through central authorization decisions instead of scattered inline checks. |
| Chapter 13 — Custom Fronts | Covers custom-front support. | Treat custom fronts as record-like resources for ownership, authorization, consent, and audit. |
| Chapter 20 — Logging | Covers diagnostic logging. | Keep diagnostic logging separate from audit/evidence records. |
| Chapter 21 — Audit / Evidence / Policy Traceability | Covers audit and traceability. | Add payload-free audit events with stable resource IDs and authority references. |
| Chapter 23 — User Delete / Account Closure | Covers deletion and closure. | Add erasure, revocation, audit retention, and possible crypto-shredding/key-destruction decisions. |

## 6. DB Alpha Preserved

DB Alpha remains:

1. Groups
2. Notes
3. Privacy buckets
4. Current fronter
5. Opt-in text notification support for current-fronter changes
6. Avatars
7. Minimal custom-front support only where needed for current-front/front-history integrity

## 7. DB Bravo Preserved

DB Bravo remains:

1. Custom fronts, full support
2. Comments
3. Polls
4. Automated/repeated timers
5. Filters / saved member views

## 8. Required Design Questions Before Coding

### PB-NEXT-DATA-001 — Partition Key and Data-Layer Isolation

1. Is `SystemId` the primary partition key for all protected domain data?
2. Are any resources better partitioned by data subject, account, import batch, or hybrid key?
3. How will data-layer isolation be enforced so application code does not manually remember tenancy rules?
4. Which tables require System-scoped uniqueness?
5. Which tables require stable resource IDs for audit references?

### PB-NEXT-USER-001 — Account / System Membership Model

1. What is the minimal account model for early implementation?
2. What is the minimal System membership model?
3. Can one account belong to multiple Systems?
4. Can multiple accounts administer or steward one System?
5. How are helpers, providers, testers, and support access represented?

### PB-NEXT-AUTHZ-001 — Central Authorization Decision Point

1. What is the shape of the central authorization question?
2. What context is required for an allow/deny decision?
3. What obligations can a decision return?
4. Which actions exist in the first implementation slice?
5. Which checks must be centralized before any add/edit/delete feature is implemented?

Candidate authorization question:

`Can subject S do action A on resource R in context C?`

Decision output:

1. allow / deny
2. obligations
3. authority references
4. policy version
5. consent version when applicable

### PB-NEXT-REB-001 — Relationship-Based Authorization Investigation

Investigate relationship-based authorization patterns before selecting a final implementation shape.

Research candidates:

1. Zanzibar-style relationship modeling
2. OpenFGA
3. SpiceDB
4. Cedar
5. Cerbos

Expected output:

1. Which relationship patterns PluralBridge needs
2. Which can be implemented directly in the application/database
3. Which require a policy engine later
4. Which decisions are irreversible enough to settle before coding

### PB-NEXT-CONSENT-001 — Versioned Consent and Revocation Records

Consent must be first-class, versioned, purpose-specific, and time-bounded where appropriate.

Suggested consent record shape:

1. subject
2. purpose
3. scope
4. grantee
5. granted_at
6. expires_at
7. revoked_at
8. artifact_version

Design rule:

Revocation stops future processing. It does not erase past disclosures. Past disclosures remain audit evidence.

### PB-NEXT-AUDIT-001 — Payload-Free Audit Event Schema With Authority References

Audit records should answer:

1. who acted
2. what action occurred
3. which stable resource ID was affected
4. under what authority the action was allowed
5. which role, relationship, consent, or policy version applied
6. when the action occurred

Audit records must not become a second copy of PII.

Audit records should reference stable IDs and authority artifacts instead of copying sensitive payload.

Diagnostic logs and audit/evidence records remain separate.

Consider a reliable outbox pattern when missing audit events would be unacceptable.

Consider hash chaining only if tamper evidence is needed. Do not introduce blockchain.

### PB-NEXT-IMPORT-001 — Import/Export as Processing Purposes

Import, export, reporting, and support access may each be separate processing purposes.

Import design must identify:

1. who initiated processing
2. which System is the protected container
3. which data subject or subjects are affected
4. which consent or authority permits processing
5. which audit references prove the action without copying payload

## 9. First Implementation Gate

Coding may begin only after these planning outputs exist:

1. Partition key and isolation decision
2. Account / System membership model
3. Central authorization decision shape
4. Relationship model baseline
5. Consent/revocation record baseline
6. Payload-free audit event baseline
7. Import/export purpose model baseline
8. First vertical slice definition using the corrected foundation

The first implementation slice must remain narrow. If scope pressure appears, reduce the slice rather than compromise privacy, correctness, authorization, auditability, or repo discipline.

## 10. Explicit Non-Goals

This PB-NEXT-001 crosswalk does not authorize:

1. App/API/database/import/upload/Azure runtime implementation
2. Website changes
3. Airmeet work
4. Release promotion
5. Broad outreach
6. Developer recruitment on r/softwarearchitecture
7. Public posting of demo credentials
8. Replacing the 32-chapter blueprint
9. Expanding DB Bravo into DB Alpha without a specific planning decision

## 11. Immediate Next Planning Artifacts

Likely follow-up planning files:

1. `PB-NEXT-DATA-001.md`
2. `PB-NEXT-USER-001.md`
3. `PB-NEXT-AUTHZ-001.md`
4. `PB-NEXT-REB-001.md`
5. `PB-NEXT-CONSENT-001.md`
6. `PB-NEXT-AUDIT-001.md`
7. `PB-NEXT-IMPORT-001.md`

These should be created in order unless a later design finding forces the order to change.
