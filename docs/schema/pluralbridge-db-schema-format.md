# PluralBridge Working DB Schema Format (Observed)

## Scope

This documents the working SQL schema for:

- `PluralBridgeChap2SafeSpine`

Source snapshot came from the exported schema script you provided (`script.sql`).

## Record Tree Key

- 🟢 **Core table**: primary domain table.
- 🟠 **Link/join table**: relationship/association table.
- 🟡 **Constraint note**: table participates in an important policy/model caveat.

## Schema Tree

```text
PluralBridgeChap2SafeSpine
├─ Account + Auth
│  ├─ 🟢 pb_accounts
│  ├─ 🟢 pb_account_statuses
│  ├─ 🟢 pb_account_credentials
│  ├─ 🟢 pb_account_codes
│  ├─ 🟢 pb_account_code_delivery_outbox
│  └─ 🟢 pb_account_audit_events
├─ System + Membership + Roles
│  ├─ 🟢 pb_systems
│  ├─ 🟢 pb_system_memberships
│  ├─ 🟢 pb_system_membership_statuses
│  ├─ 🟢 pb_roles
│  ├─ 🟠 pb_system_membership_roles
│  └─ 🟡 pb_system_relationships (single-root index conflict with multi-root goal)
├─ Member + Fronting + Privacy
│  ├─ 🟢 pb_members
│  ├─ 🟢 pb_front_history
│  ├─ 🟢 pb_custom_fields
│  ├─ 🟢 pb_privacy_buckets
│  ├─ 🟢 pb_visibility_scopes
│  └─ 🟠 pb_visibility_scope_members
├─ Groups
│  ├─ 🟢 pb_groups
│  └─ 🟠 pb_group_members
├─ Import + Source Fidelity
│  ├─ 🟢 pb_source_systems
│  ├─ 🟢 pb_import_batches
│  ├─ 🟢 pb_source_records
│  └─ 🟠 pb_source_id_map
└─ Member Write Audit
   └─ 🟢 pb_member_write_audit
```

## Table Types

### [🟢] pb_account_audit_events

| Field | Type | Description |
|---|---|---|
| `AccountAuditEventId` | uniqueidentifier | Audit event identifier (PK). |
| `CreatedAtUtc` | datetime2(3) | Event timestamp. |
| `EventName` | nvarchar(100) | Event name constrained by allowed values. |
| `Outcome` | nvarchar(30) | Outcome code (`succeeded`, `rejected`, etc.). |
| `ReasonCode` | nvarchar(80) | Reason code (`validation_failed`, etc.). |
| `ActorAccountId` | uniqueidentifier | Acting account (nullable FK). |
| `TargetAccountId` | uniqueidentifier | Target account (nullable FK). |
| `SystemId` | uniqueidentifier | Related system (nullable). |
| `MembershipId` | uniqueidentifier | Related membership (nullable). |
| `CorrelationId` | nvarchar(100) | Correlation key for operation traceability. |
| `Source` | nvarchar(30) | Source channel (`api`, `browser`, etc.). |
| `SafeSubject` | nvarchar(100) | Safe subject label. |
| `SafeDetailJson` | nvarchar(max) | Safe JSON detail payload (JSON constrained). |
| `SchemaVersion` | int | Audit schema version (currently fixed to 1). |

### [🟢] pb_account_code_delivery_outbox

| Field | Type | Description |
|---|---|---|
| `OutboxId` | uniqueidentifier | Outbox row identifier (PK). |
| `CreatedAtUtc` | datetime2(7) | Row creation timestamp. |
| `AccountId` | uniqueidentifier | Account receiving code. |
| `CodePurpose` | nvarchar(100) | Purpose (`registration_verification`, etc.). |
| `DestinationType` | nvarchar(50) | Destination kind (email). |
| `DestinationNormalized` | nvarchar(320) | Normalized destination value. |
| `PlaintextCode` | nvarchar(50) | Test/development plaintext code. |
| `CorrelationId` | nvarchar(100) | Correlation key. |
| `ConsumedForTestAtUtc` | datetime2(7) | Marker for test consumption (nullable). |

### [🟢] pb_account_codes

| Field | Type | Description |
|---|---|---|
| `AccountCodeId` | uniqueidentifier | Account code identifier (PK). |
| `AccountId` | uniqueidentifier | Account reference (nullable FK). |
| `CodePurpose` | nvarchar(50) | Code purpose (constrained set). |
| `DestinationType` | nvarchar(50) | Destination type (email). |
| `DestinationNormalized` | nvarchar(320) | Normalized destination value. |
| `CodeHash` | varbinary(256) | Hashed code value. |
| `CodeHashAlgorithm` | nvarchar(50) | Hash algorithm. |
| `CodeHashVersion` | int | Hash version. |
| `ExpiresAtUtc` | datetime2(3) | Code expiration timestamp. |
| `ConsumedAtUtc` | datetime2(3) | Consumption timestamp (nullable). |
| `AttemptCount` | int | Attempt counter. |
| `MaxAttempts` | int | Maximum allowed attempts. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `IssuedAtUtc` | datetime2(3) | Code issuance timestamp. |
| `LastAttemptAtUtc` | datetime2(3) | Last attempt timestamp (nullable). |
| `CorrelationId` | nvarchar(100) | Correlation key. |

### [🟢] pb_account_credentials

| Field | Type | Description |
|---|---|---|
| `AccountCredentialId` | uniqueidentifier | Credential row identifier (PK). |
| `AccountId` | uniqueidentifier | Account FK (unique index: one credential row per account). |
| `PasswordHash` | varbinary(256) | Password hash bytes. |
| `PasswordHashAlgorithm` | nvarchar(50) | Hash algorithm name. |
| `PasswordHashVersion` | int | Hash version. |
| `PasswordChangedAtUtc` | datetime2(3) | Password change timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟢] pb_account_statuses

| Field | Type | Description |
|---|---|---|
| `AccountStatusId` | int | Account status identifier (PK). |
| `StatusName` | nvarchar(64) | Status name (unique). |
| `StatusDesc` | nvarchar(500) | Status description. |
| `DisplayOrder` | int | Display/order rank (positive). |
| `IsActive` | bit | Whether status is active. |

### [🟢] pb_accounts

| Field | Type | Description |
|---|---|---|
| `AccountId` | uniqueidentifier | Account identifier (PK). |
| `Email` | nvarchar(320) | Account email (unique, non-blank). |
| `DisplayName` | nvarchar(255) | Account display name (nullable). |
| `AccountStatusId` | int | Account status FK. |
| `CreatedAtUtc` | datetime2(3) | Creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |
| `Username` | nvarchar(100) | Username (nullable). |
| `NormalizedUsername` | nvarchar(100) | Normalized username (nullable, unique filtered). |
| `NormalizedEmail` | nvarchar(320) | Normalized email (nullable, unique filtered). |
| `IsEmailVerified` | bit | Email verification state. |
| `LastLoginAtUtc` | datetime2(3) | Last login timestamp (nullable). |

### [🟢] pb_custom_fields

| Field | Type | Description |
|---|---|---|
| `CustomFieldId` | uniqueidentifier | Custom field identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `FieldName` | nvarchar(510) | Field display name. |
| `Description` | nvarchar(max) | Field description (nullable). |
| `FieldTypeCode` | int | Field type code (nullable). |
| `DisplayOrderText` | nvarchar(128) | Source/order text (nullable). |
| `SupportsMarkdown` | bit | Markdown support flag (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟢] pb_front_history

| Field | Type | Description |
|---|---|---|
| `FrontHistoryId` | uniqueidentifier | Front history row identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `MemberId` | uniqueidentifier | Member FK. |
| `StartTimeMs` | bigint | Start timestamp (ms). |
| `EndTimeMs` | bigint | End timestamp (ms, nullable). |
| `IsLive` | bit | Live/active flag (nullable). |
| `IsCustom` | bit | Custom front flag (nullable). |
| `CustomStatus` | nvarchar(510) | Custom front status text (nullable). |
| `LastOperationTimeMs` | bigint | Source operation timestamp (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟠] pb_group_members

| Field | Type | Description |
|---|---|---|
| `GroupMemberId` | uniqueidentifier | Group-member link identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `GroupId` | uniqueidentifier | Group FK. |
| `MemberId` | uniqueidentifier | Member FK. |
| `SourceGroupId` | nvarchar(128) | Source group identifier (nullable). |
| `SourceMemberId` | nvarchar(128) | Source member identifier (nullable). |
| `CreatedAtUtc` | datetime2(3) | Link creation timestamp. |

### [🟢] pb_groups

| Field | Type | Description |
|---|---|---|
| `GroupId` | uniqueidentifier | Group identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `SourceGroupId` | nvarchar(128) | Source group identifier (nullable). |
| `ParentGroupId` | uniqueidentifier | Parent group FK (nullable). |
| `ParentSourceGroupId` | nvarchar(128) | Parent source group id (nullable). |
| `GroupName` | nvarchar(200) | Group display name. |
| `GroupColor` | nvarchar(32) | Group color (nullable). |
| `GroupDesc` | nvarchar(max) | Group description (nullable). |
| `GroupEmoji` | nvarchar(64) | Group emoji (nullable). |
| `SupportsDescMarkdown` | bit | Markdown support flag. |
| `SourceExists` | bit | Source-existence marker. |
| `LastOperationTimeUnixMs` | bigint | Source operation timestamp (nullable). |
| `LastOperationAtUtc` | datetime2(3) | Source operation timestamp UTC (nullable). |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp. |

### [🟢] pb_import_batches

| Field | Type | Description |
|---|---|---|
| `ImportBatchId` | uniqueidentifier | Import batch identifier (PK). |
| `SourceSystemCode` | nvarchar(64) | Source system code FK. |
| `ImportStartedAtUtc` | datetime2(3) | Import start timestamp. |
| `ImportCompletedAtUtc` | datetime2(3) | Import completion timestamp (nullable). |
| `ImportToolName` | nvarchar(510) | Import tool name (nullable). |
| `ImportToolVersion` | nvarchar(128) | Import tool version (nullable). |
| `SourceExportName` | nvarchar(1000) | Source export name (nullable). |
| `SourceExportSha256` | varbinary(32) | Source export hash (nullable). |
| `Notes` | nvarchar(max) | Import notes (nullable). |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |

### [🟢] pb_member_write_audit

| Field | Type | Description |
|---|---|---|
| `MemberWriteAuditId` | uniqueidentifier | Member write audit row id (PK). |
| `SystemId` | uniqueidentifier | System FK. |
| `AccountId` | uniqueidentifier | Account FK. |
| `SystemMembershipId` | uniqueidentifier | Membership FK (nullable). |
| `MemberId` | uniqueidentifier | Member FK. |
| `Operation` | nvarchar(32) | Operation (`member.add` or `member.edit`). |
| `RequestTraceId` | nvarchar(100) | Request trace/correlation value (nullable). |
| `CreatedAtUtc` | datetime2(7) | Audit timestamp. |

### [🟢] pb_members

| Field | Type | Description |
|---|---|---|
| `MemberId` | uniqueidentifier | Member identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `DisplayName` | nvarchar(510) | Member display name. |
| `Pronouns` | nvarchar(510) | Pronouns text (nullable). |
| `Description` | nvarchar(max) | Description text (nullable). |
| `Color` | nvarchar(64) | Color value (nullable). |
| `IsArchived` | bit | Archived flag (nullable). |
| `ArchivedReason` | nvarchar(max) | Archive reason text (nullable). |
| `IsPrivate` | bit | Private visibility flag (nullable). |
| `PreventTrusted` | bit | Prevent trusted visibility flag (nullable). |
| `PreventsFrontNotifications` | bit | Front notification suppression flag (nullable). |
| `ReceiveMessageBoardNotifications` | bit | Message board notification flag (nullable). |
| `SupportsDescriptionMarkdown` | bit | Markdown support flag (nullable). |
| `LastOperationTimeMs` | bigint | Source operation timestamp (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟢] pb_privacy_buckets

| Field | Type | Description |
|---|---|---|
| `PrivacyBucketId` | uniqueidentifier | Privacy bucket identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `BucketName` | nvarchar(510) | Bucket display name. |
| `Description` | nvarchar(max) | Bucket description (nullable). |
| `Color` | nvarchar(64) | Bucket color (nullable). |
| `Icon` | nvarchar(510) | Bucket icon (nullable). |
| `RankText` | nvarchar(128) | Source rank/order text (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟢] pb_roles

| Field | Type | Description |
|---|---|---|
| `RoleId` | int | Role identifier (PK). |
| `RoleName` | nvarchar(64) | Role name (unique). |
| `RoleDesc` | nvarchar(500) | Role description. |
| `DisplayOrder` | int | Role display order (positive). |
| `IsActive` | bit | Role active flag. |

### [🟠] pb_source_id_map

| Field | Type | Description |
|---|---|---|
| `SourceIdMapId` | uniqueidentifier | Mapping row identifier (PK). |
| `SourceSystemCode` | nvarchar(64) | Source system code FK. |
| `SourceEntityTypeCode` | nvarchar(128) | Source entity type code. |
| `SourceId` | nvarchar(256) | Source entity id. |
| `PluralBridgeEntityTypeCode` | nvarchar(128) | PB entity type code. |
| `PluralBridgeId` | uniqueidentifier | PB entity id. |
| `ImportBatchId` | uniqueidentifier | Import batch FK. |
| `CreatedAtUtc` | datetime2(3) | Mapping creation timestamp. |

### [🟢] pb_source_records

| Field | Type | Description |
|---|---|---|
| `SourceRecordId` | uniqueidentifier | Source record identifier (PK). |
| `ImportBatchId` | uniqueidentifier | Import batch FK. |
| `SourceSystemCode` | nvarchar(64) | Source system code FK. |
| `SourceEntityTypeCode` | nvarchar(128) | Source entity type code. |
| `SourceId` | nvarchar(256) | Source record id (nullable). |
| `SourceEndpoint` | nvarchar(2000) | Source endpoint/route (nullable). |
| `RawJson` | nvarchar(max) | Raw source JSON payload (nullable). |
| `RawJsonSha256` | varbinary(32) | Raw JSON hash (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |

### [🟢] pb_source_systems

| Field | Type | Description |
|---|---|---|
| `SourceSystemCode` | nvarchar(64) | Source system code (PK). |
| `DisplayName` | nvarchar(510) | Source system display name. |
| `Description` | nvarchar(max) | Source description (nullable). |
| `ApiBaseUrl` | nvarchar(2000) | Source API base URL (nullable). |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |

### [🟠] pb_system_membership_roles

| Field | Type | Description |
|---|---|---|
| `SystemMembershipId` | uniqueidentifier | Membership FK (composite PK part). |
| `RoleId` | int | Role FK (composite PK part). |
| `CreatedAtUtc` | datetime2(3) | Assignment timestamp. |

### [🟢] pb_system_membership_statuses

| Field | Type | Description |
|---|---|---|
| `MembershipStatusId` | int | Membership status identifier (PK). |
| `StatusName` | nvarchar(64) | Status name (unique). |
| `StatusDesc` | nvarchar(500) | Status description. |
| `DisplayOrder` | int | Display/order rank (positive). |
| `IsActive` | bit | Status active flag. |

### [🟢] pb_system_memberships

| Field | Type | Description |
|---|---|---|
| `SystemMembershipId` | uniqueidentifier | Membership identifier (PK). |
| `AccountId` | uniqueidentifier | Account FK. |
| `SystemId` | uniqueidentifier | System FK. |
| `MembershipStatusId` | int | Membership status FK. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟡] pb_system_relationships

| Field | Type | Description |
|---|---|---|
| `SystemRelationshipId` | uniqueidentifier | Relationship row identifier (PK). |
| `SystemId` | uniqueidentifier | Child/subject system FK (unique per system). |
| `ParentSystemId` | uniqueidentifier | Parent system FK (nullable). |
| `RelationshipRank` | int | Relationship ordering rank (>=1). |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp. |

Constraint note:

- Filtered unique index `UX_pb_system_relationships_SingleRoot` on `ParentSystemId IS NULL` enforces only one root row at a time, conflicting with planned multi-root support.

### [🟢] pb_systems

| Field | Type | Description |
|---|---|---|
| `SystemId` | uniqueidentifier | System identifier (PK). |
| `SystemName` | nvarchar(510) | System name (nullable). |
| `Description` | nvarchar(max) | System description (nullable). |
| `Color` | nvarchar(64) | System color (nullable). |
| `AvatarUrl` | nvarchar(2000) | System avatar URL (nullable). |
| `AvatarUuid` | nvarchar(128) | System avatar reference (nullable). |
| `SourceCreatedAtMs` | bigint | Source creation timestamp (nullable). |
| `LastOperationTimeMs` | bigint | Source operation timestamp (nullable). |
| `ImportedAtUtc` | datetime2(3) | Import timestamp. |
| `CreatedAtUtc` | datetime2(3) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(3) | Last update timestamp (nullable). |

### [🟠] pb_visibility_scope_members

| Field | Type | Description |
|---|---|---|
| `VisibilityScopeMemberId` | uniqueidentifier | Scope-member link identifier (PK). |
| `VisibilityScopeId` | uniqueidentifier | Visibility scope FK. |
| `MemberId` | uniqueidentifier | Member FK. |
| `CreatedAtUtc` | datetime2(7) | Link creation timestamp. |

### [🟢] pb_visibility_scopes

| Field | Type | Description |
|---|---|---|
| `VisibilityScopeId` | uniqueidentifier | Visibility scope identifier (PK). |
| `SystemId` | uniqueidentifier | Owning system FK. |
| `ScopeName` | nvarchar(100) | Scope name (unique per system). |
| `ScopeDesc` | nvarchar(500) | Scope description (nullable). |
| `IsSystemDefault` | bit | System default scope flag. |
| `IsActive` | bit | Scope active flag. |
| `CreatedAtUtc` | datetime2(7) | Row creation timestamp. |
| `UpdatedAtUtc` | datetime2(7) | Last update timestamp (nullable). |

## Notes

- This file is a format reference, not a migration script.
- Use this with import/recovery docs and domain guardrails to avoid conflating source shape with authorization semantics.
