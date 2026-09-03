# Simply Plural Export JSON Format (Observed)

## Scope

This documents the **official Simply Plural export JSON** shape as observed from the inferred schema source:

- `simply_plural_last_export.inferred.schema.json`

It also adds source-derived fills from the public `ApparyllisOrg/SimplyPluralApi` repository where the observed export contained empty arrays.

This is a source-shape reference for import planning, not a PluralBridge ontology.

## Record Tree Key

- 🟢 **Documented record type**: item shape is documented in this file with a 3-column table.
- 🟡 **Observed but variant/special case**: known oddity such as misspelling or duplicated naming pattern.
- 🟠 **Known container/sub-structure**: nested object exists but is not a top-level record type table.
- 🔴 **Unknown item shape**: collection exists but item fields were not available from observed sample data.

## Record Tree

```text
Export (object)
├─ 🟢 automatedReminders[] -> automatedRemindersItem
├─ 🟢 avatarExports[] -> avatarExportsItem
├─ 🟢 boardMessages[] -> boardMessagesItem
├─ 🔴 cancelFeedback[] (unknown item shape)
├─ 🟢 channelCategories[] -> channelCategoriesItem
├─ 🟢 channels[] -> channelsItem
├─ 🟢 chatMessages[] -> chatMessagesItem
├─ 🟢 comments[] -> commentsItem
├─ 🟢 customFields[] -> customFieldsItem
├─ 🟢 dataExports[] -> dataExportsItem
├─ 🟡 events[] -> eventsItem (global metrics shape; normally export-empty under `{ uid }`)
├─ 🟢 friends[] -> friendsItem
├─ 🟢 frontHistory[] -> frontHistoryItem
├─ 🟢 frontStatuses[] -> frontStatusesItem
├─ 🟡 fronters[] -> frontersItem (legacy live-front cache migrated into frontHistory)
├─ 🟢 groups[] -> groupsItem
├─ 🔴 invalidJwtTokens[] (unknown item shape)
├─ 🟢 members[] -> membersItem
├─ 🟡 messages[] -> messagesItem (global announcement shape; normally export-empty under `{ uid }`)
├─ 🟢 notes[] -> notesItem
├─ 🟡 notifications[] -> notificationsItem (token-addressed notification queue; normally export-empty under `{ uid }`)
├─ 🟡 pendingFriendRequests[] -> pendingFriendRequestsItem (sender/receiver keyed; normally export-empty under `{ uid }`)
├─ 🟢 polls[] -> pollsItem
├─ 🔴 potato[] (unknown item shape)
├─ 🟢 privacyBuckets[] -> privacyBucketsItem
├─ 🟢 private[] -> privateItem
│  └─ 🟠 defaultPrivacySettings (object)
│     ├─ 🟠 customFields[] (array of privacy bucket identifiers)
│     ├─ 🟠 customFronts[] (array of privacy bucket identifiers)
│     ├─ 🟠 groups[] (array of privacy bucket identifiers)
│     └─ 🟠 members[] (array of privacy bucket identifiers)
├─ 🟢 privateFront[] -> privateFrontItem
├─ 🟢 queuedEvents[] -> queuedEventsItem
├─ 🟡 repeatedRemidners[] (unknown item shape, observed misspelling)
├─ 🟢 repeatedReminders[] -> repeatedRemindersItem
├─ 🟢 reports[] -> reportsItem
│  └─ 🟠 usedSettings (object)
│     ├─ 🟠 cc[] (array of email strings)
│     ├─ 🟠 customFronts (object)
│     │  ├─ 🟠 buckets[] (array of privacy bucket identifiers)
│     │  ├─ includeBucketless (boolean)
│     │  └─ privacyLevel (integer)
│     ├─ 🟠 frontHistory (object)
│     │  ├─ 🟠 customFrontBuckets[] (array of privacy bucket identifiers)
│     │  ├─ 🟠 memberBuckets[] (array of privacy bucket identifiers)
│     │  ├─ start (integer)
│     │  ├─ end (integer)
│     │  ├─ includeCustomFronts (boolean)
│     │  ├─ includeCustomFrontsBucketless (boolean)
│     │  ├─ includeMembers (boolean)
│     │  ├─ includeMembersBucketless (boolean)
│     │  └─ privacyLevel (integer)
│     ├─ 🟠 members (object)
│     │  ├─ 🟠 buckets[] (array of privacy bucket identifiers)
│     │  ├─ includeBucketless (boolean)
│     │  ├─ includeCustomFields (boolean)
│     │  └─ privacyLevel (integer)
│     └─ sendTo (string)
├─ 🟢 securityLogs[] -> securityLogsItem
├─ 🟢 serverData[] -> serverDataItem
├─ 🟢 sharedFront[] -> sharedFrontItem
├─ 🟢 socketNotifications[] -> socketNotificationsItem
├─ 🟡 subscribers[] -> subscribersItem (Stripe subscription state; sensitive)
├─ 🔴 test[] (unknown item shape)
├─ 🟢 tokens[] -> tokensItem
├─ 🟢 undeliveredMessages[] -> undeliveredMessagesItem
├─ 🟢 usage[] -> usageItem
├─ 🟢 users[] -> usersItem
│  ├─ 🟠 fields (object keyed by field id)
│  │  └─ 🟠 field config object
│  └─ 🟠 frame (object)
├─ 🟡 verifiedKeys[] -> verifiedKeysItem (confirmation-key cache; no uid in public code path)
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

### [🟢] automatedRemindersItem

Source-derived from the public API validator and event scheduler. Added documents also receive `_id`, `uid`, and `lastOperationTime` from the generic document helper.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Reminder display name. |
| `message` | string | Notification body sent when the reminder fires. |
| `action` | number | Optional action code from older/current client payloads. |
| `delayInHours` | number | Delay after matching front-change condition. |
| `type` | number | Selected trigger type: member front, custom front, or any front according to backend comments. |

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

### [🟢] commentsItem

Source-derived from the public API validator and migration `update150`. Current comments attach only to `frontHistory`.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `lastOperationTime` | integer | Source last-operation timestamp; may be absent on migration-created legacy comments. |
| `time` | integer | Comment timestamp/value. |
| `text` | string | Comment body text. |
| `supportMarkdown` | boolean | Markdown support flag; defaults true in current validator and may be absent on migrated comments. |
| `documentId` | string | Identifier of the commented document. |
| `collection` | string | Comment target collection; current code accepts `frontHistory`. |

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

### [🟡] eventsItem

Source-derived from event logging code. The export function queries every collection using `{ uid }`; event rows created by the public code do not include `uid`, so this collection normally exports as an empty array even when the database contains global metrics.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `date` | string/date | Start-of-day date for the metric bucket. |
| `event` | string | Event name. |
| `count` | integer | Incrementing event count. |

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

### [🟡] frontersItem

Legacy collection shape inferred from migration `update150`. The migration copies these entries into `frontHistory` as live front records, using the legacy `_id` as the member/custom-front id.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Legacy member/custom-front identifier. |
| `uid` | string | Source user/system identifier. |
| `startTime` | integer | Live front start timestamp/value. |

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

### [🟡] messagesItem

Source-derived from the public message endpoint. These are global/app messages read through `serverData.lastReadMessage`, not user chat messages. The public code queries active messages by `start`/`end` without `uid`, so this collection normally exports empty under the exporter's `{ uid }` filter.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `title` | string | Message title. |
| `message` | string | Message body text. |
| `answer` | string | Optional answer/action text returned to the client. |
| `start` | integer | Start timestamp/value for message visibility. |
| `end` | integer | End timestamp/value for message visibility. |

### [🟡] notificationsItem

Source-derived from the notification scheduler. These are addressed to notification tokens rather than stored under the recipient `uid`, so normal user export is expected to produce an empty array. Treat `token` as sensitive.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `token` | string | Push notification token. |
| `instigator` | string | UID that caused the notification. |
| `title` | string | Notification title. |
| `message` | string | Notification body. |
| `expireAt` | string/date | Expiration time for the queued notification. |

### [🟡] pendingFriendRequestsItem

Source-derived from friend request code. Pending requests are stored with `sender` and `receiver`, not `uid`; the exporter's `{ uid }` filter likely omits them even when a user has pending requests.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `sender` | string | UID that sent the friend request. |
| `receiver` | string | UID receiving the friend request. |
| `seeMembers` | boolean | Requested member-visibility permission. |
| `seeFront` | boolean | Requested front-visibility permission. |
| `getFrontNotif` | boolean | Requested front-notification permission. |
| `trusted` | boolean | Legacy trusted-friend flag. |
| `message` | string | Request message; current code forces this to empty during request creation. |

### [🟢] pollsItem

Source-derived from the public poll validators plus migration `update150`. Current polls use `votes[]`; older records may contain `yes`, `no`, `abstain`, `veto`, or object-shaped `votes` before migration.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Poll name. |
| `desc` | string | Poll description. |
| `supportDescMarkdown` | boolean | Markdown support flag. |
| `endTime` | integer | Poll end timestamp/value. |
| `custom` | boolean | False for yes/no/abstain/veto polls; true for custom-option polls. |
| `allowAbstain` | boolean | Normal poll option flag; present when `custom` is false. |
| `allowVeto` | boolean | Normal poll option flag; present when `custom` is false. |
| `options` | array\<object> | Custom poll options; present when `custom` is true. Each item has `name` and `color`. |
| `votes` | array\<object> | Current vote list. Each item has `id`, `vote`, and `comment`. |
| `yes` | array | Legacy yes-vote list, migrated into `votes`. |
| `no` | array | Legacy no-vote list, migrated into `votes`. |
| `abstain` | array | Legacy abstain-vote list, migrated into `votes`. |
| `veto` | array | Legacy veto-vote list, migrated into `votes`. |

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

### [🟢] queuedEventsItem

Source-derived from the public event scheduler. This collection stores scheduled internal work, including repeat reminders, automated reminders, and front-change notifications.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `event` | string | Scheduled event type, such as `scheduledRepeatReminder`, `scheduledAutomatedReminder`, `frontChange`, `frontChangeShared`, or `frontChangePrivate`. |
| `due` | integer | Due timestamp/value. |
| `reminderId` | string | Linked reminder identifier for reminder events. |
| `message` | string | Repeat-reminder notification body copied into scheduled repeat events. |

### [🟢] repeatedRemindersItem

Source-derived from the public repeated-reminder validator and scheduler.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `lastOperationTime` | integer | Source last-operation timestamp. |
| `name` | string | Reminder display name. |
| `message` | string | Notification body sent when the reminder fires. |
| `dayInterval` | number | Repeat interval in days. |
| `time` | object | Target time object. |
| `time.hour` | number | Target hour. |
| `time.minute` | number | Target minute. |
| `startTime` | object | Start date object. |
| `startTime.year` | number | Start year. |
| `startTime.month` | number | Start month. |
| `startTime.day` | number | Start day. |

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

### [🟢] socketNotificationsItem

Source-derived from websocket notification code. These records are short-lived server delivery artifacts; the collection has a TTL index on `t` with a 100-second expiration.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `title` | string | Notification title. |
| `message` | string | Notification body. |
| `t` | string/date | Insertion timestamp used by the TTL index. |

### [🟡] subscribersItem

Source-derived from Stripe subscription code. Treat Stripe identifiers and subscription state as sensitive operational data.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `customerId` | string | Stripe customer identifier. |
| `subscriptionId` | string | Stripe subscription identifier when active. |
| `periodEnd` | integer | Subscription period end timestamp/value from Stripe. |
| `priceId` | string | Stripe price identifier. |
| `cancelled` | boolean | Whether cancellation at period end is active. |

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

### [🟢] undeliveredMessagesItem

Source-derived from chat delivery failure handling. These records preserve failed chat-message writes when the target channel, writer member, or reply target cannot be resolved.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `uid` | string | Source user/system identifier. |
| `message` | object | Original chat write payload. |
| `message.message` | string | Chat message text. |
| `message.channel` | string | Target channel identifier. |
| `message.writer` | string | Writer/member identifier. |
| `message.writtenAt` | integer | Intended written-at timestamp/value. |
| `message.replyTo` | string | Optional reply-to message identifier. |
| `reason` | string | Failure reason, observed in code as `channel not found`, `Member not found`, or `Reply-to not found`. |

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

### [🟡] verifiedKeysItem

Source-derived from email confirmation code. Inserted records contain only `key`; no `uid` is stored in the public code path, so these should normally be absent from a user export filtered by `{ uid }`.

| Field | Type | Description |
|---|---|---|
| `_id` | string | Source row identifier. |
| `key` | string | Email-verification key already consumed. |

## Source-Derived Nested Shapes

### `private[].defaultPrivacySettings` and `private[].defaultPrivacy`

The public source contains both names:

- `defaultPrivacySettings` is written by migration `update300`.
- `defaultPrivacy` is accepted by the current private-user validator and read by default bucket assignment code.

Both names have the same practical shape:

| Field | Type | Description |
|---|---|---|
| `members` | array\<string> | Default privacy bucket identifiers for new members. |
| `groups` | array\<string> | Default privacy bucket identifiers for new groups. |
| `customFronts` | array\<string> | Default privacy bucket identifiers for new custom fronts/front statuses. |
| `customFields` | array\<string> | Default privacy bucket identifiers for new custom fields. |

### `reports[].usedSettings`

The report request schemas exist in both v1 and v2. The observed export shape appears to match the v2 bucket-based report schema, with legacy `privacyLevel` values still possible from v1-era records.

| Field | Type | Description |
|---|---|---|
| `sendTo` | string | Recipient email address. |
| `cc` | array\<string> | CC email addresses. |
| `members.includeCustomFields` | boolean | Include member custom fields. |
| `members.includeBucketless` | boolean | Include members without buckets. |
| `members.buckets` | array\<string> | Privacy bucket identifiers used to select members. |
| `members.privacyLevel` | number | Legacy v1 report privacy selector. |
| `customFronts.includeBucketless` | boolean | Include custom fronts without buckets. |
| `customFronts.buckets` | array\<string> | Privacy bucket identifiers used to select custom fronts. |
| `customFronts.privacyLevel` | number | Legacy v1 report privacy selector. |
| `frontHistory.start` | integer | Report start timestamp/value. |
| `frontHistory.end` | integer | Report end timestamp/value. |
| `frontHistory.includeMembers` | boolean | Include member front-history entries. |
| `frontHistory.includeCustomFronts` | boolean | Include custom-front history entries. |
| `frontHistory.includeMembersBucketless` | boolean | Include bucketless member entries. |
| `frontHistory.includeCustomFrontsBucketless` | boolean | Include bucketless custom-front entries. |
| `frontHistory.memberBuckets` | array\<string> | Privacy bucket identifiers for member front-history selection. |
| `frontHistory.customFrontBuckets` | array\<string> | Privacy bucket identifiers for custom-front history selection. |
| `frontHistory.privacyLevel` | number | Legacy v1 report privacy selector. |

## Remaining Unresolved Collections

The public backend source did not expose a reliable stored shape for these top-level collections:

| Collection | Current status |
|---|---|
| `cancelFeedback` | No public `getCollection("cancelFeedback")` usage found; subscription cancellation feedback goes directly to Stripe in current code. |
| `invalidJwtTokens` | No public collection usage found; current JWT invalidation uses account `firstValidJWtTime`. |
| `potato` | No public collection usage found. |
| `repeatedRemidners` | Observed misspelled collection name; no public collection usage found. Treat separately from `repeatedReminders`. |
| `test` | No public collection usage found. |
| `views` | No public collection usage found. Do not conflate with current `filters`, which has a separate API and collection name. |

## Notes

- Some top-level collections still have unresolved item shape after checking observed data and public backend source; they are intentionally listed in the tree.
- Key spellings and variants should be preserved as source facts (for example `repeatedRemidners`).
- Sensitive fields (for example token-like values) should never be logged or exposed in unsafe output.
- The export code enumerates Mongo collections, skips only `accounts`, then exports `find({ uid })` results. Collections whose records do not store `uid` can appear as top-level empty arrays even when the database contains records.
- Chat messages are decrypted during export. The exported `chatMessages[].message` is plaintext when `iv` and encrypted message text are present in storage.
