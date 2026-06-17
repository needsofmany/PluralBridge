# PB-NEXT-GATE-001 — First Implementation Gate And Sequencing Plan

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`, `PB-NEXT-REB-001.md`, `PB-NEXT-CONSENT-001.md`, `PB-NEXT-AUDIT-001.md`, `PB-NEXT-IMPORT-001.md`  
Scope: Convert the PB-NEXT foundation planning documents into an implementation gate, ordering constraints, and first-slice sequence.  
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document defines the first gate before PluralBridge implementation work resumes after the post-conference planning pass.

It does not implement app, API, database, import, upload, Azure runtime, website, Airmeet, outreach, or release behavior.

It defines the order in which implementation work should begin so that privacy, authorization, consent, audit, and import safety are not bolted on after data writes already exist.

## 2. Foundation Inputs

The implementation gate depends on these planning files:

1. `BLUEPRINT_CROSSWALK.md`
2. `PB-NEXT-DATA-001.md`
3. `PB-NEXT-USER-001.md`
4. `PB-NEXT-AUTHZ-001.md`
5. `PB-NEXT-REB-001.md`
6. `PB-NEXT-CONSENT-001.md`
7. `PB-NEXT-AUDIT-001.md`
8. `PB-NEXT-IMPORT-001.md`

These documents define the minimum architecture foundation for reopening implementation work.

## 3. Gate Decision

Implementation may resume only as narrow slices that preserve these boundaries:

1. Account authenticates a person or user.
2. System is the protected domain container.
3. Account and System remain separate.
4. Membership and relationships express authority.
5. Authorization goes through a central decision point.
6. Consent is first-class and purpose-specific.
7. Audit is payload-free and authority-referenceable.
8. Import is System-centered and ledgered.
9. Raw private payload is never copied into diagnostic logs or audit records.
10. Upload and import do not automatically commit protected data.

## 4. Non-Negotiable Constraints

Implementation slices must not cut across these constraints:

1. No single-owner shortcut that collapses Account, System, membership, and ownership.
2. No boolean-only consent model.
3. No consent modeled as ReBAC tuples.
4. No scattered inline authorization checks as the primary authorization model.
5. No audit records containing copied private payload.
6. No diagnostic logs containing private payload.
7. No import commit path without authorization, consent, and audit readiness.
8. No raw upload retention without explicit purge policy.
9. No public runtime behavior change unless the task explicitly reopens runtime behavior.
10. No broad implementation slice when a narrow planning or schema slice will do.

## 5. Recommended Implementation Sequence

The first implementation work should proceed in this order:

1. Identity and Account lifecycle foundation
2. System lifecycle and membership foundation
3. Central authorization decision shell
4. Relationship model foundation
5. Consent record foundation
6. Payload-free audit foundation
7. Import batch and job ledger foundation
8. Upload intake planning-to-schema bridge
9. Validation and dry-run import path
10. Commit path for the smallest safe Alpha data slice

This order keeps expensive-to-reverse decisions ahead of protected writes.

## 6. Slice 1: Account Lifecycle Foundation

Goal: establish Account lifecycle state without conflating Account with System.

Candidate outputs:

1. Account status model
2. Account lifecycle transition plan
3. active, disabled, pending, closed, and recovery states
4. stable Account ID expectations
5. account closure placeholder behavior
6. no provider/helper/support access yet

Exit criteria:

1. Account lifecycle state exists in planning or schema form.
2. System ownership is not modeled as a direct Account shortcut.
3. Later authorization can evaluate Account state.

## 7. Slice 2: System And Membership Foundation

Goal: establish System as the protected container and membership as the first authority layer.

Candidate outputs:

1. System lifecycle state
2. System membership table or schema plan
3. membership role names
4. Steward or equivalent initial authority
5. membership status transitions
6. last-Steward open question preserved

Exit criteria:

1. System is the partition and protection boundary.
2. Membership is separate from Account.
3. Membership can feed authorization.

## 8. Slice 3: Central Authorization Decision Shell

Goal: create the single conceptual access-check path before domain writes expand.

Candidate outputs:

1. authorization decision interface
2. subject, action, resource, context decision shape
3. allow or deny result
4. reason code
5. obligation placeholder
6. authority reference placeholder
7. no broad inline authorization sprawl

Exit criteria:

1. New protected actions have a central decision path.
2. Authorization result can carry audit obligations.
3. Consent, relationship, membership, and policy inputs have defined places.

## 9. Slice 4: Relationship Foundation

Goal: add relationship authority without treating ReBAC as the whole authorization model.

Candidate outputs:

1. relationship table or schema plan
2. relationship type list
3. relationship status list
4. relationship scope
5. expiration or revocation fields
6. delegation/helper/provider placeholders

Exit criteria:

1. Relationships can express authority beyond coarse roles.
2. Relationships remain separate from consent.
3. Relationship IDs can be referenced by audit.

## 10. Slice 5: Consent Foundation

Goal: establish purpose-specific consent before processing expands.

Candidate outputs:

1. consent record table or schema plan
2. purpose field
3. scope field
4. grantee field
5. status field
6. granted, expired, revoked, and superseded states
7. artifact or policy version reference

Exit criteria:

1. Consent is not a boolean.
2. Consent is not encoded as relationship tuples.
3. Revocation blocks future processing where consent is required.
4. Past audit evidence remains intact.

## 11. Slice 6: Payload-Free Audit Foundation

Goal: establish audit evidence before sensitive mutation paths expand.

Candidate outputs:

1. audit event table or schema plan
2. actor reference
3. System reference
4. resource reference
5. action
6. decision
7. authority references
8. request or correlation ID
9. audit classification

Exit criteria:

1. Audit and diagnostic logging remain separate.
2. Audit can reference membership, relationship, consent, and policy authority.
3. Audit does not copy private payload.

## 12. Slice 7: Import Batch And Job Ledger Foundation

Goal: model import as a controlled processing pipeline before reopening import writes.

Candidate outputs:

1. import batch table or schema plan
2. import job ledger table or schema plan
3. source record identity plan
4. source ID map plan
5. import lifecycle states
6. safe status message codes
7. error category list

Exit criteria:

1. Import is System-centered.
2. Upload intake does not commit protected data.
3. Import state transitions are visible and safe.
4. Import records avoid private payload leakage.

## 13. Slice 8: Upload Intake Boundary

Goal: define the first safe upload boundary.

Candidate outputs:

1. file metadata record
2. file hash
3. file size
4. source system marker
5. target System marker
6. quarantine or storage decision
7. raw upload retention placeholder
8. purge placeholder

Exit criteria:

1. Upload is actor-bound and System-bound.
2. Raw upload content does not enter audit or diagnostic logs.
3. Upload can be rejected safely.
4. Import batch can be created without committing domain data.

## 14. Slice 9: Validation And Dry Run

Goal: create a safe read/plan path before commit.

Candidate outputs:

1. parser boundary plan
2. validation result shape
3. dry-run result shape
4. safe warnings
5. safe errors
6. record counts
7. unsupported Bravo handling
8. Alpha import gate mapping

Exit criteria:

1. Dry run can explain what would happen without writing target domain records.
2. Unsupported data can be reported safely.
3. Sensitive payload does not leak into user-visible errors, logs, or audit.

## 15. Slice 10: Smallest Safe Alpha Commit

Goal: commit only the smallest protected data slice after all prior gates are satisfied.

Candidate first commit candidates:

1. System metadata only
2. groups only
3. privacy buckets only
4. members without private notes or avatars
5. source ID map only

Excluded from first commit unless explicitly reopened:

1. notes
2. avatars
3. private front history
4. custom fronts beyond integrity minimum
5. comments
6. polls
7. automated or repeated timers
8. filters or saved views

Exit criteria:

1. Authorization is enforced centrally.
2. Consent is evaluated where required.
3. Audit events are written where required.
4. Import job ledger records the commit.
5. Source ID map supports idempotency.
6. Status remains user-visible and payload-safe.

## 16. First Work Item Recommendation

The next practical planning document should be:

`PB-NEXT-IDENTITY-MEMBERSHIP-001 — Identity And Membership Implementation Plan`

That document should translate Slice 1 and Slice 2 into concrete schema and task boundaries.

It should remain planning-first unless explicitly promoted to implementation.

## 17. Completion Definition For This Gate

This gate is complete when:

1. All eight foundation documents are merged to `dev`.
2. This gate document is merged to `dev`.
3. The next implementation planning document is selected.
4. No app, API, database, import, upload, Azure runtime, website, Airmeet, outreach, or release behavior has changed.
5. The repo remains clean after merge verification.

## 18. Explicit Non-Goals

Out of scope for this document:

1. Writing implementation code
2. Creating database migrations
3. Changing API endpoints
4. Changing app UI
5. Changing import behavior
6. Changing upload behavior
7. Changing Azure runtime behavior
8. Changing website behavior
9. Promoting a release
10. Running outreach
11. Reworking Airmeet or conference materials

Those actions require separate, explicit implementation or release tasks.
