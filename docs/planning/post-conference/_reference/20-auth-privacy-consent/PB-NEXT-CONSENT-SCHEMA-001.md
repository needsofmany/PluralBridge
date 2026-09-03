# PB-NEXT-CONSENT-SCHEMA-001 — Consent Record Schema And Lifecycle Implementation Plan

Status: Planning draft  
Parent: PB-NEXT Implementation Gate Planning  
Depends on: `PB-NEXT-GATE-001.md`, `PB-NEXT-IDENTITY-MEMBERSHIP-001.md`, `PB-NEXT-AUTHZ-SHELL-001.md`, `PB-NEXT-RELATIONSHIP-001.md`, `PB-NEXT-CONSENT-001.md`  
Scope: Consent record shape, consent lifecycle, purpose, scope, grantee, revocation, expiration, artifact versioning, authorization integration, and audit readiness before enforcement or import processing expands.

## 1. Purpose

This document translates the consent foundation planning lane into an implementation-ready plan.

The goal is to define consent as a first-class, versioned, purpose-specific, scope-bound record before consent enforcement is added to authorization, import, export, helper access, provider access, support access, or deletion behavior.

This is still planning. It does not implement app, API, database, import, upload, Azure runtime, website, release, or outreach behavior.

## 2. Working Decision

PluralBridge should model consent as explicit records.

Consent should not be a boolean flag.

Consent should not be modeled as ReBAC tuples.

Consent should feed the central authorization shell as a policy input.

Consent records should answer:

1. who or what the consent is about
2. who granted the consent
3. who receives authority from the consent
4. what purpose is authorized
5. what scope is authorized
6. when the consent was granted
7. when the consent expires
8. whether the consent was revoked
9. which artifact or policy version governed the grant

## 3. Consent Layer Boundary

The consent layer should stay separate from:

1. Account lifecycle
2. System membership
3. relationship authority
4. central authorization result shape
5. audit event storage
6. import job ledger
7. diagnostic logging

Consent records provide purpose-bound policy facts.

The authorization shell decides whether a requested action is allowed.

Audit records later preserve payload-free evidence of consent-related authority and actions.

## 4. Candidate Consent Record Shape

Candidate consent fields:

1. `ConsentId`
2. `SystemId`
3. `SubjectType`
4. `SubjectId`
5. `Purpose`
6. `ScopeType`
7. `ScopeId`
8. `GranteeType`
9. `GranteeId`
10. `GrantedByAccountId`
11. `Status`
12. `GrantedAt`
13. `ExpiresAt`
14. `RevokedAt`
15. `RevokedByAccountId`
16. `RevocationReasonCode`
17. `SupersededByConsentId`
18. `ArtifactVersion`
19. `PolicyVersion`
20. `CreatedAt`
21. `UpdatedAt`

Consent records should reference stable IDs.

Consent records should not copy private System payload.

## 5. Consent Subject

The consent subject is the person, System, member, or resource scope affected by the processing.

Candidate subject types:

1. Account
2. System
3. Member
4. Group
5. PrivacyBucket
6. ImportBatch
7. ExportPackage
8. ResourceScope

The first implementation slice should keep subject types narrow.

System-level consent and Account-level consent may be enough for the first schema plan.

Member-specific, privacy-bucket-specific, import-specific, and export-specific consent can remain placeholders until needed.

## 6. Purpose

Purpose defines why processing is authorized.

Candidate purpose values:

1. Import
2. Export
3. MigrationAssistance
4. Reporting
5. HelperAccess
6. ProviderAccess
7. SupportAccess
8. SecurityInvestigation
9. BreakGlassAccess
10. AccountClosure
11. SystemClosure
12. AuditReview

Purpose should be explicit because consent for one purpose does not automatically authorize another purpose.

The first implementation slice should select only purposes needed before import or support behavior resumes.

## 7. Scope

Scope defines the boundary of the grant.

Candidate scope types:

1. SystemWide
2. ResourceSpecific
3. MemberSpecific
4. GroupSpecific
5. PrivacyBucketSpecific
6. ImportBatchSpecific
7. ExportPackageSpecific
8. ActionBound
9. TimeBound

Consent scope should be specific enough for authorization and audit to explain why an action was allowed or denied.

A broad scope should not be used where a narrow scope is possible.

## 8. Grantee

The grantee is the actor or authority holder receiving permission under the consent grant.

Candidate grantee types:

1. Account
2. SystemMembership
3. Relationship
4. Helper
5. Provider
6. SupportActor
7. ServiceActor
8. ImportJob
9. ExportJob

The first implementation slice should keep grantee support narrow.

Helper, provider, support actor, service actor, import job, and export job grantees should remain placeholders unless explicitly reopened.

## 9. Consent Lifecycle

Candidate consent states:

1. Draft
2. Pending
3. Active
4. Expired
5. Revoked
6. Superseded
7. Withdrawn
8. Closed

The first implementation slice should support the model shape even if only Active, Expired, Revoked, and Superseded are immediately used.

Inactive consent states should block future processing where consent is required.

## 10. Grant Rules

Consent grant should create a stable record.

Candidate grant requirements:

1. active granting Account
2. target System
3. consent subject
4. consent grantee
5. explicit purpose
6. explicit scope
7. policy version
8. artifact version where applicable
9. grant timestamp
10. safe audit-ready authority reference

Consent grant should not copy private payload into the consent record.

## 11. Expiration Rules

Consent may be time-bounded.

Expiration should be evaluated by the central authorization shell when consent is required.

Candidate expiration rules:

1. expired consent blocks future processing
2. expired consent remains referenceable by audit
3. expiration does not erase past audit evidence
4. expiration does not rewrite past authorization history
5. renewal creates a new consent record or superseding version

The first implementation slice should decide which consent purposes require expiration.

## 12. Revocation Rules

Revocation should be explicit and audit-ready.

Candidate revocation requirements:

1. consent record being revoked
2. revoking Account
3. revocation timestamp
4. revocation reason code
5. affected purpose
6. affected scope
7. policy version
8. artifact version
9. future-processing block

Revocation blocks future processing where that consent is required.

Revocation does not erase past disclosures or past audit evidence.

## 13. Supersession Rules

Consent renewal or material consent change should avoid silent mutation.

Candidate supersession rules:

1. preserve old consent record
2. create new consent record
3. link old record to new record
4. mark old record Superseded
5. preserve old artifact version
6. preserve old policy version
7. preserve old audit references

Supersession should make it possible to explain which consent version governed a past action.

## 14. Artifact And Policy Versioning

Consent should reference the artifact or policy version under which it was granted.

Candidate artifact references:

1. consent text version
2. purpose description version
3. privacy policy version
4. import disclosure version
5. export disclosure version
6. support access terms version
7. provider access terms version

Consent records should reference artifact versions without copying long consent text into every consent row.

## 15. Authorization Integration

Consent records should feed the central authorization shell.

Candidate authorization inputs from consent:

1. `ConsentId`
2. `Status`
3. `Purpose`
4. `ScopeType`
5. `ScopeId`
6. `GranteeType`
7. `GranteeId`
8. `ExpiresAt`
9. `RevokedAt`
10. `ArtifactVersion`
11. `PolicyVersion`

Candidate authorization outcomes involving consent:

1. allowed under active consent
2. denied because consent is missing
3. denied because consent is expired
4. denied because consent is revoked
5. denied because consent scope is insufficient
6. denied because consent purpose does not match

## 16. Relationship Integration

Consent and relationship authority remain separate.

Relationship records may establish that an Account is a helper, provider, delegate, support actor, or other authority holder.

Consent records establish whether a purpose-bound processing activity is authorized.

The authorization shell should evaluate relationship authority and consent as separate inputs.

Consent should not be encoded as relationship tuples.

## 17. Audit Readiness

Consent records should be audit-referenceable.

Future audit events should be able to reference:

1. consent granted
2. consent expired
3. consent revoked
4. consent superseded
5. action allowed under consent
6. action denied due to missing consent
7. action denied due to expired consent
8. action denied due to revoked consent
9. action denied due to insufficient consent scope
10. action denied due to purpose mismatch

Audit records should reference consent IDs, artifact versions, and policy versions without copying private payload or consent text.

## 18. Import And Export Readiness

Import and export are processing purposes.

Import consent may need to authorize:

1. upload processing
2. source record parsing
3. validation
4. dry run
5. commit
6. source ID map retention
7. import status retention
8. completion notification
9. audit evidence retention

Export consent may need to authorize:

1. export generation
2. export package retention
3. export delivery
4. export report generation
5. disclosure evidence retention

Import and export consent should remain purpose-specific and scope-specific.

## 19. Account Closure And System Closure Readiness

Consent lifecycle must be compatible with Account closure and System closure.

Open closure questions:

1. Which active consents end automatically on Account closure?
2. Which active consents end automatically on System closure?
3. Which consent records remain as historical authority references?
4. Which consent records remain audit-referenceable after erasure?
5. Which consent records can be pseudonymized?
6. Which consent records require payload erasure but metadata retention?
7. Which consent records block future processing after closure?

Closure should not destroy the ability to explain past authorization and disclosure events.

## 20. Data Minimization

Consent records should contain only authority metadata.

Consent records should avoid:

1. raw import payload
2. member descriptions
3. notes
4. private front-history content
5. avatar content
6. raw export payload
7. diagnostic details
8. plaintext validation tokens
9. unnecessary consent text copies

Long text artifacts should be versioned separately where practical.

Consent rows should reference artifact versions rather than duplicating full artifact content.

## 21. First Consent Implementation Candidate

The first consent implementation should remain narrow.

Candidate first implementation options:

1. schema only
2. enum/value-object planning only
3. authorization-shell placeholder integration only
4. import consent placeholder only
5. tests proving consent is separate from relationship authority

Do not implement helper, provider, support, break-glass, export disclosure, or deletion consent as the first consent slice unless explicitly reopened.

The safest first slice is probably schema readiness plus tests.

## 22. Test Expectations

Candidate tests:

1. consent is not a boolean
2. consent and relationship authority are separate records
3. active consent can be referenced by authorization result
4. expired consent blocks future processing where required
5. revoked consent blocks future processing where required
6. superseded consent remains audit-referenceable
7. consent purpose must match requested processing purpose
8. consent scope must cover requested resource or action
9. consent artifact version is preserved
10. private payload is not copied into consent records

Tests should prove that consent is a purpose-bound policy input, not a relationship tuple or inline flag.

## 23. First Implementation Gate Contribution

PB-NEXT-CONSENT-SCHEMA-001 contributes these gate requirements:

1. Consent is first-class.
2. Consent is versioned or artifact-versioned.
3. Consent is purpose-specific.
4. Consent is scope-bound.
5. Consent can expire.
6. Consent can be revoked.
7. Consent can be superseded without rewriting history.
8. Consent feeds the authorization shell as a policy input.
9. Consent is separate from relationship authority.
10. Consent records are audit-referenceable.
11. Consent records avoid private payload.
12. Revocation blocks future processing without erasing past evidence.

## 24. Open Questions

1. What is the smallest consent table needed for the first implementation slice?
2. Which consent purposes are required immediately?
3. Which consent scopes are required immediately?
4. Should System-level import consent be implemented before upload resumes?
5. Should expiration be mandatory for helper, provider, support, or delegated access?
6. Should revocation require reason codes immediately?
7. Which consent events require audit obligations?
8. Which consent records survive Account closure?
9. Which consent records survive System closure?
10. Which consent artifact versions must exist before enforcement?
11. Should consent artifacts live in code, database, or documentation first?
12. Which consent decisions need user-visible status messages?

## 25. Explicit Non-Goals

Out of scope for this document:

1. Writing implementation code
2. Creating database migrations
3. Implementing consent enforcement
4. Implementing consent UI
5. Implementing full ReBAC
6. Implementing audit tables
7. Implementing import behavior
8. Implementing export behavior
9. Implementing helper access
10. Implementing provider access
11. Implementing support access
12. Implementing break-glass access
13. Changing app UI
14. Changing API behavior
15. Changing Azure runtime behavior
16. Promoting a release

Those actions require later explicit implementation tasks.
