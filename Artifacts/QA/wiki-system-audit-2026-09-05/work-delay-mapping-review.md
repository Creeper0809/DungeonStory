# 31개 업무와 사건 작업 지연 매핑 후속 감사

상태: 정적 원본 대조 완료. Unity Play Mode 재현은 하지 않았으며 게임 코드·작성 자산·공개 위키는 수정하지 않았다.

## 범위와 중복 경계

- 런타임 업무 카탈로그는 `BuiltInWorkTypeIds.All`과 `WorkTypeCatalog.All`의 31개다.
- 공개 `work-references.json`은 10개 숙련군 아래의 정성적 업무 예시 42개이며 런타임 WorkType 31개와 1:1 목록이 아니다. 이 분류 차이는 기존 GAP-030이 소유한다.
- 우선순위와 비상 중단 정책은 기존 GAP-030~033, 사건 지연 공식은 GAP-093, 날씨가 원정 이동에 직접 연결되지 않는 문제는 DIFF-017이 소유한다. 이 보고서는 GAP-093의 정확한 31개 매핑과 새로 확인한 효과 무효만 다룬다.
- 전용 실행 handler가 없는 ID도 `WorkTaskExecutor`의 authored order→exterior→registered handler→legacy 경로를 탈 수 있으므로 handler 등록 수를 실행 가능 업무 수로 오인하지 않는다.

## 런타임 업무 31개

`Operate`, `Restock`, `Construct`, `Repair`, `Clean`, `Research`, `Guard`, `Reception`, `Rescue`, `Rest`, `Craft`, `Haul`, `Hunt`, `Butcher`, `DrawWater`, `Cook`, `Treat`, `Surgery`, `Refuel`, `Warden`, `Perform`, `Gather`, `Sow`, `Harvest`, `Logging`, `Quarry`, `AnimalCare`, `GrandProject`, `ThreatMitigation`, `Plumbing`, `Dismantle`다.

기본 우선순위는 P1 3개(Operate/Surgery/ThreatMitigation), P3 3개(Clean/Guard/Rest), P2 25개다. 비상 분류는 즉시 중단11, 체크포인트 중단14, 비상 대응4, 비중단 수술1, 보호 휴식1이다. 이 수치는 새 GAP으로 세지 않는다.

## WorkDelay 적용 공식과 정확한 매핑

활성 지연 scope 수를 `n`이라 하면 속도 배율은 `max(0.5, 0.8^n)`이다. 따라서 n=0/1/2/3/4에서 1/0.8/0.64/0.512/0.5배다. 같은 scope의 양수는 종료일을 연장해 활성 scope 하나로 유지한다.

| scope 종류 | 현재 31개 업무에 대한 결과 | 비고 |
| --- | --- | --- |
| `global` | 31/31 | 모든 업무 |
| `service-incident:*` | 31/31 | 구체 업무 ID가 아니라 정착지 전체 비용으로 처리 |
| `life-event:*` | 31/31 | 동일 |
| `faction-work:*` | 31/31 | 동일 |
| `flood` | 1/31, `work:haul`만 | 검사 문자열은 farm/crop/agric/haul/logistic/carry지만 현재 ID에는 haul만 존재 |
| `road` | 1/31, `work:haul`만 | expedition/haul/logistic/carry/trade 중 현재 ID에는 haul만 존재 |
| `whiteout` | 1/31, `work:haul`만 | road와 동일 |
| 그 밖의 scope | scope 문자열을 포함하는 WorkType ID | 현재 작성 83행에는 별도 사례 없음 |

즉 해빙수 범람은 설명상 낮은 농지와 운반 통로를 함께 덮치지만 실제 WorkDelay는 `Sow`, `Harvest`, `AnimalCare`가 아니라 `Haul`만 늦춘다. 씻겨나간 길과 백색 암흑도 WorkDelay만 보면 Haul만 늦추며, 원정 이동 자체 미반영은 기존 DIFF-017과 중복 집계하지 않는다.

## 작성 효과 83행 전수 집계

첫 `targetId`와 `amount`에서 즉시 중단해 각 `kind: 13` 블록을 집계했다. 총 83행이고 target도 83개 모두 고유하며 음수는 1행이다.

| 작성 계열 | 행 수 | 현재 매핑 |
| --- | ---: | --- |
| 세력 장 `faction-work:*` | 72 | 각 효과가 31개 전체를 감속 |
| 서비스 사고 `service-incident:*` | 5 | 31개 전체 |
| 생애 사건 `life-event:*` | 3 | 양수2는 31개 전체, 음수1은 아래의 no-op |
| 계절 `flood`/`road`/`whiteout` | 3 | 각각 Haul만 |

초기 집계에서 faction chapter3의 다음 효과 `targetId`까지 덮어써 12행이 `faction:dungeon:*`를 대상으로 한다고 잘못 읽었으나, 원본 재검증 결과 support/bargain 지연 72행은 모두 `faction-work:*`다. 해당 불일치 후보는 폐기했다.

## 음수 지연과 은퇴 요청

음수 지연은 새 음수 상태나 작업 가속을 만들지 않는다. 동일 scope의 기존 지연이 있을 때만 종료일을 당기며, 동일 scope가 없으면 `global`일 때에만 다른 모든 지연을 줄인다.

`life-event:retirement-request`의 둘째 선택 “한 계절 더 현장을 부탁한다”는 같은 scope에 `-2`를 적용한다. 작성 콘텐츠 전체에서 이 scope의 양수 지연은 없고 이 생애 사건은 인물별 1회이므로 정상 작성 경로에서는 줄일 기존 상태가 없어 효과가 없다. 공개 엔티티는 이를 `adds-work-delay -2` 관계로 노출하므로 실제로 지연을 줄이거나 속도를 높이는 효과처럼 읽힐 수 있다.

## 판정

- GAP-093을 정확한 31개 매핑과 83행 작성 모집단으로 보강한다. 새 문서 누락 행은 추가하지 않는다.
- DIFF-043: 해빙수 범람의 농지 영향 설명과 실제 Haul-only WorkDelay 범위 차이.
- DIFF-044: 은퇴 요청 선택의 `-2` WorkDelay가 정상 작성 경로에서 no-op인 차이.
- faction chapter3의 target 불일치 후보는 집계 오탐이므로 등록하지 않는다.

## 직접 원본

- `Assets/Scripts/Models/Work/WorkTypeId.cs`
- `Assets/Scripts/Models/Work/WorkTypeCatalog.cs`
- `Assets/Scripts/Services/Run/V20CampaignRuntime.cs`
- `Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs`
- `Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-thaw-flood.asset`
- `Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-washed-road.asset`
- `Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-whiteout.asset`
- `Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_retirement-request.asset`
- `Assets/Scripts/Services/Factions/Editor/V20FactionServiceContentAssetBuilder.cs`
- `wiki/game-versions/0.0.1v/content/work-references.json`
- `wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-thaw-flood.json`
- `wiki/game-versions/0.0.1v/data/entities/event/life-event-retirement-request.json`
