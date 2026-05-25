# Simply Plural Shutdown and Data Preservation

Simply Plural/Apparyllis announced that the Simply Plural servers will shut down on July 1, 2026.

The announcement says that after shutdown, user data and avatars associated with users will be permanently deleted. It also says users will have a chance to export user data and avatars from the app before that date.

PluralBridge exists to help users preserve their Simply Plural data while the original service is still available.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## What users should do now

1. Update the Simply Plural app.
2. Look for the official export path in the app.
3. Create a Simply Plural/Apparyllis API token with Read permission.
4. Run PluralBridge export tooling to save raw JSON locally.
5. Export or download avatar images where available.
6. Keep exported files private unless intentionally redacted.
7. Repeat exports periodically before July 1, 2026.

## Official export path

The Apparyllis announcement describes this export path:

```text
Settings -> Account Settings -> Export your data
```

If that option is not visible, check nearby account, data, backup, privacy, and advanced settings, and confirm that the app is updated.

## PluralKit export path

For users migrating to PluralKit, the announcement describes this path:

```text
Settings -> Integrations -> PluralKit -> Export
```

## Why export early

The safest preservation plan uses more than one method:

1. Official app export, if available.
2. Raw API JSON export through a user-created token.
3. Avatar download or avatar export.
4. Local app data preservation where technically possible.

Exporting early gives users time to verify the files, rerun exports, and fix problems before the shutdown date.

## What PluralBridge preserves

The current PluralBridge preservation model focuses on:

- Raw JSON exported through public Apparyllis REST API endpoints.
- Member records.
- Group records.
- Front history.
- Custom fields.
- Privacy buckets.
- Friends and chat-related metadata where available.
- Notes where available.
- Avatar image files where available.
- SQL Server import scripts and readable views for preserved data.

## What PluralBridge does not do

PluralBridge does not use a Simply Plural token as a PluralBridge login.

PluralBridge does not require users to upload their exported data to a third-party service.

PluralBridge does not copy Simply Plural branding, application code, or private implementation materials.

## Token boundary

SP_TOKEN is export-only and used only to export user-owned data from Simply Plural/Apparyllis. It is not a PluralBridge credential.

Treat SP_TOKEN like a password. Do not post it publicly, commit it to Git, paste it into screenshots, or include it in support messages.

## Related PluralBridge guides

- [Create a Simply Plural API token](../user-guide/create-simply-plural-api-token.md)
- [Preserve your data](../user-guide/preserve-your-data.md)
- [Export avatars](../user-guide/export-avatars.md)
- [Use the REST API with a token](../developer-guide/rest-api-with-token.md)

