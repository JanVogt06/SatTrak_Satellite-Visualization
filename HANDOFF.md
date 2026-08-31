# Handoff

State of the project, what changed, and what is left to do. Written for someone joining
who has not seen the earlier work.

## Where the project stands

SatTrak is a Unity satellite visualization. It downloads TLE orbital elements, propagates
them with SGP4 and renders 5000+ satellites around a globe.

The goal is to publish it as a WebGL build served from a container, installable with
`docker compose pull` like PicHunter, SolarFlow and the Spesen generator.

| | |
| --- | --- |
| Unity | 2022.3.62f3 |
| Runs in the editor | yes, no account or API key needed |
| WebGL build | first attempt in progress; the WebGL module is installed |
| Docker / CI | not started |
| Blocking issue | `cities.json` allocates ~700 MB of heap at startup |

The two things that used to block a browser build are done: Cesium is gone, and the TLE
loading path no longer uses APIs that WebGL lacks.

## Getting it running

```bash
git lfs install
git clone https://github.com/JanVogt06/SatTrak-SatelliteVisualization.git
cd SatTrak-SatelliteVisualization
git lfs pull
```

`git lfs pull` is not optional. Without it the satellite models and the city database are
130-byte pointer files and the project will not run.

Add `unity/` as a project in Unity Hub. There is no token or config file to fill in.

## What changed

Roughly 35 commits, in three blocks.

**Cleanup.** The repository was restructured: `unity/Assets/Project` holds everything
written for this project, `unity/Assets/ThirdParty` holds vendored assets. Dead code and
duplicate assets were removed, nine scripts were converted from Windows-1252 to UTF-8,
explanatory comments were stripped, and German identifiers, log messages and inspector
labels were translated. `.gitignore` used to point at `UnityProject/` while the folder was
called `UnityProjekt/`, so none of its rules had ever applied.

**Cesium removal.** Cesium for Unity is a native plugin with no WebGL target, and it
required a personal Cesium Ion token. It has been replaced by four components under
`unity/Assets/Project/Scripts/Geo`:

| Script | Responsibility |
| --- | --- |
| `Wgs84` | Ellipsoid math: geodetic ↔ ECEF, East-Up-North basis |
| `Georeference` | Local frame origin, ECEF transforms, floating origin |
| `GlobeAnchor` | Keeps a transform fixed to a geographic position |
| `EarthGlobe` | Generates the textured globe mesh |

`Georeference` is a drop-in replacement for `CesiumGeoreference`: same fields, same
runtime-movable origin. The scene still lives in an East-Up-North frame anchored near Jena
(51.21796° N, 11.66699° E, 400 m) whose origin follows the camera. That is deliberate — it
keeps float precision usable near the ground.

The axis convention is **+X East, +Y up, +Z North**, taken from Cesium's own source
(`CesiumGeoreference.cs:73`). The basis has determinant −1 because ECEF is right-handed and
Unity is left-handed.

The math was verified against the ECEF values Cesium itself had written into the scene:
agreement to 0.0000 mm, determinant −1.000, and the earth centre lands at
(0, −6365550.7, +20890.5) in the local frame, matching an independent calculation. Of the
65536 globe triangles, 65024 face outward, 0 face inward and 512 are degenerate (the poles).

**TLE data flow.** See the next section.

## How TLE data reaches the app

Clients never contact CelesTrak. `TleSource` tries two sources in order:

1. `tle/active.txt` on the hosting server, relative to the page
2. the snapshot bundled in `unity/Assets/StreamingAssets/tle/active.txt`

Both URLs are inspector fields on `SatelliteManager`. `StreamingAssets` matters: unlike
`Resources`, its files ship as loose files next to the build and are fetched over HTTP, so
**the container can replace the TLE file without rebuilding Unity**.

Refresh the committed snapshot with:

```bash
./tools/update-tle-snapshot.sh
```

### Two traps worth knowing

CelesTrak throttles repeated downloads of the same group. When it does, it answers with
**HTTP 200** and a plain-text notice instead of element sets:

```
GP data has not updated since your last successful
download of GROUP=active at 2026-08-31 15:13:07 UTC.
Data is updated once every 2 hours.
```

Both `TleSource` and `tools/update-tle-snapshot.sh` reject anything that is not in
three-line TLE format, so this can no longer be mistaken for data. Do not poll CelesTrak in
a loop — doing so gets the IP answered with HTTP 403 for a while.

TLE data ages. Along-track displacement caused by atmospheric drag, computed from a real
ISS element set:

| Age | Displacement |
| --- | --- |
| 1 day | 2 km |
| 7 days | 111 km |
| 30 days | 2047 km |
| 90 days | 18425 km |

SGP4 models this, so it is not raw error, but the model's uncertainty grows the same way
because drag depends on solar activity. This is why the container refreshes the file every
two hours rather than relying on the release cadence.

The committed snapshot holds the full active catalogue, 16046 element sets. Refresh it
before a release so the bundled fallback is not stale.

## What is left to do

### 1. Memory budget — blocks everything else

Unity WebGL compiles to wasm32, the address space is capped, and the `.data` file is loaded
into memory in full. The current asset budget does not survive a browser.

**`cities.json` is the blocker.** It holds 2,191,856 entries — the full GeoNames gazetteer,
not the "over 1000 cities" the old README claimed. `GeoNamesSearchFromJSON.Awake()` builds,
synchronously:

| Step | Cost |
| --- | --- |
| `geoJson.text` | 189 MB UTF-8 becomes a ~378 MB UTF-16 string |
| `JsonUtility.FromJson` | 2.19 M objects plus 2.19 M name strings |
| `e.nameLower = ToLowerInvariant()` | a second copy of every name |
| `_lookup[e.name] = e` | a 2.19 M entry dictionary |
| `_nameAsc`, `_nameDesc`, `_filtered` | three more 2.19 M element lists |

Peak is roughly 700 MB of managed heap. Two ways forward:

- **Keep every place.** Convert to a binary format (name blob plus two `float32`), read into
  a `NativeArray` instead of 2.19 M heap objects, drop `nameLower` in favour of a
  case-insensitive comparison, and replace the three materialized lists with index arrays.
  Estimated one to two days.
- **Filter.** Keep places above ~5000 inhabitants, roughly 50000 entries. Search still works
  worldwide, only hamlets disappear. Estimated one hour.

This decision is still open.

**Audio and help images are import settings, not code.** The source files stay as they are;
Unity re-encodes at build time. In the inspector: audio clips to Vorbis, quality ~50, load
type Streaming (179 MB of MP3 today), and `Project/Resources/HelpImages` to max size 1024
(50 MB today, and everything under a `Resources` folder ships whether it is used or not —
one screenshot alone is 9.2 MB).

`ThirdParty/Skyboxes` is 203 MB and completely unreferenced. It never enters a build, so it
only costs clone time. Left in place deliberately.

### 2. First browser build

- The WebGL module is installed. The first build recompresses every texture and takes a
  long time.
- Player settings: compression format Brotli, exception support *Explicitly Thrown*, data
  caching on, decompression fallback **off** — the server sets the headers itself.
- Desktop-only APIs need handling: `Application.Quit()` does nothing in a browser
  (`MenuManager.cs:135`, `SceneSwitcher.cs:76`, `GameHudController.cs:65`), and
  `Screen.resolutions` / `Screen.SetResolution` are meaningless
  (`MenuManager.cs:230`, `MenuManager.cs:257`). Hide the affected buttons and the resolution
  menu.
- WebGL is single-threaded, so Burst jobs run serially. With 5000 satellites,
  `MoveSatelliteJobParallelForTransform` is the first place to look if the frame rate is
  poor. Measure before optimizing.

### 3. Ship it

Follow the PicHunter layout: `Dockerfile` at the root, nginx config under `docker/`, image
on GHCR, a `v*` tag triggers build and release.

Two things differ from the other projects: Unity has to build inside CI, and the TLE file
needs refreshing inside the container.

#### `docker/entrypoint.sh`

Seeds the served TLE file from the bundled snapshot, then refreshes it on an interval. Only
data that passes the format check ever replaces the file, so a throttled or failed request
leaves the previous copy in place.

```sh
#!/bin/sh
set -eu

LIVE=/usr/share/nginx/html/tle/active.txt
SEED=/usr/share/nginx/html/StreamingAssets/tle/active.txt
URL="https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=TLE"
AGENT="SatTrak (+https://github.com/JanVogt06/SatTrak-SatelliteVisualization)"
INTERVAL="${TLE_REFRESH_SECONDS:-7200}"

mkdir -p "$(dirname "$LIVE")"
[ -f "$LIVE" ] || cp "$SEED" "$LIVE"

refresh() {
  tmp=$(mktemp)
  if curl -sSfL -A "$AGENT" "$URL" -o "$tmp"; then
    lines=$(grep -c . "$tmp" || true)
    if [ "$lines" -ge 3 ] && [ $((lines % 3)) -eq 0 ] &&
       awk 'NR%3==2 && $0 !~ /^1 / {exit 1} NR%3==0 && $0 !~ /^2 / {exit 1}' "$tmp"; then
      mv "$tmp" "$LIVE"
      echo "[tle] refreshed with $((lines / 3)) element sets"
      return
    fi
    echo "[tle] upstream sent no usable TLE data, keeping previous file"
  else
    echo "[tle] upstream request failed, keeping previous file"
  fi
  rm -f "$tmp"
}

( while :; do refresh; sleep "$INTERVAL"; done ) &

exec /docker-entrypoint.sh nginx -g 'daemon off;'
```

#### `docker/default.conf`

The Brotli blocks are the part that matters. Unity emits `.wasm.br`, `.js.br` and
`.data.br`; without the right `Content-Encoding` and `Content-Type` the browser refuses
them. More specific regex locations must come first — nginx takes the first match.

```nginx
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    location = /healthz {
        access_log off;
        default_type text/plain;
        return 200 "ok\n";
    }

    location ~ \.wasm\.br$ {
        default_type application/wasm;
        add_header Content-Encoding br;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    location ~ \.js\.br$ {
        default_type application/javascript;
        add_header Content-Encoding br;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    location ~ \.(data|symbols\.json)\.br$ {
        default_type application/octet-stream;
        add_header Content-Encoding br;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    location = /tle/active.txt {
        default_type text/plain;
        add_header Cache-Control "public, max-age=600";
    }

    location / {
        try_files $uri $uri/ /index.html;
        add_header Cache-Control "no-cache";
    }
}
```

#### `Dockerfile`

```dockerfile
FROM nginx:stable-alpine

RUN apk add --no-cache curl

COPY docker/default.conf /etc/nginx/conf.d/default.conf
COPY docker/entrypoint.sh /entrypoint.sh
COPY build/WebGL/SatTrak/ /usr/share/nginx/html/

RUN chmod +x /entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
```

#### `docker-compose.yml`

PicHunter uses 8002, so pick another port.

```yaml
services:
  sattrak:
    image: ghcr.io/janvogt06/sattrak:latest
    build: .
    container_name: sattrak
    restart: unless-stopped
    ports:
      - "${SATTRAK_PORT:-8003}:80"
    environment:
      TLE_REFRESH_SECONDS: "7200"
    healthcheck:
      test: ["CMD", "wget", "-q", "-O", "-", "http://127.0.0.1/healthz"]
      interval: 30s
      timeout: 5s
      retries: 3
```

#### `.github/workflows/release.yml`

The existing PicHunter workflow works almost unchanged. What has to be added in front of it:

```yaml
      - name: Free disk space
        uses: jlumbroso/free-disk-space@main
        with:
          tool-cache: true

      - name: Checkout code
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Refresh TLE snapshot
        run: ./tools/update-tle-snapshot.sh || echo "keeping the committed snapshot"

      - name: Cache Unity Library
        uses: actions/cache@v4
        with:
          path: unity/Library
          key: Library-WebGL-${{ hashFiles('unity/Assets/**', 'unity/Packages/**', 'unity/ProjectSettings/**') }}
          restore-keys: Library-WebGL-

      - name: Build WebGL
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: unity
          targetPlatform: WebGL
          unityVersion: 2022.3.62f3
          buildName: SatTrak
```

The rest — QEMU, Buildx, GHCR login, `docker/metadata-action`, `build-push-action` for
`linux/amd64` and `linux/arm64`, `softprops/action-gh-release` — can be copied from
PicHunter verbatim.

Three things to be aware of:

- **Unity needs a licence in CI.** GameCI wants `UNITY_EMAIL`, `UNITY_PASSWORD` and
  `UNITY_LICENSE` as repository secrets. A personal licence works; the `.ulf` file is
  produced by the GameCI activation workflow.
- **Runner disk space.** The Unity image is several GB and the Library cache adds more.
  Freeing space first is not optional.
- **Multi-arch is nearly free.** The WebGL output is platform independent; only nginx
  differs between architectures.

None of this has been executed yet. Treat the snippets as a starting point, not as tested
configuration.

## Open items and known issues

- **`cities.json` strategy** is undecided. See above.
- **Earth mode has no terrain.** Cesium streamed 3D tiles; the globe is now an ellipsoid
  with a single 8k NASA Blue Marble texture. City search, fly-to, free-fly, day/night and
  the heatmap all still work, but there is no street-level detail. At 1000 m altitude the
  whole screen covers 0.38 texels, which is why `GeoNamesSearchFromJSON.earthViewAltitude`
  now defaults to 250 km. Below roughly 50 km it stops being useful.
- **Two inspector values still need tuning** after that altitude change:
  `ViewModeController.nearEarth` is 1 m, which causes z-fighting at 250 km camera distance,
  and `FreeFlyCamera` movement speed is far too slow for that altitude.
- **Globe tessellation** is 256×128 segments, so one segment spans 156 km at the equator.
  Fine from orbit; the horizon reads as a straight edge up close. Adjustable on `EarthGlobe`.
- **One missing lighting settings asset** in `GameScene` (GUID
  `8bdf27f6e3fbb4f2f9f891fbf3dbf399`). It predates all of this work.
- **The README screenshot** still shows the Cesium globe and is out of date.

## Conventions used here

- Small commits, one line, English, imperative, no co-author trailers.
- No explanatory comments in project code. `ThirdParty/SGP` is vendored and left alone.
- Anything that belongs in the inspector stays in the inspector. Do not set values at
  runtime that could have been serialized — that is why the fly-to altitude and the TLE URLs
  became fields rather than constants.
- Verify Unity changes headlessly before committing:

  ```bash
  /Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit -nographics -projectPath unity -logFile /tmp/unity.log
  ```

  Exit code 0 and no `error CS` lines in the log.
