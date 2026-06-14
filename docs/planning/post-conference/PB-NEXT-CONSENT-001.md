# PB-NEXT-CONSENT-001 — Versioned Consent and Revocation Records

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`, `PB-NEXT-REB-001.md`  
Scope: Consent records, revocation, processing purpose, policy inputs, authority references, and audit compatibility before implementation.  
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the fifth PB-NEXT foundation lane: how PluralBridge should model consent, revocation, purpose-of-use, and processing authority before import/export, support access, provider access, sharing, deletion, or audit behavior is implemented.

Consent must be first-class, versioned, purpose-specific, and time-bounded where appropriate.

Consent feeds the central authorization decision point as a policy input. Consent should not be encoded as ReBAC tuples.

## 2. Working Decision

PluralBridge should model consent as explicit records with stable identity and lifecycle state.

Consent records should answer:

1. Who or what is the subject of the consent?
2. What purpose is authorized?
3. What scope is authorized?
4. Who is the grantee?
5. When was consent granted?
6. When does consent expire?
7. Has consent been revoked?
8. Which artifact or policy version governed the grant?

Consent is evaluated by the central authorization decision point.

Relationship records may identify who has a relationship to a System or resource. Consent records identify whether a purpose-bound processing activity is authorized.

## 3. Consent Is A Policy Input

The central authorization question remains:

`Can subject S perform action A on resource R in context C?`

Consent belongs in context `C`.

Candidate consent-sensitive actions:

1. Import
2. Export
3. Reporting
4. Provider access
5. Helper access
6. Support access
7. Sharing
8. Delegation
9. Break-glass access
10. Account closure
11. System closure
12. Data deletion or erasure request handling

A relationship can establish that an actor is connected to a System. Consent can establish whether a particular purpose is authorized.

## 4. Candidate Consent Record Shape

Candidate fields:

1. `ConsentId`
2. `SystemId`
3. `SubjectType`
4. `SubjectId`
5. `Purpose`
6. `Scope`
7. `GranteeType`
8. `GranteeId`
9. `GrantedByAccountId`
10. `GrantedAt`
11. `ExpiresAt`
12. `RevokedAt`
13. `RevokedByAccountId`
14. `RevocationReasonCode`
15. `ArtifactVersion`
16. `PolicyVersion`
17. `Status`
18. `SupersededByConsentId`
19. `CreatedAt`
20. `UpdatedAt`

Consent records should avoid protected payload. Descriptive text should be non-sensitive or stored in a separate policy/artifact record where appropriate.

## 5. Consent Subject

Consent subject is the person, System, member, or resource scope affected by the processing.

Candidate subject types:

1. Account
2. System
3. Member
4. Group
5. Resource
6. Import batch
7. Export package
8. Data subject placeholder for later refinement

The model must preserve the separation between:

1. Account
2. System
3. Data subject
4. Controller or steward
5. Accessor
6. Processor or support actor

## 6. Purpose

Purpose identifies why processing is allowed.

Candidate purposes:

1. Import
2. Export
3. Migration assistance
4. Reporting
5. Provider access
6. Helper access
7. Support access
8. Security investigation
9. Break-glass access
10. Account closure
11. System closure
12. Audit review

Purpose should be explicit because authorization for one purpose does not automatically authorize another purpose.

## 7. Scope

Scope defines the boundary of the grant.

Candidate scope dimensions:

1. System-wide
2. Resource-specific
3. Member-specific
4. Group-specific
5. Privacy-bucket-specific
6. Import-batch-specific
7. Export-package-specific
8. Time-bounded
9. Action-bounded
10. Purpose-bounded

Scope should be specific enough for authorization and audit to explain why an action was allowed.

## 8. Grantee

Grantee is the actor or relationship receiving authority under the consent grant.

Candidate grantee types:

1. Account
2. System membership
3. Relationship
4. Helper
5. Provider
6. Support actor
7. Service actor
8. Import job actor
9. Export job actor

Grantee should reference stable IDs rather than names or copied payload.

## 9. Consent Lifecycle

Candidate consent states:

1. Draft
2. Active
3. Expired
4. Revoked
5. Superseded
6. Withdrawn
7. Closed

Inactive consent states should block future processing where consent is required.

Revocation stops future processing. It does not erase past disclosures. Past disclosures remain audit evidence.

## 10. Revocation Rules

Revocation should be explicit and audit-compatible.

Revocation records or fields should capture:

1. Consent being revoked
2. Revoking actor
3. Revocation timestamp
4. Revocation reason code
5. Effective time
6. Affected purpose
7. Affected scope
8. Policy or artifact version
9. Audit reference

Revocation should trigger future authorization denial where the revoked consent is required.

Revocation should not rewrite prior audit history.

## 11. Expiration And Renewal

Consent may be time-bounded.

Expiration should be evaluated by the central authorization decision point.

Renewal should create a new consent version or superseding record rather than silently extending an old grant.

Candidate renewal rules:

1. Preserve prior consent identity for historical audit.
2. Create a new active consent grant.
3. Link old and new records through `SupersededByConsentId` or equivalent.
4. Preserve artifact version and policy version.
5. Audit the renewal event.

## 12. Consent Artifact Versioning

Consent grants should reference the artifact or policy text under which consent was granted.

Candidate artifact records:

1. Consent text version
2. Purpose description version
3. Privacy policy version
4. Support access terms version
5. Provider access terms version
6. Import/export disclosure version

Audit records should reference artifact versions where consent was part of the authority for an action.

## 13. Relationship To Authorization

PB-NEXT-AUTHZ-001 defines the central authorization decision point.

Consent should feed that decision as context.

Candidate decision inputs from consent:

1. `ConsentId`
2. consent status
3. purpose
4. scope
5. grantee
6. expiration
7. revocation state
8. artifact version
9. policy version

Candidate authorization outputs involving consent:

1. allow or deny
2. reason code
3. consent authority reference
4. consent version
5. required obligations
6. audit requirement

## 14. Relationship To ReBAC

PB-NEXT-REB-001 defines ReBAC as the relationship layer.

Relationship facts can say that an Account is a Steward, Helper, Provider, Support actor, or delegated accessor.

Consent facts say whether the requested purpose is authorized.

This division keeps relationship modeling stable and keeps time-bounded consent lifecycle rules in consent records.

## 15. Relationship To Audit

Consent records should be audit-referenceable.

Audit events should record:

1. action
2. resource
3. System
4. actor
5. consent authority reference when applicable
6. consent version
7. policy version
8. artifact version
9. decision outcome where required

Audit records should remain payload-free and should not copy consent text, private member data, raw import payload, or sensitive resource payload.

## 16. Relationship To Import And Export

Import and export should be modeled as processing purposes.

Import consent should answer:

1. Who initiated the import?
2. Which System receives the data?
3. Which source records are processed?
4. Which purpose authorizes processing?
5. Which consent or authority record applies?
6. Which audit event proves the action without copying payload?

Export consent should answer:

1. Who requested the export?
2. Which System or resources are exported?
3. Which recipient or destination receives the export?
4. Which purpose authorizes disclosure?
5. Which audit event proves the export without copying payload?

## 17. Account Closure And System Closure

Account closure and System closure require consent-aware rules.

Open questions for closure:

1. Which consents end automatically?
2. Which consents remain as historical evidence?
3. Which future processing is blocked?
4. Which audit records remain?
5. Which payload records are erased, retained, exported, or crypto-shredded?
6. What happens when the closing Account is the last Steward?

These decisions must align with PB-NEXT-AUDIT-001 and later deletion/account-closure planning.

## 18. First Implementation Gate Contribution

PB-NEXT-CONSENT-001 contributes these gate requirements:

1. Consent is a first-class record.
2. Consent is purpose-specific.
3. Consent is versioned or artifact-versioned.
4. Consent can expire.
5. Consent can be revoked.
6. Revocation blocks future processing where consent is required.
7. Revocation preserves past audit evidence.
8. Consent feeds the central authorization decision as a policy input.
9. Consent is not modeled as ReBAC tuples.
10. Consent authority references are available to audit.

## 19. Open Design Questions

1. What is the smallest consent table needed for the first implementation slice?
2. Which first-slice actions require consent?
3. Which consent purposes are required before import/export work resumes?
4. Which consent purposes are required before helper/provider/support access?
5. Which consent grants require expiration?
6. Which consent grants require renewal?
7. Which consent grants require explicit artifact version references?
8. Which revocation transitions require audit?
9. Which denied actions require audit after consent revocation?
10. How should consent be represented when the Account, data subject, steward, and accessor differ?
11. How should consent interact with privacy buckets?
12. How should consent interact with System deletion?
13. How should consent interact with account closure?
14. Which consent records need stable audit-facing IDs?

## 20. Explicit Non-Goals

Out of scope for this document:

1. Implementing consent tables
2. Implementing consent UI
3. Implementing authorization checks
4. Implementing ReBAC
5. Implementing audit schema
6. Implementing import/export behavior
7. Implementing upload behavior
8. Implementing account closure
9. Implementing System deletion
10. Azure runtime changes
11. Website changes
12. Release promotion

Those decisions belong to later PB-NEXT planning documents and implementation slices.
