#!/bin/sh
set -eu

LIVE=/usr/share/nginx/html/tle/active.txt
SEED=/usr/share/nginx/html/StreamingAssets/tle/active.txt
URL="${TLE_URL:-https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=TLE}"
AGENT="SatTrak (+https://github.com/JanVogt06/SatTrak-SatelliteVisualization)"
INTERVAL="${TLE_REFRESH_SECONDS:-7200}"

mkdir -p "$(dirname "$LIVE")"

if [ ! -f "$LIVE" ]; then
    if [ -f "$SEED" ]; then
        cp "$SEED" "$LIVE"
        echo "[tle] seeded from the bundled snapshot"
    else
        echo "[tle] no bundled snapshot found at $SEED" >&2
    fi
fi

refresh() {
    tmp=$(mktemp)

    if ! curl -sSfL --max-time 60 -A "$AGENT" "$URL" -o "$tmp"; then
        echo "[tle] upstream request failed, keeping the previous file"
        rm -f "$tmp"
        return
    fi

    lines=$(grep -c . "$tmp" || true)

    if [ "$lines" -lt 3 ] || [ $((lines % 3)) -ne 0 ] ||
       ! awk 'NR%3==2 && $0 !~ /^1 / {exit 1} NR%3==0 && $0 !~ /^2 / {exit 1}' "$tmp"; then
        echo "[tle] upstream sent no usable TLE data, keeping the previous file"
        rm -f "$tmp"
        return
    fi

    mv "$tmp" "$LIVE"
    chmod 644 "$LIVE"
    echo "[tle] refreshed with $((lines / 3)) element sets"
}

if [ "$INTERVAL" -gt 0 ]; then
    (
        while true; do
            refresh
            sleep "$INTERVAL"
        done
    ) &
fi

exec /docker-entrypoint.sh nginx -g 'daemon off;'
