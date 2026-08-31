using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Heatmap;
using Satellites.SGP.Propagation;
using Satellites.SGP.TLE;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Serialization;

namespace Satellites
{
    public class SatelliteManager : MonoBehaviour
    {

        public static SatelliteManager Instance { get; private set; }

        private const string TleUrl = "https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=TLE";
        private const string TleCacheFileName = "cacheTle.txt";
        private const int SatellitesPerFrame = 250;
        private static readonly TimeSpan TleMaxAge = TimeSpan.FromHours(12);

        [Header("Prefabs & References")]
        public GameObject satellitePrefab;
        public CesiumGeoreference cesiumGeoreference;
        public GameObject satelliteParent;
        public HeatmapController heatmapController;
        public TimeSlider.TimeSlider time;

        [Header("Satellite Models")]
        [Tooltip("Available satellite models")]
        public GameObject[] satelliteModelPrefabs;

        [Tooltip("Dedicated ISS model")]
        public GameObject issModelPrefab;

        [Header("Famous Satellite Models")]
        [Tooltip("Hubble Space Telescope Model")]
        public GameObject hubbleModelPrefab;

        [Tooltip("Kepler Space Telescope Model")]
        public GameObject keplerModelPrefab;

        [Tooltip("Sentinel 6 Model")]
        public GameObject sentinelModelPrefab;

		[Tooltip("Starlink Model")]
        public GameObject starlinkModelPrefab;

        private readonly List<int> _tooNearIss = new() { 63520, 49044, 62030, 63129, 63204 };

        [Header("Materials")]
        [Tooltip("Material for satellites in space mode")]
        public Material globalSpaceMaterial;

        public DoubleSlider.Scripts.DoubleSlider altitudeSlider;

        private readonly List<Satellite> _satellites = new();
        public event Action<List<Satellite>> OnSatellitesLoaded;

        private TransformAccessArray _transformAccessArray;
        private NativeArray<Sgp4> _propagators;
        private NativeArray<Vector3> _currentPositions;
        private JobHandle _handle;

        public bool satellitesActive = true;

        private bool _ready;

        private Dictionary<int, GameObject> famousModelPrefabs;

        void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(this);
            else
                Instance = this;
        }

        void Start()
        {
            Debug.Log("SatelliteManager: Start");

            famousModelPrefabs = new Dictionary<int, GameObject>
            {
                { 20580, hubbleModelPrefab },
                { 56217, keplerModelPrefab },
                { 46984, sentinelModelPrefab },
				{ 63147, starlinkModelPrefab }
            };

            EnableGpuInstancing();
            StartCoroutine(LoadSatellites());
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            if (!satellitesActive)
            {
                return;
            }

            if (!_handle.IsCompleted) return;
            _handle.Complete();
            heatmapController.UpdateHeatmap(_currentPositions);

            var job = new MoveSatelliteJobParallelForTransform
            {
                CurrentTime = time.CurrentSimulatedTime,
                EcefToLocalMatrix = cesiumGeoreference.ecefToLocalMatrix,
                OrbitPropagator = _propagators,
                Positions = _currentPositions
            };

            _handle = job.ScheduleByRef(_transformAccessArray);
        }

        private void OnDestroy()
        {
            Debug.Log("[SatelliteManager] OnDestroy: Completing JobHandle before disposing NativeArrays.");
            _handle.Complete();

            if (_transformAccessArray.isCreated)
                _transformAccessArray.Dispose();

            if (_propagators.IsCreated)
                _propagators.Dispose();

            if (_currentPositions.IsCreated)
                _currentPositions.Dispose();
        }

        public void OnAltitudeSliderChanged(float min, float max)
        {
            foreach (var satellite in _satellites)
            {
                var timeInMinutes = (time.CurrentSimulatedTime - satellite.OrbitPropagator.Orbit.Epoch).TotalMinutes;
                var geoCoord = satellite.OrbitPropagator.FindPosition(timeInMinutes).ToGeodetic();
                if (geoCoord.Altitude < min || (!Mathf.Approximately(altitudeSlider._maxValue, max) && geoCoord.Altitude > max))
                    satellite.gameObject.SetActive(false);
                else if (!satellite.gameObject.activeSelf)
                    satellite.gameObject.SetActive(true);
            }
        }

        public List<string> GetSatelliteNames()
        {
            return _satellites.Select(s => s.gameObject.name).ToList();
        }

        public Satellite GetSatelliteByName(string name)
        {
            return _satellites.FirstOrDefault(s => s.gameObject.name == name);
        }

        public List<Satellite> GetAllSatellites()
        {
            return _satellites;
        }

        private IEnumerator LoadSatellites()
        {
            Debug.Log($"[SatelliteManager] Downloading TLE data from {TleUrl}");

            Dictionary<int, Tle> data = null;
            var source = new TleSource(TleUrl, TleMaxAge, TleCacheFileName);

            yield return source.Load(
                tles => data = tles,
                error => Debug.LogError($"[SatelliteManager] TLE download failed: {error}"));

            if (data == null)
            {
                Debug.LogError("[SatelliteManager] No TLE data received");
                yield break;
            }

            Debug.Log($"[SatelliteManager] Received {data.Count} TLE records");

            int modelledSatellites = 0;
            int created = 0;

            foreach (var tle in data.Values)
            {
                if (CreateSatellite(tle)) modelledSatellites++;

                if (++created % SatellitesPerFrame == 0)
                    yield return null;
            }

            Debug.Log($"Initialized {_satellites.Count} satellites, {modelledSatellites} with models");
            OnSatellitesLoaded?.Invoke(_satellites);

            AllocateTransformAccessArray();
            _ready = _transformAccessArray.isCreated;
        }

        private bool CreateSatellite(Tle tle)
        {
            if (_tooNearIss.Contains((int)tle.NoradNumber)) return true;

            var satelliteGo = Instantiate(satellitePrefab, satelliteParent.transform);
            var satellite = satelliteGo.GetComponent<Satellite>();

            bool modelApplied;
            try
            {
                modelApplied = satellite.Init(tle, satelliteModelPrefabs, globalSpaceMaterial,
                                              issModelPrefab, famousModelPrefabs);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SatelliteManager] Skipped {tle.NoradNumber}: {e.Message}");
                Destroy(satelliteGo);
                return false;
            }

            _satellites.Add(satellite);
            return modelApplied;
        }

        private void EnableGpuInstancing()
        {
            EnableInstancingForPrefabs(satelliteModelPrefabs);
            EnableInstancingForMaterial(globalSpaceMaterial);
        }

        private void EnableInstancingForPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null) return;
            Debug.LogWarning($"GPU instancing check: {prefabs.Length} model prefabs found");

            foreach (var prefab in prefabs)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("Prefab is null");
                    continue;
                }

                var renderer = prefab.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogWarning($"Prefab {prefab.name} has no MeshRenderer");
                    continue;
                }

                foreach (var mat in renderer.sharedMaterials)
                {
                    EnableInstancingForMaterial(mat);
                }
            }
        }

        private void EnableInstancingForMaterial(Material mat)
        {
            if (mat == null)
            {
                Debug.LogWarning("Material is null");
                return;
            }

            if (!mat.enableInstancing)
            {
                mat.enableInstancing = true;
                Debug.LogWarning($"GPU instancing enabled for material {mat.name}");
            }
            else
            {
                Debug.LogWarning($"GPU instancing already enabled for material {mat.name}");
            }
        }

        private void AllocateTransformAccessArray()
        {
            if (_satellites.Count == 0)
            {
                Debug.LogError("No satellites available to initialize the TransformAccessArray");
                return;
            }

            Debug.Log($"Initializing TransformAccessArray with {_satellites.Count} satellites");

            _propagators = new NativeArray<Sgp4>(_satellites.Count, Allocator.Persistent);
            _currentPositions = new NativeArray<Vector3>(_satellites.Count, Allocator.Persistent);

            var transforms = new Transform[_satellites.Count];
            for (int i = 0; i < _satellites.Count; i++)
            {
                transforms[i] = _satellites[i].transform;
                _propagators[i] = _satellites[i].OrbitPropagator;
            }

            _transformAccessArray = new TransformAccessArray(transforms);
            Debug.Log("TransformAccessArray initialized");
        }
    }
}
