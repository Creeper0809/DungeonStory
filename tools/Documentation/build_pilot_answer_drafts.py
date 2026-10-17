"""Validate authored public-input answers and export a create-only review bundle."""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

from jsonschema import Draft7Validator
from export_pilot_authoring_context import public_fact_aliases

ROOT = Path(__file__).resolve().parents[2]

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

def digest(path):
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()

def require(condition, message):
    if not condition:
        raise ValueError(message)

def validate_authored_text(context, candidate):
    prose = candidate["displayName"] + candidate["narrativeFlavor"]
    if context["profile"] == "CharacterSkillModuleSelection":
        text = "\n".join(m["content"] for m in context["wire"]["messages"])
        names = re.findall(r"(?m)^character=([^\r\n]+)$", text)
        require(len(set(names)) == 1, "exact character name is not unambiguous")
        name = names[0]
        flavor = candidate["narrativeFlavor"]
        require(re.fullmatch(r"[A-Za-z가-힣]+[0-9]*", name) is not None, "unsupported character name")
        require(flavor.count(name) == 1 and re.match(re.escape(name) + r"(?:은|는)\s", flavor),
                "exact character name must be the subject once")
        require(flavor.endswith(".") and flavor.count(".") == 1 and not re.search(r"[\r\n!?]", flavor),
                "one completed skill narrative sentence required")
        # Only the exact proper name at the subject position is exempt, not
        # other numbers, a longer name prefix, or a numerical effect.
        prose = candidate["displayName"] + flavor[len(name):]
    require(not re.search(r"[0-9０-９%％]", prose), "numeric authored effect")

def validate_answer(context, answer):
    require(answer["sourceWireSha256"] == context["sourceWireSha256"], "source wire differs")
    errors = list(Draft7Validator(context["responseSchema"]).iter_errors(answer["response"]))
    require(not errors, "response schema: " + "; ".join(f"{list(e.path)}: {e.message[:160]}" for e in errors))
    response = answer["response"]
    candidates = response.get("candidates", [response])
    view = context["presentation"]
    require(len(candidates) == view["outputCount"], "candidate count differs")
    require(len({a["selectionId"] for a in candidates}) == len(candidates), "duplicate selection")
    modules = {m["id"]: m for m in view["modules"]}
    aliases = public_fact_aliases(context["wire"])
    facts = {aliases.get(f["responseEvidenceId"], f["responseEvidenceId"]): f for f in view["facts"]}
    displays = []
    for index, candidate in enumerate(candidates):
        if context["publicSelectionIds"]:
            require(candidate["selectionId"] in context["publicSelectionIds"], "selection not publicly offered")
        contract = view["candidates"][index] if view["candidates"] else None
        chosen = []
        for field, polarity, key in [("positiveModuleIds", "Positive", "positive"),
                                     ("drawbackModuleIds", "Drawback", "drawback")]:
            ids = candidate[field]
            require(len(ids) == len(set(ids)), "duplicate module")
            if contract:
                require(set(ids) <= set(contract[key]), "module outside candidate offer")
            for identity in ids:
                require(identity in modules, "unknown module")
                module = modules[identity]
                require(module["polarity"] in (polarity, "Unspecified"), "module polarity mismatch")
                needed = [aliases.get(e, e) for e in module.get("requiredEvidence", [])]
                require(not needed or bool(set(needed) & set(candidate["evidenceFactIds"])), "required evidence missing")
                chosen.append(module)
            limit = view["limits"].get(field, {})
            require(len(ids) >= limit.get("minItems", 0), "too few modules")
            require(len(ids) <= limit.get("maxItems", len(ids)), "too many modules")
        require(not {"guard", "protect"} <= set(candidate["positiveModuleIds"]), "guard/protect conflict")
        evidence = candidate["evidenceFactIds"]
        require(len(evidence) == len(set(evidence)), "duplicate evidence")
        require(set(evidence) <= set(facts), "unknown evidence reference")
        if context["explicitlyOfferedEvidenceIds"]:
            require(set(evidence) <= set(context["explicitlyOfferedEvidenceIds"]), "evidence not offered")
        require(all(facts[e].get("selectable") is not False for e in evidence), "unselectable evidence")
        require(any(not facts[e].get("background") for e in evidence), "background-only grounding")
        validate_authored_text(context, candidate)
        explanations = {e["selectionId"]: e for e in answer.get("candidateExplanations", [])}
        displays.append(dict(selectionId=candidate["selectionId"],
            context=contract["context"] if contract else "",
            modules=[{k: m[k] for k in ("id", "name", "description", "polarity")} for m in chosen],
            evidence=[dict(id=e, text=facts[e]["displayText"], background=bool(facts[e].get("background")),
                importance=facts[e].get("importance"), eventLabel=facts[e].get("eventLabel")) for e in evidence],
            explanation=explanations.get(candidate["selectionId"], {}).get("explanationKo", answer["evidenceExplanationKo"])))
    return displays

def build(base, output, sources):
    base, output = base.resolve(), output.resolve()
    require(base.is_relative_to(ROOT / "Artifacts/QA") and output.is_relative_to(base), "output outside QA task")
    require(not output.exists(), "output exists; choose a new directory")
    context_path = base / "authoring-inputs.json"
    context = read(context_path)
    manifest = ROOT / context["sourceInputManifest"]
    originals = {r["requestKey"]: r for r in
        (json.loads(line) for line in (manifest.parent / "public-requests.jsonl").read_text(encoding="utf-8").splitlines() if line.strip())}
    require(len(originals) == len(context["rows"]) == 100, "100 original inputs required")
    authored = {}
    for name in sources:
        source = read(base / name)
        require(source["authoringOrigin"] == "assistant-authored-draft" and source["trainingEligible"] is False
                and source["humanApprovalClaimed"] is False, "invalid authoring status")
        for answer in source["rows"]:
            require(answer["requestKey"] not in authored, "duplicate request")
            authored[answer["requestKey"]] = answer
    require(set(authored) == set(originals), "missing or extra answer")
    rows, pairs, count = [], [], 0
    for request in context["rows"]:
        identity = request["requestKey"]
        original = originals[identity]
        require(original["wire"] == request["wire"] and original["sourceWireSha256"] == request["sourceWireSha256"], "original input drift")
        answer = authored[identity]
        try:
            cards = validate_answer(request, answer)
        except ValueError as error:
            raise ValueError(f"Input {request['number']}: {error}") from error
        count += len(cards)
        status = dict(schema="PASS", publicReferences="PASS", gameplayReplay="NOT_RUN", humanReview="NOT_REVIEWED")
        rows.append(dict(answer, number=request["number"], profile=request["profile"], displayCandidates=cards,
            validation=status, authoringOrigin="assistant-authored-draft", trainingEligible=False, humanApprovalClaimed=False))
        pairs.append(dict(requestKey=identity, profile=request["profile"], input=request["wire"], response=answer["response"],
            sourceWireSha256=request["sourceWireSha256"], authoringOrigin="assistant-authored-draft",
            sourceStatus="historical-controlled-capture", validation=status,
            trainingEligible=False, humanApprovalClaimed=False))
    require(count == 178, "expected 178 candidates")
    pins = {name: digest(base / name) for name in sources}
    bundle = dict(schema="dungeonstory-pilot-answer-drafts@1", rows=rows, requests=100, candidates=count,
        sourceManifestSha256=digest(manifest), authoringContextSha256=digest(context_path), authoringSourcePins=pins,
        authoringOrigin="assistant-authored-draft", trainingEligible=False, humanApprovalClaimed=False)
    output.mkdir()
    for name, data in [("answers.json", bundle), ("validation.json", dict(requests=100, candidates=count,
            profiles=dict(Counter(r["profile"] for r in rows)), schemaPass=100, publicReferencePass=100,
            originalInputsUnchanged=True, gameplayReplay="NOT_RUN", humanReview="NOT_REVIEWED",
            trainingEligible=False, authoringSourcePins=pins))]:
        with (output / name).open("x", encoding="utf-8") as stream:
            json.dump(data, stream, ensure_ascii=False, indent=2)
    with (output / "dataset-drafts.jsonl").open("x", encoding="utf-8") as stream:
        for pair in pairs:
            stream.write(json.dumps(pair, ensure_ascii=False, separators=(",", ":")) + "\n")
    print(json.dumps(dict(requests=100, candidates=count, answersSha256=digest(output / "answers.json")), ensure_ascii=False))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--sources", nargs=3, required=True)
    args = parser.parse_args()
    build(args.base, args.output, args.sources)
