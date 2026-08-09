#!/usr/bin/env python3
"""Deterministic V25 diversity/grounding release gate.

Consumes JSONL records containing reference/chosen/generated plus validation
flags. It never calls a model or network service, so held-out evaluation can be
reproduced in CI and compared across SFT/DPO candidates.
"""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import math
import pathlib
import re
import statistics
import sys
from typing import Iterable

TOKEN = re.compile(r"[가-힣A-Za-z0-9]+")


def tokens(text: str) -> list[str]:
    return [value.lower() for value in TOKEN.findall(text or "")]


def entropy(values: Iterable[str]) -> float:
    counts = collections.Counter(values)
    total = sum(counts.values())
    if total == 0:
        return 0.0
    return -sum((count / total) * math.log2(count / total) for count in counts.values())


def distinct_n(sequences: list[list[str]], n: int) -> float:
    grams = []
    for sequence in sequences:
        grams.extend(tuple(sequence[i : i + n]) for i in range(max(0, len(sequence) - n + 1)))
    return len(set(grams)) / len(grams) if grams else 0.0


def ngram_set(sequence: list[str], n: int) -> set[tuple[str, ...]]:
    return {tuple(sequence[i : i + n]) for i in range(max(0, len(sequence) - n + 1))}


def mean_self_bleu_proxy(sequences: list[list[str]]) -> float:
    # Stable pairwise trigram Jaccard proxy; lower means more diverse.
    if len(sequences) < 2:
        return 0.0
    signatures = [ngram_set(sequence, 3) for sequence in sequences]
    scores = []
    for index, current in enumerate(signatures):
        others = set().union(*(value for offset, value in enumerate(signatures) if offset != index))
        union = current | others
        scores.append(len(current & others) / len(union) if union else 0.0)
    return statistics.fmean(scores)


def load(path: pathlib.Path) -> list[dict]:
    records = []
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError as error:
                raise ValueError(f"{path}:{line_number}: {error}") from error
    return records


def metrics(records: list[dict]) -> dict:
    sequences = [tokens(record.get("generated", "")) for record in records]
    all_tokens = [token for sequence in sequences for token in sequence]
    total = max(1, len(records))
    exact_names = [record.get("generatedName", "").strip() for record in records]
    name_roots = ["".join(tokens(name)[:1])[:4] for name in exact_names if name]
    culture_tokens: dict[str, list[str]] = collections.defaultdict(list)
    for record, sequence in zip(records, sequences):
        culture_tokens[str(record.get("cultureId", "unknown"))].extend(sequence)
    culture_concentrations = []
    for values in culture_tokens.values():
        counts = collections.Counter(values)
        culture_concentrations.append(max(counts.values()) / len(values) if values else 0.0)
    duplicate_names = sum(count - 1 for count in collections.Counter(exact_names).values() if count > 1)
    return {
        "samples": len(records),
        "structure_parse_rate": sum(bool(r.get("structureValid")) for r in records) / total,
        "grounding_rate": sum(bool(r.get("groundingValid")) for r in records) / total,
        "fatal_contradictions": sum(bool(r.get("fatalContradiction")) for r in records),
        "invented_proper_nouns_or_numbers": sum(bool(r.get("inventedFact")) for r in records),
        "pass_rate": sum(r.get("verdict") in ("StrongPass", "SoftPass") for r in records) / total,
        "fallback_rate": sum(bool(r.get("fallback")) for r in records) / total,
        "exact_duplicate_names": duplicate_names,
        "unique_name_root_rate": len(set(name_roots)) / len(name_roots) if name_roots else 0.0,
        "human_preference_rate": sum(bool(r.get("humanPreferred")) for r in records) / total,
        "culture_top_token_concentration": statistics.fmean(culture_concentrations)
        if culture_concentrations else 0.0,
        "near_duplicate_rate": sum(bool(r.get("nearDuplicate")) for r in records) / total,
        "viewpoint_suffix_only_rate": sum(bool(r.get("viewpointSuffixOnly")) for r in records) / total,
        "vocabulary_entropy": entropy(all_tokens),
        "distinct_2": distinct_n(sequences, 2),
        "distinct_3": distinct_n(sequences, 3),
        "self_bleu_proxy": mean_self_bleu_proxy(sequences),
    }


def regression(candidate: dict, baseline: dict) -> list[str]:
    failures = []
    grounding_gain = candidate["grounding_rate"] - baseline["grounding_rate"]
    preference_gain = candidate["human_preference_rate"] - baseline["human_preference_rate"]
    if grounding_gain < 0.005 and preference_gain < 0.03:
        failures.append("DPO must improve grounding by 0.5pp or human preference by 3pp")
    if candidate["vocabulary_entropy"] < baseline["vocabulary_entropy"] * 0.98:
        failures.append("vocabulary entropy fell by more than 2%")
    for key in ("distinct_2", "distinct_3"):
        if candidate[key] < baseline[key] * 0.97:
            failures.append(f"{key} fell by more than 3%")
    if candidate["self_bleu_proxy"] > baseline["self_bleu_proxy"] * 1.03:
        failures.append("Self-BLEU proxy worsened by more than 3%")
    if candidate["near_duplicate_rate"] > baseline["near_duplicate_rate"] + 0.01:
        failures.append("near-duplicate rate increased by more than 1 percentage point")
    if candidate["unique_name_root_rate"] < baseline["unique_name_root_rate"] * 0.97:
        failures.append("unique name-root rate fell by more than 3%")
    if candidate["culture_top_token_concentration"] > baseline["culture_top_token_concentration"]:
        failures.append("culture-specific safe-vocabulary concentration worsened")
    return failures


def absolute_gate(result: dict) -> list[str]:
    failures = []
    expected = {
        "structure_parse_rate": 1.0,
        "grounding_rate": 1.0,
    }
    for key, minimum in expected.items():
        if result[key] < minimum:
            failures.append(f"{key} must be {minimum:.0%}")
    if result["fatal_contradictions"] != 0 or result["invented_proper_nouns_or_numbers"] != 0:
        failures.append("fatal contradictions and invented facts must both be zero")
    if result["pass_rate"] < 0.99:
        failures.append("StrongPass + SoftPass must be at least 99%")
    if result["fallback_rate"] > 0.01:
        failures.append("fallback rate must be at most 1%")
    if result["exact_duplicate_names"] != 0:
        failures.append("exact duplicate names must be zero")
    if result["near_duplicate_rate"] > 0.02:
        failures.append("near-duplicate rate must be at most 2%")
    if result["viewpoint_suffix_only_rate"] > 0.05:
        failures.append("viewpoint suffix-only rate must be at most 5%")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("candidate", type=pathlib.Path)
    parser.add_argument("--baseline", type=pathlib.Path)
    parser.add_argument("--report", type=pathlib.Path)
    args = parser.parse_args()
    candidate = metrics(load(args.candidate))
    failures = absolute_gate(candidate)
    baseline = None
    if args.baseline:
        baseline = metrics(load(args.baseline))
        failures.extend(regression(candidate, baseline))
    report = {
        "candidate_sha256": hashlib.sha256(args.candidate.read_bytes()).hexdigest(),
        "candidate": candidate,
        "baseline": baseline,
        "passed": not failures,
        "failures": failures,
    }
    rendered = json.dumps(report, ensure_ascii=False, indent=2)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
