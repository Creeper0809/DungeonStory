"""Verify immutable published ScriptableObject identities from committed Git trees.

The Unity editor owns creation of ProjectSettings/PublishedContentIdentities.json.
This gate only compares committed snapshots and checks every head row against the
committed Unity asset, meta GUID, script binding, top-level scalar fields, and
the catalog registrations emitted as schema-1 virtual fields.
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
from urllib.parse import quote, unquote_to_bytes

import yaml


ROOT = Path(__file__).resolve().parents[2]
BASELINE_PATH = "ProjectSettings/PublishedContentIdentities.json"
WORK_TYPE_SOURCE_PATH = "Assets/Scripts/Models/Work/WorkTypeId.cs"
ROOT_ASSET_PATH = "Assets/Resources/SO/GameContentCatalog.asset"
GUID_RE = re.compile(r"[0-9a-f]{32}")
IDENTIFIER_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
RESOURCE_PATH_FIELD = "@resource-path"
REGISTRATION_FIELD_RE = re.compile(
    r"@registration:(?P<member>[A-Za-z_][A-Za-z0-9_]*):"
    r"(?P<guid>[0-9a-f]{32})"
)
ROOT_SCALAR_REGISTRATIONS = frozenset(
    {"itemDefinitions", "worldPresentation", "characterSkillSettings", "media"}
)
ROOT_ARRAY_REGISTRATIONS = frozenset({"domainCatalogs"})
REGISTRY_ARRAY_MEMBERS = {
    "GameDomainContentCatalogSO": frozenset({"definitions"}),
    "ItemDefinitionCatalogSO": frozenset({"definitions"}),
}
NESTED_FIELD_PREFIX = "@nested:"
LEGACY_TRAIT_NAME_PREFIX = "@legacy-trait-name:"
TOP_LEVEL_SCALAR_RE = re.compile(
    r"^  (?P<name>[A-Za-z_][A-Za-z0-9_]*):(?P<value>.*)$"
)
SCRIPT_BINDING_RE = re.compile(
    r"^  m_Script:\s*\{[^}]*\bguid:\s*(?P<guid>[0-9a-f]{32})\b[^}]*\}\s*$",
    re.MULTILINE,
)
INLINE_ASSET_REFERENCE_RE = re.compile(
    r"^\{fileID: (?P<file_id>-?[0-9]+), guid: "
    r"(?P<guid>[0-9a-f]{32}), type: 2\}$"
)
ARRAY_ASSET_REFERENCE_RE = re.compile(
    r"^  - (?P<reference>\{fileID: -?[0-9]+, guid: "
    r"[0-9a-f]{32}, type: 2\})$"
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
class NestedRoute:
    path: str
    selector: str
    leaves: tuple[str, ...]
    children: tuple["NestedRoute", ...] = ()
    managed_reference: bool = False
    managed_types: tuple[str, ...] = ()


NESTED_ROUTES: dict[str, tuple[NestedRoute, ...]] = {
    "CharacterTraitSO": (
        NestedRoute("identityRules", "ruleId", ("ruleId",), managed_reference=True),
    ),
    "AnatomyProfileSO": (NestedRoute("nodes", "nodeId", ("nodeId",)),),
    "OffenseDecisionCardSO": (NestedRoute("choices", "choiceId", ("choiceId",)),),
    "FactionChapterDefinitionSO": (
        NestedRoute("choices", "choiceId", ("choiceId",)),
    ),
    "LifeEventDefinitionSO": (NestedRoute("choices", "choiceId", ("choiceId",)),),
    "ServiceIncidentDefinitionSO": (
        NestedRoute("responses", "choiceId", ("choiceId",)),
    ),
    "OffenseCampaignCatalogSO": (NestedRoute("targets", "id", ("id",)),),
    "ProductionRecipeSO": (
        NestedRoute("outputs", "outputLineId", ("outputLineId",)),
    ),
    "CharacterSkillSystemSettingsSO": (
        NestedRoute(
            "modules",
            "id",
            ("id", "capabilityId"),
            (NestedRoute("variants", "id", ("id",)),),
            managed_reference=True,
        ),
        NestedRoute(
            "drawbackCapabilities",
            "drawbackId",
            ("drawbackId", "capabilityId"),
        ),
    ),
    "CharacterAcquiredTraitSettingsSO": (
        NestedRoute(
            "drawbackCapabilities",
            "drawbackId",
            ("drawbackId", "capabilityId"),
            (NestedRoute("effects", "bindingId", ("bindingId",)),),
        ),
    ),
    "CharacterAcquiredTraitModuleSO": (
        NestedRoute("effects", "bindingId", ("bindingId",)),
        NestedRoute("specialReactions", "reactionId", ("reactionId",)),
    ),
    "BuildingSO": (
        NestedRoute(
            "abilityModules.items",
            "abilityId",
            ("abilityId",),
            managed_reference=True,
            managed_types=("BuildingProductionAbility", "BuildingSecurityAbility"),
        ),
    ),
    "GameDomainContentCatalogSO": (
        NestedRoute("metaUpgrades", "id", ("id",)),
        NestedRoute("runVariables", "id", ("id",)),
        NestedRoute("ownerDoctrines", "id", ("id",)),
        NestedRoute("invasionPatterns", "id", ("id",)),
    ),
    "EquipmentEvolutionFormulaCatalogSO": (
        NestedRoute("capabilities", "capabilityId", ("capabilityId",)),
    ),
    "FacilityEvolutionRecipeSO": (
        NestedRoute("formulaCapabilities", "capabilityId", ("capabilityId",)),
    ),
}


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


def _registration_member(type_name: str, path: str, location: str) -> str | None:
    if path == RESOURCE_PATH_FIELD:
        if type_name != "GameContentCatalogSO":
            raise VerificationError(
                f"resource-path virtual field is invalid for {type_name} at {location}"
            )
        return None
    match = REGISTRATION_FIELD_RE.fullmatch(path)
    if match is None:
        raise VerificationError(f"invalid virtual identity path at {location}: {path}")
    member = match.group("member")
    if type_name == "GameContentCatalogSO":
        allowed = ROOT_SCALAR_REGISTRATIONS | ROOT_ARRAY_REGISTRATIONS
    else:
        allowed = REGISTRY_ARRAY_MEMBERS.get(type_name, frozenset())
    if member not in allowed:
        raise VerificationError(
            f"registration member {member!r} is invalid for {type_name} at {location}"
        )
    return member


def _decode_selector(value: str, location: str) -> str:
    if not value:
        raise VerificationError(f"nested identity selector is empty at {location}")
    if re.fullmatch(r"(?:[A-Za-z0-9._~-]|%[0-9A-F]{2})+", value) is None:
        raise VerificationError(
            f"nested identity selector is not canonically escaped at {location}: {value}"
        )
    try:
        decoded = unquote_to_bytes(value).decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        raise VerificationError(
            f"nested identity selector is not canonical UTF-8 at {location}: {value}"
        ) from error
    if not decoded or decoded != decoded.strip() or quote(decoded, safe="-._~") != value:
        raise VerificationError(
            f"nested identity selector is not canonically escaped at {location}: {value}"
        )
    return decoded


def _parse_nested_route_path(
    route: NestedRoute,
    text: str,
    location: str,
) -> tuple[NestedRoute, str] | None:
    prefix = f"{route.path}[{route.selector}="
    if not text.startswith(prefix):
        return None
    close = text.find("]", len(prefix))
    if close < 0:
        raise VerificationError(f"unterminated nested identity selector at {location}")
    selector_and_type = text[len(prefix):close]
    if ";type=" in selector_and_type:
        encoded, managed_type = selector_and_type.split(";type=", 1)
        if not route.managed_types or managed_type not in route.managed_types:
            raise VerificationError(
                f"nested identity managed type is invalid at {location}: {managed_type}"
            )
    else:
        encoded = selector_and_type
        managed_type = ""
        if route.managed_types:
            raise VerificationError(f"nested identity managed type is missing at {location}")
    if ";" in encoded:
        raise VerificationError(f"nested identity selector is malformed at {location}")
    _decode_selector(encoded, location)
    remainder = text[close + 1:]
    if not remainder.startswith("."):
        raise VerificationError(f"nested identity leaf is missing at {location}")
    tail = remainder[1:]
    if tail in route.leaves:
        return route, tail
    for child in route.children:
        parsed = _parse_nested_route_path(child, tail, location)
        if parsed is not None:
            return parsed
    raise VerificationError(f"nested identity leaf is invalid at {location}: {tail}")


def _parse_nested_identity_path(type_name: str, path: str, location: str) -> None:
    if not path.startswith(NESTED_FIELD_PREFIX):
        raise VerificationError(f"invalid nested identity path at {location}: {path}")
    text = path[len(NESTED_FIELD_PREFIX):]
    for route in NESTED_ROUTES.get(type_name, ()):
        if _parse_nested_route_path(route, text, location) is not None:
            return
    raise VerificationError(
        f"nested identity route is invalid for {type_name} at {location}: {path}"
    )


def _validate_virtual_fields(
    type_name: str,
    fields: list[IdentityField],
    location: str,
) -> None:
    virtual = [field for field in fields if field.path.startswith("@")]
    for field in virtual:
        if field.path.startswith(NESTED_FIELD_PREFIX):
            _parse_nested_identity_path(type_name, field.path, location)
            if not field.value or field.value != field.value.strip():
                raise VerificationError(
                    f"nested identity value is blank or not trimmed at {location}: {field.path}"
                )
            continue
        if field.path.startswith(LEGACY_TRAIT_NAME_PREFIX):
            if type_name != "CharacterTraitSO":
                raise VerificationError(
                    f"legacy trait name is invalid for {type_name} at {location}"
                )
            alias = _decode_selector(
                field.path[len(LEGACY_TRAIT_NAME_PREFIX):], location
            )
            if field.value != alias:
                raise VerificationError(
                    f"legacy trait name value must equal its path alias at {location}"
                )
            continue
        member = _registration_member(type_name, field.path, location)
        if member is None:
            if field.value != ROOT_ASSET_PATH:
                raise VerificationError(
                    f"startup catalog path must be {ROOT_ASSET_PATH} at {location}"
                )
            continue
        match = REGISTRATION_FIELD_RE.fullmatch(field.path)
        assert match is not None
        if field.value != match.group("guid"):
            raise VerificationError(
                f"registration value must equal its path GUID at {location}: {field.path}"
            )
    if type_name == "GameContentCatalogSO" and virtual:
        resource_paths = [field for field in virtual if field.path == RESOURCE_PATH_FIELD]
        if len(resource_paths) != 1:
            raise VerificationError(
                f"GameContentCatalogSO requires one {RESOURCE_PATH_FIELD} at {location}"
            )
        for member in ROOT_SCALAR_REGISTRATIONS:
            count = sum(
                1 for field in virtual
                if field.path.startswith(f"@registration:{member}:")
            )
            if count != 1:
                raise VerificationError(
                    f"GameContentCatalogSO requires one {member} registration at {location}"
                )


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
            if not isinstance(path, str) or (
                IDENTIFIER_RE.fullmatch(path) is None and not path.startswith("@")
            ):
                raise VerificationError(
                    f"identity path must be a top-level field or known virtual field at {field_location}"
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
        _validate_virtual_fields(type_name, fields, location)
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


def _scripted_document(
    raw: bytes,
    path: str,
    expected_script_guid: str,
) -> tuple[str, str]:
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
    return matching[0]


class _StrictUnityLoader(yaml.SafeLoader):
    """Safe Unity-YAML subset: no aliases/anchors and no duplicate mapping keys."""

    def compose_node(self, parent, index):  # type: ignore[no-untyped-def]
        event = self.peek_event()
        if isinstance(event, yaml.AliasEvent) or getattr(event, "anchor", None) is not None:
            raise VerificationError("Unity identity YAML aliases and anchors are unsupported")
        return super().compose_node(parent, index)

    def construct_mapping(self, node, deep=False):  # type: ignore[no-untyped-def]
        if not isinstance(node, yaml.MappingNode):
            raise VerificationError("Unity identity YAML mapping node is malformed")
        result: dict[object, object] = {}
        for key_node, value_node in node.value:
            key = self.construct_object(key_node, deep=deep)
            try:
                duplicate = key in result
            except TypeError as error:
                raise VerificationError(
                    "Unity identity YAML mapping key is not a scalar"
                ) from error
            if duplicate:
                raise VerificationError(f"duplicate Unity YAML mapping key: {key}")
            result[key] = self.construct_object(value_node, deep=deep)
        return result


# Unity string fields may look like YAML booleans, numbers, nulls, or timestamps.
# Keep every scalar as authored text; managed-reference rids are validated below.
_StrictUnityLoader.yaml_implicit_resolvers = {}


def _load_scripted_yaml(document: str, path: str) -> dict[str, object]:
    marker, separator, body = document.partition("\n")
    if not separator or re.fullmatch(r"--- !u!\d+ &[+-]?\d+", marker) is None:
        raise VerificationError(f"Unity scripted document marker is malformed: {path}")
    try:
        loaded = yaml.load(body, Loader=_StrictUnityLoader)
    except VerificationError as error:
        raise VerificationError(f"invalid Unity identity YAML at {path}: {error}") from error
    except yaml.YAMLError as error:
        raise VerificationError(f"invalid Unity identity YAML at {path}: {error}") from error
    if not isinstance(loaded, dict) or set(loaded) != {"MonoBehaviour"}:
        raise VerificationError(f"Unity scripted document root is malformed: {path}")
    root = loaded["MonoBehaviour"]
    if not isinstance(root, dict):
        raise VerificationError(f"Unity MonoBehaviour payload is not a mapping: {path}")
    return root


def _mapping_value(mapping: dict[object, object], key: str, location: str) -> object:
    if key not in mapping:
        raise VerificationError(f"nested Unity field is missing at {location}: {key}")
    return mapping[key]


def _sequence_at(
    root: dict[object, object],
    path: str,
    location: str,
) -> list[object]:
    value: object = root
    for part in path.split("."):
        if not isinstance(value, dict):
            raise VerificationError(f"nested Unity parent is not a mapping at {location}.{part}")
        value = _mapping_value(value, part, location)
    if value is None:
        return []
    if not isinstance(value, list):
        raise VerificationError(f"nested Unity field is not an array at {location}.{path}")
    return value


def _identity_string(mapping: dict[object, object], key: str, location: str) -> str:
    value = _mapping_value(mapping, key, location)
    if not isinstance(value, str):
        raise VerificationError(f"nested identity is not a scalar string at {location}.{key}")
    if not value or value != value.strip():
        raise VerificationError(f"nested identity is blank or not trimmed at {location}.{key}")
    return value


def _managed_reference_table(
    root: dict[object, object],
    location: str,
) -> dict[str, list[tuple[str, dict[object, object]]]]:
    references = _mapping_value(root, "references", location)
    if not isinstance(references, dict):
        raise VerificationError(f"managed references are not a mapping at {location}")
    rows = _mapping_value(references, "RefIds", f"{location}.references")
    if rows is None:
        rows = []
    if not isinstance(rows, list):
        raise VerificationError(f"managed reference table is not an array at {location}")
    result: dict[str, list[tuple[str, dict[object, object]]]] = {}
    for index, row in enumerate(rows):
        row_location = f"{location}.references.RefIds[{index}]"
        if not isinstance(row, dict):
            raise VerificationError(f"managed reference row is not a mapping at {row_location}")
        rid = _mapping_value(row, "rid", row_location)
        if not isinstance(rid, str) or re.fullmatch(r"-?[0-9]+", rid) is None:
            raise VerificationError(f"managed reference rid is invalid at {row_location}")
        type_record = _mapping_value(row, "type", row_location)
        if not isinstance(type_record, dict):
            raise VerificationError(f"managed reference type is invalid at {row_location}")
        class_name = _mapping_value(type_record, "class", f"{row_location}.type")
        if not isinstance(class_name, str) or IDENTIFIER_RE.fullmatch(class_name) is None:
            raise VerificationError(f"managed reference class is invalid at {row_location}")
        data = _mapping_value(row, "data", row_location)
        if not isinstance(data, dict):
            raise VerificationError(f"managed reference data is invalid at {row_location}")
        result.setdefault(rid, []).append((class_name, data))
    return result


def _managed_element(
    item: object,
    references: dict[str, list[tuple[str, dict[object, object]]]],
    location: str,
) -> tuple[str, dict[object, object]]:
    if not isinstance(item, dict):
        raise VerificationError(f"managed reference item is not a mapping at {location}")
    rid = _mapping_value(item, "rid", location)
    if not isinstance(rid, str) or re.fullmatch(r"-?[0-9]+", rid) is None:
        raise VerificationError(f"managed reference item rid is invalid at {location}")
    matches = references.get(rid, [])
    if len(matches) != 1:
        reason = "missing" if not matches else "ambiguous"
        raise VerificationError(f"managed reference rid is {reason} at {location}: {rid}")
    return matches[0]


def _capture_nested_route(
    items: list[object],
    route: NestedRoute,
    location: str,
    result: dict[str, str],
    *,
    scope_prefix: str = "",
    references: dict[str, list[tuple[str, dict[object, object]]]] | None = None,
) -> None:
    seen: set[str] = set()
    for index, item in enumerate(items):
        item_location = f"{location}[{index}]"
        managed_type = ""
        if route.managed_reference:
            if references is None:
                raise VerificationError(f"managed reference table is missing at {location}")
            managed_type, element = _managed_element(item, references, item_location)
            if route.managed_types and managed_type not in route.managed_types:
                continue
        else:
            if not isinstance(item, dict):
                raise VerificationError(f"nested array element is not a mapping at {item_location}")
            element = item
        selector = _identity_string(element, route.selector, item_location)
        if selector in seen:
            raise VerificationError(
                f"duplicate nested identity selector at {location}: {route.selector}={selector}"
            )
        seen.add(selector)
        encoded = quote(selector, safe="-._~")
        scope = f"{route.path}[{route.selector}={encoded}"
        if route.managed_types:
            scope += f";type={managed_type}"
        scope += "]"
        if scope_prefix:
            scope = f"{scope_prefix}.{scope}"
        for leaf in route.leaves:
            value = _identity_string(element, leaf, item_location)
            virtual_path = f"{NESTED_FIELD_PREFIX}{scope}.{leaf}"
            if virtual_path in result:
                raise VerificationError(f"duplicate nested identity path at {location}: {virtual_path}")
            result[virtual_path] = value
        for child in route.children:
            child_items = _sequence_at(element, child.path, item_location)
            _capture_nested_route(
                child_items,
                child,
                f"{item_location}.{child.path}",
                result,
                scope_prefix=scope,
                references=references,
            )


def _actual_nested_fields(
    document: str,
    record: PublishedAsset,
    asset_path: str,
) -> dict[str, str]:
    routes = NESTED_ROUTES.get(record.type_name, ())
    if not routes:
        return {}
    root = _load_scripted_yaml(document, asset_path)
    references = None
    result: dict[str, str] = {}
    for route in routes:
        # Legacy traits omitted their empty, default-initialized rule list.
        # Published rule keys still cannot disappear: expected_virtual is compared below.
        if record.type_name == "CharacterTraitSO" and route.path == "identityRules" and "identityRules" not in root:
            continue
        items = _sequence_at(root, route.path, asset_path)
        if items and route.managed_reference and references is None:
            references = _managed_reference_table(root, asset_path)
        _capture_nested_route(
            items,
            route,
            f"{asset_path}.{route.path}",
            result,
            references=references,
        )
    return result


def _actual_legacy_trait_fields(
    document: str,
    record: PublishedAsset,
    asset_path: str,
) -> dict[str, str]:
    if record.type_name != "CharacterTraitSO":
        return {}
    root = _load_scripted_yaml(document, asset_path)
    aliases = root.get("legacyPolicyNames", [])
    if aliases is None:
        aliases = []
    if not isinstance(aliases, list):
        raise VerificationError(
            f"legacyPolicyNames is not an array at {asset_path}"
        )
    result: dict[str, str] = {}
    for index, alias in enumerate(aliases):
        location = f"{asset_path}.legacyPolicyNames[{index}]"
        if not isinstance(alias, str) or not alias or alias != alias.strip():
            raise VerificationError(f"legacy trait name is blank or not trimmed at {location}")
        path = f"{LEGACY_TRAIT_NAME_PREFIX}{quote(alias, safe='-._~')}"
        if path in result:
            raise VerificationError(f"duplicate legacy trait name at {location}: {alias}")
        result[path] = alias
    return result


def _top_level_field_blocks(
    document: str,
    path: str,
) -> dict[str, tuple[str, tuple[str, ...]]]:
    values: dict[str, str] = {}
    continuations: dict[str, list[str]] = {}
    current: str | None = None
    for line in document.splitlines():
        match = TOP_LEVEL_SCALAR_RE.match(line)
        if match is not None:
            name = match.group("name")
            suffix = match.group("value")
            value = suffix[1:] if suffix.startswith(" ") else suffix
            if name in values:
                raise VerificationError(f"duplicate top-level Unity field at {path}: {name}")
            values[name] = value
            continuations[name] = []
            current = name
            continue
        if current is not None:
            continuations[current].append(line)
    return {
        name: (value, tuple(continuations[name]))
        for name, value in values.items()
    }


def _top_level_scalars(
    raw: bytes,
    path: str,
    expected_script_guid: str,
) -> tuple[dict[str, str], str, dict[str, tuple[str, tuple[str, ...]]], str]:
    document, script_guid = _scripted_document(raw, path, expected_script_guid)
    blocks = _top_level_field_blocks(document, path)
    return {name: block[0] for name, block in blocks.items()}, script_guid, blocks, document


def _inline_asset_reference_guid(reference: str, location: str) -> str:
    match = INLINE_ASSET_REFERENCE_RE.fullmatch(reference)
    if match is None or int(match.group("file_id")) == 0:
        raise VerificationError(f"registered definition is not an asset reference at {location}")
    return match.group("guid")


def _registration_guids(
    block: tuple[str, tuple[str, ...]],
    location: str,
    *,
    is_array: bool,
) -> tuple[str, ...]:
    value, continuation = block
    if not is_array:
        if continuation:
            raise VerificationError(f"scalar registration has nested YAML at {location}")
        return (_inline_asset_reference_guid(value, location),)
    if value != "":
        raise VerificationError(f"registration array must use a YAML field block at {location}")
    result: list[str] = []
    for index, line in enumerate(continuation):
        match = ARRAY_ASSET_REFERENCE_RE.fullmatch(line)
        if match is None:
            raise VerificationError(
                f"registration array has invalid element {index} at {location}"
            )
        result.append(
            _inline_asset_reference_guid(
                match.group("reference"), f"{location}[{index}]"
            )
        )
    if len(set(result)) != len(result):
        raise VerificationError(f"registration array contains duplicate GUIDs at {location}")
    return tuple(result)


def _registered_target_script_path(
    snapshot: GitSnapshot,
    target_path: str,
    script_meta_by_guid: dict[str, list[str]],
) -> str:
    raw = snapshot.read_blob(target_path)
    if raw is None:
        raise VerificationError(
            f"registered target asset is missing beside its meta: {target_path}"
        )
    try:
        text = raw.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError as error:
        raise VerificationError(
            f"registered target asset is not UTF-8: {target_path}"
        ) from error
    script_guids = SCRIPT_BINDING_RE.findall(text)
    if len(script_guids) != 1:
        reason = "missing" if not script_guids else "ambiguous"
        raise VerificationError(
            f"registered target m_Script binding is {reason} at {target_path}"
        )
    script_guid = script_guids[0]
    script_meta_paths = script_meta_by_guid.get(script_guid, [])
    if len(script_meta_paths) != 1:
        reason = "does not resolve" if not script_meta_paths else "resolves more than once"
        raise VerificationError(
            f"registered target m_Script GUID {reason} at {snapshot.commit}: "
            f"{script_guid} ({target_path})"
        )
    script_path = script_meta_paths[0][:-5]
    if not snapshot.has_path(script_path):
        raise VerificationError(
            f"registered target script is missing beside its meta: {script_path}"
        )
    return script_path


def _is_eligible_game_asset_path(asset_path: str, script_path: str) -> bool:
    normalized_asset_path = asset_path.replace("\\", "/")
    normalized_script_path = script_path.replace("\\", "/")
    excluded = normalized_script_path.casefold()
    return (
        normalized_asset_path.startswith("Assets/")
        and normalized_script_path.startswith("Assets/Scripts/")
        and all(
            segment not in excluded
            for segment in ("/editor/", "/vendor/", "/thirdparty/", "/plugins/")
        )
    )


def _actual_catalog_fields(
    snapshot: GitSnapshot,
    record: PublishedAsset,
    asset_path: str,
    blocks: dict[str, tuple[str, tuple[str, ...]]],
    asset_meta_by_guid: dict[str, list[str]],
    script_meta_by_guid: dict[str, list[str]],
    published_guids: set[str],
) -> dict[str, str]:
    if record.type_name == "GameContentCatalogSO":
        if asset_path != ROOT_ASSET_PATH:
            raise VerificationError(
                f"startup content catalog must remain at {ROOT_ASSET_PATH}: {asset_path}"
            )
        members = {
            member: False for member in ROOT_SCALAR_REGISTRATIONS
        } | {
            member: True for member in ROOT_ARRAY_REGISTRATIONS
        }
        result = {RESOURCE_PATH_FIELD: asset_path}
    elif record.type_name in REGISTRY_ARRAY_MEMBERS:
        members = {member: True for member in REGISTRY_ARRAY_MEMBERS[record.type_name]}
        result = {}
    else:
        return {}
    for member, is_array in members.items():
        block = blocks.get(member)
        if block is None:
            raise VerificationError(
                f"registered definition field is missing from {asset_path}: {member}"
            )
        for guid in _registration_guids(
            block, f"{asset_path}.{member}", is_array=is_array
        ):
            meta_paths = asset_meta_by_guid.get(guid, [])
            if len(meta_paths) != 1:
                reason = "does not resolve" if not meta_paths else "resolves more than once"
                raise VerificationError(
                    f"registered target GUID {reason} at {snapshot.commit}: {guid}"
                )
            target_path = meta_paths[0][:-5]
            if not snapshot.has_path(target_path):
                raise VerificationError(
                    f"registered target asset is missing beside its meta: {target_path}"
                )
            script_path = _registered_target_script_path(
                snapshot, target_path, script_meta_by_guid
            )
            if (
                _is_eligible_game_asset_path(target_path, script_path)
                and guid not in published_guids
            ):
                raise VerificationError(
                    "registered eligible game ScriptableObject is missing from identity "
                    f"baseline: {guid} ({target_path}, script {script_path})"
                )
            virtual_path = f"@registration:{member}:{guid}"
            result[virtual_path] = guid
    return result


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
    registered_script_meta_by_guid = {
        guid: list(paths) for guid, paths in script_meta_by_guid.items()
    }
    for guid, paths in _guid_paths(snapshot, ".dll.meta").items():
        registered_script_meta_by_guid.setdefault(guid, []).extend(paths)
    published_guids = set(baseline.assets_by_guid())
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
        scalar_tokens, script_guid, field_blocks, scripted_document = _top_level_scalars(
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
        expected_virtual = {
            field.path: field.value
            for field in record.fields
            if field.path.startswith("@")
        }
        actual_virtual = _actual_catalog_fields(
            snapshot,
            record,
            asset_path,
            field_blocks,
            asset_meta_by_guid,
            registered_script_meta_by_guid,
            published_guids,
        )
        actual_virtual.update(
            _actual_nested_fields(scripted_document, record, asset_path)
        )
        actual_virtual.update(
            _actual_legacy_trait_fields(scripted_document, record, asset_path)
        )
        if expected_virtual != actual_virtual:
            missing = sorted(set(expected_virtual) - set(actual_virtual))
            unexpected = sorted(set(actual_virtual) - set(expected_virtual))
            changed = sorted(
                path for path in set(expected_virtual) & set(actual_virtual)
                if expected_virtual[path] != actual_virtual[path]
            )
            mismatch_paths = missing + unexpected + changed
            subject = (
                "nested identities"
                if any(path.startswith((NESTED_FIELD_PREFIX, LEGACY_TRAIT_NAME_PREFIX))
                       for path in mismatch_paths)
                else "catalog registrations"
            )
            raise VerificationError(
                f"published {subject} do not match {asset_path}: "
                f"missing={missing}, unexpected={unexpected}, changed={changed}"
            )
        fields_checked += len(expected_virtual)
        for field in record.fields:
            if field.path.startswith("@"):
                continue
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


def work_type_protocol_ids(raw: bytes) -> frozenset[str]:
    text = raw.decode("utf-8-sig")
    definitions = dict(re.findall(
        r"public\s+static\s+readonly\s+WorkTypeId\s+(\w+)\s*=\s*new\s+WorkTypeId\(\s*\"([^\"]+)\"\s*\)\s*;", text
    ))
    membership = re.search(r"IReadOnlyList<WorkTypeId>\s+All\s*=\s*new\[\]\s*\{([^}]+)\}", text, re.S)
    if not definitions or membership is None:
        raise VerificationError("WorkType protocol declarations or membership cannot be read")
    names = [value.strip() for value in membership[1].split(",") if value.strip()]
    if len(names) != len(set(names)) or set(names) != set(definitions):
        raise VerificationError("WorkType protocol membership disagrees with its declarations")
    values = list(definitions.values())
    if len(values) != len(set(values)) or any(re.fullmatch(r"work:[a-z]+(?:-[a-z]+)*", value) is None for value in values):
        raise VerificationError("WorkType protocol IDs are duplicate or noncanonical")
    return frozenset(values)


def verify_work_type_protocols(base_snapshot: GitSnapshot, head_snapshot: GitSnapshot) -> None:
    before = base_snapshot.read_blob(WORK_TYPE_SOURCE_PATH)
    after = head_snapshot.read_blob(WORK_TYPE_SOURCE_PATH)
    if before is None and after is None:
        return  # Test or pre-protocol repositories contain no such contract.
    if after is None:
        raise VerificationError("Published WorkType protocol source was removed")
    current = work_type_protocol_ids(after)
    if before is not None:
        removed = work_type_protocol_ids(before) - current
        if removed:
            raise VerificationError(f"Published WorkType save IDs were removed or renamed: {sorted(removed)}")


def verify_revisions(
    base_revision: str,
    head_revision: str = "HEAD",
    root: Path = ROOT,
) -> dict[str, object]:
    base_snapshot = GitSnapshot(root, base_revision)
    head_snapshot = GitSnapshot(root, head_revision)
    verify_work_type_protocols(base_snapshot, head_snapshot)
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
