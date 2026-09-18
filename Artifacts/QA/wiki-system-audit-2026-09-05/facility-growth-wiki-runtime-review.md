# 시설 성장·합성·계보·진화 위키 → 런타임 대조

방향: 공개 `facility-growth` 가이드 → 합성·계보 교체·개체 진화·이전 production 경로

## 확인 범위

- `FacilitySynthesisRuntime`과 생산 시설 retarget transaction
- `FacilityEvolutionRuntime`, 후보 검증, 방 profile, 기록 token·물질 commit
- `FacilityEvolutionRecordRuntime`의 방문·매출·소비·재입고 실패·범죄·방어·침입피해·운영일 기록
- `FacilityInstanceEvolutionRuntime`의 세대별 usage ledger, 후보 고정, 촉매·재료·작업 주문, 활성/휴면 재평가
- 시설 이전의 대상 지정, 해체 작업, 물리 package 운반, 재설치, state module 복원

## 일치한 핵심 계약

- 합성은 실제 월드 시설, 같은 grid/world 권위, recipe/research, 정확한 재료 집합과 결과 footprint를 검사하고 원자 교체한다.
- 계보 교체는 source/lineage, star grade, 연구, 방 점수·metric·tag·fixture, 운영 기록 token, 물질, identity pressure 및 허용 mutation을 검사한다. 결과와 mutation은 material commit 전에 snapshot으로 고정되고 pending 상태로 복원된다.
- 방문·매출·재고 소비·재입고 실패·범죄·방어 발동·침입 피해·운영일 사건이 시설 기록과 instance usage ledger로 들어간다.
- 개체 진화는 세대 종료 시 주 역할·방 시너지·위험 촉매 후보를 고정하고, 재료/촉매와 작업을 거쳐 같은 시설 정의의 node를 추가한다. 방·시설 revision 변화 때 node 활성/휴면을 재평가하고 recalibration 작업도 제공한다.
- 이전은 해체와 재설치 WU를 분리하고 source에 고유 물리 package를 생성한다. package는 destination을 가진 물류 대상이며 도착 전에는 재설치가 진행되지 않는다. state module을 새 개체에 복원한 뒤 room activation을 다시 계산한다.

## WIM-057 — 합성 전 생산 상태를 비우는 규칙과 실제 보존형 retarget이 다름

- 판정: `different`
- 위키 약속: 진행 중 생산 주문·재공품·준비 출력을 모두 비워야 합성을 시작할 수 있고, 비운 뒤 재료 시설을 소비한다.
- 실제 구현: 합성 `Validate`는 파손·중복·recipe·research·동일 grid/world·결과 배치를 검사한다. 이후 `IProductionFacilityRetargetTransaction`을 열어 material 시설의 생산 주문·물리 custody·prepared output 등 참여 권위를 새 결과 시설 handle로 원자 재지정한다. 즉, 현재 계약은 “비어 있어야 함”이 아니라 “진행 권위를 안전하게 보존·이관할 수 있어야 함”이다.
- 보완: 현재 구현이 의도라면 위키 flow를 보존형 retarget으로 정정하고, 이관 불가능한 열린 권위의 typed 실패를 설명한다. 실제 설계가 drain-only라면 합성 시작 전에 destructive-empty gate를 추가하되 진행 상태를 잃지 않게 별도 취소/회수 절차를 제공한다.
- 근거:
  - `wiki/game-versions/0.0.1v/content/guides/facility-growth.md:19-31`
  - `Assets/Scripts/Services/Synthesis/FacilitySynthesisRuntime.cs:165-254,293-513`
  - `Assets/Scripts/Services/Economy/ProductionFacilityRetargetTransaction.cs:203-438`
  - `Assets/Scripts/Services/Economy/ProductionActiveMultiFacilityRetargetAdapter.cs:304-659`

## 판정 메모

계보 6개 개별 조건과 mutation 선택 상세가 공개 도감에 부족한 문제는 반대 방향 문서 보완 원장의 `GAP-024` 및 관련 항목으로 유지한다. 현재 실행 자체는 별도 WIM을 추가할 정도의 누락을 찾지 못했다.

