# Use the Simply Plural REST API with a Token

This guide explains the token-based REST access pattern used by PluralBridge export tooling.

PluralBridge uses a user-created Simply Plural/Apparyllis API token only to export user-owned data from Simply Plural.

SP_TOKEN is export-only. It is not a PluralBridge credential.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Overview

Simply Plural data can be exported through Apparyllis REST API endpoints when the user supplies a Simply Plural API token with Read permission.

The basic workflow is:

1. Create a Simply Plural API token with Read permission.
2. Store the token locally as `SP_TOKEN`.
3. Send the token in the HTTP `Authorization` header.
4. Save the API responses as local JSON files.
5. Keep the exported files private unless deliberately redacted for publication.

## Token handling

Treat the token like a password.

Do not post it publicly. Do not commit it to Git. Do not paste it into screenshots. Do not place it in shared logs.

In Git Bash, enter the token without displaying it on screen:

```bash
read -s -p "Paste Simply Plural token: " SP_TOKEN
echo
export SP_TOKEN
```

In Git Bash, the `-s` option keeps the token from being displayed while you paste it.

For macOS Terminal / zsh users, use these commands instead:

```sh
printf "Paste Simply Plural token: "
stty -echo
IFS= read -r SP_TOKEN
stty echo
printf "\n"
export SP_TOKEN
```

Run the appropriate block in the same terminal window where you will continue the export guide.

Check that a token value was stored without printing the token itself:

```bash
printf 'Token length: %s\n' "${#SP_TOKEN}"
```

Expected result:

```text
Token length: <number>
```

## API base URL

Use the Apparyllis API base URL:

```text
https://api.apparyllis.com
```

A convenient Git Bash variable is:

```bash
export SP_API='https://api.apparyllis.com'
```

## Authorization header

PluralBridge uses the token directly in the `Authorization` header:

```bash
-H "Authorization: ${SP_TOKEN}"
```

Do not add `Bearer` unless a future API change explicitly requires it.

## Verify the token

Test the token with `/v1/me`:

```bash
curl -i -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me"
```

Expected result:

```text
HTTP/2 200
```

The response body should contain JSON for the authenticated user/system. If the response is `401 Unauthorized`, revoke the token, create a fresh token with Read permission, and try again.

## Save an API response to a JSON file

Example: save `/v1/me` as `me.json`:

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me" \
  -o me.json
```

Inspect the result:

```bash
head -40 me.json
```

## Common export endpoints

Common endpoints used or investigated by PluralBridge include:

```text
GET /v1/me
GET /v1/user/{uid}
GET /v1/members/{uid}
GET /v1/groups/{uid}
GET /v1/customFronts/{uid}
GET /v1/frontHistory
GET /v1/frontHistory/{uid}?startTime=0&endTime=9999999999999
GET /v1/timers/automated/{uid}
GET /v1/timers/repeated/{uid}
GET /v1/polls/{uid}
GET /v1/customFields/{uid}
GET /v1/privacyBuckets
GET /v1/filters
GET /v1/chat/categories
GET /v1/chat/channels
GET /v1/friends/
GET /v1/notes/{uid}/{member_id}
```

## Saving output files

Use stable, boring filenames that describe the endpoint or dataset:

```text
me.json
user.json
members.json
groups.json
frontHistory.json
customFields.json
privacyBuckets.json
filters.json
friends.json
chat_categories.json
chat_channels.json
notes_by_member.json
```

Avoid embedding member display names, decorator names, token fragments, or account identifiers in generated filenames.

## Clean JSON and wrappers

Some API responses may contain wrapper fields such as `id`, `exists`, or `content`. Preserve the raw JSON first. Any cleanup or projection should happen after the raw export is safely saved.

The preferred preservation model is:

1. Save raw API JSON exactly as returned.
2. Build derived cleaned JSON only from the raw files.
3. Keep raw exports as the evidence-grade source of truth.

## Security notes

- Keep `SP_TOKEN` private.
- Keep raw exports private unless deliberately redacted.
- Do not commit tokens or private export data to source control.
- Use synthetic or redacted fixtures for tests and documentation.
- Rotate the token if it is exposed.

