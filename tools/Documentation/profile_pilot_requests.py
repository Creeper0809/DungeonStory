"""Profile immutable source-v9 requests, without generating responses or gameplay.

One row is one captured model request, never an offered candidate. Private campaign
and evidence sidecars are used only for provenance/distribution analysis; the public
request file contains the original wire messages and schema without enrichment.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import statistics


PROFILES = {
    "CharacterSkillModuleSelection": "기술",
    "AcquiredTraitModuleSelection": "특성",
    "EquipmentEvolutionModuleSelection": "장비",
    "FacilityEvolutionModuleSelection": "시설",
}


def digest(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def canonical(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


class Inputs:
    def __init__(self, root: Path):
        self.root = root.resolve(strict=True)
        self.checked = {}

    def path(self, base: Path, relative: str, *, must_exist=True) -> Path:
        item = Path(relative)
        if item.is_absolute() or ".." in item.parts:
            raise ValueError(f"Non-relative input: {relative}")
        path = base / item
        resolved = path.resolve(strict=must_exist)
        resolved.relative_to(self.root)
        for component in (path, *path.parents):
            if component == self.root:
                break
            if component.is_symlink() or (hasattr(component, "is_junction") and component.is_junction()):
                raise ValueError(f"Linked input is not allowed: {path}")
        return resolved

    def pin(self, path: Path) -> dict:
        if path not in self.checked:
            data = path.read_bytes()
            self.checked[path] = dict(path=path.relative_to(self.root).as_posix(),
                                      bytes=len(data), sha256=digest(data))
        return self.checked[path]

    def bound(self, base: Path, descriptor: dict) -> Path:
        path = self.path(base, descriptor["path"])
        observed = self.pin(path)
        if (observed["bytes"], observed["sha256"]) != (descriptor["bytes"], descriptor["sha256"]):
            raise ValueError(f"Input hash/length mismatch: {path}")
        return path

    def read(self, path: Path):
        self.pin(path)
        return json.loads(path.read_text(encoding="utf-8-sig"))

    def code_changed(self, descriptor, include_historical_code=False):
        path = self.path(self.root, descriptor["path"], must_exist=not include_historical_code)
        if not path.is_file():
            # Historical capture remains historical when its producer was removed.
            return True
        observed = self.pin(path)
        return (observed["bytes"], observed["sha256"]) != (descriptor["bytes"], descriptor["sha256"])


def offers_from_wire(wire: dict) -> dict:
    text = "\n".join(message["content"] for message in wire["messages"])
    compact = next((json.loads(line) for line in text.splitlines()
                    if line.startswith('{"C_columns"')), None)
    positive, drawback, unknown = set(), set(), set()
    if compact is not None:
        for row in compact["C"]:
            fields = dict(zip(compact["C_columns"], row))
            positive.update(compact["M"][i][0] for i in fields["positive_M"])
            drawback.update(compact["M"][i][0] for i in fields["drawback_M"])
    else:
        section = None
        for line in text.splitlines():
            line = line.strip()
            if line in ('positiveModules:', 'drawbackModules:'):
                section = positive if line == 'positiveModules:' else drawback
            elif line:
                offered = re.fullmatch(r'- id=([^;\s]+); meaning=.+', line)
                if offered and section is not None:
                    section.add(offered.group(1))
                else:
                    section = None
        for module, polarity in re.findall(
                r"(?:- moduleId=|^module=)([^;|\r\n ]+)\s*[;|]\s*polarity=(Positive|Drawback)",
                text, re.MULTILINE):
            (positive if polarity == "Positive" else drawback).add(module)
        # Historical public wires listed IDs but omitted their polarity. Keep the
        # omission as a profiling finding, never infer positivity from an ID/name.
        listed = set(re.findall(r"(?:- moduleId=|^module=)([^;|\r\n ]+)\s*[;|]", text, re.MULTILINE))
        unknown = listed - positive - drawback
    if not positive and not unknown:
        raise ValueError("No positive offers parsed; an unknown wire format must not count as empty offers.")
    result = dict(positive=sorted(positive), drawback=sorted(drawback))
    if unknown:
        result['polarityUnspecified'] = sorted(unknown)
    return result


def load_package(inputs: Inputs, manifest_path: Path, include_historical_code=False) -> tuple[list, dict]:
    base = manifest_path.parent
    manifest = inputs.read(manifest_path)
    if (manifest.get("schema") != "dungeonstory-gameplay-module-selection-source@9"
            or manifest.get("sourceTier") != "controlled-campaign"
            or manifest.get("integrationFixture") is not False):
        raise ValueError(f"Not a controlled source-v9 campaign: {manifest_path}")
    mechanics = inputs.read(inputs.bound(base, manifest["mechanics"]))
    campaign = inputs.read(inputs.bound(base, manifest["executedCampaign"]))
    source = inputs.read(inputs.bound(base, manifest["selectionSource"]))
    for document in (mechanics, campaign, source):
        if document["sourcePackageId"] != manifest["sourcePackageId"]:
            raise ValueError("Mixed package identities")
    changed_code = []
    for descriptor in mechanics["codeIdentity"]:
        if inputs.code_changed(descriptor, include_historical_code):
            changed_code.append(descriptor["path"])
    if changed_code and not include_historical_code:
        raise ValueError(f"Historical producer pins require explicit --include-historical-code: {changed_code}")
    inputs.bound(base, mechanics["sourceInventory"])
    inputs.bound(base, mechanics["b1EvidenceManifest"])
    pilot_seed = None
    for descriptor in campaign["files"]:
        inputs.bound(base, descriptor)
        if descriptor['path'] == 'campaign/pilot-initial-seed.json':
            preparation = inputs.read(inputs.bound(base, descriptor))
            if (preparation['testPreparationOnly'] is not True
                    or preparation['boundary'] != 'before-start-party-creation'
                    or type(preparation['initialSeed']) is not int
                    or preparation['initialSeed'] <= 0):
                raise ValueError('Invalid explicit pilot seed preparation')
            pilot_seed = preparation['initialSeed']
    if bool(campaign.get('explicitTestOnlyInitialSeed')) != (pilot_seed is not None):
        raise ValueError('Pilot seed declaration and preparation witness differ')
    if pilot_seed is not None:
        initial_descriptor = next(item for item in campaign['files'] if item['path'] == 'campaign/initial-world.json')
        initial = inputs.read(inputs.bound(base, initial_descriptor))
        sections = {item['sectionId']: json.loads(item['payloadJson']) for item in initial['sections']}
        if (sections['run.variables']['runSeed'] != pilot_seed
                or sections['foundation.random-streams']['rootSeed'] != pilot_seed):
            raise ValueError('Explicit pilot seed was not preserved in the official initial world')
    if campaign["fabricatedOutcome"] is not False or source["responseDataIncluded"] is not False:
        raise ValueError("Expected unmodified response-free campaign input")
    if len(source["selections"]) != manifest["selectionCount"]:
        raise ValueError("Selection count mismatch")
    rows, excluded_selections = [], []
    for selection in source["selections"]:
        profile = selection["profileId"]
        if profile not in PROFILES:
            raise ValueError("Unknown profile")
        # Character exports carry this field; current equipment/facility exports
        # instead bind their actual command witnesses in executed-campaign.files.
        # Do not invent a field or re-label a restored trait request as new.
        origin = selection.get("triggerOrigin")
        if profile in ("CharacterSkillModuleSelection", "AcquiredTraitModuleSelection") and origin != "actual-production-command":
            excluded_selections.append(dict(selectionSourceId=selection['selectionSourceId'], profile=profile,
                                            reason='not-a-new-proven-gameplay-trigger', observedOrigin=origin))
            continue
        for descriptor in selection["capture"].values():
            inputs.bound(base, descriptor)
        if "triggerWitness" in selection:
            inputs.bound(base, selection["triggerWitness"])
        original = inputs.bound(base, selection["wire"]["original"])
        restored = inputs.bound(base, selection["wire"]["restoredReplay"])
        if selection["wire"]["byteExactReplay"] is not True or original.read_bytes() != restored.read_bytes():
            raise ValueError("Original/restored wire differs")
        wire = inputs.read(original)
        witness = inputs.read(inputs.bound(base, selection["evidenceSidecar"]))
        if witness["wireSha256"].removeprefix("sha256:") != inputs.pin(original)["sha256"].removeprefix("sha256:"):
            raise ValueError("Witness wire identity differs")
        evidence = witness["evidence"]
        runs = sorted({event["runId"] for event in evidence})
        if not evidence or not all(runs):
            raise ValueError("Actual gameplay evidence/run identity missing")
        # Local owner/trigger IDs may recur in different games. The authoritative
        # ledger run identity keeps those separate while rejecting repeated exports.
        identity = [profile, runs, selection["ownerId"], selection["targetId"], selection["triggerId"]]
        outcome_types = Counter(event["outcomeTypeId"] for event in evidence)
        roles = Counter(part["roleId"] for event in evidence for part in event["participants"])
        activities = sorted({fact["value"] for event in evidence for fact in event["facts"]
                             if fact["factId"] in ("work-identity.work-id", "facility-role")})
        offers = offers_from_wire(wire)
        text = "\n".join(message["content"] for message in wire["messages"])
        warnings = []
        if offers.get('polarityUnspecified'):
            warnings.append('offered-module-polarity-missing-in-public-input')
        if len(offers["positive"]) == 1:
            warnings.append("single-positive-offer")
        if "CharacterPrefab(Clone)" in text:
            warnings.append("placeholder-character-name-in-public-input")
        if any(marker in text for marker in ('테스트 준비', '빠른 커밋 진단', '진단 패시브')):
            warnings.append('test-preparation-text-in-public-input')
        if len(outcome_types) == 1:
            warnings.append("single-gameplay-outcome-type")
        family = dict(profile=profile, template=campaign["lineage"]["planTemplateFamily"],
                      outcomeTypes=sorted(outcome_types), activities=activities, offers=offers)
        rows.append(dict(
            requestKey=digest(canonical(identity)), profile=profile,
            sourceManifest=inputs.pin(manifest_path), sourcePackageId=manifest["sourcePackageId"],
            selectionSourceId=selection["selectionSourceId"], sourceTier=manifest["sourceTier"],
            candidateCount=selection["candidateCount"], selectionIdCount=len(selection["selectionIds"]),
            ownerId=selection["ownerId"], targetId=selection["targetId"], triggerId=selection["triggerId"],
            correlationId=selection["correlationId"], ledgerRunIds=runs,
            triggerOrigin=origin, controlledSetup=campaign.get("controlledSetup"),
            explicitTestOnlyInitialSeed=pilot_seed,
            lineage=campaign["lineage"], scenarioFamily=family, scenarioFamilySha256=digest(canonical(family)),
            wire=inputs.pin(original), wireMessageCharacters=len(text), wireBytes=original.stat().st_size,
            officialCheckpoint=selection["capture"]["officialWorldCheckpoint"],
            restoredWireByteExact=True, actualEvidenceCount=len(evidence),
            observedOutcomeTypes=dict(sorted(outcome_types.items())), observedParticipantRoles=dict(sorted(roles.items())),
            offeredModules=offers, warnings=warnings,
            responseStatus="not-collected", responseReview=None,
            producerCodePinsCurrent=not changed_code, changedProducerCode=changed_code,
            fullCurrentSourceAuditRepeated=False,
            trainingEligible=False, humanApprovalClaimed=False,
            publicWire=wire))
    if not rows:
        raise ValueError('No new gameplay-triggered requests in this package; original or restored origins not inferred')
    return rows, dict(manifest=inputs.pin(manifest_path), requests=len(rows), excludedSelections=excluded_selections,
                      producerCodePins=len(mechanics["codeIdentity"]))


def unique_requests(rows: list) -> tuple[list, list]:
    unique, duplicates, identities, wires = [], [], set(), set()
    for row in rows:
        if row["requestKey"] in identities or row["wire"]["sha256"] in wires:
            duplicates.append(dict(selectionSourceId=row["selectionSourceId"], requestKey=row["requestKey"],
                                   reason="same-gameplay-request-or-exact-wire"))
            continue
        identities.add(row["requestKey"])
        wires.add(row["wire"]["sha256"])
        unique.append(row)
    return unique, duplicates


def write_new(path: Path, value, json_lines=False):
    data = (b"".join(canonical(item) + b"\n" for item in value) if json_lines
            else json.dumps(value, ensure_ascii=False, indent=2).encode("utf-8") + b"\n")
    with path.open("xb") as output:
        output.write(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--source-index", action="append", default=[])
    parser.add_argument("--package", action="append", default=[], help="Repository-relative source-v9 manifest path")
    parser.add_argument("--response-review", action="append", default=[])
    parser.add_argument("--include-historical-code", action="store_true",
                        help="Profile old immutable captures too, explicitly marking non-current producer pins; never upgrades eligibility")
    parser.add_argument("--output", required=True)
    parser.add_argument("--target", type=int, default=100)
    args = parser.parse_args()
    inputs = Inputs(args.root)
    if args.target <= 0:
        parser.error("target must be positive")
    output = inputs.root / args.output
    output.resolve().relative_to(inputs.root / "Artifacts" / "QA")
    if output.exists():
        raise FileExistsError(f"Create-only output already exists: {output}")
    manifests = []
    for relative in args.source_index:
        index = inputs.read(inputs.path(inputs.root, relative))
        for item in index['packages']:
            if 'nativeCampaignReport' in item:
                report = inputs.read(inputs.bound(inputs.root, item['nativeCampaignReport']))
                if report.get('passed') is not True:
                    raise ValueError('Source index native campaign did not pass')
            manifests.append(inputs.bound(inputs.root, item['manifest']))
    manifests.extend(inputs.path(inputs.root, path) for path in args.package)
    if not manifests:
        parser.error("at least one source index or package is required")
    rows, packages = [], []
    for path in manifests:
        found, package = load_package(inputs, path, args.include_historical_code)
        rows.extend(found)
        packages.append(package)
    rows, duplicates = unique_requests(rows)
    for relative in args.response_review:
        review_path = inputs.path(inputs.root, relative)
        review = inputs.read(review_path)
        wires = {item["sha256"] for item in review["responseArtifacts"] if item["path"].endswith("request-wire.json")}
        for row in rows:
            if row["wire"]["sha256"] in wires:
                row["responseStatus"] = "observed-" + review["semanticReview"]["status"].lower()
                row["responseReview"] = inputs.pin(review_path)
    lengths = sorted(row['wireMessageCharacters'] for row in rows)
    summary = dict(
        schema="dungeonstory-pilot-input-profile@1", targetRequests=args.target,
        uniqueRequests=len(rows), remainingRequests=max(0, args.target-len(rows)),
        countUnit="one captured request; candidates, repeated exports and restored wires do not add rows",
        collectionStatus="target-count-reached" if len(rows) >= args.target else "incomplete",
        byProfile={p: sum(row["profile"] == p for row in rows) for p in PROFILES},
        byResponseStatus=dict(Counter(row["responseStatus"] for row in rows)),
        byExplicitInitialSeed=dict(Counter(str(row['explicitTestOnlyInitialSeed'])
                                          if row['explicitTestOnlyInitialSeed'] is not None else 'not-explicit'
                                          for row in rows)),
        byScenarioFamily=dict(Counter(row["scenarioFamilySha256"] for row in rows)),
        warnings=dict(Counter(warning for row in rows for warning in row["warnings"])),
        uniqueLedgerRuns=len({run for row in rows for run in row["ledgerRunIds"]}),
        inputCharacters=dict(minimum=min(lengths), median=statistics.median(lengths),
                             p95=lengths[(95*len(lengths)+99)//100-1], maximum=max(lengths)),
        scenarioFamilyCount=len({row['scenarioFamilySha256'] for row in rows}),
        observedOutcomeTypes=dict(Counter(event for row in rows for event in row['observedOutcomeTypes'])),
        currentProducerCodeRequests=sum(row["producerCodePinsCurrent"] for row in rows),
        historicalProducerCodeRequests=sum(not row["producerCodePinsCurrent"] for row in rows),
        byProducerCodeCurrency={label: dict(
            requests=len(group),
            warnings=dict(Counter(warning for row in group for warning in row['warnings'])),
            inputCharacters=dict(minimum=min(row['wireMessageCharacters'] for row in group),
                                 median=statistics.median(row['wireMessageCharacters'] for row in group),
                                 maximum=max(row['wireMessageCharacters'] for row in group)))
            for label, group in ((label, [row for row in rows if row['producerCodePinsCurrent'] == current])
                                 for label, current in (('current', True), ('historical', False))) if group},
        duplicates=duplicates, packages=packages,
        limitations=["Controlled scenarios, not natural-play prevalence.",
                      "Source-bound sidecar outcomes are not all necessarily public selectable facts.",
                      "Scenario family groups use types/offers, not a semantic duplicate oracle.",
                      "Producer pins compared; historical differences are explicit, full B1/asset audit not repeated.",
                      "Count/distribution profiling is not editorial acceptance or training approval."],
        trainingEligible=False, humanApprovalClaimed=False, trainingPerformed=False,
        originalInputs=list(inputs.checked.values()))
    output.mkdir(parents=True, exist_ok=False)
    public = [dict(requestKey=row["requestKey"], profile=row["profile"],
                   wire=row["publicWire"], sourceWireSha256=row["wire"]["sha256"],
                   response=None, trainingEligible=False) for row in rows]
    private = [{key: value for key, value in row.items() if key != "publicWire"} for row in rows]
    write_new(output / "public-requests.jsonl", public, json_lines=True)
    write_new(output / "request-profiles.jsonl", private, json_lines=True)
    write_new(output / "profile-summary.json", summary)
    lines = ["# 100건 파일럿 — 입력 프로파일", "",
             f"확보한 독립 요청 **{len(rows)}/{args.target}건**, 남은 요청 **{summary['remainingRequests']}건**.", "",
             "후보 수·저장 복원·반복 출력은 독립 요청 수로 세지 않는다. 학습이나 사람 승인은 수행하지 않았다.", "",
             "| 유형 | 요청 |", "|---|---:|"]
    lines.extend(f"| {label} | {summary['byProfile'][profile]} |" for profile, label in PROFILES.items())
    lines.extend(["", "## 관측", "", f"- 실제 원장 run: {summary['uniqueLedgerRuns']}개.",
                  f"- 현재 수집 코드와 일치: {summary['currentProducerCodeRequests']}건; 코드 변경 전 자료: {summary['historicalProducerCodeRequests']}건(원본 보존).",
                  f"- 이 프로파일에 연결된 응답 상태: {json.dumps(summary['byResponseStatus'], ensure_ascii=False)}.",
                  f"- 입력 글자 수(토큰 수 아님): 최소 {min(lengths):,}, 중앙값 {statistics.median(lengths):,.0f}, 최대 {max(lengths):,}.",
                  f"- 시나리오/효과 조합 그룹: {summary['scenarioFamilyCount']}개. 그룹이 같다는 것만으로 같은 요청이라고 단정하지 않는다.",
                  f"- 경고: {json.dumps(summary['warnings'], ensure_ascii=False)}.",
                  "- 통제 시나리오 표본이며 자연 플레이의 분포로 일반화할 수 없다.",
                  "- 공개 요청 파일에는 원 전송 wire만 포함하며 내부 저장 상태/근거 sidecar를 추가하지 않았다.",
                  ("- 입력 수량 기준은 충족했다. 정답 응답 작성·의미 검수·학습 적격 판정은 별도 미완료다."
                   if len(rows) >= args.target else "- 현재 수량을 100건 프로파일링 완료나 학습 적격으로 표시하지 않는다."), ""])
    with (output / "PROFILE.md").open("x", encoding="utf-8", newline="\n") as report:
        report.write("\n".join(lines))
    print(json.dumps({key: summary[key] for key in (
        "collectionStatus", "uniqueRequests", "remainingRequests", "byProfile", "byResponseStatus", "warnings")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
