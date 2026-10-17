# 세정대 수술시설 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingSterilizationAbility` 작성 시설은 M04 세정대(id9504) 한 개다.
- M04는 단독 주시설이 아닌 같은 usable room support다. Sterilization tag, 성공·무균 보정과 수술 주문 재료를 제공한다.
- 공개 M04 페이지와 의료 가이드는 시설별 tag/bonus/재료값, tag 요구와 실제 재료 추가 범위, 주문 생성 시점과 실행 재평가 시점의 차이를 설명하지 않는다.
- GAP-118은 절차별 요구 시설과 조건부 추가 재료, GAP-119는 공통 위험식을 소유한다. M04 시설 계약은 GAP-180으로 분리한다.

## 작성값과 활성 판정

| 필드 | 작성값 | 현재 실행 |
| --- | ---: | --- |
| sterilityBonus | 0.3 | Sterility+0.3, SuccessBonus+0.024 |
| waterCost | 1 | `resource:clean-water` 1개 추가 |
| disinfectantCost | 1 | `medicine:disinfectant` 1개 추가 |
| primary | false | 단독 주시설 불가, 같은 방 support |
| speed / anesthesia | 1 / 0 | 속도·마취 추가보정 없음 |

- 같은 usable room의 비파괴·비손상 수술시설 abilities는 모두 합산된다. tag는 OR, sterility/success는 합산한 뒤 각각 0~1, -0.25~0.35로 제한한다.
- M04가 여러 개 있으면 각 M04의 sterility/success와 깨끗한 물·소독제 비용이 각각 누적된다. 재료 요구는 item ID별로 병합된다.

## 절차·재료·위험 연결

- 현재 수술 절차47개 중 Sterilization bit를 요구하는 절차는31개다.
- `BuildMaterials`는 절차의 Sterilization tag 요구 여부를 검사하지 않고 선택된 snapshot의 모든 support와 primary에서 M04를 찾는다. 따라서 tag가 필요 없는 나머지16개 수술도 M04가 같은 방에 있으면 깨끗한 물1·소독제1을 추가로 요구한다.
- M04 한 개의 SuccessBonus0.024는 시설 성공 기여에 직접 들어가고 Sterility0.3은 `sterility×0.08`로 성공률에 다시 +0.024를 제공한다. 다른 입력과 최종0.05~0.98 제한 전 두 성공 입력 합은 +0.048이다.
- 감염식에는 `-sterility×0.45`가 들어가므로 M04 한 개는 다른 입력과 최종 clamp 전 -0.135 입력을 제공한다.
- 추가 재료는 주문 생성 시 `order.materials`에 고정되어 물리 목적지로 운반·소비되며 저장 복제에도 포함된다.

## 시점·UI·공개 위키 경계

- 주문은 primary facility ID와 생성 당시 계산한 재료 목록을 저장하지만 M04 support ID 목록은 별도 저장하지 않는다.
- 실행 중 시설 tag와 위험 보정은 현재 방 snapshot을 다시 평가한다. 주문 생성 뒤 M04를 철거해도 이미 고정된 물·소독제 요구는 남을 수 있고, 반대로 뒤늦게 M04를 추가하면 실행 보정은 받을 수 있어도 기존 주문 재료 목록은 다시 계산되지 않는다.
- 수술 UI는 주시설 이름, required tags, Work와 최종 위험 확률을 표시하지만 M04 support, 시설별 bonus 분해와 주문 재료 목록은 표시하지 않는다.
- 공개 building-9504 facts는 분류·크기뿐이고 summary는 내부 공정 재고와 건설비만 설명한다.

## 정적 검증 경계

- ability/M04 자산,47개 절차의 required tag, 시설 query, 재료 계획·주문 저장·물리 소비, 위험식, UI와 공개 페이지/가이드를 읽었다.
- 실제 M04 배치, 다중 M04 누적,31개 tag 요구와16개 비요구 절차의 운반·소비, 주문 후 support 변경 및 저장 왕복은 Play Mode에서 재현하지 않았다.
