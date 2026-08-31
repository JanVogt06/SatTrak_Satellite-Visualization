using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Heatmap
{
    public class HeatmapController : MonoBehaviour
    {
        public EarthDayNightOverlay EarthDayNightOverlay;
        public DayNightSystem DayNightSystem;
        public Toggle DayNightSystemToggle;

        public bool isVisible = false;

        public GameObject heatMapHelper;

        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private const float InfluenceRadius = 1_000_000f;
        private const float MaxDensityCount = 100f;

        private void Start()
        {
            heatMapHelper.SetActive(false);
            _mesh = GetComponent<MeshFilter>().mesh;
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.enabled = isVisible;
        }

        public void ChangeVisibility(bool value)
        {
            heatMapHelper.SetActive(value);
            isVisible = value;
            _meshRenderer.enabled = value;
            EarthDayNightOverlay.enabled = !value;
            DayNightSystem.enabled = !value;
            DayNightSystemToggle.isOn = !value;
        }

        public void UpdateHeatmap(NativeArray<Vector3> satellitePositions)
        {
            if (!isVisible) return;

            Vector3[] meshVertices = _mesh.vertices;
            var vertexWorldPositions = new NativeArray<float3>(meshVertices.Length, Allocator.TempJob);
            var satelliteFloat3 = new NativeArray<float3>(satellitePositions.Length, Allocator.TempJob);
            var colors = new NativeArray<Color>(meshVertices.Length, Allocator.TempJob);

            float3 sphereCenter = transform.position;
            float sphereRadius = transform.lossyScale.x * 0.5f;

            for (int i = 0; i < meshVertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(meshVertices[i]);
                vertexWorldPositions[i] = worldPos;
            }

            for (int i = 0; i < satellitePositions.Length; i++)
            {
                satelliteFloat3[i] = satellitePositions[i];
            }

            var job = new HeatmapDensityJob
            {
                Vertices = vertexWorldPositions,
                Satellites = satelliteFloat3,
                InfluenceRadiusSqr = InfluenceRadius * InfluenceRadius,
                MaxDensityCount = MaxDensityCount,
                SphereCenter = sphereCenter,
                SphereRadius = sphereRadius,
                Colors = colors
            };

            JobHandle handle = job.Schedule(vertexWorldPositions.Length, 32);
            handle.Complete();

            _mesh.colors = colors.ToArray();

            vertexWorldPositions.Dispose();
            satelliteFloat3.Dispose();
            colors.Dispose();
        }
    }
}
