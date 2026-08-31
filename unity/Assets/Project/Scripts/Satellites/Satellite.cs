using System;
using System.Collections.Generic;
using System.Linq;
using Satellites.SGP.Propagation;
using Satellites.SGP.TLE;
using Satellites.SGP.Util;
using UnityEngine;
using UnityEngine.Serialization;

namespace Satellites
{
    public class Satellite : MonoBehaviour
    {
        [FormerlySerializedAs("controller")] public SatelliteOrbit orbit;
        [FormerlySerializedAs("materialController")] public SatelliteModelController modelController;
        public Sgp4 OrbitPropagator;
        public Tle Tle;

        public bool IsISS { get; private set; }
        public bool IsFamous { get; private set; }
        public int NoradId { get; private set; }

        private static readonly int[] FAMOUS_NORAD_IDS = { 20580, 56217, 46984, 63147 };

        public bool Init(Tle tle, GameObject[] satelliteModelPrefabs, Material globalSpaceMaterial,
                         GameObject issModelPrefab = null, Dictionary<int, GameObject> famousModelPrefabs = null)
        {
            name = tle.NoradNumber + " " + tle.Name;
            Tle = tle;
            NoradId = (int)tle.NoradNumber;

            IsISS = tle.NoradNumber == 25544;

            IsFamous = FAMOUS_NORAD_IDS.Contains((int)tle.NoradNumber);

            OrbitPropagator = new Sgp4(tle);
            orbit.Initialize(OrbitPropagator);

            GameObject specialModel = null;
            if (IsISS && issModelPrefab != null)
            {
                specialModel = issModelPrefab;
            }
            else if (IsFamous && famousModelPrefabs != null && famousModelPrefabs.ContainsKey(NoradId))
            {
                specialModel = famousModelPrefabs[NoradId];
            }

            return modelController.SetModel(satelliteModelPrefabs, globalSpaceMaterial, IsISS || IsFamous, specialModel);
        }
    }
}
