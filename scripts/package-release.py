#!/usr/bin/env python3
"""Build and locally validate upload archives using only Python's standard library."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
RELEASE = ROOT / "release"
REQUIRED_DEPENDENCIES = [
    "denikson-BepInExPack_Valheim-5.4.2351",
    "ValheimModding-Jotunn-2.30.2",
]
DLL_NAME = "BepInEx/plugins/Ironbound/Ironbound.dll"
RELEASE_CHANNEL = "alpha"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def metadata():
    manifest = json.loads((RELEASE / "manifest.json").read_text(encoding="utf-8-sig"))
    require(set(manifest) == {"name", "version_number", "website_url", "description", "dependencies"},
            "Unexpected manifest fields")
    require(isinstance(manifest["name"], str) and re.fullmatch(r"[A-Za-z0-9_]{1,128}", manifest["name"]), "Invalid package name")
    version = manifest["version_number"]
    require(isinstance(version, str) and re.fullmatch(r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)", version), "Invalid package version")
    require(isinstance(manifest["description"], str) and 0 < len(manifest["description"]) <= 250, "Description must be 1-250 characters")
    require(manifest["website_url"] == "https://github.com/XEngine/ironbound", "Unexpected website URL")
    require(manifest["dependencies"] == REQUIRED_DEPENDENCIES, "Dependency versions changed; verify official packages first")
    project = ET.parse(ROOT / "src/Ironbound/Ironbound.csproj").getroot()
    require(project.findtext("PropertyGroup/Version") == version, "Project/manifest version mismatch")
    require(project.findtext("PropertyGroup/InformationalVersion") == version + "-" + RELEASE_CHANNEL,
            "Assembly release-channel metadata mismatch")
    plugin = (ROOT / "src/Ironbound/Plugin.cs").read_text(encoding="utf-8-sig")
    require(f'PluginVersion = "{version}";' in plugin, "Plugin/manifest version mismatch")
    return manifest


def validate_icon(data):
    require(len(data) >= 33 and data[:8] == b"\x89PNG\r\n\x1a\n" and data[12:16] == b"IHDR", "Icon must be PNG")
    require(struct.unpack(">II", data[16:24]) == (256, 256), "Icon must be exactly 256x256")
    # Validate every chunk CRC as well as the header, detecting truncated/corrupt files.
    import zlib
    offset, ended = 8, False
    while offset + 12 <= len(data):
        length = struct.unpack(">I", data[offset:offset + 4])[0]
        end = offset + 12 + length
        require(end <= len(data), "Truncated PNG chunk")
        body = data[offset + 4:end - 4]
        require(zlib.crc32(body) & 0xFFFFFFFF == struct.unpack(">I", data[end - 4:end])[0], "Invalid PNG chunk CRC")
        if body[:4] == b"IEND":
            require(length == 0 and end == len(data), "Invalid PNG ending")
            ended = True
            break
        offset = end
    require(ended, "Missing PNG end chunk")


def payload():
    files = {
        DLL_NAME: ROOT / "src/Ironbound/bin/Release/net472/Ironbound.dll",
        "README.md": RELEASE / "README.md",
        "CHANGELOG.md": RELEASE / "CHANGELOG.md",
        "INSTALL.md": ROOT / "docs/INSTALL.md",
        "MOUNTS.md": ROOT / "docs/MOUNTS.md",
        "THIRD_PARTY_NOTICES.md": RELEASE / "THIRD_PARTY_NOTICES.md",
        "LICENSE": ROOT / "LICENSE",
    }
    fonts = ROOT / "src/Ironbound/Assets/Fonts"
    licenses = sorted(fonts.glob("*-OFL.txt"))
    require(len(licenses) == 5, "Expected all five font license texts")
    for path in licenses:
        files["licenses/" + path.name] = path
    files["licenses/SOURCES.md"] = fonts / "SOURCES.md"
    for name, path in files.items():
        require(path.is_file() and path.stat().st_size > 0, f"Missing release payload: {name}")
    require(files[DLL_NAME].read_bytes()[:2] == b"MZ", "Release DLL is not a PE assembly")
    return files


def validate_archive(archive, expected, manifest, dll_hash, thunderstore):
    with zipfile.ZipFile(archive) as contents:
        names = contents.namelist()
        require(len(names) == len(set(names)) and set(names) == expected, "Unexpected, duplicate or missing package entries")
        require(contents.testzip() is None, "ZIP CRC check failed")
        require(all("\\" not in name and not name.startswith("/") and ".." not in name.split("/") for name in names), "Unsafe ZIP path")
        require([name for name in names if name.lower().endswith(".dll")] == [DLL_NAME], "Only Ironbound.dll may be distributed")
        require(hashlib.sha256(contents.read(DLL_NAME)).hexdigest() == dll_hash, "Release DLL hash mismatch")
        for name in names:
            if name.endswith((".md", ".txt", ".json")):
                contents.read(name).decode("utf-8-sig")
        if thunderstore:
            require(json.loads(contents.read("manifest.json")) == manifest, "Manifest changed in archive")
            validate_icon(contents.read("icon.png"))


def package(output):
    manifest = metadata()
    validate_icon((RELEASE / "icon.png").read_bytes())
    files = payload()
    output.mkdir(parents=True, exist_ok=True)
    version = manifest["version_number"]
    dll_hash = hashlib.sha256(files[DLL_NAME].read_bytes()).hexdigest()
    archives = []
    for target in ("Thunderstore", "Nexus"):
        selected = dict(files)
        if target == "Thunderstore":
            selected.update({"manifest.json": RELEASE / "manifest.json", "icon.png": RELEASE / "icon.png"})
        archive = output / f"Ironbound-{version}-{RELEASE_CHANNEL}-{target}.zip"
        # Explicit POSIX paths avoid Compress-Archive's backslash entries on Windows.
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as contents:
            for name, path in sorted(selected.items()):
                entry = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                entry.external_attr = 0o100644 << 16
                entry.create_system = 3
                contents.writestr(entry, path.read_bytes(), compresslevel=9)
        validate_archive(archive, set(selected), manifest, dll_hash, target == "Thunderstore")
        archives.append(archive)
        print(f"PASS: {archive.name}: metadata, file allowlist, licensing, ZIP CRC and DLL SHA-256")
    shutil.copyfile(archives[1], output / "Ironbound-local.zip")
    sums = "".join(hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.name + "\n" for path in archives)
    (output / "SHA256SUMS.txt").write_text(sums, encoding="utf-8")
    print(f"Created both upload packages and SHA256SUMS.txt in {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts")
    args = parser.parse_args()
    try:
        package(args.output)
    except (ValueError, OSError, KeyError, TypeError, ET.ParseError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Release packaging failed: {error}\n")
