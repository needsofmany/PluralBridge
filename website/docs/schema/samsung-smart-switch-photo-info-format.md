# Samsung Smart Switch PHOTO_INFO.json Format (Observed)

## Scope

This documents the **Samsung Smart Switch / easyMover `PHOTO_INFO.json`** shape as observed from **one personal** Galaxy S24+ backup:

- SmartSwitch `tmp\PHOTO\PHOTO_INFO.json` (Galaxy S24+, `SM-S926U`)
- Root `count` was **131** in that sample, matching `files.length`

Those counts are example numbers from that one personal dataset. Other backups will differ.

This is a source-shape reference for phone recovery and media correlation. It is not a PluralBridge ontology and it is not the Simply Plural `simply.db` JSON (see `samsung-smart-switch-simply-db-format.md`).

Field lists below are a union over the observed `files` records in the sample. Personal paths, file names, and `downloadUri` values from the sample are not copied here.

## Record Tree Key

- 🟢 **Always present** on every `files` record in this sample.
- 🟡 **Optional**: present on some records only.
- 🔴 **Not a reliable original-creation signal** for Simply Plural recovery.

## Record Tree

```text
PHOTO_INFO.json (object)
├─ 🟢 categoryType  (string, observed "PHOTO")
├─ 🟢 count         (number, observed 131)
└─ 🟢 files[]       (array of photo records)
   └─ photo record
      ├─ 🟢 FilePath
      ├─ 🟢 FileName
      ├─ 🟢 Length
      ├─ 🔴 Taken
      ├─ 🔴 DateModified
      ├─ 🟡 RecentPrimary
      ├─ 🟢 Id
      ├─ 🟢 SecmpId
      ├─ 🟢 SefType
      ├─ 🟢 SefSubType
      ├─ 🟢 type2
      ├─ 🟢 CategoryType
      ├─ 🟡 ownerPackageName2
      ├─ 🟡 GrpType
      ├─ 🟡 GrpId
      ├─ 🟡 downloadUri
      ├─ 🟡 SefTypes
      ├─ 🟡 OriginHash
      ├─ 🟡 OriginSize
      ├─ 🟡 BestImage
      └─ 🟡 Orientation
```

Root `categoryType` and each record's `CategoryType` were both `"PHOTO"` in this sample.

## Root object

| Field | Type | Description |
|---|---|---|
| `categoryType` | string | Media category for this file. Observed: `PHOTO`. |
| `count` | number | Declared record count. Observed equal to `files.length`. |
| `files` | array | Photo metadata records. |

## Record Types

### [🟢] files[] photo record — always present

| Field | Type | Description |
|---|---|---|
| `FilePath` | string | Device path. Strong recovery evidence. |
| `FileName` | string | File name on device. Strong recovery evidence. |
| `Length` | number | File size in bytes. |
| `Taken` | number | Timestamp in milliseconds. Present on every record. Not a reliable original creation time for batch-indexed Simply Plural / VZMedia / download rows. |
| `DateModified` | number | Timestamp in milliseconds. Same caveat as `Taken`. |
| `RecentPrimary` | number | Always present in this sample. About 1000× `DateModified` (microseconds vs milliseconds). Samsung media-index / recency, usually close to `DateModified`. |
| `Id` | number | Samsung photo-record id. Separate sequence from `SecmpId`. Not strong Simply Plural recovery evidence. |
| `SecmpId` | number | Always present. In this sample every value has the `0x80000000` bit set (Samsung `sec_media_id` style). **Not** `Id + 0x80000000`; `Id` and `SecmpId` are different sequences. Not strong Simply Plural recovery evidence. |
| `SefType` | number | Almost always `0` in this sample; a few records used `2608`. Meaning not documented here. |
| `SefSubType` | number | Always `0` in this sample. |
| `type2` | string | Always `MEDIA` in this sample. |
| `CategoryType` | string | Always `PHOTO` in this sample. |

### [🟡] files[] photo record — optional

Counts are out of 131 records in this sample.

| Field | Type | Count | Description |
|---|---|---|---|
| `ownerPackageName2` | string | 126 | App that owned or ingested the file (Chrome, Downloads UI, Gmail, Photos, and others). Missing on 5 records. |
| `GrpType` | number | 67 | Observed `0` or `2`. Absent on the rest. |
| `downloadUri` | string | 43 | Source URL when the file was fetched (browser, Photos, and similar). May contain signed query strings. Do not commit sample values. |
| `SefTypes` | string | 11 | Comma-wrapped code list, for example `,3169,2721,`. Related to `SefType` but not identical. |
| `GrpId` | number | 8 | Group id when present. |
| `OriginHash` | string | 5 | Hash plus size fragment. Observed length about 71–72 characters. |
| `OriginSize` | number | 5 | Appears with `OriginHash`. |
| `BestImage` | number | 1 | Observed `1` on a single record. |
| `Orientation` | number | 1 | Observed `90` on a single record. |

## Path prefixes in this sample

Directory prefixes only (not file names):

| Count | Directory |
|---|---|
| 75 | `/mnt/sdcard/Download` |
| 29 | `/mnt/sdcard/DCIM/SimplyPlural` |
| 21 | `/mnt/sdcard/VZMedia` |
| 3 | `/mnt/sdcard/dbr-preview-img` |
| 3 | `/mnt/sdcard/DCIM/Restored` |

Treat `/mnt/sdcard/DCIM/SimplyPlural` as one directory prefix among several in this sample. It is not the Simply Plural avatar store.

Many DCIM/SimplyPlural / VZMedia / Download-UI records share a batch index window around **24 July 2024** (`Taken` / `DateModified` clustered). Those timestamps cannot be treated as original capture time.

## Recovery use

For Simply Plural recovery, the strongest evidence from this file is:

- `FilePath`
- `FileName`
- Simply Plural member / avatar metadata from the app or an official export

This catalog describes gallery files that happened to be in the photos transfer. It is not a map of uploaded member avatars.

Do not treat Samsung internal ids (`Id`, `SecmpId`) or the timestamp fields as proof of when a member avatar was originally created. Do not copy `downloadUri` values into git; they can include signed URLs.
