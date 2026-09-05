# DungeonStory 시스템 구현 권위 체크리스트

이 문서는 시스템 구현 상태를 기록하는 단일 체크리스트다. 핸드북은 현재 구현된 구조와 동작을 설명하고, 부분 이행·미구현·검증 보류 항목은 이 문서에서만 관리한다.

기준일은 2026-08-30이다. 이번 판정은 C# 소스와 Unity 작성 자산을 정적으로 확인한 결과이며 Unity 컴파일, 테스트, Play Mode를 새로 실행하지 않았다.

## 상태 판정 규칙

- `[x] 구현 확인`: 현재 코드 또는 작성 자산에 상태 권위와 실행 경로가 존재한다.
- `[ ] 부분 이행`: 기반 런타임은 존재하지만 대상 콘텐츠 전체의 이행이나 검증이 끝나지 않았다.
- `[ ] 미구현`: 설계 권위는 있으나 공용 작성 계약 또는 런타임 권위를 현재 소스에서 확인하지 못했다.
- `실행 검증 보류`: verifier나 보고서의 존재와 현재 revision 통과를 구분한다.

## 권위 우선순위

1. 현재 C# 소스와 Unity 작성 자산이 구현 사실을 결정한다.
2. [전체 게임 밸런스 기준서](game-design/whole-game-balance-baseline.md)가 승인 수치와 변경 기록을 소유한다.
3. [V27 물리 질량·운반 재조정 계획](game-design/v27-physical-mass-and-hauling-recalibration-plan.md)이 해당 작업 범위의 진행 상태를 소유한다. 이 파일은 읽기 전용으로 취급한다.
4. [현행 게임 시스템 설계 핸드북](handbook/README.md)은 구현된 시스템을 사람이 읽을 수 있는 형태로 설명한다.
5. 목표 설계 문서는 아직 구현되지 않은 계약과 콘텐츠 이행 범위를 정의한다.
6. 생성 보고서, 구현 보고서와 verifier는 특정 revision의 검증 증거다.

## 구현 확인

- [x] **런타임 조립과 공통 기반**
  - 구현 권위: [DungeonRuntimeLifetimeScope.cs](../Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs), [Foundation](../Assets/Scripts/Services/Foundation/)
  - 설명 권위: [시스템 구성과 상태 권위](handbook/02-system-map-and-authority.md)
  - 확인 범위: 도메인 registration, aggregate store, 시계, 난수 stream, 이벤트 버스, 영속 ID, 작업 예산, 경로 탐색 broker

- [x] **통합 콘텐츠 카탈로그와 도메인별 읽기 port**
  - 구현 권위: [GameContentCatalogSO.cs](../Assets/Scripts/Content/GameContentCatalogSO.cs), `ResourceGameContentCatalog`
  - 설명 권위: [시스템 구성과 상태 권위](handbook/02-system-map-and-authority.md)

- [x] **자동 동기화 기술·콘텐츠 지식베이스**
  - 구현 권위: [generate_content_database.py](../Tools/Documentation/generate_content_database.py), [generate_knowledge_base.py](../Tools/Documentation/generate_knowledge_base.py), [knowledge_manifest.py](../Tools/Documentation/knowledge_manifest.py), [verify_knowledge_base.py](../Tools/Documentation/verify_knowledge_base.py)
  - 생성 권위: [기술·콘텐츠 지식베이스](knowledge-base/README.md), [콘텐츠 데이터베이스](content-db/README.md)
  - 확인 범위: 작성 콘텐츠의 전체 직렬화 필드·정방향 관계·역참조·존재 이유, 유형/안정 ID별 C# 소비처, 시스템별 코드 파일·역할·정적 최적화 근거, 상태 권위·저장·UI/AI 관찰 경로, 원본/생성물 digest와 stale 검증
  - 실행 경계: Unity와 컴파일러를 사용하지 않는 정적 재생성·검증이며 실제 플레이 도달과 성능 수치를 보증하지 않음

- [x] **시설 정의와 ability 조립**
  - 구현 권위: [BuildingSO.cs](../Assets/Scripts/Services/Buildings/SO/BuildingSO.cs), `BuildingAbilityCollection`
  - 작성 자산: 건물 419개, deprecated 호환 자산 제외 398개
  - 설명 권위: [공간 구축·시설·환경 시뮬레이션](handbook/03-world-building-facilities-environment.md)

- [x] **아이템 정의와 타입화된 feature**
  - 구현 권위: [ItemDefinitionSO.cs](../Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs), [ItemDefinitionCatalogSO.cs](../Assets/Scripts/Models/Economy/Content/ItemDefinitionCatalogSO.cs)
  - 작성 자산: 구체 `*ItemDefinitionSO` 1,074개
  - 설명 권위: [물질 경제·생산·물류 체계](handbook/04-items-production-logistics-economy.md)

- [x] **물리 아이템·예약·custody·목적지 수용량**
  - 구현 권위: [WorldItemRepository.cs](../Assets/Scripts/Services/Items/WorldItemRepository.cs), [ItemTransferService.cs](../Assets/Scripts/Services/Items/ItemTransferService.cs)
  - 설명 권위: [물질 경제·생산·물류 체계](handbook/04-items-production-logistics-economy.md)

- [x] **생산 bill·WIP·prepared output·출력 확정**
  - 구현 권위: [ProductionBillRuntime.cs](../Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs), `ProductionAggregateStateSession`, 생산 output capability·materializer 계열
  - 설명 권위: [물질 경제·생산·물류 체계](handbook/04-items-production-logistics-economy.md)

- [x] **작업 registry와 AI 노동 배분**
  - 구현 권위: [CharacterAiScheduler.cs](../Assets/Scripts/Services/Character/AI/CharacterAiScheduler.cs), `WorkTypeId` 기반 provider·handler·policy
  - 설명 권위: [인물·노동·사회·생명 체계](handbook/05-characters-ai-society-health.md)

- [x] **방·환경·전기·유체·컨베이어**
  - 구현 권위: [Rooms](../Assets/Scripts/Models/Rooms/Core/), [Environment](../Assets/Scripts/Models/Environment/Core/), [Industrial](../Assets/Scripts/Services/Infrastructure/Industrial/)
  - 설명 권위: [공간 구축·시설·환경 시뮬레이션](handbook/03-world-building-facilities-environment.md)

- [x] **농업·축산·야생동물 생태**
  - 구현 권위: [CropPlotRuntime.cs](../Assets/Scripts/Services/Economy/CropPlotRuntime.cs), [Husbandry](../Assets/Scripts/Services/Economy/Husbandry/), [Wildlife](../Assets/Scripts/Services/Wildlife/)
  - 작성 자산: 작물 12개, 야생동물 종 18개
  - 설명 권위: [공간 구축·시설·환경 시뮬레이션](handbook/03-world-building-facilities-environment.md)

- [x] **캐릭터 생존·소비·사회·직업·숙련**
  - 구현 권위: [Characters](../Assets/Scripts/Models/Characters/), [Character services](../Assets/Scripts/Services/Character/), [CharacterConsumablesRuntime.cs](../Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs)
  - 설명 권위: [인물·노동·사회·생명 체계](handbook/05-characters-ai-society-health.md)

- [x] **시작 구성과 던전 주인 권위**
  - 구현 권위: [StartPartyPreparationService.cs](../Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs), [StartPartyPreparationSnapshot.cs](../Assets/Scripts/Services/Character/Core/StartPartyPreparationSnapshot.cs), [OffenseExpeditionService.cs](../Assets/Scripts/Services/Offense/OffenseExpeditionService.cs)
  - 확인 범위: 주인 한 명과 같은 종족 직원 두 명의 시작 구성, 후보 준비와 확정, 주인 고정 기술, 주인의 원정 참가 차단
  - 설명 권위: [게임 정체성과 운영 순환](handbook/01-game-vision-and-player-loop.md)

- [x] **신체 건강·의료·수술·질병·번식**
  - 구현 권위: [CharacterBodyHealthRuntime.cs](../Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs), [CharacterMedicalRuntime.cs](../Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs), [Medical](../Assets/Scripts/Services/Medical/)
  - 작성 자산: 질병 16개
  - 설명 권위: [인물·노동·사회·생명 체계](handbook/05-characters-ai-society-health.md)

- [x] **연구 프로젝트·선행 topology·청사진·시설 요구**
  - 구현 권위: [ResearchProjectSO.cs](../Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs), [BlueprintResearchRuntime.cs](../Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs)
  - 작성 자산: 연구 프로젝트 180개
  - 설명 권위: [연구 체계와 전략적 진행](handbook/06-research-progression-and-strategy.md)

- [x] **시설 합성·작성 계보 교체·개체 진화 실행 경로**
  - 구현 권위: [Synthesis](../Assets/Scripts/Services/Synthesis/), [FacilityEvolution](../Assets/Scripts/Services/FacilityEvolution/), [Facility evolution models](../Assets/Scripts/Models/Evolution/Facility/)
  - 작성 자산: 실물 합성 조합식 9개, 작성 계보 교체 조합식 6개
  - 확인 범위: 실물 시설 희생과 결과 배치, 기록·방 조건을 읽는 작성 계보 교체, 같은 시설에 진화 노드를 남기는 개체 진화, 활성·휴면, 재조율, 포장 운반과 재설치
  - 설명 권위: [공간 구축·시설·환경 시뮬레이션](handbook/03-world-building-facilities-environment.md)

- [x] **침입 위협·방어 정책·침입자·교전**
  - 구현 권위: [Invasion models](../Assets/Scripts/Models/Invasion/Core/), [Invasion services](../Assets/Scripts/Services/Invasion/)
  - 설명 권위: [무력 충돌·원정·세력 관계](handbook/07-combat-invasions-expeditions-factions.md)

- [x] **원정·전투·보상·귀환/도착**
  - 구현 권위: [Offense models](../Assets/Scripts/Models/Offense/Core/), [Offense services](../Assets/Scripts/Services/Offense/)
  - 작성 자산: 공격 조우 36개
  - 설명 권위: [무력 충돌·원정·세력 관계](handbook/07-combat-invasions-expeditions-factions.md)

- [x] **세력 관계·경로·호의·배상 outbox**
  - 구현 권위: [Faction models](../Assets/Scripts/Models/Factions/Core/), [Faction services](../Assets/Scripts/Services/Factions/)
  - 작성 자산: 던전 세력 12개
  - 설명 권위: [무력 충돌·원정·세력 관계](handbook/07-combat-invasions-expeditions-factions.md)

- [x] **사건 요구 조건·즉시 effect·경보 action**
  - 구현 권위: [V20CampaignRuntime.cs](../Assets/Scripts/Services/Run/V20CampaignRuntime.cs), [V20ContentResolutionService.cs](../Assets/Scripts/Services/Run/V20ContentResolutionService.cs)
  - 설명 권위: [사건 콘텐츠의 작성과 시스템 통합](handbook/08-content-events-and-authoring.md)

- [x] **메타 진행과 런 생명주기**
  - 구현 권위: [MetaProgressionRuntime.cs](../Assets/Scripts/Services/Infrastructure/Core/MetaProgressionRuntime.cs)
  - 설명 권위: [게임 정체성과 운영 순환](handbook/01-game-vision-and-player-loop.md)

- [x] **section 기반 트랜잭션 저장·복원**
  - 구현 권위: [Foundation save](../Assets/Scripts/Services/Foundation/Save/), [Infrastructure save](../Assets/Scripts/Services/Infrastructure/Core/Save/), 각 도메인 save section
  - 설명 권위: [영속성·결정론·검증 체계](handbook/09-save-restore-determinism-and-validation.md)

- [x] **canonical gram 질의와 질량 입고 기반**
  - 구현 권위: [PhysicalMassContracts.cs](../Assets/Scripts/Models/Items/Core/PhysicalMassContracts.cs), [PhysicalItemMassQuery.cs](../Assets/Scripts/Services/Items/PhysicalItemMassQuery.cs), [CharacterCarryInventory.cs](../Assets/Scripts/Services/Items/CharacterCarryInventory.cs), [WarehouseMassAdmissionService.cs](../Assets/Scripts/Services/Items/WarehouseMassAdmissionService.cs), [FacilityBufferMassAdmissionService.cs](../Assets/Scripts/Services/Items/FacilityBufferMassAdmissionService.cs)
  - 확인 범위: 정의와 인스턴스 projector를 합치는 정수 gram 질의, 부분 stack 픽업, 정상·최대 운반 한도와 과적, 창고·시설 버퍼의 revision 결속 gram 예약과 commit, 장비·의복 질량 투영, 동일 질의 기반 kg 표시와 물리 아이템 저장 검증
  - 범위 제한: 이 항목의 완료 범위는 공통 구조다. 전체 아이템 kg, 모든 동적 상태, 가상 재고, 원정 burden과 결합 밸런스는 아래 V27 부분 이행 항목에서 판정한다.
  - 설명 권위: [물질 경제, 생산과 물류](handbook/04-items-production-logistics-economy.md), [아이템, 재고, 예약과 운반](architecture/systems/05-items-inventory-and-hauling.md)

## 부분 이행

- [ ] **V27 물리 질량·운반 전수 재조정** `(부분 이행)`
  - 현재 구현 권위: canonical mass query와 projector, 캐릭터 부분 운반과 과적, 창고·시설 버퍼 mass admission, 생산 WIP·prepared output 질량, custody, destination drain, kg 표시, current-format 물리 아이템 저장과 다수 outbox
  - 진행 권위: [V27 물리 질량·운반 재조정 계획](game-design/v27-physical-mass-and-hauling-recalibration-plan.md)
  - 승인 권위: [전체 게임 밸런스 기준서](game-design/whole-game-balance-baseline.md)
  - 미완료: canonical item 전체와 모든 작성 위치의 최종 kg, 장비·의복·사체 등 stateful mass 전수 정합, count·slot·buffer·virtual inventory 차원 감사
  - 미완료: 생산 BOM과 source·byproduct·sink·loss 귀속, 영양·사료·약효·성능·WU·EWU·stack·저장·운반 횟수·가격의 결합 재조정
  - 미완료: 원정 보급과 전리품의 exact burden, 살아 있는 대상 운송 경계, 현재 형식의 과적·WIP·입고 저장 왕복, 장기 처리량과 다중 seed 실전 증거
  - 범위 밖: 과거 버전 세이브의 kg 자동 마이그레이션, 차량·팀 리프트 같은 신규 운송 콘텐츠
  - 완료 조건: 계획서의 structural, content migration, live evidence, final balance와 extension closure 항목을 전수 증거로 종결

- [ ] **연구 180개·비폐기 건물 398개의 전략 포트폴리오 매핑** `(부분 이행)`
  - 현재 구현 권위: `ResearchProjectSO` 선행·해금·시설 요구, `BuildingSO` ability·분류
  - 목표 설계 권위: [전략 빌드·연구·시설 포트폴리오 설계](game-design/strategic-build-and-progression-design.md)
  - 완료 조건: 문제 영역, 대체 경로, 채택 비용, 매몰비용, 약점, 환경·종족별 우위와 런별 미사용 경로를 전수 정의

- [ ] **아이템·조합식의 물리 BOM·내재 작업량·경제 비차익 전수 증명** `(부분 이행)`
  - 현재 구현 권위: 1,074개 아이템 정의, 생산 recipe, 물질수지와 출력 capability
  - 승인 권위: [전체 게임 밸런스 기준서](game-design/whole-game-balance-baseline.md)
  - 완료 조건: source digest가 있는 전수 감사와 악용 가능성 기록

- [ ] **사건 콘텐츠의 operation·receipt·후속 반응 이행** `(부분 이행)`
  - 현재 구현 권위: 요구 조건 평가, item cost preflight, 즉시 effect executor, alert action
  - 목표 설계 권위: [시스템 콘텐츠 통합 설계](game-design/systemic-content-integration-design.md)
  - 콘텐츠 범위 권위: [시스템 콘텐츠 상세 카탈로그](game-design/systemic-content-event-catalog.md)
  - 완료 조건: 발생, 문맥 snapshot, 선택 command, 지속 operation, receipt, 흔적, 역반응과 중간 저장을 정의별로 연결

- [ ] **종족·문화·질병·생애 사건의 운영 차별성 검증** `(부분 이행)`
  - 현재 구현 권위: 문화별 시설 효용·사고 가중치, 건강·질병·가족·서비스 상태
  - 완료 조건: 장기 런에서 재고 수요, 작업 배치, 생산 계획, 외교와 후속 사건의 차이를 기록

- [ ] **침입 doctrine·원정 조우·세력별 공급망 차별성 검증** `(부분 이행)`
  - 현재 구현 권위: 방어 정책, 교전, 36개 조우, 12개 세력과 계약/outbox
  - 완료 조건: 준비·전술·후유증·자원 기회비용과 세력별 공급망 차이를 대표 seed에서 확인

- [ ] **전수 콘텐츠 감사 자동화** `(부분 이행)`
  - 현재 구현 권위: 도메인별 definition validator와 Editor debug scenario
  - 목표 설계 권위: 전략 포트폴리오 설계와 시스템 콘텐츠 통합 설계의 감사 스키마
  - 완료 조건: ID, 링크, registry, 물리 source/sink/transfer, 저장 version, 전략 역할, 후속 반응 누락을 하나의 manifest에서 검사

- [ ] **장기 런 전략 다양성 검증** `(검증 보류)`
  - 검증 대상: 미사용 연구·시설 비율, 전략 전환 비용, 실제 병목, 침입·원정의 기지 영향, 사건의 지속 결과, 저장 전후 digest
  - 완료 조건: 대표 seed·환경·종족 조합별 재현 가능한 결과 보고서

- [ ] **운영 원인 사슬과 계획 대비 실적 질의** `(부분 이행)`
  - 현재 구현 권위: 도메인별 실패 원인, 재고·예약·작업·네트워크 query와 일부 진단 view
  - 비교 설계 권위: [상용·인디 시스템 게임 설계 비교](game-design/reference-game-design-synthesis.md)
  - 완료 조건: 주요 건설·생산·물류·환경·전투 흐름에서 값, 최초 원인, 전파 경로, 추세, 대응 여유와 개입 전후 실적을 공통 형식으로 조회

- [ ] **건설 현장 원장과 기반망 장애 구획** `(부분 이행)`
  - 현재 구현 권위: 건설 단계·작업량·재료, 전력·유체·컨베이어 네트워크와 도메인별 고장·파괴 처리
  - 비교 설계 권위: [상용·인디 시스템 게임 설계 비교](game-design/reference-game-design-synthesis.md)
  - 완료 조건: 현장별 조달·운송·보관·시공·회수 원장과 네트워크별 차단·우회·완충·격리·단계 재가동 계약을 연결

- [ ] **전투·원정 사후 결산** `(부분 이행)`
  - 현재 구현 권위: 교전 store, 원정 결과 이력, 귀환·도착 상태, 장비·탄약·포로·부상 상태
  - 비교 설계 권위: [상용·인디 시스템 게임 설계 비교](game-design/reference-game-design-synthesis.md)
  - 완료 조건: 준비 인원·물자·기지 노출과 실제 소비·손실·회수·치료·생산 지연·세력 후과를 같은 원장으로 비교

## 미구현

- [ ] **공용 전략 포트폴리오 작성 계약** `(미구현)`
  - 현재 구현 권위: 없음. `ResearchProjectSO`에는 선행·해금·시설 요구가 있으나 포트폴리오 역할, 대체 경로, 채택 비용과 전환 비용을 소유하는 공용 계약은 확인되지 않았다.
  - 설계 권위: [전략 빌드·연구·시설 포트폴리오 설계](game-design/strategic-build-and-progression-design.md)

- [ ] **공용 사건 operation·receipt·reaction 런타임** `(미구현)`
  - 현재 구현 권위: 없음. 개별 도메인의 operation·receipt·outbox는 존재하지만 캠페인 사건 전체가 공유하는 수명주기 권위는 확인되지 않았다.
  - 설계 권위: [시스템 콘텐츠 통합 설계](game-design/systemic-content-integration-design.md)

- [ ] **전수 콘텐츠 통합 manifest와 단일 감사기** `(미구현)`
  - 현재 구현 권위: 없음. 도메인별 validator는 존재하지만 전략 역할·물리 이행·흔적·역반응을 한 행에서 판정하는 공용 manifest는 확인되지 않았다.
  - 설계 권위: [시스템 콘텐츠 통합 설계](game-design/systemic-content-integration-design.md), [전략 빌드·연구·시설 포트폴리오 설계](game-design/strategic-build-and-progression-design.md)

- [ ] **압력 기반 사건 편성 권위** `(미구현)`
  - 현재 구현 권위: 없음. 사건 요구 조건과 경보 aggregate는 있으나 현재 압력·최근 사건·미해결 관계·참여자 점유·준비 가능성·범주별 휴지기를 함께 소유하는 공용 편성 계층은 확인되지 않았다.
  - 설계 권위: [상용·인디 시스템 게임 설계 비교](game-design/reference-game-design-synthesis.md), [시스템 콘텐츠 통합 설계](game-design/systemic-content-integration-design.md)

## 콘텐츠 승인 체크리스트

### 시설·생산 콘텐츠

- [ ] 해결하는 문제와 경쟁 시설을 명시했다.
- [ ] 입력·출력·WIP·보관·운반 경로를 연결했다.
- [ ] 방·전기·유체·인력 capability를 정의했다.
- [ ] 건설·운영·고장·해체 비용과 회수 규칙을 기록했다.
- [ ] 파괴·교체·용량 포화·중간 저장의 상태 보존을 검증했다.
- [ ] 종족·환경·런 전략별 채택 이유와 미사용 이유를 기록했다.

### 전략 진행 콘텐츠

- [ ] 주요 문제마다 실행 가능한 대체 경로가 두 개 이상 존재한다.
- [ ] 경로별 물리 BOM·작업량·시간·공간·위험·대외 의존성을 기록했다.
- [ ] 전환 시 회수 비용과 매몰비용을 구분했다.
- [ ] 종족·환경별 우위 변화를 기록했다.
- [ ] 런별 미사용 연구·시설 비율을 측정했다.
- [ ] 대표 seed에서 서로 다른 포트폴리오가 유지된다.
- [ ] 하나의 계열이 모든 문제를 지배하지 않는지 감사했다.

### 사건 콘텐츠

- [ ] 안정 ID, 버전, 발생 producer와 중복 정책을 정의했다.
- [ ] 캐릭터·시설·방·세력·지역의 영속 ID로 문맥 snapshot을 만든다.
- [ ] 선택 요구 물질·capability·인력·정책과 재검증 규칙을 정의했다.
- [ ] 지속 행위를 operation으로 실행하고 결과 receipt를 남긴다.
- [ ] 취소·실패·만료·중간 저장 경로를 정의했다.
- [ ] 후속 사건·세력·가족·경제 반응을 연결했다.
- [ ] 항상 우월한 선택지와 이름만 다른 선택 구조를 감사했다.

## 검증 증거 체크리스트

- [ ] 실행 날짜와 source digest를 기록했다.
- [ ] seed, 입력, 작성 자산 revision을 기록했다.
- [ ] 성공 조건과 실패 조건을 함께 명시했다.
- [ ] 취소, 부분 실행, 포화, 파괴와 중간 저장을 포함했다.
- [ ] 저장·복원 전후 물질·상태 digest를 비교했다.
- [ ] 밸런스 변경이면 기준서의 `콘텐츠 추가·수정 필수 기록`을 작성했다.
