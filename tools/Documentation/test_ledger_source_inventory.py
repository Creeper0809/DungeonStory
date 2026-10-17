"""Focused offline tests for the frozen ledger source inventory utility."""

from __future__ import annotations

import copy
import json
import os
import stat
import subprocess
import tempfile
import unicodedata
import unittest
from pathlib import Path
from unittest.mock import patch

import build_ledger_source_inventory as inventory


class LedgerSourceInventoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name) / "repo"
        for relative in (
            "Assets/Scripts",
            "Assets/Data",
            "Packages",
            "ProjectSettings",
            "Tools/Documentation",
            "Artifacts/QA/snapshots",
        ):
            (self.root / relative).mkdir(parents=True, exist_ok=True)
        self.write("Assets/Scripts/A.cs", b"public sealed class A {}\n")
        self.write("Assets/Scripts/Game.asmdef", b'{"name":"Game"}\n')
        self.write("Assets/Data/item.asset", b"asset-v1\n")
        self.write("Packages/manifest.json", b'{"dependencies":{}}\n')
        self.write("ProjectSettings/ProjectVersion.txt", b"m_EditorVersion: test\n")
        self.write("Tools/Documentation/contract.py", b"SCHEMA = 1\n")
        self.git("init")
        self.git("config", "user.email", "inventory@example.invalid")
        self.git("config", "user.name", "Inventory Test")
        self.git("add", ".")
        self.git("commit", "-m", "fixture")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write(self, relative: str, raw: bytes) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        return path

    def git(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["git", "-C", os.fspath(self.root), *arguments],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )

    @property
    def contract(self) -> Path:
        return Path("Tools/Documentation/contract.py")

    def create(self, name: str = "inventory.json") -> Path:
        output = Path("Artifacts/QA/snapshots") / name
        inventory.create_snapshot(self.root, output, [self.contract])
        return self.root / output

    def test_deterministic_complete_inventory_and_partition_roots(self) -> None:
        first = self.create("first.json")
        second = self.create("second.json")
        self.assertEqual(first.read_bytes(), second.read_bytes())
        snapshot, _, counts = inventory.load_snapshot(first)
        paths = [value["path"] for value in snapshot["files"]]
        self.assertEqual(paths, sorted(paths))
        self.assertEqual(
            paths,
            [
                "Assets/Data/item.asset",
                "Assets/Scripts/A.cs",
                "Assets/Scripts/Game.asmdef",
                "Packages/manifest.json",
                "ProjectSettings/ProjectVersion.txt",
                "Tools/Documentation/contract.py",
            ],
        )
        self.assertEqual(counts, {"fileCount": 6, "sourceFileCount": 2, "contentFileCount": 4})
        self.assertNotIn("manifestSha256", snapshot)
        result = inventory.verify_current(self.root, first, [self.contract])
        self.assertEqual(result["inventoryStatus"], "CURRENT_MATCH")
        self.assertEqual(result["failureCount"], 0)
        self.assertEqual(result["claims"]["runtime"], "NOT_RUN")

    def test_source_byte_drift_changes_only_source_root_and_is_reported(self) -> None:
        snapshot_path = self.create()
        before = json.loads(snapshot_path.read_text(encoding="utf-8"))
        self.write("Assets/Scripts/A.cs", b"public sealed class A { int Changed; }\n")
        current = inventory.build_snapshot(self.root, [self.contract])
        self.assertNotEqual(before["dirtySourceSha256"], current["dirtySourceSha256"])
        self.assertEqual(before["contentSha256"], current["contentSha256"])
        result = inventory.verify_current(self.root, snapshot_path, [self.contract])
        self.assertIn("file-changed:Assets/Scripts/A.cs", result["failures"])
        self.assertIn("dirty-source-root-changed", result["failures"])

    def test_content_byte_drift_changes_only_content_root_and_is_reported(self) -> None:
        snapshot_path = self.create()
        before = json.loads(snapshot_path.read_text(encoding="utf-8"))
        self.write("Assets/Data/item.asset", b"asset-v2\n")
        current = inventory.build_snapshot(self.root, [self.contract])
        self.assertEqual(before["dirtySourceSha256"], current["dirtySourceSha256"])
        self.assertNotEqual(before["contentSha256"], current["contentSha256"])
        result = inventory.verify_current(self.root, snapshot_path, [self.contract])
        self.assertIn("file-changed:Assets/Data/item.asset", result["failures"])
        self.assertIn("content-root-changed", result["failures"])

    def test_missing_and_extra_files_are_reported(self) -> None:
        snapshot_path = self.create()
        (self.root / "Packages/manifest.json").unlink()
        self.write("ProjectSettings/NewSetting.asset", b"new\n")
        result = inventory.verify_current(self.root, snapshot_path, [self.contract])
        self.assertIn("file-missing:Packages/manifest.json", result["failures"])
        self.assertIn("file-added:ProjectSettings/NewSetting.asset", result["failures"])

    def test_contract_paths_and_duplicate_normalized_paths_are_rejected(self) -> None:
        outside = Path(self.temporary.name) / "outside.py"
        outside.write_text("unsafe = True\n", encoding="utf-8")
        for supplied in (outside, Path("../outside.py")):
            with self.subTest(supplied=supplied), self.assertRaises(inventory.InventoryError):
                inventory.enumerate_files(self.root, [supplied])
        with self.assertRaisesRegex(inventory.InventoryError, "duplicate normalized"):
            inventory.enumerate_files(self.root, [self.contract, self.contract])
        with self.assertRaisesRegex(inventory.InventoryError, "must be Python"):
            inventory.enumerate_files(self.root, [Path("Packages/manifest.json")])

    def test_symlink_or_reparse_escape_is_rejected(self) -> None:
        outside = Path(self.temporary.name) / "outside.bin"
        outside.write_bytes(b"outside")
        link = self.root / "Assets/Data/escape.bin"
        try:
            link.symlink_to(outside)
        except OSError as error:
            original = inventory._is_reparse
            with patch.object(
                inventory,
                "_is_reparse",
                side_effect=lambda value: stat.S_ISREG(value.st_mode) or original(value),
            ), self.assertRaisesRegex(inventory.InventoryError, "symlink or reparse"):
                inventory.enumerate_files(self.root, [self.contract])
            self.assertIsInstance(error, OSError)
            return
        with self.assertRaisesRegex(inventory.InventoryError, "symlink or reparse"):
            inventory.enumerate_files(self.root, [self.contract])

    def test_output_is_create_only_and_must_be_under_artifacts_qa(self) -> None:
        output = self.create()
        before = output.read_bytes()
        with self.assertRaisesRegex(inventory.InventoryError, "already exists"):
            inventory.create_snapshot(
                self.root,
                Path("Artifacts/QA/snapshots/inventory.json"),
                [self.contract],
            )
        self.assertEqual(output.read_bytes(), before)
        with self.assertRaises(inventory.InventoryError):
            inventory.create_snapshot(
                self.root,
                Path("ProjectSettings/unsafe.json"),
                [self.contract],
            )

    def test_snapshot_only_is_pure_and_root_tamper_is_rejected(self) -> None:
        snapshot_path = self.create()
        self.write("Assets/Data/item.asset", b"disk-drift-after-snapshot\n")
        result = inventory.verify_snapshot_integrity(snapshot_path)
        self.assertEqual(result["inventoryStatus"], "SNAPSHOT_INTEGRITY_VALID")
        self.assertFalse(result["currentSourceChecked"])
        self.assertEqual(result["trust"], "loaded-snapshot-integrity-only")

        tampered = copy.deepcopy(json.loads(snapshot_path.read_text(encoding="utf-8")))
        tampered["files"][0]["sha256"] = "sha256:" + "0" * 64
        tampered_path = self.root / "Artifacts/QA/snapshots/tampered.json"
        tampered_path.write_bytes(inventory.canonical_json(tampered) + b"\n")
        with self.assertRaisesRegex(inventory.InventoryError, "descriptor root mismatch"):
            inventory.verify_snapshot_integrity(tampered_path)

    def test_snapshot_unsafe_path_is_rejected_even_with_recomputed_roots(self) -> None:
        snapshot_path = self.create()
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
        snapshot["files"][0]["path"] = "../escape.asset"
        sources = [value for value in snapshot["files"] if inventory._is_source_descriptor(value)]
        content = [value for value in snapshot["files"] if not inventory._is_source_descriptor(value)]
        snapshot["dirtySourceSha256"] = inventory.descriptor_root(sources)
        snapshot["contentSha256"] = inventory.descriptor_root(content)
        unsafe_path = self.root / "Artifacts/QA/snapshots/unsafe.json"
        unsafe_path.write_bytes(inventory.canonical_json(snapshot) + b"\n")
        with self.assertRaisesRegex(inventory.InventoryError, "unsafe file path"):
            inventory.verify_snapshot_integrity(unsafe_path)

    def test_boolean_schema_version_is_rejected(self) -> None:
        snapshot_path = self.create()
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
        snapshot["schemaVersion"] = True
        with self.assertRaisesRegex(inventory.InventoryError, "schemaVersion differs"):
            inventory.validate_snapshot(snapshot)

    def test_colon_in_any_relative_path_segment_is_rejected(self) -> None:
        for value in ("Assets/Data/item:stream.asset", "Assets/dir:name/item.asset"):
            with self.subTest(value=value), self.assertRaisesRegex(
                inventory.InventoryError,
                "colon-qualified",
            ):
                inventory.normalize_relative_path(value)

    def test_non_nfc_relative_path_is_rejected(self) -> None:
        decomposed = "Assets/Data/" + unicodedata.normalize("NFD", "é") + ".asset"
        self.assertNotEqual(decomposed, unicodedata.normalize("NFC", decomposed))
        with self.assertRaisesRegex(inventory.InventoryError, "not NFC-normalized"):
            inventory.normalize_relative_path(decomposed)

    def test_git_head_drift_is_not_hidden_by_unchanged_inventory_bytes(self) -> None:
        snapshot_path = self.create()
        self.write("README.md", b"new commit outside inventory roots\n")
        self.git("add", "README.md")
        self.git("commit", "-m", "head drift")
        result = inventory.verify_current(self.root, snapshot_path, [self.contract])
        self.assertTrue(any(value.startswith("game-commit-changed:") for value in result["failures"]))


if __name__ == "__main__":
    unittest.main()
