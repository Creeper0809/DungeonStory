# 저장 연속성·운영 병목 진단 위키→구현 대조

정적 C#/등록/위키 대조다. 실제 저장 슬롯 왕복과 Play Mode UI 조작은 이번 절편에서 실행하지 않았다.

## continuity 판정

현재 `DungeonSaveRegistration`에는 `IDungeonSaveSection` 등록이75개 있다. registry 생성자는 모든 section에 양수 version, 고유 ID, detached staged restore를 요구하고 dependency를 위상 정렬하며 누락·후기 phase 의존·cycle을 거부한다.

`RestoreAll`은 전체 envelope preflight와 cross-section validator를 먼저 실행하고, detached aggregate root와 transaction participant 후보를 준비한 뒤 모든 stage가 성공한 경우에만 publish한다. 실패 시 stage discard, participant 역순 rollback 또는 필요 시 이전 전체 image 복구를 수행한다. 따라서 continuity 가이드의 다음 핵심 약속은 현재 구조와 일치하는 것으로 판정했다.

- section ID·version·dependency 선검사
- live world와 분리된 후보 상태 준비
- cross-section reference validation
- 전체 성공 뒤 aggregate root 단일 교체
- 실패 시 부분 세계를 남기지 않는 rollback 경로
- physical items, work orders, production output lifecycle, surgery, offense, events 등 별도 save section

이 판정은 75개 section 각각의 모든 필드가 완전하다는 뜻이 아니다. 개별 도메인 WIM은 해당 도메인 절편에서 계속 확인한다.

## world-state 판정

운영 화면은 `GameplayFlowDiagnosticsQuery`를 실제로 호출해 공통 FlowRows를 표시한다. 진단기는 다음을 처리한다.

- 활성 일반 WorkOrder 최대7개
- 재료 부족, 운반자 없음, 운반 대기
- 작업자 없음, blocked, target unreachable, output space 부족
- loose stack, 창고 유무·gram 여유·허용 품목
- deferred path search와 실제 경로 막힘의 구분

AI에는 action/destination/facility cooldown과 최근 거부 후보 제외가 실제 구현돼 있어, 같은 실패 대상의 즉시 왕복 방지 자체는 존재한다.

그러나 위키가 말하는 공통 8상태 전체는 운영 진단기의 입력·출력 모델에 없다. 특히 개별 시설의 전력·물·연료·청결·환경·도구 조건, 신분·위험 접근 거부, 일반 retry wait를 공통 FlowRows에서 typed 상태로 표시하지 않는다. 일부 개별 건물 패널이 재료 운반 대기 등을 따로 표시하는 것과 모든 주민·시설의 통합 상태 표시는 구분해야 한다. 이를 WIM-046으로 기록한다.

## 근거 해시

| 파일 | SHA-256 |
| --- | --- |
| continuity.md | `6B5DB7EB330C1155DB83221A391E868DA52FB6FBA31F901AC98D3AEC7412CE38` |
| world-state.md | `5BC42E53CB82CC7480FAB006C7E2CDEC0E55CB3E737512A723F9003C7928F168` |
| DungeonSaveSections.cs | `6CE69C7322923D428AE135D080A7657ABB72CA9BC0022A6F9822C85930675E6C` |
| DungeonSaveRegistration.cs | `AC273D519D2B0D59A9A6E0AB6CD119F85C34880885F2D7D1B1CCD824D88FDD0D` |
| DungeonGameSaveService.cs | `642C21A3D88B1ADBD795022187162629B0859AB3449B3031D61A94C15B362F91` |
| GameplayFlowDiagnostics.cs | `148B8FB3A3A6569A613E3E385A9E5D36DCBC33EE45C07577FFF326825CCC426C` |
| OperationsFeatureQueryService.cs | `9CD734ABCEE497EE956BB250456CF3AD77FECBE5BE2E52EDAD25E02CFEB8BE87` |

