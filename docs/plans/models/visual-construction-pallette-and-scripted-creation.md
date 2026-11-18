# Future Design — Visual Construction Palette and Scripted Creation

Status: captured design direction for future work. This does not add implementation to the current task sequence.

## Purpose

Make it easy to build a plural structure directly in the workspace through a tool palette and the existing tree. Support novice and intermediate users through visible actions, while giving power users a future way to create structures through scripts or structured input.

The visual workflow should also remain useful to experienced users. Ease of navigation does not reduce the capabilities of the underlying model.

## Tool palette

Provide four building blocks on one palette:

- System
- Group
- Place
- Member

The selected destination and the location where an item is dropped determine the proposed operation.

## Tree selection determines the destination

The account appears at the top of the tree and is selectable.

- When the account is selected, adding an anchor creates a root-level anchor alongside the other roots.
- When an anchor is selected, adding a valid object places it beneath that anchor.
- The selected destination remains visibly highlighted.

This keeps root-level and nested construction within the same tree interaction. Selecting the account provides the root destination without requiring a separate root-drop area.

The exact relationship created must follow PluralBridge's model, supported object combinations, and permission rules. Visual nesting must not silently invent a relationship or change ownership.

## Creating anchors

The user selects the account or anchor that should contain the new object, then uses the palette to add a System, Group, or Place.

The creation flow should:

1. Clearly identify the proposed destination.
2. Open an appropriate form for the new object.
3. Let the user enter its name and other required information.
4. Save through the application's validated creation operations.
5. Update the tree and clearly identify the newly created object.

The detailed interaction between the highlighted selection and a different node under the pointer during dragging remains to be designed. The proposed destination must always be clear before saving.

## Creating members

After creating or selecting an anchor, that anchor remains highlighted in the tree.

Dragging the Member icon into the right-hand member panel opens the new-member form for that selected anchor. The user fills in the member information and saves. The new member then appears in the member list and tree.

The same palette can support different valid actions according to the destination. Those meanings must be explicit to the user.

## Interaction feedback

Show the destination clearly during construction. A preview or label could say:

- Add at account root
- Add group under Main House
- Add member to The Harbor

The form should retain that destination context so the user can verify it before saving.

Distinguish these operations explicitly as they are introduced:

- Create a new object.
- Add a placement of an existing object.
- Move an existing placement.

Dragging must not silently remove another occurrence, move ownership, or change grants.

## Demonstrations and onboarding

Use a few short demonstration screens or guided examples to teach the workflow through visible actions:

- Select a destination.
- Choose or drag a building block.
- Fill in its information.
- See the result appear.
- Continue building.

This provides a clearer introduction than asking new users to discover the construction workflow through menus alone.

The palette gives an empty account an obvious starting point.

## Keyboard, click, and mobile interaction

Provide equivalent creation actions for users who cannot or prefer not to drag.

The selected-destination rule should support:

- Drag-and-drop.
- Click-to-add.
- Keyboard operation.
- An appropriate touch interaction on phones.

The mobile interaction requires its own design. Desktop dragging must not be the only way to construct a structure.

## Power-user creation

Provide a future scripted or structured-input route for users who need to create larger or repetitive structures efficiently.

The format remains undecided. It could be a script, JSON, or another reviewed representation.

The motivation comes from experience with visual-only construction tools: repetitive work becomes cumbersome when the interface offers no programmable alternative. The owner's Cornelius work addressed a similar limitation with a compiler and Excel-based input. That experience informs the need for an alternative; it does not prescribe those technologies for PluralBridge.

## Shared application operations

The visual editor and future scripted creation should use the same underlying creation and placement operations.

Those operations own validation, permissions, and model rules. The palette, menus, and scripted input are interfaces to those shared capabilities.

This allows the visual workflow to remain approachable and the scripted workflow to remain repeatable while producing consistent results.

## Open design work

Before implementation, define:

- Palette placement and appearance.
- Exact drag, selection, and drop-destination behavior.
- Creation forms and post-save selection behavior.
- Supported parent/child combinations and their relationship semantics.
- Placement and movement interactions.
- Keyboard and phone equivalents.
- Scripted-input format, validation, execution, and error reporting.

These details remain future work. This document captures the intended interaction and architectural direction.