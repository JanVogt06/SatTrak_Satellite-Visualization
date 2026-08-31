using Unity.Mathematics;
using UnityEngine;

namespace Satellites
{
    public static class ConversionExtensions
    {
        public static double3 ToDouble(this Satellites.SGP.Util.Vector3 vector) =>
            new(vector.X * 1000, vector.Y * 1000, vector.Z * 1000);

        public static Vector3 ToVector(this double3 position) =>
            new((float)position.x, (float)position.y, (float)position.z);

        public static Vector3 ToVector(this Satellites.SGP.Util.Vector3 vector) =>
            new((float)vector.X * 1000, (float)vector.Y * 1000, (float)vector.Z * 1000);
    }
}
