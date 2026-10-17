# 이식 지원시설 수술·재료·거부반응 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingTransplantSupportAbility` 작성 시설은 M09 순환 이식대(id9509), M10 면역 조절기(id9510), M11 격리 회복 침상(id9511) 세 개다.
- 세 시설은 서로 다른 tag와 수술 보정을 제공하며, M09만 primary가 될 수 있다. 이식 재료 비용과 거부반응 감소의 조건도 서로 다르다.
- 공개 시설 페이지와 의료 가이드는 세 시설의 exact tag/bonus/cost, M10 거부반응 감소와 M09/M11의 미소비 rejection 값을 설명하지 않는다.
- GAP-118은 수술 절차의 조건부 혈액팩·면역억제제 존재만 일반적으로 소유하고 시설별 작성값과 적용 경계는 소유하지 않으므로 GAP-184로 분리한다.

## 작성값과 수술시설 변환

| 시설 | tag / primary | success | sterility | speed | anesthesia | rejection | blood / immunosuppressant |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| M09 순환 이식대 | Transplant / primary | 0.14 | 0.05 | 1.15 | 0.1 | 0.15 | 1 / 0 |
| M10 면역 조절기 | ImmuneControl / support | 0.08 | 0.05 | 1 | 0 | 0.35 | 0 / 1 |
| M11 격리 회복 침상 | IsolationRecovery / support | 0.05 | 0.2 | 1 | 0 | 0.2 | 0 / 0 |

- `SurgicalFacilityQuery`는 primary와 같은 usable room의 비파괴·비손상 수술시설 ability를 모두 합친다. tag는 OR, sterility/success/anesthesia는 합, speed는 곱한 뒤 snapshot clamp를 적용한다.
- 세 ability의 success/sterility는 required tag와 무관하게 같은 방의 어느 수술에도 support로 들어간다. M09도 다른 primary의 support가 될 수 있지만 M10/M11은 단독 primary가 아니다.
- 마취필수 수술에서 한 대씩의 성공식 clamp 전 입력은 M09 `0.14+0.05×0.08+0.1×0.03=0.147`, M10 `0.08+0.05×0.08=0.084`, M11 `0.05+0.2×0.08=0.066`이다.
- 감염식 입력은 M09/M10 각각 `-0.05×0.45=-0.0225`, M11 `-0.2×0.45=-0.09`다. M09 anesthesia0.1은 출혈식에도-0.01을 더한다. M09 speed1.15는 persistent surgery work multiplier다.

## 요구 절차와 재료 비용

- Transplant tag 요구 절차는 자연 장기 이식58WU(tags280)과 이종 이식72WU(tags792) 두 개다.
- ImmuneControl은 이종 이식만 요구하고 IsolationRecovery를 요구하는 현재 절차는0개다.
- 이식 재료 계획은 `RequiredFacilityTags`에 Transplant가 있으면 room snapshot에 포함된 모든 `BuildingTransplantSupportAbility`의 비용을 합친다.
- 따라서 M09 한 대당 혈액팩1개, 같은 방 M10 한 대당 면역억제제1개가 추가된다. 비용 gate는 ImmuneControl 요구 여부가 아니라 Transplant 요구 여부라서 자연 장기 이식도 M10 support가 있으면 면역억제제1개를 더 요구한다.
- 여러 M09/M10은 비용이 시설 수만큼 누적된다. M11은 두 작성 비용이0이라 추가 재료가 없다.
- 재료 목록은 주문 생성 시 현재 facility snapshot에서 고정되고 order aggregate/save에 보존된다.

## 거부반응 감소와 미소비 필드

- 성공한 `ApplySurgicalBurdenEffect`는 현재 room snapshot의 TransplantSupport abilities 가운데 `immuneControl==true`인 것만 고르고 `rejectionReduction` 최대값을 사용한다. 합산이 아니다.
- 현재 활성값은 M10의0.35뿐이다. 자연 장기 이식의 rejection8은 `8×0.65=5.2`, 이종 이식24는 `24×0.65=15.6`이 된다.
- M09의 rejectionReduction0.15와 M11의0.2는 작성되어 있으나 immuneControl이 false라 이 effect 경로에서 소비되지 않는다. 활성 15%/20% 감소로 공개하면 안 된다.
- M11의 IsolationRecovery는 현재 tag와 높은 sterility0.2를 만드는 데만 쓰인다. 이 tag를 요구하는 절차와 별도의 회복속도 consumer는 없다.
- burden effect는 성공 시 현재 room을 다시 평가한다. 따라서 주문 생성 후 M10이 추가·제거되면 저장된 면역억제제 요구와 실제 거부반응 감소가 서로 다른 시점을 반영할 수 있다.

## 공개·UI 경계

- building-9509/9510/9511 facts는 분류·크기뿐이고 summary는 내부 공정 재고, 진료/치료 또는 연료 소비, 건설비만 표시한다.
- 의료 가이드와 수술 UI는 support 시설 목록, 시설별 bonus/cost, rejection 감소 분해 및 M11의 현재 미소비 경계를 설명하지 않는다.

## 정적 검증 경계

- ability와 M09~M11 작성값, 두 이식 절차, room facility 결합, 재료 계획, 위험식, burden effect, order 저장, 공개 시설 페이지·의료 가이드와 기존 수술 보고서를 읽었다.
- 실제 이식 예약/재료 운반·소비, M10 추가·제거, 거부반응 부담, 다중 시설 stack, 저장 왕복은 Play Mode에서 재현하지 않았다.
