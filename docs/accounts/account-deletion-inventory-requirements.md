# Account deletion inventory requirements

The requirements for schema mapping, inventory, account isolation, exclusion of account protection, and deletion order are settled. Implementation, live-database verification, and the deferred dependencies in Step 6 are not done. This document does not authorize deleting any account, and it is not ready to execute. No account is selected.

Schema reference: `docs/schema/account-deletion-dependency-map.md`.

That map is tied to `scripts/sqlserver/PB-DB-backup-112226.sql`, script date 11/22/2026 5:01:40 AM, SHA-256 `DAAAA0CA71FFDE5B7E843BE794A52D3787DFF898E813122D5DB7227E36E16DB1`. Re-read these requirements when that hash changes.

## Overview

1. **Dependency map.** `docs/schema/account-deletion-dependency-map.md` is the reusable schema map for that file hash. It records tables, primary keys, `NO ACTION` foreign keys, and logical references. It is repository evidence, not a live catalog reading.
2. **Inventory.** Step 2 inventories one owner-supplied `AccountId`. A missing value refuses execution. The included paths are the direct account rows, memberships, links, placements, the systems, groups, places, and members those paths name, and their dependent records.
3. **Account isolation.** Step 3 keeps memberships, placements, links, and relationships inside the supplied account’s boundary. A discovered violation is an integrity defect to report. Repair is not defined.
4. **Account protection.** Step 4 leaves special protection of the owner’s main account as separate work. These requirements apply to whichever account the owner selects. They define no protected-account list or protection mechanism.
5. **Deletion order.** Step 5 orders deletion of the included records from the map’s foreign keys. Child rows precede the rows they reference. The ordering requirements are settled. Execution is deferred.

## Purpose and input

The inventory identifies the records for one account. The owner supplies one `AccountId` when the inventory is later run. This document does not select an account.

A missing `AccountId` refuses execution. It is not an empty inventory.

## Account boundary

Accounts never share data. Accounts never cross-reference another account’s data.

The inventory follows the paths below for the supplied account. It does not describe hypothetical cross-account cases.

## Step 3 — Account isolation

Accounts never share data. Accounts never cross-reference another account’s data.

Memberships, placements, links, and relationships operate within their account’s boundary.

The inventory remains scoped to the supplied `AccountId` and its associated objects and records.

Any discovered violation is an integrity defect to report. This step does not define its repair.

## Step 4 — Account protection excluded

These requirements apply to whichever account the owner selects.

Special protection of the owner’s main account is a separate work item.

This investigation does not define account-specific exclusions, protected-account lists, or protection mechanisms.

This documentation change does not authorize deleting any account.

## Included records

### Direct account records

Include these rows when the account column equals the supplied `AccountId`.

| Table | Path |
|---|---|
| `pb_accounts` | `AccountId` |
| `pb_account_credentials` | `AccountId` foreign key |
| `pb_account_codes` | `AccountId` foreign key. The `AccountId` value is the supplied account. |
| `pb_account_code_delivery_outbox` | `AccountId` column. The catalog defines no foreign key. Application code inserts that column. |
| `pb_account_object_links` | `AccountId` foreign key |
| `pb_account_object_placements` | `AccountId` foreign key |
| `pb_object_memberships` | `AccountId` foreign key |
| `pb_object_membership_roles` | `ObjectMembershipId` of an included object membership. The primary key is `(ObjectMembershipId, RoleId)`. |
| `pb_system_memberships` | `AccountId` foreign key |
| `pb_system_membership_roles` | `SystemMembershipId` of an included system membership. The primary key is `(SystemMembershipId, RoleId)`. |
| `pb_places` | `AccountId` foreign key |
| `pb_custom_fields` | `AccountId` foreign key |
| `pb_anchor_visibility_permissions` | `AccountId` foreign key |

`pb_account_statuses`, `pb_roles`, `pb_system_membership_statuses`, `pb_visibility_types`, and `pb_source_systems` are lookup tables. Role and status identifiers on included rows are references to those lookups. The lookup tables themselves are not account records.

### Memberships, links, and placements

From the included memberships, links, and placements, include the actual system, group, place, or member when the stored type is `system`, `group`, `place`, or `member` and the stored id is that table’s primary key.

- `pb_system_memberships.SystemId` includes that `pb_systems` row.
- `pb_object_memberships` includes the named row for those four types.
- `pb_account_object_links` includes the named row for those four types.
- `pb_account_object_placements` includes a parent or child of those four types. Parent type `account` names an account.

A place included by `AccountId` and also reached through one of these paths is the same place.

A membership, link, or placement whose type is `note`, `privacyBucket`, `visibilityScope`, `frontHistory`, or `customField` does not by itself include that target. Notes, privacy buckets, visibility scopes, and front-history rows are included as dependents of an included system or member. Custom fields are included by their own `AccountId`.

### Dependent records

Include the dependent rows of each included system, group, place, or member.

| Included object | Dependent records |
|---|---|
| System | `pb_groups` by `SystemId`; `pb_group_members`, `pb_group_system_contexts`, and `pb_members` by `SystemId`; `pb_notes`, `pb_privacy_buckets`, `pb_visibility_scopes`, and `pb_model_provenance` by `SystemId`; `pb_front_history` by `SystemId`; `pb_system_object_links` and `pb_system_object_placements` by `SystemId`; `pb_system_relationships` and `pb_system_relationship_edges` when `SystemId`, `ParentSystemId`, or `TargetSystemId` is that system |
| Group | child `pb_groups` by `ParentGroupId`; `pb_group_members` and `pb_group_system_contexts` by `GroupId` |
| Place | `pb_place_members` by `PlaceId` |
| Member | `pb_member_avatars` and `pb_member_system_contexts` by `MemberId`; `pb_front_history` by `MemberId`; `pb_visibility_scope_members` by `MemberId` |
| Included front-history row or visibility scope | `pb_front_history_members` by `FrontHistoryId`; `pb_visibility_scope_members` by `VisibilityScopeId` |

`pb_system_object_links` and `pb_system_object_placements` are included with their system. Their object ids are references. They do not start another inclusion walk.

A system named only as the other endpoint of a relationship is not included. The relationship row is included. The other endpoint is a reference.

### Anchor and member relationships

Include a `pb_anchor_relationships` row when its source or target is an included system, group, or place. The catalog limits both endpoints to those three types. Including the relationship row does not include the other endpoint.

Group-member, place-member, and front-history-member rows are the member relationship records. Their inclusion paths are in the member traversal below.

On an included system relationship or relationship edge, `AssociatedObjectType` and `AssociatedObjectId` stay references. They do not include another object.

### Visibility and permission records

Include `pb_anchor_visibility_permissions` for the supplied account, and include `pb_member_visibility_permissions` through the member traversal.

Include rows whose `PermissionMode` or `VisibilityMode` is `inherit`. Include `unset` and `explicit` rows as well. `inherit` is not a reason to omit the row.

A visibility row’s system, group, place, or visibility-type id is a reference unless that object is already included by a path in this document.

## Member traversal

Include a `pb_members` row when its `MemberId` equals `MemberId` on an included row in:

- `pb_group_members`, path `member-of-group-member`
- `pb_place_members`, path `member-of-place-member`
- `pb_front_history_members`, path `member-of-front-history-member`

Also include a member named by a membership, link, or placement, and a member whose `SystemId` is an included system.

Apply the member-dependent records above to every included member. A front-history row included through a member includes its `pb_front_history_members` rows. `MemberId` on those rows includes further members, and those members use the same member-dependent traversal.

For every included member, also include:

- `pb_group_members` whose `MemberId` is that member, path `group-member-of-included-member`
- `pb_place_members` whose `MemberId` is that member, path `place-member-of-included-member`

On rows reached through those two reverse paths, `GroupId`, `PlaceId`, and `SystemId` remain references. They do not include the group, place, or system, and they do not continue into that parent or into its other members.

Include `pb_member_visibility_permissions` when `MemberId`, `GroupMemberId`, or `PlaceMemberId` matches an included member, group-member row, or place-member row. The catalog allows one of those three targets on each visibility row.

## Deduplication and output

The identity of an included record is its primary key. Composite keys stay intact: `(ObjectMembershipId, RoleId)` and `(SystemMembershipId, RoleId)`.

A record reached by more than one path is one record. The count uses distinct primary keys. The report keeps every path that reached the key.

A repeated primary key ends traversal at that record. This includes a repeated `MemberId`, `GroupId`, `FrontHistoryId`, `FrontHistoryMemberId`, `GroupMemberId`, `PlaceMemberId`, or `MemberVisibilityPermissionId`. The member, front-history, and front-history-member cycle stops on those repeated keys.

The inventory report contains:

- table name
- record identifier
- distinct count
- association path for each included record

Referenced identifiers are a separate result from included target records. A source row can be included while an id stored on it remains only a reference.

The report excludes secret values and content payloads. It does not include password hashes, code hashes, outbox plaintext codes, tokens, raw JSON, note bodies, or avatar image bytes. The outbox is reported by `OutboxId` and count.

## Retained and deferred data

`pb_account_audit_events` and `pb_member_write_audit` are retained records. They remain after account deletion. The inventory identifies them separately from included records.

Identify an account audit row when `ActorAccountId` or `TargetAccountId` is the supplied account. Identify a member-write audit row when `AccountId` is the supplied account. One audit row matching both actor and target is one retained row, with both paths.

`SystemId`, `MembershipId`, `MemberId`, and `AnchorObjectType` plus `AnchorObjectId` on those audit rows do not establish that the named object belongs to the account. An object is included only by the paths in the included-record and member-traversal sections.

`pb_account_codes` rows whose `AccountId` is null stay untouched. Their maintenance, and any retention for abuse or bot tracing, belongs to the code-management process. They are not part of this inventory.

Import provenance is deferred until imports are addressed. This inventory does not include `pb_source_id_map`, `pb_import_batches`, or `pb_source_records`.

## Remaining ambiguity

No unresolved association question remains inside the decisions above. Implementation and a live catalog comparison are still outside this settled requirements text.

## Step 5 — Deletion order

Step 5 ordering requirements are settled. Implementation, and the required audit foreign-key schema changes, remain deferred. No account is selected. This section records the dependency order for records included by Step 2. It does not authorize deleting any account. The catalog foreign keys are `NO ACTION`: a referencing row is deleted before the row it references. Lookup tables are not deleted.

Stages A through D have no order among the tables inside the same stage. A later stage waits only where a foreign key below says so.

### Stage A. Account-referencing rows and rows with no foreign key to an included object

These must precede `pb_accounts`. They do not order systems, groups, places, or members.

| Table | Precedes | Constraint |
|---|---|---|
| `pb_account_credentials` | `pb_accounts` | `FK_pb_account_credentials_pb_accounts` |
| `pb_account_codes` whose `AccountId` is the supplied account | `pb_accounts` | `FK_pb_account_codes_pb_accounts` |
| `pb_account_code_delivery_outbox` | no foreign-key predecessor | `AccountId` has no foreign key. The row is included, and it does not block `pb_accounts`. |
| `pb_account_object_links` | `pb_accounts` | `FK_pb_account_object_links_pb_accounts_AccountId`. `ObjectType` and `ObjectId` are not foreign keys, so they do not order the named object. |
| `pb_account_object_placements` | `pb_accounts` | `FK_pb_account_object_placements_pb_accounts_AccountId`. Parent and child ids are not foreign keys. |
| `pb_custom_fields` | `pb_accounts` | `FK_pb_custom_fields_AccountId` |
| `pb_anchor_relationships` | no foreign-key predecessor | No foreign key to systems, groups, or places. Deleting the relationship row does not wait on those objects, and the row does not block them. |
| `pb_object_membership_roles` | `pb_object_memberships` | `FK_pb_object_membership_roles_pb_object_memberships`. `RoleId` references the `pb_roles` lookup, which stays. |
| `pb_system_membership_roles` | `pb_system_memberships` | `FK_pb_system_membership_roles_pb_system_memberships`. `RoleId` references the lookup, which stays. |

### Stage B. Visibility rows

`PermissionMode` and `VisibilityMode` values, including `inherit`, do not change this order.

| Table | Precedes | Constraint |
|---|---|---|
| `pb_member_visibility_permissions` | the included `pb_group_members`, `pb_place_members`, or `pb_members` row named by its one target column | `FK_pb_member_visibility_permissions_pb_group_members`, `FK_pb_member_visibility_permissions_pb_place_members`, `FK_pb_member_visibility_permissions_pb_members` |
| `pb_anchor_visibility_permissions` | the included place, group, system, and the account | `FK_pb_anchor_visibility_permissions_pb_places`, `FK_pb_anchor_visibility_permissions_pb_groups`, `FK_pb_anchor_visibility_permissions_pb_systems`, `FK_pb_anchor_visibility_permissions_pb_accounts` |

### Stage C. History and scope association rows

| Table | Precedes | Constraint |
|---|---|---|
| `pb_front_history_members` | `pb_front_history` and `pb_members` | `FK_pb_front_history_members_pb_front_history`, `FK_pb_front_history_members_pb_members` |
| `pb_visibility_scope_members` | `pb_visibility_scopes` and `pb_members` | `FK_pb_visibility_scope_members_pb_visibility_scopes`, `FK_pb_visibility_scope_members_pb_members` |

### Stage D. Member, group, and place association rows

| Table | Precedes | Constraint |
|---|---|---|
| `pb_front_history` | `pb_members`, and `pb_systems` when `SystemId` is set | `FK_pb_front_history_pb_members`, `FK_pb_front_history_pb_systems` |
| `pb_member_avatars` | `pb_members` | `FK_pb_member_avatars_pb_members` |
| `pb_member_system_contexts` | `pb_members` and `pb_systems` | `FK_pb_member_system_contexts_pb_members`, `FK_pb_member_system_contexts_pb_systems` |
| `pb_group_members` | `pb_groups`, `pb_members`, and `pb_systems` when `SystemId` is set | `FK_pb_group_members_pb_groups_GroupId`, `FK_pb_group_members_pb_members_MemberId`, `FK_pb_group_members_pb_systems_SystemId` |
| `pb_place_members` | `pb_places` and `pb_members` | `FK_pb_place_members_PlaceId`, `FK_pb_place_members_MemberId` |
| `pb_group_system_contexts` | `pb_groups` and `pb_systems` | `FK_pb_group_system_contexts_pb_groups`, `FK_pb_group_system_contexts_pb_systems` |

A group-member or place-member row reached only through an included member is still deleted here. Its `GroupId`, `PlaceId`, and `SystemId` stay references. Those references do not add the parent to this order, and deleting the membership row does not require deleting that parent.

### Stage E. Groups, then the remaining system dependents

`pb_groups.ParentGroupId` is a self-reference, `FK_pb_groups_pb_groups_ParentGroupId`. An included group that is another included group’s parent is deleted after that child. `FK_pb_groups_pb_systems_SystemId` then places every included group before its system when `SystemId` is set.

After those groups, these rows precede `pb_systems`: `pb_notes`, `pb_privacy_buckets`, `pb_visibility_scopes`, `pb_model_provenance` when `SystemId` is set, `pb_system_object_links`, `pb_system_object_placements`, `pb_system_relationships`, and `pb_system_relationship_edges`. Each foreign key points at `pb_systems` on `SystemId`, and the relationship tables also point at `pb_systems` on nullable `ParentSystemId` and `TargetSystemId`. `AssociatedObjectType` and `AssociatedObjectId` are not foreign keys and do not add a stage.

Relationship rows can name each other’s systems. That graph does not create an order among the relationship rows. The foreign keys require the relationship rows to go before the included system rows. A system that appears only as the other endpoint was not included, and this order does not delete it.

### Stage F. Members, memberships, places, systems, and the account

| Table | Precedes | Constraint |
|---|---|---|
| `pb_members` | `pb_systems` when `SystemId` is set | `FK_pb_members_pb_systems`. A null `SystemId` does not order that member against a system. |
| `pb_object_memberships` | `pb_accounts` | `FK_pb_object_memberships_pb_accounts_AccountId`, after its role rows |
| `pb_system_memberships` | `pb_accounts` and `pb_systems` | `FK_pb_system_memberships_pb_accounts`, `FK_pb_system_memberships_pb_systems`, after its role rows |
| `pb_places` | `pb_accounts` | `FK_pb_places_AccountId`, after its place members and anchor-visibility rows |
| `pb_systems` | nothing further in the included set | Wait until every included foreign key listed above has been removed. |
| `pb_accounts` | nothing further | Wait until every included row with a `NO ACTION` foreign key to `AccountId` has been removed. Retained audit rows are not those predecessors. |

`pb_accounts.AccountStatusId` references the status lookup. The lookup stays, so that foreign key does not add a delete stage.

### Left in place

Null-account `pb_account_codes` stay untouched. `AccountId` is null, so `FK_pb_account_codes_pb_accounts` does not point at the supplied account and does not block its deletion.

`pb_source_id_map`, `pb_import_batches`, and `pb_source_records` stay deferred. Their foreign keys point at import batches and source systems, not at the included account objects. Under the current catalog they do not block deletion of those objects.

Account-protection rules are outside this order. The order applies to whichever account the owner selects.

### Retained audit records

Audit rows and their original identifiers remain permanently. Those identifiers are historical references and may outlive the referenced account, member, membership, or system. Audit retention must not prevent deletion of those original records. Audit contents stay unchanged.

The current catalog still prevents that outcome. These `NO ACTION` foreign keys are present in `scripts/sqlserver/PB-DB-backup-112226.sql` and have not been changed:

| Retained table | Current constraint | Original record it currently blocks |
|---|---|---|
| `pb_account_audit_events` | `FK_pb_account_audit_events_ActorAccount` on `ActorAccountId` | `pb_accounts` |
| `pb_account_audit_events` | `FK_pb_account_audit_events_TargetAccount` on `TargetAccountId` | `pb_accounts` |
| `pb_member_write_audit` | `FK_pb_member_write_audit_accounts` on `AccountId` | `pb_accounts` |
| `pb_member_write_audit` | `FK_pb_member_write_audit_members` on `MemberId` | the referenced `pb_members` row |
| `pb_member_write_audit` | `FK_pb_member_write_audit_system_memberships` on `SystemMembershipId` when set | the referenced `pb_system_memberships` row |
| `pb_member_write_audit` | `FK_pb_member_write_audit_systems` on `SystemId` when set | the referenced `pb_systems` row |

`pb_account_audit_events.SystemId` and `MembershipId` have no foreign key. They do not block a system or membership delete.

A required future schema change, not an implemented change, is to adjust the six foreign keys above so a retained audit row does not prevent deletion of the account, member, system membership, or system it names. No schema change has been made. No mechanism for that adjustment is chosen here.

`FK_pb_members_pb_systems` and `FK_pb_system_memberships_pb_systems` remain ordinary delete order: the member or membership row is deleted before its system. They are not audit constraints.

### Group containment

Preventing cycles in `pb_groups.ParentGroupId` is a required, deferred work item documented separately. Step 5 assumes that prevention is in place and group containment is acyclic. This step does not implement cycle prevention or choose repair behavior.

`FK_pb_groups_pb_groups_ParentGroupId` still orders deletion: an included child group is deleted before its included parent.

System-relationship cycles do not create that kind of order. The foreign keys run from the relationship rows to `pb_systems`, not from one system row to another. Deleting the included relationship rows before the included systems satisfies those constraints.

### Concurrency

Later implementation must isolate temporary tables and other working state between simultaneous executions. Inventory and deletion must remain consistent when concurrent writes occur. That consistency requirement covers foreign-key associations and logical or polymorphic associations. Concrete transaction, locking, isolation, temporary-table lifetime, and cleanup mechanisms remain for implementation review. No mechanism is selected here.

### Component structure

Later implementation must use small, independently verifiable components composed into the deletion workflow, following the approach used for tree rendering. Each component must define its inputs, outputs, dependencies, and concurrency behavior. The implementation must not be one monolithic deletion SQL script.

### What the later inventory must confirm

The order above is reusable. For the selected account, the inventory still has to show which included primary keys exist, which members have a null `SystemId`, and which included groups reference one another through `ParentGroupId`. An empty stage deletes nothing. Those results do not change the foreign-key order. They show which stages have rows and which included child groups precede their included parents.

## Step 6 — Process and evidence

Step 6 connects the settled requirements. It does not add an inclusion rule, a delete stage, or an implementation.

The dependency map supplies the schema version, keys, foreign keys, and logical references. The inventory applies those paths to one supplied `AccountId` and separates included records, references, retained audit rows, and deferred rows. Account isolation limits that inventory to the supplied account. Account protection stays outside the investigation. The deletion order then deletes the included records under the map’s `NO ACTION` foreign keys, with the assumptions and deferrals below.

Supporting evidence is the catalog script named above and its SHA-256. Foreign-key names and line numbers are in the dependency map. `pb_account_code_delivery_outbox.AccountId` is included from that column and from `AccountCodeDelivery.cs`, which inserts it. Visibility `inherit` values and the one-target check on `pb_member_visibility_permissions` are catalog check constraints. No account has been selected, and no database has been read.

Where the dependency map says a row with no account column has an unresolved account association, the settled inclusion rules in this document govern. The map remains the schema record. It does not replace Steps 2 through 5.

### Deferred implementation dependencies

- The six audit foreign keys listed under Retained audit records still exist and have not been changed. A later schema change must adjust them so retained audit rows and their original identifiers can outlive the account, member, membership, or system. Audit contents stay unchanged. No adjustment mechanism is chosen.
- Null-account `pb_account_codes` stay untouched. Their handling belongs to the code-management process.
- `pb_source_id_map`, `pb_import_batches`, and `pb_source_records` stay deferred until imports are addressed.
- Preventing cycles in `pb_groups.ParentGroupId` is a required work item documented separately. Deletion order assumes that prevention is in place and containment is acyclic. Included child groups are deleted before their included parents.
- Concurrent executions must isolate temporary tables and other working state. Inventory and deletion must stay consistent for foreign-key and logical or polymorphic associations. Transaction, locking, isolation, lifetime, and cleanup mechanisms remain for implementation review.
- Implementation must use small, independently verifiable components composed into the workflow, following the approach used for tree rendering. Each component defines its inputs, outputs, dependencies, and concurrency behavior. It must not be one monolithic deletion SQL script.

A live catalog comparison against the recorded file hash is still deferred. `CustomFieldsController.cs` and `MeController.cs` select `pb_custom_fields.SystemId`, and this catalog has no such column. That code mismatch is a deferred note. It is not an inclusion path.
