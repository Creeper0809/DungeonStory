#!/usr/bin/env python3
"""Static, fail-closed supplement for the current gameplay-action-log composition.

The compiled production container remains authoritative. This verifier protects
the authored 149-module catalog and the source-level route that installs those
modules in the runtime scope. It intentionally understands only the current
GameplayActionLogDefinition/GameplayActionLogModule architecture.
"""

from __future__ import annotations

import argparse
import collections
import re
import sys
from dataclasses import dataclass
from pathlib import Path


EXPECTED_TYPED_MODULES = 77
EXPECTED_MIGRATED_MODULES = 72

CATALOG = Path(
    "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogCatalog.cs"
)
MODULE_FACTORY = Path(
    "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogModule.cs"
)
REGISTRY = Path(
    "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeRegistry.cs"
)
FOUNDATION_REGISTRATION = Path(
    "Assets/Scripts/Services/Infrastructure/Registration/DungeonFoundationRegistration.cs"
)
RUNTIME_SCOPE = Path(
    "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs"
)
PREPARATION_SCOPE = Path(
    "Assets/Scripts/Services/Infrastructure/DungeonPreparationLifetimeScope.cs"
)
PRESENTATION_QUERY = Path(
    "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/"
    "GameplayOutcomePresentationQuery.cs"
)
MIGRATED_CONTRACTS = Path(
    "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/"
    "MigratedProducerOutcomeContracts.cs"
)

TYPED_DEFINITION = re.compile(
    r"GameplayActionLogDefinition\s*\.\s*Typed\s*<\s*"
    r"(?P<adapter>[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*"
    r"(?P<descriptor>[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*"
    r"(?P<policy>[A-Za-z_][A-Za-z0-9_.]*)\s*>\s*"
    r"\(\s*nameof\s*\(\s*(?P<group>[A-Za-z_][A-Za-z0-9_.]*)\s*\)\s*\)"
)
MIGRATED_DEFINITION = re.compile(
    r"GameplayActionLogDefinition\s*\.\s*Migrated\s*<\s*"
    r"(?P<policy>[A-Za-z_][A-Za-z0-9_.]*)\s*>\s*\(\s*"
    r"MigratedProducerOutcomeKind\s*\.\s*(?P<kind>[A-Za-z_][A-Za-z0-9_]*)\s*\)"
)
GROUP_CALL = re.compile(
    r"\bRegisterGameplayActionLogs\s*\(\s*(?:"
    r"nameof\s*\(\s*(?P<name>[A-Za-z_][A-Za-z0-9_.]*)\s*\)"
    r'|"(?P<literal>[^"\r\n]+)")\s*\)'
)
REGISTRATION_METHOD = re.compile(
    r"\b(?:public|internal|private)\s+static\s+void\s+"
    r"(?P<name>Register[A-Za-z0-9_]+)\s*\("
)
CORE_LITERAL_OUTCOME = re.compile(
    r'\bnew\s+GameplayOutcomeTypeId\s*\(\s*"[^"\r\n]+"\s*\)'
)
CORE_OUTCOME_SWITCH = re.compile(
    r"\bswitch\s*\([^)]*\b(?:OutcomeType|OutcomeTypeId)\b[^)]*\)", re.S
)

COMMON_SCOPE_SERVICES = (
    "IGameplayOutcomeRegistry",
    "IGameplayOutcomeQuery",
    "IGameplayOutcomeMemoryCommands",
    "IGameplayOutcomeConsolidationService",
    "IGameplayOutcomeDiagnosticsQuery",
    "IGameplayOutcomePersistence",
    "IGameplayOutcomeRecorder",
    "IKoreanJosaFormatter",
)
RUNTIME_ONLY_SERVICES = (
    "IGameplayOutcomePresentationQuery",
    "IGameplayOutcomeNarrativeEvidenceQuery",
    "IGameplayOutcomeEvidenceUseTransaction",
)
SCOPED_SERVICES = COMMON_SCOPE_SERVICES + RUNTIME_ONLY_SERVICES

REQUIRED_UI_SURFACES = {
    "Assets/Scripts/Views/UI/CodexFeatureSurfacePresenter.cs": "GetGlobalPage",
    "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Views/UI/BuildingSummaryInfo.cs": "GetEntityPage",
    "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs": "GetEntityPage",
    "Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs": "GetExpeditionPage",
}

NON_CODE = re.compile(
    r"/\*.*?\*/|//[^\r\n]*|@\"(?:\"\"|[^\"])*\"|"
    r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',
    re.S,
)


@dataclass(frozen=True)
class MethodBody:
    name: str
    path: Path
    source: str
    masked: str


def strip_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
    return re.sub(r"//[^\r\n]*", "", source)


def mask_non_code(source: str) -> str:
    def replace(match: re.Match[str]) -> str:
        return "".join("\n" if character == "\n" else " " for character in match.group())

    return NON_CODE.sub(replace, source)


def matching_brace(masked: str, opening: int) -> int | None:
    depth = 0
    for index in range(opening, len(masked)):
        if masked[index] == "{":
            depth += 1
        elif masked[index] == "}":
            depth -= 1
            if depth == 0:
                return index
    return None


def extract_methods(path: Path, source: str, errors: list[str]) -> list[MethodBody]:
    masked = mask_non_code(source)
    methods: list[MethodBody] = []
    for match in REGISTRATION_METHOD.finditer(masked):
        opening = masked.find("{", match.end())
        if opening < 0:
            continue
        if "=>" in masked[match.end():opening]:
            continue
        closing = matching_brace(masked, opening)
        if closing is None:
            errors.append(f"{path}: registration method {match.group('name')} has no closing brace")
            continue
        methods.append(MethodBody(
            match.group("name"), path, source[opening + 1:closing], masked[opening + 1:closing]
        ))
    return methods


def extract_configure(path: Path, source: str, errors: list[str]) -> MethodBody | None:
    masked = mask_non_code(source)
    match = re.search(
        r"\b(?:protected|public|internal)\s+override\s+void\s+Configure\s*\(", masked
    )
    if match is None:
        errors.append(f"{path}: Configure method is missing")
        return None
    opening = masked.find("{", match.end())
    closing = matching_brace(masked, opening) if opening >= 0 else None
    if opening < 0 or closing is None:
        errors.append(f"{path}: Configure method body is malformed")
        return None
    return MethodBody(
        "Configure", path, source[opening + 1:closing], masked[opening + 1:closing]
    )


def duplicates(values: list[str]) -> list[str]:
    counts = collections.Counter(values)
    return sorted(value for value, count in counts.items() if count > 1)


def calls_to_known_methods(masked: str, known: set[str]) -> collections.Counter[str]:
    calls: collections.Counter[str] = collections.Counter()
    for name in known:
        count = len(re.findall(rf"\b{re.escape(name)}\s*\(", masked))
        if count:
            calls[name] = count
    return calls


def reachable_invocations(
    root_body: MethodBody,
    methods: dict[str, MethodBody],
    errors: list[str],
    scope_name: str,
) -> collections.Counter[str]:
    known = set(methods)
    graph = {
        name: calls_to_known_methods(method.masked, known)
        for name, method in methods.items()
    }
    result: collections.Counter[str] = collections.Counter()

    def walk(name: str, multiplier: int, stack: tuple[str, ...]) -> None:
        if name in stack:
            errors.append(
                f"{scope_name}: registration call cycle: {' -> '.join(stack + (name,))}"
            )
            return
        result[name] += multiplier
        for child, count in graph[name].items():
            walk(child, multiplier * count, stack + (name,))

    for name, count in calls_to_known_methods(root_body.masked, known).items():
        walk(name, count, ())
    return result


def count_service_registrations(source: str, service: str) -> int:
    return len(re.findall(
        rf"\.\s*As\s*<\s*{re.escape(service)}\s*>\s*\(\s*\)", source
    ))


def scope_service_counts(
    root_body: MethodBody,
    invocations: collections.Counter[str],
    methods: dict[str, MethodBody],
) -> dict[str, int]:
    counts = {
        service: count_service_registrations(root_body.source, service)
        for service in SCOPED_SERVICES
    }
    for method_name, invocation_count in invocations.items():
        method = methods[method_name]
        for service in SCOPED_SERVICES:
            counts[service] += invocation_count * count_service_registrations(
                method.source, service
            )
    return counts


def require_pattern(
    source: str,
    pattern: str,
    failure: str,
    errors: list[str],
) -> None:
    if re.search(pattern, source, re.S) is None:
        errors.append(failure)


def parse_enum_members(source: str, enum_name: str) -> list[str]:
    masked = mask_non_code(source)
    match = re.search(
        rf"\b(?:public|internal)\s+enum\s+{re.escape(enum_name)}\s*\{{", masked
    )
    if match is None:
        return []
    opening = masked.find("{", match.start())
    closing = matching_brace(masked, opening)
    if closing is None:
        return []
    body = source[opening + 1:closing]
    return re.findall(r"(?m)^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=", body)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.source_root.resolve()
    script_root = root / "Assets" / "Scripts"
    files = sorted(
        path for path in script_root.rglob("*.cs")
        if "Editor" not in path.relative_to(script_root).parts
    )
    sources = {
        path.relative_to(root): strip_comments(path.read_text(encoding="utf-8-sig"))
        for path in files
    }
    errors: list[str] = []

    def required_source(relative: Path) -> str:
        source = sources.get(relative)
        if source is None:
            errors.append(f"{relative.as_posix()}: required current composition source is missing")
            return ""
        return source

    catalog_source = required_source(CATALOG)
    module_source = required_source(MODULE_FACTORY)
    registry_source = required_source(REGISTRY)
    foundation_source = required_source(FOUNDATION_REGISTRATION)
    migrated_source = required_source(MIGRATED_CONTRACTS)

    typed = list(TYPED_DEFINITION.finditer(catalog_source))
    migrated = list(MIGRATED_DEFINITION.finditer(catalog_source))
    if len(typed) != EXPECTED_TYPED_MODULES:
        errors.append(
            f"GameplayActionLogCatalog: expected {EXPECTED_TYPED_MODULES} typed modules, found {len(typed)}"
        )
    if len(migrated) != EXPECTED_MIGRATED_MODULES:
        errors.append(
            f"GameplayActionLogCatalog: expected {EXPECTED_MIGRATED_MODULES} migrated modules, found {len(migrated)}"
        )

    typed_adapters = [match.group("adapter") for match in typed]
    typed_descriptors = [match.group("descriptor") for match in typed]
    typed_policies = [match.group("policy") for match in typed]
    typed_groups = [match.group("group") for match in typed]
    migrated_policies = [match.group("policy") for match in migrated]
    migrated_kinds = [match.group("kind") for match in migrated]
    for label, values in (
        ("typed adapter", typed_adapters),
        ("typed descriptor", typed_descriptors),
        ("typed policy", typed_policies),
        ("typed module group", typed_groups),
        ("migrated policy", migrated_policies),
        ("migrated kind", migrated_kinds),
    ):
        repeated = duplicates(values)
        if repeated:
            errors.append(f"GameplayActionLogCatalog: duplicate {label}: {', '.join(repeated)}")
    for match in typed:
        if match.group("adapter") != match.group("group"):
            errors.append(
                "GameplayActionLogCatalog: typed module group differs from adapter: "
                f"{match.group('adapter')} versus {match.group('group')}"
            )
    overlapping_policies = sorted(set(typed_policies).intersection(migrated_policies))
    if overlapping_policies:
        errors.append(
            "GameplayActionLogCatalog: policies are shared across modules: "
            + ", ".join(overlapping_policies)
        )

    enum_kinds = [
        value for value in parse_enum_members(migrated_source, "MigratedProducerOutcomeKind")
        if value != "None"
    ]
    if not enum_kinds:
        errors.append("MigratedProducerOutcomeKind: non-None members are missing")
    elif collections.Counter(enum_kinds) != collections.Counter(migrated_kinds):
        missing = sorted(set(enum_kinds) - set(migrated_kinds))
        unknown = sorted(set(migrated_kinds) - set(enum_kinds))
        errors.append(
            "GameplayActionLogCatalog: migrated enum/catalog mismatch"
            f"; missing={','.join(missing) or '-'}; unknown={','.join(unknown) or '-'}"
        )

    require_pattern(
        module_source,
        r"Register\s*<\s*TAdapter\s*>\s*\([^;]*?\)\s*\.\s*AsSelf\s*\(",
        "GameplayActionLogDefinition.Typed: adapter self-registration is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"Register\s*<\s*TDescriptor\s*>\s*\([^;]*?\)\s*\.\s*AsSelf\s*\(",
        "GameplayActionLogDefinition.Typed: descriptor self-registration is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"new\s+GameplayActionLogModule\s*<\s*TPolicy\s*>\s*\(\s*"
        r"resolver\.Resolve\s*<\s*TAdapter\s*>\s*\(\s*\)\s*,\s*"
        r"resolver\.Resolve\s*<\s*TDescriptor\s*>\s*\(\s*\)\s*,\s*policy\s*\)"
        r"[^;]*?\.\s*As\s*<\s*IGameplayActionLogModule\s*>",
        "GameplayActionLogDefinition.Typed: adapter/descriptor/policy module factory is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"MigratedProducerOutcomeCatalog\.Definitions\.Single\s*\([^)]*Kind\s*==\s*kind",
        "GameplayActionLogDefinition.Migrated: kind catalog link is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"policy\.OutcomeTypes\.Count\s*!=\s*1.*?policy\.OutcomeTypes\s*\[\s*0\s*\]\s*!=\s*definition\.OutcomeTypeId\.Value",
        "GameplayActionLogDefinition.Migrated: policy/outcome identity guard is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"new\s+GameplayActionLogModule\s*<\s*TPolicy\s*>\s*\(\s*"
        r"resolver\.Resolve\s*<\s*MigratedProducerOutcomeAdapter\s*>\s*\(\s*\)\s*,\s*"
        r"new\s+MigratedProducerOutcomeDescriptor\s*\(\s*definition.*?,\s*policy\s*\)"
        r"[^;]*?\.\s*As\s*<\s*IGameplayActionLogModule\s*>",
        "GameplayActionLogDefinition.Migrated: adapter/descriptor/policy module factory is missing",
        errors,
    )
    require_pattern(
        module_source,
        r"Definitions\.Where\s*\([^)]*Group\s*==\s*group\).*?"
        r"foreach\s*\([^)]*definition\s+in\s+definitions\s*\)\s*definition\.Register\s*\(\s*builder\s*\)",
        "RegisterGameplayActionLogs: declared group definitions are not installed",
        errors,
    )

    require_pattern(
        registry_source,
        r"GameplayOutcomeRegistry\s+FromModules\s*\(\s*IEnumerable\s*<\s*IGameplayActionLogModule\s*>",
        "GameplayOutcomeRegistry.FromModules factory is missing",
        errors,
    )
    require_pattern(
        registry_source,
        r"new\s+GameplayActionLogModule\s*\(\s*module\.Adapter\s*,\s*module\.Descriptor\s*,\s*module\.ExperienceObserver\s*\)",
        "GameplayOutcomeRegistry.FromModules: module identity revalidation is missing",
        errors,
    )
    require_pattern(
        foundation_source,
        r"GameplayOutcomeRegistry\.FromModules\s*\(.*?Resolve\s*<\s*"
        r"(?:System\.Collections\.Generic\.)?IEnumerable\s*<\s*IGameplayActionLogModule\s*>\s*>\s*\(",
        "DungeonFoundationRegistration: module collection is not routed through FromModules",
        errors,
    )

    method_lists: dict[str, list[MethodBody]] = collections.defaultdict(list)
    for relative, source in sources.items():
        for method in extract_methods(relative, source, errors):
            method_lists[method.name].append(method)
    methods: dict[str, MethodBody] = {}
    for name, candidates in sorted(method_lists.items()):
        if len(candidates) == 1:
            methods[name] = candidates[0]
        else:
            locations = ", ".join(str(value.path) for value in candidates)
            errors.append(f"{name}: duplicate registration method definitions: {locations}")

    runtime_source = required_source(RUNTIME_SCOPE)
    preparation_source = required_source(PREPARATION_SCOPE)
    runtime_body = extract_configure(RUNTIME_SCOPE, runtime_source, errors) if runtime_source else None
    preparation_body = (
        extract_configure(PREPARATION_SCOPE, preparation_source, errors)
        if preparation_source else None
    )
    runtime_invocations: collections.Counter[str] = collections.Counter()
    preparation_invocations: collections.Counter[str] = collections.Counter()
    if runtime_body is not None:
        runtime_invocations = reachable_invocations(
            runtime_body, methods, errors, "runtime scope"
        )
    if preparation_body is not None:
        preparation_invocations = reachable_invocations(
            preparation_body, methods, errors, "preparation scope"
        )

    declared_groups = typed_groups + ["migrated"]
    actual_group_calls: list[str] = []
    group_owners: dict[str, list[str]] = collections.defaultdict(list)
    for source in sources.values():
        for match in GROUP_CALL.finditer(source):
            actual_group_calls.append(match.group("name") or match.group("literal"))
    for name, method in methods.items():
        for match in GROUP_CALL.finditer(method.source):
            group_owners[match.group("name") or match.group("literal")].append(name)
    if runtime_body is not None:
        for match in GROUP_CALL.finditer(runtime_body.source):
            group_owners[match.group("name") or match.group("literal")].append("Configure")

    declared_set = set(declared_groups)
    actual_set = set(actual_group_calls)
    missing_groups = sorted(declared_set - actual_set)
    unknown_groups = sorted(actual_set - declared_set)
    if missing_groups:
        errors.append("action-log module group calls missing: " + ", ".join(missing_groups))
    if unknown_groups:
        errors.append("unknown action-log module group calls: " + ", ".join(unknown_groups))
    repeated_calls = duplicates(actual_group_calls)
    if repeated_calls:
        errors.append("duplicate action-log module group calls: " + ", ".join(repeated_calls))

    for group in sorted(declared_set.intersection(actual_set)):
        owners = group_owners.get(group, [])
        executions = sum(
            1 if owner == "Configure" else runtime_invocations.get(owner, 0)
            for owner in owners
        )
        if executions != 1:
            errors.append(
                f"action-log module group {group}: expected one runtime-root execution, found {executions}"
            )

    if runtime_body is not None:
        runtime_counts = scope_service_counts(runtime_body, runtime_invocations, methods)
        for service in SCOPED_SERVICES:
            if runtime_counts[service] != 1:
                errors.append(
                    f"runtime scope {service}: expected 1 registration, found {runtime_counts[service]}"
                )
    if preparation_body is not None:
        preparation_counts = scope_service_counts(
            preparation_body, preparation_invocations, methods
        )
        preparation_expected = {
            service: 1 if service in COMMON_SCOPE_SERVICES
            or service == "IGameplayOutcomeNarrativeEvidenceQuery" else 0
            for service in SCOPED_SERVICES
        }
        for service, expected in preparation_expected.items():
            if preparation_counts[service] != expected:
                errors.append(
                    f"preparation scope {service}: expected {expected} registration, "
                    f"found {preparation_counts[service]}"
                )

    core_root = Path("Assets/Scripts/Services/Narrative/GameplayOutcomes")
    for path, source in sorted(sources.items(), key=lambda item: str(item[0])):
        if core_root not in (path, *path.parents):
            continue
        if CORE_LITERAL_OUTCOME.search(source):
            errors.append(f"{path}: core declares a concrete outcome type literal")
        if CORE_OUTCOME_SWITCH.search(source):
            errors.append(f"{path}: core switches on outcome type instead of registry dispatch")

    for relative_text, query_method in REQUIRED_UI_SURFACES.items():
        relative = Path(relative_text)
        source = sources.get(relative)
        if source is None:
            errors.append(f"{relative_text}: required gameplay-outcome UI surface is missing")
            continue
        if "IGameplayOutcomePresentationQuery" not in source:
            errors.append(f"{relative_text}: presentation query is not injected")
        if re.search(rf"\b{re.escape(query_method)}\s*\(", source) is None:
            errors.append(f"{relative_text}: {query_method} is not called")
        if "DiagnosticCode" in source:
            errors.append(f"{relative_text}: internal diagnostic code leaks into the player UI surface")

    presentation_source = required_source(PRESENTATION_QUERY)
    if presentation_source and (
        re.search(r"\bGetExpeditionPage\s*\(", presentation_source) is None
        or re.search(r"\bquery\.GetForOperation\s*\(", presentation_source) is None
    ):
        errors.append(
            "GameplayOutcomePresentationQuery.GetExpeditionPage: operation query route is missing"
        )

    if errors:
        print("FAIL gameplay action-log composition source audit")
        for error in errors:
            print(f"- {error}")
        return 1
    print(
        "PASS gameplay action-log composition source audit: "
        f"{len(typed)} typed + {len(migrated)} migrated = {len(typed) + len(migrated)} modules, "
        f"{len(actual_group_calls)} exact group calls, "
        f"{len(REQUIRED_UI_SURFACES)} UI surfaces, core type branches 0"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
