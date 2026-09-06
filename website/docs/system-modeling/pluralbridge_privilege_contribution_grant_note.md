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

One DID therapist we talked to asked how this would work if Member A of System Alpha wanted to add text to Member B's description. In their workflow, this is allowed as `(Member) date-stamp: added text`. The team discussed an ROI-shaped pattern where audience, accessible content, and duration are explicit.

For PluralBridge, this should be modeled as **attributed contribution**, not ordinary editing.

- Member A should not silently overwrite Member B’s canonical description.
- Member A may add a dated, attributed contribution when consent or System policy allows it.

## Example Display

```text
Member B description

B’s own description text here.

Contributions:
(Member A) 2026-07-03: Added context about how B prefers to be addressed during high-stress mornings.
(Member C) 2026-07-05: Added note that B dislikes being described as "protector."
```

## Data Shape: DescriptionContribution

```text
DescriptionContribution
- targetSystemId: System Alpha
- targetMemberId: Member B
- authorMemberId: Member A
- actingAccountId: signed-in account that performed the action
- createdAt
- contributionText
- visibilityScope
- source: manual / import / therapist-facing summary / migration
- status: active / hidden / withdrawn / superseded
```

## Data Shape: ContributionGrant

```text
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
```

## ROI-Shaped Prompt (Design Metaphor)

The ROI analogy is a design metaphor, not a legal claim.

- **Who may act**: Member A
- **What they may access**: Member B’s description
- **What they may do**: append attributed text, not overwrite
- **Who may see it**: selected audience
- **How long permission lasts**: until a date, until revoked, or one session/event
- **What happens later**: grant can expire or be revoked while audit history remains

This aligns with the PluralBridge principle that structure and access stay separate: parent/sibling/related/focused System relationships should not automatically grant visibility or editing power. Access should come from explicit permission, membership, consent, or policy.

## Therapist Question: Concrete Modes

Scenario:

- System Alpha
- Member A wants to add text to Member B’s description

PluralBridge could support several System-defined modes:

1. **Closed**: only B can edit or annotate B’s description.
2. **Append with consent**: A can add attributed notes only if B grants permission.
3. **Append by role**: A can add attributed notes because A has a System role (caretaker, historian, archivist, admin).
4. **Append pending approval**: A writes the note; B or an authorized member approves before it appears.
5. **Private contribution**: A’s note exists, but only A, B, or a defined audience can see it.
6. **Therapist-facing contribution**: A’s note can appear in a user-controlled therapist export, bounded by explicit sharing rules.

## Safety Boundary

PluralBridge should avoid a raw shared edit field.

- Canonical description belongs to the described member or System-defined authority.
- Other members may contribute attributed, dated additions when allowed.
- Every contribution has visibility, consent, and audit metadata.

This preserves meaning, allows collaborative self-description, supports existing System conventions, and avoids silent rewrites by other members.
