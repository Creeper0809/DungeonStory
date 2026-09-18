# 대장작업대 장비 제작 ability 런타임 대조

## 범위와 판정

- 작성 모집단은 `BuildingEquipmentCraftingAbility`를 가진 S08 대장작업대 한 곳이다.
- S08의 allowlist는 전투 장비 61종(무기31·방어구21·방패9)과 일반 탄약 recipe 2종(화살·볼트), 합계 63개다. 공개 `building-1019`은 분류·크기와 포괄적인 `장비 제작` 역할만 표시한다.
- GAP-124는 각 장비 정의의 개별 입력·전투 수치를, GAP-125는 모듈을 소유한다. S08 전용 허용 목록, 주문 gate, 재료 목적지, 완료/품질/저장 계약의 공개 owner는 없었다.

## 실제 주문과 작업 경로

1. 패널은 S08 allowlist와 현재 장비 catalog를 교집합해 제작 항목과 시설별 대기열을 표시하고 `TryQueueCraft`를 호출한다. 일반 장비는 연구 해금이 필요하며, `research:equipment:weapon-patterns`를 요구하는 항목은 가동 중인 무기 설계 접근 시설도 필요하다. 탄약 recipe는 이 두 장비 정의 gate를 거치지 않는다.
2. 주문 대상은 반드시 S08 allowlist에 있어야 하고, 시설은 변경 가능 상태여야 한다. 일반 장비는 허용 재질과 구체 BOM을 결정한 뒤 order별 물리 입력 목적지를 열고 실물 이송을 요청한다. 목적지를 열거나 요청하지 못하면 주문은 추가되지 않으며, 요청 실패 시 열린 목적지를 닫아 롤백한다.
3. S08의 `workUnitsPerCycle=1`은 Craft 완료 handler가 각 적용에서 전달하는 작업량이다. 실제 주문 필요 WU는 일반 장비에서는 장비/재질의 balance calculator(없으면 정의의 RequiredCraftWork), 화살·볼트에서는 탄약 cycle authority가 결정한다. 따라서 asset의 1을 모든 장비의 총 제작 WU로 표시하면 안 된다.
4. 작업자는 기본 품질 우선 정책이며, handler는 `performance:work:craft:quality×58`을 0~100으로 제한해 기여 WU와 함께 기록한다. 각 주문은 run seed·order ID·정의 ID·시도 번호에서 고정 품질 roll을 만들며, 시설 등급 보정은 `max(0,(FacilityLevel-1)×2)`다. `Awful`, 자동분해, 안전 한도, 최대10회, 합격1개가 새 주문의 기본값이다.
5. 목표 품질을 현재 가능한 최고 작업자가 달성할 수 없으면 재료를 풀고 대기한다. 불합격 시 처리 정책과 반복 한도에 따라 다음 시도를 준비하고 새 고정 roll을 만든다. 완료 장비/탄약의 출력 publication과 입력 acknowledgement가 별도 transaction으로 끝나야 order가 종료된다.

## 저장·UI와 비범위

- `DungeonCombatEquipmentSaveData`는 craft orders, nextCraftSequence, 시설·정의별 재질 허용/우선순위, terminal effect를 저장·검증·복원한다. 패널은 재료 운반 대기/진행률, 작업자 정책, 최소 품질, 불합격품 처분, 안전 한도/목표 품질까지 표시한다.
- S08에 함께 작성된 전력·자동화·생산 buffer/컨베이어 값은 이 ability의 장비 제작 계약과 별도이므로 이 항목에서 다시 소유하지 않는다.

## 정적 증거 한계

- Unity Play Mode 실행은 하지 않았다. 위 결론은 현재 작성 asset, 비Editor 주문/완료/저장/UI 호출을 정적으로 대조한 결과다.
