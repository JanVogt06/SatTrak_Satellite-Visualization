using System;
using System.Collections.Generic;
using CesiumForUnity;
using DefaultNamespace;
using Unity.Mathematics;
using UnityEngine;
using Satellites.SGP;
using Satellites.SGP.Propagation;
using Satellites.SGP.TLE;
using UnityEngine.Serialization;

namespace Satellites
{
    public class SatelliteOrbit : MonoBehaviour
    {
        public bool ShouldCalculateOrbit { get; private set; }
        private Color _orbitColor;
        private Sgp4 _orbitPropagator;
        private LineRenderer _orbitRenderer;
        public TimeSlider.TimeSlider time;
        public List<Vector3> nextPositions;

        public void Initialize(Sgp4 propagator)
        {
            _orbitPropagator = propagator;

        }

        public void Update()
        {
            if (ShouldCalculateOrbit) CalculateOrbit();
            else if (_orbitRenderer) Destroy(_orbitRenderer);
        }

        public void ToggleCalculateOrbit(bool calculateOrbit, Color? orbitColor = null)
        {
            ShouldCalculateOrbit = calculateOrbit;
            if (calculateOrbit)
                _orbitColor = orbitColor.Value;
        }

        private void CalculateOrbit()
        {
            CreateOrbitGo();

            var positions = CalculateNextPositions(TimeSpan.FromHours(12), TimeSpan.FromMinutes(1));

            _orbitRenderer.positionCount = positions.Count;
            _orbitRenderer.SetPositions(positions.ToArray());
        }

        private void CreateOrbitGo()
        {
            if (_orbitRenderer) return;
            _orbitRenderer = gameObject.AddComponent<LineRenderer>();

            _orbitRenderer.startWidth = 15000f;
            _orbitRenderer.endWidth = 15000f;
            _orbitRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _orbitRenderer.startColor = _orbitColor;
            _orbitRenderer.endColor = _orbitColor;
        }

        private List<Vector3> CalculateNextPositions(TimeSpan until, TimeSpan stepSize)
        {
            var positions = new List<Vector3>();
            for (TimeSpan i = TimeSpan.Zero; i < until; i = i.Add(stepSize))
            {
                var pos = _orbitPropagator.FindPosition(time.CurrentSimulatedTime.Add(i))
                    .ToSphericalEcef();
                var position = math.mul(SatelliteManager.Instance.cesiumGeoreference.ecefToLocalMatrix,
                    new double4(pos.ToDouble(), 1.0)).xyz;
                positions.Add(position.ToVector());
            }

            nextPositions = positions;

            return positions;
        }
    }
}
