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
| Docker / CI | files in place, never executed; Unity secrets missing |
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

The container and the release workflow already exist. What is missing is the Unity licence
in CI and a real end-to-end run.

| File | Purpose |
| --- | --- |
| `Dockerfile` | `nginx:stable-alpine`, copies `build/WebGL/SatTrak/` into the web root |
| `docker/default.conf` | Brotli and gzip headers for `.wasm`, `.js` and `.data`, `/healthz`, the TLE location |
| `docker/entrypoint.sh` | Seeds the TLE file from the bundled snapshot, refreshes it every two hours |
| `docker-compose.yml` | GHCR image, port via `SATTRAK_PORT` (default 8003), healthcheck |
| `.dockerignore` | Keeps `unity/Library` out of the build context — it is 6.7 GB |
| `.github/workflows/release.yml` | `v*` tag builds WebGL with GameCI and pushes to GHCR |

The entrypoint only replaces the served TLE file when the response passes the three-line
format check, so a throttled or failed request leaves the previous copy in place. Set
`TLE_REFRESH_SECONDS=0` to disable refreshing entirely.

Three things still to do:

- **Repository secrets.** GameCI needs `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_LICENSE`.
  The `.ulf` file comes from the GameCI activation workflow. Without them the release job
  fails at the build step.
- **Runner disk space.** The workflow frees space before building because the Unity image
  plus the library cache does not fit otherwise.
- **A real run.** Neither the workflow nor a multi-arch image build has been executed yet.

Locally the same thing can be built and served with:

```bash
docker compose build && docker compose up -d
curl -sI http://localhost:8003/healthz
```

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
