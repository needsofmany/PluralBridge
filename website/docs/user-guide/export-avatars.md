# Export Simply Plural Member Avatars

This guide explains how to retrieve Simply Plural member avatar images using REST commands from a Bash shell.

The procedure creates local image files named by Simply Plural member ID. This keeps avatar filenames data-stable and avoids placing member or decorator display names into the filesystem.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Purpose

This document explains how to retrieve Simply Plural member avatar images using REST commands from a Bash shell. It follows the same working model as the JSON data extraction and SQL Server database creation guides: inspect first, save outputs to files, validate each result, and avoid embedding decorator/member display names in generated filenames.

The procedure creates local image files named by Simply Plural member ID. This keeps avatar filenames data-stable and avoids putting member/decorator names into the filesystem.


## Required tools

Bash shell, such as Git Bash on Windows.

curl, used to execute the REST calls.

Python, used instead of jq to parse JSON because jq was not installed in the working environment.

file command, used to verify the downloaded images and identify image types.

A Simply Plural API token with Read permission.

## Create and enter the Simply Plural token

In Simply Plural, create a token from Settings -> Account -> Tokens. Enable Read permission. In Chrome, press and hold the token value to copy it. If the token begins with a prefix such as TBD///, keep the full value exactly as copied.

Enter the token into Bash without displaying it on screen:

```bash
set +H
export SP_API='https://api.apparyllis.com'

read -s -p "Paste NEW Simply Plural token: " SP_TOKEN
echo
export SP_TOKEN
printf 'Token length: %s\n' "${#SP_TOKEN}"
```

## Test API authorization

This REST call verifies that the token works before any member or avatar extraction begins.

```bash
curl -i -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me"
```

Expected result: HTTP 200 with JSON. If the response is HTTP 401 Unauthorized and says the authorization token is missing or invalid, revoke the token and create a fresh token with Read permission.

## Prepare the local folders

Create the avatar output folder and move into the export working folder.

```bash
set +H
cd exports
mkdir -p member_images
```

## Retrieve the account/system record

This saves the authenticated account/system record as me_api.json.

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me" \
  -o me_api.json
```

Inspect the file if needed:

```bash
cat me_api.json
```

## Extract the system/user ID without jq

The local Bash environment did not have jq installed, so Python is used to extract the ID safely.

```bash
export SP_UID="$(
python - <<'PY'
import json

with open("me_api.json", "r", encoding="utf-8") as f:
    data = json.load(f)

value = (
    data.get("id")
    or data.get("uid")
    or data.get("content", {}).get("id")
    or data.get("content", {}).get("uid")
)

print(value or "")
PY
)"

printf 'SP_UID=%s\n' "$SP_UID"
```

Expected result: SP_UID prints a non-empty ID. If it prints blank, inspect me_api.json before continuing.

## Retrieve the member list

This REST call retrieves all members for the authenticated system and saves the result as members_api.json.

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/members/${SP_UID}" \
  -o members_api.json
```

Validate that the file contains JSON member records:

```bash
head -20 members_api.json
```

## Observed member avatar fields

The retrieved member records used this useful structure:

```json
{
  "id": "<member_id>",
  "content": {
    "uid": "<system_uid>",
    "avatarUrl": "",
    "avatarUuid": "<avatar_uuid>"
  }
}
```

Most member records had an empty avatarUrl and a populated avatarUuid. Therefore the working image URL is constructed from content.uid and avatarUuid:

```bash
https://spaces.apparyllis.com/avatars/<content.uid>/<avatarUuid>
```

## Build the member avatar URL list

This Python block creates member_avatar_urls.tsv. The file contains only member IDs and avatar URLs. It does not place member/decorator display names into the generated filenames.

```bash
cd exports

python - <<'PY'
import json

with open("members_api.json", "r", encoding="utf-8") as f:
    members = json.load(f)

with open("member_avatar_urls.tsv", "w", encoding="utf-8", newline="\n") as out:
    for row in members:
        member_id = row.get("id", "")
        c = row.get("content", {}) or {}

        uid = c.get("uid", "")
        avatar_uuid = c.get("avatarUuid", "")
        avatar_url = c.get("avatarUrl", "")

        if avatar_url:
            url = avatar_url
        elif uid and avatar_uuid:
            url = f"https://spaces.apparyllis.com/avatars/{uid}/{avatar_uuid}"
        else:
            continue

        out.write(f"{member_id}\t{url}\n")
PY
```

Verify the count and sample contents:

```bash
wc -l member_avatar_urls.tsv
head member_avatar_urls.tsv
```

The avatar manifest count may be lower than the member count because some members may not have an avatar image reference.

## Download the avatar images

This loop downloads each avatar image and saves it as <member_id>.img. The temporary .img extension is used because the downloaded content type is verified afterward.

```bash
mkdir -p member_images

while IFS=$'\t' read -r member_id avatar_url
do
  curl -L -sS "$avatar_url" \
    -o "member_images/${member_id}.img"
done < member_avatar_urls.tsv
```

## Verify downloaded image types

This checks the first downloaded files. In the verified run, the files reported as PNG image data, 512 x 512.

```bash
file member_images/*.img | head
```

Expected output looks like this:

```text
member_images/<member_id>.img: PNG image data, 512 x 512, 8-bit/color RGB, non-interlaced
```

## Rename .img files to real image extensions

This version renames files according to their actual MIME type.

```bash
cd member_images

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

If every downloaded file verifies as PNG, this simpler rename is also acceptable after verification:

```bash
cd member_images

for f in *.img
do
  mv "$f" "${f%.img}.png"
done
```

## Final verification

Count the downloaded image files:

```bash
find member_images -maxdepth 1 -type f | wc -l
```

Expected result: a number matching the count of downloaded avatar files.

List the downloaded files:

```bash
find member_images -maxdepth 1 -type f | sort
```

