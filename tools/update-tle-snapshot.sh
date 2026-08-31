#!/usr/bin/env bash
set -euo pipefail

URL="https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=TLE"
TARGET="unity/Assets/StreamingAssets/tle/active.txt"
AGENT="SatTrak (https://github.com/JanVogt06/SatTrak-SatelliteVisualization)"

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

curl -sSfL -A "$AGENT" "$URL" -o "$tmp"

lines=$(grep -c . "$tmp" || true)
if [ "$lines" -lt 3 ] || [ $((lines % 3)) -ne 0 ]; then
  echo "refusing to write: $lines usable lines, not a multiple of three" >&2
  head -3 "$tmp" >&2
  exit 1
fi

if ! awk 'NR%3==2 && $0 !~ /^1 / {exit 1} NR%3==0 && $0 !~ /^2 / {exit 1}' "$tmp"; then
  echo "refusing to write: payload is not in three-line TLE format" >&2
  head -3 "$tmp" >&2
  exit 1
fi

mkdir -p "$(dirname "$TARGET")"
mv "$tmp" "$TARGET"
trap - EXIT
echo "wrote $TARGET with $((lines / 3)) element sets"
