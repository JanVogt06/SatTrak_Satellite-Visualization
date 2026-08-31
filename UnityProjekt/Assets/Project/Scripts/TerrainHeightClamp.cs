using System.Collections;
using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;

public class TerrainHeightClamp : MonoBehaviour
{
    [Tooltip("Globe anchor attached to the camera")]
    public CesiumGlobeAnchor globeAnchor;

    [Tooltip("Tileset the height is sampled from")]
    public Cesium3DTileset tileset;

    [Tooltip("Minimum height above terrain in meters")]
    public float minAboveGround = 5f;

    [Tooltip("Sample rate in Hz")]
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
