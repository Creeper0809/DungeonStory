"""Validate the reviewed GAP-to-wiki publication projection."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path


START = "<!-- reviewed-rules:start -->"
END = "<!-- reviewed-rules:end -->"
INTERNAL_PATTERN = re.compile(
    r"(?:\.asset|\.cs\b|GAP-|\b[A-Z][A-Za-z]+"
    r"(?:Runtime|Service|Ability|Definition|Policy|Command|State|Id)\b|"
    r"\b[a-z]+:[a-z0-9-]+)"
)
AUDIT_TITLE_PATTERN = re.compile(r"누락|오류|불일치|과장|철회|잘못|부재|미표기|불명확|미반영|오인")


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    qa = root / "Artifacts/QA/wiki-system-audit-2026-09-05"
    register_path = qa / "missing-register.json"
    report_path = qa / "gap-wiki-publication.json"
    register = load(register_path)
    report = load(report_path)
    layout_path = root / report["editorialLayout"]
    editorial_layout = load(layout_path)["guides"]
    rows = {row["id"]: row for row in register["items"]}
    assignments = {row["id"]: row for row in report["assignments"]}
    actions = {row["id"]: row for row in report["documentationActions"]}
    errors: list[str] = []

    if report["sourceRegisterSha256"] != sha(register_path):
        errors.append("publication report uses a stale GAP register")
    if report.get("editorialLayoutSha256") != sha(layout_path):
        errors.append("publication report uses a stale editorial layout")
    navigation_path = root / report["guideNavigation"]
    navigation = load(navigation_path)
    redirects = {
        page["id"]: page["redirect_to"]
        for page in navigation["pages"]
        if page.get("redirect_to")
    }
    if report.get("guideNavigationSha256") != sha(navigation_path):
        errors.append("publication report uses stale guide navigation")
    if report.get("redirectedGuideTargets") != dict(sorted(redirects.items())):
        errors.append("publication report guide redirects are stale")
    if len(assignments) != report["publishedGapCount"]:
        errors.append("duplicate or mismatched published assignments")
    if len(actions) != report["documentationActionCount"]:
        errors.append("duplicate or mismatched documentation actions")
    if set(rows) != set(assignments) | set(actions) or set(assignments) & set(actions):
        errors.append("active GAP publication coverage is incomplete or overlapping")
    if report.get("unpublishedGapCount") != 0 or report.get("publicationComplete") is not True:
        errors.append("publication report is not marked complete")

    guide_sources: dict[str, str] = {}
    for relative, expected in report["outputGuideHashes"].items():
        path = root / relative
        if not path.is_file() or sha(path) != expected:
            errors.append(f"published guide changed after projection: {relative}")
            continue
        source = path.read_text(encoding="utf-8-sig")
        if source.count(START) != 1 or source.count(END) != 1:
            errors.append(f"managed block count is invalid: {relative}")
            continue
        guide_sources[path.stem] = source.split(START, 1)[1].split(END, 1)[0]

    cleaned_redirects = report.get("cleanedRedirectGuideHashes", {})
    if {Path(relative).stem for relative in cleaned_redirects} != set(redirects):
        errors.append("redirect guide cleanup coverage is incomplete")
    for relative, expected in cleaned_redirects.items():
        path = root / relative
        if not path.is_file() or sha(path) != expected:
            errors.append(f"redirect guide changed after cleanup: {relative}")
            continue
        source = path.read_text(encoding="utf-8-sig")
        if START in source or END in source:
            errors.append(f"redirect guide retains a managed publication block: {relative}")

    if set(guide_sources) != set(editorial_layout):
        errors.append("editorial layout and published guide coverage differ")

    layout_ids: list[str] = []
    for guide, layout in editorial_layout.items():
        target = guide_sources.get(guide, "")
        if target.count(f"## {layout['sectionTitle']}") != 1:
            errors.append(f"editorial section title missing or duplicated: {guide}")
        if not layout.get("lead", "").strip() or target.count(layout["lead"]) != 1:
            errors.append(f"editorial section lead missing or duplicated: {guide}")
        elif INTERNAL_PATTERN.search(layout["lead"]):
            errors.append(f"internal token published in editorial lead: {guide}")
        group_titles = [group["title"] for group in layout["groups"]]
        if len(group_titles) != len(set(group_titles)):
            errors.append(f"editorial group title duplicated: {guide}")
        for group in layout["groups"]:
            if target.count(f"### {group['title']}") != 1:
                errors.append(f"editorial group missing or duplicated: {guide}/{group['title']}")
            layout_ids.extend(group["gapIds"])
    if len(layout_ids) != len(set(layout_ids)) or set(layout_ids) != set(assignments):
        errors.append("editorial GAP grouping is incomplete or duplicated")

    clause_count = 0
    for gap_id, assignment in assignments.items():
        row = rows[gap_id]
        clauses = assignment["publishedClauses"]
        target = guide_sources.get(assignment["guide"], "")
        if not clauses:
            errors.append(f"published assignment has no clauses: {gap_id}")
        if assignment["clauseCount"] != len(clauses) or len(clauses) != len(row["wiki_publication"]["write"]):
            errors.append(f"clause count drift: {gap_id}")
        if INTERNAL_PATTERN.search(assignment["publicTitle"]) or AUDIT_TITLE_PATTERN.search(assignment["publicTitle"]):
            errors.append(f"internal audit wording published in title: {gap_id}")
        if target.count(f"### {assignment['publicTitle']}") != 0:
            errors.append(f"GAP-by-GAP heading leaked into target guide: {gap_id}")
        expected_group = assignment.get("editorialGroup", "")
        if not expected_group or target.count(f"### {expected_group}") != 1:
            errors.append(f"editorial group missing for published GAP: {gap_id}")
        for clause in clauses:
            clause_count += 1
            total = sum(section.count(clause) for section in guide_sources.values())
            if total != 1 or target.count(clause) != 1:
                errors.append(f"clause must occur exactly once in its target guide: {gap_id}")
            if INTERNAL_PATTERN.search(clause):
                errors.append(f"internal token published: {gap_id}")
        for excluded in row["wiki_publication"].get("do_not_write", []):
            if excluded and any(excluded in section for section in guide_sources.values()):
                errors.append(f"excluded clause published verbatim: {gap_id}")
    if clause_count != report["publishedClauseCount"]:
        errors.append("published clause total mismatch")

    truth = load(root / "wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-truth-observatory.json")["summary"]
    resource = load(root / "wiki/game-versions/0.0.1v/data/entities/facility/building-runtime-world-resource-node.json")["summary"]
    if "Building" in truth or "hand-authored" in truth or "3660 WU" not in truth:
        errors.append("GAP-111 entity correction is incomplete")
    if "Building" in resource or "Generated immutable" in resource or "18 WU" in resource:
        errors.append("GAP-163 entity correction is incomplete")

    result = {
        "status": "pass" if not errors else "fail",
        "activeGapCount": len(rows),
        "publishedGapCount": len(assignments),
        "documentationActionCount": len(actions),
        "publishedClauseCount": clause_count,
        "guideCount": len(guide_sources),
        "errors": errors,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
