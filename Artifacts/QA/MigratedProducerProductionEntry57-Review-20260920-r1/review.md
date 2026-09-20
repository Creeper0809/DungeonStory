# 57종 생산 진입 E2E 및 구현 재검토 — 2026-09-20

판정: **CHANGES_REQUIRED**. r18의 57종 PASS는 보존하되 모든 생산 분기와 모든 변경 소유자의 실패 복구가 검증됐다는 판정으로 확대할 수 없다.

범위: 현재 C# 생산 코드, 5개 E2E suite, 공용 fault probe/검증기, r18·r81·r40 보고서를 읽기 전용으로 대조했다. 이번 검토에서 C#을 수정하거나 새 Unity 실행을 하지 않았다. 아래 결함은 확정 거절 조건의 제어 흐름과 상태 변경 순서로 확인했으며, 새 실패 재현 실행 결과로 표시하지 않는다. Unity MCP와 dungeon-player MCP를 사용하지 않았다.

검토 HEAD: `b48bc859f452806635d51f25862d56c49baf6ebb`. 미커밋 변경이 있으므로 HEAD만으로 검토 소스를 특정할 수 없으며 아래 파일 해시를 함께 사용한다.

## 수정 필요한 생산 결함

### 1. [P1] 원정 결과 확정 거절 시 보상·진행 상태가 남음

- 위치: `Assets/Scripts/Services/Offense/OffenseExpeditionResultFinalizer.cs:117-194`.
- 성공 원정은 메타 성공 횟수(119), 보상 지급(125), 보상 이벤트(148), 월드 거점 처리(156), 캠페인 진행(170)을 실행한 뒤 원장을 확정한다(174).
- 원장 확정이 거절되면 catch는 결과 history만 복구한다(192-193). 이미 증가한 성공 횟수·지급된 보상·진행 효과는 복구하지 않으며 이 작업들을 재실행하지 않게 하는 확정 단계도 없다.
- 기존 `OffenseExpeditionResultScenario`는 `success=false`(테스트 파일 959)와 history-only 캡처(968-969)를 사용하므로 이 결함이 PASS와 공존한다.
- 필요한 보완: 실제 변경 소유자들을 하나의 실패 경계에 묶거나 저장 가능한 단계별 확정/재시도로 정리하고, 보상이 있는 성공 원정에 거절을 주입해 모든 소유자 및 재시도 횟수를 검사한다.

### 2. [P1] 원정 결과 원장 실패가 귀환을 처리 중에 고정함

- 위치: `Assets/Scripts/Services/Offense/OffenseExpeditionReturnCoordinator.cs:661-696`.
- `ReturnFinalizing=true`를 먼저 설정(661)한 뒤 결과 finalizer를 호출한다(690). finalizer가 원장 예약·확정 실패로 예외를 던지면 flag를 내리는 696에 도달하지 못한다.
- 이후 `Complete` 및 `ResolveIfReady`가 이 flag에서 조기 종료한다(573, 592). 해당 원정은 같은 세션에서 귀환 확정을 다시 시도할 수 없다. 결과 finalizer를 직접 호출하는 기존 E2E는 이 상위 경로를 거치지 않는다.
- 필요한 보완: 재시도 가능한 귀환 상태를 보존하며 실패 시 실행 중 flag를 해제하고, 이때 앞서 적용한 귀환 경험치·보상을 중복 지급하지 않는 상위 coordinator 회귀 검증을 추가한다. 단순 flag 해제만으로 1번 결함이 해결되지는 않는다.

### 3. [P1] 사건 결과 원장 거절 시 외부 효과가 롤백되지 않음

- 위치: `Assets/Scripts/Services/Run/V20ContentResolutionService.cs:1767-1861`.
- `TryCommitEffects`를 먼저 수행하고(1774/1783), 캠페인 후보 게시 뒤 원장을 예약·확정한다(1845). 외부 효과에는 돈(3635), 물품 소모(3650), 물품 지급(3671), 기분·경험치·건강 등(3682 이하)이 있다.
- 원장이 거절되면 catch(1850)는 캠페인 네 aggregate와 제한된 material owner만 복구한다. 돈·물품·캐릭터 효과는 남고 사건 선택 상태는 되돌아가므로 재시도 때 중복 효과가 생길 수 있다.
- 기존 `V20ResolvedEventScenario`는 `WorkDelayDays` 하나만 있는 선택을 명시적으로 고르고(1493-1496), 캠페인 상태만 비교한다(1544-1548).
- 필요한 보완: 돈·물품·캐릭터 효과가 있는 실제 사건을 대상으로 확정 전 가역 경계 또는 저장된 후속 확정 단계를 구성하고 전체 owner 상태를 검사한다.

### 4. [P1] 초기 생존 4개 분기 중 3개는 원장 실패 후 결과가 남음

- 위치: `Assets/Scripts/Services/Survival/CharacterPrimitiveSurvivalRunner.cs:373-387`, `533-570`, `641-670`.
- 야전식은 식사 소모 후, 임시 변소는 오물 생성·욕구/기분 변경 후, 간이 세면은 물 소모·위생 회복 후 `PublishCompleted`를 호출한다.
- `PublishCompleted`(829-850)가 그제야 원장을 예약·확정하며 실패 시 예외를 던진다. 세 분기에는 이미 적용한 효과를 복구하거나 미기록 결과를 저장해 재시도하는 경계가 없다.
- 기존 `PrimitiveSurvivalScenario`는 별도로 순서를 바꾼 `FloorRest`만 호출한다(714). 결과 종류 하나에 PASS가 있어도 나머지 3개 행동을 보장하지 않는다.
- 필요한 보완: 네 행동을 각각 테스트하고 실제 식사/물/오물/능력치 소유자를 포함하는 확정 경계를 둔다.

### 5. [P1] 갈증 붕괴의 음수 경로에 원장 실패 복구가 없음

- 위치: `Assets/Scripts/Services/Survival/CharacterBreakdownActionRunner.cs:432-462`.
- 월드 물을 소비(434)하고 갈증을 회복(441), 붕괴를 종료(446)한 뒤 원장을 확정한다(447). 거절 시 소비한 물과 캐릭터·붕괴 상태를 복구하지 않는다. 상위 coroutine의 finally(273)는 실행/AI lease만 정리한다.
- 특히 오염수의 후속 피해·감염 처리(463 이후)는 예외 때문에 생략되어 물 소비·회복만 남을 수 있다.
- 기존 `WaterConsumedScenario`는 `CharacterWaterConsumptionCoordinator.TryConsumeWorldSource`만 호출한다(839). 이 별도 붕괴 경로는 실행하지 않는다.
- 필요한 보완: 두 실제 음수 경로를 같은 거래 계약에 맞추고 오염수까지 포함해 물·욕구·붕괴·피해 상태를 함께 검사한다.

## 테스트 판정 보완

- [P2] 공용 `RequireExact`(`MigratedProducerOutcomeE2ETestSupport.cs:389-398`)는 정상 성공 경로에서도 `PublishedAcknowledged`를 요구하지 않는다. 타입이 맞는 pending 결과가 정상 성공으로 통과할 수 있다. 전투 suite 외 44종이 이 helper를 사용한다.
- 전달 실패 주입은 실제 descriptor delivery 경계에 도달하지만 재시도는 같은 메모리의 recorder에서 수행한다(406-438). 각 생산자의 pending 상태를 저장→새 인스턴스 복원→재시도하는 57종 검증은 아니다. 별도 81-section 기본 왕복 PASS로 이를 대체할 수 없다.
- 확정 거절 주입은 transaction interface에서 cancel 후 false를 반환하는 방식이다(203-216 및 batch 변형). 이는 생산자의 거절 대응을 검사하는 유효한 테스트지만 실제 recorder 내부의 확정 실패·부분 예외 정리를 실행했다는 뜻은 아니다.
- aggregate의 `observed`는 실제 probe 기록이 아니라 정적 `CoveredKinds` 목록이다. 현재 57종 메서드 호출은 소스에서 확인되지만, r18 JSON은 종류별 실행/상태 및 source hash를 출력하지 않는다.

## 유지되는 증거와 수정되는 해석

현재 소스는 13 combat + 16 run/offense + 16 character/survival + 6 economy + 6 service = 57종을 각각 한 가지 fixture로 실행한다. r18 PASS, r81 49-step PASS, r40 81-section PASS, r164 compile PASS 기록은 삭제·변조하지 않는다. 다만 위 5개 결함으로 인해 **전체 생산 경로의 실패 원자성이 완료됐다는 판정은 철회**한다. `humanApprovalClaimed=false`, `trainingEligible=false`, `REJECT_BEFORE_DPO`는 유지한다.

## 검토한 생산 소스 SHA-256

| 파일 | SHA-256 |
|---|---|
| OffenseExpeditionResultFinalizer.cs | `65450CD01B075CEBA200911F09E24D50082AEE9EF0C3AD5301F0900E8204486C` |
| OffenseExpeditionReturnCoordinator.cs | `CFA20BB20D9D68A46F4D64ABFB273678A7E3442246273A9D5A1CC6D03ABADB60` |
| V20ContentResolutionService.cs | `ADDDC48AA5DFAEF3A496DAB5A7088AE78A93F7C7E2084AFDD1D4D271B779D3D9` |
| CharacterPrimitiveSurvivalRunner.cs | `DCE7CC134BE42FEBE08E3FB5E9DDAA7588B8E0B17B2D73231D73DF79A8FEAB0B` |
| CharacterBreakdownActionRunner.cs | `856918BC36C8E785A8A36E28C8FC70216219BD0A76591D8D23229A07C336FC41` |
