# Cloud Step 16 Azure SQL Execution Boundaries

Date: 2026-06-06 PT

## Branch context

- Logical branch #1 has been merged to dev.
- Logical branch #2 is feature/002-cloud-readiness.
- Logical branch #2 has been rebased onto dev after the logical branch #1 merge.
- Current cloud branch checkpoint before Azure SQL candidate generation: a5a6dc1.

## Azure SQL execution boundary

The first Azure proof will use a target-database script, not a local repeat-build script.

The Azure candidate script must assume SSMS is already connected to the intended Azure SQL target database.

The Azure candidate script must not include:

- USE statements
- CREATE DATABASE statements
- DROP DATABASE statements
- ALTER DATABASE statements
- DBCC CLONEDATABASE statements
- SINGLE_USER, MULTI_USER, or ROLLBACK IMMEDIATE reset logic
- References to PluralBridge_RepeatBuild
- References to PluralBridge_RebuildScratch
- References to PluralBridge-Prequel as a live source database
- References to PluralBridge_Preop
- sqlcmd directives or sqlcmd variable tokens
- Private JSON payload paths or committed JSON payload data

## Target database naming

Recommended first Azure SQL proof database name: PluralBridgeCloudProof001.

The Azure SQL database may be created outside this repository workflow through the Azure portal or another explicit Azure administration path. Repository scripts for this phase begin after the empty target database exists and SSMS is connected to it.

## First cloud proof model

The first cloud proof should use seeded 1-6 SQL derived from the validated local repeat-build shape.

JSON import from ignored private files is deferred until the Azure SQL schema and seeded 1-6 proof are validated.

## Execution rule

Azure SQL DDL and DML execution happens only in SSMS. Git Bash may generate files and run text audits only.

## Next step

Generate an Azure SQL candidate script derived from the validated local 1-6 repeat-build shape, with local reset logic and live source-database dependencies removed.
