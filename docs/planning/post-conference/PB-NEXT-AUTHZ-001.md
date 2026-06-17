# PB-NEXT-AUTHZ-001 — Central Authorization Decision Point

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`  
Scope: Central authorization decision shape, policy inputs, action/resource framing, obligations, and audit authority references before implementation.
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the third PB-NEXT foundation decision: how PluralBridge asks and answers authorization questions in one central place before add/edit/delete/import/export behavior is implemented.

The decision must support the corrected architecture model:

1. System is the protected data container and primary isolation boundary.
2. Account is the authenticated principal.
3. Account-to-System access flows through membership or relationship records.
4. ReBAC is the relationship layer.
5. Consent state and purpose-of-use are policy inputs.
6. Audit records need authority references without copying protected payload.
7. Deployment remains monolithic while schema and policy seams are established.

## 2. Working Decision

PluralBridge should use a central authorization decision point.

The core authorization question is:

`Can subject S perform action A on resource R in context C?`

The central decision point should return:

1. Decision: allow or deny
2. Reason code
3. Obligations
4. Authority references
5. Policy version
6. Relationship or membership references where applicable
7. Consent reference where applicable
8. Audit classification or audit requirement

Authorization logic should be centralized before protected add/edit/delete/import/export behavior grows across controllers, endpoints, import jobs, or UI actions.

## 3. Decision Inputs

### 3.1 Subject

Subject is the actor requesting an action.

Candidate subject types:

1. Account
2. Service actor
3. Import job actor
4. Support actor
5. Future delegated actor

Subject context may include:

1. `AccountId`
2. stable actor identifier
3. authenticated state
4. account lifecycle state
5. System memberships
6. relationship records
7. coarse roles
8. break-glass state where applicable

### 3.2 Action

Action is the requested operation.

Initial action categories:

1. Read
2. Create
3. Update
4. Delete
5. Import
6. Export
7. Share
8. Delegate
9. Revoke
10. Audit-read
11. Support-access
12. Break-glass-access

Specific actions should be named explicitly in later implementation planning.

### 3.3 Resource

Resource is the protected object or container being acted upon.

Candidate resource categories:

1. System
2. Member
3. Group
4. Privacy bucket
5. Note
6. Current fronter state
7. Front history record
8. Custom front
9. Avatar
10. Custom field
11. Import batch
12. Source record
13. Source ID mapping
14. Audit event
15. Consent record
16. Membership record
17. Relationship record

Resources need stable identifiers so authorization decisions and audit records can reference them without copying payload.

### 3.4 Context

Context supplies policy inputs beyond subject, action, and resource.

Candidate context fields:

1. Target `SystemId`
2. request source
3. processing purpose
4. consent state
5. consent version
6. relationship version
7. membership lifecycle state
8. resource sensitivity
9. privacy bucket
10. time
11. support access scope
12. import/export job scope
13. break-glass justification where applicable

## 4. Decision Output

Authorization output should be structured.

Candidate output shape:

1. `Decision`
2. `ReasonCode`
3. `SubjectId`
4. `Action`
5. `ResourceType`
6. `ResourceId`
7. `SystemId`
8. `PolicyVersion`
9. `MembershipId`
10. `RelationshipIds`
11. `ConsentId`
12. `ConsentVersion`
13. `Obligations`
14. `AuditRequired`
15. `AuditClassification`

### 4.1 Allow / Deny

The decision point returns allow or deny.

Deny should return a reason code suitable for logging and debugging without exposing protected payload.

### 4.2 Obligations

Obligations are required follow-up behaviors attached to an authorization decision.

Candidate obligations:

1. write audit event
2. suppress sensitive payload from response
3. require export receipt
4. require consent renewal
5. require support access justification
6. require break-glass audit marker
7. require import job ledger update
8. require notification eligibility check

### 4.3 Authority References

Authority references explain why an action was allowed.

Candidate authority references:

1. membership ID
2. relationship ID
3. role or coarse permission label
4. consent record ID
5. consent artifact version
6. policy version
7. support access grant ID
8. break-glass record ID
9. import/export processing-purpose record

Audit records should reference these authority artifacts rather than copying sensitive payload.

## 5. Relationship to ReBAC

ReBAC supplies relationship facts. It does not replace the full authorization decision.

Relationship facts may answer questions such as:

1. Is this Account a Steward of this System?
2. Is this Account a Helper for this System?
3. Is this Account delegated to manage this resource?
4. Is this Account allowed to view this member, group, note, or privacy bucket through a relationship path?
5. Is this support actor granted temporary access?

Consent state, purpose-of-use, sensitivity, lifecycle state, break-glass state, and processing purpose remain policy inputs to the central decision.

This keeps ReBAC in the relationship layer and avoids encoding time-bounded consent as relationship tuples.

## 6. Relationship to Roles

Roles provide coarse human-readable permission labels.

Candidate roles from PB-NEXT-USER-001:

1. Steward
2. Member
3. Helper
4. Provider
5. Support
6. Tester

Roles can simplify policy expression, UI explanation, and initial implementation.

Roles should feed the central decision point rather than becoming scattered endpoint checks.

## 7. Relationship to Consent

Consent is a policy input.

Consent should be evaluated by the central decision point when an action requires a purpose-bound grant.

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

Consent evaluation should consider:

1. subject
2. purpose
3. scope
4. grantee
5. granted time
6. expiration
7. revocation
8. artifact version

Revoked, expired, missing, or out-of-scope consent should block future processing where consent is required.

## 8. Relationship to Audit

Authorization decisions should produce audit-compatible authority data.

Audit records should capture:

1. who acted
2. what action was requested
3. which resource was affected
4. which System scoped the resource
5. whether the action was allowed or denied when required
6. which membership, relationship, consent, policy, or break-glass authority applied
7. when the decision occurred

Audit records should remain payload-free.

Diagnostic logs and audit/evidence records remain separate.

## 9. First Protected Action Set

The first implementation slice should define a small action set.

Candidate first action set:

1. `System.Read`
2. `Member.Read`
3. `Group.Read`
4. `PrivacyBucket.Read`
5. `FrontHistory.Read`
6. `CurrentFronter.Read`
7. `ImportBatch.Read`
8. `SourceRecord.Read`
9. `AuditEvent.ReadOwnSystem`
10. `Membership.Read`

Add/edit/delete actions should wait until the central decision shape, relationship inputs, consent inputs, and audit obligations are settled enough to avoid scattered security logic.

## 10. Enforcement Expectations

Implementation should enforce authorization at explicit boundaries.

Candidate enforcement points:

1. API endpoint boundary
2. application service boundary
3. repository/query boundary for System isolation
4. import job boundary
5. export job boundary
6. support access boundary
7. audit read boundary

The preferred model is:

1. authenticate subject
2. resolve Account and lifecycle state
3. resolve System context
4. resolve membership and relationships
5. resolve consent and purpose inputs when required
6. call central authorization decision point
7. apply obligations
8. perform action
9. write audit where required

## 11. NT-Style Mental Model

PluralBridge can continue using an NT-style conceptual frame.

Planning analogies:

1. Account resembles a security principal.
2. Stable Account identity resembles a SID-like principal identifier.
3. System resembles a protected container/domain boundary.
4. Resource resembles an object with stable identity.
5. Membership and relationships resemble groups, trustees, and delegation.
6. Authorization resembles an access check against subject, action, resource, and context.
7. Obligations resemble access-check side effects such as audit requirements.
8. Audit authority references resemble SACL/security-event evidence.

This analogy supports the design goal: identity, object identity, access checks, policy inputs, and audit stay separate.

## 12. First Implementation Gate Contribution

PB-NEXT-AUTHZ-001 contributes these gate requirements:

1. A central authorization question exists.
2. Subject, action, resource, and context are defined.
3. Authorization returns allow/deny plus reason and obligations.
4. Authorization returns authority references for audit.
5. Roles feed the decision point rather than replacing it.
6. Relationships feed the decision point rather than replacing it.
7. Consent and purpose-of-use remain policy inputs.
8. Audit obligations are part of the decision output.
9. Protected add/edit/delete/import/export work waits for this decision shape.

## 13. Open Design Questions

1. What is the smallest central authorization interface needed for the first implementation slice?
2. Which action names are required for DB Alpha read behavior?
3. Which action names are required before add/edit/delete begins?
4. Which resources require resource-specific actions?
5. Which resources can share generic read/update/delete actions?
6. What obligations are needed in the first implementation slice?
7. Which denied decisions require audit?
8. Which allowed reads require audit?
9. Which support actions require break-glass modeling?
10. Which policy inputs must be available at the decision boundary?
11. How should authorization failures be represented to the UI without leaking sensitive facts?
12. How should tests prove cross-System access denial?
13. Which authority references must be stable audit-facing IDs?

## 14. Explicit Non-Goals

Out of scope for this document:

1. Implementing authorization code
2. Selecting a ReBAC engine
3. Implementing OpenFGA, SpiceDB, Cedar, or Cerbos
4. Implementing consent schema
5. Implementing audit schema
6. Implementing account or membership schema
7. Implementing API endpoint checks
8. Implementing import/export behavior
9. Implementing upload behavior
10. Azure runtime changes
11. Website changes
12. Release promotion

Those decisions belong to later PB-NEXT planning documents and implementation slices.
