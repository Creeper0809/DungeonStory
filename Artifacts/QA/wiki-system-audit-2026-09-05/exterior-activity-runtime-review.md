# 외부 활동 구역·사건 런타임 대조

조사일: 2026-09-06  
범위: 외부 zone 8종의 생성·마모·업무·원정 이동, 외부 사건 6종의 발생·결과·UI·저장, 공개 가이드  
판정 범위: 정적 원본·composition·UI consumer·생성 위키 대조. Unity Play Mode와 저장 왕복은 실행하지 않았다.

## 결론

- 외부 활동은 일반 시설 도감의 내부 runtime archetype 42개와 별개의 실제 플레이 시스템이다. 운영 화면이 구역 상태와 사건을 표시하고 상인 마차 구매를 실행한다.
- 구역은 런타임에 자동 배치되고 20초마다 청결·손상·순찰·응대가 마모된다. reception/guard/rest/clean/repair 업무, 원정 출발·귀환 이동, save v3 복원이 이 상태에 연결된다.
- 사건은 180초마다 환경·야간·순찰을 반영해 검사하고 6개 handler 중 하나를 고른다. 거래·정찰·절도·구조·포식자·화물 손상이 실제 금화, 물건, 방문자, 야생동물, 지역 상태를 바꾼다.
- 공개 가이드는 일반 사건·손님·원정·거래·운반 원칙만 설명한다. 이 구역 상태기와 사건 종류·수치·물리 결과를 소유하는 문서는 없다.
- 공개 내부 entity 문제는 GAP-163, 실제 구역 운영 설명은 GAP-164, 실제 외부 사건 설명은 GAP-165로 분리한다.

## 외부 구역 운영

### 생성과 역할

scene marker가 없으면 입구와 하차장·외부 경로 후보를 거리, y, x 안정 순서로 골라 다음 구역을 자동 배치한다.

| 구역 | 핵심 역할 |
| --- | --- |
| Entrance | 방문자·원정 출입 경계 |
| DropZone | 물품 하차, 청소·수리, 절도·화물 손상 대상 |
| ReceptionPoint | 방문자 응대, 첫인상과 기분 |
| GuardPost | 외부 경비·수리 |
| PatrolPoint | 순찰 준비 |
| OutdoorRestSpot | 외부 휴식·청소 |
| ExpeditionStaging | 원정대 집결 |
| IncidentPoint | 응대·경비와 일반 외부 사건 |

runtime은 빈 marker layer를 고르고 음수 ID·unlocked=false archetype으로 `ExteriorZoneMarker`를 직접 생성한다. 플레이어가 일반 건설 메뉴에서 40개 layer 변형을 짓는 구조가 아니다.

### 상태와 업무

| 상태 | 초기값 | 20초 마모 | 회복/영향 |
| --- | ---: | ---: | --- |
| 청결 | 100 | -0.7 | 75 미만부터 clean 업무, ability 작성 gain 적용 |
| 손상 | 0 | +0.08 | 0.01 초과부터 repair 업무, 작성 reduction 적용 |
| 순찰 준비 | 45 | -0.4 | guard 업무로 회복, 사건 발생률·종류·일부 결과에 영향 |
| 응대 준비 | 55 | -0.25 | reception 업무로 회복, 방문자 진행·기분·상인 구매에 영향 |

- 첫인상 보너스는 reception 업무로 증가하며 상한25다.
- 청소 urgency는 `clamp((75-cleanliness)×1.3, 0, 80)`, 수리는 `clamp(30+damage×0.75, 0, 95)`다.
- 운영 UI는 구역/하차장/사건 수, 평균 청결·순찰과 최대8개 구역의 위치·청결·손상·사건을 보여 준다.

### 원정 출입

- 출발은 ExpeditionStaging을 우선하고 없으면 Entrance, ReceptionPoint 순으로 대체한다.
- 생존 대원은 집결지로 이동해 `PreparingExpedition`, 입구 grid→door→outside를 지나 `DepartingExpedition`, 그 뒤 expedition 상태가 된다.
- 귀환 생존자는 outside→door→grid로 이동한 뒤 Active로 돌아온다. downed이면 입구 밖 walkable cell에 두고 구조·의료 명령을 알린다.

### 저장

save v3는 zone ID, building instance ID, 종류·좌표, 청결·손상·순찰·응대, 대기 방문자, 첫인상, 완료 업무와 사건 상태를 보존한다. restore는 candidate build→publish→rollback/complete 경계로 교체한다.

## 외부 사건

### 발생과 선택

활성 사건이 없을 때 180초마다 다음 확률을 검사한다.

```text
발생확률 = clamp(
  0.18 + 야간위험×0.004 + 날씨압력×0.12 - 순찰준비×0.0025,
  0.08,
  0.72)
```

| 사건 | 선택 weight |
| --- | --- |
| 상인 마차 | 0.85 |
| 정보상 | 0.70 |
| 부상 귀환자 | 0.55 |
| 도둑 | `max(0.1, 0.35 + danger×2.8 - patrol×1.35)` |
| 포식자 접근 | `max(0.05, 0.15 + danger×2.5 - patrol×1.1)` |
| 화물 손상 | `max(0.05, 0.1 + danger×1.35 + weather×1.8 - patrol×0.55)` |

경험 pacing은 허용 사건 종류와 최대 동시 외부 문제 수도 제한한다. 6종 handler는 모두 composition root에 singleton interface로 등록된다.

### handler 결과

| 사건 | 시간 | 핵심 실행·결과 |
| --- | ---: | --- |
| MerchantCart | 180초 | 보존식4·목재3·표준약2 exact source를 retained commit. 총 단가×수량의80% 반올림 가격. 응대 뒤 금화 결제→하차장 release, release 실패 환불, timeout source sink |
| Informant | 150초 | 응대 준비×0.004×초 진행, 100에서 국경 교역 지역 정찰+8 |
| Thief | 120초 | 순찰70 이상 즉시 저지. 아니면12초 뒤 반경12 loose item 1개 절도 시도, timeout 때 실제 절도 여부 판정 |
| InjuredReturnee | 120초 | downed visitor를 실제 생성하고 구조·치료 작업 경보, `RescueOrdered` |
| PredatorApproach | 45초 | `shadow_wolf` 실제 개체를 생성해 stalking, 8초 뒤 접근 결과 확정 |
| CargoDamage | 50초 | 하차장 반경3의 최고가 loose stack 운반 우선. 밖으로 옮기면 확보, 아니면 순찰에 따른10~24초 뒤 수량1을 `wild:rot`로 물리 변환 |

사건 상태는 Preparing/Active/Interacting/Resolved/Failed/TimedOut이고 actor IDs, wildlife IDs, exact stack IDs, stolen item/quantity, offer price를 저장한다. 운영 화면은 진행 사건의 종류·단계·남은 시간·가격을 보여 주고 응대 완료 상인 마차에서만 구매 행동을 허용한다.

## 공개 문서 대조

- `events-and-choices.md`는 중요 사건의 일반 원칙과 2~5일 간격을 설명하지만 별도의 180초 외부 검사·확률·6종 handler는 없다.
- `guests-services-and-performance.md`는 입구·접수·대기·절도를 일반 서비스 흐름으로만 설명한다.
- `expeditions.md`는 편성·보급·이동·귀환을 설명하지만 집결지와 입구를 거치는 실제 기지 이동 경계는 없다.
- `economy-and-trade.md`의 일반 외부 구매는 상인 마차 exact cargo·80% 가격·응대 조건과 다르다.
- `inventory-and-carrying.md`는 하차장 반경3 화물 구조/부패 사건을 설명하지 않는다.
- 전체 guide 용어 검색에서 외부 활동, zone8종, 사건6종의 직접 설명은0건이었다.

## 권장 문서 경계

- 외부 활동/인프라 가이드: 구역8종, 자동 생성, 20초 마모, 5개 업무, UI 상태, save v3.
- 원정 가이드: 집결지→입구→바깥 출발과 귀환·downed 구조 경계.
- 사건/손님/경제 가이드: 180초 검사, 환경·순찰 공식, 6종 기한과 실제 결과, 상인 구매 조건.
- 내부 layer별 archetype과 음수 ID는 일반 시설 도감이 아니라 구현 인덱스에만 둔다.

## 직접 확인한 원본

- `Assets/Scripts/Models/Exterior/Core/ExteriorDomain.cs`
- `Assets/Scripts/Models/Buildings/Core/CoreBuildingAbilityHandlers.cs`
- `Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs`
- `Assets/Scripts/Services/Infrastructure/Exterior/ExteriorZoneMarker.cs`
- `Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs`
- `Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs`
- `Assets/Scripts/Views/UI/OperationsFeatureQueryService.cs`
- `Assets/Scripts/Views/UI/OperationsFeatureCommandService.cs`
- `Assets/Scripts/Views/UI/Core/OperationsFeatureSurfacePresenter.cs`
- `wiki/game-versions/0.0.1v/content/guides/events-and-choices.md`
- `wiki/game-versions/0.0.1v/content/guides/expeditions.md`
- `wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md`
- `wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md`
- `wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md`
