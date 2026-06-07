#!/usr/bin/env bash
cd "/d/Src/Needs of the Many/PluralBridge"
echo "VERIFY START"
echo "GREEN_TARGET_FILES"
find database/targeting/prop_candidate -maxdepth 1 -type f | sort
echo "GREEN_BASH_FILES"
find database/bash/prop_candidiate -maxdepth 1 -type f | sort
echo "ARCHIVES"
find database/targeting database/bash -maxdepth 1 -type d \( -name "prop_candidate_fail_*" -o -name "prop_candidiate_fail_*" \) | sort
echo "GIT_STATUS"
git status --short database/targeting/prop_candidate database/bash/prop_candidiate
echo "VERIFY END"
