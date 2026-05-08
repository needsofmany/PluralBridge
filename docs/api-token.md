# Simply Plural API Token

PluralBridge uses a user-created Simply Plural / Apparyllis API token to export data through public Apparyllis REST API endpoints.

The token is private. Treat it like a password.

## What the Token Does

The API token allows scripts to request Simply Plural data for the account that created the token.

Depending on the endpoint used, this may include:

- account/system metadata
- members
- fronting history
- custom fields
- privacy buckets
- friends
- notes
- avatar references

Do not share your token publicly.

## What Not To Do

Do not:

- commit your token to Git
- paste your token into GitHub issues or discussions
- include your token in screenshots
- send your token to someone you do not completely trust
- place your real token in example files
- store your token in files that are not listed in `.gitignore`

## Environment Variable

PluralBridge examples use this environment variable:

```text
SP_TOKEN
```

In Git Bash, set it like this:

```bash
read -s -p "Paste Simply Plural token: " SP_TOKEN
echo
export SP_TOKEN
```

The `-s` option keeps the token from being displayed while you paste it.

Check that a token value was stored without printing the token itself:

```bash
printf 'Token length: %s\n' "${#SP_TOKEN}"
```

## Authorization Header

The token is sent directly in the `Authorization` header.

Example:

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "https://api.apparyllis.com/v1/me" \
  -o me.json
```

Do not add a `Bearer` prefix unless the official API documentation changes.

## Important Copying Note

When copying the token from Simply Plural, make sure you copy the full token value.

Some tokens may include a visible prefix. Use the complete token exactly as shown.

## Basic Token Test

Use this command to test whether the token works:

```bash
curl -i \
  -H "Authorization: ${SP_TOKEN}" \
  "https://api.apparyllis.com/v1/me"
```

A working token should return account data.

A missing or invalid token may return:

```text
HTTP/1.1 401 Unauthorized
Authorization token is missing or invalid.
```

## If a Token Is Exposed

If you accidentally publish or share a token:

1. revoke or invalidate the exposed token
2. create a new token
3. update your local environment or local config file
4. check Git history before pushing again

If the token was committed to Git, removing it from the latest file is not enough. Git history may still contain the old token.

## Local Config Files

Future PluralBridge scripts may also support local config files.

Use private local config names such as:

```text
config.local.json
.env
```

These files should stay out of Git.

The repository `.gitignore` should exclude them.
