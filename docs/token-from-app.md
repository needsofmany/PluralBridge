# Getting a Simply Plural API Token

PluralBridge uses a user-created Simply Plural API token.

The token lets the export scripts request your data from the public Apparyllis REST API.

## Treat the Token Like a Password

Anyone with your token may be able to access private Simply Plural data available through the API.

Do not post your token publicly.

Do not put your token in:

- Git commits
- screenshots
- issues
- pull requests
- example files
- documentation
- shared chat rooms

## Token Format

Use the full token exactly as provided by Simply Plural.

Do not add `Bearer`.

Do not remove prefixes.

Do not trim or rewrite the token.

PluralBridge sends the token directly in the HTTP `Authorization` header.

## Recommended Environment Variable

PluralBridge expects the token in:

    SP_TOKEN

In Git Bash:

    read -s -p "Paste Simply Plural token: " SP_TOKEN
    echo
    export SP_TOKEN

Check that something was captured without printing the token:

    printf 'Token length: %s\n' "${#SP_TOKEN}"

## If a Token Is Exposed

If you accidentally publish or share a token, revoke or invalidate it in Simply Plural and create a new token.

After creating the new token, update your local `SP_TOKEN` value before running the export scripts again.

## Screenshot Walkthrough

A standalone Word document with screenshots is also available for users who prefer a visual walkthrough.

The screenshot guide covers:

- opening the Simply Plural menu
- opening Settings
- opening Account settings
- opening Tokens
- deleting/revoking an existing token
- creating a new token
- selecting token permissions
- copying the generated token

The Word document is intended for non-technical users and should be kept separate from private exported data.

If screenshots show token values, those values must be blurred or replaced before the document is published.

## Screenshot Walkthrough Document

A standalone Word document with screenshots is available in the repository root:

    PluralBridge_Token_Walkthrough.docx

This document shows the token revocation and token creation process step by step.

Before publishing or updating this document, confirm that any visible token values in screenshots are blurred or replaced.
