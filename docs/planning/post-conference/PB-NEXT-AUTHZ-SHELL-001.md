# PB-NEXT-AUTHZ-SHELL-001 — Central Authorization Decision Shell Implementation Plan

Status: Planning draft  
Parent: PB-NEXT Implementation Gate Planning  
Depends on: `PB-NEXT-GATE-001.md`, `PB-NEXT-IDENTITY-MEMBERSHIP-001.md`, `PB-NEXT-AUTHZ-001.md`  
Scope: Central authorization decision shell, decision inputs, decision result shape, obligations, reason codes, and implementation boundary before ReBAC, consent, audit, or import enforcement expands.

## 1. Purpose

This document translates the central authorization planning lane into an implementation-ready plan.

The goal is to create the first central authorization decision shell before protected writes, import commits, relationship authority, consent enforcement, or audit obligations expand.

This is still planning. It does not implement app, API, database, import, upload, Azure runtime, website, release, or outreach behavior.

## 2. Working Decision

PluralBridge should have one central authorization decision shape:

`Can subject S perform action A on resource R in context C?`

The first implementation shell should define the shape of that decision even if many inputs are placeholders at first.

The shell should support:

1. subject
2. action
3. resource
4. context
5. allow or deny result
6. reason code
7. authority references
8. obligations

This prevents authorization from spreading into unrelated inline checks.

## 3. Authorization Shell Boundary

The authorization shell is not full ReBAC, full consent, or full audit.

It is the decision boundary where those later inputs will be evaluated.

The first shell should know how to receive:

1. Account status
2. System status
3. System membership
4. requested action
5. target resource
6. request context
7. optional relationship authority placeholder
8. optional consent authority placeholder
9. optional audit obligation placeholder

The first shell may return deny for inputs that are not implemented yet.

## 4. Subject

Subject is the actor requesting the action.

Candidate subject fields:

1. `SubjectType`
2. `AccountId`
3. `ActorKind`
4. `Authenticated`
5. `AccountStatus`
6. `SessionId` or request actor reference
7. `ServiceActorId` placeholder

First-slice subject support should focus on authenticated Account actors.

Service actors, support actors, providers, helpers, and break-glass actors should remain placeholders unless explicitly implemented later.

## 5. Action

Action is the operation being requested.

Candidate first action names:

1. `System.Create`
2. `System.Read`
3. `System.Update`
4. `Membership.Read`
5. `Membership.Create`
6. `Membership.Revoke`
7. `Import.Upload`
8. `Import.ViewStatus`
9. `Import.DryRun`
10. `Import.Commit`

The first implementation slice should not implement all actions.

The shell should define the action naming pattern so later slices do not invent incompatible names.

## 6. Resource

Resource is the protected object or scope being acted on.

Candidate resource fields:

1. `ResourceType`
2. `ResourceId`
3. `SystemId`
4. `ParentResourceType`
5. `ParentResourceId`
6. `PrivacyBucketId` placeholder
7. `ImportBatchId` placeholder

First-slice resources should focus on Account, System, and SystemMembership.

Later slices can extend the resource model to members, groups, notes, privacy buckets, imports, exports, and audit records.

## 7. Context

Context carries request and policy facts that affect the decision.

Candidate context fields:

1. `SystemId`
2. `RequestId`
3. `CorrelationId`
4. `Purpose`
5. `PolicyVersion`
6. `MembershipId`
7. `RelationshipIds`
8. `ConsentId`
9. `ConsentVersion`
10. `AuditRequired`
11. `UtcNow`

Context should not carry private payload.

Context should carry stable IDs and policy references.

## 8. Decision Result

The decision result should be structured.

Candidate result fields:

1. `Allowed`
2. `ReasonCode`
3. `ReasonTextSafe`
4. `SubjectReference`
5. `ResourceReference`
6. `AuthorityReferences`
7. `Obligations`
8. `PolicyVersion`
9. `DecisionAt`

The result should be safe to log at a high level, but it should not include private resource payload.

## 9. Reason Codes

Reason codes should be stable, safe, and useful for tests, audit, and user-facing error mapping.

Candidate reason codes:

1. `Allow`
2. `DenyUnauthenticated`
3. `DenyAccountInactive`
4. `DenySystemInactive`
5. `DenyMembershipMissing`
6. `DenyMembershipInactive`
7. `DenyRoleInsufficient`
8. `DenyRelationshipMissing`
9. `DenyConsentMissing`
10. `DenyConsentExpired`
11. `DenyConsentRevoked`
12. `DenyPolicy`
13. `DenyAuditUnavailable`
14. `DenyUnsupportedAction`
15. `DenyResourceNotFound`

Reason codes should not reveal private payload.

## 10. Authority References

Authority references explain why a decision was allowed or denied.

Candidate authority references:

1. Account status reference
2. System status reference
3. Membership ID
4. Membership role
5. Membership status
6. Relationship ID placeholder
7. Consent ID placeholder
8. Consent version placeholder
9. Policy version
10. Break-glass record placeholder
11. Support access grant placeholder

The first implementation shell should be able to return authority references even if only membership authority is implemented at first.

## 11. Obligations

Obligations are required follow-up actions attached to an authorization decision.

Candidate obligations:

1. write audit event
2. require consent check
3. require relationship check
4. require support-access review
5. require break-glass review
6. suppress private payload in logs
7. emit safe denial reason
8. require import ledger event

The first shell should support an obligations list even if only audit placeholders exist at first.

## 12. First-Slice Evaluation Order

Candidate first-slice evaluation order:

1. Confirm subject is authenticated.
2. Confirm Account is active.
3. Resolve target System when the action is System-scoped.
4. Confirm System is active when required.
5. Resolve membership when the action requires System authority.
6. Confirm membership is active.
7. Confirm role is sufficient for the action.
8. Attach authority references.
9. Attach audit obligations where required.
10. Return allow or deny.

This order keeps identity and membership checks explicit before later relationship and consent checks are added.

## 13. Initial Action Matrix

Candidate first-slice action matrix:

1. `System.Create`
   - Requires active Account.
   - Creates System and initial Steward membership in a later implementation slice.

2. `System.Read`
   - Requires active Account.
   - Requires active membership.

3. `System.Update`
   - Requires active Account.
   - Requires active Steward membership.

4. `Membership.Read`
   - Requires active Account.
   - Requires active membership.

5. `Membership.Create`
   - Requires active Account.
   - Requires active Steward membership.

6. `Membership.Revoke`
   - Requires active Account.
   - Requires active Steward membership.
   - Last-Steward rule remains unresolved unless explicitly implemented.

Import actions should remain placeholders until import ledger planning is ready for implementation.

## 14. Safe Error Mapping

Authorization results should map to safe user-facing messages.

Examples:

1. `DenyUnauthenticated`
   - user-facing message: sign in required

2. `DenyAccountInactive`
   - user-facing message: account is not active

3. `DenyMembershipMissing`
   - user-facing message: access not available

4. `DenyRoleInsufficient`
   - user-facing message: permission not available

5. `DenyConsentRevoked`
   - user-facing message: required consent is no longer active

Safe messages should avoid revealing whether private resources exist when the actor lacks authority.

## 15. Audit Readiness

The authorization shell should prepare audit-compatible decision results.

Audit-ready result fields:

1. subject reference
2. action
3. resource reference
4. System ID
5. decision
6. reason code
7. membership authority reference
8. relationship authority placeholder
9. consent authority placeholder
10. policy version
11. decision timestamp
12. request or correlation ID

The authorization shell should not write audit events directly unless that is explicitly chosen later.

The shell should return enough information for a caller or service layer to write a payload-free audit event.

## 16. Implementation Boundaries

Allowed in the later implementation slice:

1. define authorization request shape
2. define authorization result shape
3. define action names
4. define reason codes
5. define authority reference shape
6. define obligation shape
7. add tests for Account/System/membership authorization decisions

Not allowed in this slice:

1. full ReBAC implementation
2. consent enforcement
3. audit table implementation
4. import commit enforcement
5. support access
6. provider access
7. break-glass access
8. broad app UI changes
9. Azure runtime changes

## 17. Test Expectations

The implementation plan should produce testable authorization behavior.

Candidate tests:

1. unauthenticated subject is denied
2. inactive Account is denied
3. active Account can create a System where allowed
4. active Account without membership cannot read a System
5. active Account with active membership can read a System
6. active Steward can update a System
7. non-Steward cannot perform Steward-only actions
8. inactive membership is denied
9. unsupported action is denied
10. decision result includes reason code
11. decision result includes authority references where applicable
12. decision result includes audit obligation where required

Tests should prove that Account, System, and Membership remain separate.

## 18. Data Dependencies

The authorization shell depends on stable facts from prior planning:

1. Account lifecycle state
2. System lifecycle state
3. System membership state
4. membership role
5. stable Account ID
6. stable System ID
7. stable membership ID
8. action name
9. resource reference
10. request context

The shell should not depend on private payload fields for first-slice decisions.

## 19. Relationship Placeholder

Relationship authority is planned but should not be implemented in this shell unless explicitly reopened.

The shell should reserve space for:

1. relationship IDs
2. relationship type
3. relationship status
4. relationship scope
5. relationship expiration
6. relationship authority references

For first-slice implementation, relationship checks may return unsupported or not-applicable.

## 20. Consent Placeholder

Consent is planned but should not be implemented in this shell unless explicitly reopened.

The shell should reserve space for:

1. consent ID
2. consent version
3. consent status
4. consent purpose
5. consent scope
6. consent artifact version
7. consent policy version

For first-slice implementation, consent checks may return unsupported or not-applicable.

## 21. Import Placeholder

Import authorization should remain a placeholder until import ledger planning reaches implementation.

The shell should reserve action names and resource references for:

1. upload
2. import batch
3. import job ledger
4. validation
5. dry run
6. commit
7. purge
8. status view
9. report download

Import commit must not be implemented before identity, membership, authorization, consent, and audit readiness are in place.

## 22. First Implementation Gate Contribution

PB-NEXT-AUTHZ-SHELL-001 contributes these gate requirements:

1. Authorization has a central decision shape.
2. The shell accepts subject, action, resource, and context.
3. The shell returns allow or deny.
4. The shell returns stable reason codes.
5. The shell returns authority references.
6. The shell returns obligations.
7. Account, System, and Membership checks are explicit.
8. Relationship, consent, audit, and import concerns have reserved extension points.
9. Private payload is not required for first-slice decisions.
10. Authorization behavior is testable.

## 23. Open Questions

1. What is the exact request type name?
2. What is the exact result type name?
3. Which first-slice actions are required immediately?
4. Which reason codes are required immediately?
5. Which obligations are required immediately?
6. Should authorization live in a shared domain service, API service, or separate project?
7. Should denied resource reads reveal not-found or always return access-denied?
8. Which authorization decisions require audit obligations in the first implementation slice?
9. How should policy version be represented before a full policy system exists?
10. How much of the action matrix should be enforced before import resumes?

## 24. Explicit Non-Goals

Out of scope for this document:

1. Writing implementation code
2. Creating database migrations
3. Implementing full ReBAC
4. Implementing consent enforcement
5. Implementing audit tables
6. Implementing import authorization
7. Implementing upload authorization
8. Implementing support access
9. Implementing provider access
10. Implementing break-glass access
11. Changing app UI
12. Changing API behavior
13. Changing Azure runtime behavior
14. Promoting a release

Those actions require later explicit implementation tasks.
