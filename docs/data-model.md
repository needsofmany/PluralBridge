
## Preservation-First Schema

The current SQL Server model is preservation-first.

Common fields are extracted into relational columns where they are useful for joins, validation, and reports. Full source records are also retained in `raw_json` columns.

This allows PluralBridge to preserve the original exported payload while the relational schema improves over time.

Further normalization of embedded JSON structures is planned as a post-public-release enhancement.

## Notes and Avatar Mapping

The tested PluralBridge SQL Server model includes first-pass support for member notes and avatar metadata.

### Member Notes

Member notes are exported as neutral numeric files under:

    exports/notes

Examples:

    1.json
    2.json
    3.json

The export manifest records which API endpoint produced each note file. The SQL import uses that manifest mapping to populate:

    dbo.member_notes

The `member_notes` table joins back to:

    dbo.members

using:

    member_notes.member_id -> members.id

This preserves note exports without putting member names in note filenames.

### Member Avatars

Avatar image files are exported under:

    exports/member_images

The avatar exporter also creates:

    exports/avatar_manifest.tsv

The SQL import uses that TSV file to populate:

    dbo.member_avatars

The `member_avatars` table joins back to:

    dbo.members

using:

    member_avatars.member_id -> members.id

This preserves avatar file metadata while keeping the actual image files outside the database.

## Current Normalization Boundary

The current schema extracts high-value relational fields needed for validation, reports, member joins, note joins, and avatar joins.

Some source payloads are still preserved in `raw_json` columns. Further normalization of embedded structures is planned after the initial public release.

## Schema Diagram

A schema diagram is available at:

    docs/images/PluralBridge-DB-Schema.png

The diagram is documentation only. It should not contain private exported data.
