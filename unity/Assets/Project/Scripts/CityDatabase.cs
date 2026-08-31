using System;
using System.Collections.Generic;
using System.Text;

public static class CityDatabase
{
    private const int Magic = 'S' | ('C' << 8) | ('T' << 16) | ('Y' << 24);
    private const int SupportedVersion = 1;

    public static List<LocationEntry> Read(byte[] data)
    {
        if (data == null || data.Length < 12)
            throw new InvalidOperationException("city database is empty or truncated");

        if (BitConverter.ToInt32(data, 0) != Magic)
            throw new InvalidOperationException("city database has an unexpected header");

        int version = BitConverter.ToInt32(data, 4);
        if (version != SupportedVersion)
            throw new InvalidOperationException($"city database version {version} is not supported");

        int count = BitConverter.ToInt32(data, 8);
        if (count < 0)
            throw new InvalidOperationException("city database reports a negative count");

        int latOffset = 12;
        int lonOffset = latOffset + count * 4;
        int lengthOffset = lonOffset + count * 4;
        int nameOffset = lengthOffset + count * 2;

        if (nameOffset > data.Length)
            throw new InvalidOperationException("city database is shorter than its header claims");

        var entries = new List<LocationEntry>(count);
        int cursor = nameOffset;

        for (int i = 0; i < count; i++)
        {
            int length = BitConverter.ToUInt16(data, lengthOffset + i * 2);
            if (cursor + length > data.Length)
                throw new InvalidOperationException($"city database truncated at entry {i}");

            entries.Add(new LocationEntry
            {
                name = Encoding.UTF8.GetString(data, cursor, length),
                lat = BitConverter.ToSingle(data, latOffset + i * 4),
                lon = BitConverter.ToSingle(data, lonOffset + i * 4)
            });

            cursor += length;
        }

        return entries;
    }
}
