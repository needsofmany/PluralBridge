# Simply Plural Export JSON Format (Observed)

## Scope

This documents the **official Simply Plural export JSON** shape as observed from the inferred schema source:

- `simply_plural_last_export.inferred.schema.json`

This is a source-shape reference for import planning, not a PluralBridge ontology.

## Record Tree Key

- 🟢 **Documented record type**: item shape is documented in this file with a 3-column table.
- 🟡 **Observed but variant/special case**: known oddity such as misspelling or duplicated naming pattern.
- 🟠 **Known container/sub-structure**: nested object exists but is not a top-level record type table.
- 🔴 **Unknown item shape**: collection exists but item fields were not available from observed sample data.

## Record Tree

```text
Export (object)
├─ 🔴 automatedReminders[] (unknown item shape)
├─ 🟢 avatarExports[] -> avatarExportsItem
├─ 🟢 boardMessages[] -> boardMessagesItem
├─ 🔴 cancelFeedback[] (unknown item shape)
├─ 🟢 channelCategories[] -> channelCategoriesItem
├─ 🟢 channels[] -> channelsItem
├─ 🟢 chatMessages[] -> chatMessagesItem
├─ 🔴 comments[] (unknown item shape)
├─ 🟢 customFields[] -> customFieldsItem
├─ 🟢 dataExports[] -> dataExportsItem
├─ 🔴 events[] (unknown item shape)
├─ 🟢 friends[] -> friendsItem
├─ 🟢 frontHistory[] -> frontHistoryItem
├─ 🟢 frontStatuses[] -> frontStatusesItem
├─ 🔴 fronters[] (unknown item shape)
├─ 🟢 groups[] -> groupsItem
├─ 🔴 invalidJwtTokens[] (unknown item shape)
├─ 🟢 members[] -> membersItem
├─ 🔴 messages[] (unknown item shape)
├─ 🟢 notes[] -> notesItem
├─ 🔴 notifications[] (unknown item shape)
├─ 🔴 pendingFriendRequests[] (unknown item shape)
├─ 🔴 polls[] (unknown item shape)
├─ 🔴 potato[] (unknown item shape)
├─ 🟢 privacyBuckets[] -> privacyBucketsItem
├─ 🟢 private[] -> privateItem
│  └─ 🟠 defaultPrivacySettings (object)
│     ├─ 🔴 customFields[] (unknown item shape)
│     ├─ 🔴 customFronts[] (unknown item shape)
│     ├─ 🔴 groups[] (unknown item shape)
│     └─ 🔴 members[] (unknown item shape)
├─ 🟢 privateFront[] -> privateFrontItem
├─ 🔴 queuedEvents[] (unknown item shape)
├─ 🟡 repeatedRemidners[] (unknown item shape, observed misspelling)
├─ 🔴 repeatedReminders[] (unknown item shape)
├─ 🟢 reports[] -> reportsItem
│  └─ 🟠 usedSettings (object)
│     ├─ 🔴 cc[] (unknown item shape)
│     ├─ 🟠 customFronts (object)
│     │  ├─ 🔴 buckets[] (unknown item shape)
│     │  ├─ includeBucketless (boolean)
│     │  └─ privacyLevel (integer)
│     ├─ 🟠 frontHistory (object)
│     │  ├─ 🔴 customFrontBuckets[] (unknown item shape)
│     │  ├─ 🔴 memberBuckets[] (unknown item shape)
│     │  ├─ start (integer)
│     │  ├─ end (integer)
│     │  ├─ includeCustomFronts (boolean)
│     │  ├─ includeCustomFrontsBucketless (boolean)
│     │  ├─ includeMembers (boolean)
│     │  ├─ includeMembersBucketless (boolean)
│     │  └─ privacyLevel (integer)
│     ├─ 🟠 members (object)
│     │  ├─ 🔴 buckets[] (unknown item shape)
│     │  ├─ includeBucketless (boolean)
│     │  ├─ includeCustomFields (boolean)
│     │  └─ privacyLevel (integer)
│     └─ sendTo (string)
├─ 🟢 securityLogs[] -> securityLogsItem
├─ 🟢 serverData[] -> serverDataItem
├─ 🟢 sharedFront[] -> sharedFrontItem
├─ 🔴 socketNotifications[] (unknown item shape)
├─ 🔴 subscribers[] (unknown item shape)
├─ 🔴 test[] (unknown item shape)
├─ 🟢 tokens[] -> tokensItem
├─ 🔴 undeliveredMessages[] (unknown item shape)
├─ 🟢 usage[] -> usageItem
├─ 🟢 users[] -> usersItem
│  ├─ 🟠 fields (object keyed by field id)
│  │  └─ 🟠 field config object
│  └─ 🟠 frame (object)
├─ 🔴 verifiedKeys[] (unknown item shape)
└─ 🔴 views[] (unknown item shape)
```

## Record Types

### [🟢] avatarExportsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `downloads` | integer | Observed download counter. |
| `exp` | integer | Expiration timestamp/value from source. |
| `key` | string | Source storage/export key. |
| `lastDownload` | integer | Last download timestamp/value. |
| `uid` | string | Source user/system identifier. |

### [🟢] boardMessagesItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `message` | string | Message body text. |
| `read` | boolean | Read/unread state. |
| `supportMarkdown` | boolean | Markdown support flag. |
| `title` | string | Message title. |
| `uid` | string | Source user/system identifier. |
| `writtenAt` | integer | Written-at timestamp/value. |
| `writtenBy` | string | Source author identifier. |
| `writtenFor` | string | Source target identifier. |

### [🟢] channelCategoriesItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `channels` | array\<string> | Channel identifiers in this category. |
| `desc` | string | Category description. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Category name. |
| `uid` | string | Source user/system identifier. |

### [🟢] channelsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `desc` | string | Channel description. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Channel name. |
| `uid` | string | Source user/system identifier. |

### [🟢] chatMessagesItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `channel` | string | Channel identifier for this message. |
| `iv` | string | Source encryption/vector-related field. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `message` | string | Message body text. |
| `uid` | string | Source user/system identifier. |
| `writer` | string | Source author identifier. |
| `writtenAt` | integer | Written-at timestamp/value. |

### [🟢] customFieldsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `buckets` | array\<string> | Linked privacy bucket identifiers. |
| `name` | string | Custom field display name. |
| `oid` | string | Source object/field identifier. |
| `order` | string | Source ordering value. |
| `supportMarkdown` | null | Observed as null in this export sample. |
| `type` | integer | Source custom field type code. |
| `uid` | string | Source user/system identifier. |

### [🟢] dataExportsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `downloads` | integer | Observed download counter. |
| `exp` | integer | Expiration timestamp/value from source. |
| `key` | string | Source storage/export key. |
| `lastDownload` | integer | Last download timestamp/value. |
| `uid` | string | Source user/system identifier. |

### [🟢] friendsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `buckets` | array\<string> | Linked privacy bucket identifiers. |
| `customFrontString` | string | Source custom front status text. |
| `frienduid` | string | Friend user/system identifier. |
| `frontNotificationString` | string | Front notification display text. |
| `frontString` | string | Front display text. |
| `getFrontNotif` | boolean | Receive front notifications flag. |
| `getTheirFrontNotif` | boolean | Receive their-front notifications flag. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `seeFront` | boolean | Visibility flag for front state. |
| `seeMembers` | boolean | Visibility flag for member data. |
| `trusted` | boolean | Trusted-relationship flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] frontHistoryItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `custom` | boolean | Custom front state flag. |
| `customStatus` | string | Custom front status text. |
| `endTime` | integer | End timestamp/value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `live` | boolean | Live/current front entry flag. |
| `member` | string | Member identifier linked to entry. |
| `startTime` | integer | Start timestamp/value. |
| `uid` | string | Source user/system identifier. |

### [🟢] frontStatusesItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `avatarUrl` | string | Avatar URL. |
| `avatarUuid` | string | Avatar UUID/reference. |
| `buckets` | array | Linked privacy bucket identifiers (observed empty). |
| `color` | string | Color value. |
| `desc` | string | Description text. |
| `frame` | object | Visual frame configuration object. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Name/title value. |
| `preventTrusted` | boolean | Trusted-visibility prevention flag. |
| `preventsFrontNotifs` | boolean | Front notification prevention flag. |
| `private` | boolean | Private visibility flag. |
| `supportDescMarkdown` | boolean | Markdown support flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] groupsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `buckets` | array | Linked privacy bucket identifiers. |
| `color` | string | Group color value. |
| `desc` | string | Group description text. |
| `emoji` | string | Group emoji value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `members` | array\<string> | Member identifiers linked to group. |
| `name` | string | Group name. |
| `parent` | string | Parent group identifier (source). |
| `supportDescMarkdown` | boolean | Markdown support flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] membersItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `archived` | boolean | Archived state flag. |
| `archivedReason` | string | Archived reason text. |
| `avatarUrl` | string | Avatar URL. |
| `avatarUuid` | string | Avatar UUID/reference. |
| `buckets` | array\<string> | Linked privacy bucket identifiers. |
| `color` | string | Member color value. |
| `desc` | string | Member description text. |
| `frame` | object | Visual frame configuration object. |
| `info` | object | Dynamic map of custom field id to value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Member display name. |
| `pkId` | string | Source primary-key-like identifier. |
| `preventTrusted` | boolean | Trusted-visibility prevention flag. |
| `preventsFrontNotifs` | boolean | Front notification prevention flag. |
| `private` | boolean | Private visibility flag. |
| `pronouns` | string | Pronoun text. |
| `receiveMessageBoardNotifs` | boolean | Message board notification flag. |
| `supportDescMarkdown` | boolean | Markdown support flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] notesItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `color` | string | Note color value. |
| `date` | integer | Note timestamp/value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `member` | string | Member identifier linked to note. |
| `note` | string | Note body text. |
| `supportMarkdown` | boolean | Markdown support flag. |
| `title` | string | Note title text. |
| `uid` | string | Source user/system identifier. |

### [🟢] privacyBucketsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `color` | string | Bucket color value. |
| `desc` | string | Bucket description text. |
| `icon` | string | Bucket icon value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Bucket name. |
| `rank` | string | Bucket ranking/order value. |
| `uid` | string | Source user/system identifier. |

### [🟢] privateItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `auditContentChanges` | boolean | Audit-content-changes flag. |
| `auditRetention` | integer | Audit retention value. |
| `categories` | array\<string> | Source category list. |
| `defaultPrivacySettings` | object | Default privacy configuration object. |
| `generationsLeft` | integer | Source generation counter. |
| `hideAudits` | boolean | Hide-audits flag. |
| `lastExport` | integer | Last export timestamp/value. |
| `lastExportAttempt` | integer | Last export attempt timestamp/value. |
| `lastGenerationReset` | integer | Last generation reset timestamp/value. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `lastRefresh` | integer | Last refresh timestamp/value. |
| `latestVersion` | integer | Source version indicator. |
| `location` | string | Location/region text. |
| `notificationHistory` | array\<object> | Notification history entries. |
| `notificationToken` | array\<string> | Notification token values (sensitive). |
| `termsOfServiceAccepted` | boolean | Terms accepted flag. |
| `termsOfServicesAccepted` | boolean | Alternate spelling variant observed. |
| `uid` | string | Source user/system identifier. |

### [🟢] privateFrontItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `beforeCustomFrontString` | string | Prior custom front text. |
| `beforeFrontNotificationString` | string | Prior front notification text. |
| `customFrontString` | string | Current custom front text. |
| `customFronters` | array | Custom fronter entries. |
| `frontNotificationString` | string | Front notification text. |
| `frontString` | string | Front display text. |
| `fronters` | array\<string> | Fronter identifiers. |
| `private` | boolean | Private visibility flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] reportsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `createdAt` | integer | Report created-at timestamp/value. |
| `uid` | string | Source user/system identifier. |
| `url` | string | Report URL/reference. |
| `usedSettings` | object | Report generation settings object. |

### [🟢] securityLogsItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `action` | string | Security-log action label. |
| `at` | integer | Action timestamp/value. |
| `ip` | string | IP address string. |
| `uid` | string | Source user/system identifier. |

### [🟢] serverDataItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `lastReadMessage` | integer | Last-read message pointer/timestamp. |
| `migrated` | boolean | Source migrated flag. |
| `uid` | string | Source user/system identifier. |

### [🟢] sharedFrontItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `beforeCustomFrontString` | string | Prior custom front text. |
| `beforeFrontNotificationString` | string | Prior front notification text. |
| `customFrontString` | string | Current custom front text. |
| `customFronters` | array | Custom fronter entries. |
| `frontNotificationString` | string | Front notification text. |
| `frontString` | string | Front display text. |
| `fronters` | array | Fronter entries (observed mixed/empty). |
| `uid` | string | Source user/system identifier. |

### [🟢] tokensItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `permission` | integer | Source permission value/code. |
| `token` | string | Token value (sensitive). |
| `uid` | string | Source user/system identifier. |

### [🟢] usageItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `expireAt` | string | Usage-metrics expiration value. |
| `uid` | string | Source user/system identifier. |
| `GET/POST/PATCH/DELETE endpoint counters` | integer fields | Dynamic map of endpoint label to usage count. |

### [🟢] usersItem

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `avatarUrl` | string | Avatar URL. |
| `color` | string | User/system color value. |
| `desc` | string | Description text. |
| `fields` | object | Dynamic map of custom field definitions keyed by field id. |
| `frame` | object | Visual frame configuration object. |
| `isAsystem` | boolean | Source system-indicator flag. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `supportDescMarkdown` | boolean | Markdown support flag. |
| `uid` | string | Source user/system identifier. |
| `username` | string | Source username. |

## Notes

- Some top-level collections have unknown/empty item shape in observed data; they are intentionally listed in the tree.
- Key spellings and variants should be preserved as source facts (for example `repeatedRemidners`).
- Sensitive fields (for example token-like values) should never be logged or exposed in unsafe output.
