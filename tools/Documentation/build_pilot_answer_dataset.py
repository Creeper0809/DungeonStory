"""Package explicitly authored100 answers; no gameplay, approval or training claims."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys
import unicodedata
import jsonschema

def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")

def sha(value): return "sha256:" + hashlib.sha256(value).hexdigest()

def validate_answer(row, answer):
    if answer["sourceWireSha256"] != row["sourceWireSha256"]: raise ValueError("Wire identity mismatch")
    response = answer["response"]
    errors = list(jsonschema.Draft7Validator(row["responseSchema"]).iter_errors(response))
    if errors: raise ValueError(f"Request{row['number']} schema: {errors[0].message}")
    candidates = response.get("candidates", [response])
    if len(candidates) != row["presentation"]["outputCount"]: raise ValueError("Candidate count mismatch")
    all_modules = {m["id"]: m for m in row["presentation"]["modules"]}
    facts = {f["responseEvidenceId"]: f for f in row["presentation"]["facts"]}
    grounding = []
    for index, candidate in enumerate(candidates):
        if candidate["selectionId"] not in row["publicSelectionIds"] and not any(
            candidate["selectionId"] in item.get("properties", {}).get("selectionId", {}).get("enum", [])
            for item in (row["responseSchema"]["properties"].get("candidates", {}).get("items", [])
                         if isinstance(row["responseSchema"]["properties"].get("candidates", {}).get("items"), list)
                         else [row["responseSchema"]])):
            raise ValueError("Selection ID not publicly supplied")
        if not set(candidate["positiveModuleIds"] + candidate["drawbackModuleIds"]).issubset(all_modules):
            raise ValueError("Unoffered module")
        if set(candidate["positiveModuleIds"]) & set(candidate["drawbackModuleIds"]): raise ValueError("Conflicting polarity")
        if row["profile"] == "CharacterSkillModuleSelection" and not set(candidate["positiveModuleIds"]).issubset(
            row["presentation"]["candidates"][index]["positive"]): raise ValueError("Wrong slot module")
        if not set(candidate["evidenceFactIds"]).issubset(facts): raise ValueError("Unknown public evidence")
        if any(facts[e].get("selectable") is False for e in candidate["evidenceFactIds"]):
            raise ValueError("Unselectable public fact used")
        for module in candidate["positiveModuleIds"]:
            required = all_modules[module]["requiredEvidence"]
            if required and not set(required).intersection(candidate["evidenceFactIds"]):
                raise ValueError("Required module evidence missing")
        for field in ("positiveModuleIds", "drawbackModuleIds"):
            cap = row["presentation"]["limits"].get(field, {}).get("maxItems")
            if cap is not None and len(candidate[field]) > cap: raise ValueError("Public stricter limit exceeded")
        for field in ("displayName", "narrativeFlavor"):
            value = candidate[field]
            if any(unicodedata.category(c).startswith("N") or c in "%％" for c in value):
                raise ValueError("Machine numbers in player prose")
            limit = row["presentation"]["limits"].get(field, {}).get("maxLength")
            if limit and len(value) > limit: raise ValueError("Player prose exceeds stricter limit")
        if len(set(candidate["evidenceFactIds"])) != len(candidate["evidenceFactIds"]): raise ValueError("Duplicate evidence")
        grounding.append(dict(selectionId=candidate["selectionId"],
            selectedModules=[all_modules[mid] for mid in candidate["positiveModuleIds"] + candidate["drawbackModuleIds"]],
            selectedFacts=[dict(id=e, displayText=facts[e]["displayText"],
                background=facts[e].get("background") is True) for e in candidate["evidenceFactIds"]]))
    return grounding

def build(base, output):
    context_path = base / "authoring-inputs.json"
    context = json.loads(context_path.read_text(encoding="utf-8"))
    paths = [base / "skill-answers-r1.json", base / "trait-answers-r2.json", base / "item-answers-r1.json"]
    authored = []
    for path in paths:
        document = json.loads(path.read_text(encoding="utf-8"))
        if document.get("trainingEligible") is not False or document.get("humanApprovalClaimed") is not False:
            raise ValueError("Authoring file grants approval")
        authored.extend(document["rows"])
    by_key = {a["requestKey"]: a for a in authored}
    if len(authored) != 100 or len(by_key) != 100: raise ValueError("100 unique answers required")
    records, sft = [], []
    for row in context["rows"]:
        answer = by_key[row["requestKey"]]
        grounding = validate_answer(row, answer)
        records.append(dict(requestKey=row["requestKey"], sourceWireSha256=row["sourceWireSha256"],
            profile=row["profile"], response=answer["response"], grounding=grounding,
            evidenceExplanationKo=answer["evidenceExplanationKo"], authoringOrigin="assistant-authored-draft",
            candidateExplanations=answer.get("candidateExplanations", []), schemaValidation="PASS",
            offeredIdsAndEvidenceValidation="PASS", semanticHumanReview="PENDING",
            csharpCalculation="NOT_RUN", gameplayApplied=False, trainingEligible=False, humanApprovalClaimed=False))
        sft.append(dict(requestKey=row["requestKey"], sourceWireSha256=row["sourceWireSha256"],
            profile=row["profile"], messages=row["wire"]["messages"] + [dict(role="assistant",
                content=canonical(answer["response"]).decode("utf-8"))],
            responseFormat=row["wire"]["response_format"], trainingEligible=False,
            humanApprovalClaimed=False, sourceCurrency="historical", authoringOrigin="assistant-authored-draft"))
    if output.exists(): raise ValueError("Create-only dataset exists")
    output.mkdir(parents=True, exist_ok=False)
    files = {}
    for name, data in (("answers.jsonl", records), ("sft-drafts.jsonl", sft)):
        raw = b"".join(canonical(r) + b"\n" for r in data)
        with (output / name).open("xb") as stream: stream.write(raw)
        files[name] = dict(path=name, bytes=len(raw), sha256=sha(raw))
    report = dict(schema="dungeonstory-pilot100-authored-dataset@1", status="AUTHORED_DRAFTS_REVIEW_ONLY",
        requests=100, answerPairs=100, candidates=sum(len(r["response"].get("candidates", [r["response"]])) for r in records),
        schemaPassed=100, sourceIdsPassed=100, byProfile=dict(Counter(r["profile"] for r in records)),
        contextSha256=sha(context_path.read_bytes()), authorFiles=[dict(path=p.name, sha256=sha(p.read_bytes())) for p in paths],
        files=files, semanticHumanReview="PENDING", csharpCalculation="NOT_RUN", gameplayApplied=False,
        trainingEligible=False, humanApprovalClaimed=False, trainingPerformed=False)
    with (output / "manifest.json").open("xb") as stream: stream.write(canonical(report) + b"\n")
    print(json.dumps({k:report[k] for k in ("requests","answerPairs","candidates","schemaPassed","sourceIdsPassed","byProfile")},ensure_ascii=False))

if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base",required=True,type=Path)
    parser.add_argument("--output",required=True,type=Path)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[2]
    if not args.base.resolve().is_relative_to(root / "Artifacts/QA") or not args.output.resolve().is_relative_to(root / "Artifacts/QA"):
        raise ValueError("Dataset stays inside DungeonStory")
    build(args.base,args.output)
