# PB-NEXT-RELATIONSHIP-001 — Relationship Authority Model Implementation Plan

Status: Planning draft  
Parent: PB-NEXT Implementation Gate Planning  
Depends on: `PB-NEXT-GATE-001.md`, `PB-NEXT-IDENTITY-MEMBERSHIP-001.md`, `PB-NEXT-AUTHZ-SHELL-001.md`, `PB-NEXT-REB-001.md`  
Scope: Relationship authority model, relationship facts, delegation, helper/provider placeholders, relationship lifecycle, and central authorization integration before consent, audit, or import enforcement expands.

## 1. Purpose

This document translates the relationship-based authorization planning lane into an implementation-ready plan.

The goal is to define relationship authority as a separate layer from Account lifecycle, System membership, consent, audit, and import.

This is still planning. It does not implement app, API, database, import, upload, Azure runtime, website, release, or outreach behavior.

## 2. Working Decision

PluralBridge should model relationship authority separately from membership and separately from consent.

Membership answers:

1. Is this Account connected to this System?
2. What coarse role does this Account have in this System?

Relationship authority answers:

1. Does this subject have a specific relationship to this resource, System, member, group, helper role, provider role, or delegated authority?
2. Is that relationship active?
3. What scope does that relationship cover?
4. What actions may that relationship support?

Consent answers a different question:

1. Is this purpose-bound processing activity authorized?

Relationship records should feed the central authorization decision shell as authority inputs.

Consent should remain a policy input, not a relationship tuple.

## 3. Relationship Layer Boundary

The relationship layer should model authority facts.

It should not become:

1. the Account lifecycle model
2. the System membership model
3. the consent lifecycle model
4. the audit event model
5. the import job ledger
6. a replacement for central authorization

The relationship layer should provide facts that the central authorization shell can evaluate.

## 4. Candidate Relationship Record Shape

Candidate relationship fields:

1. `RelationshipId`
2. `SystemId`
3. `SubjectType`
4. `SubjectId`
5. `RelationType`
6. `ObjectType`
7. `ObjectId`
8. `ScopeType`
9. `ScopeId`
10. `Status`
11. `GrantedByAccountId`
12. `GrantedAt`
13. `ExpiresAt`
14. `RevokedAt`
15. `RevokedByAccountId`
16. `PolicyVersion`
17. `CreatedAt`
18. `UpdatedAt`

Relationship records should reference stable IDs.

Relationship records should not copy private payload from the target resource.

## 5. Subject

The subject is the actor or authority holder.

Candidate subject types:

1. Account
2. SystemMembership
3. Helper
4. Provider
5. SupportActor
6. ServiceActor
7. Group placeholder

The first implementation slice should focus on Account or SystemMembership subjects only.

Helper, provider, support, service, and group subjects should remain placeholders unless explicitly reopened.

## 6. Object

The object is the thing the relationship applies to.

Candidate object types:

1. System
2. Member
3. Group
4. PrivacyBucket
5. Note
6. FrontHistory
7. CurrentFronterState
8. ImportBatch
9. ExportPackage
10. AuditRecord placeholder

The first implementation slice should avoid applying relationship rules to sensitive payload-heavy objects until audit and consent readiness are in place.

## 7. Relation Types

Candidate relation types:

1. Steward
2. CanView
3. CanEdit
4. CanManageMembership
5. CanDelegate
6. HelperFor
7. ProviderFor
8. SupportAccess
9. EmergencyAccess
10. ImportOperator
11. ExportOperator

The first relationship implementation should be narrow.

It may only need placeholders and schema readiness until a concrete relationship use case is selected.

## 8. Scope

Relationship scope limits where the relationship applies.

Candidate scope types:

1. SystemWide
2. ResourceSpecific
3. MemberSpecific
4. GroupSpecific
5. PrivacyBucketSpecific
6. ImportBatchSpecific
7. ExportPackageSpecific
8. TimeBound
9. ActionBound

Scope should be explicit enough for authorization and audit to explain why an action was allowed or denied.

## 9. Relationship Lifecycle

Candidate relationship states:

1. Pending
2. Active
3. Suspended
4. Expired
5. Revoked
6. Superseded

The first implementation slice should support the model shape even if only Active and Revoked are immediately used.

Expired and Superseded should remain available for time-bounded delegation and later policy changes.

## 10. Membership Versus Relationship

Membership is the first authority bridge between Account and System.

Relationship authority is narrower and more specific.

Membership should answer:

1. Is this Account attached to this System?
2. Is the membership active?
3. What coarse role does the membership carry?

Relationship should answer:

1. Does this subject have a specific authority relation?
2. What object does the relation apply to?
3. What scope limits the relation?
4. Is the relation active, expired, revoked, or superseded?

The relationship layer should not replace System membership.

## 11. Relationship Versus Consent

Relationship records and consent records must stay separate.

Relationship records describe authority structure.

Consent records describe purpose-bound permission for processing.

A helper relationship may establish that an Account is a helper.

Consent may still be required before that helper can process certain data for a specific purpose.

The central authorization shell should evaluate both as separate inputs when both are required.

## 12. Authorization Integration

Relationship facts should feed the central authorization shell.

Candidate authorization inputs from relationship records:

1. `RelationshipId`
2. `RelationType`
3. `SubjectType`
4. `SubjectId`
5. `ObjectType`
6. `ObjectId`
7. `ScopeType`
8. `ScopeId`
9. `Status`
10. `ExpiresAt`
11. `PolicyVersion`

Candidate authorization outputs involving relationship authority:

1. allow or deny
2. relationship authority reference
3. reason code
4. policy version
5. audit obligation

The authorization shell should not require relationship payload copies.

## 13. Audit Readiness

Relationship records should be audit-referenceable.

Future audit events should be able to reference:

1. relationship creation
2. relationship activation
3. relationship suspension
4. relationship expiration
5. relationship revocation
6. relationship supersession
7. action allowed through relationship authority
8. action denied due to missing relationship authority
9. action denied due to inactive relationship authority

Audit records should reference relationship IDs and policy versions without copying private resource payload.

## 14. Delegation

Delegation should be relationship-based.

Candidate delegation questions:

1. Who delegated authority?
2. Who received authority?
3. Which System does it apply to?
4. Which resource or scope does it apply to?
5. Which actions are covered?
6. When does it expire?
7. How is it revoked?
8. What audit event records the delegation?

Delegation should not be implemented as a direct owner shortcut.

Delegation should not silently bypass consent or audit requirements.

## 15. Helper And Provider Placeholders

Helper and provider access are future relationship use cases.

Candidate helper/provider relationship facts:

1. helper for System
2. helper for member
3. provider for System
4. provider for member
5. provider access to scoped records
6. time-bounded helper access
7. time-bounded provider access

These should remain placeholders until explicit helper/provider implementation planning occurs.

No helper/provider access should be opened by this document.

## 16. Implementation Boundaries

Allowed in a later implementation slice:

1. define relationship record shape
2. define relationship status values
3. define relationship type values
4. define relationship scope values
5. connect relationship placeholders to the authorization shell
6. add tests proving membership and relationship are separate

Not allowed in this slice:

1. full OpenFGA or SpiceDB integration
2. full provider access
3. full helper access
4. support access
5. break-glass access
6. consent enforcement
7. audit table implementation
8. import commit behavior
9. upload behavior
10. Azure runtime changes

## 17. Support And Break-Glass Placeholders

Support access and break-glass access are future relationship use cases with stricter audit requirements.

Candidate support relationship facts:

1. support actor for System
2. support actor for import batch
3. support actor for account recovery
4. support actor for audit review
5. time-bounded support access
6. purpose-bounded support access

Candidate break-glass relationship facts:

1. emergency actor
2. emergency scope
3. emergency justification record
4. time-bounded emergency access
5. mandatory review obligation
6. mandatory audit obligation

No support or break-glass access should be opened by this document.

## 18. Relationship Type Governance

Relationship types should be controlled and versioned.

New relationship types should require:

1. clear purpose
2. target object type
3. scope rules
4. allowed actions
5. lifecycle states
6. authorization behavior
7. audit behavior
8. consent interaction
9. tests

Relationship type expansion should not be ad hoc.

## 19. Relationship Scope Governance

Relationship scopes should be explicit and testable.

Scope rules should define:

1. which object types are allowed
2. whether SystemId is required
3. whether ResourceId is required
4. whether expiration is required
5. whether consent is also required
6. whether audit is mandatory
7. whether delegation is allowed
8. whether the relationship can be superseded

A broad scope should not be used where a narrow scope is possible.

## 20. First Relationship Implementation Candidate

The first relationship implementation should remain narrow.

Candidate first implementation options:

1. schema only
2. authorization-shell placeholder only
3. relationship status and type enums only
4. tests proving relationship is separate from membership and consent

Do not implement helper, provider, support, or break-glass behavior as the first relationship slice.

The safest first slice is probably schema readiness plus tests, not new runtime access.

## 21. Test Expectations

Candidate tests:

1. membership and relationship are separate records
2. relationship and consent are separate records
3. inactive relationship is not valid authority
4. expired relationship is not valid authority
5. revoked relationship is not valid authority
6. relationship authority can be referenced by authorization result
7. relationship authority can be referenced by future audit event
8. unsupported relationship type is denied or rejected
9. relationship scope is enforced
10. private payload is not copied into relationship authority records

Tests should prove that relationship authority is a central authorization input, not a scattered inline shortcut.

## 22. First Implementation Gate Contribution

PB-NEXT-RELATIONSHIP-001 contributes these gate requirements:

1. Relationship authority is separate from membership.
2. Relationship authority is separate from consent.
3. Relationship facts are scoped to System and resource IDs.
4. Relationship facts have lifecycle state.
5. Relationship facts can expire.
6. Relationship facts can be revoked.
7. Relationship facts can be referenced by authorization results.
8. Relationship facts can be referenced by future audit records.
9. Helper, provider, support, and break-glass access remain closed until explicitly planned.
10. Relationship expansion is governed by type, scope, authorization, consent, audit, and test rules.

## 23. Open Questions

1. What is the smallest relationship table needed for the first implementation slice?
2. Which relationship types are required immediately?
3. Should the first slice create schema only?
4. Should relationship expiration be mandatory for delegated authority?
5. Should helper/provider relationships require consent before use?
6. Should support access use relationship records or a separate grant table?
7. Should break-glass access use relationship records or a separate emergency-access table?
8. Which relationship actions require audit obligations?
9. Which relationship changes require notification?
10. How should relationship supersession be represented?
11. Which relationship scopes are too broad for the first implementation slice?
12. Which relationship facts should be visible to users?

## 24. Explicit Non-Goals

Out of scope for this document:

1. Writing implementation code
2. Creating database migrations
3. Implementing full ReBAC
4. Integrating OpenFGA, SpiceDB, Cedar, or Cerbos
5. Implementing helper access
6. Implementing provider access
7. Implementing support access
8. Implementing break-glass access
9. Implementing consent enforcement
10. Implementing audit tables
11. Implementing import behavior
12. Implementing upload behavior
13. Changing app UI
14. Changing API behavior
15. Changing Azure runtime behavior
16. Promoting a release

Those actions require later explicit implementation tasks.
