````text
Copyright © 2026 Needs of the Many / PluralBridge contributors

Licensed under the GNU General Public License v3.0 (GPLv3).

This recovery implementation and accompanying documentation were developed by Needs of the Many / PluralBridge. Please preserve this attribution as required by the applicable license terms.

# Simply Plural Data Recovery

This folder contains the documented recovery path for retrieving locally stored Simply Plural data from an Android device after the Simply Plural server shutdown.

## Confirmed Recovery Path

The current procedure has been successfully reproduced on a **Samsung Galaxy S24+** using Samsung Smart Switch.

The confirmed path is:

```text
Samsung Galaxy S24+
        ↓
Samsung Smart Switch backup
        ↓
com.saltypandastudios.frontime.noedata
        ↓
Android backup payload
        ↓
app_flutter/simply.db
        ↓
validated UTF-8 JSON
```

The recovered JSON contains structured Simply Plural data including member records, front history, custom fields, privacy buckets, channels, friends and related locally cached data.

No existing Simply Plural export is required.

No root access is required for the confirmed Samsung recovery path.

## Files

### `SP data extraction instructions.md`

Step-by-step instructions for:

- creating the Simply Plural Smart Switch backup;
- locating the `.noedata` file;
- extracting the Android backup payload;
- locating and extracting `simply.db`;
- converting it to normal UTF-8 JSON;
- validating the JSON;
- displaying recovered collection and record counts.

Start here if performing the recovery manually.

### `recover-simplyplural-data.cmd`

Command-line recovery steps corresponding to the extraction portion of the documented procedure.

The script uses placeholders for device-specific Smart Switch paths. Review and replace the required placeholders before running it.

## Privacy

**Do not send your Simply Plural data to Needs of the Many / PluralBridge.**

Recovered data may contain highly private System information.

Keep the following under your own control:

- Simply Plural exports
- Smart Switch backups
- `.noedata` files
- `simply.db`
- recovered JSON
- avatar or image files
- any other recovered application data

Do not post these files to public GitHub repositories, paste sites, Reddit threads, issue trackers, or other public locations.

The purpose of this recovery work is to let users recover their own data locally without Needs of the Many / PluralBridge receiving it.

## Avatar Recovery

Avatar recovery is still under investigation.

We have confirmed that recovered member records retain avatar identifiers, and Samsung Smart Switch can preserve recognizable images from a `DCIM/SimplyPlural` folder.

We have not yet established a complete machine-readable mapping between those recovered source images and individual member records.

## Other Android Devices

The recovery target, `app_flutter/simply.db`, is part of Simply Plural rather than Samsung.

Samsung Smart Switch is currently the first stock, non-rooted acquisition path we have successfully reproduced.

Recovery from non-Samsung Android devices has not yet been established.

## For Other Plural-App Developers

If your application already supports importing Simply Plural data, we welcome collaboration on supporting this recovery path for users who did not create an export before shutdown.

Needs of the Many / PluralBridge intends to publish the technical information, documentation, scripts, mappings, and reference implementation needed to support this work.

Users should be able to recover their data and take it to the plural application of their choice without sending that data through Needs of the Many / PluralBridge.

## License

The recovery implementation and accompanying documentation in this folder are licensed under the **GNU General Public License v3.0 (GPLv3)**.

Please preserve the applicable copyright, license, and attribution notices when using or modifying covered material.

PluralBridge is an independent project by Needs of the Many.

PluralBridge is not affiliated with Simply Plural or Apparyllis.
````
