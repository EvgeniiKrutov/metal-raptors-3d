#!/bin/bash
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
APP="${SHOT_APP:-$ROOT/Builds/ShotPlayer/ShotPlayer.app}"
mkdir -p "$ROOT/Builds/Screenshots"
for SPEC in "$@"; do
  SPEC="$(cd "$(dirname "$SPEC")" && pwd)/$(basename "$SPEC")"
  LOG="$ROOT/Builds/Screenshots/$(basename "$SPEC" .json).log"
  rm -f "$LOG"
  open -g -n -W -a "$APP" --args -shot "$SPEC" -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "$LOG"
  grep -E "ShotDirector|Exception" "$LOG"
done
