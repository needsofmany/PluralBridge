# PluralBridge Privilege Contribution Grant Note

## Attributed Member Contributions Using an ROI-Shaped Permission Model

This note captures one standalone design idea for member-to-member description contributions without silent overwrites. It proposes attributed append behavior plus explicit grant metadata for audience, scope, and duration. It is a working concept note to express and test permission behavior, not a final specification.

## Start Here

- This note explores one permission pattern: attributed contribution instead of direct overwrite.
- Use it when designing collaborative member-description workflows with auditability and consent controls.
- The guiding rule is simple: append with attribution under explicit grant; never silently rewrite someone else's canonical text.

## Quick Concept Key

- ✍️ **DescriptionContribution** = attributed text addition with metadata.
- 🧾 **ContributionGrant** = explicit permission envelope (who, what, audience, duration).
- 👁️ **Audience scope** = visibility boundary for each contribution.
- ⏱️ **Grant lifecycle** = starts, expires, or is revoked, while audit history remains.
- 📝 **Working note** = design concept in progress, not final implementation spec.

## What This Note Is Not

- Not a legal ROI document.
- Not a blanket edit permission model.
- Not a replacement for account/system authorization checks.

One DID therapist we talked to asked about how it would work if member A of System Alpha wanted to add text to member B's description. In our System, we allow that in the form of (Member) date-stamp: added text. We were thinking about using something similar to medical ROIs... you specify the audience, what can be accessed and the time for which this request lives

For PluralBridge, we want to model this as attributed contribution, not ordinary editing.
The difference matters. Member A should not silently overwrite Member B’s description. Member A may be allowed to add a dated, attributed contribution to B’s description when consent or System policy allows it.

Example display:
Member B description

B’s own description text here.

Contributions:
(Member A) 2026-07-03: Added context about how B prefers to be addressed during high-stress mornings.
(Member C) 2026-07-05: Added note that B dislikes being described as “protector.”

The core object is something like:

DescriptionContribution
- targetSystemId: System Alpha
- targetMemberId: Member B
- authorMemberId: Member A
- actingAccountId: the signed-in account that performed the action
- createdAt
- contributionText
- visibilityScope
- source: manual / import / therapist-facing summary / migration
- status: active / hidden / withdrawn / superseded

Then the permission model sits beside it:

ContributionGrant
- grantor: Member B, System Alpha policy, or authorized System role
- grantee: Member A
- target: Member B description
- allowedAction: append_contribution
- audience: private, System members, therapist-facing export, selected members, etc.
- startsAt
- expiresAt
- revokedAt
- purpose: optional plain-language reason

The ROI analogy is the right shape, used as a design metaphor rather than a legal claim. The user specifies:
Who may act:
Member A

What they may access:
Member B’s description

What they may do:
Append attributed text, not overwrite

Who may see it:
Selected audience

How long the permission lasts:
Until a date, until revoked, or for one session/event

What happens later:
The grant can expire or be revoked, while the historical audit remains
This lines up neatly with the existing PluralBridge principle that structure and access stay separate: a parent, sibling, related, or focused System relationship should not automatically grant visibility or editing power. Access should come from explicit permission, membership, consent, or policy.

To answer the DID therapist’s concrete question:

System Alpha
Member A wants to add text to Member B’s description.

PluralBridge could support several System-defined modes:

Mode 1: Closed
Only B can edit or annotate B’s description.

Mode 2: Append with consent
A can add attributed notes only if B grants permission.

Mode 3: Append by role
A can add attributed notes because A has a System role, such as caretaker, historian, archivist, or admin.

Mode 4: Append pending approval
A writes the note, but B or an authorized member approves it before it appears.

Mode 5: Private contribution
A’s note exists, but only A, B, or a defined audience can see it.

Mode 6: Therapist-facing contribution
A’s note can appear in a user-controlled therapist export, bounded by explicit sharing rules.

We want to avoid making this a raw shared edit field.

A safer rule is:
- Canonical description belongs to the described member or System-defined authority.
- Other members may contribute attributed, dated additions when allowed.
- Every contribution has visibility, consent, and audit metadata.

That gives PluralBridge a clean safety boundary. It preserves meaning, allows collaborative self-description, supports a System’s existing convention, and avoids the dangerous software behavior where one member’s description can be rewritten by another without trace.
