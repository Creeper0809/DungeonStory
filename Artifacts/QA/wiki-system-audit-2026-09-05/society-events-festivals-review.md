# 축제·손님 요청·서비스 사고·생애 사건 후속 감사

상태: 정적 원본 대조 완료. Unity 플레이 실행·저장 왕복은 미실행이다. 게임 코드·자산·공개 위키는 수정하지 않았다.

## 범위와 판정 경계

- 작성 모집단: 축제16개, 손님 요청14개, 서비스 사고8개, 생애 사건32개.
- 계절 사건28개는 기존 GAP-091~093이 소유하므로 다시 세지 않는다.
- 일반 손님 영입·만족은 GAP-038~040, 소매 손님은 GAP-140이 소유한다.
- 생산 UI와 generic 캠페인 선택/효과 transaction은 존재한다. 따라서 네 계열 전체를 고아 또는 미구현으로 판정하지 않는다.
- 통합계획에 남은 실제 수술·격리·호위·제작·체포·수리 operation과 현재 generic 효과를 구분한다.

## 축제16개 작성 전수표

모든 축제는 성공 `기분+6/10일`, 부분 `+2/5일`, 실패 `-3/4일`이다. 공용 축제는 성공 시 모든 세력 호의+3, 실패 시 -2이고 문화 축제는 호의 변화0이다. grief 축제는 성공25%·부분10%를 전환한다.

| ID | 계절/일 | 문화 | 시설 | 준비물 | 최소 참가 | grief |
| --- | --- | --- | --- | --- | ---: | ---: |
| `festival:sprout` | 봄15 | 공용 | `building:festival-common-hall` | `seed-lot:twilight-grain`×12 | 8 | 0 |
| `festival:high-sun` | 여름15 | 공용 | `building:festival-common-hall` | `food:twilight-beer`×8 | 8 | 0 |
| `festival:storage` | 가을25 | 공용 | `building:festival-common-hall` | `food:preserved-ration`×16 | 10 | 0 |
| `festival:long-night-memorial` | 겨울30 | 공용 | `workstation:v19:memorial-room` | `craft:candle`×12 | 10 | 1 |
| `festival:frontier-map-night` | 봄22 | adventurer-frontier | `building:expedition-map-room` | `material:paper`×8 | 6 | 0 |
| `festival:pack-first-hunt` | 가을8 | beastkin-pack | `building:festival-common-hall` | `resource:meat`×18 | 8 | 0 |
| `festival:ash-oath` | 겨울12 | demon-contract | `building:faction-audience-hall` | `material:paper`×12 | 6 | 0 |
| `festival:core-resonance` | 여름6 | golem-core | `building:rune-tuning-room` | `tool:maintenance-kit`×2 | 5 | 0 |
| `festival:open-sky-chorus` | 봄10 | harpy-aerie | `building:weather-observation-tower` | `resource:night-grape`×10 | 6 | 0 |
| `festival:tool-clan-fair` | 가을18 | kobold-toolclan | `building:apprentice-workbench` | `component:machine-parts`×2 | 6 | 0 |
| `festival:spore-bloom` | 봄25 | myconid-grove | `building:cave-growing-rack` | `resource:cave-mushroom`×14 | 6 | 0 |
| `festival:weapon-vigil` | 겨울20 | orc-vigil | `building:armory` | `material:charcoal`×12 | 8 | 1 |
| `festival:clear-confluence` | 여름20 | slime-confluence | `building:clean-water-reservoir` | `resource:clean-water`×24 | 8 | 0 |
| `festival:blood-lantern` | 가을30 | vampire-nightcourt | `workstation:v19:memorial-room` | `craft:candle`×8 | 6 | 1 |
| `festival:many-tables` | 여름28 | 공용 | `building:festival-common-hall` | `food:lavish-vegan`×8 | 20 | 0 |
| `festival:dungeon-accord-day` | 가을12 | 공용 | `building:faction-audience-hall` | `craft:dreamweave-ritual-banner`×1 | 12 | 0 |

### 축제 실행

- 해당 계절/일에 매년 high alert 한 건을 게시하고 버튼은 모든 생존자를 참가자로 넘긴다.
- 작성 definition ID와 같은 첫 생존 시설이 있어야 한다.
- 성공은 참가자와 준비물 모두100%, 부분은 둘 다 절반 올림 이상, 그 아래는 실패다.
- 성공은 준비물100%, 부분은50% 올림, 실패는0개를 예약·원자 소비한다.
- 소비 뒤 출석/슬픔 전환, 참가자 기분, 공용 축제의 전 세력 호의, 완료 event를 적용한다.
- 공개 entity는 문화 축제12개뿐이고 facts0/required-item 관계1개다. 공용4개는 V19/V20 중복 ID 두 건씩이 `manual-review` 제외되어 공개 entity가 없다.

### 문화·참가자 연결 확인

- 작성16개 중 `cultureId`가 있는 문화별 축제는10개, 공용은6개다. 공개12개라는 수치는 문화 축제 수가 아니라 중복 제외 뒤 남은 페이지 수다.
- `cultureId`는 정의·Editor builder 밖의 비Editor 소비가 없다. 날짜 경보는 해당 문화 주민의 존재를 검사하지 않고, 버튼은 살아 있는 주민 전원을 참가자로 넘기며 성공/부분 인원에도 전원을 센다.
- 의식 단식 자동 시작도 다음 날 **어떤 축제든** 있으면 `ritual:fast` 행동 규칙을 가진 모든 주민에게 적용을 시도하며 주민 문화와 축제 `cultureId`를 대조하지 않는다.
- 참가자와 시설은 즉시 결과 검증에만 쓰인다. 주민 이동, 시설 점유·예약 시간, 축제 작업 WU, 준비물의 시설 배송은 생성하지 않는다. 전역 stack 직접 소비는 DIFF-037의 공통 물류 공백과 같은 원인이므로 별도 행으로 중복하지 않는다.
- `convertsActiveGrief` bool은 runtime에서 읽지 않지만 실제 grief 감소는 outcome의 `griefConversionPercent`가 수행하며 현재 세 자산에서 두 작성값이 일치한다. 효과 전체가 빠진 것으로 판정하지 않는다.

## 손님 요청14개

종류7개가 각2종이며 기한은2~8일이다. 13종은 물품1+시설1, `monster-circus`만 시설1·물품0을 요구한다. 전부 성공효과2·실패효과1이다.

| ID | 종류 | 기한 |
| --- | --- | ---: |
| `guest-request:allergen-banquet` | LuxuryMeal | 4 |
| `guest-request:bodyguard-kit` | Armament | 5 |
| `guest-request:coronation-feast` | LuxuryMeal | 5 |
| `guest-request:disease-sample` | Research | 6 |
| `guest-request:emergency-surgery` | Medical | 2 |
| `guest-request:flood-refuge` | Refuge | 4 |
| `guest-request:memorial-performance` | Spectacle | 5 |
| `guest-request:militia-arms` | Armament | 4 |
| `guest-request:monster-circus` | Spectacle | 6 |
| `guest-request:persecuted-family` | Refuge | 3 |
| `guest-request:plague-screening` | Medical | 3 |
| `guest-request:precision-barter` | Trade | 7 |
| `guest-request:sealed-archive` | Research | 8 |
| `guest-request:winter-fuel-auction` | Trade | 5 |

`fulfill`은 현재 world snapshot으로 물품/시설 등을 검사하고 물품 consume 효과를 합성한다. `decline`은 즉시 failureEffects를 사용한다. 공개14개는 성공/실패 효과 개수 facts만 있고 관계는13개다. 기한·종류·fulfill/decline·실제 값·만료·재발 규칙은 설명되지 않는다.

## 서비스 사고8개

`brawl`, `theft`, `contamination`, `culturalinsult`, `forbiddenmeal`, `medicalcollapse`, `envoyconflict`, `sabotage`가 각3개 선택과 각 선택당1개 generic 효과를 가진다. 공개8개는 facts0·효과 관계18개다.

작성 trigger requirements는 8개 모두 비어 있다. 따라서 현재 일일 캠페인 후보는 실제 난투·절도·오염·금기식·환자·사절·파손 영수증을 요구하지 않는다. 선택지는 실제 alert/dispatcher로 해결할 수 있지만, 통합계획이 요구하는 체포·격리·조사·수리 operation/receipt/reaction은 OPEN이다.

## 생애 사건32개

- 분류: Childhood6, Apprenticeship6, PartnershipFamily6, Career6, ElderRetirement4, DeathLegacy4.
- 자동12개/선택20개, emergency0, trigger requirements0개.
- 32개 모두 OncePerCharacter. 자동은 deadline1/cooldown45, 선택은 deadline3/cooldown90이다.
- 자동12개는 효과1개씩 즉시 적용한다. 선택20개는 선택2개·효과2개씩이다.
- 공개32개는 분류 fact1개씩과 효과 관계49개만 제공한다.

현재 자동 사건은 빈 참가자 중 한 명을 결정론적으로 골라 하루 최대6개를 즉시 적용한다. 선택 사건도 작성된 나이·가구·작업·사망·건강 원인을 검사하지 않고 빈 참가자에게 배정될 수 있다. 통합계획의 출생/작업/장비/기록/가구 reader 및 장기 operation은 모두 OPEN이다.

## 공통 society event 상태 계약

- 하루 시작에1회 평가한다. 자동 생애 사건은 하루 최대6개다.
- 일반 활성 한도는 30일까지1개, 이후2개이며 서비스 사고는 별도 emergency1개까지다.
- 같은 definition과 같은 category가 활성 또는 cooldown이면 새 후보에서 제외한다.
- deadline 당일까지 선택 가능하고 다음 날(`deadline < today`) 만료된다.
- 만료 효과는 손님 요청의 failureEffects만 있다. 생애 사건·서비스 사고는 효과 없이 만료 완료된다.
- 해결 뒤 guest/incident는 정의30일, 자동 life45일, 선택 life90일 cooldown과 category3일 cooldown을 기록한다.
- active/recent resolved 최대256/once/recurrence/cooldown/work-delay/last-evaluation은 society save 검증 대상이다.
- production 선택 transaction은 금액 선검사, exact stack 수량 예약/원자 소비, item grant dropoff 준비·commit/rollback, 참가자 typed 효과를 처리한다. 이는 선택 문구의 모든 도메인 작업을 생성한다는 뜻이 아니다.

## 원장 반영

- GAP-148(기존 동시 조사): 손님 요청14·서비스 사고8·생애 사건32의 개별 필드 누락.
- GAP-149(기존 동시 조사): society event 공통 후보·동시 진행·cooldown·save 규칙 누락.
- GAP-150: 축제16개 공개 대응과 필드 누락.
- GAP-151: 축제 날짜·등급·소비·효과 실행 규칙 누락.
- DIFF-037~039(기존 동시 조사): 물류 실행, 발생 간격/동시 슬롯, 사건 창·경보 정책 차이.
- DIFF-040: 실제 원인과 도메인 operation을 보장한다는 가이드와 empty trigger/integration OPEN의 차이.
- DIFF-041: 문화별 축제의 `cultureId`와 현장 참가가 경보·참가자 선택·의식 단식에 연결되지 않음.

## 주요 원본

- `Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs`
- `Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs`
- `Assets/Scripts/Services/Run/V20CampaignRuntime.cs`
- `Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs`
- `Assets/Scripts/Services/Run/V20ContentResolutionService.cs`
- `Assets/Scripts/Services/Character/Identity/Runtime/CharacterRitualFastingRuntime.cs`
- `Assets/Scripts/Content/GuestRequestDefinitionSO.cs`
- `Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs`
- `Assets/Scripts/Content/LifeEventDefinitionSO.cs`
- `docs/game-design/content-plans/guest-request-integration.csv`
- `docs/game-design/content-plans/service-incident-integration.csv`
- `docs/game-design/content-plans/life-event-integration.csv`
- `wiki/game-versions/0.0.1v/content/guides/events-and-choices.md`
- `wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md`
