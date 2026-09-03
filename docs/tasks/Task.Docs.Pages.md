# Task.Docs.Pages

## Description

Publish chosen schema documentation pages. The first cut copies selected notes from private `Working\Architecture\schema\` into public PluralBridge `docs\schema\` as public-safe format/shape pages. Private originals stay in PluralBridge-Private. Unchosen files stay private. `website\docs.html` is not part of this first cut. The assistant does not run git.

---

## Step Docs.Pages.1 — define public destination and privacy boundary

### Description

Define where first-cut schema pages are published and what must be excluded from every public copy.

### Commands

- Design text shown in chat.

### Done When

- Public destination is `PluralBridge\docs\schema\`.
- Private source is `PluralBridge-Private\Working\Architecture\schema\`.
- A published page is defined as a format/shape note, not a dump of a real export or backup.
- Public copies must not include tokens, passwords, export files, screenshots, or private System data.
- Public copies must not include real member names, personal phone paths, Windows usernames, IMEI, device serials, signed URLs, Discord/Gmail attachments, identifying sample counts, or avatar-extraction ADB.
- `website\docs.html` is out of this first cut.
- No schema file has been copied yet.

---

## Step Docs.Pages.2 — choose the first-cut file list

### Description

Choose which private schema files belong in the first cut. Later steps handle one chosen file at a time. Unchosen files are skipped, not deleted from private.

### Commands

- Design text shown in chat.

### Done When

- Each candidate is marked chosen or skipped:
  - `simply-plural-export-format.md`
  - `simply-plural-export-format-updated-from-pluralkit.md`
  - `pluralbridge-db-schema-format.md`
  - `samsung-smart-switch-simply-db-format.md`
  - `samsung-smart-switch-backup-layout.md`
  - `samsung-smart-switch-photo-info-format.md`
  - `samsung-smart-switch-sp-recovery.md`
  - `PluralBridge-DB-Schema.png`
- Each chosen file maps to one later step in this task.
- Unchosen files remain only in PluralBridge-Private.
- No file has been copied yet.

---

## Step Docs.Pages.3 — confirm or refresh simply-plural-export-format.md

### Description

Only if chosen. Compare the private `simply-plural-export-format.md` with the public `docs\schema\` copy and keep or refresh the public file.

### Commands

- Markdown shown in chat.
- Manual compare in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or the public file is the intended published version.
- The public page remains an observed official Simply Plural export JSON shape note, not a PluralBridge ontology.
- The public file has no tokens, passwords, real exports, screenshots, private System data, member names, or personal paths.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.4 — confirm or refresh pluralbridge-db-schema-format.md

### Description

Only if chosen. Compare the private `pluralbridge-db-schema-format.md` with the public `docs\schema\` copy and keep or refresh the public file.

### Commands

- Markdown shown in chat.
- Manual compare in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or the public file is the intended published version.
- The public page remains a working SQL schema shape note.
- The public file has no populated System rows, member names, notes, tokens, or personal paths.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.5 — confirm or refresh samsung-smart-switch-simply-db-format.md

### Description

Only if chosen. Compare the private `samsung-smart-switch-simply-db-format.md` with the public `docs\schema\` copy and keep or refresh the public file.

### Commands

- Markdown shown in chat.
- Manual compare in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or the public file is the intended published version.
- The public page remains a simply.db JSON shape note, not a personal Smart Switch how-to or ADB walkthrough.
- The public file has no personal backup path, username, IMEI, signed URLs, member names, or identifying sample counts.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.6 — confirm PluralBridge-DB-Schema.png

### Description

Only if chosen. Confirm the public schema diagram is the intended published image, or replace it from a public-safe source you name.

### Commands

- Image inspect shown in chat.

### Done When

- The step is skipped because the file is not in the first-cut list, or the public image is the intended published diagram.
- The image shows table and relationship structure only.
- The image has no private System data or member names.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.7 — publish simply-plural-export-format-updated-from-pluralkit.md

### Description

Only if chosen. Create a public-safe copy of the private updated export-format note in public `docs\schema\`.

### Commands

- Markdown shown in chat.
- Manual public-safe copy in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or public `docs\schema\simply-plural-export-format-updated-from-pluralkit.md` exists.
- The public page remains a format/shape note, including fills for empty observed arrays if those are part of the intended page.
- The public file has no private System values, tokens, or personal paths.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.8 — publish samsung-smart-switch-backup-layout.md

### Description

Only if chosen. Create a generalized public-safe copy of the private backup-layout note in public `docs\schema\`.

### Commands

- Markdown shown in chat.
- Manual public-safe copy in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or public `docs\schema\samsung-smart-switch-backup-layout.md` exists.
- The public page remains a layout/shape note with useful file-type names.
- The public file has no personal UNC root, username, IMEI, device serial, phone number, or identifying folder inventory.
- The public file does not present one personal Galaxy backup as a spec.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.9 — publish samsung-smart-switch-photo-info-format.md

### Description

Only if chosen. Create a public-safe copy of the private PHOTO_INFO format note in public `docs\schema\`.

### Commands

- Markdown shown in chat.
- Manual public-safe copy in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or public `docs\schema\samsung-smart-switch-photo-info-format.md` exists.
- The public page remains a PHOTO_INFO.json shape note, not a photo catalog dump and not the simply.db format note.
- The public file has no personal file names, paths, download URIs, or identifying sample counts.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.10 — publish samsung-smart-switch-sp-recovery.md

### Description

Only if chosen. Create a generalized public-safe copy of the private recovery roadmap in public `docs\schema\`.

### Commands

- Markdown shown in chat.
- Manual public-safe copy in editor.

### Done When

- The step is skipped because the file is not in the first-cut list, or public `docs\schema\samsung-smart-switch-sp-recovery.md` exists.
- The public page remains a generalized developer recovery note.
- The public page still treats the official export format as the reference, not a replacement.
- The public file has no personal backup path, username, IMEI, member names, identifying sample counts, signed URLs, or avatar-extraction ADB.
- The private original remains in PluralBridge-Private.
- Git is left for the user in Git Bash.

---

## Step Docs.Pages.11 — verify first-cut schema pages

### Description

Verify the first cut is complete: chosen public-safe copies exist under `docs\schema\`, unchosen files remain private, and no website page was edited.

### Commands

- Manual review of public `docs\schema\`.
- Git Bash commands shown for status, staging, commit, and push after verification.

### Done When

- Every candidate from Docs.Pages.2 is either published/confirmed or marked skipped.
- Every chosen public file is under `PluralBridge\docs\schema\`.
- Unchosen private schema files are still only in PluralBridge-Private.
- `website\docs.html` is unchanged.
- No other website page was changed in this first cut.
- The privacy exclusions from Docs.Pages.1 still hold on every public file in this cut.
- The user has run any git status, diff, commit, or push from Git Bash.
