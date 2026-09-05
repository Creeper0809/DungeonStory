#!/usr/bin/env python3
"""Reject duplicate detailed-health projections in the public wiki."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def load(path: Path) -> dict:
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def require_text(path: Path, expected: str) -> None:
    if expected not in path.read_text(encoding="utf-8"):
        raise ValueError(f"missing document-authority contract: {path} ({expected})")


def reject_text(path: Path, prohibited: str) -> None:
    if prohibited in path.read_text(encoding="utf-8"):
        raise ValueError(f"duplicate document-authority projection: {path} ({prohibited})")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate player-wiki document ownership.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--game-version", required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    version_root = root / "wiki" / "game-versions" / args.game_version
    need_component = root / "wiki" / "src" / "components" / "NeedReferenceContent.astro"
    anatomy_component = root / "wiki" / "src" / "components" / "AnatomyIndexContent.astro"
    health_hub_component = root / "wiki" / "src" / "components" / "HealthCommunityContent.astro"

    try:
        for path in (need_component, anatomy_component, health_hub_component):
            if not path.is_file():
                raise ValueError(f"missing document-authority source: {path}")

        # Health state may link to detailed authorities, but it may not render their catalogues or details.
        for symbol in (
            "getAnatomyReferences",
            "getAnatomyProfileGroups",
            "anatomyProfileCardTitle",
            "injuryStateReference",
            "baselineAnatomyReferences",
            "specialAnatomyGroups",
            "anatomy-quick-index",
            "injury-system-section",
        ):
            reject_text(need_component, symbol)
        require_text(need_component, "anatomyHref(undefined, archiveVersion)")
        require_text(need_component, "anatomyHref('injury-states', archiveVersion)")

        # The anatomy index remains the only catalogue projection for body parts and special anatomy.
        require_text(anatomy_component, "anatomy-index-grid")
        require_text(anatomy_component, "anatomy-profile-groups--index")
        reject_text(health_hub_component, "getAnatomyReferences")

        need_document = load(version_root / "content" / "need-references.json")
        health = next((item for item in need_document.get("references", []) if item.get("id") == "health"), None)
        if not health:
            raise ValueError("health need reference is missing")
        health_text = json.dumps(health, ensure_ascii=False)
        for phrase in ("신체 부위", "기관 상태", "다친 부위", "부위별 체력", "종족별 특수 부위", "인간형 기본 구조"):
            if phrase in health_text:
                raise ValueError(f"health need reference duplicates anatomy detail: {phrase}")

        anatomy_documents = [
            load(version_root / "content" / "anatomy-references.json"),
            load(version_root / "content" / "special-anatomy-references.json"),
        ]
        anatomy_ids = {item.get("id") for document in anatomy_documents for item in document.get("references", [])}
        if "injury-states" not in anatomy_ids:
            raise ValueError("injury-state detail owner is missing")

        print(json.dumps({
            "status": "valid",
            "health_state_owner": "/needs/health/",
            "anatomy_owner": "/health/",
            "injury_owner": "/health/injury-states/",
        }, ensure_ascii=False))
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"wiki document-authority validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
