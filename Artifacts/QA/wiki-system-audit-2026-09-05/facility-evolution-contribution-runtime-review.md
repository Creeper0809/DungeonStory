# 시설 진화 기여 127자산의 room profile·환경 소비 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- `BuildingEvolutionAbility`는 작성 시설127개에 있으며, 그 중125개만 `contributesToRoomProfile=true`로 같은 room profile에 실제 기여한다.
- 활성 기여는 tag·score·metric을 별도로 누적한다. 일반 “진화 가능” 표시는 해당 시설이 무엇을 어느 경로로 기여하는지 설명하지 않는다.
- E09 방 표지판은 빈 contribution, E12 환기 덕트는 Ventilation tag/score2를 작성했지만 `contributesToRoomProfile=false`라 tag·score·metric이 모두 room profile에 들어가지 않는다.

## 작성 census

| 항목 | 결과 |
| --- | ---: |
| BuildingEvolutionAbility 작성 자산 | 127 |
| room profile 기여 활성 | 125 |
| room profile 기여 비활성 | 2 |
| score key 종류 | 48 |
| metric key 종류 | 5 |

- 가장 많이 작성된 활성 tag는 Production25, Luxury18, Combat16, Storage14, medical/surgery 각13, Mana/Service 각12, Hygiene/Dining 각10, Security9, Research8, Defense/Cooking/Rest 각7이다.
- score 누계의 주요값은 Dining180, Luxury125, Combat114, Cooking96, Training90, Service77.5, Meat70, Rest64, Mana62, Production50, Defense44, Storage35, Hygiene30이다.
- 작성 metric은 CounterCount2, LargeTableCount3, PrivateSeatCount6, SeatCount23, TableCount6의 다섯 key뿐이다.

## profile 작성과 소비 경계

- room profile은 대상 시설과 같은 room cell을 점유한 파괴되지 않은 모든 building을 모아, 활성 contribution의 tag를 set에 추가하고 score/metric은 key별 합산한다. facility role과 defense contribution도 별도로 더한다.
- facility evolution recipe validation은 usable room, required tag, required room score, required room metric, record token, identity pressure, unique fixture와 resource를 각기 검사한다. 따라서 tag가 작성되었다고 모든 recipe가 자동 충족되는 것이 아니라 recipe가 해당 key를 요구할 때만 gate가 된다.
- 현재 environment adapter가 evolution score에서 직접 읽는 것은 Luxury와 Hygiene뿐이다. 나머지 score/tag/metric을 일상 환경 보정이나 일반 생산 multiplier로 설명하면 안 된다.
- Luxury/Hygiene는 fixtures의 활성 score를 합산한 뒤 room environment의 luxury/hygiene 입력에 들어간다. field snapshot이 존재하는 온도·환기·조도와는 별도 계산 경로다.

## 공개 경계

- facility-growth는 여섯 recipe의 조건과 mutation 선택을 설명하지만 127개 authored facility contribution의 활성/비활성, key별 누적, recipe validation과 environment의 서로 다른 소비 경계를 설명하지 않는다.
- 개별 facility pages는 분류·크기·포괄 역할 위주라 evolution tag·score·metric 기여를 표시하지 않는다. E12의 작성값이 room profile에 실제 반영되지 않는 경계도 공개되지 않는다.

## 정적 검증 경계

- 모든 `BuildingEvolutionAbility` 작성 asset127개를 census하고 contribution 정의·RoomProfile·FacilityEvolutionService validation·RoomEnvironmentAdapter, 기존 recipe/mutation 대조와 공개 facility-growth를 읽었다.
- 실제 room 조합의 tag/score/metric 합산, recipe UI validation, environment 수치, 시설 진화 commit와 save 왕복은 Play Mode에서 재현하지 않았다.
