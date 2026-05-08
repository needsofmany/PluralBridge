/*
PluralBridge SQL Server script
030_add_constraints.sql

Adds primary keys, foreign keys, and useful indexes for locally imported Simply Plural export data.

This script contains schema only.
It contains no exported user data.
*/

USE PluralBridge;
GO

ALTER TABLE dbo.[user]
    ADD CONSTRAINT PK_user PRIMARY KEY (uid);
GO

ALTER TABLE dbo.me
    ADD CONSTRAINT PK_me PRIMARY KEY (uid);
GO

ALTER TABLE dbo.members
    ADD CONSTRAINT PK_members PRIMARY KEY (id);
GO

ALTER TABLE dbo.front_history
    ADD CONSTRAINT PK_front_history PRIMARY KEY (id);
GO

ALTER TABLE dbo.customfields
    ADD CONSTRAINT PK_customfields PRIMARY KEY (id);
GO

ALTER TABLE dbo.member_info_values
    ADD CONSTRAINT PK_member_info_values PRIMARY KEY (member_id, field_id);
GO

ALTER TABLE dbo.privacybuckets
    ADD CONSTRAINT PK_privacybuckets PRIMARY KEY (id);
GO

ALTER TABLE dbo.member_buckets
    ADD CONSTRAINT PK_member_buckets PRIMARY KEY (member_id, bucket_id);
GO

ALTER TABLE dbo.chat_categories
    ADD CONSTRAINT PK_chat_categories PRIMARY KEY (id);
GO

ALTER TABLE dbo.chat_channels
    ADD CONSTRAINT PK_chat_channels PRIMARY KEY (id);
GO

ALTER TABLE dbo.friends
    ADD CONSTRAINT PK_friends PRIMARY KEY (id);
GO

ALTER TABLE dbo.member_avatars
    ADD CONSTRAINT PK_member_avatars PRIMARY KEY (member_id);
GO

ALTER TABLE dbo.me
    ADD CONSTRAINT FK_me_user_uid
    FOREIGN KEY (uid)
    REFERENCES dbo.[user](uid);
GO

ALTER TABLE dbo.members
    ADD CONSTRAINT FK_members_user
    FOREIGN KEY (system_uid)
    REFERENCES dbo.[user](uid);
GO

ALTER TABLE dbo.front_history
    ADD CONSTRAINT FK_front_history_member
    FOREIGN KEY (member_id)
    REFERENCES dbo.members(id);
GO

ALTER TABLE dbo.member_info_values
    ADD CONSTRAINT FK_member_info_values_member
    FOREIGN KEY (member_id)
    REFERENCES dbo.members(id);
GO

ALTER TABLE dbo.member_info_values
    ADD CONSTRAINT FK_member_info_values_customfield
    FOREIGN KEY (field_id)
    REFERENCES dbo.customfields(id);
GO

ALTER TABLE dbo.member_buckets
    ADD CONSTRAINT FK_member_buckets_member
    FOREIGN KEY (member_id)
    REFERENCES dbo.members(id);
GO

ALTER TABLE dbo.member_buckets
    ADD CONSTRAINT FK_member_buckets_bucket
    FOREIGN KEY (bucket_id)
    REFERENCES dbo.privacybuckets(id);
GO

ALTER TABLE dbo.chat_channels
    ADD CONSTRAINT FK_chat_channels_category
    FOREIGN KEY (category_id)
    REFERENCES dbo.chat_categories(id);
GO

ALTER TABLE dbo.member_avatars
    ADD CONSTRAINT FK_member_avatars_member
    FOREIGN KEY (member_id)
    REFERENCES dbo.members(id);
GO

CREATE INDEX IX_members_system_uid
    ON dbo.members(system_uid);
GO

CREATE INDEX IX_front_history_member_id
    ON dbo.front_history(member_id);
GO

CREATE INDEX IX_front_history_start_time
    ON dbo.front_history(start_time);
GO

CREATE INDEX IX_front_history_end_time
    ON dbo.front_history(end_time);
GO

CREATE INDEX IX_customfields_system_uid
    ON dbo.customfields(system_uid);
GO

CREATE INDEX IX_privacybuckets_system_uid
    ON dbo.privacybuckets(system_uid);
GO

CREATE INDEX IX_chat_channels_category_id
    ON dbo.chat_channels(category_id);
GO

CREATE INDEX IX_member_avatars_system_uid
    ON dbo.member_avatars(system_uid);
GO
