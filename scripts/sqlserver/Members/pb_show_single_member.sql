USE [PluralBridgeChap2SafeSpine];
SET NOCOUNT ON;

EXEC dbo.pb_show_tree_step4_members_by_email
    @Email = N'example-9@pluralbridge.org';