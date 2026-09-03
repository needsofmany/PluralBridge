# Samsung Smart Switch Backup Layout (Observed)

The combined how-to plus this tree is `samsung-smart-switch-sp-recovery.md`. This file remains the shorter observed-layout note.

## Scope

This is a **file-tree map** of **one personal** PC Smart Switch backup of a Galaxy S24+ (`SM-S926U`). Counts, sizes, and folder inventories below are example numbers from that dataset, not a dump of the backup, not typical values, and not a PluralBridge ontology.

Observed backup root (UNC, read in place; do not copy into git):

`\\<host>\c_drive\Users\<user>\Samsung\SmartSwitch\backup\<device_model>\<device_id>\<backup_timestamp>\`

Sibling notes (do not duplicate here):

- `samsung-smart-switch-photo-info-format.md` — easyMover `PHOTO_INFO.json`
- `samsung-smart-switch-simply-db-format.md` — `simply.db` JSON shape

Official Simply Plural export JSON and Chap2 Safe Spine SQL notes stay as they are. This tree is a Smart Switch media + one-app backup, not an Apparyllis export.

Current location of this map: `Working\Architecture\schema\` in the private repo. Destination if a later pass migrates it: public `recovery/` or `docs/schema/` — decide then. Do not reshape the public `docs/` tree from this pass.

## Record Tree Key

- 🟢 **Strong recovery** for Simply Plural / PluralBridge phone recovery.
- 🟡 **Supporting index / catalog**: useful to correlate, not a substitute for bytes or `simply.db`.
- 🟠 **Opaque or encrypted**: present, not directly readable in this pass.
- 🔴 **Not useful, failed, or absent** from this tree.

## What this backup is

This is **one personal Galaxy S24+** PC transfer (`MakeOption` = `WithSelectList`, `FullBackupMode` = false, `ConnectType` = `pc`), not a full-phone backup and not a typical inventory. Counts below are from that sample. Manifest `CategoryInfoExtra` lists **226** device packages that *could* be offered on that phone; this folder only contains what was actually transferred.

| Observed | Value |
|---|---|
| Device | `SM-S926U` (product code `SM-S926UZVAVZW`, sales code `VZW`) |
| Android / One UI | Android 16 (`OsVer` 36 / `PlatformVersion` 16.0.0), One UI 8 (`OneUiVer` 80500) |
| Smart Switch (phone) | `AppVer` 3.7.72.6 |
| PC Kies / Smart Switch | `KiesVersion` 5.0.48.0, bundle `Version` 2.6 |
| Saved time | `2026-08-19T23:46:26` (folder stamp `20260819234505`) |
| Protocol | `Type` = `AOA`, `D2dProtocolVer` 37 |
| History rollup | `ItemsCount` 141, `FileSize` 303888312 (declared; packed-on-disk is smaller) |

The only app APK + app-data **in that sample** is **Simply Plural 1.11.5**, package `com.saltypandastudios.frontime` (Salty Panda / Frontime). There is **no** `com.apparyllis.simplyplural` package, and **no** loose `simply.db` at backup root. `simply.db` is inside the Frontime Android backup envelope (see APKFILE).

## Backup root

```text
<backup_timestamp>\
├─ 🟡 backupHistoryInfo.xml
├─ 🟡 ReqItemsInfo.json
├─ 🟡 SmartSwitchBackup.json          (identical SHA-256 to *_back.json)
├─ 🟡 SmartSwitchBackup_back.json
├─ 🟢 APKFILE\                        Frontime / Simply Plural APK + app data
├─ 🟡 Photo\                          131 media files + PHOTO_INFO.json
├─ 🟡 PHOTO_ORIGIN\                    5 photo-editor originals
├─ 🟡 GALLERYSETTING\                album catalog + gallery prefs (zip)
├─ 🟠 SECMP\                          encrypted Samsung media-provider DB
├─ 🟠 MEDIASEARCH\                    opaque gallery-search blob
├─ 🔴 APKDENYLIST\                    unrelated denylist stub
├─ 🔴 ContentBnrResult\                per-category backup result receipts
└─ 🔴 (no USERTAG\ folder)             requested in manifests, not on disk
```

On-disk file counts / bytes (this tree):

| Area | Files | Bytes |
|---|---|---|
| Photo | 132 | 123,381,467 |
| SECMP | 1 | 56,206,254 |
| APKFILE | 9 | 44,785,379 |
| MEDIASEARCH | 1 | 19,353,233 |
| PHOTO_ORIGIN | 5 | 4,801,303 |
| GALLERYSETTING | 1 | 5,879 |
| APKDENYLIST | 1 | 10,991 |
| ContentBnrResult | 2 | 858 |
| Root JSON/XML | 4 | ~417 KB |

## Manifest files (root)

### [🟡] backupHistoryInfo.xml

Root element `BatchInfomationItem` (Samsung spelling). Children: `BundleInfo`, `ItemsInfo`.

`BundleInfo` is a `BatchServiceBuldleInformation`: mode `Basic`, `FullBackupMode` false, `Type` AOA. `ItemsInfo` has one `BatchInformation` per transferred category. Observed `FileName` values vs this tree:

| History `FileName` | History `Type` | On disk |
|---|---|---|
| `APKFILE\` | Application | folder (not a zip) |
| `PHOTO\` | Photo | folder, 131 items |
| `MEDIASEARCH\MEDIASEARCH.zip` | MediaSearch | matches |
| `SECMP\SECMP.zip` | SECMP | matches |
| `PHOTO_ORIGIN\PHOTO_ORIGIN.zip` | PhotoOrigin | **mismatch**: tree is `PHOTO_ORIGIN\data\sec\photoeditor\0\`, not a zip |
| `GALLERYSETTING\GALLERYSETTING.zip` | GallerySetting | matches |
| `APKDENYLIST\APKDENYLIST.zip` | ApkDenyList | matches |

History `FileSize` on several categories is a placeholder or pre-pack size (e.g. MEDIASEARCH declared 102400, zip is ~19 MB; GALLERYSETTING declared ~59 MB, zip is 5.8 KB). Trust the files on disk, not those size fields.

The APK `BatchInformation` names the app `Simply Plural`, `ApplicationID` `com.saltypandastudios.frontime`, version `1.11.5` / code 384.

### [🟡] ReqItemsInfo.json

| Field | Type | Description |
|---|---|---|
| `MakeOption` | string | Observed `WithSelectList`. |
| `SDeviceInfoAll` | object | Device capability snapshot (model, One UI, storage, feature flags). Contains identifiers; do not copy IMEI, accounts, phone number, serial, or keystore material into git. |
| `Items.ItemList` | array | 10 requested categories. Status on this snapshot: `WAITING`. |

`ItemList` types, in order: `APKFILE`, `PHOTO` (ViewCount 131), `GALLERYEVENT`, `MEDIASEARCH`, `GALLERY_PET_SERVICE`, `SECMP`, `PHOTO_ORIGIN` (ViewCount 5), `GALLERYSETTING`, `USERTAG`, `APKDENYLIST`.

`SDeviceInfoAll.IndivisualTransferSupportItems` is a capability list for this phone/session, not a list of what is in the folder.

### [🟡] SmartSwitchBackup.json / SmartSwitchBackup_back.json

Byte-identical in this sample. Device + session catalog. Useful fields for recovery mapping (values that are identifiers stay out of git):

| Field | Observed |
|---|---|
| `OsType` | `Android` |
| `OsVer` | 36 |
| `OneUiVer` | 80500 |
| `AppVer` | 3.7.72.6 |
| `ConnectType` | `pc` |
| `DeviceType` | `phone` |
| `IsApp` | 10 category objects, `Status` = `PREPARED` |
| `CategoryInfoExtra` | 226 packages (device inventory, not this backup’s contents) |
| `PackageNameKeys` / `PackageNameValues` | length 3 |

`IsApp` `AppNameInfos` maps every Frontime split APK name to launcher label `Simply Plural`.

`IsApp` includes `USERTAG` and `GALLERY_PET_SERVICE`. Neither has a payload folder here.

## APKFILE (Simply Plural / Frontime)

```text
APKFILE\
├─ 🟡 AppList.json / AppList.bk
├─ 🟠 com.saltypandastudios.frontime.penc          encrypted base APK
├─ 🟡 com.saltypandastudios.frontime.png           launcher icon (PNG)
├─ 🟡 com.saltypandastudios.frontime_split_config.*.apk   plaintext split APKs
└─ 🟢 data\com.saltypandastudios.frontime\
     └─ 🟢 com.saltypandastudios.frontime.noedata  Android Backup envelope
```

### [🟡] AppList.json

Single `Apks[]` entry. `ApkName` = `Simply Plural`, `ApkPkgName` = `com.saltypandastudios.frontime`, `VersionName` 1.11.5, `VersionCode` 384, `TargetSdkVersion` 35, `AllowBackup` true, `IsSelected` true. `InstPkgName` = `com.android.vending`. `DataFile` / `AppDataFileName` point at the `.noedata` envelope. `AppList.bk` is a binary companion (not JSON; magic not PNG/ZIP/SQLite).

### [🟠] `.penc` vs split APKs

`.penc` is not a ZIP (leading bytes not `PK`). Split configs (`arm64_v8a`, `en`, `es`, `xxhdpi`) are ordinary APK/ZIP. For recovery, the **app data** in `.noedata` matters more than the APK bytes.

### [🟢] `.noedata` — Android Backup, then tar

Header (ASCII, four newline-terminated lines), then uncompressed ustar. Observed:

```text
ANDROID BACKUP
5
0
none
```

Payload starts at byte 24. Encryption line `none`, compression `0`. This is the live Simply Plural app-data source for this backup.

Android backup path tokens under `apps/com.saltypandastudios.frontime/`:

| Prefix | Meaning |
|---|---|
| `_manifest` | Package backup manifest |
| `r/` | Remaining app-data tree (Flutter `app_flutter`) |
| `f/` | `files/` |
| `db/` | `databases/` |
| `sp/` | `shared_prefs` |

### [🟢] `r/app_flutter/simply.db`

| | |
|---|---|
| Size | 116,539 bytes |
| Disk type | JSON object, despite `.db` |
| Starts with | `{"private":{...` |

Top-level keys on this live file (duplicate case/pluralization, same family as the sibling note): `private`, `Privates`, `privates`, `users`, `Users`, `Members`, `members`, `Channels`, `channels`, `FrontHistory`, `fronthistory`, `chatcategories`, `customfields`, `friends`, `friendssettings`, `privacybuckets`.

That agrees with `samsung-smart-switch-simply-db-format.md`. Do not replace that note from this tree. Field tables stay there. This map only records **location** and that the live object is JSON with those keys.

### Other `app_flutter` files (also `.db` names, mostly JSON)

Do not copy bodies (messages, queued API payloads, log lines, member names).

| Member | Size | Observed shape |
|---|---|---|
| `messages_<channelId>.db` | 616 | JSON `{ "messages": [...] }`. One file in this sample. Item fields: `message`, `channel`, `writer`, `writtenAt`, `id`. 4 messages. |
| `pendingRequests.db` | 4,043 | JSON array of 9 stringified request objects. Inner keys: `method`, `path`, `query`, `payload`, `timestamp`. |
| `logs.db` | 90,706 | JSON array of log-line strings. Sample starts `2026-08-18` (day before this backup). |
| `events.db` | 306 | JSON `{ "events": [{ "event", "time" }] }`. 5 events. |

### [🟡] `f/images.db` (real SQLite)

SQLite 3, 40,960 bytes. Tables: `android_metadata`, `cacheObject` (57 rows).

`cacheObject` columns: `_id`, `url`, `key`, `relativePath`, `eTag`, `validTill`, `touched`, `length`.

URL hosts in this sample (no full URLs, no signed query strings, no system uid):

| Count | Host | Path prefix |
|---|---|---|
| 51 | `spaces.apparyllis.com` | `/avatars/<systemUid>/` |
| 5 | `dist.apparyllis.com` | `/resources/` (stock plurality art) |
| 1 | one non-Apparyllis host | ignore for SP recovery |

`relativePath` values are 40-character `.bin` names. The **blob files are not in this Android backup** (typical: image cache lives under `cache/`, which backup omits). Treat `images.db` as an avatar-URL index, not as recovered avatar bytes. This backup’s `Photo\` tree is a selected gallery transfer, not the avatar store.

### [🟡] `sp/` shared_prefs (key names only)

Present: `FlutterSharedPreferences.xml`, `FlutterSecureStorage.xml`, `FlutterSecureKeyStorage.xml`, Frontime prefs, Firebase/GMS/messaging XML.

`FlutterSharedPreferences.xml` key names observed: `flutter.uid`, `flutter.access_key`, `flutter.refresh_key`, `flutter.pkAPI`, `flutter.loc`, `flutter.loc_country`, `flutter.lastConfigSync`, `flutter.listMembers`, plus FAQ/how-to and remote-config keys.

`flutter.access_key`, `flutter.refresh_key`, `flutter.pkAPI`, and `FlutterSecureStorage.xml` values are credentials. Do not copy them into git. They may be useful on a private recovery workstation only.

`db/` contains only Google Data Transport events SQLite (not Simply Plural). Firebase installation JSON filenames include an installation id; ignore the id.

## Photo

Matches `PHOTO_INFO.json` `count` = 131 = `files.length`. Layout:

```text
Photo\
├─ 🟢 data\user\0\com.sec.android.easyMover\files\SmartSwitch\tmp\PHOTO\
│     PHOTO_INFO.json
├─ 🟡 DCIM\SimplyPlural\     29 files
├─ 🟡 DCIM\Restored\         3 files (.jpg)
├─ 🟡 Download\              75 files
├─ 🟡 VZMedia\               21 files (.jpeg)
└─ 🟡 dbr-preview-img\       3 files (.jpg)
```

### [🟢] PHOTO_INFO.json

Path: `Photo\data\user\0\com.sec.android.easyMover\files\SmartSwitch\tmp\PHOTO\PHOTO_INFO.json` (61,187 bytes).

Field tables, SecmpId bit, and timestamp caveats: `samsung-smart-switch-photo-info-format.md`. `FilePath` / `FileName` describe the packed gallery files. They are not a map of uploaded member avatars.

### Path prefixes in this tree (directory only)

| Count | Backup folder | Device prefix in PHOTO_INFO / album DB |
|---|---|---|
| 75 | `Photo\Download` | `/mnt/sdcard/Download` |
| 29 | `Photo\DCIM\SimplyPlural` | `/mnt/sdcard/DCIM/SimplyPlural` |
| 21 | `Photo\VZMedia` | `/mnt/sdcard/VZMedia` |
| 3 | `Photo\dbr-preview-img` | `/mnt/sdcard/dbr-preview-img` |
| 3 | `Photo\DCIM\Restored` | `/mnt/sdcard/DCIM/Restored` |

`DCIM\SimplyPlural` extensions in this tree: `.jpg` 22, `.jpeg` 3, `.webp` 3, `.png` 1.

`Photo\DCIM\SimplyPlural` is one gallery prefix in this sample, not the Simply Plural avatar store. Download / VZMedia / Restored / dbr-preview are likewise selected photos. Do not catalog personal names, Discord/Gmail attachments, or `downloadUri` values.

## PHOTO_ORIGIN

```text
PHOTO_ORIGIN\data\sec\photoeditor\0\
  <64-hex>_<byteLength>.jpg   (5 files)
```

Samsung Photo Editor originals, not an SP folder. History XML still calls this `PHOTO_ORIGIN.zip`. Useful only if an edited gallery file needs a pre-edit original. Hashed names; no personal file names in the tree.

## GALLERYSETTING.zip

Three text members:

| Member | Role |
|---|---|
| `BACKUP_ALBUM_DB.txt` | JSON array, **70** album objects |
| `BACKUP_GALLERY_PREFERENCE_VALUE.txt` | Gallery UI prefs (column counts, location) |
| `BACKUP_SETTINGS_PREFERENCE_VALUE.txt` | Gallery settings (trash, notifications, etc.) |

### [🟡] album object fields

| Field | Presence | Description |
|---|---|---|
| `__bucketID` | 70/70 | Album id |
| `__Title` | 70/70 | Album title |
| `album_order` | 70/70 | Sort order |
| `__albumType` | 70/70 | Observed 0, 1, 2, 3, 5 |
| `__albumLevel` | 70/70 | Observed 0 or 1 |
| `essential_album_order` | 70/70 | |
| `__absPath` | 63/70 | Device directory |
| `cover_rect` | 63/70 | Cover crop |
| `default_cover_path` | 62/70 | Cover file path (do not copy personal names) |
| `folder_id` / `folder_name` | 25/70 | Optional folder grouping |

Prefixes present in the album catalog **and** in `Photo\`:

- `/storage/emulated/0/DCIM/SimplyPlural` (title `SimplyPlural`)
- `/storage/emulated/0/Download`
- `/storage/emulated/0/VZMedia`
- `/storage/emulated/0/DCIM/Restored`
- `/storage/emulated/0/dbr-preview-img`

The album DB also lists many other device albums (Camera, Screenshots, WhatsApp media, trip folders, and similar). **Those files are not in this backup.** Do not copy personal album titles into git. The catalog is a device map; this transfer only packed the 131 PHOTO items.

## SECMP / MEDIASEARCH / APKDENYLIST / ContentBnrResult

### [🟠] SECMP.zip

One member: `encrypted_backup.db` (~56.2 MB uncompressed). Magic is **not** SQLite. Relates to Samsung `sec_media_id` / `SecmpId` in PHOTO_INFO. Do not treat as a Simply Plural database. Not opened in this pass.

### [🟠] MEDIASEARCH.zip

One member: `backup_v23.bin` (~19.3 MB). Opaque binary. Gallery search index, not SP app data.

### [🔴] APKDENYLIST.zip

`APKDENYLIST.json` lists one unrelated package (`com.dti.folderlauncher` / GamesHub) plus a PNG. Not Simply Plural.

### [🔴] ContentBnrResult

Per-category JSON receipts named `fail.bk`:

| Category | `Result` | Notes |
|---|---|---|
| `GALLERYEVENT` | `true` | No `GALLERYEVENT\` payload folder in this tree |
| `GALLERY_PET_SERVICE` | `false` | `Err_Code` 5; no payload folder |

`USERTAG` appears in `ReqItemsInfo` / `IsApp` and not in `backupHistoryInfo` `ItemsInfo` and not on disk.

## Recovery use (this tree)

Work in this order:

1. **App state** — extract `simply.db` from `APKFILE\data\com.saltypandastudios.frontime\com.saltypandastudios.frontime.noedata` (Android Backup header, then tar member `apps/com.saltypandastudios.frontime/r/app_flutter/simply.db`). Shape: sibling simply.db note.
2. **Avatar references** — `simply.db` fields and optional `images.db` `cacheObject.url` hosts (`spaces.apparyllis.com/avatars/`) without committing URLs. Cached `.bin` files are not in this backup.
3. **Gallery `Photo\`** — selected media only; not the Simply Plural avatar store.
4. **Do not** treat SECMP, MEDIASEARCH, `.penc`, Samsung timestamps, or `SecmpId` as SP source of truth.
5. **Do not** commit tokens from `FlutterSharedPreferences` / `FlutterSecureStorage`, `downloadUri` values, personal file names, or member names from `simply.db`.

## Not in this tree

- No official Apparyllis/Simply Plural **export** JSON (see `simply-plural-export-format.md` if needed; it is a different source).
- No `com.apparyllis.simplyplural` package data.
- No messages/contacts/calendar/SMS payload folders.
- No Chap2 Safe Spine SQL.
- Cached avatar `.bin` files referenced by `images.db` (index only).
