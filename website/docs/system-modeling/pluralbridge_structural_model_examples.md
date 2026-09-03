# PluralBridge Structural Model Examples

This note captures standalone example structures that help express and test design ideas in progress. It is meant to pressure-test assumptions about focus, relationships, and access without forcing one hierarchy. It stands on its own as examples, not as a final model contract.

## Start Here

- This note is an example catalog you can use while designing APIs, DB constraints, and UI context flows.
- Each model is intentionally descriptive, not prescriptive.
- The check to apply in every example: does structure stay separate from access authority?

## How To Read The Examples

- Read each section as "one valid shape that the model should tolerate."
- Assume examples are independent; no single example is the official topology.
- Test focus switching, permission checks, and query scoping against several shapes, not just the simple case.

## Quick Concept Key

- 🌲 **Tree model** = parent/child-style structure.
- 🕸️ **Graph model** = cross-linked relationships that are not a single hierarchy.
- 🧭 **Focus** = active System context.
- 🔐 **Access** = explicit membership/consent/policy, never inferred from relationship shape.

## What This Note Is Not

- Not a canonical production data model.
- Not a list of required user configurations.
- Not a final ruleset for relationship labels.


## Purpose

These examples show the kinds of user-authored structural models PluralBridge should be able to support.

The central idea is that PluralBridge should not force every plural System into one official hierarchy. It should allow people and Systems to describe their own structure safely, while keeping access, privacy, consent, and authority separate from hierarchy.



## 1. Simple Single-System Model

A person signs in and has one System.

Account
└── System: The Harbor
    ├── Member: A
    ├── Member: B
    ├── Member: C
    ├── Groups
    ├── Notes
    └── Front history

This is the default model: one account, one System, members inside it, and one focused System context.



## 2. Multiple Top-Level Systems Model

A person has more than one root-level System. Neither root owns the other.

Account
├── System: The Harbor
│   ├── Members
│   └── Front history
│
└── System: The Observatory
    ├── Members
    └── Notes

This supports a user who experiences two distinct top-level structures. The app treats both as root Systems because neither has a parent relationship.



## 3. Parent / Child System Model

One System contains or relates to a smaller child System.

Account
└── System: Main House
    ├── Member: Rowan
    ├── Member: Tess
    │
    └── Child System: The Workshop
        ├── Member: Kit
        └── Member: Lark

The parent / child relationship describes structure. Access and privacy remain governed by membership, consent, and policy.



## 4. Sibling Systems Model

Two Systems share a common relationship, but neither controls the other.

Account
├── System: Day Team
└── System: Night Team

This is useful for Systems that see themselves as parallel, cooperative, or operationally separate.



## 5. Related Systems Model

Two Systems are associated without a parent / child shape.

Account
├── System: The Harbor
│
└── System: The Archive
    relationship to The Harbor: "related"

This could represent shared history, partial overlap, a past structure, or a loosely connected internal organization.



## 6. Subsystem Model

A member, group, or internal cluster may have its own structure.

System: The Harbor
├── Member: Alex
├── Member: Morgan
│   └── Subsystem: Morgan's Room
│       ├── Member: M1
│       └── Member: M2
└── Member: Rae

This is one of the models where a strict flat member list starts to run out of descriptive power.



## 7. Evolving / Uncertain Relationship Model

The user knows two structures are connected, and the exact relationship is still forming.

Account
├── System: The Harbor
└── System: North Wing
    relationship to The Harbor: "unknown" or "evolving"

This gives the user a way to preserve reality-in-progress without prematurely naming it.



## 8. Archived System Model

A System or structure still matters historically, but is no longer active.

Account
├── System: Current System
└── System: Old Map
    status: archived

This helps preserve migration meaning, old fronting records, old notes, and past organization.



## 9. Non-Tree / Graph Model

Some Systems may have overlapping or cross-linked relationships.

Account
├── System: A
├── System: B
└── System: C

Relationships:
A related to B
B sibling of C
C associated with A

This breaks the ordinary tree assumption. The structure becomes a graph of relationships instead of a single trunk with branches.



## 10. Focus-Based Working Model

The account has access to multiple Systems, but one System is active at a time.

Account has access to:
- The Harbor
- The Observatory
- The Archive

Current focus:
The Observatory

Inside the focused System, fronting can still be separate.

Focused System: The Observatory
Current front state:
- Mira fronting
- Sol co-fronting

This keeps System focus and fronting as separate concepts.

Focus answers:
Which System context is active?

Fronting answers:
Who or what is active inside that context?



## Design Notes

These examples imply several important PluralBridge design rules:

1. A root System means a System with no parent relationship.
2. There can be more than one root System.
3. Parent / child, sibling, related, subsystem, archived, and unknown relationships describe structure.
4. Structural relationships should not automatically grant access, authority, or visibility.
5. Access should come from explicit membership, consent, permission, or policy.
6. The active working context should be handled through focus.
7. Fronting should be modeled inside the focused System context.
8. The default user experience can remain simple while the underlying model stays flexible.
9. Therapist-facing views should be user-controlled and privacy-aware.
10. Professional input can improve safety without overriding lived experience.
