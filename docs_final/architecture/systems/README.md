# 시스템 구현과 성능 설계

이 문서 묶음은 현재 소스 코드가 각 게임 시스템을 어떻게 실행하는지 설명한다. 디자인 패턴 자체는 상위 [코드 아키텍처 안내서](../code-architecture-guide.md)에서 다루고, 여기서는 상태가 어디에 있고 한 틱에 무슨 일이 일어나며 어떤 비용을 줄이기 위해 어떤 장치를 두었는지에 집중한다. 시설 합성과 사용 기반 진화는 건설 이후의 운영 이력을 다시 콘텐츠 선택으로 돌려보내는 구조이므로 별도 시스템으로 분리했다. 시설과 장비가 공유하는 이력 압축, 진화 노드와 결정론 경계는 [사용 이력 기반 진화 아키텍처](../07-history-driven-evolution-architecture.md)에서 함께 설명한다.

19개 시스템이 실제 런타임에서 만나는 위치는 [전체 런타임 구조](../08-whole-runtime-topology.md), 상태별 단일 쓰기 경계는 [상태 권위 원장](../09-state-authority-ledger.md), 물리 효과와 장기 작업의 인계는 [도메인 간 거래와 실패 경계](../10-cross-domain-transactions.md), 공통 시계와 캐시 수명은 [런타임 스케줄링과 읽기 투영](../11-runtime-scheduling-and-projections.md)에서 이어진다. 이 네 장은 시스템 문서의 내용을 반복하지 않고 시스템 사이의 공통 계약을 정리한다.

성능 관련 표현은 다음 기준을 따른다.

| 구분 | 의미 |
|---|---|
| 적용된 최적화 | 캐시, 풀, 인덱스, 갱신 주기, 작업 예산처럼 코드에 실제로 존재하는 장치 |
| 구조적 비용 통제 | 원자적 배치, 사전 검증, 버전 검사처럼 속도보다 실패 비용과 재작업을 줄이는 장치 |
| 측정이 필요한 속성 | 프레임 시간, GC 할당량, 최대 인구처럼 정적 코드 검토로 확정할 수 없는 값 |
| 개선 후보 | 현재 구현에는 없고 병목이 확인될 때 검토할 선택지 |

## 문서 구성

1. [캐릭터 AI와 행동 실행](01-character-ai-and-behavior.md)
2. [그리드, 공간 질의와 경로 탐색](02-grid-spatial-and-pathfinding.md)
3. [건물 배치, 건설과 방 판정](03-buildings-construction-and-rooms.md)
4. [작업 주문, 노동 배정과 실행](04-work-orders-and-labor.md)
5. [아이템, 재고, 예약과 운반](05-items-inventory-and-hauling.md): canonical gram, 부분 운반과 목적지 질량 입고를 포함한다
6. [생산, 제작식과 출력 인계](06-production-and-output-routing.md): WIP 물질수지와 출력 buffer 질량 경계를 포함한다
7. [전력, 유체, 컨베이어와 자동화](07-industrial-networks-and-automation.md): 컨베이어 payload와 시설 질량 capacity의 분리를 포함한다
8. [캐릭터 생애, 욕구와 사회 상태](08-character-life-needs-and-society.md)
9. [의료, 질병과 신체 상태](09-medical-disease-and-body-health.md)
10. [환경장, 생존과 공간 위생](10-environment-survival-and-sanitation.md)
11. [야생동물, 농업과 축산](11-wildlife-agriculture-and-husbandry.md)
12. [전투, 장비와 피해 처리](12-combat-equipment-and-damage.md): 장비별 전투 이력과 진화 노드를 포함한다
13. [방어, 침입과 위협 대응](13-defense-invasion-and-threat.md)
14. [원정, 월드맵과 전략 전투](14-offense-expeditions-and-world-map.md)
15. [연구, 해금과 런 진행](15-research-progression-and-meta.md)
16. [세력, 사건, 모집과 포로](16-factions-events-recruitment-and-captivity.md)
17. [세션 조립, 저장과 결정론](17-session-save-and-determinism.md)
18. [UI, 알림과 성능 진단](18-presentation-notifications-and-diagnostics.md)
19. [시설 합성과 사용 기반 진화](19-facility-synthesis-and-use-based-evolution.md): 합성, 작성 조합식 교체, 개체 진화, 재조율과 이전을 구분한다

각 문서의 성능 평가는 코드 형태에 대한 평가다. 프로파일러 캡처나 실제 플레이 측정 없이 "빠르다", "GC가 없다", "대규모 인구를 감당한다"고 단정하지 않는다.

## 횡단 경로

| 변경 또는 조사 | 먼저 볼 문서 | 이어서 볼 시스템 문서 |
|---|---|---|
| 새 상태 권위와 저장 경계 | [상태 권위 원장](../09-state-authority-ledger.md) | 상태가 속한 시스템 장과 [저장](17-session-save-and-determinism.md) |
| 여러 도메인의 물리 인계 | [도메인 간 거래와 실패 경계](../10-cross-domain-transactions.md) | [아이템](05-items-inventory-and-hauling.md), [생산](06-production-and-output-routing.md), 대상 도메인 장 |
| 아이템 kg, 운반량 또는 저장 capacity | [상태 권위 원장](../09-state-authority-ledger.md)과 [도메인 간 거래](../10-cross-domain-transactions.md) | [아이템](05-items-inventory-and-hauling.md), [생산](06-production-and-output-routing.md), [산업망](07-industrial-networks-and-automation.md), [장비](12-combat-equipment-and-damage.md), [원정](14-offense-expeditions-and-world-map.md), [저장](17-session-save-and-determinism.md) |
| 새 tick, cadence 또는 cache | [런타임 스케줄링과 읽기 투영](../11-runtime-scheduling-and-projections.md) | 해당 시스템 장의 실행 주기와 비용 통제 |
| 등록 모듈과 Unity 수명주기 | [전체 런타임 구조](../08-whole-runtime-topology.md) | [세션과 저장](17-session-save-and-determinism.md), [Presentation](18-presentation-notifications-and-diagnostics.md) |
| 시설 성장 경로 | [전체 런타임 구조](../08-whole-runtime-topology.md)와 [이력 기반 진화](../07-history-driven-evolution-architecture.md) | [시설 합성과 사용 기반 진화](19-facility-synthesis-and-use-based-evolution.md) |
