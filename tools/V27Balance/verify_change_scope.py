"""PR gates: archived V27 bytes, changed-domain contracts, and current Unity input binding.

This is not full balance certification. That remains verify_committed_artifacts.py.
Unknown Unity changes select every short contract, never an empty test set.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unicodedata

import verify_committed_artifacts as ledger

ROOT = ledger.ROOT
EVIDENCE = "Artifacts/QA/ImpactCI/verification.json"
GROUPS = {
    "core": "V27CiEvidenceBatch.RunImpactCoreContracts",
    "character": "V27CiEvidenceBatch.RunImpactCharacterContracts",
    "effects": "V27CiEvidenceBatch.RunImpactEffectContracts",
    "crop": "V27CiEvidenceBatch.RunImpactCrops",
    "world": "V27CiEvidenceBatch.RunImpactWorldRestore",
}
# All three executions are required when the effects group is partitioned.
# They invoke the same bodies as RunImpactEffectContracts, under unchanged guards.
EFFECT_PARTS = {
    "effects-definitions": "V27CiEvidenceBatch.RunImpactEffectDefinitionContracts",
    "effects-recovery": "V27CiEvidenceBatch.RunImpactEffectRecoveryContracts",
    "effects-traits": "V27CiEvidenceBatch.RunImpactAcquiredTraitContracts",
}
# Ordered ownership rules. Shared/unknown dependencies deliberately take all groups.
DOMAIN_RULES = (
    ("Assets/Scripts/Content/", {"effects"}),
    ("Assets/Scripts/Services/Character/Core/CharacterAcquiredTrait", {"effects", "character"}),
    ("Assets/Scripts/Services/Character/", {"character", "effects"}),
    ("Assets/Scripts/Services/Infrastructure/Environment/", {"core", "crop"}),
    ("Assets/Scripts/Services/Economy/", {"core", "crop"}),
    ("Assets/Scripts/Services/Items/", {"core", "crop"}),
    ("Assets/Scripts/Services/Survival/", {"core", "character"}),
    ("Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/", {"core", "crop"}),
)
PRODUCERS = {
    "Assets/Scripts/Services/Economy/Editor/V27CiEvidenceBatch.cs",
    "Assets/Scripts/Services/Infrastructure/Editor/DungeonFullWorldRoundTripPlayModeFacade.cs",
}


def changed_paths(base: str, head: str = "HEAD") -> list[str]:
    # No rename detection: both old deletion and new addition affect the selection.
    ancestor = subprocess.check_output(["git", "merge-base", base, head], cwd=ROOT).decode().strip()
    raw = subprocess.check_output(
        ["git", "diff", "--no-renames", "--name-only", "-z", ancestor, head, "--"], cwd=ROOT)
    return [value.decode("utf-8", errors="strict") for value in raw.split(b"\0") if value]


def required_groups(paths: list[str]) -> set[str]:
    selected: set[str] = set()
    for path in paths:
        if path == ".github/workflows/v27-ledger-integrity.yml" or (
            path.startswith("tools/V27Balance/") and Path(path).suffix in {".py", ".ps1", ".json", ".yml"}
        ):
            return set(GROUPS)
        unity = path.startswith(("Assets/", "Packages/", "ProjectSettings/"))
        if not unity:
            continue
        if path in PRODUCERS or path.endswith((".asmdef", ".asmref", ".rsp", ".asmdef.meta", ".asmref.meta", ".rsp.meta")):
            return set(GROUPS)
        # Every Unity change retains actual world restore plus exactly-once contracts.
        selected.update(("core", "world"))
        for prefix, groups in DOMAIN_RULES:
            if path.startswith(prefix):
                selected.update(groups)
                break
        else:
            return set(GROUPS)
    return selected


BALANCE_AUTHORITY = "docs/game-design/whole-game-balance-baseline.md"


def only_adds_balance_records(before: str, after: str) -> bool:
    """A new record cannot rewrite, remove or shadow historical authority text."""
    title, separator, previous_records = before.partition("\n\n")
    if (not separator or not title.startswith("# ") or not previous_records
            or len(after) <= len(before) or not after.startswith(title + separator)
            or not after.endswith(previous_records)):
        return False
    added = after[len(title + separator):-len(previous_records)]
    if not added.startswith("## ") or not added.endswith("\n\n"):
        return False
    record_pattern = r"`(balance:[a-z0-9][a-z0-9:._-]*)`"
    identities = re.findall(record_pattern, added)
    if not identities or len(identities) != len(set(identities)):
        return False
    # Also reject a reused key written without backticks in the old document.
    return all(identity not in before for identity in identities)


def archive_changed(paths: list[str], base: str | None = None, head: str = "HEAD") -> bool:
    exact = set(ledger.MANIFEST_ARTIFACT_HASHES.values()) | {
        "Artifacts/QA/v27-balance-artifact-manifest.json",
        "docs/game-design/v27-balance-market-review-decisions.json",
        BALANCE_AUTHORITY,
        "docs/implementation-reports/world-resource-runtime-latest.txt",
        "docs/implementation-reports/crop-plot-runtime-latest.txt",
    }
    for path in paths:
        if path == BALANCE_AUTHORITY and base is not None:
            ancestor = subprocess.check_output(
                ["git", "merge-base", base, head], cwd=ROOT).decode().strip()
            before, after = (
                subprocess.check_output(["git", "show", f"{ref}:{path}"], cwd=ROOT)
                .decode("utf-8", errors="strict")
                for ref in (ancestor, head)
            )
            if only_adds_balance_records(before, after):
                continue
        if path in exact or path.startswith((
                "Artifacts/QA/v27-", "Artifacts/QA/combat-balance-final/")):
            return True
    return False


def configuration_snapshot(root: Path = ROOT) -> str:
    """All Unity input files, including runtime JSON and binary assets; no extension omissions."""
    captured: dict[str, Path] = {}
    for folder in ("Assets", "Packages", "ProjectSettings"):
        if not (root / folder).is_dir():
            ledger.fail(f"Unity configuration root missing: {folder}")
        for path in (root / folder).rglob("*"):
            if path.is_symlink():
                ledger.fail(f"Unity configuration symlink: {path}")
            if not path.is_file():
                continue
            relative = unicodedata.normalize("NFC", path.relative_to(root).as_posix())
            if relative in captured:
                ledger.fail(f"duplicate Unity configuration path: {relative}")
            captured[relative] = path
    digest = hashlib.sha256()
    for relative in sorted(captured, key=lambda value: value.encode("utf-8")):
        path = captured[relative]
        raw = path.read_bytes()
        # Git's platform newline conversion also applies to extensionless LICENSE
        # and differently-cased text extensions. Binary/opaque .bytes stay exact.
        if path.suffix != ".bytes" and b"\0" not in raw:
            try:
                text = raw.decode("utf-8-sig", errors="strict")
            except UnicodeDecodeError:
                pass  # Binary identity is the original bytes, not replacement text.
            else:
                raw = text.replace("\r\n", "\n").replace("\r", "\n").encode("utf-8")
        digest.update(f"{relative}\n{hashlib.sha256(raw).hexdigest()}\n".encode("utf-8"))
    return digest.hexdigest()


def verify_execution(check: dict, source: str, config: str, root: Path = ROOT) -> str:
    group = check.get("group")
    if group not in GROUPS or check.get("method") != GROUPS[group]:
        ledger.fail("unknown or mismatched targeted execution method")
    if "parts" not in check:
        return _verify_execution_record(check, source, config, root, GROUPS)
    parts = check["parts"]
    if (group != "effects" or not isinstance(parts, list) or len(parts) != len(EFFECT_PARTS)
            or {"log", "summary", "logSha256", "summarySha256"}.intersection(check)):
        ledger.fail("partitioned effects require exactly the three complete executions")
    observed = set()
    for part in parts:
        if not isinstance(part, dict) or "parts" in part:
            ledger.fail("invalid or nested effects execution part")
        current = _verify_execution_record(part, source, config, root, EFFECT_PARTS)
        if current in observed:
            ledger.fail("duplicate effects execution part")
        observed.add(current)
    if observed != set(EFFECT_PARTS):
        ledger.fail("missing required effects execution part")
    return group


def _verify_execution_record(check: dict, source: str, config: str, root: Path,
                             methods: dict[str, str]) -> str:
    group = check.get("group")
    if group not in methods or check.get("method") != methods[group]:
        ledger.fail("unknown or mismatched targeted execution method")
    loaded = {}
    for field in ("log", "summary"):
        relative = check.get(field, "")
        path = root / relative
        if (not relative.startswith("Artifacts/QA/ImpactCI/") or ".." in Path(relative).parts
                or not path.is_file() or path.is_symlink()):
            ledger.fail(f"missing/unsafe execution {field}: {relative}")
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != check.get(field + "Sha256"):
            ledger.fail(f"execution {field} bytes differ: {group}")
        loaded[field] = raw.decode("utf-8", errors="strict")
    summary = json.loads(loaded["summary"], object_pairs_hook=ledger._reject_duplicate_json_keys)
    if (not {"exit_code", "stop_reason", "compiler_error_count", "source_drift", "seconds"} <= summary.keys()
            or type(summary.get("exit_code")) is not int or summary.get("exit_code") != 0
            or summary.get("stop_reason") is not None
            or type(summary.get("compiler_error_count")) is not int or summary.get("compiler_error_count") != 0
            or summary.get("source_drift") != [] or summary.get("seconds", 0) <= 0):
        ledger.fail(f"native execution/compile/cleanup failed: {group}")
    if re.search(r"\berror CS\d+:|\b(?:[\w.]*Exception):|UnityEngine\.Debug:Log(?:Error|Exception|Assertion)|Aborting batchmode|Assertion failed", loaded["log"]):
        ledger.fail(f"execution log contains a compiler/console failure: {group}")
    marker = f"IMPACT_CI_PASS group={group};source={source};config={config};consoleErrors=0"
    # Exact whole-line marker is emitted only after the real scenarios and cleanup.
    if loaded["log"].splitlines().count(marker) != 1:
        ledger.fail(f"current-input clean completion marker missing/duplicate: {group}")
    return group


def verify_receipt(required: set[str]) -> None:
    receipt = ledger.load_json_strict(ROOT / EVIDENCE)
    if receipt.get("schema") != "impact-ci-v1" or receipt.get("scope") != "targeted-contracts-not-full-balance-certification":
        ledger.fail("targeted execution receipt schema/scope differs")
    source = ledger.current_source_snapshot()
    if receipt.get("source") != dict(digest=source[0], count=source[1], pathsDigest=source[2]):
        ledger.fail("targeted execution receipt is not bound to current compiled sources")
    config = configuration_snapshot()
    if receipt.get("configurationDigest") != config:
        ledger.fail("targeted execution receipt has stale Unity configuration")
    observed = set()
    for check in receipt.get("checks", []):
        group = verify_execution(check, source[0], config)
        if group in observed:
            ledger.fail(f"duplicate targeted execution group: {group}")
        observed.add(group)
    if not required <= observed:
        ledger.fail(f"missing affected-domain executions: {sorted(required - observed)}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--plan-only", action="store_true")
    args = parser.parse_args()
    paths = changed_paths(args.base, args.head)
    groups = required_groups(paths)
    full = archive_changed(paths, args.base, args.head)
    print(json.dumps(dict(changedFiles=len(paths), requiredGroups=sorted(groups),
                          fullBalanceCertification=full), ensure_ascii=False))
    if args.plan_only:
        return
    # Unchanged archive bytes are historical evidence, never current-source PASS.
    ledger.verify_manifest_artifact_bytes(ledger.load_json_strict(ledger.MANIFEST_PATH))
    print("ARCHIVED_V27_BYTES=PASS; historical record, not current runtime certification")
    if full:
        ledger.main()
    if groups:
        verify_receipt(groups)
        print("IMPACT_EXECUTION=PASS; current compilation and selected short contracts")
    else:
        print("IMPACT_EXECUTION=NOT_REQUIRED; no Unity/policy inputs changed")


if __name__ == "__main__":
    main()
