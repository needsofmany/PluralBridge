# PB-NEXT-IMPLEMENTATION-QUEUE-001 — Final Ordered Implementation Task Queue

## 1. Purpose

This planning document defines the final ordered implementation queue for PB-NEXT.

The queue converts the PB-NEXT implementation gate planning documents into a sequence of actual implementation branches.

This lane is planning/docs only. It does not change app, API, database runtime behavior, import behavior, upload behavior, Azure runtime, website, Airmeet, release, or outreach behavior.

## 2. Design Principle

Implementation should proceed in narrow, reviewable slices.

Each branch should have one clear authority boundary, one primary deliverable, and one verification target.

The implementation queue should preserve the safety decisions already made across identity, membership, authorization, relationship authority, consent, audit, import, upload intake, dry run, and Alpha commit planning.

The queue should make the next engineering action obvious without reopening settled architecture at every step.

## 3. Source Planning Documents

The implementation queue is derived from these planning documents:

- PB-NEXT-DATA-001
- PB-NEXT-USER-001
- PB-NEXT-AUTHZ-001
- PB-NEXT-REB-001
- PB-NEXT-CONSENT-001
- PB-NEXT-AUDIT-001
- PB-NEXT-IMPORT-001
- PB-NEXT-GATE-001
- PB-NEXT-IDENTITY-MEMBERSHIP-001
- PB-NEXT-AUTHZ-SHELL-001
- PB-NEXT-RELATIONSHIP-001
- PB-NEXT-CONSENT-SCHEMA-001
- PB-NEXT-AUDIT-SCHEMA-001
- PB-NEXT-IMPORT-JOB-LEDGER-001
- PB-NEXT-UPLOAD-INTAKE-001
- PB-NEXT-DRYRUN-001
- PB-NEXT-ALPHA-COMMIT-001

These documents form the gate record for beginning implementation work after this queue is approved.

## 4. Implementation Rules

Implementation branches should follow these rules:

- create branches from current dev
- keep each branch narrow
- preserve planning/docs history
- add implementation code only inside explicit implementation branches
- keep app/API/database/import/upload runtime surfaces frozen until their owning branch is opened
- avoid mixed concerns in one PR
- write tests with the implementation slice where practical
- merge to dev only after verification
- sync local dev after every browser merge
- verify clean status after every sync

Branches that touch security, consent, audit, import commit, or deletion behavior should include explicit review notes in the PR body.

## 5. Queue Shape

The implementation queue is organized into ordered branch groups.

Branch groups should be completed in this order:

1. repository and safety baseline
2. identity and membership foundation
3. authorization shell integration
4. relationship authority records
5. consent records and lifecycle
6. audit event store
7. import job ledger
8. upload intake
9. dry run validation and planning
10. Alpha commit path
11. final gate review and release readiness

A later group may depend on stable outputs from earlier groups.

When a group exposes a risk that changes the architecture, the queue should pause for a planning correction rather than burying the decision inside code.

## 6. Group 0 Repository And Safety Baseline

Group 0 should prepare the implementation surface before application behavior changes.

Recommended branches:

- feature/pb-next-impl-000-repo-baseline
- feature/pb-next-impl-001-test-baseline
- feature/pb-next-impl-002-config-boundary

Primary goals:

- confirm current dev is clean
- confirm current tests or build checks are known
- document baseline command results
- add missing test scaffolding only when required for later slices
- avoid runtime behavior changes
- avoid schema changes
- avoid import behavior changes

Exit criteria:

- dev has a known verification baseline
- implementation branch naming is settled
- build and test commands are documented
- no app/API/import/upload behavior has changed

## 7. Group 1 Identity And Membership Foundation

Group 1 should implement the minimum identity and membership data foundation required before authorization decisions become meaningful.

Recommended branches:

- feature/pb-next-impl-010-account-system-schema
- feature/pb-next-impl-011-membership-schema
- feature/pb-next-impl-012-account-lifecycle-state
- feature/pb-next-impl-013-system-lifecycle-state
- feature/pb-next-impl-014-initial-steward-membership

Primary goals:

- create Account and System persistence shape
- create membership persistence shape
- represent Account lifecycle states
- represent System lifecycle states
- represent initial Steward membership
- prepare stable ids for later authorization, consent, audit, and import references

Exit criteria:

- Account, System, and membership records have stable identifiers
- SystemId is available as the primary partition anchor
- membership can support later authorization decisions
- lifecycle states can block unsafe operations
- no import commit behavior has been introduced

## 8. Group 2 Authorization Shell Integration

Group 2 should implement the central authorization decision shell before domain-specific policies are wired into runtime behavior.

Recommended branches:

- feature/pb-next-impl-020-authz-decision-model
- feature/pb-next-impl-021-authz-reason-codes
- feature/pb-next-impl-022-authz-authority-reference
- feature/pb-next-impl-023-authz-obligation-model
- feature/pb-next-impl-024-authz-test-fixtures

Primary goals:

- create the authorization decision shape
- model subject, action, resource, context, outcome, reason codes, authority references, and obligations
- prepare seams for relationship authority, consent, audit, and import checks
- keep initial behavior testable before broad runtime integration

Exit criteria:

- authorization decisions can be represented consistently
- allowed and denied outcomes carry controlled reason codes
- authority references can be attached
- later groups can call the shell without inventing parallel access checks

## 9. Group 3 Relationship Authority Records

Group 3 should implement the relationship authority model after the authorization shell exists.

Recommended branches:

- feature/pb-next-impl-030-relationship-schema
- feature/pb-next-impl-031-relationship-lifecycle
- feature/pb-next-impl-032-relationship-authority-binding
- feature/pb-next-impl-033-relationship-authz-fixtures

Primary goals:

- create relationship authority records
- represent grant, change, revoke, expire, and supersede states
- bind relationship authority into the authorization decision shell
- preserve stable authority references for audit events
- keep relationship authority separate from direct membership

Exit criteria:

- relationship authority can be represented as a distinct access source
- authorization decisions can reference relationship authority
- revoked and expired authority remains explainable
- audit-ready stable identifiers exist

## 10. Group 4 Consent Records And Lifecycle

Group 4 should implement consent records after identity, membership, authorization, and relationship authority seams exist.

Recommended branches:

- feature/pb-next-impl-040-consent-schema
- feature/pb-next-impl-041-consent-lifecycle
- feature/pb-next-impl-042-consent-versioning
- feature/pb-next-impl-043-consent-authz-binding
- feature/pb-next-impl-044-consent-test-fixtures

Primary goals:

- create consent record persistence
- represent consent grant, change, revoke, expire, and supersede states
- preserve consent versions for later decision explanation
- bind consent checks into the authorization decision shell
- prepare consent references for audit events

Exit criteria:

- consent state can be evaluated by authorization decisions
- consent version references can explain past decisions
- missing, expired, and revoked consent produce controlled reason codes
- later import and commit gates can depend on consent checks

## 11. Group 5 Audit Event Store

Group 5 should implement the payload-free audit event store before import commit behavior begins.

Recommended branches:

- feature/pb-next-impl-050-audit-schema
- feature/pb-next-impl-051-audit-event-writer
- feature/pb-next-impl-052-audit-reason-codes
- feature/pb-next-impl-053-audit-correlation
- feature/pb-next-impl-054-audit-test-fixtures

Primary goals:

- create the audit event persistence shape
- preserve SystemId as the primary partition anchor
- record actor, action, target, authority reference, outcome, reason code, and correlation id
- enforce the payload-free audit boundary
- create fixtures for allowed and denied security-relevant decisions

Exit criteria:

- audit events can be written without storing private payloads
- authorization decisions can emit durable audit records
- audit records carry stable authority references
- import, upload, dry run, and commit groups have an audit target

## 12. Group 6 Import Job Ledger

Group 6 should implement the import job ledger after audit storage exists.

Recommended branches:

- feature/pb-next-impl-060-import-job-schema
- feature/pb-next-impl-061-import-transition-history
- feature/pb-next-impl-062-import-attempt-model
- feature/pb-next-impl-063-import-reason-codes
- feature/pb-next-impl-064-import-ledger-audit-binding

Primary goals:

- create import job header persistence
- create transition history
- create attempt tracking
- preserve SystemId and ImportJobId as stable review anchors
- integrate meaningful lifecycle transitions with audit events
- keep payload data outside ledger rows

Exit criteria:

- import jobs have durable lifecycle state
- retries and attempts can be represented
- transition history explains job movement
- upload intake and dry run can attach to ImportJobId

## 13. Group 7 Upload Intake

Group 7 should implement upload intake after the import job ledger can receive handoff.

Recommended branches:

- feature/pb-next-impl-070-upload-intake-schema
- feature/pb-next-impl-071-upload-storage-reference
- feature/pb-next-impl-072-upload-validation-gates
- feature/pb-next-impl-073-upload-import-job-handoff
- feature/pb-next-impl-074-upload-cleanup-policy

Primary goals:

- create upload intake records
- store generated upload references
- enforce shallow intake validation
- create import job handoff
- define cleanup and expiration behavior
- preserve original filename privacy decisions

Exit criteria:

- an upload can be represented without durable payload leakage
- rejected uploads produce controlled reason codes
- accepted uploads can create import jobs
- cleanup can find expired upload artifacts

## 14. Group 8 Dry Run Validation And Planning

Group 8 should implement dry run processing after upload intake and import job ledger foundations exist.

Recommended branches:

- feature/pb-next-impl-080-dryrun-schema
- feature/pb-next-impl-081-dryrun-validation-results
- feature/pb-next-impl-082-dryrun-mapping-preview
- feature/pb-next-impl-083-dryrun-operation-summary
- feature/pb-next-impl-084-dryrun-commit-eligibility

Primary goals:

- create dry run result persistence
- represent validation results with stable rule ids
- summarize mapping preview and planned operations
- calculate commit eligibility
- preserve payload-minimizing dry run state
- prepare handoff to Alpha commit

Exit criteria:

- dry run can produce controlled warnings, errors, and blocked states
- commit eligibility is explainable
- planned operation summaries exist for Alpha scope
- expired dry runs cannot be committed

## 15. Group 9 Alpha Commit Path

Group 9 should implement the smallest safe Alpha commit path after dry run commit eligibility exists.

Recommended branches:

- feature/pb-next-impl-090-alpha-commit-schema
- feature/pb-next-impl-091-alpha-commit-authz
- feature/pb-next-impl-092-alpha-source-id-mapping
- feature/pb-next-impl-093-alpha-domain-write-slice
- feature/pb-next-impl-094-alpha-ledger-audit-binding
- feature/pb-next-impl-095-alpha-idempotency-and-repair

Primary goals:

- create commit run identity and status
- perform final authorization and consent checks
- commit source id mappings
- write the smallest safe Alpha domain slice
- emit import ledger transitions and audit events
- protect against duplicate commit attempts
- define repair state for partial failure

Exit criteria:

- one narrow committed import path exists
- duplicate commit attempts are blocked or resolved safely
- source mappings explain committed records
- ledger and audit records explain the commit result
- partial failure enters explicit repair state

## 16. Group 10 Final Gate Review And Release Readiness

Group 10 should evaluate whether the implementation sequence can advance beyond Alpha.

Recommended branches:

- feature/pb-next-impl-100-alpha-gate-review
- feature/pb-next-impl-101-security-review-notes
- feature/pb-next-impl-102-privacy-review-notes
- feature/pb-next-impl-103-release-readiness-checklist

Primary goals:

- review implemented slices against planning gates
- confirm no payload-free boundary was violated
- confirm SystemId partitioning is preserved
- confirm authorization, consent, audit, ledger, dry run, and commit flows align
- document known deferred work
- decide whether the next implementation wave can begin

Exit criteria:

- Alpha implementation has a clean gate record
- deferred risks are explicit
- release readiness is documented
- next implementation wave has a clear starting point

## 17. Cross-Cutting Branch Contract

Every implementation branch should state its contract before code is merged.

The contract should include:

- owning planning document
- branch group
- branch number
- primary deliverable
- files expected to change
- files expected to remain unchanged
- runtime behavior expected to remain unchanged or change
- test or verification command
- rollback expectation
- deferred work created by the branch

A branch should avoid hidden scope expansion.

When a branch discovers missing planning, the correct action is a narrow planning correction branch before implementation continues.

## 18. Verification Contract

Each implementation branch should define verification before merge.

Verification may include:

- build command
- unit test command
- integration test command
- schema inspection command
- migration dry run command
- targeted file diff review
- expected clean git status
- browser verification only when the branch intentionally changes UI behavior

Verification output should be scoped to the branch risk.

Security, consent, audit, import, upload, dry run, and commit branches should include explicit allowed and denied cases where practical.

## 19. Pull Request Contract

Each implementation pull request should describe the exact branch slice.

The PR body should include:

- scope
- planning source
- behavior changed
- behavior intentionally unchanged
- verification performed
- privacy boundary notes
- audit implications
- deferred work
- rollback notes

Planning-only branches should continue to say planning/docs only.

Implementation branches should state the precise runtime surface they affect.

A PR should not rely on commit title alone to explain security or privacy behavior.

## 20. Deferred Work Register

Deferred work should be recorded explicitly.

Deferred work entries should include:

- source branch
- deferred item
- reason for deferral
- risk level
- owning future group
- blocking dependency
- required planning update when applicable

Deferred work should not become invisible backlog dust.

A later implementation group should review deferred items before declaring its exit criteria complete.

## 21. Stop Conditions

Implementation should pause when a stop condition appears.

Stop conditions include:

- SystemId partitioning cannot be preserved
- authorization shell cannot express the needed decision
- consent state cannot explain allowed or denied behavior
- audit event would require private payload content
- import ledger cannot explain a transition
- upload or dry run state would expose private payload material
- Alpha commit would create records without repair evidence
- tests reveal ambiguous ownership
- schema migration would force broad runtime changes outside the branch scope

A stop condition should create a planning correction branch or a narrower implementation slice.

## 22. First Implementation Starting Point

The first implementation branch after this queue should be:

- feature/pb-next-impl-000-repo-baseline

The first branch should confirm the current repository baseline before changing behavior.

It should collect the build, test, and status facts needed to support later implementation branches.

It should avoid schema changes.

It should avoid import, upload, dry run, and commit behavior changes.

It should produce a clean starting ledger for PB-NEXT implementation work.

## 23. Final Ordered Queue

The final ordered implementation queue is:

1. feature/pb-next-impl-000-repo-baseline
2. feature/pb-next-impl-001-test-baseline
3. feature/pb-next-impl-002-config-boundary
4. feature/pb-next-impl-010-account-system-schema
5. feature/pb-next-impl-011-membership-schema
6. feature/pb-next-impl-012-account-lifecycle-state
7. feature/pb-next-impl-013-system-lifecycle-state
8. feature/pb-next-impl-014-initial-steward-membership
9. feature/pb-next-impl-020-authz-decision-model
10. feature/pb-next-impl-021-authz-reason-codes
11. feature/pb-next-impl-022-authz-authority-reference
12. feature/pb-next-impl-023-authz-obligation-model
13. feature/pb-next-impl-024-authz-test-fixtures
14. feature/pb-next-impl-030-relationship-schema
15. feature/pb-next-impl-031-relationship-lifecycle
16. feature/pb-next-impl-032-relationship-authority-binding
17. feature/pb-next-impl-033-relationship-authz-fixtures
18. feature/pb-next-impl-040-consent-schema
19. feature/pb-next-impl-041-consent-lifecycle
20. feature/pb-next-impl-042-consent-versioning
21. feature/pb-next-impl-043-consent-authz-binding
22. feature/pb-next-impl-044-consent-test-fixtures
23. feature/pb-next-impl-050-audit-schema
24. feature/pb-next-impl-051-audit-event-writer
25. feature/pb-next-impl-052-audit-reason-codes
26. feature/pb-next-impl-053-audit-correlation
27. feature/pb-next-impl-054-audit-test-fixtures
28. feature/pb-next-impl-060-import-job-schema
29. feature/pb-next-impl-061-import-transition-history
30. feature/pb-next-impl-062-import-attempt-model
31. feature/pb-next-impl-063-import-reason-codes
32. feature/pb-next-impl-064-import-ledger-audit-binding
33. feature/pb-next-impl-070-upload-intake-schema
34. feature/pb-next-impl-071-upload-storage-reference
35. feature/pb-next-impl-072-upload-validation-gates
36. feature/pb-next-impl-073-upload-import-job-handoff
37. feature/pb-next-impl-074-upload-cleanup-policy
38. feature/pb-next-impl-080-dryrun-schema
39. feature/pb-next-impl-081-dryrun-validation-results
40. feature/pb-next-impl-082-dryrun-mapping-preview
41. feature/pb-next-impl-083-dryrun-operation-summary
42. feature/pb-next-impl-084-dryrun-commit-eligibility
43. feature/pb-next-impl-090-alpha-commit-schema
44. feature/pb-next-impl-091-alpha-commit-authz
45. feature/pb-next-impl-092-alpha-source-id-mapping
46. feature/pb-next-impl-093-alpha-domain-write-slice
47. feature/pb-next-impl-094-alpha-ledger-audit-binding
48. feature/pb-next-impl-095-alpha-idempotency-and-repair
49. feature/pb-next-impl-100-alpha-gate-review
50. feature/pb-next-impl-101-security-review-notes
51. feature/pb-next-impl-102-privacy-review-notes
52. feature/pb-next-impl-103-release-readiness-checklist

This queue is intentionally ordered from boundary and authority foundation toward committed import behavior.

## 24. Open Questions

Open questions before starting implementation:

- What exact command set defines the repository baseline?
- Which existing tests must pass before PB-NEXT implementation branches begin?
- Which implementation group owns first schema migration mechanics?
- Which branch first introduces database migration files?
- Which branch first introduces runtime behavior changes?
- Which branch first creates user-visible import behavior?
- Which branch first writes audit events from runtime code?
- Which branch first writes committed imported domain data?
- What release marker should identify the start of PB-NEXT implementation?
- What checkpoint should be created after Group 0 completes?

These questions should be resolved before opening the first implementation branch.
