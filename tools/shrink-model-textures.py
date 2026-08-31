#!/usr/bin/env python3
"""Downscale textures embedded in .glb files.

Satellite models ship with textures far larger than they are ever displayed at.
A single 4096x4096 RGBA texture costs 64 MB uncompressed, plus a third again for
mipmaps, and glTF stores them PNG or JPEG compressed so the cost is invisible
until Unity imports the file.

The glb is rewritten in place: every bufferView keeps its contents and only the
image ones are replaced, so accessors stay valid because their offsets are
relative to the bufferView.
"""

import argparse
import io
import json
import struct
import sys
from pathlib import Path

from PIL import Image

GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942


def read_glb(path):
    data = path.read_bytes()
    magic, version, _ = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError(f"{path} is not a glb file")
    if version != 2:
        raise ValueError(f"{path} uses glb version {version}, only 2 is supported")

    offset = 12
    gltf = None
    binary = b""

    while offset < len(data):
        length, kind = struct.unpack_from("<II", data, offset)
        payload = data[offset + 8 : offset + 8 + length]
        if kind == JSON_CHUNK:
            gltf = json.loads(payload)
        elif kind == BIN_CHUNK:
            binary = payload
        offset += 8 + length

    if gltf is None:
        raise ValueError(f"{path} has no JSON chunk")

    return gltf, binary


def shrink_image(raw, limit):
    image = Image.open(io.BytesIO(raw))
    width, height = image.size
    if max(width, height) <= limit:
        return None, (width, height), (width, height)

    scale = limit / max(width, height)
    size = (max(1, round(width * scale)), max(1, round(height * scale)))
    resized = image.resize(size, Image.LANCZOS)

    out = io.BytesIO()
    if image.format == "JPEG":
        resized.convert("RGB").save(out, format="JPEG", quality=90, optimize=True)
    else:
        resized.save(out, format="PNG", optimize=True)

    return out.getvalue(), (width, height), size


def rewrite(path, limit, dry_run):
    gltf, binary = read_glb(path)
    views = gltf.get("bufferViews", [])
    replacements = {}
    report = []

    for image in gltf.get("images", []):
        index = image.get("bufferView")
        if index is None or index in replacements:
            continue

        view = views[index]
        start = view.get("byteOffset", 0)
        raw = binary[start : start + view["byteLength"]]

        try:
            new_raw, before, after = shrink_image(raw, limit)
        except Exception as error:
            print(f"  skipped an image in {path.name}: {error}", file=sys.stderr)
            continue

        if new_raw is None:
            continue

        replacements[index] = new_raw
        report.append(f"{before[0]}x{before[1]} -> {after[0]}x{after[1]}, "
                      f"{len(raw) / 1024:.0f} kB -> {len(new_raw) / 1024:.0f} kB")

    if not replacements:
        return None

    if dry_run:
        return report

    order = sorted(range(len(views)), key=lambda i: views[i].get("byteOffset", 0))
    out = bytearray()

    for index in order:
        view = views[index]
        start = view.get("byteOffset", 0)
        payload = replacements.get(index, binary[start : start + view["byteLength"]])
        while len(out) % 4:
            out.append(0)
        view["byteOffset"] = len(out)
        view["byteLength"] = len(payload)
        out += payload

    while len(out) % 4:
        out.append(0)

    gltf["buffers"][0]["byteLength"] = len(out)
    if "uri" in gltf["buffers"][0]:
        del gltf["buffers"][0]["uri"]

    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    json_bytes += b" " * (-len(json_bytes) % 4)

    total = 12 + 8 + len(json_bytes) + 8 + len(out)
    glb = bytearray()
    glb += struct.pack("<III", GLB_MAGIC, 2, total)
    glb += struct.pack("<II", len(json_bytes), JSON_CHUNK) + json_bytes
    glb += struct.pack("<II", len(out), BIN_CHUNK) + bytes(out)

    path.write_bytes(glb)
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path, help="directory searched for .glb files")
    parser.add_argument("--limit", type=int, default=1024, help="maximum edge length")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    files = sorted(args.root.rglob("*.glb"))
    if not files:
        print(f"no glb files below {args.root}", file=sys.stderr)
        return 1

    touched = 0
    before_total = after_total = 0

    for path in files:
        before = path.stat().st_size
        report = rewrite(path, args.limit, args.dry_run)
        after = path.stat().st_size

        before_total += before
        after_total += after

        if report:
            touched += 1
            print(f"{path.relative_to(args.root)}  {before / 1048576:.1f} MB -> "
                  f"{after / 1048576:.1f} MB")
            for line in report:
                print(f"    {line}")

    print(f"\n{touched} of {len(files)} files changed, "
          f"{before_total / 1048576:.1f} MB -> {after_total / 1048576:.1f} MB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
