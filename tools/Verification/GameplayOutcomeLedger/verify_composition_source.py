#!/usr/bin/env python3
"""Static fail-closed supplement for Phase80 VContainer composition.

This does not replace a compiled container build.  It catches the specific
source-level failure where a domain registration extension exists but is never
called, and checks that the shared recorder/query/presentation/evidence services
are present exactly once in the registration source set.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


METHOD = re.compile(
    r"\b(?:public|internal)\s+static\s+void\s+"
    r"(?P<name>Register[A-Za-z0-9_]*GameplayOutcomeBridges)\s*\("
)
CALL = re.compile(r"\b(?P<name>Register[A-Za-z0-9_]*GameplayOutcomeBridges)\s*\(")
REGISTER_AS = re.compile(
    r"builder\s*\.\s*Register\s*<\s*(?P<concrete>[A-Za-z_][A-Za-z0-9_.]*)\s*>"
    r"\s*\([^;]*?\)\s*\.\s*As\s*<\s*(?P<service>"
    r"IGameplayOutcomeAdapterRegistration|IGameplayOutcomeDescriptor)\s*>\s*\(\s*\)",
    re.S,
)
CORE_LITERAL_OUTCOME = re.compile(
    r'\bnew\s+GameplayOutcomeTypeId\s*\(\s*"[^"\r\n]+"\s*\)'
)
CORE_OUTCOME_SWITCH = re.compile(
    r'\bswitch\s*\([^)]*\b(?:OutcomeType|OutcomeTypeId)\b[^)]*\)', re.S
)

REQUIRED_SERVICE_REGISTRATIONS = {
    "IGameplayOutcomeRegistry": 1,
    "IGameplayOutcomeQuery": 1,
    "IGameplayOutcomeMemoryCommands": 1,
    "IGameplayOutcomeConsolidationService": 1,
    "IGameplayOutcomeDiagnosticsQuery": 1,
    "IGameplayOutcomePersistence": 1,
    "IGameplayOutcomeRecorder": 1,
    "IGameplayOutcomePresentationQuery": 1,
    "IGameplayOutcomeNarrativeEvidenceQuery": 1,
    "IGameplayOutcomeEvidenceUseTransaction": 1,
    "IKoreanJosaFormatter": 1,
}

REQUIRED_UI_SURFACES = {
    "Assets/Scripts/Views/UI/CodexFeatureSurfacePresenter.cs": "GetGlobalPage",
    "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs": "GetEntityPage",
    "Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs": "GetOperationPage",
}


def strip_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
    return re.sub(r"//[^\r\n]*", "", source)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.source_root.resolve()
    files = sorted((root / "Assets" / "Scripts").rglob("*.cs"))
    sources = {path: strip_comments(path.read_text(encoding="utf-8-sig")) for path in files}
    errors: list[str] = []

    definitions: dict[str, list[Path]] = {}
    calls: dict[str, list[Path]] = {}
    for path, source in sources.items():
        for match in METHOD.finditer(source):
            definitions.setdefault(match.group("name"), []).append(path)
        for match in CALL.finditer(source):
            calls.setdefault(match.group("name"), []).append(path)

    if not definitions:
        errors.append("no domain GameplayOutcome bridge registration extensions were found")
    for name, definition_paths in sorted(definitions.items()):
        if len(definition_paths) != 1:
            errors.append(f"{name}: expected one definition, found {len(definition_paths)}")
            continue
        definition_path = definition_paths[0]
        invocation_paths = [path for path in calls.get(name, []) if path != definition_path]
        if not invocation_paths:
            errors.append(f"{name}: extension is defined but never invoked")

    concrete_by_service: dict[str, list[str]] = {
        "IGameplayOutcomeAdapterRegistration": [],
        "IGameplayOutcomeDescriptor": [],
    }
    for source in sources.values():
        for match in REGISTER_AS.finditer(source):
            concrete_by_service[match.group("service")].append(match.group("concrete"))
    for service, concretes in concrete_by_service.items():
        if not concretes:
            errors.append(f"{service}: no concrete registrations found")
        duplicates = sorted({value for value in concretes if concretes.count(value) > 1})
        if duplicates:
            errors.append(f"{service}: duplicate concrete registrations: {', '.join(duplicates)}")
    adapter_count = len(concrete_by_service["IGameplayOutcomeAdapterRegistration"])
    descriptor_count = len(concrete_by_service["IGameplayOutcomeDescriptor"])
    if adapter_count != descriptor_count:
        errors.append(
            "adapter/descriptor registration count mismatch: "
            f"{adapter_count} adapters versus {descriptor_count} descriptors"
        )

    core_root = root / "Assets" / "Scripts" / "Services" / "Narrative" / "GameplayOutcomes"
    for path in sorted(core_root.rglob("*.cs")):
        if "Editor" in path.parts:
            continue
        source = sources[path]
        if CORE_LITERAL_OUTCOME.search(source):
            errors.append(
                f"{path.relative_to(root)}: core declares a concrete outcome type literal"
            )
        if CORE_OUTCOME_SWITCH.search(source):
            errors.append(
                f"{path.relative_to(root)}: core switches on outcome type instead of registry dispatch"
            )

    combined = "\n".join(sources.values())
    for service, expected in REQUIRED_SERVICE_REGISTRATIONS.items():
        count = len(re.findall(
            rf"\.\s*As\s*<\s*{re.escape(service)}\s*>\s*\(\s*\)", combined
        ))
        if count != expected:
            errors.append(f"{service}: expected {expected} registration, found {count}")

    for relative, query_method in REQUIRED_UI_SURFACES.items():
        path = root / relative
        source = sources.get(path)
        if source is None:
            errors.append(f"{relative}: required gameplay-outcome UI surface is missing")
            continue
        if "IGameplayOutcomePresentationQuery" not in source:
            errors.append(f"{relative}: presentation query is not injected")
        if not re.search(rf"\b{re.escape(query_method)}\s*\(", source):
            errors.append(f"{relative}: {query_method} is not called")
        if "DiagnosticCode" in source:
            errors.append(f"{relative}: internal diagnostic code leaks into the player UI surface")

    if errors:
        print("FAIL Phase80 composition source audit")
        for error in errors:
            print(f"- {error}")
        return 1
    print(
        "PASS Phase80 composition source audit: "
        f"{len(definitions)} domain extensions, "
        f"{adapter_count} adapters, "
        f"{descriptor_count} descriptors, "
        f"{len(REQUIRED_UI_SURFACES)} UI surfaces, core type branches 0"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
