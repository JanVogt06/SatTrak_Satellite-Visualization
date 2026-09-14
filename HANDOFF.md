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
| Unity | 6000.6.0f1 |
| Runs in the editor | yes, no account or API key needed |
| WebGL build | works, 134 MB on disk, 66 MB first load, about 6 minutes on a warm library |
| Container | built and verified before Unity 6; the music cache rule added since is untested |
| Release | `v0.3.0` tagged, pipeline runs on `v*` tags |
| Open blocker | none — the menu, the localization and the language switch all work in the browser |

Cesium is gone, the TLE path no longer uses APIs WebGL lacks, and the asset budget has been
cut far enough that the build loads quickly in a browser.

What has been verified, and how, matters here. The current build was driven by hand in a
browser against a local static server with brotli headers: the menu renders, the language
switch works, the music streams, the game scene loads with the globe and its satellites. The
**container** has not been rebuilt since the Unity 6 upgrade — the last end to end run through
nginx was on `v0.1.0`, and `docker/default.conf` has gained a `/StreamingAssets/music/` cache
rule since that nobody has exercised. That is the first thing to check before trusting a
release.

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

83 commits, in six blocks.

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
no `CBUFFER`, so it was not SRP Batcher compatible either. The translation is line for line;
**the terminator still deserves one visual check**, which a headless build cannot give.

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
`unityci/editor:ubuntu-6000.6.0f1-webgl-3`, so the image side of the upgrade is covered. The
licence has only ever been exercised against 2022.3 in CI; the first tagged Unity 6 build is
where that gets proven.

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

**Load the ISS model on demand — still open, and now the largest single item.** Of the 57 MB
that remain, roughly 49 MB is `ISS_stationary.glb`. Its textures are not the issue: all 26
are 512x512 and total 9.2 MB of the glb. The other ~12 MB is geometry, which
`tools/shrink-model-textures.py` cannot touch by design. Load the glb at runtime through
glTFast when the camera approaches the ISS instead of shipping it in every first load. That
should take the first load to roughly 20 MB.

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

**Two inspector values were retuned, and both still deserve an eyeball.**
`ViewModeController.nearEarth` was 1 m against a 1e9 m far plane — a depth ratio of 10^9, so
a 24 bit depth buffer spent nearly all of its precision in the first few metres in front of
the camera. It is now 1000 m, which clips nothing at the 250 km fly-to altitude. `FreeFlyCamera`
went from 100 to 2000 m/s, boosted from 1500 to 20000; the cubic acceleration in
`CalculateCurrentIncrease` still applies on top. Both numbers are reasoned rather than felt —
**nobody has flown with them yet.** The principled fix is to scale the speed with altitude.

**The earth mode has no terrain.** Cesium streamed 3D tiles; the globe is now an ellipsoid
with one 4K NASA Blue Marble texture. City search, fly-to, free-fly, day/night and the
heatmap all work, but there is no street level detail. At 1000 m altitude the whole screen
covered 0.38 texels, which is why `GeoNamesSearchFromJSON.earthViewAltitude` defaults to
250 km. Below roughly 50 km it stops being useful.

**Globe tessellation** is 256x128 segments, so one segment spans 156 km at the equator. Fine
from orbit; the horizon reads as a straight edge up close. Adjustable on `EarthGlobe`.

**Cesium for Unity can reach the web now**, which is why the Unity 6 upgrade was worth doing.
As of Cesium for Unity v1.20.0 (March 2026) the plugin builds for the web on Unity 6 or
later, over WebGL as well as WebGPU, and streams 3D Tiles terrain. That is the one thing the
hand-written globe cannot do. `Georeference` was deliberately written as a drop-in for
`CesiumGeoreference`, so the swap does not disturb `Wgs84`, `GlobeAnchor` or any satellite
code.

Three conditions come with it, and none are free. It is experimental by Cesium's own label.
It requires Native C/C++ Multithreading, which means the server must send
`Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp` and
`Cross-Origin-Resource-Policy: cross-origin` — and cross-origin isolation needs a secure
context, so `http://localhost:8003` works but a plain-HTTP LAN address does not. That would
weaken the `docker compose pull` promise for anyone without a TLS proxy. And the Cesium ion
token returns, which for a publicly pulled container means every visitor streams against one
account's quota. That last point is unrelated to Unity — CesiumJS would raise it too — but it
has to be answered before terrain is worth starting.

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
- Verify Unity changes headlessly before committing. `-accept-apiupdate` matters: without it
  the API updater does not run in batch mode and you get compile errors that are not real.

  ```bash
  /Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit -nographics -accept-apiupdate -buildTarget WebGL \
    -projectPath unity -logFile /tmp/unity.log
  ```

  Exit code 0 and no `error CS` lines in the log.
