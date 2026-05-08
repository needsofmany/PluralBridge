
## Preservation-First Schema

The current SQL Server model is preservation-first.

Common fields are extracted into relational columns where they are useful for joins, validation, and reports. Full source records are also retained in `raw_json` columns.

This allows PluralBridge to preserve the original exported payload while the relational schema improves over time.

Further normalization of embedded JSON structures is planned as a post-public-release enhancement.
