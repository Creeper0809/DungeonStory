#!/usr/bin/env python3
"""Fail a DungeonStory wiki release artifact when public-page contracts leak."""

from __future__ import annotations

import argparse
import json
import posixpath
import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


class LinkCollector(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.links: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag not in {"a", "link", "script", "img"}:
            return
        for key, value in attrs:
            if key in {"href", "src"} and value:
                self.links.append(value)


def html_files(root: Path) -> list[Path]:
    return sorted(root.rglob("*.html"))


def is_external(value: str) -> bool:
    parsed = urlsplit(value)
    return bool(parsed.scheme or parsed.netloc) or value.startswith(("#", "mailto:", "tel:", "data:"))


def target_path(dist: Path, page: Path, value: str) -> Path | None:
    if is_external(value):
        return None
    path = unquote(urlsplit(value).path)
    if not path:
        return None
    if path.startswith("/"):
        candidate = dist / path.lstrip("/")
    else:
        candidate = page.parent / path
    normalized = Path(posixpath.normpath(candidate.as_posix()))
    try:
        normalized.relative_to(dist)
    except ValueError:
        raise ValueError(f"link escapes release artifact: {value}")
    if path.endswith("/") or normalized.suffix == "":
        return normalized / "index.html"
    return normalized


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit built DungeonStory wiki artifact.")
    parser.add_argument("--dist", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    args = parser.parse_args()
    dist = args.dist.resolve()
    model = args.model.resolve()
    failures: list[str] = []
    pages = html_files(dist)
    if not pages:
        failures.append("no HTML pages in release artifact")
    for page in pages:
        text = page.read_text(encoding="utf-8")
        for forbidden in ("Assets/", "docs_final/", "F:\\\\", "source_path", "asset_guid"):
            if forbidden in text:
                failures.append(f"forbidden source marker {forbidden!r} in {page.relative_to(dist)}")
        parser_instance = LinkCollector()
        parser_instance.feed(text)
        for link in parser_instance.links:
            try:
                target = target_path(dist, page, link)
            except ValueError as error:
                failures.append(f"{page.relative_to(dist)}: {error}")
                continue
            if target is not None and not target.exists():
                failures.append(f"broken release link {page.relative_to(dist)} -> {link}")

    warning_entities = []
    for path in sorted((model / "entities").rglob("*.json")):
        entity = json.loads(path.read_text(encoding="utf-8"))
        if entity.get("spoiler_tier") == "warning":
            warning_entities.append(entity)
    for entity in warning_entities:
        page = dist / "entry" / entity["kind"] / entity["slug"] / "index.html"
        if not page.exists():
            failures.append(f"spoiler page is missing: {entity['kind']}/{entity['slug']}")
            continue
        if entity["title"] in page.read_text(encoding="utf-8"):
            failures.append(f"spoiler title leaked into initial HTML: {entity['kind']}/{entity['slug']}")
    pagefind = dist / "pagefind" / "pagefind.js"
    if not pagefind.exists():
        failures.append("Pagefind artifact is missing")
    if failures:
        for failure in failures[:50]:
            print(f"publication audit failed: {failure}", file=sys.stderr)
        if len(failures) > 50:
            print(f"publication audit failed: {len(failures) - 50} additional failures", file=sys.stderr)
        return 1
    print(json.dumps({"status": "valid", "html_pages": len(pages), "spoiler_pages_checked": len(warning_entities)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
