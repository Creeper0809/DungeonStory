#!/usr/bin/env python3
"""Validate the generated DungeonStory system knowledge indexes."""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MARKDOWN_LINK = re.compile(r"\[[^\]]*\]\(([^)]+)\)")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default="docs_final/knowledge-base")
    parser.add_argument("--content-db", default="docs_final/content-db")
    return parser.parse_args(argv)


def project_path(value: str) -> Path:
    candidate = Path(value)
    resolved = candidate.resolve() if candidate.is_absolute() else (ROOT / candidate).resolve()
    resolved.relative_to(ROOT.resolve())
    return resolved


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    knowledge_root = project_path(args.root)
    content_db = project_path(args.content_db)
    failures: list[str] = []

    summary = json.loads((knowledge_root / "knowledge-base-summary.json").read_text(encoding="utf-8"))
    content_manifest = json.loads((content_db / "generation-manifest.json").read_text(encoding="utf-8"))
    if summary["content_source_digest"] != content_manifest["source_digest"]:
        failures.append("content-source-digest-mismatch")

    architecture = read_csv(knowledge_root / "systems" / "architecture-system-index.csv")
    architecture_sources = read_csv(knowledge_root / "systems" / "architecture-source-map.csv")
    code_systems = read_csv(knowledge_root / "code" / "system-index.csv")
    state_authority = read_csv(knowledge_root / "authority" / "state-authority.csv")
    implementation = read_csv(knowledge_root / "authority" / "implementation-status.csv")
    documents = read_csv(knowledge_root / "authority" / "document-index.csv")
    content_impact = read_csv(knowledge_root / "relations" / "content-impact.csv")
    system_content = read_csv(knowledge_root / "relations" / "system-content-relations.csv")
    research_unlocks = read_csv(knowledge_root / "relations" / "research-unlocks.csv")
    content_types = read_csv(content_db / "content-type-index.csv")

    count_checks = {
        "architecture_system_count": len(architecture),
        "architecture_source_link_count": len(architecture_sources),
        "code_system_count": len(code_systems),
        "state_authority_count": len(state_authority),
        "implementation_status_count": len(implementation),
        "document_count": len(documents),
        "system_content_relation_count": len(system_content),
        "content_impact_type_count": len(content_impact),
        "research_unlock_relation_count": len(research_unlocks),
    }
    for field, actual in count_checks.items():
        if int(summary[field]) != actual:
            failures.append(f"count:{field}:{summary[field]}!={actual}")

    if len(architecture) != 19:
        failures.append(f"architecture-system-contract:{len(architecture)}")
    if len(content_impact) != len(content_types):
        failures.append(f"content-type-coverage:{len(content_impact)}!={len(content_types)}")
    if not architecture_sources:
        failures.append("architecture-source-map-empty")
    if not state_authority:
        failures.append("state-authority-empty")
    if not implementation:
        failures.append("implementation-status-empty")
    unlock_content_types = {
        content_type
        for row in research_unlocks
        for content_type in row["content_type"].split("; ")
    }
    if "BuildingSO" not in unlock_content_types or "ProductionRecipeSO" not in unlock_content_types:
        failures.append("research-unlock-content-coverage")

    for row in architecture:
        if not (ROOT / row["document"]).exists():
            failures.append(f"missing-architecture-document:{row['document']}")
    for row in architecture_sources:
        if row["exists"] != "true" or not (ROOT / row["linked_source"]).exists():
            failures.append(f"missing-architecture-source:{row['linked_source']}")
    for row in code_systems:
        source_index = knowledge_root / row["source_index"]
        if not source_index.exists():
            failures.append(f"missing-code-index:{row['source_index']}")
            continue
        partition = read_csv(source_index)
        if len(partition) != int(row["source_file_count"]):
            failures.append(f"code-index-count:{row['code_system']}")
        for source in partition:
            if not (ROOT / source["source_path"]).exists():
                failures.append(f"missing-code-source:{source['source_path']}")

    expected_types = {row["content_type"] for row in content_types}
    impact_types = {row["content_type"] for row in content_impact}
    if impact_types != expected_types:
        failures.append("content-impact-type-set-mismatch")
    for row in content_impact:
        for field in (
            "content_csv",
            "fields_csv",
            "outgoing_relations_csv",
            "incoming_relations_csv",
            "code_consumers_csv",
            "type_document",
        ):
            if not (ROOT / row[field]).exists():
                failures.append(f"missing-content-link:{row['content_type']}:{field}:{row[field]}")
    for row in system_content:
        if row["content_type"] not in expected_types:
            failures.append(f"unknown-content-type:{row['content_type']}")
        if not (ROOT / row["consumer_csv"]).exists():
            failures.append(f"missing-consumer-csv:{row['consumer_csv']}")

    for row in state_authority:
        for field in (
            "state_family",
            "runtime_write_authority",
            "allowed_entry",
            "read_projection",
            "persistence_restore",
            "forbidden_bypass",
        ):
            if not row[field].strip():
                failures.append(f"empty-state-authority:{row['state_family']}:{field}")
    for row in documents:
        if not (ROOT / row["document"]).exists():
            failures.append(f"missing-document:{row['document']}")
        if int(row["broken_local_link_count"]) != 0:
            failures.append(f"broken-authority-links:{row['document']}:{row['broken_local_link_count']}")

    for document in knowledge_root.rglob("*.md"):
        text = document.read_text(encoding="utf-8")
        for target in MARKDOWN_LINK.findall(text):
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            clean = target.split("#", 1)[0].strip()
            if clean.startswith("<") and clean.endswith(">"):
                clean = clean[1:-1]
            if clean and not (document.parent / clean).resolve().exists():
                failures.append(f"broken-generated-link:{document.relative_to(ROOT)}:{target}")

    validation = {
        "architecture_systems": len(architecture),
        "architecture_source_links": len(architecture_sources),
        "code_systems": len(code_systems),
        "code_source_files": int(summary["code_source_file_count"]),
        "state_authority_rows": len(state_authority),
        "implementation_status_rows": len(implementation),
        "documents": len(documents),
        "content_types": len(content_impact),
        "system_content_relations": len(system_content),
        "research_unlock_relations": len(research_unlocks),
        "failure_count": len(failures),
        "failures": failures[:100],
    }
    print(json.dumps(validation, ensure_ascii=False, indent=2))
    return 0 if not failures else 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
