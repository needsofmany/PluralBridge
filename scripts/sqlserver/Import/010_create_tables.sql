/*
PluralBridge SQL Server script
010_create_tables.sql

Creates core tables for locally imported Simply Plural export data.

This script contains schema only.
It contains no exported user data.
*/

USE PluralBridge;
GO

IF OBJECT_ID(N'dbo.member_notes', N'U') IS NOT NULL DROP TABLE dbo.member_notes;
IF OBJECT_ID(N'dbo.member_avatars', N'U') IS NOT NULL DROP TABLE dbo.member_avatars;
IF OBJECT_ID(N'dbo.member_buckets', N'U') IS NOT NULL DROP TABLE dbo.member_buckets;
IF OBJECT_ID(N'dbo.member_info_values', N'U') IS NOT NULL DROP TABLE dbo.member_info_values;
IF OBJECT_ID(N'dbo.front_history', N'U') IS NOT NULL DROP TABLE dbo.front_history;
IF OBJECT_ID(N'dbo.friends', N'U') IS NOT NULL DROP TABLE dbo.friends;
IF OBJECT_ID(N'dbo.chat_channels', N'U') IS NOT NULL DROP TABLE dbo.chat_channels;
IF OBJECT_ID(N'dbo.chat_categories', N'U') IS NOT NULL DROP TABLE dbo.chat_categories;
IF OBJECT_ID(N'dbo.privacybuckets', N'U') IS NOT NULL DROP TABLE dbo.privacybuckets;
IF OBJECT_ID(N'dbo.customfields', N'U') IS NOT NULL DROP TABLE dbo.customfields;
IF OBJECT_ID(N'dbo.members', N'U') IS NOT NULL DROP TABLE dbo.members;
IF OBJECT_ID(N'dbo.me', N'U') IS NOT NULL DROP TABLE dbo.me;
IF OBJECT_ID(N'dbo.[user]', N'U') IS NOT NULL DROP TABLE dbo.[user];
GO

CREATE TABLE dbo.[user]
(
    uid             nvarchar(64)  NOT NULL,
    name            nvarchar(255) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_user_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.me
(
    uid             nvarchar(64)  NOT NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_me_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.members
(
    id              nvarchar(64)   NOT NULL,
    system_uid      nvarchar(64)   NULL,
    name            nvarchar(255)  NULL,
    pronouns        nvarchar(255)  NULL,
    description     nvarchar(max)  NULL,
    avatar_url      nvarchar(1000) NULL,
    avatar_uuid     nvarchar(255)  NULL,
    raw_json         nvarchar(max)  NULL,
    imported_at_utc datetime2(3)   NOT NULL CONSTRAINT DF_members_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.front_history
(
    id              nvarchar(64)  NOT NULL,
    member_id       nvarchar(64)  NULL,
    start_time      bigint        NULL,
    end_time        bigint        NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_front_history_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.customfields
(
    id              nvarchar(64)  NOT NULL,
    system_uid      nvarchar(64)  NULL,
    name            nvarchar(255) NULL,
    type            nvarchar(100) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_customfields_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.member_info_values
(
    member_id       nvarchar(64)  NOT NULL,
    field_id        nvarchar(64)  NOT NULL,
    value           nvarchar(max) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_member_info_values_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.privacybuckets
(
    id              nvarchar(64)  NOT NULL,
    system_uid      nvarchar(64)  NULL,
    name            nvarchar(255) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_privacybuckets_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.member_buckets
(
    member_id       nvarchar(64)  NOT NULL,
    bucket_id       nvarchar(64)  NOT NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_member_buckets_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.chat_categories
(
    id              nvarchar(64)  NOT NULL,
    system_uid      nvarchar(64)  NULL,
    name            nvarchar(255) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_chat_categories_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.chat_channels
(
    id              nvarchar(64)  NOT NULL,
    category_id     nvarchar(64)  NULL,
    system_uid      nvarchar(64)  NULL,
    name            nvarchar(255) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_chat_channels_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.friends
(
    id              nvarchar(64)  NOT NULL,
    system_uid      nvarchar(64)  NULL,
    name            nvarchar(255) NULL,
    raw_json         nvarchar(max) NULL,
    imported_at_utc datetime2(3)  NOT NULL CONSTRAINT DF_friends_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.member_avatars
(
    member_id       nvarchar(64)   NOT NULL,
    system_uid      nvarchar(64)   NULL,
    avatar_uuid     nvarchar(255)  NULL,
    source_url      nvarchar(1000) NULL,
    local_filename  nvarchar(260)  NULL,
    local_path      nvarchar(1000) NULL,
    downloaded_at   datetime2(3)   NULL,
    raw_json         nvarchar(max)  NULL,
    imported_at_utc datetime2(3)   NOT NULL CONSTRAINT DF_member_avatars_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.member_notes
(
    member_id       nvarchar(64)   NOT NULL,
    note_file       nvarchar(260)  NOT NULL,
    note_index      int            NULL,
    endpoint        nvarchar(1000) NULL,
    ok              bit            NULL,
    raw_json         nvarchar(max)  NULL,
    imported_at_utc datetime2(3)   NOT NULL CONSTRAINT DF_member_notes_imported_at_utc DEFAULT SYSUTCDATETIME()
);
GO
