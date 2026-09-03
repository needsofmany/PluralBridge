# Samsung Smart Switch simply.db JSON Format (Observed)

## Scope

This documents the **phone-recovered Simply Plural `simply.db` JSON** shape observed from:

- `samsung_smart_switch_simply_db.inferred.schema.json`

This is a source-shape reference for recovery/import planning, not a PluralBridge ontology.

## Record Tree Key

- 🟢 **Documented record type**: item shape is documented with a 3-column table.
- 🟡 **Observed variant/special case**: duplicate family by case/pluralization.
- 🟠 **Known container/sub-structure**: nested object or map container.
- 🔴 **Unknown/empty shape**: object exists but internal fields were not observed.

## Record Tree

```text
simply.db JSON (object)
├─ 🟡 Channels (map) -> ChannelsMapOrObject
│  └─ 🟢 dynamic key -> channel entry object
├─ 🟡 FrontHistory (map) -> FrontHistoryMapOrObject
│  └─ 🟢 dynamic key -> front history entry object
├─ 🟡 Members (map) -> MembersMapOrObject
│  └─ 🟢 dynamic key -> member entry object
├─ 🟡 Privates (map) -> PrivatesMapOrObject
│  └─ 🟢 dynamic key -> private entry object
├─ 🟡 Users (map) -> UsersMapOrObject
│  └─ 🟢 dynamic key -> user entry object
├─ 🟡 channels (map) -> channelsMapOrObject
│  └─ 🟢 dynamic key -> channel entry object
├─ 🟢 chatcategories (map) -> chatcategoriesMapOrObject
│  └─ 🟢 dynamic key -> chat category entry object
├─ 🟢 customfields (map) -> customfieldsMapOrObject
│  └─ 🟢 dynamic key -> custom field entry object
├─ 🟢 friends (map) -> friendsMapOrObject
│  └─ 🟢 dynamic key -> friend entry object
├─ 🟢 friendssettings (map) -> friendssettingsMapOrObject
│  └─ 🟢 dynamic key -> friend settings entry object
├─ 🟡 fronthistory (map) -> fronthistoryMapOrObject
│  └─ 🟢 dynamic key -> front history entry object
├─ 🟡 members (map) -> membersMapOrObject
│  └─ 🟢 dynamic key -> member entry object
├─ 🟢 privacybuckets (map) -> privacybucketsMapOrObject
│  └─ 🟢 dynamic key -> privacy bucket entry object
├─ 🟡 private (map) -> privateMapOrObject
│  └─ 🟢 dynamic key -> private entry object
│     └─ 🟠 defaultPrivacy (object)
│        ├─ 🔴 customFields[] (unknown item shape)
│        ├─ 🔴 customFronts[] (unknown item shape)
│        ├─ 🔴 groups[] (unknown item shape)
│        └─ 🔴 members[] (unknown item shape)
├─ 🟡 privates (map) -> privatesMapOrObject
│  └─ 🟢 dynamic key -> private summary object
└─ 🟡 users (map) -> usersMapOrObject
   └─ 🟢 dynamic key -> user entry object
      ├─ 🟠 fields (dynamic map keyed by field id)
      │  └─ 🟢 dynamic key -> field config object
      └─ 🟠 frame (object, observed empty shape)
```

## Record Types

### [🟢] ChannelsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Channel entry keyed by source identifier. |
| `<dynamic key>.name` | string | Channel name. |
| `<dynamic key>.desc` | string | Channel description. |

### [🟢] FrontHistoryMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Front history entry keyed by source identifier. |
| `<dynamic key>.custom` | boolean | Custom front state flag. |
| `<dynamic key>.customStatus` | string | Custom front state text. |
| `<dynamic key>.endTime` | integer | End timestamp/value. |
| `<dynamic key>.id` | string | Source row identifier. |
| `<dynamic key>.live` | boolean | Live/current entry flag. |
| `<dynamic key>.member` | string | Linked member identifier. |
| `<dynamic key>.startTime` | integer | Start timestamp/value. |
| `<dynamic key>.type` | string | Source type label. |

### [🟢] MembersMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Member entry keyed by source identifier. |
| `<dynamic key>.archived` | boolean | Archived state flag. |
| `<dynamic key>.archivedReason` | string | Archive reason text. |
| `<dynamic key>.avatarUrl` | string | Avatar URL. |
| `<dynamic key>.avatarUuid` | string | Avatar UUID/reference. |
| `<dynamic key>.color` | string | Member color value. |
| `<dynamic key>.desc` | string | Member description text. |
| `<dynamic key>.frame` | object | Frame styling object (`bgClip`, `bgEndColor`, `bgShape`, `bgStartColor`). |
| `<dynamic key>.info` | object | Dynamic map of field id to string value. |
| `<dynamic key>.name` | string | Member display name. |
| `<dynamic key>.pkId` | string | Source primary-key-like identifier. |
| `<dynamic key>.preventTrusted` | boolean | Prevent trusted visibility flag. |
| `<dynamic key>.preventsFrontNotifs` | boolean | Front notification prevention flag. |
| `<dynamic key>.private` | boolean | Private visibility flag. |
| `<dynamic key>.pronouns` | string | Pronoun text. |
| `<dynamic key>.receiveMessageBoardNotifs` | boolean | Message board notification flag. |
| `<dynamic key>.supportDescMarkdown` | boolean | Markdown support flag. |

### [🟢] PrivatesMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Private entry keyed by source identifier. |
| `<dynamic key>.notificationToken` | array\<string> | Notification token values (sensitive). |

### [🟢] UsersMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | User entry keyed by source identifier. |
| `<dynamic key>.avatarUrl` | string | Avatar URL. |
| `<dynamic key>.color` | string | Color value. |
| `<dynamic key>.desc` | string | Description text. |
| `<dynamic key>.frame` | object | Frame object (observed empty shape). |
| `<dynamic key>.supportDescMarkdown` | boolean | Markdown support flag. |

### [🟢] channelsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Lowercase variant of channel entry map. |
| `<dynamic key>.name` | string | Channel name. |
| `<dynamic key>.desc` | string | Channel description. |

### [🟢] chatcategoriesMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Chat category entry keyed by source identifier. |
| `<dynamic key>.channels` | array\<string> | Channel identifiers in category. |
| `<dynamic key>.desc` | string | Category description. |
| `<dynamic key>.name` | string | Category name. |

### [🟢] customfieldsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Custom field entry keyed by source identifier. |
| `<dynamic key>.buckets` | array\<string> | Linked privacy bucket identifiers. |
| `<dynamic key>.name` | string | Field name. |
| `<dynamic key>.order` | string | Source order value. |
| `<dynamic key>.type` | integer | Source field type code. |

### [🟢] friendsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Friend entry keyed by source identifier. |
| `<dynamic key>.avatarUrl` | string | Avatar URL. |
| `<dynamic key>.color` | string | Color value. |
| `<dynamic key>.desc` | string | Description text. |
| `<dynamic key>.frame` | object | Frame object (observed empty shape). |
| `<dynamic key>.isAsystem` | boolean | System indicator flag. |
| `<dynamic key>.supportDescMarkdown` | boolean | Markdown support flag. |
| `<dynamic key>.username` | string | Username text. |

### [🟢] friendssettingsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Friend settings entry keyed by source identifier. |
| `<dynamic key>.buckets` | array\<string> | Linked bucket identifiers. |
| `<dynamic key>.frienduid` | string | Friend identifier. |
| `<dynamic key>.getFrontNotif` | boolean | Receive front notifications flag. |
| `<dynamic key>.getTheirFrontNotif` | boolean | Receive their front notifications flag. |
| `<dynamic key>.seeFront` | boolean | Front visibility permission flag. |
| `<dynamic key>.seeMembers` | boolean | Member visibility permission flag. |

### [🟢] fronthistoryMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Lowercase variant of front history entry map. |
| `<dynamic key>.custom` | boolean | Custom front state flag. |
| `<dynamic key>.customStatus` | string | Custom front state text. |
| `<dynamic key>.endTime` | integer | End timestamp/value. |
| `<dynamic key>.live` | boolean | Live/current entry flag. |
| `<dynamic key>.member` | string | Linked member identifier. |
| `<dynamic key>.startTime` | integer | Start timestamp/value. |

### [🟢] membersMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Lowercase variant of member entry map. |
| `<dynamic key>.archived` | boolean | Archived state flag. |
| `<dynamic key>.archivedReason` | string | Archive reason text. |
| `<dynamic key>.avatarUrl` | string | Avatar URL. |
| `<dynamic key>.avatarUuid` | string | Avatar UUID/reference. |
| `<dynamic key>.buckets` | array\<string> | Linked privacy bucket identifiers. |
| `<dynamic key>.color` | string | Member color value. |
| `<dynamic key>.desc` | string | Member description text. |
| `<dynamic key>.frame` | object | Frame styling object (`bgClip`, `bgEndColor`, `bgShape`, `bgStartColor`). |
| `<dynamic key>.info` | object | Dynamic map of field id to string value. |
| `<dynamic key>.name` | string | Member display name. |
| `<dynamic key>.pkId` | string | Source primary-key-like identifier. |
| `<dynamic key>.preventsFrontNotifs` | boolean | Front notification prevention flag. |
| `<dynamic key>.pronouns` | string | Pronoun text. |
| `<dynamic key>.receiveMessageBoardNotifs` | boolean | Message board notification flag. |
| `<dynamic key>.supportDescMarkdown` | boolean | Markdown support flag. |

### [🟢] privacybucketsMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Privacy bucket entry keyed by source identifier. |
| `<dynamic key>.color` | string | Bucket color value. |
| `<dynamic key>.desc` | string | Bucket description text. |
| `<dynamic key>.icon` | string | Bucket icon value. |
| `<dynamic key>.name` | string | Bucket name. |
| `<dynamic key>.rank` | string | Bucket ranking/order value. |

### [🟢] privateMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Private settings entry keyed by source identifier. |
| `<dynamic key>.auditContentChanges` | boolean | Audit-content flag. |
| `<dynamic key>.auditRetention` | integer | Audit retention value. |
| `<dynamic key>.categories` | array\<string> | Category list. |
| `<dynamic key>.defaultPrivacy` | object | Default privacy object (`customFields`, `customFronts`, `groups`, `members`). |
| `<dynamic key>.hideAudits` | boolean | Hide-audits flag. |
| `<dynamic key>.latestVersion` | integer | Source version indicator. |
| `<dynamic key>.location` | string | Location/region text. |
| `<dynamic key>.notificationToken` | array\<string> | Notification token values (sensitive). |
| `<dynamic key>.termsOfServiceAccepted` | boolean | Terms acceptance flag. |

### [🟢] privatesMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Minimal private settings variant keyed by source identifier. |
| `<dynamic key>.latestVersion` | integer | Source version indicator. |
| `<dynamic key>.location` | string | Location/region text. |

### [🟢] usersMapOrObject

| Field | Type | Description |
|---|---|---|
| `<dynamic key>` | object | Lowercase variant of user entry map. |
| `<dynamic key>.avatarUrl` | string | Avatar URL. |
| `<dynamic key>.color` | string | Color value. |
| `<dynamic key>.desc` | string | Description text. |
| `<dynamic key>.fields` | object | Dynamic map keyed by field id to field config object (`name`, `order`, `preventTrusted`, `private`, `type`). |
| `<dynamic key>.frame` | object | Frame object (observed empty shape). |
| `<dynamic key>.isAsystem` | boolean | System indicator flag. |
| `<dynamic key>.supportDescMarkdown` | boolean | Markdown support flag. |
| `<dynamic key>.username` | string | Username text. |

## Notes

- This source is a recovery/discovery path, not the official export format.
- Mixed case and duplicate families are source facts and should be normalized during import parsing.
- Dynamic-key maps should be treated as dictionaries, not fixed-property objects.
- Sensitive token-like values should never be emitted in logs or unsafe outputs.
