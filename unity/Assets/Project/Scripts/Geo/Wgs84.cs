using Unity.Mathematics;

namespace Geo
{
    public static class Wgs84
    {
        public const double SemiMajorAxis = 6378137.0;
        public const double Flattening = 1.0 / 298.257223563;
        public const double SemiMinorAxis = SemiMajorAxis * (1.0 - Flattening);

        private const double E2 = 2.0 * Flattening - Flattening * Flattening;
        private const double Ep2 = (SemiMajorAxis * SemiMajorAxis - SemiMinorAxis * SemiMinorAxis)
                                   / (SemiMinorAxis * SemiMinorAxis);

        public static double3 LongitudeLatitudeHeightToEcef(double3 longitudeLatitudeHeight)
        {
            double lon = math.radians(longitudeLatitudeHeight.x);
            double lat = math.radians(longitudeLatitudeHeight.y);
            double height = longitudeLatitudeHeight.z;

            math.sincos(lat, out var sinLat, out var cosLat);
            math.sincos(lon, out var sinLon, out var cosLon);

            double n = SemiMajorAxis / math.sqrt(1.0 - E2 * sinLat * sinLat);

            return new double3(
                (n + height) * cosLat * cosLon,
                (n + height) * cosLat * sinLon,
                (n * (1.0 - E2) + height) * sinLat);
        }

        public static double3 EcefToLongitudeLatitudeHeight(double3 ecef)
        {
            double p = math.sqrt(ecef.x * ecef.x + ecef.y * ecef.y);

            if (p < 1e-9)
            {
                double sign = ecef.z >= 0.0 ? 1.0 : -1.0;
                return new double3(0.0, sign * 90.0, math.abs(ecef.z) - SemiMinorAxis);
            }

            double theta = math.atan2(SemiMajorAxis * ecef.z, SemiMinorAxis * p);
            math.sincos(theta, out var sinTheta, out var cosTheta);

            double lat = math.atan2(
                ecef.z + Ep2 * SemiMinorAxis * sinTheta * sinTheta * sinTheta,
                p - E2 * SemiMajorAxis * cosTheta * cosTheta * cosTheta);
            double lon = math.atan2(ecef.y, ecef.x);

            double sinLat = math.sin(lat);
            double n = SemiMajorAxis / math.sqrt(1.0 - E2 * sinLat * sinLat);
            double height = p / math.cos(lat) - n;

            return new double3(math.degrees(lon), math.degrees(lat), height);
        }

        public static double4x4 EastUpNorthToEcef(double3 originEcef, double3 originLonLatHeight, double scale)
        {
            double lon = math.radians(originLonLatHeight.x);
            double lat = math.radians(originLonLatHeight.y);

            math.sincos(lat, out var sinLat, out var cosLat);
            math.sincos(lon, out var sinLon, out var cosLon);

            var east = new double3(-sinLon, cosLon, 0.0);
            var up = new double3(cosLat * cosLon, cosLat * sinLon, sinLat);
            var north = new double3(-sinLat * cosLon, -sinLat * sinLon, cosLat);

            double inverseScale = 1.0 / scale;

            return new double4x4(
                new double4(east * inverseScale, 0.0),
                new double4(up * inverseScale, 0.0),
                new double4(north * inverseScale, 0.0),
                new double4(originEcef, 1.0));
        }
    }
}
