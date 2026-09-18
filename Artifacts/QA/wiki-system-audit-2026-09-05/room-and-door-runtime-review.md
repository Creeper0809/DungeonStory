# 방 환경·역할·문화 선호·문 권한 후속 감사

상태: 정적 원본·호출자 대조 완료. Unity Play Mode, 실제 UI 조작, 저장 왕복은 실행하지 않았다. 공개 위키나 게임 원본은 수정하지 않았다.

## 조사 경계

- 공개 문서: `spaces.md`, `species-culture-and-life.md`, 공개 entity 전체, `omitted-relations.json`
- 방 원본: `RoomSpatialDomain.cs`, `RoomEnvironmentDomain.cs`, `RoomEnvironmentAdapter.cs`, `RoomEnvironmentExperienceAdapter.cs`, `RoomRole.cs`, `RoomFacilityPolicyDomain.cs`, `RoomFacilityPolicyAdapter.cs`
- 문화 원본: `SpeciesCultureDefinitionSO.cs`, `CharacterCultureGameplayRuntime.cs`, `FacilityScoringContext.cs`, `FacilityCandidateScorer.cs`, 문화 자산10개
- 문 원본: `DoorAccessModels.cs`, `DoorAccessService.cs`, `DoorAccessStateModule.cs`, `DoorAccessUnityAdapter.cs`, `DoorAccessPanelPresenter.cs`

`spaces.md`는 방의 운영 요소를 정성적으로 소개하지만 아래 점수·문턱·수치·저장 계약을 싣지 않는다. `species-culture-and-life.md`도 문화가 방 환경과 공동 공간 만족 조건을 바꾼다고만 설명한다.

## 1. 방 성립과 품질

`RoomInstance`는 셀이 있고 열린 경계가0이며 문이 하나 이상이거나 self-contained일 때 사용할 수 있다. 다만 환경식에서 self-contained는 방 환경 적용 대상 밖으로 분류된다.

- `IsClosed = Cells.Count > 0 && OpenBoundaryCount == 0`
- `HasDoor = Doors.Count > 0 || IsSelfContained`
- `IsUsable = IsClosed && HasDoor`
- 기본 품질은 사용할 수 없는 방이면0, 아니면 `clamp01(0.5 + min(1,area/8)×0.25 + min(1,doors/2)×0.15 + min(1,furniture/4)×0.1)`이다.

환경 설정 권위는 `Assets/Resources/Config/RoomEnvironmentSettings.asset`이다.

| 점수 | 현재 공식 |
| --- | --- |
| 넓이감 | `100 × (InverseLerp(2,16,area)×0.45 + freeCellRatio×0.55)` |
| 아름다움 | `50 + luxury×1.25 - damagedRatio×30 - max(0,occupiedRatio-0.6)×50` |
| 청결 | `60 + min(25,hygiene) + min(10,cleanStreak×2) - damagedRatio×35 - max(0,occupiedRatio-0.5)×40 + (operationalCleanliness-50)×0.2 - worldFilthPenalty` |
| 바닥 오염 페널티 | `clamp(averageFilth×0.65 + peakFilth×0.35,0,70)` |
| 인상 | `beauty×0.35 + spaciousness×0.30 + cleanliness×0.20 + quality×100×0.15` |

방 검사 화면은 면적·빈칸·문·벽·시설·파손 수, 넓이감·아름다움·청결·인상, 차폐·온도·환기·조명과 역할 기여 시설을 표시한다. 공개 위키에는 계산식과 등급 경계가 없다.

## 2. 작업·시설 선택·기분에 쓰이는 수치

작업 환경 점수는 `인상×0.6 + 청결×0.4`이고 속도 배율은 `clamp(0.85 + 점수×0.003,0.85,1.15)`다. 실제 작업 시간에는 그 역수가 들어간다. 31개 `WorkType` 가운데 `Operate`, `Research`, `Restock`, `Guard`, `Craft` 5개만 적용된다.

방 시설 선호 점수는 사용할 수 있는 방이면 같은 환경 점수를 8~28 범위로 투영한다. 방 역할이 필요한 시설인데 사용할 수 있는 방이 없으면 -15다. `WorkTargetSelector`가 이를 실제 대상 선택에 사용하고 `AbilityWork`가 작업 시간 배율을 읽는다.

작업 완료, 시설 이용 완료, 쇼핑 완료는 `RoomEnvironmentExperienceService`로 방 경험을 적용한다. 인상 기분은 `<20:-6`, `<40:-3`, `<60:0`, `<80:+3`, `그 이상:+6`; 청결 기분은 `<20:-4`, `<40:-2`, `<80:0`, `그 이상:+2`이며 지속시간은180초다.

## 3. 역할13종과 유효성·수용력

고정 bit 역할은 식사, 상점, 휴식, 훈련, 연구, 마나, 물류, 화장실, 위생, 집무, 경비, 흥행, 의료의13종이다. 역할이 필요한 시설은 방 없음, 사용할 수 없는 방, 역할 불일치일 때 거부된다.

기본 수용력은 그대로 유지하되 사용할 수 있는 비-self-contained 방에서 식사 역할은 `max(base,min(seats,tables))`, 상점 역할은 `max(base,base+serviceCapacity)`를 쓴다. `BuildableObject.EffectiveCapacity`가 어댑터를 통해 이 결과를 소비한다.

현재 비deprecated 테이블 부품은 D05 소형식탁(id1004, table capacity2)과 D06 대형연회식탁(id1005, capacity6) 두 개다. 같은 일반 방의 table capacity는 부품별 값을 합산한다. 공개 두 시설 페이지는 facts에 분류·크기만 두고 summary에 raw `BuildingTableAbility` 클래스명만 노출하므로 실제 이용 인원2/6과 식사 수용력 기여를 알 수 없다. 테이블만 늘려도 좌석 합계가 낮으면 `min(seats,tables)`에서 제한되며, 반대로 좌석만 늘려도 같은 제한을 받는다. self-contained/무효 방에서는 이 보너스를 적용하지 않고 base capacity를 유지한다.

## 4. 문화10종의 방 선호와 실제 시설 점수

role bit는 `Meal=1`, `Purchase=2`, `Rest=4`, `Training=8`, `Research=16`, `Mana=32`, `Logistics=64`, `Toilet=128`, `Hygiene=256`, `Administration=512`, `Security=1024`, `Entertainment=2048`, `Medical=4096`이다.

| 문화 | 선호 역할 | 이상온도±허용 | 최소환기 | 조명범위 | 최소청결 | 공간 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| adventurer-frontier | 식사+휴식 | 20±10 | 45 | 55~100 | 40 | 공유 |
| beastkin-pack | 식사+휴식 | 24±8 | 35 | 20~80 | 30 | 공유 |
| demon-contract | 마나+집무 | 27±8 | 35 | 20~70 | 40 | 개인 |
| golem-core | 마나+물류 | 18±15 | 50 | 30~90 | 60 | 개인 |
| harpy-aerie | 휴식 | 18±10 | 70 | 50~100 | 35 | 공유 |
| kobold-toolclan | 훈련+물류 | 24±7 | 50 | 45~100 | 40 | 공유 |
| myconid-grove | 식사+휴식 | 20±5 | 45 | 5~35 | 40 | 공유 |
| orc-vigil | 식사+훈련 | 20±10 | 40 | 25~85 | 30 | 공유 |
| slime-confluence | 휴식+위생 | 22±5 | 40 | 20~70 | 70 | 공유 |
| vampire-nightcourt | 휴식+집무 | 18±7 | 40 | 0~30 | 55 | 개인 |

`FacilityCandidateScorer`의 생산 경로는 `FacilityScoringContext.GetCulturePreferenceBias`를 거쳐 `CharacterCultureGameplayRuntime.GetFacilityUtilityBias`를 호출한다.

- 선호 역할 +0.08, 선호 시설 stable ID +0.12
- 사용할 수 있는 방이 없으면 현재 합계에서 -0.08 뒤 -0.2~0.2 제한
- 온도·환기·청결·조명 점수 평균을 `((평균)-0.5)×0.16`으로 가감
- 공유 선호는 `area≥12 && freeCells≥3`, 개인 선호는 `area≤8 && doorCount>0`; 충족 +0.05, 불충족 -0.05
- 최종 점수는 -0.2~0.2 제한

문화 stable ID10개는 공개 entity 디렉터리에서 모두0건이며 `omitted-relations.json`의 source로만 남아 있다. 따라서 이는 방 공통 공식과 별개인 개체별 공개 데이터 누락이다.

## 5. 문 출입 정책과 저장

출입 집단은 주인, 직원, 손님, 포로, 침입자, 야생동물, 포획 야생동물7종이다. 개별 persistent ID에는 집단 기본/허용/거부를 덮어쓸 수 있고, 명시 거부가 허용보다 우선한다.

프리셋은 모두 허용, 직원 전용, 손님 구역, 감방, 동물 우리의5종이다. UI는 집단 토글, 개별 허용/거부, 정책 복사·붙여넣기, 같은 방의 모든 문에 적용을 제공한다. `DoorAccessStateModule`은 module ID `door.access`, version1로 allowedGroups와 개별 허용/거부 목록을 JSON 저장·복원한다. 이동 직전 `DoorAccessUnityAdapter.CanTraverse`가 현재 주체와 정책을 검사한다.

현재 `Door`와 `DoorAccessPolicyState`에는 플레이어가 여닫는 open/closed 물리 상태가 없고, 문 칸 통과는 출입 정책으로 허용·거부된다. 따라서 `spaces.md`의 “완공된 벽과 닫힌 문은 이동을 막는다”는 표현은 현재 확인한 구현과 맞지 않는다. 이는 문 정책 전체가 없다는 판정이 아니라 물리 개폐 설명만의 차이다.

## 6. 방 condition·특성 반응·마나 노출

시설 휴식의 need 회복은 현재 방 condition ID를 함께 전달한다. 같은 방의 다른 생산/제작 시설 또는 훈련·마나 역할 시설이 있으면 `room:noise`, 면적8이하이며 문이 있으면 `room:private`다.

- 얕은 잠(`trait:201`)은 `room:noise`에서 수면 회복×0.8, `sleep:noisy` 결과에 기분-3/1일이다.
- 과묵함(`trait:222`)은 `rest:private` event에 개인 휴식 기분+3/1일을 작성한다. 같은 자산의 행동 선호+0.75는 이 방 condition 호출 대조만으로 생산 소비까지 확정하지 않는다.
- 청소 강박(`trait:245`)은 `room:dirty` -4/1일, `room:cleaned` +2/1일을 작성한다. 같은 자산의 청소/비청소 작업 배율은 별도 작업 특성 범위다.
- 청결 변화 event는 주민×방의 직전 관찰값과 현재값을 비교한다. 이 직전값은 `RoomEnvironmentExperienceService`의 메모리 dictionary이고 별도 Capture/Restore가 없어 로드 뒤 첫 관찰은 새 기준점이 된다.

마나 역할 시설 경험은 사용할 수 있는 방인지 검사하기 전 `disease:mana-pox`의 ManaExposure를 작업8시간, 시설 이용2시간, 기타 활동1시간으로 발행한다. `PopulationHealthRuntime`이 이 event를 구독해 실제 노출 상태에 넣고 유전 특성의 mana-overload 배율을 환경 계수로 사용한다. 이 생산자는 기존 질병 노출 GAP-022 범위에 합치며 새 누락으로 중복 계수하지 않는다.

## 판정

- GAP-152 후보: 방 점수·작업5종·시설선호·기분 수치와 호출 경로
- GAP-153 후보: 역할13종, 방 유효성, 역할 거부, 식사/상점 수용력
- GAP-154 후보: 문 집단7종·프리셋5종·개별 예외·일괄 적용·저장
- GAP-155 후보: 문화10종 방 선호 필드와 시설 점수 공식, 공개 culture entity0건
- GAP-156 후보: `room:noise/private/cleaned/dirty`의 판정, 특성201/222/245 효과, 청결 관찰 기준의 저장 경계
- DIFF-045 후보: “닫힌 문” 물리 차단 설명과 정책 기반 통과 구현의 차이

다른 방/환경 시스템 전체, 실제 UI 상호작용, 저장 왕복, 층간 방 검출은 아직 완료로 판정하지 않는다.
