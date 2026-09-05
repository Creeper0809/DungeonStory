# 구조 내구도·수리·돌파 대조

상태: 부분 대조 완료, 전체 시스템 감사 진행 중. 스크립트·자산·공개 위키 변경 없음.

## 모집단과 도감

runtimeArchetype 보유 자산419개에서 명시적 구조 모듈10개와 기본 생성 대상4개를 찾았다. 모두 루트→도메인 카탈로그 참조와 도감 페이지가 있다. 명시적10개와 벽·문3개의 수치52개가 도감에서 빠져 있다. 복도1개는 생성 조건에 포함되지만 침입자 대상 층이 달라 설명 필요 여부를 보류한다. 생성 조건 확인을14개 시설의 실제 건설·전투 실행 검증으로 세지 않는다.

| 시설 | 정의 | 최대HP | 강도 | HP/WU | 파괴가능 | 판정 |
| --- | --- | ---: | ---: | ---: | --- | --- |
| [내벽](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-7.json) | runtime-default | 300 | 18 | 2 | 예 | 4필드 누락 |
| [내벽 문](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8.json) | runtime-default | 120 | 8 | 2 | 예 | 4필드 누락 |
| [복도](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-0.json) | runtime-default | 300 | 18 | 2 | 예 | 설명 필요 여부 보류 |
| [문](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1.json) | runtime-default | 220 | 14 | 2 | 예 | 4필드 누락 |
| [문 연동 강화 낙하문](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1804.json) | authored | 450 | 24 | 2 | 예 | 4필드 누락 |
| [봉인 생태정원](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-sealed-garden.json) | authored | 1400 | 38 | 3 | 예 | 4필드 누락 |
| [진실 관측소](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-truth-observatory.json) | authored | 1400 | 38 | 3 | 예 | 4필드 누락 |
| [영원 계보전](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-lineage-vault.json) | authored | 2400 | 60 | 3 | 예 | 4필드 누락 |
| [강철 신격상](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-steel-colossus.json) | authored | 2400 | 60 | 3 | 예 | 4필드 누락 |
| [비전 승천탑](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-arcane-spire.json) | authored | 2400 | 60 | 3 | 예 | 4필드 누락 |
| [시간 고정 성소](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-temporal-sanctum.json) | authored | 2400 | 60 | 3 | 예 | 4필드 누락 |
| [주권 성채](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-sovereign-citadel.json) | authored | 1400 | 38 | 3 | 예 | 4필드 누락 |
| [지상 패권문](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-surface-gate.json) | authored | 1400 | 38 | 3 | 예 | 4필드 누락 |
| [대협약 회당](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-accord-hall.json) | authored | 1400 | 38 | 3 | 예 | 4필드 누락 |

랜드마크9개의 요약은 모두 `V20 hand-authored milestone landmark.`다. 이 기록은 구조 수치 누락만 확정한다. 이정표별 고유 기능·완공 효과의 누락 범위는 추가 대조 대상이다.

## 공통 규칙

- cracks: HP비율>0.75 없음; 0.50<비율<=0.75 Hairline; 0.25<비율<=0.50 Cracked; 비율<=0.25 Critical
- repair_candidate: 현재HP < 최대HP - 0.001
- damaged_flag: 파괴되지 않고 HP비율<=0.5
- repair_hp: min(최대HP,현재HP+승인WU×repairHitPointsPerWork)
- repair_resources: ExecuteStructuralRepair/TryApplyRepairWork에서 물리 자재를 소비하지 않는다. 장비·방어시설 수리 비용과 구별한다.
- save: version1 currentHitPoints 저장, 복원 시0..최대HP로 제한. 실제 저장왕복 미실행.
- damage_estimate: A=5×melee-hit 성능값; S=5×melee-power 성능값; E=max(1,(0.75A+0.45S)×캐릭터 전투력 배율×근접피해 배율×max(0.01,구조피해 설정배율))
- damage: max(1,E-max(0,강도)×0.5)×(격노?1.25:1)
- interval: max(0.1,설정 구조공격간격)/공격속도배율×(격노?0.65:1)
- enrage: 돌파 시작 후3초 이상. 접근 이동시간도 포함하며 복원된 경과시간을 잇는다.
- planning_start: 정상경로 조회가 완료되었고 길이0이며 현재칸!=목표칸일 때만 가상 돌파 경로 계산.
- virtual_cost: 구조칸10+현재HP+강도×3+ceil(현재HP/max(1,E))×35; 일반칸10; 알려진위험>=0×clamp01(1-위험감수)×30 추가.
- attack_slots: 구조 점유칸 바깥 상하좌우의 도달 가능·비예약 칸을 한 침입자에 하나씩 예약. 멀티칸 구조물에 일괄4명 제한을 의미하지 않는다.
- opened_path: 정상 경로가 생기면 돌파를 멈추고 탐색으로 복귀.
- destruction: 치명 피해는 구조파괴 손실 명령의 제거 성공이 필요하다. 실패/지연 시 이 호출에서 HP를 먼저0으로 만들지 않는다. 남은 재고·잔해 회수율은 미검토.

## 구현 확인 후보

- 기본 구조의 실행기 잔여WU 계산1HP/WU와 실제 회복2HP/WU 차이
- 방어HUD의 HP/(공격자수×10)×격노0.65 예상시간은 실제 강도·성능·공격간격을 반영하지 않음
- 복도는 기본모듈 생성 대상이지만 침입자 Building 층 조회 대상이 아님
- 랜드마크9개 요약은 영문 작성 메모이며 고유 효과 전체는 다음 시스템 감사에서 대조

## 근거와 검증 한계

KB는 stale(실패469·반환0행)이며 생성물을 재생성하거나 현재 근거로 사용하지 않았다. query·digest와 모든 원본 경로는 [기계 판독 보고서](structural-mechanics-review.json)에 있다. 원본 함수와 자산을 직접 확인한 정적 감사이며 Unity 컴파일·실제 UI·PlayMode·저장왕복을 실행하지 않았다. 도감 수치 누락은 GAP-058, 공통 상태/수리는 GAP-057, 피해/격노는 GAP-059, 돌파 경로는 GAP-060에 기록한다.
