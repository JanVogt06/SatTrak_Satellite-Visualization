using System.Collections;
using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;

public class TerrainHeightClamp : MonoBehaviour
{
    [Tooltip("Der GlobeAnchor an eurer Kamera")]
    public CesiumGlobeAnchor globeAnchor;

    [Tooltip("Das Tileset, von dem wir die Höhe abfragen")]
    public Cesium3DTileset tileset;

    [Tooltip("Minimale Höhe über dem Gelände (m)")]
    public float minAboveGround = 5f;

    [Tooltip("Abtastrate in Hz")]
    public float sampleRateHz = 10f;

    void Start()
    {
        StartCoroutine(ClampHeightRoutine());
    }

    IEnumerator ClampHeightRoutine()
    {
        var wait = new WaitForSeconds(1f / sampleRateHz);

        while (true)
        {
            double3 llh = this.globeAnchor.longitudeLatitudeHeight;
            var task = this.tileset.SampleHeightMostDetailed(llh);

            while (!task.IsCompleted)
                yield return null;

            if (!task.IsFaulted && task.Result != null)
            {
                var result = task.Result;

                if (result.sampleSuccess != null
                  && result.sampleSuccess.Length > 0
                  && result.sampleSuccess[0])
                {

                    var llhResults = result.longitudeLatitudeHeightPositions;
                    double groundH = llhResults[0].z;

                    if (llh.z < groundH + this.minAboveGround)
                    {
                        llh.z = groundH + this.minAboveGround;
                        this.globeAnchor.longitudeLatitudeHeight = llh;
                    }
                }
            }

            yield return wait;
        }
    }
}
