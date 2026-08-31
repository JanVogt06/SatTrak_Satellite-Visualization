using System;
using CesiumForUnity;
using DefaultNamespace;
using Satellites.SGP.Propagation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Satellites
{
    public struct MoveSatelliteJobParallelForTransform : IJobParallelForTransform
    {
        [ReadOnly]public NativeArray<Sgp4> OrbitPropagator;
        [ReadOnly]public DateTime CurrentTime;
        [ReadOnly]public double4x4 EcefToLocalMatrix;
        public NativeArray<Vector3> Positions;

        public void Execute(int index, TransformAccess transform)
        {
            double tsince = (CurrentTime - OrbitPropagator[index].Orbit.Epoch).TotalMinutes;
            Vector3 position;
            try
            {
                var pos = OrbitPropagator[index].FindPosition(tsince).ToSphericalEcef();
                position = (math.mul(EcefToLocalMatrix, new double4(pos.ToDouble(), 1.0)).xyz).ToVector();
            }
            catch (Exception e)
            {
                position = new Vector3(0, 0, 0);
            }

            Positions[index] = position;
            transform.position = position;
        }
    }
}
