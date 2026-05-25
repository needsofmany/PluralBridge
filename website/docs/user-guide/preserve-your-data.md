# Preserve Your Simply Plural Data

Simply Plural by Apparyllis has announced that its servers will shut down on July 1, 2026, and that user data and avatars will be deleted after shutdown.

This guide gathers the major preservation paths discussed for PluralBridge users and developers.

For ordinary users, the preferred sequence is simple: use the official export if available, create an API token with Read permission, run the PluralBridge export tooling, and save the resulting files somewhere private.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Official Export Path

Official shutdown notice states that servers shut down July 1, 2026 and user data/avatars will be deleted after shutdown.

Official export path (if visible):

Simply Plural → Settings → Account Settings → Export your data

PluralKit export path:

Settings → Integrations → PluralKit → Export

If the export option is not visible, possible causes include:

1. Export rollout not enabled for the account/app version.
2. Wording/path changed after the announcement.
3. Export is server-gated and temporarily disabled.

Additional paths worth checking:

- Settings → Account
- Settings → Data
- Settings → Backup
- Settings → Advanced
- Settings → Privacy
- Settings → Experimental

Also update the app from the app store before proceeding.

## API Token Acquisition

The API route is the most reliable extraction path because it avoids dependence on the application UI.

Potential token paths:

Settings → Tokens
Settings → Account → Tokens

Create a token with at least Read permission.

API base URL:

https://api.apparyllis.com

Authorization header format:

Authorization: YOUR_TOKEN

Verification test from shell:

curl https://api.apparyllis.com/v1/me   -H "Authorization: YOUR_TOKEN"

If JSON is returned, the API is accessible and the dataset can be harvested programmatically.

## Known API Endpoints

Core endpoints identified from public documentation and route listings:

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
```

For notes, enumerate members first, then:

```text
GET /v1/notes/{uid}/{member_id}
```

The public route definitions suggest a conventional REST architecture.

## Python Extraction Script

Example extraction script:

```python
import json
import pathlib
import requests

BASE = "https://api.apparyllis.com"
TOKEN = "PASTE_TOKEN_HERE"
OUT = pathlib.Path("exports/json")
OUT.mkdir(exist_ok=True)

s = requests.Session()
s.headers.update({
    "Authorization": TOKEN,
    "Accept": "application/json",
})

def get(path, params=None):
    r = s.get(BASE + path, params=params, timeout=60)
    r.raise_for_status()
    return r.json() if r.text else None

def save(name, obj):
    (OUT / name).write_text(
        json.dumps(obj, indent=2, ensure_ascii=False),
        encoding="utf-8"
    )

me = get("/v1/me")
save("me.json", me)

uid = me.get("id") or me.get("uid") or me.get("_id")
if not uid:
    raise RuntimeError(f"Could not determine uid from /v1/me: {me}")

targets = {
    "user.json": f"/v1/user/{uid}",
    "members.json": f"/v1/members/{uid}",
    "groups.json": f"/v1/groups/{uid}",
    "custom_fronts.json": f"/v1/customFronts/{uid}",
    "front_history_all.json": "/v1/frontHistory",
    "front_history_range.json": f"/v1/frontHistory/{uid}",
    "automated_timers.json": f"/v1/timers/automated/{uid}",
    "repeated_timers.json": f"/v1/timers/repeated/{uid}",
    "polls.json": f"/v1/polls/{uid}",
    "custom_fields.json": f"/v1/customFields/{uid}",
    "privacy_buckets.json": "/v1/privacyBuckets",
    "filters.json": "/v1/filters",
    "chat_categories.json": "/v1/chat/categories",
    "chat_channels.json": "/v1/chat/channels",
    "friends.json": "/v1/friends/",
}

for filename, path in targets.items():
    try:
        params = None
        if filename == "front_history_range.json":
            params = {"startTime": "0", "endTime": "9999999999999"}
        save(filename, get(path, params=params))
        print("saved", filename)
    except Exception as e:
        print("FAILED", filename, path, e)

members = json.loads((OUT / "members.json").read_text(encoding="utf-8"))
notes = {}

for m in members:
    mid = m.get("id") or m.get("_id")
    if not mid:
        continue
    try:
        notes[mid] = get(f"/v1/notes/{uid}/{mid}")
        print("saved notes for", mid)
    except Exception as e:
        notes[mid] = {"error": str(e)}

save("notes_by_member.json", notes)
```

## Avatar Preservation

The official shutdown post states that the built-in export includes avatars.

API-level avatar recovery may involve fields such as:

avatarUuid
avatarUrl
uid

Older tooling reportedly used patterns such as:

https://spaces.apparyllis.com/avatars/{uid}/{avatarUuid}

This should be treated as implementation-derived rather than contractual API behavior.


## Preservation Recommendations

Preserve all available user-controlled export formats.

Recommended preservation set:

- Official Simply Plural export, if available.
- PluralKit export, if relevant.
- Raw API JSON exported through the user-created token.
- Avatar files where available.
- Front history.
- Notes.
- Polls.
- Journals.
- Custom fields.
- Group relationships.

The safest preservation strategy uses more than one ordinary export method:

1. Official app export.
2. API JSON export.
3. Avatar export or avatar download.

Keep the original exported files unchanged. Make working copies for experiments, imports, reports, or future conversion tools.

## Operational Recommendations

Immediate recommended sequence:

1. Update the Simply Plural application.
2. Search the app settings for export and token options.
3. Generate a Read-permission API token.
4. Verify the `/v1/me` endpoint.
5. Execute the full API JSON export.
6. Download avatars where available.
7. Save the official app export, if available.
8. Repeat exports periodically before shutdown.

If the service becomes unstable near shutdown, prioritize:

- official export files
- API JSON export files
- front history
- member metadata
- notes and journals
- avatars
