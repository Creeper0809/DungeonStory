# 해부대 수술시설 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingAnatomyTableAbility` 작성 시설은 M02 해부대(id9502) 한 개다.
- M02는 Anatomy tag를 제공하는 primary 수술시설이며, 같은 usable room의 다른 primary 수술시설에는 support로도 합산된다.
- 공개 M02 페이지는 분류·크기와 내부 공정 재고/건설비만 보여 Anatomy 역할, 속도, 성공·무균 보정과 수술실 명명을 설명하지 않는다.
- GAP-118은 절차별 요구 시설 태그, GAP-119는 공통 위험식, GAP-177은 M07/M13 Rehabilitation ability를 소유한다. M02의 authored capability는 별도 GAP-178로 등록한다.

## 작성값과 제공 능력

| 시설 | 제공 tag | primary | speed | success | sterility | anesthesia |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| M02 해부대 | Anatomy | true | 1.0 | +0.02 | +0.10 | 0 |

- M02 자체를 주시설로 쓰면 speed1.0, success+0.02, sterility0.1인 snapshot을 시작점으로 삼는다.
- 다른 primary와 같은 usable room에 M02가 있으면 Anatomy tag를 OR하고 speed×1.0, success+0.02, sterility+0.1을 support로 더한다.
- 최종 시설 snapshot은 speed0.25~3, success -0.25~0.35, sterility0~1로 제한한다.
- 사용 가능한 주시설 후보는 success 내림차순, sterility 내림차순, persistent facility ID 오름차순이다.

## 절차·작업·위험 연결

- 현재 Anatomy bit를 요구하는 절차는 `procedure:corpse-organ-extraction` 사체 장기 적출 한 개이며 requiredWork26이다.
- procedure의 모든 required tag가 같은 방 합산 tag에 있어야 시설을 사용할 수 있다.
- speed1.0은 persistent surgery work의 extra multiplier라 M02 자체 추가 가속은 없다. 다른 support speed와는 곱한다.
- success+0.02는 GAP-119 성공식의 facility contribution에 직접 더한다.
- sterility0.1은 성공식의 `sterility×0.08`에서 +0.008을 만들고 감염식의 `-sterility×0.45` 입력으로 쓰인다. 최종 확률은 다른 시설·의사·환자·절차·환경 입력과 clamp를 함께 거친다.
- RoomEnvironmentPresentation은 Medical room에 AnatomyTable primary 계열 시설이 있으면 방 이름을 `수술실`로 표시한다.

## UI·공개 위키 경계

- 수술 UI는 주시설 이름, required tag, Work와 최종 위험 확률을 표시하지만 support 목록이나 facility bonus 분해는 표시하지 않는다.
- M02 시설 페이지 facts는 분류·크기뿐이고 relations는0이다.
- M02의 일반 생존 Treat/Golem recharge/공정 급배수 등 다른 abilities는 각각 별도 GAP 소유 범위이며 이 보고서에 중복하지 않는다.

## 정적 검증 경계

- ability 정의, M02 자산, Anatomy 요구 절차, 시설 query, work/risk 소비, 방 이름과 공개 페이지를 직접 읽었다.
- 실제 사체 장기 적출, 같은 방 support 합산, 후보 선택, 위험 변화와 저장 왕복은 Play Mode에서 재현하지 않았다.
