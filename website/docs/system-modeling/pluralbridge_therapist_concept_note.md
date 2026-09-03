# PluralBridge Concept Note for Therapist / Professional Input

## Flexible System Modeling, Focus, and User-Authored Structure


**Created: 2026-07-01 02:10 PM PT**

This note captures a standalone concept for therapist and professional feedback while design is still evolving. It focuses on safety, consent, and continuity implications without introducing clinical gatekeeping into product behavior. It is a working idea note, not a final policy or roadmap.

## Start Here

- This note is for therapist/professional feedback on safety language and harm-reduction framing.
- It explains design intent in plain language, without requiring architecture context first.
- The boundary is explicit: professional input informs design, but does not define user legitimacy.

## Quick Concept Key

- 🤝 **Professional input** = safety and continuity feedback.
- 🧭 **System focus** = active context for work in-app.
- 🪪 **User-defined structure** = self-modeled relationships, not app-imposed hierarchy.
- 🔒 **Consent boundary** = sharing and access require explicit permission.
- 📝 **Working note** = in-progress idea capture, not final policy.

## What This Note Is Not

- Not clinical advice.
- Not a gatekeeping framework.
- Not a requirement that users match a professional model to be valid.


## Purpose

This note is written for therapist / professional review rather than software architecture review.

PluralBridge is an open-source project intended to help people preserve and eventually move their Simply Plural data into a safer replacement path. The immediate software work is technical, but the deeper design question is human:

How can an app support plural Systems without forcing them into a rigid model of what a System is supposed to be?

This note explains a design direction that came from discussion with lived experience and therapist input.



## Core Idea

PluralBridge should not define what a System must be.

PluralBridge should support how Systems define and model themselves.

That means the app should be flexible enough to represent different lived structures, including:

- one System
- more than one System
- child Systems
- related Systems
- sibling Systems
- subsystems
- unclear or evolving relationships
- multiple top-level Systems
- structures that may not fit a strict tree

The software should not force every user into one official hierarchy.



## Why This Matters

Some apps treat System structure as if there is one obvious shape:

one account
one System
members inside that System

That may work for many people, especially at the start, but it may not fit everyone.

Some people may experience their organization as multiple related Systems, nested Systems, parallel Systems, or something that changes over time.

PluralBridge should make room for that without requiring the person to prove that their structure is clinically correct, officially recognized, or acceptable to the app.



## Root Systems

In ordinary software language, a "root" object often means the top of a hierarchy.

That can accidentally imply there should only be one.

For PluralBridge, a root System should simply mean:

A System with no parent relationship.

There may be one root System.
There may be more than one root System.
A root System is not more legitimate than another System.
A root System is not automatically the authority over every other System.

This matters because "root" should not become a hidden rule that forces people into one model.



## System Relationships

PluralBridge should allow Systems to describe relationships between Systems.

For example:

- parent / child
- related
- sibling
- subsystem
- associated
- archived
- unknown
- user-defined labels later

These relationships should be descriptive.

They describe how the System wants to model itself.

They should not automatically decide authority, access, privacy, or legitimacy.



## Access and Privacy

A key design principle is that structure and access should stay separate.

Just because one System is described as a parent of another does not mean the parent automatically gets access to everything in the child System.

Just because two Systems are related does not mean their private data should automatically be shared.

Access should come from explicit permission, membership, consent, or policy.

This keeps the app from making unsafe assumptions.



## Focus

The most useful concept from this discussion is "focus."

Rather than asking which System is the one true root, PluralBridge can ask:

Which System is currently in focus?

A focused System is the active System context.

In practical terms:

- the account may have access to one or more Systems
- only one System context is focused at a time
- the focused System is where current work happens
- changing focus changes the working context
- changing focus does not automatically create access

This is useful because it matches both software design and lived experience.



## Focus and Fronting

In lived terms, focus may map better than hierarchy.

Only one System context may have focus at a time, even if multiple members or children are fronting within that System.

A focused System may have:

- no one fronting
- one fronter
- multiple simultaneous fronters
- custom front state
- activity by members or child structures underneath it

This separates two ideas:

System focus:
Which System context is active.

Fronting:
Who or what is active inside that context.

That distinction may help the app support complex Systems without flattening everything into one global fronting state.



## Simple Default, Flexible Future

PluralBridge does not need to make the first user experience complicated.

The default experience can be simple:

A person signs in.
They have one System.
That System is focused.
They see their members, notes, groups, fronts, and imported data.

But the underlying model should not break if later they need:

- another root-level System
- a child System
- related Systems
- a different focus
- more complex sharing or privacy boundaries

This allows the app to start simple without locking users into an oversimplified model.



## Professional Input

Therapist and professional input can be valuable, especially around:

- safety
- privacy
- consent
- continuity of care
- accessibility
- therapist-facing summaries or exports
- avoiding harm during migration
- preserving meaning when data is imported
- supporting users without imposing clinical authority

However, professional input should not become gatekeeping.

The app should not say:

A System is only valid if it matches a professional model.

Instead, professional input should help the app avoid harm while preserving user self-definition.



## Possible Professional Review Questions

These are the kinds of questions where therapist input could help:

1. Does the concept of "focus" make sense as a way to describe the active System context?

2. Is it useful to separate "System focus" from "fronting"?

3. Are there risks in allowing multiple root-level Systems?

4. Are there risks in forcing only one root-level System?

5. What language would feel least pathologizing to users?

6. What kinds of therapist-facing reports would be helpful if controlled entirely by the user?

7. What should the app avoid assuming about System hierarchy?

8. What privacy mistakes would be especially harmful in this context?

9. How can the app support continuity without encouraging over-disclosure?

10. How can professional perspectives inform safety without overriding lived experience?



## Therapist-Friendly Feature Ideas

These are not immediate commitments, but possible future directions:

- user-controlled sharing
- printable summaries
- therapist-facing exports
- selective disclosure
- consent-bounded sharing
- "show this to my therapist" views
- privacy-aware reports
- clear separation between private notes and shareable summaries
- no automatic clinician access
- no default sharing of private System data

The user should remain in control of what is shared.



## Plain-Language Summary

PluralBridge is trying to preserve more than data.

It is trying to preserve meaning.

A System's structure may be personal, complex, evolving, and not easily captured by one rigid hierarchy.

The app should support that complexity without making the user explain or defend it to the software.

The key concepts are:

- Systems define their own structure.
- More than one root System can exist.
- Relationships describe structure but do not automatically grant authority.
- Focus means the active System context.
- Fronting happens inside that focused context.
- Access, privacy, and consent stay separate from hierarchy.
- Therapist and professional input can improve safety without becoming gatekeeping.



## Short Version

PluralBridge should not tell Systems what shape they are allowed to have.

PluralBridge should give Systems safe, flexible tools to model themselves.

The app should be simple when the user needs simple, and flexible when the user's lived structure is more complex.
