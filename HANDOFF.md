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
| WebGL build | works, 148 MB, about 6 minutes locally |
| Container | built and tested locally, all headers verified |
| Release | `v0.1.0` tagged, pipeline runs on `v*` tags |
| Open blocker | Unity Localization uses synchronous Addressables, which WebGL rejects |

Cesium is gone, the TLE path no longer uses APIs WebGL lacks, and the asset budget has been
cut far enough that the build loads in a browser. It has been verified end to end: the image
serves the build, the player starts, the main menu responds and the game scene loads.

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

54 commits, in four blocks.

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

**Shipping.** The asset budget was cut, the container and the release pipeline were built and
`v0.1.0` was tagged. See the two sections after that.

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

## How a release works

Tagging is the whole process:

```bash
git tag -a v0.2.0 -m "..." && git push origin v0.2.0
```

`.github/workflows/release.yml` then refreshes the TLE snapshot, builds WebGL through GameCI,
pushes a `linux/amd64` and `linux/arm64` image to `ghcr.io/janvogt06/sattrak` and creates the
GitHub release. Consumers run:

```bash
docker compose pull && docker compose up -d
```

Port comes from `SATTRAK_PORT`, default 8003. `TLE_REFRESH_SECONDS` controls the refresh
interval, `0` disables it.

Running the workflow manually from the Actions tab builds without publishing and attaches
the result as an artifact — use that to check a change before tagging. The first run takes
one to three hours because the Unity library cache is cold; later runs reuse it.

CI needs `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_LICENSE`. A `.ulf` produced by a Unity 6
Hub activates 2022.3.62f3 without complaint — that was verified, the licence file version
does not have to match the editor.

## What the asset budget looks like

Five builds, each measured:

| | 1 | 2 | 3 | 4 | 5 |
| --- | --- | --- | --- | --- | --- |
| Build size | 283 MB | 249 MB | 223 MB | 156 MB | **148 MB** |
| Textures uncompressed | 718 MB | 718 MB | 456 MB | 243 MB | 243 MB |
| Build time | 33 min | 11 min | 8.5 min | 6 min | 6 min |

What each step did: the binary city database, then model textures capped at 1024, then at
512 plus the earth texture at 4K, the starfield at 2K and audio quality at 0.35, then help
images at 1024.

The lesson worth passing on: **judge assets by Unity's build report, not by file size on
disk.** `starlink_spacex_satellite.glb` is 15 MB as a file and was 257 MB in the build,
because glTF stores textures PNG compressed and three 4096x4096 maps expand seventeenfold on
import. `tools/shrink-model-textures.py` rewrites the embedded images; untouched originals
live in `art-source/models/`, outside `Assets/` so Unity does not import them. The shrunk
files keep their names, meta files and GUIDs, so no scene reference breaks.

Largest remaining assets: `ISS_stationary.glb` at 48.8 MB (26 textures, none oversized on
its own), then the audio tracks at roughly 10 MB each.

## Open items and known issues

**Localization is broken on WebGL.** This is the one real blocker left. The browser console
shows:

```
Exception: WebGLPlayer does not support synchronous Addressable loading.
Please do not use WaitForCompletion on the WebGLPlayer platform.
Locales PreloadOperation has not been initialized, can not return the available locales.
```

Unity's Localization package resolves locales synchronously through Addressables, which
WebGL does not support. Menu text still appears through the English fallback, so it is not
immediately obvious, but language switching is very likely dead. The fix is to await
`LocalizationSettings.InitializationOperation` before anything touches locales, rather than
letting the package block.

**Two inspector values still need tuning.** `ViewModeController.nearEarth` is 1 m, which
causes z-fighting now that the city fly-to ends at 250 km, and `FreeFlyCamera` movement speed
is far too slow for that altitude.

**The earth mode has no terrain.** Cesium streamed 3D tiles; the globe is now an ellipsoid
with one 4K NASA Blue Marble texture. City search, fly-to, free-fly, day/night and the
heatmap all work, but there is no street level detail. At 1000 m altitude the whole screen
covered 0.38 texels, which is why `GeoNamesSearchFromJSON.earthViewAltitude` defaults to
250 km. Below roughly 50 km it stops being useful.

**Globe tessellation** is 256x128 segments, so one segment spans 156 km at the equator. Fine
from orbit; the horizon reads as a straight edge up close. Adjustable on `EarthGlobe`.

**Unity 6** was considered and deliberately postponed. Its WebGL backend is better and the
memory budget would benefit, but it is a URP 14 to 17 jump that would need everything
reverified. Note the existing `unity6-upgrade` branch is useless: it forks from before this
work and reinstates Cesium. Starting fresh from `main` is the only sensible path.

**One missing lighting settings asset** in `GameScene` (GUID
`8bdf27f6e3fbb4f2f9f891fbf3dbf399`). It predates all of this work.

**The README screenshot** still shows the Cesium globe and is out of date.

**CelesTrak throttling** is easy to trip. It answers with HTTP 200 and a plain text notice
rather than an error, and polling in a loop earns an HTTP 403 for a while. Both the runtime
and the update script reject anything that is not three-line TLE format, so this degrades
into "keep the previous data" rather than breaking, but do not poll it.

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
