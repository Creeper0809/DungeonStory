#!/usr/bin/env python3
"""Independently verify a new unseen naming-evaluation Unity export.

This verifier is read-only unless ``--report`` is supplied. It imports the
current NarrativeAI adapter-v3 implementation for projection only; it never
invokes a model and never writes to the NarrativeAI workspace.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import importlib
import json
import os
import re
import sys
from collections import Counter, defaultdict
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any


PROFILE_COUNTS = {
    "CharacterSkill": 20,
    "AcquiredTrait": 20,
    "FacilityEvolution": 15,
    "EquipmentChoice": 15,
    "EvolutionHistory": 15,
    "Persona": 15,
}
EXPECTED_NEGATIVE_COUNT = 15
EXPECTED_FAMILY_COUNT = 85
EXPECTED_SPLIT = "unseen-evaluation"
EXPECTED_EXCLUSION_SHA = (
    "sha256:b515811c01961c04def62c170e67681ee001f0d7118675c606812dfa40ee880b"
)
EXPECTED_SOURCE_PILOT_HASH = (
    "sha256:df7dc89e8904bbae8387fd36692c827201fcfe0845dc5f12b0a201e957c98e7f"
)
EXPECTED_ADAPTER_SHA = (
    "sha256:929369b1eb674ee7212f5012759d3b0793fec5021a8b32a72a2c3a37a2834c6b"
)
EXPECTED_PRIOR_DELIVERY_SHA = (
    "sha256:fb24807af274fa1d9f05668ae89060ba0d2acfb77cb048369ac1e55d37109ce2"
)
EVENT_MEANING_PROFILES = {"CharacterSkill", "AcquiredTrait"}
PERSONA_PROFILE = "Persona"
PERSONA_NEGATIVE_PUBLIC_DELTA_ID = "persona-reject-extra-mechanical-key"
UNCHANGED_PROFILE_ROWS = 45
EXPECTED_READABLE_EVENT_ROWS = 40
EXPECTED_READABLE_EVENT_COUNT = 257
PERSONA_NEED_KEYS = {"hunger", "sleep", "fun", "mood", "excretion", "hygiene"}
PERSONA_IDENTITY_PREFIXES = ("Name: ", "Potential: ")
INTERNAL_EVENT_TOKEN = re.compile(r"(?:fixture:|audit:|sha256:)", re.IGNORECASE)
NEED_TEXT = re.compile(
    r"^Current need (hunger|sleep|fun|mood|excretion|hygiene): "
    r"(unavailable|-?\d+(?:\.\d+)?)$"
)


class VerificationError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def strict_json(raw: bytes | str) -> Any:
    def pairs(items: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in items:
            require(key not in result, f"duplicate JSON key: {key}")
            result[key] = value
        return result

    def invalid(value: str) -> None:
        raise VerificationError(f"non-finite JSON number: {value}")

    return json.loads(raw, object_pairs_hook=pairs, parse_constant=invalid)


def canonical(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def sha_bytes(raw: bytes) -> str:
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def write_new(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        stream.write(canonical(value) + b"\n")


def load_adapter(ai_root: Path):
    tools = ai_root / "tools" / "v25_narrative_training"
    adapter = tools / "scenario_adapter.py"
    require(adapter.is_file(), f"NarrativeAI scenario adapter is missing: {adapter}")
    sys.path.insert(0, str(tools))
    return importlib.import_module("scenario_adapter"), sha_file(adapter)


def delivery_files(export_dir: Path, delivery: dict[str, Any]) -> dict[str, bytes]:
    entries = delivery.get("files")
    require(isinstance(entries, list) and entries, "delivery file list is missing")
    paths = [entry.get("path") for entry in entries]
    require(all(isinstance(path, str) and path for path in paths), "invalid delivery path")
    require(len(paths) == len(set(paths)), "duplicate delivery path")
    required = {
        "README.md",
        "catalog.json",
        "continuity_scenarios.json",
        "manifest.json",
        "negative_scenarios.json",
        "novelty_review.json",
        "packet_parity_cases.json",
        "policy.json",
        "raw_test_results.json",
        "scenario_schema.json",
        "scenarios_100.json",
        "test_evidence.json",
    }
    require(set(paths) == required, f"delivery file set differs: {sorted(set(paths) ^ required)}")
    result: dict[str, bytes] = {}
    for entry in entries:
        path = export_dir / entry["path"]
        require(path.is_file(), f"delivery file missing: {entry['path']}")
        raw = path.read_bytes()
        require(len(raw) == entry.get("byteLength"), f"byte length differs: {entry['path']}")
        require(sha_bytes(raw) == entry.get("sha256"), f"SHA differs: {entry['path']}")
        result[entry["path"]] = raw
    return result


def verify_source_manifest(
    project_root: Path,
    manifest: dict[str, Any],
    catalog: dict[str, Any],
) -> dict[str, int]:
    files = manifest.get("files")
    require(isinstance(files, list) and files, "source manifest files are missing")
    paths = [entry.get("path") for entry in files]
    require(len(paths) == len(set(paths)), "duplicate source-manifest path")
    for entry in files:
        relative = entry.get("path")
        require(isinstance(relative, str) and relative, "invalid source path")
        path = project_root / Path(relative)
        require(path.is_file(), f"declared source no longer exists: {relative}")
        require(sha_file(path) == entry.get("sha256"), f"declared source changed: {relative}")

    game_commit = catalog["source"]["gameCommit"]
    input_preimage = {"files": files, "gameCommit": game_commit, "schemaVersion": 1}
    input_digest = sha_bytes(canonical(input_preimage))
    require(input_digest == manifest.get("inputDigest"), "inputDigest preimage mismatch")

    dirty = [
        {
            "gitStatus": entry["gitStatus"],
            "path": entry["path"],
            "sha256": entry["sha256"],
            "tracked": entry["tracked"],
        }
        for entry in files
        if entry.get("gitStatus") != "clean"
    ]
    dirty_hash = sha_bytes(canonical({"files": dirty, "schemaVersion": 1}))
    require(
        dirty_hash == manifest.get("uncommittedSourceHash"),
        "uncommittedSourceHash preimage mismatch",
    )
    return {"sourceFileCount": len(files), "dirtySourceFileCount": len(dirty)}


def semantic_projection(scenario: dict[str, Any]) -> dict[str, Any]:
    excluded = {"scenarioId", "catalogHash", "inputDigest", "semanticHash"}
    return {key: value for key, value in scenario.items() if key not in excluded}


def public_story(scenario: dict[str, Any]) -> dict[str, Any]:
    return {
        "profileId": scenario["profileId"],
        "publicFacts": scenario["publicFacts"],
        "publicNarrativeContext": scenario["publicNarrativeContext"],
    }


def scenario_mechanics(scenario: dict[str, Any]) -> dict[str, Any]:
    """Return gameplay/request authority while excluding the corrected public prose."""
    excluded = {
        "catalogHash",
        "evaluationMetadata",
        "inputDigest",
        "publicFacts",
        "publicNarrativeContext",
        "semanticHash",
    }
    result = {key: copy.deepcopy(value) for key, value in scenario.items() if key not in excluded}
    request = result.get("request")
    if isinstance(request, dict):
        request.pop("prompt", None)
    authority = result.get("authorityContext")
    if isinstance(authority, dict):
        authority.pop("requestPromptHash", None)
    return result


def verify_negative_regressions(
    current_rows: list[dict[str, Any]],
    prior_rows: list[dict[str, Any]],
) -> tuple[int, int]:
    """Preserve all rejection mechanics while allowing the requested Persona facts."""
    current_by_id = {row["scenarioId"]: row for row in current_rows}
    prior_by_id = {row["scenarioId"]: row for row in prior_rows}
    require(len(current_by_id) == len(current_rows), "duplicate current negative scenario ID")
    require(len(prior_by_id) == len(prior_rows), "duplicate prior negative scenario ID")
    require(set(current_by_id) == set(prior_by_id), "negative regression IDs changed")

    exact_rows = 0
    persona_public_delta_rows = 0
    for scenario_id, current in current_by_id.items():
        prior = prior_by_id[scenario_id]
        if scenario_id != PERSONA_NEGATIVE_PUBLIC_DELTA_ID:
            require(
                current["semanticHash"] == prior["semanticHash"]
                and semantic_projection(current) == semantic_projection(prior),
                f"negative regression changed: {scenario_id}",
            )
            exact_rows += 1
            continue

        current_prompt_hash = sha_bytes(current["request"]["prompt"].encode("utf-8"))
        prior_prompt_hash = sha_bytes(prior["request"]["prompt"].encode("utf-8"))
        require(
            current["authorityContext"]["requestPromptHash"] == current_prompt_hash
            and prior["authorityContext"]["requestPromptHash"] == prior_prompt_hash,
            "Persona negative request prompt provenance differs",
        )
        current_mechanics = scenario_mechanics(current)
        prior_mechanics = scenario_mechanics(prior)
        require(
            current_mechanics == prior_mechanics,
            "Persona negative rejection mechanics changed",
        )
        prior_non_need = [
            fact for fact in prior["publicFacts"] if fact.get("domain") != "Need"
        ]
        current_non_need = [
            fact for fact in current["publicFacts"] if fact.get("domain") != "Need"
        ]
        require(
            current_non_need == prior_non_need,
            "Persona negative changed public facts outside the requested current needs",
        )
        verify_persona_public_semantics(
            current,
            {
                "publicFacts": current["publicFacts"],
                "publicNarrativeContext": current["publicNarrativeContext"],
            },
        )

        prior_context = copy.deepcopy(prior["publicNarrativeContext"])
        current_context = copy.deepcopy(current["publicNarrativeContext"])
        prior_selection = prior_context["selection"]
        current_selection = current_context["selection"]
        require(
            current_selection["availableFactCount"]
            == prior_selection["availableFactCount"] + len(PERSONA_NEED_KEYS),
            "Persona negative public-context fact count did not change by six needs",
        )
        current_selection["availableFactCount"] = prior_selection["availableFactCount"]
        require(
            current_context == prior_context,
            "Persona negative public context changed outside the requested need-fact count",
        )
        persona_public_delta_rows += 1

    require(exact_rows == EXPECTED_NEGATIVE_COUNT - 1, "exact negative regression count differs")
    require(persona_public_delta_rows == 1, "Persona negative public delta count differs")
    return exact_rows, persona_public_delta_rows


def persona_masked_projection(model_input: dict[str, Any]) -> dict[str, Any]:
    """Remove Persona name, potential, and opaque IDs while preserving usable facts."""
    facts: list[dict[str, Any]] = []
    facts_by_id: dict[str, dict[str, Any]] = {}
    for fact in model_input["publicFacts"]:
        text = fact["text"]
        if text.startswith(PERSONA_IDENTITY_PREFIXES):
            continue
        masked = {
            key: value
            for key, value in fact.items()
            if key not in {"factId", "subjectId"}
        }
        facts.append(masked)
        facts_by_id[fact["factId"]] = masked

    context = model_input["publicNarrativeContext"]
    references = {
        group: [facts_by_id[fact_id] for fact_id in context[group] if fact_id in facts_by_id]
        for group in (
            "backgroundFactIds",
            "memoryFactIds",
            "priorHistoryFactIds",
            "relationshipFactIds",
        )
    }
    return {
        "adapterVersion": model_input["adapterVersion"],
        "profileId": model_input["profileId"],
        "publicFacts": facts,
        "publicNarrativeContext": {
            "entities": [{"kind": entity["kind"]} for entity in context["entities"]],
            "events": context["events"],
            "profileId": context["profileId"],
            "references": references,
            "schemaVersion": context["schemaVersion"],
            "selection": context["selection"],
            "subjectKind": context["subjectKind"],
        },
        "request": model_input["request"],
    }


def identity_only_persona_spoof(model_input: dict[str, Any]) -> dict[str, Any]:
    """Change only opaque identity, Name, and Potential to exercise the novelty guard."""
    spoof = copy.deepcopy(model_input)
    id_map: dict[str, str] = {}
    for index, fact in enumerate(spoof["publicFacts"]):
        old_id = fact["factId"]
        new_id = f"spoof-fact-{index}"
        id_map[old_id] = new_id
        fact["factId"] = new_id
        if fact["text"].startswith("Name: "):
            fact["text"] = "Name: identity-only-spoof"
        elif fact["text"].startswith("Potential: "):
            fact["text"] = "Potential: identity-only-spoof"
        if "subjectId" in fact:
            fact["subjectId"] = "character:identity-only-spoof"

    context = spoof["publicNarrativeContext"]
    old_subject = context["subjectId"]
    context["subjectId"] = "character:identity-only-spoof"
    for group in (
        "backgroundFactIds",
        "memoryFactIds",
        "priorHistoryFactIds",
        "relationshipFactIds",
    ):
        context[group] = [id_map[value] for value in context[group]]
    for entity in context["entities"]:
        if entity["entityId"] == old_subject:
            entity["entityId"] = context["subjectId"]
            entity["displayName"] = "identity-only-spoof"
    return spoof


def verify_persona_public_semantics(
    scenario: dict[str, Any],
    model_input: dict[str, Any],
) -> None:
    sid = scenario["scenarioId"]
    require(
        model_input["publicFacts"] == scenario["publicFacts"],
        f"adapter3 changed Persona public facts: {sid}",
    )
    require(
        model_input["publicNarrativeContext"] == scenario["publicNarrativeContext"],
        f"adapter3 changed Persona public context: {sid}",
    )
    context = model_input["publicNarrativeContext"]
    require(not context["events"], f"Persona invents event history: {sid}")
    require(
        not context["memoryFactIds"]
        and not context["priorHistoryFactIds"]
        and not context["relationshipFactIds"],
        f"Persona invents individual history: {sid}",
    )
    need_facts = [fact for fact in model_input["publicFacts"] if fact.get("domain") == "Need"]
    require(len(need_facts) == 6, f"Persona does not expose exactly six current needs: {sid}")
    labels: set[str] = set()
    for fact in need_facts:
        match = NEED_TEXT.fullmatch(fact["text"])
        require(match is not None, f"Persona need text is not authoritative/readable: {sid}")
        label, displayed = match.groups()
        labels.add(label)
        if displayed == "unavailable":
            require(
                "totalValueDecimal" not in fact,
                f"Unavailable Persona need exposes a numeric value: {sid}/{label}",
            )
        else:
            require(
                "totalValueDecimal" in fact,
                f"Available Persona need omits its authoritative numeric value: {sid}/{label}",
            )
            try:
                require(
                    Decimal(displayed) == Decimal(fact["totalValueDecimal"]),
                    f"Persona need text/value disagree: {sid}/{label}",
                )
            except InvalidOperation as error:
                raise VerificationError(
                    f"Persona need decimal is invalid: {sid}/{label}"
                ) from error
    require(labels == PERSONA_NEED_KEYS, f"Persona current-need labels differ: {sid}")

    encoded = canonical(model_input).decode("utf-8").lower()
    for forbidden in (
        "needfocus",
        "preferredfacilitytag",
        "fixturekind",
        "multiplier",
        "locked mechanic",
        "facility tag",
    ):
        require(forbidden not in encoded, f"Persona exposes host-only '{forbidden}': {sid}")


def event_content_tokens(text: str, event_type: str, domain: str, outcome: str) -> list[str]:
    reduced = text.lower()
    for value in (event_type, domain, outcome):
        reduced = reduced.replace(value.lower(), " ")
    generic = {
        "experience",
        "event",
        "outcome",
        "count",
        "total",
        "value",
    }
    return [
        token
        for token in re.findall(r"[a-zA-Z가-힣]{2,}", reduced)
        if token not in generic
    ]


def verify_event_public_semantics(
    scenario: dict[str, Any],
    model_input: dict[str, Any],
) -> int:
    sid = scenario["scenarioId"]
    require(
        model_input["publicFacts"] == scenario["publicFacts"],
        f"adapter3 changed event public facts: {sid}",
    )
    require(
        model_input["publicNarrativeContext"] == scenario["publicNarrativeContext"],
        f"adapter3 changed event public context: {sid}",
    )
    facts = {fact["factId"]: fact for fact in model_input["publicFacts"]}
    context = model_input["publicNarrativeContext"]
    entities = {entity["entityId"]: entity for entity in context["entities"]}
    events = context["events"]
    calls = scenario["fixtureInput"].get("ledgerRecordCalls", [])
    require(events and len(events) == len(calls), f"event/ledger-call count differs: {sid}")

    public_tuples: Counter[tuple[Any, ...]] = Counter()
    for event in events:
        fact = facts[event["factId"]]
        event_type = event["eventType"]
        require(
            event_type and not INTERNAL_EVENT_TOKEN.search(event_type),
            f"event meaning depends on an internal ID: {sid}/{event['factId']}",
        )
        require(event["actorId"] == context["subjectId"], f"event actor differs: {sid}")
        require(
            bool(event["targetId"])
            and event["targetId"] != event["actorId"]
            and event["targetId"] in entities,
            f"event target is not a concrete public entity: {sid}/{event['factId']}",
        )
        require(
            not INTERNAL_EVENT_TOKEN.search(fact["text"]),
            f"public fact leaks an internal event ID: {sid}/{event['factId']}",
        )
        require(
            len(event_content_tokens(fact["text"], event_type, fact["domain"], event["outcome"])) >= 3,
            f"public event text has no concrete meaning after IDs are masked: {sid}/{event['factId']}",
        )
        require(event["day"] is not None and event["count"] > 0, f"event state is incomplete: {sid}")
        public_tuples[(fact["domain"], event["outcome"], event["day"], event["count"])] += 1

    ledger_tuples = Counter(
        (call["domain"], call["outcome"], call["day"], call["repetitions"])
        for call in calls
    )
    require(public_tuples == ledger_tuples, f"public event authority tuple differs: {sid}")
    masked = copy.deepcopy(model_input)
    for fact in masked["publicFacts"]:
        fact["factId"] = "<masked>"
        if "subjectId" in fact:
            fact["subjectId"] = "<masked>"
    for entity in masked["publicNarrativeContext"]["entities"]:
        entity["entityId"] = "<masked>"
    masked["publicNarrativeContext"]["subjectId"] = "<masked>"
    for group in (
        "backgroundFactIds",
        "memoryFactIds",
        "priorHistoryFactIds",
        "relationshipFactIds",
    ):
        masked["publicNarrativeContext"][group] = ["<masked>"] * len(
            masked["publicNarrativeContext"][group]
        )
    for event in masked["publicNarrativeContext"]["events"]:
        for key in ("actorId", "evidenceId", "factId", "targetId"):
            event[key] = "<masked>"
    require(
        all(event["eventType"] != "<masked>" for event in masked["publicNarrativeContext"]["events"]),
        f"masking opaque IDs removed structured event meaning: {sid}",
    )
    return len(events)


def candidate_ids(scenario: dict[str, Any]) -> list[str]:
    profile = scenario["profileId"]
    candidates = scenario["fullLegalCandidates"]
    if profile == "CharacterSkill":
        request_rules = {
            rule["ruleId"]: rule for rule in scenario["request"].get("rules", [])
        }
        require(
            len(request_rules) == len(scenario["request"].get("rules", [])),
            f"duplicate skill request rule: {scenario['scenarioId']}",
        )
        result: list[str] = []
        for rule in candidates:
            rule_id = rule.get("ruleId")
            request_rule = request_rules.get(rule_id)
            require(request_rule is not None, f"skill rule is absent from request: {scenario['scenarioId']}")
            budget = request_rule.get("budget", 0)
            require(budget > 0, f"skill rule budget is not positive: {scenario['scenarioId']}")
            options = rule.get("options")
            require(options, f"skill rule has no options: {scenario['scenarioId']}")
            require(
                all(0 < option.get("cost", 0) <= budget for option in options),
                f"skill option cost exceeds its rule budget: {scenario['scenarioId']}",
            )
            result.extend(option["combinationId"] for option in options)
        return result
    key = {
        "AcquiredTrait": "combinationId",
        "FacilityEvolution": "recipeId",
        "EquipmentChoice": "effectId",
        "EvolutionHistory": "effectId",
        "Persona": "candidateId",
    }[profile]
    return [candidate[key] for candidate in candidates]


def verify_profile_boundaries(scenarios: list[dict[str, Any]]) -> dict[str, int]:
    participant_rows = 0
    history_rows = 0
    trait_active_rows = 0
    trait_erased_rows = 0
    for scenario in scenarios:
        sid = scenario["scenarioId"]
        ids = candidate_ids(scenario)
        require(ids and len(ids) == len(set(ids)), f"candidate IDs missing/duplicated: {sid}")
        profile = scenario["profileId"]
        request = scenario["request"]
        if profile == "CharacterSkill":
            require(request.get("candidateCount") == len(request.get("rules", [])), f"skill candidate count differs: {sid}")
        elif profile == "AcquiredTrait":
            require(request.get("budget", 0) > 0, f"trait budget is not positive: {sid}")
            instances = scenario["authorityContext"].get("instances", [])
            trait_active_rows += int(any(not item.get("erased", False) for item in instances))
            trait_erased_rows += int(any(item.get("erased", False) for item in instances))
        elif profile == "EquipmentChoice":
            require(2 <= len(ids) <= 3, f"equipment candidate count outside 2..3: {sid}")
            participant_ids = request["requestSnapshot"]["participantIds"]
            entity_ids = {item["entityId"] for item in scenario["publicNarrativeContext"]["entities"]}
            require(set(participant_ids) <= entity_ids, f"equipment participant unresolved: {sid}")
            participant_rows += 1
        elif profile == "EvolutionHistory":
            locked = request["lockedRequest"]
            generation = locked["generation"]
            require(locked.get("effectBudget", 0) > 0, f"history budget is not positive: {sid}")
            entity_ids = {item["entityId"] for item in scenario["publicNarrativeContext"]["entities"]}
            require(set(locked["participantIds"]) <= entity_ids, f"history participant unresolved: {sid}")
            facts = {fact["factId"]: fact for fact in scenario["publicFacts"]}
            current: list[dict[str, Any]] = []
            prior: list[dict[str, Any]] = []
            for event in scenario["publicNarrativeContext"]["events"]:
                bucket = current if facts[event["factId"]]["domain"] == "evolution-current-event" else prior
                bucket.append(event)
            require(current and prior, f"history current/prior evidence missing: {sid}")
            require(all(event["generation"] == generation for event in current), f"history current generation differs: {sid}")
            require(all(event["generation"] is not None and event["generation"] < generation for event in prior), f"history prior generation is not earlier: {sid}")
            participant_rows += 1
            history_rows += 1
    require(trait_active_rows > 0 and trait_erased_rows > 0, "trait active/erased coverage is missing")
    return {
        "participantReferenceRows": participant_rows,
        "historyGenerationRows": history_rows,
        "traitActiveRows": trait_active_rows,
        "traitErasedRows": trait_erased_rows,
    }


def require_false_guards(document: Any, label: str) -> None:
    def walk(value: Any, path: str) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                child_path = f"{path}.{key}"
                if key in {"humanApprovalClaimed", "trainingEligible", "inferenceRun"}:
                    require(child is False, f"{label} has non-false guard {child_path}")
                if key == "modelInferenceRun":
                    require(child == "NOT_RUN", f"{label} claims model inference at {child_path}")
                walk(child, child_path)
        elif isinstance(value, list):
            for index, child in enumerate(value):
                walk(child, f"{path}[{index}]")

    walk(document, "$")


def verify(args: argparse.Namespace) -> dict[str, Any]:
    export_dir = args.export_dir.resolve()
    project_root = args.project_root.resolve()
    prior_evaluation_dir = args.prior_evaluation_dir.resolve()
    v26_dir = args.v26_dir.resolve()
    exclusion_path = args.exclusion_ledger.resolve()
    ai_root = args.narrative_ai_root.resolve()
    require(export_dir.is_dir(), f"export directory missing: {export_dir}")
    require(
        prior_evaluation_dir.is_dir(),
        f"prior evaluation directory missing: {prior_evaluation_dir}",
    )
    require(
        export_dir != prior_evaluation_dir,
        "V2 verification cannot target the immutable V1 directory",
    )
    require(v26_dir.is_dir(), f"v26 directory missing: {v26_dir}")
    require(exclusion_path.is_file(), f"exclusion ledger missing: {exclusion_path}")
    require(sha_file(exclusion_path) == EXPECTED_EXCLUSION_SHA, "exclusion ledger SHA differs")

    delivery_raw = (export_dir / "delivery_manifest.json").read_bytes()
    delivery = strict_json(delivery_raw)
    delivered = delivery_files(export_dir, delivery)
    docs = {name: strict_json(raw) for name, raw in delivered.items() if name.endswith(".json")}
    catalog = docs["catalog.json"]
    manifest = docs["manifest.json"]
    positives_doc = docs["scenarios_100.json"]
    negatives_doc = docs["negative_scenarios.json"]
    novelty = docs["novelty_review.json"]
    raw_results = docs["raw_test_results.json"]

    prior_delivery_raw = (prior_evaluation_dir / "delivery_manifest.json").read_bytes()
    require(
        sha_bytes(prior_delivery_raw) == EXPECTED_PRIOR_DELIVERY_SHA,
        "immutable V1 delivery manifest changed",
    )
    prior_delivery = strict_json(prior_delivery_raw)
    prior_delivered = delivery_files(prior_evaluation_dir, prior_delivery)
    prior_docs = {
        name: strict_json(raw)
        for name, raw in prior_delivered.items()
        if name.endswith(".json")
    }
    prior_positive = prior_docs["scenarios_100.json"]["scenarios"]

    catalog_without_hash = dict(catalog)
    catalog_hash = catalog_without_hash.pop("catalogHash")
    require(sha_bytes(canonical(catalog_without_hash)) == catalog_hash, "catalogHash differs")
    require(sha_bytes(delivered["catalog.json"]) == manifest.get("catalogBytesSha256"), "catalog byte hash differs")
    require(delivery.get("catalogHash") == catalog_hash, "delivery catalogHash differs")
    require(delivery.get("currentCommit") == catalog["source"]["gameCommit"], "current commit differs")
    require(delivery.get("inputDigest") == manifest.get("inputDigest"), "delivery inputDigest differs")
    require(delivery.get("uncommittedSourceHash") == manifest.get("uncommittedSourceHash"), "delivery dirty hash differs")
    require(delivery.get("humanApprovalClaimed") is False, "delivery claims human approval")
    source_counts = verify_source_manifest(project_root, manifest, catalog)

    for name in ("manifest.json", "negative_scenarios.json", "policy.json", "scenarios_100.json"):
        require(docs[name].get("catalogHash") == catalog_hash, f"catalog identity differs: {name}")
        require(docs[name].get("inputDigest") == manifest["inputDigest"], f"input identity differs: {name}")
    require(novelty.get("catalogHash") == catalog_hash, "novelty catalogHash differs")
    require(novelty.get("inputDigest") == manifest["inputDigest"], "novelty inputDigest differs")

    try:
        from jsonschema import Draft202012Validator
    except ImportError as error:
        raise VerificationError("jsonschema is required for scenario validation") from error
    schema = docs["scenario_schema.json"]
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)
    validator.validate(positives_doc)
    validator.validate(negatives_doc)

    positives = positives_doc["scenarios"]
    negatives = negatives_doc["scenarios"]
    require(len(positives) == 100, "positive count differs")
    require(Counter(row["profileId"] for row in positives) == Counter(PROFILE_COUNTS), "profile counts differ")
    require(len(negatives) == EXPECTED_NEGATIVE_COUNT, "negative count differs")
    all_rows = positives + negatives
    require(len({row["scenarioId"] for row in all_rows}) == len(all_rows), "duplicate scenario ID")
    require(len({row["semanticHash"] for row in positives}) == 100, "duplicate positive semantics")
    for row in all_rows:
        require(sha_bytes(canonical(semantic_projection(row))) == row["semanticHash"], f"semanticHash differs: {row['scenarioId']}")
        require(row["catalogHash"] == catalog_hash and row["inputDigest"] == manifest["inputDigest"], f"scenario identity differs: {row['scenarioId']}")

    old_positive = strict_json((v26_dir / "scenarios_100.json").read_bytes())["scenarios"]
    old_negative = strict_json((v26_dir / "negative_scenarios.json").read_bytes())["scenarios"]
    old_catalog = strict_json((v26_dir / "catalog.json").read_bytes())
    require(catalog_hash == old_catalog["catalogHash"], "evaluation changed catalog mechanics")
    exact_negative_rows, persona_negative_public_delta_rows = verify_negative_regressions(
        negatives,
        old_negative,
    )
    adapter, adapter_hash = load_adapter(ai_root)
    require(adapter_hash == EXPECTED_ADAPTER_SHA, "NarrativeAI adapter source changed")
    exclusion = strict_json(exclusion_path.read_bytes())
    require(exclusion.get("sourcePilotHash") == EXPECTED_SOURCE_PILOT_HASH, "exclusion source pilot differs")
    excluded_rows = exclusion.get("scenarios")
    require(isinstance(excluded_rows, list) and len(excluded_rows) == 100, "exclusion count differs")
    excluded_by_id = {row["scenarioId"]: row for row in excluded_rows}
    require(len(excluded_by_id) == 100, "duplicate exclusion scenario ID")
    old_by_id = {row["scenarioId"]: row for row in old_positive}
    require(set(old_by_id) == set(excluded_by_id), "v26/exclusion scenario IDs differ")
    for sid, row in old_by_id.items():
        projected_hash = sha_bytes(canonical(adapter.model_input(row, old_catalog, adapter_version=3)))
        require(projected_hash == excluded_by_id[sid]["inputHash"], f"v26/exclusion input hash differs: {sid}")
        story_hash = sha_bytes(canonical(public_story(row)))
        require(story_hash == excluded_by_id[sid]["exactPublicStoryHash"], f"v26/exclusion public-story hash differs: {sid}")

    excluded_ids = set(excluded_by_id)
    excluded_inputs = {row["inputHash"] for row in excluded_rows}
    excluded_stories = {row["exactPublicStoryHash"] for row in excluded_rows}
    new_ids = {row["scenarioId"] for row in positives}
    prior_by_id = {row["scenarioId"]: row for row in prior_positive}
    require(len(prior_by_id) == 100, "prior V1 has duplicate scenario IDs")
    require(new_ids == set(prior_by_id), "V2 scenario IDs differ from immutable V1")
    new_model_inputs_by_id = {
        row["scenarioId"]: adapter.model_input(row, catalog, adapter_version=3)
        for row in positives
    }
    new_inputs = {
        sha_bytes(canonical(model_input))
        for model_input in new_model_inputs_by_id.values()
    }
    new_stories = {sha_bytes(canonical(public_story(row))) for row in positives}
    require(len(new_inputs) == 100, "duplicate adapter3 input hash")
    require(len(new_stories) == 100, "duplicate public-story hash")
    require(not (new_ids & excluded_ids), "new scenario IDs overlap exclusion")
    require(not (new_inputs & excluded_inputs), "new adapter3 inputs overlap exclusion")
    require(not (new_stories & excluded_stories), "new public stories overlap exclusion")

    affected_rows = 0
    readable_event_rows = 0
    readable_event_count = 0
    persona_need_rows = 0
    identity_only_spoof_rejections = 0
    unchanged_rows = 0
    prior_persona_signatures = {
        sha_bytes(canonical(persona_masked_projection(
            adapter.model_input(row, prior_docs["catalog.json"], adapter_version=3))))
        for row in prior_positive
        if row["profileId"] == PERSONA_PROFILE
    }
    calibration_persona_signatures = {
        sha_bytes(canonical(persona_masked_projection(
            adapter.model_input(row, old_catalog, adapter_version=3))))
        for row in old_positive
        if row["profileId"] == PERSONA_PROFILE
    }
    audit_rows = novelty.get("rows")
    require(isinstance(audit_rows, list) and len(audit_rows) == 100, "novelty audit rows differ")
    audit_by_id = {row.get("scenarioId"): row for row in audit_rows}
    require(len(audit_by_id) == 100 and set(audit_by_id) == new_ids, "novelty/scenario IDs differ")
    prior_audit_rows = prior_docs["novelty_review.json"].get("rows")
    require(
        isinstance(prior_audit_rows, list) and len(prior_audit_rows) == 100,
        "prior novelty audit rows differ",
    )
    prior_audit_by_id = {row.get("scenarioId"): row for row in prior_audit_rows}
    require(
        len(prior_audit_by_id) == 100 and set(prior_audit_by_id) == new_ids,
        "prior novelty/scenario IDs differ",
    )
    new_persona_signatures: set[str] = set()
    for scenario in positives:
        sid = scenario["scenarioId"]
        prior = prior_by_id[sid]
        current_prompt_hash = scenario["authorityContext"].get("requestPromptHash")
        prior_prompt_hash = prior["authorityContext"].get("requestPromptHash")
        if current_prompt_hash is not None:
            require(
                current_prompt_hash
                == sha_bytes(scenario["request"]["prompt"].encode("utf-8")),
                f"V2 request prompt provenance differs: {sid}",
            )
        if prior_prompt_hash is not None:
            require(
                prior_prompt_hash
                == sha_bytes(prior["request"]["prompt"].encode("utf-8")),
                f"V1 request prompt provenance differs: {sid}",
            )
        require(
            scenario_mechanics(scenario) == scenario_mechanics(prior),
            f"V2 changed mechanics/request/response authority: {sid}",
        )
        current_metadata = audit_by_id[sid]
        prior_metadata = prior_audit_by_id[sid]
        for key in (
            "nearestCalibrationScenarioId",
            "scenarioFamilyId",
            "semanticOverlapDecision",
            "split",
        ):
            require(
                current_metadata.get(key) == prior_metadata.get(key),
                f"V2 changed frozen evaluation metadata '{key}': {sid}",
            )

        profile = scenario["profileId"]
        model_input = new_model_inputs_by_id[sid]
        if profile in EVENT_MEANING_PROFILES:
            readable_event_count += verify_event_public_semantics(scenario, model_input)
            readable_event_rows += 1
            affected_rows += 1
        elif profile == PERSONA_PROFILE:
            verify_persona_public_semantics(scenario, model_input)
            signature = sha_bytes(canonical(persona_masked_projection(model_input)))
            require(
                signature not in prior_persona_signatures,
                f"Persona remains equal to V1 after Name/Potential/IDs are masked: {sid}",
            )
            require(
                signature not in calibration_persona_signatures,
                f"Persona remains equal to V26 after Name/Potential/IDs are masked: {sid}",
            )
            require(signature not in new_persona_signatures, f"duplicate masked V2 Persona: {sid}")
            new_persona_signatures.add(signature)
            spoof = identity_only_persona_spoof(model_input)
            require(
                persona_masked_projection(spoof) == persona_masked_projection(model_input),
                f"identity-only Persona spoof escaped the masked novelty comparator: {sid}",
            )
            identity_only_spoof_rejections += 1
            persona_need_rows += 1
            affected_rows += 1
        else:
            require(
                semantic_projection(scenario) == semantic_projection(prior),
                f"non-target V2 scenario changed: {sid}",
            )
            unchanged_rows += 1
    require(affected_rows == 55, f"affected V2 row count differs: {affected_rows}")
    require(
        readable_event_rows == EXPECTED_READABLE_EVENT_ROWS,
        f"readable event row count differs: {readable_event_rows}",
    )
    require(
        readable_event_count == EXPECTED_READABLE_EVENT_COUNT,
        f"readable event count differs: {readable_event_count}",
    )
    require(persona_need_rows == 15, f"Persona current-need row count differs: {persona_need_rows}")
    require(
        identity_only_spoof_rejections == 15,
        f"identity-only Persona regression count differs: {identity_only_spoof_rejections}",
    )
    require(unchanged_rows == UNCHANGED_PROFILE_ROWS, f"unchanged V2 row count differs: {unchanged_rows}")

    family_profiles: dict[str, list[str]] = defaultdict(list)
    for scenario in positives:
        row = audit_by_id[scenario["scenarioId"]]
        require(row.get("profileId") == scenario["profileId"], f"novelty profile differs: {scenario['scenarioId']}")
        require(row.get("split") == EXPECTED_SPLIT, f"novelty split differs: {scenario['scenarioId']}")
        require(row.get("semanticOverlapDecision") == "distinct-context", f"novelty decision differs: {scenario['scenarioId']}")
        reason = row.get("semanticOverlapReason")
        require(isinstance(reason, str) and len(reason.strip()) >= 40, f"novelty reason is not substantive: {scenario['scenarioId']}")
        nearest = old_by_id.get(row.get("nearestCalibrationScenarioId"))
        require(nearest is not None and nearest["profileId"] == scenario["profileId"], f"nearest calibration row invalid: {scenario['scenarioId']}")
        story_hash = sha_bytes(canonical(public_story(scenario)))
        require(row.get("publicStoryHash") == story_hash, f"novelty public story hash differs: {scenario['scenarioId']}")
        require(row.get("exactCalibrationStoryMatch") is False, f"novelty row claims exact match: {scenario['scenarioId']}")
        family_id = row.get("scenarioFamilyId")
        require(isinstance(family_id, str) and family_id, f"family missing: {scenario['scenarioId']}")
        family_profiles[family_id].append(scenario["profileId"])
    require(len(family_profiles) == EXPECTED_FAMILY_COUNT, "family count differs")
    paired = 0
    for profiles in family_profiles.values():
        if len(profiles) == 2:
            require(sorted(profiles) == ["EquipmentChoice", "EvolutionHistory"], "non-equipment family crosses profiles")
            paired += 1
        else:
            require(len(profiles) == 1, "family contains unsupported related variants")
    require(paired == 15, "equipment/history family pairing differs")
    require(novelty.get("familyCount") == EXPECTED_FAMILY_COUNT, "novelty familyCount differs")
    require(novelty.get("semanticReviewCount") == 100, "novelty semanticReviewCount differs")
    require(novelty.get("unreviewedCount") == 0, "novelty has unreviewed rows")
    require(novelty.get("exactScenarioIdOverlap") == 0, "novelty ID overlap is nonzero")
    require(novelty.get("exactPublicStoryHashOverlap") == 0, "novelty public-story overlap is nonzero")
    require(novelty.get("exclusionLedgerSha256") == EXPECTED_EXCLUSION_SHA, "novelty exclusion SHA differs")
    require(novelty.get("exclusionSourcePilotHash") == EXPECTED_SOURCE_PILOT_HASH, "novelty source pilot differs")
    require_false_guards(novelty, "novelty review")
    require_false_guards(raw_results, "raw test results")
    require_false_guards(docs["policy.json"], "policy")
    profile_boundaries = verify_profile_boundaries(positives)

    return {
        "schemaVersion": 1,
        "status": "PASS",
        "sourceExport": str(export_dir),
        "priorEvaluationExport": str(prior_evaluation_dir),
        "priorDeliveryManifestSha256": sha_bytes(prior_delivery_raw),
        "deliveryManifestSha256": sha_bytes(delivery_raw),
        "catalogHash": catalog_hash,
        "inputDigest": manifest["inputDigest"],
        "uncommittedSourceHash": manifest["uncommittedSourceHash"],
        "currentCommit": catalog["source"]["gameCommit"],
        "profileCounts": dict(sorted(Counter(row["profileId"] for row in positives).items())),
        "positiveCount": 100,
        "negativeRegressionCount": 15,
        "exactNegativeSemanticRows": exact_negative_rows,
        "personaNegativePublicProjectionDeltaRows": persona_negative_public_delta_rows,
        "adapterVersion": 3,
        "adapterSourceSha256": adapter_hash,
        "uniqueAdapterInputHashes": len(new_inputs),
        "uniquePublicStoryHashes": len(new_stories),
        "affectedSemanticRows": affected_rows,
        "readableEventRows": readable_event_rows,
        "readableEventCount": readable_event_count,
        "personaCurrentNeedRows": persona_need_rows,
        "identityOnlyPersonaSpoofRejections": identity_only_spoof_rejections,
        "unchangedNonTargetRows": unchanged_rows,
        "excludedScenarioIdOverlap": len(new_ids & excluded_ids),
        "excludedAdapterInputHashOverlap": len(new_inputs & excluded_inputs),
        "excludedPublicStoryHashOverlap": len(new_stories & excluded_stories),
        "semanticReviewCount": 100,
        "unreviewedCount": 0,
        "familyCount": len(family_profiles),
        **profile_boundaries,
        **source_counts,
        "modelInferenceRun": "NOT_RUN",
        "naturalPlayDistribution": "NOT_RUN",
        "humanApprovalClaimed": False,
        "trainingEligible": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-dir", type=Path, required=True)
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--prior-evaluation-dir", type=Path, required=True)
    parser.add_argument("--v26-dir", type=Path, required=True)
    parser.add_argument("--exclusion-ledger", type=Path, required=True)
    parser.add_argument("--narrative-ai-root", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    try:
        result = verify(args)
    except (OSError, KeyError, TypeError, VerificationError, ValueError) as error:
        print(json.dumps({"status": "FAIL", "error": str(error)}, ensure_ascii=False))
        return 1
    if args.report is not None:
        write_new(args.report.resolve(), result)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
