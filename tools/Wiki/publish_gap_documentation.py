"""Project reviewed GAP publication clauses into existing player guides.

The GAP register owns the approved public sentences. This script does not infer
new rules and never publishes exclusions, evidence paths, audit metadata, or
history rows. Generated entity data is handled by the wiki model generator.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter, defaultdict
from pathlib import Path


START = "<!-- reviewed-rules:start -->"
END = "<!-- reviewed-rules:end -->"
VERSION = "0.0.1v"
TARGET_OVERRIDES = {
    "GAP-005": "residents-and-work",
    "GAP-026": "medical-care-and-surgery",
    "GAP-027": "medical-care-and-surgery",
    "GAP-028": "medical-care-and-surgery",
    "GAP-110": "infrastructure",
    "GAP-159": "medical-care-and-surgery",
}
INTERNAL_PATTERN = re.compile(
    r"(?:\.asset|\.cs\b|GAP-|\b[A-Z][A-Za-z]+"
    r"(?:Runtime|Service|Ability|Definition|Policy|Command|State|Id)\b|"
    r"\b[a-z]+:[a-z0-9-]+)"
)
PUBLIC_CLAUSE_OVERRIDES = {
    ("GAP-018", 1): "범위를 벗어난 온도로 단계가 중단되는 번식 모드는 알·포자·핵분열뿐이다. 임신·골렘은 이 규칙의 적용 대상에서 제외된다.",
    ("GAP-046", 0): "비전 기초 청사진(6104)은 구매 시 실물 청사진이 하차장에 배송된다. 이후 관련 연구의 선행·보관·시설 조건을 충족해 연구를 진행해야 해금된다.",
    ("GAP-070", 1): "병입 목표는 같은 깨끗한 물 아이템의 전체 스택 수량 합계를 기준으로 하며 충전소별 버퍼는 기준에서 제외한다. 목표 이상이면 병입을 멈춘다.",
    ("GAP-075", 0): "응유 조합식 자체는 사이클당 물0.4·폐수4.2를 요구하고 수동 급수를 허용한다. 시설·보조 설비가 요구하는 추가 물은 이 물0.4에 별도로 더한다.",
    ("GAP-076", 2): "화물 진행도는 속도×경과시간만큼 증가하지만 한 번의 갱신에서 다음 한 구간까지만 이동한다. 화물 수용량은 개별 아이템 수량 대신 운송 화물 개수를 기준으로 한다.",
    ("GAP-084", 0): "부담/기능 저하/위급/붕괴의 일반 작업 기본 배율은0.9/0.75/0.5/0.1, 정밀작업 기본 배율은0.85/0.6/0.35/0.35다. 실제 작업 속도는 이 표에 현재 조명 적응 배율을 별도로 결합해 산정한다.",
    ("GAP-088", 3): "가중치는 교체 때의 추첨 확률이다. 달력상의 점유 일수 비율과 구분하며 기후·현재 날씨·남은 일수·일일 잡음은 저장한다.",
    ("GAP-107", 1): "안전 한도는 다음 반복 진입의 중단 기준이다. 설정 숫자는 불합격품의 제작·해체가 모두 완결되는 횟수를 보장하지 않는다.",
    ("GAP-108", 0): "불합격품은 물리 의복으로 남고 정책에 따라 보관·판매 대기 또는 해체로 진행한다. 판매 대기 상태에서는 실제 거래가 확정될 때까지 현금이 늘지 않는다.",
    ("GAP-123", 1): "이 수치는 기분 정책 적용 전 기본 요인이다. 최종 기분 감소는 캐릭터별 면역과 보정을 적용한 뒤 결정된다.",
    ("GAP-132", 1): "작물당 활성 품종 상한은12, 외부 참조가 없는 동결 품종의 보관 상한은32다.32는 작물별 동결 품종 보관에 적용한다.",
    ("GAP-166", 1): "작성 회복이 없으면 역할별 기본 회복을 사용한다. 예를 들어 휴식은 수면35·기분12, 훈련은 재미15·기분5이며 작성 회복이 있으면 이 값을 추가하지 않는다.",
    ("GAP-179", 0): "마취 장치는 마취 지원시설이다. 마취도0.35와 기본 시설 성공보정+0.0105를 제공한다.",
    ("GAP-183", 1): "이 성공 보정은 수술 성공 계산에 반영된다. 제작 부품은 생산 작업이 확정한 작업자 품질을 받아 생성된다.",
    ("GAP-189", 0): "시설의 방 프로필 기여가 꺼져 있어도 시설 역할과 방어 기여는 유지된다.",
    ("GAP-189", 1): "방 프로필에 기여하는 시설의 호화·위생 점수는 실제 방 환경값에도 더해진다. 환경 성능 증가는 해당 호화·위생 기여분에 한정된다.",
    ("GAP-203", 0): "변기·화장실 칸막이·세면대·목욕통·수건걸이·청소도구함·바닥 배수구의 청소 완료는 대상 시설과 같은 운영 방 구성품의 청결을 100으로 설정한다.",
    ("GAP-205", 0): "간이 화덕과 고기 그릴은 생산 주문 없이 수동 조리하며 식량1과 연료1을 사용해 기본 일반 식사2/3을 만든다. 기본 작업값은1.1/1.3이다.",
    ("GAP-206", 2): "엄폐에 가하는 피해가 남은 내구도 이상이면 즉시 제거를 요청한다. 내구도0 뒤 추가 피격은 필요하지 않다.",
    ("GAP-227", 0): "적용 가능한 필수 신체 기능이 요구 임계치에 못 미치면 작업 성능을 사용할 수 없다. 종족·해부상 미적용인 기능은 필수 검사 전에 제외하며 실패는 적용 대상 필수 기능의 부족으로 판정한다.",
    ("GAP-233", 1): "세 번째 상태의 현재 화면 문구는 '민첩 7+'지만 실제 제한은 제작 숙련400 XP 이상이다.",
    ("GAP-240", 1): "직원은 구매 역할의 소매 시설을 쇼핑 방문 후보로 선택할 수 없다. 훈련·연구·마나·화장실·위생 시설은 각 이용 조건에 따라 별도로 판정한다.",
    ("GAP-244", 3): "이 수치는 기분과 상황 보정 전 기본 우선 점수다. 실제 행동 선택에서는 다른 후보와 보정을 함께 비교한다.",
    ("GAP-245", 2): "같은 저기분 구간에서 개별 업무 후보 점수는1→0.2배로 줄고, 기다리기·둘러보기 후보 점수 하한은0.48→0.9로 높아진다. 최종 실행 행동은 모든 후보 점수를 비교한 뒤 결정한다.",
    ("GAP-249", 0): "개조·다시 조정·이전 주문 중 하나가 이미 진행 중인 시설에는 새 개체 진화 주문을 승인할 수 없다.",
    ("GAP-252", 0): "그림자늑대 습격은 수량이 남은 바닥 식량 가운데 보관·운반 중인 묶음을 제외하고 외부 진입로에서 도달 가능한 묶음을 표적으로 삼는다.",
    ("GAP-256", 0): "도착 화물의 품목별 수량은 원래 수량×경로 전력÷100을 가장 가까운 정수로 반올림하며, 정확히 반이면 짝수 쪽을 고른다. 결과는 최소1개다. 예를 들어3개×80%는2개다.",
    ("GAP-260", 0): "같은 칸·오물 종류·벽 얼룩 여부·배출자의 오물은 합쳐진다. 추가량은 최소0.1이며 기존 오물에 합칠 때 양을 최대100으로 제한한다. 새 오물 생성량에는 병합 상한을 적용하지 않는다.",
    ("GAP-269", 0): "문을 제외한 구조벽 공사는 작업자 퇴로, 입구·외부 동선, 먼저 필요한 문, 남은 공사 접근을 차례로 검사하고 안전하지 않으면 해당 사유로 대기한다.",
    ("GAP-269", 1): "강제 공사는 불안전 판정을 '강제 공사: 갇힘 가능' 경고로 바꾸어 안전 검사상 진행을 허용한다. 강제 지시에서도 갇힘 가능성은 남는다.",
    ("GAP-270", 1): "층 중앙 접근은 이동 속도0.9배로 진행하고, 이동 중 모습을 숨긴 채2초를 기다린 뒤 반대 층에 배치하며0.06초 후 논리 목적지에 정렬하고 모습을 복원한다. 전체 소요 시간은 접근 시간에2.06초를 더해 결정된다.",
    ("GAP-271", 1): "기본 작업 위치는 같은 층의 왼쪽 끝 점유 칸 중심에서 오른쪽 끝 점유 칸 중심까지 거리의85%, 계산 위치는75% 지점이다. 이 숫자는 위치 비율이며 작업·계산 속도에는 적용하지 않는다. 지정된 위치가 있으면 지정 위치를 우선한다.",
    ("GAP-275", 1): "시설 선택용 완료 예상 시간은 이동 예상 시간에 앞선 이용자·예약자 수와 수용량으로 계산한 회차 수×예상 서비스 시간을 더한다. 회차는 최소1이며 예상 서비스 시간은 시설 기본값 최소0.1초, 식사일 때 최소4초에 방문객 체류 배율 최소0.1을 곱한다. 이 값은 후보 선택을 위한 예상치로 사용한다.",
}
PUBLIC_TITLE_OVERRIDES = {
    "GAP-027": "특수 기관의 신체 기능 담당 범위",
    "GAP-034": "수면 휴식·휴무의 실제 응답 임계값",
    "GAP-046": "비전 기초 청사진(6104)의 실제 해금 절차",
    "GAP-061": "전력망 연결·우선 공급·최소 가동 비율과 저장 규칙",
    "GAP-075": "조합식의 폐수량·폐수 종류·수동 공급과 물 수치 적용 범위",
    "GAP-080": "컨베이어 경로 저장·복원 후 재계산·승인 상태",
    "GAP-121": "수술 도감의 조사 수치",
    "GAP-233": "생산 주문 작업자 정책의 실제 선택과 표시 기준",
    "GAP-261": "노화 치료 절차별 전용 주시설 조건",
}


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def guide_id(path: Path) -> str:
    source = path.read_text(encoding="utf-8-sig")
    match = re.search(r"(?m)^id:\s*([^\s]+)\s*$", source)
    if not match:
        raise ValueError(f"Guide has no frontmatter ID: {path}")
    return match.group(1)


def evidence_guides(row: dict, known: set[str]) -> list[str]:
    found = []
    for evidence in row.get("wiki_source_evidence", []):
        normalized = evidence["path"].replace("\\", "/")
        match = re.search(r"/content/guides/([^/]+)\.md$", normalized)
        if match and match.group(1) in known and match.group(1) not in found:
            found.append(match.group(1))
    return found


def target_guide(row: dict, known: set[str]) -> str:
    if row["id"] in TARGET_OVERRIDES:
        return TARGET_OVERRIDES[row["id"]]
    owner = row.get("owner", "").lower()
    occurrences = [
        (owner.find(candidate), -len(candidate), candidate)
        for candidate in known
        if owner.find(candidate) >= 0
    ]
    if occurrences:
        return min(occurrences)[2]
    candidates = evidence_guides(row, known)
    if candidates:
        return candidates[0]
    raise ValueError(f"No guide target for {row['id']}: {row.get('owner', '')}")


def canonical_guide(value: str, redirects: dict[str, str]) -> str:
    seen: set[str] = set()
    current = value
    while current in redirects:
        if current in seen:
            raise ValueError(f"Guide redirect cycle: {value}")
        seen.add(current)
        current = redirects[current]
    return current


def without_managed_block(source: str) -> str:
    if START not in source and END not in source:
        return source.rstrip() + "\n"
    if source.count(START) != 1 or source.count(END) != 1:
        raise ValueError("Managed guide markers are unbalanced or duplicated")
    before, tail = source.split(START, 1)
    _, after = tail.split(END, 1)
    return (before.rstrip() + "\n\n" + after.lstrip()).rstrip() + "\n"


def published_clauses(row: dict) -> list[str]:
    return [
        PUBLIC_CLAUSE_OVERRIDES.get((row["id"], index), clause).strip()
        for index, clause in enumerate(row["wiki_publication"]["write"])
    ]


def public_title(row: dict) -> str:
    if row["id"] in PUBLIC_TITLE_OVERRIDES:
        return PUBLIC_TITLE_OVERRIDES[row["id"]]
    return re.sub(r"\s+누락$", "", row["title"]).strip()


def render_section(layout: dict, rows_by_id: dict[str, dict]) -> str:
    lines = [START, f"## {layout['sectionTitle']}", "", layout["lead"], ""]
    for group in layout["groups"]:
        lines.extend([f"### {group['title']}", ""])
        for gap_id in group["gapIds"]:
            lines.extend([" ".join(published_clauses(rows_by_id[gap_id])), ""])
    lines.append(END)
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    root = args.repo_root.resolve()
    register_path = root / "Artifacts/QA/wiki-system-audit-2026-09-05/missing-register.json"
    register = load_json(register_path)
    layout_path = root / "tools/Wiki/gap_editorial_layout.ko.json"
    editorial_layout = load_json(layout_path)
    layouts = editorial_layout["guides"]
    guide_root = root / f"wiki/game-versions/{VERSION}/content/guides"
    guide_paths = {guide_id(path): path for path in guide_root.glob("*.md")}
    known = set(guide_paths)
    navigation_path = guide_root / "guide-navigation.json"
    navigation = load_json(navigation_path)
    redirects = {
        page["id"]: page["redirect_to"]
        for page in navigation["pages"]
        if page.get("redirect_to")
    }
    rows_by_guide: dict[str, list[dict]] = defaultdict(list)
    assignments = []
    documentation_actions = []
    errors = []

    for row in register["items"]:
        clauses = published_clauses(row)
        if not clauses:
            action = row.get("documentation_action", "").strip()
            if not action:
                errors.append(f"{row['id']} has neither public clauses nor a documentation action")
            else:
                documentation_actions.append({"id": row["id"], "action": action})
            continue
        target = canonical_guide(target_guide(row, known), redirects)
        if target not in known:
            errors.append(f"{row['id']} redirects to an unknown guide: {target}")
            continue
        for clause in clauses:
            if INTERNAL_PATTERN.search(clause):
                errors.append(f"{row['id']} contains an internal token: {clause}")
            if clause in row["wiki_publication"].get("do_not_write", []):
                errors.append(f"{row['id']} publishes an excluded clause verbatim")
        rows_by_guide[target].append(row)
        assignments.append({
            "id": row["id"],
            "guide": target,
            "sourceTitle": row["title"],
            "publicTitle": public_title(row),
            "clauseCount": len(clauses),
            "clauseHashes": [sha256_bytes(value.encode("utf-8")) for value in clauses],
            "publishedClauses": clauses,
        })

    all_ids = {row["id"] for row in register["items"]}
    assigned_ids = {row["id"] for row in assignments} | {row["id"] for row in documentation_actions}
    if all_ids != assigned_ids:
        errors.append(f"Active ID coverage mismatch: missing={sorted(all_ids-assigned_ids)}, extra={sorted(assigned_ids-all_ids)}")
    if len(assigned_ids) != len(assignments) + len(documentation_actions):
        errors.append("Duplicate active GAP assignment")
    if set(TARGET_OVERRIDES.values()) - known:
        errors.append("A target override names an unknown guide")
    if set(layouts) != set(rows_by_guide):
        errors.append(
            "Editorial guide coverage mismatch: "
            f"missing={sorted(set(rows_by_guide)-set(layouts))}, "
            f"extra={sorted(set(layouts)-set(rows_by_guide))}"
        )

    editorial_groups: dict[str, str] = {}
    for target, layout in layouts.items():
        if not layout.get("sectionTitle", "").strip():
            errors.append(f"Editorial section title is empty: {target}")
        if not layout.get("lead", "").strip():
            errors.append(f"Editorial section lead is empty: {target}")
        elif INTERNAL_PATTERN.search(layout["lead"]):
            errors.append(f"Editorial section lead contains an internal token: {target}")
        group_titles = [group.get("title", "").strip() for group in layout.get("groups", [])]
        if not group_titles or any(not title for title in group_titles):
            errors.append(f"Editorial groups are empty or untitled: {target}")
        if len(group_titles) != len(set(group_titles)):
            errors.append(f"Editorial group title is duplicated: {target}")
        layout_ids = [gap_id for group in layout.get("groups", []) for gap_id in group.get("gapIds", [])]
        target_ids = [row["id"] for row in rows_by_guide.get(target, [])]
        if len(layout_ids) != len(set(layout_ids)):
            errors.append(f"Editorial GAP assignment is duplicated: {target}")
        if set(layout_ids) != set(target_ids):
            errors.append(
                f"Editorial GAP coverage mismatch for {target}: "
                f"missing={sorted(set(target_ids)-set(layout_ids))}, "
                f"extra={sorted(set(layout_ids)-set(target_ids))}"
            )
        for group in layout.get("groups", []):
            for gap_id in group.get("gapIds", []):
                editorial_groups[gap_id] = group["title"]

    for assignment in assignments:
        assignment["editorialGroup"] = editorial_groups.get(assignment["id"], "")
        if not assignment["editorialGroup"]:
            errors.append(f"Published GAP has no editorial group: {assignment['id']}")
    if errors:
        raise ValueError("\n".join(errors))

    original_hashes = {}
    output_hashes = {}
    cleaned_redirect_hashes = {}
    for target, path in sorted(guide_paths.items()):
        if target not in rows_by_guide and target not in redirects and START not in path.read_text(encoding="utf-8-sig"):
            continue
        path = guide_paths[target]
        source = path.read_text(encoding="utf-8-sig")
        original_hashes[path.relative_to(root).as_posix()] = sha256_bytes(path.read_bytes())
        output = without_managed_block(source)
        if target in rows_by_guide:
            rows = rows_by_guide[target]
            rows_by_id = {row["id"]: row for row in rows}
            output = output.rstrip() + "\n\n" + render_section(layouts[target], rows_by_id)
            output_hashes[path.relative_to(root).as_posix()] = sha256_bytes(output.encode("utf-8"))
        elif target in redirects:
            cleaned_redirect_hashes[path.relative_to(root).as_posix()] = sha256_bytes(output.encode("utf-8"))
        if args.apply:
            path.write_text(output, encoding="utf-8", newline="\n")

    report = {
        "schemaVersion": 1,
        "gameVersion": VERSION,
        "status": "applied" if args.apply else "dry-run",
        "sourceRegister": register_path.relative_to(root).as_posix(),
        "sourceRegisterSha256": sha256_bytes(register_path.read_bytes()),
        "editorialLayout": layout_path.relative_to(root).as_posix(),
        "editorialLayoutSha256": sha256_bytes(layout_path.read_bytes()),
        "guideNavigation": navigation_path.relative_to(root).as_posix(),
        "guideNavigationSha256": sha256_bytes(navigation_path.read_bytes()),
        "redirectedGuideTargets": dict(sorted(redirects.items())),
        "activeGapCount": len(register["items"]),
        "publishedGapCount": len(assignments),
        "documentationActionCount": len(documentation_actions),
        "unpublishedGapCount": len(all_ids - assigned_ids),
        "publicationComplete": all_ids == assigned_ids,
        "publishedClauseCount": sum(row["clauseCount"] for row in assignments),
        "guideCount": len(rows_by_guide),
        "guideDistribution": dict(sorted(Counter(row["guide"] for row in assignments).items())),
        "assignments": assignments,
        "documentationActions": documentation_actions,
        "inputGuideHashes": original_hashes,
        "outputGuideHashes": output_hashes,
        "cleanedRedirectGuideHashes": cleaned_redirect_hashes,
        "errors": [],
    }
    report_path = root / "Artifacts/QA/wiki-system-audit-2026-09-05/gap-wiki-publication.json"
    if args.apply:
        report_path.write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
    print(json.dumps({key: value for key, value in report.items() if key not in {"assignments", "inputGuideHashes", "outputGuideHashes"}}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
