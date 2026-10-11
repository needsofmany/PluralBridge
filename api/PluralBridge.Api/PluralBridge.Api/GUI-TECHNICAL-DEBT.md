# GUI Technical Debt (Running Log)

Purpose: keep a running list of UI-level gaps discovered while validating system-model behavior on `feature/system-models`.

Scope note: this log tracks GUI/product-surface debt only. Core schema/data/API correctness work stays in this branch; UI remediations are deferred to the later GUI branch unless explicitly requested.

## Open Items

- **TD-GUI-001**: Members view is current-system scoped while session counts are account-scoped
  - **Observed**: Session card aggregates across all systems in membership access (for example, `systems=2`, `members=4`), but `members` button shows only the active current system.
  - **Impact**: Users can read this as inconsistent behavior when validating multi-system examples.
  - **Status**: Deferred (intentional for this branch)

- **TD-GUI-002**: No explicit system context switcher in the demo UI
  - **Observed**: User cannot switch active system context from the GUI.
  - **Impact**: Multi-system examples require indirect testing or backend-default current system.
  - **Status**: Deferred to GUI branch

- **TD-GUI-003**: Session panel does not show relationship semantics breakdown
  - **Observed**: Session shows totals (`systems`, `members`, etc.) but not relationship model details (for example, parent/child/sibling/related counts).
  - **Impact**: Harder to validate examples like parent-child and sibling models from the UI alone.
  - **Status**: Deferred to GUI branch

- **TD-GUI-004**: Session card labels can imply "proof context" over account context
  - **Observed**: Header copy still says "read-only proof context" even though key fields now reflect account-scoped access.
  - **Impact**: Terminology can confuse expected behavior in multi-system fixtures.
  - **Status**: Deferred to GUI branch

- **TD-GUI-005**: No relationship topology view (tree/graph/hierarchy) for system models
  - **Observed**: UI does not provide a visual or structured relationship map across systems.
  - **Impact**: Users cannot verify how the full model is constructed (parent/child, sibling, related, graph edges) from the GUI alone.
  - **Status**: Deferred to GUI branch

- **TD-GUI-006**: No explicit relationship-type inventory/legend in UI
  - **Observed**: UI does not enumerate which relationship types exist in the current model (for example: `root`, `parent_child`, `sibling`, `related`, `subsystem`, `evolving/uncertain`, archived-state links, non-tree graph edges).
  - **Impact**: Relationship semantics can be partially validated in data, but not quickly verified in GUI as a complete type set.
  - **Status**: Deferred to GUI branch

- **TD-GUI-007**: "Current system" alone is insufficient for hierarchical/subsystem models
  - **Observed**: UI presents a single current-system context without showing parent/subsystem position in the relationship structure.
  - **Impact**: In non-peer models (for example parent-child or subsystem), users cannot infer authority/placement boundaries from current-system label alone.
  - **Status**: Deferred to GUI branch

- **TD-GUI-008**: No UI representation for evolving/uncertain relationship state
  - **Observed**: Session/UI does not expose relationship status values such as `unknown` or `evolving`.
  - **Impact**: Model semantics for uncertain or transitioning system relationships cannot be validated from GUI.
  - **Status**: Deferred to GUI branch

- **TD-GUI-009**: No explicit archived-relationship/archive-state visibility in system relationship view
  - **Observed**: UI does not explicitly surface when a system relationship/state is `archived`.
  - **Impact**: Archived model semantics can exist in data but are not directly verifiable in GUI.
  - **Status**: Deferred to GUI branch

## Recently Resolved (Kept for Context)

- Session and members no longer 403 due to proof-system/current-system mismatch in route system resolution.
- Session panel now shows current system ID/name from current access context.
- Phase label updated to `Phase SysModel Update`.

