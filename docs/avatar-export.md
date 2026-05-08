# Avatar Export

PluralBridge can preserve Simply Plural member avatar images when avatar references are available in exported member data.

Avatar export is separate from JSON export. The JSON data contains member records and avatar references. The avatar export step downloads the image files themselves.

## Requirements

You need:

- exported member JSON data
- a Simply Plural / Apparyllis API token if fetching fresh member data
- `curl`
- Python
- a local avatar output folder

For token setup, see:

```text
docs/api-token.md
```

For JSON export, see:

```text
docs/json-export.md
```

## Recommended Output Folder

Recommended local folder:

```text
exports/member_images/
```

This folder should stay out of Git.

The repository `.gitignore` should exclude:

```text
exports/
member_images/
```

## Member Data Shape

Member API responses may contain avatar fields under each member's `content` object.

Observed shape:

```json
[
  {
    "exists": true,
    "id": "member_id_redacted",
    "content": {
      "name": "Member A",
      "uid": "system_uid_redacted",
      "avatarUrl": "",
      "avatarUuid": "avatar_uuid_redacted"
    }
  }
]
```

When `avatarUrl` is present, use it directly.

When `avatarUrl` is empty and both `content.uid` and `content.avatarUuid` are present, the avatar URL can be constructed as:

```text
https://spaces.apparyllis.com/avatars/<system_uid>/<avatar_uuid>
```

## Create Avatar URL List

This script reads exported `members.json` and creates a tab-separated file containing member IDs and avatar URLs.

From the repository root:

```bash
mkdir -p exports/member_images

python - <<'PY'
import json
from pathlib import Path

members_path = Path("exports/json/members.json")
output_path = Path("exports/member_avatar_urls.tsv")

with members_path.open("r", encoding="utf-8") as f:
    members = json.load(f)

with output_path.open("w", encoding="utf-8", newline="\n") as out:
    for row in members:
        member_id = row.get("id", "")
        content = row.get("content", {}) or {}

        system_uid = content.get("uid", "")
        avatar_uuid = content.get("avatarUuid", "")
        avatar_url = content.get("avatarUrl", "")

        if avatar_url:
            url = avatar_url
        elif system_uid and avatar_uuid:
            url = f"https://spaces.apparyllis.com/avatars/{system_uid}/{avatar_uuid}"
        else:
            continue

        out.write(f"{member_id}\t{url}\n")
PY
```

Check how many avatar URLs were found:

```bash
wc -l exports/member_avatar_urls.tsv
```

## Download Avatar Images

Download each avatar image to a neutral filename based on member ID.

```bash
while IFS=$'\t' read -r member_id avatar_url
do
  curl -L -sS "$avatar_url" \
    -o "exports/member_images/${member_id}.img"
done < exports/member_avatar_urls.tsv
```

The `.img` extension is temporary. It avoids guessing the image type before download.

## Identify Image Types

Use the `file` command to inspect downloaded files:

```bash
file exports/member_images/*.img | head
```

Example output may show PNG images:

```text
exports/member_images/member_id_redacted.img: PNG image data, 512 x 512, 8-bit/color RGBA, non-interlaced
```

## Rename Images by MIME Type

Use this version when the image set may contain multiple formats:

```bash
cd exports/member_images

for f in *.img
do
  mime="$(file -b --mime-type "$f")"

  case "$mime" in
    image/png)  mv "$f" "${f%.img}.png" ;;
    image/jpeg) mv "$f" "${f%.img}.jpg" ;;
    image/gif)  mv "$f" "${f%.img}.gif" ;;
    image/webp) mv "$f" "${f%.img}.webp" ;;
    *) echo "Unknown type: $f -> $mime" ;;
  esac
done
```

If inspection proves all downloaded files are PNG files, this simpler rename is enough:

```bash
cd exports/member_images

for f in *.img
do
  mv "$f" "${f%.img}.png"
done
```

## Verify Download Count

Count downloaded image files:

```bash
find exports/member_images -maxdepth 1 -type f | wc -l
```

Compare that count to:

```bash
wc -l exports/member_avatar_urls.tsv
```

The counts should match unless some downloads failed.

## Filename Privacy

Use neutral filenames.

Recommended:

```text
<member_id>.png
```

Avoid filenames based on:

- member names
- decorator names
- display names
- note titles
- private labels

Neutral filenames reduce accidental exposure when folders are viewed, copied, backed up, or screenshotted.

## SQL Server Use

SQL Server can store avatar metadata such as:

- member ID
- avatar UUID
- source URL
- local filename
- local path
- downloaded timestamp

SSMS result grids do not conveniently render image files inline.

A practical approach is to store avatar file paths in SQL Server and use reports, HTML output, Access, Power BI, SSRS, or a small local viewer to render the images.

## Privacy Warning

Avatar images can be highly identifying.

Do not commit real avatar images to Git unless you intentionally chose to make them public.

Before committing, run:

```bash
git status
git diff --cached --stat
git diff --cached
```

Review staged files carefully before pushing.

## Visual Check on Windows

After exporting member avatar images, it is useful to visually confirm that the image files were downloaded correctly.

On Windows:

1. Open File Explorer.
2. Navigate to the avatar image output folder.

Example local folder:

    <repo-folder>\exports\member_images

3. Right-click in the folder background.
4. Select **View**.
5. Choose **Large icons** or **Extra large icons**.
6. Review the images by eye.

This check helps confirm that the downloaded avatar files render correctly and are not empty, corrupted, HTML error pages, or placeholder files.

The avatar image folder contains private exported data. Do not publish screenshots of this folder unless the images are intentionally redacted or synthetic.
