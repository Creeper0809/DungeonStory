#!/usr/bin/env python3
"""Merge completed human-review CSV files into reviewed JSONL artifacts."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

from verify_dataset import jsonl, verify_record


def load_by_id(path: Path) -> dict[str, dict]:
    return {record["exampleId"]: record for _, record in jsonl(path)}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", type=Path, default=Path("Artifacts/Training/V25"))
    parser.add_argument("--review-csv", type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--allow-partial", action="store_true")
    args = parser.parse_args()
    dataset = args.dataset.resolve()
    repo_root = Path(__file__).resolve().parents[2]
    keys = {record["reviewId"]: record for _, record in jsonl(dataset / "review/review_key_8000.jsonl.gz")}
    source = load_by_id(dataset / "preference_review_candidates_6000.jsonl.gz")
    source.update(load_by_id(dataset / "held_out_review_candidates_2000.jsonl.gz"))
    approved = []
    dropped = []
    review_paths = [args.review_csv.resolve()] if args.review_csv else sorted((dataset / "review").glob("review_[0-9]*_[0-9]*.csv"))
    row_number = 1
    for review_path in review_paths:
      with review_path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            row_number += 1
            review_id = row["review_id"].strip()
            verdict = row["verdict"].strip().upper()
            if not verdict:
                if args.allow_partial:
                    continue
                raise SystemExit(f"{review_path}: row {row_number} ({review_id}) has no verdict")
            if review_id not in keys:
                raise SystemExit(f"row {row_number}: unknown review_id {review_id}")
            key = keys[review_id]
            record = dict(source[key["exampleId"]])
            if verdict == "DROP":
                dropped.append({"reviewId": review_id, "exampleId": record["exampleId"], "reason": row["issue_tags"].strip(), "note": row["reviewer_note"].strip()})
                continue
            if verdict == "APPROVE":
                selected = row["selected_candidate"].strip().upper()
                if selected not in ("A", "B"):
                    raise SystemExit(f"row {row_number} ({review_id}) requires selected_candidate A or B")
                record["chosen"] = row["candidate_a"] if selected == "A" else row["candidate_b"]
                record["provenance"] = "human_approved"
            elif verdict == "REWRITE":
                rewrite = row["rewrite"].strip()
                if not rewrite:
                    raise SystemExit(f"row {row_number} ({review_id}) requires rewrite JSON")
                json.loads(rewrite)
                record["chosen"] = rewrite
                record["provenance"] = "human_rewritten"
            else:
                raise SystemExit(f"row {row_number} ({review_id}) has invalid verdict {verdict}")
            verify_record(record, repo_root, set())
            approved.append(record)
    if not args.allow_partial and len(approved) + len(dropped) != 8000:
        raise SystemExit(f"review coverage is {len(approved) + len(dropped)}/8000")
    train = [record for record in approved if record["split"] == "preference_train"]
    held = [record for record in approved if record["split"] == "held_out"]
    out_dir = args.output_dir.resolve() if args.output_dir else dataset / "reviewed"
    out_dir.mkdir(parents=True, exist_ok=True)
    for path, records in ((out_dir / "human_reviewed_preference.jsonl", train), (out_dir / "human_reviewed_held_out.jsonl", held)):
        with path.open("w", encoding="utf-8", newline="\n") as stream:
            for record in records:
                stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n")
    (out_dir / "dropped.json").write_text(json.dumps(dropped, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"approvedPreference": len(train), "approvedHeldOut": len(held), "dropped": len(dropped)}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
