# PluralBridge Database API Contracts

**Status:** Initial design draft  
**Related issue:** [#40 — Define portable local persistence schema and cloud/local data compatibility](https://github.com/needsofmany/PluralBridge/issues/40)  
**Proposed repository location:** `docs/system-modeling/db-SQL/Database-API-Contracts.md`

## 1. Purpose and scope

PluralBridge treats its database operations as a defined API boundary. In the cloud implementation, SQL Server stored procedures encapsulate database operations and expose parameters, result sets, and defined behavior to the backend. A future locally hosted web application must provide equivalent operations without requiring the same database engine or SQL dialect.

This document is intended to become the authoritative **database-operation contract catalog**. It describes externally observable behavior rather than prescribing a particular implementation. The same contracts can support:

- SQL Server stored-procedure implementations.
- Local database implementations using stored procedures, parameterized queries, views, or adapters as appropriate.
- Backend integration and regression tests.
- Shared fixture-based cloud/local compatibility tests.
- A future interactive, Swagger-like database API explorer.

**Not in scope:** Selecting a local database engine, rewriting the current navigator, or treating a database API as an unauthenticated public network endpoint. “Public API” here means a documented interface between trusted application components, subject to database and application authorization.

## 2. Architecture and compatibility principles

1. **Contract first.** Define operation names, inputs, outputs, errors, authorization expectations, and semantics independently of provider-specific SQL.
2. **Implementation freedom.** SQL Server can implement contracts with stored procedures; a local provider can implement them differently.
3. **Frontend independence.** The frontend consumes the same backend API and navigator response shapes regardless of the persistence provider.
4. **Behavioral equivalence.** Matching tables is insufficient. Operations must return equivalent results for equivalent data and access context.
5. **Canonical-model coverage.** Contracts must accommodate all eleven canonical models, including Systems, Groups, Places, Members, placements, and graph relationships, as applicable to each operation.
6. **No mandatory System root.** A valid plural configuration may have Group or Place anchors and zero Systems.
7. **Explicit security context.** Operations must document how account identity, authorization, and visibility are established; a local deployment must not bypass these semantics by accident.
8. **Stable output contracts.** Callers should not depend on temporary tables, internal query order, or undocumented implementation details.

## 3. Contract conventions

Each catalog entry should include the following fields. Use `TBD` where the implementation has not yet been inspected or a decision has not been made.

| Field | Meaning |
| --- | --- |
| Contract ID | Stable logical identifier independent of provider |
| Operation | Human-readable name and purpose |
| SQL Server implementation | Procedure name and source-file path, when applicable |
| Local implementation | Provider-specific implementation reference, when available |
| Inputs | Name, type, required/optional, null behavior, defaults, validation |
| Outputs | Named result sets or return model; columns, types, nullability, semantics |
| Errors | Defined failure categories and caller-visible behavior |
| Authorization | Required identity, account scope, roles, and visibility rules |
| Side effects | Read-only or mutating; transaction and idempotency expectations |
| Ordering | Explicit ordering guarantees, if any |
| Version | Contract revision and compatibility expectations |
| Fixtures/tests | Shared cases and expected outputs |
| Status | Draft, verified against implementation, or implemented by provider |

### 3.1 Naming and versioning

- Use stable logical IDs such as `navigator.resolve-tree`; do not require the local provider to use SQL Server procedure names.
- Document the existing procedure name as an implementation binding, not as the cross-provider contract name.
- Treat changes to required inputs, output fields, result-set ordering, or visibility semantics as contract changes requiring review.
- Record compatible additions and breaking changes explicitly. A versioning scheme will be selected when the first contracts are verified.

### 3.2 Data types and serialization

Define contract-level types (for example, identifier, string, boolean, timestamp, enumeration, and nullable reference) separately from SQL Server types. Document mapping rules for GUIDs, timestamps, nulls, Unicode strings, and any provider-specific precision or collation behavior. Preserve canonical identifiers and relationship directionality across providers.

### 3.3 Errors and security

Each operation must distinguish invalid input, unauthorized or out-of-scope access, missing records, integrity violations, and unexpected execution failures where applicable. Do not expose sensitive database diagnostics through production-facing APIs. Any future interactive explorer must be disabled or tightly restricted in production and must enforce the same authorization rules as normal callers.

## 4. Initial operation catalog

The following is a **starting inventory**, not a verified enumeration of all stored procedures or their signatures. Procedure names and result-set labels below reflect the current navigator work; exact signatures and schemas must be checked against source before being marked verified.

| Contract ID | SQL Server implementation | Responsibility | Status |
| --- | --- | --- | --- |
| `navigator.resolve-tree` | `dbo.pb_show_tree_step4_members_by_email` | Resolve account/anchor context and produce navigator-related graph/hierarchy data | SQL Server binding verified; local binding unresolved |
| `navigator.build-topology-seed` | `dbo.pb_show_tree_step5_topology_seed_builder` | Build topology data used by the navigator pipeline | Internal/dependency candidate; determine whether it belongs in the public contract catalog |

### 4.1 `navigator.resolve-tree` — SQL Server contract

**Purpose:** Resolve the signed-in account and return the four navigator result sets produced by the current show-tree entry procedure.

**HTTP binding:** `GET /api/me/plural-map`  
**SQL Server binding:** `dbo.pb_show_tree_step4_members_by_email`  
**Source:** `docs/system-modeling/db-SQL/pb_show_tree_step4_members_by_email.sql`  
**Local binding:** TBD  
**Read/write:** Read-only. The procedure does not persist rows.  
**Status:** SQL Server response verified against the current procedure definition. The local database binding remains unresolved.

**Inputs:** The HTTP action takes no account selector. The API resolves `AccountId` from the authenticated `NameIdentifier` claim through `pb_accounts`, then passes that account's `Email` as `@Email NVARCHAR(320)`. The client does not supply `@Email`.

**Result sets, in procedure order:**

| Result set | JSON array | Columns |
| --- | --- | --- |
| `Context` | `context` | `Section NVARCHAR`, `Email NVARCHAR`, `AccountId UNIQUEIDENTIFIER` |
| `GraphNodeCounts` | `graphNodeCounts` | `Section NVARCHAR`, `ObjectType NVARCHAR`, `NodeCount INT` |
| `GraphEdges` | `graphEdges` | `Section NVARCHAR`, `ParentObjectType NVARCHAR`, `ParentObjectId UNIQUEIDENTIFIER`, `ChildObjectType NVARCHAR`, `ChildObjectId UNIQUEIDENTIFIER`, `EdgeRank INT`, `EdgeSource NVARCHAR` |
| `HierarchyPreview` | `hierarchyPreview` | `Section NVARCHAR`, `TreeLine NVARCHAR`, `PathKey NVARCHAR` |

JSON names are camelCase (`pathKey` for `PathKey`). Property order inside each object is not part of the contract. Array order is the procedure's row order, including every `Section`, `TreeLine`, and `PathKey` value. Object types remain strings. `Context` is exactly one row, and its `AccountId` is the authenticated account. The later arrays may be empty when the account has no current system. A missing, malformed, extra, or account-mismatched result is rejected with no map payload.

The stored procedure still returns only those three preview columns. The API derives the following JSON properties from the verified walk encoding and does not change row order:

| JSON property | Derivation |
| --- | --- |
| `nodeLabel` | `TreeLine` after one leading indent of four-space groups and one `├── ` or `└── ` prefix. The type prefix inside the label, such as `System: `, stays. A line without that drawing prefix has a null label. |
| `objectType` | Type text in the last `PathKey` segment. A segment is `typeCode\|rank\|type\|guid`. |
| `objectId` | Guid in that segment, accepted only by `Guid.TryParseExact` format `D`. |
| `parentPathKey` | The path before the final `.`, or null for a single segment. A group whose parent segment is `system` is then attached to that occurrence's `15\|000000\|groups\|00000000-0000-0000-0000-000000000000` row when that exact row is present. |
| `header` | True only when the last segment is that Groups header. |
| `anchorName` | Stored name for a `system`, `group`, or `place` row: `pb_systems.SystemName`, `pb_groups.GroupName`, or `pb_places.PlaceName`, matched by that row's `objectType` and `objectId`. The lookup runs only after the procedure reader is closed and only for distinct anchor ids already in the mapped response. Repeated occurrences share one stored name. The value is null for members, synthetic headers, missing rows, and a null `SystemName`. `nodeLabel` is unchanged, including any edge-label suffix. |

A path that does not match this encoding keeps `section`, `treeLine`, and `pathKey`, with null identity and `header` false. `pathKey` remains the opaque occurrence key. The browser matches `parentPathKey` to it and does not split it. Repeated occurrences stay separate rows. The local database binding remains unresolved and must produce this same HTTP shape.

**Required behavior to test:**

- A plural with a conventional System anchor.
- Multiple Systems and directed graph relationships.
- Group and Place anchors.
- A valid configuration with zero Systems (including canonical Example 12).
- Nested placements and membership relationships as defined by the canonical models.
- Correct account scoping and visibility, including absent or invalid context.
- Deterministic output semantics where ordering affects the navigator.

**Errors and authorization:** An unauthenticated request is rejected before the action. An authenticated request whose account cannot be resolved returns 401 and no map. A failed, incomplete, extra, malformed, or account-mismatched procedure result returns a generic 500 and no map. Responses do not include SQL text or connection secrets.

## 5. Cloud/local implementation bindings

The backend should depend on a logical persistence-operation interface. Each provider supplies an implementation that satisfies the same contract.

| Concern | Cloud implementation | Local implementation |
| --- | --- | --- |
| Database engine | SQL Server | Not yet selected |
| Operation mechanism | Stored procedures | Engine-appropriate queries/procedures/adapters |
| Contract IDs | Shared | Shared |
| Inputs and outputs | Shared logical schemas | Shared logical schemas |
| Navigator behavior | Current SQL Server implementation | Must be functionally equivalent |
| Canonical fixtures | Shared | Shared |
| Authorization and visibility | Documented contract | Equivalent documented contract |

A local web application must populate the left-hand navigator from local persistence without relying on SQL Server stored procedures being present. The local implementation is free to use different SQL internally, but it must satisfy the same observable contract.

## 6. Contract verification

### 6.1 Verification process

1. Inventory the actual SQL Server procedures called by the backend, including helper procedures and their call graph.
2. Identify which procedures are **application-facing contracts** and which are **internal implementation details**.
3. Extract exact input signatures from SQL Server metadata and source definitions.
4. Document output result sets and schemas through source review and execution-based tests; metadata alone may not describe procedures using temporary tables, dynamic SQL, or multiple result sets.
5. Capture representative fixture inputs and expected normalized outputs.
6. Execute equivalent tests against SQL Server and each local provider.
7. Compare semantic outputs, including identifiers, relationship direction, visibility, null handling, and ordering where specified.
8. Mark a contract `Verified` only when its definition and test evidence agree.

### 6.2 Shared fixtures

Use the canonical-model generator fixtures as test inputs, including Example 12's Group and Place anchors with zero Systems. Maintain explicit expected results for the navigator and other contracted operations. A fixture's successful generation does not by itself establish database API compatibility.

### 6.3 Acceptance criteria for issue #40

- A documented inventory of required persistence operations exists.
- Application-facing operation contracts are separated from internal stored-procedure helpers.
- Each required contract specifies inputs, outputs, errors, and authorization behavior.
- The chosen local provider can implement the same contracts without requiring T-SQL compatibility.
- Shared fixture tests establish equivalent cloud/local navigator behavior, including zero-System configurations.
- Import/export and identifier/relationship compatibility are tested across providers.
- The frontend does not branch on the persistence provider.

## 7. Future Swagger-like database API explorer

The proposed explorer would render the contract catalog as browsable, searchable documentation analogous to Swagger UI, but organized around **database operations**, not HTTP routes.

Potential capabilities:

- Browse operations by domain (navigator, membership, placement, import/export, etc.).
- Inspect input parameters, result-set schemas, authorization requirements, and examples.
- View the SQL Server implementation binding and any local-provider bindings.
- Run approved operations against an explicitly selected **development/test** database.
- Inspect multiple result sets, execution errors, and contract-validation outcomes.
- Compare normalized results across providers using the same fixture.
- Flag contract drift when an implementation changes.

**Implementation approach:** Start with this Markdown catalog. Once the contracts stabilize, consider structured machine-readable definitions as the source for generated Markdown and the interactive explorer. Do not make the explorer a prerequisite for completing the current SQL Server navigator.

## 8. Open decisions

- Which stored procedures are stable application-facing operations versus internal pipeline steps?
- What is the exact production navigator operation and response contract?
- Which local database engine best meets deployment, concurrency, portability, and maintenance requirements?
- How will authentication and account visibility be represented in local mode?
- What contract versioning and normalized test-output format should be adopted?
- Should contract definitions eventually move to a structured format to support automatic documentation and test generation?

## 9. Immediate next steps

1. Finish and verify the current SQL Server navigator implementation.
2. Inventory its procedure call graph and identify the backend-facing entry point.
3. Replace the `TBD` fields in the first navigator contract with source-verified definitions.
4. Add fixture-based contract tests before selecting and implementing the local persistence provider.

---

*This document is an architectural draft. It does not assert that all listed operations, signatures, or output schemas have been verified against the current repository.*
