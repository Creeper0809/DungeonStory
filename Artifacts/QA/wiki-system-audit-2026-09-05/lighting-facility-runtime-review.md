# 조명 ability 5시설의 환경 조도·시각 clipping 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 `BuildingLightingAbility` 작성 시설은 E01 벽횃불, E02 바닥화로, E03 샹들리에, E07 촛대, I15 전기 아크등 5개다.
- ability의 `intensity`와 `radius`는 장식용 Light2D만이 아니라 환경 field의 실제 조도 source와 room environment의 fallback 조도에도 소비된다.
- 다섯 asset은 모두 settings가 null이므로 같은 default 색·falloff·inner radius ratio·target sorting layers를 쓴다. 공개 페이지는 연료/전력 소비 역할만 보여 이 조도 계약과 fuel/power gating 경계를 누락한다.

## 작성값과 공통 default

| 시설 | facility ID | intensity | radius | 작성된 별도 소비 ability |
| --- | ---: | ---: | ---: | --- |
| E01 벽횃불 | 1064 | 0.75 | 2.8 | FuelConsumer |
| E02 바닥화로 | 1065 | 0.9 | 3.2 | FuelConsumer |
| E03 샹들리에 | 1066 | 1.15 | 4.2 | FuelConsumer |
| E07 촛대 | 1070 | 0.5 | 2.1 | FuelConsumer |
| I15 전기 아크등 | 9824 | 1.2 | 5.5 | PowerConsumer, minimum supply fraction 1 |

- null settings의 공통값은 inner-radius ratio0.35, falloff intensity0.65, color `(1,0.72,0.38,1)`, target layer `DungeonHallway`, `DungeonBackObject`, `DungeonMiddleObject`, `DungeonFrontObject`다.
- visual Light2D outer radius는 authored radius, inner radius는 `max(0.1, radius×0.35)`다. intensity/radius가 0 이하인 ability는 visual setup 대상이 아니다.

## 실제 환경 조도

- 환경 runtime은 `radius=ceil(authored radius)`의 Manhattan 반경을 방문한다. 즉 E01 3, E02 4, E03 5, E07 3, I15 6 cell이다.
- source cell에서 target 조도는 `clamp(intensity×100, 0, 100)`이고, 각 cell은 기존 값과 `target×(1-distance/(radius+1))` 중 큰 값이 된다. source는 additive가 아니라 max merge다.
- 범위 밖, barrier cell, source에서 line-of-effect가 막힌 cell에는 적용하지 않는다. 이 환경 반경은 시각 renderer의 room clipping과 별도다.
- field snapshot을 얻을 수 있으면 방 환경의 조명값은 cell 평균 LightLevel이다. field가 없을 때의 fallback room lighting은 같은 방 fixtures의 `intensity×radius×12` 합에35를 더한 값이다.
- 낮은 조도는 precision threshold 아래에서 visual strain rate를 만들고, 수술 environment snapshot이 LightLevel70 미만이면 Lighting ability가 있는 시설의 Refuel recovery request를 낸다.

## 시각 clipping과 소비 경계

- spawn 시 visual runtime은 child `FacilityRuntimeLight`에 freeform Light2D와 `RoomClippedLight2D`를 만들고 default color/falloff/layers를 설정한다.
- visual shape는 usable이며 self-contained가 아닌 source room 직사각형을 먼저 authored radius로 자른다. 그런 room이 없거나 self-contained면 source cell rect로 대체한다. 구조 version, room ID, light position 변화에서 shape를 다시 만든다.
- 반면 environment source의 power gate는 thermal/air ability의 `requiresPower`만으로 결정한다. Lighting ability는 그 조건에 넣지 않는다. 따라서 I15의 PowerConsumer나 E01/E02/E03/E07의 FuelConsumer는 이 조도 source를 직접 on/off하지 않는다. 소비 ability의 별도 refuel/power 시스템과 `BuildingLightingAbility` 환경 source를 같은 gating으로 설명하면 안 된다.

## 공개·UI 경계

- building-1064/1065/1066/1070은 연료 소비만, building-9824는 전력 소비만 요약하며 facts는 분류·크기뿐이다.
- infrastructure·spaces·environment guides는 조명을 일반 환경 조건으로만 언급하고 다섯 시설의 authored 값, field 산식, visual/field 범위 차이, 소비 ability가 조도 source를 직접 gate하지 않는 현재 경계를 설명하지 않는다.

## 정적 검증 경계

- ability/settings·visual setup·room clipping·environment field·room fallback·수술 recovery request, 다섯 작성 asset, 다섯 공개 entity와 환경/공간 가이드를 읽었다.
- 실제 Light2D renderer shape, wall/door line-of-effect, field diffusion, power/fuel state 변화, 수술 Refuel request UI는 Play Mode에서 재현하지 않았다.
