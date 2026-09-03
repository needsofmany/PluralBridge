# PluralBridge Spine Design Note

## Focused System Context and Flexible System Modeling


**Created: 2026-07-01 02:03 PM PT**

This note captures one design idea in progress: how focus, membership, and System relationships should be separated in the spine. It records the boundary that relationship shape is descriptive and does not grant authority by itself. It stands on its own as a working concept note, not a final roadmap decision.

## Start Here

- This note is for engineering/design decisions around the Account/System/Membership spine.
- Read this as a working model for safe defaults and future flexibility, not as final policy text.
- Keep one core rule in mind: structure describes relationships; access comes from explicit authority.

## Quick Concept Key

- 🧭 **Focus** = active System context for current operations.
- 🔐 **Membership** = access grant to a System boundary.
- 🧱 **Relationship** = descriptive structure only (not automatic authority).
- 📝 **Working note** = idea capture in progress, not a final roadmap contract.

## What This Note Is Not

- Not a release checklist.
- Not a complete API spec.
- Not a requirement that every System follow one hierarchy.


## Purpose

This note captures an important spine-level design decision for PluralBridge:

PluralBridge should support flexible, user-authored System modeling rather than enforcing a single official hierarchy.

This came out of discussion around whether there can be more than one root System. Input from a DID therapist reinforced that priority should be given to how a System, or sets of Systems, want to model themselves rather than forcing an external or clinical rule into the software.

This is the right place to nail the decision down because we are currently working on the Account / System / Membership spine. Once the spine hardens, assumptions about what a System is will become expensive to reverse.



## Core Design Decision

PluralBridge supports user-authored System modeling.

A PluralBridge account may have access to one or more Systems. A System may exist without a parent System. More than one root-level System may exist for the same account or account group.

System relationships are descriptive structure. They are not proof of legitimacy, clinical category, hierarchy, or authority.

PluralBridge should support the way Systems model themselves, including:

- independent Systems
- related Systems
- child Systems
- sibling Systems
- subsystems
- multiple root-level Systems
- blurred or evolving boundaries
- other relationship shapes that may emerge later

Authorization, privacy, consent, and audit must remain separate from relationship shape.

A parent/child or related-System link does not automatically grant access unless an explicit membership, consent, or policy rule grants it.



## Important Invariant

SystemId is the isolation boundary.
Membership grants access.
Relationships describe structure.
Relationships do not automatically create authority.



## Current Technical Assessment

As currently designed, there is no technical constraint blocking this direction.

The spine already separates the important concepts:

- Account = who can sign in
- System = security / data boundary
- Membership = who has access to a System
- System relationships = descriptive structure
- Privacy / consent = separate authority layer

That means allowing multiple root Systems does not require undoing the spine.

The work is mostly to preserve the rule that parent/child structure must not become responsible for access.



## Avoid These Assumptions

Do not assume:

- one account = one root System
- one System must have a parent
- all Systems roll up to one canonical System
- parent System access implies child System access
- child System access implies parent System access
- a root System means "the one true top-level System"

The risk is not the current DB shape. The risk is accidentally baking a single-root assumption into:

- /api/me currentSystem resolution
- import preview
- member query filters
- UI navigation
- tests
- future relationship constraints



## Root, Parent, and Focus

The word "root" should not mean "there can be only one."

Root System:
A System with no parent relationship. There may be zero, one, or many root Systems visible to an account.

Parent System:
A System used as a modeling/navigation relationship, not an automatic authority source.

Focused System:
The System currently selected for the user's session, navigation, and default API operations.

Changing focus changes the working context.
Changing focus does not create access.

Membership decides what Systems an account may access.
Focus decides which System the account is currently working in.
Relationships decide how Systems are organized or displayed.



## Default Case

The default first-release case stays simple:

Account signs in.
Account has one membership.
One System exists.
Focus is that System.
Everything behaves like a normal single-System app.

But the model does not break when the account can access multiple Systems:

Account
  -> Membership in System A
  -> Membership in System B
  -> Membership in System C

Focus: System B

Or when Systems are related:

System A
  -> Child System A1
  -> Child System A2

Focus: System A1



## Focus and Fronting

The idea of focus maps to lived use.

Only one System context has focus at a time.

That focused System may have:

- zero fronters
- one fronter
- multiple simultaneous fronters
- custom front state
- child/member activity underneath it

Multiple children or members may front at the same time, but the active working context is still one focused System.

System focus = which System context is active.
Front state = who or what is fronting inside that focused System.
Membership = whether the account may access that System.
Relationships = how Systems are organized.

Spine rule:

A session has one focused System.
A focused System may have multiple active fronters.
Changing focus changes the System context.
Changing focus does not create access.
Fronting state belongs to a System, not directly to the account.



## Windows / Window Manager Analogy

The Windows/window-manager comparison is useful because it separates root objects, containment/relationship, and active focus.

Desktop / window manager
  = PluralBridge account/session context

Top-level windows
  = root Systems

Child windows / owned windows
  = child Systems, subsystems, related Systems, nested structures

Active window
  = focused System

Window ownership / parentage
  = descriptive System relationship

Input focus / permissions
  = membership + authorization, not hierarchy

A window can be top-level without being invalid.
Multiple top-level windows can exist at the same time.
A child relationship describes structure.
It does not mean every action automatically flows through the parent.

PluralBridge equivalent:

A System can be root-level without being invalid.
Multiple root Systems can exist for the same account.
A System relationship describes how the user models the Systems.
It does not automatically grant access.



## WPF Analogy

PluralBridge is conceptually similar to a WPF-style context system for plural data.

WPF Application / runtime
  -> PluralBridge app/runtime

Dispatcher
  -> FocusedSystemContext

SynchronizationContext
  -> resolved account + membership + focused System boundary

Window
  -> System

Child window / owned window
  -> child System / related System

Visual tree
  -> UI presentation of Systems, members, groups, fronts, notes

Logical tree
  -> user-authored System/member/group relationship model

DataContext
  -> current focused System + selected member/import/fronting context

Binding
  -> API calls scoped through FocusedSystemContext

Command routing
  -> member/front/import operations routed through membership + SystemId

Thread affinity
  -> System affinity

WPF rule:
Do not touch UI objects from the wrong Dispatcher.

PluralBridge rule:
Do not touch System data from the wrong FocusedSystemContext.



## FocusedSystemContext

PluralBridge "focus" is effectively a human/System-scoped execution context.

FocusedSystemContext is the active System context that app operations are routed through.

All protected member, front, note, group, import-preview, privacy, and audit operations should execute inside the focused System context unless explicitly scoped otherwise.

Software version:
Do not mutate UI state from the wrong thread.
Marshal work back to the correct Dispatcher.

PluralBridge version:
Do not read/write System data outside the focused System context.
Resolve the account's authorized System focus first, then operate inside that boundary.



## Context Stack

Every protected operation should resolve:

AccountContext
  -> who is signed in

MembershipContext
  -> what Systems this account can access

FocusedSystemContext
  -> which System is currently active

OperationContext
  -> what is being read, written, imported, or audited inside that focused System

Invariant:

No System operation runs without a resolved FocusedSystemContext.



## Effect on /api/me

/api/me is not merely returning "the user and a system."

It is resolving the current execution context for the app.

It should make clear:

- the signed-in account
- the Systems the account may access
- the current focused System
- the membership basis for that focus
- possibly enough metadata for the UI to let the user change focus later

currentSystem should be understood internally as the Focused System.



## Effect on Import

Import preview should execute inside a FocusedSystemContext.

The import path should not assume:

- all imported data belongs to one global account-level root
- there is only one root System
- a parent System owns all child System data by default
- relationship shape is the authority model

Import should preserve source meaning before converting it.

If Simply Plural or another source app implies relationship, grouping, privacy bucket, member, custom front, front history, or note structure, PluralBridge should preserve that meaning as faithfully as possible and map it into the focused System context.



## Effect on UI

The user's System model is not required to match the UI tree.

The UI may present Systems as:

- a tree
- a list
- grouped roots
- focused views
- pinned Systems
- archived Systems
- flattened views
- filtered views

But UI presentation must not rewrite the underlying relationship model or authorization model.

"Switch focus" is a better internal phrase than "switch root."

"Switch system" may still be fine in user-facing UI language, but internally "focus" is better because it does not imply there is only one valid top and does not imply that moving around the model changes authorization.



## Professional / Clinical / Research Input

This design direction also suggests a future discussion-board section for professionals.

Possible section name:

Professional / Clinical / Research Input

Purpose:

A place for therapists, clinicians, researchers, peer-support professionals, accessibility reviewers, and privacy/safety people to help shape PluralBridge as a replacement path for Simply Plural data.

Boundary:

Professionals should inform the design without becoming gatekeepers over plural experience.

Possible ground rules:

- No patient/client details.
- No clinical advice to individual users.
- No diagnosing or fakeclaiming.
- No "official rules" about what a System is allowed to be.
- Lived experience remains first-class.
- Professional input is welcome where it improves safety, privacy, accessibility, continuity, and data modeling.
- PluralBridge is community-led, with professional perspective invited as one input stream.

Possible board sections:

- Professional / Clinical Input
- System Modeling and Relationships
- Privacy, Consent, and Safety
- Import / Migration Needs
- Accessibility and Usability
- Therapist-Friendly Reports and Exports

Potential future features:

- user-controlled sharing
- printable/exportable summaries
- therapist-facing reports
- selective disclosure
- consent-bounded sharing
- "show this to my therapist" views
- no automatic clinician access
- no default sharing of private System data



## Backlog / Design Task

Task:
Confirm flexible System relationship model during spine work.

Goal:
Ensure Account, System, Membership, privacy, authorization, and import design do not assume a single root System.

Decision:
PluralBridge supports multiple root Systems and user-authored System relationship modeling.

Rule:
System relationships are descriptive. Access is governed by membership, consent, and policy.

Reason:
Systems may model themselves in different ways. PluralBridge should preserve and support that modeling rather than forcing all users into a single hierarchy.



## Short Form Design Principle

PluralBridge supports flexible System modeling from the start.

Focus is the active System context.
Fronting is activity within that context.

Membership decides access.
Focus decides working context.
Relationships decide organization.

No System operation runs outside a resolved FocusedSystemContext.
