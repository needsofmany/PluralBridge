# Account deletion dependency map

Step 1 only. This map records tables and dependencies relevant to deleting one account and its data. It does not select an account, inventory rows, or decide a deletion order.

Repository text is not a reading of the live database.

Accounts do not share data. A membership, placement, or relationship row belongs to the account named on that row. Another account storing the same system or object id is a second reference. It is not a sharing relationship. Where the referenced row has no account column, that row’s account association stays unresolved.

## Schema state

| Item | Value |
|---|---|
| Catalog script | `scripts/sqlserver/PB-DB-backup-112226.sql` |
| Script header | `Script Date: 11/22/2026 5:01:40 AM`, database `PluralBridgeChap2SafeSpine` |
| SHA-256 | `DAAAA0CA71FFDE5B7E843BE794A52D3787DFF898E813122D5DB7227E36E16DB1` |
| How to recheck | Hash that file. Re-read this map when the hash changes. The repository has no migration-history table. |

`docs/schema/pluralbridge-db-schema-format.md` identifies an earlier state: the 13 November 2026 catalog plus `scripts/sqlserver/Members/001`, `002`, and `003`. The 22 November catalog already contains those changes and later tables. `Members/007`, `008`, and `009` create temporary procedure tables only. They do not add a permanent table.

The catalog script contains 44 `dbo.pb_*` tables. It contains no `ON DELETE`, no `ON UPDATE`, no view, no function, and no trigger. Every foreign key below is specified as `NO ACTION`.

## Tables and primary keys

All primary keys are in `scripts/sqlserver/PB-DB-backup-112226.sql`.

| Table | Primary key | Line |
|---|---|---|
| `pb_accounts` | `AccountId` | 224 |
| `pb_account_statuses` | `AccountStatusId` | 197 |
| `pb_account_credentials` | `AccountCredentialId` | 139 |
| `pb_account_codes` | `AccountCodeId` | 119 |
| `pb_account_code_delivery_outbox` | `OutboxId` | 91 |
| `pb_account_audit_events` | `AccountAuditEventId` | 70 |
| `pb_account_object_links` | `AccountObjectLinkId` | 158 |
| `pb_account_object_placements` | `AccountObjectPlacementId` | 180 |
| `pb_anchor_visibility_permissions` | `AnchorVisibilityPermissionId` | 275 |
| `pb_custom_fields` | `CustomFieldId` | 297 |
| `pb_object_memberships` | `ObjectMembershipId` | 612 |
| `pb_object_membership_roles` | `(ObjectMembershipId, RoleId)` | 592 |
| `pb_system_memberships` | `SystemMembershipId` | 821 |
| `pb_system_membership_roles` | `(SystemMembershipId, RoleId)` | 781 |
| `pb_system_membership_statuses` | `MembershipStatusId` | 799 |
| `pb_roles` | `RoleId` | 698 |
| `pb_places` | `PlaceId` | 659 |
| `pb_place_members` | `PlaceMemberId` | 636 |
| `pb_member_write_audit` | `MemberWriteAuditId` | 508 |
| `pb_member_visibility_permissions` | `MemberVisibilityPermissionId` | 486 |
| `pb_systems` | `SystemId` | 936 |
| `pb_groups` | `GroupId` | 407 |
| `pb_group_members` | `GroupMemberId` | 357 |
| `pb_group_system_contexts` | `GroupSystemContextId` | 380 |
| `pb_members` | `MemberId` | 538 |
| `pb_member_avatars` | `MemberId` | 448 |
| `pb_member_system_contexts` | `MemberSystemContextId` | 465 |
| `pb_notes` | `NoteId` | 577 |
| `pb_privacy_buckets` | `PrivacyBucketId` | 681 |
| `pb_front_history` | `FrontHistoryId` | 321 |
| `pb_front_history_members` | `FrontHistoryMemberId` | 338 |
| `pb_model_provenance` | `ModelProvenanceId` | 557 |
| `pb_visibility_scopes` | `VisibilityScopeId` | 977 |
| `pb_visibility_scope_members` | `VisibilityScopeMemberId` | 952 |
| `pb_visibility_types` | `VisibilityTypeId` | 998 |
| `pb_anchor_relationships` | `AnchorRelationshipId` | 248 |
| `pb_system_relationships` | `SystemRelationshipId` | 909 |
| `pb_system_relationship_edges` | `SystemRelationshipEdgeId` | 885 |
| `pb_system_object_links` | `SystemObjectLinkId` | 839 |
| `pb_system_object_placements` | `SystemObjectPlacementId` | 861 |
| `pb_import_batches` | `ImportBatchId` | 429 |
| `pb_source_records` | `SourceRecordId` | 749 |
| `pb_source_id_map` | `SourceIdMapId` | 722 |
| `pb_source_systems` | `SourceSystemCode` | 766 |

`pb_account_statuses`, `pb_roles`, `pb_system_membership_statuses`, `pb_visibility_types`, and `pb_source_systems` are lookup tables. Account rows reference them. They have no account column.

## Verified direct account references

| From | Column | Constraint | Line | Action |
|---|---|---|---|---|
| `pb_account_audit_events` | `ActorAccountId` | `FK_pb_account_audit_events_ActorAccount` | 1638 | NO ACTION, column nullable |
| `pb_account_audit_events` | `TargetAccountId` | `FK_pb_account_audit_events_TargetAccount` | 1643 | NO ACTION, column nullable |
| `pb_account_codes` | `AccountId` | `FK_pb_account_codes_pb_accounts` | 1648 | NO ACTION, column nullable |
| `pb_account_credentials` | `AccountId` | `FK_pb_account_credentials_pb_accounts` | 1653 | NO ACTION |
| `pb_account_object_links` | `AccountId` | `FK_pb_account_object_links_pb_accounts_AccountId` | 1658 | NO ACTION |
| `pb_account_object_placements` | `AccountId` | `FK_pb_account_object_placements_pb_accounts_AccountId` | 1663 | NO ACTION |
| `pb_anchor_visibility_permissions` | `AccountId` | `FK_pb_anchor_visibility_permissions_pb_accounts` | 1673 | NO ACTION |
| `pb_custom_fields` | `AccountId` | `FK_pb_custom_fields_AccountId` | 1698 | NO ACTION |
| `pb_member_write_audit` | `AccountId` | `FK_pb_member_write_audit_accounts` | 1798 | NO ACTION |
| `pb_object_memberships` | `AccountId` | `FK_pb_object_memberships_pb_accounts_AccountId` | 1848 | NO ACTION |
| `pb_places` | `AccountId` | `FK_pb_places_AccountId` | 1868 | NO ACTION |
| `pb_system_memberships` | `AccountId` | `FK_pb_system_memberships_pb_accounts` | 1908 | NO ACTION |
| `pb_accounts` | `AccountStatusId` | `FK_pb_accounts_pb_account_statuses` | 1668 | NO ACTION, lookup |

`pb_account_code_delivery_outbox.AccountId` is `NOT NULL` at line 84 and has no foreign key. `api/PluralBridge.Api/PluralBridge.Api/Account/AccountCodeDelivery.cs` lines 52–66 insert that column. The association is verified in code and schema. The constraint is not.

`pb_accounts.Email` is unique at line 228. `pb_show_tree_step1_roots_by_email` lines 2666–2668 resolve an account from that email. That is a procedure lookup, not a second owner column.

## Verified indirect foreign keys

These reference a row that already has an account column. They do not assign the further target, such as a member or system, to the account.

| Child | Column | Parent | Constraint | Line | Action |
|---|---|---|---|---|---|
| `pb_object_membership_roles` | `ObjectMembershipId` | `pb_object_memberships` | `FK_pb_object_membership_roles_pb_object_memberships` | 1838 | NO ACTION |
| `pb_object_membership_roles` | `RoleId` | `pb_roles` | `FK_pb_object_membership_roles_pb_roles` | 1843 | NO ACTION, lookup |
| `pb_system_membership_roles` | `SystemMembershipId` | `pb_system_memberships` | `FK_pb_system_membership_roles_pb_system_memberships` | 1903 | NO ACTION |
| `pb_system_membership_roles` | `RoleId` | `pb_roles` | `FK_pb_system_membership_roles_pb_roles` | 1898 | NO ACTION, lookup |
| `pb_place_members` | `PlaceId` | `pb_places` | `FK_pb_place_members_PlaceId` | 1863 | NO ACTION |
| `pb_place_members` | `MemberId` | `pb_members` | `FK_pb_place_members_MemberId` | 1858 | NO ACTION |
| `pb_member_visibility_permissions` | `PlaceMemberId` | `pb_place_members` | `FK_pb_member_visibility_permissions_pb_place_members` | 1788 | NO ACTION, column nullable |
| `pb_anchor_visibility_permissions` | `PlaceId` | `pb_places` | `FK_pb_anchor_visibility_permissions_pb_places` | 1683 | NO ACTION, column nullable |
| `pb_member_write_audit` | `SystemMembershipId` | `pb_system_memberships` | `FK_pb_member_write_audit_system_memberships` | 1808 | NO ACTION, column nullable |
| `pb_member_write_audit` | `MemberId` | `pb_members` | `FK_pb_member_write_audit_members` | 1803 | NO ACTION |
| `pb_member_write_audit` | `SystemId` | `pb_systems` | `FK_pb_member_write_audit_systems` | 1813 | NO ACTION, column nullable |
| `pb_system_memberships` | `SystemId` | `pb_systems` | `FK_pb_system_memberships_pb_systems` | 1918 | NO ACTION |
| `pb_system_memberships` | `MembershipStatusId` | `pb_system_membership_statuses` | `FK_pb_system_memberships_pb_system_membership_statuses` | 1913 | NO ACTION, lookup |
| `pb_object_memberships` | `MembershipStatusId` | `pb_system_membership_statuses` | `FK_pb_object_memberships_pb_system_membership_statuses_MembershipStatusId` | 1853 | NO ACTION, lookup |
| `pb_anchor_visibility_permissions` | `SystemId`, `GroupId`, `VisibilityTypeId` | `pb_systems`, `pb_groups`, `pb_visibility_types` | lines 1678, 1688, 1693 | NO ACTION, system and group nullable |

`UQ_pb_object_memberships_Account_Object` at lines 616–620 is unique per account, object type, and object id. It allows another account to store the same type and id. `pb_system_memberships` has no unique key on `(AccountId, SystemId)`, so the catalog also allows more than one membership row for the same pair and more than one account id on the same `SystemId`. Those are separate account-owned rows. They do not make the system shared.

## Logical and polymorphic references

These are verified stored references. The target row is not established as account-owned.

| Source | Reference | Enforcement | Location |
|---|---|---|---|
| `pb_account_object_links` | `ObjectType`, `ObjectId` | Check allows `visibilityScope`, `privacyBucket`, `customField`, `frontHistory`, `note`, `place`, `group`, `system`, `member`. No foreign key to those tables. | lines 150–156, check at 2022 |
| `pb_account_object_placements` | parent and child type plus id | Child types match the link check. Parent types are `member`, `place`, `group`, `system`, `account`. One row cannot name itself. No target foreign key. | lines 169–176, checks at 2026–2034 |
| `pb_object_memberships` | `ObjectType`, `ObjectId` | Both must be non-blank. No type list and no target foreign key. | lines 607–608, checks at 2118–2122 |
| `pb_account_audit_events` | `SystemId`, `MembershipId` | Columns exist. No foreign key. | lines 63–64; absent from the foreign-key block |
| `pb_member_write_audit` | `AnchorObjectType`, `AnchorObjectId` | Columns exist. No foreign key. `AnchorMemberProfileSave.cs` writes `AnchorObjectType`. | backup lines 506–507; save call sites beginning at line 363 |
| `pb_account_codes` | `DestinationNormalized` when `AccountId` is null | No account foreign key on that row. | lines 104–107 |
| `pb_source_id_map` | `PluralBridgeEntityTypeCode`, `PluralBridgeId` | Foreign keys point at the import batch and source system only, lines 1878 and 1883. No foreign key to the PluralBridge row. | lines 718–719 |
| `MeController.ReadCountsAsync` | map id converted and compared with system ids | The query does not filter `PluralBridgeEntityTypeCode`. | `MeController.cs` lines 1401 and 1439–1444 |

`pb_show_tree_step1_roots_by_email` lines 2678–2695 reads placements whose parent type is `account` and whose child type is `group`, `system`, or `place`. Its comment says active system memberships are a fallback when those placement roots are absent. That is a read procedure. It does not add an owner column to `pb_systems`, `pb_groups`, or `pb_places`.

## Referenced tables with no account column

These foreign keys are verified. Their starting point is a system, member, group, or place id. Reaching them from an account requires one of the unresolved references above.

| Child | Parent | Constraint line |
|---|---|---|
| `pb_groups` | `pb_groups` through `ParentGroupId`; `pb_systems` through nullable `SystemId` | 1748, 1753 |
| `pb_group_members` | `pb_groups`, `pb_members`, nullable `pb_systems` | 1723, 1728, 1733 |
| `pb_group_system_contexts` | `pb_groups`, `pb_systems` | 1738, 1743 |
| `pb_members` | nullable `pb_systems`; nullable `pb_visibility_types` | 1818, 1823 |
| `pb_member_avatars` | `pb_members` | 1763 |
| `pb_member_system_contexts` | `pb_members`, `pb_systems` | 1768, 1773 |
| `pb_member_visibility_permissions` | nullable `pb_group_members`, nullable `pb_members`, nullable `pb_visibility_types` | 1778, 1783, 1793 |
| `pb_notes`, `pb_privacy_buckets`, `pb_visibility_scopes` | `pb_systems` | 1833, 1873, 1973 |
| `pb_model_provenance` | nullable `pb_systems` | 1828 |
| `pb_front_history` | `pb_members`; nullable `pb_systems` | 1703, 1708 |
| `pb_front_history_members` | `pb_front_history`, `pb_members` | 1713, 1718 |
| `pb_visibility_scope_members` | `pb_members`, `pb_visibility_scopes` | 1963, 1968 |
| `pb_system_object_links`, `pb_system_object_placements` | `pb_systems` only | 1923, 1928 |
| `pb_system_relationships`, `pb_system_relationship_edges` | `pb_systems` on `SystemId`, nullable `ParentSystemId`, nullable `TargetSystemId` | 1933–1958 |
| `pb_import_batches`, `pb_source_records` | `pb_source_systems` and, for records, `pb_import_batches` | 1758, 1888, 1893 |

`pb_system_object_links.ObjectType` and `ObjectId`, and `pb_system_object_placements` parent and child type and id, have indexes and no type check. `AssociatedObjectType` and `AssociatedObjectId` on both relationship tables, lines 880–881 and 907–908, have no foreign key. The catalog procedures and the API search do not reference `pb_system_object_links`, `pb_system_object_placements`, or `pb_anchor_relationships`. `pb_anchor_relationships` stores source and target type and id, is unique on the source, and has no foreign key, lines 239–256.

## Cycles and unresolved items

- `pb_groups.ParentGroupId` is a self-reference, line 1748. The schema permits a cycle.
- `pb_system_relationships` and `pb_system_relationship_edges` reference `pb_systems` from three columns. The schema permits a cycle among systems. No account data was read, so no actual cycle is claimed.
- Placement checks forbid a row whose parent and child are the same value. They do not forbid a cycle across several rows.
- `pb_custom_fields` has `AccountId` and no `SystemId`, lines 286–296. `CustomFieldsController.cs` lines 173–183 and `MeController.cs` line 1436 select `pb_custom_fields.SystemId`. That code path does not match this catalog.
- `pb_model_provenance.SystemId` and `pb_members.SystemId` are nullable. A null value leaves no system path.
- `pb_member_visibility_permissions` allows one of `MemberId`, `GroupMemberId`, or `PlaceMemberId` on a row. The check constraint is `CK_pb_member_visibility_permissions_Target`. That column has no account column.
- Import batches and source records have no account column. The source-map guid is not a verified account path.
- Null `AccountId` on `pb_account_codes` is not an account path.

## Evidence still needed

- A live catalog comparison when someone chooses to connect. This file hash does not prove the deployed database.
- Whether rows exist for the unreferenced tables and polymorphic ids. That is later inventory, not this map.

Deletion order, deletion SQL, account protection, authorization, API, recovery, and session policy are outside this map.
