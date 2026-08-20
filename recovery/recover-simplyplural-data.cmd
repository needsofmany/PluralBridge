rem Copyright © 2026 Needs of the Many / PluralBridge contributors

rem Licensed under the GNU General Public License v3.0 (GPLv3).

rem This recovery implementation and accompanying documentation were
rem developed by Needs of the Many / PluralBridge. Please preserve this
rem attribution as required by the applicable license terms.

rem Start with step 6 from the extract markdown file

rem Required path placeholders:
rem <WINDOWS_USER>   = Windows account name
rem <DEVICE_MODEL>   = Smart Switch device model folder
rem <DEVICE_FOLDER>  = Smart Switch device-specific folder
rem <BACKUP_FOLDER>  = Smart Switch timestamped backup folder

rem Step 6 — Create a clean working folder
mkdir C:\SP-Recovery-Repro

rem Step 7 — Copy the .noedata file into the working folder
copy "C:\Users\<WINDOWS_USER>\Samsung\SmartSwitch\backup\<DEVICE_MODEL>\<DEVICE_FOLDER>\<BACKUP_FOLDER>\APKFILE\data\com.saltypandastudios.frontime\com.saltypandastudios.frontime.noedata" C:\SP-Recovery-Repro\

rem Step 8 — Verify the .noedata header
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery-Repro\com.saltypandastudios.frontime.noedata'); print(p.read_bytes()[:24])"

rem Step 9 — Strip the Android Backup header and create the TAR
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery-Repro\com.saltypandastudios.frontime.noedata'); Path(r'C:\SP-Recovery-Repro\simplyplural.tar').write_bytes(p.read_bytes()[24:])"

rem Step 10 — Confirm the TAR contains Simply Plural’s local database
tar -tf C:\SP-Recovery-Repro\simplyplural.tar | findstr /I "app_flutter/simply.db"

rem Step 11 — Extract simply.db
tar -xf C:\SP-Recovery-Repro\simplyplural.tar -C C:\SP-Recovery-Repro apps/com.saltypandastudios.frontime/r/app_flutter/simply.db

rem Step 12 — Confirm simply.db is JSON
python -c "from pathlib import Path; p=Path(r'C:\SP-Recovery-Repro\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db'); print(p.read_text(encoding='utf-8')[:40])"

rem Step 13 — Convert simply.db into a normal UTF-8 .json file
python -c "import json; src=r'C:\SP-Recovery-Repro\apps\com.saltypandastudios.frontime\r\app_flutter\simply.db'; dst=r'C:\SP-Recovery-Repro\simply.pretty.utf8.json'; d=json.load(open(src,encoding='utf-8')); json.dump(d,open(dst,'w',encoding='utf-8'),ensure_ascii=False,indent=2)"

rem Step 14 — Validate the JSON file
python -m json.tool C:\SP-Recovery-Repro\simply.pretty.utf8.json > nul

rem Step 15 — Confirm the recovered JSON contains the expected Simply Plural collections
python -c "import json; d=json.load(open(r'C:\SP-Recovery-Repro\simply.pretty.utf8.json',encoding='utf-8')); [print(k, type(v).__name__, len(v) if hasattr(v,'__len__') else '') for k,v in d.items()]"