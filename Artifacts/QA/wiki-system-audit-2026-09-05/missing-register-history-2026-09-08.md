# GAP 재검토 제외·병합·보류 이력

최종 갱신 2026-09-14. 현재 제외 이력 21건. 이전 판정과 원문은 JSON의 previous_register_entry에 보존한다.

## GAP-007 반복 작업 XP의 구간과 집계 기준

- 판정: withdraw
- 근거: 현재 공개 생산 안내 41줄에 세 반복 XP 구간이 이미 있다. 변경된 WorkTaskExecutor도 동일한 구간을 사용하므로 철회 이력을 유지한다.
- 처리: 누락 수에서 계속 제외한다. 주문 단위 집계는 별도 좁은 검토만 가능.
- 원본 근거: [WorkTaskExecutor.cs 4129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:4129>): 반복 횟수별 1/.5/.15 배율 / [WorkTaskExecutor.cs 579행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:579>): 주문 QualityAttemptIndex를 배율에 전달
- 위키 근거: [production-quality-and-supply.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:41>): 이미 세 구간 설명
- 이번 재검토: 현재 공개 생산 안내 41줄에 세 반복 XP 구간이 이미 있다. 변경된 WorkTaskExecutor도 동일한 구간을 사용하므로 철회 이력을 유지한다.

## GAP-014 임신 운반자 사망과 응급 적출 기한

- 판정: hold
- 근거: 도메인에는80%/1일 적출 규칙이 있지만 현행 비 Editor 검색에서 래퍼·상태 표시는 확인했어도 실제 적출 명령 caller는 발견하지 못했다. 실행 진입 근거 미충족 이력을 유지한다.
- 처리: 라이브 적출 실행경로를 찾기 전 위키 누락 확정에서 제외하고 구현 연결 문제로 구분한다.
- 원본 근거: [ReproductionDomain.cs 327행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:327>): 사망 및 적출 도메인 / [PopulationSocialRuntime.cs 221행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationSocialRuntime.cs:221>): 래퍼만 존재 / [CharacterSummaryPopulationPresenter.cs 505행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/CharacterSummaryPopulationPresenter.cs:505>): 대기 상태 표시만
- 위키 근거: [family-education-and-legacy.md 28행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:28>): 죽음 일반 안내
- 검증 한계: 실제 UI/AI/도메인 적출 caller 미발견
- 이번 재검토: 도메인에는80%/1일 적출 규칙이 있지만 현행 비 Editor 검색에서 래퍼·상태 표시는 확인했어도 실제 적출 명령 caller는 발견하지 못했다. 실행 진입 근거 미충족 이력을 유지한다.

## GAP-042 아이템 198개의 기준 가격이 현재 작성 원본과 불일치

- 판정: withdraw
- 근거: 2026-09-14 메인이 과거 GAP042의198개 품목을 현재 원본 itemId·unitPrice와 공개 도감 기준가격으로 직접 재비교했다. 비교198·일치198·차이0·해석실패0이므로 기존 철회 상태를 유지한다. 이전198개 목록은 탐색용이며 원장 수치를 증거로 재승인하지 않았다.
- 처리: 기존198개가격불일치GAP을해소/철회처리한다. 새전체1075필드감사는별도목록으로만수행한다.
- 원본 근거: [V3I91_비전_색인철.asset 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I91_비전_색인철.asset:32>): 현재unitPrice49
- 위키 근거: [record-arcane-index.json 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/record-arcane-index.json:13>): 현재기준가격49
- 검증 한계: 과거198개 가격 주장 범위의 현재 재대조이며 전체1075개 가격 신규 감사는 아니다.
- 이번 재검토: 2026-09-14 메인이 과거 GAP042의198개 품목을 현재 원본 itemId·unitPrice와 공개 도감 기준가격으로 직접 재비교했다. 비교198·일치198·차이0·해석실패0이므로 기존 철회 상태를 유지한다. 이전198개 목록은 탐색용이며 원장 수치를 증거로 재승인하지 않았다.

## GAP-089 기후 도감 5개의 온도 진폭을 연교차로 표기

- 판정: withdraw
- 근거: 현재 가이드13~23와5개 기후 도감은 진폭과2배 최고-최저차를 구분하며 실제 사인식과 일치한다. 이미 수정된 이력이므로 철회 유지한다.
- 처리: 현재 누락목록에서 철회하고 기완료 이력으로만 남긴다.
- 원본 근거: [ClimateDomain.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:187>): 진폭 사인 소비 / [ClimateZone_temperate-cave.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_temperate-cave.asset:18>): 진폭14 / [ClimateZone_mycelial-depths.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_mycelial-depths.asset:18>): 진폭5 / [ClimateZone_mana-stormlands.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_mana-stormlands.asset:18>): 진폭16 / [ClimateZone_frost-rift.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_frost-rift.asset:18>): 진폭12 / [ClimateZone_ember-wastes.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_ember-wastes.asset:18>): 진폭8
- 위키 근거: [weather-seasons-and-environment.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:13>): 정확한 진폭 정의/2배 표 / [climate-temperate-cave.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-temperate-cave.json:15>): 14/28 정정 / [climate-mycelial-depths.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-mycelial-depths.json:15>): 5/10 정정 / [climate-mana-stormlands.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-mana-stormlands.json:15>): 16/32 정정 / [climate-frost-rift.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-frost-rift.json:15>): 12/24 정정 / [climate-ember-wastes.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-ember-wastes.json:15>): 8/16 정정
- 관련 GAP: GAP-087
- 이번 재검토: 현재 가이드13~23와5개 기후 도감은 진폭과2배 최고-최저차를 구분하며 실제 사인식과 일치한다. 이미 수정된 이력이므로 철회 유지한다.

## GAP-109 시설 건설 재료 수량 차이 미확정 이력

- 판정: hold
- 근거: 기존 검토에서는 건설WU356건이 모두 일치했고 직접 비교한 D03재료도 일치했다. 잔여 BOM42건 주장은 전수 대조가 끝나지 않아 확정 차이가0이다. 현재도 이력 hold로 두며 공개 작성 대상으로 승격하지 않는다.
- 처리: narrow승인을취소하고hold로돌린다.낡은144/103WU불일치집계는제거한다.현재BOM반례가확인될때만그ID·수량을새로운잔여차이로등록하며,이번보완에서대규모전수를다시시작하지않는다.
- 원본 근거: [D03_조리손질대.asset 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:187>): 312 WU 및203–209행 재료수량 / [V27BalanceWorkCalculator.cs 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs:31>): 작성 constructionWorkRequired 직접 반환
- 위키 근거: [building-1002.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json:19>): 현재312 WU와13·3·2 표시
- 검증 한계: 356/42는2026-09-08 검토 당시 범위이며 이번에 현재 시설 전체를 다시 전수 집계한 수가 아니다. 남은 재료 전수 대조 미완료.
- 이번 재검토: 기존 검토에서는 건설WU356건이 모두 일치했고 직접 비교한 D03재료도 일치했다. 잔여 BOM42건 주장은 전수 대조가 끝나지 않아 확정 차이가0이다. 현재도 이력 hold로 두며 공개 작성 대상으로 승격하지 않는다.

## GAP-118 수술 절차 정보 전역 누락 주장의 철회

- 판정: withdraw
- 근거: 의료 가이드는 대상·같은방 시설·마취·기본 위험·효과와 추가 재료를 이미 공개한다. 개별 도감 빈약함을 공개 전체 누락으로 일반화한 주장을 철회한다. 기존 근거에는 특정 개별 도감의 확정 잔여 필드나 연결 결함이 없으므로 별도 작성 차이를 승인하지 않는다.
- 처리: 
- 원본 근거: [SurgicalProcedureSO.cs 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs:49>): 현재 절차 정의 필드
- 위키 근거: [medical-care-and-surgery.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:40>): 대상·시설·시술자 gate 설명 / [medical-care-and-surgery.md 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:48>): 절차별 대상/시설/WU/마취/위험/효과 표
- 검증 한계: 47절차 에셋과 공개표의 전수 일치까지 인증한 것은 아니다. 특정 entity 링크 결함은 별도 양쪽 근거 없이 승인하지 않는다.
- 이번 재검토: 의료 가이드는 대상·같은방 시설·마취·기본 위험·효과와 추가 재료를 이미 공개한다. 개별 도감 빈약함을 공개 전체 누락으로 일반화한 주장을 철회한다. 기존 근거에는 특정 개별 도감의 확정 잔여 필드나 연결 결함이 없으므로 별도 작성 차이를 승인하지 않는다.

## GAP-127 중앙 금화 권위 설명 해결 이력

- 판정: withdraw
- 근거: 공개 위키는 중앙 잔액과 물품 운반을 정확히 구분하고 있어 이 차이는 해결되어 있다.
- 처리: 해결/철회로 전환한다.
- 원본 근거: [GameRuntimeServices.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/GameRuntimeServices.cs:158>): GameMoneyAccount / [GameRuntimeServices.cs 458행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/GameRuntimeServices.cs:458>): holdingMoney 조회
- 위키 근거: [economy-and-trade.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:13>): 중앙잔액과 물품 운반을 명시적으로 구분
- 검증 한계: 중앙 잔액 조회와 현행 공개 설명의 확인이며 모든 결제·저장을 실행 인증한 것은 아니다.
- 이번 재검토: 공개 위키는 중앙 잔액과 물품 운반을 정확히 구분하고 있어 이 차이는 해결되어 있다.

## GAP-133 작성 세력 계약 수락 진입점 부재 주장 철회 이력

- 판정: withdraw
- 근거: 현재 지도 패널은 작성 계약 목록과 수락 버튼을 만들고 각 계약의 수락 action ID를 실제 명령으로 전달한다. 수락 진입점이 없다는 핵심 주장은 현재 틀리다.
- 처리: 미구현 요구를 철회한다. 새 UI 사용법 누락이 있다면 별도 사실로 재작성한다.
- 원본 근거: [OffenseWorldMapPanelStrategicDetails.cs 623행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs:623>): 계약목록·CanAccept버튼·AcceptActionId전달 / [V20ContentResolutionService.cs 2770행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs:2770>): 작성계약조회·목적지준비·수락명령
- 위키 근거: [factions-contracts-and-prisoners.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:15>): 계약 일반 안내
- 검증 한계: UI 및 명령 연결의 정적 확인이다. 모든18계약의 수락·완료·저장 실행을 인증한 것은 아니다.
- 이번 재검토: 현재 지도 패널은 작성 계약 목록과 수락 버튼을 만들고 각 계약의 수락 action ID를 실제 명령으로 전달한다. 수락 진입점이 없다는 핵심 주장은 현재 틀리다.

## GAP-134 작성 세력 계약 목적지·실물 배송 부재 주장 철회 이력

- 판정: withdraw
- 근거: 현재 물자 계약 수락은 전용 납품 목적지를 만들고 배송 gateway의 실물 이송 영수증을 기록한 뒤 배송 완료 계약을 정산한다. 목적지 없이 전역 스택을 제자리 소비한다는 주장은 낡았다.
- 처리: 현재 배송 연결 상태에 맞춰 해당 불일치를 철회한다.
- 원본 근거: [V20ContentResolutionService.cs 2790행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs:2790>): 물자계약전용목적지 / [V20ContentResolutionService.cs 2200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs:2200>): 목적지물리이송·실물영수증기록 / [V20CampaignRuntime.cs 4426행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:4426>): 배송완료계약성공효과
- 위키 근거: [factions-contracts-and-prisoners.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:21>): 물자 예약·운반 안내와 현재 원본 방향 일치
- 검증 한계: 목적지·물리이송·영수증·배송완료 경로의 정적 확인이다. 모든 실패·복원·중복 정산을 실행 검증한 것은 아니다.
- 이번 재검토: 현재 물자 계약 수락은 전용 납품 목적지를 만들고 배송 gateway의 실물 이송 영수증을 기록한 뒤 배송 완료 계약을 정산한다. 목적지 없이 전역 스택을 제자리 소비한다는 주장은 낡았다.

## GAP-148 물려받은 도구의 일일 발동·운영 수치 누락 주장 철회

- 판정: withdraw
- 근거: 현재 작성정책은 ExternalObservedOnly여서 일일 후보에서 제외되고 공개 도감에도 현재 비활성 표시가 있다. 자동/1일/45일/OncePerCharacter 필드가 있다는 사실만으로 플레이 중 일일 자동 발동 규칙을 승인한 이전 문장을 철회한다.
- 처리: 
- 원본 근거: [life-event_tool-inheritance.asset 26행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_tool-inheritance.asset:26>): automatic/기한/쿨다운/빈도·ExternalObservedOnly0 / [LifeEventDefinitionSO.cs 7행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/LifeEventDefinitionSO.cs:7>): ExternalObservedOnly0 enum / [V20CampaignRuntime.cs 5377행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5377>): 외부관측전용은일일후보에서제외
- 위키 근거: [life-event-tool-inheritance.json 8행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/life-event-tool-inheritance.json:8>): 현재비활성표시·관계효과10
- 관련 GAP: GAP-149
- 검증 한계: 외부 관측 진입점이 이 사건의 실제 원인 시스템으로 연결되는지는 이번 근거로 확정하지 않았다. 작성필드 자동1·응답1일·쿨다운45·개인당1회는 런타임 발동 보장과 분리한다.
- 이번 재검토: 현재 작성정책은 ExternalObservedOnly여서 일일 후보에서 제외되고 공개 도감에도 현재 비활성 표시가 있다. 자동/1일/45일/OncePerCharacter 필드가 있다는 사실만으로 플레이 중 일일 자동 발동 규칙을 승인한 이전 문장을 철회한다.

## GAP-201 공기 교환·덕트의 공기질 전파 계약 누락

- 판정: merge
- 근거: 현재 공기질100·반경4/3/4·덕트0.65는 실제 구현이지만 GAP-081의 공기/덕트 원리와 GAP-082의 동일 시설 수치 요구에 이미 포함된다. 양쪽 required_documentation 원문을 대조했으며 독립 차이가 아니다.
- 처리: GAP-081에 외부 공기 목표100과 덕트0.65 원리를, GAP-082에 E11/E13/E14 반경4/3/4를 통합한다. 독립 GAP로 중복 계산하지 않는다.
- 원본 근거: [EnvironmentalFieldRuntime.cs 687행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:687>): outside->100 및 quality/distance/dt 실제 적용 / [EnvironmentalFieldRuntime.cs 869행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:869>): duct.exchangeRate를 연결에 전달 / [E13_송풍구.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E13_송풍구.asset:143>): 반경3/교환8/덕트0.65
- 위키 근거: [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 칸별 공기·조명 일반설명 / [infrastructure.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:23>): 환경조건 일반설명
- 관련 GAP: GAP-082, GAP-081
- 검증 한계: owner의 현행 문안은 이전 원문보다 축소됐다. 병합된 수치를 owner의 최종 작성 범위에 반영했는지 메인 통합 확인 필요.
- 이번 재검토: 병합 유지. 현행 owner 문안 축소로 실제 수치가 사라지지 않도록 전달 필요

## GAP-202 냉각기·공조기의 온도 제어·thermostat 저장 계약 누락

- 판정: merge
- 근거: 냉각기·공조기의 목표/범위/속도/반경은 GAP-082의 요구와 동일하다. 추가 thermostat ID·범위 저장 검증은 같은 조절 기능의 보존 조건이지 별도 공개 기능이 아니다. 양쪽 원문 요구를 직접 대조했다.
- 처리: GAP-082에 E10 목표8°C·조절2~8°C·속도3·반경3, E11 목표22°C·조절2~30°C·속도2.5·반경4의 작성 규칙을 통합한다. 조절값 보존도 같은 기능에 연결한다.
- 원본 근거: [E10_냉각기.asset 159행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E10_냉각기.asset:159>): 8/2..8/3도/반경3 / [E11_공조기.asset 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E11_공조기.asset:155>): 22/2..30/2.5도/반경4 / [EnvironmentalFieldRuntime.cs 608행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:608>): instance override 또는 authored 목표 소비 / [EnvironmentalFieldRuntime.cs 395행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:395>): owner 정규ID 및 범위 검증
- 위키 근거: [building-1501.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1501.json:19>): 전력 소비/열공급만 표시 / [building-1502.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1502.json:19>): 전력 소비/열공급만 표시
- 관련 GAP: GAP-082
- 검증 한계: owner의 현행 문안은 이전 원문보다 축소됐다. 병합된 수치를 owner의 최종 작성 범위에 반영했는지 메인 통합 확인 필요.
- 이번 재검토: 병합 유지. 현행 owner 문안 축소로 실제 수치가 사라지지 않도록 전달 필요

## GAP-234 정착지 사건 경보의 단계 지연·epoch·중단 업무 저장 계약 누락

- 판정: merge
- 근거: 즉시 상승·2시간 한 단계 하강은 GAP-032, 중단 업무 보존·복귀는 GAP-033의 원문 요구에 포함돼 있다. epoch 저장 필드만으로 새 플레이 차이가 생기지 않는다.
- 처리: 경보 단계는 GAP-032, 중단·복귀는 GAP-033으로 병합하고 독립 GAP에서 제외한다.
- 원본 근거: [SettlementAlertRuntime.cs 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs:11>): 하강안정화2시간 / [SettlementAlertRuntime.cs 549행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs:549>): 하강시간확인 / [SettlementAlertRuntime.cs 370행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs:370>): 중단업무기록
- 위키 근거: [invasions-and-defence.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:13>): 침입경고일반설명
- 관련 GAP: GAP-032, GAP-033
- 이번 재검토: 공개 기능 누락 적격성 재검토: 내부 형식 차이를 독립 게임 GAP으로 세지 않음

## GAP-236 일반 작업 주문 자재의 WIP custody·취소 원복·저장 join 계약 누락

- 판정: withdraw
- 근거: 실물Transfer/restitution은내부트랜잭션구현이다.현재공개저장가이드는건설/생산현장재료·한번만결과반영·그램/거래ID/내용지문·전역참조/총량검증을이미설명한다.원장은더구체적인내부receipt join문법이없다는사실만으로새게임플레이차이를만들었다.
- 처리: 공개누락확정에서철회.개발저장계약으로보존하고취소재료반환의구체사용자결과가별도로빠졌다면그결과만별도등록.
- 원본 근거: [WorkOrderMaterialOutbox.cs 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkOrderMaterialOutbox.cs:16>): Transfer→custody→ack 내부상태머신 / [WorkOrderMaterialOutbox.cs 117행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkOrderMaterialOutbox.cs:117>): restoration outbox
- 위키 근거: [continuity.md 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:16>): 공사재료/WIP저장 / [continuity.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:21>): 결과한번만반영 / [continuity.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:43>): 교차참조총량검증 / [continuity.md 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:47>): 그램·거래번호·지문이미서술
- 관련 GAP: GAP-237
- 검증 한계: 원장코드주장전체의진위승인이아닌공개GAP적격성철회
- 이번 재검토: 공개 기능 누락 적격성 재검토: 내부 형식 차이를 독립 게임 GAP으로 세지 않음

## GAP-237 건설 현장 입력 목적지의 질량 권위·terminal release·복원 계약 누락

- 판정: withdraw
- 근거: 전용목적지ID/fingerprint/revision/claim-profilejoin은구현세부다.공개저장·재고가이드는질량/수용량·파괴/취소·교차참조·원자복원규칙을이미다룬다.별도플레이어선택/효과의미기재근거가제시되지않았다.
- 처리: 공개누락확정에서철회하고개발용건설입력권위감사로이동.
- 원본 근거: [WorkConstructionInputOwnerRuntime.cs 225행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkConstructionInputOwnerRuntime.cs:225>): site없어도descriptor권위로terminalrelease / [WorkConstructionInputOwnerRuntime.cs 242행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkConstructionInputOwnerRuntime.cs:242>): claim/profile짝검증후revoke
- 위키 근거: [inventory-and-carrying.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md:43>): 입고수용량/무게·도착재검증 / [continuity.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:25>): 취소/파괴재료구분 / [continuity.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:39>): 의존구역/원자복원 / [continuity.md 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:47>): 질량·지문검증
- 관련 GAP: GAP-236
- 검증 한계: 공개GAP적격성철회이며전체source정확성을재승인하지않음
- 이번 재검토: 공개 기능 누락 적격성 재검토: 내부 형식 차이를 독립 게임 GAP으로 세지 않음

## GAP-242 일반 작업 주문 저장의 정규화·품질 난수·해체 연계 검증 계약 누락

- 판정: withdraw
- 근거: 정규 ID와 4096 제한 등은 실제 저장 검증 구현이지만, 공개 위키는 주문·현장 자재·WIP·일회성 결과·fingerprint·교차 참조·원자 복원을 이미 설명한다. 내부 payload 필드를 별도 플레이어 누락으로 취급할 이유가 없다.
- 처리: 공개 GAP에서 철회하고 개발자 저장 스키마 문서 후보로 분리한다.
- 원본 근거: [WorkOrderSaveValidation.cs 7행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkOrderSaveValidation.cs:7>): MaxSavedOrders 4096; 저장 검증 상한 / [WorkOrderSaveValidation.cs 124행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkOrderSaveValidation.cs:124>): InProgress, Completed, Cancelled는 저장 상태에서 제외
- 위키 근거: [continuity.md 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:16>): 건설·생산 현장 자재, 진행 및 산출물 복원 / [continuity.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:39>): 섹션 검증·참조·질량·원자 발행과 거래 지문 설명
- 관련 GAP: GAP-236, GAP-237
- 검증 한계: quality/pipeline/destructive drain 전체 분기를 재검증하지 않았으며 구현 완전성 승인이 아니다
- 이번 재검토: 현재 원본과 공개 본문을 재확인하여 공개 누락 철회 유지. 내부 계약 전부를 승인하는 판정은 아니다.

## GAP-246 기분 충동의 생성·검증·명시 발행 경계 계약 누락

- 판정: withdraw
- 근거: 원장 자체도 인정하듯 LLM 응답은 narrative-only이며 현재 게임 경로에서 impulse를 발행하지 않는다. 명시 발행 외부 호출자는 Editor verifier뿐이므로 실제 플레이 차이로 등록하는 것은 부적절하다.
- 처리: 공개 시스템 누락에서 철회하고 비활성/진단용 AI 계약으로 분리한다.
- 원본 근거: [AiDirectorRuntime.cs 446행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/AiDirectorRuntime.cs:446>): 응답은 lastApplied None, 449 narrative-only / [CharacterVisitorControlJourneyPlayModeVerifier.cs 602행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/Editor/CharacterVisitorControlJourneyPlayModeVerifier.cs:602>): 유일한 외부 TryPublishMoodImpulse 호출
- 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 실제 플레이 제어 체계 설명; 실험 발행 API 문서는 불필요
- 관련 GAP: GAP-245, GAP-247
- 검증 한계: 요청/JSON/parser/직렬화 조건 전체 검증이 아니라 현재 활성 경로 부재에 따른 입장 판정
- 이번 재검토: 현재 원본과 공개 본문을 재확인하여 공개 누락 철회 유지. 내부 계약 전부를 승인하는 판정은 아니다.

## GAP-247 Director macro goal의 요청·서사 제안·상태 발행 경계 계약 누락

- 판정: withdraw
- 근거: OnMacroGoalResult는 명시적으로 narrative-only다. 비 Editor SetMacroGoal은 명시 impulse 승격뿐이고 그 impulse의 발행자가 Editor에만 있어 자동 행동 명령 누락으로 취급할 수 없다.
- 처리: 공개 GAP에서 철회하고 AI 개발자 진단 문서로 이동한다.
- 원본 근거: [AiDirectorRuntime.cs 404행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/AiDirectorRuntime.cs:404>): 응답 applied type None; 407 narrative-only / [AiDirectorRuntime.cs 622행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/AiDirectorRuntime.cs:622>): 실제 setter는 mood impulse 승격 경로
- 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 플레이어 업무 제어 설명
- 관련 GAP: GAP-246
- 검증 한계: 모든 요청 간격 및 JSON 검증 세부는 미확인
- 이번 재검토: 현재 원본과 공개 본문을 재확인하여 공개 누락 철회 유지. 내부 계약 전부를 승인하는 판정은 아니다.

## GAP-248 Director mood·macro LLM 요청의 큐 우선순위·포화 재시도 계약 누락

- 판정: withdraw
- 근거: 64 큐 상한과 macro/mood RejectQuietly 프로필은 구현돼 있지만 두 응답이 gameplay 상태를 적용하지 않는 현재 경로의 내부 큐 정책이다. 플레이어 시스템 위키의 독립 누락은 아니다.
- 처리: 내부 LLM 운용 문서 후보로 전환한다.
- 원본 근거: [LocalLlmRequestQueue.cs 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/AI/Core/LocalLlmRequestQueue.cs:85>): macro/mood 프로필 RejectQuietly / [LocalLlmRequestQueue.cs 322행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/AI/Core/LocalLlmRequestQueue.cs:322>): queue64와 timeout12/8 설정 / [AiDirectorRuntime.cs 407행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/AiDirectorRuntime.cs:407>): macro 결과 narrative-only; 449 mood 결과도 동일
- 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 공개 AI/업무 제어 범위
- 관련 GAP: GAP-246, GAP-247
- 검증 한계: 큐 포화 및 재시도 실행 테스트는 하지 않음
- 이번 재검토: 현재 원본과 공개 본문을 재확인하여 공개 누락 철회 유지. 내부 계약 전부를 승인하는 판정은 아니다.

## GAP-257 세력 증원 손실의 신뢰·원한 후속 계약 누락

- 판정: hold
- 근거: 손실 계산 API는 있지만 Assets 전체에서 RecordReinforcementLoss 외부 실게임 호출자를 찾지 못했다(인터페이스·정의·자기 domain 호출·Editor fake만 검색됨). 사망 시 자동 적용되는 실제 차이로 승인할 수 없다. 또 현재 trust=-100이면 delta=0이라 campaign grievance 적용도 생략된다.
- 처리: 사망/장비 손실 이벤트에서 API까지의 실제 연결을 확보하기 전 보류한다. 원한 항상 증가 표현도 조건부로 정정해야 한다.
- 원본 근거: [FactionRuntime.cs 728행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:728>): loss API 정의;739 domain 호출 / [FactionRuntime.cs 741행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:741>): delta != 0일 때만 grievance deaths*2+equipment 적용 / [FactionDomainRuntime.cs 413행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Factions/Core/FactionDomainRuntime.cs:413>): 사망·장비 누적 후 trust -dead*4-lost clamp / [FactionModels.cs 468행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Factions/Core/FactionModels.cs:468>): 명령 포트 선언만 발견
- 위키 근거: [factions-contracts-and-prisoners.md 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:53>): 지원군 요청 비용만 설명
- 관련 GAP: GAP-256
- 검증 한계: 자동 손실 관측/호출 연결 미확인. 전체 Assets .cs 검색에서 외부 호출 없음
- 이번 재검토: 현재 Assets 전체 RecordReinforcementLoss 역검색에도 외부 실게임 호출자가 없어 보류 유지.

## GAP-263 제한 출입문의 런타임 자물쇠 표시 계약 누락

- 판정: merge
- 근거: 자물쇠 표시는 현재도 구현되어 있고 공간 가이드에는 설명이 없다. 같은 출입 정책의 관찰 방법이므로 GAP-154에 병합한 판정을 유지한다.
- 처리: GAP-154에 자물쇠 식별 방법을 합치고 독립 누락으로 중복 집계하지 않는다.
- 원본 근거: [Door.cs 97행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Door.cs:97>): RefreshAccessIndicator가 제한 정책을 자물쇠 표시로 전달. / [DoorAccessLockIndicator.cs 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/DoorAccessLockIndicator.cs:11>): restricted에 따라 표시를 활성/비활성화.
- 위키 근거: [spaces.md 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:51>): 출입 정책 개요만 있고 제한문 식별 표시는 없음.
- 관련 GAP: GAP-154
- 이번 재검토: 자물쇠 표시는 현재도 구현되어 있고 공간 가이드에는 설명이 없다. 같은 출입 정책의 관찰 방법이므로 GAP-154에 병합한 판정을 유지한다.
