#!/usr/bin/env python3
"""Dependency-free localhost review workbench for the V25 narrative corpus."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import secrets
import tempfile
import threading
import time
import webbrowser
from collections import Counter
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse


TOOL_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = TOOL_ROOT.parents[1]
DEFAULT_DATASET = REPO_ROOT / "Artifacts/Training/V25"
DEFAULT_REVIEW_WORKSPACE = REPO_ROOT / "Artifacts/Review/V25"
STATIC_ROOT = Path(__file__).resolve().parent / "static"
REVIEW_FIELDS = (
    "review_id", "split", "category", "profile_id", "culture_style", "event_id",
    "viewpoint_character_id", "fact_summary", "motif_summary", "prompt",
    "candidate_a", "candidate_b", "verdict", "selected_candidate", "rewrite",
    "issue_tags", "reviewer_note",
)
PROSE_KEYS = {
    "line", "name", "description", "narrativeReason", "displayName", "historyReason",
    "facilityIdentitySummary", "reason", "flavorText", "summary", "traitName",
    "usedMotifIds", "usedCharacterFactIds",
}
CLICHE_PATTERNS = (
    "전설의 운명", "운명이 깨어나", "모든 것을 바꾸었다", "운명의 검",
    "전설적인 힘", "선택받은 자", "알 수 없는 힘",
)
GRAMMAR_PATTERNS = (
    (re.compile(r"(?:이이라는|가가|은은|는는|을을|를를)"), "조사가 중복되었습니다."),
    (re.compile(r"의 대가의 대가"), "같은 명사가 연속으로 반복됩니다."),
    (re.compile(r"[�]"), "손상된 유니코드 문자가 있습니다."),
    (re.compile(r"\?{2,}|!{3,}"), "문장부호가 과도하게 반복됩니다."),
)
REF_PATTERN = re.compile(r"\b([FM][0-9]{2})\b")
SUMMARY_REF_PATTERN = re.compile(r"(?:^|\s\|\s)([FM][0-9]{2})=")
TOKEN_PATTERN = re.compile(r"[가-힣A-Za-z0-9]+")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def atomic_json_write(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temp_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temp_name, path)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)


def split_summary(value: str, prefix: str) -> list[dict[str, str]]:
    result = []
    for part in value.split(" | "):
        if "=" not in part:
            continue
        ref, text = part.split("=", 1)
        if ref.startswith(prefix):
            result.append({"ref": ref, "text": text})
    return result


def all_strings(value) -> list[str]:
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        return [item for child in value for item in all_strings(child)]
    if isinstance(value, dict):
        return [item for child in value.values() for item in all_strings(child)]
    return []


def mechanics_only(value):
    if isinstance(value, dict):
        return {key: mechanics_only(child) for key, child in value.items() if key not in PROSE_KEYS}
    if isinstance(value, list):
        return [mechanics_only(child) for child in value]
    return value


def schema_shape(value):
    if isinstance(value, dict):
        return {key: schema_shape(child) for key, child in sorted(value.items())}
    if isinstance(value, list):
        return [schema_shape(child) for child in value]
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, (int, float)):
        return "number"
    if isinstance(value, str):
        return "string"
    return type(value).__name__


def analyze_candidate(raw: str, facts: set[str], motifs: set[str]) -> tuple[dict | list | None, list[dict]]:
    warnings = []
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as error:
        return None, [{"type": "FORMAT", "severity": "error", "message": f"JSON 오류: {error.msg}", "matches": []}]

    rendered = json.dumps(parsed, ensure_ascii=False)
    cliche_matches = [phrase for phrase in CLICHE_PATTERNS if phrase in rendered]
    if cliche_matches:
        warnings.append({"type": "CLICHE", "severity": "warning", "message": "상투적인 표현이 감지되었습니다.", "matches": cliche_matches})
    for pattern, message in GRAMMAR_PATTERNS:
        matches = sorted(set(pattern.findall(rendered)))
        if matches:
            warnings.append({"type": "GRAMMAR", "severity": "warning", "message": message, "matches": matches})

    used_facts = set()
    used_motifs = set()
    if isinstance(parsed, dict):
        used_facts = set(parsed.get("usedCharacterFactIds", []))
        used_motifs = set(parsed.get("usedMotifIds", []))
    unknown = sorted((used_facts - facts) | (used_motifs - motifs))
    inline_unknown = sorted(ref for ref in set(REF_PATTERN.findall(rendered)) if ref not in facts | motifs)
    unknown = sorted(set(unknown) | set(inline_unknown))
    if unknown:
        warnings.append({"type": "FACT", "severity": "error", "message": "제공되지 않은 사실·모티프 참조입니다.", "matches": unknown})
    return parsed, warnings


def simhash_bucket(text: str) -> str:
    tokens = TOKEN_PATTERN.findall(text.lower())
    features = []
    for index in range(max(1, len(tokens) - 2)):
        features.append(" ".join(tokens[index:index + 3]) or "empty")
    vector = [0] * 16
    for feature in set(features):
        value = int(hashlib.sha256(feature.encode("utf-8")).hexdigest()[:4], 16)
        for bit in range(16):
            vector[bit] += 1 if value & (1 << bit) else -1
    fingerprint = sum((1 << bit) for bit, score in enumerate(vector) if score >= 0)
    return f"S{fingerprint >> 8:02X}"


class ReviewRepository:
    def __init__(self, dataset: Path, state_path: Path | None = None, export_path: Path | None = None):
        self.dataset = dataset.resolve()
        self.review_dir = self.dataset / "review"
        self.state_path = (state_path or DEFAULT_REVIEW_WORKSPACE / "reviewer_state.json").resolve()
        self.export_path = (export_path or DEFAULT_REVIEW_WORKSPACE / "reviewer_export.csv").resolve()
        self.lock = threading.RLock()
        self.rows = self._load_rows()
        self.by_id = {row["review_id"]: row for row in self.rows}
        if len(self.by_id) != len(self.rows):
            raise ValueError("Duplicate review_id in source CSV files")
        self.analysis = {row["review_id"]: self._analyze_row(row) for row in self.rows}
        self.cluster_counts = Counter(item["clusterId"] for item in self.analysis.values())
        self.state = self._load_state()

    def _load_rows(self) -> list[dict[str, str]]:
        paths = sorted(self.review_dir.glob("review_[0-9]*_[0-9]*.csv"))
        if len(paths) != 8:
            raise ValueError(f"Expected 8 source review CSV files, found {len(paths)}")
        rows = []
        for path in paths:
            with path.open("r", encoding="utf-8-sig", newline="") as stream:
                reader = csv.DictReader(stream)
                if tuple(reader.fieldnames or ()) != REVIEW_FIELDS:
                    raise ValueError(f"Unexpected review columns in {path}")
                rows.extend(dict(row) for row in reader)
        if len(rows) != 8000:
            raise ValueError(f"Expected 8000 review rows, found {len(rows)}")
        return rows

    def _load_state(self) -> dict:
        if not self.state_path.exists():
            return {"version": 1, "updatedAt": None, "reviews": {}, "history": []}
        value = json.loads(self.state_path.read_text(encoding="utf-8"))
        if value.get("version") != 1 or not isinstance(value.get("reviews"), dict):
            raise ValueError("Unsupported reviewer state format")
        value.setdefault("history", [])
        value["reviews"] = {key: item for key, item in value["reviews"].items() if key in self.by_id}
        return value

    def _analyze_row(self, row: dict[str, str]) -> dict:
        facts = set(SUMMARY_REF_PATTERN.findall(row["fact_summary"]))
        motifs = set(SUMMARY_REF_PATTERN.findall(row["motif_summary"]))
        parsed_a, warnings_a = analyze_candidate(row["candidate_a"], facts, motifs)
        parsed_b, warnings_b = analyze_candidate(row["candidate_b"], facts, motifs)
        if parsed_a is not None and parsed_b is not None and mechanics_only(parsed_a) != mechanics_only(parsed_b):
            warning = {"type": "MECHANIC", "severity": "error", "message": "A/B의 규칙 필드가 서로 다릅니다.", "matches": []}
            warnings_a.append(warning)
            warnings_b.append(warning)
        if row["candidate_a"] == row["candidate_b"]:
            warning = {"type": "DUPLICATE", "severity": "error", "message": "A/B 후보가 완전히 같습니다.", "matches": []}
            warnings_a.append(warning)
            warnings_b.append(warning)
        score_a = sum(3 if item["severity"] == "error" else 1 for item in warnings_a)
        score_b = sum(3 if item["severity"] == "error" else 1 for item in warnings_b)
        preferred = parsed_a if score_a <= score_b else parsed_b
        preferred_text = " ".join(all_strings(preferred)) if preferred is not None else row["candidate_a"]
        return {
            "candidateA": {"parsed": parsed_a, "pretty": json.dumps(parsed_a, ensure_ascii=False, indent=2) if parsed_a is not None else row["candidate_a"], "warnings": warnings_a},
            "candidateB": {"parsed": parsed_b, "pretty": json.dumps(parsed_b, ensure_ascii=False, indent=2) if parsed_b is not None else row["candidate_b"], "warnings": warnings_b},
            "warningTypes": sorted({item["type"] for item in warnings_a + warnings_b}),
            "clusterId": simhash_bucket(preferred_text),
        }

    def _review(self, review_id: str) -> dict:
        return self.state["reviews"].get(review_id, {})

    def _validate_rewrite(self, review_id: str, rewrite: str) -> None:
        parsed = json.loads(rewrite)
        expected = self.analysis[review_id]["candidateA"]["parsed"]
        if expected is None or schema_shape(parsed) != schema_shape(expected):
            raise ValueError("수정 JSON의 필드 구조가 프로필 계약과 다릅니다.")
        if mechanics_only(parsed) != mechanics_only(expected):
            raise ValueError("수정본은 규칙·수치·대상 필드를 변경할 수 없습니다.")
        row = self.by_id[review_id]
        facts = set(SUMMARY_REF_PATTERN.findall(row["fact_summary"]))
        motifs = set(SUMMARY_REF_PATTERN.findall(row["motif_summary"]))
        _, warnings = analyze_candidate(rewrite, facts, motifs)
        hard = [warning for warning in warnings if warning["severity"] == "error"]
        if hard:
            raise ValueError(hard[0]["message"])

    def status(self, review_id: str) -> str:
        verdict = self._review(review_id).get("verdict", "")
        return {"APPROVE": "approved", "REWRITE": "rewrite", "DROP": "dropped"}.get(verdict, "unreviewed")

    def _save(self) -> None:
        self.state["updatedAt"] = utc_now()
        atomic_json_write(self.state_path, self.state)

    def set_review(self, review_id: str, payload: dict, record_history: bool = True, persist: bool = True) -> dict:
        with self.lock:
            if review_id not in self.by_id:
                raise KeyError(review_id)
            action = str(payload.get("action", "DRAFT")).upper()
            if action not in {"DRAFT", "APPROVE", "REWRITE", "DROP"}:
                raise ValueError("Unknown review action")
            selected = str(payload.get("selectedCandidate", "")).upper()
            rewrite = str(payload.get("rewrite", "")).strip()
            if action == "APPROVE" and selected not in {"A", "B"}:
                raise ValueError("APPROVE requires candidate A or B")
            if action == "REWRITE":
                if not rewrite:
                    raise ValueError("REWRITE requires complete JSON")
                self._validate_rewrite(review_id, rewrite)
            before = self.state["reviews"].get(review_id)
            if record_history and action != "DRAFT":
                self.state["history"].append({"reviewId": review_id, "before": before, "at": utc_now()})
                self.state["history"] = self.state["history"][-500:]
            previous = before or {}
            item = {
                "verdict": previous.get("verdict", "") if action == "DRAFT" else action,
                "selectedCandidate": previous.get("selectedCandidate", "") if action == "DRAFT" else selected if action == "APPROVE" else "",
                "rewrite": rewrite,
                "issueTags": sorted(set(str(value).upper() for value in payload.get("issueTags", []) if str(value).strip())),
                "reviewerNote": str(payload.get("reviewerNote", "")),
                "updatedAt": utc_now(),
            }
            self.state["reviews"][review_id] = item
            if persist:
                self._save()
            return item

    def undo(self) -> dict | None:
        with self.lock:
            if not self.state["history"]:
                return None
            change = self.state["history"].pop()
            if change["before"] is None:
                self.state["reviews"].pop(change["reviewId"], None)
            else:
                self.state["reviews"][change["reviewId"]] = change["before"]
            self._save()
            return {"reviewId": change["reviewId"], "review": self._review(change["reviewId"])}

    def filtered(self, query: dict[str, list[str]]) -> list[dict[str, str]]:
        culture = query.get("culture", [""])[0]
        profile = query.get("profile", [""])[0]
        split = query.get("split", [""])[0]
        status = query.get("status", [""])[0]
        warning = query.get("warning", [""])[0]
        cluster = query.get("cluster", [""])[0]
        search = query.get("q", [""])[0].strip().lower()
        result = []
        for row in self.rows:
            review_id = row["review_id"]
            analysis = self.analysis[review_id]
            if culture and row["culture_style"] != culture:
                continue
            if profile and row["profile_id"] != profile:
                continue
            if split and row["split"] != split:
                continue
            if status and self.status(review_id) != status:
                continue
            if warning and warning not in analysis["warningTypes"]:
                continue
            if cluster and analysis["clusterId"] != cluster:
                continue
            if search and search not in " ".join(row.values()).lower():
                continue
            result.append(row)
        return result

    def list_records(self, query: dict[str, list[str]]) -> dict:
        matched = self.filtered(query)
        page = max(1, int(query.get("page", ["1"])[0] or 1))
        page_size = min(50, max(10, int(query.get("pageSize", ["20"])[0] or 20)))
        start = (page - 1) * page_size
        items = []
        for row in matched[start:start + page_size]:
            review_id = row["review_id"]
            items.append({
                "reviewId": review_id, "profile": row["profile_id"], "culture": row["culture_style"],
                "split": row["split"], "status": self.status(review_id),
                "warningTypes": self.analysis[review_id]["warningTypes"],
                "clusterId": self.analysis[review_id]["clusterId"],
                "eventId": row["event_id"],
            })
        return {"items": items, "total": len(matched), "page": page, "pageSize": page_size, "pageCount": max(1, (len(matched) + page_size - 1) // page_size)}

    def record(self, review_id: str) -> dict:
        row = self.by_id[review_id]
        return {
            "reviewId": review_id, "split": row["split"], "category": row["category"],
            "profile": row["profile_id"], "culture": row["culture_style"],
            "eventId": row["event_id"], "viewpointCharacterId": row["viewpoint_character_id"],
            "facts": split_summary(row["fact_summary"], "F"),
            "motifs": split_summary(row["motif_summary"], "M"),
            "candidateA": self.analysis[review_id]["candidateA"],
            "candidateB": self.analysis[review_id]["candidateB"],
            "warningTypes": self.analysis[review_id]["warningTypes"],
            "clusterId": self.analysis[review_id]["clusterId"],
            "clusterSize": self.cluster_counts[self.analysis[review_id]["clusterId"]],
            "review": self._review(review_id), "status": self.status(review_id),
        }

    def meta(self) -> dict:
        status_counts = Counter(self.status(row["review_id"]) for row in self.rows)
        return {
            "total": len(self.rows), "reviewed": len(self.rows) - status_counts["unreviewed"],
            "statusCounts": dict(status_counts),
            "cultures": dict(sorted(Counter(row["culture_style"] for row in self.rows).items())),
            "profiles": dict(sorted(Counter(row["profile_id"] for row in self.rows).items())),
            "splits": dict(sorted(Counter(row["split"] for row in self.rows).items())),
            "warnings": dict(sorted(Counter(warning for item in self.analysis.values() for warning in item["warningTypes"]).items())),
            "clusters": [{"id": key, "count": count} for key, count in self.cluster_counts.most_common()],
            "updatedAt": self.state.get("updatedAt"), "canUndo": bool(self.state["history"]),
            "statePath": str(self.state_path), "exportPath": str(self.export_path),
        }

    def bulk(self, ids: list[str], payload: dict) -> dict:
        unique = list(dict.fromkeys(ids))
        if not unique or len(unique) > 50:
            raise ValueError("Bulk action requires 1-50 visible record IDs")
        expected = f"APPLY {len(unique)}"
        if payload.get("confirmation") != expected:
            raise ValueError(f"Bulk confirmation must be '{expected}'")
        for review_id in unique:
            self.set_review(review_id, payload, record_history=True, persist=False)
        self._save()
        return {"updated": len(unique)}

    def export(self) -> dict:
        with self.lock:
            self.export_path.parent.mkdir(parents=True, exist_ok=True)
            handle, temp_name = tempfile.mkstemp(prefix=self.export_path.name + ".", suffix=".tmp", dir=self.export_path.parent)
            try:
                with os.fdopen(handle, "w", encoding="utf-8-sig", newline="") as stream:
                    writer = csv.DictWriter(stream, fieldnames=REVIEW_FIELDS)
                    writer.writeheader()
                    for source in self.rows:
                        row = dict(source)
                        review = self._review(row["review_id"])
                        row["verdict"] = review.get("verdict", "")
                        row["selected_candidate"] = review.get("selectedCandidate", "")
                        row["rewrite"] = review.get("rewrite", "")
                        row["issue_tags"] = ",".join(review.get("issueTags", []))
                        row["reviewer_note"] = review.get("reviewerNote", "")
                        writer.writerow(row)
                    stream.flush()
                    os.fsync(stream.fileno())
                os.replace(temp_name, self.export_path)
            finally:
                if os.path.exists(temp_name):
                    os.unlink(temp_name)
            counts = Counter(self.status(row["review_id"]) for row in self.rows)
            return {"path": str(self.export_path), "counts": dict(counts), "rows": len(self.rows)}


class ReviewerHandler(BaseHTTPRequestHandler):
    server_version = "DungeonStoryReviewer/1.0"

    @property
    def app(self):
        return self.server.app

    def log_message(self, format_string: str, *args) -> None:
        self.app["log"](format_string % args)

    def _authorized(self) -> bool:
        parsed = urlparse(self.path)
        query_token = parse_qs(parsed.query).get("token", [""])[0]
        return secrets.compare_digest(self.headers.get("X-Review-Token", "") or query_token, self.app["token"])

    def _json(self, value, status=HTTPStatus.OK) -> None:
        body = json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def _error(self, status, message: str) -> None:
        self._json({"error": message}, status)

    def _body(self) -> dict:
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0 or length > 2 * 1024 * 1024:
            raise ValueError("Invalid request body size")
        return json.loads(self.rfile.read(length).decode("utf-8"))

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path.startswith("/api/"):
            if not self._authorized():
                self._error(HTTPStatus.FORBIDDEN, "Invalid local review token")
                return
            try:
                query = parse_qs(parsed.query)
                if parsed.path == "/api/meta":
                    self._json(self.app["repo"].meta())
                elif parsed.path == "/api/records":
                    self._json(self.app["repo"].list_records(query))
                elif parsed.path.startswith("/api/records/"):
                    self._json(self.app["repo"].record(parsed.path.rsplit("/", 1)[-1]))
                else:
                    self._error(HTTPStatus.NOT_FOUND, "Unknown API endpoint")
            except KeyError:
                self._error(HTTPStatus.NOT_FOUND, "Unknown review record")
            except (ValueError, json.JSONDecodeError) as error:
                self._error(HTTPStatus.BAD_REQUEST, str(error))
            return
        self._static(parsed.path)

    def _static(self, path: str) -> None:
        file_name = {"/": "index.html", "/index.html": "index.html", "/app.js": "app.js", "/styles.css": "styles.css"}.get(path)
        if not file_name:
            self.send_error(HTTPStatus.NOT_FOUND)
            return
        file_path = STATIC_ROOT / file_name
        body = file_path.read_bytes()
        content_type = {".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8"}[file_path.suffix]
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if not parsed.path.startswith("/api/") or not self._authorized():
            self._error(HTTPStatus.FORBIDDEN, "Invalid local review token")
            return
        try:
            payload = self._body()
            if parsed.path.startswith("/api/review/"):
                result = self.app["repo"].set_review(parsed.path.rsplit("/", 1)[-1], payload)
            elif parsed.path == "/api/bulk":
                result = self.app["repo"].bulk(payload.get("ids", []), payload)
            elif parsed.path == "/api/undo":
                result = self.app["repo"].undo()
            elif parsed.path == "/api/export":
                result = self.app["repo"].export()
            else:
                self._error(HTTPStatus.NOT_FOUND, "Unknown API endpoint")
                return
            self._json(result)
        except KeyError:
            self._error(HTTPStatus.NOT_FOUND, "Unknown review record")
        except (ValueError, json.JSONDecodeError) as error:
            self._error(HTTPStatus.BAD_REQUEST, str(error))


def build_server(repository: ReviewRepository, host: str, port: int, token: str, logger=print) -> ThreadingHTTPServer:
    server = ThreadingHTTPServer((host, port), ReviewerHandler)
    server.daemon_threads = True
    server.app = {"repo": repository, "token": token, "log": logger}
    return server


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    parser.add_argument("--state", type=Path)
    parser.add_argument("--export", type=Path)
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--open", action="store_true")
    parser.add_argument("--write-url", type=Path)
    args = parser.parse_args()

    repository = ReviewRepository(args.dataset, args.state, args.export)
    token = secrets.token_urlsafe(24)
    server = build_server(repository, "127.0.0.1", args.port, token)
    url = f"http://127.0.0.1:{server.server_port}/?token={token}"
    if args.write_url:
        args.write_url.parent.mkdir(parents=True, exist_ok=True)
        args.write_url.write_text(url + "\n", encoding="utf-8")
    print(f"DungeonStory narrative reviewer: {url}", flush=True)
    print(f"Autosave: {repository.state_path}", flush=True)
    if args.open:
        threading.Timer(0.35, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
