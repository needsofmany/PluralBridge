# Cloud Readiness Gitignore Policy Audit

Date: 2026-06-06 PT

## Decision

The cloud-readiness branch will track safe planning, audit, manifest, checkpoint, shell, PowerShell, and SQL candidate artifacts.

The branch will not track transient cloud scratch folders, temporary files, local logs, or JSON/JSONL payload files under the cloud candidate folder.

Private export JSON remains ignored separately through the existing database/targeting/prop_candidate/private_export/json/ rule.

## Boundary

This change affects repository hygiene only. It does not touch SQL Server, Azure SQL, or private JSON payload data.
