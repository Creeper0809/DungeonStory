# 재활 보조대·룬 봉합기 수술시설 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingRehabilitationAbility` 작성 시설은 M07 재활 보조대(id9507)와 M13 룬 봉합기(id9513) 두 개다.
- 이 ability는 포로의 재사회화와 무관한 수술시설 계약이다. 수술시설 tag, 주시설 가능 여부, 같은 방 지원시설 합산, 작업속도·성공·무균 보정을 대조했다.
- 공개 M07/M13 페이지는 분류·크기와 건설비/광역 역할명만 제공한다. 의료 가이드와 수술 UI도 절차 요구 태그 및 최종 확률은 보여 주지만 시설별 제공값과 지원시설 합산을 설명하지 않는다.
- GAP-118은 절차별 요구 시설 필드, GAP-119는 공통 성공·합병증 공식을 소유한다. M07/M13의 실제 입력값과 결합 규칙은 별도 GAP-177로 등록한다.

## 작성값과 활성 판정

| 시설 | speed | rejection/work | primary | runeSuture | mana cost | 실행 효과 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| M07 재활 보조대 | 1.6 | 0.12 | true | false | 0 | Rehabilitation tag, 주시설, 작업속도×1.6 |
| M13 룬 봉합기 | 1.35 | 0.15 | false | true | 1 | Rehabilitation+RuneSuture tag, 지원시설, 속도×1.35, sterility+0.12, success+0.08 |

`rejectionReductionPerWork`는 능력 정의와 작성 builder 외 runtime consumer를 찾지 못했다. Rehabilitation ability의 `manaCrystalCost`도 현재 재료 계획에서 읽지 않는다. 재료 계획은 `BuildingArcaneSurgeryAbility.manaCrystalCost`만 마나결정 입력으로 추가한다. 따라서 M07/M13의 rejection0.12/0.15와 M13 mana1을 실제 감소·소비 효과로 공개하지 않는다.

## 주시설·같은 방 지원시설 결합

1. 주시설은 비파괴·비손상이며 `IsPrimaryOperatingFacility=true`인 첫 수술 ability가 있어야 한다. M07은 주시설 후보지만 M13은 단독 주시설 후보가 아니다.
2. 주시설이 속한 방이 usable이어야 한다. 같은 방의 다른 비파괴·비손상 수술시설은 support가 된다.
3. 제공 tag는 OR, sterility/success/anesthesia는 합산, speed는 곱산한다. 최종 snapshot은 sterility0~1, success -0.25~0.35, speed0.25~3으로 제한한다.
4. M07 단독 speed는1.6이다. M07과 M13이 같은 usable room이면 speed는 `1.6×1.35=2.16`, success+0.08, sterility+0.12가 된다. 다른 수술 support가 있으면 그 값도 같은 규칙으로 합쳐진다.
5. 절차 required tags가 합산 tag에 모두 포함돼야 시설이 available하다. 현재 Rehabilitation bit를 요구하는 절차는 보철 재활(30WU), RuneSuture bit를 요구하는 절차는 이형 개조(88WU) 한 개씩이다.
6. 사용 가능한 주시설 후보는 success 내림차순, sterility 내림차순, persistent facility ID 오름차순으로 정렬된다.

## 작업·위험·UI 연결

- `SurgeryWorkExecutionHandler`는 snapshot SpeedMultiplier를 persistent surgery work의 extra multiplier로 전달한다.
- M13 success+0.08은 `SurgeryRiskEvaluator`의 facility contribution에 직접 더해진다. sterility+0.12는 성공식의 `sterility×0.08`과 감염식의 `-sterility×0.45`에 들어간다.
- 수술 UI의 시설 선택지는 주시설 이름만 표시한다. 상세에는 required tag, Work, 최종 Success/Infection/Bleeding/Organ damage/Death와 환경은 나오지만 support 목록·속도·시설별 bonus 분해는 없다.
- 시설 구성은 authored BuildingSO이고 수술 order는 선택한 주시설 persistent ID와 계산된 위험을 저장한다. support 구성 자체를 order에 별도 저장하지 않고 실행 중 현재 room snapshot을 다시 평가한다.

## 정적 검증 경계

- ability 정의, M07/M13 자산, 두 관련 절차, 수술시설 평가·재료계획·작업실행·위험식·UI, 공개 시설 페이지와 의료 가이드를 직접 읽었다.
- 실제 같은 방 배치, 속도2.16, 성공/감염 확률 변화, 시설 파손·철거 뒤 재평가와 저장 왕복은 Play Mode에서 재현하지 않았다.
