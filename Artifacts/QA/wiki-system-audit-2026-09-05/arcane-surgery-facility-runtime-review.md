# 비전 개조대 수술시설 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingArcaneSurgeryAbility` 작성 시설은 M12 비전 개조대(id9512) 한 개다.
- M12는 ArcaneSurgery tag를 제공하는 primary 수술시설이며 같은 usable room의 다른 primary에는 support로도 합산된다.
- 공개 M12 페이지와 의료 가이드는 시설별 성공·무균·마취 보정, 마나결정 비용, 최소 변이 하한과 다른 수술에 support로 들어갈 때의 효과를 설명하지 않는다.
- GAP-118은 절차별 시설·재료·효과, GAP-119는 공통 위험식을 소유한다. M12 시설 계약은 GAP-181로 분리한다.

## 작성값과 제공 능력

| 필드 | 작성값 | 현재 실행 |
| --- | ---: | --- |
| successBonus | 0.12 | SuccessBonus+0.12 |
| minimumMutationRisk | 0.08 | ApplySurgicalBurden 변이 하한8 |
| manaCrystalCost | 2 | `resource:mana-crystal` 2개 추가 |
| primary | true | 주시설 또는 같은 방 support |
| sterility / speed / anesthesia | 0.05 / 1 / 0.1 | 무균·속도·마취 snapshot 입력 |

- 같은 usable room의 비파괴·비손상 수술시설은 tag OR, sterility/success/anesthesia 합, speed 곱으로 결합된다.
- 최종 snapshot은 sterility/anesthesia0~1, speed0.25~3, success -0.25~0.35로 제한된다.

## 이형 개조와 재료 연결

- 현재 ArcaneSurgery tag 요구 절차는 `procedure:arcane-modification` 이형 개조88WU 한 개다.
- required tags6160은 ArcaneSurgery2048, RuneSuture4096, Anesthesia16의 조합이다. M12만으로는 충족되지 않아 M13 룬 봉합기와 M05 마취 장치가 같은 usable room에 있어야 한다. Sterilization tag는 요구하지 않는다.
- 이형 개조 자체 재료는 마나결정3·표준약품2이며 마취필수라 마취제1이 추가된다. M12의 facility 비용으로 마나결정2가 더해져 M12 한 대 기준 마나결정 총5개가 된다.
- `BuildMaterials`는 ArcaneSurgery tag 요구 여부를 검사하지 않고 snapshot의 각 M12마다 마나결정2를 추가한다. 다른 수술의 same-room support로 M12가 들어가도 비용이 붙고 여러 M12면 누적된다.
- 시설 추가 재료는 주문 생성 시 목록에 고정·저장·물리 운반·소비된다. 실행 중 tag와 위험은 현재 방 snapshot을 다시 평가한다.

## 위험·변이 효과

- M12 한 개는 성공식 facility+0.12, sterility×0.08=+0.004를 제공한다. 마취필수 수술에는 anesthesia×0.03=+0.003도 더해져 다른 입력과 clamp 전 합계+0.127이다.
- 감염식에는 `-0.05×0.45=-0.0225`, 출혈식에는 `-0.1×0.1=-0.01` 입력을 제공한다.
- 성공한 수술의 `ApplySurgicalBurdenEffect` 처리 때 현재 방의 모든 M12 가운데 가장 큰 `minimumMutationRisk×100`을 변이 하한으로 쓴다.
- 해당 effect가 있는 절차는 이형 개조(변이18), 자연 장기 이식(0), 이종 이식(4) 세 개다. M12 한 대의 하한8은 이형 개조18은 유지하지만 M12가 support로 존재하는 자연 장기 이식0과 이종 이식4를8로 올린다.

## UI·공개 위키 경계

- 수술 UI는 주시설 이름, required tags, Work와 최종 위험을 표시하지만 M12 support, 시설별 보정, 마나 비용 분해와 변이 하한을 표시하지 않는다.
- 공개 building-9512 facts는 분류·크기뿐이고 summary는 연료 소비·내부 공정 재고·진료와 치료·건설비만 설명한다.
- M12의 FuelConsumer와 공정 급배수 등 다른 ability는 각 기존 GAP 소유 범위이며 여기서 중복하지 않는다.

## 정적 검증 경계

- ability/M12 자산,47개 절차의 required tag와 세 burden effect, 시설 query, 재료 계획·저장·소비, 위험식·effect handler, UI와 공개 페이지/가이드를 읽었다.
- 실제 이형 개조, 다른 수술의 M12 support, 마나 운반·소비, 변이 하한, 다중 M12와 저장 왕복은 Play Mode에서 재현하지 않았다.
