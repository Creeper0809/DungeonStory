# 마취 장치 수술시설 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingAnesthesiaAbility` 작성 시설은 M05 마취 장치(id9505) 한 개다.
- M05는 단독 주시설이 아닌 같은 usable room support다. Anesthesia tag와 성공·마취 보정을 제공하지만 작성된 마취제 ID/비용은 현재 재료 계획에서 사용되지 않는다.
- 공개 M05 페이지와 의료 가이드에는 시설별 tag/bonus, 태그 요구와 마취제 요구의 차이, 작성 비용 필드의 비소비 경계가 없다.
- GAP-118은 절차별 마취/시설 필드, GAP-119는 공통 위험식을 소유한다. M05 시설 계약은 GAP-179로 분리한다.

## 작성값과 활성 판정

| 필드 | 작성값 | 현재 실행 |
| --- | --- | --- |
| stabilityBonus | 0.35 | AnesthesiaBonus0.35, SuccessBonus0.0105 |
| anesthesiaItemId | medicine:anesthetic | 시설 ability에서는 미소비 |
| anesthesiaCost | 1 | 시설 ability에서는 미소비 |
| primary | false | 단독 주시설 불가, 같은 방 support |
| speed / sterility | 1 / 0 | 속도·무균 추가보정 없음 |

수술 재료 계획은 시설 ability의 ID/cost가 아니라 procedure가 마취필수이거나 환자가 비동의일 때 고정 `SurgeryItemDefinitions.AnestheticId` 1개를 추가한다.

## 절차 모집단과 위험 연결

- 현재 `requiresAnesthesia=true` 절차는11개다.
- 그중 Anesthesia facility tag까지 요구하는 절차는8개: 절단, 이형 개조, 임플란트 설치, 생체 장기 적출, 자연 장기 이식, 장기 치료, 보철 설치, 이종 이식이다.
- 이물 제거, 장기 재생, 전신 재생3개는 마취필수지만 required tags에 Anesthesia bit가 없다. 이 세 절차도 마취제1개를 요구하고, M05가 같은 방에 있으면 anesthesia bonus는 받을 수 있지만 M05 부재만으로 시설이 거부되지는 않는다.
- M05 SuccessBonus0.0105는 procedure의 마취필수 여부와 관계없이 facility contribution에 더해진다.
- 마취필수 절차는 추가로 `AnesthesiaBonus×0.03`을 성공식에 넣으므로 M05 단독 기여가 다시 +0.0105다. 다른 입력이 같다면 마취필수 절차에 대한 M05의 두 성공 입력 합은 +0.021이며 최종0.05~0.98 clamp 전 값이다.
- 마취비필수 절차는 anesthesia contribution이 고정+0.03이고 M05 AnesthesiaBonus를 쓰지 않지만, M05 SuccessBonus+0.0105는 계속 적용된다.
- 같은 방 시설 tag는 OR, bonus는 합산된다. 주시설 후보는 success→sterility→persistent ID 순으로 정렬된다.

## UI·저장 경계

- 수술 UI는 주시설 이름, required tags, Work와 최종 위험 확률을 표시하지만 M05 support 존재, stability0.35, 성공 기여 분해를 표시하지 않는다.
- 수술 order는 계산된 material 요구와 위험, 선택 primary facility ID를 저장한다. M05 support 자체를 별도 필드로 고정하지 않고 실행 중 현재 room snapshot을 다시 평가한다.
- M05의 FuelConsumer/refuel 효과는 GAP-174 범위이며 여기서 중복하지 않는다.

## 정적 검증 경계

- ability/M05 자산,47개 절차의 tags·requiresAnesthesia 전수, 시설 query, 재료 계획, 위험식, UI와 공개 페이지/가이드를 읽었다.
- 실제 M05 배치,11개 절차의 마취제 배송·소비, 성공률 변화, support 철거와 저장 왕복은 Play Mode에서 재현하지 않았다.
