"""Reuse the earlier parsed review screen on loopback; no approval or training writes."""
from __future__ import annotations
import argparse
from collections import Counter
import json
from pathlib import Path
import sys
from export_pilot_authoring_context import public_fact_aliases

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
TRAINING = ROOT.parent / "DungeonStoryNarrativeAI/tools/v25_narrative_training"
sys.path.insert(0, str(TRAINING))
from pilot_input_intake import Snapshot, byte_hash, loads, require
from module_selection_reviewer.input_profile import InputProfileStore
from module_selection_reviewer.server import make_server, STATIC

LABELS = {0: "미평가", 1: "일상", 2: "가벼운 경험", 3: "의미 있는 경험",
          4: "강렬한 경험", 5: "결정적 경험", 6: "개인 경험 아님"}
NARRATIVE_WORKING = (TRAINING.parents[1] / "Artifacts/Working").resolve()
SKILL_PAIR_NAV_LABEL = "새 경험–스킬 100쌍 · 합성 보강 자료 / 내용 검토, 게임 재생·사람 승인 별도"
SKILL_PAIR_NAV = ('<nav style="padding:16px;background:#edf3e8">'
                  f'<a href="/skills"><strong>{SKILL_PAIR_NAV_LABEL}</strong></a></nav>')

def require_optional_pair(path, sha256, label):
    require(bool(path) == bool(sha256), f"{label} package and pin are both required")

def load_skill_pair_review(package_path, manifest_sha256, verify=None):
    """Verify one pinned create-only package, then freeze its served bytes."""
    package_path = package_path.resolve()
    require(package_path.is_relative_to(NARRATIVE_WORKING),
            "skill pairs outside NarrativeAI working artifacts")
    manifest_path = package_path / "manifest.json"
    require(byte_hash(manifest_path.read_bytes()) == manifest_sha256,
            "skill pair package pin mismatch")
    if verify is None:
        from build_experience_skill_dataset import verify
    verify(package_path)
    html = (package_path / "index.html").read_bytes()
    dataset = (package_path / "dataset-drafts.jsonl").read_bytes()
    (package_path / "review.json").read_bytes()
    return html, dataset

def add_skill_pair_navigation(html):
    # The pinned action-draft renderer intentionally omits the optional body tag.
    # Its single main element, not a fabricated body fixture, is the anchor.
    marker = b"<main>"
    require(html.count(marker) == 1, "review HTML has no unique main for skill-pair navigation")
    return html.replace(marker, marker + SKILL_PAIR_NAV.encode("utf-8"), 1)

def load_store(manifest_path, policy_path, policy_sha256, captured_sha256):
    require(manifest_path.resolve().is_relative_to(ROOT / "Artifacts/QA")
            and policy_path.resolve().is_relative_to(ROOT / "Artifacts/QA"), "review outside DungeonStory")
    store = InputProfileStore(manifest_path, byte_hash(manifest_path.read_bytes()))
    snapshot = Snapshot(ROOT)
    policy = snapshot.document(policy_path.parent, dict(path=policy_path.name,
        bytes=policy_path.stat().st_size, sha256=policy_sha256))
    prepared_path = manifest_path.parent.parent / "policy-inputs-r1.json"
    prepared = snapshot.document(prepared_path.parent, dict(path=prepared_path.name,
        bytes=prepared_path.stat().st_size, sha256=policy["inputSha256"]))
    require(policy["inputSha256"] == byte_hash(prepared_path.read_bytes())
            and prepared["sourceManifestSha256"] == store.pilot_hash, "policy/input pin mismatch")
    require(policy["requests"] == len(store.rows) == 100 and policy["policies"] == 149
            and policy["policyVersion"] == 3 and policy["freshGameplay"] is False
            and policy["originalWireModified"] is False and policy["trainingEligible"] is False
            and policy["humanApprovalClaimed"] is False,
            "invalid current-policy review")
    result = {row["requestKey"]: row for row in policy["rows"]}
    captured_path = manifest_path.parent / "captured-importance.json"
    captured = {row["requestKey"]: row for row in snapshot.document(captured_path.parent,
        dict(path=captured_path.name, bytes=captured_path.stat().st_size, sha256=captured_sha256))["rows"]}
    distribution = Counter()
    for row in store.rows:
        evaluated = result[row["requestKey"]]
        require(evaluated["sourceWireSha256"] == row["sourceWireSha256"], "policy wire mismatch")
        by_id = {f["id"]: f for f in evaluated["facts"]}
        require(len(by_id) == len(evaluated["facts"]) and set(by_id) == {f["id"] for f in row["presentation"]["facts"]},
                "policy/public fact exact-set mismatch")
        scores, cats, unknown = [], [], 0
        for fact in row["presentation"]["facts"]:
            value = by_id[fact["id"]]
            original = captured[row["requestKey"]]["facts"][fact["id"]]
            require(original["sourceFactId"] == value["sourceFactId"], "policy captured identity differs")
            assessment = value["assessment"]
            category = assessment["category"] if assessment else 0
            importance = assessment["importance"] if category != 0 else None
            # Profile context is not an experience or a missing experience score.
            if fact["presentationKind"] == "event":
                if importance is not None: scores.append(importance)
                else: unknown += 1
                cats.append(category)
            elif fact["presentationKind"] == "unknown":
                unknown += 1
                cats.append(0)
            fact["importance"] = dict(salience=importance, tier=None, tierLabel=LABELS[category],
                formulaImportancePoints=original["formulaImportancePoints"], source="current-policy-review")
            fact["currentExperience"] = assessment
            fact["capturedImportance"] = original
        core = sum(c in (4, 5) for c in cats)
        band = "strong" if core else "meaningful" if 3 in cats else "routine" if any(c in (1, 2) for c in cats) else "unassessed" if 0 in cats else "not-experience"
        # Machine grouping is separate from human approval and memory storage tiers.
        band = {"strong":"강렬한 경험 포함", "meaningful":"의미 있는 경험", "routine":"일상 중심",
                "unassessed":"미평가 근거 포함", "not-experience":"개인 경험 아님"}[band]
        distribution[band] += 1
        row["qualityProfile"] = dict(experienceBand=band, strongExperienceFacts=core,
            unknownFacts=unknown, contextFacts=len(row["presentation"]["characterContext"]),
            eventFacts=len(row["presentation"]["events"]),
            currentPolicyVersion=3, answerStatus=row["inputStatus"]["responseStatus"])
        row["importanceSummary"] = dict(maxSalience=max(scores) if scores else None,
            coreFactCount=core, scoredFactCount=len(scores), unknownSalienceCount=unknown,
            legacyPointFactCount=sum(f["capturedImportance"]["formulaImportancePoints"] is not None
                                     for f in row["presentation"]["events"]))
        row["presentation"]["notices"].append("현행 경험 평가를 별도로 표시합니다. 원 전송 입력과 수집 당시 점수는 변경하지 않았습니다.")
        if row["inputStatus"]["responseStatus"] == "observed-fail":
            row["presentation"]["notices"].append("이 입력의 이전 모델 응답은 의미 검수에서 실패했습니다. 정답 응답으로 포함하지 않았습니다.")
    store.review_context.update(importanceAvailable=True, importanceThreshold=.8,
        currentExperiencePolicyVersion=3, experienceDistribution=dict(distribution),
        freshGameplay=False, source9TrainingIntakeComplete=False, trainingEligible=False)
    store.dataset_label = "100개 프로파일 · 현행 경험 정책 v3"
    store.importance_snapshot = snapshot
    return store

def attach_answers(store, answers_path, answers_sha256):
    require(answers_path.resolve().is_relative_to(ROOT / "Artifacts/QA"), "answers outside DungeonStory")
    bundle = store.importance_snapshot.document(answers_path.parent, dict(path=answers_path.name,
        bytes=answers_path.stat().st_size, sha256=answers_sha256))
    require(bundle["schema"] == "dungeonstory-pilot-answer-drafts@1"
            and bundle["sourceManifestSha256"] == store.pilot_hash
            and bundle["requests"] == 100 and bundle["candidates"] == 178
            and bundle["authoringOrigin"] == "assistant-authored-draft"
            and bundle["trainingEligible"] is False and bundle["humanApprovalClaimed"] is False,
            "invalid answer draft bundle")
    answers = {a["requestKey"]: a for a in bundle["rows"]}
    require(len(answers) == len(bundle["rows"]) == 100 and set(answers) == {r["requestKey"] for r in store.rows},
            "answer/input exact-set mismatch")
    for row in store.rows:
        answer = answers[row["requestKey"]]
        require(answer["sourceWireSha256"] == row["sourceWireSha256"] and answer["profile"] == row["profileId"]
                and answer["validation"] == dict(schema="PASS", publicReferences="PASS", gameplayReplay="NOT_RUN", humanReview="NOT_REVIEWED")
                and answer["trainingEligible"] is False and answer["humanApprovalClaimed"] is False,
                "answer source/status mismatch")
        row["authoredAnswer"] = answer
        aliases = public_fact_aliases(row["wire"])
        facts_by_id = {aliases.get(f["id"], f["id"]): f for f in row["presentation"]["facts"]}
        for candidate in answer["displayCandidates"]:
            for evidence in candidate["evidence"]:
                fact = facts_by_id.get(evidence["id"])
                if fact:
                    evidence["presentationKind"] = fact["presentationKind"]
    store.dataset_label = "100개 입력–답변 초안 · 178개 후보"
    store.review_context.update(authoredAnswerPairs=100, authoredCandidates=178, answerDrafts=True)

def attach_identity(store, identity_path, identity_sha256):
    require(identity_path.resolve().is_relative_to(ROOT / "Artifacts/QA"), "identity outside DungeonStory")
    bundle = store.importance_snapshot.document(identity_path.parent, dict(path=identity_path.name,
        bytes=identity_path.stat().st_size, sha256=identity_sha256))
    require(bundle["schema"] == "dungeonstory-pilot-name-role-review@1" and bundle["derivedInput"] is True
        and bundle["originalInputsModified"] is False and bundle["trainingEligible"] is False
        and bundle["humanApprovalClaimed"] is False, "invalid name/role review")
    identities = {r["requestKey"]: r for r in bundle["rows"]}
    require(len(identities) == len(bundle["rows"]) == 100 and set(identities) == {r["requestKey"] for r in store.rows},
        "identity/input exact-set mismatch")
    for row in store.rows:
        entry = identities[row["requestKey"]]
        require(entry["sourceWireSha256"] == row["sourceWireSha256"], "identity wire mismatch")
        prepared = dict(entry["preparedWire"])
        require(len(prepared["messages"]) == len(row["wire"]["messages"]) + 1, "identity supplement count differs")
        prepared["messages"] = prepared["messages"][:-1]
        require(prepared == row["wire"], "identity supplement changed historical input")
        row["identityReview"] = entry
        aliases = public_fact_aliases(row["wire"])
        response_facts = {}
        for fact in row["presentation"]["facts"]:
            resolved = entry["facts"].get(fact["id"])
            if resolved:
                require(resolved["originalText"] == fact["displayText"], "identity fact differs")
                fact["recordedDisplayText"] = fact["displayText"]
                fact["displayText"] = resolved["displayText"]
                fact["identityParticipants"] = resolved["participants"]
                response_facts[aliases.get(fact["id"], fact["id"])] = fact
        if row.get("authoredAnswer"):
            for candidate in row["authoredAnswer"]["displayCandidates"]:
                for fact in candidate["evidence"]:
                    resolved = response_facts.get(fact["id"])
                    if resolved:
                        fact["recordedText"] = fact["text"]
                        fact["text"] = resolved["displayText"]
                        fact["identityParticipants"] = resolved["identityParticipants"]
    store.review_context.update(nameRoleSeparated=True, derivedModelInput=True)

def serve(manifest_path, policy_path, policy_sha256, captured_sha256, port, answers_path=None, answers_sha256=None,
          identity_path=None, identity_sha256=None, scenario_path=None, scenario_sha256=None,
          action_drafts_path=None, action_drafts_sha256=None,
          skill_pairs_path=None, skill_pairs_sha256=None):
    store = load_store(manifest_path, policy_path, policy_sha256, captured_sha256)
    require(bool(answers_path) == bool(answers_sha256), "answer file and pin are both required")
    if answers_path:
        attach_answers(store, answers_path, answers_sha256)
    require(bool(identity_path) == bool(identity_sha256), "identity file and pin are both required")
    if identity_path:
        require(bool(answers_path), "identity review requires the answer review renderer")
        attach_identity(store, identity_path, identity_sha256)
    require(bool(scenario_path) == bool(scenario_sha256), "scenario package and pin are both required")
    scenario_html = None
    action_html = action_jsonl = None
    require(bool(action_drafts_path) == bool(action_drafts_sha256), "action draft package and pin are both required")
    if action_drafts_path:
        from build_plausible_action_dataset import verify as verify_action_drafts
        require(action_drafts_path.resolve().is_relative_to(TRAINING.parents[1] / "Artifacts/Working"),
                "action drafts outside NarrativeAI working artifacts")
        require(byte_hash((action_drafts_path / "manifest.json").read_bytes()) == action_drafts_sha256,
                "action draft package pin mismatch")
        verify_action_drafts(action_drafts_path)
        action_html = (action_drafts_path / "index.html").read_bytes()
        action_jsonl = (action_drafts_path / "dataset-drafts.jsonl").read_bytes()
    skill_html = skill_jsonl = None
    require_optional_pair(skill_pairs_path, skill_pairs_sha256, "skill pair")
    if skill_pairs_path:
        skill_html, skill_jsonl = load_skill_pair_review(skill_pairs_path, skill_pairs_sha256)
        if action_html is not None:
            action_html = add_skill_pair_navigation(action_html)
    if scenario_path:
        from reuse_production_scenario_pilot import verify as verify_scenarios
        require(scenario_path.resolve().is_relative_to(TRAINING.parents[1] / "Artifacts/Working"),
                "scenario package outside NarrativeAI working artifacts")
        require(byte_hash((scenario_path / "manifest.json").read_bytes()) == scenario_sha256,
                "scenario package pin mismatch")
        verify_scenarios(scenario_path)
        scenario_html = (scenario_path / "index.html").read_bytes()
    server = make_server(store, port)
    parent = server.RequestHandlerClass
    js = (STATIC / "app.js").read_text(encoding="utf-8")
    old = "점수와 등급은 수집 당시 값이며, 높은 점수만으로 결정적 서사나 학습 적격을 뜻하지 않습니다."
    require(js.count(old) == 1, "review template changed; do not silently patch unknown UI")
    js = js.replace(old, "현행 C# 정책 v3의 재평가입니다. 수집 당시 값은 각 사건 아래 별도로 표시합니다. 원 입력은 변경하지 않았습니다.")
    js = js.replace("핵심 기억", "강렬한 경험").replace("원장 점수 연결", "현행 정책 평가")
    js = js.replace("원장 중요도 미확인", "현행 정책 미평가")
    js = js.replace("사건을 읽고, 어울리는 효과와 서사를 검토해요", "LLM 데이터셋 100개 프로파일")
    js = js.replace("rows=rows.filter(row=>mode==='core'", "rows=rows.filter(row=>mode==='meaningful'?row.qualityProfile.experienceBand==='의미 있는 경험':mode==='routine'?row.qualityProfile.experienceBand==='일상 중심':mode==='not-experience'?row.qualityProfile.experienceBand==='개인 경험 아님':mode==='unassessed'?row.qualityProfile.experienceBand==='미평가 근거 포함':mode==='core'")
    js = js.replace("정답 초안 준비 대상 ${context.preliminaryDraftInputs}건 — 정식 출처·계보·의미 검증은 별도",
        "강렬 ${context.experienceDistribution['강렬한 경험 포함']||0} · 의미 있는 경험 ${context.experienceDistribution['의미 있는 경험']||0} · 일상 ${context.experienceDistribution['일상 중심']||0} · 개인 경험 아님 ${context.experienceDistribution['개인 경험 아님']||0} · 미평가 근거 포함 ${context.experienceDistribution['미평가 근거 포함']||0}건")
    js = js.replace("$('importanceThreshold').value||'0.7'", "$('importanceThreshold').value||'0.8'")
    js = js.replace("inputModeConfigured=true;", "inputModeConfigured=true; $('importanceThreshold').value='0.8'; $('inputOrder').value='importance';")
    js = js.replace("아직 답변이 없는 입력 검토 단계입니다.", "강렬한 경험과 일상 기록을 구분한 프로파일입니다. 정답 응답은 아직 작성하지 않았습니다.")
    replacements = {
        "'inputFacts','inputBackground','inputOffers'": "'inputFacts','inputBackground','inputUnknown','inputOffers'",
        "facts.filter(f=>!f.background).length": "view.events.length",
        "$('inputBackgroundSection').hidden=!facts.some(f=>f.background);":
            "$('inputBackgroundSection').hidden=!view.characterContext.length; "
            "$('inputUnknownSection').hidden=!view.unknownFacts.length;",
        "fact.background?'배경 정보':fact.eventLabel":
            "fact.presentationKind==='character-context'?'인물 기본정보':fact.presentationKind==='unknown'?'종류 미확인':fact.eventLabel",
        "if(importance){": "if(importance&&fact.presentationKind==='event'){",
        "$(fact.background?'inputBackground':'inputFacts').append(card);":
            "if(fact.presentationKind==='character-context')card.append(inputElement('p','사건이 아닌 인물 설정 · 중요도 평가 대상 아님','muted')); "
            "if(fact.presentationKind==='unknown')card.append(inputElement('p','사건인지 확인할 근거가 부족한 공개 정보','muted')); "
            "$(fact.presentationKind==='character-context'?'inputBackground':fact.presentationKind==='unknown'?'inputUnknown':'inputFacts').append(card);",
    }
    for old, new in replacements.items():
        require(js.count(old) == 1, "context/event template changed: " + old)
        js = js.replace(old, new)
    insertion = "card.append(badge);"
    require(js.count(insertion) == 1, "fact-card template changed")
    js = js.replace(insertion, insertion + "\n      if(fact.currentExperience)card.append(inputElement('p',fact.currentExperience.explanationKo||fact.currentExperience.reasonCode,'muted'));\n      if(fact.capturedImportance)card.append(inputElement('p','수집 당시: '+(fact.capturedImportance.salience===null?'원장 점수 미확인':Number(fact.capturedImportance.salience).toFixed(2))+' · '+fact.capturedImportance.tierLabel,'muted'));" )
    if identity_path:
        require(js.count("inputElement('p',fact.displayText));") == 1, "identity fact-card template changed")
        js = js.replace("inputElement('p',fact.displayText));", "inputElement('p',fact.displayText)); appendNameRole(card,fact);")
    if answers_path:
        replacements = {
            "renderPresentation(row.presentation);": "renderPresentation(row.presentation); renderAuthoredAnswer(row);",
            "입력 ${total}건 · 완성 정답 0쌍": "답변 작성 ${context.authoredAnswerPairs} / ${total} · 후보 ${context.authoredCandidates}개",
            "정답 미작성 · 읽기 전용": "답변 작성 완료 · 사람 검토 전",
            "입력 검토 · 읽기 전용": "답변 초안 검토",
            "정답 응답은 아직 작성하지 않았습니다.": "이름·서사·선택 효과·근거를 모두 작성했습니다.",
            "작성할 답변 ${view.outputCount}개": "작성된 답변 ${view.outputCount}개",
            "wire:row.wire,presentation:row.presentation": "wire:row.wire,presentation:row.presentation,authoredAnswer:row.authoredAnswer",
        }
        for old, new in replacements.items():
            require(js.count(old) == 1, "answer review template changed: " + old)
            js = js.replace(old, new)
        js += "\n" + Path(__file__).with_name("pilot_answer_review.js").read_text(encoding="utf-8")
    class Handler(parent):
        def do_GET(self):
            if self.path in ("/skills", "/api/skill-pairs") and skill_html is not None:
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                if self.path == "/skills":
                    return self.send_value(skill_html, content_type="text/html; charset=utf-8")
                return self.send_value(skill_jsonl, content_type="application/x-ndjson; charset=utf-8")
            if self.path in ("/actions", "/api/action-drafts") and action_html is not None:
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                if self.path == "/actions":
                    return self.send_value(action_html, content_type="text/html; charset=utf-8")
                return self.send_value(action_jsonl, content_type="application/x-ndjson; charset=utf-8")
            if self.path == "/scenarios" and scenario_html is not None:
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                return self.send_value(scenario_html, content_type="text/html; charset=utf-8")
            if self.path == "/":
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                html = (STATIC / "index.html").read_text(encoding="utf-8")
                html = html.replace('인물의 배경·이력', '인물 기본정보·배경 (사건 아님)')
                html = html.replace('<details id="inputBackgroundSection">',
                    '<details id="inputUnknownSection"><summary>종류를 확인하지 못한 공개 정보</summary>'
                    '<div id="inputUnknown"></div></details><details id="inputBackgroundSection">')
                if action_html is not None:
                    html = html.replace('<body>', '<body><nav style="padding:20px;background:#dcebdc">'
                        '<a href="/actions"><strong>전체 149종 행동: 맥락·결과·답변을 갖춘 합성 사례 보기</strong></a>'
                        '<p>이 아래는 이전 실제 수집 자료입니다. 새 합성 사례와 구분해 보존합니다.</p></nav>', 1)
                if scenario_html is not None:
                    html = html.replace('<body>', '<body><nav style="padding:16px;background:#e7f1e8">'
                        '<a href="/scenarios">새 시나리오 100개 구성 보기 — 기존 생성기 재사용 · 아직 실행 전 계획</a></nav>', 1)
                if skill_html is not None:
                    html = html.replace('<body>', '<body>' + SKILL_PAIR_NAV, 1)
                html = html.replace('<option value="core">',
                    '<option value="meaningful">의미 있는 경험</option><option value="routine">일상 중심</option>'
                    '<option value="not-experience">개인 경험 아님</option><option value="unassessed">미평가 근거 포함</option><option value="core">')
                if answers_path:
                    start = html.index('<section class="input-card input-answer">')
                    end = html.index('</section>', start) + len('</section>')
                    html = html[:start] + '<section class="input-card input-answer"><h3>작성한 답변 불러오는 중…</h3><p id="inputOutputGuide"></p></section>' + html[end:]
                return self.send_value(html.encode("utf-8"), content_type="text/html; charset=utf-8")
            if self.path in ("/api/dataset", "/api/dataset-names") and answers_path:
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                try:
                    store.read()
                    pairs = [dict(requestKey=row["requestKey"], profile=row["profileId"], input=row["wire"],
                        response=row["authoredAnswer"]["response"], sourceWireSha256=row["sourceWireSha256"],
                        authoringOrigin="assistant-authored-draft", sourceStatus="historical-controlled-capture",
                        validation=row["authoredAnswer"]["validation"], trainingEligible=False, humanApprovalClaimed=False)
                        for row in store.rows]
                    if self.path == "/api/dataset-names":
                        require(bool(identity_path), "name/role dataset is not loaded")
                        for pair, row in zip(pairs, store.rows):
                            pair["input"] = row["identityReview"]["preparedWire"]
                            pair["preparedWireSha256"] = row["identityReview"]["preparedWireSha256"]
                            pair["inputDerivation"] = "historical-wire-plus-pinned-name-role-context"
                            pair["identityContext"] = {k: row["identityReview"][k] for k in ("characters", "corrections")}
                    data = "".join(json.dumps(pair, ensure_ascii=False, separators=(",", ":")) + "\n" for pair in pairs)
                    return self.send_value(data.encode("utf-8"), content_type="application/x-ndjson; charset=utf-8")
                except (ValueError, OSError) as error:
                    return self.send_value({"error":str(error)},409)
            if self.path == "/app.js":
                if not self.valid_host(): return self.send_value({"error":"Invalid host"},403)
                return self.send_value(js.encode("utf-8"), content_type="text/javascript; charset=utf-8")
            return super().do_GET()
    server.RequestHandlerClass = Handler
    print(json.dumps(dict(url=f"http://127.0.0.1:{port}/", requests=100,
        distribution=store.review_context["experienceDistribution"], trainingEligible=False), ensure_ascii=False),flush=True)
    server.serve_forever()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--policy", required=True, type=Path)
    parser.add_argument("--policy-sha256", required=True)
    parser.add_argument("--captured-sha256", required=True)
    parser.add_argument("--port", type=int, default=8780)
    parser.add_argument("--answers", type=Path)
    parser.add_argument("--answers-sha256")
    parser.add_argument("--identity", type=Path)
    parser.add_argument("--identity-sha256")
    parser.add_argument("--scenario", type=Path)
    parser.add_argument("--scenario-sha256")
    parser.add_argument("--action-drafts", type=Path)
    parser.add_argument("--action-drafts-sha256")
    parser.add_argument("--skill-pairs", type=Path)
    parser.add_argument("--skill-pairs-sha256")
    args = parser.parse_args()
    serve(args.manifest, args.policy, args.policy_sha256, args.captured_sha256, args.port, args.answers, args.answers_sha256,
          args.identity, args.identity_sha256, args.scenario, args.scenario_sha256,
          args.action_drafts, args.action_drafts_sha256, args.skill_pairs, args.skill_pairs_sha256)
