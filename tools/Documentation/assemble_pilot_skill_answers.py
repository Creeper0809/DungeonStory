"""Bind explicitly authored skill prose/choices to supplied schema IDs and public facts."""
import json
from pathlib import Path
import sys
import copy

def variants(row):
    schema = row["responseSchema"]
    items = schema["properties"]["candidates"]["items"]
    if isinstance(items, list): return items
    if "oneOf" in items: return items["oneOf"]
    if "anyOf" in items: return items["anyOf"]
    ids = items.get("properties", {}).get("selectionId", {}).get("enum", row["publicSelectionIds"])
    if len(ids) != 3: raise ValueError("Older public request must explicitly offer three selection IDs")
    result = []
    for identity in ids:
        option = copy.deepcopy(items)
        option["properties"]["selectionId"]["enum"] = [identity]
        option["properties"]["positiveModuleIds"]["items"]["enum"] = row["presentation"]["candidates"][len(result)]["positive"]
        option["properties"]["evidenceFactIds"]["items"]["enum"] = row["explicitlyOfferedEvidenceIds"]
        result.append(option)
    return result

def select_evidence(row, topic, allowed):
    facts = [f for f in row["presentation"]["facts"] if f.get("background") is not True
             and f.get("selectable") is not False and f["responseEvidenceId"] in allowed]
    def matching(kind):
        if kind == "training": return [f for f in facts if "훈련" in f["displayText"]
                                       and (f.get("currentExperience") or {}).get("category") != 6]
        if kind == "rest": return [f for f in facts if "휴식" in f["displayText"] or "수면" in f["displayText"]]
        if kind == "environment": return [f for f in facts if "환경 노출" in f["displayText"]]
        if kind == "support": return [f for f in facts if "마지막 격려" in f["displayText"]]
        if kind == "campaign": return [f for f in facts if "캠페인 사건" in f["displayText"]]
        raise ValueError("Unknown authoring topic: " + kind)
    selected = []
    for kind in topic.split("-"):
        candidates = matching(kind)
        if not candidates: raise ValueError(f"No supplied {kind} fact for request{row['number']}")
        fact = candidates[0]
        if fact["responseEvidenceId"] not in [s["responseEvidenceId"] for s in selected]: selected.append(fact)
    return selected[:2]

def main():
    base = Path(sys.argv[1])
    context = json.loads((base / "authoring-inputs.json").read_text(encoding="utf-8"))
    prose = json.loads((base / "skill-prose-r1.json").read_text(encoding="utf-8"))
    by_number = {r["number"]: r for r in context["rows"]}
    output = []
    for number, *authored in prose["rows"]:
        row = by_number[number]
        schema = row["responseSchema"]
        options = variants(row)
        if len(options) != len(authored) or len(authored) != 3: raise ValueError("Candidate slot count differs")
        result = []
        explains = []
        for option, (module, name, flavor, topic) in zip(options, authored):
            fields = option["properties"]
            if module not in fields["positiveModuleIds"]["items"]["enum"]: raise ValueError("Unoffered module")
            evidence = select_evidence(row, topic, fields["evidenceFactIds"]["items"]["enum"])
            result.append(dict(selectionId=fields["selectionId"]["enum"][0], positiveModuleIds=[module],
                drawbackModuleIds=[], evidenceFactIds=[f["responseEvidenceId"] for f in evidence],
                displayName=name, narrativeFlavor=flavor))
            explains.append(dict(selectionId=result[-1]["selectionId"], evidenceTexts=[f["displayText"] for f in evidence],
                explanationKo="표시된 기록에서 기능을 제안한 창작 초안이며, 새 피해·승리·구출을 실제 사건으로 주장하지 않는다."))
        response = dict(candidates=result)
        if "requestRef" in schema["properties"]: response["requestRef"] = schema["properties"]["requestRef"]["enum"][0]
        output.append(dict(requestKey=row["requestKey"], sourceWireSha256=row["sourceWireSha256"],
            response=response, evidenceExplanationKo="각 후보의 실제 공개 근거와 기능 제안을 구분했다.", candidateExplanations=explains))
    if len(output) != 39: raise ValueError("Expected39 authored skill answers")
    with (base / "skill-answers-r1.json").open("x", encoding="utf-8") as stream:
        json.dump(dict(rows=output, authoringOrigin="assistant-authored-draft", trainingEligible=False,
            humanApprovalClaimed=False), stream, ensure_ascii=False, indent=2)
    print("SKILL_ANSWERS39_CANDIDATES117=WRITTEN")

if __name__ == "__main__": main()
