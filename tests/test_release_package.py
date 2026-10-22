"""Release validator regression checks; run with python -B tests/test_release_package.py."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("release_package", ROOT / "scripts/package-release.py")
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class ReleaseChecks(unittest.TestCase):
    def test_current_metadata_and_icon(self):
        package.metadata()
        package.validate_icon((ROOT / "release/icon.png").read_bytes())

    def reject_manifest(self, key, value):
        manifest = package.metadata()
        manifest[key] = value
        with tempfile.TemporaryDirectory(prefix="Ironbound-package-check-") as temporary:
            directory = Path(temporary)
            (directory / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            with patch.object(package, "RELEASE", directory), self.assertRaises(ValueError):
                package.metadata()

    def test_invalid_name(self):
        self.reject_manifest("name", "Ironbound has spaces")

    def test_overlong_description(self):
        self.reject_manifest("description", "x" * 251)

    def test_semver(self):
        self.reject_manifest("version_number", "0.5.5-beta")

    def test_project_version_mismatch(self):
        self.reject_manifest("version_number", "9.9.9")

    def test_missing_dependency(self):
        self.reject_manifest("dependencies", [])

    def test_invalid_icon(self):
        for data in (b"", b"not an image", (ROOT / "release/icon.png").read_bytes()[:20]):
            with self.assertRaises(ValueError):
                package.validate_icon(data)

    def test_wrong_icon_size(self):
        data = bytearray((ROOT / "release/icon.png").read_bytes())
        data[16:20] = (128).to_bytes(4, "big")
        with self.assertRaises(ValueError):
            package.validate_icon(data)

    def test_icon_corruption(self):
        data = bytearray((ROOT / "release/icon.png").read_bytes())
        data[-1] ^= 1
        with self.assertRaises(ValueError):
            package.validate_icon(data)

    def test_archive_unexpected_dll(self):
        with tempfile.TemporaryDirectory(prefix="Ironbound-package-check-") as temporary:
            archive = Path(temporary) / "bad.zip"
            with zipfile.ZipFile(archive, "w") as contents:
                contents.writestr(package.DLL_NAME, b"MZtest")
                contents.writestr("assembly_valheim.dll", b"MZgame")
            with self.assertRaises(ValueError):
                package.validate_archive(archive, {package.DLL_NAME}, package.metadata(), "wrong", False)

    def test_archive_hash_mismatch(self):
        with tempfile.TemporaryDirectory(prefix="Ironbound-package-check-") as temporary:
            archive = Path(temporary) / "wrong.zip"
            with zipfile.ZipFile(archive, "w") as contents:
                contents.writestr(package.DLL_NAME, b"MZwrong build")
            with self.assertRaises(ValueError):
                package.validate_archive(archive, {package.DLL_NAME}, package.metadata(), "wrong", False)

    def test_archive_path_traversal(self):
        with tempfile.TemporaryDirectory(prefix="Ironbound-package-check-") as temporary:
            archive = Path(temporary) / "unsafe.zip"
            with zipfile.ZipFile(archive, "w") as contents:
                contents.writestr("../Ironbound.dll", b"MZ")
            with self.assertRaises(ValueError):
                package.validate_archive(archive, {"../Ironbound.dll"}, package.metadata(), "wrong", False)


if __name__ == "__main__":
    unittest.main()
