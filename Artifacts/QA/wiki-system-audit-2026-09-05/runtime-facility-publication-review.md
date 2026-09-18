# 내부 런타임 시설 42개 공개 투영 대조

조사일: 2026-09-06  
범위: `BuildingSO` runtime archetype42개, 건설 메뉴 가시성, 외부 zone 생성 권위, 공개 facility page  
판정 범위: 정적 원본·생성 결과 대조. 실제 UI 조작과 Unity Play Mode는 실행하지 않았다.

## 결론

- root content catalog에는 외부 zone40개와 월드 자원/오염 작업 대상2개가 immutable runtime `BuildingSO`로 들어 있다.
- 42개는 전부 음수 definition ID, `unlocked=false`이고 연구 해금 참조가 없다. 외부 zone은 플레이어가 짓는 시설이 아니라 `ExteriorActivityRuntime`이 적합한 빈 layer에 직접 생성한다.
- 그런데 공개 위키는 42개를 모두 일반 `facility` entity로 만들었다. 각 page는 facts2(분류·크기), relations0, 영어 placeholder summary를 가지며, 일부 summary는 공통 작성값18WU·재료1개를 건설 비용처럼 표시한다.
- 공개 모델은 내부 layer별 구현 복제40개를 각각 독립 시설로 보여 주지 않아야 한다. 내부 entity를 제외하고, 플레이어가 알아야 하는 외부 활동 개념8종과 실제 이용/작업 규칙은 관련 가이드에서 설명하는 편이 정확하다.

## 모집단

| 분류 | 수 | 작성 구조 | 공개 상태 |
| --- | ---: | --- | --- |
| 외부 zone archetype | 40 | 8 `ExteriorZoneType` × 5 `GridLayer` | page40, 같은 표시명 반복 |
| 월드 자원 작업 대상 | 1 | gather/logging/quarry용 runtime target | page1 |
| 월드 오염 작업 대상 | 1 | clean용 runtime target | page1 |
| 합계 | 42 | 모두 음수 ID·unlocked=false | page42·facts2·relations0 |

외부 layer는 Hallway(0), Building(1), WallFixture(3), CeilingFixture(4), FloorOverlay(5)다. layer 차이는 런타임이 빈 점유층을 고르기 위한 내부 구현 세부이며 독립적인 플레이어 시설 변형이 아니다.

## 외부 zone 8종

| 타입 | 공개 표시명 | 지원 업무/작성 ability |
| --- | --- | --- |
| Entrance | 입구 | marker 전용 |
| DropZone | 하차장 | Clean·Repair, 청결+40·피해감소35 외부 정비 |
| ReceptionPoint | 응대 지점 | Reception·Clean, 응대 준비·첫인상·기분과 외부 정비 |
| GuardPost | 경비 초소 | Guard·Repair, 순찰 준비/사건 탐지와 외부 정비 |
| PatrolPoint | 순찰 지점 | Guard, 순찰 준비25 |
| OutdoorRestSpot | 외부 휴식처 | Rest·Clean, 외부 휴식과 정비 |
| ExpeditionStaging | 출정 집결지 | marker 전용 |
| IncidentPoint | 외부 사건 지점 | Reception·Guard, 응대 준비45·순찰 준비45 |

이 표는 runtime 정의의 역할 구분을 위한 감사 근거다. 각 ability의 전체 효과·사건 상태기까지 이번 GAP 하나에서 전수 설명 완료로 간주하지 않는다.

## 건설 가능성 판정

- asset builder는 모든 runtime archetype을 `runtimeArchetype=Generic`, `unlocked=false`, 1×1로 만든다.
- 일반 건설 메뉴는 `building.unlocked` 또는 연구의 `IsBuildingUnlocked(building.id)`만 가시화한다.
- 음수 runtime ID를 해금하는 연구 자산 참조는0건이다.
- `ExteriorActivityRuntime`은 후보 칸의 빈 marker layer를 찾고 `IRuntimeBuildingArchetypeCatalog.RequireExteriorZone`으로 definition을 얻어 `ExteriorZoneMarker.InitializeRuntime`에 직접 넣는다.
- 월드 자원·오염도 같은 runtime catalog가 반드시 존재해야 하는 작업 target definition으로 요구한다.

따라서 공통18WU·재료1개를 공개 건설 비용으로 읽게 하는 것은 현재 게임의 생성 권위와 맞지 않는다.

## 공개 생성 원인

- content-db는 root catalog 등록과 일반 `BuildingSO` 타입 소비자를 근거로 이 레코드를 `active-authored`로 분류한다.
- wiki model의 `kind_for`는 `BuildingSO`를 runtime/public 구분 없이 `facility`로 매핑한다.
- 생성 page는 내부 archetype 여부·음수 ID·`unlocked=false`를 독자에게 알려 주지 않고 일반 시설 navigation에 포함한다.
- 동일 개념을 layer별5페이지로 복제해 검색 결과와 도감 수를 부풀린다.

## 권장 문서 경계

- 공개 facility entity에서는 `building:runtime:*` 42개를 제외하거나 명시적인 내부/개발자 범주로 격리한다.
- 하차장·응대·경비·순찰·외부 휴식·출정 집결·사건 지점 등 플레이어가 접하는 개념은 외부 활동/원정/손님/경비 가이드에서 실제 생성 조건과 이용 업무를 설명한다.
- layer별 definition, 음수 ID, placeholder18WU/BOM은 사용자 문서가 아니라 구현 인덱스에 남긴다.
- 기존 GAP-109~111의 일반 시설 건설비 오류/누락 및 랜드마크와 중복 합산하지 않는다.

## 직접 확인한 원본

- `Assets/Resources/SO/Buildings/RuntimeArchetypes/` 42개 asset
- `Assets/Scripts/Services/Buildings/RuntimeBuildingArchetypeCatalog.cs`
- `Assets/Scripts/Services/Buildings/Editor/RuntimeBuildingArchetypeAssetBuilder.cs`
- `Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs`
- `Assets/Scripts/Controllers/Grid/DungeonStory/UI/GridConstructTab.cs`
- `Assets/Scripts/Services/Economy/Editor/V23BalanceAudit.cs`
- `Tools/Documentation/generate_content_database.py`
- `Tools/Wiki/generate_wiki_model.py`
- `wiki/game-versions/0.0.1v/data/entities/facility/building-runtime-*.json` 42개
- `Artifacts/QA/wiki-system-audit-2026-09-05/facility-construction-review.json`

