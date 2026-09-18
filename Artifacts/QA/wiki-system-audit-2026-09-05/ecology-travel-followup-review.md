# 농업·축산·야생동물·원정 환경 연계 후속 감사

기준 main `c03d2e0a`. 정적 확인이며 실제 Unity 실행·저장 왕복 미실행. 밸런스 영향 없음: 코드·수치·에셋은 바꾸지 않고 설명 차이만 기록했다.

## 1. 작물 12종 작성값 전수 대조

`CropDefinitionSO.cs.meta` GUID `6a00dd16ef9aaab44b6e51a3f9da8394`를 Resources 전체 `.asset`에서 역검색해 12개를 열거했다. 공개 도감은 `wiki/game-versions/0.0.1v/data/entities/nature/crop-<suffix>.json`, 가이드는 `food-and-ecology.md`다.

성장시간·기본 산출·일일 물의 36값은 가이드와 도감 요약 양쪽에서 모두 일치했다. 모든 작물 indoorAllowed=1. 아래 WU와 온도 범위는 도감 facts에 없으며 facts는 필요 연구 1개뿐이다. 해당 누락의 위키 전체 다른 페이지 교차검토는 미완료다.

| crop suffix | 기본 생육 h | 기본 수확 개 | 작성 물/일 | 파종 WU | 수확 WU | 야외 온도 °C |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| twilight-grain | 36 | 6 | 0.35 | 3 | 6 | 4~30 |
| cave-mushroom | 28 | 5 | 0.20 | 3 | 5 | 3~26 |
| ember-root | 42 | 5 | 0.25 | 4 | 7 | 2~28 |
| night-grape | 54 | 5 | 0.50 | 5 | 8 | 8~32 |
| bloodleaf | 46 | 4 | 0.35 | 4 | 7 | 8~30 |
| dreamleaf | 52 | 4 | 0.30 | 5 | 8 | 6~26 |
| moonflower | 60 | 3 | 0.40 | 5 | 9 | 5~24 |
| shade-fiber | 40 | 6 | 0.30 | 4 | 7 | 5~30 |
| ember-cotton | 92 | 6 | 0.40 | 4 | 7 | 14~42 |
| frost-flax | 84 | 6 | 0.35 | 4 | 7 | -8~20 |
| mire-reed | 100 | 6 | 0.45 | 4 | 7 | 8~34 |
| spore-hemp | 108 | 6 | 0.50 | 4 | 7 | 6~30 |

앞 8개의 작성 경로는 `Assets/Resources/SO/Economy/Crops/crop_<underscored_suffix>.asset`, 나머지 4개는 같은 디렉터리의 `V22Textiles/crop_<hyphenated_suffix>.asset`다.

### 설명에서 구분해야 하는 것

1. `36시간마다 6개`는 파종/수확 WU·노동자 대기·성장 배율·수확물 인계를 포함한 실제 반복 처리량이 아니다. 기본 생육시간/기본 수확량이다. CropPlotRuntime의 Growing 전후 상태와 Output receipt gate가 존재한다.
2. 물은 매일 소수량을 직접 소비하는 것이 아니다. CropCycleInputRequirementAuthority는 `ceil(dailyWater × GrowthHours/24 × 물계수 × 소비보정)`개의 실물 물을 파종 주기에 준비한다. 양수면 최소 1개이며 야외 비/폭풍이면 물계수에 0.5를 곱한다. 파종 입력 weather를 동결·저장하므로 매 틱 재산정하지 않는다. 예: 황혼곡, 물계수/소비보정 1이면 맑음 0.525→1개, 비 0.2625→1개로 반올림 후 같아진다.
3. 실내는 현재 칸 온도·빛·수분을 검사하는 성장 gate가 없다. 실내시설·기후제어·달력·유전체 배율을 사용한다. 야외는 온도 범위 밖 정지, 비1.10/안개0.85/폭풍0.55/폭염·한파0.90, 야간0.55를 사용한다.
4. 토양이 아무 효과 없는 것은 아니다. CropEcologyDomain.ComputeYieldMultiplier(816)는 비옥도0~100을 수확 배율0.55~1.0으로 투영하며, 연작0.85·해충·질병·품종 배율을 함께 곱한다. 이는 매 틱 성장시간을 막는 토양 gate와 다르다.
5. OnDayEnded(2911)의 고사 판정은 실외 온도가 품종 보정 적정범위보다 5°C 더 벗어나면 lethal을 ecology.AdvanceDay에 전달한다. 실내는 이 온도 lethal 판정을 받지 않는다. 해충/질병 고사는 별도다.

## 2. 축산과 야생동물: 연결 여부 분리

| 설명/기능 | 확인한 실제 연결 | 한계/차이 |
| --- | --- | --- |
| 축산 5초 갱신 | AnimalHusbandryRuntime.Tick interval=5, elapsed/180를 AdvanceAnimals로 전달 | 위키와 일치 |
| 생산·번식 | 나이·성별·길들임·우리정책·공간/궁합·사료병·시설보정→산물 진행/임신 진행 | 직접 날씨/계절/현재 칸 온도 입력은 이 경로에 없음 |
| 가축용 멜빵 시설 | StableHarnessing→comfort×1.05 | 동물을 실제 짐꾼으로 바꾸는 경로는 아님 |
| 야생 계절 활동 | WildlifeEcosystemRuntime.TickAnimal→IsActiveIn→LeaveMap | 가축 계절 번식과 다름 |
| 번식 계절 | ScoreRespawnWeight×1.65 | 자연 스폰 가중치이며 실제 가축 임신 달력이 아님 |
| 질병 매개 | WildlifeRuntime245→WildlifeDiseaseVectorRuntime→PopulationDiseaseRouteExposureEvent→PopulationHealthRuntime347/425→health.RecordExposure | 살아 있는 동물, 매일, Manhattan거리≤2, 거리0은1h/그 외0.5h 노출. 실제 감염은 질병 권위가 결정 |
| 종별 이동 패턴 | MigrationPatternId 작성·복사는 존재 | 실제 소비 미발견. 일반 서식지·계절 이동은 구현됨 |
| 염소·숫양 짐꾼/개 경비·추적/두더지 광맥 | 위키 설명은 존재 | 역할 배정/물리 운반/탐색 결과 경로 추가 확인 필요. 키워드 부재만으로 최종 미구현 확정하지 않음 |

후속 대조에서 stable ID 검색을 넘어 비Editor 역할·물류·작물·채굴·전력 교차 소비를 확인했고 해당 실행 연결을 찾지 못했다. `wildlife-species-entry-review.md`와 DIFF-029를 현재 판정으로 사용한다. 수정딱정벌레 갑각 산출은 authored 출력0으로 DIFF-030에 분리했다.

## 3. 원정

- 운영 `TryPrepareTravel`은 `OffenseTravelProfile.Default`를 전달한다. Default의 weather/injury/load는 각각 1이다. 다른 운영 출발/귀환 호출도 같은 Default를 사용한다.
- `OffenseHexWorldSimulation.GetTravelCost`는 지형·도로·weather/injury/load를 비용에 곱해 경로를 고른다. 함수 존재 자체를 실제 날씨 반영으로 세지 않는다.
- `OffenseTravelRuntime.TrySetDestination`은 계산된 비용을 `out _`로 버리고 경로 좌표만 보존한다.
- `Tick`의 이동 간격은 `2.5초 × 의료 이동 배율 × 이정표 배율 × 시설 배율`이다. 지형 가중치는 경로 선택에는 쓰지만 각 칸의 이동 시간으로 직접 소비하지 않는다. 날씨 query도 없다.
- 위키 최대 5명은 LaunchService(154), 편성 UI와 전략전투 상한에서 일치한다. 날씨 연계의 누락을 전체 원정 미구현으로 일반화하지 않는다.

## 4. 범위와 다음 조사

기록 검증: 근거48개 현재 SHA 일치, 표12행의 작성 필드72개 대조 오류0, 별도 차이원장19개 ID 중복0, `git diff --check` 오류0. README의 기존 Git 줄끝 정책 안내(LF→CRLF)는 게임 경고나 검사 실패가 아니다. 이 검사는 기록의 정확성을 확인하며 Unity 기능 테스트를 대체하지 않는다.

이번 체크포인트: 작물 정의 12/12의 선택 작성값 대조, 농업 환경 분기·축산 진행·야생 계절/질병·원정 경로/시계의 정적 소비 확인. 전체 농업/축산/원정 도메인 검증 완료가 아니다. 유전체 전수·야생동물 전체 역할/산물·식품 실제 섭취·전체 원정 결과·기존 원장 중복/다른 위키 설명은 남았다.

오류 기록: Windows rg의 경로 wildcard 인수가 실패한 호출은 결과로 세지 않았다. 후속에는 실제 디렉터리와 `-g`로 검색했다. 출력이 잘린 광역 검색 역시 부재/완독 근거로 사용하지 않았다.
