# Handoff

State of the project, what changed, and what is left to do. Written for someone joining
who has not seen the earlier work.

## Where the project stands

SatTrak is a Unity satellite visualization. It downloads TLE orbital elements, propagates
them with SGP4 and renders the full active catalogue, about 16000 satellites, around a globe.

The goal is to publish it as a WebGL build served from a container, installable with
`docker compose pull` like PicHunter, SolarFlow and the Spesen generator.

| | |
| --- | --- |
| Unity | 6000.6.0f1 |
| Runs in the editor | yes, no account or API key needed |
| WebGL build | works, 139 MB on disk, 51 MB first load, 5 to 10 minutes on a warm library |
| Container | rebuilt on Unity 6 and verified end to end through nginx, music cache rule included |
| Release | `v0.3.0` is the last published release; the `v0.4.0` run failed in CI and published nothing |
| Open blocker | none — the menu, the localization and the language switch all work in the browser |

Cesium is gone, the TLE path no longer uses APIs WebGL lacks, and the asset budget has been
cut far enough that the build loads quickly in a browser.

What has been verified, and how, matters here. The current build was run from the container
image, built locally with `docker build`, and driven by hand in a browser. Every response type
carries the right headers: the `.br` files go out with `Content-Encoding: br` and the right MIME
type, the loader and `index.html` with `no-cache`, `/tle/active.txt` with ten minutes, and
`/StreamingAssets/music/` as `audio/mpeg` with the one year immutable rule. In the browser the
menu renders, the TLE file comes from `/tle/active.txt` rather than the bundled fallback, a
track streams, and the game scene loads with the globe and its satellites within about ten
seconds.

**Do not measure anything in a browser pane you cannot see.** Chrome throttles
`requestAnimationFrame` in a hidden pane to about one frame per second, and Unity's player loop
runs on it. Under that throttle the satellites took a minute to appear and the ISS model took
twelve minutes to load, and both looked like bugs. Everything measured here was measured in a
headless Chrome started with `--disable-background-timer-throttling
--disable-renderer-backgrounding --use-angle=metal`, driven over the DevTools protocol, which
renders on the GPU at a steady 60 frames per second in the menu.

The image itself has only been built on an arm64 Mac. The `linux/amd64` half and the CI
licence are still proven only by the first tagged Unity 6 run.

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

103 commits, in nine blocks.

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

**Unity 6.** The project moved from 2022.3.62f3 to 6000.6.0f1. It cost far less than the
`unity6-upgrade` branch suggested: exactly one compile error in the whole project
(`TMP_Text.enableWordWrapping`, now `textWrappingMode`) and one file the API updater fixed by
itself (`Rigidbody2D.velocity`, now `linearVelocity`). URP went 14.0.12 to 17.6.0 without a
single shader or material breaking, because the project has no renderer features, no shader
graphs and six materials.

The risk worth recording is the one that did *not* fire. `com.atteneder.gltfast` was pinned
to nothing — a bare git URL resolving to whatever `HEAD` was — and had to become
`com.unity.cloud.gltfast`, which reimports all 25 `.glb` files. Scenes reference those models
as `{fileID, guid}`, and the `.glb.meta` files carry an empty `internalIDToNameTable`, so the
ids are generated at import rather than pinned. Had the new importer numbered them
differently, every model reference in both scenes would have broken silently. Capture the
mapping before such a change and diff it afterwards:

```bash
grep -oE "fileID: -?[0-9]+, guid: \w+" unity/Assets/Project/Scenes/*.unity
```

All 25 came back identical — glTFast derives the id from the node name, and the Unity fork
kept that scheme. Scenes and prefabs were not touched at all; of 590 changed files, 572 were
`.meta` files getting importer format bumps.

`EarthDayNightOverlay.shader` was ported from `CGPROGRAM`/`UnityCG.cginc` to URP HLSL. It
still compiled under the old syntax, but that path is legacy under an SRP and the shader had
no `CBUFFER`, so it was not SRP Batcher compatible either. The translation is line for line,
and the visual check it was waiting for has been done in the browser — see the next block.

**Coordinates and time.** Checking the terminator turned up three faults that predate every
change here and that nobody could have seen from orbit, because a cloud of 16000 dots around a
sphere looks right in any orientation. All three are fixed and verified in the browser.

- *Satellites were drawn in the wrong frame.* `ToSphericalEcef` in the vendored SGP code
  returns `(-r cos φ cos λ, r sin φ, r cos φ sin λ)` — Y as the polar axis, X mirrored — and
  that went straight into the true ECEF to local matrix. The globe uses WGS84 ECEF with Z at
  the pole, so the whole constellation was rotated 180° about an oblique axis: a satellite
  over Jena was drawn over Indonesia at 7° N, 128° E, and the geostationary belt ran over the
  poles. `ConversionExtensions.ToEcef` now rotates the ECI position by GMST, the same angle
  SGP's own `ToGeodetic` uses. **The check to repeat:** look at the earth from the equator and
  the geostationary belt must be a horizontal line through it.
- *Satellites ran ahead by the UTC offset.* `TimeSlider` keeps local time, and
  `CurrentTime - Epoch` subtracts a local from a UTC `DateTime`, which ignores `Kind`. In
  Germany that put every satellite one or two hours ahead — more than an orbit for anything
  in LEO. `TimeSlider.CurrentSimulatedTimeUtc` exists for consumers; the display stays local.
- *The sun was in a frame that does not exist.* `DayNightSystem` built the sun direction with
  +Y as the earth's axis and used it as a world direction, but the world is East-Up-North at
  an origin that follows the camera. Europe sat in the dark at noon. The sun position now comes
  from the Astronomical Almanac's low precision formula (about 0.01°), rotated by GMST into
  ECEF and mapped through `Georeference`. Checked against the equation of time for three dates,
  then in the browser: Europe and Africa lit at 13:00 UTC, the Arctic in polar night and
  Antarctica in sunlight a week after the equinox.

The terminator shader itself was fine — the port is line for line, and the strong blue of the
night side is `_NightColor (0, 0, 1, 0.4)`, unchanged since the day/night system was written.

**Terrain and the web page.** Earth mode streams real terrain and Sentinel-2 imagery without a
key, and the Unity page is replaced by a SatTrak template. Both are written up under open items.

**Performance and caching.** The ISS model now loads on demand, distant satellite models are
hidden in earth mode, the build files carry content hashes, and the free fly speed follows the
altitude. Each is written up below, under the asset budget and the open items.

**Making it work in a browser.** The music left the build and now streams from
`StreamingAssets`, which halved the first load. Then the reason nobody had noticed how broken
the web build was: no text rendered at all, in either scene. That turned out to be the TMP
shader, not localization — and behind it a second fault, two Addressables operations that
never complete on the web. Both are written up under "Open items and known issues", along
with the near clip plane, the camera speed, the dangling lighting settings asset and a
`Shader.Find` that asked for a built-in shader under URP.

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
git tag -a v0.4.0 -m "..." && git push origin v0.4.0
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

CI needs `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_LICENSE`. GameCI publishes
`unityci/editor:ubuntu-6000.6.0f1-webgl-3`. The licence is proven on Unity 6: the `v0.4.0` run
activated it and compiled the whole project.

That run still failed, and the cause is worth knowing. With **Name Files As Hashes** turned on,
Unity's build backend on the Linux runner re-ran its build program six times, each time
because one of its own intermediate files had a new timestamp, and then gave up with
*Internal build system error. Backend has requested a buildprogram run 6 times*. It is a known
Unity bug tied to that setting, and it never happened on the Mac. The setting is off again;
see the caching note under open items for what replaced it. A manual run on `main` with the
setting off then built cleanly in 36 minutes on a warm cache: 138 MB, no errors, two backend
reruns instead of six. `v0.4.0` published nothing — no image, no release — so the next tag is
the first Unity 6 release.

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

The Unity 6 upgrade moved that to 140 MB on its own: `SatTrak.data.br` fell from 138 MB to
129 MB while `SatTrak.wasm.br` grew from 7.4 MB to 8.8 MB. The engine got bigger, the assets
got slightly smaller, and the picture did not change — **the engine was never the problem.**
Streaming the music then took it to where it stands today, 134 MB on disk and 66 MB of first
load.

### Where the 129 MB actually are

| | Share of the build |
| --- | --- |
| 8 music tracks | ~80 MB |
| `ISS_stationary.glb` | ~49 MB |
| everything else — engine, scenes, globe, 24 satellite models, UI, help | ~10 MB |

The application is ten megabytes. The rest is background music and one model. Two changes
would take the first load to roughly 20-30 MB, and neither needs a new engine:

**The music is out of the build — done.** The import settings had always been right
(streaming, Vorbis, quality 0.35, `preloadAudioData: 0`) but none of that applies on the web,
where Unity hands audio to the Web Audio API and the clip data sits in `.data` regardless.
`MusicManager` held a `List<AudioClip>` in the inspector, so the scene referenced all eight
and all eight shipped.

The tracks now live in `unity/Assets/StreamingAssets/music/` and load through
`UnityWebRequestMultimedia.GetAudioClip`, the same pattern the TLE path uses. File names are
inspector fields on `MusicManager`, not constants. `SatTrak.data.br` fell from **129 MB to
57 MB**; the first load is now about 66 MB and one ~9 MB track streams in behind it. nginx
serves `/StreamingAssets/music/` with a one year immutable cache.

The 320 kbps originals were 179 MB and now live in `art-source/audio/`, outside `Assets/`,
matching what `art-source/models/` does. What ships is VBR ~130 kbps, 70 MB for all eight —
that is the same bitrate the Vorbis q0.35 import was already producing, so nothing audible
changed. Re-encode from the originals if that judgement needs revisiting.

Verified by serving the build locally with brotli headers and watching the request log:
`GET /StreamingAssets/music/Quiet%20Wormhole.mp3` returned 200 and the audio context
resumed.

**The ISS model loads on demand — done, and it saved less than predicted.** The glb now lives
in `unity/Assets/StreamingAssets/models/`, where Unity does not import it, and
`SatelliteModelController` loads it through glTFast the first time the camera comes within
5000 km of the ISS in earth mode. Until then the ISS wears a random generic model. nginx serves
the file gzip compressed from a copy the Dockerfile writes, 14.0 MB on the wire instead of
21.8 MB, revalidated with an ETag rather than cached for a year.

`SatTrak.data.br` fell from 59.7 MB to 42.3 MB, and the first load from about 68 MB to about
51 MB. The 20 MB this section used to promise was wrong: the 49 MB the ISS took was the
*uncompressed* size from the build report, and after Brotli it had only ever been 17 MB of the
download. What remains is mostly the textures of the 24 other satellite models, about 100 MB
uncompressed in the build report. They are the next lever, and the same on demand path would
work for them.

Three things had to be right for this to work, and each is worth knowing before touching it:

- **Shader variants.** glTFast builds materials at runtime from shader graphs, and a build only
  contains the variants something referenced at build time. All 29 ISS materials use the
  keyword-free `glTF-pbrMetallicRoughness` variant, which 158 materials of the editor imported
  models already pull in. A model with alpha, emission or texture transforms would need its
  variants added to a shader variant collection first, or it renders magenta.
- **`UninterruptedDeferAgent`.** glTFast's default spreads a load across frames, and this one
  took about 1800 frames. At the frame rate of earth mode that was twelve minutes. Without
  deferral it takes two to six seconds with one visible hitch.
- **`ConsoleLogger`.** Without a logger glTFast fails silently.

## Open items and known issues

**Localization and text rendering are fixed.** This was two separate faults wearing one
costume, and the visible symptom — a main menu of empty outlines, no text anywhere, not even
the static `Loading...` — belonged to the second one.

*Fault one: the fonts were invisible.* `LeagueSpartan-Regular SDF` and `-Thin SDF` used the
`TextMeshPro/Distance Field` shader, which does not render on the web under Unity 6.6 and URP
17. Nothing was broken in the usual places: the font assets existed, the atlases were static
and populated, the materials resolved, and the shader compiled into the build. A runtime dump
showed every text component correct — content set, font assigned, colour white, alpha 1,
geometry generated with the right bounds — and still nothing on screen. Swapping the material
to `TextMeshPro/Mobile/Distance Field` at runtime made the whole menu appear at once. Both
font assets now use the mobile shader, which is what Unity's own `LiberationSans SDF` already
shipped with. **The tell was that Unity's IMGUI development console rendered text fine while
no TMP text did** — that is the check to repeat if this ever comes back.

*Fault two: two Addressables operations never finish on the web.* Both
`LocalizationSettings.InitializationOperation` and `LocalizationSettings.SelectedLocaleAsync`
sit at `IsDone == false` forever — measured, not guessed: a 30 second poll of the first and a
15 second poll of the second both timed out while the rest of localization worked normally.
`LocalizationSettings.AvailableLocales.Locales` stays empty for the same reason. So:

- **Never yield on `InitializationOperation`.** It blocks forever. `MenuManager` gates on the
  locales `PreloadOperation` instead, which does complete.
- **Never read `LocalizationSettings.SelectedLocale`.** It is a synchronous property
  (`AsyncOperationUtility.SynchronousLoad`), and on the web `WaitForCompletion` throws
  unconditionally — there is no "already done" short circuit in `AsyncOperationBase`.
- **Never index into `AvailableLocales.Locales`.** It is empty. `ApplyLocale` now matches on
  the locale code and falls back to `Locale.CreateLocale`, which the string database accepts.
- `GetLocalizedStringAsync` works fine. The dropdowns are filled from it; the synchronous
  `GetLocalizedString` is what used to throw and abort `Awake` half way through.

Language switching now works at runtime, verified in a browser: picking German turns the
whole UI German within a second or two.

**Earth mode ran at three frames per second — fixed.** Every one of the 16045 satellites
switched its full 3D model on in earth mode, including the ones on the far side of the planet.
Measured on an M3: 3.4 frames per second with satellites shown, 51 with them hidden. Models are
now only shown within `SatelliteModelController.earthModeModelDistance`, 2000 km by default,
which is about the range `FreeFlyCamera.maxDistance` already used for picking. A 40 km model
at that distance is around twenty pixels. Earth mode now runs at 33 frames per second.

**Space mode runs at 13 to 17 frames per second** on the same machine and was not looked into.
Sixteen thousand `SatelliteModelController.Update` calls a frame are the first suspect.

**The build files were cached for a year under fixed names — fixed.** nginx sent every
`.br` file with `max-age=31536000, immutable`, but Unity names them `SatTrak.data.br`,
`SatTrak.wasm.br` and so on. After a release a returning browser fetched the new `index.html`
and loader and kept the old engine: the request log showed `data.br` fetched again and
`wasm.br` and `framework.js.br` not at all. Twice in testing that combination hung the page
before the menu. The first fix, content hashed file names, breaks the CI build (see the release
section), so the files keep their names and nginx now sends them with `no-cache`. The browser
asks on every visit and gets a body-less 304 unless the build changed. `BuildWebGL.Run` still
empties the `Build` folder first, which costs nothing and keeps stale files out of the image.

`no-cache` alone did not save anyone who had visited before. Browsers keep the old
`immutable` entries for a year no matter what the server says later, and the first real
deployment of `v0.4.1` showed it: a Safari that had seen an older version loaded the old engine
against the new data and printed *No translation found for 'Key Id …'* on every label, and in a
reproduction the same mix crashed with a stack overflow. Only a new URL gets past such an entry.
`BuildWebGL.Run` now hashes the files in `Build/` and writes the first twelve hex digits into
`index.html` in place of `__BUILD_ID__`, so every build loads `SatTrak.wasm.br?v=…` and friends
from a URL no browser has cached. Reproduced and fixed the same way: load `0.3.0`, switch the
container to the new build on the same port, reload — all four files are fetched again and the
menu is correct. Unity's own IndexedDB cache was never the problem; it already revalidates the
data file and the Addressables bundles.

**The free fly speed follows the altitude now.** `FreeFlyCamera` moves at a tenth of its
altitude per second, never slower than 100 m/s, and `Shift` multiplies that by ten: 25 km/s at
the 250 km fly-to altitude, 100 m/s near the ground. The old note claimed a cubic acceleration
applied on top; it does not, `_enableSpeedAcceleration` is off in the scene. Like
`ViewModeController.nearEarth`, now 1000 m instead of 1 m, **these numbers are reasoned, not
flown.** Nobody has steered the camera with a keyboard since.

**The search filter dropdown was empty in German.** `SearchPanelController` filled it with the
synchronous `GetLocalizedString` in `Start`, before the German table had loaded. It now uses
`GetLocalizedStringAsync`, like the menu dropdowns.

**Satellite tracking works, but not for the reasons it looks like.**
`SearchPanelController.TheLoop` writes the satellite's Unity position in metres into
`georeference.latitude`, `longitude` and `height`, and
`ViewModeController.ZoomToPositionSatellite` writes the same metres into the camera's
`GlobeAnchor` as longitude, latitude and height. Both get the same nonsense coordinate, which
puts the camera at the new origin, and the orbit controller then places it near the satellite.
The fly-in animation divides by zero (`Time.deltaTime / 0`), so it jumps in one frame. All of
this predates the work here and was left alone because it produces the right picture; anyone
changing tracking should replace it with a geodetic position from SGP4 rather than patch it.
In one of five test runs the camera ended up next to the satellites around the ISS rather than
on it. That run could not be repeated.

**Terrain is in, without Cesium and without a key.** The requirement was that every visitor
sees terrain, that nobody needs a token, and that it costs the operator nothing. Cesium ion
fails the last two: its free Community plan wants a token in the page and covers 15 GB of
streaming a month, after which a paid plan is expected. Cesium for Unity on the web would also
have needed cross-origin isolation, which means HTTPS for anyone pulling the container.

What ships instead is `Geo/TerrainTiles` and three helpers, about 670 lines with the shader, fed by two open tile services that
browsers fetch directly — no account, no key, CORS open, nothing proxied through the container:

| | Source | Licence |
| --- | --- | --- |
| Elevation | Mapzen Terrain Tiles on AWS Open Data, Terrarium PNG, `{z}/{x}/{y}` | open, attribution |
| Imagery | EOX Sentinel-2 cloudless 2016, layer `s2cloudless_3857` | CC BY 4.0 |

Use the 2016 layer specifically. EOX's later years are CC BY-NC-SA, which would make the
project non-commercial. Both URLs are inspector fields on `TerrainTiles`. Both services ask
for attribution: a line in the page's top left credits EOX, Mapzen and CelesTrak, and the info
dialog carries the full list of Mapzen's upstream sources. Either service could change its terms
or go away; the globe underneath is still there when that happens, it just stays blurry.

How it works: below 1500 km altitude, `TerrainTiles` picks Web Mercator tiles in a quadtree —
roots at zoom 5 around the camera, split while the camera is closer than 3.5 tile widths, down
to zoom 14 — and drops anything past the horizon. Each tile is a 33x33 grid built in WGS84 ECEF
through `Georeference`, so it lines up with the globe and the satellites, and it is rebuilt when
the origin moves. Skirts hide cracks between levels. Four levels coarser than every wanted tile
a cover tile loads first, so the view is covered at once and sharpens as finer tiles arrive;
coarse stand-ins sink one percent of their width below the finer ones instead of fighting them
for depth. `TerrainTile.shader` multiplies the imagery by the sun's Lambert term, with a depth
offset so terrain at sea level wins over the ellipsoid.

Measured from 40 km over Denver on the M3: 234 tile requests, the screen covered after four
seconds, sharp to the horizon after thirty, 30 frames per second. The city fly-to altitude came
down from 250 km to 40 km because the view now holds up there.

Not done: no water mask, so sea level is flat imagery; no terrain collision; heights below
zero are clamped, so the Dead Sea is flat; and the imagery is 2016.

**The web page is SatTrak's own now.** `unity/Assets/WebGLTemplates/SatTrak` replaces Unity's
default page, selected through `webGLTemplate: PROJECT:SatTrak`. It fills the window, shows a
loading screen in the game's style — thin white corner brackets, amber accent, League Spartan,
a starfield and a glowing horizon — and reports failures on the page instead of in an
`alert`. After loading, a fullscreen and an info button fade in at the top right, and the info
dialog carries controls and data credits. The font is self-hosted as subset WOFF files (SIL
OFL); Google Fonts was left out on purpose, because embedding it is a GDPR problem in Germany.
Unity's own splash screen is off, which Unity 6 allows on every licence. The render resolution
is capped at 1.5x the CSS pixel size to keep high DPI screens from costing frame rate.

**Globe tessellation** is 256x128 segments, so one segment spans 156 km at the equator. It now
only shows beyond the terrain, which hides it below 1500 km. Adjustable on `EarthGlobe`.

**The missing lighting settings asset is gone.** `GameScene` pointed at GUID
`8bdf27f6e3fbb4f2f9f891fbf3dbf399`, which does not exist; it is now `{fileID: 0}`, matching
`MainMenu` and matching what Unity was already doing in practice.

**`Shader.Find("Standard")`** in `SatelliteModelController` now asks for
`Universal Render Pipeline/Lit`. It was an unreachable fallback rather than a live bug, since
`globalSpaceMaterial` is assigned in `GameScene`, but it would have rendered magenta the day
that changed. `Shader.Find("Sprites/Default")` in `SatelliteOrbit` is fine — that shader is in
the Always Included list.

**`SphereCollider` is stripped from the web build.** The console repeats *Can't add component
because class 'SphereCollider' doesn't exist!* — engine code stripping drops it because no
scene object uses one and the primitives are created at runtime. Harmless where it was found
(`SatelliteModelController` destroys the collider immediately anyway) but it points at a whole
class of runtime-created components that stripping cannot see. Left alone deliberately.

**The music is cached for a year under unhashed names.** `/StreamingAssets/music/` is served
`immutable`, but the files are plain track names. Re-encode a track under the same name and
returning visitors keep the old one for a year. Rename the file when the content changes.

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
- Verify Unity changes headlessly before committing. `-accept-apiupdate` matters: without it
  the API updater does not run in batch mode and you get compile errors that are not real.

  ```bash
  /Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit -nographics -accept-apiupdate -buildTarget WebGL \
    -projectPath unity -logFile /tmp/unity.log
  ```

  Exit code 0 and no `error CS` lines in the log.
