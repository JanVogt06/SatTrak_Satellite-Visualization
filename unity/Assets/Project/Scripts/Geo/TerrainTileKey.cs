using System;
using Unity.Mathematics;

namespace Geo
{
    public readonly struct TerrainTileKey : IEquatable<TerrainTileKey>
    {
        public readonly int Zoom;
        public readonly int X;
        public readonly int Y;

        public TerrainTileKey(int zoom, int x, int y)
        {
            Zoom = zoom;
            int count = 1 << zoom;
            X = ((x % count) + count) % count;
            Y = math.clamp(y, 0, count - 1);
        }

        public TerrainTileKey Parent => new(Zoom - 1, X >> 1, Y >> 1);

        public TerrainTileKey Child(int index) => new(Zoom + 1, X * 2 + (index & 1), Y * 2 + (index >> 1));

        public double2 LongitudeLatitude(double u, double v)
        {
            double count = 1 << Zoom;
            double lon = (X + u) / count * 360.0 - 180.0;
            double n = math.PI_DBL * (1.0 - 2.0 * (Y + v) / count);
            double lat = math.degrees(math.atan(math.sinh(n)));
            return new double2(lon, lat);
        }

        public static TerrainTileKey Containing(int zoom, double longitude, double latitude)
        {
            double count = 1 << zoom;
            double lat = math.radians(math.clamp(latitude, -85.0511, 85.0511));
            int x = (int)math.floor((longitude + 180.0) / 360.0 * count);
            int y = (int)math.floor((1.0 - math.log(math.tan(lat) + 1.0 / math.cos(lat)) / math.PI_DBL) / 2.0 * count);
            return new TerrainTileKey(zoom, x, y);
        }

        public string Format(string template) => template
            .Replace("{z}", Zoom.ToString())
            .Replace("{x}", X.ToString())
            .Replace("{y}", Y.ToString());

        public bool Equals(TerrainTileKey other) => Zoom == other.Zoom && X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is TerrainTileKey other && Equals(other);

        public override int GetHashCode() => (Zoom * 73856093) ^ (X * 19349663) ^ (Y * 83492791);

        public override string ToString() => $"{Zoom}/{X}/{Y}";
    }
}
