# Privacy and Safety

PluralBridge is designed for preserving personal Simply Plural data under the account holder's control.

Exported Simply Plural data can contain deeply private information. Treat exported files, avatar images, notes, SQL databases, reports, screenshots, and backups as sensitive.

## Independent Project

PluralBridge is an independent community preservation effort.

PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.

PluralBridge uses public Apparyllis REST API endpoints with a user-created API token. It does not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.

## Sensitive Data

Simply Plural exports may include:

- system profile information
- member names
- member descriptions
- pronouns
- custom fields
- private notes
- avatar images
- fronting history
- timestamps
- privacy buckets
- friends
- internal IDs
- API-derived metadata

Do not publish real exported data unless you intentionally chose to make that data public.

## API Tokens

Your API token is private. Treat it like a password.

Do not:

- commit it to Git
- paste it into GitHub issues
- paste it into public chats
- include it in screenshots
- place it in examples
- share it with anyone you do not completely trust

Use environment variables or ignored local configuration files.

Recommended environment variable:

```text
SP_TOKEN
```

Recommended ignored local config files:

```text
.env
config.local.json
```

## Local Storage

Store exports somewhere you control.

Recommended local output folders:

```text
exports/
exports/json/
exports/member_images/
exports/notes/
```

These folders should stay out of Git.

Before publishing anything, run:

```bash
git status
git diff --cached --stat
git diff --cached
```

Review staged files carefully before pushing.

## Filenames

Avoid filenames that expose private names or labels.

Recommended avatar filenames:

```text
<member_id>.png
```

Recommended note filenames:

```text
1.json
2.json
3.json
```

Avoid filenames based on:

- member names
- decorator names
- note titles
- private labels
- fronting labels
- relationship labels

Neutral filenames reduce accidental exposure when folders are viewed, copied, backed up, or screenshotted.

## Redacted Examples

Use redacted or synthetic examples in documentation.

Good example shape:

```json
{
  "id": "member_id_redacted",
  "content": {
    "name": "Member A",
    "avatarUuid": "avatar_uuid_redacted"
  }
}
```

Avoid examples containing real names, real notes, real avatar URLs, or real fronting history.

## SQL Databases

A local SQL Server database created from exported Simply Plural data should be treated as private.

It may contain the same sensitive information as the JSON files, plus readable views and reports that make the data easier to browse.

Do not upload database backups, exported tables, screenshots, or query results unless they have been reviewed and redacted.

Potentially sensitive SQL files include:

```text
*.bak
*.mdf
*.ldf
*.csv
*.tsv
*.xlsx
```

## Screenshots

Screenshots can expose private data even when the underlying files are ignored by Git.

Before posting screenshots, check for:

- names
- notes
- avatars
- token values
- IDs
- file paths
- browser tabs
- terminal history
- SQL result grids

## If Private Data Is Published

If private data is accidentally published:

1. remove it from the visible file
2. remove or rewrite the Git history if needed
3. revoke exposed API tokens
4. replace the token locally
5. inspect branches, tags, releases, issues, and pull requests
6. assume screenshots and copied text may persist elsewhere

For tokens, revocation matters more than cleanup. A token that reached a public place should be considered burned.

## Public Issues

When opening a GitHub issue, include only the smallest safe example needed to show the problem.

Prefer this:

```text
The export script fails when avatarUuid is empty.
```

Over this:

```text
Here is my full members.json file.
```

For bugs involving JSON shape, create a small redacted sample.
