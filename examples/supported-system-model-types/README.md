# Supported System Model Types

This folder contains PluralBridge JSON examples for the system model types currently targeted for Alpha.

These examples are synthetic, self-contained, and intentionally small. They are meant to show supported system shapes, relationship patterns, and export structure without depending on live user data or the current database schema.

Each file uses the same flattened top-level JSON structure. Systems, members, groups, notes, front history, and other records are represented as separate collections. System context is expressed through explicit relationship and object-link records.

## Model Types

1. `example-001-simple-single-system.json`  
   One account context with one System.

2. `example-002-multiple-top-level-systems.json`  
   Multiple root-level Systems with no parent relationship.

3. `example-003-parent-child-system.json`  
   A parent System with a child System.

4. `example-004-sibling-systems.json`  
   Two peer Systems connected as siblings.

5. `example-005-related-systems.json`  
   Two Systems associated without parent, child, or sibling structure.

6. `example-006-subsystem.json`  
   A member-associated subsystem with its own System structure.

7. `example-007-evolving-uncertain-relationship.json`  
   Systems with a known connection whose exact relationship is still unresolved.

8. `example-008-archived-system.json`  
   An inactive historical System preserved for migration, notes, and fronting history.

9. `example-009-non-tree-graph.json`  
   Multiple Systems connected through graph-style relationships instead of a strict tree.

## Current Status

These examples passed the initial mechanical consistency check:

- valid JSON
- consistent top-level collection names
- matching example IDs and filenames
- synthetic IDs
- valid internal references
- relationship types reflected in lookups

The next design step is semantic review: confirming that each file expresses its intended PluralBridge system model clearly before database schema changes or generator work proceed.