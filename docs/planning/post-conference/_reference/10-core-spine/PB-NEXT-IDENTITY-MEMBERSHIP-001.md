# PB-NEXT-IDENTITY-MEMBERSHIP-001 — Identity And Membership Implementation Plan

Status: Planning draft  
Parent: PB-NEXT Implementation Gate Planning  
Depends on: `PB-NEXT-GATE-001.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`  
Scope: Account lifecycle, System lifecycle, initial membership model, and implementation boundaries before central authorization, consent, audit, or import work begins.

## 1. Purpose

This document starts the PB-NEXT Implementation Gate Planning phase.

It translates the first two gate slices from `PB-NEXT-GATE-001.md` into an implementation-ready plan:

1. Account lifecycle foundation
2. System and membership foundation

This document does not implement app, API, database, import, upload, Azure runtime, website, release, or outreach behavior.

## 2. Working Decision

PluralBridge must keep Account and System separate.

An Account authenticates a person or user.

A System is the protected domain container.

Membership connects an Account to a System.

Authorization later evaluates Account state, System state, membership state, relationship state, consent, policy, and audit obligations.

This document defines the first schema and task boundary needed before implementation resumes.

## 3. Core Model Boundary

The implementation model should preserve these boundaries:

1. Account is the authenticated principal.
2. System is the protected domain container.
3. Membership connects Account to System.
4. Role is a coarse membership label.
5. Relationship is a later authority layer.
6. Consent is a later purpose-bound policy input.
7. Audit is a later payload-free evidence layer.
8. Import is a later System-centered processing pipeline.

This step should not collapse Account ownership into System ownership.

## 4. Account Lifecycle

Candidate Account lifecycle states:

1. PendingEmailValidation
2. Active
3. Disabled
4. Recovery
5. ClosureRequested
6. Closed

The first implementation slice should support at minimum:

1. PendingEmailValidation
2. Active
3. Disabled
4. Closed

Recovery and closure detail may remain placeholders if the schema preserves room for them.

## 5. Account Lifecycle Fields

Candidate Account fields:

1. `AccountId`
2. `Email`
3. `EmailNormalized`
4. `DisplayName`
5. `AccountStatus`
6. `EmailVerifiedAt`
7. `DisabledAt`
8. `ClosureRequestedAt`
9. `ClosedAt`
10. `CreatedAt`
11. `UpdatedAt`

Sensitive authentication provider details should remain separate from domain records where practical.

## 6. Email Validation

Email validation should be time-bounded.

Candidate validation fields:

1. `AccountEmailValidationId`
2. `AccountId`
3. `Email`
4. `TokenHash`
5. `CreatedAt`
6. `ExpiresAt`
7. `UsedAt`
8. `RevokedAt`

Validation tokens should not be logged in plaintext.

Validation should not automatically create a System.

## 7. System Lifecycle

Candidate System lifecycle states:

1. Active
2. Disabled
3. ClosureRequested
4. Closed
5. Purged

The first implementation slice should support at minimum:

1. Active
2. Closed placeholder

Closure and purge details may remain placeholders if the model preserves room for later deletion and audit decisions.

## 8. System Fields

Candidate System fields:

1. `SystemId`
2. `SystemName`
3. `SystemStatus`
4. `CreatedAt`
5. `UpdatedAt`
6. `ClosureRequestedAt`
7. `ClosedAt`
8. `PurgedAt`

System records should not contain Account authentication state.

System records should not be treated as owned by a single Account field.

## 9. Membership Model

Membership connects an Account to a System.

Candidate membership fields:

1. `SystemMembershipId`
2. `SystemId`
3. `AccountId`
4. `MembershipRole`
5. `MembershipStatus`
6. `InvitedByAccountId`
7. `AcceptedAt`
8. `RevokedAt`
9. `CreatedAt`
10. `UpdatedAt`

Membership records become authorization inputs.

Membership records also become future audit authority references.

## 10. Membership Roles

Candidate first-slice roles:

1. Steward
2. Member
3. Viewer

The first implementation slice may only need Steward.

Additional roles should not be added until there is a concrete authorization need.

## 11. Membership Status

Candidate membership states:

1. Pending
2. Active
3. Suspended
4. Revoked
5. Expired

The first implementation slice should support at minimum:

1. Active
2. Revoked placeholder

Pending invitations may be deferred if no invite workflow is being implemented yet.

## 12. Initial System Creation Rule

When a new System is created, the creating Account should receive an initial Active Steward membership.

This should be represented through membership, not a shortcut owner field.

Candidate creation sequence:

1. Account is Active.
2. System is created.
3. SystemMembership is created.
4. Account receives Steward role through membership.
5. Later audit records can reference the membership authority.

## 13. Last Steward Question

The model must preserve the open question of last-Steward handling.

Open cases:

1. Can the last Steward leave?
2. Can the last Steward be revoked?
3. Can a System exist with no active Steward?
4. Can closure proceed if the last Steward requests it?
5. Who can recover a System after last-Steward loss?

No shortcut should be added that makes this impossible to resolve later.

## 14. Authorization Readiness

This plan prepares for the central authorization shell.

Authorization should later evaluate:

1. Account status
2. System status
3. Membership status
4. Membership role
5. Requested action
6. Target resource
7. Context

This document should not scatter authorization checks across app code.

## 15. Audit Readiness

Account, System, and Membership records should expose stable IDs for future audit references.

Future audit events should be able to reference:

1. Account lifecycle changes
2. System lifecycle changes
3. Membership creation
4. Membership activation
5. Membership revocation
6. Initial Steward assignment

No audit payload design is implemented here.

## 16. Implementation Boundaries

Allowed in the later implementation slice:

1. Add or refine Account lifecycle schema.
2. Add or refine System lifecycle schema.
3. Add or refine System membership schema.
4. Add seed or bootstrap logic for initial Steward membership.
5. Add tests around Account/System separation.

Not allowed in this slice:

1. Import writes
2. Upload behavior
3. Consent enforcement
4. Full ReBAC
5. Full audit schema
6. Provider access
7. Helper access
8. Support access
9. Break-glass access
10. Website or demo changes

## 17. Candidate Task Sequence

Recommended implementation task order after this plan is accepted:

1. Inspect existing Account and System schema.
2. Identify current shortcuts or conflations.
3. Draft schema delta for Account lifecycle.
4. Draft schema delta for System lifecycle.
5. Draft schema delta for System membership.
6. Add or update tests for Account/System separation.
7. Add initial Steward membership creation path.
8. Verify no import, upload, runtime, or UI behavior changes.
9. Document remaining open questions.

## 18. Exit Criteria

This plan is ready for implementation when:

1. Account lifecycle states are accepted.
2. System lifecycle states are accepted.
3. Membership fields are accepted.
4. Initial Steward rule is accepted.
5. Last-Steward question remains explicitly tracked.
6. Authorization readiness is preserved.
7. Audit readiness is preserved.
8. No implementation behavior has changed.

## 19. Open Questions

1. Which Account states are required in the first implementation slice?
2. Which System states are required in the first implementation slice?
3. Is Steward the only first-slice membership role?
4. Do we need Pending membership before invitations exist?
5. Should closed Accounts remain referenceable by audit through stable IDs?
6. Should closed Systems remain referenceable by audit through stable IDs?
7. What is the first safe behavior for last-Steward loss?
8. Does initial System creation belong in API, service layer, or database transaction boundary?
9. Which tests prove Account and System remain separate?
10. Which existing code paths already assume a single owner?

## 20. Explicit Non-Goals

Out of scope for this document:

1. Writing implementation code
2. Creating migrations
3. Changing API behavior
4. Changing app UI
5. Changing import behavior
6. Changing upload behavior
7. Changing Azure runtime behavior
8. Implementing central authorization
9. Implementing ReBAC
10. Implementing consent
11. Implementing audit
12. Promoting a release

Those actions require later explicit implementation tasks.
