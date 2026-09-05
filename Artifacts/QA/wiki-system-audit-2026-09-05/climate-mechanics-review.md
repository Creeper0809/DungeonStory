# 기후·날씨·계절 사건 대조

상태: 부분 감사. 전체 시스템·위키 감사는 미완료다. 기후5종·날씨6종·계절사건28개와 대응 도감을 전수 대조했다. 코드 정적 확인과 실제 실행을 구분한다.

## 이미 표시된 내용과 실제 공백

- 날씨6종의 기간·기온보정·계절가중치42수치와 기후 평균5수치는 facts와 일치한다. 기후 진폭·시차10수치도 summary에 있으므로 미노출로 세지 않는다.
- 기후5개 모두 진폭을 연교차로 잘못 적었다. 날씨와 잡음을 제외한 연교차는 진폭의2배다.
- 계절사건28개의 기간·종료효과개수84필드는 일치한다. 물/숯 소비2개와 질병 노출5개의 대상·수치는 기존 관계에 이미 있다. 이를 새 누락으로 중복 집계하지 않는다. 나머지21개는 관계 배열이 비어 있다.
- 실제 공백은 초기 기후 예외, 기온 공식, 교체 추첨의 의미, 관측 장비·예보, 사건 수명주기, 세력/금액/지연 효과와 각 단계 적용 방식이다. GAP-087~093에 기록했다.

## 기후 5종

| 기후 | 평균°C | 진폭°C | 날씨·잡음 제외 최고-최저차°C | 시차 |
| --- | ---: | ---: | ---: | ---: |
| 잿불 황무지 | 27 | 8 | 16 | 0 |
| 균사 심층 | 16 | 5 | 10 | 0 |
| 마나 폭풍지 | 12 | 16 | 32 | 0 |
| 서리 균열 | 0 | 12 | 24 | 0 |
| 온대 동굴 | 14 | 14 | 28 | 0 |

## 날씨 6종

가중치는 날씨가 끝난 뒤 새 날씨를 선택할 때의 비중이다. 달력상 점유 일수의 비율이 아니다.

| 날씨 | 기간(일) | 기온 보정°C | 봄 | 여름 | 가을 | 겨울 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 맑음 | 1~3 | 0 | 35 | 45 | 30 | 35 |
| 한파 | 2~5 | -12 | 8 | 1 | 8 | 30 |
| 안개 | 1~3 | -2 | 15 | 8 | 20 | 18 |
| 폭풍 | 1~2 | -6 | 10 | 13 | 15 | 9 |
| 비 | 2~4 | -3 | 30 | 15 | 25 | 8 |
| 폭염 | 2~5 | 10 | 2 | 18 | 2 | 0 |

## 계절 사건 28개

모두 시작 효과1개·일일 효과0개·종료 flag1개다. 발동 요구8개 배열은 모두 비어 있다. 아래 duration은 효과 작성값이며 반복횟수나 자동 해제 보장이 아니다. 강도는 Editor 작성 감사 산식을 별도로 계산한 값이다.

| 계절 | 사건 | 기간 | 시작 효과: 종류 / 대상 / 값 | 효과 duration | 작성 강도 | 기존 관계 수 |
| --- | --- | --- | --- | ---: | ---: | ---: |
| 봄 | [봄열 피난민](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-fever-camp.json>) | 2~4 | 질병 노출 / disease:red-fever / 10 | 4 | 5 | 1 |
| 봄 | [이동 초식군](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-migrant-herd.json>) | 2~4 | Threat 표식 / herd / 3 | 4 | 3 | 0 |
| 봄 | [둥지철](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-nesting-season.json>) | 3~5 | Threat 표식 / nests / 2 | 5 | 2 | 0 |
| 봄 | [떠돌이 종자상](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-seed-exchange.json>) | 2~2 | 세력 호의 / faction:dungeon:myconid / 4 | 2 | 3 | 0 |
| 봄 | [포자비](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-spore-rain.json>) | 2~4 | 질병 노출 / disease:spore-lung / 8 | 4 | 4 | 1 |
| 봄 | [해빙수 범람](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-thaw-flood.json>) | 2~3 | 작업 지연일 / flood / 1 | 3 | 2 | 0 |
| 봄 | [씻겨나간 길](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-washed-road.json>) | 1~3 | 작업 지연일 / road / 2 | 3 | 4 | 0 |
| 여름 | [마르는 수원](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-dry-well.json>) | 3~5 | 아이템 소비 / resource:clean-water / 6 | 5 | 6 | 1 |
| 여름 | [축제 식재료 경쟁](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-festival-scarcity.json>) | 2~3 | 금액 / market / -120 | 3 | 2.4 | 0 |
| 여름 | [폭염 전력부하](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-heat-grid.json>) | 2~5 | Threat 표식 / power-grid / 4 | 5 | 4 | 0 |
| 여름 | [마나 번개](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-mana-lightning.json>) | 1~3 | Threat 표식 / mana-surge / 5 | 3 | 5 | 0 |
| 여름 | [연무 계곡](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-smoke-valley.json>) | 2~4 | 질병 노출 / disease:ash-lung / 7 | 4 | 3.5 | 1 |
| 여름 | [해충 대발생](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-vermin-bloom.json>) | 3~5 | Threat 표식 / crop-pests / 5 | 5 | 5 | 0 |
| 여름 | [부상 용병대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-wounded-mercenaries.json>) | 2~3 | 세력 원한 / faction:dungeon:beastkin / 5 | 3 | 3.75 | 0 |
| 가을 | [겨울 전 대상행렬](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-caravan-rush.json>) | 2~4 | 금액 / contract / 180 | 4 | 3.6 | 0 |
| 가을 | [이른 서리](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-early-frost.json>) | 2~4 | Threat 표식 / early-frost / 4 | 4 | 4 | 0 |
| 가을 | [수확 몫 분쟁](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-harvest-dispute.json>) | 2~3 | 세력 원한 / faction:dungeon:kobold / 5 | 3 | 3.75 | 0 |
| 가을 | [짧은 이동창](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-migration-window.json>) | 2~3 | 세계 flag / migration-window / 1 | 3 | 0 | 0 |
| 가을 | [포식자 하산](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-predator-descent.json>) | 3~5 | Threat 표식 / predators / 5 | 5 | 5 | 0 |
| 가을 | [썩은 수레](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-rot-cart.json>) | 1~2 | 질병 노출 / disease:gut-rot / 9 | 2 | 4.5 | 1 |
| 가을 | [사일리지 발열](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-autumn-spoiled-silage.json>) | 1~3 | Threat 표식 / silage-fire / 4 | 3 | 4 | 0 |
| 겨울 | [동굴 독감 유행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-cave-flu-wave.json>) | 3~6 | 질병 노출 / disease:cave-flu / 12 | 6 | 6 | 1 |
| 겨울 | [심층의 메아리](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-deep-echo.json>) | 1~2 | Threat 표식 / truth-guardian / 5 | 2 | 5 | 0 |
| 겨울 | [동결 배관](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-frozen-pipes.json>) | 2~4 | Threat 표식 / frozen-pipes / 4 | 4 | 4 | 0 |
| 겨울 | [연료 쟁탈](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-fuel-demand.json>) | 2~4 | 아이템 소비 / material:charcoal / 8 | 4 | 8 | 1 |
| 겨울 | [굶주린 무리](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-hungry-pack.json>) | 3~5 | Threat 표식 / hungry-pack / 6 | 5 | 6 | 0 |
| 겨울 | [추모 사절단](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-memorial-envoys.json>) | 1~2 | 세력 호의 / faction:dungeon:golem / 5 | 2 | 3.75 | 0 |
| 겨울 | [백색 암흑](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-winter-whiteout.json>) | 1~3 | 작업 지연일 / whiteout / 3 | 3 | 6 | 0 |

## 설명에 추가할 규칙

### GAP-087 초기 기후 보호 기간과 계절별 외기 계산식 누락

첫 1~5일은 기후 정의와 무관하게 맑음·외기 20°C·일일 잡음 0으로 고정된다. 6일부터 정상 기후로 전환한다. 한 계절은 30일, 1년은 120일이며 연중일 d=(절대일-1)%120+1이다. 외기=평균기온+연진폭×sin(2π×(d-30)/120)+날씨 기온보정+일일 잡음이다. 잡음은 -2+4×난수로 생성하며 명목상 -2~2°C 범위다. float 반올림상 상한 2를 배제한다고 단정하지 않는다. 하루가 바뀔 때 갱신하며 시각에 따른 별도 일교차 항은 없다. 온대 동굴의 맑음·잡음0일 때 30/60/90/120일 외기는 14/28/14/0°C다. 기본 시작 기후는 온대 동굴이며 나머지 기후의 실제 선택 경로는 구현 확인 대상으로 남긴다.

근거: [ClimateDomain.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs>), [ClimateRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs>), [CoreSessionContracts.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/CoreSessionContracts.cs>), [weather-seasons-and-environment.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md>)

### GAP-088 날씨 교체 시점·계절 가중치·지속일 선택 규칙 누락

현재 날씨의 남은 일수를 하루마다 1 줄여 0이 되면 그날의 계절 가중치로 새 날씨를 고른다. 양수 가중치만 후보에 들며 확률은 해당 가중치/합계다. 현재 각 계절 합계는 100이고 겨울 폭염은 0으로 추첨에서 제외된다. 지속 기간은 선택된 날씨의 최소~최대 정수 범위에서 별도로 고른다. 직전 날씨를 후보에서 빼지 않으므로 같은 날씨가 다시 선택될 수 있다. 계절이 바뀌었다는 이유만으로 진행 중 날씨를 교체하지 않는다. 가중치는 교체 추첨 확률이지 달력상의 점유 일수 비율이 아니다. 저장에는 기후·현재 날씨·남은 일수·일일 잡음이 들어가며 난수 스트림의 교차 저장 검증은 별도다.

근거: [ClimateDomain.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs>), [ClimateRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs>), [WeatherFrontDefinitionSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/WeatherFrontDefinitionSO.cs>), [weather-seasons-and-environment.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md>)

### GAP-089 기후 도감 5개의 온도 진폭을 연교차로 표기

annualAmplitudeC는 평균기온에서 위아래로 움직이는 계절 기온의 진폭이다. 날씨·일일 잡음을 제외한 최고-최저차는 진폭의 2배다. 온대 동굴14→28°C, 균사 심층5→10°C, 마나 폭풍지16→32°C, 서리 균열12→24°C, 잿불 황무지8→16°C로 구분한다. 원본 숫자를 바꾸지 않고 도감의 라벨을 '계절 기온 진폭'으로 정정하거나 연교차를 2배로 계산해 표시해야 한다. 현지 시차는 5개 모두0이며 외기 공식의 입력은 아니다.

근거: [ClimateDomain.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs>), [ClimateZoneDefinitionSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/ClimateZoneDefinitionSO.cs>), [ClimateZone_ember-wastes.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/ClimateZone_ember-wastes.asset>), [climate-ember-wastes.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/climate-ember-wastes.json>)

### GAP-090 기상 관측탑의 장비·내구도·예보 표시 조건 누락

가동 가능한 기상 관측탑에 계절력 책자1개와 기상 관측 도구함1개가 물리 장비 슬롯으로 공급되어야 예보가 표시된다. 최대 내구도는 각각180/120, 하루 관측 소모량은0.25/1이다. 180÷0.25=720,120÷1=120은 단순 최대 마모 횟수이며 보급 지연·고갈·교체를 포함한 보장 가동일이 아니다. 공급 준비 여부가 참일 때 예보 표시일수=min(3,max(1,현재 날씨 남은 일수)), 아니면0이다. 실제 시간 HUD는 '예보 N일'만 표시하며 미래의 다른 날씨 목록을 제공하는 기능과 구분해야 한다. 여러 관측탑 중 ID순 첫 시설만 공급·가동 확인에 쓰는 현행 경로와 신규 건설 직후 갱신 지연은 구현 확인 사항이다.

근거: [ClimateRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs>), [ClimateDurableEquipmentRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentRuntime.cs>), [ClimateDurableEquipmentPolicySource.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentPolicySource.cs>), [ItemPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs>)

### GAP-091 계절 사건의 후보·재발·동시 진행·종료일 규칙 누락

하루 시작에 1회 평가하며 활성 계절 사건이 있으면 새 사건을 시작하지 않는다. 종료 후 그날 계절에 속하고 이번 주기에 미완료이며 발동 요구를 충족한 후보에서 런 시드·날짜·ID의 안정 해시로 선택한다. 계절별7개는 후보 정의 수이며 매 계절7회 발생 보장이 아니다. 선택 지속일은 최소+해시%(최대-최소+1)이다. 시작 효과는 시작 시, 일일 효과는 다음 평가부터, 종료 효과는 만료 시 적용한다. 현재 28개는 발동 요구가 전부 비어 있고 일일 효과도 전부 없다. 기존 사건은 계절이 넘어도 기한까지 남는다. 코드의 deadline=시작일+지속일, 만료조건=오늘>deadline과 주기=절대일/120은 각각 포함 일수 및 달력 연말과의 경계 확인이 필요하다. 이를 수정 없이 '정확히1~6일'이라는 실행 보장으로 쓰지 않는다.

근거: [V20CampaignRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs>), [V20CampaignApplicationAdapter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs>), [V20ContentResolutionService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs>), [SeasonalWorldEventDefinitionSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs>)

### GAP-092 계절 사건 28개의 효과 단계·대상·수치 설명 부족

각 사건에 발생 계절, 시작/일일/종료 단계와 실제 대상·효과를 구분해 표시한다. 현재 시작효과는 호의2·원한2·금액2·아이템소비2·작업지연3·Threat11·질병노출5·WorldFlag1이고 종료효과28개는 resolved flag다. 예: 떠돌이 종자상은 균사 세력 호의+4, 추모 사절단은 골렘 호의+5, 수확 몫 분쟁은 코볼트 원한+5, 부상 용병대는 수인 원한+5, 축제 식재료 경쟁은 금액-120, 겨울 전 대상행렬은+180이다. 물·숯 비용과 질병 대상/수치의 기존 관계 표시는 유지하고 시작 시 1회 적용임을 보완한다. durationDays가 모든 효과를 매일 반복하거나 끝날 때 되돌리는 값은 아니다. Threat11개는 현재 압력 ID만 기록하며 물 동결·해충·발화 등 개별 결과 소비가 확인되지 않는다. 영향영역2개와 정규화강도≤12는 작성 감사 메타데이터이지 두 시스템의 실제 피해 실행 증거가 아니다. 28개 전수표와 구현 미확인 목록을 함께 참조한다.

근거: [SeasonalWorldEventDefinitionSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs>), [V20AuthoredContentContracts.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Content/V20AuthoredContentContracts.cs>), [V20CampaignRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs>), [V20ContentResolutionService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20ContentResolutionService.cs>)

### GAP-093 사건으로 생기는 작업 지연의 중첩·기간·실제 속도 배율 누락

업무에 해당하는 활성 지연 범위가 n개면 작업 속도에 max(0.5,0.8^n)을 곱한다. n=0/1/2/3/4에서1/0.8/0.64/0.512/0.5배다. 같은 범위의 양수 지연은 기존 종료일과 오늘 중 늦은 날짜에 지연일을 더하며, 동일 범위를 여러 별개 배율로 세지 않는다. 오늘이 종료일 이상이면 비활성이다. flood는 farm/crop/agric/haul/logistic/carry를 포함한 업무ID, road·whiteout은 expedition/haul/logistic/carry/trade를 포함한 업무ID에 적용한다. global과 service-incident/life-event/faction-work 범위는 일반적으로 전체 업무에 적용된다. 이는 이름상의 업무ID 판정이며 '원정 이동이 정확히 해당 일수 정지'라고 풀어 쓰면 안 된다. 현재 해빙수 범람1일·씻겨나간 길2일·백색 암흑3일의 지연량은 사건 표시 지속일과 별개다. 정확한31업무 매핑과 이동 도메인 직접 소비는 후속 대조 대상이다.

근거: [V20CampaignRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs>), [CharacterStatsProjectionService.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs>), [events-and-choices.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/events-and-choices.md>), [residents-and-work.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md>)


## 문서 누락과 분리할 구현 확인 사항

### CLIMATE-U01 기후 선택과 시차 적용 경로

ClimateRuntime은 신규 상태를 온대 동굴로 생성한다. LocalHourOffset은 정의·카탈로그 밖에서 비Editor 직접 소비를 찾지 못했다. 다른 기후를 실제 선택/이동하는 UI와 저장 설정 경로는 미확인이다.

### CLIMATE-U02 관측탑 하나만 선택하고 갱신 시점 제한

ID순 첫 가동 시설만 TryMaintain/IsOperational에서 확인한다. 그 시설이 장비 미공급일 때 다음 관측탑을 조회하지 않는다. 유지 호출은 Start와 하루 종료이므로 건설·도구 공급 직후의 예보 갱신은 실제 화면 확인이 필요하다.

### CLIMATE-U03 예보와 현재 날씨 잔여일수의 구분

ForecastHorizonDays는 현재 날씨 잔여일수 상한3이다. HUD는 일수만 표시한다. 다음 전선 종류·다음날 온도 목록을 생성하거나 노출하는 기능으로 검증하지 않았다.

### CLIMATE-U04 계절 사건의 종료일·연말 경계

deadline=start+duration 및 today>deadline 만료이면 시작일10/기간2 사건은10~12일 활성,13일 만료다. 주기 absoluteDay/120은120일에 바뀌지만 달력 연도는121일에 바뀐다. 현재 코드를 정적으로 확인한 것이며 경계 실행 테스트는 미실행이다.

### CLIMATE-U05 Threat11개와 이동창 flag의 개별 결과 소비 미확인

Threat는 amount/duration 대신 targetId를 activePressureIds에 중복 없이 추가한다. 비Editor HasPressure 소비자는 ending:* 계통뿐이며 계절 target11개, migration-window와 resolved:seasonal:*의 별도 소비는 전체 Assets/Scripts 역검색에서 확인되지 않았다. 따라서 배관 동결·야생동물 생성·시설 손상·추가 이동 효과를 완료로 세지 않는다. affectedDomainIds의2개는 작성 메타데이터다.

### CLIMATE-U06 비용 부족 시 하루 사건 평가 전체 거부와 실패 알림

DailyEvaluation 후보는 비용 선검사를 통과한 후에만 live 상태를 publish한다. 물6·숯8·금액120이 부족하면 false이며 OnDayStarted는 failure를 버린다. 계절·사회 평가가 같은 명령에 묶인 데 따른 부작용/재시도/시계 진행은 별도 실행 확인이 필요하다.

### CLIMATE-U07 저장·장비 교체·질병의 실제 실행

기후 Capture/Restore와 계절 Prepare/Publish를 읽었으나 난수 스트림과의 교차 복원, 공통 장비 전달/마모/교체, 질병 노출 이후 감염, 사건 결과 UI 입력은 실행하지 않았다. 최대 마모 횟수는 단순 산술로만 표시한다.

### CLIMATE-U08 계절 사건 영향 영역과 강도 상한은 작성 감사

StrategicContentBalanceCalibrationScenario는 영역 이름2개·기간1~6·정규화강도≤12를 검사한다. 실제 두 도메인 소비를 검증하거나 런타임 피해를12로 자르는 코드가 아니다. 현재28개를 같은 산식으로 계산하면0~8이며 이는 C# 감사 실행 결과가 아니다.


## 검증과 한계

- 카탈로그 Root→GameDomain 참조39/39. 대응 도감39/39. 공개 수치131개 비교 오류0. 위키 전체 의미 중복 최종 검토는 남아 있다.
- 작성 강도는 28개 모두0~8이며 일일효과가 없으므로 시작·종료 합으로 계산했다. Editor 시나리오 실행이나 실제 피해 상한 검증이 아니다.
- 독립 산술15개 오류0. C# 실행·Unity 컴파일·실제 UI·저장왕복은 수행하지 않았다.
- 직접 근거148개의 경로와 SHA-256, 전체39행과 효과56개는 [JSON](climate-mechanics-review.json)에 보존했다. 전체 파일 해시는 부분 읽기를 완독으로 뜻하지 않는다.
- KB query=ClimateRuntime ClimateZone WeatherFront, areas=code/content/authority, limit8, session35989: stale4/반환0행. content digest=139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6; KB digest=ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd. 생성물은 재생성하거나 현재 근거로 쓰지 않았다.
- 스크립트·자산·공개위키·서버는 변경하지 않았다. 밸런스 영향 없음.

## 남은 범위

- 종족·의복 온도 보호 전수
- 날씨의 작물·축산·원정 등 개별 도메인 보정
- 계절 Threat/flag 소비처 연결 및 실제 UI·저장 실행
- 시설 일반 장비 보급·수리·교체 수명주기
- 식품 전체 부패·보존·오염 수명주기
- 31업무별 지연 범위 및 나머지 전체 시스템·문서 의미 대조

