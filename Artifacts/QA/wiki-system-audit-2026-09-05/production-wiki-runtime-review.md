# 생산·품질·공급 위키 → 런타임 대조

방향: 공개 `production`, `production-quality-and-supply` 가이드 → production 코드·저장·UI

## 확인 범위

- `ProductionBillRuntime`, `ProductionPreparedOutputExecutionAdapter`, prepared-output routing/save
- 일반 생산·의복·전투 장비의 품질 반복 주문과 작업자 정책
- `DeterministicCraftQualityResolver`, 숙련도 연속 품질 점수, 반복 연습 XP
- 자동화 mode·작업 배율·품질 상한 작성 권위

## 일치한 핵심 계약

- 주문은 작업자 eligibility·예약, 작업자별 기여량, 물리 WIP 입력 영수증을 실제 상태로 가진다.
- 품질 난수는 `runSeed/pipelineId/definitionId/attemptIndex`로 고정되고 세 개의 `-10..10` 값을 사용한다. 품질 공식과 등급 경계, 숙련 등급 점수 `25/40/58/78/95`도 위키와 일치한다.
- prepared output은 결과 확정, 출력 공간 예약 대기, 물리 batch commit, publication acknowledgement, 완료 단계를 분리한다. routing과 생산 주문은 별도 save section 및 restore join을 가진다.
- 반복 작업 XP 배율은 주문 attempt 기준 `1~3회 1.00`, `4~10회 0.50`, `11회 이상 0.15`로 적용된다.
- `assistedWorkMultiplier=1.35`, manual/assisted/automatic mode, 자동 품질 상한 작성 필드는 존재한다. 자동 품질 상한의 실제 미적용 문제는 기존 `WIM-006`으로만 집계한다.

## WIM-056 — 예상 시도 20회 초과 자원 경고가 없음

- 판정: `partial`
- 위키 약속: 목표 품질의 예상 시도가 20회를 넘으면 강한 자원 경고를 표시한다.
- 실제 구현: 일반 시설·의복·장비 주문은 목표 품질과 최대 시도 횟수 및 `TargetCurrentlyUnreachable` gate를 가진다. 그러나 production gameplay/UI 경로에는 성공 확률에서 예상 시도 횟수를 계산하고 `>20`을 경고하는 snapshot·상태·표시가 없다. 현재 UI의 안전 반복 기본값은 10회다.
- 보완: 현재 작업자·시설·도구·재료 조건의 고정 난수 분포로 목표 도달 확률과 예상 입력/WU를 계산하고, 20회 초과를 typed warning으로 주문 미리보기와 운영 UI에 연결한다. 확률 0 gate 및 실제 최대 시도 제한과 별도 의미로 유지한다.
- 근거:
  - `wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:37-41`
  - `Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:198-230,494-497,3003`
  - `Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs:512-552,1771-1807,1915-1920`
  - `Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs:256,351,530-535,614`
  - `Assets/Scripts/Views/Buildings/UI/EquipmentCraftingPanelPresenter.cs:404-418`

## 판정 메모

생산망 깊이·소비처 수·EWU 회수율·출력 포화 5%는 플레이어에게 약속한 개별 실행 기능이 아니라 밸런스 감사 기준이다. 해당 기준의 자동 감사 증거는 최종 freshness 단계에서 V27 산출물과 다시 연결하되, 이번 정적 runtime 대조에서는 별도 WIM으로 중복 집계하지 않았다.

