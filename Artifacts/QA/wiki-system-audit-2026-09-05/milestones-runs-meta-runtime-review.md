# 이정표·런 결과·계승·엔드리스 구현 대조

조사일: 2026-09-06  
방향: 공개 위키가 약속한 동작 → 현재 production 구현  
범위: `milestones-runs-and-meta` 가이드, 이정표 9개 SO, 일일 평가, 런 결과 DTO·저장, 계승 상태, 엔드리스 조합

## 모집단과 정상 연결

- `EndingDefinitionSO`는 정확히 9개이며 stable ID도 9개다.
- 각 이정표 SO에는 completion requirement, landmark ID, permanent reward 1개, counter-pressure 1개가 있다.
- 일일 시작 시 실제 월드 snapshot을 만들고 `IRunMilestoneCommand.Evaluate`를 호출한다.
- 조건을 만족한 이정표는 완료 ID, 랜드마크 해금 ID, 영구 보상 ID, 압력 ID를 현재 런 aggregate에 기록한다.
- 첫 Legacy 이정표는 `LegacyAge`, Grand 이정표는 `EndlessAge`로 진행 단계를 변경한다.
- 완료 ID·보상·랜드마크·압력·엔드리스 cycle과 현재 crisis ID는 `RunMilestoneWorldSaveData`에 저장되고 restore 시 ID와 중복을 검증한다.
- 계승 재화·구매한 강화·보존 레시피·완료 런 수·최근 런 결과는 settlement save와 분리된 meta profile 저장 경로를 가진다.
- 현재 런의 일반 돈·재고·배치 시설은 `DungeonMetaProgressionSaveData`에 복제되지 않는다.

## WIM-050 — 런 결산에 완료 목표와 선택 흔적이 없음

위키는 런 종료 시 결과 snapshot에 완료한 목표와 선택의 흔적을 결산한다고 설명한다. 실제 `RunResultSnapshot`과 `DungeonRunResultSaveData`에는 생존 시간·운영일·침입·위협·발견 시설·해금 레시피·오펜스 성공·난이도·계승 재화만 있다. 완료 이정표 ID, 사건/세력/엔딩 선택 ID 또는 선택 이력 필드는 없다.

판정: `partial`. 런 결과 생성·UI·저장은 존재하지만 위키가 약속한 두 핵심 결산 축이 누락됐다.

필요 보완: 현재 런의 completed milestone ID와 중요한 choice trace를 canonical snapshot으로 캡처하고, meta result 저장·결과 UI까지 연결한다. 일반 재고나 진행 중 주문을 계승 데이터로 복제해서는 안 된다.

## WIM-051 — 엔드리스 축 선택이 실제 압력으로 적용되지 않음

위키는 10일마다 기후·세력·질병·물류·전투 중 한두 축을 강화한다고 설명한다. 실제 일일 adapter는 EndlessAge에서 10일마다 `ComposeNextEndlessCrisis`를 호출한다. 그러나 이 메서드는 계절 사건·조우·전장 modifier·질병·야생동물에서 각각 하나씩, 항상 정확히 5개 ID를 골라 `activeEndlessCrisisIds`에 저장할 뿐이다.

production 검색에서 이 ID 목록의 reader나 gameplay application을 찾지 못했다. `IRunMilestoneQuery`에도 active crisis 목록이 노출되지 않고, application adapter는 반환값을 사용하지 않는다. 개별 이정표의 permanent counter-pressure 적용 경로는 별도로 존재하므로 그것을 엔드리스 crisis 적용의 반증으로 세지 않았다.

판정: `partial`. 10일 trigger·결정론적 선택·저장은 있으나 공개된 1~2축 의미와 실제 gameplay 강화가 없다.

필요 보완: 선택 축 수와 조합 규칙을 단일 권위로 정하고, active crisis ID를 각 기후·세력·질병·물류·전투 runtime modifier에 적용·해제·저장 복원한다.

## WIM-052 — 복합 위기 뒤 최소 5일 회복 창이 없음

위키는 일반 복합 위기 뒤 최소 5일 회복 창을 목표로 한다. 현재 엔드리스 상태에는 cycle과 active ID만 있고, crisis 종료일·회복 종료일·다음 조합 가능일 필드가 없다. adapter는 `day % 10 == 0`이면 직전 crisis의 종료나 회복 상태를 확인하지 않고 다음 5개 ID로 덮어쓴다.

판정: `missing`.

필요 보완: crisis lifecycle에 active/resolve/recovery 상태와 `nextEligibleAbsoluteDay`를 추가하고, 최소 5일 회복 gate가 저장·복원과 UI에 동일하게 적용되도록 한다.

## 중복 제외

- 시작 시설·사장 특성 후보를 넓히는 meta 강화의 구매·저장·query는 존재하지만 실제 후보 생성 소비자가 없는 문제는 기존 `WIM-010`이다.
- 이정표별 영구 보상과 counter-pressure는 typed runtime query 및 application adapter가 있으므로 이번 세 항목에 합산하지 않았다.
- 위키의 도달 일수 표는 명시적으로 밸런스 목표이며 completion gate 자체가 아니므로, 날짜 gate 부재를 결함으로 세지 않았다.

## 원본 SHA-256

| SHA-256 | 원본 |
| --- | --- |
| `3ffb3e1c0604f9e75a8f90335426a0e7969ecc0d27275bca8a1857778389b778` | `wiki/game-versions/0.0.1v/content/guides/milestones-runs-and-meta.md` |
| `30e12b6482144b873b0fe9868c9e5e9f065c91651b6097c2248073f32a539c3c` | `Assets/Scripts/Services/Run/V20CampaignRuntime.cs` |
| `51bd1be0f08336e117a2e359871868374f796ff15b91351a0e9b270cc254b4ce` | `Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs` |
| `4634be67f4536fbbe4c57e1c21725215a8bb42e684dd0779d862f2c646446be0` | `Assets/Scripts/Content/EndingDefinitionSO.cs` |
| `91cc510d875b1cc9e473222efa47ed5516dfa171149d1d0542c1e2c3ff5a37d4` | `Assets/Scripts/Models/Meta/Core/MetaProgressionModel.cs` |
| `6fad8374d32458948dcbbaf1e5e36b75363dc77494ad4fe6583cbb392b92d50d` | `Assets/Scripts/Models/Meta/Core/MetaRunProgressTracker.cs` |
| `c10b69c1fb405ce4e1c06124339e4c0c7b7afe3fac2369bba453d5f2f6155b3d` | `Assets/Scripts/Models/Meta/Core/DungeonMetaProgressionSaveData.cs` |

주의: 이 보고서는 정적 production 경로 대조다. 실제 Play Mode에서 완료·저장 왕복·엔드리스 적용을 재현했다는 뜻은 아니다.
