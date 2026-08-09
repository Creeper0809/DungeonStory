#!/usr/bin/env python3
"""Fail-closed validation for generated V25 corpus and review artifacts."""

from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import json
import re
from collections import Counter
from pathlib import Path


REQUIRED_RECORD = {
    "exampleId", "scenarioFamilyId", "category", "profileId", "cultureStyleId",
    "factPacket", "motifPacket", "prompt", "chosen", "rejected", "negativeType", "provenance", "sourceAssetIds",
}
NEGATIVE_TYPES = {"generic_safe", "fact_distortion", "motif_listing"}
PROFILE_REQUIRED = {
    "CharacterSkill": {"candidates", "usedMotifIds", "usedCharacterFactIds"},
    "Persona": {"traitName", "flavorText", "selfCareMultiplier", "curiosityMultiplier", "shoppingMultiplier", "patienceMultiplier", "hungerCurveMultiplier", "funCurveMultiplier", "moodCurveMultiplier", "preferredFacilityTags", "usedMotifIds", "usedCharacterFactIds"},
    "MacroGoal": {"macroGoal", "reason", "targetFacilityId", "targetFacilityTag", "validSeconds", "usedMotifIds", "usedCharacterFactIds"},
    "MoodImpulse": {"moodImpulse", "strength", "targetFacilityId", "targetFacilityTag", "reason", "validSeconds", "usedMotifIds", "usedCharacterFactIds"},
    "FacilityEvolution": {"facilityIdentitySummary", "proposalIds", "reasons", "rejectedHints", "rejectedHintText", "mutationTagSuggestions", "flavorText", "confidence", "usedMotifIds", "usedCharacterFactIds"},
    "EvolutionHistory": {"requestKey", "targetPersistentId", "nodeId", "parentNodeId", "effectId", "effectBudget", "evidenceIds", "displayName", "description", "historyReason", "usedMotifIds", "usedCharacterFactIds"},
    "SocialRumor": {"rumorType", "targetType", "targetFacilityId", "targetFacilityTag", "targetCharacterId", "targetCharacterName", "sentiment", "summary", "spreadChance", "trustImpact", "validSeconds", "usedMotifIds", "usedCharacterFactIds"},
    "CharacterRecord": {"line", "usedMotifIds", "usedCharacterFactIds"},
    "MultiPerspective": {"eventId", "perspectives", "usedMotifIds", "usedCharacterFactIds"},
    "BubbleLine": {"line"},
}
REFERENCE = re.compile(r"^[FM][0-9]{2}$")
RULE_LINE = re.compile(r"고정 규칙 필드\(문자열과 수치를 그대로 복사\): (\{.*\})")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def jsonl(path: Path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                yield line_number, json.loads(line)
            except json.JSONDecodeError as error:
                raise AssertionError(f"{path}:{line_number}: invalid JSON: {error}") from error


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def parse_rule_contract(record: dict) -> dict:
    match = RULE_LINE.search(record["prompt"])
    require(match is not None, f"{record['exampleId']}: fixed rule contract missing")
    return json.loads(match.group(1))


def verify_rule_copy(record: dict, chosen: dict) -> None:
    profile = record["profileId"]
    rule = parse_rule_contract(record)
    if profile == "MultiPerspective":
        require(chosen["eventId"] == rule["eventId"], f"{record['exampleId']}: eventId changed")
        require([item["viewpointCharacterId"] for item in chosen["perspectives"]] == rule["viewpointCharacterIds"], f"{record['exampleId']}: viewpoints changed")
    elif profile == "CharacterSkill":
        candidate = chosen["candidates"][0]
        for key in ("index", "trigger", "target", "ultimateDomain", "cooldownTurns", "combinationId"):
            require(candidate[key] == rule[key], f"{record['exampleId']}: skill {key} changed")
        require(candidate["modules"][0] == rule["module"], f"{record['exampleId']}: skill module changed")
    elif profile == "EvolutionHistory":
        for key in ("requestKey", "targetPersistentId", "nodeId", "parentNodeId", "effectId", "effectBudget", "evidenceIds"):
            require(chosen[key] == rule[key], f"{record['exampleId']}: history {key} changed")
    elif profile == "FacilityEvolution":
        require(set(chosen["proposalIds"]) <= set(rule["allowedProposalIds"]), f"{record['exampleId']}: illegal proposal")
        require(set(chosen["mutationTagSuggestions"]) <= set(rule["allowedMutationTags"]), f"{record['exampleId']}: illegal mutation tag")
    elif profile == "Persona":
        for key, value in rule.items():
            require(chosen[key] == value, f"{record['exampleId']}: persona {key} changed")
    elif profile == "SocialRumor":
        for key, value in rule.items():
            require(chosen[key] == value, f"{record['exampleId']}: rumor {key} changed")
    elif profile in ("MacroGoal", "MoodImpulse"):
        for key, value in rule.items():
            require(chosen[key] == value, f"{record['exampleId']}: {profile} {key} changed")


def verify_record(record: dict, repo_root: Path, seen_ids: set[str]) -> None:
    missing = REQUIRED_RECORD - set(record)
    require(not missing, f"record missing keys: {sorted(missing)}")
    example_id = record["exampleId"]
    require(example_id not in seen_ids, f"duplicate exampleId: {example_id}")
    seen_ids.add(example_id)
    require(record["profileId"] in PROFILE_REQUIRED, f"{example_id}: unknown profile")
    require(record["provenance"] in ("rule_generated", "human_rewritten", "human_approved"), f"{example_id}: invalid provenance")
    facts = record["factPacket"]
    motifs = record["motifPacket"]
    require(1 <= len(facts) <= 24 and 1 <= len(motifs) <= 12, f"{example_id}: packet bounds")
    fact_refs = {item["ref"] for item in facts}
    motif_refs = {item["ref"] for item in motifs}
    require(len(fact_refs) == len(facts) and all(REFERENCE.fullmatch(value) and value.startswith("F") for value in fact_refs), f"{example_id}: fact refs")
    require(len(motif_refs) == len(motifs) and all(REFERENCE.fullmatch(value) and value.startswith("M") for value in motif_refs), f"{example_id}: motif refs")
    require(all(item["visibility"] in ("speaker", "player", "public") for item in facts), f"{example_id}: visibility")
    require(all(item["text"] and item["stableId"] for item in facts + motifs), f"{example_id}: empty packet entry")
    for source in record["sourceAssetIds"]:
        require((repo_root / source).is_file(), f"{example_id}: missing source asset {source}")
    try:
        chosen = json.loads(record["chosen"])
        rejected = json.loads(record["rejected"])
    except json.JSONDecodeError as error:
        raise AssertionError(f"{example_id}: invalid completion JSON: {error}") from error
    missing_profile = PROFILE_REQUIRED[record["profileId"]] - set(chosen)
    require(not missing_profile, f"{example_id}: chosen missing {sorted(missing_profile)}")
    require(record["negativeType"] in NEGATIVE_TYPES, f"{example_id}: invalid hard-negative type")
    rejected_missing = PROFILE_REQUIRED[record["profileId"]] - set(rejected)
    require(not rejected_missing, f"{example_id}: rejected missing {sorted(rejected_missing)}")
    used_facts = chosen.get("usedCharacterFactIds", [])
    used_motifs = chosen.get("usedMotifIds", [])
    require(len(used_facts) == len(set(used_facts)) <= 4 and set(used_facts) <= fact_refs, f"{example_id}: invalid used facts")
    require(len(used_motifs) == len(set(used_motifs)) <= 3 and set(used_motifs) <= motif_refs, f"{example_id}: invalid used motifs")
    if record["profileId"] not in ("BubbleLine", "MacroGoal", "MoodImpulse", "SocialRumor"):
        require(used_facts and used_motifs, f"{example_id}: persistent prose is ungrounded")
    verify_rule_copy(record, chosen)
    verify_rule_copy(record, rejected)


def collect(path: Path, repo_root: Path) -> dict:
    seen: set[str] = set()
    families: set[str] = set()
    categories = Counter()
    profiles = Counter()
    splits = Counter()
    negative_types = Counter()
    count = 0
    for _, record in jsonl(path):
        verify_record(record, repo_root, seen)
        count += 1
        families.add(record["scenarioFamilyId"])
        categories[record["category"]] += 1
        profiles[record["profileId"]] += 1
        negative_types[record["negativeType"]] += 1
        if record.get("split"):
            splits[record["split"]] += 1
    return {"count": count, "ids": seen, "families": families, "categories": categories, "profiles": profiles, "splits": splits, "negativeTypes": negative_types}


def verify_review(output: Path, expected: int) -> None:
    review_dir = output / "review"
    chunk_paths = sorted(path for path in review_dir.glob("review_[0-9]*_[0-9]*.csv") if "master" not in path.name)
    require(len(chunk_paths) == 8, f"expected 8 review chunks, got {len(chunk_paths)}")
    seen = set()
    split_counts = Counter()
    for path in chunk_paths:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream))
        require(len(rows) == 1000, f"{path}: expected 1000 rows")
        for row in rows:
            require(row["review_id"] not in seen, f"duplicate review id {row['review_id']}")
            seen.add(row["review_id"])
            require(not row["verdict"] and not row["selected_candidate"] and not row["rewrite"], f"{row['review_id']}: falsely pre-reviewed")
            require(row["candidate_a"] != row["candidate_b"], f"{row['review_id']}: candidates identical")
            split_counts[row["split"]] += 1
    require(len(seen) == expected, f"review rows={len(seen)} expected={expected}")
    require(split_counts == Counter({"preference_train": 6000, "held_out": 2000}), f"review split counts={split_counts}")
    require(sum(1 for _ in jsonl(review_dir / "review_key_8000.jsonl.gz")) == expected, "review key count mismatch")


def verify_manifest(output: Path) -> dict:
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    require(manifest["humanApprovalClaimed"] is False, "manifest falsely claims human approval")
    audit = json.loads((output / "corpus_audit.json").read_text(encoding="utf-8"))
    require(all(audit["gate"].values()), f"corpus quality gate failed: {audit['gate']}")
    require(audit == manifest["corpusAudit"], "manifest corpus audit differs from report")
    for entry in manifest["files"]:
        path = output / entry["path"]
        require(path.is_file(), f"manifest file missing: {path}")
        require(path.stat().st_size == entry["bytes"], f"manifest size mismatch: {path}")
        require(sha256(path) == entry["sha256"], f"manifest hash mismatch: {path}")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, default=Path("Artifacts/Training/V25"))
    args = parser.parse_args()
    output = args.input.resolve()
    repo_root = Path(__file__).resolve().parents[2]
    manifest = verify_manifest(output)
    raw = collect(output / "raw_scenarios_50000.jsonl.gz", repo_root)
    filtered = collect(output / "filtered_pool_40000.jsonl.gz", repo_root)
    train = collect(output / "sft_train_candidates_38000.jsonl.gz", repo_root)
    preference = collect(output / "preference_review_candidates_6000.jsonl.gz", repo_root)
    held = collect(output / "held_out_review_candidates_2000.jsonl.gz", repo_root)
    require(raw["count"] == 50000 and filtered["count"] == 40000 and train["count"] == 38000 and preference["count"] == 6000 and held["count"] == 2000, "top-level count contract failed")
    require(raw["negativeTypes"] == Counter({"generic_safe": 25000, "fact_distortion": 15000, "motif_listing": 10000}), f"raw hard-negative mix={raw['negativeTypes']}")
    require(all(filtered["negativeTypes"][key] > 0 for key in NEGATIVE_TYPES), f"filtered hard-negative coverage={filtered['negativeTypes']}")
    require(held["families"].isdisjoint(train["families"]), "held-out family leakage into SFT candidates")
    require(preference["ids"] <= train["ids"], "preference records missing from SFT candidate set")
    require(held["ids"].isdisjoint(train["ids"]), "held-out example leakage into SFT candidates")
    require(filtered["ids"] == train["ids"] | held["ids"], "filtered partition is incomplete")
    require(sum(1 for _ in jsonl(output / "trl_sft_train_38000.jsonl.gz")) == 38000, "TRL projection count mismatch")
    verify_review(output, 8000)
    report = {
        "passed": True,
        "counts": manifest["counts"],
        "rawCategories": dict(raw["categories"]),
        "filteredProfiles": dict(filtered["profiles"]),
        "heldOutFamilyLeakage": 0,
        "humanApprovalClaimed": False,
        "manifestFilesVerified": len(manifest["files"]),
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
