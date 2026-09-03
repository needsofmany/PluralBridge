# Public API Use

PluralBridge uses public Apparyllis REST API endpoints with a user-created Simply Plural / Apparyllis API token.

The project is intended for users preserving their own data from their own Simply Plural accounts.

## Project Boundary

PluralBridge does not require:

- reverse engineering Simply Plural website code
- reverse engineering Simply Plural mobile app code
- decompiling Simply Plural software
- disassembling Simply Plural software
- patching Simply Plural software
- modifying Simply Plural software
- intercepting private traffic
- bypassing authentication
- accessing data without the account holder's consent

PluralBridge should use documented or publicly visible API behavior and normal authenticated requests.

## API Base

The current API base used by PluralBridge examples is:

```text
https://api.apparyllis.com
```

## Authentication

Requests use a user-created API token.

The token is sent directly in the `Authorization` header.

Example:

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "https://api.apparyllis.com/v1/me" \
  -o me.json
```

Do not add a `Bearer` prefix unless the official API documentation changes.

## Basic Account Test

Use `/v1/me` to test authentication:

```bash
curl -i \
  -H "Authorization: ${SP_TOKEN}" \
  "https://api.apparyllis.com/v1/me"
```

A valid token should return account data.

A missing or invalid token may return:

```text
HTTP/1.1 401 Unauthorized
Authorization token is missing or invalid.
```

## Public Documentation and References

Official Apparyllis documentation:

```text
https://docs.apparyllis.com/
```

Official API reference:

```text
https://docs.apparyllis.com/docs/docs/api/
```

Official Simply Plural API introduction:

```text
https://docs.apparyllis.com/docs/api/simply-plural-api/
```

Official authenticated-user endpoint documentation:

```text
https://docs.apparyllis.com/docs/api/get-your-authed-user-id
```

Official members endpoint documentation:

```text
https://docs.apparyllis.com/docs/api/members
```

Official public route source:

```text
https://raw.githubusercontent.com/ApparyllisOrg/SimplyPluralApi/release/src/api/v1/routes.ts
```

Official Simply Plural API GitHub repository:

```text
https://github.com/ApparyllisOrg/SimplyPluralApi
```

## User Consent

PluralBridge should only be used with an API token created by the account holder or by someone who has clear permission from the account holder.

Do not use PluralBridge to access another person's data without consent.

## Data Handling

API responses may contain private data.

Save responses only to local folders that are ignored by Git unless the files are redacted examples.

Recommended ignored export folders:

```text
exports/
spdump/
member_images/
Notes/
```

## Rate and Load Considerations

PluralBridge should use ordinary request patterns and avoid unnecessary repeated calls.

Scripts should:

- fetch only the data needed
- avoid tight infinite loops
- fail cleanly on authentication errors
- avoid printing tokens
- make output paths explicit
- keep exported private data out of Git

## Public Wording

Recommended public wording:

```text
PluralBridge uses public Apparyllis REST API endpoints with a user-created API token. It does not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.
```
