using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Heatmap
{
    [BurstCompile]
    public struct HeatmapDensityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Vertices;
        [ReadOnly] public NativeArray<float3> Satellites;
        [ReadOnly] public float InfluenceRadiusSqr;
        [ReadOnly] public float MaxDensityCount;
        [ReadOnly] public float3 SphereCenter;
        [ReadOnly] public float SphereRadius;

        [WriteOnly] public NativeArray<Color> Colors;

        public void Execute(int index)
        {
            float3 vertex = Vertices[index];
            int count = 0;

            for (int i = 0; i < Satellites.Length; i++)
            {

                float3 direction = math.normalize(Satellites[i] - SphereCenter);
                float3 projected = SphereCenter + direction * SphereRadius;

                float distSqr = math.distancesq(projected, vertex);
                if (distSqr < InfluenceRadiusSqr)
                {
                    count++;
                }
            }

            float density = math.clamp((float)count / MaxDensityCount, 0f, 1f);
            Colors[index] = Color.Lerp(Color.green, Color.red, density);
        }
    }
}
