using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

namespace Geo
{
    public class TerrainTiles : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Georeference _georeference;
        [SerializeField] private Camera _camera;
        [SerializeField] private Shader _shader;

        [Header("Sources")]
        [Tooltip("Terrarium encoded elevation tiles, {z}/{x}/{y} in Web Mercator")]
        [SerializeField] private string _elevationUrl = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png";

        [Tooltip("Imagery tiles on the same grid as the elevation")]
        [SerializeField] private string _imageryUrl = "https://tiles.maps.eox.at/wmts/1.0.0/s2cloudless_3857/default/g/{z}/{y}/{x}.jpg";

        [Header("Detail")]
        [Tooltip("Camera altitude in metres above which no terrain is shown")]
        [SerializeField] private float _maxAltitude = 1500000f;

        [SerializeField] private int _rootZoom = 5;
        [SerializeField] private int _rootRadius = 3;
        [SerializeField] private int _maxZoom = 14;

        [Tooltip("A tile is split while the camera is closer than this many tile widths")]
        [SerializeField] private float _splitDistance = 3.5f;

        [SerializeField] private int _gridSegments = 32;

        [Tooltip("Skirt depth as a fraction of the tile width, hides cracks between detail levels")]
        [SerializeField] private float _skirtFraction = 0.02f;

        [Tooltip("Levels between a tile and the coarse tile loaded first to cover its area")]
        [SerializeField] private int _coverLevels = 4;

        [Tooltip("How far a coarse stand-in sinks below the finer tiles, as a fraction of its width")]
        [SerializeField] private float _standInSinkFraction = 0.01f;

        [Header("Loading")]
        [SerializeField] private int _maxConcurrentTiles = 6;
        [SerializeField] private int _cacheSize = 400;
        [SerializeField] private float _selectionInterval = 0.2f;

        private const double EarthCircumference = 40075016.686;

        private readonly Dictionary<TerrainTileKey, TerrainTile> _tiles = new();
        private readonly HashSet<TerrainTileKey> _shown = new();
        private readonly HashSet<TerrainTileKey> _standIns = new();
        private readonly List<TerrainTileKey> _leaves = new();
        private Material _material;
        private int _loading;
        private float _nextSelection;

        private void Awake()
        {
            _material = new Material(_shader) { name = "Terrain" };
        }

        private void Update()
        {
            if (_georeference == null || _camera == null) return;

            foreach (var key in _shown)
                _tiles[key].RebuildIfMoved(_georeference, SkirtDepth(key));

            if (Time.unscaledTime < _nextSelection) return;
            _nextSelection = Time.unscaledTime + _selectionInterval;

            var p = _camera.transform.position;
            var cameraEcef = _georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(p.x, p.y, p.z));
            var cameraLlh = Wgs84.EcefToLongitudeLatitudeHeight(cameraEcef);

            if (cameraLlh.z > _maxAltitude)
            {
                Show(new HashSet<TerrainTileKey>());
                return;
            }

            _leaves.Clear();
            var root = TerrainTileKey.Containing(_rootZoom, cameraLlh.x, cameraLlh.y);
            int rows = 1 << _rootZoom;
            for (int dy = -_rootRadius; dy <= _rootRadius; dy++)
            {
                int y = root.Y + dy;
                if (y < 0 || y >= rows) continue;
                for (int dx = -_rootRadius; dx <= _rootRadius; dx++)
                    Select(new TerrainTileKey(_rootZoom, root.X + dx, y), cameraEcef, math.max(cameraLlh.z, 0.0));
            }

            Request(cameraEcef);
            Show(Displayed());
            Evict();
        }

        private void Select(TerrainTileKey key, double3 cameraEcef, double altitude)
        {
            var center = TileCenterEcef(key);
            double width = TileWidth(key);

            double horizon = math.acos(Wgs84.SemiMajorAxis / (Wgs84.SemiMajorAxis + altitude));
            double angle = math.acos(math.clamp(math.dot(math.normalize(center), math.normalize(cameraEcef)), -1.0, 1.0));
            if (angle > horizon + width / Wgs84.SemiMajorAxis) return;

            double distance = math.distance(center, cameraEcef);
            if (key.Zoom < _maxZoom && distance < _splitDistance * width)
            {
                for (int i = 0; i < 4; i++)
                    Select(key.Child(i), cameraEcef, altitude);
                return;
            }

            _leaves.Add(key);
        }

        private void Request(double3 cameraEcef)
        {
            if (_loading >= _maxConcurrentTiles) return;

            var covers = _leaves.Select(k => Ancestor(k, math.max(_rootZoom, k.Zoom - _coverLevels)));
            var missing = covers.Concat(_leaves)
                .Distinct()
                .Where(k => !_tiles.ContainsKey(k))
                .OrderBy(k => k.Zoom > _rootZoom + _coverLevels ? 1 : 0)
                .ThenBy(k => math.distancesq(TileCenterEcef(k), cameraEcef))
                .Take(_maxConcurrentTiles - _loading)
                .ToList();

            foreach (var key in missing)
            {
                var tile = new TerrainTile(key, _gridSegments);
                _tiles[key] = tile;
                StartCoroutine(Load(tile));
            }
        }

        private IEnumerator Load(TerrainTile tile)
        {
            _loading++;

            using var elevation = UnityWebRequestTexture.GetTexture(tile.Key.Format(_elevationUrl), false);
            using var imagery = UnityWebRequestTexture.GetTexture(tile.Key.Format(_imageryUrl), true);
            var elevationOp = elevation.SendWebRequest();
            var imageryOp = imagery.SendWebRequest();
            yield return elevationOp;
            yield return imageryOp;

            _loading--;

            if (imagery.result != UnityWebRequest.Result.Success)
            {
                tile.Status = TerrainTile.State.Failed;
                yield break;
            }

            Texture2D terrarium = null;
            if (elevation.result == UnityWebRequest.Result.Success)
                terrarium = DownloadHandlerTexture.GetContent(elevation);

            tile.SetElevation(terrarium);
            if (terrarium != null) Destroy(terrarium);

            tile.SetImagery(DownloadHandlerTexture.GetContent(imagery));
            tile.Build(transform, _material, _georeference, SkirtDepth(tile.Key));
            tile.Visible = _shown.Contains(tile.Key);
            tile.Status = TerrainTile.State.Ready;
        }

        private HashSet<TerrainTileKey> Displayed()
        {
            _standIns.Clear();
            var shown = new HashSet<TerrainTileKey>();

            foreach (var leaf in _leaves)
            {
                if (IsReady(leaf))
                {
                    shown.Add(leaf);
                    continue;
                }

                for (var k = leaf; k.Zoom > _rootZoom;)
                {
                    k = k.Parent;
                    if (IsReady(k))
                    {
                        _standIns.Add(k);
                        break;
                    }
                }
            }

            shown.UnionWith(_standIns);
            return shown;
        }

        private static TerrainTileKey Ancestor(TerrainTileKey key, int zoom)
        {
            while (key.Zoom > zoom) key = key.Parent;
            return key;
        }

        private bool IsReady(TerrainTileKey key) =>
            _tiles.TryGetValue(key, out var tile) && tile.Status == TerrainTile.State.Ready;

        private void Show(HashSet<TerrainTileKey> shown)
        {
            foreach (var key in _shown)
                if (!shown.Contains(key) && _tiles.TryGetValue(key, out var tile))
                    tile.Visible = false;

            _shown.Clear();
            foreach (var key in shown)
            {
                var tile = _tiles[key];
                tile.RebuildIfMoved(_georeference, SkirtDepth(key));
                tile.Sink = _standIns.Contains(key) ? (float)(TileWidth(key) * _standInSinkFraction) : 0f;
                tile.Visible = true;
                tile.LastUsed = Time.unscaledTime;
                _shown.Add(key);
            }

            foreach (var key in _leaves)
            {
                if (_tiles.TryGetValue(key, out var tile))
                    tile.LastUsed = Time.unscaledTime;
                if (_tiles.TryGetValue(Ancestor(key, math.max(_rootZoom, key.Zoom - _coverLevels)), out var cover))
                    cover.LastUsed = Time.unscaledTime;
            }
        }

        private void Evict()
        {
            if (_tiles.Count <= _cacheSize) return;

            var stale = _tiles.Values
                .Where(t => t.Status != TerrainTile.State.Loading && !_shown.Contains(t.Key))
                .OrderBy(t => t.LastUsed)
                .Take(_tiles.Count - _cacheSize)
                .ToList();

            foreach (var tile in stale)
            {
                tile.Destroy();
                _tiles.Remove(tile.Key);
            }
        }

        private float SkirtDepth(TerrainTileKey key) => (float)(TileWidth(key) * _skirtFraction);

        private static double3 TileCenterEcef(TerrainTileKey key) =>
            Wgs84.LongitudeLatitudeHeightToEcef(new double3(key.LongitudeLatitude(0.5, 0.5), 0.0));

        private static double TileWidth(TerrainTileKey key)
        {
            double lat = key.LongitudeLatitude(0.5, 0.5).y;
            return EarthCircumference * math.cos(math.radians(lat)) / (1 << key.Zoom);
        }

        private void OnDestroy()
        {
            foreach (var tile in _tiles.Values)
                tile.Destroy();
            _tiles.Clear();
            if (_material != null) Destroy(_material);
        }
    }
}
