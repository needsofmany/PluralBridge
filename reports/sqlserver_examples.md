# SQL Server Report Examples

This document lists useful report ideas for Simply Plural data imported into SQL Server through PluralBridge.

The examples here are intentionally data-agnostic. They do not contain real exported data.

## Current Front

Shows the member or members with a fronting-history row that has no end time.

Useful fields:

- member name
- start time UTC
- start time local
- front duration so far

## Fronting History by Member

Shows fronting-history counts and date ranges grouped by member.

Useful fields:

- member name
- front-history row count
- first recorded front
- most recent front
- total fronting time, when calculable

## Member Profile Summary

Shows a readable profile-style row for each member.

Useful fields:

- member name
- pronouns
- description
- custom field values
- privacy bucket memberships
- avatar filename, if exported

## Custom Field Coverage

Shows which members have values for each custom field.

Useful fields:

- custom field name
- member count with values
- member count without values

## Privacy Bucket Membership

Shows which members are assigned to each privacy bucket.

Useful fields:

- privacy bucket name
- member name
- member ID

## Avatar Export Coverage

Shows which members have local avatar files after avatar export.

Useful fields:

- member name
- member ID
- avatar filename
- avatar source URL
- downloaded timestamp

## Data Quality Checks

Useful validation checks include:

- members without matching user/system row
- front-history rows without matching member
- custom field values without matching custom field
- bucket memberships without matching bucket
- duplicate IDs
- front-history rows where end time is before start time
- current-front rows where end time is null
