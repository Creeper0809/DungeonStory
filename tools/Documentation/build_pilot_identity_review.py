"""Resolve personal names and roles from each historical input's pinned world."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()

def pinned(path, pin):
    path = path.resolve()
    if not path.is_relative_to(ROOT / "Artifacts/QA"):
        raise ValueError("Identity source must be inside this project's QA tree")
    data = path.read_bytes()
    if len(data) != pin["bytes"] or digest(data) != pin["sha256"]:
        raise ValueError("Historical identity source changed: " + str(path))
    return json.loads(data)

def identities(world):
    actors = []
    for section in world["sections"]:
        payload = json.loads(section["payloadJson"])
        if isinstance(payload, dict) and isinstance(payload.get("actors"), list):
            actors.extend(payload["actors"])
    result = {}
    for actor in actors:
        if "persistentId" not in actor or "growth" not in actor:
            continue
        name = actor["growth"].get("displayName", "").strip()
        if not name or name != actor["displayName"].strip():
            raise ValueError("Conflicting saved personal name")
        role, kind = actor["role"], actor["characterType"]
        if role not in (0, 1) or kind not in (0, 1, 2) or bool(actor["isOwner"]) != (role == 1):
            raise ValueError("Invalid saved character role")
        identity = actor["persistentId"]
        if identity in result:
            raise ValueError("Duplicate character identity")
        result[identity] = dict(entityId=identity, name=name, roleCode=("Regular", "Owner")[role],
            roleLabel=("일반", "사장")[role], typeCode=("NPC", "Customer", "Intruder")[kind],
            typeLabel=("내부 인력", "손님", "침입자")[kind])
    return result

def actor_display(text, old, actual):
    if old == actual or not old:
        return text
    # Only a bound actor prefix is rewritten; titles mentioned elsewhere remain intact.
    for prefix in (old + "은 ", old + "는 ", old + "이 ", old + "가 ", old + ": "):
        if text.startswith(prefix):
            return actual + ": " + text[len(prefix):]
    if " · " + old + ": " in text:
        return text.replace(" · " + old + ": ", " · " + actual + ": ", 1)
    return text

def build(output):
    output = output.resolve()
    if not output.is_relative_to(ROOT / "Artifacts/QA") or output.exists():
        raise ValueError("A new QA output directory is required")
    base = ROOT / "Artifacts/QA/Pilot100-20261011-b132-r1"
    profiles = [json.loads(line) for line in (base / "profile-100-r1/request-profiles.jsonl").read_text(encoding="utf-8").splitlines()]
    context_path = ROOT / "Artifacts/QA/Pilot100-20261011-b133-r1/authoring-inputs.json"
    context = {r["requestKey"]: r for r in json.loads(context_path.read_text(encoding="utf-8"))["rows"]}
    policy_path = base / "policy-inputs-r1.json"
    policy = {r["requestKey"]: r for r in json.loads(policy_path.read_text(encoding="utf-8"))["rows"]}
    cache, rows, pins = {}, [], {}
    for profile in profiles:
        key = profile["requestKey"]
        request, evidence = context[key], policy[key]
        if request["sourceWireSha256"] != profile["wire"]["sha256"] or evidence["sourceWireSha256"] != request["sourceWireSha256"]:
            raise ValueError("Input identity lineage differs")
        manifest_path = ROOT / profile["sourceManifest"]["path"]
        pinned(manifest_path, profile["sourceManifest"])
        checkpoint = manifest_path.parent / profile["officialCheckpoint"]["path"]
        if checkpoint not in cache:
            cache[checkpoint] = identities(pinned(checkpoint, profile["officialCheckpoint"]))
        people = cache[checkpoint]
        pins[str(checkpoint.relative_to(ROOT)).replace("\\", "/")] = profile["officialCheckpoint"]
        relevant, facts, corrections = {}, {}, []
        if profile["ownerId"] in people:
            relevant[profile["ownerId"]] = people[profile["ownerId"]]
        public = {f["id"]: f for f in request["presentation"]["facts"]}
        for fact in evidence["facts"]:
            bound = fact.get("binding")
            if not bound:
                continue
            participants = []
            text = public[fact["id"]]["displayText"]
            for part in bound["participants"]:
                if part["entityKindId"] != "character" or part["entityId"] not in people:
                    continue
                person = people[part["entityId"]]
                relevant[person["entityId"]] = person
                participants.append(dict(person, eventRole=part["roleId"], recordedName=part["displayText"]))
                if part["roleId"] == "actor":
                    text = actor_display(text, part["displayText"], person["name"])
                if part["displayText"] != person["name"]:
                    corrections.append(dict(factId=fact["id"], entityId=person["entityId"],
                        recordedName=part["displayText"], personalName=person["name"], role=person["roleCode"]))
            if participants:
                facts[fact["id"]] = dict(displayText=text, originalText=public[fact["id"]]["displayText"], participants=participants)
        supplemental = dict(characters=list(relevant.values()), factNameBindings=corrections)
        prepared = copy.deepcopy(request["wire"])
        prepared["messages"].append(dict(role="user", content=
            "인물 식별 보충 자료입니다. name은 개인 이름, roleCode/roleLabel은 직급, typeCode/typeLabel은 인물 구분입니다. "
            "서사의 이름 자리에는 개인 이름을 쓰고 직급을 이름처럼 쓰지 마세요. factNameBindings는 같은 인물 ID의 기록상 명칭을 설명하며 새 사건이 아닙니다.\n"
            + json.dumps(supplemental, ensure_ascii=False, separators=(",", ":"))))
        rows.append(dict(requestKey=key, sourceWireSha256=request["sourceWireSha256"], characters=list(relevant.values()),
            facts=facts, corrections=corrections, preparedWire=prepared,
            preparedWireSha256=digest(json.dumps(prepared, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode())))
    output.mkdir(parents=True)
    document = dict(schema="dungeonstory-pilot-name-role-review@1", rows=rows,
        sourceContextSha256=digest(context_path.read_bytes()), policyInputSha256=digest(policy_path.read_bytes()),
        checkpointPins=pins, originalInputsModified=False, derivedInput=True, trainingEligible=False, humanApprovalClaimed=False)
    with (output / "identity-review.json").open("x", encoding="utf-8") as stream:
        json.dump(document, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(requests=len(rows), correctedRequests=sum(bool(r["corrections"]) for r in rows),
        correctedFacts=sum(len(r["corrections"]) for r in rows), sha256=digest((output / "identity-review.json").read_bytes())), ensure_ascii=False))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    build(parser.parse_args().output)
