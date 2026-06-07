#!/usr/bin/env bash
cd "/d/Src/Needs of the Many/PluralBridge"
echo "VERIFY START"
echo "GREEN_TARGET_FILES"
find database/targeting/prop_candidate -maxdepth 1 -type f | sort
echo "GREEN_BASH_FILES"
find database/bash/prop_candidiate -maxdepth 1 -type f | sort
echo "FAILED_TARGET_ARCHIVES"
find database/targeting -maxdepth 1 -type d -name "prop_candidate_fail_*" | sort
echo "FAILED_BASH_ARCHIVES"
find database/bash -maxdepth 1 -type d -name "prop_candidiate_fail_*" | sort
echo "VERIFY END"
