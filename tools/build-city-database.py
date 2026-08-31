#!/usr/bin/env python3
"""Convert a GeoNames dump into the compact binary the city search reads.

Layout, little endian:

    magic     4 bytes  "SCTY"
    version   int32    1
    count     int32
    lat       float32 * count
    lon       float32 * count
    nameLen   uint16  * count   UTF-8 byte length
    names     UTF-8 blob, concatenated in the same order

Records are sorted by lowercased name so the app needs no sort at startup.
"""

import argparse
import struct
import sys
from pathlib import Path

MAGIC = b"SCTY"
VERSION = 1

# GeoNames feature codes for seats of administration, kept regardless of population
ADMIN_CODES = {"PPLC", "PPLA", "PPLA2", "PPLA3", "PPLA4"}


def read_geonames(path, min_population):
    seen = set()
    rows = []

    with open(path, encoding="utf-8") as handle:
        for line in handle:
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 15:
                continue

            name = parts[1].strip()
            if not name:
                continue

            try:
                lat = float(parts[4])
                lon = float(parts[5])
                population = int(parts[14] or 0)
            except ValueError:
                continue

            if population < min_population and parts[7] not in ADMIN_CODES:
                continue

            key = (name.lower(), round(lat, 3), round(lon, 3))
            if key in seen:
                continue
            seen.add(key)

            rows.append((name, lat, lon, population))

    return rows


def write_binary(rows, path):
    rows.sort(key=lambda r: (r[0].lower(), r[0]))

    names = [r[0].encode("utf-8") for r in rows]
    too_long = [n for n in names if len(n) > 0xFFFF]
    if too_long:
        raise ValueError(f"{len(too_long)} names exceed the 16 bit length field")

    count = len(rows)
    out = bytearray()
    out += MAGIC
    out += struct.pack("<ii", VERSION, count)
    out += struct.pack(f"<{count}f", *[r[1] for r in rows])
    out += struct.pack(f"<{count}f", *[r[2] for r in rows])
    out += struct.pack(f"<{count}H", *[len(n) for n in names])
    for n in names:
        out += n

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(out)
    return count, len(out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="GeoNames tab separated dump")
    parser.add_argument("target", type=Path, help="binary output written for Unity")
    parser.add_argument("--min-population", type=int, default=5000)
    args = parser.parse_args()

    rows = read_geonames(args.source, args.min_population)
    if not rows:
        print("no usable rows found", file=sys.stderr)
        return 1

    count, size = write_binary(rows, args.target)
    print(f"wrote {args.target} with {count} places, {size / 1_048_576:.2f} MB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
