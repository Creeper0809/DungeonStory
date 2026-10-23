"""Verify immutable published ScriptableObject identities from committed Git trees.

The Unity editor owns creation of ProjectSettings/PublishedContentIdentities.json.
This gate only compares committed snapshots and checks every head row against the
committed Unity asset, meta GUID, script binding, and top-level scalar fields.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import json
from pathlib import Path
import re
import subprocess
import sys
from typing import Iterable


ROOT = Path(__file__).resolve().parents[2]
BASELINE_PATH = "ProjectSettings/PublishedContentIdentities.json"
GUID_RE = re.compile(r"[0-9a-f]{32}")
IDENTIFIER_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
TOP_LEVEL_SCALAR_RE = re.compile(
    r"^  (?P<name>[A-Za-z_][A-Za-z0-9_]*):(?P<value>.*)$"
)
SCRIPT_BINDING_RE = re.compile(
    r"^  m_Script:\s*\{[^}]*\bguid:\s*(?P<guid>[0-9a-f]{32})\b[^}]*\}\s*$",
    re.MULTILINE,
)


class VerificationError(RuntimeError):
    """A committed identity contract is malformed or has regressed."""


@dataclass(frozen=True)
class IdentityField:
    path: str
    value: str


@dataclass(frozen=True)
class PublishedAsset:
    guid: str
    type_name: str
    script_guid: str
    fields: tuple[IdentityField, ...]

    def fields_by_path(self) -> dict[str, IdentityField]:
        return {field.path: field for field in self.fields}


@dataclass(frozen=True)
class IdentityBaseline:
    assets: tuple[PublishedAsset, ...]

    def assets_by_guid(self) -> dict[str, PublishedAsset]:
        return {asset.guid: asset for asset in self.assets}


@dataclass(frozen=True)
class GitEntry:
    mode: str
    object_type: str
    object_id: str


class GitSnapshot:
    """A read-only, cached view of one committed tree."""

    def __init__(self, root: Path, revision: str):
        self.root = root.resolve()
        self.commit = _resolve_commit(self.root, revision)
        self.entries = self._read_tree()
        self._blobs: dict[str, bytes] = {}

    def _read_tree(self) -> dict[str, GitEntry]:
        command = [
            "git", "ls-tree", "-r", "-z", "--full-tree", self.commit, "--",
            "Assets", BASELINE_PATH,
        ]
        raw = _run_git(self.root, command)
        result: dict[str, GitEntry] = {}
        for record in raw.split(b"\0"):
            if not record:
                continue
            try:
                metadata, encoded_path = record.split(b"\t", 1)
                mode, object_type, object_id = metadata.decode("ascii").split(" ")
                path = encoded_path.decode("utf-8", errors="strict")
            except (UnicodeDecodeError, ValueError) as error:
                raise VerificationError(
                    f"cannot parse Git tree entry at {self.commit}: {record!r}"
                ) from error
            if path in result:
                raise VerificationError(f"duplicate Git tree path at {self.commit}: {path}")
            result[path] = GitEntry(mode, object_type, object_id)
        return result

    def has_path(self, path: str) -> bool:
        return path in self.entries

    def read_blob(self, path: str) -> bytes | None:
        if path not in self.entries:
            return None
        self.read_blobs((path,))
        return self._blobs[path]

    def read_blobs(self, paths: Iterable[str]) -> dict[str, bytes]:
        requested = tuple(dict.fromkeys(paths))
        missing = [path for path in requested if path not in self.entries]
        if missing:
            raise VerificationError(
                f"paths do not exist at {self.commit}: {', '.join(sorted(missing))}"
            )
        unloaded = [path for path in requested if path not in self._blobs]
        if unloaded:
            objects: dict[str, list[str]] = {}
            for path in unloaded:
                entry = self.entries[path]
                if entry.object_type != "blob" or entry.mode == "120000":
                    raise VerificationError(
                        f"identity input is not a regular blob at {self.commit}: {path}"
                    )
                objects.setdefault(entry.object_id, []).append(path)
            object_ids = tuple(objects)
            payload = "".join(object_id + "\n" for object_id in object_ids).encode("ascii")
            raw = _run_git(self.root, ["git", "cat-file", "--batch"], input_bytes=payload)
            offset = 0
            loaded_by_id: dict[str, bytes] = {}
            for expected_id in object_ids:
                line_end = raw.find(b"\n", offset)
                if line_end < 0:
                    raise VerificationError("truncated git cat-file header")
                header = raw[offset:line_end].decode("ascii", errors="strict").split(" ")
                offset = line_end + 1
                if len(header) != 3 or header[0] != expected_id or header[1] != "blob":
                    raise VerificationError(
                        f"unexpected git cat-file response for {expected_id}: {' '.join(header)}"
                    )
                try:
                    size = int(header[2])
                except ValueError as error:
                    raise VerificationError(
                        f"invalid git blob size for {expected_id}: {header[2]}"
                    ) from error
                end = offset + size
                if end >= len(raw) or raw[end:end + 1] != b"\n":
                    raise VerificationError(f"truncated git blob for {expected_id}")
                loaded_by_id[expected_id] = raw[offset:end]
                offset = end + 1
            if offset != len(raw):
                raise VerificationError("unexpected trailing bytes from git cat-file")
            for object_id, object_paths in objects.items():
                for path in object_paths:
                    self._blobs[path] = loaded_by_id[object_id]
        return {path: self._blobs[path] for path in requested}


def _run_git(root: Path, command: list[str], input_bytes: bytes | None = None) -> bytes:
    result = subprocess.run(
        command,
        cwd=root,
        input=input_bytes,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise VerificationError(
            f"Git command failed ({' '.join(command)}): {detail or result.returncode}"
        )
    return result.stdout


def _resolve_commit(root: Path, revision: str) -> str:
    if not revision or "\0" in revision:
        raise VerificationError("Git revision is empty or contains NUL")
    raw = _run_git(
        root,
        ["git", "rev-parse", "--verify", "--end-of-options", f"{revision}^{{commit}}"],
    )
    commit = raw.decode("ascii", errors="strict").strip()
    if not re.fullmatch(r"[0-9a-f]{40,64}", commit):
        raise VerificationError(f"Git revision did not resolve to a commit: {revision}")
    return commit


def _strict_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise VerificationError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_baseline(raw: bytes, label: str) -> IdentityBaseline:
    try:
        text = raw.decode("utf-8-sig", errors="strict")
        document = json.loads(text, object_pairs_hook=_strict_object)
    except VerificationError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise VerificationError(f"invalid identity baseline JSON at {label}: {error}") from error
    if not isinstance(document, dict) or set(document) != {"schemaVersion", "assets"}:
        raise VerificationError(
            f"identity baseline at {label} must contain only schemaVersion and assets"
        )
    if type(document["schemaVersion"]) is not int or document["schemaVersion"] != 1:
        raise VerificationError(f"unsupported identity baseline schema at {label}")
    rows = document["assets"]
    if not isinstance(rows, list):
        raise VerificationError(f"identity baseline assets must be an array at {label}")
    assets: list[PublishedAsset] = []
    seen_guids: set[str] = set()
    for index, row in enumerate(rows):
        location = f"{label}.assets[{index}]"
        if not isinstance(row, dict) or set(row) != {
            "guid", "typeName", "scriptGuid", "fields"
        }:
            raise VerificationError(
                "asset row must contain only guid, typeName, scriptGuid, and fields "
                f"at {location}"
            )
        guid = row["guid"]
        type_name = row["typeName"]
        script_guid = row["scriptGuid"]
        field_rows = row["fields"]
        if not isinstance(guid, str) or GUID_RE.fullmatch(guid) is None:
            raise VerificationError(f"asset GUID is not canonical at {location}")
        if guid in seen_guids:
            raise VerificationError(f"duplicate published asset GUID at {location}: {guid}")
        seen_guids.add(guid)
        if (
            not isinstance(type_name, str)
            or not type_name
            or type_name != type_name.strip()
            or any(IDENTIFIER_RE.fullmatch(part) is None for part in type_name.split("."))
        ):
            raise VerificationError(f"typeName is not canonical at {location}")
        if not isinstance(script_guid, str) or GUID_RE.fullmatch(script_guid) is None:
            raise VerificationError(f"script GUID is not canonical at {location}")
        if not isinstance(field_rows, list):
            raise VerificationError(f"published asset fields must be an array at {location}")
        fields: list[IdentityField] = []
        seen_paths: set[str] = set()
        for field_index, field in enumerate(field_rows):
            field_location = f"{location}.fields[{field_index}]"
            if not isinstance(field, dict) or set(field) != {"path", "value"}:
                raise VerificationError(
                    f"identity field must contain only path and value at {field_location}"
                )
            path = field["path"]
            value = field["value"]
            if not isinstance(path, str) or IDENTIFIER_RE.fullmatch(path) is None:
                raise VerificationError(
                    f"identity path must be one top-level serialized field at {field_location}"
                )
            if path in seen_paths:
                raise VerificationError(
                    f"duplicate identity field path at {field_location}: {path}"
                )
            seen_paths.add(path)
            if not isinstance(value, str):
                raise VerificationError(
                    f"identity value must be a string at {field_location}"
                )
            if isinstance(value, str) and ("\r" in value or "\n" in value):
                raise VerificationError(
                    f"identity string is not a top-level scalar at {field_location}"
                )
            fields.append(IdentityField(path, value))
        assets.append(PublishedAsset(guid, type_name, script_guid, tuple(fields)))
    return IdentityBaseline(tuple(assets))


def compare_baselines(base: IdentityBaseline, head: IdentityBaseline) -> None:
    head_assets = head.assets_by_guid()
    for old_asset in base.assets:
        current = head_assets.get(old_asset.guid)
        if current is None:
            raise VerificationError(
                f"published asset removed or GUID changed: {old_asset.guid} ({old_asset.type_name})"
            )
        if current.type_name != old_asset.type_name:
            raise VerificationError(
                f"published asset type changed for {old_asset.guid}: "
                f"{old_asset.type_name!r} -> {current.type_name!r}"
            )
        if current.script_guid != old_asset.script_guid:
            raise VerificationError(
                f"published asset script GUID changed for {old_asset.guid}: "
                f"{old_asset.script_guid} -> {current.script_guid}"
            )
        current_fields = current.fields_by_path()
        for old_field in old_asset.fields:
            new_field = current_fields.get(old_field.path)
            if new_field is None:
                raise VerificationError(
                    f"published identity field removed: {old_asset.guid}.{old_field.path}"
                )
            if new_field.value != old_field.value:
                raise VerificationError(
                    f"published identity changed: {old_asset.guid}.{old_field.path} "
                    f"{old_field.value!r} -> {new_field.value!r}"
                )


def _parse_guid_meta(raw: bytes, path: str) -> str:
    try:
        text = raw.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError as error:
        raise VerificationError(f"meta file is not UTF-8: {path}") from error
    values = [line[6:] for line in text.splitlines() if line.startswith("guid: ")]
    if len(values) != 1 or GUID_RE.fullmatch(values[0]) is None:
        raise VerificationError(f"meta file has no single canonical GUID: {path}")
    return values[0]


def _decode_double_quoted(raw: str, location: str) -> str:
    result: list[str] = []
    index = 1
    while index < len(raw):
        character = raw[index]
        if character == '"':
            if index != len(raw) - 1:
                raise VerificationError(f"trailing bytes after quoted scalar at {location}")
            return "".join(result)
        if character != "\\":
            result.append(character)
            index += 1
            continue
        index += 1
        if index >= len(raw):
            raise VerificationError(f"unterminated scalar escape at {location}")
        escape = raw[index]
        simple = {
            "0": "\0", "a": "\a", "b": "\b", "t": "\t", "n": "\n",
            "v": "\v", "f": "\f", "r": "\r", "e": "\x1b", " ": " ",
            '"': '"', "/": "/", "\\": "\\", "N": "\u0085", "_": "\u00a0",
            "L": "\u2028", "P": "\u2029",
        }
        if escape in simple:
            result.append(simple[escape])
            index += 1
            continue
        widths = {"x": 2, "u": 4, "U": 8}
        width = widths.get(escape)
        if width is None:
            raise VerificationError(f"unsupported scalar escape \\{escape} at {location}")
        digits = raw[index + 1:index + 1 + width]
        if len(digits) != width or re.fullmatch(r"[0-9A-Fa-f]+", digits) is None:
            raise VerificationError(f"invalid Unicode scalar escape at {location}")
        codepoint = int(digits, 16)
        index += 1 + width
        if 0xD800 <= codepoint <= 0xDBFF:
            if not raw.startswith("\\u", index):
                raise VerificationError(f"unpaired high surrogate at {location}")
            low_digits = raw[index + 2:index + 6]
            if len(low_digits) != 4 or re.fullmatch(r"[0-9A-Fa-f]{4}", low_digits) is None:
                raise VerificationError(f"invalid low surrogate at {location}")
            low = int(low_digits, 16)
            if not 0xDC00 <= low <= 0xDFFF:
                raise VerificationError(f"invalid low surrogate at {location}")
            codepoint = 0x10000 + ((codepoint - 0xD800) << 10) + (low - 0xDC00)
            index += 6
        elif 0xDC00 <= codepoint <= 0xDFFF or codepoint > 0x10FFFF:
            raise VerificationError(f"invalid Unicode codepoint at {location}")
        result.append(chr(codepoint))
    raise VerificationError(f"unterminated quoted scalar at {location}")


def _decode_single_quoted(raw: str, location: str) -> str:
    if len(raw) < 2 or not raw.endswith("'"):
        raise VerificationError(f"unterminated quoted scalar at {location}")
    inner = raw[1:-1]
    result: list[str] = []
    index = 0
    while index < len(inner):
        if inner[index] != "'":
            result.append(inner[index])
            index += 1
        elif index + 1 < len(inner) and inner[index + 1] == "'":
            result.append("'")
            index += 2
        else:
            raise VerificationError(f"unescaped single quote at {location}")
    return "".join(result)


def decode_identity_scalar(raw: str, location: str) -> str:
    if raw == "" or raw in {"~", "null", "Null", "NULL"}:
        return ""
    if raw.startswith('"'):
        return _decode_double_quoted(raw, location)
    if raw.startswith("'"):
        return _decode_single_quoted(raw, location)
    if raw.startswith(("[", "{", "|", ">", "&", "*", "!")):
        raise VerificationError(f"identity value is not a scalar at {location}")
    if re.search(r"\s+#", raw):
        raise VerificationError(f"identity scalar has an unsupported YAML comment at {location}")
    return raw


CSHARP_NON_CODE_RE = re.compile(
    r'(?P<raw>"{3,}).*?(?P=raw)'
    r'|@"(?:""|[^"])*"'
    r'|"(?:\\.|[^"\\])*"'
    r"|'(?:\\.|[^'\\])*'"
    r"|//[^\r\n]*"
    r"|/\*.*?\*/",
    re.DOTALL,
)


def _script_declares_type(raw: bytes, path: str, type_name: str) -> None:
    """Check the simple declaration; immutable scriptGuid protects namespace identity."""
    try:
        text = raw.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError as error:
        raise VerificationError(f"C# source is not UTF-8: {path}") from error
    code = CSHARP_NON_CODE_RE.sub(" ", text)
    simple_name = type_name.rsplit(".", 1)[-1]
    declaration = re.compile(rf"\bclass\s+@?{re.escape(simple_name)}\b")
    if declaration.search(code) is None:
        raise VerificationError(
            f"published type {type_name!r} has no exact class declaration in {path}"
        )


def _top_level_scalars(
    raw: bytes,
    path: str,
    expected_script_guid: str,
) -> tuple[dict[str, str], str]:
    try:
        text = raw.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError as error:
        raise VerificationError(f"Unity asset is not UTF-8: {path}") from error
    documents = re.split(r"(?=^--- !u!\d+ &[+-]?\d+\s*$)", text, flags=re.MULTILINE)
    scripted_documents: list[tuple[str, str]] = []
    for document in documents:
        scripts = SCRIPT_BINDING_RE.findall(document)
        if len(scripts) > 1:
            raise VerificationError(f"Unity YAML document has multiple m_Script GUIDs: {path}")
        if scripts:
            scripted_documents.append((document, scripts[0]))
    matching = [entry for entry in scripted_documents if entry[1] == expected_script_guid]
    if len(matching) != 1:
        if not matching:
            raise VerificationError(
                f"published asset script binding does not match {path}: "
                f"baseline {expected_script_guid}, asset candidates "
                f"{[guid for _, guid in scripted_documents]}"
            )
        raise VerificationError(
            f"published asset has ambiguous scripted subassets for {expected_script_guid}: {path}"
        )
    document, script_guid = matching[0]
    fields: dict[str, str] = {}
    for line in document.splitlines():
        match = TOP_LEVEL_SCALAR_RE.match(line)
        if match is None:
            continue
        name = match.group("name")
        suffix = match.group("value")
        value = suffix[1:] if suffix.startswith(" ") else suffix
        if name in fields:
            raise VerificationError(f"duplicate top-level Unity field at {path}: {name}")
        fields[name] = value
    return fields, script_guid


def _guid_paths(snapshot: GitSnapshot, suffix: str) -> dict[str, list[str]]:
    paths = sorted(path for path in snapshot.entries if path.endswith(suffix))
    blobs = snapshot.read_blobs(paths)
    result: dict[str, list[str]] = {}
    for path in paths:
        guid = _parse_guid_meta(blobs[path], path)
        result.setdefault(guid, []).append(path)
    return result


def verify_head_assets(snapshot: GitSnapshot, baseline: IdentityBaseline) -> tuple[int, int]:
    asset_meta_by_guid = _guid_paths(snapshot, ".asset.meta")
    script_meta_by_guid = _guid_paths(snapshot, ".cs.meta")
    assets_checked = 0
    fields_checked = 0
    for record in baseline.assets:
        meta_paths = asset_meta_by_guid.get(record.guid, [])
        if len(meta_paths) != 1:
            reason = "does not resolve" if not meta_paths else "resolves more than once"
            raise VerificationError(
                f"published asset GUID {reason} at {snapshot.commit}: {record.guid}"
            )
        meta_path = meta_paths[0]
        asset_path = meta_path[:-5]
        raw = snapshot.read_blob(asset_path)
        if raw is None:
            raise VerificationError(
                f"published asset is missing beside its meta at {snapshot.commit}: {asset_path}"
            )
        scalar_tokens, script_guid = _top_level_scalars(
            raw, asset_path, record.script_guid
        )
        if script_guid != record.script_guid:
            raise VerificationError(
                f"published asset script binding does not match {asset_path}: "
                f"baseline {record.script_guid}, asset {script_guid}"
            )
        script_meta_paths = script_meta_by_guid.get(record.script_guid, [])
        if len(script_meta_paths) != 1:
            reason = "does not resolve" if not script_meta_paths else "resolves more than once"
            raise VerificationError(
                f"m_Script GUID {reason} for {asset_path}: {script_guid}"
            )
        script_path = script_meta_paths[0][:-5]
        script_raw = snapshot.read_blob(script_path)
        if script_raw is None:
            raise VerificationError(
                f"C# source is missing beside its meta for {asset_path}: {script_path}"
            )
        _script_declares_type(script_raw, script_path, record.type_name)
        for field in record.fields:
            if field.path not in scalar_tokens:
                if field.value == "":
                    # Unity omits newly added serialized string fields while their
                    # object value remains null/empty until the asset is re-saved.
                    fields_checked += 1
                    continue
                raise VerificationError(
                    f"published identity field is missing from {asset_path}: {field.path}"
                )
            actual = decode_identity_scalar(
                scalar_tokens[field.path], f"{asset_path}.{field.path}"
            )
            if actual != field.value:
                raise VerificationError(
                    f"published identity does not match {asset_path}.{field.path}: "
                    f"baseline {field.value!r}, asset {actual!r}"
                )
            fields_checked += 1
        assets_checked += 1
    return assets_checked, fields_checked


def verify_revisions(
    base_revision: str,
    head_revision: str = "HEAD",
    root: Path = ROOT,
) -> dict[str, object]:
    base_snapshot = GitSnapshot(root, base_revision)
    head_snapshot = GitSnapshot(root, head_revision)
    head_raw = head_snapshot.read_blob(BASELINE_PATH)
    if head_raw is None:
        raise VerificationError(
            f"head identity baseline is missing at {head_snapshot.commit}: {BASELINE_PATH}"
        )
    head = load_baseline(head_raw, f"{head_snapshot.commit}:{BASELINE_PATH}")
    if not head.assets:
        raise VerificationError("head identity baseline cannot reset to an empty asset list")
    base_raw = base_snapshot.read_blob(BASELINE_PATH)
    base_present = base_raw is not None
    if base_raw is not None:
        base = load_baseline(base_raw, f"{base_snapshot.commit}:{BASELINE_PATH}")
        compare_baselines(base, head)
    assets_checked, fields_checked = verify_head_assets(head_snapshot, head)
    return {
        "base": base_snapshot.commit,
        "head": head_snapshot.commit,
        "baseBaselinePresent": base_present,
        "assetsChecked": assets_checked,
        "fieldsChecked": fields_checked,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True, help="Exact base commit or ref")
    parser.add_argument("--head", default="HEAD", help="Exact head commit or ref")
    parser.add_argument("--repo-root", type=Path, default=ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    try:
        summary = verify_revisions(args.base, args.head, args.repo_root)
    except VerificationError as error:
        print(f"PUBLISHED_CONTENT_IDENTITIES=FAIL; {error}", file=sys.stderr)
        return 1
    print("PUBLISHED_CONTENT_IDENTITIES=PASS; " + json.dumps(summary, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
