# Simply Plural from a Samsung backup — importer roadmap

When Simply Plural’s servers shut down on **1 July 2026**, anyone who had not already used **Settings → Export** was left without the supported export path. We set out to find out whether the data still stored locally on a user’s phone could be recovered.

We found that it could. The phone still retained the same System data, including members, front history, and privacy information.

We created this roadmap for developers who already support Simply Plural exports, as well as developers who want to add that capability to an app they are building. The goal is to provide another way to salvage user data after the Simply Plural shutdown.

Our first successful recovery was from one **personal** Samsung Galaxy S24+. File counts and sizes in this document are **example numbers from that one backup**, not typical values and not a complete phone dump. Other devices and other Smart Switch selections will differ. Reuse the layout and file types; do not treat these quantities as a spec.

We recognize that this represents only a subset of Android devices, but we hope much of the technical and architectural information we uncovered will also help with recovery work on other Android phones, including devices such as the Pixel.

This is a living document. We are sharing it with the developer community in the hope that the work can benefit the larger Plural community.

This recovery path is not a replacement for importing an official Simply Plural export. The official export format should remain the source of truth and the reference point for sanity-checking any alternate recovery method.

In addition to this roadmap, we are providing the following schema documents:

- `simply-plural-export-format.md` — official export JSON
- `samsung-smart-switch-simply-db-format.md` — phone `simply.db` JSON

---

## Why this path exists

Before shutdown, Apparyllis provided an official export as the supported method for retrieving Simply Plural data. After the service shut down, that export path was no longer available.

The Android app (`com.saltypandastudios.frontime`, displayed as Simply Plural) nevertheless retained a local offline copy of System data.

On a stock Samsung device, Android’s application sandbox prevents that private data from being retrieved with `adb pull`. Samsung Smart Switch provides another route because Simply Plural was configured with `allowBackup=true`.

What we actually recovered from that backup:

- **System data** — `simply.db`, which is JSON despite the name

A product that already supports Simply Plural exports can reuse much of the same import pipeline for recovered data.

The recovered `simply.db` can be normalized into the product’s existing internal model. Member records include avatar references (`avatarUuid` / `avatarUrl`). The app’s cached avatar files are not in this backup. **In-app Settings → Export is a dead end** after shutdown.

Users who still have an official Simply Plural export should continue to use that as the preferred source.

For users who do not have an export, the Smart Switch recovery path provides an alternative that has been confirmed on the Samsung Galaxy S24+.

---

## Roadmap if you are adding this alongside an export importer

1. **Keep the export importer.** Preserve the same contracts and the same tests. This recovery path is additive.
2. **Accept a Smart Switch backup folder** or a pre-extracted `simply.db`. If `.noedata` is plaintext, as it was in the case we proved, do not require the user to decrypt Samsung `.data`.
3. **Parse `simply.db` as JSON with duplicate keys preserved.** Standard `json.load` / `ConvertFrom-Json` will drop `Users` vs `users`, along with the other case-sensitive pairs. The sibling simply.db note contains the field map. Do not assume the file is SQLite.
4. **Do not expect avatar image files in `simply.db`.** Member records contain `avatarUuid` / `avatarUrl`. The Flutter cache index (`images.db`) may list URLs, but the `.bin` files are usually missing because Android omits `cache/` from backup. In-app Settings → Export is a dead end after shutdown.
5. **Ship Samsung support first.** This backup is selective EasyMover data, not a full-phone dump. iOS recovery using an unencrypted iTunes/Finder backup and non-Samsung Android recovery are separate work.
6. **Never ingest secrets into product logs or a repository.** `shared_prefs` can contain access and refresh keys. They are not required to read `simply.db`.

If you only need to pull one backup apart by hand, the next section documents the extraction process we actually used.

A production importer should automate this process rather than require every user to run Python manually.

---

## If you just need the files off one phone

Leave the app installed. Do not clear its storage.

Use Smart Switch to back up the Galaxy to a PC, and include **Simply Plural**.

You are looking for the System data:

| What | Where | What it actually is |
|---|---|---|
| Members, fronts, and the rest of the System | `.noedata` → `simply.db` | JSON |

Cached avatar `.bin` files are not in this backup. In-app Settings → Export is a dead end after shutdown.

This process requires no root access, no `adb` access into `/data/data`, no SQLCipher, and no server.

### Find the backup

The backup root is usually:

`...\Samsung\SmartSwitch\backup\<model>\<session>\<timestamp>\`

The file you want is:

`APKFILE\data\com.saltypandastudios.frontime\com.saltypandastudios.frontime.noedata`

Copy it. Leave the original untouched.

### Check the header

The first four lines should be:

```text
ANDROID BACKUP
5
0
none
```

`none` means this file can be unpacked using the process described here.

If the header says `AES-256`, you are looking at a different container. Stop there; this procedure does not apply to that file.

### Skip the first 24 bytes, treat the remainder as a tar archive, and extract `simply.db`

```powershell
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery\com.saltypandastudios.frontime.noedata'); Path(r'C:\SP-Recovery\simplyplural.tar').write_bytes(p.read_bytes()[24:])"
tar -tf C:\SP-Recovery\simplyplural.tar
tar -xf C:\SP-Recovery\simplyplural.tar -C C:\SP-Recovery apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
```

You should now have:

`C:\SP-Recovery\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db`

The file begins with:

```text
{"private":{...
```

Keep the recovered file as-is.

If you want a copy with a `.json` extension, copy the bytes directly:

```powershell
Copy-Item simply.db simply.json
```

Do not pretty-print the file through a normal JSON parser.

### Avatars

This Smart Switch payload does not contain the app’s cached avatar files. Android does not back up `cache/`. `simply.db` and `images.db` may still contain URL or uuid references.

**In-app Settings → Export is a dead end** for this recovery: it is not available in the local app after shutdown.

If avatars still render in the local member list, the image files are still on the phone in the app cache. They are not in the Smart Switch copy.

The overall path looks like this:

```text
phone, with the app still installed
  → Smart Switch
  → .noedata
  → simply.db JSON
```

---

## What Smart Switch puts on disk

The tree and counts in this section are from **one personal Galaxy S24+ backup**. They are an example of what that transfer contained, not a template every importer should expect.

This backup was a selection, not a complete phone backup.

Samsung offered 226 packages on that device. The only app transferred in that sample was Simply Plural, identified as Frontime 1.11.5.

### Color key

- **Green** — use this for import
- **Cyan** — useful for matching; not the data itself
- **Orange** — encrypted or opaque; skip for Simply Plural
- **Red** — noise, failed, or missing

### Backup tree

Counts in this tree are from that one personal S24+ sample.

```text
<timestamp>\
├─ backupHistoryInfo.xml
├─ ReqItemsInfo.json
├─ SmartSwitchBackup.json            (same hash as *_back.json)
├─ SmartSwitchBackup_back.json
│
├─ APKFILE\                          Simply Plural 1.11.5
│  ├─ AppList.json / AppList.bk
│  ├─ *.penc                         encrypted APK; you do not need it
│  ├─ *.png, *_split_config.*.apk
│  └─ data\com.saltypandastudios.frontime\
│       └─ *.noedata                 ANDROID BACKUP / 5 / 0 / none
│            tar data begins at byte 24
│            apps/.../frontime\
│            ├─ r/app_flutter\
│            │  ├─ simply.db         JSON, ~117 KB; import this
│            │  ├─ messages_*.db     JSON
│            │  ├─ pendingRequests.db, logs.db, events.db
│            ├─ f\images.db          SQLite URL index, no image blobs
│            ├─ db\                  Google telemetry; ignore
│            └─ sp\                  preferences / tokens; do not commit
│
└─ (other Smart Switch categories)   media / gallery / Samsung DBs; not SP; skip
```

Approximate size on disk **for that personal S24+ sample**:

| Area | Size |
|---|---|
| APKFILE | ~45 MB |
| SECMP | ~56 MB |
| MEDIASEARCH | ~19 MB |

---

## How the pieces fit together

Smart Switch, also called EasyMover, creates one folder per backup category along with a collection of manifests.

The XML is not always reliable about filenames and sizes. Trust the files on disk over what the metadata claims.

### Why `.noedata` matters

The application directory `/data/user/0/com.saltypandastudios.frontime/` is sandboxed.

On Android 12 and later, `adb backup` produces only a stub for this app.

Smart Switch, however, still created an Android Backup stream.

Inside that backup, `r/` contains the remaining application tree, including Flutter’s `app_flutter` directory. That is where Apparyllis’s SDK writes `simply.db` using `jsonEncode`.

That is why the useful System data appears as JSON under `r/app_flutter/` rather than as SQLite beneath `db/`.

### The other `.db` files

The other `*.db` files alongside `simply.db` are JSON as well, including `pendingRequests`, `logs`, `events`, and `messages`.

`f/images.db` is the exception.

That file is actual SQLite and belongs to Flutter Cache Manager. In this personal S24+ sample, its `cacheObject` table contains 57 rows, with most URLs pointing beneath `spaces.apparyllis.com/avatars/`.

The actual cached image files are gone. Android does not back up `cache/`.

### Avatar references

The combination of `uid` + `avatarUuid` previously corresponded to `spaces.apparyllis.com/avatars/<uid>/…`.

That is a CDN path relationship in `simply.db` / `images.db`. Those URLs are dead after shutdown. Do not put full avatar URLs into Git.

### The rest of the backup

`*.penc` is the encrypted APK. The APK splits are ordinary zip files.

Other Smart Switch categories (Samsung media DBs, gallery catalogs, denylists) are not Simply Plural. Skip them.

---

## Details that can cause problems

### Official export vs this recovery path

**Dead end:** in-app Settings → Export is not available in the local app after shutdown. Do not use it as an avatar-recovery step.

If the user already has JSON matching `simply-plural-export-format.md` from an export made before shutdown, along with an avatar zip if that export included one, that remains the preferred import source. This guide applies specifically to phone → Smart Switch recovery of System JSON.

### Package information

Package: `com.saltypandastudios.frontime`

This package has been used since approximately 2021.

For the backup described here:

- Simply Plural / Frontime version: 1.11.5 / 384
- Source: Play Store
- `AllowBackup`: true
- Android 16
- One UI 8
- Smart Switch 3.7.72.6 on the phone

### `.noedata` vs `.data`

The file we successfully unpacked was plaintext `.noedata`.

An earlier backup on the same PC reportedly included an AES-256 `.data` file alongside it.

If the header says `AES-256`, do not skip 24 bytes and assume the same process will work.

### `adb backup`

`adb backup` is the wrong tool for this recovery path.

Smart Switch is what worked.

### Tokens

`FlutterSharedPreferences` / `FlutterSecureStorage` can contain `access_key`, `refresh_key`, `pkAPI`, and `uid`.

You do not need these values to read `simply.db`.

After shutdown, they may no longer work anyway.

Do not commit them.

### Duplicate keys

Duplicate keys in `simply.db` are real.

Pretty-printing through a normal JSON parser can cause data loss.

See the separate simply.db note for details.

### Not in this tree

`com.apparyllis.simplyplural`, official export JSON, SMS/contacts, Chap2 SQL, cache `.bin` avatars, and iPhone.

### Keep out of git

The backup itself, signed URLs, personal filenames, member names, tokens, IMEI, accounts, and phone numbers.

---

Current location: private `Working\Architecture\schema\`. Public `docs/` was not changed. If this later becomes user-facing recovery docs, that is a separate migrate.
