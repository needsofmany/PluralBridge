# PB-NEXT-USER-001 — Account / System Membership Model

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`  
Scope: Account identity, System membership, stewardship, helper/support/tester access, and relationship seams before implementation.
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the second PB-NEXT foundation decision: how PluralBridge separates authenticated accounts from Systems and how access to Systems begins before central authorization, ReBAC, consent, audit, and import processing are finalized.

The decision must support the corrected architecture model:

1. Account authenticates a person or user.
2. System is the protected domain container.
3. Account and System remain separate concepts.
4. Account-to-System access is represented through membership or relationship records.
5. Resource ownership, authorization, consent, audit, and processing purpose remain separate design concerns.
6. Deployment remains monolithic while schema seams are established.

## 2. Working Decision

PluralBridge should model Account and System as separate first-class records.

Use Account records for authentication and account lifecycle.

Use System records as protected data containers.

Use Account-to-System membership records to connect accounts to Systems.

Use explicit relationship records or relationship-ready membership records for stewardship, helper access, provider access, tester access, support access, sharing, delegation, and future ReBAC integration.

Avoid single-owner foreign keys as the primary access model for protected System data.

## 3. Core Entities

### 3.1 Account

An Account represents an authenticated principal.

Minimum Account responsibilities:

1. Stable account identity
2. Sign-in identity linkage
3. Account lifecycle state
4. Contact/security metadata where required
5. Relationship to System membership records
6. Relationship to audit actor references

Account records should avoid storing protected System payload.

### 3.2 System

A System represents the primary protected domain container.

A System contains or scopes:

1. Members
2. Groups
3. Privacy buckets
4. Notes
5. Current fronter state
6. Front history
7. Custom fronts
8. Avatars
9. Custom fields
10. Import/source records
11. Audit records for System-scoped resources

System records should not depend on one Account foreign key for ownership.

### 3.3 System Membership

A System membership connects an Account to a System.

Minimum membership responsibilities:

1. `AccountId`
2. `SystemId`
3. Membership status
4. Membership role or coarse permission label
5. Created/updated metadata
6. Optional invitation or acceptance metadata
7. Stable membership identity for later audit references

Membership creates the starting authority context. It does not replace central authorization.

### 3.4 Relationship Records

Relationship records represent ownership, stewardship, delegation, helper access, provider access, support access, tester access, and sharing.

Relationship records should be compatible with a later ReBAC model.

ReBAC is the relationship layer. Consent state, purpose-of-use, sensitivity, break-glass, and processing purpose remain policy inputs to the central authorization decision.

## 4. Minimal Membership Roles

The first schema pass should support coarse human-readable membership roles while preserving a path to relationship-based authorization.

Candidate roles:

1. Steward
2. Member
3. Helper
4. Provider
5. Support
6. Tester

These roles are planning labels. PB-NEXT-AUTHZ-001 will define the central authorization decision shape and first action set.

### 4.1 Steward

A Steward can administer a System within the limits of policy, consent, and audit rules.

A System may have multiple Stewards.

Stewardship should be represented through membership or relationship records rather than a single `OwnerAccountId`.

### 4.2 Member

A Member is associated with a System for ordinary use.

A Member role may represent a person/user account attached to a System, depending on the final identity and consent model.

The model must leave room for Systems where the account holder, data subject, System participant, and accessor are distinct.

### 4.3 Helper

A Helper has delegated access to assist with System use, migration, administration, or care-related tasks.

Helper access should be limited by scope, purpose, duration, and audit obligations.

### 4.4 Provider

A Provider represents professional or care-related access.

Provider access should be explicit, scoped, revocable, purpose-bound, and auditable.

### 4.5 Support

Support access represents operational or project support access.

Support access should be exceptional, scoped, time-bounded where possible, and heavily audited.

Support access must not become informal backdoor access to protected System payload.

### 4.6 Tester

Tester access represents early stewardship, QA, migration validation, or foundation testing.

Tester access should be modeled explicitly so early implementation does not smuggle special cases into production authorization logic.

## 5. Membership Lifecycle

Membership should support explicit lifecycle states.

Candidate states:

1. Invited
2. Active
3. Suspended
4. Revoked
5. Expired
6. Closed

Lifecycle state should feed the central authorization decision.

Revoked, expired, suspended, or closed memberships should block future access while preserving audit history.

## 6. Account Lifecycle

Account lifecycle is separate from System lifecycle.

Candidate account states:

1. Active
2. Locked
3. Suspended
4. Closed
5. Deleted or deletion-pending

Account closure does not automatically define System deletion.

Account deletion, System transfer, last-Steward handling, audit retention, consent revocation, and payload erasure require explicit design in later planning documents.

## 7. System Lifecycle

System lifecycle should be explicit.

Candidate System states:

1. Active
2. Archived
3. Transfer-pending
4. Closure-pending
5. Closed
6. Deleted or deletion-pending

System closure and System deletion require rules for:

1. Protected payload retention or erasure
2. Audit retention
3. Import/source record treatment
4. Consent and revocation state
5. Last-Steward or no-Steward handling
6. Export before closure
7. Support escalation boundaries

## 8. Data Subject / Controller / Accessor Separation

The model must leave room for these concepts:

1. Data subject
2. Controller or steward
3. Accessor
4. Processor or support actor
5. Import initiator
6. Export recipient

These concepts should not be collapsed into Account or System.

A single Account may act under different relationships depending on resource, purpose, and context.

A System may contain data about members or events where the authenticated Account is not the sole data subject.

## 9. NT-Style Mental Model

PluralBridge can use an NT-style conceptual frame without cloning NT implementation details.

Planning analogies:

1. Account resembles a security principal.
2. Stable Account identity resembles a SID-like durable principal identifier.
3. System resembles a protected container or domain boundary.
4. Membership and relationship records resemble group membership, trustees, and delegation.
5. Central authorization resembles an access check against subject, action, resource, and context.
6. Audit records resemble SACL-driven security evidence.
7. Resource records need stable identity so audit can reference them without copying payload.

This analogy is useful because it keeps identity, object identity, access decisions, and audit separate.

## 10. First Implementation Gate Contribution

PB-NEXT-USER-001 contributes these gate requirements:

1. Account and System are separate first-class concepts.
2. Protected System data is not directly owned by a single Account foreign key.
3. Account-to-System access uses membership or relationship records.
4. Multiple Stewards for one System must be possible.
5. One Account belonging to multiple Systems must be possible.
6. Helper, provider, support, and tester access must have explicit modeling seams.
7. Membership lifecycle state must feed authorization.
8. Account lifecycle and System lifecycle must remain separate.
9. Data subject / controller / accessor distinctions must remain available to later consent, audit, and import design.

## 11. Open Design Questions

1. What is the smallest Account table needed for the first implementation slice?
2. What is the smallest System membership table needed for the first implementation slice?
3. Which membership roles are required immediately?
4. Should helper/provider/support/tester access use one relationship table or separate typed tables?
5. How should invitation and acceptance be represented?
6. What prevents a System from losing its last Steward?
7. How should System transfer work?
8. How should Account closure behave when the Account is the only Steward of one or more Systems?
9. Which membership lifecycle transitions require audit?
10. Which relationship changes require consent review?
11. Which roles are coarse labels and which relationships become ReBAC tuples later?
12. Which identifiers need stable audit-facing IDs?
13. Which relationship records require expiration or renewal?
14. Which access types require break-glass support?

## 12. Explicit Non-Goals

This document does not implement:

1. Account schema changes
2. Membership schema changes
3. Authentication changes
4. Authorization checks
5. ReBAC engine selection
6. Consent schema
7. Audit schema
8. Import pipeline changes
9. Upload behavior
10. Azure runtime behavior
11. Website changes
12. Release promotion

Those decisions belong to later PB-NEXT planning documents and implementation slices.
