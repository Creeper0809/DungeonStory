# FuelConsumer 시설 13종 런타임·공개 위키 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 판정

- 실제 `BuildingFuelConsumerAbility` 작성 자산 13개와 공개 시설 엔티티 13개를 전수 대조했다.
- 공개 시설 페이지는 `연료 소비`라는 역할명과 건설비만 요약하고, facts는 모두 `분류/크기` 두 개이며 relations는 0이다.
- 공개 업무 참조의 `연료 보급`은 정성적인 운반·경로·버퍼 설명뿐이다. 실제 FuelConsumer의 보충량, 작업량, 전역 생존 상태 변경, `lightSafety` 합산과 야간 위험 계산은 없다.
- 기존 GAP-056(자동화), GAP-068(전력), GAP-083(생산지원), GAP-085(환경장 조명)은 같은 시설 일부를 다루지만 FuelConsumer 계약을 소유하지 않는다. 구조 검색에서도 기존 GAP 소유자는 0건이었다.
- 따라서 실제 구현에서 공개 위키로 빠진 독립 항목을 GAP-174로 등록한다.

## 작성 모집단

| 시설 | ID | fuelPerRefuel | workSeconds | warmth | lightSafety |
| --- | ---: | ---: | ---: | ---: | ---: |
| 간이화덕 D01 | 1000 | 1 | 0.8 | 8 | 6 |
| 고기그릴 D02 | 1001 | 1 | 0.9 | 10 | 7 |
| 벽횃불 E01 | 1064 | 1 | 0.6 | 2 | 14 |
| 바닥화로 E02 | 1065 | 1 | 0.8 | 14 | 10 |
| 샹들리에 E03 | 1066 | 1 | 0.8 | 3 | 18 |
| 촛대 E07 | 1070 | 1 | 0.5 | 1 | 8 |
| 화덕·가마솥 WS08 | 1607 | 1 | 0.8 | 8 | 4 |
| 벽돌 오븐 WS09 | 1608 | 1 | 0.8 | 8 | 4 |
| 마취 장치 M05 | 9505 | 1 | 1.0 | 0 | 2 |
| 장기 보관함 M08 | 9508 | 1 | 1.0 | 0 | 2 |
| 면역 조절기 M10 | 9510 | 1 | 1.0 | 0 | 2 |
| 비전 개조대 M12 | 9512 | 1 | 1.0 | 0 | 2 |
| 룬 봉합기 M13 | 9513 | 1 | 1.0 | 0 | 2 |

## 실제 실행 계약

1. `HasSurvivalWorkAvailable`은 대상이 FuelConsumer이고 현재 Grid의 등록 창고에 Fuel category 재고가 1개 이상이면 Refuel 업무를 허용한다. 시설별 연료 잔량이나 목표 buffer 상태는 검사하지 않는다.
2. `SurvivalWorkExecutionHandler`는 `workSeconds`를 필요한 작업량으로 사용한다. 일반 날씨 urgency는 `clamp(ExteriorNightDanger × 0.45, 10, 55)`, 한파는 75다.
3. 완료 시 `TryApplyRefuel`은 등록 창고의 Fuel category에서 `max(1, fuelPerRefuel)`을 withdraw하고 `lastMissingFuel`을 0으로 만든 뒤 활동 기록을 남긴다. 현재 13개 작성값은 모두 1이다.
4. 별도 일일 정산은 기본 Fuel 1, 한파에는 2를 요구하고 FuelConsumption 위협 배율을 곱해 올림한 수량을 다시 전역 창고에서 withdraw한다. 부족량은 `lastMissingFuel`에 저장된다. 이는 시설별 보충 상태가 아니다.
5. 같은 Grid에 배치되고 파괴되지 않은 모든 FuelConsumer의 `lightSafety`를 합산한다. refuel 완료 여부, 시설별 연료 잔량, 전력/가동 상태는 이 합산 조건에 없다.
6. 야간 위험은 `clamp(weatherDanger + lastMissingFuel × 18 + rotStacks × 4 - summedLightSafety, 0, 100)`이다. weatherDanger는 폭우35, 안개25, 비18, 한파16, 그 외10이다.
7. survival save는 `lastConsumedFuel`, `lastMissingFuel`, 최종 `exteriorNightDanger`를 저장·검증·복원한다. Operations 화면은 저장 연료와 최종 야간 위험만 표시하며 시설별 기여나 공식은 보여 주지 않는다.

## 비활성 작성 필드와 과장 방지

- `warmth`는 작성 자산과 editor builder에는 있으나 production runtime 소비자가 없다. 의복 소재의 별도 warmth 계산과 이름만 같을 뿐 FuelConsumer 필드를 읽지 않는다.
- 따라서 13개 `warmth` 값을 현재 난방 효과로 공개하면 안 된다.
- 공개 문서는 현재 구현을 설명할 때 연료가 시설 buffer에 실제 배송·보관된다고 단정하면 안 된다. FuelConsumer 완료 경로는 등록 창고 stock의 직접 withdraw와 전역 `lastMissingFuel` 변경을 확인한 범위다.

## 정적 검증 경계

- 능력 정의, 작성 자산 13개, 업무 가용성/작업량/완료, 일일 연료 정산, 야간 위험 평가, save/UI 소비자를 직접 읽었다.
- 공개 시설 엔티티 13개와 관련 가이드·업무 참조를 정적 검색·파싱했다.
- Unity Play Mode에서 실제 AI 선택, 창고 withdraw, 저장 왕복과 UI 표시를 재현하지 않았다.
