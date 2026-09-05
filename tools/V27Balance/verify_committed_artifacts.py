#!/usr/bin/env python3
"""Portable CI verification for committed V27 artifacts.

Unity remains the authority that generates the artifacts. This verifier deliberately
does not regenerate gameplay data without a licensed Editor; it proves that the
reviewed artifact set is byte-exact, internally ordered, complete, and bound to the
same analyzer, approvals, and durable gameplay evidence recorded by the manifest.
"""

from __future__ import annotations

import csv
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import unicodedata

ROOT = Path(__file__).resolve().parents[2]
QA = ROOT / "Artifacts" / "QA"
MANIFEST_PATH = QA / "v27-balance-artifact-manifest.json"
SOURCE_INVENTORY_PATH = QA / "v27-balance-source-inventory.json"
MARKET_DECISION_PATH = (
    ROOT / "docs" / "game-design" / "v27-balance-market-review-decisions.json"
)
MARKET_SECOND_APPLY_RECEIPT_PATH = (
    QA / "v27-balance-market-second-apply-noop.json"
)
AUDIT_SECOND_GENERATION_RECEIPT_PATH = (
    QA / "v27-balance-audit-second-generation-noop.json"
)


def fail(message: str) -> None:
    raise RuntimeError(message)


def _reject_duplicate_json_keys(
    pairs: list[tuple[str, object]],
) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON key: {key!r}")
        result[key] = value
    return result


def load_json_strict(path: Path) -> object:
    if not path.is_file():
        fail(f"required JSON is missing: {path.relative_to(ROOT)}")
    if path.is_symlink():
        fail(f"required JSON must not be a symlink: {path.relative_to(ROOT)}")
    with path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        raw = stream.read()
        after = os.fstat(stream.fileno())
    if any(
        getattr(before, field) != getattr(after, field)
        for field in ("st_dev", "st_ino", "st_size", "st_mtime_ns")
    ) or len(raw) != after.st_size:
        fail(f"JSON changed while it was read: {path.relative_to(ROOT)}")
    if raw.startswith(b"\xef\xbb\xbf"):
        fail(f"JSON must be UTF-8 without BOM: {path.relative_to(ROOT)}")
    try:
        text = raw.decode("utf-8", errors="strict")
        return json.loads(text, object_pairs_hook=_reject_duplicate_json_keys)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"invalid strict JSON in {path.relative_to(ROOT)}: {error}")


def require_exact_object_keys(
    value: object,
    expected: tuple[str, ...],
    label: str,
) -> dict[str, object]:
    if not isinstance(value, dict):
        fail(f"{label} must be a JSON object")
    actual = tuple(value)
    if actual != expected:
        fail(f"{label} field order/set differs: expected={expected} actual={actual}")
    return value


def require_exact_array(value: object, label: str) -> list[object]:
    if not isinstance(value, list):
        fail(f"{label} must be a JSON array")
    return value


def require_int(value: object, label: str, *, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        fail(f"{label} must be an integer >= {minimum}: {value!r}")
    return value


def require_bool(value: object, label: str) -> bool:
    if not isinstance(value, bool):
        fail(f"{label} must be a JSON boolean: {value!r}")
    return value


def require_string(value: object, label: str, *, allow_empty: bool = False) -> str:
    if not isinstance(value, str) or (not allow_empty and not value):
        fail(f"{label} must be a {'possibly empty ' if allow_empty else ''}string")
    try:
        value.encode("utf-8", errors="strict")
    except UnicodeEncodeError as error:
        fail(f"{label} contains an unpaired surrogate: {error}")
    return value


def require_sha256_value(
    value: object,
    label: str,
    *,
    case: str = "lower",
) -> str:
    token = require_string(value, label)
    pattern = {
        "lower": r"[0-9a-f]{64}",
        "upper": r"[0-9A-F]{64}",
        "either": r"[0-9A-Fa-f]{64}",
    }.get(case)
    if pattern is None or re.fullmatch(pattern, token) is None:
        fail(f"{label} is not a canonical {case} SHA-256: {token!r}")
    return token


def _canonical_relative_parts(raw: str, label: str) -> tuple[str, ...]:
    if (
        not raw
        or raw != unicodedata.normalize("NFC", raw)
        or "\\" in raw
        or raw.startswith("/")
        or raw.endswith("/")
        or "//" in raw
        or re.match(r"^[A-Za-z]:", raw)
    ):
        fail(f"{label} is not a canonical repository-relative path: {raw!r}")
    parts = tuple(raw.split("/"))
    if any(part in ("", ".", "..") for part in parts):
        fail(f"{label} contains a forbidden path segment: {raw!r}")
    return parts


def resolve_repository_file(
    raw: object,
    label: str,
    *,
    required_prefix: str | None = None,
) -> Path:
    relative = require_string(raw, label)
    parts = _canonical_relative_parts(relative, label)
    if required_prefix is not None and not relative.startswith(required_prefix):
        fail(f"{label} must start with {required_prefix!r}: {relative!r}")
    candidate = ROOT.joinpath(*parts)
    current = ROOT
    for part in parts:
        current = current / part
        if current.is_symlink():
            fail(f"{label} traverses a symlink: {relative!r}")
    try:
        resolved = candidate.resolve(strict=True)
    except OSError as error:
        fail(f"{label} does not resolve to a file: {relative!r}: {error}")
    root = ROOT.resolve(strict=True)
    if root != resolved and root not in resolved.parents:
        fail(f"{label} escapes the repository root: {relative!r}")
    if not resolved.is_file():
        fail(f"{label} is not a file: {relative!r}")
    return resolved


def read_file_identity(path: Path, label: str) -> tuple[str, int]:
    """Hash and length one open handle; reject concurrent replacement/mutation.

    Runtime mtime equality belongs to the Unity receipt producer. Portable CI
    intentionally does not compare a checkout timestamp with that runtime value.
    The stat values here are used only to detect a read race in this process.
    """
    with path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        digest = hashlib.sha256()
        length = 0
        while True:
            block = stream.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
            length += len(block)
        after = os.fstat(stream.fileno())
    stable_fields = (
        "st_dev",
        "st_ino",
        "st_size",
        "st_mtime_ns",
    )
    if any(getattr(before, field) != getattr(after, field) for field in stable_fields):
        fail(f"{label} changed while portable verification was reading it")
    if length != after.st_size:
        fail(f"{label} byte length changed during portable verification")
    return digest.hexdigest(), length


def require_lf_json_file(path: Path, label: str) -> None:
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf") or b"\r" in raw or not raw.endswith(b"\n"):
        fail(f"{label} must be UTF-8 without BOM, LF-only, and newline-terminated")
    try:
        raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        fail(f"{label} is not strict UTF-8: {error}")


def utf16_ordinal_key(value: str) -> bytes:
    # StringComparer.Ordinal compares UTF-16 code units, not Unicode scalars.
    return value.encode("utf-16-be", errors="strict")


def market_digest(tokens: list[object]) -> str:
    """C# market authority framing: UTF-16 length, ':', token; uppercase SHA."""
    canonical = bytearray()
    for raw in tokens:
        if isinstance(raw, bool) or raw is None:
            fail(f"market digest received a non-canonical token: {raw!r}")
        if isinstance(raw, int):
            raw = str(raw)
        elif not isinstance(raw, str):
            fail(f"market digest received an unsupported token: {raw!r}")
        token = require_string(raw, "market digest token", allow_empty=True)
        utf16_length = len(token.encode("utf-16-le", errors="strict")) // 2
        canonical.extend(str(utf16_length).encode("ascii"))
        canonical.extend(b":")
        canonical.extend(token.encode("utf-8", errors="strict"))
    return hashlib.sha256(canonical).hexdigest().upper()


def semantic_digest(tokens: list[object]) -> str:
    """CanonicalSemanticDigestBuilder framing: UTF-8 length, ':', token, '|'."""
    canonical = bytearray()
    for raw in tokens:
        if isinstance(raw, bool):
            token = "1" if raw else "0"
        elif raw is None:
            token = ""
        else:
            token = str(raw)
        encoded = token.encode("utf-8", errors="strict")
        canonical.extend(str(len(encoded)).encode("ascii"))
        canonical.extend(b":")
        canonical.extend(encoded)
        canonical.extend(b"|")
    return hashlib.sha256(canonical).hexdigest()


def sha256(path: Path) -> str:
    if not path.is_file():
        fail(f"required artifact is missing: {path.relative_to(ROOT)}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_source_sha256(path: Path) -> str:
    if not path.is_file():
        fail(f"required source is missing: {path.relative_to(ROOT)}")
    raw = path.read_bytes()
    canonical = raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return hashlib.sha256(canonical).hexdigest()


def current_source_snapshot() -> tuple[str, int, str]:
    supported_extensions = {".cs", ".asmdef", ".asmref", ".rsp"}
    exact_files = {
        "Packages/manifest.json",
        "Packages/packages-lock.json",
    }
    captured: dict[str, Path] = {}
    for relative_root in ("Assets", "Packages"):
        source_root = ROOT / relative_root
        if not source_root.is_dir():
            fail(f"current-source evidence root is missing: {relative_root}")
        for path in source_root.rglob("*"):
            if path.is_symlink():
                fail(
                    "reparse/symlink is forbidden in current-source evidence: "
                    f"{path.relative_to(ROOT).as_posix()}"
                )
            if not path.is_file():
                continue
            relative_raw = path.relative_to(ROOT).as_posix()
            extension = path.suffix
            if extension.lower() in supported_extensions and extension not in supported_extensions:
                fail(f"non-canonical current-source extension: {relative_raw}")
            if extension not in supported_extensions and relative_raw not in exact_files:
                continue
            relative = unicodedata.normalize("NFC", relative_raw)
            if relative in captured:
                fail(f"duplicate canonical current-source path: {relative}")
            captured[relative] = path

    paths = sorted(captured, key=lambda value: value.encode("utf-8"))
    if not paths:
        fail("current-source evidence input set is empty")

    digest = hashlib.sha256()
    path_digest = hashlib.sha256()
    for relative in paths:
        raw = captured[relative].read_bytes()
        if raw.startswith(b"\xef\xbb\xbf"):
            raw = raw[3:]
        try:
            source = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as error:
            fail(f"current-source input is not strict UTF-8: {relative}: {error}")
        source = source.replace("\r\n", "\n").replace("\r", "\n")
        digest.update(f"{relative}\n{source}\n".encode("utf-8"))
        path_digest.update(f"{relative}\n".encode("utf-8"))
    return digest.hexdigest(), len(paths), path_digest.hexdigest()


def current_all_scripts_sha256() -> str:
    return current_source_snapshot()[0]


def require_hash(manifest: dict[str, object], key: str, relative: str) -> None:
    expected = str(manifest.get(key, "")).lower()
    actual = sha256(ROOT / relative)
    if not expected or actual != expected:
        fail(f"{key} mismatch for {relative}: expected={expected} actual={actual}")


def require_text(path: Path, markers: tuple[str, ...]) -> None:
    if not path.is_file():
        fail(f"required report is missing: {path.relative_to(ROOT)}")
    text = path.read_text(encoding="utf-8-sig")
    for marker in markers:
        if marker not in text:
            fail(f"missing marker {marker!r} in {path.relative_to(ROOT)}")


def require_count_marker(text: str, path: Path, pattern: str) -> int:
    match = re.search(pattern, text, flags=re.MULTILINE)
    if match is None:
        fail(f"missing count marker {pattern!r} in {path.relative_to(ROOT)}")
    return int(match.group(1))


def verify_labor_facility_report() -> None:
    path = QA / "v27-balance-labor-facility-authority.txt"
    require_text(path, ("RESULT=PASS; stage=applied; failures=0",))
    text = path.read_text(encoding="utf-8-sig")

    labor_counts = (
        require_count_marker(
            text,
            path,
            r"^PASS V27_RECURRING_WU_PROJECT_SCALE_REMOVED rows=(\d+); factor=1$",
        ),
        require_count_marker(text, path, r"^PASS V27_LABOR_BOM_UNCHANGED rows=(\d+)$"),
        require_count_marker(text, path, r"^PASS V27_LABOR_EXACT_APPROVAL_KEYS rows=(\d+)$"),
        require_count_marker(
            text,
            path,
            r"^PASS V27_LABOR_ASSET_APPLIED_EXACT applied=(\d+); total=\d+$",
        ),
    )
    labor_total = require_count_marker(
        text,
        path,
        r"^PASS V27_LABOR_ASSET_APPLIED_EXACT applied=\d+; total=(\d+)$",
    )
    if len(set((*labor_counts, labor_total))) != 1:
        fail(f"labor authority row counts disagree in {path.relative_to(ROOT)}: {labor_counts}; total={labor_total}")

    research_rows = require_count_marker(
        text,
        path,
        r"^PASS V27_RESEARCH_WU_EFFECTIVE_AUTHORITY_EXACT rows=(\d+); factor=45/99$",
    )
    research_applied = require_count_marker(
        text,
        path,
        r"^PASS V27_RESEARCH_WU_ASSET_APPLIED_EXACT applied=(\d+); total=\d+$",
    )
    research_total = require_count_marker(
        text,
        path,
        r"^PASS V27_RESEARCH_WU_ASSET_APPLIED_EXACT applied=\d+; total=(\d+)$",
    )
    if research_rows != research_applied or research_applied != research_total:
        fail(
            f"research authority row counts disagree in {path.relative_to(ROOT)}: "
            f"rows={research_rows}; applied={research_applied}; total={research_total}"
        )


def verify_source_inventory(manifest: dict[str, object]) -> None:
    inventory = load_json_strict(SOURCE_INVENTORY_PATH)
    if not isinstance(inventory, dict):
        fail("V27 source inventory root must be an object")
    if inventory.get("schemaVersion") != "v27.source.v2":
        fail(f"unexpected source inventory schema: {inventory.get('schemaVersion')}")
    entries = inventory.get("entries")
    if not isinstance(entries, list) or not entries:
        fail("V27 source inventory has no entries")
    if len(entries) != int(manifest.get("sourceCount", -1)):
        fail(
            "V27 source inventory count mismatch: "
            f"manifest={manifest.get('sourceCount')} inventory={len(entries)}"
        )

    previous = ""
    pairs: list[tuple[str, str]] = []
    seen: set[str] = set()
    root = ROOT.resolve()
    for entry in entries:
        if not isinstance(entry, dict):
            fail("V27 source inventory contains a non-object entry")
        relative = str(entry.get("path", ""))
        expected = str(entry.get("sha256", "")).lower()
        if not relative or "\\" in relative or relative.startswith("/") or ".." in Path(relative).parts:
            fail(f"non-canonical source inventory path: {relative!r}")
        if relative in seen or (previous and relative <= previous):
            fail(f"duplicate or unsorted source inventory path: {relative}")
        path = (ROOT / relative).resolve()
        if root not in path.parents or not path.is_file():
            fail(f"source inventory path is missing or outside the repository: {relative}")
        actual = canonical_source_sha256(path)
        if actual != expected:
            fail(f"source digest mismatch for {relative}: expected={expected} actual={actual}")
        seen.add(relative)
        previous = relative
        pairs.append((relative, expected))

    canonical = "".join(f"{path}={digest}\n" for path, digest in pairs).encode("utf-8")
    aggregate = hashlib.sha256(canonical).hexdigest()
    if aggregate != str(manifest.get("sourceDigest", "")).lower():
        fail(
            "aggregate source digest mismatch: "
            f"expected={manifest.get('sourceDigest')} actual={aggregate}"
        )


def verify_csv(manifest: dict[str, object]) -> set[str]:
    path = QA / "v27-balance-before-after.csv"
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        fail("V27 CSV must be UTF-8 without BOM")
    if not raw.endswith(b"\r\n"):
        fail("V27 CSV must end with RFC 4180 CRLF")

    expected_header = ("domain", "definitionKind", "stableId", "metric")
    required_evidence = (
        "balanceBaselineRecordId",
        "sourceAuthority",
        "sourcePropertyPath",
        "executionRoute",
        "saveAuthority",
        "verificationEvidence",
        "dependencyFingerprint",
        "sourceDigest",
        "semanticHash",
    )
    previous: tuple[str, str, str, str] | None = None
    seen: set[tuple[str, str, str, str]] = set()
    baseline_ids: set[str] = set()
    row_count = 0
    with path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        if reader.fieldnames is None:
            fail("V27 CSV has no header")
        missing = [name for name in expected_header if name not in reader.fieldnames]
        missing.extend(name for name in required_evidence if name not in reader.fieldnames)
        if missing:
            fail(f"V27 CSV is missing key columns: {missing}")
        for row in reader:
            key = tuple(row[name] for name in expected_header)
            if key in seen:
                fail(f"duplicate V27 stable key: {key}")
            if previous is not None and key < previous:
                fail(f"V27 CSV sort order regressed: previous={previous} current={key}")
            seen.add(key)
            previous = key
            for name in required_evidence:
                if not row[name].strip():
                    fail(f"V27 CSV row {key} has empty required field {name}")
            # `pending` is also the authored-state label for selected, non-Critical
            # redistribution proposals and for unchanged rows awaiting a later
            # ApplyApproved pass.  It is not an anomaly severity.  Unresolved
            # root/local Criticals are enforced by manifest.criticalCount above;
            # collapsed dispositions remain presentation-only evidence.
            baseline_ids.add(row["balanceBaselineRecordId"])
            row_count += 1

    expected_rows = int(manifest.get("rowCount", -1))
    if row_count != expected_rows:
        fail(f"V27 CSV row count mismatch: expected={expected_rows} actual={row_count}")
    return baseline_ids


def verify_approvals(manifest: dict[str, object]) -> None:
    approval_path = ROOT / "docs/game-design/v27-balance-critical-approvals.json"
    payload = load_json_strict(approval_path)
    if not isinstance(payload, dict):
        fail("V27 approval authority root must be an object")
    if payload.get("schemaVersion") != "v27.2":
        fail(f"unexpected approval schema: {payload.get('schemaVersion')}")
    approvals = payload.get("approvals")
    if not isinstance(approvals, list):
        fail("V27 approval authority has no approvals array")
    if len(approvals) != int(manifest.get("approvedCount", -1)):
        fail(
            "V27 approval count mismatch: "
            f"manifest={manifest.get('approvedCount')} file={len(approvals)}"
        )
    required = (
        "approvalKey",
        "rootStableId",
        "metric",
        "exactBeforeValue",
        "exactAfterValue",
        "dependencyFingerprint",
        "sourceDigest",
        "reasonCode",
        "balanceBaselineRecordId",
    )
    keys: set[str] = set()
    for approval in approvals:
        if not isinstance(approval, dict):
            fail("V27 approval authority contains a non-object entry")
        missing = [name for name in required if not str(approval.get(name, "")).strip()]
        if missing:
            fail(f"V27 approval is missing exact fields {missing}: {approval}")
        key = str(approval["approvalKey"])
        if key in keys:
            fail(f"duplicate V27 approval key: {key}")
        keys.add(key)


def verify_combat() -> None:
    require_text(
        QA / "combat-balance-final.txt",
        ("RESULT=PASS", "encounters=36", "samplesPerEncounter=1000", "failures=0"),
    )
    for number in range(1, 37):
        require_text(
            QA / "combat-balance-final" / f"encounter-{number:02d}.txt",
            ("RESULT=PASS; samples=1000; failures=0; stalled=0",),
        )


def verify_daily_routine() -> None:
    for seed in (157181, 157182, 157183):
        require_text(
            QA / f"phase157-daily-routine-wu-seed-{seed}.txt",
            (
                "observedDays=5",
                f"runSeed={seed}",
                "runtimeDiagnosticsGate=ai-runtime-gate-v3",
                "RESULT=PASS; failures=0",
                "capturedIssues=0",
            ),
        )


def verify_physical_mass_coupling() -> None:
    report_path = QA / "v27-physical-mass-coupling.txt"
    csv_path = QA / "v27-physical-mass-coupling.csv"
    require_text(
        report_path,
        (
            "RESULT=PASS; phase=physical-mass-parent-coupling; assetMutations=0",
            "warehouseGramEligibility=PASS; warehouseCapacityUnitsFit=PASS; "
            "maxStackAndOrdinaryHaul=PASS",
            "facilityInputAuthority=PASS; facilityOutputAuthority=PASS; "
            "facilityOutputBufferCycleCapacity=PASS",
            "ewuAcquisitionRecoverable=PASS; marketPriceSaleRate=PASS; "
            "rootlessDelta=0; critical=0",
            "secondWriteNoOp=PASS; byteDiff=0; lengthDiff=0; mtimeDiff=0",
            "exitGate=CURRENT_SOURCE_COUPLING_AND_SECOND_WRITE_NOOP_PASS",
        ),
    )
    text = report_path.read_text(encoding="utf-8-sig")
    source_match = re.search(
        r"^currentSourceDigest=([0-9a-f]{64}); "
        r"currentSourceInputCount=(\d+); "
        r"currentSourcePathListDigest=([0-9a-f]{64})$",
        text,
        flags=re.MULTILINE,
    )
    if source_match is None:
        fail("physical-mass coupling source binding is missing")
    expected_source, expected_count, expected_paths = current_source_snapshot()
    actual_source, actual_count, actual_paths = source_match.groups()
    if (
        actual_source != expected_source
        or int(actual_count) != expected_count
        or actual_paths != expected_paths
    ):
        fail(
            "physical-mass coupling source binding is stale: "
            f"source={actual_source}/{expected_source}; "
            f"count={actual_count}/{expected_count}; paths={actual_paths}/{expected_paths}"
        )
    expected_scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    if (
        f"gameplaySceneSha256={expected_scene}; currentSourceParent=PASS"
        not in text
    ):
        fail("physical-mass coupling official-scene binding is stale")
    counts = re.search(
        r"^items=(\d+); recipes=(\d+); buildings=(\d+); rows=(\d+); "
        r"changedRows=(\d+); changedMassRoots=(\d+)$",
        text,
        flags=re.MULTILINE,
    )
    if counts is None:
        fail(
            "physical-mass coupling denominator differs: "
            f"actual={counts.groups() if counts else 'MISSING'}"
        )
    item_count, recipe_count, building_count, reported_rows, changed_rows, changed_roots = (
        map(int, counts.groups())
    )
    if (
        item_count <= 0
        or recipe_count <= 0
        or building_count <= 0
        or reported_rows <= 0
        or changed_rows != 0
        or changed_roots != 0
    ):
        fail(f"physical-mass coupling denominator differs: actual={counts.groups()}")

    raw = csv_path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf") or not raw.endswith(b"\r\n"):
        fail("physical-mass coupling CSV must be UTF-8 without BOM and end CRLF")
    expected_header = [
        "schemaVersion",
        "stableId",
        "impactDomain",
        "consumerKind",
        "consumerStableId",
        "metric",
        "before",
        "after",
        "deltaStatus",
        "rootCauseIds",
        "formula",
    ]
    keys: list[tuple[str, str, str, str, str]] = []
    with csv_path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        if reader.fieldnames != expected_header:
            fail(f"physical-mass coupling CSV header differs: {reader.fieldnames}")
        for row in reader:
            if row["schemaVersion"] != "v27.mass.coupling.1":
                fail("physical-mass coupling CSV schema differs")
            if row["before"] != row["after"] or row["deltaStatus"] != "unchanged":
                fail(
                    "physical-mass coupling changed row escaped zero-root audit: "
                    f"{row['stableId']}/{row['metric']}"
                )
            if row["rootCauseIds"]:
                fail("unchanged physical-mass coupling row has root causes")
            keys.append(
                (
                    row["stableId"],
                    row["impactDomain"],
                    row["consumerKind"],
                    row["consumerStableId"],
                    row["metric"],
                )
            )
    if (
        len(keys) != reported_rows
        or len(set(keys)) != len(keys)
        or keys != sorted(keys)
    ):
        fail(
            "physical-mass coupling CSV coverage/order differs: "
            f"rows={len(keys)}; unique={len(set(keys))}"
        )


def parse_exact_key_value_report(path: Path) -> dict[str, str]:
    if not path.is_file():
        fail(f"required report is missing: {path.relative_to(ROOT)}")
    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if not line:
            continue
        if "=" not in line:
            fail(f"non key/value line in {path.relative_to(ROOT)}: {line!r}")
        key, value = line.split("=", 1)
        if not key or key in values:
            fail(f"duplicate or empty key in {path.relative_to(ROOT)}: {key!r}")
        values[key] = value
    return values


def verify_output_clearance_profiles() -> None:
    generation_path = QA / "v27-production-output-clearance-profile-generation.txt"
    promotion_path = QA / "v27-production-output-clearance-profile-promotion.txt"
    current_path = QA / "v27-production-output-clearance-profile-current.txt"
    candidate_path = QA / "v27-production-output-clearance-profiles.candidate.json"
    resource_path = ROOT / "Assets/Resources/V27/production-output-clearance-profiles.json"
    expected_profiles = 92
    expected_blocking = 0
    max_int32 = (1 << 31) - 1
    max_int64 = (1 << 63) - 1
    def read_ordered_report(
        path: Path,
        scalar_keys: tuple[str, ...],
        pressure_count: int | None,
        review_input_count: int = 0,
    ) -> dict[str, str]:
        if not path.is_file():
            fail(f"required report is missing: {path.relative_to(ROOT)}")
        raw = path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or b"\r" in raw or not raw.endswith(b"\n"):
            fail(f"report must be strict UTF-8 without BOM and LF-only: {path.relative_to(ROOT)}")
        try:
            text = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as error:
            fail(f"report is not strict UTF-8: {path.relative_to(ROOT)}: {error}")
        lines = text[:-1].split("\n")
        if any(not line or "=" not in line for line in lines):
            fail(f"report contains an empty or malformed line: {path.relative_to(ROOT)}")
        keys = [line.split("=", 1)[0] for line in lines]
        if pressure_count is None:
            if len(lines) < len(scalar_keys):
                fail(f"report scalar denominator differs: {path.relative_to(ROOT)}")
            scalar_values = {
                key: line.split("=", 1)[1]
                for key, line in zip(keys[:len(scalar_keys)], lines[:len(scalar_keys)])
            }
            token = scalar_values.get("backpressureExpected", "")
            if re.fullmatch(r"0|[1-9][0-9]*", token) is None:
                fail(
                    "output-clearance backpressure count is non-canonical in "
                    f"{path.relative_to(ROOT)}"
                )
            pressure_count = int(token)
        expected_keys = list(scalar_keys) + [
            f"reviewInput[{index}]" for index in range(review_input_count)
        ] + [
            f"backpressure[{index}]" for index in range(pressure_count)
        ]
        if keys != expected_keys:
            fail(f"report key order/set differs: {path.relative_to(ROOT)}")
        return {key: line.split("=", 1)[1] for key, line in zip(keys, lines)}

    generation = read_ordered_report(
        generation_path,
        (
            "schema", "result", "currentSourceDigest", "gameplaySceneSha256",
            "naturalAcceptedDigest", "throughputAuthorityDigest",
            "capacityReviewDigest", "catalogAuthorityDigest", "profiles",
            "seedsPerProfile", "observations", "accepted",
            "backpressureExpected", "blockingCritical", "candidateSha256",
            "secondWriteByteDiff",
        ),
        None,
        expected_profiles,
    )
    promotion = read_ordered_report(
        promotion_path,
        (
            "schema", "result", "candidateSha256", "resourceSha256",
            "catalogAuthorityDigest", "profiles", "secondWriteByteDiff",
        ),
        0,
    )
    current = read_ordered_report(
        current_path,
        (
            "schema", "result", "currentSourceDigest", "gameplaySceneSha256",
            "currentPortfolioDigest", "catalogAuthorityDigest",
            "capacityReviewDigest", "verificationDigest", "profiles",
            "accepted", "backpressureExpected", "blockingCritical",
            "lookupMismatches",
        ),
        None,
    )

    expected_source = generation.get("currentSourceDigest", "")
    require_lower_sha256(
        expected_source,
        "output-clearance historical source digest",
    )
    expected_scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    disposition_counts: list[tuple[int, int, int, int]] = []
    for values, path, schema in (
        (generation, generation_path, "v27-production-output-clearance-profile-generation@3"),
        (current, current_path, "v27-production-output-clearance-profile-current@2"),
    ):
        if values.get("schema") != schema or values.get("result") != "PASS":
            fail(f"output-clearance profile report schema/result differs: {path.relative_to(ROOT)}")
        if values.get("currentSourceDigest") != expected_source:
            fail(
                "output-clearance profile reports belong to different source epochs: "
                f"{path.relative_to(ROOT)}"
            )
        if values.get("gameplaySceneSha256") != expected_scene:
            fail(f"output-clearance profile scene binding is stale: {path.relative_to(ROOT)}")
        count_keys = (
            "profiles", "accepted", "backpressureExpected", "blockingCritical",
        )
        if any(
            re.fullmatch(r"0|[1-9][0-9]*", values.get(key, "")) is None
            for key in count_keys
        ):
            fail(
                "output-clearance disposition count is non-canonical in "
                f"{path.relative_to(ROOT)}"
            )
        counts = tuple(int(values[key]) for key in count_keys)
        if counts[0] != expected_profiles or counts[3] != expected_blocking:
            fail(
                "output-clearance disposition denominator differs in "
                f"{path.relative_to(ROOT)}: {counts}"
            )
        if counts[0] != counts[1] + counts[2] + counts[3]:
            fail(
                "output-clearance disposition count invariant differs in "
                f"{path.relative_to(ROOT)}: {counts}"
            )
        disposition_counts.append(counts)
        for key in ("capacityReviewDigest", "catalogAuthorityDigest"):
            require_lower_sha256(values.get(key, ""), f"{path.name}.{key}")

    if disposition_counts[0] != disposition_counts[1]:
        fail("output-clearance disposition differs between generation and strict current")
    expected_backpressure = disposition_counts[0][2]

    if generation.get("seedsPerProfile") != "32" or generation.get("observations") != "2944":
        fail("output-clearance generation seed/observation denominator differs")
    if generation.get("secondWriteByteDiff") != "0":
        fail("output-clearance candidate/report second write was not byte-identical")
    if current.get("lookupMismatches") != "0":
        fail("output-clearance strict profile lookup mismatch remains")

    candidate_hash = sha256(candidate_path)
    resource_hash = sha256(resource_path)
    if generation.get("candidateSha256") != candidate_hash:
        fail("output-clearance generation candidate hash differs")
    if promotion.get("schema") != "v27-production-output-clearance-profile-promotion@1" \
            or promotion.get("result") != "PASS" \
            or re.fullmatch(r"[1-9][0-9]*", promotion.get("profiles", "")) is None \
            or int(promotion["profiles"]) != expected_profiles \
            or promotion.get("secondWriteByteDiff") != "0":
        fail("output-clearance promotion report is incomplete")
    if promotion.get("candidateSha256") != candidate_hash \
            or promotion.get("resourceSha256") != resource_hash \
            or candidate_hash != resource_hash:
        fail("output-clearance candidate/resource promotion hash differs")
    if promotion.get("catalogAuthorityDigest") != generation.get("catalogAuthorityDigest") \
            or current.get("catalogAuthorityDigest") != generation.get("catalogAuthorityDigest"):
        fail("output-clearance catalog authority differs across generation/promotion/current")
    if current.get("capacityReviewDigest") != generation.get("capacityReviewDigest"):
        fail("output-clearance capacity review differs between generation/current")
    for values, path, key in (
        (generation, generation_path, "naturalAcceptedDigest"),
        (generation, generation_path, "throughputAuthorityDigest"),
        (current, current_path, "currentPortfolioDigest"),
        (current, current_path, "verificationDigest"),
    ):
        require_lower_sha256(values.get(key, ""), f"{path.name}.{key}")
    natural_report = parse_exact_key_value_report(
        QA / "v27-production-output-clearance-natural-portfolio.txt"
    )
    if current.get("currentPortfolioDigest") != natural_report.get("currentPortfolioDigest"):
        fail("output-clearance strict current portfolio differs from natural report")

    candidate_raw = candidate_path.read_bytes()
    if candidate_raw.startswith(b"\xef\xbb\xbf") or candidate_raw != candidate_raw.strip():
        fail("output-clearance candidate JSON has BOM or surrounding whitespace")
    def reject_duplicate_json_keys(
        pairs: list[tuple[str, object]],
    ) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                fail(f"output-clearance candidate JSON contains duplicate key: {key!r}")
            result[key] = value
        return result

    try:
        candidate_text = candidate_raw.decode("utf-8", errors="strict")
        candidate_document = json.loads(
            candidate_text,
            object_pairs_hook=reject_duplicate_json_keys,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        fail(f"output-clearance candidate JSON is invalid: {error}")
    python_canonical = json.dumps(
        candidate_document,
        ensure_ascii=False,
        separators=(",", ":"),
    )
    if candidate_text != python_canonical:
        fail("output-clearance candidate JSON is not canonical compact UTF-8")
    if not isinstance(candidate_document, dict) or list(candidate_document) != [
        "schema", "profileCount", "catalogSourceDigest", "profiles"
    ]:
        fail("output-clearance candidate top-level field order/set differs")
    if candidate_document.get("schema") != "production-output-clearance-profile-resource@1" \
            or candidate_document.get("profileCount") != expected_profiles:
        fail("output-clearance candidate schema/profileCount differs")
    candidate_profiles = candidate_document.get("profiles")
    if not isinstance(candidate_profiles, list) or len(candidate_profiles) != expected_profiles:
        fail("output-clearance candidate profile array denominator differs")

    def semantic_digest(tokens: tuple[object, ...]) -> str:
        digest = hashlib.sha256()
        for value in tokens:
            token = str(value).encode("utf-8")
            digest.update(str(len(token)).encode("ascii"))
            digest.update(b":")
            digest.update(token)
            digest.update(b"|")
        return digest.hexdigest()

    row_keys = [
        "definitionId", "workstationTag", "p95HaulClearanceMilliHours",
        "peakOutputMassGramsPerHour", "sampleCount", "distinctSeedCount",
        "measurementSourceDigest", "throughputSourceDigest", "rowSourceDigest",
    ]
    candidate_by_identity: dict[tuple[str, str], dict[str, object]] = {}
    identities: list[tuple[str, str]] = []
    for row in candidate_profiles:
        if not isinstance(row, dict) or list(row) != row_keys:
            fail("output-clearance candidate row field order/set differs")
        definition = row["definitionId"]
        workstation = row["workstationTag"]
        if not isinstance(definition, str) or not isinstance(workstation, str) \
                or not definition or not workstation \
                or definition != definition.strip() or workstation != workstation.strip() \
                or any(character in definition + workstation for character in ";\r\n"):
            fail("output-clearance candidate identity is non-canonical")
        identity = (definition, workstation)
        if identity in candidate_by_identity:
            fail(f"duplicate output-clearance candidate identity: {identity}")
        integer_fields = (
            "p95HaulClearanceMilliHours", "peakOutputMassGramsPerHour",
            "sampleCount", "distinctSeedCount",
        )
        if any(
            isinstance(row[name], bool)
            or not isinstance(row[name], int)
            or row[name] <= 0
            for name in integer_fields
        ) or row["sampleCount"] != 32 or row["distinctSeedCount"] != 32:
            fail(f"output-clearance candidate numeric denominator differs: {identity}")
        if row["p95HaulClearanceMilliHours"] > max_int64 \
                or row["peakOutputMassGramsPerHour"] > max_int64 \
                or row["sampleCount"] > max_int32 \
                or row["distinctSeedCount"] > max_int32:
            fail(f"output-clearance candidate numeric range exceeds C# authority: {identity}")
        for name in ("measurementSourceDigest", "throughputSourceDigest", "rowSourceDigest"):
            if not isinstance(row[name], str):
                fail(f"output-clearance candidate digest is not a string: {identity}.{name}")
            require_lower_sha256(row[name], f"{identity}.{name}")
        calculated_row_digest = semantic_digest((
            "production-output-clearance-profile-record@1",
            definition,
            workstation,
            row["p95HaulClearanceMilliHours"],
            row["peakOutputMassGramsPerHour"],
            row["sampleCount"],
            row["distinctSeedCount"],
            row["measurementSourceDigest"],
            row["throughputSourceDigest"],
        ))
        if calculated_row_digest != row["rowSourceDigest"]:
            fail(f"output-clearance candidate row digest differs: {identity}")
        candidate_by_identity[identity] = row
        identities.append(identity)
    ordinal = lambda value: value.encode("utf-16-be", errors="surrogatepass")
    if identities != sorted(
        identities,
        key=lambda value: (ordinal(value[0]), ordinal(value[1])),
    ):
        fail("output-clearance candidate rows are not .NET Ordinal sorted")
    catalog_tokens: list[object] = [
        "production-output-clearance-profile-catalog@1",
        expected_profiles,
    ]
    for identity in identities:
        row = candidate_by_identity[identity]
        catalog_tokens.extend((identity[0], identity[1], row["rowSourceDigest"]))
    catalog_digest = semantic_digest(tuple(catalog_tokens))
    if candidate_document.get("catalogSourceDigest") != catalog_digest \
            or generation.get("catalogAuthorityDigest") != catalog_digest:
        fail("output-clearance candidate catalog digest differs")

    review_field_order = [
        "definition", "workstation", "authoredCycles", "lanePolicy",
        "manualLanes", "automaticLanes", "maxCycleGrams",
        "upstreamDigest",
    ]
    review_row_digests: list[str] = []
    derived_pressure_identities: set[tuple[str, str]] = set()
    derived_accepted = 0
    derived_backpressure = 0
    derived_critical = 0
    for index in range(expected_profiles):
        key = f"reviewInput[{index}]"
        value = generation.get(key)
        if value is None:
            fail(f"missing output-clearance review input: {key}")
        fields: dict[str, str] = {}
        for token in value.split(";"):
            if ":" not in token:
                fail(f"malformed output-clearance review input token: {key}/{token!r}")
            name, field_value = token.split(":", 1)
            if not name or name in fields or not field_value:
                fail(f"duplicate/empty output-clearance review input field: {key}/{token!r}")
            fields[name] = field_value
        if list(fields) != review_field_order:
            fail(f"output-clearance review input field order/set differs: {key}")
        identity = (fields["definition"], fields["workstation"])
        if identity != identities[index]:
            fail(f"output-clearance review input/candidate identity differs: {key}/{identity}")
        if any(
            not component
            or component != component.strip()
            or any(character in component for character in ";\r\n")
            for component in identity
        ):
            fail(f"output-clearance review input identity is non-canonical: {key}")
        positive_names = (
            "authoredCycles", "lanePolicy", "manualLanes", "maxCycleGrams",
        )
        if any(re.fullmatch(r"[1-9][0-9]*", fields[name]) is None
               for name in positive_names) \
                or re.fullmatch(r"0|[1-9][0-9]*", fields["automaticLanes"]) is None:
            fail(f"output-clearance review input integer is non-canonical: {key}")
        authored = int(fields["authoredCycles"])
        lane_policy = int(fields["lanePolicy"])
        manual_lanes = int(fields["manualLanes"])
        automatic_lanes = int(fields["automaticLanes"])
        max_cycle_grams = int(fields["maxCycleGrams"])
        if authored < 2 or authored > 4 \
                or lane_policy not in (1, 2) \
                or manual_lanes > max_int32 \
                or automatic_lanes > max_int32 \
                or max_cycle_grams > max_int64 \
                or lane_policy == 1 and automatic_lanes != 0 \
                or lane_policy == 2 and automatic_lanes <= 0:
            fail(f"output-clearance review input contract differs: {key}")
        require_lower_sha256(fields["upstreamDigest"], f"{key}.upstreamDigest")
        candidate_row = candidate_by_identity[identity]
        p95_milli_hours = candidate_row["p95HaulClearanceMilliHours"]
        peak_grams_per_hour = candidate_row["peakOutputMassGramsPerHour"]
        profile_digest = candidate_row["rowSourceDigest"]
        if p95_milli_hours > max_int64 // peak_grams_per_hour \
                or max_cycle_grams > max_int64 // 4:
            fail(f"output-clearance review input arithmetic overflows: {key}")
        measured_grams = (
            p95_milli_hours * peak_grams_per_hour + 999
        ) // 1000
        required_grams = max(2 * max_cycle_grams, measured_grams)
        if required_grams > max_int64 // 1000:
            fail(f"output-clearance review milli-cycle arithmetic overflows: {key}")
        required_cycle_milli = (
            required_grams * 1000 + max_cycle_grams - 1
        ) // max_cycle_grams
        raw_cycles = (required_cycle_milli + 999) // 1000
        bounded_cycles = min(raw_cycles, 4)
        published_grams = max_cycle_grams * bounded_cycles
        requirement_disposition = 1 if raw_cycles > 4 else 0
        requirement_diagnostic = (
            "PRODUCTION_OUTPUT_CLEARANCE_BACKPRESSURE_EXPECTED"
            if requirement_disposition == 1 else ""
        )
        requirement_digest = semantic_digest((
            "production-output-clearance-requirement@3",
            max_cycle_grams,
            p95_milli_hours,
            peak_grams_per_hour,
            profile_digest,
            measured_grams,
            required_grams,
            published_grams,
            required_cycle_milli,
            raw_cycles,
            bounded_cycles,
            requirement_disposition,
            "",
            requirement_diagnostic,
        ))
        authored_grams = authored * max_cycle_grams
        is_critical = authored < bounded_cycles
        gate_disposition = 2 if is_critical else requirement_disposition
        failure_code = (
            "PRODUCTION_OUTPUT_CLEARANCE_AUTHORED_CAPACITY_UNDERSIZED"
            if is_critical else ""
        )
        gate_diagnostic = "" if is_critical else requirement_diagnostic
        gate_digest = semantic_digest((
            "production-output-clearance-capacity-gate@2",
            identity[0],
            identity[1],
            authored,
            max_cycle_grams,
            profile_digest,
            requirement_digest,
            required_cycle_milli,
            raw_cycles,
            authored_grams,
            gate_disposition,
            failure_code,
            gate_diagnostic,
        ))
        lane_digest = semantic_digest((
            "production-facility-workstation-lane-capacity-profile@1",
            lane_policy,
            manual_lanes,
            automatic_lanes,
        ))
        input_digest = semantic_digest((
            "production-output-clearance-capacity-review-input@1",
            identity[0],
            identity[1],
            authored,
            max_cycle_grams,
            lane_digest,
            peak_grams_per_hour,
            candidate_row["throughputSourceDigest"],
            fields["upstreamDigest"],
        ))
        review_row_digests.append(semantic_digest((
            "production-output-clearance-capacity-review-row@2",
            input_digest,
            profile_digest,
            gate_digest,
            gate_disposition,
            failure_code,
            gate_diagnostic,
        )))
        if gate_disposition == 0:
            derived_accepted += 1
        elif gate_disposition == 1:
            derived_backpressure += 1
            derived_pressure_identities.add(identity)
        else:
            derived_critical += 1

    review_digest = semantic_digest(tuple([
        "production-output-clearance-capacity-review-portfolio@2",
        expected_profiles,
        *review_row_digests,
        derived_accepted,
        derived_backpressure,
        derived_critical,
    ]))
    if (derived_accepted, derived_backpressure, derived_critical) != (
        int(generation["accepted"]),
        int(generation["backpressureExpected"]),
        int(generation["blockingCritical"]),
    ) or review_digest != generation.get("capacityReviewDigest"):
        fail("output-clearance full review recomputation differs from generation report")

    def pressure_rows(values: dict[str, str], path: Path) -> dict[tuple[str, str], str]:
        rows: dict[tuple[str, str], str] = {}
        indexed = sorted(
            [
                (key, value)
            for key, value in values.items()
                if re.fullmatch(r"backpressure\[\d+\]", key)
            ],
            key=lambda item: int(item[0][13:-1]),
        )
        if len(indexed) != expected_backpressure:
            fail(f"output-clearance pressure row denominator differs in {path.relative_to(ROOT)}")
        identities: list[tuple[str, str]] = []
        for expected_index, (key, value) in enumerate(indexed):
            if key != f"backpressure[{expected_index}]":
                fail(f"output-clearance pressure row index is non-contiguous: {key}")
            fields: dict[str, str] = {}
            for token in value.split(";"):
                if ":" not in token:
                    fail(f"malformed output-clearance pressure token: {token!r}")
                name, field_value = token.split(":", 1)
                if not name or name in fields or not field_value:
                    fail(f"duplicate/empty output-clearance pressure field: {token!r}")
                fields[name] = field_value
            required_order = [
                "definition", "workstation", "authoredCycles", "boundedCycles",
                "rawRequiredCycles", "p95MilliHours", "peakGramsPerHour",
                "maxCycleGrams", "requiredGrams", "authoredGrams", "diagnostic",
                "profileDigest", "gateDigest",
            ]
            if list(fields) != required_order:
                fail(
                    "output-clearance pressure field order/set differs: "
                    f"{list(fields)!r}"
                )
            identity = (fields["definition"], fields["workstation"])
            if identity in rows:
                fail(f"duplicate output-clearance pressure identity: {identity}")
            identities.append(identity)
            numeric_names = (
                "authoredCycles", "boundedCycles", "rawRequiredCycles",
                "p95MilliHours", "peakGramsPerHour", "maxCycleGrams",
                "requiredGrams", "authoredGrams",
            )
            for name in numeric_names:
                if re.fullmatch(r"[1-9][0-9]*", fields[name]) is None:
                    fail(f"non-canonical output-clearance integer: {identity}.{name}")
            numbers = {name: int(fields[name]) for name in numeric_names}
            if numbers["authoredCycles"] > max_int32 \
                    or any(numbers[name] > max_int64 for name in numeric_names):
                fail(f"output-clearance pressure integer exceeds C# range: {identity}")
            if numbers["p95MilliHours"] > max_int64 // numbers["peakGramsPerHour"]:
                fail(f"output-clearance measured-mass multiplication overflows Int64: {identity}")
            if numbers["maxCycleGrams"] > max_int64 // 2:
                fail(f"output-clearance two-cycle multiplication overflows Int64: {identity}")
            measured_grams = (
                numbers["p95MilliHours"] * numbers["peakGramsPerHour"] + 999
            ) // 1000
            required_grams = max(
                2 * numbers["maxCycleGrams"],
                measured_grams,
            )
            raw_cycles = (
                required_grams + numbers["maxCycleGrams"] - 1
            ) // numbers["maxCycleGrams"]
            bounded_cycles = min(raw_cycles, 4)
            if numbers["authoredCycles"] != 4 \
                    or numbers["boundedCycles"] != 4 \
                    or numbers["rawRequiredCycles"] <= 4 \
                    or numbers["requiredGrams"] != required_grams \
                    or numbers["rawRequiredCycles"] != raw_cycles \
                    or numbers["boundedCycles"] != bounded_cycles \
                    or numbers["authoredGrams"] \
                        != numbers["authoredCycles"] * numbers["maxCycleGrams"]:
                fail(f"invalid bounded output-clearance pressure row: {identity}/{numbers}")
            if fields["diagnostic"] != "PRODUCTION_OUTPUT_CLEARANCE_BACKPRESSURE_EXPECTED":
                fail(f"output-clearance pressure diagnostic differs: {identity}")
            require_lower_sha256(fields["profileDigest"], f"{identity}.profileDigest")
            require_lower_sha256(fields["gateDigest"], f"{identity}.gateDigest")
            required_cycle_milli = (
                required_grams * 1000 + numbers["maxCycleGrams"] - 1
            ) // numbers["maxCycleGrams"]
            if required_grams > max_int64 // 1000:
                fail(f"output-clearance milli-cycle multiplication overflows Int64: {identity}")
            if numbers["maxCycleGrams"] > max_int64 // numbers["authoredCycles"]:
                fail(f"output-clearance authored-mass multiplication overflows Int64: {identity}")
            published_grams = numbers["maxCycleGrams"] * bounded_cycles
            diagnostic = "PRODUCTION_OUTPUT_CLEARANCE_BACKPRESSURE_EXPECTED"
            requirement_digest = semantic_digest((
                "production-output-clearance-requirement@3",
                numbers["maxCycleGrams"],
                numbers["p95MilliHours"],
                numbers["peakGramsPerHour"],
                fields["profileDigest"],
                measured_grams,
                required_grams,
                published_grams,
                required_cycle_milli,
                raw_cycles,
                bounded_cycles,
                1,
                "",
                diagnostic,
            ))
            calculated_gate_digest = semantic_digest((
                "production-output-clearance-capacity-gate@2",
                identity[0],
                identity[1],
                numbers["authoredCycles"],
                numbers["maxCycleGrams"],
                fields["profileDigest"],
                requirement_digest,
                required_cycle_milli,
                raw_cycles,
                numbers["authoredGrams"],
                1,
                "",
                diagnostic,
            ))
            if calculated_gate_digest != fields["gateDigest"]:
                fail(f"output-clearance gate digest differs: {identity}")
            rows[identity] = value
        ordinal = lambda value: value.encode("utf-16-be", errors="surrogatepass")
        if identities != sorted(
            identities,
            key=lambda value: (ordinal(value[0]), ordinal(value[1])),
        ):
            fail(f"output-clearance pressure rows are not .NET Ordinal sorted: {path.relative_to(ROOT)}")
        return rows

    generated_rows = pressure_rows(generation, generation_path)
    current_rows = pressure_rows(current, current_path)
    if set(generated_rows) != derived_pressure_identities:
        fail("output-clearance generated pressure identities differ from full review")
    if generated_rows != current_rows:
        fail("output-clearance pressure rows differ between generation and strict current reports")
    for identity, value in generated_rows.items():
        profile = candidate_by_identity.get(identity)
        if profile is None:
            fail(f"output-clearance pressure row has no candidate profile: {identity}")
        fields = dict(token.split(":", 1) for token in value.split(";"))
        if int(fields["p95MilliHours"]) != profile["p95HaulClearanceMilliHours"] \
                or int(fields["peakGramsPerHour"]) != profile["peakOutputMassGramsPerHour"] \
                or fields["profileDigest"] != profile["rowSourceDigest"]:
            fail(f"output-clearance pressure row differs from candidate profile: {identity}")


def require_lower_sha256(value: str, label: str) -> None:
    if re.fullmatch(r"[0-9a-f]{64}", value) is None:
        fail(f"{label} is not a lowercase SHA-256 digest: {value!r}")


def parse_unique_simple_fields(text: str, path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in text.splitlines():
        match = re.fullmatch(r"([A-Za-z][A-Za-z0-9]*)=([^;\r\n]*)", line)
        if match is None:
            continue
        key, value = match.groups()
        if key in values:
            fail(f"duplicate simple field in {path.relative_to(ROOT)}: {key}")
        values[key] = value
    return values


def require_current_source_parent_fields(
    fields: dict[str, str], path: Path, schema_key: str, schema: str
) -> tuple[str, int, str, str]:
    source, input_count, path_digest = current_source_snapshot()
    scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    official_scene = (
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40"
    )
    required = {
        schema_key: schema,
        "RESULT": "PASS",
        "currentSourceDigest": source,
        "currentSourceInputCount": str(input_count),
        "currentSourcePathListDigest": path_digest,
        "gameplaySceneSha256": scene,
        "consoleWarnings": "0",
        "consoleErrors": "0",
        "secondWriteDiff": "0",
        "secondWriteLengthDiff": "0",
        "secondWriteMtimeDiff": "0",
    }
    if scene != official_scene:
        fail(
            "official GameplayScene digest drifted: "
            f"expected={official_scene} actual={scene}"
        )
    for key, expected in required.items():
        if fields.get(key) != expected:
            fail(
                f"{path.relative_to(ROOT)} {key} mismatch: "
                f"expected={expected!r} actual={fields.get(key)!r}"
            )
    return source, input_count, path_digest, scene


def verify_batch_c_owner_manifest() -> tuple[dict[str, str], str, str]:
    report_path = QA / "v27-facility-buffer-owner-manifest.txt"
    csv_path = QA / "v27-facility-buffer-owner-manifest.csv"
    report = parse_exact_key_value_report(report_path)
    required = {
        "schemaVersion": "3",
        "scope": "FacilityBuffer,FacilityOutputBuffer,DirectLooseOutput",
        "fullStoredDestinationCoverage": "true",
        "inputRemaining": "0",
        "outputRemaining": "0",
        "remaining": "0",
        "bypass": "0",
        "orphan": "0",
        "unclassified": "0",
        "classificationGate": "PASS",
        "fullMigrationGate": "PASS",
    }
    for key, expected in required.items():
        if report.get(key) != expected:
            fail(
                f"Batch C owner manifest {key} mismatch: "
                f"expected={expected!r} actual={report.get(key)!r}"
            )
    require_lower_sha256(report.get("sourceDigest", ""), "Batch C sourceDigest")
    require_lower_sha256(
        report.get("deliveryInvocationSetDigest", ""),
        "Batch C deliveryInvocationSetDigest",
    )

    raw = csv_path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf") or not raw.endswith(b"\r\n"):
        fail("Batch C owner CSV must be UTF-8 without BOM and end CRLF")
    expected_header = [
        "schemaVersion",
        "state",
        "ownerDomain",
        "destinationRule",
        "producerSymbol",
        "claimAuthority",
        "capacityAuthority",
        "consumerAndPersistence",
        "cancelRelease",
        "disposition",
        "sourcePath",
        "sourceDigest",
    ]
    rows: list[dict[str, str]] = []
    previous: tuple[str, str, str, str, str] | None = None
    with csv_path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        if reader.fieldnames != expected_header:
            fail(f"Batch C owner CSV header drifted: {reader.fieldnames}")
        for row in reader:
            if row["schemaVersion"] != "1":
                fail(f"Batch C owner row schema drifted: {row}")
            if row["sourceDigest"] != report["sourceDigest"]:
                fail(f"Batch C owner row source digest drifted: {row['ownerDomain']}")
            key = (
                row["state"],
                row["ownerDomain"],
                row["destinationRule"],
                row["sourcePath"],
                row["producerSymbol"],
            )
            if previous is not None and key <= previous:
                fail(f"Batch C owner CSV duplicate/ordering drift: {key}")
            previous = key
            rows.append(row)
    if not rows:
        fail("Batch C owner CSV has no rows")

    input_rows = [
        row
        for row in rows
        if row["state"] == "FacilityBuffer"
        and row["disposition"] in {"migrated", "remaining"}
    ]
    output_rows = [
        row
        for row in rows
        if row["state"] in {"FacilityOutputBuffer", "DirectLooseOutput"}
    ]
    input_migrated = sum(row["disposition"] == "migrated" for row in input_rows)
    output_migrated = sum(row["disposition"] == "migrated" for row in output_rows)
    counts = {
        "inputOwners": len(input_rows),
        "inputMigrated": input_migrated,
        "outputOwners": len(output_rows),
        "outputMigrated": output_migrated,
        "remaining": sum(row["disposition"] == "remaining" for row in rows),
        "bypass": sum(row["disposition"] == "bypass" for row in rows),
        "orphan": sum(row["disposition"] == "orphan" for row in rows),
        "transportDelegatedExact": sum(
            row["disposition"] == "transport-delegated-exact" for row in rows
        ),
        "delegatedConsumer": sum(
            row["disposition"] == "delegated-consumer" for row in rows
        ),
        "duplicateAuthority": sum(
            row["disposition"] == "duplicate-authority"
            for row in rows
        ),
    }
    for key, actual in counts.items():
        if int(report.get(key, "-1")) != actual:
            fail(
                f"Batch C owner denominator {key} differs: "
                f"report={report.get(key)} actual={actual}"
            )
    if len(input_rows) < 36 or input_migrated != len(input_rows):
        fail(f"Batch C input owner closure regressed: {input_migrated}/{len(input_rows)}")
    if len(output_rows) < 10 or output_migrated != len(output_rows):
        fail(
            f"Batch C output owner closure regressed: "
            f"{output_migrated}/{len(output_rows)}"
        )
    return report, sha256(csv_path), sha256(report_path)


def verify_batch_a_output_closure() -> None:
    path = QA / "v27-batch-a-output-closure.txt"
    fields = parse_exact_key_value_report(path)
    source, input_count, path_digest = current_source_snapshot()
    scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    required = {
        "schemaVersion": "2",
        "batch": "A",
        "currentSourceDigest": source,
        "currentSourceInputCount": str(input_count),
        "currentSourcePathListDigest": path_digest,
        "gameplaySceneSha256": scene,
        "outputOwners": "10",
        "outputMigrated": "10",
        "outputRemaining": "0",
        "bypass": "0",
        "orphan": "0",
        "unclassified": "0",
        "deliveryInvocations": "46",
        "deliveryInvocationFiles": "29",
        "partialRoute": "PASS",
        "cancel": "PASS",
        "downedCurrentCell": "PASS",
        "midHaulRestore": "PASS",
        "outputSpaceRetry": "PASS",
        "syntheticLive": "PASS",
        "sawmillLive": "PASS",
        "surgicalLive": "PASS",
        "worldResourceFaultMatrix": "PASS",
        "deterministicDoubleCapture": "PASS",
        "secondRunByteDiff": "0",
        "secondRunLengthDiff": "0",
        "secondRunMtimeDiff": "0",
        "result": "PASS",
    }
    for key, expected in required.items():
        if fields.get(key) != expected:
            fail(
                f"Batch A {key} mismatch: "
                f"expected={expected!r} actual={fields.get(key)!r}"
            )
    official_scene = (
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40"
    )
    if scene != official_scene:
        fail(f"Batch A official GameplayScene digest drifted: {scene}")
    for key in (
        "sourceDigest",
        "manifestSourceDigest",
        "ownerSetDigest",
        "manifestDeliveryCallsiteDigest",
        "deliveryCallsiteDigest",
    ):
        require_lower_sha256(fields.get(key, ""), f"Batch A {key}")
    if fields["manifestDeliveryCallsiteDigest"] != fields["deliveryCallsiteDigest"]:
        fail("Batch A manifest/current delivery callsite digests differ")

    owner_keys = sorted(
        (key for key in fields if re.fullmatch(r"owner\[\d+\]", key)),
        key=lambda key: int(key[6:-1]),
    )
    if owner_keys != [f"owner[{index}]" for index in range(10)]:
        fail(f"Batch A owner rows are not contiguous 0..9: {owner_keys}")
    if len({fields[key] for key in owner_keys}) != 10:
        fail("Batch A owner rows contain duplicates")

    expected_artifacts = {
        "m06-surgical",
        "owner-manifest-csv",
        "owner-manifest-report",
        "sawmill",
        "synthetic-canary",
        "world-resource",
    }
    actual_artifacts = {
        match.group(1)
        for key in fields
        if (match := re.fullmatch(r"artifact:([^.]+)\.path", key)) is not None
    }
    if actual_artifacts != expected_artifacts:
        fail(f"Batch A artifact set differs: {sorted(actual_artifacts)}")
    for artifact_id in sorted(expected_artifacts):
        prefix = f"artifact:{artifact_id}."
        relative = fields.get(prefix + "path", "")
        if (
            not relative
            or "\\" in relative
            or relative.startswith("/")
            or ".." in Path(relative).parts
        ):
            fail(f"Batch A artifact path is non-canonical: {relative!r}")
        artifact_path = ROOT / relative
        if fields.get(prefix + "currentSourceDigest") != source:
            fail(f"Batch A artifact source is stale: {artifact_id}")
        if fields.get(prefix + "gameplaySceneSha256") != scene:
            fail(f"Batch A artifact scene is stale: {artifact_id}")
        if fields.get(prefix + "byteSha256") != sha256(artifact_path):
            fail(f"Batch A artifact hash drifted: {artifact_id}")
        if int(fields.get(prefix + "byteLength", "-1")) != artifact_path.stat().st_size:
            fail(f"Batch A artifact length drifted: {artifact_id}")

    c_report, c_csv_sha, c_report_sha = verify_batch_c_owner_manifest()
    if fields["manifestSourceDigest"] != c_report["sourceDigest"]:
        fail("Batch A/C owner manifest source digest differs")
    if fields["artifact:owner-manifest-csv.byteSha256"] != c_csv_sha:
        fail("Batch A child hash for the C owner CSV differs")
    if fields["artifact:owner-manifest-report.byteSha256"] != c_report_sha:
        fail("Batch A child hash for the C owner report differs")
    for key in (
        "deliveryInvocations",
        "deliveryInvocationFiles",
        "deliveryInvocationSetDigest",
    ):
        a_key = (
            "manifestDeliveryCallsiteDigest"
            if key == "deliveryInvocationSetDigest"
            else key
        )
        if fields[a_key] != c_report[key]:
            fail(f"Batch A/C delivery authority differs: {key}")


def verify_batch_b_parent() -> None:
    path = QA / "v27-batch-b-parent.txt"
    fields = parse_exact_key_value_report(path)
    require_current_source_parent_fields(
        fields, path, "schema", "v27-batch-b-parent@1"
    )
    required = {
        "batch": "B",
        "expectedChecks": "40",
        "verifiedChecks": "40",
        "retargetTransaction": "PASS",
        "clearanceRequirement": "PASS",
        "clearanceProfile": "PASS",
        "clearanceCapacityPortfolio": "PASS",
        "unifiedMutationFence": "PASS",
        "activeMultiFacilityRetarget": "PASS",
        "productionEconomyBroad": "PASS",
    }
    for key, expected in required.items():
        if fields.get(key) != expected:
            fail(
                f"Batch B {key} mismatch: "
                f"expected={expected!r} actual={fields.get(key)!r}"
            )
    expected_ids = [
        "b01-cycle-capacity-authority",
        "b02-explicit-live-buffer-authority",
        "b03-p17-maximum-branch-mass",
        "b04-restore-capacity-upper-bound",
        "b05-one-gram-admission-boundary",
        "b06-no-bill-profile-publication",
        "b07-support-rational-maximum",
        "b08-generic-recipe-preprojection",
        "b09-capacity-contributor-registry",
        "b10-producer-facility-census",
        "b11-apparel-capability-envelope",
        "b12-surgical-recipe-envelope",
        "b13-combat-shared-eligibility",
        "b14-combat-primary-craft-envelope",
        "b15-combat-recovery-envelope",
        "b16-census-zero-orphans",
        "b17-capacity-source-digest",
        "b18-topology-change-reprojection",
        "b19-destructive-terminal-drain",
        "b20-restore-input-order-invariance",
        "b21-whole-maximum-envelope",
        "b22-projection-execution-registry-parity",
        "b23-certified-seed-eligibility",
        "b24-combat-allowlist-authority",
        "b25-p17-live-current-source-contract",
        "b26-production-normal-boot-contract",
        "b27-output-destination-lifecycle",
        "b28-output-exact-claim-authority",
        "b29-full-path-capacity-canary",
        "b30-direct-demolition-transaction",
        "b31-mutation-epoch-fence",
        "b32-structural-loss-fence",
        "b33-world-replacement-retire",
        "b34-active-custody-terminal-drain",
        "b35-crop-whole-vector-publication",
        "b36-destructive-live-integration",
        "b37-reversible-retarget-transaction",
        "b38-support-p95-four-cycle-gate",
        "b39-unified-mutation-parent",
        "b40-active-multi-facility-retarget",
    ]
    rows: list[tuple[str, str, str]] = []
    for index in range(40):
        value = fields.get(f"row[{index}]", "")
        parts = value.split("|")
        if len(parts) != 3:
            fail(f"Batch B row[{index}] has invalid shape: {value!r}")
        rows.append((parts[0], parts[1], parts[2]))
    if [row[0] for row in rows] != expected_ids:
        fail("Batch B stable 40-row denominator drifted")
    if any(not row[1] or row[2] != "PASS" for row in rows):
        fail("Batch B row lacks an exact evidence gate or PASS result")


def verify_batch_c_parent() -> None:
    path = QA / "v27-batch-c-parent.txt"
    fields = parse_exact_key_value_report(path)
    require_current_source_parent_fields(
        fields, path, "schema", "v27-batch-c-parent@1"
    )
    required = {
        "batch": "C",
        "inputOwners": "36",
        "inputMigrated": "36",
        "inputRemaining": "0",
        "outputOwners": "10",
        "outputMigrated": "10",
        "outputRemaining": "0",
        "remaining": "0",
        "bypass": "0",
        "orphan": "0",
        "unclassified": "0",
        "ownerManifestAuthority": "PASS",
        "fullStoredDestinationCoverage": "true",
    }
    for key, expected in required.items():
        if fields.get(key) != expected:
            fail(
                f"Batch C parent {key} mismatch: "
                f"expected={expected!r} actual={fields.get(key)!r}"
            )
    _, csv_sha, report_sha = verify_batch_c_owner_manifest()
    if fields.get("ownerManifestCsvSha256") != csv_sha:
        fail("Batch C parent owner CSV hash drifted")
    if fields.get("ownerManifestReportSha256") != report_sha:
        fail("Batch C parent owner report hash drifted")


def verify_current_source_fg_orchestration() -> None:
    path = QA / "v27-current-source-fg-evidence-orchestration.txt"
    if not path.is_file():
        fail(f"required report is missing: {path.relative_to(ROOT)}")
    text = path.read_text(encoding="utf-8-sig")
    fields = parse_unique_simple_fields(text, path)
    expected_source = current_all_scripts_sha256()
    expected_scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    final_paired_seed_count = 64
    final_paired_window_count = 1024
    final_paired_floor_row_count = 1280
    final_paired_fault_arm_count = 128
    required = {
        "RESULT": "PASS",
        "schema": "v27-current-source-fg-orchestration@2",
        "currentSourceDigest": expected_source,
        "gameplaySceneSha256": expected_scene,
        "evidenceSteps": "9/9",
        "pairedSeeds": str(final_paired_seed_count),
        "pairedWindows": str(final_paired_window_count),
        "pairedFloorRows": str(final_paired_floor_row_count),
        "pairedFaultArms": str(final_paired_fault_arm_count),
        "consoleWarnings": "0",
        "consoleErrors": "0",
        "aggregateSecondRunByteDiff": "0",
        "aggregateSecondRunLengthDiff": "0",
        "aggregateSecondRunMtimeDiff": "0",
        "orchestrationSecondRunByteDiff": "0",
        "orchestrationSecondRunLengthDiff": "0",
        "orchestrationSecondRunMtimeDiff": "0",
    }
    for key, expected in required.items():
        if fields.get(key) != expected:
            fail(
                f"F/G orchestration {key} mismatch: "
                f"expected={expected!r} actual={fields.get(key)!r}"
            )

    expected_steps = {
        "prepared-output-p17",
        "prepared-output-synthetic-canary",
        "prepared-output-sawmill-transport",
        "production-input-mass",
        "prepared-output-destructive-drain",
        "character-ai-cross-action-fault",
        "ability-haul-lifecycle-recovery",
        "remaining-focused-faults",
        "paired-clutter-final",
    }
    seen_steps: set[str] = set()
    paired_evidence_path: Path | None = None
    for step, relative, expected_hash, expected_length in re.findall(
        r"^evidence=([^;]+); path=([^;]+); sha256=([0-9a-f]{64}); bytes=(\d+)$",
        text,
        flags=re.MULTILINE,
    ):
        if step in seen_steps or step not in expected_steps:
            fail(f"unexpected or duplicate F/G evidence step: {step}")
        if "\\" in relative or relative.startswith("/") or ".." in Path(relative).parts:
            fail(f"non-canonical F/G evidence path: {relative!r}")
        evidence_path = ROOT / relative
        if step == "paired-clutter-final":
            expected_relative = "Artifacts/QA/v27-balance-paired-run-rng.txt"
            if relative != expected_relative:
                fail(
                    "final paired-clutter evidence path differs: "
                    f"expected={expected_relative!r} actual={relative!r}"
                )
            paired_evidence_path = evidence_path
        actual_hash = sha256(evidence_path)
        actual_length = evidence_path.stat().st_size
        if actual_hash != expected_hash or actual_length != int(expected_length):
            fail(
                f"F/G evidence artifact drift for {step}: "
                f"sha={actual_hash}/{expected_hash}; bytes={actual_length}/{expected_length}"
            )
        seen_steps.add(step)
    if seen_steps != expected_steps:
        fail(f"F/G evidence steps differ: missing={sorted(expected_steps - seen_steps)}")
    if paired_evidence_path is None:
        fail("final paired-clutter evidence is missing")

    paired_text = paired_evidence_path.read_text(encoding="utf-8-sig")

    def require_paired_field(key: str, expected: str) -> None:
        matches: list[str] = []
        for line in paired_text.splitlines():
            for field in line.split(";"):
                candidate = field.strip()
                if "=" not in candidate:
                    continue
                actual_key, actual_value = candidate.split("=", 1)
                if actual_key.strip() == key:
                    matches.append(actual_value.strip())
        if len(matches) != 1 or matches[0] != expected:
            fail(
                f"final paired-clutter {key} mismatch: "
                f"expected={expected!r} actual={matches!r}"
            )

    paired_required = {
        "RESULT": "PASS",
        "seeds": str(final_paired_seed_count),
        "windows": str(final_paired_window_count),
        "floorRows": str(final_paired_floor_row_count),
        "failures": "0",
        "consoleIssues": "0",
        "currentSourceDigest": expected_source,
        "gameplaySceneSha256": expected_scene,
    }
    for key, expected in paired_required.items():
        require_paired_field(key, expected)

    exact_markers = {
        f"PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds={final_paired_seed_count}",
        f"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={final_paired_fault_arm_count}",
        f"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={final_paired_fault_arm_count}",
        f"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={final_paired_fault_arm_count}",
    }
    paired_lines = set(paired_text.splitlines())
    missing_markers = sorted(exact_markers - paired_lines)
    if missing_markers:
        fail(f"final paired-clutter exact markers are missing: {missing_markers}")

    paired_csv_path = QA / "v27-balance-paired-run-rng.csv"
    floor_csv_path = QA / "v27-balance-floor-clutter.csv"
    paired_csv_hash = sha256(paired_csv_path)
    floor_csv_hash = sha256(floor_csv_path)
    require_lower_sha256(paired_csv_hash, "final paired-clutter paired CSV")
    require_lower_sha256(floor_csv_hash, "final paired-clutter floor CSV")
    require_paired_field("pairedCsvSha256", paired_csv_hash)
    require_paired_field("floorCsvSha256", floor_csv_hash)

    def verify_paired_csv(
        csv_path: Path,
        expected_header: list[str],
        expected_rows: int,
        rows_per_seed: int,
        key_fields: tuple[str, ...],
    ) -> None:
        raw = csv_path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or not raw.endswith(b"\r\n"):
            fail(
                "final paired-clutter CSV must be UTF-8 without BOM and end CRLF: "
                f"{csv_path.relative_to(ROOT)}"
            )
        row_count = 0
        row_keys: set[tuple[str, ...]] = set()
        seed_counts: dict[int, int] = {}
        with csv_path.open("r", encoding="utf-8", newline="") as stream:
            reader = csv.DictReader(stream, strict=True)
            if reader.fieldnames != expected_header:
                fail(
                    "final paired-clutter CSV header differs: "
                    f"{csv_path.relative_to(ROOT)}: {reader.fieldnames}"
                )
            for row in reader:
                try:
                    seed = int(row["seed"])
                except (KeyError, TypeError, ValueError) as error:
                    fail(
                        "final paired-clutter CSV seed is invalid: "
                        f"{csv_path.relative_to(ROOT)}: {error}"
                    )
                key = tuple(row[field] for field in key_fields)
                if key in row_keys:
                    fail(
                        "duplicate final paired-clutter CSV row key: "
                        f"{csv_path.relative_to(ROOT)}: {key}"
                    )
                row_keys.add(key)
                seed_counts[seed] = seed_counts.get(seed, 0) + 1
                row_count += 1
        expected_seeds = set(range(1, final_paired_seed_count + 1))
        if (
            row_count != expected_rows
            or set(seed_counts) != expected_seeds
            or any(count != rows_per_seed for count in seed_counts.values())
        ):
            fail(
                "final paired-clutter CSV denominator differs: "
                f"{csv_path.relative_to(ROOT)}: rows={row_count}/{expected_rows}; "
                f"seeds={len(seed_counts)}/{final_paired_seed_count}; "
                f"perSeed={sorted(set(seed_counts.values()))}/{rows_per_seed}"
            )

    verify_paired_csv(
        paired_csv_path,
        [
            "seed",
            "arm",
            "window",
            "travelMilliWu",
            "waitMilliWu",
            "dispatchWaitMilliWu",
            "reservationWaitMilliWu",
            "facilityAccessWaitMilliWu",
            "noPathMilliWu",
            "burstDeliveredQuantity",
            "burstOutstandingQuantity",
            "burstQuantityConserved",
            "replanCount",
            "stepAsideCount",
            "clutterCellSeconds",
            "semanticStateHash",
            "randomStateHash",
            "exogenousEventHash",
        ],
        final_paired_window_count,
        16,
        ("seed", "arm", "window"),
    )
    verify_paired_csv(
        floor_csv_path,
        [
            "seed",
            "arm",
            "window",
            "isRecovery",
            "graceSeconds",
            "looseStacks",
            "looseQuantity",
            "outsideContainment",
            "persistent",
            "immediateFailures",
            "clutterCellSeconds",
            "runtimeHeadroomPermille",
            "runtimeErosionCells",
            "runtimeErosionDetail",
        ],
        final_paired_floor_row_count,
        20,
        ("seed", "arm", "window"),
    )

    aggregates = {
        batch: (csv_hash, report_hash, no_op)
        for batch, csv_hash, report_hash, no_op in re.findall(
            r"^batch=([FG]); csvSha256=([0-9a-f]{64}); "
            r"reportSha256=([0-9a-f]{64}); noOp=([^\r\n]+)$",
            text,
            flags=re.MULTILINE,
        )
    }
    expected_aggregates = {
        "F": (
            QA / "v27-domain-cluster-closure.csv",
            QA / "v27-domain-cluster-closure.txt",
            "RESULT=PASS; batch=F; structural=6/6; closed=6/6; open=0",
        ),
        "G": (
            QA / "v27-live-fault-matrix.csv",
            QA / "v27-live-fault-matrix.txt",
            "RESULT=PASS; batch=G; closed=19; total=19; open=0",
        ),
    }
    if set(aggregates) != set(expected_aggregates):
        fail(f"F/G aggregate set differs: {sorted(aggregates)}")
    for batch, (csv_path, report_path, marker) in expected_aggregates.items():
        csv_hash, report_hash, no_op = aggregates[batch]
        if no_op != "PASS" or sha256(csv_path) != csv_hash or sha256(report_path) != report_hash:
            fail(f"F/G aggregate artifact or no-op drift: batch={batch}")
        require_text(report_path, (marker,))


def verify_natural_output_clearance_portfolio() -> None:
    report_path = QA / "v27-production-output-clearance-natural-portfolio.txt"
    observations_path = (
        QA / "v27-production-output-clearance-natural-observations.csv"
    )
    measurement_plans_path = (
        QA / "v27-production-output-clearance-measurement-plan.csv"
    )
    slices_path = (
        QA / "v27-production-output-clearance-natural-output-slices.csv"
    )
    runner_path = (
        QA / "v27-production-output-clearance-natural-portfolio-runner.txt"
    )
    if not runner_path.is_file():
        fail(f"required report is missing: {runner_path.relative_to(ROOT)}")
    runner_text = runner_path.read_text(encoding="utf-8-sig")
    if "RESULT=PASS; failures=0" not in runner_text:
        fail("natural portfolio runner is not terminal PASS")
    runner = parse_unique_simple_fields(runner_text, runner_path)
    report = parse_exact_key_value_report(report_path)
    required = {
        "schema": "v27-production-output-clearance-natural-portfolio@2",
        "result": "PASS",
        "minimumV27Plans": "92",
        "seeds": "32",
        "consoleWarnings": "0",
        "consoleErrors": "0",
        "secondBuildByteDiff": "0",
        "secondBuildLengthDiff": "0",
        "secondBuildMtimeDiff": "0",
    }
    for key, expected in required.items():
        if report.get(key) != expected:
            fail(
                f"natural portfolio report {key} mismatch: "
                f"expected={expected!r} actual={report.get(key)!r}"
            )
    plan_count = int(report.get("plans", "-1"))
    seed_count = int(report.get("seeds", "-1"))
    observation_count = int(report.get("observations", "-1"))
    if (
        plan_count < 92
        or seed_count != 32
        or observation_count != plan_count * seed_count
    ):
        fail(
            "natural portfolio denominator is not a complete dynamic x32 matrix: "
            f"plans={plan_count}; seeds={seed_count}; observations={observation_count}"
        )
    resumed = int(report.get("resumed", "-1"))
    executed = int(report.get("executed", "-1"))
    reported_slice_count = int(report.get("outputSlices", "-1"))
    if resumed < 0 or executed < 0 or resumed + executed != observation_count:
        fail(
            "natural portfolio resume/execution counts do not form the current matrix: "
            f"resumed={resumed}; executed={executed}"
        )

    measurement_plan_keys: set[tuple[str, str]] = set()
    with measurement_plans_path.open("r", encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream, strict=True):
            key = (row["definitionId"], row["workstationTag"])
            if key in measurement_plan_keys:
                fail(f"duplicate natural measurement plan: {key}")
            measurement_plan_keys.add(key)
    if len(measurement_plan_keys) != plan_count:
        fail(
            "natural portfolio plan denominator differs from the measurement plan: "
            f"report={plan_count}; measurement={len(measurement_plan_keys)}"
        )
    if report.get("measurementPlanCsvSha256") != sha256(measurement_plans_path):
        fail("natural portfolio measurement-plan byte hash is stale")

    expected_source = report.get("currentSourceDigest", "")
    require_lower_sha256(expected_source, "natural portfolio historical source digest")
    expected_scene = sha256(ROOT / "Assets/Scenes/GameplayScene.unity")
    official_scene = (
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40"
    )
    if expected_scene != official_scene or report.get("gameplaySceneSha256") != expected_scene:
        fail(
            "natural portfolio official GameplayScene digest mismatch: "
            f"official={official_scene} current={expected_scene} "
            f"report={report.get('gameplaySceneSha256')}"
        )
    runner_required = {
        "naturalRunnerSchema": "v27-production-output-clearance-natural-runner@2",
        "naturalMinimumV27Plans": "92",
        "naturalPlanCount": str(plan_count),
        "currentSourceDigest": expected_source,
        "gameplaySceneSha256": expected_scene,
        "naturalPortfolioReportSha256": sha256(report_path),
        "naturalObservationsCsvSha256": sha256(observations_path),
        "naturalOutputSlicesCsvSha256": sha256(slices_path),
    }
    for key, expected in runner_required.items():
        if runner.get(key) != expected:
            fail(
                f"natural runner {key} mismatch: "
                f"expected={expected!r} actual={runner.get(key)!r}"
            )
    for key in (
        "currentPortfolioDigest",
        "descriptorCoverageDigest",
        "measurementPortfolioDigest",
        "acceptedPortfolioDigest",
        "handlerRegistryFingerprint",
        "executorRegistryFingerprint",
        "observationsCsvSha256",
        "outputSlicesCsvSha256",
    ):
        require_lower_sha256(report.get(key, ""), f"natural portfolio {key}")
    if sha256(observations_path) != report["observationsCsvSha256"]:
        fail("natural observation CSV byte hash differs from its report")
    if sha256(slices_path) != report["outputSlicesCsvSha256"]:
        fail("natural output-slice CSV byte hash differs from its report")

    for path in (observations_path, slices_path):
        raw = path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or not raw.endswith(b"\r\n"):
            fail(
                f"natural portfolio CSV must be UTF-8 without BOM and end CRLF: "
                f"{path.relative_to(ROOT)}"
            )

    observation_rows: dict[str, dict[str, str]] = {}
    observation_keys: set[tuple[str, str, int]] = set()
    execution_commits: set[tuple[str, str]] = set()
    plan_seed_pairs: dict[tuple[str, str], list[tuple[int, int]]] = {}
    with observations_path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        expected_observation_header = [
            "schema", "definitionId", "workstationTag", "seedIndex",
            "deterministicSeed", "observationId", "facilitySemanticId",
            "operationSemanticId", "batchSemanticId", "actualBatchMassGrams",
            "clearanceMicroHours", "canonicalResolvedOutputVectorDigest",
            "canonicalReceiptDigest", "canonicalRunDigest", "currentSourceDigest",
            "gameplaySceneSha256", "measurementPortfolioDigest",
            "acceptedPortfolioDigest",
        ]
        if reader.fieldnames != expected_observation_header:
            fail(f"natural observation CSV header differs: {reader.fieldnames}")
        for row in reader:
            observation_id = row["observationId"]
            key = (
                row["definitionId"],
                row["workstationTag"],
                int(row["deterministicSeed"]),
            )
            batch = row["batchSemanticId"]
            execution_commit = (observation_id, batch)
            if (
                observation_id in observation_rows
                or key in observation_keys
                or execution_commit in execution_commits
            ):
                fail(f"natural observation row is duplicated: {observation_id}")
            if (
                "structural-clearance" in row["facilitySemanticId"]
                or "structural-clearance" in batch
            ):
                fail(f"structural fixture was presented as natural evidence: {observation_id}")
            if int(row["actualBatchMassGrams"]) <= 0 or int(row["clearanceMicroHours"]) <= 0:
                fail(f"natural observation is nonphysical: {observation_id}")
            if row["currentSourceDigest"] != expected_source:
                fail(f"stale natural observation source: {observation_id}")
            if row["gameplaySceneSha256"] != expected_scene:
                fail(f"stale natural observation scene: {observation_id}")
            for field in (
                "canonicalResolvedOutputVectorDigest",
                "canonicalReceiptDigest",
                "canonicalRunDigest",
                "measurementPortfolioDigest",
                "acceptedPortfolioDigest",
            ):
                require_lower_sha256(row[field], f"{observation_id}.{field}")
            if row["measurementPortfolioDigest"] != report["measurementPortfolioDigest"]:
                fail(f"natural observation measurement portfolio drift: {observation_id}")
            if row["acceptedPortfolioDigest"] != report["acceptedPortfolioDigest"]:
                fail(f"natural observation accepted portfolio drift: {observation_id}")
            observation_rows[observation_id] = row
            observation_keys.add(key)
            execution_commits.add(execution_commit)
            plan_seed_pairs.setdefault((key[0], key[1]), []).append(
                (int(row["seedIndex"]), key[2])
            )
    if len(observation_rows) != observation_count:
        fail(
            "natural observation row count mismatch: "
            f"{len(observation_rows)}/{observation_count}"
        )
    expected_seeds = [(index, 157181 + index) for index in range(32)]
    if set(plan_seed_pairs) != measurement_plan_keys:
        fail(
            "natural observation plan keys differ from the measurement plan: "
            f"observed={len(plan_seed_pairs)}; expected={len(measurement_plan_keys)}"
        )
    for plan, seeds in plan_seed_pairs.items():
        if sorted(seeds) != expected_seeds:
            fail(f"natural observation seed portfolio mismatch for {plan}: {sorted(seeds)}")

    slice_mass_by_observation: dict[str, int] = {}
    slice_keys: set[tuple[str, str, str]] = set()
    slice_count = 0
    with slices_path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        expected_slice_header = [
            "schema", "observationId", "batchSemanticId", "outputLineId",
            "itemId", "itemInstanceSemanticId", "stackSemanticId",
            "sliceOrdinal", "quantity", "massGrams", "capabilityFingerprint",
            "semanticDigest", "currentSourceDigest", "gameplaySceneSha256",
        ]
        if reader.fieldnames != expected_slice_header:
            fail(f"natural output-slice CSV header differs: {reader.fieldnames}")
        for row in reader:
            observation_id = row["observationId"]
            if observation_id not in observation_rows:
                fail(f"orphan natural output slice: {observation_id}")
            key = (observation_id, row["outputLineId"], row["stackSemanticId"])
            if key in slice_keys:
                fail(f"duplicate natural physical output slice: {key}")
            if row["batchSemanticId"] != observation_rows[observation_id]["batchSemanticId"]:
                fail(f"natural output slice batch mismatch: {key}")
            quantity = int(row["quantity"])
            mass = int(row["massGrams"])
            if quantity <= 0 or mass <= 0:
                fail(f"nonphysical natural output slice: {key}")
            if row["currentSourceDigest"] != expected_source or row["gameplaySceneSha256"] != expected_scene:
                fail(f"stale natural output slice: {key}")
            for field in ("capabilityFingerprint", "semanticDigest"):
                require_lower_sha256(row[field], f"{key}.{field}")
            slice_keys.add(key)
            slice_mass_by_observation[observation_id] = (
                slice_mass_by_observation.get(observation_id, 0) + mass
            )
            slice_count += 1
    if (
        slice_count < observation_count
        or slice_count != reported_slice_count
        or len(slice_mass_by_observation) != observation_count
    ):
        fail(
            "natural output-slice coverage is incomplete: "
            f"rows={slice_count}/{reported_slice_count}; "
            f"observations={len(slice_mass_by_observation)}"
        )
    for observation_id, row in observation_rows.items():
        expected_mass = int(row["actualBatchMassGrams"])
        actual_mass = slice_mass_by_observation.get(observation_id, 0)
        if actual_mass != expected_mass:
            fail(
                f"natural output-slice mass mismatch for {observation_id}: "
                f"expected={expected_mass} actual={actual_mass}"
            )


MARKET_DECISION_KEYS = (
    "schemaVersion",
    "epochId",
    "decisionPayloadDigest",
    "decisionEpochDigest",
    "sourceLedgerDigest",
    "patchScopeDigest",
    "previousDecisionEpochDigest",
    "previousDecisionAuthorityDigest",
    "decisions",
)
MARKET_DECISION_BUNDLE_KEYS = (
    "bundleId",
    "bundleDigest",
    "anchorItemId",
    "decisionReason",
    "reviewedBaselineRecordId",
    "members",
)
MARKET_DECISION_MEMBER_KEYS = (
    "stableId",
    "authorityMetric",
    "sourceAuthority",
    "sourcePropertyPath",
    "beforeExactToken",
    "candidateExactToken",
    "dependencyFingerprint",
    "sourceDigest",
    "semanticHash",
    "promotedAuthorityDependencyFingerprint",
    "promotedAuthoritySourceDigest",
    "promotedAuthoritySemanticHash",
    "decision",
    "replacementExactToken",
)
MARKET_PATCH_SCOPE_KEYS = (
    "role",
    "stableId",
    "metric",
    "sourceAuthority",
    "sourcePropertyPath",
    "before",
    "after",
    "dependencyFingerprint",
    "sourceDigest",
    "semanticHash",
)
MARKET_V2_ASSET_KEYS = (
    "sourceAuthority",
    "assetBeforeSha256",
    "assetAfterSha256",
)
MARKET_V2_ROW_KEYS = (
    "decisionBundleId",
    "decisionBundleDigest",
    "stableId",
    "authorityMetric",
    "sourceAuthority",
    "sourcePropertyPath",
    "exactBeforeValue",
    "exactAfterValue",
    "dependencyFingerprint",
    "sourceDigest",
    "semanticHash",
    "promotedAuthorityDependencyFingerprint",
    "promotedAuthoritySourceDigest",
    "promotedAuthoritySemanticHash",
    "decision",
    "replacementExactToken",
    "decisionMemberDigest",
    "appliedApprovalKey",
    "assetBeforeSha256",
    "assetAfterSha256",
    "receiptRowDigest",
)


def _market_patch_scope_digest(rows: list[dict[str, object]]) -> str:
    tokens: list[object] = []
    ordered = sorted(
        rows,
        key=lambda row: tuple(
            utf16_ordinal_key(require_string(row[field], f"patchScope.{field}"))
            for field in ("role", "stableId", "metric")
        ),
    )
    for row in ordered:
        tokens.extend(row[field] for field in MARKET_PATCH_SCOPE_KEYS)
    return market_digest(tokens)


def _market_decision_member_digest(
    bundle: dict[str, object],
    member: dict[str, object],
) -> str:
    return market_digest(
        [
            bundle["bundleId"],
            bundle["bundleDigest"],
            bundle["anchorItemId"],
            bundle["decisionReason"],
            bundle["reviewedBaselineRecordId"],
            *(member[field] for field in MARKET_DECISION_MEMBER_KEYS),
        ]
    )


def _market_v2_row_digest(row: dict[str, object]) -> str:
    return market_digest([row[field] for field in MARKET_V2_ROW_KEYS[:-1]])


def _market_v2_scope_digest(rows: list[dict[str, object]]) -> str:
    tokens: list[object] = []
    ordered = sorted(
        rows,
        key=lambda row: (
            utf16_ordinal_key(require_string(row["sourceAuthority"], "V2 row asset")),
            utf16_ordinal_key(require_string(row["sourcePropertyPath"], "V2 row property")),
        ),
    )
    stored_fields = (
        "decisionBundleId",
        "decisionBundleDigest",
        "stableId",
        "authorityMetric",
        "sourceAuthority",
        "sourcePropertyPath",
        "exactBeforeValue",
        "exactAfterValue",
        "dependencyFingerprint",
        "sourceDigest",
        "semanticHash",
        "promotedAuthorityDependencyFingerprint",
        "promotedAuthoritySourceDigest",
        "promotedAuthoritySemanticHash",
        "decision",
        "replacementExactToken",
        "appliedApprovalKey",
        "decisionMemberDigest",
    )
    for row in ordered:
        tokens.append("\x1f".join(require_string(row[field], field, allow_empty=True) for field in stored_fields))
    return market_digest(tokens)


def _market_v2_asset_set_digest(
    assets: list[dict[str, object]],
    hash_field: str,
) -> str:
    tokens: list[object] = []
    for asset in sorted(
        assets,
        key=lambda row: utf16_ordinal_key(
            require_string(row["sourceAuthority"], "V2 asset path")
        ),
    ):
        tokens.extend((asset["sourceAuthority"], asset[hash_field]))
    return market_digest(tokens)


def _market_v2_receipt_digest(
    receipt: dict[str, object],
    patch_digest: str,
    assets: list[dict[str, object]],
    rows: list[dict[str, object]],
) -> str:
    tokens: list[object] = [
        receipt[field]
        for field in (
            "schemaVersion",
            "epochId",
            "decisionPayloadDigest",
            "decisionEpochDigest",
            "sourceLedgerDigest",
            "patchScopeDigest",
            "previousDecisionEpochDigest",
            "previousDecisionAuthorityDigest",
            "decisionAuthoritySha256Diagnostic",
            "approvalBeforeSha256",
            "approvalAfterSha256",
            "assetSetBeforeDigest",
            "assetSetAfterDigest",
            "receiptScopeDigest",
        )
    ]
    tokens.append(patch_digest)
    for asset in sorted(
        assets,
        key=lambda row: utf16_ordinal_key(
            require_string(row["sourceAuthority"], "V2 asset path")
        ),
    ):
        tokens.extend(
            (
                asset["sourceAuthority"],
                asset["assetBeforeSha256"],
                asset["assetAfterSha256"],
            )
        )
    for row in sorted(
        rows,
        key=lambda value: (
            utf16_ordinal_key(require_string(value["sourceAuthority"], "V2 row asset")),
            utf16_ordinal_key(require_string(value["sourcePropertyPath"], "V2 row property")),
        ),
    ):
        tokens.append(row["receiptRowDigest"])
    return market_digest(tokens)


def _verify_market_decision_authority(
    decision: object,
    manifest: dict[str, object],
) -> tuple[dict[str, object], dict[tuple[str, str], tuple[dict[str, object], dict[str, object]]]]:
    value = require_exact_object_keys(decision, MARKET_DECISION_KEYS, "market decision authority")
    if value["schemaVersion"] != "v27.market-review-decisions.3":
        fail(f"unexpected market decision schema: {value['schemaVersion']!r}")
    for field in ("decisionPayloadDigest", "decisionEpochDigest", "patchScopeDigest"):
        require_sha256_value(value[field], f"market decision {field}", case="upper")
    require_sha256_value(
        value["sourceLedgerDigest"], "market decision sourceLedgerDigest", case="lower"
    )
    # sourceLedgerDigest records the immutable ledger epoch that a reviewer
    # approved. It must not be rebound whenever unrelated production source
    # changes. Current-source safety is proven independently by the source
    # inventory, the current no-op receipt, and each decision member's exact
    # authority source/dependency/semantic hashes below.
    bundles_raw = require_exact_array(value["decisions"], "market decisions")
    if not bundles_raw:
        fail("market decision authority contains no bundles")
    bundles: list[dict[str, object]] = []
    member_index: dict[
        tuple[str, str], tuple[dict[str, object], dict[str, object]]
    ] = {}
    payload_tokens: list[object] = [
        "schema",
        "v27.market-review-decisions.3",
        "source-ledger",
        value["sourceLedgerDigest"],
        "patch-scope",
        value["patchScopeDigest"],
    ]
    for index, raw_bundle in enumerate(bundles_raw):
        bundle = require_exact_object_keys(
            raw_bundle, MARKET_DECISION_BUNDLE_KEYS, f"market decision bundle[{index}]"
        )
        bundles.append(bundle)
    expected_bundles = sorted(
        bundles,
        key=lambda row: utf16_ordinal_key(
            require_string(row["bundleId"], "market bundle ID")
        ),
    )
    if bundles != expected_bundles:
        fail("market decision bundles are not StringComparer.Ordinal sorted")
    for bundle_index, bundle in enumerate(bundles):
        for field in MARKET_DECISION_BUNDLE_KEYS[:-1]:
            require_string(bundle[field], f"market bundle[{bundle_index}].{field}")
        payload_tokens.extend(
            (
                "bundle",
                bundle["bundleId"],
                bundle["bundleDigest"],
                bundle["anchorItemId"],
                bundle["decisionReason"],
                bundle["reviewedBaselineRecordId"],
            )
        )
        members_raw = require_exact_array(
            bundle["members"], f"market bundle[{bundle_index}].members"
        )
        if not members_raw:
            fail(f"market bundle[{bundle_index}] contains no members")
        members = [
            require_exact_object_keys(
                raw,
                MARKET_DECISION_MEMBER_KEYS,
                f"market bundle[{bundle_index}].member[{member_index_value}]",
            )
            for member_index_value, raw in enumerate(members_raw)
        ]
        expected_members = sorted(
            members,
            key=lambda row: (
                utf16_ordinal_key(require_string(row["stableId"], "market member ID")),
                utf16_ordinal_key(require_string(row["authorityMetric"], "market member metric")),
            ),
        )
        if members != expected_members:
            fail(f"market bundle[{bundle_index}] members are not ordinal sorted")
        for member in members:
            for field in MARKET_DECISION_MEMBER_KEYS:
                require_string(
                    member[field],
                    f"market member {member.get('stableId')}.{field}",
                    allow_empty=field == "replacementExactToken",
                )
            identity = (str(member["stableId"]), str(member["authorityMetric"]))
            if identity in member_index:
                fail(f"duplicate market decision member identity: {identity}")
            member_index[identity] = (bundle, member)
            payload_tokens.append("member")
            payload_tokens.extend(member[field] for field in MARKET_DECISION_MEMBER_KEYS)
    if market_digest(payload_tokens) != value["decisionPayloadDigest"]:
        fail("market decision payload digest is stale")
    epoch_digest = market_digest(
        [
            "schema",
            value["schemaVersion"],
            "payload",
            value["decisionPayloadDigest"],
            "previous-epoch",
            value["previousDecisionEpochDigest"],
            "previous-authority",
            value["previousDecisionAuthorityDigest"],
        ]
    )
    if epoch_digest != value["decisionEpochDigest"]:
        fail("market decision epoch digest is stale")
    if value["epochId"] != "market-review-epoch:" + epoch_digest.lower():
        fail("market decision epoch ID is not derived from the epoch digest")
    return value, member_index


def _verify_market_v2_receipt(
    path: Path,
    decision: dict[str, object],
    decision_members: dict[
        tuple[str, str], tuple[dict[str, object], dict[str, object]]
    ],
) -> tuple[dict[str, object], list[dict[str, object]], list[dict[str, object]], list[dict[str, object]]]:
    keys = (
        "schemaVersion",
        "epochId",
        "decisionPayloadDigest",
        "decisionEpochDigest",
        "sourceLedgerDigest",
        "patchScopeDigest",
        "previousDecisionEpochDigest",
        "previousDecisionAuthorityDigest",
        "decisionAuthoritySha256Diagnostic",
        "approvalBeforeSha256",
        "approvalAfterSha256",
        "assetSetBeforeDigest",
        "assetSetAfterDigest",
        "receiptScopeDigest",
        "receiptDigest",
        "patchScopeRows",
        "assets",
        "receipts",
    )
    require_lf_json_file(path, "market V2 application receipt")
    receipt = require_exact_object_keys(
        load_json_strict(path), keys, "market V2 application receipt"
    )
    if receipt["schemaVersion"] != "v27.market-application-receipt.2":
        fail(f"unexpected market V2 receipt schema: {receipt['schemaVersion']!r}")
    decision_fields = (
        "epochId",
        "decisionPayloadDigest",
        "decisionEpochDigest",
        "sourceLedgerDigest",
        "patchScopeDigest",
        "previousDecisionEpochDigest",
        "previousDecisionAuthorityDigest",
    )
    for field in decision_fields:
        if receipt[field] != decision[field]:
            fail(f"market V2 receipt {field} differs from decision authority")
    for field in (
        "decisionAuthoritySha256Diagnostic",
        "approvalBeforeSha256",
        "approvalAfterSha256",
    ):
        require_sha256_value(receipt[field], f"market V2 receipt {field}", case="lower")
    for field in (
        "assetSetBeforeDigest",
        "assetSetAfterDigest",
        "receiptScopeDigest",
        "receiptDigest",
    ):
        require_sha256_value(receipt[field], f"market V2 receipt {field}", case="upper")
    decision_hash, _ = read_file_identity(MARKET_DECISION_PATH, "market decision authority")
    if receipt["decisionAuthoritySha256Diagnostic"] != decision_hash:
        fail("market V2 receipt decision authority diagnostic hash is stale")

    patch_rows = [
        require_exact_object_keys(raw, MARKET_PATCH_SCOPE_KEYS, f"V2 patch row[{index}]")
        for index, raw in enumerate(
            require_exact_array(receipt["patchScopeRows"], "V2 patch scope rows")
        )
    ]
    assets = [
        require_exact_object_keys(raw, MARKET_V2_ASSET_KEYS, f"V2 asset[{index}]")
        for index, raw in enumerate(require_exact_array(receipt["assets"], "V2 assets"))
    ]
    rows = [
        require_exact_object_keys(raw, MARKET_V2_ROW_KEYS, f"V2 receipt row[{index}]")
        for index, raw in enumerate(require_exact_array(receipt["receipts"], "V2 rows"))
    ]
    if not patch_rows or not assets or not rows:
        fail("market V2 receipt has an empty patch, asset, or property scope")

    expected_patch_order = sorted(
        patch_rows,
        key=lambda row: tuple(
            utf16_ordinal_key(require_string(row[field], f"V2 patch {field}"))
            for field in ("role", "stableId", "metric")
        ),
    )
    if patch_rows != expected_patch_order:
        fail("market V2 patch scope is not ordinal sorted")
    for row in patch_rows:
        for field in MARKET_PATCH_SCOPE_KEYS:
            require_string(row[field], f"V2 patch.{field}", allow_empty=False)
    patch_digest = _market_patch_scope_digest(patch_rows)
    if patch_digest != receipt["patchScopeDigest"]:
        fail("market V2 patch scope digest is stale")

    expected_assets = sorted(
        assets,
        key=lambda row: utf16_ordinal_key(
            require_string(row["sourceAuthority"], "V2 asset path")
        ),
    )
    if assets != expected_assets:
        fail("market V2 assets are not ordinal sorted")
    asset_paths: set[str] = set()
    asset_by_path: dict[str, dict[str, object]] = {}
    for asset in assets:
        raw_path = require_string(asset["sourceAuthority"], "V2 asset path")
        if raw_path in asset_paths:
            fail(f"duplicate market V2 asset: {raw_path}")
        asset_paths.add(raw_path)
        asset_by_path[raw_path] = asset
        asset_path = resolve_repository_file(raw_path, "V2 asset path", required_prefix="Assets/")
        before_hash = require_sha256_value(
            asset["assetBeforeSha256"], f"V2 asset before {raw_path}", case="lower"
        )
        after_hash = require_sha256_value(
            asset["assetAfterSha256"], f"V2 asset after {raw_path}", case="lower"
        )
        del before_hash
        actual_hash, _ = read_file_identity(asset_path, f"V2 asset {raw_path}")
        if actual_hash != after_hash:
            fail(f"market V2 asset after hash is stale: {raw_path}")
    if _market_v2_asset_set_digest(assets, "assetBeforeSha256") != receipt["assetSetBeforeDigest"]:
        fail("market V2 before asset-set digest is stale")
    if _market_v2_asset_set_digest(assets, "assetAfterSha256") != receipt["assetSetAfterDigest"]:
        fail("market V2 after asset-set digest is stale")

    expected_row_order = sorted(
        rows,
        key=lambda row: (
            utf16_ordinal_key(require_string(row["sourceAuthority"], "V2 row asset")),
            utf16_ordinal_key(require_string(row["sourcePropertyPath"], "V2 row property")),
        ),
    )
    if rows != expected_row_order:
        fail("market V2 receipt rows are not ordinal sorted")
    row_identities: set[tuple[str, str]] = set()
    for row in rows:
        for field in MARKET_V2_ROW_KEYS:
            require_string(
                row[field],
                f"V2 row {row.get('stableId')}.{field}",
                allow_empty=field == "replacementExactToken",
            )
        identity = (str(row["stableId"]), str(row["authorityMetric"]))
        if identity in row_identities:
            fail(f"duplicate market V2 receipt identity: {identity}")
        row_identities.add(identity)
        if identity not in decision_members:
            fail(f"market V2 row has no decision member: {identity}")
        bundle, member = decision_members[identity]
        expected_fields = {
            "decisionBundleId": bundle["bundleId"],
            "decisionBundleDigest": bundle["bundleDigest"],
            "stableId": member["stableId"],
            "authorityMetric": member["authorityMetric"],
            "sourceAuthority": member["sourceAuthority"],
            "sourcePropertyPath": member["sourcePropertyPath"],
            "exactBeforeValue": member["beforeExactToken"],
            "exactAfterValue": member["candidateExactToken"],
            "dependencyFingerprint": member["dependencyFingerprint"],
            "sourceDigest": member["sourceDigest"],
            "semanticHash": member["semanticHash"],
            "promotedAuthorityDependencyFingerprint": member[
                "promotedAuthorityDependencyFingerprint"
            ],
            "promotedAuthoritySourceDigest": member["promotedAuthoritySourceDigest"],
            "promotedAuthoritySemanticHash": member["promotedAuthoritySemanticHash"],
            "decision": member["decision"],
            "replacementExactToken": member["replacementExactToken"],
            "decisionMemberDigest": _market_decision_member_digest(bundle, member),
        }
        for field, expected in expected_fields.items():
            if row[field] != expected:
                fail(f"market V2 row {identity} field differs: {field}")
        asset = asset_by_path.get(str(row["sourceAuthority"]))
        if asset is None:
            fail(f"market V2 row asset is absent from asset scope: {identity}")
        if (
            row["assetBeforeSha256"] != asset["assetBeforeSha256"]
            or row["assetAfterSha256"] != asset["assetAfterSha256"]
        ):
            fail(f"market V2 row asset hashes differ: {identity}")
        if _market_v2_row_digest(row) != row["receiptRowDigest"]:
            fail(f"market V2 row digest is stale: {identity}")
        require_sha256_value(
            row["decisionMemberDigest"],
            f"market V2 decision-member digest {identity}",
            case="upper",
        )
        require_sha256_value(
            row["receiptRowDigest"],
            f"market V2 row digest {identity}",
            case="upper",
        )
        require_sha256_value(
            row["appliedApprovalKey"],
            f"market V2 approval key {identity}",
            case="lower",
        )
        # The V2 application receipt is immutable historical custody. Its
        # appliedApprovalKey identifies the exact approval used at application
        # time and is already covered by the row/aggregate receipt digests.
        # A later semantically unchanged approval refresh may legitimately
        # issue a new active key, so historical keys must not be required to
        # remain in the current approval file.
    expected_promoted = {
        identity
        for identity, (_, member) in decision_members.items()
        if member["decision"] == "promote-candidate"
    }
    if row_identities != expected_promoted:
        fail(
            "market V2 promoted member scope differs: "
            f"missing={sorted(expected_promoted - row_identities)} "
            f"extra={sorted(row_identities - expected_promoted)}"
        )
    scope_digest = _market_v2_scope_digest(rows)
    if scope_digest != receipt["receiptScopeDigest"]:
        fail("market V2 receipt scope digest is stale")
    if _market_v2_receipt_digest(receipt, patch_digest, assets, rows) != receipt["receiptDigest"]:
        fail("market V2 aggregate receipt digest is stale")
    return receipt, patch_rows, assets, rows


def verify_market_second_apply_noop_receipt(
    manifest: dict[str, object],
) -> dict[str, object]:
    top_keys = (
        "schemaVersion",
        "executionCommand",
        "executionBranch",
        "sourceDigest",
        "sourceInputCount",
        "sourcePathListDigest",
        "epochId",
        "decisionAuthoritySha256",
        "decisionPayloadDigest",
        "decisionEpochDigest",
        "sourceLedgerDigest",
        "patchScopeDigest",
        "v2ApplicationReceiptPath",
        "v2ApplicationReceiptSha256",
        "v2ApplicationReceiptByteLength",
        "v2ReceiptScopeDigest",
        "v2ReceiptDigest",
        "applicationInvocationOrdinal",
        "applicationInvocationCount",
        "approvedPatchCount",
        "applicationAssetCount",
        "v2AssetCount",
        "propertyCount",
        "targetFileCount",
        "differingPropertyCount",
        "runtimeByteDifferenceCount",
        "runtimeLengthDifferenceCount",
        "runtimeMtimeDifferenceCount",
        "assetSetDigest",
        "propertySetDigest",
        "receiptDigest",
        "assets",
        "properties",
        "files",
    )
    require_lf_json_file(
        MARKET_SECOND_APPLY_RECEIPT_PATH,
        "market second-apply no-op receipt",
    )
    receipt = require_exact_object_keys(
        load_json_strict(MARKET_SECOND_APPLY_RECEIPT_PATH),
        top_keys,
        "market second-apply no-op receipt",
    )
    expected_constants = {
        "schemaVersion": "v27.market-second-apply-noop.1",
        "executionCommand": "DungeonStory/V27/Apply Reviewed Market Promotions",
        "executionBranch": "already-applied-no-op",
        "applicationInvocationOrdinal": 2,
        "applicationInvocationCount": 1,
        "differingPropertyCount": 0,
        "runtimeByteDifferenceCount": 0,
        "runtimeLengthDifferenceCount": 0,
        "runtimeMtimeDifferenceCount": 0,
    }
    for field, expected in expected_constants.items():
        if receipt[field] != expected:
            fail(
                f"market second-apply receipt {field} differs: "
                f"expected={expected!r} actual={receipt[field]!r}"
            )
    for field in (
        "sourceDigest",
        "sourcePathListDigest",
        "decisionAuthoritySha256",
        "sourceLedgerDigest",
        "v2ApplicationReceiptSha256",
        "assetSetDigest",
        "receiptDigest",
    ):
        require_sha256_value(receipt[field], f"market no-op {field}", case="lower")
    for field in (
        "decisionPayloadDigest",
        "decisionEpochDigest",
        "patchScopeDigest",
        "v2ReceiptScopeDigest",
        "v2ReceiptDigest",
        "propertySetDigest",
    ):
        require_sha256_value(receipt[field], f"market no-op {field}", case="upper")
    source_digest, source_count, source_paths = current_source_snapshot()
    if (
        receipt["sourceDigest"] != source_digest
        or require_int(receipt["sourceInputCount"], "market source input count", minimum=1)
        != source_count
        or receipt["sourcePathListDigest"] != source_paths
    ):
        fail("market second-apply receipt current-source binding is stale")
    decision, decision_members = _verify_market_decision_authority(
        load_json_strict(MARKET_DECISION_PATH), manifest
    )
    decision_hash, _ = read_file_identity(MARKET_DECISION_PATH, "market decision authority")
    if receipt["decisionAuthoritySha256"] != decision_hash:
        fail("market second-apply decision authority hash is stale")
    for field in (
        "epochId",
        "decisionPayloadDigest",
        "decisionEpochDigest",
        "sourceLedgerDigest",
        "patchScopeDigest",
    ):
        if receipt[field] != decision[field]:
            fail(f"market second-apply {field} differs from decision authority")

    expected_v2_relative = (
        "Artifacts/QA/v27-balance-market-application-receipts/"
        + str(decision["decisionEpochDigest"]).lower()
        + ".json"
    )
    if receipt["v2ApplicationReceiptPath"] != expected_v2_relative:
        fail("market second-apply V2 receipt path is not epoch-derived")
    v2_path = resolve_repository_file(
        receipt["v2ApplicationReceiptPath"], "market V2 receipt path"
    )
    v2_hash, v2_length = read_file_identity(v2_path, "market V2 receipt")
    if (
        receipt["v2ApplicationReceiptSha256"] != v2_hash
        or require_int(
            receipt["v2ApplicationReceiptByteLength"],
            "market V2 receipt byte length",
            minimum=1,
        )
        != v2_length
    ):
        fail("market second-apply V2 receipt byte identity is stale")
    v2, patch_rows, v2_assets, _ = _verify_market_v2_receipt(
        v2_path, decision, decision_members
    )
    if (
        receipt["v2ReceiptScopeDigest"] != v2["receiptScopeDigest"]
        or receipt["v2ReceiptDigest"] != v2["receiptDigest"]
    ):
        fail("market second-apply V2 semantic receipt binding is stale")

    asset_keys = (
        "sourceAuthority",
        "expectedAfterSha256",
        "observedSha256",
        "byteLength",
    )
    property_keys = MARKET_PATCH_SCOPE_KEYS
    file_keys = ("path", "sha256", "byteLength")
    assets = [
        require_exact_object_keys(raw, asset_keys, f"market no-op asset[{index}]")
        for index, raw in enumerate(
            require_exact_array(receipt["assets"], "market no-op assets")
        )
    ]
    properties = [
        require_exact_object_keys(raw, property_keys, f"market no-op property[{index}]")
        for index, raw in enumerate(
            require_exact_array(receipt["properties"], "market no-op properties")
        )
    ]
    files = [
        require_exact_object_keys(raw, file_keys, f"market no-op file[{index}]")
        for index, raw in enumerate(
            require_exact_array(receipt["files"], "market no-op files")
        )
    ]
    count_fields = {
        "approvedPatchCount": len(properties),
        "v2AssetCount": len(assets),
        "propertyCount": len(properties),
        "targetFileCount": len(files),
        "applicationAssetCount": len(
            {str(row["sourceAuthority"]) for row in properties}
        ),
    }
    for field, expected in count_fields.items():
        if require_int(receipt[field], f"market no-op {field}") != expected:
            fail(f"market no-op {field} differs: expected={expected} actual={receipt[field]}")

    if assets != sorted(
        assets,
        key=lambda row: utf16_ordinal_key(
            require_string(row["sourceAuthority"], "market no-op asset path")
        ),
    ):
        fail("market no-op assets are not ordinal sorted")
    v2_asset_by_path = {str(row["sourceAuthority"]): row for row in v2_assets}
    if len(v2_asset_by_path) != len(v2_assets) or len(assets) != len(v2_assets):
        fail("market no-op asset scope denominator differs from V2")
    asset_tokens: list[object] = []
    for asset in assets:
        path_text = require_string(asset["sourceAuthority"], "market no-op asset path")
        v2_asset = v2_asset_by_path.get(path_text)
        if v2_asset is None:
            fail(f"market no-op asset is outside V2 scope: {path_text}")
        expected_hash = require_sha256_value(
            asset["expectedAfterSha256"], f"market no-op expected hash {path_text}"
        )
        observed_hash = require_sha256_value(
            asset["observedSha256"], f"market no-op observed hash {path_text}"
        )
        if expected_hash != v2_asset["assetAfterSha256"] or observed_hash != expected_hash:
            fail(f"market no-op asset does not match V2 after authority: {path_text}")
        path = resolve_repository_file(path_text, "market no-op asset", required_prefix="Assets/")
        actual_hash, actual_length = read_file_identity(path, f"market no-op asset {path_text}")
        if (
            actual_hash != observed_hash
            or require_int(asset["byteLength"], f"market no-op asset length {path_text}", minimum=1)
            != actual_length
        ):
            fail(f"market no-op asset byte identity is stale: {path_text}")
        asset_tokens.extend((path_text, expected_hash, observed_hash, str(actual_length)))
    if market_digest(asset_tokens).lower() != receipt["assetSetDigest"]:
        fail("market no-op asset-set digest is stale")

    expected_properties = sorted(
        properties,
        key=lambda row: tuple(
            utf16_ordinal_key(require_string(row[field], f"market property {field}"))
            for field in ("role", "stableId", "metric", "sourceAuthority", "sourcePropertyPath")
        ),
    )
    if properties != expected_properties:
        fail("market no-op properties are not ordinal sorted")
    if properties != sorted(
        patch_rows,
        key=lambda row: tuple(
            utf16_ordinal_key(require_string(row[field], f"V2 property {field}"))
            for field in ("role", "stableId", "metric", "sourceAuthority", "sourcePropertyPath")
        ),
    ):
        fail("market no-op property scope differs from V2 patch scope")
    property_identities = [
        (str(row["sourceAuthority"]), str(row["sourcePropertyPath"]))
        for row in properties
    ]
    if len(property_identities) != len(set(property_identities)):
        fail("market no-op property scope contains duplicate asset/property targets")
    if _market_patch_scope_digest(properties) != receipt["propertySetDigest"]:
        fail("market no-op property-set digest is stale")

    if files != sorted(
        files,
        key=lambda row: utf16_ordinal_key(require_string(row["path"], "market file path")),
    ):
        fail("market no-op file identities are not ordinal sorted")
    expected_file_paths = {
        str(asset["sourceAuthority"]) for asset in v2_assets
    } | {
        str(asset["sourceAuthority"]) + ".meta" for asset in v2_assets
    } | {
        expected_v2_relative,
        "docs/game-design/v27-balance-market-review-decisions.json",
        "docs/game-design/v27-balance-critical-approvals.json",
    }
    actual_file_paths = {str(row["path"]) for row in files}
    if len(actual_file_paths) != len(files) or actual_file_paths != expected_file_paths:
        fail(
            "market no-op target file set differs from V2 assets + metas + receipt: "
            f"missing={sorted(expected_file_paths - actual_file_paths)} "
            f"extra={sorted(actual_file_paths - expected_file_paths)}"
        )
    for file in files:
        raw_path = require_string(file["path"], "market no-op file path")
        path = resolve_repository_file(raw_path, "market no-op file path")
        actual_hash, actual_length = read_file_identity(path, f"market no-op file {raw_path}")
        if (
            file["sha256"] != actual_hash
            or require_int(file["byteLength"], f"market no-op file length {raw_path}", minimum=1)
            != actual_length
        ):
            fail(f"market no-op file byte identity is stale: {raw_path}")

    digest_tokens: list[object] = [
        receipt[field]
        for field in (
            "schemaVersion",
            "executionCommand",
            "executionBranch",
            "sourceDigest",
            "sourceInputCount",
            "sourcePathListDigest",
            "epochId",
            "decisionAuthoritySha256",
            "decisionPayloadDigest",
            "decisionEpochDigest",
            "sourceLedgerDigest",
            "patchScopeDigest",
            "v2ApplicationReceiptPath",
            "v2ApplicationReceiptSha256",
            "v2ApplicationReceiptByteLength",
            "v2ReceiptScopeDigest",
            "v2ReceiptDigest",
            "applicationInvocationOrdinal",
            "applicationInvocationCount",
            "approvedPatchCount",
            "applicationAssetCount",
            "v2AssetCount",
            "propertyCount",
            "targetFileCount",
            "differingPropertyCount",
            "runtimeByteDifferenceCount",
            "runtimeLengthDifferenceCount",
            "runtimeMtimeDifferenceCount",
            "assetSetDigest",
            "propertySetDigest",
        )
    ]
    for asset in assets:
        digest_tokens.extend(
            (
                asset["sourceAuthority"],
                asset["expectedAfterSha256"],
                asset["observedSha256"],
                asset["byteLength"],
            )
        )
    for property_row in properties:
        digest_tokens.extend(property_row[field] for field in MARKET_PATCH_SCOPE_KEYS)
    for file in files:
        digest_tokens.extend((file["path"], file["sha256"], file["byteLength"]))
    if market_digest(digest_tokens).lower() != receipt["receiptDigest"]:
        fail("market second-apply receipt self-digest is stale")
    return receipt


AUDIT_NOOP_KEYS = (
    "schemaVersion",
    "executionCommand",
    "executionBranch",
    "currentSourceDigest",
    "currentSourceInputCount",
    "currentSourcePathDigest",
    "gameplaySceneSha256",
    "generatorVersion",
    "ledgerSourceDigest",
    "ledgerSourceCount",
    "approvalDigest",
    "assetPatchDigest",
    "marketSecondApplyReceiptPath",
    "marketSecondApplyReceiptSha256",
    "marketSecondApplyReceiptLength",
    "marketSecondApplySemanticDigest",
    "rowCount",
    "criticalCount",
    "collapsedCriticalCount",
    "approvedCount",
    "sccCount",
    "integrityFailureCount",
    "firstSemanticDigest",
    "secondSemanticDigest",
    "firstWriterInvocationCount",
    "secondWriterInvocationCount",
    "secondChangedCount",
    "byteDiffCount",
    "lengthDiffCount",
    "runtimeMtimeDiffCount",
    "artifactSetDigest",
    "files",
    "executionEpochDigest",
    "receiptDigest",
)
AUDIT_NOOP_FILE_KEYS = ("path", "sha256", "length", "secondWriteChanged")
AUDIT_NOOP_ARTIFACT_PATHS = (
    "Artifacts/QA/v27-balance-before-after.csv",
    "docs/generated/V27_Balance_Before_After.md",
    "Artifacts/QA/v27-balance-anomaly-graph.json",
    "Artifacts/QA/v27-balance-recalibration-audit.txt",
    "Artifacts/QA/v27-balance-source-inventory.json",
    "Artifacts/QA/v27-balance-artifact-manifest.json",
)


def _audit_noop_receipt_tokens(
    receipt: dict[str, object],
    files: list[dict[str, object]],
) -> list[object]:
    tokens: list[object] = [
        receipt[field]
        for field in (
            "schemaVersion",
            "executionCommand",
            "executionBranch",
            "currentSourceDigest",
            "currentSourceInputCount",
            "currentSourcePathDigest",
            "gameplaySceneSha256",
            "generatorVersion",
            "ledgerSourceDigest",
            "ledgerSourceCount",
            "approvalDigest",
            "assetPatchDigest",
            "marketSecondApplyReceiptPath",
            "marketSecondApplyReceiptSha256",
            "marketSecondApplyReceiptLength",
            "marketSecondApplySemanticDigest",
            "rowCount",
            "criticalCount",
            "collapsedCriticalCount",
            "approvedCount",
            "sccCount",
            "integrityFailureCount",
            "firstSemanticDigest",
            "secondSemanticDigest",
            "firstWriterInvocationCount",
            "secondWriterInvocationCount",
            "secondChangedCount",
            "byteDiffCount",
            "lengthDiffCount",
            "runtimeMtimeDiffCount",
            "artifactSetDigest",
        )
    ]
    tokens.append(len(files))
    for file in files:
        tokens.extend(
            (
                file["path"],
                file["sha256"],
                file["length"],
                file["secondWriteChanged"],
            )
        )
    return tokens


def verify_audit_second_generation_noop_receipt(
    manifest: dict[str, object],
    market_receipt: dict[str, object],
) -> None:
    require_lf_json_file(
        AUDIT_SECOND_GENERATION_RECEIPT_PATH,
        "AuditOnly second-generation no-op receipt",
    )
    receipt = require_exact_object_keys(
        load_json_strict(AUDIT_SECOND_GENERATION_RECEIPT_PATH),
        AUDIT_NOOP_KEYS,
        "AuditOnly second-generation no-op receipt",
    )
    expected_constants = {
        "schemaVersion": "v27.audit-second-generation-noop.1",
        "executionCommand": "DungeonStory/V27/Generate Audit-Only Twice And Verify No-Op",
        "executionBranch": "audit-only-second-generation",
        "firstWriterInvocationCount": 6,
        "secondWriterInvocationCount": 6,
        "secondChangedCount": 0,
        "byteDiffCount": 0,
        "lengthDiffCount": 0,
        "runtimeMtimeDiffCount": 0,
        "criticalCount": 0,
        "integrityFailureCount": 0,
    }
    for field, expected in expected_constants.items():
        if receipt[field] != expected:
            fail(
                f"AuditOnly no-op receipt {field} differs: "
                f"expected={expected!r} actual={receipt[field]!r}"
            )

    current_source, source_count, path_digest = current_source_snapshot()
    if (
        receipt["currentSourceDigest"] != current_source
        or require_int(
            receipt["currentSourceInputCount"],
            "AuditOnly currentSourceInputCount",
            minimum=1,
        )
        != source_count
        or receipt["currentSourcePathDigest"] != path_digest
    ):
        fail("AuditOnly no-op receipt current-source binding is stale")
    scene_hash, _ = read_file_identity(
        ROOT / "Assets" / "Scenes" / "GameplayScene.unity",
        "official GameplayScene",
    )
    official_scene = "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40"
    if scene_hash != official_scene or receipt["gameplaySceneSha256"] != scene_hash:
        fail("AuditOnly no-op receipt official GameplayScene binding is stale")

    manifest_fields = {
        "generatorVersion": "generatorVersion",
        "ledgerSourceDigest": "sourceDigest",
        "ledgerSourceCount": "sourceCount",
        "approvalDigest": "approvalDigest",
        "assetPatchDigest": "assetPatchDigest",
        "rowCount": "rowCount",
        "criticalCount": "criticalCount",
        "collapsedCriticalCount": "collapsedCriticalCount",
        "approvedCount": "approvedCount",
        "sccCount": "sccCount",
        "integrityFailureCount": "integrityFailureCount",
    }
    for receipt_field, manifest_field in manifest_fields.items():
        if receipt[receipt_field] != manifest.get(manifest_field):
            fail(
                f"AuditOnly no-op {receipt_field} differs from manifest "
                f"{manifest_field}"
            )
    if require_int(receipt["approvedCount"], "AuditOnly approvedCount", minimum=1) <= 0:
        fail("AuditOnly no-op receipt has no approved records")
    for field in (
        "currentSourceDigest",
        "currentSourcePathDigest",
        "gameplaySceneSha256",
        "ledgerSourceDigest",
        "approvalDigest",
        "assetPatchDigest",
        "marketSecondApplyReceiptSha256",
        "marketSecondApplySemanticDigest",
        "firstSemanticDigest",
        "secondSemanticDigest",
        "artifactSetDigest",
        "executionEpochDigest",
        "receiptDigest",
    ):
        require_sha256_value(receipt[field], f"AuditOnly no-op {field}", case="lower")
    approval_path = ROOT / "docs" / "game-design" / "v27-balance-critical-approvals.json"
    approval_hash, _ = read_file_identity(approval_path, "V27 approval authority")
    if receipt["approvalDigest"] != approval_hash:
        fail("AuditOnly no-op approval digest is stale")

    market_path_expected = MARKET_SECOND_APPLY_RECEIPT_PATH.relative_to(ROOT).as_posix()
    if receipt["marketSecondApplyReceiptPath"] != market_path_expected:
        fail("AuditOnly no-op market receipt path differs from the fixed authority")
    market_hash, market_length = read_file_identity(
        MARKET_SECOND_APPLY_RECEIPT_PATH, "market second-apply receipt"
    )
    if (
        receipt["marketSecondApplyReceiptSha256"] != market_hash
        or require_int(
            receipt["marketSecondApplyReceiptLength"],
            "AuditOnly market receipt length",
            minimum=1,
        )
        != market_length
        or receipt["marketSecondApplySemanticDigest"]
        != require_sha256_value(
            market_receipt["receiptDigest"], "market second-apply receipt digest"
        )
    ):
        fail("AuditOnly no-op receipt is not bound to the current market receipt")
    if receipt["currentSourceDigest"] != market_receipt["sourceDigest"]:
        fail("market and AuditOnly no-op receipts belong to different source snapshots")

    files = [
        require_exact_object_keys(raw, AUDIT_NOOP_FILE_KEYS, f"AuditOnly file[{index}]")
        for index, raw in enumerate(
            require_exact_array(receipt["files"], "AuditOnly no-op files")
        )
    ]
    if tuple(str(file["path"]) for file in files) != AUDIT_NOOP_ARTIFACT_PATHS:
        fail("AuditOnly no-op artifact path/order denominator differs from six writers")
    identity_tokens: list[object] = [len(files)]
    artifact_tokens: list[object] = [len(files)]
    for file in files:
        raw_path = require_string(file["path"], "AuditOnly artifact path")
        if require_bool(file["secondWriteChanged"], f"{raw_path}.secondWriteChanged"):
            fail(f"AuditOnly second writer changed artifact: {raw_path}")
        path = resolve_repository_file(raw_path, "AuditOnly artifact path")
        actual_hash, actual_length = read_file_identity(path, f"AuditOnly artifact {raw_path}")
        expected_hash = require_sha256_value(
            file["sha256"], f"AuditOnly artifact hash {raw_path}"
        )
        expected_length = require_int(
            file["length"], f"AuditOnly artifact length {raw_path}", minimum=1
        )
        if expected_hash != actual_hash or expected_length != actual_length:
            fail(f"AuditOnly artifact byte identity is stale: {raw_path}")
        identity_tokens.extend((raw_path, expected_hash, expected_length))
        artifact_tokens.extend((raw_path, expected_hash, expected_length, False))
    if len(files) != 6 or semantic_digest(artifact_tokens) != receipt["artifactSetDigest"]:
        fail("AuditOnly artifact-set digest is stale")

    current_semantic = semantic_digest(identity_tokens)
    if (
        receipt["firstSemanticDigest"] != current_semantic
        or receipt["secondSemanticDigest"] != current_semantic
    ):
        fail("AuditOnly first/second semantic digest differs from current ledger")
    receipt_tokens = _audit_noop_receipt_tokens(receipt, files)
    execution_epoch = semantic_digest(receipt_tokens)
    if execution_epoch != receipt["executionEpochDigest"]:
        fail("AuditOnly no-op execution epoch digest is stale")
    expected_receipt_digest = semantic_digest(
        ["v27-audit-second-generation-noop-receipt", *receipt_tokens, execution_epoch]
    )
    if expected_receipt_digest != receipt["receiptDigest"]:
        fail("AuditOnly no-op receipt self-digest is stale")


def main() -> int:
    manifest = load_json_strict(MANIFEST_PATH)
    if not isinstance(manifest, dict):
        fail("V27 artifact manifest root must be an object")
    if manifest.get("schemaVersion") != "v27.1":
        fail(f"unexpected manifest schema: {manifest.get('schemaVersion')}")
    if int(manifest.get("criticalCount", -1)) != 0:
        fail("unresolved V27 Critical nodes remain")
    if int(manifest.get("integrityFailureCount", -1)) != 0:
        fail("V27 manifest records integrity failures")
    if int(manifest.get("approvedCount", 0)) <= 0:
        fail("V27 manifest has no exact approvals")

    hashes = {
        "sourceInventoryByteHash": "Artifacts/QA/v27-balance-source-inventory.json",
        "csvByteHash": "Artifacts/QA/v27-balance-before-after.csv",
        "markdownByteHash": "docs/generated/V27_Balance_Before_After.md",
        "auditByteHash": "Artifacts/QA/v27-balance-recalibration-audit.txt",
        "anomalyGraphByteHash": "Artifacts/QA/v27-balance-anomaly-graph.json",
        "economy256EvidenceHash": "Artifacts/QA/v27-balance-economy-256-seed.txt",
        "verticalSliceFullLoopEvidenceHash": "Artifacts/QA/v27-balance-vertical-slice-full-loop-playmode.txt",
        "assetRollbackEvidenceHash": "Artifacts/QA/v27-balance-asset-rollback.txt",
        "marketAuthorityEvidenceHash": "Artifacts/QA/v27-balance-market-authority.txt",
        "laborFacilityAuthorityEvidenceHash": "Artifacts/QA/v27-balance-labor-facility-authority.txt",
        "combatOutcome1000SeedEvidenceHash": "Artifacts/QA/combat-balance-final.txt",
        "wholeGameCoverageEvidenceHash": "Artifacts/QA/v27-balance-whole-game-coverage.txt",
        "laborAuthorityMatrixEvidenceHash": "Artifacts/QA/v27-labor-authority-matrix.txt",
        "ledgerContractEvidenceHash": "Artifacts/QA/v27-balance-ledger-contracts.txt",
        "equipmentReadinessEvidenceHash": "Artifacts/QA/v26-equipment-readiness-throughput.md",
        "dailyRoutine157181EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157181.txt",
        "dailyRoutine157182EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157182.txt",
        "dailyRoutine157183EvidenceHash": "Artifacts/QA/phase157-daily-routine-wu-seed-157183.txt",
        "finalAcceptanceEvidenceHash": "Artifacts/QA/final-acceptance-report.txt",
        "approvalDigest": "docs/game-design/v27-balance-critical-approvals.json",
        "analyzerDllHash": "Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll",
    }
    for key, path in hashes.items():
        require_hash(manifest, key, path)

    analyzer_source = ROOT / "tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs"
    expected_analyzer_source = str(manifest.get("analyzerSourceHash", "")).lower()
    actual_analyzer_source = canonical_source_sha256(analyzer_source)
    if not expected_analyzer_source or actual_analyzer_source != expected_analyzer_source:
        fail(
            "analyzerSourceHash mismatch for canonical analyzer source: "
            f"expected={expected_analyzer_source} actual={actual_analyzer_source}"
        )

    verify_source_inventory(manifest)
    baseline_ids = verify_csv(manifest)
    manifest_baselines = set(manifest.get("balanceBaselineRecordIds", []))
    if not baseline_ids.issubset(manifest_baselines):
        fail(f"CSV baseline ids are absent from manifest: {sorted(baseline_ids - manifest_baselines)}")
    baseline_text = (ROOT / "docs/game-design/whole-game-balance-baseline.md").read_text(
        encoding="utf-8"
    )
    missing_baselines = sorted(value for value in manifest_baselines if value not in baseline_text)
    if missing_baselines:
        fail(f"manifest baseline records are missing from the authority document: {missing_baselines}")
    verify_approvals(manifest)
    market_noop_receipt = verify_market_second_apply_noop_receipt(manifest)
    verify_audit_second_generation_noop_receipt(
        manifest,
        market_noop_receipt,
    )
    verify_combat()
    verify_daily_routine()
    verify_physical_mass_coupling()
    # Batch A/B/C and the former F/G parent reports are historical checkpoint
    # receipts. Their covered production contracts are now checked by the
    # current ledger, physical-mass coupling, focused Unity gates, and the
    # deterministic no-op receipts above. Do not make an obsolete milestone's
    # global source digest a permanent merge blocker.
    verify_natural_output_clearance_portfolio()
    verify_output_clearance_profiles()
    require_text(
        QA / "v27-balance-ledger-contracts.txt",
        (
            "RESULT=PASS; checks=13",
            "PASS V27_MEWU_ASYMMETRIC_QUANTIZATION",
            "PASS V27_ATTRIBUTION_COLLAPSE_EPSILON_ISOLATED",
            "PASS V27_SCC_ZERO_TOLERANCE",
            "PASS V27_CSV_RFC4180_ESCAPE",
            "PASS V27_STABLE_SORT_P95_2MS_ZERO_ALLOC",
            "PASS V27_CSV_ESCAPE_P95_2MS_ZERO_ALLOC",
        ),
    )
    require_text(
        QA / "v27-balance-asset-rollback.txt",
        (
            "RESULT=PASS; failures=0",
            "PASS V27_ASSET_ROLLBACK_YAML_BYTE_EXACT",
            "PASS V27_ASSET_ROLLBACK_META_GUID_FILEID_EXACT",
        ),
    )
    require_text(
        QA / "v27-balance-market-authority.txt",
        ("RESULT=PASS; failures=0", "MARKET_SALE_OUTPUT_FLOOR_EXACT"),
    )
    verify_labor_facility_report()
    require_text(
        QA / "v27-labor-authority-matrix.txt",
        ("RESULT=PASS; cells=360", "PASS V27_LABOR_MATRIX_360_CELLS count=360"),
    )
    require_text(
        QA / "v27-balance-vertical-slice-full-loop-playmode.txt",
        (
            "RESULT=PASS; checks=11; failures=0",
            "PASS V27_SLICE_REBUILD_COMPLETED",
            "PASS V27_SLICE_CONSOLE_ZERO warnings=0; errors=0",
        ),
    )
    require_text(
        QA / "v26-equipment-readiness-throughput.md",
        ("## Checkpoint throughput", "| 960 | 48h |", "| PASS |"),
    )
    require_text(
        QA / "final-acceptance-report.txt",
        ("ExpectedSteps: 33", "ActualSteps: 33", "Passed: 33", "Failed: 0"),
    )
    require_text(
        QA / "v27-balance-economy-256-seed.txt",
        (
            "RESULT=PASS; seeds=256; failures=0",
            f"rows={manifest['rowCount']}; critical=0; scc={manifest['sccCount']}",
        ),
    )
    require_text(
        QA / "v27-balance-whole-game-coverage.txt",
        ("RESULT=PASS", "producerOrphans=0", "consumerOrphans=0", "approvedUnapplied=0"),
    )

    print(
        "RESULT=PASS; "
        f"rows={manifest['rowCount']}; critical=0; scc={manifest['sccCount']}; "
        "combat=36x1000; dailySeeds=3; finalAcceptance=33/33"
    )
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:  # CI needs one stable fail-loud boundary.
        print(f"RESULT=FAIL; {error}", file=sys.stderr)
        sys.exit(1)
