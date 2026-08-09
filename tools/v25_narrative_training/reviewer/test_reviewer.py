#!/usr/bin/env python3
"""Focused tests for the local narrative review workbench."""

from __future__ import annotations

import csv
import gzip
import hashlib
import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
import re
import subprocess
import sys
from html.parser import HTMLParser
from pathlib import Path

from server import DEFAULT_DATASET, STATIC_ROOT, TOOL_ROOT, ReviewRepository, analyze_candidate, build_server


class IdCollector(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = []
        self.external_urls = []

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if "id" in values:
            self.ids.append(values["id"])
        for key in ("src", "href"):
            if values.get(key, "").startswith(("http://", "https://", "//")):
                self.external_urls.append(values[key])


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class ReviewerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix="dungeonstory-reviewer-test-")
        temp_root = Path(cls.temp.name)
        cls.source_paths = sorted((DEFAULT_DATASET / "review").glob("review_[0-9]*_[0-9]*.csv"))
        cls.source_hashes = {path: file_hash(path) for path in cls.source_paths}
        cls.repo = ReviewRepository(DEFAULT_DATASET, temp_root / "state.json", temp_root / "export.csv")

    @classmethod
    def tearDownClass(cls):
        for path, expected in cls.source_hashes.items():
            if file_hash(path) != expected:
                raise AssertionError(f"source review CSV changed: {path}")
        cls.temp.cleanup()

    def setUp(self):
        self.repo.state = {"version": 1, "updatedAt": None, "reviews": {}, "history": []}

    def test_inventory_analysis_and_filters(self):
        self.assertEqual(8000, len(self.repo.rows))
        self.assertEqual(8000, sum(self.repo.cluster_counts.values()))
        self.assertGreater(self.repo.meta()["warnings"].get("FACT", 0), 0)
        filtered = self.repo.filtered({"warning": ["FACT"], "status": ["unreviewed"]})
        self.assertTrue(filtered)
        self.assertTrue(all("FACT" in self.repo.analysis[row["review_id"]]["warningTypes"] for row in filtered))
        _, warnings = analyze_candidate('{"line":"전설의 운명이이이라는 힘", "usedCharacterFactIds":["F99"]}', {"F01"}, {"M01"})
        self.assertTrue({"CLICHE", "GRAMMAR", "FACT"} <= {warning["type"] for warning in warnings})

    def test_rewrite_preserves_schema_mechanics_and_references(self):
        review_id = self.repo.rows[0]["review_id"]
        original = json.loads(self.repo.rows[0]["candidate_a"])
        prose_key = "historyReason" if "historyReason" in original else "line"
        original[prose_key] = "검수자가 사실과 문체를 확인해 다시 쓴 문장이다."
        valid = json.dumps(original, ensure_ascii=False)
        self.repo.set_review(review_id, {"action": "REWRITE", "rewrite": valid, "issueTags": ["VOICE"], "reviewerNote": "rewritten"})
        changed = dict(original)
        if "effectBudget" in changed:
            changed["effectBudget"] += 1
        else:
            changed["illegalMechanic"] = 1
        with self.assertRaises(ValueError):
            self.repo.set_review(review_id, {"action": "REWRITE", "rewrite": json.dumps(changed, ensure_ascii=False), "issueTags": [], "reviewerNote": ""})

    def test_autosave_resume_draft_and_undo(self):
        review_id = self.repo.rows[0]["review_id"]
        self.repo.set_review(review_id, {"action": "APPROVE", "selectedCandidate": "A", "issueTags": ["VOICE"], "reviewerNote": "ok", "rewrite": ""})
        self.repo.set_review(review_id, {"action": "DRAFT", "issueTags": ["VOICE"], "reviewerNote": "updated", "rewrite": ""})
        self.assertEqual("APPROVE", self.repo.state["reviews"][review_id]["verdict"])
        self.assertEqual("A", self.repo.state["reviews"][review_id]["selectedCandidate"])
        resumed = ReviewRepository(DEFAULT_DATASET, self.repo.state_path, self.repo.export_path)
        self.assertEqual("updated", resumed.state["reviews"][review_id]["reviewerNote"])
        resumed.undo()
        self.assertNotIn(review_id, resumed.state["reviews"])

    def test_bulk_is_bounded_confirmed_and_export_is_merge_ready(self):
        ids = [row["review_id"] for row in self.repo.rows[:3]]
        payload = {"action": "DROP", "ids": ids, "confirmation": "APPLY 3", "issueTags": ["FACT"], "reviewerNote": "cluster", "rewrite": ""}
        self.assertEqual(3, self.repo.bulk(ids, payload)["updated"])
        with self.assertRaises(ValueError):
            self.repo.bulk(ids, {**payload, "confirmation": "yes"})
        approved_id = self.repo.rows[3]["review_id"]
        with gzip.open(DEFAULT_DATASET / "review/review_key_8000.jsonl.gz", "rt", encoding="utf-8") as stream:
            preferred = {item["reviewId"]: item["systemPreferred"] for item in map(json.loads, stream)}
        self.repo.set_review(approved_id, {"action": "APPROVE", "selectedCandidate": preferred[approved_id], "issueTags": [], "reviewerNote": "approved", "rewrite": ""})
        result = self.repo.export()
        self.assertEqual(8000, result["rows"])
        with self.repo.export_path.open("r", encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual(8000, len(rows))
        self.assertEqual("DROP", rows[0]["verdict"])
        self.assertEqual("FACT", rows[0]["issue_tags"])
        merge_output = Path(self.temp.name) / "merged"
        merge = subprocess.run(
            [sys.executable, str(TOOL_ROOT / "apply_human_review.py"), "--dataset", str(DEFAULT_DATASET), "--review-csv", str(self.repo.export_path), "--output-dir", str(merge_output), "--allow-partial"],
            check=True, capture_output=True, text=True, encoding="utf-8",
        )
        counts = json.loads(merge.stdout)
        self.assertEqual(1, counts["approvedPreference"] + counts["approvedHeldOut"])
        self.assertEqual(3, counts["dropped"])
        self.assertTrue((merge_output / "dropped.json").is_file())

    def test_loopback_http_token_and_actions(self):
        token = "focused-test-token"
        server = build_server(self.repo, "127.0.0.1", 0, token, logger=lambda _: None)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f"http://127.0.0.1:{server.server_port}"
        try:
            with self.assertRaises(urllib.error.HTTPError) as denied:
                urllib.request.urlopen(base + "/api/meta", timeout=3)
            self.assertEqual(403, denied.exception.code)
            request = urllib.request.Request(base + "/api/meta", headers={"X-Review-Token": token})
            with urllib.request.urlopen(request, timeout=3) as response:
                self.assertEqual(8000, json.load(response)["total"])
            with urllib.request.urlopen(base + "/", timeout=3) as response:
                self.assertIn("default-src 'self'", response.headers["Content-Security-Policy"])
                self.assertIn("서사 검수실", response.read().decode("utf-8"))
            review_id = self.repo.rows[0]["review_id"]
            body = json.dumps({"action": "APPROVE", "selectedCandidate": "B", "issueTags": [], "reviewerNote": "http", "rewrite": ""}).encode()
            request = urllib.request.Request(base + f"/api/review/{review_id}", data=body, method="POST", headers={"Content-Type": "application/json", "X-Review-Token": token})
            with urllib.request.urlopen(request, timeout=3) as response:
                self.assertEqual("B", json.load(response)["selectedCandidate"])
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=3)

    def test_static_ui_contracts(self):
        html = (STATIC_ROOT / "index.html").read_text(encoding="utf-8")
        script = (STATIC_ROOT / "app.js").read_text(encoding="utf-8")
        styles = (STATIC_ROOT / "styles.css").read_text(encoding="utf-8")
        parser = IdCollector()
        parser.feed(html)
        self.assertEqual(len(parser.ids), len(set(parser.ids)), "HTML IDs must be unique")
        self.assertEqual([], parser.external_urls, "reviewer must not load third-party resources")
        referenced_ids = set(re.findall(r'\$\("([A-Za-z0-9_-]+)"\)', script))
        self.assertEqual(set(), referenced_ids - set(parser.ids), "JavaScript references missing HTML IDs")
        for required in ("candidateA", "candidateB", "candidateAView", "candidateBView", "facts", "motifs", "progressBar", "bulkDialog", "rewriteDialog", "rewriteFields", "rewriteText"):
            self.assertIn(required, parser.ids)
        for shortcut in ('key==="1"', 'key==="2"', 'key==="r"', 'key==="d"', 'key==="s"', 'key==="u"'):
            self.assertIn(shortcut, script)
        self.assertIn("function renderCandidate", script)
        self.assertIn("function proseFields", script)
        self.assertIn("원본 JSON", script)
        self.assertIn("@media(max-width:820px)", styles)
        self.assertIn("overflow:hidden", styles)
        self.assertIn("/api/export", script)
        self.assertIn("/api/bulk", script)


if __name__ == "__main__":
    unittest.main(verbosity=2)
