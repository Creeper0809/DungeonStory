# 외부 휴식처의 Rest gate·기분·원정 스트레스 회복 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- `BuildingOutdoorRestAbility`는 플레이어가 설치하는 일반 시설이 아니라 ExteriorActivityRuntime이 자동 배치하는 `OutdoorRestSpot` runtime zone의 ability다.
- Rest work 완료는 기분 factor와 원정 스트레스만 바꾸며, 수면·허기·위생을 회복하거나 zone readiness를 올리지 않는다.
- GAP-164는 zone 생성·마모·작업·저장을 소유하지만 Rest eligibility와 ability의 정확한 수치/효과를 문서화하지 않는다. 공개 가이드에도 이 계약은 없다.

## 생성·작업 가능 조건

- runtime archetype builder는 OutdoorRestSpot에 default `BuildingOutdoorRestAbility`와 `BuildingExteriorMaintenanceAbility`를 함께 붙이고, 지원 work type은 Rest와 Clean이다.
- Rest ability의 authored default는 workSeconds1.4, moodBonus+4, stressRecovery8, moodDurationSeconds180이다.
- Exterior work는 `work:rest` 한 종류다. target이 `ExteriorZoneMarker`이고 실제 OutdoorRestSpot이어야 한다.
- 작업자는 Mood가85 미만이거나 ExpeditionStress가 0 초과일 때만 후보가 된다. urgency는 `clamp((85-Mood)×0.75 + ExpeditionStress×0.45, 15, 80)`이다.
- Rest 시간은 `max(0.1, workSeconds)`이므로 default1.4초다.

## 완료 효과·기록·비효과

- core handler는 `exterior:rest:<zoneId>` mood factor를 `+4`, 180초, stack1로 적용하고 `ApplyExpeditionRecovery(0,0,8)`을 호출한다.
- 따라서 이 work는 피로·수면 recovery 인자에는0을 넘기고 ExpeditionStress만8 회복한다. ordinary need recovery나 room 휴식과 동일한 효과로 설명하면 안 된다.
- zone의 `RecordOutdoorRest`는 `completedWorks`만1 증가시킨다. 청결·손상·순찰·응대 readiness는 바꾸지 않는다.
- activity는 `exterior-rest`, quantity moodBonus로 남는다. work type이 다르거나 target이 exterior zone이 아니면 handler는0으로 끝내고 효과를 적용하지 않는다.

## 공개 경계

- 공개 위키는 runtime zone의 일반 공개 문제/자동 배치·마모·rest 업무 존재만 GAP-163/164 범위에서 다룬다.
- infrastructure·residents-and-work·expeditions 가이드는 OutdoorRestSpot의 Mood85/ExpeditionStress gate, urgency, 1.4초 completion, mood+4/180초와 stress8만 회복하는 효과를 설명하지 않는다.

## 정적 검증 경계

- ability·core handler·runtime archetype builder·ExteriorZoneMarker·외부 runtime과 기존 exterior review 및 관련 가이드를 읽었다.
- 자동 zone 배치, AI candidate selection, mood factor 만료, expedition stress 감소, activity 및 save 왕복은 Play Mode에서 재현하지 않았다.
