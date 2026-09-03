# Shutdown and Preservation

PluralBridge exists because users may need a local preservation path for Simply Plural data.

As currently announced by Apparyllis, Simply Plural servers will be shut down on July 1, 2026. The announcement also states that once the servers are officially shut down, user data and avatars associated with users will be permanently deleted. The announcement says users will have a chance to export their user data and avatars through the app before that date. :contentReference[oaicite:0]{index=0}

## Why Preservation Matters

Simply Plural data may represent years of carefully maintained personal records.

That data can include:

- system profile information
- members
- member descriptions
- custom fields
- privacy buckets
- notes
- avatars
- friends
- fronting history
- timestamps
- reports and patterns built through long-term use

For many users, the value of the data is not limited to the app interface. The exported data can become the raw material for local viewers, converters, importers, reports, archives, and migration tools.

## Current Goal

The first goal of PluralBridge is preservation.

That means:

1. retrieve data through public Apparyllis REST API endpoints
2. save API responses as local JSON files
3. save avatar images where available
4. keep exported filenames neutral where practical
5. document safe token handling
6. document safe local storage
7. provide SQL Server scripts for users who want a relational local copy
8. provide readable views and validation queries

## What PluralBridge Is

PluralBridge is an independent community preservation effort.

It is intended to help users preserve their own data from their own Simply Plural accounts.

PluralBridge is maintained by Needs of the Many.

## What PluralBridge Is Not

PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.

PluralBridge is not a Simply Plural replacement app.

PluralBridge does not provide hosted accounts, hosted synchronization, or a hosted community service.

PluralBridge does not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.

## Preservation Path

A practical preservation path has several layers.

### 1. Local JSON Export

Save raw API responses locally.

This preserves the original data shape as closely as possible.

Recommended folder:

```text
exports/json/
```

### 2. Avatar Export

Download avatar images where avatar references are available.

Recommended folder:

```text
exports/member_images/
```

Recommended filenames:

```text
<member_id>.png
```

### 3. Notes Export

Save notes as local JSON files.

Recommended folder:

```text
exports/notes/
```

Recommended neutral filenames:

```text
1.json
2.json
3.json
```

### 4. SQL Server Import

Load exported JSON into relational SQL Server tables.

This makes the data easier to inspect, validate, query, and report.

Useful views may include:

- current front
- readable fronting history
- readable custom fields
- member profile summary
- member bucket summary
- avatar file paths
- validation reports

### 5. Future Tools

Once data is preserved locally, future tools can be built around it.

Possible future tools include:

- local viewers
- converters
- importers
- report generators
- timeline browsers
- migration helpers
- static HTML reports

## Preserve First, Polish Later

The first priority is getting users a reliable local copy of their data.

A polished one-click tool can come later. Local JSON files, avatar files, SQL scripts, and clear documentation are enough to give users a foundation other tools can build on.

## Public Wording

Suggested public wording:

```text
As currently announced, the Simply Plural servers will be shut down on July 1, 2026, and user data and avatars will be permanently deleted after shutdown.

PluralBridge is an independent community preservation effort. It uses public Apparyllis REST API endpoints with a user-created API token to help users preserve their own Simply Plural data locally.
```
