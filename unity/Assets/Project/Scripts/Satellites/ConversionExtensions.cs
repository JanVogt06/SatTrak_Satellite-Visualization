using Satellites.SGP.CoordinateSystem;
using Unity.Mathematics;
using UnityEngine;

namespace Satellites
{
    public static class ConversionExtensions
    {
        public static double3 ToDouble(this Satellites.SGP.Util.Vector3 vector) =>
            new(vector.X * 1000, vector.Y * 1000, vector.Z * 1000);

        public static double3 ToEcef(this EciCoordinate eci)
        {
            math.sincos(Satellites.SGP.Util.TimeExtensions.ToGreenwichSiderealTime(eci.Time), out var sinTheta, out var cosTheta);
            var p = eci.Position.ToDouble();
            return new double3(
                cosTheta * p.x + sinTheta * p.y,
                -sinTheta * p.x + cosTheta * p.y,
                p.z);
        }

        public static Vector3 ToVector(this double3 position) =>
            new((float)position.x, (float)position.y, (float)position.z);

        public static Vector3 ToVector(this Satellites.SGP.Util.Vector3 vector) =>
            new((float)vector.X * 1000, (float)vector.Y * 1000, (float)vector.Z * 1000);
    }
}
