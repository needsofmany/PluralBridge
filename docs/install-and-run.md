# Install and Run

This guide is for users who know how to install and run software, but may not be developers.

PluralBridge is currently an early preservation toolkit. The first working path uses command-line tools.

## What You Need

You need:

- a computer you control
- Git, if you want to download the repository that way
- Python 3
- a Simply Plural API token
- local disk space for exported files

## Privacy Warning

Your exported data may contain private system information, member information, notes, avatars, timestamps, custom fields, and other sensitive material.

Save exports only somewhere you trust.

Do not upload exported files into public issues, pull requests, screenshots, chat rooms, or shared folders.

## Get the Repository

Download or clone PluralBridge from GitHub.

If using Git:

    git clone https://github.com/needsofmany/PluralBridge.git
    cd PluralBridge

## Set Your API Token

PluralBridge uses the `SP_TOKEN` environment variable.

In Git Bash:

    read -s -p "Paste Simply Plural token: " SP_TOKEN
    echo
    export SP_TOKEN

Check that a token is present without printing it:

    printf 'Token length: %s\n' "${#SP_TOKEN}"

## Export JSON Data

Run:

    python scripts/python/export_json.py --output-dir exports/json

The exported JSON files will be written under:

    exports/json

## Export Avatar Images

After exporting JSON data, run:

    python scripts/python/export_avatars.py \
      --members-json exports/json/members.json \
      --output-dir exports/member_images

The avatar image files will be written under:

    exports/member_images

## Keep Exported Data Private

The `exports` folder is intended for your local machine only.

Do not commit it to Git.

Do not publish it by accident.
