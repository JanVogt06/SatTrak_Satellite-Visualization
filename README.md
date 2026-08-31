# SatTrak

Interactive 3D satellite visualization built in Unity. TLE orbital elements are pulled from
CelesTrak and propagated with an SGP4 implementation to place more than 5000 active
satellites on a textured globe.

![SatTrak](screenshots/main-view.png)

## Status

Cesium has been removed. The globe is a generated WGS84 ellipsoid mesh textured with NASA
Blue Marble imagery, and the georeference math lives in `Assets/Project/Scripts/Geo`. No
account and no API key are needed to run the project.

The remaining work towards a browser build is the asset budget: `cities.json` alone is
189 MB and is parsed into roughly 700 MB of managed heap at startup, which no browser
will survive.

## Requirements

- Unity 2022.3.62f3
- Git LFS

## Setup

```bash
git lfs install
git clone https://github.com/JanVogt06/SatTrak-SatelliteVisualization.git
cd SatTrak-SatelliteVisualization
git lfs pull
```

Without `git lfs pull` the satellite models and the city database stay 130-byte pointer
files and the project will not run.

Add `unity/` as a project in Unity Hub and open it.

## Controls

Space mode:

| Input | Action |
| --- | --- |
| Left mouse drag | Rotate the globe |
| Scroll wheel | Zoom |
| `Esc` | Open the menu |

Earth mode:

| Input | Action |
| --- | --- |
| `Esc` | Toggle inspection and camera mode |
| `W` `A` `S` `D` | Move |
| Mouse | Look around |
| `Shift` | Move faster |
| Scroll wheel | Move forward and backward |
| `R` | Return to the start position |

## Repository layout

```
screenshots/                  README image
unity/
  Assets/
    AddressableAssetsData/    Addressables configuration (fixed path)
    TextMesh Pro/             TextMesh Pro essentials (fixed path)
    Project/                  Everything written for this project
      Art/                    Animations, fonts, images, materials, UI sprites
      Art/Earth/              Blue Marble texture and globe material (Git LFS)
      Audio/                  Background music
      Data/Cities/            GeoNames city database (Git LFS)
      Localization/           German and English string tables
      Models/                 Satellite models (Git LFS)
      Prefabs/
      Resources/HelpImages/   Loaded at runtime by the help panel
      Scenes/                 MainMenu and GameScene
      Scripts/
      Settings/               URP render pipeline assets
    ThirdParty/               Vendored assets, kept as delivered
      DoubleSlider/           Altitude range slider
      SGP/                    SGP4 propagator port
      SimpleSpinner/          Loading spinner
      Skyboxes/               Space skyboxes
  Packages/                   Unity Package Manager manifest and lock file
  ProjectSettings/
```

## Scripts

| Script | Responsibility |
| --- | --- |
| `Satellites/SatelliteManager` | TLE download, satellite spawning, job scheduling |
| `Satellites/Satellite` | Per-satellite state and orbital elements |
| `Satellites/SatelliteOrbit` | Orbit path rendering |
| `Satellites/SatelliteModelController` | Model and sphere switching per camera mode |
| `Satellites/MoveSatelliteJobParallelForTransform` | Burst job moving satellite transforms |
| `Satellites/ConversionExtensions` | SGP to Unity coordinate conversion |
| `Satellites/TleSource` | TLE download over UnityWebRequest with a 12 hour disk cache |
| `Geo/Wgs84` | Ellipsoid math: geodetic and ECEF conversion, East-Up-North frame |
| `Geo/Georeference` | Local frame origin, ECEF transforms, floating origin |
| `Geo/GlobeAnchor` | Keeps a transform fixed to a geographic position |
| `Geo/EarthGlobe` | Generates the textured globe mesh |
| `ViewModeController` | Transition between space and earth mode |
| `FreeFlyCamera` | First person camera for earth mode |
| `GlobeRotationController` | Orbit camera around the globe |
| `CameraFlySequence` | Scripted camera moves in the main menu |
| `Lighting/DayNightSystem` | Sun position and ambient light |
| `Lighting/EarthDayNightOverlay` | Terminator overlay shader driver |
| `Heatmap/HeatmapController` | Satellite density heatmap |
| `Heatmap/HeatmapDensityJob` | Burst job computing density |
| `TimeSlider/TimeSlider` | Simulated time and time multiplier |
| `TimeSlider/SliderStep` | Slider zoom steps |
| `UI/SearchPanelController` | Satellite search, filter and tracking |
| `UI/GameHudController` | HUD animations, FPS display, quit |
| `UI/HelpContentBuilder` | Renders the markdown help pages |
| `UI/HelpPanelController` | Help panel visibility |
| `UI/TabBarController` | Tab switching |
| `UI/SatelliteLabelUI` | Satellite name labels |
| `UI/SatelliteShowHide` | Satellite visibility toggle |
| `UI/ISSQuickButton` | Jump to the ISS |
| `UI/TooltipController` | Tooltips |
| `UI/TOCButton` | Help table of contents entries |
| `UI/HelpImageScaler` | Aspect ratio for help images |
| `UI/ForceAspectRatio16x9` | Letterboxing |
| `GeoNamesSearchFromJSON` | City search over the GeoNames database |
| `MenuManager` | Main menu, resolution and language settings |
| `SceneSwitcher` | Async scene loading with progress bar |
| `MusicManager` | Background music playback |
| `CrosshairSelector`, `CrosshairSettings`, `CustomCursor` | Crosshair and cursor customization |
| `MainMenuCameraMovement`, `MainMenuSatelliteSpawner`, `FlyingUIPhysics` | Main menu decoration |

## Data sources

- TLE data: [CelesTrak](https://celestrak.org/)
- City database: [GeoNames](https://www.geonames.org/)
- Satellite models: NASA
- Earth texture: NASA Visible Earth, Blue Marble Next Generation (public domain)

## Credits

University project at FSU Jena by Jan Vogt, Yannik Köllmann, Leon Erdhütter and
Niklas Maximilian Becker-Klöster.
