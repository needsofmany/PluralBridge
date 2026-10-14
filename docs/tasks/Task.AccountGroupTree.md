# Task.AccountGroupTree

## Description

Put a group at the account root so it can contain systems. The target is Example 10: group Household contains system The Harbor and system Main House. Example 1 stays as it is: The Harbor contains Dancers, Ballet, and Musicians. `pb_object_memberships` is out of scope. It records who can open a system. It does not record where a group or system sits. `pb_system_object_links` and `pb_system_object_placements` stay as they are. They place objects inside a system, so they cannot place a system inside an account-level group. Login, authorization, and the current system tree path stay. The assistant does not run SQL, git, or a build. Stacy runs each SQL script in SSMS and pastes the result before the next step.

---

## Step AccountGroupTree.1 — let a group exist without a system

### Description

`pb_groups.SystemId` is required today, so every group is stuck inside one system. This step makes `SystemId` nullable, adds nullable `AccountId`, and requires at least one of those two. Existing system groups keep their `SystemId`. No rows are moved.

### Commands

- SQL shown in chat.
- Stacy runs it in SSMS against `PluralBridgeChap2SafeSpine`.
- Stacy pastes the column and check result before Step 2.

### Done When

- `pb_groups.SystemId` accepts null.
- `pb_groups.AccountId` exists and references `pb_accounts.AccountId`.
- A check constraint requires `AccountId` or `SystemId`.
- Existing Harbor groups still have a `SystemId`.
- No tree code has been changed.

---

## Step AccountGroupTree.2 — add account object links

### Description

Add `pb_account_object_links`, modeled on `pb_system_object_links`. A row says an account is connected to an object. It does not say where that object sits in the tree.

### Commands

- SQL shown in chat.
- Stacy runs it in SSMS.
- Stacy pastes the table-exists result before Step 3.

### Done When

- `pb_account_object_links` exists with `AccountObjectLinkId`, `AccountId`, `ObjectType`, `ObjectId`, `LinkRole`, and `CreatedAtUtc`.
- `AccountId` references `pb_accounts.AccountId`.
- Indexes exist on `AccountId` and on `ObjectType`, `ObjectId`.
- No Example 10 rows have been inserted.
- No tree code has been changed.

---

## Step AccountGroupTree.3 — add account object placements

### Description

Add `pb_account_object_placements`. A row says what sits under what for one account. Household under the account, and The Harbor under Household, are placement rows. This is the table the system placement table cannot be.

### Commands

- SQL shown in chat.
- Stacy runs it in SSMS.
- Stacy pastes the table-exists result before Step 4.

### Done When

- `pb_account_object_placements` exists with `AccountObjectPlacementId`, `AccountId`, `SourcePlacementId`, `ObjectType`, `ObjectId`, `ParentObjectType`, `ParentObjectId`, `PlacementRole`, `PlacementRank`, and `CreatedAtUtc`.
- `AccountId` references `pb_accounts.AccountId`.
- Indexes exist on `AccountId`, on `ObjectType`, `ObjectId`, and on `ParentObjectType`, `ParentObjectId`.
- The table is empty.
- No tree code has been changed.

---

## Step AccountGroupTree.4 — draw the account tree when placements exist

### Description

Change only the members tree loader in `app/src`. If the signed-in account has rows in `pb_account_object_placements`, draw that tree. If it has none, keep the current system-and-group tree. Copy the changed source files to `wwwroot` only when Stacy asks.

### Commands

- Code shown in `app/src`.
- Stacy rebuilds and refreshes.
- The assistant does not run git.

### Done When

- An account with no account placements still shows the current tree.
- An account with account placements shows those parents and children.
- Example 1 has no account placements, so its tree is unchanged.
- Login and system access are unchanged.

---

## Step AccountGroupTree.5 — load the Example 10 tree

### Description

Stacy runs one data script for `example-10@pluralbridge.org`. It creates group Household with `AccountId` set and `SystemId` null. It places Household at the account, and places The Harbor and Main House under Household. Members A, E, I, O, W, Y, X, and Z are not part of this step.

### Commands

- SQL shown in chat.
- Stacy runs it in SSMS.
- Stacy signs in as Example 10 and Example 1.

### Done When

- Example 10 shows Household, with The Harbor and Main House inside it.
- Example 1 still shows The Harbor, with Dancers, Ballet, and Musicians inside it.
- `@@TRANCOUNT` is 0 after the script.
