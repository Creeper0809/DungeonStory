# 음료·기호물질·중독 후속 감사

상태: 정적 대조 완료. Unity 실행·UI 시연·저장 왕복 검증은 수행하지 않았다. 게임 코드·자산·공개 위키는 수정하지 않았다.

## 판정 요약

- `SubstanceItemFeature` 작성 자산은 9개다. 모두 `ResourceItemDefinitionSO`이며 `ItemDefinitionCatalog.asset`에 정확히 한 번 등록되고 공개 item entry도 9/9 존재한다.
- 작성 class는 NonAddictive2, Addictive3, Recreational4, Medicine0이다. 모든 품목에 필요 연구가 있고 범용 복용 런타임과 AI가 실제 연결된다.
- 공개 entry9개는 모두 무게·한 칸 적재·기준 가격만 표시하고 relations도0이다. class, 필요 연구, 중독·과다복용 확률, 내성·금단, 기분/작업/전투 효과와 지속시간을 표시하지 않는다.
- 주민 건강 UI는 품목 선택과 정책 순환, 활성시간·내성·중독·금단 상태 표시를 제공한다. 다만 기분 문턱30과 예약 시각20을 직접 조절하는 production UI 호출자는 찾지 못했다.
- 몽엽 진통제의 `response:dreamless-sedative` 질병 대응은 별도 물리 sink·중증도22 감소 경로다. 비용·효과는 기존 GAP-020이 소유하므로 신규 누락 수에 더하지 않는다.

## 작성 물질 9종

| item ID | class | 중독 / 과다복용 | 내성 / 시간당 금단 | 기분 / 작업 / 전투 | 지속(s) | 필요 연구 |
| --- | --- | --- | --- | --- | ---: | --- |
| drug:blood-stimulant | Addictive | 0.14 / 0.06 | 0.18 / 0.03 | +1 / +0.16 / +0.20 | 150 | research:pharmacology:stimulants |
| drug:dreamleaf-analgesic | Addictive | 0.08 / 0.02 | 0.12 / 0.02 | +4 / +0.05 / -0.03 | 240 | research:pharmacology:anesthesia |
| drug:hallucinogenic-distillate | Recreational | 0.09 / 0.04 | 0.13 / 0.025 | +10 / -0.12 / -0.08 | 210 | research:pharmacology:distillation |
| drug:mana-awakener | Addictive | 0.11 / 0.05 | 0.16 / 0.025 | +2 / +0.18 / +0.08 | 180 | research:pharmacology:stimulants |
| drug:moonflower-tea | NonAddictive | 0 / 0.002 | 0 / 0 | +3 / +0.04 / 0 | 180 | research:pharmacology:herbalism |
| drug:night-wine | Recreational | 0.04 / 0.025 | 0.08 / 0.015 | +7 / -0.04 / -0.04 | 240 | research:cuisine:fermentation |
| drug:vitality-tonic | NonAddictive | 0 / 0.006 | 0 / 0 | +2 / +0.12 / 0 | 150 | research:pharmacology:distillation |
| food:night-spirit | Recreational | 0.07 / 0.045 | 0.12 / 0.025 | +9 / -0.08 / -0.06 | 240 | research:cuisine:distilling-aging |
| food:twilight-beer | Recreational | 0.025 / 0.015 | 0.05 / 0.008 | +5 / -0.02 / -0.02 | 180 | research:cuisine:fermentation |

`food-and-ecology.md`는 마지막 두 주류만 가공 식품과 음료 표에 나열한다. 나머지7개 약물은 공개 item 검색에는 나오지만 주민 복용·정책 안내에서 찾을 수 없다.

## 정책과 자동 사용

- 신규 기본 정책은 Medicine→MedicalOnly, NonAddictive/Recreational→MoodThreshold, Addictive→Forbidden이다. 유효하지 않은 정의도 Forbidden이다.
- 정책은 Forbidden, MedicalOnly, CombatOnly, MoodThreshold, Scheduled 다섯 모드다. 주민 건강 UI의 버튼은 이 enum 순서로 순환하고 현재 문턱·시각을 보존한다.
- 기본 기분 문턱은30, 예약 시각은20시다. 저장·명령 범위는 문턱0~100, 시각0~23으로 clamp한다.
- 체력82% 미만을 medical context로 본다. MedicalOnly urgency는 손실 체력에 따라0.62~1, CombatOnly는 전투태세에서0.96, MoodThreshold는0.55~0.9, Scheduled는 지정 시각 이후와 cooldown0에서0.48이다.
- addicted이고 withdrawal20 이상이면 기존 정책 urgency에0.7~1을 덮어올린다. 활성 효과가 남은 같은 물질은 자동 후보에서 제외한다.
- `AISubstanceUse`는 행동 우선순위72, 최소0.25초, 생존 비상 중단 불가다. Recreational은 거리→시설ID 순 술음료장을 방문하고, 다른 class는 실물1개를 개인 소비 lease로 확보·운반한 뒤 복용한다.

## 사용 결과·중독·금단

- 과다복용 확률은 `clamp01(overdoseChance×(1+tolerance/100))`다.
- 사용 뒤 내성은 `clamp(tolerance+toleranceGain,0,100)`, 중독 점수는 `clamp(addiction+addictionChance×100×(0.65+tolerance/100),0,100)`이다.
- 기존 중독을 유지하고, 점수60 이상 또는 별도 `addictionChance×0.2` 추첨 성공이면 addicted가 된다.
- Scheduled 사용은 24 game-hour cooldown을 둔다. 이 런타임의 1 game-hour는60초다.
- 기분 효과는 `moodEffect×(1-0.55×tolerance/100)`로 duration 동안 적용한다. overdose는 `max(4, maxHP×0.12)` 피해와 기분-12/300초를 추가한다.
- 활성 효과 종료 뒤 내성은 초당0.004 감소한다. 마지막 복용1 game-hour 이후 addicted 상태의 금단은 `withdrawalPerHour×delta/60`으로0~100까지 증가한다.
- 금단20 이상은 기분 `-lerp(3,14,withdrawal/100)`를 2초씩 갱신한다. 작업·전투 배율은 각각 활성 저작 효과를 합산하고 모든 금단 점수×0.0025를 뺀 `clamp(1+active-withdrawal,0.45,1.75)`다.

## 시설·물류·저장

- Recreational 후보는 policy 허용 품목만 `moodEffect-20×addictionChance-30×overdoseChance`로 정렬하고 item ID, stack ID로 동률을 푼다.
- 술음료장은 해당 facility/item 목적지 buffer를 먼저 소비한다. 없으면 stored/loose 1개 배송을 요청하고 같은 목적지로 routed item이 있으면 DeliveryPending을 유지하며 운반자1명 재계획을 요청한다.
- 술음료장 작성값은 수용1, 사용1.5초, fun+8, 시설 sentiment+0.25다. 물질 소비 실패 시 서비스와 활동도 실패하고 fun을 주지 않는다. 성공하되 overdose면 활동 sentiment는 -0.5다.
- 물질1개를 pending physical sink한 뒤 active plan에 resolved 상태와 영수증을 기록한다. `ItemCommitted→EffectsPublished→physical ack` 순서이며 ack 실패 재시도에서 효과를 반복하지 않는다.
- consumables save v8은 주민별 substance policy/state, active substance plan, completed operation과 물리 commit 정보를 저장한다. package가 있는 품목의 empty tare는 terminal sink 출력으로 복구한다.

## 공개 설명과 typed 효과 차이

품목 stable ID의 비Editor 소비를 검색하면 몽엽 진통제의 질병 대응 외에는 품목별 분기가 없다. 범용 복용은 기분과 모든 작업/전투 배율만 제공한다. 따라서 다음 문구는 현재 typed 효과보다 구체적이다.

| 공개 품목 | 공개 설명 | 실제 범용 복용 |
| --- | --- | --- |
| 활력 강장제 | 피로를 줄인다 | 기분+2, 모든 작업+0.12, 피로/수면 need 직접 변경 없음 |
| 마나 각성제 | 연구와 비전 감각을 증폭한다 | 기분+2, 모든 작업+0.18, 모든 전투+0.08, 연구/비전 전용 분기 없음 |
| 몽엽 진통제 | 통증을 크게 낮춘다 | 기분+4, 모든 작업+0.05, 모든 전투-0.03. 별도 질병 대응은 중증도22 감소지만 일반 복용의 통증 상태 변경은 없음 |

이 세 문구를 DIFF-036 한 행으로 묶는다. 서사적 이름 하나마다 독립 버그로 세지 않으며, 향후 typed 효과 또는 문구 정정 중 어느 쪽이 권위인지는 설계 판단이 필요하다.

## 근거 범위

- 작성/공개: `ItemDefinitionSO.cs`, substance 자산9개, `ItemDefinitionCatalog.asset`, 공개 item entry9개, `food-and-ecology.md`, `residents-and-work.md`.
- 실행/저장: `CharacterConsumablesRuntime`과 contracts/state/persistence, `SubstanceDefinitionView`, `AbilityUseSubstance`, `AISubstanceUse`, `CharacterSummaryHealthPresenter`, `Facility`, `CharacterBuildingVisitorPort`, `CharacterConsumablesInputOwnerRuntime`, `CharacterConsumablesSaveSection`.
- KB query `drink beverage substance alcohol addiction withdrawal overdose intoxication thirst`는 stale2429·반환0행이었다. 생성물을 재빌드하지 않고 원본과 공개 문서를 직접 대조했다.

