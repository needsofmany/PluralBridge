# PB-NEXT-DATA-001 — Partition Key and Data-Layer Isolation Decision

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`  
Scope: Data-layer isolation, partition key, protected domain boundaries, and table-design rules before implementation.

## 1. Purpose

This document captures the first PB-NEXT foundation decision: how PluralBridge partitions protected data and enforces data-layer isolation before any new application, API, import, upload, database, Azure runtime, or feature implementation work begins.

The decision must support the corrected architecture model:

- System is the primary protected domain container and isolation boundary.
- Account authenticates a person or user.
- Account and System remain separate concepts.
- Ownership, authorization, consent, audit, and processing purpose are modeled separately from System isolation.
- Application code must have a small number of explicit isolation rules instead of scattered tenancy checks.

## 2. Working Decision

Use `SystemId` as the primary partition key and data-layer isolation key for protected PluralBridge domain data.

This applies to protected resources such as:

1. Members
2. Groups
3. Privacy buckets
4. Notes
5. Current fronter state
6. Front history
7. Custom fronts
8. Avatars
9. Custom fields
10. Import batches
11. Source records
12. Source ID mappings
13. Audit records for System-scoped resources

Use `AccountId` for authentication and account lifecycle records, not as the primary partition key for protected System data.

Use explicit join/relationship records for account-to-System membership, stewardship, helper access, tester access, support access, sharing, delegation, and future relationship-based authorization.

## 3. Boundary Rules

### 3.1 System Boundary

A System is the protected data container for plural-domain records.

System-scoped tables should include:

1. `SystemId`
2. Stable resource ID
3. Created/updated metadata where appropriate
4. Isolation-aware uniqueness constraints where appropriate
5. Audit reference compatibility

### 3.2 Account Boundary

An Account identifies the authenticated user or login principal.

Account records should stay separate from protected System payload. Account deletion, account closure, System stewardship, System transfer, and support access need specific rules because Account and System lifecycles can diverge.

### 3.3 Membership Boundary

Account-to-System access belongs in membership or relationship records.

A single owner foreign key is not sufficient for the expected model because PluralBridge may need:

1. Multiple stewards for one System
2. One account participating in multiple Systems
3. Delegated helper access
4. Support/provider access
5. Tester or early stewardship roles
6. Relationship-based access control later

### 3.4 Data Subject Boundary

Data subject is separate from account, System, and accessor.

A member, System participant, account holder, helper, provider, or support actor may have different roles depending on resource and purpose. The data model must leave room for data subject / controller / accessor distinctions without forcing them into the `SystemId` column.

## 4. Table Design Rules

### 4.1 Protected Domain Tables

For protected System data, default table shape should include:

1. Primary key
2. `SystemId`
3. Resource-specific fields
4. Stable resource identity for audit references
5. Created/updated metadata where appropriate

### 4.2 System-Scoped Uniqueness

Any uniqueness rule for user-visible or imported domain values should be evaluated as System-scoped unless a stronger reason exists.

Candidate examples:

1. Imported source IDs
2. Source record mappings
3. System-local names where uniqueness is required
4. Import batch identifiers
5. Privacy bucket identifiers
6. Custom field keys where required

### 4.3 Import and Source Records

Import records belong under the protected System container.

Import-related records should preserve:

1. `SystemId`
2. `ImportBatchId`
3. Source system identity
4. Stable source record identity
5. Source-to-target mapping identity
6. Processing purpose and authority references where required by later PB-NEXT docs

### 4.4 Audit Records

Audit records for System-scoped resources should include `SystemId` for partitioning and queryability.

Audit records should reference stable resource IDs and authority artifacts rather than copying sensitive payload.

Audit records should be compatible with later decisions from:

1. PB-NEXT-AUTHZ-001
2. PB-NEXT-CONSENT-001
3. PB-NEXT-AUDIT-001
4. PB-NEXT-IMPORT-001

## 5. Enforcement Expectations

Data-layer isolation should be enforceable in a repeatable pattern.

Candidate enforcement mechanisms:

1. Repository/query-layer methods require `SystemId` for protected reads and writes.
2. Protected queries filter by `SystemId` at the data access boundary.
3. Mutations receive `SystemId` from authorized context rather than request body trust.
4. Import pipelines bind every protected write to one target `SystemId`.
5. Audit writes include `SystemId` and stable resource references.
6. Tests verify cross-System access denial for protected resources.

Later implementation may consider database-level support such as row-level security or scoped views, but this planning decision does not require selecting that mechanism yet.

## 6. Open Design Questions

1. Should every protected table physically include `SystemId`, including child tables whose parent already carries `SystemId`?
2. Which records need globally stable public IDs versus internal stable IDs?
3. Which System-scoped uniqueness constraints are required for DB Alpha?
4. How should membership and relationship records be partitioned when they connect Account and System?
5. Which import ledger records are operational-only and which become audit/evidence references?
6. Which records need pseudonymization or crypto-shredding support for account closure or System deletion?
7. Which diagnostic tables, if any, may exist outside System partitioning because they contain no protected payload?

## 7. First Implementation Gate Contribution

PB-NEXT-DATA-001 contributes this gate requirement:

Before coding starts, PluralBridge must have a documented table-design rule for protected resources:

- Protected System-domain records carry `SystemId`.
- Account records do not own protected System data directly.
- Account-to-System access is represented through membership or relationship records.
- Import/source records are System-scoped.
- Audit records are System-scoped where they reference System resources and remain payload-free.
- Query and mutation patterns must enforce System isolation at the data access boundary.

## 8. Explicit Non-Goals

This document does not implement:

1. Database schema changes
2. API authorization changes
3. Import pipeline changes
4. Upload behavior
5. Azure runtime behavior
6. Website changes
7. Release promotion
8. A final ReBAC engine decision
9. A final consent schema
10. A final audit schema

Those decisions belong to later PB-NEXT planning documents and implementation slices.

## 9. Follow-Up Architecture Guardrails

The follow-up r/softwarearchitecture feedback adds two guardrails for the schema pass.

### 9.1 ReBAC Is the Relationship Layer

Relationship-based authorization should model ownership, sharing, delegation, membership, stewardship, helper access, provider access, tester access, and support access.

Consent state and purpose-of-use should remain policy inputs to the central authorization decision. They should not be modeled as relationship tuples merely because a ReBAC engine or tuple store is available.

Time-bounded consent belongs in first-class consent records with policy evaluation, versioning, expiration, and revocation semantics. Encoding time-bounded consent as relationship tuples creates avoidable schema and lifecycle complexity.

### 9.2 Model the Seams, Defer the Service Splits

The five separations are conceptual and schema boundaries. They are not a requirement to build five deployable services in the first implementation pass.

PluralBridge should put seams in the schema and behind the central authorization decision point now, while keeping the deployment shape monolithic until operational pressure proves a split is necessary.

Near-term implementation should favor:

1. Clear schema boundaries
2. A single authorization decision point
3. Explicit policy inputs
4. A monolithic deployable unit
5. Deferred service extraction

This preserves the July 1 delivery pressure without losing the foundation needed for later scale, auditability, consent handling, or relationship-based access control.
