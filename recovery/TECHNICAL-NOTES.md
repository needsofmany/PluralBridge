````text
Copyright © 2026 Needs of the Many / PluralBridge contributors

Licensed under the GNU General Public License v3.0 (GPLv3).

This recovery implementation and accompanying documentation were developed by Needs of the Many / PluralBridge. Please preserve this attribution as required by the applicable license terms.

# Simply Plural Recovery — Technical Notes

## Purpose

This document records the technical findings behind the Simply Plural Android recovery work.

It is intended for:

- developers implementing recovery or import support;
- researchers reviewing the recovery method;
- contributors extending recovery to other Android devices;
- maintainers of other plural applications that want to support recovered Simply Plural data.

For end-user recovery instructions, see:

`SP data extraction instructions.md`

For the command-line extraction sequence, see:

`recover-simplyplural-data.cmd`

---

## Confirmed Test Environment

The recovery procedure has been successfully reproduced on:

```text
Device: Samsung Galaxy S24+
Model: SM-S926U
Platform: Android
Acquisition method: Samsung Smart Switch for Windows
Simply Plural package: com.saltypandastudios.frontime
```

The recovery was performed after the Simply Plural servers had shut down.

Simply Plural still opened on the device and displayed locally retained System data.

No existing official Simply Plural export was required.

No root access was required.

---

## Recovery Architecture

The confirmed recovery path is:

```text
Samsung Galaxy S24+
        ↓
Samsung Smart Switch
        ↓
com.saltypandastudios.frontime.noedata
        ↓
Android Backup payload
        ↓
TAR archive
        ↓
apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
        ↓
UTF-8 JSON
```

The important recovery target is:

```text
app_flutter/simply.db
```

The acquisition method is Samsung-specific.

The `simply.db` target belongs to Simply Plural and is therefore not inherently Samsung-specific.

---

## Smart Switch Backup Artifact

When Simply Plural is selected in Samsung Smart Switch, the application backup contains:

```text
APKFILE\data\com.saltypandastudios.frontime\
    com.saltypandastudios.frontime.noedata
```

The confirmed `.noedata` file begins with:

```text
ANDROID BACKUP
5
0
none
```

The corresponding raw byte representation is:

```text
b'ANDROID BACKUP\n5\n0\nnone\n'
```

For the confirmed backup this header is 24 bytes.

The fields indicate:

```text
ANDROID BACKUP   Android backup magic
5                backup format version
0                payload is not compressed
none             payload is not encrypted
```

After removing the 24-byte header, the remainder is a TAR archive.

The current extraction script assumes this exact confirmed header.

A backup with a different header should not automatically be processed using the same fixed offset.

---

## TAR Contents

The confirmed Smart Switch application archive included:

```text
apps/com.saltypandastudios.frontime/_manifest
apps/com.saltypandastudios.frontime/r/app_flutter
apps/com.saltypandastudios.frontime/r/app_flutter/messages_*.db
apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
apps/com.saltypandastudios.frontime/r/app_flutter/pendingRequests.db
apps/com.saltypandastudios.frontime/r/app_flutter/logs.db
apps/com.saltypandastudios.frontime/r/app_flutter/events.db
apps/com.saltypandastudios.frontime/f/images.db
apps/com.saltypandastudios.frontime/f/images.db-journal
```

Additional application and preference files were also present.

The primary structured-data recovery target is:

```text
apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
```

---

## `simply.db` Format

Despite its `.db` extension, `simply.db` is not a SQLite database.

The recovered production file begins with JSON text:

```text
{"private":{
```

It can be read directly as UTF-8 and parsed using a standard JSON parser.

A normal formatted JSON file can therefore be produced without reverse-engineering a database engine or encryption layer.

---

## Reproduced Collection Inventory

The confirmed Samsung Galaxy S24+ recovery produced the following top-level collections and record counts:

```text
private dict 1
Privates dict 1
users dict 1
FrontHistory dict 32
Users dict 1
Channels dict 6
Members dict 37
fronthistory dict 99
privates dict 1
chatcategories dict 1
customfields dict 7
friends dict 2
friendssettings dict 2
privacybuckets dict 2
members dict 49
channels dict 6
```

Counts are specific to the test System and will vary between users.

Important recovered collections include:

```text
members
fronthistory
customfields
privacybuckets
channels
friends
friendssettings
chatcategories
users
private
```

---

## Duplicate / Generational Collections

The recovered cache contains several collection names in both uppercase and lowercase forms, including:

```text
Members
members

FrontHistory
fronthistory

Channels
channels

Users
users

Privates
privates
```

Observed relationships in the confirmed recovery:

```text
Members       37 records
members       49 records
shared IDs    37

Channels      6 records
channels      6 records
shared IDs    6

FrontHistory  32 records
fronthistory  99 records
shared IDs    0
```

The lowercase `members` collection appears to contain a newer or broader generation of member data for the confirmed System.

This should not yet be generalized into a destructive normalization rule.

A future export-reconstruction implementation should preserve source data and handle collection merging deliberately.

---

## Example Member Structure

Recovered member records are stored in dictionary form.

The dictionary key functions as the member record ID.

Conceptually:

```json
{
  "members": {
    "<MEMBER_ID>": {
      "name": "...",
      "desc": "...",
      "pronouns": "...",
      "avatarUuid": "...",
      "avatarUrl": "...",
      "color": "...",
      "info": {},
      "buckets": []
    }
  }
}
```

This differs structurally from an official Simply Plural export, which represents entity collections as arrays containing explicit `_id` fields.

The recovered JSON is therefore structured Simply Plural data, but it is not yet an official-export-compatible reconstruction.

---

## System Identifier

The recovered cache retains the Simply Plural System/user identifier.

It appears in local data including the `private` collection and avatar URLs.

This means a converter can recover the System identifier without requiring an existing official export.

---

## Avatar Metadata

Recovered member records retain avatar metadata such as:

```text
avatarUuid
avatarUrl
```

The Smart Switch application backup also contains:

```text
apps/com.saltypandastudios.frontime/f/images.db
```

`images.db` is a SQLite database containing a cache index.

Observed schema:

```text
cacheObject
    _id
    url
    key
    relativePath
    eTag
    validTill
    touched
    length
```

For a member with a known `avatarUuid`, the cache index can provide a record such as:

```text
https://spaces.apparyllis.com/avatars/<SYSTEM_ID>/<AVATAR_UUID>
```

and a corresponding cached filename such as:

```text
3a7b29a0-4c66-11ef-99d7-cb5f4a67e2a2.bin
```

This provides a machine-readable relationship:

```text
member
    ↓
avatarUuid
    ↓
images.db
    ↓
Simply Plural avatar URL
    ↓
local cache filename
```

---

## Avatar Cache Limitation

The confirmed Smart Switch application backup contained the `images.db` cache index but did not contain the referenced `.bin` cache files.

Therefore:

```text
member → avatarUuid → cache record
```

is recoverable, but:

```text
cache record → cached image bytes
```

was not recovered through this Smart Switch application backup.

---

## Smart Switch Image Backup Findings

When the Smart Switch **Images** category was also selected, Smart Switch preserved files from:

```text
/mnt/sdcard/DCIM/SimplyPlural/
```

These included recognizable original/source images associated with the Simply Plural System.

This is potentially valuable because the recovered files may retain more image data than cropped or resized application-cache versions.

However, the current backup does not contain a confirmed machine-readable mapping between:

```text
DCIM/SimplyPlural/<SOURCE_FILENAME>
```

and:

```text
member.avatarUuid
```

Avatar/source-image reconstruction therefore remains under investigation.

---

## ADB Findings

Standard ADB access alone did not provide access to Simply Plural's private application data on the confirmed production installation.

The package returned:

```text
run-as: package not debuggable: com.saltypandastudios.frontime
```

Therefore the confirmed stock-device recovery path does not rely on `run-as`.

The successful private-data acquisition was performed by Samsung Smart Switch.

---

## Non-Samsung Android Devices

The recovered target:

```text
app_flutter/simply.db
```

is a Simply Plural application artifact.

It is not inherently Samsung-specific.

What is currently Samsung-specific is the proven acquisition method:

```text
Samsung Smart Switch → .noedata → simply.db
```

Equivalent acquisition paths for non-Samsung Android devices have not yet been confirmed.

Any mechanism that can safely acquire the Simply Plural application data and recover `app_flutter/simply.db` should be able to use the same JSON-processing work after that point.

---

## Privacy Model

Needs of the Many / PluralBridge does not need access to a user's recovered System data.

The intended architecture is:

```text
user's device
    ↓
local recovery
    ↓
recovered Simply Plural data
    ↓
user's chosen plural application
```

Recovery tools should operate locally whenever possible.

Users should not be instructed to send Needs of the Many / PluralBridge:

```text
.noedata files
simply.db
recovered JSON
official exports
avatar files
passwords
tokens
screenshots containing private System information
```

---

## Integration Goal

Plural applications that already support official Simply Plural imports can potentially support a second input path:

```text
Official Simply Plural export
        ↓
existing importer
```

or:

```text
Recovered Android application data
        ↓
recovery / normalization layer
        ↓
Simply Plural-compatible data
        ↓
existing importer
```

Needs of the Many / PluralBridge intends to publish the technical information, documentation, scripts, mappings, and reference implementation needed to support this work.

---

## Current Status

### Confirmed

- Samsung Galaxy S24+ recovery
- Samsung Smart Switch acquisition
- `.noedata` extraction
- Android Backup header identification
- TAR extraction
- recovery of `app_flutter/simply.db`
- confirmation that `simply.db` is UTF-8 JSON
- structured member and front-history recovery
- recovery of additional Simply Plural collections
- recovery of avatar UUID metadata
- recovery of the `images.db` avatar-cache index
- preservation of recognizable files from `DCIM/SimplyPlural` when Images are included in Smart Switch

### In Progress

- reconstruction of an official-export-compatible JSON structure
- avatar/source-image mapping
- avatar recovery
- import support for recovered data
- testing acquisition methods for additional Android manufacturers

### Not Yet Confirmed

- equivalent stock recovery path on non-Samsung Android devices
- complete avatar recovery
- direct machine-readable mapping from recovered `DCIM/SimplyPlural` filenames to member records
- universal behavior across Simply Plural versions and Android versions

---

## Preservation Principle

The recovery process should preserve the original acquired files unchanged.

Conversion, normalization, export reconstruction, and import processing should operate on copies.

The raw recovery artifacts may contain information that later versions of the tooling can interpret even if the first converter does not understand it.

---

PluralBridge is an independent project by Needs of the Many.

PluralBridge is not affiliated with Simply Plural or Apparyllis.
````
