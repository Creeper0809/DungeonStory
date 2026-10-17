# 위키 주장과 현재 게임 구현의 차이

상태: 전수 조사 진행 중. 기준 main `c03d2e0a`. 정적 호출·자산 대조이며 Unity 플레이 재현 보고서가 아니다.

기존 `missing-register.md`의 131개 잠정 공백은 주로 구현 내용을 위키가 설명하지 않은 방향이다. 아래는 그 반대 방향을 포함하며 합산하지 않는다. 설정·API 존재와 게임플레이 적용을 구분한다. 확인되지 않은 기능을 없다고 단정하지 않는다.

## 근거 환경

- KB query `wiki system`, areas `implementation/documents`, limit 12: stale, 2429 freshness failures, 반환 0행. 생성물을 재생성하지 않았다.
- content digest `76cce09a76ded556dc74ac400c571e92136f17f86fc3697dfb9eb30dca669871`
- system digest `74d85480f35dc5704306579326fa8f38e3bb5841f0dbaab8e697f4be25522a06`
- 위키 가이드 30개 본문 완독은 이전 감사의 범위다. 모든 문장의 구현 검증 완료를 뜻하지 않는다.
- 기존 원장: 가격 198개, 건설 WU 103시설·BOM 42시설 등 문서 수치 차이는 해당 상세 원장에 보존한다. 이 신규 목록과 중복 집계하지 않는다.

## 직전 턴에서 직접 조사한 구현 연결 공백

모든 경로는 저장소 상대 경로다. 공통 위키 위치는 `wiki/game-versions/0.0.1v/content/guides/`다. 호출자 부재 판정은 Editor·테스트·등록·정의 자체를 라이브 소비처에서 제외한 정적 검색 범위다.

| ID | 위키 설명/대상 | 현재 확인한 차이 | 직접 원본 | 판정 및 한계 |
| --- | --- | --- | --- | --- |
| DIFF-001 | species-culture-and-life: 평상·작업·예비 의복 | 일반 환복 명령의 외부 운영 호출은 환경 작업복뿐. 일반 의복 선택 UI/자동 환복 호출 미발견 | Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs; EnvironmentalWorkwearRuntime.cs | 일반 연결 미확인. 작업복 자동 착용 자체는 존재 |
| DIFF-002 | 같은 문서: 원단이 보온·통기·방수·내구 결정 | IApparelMaterialProjector.GetOrCreate의 실제 소비처 미발견. 보호는 고정 EnvironmentalWorkwearSO.Protection을 읽음 | Assets/Scripts/Services/Infrastructure/Environment/ApparelItemStateCodec.cs:335; EnvironmentalWorkwearRuntime.cs:485 | 소재 투영→보호 계산 단절 |
| DIFF-003 | 의복 세탁·건조·수선 순환 | 세탁/수선 상태 쓰기는 있으나 착용·비·작업이 의복 moisture/contamination/durability를 누적하는 생산자 미발견 | Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs; ApparelItemStateCodec.cs | 일반 아이템 오염과 의복 component는 구분. 동적 재현 미실행 |
| DIFF-004 | species-culture-and-life: 종족 환경 적응 | preferredHumidity/drynessSensitivity/comfortableAirMinimum/light 범위/visualStrainMultiplier는 정의·복사 외 소비 미발견 | Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs:125; Assets/Scripts/Services/Character/SO/CharacterModelData.cs:394 | 온도와 airborneExposureMultiplier는 연결됨. 모든 종족 효과 누락 아님 |
| DIFF-005 | 질병 도감의 직접 작업·이동 배율 | diseaseSymptoms 주입 후 실제 작업/이동 계산에서 사용하지 않음 | Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:172 | 질병 기분·신체를 통한 간접 영향과 별개 |
| DIFF-006 | infrastructure/production-quality-and-supply: 자동 품질 상한 | 27시설의 automaticQualityCap 작성값에 runtime 소비 미발견 | Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs:317 | 목표 범위 표와 실제 품질 제한을 구분 |
| DIFF-007 | infrastructure: 컨베이어 목적지 운영 | SetPortDestination은 인터페이스/구현만 있고 gameplay 호출 미발견 | Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:256 | 경로 알고리즘 존재와 설정 가능성 별개 |
| DIFF-008 | infrastructure: 분류/분배/넘침 | UI는 오염·신선도·금지·품질 토글만. 아이템/카테고리/재료 목록 및 예비창고 선택 UI 미발견 | Assets/Scripts/Views/UI/IndustrialFeatureSurfacePresenter.cs:370 | overflow enum은 전환하지만 기존 ReserveWarehouseId 재사용 |
| DIFF-009 | weather-seasons-and-environment: 계절 사건의 다영역 효과 | Threat는 pressure ID만 보존. HasPressure의 확인된 소비는 ending 계열. 계절 pressure 11개와 migration-window 실제 도메인 소비 미확인 | Assets/Scripts/Services/Run/V20CampaignRuntime.cs:1514; MilestonePressureApplicationAdapter.cs | WorkDelayDays 등 다른 효과는 작동 경로 존재. 모든 계절 사건 무효라는 의미 아님 |
| DIFF-010 | milestones-runs-and-meta: 시작 후보 강화 | 주인 특성 후보 보너스 구매/저장/query는 있으나 시작 후보 생성 측 소비 미발견 | Assets/Scripts/Services/Infrastructure/Core/MetaProgressionRuntime.cs:70 | 나머지 계승 강화까지 미구현으로 일반화 금지 |

## 추가 불일치·실행 확인 사항

| ID | 대상 | 정적 근거와 확인할 사항 |
| --- | --- | --- |
| DIFF-011 | 전력/연료 중단 시 조명 | EnvironmentalFieldRuntime의 RequiresPower는 thermal/air만 본다(847). 순수 조명 정전·연료 고갈 효과는 실행 확인 필요 |
| DIFF-012 | 문 개폐 시 환경망 | 같은 파일은 문 여부를 보지만 열린 상태 소비 미발견. 벽 변경과 문 열기는 별개 |
| DIFF-013 | 전력 연결기 스위치·통과량 | IndustrialInfrastructureBuildingAbilities normallyOpen/maxThroughput의 runtime 소비 미발견. 과부하 breaker는 별도 존재 |
| DIFF-014 | 장기 냉장 보존 | IsOrganPreservationSafe 2~8°C helper/query는 존재하나 장기 보관/열화 소비 미발견 |
| DIFF-015 | 연구 예상 기간 | Assets/Scripts/Views/UI/ResearchTreeWindow.cs:686은 180×0.55=99 WU/일 추정. 공개 research의 V27 일정 45 WU/일과 다름. 연구 실행 자체 누락 아님 |

## 이번 턴 추가 확인 — 농업·축산·원정

| ID | 위키 설명 | 현재 구현 | 판정 |
| --- | --- | --- | --- |
| DIFF-016 | food-and-ecology: 적정 온도·물·조명·토양을 충족한 시간만 생육 누적 | CropPlotRuntime.TickState(1966~1983)→ResolveGrowthMultiplier(2784). 실내는 ability·기후제어시설·농사달력·유전체 배율만 곱한다. 물은 파종 시 주기분 입력을 계산한다. 현재 칸 빛·온도·토양을 모두 충족해야만 시간 누적하는 규칙이 아님 | 설명 과장/다른 조건. 야외 기온 정지·날씨·야간 배율은 실제 연결됨 |
| DIFF-017 | weather-seasons-and-environment/expeditions: 날씨·기후가 원정 이동을 변경 | OffenseTravelProfile.WeatherMultiplier는 존재하지만 운영 출발/귀환 호출은 Default(1). OffenseTravelRuntime.TrySetDestination은 경로 비용을 버리고 좌표만 저장. Tick(568)은 기본 2.5초×의료×이정표×시설 | 날씨→이동 연결 누락. 지형 경로 선호와 실제 칸별 이동 시간을 혼동하면 안 됨 |
| DIFF-018 | weather-seasons-and-environment: 계절·날씨가 축산 생산 주기를 변경 | AnimalHusbandryRuntime.AdvanceAnimals(720)·AdvanceProducts(855)·TryBeginPregnancy(904)는 나이·성별·길들임·우리정책/궁합·먹이질병·시설보정만 사용. 기후·계절 입력 없음 | 축산 주기에 대한 직접 계절/날씨 효과 미확인. 야생동물 계절 스폰과 혼동 금지 |
| DIFF-019 | food-and-ecology: 전력누출/환기구/마나빛을 따르는 종별 이동 | MigrationPatternId는 WildlifeSpeciesSO/Definition에서 작성·복사하지만 runtime 소비 미발견. 일반 서식지·포식·계절퇴장은 별도 구현 | 종별 특수 이동 설명의 실행 근거 미확인. 범용 서식지 선택까지 미구현은 아님 |

반증/정상 연결도 기록한다:

- `CropGrowthCycleAuthority` 야외 성장: 비×1.10/안개×0.85/폭풍×0.55/폭염·한파×0.90, 야간×0.55. 기온 범위 밖 0. 실제 CropPlotRuntime에서 호출한다.
- `CropCycleInputRequirementAuthority`(152): 야외 비·폭풍은 파종 주기의 물 계수 0.5. 실물 물의 주기분을 올림한다. 매 틱 날씨 변화에 맞춰 물 소비를 다시 계산하는 것은 아니다.
- `WildlifeEcosystemRuntime.TickAnimal`(239)은 비활동 계절 LeaveMap; `ScoreRespawnWeight`(800)은 번식 계절 스폰 가중치×1.65. 가축 임신 계절 제한과 별개다.
- `WildlifeRuntime`(245)→`WildlifeDiseaseVectorRuntime.PublishDailyExposure`는 살아 있는 벡터의 2칸 이내 주민에게 매일 질병 노출 event를 발행한다. DiseaseVectorIds를 고아로 판정하지 않는다. 마지막 질병 소비자 검증은 후속 항목이다.

### 미확인 보류

위키의 염소/숫양 운반, 사냥개 경비·추적, 두더지 광맥 탐색은 종 stable ID 검색만으로 부재를 단정하지 않는다. 현재 husbandry profile은 생산물·번식 정보이며, 실제 역할 할당/운반 주체/탐색 소비 전체를 추가 확인해야 한다.

### 야생동물 종별 역할 후속 판정

상세 모집단과 정상 관계69행: `wildlife-species-entry-review.md`. 이전의 stable ID 검색만이 아니라 비Editor 축산·포획·작업·물류·전력·작물·채굴 소비 전체와 실제 도축 출력 경로를 추가 대조했다.

| ID | 위키 설명 | 현재 구현 | 판정 |
| --- | --- | --- | --- |
| DIFF-029 | 심층염소·서리숫양 운반, 동굴사냥개 경비/추적, 굴착두더지 작물피해/광맥탐지, 발광나방·포자큰사슴 포자확산, 마나도깨비불 전력누출 추종/비전생물 유인 | 비Editor에서 wildlife/husbandry와 운반·경비·추적·채굴·작물·전력·포자의 교차 소비0. StableHarnessing은 모든 가축의 comfort×1.05로 산물·분뇨·임신을 가속할 뿐 동물을 운반 주체로 만들지 않는다 | 종별 산업·환경 역할 실행 연결 미발견. 일반 포식·질병·활동계절·털/비단 생산은 별도 정상 연결이며 DIFF-019의 이동패턴 미소비와 중복 집계하지 않음 |
| DIFF-030 | 수정딱정벌레는 단단한 갑각을 남기고 사체를 부산물로 나눈다 | `wildlife_crystal_beetle.asset`의 butcherYields/husbandryProducts 모두 빈 목록. `WildlifeCarcassService`는 authored ButcherYields만 물리 출력으로 만들며 수정딱정벌레 갑각 item도 없음 | 현재 산출 없는 설명. 일반 사체 item 존재와 갑각 부산물 생산을 구분 |

DIFF-029는 여러 문구의 공통 연결 공백을 한 행으로 묶은 것이며 종 수만큼 독립 결함을 더한 집계가 아니다. 잿불도마뱀의 먹이/질병, 진흙거머리의 포식/질병, 동굴명주거미의 먹이/비단, 서리숫양의 계절/털처럼 실제 연결된 절은 이 판정에서 제외했다.

## 의료·수술·전투 추가 대조

상세 근거와 수술24개 효과표: `medical-combat-followup-review.md`. 수술47개 작성/도감 전수 필드: `surgery-projection-followup-review.json`. 소재12개 배율과 종족별 치유: `combat-material-and-species-procedure-review.json`.

| ID | 위키 설명/부족한 설명 | 실제 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-020 | 장비 무게 기본×소재×진화 | 착용 부담은 이 공식, 물리 운반/창고는 정의gram+모듈+탄약. 서로 반영 항목 다름 | 설명 공백 및 질량권위 불일치 후보. 위키 착용부담 공식 자체는 맞음 |
| DIFF-021 | 수혈은 혈액 손실 완화 | 10WU/blood-pack1→부위health14/infection-2. bloodLoss 직접 감소 없음 | 명칭/효과 연결 차이 |
| DIFF-022 | 긴급봉합은 출혈을 닫음 | health8/infection-8, 실제 bleedingPerSecond 감소 없음 | 주문 incisionOpen 종료와 신체 지혈을 구분 |
| DIFF-023 | 종족 특화 이식·보철·보강 | 종족별24개는 모두 일반 치유. 일부 이식명칭에도 설치효과 없고 missing 치유 거부 | 해당 명칭의 의미 차이. 세정/처치까지 전부 잘못된 이름으로 세지 않음 |
| DIFF-024 | 개별 수술의 효과·조건 안내 | 47개 facts는 작업/연구/효과개수만. 효과값·마취·대상·시설·위험 등 없음 | 도감 설명 부족. 작업량/효과개수94값 자체는 일치 |
| DIFF-025 | 수술 환경이 중요하다는 안내 | 구체 중단/응급예외/정상5초재개/단계별0.25위험 합산 설명 없음 | 구현은 연결돼 있고 위키 설명이 부족함 |

수술 작업/효과개수47×2 및 소재12×5배율은 모두 일치했다. 이 6행을 미구현 버그6개로 집계하지 않는다.

## 구조 치료·생활 건강 치료 교차 대조

상세 근거: `general-medical-cross-path-review.md`, 작성22약품/10시설140필드: `general-medical-authoring-review.json`.

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-026 | 의료 흐름: 병상 이송 뒤 안정화 | AbilityRescue는 환자 위치에서 안정화한 뒤 TryBeginCarrying. 미안정이면 운반 거부 | 순서 오류. GAP-122와 중복 합산 금지 |
| DIFF-027 | 치료를 하나의 의료·약품·병상 흐름으로 설명 | 구조 주문은 Downed만, 생활시설 치료는 별도 상태·시간·HP 및 창고 카테고리 소비. WU/약효/대상 선택도 다름 | 두 경로 설명 누락. 모든 보행 환자 치료가 불가능하다는 뜻 아님 |
| DIFF-028 | 해독제는 독소·과다 복용 완화, UI 해독 수치 | detoxReduction의 production 소비는 UI 외 미발견. 질병 antiparasitic의 해독제 소비는 고정 중증도26 감소 | authored 해독 효과 연결 미확인. 해독제의 모든 쓰임이 없다는 뜻 아님 |

## 남은 범위

### 경제 후속: 중앙 금화와 물리 금고

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-031 | 금고 접근·운반이 막히면 결제가 지연될 수 있다 | GameMoneyAccount는 세션 holdingMoney를 직접 차감/입금하며 확인한 급여·자동구매·지역납품 결제에는 금고 위치·운반 검사가 없다 | 물리 금화 출납 설명 불일치. GAP-127/ECON-001과 같은 건이며 중복 합산하지 않음 |

상세: [경제·고용·자동 조달](economy-employment-procurement-review.md), [25개 원본 해시·독립 산술9건](economy-employment-procurement-evidence.json). 구매품·납품품의 실물 물류가 없다는 뜻은 아니다. 나머지 ECON-002~004는 위키의 세부 설명 공백이며 모두 미구현 버그로 세지 않는다.

### 세력 계약·교역 경로 후속

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-032 | 세력 계약을 수락하고 납품·방어·조사 의무를 이행하는 운영 기능 | authored 계약18개, 도메인 accept/outcome과 dispatcher는 있으나 action ID를 만드는 비Editor UI/AI 호출자0. Editor debug 직접 호출만 있음 | 현재 플레이 진입점 연결 미발견. GAP-133과 같은 건이며 일반 route/지역계약은 별도 |
| DIFF-033 | 계약 물자를 예약하고 운반할 인원·경로를 준비 | authored 계약 성공은 모든 월드 스택 수량 판정 뒤 가능한 exact stack을 StackId 순으로 예약해 제자리 원자 소비. 전용 목적지·배송주문·운반자 없음 | 물류 실행 의미 불일치. GAP-134와 같은 건이며 지역 공급 계약은 실제 배송을 사용 |
| DIFF-034 | 세력의 무상 화물, 교역/보급 화물 재사용시간 | 6세력 교역 policy는 모두 paid-market-purchase이고 금화를 선결제. 보급만 alliance-benefit 예산 사용 | 유료 교역과 무상 보급 구분 필요. 최소 cooldown 공식 자체는 일치 |

상세: [세력 계약·교역 경로](faction-contract-route-review.md), [52개 직접 근거](faction-contract-route-evidence.json). 계약 페이지 요구 item·수량18개는 일치하며 정상 필드를 오류로 세지 않는다.

### 주민 식사 자동 소비 후속

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-035 | `food:lavish-meat`와 `food:lavish-vegan`을 주민이 먹는 호화식으로 안내 | 두 품목의 band는 Lavish지만 자동 facility/field 후보는 주민 품질 상한을 적용하고 기본 Inherit를 Fine으로 해석한다. 비Editor의 `SetMealQualityLimit` 호출자와 특정 stack `ConsumeMealCommand` 생산자를 찾지 못했다 | 현재 확인한 production 자동 경로에서 호화식2종 도달 불가. 실행 재현 판정은 아니며 GAP-137/138의 필드·규칙 누락과 같은 근거를 사용 |

상세: [주민 식사 소비·효과](meal-consumption-review.md). 실제 식사22개 자체는 모두 ItemDefinitionCatalog·공개 entry·가이드에 연결되며 페이지 부재로 세지 않는다. FoodItemFeature만 가진 사체/placeholder25개와 음료2개도 식사 후보로 오분류하지 않는다.

### 음료·기호물질 typed 효과 후속

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-036 | 활력 강장제는 피로 감소, 마나 각성제는 연구·비전 감각 증폭, 몽엽 진통제는 통증 감소 | 범용 substance 경로는 각각 mood와 모든 작업/전투 배율만 적용하고 피로/수면 need·연구/비전 전용·통증 상태를 직접 바꾸지 않는다. 몽엽의 별도 질병 대응은 1개 sink·중증도22 감소이며 GAP-020 소유 | 세 품목의 typed 설명과 범용 효과 의미 차이. 품목마다 독립 결함으로 중복 집계하지 않음 |

상세: [음료·기호물질·중독](substance-consumption-review.md). 물질9개 페이지와 범용 AI/시설 소비는 연결돼 있으므로 시스템 전체가 미구현이라는 판정은 아니다.

### 사회 사건·손님 요청·서비스 사고

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-037 | 사건 선택 뒤 주민 이동·물건 운반·조건 충족 작업이 이어지고, 예약·운반 상태와 경로를 확인한다 | 사회 사건54개의 선택은 사용 가능한 월드 stack을 exact StackId 순으로 예약한 뒤 제자리에서 원자 소비한다. 사건 전용 목적지·배송 주문·운반자·작업 목록이 없다 | 현재 사회 사건 실행 의미 불일치. 세력 계약의 같은 문제인 DIFF-033과 대상 계열이 달라 분리하되 근본 물류 공백은 중복 집계하지 않음 |
| DIFF-038 | 중요한 사건은 평상시2~5일마다, 위기1~2일마다1회이며 동시 중요 사건 최대2개 | 런타임은 매일 평가한다. 30일까지 일반1, 31일부터 일반2와 별도 비상1이라 이후 총3개가 가능하다. 강제 간격은 같은 분류3일과 같은 정의30/45/90일뿐이라 다른 분류를 즉시 해결하면 다음 날 새 사건이 가능하다 | 목표 빈도 문장과 실제 scheduler 계약 차이. 확률 평균 재현이 아니라 구조 규칙 비교 |
| DIFF-039 | 사건 창에 대상·기한·위험·선택 조건·필요 자원과 예약/운반 상태를 표시하고, 중요 경보5·치명 경보2·자동 소사건6개 뒤 요약한다 | 현재 사회 사건 경보는 설명+절대 마감일과 선택 제목/outcome만 표시한다. 모두 High이고 enum은 Low/Medium/High뿐이다. 기록 상한/치명 등급/자동 사건 하루 요약 subscriber가 없다 | UI·경보 정책 연결 누락. 자동 사건 최대6과 사건 페이지54개 자체는 정상 |
| DIFF-040 | 사건은 현재 주민·시설·관계의 실제 원인에서 시작하고 서비스 사고는 실제 손님·직원·시설·물건을 원인으로 기록하며, 선택은 해당 수술·격리·호위·제작·체포·수리 등을 실행한다 | 서비스 사고8개와 생애 사건32개의 trigger requirements는 모두 비어 있어 실제 서술 원인 없이 일일 후보가 된다. 손님 요청14개도 fulfill 시 물품/시설 snapshot만 검사하며, 세 통합계획 CSV는 구체 도메인 reader/operation/receipt/reaction을 모두 OPEN으로 둔다 | generic alert·선택·금액/물품/typed 효과는 정상 연결됨. ‘사건 전체 미구현’이 아니라 구체 causal context와 도메인 operation 의미 차이를 한 행으로 묶음 |
| DIFF-041 | 문화별 축제와 공동 공간의 사회 활동 | 작성 축제16개 중10개에 cultureId가 있지만 비Editor 소비가 없다. 해당 문화 주민이 없어도 경보가 뜨고 모든 생존자를 참가자로 세며, 의식 단식도 내 문화가 아닌 다음 날의 아무 축제에 반응한다. 실제 주민 이동·시설 점유·축제 작업도 없다 | 축제 개최·물품 소비·기분/애도/호의는 작동한다. culture/현장 참가 연결만 미확인이고 직접 stack 소비는 DIFF-037과 중복하지 않음 |
| DIFF-042 | 시작 준비 화면에서 현재 건강·특성뿐 아니라 식단·수면·기후 조건을 함께 확인한다 | 실제 GameplayScene의 `OwnerSelectionPanel`은 출신·특성, 잠재력·9개 숙련, 생성 기술을 보여 주지만 나이·질환·식단·수면·기후 적합 정보는 표시하지 않는다. 별도 미사용 렌더러도 나이와 건강 문제 개수까지만 표시한다 | 시작 후보 선택 UI의 정보 노출 차이. 후보 생성 자체의 나이·초기 질환과 종족 생활 규칙은 존재하므로 시스템 부재가 아니라 presentation 공백으로 한정 |

상세: [사회 사건·손님 요청·서비스 사고](society-event-scheduler-review.md), [축제·사건 통합계획 교차 대조](society-events-festivals-review.md), [정적 근거](society-event-scheduler-evidence.json). 작성/공개 사건은54/54로 대응하며 서비스 사고8개는 모두 대응3개를 가지므로 페이지 부재나 대응 수 부족으로 세지 않는다.

### 사건 작업 지연의 실제 업무 범위

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-043 | 해빙수 범람은 낮은 농지와 운반 통로를 동시에 덮친다 | `flood` scope는 WorkType ID에 farm/crop/agric/haul/logistic/carry가 들어가는지 검사하지만 현재31개 ID 중 일치하는 것은 `work:haul`뿐이다. `work:sow`, `work:harvest`, `work:animal-care`는 감속되지 않는다 | 농지 영향 설명과 실제 WorkDelay 범위 차이. 범람의 다른 위협·질병 효과 전체가 없다는 판정은 아니며 road/whiteout의 원정 이동은 DIFF-017과 중복하지 않음 |
| DIFF-044 | 은퇴 요청에서 “한 계절 더 현장을 부탁한다” 선택은 `adds-work-delay -2`로 공개된다 | 음수 WorkDelay는 기존 동일 scope의 종료일만 당기고 새 상태나 가속을 만들지 않는다. 작성83행 중 `life-event:retirement-request` 동일 scope의 양수 효과가 없고 사건은 인물별1회라 정상 작성 경로에서 줄일 상태가 없다 | 공개된 -2 관계가 실제 선택 효과 없이 no-op. 외부/모드가 같은 scope를 선행 주입한 경우까지 항상 무효라는 판정은 아님 |
| DIFF-045 | `spaces.md`는 “완공된 벽과 닫힌 문은 이동을 막는다”고 설명한다 | 현재 `Door`/`DoorAccessPolicyState`에는 플레이어가 여닫는 open/closed 물리 상태가 없고, 문 칸의 `CanTraverse`는 주체의 집단·개별 출입 정책을 검사한다 | 문 정책7집단·5프리셋·저장은 실제 연결됨. 문 시스템 전체 부재가 아니라 물리 개폐 표현과 정책 기반 통과 구현의 차이 |

상세: [31개 업무와 사건 작업 지연 매핑](work-delay-mapping-review.md), [방 환경·역할·문화 선호·문 권한](room-and-door-runtime-review.md). 작성 WorkDelay83행은 `faction-work:*`72, service incident5, life event3, 계절3이며 정확한 target83개는 모두 고유하다. 처음 발견한 faction chapter3 target 불일치 후보는 집계기가 다음 effect의 target을 덮어쓴 오탐으로 확인해 등록하지 않았다.

### 보안 시설·경계 신호 나팔 후속

| ID | 위키 설명 | 현재 확인한 차이 | 판정 |
| --- | --- | --- | --- |
| DIFF-046 | 경계 신호 나팔은 침입과 화재 같은 위험을 멀리 있는 주민에게 알린다 | 비Editor item consumer는 침입 신호소 경로 하나뿐이다. operational Security 시설에 나팔1개가 준비되면 침입 집결+6초·성공 시 내구도1 소비는 작동하지만 fire↔signal/horn production 참조는0이다 | 침입 기능 전체 부재가 아니라 공개 설명에 포함된 화재 경보 효능의 미연결. WIM-061과 같은 근거를 사용 |
| DIFF-047 | 식사·상점·의료·공연 물품은 실제 물건을 서비스 장소까지 옮긴 뒤 소비한다 | 의료 치료는 현재 Grid의 등록 창고에 Stored인 medicine1 또는 biological1 stack을 item/stack ID 순으로 골라 그 자리에서 physical Sink한다. 의료시설 buffer·예약·운반/배송·도착 검사가 없다 | 물리 stack 자체는 소비하지만 의료 서비스 장소로 옮긴다는 조건은 거짓이다. 복합 범주 문장의 일부 미구현이므로 partial WIM-062와 같은 근거를 사용 |

상세: [보안 시설·경계 신호 나팔](security-facility-signal-horn-review.md). Guard 완료로 별도 alarm charge가 최대3까지 쌓이고 저장되지만 소비처가 없는 사실은 위키가 약속한 화재 기능과 다른 구현 고아 상태라 DIFF-046에 중복 합산하지 않았다.

서비스실 상세: [서비스 허브·지원시설·의료 물류](service-room-runtime-review.md). 공개 문장은 `guests-services-and-performance.md:21`, 구현 경계는 `SurvivalFoodRuntime.TryApplyTreat`와 `SurvivalFoodStockRuntime.TryConsumeTreatmentMaterial/WithdrawStock`이다.

농업·축산·야생동물·원정의 남은 전체 효과, 의료/전투/경제/사건 전체 실행 연결, 도감 2,905개 효과·관계 전수, 참고 문서·노출 템플릿, 기존 누락의 위키 다른 위치 중복 설명 및 source freshness 재확인이 남았다. 전체 목표는 미완료다. 이번 농업·축산·원정의 상세 대조는 `ecology-travel-followup-review.md`, 원본 48개 해시는 `ecology-travel-followup-evidence.json`에 있다.
