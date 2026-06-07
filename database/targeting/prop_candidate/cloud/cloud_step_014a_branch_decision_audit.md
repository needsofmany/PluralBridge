# Cloud Readiness Branch Decision Audit

Date: 2026-06-06 PT

## Decision

Cloud-readiness work will proceed on a new feature branch instead of continuing directly on feature/table-normalization-plan.

The new branch is logical branch #2 for currently unmerged feature work.

## Logical branch ordering

- Logical branch #1: feature/table-normalization-plan
- Logical branch #2: feature/002-cloud-readiness

Logical branch #1 is complete for its current scope, committed, and pushed. It has not yet been merged to dev or master.

Logical branch #1 is intended to be the first branch merged when the unmerged feature branches are integrated.

Logical branch #2 starts from the same green checkpoint HEAD and records Azure/cloud-readiness work separately.

## Starting checkpoint

- Starting HEAD: 1f04f24 Add structure audit for private JSON export
- Prior green private export checkpoint: database/targeting/prop_candidate/private_export/private_export_step_004_green_checkpoint.md
- Validated local 1-6 repeat-build script: database/targeting/prop_candidate/pop_candidate_one_script_001_local_repeatbuild_001_006.sql

## Boundary

This note records branch governance only. It does not change SQL Server, Azure SQL, private JSON payloads, or generated export data.
