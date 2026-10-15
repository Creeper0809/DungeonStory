"""Create-only100-input review; imports the previous parser without modifying its repo."""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
TRAINING = ROOT.parent / "DungeonStoryNarrativeAI/tools/v25_narrative_training"
sys.path.insert(0, str(TRAINING))
from pilot_input_intake import Snapshot, canonical, byte_hash, loads, require
from module_selection_reviewer.input_profile import InputProfileStore
from module_selection_reviewer.input_importance import bindings_in, documents, metadata_for

def write_new(path, value):
    with path.open("xb") as stream:
        stream.write(canonical(value) + b"\n")

def prepare(profile, output):
    profile = profile.resolve(strict=True)
    output = output.resolve()
    require(profile.is_relative_to(ROOT / "Artifacts/QA") and output.is_relative_to(ROOT / "Artifacts/QA"),
            "review stays inside DungeonStory QA")
    require(not output.exists(), "create-only review directory already exists")
    raw = {name: (profile / name).read_bytes() for name in
           ("public-requests.jsonl", "request-profiles.jsonl", "profile-summary.json")}
    public = [loads(line) for line in raw["public-requests.jsonl"].splitlines()]
    private = [loads(line) for line in raw["request-profiles.jsonl"].splitlines()]
    summary = loads(raw["profile-summary.json"])
    require(len(public) == len(private) == summary["uniqueRequests"] == 100, "100 requests required")
    require(len({r["requestKey"] for r in public}) == len({r["sourceWireSha256"] for r in public}) == 100,
            "duplicate identity/wire cannot add a request")
    require(all(r["response"] is None and r["trainingEligible"] is False for r in public), "no fabricated answer")
    by_key = {r["requestKey"]: r for r in private}
    rows = []
    for item in public:
        meta = by_key[item["requestKey"]]
        require(item["sourceWireSha256"] == meta["wire"]["sha256"], "public/private wire differs")
        blockers = ["historical-producer-code"] if not meta["producerCodePinsCurrent"] else []
        blockers += sorted(set(meta["warnings"]) & {"placeholder-character-name-in-public-input",
            "offered-module-polarity-missing-in-public-input", "test-preparation-text-in-public-input"})
        rows.append(dict(requestKey=item["requestKey"], profile=item["profile"],
            producerCodeCurrent=meta["producerCodePinsCurrent"], draftInputBlockers=blockers,
            warnings=meta["warnings"], responseStatus=meta["responseStatus"]))
    report = dict(schema="dungeonstory-pilot-input-intake@1", status="INPUT_REVIEW_ONLY", requests=100,
        currentProducerCodeRequests=sum(r["producerCodeCurrent"] for r in rows),
        historicalProducerCodeRequests=sum(not r["producerCodeCurrent"] for r in rows),
        preliminaryDraftInputs=sum(not r["draftInputBlockers"] for r in rows), completedAnswerPairs=0,
        rows=rows, byProfile=summary["byProfile"], trainingEligible=False, humanApprovalClaimed=False)
    raw["authoring-requests.jsonl"] = b"".join(canonical(r) + b"\n" for r in public
        if not next(v for v in rows if v["requestKey"] == r["requestKey"])["draftInputBlockers"])
    raw["intake-report.json"] = canonical(report) + b"\n"
    manifest = dict(schema="dungeonstory-pilot-input-review@1", status="INPUT_REVIEW_ONLY",
        sourceProfilePins={k: byte_hash(v) for k, v in raw.items() if k in
            ("public-requests.jsonl", "request-profiles.jsonl", "profile-summary.json")},
        files=[dict(path=k, bytes=len(v), sha256=byte_hash(v)) for k, v in raw.items()],
        trainingEligible=False, humanApprovalClaimed=False)
    output.mkdir(parents=True, exist_ok=False)
    for name, data in raw.items():
        with (output / name).open("xb") as stream: stream.write(data)
    write_new(output / "manifest.json", manifest)
    store = InputProfileStore(output / "manifest.json", byte_hash((output / "manifest.json").read_bytes()))
    source = Snapshot(ROOT)
    policies, raw_importance = [], []
    for row in store.rows:
        require(row["presentation"]["parsed"], "unparsed public input cannot count as parsed review")
        meta = by_key[row["requestKey"]]
        package = source.document(ROOT, meta["sourceManifest"])
        base = (ROOT / meta["sourceManifest"]["path"]).parent
        selections = source.document(base, package["selectionSource"])
        matches = [s for s in selections["selections"] if s["selectionSourceId"] == meta["selectionSourceId"]]
        require(len(matches) == 1, "missing/duplicate captured selection")
        selection = matches[0]
        witness = source.document(base, selection["evidenceSidecar"])
        original = source.read(base, selection["capture"]["originalPrompt"]) if any(
            d.get("transport") == "skill-request-refs-v1" for d in documents(row["wire"])) else None
        captured = metadata_for(row, witness, original)
        raw_importance.append(captured)
        bindings = {}
        for binding in bindings_in(witness.get("pending", {})):
            key = binding["publicFactId"]
            require(key not in bindings or bindings[key] == binding, "conflicting captured binding")
            bindings[key] = binding
        policies.append(dict(requestKey=row["requestKey"], sourceWireSha256=row["sourceWireSha256"],
            profileId=row["profileId"], facts=[dict(id=f["id"], sourceFactId=captured["facts"][f["id"]]["sourceFactId"],
                binding=bindings.get(captured["facts"][f["id"]]["sourceFactId"])) for f in row["presentation"]["facts"]]))
    source.unchanged()
    store.read()
    pins = [dict(path=p.relative_to(ROOT).as_posix(), bytes=len(v), sha256=byte_hash(v))
            for p, v in source.data.items()]
    write_new(output / "captured-importance.json", dict(rows=raw_importance, sourcePins=pins,
        trainingEligible=False, humanApprovalClaimed=False))
    write_new(output.parent / "policy-inputs-r1.json", dict(schema="dungeonstory-pilot-experience-evaluation-input@1",
        rows=policies, sourcePins=pins, sourceManifestSha256=store.pilot_hash,
        trainingEligible=False, humanApprovalClaimed=False))
    print(json.dumps(dict(requests=100, parsed=100, byProfile=summary["byProfile"],
        currentProducerCodeRequests=report["currentProducerCodeRequests"],
        historicalProducerCodeRequests=report["historicalProducerCodeRequests"]), ensure_ascii=False))

if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--profile", type=Path, required=True)
    args.add_argument("--output", type=Path, required=True)
    options = args.parse_args()
    prepare(options.profile, options.output)
