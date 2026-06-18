# PB-NEXT-REB-001 — Relationship-Based Authorization Investigation

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`  
Scope: Relationship model, ownership, sharing, delegation, stewardship, helper/provider/support/tester access, and future ReBAC compatibility before implementation.
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the fourth PB-NEXT foundation lane: how PluralBridge should model relationship facts before selecting a final ReBAC implementation strategy.

The goal is to define the relationship layer that feeds the central authorization decision point.

ReBAC supplies relationship facts. The central authorization decision point combines those facts with roles, consent, purpose-of-use, sensitivity, lifecycle state, policy, and audit obligations.

## 2. Working Decision

PluralBridge should treat ReBAC as the relationship layer.

Relationship records should model:

1. Ownership
2. Stewardship
3. Membership
4. Sharing
5. Delegation
6. Helper access
7. Provider access
8. Support access
9. Tester access
10. Resource-specific access paths

Consent state and purpose-of-use remain policy inputs. They should be evaluated by the central authorization decision point rather than encoded as relationship tuples.

The near-term deployment remains monolithic. Schema seams and authorization seams should be created now. Service extraction is deferred until operational pressure proves a split is required.

## 3. Relationship Layer Responsibilities

The relationship layer should answer relationship questions.

Candidate relationship questions:

1. Is Account A a Steward of System S?
2. Is Account A a Member of System S?
3. Is Account A a Helper for System S?
4. Is Account A delegated to manage Resource R?
5. Is Account A allowed through a provider relationship?
6. Is Account A granted support access for System S?
7. Is Account A part of a tester or early stewardship relationship?
8. Is Resource R contained by System S?
9. Is Resource R shared with Account A or a group containing Account A?
10. Is Relationship X active, expired, revoked, or superseded?

The relationship layer should return facts and paths. It should not independently decide every access question.

## 4. Central Authorization Boundary

The central authorization question remains:

`Can subject S perform action A on resource R in context C?`

The relationship layer feeds that decision.

Authorization also evaluates:

1. Account lifecycle state
2. System lifecycle state
3. Membership lifecycle state
4. Consent state
5. Purpose-of-use
6. Resource sensitivity
7. Privacy bucket
8. Break-glass state
9. Support access scope
10. Policy version
11. Audit obligations

This keeps relationship modeling useful without turning every policy input into a tuple.

## 5. Candidate Relationship Types

### 5.1 Account to System

Candidate relationships:

1. `account steward_of system`
2. `account member_of system`
3. `account helper_for system`
4. `account provider_for system`
5. `account support_for system`
6. `account tester_for system`

These relationships map to the Account / System membership model from PB-NEXT-USER-001.

### 5.2 System to Resource

Candidate relationships:

1. `system contains member`
2. `system contains group`
3. `system contains privacy_bucket`
4. `system contains note`
5. `system contains current_fronter_state`
6. `system contains front_history_record`
7. `system contains custom_front`
8. `system contains avatar`
9. `system contains custom_field`
10. `system contains import_batch`
11. `system contains source_record`
12. `system contains audit_event`

These relationships support containment checks and stable audit references.

### 5.3 Account to Resource

Candidate relationships:

1. `account owns resource`
2. `account can_view resource`
3. `account can_manage resource`
4. `account delegated_to resource`
5. `account support_access_to resource`

Direct Account-to-resource relationships should be used sparingly. System-scoped membership or delegation relationships should remain the ordinary path.

### 5.4 Group and Nested Relationship Paths

Future relationship paths may include:

1. Account belongs to access group.
2. Access group has permission over resource.
3. Account inherits access through group membership.
4. Account inherits stewardship through System relationship.
5. Helper inherits limited authority through delegation.

Nested paths should remain explainable for audit and support.

## 6. Relationship Record Shape

A relationship record should be stable and audit-referenceable.

Candidate fields:

1. `RelationshipId`
2. `RelationshipType`
3. `SubjectType`
4. `SubjectId`
5. `ObjectType`
6. `ObjectId`
7. `SystemId`
8. `Status`
9. `CreatedAt`
10. `CreatedByAccountId`
11. `ActivatedAt`
12. `ExpiresAt`
13. `RevokedAt`
14. `RevokedByAccountId`
15. `SupersededByRelationshipId`
16. `PolicyVersion`
17. `Notes` or non-sensitive reason metadata where appropriate

Relationship records should avoid protected payload.

## 7. Relationship Lifecycle

Candidate relationship states:

1. Proposed
2. Active
3. Suspended
4. Expired
5. Revoked
6. Superseded

Inactive relationship states should block future authorization paths while preserving audit history.

Relationship lifecycle transitions should be audit candidates.

## 8. Relationship Versioning

Authorization and audit need stable authority references.

Relationship changes should preserve enough information to explain past authorization decisions.

Candidate versioning approaches:

1. Append-only relationship history
2. Superseding relationship records
3. Version column with immutable historical snapshots
4. Status transitions with audit records

The implementation decision can be deferred, but the schema pass must preserve stable identity and historical explainability.

## 9. Ownership, Stewardship, and Delegation

Ownership should be represented as relationships rather than a single owner foreign key.

Stewardship should support multiple Stewards per System.

Delegation should support limited, scoped access.

Delegation fields may require:

1. Delegate subject
2. Delegating subject
3. Target System or resource
4. Action scope
5. Duration
6. Revocation state
7. Audit obligation
8. Consent or purpose link when required by later planning docs

## 10. Helper, Provider, Support, and Tester Access

### 10.1 Helper Access

Helper access should be explicit, scoped, revocable, and auditable.

Helper relationships may support migration help, day-to-day System management help, or care-related support.

### 10.2 Provider Access

Provider access should be explicit, purpose-bound, scoped, revocable, and auditable.

Provider access may also require stronger consent handling in PB-NEXT-CONSENT-001.

### 10.3 Support Access

Support access should be exceptional, time-bounded where possible, and heavily audited.

Support relationships should avoid becoming informal administrative backdoors.

### 10.4 Tester Access

Tester access should be represented explicitly so early QA and stewardship work does not create hidden production exceptions.

Tester relationships may expire or convert to ordinary membership/stewardship later.

## 11. Candidate Technology Investigation

### 11.1 Zanzibar-Style Modeling

Zanzibar-style modeling is useful as the conceptual baseline for object relationships, usersets, and relationship paths.

Investigation questions:

1. Which PluralBridge relationships map cleanly to tuples?
2. Which relationship paths require nesting?
3. Which paths need explainability for audit?
4. Which relationships require time-bounded lifecycle fields outside a pure tuple model?

### 11.2 OpenFGA

OpenFGA should be evaluated as a possible future relationship engine.

Investigation questions:

1. Can the first relationship model be represented cleanly?
2. Can relationship paths be explained sufficiently for audit?
3. How much operational overhead appears in a monolithic early deployment?
4. What migration path exists from database relationship records to OpenFGA tuples?

### 11.3 SpiceDB

SpiceDB should be evaluated as a possible future relationship engine.

Investigation questions:

1. Does it fit PluralBridge relationship depth and explainability needs?
2. How would it operate beside the application database?
3. What does local development and testing look like?
4. What operational complexity would it add before scale demands it?

### 11.4 Cedar and Cerbos

Cedar and Cerbos belong in the policy-engine investigation more than the relationship-store investigation.

They may help with authorization policy expression, attributes, purpose-of-use, and decision evaluation.

They should be compared during PB-NEXT-AUTHZ follow-up or later implementation planning, with clear separation from the relationship layer.

## 12. Monolith-First Deployment Rule

The first implementation should keep the deployment monolithic.

Relationship seams should appear in:

1. Schema
2. Application services
3. Central authorization inputs
4. Audit authority references
5. Tests

A separate ReBAC service should wait until operational pressure, scale, or complexity justifies the split.

This keeps the July 1 pressure aligned with architectural discipline.

## 13. First Implementation Gate Contribution

PB-NEXT-REB-001 contributes these gate requirements:

1. ReBAC is the relationship layer.
2. Consent and purpose-of-use remain policy inputs.
3. Relationship records have stable audit-facing identity.
4. Ownership, stewardship, delegation, helper/provider/support/tester access are modeled as relationships.
5. Single-owner foreign keys are avoided for protected System authority.
6. Relationship lifecycle state is available to authorization.
7. Relationship facts feed the central authorization decision point.
8. Monolithic deployment remains the starting shape.
9. Future OpenFGA/SpiceDB-style migration remains possible without forcing service extraction now.

## 14. Open Design Questions

1. What is the smallest relationship table needed for the first implementation slice?
2. Which relationship types are required for DB Alpha read behavior?
3. Which relationship types are required before add/edit/delete work begins?
4. Which relationships require expiration?
5. Which relationships require explicit revocation?
6. Which relationship transitions require audit?
7. Which relationship paths must be explainable to users or support?
8. Which relationship facts should be cached?
9. Which relationship facts should stay in the relational database?
10. Which future relationship facts map cleanly to OpenFGA or SpiceDB?
11. Which policy facts should be kept out of the relationship layer?
12. Which relationship records need stable audit-facing IDs?
13. How should relationship records interact with System deletion or account closure?
14. How should relationship records interact with consent revocation?

## 15. Explicit Non-Goals

Out of scope for this document:

1. Implementing a ReBAC engine
2. Selecting OpenFGA, SpiceDB, Cedar, or Cerbos
3. Implementing relationship tables
4. Implementing authorization checks
5. Implementing consent schema
6. Implementing audit schema
7. Implementing account or membership schema
8. Implementing import/export behavior
9. Implementing upload behavior
10. Azure runtime changes
11. Website changes
12. Release promotion

Those decisions belong to later PB-NEXT planning documents and implementation slices.
