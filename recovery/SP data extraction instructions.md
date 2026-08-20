> **Copyright © 2026 Needs of the Many / PluralBridge contributors**
>
> Licensed under the GNU General Public License v3.0 (GPLv3).
>
> This recovery implementation and accompanying documentation were developed by Needs of the Many / PluralBridge. Please preserve this attribution as required by the applicable license terms.

# Simply Plural Android Data Recovery — Samsung Smart Switch Path A

## Scope

This procedure documents a Simply Plural data-recovery path successfully reproduced on a Samsung Galaxy S24+ after the Simply Plural servers shut down.

It recovers Simply Plural's locally stored structured data and converts it into a normal UTF-8 JSON file.

This procedure:

- does not require root access;
- does not require ADB access to Simply Plural's private app directory;
- does not require an existing Simply Plural export;
- keeps the recovered data local on the user's computer.

The resulting JSON is recovered Simply Plural local data. It is not yet a reconstructed official Simply Plural export.

## Requirements

- Samsung Galaxy phone with Simply Plural still installed
- Windows PC
- Samsung Smart Switch
- Python 3 available from Command Prompt
- Windows `tar` command

> **Important:** Do not uninstall Simply Plural, clear its app data, or factory-reset the phone before attempting recovery.

---

## Step 1 — Back up Simply Plural with Samsung Smart Switch

Connect the Samsung phone to the PC and open Samsung Smart Switch.

Choose **Backup**.

In the backup-selection screen:

1. Select **Apps**.
2. Select **Simply Plural**.
3. Deselect other apps if you want the smallest possible backup.

Selecting **Images** is optional for recovering the JSON, but is recommended if you also want to preserve possible Simply Plural-related images for later avatar-recovery work.

Run the backup.

**Expected**

Smart Switch reports that the Simply Plural backup completed successfully.

---

## Step 2 — Locate the Simply Plural `.noedata` file

Open the newly created Smart Switch backup folder.

The Simply Plural backup payload should be under a path similar to:

```text
C:\Users\<WINDOWS_USER>\Samsung\SmartSwitch\backup\<DEVICE_MODEL>\<DEVICE_FOLDER>\<BACKUP_FOLDER>\APKFILE\data\com.saltypandastudios.frontime\
```

Inside that folder, locate:

```text
com.saltypandastudios.frontime.noedata
```

**Expected**

The `.noedata` file exists.

---

## Step 3 — Create a clean recovery working folder

Open Command Prompt and run:

```cmd
mkdir C:\SP-Recovery
```

**Expected**

No output if the directory is created successfully.

If the folder already exists from a previous recovery attempt, use a different empty folder.

---

## Step 4 — Copy the `.noedata` file into the working folder

Run:

```cmd
copy "<FULL_PATH_TO_BACKUP>\com.saltypandastudios.frontime.noedata" C:\SP-Recovery\
```

**Expected**

```text
        1 file(s) copied.
```

---

## Step 5 — Verify the Android Backup header

Run:

```cmd
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery\com.saltypandastudios.frontime.noedata'); print(p.read_bytes()[:24])"
```

**Expected on the confirmed Samsung Galaxy S24+ recovery**

```text
b'ANDROID BACKUP\n5\n0\nnone\n'
```

This indicates:

- Android Backup version 5
- uncompressed payload
- unencrypted payload

**If your header differs, stop before continuing.**

The extraction command below assumes this exact 24-byte header.

---

## Step 6 — Remove the Android Backup header and create a TAR archive

Run:

```cmd
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery\com.saltypandastudios.frontime.noedata'); Path(r'C:\SP-Recovery\simplyplural.tar').write_bytes(p.read_bytes()[24:])"
```

**Expected**

No output.

The following file should now exist:

```text
C:\SP-Recovery\simplyplural.tar
```

---

## Step 7 — Confirm that the TAR contains `simply.db`

Run:

```cmd
tar -tf C:\SP-Recovery\simplyplural.tar | findstr /I "app_flutter/simply.db"
```

**Expected**

```text
apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
```

---

## Step 8 — Extract `simply.db`

Run:

```cmd
tar -xf C:\SP-Recovery\simplyplural.tar -C C:\SP-Recovery apps/com.saltypandastudios.frontime/r/app_flutter/simply.db
```

**Expected**

No output.

The recovered file should now exist at:

```text
C:\SP-Recovery\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db
```

---

## Step 9 — Confirm that `simply.db` contains JSON

Run:

```cmd
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db'); print(p.read_text(encoding='utf-8')[:40])"
```

**Expected**

Output beginning with:

```text
{"private":{
```

The characters following that will vary by system.

Despite its `.db` filename extension, `simply.db` contains JSON text.

---

## Step 10 — Create a normal UTF-8 `.json` file

Run:

```cmd
python -c "import json; src=r'C:\SP-Recovery\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db'; dst=r'C:\SP-Recovery\simply.pretty.utf8.json'; d=json.load(open(src,encoding='utf-8')); json.dump(d,open(dst,'w',encoding='utf-8'),ensure_ascii=False,indent=2)"
```

**Expected**

No output.

The resulting JSON file should be:

```text
C:\SP-Recovery\simply.pretty.utf8.json
```

---

## Step 11 — Validate the JSON syntax

Run:

```cmd
python -m json.tool C:\SP-Recovery\simply.pretty.utf8.json > nul
```

**Expected**

No output.

No output means Python successfully parsed the file as valid JSON.

---

## Step 12 — Display the recovered collections and record counts

Run:

```cmd
python -c "import json; d=json.load(open(r'C:\SP-Recovery\simply.pretty.utf8.json',encoding='utf-8')); [print(k, type(v).__name__, len(v) if hasattr(v,'__len__') else '') for k,v in d.items()]"
```

**Expected**

The exact collections and counts will vary by system.

In the Samsung Galaxy S24+ system used to reproduce this procedure, the result was:

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

Important recovered collections included:

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

For this reproduced recovery, the JSON contained:

- 49 records in the lowercase `members` collection
- 99 records in the lowercase `fronthistory` collection
- 7 custom-field records
- 2 privacy-bucket records
- 6 channel records
- 2 friend records
- additional system and cached Simply Plural collections

At this point, the structured Simply Plural data has been successfully recovered from the Samsung Smart Switch backup and converted into a validated UTF-8 JSON file.
