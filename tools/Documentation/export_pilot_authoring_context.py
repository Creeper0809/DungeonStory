"""Freeze the100 displayed public inputs for answer authoring; never change the originals."""
import json
from pathlib import Path
import re
import sys
from urllib.request import urlopen

def public_fact_aliases(wire):
    aliases = {}
    for message in wire["messages"]:
        for line in message["content"].splitlines():
            if not line.startswith("{"):
                continue
            try:
                document = json.loads(line)
            except json.JSONDecodeError:
                continue
            for alias, identity in document.get("factIds", {}).items():
                if alias in aliases and aliases[alias] != identity:
                    raise ValueError("Conflicting public fact alias")
                aliases[alias] = identity
    return aliases

def main():
    target = Path(sys.argv[1]).resolve()
    root = Path(__file__).resolve().parents[2]
    if not target.is_relative_to(root / "Artifacts/QA") or target.exists():
        raise ValueError("A new DungeonStory QA output is required")
    with urlopen("http://127.0.0.1:8780/api/session", timeout=20) as response:
        session = json.load(response)
    if len(session["rows"]) != 100:
        raise ValueError("Expected100 requests")
    rows = []
    for row in session["rows"]:
        wire = row["wire"]
        schema = wire["response_format"]["json_schema"]["schema"]
        text = "\n".join(m["content"] for m in wire["messages"])
        selection_matches = re.findall(r"(?m)^selectionId=([^\r\n\s]+)", text)
        offered = re.findall(r"(?m)^evidenceFactIds=([^\r\n]+)", text)
        evidence_ids = [s for chunk in offered for s in chunk.split(",") if s]
        aliases = public_fact_aliases(wire)
        facts = []
        for fact in row["presentation"]["facts"]:
            explicit = re.search(r"\[evidence=([^\]\s]+)\]", fact.get("text", ""))
            item = dict(fact)
            item["responseEvidenceId"] = explicit.group(1) if explicit else aliases.get(fact["id"], fact["id"])
            facts.append(item)
        rows.append(dict(number=row["rowNumber"], requestKey=row["requestKey"], profile=row["profileId"],
            sourceWireSha256=row["sourceWireSha256"], wire=wire, responseSchema=schema,
            publicSelectionIds=selection_matches, explicitlyOfferedEvidenceIds=evidence_ids,
            presentation=dict(row["presentation"], facts=facts), quality=row["qualityProfile"]))
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open("x", encoding="utf-8") as stream:
        json.dump(dict(schema="dungeonstory-pilot-authoring-context@1", rows=rows,
            sourceInputManifest="Artifacts/QA/Pilot100-20261011-b132-r1/input-review-r1/manifest.json",
            authoringOnly=True, humanApprovalClaimed=False, trainingEligible=False), stream, ensure_ascii=False, indent=2)
    print("AUTHORING_CONTEXT100=FROZEN")

if __name__ == "__main__": main()
