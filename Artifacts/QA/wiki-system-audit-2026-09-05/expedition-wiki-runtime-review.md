# 원정 가이드 → 현재 구현 대조

조사일: 2026-09-06  
방향: 공개 원정 가이드의 주장 → production 코드·저장·UI  
한계: 정적 대조이며 새 Play Mode 실행은 하지 않았다.

## 정상 연결

- 실제 원정 시작은 `research:survival:field-rations` 완료 여부를 UI query, launch command와 runtime에서 반복 검증한다.
- 최대 인원은 launch service에서 5명으로 제한한다.
- 사장은 명시적으로 거절하고, active NPC이면서 작업/전투 ability를 가진 인원만 허용하므로 주인·비NPC 하수인 제외 설명과 일치한다.
- 보급은 물리 staging·delivery·custody·소비/손실·반환 질량을 가진다. 출정 준비 실패 시 소유권을 보존하는 rollback 경로도 있다.
- 원정 run은 참가자, 보급, 진행 node, 완료 node, stress·피해, carried stock, 회수 장비 instance, 현장 자금과 귀환 상태를 저장한다.
- 전략 원정은 좌표·목적지·남은 path·tile 진행도·노출·결정 중단·전투 중단·조난을 저장한다.
- 현장 결정은 결정론적 card/choice 상태를 저장하고, 전투 command deck·적 intent·command queue도 별도 저장한다.
- 종족별 야전 의료 키트, 안정화, 부상자 운반, 조난·구조·귀환 안전 예산과 포로 도착 lifecycle이 있다.
- 범주형 전리품은 carried stock으로 보존되고 회수 장비는 instance ID로 분리된다. 귀환 시 물리 materialization과 도착 처리가 있다.

## 기존 WIM과의 연결

- 날씨·기후·지형 비용이 실제 이동 clock에 반영되지 않는 문제는 `WIM-017`이 소유한다. 원정 전체가 미구현이라고 확대하지 않는다.
- 현장 자금·적대 소문·원정지 정보 결제처럼 구현에 있으나 위키 설명이 없는 항목은 반대 방향 `missing-register.*`의 GAP-146/147이 소유한다.

## WIM-053 — 원정 결산의 소비·손실·치료·포획 내역이 없음

위키는 귀환 결산에 교전 시간, 탄약·약품·식량 소비, 장비 내구도 손실, 치료 작업과 회복일, 사망·포획·탈출, 성공 여부, 전리품 가치와 고유 보상을 함께 기록한다고 설명한다.

실제 `OffenseExpeditionResult`는 대상, 성공 여부, 전투력, 요구 전투력, 위험도, 총 경과 시간, 대원별 생존·피해, 보상 요약만 가진다. 저장 DTO도 같은 축만 보존하며 reward grant의 구조화된 세부는 문자열 summary로 축약된다. 탄약/약품/식량의 종류별 소비량, 장비별 내구도 손실, 치료 작업·회복일, 포획·탈출 수, 전리품 총가치 필드는 결과 history에 없다. 개별 subsystem에 현재 상태나 event가 존재하는 것은 귀환 결산 기록의 반증으로 세지 않았다.

판정: `partial`.

필요 보완: 원정 operation 단위 immutable ledger에서 supply·ammo 소비, equipment durability delta, field treatment, recovery estimate, casualty/capture/escape, physical reward value를 집계하고 결과 snapshot·save·UI에 동일한 canonical 필드로 보존한다.

## 원본 SHA-256

| SHA-256 | 원본 |
| --- | --- |
| `63b17ff7e89572566536676dff09479ca437eacf774ff6946ece19a08ffa7e63` | `wiki/game-versions/0.0.1v/content/guides/expeditions.md` |
| `3c4c41b50738e1adcd0fd03f236352bd12dc835c50839d9f5d4f9778488c7847` | `Assets/Scripts/Services/Offense/OffenseExpeditionAccessRules.cs` |
| `66e9181f361ac8c1b1a7fabdd4851064a4e639628f84b632adfd20d62bee0bb3` | `Assets/Scripts/Services/Offense/OffenseExpeditionLaunchService.cs` |
| `1bae5523c7bdf6b9c96a410647549cf5e10d4b5176862433971268b4cf86be0a` | `Assets/Scripts/Services/Offense/OffenseExpeditionService.cs` |
| `394bcb1f744f766b95a23ee9536ce8fb4591655904ddd34e8a77810b12cb0984` | `Assets/Scripts/Models/Offense/Core/OffenseRewardEvents.cs` |
| `a364537cb49a6843d3e4791f3172d5ad50440d8f57269aae146738962418f5f2` | `Assets/Scripts/Services/Infrastructure/OffenseSaveService.cs` |
| `ffbed9e10a2af6dda8a037817043122442d419e7fc1134bd57182b3001515a4b` | `Assets/Scripts/Services/Offense/OffenseExpeditionModel.cs` |
| `26e0fb4ecfe6224068dc7f14bf56f3b36bdb0d7bdc4044b9bc6f00672d14df74` | `Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs` |
| `4f95b2847da1120fb09a93b24b83040c46968a83a9d1037f0c191bab3b31f811` | `Assets/Scripts/Services/Offense/Strategic/OffenseFieldMedicalRuntime.cs` |
