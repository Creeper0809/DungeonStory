# 스크립트 책임/커플링 리팩토링 로그

## 기준

- 함수 인벤토리: [all-scripts-function-inventory.md](all-scripts-function-inventory.md)
- 커플링 감사: [script-coupling-audit.md](script-coupling-audit.md)
- 범위: `Assets/Scripts/**/*.cs`

## 현재 자동 감사 요약

```text
Script files: 324
Extracted declarations: 6311
Misplaced Runtime Editor API Candidates: None
```

자동 추출은 로컬 함수와 일부 특수 선언을 놓칠 수 있으므로, 리팩토링 전에는 반드시 대상 파일을 직접 확인한다.

## 적용한 리팩토링

### 1. TMPKoreanFont의 Editor API 분리

대상:

- `Assets/Scripts/UI/TMPKoreanFont.cs`
- `Assets/Scripts/UI/Editor/TMPKoreanFontEditorResolver.cs`

판단:

- `TMPKoreanFont`는 런타임 UI 코드가 호출하는 폰트 적용 유틸이다.
- 기존 `Resolve()`는 `#if UNITY_EDITOR` 안에서 `UnityEditor.AssetDatabase`를 직접 알고 있었다.
- 빌드는 가능하지만, 런타임 유틸이 Editor 전용 에셋 로딩 경로를 소유하는 것은 책임 위치가 좋지 않다.

변경:

- 런타임 `TMPKoreanFont`는 당시 `TMP_FontAsset` 캐시, `Resources.Load`, `TMP_Settings.defaultFontAsset` fallback만 담당했다. 이후 66번에서 Resources 로딩과 default fallback은 VContainer provider 경계로 분리됐다.
- Editor 전용 `TMPKoreanFontEditorResolver`가 `AssetDatabase.LoadAssetAtPath`로 프로젝트 폰트를 로드하고 `TMPKoreanFont.SetOverrideFont()`로 주입한다.

효과:

- `script-coupling-audit.md`의 `Misplaced Runtime Editor API Candidates`가 `None`으로 내려갔다.
- 런타임 UI 코드에서 `UnityEditor` 의존성이 사라졌다.

### 2. Shop의 직원 전역 검색 분리

대상:

- `Assets/Scripts/Buildings/Shop.cs`
- `Assets/Scripts/Character/Work/WorkforceReplanService.cs`

판단:

- `Shop.RequestIdleWorkersToServeWaitingCheckout()`는 상점 내부에서 모든 `AbilityWork`를 `FindObjectsByType`로 검색하고 즉시 재계획을 요청했다.
- 계산 대기라는 상점 도메인 이벤트는 Shop 책임이지만, 전역 직원 검색과 AI 재계획 요청은 직원 작업 도메인 책임이다.

변경:

- `Shop`에서 전역 검색 함수를 제거했다.
- 직원 재계획 요청을 `WorkforceReplanService.RequestIdleWorkersToReplan()`으로 이동했다.
- Shop은 계산 대기 상태가 생겼을 때 직원 도메인의 재계획 요청 서비스만 호출한다.

효과:

- `Shop`에서 `FindObjectsByType<AbilityWork>` 직접 결합이 사라졌다.
- 전역 직원 재계획 정책이 `Character/Work` 쪽으로 이동해, 이후 캐시/스케줄러/이벤트 기반으로 바꾸기 쉬워졌다.

### 3. StaffWorkPriorityPanel의 작업자 전역 검색 분리

대상:

- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`
- `Assets/Scripts/Character/Work/StaffWorkforceQueryService.cs`

판단:

- `StaffWorkPriorityPanel.FindWorkers()`는 UI 패널 내부에서 모든 `CharacterActor`를 `FindObjectsByType`로 검색했다.
- UI는 작업 우선순위 표 렌더링, 선택 캐릭터 표시, 버튼 이벤트 생성에 집중해야 한다.
- 활성 작업자 판정, 사망/비활성 제외, 사장 우선 정렬, 표시 이름 정렬은 직원 작업 도메인의 조회 정책이다.

변경:

- `StaffWorkforceQueryService.FindActiveWorkers()`를 추가해 활성 작업자 검색과 정렬을 담당하게 했다.
- `StaffWorkforceQueryService.IsActiveWorker()`가 사망 여부와 `AbilityWork` 보유 여부를 판정한다.
- `StaffWorkforceQueryService.GetDisplayName()`으로 UI와 작업자 정렬에서 쓰는 표시 이름 규칙을 통일했다.
- `StaffWorkPriorityPanel`은 조회된 캐릭터를 `WorkerRow`로 변환하고 UI를 그리는 역할만 유지한다.

효과:

- `StaffWorkPriorityPanel`에서 `FindObjectsByType<CharacterActor>` 직접 결합이 사라졌다.
- 작업자 조회 정책이 `Character/Work` 도메인에 모여, 이후 캐시/이벤트 기반 목록 공급자로 교체하기 쉬워졌다.

### 4. AiDirectorContextAggregator의 씬 조회 분리

대상:

- `Assets/Scripts/Character/AI/AiDirectorContextAggregator.cs`
- `Assets/Scripts/Character/AI/AiDirectorContextSceneQuery.cs`

판단:

- `AiDirectorContextAggregator.Build()`는 LLM 프롬프트용 요약을 압축하는 함수인데, 동시에 모든 `CharacterActor`와 `BuildableObject`를 씬에서 직접 검색했다.
- 컨텍스트 압축 로직은 테스트 가능한 순수 계산에 가까워야 하고, 씬 전역 검색은 런타임 조회 경계로 분리되는 편이 낫다.

변경:

- `AiDirectorContextSceneQuery.Capture()`를 추가해 씬 전역 캐릭터/시설 조회를 담당하게 했다.
- `AiDirectorContextSceneSnapshot`을 추가해 집계에 필요한 배열을 명시적으로 전달한다.
- `AiDirectorContextAggregator.Build(CharacterActor, AiDirectorContextSceneSnapshot, int)` overload를 추가해 스냅샷 기반 집계를 지원한다.
- 기존 `Build(CharacterActor, int)` 호출 경로는 유지하되 내부에서 씬 조회 서비스를 통해 스냅샷을 만든다.

효과:

- `AiDirectorContextAggregator`에서 `FindObjectsByType<CharacterActor>`와 `FindObjectsByType<BuildableObject>` 직접 결합이 사라졌다.
- 디렉터 컨텍스트 압축 로직을 테스트에서 가짜 스냅샷으로 검증하기 쉬워졌다.

### 5. UITabManager의 탭 요약 텍스트 생성 분리

대상:

- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/UI/UITabContentTextProvider.cs`

판단:

- `UITabManager`는 상단 탭 버튼 생성, 탭 열기/닫기, 자동 패널 생성, 직원 우선순위 패널 연결을 담당하는 UI 흐름 관리자다.
- 기존 `Build*Text()` 계열 함수는 건물, 직원, 상점, 창고, 운영, 침공, 원정, 연구, 도감 런타임을 직접 조회해 요약 문구를 만들었다.
- 탭 요약 텍스트는 임시 view model/provider 책임에 가깝고, 탭 관리자 안에 있으면 UI 배치/라이프사이클 코드와 도메인 상태 조회가 섞인다.

변경:

- `UITabContentTextProvider.Build(int id)`를 추가해 탭별 요약 텍스트 생성을 담당하게 했다.
- `BuildBuildingManagementText()`, `BuildStaffManagementText()`, `BuildShopText()`, `BuildWarehouseText()`, `BuildOperationText()`, `BuildInvasionDefenseText()`, `BuildOffenseText()`, `BuildResearchCraftingText()`, `BuildCodexRecordText()`를 provider로 이동했다.
- `UITabManager.RefreshGeneratedTab()`은 직원 관리 탭의 특수 패널 처리와 body 텍스트 주입만 남겼다.

효과:

- `UITabManager` 줄 수가 자동 감사 기준 `882`줄에서 `601`줄로 줄었다.
- `UITabManager`에서 `GlobalObjectLookup` 플래그가 사라졌다.
- 도메인 런타임 조회는 아직 `UITabContentTextProvider`에 남아 있으나, 이제 탭 라이프사이클과 분리된 단일 경계라 이후 탭별 provider/view model로 더 쪼개기 쉬워졌다.

### 6. UITabContentTextProvider의 건물/상점/창고 조회 분리

대상:

- `Assets/Scripts/UI/UITabContentTextProvider.cs`
- `Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs`

판단:

- `UITabContentTextProvider`는 탭 내용을 문자열로 조립하는 UI provider다.
- 건물 총수, 방문 가능 시설, 수리 필요 시설, 상점 품절 상태, 창고 재고 합산은 Buildings/Stock 도메인의 요약 조회 책임에 가깝다.
- UI provider가 `BuildableObject`, `Shop`, `IWarehouseFacility`를 직접 `FindObjectsByType`로 찾으면, UI 문자열 변경과 경영 시설 조회 정책이 한 파일에 묶인다.

변경:

- `BuildingManagementSummary`, `ShopManagementSummary`, `WarehouseManagementSummary` 값을 추가했다.
- `BuildingManagementSummaryQuery.CaptureBuildings()`, `CaptureShops()`, `CaptureWarehouses()`를 추가해 씬 조회와 집계 정책을 Buildings 도메인 경계로 이동했다. 이후 92번에서 static capture facade는 제거하고 injected service + 순수 집계 함수만 남겼다.
- 테스트/후속 검증에서 가짜 목록을 넣을 수 있도록 `FromBuildings()`, `FromShops()`, `FromWarehouses()` 순수 집계 함수도 함께 뒀다.
- `UITabContentTextProvider`는 요약 값을 받아 문자열을 만드는 역할만 남겼다.

효과:

- 건물/상점/창고 탭의 씬 전역 조회가 UI provider에서 제거됐다.
- 경영 시설 요약 정책을 나중에 캐시, 이벤트 기반 갱신, 운영일 정산 데이터 기반으로 바꿀 때 UI provider를 건드릴 필요가 줄었다.

### 7. UITabContentTextProvider의 운영/침공 요약 조회 분리

대상:

- `Assets/Scripts/UI/UITabContentTextProvider.cs`
- `Assets/Scripts/Operation/OperationTabSummaryQuery.cs`
- `Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs`

판단:

- 운영 탭은 `UIManager`, `GameData`, `OperatingDaySettlementRuntime`, `EventAlertRuntime`, `RunVariableRuntime`을 직접 읽고 있었다.
- 침공/방어 탭은 `InvasionThreatRuntime`, `InvasionDirectorRuntime`, `InvasionCombatReportRuntime`, `BuildableObject`를 직접 읽고 있었다.
- 이 정보들은 UI 문자열 생성 책임이 아니라 운영/침공 도메인의 현재 상태 요약 책임이다.

변경:

- `OperationTabSummary`와 `OperationTabSummaryQuery.Capture()`를 추가해 날짜/자금/속도/이벤트/정산/런 변수 상태를 한 값으로 묶었다.
- `InvasionDefenseSummary`와 `InvasionDefenseSummaryQuery.Capture()`를 추가해 위협도/침공 후보/활성 침입자/방어 시설/손상 시설/전투 보고서 상태를 한 값으로 묶었다.
- `UITabContentTextProvider`는 두 summary를 받아 텍스트만 만든다.

효과:

- 운영 탭과 침공/방어 탭의 런타임 조회가 UI provider에서 제거됐다.
- `UITabContentTextProvider`에 남은 직접 런타임 조회는 원정/연구/도감 쪽으로 좁혀졌다.

### 8. UITabContentTextProvider의 원정/연구/도감 요약 조회 분리

대상:

- `Assets/Scripts/UI/UITabContentTextProvider.cs`
- `Assets/Scripts/Offense/OffenseTabSummaryQuery.cs`
- `Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs`
- `Assets/Scripts/Codex/CodexRecordSummaryQuery.cs`

판단:

- 원정 탭은 `OffenseWorldMapRuntime`, `OffenseExpeditionRuntime`, `OffenseRewardRuntime`을 직접 읽고 있었다.
- 연구/제작 탭은 `BlueprintResearchRuntime`, `FacilitySynthesisRuntime`을 직접 읽고 있었다.
- 도감/기록 탭은 `CodexRuntime`, `OperatingDaySettlementRuntime`, `EventAlertRuntime`을 직접 읽고 있었다.
- 이 조회들은 각 도메인의 현재 상태 요약이고, UI provider는 표시 문자열 생성에 집중해야 한다.

변경:

- `OffenseTabSummaryQuery.Capture()`를 추가해 정찰 레벨, 스캔 범위, 발견 목표, 선택 목표, 진행 중 원정, 원정 보상을 묶었다.
- `ResearchCraftingSummaryQuery.Capture()`를 추가해 연구 작업 수, 완료 설계도, 진행 중 연구, 선택 합성 재료, 보이는 합성식을 묶었다.
- `CodexRecordSummaryQuery.Capture()`를 추가해 몬스터/침공/시설 도감 항목 수, 이벤트 기록 수, 최근 운영 보고서를 묶었다.
- `UITabContentTextProvider`는 모든 탭에서 summary를 읽어 텍스트만 만드는 형태가 됐다.

효과:

- `UITabContentTextProvider`에서 `FindObjectsByType`, `FindFirstObjectByType`, 런타임 singleton 직접 접근이 제거됐다.
- `UITabContentTextProvider`의 `GlobalObjectLookup`/`SingletonAccess` 플래그 제거를 목표로 할 수 있는 구조가 됐다.

### 9. CodexService의 포맷팅/침입 관찰 해석 책임 분리

대상:

- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Codex/CodexTextFormatter.cs`
- `Assets/Scripts/Codex/CodexInvasionObservationMapper.cs`

판단:

- `CodexService`는 도감 상태 기록, 기준 데이터 import, 관찰 이벤트 반영, 합성/진화 기록, 시설/방어 효과 문구 포맷, 침입 관찰 문장 정규화를 모두 들고 있었다.
- 도감 상태에 무엇을 기록할지 결정하는 책임과, 그 기록을 어떤 문자열로 표현할지/관찰 문장을 어떤 도감 라인으로 매핑할지는 다른 책임이다.
- 특히 방어 효과 포맷과 침입 약점 매핑은 도감 기록 저장소보다 표현/해석 유틸에 가까워, 별도 클래스로 빼는 편이 테스트와 후속 분리에 유리하다.

변경:

- `CodexTextFormatter`를 추가해 시설 역할, 작업 타입, 방어 컨셉, 발동 조건, 대상 규칙, 방어 효과, 진화 변이 태그 문자열 생성을 담당하게 했다.
- `CodexInvasionObservationMapper`를 추가해 방어 효과 태그와 전투 보고서 관찰 문장을 침입 도감 라인으로 정규화하게 했다.
- `CodexService`는 새 helper들을 호출해 도감 엔트리에 무엇을 추가할지 조율하는 역할만 남겼다.

효과:

- `CodexSystem.cs`가 자동 감사 기준 `969`줄에서 `749`줄로 줄었다.
- 도감의 포맷 변경, 방어 효과 표시명 변경, 침입 약점 매핑 변경을 `CodexService` 본체에서 분리했다.
- `CodexService`에서 private 포맷 함수와 침입 관찰 정규화 함수 묶음이 제거되어, 다음 단계에서 `CodexReferenceImporter`, `CodexRecipeRecorder`, `CodexEvolutionRecorder`로 더 나누기 쉬워졌다.

### 10. CodexService의 레시피/진화 기록 책임 분리

대상:

- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Codex/CodexRecipeRecorder.cs`
- `Assets/Scripts/Codex/CodexEvolutionRecorder.cs`
- `Assets/Scripts/Codex/CodexFacilityInfoWriter.cs`

판단:

- 9번 분리 이후에도 `CodexService`에는 연구 완료, 시설 합성, 계보 진화 결과를 도감 라인으로 기록하는 세부 정책이 남아 있었다.
- 연구/합성 기록은 합성식 공개, 특수 조합식 힌트, 기본 구매 해금 문구를 다루므로 도감 관찰 서비스보다 레시피 기록기 책임에 가깝다.
- 계보 진화 기록은 LLM 해석 요약, flavor text, 변이 태그를 도감 히스토리로 남기는 별도 흐름이다.

변경:

- `CodexRecipeRecorder`를 추가해 연구 완료 기록, 합성 결과 기록, 합성식 import, 특수 조합식 힌트를 담당하게 했다.
- `CodexEvolutionRecorder`를 추가해 시설 계보 진화 성공 결과와 LLM 해석/변이 태그 도감 기록을 담당하게 했다.
- `CodexFacilityInfoWriter`를 추가해 시설 엔트리 ID 계산과 시설 도감 라인 추가를 공통화했다.
- `CodexService.RecordResearch`, `RecordSynthesis`, `RecordEvolution`, `ImportSynthesisRecipes`는 외부 호출 호환성을 유지하는 얇은 wrapper가 됐다.

효과:

- `CodexSystem.cs`가 현재 파일 기준 `588`줄로 줄었다.
- 도감의 관찰/이벤트 라우팅, 합성식 기록, 계보 진화 히스토리 기록이 분리되어 후속 테스트와 수정 범위가 작아졌다.
- 남은 큰 책임은 기준 데이터 import와 종족/시설 관찰 기록 쪽으로 좁혀졌다.

### 11. CodexService의 기준 데이터/관찰/침입 기록 책임 분리

대상:

- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Codex/CodexReferenceImporter.cs`
- `Assets/Scripts/Codex/CodexObservationRecorder.cs`
- `Assets/Scripts/Codex/CodexInvasionRecorder.cs`
- `Assets/Scripts/Codex/CodexRecipeRecorder.cs`
- `Assets/Scripts/Codex/CodexEvolutionRecorder.cs`

판단:

- 10번 분리 이후에도 `CodexService`는 `Resources.LoadAll` 기준 데이터 로딩, 종족/시설 관찰 기록, 침입자 도감 seed와 방어/전투 관찰 기록을 직접 들고 있었다.
- 기준 데이터 import는 런타임 도감 서비스가 아니라 catalog/import 책임이다.
- 종족/시설 관찰은 도감 이벤트 라우팅보다 관찰 기록 정책에 가깝고, 침입 도감 기록은 침입 관찰 도메인으로 분리하는 편이 변경 범위가 작다.

변경:

- `CodexReferenceImporter`를 추가해 종족/시설 기준 데이터 로딩과 초기 합성식/침입자 seed import를 담당하게 했다.
- `CodexObservationRecorder`를 추가해 캐릭터 방문, 종족 정보, 시설/방어 시설 정보 관찰 기록을 담당하게 했다.
- `CodexInvasionRecorder`를 추가해 방어 발동 관찰, 침입 전투 보고서, 시설 피해, 돌파형 침입자 seed 기록을 담당하게 했다.
- `CodexRecipeRecorder`와 `CodexEvolutionRecorder`가 더 이상 `CodexService.ObserveFacility`를 우회 호출하지 않고 `CodexObservationRecorder`를 직접 사용하게 했다.
- `CodexService`는 외부 호출 호환성을 유지하는 얇은 wrapper로 축소했다.

효과:

- `CodexSystem.cs`가 현재 파일 기준 `391`줄로 줄었다.
- `CodexSystem.cs`의 `ResourcesAccess` 플래그가 제거되고, 해당 의존은 `CodexReferenceImporter`로 이동했다.
- 기준 데이터 import, 관찰 기록, 침입 기록, 레시피 기록, 진화 기록의 책임 경계가 분리되어 도감 변경 시 수정 범위가 명확해졌다.

### 12. RunVariableRuntime의 모델/카탈로그/시작 후보/효과 계산 책임 분리

대상:

- `Assets/Scripts/Run/RunVariableSystem.cs`
- `Assets/Scripts/Run/RunVariableModel.cs`
- `Assets/Scripts/Run/RunVariableEvents.cs`
- `Assets/Scripts/Run/RunVariableCatalog.cs`
- `Assets/Scripts/Run/RunStartVariableSelector.cs`
- `Assets/Scripts/Run/RunVariableEffects.cs`

판단:

- 기존 `RunVariableSystem.cs`는 런 변수 모델, 이벤트 DTO, 정적 정의 카탈로그, `Resources.LoadAll` 기반 시작 후보 샘플링, 런타임 이벤트 리스너, 상점/재고/침입 효과 계산을 한 파일에 모두 들고 있었다.
- 시작 후보 샘플링은 `Resources`와 메타 진행 보너스를 읽는 catalog/select 책임이고, `RunVariableRuntime`의 일일 이벤트 수명주기와 섞이면 테스트/런타임 결합이 커진다.
- 상점 비용, 손님 수요, 위협 상승, 침입자 설정 보정은 런타임 MonoBehaviour가 아니라 현재 `RunVariableState`를 읽는 순수 효과 계산 책임에 가깝다.
- 런 변수 이벤트 struct는 전역 이벤트 버스 경계이므로 모델/상태 객체와 분리해 두는 편이 책임이 명확하다.

변경:

- `RunVariableModel`을 추가해 `RunVariableCategory`, `RunVariableId`, `RunStartVariableSnapshot`, `RunVariableDefinition`, `ActiveRunVariable`, `RunVariableState`를 담당하게 했다.
- `RunVariableEvents`를 추가해 `RunStartVariablesSelectedEvent`, `RunVariableActivatedEvent`, `RunVariableExpiredEvent`, `InvasionVariableSelectedEvent`를 담당하게 했다.
- `RunVariableCatalog`를 추가해 운영/침입 변수 정의와 category 조회를 담당하게 했다.
- `RunStartVariableSelector`를 추가해 `Resources.LoadAll` 기반 시작 시설/손님/설계도 후보 샘플링과 초기 레이아웃 결정을 담당하게 했다.
- `RunVariableEffects`를 추가해 손님 수요, 재고/상점/설계도 가격, 위협 상승, 경고 임계값, 침입자 설정 보정을 담당하게 했다.
- `RunVariableRuntime`은 외부 호출 호환성을 유지하면서 이벤트 수명주기, 활성화/선택 트리거, alert 발행, singleton 접근 wrapper만 남겼다.

효과:

- `RunVariableSystem.cs`가 현재 파일 기준 `253`줄로 줄었다.
- `RunVariableSystem.cs`의 `ResourcesAccess` 플래그가 제거되고, 해당 의존은 `RunStartVariableSelector`로 이동했다.
- 비용/침입 보정 계산은 `RunVariableEffects`로 이동해 상점/침입 시스템이 호출하는 기존 public API는 유지하면서 내부 계산 책임만 분리됐다.
- 런 변수 정적 데이터, 런 상태 모델, 이벤트 버스 DTO, 런타임 MonoBehaviour 경계가 분리되어 후속 변경 시 수정 범위가 좁아졌다.

### 13. InvasionIntruderSystem의 모델/이벤트/경로/생성/피해 대상 책임 분리

대상:

- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Invasion/InvasionIntruderModel.cs`
- `Assets/Scripts/Invasion/InvasionIntruderEvents.cs`
- `Assets/Scripts/Invasion/InvasionIntruderPlanner.cs`
- `Assets/Scripts/Invasion/InvasionIntruderDataResolver.cs`
- `Assets/Scripts/Invasion/InvasionIntruderEntryResolver.cs`
- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`
- `Assets/Scripts/Invasion/InvasionFacilityDamageResolver.cs`

판단:

- 기존 `InvasionIntruderSystem.cs`는 설정/상태 모델, 침입 이벤트 DTO, 경로 탐색 플래너, 침입자 데이터 `Resources.Load`, 입구 해석, 런타임 오브젝트 보정, 시설 피해 대상 탐색, 침입자 코루틴 상태 전이를 한 파일에 모두 들고 있었다.
- 경로 플래너는 `GridSystemManager.Instance`를 직접 조회하고 있어 순수 경로 계산 책임과 씬 전역 조회가 섞여 있었다.
- 침입자 prefab/오브젝트 보정은 scene mutation/factory 책임이고, director의 후보 이벤트 처리와 섞이면 생성 정책 변경 시 런타임 흐름까지 흔들린다.
- 피해 대상 탐색은 "무엇을 때릴 수 있는가"의 규칙이고, 실제 `SetDamaged`/이벤트 발행과 분리하면 침입자 행동 수정 범위가 작아진다.

변경:

- `InvasionIntruderModel`을 추가해 `InvasionIntruderSettings`와 `InvasionIntruderState`를 담당하게 했다.
- `InvasionIntruderEvents`를 추가해 `InvasionSpawnedEvent`, `InvasionFacilityDamagedEvent`, `InvasionFinalCombatStartedEvent`를 담당하게 했다.
- `InvasionIntruderPlanner`를 추가하고 `IsAtOwner`가 `Grid`를 직접 인자로 받게 고쳐 플래너의 `GridSystemManager.Instance` 의존을 제거했다.
- `InvasionIntruderDataResolver`를 추가해 기본 침입자 `Resources.Load`를 담당하게 했다.
- `InvasionIntruderEntryResolver`를 추가해 `CharacterSpawner` 기반 입구 해석과 grid fallback을 담당하게 했다.
- `InvasionIntruderFactory`를 추가해 침입자 GameObject의 `CharacterActor`, `AbilityMove`, collider, visual, runtime component 보정을 담당하게 했다.
- `InvasionFacilityDamageResolver`를 추가해 인접한 손상 가능 시설 탐색 규칙을 담당하게 했다.
- Unity 직렬화 안정성을 위해 `InvasionDirectorRuntime`와 `InvasionIntruderRuntime` MonoBehaviour는 원래 파일에 유지했다.

효과:

- `InvasionIntruderSystem.cs`가 현재 파일 기준 `356`줄로 줄었다.
- `InvasionIntruderSystem.cs`의 `ResourcesAccess`와 `Reflection` 플래그가 제거되고, 당시 `ResourcesAccess`는 `InvasionIntruderDataResolver`로 이동했다. 이후 65번에서 이 로딩 책임도 VContainer provider 경계로 분리됐다.
- 경로 플래너가 씬 singleton을 직접 읽지 않게 되어 path/focus 계산 테스트 범위가 좁아졌다.
- 침입자 생성 보정, 입구 해석, 피해 대상 탐색이 분리되어 침입자 행동과 생성 정책을 따로 수정할 수 있게 됐다.

### 14. Offense 월드맵/원정 시스템의 모델/서비스/이벤트/UI 생성 책임 분리

대상:

- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`
- `Assets/Scripts/Offense/OffenseWorldMapModel.cs`
- `Assets/Scripts/Offense/OffenseWorldMapService.cs`
- `Assets/Scripts/Offense/OffenseWorldMapEvents.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Offense/OffenseExpeditionModel.cs`
- `Assets/Scripts/Offense/OffenseExpeditionService.cs`
- `Assets/Scripts/Offense/OffenseExpeditionEvents.cs`
- `Assets/Scripts/Offense/OffensePanelUiFactory.cs`

판단:

- 기존 `OffenseWorldMapSystem.cs`는 원정 대상 enum/보상 preview, target definition/snapshot, 정찰 상태, 기본 타깃 카탈로그, 정찰/선택 이벤트, 런타임 singleton, 임시 UI 생성까지 한 파일에 모두 들고 있었다.
- 기존 `OffenseExpeditionSystem.cs`는 원정 결과 모델, 진행 중 원정 상태, 원정 가능 인력/전투력/피해 계산, 시작/완료 이벤트, 런타임 singleton, 편성 UI 생성을 한 파일에 같이 들고 있었다.
- target snapshot과 expedition result의 문자열 포맷은 도메인 모델 책임이고, 런타임 MonoBehaviour가 소유하면 UI/씬 수명주기와 데이터 표현이 불필요하게 묶인다.
- 정찰 범위, 기본 target pool, 원정 성공/피해 계산은 순수 서비스 책임이다. 씬 오브젝트 조회와 같이 있으면 시나리오 테스트와 밸런스 조정의 수정 범위가 커진다.
- 월드맵/원정 패널은 같은 방식의 Canvas/Text/Button 생성 코드를 중복하고 있었고, 이는 UI factory/lifetime 책임으로 분리하는 편이 좋다.
- Unity 직렬화 안정성을 위해 `OffenseWorldMapRuntime`, `OffenseWorldMapPanel`, `OffenseExpeditionRuntime`, `OffenseExpeditionPanel` MonoBehaviour는 원래 파일에 유지했다.

변경:

- `OffenseWorldMapModel`을 추가해 `OffenseTargetKind`, `OffenseRewardCategory`, `OffenseRewardPreview`, `OffenseTargetDefinition`, `OffenseTargetSnapshot`, `OffenseWorldMapState`를 담당하게 했다.
- `OffenseWorldMapService`를 추가해 정찰 범위, 기본 원정 대상 pool, target 정규화, 공개/조회 규칙을 담당하게 했다.
- `OffenseWorldMapEvents`를 추가해 월드맵 변경, 원정 대상 선택, 정찰 강화 이벤트 DTO와 `EventObserver` 경계를 담당하게 했다.
- `OffenseExpeditionModel`을 추가해 원정 멤버 snapshot, 원정 결과, 진행 중 원정 run 상태를 담당하게 했다.
- `OffenseExpeditionService`를 추가해 원정 가능 인력 판정, 전투력 계산, 성공/피해/보상 preview 결과 생성을 담당하게 했다.
- `OffenseExpeditionEvents`를 추가해 원정 시작/완료 이벤트 DTO와 이벤트 버스 경계를 담당하게 했다.
- `OffensePanelUiFactory`를 추가해 오펜스 패널의 Canvas, 기본 panel, vertical root, TMP text, button 생성 책임을 공통화했다.

효과:

- `OffenseWorldMapSystem.cs`가 현재 파일 기준 `319`줄로 줄고, 월드맵 런타임/패널 orchestration에 집중하게 됐다.
- `OffenseExpeditionSystem.cs`가 현재 파일 기준 `378`줄로 줄고, 원정 시작/완료 orchestration과 편성 패널에 집중하게 됐다.
- 정찰 카탈로그와 원정 계산은 MonoBehaviour 없이 테스트 가능한 서비스로 분리됐다.
- 이벤트 버스 직접 호출은 `OffenseWorldMapEvents`와 `OffenseExpeditionEvents`로 이동했다.
- 런타임 UI 오브젝트 생성은 `OffensePanelUiFactory`로 모여 월드맵/원정 패널의 중복 생성 코드가 줄었다.

### 15. OffenseRewardSystem의 보상 모델/이벤트/선택/컨텍스트 조회 책임 분리

대상:

- `Assets/Scripts/Offense/OffenseRewardSystem.cs`
- `Assets/Scripts/Offense/OffenseRewardModel.cs`
- `Assets/Scripts/Offense/OffenseRewardEvents.cs`
- `Assets/Scripts/Offense/OffenseRewardSelector.cs`
- `Assets/Scripts/Offense/OffenseRewardContextResolver.cs`

판단:

- 기존 `OffenseRewardSystem.cs`는 보상 결과 DTO, 누적 보상 상태, 지급 컨텍스트, 보상 지급 이벤트, 지급 규칙, `Resources.LoadAll` 기반 희귀 시설/설계도 선택, 씬 전역 context 조회, 런타임 적용을 한 파일에 모두 들고 있었다.
- 보상 결과와 상태는 순수 모델인데, `Resources`와 scene lookup을 함께 가진 파일에 있으면 보상 UI/정산/원정 결과가 모두 같은 파일 변경에 묶인다.
- 희귀 시설과 설계도 후보 선택은 에셋 카탈로그 조회 책임이고, 실제 지급 규칙과 섞이면 보상 밸런스 조정과 에셋 선택 정책 변경이 서로 엮인다.
- `GameManager`, `BlueprintResearchRuntime`, `DailyFacilityShopRuntime`, 창고 조회는 런타임 context 조립 책임이다. 보상 지급 서비스가 직접 씬을 뒤지면 서비스 테스트 범위가 넓어진다.
- 이벤트 struct는 전역 이벤트 버스 경계이므로 모델/서비스와 분리해 두는 편이 책임이 명확하다.
- Unity 직렬화 안정성을 위해 `OffenseRewardRuntime` MonoBehaviour는 원래 파일에 유지했다.

변경:

- `OffenseRewardModel`을 추가해 `OffenseRewardGrantResult`, `OffenseRewardState`, `OffenseRewardContext`를 담당하게 했다.
- `OffenseRewardEvents`를 추가해 `OffenseRewardGrantedEvent`와 `EventObserver` 경계를 담당하게 했다.
- `OffenseRewardSelector`를 추가해 재고 카테고리 해석, 희귀 시설 선택, 설계도 선택, 세력 약화 대상 판정, label helper를 담당하게 했다.
- `OffenseRewardContextResolver`를 추가해 `GameManager`, 연구 런타임, 시설 상점 런타임, 창고 조회와 debug context 병합을 담당하게 했다.
- `OffenseRewardService`는 보상 종류별 지급 흐름, 상태 기록, 성공/실패 결과 생성만 남겼다.
- `OffenseRewardRuntime`은 원정 결과에서 context를 만들고 보상 지급 결과 이벤트를 발행하는 orchestration만 남겼다.

효과:

- `OffenseRewardSystem.cs`가 현재 파일 기준 `287`줄로 줄었다.
- `OffenseRewardSystem.cs`의 `ResourcesAccess`와 직접 scene context 조회 책임이 제거됐다.
- `ResourcesAccess`는 `OffenseRewardSelector`로 이동했고, 씬 singleton/global lookup은 `OffenseRewardContextResolver`로 모였다.
- 보상 결과/상태 모델, 이벤트 버스 DTO, 에셋 후보 선택, 런타임 context 수집, 보상 적용 흐름이 분리되어 후속 보상 타입 추가와 선택 정책 변경의 수정 범위가 작아졌다.

### 16. CharacterAiPlanDebugScenarios의 fixture/action stub 책임 분리

대상:

- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanProbeActions.cs`

판단:

- `CharacterAiPlanDebugScenarios.cs`는 AI 시나리오 검증, Play Mode probe 상태, 테스트 캐릭터/건물 생성, 씬 런타임 singleton 보정, 임시 `AIActionSet` 구현체까지 한 파일에 담고 있었다.
- `Probe*ActionSet` 클래스들은 실제 검증 시나리오가 아니라 utility/BT/JobGiver 경로를 자극하기 위한 테스트 더블이다. 시나리오 파일 내부에 있으면 검증 의도와 액션 stub 구현이 섞인다.
- 테스트용 actor/grid/queue/social runtime 생성과 임시 `CharacterSO`/`BuildingSO` 생성은 fixture 책임이다. 시나리오 함수가 직접 계속 소유하면 전역 조회, reflection, scene object 생성이 검증 본문 곳곳에 퍼진다.
- 메뉴 항목과 시나리오 본문은 원래 파일에 남기고, 반복 fixture와 액션 stub만 분리해 Unity Editor 메뉴/기존 호출 경로를 깨지 않게 했다.

변경:

- `CharacterAiPlanProbeActions`를 추가해 `ProbeExitDungeonActionSet`, `ProbeEatActionSet`, `ProbeWorkActionSet`, `ProbeShoppingActionSet`, `ProbeLookAroundActionSet`, `ProbeWaitActionSet`, `ProbeVolatileWorkActionSet`, `ProbeContinuableWorkActionSet`, `ProbeOneShotWorkActionSet`를 담당하게 했다.
- `CharacterAiPlanDebugFixtures`를 추가해 Play Mode director lookup, actor 생성, probe object 파괴, grid 보정, `SocialReputationRuntime`/`LocalLlmRequestQueue` 보정, 임시 캐릭터/건물 SO 생성을 담당하게 했다.
- `CharacterAiPlanDebugScenarios.cs`는 메뉴, probe 상태, 시나리오 실행/검증 함수, Behavior Designer 그래프 검증 함수에 집중하도록 줄였다.

효과:

- `CharacterAiPlanDebugScenarios.cs`가 현재 파일 기준 `4406`줄에서 `3894`줄로 줄었다.
- 테스트 더블 액션은 runtime scene/global lookup flag가 없는 `CharacterAiPlanProbeActions.cs`로 분리됐다.
- 전역 조회, singleton 보정, reflection 기반 `Awake` 호출, 임시 scene object 생성은 `CharacterAiPlanDebugFixtures.cs`로 모였다.
- 이 변경은 전체 AI 검증 파일을 끝까지 분해한 것은 아니며, 다음 단계로 Play Mode LLM probe 상태와 Behavior Designer layout assertion을 추가 분리할 여지가 남아 있다.

### 17. CharacterAiPlanDebugScenarios의 테스트 씬 준비 책임 분리

대상:

- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs`

판단:

- `PrepareTestScene()` 메뉴는 사용자가 호출하는 진입점이지만, `AssetDatabase.Refresh`, 프리팹 로드/저장, SampleScene 복사, Scene marker 생성, 런타임 컴포넌트 보정, scene 저장까지 직접 들고 있었다.
- 이 로직은 AI 검증 시나리오가 아니라 Editor test scene 구성 책임이다. 시나리오 검증 파일에 남아 있으면 `MenuItem` 진입점, scene mutation, prefab mutation, 테스트 환경 보정이 한 파일에 섞인다.
- 테스트 씬 준비는 Unity Editor API 의존성이 강하므로, 별도 Editor helper에 모아 두는 편이 이후 시나리오 suite 분리와 Play Mode probe 분리를 쉽게 만든다.

변경:

- `CharacterAiPlanTestScenePreparer`를 추가해 테스트 씬 준비 orchestration과 세부 helper를 담당하게 했다.
- `PrepareTestScene()`은 `CharacterAiPlanTestScenePreparer.Prepare()` 호출과 결과 로그 출력만 남겼다.
- `CharacterAiPlanDebugScenarios.cs`에서 `AssetDatabase`, `PrefabUtility`, `EditorSceneManager`, `EditorUtility`, `SceneManager` 직접 사용을 제거했다.

효과:

- `CharacterAiPlanDebugScenarios.cs`가 현재 파일 기준 `3894`줄에서 `3735`줄로 줄었다.
- 프리팹/씬 변경 책임이 `CharacterAiPlanTestScenePreparer.cs`로 모여, 검증 시나리오 파일의 직접 Editor asset/scene mutation 코드가 줄었다.
- 남은 큰 책임은 Play Mode LLM probe 상태와 Behavior Designer layout assertion 묶음이다.

## 최근 검증

```text
RunVariableDebugScenarios: Passed
InvasionIntruderDebugScenarios: Passed
OffenseWorldMapDebugScenarios: Passed
OffenseExpeditionDebugScenarios: Passed
OffenseRewardDebugScenarios: Passed
Generated: 2026-07-04 09:27:05
Suites: 29
Passed: 29
Failed: 0
Console errors: 0
Console warnings: 7
```

경고는 기존 컴파일 경고다.

추가 확인:

```text
CharacterAiPlanDebugScenarios fixture/action split: Unity MCP scenario passed
Evidence: Unity MCP compiled and executed CharacterAiPlanDebugScenarios.RunAll()
Generated: 2026-07-04 10:36:05 +09:00
Unity MCP CharacterAiPlanDebugScenarios: Passed
Unity Console: 0 errors, 2 warnings
```

```text
CharacterAiPlan test scene preparer split: Unity MCP project compile passed
Generated: 2026-07-04 10:36:05 +09:00
Unity MCP compile evidence: CharacterAiPlanDebugScenarios.RunAll() command compiled the project successfully after the preparer split
Prepare invocation: Not run because it mutates the editor scene
Reflection-only check: Blocked by Unity MCP command namespace policy
```

### 18. MetaProgressionSystem의 모델/카탈로그/계산/이벤트/UI 책임 분리

대상:

- `Assets/Scripts/Meta/MetaProgressionSystem.cs`
- `Assets/Scripts/Meta/MetaProgressionModel.cs`
- `Assets/Scripts/Meta/MetaProgressionCatalog.cs`
- `Assets/Scripts/Meta/MetaProgressionCalculator.cs`
- `Assets/Scripts/Meta/MetaProgressionEvents.cs`
- `Assets/Scripts/Meta/RunResultPanel.cs`

판단:

- 기존 `MetaProgressionSystem.cs`는 메타 강화 enum/정의, 강화 카탈로그, 영구 상태, 런 결과 DTO/표시 문자열, 계승 재화 계산, 런 결과 이벤트, 런타임 이벤트 수집, 런 결과 UI 패널 생성까지 모두 갖고 있었다.
- 강화 모델과 계산은 순수 규칙에 가깝고, `MonoBehaviour` 런타임이나 UI 생성과 같은 파일에 있으면 보상 밸런스 조정, UI 변경, 런 이벤트 수집 변경이 모두 같은 파일에 묶인다.
- `RunResultReadyEvent`는 전역 이벤트 버스 경계이므로 모델/런타임 본문과 분리하는 편이 책임이 명확하다.
- `RunResultPanel`은 `Canvas`, `Image`, `TextMeshProUGUI`를 생성하는 UI 책임이다. 메타 진행 런타임이 직접 갖고 있으면 런 결과 계산과 UI hierarchy 생성이 커플링된다.
- Unity 직렬화 안정성을 위해 `MetaProgressionRuntime` MonoBehaviour는 원래 파일에 유지했다.

변경:

- `MetaProgressionModel`을 추가해 `MetaProgressionBranch`, `MetaUpgradeId`, `MetaUpgradeDefinition`, `MetaProgressionState`, `RunResultSnapshot`을 담당하게 했다.
- `MetaProgressionCatalog`를 추가해 강화 정의 풀과 조회를 담당하게 했다.
- `MetaProgressionCalculator`를 추가해 계승 재화 계산 규칙을 담당하게 했다.
- `MetaProgressionEvents`를 추가해 `RunResultReadyEvent`와 `EventObserver` 경계를 담당하게 했다.
- `RunResultPanel`을 추가해 런 결과 패널 조회/생성/렌더링을 담당하게 했다.
- `MetaProgressionSystem.cs`에는 `MetaProgressionRuntime`의 런 이벤트 수집, 상태 갱신, 결과 생성 orchestration만 남겼다.

효과:

- `MetaProgressionSystem.cs`가 현재 파일 기준 `716`줄에서 `339`줄로 줄었다.
- `MetaProgressionSystem.cs`에서 `TMP_Text`, `Canvas`, `Image`, `TextMeshProUGUI` 직접 의존이 제거됐다.
- `MetaProgressionSystem.cs`의 `Reflection`, `RuntimeObjectCreation`, `SceneMutation` 책임이 제거되고, UI 생성 책임은 `RunResultPanel.cs`로 이동했다.
- 강화/런 결과 모델, 카탈로그, 보상 계산, 이벤트 DTO, UI 패널, 런타임 orchestration이 분리되어 후속 메타 보상 추가와 UI 변경의 수정 범위가 작아졌다.

검증:

```text
MetaProgression split: Unity MCP scenario passed
Generated: 2026-07-04 10:34:08 +09:00
Unity MCP MetaProgressionDebugScenarios: Passed
Unity Console: 0 errors, 2 warnings
```

### 19. EventAlertSystem의 모델/이벤트/서비스/정산 브리지 책임 분리

대상:

- `Assets/Scripts/Operation/EventAlertSystem.cs`
- `Assets/Scripts/Operation/EventAlertModel.cs`
- `Assets/Scripts/Operation/EventAlertEvents.cs`
- `Assets/Scripts/Operation/EventAlertService.cs`
- `Assets/Scripts/Operation/EventAlertMergePolicy.cs`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`
- `Assets/Scripts/Operation/EventAlertLayout.cs`
- `Assets/Scripts/Operation/OperatingDayReportAlertBridge.cs`

판단:

- 기존 `EventAlertSystem.cs`는 알림 중요도 enum, 선택지/요청/기록 모델, 전역 이벤트 DTO, 알림 발행 서비스, 런타임 UI, 운영일 정산 보고서 브리지를 모두 들고 있었다.
- 알림 모델은 순수 데이터와 표시 문자열 정책이고, 이벤트 DTO는 전역 이벤트 버스 경계이며, 발행 서비스는 다른 도메인이 호출하는 integration API다.
- 운영일 정산 보고서를 알림으로 바꾸는 로직은 정산 도메인과 알림 UI 사이의 adapter에 가깝다. UI 런타임 파일에 남아 있으면 정산 문구 변경과 알림 버튼/상세 패널 변경이 한 파일에 묶인다.
- `EventAlertRuntime`에는 알림 수신/기록/선택 실행과 버튼/상세 UI 생성이 함께 남아 있었고, UI hierarchy 생성 함수들은 알림 런타임의 상태 관리보다 UI factory 책임에 가깝다.
- `EventAlertUiFactory` 안에서도 버튼 생성과 viewport/row 배치 계산이 섞이면 UI 오브젝트 생성 변경과 레이아웃 튜닝이 같은 파일에 묶인다.

변경:

- `EventAlertModel`을 추가해 `EventAlertImportance`, `EventAlertChoice`, `EventAlertRequest`, `EventAlertRecord`를 담당하게 했다.
- `EventAlertEvents`를 추가해 `EventAlertRequestedEvent`, `EventAlertLoggedEvent` 전역 이벤트 경계를 담당하게 했다.
- `EventAlertService`를 추가해 알림 발행 helper와 카테고리별 발행 API를 담당하게 했다.
- `EventAlertMergePolicy`를 추가해 선택지가 있는 알림은 병합하지 않고, 제목/카테고리/중요도/상세가 같은 선택지 없는 알림만 병합하는 정책을 담당하게 했다.
- `EventAlertUiFactory`를 추가해 Canvas 조회/생성, runtime root, 알림 버튼, 선택지 버튼, 상세 패널, 스크롤 viewport 구성, 버튼 layout 계산을 담당하게 했다.
- `EventAlertLayout`을 추가해 알림 버튼 크기, 스크롤 viewport 위치/높이, 버튼 row 배치를 담당하게 했다.
- `OperatingDayReportAlertBridge`를 추가해 운영일 정산 보고서를 알림으로 변환하는 adapter를 담당하게 했다.
- `EventAlertSystem.cs`에는 `EventAlertRuntime`의 알림 수신, 기록 갱신, 선택 실행, UI factory 호출 orchestration만 남겼다.

효과:

- `EventAlertSystem.cs`가 현재 파일 기준 `757`줄에서 `211`줄로 줄었다.
- `EventAlertSystem.cs` 선언 수가 `73`개에서 `17`개로 줄었다.
- `EventAlertUiFactory.cs`가 layout helper 분리 후 `429`줄에서 `292`줄로 줄었다.
- 알림 모델, 이벤트 DTO, 발행 서비스, merge 정책, UI factory, layout helper, 정산 보고서 adapter가 런타임 본문에서 분리됐다.
- `EventAlertSystem.cs`의 `Reflection`, `GlobalObjectLookup`, `RuntimeObjectCreation` 플래그가 제거됐고, UI hierarchy 생성/Canvas 조회 책임은 `EventAlertUiFactory.cs`로 이동했다.
- 후속 단계에서는 `EventAlertRuntime`에 남은 선택지 버튼 lifetime과 상세 패널 상태를 별도 presenter로 더 세분화할 수 있다.

검증:

```text
EventAlert split: Unity MCP scenario passed
Generated: 2026-07-04 10:32:45 +09:00
Static file check: EventAlert model/events/service/merge policy/UI factory/layout/bridge types are unique and EventAlertRuntime remains in EventAlertSystem.cs
Unity MCP EventAlertDebugScenarios: Passed
Unity Console: 0 errors, 2 warnings
```

### 20. VContainer composition root와 운영 탭 DI 경계 도입

대상:

- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Infrastructure/DungeonSceneRuntimeReferences.cs`
- `Assets/Scripts/Operation/OperationTabSummaryQuery.cs`
- `Assets/Scripts/UI/UITabContentTextProvider.cs`
- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/Run/RunVariableSystem.cs`

진단:

- VContainer 패키지는 설치됐지만 런타임 코드가 아직 DI 컨테이너를 사용하지 않았다.
- `UITabManager`는 static `UITabContentTextProvider.Build(id)`에 직접 묶여 있었고, 운영 탭 요약은 static query 안에서 UIManager/정산 런타임/알림 런타임/런 변수 런타임을 직접 조회했다.
- `OperationTabSummaryQuery`는 탭 텍스트 생성과 런타임 씬 조회 사이의 경계가 없어, 이후 탭별 provider를 주입식으로 바꾸기 어려웠다.

변경:

- VContainer UPM git dependency `jp.hadashikick.vcontainer`를 추가했다.
- `DungeonRuntimeLifetimeScope`를 추가해 씬 로드 후 VContainer scope를 만들고, `DungeonSceneRuntimeReferences`, `IUITabContentTextProvider`, `IOperationTabSummaryService`, `IOperationTabSummaryRuntimeSource`를 등록하게 했다.
- `DungeonSceneRuntimeReferences`를 추가해 UIManager, 정산 런타임, 이벤트 알림 런타임, 런 변수 런타임 참조를 immutable bundle로 주입하게 했다.
- `UITabManager`에 `[Inject] Construct(IUITabContentTextProvider)`를 추가하고, 주입 누락 시 fallback 없이 명시 예외를 내게 했다.
- `UITabContentTextProvider`를 static class에서 `IUITabContentTextProvider` 구현체로 바꾸고, 운영 탭만 `IOperationTabSummaryService`를 생성자 주입받게 했다.
- `OperationTabSummaryQuery`의 실제 요약 계산을 `OperationTabSummaryService`로 옮기고, 씬 런타임 참조 수집은 `DungeonRuntimeLifetimeScope` composition root에 격리했다.
- `RunVariableRuntime.Current`를 추가해 DI 런타임 소스가 `Instance` 조회를 트리거하지 않고 현재 런타임만 읽을 수 있게 했다.

효과:

- VContainer가 실제 런타임 UI 경로에 연결됐다.
- `UITabContentTextProvider`는 운영 탭 요약을 직접 조회하지 않고 주입된 서비스에 위임한다.
- `OperationTabSummaryQuery`와 `OperationTabSummaryRuntimeSource`의 직접 `FindFirstObjectByType`/singleton 접근은 제거됐고, 남은 씬 조회는 `DungeonRuntimeLifetimeScope` composition root 경계로 이동했다.
- 아직 완전한 DI는 아니다. 로드된 씬 컴포넌트 수집 경계는 `DungeonSceneComponentQuery`로 모았지만, 다음 단계에서는 주요 런타임을 명시 scene binding 또는 prefab composition root로 더 올려야 한다.

검증:

```text
VContainer package install: Passed
Unity PackageCache: jp.hadashikick.vcontainer resolved
Unity domain reflection: VContainer, LifetimeScope, and IContainerBuilder loaded
Temporary runtime DI probe: OperationDI ok; hasRunVariables=False; textLength=153
Final scope path probe: OperationTabSummaryQuery.Capture resolved through DungeonRuntimeLifetimeScope
Operation runtime source lookup split: scene lookup removed from OperationTabSummaryRuntimeSource
Unity MCP refresh after probe removal: Passed
Unity Console: 0 errors, 7 existing warnings
Generated: 2026-07-04 11:22:00 +09:00
```

### 21. 탭 요약 query 서비스 DI 확장

대상:

- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs`
- `Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs`
- `Assets/Scripts/Offense/OffenseTabSummaryQuery.cs`
- `Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs`
- `Assets/Scripts/Codex/CodexRecordSummaryQuery.cs`
- `Assets/Scripts/UI/UITabContentTextProvider.cs`

진단:

- Operation 탭만 DI 서비스 경계로 이동했고, 건물/침공/원정/연구/도감 탭 요약은 여전히 static query 내부에서 singleton 또는 씬 전체 조회를 직접 사용했다.
- `UITabContentTextProvider`가 UI 문자열 생성 책임과 런타임 조회 책임을 동시에 잡고 있어, 탭 하나를 테스트하려 해도 씬 상태와 전역 singleton에 묶였다.

변경:

- `IDungeonSceneComponentQuery`/`DungeonSceneComponentQuery`를 추가해 loaded scene component 조회를 공통 경계로 모았다.
- 건물/상점/창고, 침공/방어, 원정, 연구/합성, 도감/기록 요약을 각각 `RuntimeSource` + `SummaryService`로 분리했다.
- `DungeonRuntimeLifetimeScope`에 각 요약 runtime source/service를 등록했다.
- `UITabContentTextProvider`는 static query 호출 대신 생성자로 받은 summary service들을 호출한다.
- 기존 static `*SummaryQuery.Capture()` entry point는 fallback 없이 `DungeonRuntimeLifetimeScope.TryResolve(...)`만 사용하게 유지했다.

효과:

- `BuildingManagementSummaryQuery`, `InvasionDefenseSummaryQuery`, `OffenseTabSummaryQuery`, `ResearchCraftingSummaryQuery`, `CodexRecordSummaryQuery`에서 직접 `Instance`, `FindFirstObjectByType`, `FindObjectsByType` 사용을 제거했다.
- 씬 조회는 `DungeonSceneComponentQuery` 한 곳으로 모였고, 탭 provider는 요약 서비스 조합자가 됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 48 -> 46, `SingletonAccess`는 75 -> 71, `DependencyInjection`은 5 -> 11로 이동했다.

검증:

```text
Unity MCP refresh after tab summary DI expansion: Passed
Targeted direct lookup scan: only DungeonRuntimeLifetimeScope and DungeonSceneComponentQuery retain scene lookup boundaries
git diff --check on touched files: Passed, LF->CRLF warnings only
Unity Console after final refresh: 0 errors, 7 existing warnings
Generated: 2026-07-04 11:37:13 +09:00
```

### 22. 직원 조회와 탭 popup 호출 DI 경계 확장

대상:

- `Assets/Scripts/Character/Work/StaffWorkforceQueryService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/UI/UITabContentTextProvider.cs`
- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/UI/UiPopupService.cs`

진단:

- 직원 탭과 직원 우선순위 패널이 `StaffWorkforceQueryService.FindActiveWorkers()` static 경로를 통해 씬 전체 캐릭터 검색에 묶여 있었다.
- `UITabManager`는 VContainer로 content provider를 받으면서도 탭 열기/닫기에서는 `UIManager.Instance`를 직접 호출했다.

변경:

- `IStaffWorkforceQueryService`와 `StaffWorkforceRuntimeQueryService`를 추가하고, 직원 씬 조회는 `IDungeonSceneComponentQuery`를 통해 수행하게 했다.
- 기존 `StaffWorkforceQueryService` static entry point는 compatibility layer로 남겼지만, 이후 91번에서 제거했다.
- `UITabContentTextProvider`는 직원 탭 요약도 `IStaffWorkforceQueryService` 주입으로 만든다.
- `IUiPopupService`/`UiPopupService`를 추가해 `UITabManager`의 `UIManager.Instance` 직접 접근을 제거했다.
- `DungeonRuntimeLifetimeScope`에 직원 조회 서비스와 popup 서비스를 등록했다.

효과:

- `StaffWorkforceQueryService.cs`에서 직접 `FindObjectsByType` 사용이 사라졌다.
- `UITabManager.cs`에서 직접 `UIManager.Instance`/`UIManager.HasInstance` 사용이 사라졌다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 46 -> 45, `SingletonAccess`는 71 -> 70, `DependencyInjection`은 11 -> 13으로 이동했다.

검증:

```text
Unity MCP refresh after staff workforce DI split: Passed
Unity MCP refresh after UI popup service DI split: Passed
Targeted direct lookup scan: only DungeonRuntimeLifetimeScope and DungeonSceneComponentQuery retain scene lookup boundaries
Unity Console: 0 errors, 7 existing warnings
Generated: 2026-07-04 11:50:00 +09:00
```

### 23. UITab popup singleton 접근 제거

대상:

- `Assets/Scripts/UI/UITab.cs`
- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `UITabManager`의 popup 호출은 서비스로 이동했지만, 실제 탭 컴포넌트인 `UITab.Toggle()`/`CloseTab()`은 여전히 `UIManager.Instance`를 직접 호출했다.
- `UITabManager`가 런타임 생성하는 탭은 scene load 시점의 VContainer build callback만으로는 주입되지 않으므로 생성 직후 주입 경로가 필요했다.

변경:

- `UITab`에 `[Inject] Construct(IUiPopupService)`를 추가하고, 탭 열기/닫기는 `IUiPopupService`를 통해 수행하게 했다.
- `DungeonRuntimeLifetimeScope`가 로드된 씬의 기존 `UITab` 컴포넌트에도 `resolver.Inject(tab)`를 수행한다.
- `UITabManager.EnsureGeneratedTab()`에서 새로 생성한 `UITab`에 `ResolvePopupService()`를 전달해 런타임 생성 탭도 같은 popup service를 사용하게 했다.

효과:

- `UITab.cs`에서 직접 `UIManager.Instance` 사용이 사라졌다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 70 -> 69, `DependencyInjection`은 13 -> 14로 이동했다.

검증:

```text
Unity MCP refresh after UITab popup DI split: Passed
Targeted direct lookup scan: only DungeonRuntimeLifetimeScope and DungeonSceneComponentQuery retain scene lookup boundaries
Unity Console: 0 errors, 7 existing warnings
Generated: 2026-07-04 11:56:24 +09:00
```

### 24. Summary popup과 DataManager 조회 DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/DataCatalogService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`

진단:

- `BuildingSummaryInfo`는 popup 열기/닫기에서 `UIManager.Instance`, 건물 정의 조회에서 `DataManager.Instance`를 직접 호출했다.
- `CharacterSummeryInfo`는 캐릭터 popup 열기/닫기에서 `UIManager.Instance`를 직접 호출했다.
- 기존 scene load inject 대상에 두 summary popup이 없어, VContainer 서비스로 옮겨도 실제 씬 컴포넌트에 주입되지 않을 수 있었다.

변경:

- `IDataCatalog`/`DataManagerCatalog`를 추가해 `DataManager.Instance` 접근을 DI 경계로 모았다.
- `IBuildingDefinitionLookup`/`BuildingDefinitionLookup`을 추가해 `BuildingSO` id 조회와 누락 시 명시 예외를 분리했다.
- `BuildingSummaryInfo`는 `IUiPopupService`, `IBuildingDefinitionLookup`을 주입받아 popup 호출과 건물 정의 조회를 수행한다.
- `CharacterSummeryInfo`는 `IUiPopupService`를 주입받아 popup 호출을 수행한다.
- `DungeonRuntimeLifetimeScope`에 데이터/건물 lookup 서비스를 등록하고, `BuildingSummaryInfo`, `CharacterSummeryInfo` 씬 컴포넌트에도 inject를 수행한다.

효과:

- `BuildingSummaryInfo.cs`에서 직접 `UIManager.Instance`와 `DataManager.Instance` 사용이 사라졌다.
- `CharacterSummeryInfo.cs`에서 직접 `UIManager.Instance` 사용이 사라졌다.
- `DataManager.Instance` 접근은 `DataCatalogService.cs`의 명시 DI boundary로 이동했다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 69 -> 68, `DependencyInjection`은 14 -> 17로 이동했다.

검증:

```text
Unity MCP refresh after summary popup DI split: Passed
Targeted direct lookup scan: direct UI/Data singleton access removed from BuildingSummaryInfo and CharacterSummeryInfo
Unity Console: 0 errors, 7 existing warnings
Generated: 2026-07-04 12:05:00 +09:00
```

### 25. GridSystem UI singleton 접근 DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/GridSystemProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonBackdropReferenceProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/UI/CameraManager.cs`
- `Assets/Scripts/UI/DungeonSceneBackdropFitter.cs`
- `Assets/Scripts/UI/WorldInfoClickSelector.cs`

진단:

- `CameraManager`와 `DungeonSceneBackdropFitter`가 `GridSystemManager.Instance`를 직접 잡고 있어 카메라 경계 계산, 배경 확장, grid expand 구독이 전역 singleton에 묶여 있었다.
- `DungeonSceneBackdropFitter`는 `GameObject.Find("BackGround")`, `FindObjectsByType<Tilemap>()`까지 직접 수행해 배경 UI 컴포넌트 안에 씬 검색 책임이 섞여 있었다.
- `WorldInfoClickSelector`는 static 호출부를 유지해야 하는 상태였지만, 내부에서 grid mode를 직접 singleton으로 확인해 건물/캐릭터 클릭 우선순위 판단이 GridSystem 전역 상태에 결합되어 있었다.
- Unity 생명주기상 씬 오브젝트의 `Awake`/`OnEnable`이 VContainer 주입보다 먼저 돌 수 있으므로, 주입 의존 코드는 `Start` 또는 주입 이후 guard를 통해 실행되어야 했다.

변경:

- `IGridSystemProvider`/`GridSystemProvider`를 추가해 `GridSystemManager`와 `Grid` 접근을 명시 DI 경계로 이동했다.
- `IDungeonBackdropReferenceProvider`/`DungeonBackdropReferenceProvider`를 추가해 `BackGround` transform과 `Ground` tilemap 조회를 배경 전용 provider로 분리했다.
- `CameraManager`는 `[Inject] Construct(IGridSystemProvider)`를 통해 grid expand를 구독하고, grid가 없으면 fallback하지 않고 명시 예외를 던지게 했다.
- `DungeonSceneBackdropFitter`는 grid/backdrop provider를 주입받고, 기존 직접 씬 검색 함수 `ResolveReferences()`를 제거했다.
- `IWorldInfoClickSelector`/`WorldInfoClickSelectionService`를 추가하고, 기존 `WorldInfoClickSelector` static API는 compatibility facade로만 남겨 VContainer 서비스에 위임하게 했다. 이후 90번에서 해당 facade까지 제거했다.
- `DungeonRuntimeLifetimeScope`에 새 provider/service를 등록하고 `CameraManager`, `DungeonSceneBackdropFitter` 기존 씬 컴포넌트에도 `resolver.Inject(...)`를 수행하게 했다.

효과:

- `CameraManager.cs`, `DungeonSceneBackdropFitter.cs`, `WorldInfoClickSelector.cs`에서 직접 `GridSystemManager.Instance` 사용이 제거됐다.
- `DungeonSceneBackdropFitter.cs`에서 직접 `GameObject.Find`/`FindObjectsByType<Tilemap>` 사용이 제거됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 45 -> 44, `SingletonAccess`는 68 -> 65, `DependencyInjection`은 17 -> 22로 이동했다.
- 배경 피터의 runtime sprite 복제 책임은 그대로 남아 있으므로 `RuntimeObjectCreation`/`SceneMutation` flag는 유지했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after GridSystem provider refactor: Passed
Scene provider probe: GridSystemManager=[Grid], Grid=32x3, backgroundRoot=[BackGround], groundTilemap=[Ground]
Targeted direct lookup scan: no GridSystemManager.Instance/GameObject.Find/FindObjectsByType usage remains in CameraManager, DungeonSceneBackdropFitter, WorldInfoClickSelector, GridSystemProvider, DungeonBackdropReferenceProvider
Unity Console: 0 errors, 7 existing warnings
Generated: 2026-07-04 12:16:19 +09:00
```

### 26. Grid construction UI singleton 접근 DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/GridUIManager.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/DungeonStoryGridGhostPresenter.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`
- `Assets/Scripts/Buildings/UI/UIBuildingSelectButton.cs`

진단:

- `GridUIManager`가 `GameObject.FindGameObjectWithTag("GridCanvas")`, `GridSystemManager.Instance`, `DungeonStoryGridBuildingController.Instance`를 직접 사용했다.
- `DungeonStoryGridGhostPresenter`가 grid, 건설 컨트롤러, 마우스 월드 좌표를 `GridSystemManager.Instance`, `DungeonStoryGridBuildingController.Instance`, `GameManager.Instance`에서 직접 가져왔다.
- `GridConstructTab`이 건설 컨트롤러, popup, 건물 데이터 조회를 각각 `DungeonStoryGridBuildingController.Instance`, `UIManager.Instance`, `DataManager.Instance`에 직접 묶고 있었다.
- 동적으로 생성되는 `UIBuildingSelectButton`도 클릭 시 건설 컨트롤러 singleton을 직접 호출했다.

변경:

- `IDungeonGridBuildingControllerProvider`/`DungeonGridBuildingControllerProvider`를 추가해 건설 UI가 컨트롤러를 직접 찾지 않게 했다.
- `IWorldPointerPositionProvider`/`GameWorldPointerPositionProvider`를 추가해 ghost presenter의 마우스 월드 좌표 조회를 주입 경계로 이동했다.
- `GridUIManager`는 grid provider와 건설 컨트롤러 provider를 주입받고, grid canvas는 현재 씬의 serialized reference를 명시 요구한다.
- `GridConstructTab`은 `IDataCatalog`, `IUiPopupService`, `IDungeonGridBuildingControllerProvider`를 주입받고, 생성한 `UITab`/`UIBuildingSelectButton`에도 필요한 의존성을 즉시 전달한다.
- `DungeonRuntimeLifetimeScope`가 `GridUIManager`, `DungeonStoryGridGhostPresenter`, `UIBuildingSelectButton` 씬 컴포넌트에도 inject를 수행한다.

효과:

- `GridUIManager.cs`, `DungeonStoryGridGhostPresenter.cs`, `GridConstructTab.cs`, `UIBuildingSelectButton.cs`에서 직접 singleton 접근이 제거됐다.
- `GridUIManager.cs`에서 직접 태그 기반 씬 검색이 제거됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 44 -> 43, `SingletonAccess`는 65 -> 61, `Reflection`은 58 -> 57, `DependencyInjection`은 22 -> 27로 이동했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after grid construction UI DI refactor: Passed
Scene provider probe: controller=[Grid], mouseWorld=(-30.50, -3.94, 0.00), gridUiManagers=1, missingCanvas=0
Targeted direct lookup scan: no GridSystemManager.Instance/DungeonStoryGridBuildingController.Instance/GameManager.Instance/DataManager.Instance/UIManager.Instance/GameObject.Find/FindGameObjectWithTag remains in grid construction UI target files
Unity Console: 0 errors, existing warnings only
Generated: 2026-07-04 12:35:00 +09:00
```

### 27. Runtime UI panel singleton fallback DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/RuntimePanelProviders.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`
- `Assets/Scripts/Codex/UI/CodexPanel.cs`

진단:

- `FacilityEvolutionPanel`, `FacilitySynthesisPanel`, `CodexPanel`이 `runtime != null ? runtime : Runtime.Instance` 형태의 fallback을 갖고 있었다.
- 동적 UI 생성은 이미 `Bind(runtime)`로 명시적 런타임을 받으므로, 남은 scene runtime 조회는 패널 내부 singleton 접근이 아니라 DI 경계 뒤에 있어야 한다.
- fallback이 남아 있으면 테스트/씬 누락을 숨기고, 패널이 어떤 런타임을 쓰는지 디버깅하기 어려워진다.

변경:

- `IFacilityEvolutionRuntimeProvider`, `IFacilitySynthesisRuntimeProvider`, `ICodexRuntimeProvider`와 구현체를 추가했다.
- `DungeonRuntimeLifetimeScope`에 세 provider를 등록하고, scene-owned evolution/synthesis/codex 패널에 주입하도록 했다.
- 세 패널은 explicit `Bind(...)`를 우선 사용하고, 없을 때 주입된 provider를 통해 런타임을 받는다.
- 대상 패널의 직접 `Runtime.Instance` fallback은 제거했다.

효과:

- 세 런타임 UI 패널이 `SingletonAccess` 후보에서 `DependencyInjection` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 61 -> 58, `DependencyInjection`은 27 -> 31로 이동했다.
- 감사 요약은 275 script files, 5595 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after runtime panel provider refactor: Passed
Targeted direct singleton scan: no FacilityEvolutionRuntime.Instance / FacilitySynthesisRuntime.Instance / CodexRuntime.Instance remains in target panels
Runtime panel setup probe: current scene has FacilityEvolutionRuntime=0, FacilitySynthesisRuntime=0, CodexRuntime=0 and panel counts=0/0/0
Unity Console: 0 errors, existing warnings only
Generated: 2026-07-04 13:05:00 +09:00
```

### 28. Meta progression run-result builder/panel DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/InvasionThreatRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Meta/MetaProgressionSystem.cs`
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`
- `Assets/Scripts/Meta/RunResultPanel.cs`

진단:

- `MetaProgressionRuntime`이 런 이벤트 수집, 런 결과 snapshot 조립, 침입 위협 런타임 singleton 조회, 결과 패널 표시까지 함께 맡고 있었다.
- `BuildResult`가 `InvasionThreatRuntime.Instance`를 직접 조회해서 결과 계산과 씬 singleton 접근이 붙어 있었다.
- `RunResultPanel.Show`가 `FindFirstObjectByType<RunResultPanel>()`로 패널을 직접 찾고 없으면 생성해서, 결과 UI 표시 책임이 static global lookup으로 노출되어 있었다.

변경:

- `IInvasionThreatRuntimeProvider`/`InvasionThreatRuntimeProvider`를 추가해 침입 위협 런타임 조회를 DI 경계 뒤로 이동했다.
- `IMetaRunResultBuilder`/`MetaRunResultBuilder`를 추가해 `RunResultSnapshot` 조립을 `MetaProgressionRuntime`에서 분리했다.
- `IRunResultPanelService`/`RunResultPanelService`를 추가해 결과 패널 조회/생성/표시 책임을 service로 이동했다.
- `MetaProgressionRuntime`은 `IMetaRunResultBuilder`, `IRunResultPanelService`를 VContainer로 주입받고, 런 상태를 context로 넘기는 orchestration만 담당하게 했다.
- `RunResultPanel.Show` static global lookup을 제거했고, TMP obsolete `enableWordWrapping` 사용을 `textWrappingMode`로 교체했다.

효과:

- `MetaProgressionSystem.cs`에서 `InvasionThreatRuntime.Instance`와 `RunResultPanel.Show` 직접 결합이 제거됐다.
- `RunResultPanel.cs`에서 `GlobalObjectLookup` 플래그가 제거됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 43 -> 42, `DependencyInjection`은 31 -> 34, `RuntimeObjectCreation`은 73 -> 74, `SceneMutation`은 86 -> 87로 갱신했다.
- 감사 요약은 277 script files, 5611 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after meta progression DI refactor: Passed
Targeted direct coupling scan: no RunResultPanel.Show / InvasionThreatRuntime.Instance / FindFirstObjectByType<RunResultPanel> remains in Meta target files
Meta progression DI probe: current scene has MetaProgressionRuntime=0, InvasionThreatRuntime=0, RunResultPanel=0
Injected EndRun probe: passed with IMetaRunResultBuilder and IRunResultPanelService manually injected, panel disabled
Unity Console: 0 errors, existing warnings only
Generated: 2026-07-04 13:25:00 +09:00
```

### 29. Event alert runtime view presenter DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Operation/EventAlertSystem.cs`
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`

진단:

- `EventAlertRuntime`이 알림 기록/merge 처리뿐 아니라 버튼 생성, 버튼 업데이트, 선택지 버튼 파괴, 상세 패널 표시, 런타임 UI root 생성까지 함께 맡고 있었다.
- 이 때문에 이벤트 도메인 로직을 테스트하려면 Unity UI 생성과 Canvas 조회까지 같이 따라와야 했다.
- `EventAlertUiFactory`는 이미 UI hierarchy 생성 책임을 갖고 있었지만, 그 호출 순서와 생성된 버튼 생명주기는 여전히 runtime에 남아 있었다.

변경:

- `IEventAlertViewPresenter`, `IEventAlertViewPresenterFactory`, `EventAlertViewPresenter`를 추가했다.
- `EventAlertRuntime`은 `IEventAlertViewPresenterFactory`를 VContainer로 주입받고, UI 작업은 presenter에 위임한다.
- 버튼 dictionary, 선택지 버튼 list, `Destroy`/`DestroyImmediate`, `EventAlertUiFactory` 직접 호출을 `EventAlertRuntime`에서 제거했다.
- `DungeonRuntimeLifetimeScope`가 presenter factory를 등록하고 scene `EventAlertRuntime`에 inject를 수행한다.

효과:

- `EventAlertSystem.cs`는 211 lines / 17 declarations에서 130 lines / 13 declarations로 줄었다.
- `EventAlertSystem.cs`의 직접 `SceneMutation` 책임은 `EventAlertViewPresenter.cs`로 이동했고, runtime은 `DependencyInjection, EventBus` 중심이 됐다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 34 -> 36, `RuntimeObjectCreation`은 74 -> 75로 갱신했다.
- 감사 요약은 278 script files, 5625 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after event alert presenter DI refactor: Passed
Targeted runtime scan: no EventAlertUiFactory / Destroy / DestroyImmediate / CreateChoiceButton / CreateAlertButton remains in EventAlertSystem.cs
Injected EventAlertRuntime probe: passed with stub IEventAlertViewPresenterFactory; event logged, button creation delegated, choice callback executed
Unity Console: 0 errors, existing warnings only
Generated: 2026-07-04 13:45:00 +09:00
```

### 30. Invasion intruder runtime DI 컨텍스트 경계 분리

대상:

- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Invasion/InvasionIntruderContext.cs`
- `Assets/Scripts/Invasion/InvasionIntruderEntryResolver.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`

진단:

- `InvasionDirectorRuntime`이 침입 입구, grid, 런 변수 적용을 처리하면서 `GridSystemManager.Instance`, `RunVariableRuntime.Instance`, `FindFirstObjectByType<CharacterSpawner>`에 직접 결합되어 있었다.
- `InvasionIntruderRuntime`도 이동 루프 안에서 `GridSystemManager.Instance`, `OwnerRunManager.Instance`를 계속 조회해 침입자 상태 전이 테스트가 씬 singleton 상태에 묶였다.
- `InvasionIntruderEntryResolver`가 입구 해석 로직과 scene-wide spawner 검색을 같이 맡고 있어, 입구 계산 자체를 순수하게 검증하기 어려웠다.

변경:

- `IInvasionIntruderContext`/`InvasionIntruderContext`를 추가해 grid, owner, entry, run-variable 적용을 침입자 전용 DI 경계로 묶었다.
- `InvasionIntruderEntryResolver`는 `CharacterSpawner`와 `Grid`를 인자로 받아 `InvasionIntruderEntry`를 계산하는 역할만 남겼다.
- `InvasionDirectorRuntime`은 VContainer `[Inject]`로 `IInvasionIntruderContext`를 받고, 동적으로 생성한 `InvasionIntruderRuntime`에 같은 context를 명시 초기화한다.
- `InvasionIntruderRuntime`은 이동/시설 피해/최종 교전 루프에서 grid와 owner를 context로만 조회한다.
- `DungeonRuntimeLifetimeScope`에 `InvasionIntruderContext`를 등록하고, scene `InvasionDirectorRuntime`에 inject를 수행하도록 했다.

효과:

- `InvasionIntruderSystem.cs`와 `InvasionIntruderEntryResolver.cs`에서 직접 `GridSystemManager.Instance`, `RunVariableRuntime.Instance`, `OwnerRunManager.Instance`, `FindFirstObjectByType<CharacterSpawner>` 사용이 제거됐다.
- `InvasionIntruderSystem.cs`는 `GlobalObjectLookup, SingletonAccess` 후보에서 `DependencyInjection, EventBus, RuntimeObjectCreation, SceneMutation` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 42 -> 40, `SingletonAccess`는 58 -> 57, `DependencyInjection`은 36 -> 38로 갱신했다.
- 감사 요약은 279 script files, 5638 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after invasion intruder context DI refactor: Passed
Targeted direct coupling scan: no GridSystemManager.Instance / RunVariableRuntime.Instance / OwnerRunManager.Instance / FindFirstObjectByType<CharacterSpawner> remains in invasion intruder target files
DI wiring scan: InvasionIntruderContext registered, InvasionDirectorRuntime scene injection enabled, spawned InvasionIntruderRuntime receives Initialize(context)
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 13:35:28 +09:00
```

### 31. Offense runtime singleton/context DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs`
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Offense/OffenseRewardContextResolver.cs`
- `Assets/Scripts/Offense/OffenseRewardSystem.cs`

진단:

- `OffenseWorldMapRuntime`, `OffenseExpeditionRuntime`, `OffenseRewardRuntime`이 각각 static `Instance`와 `FindFirstObjectByType` 기반 조회를 들고 있었다.
- 원정 런타임은 원정 가능 캐릭터를 직접 `FindObjectsByType<CharacterActor>`로 검색하고, 월드맵/메타 진행/보상 런타임을 singleton으로 직접 호출했다.
- 원정/월드맵 패널은 static `Show()` 안에서 패널을 직접 찾거나 생성했고, `Bind(null)`이 runtime singleton fallback으로 이어졌다.
- 보상 context resolver가 `BlueprintResearchRuntime.Instance`, `GameManager.TryGetInstance()`, scene-wide 상점/창고 검색을 직접 처리했다.

변경:

- `IMetaProgressionRuntimeProvider`/`MetaProgressionRuntimeProvider`를 추가해 메타 진행 런타임 조회를 DI 경계로 이동했다.
- `OffenseRuntimeServices`를 추가해 월드맵/보상 런타임 provider, 원정 멤버 query, 오펜스 패널 표시 service를 분리했다.
- `IOffenseRewardContextBuilder`/`OffenseRewardContextBuilder`로 보상 context 조립을 이동하고, scene query를 통해 연구/상점/게임 데이터/창고를 조회하게 했다.
- `OffenseWorldMapRuntime`, `OffenseExpeditionRuntime`, `OffenseRewardRuntime`은 VContainer `[Inject]`로 필요한 provider/service/builder를 받는다.
- `OffenseWorldMapPanel`과 `OffenseExpeditionPanel`은 explicit runtime만 받도록 바꾸고, static `Show()` fallback을 제거했다.
- `DungeonRuntimeLifetimeScope`가 새 provider/service/builder를 등록하고 scene 오펜스 런타임들에 inject를 수행한다.

효과:

- 오펜스 대상 파일에서 `OffenseWorldMapRuntime.Instance`, `OffenseExpeditionRuntime.Instance`, `OffenseRewardRuntime.Instance`, `MetaProgressionRuntime.Instance`, `FindFirstObjectByType<Offense...>`, `FindObjectsByType<CharacterActor>` 직접 사용이 제거됐다.
- `OffenseWorldMapSystem.cs`, `OffenseExpeditionSystem.cs`, `OffenseRewardContextResolver.cs`, `OffenseRewardSystem.cs`가 `GlobalObjectLookup`/`SingletonAccess` 후보에서 빠지고 DI 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 40 -> 36, `SingletonAccess`는 57 -> 53, `DependencyInjection`은 38 -> 44로 갱신했다.
- 감사 요약은 281 script files, 5705 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after offense DI refactor: Passed
Targeted direct coupling scan: no Offense*.Instance / MetaProgressionRuntime.Instance / FindFirstObjectByType<Offense...> / FindObjectsByType<CharacterActor> remains in offense target files
DI wiring scan: offense providers/services registered, scene OffenseWorldMapRuntime/OffenseExpeditionRuntime/OffenseRewardRuntime injection enabled
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 13:46:18 +09:00
```

### 32. DungeonStoryGridBuildingController DI 경계 분리

대상:

- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`
- `Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `DungeonStoryGridBuildingController`가 static `Instance` 접근자와 scene fallback을 들고 있어, 컨트롤러 생성/조회 책임이 runtime 조립 경계 밖에 남아 있었다.
- grid, data catalog, pointer position, grid texture 접근이 각각 `GridSystemManager.Instance`, `DataManager.Instance`, `GameManager.Instance`, `GridTexture.Instance`에 직접 묶여 있었다.
- 그 결과 grid 건설 UI는 DI로 넘어갔지만 실제 건설 컨트롤러는 여전히 singleton/scene 조회와 섞여 테스트 경계가 흐렸다.

변경:

- `DungeonStoryGridBuildingController.Instance`와 `FindFirstObjectByType<DungeonStoryGridBuildingController>` 기반 fallback을 제거했다.
- 컨트롤러가 `IGridSystemProvider`, `IDataCatalog`, `IWorldPointerPositionProvider`, `IGridTextureProvider`를 VContainer `[Inject]`로 받도록 변경했다.
- `IGridTextureProvider`/`GridTextureProvider`를 추가해 `GridTexture` scene lookup을 runtime provider 경계로 이동했다.
- `DungeonRuntimeLifetimeScope`에 grid texture provider를 등록하고 scene-owned `DungeonStoryGridBuildingController`에 inject를 수행하도록 했다.

효과:

- 대상 파일에서 `DungeonStoryGridBuildingController.Instance`, `GridSystemManager.Instance`, `DataManager.Instance`, `GameManager.Instance`, `GridTexture.Instance`, `FindObjectsByType`, `GameObject.Find` 직접 사용이 제거됐다.
- `DungeonStoryGridBuildingController.cs`가 `GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation` 후보에서 `DependencyInjection, RuntimeObjectCreation, SceneMutation` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 35, `SingletonAccess`는 52, `Reflection`은 56, `DependencyInjection`은 45로 갱신했다.
- 감사 요약은 281 script files, 5705 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after grid building controller DI refactor: Passed
Targeted direct coupling scan: no DungeonStoryGridBuildingController.Instance / FindFirstObjectByType<DungeonStoryGridBuildingController> / GridSystemManager.Instance / DataManager.Instance / GameManager.Instance / GridTexture.Instance / FindObjectsByType / GameObject.Find remains in the target files
DI wiring scan: GridTextureProvider registered, DungeonStoryGridBuildingController scene injection enabled, controller has VContainer Construct method
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 13:55:39 +09:00
```

### 33. MetaProgressionRuntime reader DI 경계 분리

대상:

- `Assets/Scripts/Meta/MetaProgressionSystem.cs`
- `Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/Run/RunStartVariableSelector.cs`
- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/Character/Core/CharacterStats.cs`

진단:

- `MetaProgressionRuntime.Instance`가 `FindFirstObjectByType<MetaProgressionRuntime>` fallback을 들고 있어 메타 보너스 조회가 scene lookup에 묶여 있었다.
- 실제 호출부는 runtime 자체가 아니라 시작 후보 보너스, 오너 체력 배율, 침입 경고 임계값, 보존 레시피, 기본 구매 확장 ID 같은 read-only 값만 필요했다.
- 정적 유틸리티/런타임 시스템이 각자 `MetaProgressionRuntime.Instance`를 확인하면서 메타 진행의 읽기 책임과 전역 조회 책임이 섞여 있었다.

변경:

- `IMetaProgressionRuntimeReader`/`MetaProgressionRuntimeReader`를 추가해 메타 보너스 읽기 전용 경계를 만들었다.
- `MetaProgressionRuntimeAccess`는 `DungeonRuntimeLifetimeScope.TryResolve<IMetaProgressionRuntimeReader>`만 사용하고 scene lookup fallback을 두지 않는다.
- `DungeonRuntimeLifetimeScope`가 reader를 등록한다.
- `MetaProgressionRuntime`에서 static `Instance`, 내부 static instance field, `OnDestroy` singleton 정리를 제거했다.
- 상점 기본 구매, 런 시작 후보, 침입 임계값, 레시피 표시 조건, 오너 체력 계산이 `MetaProgressionRuntimeAccess`를 통해 reader를 사용하도록 변경했다.

효과:

- 대상 파일에서 `MetaProgressionRuntime.Instance`, `FindFirstObjectByType<MetaProgressionRuntime>`, `private static MetaProgressionRuntime instance` 직접 사용이 제거됐다.
- `MetaProgressionSystem.cs`가 `GlobalObjectLookup, SingletonAccess` 후보에서 빠지고 `DependencyInjection, EventBus` 후보로 정리됐다.
- `RunStartVariableSelector.cs`와 `FacilityEvolutionService.cs`도 `SingletonAccess` 후보에서 빠졌다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 35 -> 34, `SingletonAccess`는 52 -> 49로 이동했다.
- 감사 요약은 281 script files, 5705 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after meta progression reader refactor: Passed
Targeted direct coupling scan: no MetaProgressionRuntime.Instance / FindFirstObjectByType<MetaProgressionRuntime> / private static MetaProgressionRuntime instance remains in the target files
DI wiring scan: MetaProgressionRuntimeReader registered, MetaProgressionRuntimeAccess resolves only through DungeonRuntimeLifetimeScope
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 14:07:41 +09:00
```

### 34. RunVariableRuntime reader DI 경계 분리

대상:

- `Assets/Scripts/Run/RunVariableSystem.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/Buildings/SO/StockInfo.cs`
- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/Invasion/InvasionIntruderContext.cs`

진단:

- `RunVariableRuntime.Instance`가 `FindFirstObjectByType<RunVariableRuntime>` fallback을 들고 있어 상점, 재고, 스포너, 침입 위협 계산이 런 변수 scene lookup에 묶여 있었다.
- `RunVariableRuntime` 내부에서도 `OwnerRunManager.Instance`와 `InvasionThreatRuntime.Instance`를 직접 읽어 시작 변수 생성 시 소유자/난이도 조회 책임이 섞여 있었다.
- `InvasionIntruderContext`는 DI 경계였지만 런 변수 적용만 별도로 `RunVariableRuntime`을 scene query로 찾아 읽고 있었다.

변경:

- `IRunVariableRuntimeProvider`/`IRunVariableRuntimeReader`/`RunVariableRuntimeAccess`를 추가해 런 변수 효과 읽기 전용 경계를 만들었다.
- `IOwnerRunDataProvider`/`OwnerRunDataProvider`를 추가해 선택된 사장 데이터 조회를 DI 경계로 옮겼다.
- `DungeonRuntimeLifetimeScope`에 run-variable provider/reader/owner provider를 등록하고 scene `RunVariableRuntime`에 inject를 수행하도록 했다.
- `RunVariableRuntime`에서 static `Instance`, `Current`, 내부 static instance field, `OnDestroy` singleton 정리를 제거했다.
- `RunVariableRuntime`이 `IOwnerRunDataProvider`, `IInvasionThreatRuntimeProvider`를 주입받아 시작 변수와 난이도를 resolve하도록 했다.
- 상점/재고/스포너/침입 위협 계산은 `RunVariableRuntimeAccess`를 통해 reader를 사용하도록 변경했다.
- `InvasionIntruderContext`의 런 변수 적용도 `IRunVariableRuntimeReader`로 통일했다.

효과:

- 대상 파일에서 `RunVariableRuntime.Instance`, `RunVariableRuntime.Current`, `FindFirstObjectByType<RunVariableRuntime>`, `private static RunVariableRuntime instance`, `OwnerRunManager.Instance`, `InvasionThreatRuntime.Instance` 직접 사용이 제거됐다.
- `RunVariableSystem.cs`가 `GlobalObjectLookup, SingletonAccess, EventBus` 후보에서 `DependencyInjection, EventBus` 후보로 이동했다.
- `FacilityShopSystem.cs`와 `StockInfo.cs`가 `SingletonAccess` 후보에서 빠졌다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 34 -> 33, `SingletonAccess`는 49 -> 46, `DependencyInjection`은 45 -> 47로 이동했다.
- 감사 요약은 281 script files, 5705 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after run variable reader DI refactor: Passed
Targeted direct coupling scan: no RunVariableRuntime.Instance / RunVariableRuntime.Current / FindFirstObjectByType<RunVariableRuntime> / private static RunVariableRuntime instance / OwnerRunManager.Instance / InvasionThreatRuntime.Instance remains in the target files
DI wiring scan: RunVariableRuntimeProvider, RunVariableRuntimeReader, OwnerRunDataProvider registered; scene RunVariableRuntime injection enabled
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 14:19:28 +09:00
```

### 35. InvasionThreatRuntime world sampler/reader DI 분리

대상:

- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/Invasion/InvasionThreatWorldSampler.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `InvasionThreatRuntime.Instance`가 `FindFirstObjectByType<InvasionThreatRuntime>`를 직접 노출하고 있어 런 변수/침입 위협 사이에 scene lookup 결합이 남아 있었다.
- `InvasionThreatCalculator.SampleWorldFactors()`가 `Object.FindObjectsByType<BuildableObject/CharacterActor>`로 월드 상태를 직접 수집해 순수 계산기와 씬 조회 책임이 섞여 있었다.
- 침입 위협 상승/경고 임계치 계산이 `RunVariableRuntimeAccess`, `MetaProgressionRuntimeAccess` static 경계를 직접 호출해 런타임 상태 전이와 데이터 읽기 경계가 붙어 있었다.

변경:

- `IInvasionThreatWorldSampler`/`InvasionThreatWorldSampler`를 추가해 건물/캐릭터 기반 위협 factor 수집을 별도 서비스로 분리했다.
- `InvasionThreatRuntime`에서 static `Instance`를 제거하고, VContainer method injection으로 `IInvasionThreatWorldSampler`, `IRunVariableRuntimeReader`, `IMetaProgressionRuntimeReader`를 받게 했다.
- `InvasionThreatCalculator`는 threat 상승량 순수 계산만 담당하게 하고, run multiplier는 런타임에서 reader로 읽어 명시적으로 전달한다.
- 기존 Editor debug scenario 호출이 깨지지 않도록 `CalculateRisePerSecond(settings, factors)` 호환 overload는 유지하되 기본 multiplier 1로만 계산한다.
- `DungeonRuntimeLifetimeScope`에 `InvasionThreatWorldSampler`를 등록하고 scene `InvasionThreatRuntime`에도 inject를 수행하도록 했다.

효과:

- `InvasionThreatSystem.cs`에서 `InvasionThreatRuntime.Instance`, `FindFirstObjectByType<InvasionThreatRuntime>`, `Object.FindObjectsByType`, `RunVariableRuntimeAccess`, `MetaProgressionRuntimeAccess` 직접 사용이 제거됐다.
- `InvasionThreatSystem.cs`는 `GlobalObjectLookup, SingletonAccess, EventBus` 후보에서 `DependencyInjection, EventBus` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 33 -> 32, `SingletonAccess`는 46 -> 45, `DependencyInjection`은 47 -> 49로 이동했다.
- 감사 요약은 282 script files, 5714 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after invasion threat DI refactor: Passed
Unity MCP AssetDatabase.Refresh after invasion threat calculator overload: Passed
Targeted direct coupling scan: no InvasionThreatRuntime.Instance / FindFirstObjectByType<InvasionThreatRuntime> / Object.FindObjectsByType / RunVariableRuntimeAccess / MetaProgressionRuntimeAccess remains in InvasionThreatSystem or the new sampler boundary
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 14:33:13 +09:00
```

### 36. CharacterSpawner runtime dependency DI 분리

대상:

- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/Recruitment/RegularCustomerSystem.cs`
- `Assets/Scripts/Infrastructure/GridSystemProvider.cs`
- `Assets/Scripts/Infrastructure/RegularCustomerRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `CharacterSpawner`가 `RegularCustomerRuntime.Instance`, `GridSystemManager.Instance`, `RunVariableRuntimeAccess`를 직접 읽어 고객 생성, 입구 좌표, 런 변수 배율 계산이 scene/global 상태에 결합되어 있었다.
- `RegularCustomerRuntime.Instance`는 `FindFirstObjectByType<RegularCustomerRuntime>`를 직접 노출해 정규고객 상태 조회가 테스트/씬 구성에 강하게 묶였다.
- `IGridSystemProvider`는 필수 접근자만 있어, 기존 스포너의 "grid가 아직 없으면 spawn 보류" 동작을 DI로 옮길 때 선택적 조회 경계가 필요했다.

변경:

- `IRegularCustomerRuntimeProvider`/`RegularCustomerRuntimeProvider`를 추가해 정규고객 런타임 조회를 scene query provider 경계로 이동했다.
- `RegularCustomerRuntime.Instance` static accessor를 제거했다.
- `IGridSystemProvider`에 `TryGetManager`, `TryGetGrid`를 추가해 필수 접근(`Manager`, `Grid`)과 선택 접근을 분리했다.
- `CharacterSpawner`가 `IRegularCustomerRuntimeProvider`, `IGridSystemProvider`, `IRunVariableRuntimeReader`를 VContainer method injection으로 받도록 변경했다.
- `DungeonRuntimeLifetimeScope`에 정규고객 provider를 등록하고 scene `CharacterSpawner`에도 inject를 수행하도록 했다.

효과:

- `CharacterSpawner.cs`와 `RegularCustomerSystem.cs`에서 `RegularCustomerRuntime.Instance`, `FindFirstObjectByType<RegularCustomerRuntime>`, `GridSystemManager.Instance`, `RunVariableRuntimeAccess` 직접 사용이 제거됐다.
- `CharacterSpawner.cs`는 `SingletonAccess, RuntimeObjectCreation, SceneMutation` 후보에서 `DependencyInjection, RuntimeObjectCreation, SceneMutation` 후보로 이동했다.
- `RegularCustomerSystem.cs`는 `GlobalObjectLookup, EventBus` 후보에서 `EventBus` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 32 -> 31, `SingletonAccess`는 45 -> 44, `DependencyInjection`은 49 -> 51로 이동했다.
- 감사 요약은 283 script files, 5727 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after character spawner DI refactor: Passed
Targeted direct coupling scan: no RegularCustomerRuntime.Instance / FindFirstObjectByType<RegularCustomerRuntime> / GridSystemManager.Instance / RunVariableRuntimeAccess remains in CharacterSpawner, RegularCustomerSystem, or the provider boundary
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 14:44:38 +09:00
```

### 37. Character prefab runtime DI 주입 경계 분리

대상:

- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/Character/Ability/AbilityMove.cs`
- `Assets/Scripts/Character/Ability/CharacterAbility.cs`
- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/Core/CharacterLifecycle.cs`
- `Assets/Scripts/Infrastructure/CharacterSpawnerProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `CharacterSpawner`가 DI를 받더라도 풀에서 생성되는 캐릭터 prefab 인스턴스에는 VContainer 주입이 자동으로 들어가지 않았다.
- `AbilityMove`가 `GameObject.FindGameObjectWithTag("CharacterSpawner")`로 스포너를 찾고 있어 태그/씬 구성에 직접 결합되어 있었다.
- `CharacterAbility`, `CharacterActor`, `CharacterLifecycle`가 `GridSystemManager.Instance`를 직접 읽어 캐릭터 이동/좌표/방문 가능 시설 계산이 전역 grid singleton에 묶여 있었다.

변경:

- `ICharacterSpawnerProvider`/`CharacterSpawnerProvider`를 추가해 스포너 조회를 scene query provider 경계로 이동했다.
- `CharacterSpawner`가 `IObjectResolver`를 주입받고, 풀에서 꺼낸 캐릭터 GameObject의 MonoBehaviour 컴포넌트들에 즉시 `resolver.Inject(...)`를 수행하도록 했다.
- `AbilityMove`는 `ICharacterSpawnerProvider`를 주입받아 exit dungeon 시 스포너를 resolve한다.
- `CharacterAbility`, `CharacterActor`, `CharacterLifecycle`는 `IGridSystemProvider`를 주입받아 grid를 optional accessor로 조회한다.
- `DungeonRuntimeLifetimeScope`가 scene-owned `CharacterActor`, `CharacterAbility`, `CharacterLifecycle`에도 inject를 수행하고, 동적 생성 캐릭터는 `CharacterSpawner`가 주입한다.

효과:

- 대상 캐릭터 런타임 파일에서 `GridSystemManager.Instance`, `GameObject.FindGameObjectWithTag`, `RegularCustomerRuntime.Instance`, `RunVariableRuntimeAccess` 직접 사용이 제거됐다.
- `AbilityMove.cs`는 `GlobalObjectLookup, SceneMutation` 후보에서 `DependencyInjection, SceneMutation` 후보로 이동했다.
- `CharacterAbility.cs`, `CharacterActor.cs`, `CharacterLifecycle.cs`는 singleton/grid 직접 접근 후보에서 DI 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 31 -> 30, `SingletonAccess`는 44 -> 41, `DependencyInjection`은 51 -> 56으로 이동했다.
- 감사 요약은 284 script files, 5739 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after character runtime DI injection refactor: Passed
Targeted direct coupling scan: no GridSystemManager.Instance / GameObject.FindGameObjectWithTag / FindGameObjectWithTag remains in CharacterActor, CharacterLifecycle, CharacterAbility, AbilityMove, CharacterSpawner, or CharacterSpawnerProvider
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 14:53:54 +09:00
```

### 38. AIBrain grid provider DI 분리

대상:

- `Assets/Scripts/Character/AI/AIBrain.cs`

진단:

- `AIBrain.DecideAction()`, `GetPathSearch()`, `AIAction.SetDestinationWithFailure()`, `AIAction.IsCharacterAtDestination()`가 `GridSystemManager.Instance.grid`를 직접 읽고 있었다.
- 캐릭터 prefab 주입 경계가 생겼음에도 AI 의사결정 중심부가 여전히 grid singleton에 묶여 있어 동적 생성 캐릭터와 테스트 격리가 불완전했다.

변경:

- `AIBrain`이 `CharacterAbility`의 injected grid provider 경계를 사용하도록 `TryGetRuntimeGrid()`를 추가했다.
- path search와 destination planning은 `TryGetRuntimeGrid()` 또는 `actor.Brain.TryGetRuntimeGrid()`를 통해 grid를 얻는다.
- grid가 없을 때 기존처럼 decision/path/destination 실패로 빠지는 동작은 유지했다.

효과:

- `AIBrain.cs`에서 `GridSystemManager.Instance` 직접 사용이 제거됐다.
- `AIBrain.cs`는 당시 `SingletonAccess, ResourcesAccess` 후보에서 `DependencyInjection, ResourcesAccess` 후보로 이동했고, 이후 64번에서 `ResourcesAccess`도 provider 경계로 분리됐다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 41 -> 40, `DependencyInjection`은 56 -> 57로 이동했다.
- 감사 요약은 284 script files, 5740 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after AIBrain grid provider refactor: Passed
Targeted direct coupling scan: no GridSystemManager.Instance remains in AIBrain.cs
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 15:01:21 +09:00
```

### 39. WorkGridUtility DI resolver 경계 분리

대상:

- `Assets/Scripts/Character/Work/WorkGridUtility.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `WorkGridUtility`가 작업 grid 선택 시 `GridSystemManager.Instance`를 직접 읽고 있어, 작업 실행 계층이 전역 grid singleton에 묶여 있었다.
- 작업 계층의 호출부는 정적 `WorkGridUtility` API를 사용하고 있어 즉시 전체 호출부를 바꾸면 변경 범위가 커졌다.

변경:

- `IWorkGridResolver`와 `WorkGridResolver`를 추가해 grid 선택 책임을 주입 가능한 서비스로 분리했다.
- `WorkGridResolver`는 `IGridSystemProvider.TryGetGrid()`를 통해 grid를 조회하고, search result source grid와 priority grid 우선순위는 기존대로 유지한다.
- `WorkGridUtility` 정적 API는 호환 facade로 남기되, `DungeonRuntimeLifetimeScope.TryResolve<IWorkGridResolver>()`로 실제 resolver를 가져온다.
- resolver를 얻지 못하면 명시적으로 예외를 던져 조용한 fallback 없이 composition 누락을 바로 드러낸다.
- `DungeonRuntimeLifetimeScope`에 `WorkGridResolver`를 singleton DI 서비스로 등록했다.

효과:

- `WorkGridUtility.cs`에서 `GridSystemManager.Instance` 직접 사용이 제거됐다.
- `WorkGridUtility.cs`는 `SingletonAccess, SceneMutation` 후보에서 `DependencyInjection, SceneMutation` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 40 -> 39, `DependencyInjection`은 57 -> 58로 이동했다.
- 감사 요약은 284 script files, 5747 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after WorkGridUtility DI resolver refactor: Passed
Targeted direct coupling scan: no GridSystemManager.Instance remains in WorkGridUtility.cs
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 15:06:11 +09:00
```

### 40. Blueprint research work DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/Work/WorkTargetSelector.cs`
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`
- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`

진단:

- 작업 실행 계층의 `WorkTaskExecutor.ExecuteResearchWork()`가 `BlueprintResearchRuntime.Instance`를 직접 읽고 있었다.
- 작업 후보 평가의 `WorkTargetSelector`와 건물 긴급도 계산의 `BuildableObject`가 `BlueprintResearchRuntime.HasResearchWorkFor()` 정적 helper를 통해 연구 런타임 singleton에 묶여 있었다.
- `WorkTaskExecutor`는 `AbilityWork` 내부에서 직접 생성되는 순수 C# 모듈이라, executor 자체 주입보다 owner인 `AbilityWork`가 서비스를 주입받아 모듈에 제공하는 구조가 더 안전했다.

변경:

- `IBlueprintResearchRuntimeProvider`와 `IBlueprintResearchWorkService`를 추가해 연구 런타임 조회와 연구 작업 가능/적용 판단을 VContainer 서비스로 분리했다.
- `BlueprintResearchWorkAccess` static facade를 추가해 동적으로 생성되는 `BuildableObject`도 별도 컴포넌트 주입 누락 없이 같은 DI 서비스를 사용하게 했다.
- `AbilityWork`가 `IBlueprintResearchWorkService`를 VContainer method injection으로 받도록 했다.
- `WorkTargetSelector`와 `WorkTaskExecutor`는 `AbilityWork.BlueprintResearchWorkService`를 통해 연구 작업 후보/실행을 처리한다.
- `BlueprintResearchRuntime.HasResearchWorkFor()`는 호환 API로 남기되 내부 구현은 `BlueprintResearchWorkAccess`로 이동했다.
- `DungeonRuntimeLifetimeScope`에 research runtime provider와 work service를 singleton으로 등록했다.

효과:

- 작업 실행/선택 경로에서 `BlueprintResearchRuntime.Instance`와 `BlueprintResearchRuntime.HasResearchWorkFor()` 직접 사용이 제거됐다.
- `WorkTaskExecutor.cs`는 `SingletonAccess, SceneMutation` 후보에서 `DependencyInjection, SceneMutation` 후보로 이동했고, research service 누락 시 조용히 종료하는 fallback 없이 `AbilityWork`의 injected service 경계를 통과한다.
- `BuildableObject.cs`는 연구 작업 긴급도 계산에서 DI-backed research work facade를 사용한다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 39 -> 38, `DependencyInjection`은 58 -> 62로 이동했다.
- 감사 요약은 285 script files, 5761 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after BlueprintResearch work DI refactor: Passed
Targeted direct coupling scan: no BlueprintResearchRuntime.Instance or BlueprintResearchRuntime.HasResearchWorkFor remains in WorkTaskExecutor, WorkTargetSelector, BuildableObject, AbilityWork, or BlueprintResearchRuntimeProvider
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 15:22:09 +09:00
```

### 41. Shop stock catalog DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/ShopStockCatalogService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Buildings/Shop.cs`
- `Assets/Scripts/Character/Ability/AbilityShopping.cs`

진단:

- `Shop.Initialization()`이 `DataManager.Instance.GetData<StockInfo>()`를 직접 읽어 상점 재고 데이터를 찾고 있었다.
- `Shop.GetStockCategory()`와 `AbilityShopping.BuyItem()`이 `DataManager.Instance.GetData<SaleItem>()`를 직접 읽어 판매 아이템 분류/아이콘을 찾고 있었다.
- `Shop`은 동적 생성되는 `BuildableObject`라 컴포넌트 주입만 의존하면 합성/진화/런타임 생성 경로에서 주입 누락 가능성이 있다.

변경:

- `IShopStockCatalog`/`ShopStockCatalog`를 추가해 `StockInfo`와 `SaleItem` 조회를 `IDataCatalog` 기반 VContainer 서비스로 분리했다.
- `ShopStockCatalogAccess`를 추가해 동적 생성 `Shop`도 fallback 없이 VContainer catalog를 resolve하도록 했다.
- `AbilityShopping`은 `IShopStockCatalog`를 method injection으로 받아 구매 피드백 아이콘 조회에 사용한다.
- `DungeonRuntimeLifetimeScope`에 `ShopStockCatalog`를 singleton 서비스로 등록했다.

효과:

- `Shop.cs`와 `AbilityShopping.cs`에서 `DataManager.Instance` 직접 사용이 제거됐다.
- `Shop.cs`와 `AbilityShopping.cs`는 `SingletonAccess, SceneMutation` 후보에서 `DependencyInjection, SceneMutation` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 38 -> 36, `DependencyInjection`은 62 -> 65로 이동했다.
- 감사 요약은 286 script files, 5772 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after shop stock catalog DI refactor: Passed
Targeted direct coupling scan: no DataManager.Instance / GameObject.Find / FindFirstObjectByType / GridSystemManager.Instance remains in Shop.cs, AbilityShopping.cs, or ShopStockCatalogService.cs
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 15:34:27 +09:00
```


### 42. Staff discontent/owner run runtime DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/StaffDiscontentRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/Core/CharacterStats.cs`
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`
- `Assets/Scripts/Character/Work/WorkCommandHandler.cs`
- `Assets/Scripts/Character/Work/WorkDutyController.cs`
- `Assets/Scripts/Character/Work/WorkPriorityProfile.cs`
- `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`

진단:

- `CharacterStats.GetWorkSpeedMultiplier()`가 `StaffDiscontentRuntime.Instance`를 직접 읽어 직원 불만 시스템에 묶여 있었다.
- `CharacterStats.Die()`가 owner 사망 판단을 `OwnerRunManager.Instance`에 직접 위임하고 있었다.
- 작업 명령/근무/우선순위 경로가 `StaffDiscontentRuntime.Instance`를 직접 호출해 반란/업무 차단/진압 처리 테스트 경계가 좁았다.
- `OwnerRunManager.ResolveOwnerSpawnPosition()`이 `GridSystemManager.Instance`를 직접 읽고, 동적 생성 owner 컴포넌트가 VContainer 주입을 명시적으로 받는 지점도 없었다.
- `OwnerSelectionPanel`이 `OwnerRunManager.Instance`를 직접 읽어 UI 선택 패널도 owner singleton에 묶여 있었다.

변경:

- `IStaffDiscontentRuntimeProvider`와 `IStaffDiscontentRuntimeService`를 추가해 직원 불만 런타임 조회와 업무 효율/차단/반란/진압 처리를 DI 서비스로 분리했다.
- `StaffDiscontentRuntimeAccess`를 추가해 순수 C# 작업 모듈도 fallback 없이 VContainer 서비스만 사용하게 했다.
- `IOwnerRunManagerProvider`와 `IOwnerRunLifecycleService`를 추가해 owner 사망 처리를 DI 경계로 이동했다.
- `CharacterStats`는 VContainer method injection으로 직원 불만 서비스와 owner lifecycle 서비스를 받는다.
- `OwnerRunManager`는 object resolver와 grid provider를 주입받고, owner 생성 직후 모든 child MonoBehaviour에 resolver injection을 수행한다.
- `DungeonRuntimeLifetimeScope`는 StaffDiscontent/OwnerRun 서비스를 등록하고, 씬의 `CharacterStats`, `OwnerRunManager`, `OwnerSelectionPanel`도 명시적으로 주입한다.
- `OwnerSelectionPanel`은 `IOwnerRunManagerProvider`를 VContainer method injection으로 받아 owner 후보 UI를 구성한다.

효과:

- 타깃 파일에서 `StaffDiscontentRuntime.Instance`, `OwnerRunManager.Instance`, `GridSystemManager.Instance` 직접 사용을 제거했다.
- `CharacterStats`, `WorkCommandHandler`, `WorkDutyController`, `WorkPriorityProfile`, `OwnerSelectionPanel`은 `SingletonAccess` 후보에서 빠지고 DI-backed 경계로 이동했다.
- `OwnerRunManager`는 기존 singleton 타입으로 남아 있지만 grid lookup과 owner component injection 책임은 VContainer 서비스 경계로 분리됐다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 36 -> 31, `DependencyInjection`은 65 -> 72로 이동했다.
- 감사 요약은 287 script files, 5803 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after StaffDiscontent/OwnerRun DI refactor: Passed
Unity MCP AssetDatabase.Refresh after OwnerSelectionPanel DI refactor: Passed
Runtime direct coupling scan: no StaffDiscontentRuntime.Instance / OwnerRunManager.Instance / GridSystemManager.Instance remains in non-Editor runtime scripts
Unity Console: 0 errors, 6 existing warnings
Generated: 2026-07-04 15:55:53 +09:00
```


### 43. UI building/feedback direct singleton DI 경계 분리

대상:

- `Assets/Scripts/Buildings/UI/UIBuildingInfo.cs`
- `Assets/Scripts/Character/UI/CharacterFloatingIcon.cs`
- `Assets/Scripts/UI/UiPopupService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `UIBuildingInfo.DisplayBuildingInfo()`가 맨 앞 `return` 때문에 실제 표시 코드가 전부 unreachable 상태였고, 그 뒤에서 `DataManager.Instance`를 직접 읽고 있었다.
- `UIBuildingInfo.OpenDispaly()/CloseDispaly()`가 `UIManager.Instance`를 직접 호출해 touch guard 제어를 UI singleton에 묶고 있었다.
- `CharacterFloatingIcon.Show()`가 `GameManager.Instance`에서 DamageNumber prefab을 직접 찾고, 누락 시 조용히 `false`를 반환해 설정 오류를 숨겼다.

변경:

- `IUiTouchGuardService`/`UiTouchGuardService`를 추가해 UI touch guard 제어를 VContainer 서비스로 분리했다.
- `IFloatingIconFeedbackService`/`GameManagerFloatingIconFeedbackService`를 추가해 DamageNumber 기반 floating icon 출력을 주입 가능한 서비스로 분리했다.
- `CharacterFloatingIcon`은 static facade로 남기되 `DungeonRuntimeLifetimeScope`에서 `IFloatingIconFeedbackService`를 resolve하도록 바꿨고, DI/프리팹 누락은 예외로 드러나게 했다. 이후 93번에서 static facade까지 제거했다.
- `UIBuildingInfo`는 `IBuildingDefinitionLookup`과 `IUiTouchGuardService`를 method injection으로 받도록 바꾸고, unreachable `return`을 제거했다.
- `DungeonRuntimeLifetimeScope`에 새 서비스를 등록하고 씬의 `UIBuildingInfo`를 명시 주입한다.

효과:

- `UIBuildingInfo.cs`에서 `DataManager.Instance`와 `UIManager.Instance` 직접 사용이 제거됐다.
- `CharacterFloatingIcon.cs`에서 `GameManager.Instance` 직접 사용이 제거됐다.
- `UIBuildingInfo`는 아직 `UtilSingleton` 타입이라 `SingletonAccess` 후보에 남지만, 데이터 조회와 touch guard 책임은 DI 경계로 이동했다.
- `CharacterFloatingIcon`은 `SingletonAccess` 후보에서 `DependencyInjection, RuntimeObjectCreation, SceneMutation` 후보로 이동했다. 이후 93번에서 service-locator 후보에서도 빠졌다.
- Unity 콘솔의 unreachable code 경고가 사라져 경고 수가 6 -> 5로 줄었다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 31 -> 30, `DependencyInjection`은 72 -> 74로 이동했다.
- 감사 요약은 287 script files, 5815 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after UI feedback DI refactor: Passed
Targeted direct coupling scan: no DataManager.Instance / UIManager.Instance / GameManager.Instance remains in UIBuildingInfo.cs, CharacterFloatingIcon.cs, DungeonRuntimeLifetimeScope.cs, or UiPopupService.cs
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:03:13 +09:00
```


### 44. Social reputation facility score DI 경계 분리

대상:

- `Assets/Scripts/Infrastructure/SocialReputationRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/AI/FacilityCandidateScorer.cs`

진단:

- `FacilityCandidateScorer.GetReputationBias()`가 `SocialReputationRuntime.Instance`를 직접 읽어 AI 시설 점수 계산이 social runtime singleton에 묶여 있었다.
- social reputation runtime이 없으면 조용히 `0f` bias로 진행해, 점수 계산 테스트에서 social layer 누락을 알아차리기 어려웠다.

변경:

- `ISocialReputationRuntimeProvider`를 추가해 social reputation runtime 조회를 VContainer 서비스로 분리했다.
- `ISocialReputationBiasService`를 추가해 시설 utility bias 계산만 scorer가 사용할 수 있는 작은 경계로 만들었다.
- `SocialReputationBiasAccess` static facade를 추가해 기존 static scorer 구조를 크게 흔들지 않고 DI 서비스를 통과하게 했다.
- `DungeonRuntimeLifetimeScope`에 social reputation provider와 bias service를 singleton으로 등록했다.

효과:

- `FacilityCandidateScorer.cs`에서 `SocialReputationRuntime.Instance` 직접 사용이 제거됐다.
- social reputation runtime이 composition에 없으면 silent `0f` fallback이 아니라 명시 예외로 드러난다.
- `FacilityCandidateScorer.cs`는 `SingletonAccess` 후보에서 `DependencyInjection` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 30 -> 29, `DependencyInjection`은 74 -> 76으로 이동했다.
- 감사 요약은 288 script files, 5825 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after social reputation bias DI refactor: Passed
Targeted direct coupling scan: no SocialReputationRuntime.Instance remains in FacilityCandidateScorer.cs, SocialReputationRuntimeProvider.cs, or DungeonRuntimeLifetimeScope.cs
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:07:22 +09:00
```


### 45. Synthesis/Evolution/Codex runtime provider access 분리

대상:

- `Assets/Scripts/Infrastructure/RuntimePanelProviders.cs`
- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Codex/CodexSystem.cs`

진단:

- `FacilitySynthesisRuntime`, `FacilityEvolutionRuntime`, `CodexRuntime`의 static `Instance`가 `FindFirstObjectByType`를 직접 호출하고 있었다.
- 세 runtime 모두 `BlueprintResearchRuntime.Instance`로 연구 상태를 직접 읽어 synthesis/evolution/codex UI와 기록 경계가 research singleton에 묶여 있었다.
- synthesis/evolution의 건물 생성 경로가 `GridTexture.Instance`로 visual factory를 직접 만들고 있었다.
- `CodexRuntime.Awake()`가 reference import를 즉시 실행해, scene injection보다 먼저 research state를 읽을 위험이 있었다.

변경:

- `FacilityEvolutionRuntimeAccess`, `FacilitySynthesisRuntimeAccess`, `CodexRuntimeAccess`를 추가해 기존 static `Instance` 호환 API를 VContainer provider 기반으로 바꿨다.
- `IBlueprintResearchStateService`와 `BlueprintResearchStateAccess`를 추가해 research state 조회를 DI 서비스로 분리했다.
- `FacilitySynthesisRuntime`과 `FacilityEvolutionRuntime`은 `IBlueprintResearchStateService`, `IGridTextureProvider`를 method injection으로 받도록 했다.
- `CodexRuntime`은 `IBlueprintResearchStateService`를 method injection으로 받고, reference import를 `Awake()`에서 `Start()`로 늦춰 주입 이후 실행되게 했다.
- `GridFacilityEvolutionBuildingReplacer`의 parameterless `GridTexture.Instance` fallback을 제거하고 `GridBuildingFactory`를 명시 요구하게 했다.
- `DungeonRuntimeLifetimeScope`가 세 runtime과 blueprint research state service를 등록/주입한다.

효과:

- non-Editor runtime의 synthesis/evolution/codex 타깃에서 `FindFirstObjectByType<...Runtime>`, `BlueprintResearchRuntime.Instance`, `GridTexture.Instance` 직접 사용이 제거됐다.
- `CodexSystem`, `FacilityEvolutionRuntime`, `FacilitySynthesisSystem`은 `GlobalObjectLookup`/`SingletonAccess` 후보에서 빠지고 `DependencyInjection` 후보로 이동했다.
- `FacilityEvolutionRuntime`과 `FacilitySynthesisSystem`의 `ResourcesAccess`는 recipe catalog 로딩 때문에 아직 남아 있으며 다음 분리 후보로 유지된다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 30 -> 27, `SingletonAccess`는 29 -> 26, `DependencyInjection`은 76 -> 79로 이동했다.
- 감사 요약은 288 script files, 5848 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after runtime provider access refactor: Passed
Targeted direct coupling scan: no FacilitySynthesisRuntime.Instance / FacilityEvolutionRuntime.Instance / CodexRuntime.Instance / BlueprintResearchRuntime.Instance / GridTexture.Instance / FindFirstObjectByType<...Runtime> remains in the touched runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:14:32 +09:00
```

### 46. Blueprint research shop unlock state DI 분리

대상:

- `Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`

진단:

- `BlueprintResearchRuntime.Instance`가 `FindFirstObjectByType<BlueprintResearchRuntime>()`를 직접 호출하는 static 진입점으로 남아 있었다.
- 연구 완료 시 기본 구매 해금을 적용하기 위해 `BlueprintResearchRuntime.ShopUnlockState`가 `FindFirstObjectByType<DailyFacilityShopRuntime>()`로 상점 런타임을 직접 찾고 있었다.
- research runtime이 shop runtime의 위치를 아는 구조라 씬 배치/테스트 순서에 결합되어 있었다.

변경:

- `IDailyFacilityShopRuntimeProvider`, `IFacilityShopUnlockStateService`와 구현체를 추가했다.
- `BlueprintResearchRuntime`은 `IFacilityShopUnlockStateService`를 VContainer method injection으로 받고, 누락 시 명시 예외를 던지게 했다.
- `BlueprintResearchRuntime.Instance` 직접 조회 API를 제거하고, 연구 작업 여부는 기존 `BlueprintResearchWorkAccess` DI 진입점으로 위임하게 했다.
- `DungeonRuntimeLifetimeScope`에 상점 런타임 provider/service를 등록하고, 씬의 `BlueprintResearchRuntime`에도 injection을 수행한다.

효과:

- non-Editor runtime에서 `BlueprintResearchRuntime.Instance`, `FindFirstObjectByType<BlueprintResearchRuntime>`, `FindFirstObjectByType<DailyFacilityShopRuntime>` 타깃 검색이 제거됐다.
- `BlueprintResearchSystem.cs`는 `GlobalObjectLookup` 후보에서 빠지고 `DependencyInjection` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 27 -> 26, `DependencyInjection`은 79 -> 81로 이동했다.
- 감사 요약은 289 script files, 5858 extracted declarations 상태로 갱신했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after BlueprintResearchRuntime DI refactor: Passed
Targeted direct coupling scan: no BlueprintResearchRuntime.Instance / FindFirstObjectByType<BlueprintResearchRuntime> / FindFirstObjectByType<DailyFacilityShopRuntime> remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:22:25 +09:00
```

### 47. Facility evolution record unused global entry 제거

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`

진단:

- `FacilityEvolutionRecordRuntime.Instance`가 `FindFirstObjectByType<FacilityEvolutionRecordRuntime>()`를 직접 호출하는 static 전역 진입점으로 남아 있었다.
- non-Editor 런타임 검색 결과 실제 사용처는 없었고, 런타임은 이벤트 리스너로만 동작하고 있었다.

변경:

- 사용되지 않는 `FacilityEvolutionRecordRuntime.Instance` 속성을 제거했다.
- provider를 새로 만들지 않고, 불필요한 전역 접근면 자체를 없앴다.

효과:

- `FacilityEvolutionRecordRuntime.cs`는 `GlobalObjectLookup` 후보에서 빠지고 `EventBus, RuntimeObjectCreation` 후보만 남았다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 26 -> 25로 내려갔다.

검증:

```text
Unity MCP AssetDatabase.Refresh after FacilityEvolutionRecordRuntime.Instance removal: Passed
Targeted direct coupling scan: no FacilityEvolutionRecordRuntime.Instance / FindFirstObjectByType<FacilityEvolutionRecordRuntime> remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:27:37 +09:00
```

### 48. Staff discontent unused global entry 제거

대상:

- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`

진단:

- `StaffDiscontentRuntime.Instance`가 `FindFirstObjectByType<StaffDiscontentRuntime>()`를 직접 호출하는 static 전역 진입점으로 남아 있었다.
- 실제 non-Editor 사용처는 없었고, 작업 차단/효율/반란 대상 조회는 이미 `StaffDiscontentRuntimeProvider`와 `StaffDiscontentRuntimeService` 경유로 이동해 있었다.

변경:

- 사용되지 않는 `StaffDiscontentRuntime.Instance` 속성을 제거했다.

효과:

- `StaffDiscontentRuntime.Instance`와 `FindFirstObjectByType<StaffDiscontentRuntime>` 직접 검색 결과가 0건이 됐다.
- 감사 문서상 `StaffDiscontentSystem.cs`는 기존처럼 `GlobalObjectLookup, EventBus` 후보로 남는다. `ProcessAllStaff()`의 `FindObjectsByType<CharacterActor>`가 아직 다음 분리 후보이기 때문이다.

검증:

```text
Unity MCP AssetDatabase.Refresh after StaffDiscontentRuntime.Instance removal: Passed
Targeted direct coupling scan: no StaffDiscontentRuntime.Instance / FindFirstObjectByType<StaffDiscontentRuntime> remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:30:44 +09:00
```

### 49. Event alert Canvas 조회 DI 분리

대상:

- `Assets/Scripts/Operation/EventAlertCanvasProvider.cs`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`
- `Assets/Scripts/Infrastructure/DungeonSceneRuntimeReferences.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `EventAlertUiFactory.FindOrCreateCanvas()`가 `Object.FindFirstObjectByType<Canvas>()`를 직접 호출하고 있었다.
- 이벤트 알림 UI presenter가 static factory를 통해 Canvas 조회와 UI 생성 책임을 함께 끌어와, Canvas 공급 책임과 UI hierarchy 생성 책임이 섞여 있었다.

변경:

- `IEventAlertCanvasProvider`와 `EventAlertCanvasProvider`를 추가했다.
- `EventAlertCanvasProvider`는 `DungeonSceneRuntimeReferences.Canvas`를 우선 사용하고, 씬 Canvas가 없을 때만 `RuntimeUICanvas`를 명시적으로 생성한다.
- `EventAlertViewPresenterFactory`가 `IEventAlertCanvasProvider`를 생성자 주입으로 받고 presenter에 전달한다.
- `EventAlertUiFactory`에서 Canvas 검색/생성 함수를 제거하고 순수 UI 오브젝트 생성 책임만 남겼다.
- `DungeonRuntimeLifetimeScope`가 Canvas provider를 등록하고 scene reference capture에 Canvas를 포함한다.

효과:

- non-Editor runtime에서 `EventAlertUiFactory`의 `FindFirstObjectByType<Canvas>` 직접 검색이 제거됐다.
- 남은 직접 조회 목록에서 `EventAlertUiFactory`가 빠졌다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 25 -> 24, `DependencyInjection`은 81 -> 82로 이동했다.
- 새 provider 파일 추가로 감사 요약은 290 script files, 5862 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after EventAlert canvas provider refactor: Passed
Targeted direct coupling scan: no Object.FindFirstObjectByType<Canvas> / FindFirstObjectByType<Canvas> / EventAlertUiFactory.FindOrCreateCanvas remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:34:28 +09:00
```

### 50. DataManager singleton 제거 및 catalog DI 전환

대상:

- `Assets/DataManager.cs`
- `Assets/Scripts/Infrastructure/DataCatalogService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `DataManager`가 plain C# singleton으로 `DataManager.Instance`를 노출하고 있었다.
- `DataManagerCatalog`가 `DataManager.Instance.GetData<T>()`를 직접 호출해 데이터 카탈로그 경계가 static singleton에 묶여 있었다.
- `Assets/DataManager.cs`가 `Assets/Scripts` 밖에 있어 기존 함수 인벤토리에서 빠져 있었다.

변경:

- `DataManager.Instance`와 내부 static instance를 제거하고 public constructor를 가진 일반 서비스로 바꿨다.
- `DungeonRuntimeLifetimeScope`가 `DataManager`를 VContainer singleton으로 등록한다.
- `DataManagerCatalog`는 `DataManager`를 생성자 주입으로 받아 `GetData<T>()`를 호출한다.
- `Assets/DataManager.cs`를 함수 인벤토리와 커플링 감사 문서에 포함했다.

효과:

- runtime 코드에서 `DataManager.Instance` 직접 검색 결과가 0건이 됐다.
- `DataCatalogService.cs`는 `SingletonAccess` 후보에서 빠지고 `DependencyInjection` 후보만 남았다.
- `Assets/DataManager.cs`는 `ResourcesAccess` 후보로 새로 드러났다.
- 감사 요약은 291 script files, 5867 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after DataManager DI refactor: Passed
Targeted direct coupling scan: no DataManager.Instance / public static DataManager Instance remains under Assets
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:39:59 +09:00
```

### 51. Grid runtime singleton 제거 및 UtilSingleton 자동 탐색 제거

대상:

- `Assets/Scripts/Grid/System/GridSystemManager.cs`
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Utils/UtilSingleton.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`

진단:

- `GridSystemManager`와 `GridTexture`가 static `Instance`로 씬 오브젝트 접근을 열어 두고 있었다.
- `UtilSingleton<T>.Instance`는 없으면 씬 전체 검색 후 GameObject를 자동 생성하는 경로까지 포함하고 있어 DI 전환과 테스트 격리를 방해했다.
- Editor debug scenario는 refactor 대상은 아니지만 compile 유지를 위해 grid manager 접근만 최소 수정이 필요했다.

변경:

- `GridSystemManager.Instance`와 `GridTexture.Instance`를 제거하고, grid manager는 자기 수명주기에서 `EnsureGridInitialized()`만 수행하게 했다.
- `GridTexture`는 static access 없이 `IGridBuildingVisual` 구현과 tilemap 렌더링 책임만 남겼다.
- `UtilSingleton<T>.Instance`의 scene search / GameObject auto-create 경로를 제거하고 `HasInstance`, `TryGetInstance()`, `Current`만 남겼다.
- 두 Editor debug scenario의 compile-only grid lookup을 `Object.FindFirstObjectByType<GridSystemManager>()`로 바꿨다. Editor 밑 시나리오는 이번 runtime refactor 범위에서 제외한다.

효과:

- non-Editor runtime에서 `GridSystemManager.Instance`, `GridTexture.Instance`, generic `UtilSingleton<T>.Instance` 자동 조회가 제거됐다.
- `GridSystemManager.cs`는 `GlobalObjectLookup, Reflection` 후보에서 빠지고 `SceneMutation` 후보만 남았다.
- `GridTexture.cs`는 `GlobalObjectLookup` 후보에서 빠지고 실제 책임인 `RuntimeObjectCreation, SceneMutation` 후보로 재분류됐다.
- `UtilSingleton.cs`는 `GlobalObjectLookup, Reflection, RuntimeObjectCreation` 후보에서 빠지고 static holder 성격의 `SingletonAccess` 후보만 남았다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 24 -> 21, `Reflection`은 56 -> 54, `SceneMutation`은 88 -> 89로 갱신했다.
- 감사 요약은 291 script files, 5848 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after grid/util singleton refactor: Passed
Targeted direct coupling scan: no GridSystemManager.Instance / GridTexture.Instance / UtilSingleton<T>.Instance auto-lookup remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 16:46:12 +09:00
```

### 52. Local LLM runtime provider 도입 및 AI 호출부 DI 전환

대상:

- `Assets/Scripts/Infrastructure/LocalLlmRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs`
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`
- `Assets/Scripts/Character/AI/CustomerPersonaRuntime.cs`
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs`

진단:

- AI director, persona, dialogue bubble, social rumor, facility evolution LLM proposal이 `LocalLlmRequestQueue.GetOrCreateInstance()`를 직접 호출했다.
- `LocalLlmRequestQueue`와 `SocialReputationRuntime`, `AiDirectorRuntime`에는 load-time 자동 생성 경로가 있어 주입 누락을 숨길 수 있었다.
- `CachedLocalLlmFacilityEvolutionProposalProvider`도 생성자 기본값으로 queue를 몰래 만드는 fallback을 갖고 있었다.

변경:

- `ILocalLlmRuntimeProvider`와 `LocalLlmRuntimeProvider`를 추가하고 VContainer에 등록했다.
- `AiDirectorRuntime`, `CustomerPersonaRuntime`, `CharacterDialogueRuntime`, `SocialReputationRuntime`이 `ILocalLlmRuntimeProvider`를 주입받아 LLM queue를 조회한다.
- `FacilityEvolutionRuntime`이 local LLM provider를 주입받고, room evolution LLM proposal provider에 명시적으로 전달한다.
- `LocalLlmRequestQueue.GetOrCreateInstance()`와 queue load-time auto-create를 제거했다.
- `SocialReputationRuntime`과 `AiDirectorRuntime`의 load-time auto-create를 제거했다.
- 당시 `LocalLlmRequestQueue.Instance`와 `SocialReputationRuntime.Instance`는 Editor debug scenario 호환용으로만 `#if UNITY_EDITOR` 안에 남겼고, 이후 145번에서 제거했다.

효과:

- non-Editor runtime의 `LocalLlmRequestQueue.GetOrCreateInstance()` 직접 호출이 0건이 됐다.
- LLM 호출부가 queue 생성 책임을 갖지 않고, 씬에 명시적으로 존재하는 queue를 provider로만 사용한다.
- `CachedLocalLlmFacilityEvolutionProposalProvider`는 더 이상 기본 생성자에서 queue fallback을 만들지 않는다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 21 -> 20, `RuntimeObjectCreation`은 76 -> 75, `SceneMutation`은 89 -> 88, `DependencyInjection`은 82 -> 87로 이동했다.
- 새 provider 파일 추가로 감사 요약은 292 script files, 5853 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after Local LLM DI refactor: Passed
Targeted direct coupling scan: no LocalLlmRequestQueue.GetOrCreateInstance / LocalLlm auto-create / SocialReputation auto-create / AiDirector auto-create remains in non-Editor runtime files
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Generated: 2026-07-04 17:02:20 +09:00
```

### 53. GameManager runtime auto-attach 제거 및 Invasion runtime 명시 DI 전환

대상:

- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs`
- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/Invasion/InvasionThreatRuntime.cs`
- `Assets/Scripts/Invasion/InvasionCombatReportSystem.cs`
- `Assets/Scripts/Invasion/InvasionCombatReportRuntime.cs`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/CharacterAiTestScene.unity`

진단:

- `GameManager`가 운영일/AI/침입/메타/합성/코덱 런타임 컴포넌트를 `Awake()`에서 직접 `AddComponent`로 붙이고 있었다.
- `GameManager.GetMouseWorldPos()`가 grid building 입력 provider와 직접 엮여 있어 UI 입력 책임이 시간/게임 상태 관리자에 섞여 있었다.
- `DungeonRuntimeLifetimeScope`도 씬에 없으면 런타임에 새 GameObject를 생성하는 경로가 있어 DI 루트 누락을 숨길 수 있었다.
- `InvasionThreatRuntime`과 `InvasionCombatReportRuntime`이 `*System.cs` 파일 안에 있어 Unity scene serialization에서 runtime MonoBehaviour mapping이 불안정했다.
- `InvasionThreatRuntime`에는 미사용 `Instance` 전역 조회와 정적 calculator를 통한 run/meta runtime 접근이 남아 있었다.

변경:

- `GameManager`에서 `RequiredRuntimeTypes`, `EnsureOperatingDaySystems`, `EnsureRuntimeComponent`, `FindExistingRuntimeComponent`, `GetMouseWorldPos`를 제거했다.
- `SampleScene`의 `DungeonRuntimeSystems`에 운영일, AI, LLM queue, 침입, 오펜스, 연구, 합성, 진화, 코덱, 불만 런타임과 `DungeonRuntimeLifetimeScope`를 명시적으로 배치했다.
- `CharacterAiTestScene`에는 테스트 씬용 `DungeonRuntimeSystems`와 `DungeonRuntimeLifetimeScope`를 명시적으로 배치했다.
- `DungeonRuntimeLifetimeScope`의 load-time auto-create 경로를 제거하고, 씬에 배치된 scope만 composition root가 되도록 했다.
- `GameWorldPointerPositionProvider`를 `SceneCameraWorldPointerPositionProvider`로 교체해 `GameManager` 대신 씬 카메라를 주입 조회한다.
- `InvasionThreatRuntime`과 `InvasionCombatReportRuntime`을 각각 별도 파일로 분리했다.
- `InvasionThreatRuntime`은 `IInvasionThreatWorldSampler`, `IRunVariableRuntimeReader`, `IMetaProgressionRuntimeReader`를 VContainer로 주입받고, 주입 누락 시 명시 예외를 던진다.
- `InvasionThreatCalculator`는 run variable을 직접 읽지 않는 순수 계산기로 되돌렸다.

효과:

- `GameManager`는 시간, 일차, 게임 속도, 운영일 이벤트 발행만 담당한다.
- runtime component composition은 `GameManager`가 아니라 씬과 `DungeonRuntimeLifetimeScope`가 담당한다.
- `InvasionThreatRuntime.Instance`, `RunVariableRuntime.Instance`, `MetaProgressionRuntime.Instance` 기반 직접 접근이 침입 위협 런타임에서 사라졌다.
- Unity scene holder의 missing script가 0개로 확인됐다.
- `script-coupling-audit.md` 기준 `GameManager`는 `GlobalObjectLookup, Reflection, RuntimeObjectCreation, SceneMutation` 후보에서 빠지고 `SingletonAccess` 후보만 남았다.

검증:

```text
Unity MCP AssetDatabase.Refresh after InvasionThreatRuntime DI tightening: Passed
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Scene validation:
- SampleScene DungeonRuntimeSystems: missingScripts=0, hasScope=True, hasThreat=True, hasReport=True
- CharacterAiTestScene DungeonRuntimeSystems: missingScripts=0, hasScope=True, hasThreat=False, hasReport=False
Targeted direct coupling scan:
- no GameManager runtime auto-attach helpers remain
- no InvasionThreatRuntime.Instance usage remains
- no RunVariableRuntime.Instance / MetaProgressionRuntime.Instance usage remains in invasion threat runtime
Generated: 2026-07-04 17:45:00 +09:00
```

### 54. OperatingDaySettlementRuntime report snapshot DI 전환

대상:

- `Assets/Scripts/Operation/OperatingDaySettlement.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `OperatingDaySettlementRuntime.BuildReport()`가 `FindObjectsByType<BuildableObject>`와 `FindObjectsByType<CharacterActor>`로 씬 전체를 직접 조회했다.
- 정산 런타임이 `DungeonRuntimeLifetimeScope`의 scene component injection 대상이 아니라, 주입 누락이 있어도 직접 조회 경로로 숨겨질 수 있었다.

변경:

- `OperatingDaySettlementRuntime`에 `IDungeonSceneComponentQuery` VContainer method injection을 추가했다.
- `BuildReport()`가 `RequireSceneQuery().All<BuildableObject>()`와 `RequireSceneQuery().All<CharacterActor>()`로 리포트 스냅샷을 수집하게 했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`가 씬의 `OperatingDaySettlementRuntime`을 주입 대상으로 포함하게 했다.
- 주입 누락 시 조용한 fallback 없이 `InvalidOperationException`을 던지도록 했다.

효과:

- 운영일 정산 리포트가 Unity scene API를 직접 호출하지 않고, 씬 조회 boundary를 통해 데이터를 수집한다.
- `OperatingDaySettlement.cs`는 `GlobalObjectLookup` 후보에서 빠지고 `DependencyInjection, EventBus` 후보로 재분류됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 19 -> 18, `DependencyInjection`은 87 -> 88로 이동했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after OperatingDaySettlement DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Targeted direct coupling scan:
- no FindObjectsByType / FindFirstObjectByType / FindObjectOfType / GameObject.Find remains in OperatingDaySettlement.cs or DungeonRuntimeLifetimeScope.cs target
- no .Instance or public static Instance remains in OperatingDaySettlement.cs or DungeonRuntimeLifetimeScope.cs target
Generated: 2026-07-04 17:55:00 +09:00
```

### 55. WorkforceReplanService idle worker replan DI 전환

대상:

- `Assets/Scripts/Character/Work/WorkforceReplanService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `WorkforceReplanService.RequestIdleWorkersToReplan()`이 `Object.FindObjectsByType<AbilityWork>`로 씬의 작업 능력을 직접 훑고 있었다.
- 호출부인 `Shop`은 self-service/checkout 대기 상태가 바뀔 때 재계획을 요청해야 하므로, 호출 API는 유지하되 조회 책임만 분리하는 쪽이 안전했다.

변경:

- `IWorkforceReplanService`와 `DungeonWorkforceReplanService`를 추가했다.
- `DungeonWorkforceReplanService`가 `IDungeonSceneComponentQuery.All<AbilityWork>()`를 통해 active worker ability를 수집하게 했다.
- 이 단계에서는 기존 `WorkforceReplanService` static API를 VContainer에서 `IWorkforceReplanService`를 resolve하는 compatibility facade로 전환했다. 이후 89번에서 해당 facade까지 제거했다.
- `DungeonRuntimeLifetimeScope`에 `DungeonWorkforceReplanService` 등록을 추가했다.
- 플레이 중 scope/service가 없으면 fallback 없이 `InvalidOperationException`을 던진다.

효과:

- `Shop` 호출부를 흔들지 않고 worker replan의 씬 조회 책임을 DI service boundary로 이동했다.
- `WorkforceReplanService.cs`는 `GlobalObjectLookup` 후보에서 빠지고 `DependencyInjection` 후보로 재분류됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 18 -> 17, `DependencyInjection`은 88 -> 89로 이동했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after WorkforceReplanService DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Targeted direct coupling scan:
- no FindObjectsByType / FindFirstObjectByType / FindObjectOfType / Object.FindObjectsByType / GameObject.Find remains in WorkforceReplanService.cs or DungeonRuntimeLifetimeScope.cs target
Generated: 2026-07-04 18:01:00 +09:00
```

### 56. StaffDiscontentRuntime scene query DI 전환

대상:

- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `StaffDiscontentRuntime.ProcessAllStaff()`가 운영일 종료 시 `FindObjectsByType<CharacterActor>`로 모든 캐릭터를 직접 조회했다.
- `DispatchAutoSuppress()`도 반란 직원 진압 후보를 찾기 위해 같은 직접 scene lookup을 반복했다.
- 이전 단계에서 `StaffDiscontentRuntime.Instance`는 제거됐지만, 런타임 내부의 scene-wide lookup은 아직 남아 있었다.

변경:

- `StaffDiscontentRuntime`에 `IDungeonSceneComponentQuery` VContainer method injection을 추가했다.
- `ProcessAllStaff()`와 `DispatchAutoSuppress()`가 `RequireSceneQuery().All<CharacterActor>()`를 통해 후보를 수집하게 했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`가 씬의 `StaffDiscontentRuntime`을 주입 대상으로 포함하게 했다.
- 주입 누락 시 fallback 없이 `InvalidOperationException`을 던지도록 했다.

효과:

- 직원 불만 처리의 캐릭터 수집 책임이 runtime 내부 Unity API 호출에서 scene query boundary로 이동했다.
- `StaffDiscontentSystem.cs`는 `GlobalObjectLookup` 후보에서 빠지고 `DependencyInjection, EventBus` 후보로 재분류됐다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 17 -> 16, `DependencyInjection`은 89 -> 90으로 이동했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after StaffDiscontentRuntime DI query refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer warnings
Targeted direct coupling scan:
- no FindObjectsByType / FindFirstObjectByType / FindObjectOfType / Object.FindObjectsByType / GameObject.Find remains in StaffDiscontentSystem.cs or DungeonRuntimeLifetimeScope.cs target
Generated: 2026-07-04 18:04:00 +09:00
```

### 57. AI director/social/scheduler scene lookup DI 전환

대상:

- `Assets/Scripts/Character/AI/AiDirectorContextSceneQuery.cs`
- `Assets/Scripts/Character/AI/AiDirectorContextAggregator.cs`
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`
- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- AI director context capture, director actor scan, scheduler initial actor registration, macro facility lookup, social rumor actor/facility scan이 `FindObjectsByType` 계열 직접 scene lookup에 묶여 있었다.
- `AiDirectorContextAggregator.Build(actor)`가 내부에서 scene query를 숨겨 호출 경계가 불명확했다.
- `CharacterAiScheduler`와 `SocialReputationRuntime`은 `Awake`/`OnEnable`이 VContainer 주입보다 먼저 돌 수 있어, 주입 의존 scene scan을 생명주기상 늦춰야 했다.

변경:

- `IAiDirectorContextSceneQuery`/`AiDirectorContextSceneQuery`를 추가해 AI director용 actor/facility snapshot을 injected scene query로 수집한다.
- `AiDirectorRuntime`은 `ILocalLlmRuntimeProvider`와 `IAiDirectorContextSceneQuery`를 함께 주입받고, actor scan과 prompt summary를 모두 snapshot 경계로 통일했다.
- `AiDirectorContextAggregator.Build(actor)` convenience overload를 제거하고 snapshot을 명시 인자로 받게 했다.
- `CharacterAiScheduler`는 `IDungeonSceneComponentQuery`를 주입받고, 기존 `Awake` 직접 등록을 `Construct`/`Start`/주입 후 경로로 늦췄다.
- `ICharacterAiFacilityLookup`/`CharacterAiFacilityLookup`을 추가해 `CharacterAiDecisionPipeline.FindFacility()`의 직접 facility scan을 DI-backed service로 이동했다.
- `SocialReputationRuntime`은 `IDungeonSceneComponentQuery`를 주입받고 actor registration, nearby facility scan, log facility resolve, rumor spread, known-facility validation을 scene query 경계로 옮겼다.
- Editor debug scenario의 context compression 검증은 명시 `AiDirectorContextSceneSnapshot`을 넘기도록 최소 호환 수정했다.
- `DungeonRuntimeLifetimeScope`에 AI scene query/facility lookup service 등록과 `CharacterAiScheduler` scene component injection을 추가했다.

효과:

- non-Editor runtime scripts에서 `FindObjectsByType`/`FindFirstObjectByType`/`FindObjectOfType`/`Object.FindObjectsByType`/`GameObject.Find` 직접 검색 결과가 0건이 됐다.
- `AiDirectorContextSceneQuery`, `AiDirectorRuntime`, `CharacterAiDecisionPipeline`, `CharacterAiScheduler`, `SocialReputationRuntime`이 `GlobalObjectLookup` 후보에서 빠졌다.
- `script-coupling-audit.md` 기준 `GlobalObjectLookup`은 16 -> 11, `DependencyInjection`은 90 -> 93으로 이동했다.

검증:

```text
Unity MCP AssetDatabase.Refresh after AI DI compile fix: Passed
Unity Console: 0 errors, 0 warnings
Targeted direct coupling scan:
- no FindObjectsByType / FindFirstObjectByType / FindObjectOfType / Object.FindObjectsByType / GameObject.Find remains in non-Editor Assets/Scripts
Generated: 2026-07-04 18:14:00 +09:00
```

### 58. GameData/earning feedback GameManager.Current 제거

대상:

- `Assets/Scripts/Infrastructure/GameRuntimeServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Buildings/Shop.cs`
- `Assets/Scripts/Buildings/SO/Conditions/ConditionNeedMoney.cs`

진단:

- `Shop.cs`는 매출 정산과 수익 숫자 표시를 위해 `GameManager.Current`에 직접 접근했다.
- `ConditionNeedMoney.cs`는 빌드 조건 검증/소모 시 `ScriptableObject` 조건에서 `GameManager.Current.gameData`를 직접 읽었다.
- 이 구조는 씬 싱글톤 상태가 판매 로직, 빌드 조건 자산, 피드백 연출에 새어 나가 테스트와 초기화 순서를 불안정하게 만든다.

변경:

- `IGameDataProvider`와 `IFloatingNumberFeedbackService`를 추가하고, 실제 `GameManager` 조회는 `GameManagerGameDataProvider`/`GameManagerFloatingNumberFeedbackService` 안으로 한정했다.
- `GameDataAccess`와 `FloatingNumberFeedbackAccess`는 기존 정적 호출부 호환을 유지하되 내부에서는 `DungeonRuntimeLifetimeScope.TryResolve`로 VContainer 등록 서비스만 사용한다.
- `DungeonRuntimeLifetimeScope`에 GameData provider와 floating-number feedback service를 singleton으로 등록했다.
- `Shop.cs`는 초기화/매출 정산/수익 숫자 표시에서 `GameManager.Current` 대신 DI-backed access facade를 사용한다.
- `ConditionNeedMoney.cs`는 빌드 조건에서 직접 싱글톤을 읽지 않고 `GameDataAccess`를 통해 `GameData`를 받는다.

효과:

- non-Editor `Assets/Scripts`에서 `GameManager.Current` 직접 사용이 0건이 됐다.
- `GameManager` 싱글톤 결합은 런타임 provider 경계로 좁아졌고, 판매/건설 조건 로직은 `GameData` 공급 방식에 덜 묶인다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 93 -> 95로 이동했다.
- 새 provider 파일 추가와 targeted refresh로 감사 요약은 296 script files, 5961 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after GameData DI service refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no GameManager.Current / GameManager.TryGetInstance / GameManager.HasInstance / GameManager.Instance remains in non-Editor Assets/Scripts
```

### 59. FacilityShop Resources.LoadAll catalog DI 분리

대상:

- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `FacilityShopService.CreateDailyOffers`, `CreateBasicPurchaseOffers`, `FindBuildingById`의 기본 경로가 `Resources.LoadAll<BuildingSO>`/`Resources.LoadAll<FacilityBlueprintSO>`를 직접 호출했다.
- 시설 상점의 후보 생성은 게임 데이터 catalog의 책임인데, 도메인 정적 서비스가 리소스 경로를 직접 알고 있어 테스트와 데이터 로딩 경계가 섞여 있었다.

변경:

- `IFacilityShopCatalog`와 `DataCatalogFacilityShopCatalog`를 추가해 상점용 building/blueprint 조회를 `IDataCatalog` 뒤로 이동했다.
- `FacilityShopCatalogAccess`를 추가해 기존 정적 `FacilityShopService` 호출 경로는 유지하되 내부 데이터 조회는 VContainer 등록 catalog를 통과하게 했다.
- `FacilityShopService`에서 `LoadAllBuildings`/`LoadAllBlueprints`와 직접 `Resources.LoadAll` 호출을 제거했다.
- `DungeonRuntimeLifetimeScope`에 `DataCatalogFacilityShopCatalog`를 `IFacilityShopCatalog`로 등록했다.

효과:

- `FacilityShopSystem.cs`가 `ResourcesAccess` 후보에서 빠지고 `DependencyInjection, EventBus` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `ResourcesAccess`는 18 -> 17, `DependencyInjection`은 95 -> 96으로 이동했다.
- 감사 요약은 296 script files, 5973 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after FacilityShop catalog DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.LoadAll remains in Assets/Scripts/FacilityShop/FacilityShopSystem.cs
```

### 60. Offense reward Resources.LoadAll catalog DI 분리

대상:

- `Assets/Scripts/Offense/OffenseRewardSelector.cs`
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `OffenseRewardSelector.SelectRareFacility`와 `SelectBlueprint`이 보상 후보를 고르기 위해 `Resources.LoadAll<BuildingSO>`/`Resources.LoadAll<FacilityBlueprintSO>`를 직접 호출했다.
- 원정 보상 selector가 리소스 경로와 데이터 로딩 방식을 알고 있어, 보상 규칙과 catalog 공급 책임이 섞여 있었다.

변경:

- `IOffenseRewardCatalog`와 `DataCatalogOffenseRewardCatalog`를 추가해 building/blueprint reward pool을 `IDataCatalog` 뒤로 이동했다.
- `OffenseRewardCatalogAccess`를 추가해 정적 selector의 기존 호출 형태를 유지하되 내부 데이터 조회는 VContainer 등록 catalog를 통과하게 했다.
- `OffenseRewardSelector`에서 직접 `Resources.LoadAll` 호출과 `UnityEngine` using을 제거했다.
- `DungeonRuntimeLifetimeScope`에 `DataCatalogOffenseRewardCatalog`를 `IOffenseRewardCatalog`로 등록했다.

효과:

- `OffenseRewardSelector.cs`가 `ResourcesAccess` 후보에서 빠지고 `DependencyInjection` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `ResourcesAccess`는 17 -> 16, `DependencyInjection`은 96 -> 97로 이동했다.
- 감사 요약은 296 script files, 5984 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after Offense reward catalog DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.LoadAll remains in Assets/Scripts/Offense/OffenseRewardSelector.cs
```

### 61. Run start/owner candidate Resources.LoadAll provider 경계 분리

대상:

- `Assets/Scripts/Run/RunStartVariableSelector.cs`
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `RunStartVariableSelector.Create`가 시작 시설/손님/설계도 후보를 고르기 위해 `Resources.LoadAll`을 직접 호출했다.
- `OwnerRunManager.EnsureOwnerCandidates`가 `Awake`에서 owner 후보를 직접 `Resources.LoadAll<CharacterSO>`로 채워, owner 선택 책임과 asset loading 책임이 섞여 있었다.
- `OwnerRunManager.Awake`는 VContainer 주입보다 먼저 실행될 수 있어, 후보 로딩과 후보 정규화를 생명주기상 분리해야 했다.

변경:

- `IRunStartVariableCatalog`/`RunStartVariableCatalog`/`RunStartVariableCatalogAccess`를 추가해 run start 후보 pool을 DI-backed catalog 경계로 이동했다. 이후 72번에서 `RunStartVariableCatalogAccess`는 제거되고 selector가 직접 `IRunStartVariableCatalog`를 주입받는다.
- `IOwnerCandidateCatalog`/`ResourceOwnerCandidateCatalog`를 추가해 owner 후보 loading을 manager 밖 provider 경계로 이동했다.
- `RunStartVariableSelector`는 더 이상 `Resources.LoadAll`을 호출하지 않고 당시에는 `RunStartVariableCatalogAccess`에서 후보 pool을 받았다. 이후 72번에서 이 static access도 제거됐다.
- `OwnerRunManager.Awake`는 직렬화된 후보 정규화만 수행하고, 실제 후보 보충은 VContainer `ConstructOwnerRunManager` 이후 `IOwnerCandidateCatalog`를 통해 수행한다.
- `DungeonRuntimeLifetimeScope`에 owner candidate catalog와 run-start variable catalog를 등록했다.

효과:

- `RunStartVariableSelector.cs`와 `OwnerRunManager.cs`가 `ResourcesAccess` 후보에서 빠졌다.
- character asset은 `DataScriptableObject`가 아니므로 당시에는 `RunVariableRuntimeProvider.cs`의 provider 경계 안에만 `Resources.LoadAll<CharacterSO>`가 남았다. 이후 71번에서 이 책임은 `RunVariableCatalogServices.cs`로 이동했다.
- `script-coupling-audit.md` 기준 `ResourcesAccess`는 16 -> 15, `DependencyInjection`은 97 -> 98로 이동했다.
- 감사 요약은 296 script files, 6003 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after RunStartVariable catalog DI refactor:
- first pass caught CS0311 because CharacterSO is not DataScriptableObject
- fixed by moving CharacterSO loading to character-specific provider boundary
Unity MCP AssetDatabase.Refresh after run start and owner catalog DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.LoadAll remains in Assets/Scripts/Run/RunStartVariableSelector.cs
- no Resources.LoadAll remains in Assets/Scripts/Character/Core/OwnerRunManager.cs
```

### 62. Facility synthesis/evolution recipe catalog DI 경계 분리

대상:

- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordTokens.cs`
- `Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `FacilitySynthesisService.LoadAllRecipes`와 `FacilityEvolutionService.LoadAllRecipes`가 레시피 asset을 직접 `Resources.LoadAll`로 읽고 있었다.
- `FacilityEvolutionRuntime`은 recipe provider가 없으면 Resources-backed provider를 내부 생성했다.
- `DefaultFacilityEvolutionRecordTokenConsumer`도 token definition provider가 없으면 Resources-backed provider를 내부 생성해, 진화 기록 토큰 정책 로딩 책임이 consumer에 섞여 있었다.

변경:

- `DataCatalogFacilitySynthesisRecipeCatalog`, `DataCatalogFacilityEvolutionRecipeProvider`, `DataCatalogFacilityEvolutionRecordTokenDefinitionProvider`를 추가해 레시피/토큰 정의 로딩을 `IDataCatalog` 기반 VContainer 서비스로 이동했다.
- `FacilitySynthesisRecipeCatalogAccess`와 `FacilityEvolutionRecipeProviderAccess`를 추가해 기존 static `LoadAllRecipes` 호출 표면은 유지하되 로딩 책임은 DI 경계로 넘겼다.
- `FacilityEvolutionEngine`과 `FacilityEvolutionRuntime`은 recipe provider와 record-token consumer를 명시적으로 요구한다.
- `DefaultFacilityEvolutionRecordTokenConsumer`의 Resources fallback을 제거하고 token definition provider를 생성자 필수 의존성으로 바꿨다.
- `DungeonRuntimeLifetimeScope`에 synthesis/evolution recipe catalog, record token definition provider, record token consumer를 등록했다.

효과:

- `FacilitySynthesisSystem.cs`, `FacilityEvolutionService.cs`, `FacilityEvolutionRecordTokens.cs`, `FacilityEvolutionRuntime.cs`에서 recipe/token Resources loading 책임이 제거됐다.
- `script-coupling-audit.md` 기준 `ResourcesAccess`는 15 -> 12, `DependencyInjection`은 98 -> 99로 이동했다.
- 감사 요약은 297 script files, 6016 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after facility recipe catalog DI refactor: Passed
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Unity MCP catalog probe without scope reference: synthesisRecipes=10, evolutionRecipes=4, tokenDefinitions=9, tokenConsumer=True
Targeted direct coupling scan:
- no Resources.LoadAll remains in Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs
- no Resources.LoadAll remains in Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs
- no Resources.LoadAll remains in Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordTokens.cs
- no Resources-backed FacilityEvolution provider fallback remains
Targeted exact Resources.LoadAll scan in touched files:
- no Resources.LoadAll remains in synthesis/evolution recipe/token runtime files
Known remaining non-Editor Resources loads after this step:
- Assets/DataManager.cs central SO bootstrap
- Assets/Scripts/Codex/CodexReferenceImporter.cs codex import
- Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs character catalog provider boundary (later moved to RunVariableCatalogServices.cs in section 71)
- existing Resources.Load callers in InvasionIntruderDataResolver and TMPKoreanFont at this point
- AIBrain Resources.Load was moved to the action asset provider boundary in section 64
- InvasionIntruderDataResolver and TMPKoreanFont Resources.Load were later moved to provider boundaries in sections 65 and 66
```

### 63. Codex reference catalog Resources.LoadAll DI 경계 분리

대상:

- `Assets/Scripts/Codex/CodexReferenceImporter.cs`
- `Assets/Scripts/Infrastructure/CodexReferenceCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `CodexReferenceImporter.Import`가 코덱 seed 데이터를 만들기 위해 `CharacterSpeciesSO`와 `BuildingSO` asset을 직접 `Resources.LoadAll`로 훑고 있었다.
- 코덱 importer의 책임은 기준 데이터를 읽는 일이 아니라, 기준 데이터가 주어졌을 때 코덱 관찰 기록을 seed하는 일이어야 한다.

변경:

- `ICodexReferenceCatalog`/`DataCatalogCodexReferenceCatalog`/`CodexReferenceCatalogAccess`를 추가했다.
- `DataCatalogCodexReferenceCatalog`는 `IDataCatalog`에서 species/facility reference를 읽고 id순으로 정렬한다.
- `CodexReferenceImporter`는 더 이상 `Resources.LoadAll`을 호출하지 않고 DI-backed catalog access에서 reference data를 받는다.
- `DungeonRuntimeLifetimeScope`에 `DataCatalogCodexReferenceCatalog`를 `ICodexReferenceCatalog`로 등록했다.

효과:

- `CodexReferenceImporter.cs`가 `ResourcesAccess` 후보에서 빠지고 `DependencyInjection` 후보로 이동했다.
- `script-coupling-audit.md` 기준 `ResourcesAccess`는 12 -> 11, `DependencyInjection`은 99 -> 101로 이동했다.
- 감사 요약은 298 script files, 6027 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh after codex reference catalog DI refactor: Passed
Unity MCP codex reference catalog probe: species=3, facilities=35
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.LoadAll remains in Assets/Scripts/Codex/CodexReferenceImporter.cs
Known remaining non-Editor Resources loads after this step:
- Assets/DataManager.cs central SO bootstrap
- Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs character catalog provider boundary (later moved to RunVariableCatalogServices.cs in section 71)
- existing Resources.Load callers in InvasionIntruderDataResolver and TMPKoreanFont at this point
- AIBrain Resources.Load was moved to the action asset provider boundary in section 64
- InvasionIntruderDataResolver and TMPKoreanFont Resources.Load were later moved to provider boundaries in sections 65 and 66
```

### 64. AIBrain AI action asset catalog DI 경계 분리

대상:

- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Infrastructure/CharacterAiActionAssetCatalog.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `AIBrain`이 visitor/owner 기본 행동을 보강할 때 `AIActionSet` asset을 직접 `Resources.Load`로 읽고 있었다.
- 행동 선택/전환/예약/디버그 상태를 책임지는 brain이 asset 로딩 경로까지 알고 있어, 테스트와 DI 경계가 흐려졌다.
- `AIActionSet`은 `DataScriptableObject`가 아니므로 `IDataCatalog`로 억지 편입하지 않고, action asset 전용 provider 경계를 두는 쪽이 책임 분리가 맞다.

변경:

- `ICharacterAiActionAssetCatalog`와 `ResourceCharacterAiActionAssetCatalog`를 추가했다.
- `AIBrain`은 VContainer method injection으로 action catalog를 받고, 필수 action 보강 시 catalog만 호출한다.
- `ResourceCharacterAiActionAssetCatalog`가 asset 누락과 facility role 불일치를 즉시 예외로 보고한다.
- `DungeonRuntimeLifetimeScope`에 action asset catalog를 singleton으로 등록했다.

효과:

- `AIBrain.cs`에서 직접 `Resources.Load` 책임이 제거됐다.
- `ResourcesAccess` 후보는 `AIBrain.cs`에서 `CharacterAiActionAssetCatalog.cs` provider 경계로 이동했다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 101 -> 102, 감사 요약은 299 script files, 6007 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh and action catalog probe after AIBrain action asset catalog DI refactor: Passed
Unity MCP action catalog probe: requiredActions=9
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.Load remains in Assets/Scripts/Character/AI/AIBrain.cs
- Resources.Load remains only in Assets/Scripts/Infrastructure/CharacterAiActionAssetCatalog.cs for the intentional action asset provider boundary
Package manifest scan:
- jp.hadashikick.vcontainer is present in Packages/manifest.json and Packages/packages-lock.json
```

### 65. Invasion intruder data provider DI 경계 분리

대상:

- `Assets/Scripts/Invasion/InvasionIntruderDataResolver.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Infrastructure/InvasionIntruderDataProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `InvasionIntruderDataResolver.Resolve`가 기본 침입자 `CharacterSO`를 직접 `Resources.Load`로 읽고 있었다.
- `InvasionDirectorRuntime`은 이미 VContainer로 침입 context를 받는데, 침입자 데이터만 static resolver에 의존해 DI 경계가 어긋나 있었다.
- `CharacterSO`는 `DataScriptableObject`가 아니므로 `IDataCatalog`에 억지 편입하지 않고 침입자 데이터 전용 provider 경계를 두는 편이 맞다.

변경:

- `IInvasionIntruderDataProvider` 계약을 `InvasionIntruderDataResolver.cs`에 남기고, static Resources resolver를 제거했다.
- `ResourceInvasionIntruderDataProvider`를 추가해 기본 침입자 asset 로딩을 Infrastructure provider로 이동했다.
- 기본 침입자 asset이 없으면 null fallback 없이 `InvalidOperationException`을 던져 설정 오류를 명확히 드러낸다.
- `InvasionDirectorRuntime`이 `IInvasionIntruderContext`와 `IInvasionIntruderDataProvider`를 함께 주입받도록 바꿨다.
- `DungeonRuntimeLifetimeScope`에 `ResourceInvasionIntruderDataProvider`를 등록했다.

효과:

- `InvasionIntruderDataResolver.cs`에서 직접 `Resources.Load` 책임이 제거되고 DI contract만 남았다.
- `ResourcesAccess` 후보는 `InvasionIntruderDataResolver.cs`에서 `Infrastructure/InvasionIntruderDataProvider.cs` provider 경계로 이동했다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 102 -> 104, 감사 요약은 300 script files, 6009 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh and provider probe after invasion intruder data provider DI refactor: Passed
Unity MCP provider probe: id=2001, name=Intruder_Breakthrough
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.Load remains in Assets/Scripts/Invasion/InvasionIntruderDataResolver.cs
- no Resources.Load remains in Assets/Scripts/Invasion/InvasionIntruderSystem.cs
- Resources.Load remains only in Assets/Scripts/Infrastructure/InvasionIntruderDataProvider.cs for the intentional intruder data provider boundary
```

### 66. TMP Korean font provider DI 경계 분리

대상:

- `Assets/Scripts/UI/TMPKoreanFont.cs`
- `Assets/Scripts/UI/TmpKoreanFontSettingsSO.cs`
- `Assets/Scripts/Infrastructure/TmpKoreanFontProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Resources/Config/TMPKoreanFontSettings.asset`

진단:

- `TMPKoreanFont.Resolve`가 UI helper 안에서 직접 `Resources.Load<TMP_FontAsset>`를 호출하고, 없으면 `TMP_Settings.defaultFontAsset`로 조용히 대체했다.
- UI text 적용 유틸의 책임은 font를 text에 적용하는 것이고, asset 경로/로딩/누락 정책은 provider 경계에 있어야 한다.
- 기본 font fallback은 설정 오류를 숨길 수 있으므로, 필수 폰트 누락은 즉시 예외로 드러내는 편이 디버깅에 안전하다.

변경:

- `ITmpKoreanFontProvider` 계약을 추가하고 `TMPKoreanFont.Resolve`가 `DungeonRuntimeLifetimeScope`에서 provider를 resolve하게 했다.
- `TmpKoreanFontSettingsSO`와 `Assets/Resources/Config/TMPKoreanFontSettings.asset`을 추가해 실제 `Maplestory Light SDF` 폰트를 명시 참조하게 했다.
- `ResourceTmpKoreanFontProvider`를 추가해 설정 asset 로딩과 필수 폰트 검증을 Infrastructure provider로 이동했다.
- `TMP_Settings.defaultFontAsset` fallback을 제거하고, provider/settings/font asset 누락 시 `InvalidOperationException`을 던지게 했다.
- `DungeonRuntimeLifetimeScope`에 `ResourceTmpKoreanFontProvider`를 등록했다.
- 기존 `TMPKoreanFont.SetOverrideFont`는 Editor 초기화 주입 경로로 유지했다.

효과:

- `TMPKoreanFont.cs`에서 직접 `Resources.Load` 책임이 제거됐다.
- `ResourcesAccess` 후보는 `TMPKoreanFont.cs`에서 `Infrastructure/TmpKoreanFontProvider.cs` provider 경계로 이동했다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 104 -> 106, 감사 요약은 302 script files, 6015 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP first provider probe exposed hidden configuration issue:
- Resources/Font/Maplestory Light SDF did not exist and old TMP_Settings fallback had hidden the missing asset path
Unity MCP AssetDatabase.Refresh and settings provider probe after TMP Korean font provider DI refactor: Passed
Unity MCP provider probe: settings=TMPKoreanFontSettings, font=Maplestory Light SDF
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.Load remains in Assets/Scripts/UI/TMPKoreanFont.cs
- Resources.Load remains only in Assets/Scripts/Infrastructure/TmpKoreanFontProvider.cs for the intentional TMP Korean font settings provider boundary
```

### 67. DataManager DataScriptableObject source DI 경계 분리

대상:

- `Assets/DataManager.cs`
- `Assets/Scripts/Infrastructure/DataScriptableObjectSource.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `DataManager`가 생성자에서 `Resources.LoadAll<DataScriptableObject>("SO")`를 직접 호출하고 있었다.
- 데이터 인덱싱 책임과 asset 로딩 경로가 한 클래스에 섞여 있어 테스트 source를 주입하기 어렵고, Resources 의존도 중앙 서비스에 남아 있었다.
- `DataManager`는 데이터 구조화/조회만 담당하고, Resources 경로와 누락 정책은 Infrastructure provider 경계가 갖는 편이 맞다.

변경:

- `IDataScriptableObjectSource`와 `ResourceDataScriptableObjectSource`를 추가했다.
- `DataManager`는 `IDataScriptableObjectSource`를 생성자로 요구하고, 직접 `Resources.LoadAll`을 호출하지 않는다.
- `ResourceDataScriptableObjectSource`는 `Resources/SO`에 `DataScriptableObject`가 없으면 fallback 없이 `InvalidOperationException`을 던진다.
- `DataManager`의 중복 id 처리는 유지하되 깨진/mojibake 로그 문자열을 ASCII 경고 로그로 정리했다.
- `DungeonRuntimeLifetimeScope`에 `ResourceDataScriptableObjectSource`와 `DataManager` 생성자 주입 경계를 등록했다.

효과:

- `DataManager.cs`에서 직접 `Resources.LoadAll` 책임이 제거됐다.
- `ResourcesAccess` 후보는 `DataManager.cs`에서 `Infrastructure/DataScriptableObjectSource.cs` provider 경계로 이동했다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 106 -> 108, 감사 요약은 303 script files, 6019 extracted declarations 상태가 됐다.

검증:

```text
Unity MCP AssetDatabase.Refresh and DataManager source construction probe: Passed
Unity MCP provider probe: loadedAssets=89
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted direct coupling scan:
- no Resources.Load/Resources.LoadAll remains in Assets/DataManager.cs
- Resources.LoadAll remains only in Assets/Scripts/Infrastructure/DataScriptableObjectSource.cs for the intentional DataScriptableObject source provider boundary
```

### 68. 미사용 scene singleton 상속 제거

대상:

- `Assets/Scripts/Buildings/UI/UIBuildingInfo.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/UI/UIManager.cs`

진단:

- 세 클래스는 `UtilSingleton<T>`를 상속하고 있었지만, 런타임/Editor 코드에서 `UIBuildingInfo.Current`, `GameManager.Current`, `UIManager.Current` 또는 대응 `Instance` 접근이 발견되지 않았다.
- 현재 접근 경로는 VContainer scene injection, `DungeonSceneRuntimeReferences`, `GameManagerGameDataProvider`, `IUiPopupService`, `IUiTouchGuardService`로 이미 분리되어 있다.
- 단순히 `MonoBehaviour`로 내리면 `GameManager.numbers` dictionary나 `UIBuildingInfo.simpleInfoText` 같은 Odin 직렬화 가능성이 깨질 수 있으므로, singleton 상속만 제거하고 `SerializedMonoBehaviour`는 유지하는 편이 안전하다.

변경:

- `UIBuildingInfo`, `GameManager`, `UIManager`가 `UtilSingleton<T>` 대신 `SerializedMonoBehaviour`를 직접 상속하게 했다.
- `UIBuildingInfo.Awake`와 `GameManager.Awake`에서 `base.Awake()` 호출을 제거하고 일반 Unity lifecycle method로 정리했다.
- `GameManager`에는 `SerializedMonoBehaviour` 사용을 위해 `Sirenix.OdinInspector` using을 명시했다.

효과:

- 세 scene component가 더 이상 전역 singleton instance를 노출하지 않는다.
- 해당 객체들은 기존처럼 scene에 존재하고, 필요한 서비스는 VContainer/provider가 scene query로 받는다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 25 -> 22로 감소했다.

검증:

```text
Unity MCP AssetDatabase.Refresh and scene singleton inheritance probe: Passed
Unity MCP inheritance probe: GameManager=SerializedMonoBehaviour, UIManager=SerializedMonoBehaviour, UIBuildingInfo=SerializedMonoBehaviour
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted singleton usage scan:
- no UIBuildingInfo.Current/UIBuildingInfo.Instance usage found
- no GameManager.Current/GameManager.Instance usage found
- no UIManager.Current/UIManager.Instance usage found
Targeted UtilSingleton inheritance scan:
- remaining runtime UtilSingleton inheritance is OwnerRunManager plus the UtilSingleton base definition itself
```

### 69. OwnerRunManager singleton 상속 제거

대상:

- `Assets/Scripts/Character/Core/OwnerRunManager.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `OwnerRunManager`는 아직 `UtilSingleton<OwnerRunManager>`를 상속하고 있었지만, 현재 런타임 접근 경로는 `IOwnerRunManagerProvider`, `IOwnerRunDataProvider`, `IOwnerRunLifecycleService`, `InvasionIntruderContext`의 scene-query 경계로 이동해 있었다.
- `OwnerRunManager.Current`, `OwnerRunManager.Instance`, `OwnerRunManager.TryGetInstance` 직접 사용처는 없었다.
- owner 후보 배열과 Odin 기반 직렬화 가능성은 유지해야 하므로, scene singleton만 제거하고 `SerializedMonoBehaviour` 기반은 유지하는 편이 안전하다.

변경:

- `OwnerRunManager`가 `UtilSingleton<OwnerRunManager>` 대신 `SerializedMonoBehaviour`를 직접 상속하게 했다.
- `OwnerRunManager.Awake`에서 `base.Awake()` 호출을 제거하고 일반 Unity lifecycle method로 정리했다.
- 기존 VContainer method injection, owner spawn 후 `IObjectResolver.Inject`, owner 후보 catalog 보충 흐름은 유지했다.

효과:

- non-Editor runtime에서 `UtilSingleton<T>` 상속자는 더 이상 남지 않고, `UtilSingleton.cs`는 호환용 base 정의로만 남는다.
- owner 선택/런 종료/침입자 owner 조회/런 변수 owner 조회는 기존 provider/service 경계를 계속 사용한다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 22 -> 21로 감소했다.

검증:

```text
Unity MCP AssetDatabase.Refresh and OwnerRunManager inheritance probe: Passed
Unity MCP inheritance probe: OwnerRunManager=SerializedMonoBehaviour, IOwnerRunManagerProvider=True, IOwnerRunLifecycleService=True
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
Targeted singleton usage scan:
- no OwnerRunManager.Current/OwnerRunManager.Instance/OwnerRunManager.TryGetInstance usage found
Targeted UtilSingleton inheritance scan:
- only Assets/Scripts/Utils/UtilSingleton.cs defines UtilSingleton<T>; no non-Editor runtime class inherits it
```

### 70. CharacterAiScheduler scheduling service DI 경계 분리

대상:

- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`
- `Assets/Scripts/Infrastructure/CharacterAiSchedulingService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/Ability/AbilityMove.cs`
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`

진단:

- `AIBrain`, `CharacterActor`, `AbilityMove`, `AiDirectorRuntime`, `CharacterDialogueRuntime`, `CharacterFeedbackBubble`가 AI scheduler의 정적 facade에 직접 묶여 있었다.
- 등록/해제, 즉시 재계획, path-search budget, offscreen feedback throttling, movement frame stride는 scheduler 정책이지만, 각 도메인 컴포넌트가 scheduler singleton 위치를 알아야 하는 구조였다.
- Behavior Designer Editor 디버그 시나리오 호환은 필요하지만, 런타임 호출부가 같은 static entry에 의존할 필요는 없다.

변경:

- `ICharacterAiSchedulingService`와 `CharacterAiSchedulingService`를 추가해 scheduler 조회와 정책 호출을 VContainer service 경계로 이동했다.
- `CharacterAiScheduler`는 runtime static facade를 제거하고 `RegisterActor`, `UnregisterActor`, `RequestImmediateDecisionFor`, `TryConsumePathSearchBudget`, `ShouldShowCharacterFeedbackFor`, `GetMovementFrameStrideFor` 같은 instance API를 노출한다.
- 이 단계에서는 Editor debug scenario 호환을 위해 scheduler editor wrapper를 임시로 유지했다. 이후 88번에서 해당 wrapper까지 제거했다.
- `CharacterActor`, `AIBrain`, `AbilityMove`, `AiDirectorRuntime`, `CharacterDialogueRuntime`, `CharacterFeedbackBubble`는 `ICharacterAiSchedulingService`를 주입받아 scheduler 정책을 사용한다.
- `DungeonRuntimeLifetimeScope`에 scheduling service를 등록하고, scene injection 대상에 `CharacterFeedbackBubble`를 추가했다.

효과:

- non-Editor runtime에서 `CharacterAiScheduler.*` 직접 호출은 사라지고, scheduler 정책은 VContainer service를 통해 들어간다.
- `CharacterActor`는 injection 완료 전 `OnEnable`이 먼저 호출돼도 `Start`에서 필수 주입을 확인한 뒤 scheduler 등록을 보장한다.
- feedback bubble과 dialogue visibility throttling, movement stride, AI replan budget이 같은 scheduler policy를 공유하되 정적 singleton에는 결합하지 않는다.
- `script-coupling-audit.md` 기준 `SingletonAccess`는 21 -> 20, `DependencyInjection`은 108 -> 110, 감사 요약은 304 script files, 6041 extracted declarations 상태가 됐다.

검증:

```text
Targeted runtime scheduler static scan:
- no non-Editor runtime CharacterAiScheduler.* call sites remain
- remaining CharacterAiScheduler.* text in runtime is the profiler marker string only

Unity MCP AssetDatabase.Refresh and scheduling service probe: Passed
Unity MCP service probe:
- CharacterAiSchedulingService resolved the loaded CharacterAiScheduler
- ShouldShowCharacterFeedback(null)=False
- GetMovementFrameStride(null)=3
- TryConsumePathSearchBudget()=True

Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
```

### 71. RunVariable runtime provider와 catalog Resources 경계 분리

대상:

- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/RunVariableCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `RunVariableRuntimeProvider.cs`가 run-variable runtime 조회, owner run data/lifecycle, owner 후보 catalog, 시작 변수 catalog, character `Resources.LoadAll`까지 한 파일에 섞고 있었다.
- `RunVariableRuntimeProvider`라는 이름의 파일이 실제로는 runtime provider와 catalog provider를 함께 들고 있어, 런 상태 조회 책임과 asset catalog 로딩 책임의 경계가 흐렸다.
- character asset은 `DataScriptableObject` catalog로 바로 들어오지 않는 경로라 Resources provider 자체는 필요하지만, 그것을 run-variable runtime provider 파일에 둘 필요는 없다.

변경:

- `RunVariableCatalogServices.cs`를 추가하고 `IRunCharacterCatalog`, `IOwnerCandidateCatalog`, `IRunStartVariableCatalog`와 구현체를 이동했다.
- `ResourceRunCharacterCatalog`가 `Resources.LoadAll<CharacterSO>("SO/Character")`를 단일 provider 경계에서 캐시한다.
- `ResourceOwnerCandidateCatalog`와 `RunStartVariableCatalog`는 직접 Resources를 읽지 않고 `IRunCharacterCatalog`를 주입받아 character/owner 후보를 제공한다.
- `RunVariableRuntimeProvider.cs`는 run-variable runtime 조회, reader/access, owner run data/lifecycle만 남겼다.
- `DungeonRuntimeLifetimeScope`에 `ResourceRunCharacterCatalog`를 `IRunCharacterCatalog`로 등록했다.

효과:

- run-variable runtime 조회 파일에서 `ResourcesAccess` 책임이 제거됐다.
- character asset Resources 로딩은 `RunVariableCatalogServices.cs`의 provider 경계로 모였다.
- owner candidate catalog와 start variable catalog가 같은 character source를 공유한다.
- `script-coupling-audit.md` 기준 감사 요약은 305 script files, 6047 extracted declarations, `DependencyInjection`은 110 -> 111 상태가 됐다.

검증:

```text
Targeted Resources scan:
- no Resources.Load/LoadAll remains in RunVariableRuntimeProvider.cs
- character Resources.LoadAll remains only in RunVariableCatalogServices.cs for the intentional run character catalog provider boundary

Targeted registration scan:
- ResourceRunCharacterCatalog registered as IRunCharacterCatalog
- ResourceOwnerCandidateCatalog registered as IOwnerCandidateCatalog
- RunStartVariableCatalog registered as IRunStartVariableCatalog

Unity MCP AssetDatabase.Refresh and run-variable catalog split probe: Passed
Unity MCP catalog probe: characters=5, owners=3, buildings=35, blueprints=7
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
```

### 72. RunStartVariableSelector static access 제거 및 DI 서비스화

대상:

- `Assets/Scripts/Run/RunStartVariableSelector.cs`
- `Assets/Scripts/Run/RunVariableSystem.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `RunVariableRuntime.StartRun`이 `RunStartVariableSelector.Create` static method를 호출하고 있었다.
- `RunStartVariableSelector` 내부는 `RunStartVariableCatalogAccess`와 `MetaProgressionRuntimeAccess`를 통해 다시 static DI compatibility facade를 탔다.
- 시작 변수 선택은 런 시작 도메인 정책이므로, 런타임 컴포넌트가 명시적으로 selector service를 주입받고 selector가 catalog/meta reader를 직접 주입받는 편이 책임 위치가 더 명확하다.

변경:

- `IRunStartVariableSelector`를 추가하고 `RunStartVariableSelector`를 static class에서 VContainer service class로 변경했다.
- selector는 `IRunStartVariableCatalog`와 `IMetaProgressionRuntimeReader`를 생성자로 받는다.
- `RunVariableRuntime`은 `IRunStartVariableSelector`를 method injection으로 받아 `StartRun`에서 사용한다.
- 더 이상 사용되지 않는 `RunStartVariableCatalogAccess` static facade를 제거했다.
- `DungeonRuntimeLifetimeScope`에 `RunStartVariableSelector`를 `IRunStartVariableSelector`로 등록했다.

효과:

- run start 후보 선택 경로에서 static `RunStartVariableCatalogAccess`/`MetaProgressionRuntimeAccess` 우회가 사라졌다.
- `RunVariableRuntime`의 시작 변수 생성 의존성이 `IRunStartVariableSelector`로 드러난다.
- `RunVariableRuntimeProvider.cs`는 static catalog access까지 빠져 runtime reader/access와 owner lifecycle compatibility만 남는다.
- `script-coupling-audit.md` 기준 감사 요약은 305 script files, 6046 extracted declarations 상태가 됐다.

검증:

```text
Targeted static access scan:
- no RunStartVariableCatalogAccess references remain
- no RunStartVariableSelector.Create static call remains
- RunVariableRuntime depends on IRunStartVariableSelector
- RunStartVariableSelector depends on IRunStartVariableCatalog and IMetaProgressionRuntimeReader

Unity MCP AssetDatabase.Refresh and run-start selector DI probe: Passed
Unity MCP selector probe: facilities=3, guests=1, blueprints=2, runtimeConstructed=True
Unity Console: 0 errors, 5 existing Behavior Designer obsolete warnings
```

### 73. StaffDiscontent work command static access 제거

대상:

- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/Input/OwnerCommandController.cs`
- `Assets/Scripts/Character/Work/WorkCommandHandler.cs`
- `Assets/Scripts/Character/Work/WorkDutyController.cs`
- `Assets/Scripts/Character/Work/WorkPriorityProfile.cs`
- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`
- `Assets/Scripts/Infrastructure/StaffDiscontentRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `WorkDutyController`, `WorkCommandHandler`, `WorkCommandResolver`, `OwnerCommandController`가 staff discontent runtime을 static compatibility facade로 우회하거나, resolver 내부에서 반란 대상 판정을 직접 찾고 있었다.
- 작업 시작 차단, 반란 제압 대상 판정, 제압 완료 처리는 작업 시스템의 정책이지만 실제 상태 source는 staff discontent runtime이므로, 의존성을 명시적으로 주입받아 전달해야 책임 경계가 보인다.
- `StaffDiscontentRuntimeAccess`는 더 이상 non-Editor runtime 호출부가 없어서 남겨둘 이유가 없었다.

변경:

- `AbilityWork`가 `IStaffDiscontentRuntimeService`를 VContainer로 받고, 하위 `WorkDutyController`/`WorkCommandHandler`는 `AbilityWork`를 통해 같은 service를 사용한다.
- `OwnerCommandController`도 `IStaffDiscontentRuntimeService`를 주입받고, `DungeonRuntimeLifetimeScope`가 씬의 `OwnerCommandController`에 injection을 수행한다.
- `WorkCommandResolver`는 staff discontent runtime을 직접 resolve하지 않고, `Predicate<CharacterActor>` 형태의 반란 대상 판정 함수를 인자로 받는다.
- `StaffDiscontentRuntime.DispatchAutoSuppress`는 자기 자신의 `IsRebellionTarget`을 전달하고, unused `StaffDiscontentRuntimeAccess` static facade는 삭제했다.
- 컴파일 호환을 위해 Editor debug scenario의 기존 resolver 호출부는 새 predicate 인자를 넘기도록 최소 수정했다. Editor scenario 자체 리팩토링은 범위 밖으로 남겼다.

효과:

- staff discontent work-block/suppression 경로에서 non-Editor runtime `StaffDiscontentRuntimeAccess` 참조가 사라졌다.
- 작업 명령 UI, 작업 controller, 반란 자동 제압이 모두 같은 명시적 service/predicate 경계를 공유한다.
- `script-coupling-audit.md` 기준 `DependencyInjection`은 111 -> 112, 감사 요약은 305 script files, 6041 extracted declarations 상태가 됐다.

검증:

```text
Targeted static access scan:
- no StaffDiscontentRuntimeAccess references remain in non-Editor runtime
- no RunStartVariableCatalogAccess or RunStartVariableSelector.Create regressions found
- all WorkCommandResolver suppress checks now pass a rebellion-target predicate

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP StaffDiscontent DI signature probe: Passed
- TryResolveSuppressCommand(null, null, _ => false, out reason) compiled against the new signature
- IsSuppressTarget(null, _ => false) compiled against the new signature
Unity Console after probe: 0 errors, 0 warnings
```

### 74. BlueprintResearch work static access 제거

대상:

- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`
- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`

진단:

- `BuildableObject.GetWorkUrgency`가 연구 작업 여부를 판단할 때 `BlueprintResearchWorkAccess` static facade를 통해 runtime service를 우회하고 있었다.
- `BlueprintResearchRuntime.HasResearchWorkFor`도 호환 API로 남아 있어 연구 작업 판정의 진짜 소유자가 runtime인지 service인지 흐려졌다.
- 합성/진화/그리드 배치로 동적 생성되는 `BuildableObject`는 씬 주입만으로는 새 DI 필드를 받을 수 없으므로, 생성 직후 명시 injection 경로가 필요했다.

변경:

- `BuildableObject`가 `IBlueprintResearchWorkService`를 VContainer로 직접 받고, 연구 작업 urgency 계산은 주입된 service만 사용한다.
- `BlueprintResearchWorkAccess` static facade와 `BlueprintResearchRuntime.HasResearchWorkFor` 호환 API를 제거했다.
- `DungeonStoryGridBuildingController`, `FacilitySynthesisSystem`, `FacilityEvolutionRuntime`이 생성/배치한 `BuildableObject`에 `IObjectResolver.Inject`를 수행한다.
- `DungeonRuntimeLifetimeScope`가 씬에 이미 존재하는 `BuildableObject`도 scope 구성 시 주입한다.

효과:

- non-Editor runtime에서 `BlueprintResearchWorkAccess` 참조가 사라졌다.
- 연구 작업 가능 여부의 책임이 `IBlueprintResearchWorkService`로 고정되어 `BuildableObject`의 숨은 service lookup이 없어졌다.
- 씬 배치 건물과 런타임 생성 건물이 같은 DI 경로를 거쳐 연구 작업 urgency 계산을 수행한다.

검증:

```text
Targeted static access scan:
- no BlueprintResearchWorkAccess references remain in Assets/Scripts runtime code
- no BlueprintResearchRuntime.HasResearchWorkFor references remain
- no public static HasResearchWorkFor compatibility API remains

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP BlueprintResearch DI signature probe: Passed
- BuildableObject.ConstructBuildableObject exists
- BlueprintResearchWorkAccess type lookup returns false
- IBlueprintResearchWorkService.HasResearchWorkFor exists
Unity Console after refresh: 0 errors, 5 existing Behavior Designer obsolete warnings
git diff --check: Passed with LF-to-CRLF warnings only
```

### 75. OffenseReward selector/catalog static access 제거

대상:

- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Offense/OffenseRewardSelector.cs`
- `Assets/Scripts/Offense/OffenseRewardSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`

진단:

- `OffenseRewardSelector`가 static class로 남아 있었고, 희귀 시설/설계도 후보를 고를 때 `OffenseRewardCatalogAccess` static facade를 통해 catalog를 우회했다.
- 보상 지급 로직도 `OffenseRewardService` static class라서 reward runtime이 실제 지급 정책과 후보 선택 정책을 명시적으로 주입받지 않았다.
- 보상 후보 선택은 catalog 의존성이 있는 정책이고, 보상 지급은 결과 적용/상태 기록 책임이므로 둘을 서비스 단위로 분리해야 테스트와 튜닝 경계가 보인다.

변경:

- `IOffenseRewardSelector`와 `IOffenseRewardGrantService`를 추가했다.
- `OffenseRewardSelector`를 catalog 주입을 받는 instance service로 전환하고 `OffenseRewardCatalogAccess` static facade를 제거했다.
- `OffenseRewardService`를 `OffenseRewardGrantService`로 전환하고 selector를 생성자 주입받게 했다.
- `OffenseRewardRuntime`은 `IOffenseRewardContextBuilder`와 `IOffenseRewardGrantService`를 VContainer로 받는다.
- `DungeonRuntimeLifetimeScope`에 `OffenseRewardSelector`와 `OffenseRewardGrantService`를 등록했다.
- Editor debug scenario는 리팩터링 대상에서 제외하되, 컴파일 호환을 위해 새 grant service를 직접 구성하는 최소 adapter만 추가했다.

효과:

- non-Editor runtime에서 `OffenseRewardCatalogAccess` 호출이 사라졌다.
- 보상 후보 선택과 보상 지급이 명확히 분리되어, catalog 교체/테스트/튜닝이 static global state에 묶이지 않는다.
- 원정 보상 적용 경로가 VContainer composition root에서 추적 가능한 DI 체인으로 정리됐다.

검증:

```text
Targeted static access scan:
- no OffenseRewardCatalogAccess references remain in Assets/Scripts runtime code
- no OffenseRewardService references remain
- no OffenseRewardSelector static calls remain
- remaining non-Editor Access scan no longer includes OffenseRewardCatalogAccess

git diff --check: Passed with LF-to-CRLF warnings only
Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
```

### 76. Codex reference catalog static access 제거

대상:

- `Assets/Scripts/Codex/CodexReferenceImporter.cs`
- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Infrastructure/CodexReferenceCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- `CodexReferenceImporter`가 static class로 남아 있었고, species/facility reference import 시 `CodexReferenceCatalogAccess` static facade를 직접 호출했다.
- Codex reference import는 catalog 데이터 source를 읽는 책임이라 `CodexRuntime`의 import flow에서 명시적으로 주입되는 서비스가 더 맞다.
- static importer/facade 조합은 테스트 시 catalog 교체가 어렵고, Codex 초기 import가 composition root에서 추적되지 않는다.

변경:

- `ICodexReferenceImporter`를 추가하고 `CodexReferenceImporter`를 catalog 주입을 받는 instance service로 전환했다.
- `CodexReferenceCatalogAccess` static facade를 제거했다.
- `CodexRuntime`이 `IBlueprintResearchStateService`와 함께 `ICodexReferenceImporter`를 VContainer로 받게 했다.
- `CodexService.ImportReferenceData`는 importer를 인자로 받아 domain helper 역할만 하도록 바꿨다.
- `DungeonRuntimeLifetimeScope`에 `CodexReferenceImporter`를 등록했다.

효과:

- non-Editor runtime에서 `CodexReferenceCatalogAccess` 호출이 사라졌다.
- Codex reference import 경로가 `ICodexReferenceCatalog -> ICodexReferenceImporter -> CodexRuntime` DI 체인으로 읽힌다.
- reference import와 observation/recording helper 책임이 분리되어 catalog source 교체가 쉬워졌다.

검증:

```text
Targeted static access scan:
- no CodexReferenceCatalogAccess references remain in Assets/Scripts runtime code
- no CodexReferenceImporter.Import static call remains
- CodexRuntime ConstructCodexRuntime now receives ICodexReferenceImporter

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
```

### 77. CharacterStats owner health meta progression static access 제거

대상:

- `Assets/Scripts/Character/Core/CharacterStats.cs`

진단:

- `CharacterStats.RecalculateVitals`가 owner max health 보정을 계산할 때 `MetaProgressionRuntimeAccess.GetOwnerMaxHealthMultiplier()` static facade를 직접 호출했다.
- `CharacterStats`는 이미 staff discontent와 owner lifecycle service를 VContainer로 받는 MonoBehaviour라, meta progression reader도 같은 injection 경계에 두는 것이 자연스럽다.
- 캐릭터 스탯 계산이 static runtime access에 묶이면 owner 캐릭터 테스트와 메타 진행도 교체가 어려워진다.

변경:

- `CharacterStats.ConstructCharacterStats`에 `IMetaProgressionRuntimeReader` 인자를 추가했다.
- owner max health 보정은 `ResolveMetaProgressionRuntimeReader().GetOwnerMaxHealthMultiplier()`로 읽는다.
- static facade는 유지했지만 `CharacterStats` 호출부에서는 제거했다.

효과:

- `CharacterStats`에서 `MetaProgressionRuntimeAccess.GetOwnerMaxHealthMultiplier()` 호출이 사라졌다.
- 캐릭터 스탯 계산 의존성이 `IStaffDiscontentRuntimeService`, `IOwnerRunLifecycleService`, `IMetaProgressionRuntimeReader`로 명시된다.
- 남은 meta progression static access는 synthesis/evolution recipe 보존 조건 쪽으로 좁혀졌다.

검증:

```text
Targeted static access scan:
- no MetaProgressionRuntimeAccess.GetOwnerMaxHealthMultiplier call remains in CharacterStats
- remaining MetaProgressionRuntimeAccess calls are FacilitySynthesis/FacilityEvolution recipe preservation checks

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
```

### 78. Facility synthesis/evolution recipe query DI 분리

대상:

- `Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Codex/CodexRecipeRecorder.cs`
- `Assets/Scripts/Codex/CodexReferenceImporter.cs`
- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- synthesis/evolution 레시피는 이전 단계에서 catalog loading을 DI로 옮겼지만, 공개 여부와 snapshot 변환은 여전히 static access/facade 경계에 걸쳐 있었다.
- `MetaProgressionRuntimeAccess.IsRecipePreserved`가 레시피 공개 조건 내부에서 직접 호출되면, 연구 상태와 영구 보존 상태를 합친 조회 정책을 테스트/교체하기 어렵다.
- Codex import/recording도 합성식 공개 여부를 알아야 하므로, 단순 catalog보다 “현재 연구/메타 상태를 반영한 query service”가 더 적절한 책임 단위다.

변경:

- `IFacilitySynthesisRecipeQuery`와 `IFacilityEvolutionRecipeQuery`를 추가했다.
- `FacilitySynthesisRecipeQuery`와 `FacilityEvolutionRecipeQuery`가 catalog/provider와 `IMetaProgressionRuntimeReader`를 주입받아 공개 여부, visible list, snapshot/source candidate 계산을 담당한다.
- `FacilitySynthesisRecipeCatalogAccess`와 `FacilityEvolutionRecipeProviderAccess` static facade를 제거했다.
- `FacilitySynthesisRuntime`, `FacilityEvolutionRuntime`, `FacilityEvolutionEngine`, `CodexRuntime`, `CodexReferenceImporter`, `CodexRecipeRecorder`가 query service를 명시 인자로 받도록 바꿨다.
- `DungeonRuntimeLifetimeScope`에 synthesis/evolution recipe query 서비스를 singleton으로 등록했다.
- Editor debug scenario는 리팩터링 대상에서 제외하되, 새 생성자/인터페이스 계약에 맞는 최소 adapter만 추가했다.

효과:

- non-Editor runtime에서 synthesis/evolution recipe catalog access static facade가 사라졌다.
- recipe 공개 조건은 `catalog/provider -> query service -> runtime/codex` 체인으로 추적 가능해졌다.
- meta progression 보존 조건도 static access가 아니라 `IMetaProgressionRuntimeReader` 주입으로 전달된다.
- 합성 UI는 runtime의 `ToSnapshot`을 통해 query 결과를 표시하므로, UI가 recipe visibility/snapshot 정책을 직접 알 필요가 없다.

검증:

```text
Targeted static access scan:
- no FacilitySynthesisRecipeCatalogAccess references remain in Assets/Scripts
- no FacilityEvolutionRecipeProviderAccess references remain in Assets/Scripts
- no MetaProgressionRuntimeAccess.IsRecipePreserved direct calls remain in Assets/Scripts
- no FacilitySynthesisService.LoadAllRecipes / FacilityEvolutionService.LoadAllRecipes calls remain

Runtime static access scan:
- remaining non-Editor Access calls are FacilityShop/RunVariable/GameData/ShopStock/runtime provider/SocialReputation compatibility paths

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
```

### 79. Facility shop catalog/run-variable static access 제거

대상:

- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs`
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`
- `Assets/Scripts/Codex/CodexRecipeRecorder.cs`
- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

진단:

- 시설 상점 오퍼 생성이 catalog, run seed, run 가격 보정, meta basic-purchase 확장을 정적 access 경로로 끌어왔다.
- 연구 완료와 Codex 기록 경로가 `FacilityShopService.FindBuildingById`를 통해 암묵적으로 facility shop catalog를 찾으면, blueprint unlock 검증 책임과 catalog 공급 책임이 섞인다.
- `DailyFacilityShopRuntime`은 런타임 상태 owner인데도 오퍼 계산에 필요한 외부 catalog/reader 의존성이 코드상 드러나지 않았다.

변경:

- `FacilityShopService`의 daily/basic purchase 오퍼 생성은 `IFacilityShopCatalog`, `IRunVariableRuntimeReader`, `IMetaProgressionRuntimeReader`, 가격 보정 delegate를 명시 인자로 받도록 바꿨다.
- `DailyFacilityShopRuntime`은 VContainer `[Inject]`로 facility shop catalog, run-variable reader, meta-progression reader를 받는다.
- `OperatingDaySettlement`는 다음 날 상점 미리보기 생성을 위해 facility shop catalog와 run-variable reader를 주입받는다.
- `BlueprintResearchRuntime`과 `CodexRuntime`은 blueprint unlock building lookup을 위해 `IFacilityShopCatalog`를 주입받고, `CodexRecipeRecorder`에도 명시 인자로 전달한다.
- `FacilityShopCatalogAccess` static facade를 제거했다.
- Editor debug scenario는 리팩터링 대상에서 제외하되, 바뀐 signature에 맞는 최소 adapter만 추가했다.

효과:

- facility shop catalog/run seed/run 가격 보정/meta basic-purchase 확장 경로가 정적 access 없이 추적 가능해졌다.
- 상점 오퍼 계산은 입력 catalog와 가격 정책 delegate만 바꾸면 순수 계산처럼 검증할 수 있다.
- blueprint research와 Codex 기록은 암묵적 catalog lookup 없이 같은 `IFacilityShopCatalog` 경계를 공유한다.
- `DailyFacilityShopRuntime`은 상태 보관/구매 처리 owner로 남고, 데이터 공급과 가격 보정 정책은 composition root에서 드러난다.

검증:

```text
Targeted static access scan:
- no FacilityShopCatalogAccess references remain in Assets/Scripts
- no RunVariableRuntimeAccess.GetInitialShopSeed / GetFacilityShopCostMultiplier / GetBlueprintCostMultiplier calls remain in Assets/Scripts
- no MetaProgressionRuntimeAccess.GetExpandedBasicPurchaseBuildingIds calls remain in Assets/Scripts
- no legacy one-argument FacilityShopService.CreateDailyOffers / CreateBasicPurchaseOffers / FindBuildingById calls remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 80. Stock delivery run-variable static access 제거

대상:

- `Assets/Scripts/Buildings/SO/StockInfo.cs`
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`

진단:

- `StockSupplyService.CreateDailyDeliveryOffers`가 재고 납품 가격을 계산하면서 `RunVariableRuntimeAccess.GetStockCostMultiplier`를 직접 읽었다.
- 정산 리포트는 이미 `IRunVariableRuntimeReader`를 주입받고 있었기 때문에, stock delivery 가격만 정적 access를 우회로처럼 남겨둘 이유가 없었다.
- `RunVariableRuntimeAccess` 전체가 더 이상 호출되지 않는데 static facade가 남아 있으면 새 코드가 다시 우회로를 사용할 가능성이 있다.

변경:

- `StockSupplyService.CreateDailyDeliveryOffers`는 `IRunVariableRuntimeReader` 또는 `Func<StockCategory, float>`를 명시 인자로 받도록 바꿨다.
- `OperatingDaySettlementRuntime`은 주입된 run-variable reader를 stock delivery offer 생성에도 전달한다.
- run-variable editor scenario는 테스트 runtime의 `GetStockCostMultiplier`를 직접 넘겨 비용 변화 검증을 유지한다.
- facility editor scenario는 고정 1배 multiplier helper를 넘기는 compile adapter만 추가했다.
- `RunVariableRuntimeAccess` static facade를 삭제했다.

효과:

- `RunVariableRuntimeAccess`는 `Assets/Scripts`에서 완전히 제거됐다.
- stock delivery 가격 계산은 호출자가 넘긴 reader/delegate만 사용하므로 테스트와 런타임 정책 주입이 분리된다.
- run-variable provider 파일은 provider/reader/owner lifecycle 경계만 남고, 런 변수 읽기 우회 static API는 사라졌다.

검증:

```text
Targeted static access scan:
- no RunVariableRuntimeAccess references remain in Assets/Scripts
- no one-argument StockSupplyService.CreateDailyDeliveryOffers call remains in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 81. Shop/GameData/stock catalog static access 제거

대상:

- `Assets/Scripts/Buildings/Shop.cs`
- `Assets/Scripts/Buildings/SO/Conditions/IBuildingCondition.cs`
- `Assets/Scripts/Buildings/SO/Conditions/ConditionNeedMoney.cs`
- `Assets/Scripts/Buildings/SO/Conditions/ConditionNeedToConnect.cs`
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`
- `Assets/Scripts/Infrastructure/GameRuntimeServices.cs`
- `Assets/Scripts/Infrastructure/ShopStockCatalogService.cs`

진단:

- `Shop`이 재고 초기화, 매출 정산, 수익 숫자 표시, 판매 아이템 카테고리 조회를 `GameDataAccess`, `ShopStockCatalogAccess`, `FloatingNumberFeedbackAccess` static facade에 의존했다.
- `ConditionNeedMoney`는 ScriptableObject 조건 객체라 직접 DI 대상이 되기 어렵지만, 전역 access를 호출하면 배치 검증 로직이 런타임 singleton 상태에 묶인다.
- building placement 조건에는 명시적인 실행 컨텍스트가 없어서 검증 시점과 성공 적용 시점의 데이터 출처가 코드에 드러나지 않았다.

변경:

- `Shop`에 VContainer `[Inject]` `ConstructShop(IGameDataProvider, IShopStockCatalog, IFloatingNumberFeedbackService)`를 추가했다.
- 동적 배치 순서를 고려해 `Shop.Initialization`은 주입 전이면 대기하고, 주입 후 `TryInitializeStock`으로 재고 데이터를 초기화하도록 분리했다.
- `Shop`의 재고/매출/수익 피드백/판매 카테고리 조회는 주입된 provider/service/catalog만 사용한다.
- `BuildingConditionContext`를 추가하고 `IBuildingCondition.IsSatisfy`/`OnBuild`가 context를 받도록 바꿨다.
- `DungeonStoryGridBuildingController`가 VContainer로 `IGameDataProvider`를 받아 `BuildingConditionContext`를 생성하고, `BuildingPlacementValidator`가 조건 검증/성공 적용에 전달한다.
- `GameDataAccess`, `FloatingNumberFeedbackAccess`, `ShopStockCatalogAccess` static facade를 제거했다.

효과:

- `Assets/Scripts`에서 `GameDataAccess`, `FloatingNumberFeedbackAccess`, `ShopStockCatalogAccess` 참조가 0건이 됐다.
- `Shop`은 scene/dynamic injection 경계에서 필요한 서비스를 직접 받으므로, 판매/재고 로직의 외부 의존성이 필드와 생성 경로에 드러난다.
- building condition은 전역 상태를 직접 resolve하지 않고 validator가 제공한 context만 읽는다.

검증:

```text
Targeted static access scan:
- no GameDataAccess references remain in Assets/Scripts
- no FloatingNumberFeedbackAccess references remain in Assets/Scripts
- no ShopStockCatalogAccess references remain in Assets/Scripts
- no legacy IBuildingCondition.IsSatisfy(..., out string) / OnBuild() calls remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 82. Social reputation facility scoring static access 제거

대상:

- `Assets/Scripts/Character/AI/FacilityScoringContext.cs`
- `Assets/Scripts/Character/AI/FacilityCandidateScorer.cs`
- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/AI/Action/AIEat.cs`
- `Assets/Scripts/Character/AI/Action/AIRest.cs`
- `Assets/Scripts/Character/AI/Action/AIShopping.cs`
- `Assets/Scripts/Character/AI/Action/AIFacilityRoleAction.cs`
- `Assets/Scripts/Infrastructure/SocialReputationRuntimeProvider.cs`

진단:

- `FacilityCandidateScorer`가 시설 후보 점수를 계산하면서 `SocialReputationBiasAccess` static facade를 직접 호출했다.
- AI 행동 ScriptableObject는 직접 DI 대상이 아니므로, 액션 자산 안에서 컨테이너를 다시 찾게 두면 평판/시설 점수 결합이 숨은 전역 의존성으로 남는다.
- social reputation bias는 이미 `ISocialReputationBiasService`로 등록되어 있으므로, `AIBrain`이 받은 서비스를 점수 계산 컨텍스트로 넘기는 편이 의존 방향이 더 분명하다.

변경:

- `FacilityScoringContext`를 추가해 시설 점수 계산에 필요한 `ISocialReputationBiasService` 의존성을 명시 값으로 전달한다.
- `AIBrain.ConstructAIBrain`이 `ISocialReputationBiasService`를 VContainer로 받고 `RequireFacilityScoringContext`를 제공한다.
- `FacilityCandidateScorer.SelectBest`, `TrySelectBest`, `ScoreCandidate`가 `FacilityScoringContext`를 필수 인자로 받도록 바꿨다.
- Eat/Rest/Shopping/FacilityRole 액션은 `actor.Brain.RequireFacilityScoringContext()`를 통해 평판 bias가 포함된 scoring context를 넘긴다.
- social reputation editor probe는 실제 runtime provider를 명시 생성해 평판 점수 검증을 유지하고, 단순 점수 단위 editor scenario만 의도적으로 reputation bias를 제외한다.
- `SocialReputationBiasAccess` static facade를 삭제했다.

효과:

- 시설 후보 점수 계산은 더 이상 컨테이너나 runtime singleton을 직접 resolve하지 않는다.
- 평판/소문이 시설 선택에 영향을 주는 경로가 `AIBrain -> FacilityScoringContext -> FacilityCandidateScorer`로 드러난다.
- editor test opt-out도 `WithoutReputationBiasForIsolatedTest()`라는 명시 호출로만 가능해서 런타임 fallback이 되지 않는다.

검증:

```text
Targeted static access scan:
- no SocialReputationBiasAccess references remain in Assets/Scripts
- no FacilityCandidateScorer.SelectBest / TrySelectBest / ScoreCandidate call remains without FacilityScoringContext

Package state scan:
- VContainer is present in Packages/manifest.json and Packages/packages-lock.json as jp.hadashikick.vcontainer#1.19.0

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 83. Owner run lifecycle unused static access 제거

대상:

- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`

진단:

- `OwnerRunLifecycleAccess`는 `IOwnerRunLifecycleService`를 VContainer에서 다시 resolve하는 static facade였지만, `Assets/Scripts` 안에 실제 호출자가 없었다.
- owner death 처리의 실사용 경로는 이미 `CharacterStats`가 주입받은 `IOwnerRunLifecycleService`를 통해 `OwnerRunManager.HandleOwnerDeath`로 들어간다.
- 호출자가 없는 static facade를 남기면 새 코드가 다시 전역 resolve 우회로를 선택할 수 있다.

변경:

- `OwnerRunLifecycleAccess` static class를 삭제했다.
- `IOwnerRunLifecycleService`와 `OwnerRunLifecycleService`는 유지해 owner death 처리의 DI 경계를 보존했다.

효과:

- owner run lifecycle 처리는 더 이상 static access facade를 제공하지 않는다.
- `RunVariableRuntimeProvider.cs`는 run-variable runtime provider/reader, owner run data provider, owner lifecycle service만 남는다.

검증:

```text
Targeted static access scan:
- no OwnerRunLifecycleAccess references remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed
```

### 84. Blueprint research state unused static access 제거

대상:

- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`

진단:

- `BlueprintResearchStateAccess`는 `IBlueprintResearchStateService`를 VContainer에서 다시 resolve하는 static facade였지만, `Assets/Scripts` 안에 실제 호출자가 없었다.
- codex/synthesis/evolution/research runtime은 이미 `IBlueprintResearchStateService`를 직접 주입받거나 명시 인자로 research state를 전달하고 있었다.
- 호출자가 없는 facade를 남기면 research state 조회가 다시 전역 resolve 우회로로 돌아갈 여지가 있다.

변경:

- `BlueprintResearchStateAccess` static class를 삭제했다.
- `IBlueprintResearchStateService`와 `BlueprintResearchStateService`는 유지해 research state 조회의 DI 경계를 보존했다.

효과:

- blueprint research state 조회는 더 이상 static access facade를 제공하지 않는다.
- `BlueprintResearchRuntimeProvider.cs`는 runtime provider, research work service, research state service만 남는다.

검증:

```text
Targeted static access scan:
- no BlueprintResearchStateAccess references remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed
```

### 85. Meta progression runtime unused static access 제거

대상:

- `Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs`

진단:

- `MetaProgressionRuntimeAccess`는 `IMetaProgressionRuntimeReader`를 VContainer에서 다시 resolve하는 static facade였지만, 현재 `Assets/Scripts`의 실사용 경로는 이미 `IMetaProgressionRuntimeReader` 주입으로 이동해 있었다.
- shop, synthesis, evolution, invasion threat, character stats, run-start selection, recipe catalog service 모두 명시 reader를 받는다.
- 호출자가 없는 static facade를 남기면 메타 강화 효과 조회가 다시 전역 resolve 우회로로 돌아갈 수 있다.

변경:

- `MetaProgressionRuntimeAccess` static class를 삭제했다.
- `IMetaProgressionRuntimeProvider`, `IMetaProgressionRuntimeReader`, `MetaProgressionRuntimeReader`는 유지해 메타 효과 조회의 DI 경계를 보존했다.

효과:

- meta progression effect 조회는 더 이상 static access facade를 제공하지 않는다.
- `MetaProgressionRuntimeProvider.cs`는 provider/reader 계약과 구현만 남는다.

검증:

```text
Targeted static access scan:
- no MetaProgressionRuntimeAccess references remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed
```

### 86. Runtime panel provider static access 제거

대상:

- `Assets/Scripts/Infrastructure/RuntimePanelProviders.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Codex/CodexSystem.cs`

진단:

- `FacilitySynthesisRuntimeAccess`, `FacilityEvolutionRuntimeAccess`, `CodexRuntimeAccess`는 VContainer provider를 다시 static으로 resolve하는 compatibility facade였다.
- 세 runtime의 `public static Instance` property가 이 facade를 참조했지만, 현재 `Assets/Scripts`의 런타임/패널 호출부는 이미 provider 주입 또는 explicit `Bind(runtime)`를 사용한다.
- 미사용 compatibility API가 남아 있으면 새 UI나 런타임 코드가 다시 `Runtime.Instance` 경로를 선택할 수 있다.

변경:

- `FacilitySynthesisRuntime.Instance`, `FacilityEvolutionRuntime.Instance`, `CodexRuntime.Instance` property를 삭제했다.
- `FacilitySynthesisRuntimeAccess`, `FacilityEvolutionRuntimeAccess`, `CodexRuntimeAccess` static classes를 삭제했다.
- `RuntimePanelProviders.cs`는 세 panel/runtime provider 구현만 남겼다.

효과:

- `Assets/Scripts` 기준 `public static class *Access`, `*Access.`, 세 runtime `Instance` compatibility property 검색 결과가 0건이 됐다.
- synthesis/evolution/codex UI는 explicit binding 또는 VContainer provider 주입 경로만 사용한다.

검증:

```text
Targeted static access scan:
- no public static class *Access remains in Assets/Scripts
- no *Access. references remain in Assets/Scripts
- no FacilitySynthesisRuntime.Instance / FacilityEvolutionRuntime.Instance / CodexRuntime.Instance references remain in Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 87. Unused UtilSingleton base 제거

대상:

- `Assets/Scripts/Utils/UtilSingleton.cs`
- `Assets/Scripts/Utils/UtilSingleton.cs.meta`
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`

진단:

- non-Editor runtime 기준 `UtilSingleton<T>` 상속자와 `Instance`/`HasInstance`/`TryGetInstance` 호출자가 0건이었다.
- `UtilSingleton.cs`는 자동 scene lookup fallback은 이미 제거됐지만, static holder base로 남아 있어 DI 전환 방향과 맞지 않았다.
- 실제 참조는 Editor debug scenario 한 곳이 `UtilSingleton<OwnerRunManager>._instance`를 reflection으로 만지는 compile-time 의존뿐이었다.

변경:

- `UtilSingleton.cs`와 meta 파일을 삭제했다.
- `InvasionIntruderDebugScenarios`에서 더 이상 존재하지 않는 owner singleton field 조작을 제거했다.

효과:

- non-Editor runtime에서 generic singleton base와 대응 static API가 사라졌다.
- owner/grid/invasion 테스트 경로는 더 이상 `UtilSingleton<OwnerRunManager>` reflection에 의존하지 않는다.

검증:

```text
Targeted runtime singleton scan:
- no UtilSingleton, .Instance, HasInstance, or TryGetInstance remains in non-Editor Assets/Scripts

Unity MCP AssetDatabase.Refresh: Passed
Unity MCP Console error scan: 0 errors
Targeted git diff --check for touched code/docs: Passed, with existing CRLF normalization warnings only
```

### 88. CharacterAiScheduler editor static scene lookup 제거

대상:

- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterAiScheduler`의 남은 `FindObjectsByType<CharacterAiScheduler>`는 `#if UNITY_EDITOR` 디버그 wrapper 안에만 있었지만, 런타임 스케줄러 파일이 editor-only static scene lookup까지 들고 있으면 DI 전환 감사에서 계속 혼선을 만든다.
- 실제 런타임 경로는 이미 `ICharacterAiSchedulingService`와 `IDungeonSceneComponentQuery`로 들어가므로, 스케줄러 본체는 instance API만 유지하는 편이 책임이 선명하다.

변경:

- `CharacterAiScheduler.Register` / `CharacterAiScheduler.ResetPathSearchBudgetForDebug` / `TryFindEditorScheduler` editor static wrapper를 제거했다.
- `CharacterAiPlanDebugScenarios`는 로컬 `ResetSchedulerPathBudgetForDebug()` 헬퍼에서 VContainer scheduling service를 우선 사용하고, 없을 때만 `DungeonSceneComponentQuery` 경계로 scheduler를 찾는다.
- `CharacterAiStressDebugScenarios`는 이미 만든 `Scheduler` 인스턴스에 직접 actor를 등록한다.
- 감사 문서의 `CharacterAiScheduler` 수치를 571 lines / 61 declarations로 갱신하고, stale editor wrapper 선언을 함수 인벤토리에서 제거했다.

효과:

- non-Editor runtime에서 `FindObjectsByType` / `FindFirstObjectByType` / `FindObjectOfType` / `GameObject.Find` 직접 사용은 0건이다.
- scene/resource lookup 잔여는 `DungeonSceneComponentQuery`, `DataScriptableObjectSource`, `CharacterAiActionAssetCatalog`, `InvasionIntruderDataProvider`, `TmpKoreanFontProvider` 같은 의도된 Infrastructure provider 경계에만 남는다.
- non-Editor runtime에서 `Access` facade, `.Instance`, `UtilSingleton`, `HasInstance`, `TryGetInstance` 직접 접근은 0건이다.

검증:

```text
Targeted direct scene/resource scan:
- no direct scene lookup remains in non-Editor runtime files outside Infrastructure provider/query boundaries

Targeted runtime singleton/access scan:
- no public static *Access facade, *Access. call, .Instance, HasInstance, TryGetInstance, or UtilSingleton remains in non-Editor Assets/Scripts
```

### 89. WorkforceReplanService static compatibility facade 제거

대상:

- `Assets/Scripts/Buildings/Shop.cs`
- `Assets/Scripts/Character/Work/WorkforceReplanService.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `WorkforceReplanService`의 static facade는 `DungeonRuntimeLifetimeScope.TryResolve<IWorkforceReplanService>`를 통해 컨테이너를 끌어다 쓰고 있었다.
- 호출처는 `Shop`의 checkout 대기/셀프서비스 경로 두 곳뿐이고, `Shop`은 이미 VContainer injection을 받고 있으므로 static facade를 유지할 이유가 없다.

변경:

- `Shop.ConstructShop`에 `IWorkforceReplanService`를 추가하고, checkout 대기열 변화 시 주입받은 service로 유휴 직원 재계획을 요청하게 했다.
- `WorkforceReplanService` static class와 내부 `ResolveService()`를 삭제했다.
- `DungeonWorkforceReplanService`는 `IDungeonSceneComponentQuery` 기반 실제 구현으로 유지했다.

효과:

- 상점 checkout 상태 변경이 더 이상 service locator를 경유하지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 13개에서 12개로 줄었다.
- `WorkforceReplanService.cs`는 38 lines / 5 declarations로 줄었고, static compatibility facade 후보에서 빠졌다.

검증:

```text
Targeted source scan:
- no WorkforceReplanService static class or RequestIdleWorkersToReplan facade call remains
- ConstructShop has no direct call sites, so the VContainer injection signature is the only runtime construction path
```

### 90. WorldInfoClickSelector static compatibility facade 제거

대상:

- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/UI/WorldInfoClickSelector.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `WorldInfoClickSelector` static facade는 `DungeonRuntimeLifetimeScope.TryResolve<IWorldInfoClickSelector>`로 컨테이너를 우회했다.
- 실제 호출처는 `BuildableObject.OnMouseDown()`과 `CharacterActor.OnMouseDown()`뿐이고, 둘 다 VContainer method injection 대상이다.
- “건물과 캐릭터가 겹치면 직원이 먼저 클릭되어야 한다”는 선택 정책은 `WorldInfoClickSelectionService`에 남기고, 호출자들은 해당 서비스를 직접 받는 편이 책임 경계가 맞다.

변경:

- `BuildableObject.ConstructBuildableObject`에 `IWorldInfoClickSelector`를 추가하고 건물 클릭 전에 주입 서비스로 캐릭터 우선 선택을 시도하게 했다.
- `CharacterActor.ConstructCharacterActor`에 `IWorldInfoClickSelector`를 추가하고 캐릭터 클릭도 주입 서비스로 처리하게 했다.
- `WorldInfoClickSelector` static class와 `ResolveSelector()`를 삭제했다.

효과:

- 클릭 선택 흐름이 더 이상 service locator를 경유하지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 12개에서 11개로 줄었다.
- `WorldInfoClickSelector.cs`는 141 lines / 10 declarations로 줄었고 static compatibility facade 후보에서 빠졌다.

검증:

```text
Targeted source scan:
- no WorldInfoClickSelector static facade or WorldInfoClickSelector.* call remains in non-Editor runtime scripts
- ConstructBuildableObject and ConstructCharacterActor have no direct call sites, so VContainer method injection is the runtime construction path
```

### 91. StaffWorkforceQueryService static compatibility facade 제거

대상:

- `Assets/Scripts/Character/Work/StaffWorkforceQueryService.cs`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`
- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `StaffWorkforceQueryService` static facade는 `DungeonRuntimeLifetimeScope.TryResolve<IStaffWorkforceQueryService>`로 컨테이너를 우회하고 있었다.
- 실제 런타임 호출처는 직원 우선순위 패널과 직원 탭 계열이며, 둘 다 VContainer 주입 또는 생성 직후 주입을 받을 수 있는 UI 경계다.
- 정적 facade를 남기면 직원 조회가 다시 service locator로 회귀하기 쉬우므로, 패널이 직접 `IStaffWorkforceQueryService`를 받게 하는 편이 책임이 분명하다.

변경:

- `StaffWorkforceQueryService` static class와 내부 `ResolveService()`를 삭제했다.
- `StaffWorkforceRuntimeQueryService`는 활성 직원 판정과 표시 이름 계산을 instance service 안에서 직접 처리한다.
- `StaffWorkPriorityPanel`은 `[Inject] ConstructStaffWorkPriorityPanel(IStaffWorkforceQueryService)`로 직원 조회 서비스를 받는다.
- `UITabManager`는 `IObjectResolver`를 주입받고, 동적으로 생성하거나 찾아낸 `StaffWorkPriorityPanel`에 `resolver.Inject(panel)`을 호출한다.
- `DungeonRuntimeLifetimeScope`는 씬에 이미 배치된 `StaffWorkPriorityPanel`도 초기 composition 단계에서 주입한다.

효과:

- 직원 우선순위 UI가 더 이상 static workforce query facade에 묶이지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 11개에서 10개로 줄었다.
- `StaffWorkforceQueryService.cs`는 51 lines / 9 declarations로 줄었고 static compatibility facade 후보에서 빠졌다.
- `StaffWorkPriorityPanel.cs`는 630 lines / 54 declarations, `UITabManager.cs`는 653 lines / 36 declarations로 갱신했다.

검증:

```text
Targeted source scan:
- no StaffWorkforceQueryService static class or StaffWorkforceQueryService.* call remains in non-Editor runtime scripts
- non-Editor runtime DungeonRuntimeLifetimeScope.TryResolve direct callers: 10
- generated StaffWorkPriorityPanel instances are injected through UITabManager's IObjectResolver
```

### 92. Tab summary query static capture facade 제거

대상:

- `Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs`
- `Assets/Scripts/Operation/OperationTabSummaryQuery.cs`
- `Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs`
- `Assets/Scripts/Offense/OffenseTabSummaryQuery.cs`
- `Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs`
- `Assets/Scripts/Codex/CodexRecordSummaryQuery.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 탭 본문은 이미 `UITabContentTextProvider`가 `IBuildingManagementSummaryService`, `IOperationTabSummaryService`, `IInvasionDefenseSummaryService`, `IOffenseTabSummaryService`, `IResearchCraftingSummaryService`, `ICodexRecordSummaryService`를 직접 주입받아 생성하고 있었다.
- 따라서 각 `*SummaryQuery.Capture()` static wrapper는 실제 호출자가 없는 service-locator 잔여물이었다.
- `BuildingManagementSummaryQuery`는 `FromBuildings`/`FromShops`/`FromWarehouses` 순수 계산 함수만 내부 서비스가 재사용하므로, 이 계산 API는 유지하고 컨테이너 resolve wrapper만 제거하는 편이 맞다.

변경:

- `OperationTabSummaryQuery`, `InvasionDefenseSummaryQuery`, `OffenseTabSummaryQuery`, `ResearchCraftingSummaryQuery`, `CodexRecordSummaryQuery` static class를 제거했다.
- `BuildingManagementSummaryQuery`에서는 `CaptureBuildings`/`CaptureShops`/`CaptureWarehouses`와 `ResolveService()`를 제거하고, 순수 변환 함수만 유지했다.
- 감사 문서의 라인 수와 선언 수를 갱신했다.

효과:

- 탭 요약 생성 경로가 모두 `UITabContentTextProvider`의 주입 서비스 호출로만 추적된다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 10개에서 4개로 줄었다.
- 자동 감사 요약은 305 script files, 6032 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no OperationTabSummaryQuery/ResearchCraftingSummaryQuery/CodexRecordSummaryQuery/OffenseTabSummaryQuery/InvasionDefenseSummaryQuery static class remains
- no BuildingManagementSummaryQuery.Capture* or query ResolveService remains
- non-Editor runtime DungeonRuntimeLifetimeScope.TryResolve direct callers: 4
```

### 93. CharacterFloatingIcon static facade 제거

대상:

- `Assets/Scripts/Character/UI/CharacterFloatingIcon.cs`
- `Assets/Scripts/Character/Ability/AbilityShopping.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterFloatingIcon.Show()` static facade는 `DungeonRuntimeLifetimeScope.TryResolve<IFloatingIconFeedbackService>`로 컨테이너를 우회했다.
- 실제 호출자는 구매 피드백의 `AbilityShopping`과 직원 보충 운반 피드백의 `WorkTaskExecutor` 두 곳뿐이었다.
- `AbilityShopping`은 VContainer method injection 대상이고, `WorkTaskExecutor`는 `AbilityWork`가 생성하는 일반 객체이므로 `AbilityWork`가 받은 서비스를 내부 property로 공급하면 static facade 없이도 흐름이 닫힌다.

변경:

- `CharacterFloatingIcon` static class와 `Show()` facade를 제거했다.
- 기본 아이콘 크기 상수는 service-locator 기능이 없는 `FloatingIconFeedbackDefaults`로 분리했다.
- `AbilityShopping.ConstructAbilityShopping`에 `IFloatingIconFeedbackService`를 추가하고 구매 아이콘 표시를 주입 서비스 호출로 바꿨다.
- `AbilityWork.ConstructAbilityWork`에 `IFloatingIconFeedbackService`를 추가하고, `WorkTaskExecutor`는 `work.FloatingIconFeedbackService.Show(...)`를 사용하게 했다.

효과:

- 구매/보충 피드백 아이콘 표시가 더 이상 static service locator를 경유하지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 4개에서 3개로 줄었다.
- `CharacterFloatingIcon.cs`는 78 lines / 8 declarations가 됐고, static compatibility facade 후보에서 빠졌다.

검증:

```text
Targeted source scan:
- no CharacterFloatingIcon static class or CharacterFloatingIcon.* call remains in non-Editor runtime scripts
- ConstructAbilityShopping and ConstructAbilityWork have no direct call sites, so VContainer method injection is the runtime construction path
- non-Editor runtime DungeonRuntimeLifetimeScope.TryResolve direct callers: 3
```

### 94. WorkGridUtility static facade 제거

대상:

- `Assets/Scripts/Character/Work/WorkGridUtility.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/Work/WorkTargetSelector.cs`
- `Assets/Scripts/Character/Work/WorkCommandHandler.cs`
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `WorkGridUtility` static facade는 `DungeonRuntimeLifetimeScope.TryResolve<IWorkGridResolver>`로 컨테이너를 우회했다.
- 모든 호출자는 `AbilityWork`가 생성하는 작업 모듈(`WorkTargetSelector`, `WorkCommandHandler`, `WorkTaskExecutor`) 내부였다.
- 따라서 각 모듈이 static utility를 부르는 대신, owner인 `AbilityWork`가 주입받은 `IWorkGridResolver`를 제공하는 구조가 더 책임 경계에 맞다.

변경:

- `AbilityWork.ConstructAbilityWork`에 `IWorkGridResolver`를 추가하고 내부 `WorkGridResolver` property로 노출했다.
- `WorkTargetSelector`, `WorkCommandHandler`, `WorkTaskExecutor`의 `WorkGridUtility.ResolveActiveGrid`/`GetGridPosition` 호출을 `work.WorkGridResolver` 호출로 바꿨다.
- `WorkGridUtility` static class와 내부 `ResolveResolver()`를 제거하고, `IWorkGridResolver`/`WorkGridResolver`만 남겼다.

효과:

- 작업 대상 선택/명령/실행 모듈이 더 이상 static service locator로 grid resolver를 얻지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 3개에서 2개로 줄었다.
- `WorkGridUtility.cs`는 48 lines / 5 declarations가 됐고, static compatibility facade 후보에서 빠졌다.
- 자동 감사 요약은 305 script files, 6028 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no WorkGridUtility static class or WorkGridUtility.* call remains in non-Editor runtime scripts
- ConstructAbilityWork has no direct call sites, so VContainer method injection is the runtime construction path
- non-Editor runtime DungeonRuntimeLifetimeScope.TryResolve direct callers: 2
```

### 95. TMPKoreanFont container resolve 제거

대상:

- `Assets/Scripts/UI/TMPKoreanFont.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `TMPKoreanFont.Resolve()`는 UI helper 전역 호출자가 많아 당장 모든 UI factory/panel에 font provider를 주입하면 변경 범위가 과하게 커진다.
- 그러나 helper 내부에서 `DungeonRuntimeLifetimeScope.TryResolve<ITmpKoreanFontProvider>`를 직접 호출하는 것은 service-locator 잔여물이므로 제거해야 한다.
- composition root가 런타임 시작 시 `ITmpKoreanFontProvider`로 폰트를 읽어 `TMPKoreanFont.SetOverrideFont()`에 명시 설정하면, 호출자들은 기존 helper를 쓰면서도 컨테이너 우회는 사라진다.

변경:

- `TMPKoreanFont.Resolve()`에서 `DungeonRuntimeLifetimeScope.TryResolve` 호출을 제거하고, 사전 설정된 font가 없으면 명시 예외를 던지게 했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`에서 `resolver.Resolve<ITmpKoreanFontProvider>().GetRequiredFont()`를 `TMPKoreanFont.SetOverrideFont()`로 설정한다.

효과:

- TMP 폰트 적용 helper가 더 이상 service locator로 컨테이너를 직접 읽지 않는다.
- `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 2개에서 1개로 줄었다.
- `TMPKoreanFont.cs`는 41 lines / 7 declarations로 줄었다.

검증:

```text
Targeted source scan:
- no DungeonRuntimeLifetimeScope.TryResolve remains in TMPKoreanFont.cs
- non-Editor runtime DungeonRuntimeLifetimeScope.TryResolve direct callers: 1
```

### 96. DungeonRuntimeLifetimeScope.TryResolve service locator 제거

대상:

- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 마지막 `DungeonRuntimeLifetimeScope.TryResolve` 호출자는 `CharacterAiDecisionPipeline.ResolveFacilityLookup()`이었다.
- BehaviorDesigner task들이 static pipeline을 직접 호출하므로 pipeline 전체를 instance service로 바꾸는 것은 이번 범위에서는 과하다.
- 대신 composition root가 `ICharacterAiFacilityLookup`을 pipeline에 명시 구성하고, pipeline은 설정 누락을 예외로 드러내게 하면 service locator 없이 기존 BT task 호출 구조를 유지할 수 있다.
- `AiDirectorRuntime`은 VContainer method injection 대상이므로 facility lookup을 직접 주입받아 `CharacterAiDecisionPipeline.FindFacility` 외부 호출을 제거할 수 있다.

변경:

- `CharacterAiDecisionPipeline.ConfigureFacilityLookup()` / `ClearFacilityLookup()`을 추가하고, 내부 macro facility lookup은 구성된 `ICharacterAiFacilityLookup`을 사용하게 했다.
- `CharacterAiDecisionPipeline.FindFacility`는 외부 public API에서 private 내부 helper로 낮췄다.
- `AiDirectorRuntime.ConstructAiDirectorRuntime`에 `ICharacterAiFacilityLookup`을 추가하고 mood impulse target 검증/side effect에서 주입 서비스를 사용하게 했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`에서 pipeline facility lookup을 구성하고, `OnDestroy()`에서 해제한다.
- `DungeonRuntimeLifetimeScope.TryResolve<T>()`, static `current`, resolver용 `Awake()`를 제거했다.
- Editor debug scenario의 scheduler path-budget reset은 compile adapter로 `DungeonSceneComponentQuery`를 직접 사용하게 했다.

효과:

- non-Editor runtime에서 `DungeonRuntimeLifetimeScope.TryResolve` 직접 호출자는 1개에서 0개가 됐다.
- `DungeonRuntimeLifetimeScope.TryResolve<T>()` API 자체가 제거되어 새 런타임 코드가 service locator로 회귀하기 어려워졌다.
- 자동 감사 요약은 305 script files, 6029 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no DungeonRuntimeLifetimeScope.TryResolve, public static bool TryResolve, or Container.TryResolve remains in non-Editor runtime scripts
- no public static *Access facade, .Instance, HasInstance, TryGetInstance, or UtilSingleton remains in non-Editor runtime scripts
- direct scene/resource lookup remains only in intentional Infrastructure provider/query boundaries
```

### 97. MetaProgressionRuntime run progress tracker 분리

대상:

- `Assets/Scripts/Meta/MetaProgressionSystem.cs`
- `Assets/Scripts/Meta/MetaRunProgressTracker.cs`
- `Assets/Scripts/Meta/MetaRunProgressTracker.cs.meta`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `MetaProgressionRuntime`은 VContainer로 run-result builder/panel service를 받는 런타임 orchestration 지점이어야 한다.
- 기존 파일은 orchestration 외에도 운영일, 침공 위협, 방어 성공, 발견 시설, 해금 recipe, 원정 성공 카운터를 직접 소유했다.
- 이 상태에서는 메타 보상 조건이 늘어날수록 이벤트 수집 규칙과 런 종료 보상 적용 규칙이 한 클래스 안에서 계속 엉킨다.

변경:

- `MetaRunProgressTracker`를 추가해 런 시작, 운영일, 침공, 시설 방문, 연구, 합성, 원정 성공 기록을 전담하게 했다.
- `MetaProgressionRuntime`은 이벤트를 tracker에 전달하고, 런 종료 시 `runProgress.CreateResultContext()`로 결과 빌더에 필요한 context만 넘긴다.
- 보존 recipe 정렬도 tracker의 `UnlockedRecipeIds`에서 읽도록 바꿔 런타임의 HashSet 소유를 제거했다.

효과:

- `MetaProgressionSystem.cs`는 332 lines / 35 declarations에서 247 lines / 31 declarations로 줄었다.
- 런 이벤트 수집/누적 정책은 `MetaRunProgressTracker.cs` 144 lines / 17 declarations로 분리됐다.
- 자동 감사 요약은 305 script files, 6042 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- MetaProgressionRuntime no longer owns discoveredFacilityIdsThisRun, unlockedRecipeIdsThisRun, run counter fields, CreateResultContext, RecordThreat, or GetThreatStageScore
- MetaProgressionRuntime run event handlers delegate progress collection to MetaRunProgressTracker
- Unity asset meta was added for MetaRunProgressTracker.cs
```

### 98. EventAlert selection state / choice presenter 분리

대상:

- `Assets/Scripts/Operation/EventAlertSystem.cs`
- `Assets/Scripts/Operation/EventAlertSelectionState.cs`
- `Assets/Scripts/Operation/EventAlertSelectionState.cs.meta`
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs.meta`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `EventAlertRuntime`은 알림 수신, merge, event log, VContainer presenter 연결만 담당하는 편이 낫다.
- 선택된 record와 선택지 index 검증/콜백 실행은 UI 생성도, 이벤트 수신도 아닌 선택 상태 책임이다.
- `EventAlertViewPresenter`는 button dictionary, detail panel, choice button list를 모두 들고 있었는데, choice button은 detail panel의 하위 lifetime으로 따로 관리할 수 있다.

변경:

- `EventAlertSelectionState`를 추가해 selected record와 choice 실행 검증을 분리했다.
- `EventAlertRuntime.Open/ExecuteChoice/SelectedRecord`는 public API를 유지하면서 selection state에 위임한다.
- `EventAlertChoicePresenter`를 추가해 선택지 버튼 rebuild/clear와 play/edit mode destroy 분기를 전담하게 했다.
- `EventAlertViewPresenter`는 detail panel 열기와 alert button 관리만 남기고, 선택지 버튼 생성은 choice presenter로 위임한다.

효과:

- `EventAlertSystem.cs`는 130 lines / 13 declarations에서 129 lines / 14 declarations가 됐다. 선택 상태 객체가 추가되어 선언은 1개 늘었지만 selected-record 검증 책임이 빠졌다.
- `EventAlertViewPresenter.cs`는 246 lines / 19 declarations에서 216 lines / 18 declarations로 줄었다.
- 선택지 버튼 생명주기는 `EventAlertChoicePresenter.cs` 53 lines / 4 declarations로 분리됐다.
- 선택 상태는 `EventAlertSelectionState.cs` 23 lines / 4 declarations로 분리됐다.
- 자동 감사 요약은 307 script files, 6050 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- EventAlertRuntime no longer owns selectedRecord directly
- EventAlertViewPresenter no longer owns choiceButtons or RebuildChoices
- EventAlertChoicePresenter and EventAlertSelectionState have Unity asset meta files
```

### 99. Main camera provider 추출

대상:

- `Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/UI/CameraManager.cs`
- `Assets/Scripts/UI/WorldInfoClickSelector.cs`
- `Assets/Scripts/Character/Input/OwnerCommandController.cs`
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `Camera.main`은 씬 태그/전역 조회에 기대는 암묵 의존성이다.
- 클릭 선택, 우클릭 명령, AI visible 판단, 카메라 manager fallback이 각자 `Camera.main`을 읽으면 테스트와 씬 구성 검증 지점이 흩어진다.
- 이미 scene lookup은 `IDungeonSceneComponentQuery` provider 경계로 모으는 방향이므로, main camera도 같은 Infrastructure 경계에 두는 편이 맞다.

변경:

- `IMainCameraProvider`와 `SceneMainCameraProvider`를 추가했다.
- `SceneCameraWorldPointerPositionProvider`도 직접 scene query로 카메라를 찾지 않고 `IMainCameraProvider`를 사용하게 했다.
- `CameraManager`, `WorldInfoClickSelectionService`, `OwnerCommandController`, `CharacterAiScheduler`가 `IMainCameraProvider`를 주입받도록 바꿨다.
- `DungeonRuntimeLifetimeScope`에 `SceneMainCameraProvider` 등록을 추가했다.

효과:

- non-Editor runtime에서 직접 `Camera.main` 접근이 사라졌다.
- 남은 직접 scene/resource lookup은 의도된 Infrastructure provider/query 경계에만 남았다.
- 자동 감사 요약은 307 script files, 6056 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no Camera.main remains in non-Editor runtime scripts
- direct FindObject/Resources lookup remains only in Infrastructure provider/query boundaries
- diff check on touched camera-provider files reported only CRLF warnings
```

### 100. Facility/room cache service 분리

대상:

- `Assets/Scripts/Character/AI/FacilityCandidateCache.cs`
- `Assets/Scripts/Rooms/RoomRegistry.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `FacilityCandidateCache`는 grid별 facility 후보 cache와 dynamic state version을 static 필드로 직접 들고 있었다.
- `RoomRegistry`도 grid별 room layout cache를 static 필드로 직접 들고 있었다.
- 더 나쁜 부분은 `FacilityCandidateCache.Clear()`가 `RoomRegistry.Clear()`까지 호출해 facility 후보 cache와 room layout cache invalidation 책임이 한 방향으로 엮여 있었다는 점이다.
- 호출부가 많아 static facade 자체를 한 번에 없애면 변경 범위가 지나치게 커지므로, 이번 단계에서는 상태 소유와 cache 간 결합을 먼저 분리한다.

변경:

- `IFacilityCandidateCache` / `FacilityCandidateCacheStore`를 추가하고 후보 cache 상태를 instance service가 소유하게 했다.
- `IRoomLayoutCache` / `RoomLayoutCache`를 추가하고 room layout cache 상태를 instance service가 소유하게 했다.
- 기존 `FacilityCandidateCache`와 `RoomRegistry` static class는 VContainer composition root가 configure하는 facade로 낮췄다.
- `FacilityCandidateCache.Clear()`에서 `RoomRegistry.Clear()` 호출을 제거했다. room layout 무효화는 실제 구조 교체를 수행하는 `FacilityEvolutionRuntime` 호출부에 남아 있다.
- `DungeonRuntimeLifetimeScope`에서 두 cache service를 등록하고 build callback에서 facade를 구성하며, `OnDestroy()`에서 구성을 해제한다.

효과:

- static class가 직접 cache dictionary를 소유하지 않는다.
- facility 후보 cache와 room layout cache의 무효화 책임이 분리됐다.
- `FacilityCandidateCache.cs`는 119 lines / 9 declarations에서 162 lines / 21 declarations가 됐다.
- `RoomRegistry.cs`는 50 lines / 5 declarations에서 96 lines / 16 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6079 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no RoomRegistry.Clear call remains inside FacilityCandidateCache.cs
- FacilityCandidateCacheStore and RoomLayoutCache own instance cache dictionaries
- DungeonRuntimeLifetimeScope registers and configures IFacilityCandidateCache and IRoomLayoutCache
```

### 101. TMPKoreanFont 적용 서비스 분리

대상:

- `Assets/Scripts/UI/TMPKoreanFont.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `TMPKoreanFont`는 많은 UI 호출부가 공유하는 compatibility helper라 호출 구조를 한 번에 모두 주입식으로 바꾸면 변경 범위가 지나치게 커진다.
- 다만 helper가 static 필드로 `TMP_FontAsset` 자체를 들고 있으면, 폰트 해석/적용 책임과 전역 상태가 계속 UI utility에 남는다.
- 이미 `ITmpKoreanFontProvider`가 Infrastructure provider 경계로 분리되어 있으므로, 적용 책임도 instance service로 옮기는 편이 VContainer 설계와 맞다.

변경:

- `ITmpKoreanFontService`와 `TmpKoreanFontService`를 추가했다.
- `TmpKoreanFontService`가 `ITmpKoreanFontProvider`로 폰트를 resolve하고 `TMP_Text`/children 적용을 담당한다.
- `TMPKoreanFont` static helper는 `ITmpKoreanFontService`를 보관하는 compatibility facade로 낮췄다.
- Editor 주입 경로 호환을 위해 `TmpKoreanFontAssetProvider`와 `SetOverrideFont`를 유지했다.
- `DungeonRuntimeLifetimeScope`에서 `TmpKoreanFontService`를 등록하고 build callback에서 `TMPKoreanFont.Configure(...)`를 수행한다.
- `OnDestroy()`에서 `TMPKoreanFont.ClearConfiguredService()`를 호출해 static facade 연결을 해제한다.

효과:

- `TMPKoreanFont`가 더 이상 static `TMP_FontAsset` cache를 직접 소유하지 않는다.
- 런타임 폰트 해석/적용 책임은 VContainer가 소유하는 instance service로 이동했다.
- `TMPKoreanFont.cs`는 41 lines / 7 declarations에서 111 lines / 22 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6094 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no cached TMP_FontAsset field remains in TMPKoreanFont.cs
- DungeonRuntimeLifetimeScope registers ITmpKoreanFontService and configures TMPKoreanFont with that service
- editor SetOverrideFont compatibility path still exists through TmpKoreanFontAssetProvider
```

### 102. Invasion intruder factory DI 분리

대상:

- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `InvasionIntruderFactory`는 침입자 GameObject에 `CharacterActor`, `AbilityMove`, `Collider2D`, `InvasionIntruderRuntime`, visual child를 붙이는 scene mutation factory다.
- 이 책임이 static class에 있으면 침입자 생성 정책을 테스트에서 교체하기 어렵고, 감독 런타임이 어떤 factory를 쓰는지 composition root에서 볼 수 없다.
- 호출부는 `InvasionDirectorRuntime` 한 곳뿐이라 DI 서비스로 옮기기 좋은 작은 경계다.

변경:

- `IInvasionIntruderFactory`와 `InvasionIntruderRuntimeFactory`를 추가했다.
- 기존 static `InvasionIntruderFactory`는 제거하고, component 보강 로직을 instance factory로 이동했다.
- `InvasionDirectorRuntime`이 `IInvasionIntruderFactory`를 주입받아 침입자 runtime을 보장하게 했다.
- `DungeonRuntimeLifetimeScope`에 `InvasionIntruderRuntimeFactory` 등록을 추가했다.
- 깨진 로그 문자열 하나가 컴파일을 막을 수 있어 `ResolveSuppressedBy`의 억제 로그를 ASCII 문자열로 고쳤다.

효과:

- 침입자 component 생성/보강 책임이 VContainer로 보이는 명시 서비스가 됐다.
- `InvasionDirectorRuntime`의 침입자 생성 정책을 테스트/다른 composition에서 교체할 수 있다.
- `InvasionIntruderFactory.cs`는 53 lines / 3 declarations에서 63 lines / 5 declarations가 됐다.
- `InvasionIntruderSystem.cs`는 403 lines / 27 declarations 상태가 됐다.
- 자동 감사 요약은 307 script files, 6097 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no InvasionIntruderFactory.EnsureRuntime static call remains
- DungeonRuntimeLifetimeScope registers InvasionIntruderRuntimeFactory as IInvasionIntruderFactory
- InvasionDirectorRuntime resolves IInvasionIntruderFactory before component creation
```

### 103. Room facility policy service 분리

대상:

- `Assets/Scripts/Rooms/RoomFacilityPolicy.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `RoomFacilityPolicy`는 방문 가능 역할과 방 품질 점수를 판정하는 정책 계층인데, 내부에서 `RoomRegistry.TryGetRoom` static facade를 직접 호출하고 있었다.
- 앞 단계에서 room layout cache 상태는 `IRoomLayoutCache`로 분리했으므로, 정책도 같은 cache service를 직접 주입받는 쪽이 책임 경계가 맞다.
- 호출부는 기존 static facade를 유지해도 되지만, 실제 판정 책임은 instance service로 이동해야 테스트와 대체가 쉬워진다.

변경:

- `IRoomFacilityPolicy`와 `RoomFacilityPolicyService`를 추가했다.
- `RoomFacilityPolicyService`가 `IRoomLayoutCache`를 주입받아 room-role availability와 room utility score를 계산한다.
- 기존 `RoomFacilityPolicy` static class는 VContainer composition root가 configure하는 compatibility facade로 낮췄다.
- `DungeonRuntimeLifetimeScope`에 `RoomFacilityPolicyService` 등록, build callback configure, `OnDestroy()` clear를 추가했다.

효과:

- `RoomFacilityPolicy`가 더 이상 `RoomRegistry` static facade를 직접 호출하지 않는다.
- 방 레이아웃 cache, 방 registry facade, 방 정책 판정 책임이 더 분리됐다.
- `RoomFacilityPolicy.cs`는 53 lines / 3 declarations에서 117 lines / 13 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6107 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no RoomRegistry call remains inside RoomFacilityPolicy.cs
- DungeonRuntimeLifetimeScope registers RoomFacilityPolicyService as IRoomFacilityPolicy
- build callback configures RoomFacilityPolicy facade through IRoomFacilityPolicy
```

### 104. Facility evolution state service 분리

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `FacilityEvolutionStateUtility.GetOrAdd`는 시설 GameObject에 `FacilityEvolutionStateComponent`를 붙이는 scene mutation 책임을 static utility에 두고 있었다.
- 시설 진화 runtime/service가 이 helper를 여러 곳에서 호출하므로, 실제 component 생성 책임은 DI service로 드러나는 편이 테스트와 대체에 유리하다.
- 기존 static 호출부를 전부 갈아엎는 대신, static utility는 composition root가 configure하는 facade로 낮추는 단계가 안전하다.

변경:

- `IFacilityEvolutionStateService`와 `FacilityEvolutionStateService`를 추가했다.
- 당시 기준으로 `FacilityEvolutionStateService.GetOrAdd`가 `FacilityEvolutionStateComponent` 조회/생성/초기화를 담당했다.
- 이후 168번에서 컴포넌트 생성/초기화 책임은 `FacilityEvolutionStateComponentFactory`로 분리됐다.
- 기존 `FacilityEvolutionStateUtility.GetOrAdd`는 configured service에 위임하게 했다.
- `DungeonRuntimeLifetimeScope`에 state service 등록, build callback configure, `OnDestroy()` clear를 추가했다.

효과:

- 당시 기준으로 시설 진화 state component 생성 책임이 명시 DI service로 이동했다.
- 이후 168번에서 service는 `IFacilityEvolutionStateComponentFactory`에 위임하는 정책 경계로 축소됐다.
- static utility는 직접 `AddComponent<FacilityEvolutionStateComponent>`를 수행하지 않는다.
- `FacilityEvolutionState.cs`는 242 lines / 12 declarations에서 275 lines / 19 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6114 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- FacilityEvolutionStateUtility only delegates to IFacilityEvolutionStateService
- AddComponent<FacilityEvolutionStateComponent> remained in FacilityEvolutionStateService at this step.
- Current owner after step 168: FacilityEvolutionStateComponentFactory.cs
- DungeonRuntimeLifetimeScope registers and configures IFacilityEvolutionStateService
```

### 105. RoomProfileBuilder room layout cache DI 분리

대상:

- `Assets/Scripts/FacilityEvolution/RoomProfile.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `RoomProfileBuilder.Build`는 방 프로파일 생성 책임을 갖지만, 내부에서 `RoomRegistry.TryGetRoom` static facade를 직접 호출하고 있었다.
- 방 레이아웃 캐시는 이미 `IRoomLayoutCache`로 분리됐으므로, 시설 진화 프로파일러도 같은 cache service를 명시적으로 받아야 한다.
- 기본 `RoomProfileBuilder` 생성은 `FacilityEvolutionRuntime`/`FacilityEvolutionEngine`의 composition 경계에서 처리하고, 누락 시 fallback 없이 명시 예외를 내는 편이 디버깅에 맞다.

변경:

- `RoomProfileBuilder` 생성자가 `IFacilityEvolutionRecordProvider`와 `IRoomLayoutCache`를 필수로 받게 했다.
- `RoomProfileBuilder.Build`에서 `RoomRegistry.TryGetRoom` 호출을 제거하고 주입받은 `IRoomLayoutCache.TryGetRoom`만 사용하게 했다.
- `FacilityEvolutionRuntime.ConstructFacilityEvolutionRuntime`에 `IRoomLayoutCache` 주입을 추가하고, `CreateEngine()`에서 같은 cache를 `RoomProfileBuilder`와 `FacilityEvolutionEngine`에 전달하게 했다.
- 명시 구성 경로인 `FacilityEvolutionRuntime.Configure`에도 `IRoomLayoutCache` 인자를 추가했다.
- 에디터 디버그 시나리오는 리팩터 대상은 아니지만, 생성자 계약 변경으로 컴파일이 깨지지 않도록 명시 `RoomLayoutCache`만 전달했다.

효과:

- 시설 진화의 방 프로파일 생성 경로가 `RoomRegistry` static facade에서 분리됐다.
- `FacilityEvolutionRuntime`은 recipe/record/token/grid/LLM/object resolver에 더해 room layout cache까지 VContainer 주입으로 받는 명시 orchestration 경계가 됐다.
- `RoomProfile.cs`는 394 lines / 31 declarations, `FacilityEvolutionRuntime.cs`는 741 lines / 46 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6115 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no RoomRegistry.TryGetRoom call remains in RoomProfile.cs
- all RoomProfileBuilder constructors pass IRoomLayoutCache explicitly
- FacilityEvolutionRuntime requires IRoomLayoutCache injection through ResolveRoomLayoutCache()
```

### 106. Facility evolution engine state/cache invalidation DI 분리

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 105번에서 `RoomProfileBuilder`는 `IRoomLayoutCache`를 받게 됐지만, 실제 진화 실행 경로인 `FacilityEvolutionEngine.TryEvolve`는 아직 `FacilityEvolutionStateUtility`, `FacilityCandidateCache`, `RoomRegistry` static facade를 직접 호출했다.
- 진화 엔진의 책임은 후보 판정/재료 소비/건물 교체 orchestration이지, static facade를 통해 캐시와 state component 생성 위치를 숨기는 것이 아니다.
- 이미 `IFacilityEvolutionStateService`, `IFacilityCandidateCache`, `IRoomLayoutCache`가 VContainer에 등록되어 있으므로 엔진 생성자 계약으로 드러내는 편이 테스트와 디버깅에 맞다.

변경:

- `FacilityEvolutionEngine` 생성자에 `IFacilityEvolutionStateService`와 `IFacilityCandidateCache`를 추가했다.
- `BuildContext`, `TryEvolve`에서 `FacilityEvolutionStateUtility.GetOrAdd` 대신 주입된 state service를 사용하게 했다.
- 진화 완료 후 `FacilityCandidateCache.MarkDynamicStateDirty()`와 `RoomRegistry.Clear()` 대신 주입된 `IFacilityCandidateCache.MarkDynamicStateDirty()`와 `IRoomLayoutCache.Clear()`를 호출하게 했다.
- `FacilityEvolutionRuntime.ConstructFacilityEvolutionRuntime`과 `CreateEngine()`에 state/cache service 전달을 추가하고, 누락 시 `ResolveStateService()` / `ResolveFacilityCandidateCache()`에서 명시 예외를 내게 했다.
- 에디터 디버그 헬퍼는 생성자 계약 변경에 맞춰 명시 service 인스턴스를 전달했다.

효과:

- `FacilityEvolutionEngine` 내부에서는 더 이상 `FacilityEvolutionStateUtility`, `FacilityCandidateCache`, `RoomRegistry` static facade를 직접 호출하지 않는다.
- 진화 실행 후 캐시 무효화 경로가 VContainer로 보이는 명시 의존성이 됐다.
- `FacilityEvolutionRuntime.cs`는 777 lines / 48 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6117 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no FacilityEvolutionStateUtility.GetOrAdd call remains in FacilityEvolutionRuntime.cs
- no FacilityCandidateCache.MarkDynamicStateDirty call remains in FacilityEvolutionRuntime.cs
- no RoomRegistry.Clear call remains in FacilityEvolutionRuntime.cs
- FacilityEvolutionRuntime requires state/cache services through ResolveStateService() and ResolveFacilityCandidateCache()
```

### 107. Facility evolution record cache dirty DI 분리

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `FacilityEvolutionRecordRuntime`은 방문, 매출, 재고 소비, 범죄, 방어 발동, 침공 피해, 영업일 종료 이벤트를 받아 방 진화 기록을 누적하는 런타임이다.
- 기록 변경 뒤 AI 시설 후보 cache를 무효화하는 책임은 필요하지만, `FacilityCandidateCache.MarkDynamicStateDirty()` static facade를 직접 호출하면 기록 런타임이 cache 구현/초기화 방식에 묶인다.
- 이미 `IFacilityCandidateCache`가 VContainer에 등록되어 있으므로 record runtime도 같은 cache service를 주입받는 편이 책임 경계가 맞다.

변경:

- `FacilityEvolutionRecordRuntime`에 `[Inject] Construct(IFacilityCandidateCache)`를 추가했다.
- 모든 record event path의 `FacilityCandidateCache.MarkDynamicStateDirty()` 호출을 `MarkDynamicStateDirty()` private helper로 모으고, helper가 주입된 `IFacilityCandidateCache`를 사용하게 했다.
- 주입 누락은 `ResolveFacilityCandidateCache()`에서 fallback 없이 명시 예외를 내게 했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents`가 씬의 `FacilityEvolutionRecordRuntime`에도 VContainer injection을 수행하게 했다.

효과:

- `FacilityEvolutionRecordRuntime.cs` 안에는 더 이상 `FacilityCandidateCache.MarkDynamicStateDirty()` static facade 호출이 없다.
- 진화 기록 누적과 시설 후보 cache 무효화의 연결이 VContainer로 보이는 명시 의존성이 됐다.
- `FacilityEvolutionRecordRuntime.cs`는 451 lines / 58 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6120 extracted declarations, `DependencyInjection` 135 files 상태가 됐다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache.MarkDynamicStateDirty call remains in FacilityEvolutionRecordRuntime.cs
- FacilityEvolutionRecordRuntime has Construct(IFacilityCandidateCache)
- DungeonRuntimeLifetimeScope injects FacilityEvolutionRecordRuntime scene components
```

### 108. Facility evolution service state DI 분리

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 106번에서 `FacilityEvolutionEngine` 내부 state 접근은 DI service로 바꿨지만, `FacilityEvolutionService.GetSourceCandidates`와 `FacilityEvolutionService.Validate`는 여전히 `FacilityEvolutionStateUtility.GetOrAdd` static facade를 직접 호출했다.
- 두 static helper는 recipe query service와 validator service 뒤에서 호출되므로, state 생성/조회 책임을 helper 안에 숨기지 않고 호출 경계에서 `IFacilityEvolutionStateService`를 넘기는 편이 맞다.
- `FacilityEvolutionRecipeQuery`와 `DefaultFacilityEvolutionValidator`는 이미 VContainer 또는 엔진 생성자 경계에서 만들어지는 객체라 state service 의존성을 명시하기 좋다.

변경:

- `FacilityEvolutionService.GetSourceCandidates`와 `Validate`에 `IFacilityEvolutionStateService` 인자를 추가했다.
- 두 helper 내부의 `FacilityEvolutionStateUtility.GetOrAdd` 호출을 주입받은 state service 호출로 교체했다.
- `FacilityEvolutionRecipeQuery`가 `IFacilityEvolutionStateService`를 생성자로 받아 source candidate helper에 전달하게 했다.
- `DefaultFacilityEvolutionValidator`가 `IFacilityEvolutionStateService`를 생성자로 받아 validation helper에 전달하게 했다.
- 에디터 디버그 query 구현체는 생성자 계약 변화에 맞춰 명시 state service를 전달하게만 수정했다.

효과:

- `FacilityEvolutionService.cs` 안에는 더 이상 `FacilityEvolutionStateUtility.GetOrAdd` 호출이 없다.
- 진화 source-candidate 조회와 validation의 state component 접근이 VContainer/engine 생성 경계에서 보이는 명시 의존성이 됐다.
- `FacilityEvolutionRuntime.cs`는 783 lines / 48 declarations, `FacilityRecipeCatalogServices.cs`는 195 lines / 25 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6120 extracted declarations 상태를 유지한다.

검증:

```text
Targeted source scan:
- no FacilityEvolutionStateUtility.GetOrAdd call remains in non-Editor FacilityEvolution scripts except the configured facade itself
- FacilityEvolutionRecipeQuery constructor includes IFacilityEvolutionStateService
- DefaultFacilityEvolutionValidator constructor includes IFacilityEvolutionStateService
```

### 109. BuildableObject cache dirty DI 분리

대상:

- `Assets/Scripts/Buildings/BuildableObject.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `BuildableObject`는 파괴, 피해 상태, 시설 레벨, 방문/작업 예약, 사용 인원 변화처럼 AI 시설 후보 cache에 영향을 주는 상태 변경을 소유한다.
- 다만 각 상태 변경 path에서 `FacilityCandidateCache.MarkDynamicStateDirty()` static facade를 직접 호출하면 모든 건물 lifecycle/reservation 로직이 cache 구현과 static 초기화 방식에 묶인다.
- 씬 배치 건물은 `DungeonRuntimeLifetimeScope`가 inject 하고, 런타임 생성 건물은 `GridBuildingFactory` 생성 콜백에서 inject 된 뒤 `SetGrid/Initialization`으로 넘어가므로 `BuildableObject`도 cache service를 명시 주입받을 수 있다.

변경:

- `BuildableObject.ConstructBuildableObject`에 `IFacilityCandidateCache` 인자를 추가했다.
- 모든 `FacilityCandidateCache.MarkDynamicStateDirty()` 호출을 `MarkFacilityDynamicStateDirty()` private helper로 모았다.
- helper는 `ResolveFacilityCandidateCache()`를 통해 주입된 `IFacilityCandidateCache.MarkDynamicStateDirty()`만 호출하고, 주입 누락은 fallback 없이 명시 예외를 낸다.

효과:

- `BuildableObject.cs` 안에는 더 이상 `FacilityCandidateCache.MarkDynamicStateDirty()` static facade 호출이 없다.
- 건물 상태 변화와 시설 후보 cache 무효화의 연결이 VContainer 의존성으로 보이게 됐다.
- `BuildableObject.cs`는 550 lines / 44 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6122 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache.MarkDynamicStateDirty call remains in BuildableObject.cs
- BuildableObject.ConstructBuildableObject includes IFacilityCandidateCache
- scene BuildableObject instances are injected by DungeonRuntimeLifetimeScope
- runtime-created BuildableObject instances are injected through GridBuildingFactory callbacks before SetGrid/Initialization
```

### 110. Facility candidate scorer cache DI 분리

대상:

- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/AI/FacilityCandidateScorer.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `FacilityCandidateScorer`는 AI action/consideration이 공통으로 쓰는 후보 시설 조회와 점수 계산 helper다.
- reputation bias는 이미 `FacilityScoringContext`로 명시 주입 경계를 만들었지만, path-search cache가 있을 때 후보 source는 여전히 `FacilityCandidateCache.GetCandidates()` static facade를 직접 호출했다.
- scorer를 통째로 MonoBehaviour/service로 바꾸면 action asset 호출부 전반에 파급이 커지므로, 이미 VContainer로 주입되는 `AIBrain`이 `IFacilityCandidateCache`를 보관하고 scorer가 actor brain에서 명시 cache를 요구하게 하는 편이 현재 구조와 더 잘 맞다.

변경:

- `AIBrain.ConstructAIBrain`에 `IFacilityCandidateCache` 인자를 추가했다.
- `AIBrain.RequireFacilityCandidateCache()`를 추가해 주입 누락 시 fallback 없이 명시 예외를 내게 했다.
- `FacilityCandidateScorer.GetCandidateSource`가 `FacilityCandidateCache.GetCandidates()` static facade 대신 `actor.Brain.RequireFacilityCandidateCache().GetCandidates(...)`를 사용하게 했다.
- actor/brain 없이 cached source를 요구하는 호출은 `FacilityCandidateScorer.RequireFacilityCandidateCache`에서 명시 예외를 낸다.

효과:

- non-Editor runtime에서 `FacilityCandidateCache.GetCandidates()` 직접 호출이 사라졌다.
- AI 시설 후보 조회 cache 의존성이 character brain의 VContainer 주입 경계로 드러났다.
- `AIBrain.cs`는 1261 lines / 60 declarations, `FacilityCandidateScorer.cs`는 481 lines / 19 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6124 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache.GetCandidates call remains in non-Editor runtime scripts
- FacilityCandidateScorer resolves cached candidate source through AIBrain.RequireFacilityCandidateCache
- AIBrain.ConstructAIBrain includes IFacilityCandidateCache
```

### 111. Work ability cache dirty DI 분리

대상:

- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`
- `Assets/Scripts/Character/Work/WorkDutyController.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `AbilityWork`는 직원의 assigned work, duty state, restock/research/repair 실행을 소유하는 작업 능력 루트다.
- 하위 모듈인 `WorkTaskExecutor`와 `WorkDutyController`가 `FacilityCandidateCache.MarkDynamicStateDirty()` static facade를 직접 호출하면, 작업 실행/근무 상태 결정 로직이 cache 구현에 묶인다.
- 하위 모듈은 `AbilityWork`가 직접 생성해 보유하므로, cache service는 `AbilityWork`에 주입하고 하위 모듈은 `work.MarkFacilityDynamicStateDirty()`만 호출하는 편이 책임 경계가 맞다.

변경:

- `AbilityWork.ConstructAbilityWork`에 `IFacilityCandidateCache` 인자를 추가했다.
- `AbilityWork.MarkFacilityDynamicStateDirty()`를 추가해 cache dirty 호출을 주입 서비스로 모았다.
- `AbilityWork.SetWorkPriority`와 `StopAssignedWork`의 직접 static 호출을 helper 호출로 교체했다.
- `WorkTaskExecutor.ReturnCarriedStock`을 instance method로 바꾸고, 재고 반환 뒤 cache dirty를 `work.MarkFacilityDynamicStateDirty()`로 전달했다.
- `WorkDutyController.SetDutyState`도 `work.MarkFacilityDynamicStateDirty()`를 사용하게 했다.

효과:

- `AbilityWork.cs`, `WorkTaskExecutor.cs`, `WorkDutyController.cs` 안에는 더 이상 `FacilityCandidateCache.MarkDynamicStateDirty()` static facade 호출이 없다.
- 작업 실행과 근무 상태 변경이 AI 시설 후보 cache를 무효화해야 한다는 사실은 유지하되, 구현 의존성은 `AbilityWork`의 VContainer 주입 경계로 모였다.
- `AbilityWork.cs`는 666 lines / 89 declarations, `WorkTaskExecutor.cs`는 490 lines / 28 declarations, `WorkDutyController.cs`는 462 lines / 33 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6125 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache.MarkDynamicStateDirty call remains in AbilityWork.cs, WorkTaskExecutor.cs, or WorkDutyController.cs
- AbilityWork.ConstructAbilityWork includes IFacilityCandidateCache
- WorkTaskExecutor and WorkDutyController route dirty signaling through AbilityWork.MarkFacilityDynamicStateDirty
```

### 112. Shop inherited cache dirty DI 분리

대상:

- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Buildings/Shop.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `Shop`은 구매, 절도, 계산 대기, 보충, 직원 배치, 재고 초기화처럼 AI 시설 후보 cache에 영향을 주는 상태 변경이 많다.
- 하지만 `Shop`도 `BuildableObject`의 하위 시설이므로 cache service를 별도 중복 주입하기보다, `BuildableObject`가 가진 VContainer 주입 helper를 상속 경계로 노출하는 편이 책임이 더 작다.
- `BuildableObject`의 cache helper는 구현 service를 private resolver로 숨기고, 하위 class에는 dirty intent만 제공하면 된다.

변경:

- `BuildableObject.MarkFacilityDynamicStateDirty()`를 `private`에서 `protected`로 바꿨다.
- `Shop` 안의 모든 `FacilityCandidateCache.MarkDynamicStateDirty()` 호출을 `MarkFacilityDynamicStateDirty()` 호출로 교체했다.
- `#if UNITY_EDITOR` 안의 debug stock clear도 같은 inherited helper를 사용하게 했다.

효과:

- non-Editor runtime scripts 전체에서 `FacilityCandidateCache.MarkDynamicStateDirty()`와 `FacilityCandidateCache.GetCandidates()` 직접 호출이 사라졌다.
- 상점의 재고/계산/직원 상태 변경은 여전히 후보 cache를 무효화하지만, cache 구현 의존성은 `BuildableObject`의 VContainer 주입 경계로 모였다.
- `Shop.cs`는 959 lines / 78 declarations, `BuildableObject.cs`는 550 lines / 44 declarations 상태를 유지한다.
- 자동 감사 요약은 307 script files, 6125 extracted declarations 상태를 유지한다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache.MarkDynamicStateDirty call remains in Shop.cs
- no FacilityCandidateCache.MarkDynamicStateDirty/GetCandidates direct call remains in non-Editor runtime scripts
- Shop calls inherited MarkFacilityDynamicStateDirty from BuildableObject
```

### 113. Character AI facility lookup brain DI 분리

대상:

- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 이전 단계에서는 `CharacterAiDecisionPipeline`이 `ConfigureFacilityLookup()`으로 composition root가 넣어준 `ICharacterAiFacilityLookup` static slot을 보관했다.
- 이 방식은 scene lookup을 service로 감싼 점은 좋지만, behavior-task pipeline 자체가 runtime service lifecycle을 static state로 들고 있어 테스트와 씬 전환 경계가 흐려진다.
- 모든 pipeline macro target lookup은 `CharacterActor`를 받으므로, 이미 VContainer 주입 대상인 `AIBrain`에 facility lookup을 주입하고 pipeline은 actor brain에서 요구하는 편이 책임 경계가 더 선명하다.

변경:

- `AIBrain.ConstructAIBrain`에 `ICharacterAiFacilityLookup` 인자를 추가했다.
- `AIBrain.RequireFacilityLookup()`을 추가해 주입 누락 시 fallback 없이 명시 예외를 내게 했다.
- `CharacterAiDecisionPipeline.ConfigureFacilityLookup()` / `ClearFacilityLookup()` static lifecycle API와 내부 static field를 제거했다.
- `ApplyAvoidFacility`와 `RunVandalizeMacro`의 target lookup은 `FindFacility(actor, id, tag)`를 거쳐 `actor.Brain.RequireFacilityLookup()`으로 수행하게 했다.
- `DungeonRuntimeLifetimeScope`에서 pipeline facility lookup configure/clear 호출을 제거했다.

효과:

- `CharacterAiDecisionPipeline`은 더 이상 `ICharacterAiFacilityLookup` service instance를 static으로 보관하지 않는다.
- macro target lookup 의존성은 character AI brain의 VContainer 주입 경계로 드러났다.
- scene character injection, pooled customer spawn, owner spawn 모두 component tree를 resolver로 inject 하므로 새 `AIBrain` 의존성도 같은 경로로 주입된다.
- `AIBrain.cs`는 1271 lines / 61 declarations, `CharacterAiDecisionPipeline.cs`는 713 lines / 39 declarations, `DungeonRuntimeLifetimeScope.cs`는 464 lines / 6 declarations가 됐다.
- 자동 감사 요약은 307 script files, 6124 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no CharacterAiDecisionPipeline.ConfigureFacilityLookup or ClearFacilityLookup call remains
- AIBrain.ConstructAIBrain includes ICharacterAiFacilityLookup
- CharacterAiDecisionPipeline macro facility lookup routes through actor.Brain.RequireFacilityLookup()
- scene and spawned character component injection paths include AIBrain
```

### 114. Character AI job giver catalog DI 분리

대상:

- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/AI/CharacterAiBehaviorTasks.cs`
- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`
- `Assets/Scripts/Character/AI/CharacterAiJobGiver.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiJobGiverRegistry.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterAiJobGiverRegistry`가 런타임 static catalog로 남아 있으면 BT task와 decision pipeline이 전역 상태에 직접 기대게 된다.
- JobGiver는 “BT가 큰 행동 후보를 고르고, 그 가지 안에서 utility가 세부 후보를 점수화한다”는 AI 구조의 핵심 정책 테이블이므로 composition root에서 서비스로 등록되는 편이 책임 경계가 선명하다.
- Editor debug scenario는 아직 static registry 이름을 참조하므로 런타임 static을 제거하되 Editor 폴더에 얇은 호환 어댑터만 남겼다.

변경:

- `ICharacterAiJobGiverCatalog`와 `CharacterAiJobGiverCatalog`를 추가하고 기존 registry의 JobGiver 인스턴스 생성을 catalog 인스턴스 책임으로 옮겼다.
- `DungeonRuntimeLifetimeScope`가 `CharacterAiJobGiverCatalog`를 `ICharacterAiJobGiverCatalog` singleton으로 등록한다.
- `AIBrain.ConstructAIBrain`이 `ICharacterAiJobGiverCatalog`를 주입받고 `RequireJobGiverCatalog()`로 fallback 없이 노출한다.
- `CharacterAiBehaviorTasks`의 JobGiver branch와 action-select task가 static registry 대신 actor brain의 injected catalog를 경유한다.
- `CharacterAiDecisionPipeline.RunMacroGoalDecision`의 SeekFood/SeekToilet/SeekHygiene/SeekFun macro도 actor brain의 catalog에서 JobGiver를 받는다.
- `CharacterAiJobGiverRegistry` 이름은 `Assets/Scripts/Character/AI/Editor` 아래 호환 adapter로만 남겼다.

효과:

- non-Editor runtime scripts에서 `CharacterAiJobGiverRegistry` 직접 참조가 사라졌다.
- BT task가 어떤 행동 branch를 평가할지 결정하고, 선택된 branch 내부의 세부 실행 후보는 주입된 JobGiver catalog와 utility 점수화가 맡는 구조가 코드 경계로 드러났다.
- `AIBrain.cs`는 1281 lines / 62 declarations, `CharacterAiDecisionPipeline.cs`는 724 lines / 40 declarations, `DungeonRuntimeLifetimeScope.cs`는 466 lines / 6 declarations가 됐다.
- 자동 감사 요약은 308 script files, 6158 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no CharacterAiJobGiverRegistry reference remains in non-Editor runtime scripts
- DungeonRuntimeLifetimeScope registers CharacterAiJobGiverCatalog as ICharacterAiJobGiverCatalog
- AIBrain.ConstructAIBrain includes ICharacterAiJobGiverCatalog
- CharacterAiBehaviorTasks resolves JobGiver through actor.Brain.RequireJobGiverCatalog()
- CharacterAiDecisionPipeline macro JobGiver lookup routes through actor.Brain.RequireJobGiverCatalog()
```

### 115. Room facility policy runtime static 호출 제거

대상:

- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Character/AI/FacilityCandidateScorer.cs`
- `Assets/Scripts/Character/AI/FacilityScoringContext.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `IRoomFacilityPolicy`와 `RoomFacilityPolicyService`는 이미 VContainer 서비스로 존재했지만, `BuildableObject`와 `FacilityCandidateScorer`가 static `RoomFacilityPolicy` facade를 직접 호출했다.
- 특히 AI 후보 선택은 방 역할과 시설 역할의 매칭을 판단하므로, scorer가 static policy를 숨겨 읽으면 테스트/씬 경계가 흐려진다.
- `CanVisit()`만으로 대체하면 다중 역할 시설에서 요청 역할과 실제 방 역할이 다를 때 통과할 수 있으므로, requested role 검증은 유지하되 의존성만 명시 주입으로 옮긴다.

변경:

- `BuildableObject.ConstructBuildableObject`에 `IRoomFacilityPolicy`를 추가하고, `CanVisit()`의 방 역할 검증을 injected policy로 수행하게 했다.
- `FacilityScoringContext`가 `ISocialReputationBiasService`와 `IRoomFacilityPolicy`를 함께 보관하게 했다.
- `FacilityCandidateScorer`의 후보 필터와 room utility score 계산이 `FacilityScoringContext`를 통해 room policy를 사용하게 했다.
- `AIBrain.ConstructAIBrain`이 `IRoomFacilityPolicy`를 받아 `FacilityScoringContext`를 생성한다.
- `DungeonRuntimeLifetimeScope`에서 `RoomFacilityPolicy.Configure/ClearConfiguredPolicy` static lifecycle 호출을 제거했다.

효과:

- non-Editor runtime scripts에서 `RoomFacilityPolicy.` 직접 호출이 사라졌다.
- 시설 방문 가능 여부와 AI 시설 후보 점수화가 VContainer 주입 서비스 경계를 공유한다.
- room policy 누락은 fallback 없이 `BuildableObject` 또는 `FacilityScoringContext`의 명시 예외로 드러난다.
- `BuildableObject.cs`는 560 lines / 45 declarations, `FacilityCandidateScorer.cs`는 499 lines / 20 declarations, `FacilityScoringContext.cs`는 86 lines / 10 declarations, `DungeonRuntimeLifetimeScope.cs`는 464 lines / 6 declarations가 됐다.
- 자동 감사 요약은 308 script files, 6164 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no RoomFacilityPolicy direct call remains in non-Editor runtime scripts
- DungeonRuntimeLifetimeScope no longer configures or clears RoomFacilityPolicy static facade
- BuildableObject.ConstructBuildableObject includes IRoomFacilityPolicy
- AIBrain.ConstructAIBrain includes IRoomFacilityPolicy
- FacilityCandidateScorer room role filtering and room utility scoring route through FacilityScoringContext
```

### 116. Facility/room static cache facade lifecycle 제거

대상:

- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `IFacilityCandidateCache`와 `IRoomLayoutCache`는 이미 VContainer singleton service가 상태를 소유하고 있다.
- 앞 단계들에서 non-Editor runtime의 `FacilityCandidateCache.*` / `RoomRegistry.*` 직접 호출은 모두 제거됐지만, composition root가 아직 두 static facade를 configure/clear 하고 있었다.
- 호출자가 없는 static facade를 런타임에서 계속 초기화하면 새 코드가 다시 전역 우회 경로를 선택할 여지를 남긴다.
- Editor debug scenario는 아직 해당 static facade를 호출하지만, 이번 목표 범위에서 Editor 밑 시나리오는 리팩터 대상이 아니다.

변경:

- `DungeonRuntimeLifetimeScope.OnDestroy()`에서 `FacilityCandidateCache.ClearConfiguredCache()`와 `RoomRegistry.ClearConfiguredCache()` 호출을 제거했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`에서 `FacilityCandidateCache.Configure(...)`와 `RoomRegistry.Configure(...)` 호출을 제거했다.
- `IFacilityCandidateCache` / `IRoomLayoutCache` 서비스 등록은 유지했다.

효과:

- non-Editor runtime은 facility 후보 cache와 room layout cache를 static facade lifecycle로 초기화하지 않는다.
- cache 상태의 소유자는 VContainer singleton service로만 남았다.
- `DungeonRuntimeLifetimeScope.cs`는 460 lines / 6 declarations가 됐다.
- 자동 감사 요약은 308 script files, 6164 extracted declarations 상태를 유지한다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache direct call remains in non-Editor runtime scripts
- no RoomRegistry direct call remains in non-Editor runtime scripts
- DungeonRuntimeLifetimeScope no longer calls FacilityCandidateCache.Configure/ClearConfiguredCache
- DungeonRuntimeLifetimeScope no longer calls RoomRegistry.Configure/ClearConfiguredCache
```

### 117. Facility evolution state static lifecycle 제거

대상:

- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `IFacilityEvolutionStateService`는 이미 VContainer singleton service로 등록되어 있고, 시설 진화 runtime/service/query/validator는 이 service를 명시적으로 받는다.
- non-Editor runtime에서 `FacilityEvolutionStateUtility.GetOrAdd` 직접 호출은 이미 제거되어 있었다.
- 그런데 composition root가 아직 `FacilityEvolutionStateUtility.Configure/ClearConfiguredService`를 호출해 static facade lifecycle을 살려두고 있었다.
- 호출자가 없는 static facade를 runtime에서 구성하면, 새 코드가 다시 state 생성 책임을 전역 helper로 우회할 여지를 남긴다.

변경:

- `DungeonRuntimeLifetimeScope.OnDestroy()`에서 `FacilityEvolutionStateUtility.ClearConfiguredService()` 호출을 제거했다.
- `DungeonRuntimeLifetimeScope.InjectSceneComponents()`에서 `FacilityEvolutionStateUtility.Configure(...)` 호출을 제거했다.
- `IFacilityEvolutionStateService` 서비스 등록은 유지했다.

효과:

- non-Editor runtime은 facility-evolution state access를 static lifecycle로 초기화하지 않는다.
- state component 생성/초기화 책임은 VContainer로 등록된 `IFacilityEvolutionStateService`에만 남는다.
- `DungeonRuntimeLifetimeScope.cs`는 458 lines / 6 declarations가 됐다.
- 자동 감사 요약은 308 script files, 6164 extracted declarations 상태를 유지한다.

검증:

```text
Targeted source scan:
- no FacilityEvolutionStateUtility direct call remains in non-Editor runtime scripts
- DungeonRuntimeLifetimeScope no longer calls FacilityEvolutionStateUtility.Configure/ClearConfiguredService
- IFacilityEvolutionStateService remains registered in DungeonRuntimeLifetimeScope
```

### 118. TMP Korean font UI component DI 전환

대상:

- `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `TMPKoreanFont`는 아직 runtime UI factory/panel 다수가 공유하는 compatibility facade라 한 번에 제거하면 변경 범위가 과하게 커진다.
- 그러나 `OwnerSelectionPanel`, `BuildingSummaryInfo`, `CharacterSummeryInfo`는 이미 VContainer scene injection 대상이므로 직접 `ITmpKoreanFontService`를 받을 수 있다.
- 주입 가능한 UI에서 static facade를 계속 호출하면, 새 UI 코드가 전역 폰트 helper에 기대는 패턴을 유지하게 된다.

변경:

- 세 UI 컴포넌트의 `[Inject]` construct method에 `ITmpKoreanFontService`를 추가했다.
- `TMPKoreanFont.Apply` / `ApplyToChildren` 직접 호출을 `RequireTmpKoreanFontService().Apply...`로 바꿨다.
- 각 컴포넌트에 주입 누락 시 명시 예외를 던지는 `RequireTmpKoreanFontService()`를 추가했다.
- 감사 문서의 함수 목록, 라인 수, 선언 수를 갱신했다.

효과:

- owner 선택 UI, 건물 정보 팝업, 캐릭터 정보 팝업은 더 이상 TMP Korean font static facade를 직접 호출하지 않는다.
- font 적용 책임은 VContainer로 등록된 `ITmpKoreanFontService`를 통해 명시적으로 들어온다.
- 자동 감사 요약은 308 script files, 6167 extracted declarations가 됐다.

검증:

```text
Targeted source scan:
- no TMPKoreanFont.Apply/ApplyToChildren direct call remains in OwnerSelectionPanel.cs
- no TMPKoreanFont.Apply/ApplyToChildren direct call remains in BuildingSummaryInfo.cs
- no TMPKoreanFont.Apply/ApplyToChildren direct call remains in CharacterSummeryInfo.cs
- remaining TMPKoreanFont static calls are outside this targeted UI tranche
```

### 119. TMP Korean font runtime factory DI 전환

대상:

- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`
- `Assets/Scripts/Meta/RunResultPanel.cs`
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`
- `Assets/Scripts/UI/NoticeFeed.cs`
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Offense/OffensePanelUiFactory.cs`
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 118번에서 scene UI 3종은 전환했지만, generated UI/factory 경로에는 여전히 `TMPKoreanFont.Apply` 직접 호출이 남아 있었다.
- `UITabManager`, `StaffWorkPriorityPanel`, `GridConstructTab`, 캐릭터 feedback/dialogue, `NoticeFeed`는 VContainer injection 경계가 있거나 scene injection 목록에 추가할 수 있다.
- run-result, event-alert, offense panel은 service/factory가 이미 VContainer로 생성되므로 font service를 생성 인자와 bind 인자로 명시 전달하는 쪽이 책임 분리가 분명하다.

변경:

- 위 runtime UI 컴포넌트와 factory 경로에 `ITmpKoreanFontService`를 명시 주입/전달했다.
- `NoticeFeed`를 `DungeonRuntimeLifetimeScope` scene injection 목록에 추가했다.
- `EventAlertViewPresenterFactory`가 `ITmpKoreanFontService`를 받아 `EventAlertViewPresenter`, `EventAlertChoicePresenter`, `EventAlertUiFactory`로 전달하게 했다.
- `OffensePanelService`가 generated offense panels에 font service를 넘기고, offense UI factory는 더 이상 font static facade를 호출하지 않는다.
- 당시 `RunResultPanel.CreateDefaultPanel`은 font service 인자를 필수로 받게 했다. 이후 129번에서 이 생성 책임은 `IRunResultPanelFactory`로 이동했다.

효과:

- targeted runtime UI/factory 파일에서 `TMPKoreanFont.Apply` / `ApplyToChildren` 직접 호출이 사라졌다.
- non-Editor runtime 검색 기준 남은 `TMPKoreanFont.Apply` 직접 호출은 Codex/Synthesis/FacilityEvolution default panel 생성 함수 3개뿐이며, 현재 호출자는 Editor debug scenario다.
- `DungeonRuntimeLifetimeScope`의 `TMPKoreanFont.Configure/ClearConfiguredService`는 아직 compatibility facade lifecycle로 남아 있다.
- 자동 감사 요약은 308 script files, 6177 extracted declarations가 됐다.

검증:

```text
Targeted source scan:
- no TMPKoreanFont.Apply/ApplyToChildren direct call remains in UITabManager, StaffWorkPriorityPanel, GridConstructTab, CharacterFeedbackBubble, CharacterDialogueRuntime, RunResultPanel, NoticeFeed, EventAlertUiFactory, or OffensePanelUiFactory
- RunResultPanel generated UI creation requires ITmpKoreanFontService through IRunResultPanelFactory; offense generated panel creation requires ITmpKoreanFontService through IOffensePanelFactory
- EventAlert UI creation receives ITmpKoreanFontService through EventAlertViewPresenterFactory
- remaining non-Editor TMPKoreanFont.Apply direct calls are in CodexPanel, FacilityEvolutionPanel, and FacilitySynthesisPanel default-panel helpers
```

### 120. TMP Korean font runtime static facade 제거

대상:

- `Assets/Scripts/Codex/UI/CodexPanel.cs`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`
- `Assets/Scripts/UI/Editor/TMPKoreanFontEditorResolver.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 119번 이후 남은 non-Editor `TMPKoreanFont.Apply` 호출 3개는 모두 Editor debug scenario에서만 호출하는 default panel helper였다.
- helper가 static font facade를 직접 호출하면, 실제 런타임이 아니어도 non-Editor 코드에 전역 font 의존이 계속 남는다.
- `DungeonRuntimeLifetimeScope`가 static facade를 Configure/Clear하는 구조는 VContainer 등록과 별개로 전역 상태 lifecycle을 유지한다.

변경:

- Codex/Synthesis/FacilityEvolution default panel helper가 `ITmpKoreanFontService`를 필수 인자로 받게 했다.
- Editor debug scenario는 `TMPKoreanFontEditorResolver.CreateService()`로 명시 font service를 만들어 넘긴다.
- `DungeonRuntimeLifetimeScope`에서 `TMPKoreanFont.Configure` / `ClearConfiguredService` 호출을 제거했다.
- `TMPKoreanFont` static class는 Editor/legacy compatibility facade로만 남긴다.

효과:

- non-Editor runtime scripts 기준 `TMPKoreanFont.` 직접 호출이 0개가 됐다.
- TMP Korean font 적용 책임은 runtime에서는 VContainer 등록 서비스, Editor scenario에서는 Editor resolver service로 분리된다.
- 자동 감사 요약은 308 script files, 6178 extracted declarations가 됐다.

검증:

```text
Targeted source scan:
- no TMPKoreanFont. direct call remains in Assets/Scripts excluding Editor folders
- no old CreateDefaultPanel(runtime-only) default panel helper call remains in non-Editor runtime scripts
- DungeonRuntimeLifetimeScope no longer configures or clears TMPKoreanFont static facade
```

### 121. Character AI decision pipeline service DI

대상:

- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`
- `Assets/Scripts/Character/AI/CharacterAiBehaviorTasks.cs`
- `Assets/Scripts/Character/AI/AIBrain.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- Behavior Designer task들이 `CharacterAiDecisionPipeline.*` static method를 직접 호출하면, BT 실행 경로가 DI 컨테이너에 등록된 정책 교체 지점을 우회한다.
- 파이프라인 내부의 facility lookup/job giver catalog 의존성은 이미 actor brain을 통해 주입 서비스로 이동했으므로, 다음 단계는 task entry point 자체를 instance service로 닫는 것이다.
- 시설 태그 매칭 같은 순수 판정 함수는 상태나 컨테이너를 들고 있지 않아 static utility로 남겨도 결합 위험이 낮다.

변경:

- `ICharacterAiDecisionPipeline`을 추가하고 `CharacterAiDecisionPipeline`을 VContainer singleton service로 등록했다.
- `AIBrain`이 `ICharacterAiDecisionPipeline`을 주입받고 `RequireDecisionPipeline()`으로 BT task에 노출한다.
- `CharacterAiBehaviorTasks`는 actor brain에서 decision pipeline service를 가져와 모든 macro/critical/selection/idle 실행을 호출한다.
- non-Editor direct `CharacterAiDecisionPipeline.` 호출은 `CharacterAiFacilityLookup`의 순수 `MatchesFacility` 사용만 남겼다.

효과:

- BT task가 행동 결정 정책을 static class가 아니라 actor brain에 주입된 서비스 경계로 호출한다.
- 테스트/디버그에서 decision pipeline 자체를 대체하거나 계측할 수 있는 VContainer seam이 생겼다.
- 자동 감사 요약은 308 script files, 6199 extracted declarations가 됐다.

검증:

```text
Targeted source scan:
- only CharacterAiDecisionPipeline.MatchesFacility remains as a direct non-Editor CharacterAiDecisionPipeline. call
- DungeonRuntimeLifetimeScope registers CharacterAiDecisionPipeline as ICharacterAiDecisionPipeline
- AIBrain receives and exposes ICharacterAiDecisionPipeline through VContainer injection
- CharacterAiBehaviorTasks resolves ICharacterAiDecisionPipeline through actor.Brain.RequireDecisionPipeline()
```

### 122. Runtime static facade definition Editor 경계 이동

대상:

- `Assets/Scripts/Character/AI/FacilityCandidateCache.cs`
- `Assets/Scripts/Character/AI/Editor/FacilityCandidateCacheEditorFacade.cs`
- `Assets/Scripts/Rooms/RoomRegistry.cs`
- `Assets/Scripts/Rooms/RoomFacilityPolicy.cs`
- `Assets/Scripts/Rooms/Editor/RoomEditorFacades.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs`
- `Assets/Scripts/UI/TMPKoreanFont.cs`
- `Assets/Scripts/UI/Editor/TMPKoreanFontEditorResolver.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 120번과 121번까지 진행한 뒤 non-Editor runtime 호출자는 사라졌지만, 런타임 파일 안에 static compatibility facade 정의 자체가 남아 있었다.
- 런타임 파일에 static facade 정의가 남으면 실제 호출자가 없어도 다음 작업자가 다시 전역 접근을 붙이기 쉽고, VContainer 경계가 애매해진다.
- 남은 사용자는 Editor debug scenario뿐이므로, 호환 wrapper는 Editor 폴더로 이동시키고 런타임 파일은 service/interface 구현만 가지는 편이 책임 분리가 분명하다.

변경:

- `FacilityCandidateCache`, `RoomRegistry`, `RoomFacilityPolicy` static wrapper를 Editor 전용 facade 파일로 이동했다.
- `FacilityEvolutionStateUtility`와 `TMPKoreanFont` runtime static facade 정의를 제거했다.
- `TMPKoreanFontEditorResolver`의 `InitializeOnLoad` 기반 static override 등록을 제거하고, Editor scenario가 명시적으로 `CreateService()`를 호출하는 형태만 남겼다.
- 감사 문서의 파일 수/선언 수와 해당 파일별 선언 목록을 `310 script files`, `6177 extracted declarations` 기준으로 갱신했다.

효과:

- non-Editor runtime scripts 기준 `FacilityCandidateCache.`, `RoomRegistry.`, `RoomFacilityPolicy.`, `FacilityEvolutionStateUtility.`, `TMPKoreanFont.` 직접 호출이 0개다.
- non-Editor runtime scripts 기준 위 static facade class 정의도 0개다.
- Editor debug scenario 호환은 Editor 폴더의 wrapper가 담당하므로, 런타임 DI 경계와 Editor 테스트 편의 경계가 분리됐다.

검증:

```text
Targeted source scan:
- no FacilityCandidateCache./RoomRegistry./RoomFacilityPolicy./FacilityEvolutionStateUtility./TMPKoreanFont. direct call remains in Assets/Scripts excluding Editor folders
- no public static class FacilityCandidateCache/RoomRegistry/RoomFacilityPolicy/FacilityEvolutionStateUtility/TMPKoreanFont remains in Assets/Scripts excluding Editor folders
- remaining FacilityCandidateCache/RoomRegistry/RoomFacilityPolicy static calls are all under Editor folders
```

### 123. Invasion intruder 생성 책임 factory 소유로 축소

대상:

- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 91번에서 `IInvasionIntruderFactory`를 추가했지만, `InvasionDirectorRuntime.TrySpawnIntruder`가 여전히 prefab instantiate, prefab-less GameObject 생성, 위치 지정, runtime 보장을 직접 수행하고 있었다.
- `InvasionIntruderRuntime.Begin`도 `CharacterActor`와 `AbilityMove`가 없으면 직접 `AddComponent`로 보강해서, 실제 생성 계약이 factory와 runtime에 중복되어 있었다.
- 현재 `SampleScene`의 `intruderPrefab`은 비어 있으므로 prefab-less 생성 경로 자체를 제거하면 기존 침입 이벤트가 즉시 실패할 수 있다. 대신 그 경로를 factory의 명시 책임으로 몰아넣는 쪽이 안전하고 책임 경계도 선명하다.

변경:

- `IInvasionIntruderFactory.Create(GameObject intruderPrefab, Vector3 position)`를 추가했다.
- `InvasionDirectorRuntime`은 `Create(...)`만 호출하고 더 이상 `Instantiate`, `new GameObject`, `AddComponent` 계열 생성 책임을 갖지 않는다.
- `InvasionIntruderRuntime.Begin`은 `RequireRuntimeComponents()`로 구성요소 존재만 검증하고, 누락 컴포넌트를 몰래 추가하지 않는다.
- `InvasionIntruderRuntimeFactory`가 prefab instantiate, prefab-less 생성, visual/actor/move/collider/runtime component 보장을 단일 책임으로 가진다.

효과:

- `InvasionIntruderSystem.cs`에서 runtime object creation 호출이 사라졌다.
- 침입자 생성/구성 책임은 VContainer에 등록된 `IInvasionIntruderFactory` 구현으로 집중됐다.
- 감사 요약은 310 script files, 6179 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no new GameObject / AddComponent< / Instantiate call remains in InvasionIntruderSystem.cs
- all remaining intruder creation/component composition calls are in InvasionIntruderFactory.cs
- DungeonRuntimeLifetimeScope still registers InvasionIntruderRuntimeFactory as IInvasionIntruderFactory
```

### 124. Defense status runtime component service DI

대상:

- `Assets/Scripts/Defense/DefenseFacilitySystem.cs`
- `Assets/Scripts/Defense/DefenseStatusRuntimeService.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `DefenseStatusRuntime.GetOrAdd` 정적 helper가 방어 효과 적용 중 `AddComponent<DefenseStatusRuntime>`를 숨겨 실행하고 있었다.
- 침입자 이동/방어 함정 처리 경로가 VContainer로 실행되더라도, 상태 컴포넌트 lifecycle은 정적 helper에 남아 있어 테스트와 런타임 생성 책임이 섞였다.
- 방어 시스템 파일은 효과 판정/상태 tick을 맡고, 실제 컴포넌트 생성은 명시 service/factory 경계가 맡는 편이 책임이 선명하다.

변경:

- `IDefenseStatusRuntimeService` / `DefenseStatusRuntimeService`를 추가했다.
- `DefenseFacilityResolver.TriggerAt`, `DefenseEffectResolver.ApplyEffects`, `DefenseEffectResolver.TickStatuses`가 `IDefenseStatusRuntimeService`를 명시 인자로 받게 했다.
- `InvasionDirectorRuntime`은 VContainer로 `IDefenseStatusRuntimeService`를 주입받고, 생성한 `InvasionIntruderRuntime`에 전달한다.
- `InvasionIntruderRuntime`은 방어 함정 발동과 status tick에서 주입받은 service를 사용한다.
- `DungeonRuntimeLifetimeScope`에 `DefenseStatusRuntimeService`를 singleton으로 등록했다.
- Editor 방어 시나리오는 compile 호환을 위해 직접 만든 `DefenseStatusRuntimeService`를 전달한다.

효과:

- `DefenseFacilitySystem.cs`에서 `AddComponent<DefenseStatusRuntime>`와 정적 `DefenseStatusRuntime.GetOrAdd`가 제거됐다.
- 당시 기준으로 `AddComponent<DefenseStatusRuntime>`는 `DefenseStatusRuntimeService.cs` 한 곳에만 남아, 방어 상태 컴포넌트 생성 책임이 DI 경계로 모였다.
- 이후 166번에서 이 생성 책임은 다시 `DefenseStatusRuntimeFactory.cs`로 분리됐다.
- 감사 요약은 311 script files, 6186 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no DefenseStatusRuntime.GetOrAdd call or definition remains in Assets/Scripts
- AddComponent<DefenseStatusRuntime> remained only in DefenseStatusRuntimeService.cs at this step.
- Current owner after step 166: DefenseStatusRuntimeFactory.cs
- no new GameObject / AddComponent< / Instantiate call remains in DefenseFacilitySystem.cs
- DungeonRuntimeLifetimeScope registers DefenseStatusRuntimeService as IDefenseStatusRuntimeService
```

### 125. Owner character 생성 책임 factory 소유로 축소

대상:

- `Assets/Scripts/Character/Core/OwnerRunManager.cs`
- `Assets/Scripts/Character/Core/OwnerCharacterFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `OwnerRunManager`가 사장 선택/런 종료 상태 관리와 함께 prefab instantiate, prefab-less GameObject 생성, 필수 컴포넌트 보강, visual 생성, VContainer component injection까지 직접 수행하고 있었다.
- 이 구조에서는 런 생명주기 관리자와 캐릭터 생성 factory 책임이 한 파일에 섞여, 사장 생성 규칙 변경이 manager 상태 흐름을 건드리게 된다.
- 이미 침입자 생성은 factory로 분리했으므로, 사장 캐릭터 생성도 같은 명시 factory 경계로 맞추는 편이 런타임 객체 생성 책임을 일관되게 만든다.

변경:

- `IOwnerCharacterFactory` / `OwnerCharacterFactory`를 추가했다.
- `OwnerCharacterFactory`가 owner prefab instantiate, prefab-less 생성, `BehaviorTree`/`CharacterActor`/`AIBrain`/`AbilityMove`/`AbilityWork` 보장, `Visual`/`SpriteRenderer` 보장, spawn position 계산, 컴포넌트 injection, owner 초기화를 전담한다.
- `OwnerRunManager`는 `IOwnerCharacterFactory`를 주입받아 `CreateOwner(...)`만 호출하게 했다.
- `DungeonRuntimeLifetimeScope`에 `OwnerCharacterFactory`를 singleton으로 등록했다.

효과:

- `OwnerRunManager.cs`에서 `new GameObject`, `AddComponent<...>`, `Instantiate(...)`, `IObjectResolver`, `IGridSystemProvider`, `BehaviorTree` 직접 생성/보강 의존이 제거됐다.
- 사장 선택/런 종료 책임과 사장 캐릭터 생성/구성 책임이 분리됐다.
- 감사 요약은 312 script files, 6192 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no new GameObject / AddComponent< / Instantiate / BehaviorTree / IObjectResolver / IGridSystemProvider remains in OwnerRunManager.cs
- owner object creation/component composition calls are centralized in OwnerCharacterFactory.cs
- DungeonRuntimeLifetimeScope registers OwnerCharacterFactory as IOwnerCharacterFactory
```

### 126. Character BehaviorTree runtime configurator DI

대상:

- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`
- `Assets/Scripts/Character/AI/CharacterBehaviorTreeRuntimeConfigurator.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterAiScheduler`는 AI 실행 cadence와 예산을 관리하는 컴포넌트인데, 캐릭터에 `BehaviorTree`가 없으면 직접 `AddComponent<BehaviorTree>`로 보강하고 외부 BT까지 설정하고 있었다.
- BT 컴포넌트 생성/외부 behavior 연결/런타임 enable 보장은 캐릭터 AI 실행 준비 책임이며, 스케줄러의 순회/예산/수동 tick 책임과 분리하는 편이 디버깅 경계가 선명하다.
- BehaviorManager 수동 tick 설정은 여전히 스케줄러 cadence 책임으로 남기되, 캐릭터별 BT 구성은 주입 서비스가 담당하게 한다.

변경:

- `ICharacterBehaviorTreeRuntimeConfigurator` / `CharacterBehaviorTreeRuntimeConfigurator`를 추가했다.
- configurator가 캐릭터 `BehaviorTree` 컴포넌트 보장, `ExternalBehaviorTree` 할당, `StartWhenEnabled`/runtime enable/visual status refresh를 전담한다.
- `CharacterAiScheduler`는 configurator를 VContainer로 주입받아 `ConfigureCharacterBehaviorTree`에서 서비스만 호출한다.
- `CharacterAiScheduler.cs`의 누락된 `using System;`도 보강해 주입 null-check 예외 타입 참조를 명확히 했다.
- `DungeonRuntimeLifetimeScope`에 configurator singleton 등록을 추가했다.

효과:

- `CharacterAiScheduler.cs`에서 `AddComponent<BehaviorTree>` 직접 호출이 제거됐다.
- 캐릭터별 BT 생성/초기화 책임은 `ICharacterBehaviorTreeRuntimeConfigurator` 경계로 집중됐다.
- 감사 요약은 313 script files, 6197 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no AddComponent<BehaviorTree> / new GameObject / Instantiate remains in CharacterAiScheduler.cs
- AddComponent<BehaviorTree> remains only in CharacterBehaviorTreeRuntimeConfigurator.cs for the scheduled character BT setup path
- DungeonRuntimeLifetimeScope registers CharacterBehaviorTreeRuntimeConfigurator as ICharacterBehaviorTreeRuntimeConfigurator
```

### 127. Character feedback bubble component service DI

대상:

- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterActor`가 런타임 상태 보장 과정에서 `CharacterFeedbackBubble` 컴포넌트를 직접 `AddComponent`로 생성하고 있었다.
- `CharacterFeedbackBubble`은 `ICharacterAiSchedulingService`, `ITmpKoreanFontService`를 VContainer로 주입받는 UI 피드백 컴포넌트라, actor가 직접 붙이면 주입 누락과 actor core/UI 생성 책임 혼합 위험이 생긴다.
- 캐릭터 core는 자기 상태와 능력 연결을 담당하고, 피드백 bubble 생성/주입은 별도 서비스 경계가 담당하는 편이 책임 위치가 맞다.

변경:

- `ICharacterFeedbackBubbleService` / `CharacterFeedbackBubbleService`를 추가했다.
- 당시 기준으로 서비스가 actor의 `CharacterFeedbackBubble` 컴포넌트 존재를 보장하고, 새로 붙인 경우 즉시 `IObjectResolver.Inject(...)`로 VContainer 주입을 수행했다.
- 이후 167번에서 컴포넌트 생성/주입 책임은 `CharacterFeedbackBubbleFactory`로 분리됐다.
- `CharacterActor`는 `ICharacterFeedbackBubbleService`를 주입받아, 주입된 경우에만 feedback bubble 보장 요청을 하게 했다.
- `DungeonRuntimeLifetimeScope`에 `CharacterFeedbackBubbleService` singleton 등록을 추가했다.

효과:

- `CharacterActor.cs`에서 `AddComponent<CharacterFeedbackBubble>` 직접 호출이 제거됐다.
- 당시 기준으로 feedback UI 컴포넌트 생성과 주입 책임이 `ICharacterFeedbackBubbleService` 경계로 집중됐다.
- 이후 167번에서 서비스는 `ICharacterFeedbackBubbleFactory`에 위임하는 정책 경계로 축소됐다.
- 감사 요약은 314 script files, 6202 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- AddComponent<CharacterFeedbackBubble> remained only in CharacterFeedbackBubbleService.cs at this step.
- Current owner after step 167: CharacterFeedbackBubbleFactory.cs
- CharacterActor receives ICharacterFeedbackBubbleService through VContainer method injection
- DungeonRuntimeLifetimeScope registers CharacterFeedbackBubbleService as ICharacterFeedbackBubbleService
```

### 128. Character social memory component service DI

대상:

- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterActor`가 런타임 상태 보장 중 `CharacterSocialMemory`를 직접 `AddComponent`로 붙이고 있었다.
- `SocialReputationRuntime`도 소문 적용/전파/actor 등록 과정에서 static `EnsureMemory`로 같은 컴포넌트를 직접 생성했다.
- 소셜 메모리는 소문/평판 도메인의 런타임 상태 컴포넌트이므로, actor core와 평판 런타임 양쪽에 생성 책임이 흩어지면 주입/바인딩 정책이 두 군데로 갈라진다.

변경:

- `ICharacterSocialMemoryService` / `CharacterSocialMemoryService`를 추가했다.
- 서비스가 `CharacterSocialMemory` 컴포넌트 보장, VContainer injection, actor binding을 전담한다.
- `CharacterActor`는 `ICharacterSocialMemoryService`를 주입받아 social memory 보장을 서비스에 위임한다. 주입 전 경로에서는 `[RequireComponent]`로 존재하는 기존 컴포넌트만 읽는다.
- `SocialReputationRuntime`은 `ICharacterSocialMemoryService`를 주입받고, static `EnsureMemory`를 instance DI 서비스 호출로 바꿨다.
- `DungeonRuntimeLifetimeScope`에 `CharacterSocialMemoryService` singleton 등록을 추가했다.

효과:

- 당시 기준으로 `AddComponent<CharacterSocialMemory>` 직접 호출은 `CharacterSocialMemoryService.cs` 한 곳으로 집중됐다.
- 이후 167번에서 컴포넌트 생성/주입/바인딩 책임은 `CharacterSocialMemoryFactory`로 분리됐다.
- actor core의 런타임 상태 캐시와 소문/평판 메모리 생성/바인딩 책임이 분리됐다.
- 감사 요약은 315 script files, 6208 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- AddComponent<CharacterSocialMemory> remained only in CharacterSocialMemoryService.cs at this step.
- Current owner after step 167: CharacterSocialMemoryFactory.cs
- CharacterActor receives ICharacterSocialMemoryService through VContainer method injection
- SocialReputationRuntime receives ICharacterSocialMemoryService and no longer owns static memory creation
- DungeonRuntimeLifetimeScope registers CharacterSocialMemoryService as ICharacterSocialMemoryService
```

### 129. Run result panel factory DI

대상:

- `Assets/Scripts/Meta/RunResultPanel.cs`
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `RunResultPanel`은 결과 텍스트를 렌더링하는 MonoBehaviour인데, static `CreateDefaultPanel`에서 canvas/panel/text 오브젝트 생성, TMP 폰트 적용, 자기 자신 `AddComponent`까지 직접 수행하고 있었다.
- 이미 `IRunResultPanelService`가 VContainer로 등록되어 run result 표시 흐름을 담당하므로, 기본 UI 생성도 panel 타입이 아니라 service/factory 경계가 맡는 편이 책임 위치가 맞다.
- MonoBehaviour 내부 static factory는 DI 주입과 생성 책임을 우회하기 쉬워, 다른 UI 패널과 같은 방식으로 factory/service 쪽에 모으는 것이 안전하다.

변경:

- `IRunResultPanelFactory` / `RunResultPanelFactory`를 추가했다.
- `RunResultPanelFactory`가 기본 결과 패널 canvas/panel/text 생성, TMP Korean font 적용, `RunResultPanel` 컴포넌트 생성, VContainer injection을 전담한다.
- `RunResultPanelService`는 `ITmpKoreanFontService` 대신 `IRunResultPanelFactory`를 주입받고, 씬에 기존 패널이 없을 때 factory를 호출한다.
- `RunResultPanel`에서는 static `CreateDefaultPanel`을 제거해 렌더링/숨김 책임만 남겼다.
- `DungeonRuntimeLifetimeScope`에 `RunResultPanelFactory`를 singleton으로 등록했다.

효과:

- `RunResultPanel.cs`에서 `new GameObject`, `AddComponent`, UI 조립, TMP 폰트 적용 책임이 제거됐다.
- run result UI 생성 책임이 VContainer 등록 factory 경계로 이동했다.
- 감사 요약은 315 script files, 6211 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no RunResultPanel.CreateDefaultPanel call or definition remains
- RunResultPanel.cs no longer contains new GameObject / AddComponent / UnityEngine.UI usage
- DungeonRuntimeLifetimeScope registers RunResultPanelFactory as IRunResultPanelFactory
```

### 130. Offense generated panel factory DI

대상:

- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `OffenseWorldMapPanel`과 `OffenseExpeditionPanel`이 static `CreateDefaultPanel`로 canvas/panel/text/root 생성과 자기 자신 `AddComponent`를 직접 수행하고 있었다.
- 두 패널은 런타임 표시/버튼 렌더링/선택 처리 책임을 갖고 있으므로, 기본 패널 shell 생성은 이미 존재하는 offense service 경계로 옮기는 편이 책임 분리가 명확하다.
- `OffensePanelService`가 VContainer 경로로 패널 표시를 담당하므로, 생성 책임은 별도 `IOffensePanelFactory`로 주입받게 하는 것이 자연스럽다.

변경:

- `IOffensePanelFactory` / `OffensePanelFactory`를 추가했다.
- `OffensePanelFactory`가 world-map/expedition 기본 canvas, panel, header/detail text, button root 생성과 panel component 생성, VContainer injection을 전담한다.
- `OffensePanelService`는 `IOffensePanelFactory`를 주입받아 씬에 기존 패널이 없을 때 factory를 호출한다.
- `OffenseWorldMapPanel`, `OffenseExpeditionPanel`에서는 static `CreateDefaultPanel`을 제거하고, 생성된 뷰 참조를 받는 `BindGeneratedView(...)`만 남겼다.
- `DungeonRuntimeLifetimeScope`에 `OffensePanelFactory` singleton 등록을 추가했다.

효과:

- offense 패널 타입에서 기본 panel shell 생성 책임이 제거됐다.
- panel 표시 흐름은 `IOffensePanelService`, panel shell 생성은 `IOffensePanelFactory`, 실제 렌더링은 각 panel MonoBehaviour로 나뉘었다.
- 감사 요약은 315 script files, 6218 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no OffenseWorldMapPanel.CreateDefaultPanel / OffenseExpeditionPanel.CreateDefaultPanel call or definition remains
- OffenseWorldMapPanel and OffenseExpeditionPanel expose BindGeneratedView for generated view references
- DungeonRuntimeLifetimeScope registers OffensePanelFactory as IOffensePanelFactory
```

### 131. Runtime generated panel factory DI

대상:

- `Assets/Scripts/Codex/UI/CodexPanel.cs`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`
- `Assets/Scripts/UI/RuntimePanelFactories.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CodexPanel`, `FacilitySynthesisPanel`, `FacilityEvolutionPanel`이 static `CreateDefaultPanel` 안에서 canvas/panel/text 생성과 자기 자신 `AddComponent`를 직접 수행하고 있었다.
- 세 패널은 이벤트 수신, runtime binding, 표시 텍스트 갱신을 맡는 MonoBehaviour이므로, 기본 shell 생성 책임까지 갖고 있으면 런타임 UI 생성 경계와 DI 주입 경계가 흐려진다.
- editor debug scenario는 여전히 prefab-less 패널 생성이 필요하지만, 그 호출도 panel static 함수가 아니라 명시 factory를 통하게 하는 편이 구조가 일관된다.

변경:

- `ICodexPanelFactory`, `IFacilitySynthesisPanelFactory`, `IFacilityEvolutionPanelFactory`와 구현체를 `RuntimePanelFactories.cs`로 추가했다.
- 세 factory가 기본 overlay canvas, panel, TMP summary text 생성과 생성된 panel component의 VContainer injection을 전담한다.
- 런타임용 factory 생성자는 `[Inject]`로 `IObjectResolver`를 필수화하고, editor debug scenario용 1-인자 생성자는 별도로 둬서 런타임 DI 누락이 조용히 통과하지 않게 했다.
- 세 panel MonoBehaviour에서는 static `CreateDefaultPanel`을 제거하고, 생성된 TMP view 참조를 받는 `BindGeneratedView(TMP_Text)`만 남겼다.
- 세 editor debug scenario는 새 factory를 직접 생성해 테스트용 기본 패널을 만들도록 변경했다.
- `DungeonRuntimeLifetimeScope`에 세 factory singleton 등록을 추가했다.

효과:

- runtime panel 타입에서 기본 panel shell 생성 책임이 제거됐다.
- panel 표시/갱신은 MonoBehaviour, prefab-less UI shell 생성은 factory, dependency composition은 VContainer scope로 역할이 분리됐다.
- 감사 요약은 316 script files, 6241 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- no CodexPanel.CreateDefaultPanel / FacilitySynthesisPanel.CreateDefaultPanel / FacilityEvolutionPanel.CreateDefaultPanel call or definition remains
- RuntimePanelFactories.cs owns generated panel creation and binds generated view references
- DungeonRuntimeLifetimeScope registers CodexPanelFactory, FacilitySynthesisPanelFactory, and FacilityEvolutionPanelFactory
```

### 132. Character dialogue bubble factory DI

대상:

- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`
- `Assets/Scripts/Character/AI/CharacterDialogueBubbleFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterDialogueRuntime`은 캐릭터 로그 이벤트를 감지하고 LLM에 말풍선 문구를 요청하는 런타임인데, 동시에 `new GameObject("CharacterDialogueBubble")`로 월드 텍스트 오브젝트 생성과 TMP 폰트/렌더러 스타일링까지 직접 수행하고 있었다.
- 대사 요청/쿨다운/가시성 제어와 시각 오브젝트 생성은 변경 이유가 다르므로, 대사 런타임은 생성된 `TextMeshPro` 참조를 사용만 하고 생성 책임은 별도 factory로 분리하는 편이 명확하다.
- TMP Korean font 적용도 UI 생성 책임과 함께 묶어야 런타임 로직이 폰트 서비스 세부 구현에 덜 결합된다.

변경:

- `ICharacterDialogueBubbleFactory` / `CharacterDialogueBubbleFactory`를 추가했다.
- factory가 월드 공간 `TextMeshPro` 생성, 부모 transform 부착, 정렬/크기/wrapping/TMP Korean font/renderer sorting 설정을 전담한다.
- `CharacterDialogueRuntime`은 `ITmpKoreanFontService` 대신 `ICharacterDialogueBubbleFactory`를 주입받고, `EnsureText()`에서 factory를 호출하도록 변경했다.
- `DungeonRuntimeLifetimeScope`에 `CharacterDialogueBubbleFactory` singleton 등록을 추가했다.

효과:

- `CharacterDialogueRuntime`에서 런타임 오브젝트 생성 책임이 제거됐다.
- LLM 말풍선 요청/상태 제어는 dialogue runtime, 말풍선 view 생성은 injected factory로 역할이 분리됐다.
- 감사 요약은 317 script files, 6246 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- CharacterDialogueRuntime.cs no longer contains new GameObject or TextMeshPro style construction code
- CharacterDialogueBubbleFactory.cs owns CharacterDialogueBubble TextMeshPro creation
- DungeonRuntimeLifetimeScope registers CharacterDialogueBubbleFactory as ICharacterDialogueBubbleFactory
```

### 133. Character feedback bubble view factory DI

대상:

- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleViewFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterFeedbackBubble`은 캐릭터 상태/로그 태그를 평가하고 표시 상태를 결정하는 컴포넌트인데, 동시에 `TextMeshPro` 오브젝트 생성, TMP Korean font 적용, renderer sorting 설정, static text pool 관리까지 직접 수행하고 있었다.
- 상태 평가/표시 상태 제어와 월드 텍스트 view 생성/풀링은 변경 이유가 다르므로 분리하는 편이 더 명확하다.
- feedback bubble component 생성은 이미 `ICharacterFeedbackBubbleService`로 분리되어 있으므로, 그 내부 텍스트 view 생성도 별도 factory로 닫는 것이 일관된다.

변경:

- `ICharacterFeedbackBubbleViewFactory` / `CharacterFeedbackBubbleViewFactory`를 추가했다.
- factory가 pooled `TextMeshPro` view 생성, TMP Korean font 적용, parent/local position 설정, renderer sorting, release/reset을 전담한다.
- `CharacterFeedbackBubble`은 `ITmpKoreanFontService` 대신 `ICharacterFeedbackBubbleViewFactory`를 주입받고, `EnsureView()`/`ReleaseView()`에서 factory를 호출하도록 변경했다.
- `DungeonRuntimeLifetimeScope`에 `CharacterFeedbackBubbleViewFactory` singleton 등록을 추가했다.

효과:

- `CharacterFeedbackBubble`에서 런타임 오브젝트 생성 책임과 static text pool이 제거됐다.
- feedback state 평가/표시 제어는 component, text view 생성/풀링은 injected factory로 역할이 분리됐다.
- 감사 요약은 318 script files, 6253 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- CharacterFeedbackBubble.cs no longer contains new GameObject, TextMeshPro style construction, MeshRenderer sorting, or ITmpKoreanFontService direct dependency
- CharacterFeedbackBubbleViewFactory.cs owns feedback bubble TextMeshPro creation and pooling
- DungeonRuntimeLifetimeScope registers CharacterFeedbackBubbleViewFactory as ICharacterFeedbackBubbleViewFactory
```

### 134. Notice feed item factory DI

대상:

- `Assets/Scripts/UI/NoticeFeed.cs`
- `Assets/Scripts/UI/NoticeFeedItemFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `NoticeFeed`는 `NoticeFeedEvent`를 받아 표시 순서와 tween release를 관리하는 UI listener인데, 동시에 prefab instantiate, TMP Korean font 적용, text/color 설정, pool item 활성화/비활성화/파괴까지 직접 수행하고 있었다.
- 이벤트 처리와 item view 생성/초기화는 변경 이유가 다르므로, item view 책임을 factory로 분리하는 편이 테스트와 DI 경계가 명확하다.
- `NoticeFeed`가 더 이상 `ITmpKoreanFontService`에 직접 결합하지 않아도 되며, font 적용은 생성/준비 factory 내부로 모을 수 있다.

변경:

- `INoticeFeedItemFactory` / `NoticeFeedItemFactory`를 추가했다.
- factory가 prefab instantiate, parent/scale 설정, TMP Korean font 적용, notice text/color 적용, pool item take/return/destroy 처리를 전담한다.
- `NoticeFeed`는 `INoticeFeedItemFactory`를 주입받고, pool callback과 event 준비 단계에서 factory를 호출하도록 변경했다.
- `DungeonRuntimeLifetimeScope`에 `NoticeFeedItemFactory` singleton 등록을 추가했다.

효과:

- `NoticeFeed`에서 직접 prefab instantiate, Destroy, TMP font 적용, grade color 결정 책임이 제거됐다.
- event listening/tween sequence는 `NoticeFeed`, item 생성/초기화/수명 처리는 injected factory로 역할이 분리됐다.
- 감사 요약은 319 script files, 6264 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- NoticeFeed.cs no longer contains Instantiate, UnityEngine.Object.Destroy, ITmpKoreanFontService direct dependency, or grade color switch
- NoticeFeedItemFactory.cs owns notice item prefab creation, setup, activation, return, and destruction
- DungeonRuntimeLifetimeScope registers NoticeFeedItemFactory as INoticeFeedItemFactory
```

### 135. Character summary runtime log factory DI

대상:

- `Assets/Scripts/UI/CharacterSummeryInfo.cs`
- `Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterSummeryInfo`는 캐릭터 선택 이벤트, stat slider 갱신, character log subscription을 관리하는 popup인데, 동시에 런타임 `TextMeshProUGUI` 오브젝트 생성과 TMP Korean font/style 적용까지 직접 수행하고 있었다.
- 캐릭터 popup 상태 바인딩과 runtime log text view 생성은 변경 이유가 다르므로 분리하는 편이 명확하다.
- 누락된 UI root 또는 VContainer injection은 조용히 무시하지 않고 명시 예외로 드러내야 디버깅이 빠르다.

변경:

- `ICharacterSummaryRuntimeLogFactory` / `CharacterSummaryRuntimeLogFactory`를 추가했다.
- factory가 `CharacterLogText` GameObject 생성, RectTransform 배치, TMP Korean font 적용, text style 설정을 전담한다.
- `CharacterSummeryInfo`는 `ITmpKoreanFontService` 대신 `ICharacterSummaryRuntimeLogFactory`를 주입받고, log text 확보와 font 적용을 factory에 위임한다.
- `DungeonRuntimeLifetimeScope`에 `CharacterSummaryRuntimeLogFactory` singleton 등록을 추가했다.

효과:

- `CharacterSummeryInfo`에서 직접 `new GameObject`, `TextMeshProUGUI`, TMP font service 접근이 제거됐다.
- 캐릭터 popup은 이벤트/상태 표시, factory는 runtime log text view 생성/스타일링으로 역할이 분리됐다.
- 감사 요약은 320 script files, 6272 extracted declarations 상태가 됐다.

검증:

```text
Targeted source scan:
- CharacterSummeryInfo.cs no longer contains new GameObject, TextMeshProUGUI, ITmpKoreanFontService direct dependency, EnsureRuntimeLogText, or RequireTmpKoreanFontService
- CharacterSummaryRuntimeLogFactory.cs owns CharacterLogText creation, RectTransform placement, TMP Korean font application, and style setup
- DungeonRuntimeLifetimeScope registers CharacterSummaryRuntimeLogFactory as ICharacterSummaryRuntimeLogFactory
```

### 136. Hallway structural-wall render split

대상:

- `Assets/Scripts/Buildings/SO/BuildingSO.cs`
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `Hallway.asset`은 배치 레이어가 `GridLayer.Hallway`지만 카테고리는 `Wall`이다. 이 값은 복도가 일반 건물 콜라이더/방문 시설처럼 동작하지 않게 하려는 gameplay 의미로 쓰인다.
- 기존 `GridTexture.DrawBuilding(BuildingSO, Vector2Int)`는 `buildingData.IsWall`을 먼저 검사해 복도 타일 dictionary를 그리기 전에 `wallTilemap`에 벽 타일을 찍고 반환했다.
- 결과적으로 복도는 데이터상 tile visual을 가지고 있어도 구조 벽처럼 렌더링되어, PlayMode에서 복도가 사라지거나 건물과 벽이 겹쳐 보이는 디버깅 장애가 생겼다.

변경:

- `GridBuildingPlacement.IsStructuralWall`을 추가했다.
- `BuildingSO.IsStructuralWall`이 placement 판정을 노출한다.
- `GridTexture.DrawBuilding`/`DeleteBuilding`은 `IsWall`이 아니라 `IsStructuralWall`일 때만 `SetWallCell`을 호출한다.
- hallway-layer 오브젝트는 category가 `Wall`이어도 tile dictionary 또는 sprite render 경로를 유지한다.

효과:

- `Wall` 카테고리의 gameplay 의미와 실제 구조 벽 렌더링 책임이 분리됐다.
- 복도는 시설 밑에 깔리는 Hallway layer 점유/이동 가능성은 유지하면서 wallTilemap을 오염시키지 않는다.
- 방/건물 실제 Building layer overlap 검증과 복도-underlay 공존 검증을 분리해서 볼 수 있게 됐다.

검증:

```text
Unity MCP PlayMode:
- Scene: CharacterAiTestScene
- Main Camera capture: structural wall contamination smoke only; final corridor/building visibility separation is tracked in section 141
- Tilemap Hallway tiles: 81 (pre hallway visual-sync value; includes visible hallway tiles under facilities)
- Grid cells with Hallway layer occupant: 81
- Non-hallway building overlap count: 0 (logic-layer Building overlap only, not a final visual-overlap verdict)
- Unity console warnings/errors: 0

Coverage scan:
- Runtime scripts excluding Editor/debug scenarios: 272
- Missing from all-scripts-function-inventory.md: 0
```

### 137. Character spawn object factory DI

대상:

- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/Character/CharacterSpawnObjectFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `CharacterSpawner`는 손님 스폰 주기, 리스폰 타이머, 입구 grid/world 위치 결정, regular-customer/run-variable gating을 맡는 정책 컴포넌트다.
- 같은 파일이 `Instantiate(characterPrefab)`, pooled object destroy, 생성 직후 모든 child `MonoBehaviour`에 대한 `IObjectResolver.Inject(...)` 루프까지 직접 수행하고 있어, 스폰 정책과 런타임 오브젝트 composition/DI 주입 책임이 섞여 있었다.
- 스폰된 캐릭터의 DI 누락은 PlayMode에서 AI/이동/피드백이 조용히 깨지는 종류의 오류라, 생성/주입/파괴 책임은 VContainer 등록 factory 경계에 모으는 편이 디버깅이 쉽다.

변경:

- `ICharacterSpawnObjectFactory` / `CharacterSpawnObjectFactory`를 추가했다.
- factory가 customer prefab instantiate, child component VContainer injection, pool object destroy/destroy-immediate를 전담한다.
- `CharacterSpawner`는 `IObjectResolver` 대신 `ICharacterSpawnObjectFactory`를 주입받는다.
- `CreatePooledItem`, `TrySpawnCharacter`, `OnDestroyPoolObject`는 factory를 호출하고, 스폰 조건/리스폰/입구 위치/풀 release 정책만 유지한다.
- `DungeonRuntimeLifetimeScope`에 `CharacterSpawnObjectFactory` singleton 등록을 추가했다.

효과:

- 스폰 정책 컴포넌트에서 직접 `Instantiate`, `Destroy`, `DestroyImmediate`, child component injection loop가 제거됐다.
- spawned customer object composition은 VContainer factory 경계로 보이게 됐다.
- 캐릭터 prefab 생성 경로의 DI 누락을 PlayMode에서 스포너/캐릭터 상태로 직접 확인할 수 있게 됐다.

검증:

```text
Unity MCP compile/PlayMode:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- Scene: CharacterAiTestScene
- CharacterSpawner count: 1
- Spawner poolCreated: True
- CharacterActor reflection-count: 2
- CharacterPrefab(Clone): hasBrain=True, hasMove=True, hasVisual=True, hasLifecycle=True
- 테스트 직원: hasBrain=True, hasMove=True, hasVisual=True, hasLifecycle=True
- Unity console warnings/errors after PlayMode verification: 0
- Main Camera capture: spawned character visible; hallway/facility layout still rendered

Coverage scan:
- Script files: 321
- Runtime scripts excluding Editor/debug scenarios: 273
- Missing from all-scripts-function-inventory.md: 0
```

### 138. Character visual root factory DI

대상:

- `Assets/Scripts/Character/Core/CharacterVisualRootFactory.cs`
- `Assets/Scripts/Character/Core/OwnerCharacterFactory.cs`
- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `OwnerCharacterFactory`와 `InvasionIntruderRuntimeFactory`가 각각 `Visual` child 생성, `SpriteRenderer` 보장 로직을 중복으로 가지고 있었다.
- 두 factory는 owner/intruder의 런타임 component composition 책임은 가져도 되지만, 같은 visual-root 보강 규칙을 각자 구현하면 sprite root 구조 변경 시 두 경로가 쉽게 갈라진다.
- `CharacterVisual`은 root renderer migration과 visibility/anchor 조정을 맡는 컴포넌트라 그대로 두고, owner/intruder 생성 경로의 공통 Visual child/SpriteRenderer 보강만 별도 factory로 분리하는 것이 책임 경계가 가장 작다.

변경:

- `ICharacterVisualRootFactory` / `CharacterVisualRootFactory`를 추가했다.
- factory가 `Visual` child를 찾거나 생성하고, child `SpriteRenderer`를 보장한다.
- `OwnerCharacterFactory`는 `ICharacterVisualRootFactory`를 주입받아 owner 필수 component 구성 중 visual root 보장을 위임한다.
- `InvasionIntruderRuntimeFactory`는 `ICharacterVisualRootFactory`를 주입받아 intruder runtime 보강 중 visual root 보장을 위임한다.
- `DungeonRuntimeLifetimeScope`에 `CharacterVisualRootFactory` singleton 등록을 추가했다.

효과:

- owner/intruder runtime factory에서 중복 `new GameObject("Visual")`/`AddComponent<SpriteRenderer>` 구현이 제거됐다.
- prefab 없는 owner/intruder 생성 경로도 같은 visual-root 규칙을 쓰게 됐다.
- 캐릭터 runtime composition 책임은 factory 계층에 남기되, shared visual-root 생성 규칙은 VContainer 서비스 경계로 보이게 됐다.

검증:

```text
Targeted source scan:
- no direct `new OwnerCharacterFactory(...)` or `new InvasionIntruderRuntimeFactory(...)` call remains under Assets/Scripts
- OwnerCharacterFactory.cs no longer contains EnsureCharacterVisual
- InvasionIntruderFactory.cs no longer contains EnsureCharacterVisual
- shared Visual child/SpriteRenderer creation exists in CharacterVisualRootFactory.cs

Unity MCP compile/PlayMode:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- Scene: CharacterAiTestScene
- CharacterActor reflection-count: 2
- 테스트 직원: visualChild=True, visualRenderer=True, characterVisual=True, behaviorTree=True
- CharacterPrefab(Clone): visualChild=True, visualRenderer=True, characterVisual=True, behaviorTree=True
- CharacterVisualRootFactory smoke object: visualChild=True, renderer=True, parentIsSmoke=True
- Tilemap Hallway tiles: 81 (pre hallway visual-sync smoke value; final visual separation is tracked in section 141)
- Unity console warnings/errors after PlayMode verification: 0
- Main Camera capture: initial smoke capture only; final room/corridor visual overlap verification is tracked in section 141

Coverage scan:
- Script files: 322
- Runtime scripts excluding Editor/debug scenarios: 274
- Missing from all-scripts-function-inventory.md: 0
```

### 139. Overlap click priority PlayMode verification

대상:

- `Assets/Scripts/UI/WorldInfoClickSelector.cs`
- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/Input/OwnerCommandController.cs`

판단:

- 건물과 캐릭터 collider가 같은 위치에 있을 때 Unity의 mouse event가 건물 쪽으로 먼저 들어가면, 건물 popup이 먼저 열려 캐릭터 디버깅이 막힐 수 있다.
- 현재 `BuildableObject.OnMouseDown()`은 먼저 `IWorldInfoClickSelector.TryTriggerCharacterUnderPointer()`를 호출하고, 캐릭터가 있으면 building click event를 중단한다.
- 따라서 핵심 검증은 `WorldInfoClickSelectionService`가 같은 지점의 building collider와 character collider를 함께 받았을 때 character를 우선 반환하는지다.

검증:

```text
Unity MCP PlayMode:
- Scene: CharacterAiTestScene
- Probe: temporary BoxCollider2D building overlap at active CharacterActor collider center
- Direct collider list order: [buildingProbeCollider, actorCollider]
- TryGetPreferredCharacter result: directFound=True, directActor=테스트 직원, directSameGameObject=True
- TryGetPreferredCharacterAtScreenPosition result: screenFound=True, screenActor=테스트 직원, screenSameGameObject=True
- Physics2D.OverlapPointAll at probe screen/world position: 2 overlapping colliders
- Unity console warnings/errors after corrected verification: 0
```

결론:

- 겹친 건물/캐릭터 클릭 우선순위는 현재 직원 우선으로 동작한다.
- 이번 구간은 코드 수정 없이 PlayMode 검증만 수행했다.

### 140. Grid building object factory DI

대상:

- `Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs`
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 런타임 건물 생성은 `new GameObject`, collider/Rigidbody 구성, `AddComponent(buildingData.type)`까지 scene mutation이 강한 경계다.
- placement, synthesis, evolution 경로가 같은 건물 생성 규칙을 공유하므로 VContainer로 주입되는 factory 경계가 맞다.
- 기존 editor/debug 호환 생성자는 남기되, 실제 runtime composition은 `IGridBuildingObjectFactory`를 통해 보이게 한다.

변경:

- `IGridBuildingObjectFactory` / `GridBuildingObjectFactory`를 추가했다.
- `GridBuildingFactory`가 injected object factory를 사용해 건물 GameObject/collider/Rigidbody/component 생성을 위임한다.
- grid controller, synthesis, evolution runtime, lifetime scope에 object factory 주입/등록을 연결했다.

검증:

```text
Unity MCP:
- AssetDatabase.Refresh: compilation successful, isCompiling=False
- PlayMode smoke with non-generic Resources.LoadAll("SO/Building") because MCP dynamic compile cannot safely reference Odin-derived BuildingSO types directly.
- factorySmoke building=[Door], object=[문], hasCollider=[True], hasRigidbody=[True], expectedType=[True], position=[(0.50, 0.00, 0.00)]
- Unity console warnings/errors after smoke: 0
```

### 141. Hallway visual sync and ghost preview cap

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `GridBuildingPlacementService.EnsureHallwayUnderBuilding` 때문에 건물 아래에도 gameplay Hallway 점유가 유지된다. 이건 이동/경로 계산에는 필요하지만, 그대로 visible hallway tile로 남기면 건물과 복도가 겹쳐 보인다.
- build ghost가 alpha 1로 표시되면 기존 방, 복도, 건물을 가려서 PlayMode 디버깅에서 실제 overlap처럼 보인다.
- 따라서 논리용 Hallway occupancy와 눈에 보이는 corridor tile을 분리해야 한다.

변경:

- `GridTexture.DrawWall(Grid)` 끝에서 `SynchronizeHallwayVisuals(Grid)`를 호출한다.
- 비이동 건물이 있는 칸은 Hallway tilemap의 visible tile을 제거하고, 순수 복도/이동 건물 칸만 Hallway visual을 유지한다.
- `GridGhostObject`에 `maxPreviewAlpha = 0.35`를 추가하고 단일/드래그 preview 색상에 동일하게 적용했다.

검증:

```text
Unity MCP clean PlayMode restart:
- Scene: CharacterAiTestScene
- Grid: 32x3, version 93
- Gameplay Hallway occupants: 81
- Visible filled tile cells: Hallway=41, Wall=102, BuildingBack=5, BuildingBackWidth=8, BuildingFront=3, BuildingFrontWidth=1, Ground=275
- Occupancy rows still include hallway+building cells by design:
  y=2 ____.....BBB.BBBBBBB.BBBB..BBB._
  y=1 ____BBBBBBBBBBBB.BBB.BBBB......_
  y=0 _XXXBBBBBBBBBBBBBBBB..........._
- SceneView 2D captures at worldX=-16 and worldX=-24 showed the corridor/room structure without the opaque placement overlay.
- Selected rest-room ghost: sprite=facility_rest_room, alpha=0.350
- Unity console warnings/errors: 0
```

### 142. AI scheduler unregister PlayMode-exit safety

대상:

- `Assets/Scripts/Infrastructure/CharacterAiSchedulingService.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 일반 AI 실행 경로에서 `CharacterAiScheduler`가 없으면 오류를 내는 것이 맞다.
- 하지만 PlayMode 종료 중에는 scheduler가 먼저 unload되고 캐릭터 `OnDisable()`이 뒤늦게 `Unregister`를 호출할 수 있다.
- 종료 중 해제는 이미 scheduler가 없으면 할 일이 없으므로, `Unregister`만 idempotent하게 처리하고 등록/즉시결정/budget/feedback API는 기존처럼 강제 resolve를 유지한다.

변경:

- `CharacterAiSchedulingService.Unregister`가 `TryResolveScheduler`로 scheduler 존재 여부를 확인한 뒤 있을 때만 `UnregisterActor`를 호출하게 했다.
- `ResolveScheduler`는 `TryResolveScheduler`를 사용하되, 실행 필수 경로에서는 여전히 `InvalidOperationException`을 던진다.

검증:

```text
Unity MCP recheck:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- CharacterAiTestScene PlayMode enter: isPlaying=True
- Filled tile cells during PlayMode: Hallway=41, Wall=102, BuildingBack=5, BuildingBackWidth=8, BuildingFront=3, BuildingFrontWidth=1, Ground=275
- Gameplay Hallway occupants: 81
- Building+Hallway logic cells: 52
- PlayMode exit requested after grid mode reset
- Unity console warnings/errors after exit: 0
- Editor state after exit: scene=CharacterAiTestScene, isPlaying=False, isCompiling=False
```

### 143. Centralized Resources asset loader DI

대상:

- `Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs`
- `Assets/Scripts/Infrastructure/CharacterAiActionAssetCatalog.cs`
- `Assets/Scripts/Infrastructure/DataScriptableObjectSource.cs`
- `Assets/Scripts/Infrastructure/InvasionIntruderDataProvider.cs`
- `Assets/Scripts/Infrastructure/TmpKoreanFontProvider.cs`
- `Assets/Scripts/Infrastructure/RunVariableCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 이전 단계들에서 `Resources.Load/LoadAll`을 도메인 provider 안으로 이동시켰지만, provider마다 직접 `Resources` API를 호출하고 있었다.
- 목표는 fallback 없이 asset 누락을 즉시 드러내면서도, 런타임 asset 접근 지점을 하나로 모아 테스트/교체 가능한 경계를 만드는 것이다.
- `DungeonSceneComponentQuery`의 `Resources.FindObjectsOfTypeAll`은 scene component lookup 용도라 이번 asset-loader 경계와 분리해서 유지한다.

변경:

- `IResourcesAssetLoader` / `UnityResourcesAssetLoader`를 추가했다.
- DataScriptableObject source, AI action catalog, 침입자 데이터 provider, TMP Korean font provider, run character catalog가 `IResourcesAssetLoader`를 주입받아 asset을 읽도록 바꿨다.
- `DungeonRuntimeLifetimeScope`에 `UnityResourcesAssetLoader` singleton 등록을 추가했다.
- 런타임 `Resources.Load/LoadAll` 직접 호출은 `ResourcesAssetLoader.cs` 하나로 수렴했다.

검증:

```text
Targeted source scan:
- `Resources.Load/LoadAll` under Assets/Scripts excluding Editor now appears only in Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- Clean CharacterAiTestScene PlayMode enter: isPlaying=True
- VContainer Resolve(Type,key) via reflection:
  - loaderType=UnityResourcesAssetLoader
  - dataCount=89
  - characterCount=5
  - workActionResolved=True
  - fontResolved=True
  - intruderResolved=True
- Filled tile cells: Hallway=41, Wall=102, BuildingBack=5, BuildingBackWidth=8, BuildingFront=3, BuildingFrontWidth=1, Ground=275
- 2D SceneView capture succeeded at origin=(-24, 4.5), size=(42x13), resolution=1344x416.
- Unity console warnings/errors before PlayMode exit: 0
- Editor state after exit: scene=CharacterAiTestScene, isPlaying=False, isCompiling=False
```

### 144. Runtime-only audit documents and hallway visual recheck

대상:

- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`

판단:

- 목표 기준은 `Editor` 밑 스크립트를 제외한 런타임 코드 감사인데, legacy full inventory/coupling 문서는 전체 스크립트를 포함한다.
- 복도/건물 시각 문제는 코드 존재만으로 판단하면 안 되고, PlayMode에서 실제 타일맵 셀과 카메라 캡처를 같이 봐야 한다.

변경:

- 런타임 전용 함수 인벤토리와 커플링 감사 문서를 별도로 추가했다.
- 기존 full inventory/coupling 문서에는 런타임 전용 companion 링크를 추가했다.
- 런타임 커플링 감사의 `GlobalObjectLookup` 카운트 누락을 `1`로 바로잡았다.

검증:

```text
Runtime audit generation:
- Runtime script files: 276
- Extracted declarations: 3480
- Exclusion: Assets/Scripts/**/Editor/**/*.cs
- Runtime flag counts: GlobalObjectLookup=1, SingletonAccess=0, ResourcesAccess=2, Reflection=4, EventBus=38, RuntimeObjectCreation=31, SceneMutation=71, DependencyInjection=65

Unity MCP PlayMode visual recheck:
- Scene: Assets/Scenes/CharacterAiTestScene.unity
- PlayMode: isPlaying=True, isCompiling=False
- Filled tile cells: Hallway=41, Wall=102, BuildingBack=4, BuildingBackWidth=5, BuildingFront=3, BuildingFrontWidth=1, Ground=275
- Grid occupancy: grid=32x3, hallwayLogic=81, buildingLogic=55, bothLayers=52
- Hallway visibility rule: visibleExpected=41, visibleActual=41, hiddenByBuilding=40
- Visual mismatch counts: missingVisible=0, wronglyVisibleUnderBuilding=0
- 2D SceneView capture succeeded at origin=(-40, -5), size=(55x16), resolution=1760x512.
- Main Camera capture succeeded from Main Camera at (-20, 4.5, -10), orthographicSize=8.4375.
- Unity console warnings/errors/exceptions/asserts: 0
```

### 145. Runtime public Instance facade removal for LLM/social runtimes

대상:

- `Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `LocalLlmRequestQueue.Instance`와 `SocialReputationRuntime.Instance`는 런타임 호출자가 아니라 Editor 디버그 시나리오 편의 접근자였다.
- VContainer/provider 경계가 이미 존재하므로 런타임 클래스가 public static facade를 계속 노출할 이유가 없다.
- 중복 인스턴스 방어용 private static 필드는 유지하되, 외부 전역 접근 지점만 제거한다.

변경:

- `LocalLlmRequestQueue`와 `SocialReputationRuntime`의 public `Instance` property를 제거했다.
- Editor 디버그 시나리오는 `CharacterAiPlanDebugFixtures.FindQueueInstance()` / `FindSocialRuntimeInstance()` helper를 통해 scene lookup을 수행하게 바꿨다.
- 런타임 전용 coupling 감사의 `SingletonAccess` 카운트를 0으로 갱신하고, `RoomProfile`의 이전 hit가 false positive였음을 문서화했다.

검증:

```text
Source scan:
- No `LocalLlmRequestQueue.Instance` or `SocialReputationRuntime.Instance` remains under Assets/Scripts.
- Runtime-only `Instance` scan has no direct singleton facade hit; remaining names are Editor helper methods or unrelated `RoomInstance` naming.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- CharacterAiTestScene PlayMode enter: isPlaying=True, isCompiling=False
- LocalLlmRuntimeProvider.TryGetRuntime: True, runtime type LocalLlmRequestQueue
- Runtime component counts: LocalLlmRequestQueue=1, SocialReputationRuntime=1
- Hallway visual mismatch counts: missingVisible=0, wronglyVisibleUnderBuilding=0
- Main Camera capture succeeded from Main Camera at (-20, 4.5, -10), orthographicSize=8.4375.
- Unity console warnings/errors/exceptions/asserts: 0
```

### 146. Generated UI tab panel factory extraction

대상:

- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/UI/UITabGeneratedPanelFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `UITabManager`가 탭 열기/닫기 orchestration뿐 아니라 generated tab panel, title/body text, `UITab`, `StaffWorkPriorityPanel` 생성까지 직접 맡고 있었다.
- 이 생성 책임은 scene component가 아니라 VContainer에 등록된 factory 경계가 맡는 편이 테스트/주입/수명 추적에 더 맞다.
- Top button template 복제와 layout group 보정은 아직 `UITabManager`의 UI orchestration 책임으로 남겼다.

변경:

- `IUITabGeneratedPanelFactory` / `UITabGeneratedPanelFactory`를 추가해 generated tab panel 생성과 `UITab` 주입을 담당하게 했다.
- `IStaffWorkPriorityPanelFactory` / `StaffWorkPriorityPanelFactory`를 추가해 staff priority panel 부착, 주입, placeholder body 비활성화를 담당하게 했다.
- `UITabManager`는 generated panel을 직접 만들지 않고 factory를 주입받아 사용한다.
- `DungeonRuntimeLifetimeScope`에 두 factory를 singleton으로 등록했다.
- 런타임 전용 감사 문서에 새 runtime script를 추가하고 `UITabManager` 라인/선언 목록을 갱신했다.

검증:

```text
Source scan:
- UITabManager no longer contains generated panel `new GameObject`, `AddComponent<UITab>`, or `AddComponent<StaffWorkPriorityPanel>`.
- Runtime audit summary updated to Runtime script files=277, Extracted declarations=3492.
- Runtime flag counts updated: RuntimeObjectCreation=32, SceneMutation=72, DependencyInjection=66.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- CharacterAiTestScene PlayMode enter: isPlaying=True, isCompiling=False
- UITabManager.ToggleSelectButton(2) runtime probe:
  - tabCount=11
  - generatedNamedTabs=10
  - staffPanelCount=1
  - staffTabExists=True
  - staffTabActive=True
  - staffPanelOnStaffTab=True
  - staffPanelVisibleWorkers=2
  - visibleCells=16
- Main Camera capture succeeded from Main Camera at (-20, 4.5, -10), orthographicSize=8.4375.
- Unity console warnings/errors/exceptions/asserts: 0
```

### 147. CharacterAiTestScene leaked root cleanup and scene validator hardening

대상:

- `Assets/Scenes/CharacterAiTestScene.unity`
- `Assets/Scripts/Infrastructure/SceneBuildableLeakValidator.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

판단:

- 사용자 카메라 스크린샷 기준으로 복도/건물 시각 상태가 깨져 있었고, 이전의 콘솔 중심 검증은 충분하지 않았다.
- 실제 에디터 씬에는 `오크 사장` 루트 30개와 2성/3성 방어시설 루트 28개가 중복 저장되어 있었다.
- 방어시설 루트 일부는 `Missing` 컴포넌트 상태라 단순 `BuildableObject` 검색만으로는 다음 검증에서 다시 놓칠 수 있었다.

변경:

- `CharacterAiTestScene`에 저장된 누수 루트 58개를 삭제하고 씬을 저장했다.
- `SceneBuildableLeakValidator`를 보강해 PlayMode 시작 시 loaded scene root와 child의 Missing script를 즉시 실패로 보고한다.
- 기존 미초기화 `Shop`/`DefenseFacility` 루트 검사도 유지해, 그리드에 등록되지 않은 시설이 collider/click selection을 오염시키지 못하게 했다.

검증:

```text
EditMode:
- CharacterAiTestScene root count: 10
- Suspicious saved roots: 0
- Disk search for escaped leaked names (`2\uC131`, `3\uC131`, `\uC624\uD06C`): 0 matches

PlayMode:
- Scene roots after runtime generation: 105
- Missing components: 0
- Suspicious leaked roots: 0
- Hallway visual expected/actual: 41 / 41
- Missing visible hallway cells: 0
- Wrongly visible hallway cells under non-movement buildings: 0
- Hidden logical hallway cells under buildings: 40
- Active ghost sprite renderers: 0
- Main Camera capture succeeded from Main Camera at (-20, 4.5, -10), orthographicSize=8.4375.
- Unity console warnings/errors/exceptions/asserts: 0
```

### 148. Grid construct button factory extraction

대상:

- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `GridConstructTab.MakeSelectButton()`가 카테고리 패널 생성, 카테고리 버튼 생성, 건물 선택 버튼 생성, TMP 폰트 적용, 새 버튼 주입까지 한 번에 처리하고 있었다.
- 탭 컴포넌트가 맡아야 할 책임은 "어떤 카테고리를 열고 닫는가"이고, prefab instantiation과 runtime component injection은 factory 경계가 맡는 편이 맞다.

변경:

- `IGridConstructButtonFactory` / `GridConstructButtonFactory`를 추가했다.
- `GridConstructTab`에서 `Instantiate`/동적 `GetComponent` 생성 경로를 제거하고, category panel/button/building button 생성은 factory로 위임했다.
- Runtime-instantiated `UITab`과 `UIBuildingSelectButton`은 factory가 `IObjectResolver.Inject`로 즉시 주입한다.
- `DungeonRuntimeLifetimeScope`에 `GridConstructButtonFactory`를 singleton으로 등록했다.
- runtime/all script inventory와 coupling audit에서 `GridConstructTab`의 `RuntimeObjectCreation` 플래그를 factory로 이동시켰다.

검증:

```text
Source scan:
- GridConstructTab.cs has no `Instantiate`, `new GameObject`, `AddComponent`, `Destroy(`, or `SetParent` hits.
- GridConstructButtonFactory.cs owns the three prefab instantiation paths.
- Runtime inventory updated to Runtime script files=278, Extracted declarations=3508.
- Runtime coupling flags updated: RuntimeObjectCreation=32, SceneMutation=73, DependencyInjection=67.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- CharacterAiTestScene PlayMode enter: isPlaying=True
- GridConstructTab generated UI probe:
  - panels=1
  - activePanels=1
  - categoryButtons=1
  - categoryLabels=1
  - buildingButtons=1
  - buttonsWithIcons=1
- Generated UIBuildingSelectButton injection probe:
  - ActiveDestroyMode worked=True
  - OnClick worked=True
  - error=""
- Main Camera capture succeeded from Main Camera at (-20, 4.5, -10), orthographicSize=8.4375.
- Unity console warnings/errors/exceptions/asserts after PlayMode validation: 0
```

### 149. Hidden hallway overlap removal and self-contained room-role validation

대상:

- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`
- `Assets/Scripts/Grid/Core/Grid.cs`
- `Assets/Scripts/Buildings/SO/BuildingSO.cs`
- `Assets/Scripts/Rooms/RoomDetector.cs`
- `Assets/Scripts/Rooms/RoomInstance.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 이전 검증에서 "복도와 건물이 겹치지 않는다"는 판단은 실제 플레이 화면 기준으로 틀렸다. `initialPlacement`의 복도 배치와 `GridBuildingPlacementService.EnsureHallwayUnderBuilding` 경로가 시설/상점 아래 gameplay hallway occupant를 남겼고, 이 상태는 시각 레이어 보정만으로는 디버깅 가능한 구조가 아니었다.
- 현재 씬의 시설은 prefab형 방처럼 배치되어 있는데, `requiresRoomRole` 시설이 벽/문으로 감싼 `RoomInstance`만 요구하면 방문 가능한 시설이 0개가 된다. 이건 fallback으로 뚫을 문제가 아니라, "완성된 방으로 취급하는 시설"을 데이터와 룸 감지 책임으로 분리해야 하는 문제다.

변경:

- `EnsureHallwayUnderBuilding`을 제거하고 `EnsureHallwayForPassageOverlay`로 대체했다. 이제 hallway footprint 공유는 hallway layer, movement 건물, `Door` 타입처럼 통로 역할을 하는 배치에만 허용한다.
- 초기 배치에서는 `CollectReservedRoomCells`/`IsRedundantInitialHallway`로 room-like footprint 아래의 중복 hallway placement를 건너뛴다.
- `Grid.IsWalkable`은 벽 건물을 막고, 방문자용 시설(`FacilityData.IsVisitorFacility`)은 워커블 셀로 인정한다. 단, `IsGridVisitable` 호출은 사용하지 않아 `RoomFacilityPolicy -> RoomDetector -> Grid.IsWalkable` 재귀를 만들지 않는다.
- `FacilityData.selfContainedRoom`을 추가하고, `RoomDetector.AddSelfContainedFacilityRooms`가 해당 시설을 `RoomInstance(selfContained: true)`로 등록한다. `RoomInstance.HasDoor`는 self-contained room이면 문이 없어도 usable room으로 본다.
- 현재 room-role 시설 asset 19개에 `selfContainedRoom=true`를 설정했다: `HamburgerStore`, `WeaponStore`, `P1_Barracks`, `P1_BattleDining`, `P1_BattlefieldDining`, `P1_GeneralStore`, `P1_LowFoodShop`, `P1_ManaStorage`, `P1_MeatRestaurant`, `P1_NobleDining`, `P1_PremiumMeatRestaurant`, `P1_ResearchLab`, `P1_RestRoom`, `P1_Toilet`, `P1_TrainingRoom`, `P1_WarBarracks`, `P1_Warehouse`, `P1_Washroom`, `P1_WeaponShop`.

검증:

```text
Unity MCP / PlayMode probes:
- Before fix: buildables=94, occupiedCells=84, overlapCells=52
- After hallway filtering: buildables=54, occupiedCells=84, overlapCells=12, unexpectedOverlapCells=0
- Remaining overlaps are intentional passage overlays (Door/Stair + hallway), not room/facility hidden hallway overlap.

Facility/room probe:
- walkableCells=68
- bestStart=(4, 0)
- bestVisitableCount=7
- visitable: 무기상점, 저가 음식점, 훈련장, 고기 식당, 연구실, 휴식방, 마력 저장소
- roomCount=14
- self-contained usable room instances: 8

Main Camera:
- Capture succeeded.
- Actual Game camera did not show the Scene View yellow selection overlay.
- Character movement and building layout remained visible after the hallway cleanup.

Unity console / editor state:
- errors=0, warnings=0
- Exited PlayMode after validation.
- isPlaying=False, isPlayingOrWillChange=False, isCompiling=False
```

### 150. Staff work priority panel UI factory extraction

대상:

- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanelUiFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `StaffWorkPriorityPanel`은 직원 작업 우선순위 상태/이벤트/테이블 구성 정책을 담당해야 하는데, `new GameObject`, `AddComponent`, `Destroy`/`DestroyImmediate`까지 직접 수행하고 있었다.
- 이 상태에서는 패널의 UI 표시 정책과 Unity hierarchy 조립/파괴 책임이 결합되어, VContainer 주입 경계와 테스트 가능한 책임 분리가 흐려진다.
- `DungeonSceneBackdropFitter`는 이미 `IDungeonBackdropSpriteTilingFactory`를 통해 sprite duplication을 위임하고 있었지만, 감사 문서는 이전 `DuplicateBackgroundRenderer` 기준으로 남아 있어 함께 보정했다.

변경:

- `IStaffWorkPriorityPanelUiFactory` / `StaffWorkPriorityPanelUiFactory`를 추가했다.
- row/cell/text/button/scroll/mask/layout/shadow 생성과 release 처리를 새 factory로 이동했다.
- `StaffWorkPriorityPanel`은 `IStaffWorkforceQueryService`와 `IStaffWorkPriorityPanelUiFactory`만 주입받고, 패널 상태 갱신과 테이블 조립 순서만 담당한다.
- `DungeonRuntimeLifetimeScope`에 `StaffWorkPriorityPanelUiFactory`를 singleton으로 등록했다.
- runtime/all function inventory와 coupling audit에서 `StaffWorkPriorityPanel`의 `RuntimeObjectCreation` 플래그를 factory로 이동하고, `DungeonSceneBackdropFitter`의 stale runtime creation 플래그를 제거했다.

검증:

```text
Source scan:
- StaffWorkPriorityPanel.cs has no direct `new GameObject`, `AddComponent`, `Instantiate`, `Destroy(`, or `DestroyImmediate` hits.
- StaffWorkPriorityPanelUiFactory.cs owns the row/cell/text/button creation and release paths.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False
- PlayMode probe:
  - StaffWorkPriorityPanel count=1
  - Refresh failures=0
  - ScrollRects=1
  - GeneratedObjects=37
  - Buttons=32
  - Texts=63
  - VisibleWorkers=2
  - VisibleCells=16
- Main Camera capture succeeded from Main Camera at (-15.50, 4.50, -10.00), orthographicSize=8.4375.
- Scene View capture succeeded; overlay UI is not part of Main Camera render, so panel visual validation used hierarchy/probe data rather than camera pixels.
- PlayMode console warnings/errors/exceptions/asserts during validation: 0
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False, isCompiling=False
```

### 151. Owner selection option button factory extraction

대상:

- `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`
- `Assets/Scripts/Character/UI/OwnerSelectionOptionButtonFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `OwnerSelectionPanel`은 owner 후보 목록 구성과 선택 상태 갱신을 담당해야 하는데, option button prefab 생성/이벤트 바인딩/파괴까지 직접 들고 있었다.
- 이 책임은 UI hierarchy 생성 경계라서 패널 내부에 남기면 VContainer 주입 경계와 런타임 오브젝트 수명 관리 책임이 섞인다.

변경:

- `IOwnerSelectionOptionButtonFactory` / `OwnerSelectionOptionButtonFactory`를 추가했다.
- option button prefab instantiation, TMP label font 적용, click listener binding, release/destruction을 factory로 이동했다.
- `OwnerSelectionPanel`은 `IOwnerRunManagerProvider`, `ITmpKoreanFontService`, `IOwnerSelectionOptionButtonFactory`를 주입받고 owner-list orchestration만 수행한다.
- `DungeonRuntimeLifetimeScope`에 `OwnerSelectionOptionButtonFactory`를 singleton으로 등록했다.
- runtime/all function inventory와 coupling audit에서 `OwnerSelectionPanel`의 `RuntimeObjectCreation` 플래그를 factory로 이동했다.

검증:

```text
Source scan:
- OwnerSelectionPanel.cs has no direct `Instantiate(`, `new GameObject`, `AddComponent`, `Destroy(`, or `DestroyImmediate` hits.
- OwnerSelectionOptionButtonFactory.cs owns `Instantiate`, label setup, listener binding, and release/destruction paths.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False.
- Current PlayMode scene did not contain an OwnerSelectionPanel instance, so panel rendering could not be verified in that scene.
- Direct factory probe in PlayMode succeeded:
  - created=True
  - parented=True
  - name=OwnerOption_TestOwner
  - label=TestOwner / Human
  - fontApplied=True
  - clicked=True
```

### 152. Door passage overlay hallway visual policy alignment

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/script-coupling-audit.md`
- `docs/code-audit/refactor-log.md`

판단:

- `GridBuildingPlacementService`는 passage overlay 공유를 hallway layer, movement 건물, `Door`로 판단한다.
- 반면 `GridTexture.SynchronizeHallwayVisuals`는 movement 건물만 visible hallway 공유로 인정해서, Door가 통로 정책에서 빠지는 논리 불일치가 있었다.
- 현재 PlayMode probe에서는 visible hallway 누락이 0으로 나왔지만, 배치 정책과 렌더 정책이 달라지면 디버깅 시 다시 "통로가 안 보인다"는 상태가 생길 수 있다.

변경:

- `GridTexture.SynchronizeHallwayVisuals`의 공유 조건을 `CanShareHallwayVisual(BuildableObject building)`으로 분리했다.
- `CanShareHallwayVisual`은 `building.IsGridMovement || building is Door`를 인정한다.
- coupling audit notes에 Door/movement passage overlay visual policy를 기록했다.

검증:

```text
Unity MCP / CharacterAiTestScene PlayMode:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False.
- Grid probe after fix:
  - grid=32x3
  - hallwayCells=41
  - buildingCells=55
  - emptyCells=12
  - expectedVisibleHallwayCells=41
  - missingVisibleHallwayTiles=0
  - unexpectedOverlapCells=0
  - hiddenHallwayUnderBuilding=0
  - allowedMovementOrDoorOverlaps=12
  - hallwayTilemaps=Grid/Tilemap/Hallway
- 2D SceneView capture succeeded for the gameplay region; visible corridor/room separation was present.
- Console Error/Exception/Assert after validation: 0.
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False, isCompiling=False.
```

### 153. UITab top button factory extraction

대상:

- `Assets/Scripts/UI/UITabManager.cs`
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `UITabManager`는 탭 상태, 탭 내용 갱신, 상단 버튼 바인딩 순서를 조율하는 컴포넌트다.
- 상단 버튼 prefab cloning, TMP label setup, 이전 2-row 구조 정규화, `HorizontalLayoutGroup` 생성은 UI hierarchy 생성 책임이므로 manager 내부에 있으면 런타임 오브젝트 생성 경계가 흐려진다.

변경:

- `IUITabTopButtonFactory` / `UITabTopButtonFactory`를 추가했다.
- 상단 탭 버튼 `Instantiate`, label setup, row child normalization, `HorizontalLayoutGroup` 생성/설정을 factory로 이동했다.
- `UITabManager`는 `IUITabTopButtonFactory`를 VContainer로 주입받고, 기존 버튼 탐색/정렬/탭 바인딩 orchestration만 담당한다.
- `DungeonRuntimeLifetimeScope`에 `UITabTopButtonFactory`를 singleton으로 등록했다.
- runtime/all function inventory와 coupling audit에서 `UITabManager`의 `RuntimeObjectCreation` 플래그를 factory로 이동했다.

검증:

```text
Source scan:
- UITabManager.cs has no direct `new GameObject`, `Instantiate(`, `Destroy(`, `DestroyImmediate`, or `AddComponent<` hits.
- UITabTopButtonFactory.cs owns `Instantiate` and `AddComponent<HorizontalLayoutGroup>`.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False.
- PlayMode UITab probe:
  - UITabManager count=1
  - rootSize=(1920,60)
  - buttons=10
  - horizontal=True
  - horizontalEnabled=True
  - verticalEnabled=False
  - overflowLabels=0
  - ordered labels: 건축, 건물, 직원, 상점, 창고, 운영, 방어, 원정, 연구, 도감
- 2D SceneView capture succeeded after the refactor; gameplay world render remained visible.
- Main Camera capture for UI had an MCP preview failure in one attempt, so UI validation used hierarchy/probe data for overlay UI and camera capture for world visibility.
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False, isCompiling=False.
```

### 154. Notice feed presenter extraction

대상:

- `Assets/Scripts/UI/NoticeFeed.cs`
- `Assets/Scripts/UI/NoticeFeedPresenter.cs`
- `Assets/Scripts/UI/NoticeFeedItemFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- `NoticeFeed`는 `NoticeFeedEvent`를 듣는 UI listener여야 하는데, 기존에는 `ObjectPool<GameObject>` 생성, pool callback 연결, DOTween release sequence까지 직접 관리했다.
- prefab instantiation과 item styling은 이미 `NoticeFeedItemFactory`로 이동했지만, pool ownership과 tween-release 책임이 listener에 남아 있어 이벤트 경계와 UI 수명 관리가 계속 섞여 있었다.

변경:

- `INoticeFeedPresenter` / `NoticeFeedPresenter`를 추가했다.
- notice object pool lookup/creation, pooled item prepare 호출, DOTween display/release sequence를 presenter로 이동했다.
- `NoticeFeed`는 `INoticeFeedPresenter`를 VContainer로 주입받고 `OnTriggerEvent`에서 `Present(textPrefab, transform, e)`만 호출한다.
- `DungeonRuntimeLifetimeScope`에 `NoticeFeedPresenter`를 singleton으로 등록했다.
- runtime/all function inventory와 coupling audit에서 `NoticeFeed`의 SceneMutation 책임을 presenter/factory 경계로 이동했다.

검증:

```text
Source scan:
- NoticeFeed.cs has no `DG.Tweening`, `ObjectPool`, `IObjectPool`, `new ObjectPool`, `DOTween`, `Instantiate`, `Destroy`, or `DestroyImmediate` hits.
- NoticeFeedPresenter.cs owns `IObjectPool<GameObject>`, `new ObjectPool<GameObject>`, and `DOTween.Sequence`.
- NoticeFeedItemFactory.cs still owns prefab `Instantiate` and pooled item `Destroy`.

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful, isCompiling=False.
- PlayMode probe on CharacterAiTestScene:
  - NoticeFeed count=1
  - triggered=1
  - failures=0
  - matchingTexts=1
  - activeMatchingTexts=1
  - warningColorTexts=1
  - feed childProbeTexts=1
  - feed childCount=1
- Camera/scene capture succeeded after the notice-feed probe; gameplay world render remained visible.
- Console Error/Exception/Assert after validation: 0.
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False, isCompiling=False.
```

### 155. Hallway render order and multi-cell footprint validation

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs`
- `Assets/Scripts/Grid/UI/GridPlacementGhostPresenter.cs`
- `Assets/Scripts/Grid/DungeonStory/UI/DungeonStoryGridGhostPresenter.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 사용자 스크린샷 기준으로 복도와 건물 시각 상태가 깨졌는데, 이전 판단은 grid occupant 수치 위주라 실제 카메라 화면에서의 렌더 정렬/프리뷰/콜라이더 문제를 충분히 보지 못했다.
- fresh PlayMode probe에서 일반 시설과 Hallway의 비정상 점유 overlap은 0이었고, 남은 overlap 12칸은 모두 계단/movement였다. 따라서 이번 문제의 핵심은 gameplay occupancy가 아니라 Hallway tilemap의 sorting layer와 multi-cell footprint 시각/클릭 판정이었다.
- `Hallway` tilemap은 `DungeonHallway` sorting layer value -5, order 100으로 Ground/WhiteBox/Wall보다 뒤에 있었고, 방 배경/벽 타일에 묻혀 복도 디버깅이 어려웠다.
- `GridBuildingObjectFactory`는 `height > 1` 건물에도 collider offset y를 항상 1.5로 두고 있어 다층 footprint의 클릭/겹침 판정이 아래층까지 내려갈 수 있었다.
- `GridPlacementGhostPresenter`는 build ghost size를 width와 1셀 높이로만 잡아, `Stair` 같은 2셀 건물의 실제 footprint를 시각적으로 보여주지 못했다.

변경:

- `GridTexture`에 Hallway tilemap sorting configuration을 추가했다. 런타임/OnValidate에서 Hallway tilemap을 `Wall` sorting layer, order `2`로 맞춰 `Ground`/`WhiteBox` 위와 `Wall` 선 아래에 표시한다.
- `GridBuildingObjectFactory`의 collider y size/offset을 `placement.Height * 3f` 기준으로 계산하게 변경했다. 1셀 건물은 기존 `(size.y=2.9, offset.y=1.5)`를 유지하고, 2셀 계단은 `(size.y=5.9, offset.y=3.0)`이 된다.
- `GridPlacementGhostPresenter`에 `GhostHeight`와 `GetGhostFootprintSize()`를 추가하고, single/dragged preview 모두 실제 `height * CellWorldHeight`를 사용하게 했다.
- `DungeonStoryGridGhostPresenter`가 선택 건물의 `height`를 `GhostHeight`로 넘긴다.
- runtime/all function inventory와 coupling audit의 관련 카운트를 갱신했다.

검증:

```text
Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful.
- Fresh PlayMode scene: Assets/Scenes/CharacterAiTestScene.unity.
- Hallway TilemapRenderer after fresh PlayMode:
  - sortingLayer=Wall
  - sortingLayerValue=-1
  - sortingOrder=2
  - tiles=41
- Grid overlap probe:
  - grid=32x3
  - total Building+Hallway overlap cells=12
  - blockedNonMovement overlap cells=0
  - overlap cells were movement/stair cells only.
- Collider footprint probe:
  - Stair heightCells=2, colliderSize=(3.00, 5.90), offset=(0.00, 3.00), ok=True
  - one Door trigger mismatch remains intentional: Door overrides collider as a 1-tile trigger.
- Build ghost footprint probe after selecting Stair id=4:
  - activeRenderers=1
  - Ghost bounds=(3.00, 6.00, 0.20)
- Main Camera capture succeeded after the fix; corridor brick strips were visible above room/background panels.
- Unity Console Error/Exception/Warning: 0.
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False.
```

### 156. Offense expedition button factory extraction and placement ghost visibility cap

대상:

- `Assets/Scripts/Grid/UI/GridGhostObject.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Offense/OffensePanelUiFactory.cs`
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`
- `docs/code-audit/script-coupling-audit.md`

판단:

- 사용자 캡처 기준으로 배치 ghost가 기존 방/복도 위를 강하게 덮어 "복도가 안 보이고 건물이 겹치는" 상태처럼 보였다. PlayMode hierarchy probe에서 `Grid/GridGhostObject/Ghost`는 `sortingLayer=UI`, `sortingOrder=100`, 기존 serialized alpha 0.35 경로였으므로 맵 디버깅을 가릴 수 있었다.
- `OffenseWorldMapPanel`은 이미 `IOffensePanelButtonFactory`를 사용했지만, `OffenseExpeditionPanel`은 여전히 `OffensePanelUiFactory.CreateButton(...)`와 `Destroy(button)`을 직접 호출했다. 패널은 렌더링 책임만 갖고 버튼 생성/해제 수명은 factory가 가져가는 편이 기존 VContainer 경계와 맞다.
- factory로 바꾼 뒤 같은 프레임에서 `Render()`를 반복 호출해 보니, `Destroy()`가 프레임 끝까지 지연되어 버튼 root 아래에 중복이 잠깐 남는 문제도 드러났다. 버튼 release는 즉시 비활성화/부모 분리 후 파괴해야 UI layout과 클릭 판정에 중복이 남지 않는다.

변경:

- `GridGhostObject`에 `PreviewAlphaHardLimit = 0.18f`를 추가했다. 씬에 예전 `maxPreviewAlpha` 값이 serialized로 남아 있어도 runtime preview alpha가 0.18을 넘지 않게 했다.
- `OffenseExpeditionPanel.Bind(...)`가 `ITmpKoreanFontService` 대신 `IOffensePanelButtonFactory`를 받도록 변경했다.
- `OffenseExpeditionPanel.Render()`의 멤버/출발 버튼 생성은 `RequireButtonFactory().CreateButton(...)`으로 이동했고, `ClearButtons()`는 `RequireButtonFactory().Release(...)`를 호출한다.
- `OffenseExpeditionPanel.BindGeneratedView(...)`에서 font service 인자를 제거했다. generated view는 text/root 참조만 들고, 버튼 생성에 필요한 font는 button factory 내부로 들어간다.
- `OffensePanelService`는 expedition panel에도 기존 singleton `IOffensePanelButtonFactory`를 전달한다.
- `OffensePanelButtonFactory.Release(...)`가 button을 즉시 `SetActive(false)` 처리하고 parent에서 분리한 뒤 Destroy/DestroyImmediate를 호출하도록 수정했다.
- runtime/all function inventory와 coupling audit의 관련 줄 수와 책임 설명을 갱신했다.

검증:

```text
Unity MCP:
- MCP connection check: scene=Assets/Scenes/CharacterAiTestScene.unity, loaded=True.
- Fresh PlayMode visual probe:
  - Main Camera capture succeeded.
  - corridor brick strips are visible.
  - runtime grid overlap cells=12, all Building+Hallway overlaps are hallway + Stair movement cells:
    (17,2), (18,2), (19,2), (4,1), (5,1), (6,1), (17,1), (18,1), (19,1), (4,0), (5,0), (6,0)
  - blocked non-movement hallway/building overlap=0.
- Placement ghost probe:
  - forced RestRoom ghost over existing rest-room cell.
  - rendererColor=RGBA(1.000, 0.000, 0.000, 0.180)
  - sortingLayer=UI, sortingOrder=100
  - bounds center=(-9.50, 7.50, 0.00), extents=(1.50, 1.50, 0.10)
  - camera capture after ghost probe still showed corridors/rooms readable.
- DI public-path probe after clean PlayMode:
  - GridUIManager.DrawGrid() OK.
  - CameraManager.ClampToCurrentBounds() OK.
- Offense expedition panel factory-path probe:
  - constructed panel with injected OffensePanelButtonFactory and no container fallback.
  - buttonsAfterBind=1
  - buttonsAfterRender=1
  - buttonsAfterSecondRender=1
  - detachedPendingButtons=2 (expected delayed Destroy objects, already detached from root)
  - panel active=True.
```

### 157. Event alert button lifetime factory extraction and runtime inventory coverage close

대상:

- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`

판단:

- `EventAlertViewPresenter`는 이미 detail panel과 choice presenter를 일부 분리했지만, alert 버튼 생성/갱신과 choice 버튼 생성/해제는 여전히 presenter/choice presenter가 `EventAlertUiFactory`를 직접 호출했다.
- 화면 presenter가 버튼 prefabless GameObject 생성과 delayed Destroy 수명까지 알면 UI 상태 orchestration, layout, runtime object lifetime이 한 군데에 섞인다. 기존 Offense/UITab/NoticeFeed 리팩토링 방향과 맞추려면 버튼 생성/해제는 factory 경계로 내려야 한다.
- 런타임 인벤토리 coverage 재검사에서 현재 비-Editor 스크립트 284개 중 2개가 누락되어 있었다. `all-scripts-function-inventory.md` companion도 runtime 경로 3개가 누락되어 있어 같이 보정했다.

변경:

- `IEventAlertButtonFactory` / `EventAlertButtonFactory`를 추가했다.
- `EventAlertButtonFactory`가 alert 버튼 생성, alert 버튼 갱신, choice 버튼 생성, 버튼 해제를 담당한다.
- `EventAlertChoicePresenter`는 `ITmpKoreanFontService` 대신 `IEventAlertButtonFactory`를 받도록 변경했다.
- `EventAlertViewPresenterFactory`는 `IEventAlertButtonFactory`를 VContainer 생성자 주입으로 받고 `EventAlertViewPresenter`에 전달한다.
- `EventAlertViewPresenter`의 alert 버튼 생성/갱신은 `buttonFactory`로 이동했고, `DestroyRuntimeUI()`는 dynamic alert buttons도 `Release(...)`로 정리한다.
- `DungeonRuntimeLifetimeScope`에 `EventAlertButtonFactory`를 singleton으로 등록했다.
- 런타임 인벤토리에 누락됐던 `SceneBuildableLeakValidator`, `DungeonBackdropSpriteTilingFactory`를 추가했다.
- `all-scripts-function-inventory.md` companion에 `SceneBuildableLeakValidator`, `DungeonBackdropSpriteTilingFactory`, `UITabGeneratedPanelFactory` 누락 섹션을 추가했다.

검증:

```text
Coverage:
- runtime script paths=284
- runtime inventory headings=284
- runtime inventory missing=0
- all-scripts inventory missing runtime paths=0
- runtime inventory declarations=3635

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful.
- PlayMode generated EventAlert probe:
  - EventAlertRuntime constructed with EventAlertButtonFactory -> EventAlertViewPresenterFactory chain.
  - activeAlertButtons=1
  - activeChoiceButtons=2
  - visibleProbeTexts=2
  - choiceResult=True
  - callback marker created=True
  - detailAfterChoice=False
  - activeButtonsAfterDestroy=0
- Main Camera capture succeeded while the probe UI was open. ScreenSpaceOverlay UI is not included in this camera render, so visual UI proof used canvas hierarchy/active RectTransform/Button state while camera capture confirmed the game view remained readable.
- Exited PlayMode after validation: isPlaying=False, isPlayingOrWillChange=False.
```

### 158. Staff work priority panel model extraction

대상:

- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanelModel.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`

판단:

- `StaffWorkPriorityPanel`은 이미 UI GameObject 생성 자체를 `IStaffWorkPriorityPanelUiFactory`로 위임했지만, 직원 조회, 표시 이름 해석, active worker row 구성, 변경 감지 hash 계산을 직접 들고 있었다.
- 화면 컴포넌트가 workforce query와 hash 계산까지 담당하면 패널 테스트/렌더링 검증이 도메인 조회 방식에 묶인다. 패널은 표 렌더링과 priority click orchestration에 집중하고, worker row 모델은 별도 서비스에서 만들도록 분리하는 편이 맞다.

변경:

- `StaffWorkPriorityRowModel`, `IStaffWorkPriorityPanelModelBuilder`, `StaffWorkPriorityPanelModelBuilder`를 추가했다.
- `StaffWorkPriorityPanelModelBuilder`가 `IStaffWorkforceQueryService`를 통해 active worker를 조회하고, display name과 worker hash를 계산한다.
- `StaffWorkPriorityPanel.ConstructStaffWorkPriorityPanel(...)`은 `IStaffWorkforceQueryService` 대신 `IStaffWorkPriorityPanelModelBuilder`를 받는다.
- `StaffWorkPriorityPanel`의 nested `WorkerRow`, `FindWorkers()`, static worker hash 계산을 제거했다.
- `DungeonRuntimeLifetimeScope`에 `StaffWorkPriorityPanelModelBuilder`를 singleton으로 등록했다.
- runtime/all function inventory와 coupling audit을 현재 파일 수/줄 수에 맞춰 갱신했다.

검증:

```text
Coverage:
- runtime script paths=285
- runtime inventory headings=285
- runtime inventory missing=0
- all-scripts inventory missing runtime paths=0
- runtime inventory declarations=3644

Unity MCP:
- AssetDatabase.Refresh after edit: compilation successful.
- PlayMode scene: Assets/Scenes/CharacterAiTestScene.unity.
- Actual scene StaffWorkPriorityPanel found:
  - count=1
  - panel name=직원 관리Tab
  - initial activeSelf=False
  - visibleWorkers=2
  - visibleCells=16
- Activated panel and called Refresh():
  - active=True
  - visibleWorkers=2
  - visibleCells=16
  - textCount=63
  - buttonCount=32
  - activeButtonCount=16
  - rectSize=(1920.00, 420.00)
- Main Camera capture succeeded after activation. ScreenSpaceOverlay UI is not included in the camera render, so UI proof used panel hierarchy/button state while camera capture confirmed the world view remained readable.
- Exited PlayMode after validation.
```

### 159. Grid visual hallway and sprite footprint correction

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/System/Editor/GridVisualDebugScenarios.cs`
- `Assets/Scenes/CharacterAiTestScene.unity`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`
- `docs/code-audit/all-scripts-function-inventory.md`

판단:

- Play 화면에서 복도가 보이지 않고 건물이 겹쳐 보인다는 사용자 지적이 맞다.
- 씬 `initialPlacement`를 계산하면 `CharacterAiTestScene`/`SampleScene` 모두 93 placements, same GridLayer footprint conflicts=0이었다. 즉 논리 점유 충돌보다는 visible hallway tile 숨김과 tilemap anchor 보정 누락이 핵심이었다.
- `GridTexture`는 `Tilemap.tileAnchor=(0.5,0.5)`인 씬에서 스프라이트 건물의 y 중심을 0.5셀 높게 잡고, 짝수 폭 건물의 x 중심 보정도 빠져 있었다. 그래서 실제 footprint와 시각 bounds가 어긋날 수 있었다.

변경:

- `hideHallwayUnderBuildings` 기본값을 `false`로 돌려 PlayMode에서 hallway tile이 기본적으로 계속 보이게 했다.
- `CharacterAiTestScene`의 직렬화 값도 `hideHallwayUnderBuildings: 0`으로 맞췄다.
- 스프라이트 타일 변환을 `GridBuildingTileTransformCalculator`로 분리하고, `Tilemap.tileAnchor`와 짝수 폭 보정을 반영했다.
- sprite tile cache key에 width/height/cellTileHeight/tileAnchor를 포함해 서로 다른 footprint 변환이 섞이지 않게 했다.
- Editor 전용 `GridVisualDebugScenarios`를 추가해 홀수 폭 `P1_RestRoom`과 짝수 폭 `P1_ResearchLab`의 visual footprint 중심/크기를 검증하도록 했다.

검증:

```text
Static scene placement probe:
- CharacterAiTestScene: placements=93, same-layer footprint conflicts=0
- SampleScene: placements=93, same-layer footprint conflicts=0

Unity MCP:
- Attempted Unity_RunCommand after the fix.
- Result: Connection revoked. PlayMode compile/camera capture could not be run from MCP in this turn.
- Therefore this entry does not claim PlayMode visual proof yet; the next MCP-enabled pass must run the new Grid Visual Alignment scenario and capture the corrected scene.
```

## 다음 우선순위 후보

### 160. Grid ghost resolver inventory coverage and visual inset verification

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`
- `Assets/Scripts/Grid/UI/GridGhostObjectResolver.cs`
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`
- `Assets/Scripts/Grid/System/Editor/GridVisualDebugScenarios.cs`
- `Assets/Scenes/CharacterAiTestScene.unity`
- `Assets/Scenes/SampleScene.unity`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- 배치 프리뷰와 실제 건물 스프라이트가 grid cell 높이 3을 꽉 채우면 픽셀아트 층 경계와 맞닿아 아래로 삐져나온 것처럼 보인다.
- `GridPlacementGhostPresenter`가 runtime에서 `GridGhostObject`를 `AddComponent`로 보강하는 fallback은 DI 목표와 맞지 않는다. 고스트 컴포넌트는 씬/프리팹에 명시되어야 하고, resolver는 누락 시 즉시 실패해야 한다.
- coverage 검사에서 현재 비-Editor 런타임 스크립트 286개 중 `GridGhostObjectResolver.cs`가 runtime inventory에 누락되어 있었다.

변경:

- `GridBuildingTileTransformCalculator.GetVisualFootprintSize()`를 추가하고 한 칸 높이 3의 시각 높이를 2.5로 줄여 위아래 0.25 cell 여백을 둔다.
- `GridGhostObject`도 같은 visual footprint 계산을 사용해 실제 건물과 preview bounds를 맞춘다.
- `GridGhostObject` preview sorting을 `DungeonFrontObject`/`200`으로 고정하고, `CharacterAiTestScene`/`SampleScene` 직렬화 값도 저장했다.
- `GridGhostObjectResolver.cs`를 runtime function inventory에 추가하고 coverage 메모를 286/286으로 갱신했다.

검증:

```text
Coverage:
- runtime script paths=286
- runtime inventory headings=286
- runtime inventory missing=0
- runtime inventory declarations=3651

Unity MCP:
- GridFoundationDebugScenarios=True
- GridVisualDebugScenarios=True
- One-cell visual footprint height=2.5
- Fresh PlayMode scene: Assets/Scenes/CharacterAiTestScene.unity
- Console Error/Warning: 0
- MainCamera: pos=(-10.00, 4.50, -10.00), size=8.4375
- Tilemap renderers: BuildingBack/Front on DungeonBackObject/DungeonFrontObject, Wall on Wall, Hallway on DungeonHallway
- Main Camera capture succeeded after inset change.
- Exited PlayMode after validation.
```

### 161. Grid sprite tilemap y-offset correction

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/System/Editor/GridVisualDebugScenarios.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- PlayMode 런타임 bounds 확인 결과 `BuildingBackWidth`의 generated runtime sprite tiles가 `worldY=[-0.25,2.25]`로 셀 바닥 `0` 아래로 삐져나왔다.
- 원인은 `BuildingBack`/`BuildingFront` tilemap의 `localPosition.y=-0.5`를 `GridBuildingTileTransformCalculator`가 반영하지 않았기 때문이다. 단순히 sprite visual height를 2.5로 줄이는 것만으로는 타일맵 자체 y 오프셋 때문에 월드 기준 중심이 0.5 내려간다.

변경:

- sprite runtime tile cache key에 `tilemapLocalYOffset`을 포함해 같은 sprite/footprint라도 tilemap y 오프셋이 다르면 별도 transform을 쓰게 했다.
- `GridBuildingTileTransformCalculator.Calculate(..., tilemapLocalYOffset)` overload를 추가하고, desired center 계산에서 tilemap local y offset을 보정했다.
- `GridVisualDebugScenarios`가 `tilemapLocalYOffset=-0.5` 조건에서도 world center y=1.5, visual height=2.5를 검증하게 했다.
- runtime function inventory와 coupling audit의 `GridTexture.cs` 라인 수/함수 목록/declaration count를 갱신했다.

검증:

```text
Unity MCP:
- isPlaying=False, isCompiling=False before scenario run.
- GridVisualDebugScenarios=True
- GridFoundationDebugScenarios=True
- Fresh PlayMode scene: Assets/Scenes/CharacterAiTestScene.unity
- Before correction, sampled runtime generated tile: facility_warehouse worldY=[-0.25,2.25]
- After correction, sampled runtime generated tiles:
  - facility_warehouse_4x1_RuntimeTile worldY=[0.25,2.75]
  - facility_food_basic_3x1_RuntimeTile worldY=[3.25,5.75]
  - facility_rest_room_3x1_RuntimeTile worldY=[6.25,8.75]
- RuntimeTileCount=7
- minRuntimeBuildingY=0.25
- Console Error/Warning: 0
- MainCamera capture succeeded after y-offset correction.
- Exited PlayMode after validation.
```

### 162. Facility evolution record component service boundary

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecord.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `FacilityEvolutionRecordRuntime.GetOrAddRecord()`와 `FacilityEvolutionEngine.CopyRecordToResult()`가 각각 `FacilityEvolutionRecordComponent`를 직접 `AddComponent` 하고 있었다.
- 기록 컴포넌트 생성은 시설 진화 이벤트 누적/진화 판정의 도메인 로직이 아니라 runtime component storage 경계다. 이 책임이 런타임/엔진에 흩어져 있으면 테스트 fixture, 결과 건물 생성, VContainer 주입 요구가 서로 어긋난다.

변경:

- `IFacilityEvolutionRecordComponentService`와 `FacilityEvolutionRecordComponentService`를 추가했다.
- `FacilityEvolutionRecordRuntime`은 `IFacilityEvolutionRecordComponentService`를 VContainer로 주입받아 기록 컴포넌트를 얻는다.
- `FacilityEvolutionEngine`은 결과 건물 기록 복사를 `recordComponentService.ReplaceWith()`로 위임하고 직접 `AddComponent` 하지 않는다.
- `FacilityEvolutionRuntime`은 `IFacilityEvolutionRecordComponentService`를 주입/명시 구성 받도록 확장했다.
- `DungeonRuntimeLifetimeScope`에 `FacilityEvolutionRecordComponentService`를 `IFacilityEvolutionRecordComponentService`와 `IFacilityEvolutionRecordProvider`로 등록했다.
- 에디터 시나리오의 수동 fixture는 `BuildableObject`와 `FacilityEvolutionRuntime`에 필요한 의존성을 명시적으로 주입하도록 보강했다.

검증:

```text
Unity MCP:
- isPlaying=False, isCompiling=False, isUpdating=False before scenario run.
- First run exposed missing fixture DI:
  - BuildableObject requires IFacilityCandidateCache injection.
  - FacilityEvolutionRuntime requires IFacilityEvolutionRecordTokenConsumer / IBlueprintResearchStateService.
- Fixture DI was fixed instead of weakening runtime requirements.
- FacilityEvolutionDebugScenarios=True after the fix.

Inventory:
- runtime script paths=286
- runtime inventory headings=286
- runtime inventory missing=0
- runtime inventory declarations=3669
```

### 163. Grid visual one-cell bounds inset tightening

대상:

- `Assets/Scripts/Grid/Rendering/GridTexture.cs`
- `Assets/Scripts/Grid/System/Editor/GridVisualDebugScenarios.cs`
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`

판단:

- 한 칸짜리 시설/고스트가 셀 높이 3을 거의 끝까지 쓰면 바닥/천장 경계와 맞닿아 실제 화면에서 아래로 삐져나온 것처럼 보인다.
- 기존 0.25 inset 검증은 center/size 위주라 실제 min/max bounds가 한 셀 내부 밴드에 들어오는지 직접 보장하지 못했다.

변경:

- `GridBuildingTileTransformCalculator.DefaultVerticalVisualInset`을 0.5로 조정해 한 칸 시각 bounds 목표를 `y=[0.5,2.5]`로 좁혔다.
- `GridVisualDebugScenarios`가 transformed bounds의 min/max x/y를 직접 검증하게 했다.
- `GridFoundationDebugScenarios`의 repeated ghost 검증도 renderer bounds min/max y를 같은 기준으로 확인하게 했다.

검증:

```text
Static:
- git diff --check scoped to grid visual files: clean, CRLF warnings only.
- One-cell target bounds are asserted as minY=0.5, maxY=2.5 by calculator/debug scenario checks.

Unity:
- Unity MCP was unavailable in the previous tool list during this specific tightening.
- Batch Unity 6000.3.8f1 did not emit the requested log and spawned two extra Unity processes; those were cleaned up.
- No PlayMode/camera proof is claimed for this tightening entry.
```

### 164. Dungeon scene component query loaded-scene traversal

대상:

- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `Resources.FindObjectsOfTypeAll` 기반 scene DI lookup은 asset/prefab까지 포함할 수 있어 loaded scene component 조회 경계로 과하다.
- `IDungeonSceneComponentQuery`는 VContainer로 주입되는 infrastructure boundary이므로, Unity 전역 object scan 대신 로드된 씬 root traversal만 수행하는 편이 책임이 선명하다.

변경:

- `DungeonSceneComponentQuery`를 `SceneManager.sceneCount`/loaded scene root traversal 기반으로 변경했다.
- `All<T>()`는 root별 `GetComponentsInChildren<T>(includeInactive)` 결과를 `GetInstanceID()`로 de-dupe한다.
- `First<T>()`는 같은 traversal을 사용하되 첫 component에서 즉시 반환한다.
- runtime function inventory/coupling audit에서 `DungeonSceneComponentQuery`의 `GlobalObjectLookup`, `ResourcesAccess`, `SceneMutation` 플래그를 제거했다.

검증:

```text
Static:
- Non-Editor runtime scan for Resources.FindObjectsOfTypeAll / FindObjectsOfType / FindFirstObjectByType / FindAnyObjectByType: 0 results.
- DungeonSceneComponentQuery.cs lines=67, flags=None.
- Runtime counts remain: files=286, declarations=3669.

Coupling:
- GlobalObjectLookup: 1 -> 0
- ResourcesAccess: 2 -> 1
- SceneMutation: 79 -> 78

Unity MCP:
- MCP connection restored and verified through Unity_RunCommand.
- Unity=6000.3.8f1, active scene=CharacterAiTestScene, isPlaying=False.
- Console check after reconnect: Error/Exception=0, Warning=7.
- Remaining warnings are Behavior Designer obsolete API warnings plus Unity relay/codex signature warnings; no project compile error was reported.
```

### 165. Building summary formatter and popup DI boundary

대상:

- `Assets/Scripts/UI/BuildingSummaryInfo.cs`
- `Assets/Scripts/Buildings/BuildingSummaryFormatter.cs`
- `Assets/Scripts/Buildings/BuildingSummaryFormatter.cs.meta`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `BuildingSummaryInfo`는 UI popup인데 건물 데이터 조회, shop/warehouse 타입 분기, 재고 문구 조립을 직접 들고 있었다.
- 과거 구현은 `DataManager.Instance`, `UIManager.Instance`, static font helper에 붙어 있었고, 이후 DI가 들어온 상태에서도 표시 모델 조립 책임이 view에 남아 있었다.
- 건물 요약 view는 이벤트 수신/팝업 표시/텍스트 바인딩만 맡고, 도메인별 표시 문구는 별도 service에서 만드는 편이 책임이 선명하다.

변경:

- `BuildingSummaryPresentation`, `IBuildingSummaryFormatter`, `BuildingSummaryFormatter`를 추가했다.
- `BuildingSummaryFormatter`가 `IBuildingDefinitionLookup`으로 건물 이름을 얻고, shop/warehouse 재고 문구를 만든다.
- `BuildingSummaryInfo`는 `IUiPopupService`, `IBuildingSummaryFormatter`, `ITmpKoreanFontService`를 VContainer로 받도록 정리했다.
- `BuildingSummaryInfo`에서 직접 `DataManager.Instance`, `UIManager.Instance`, `TMPKoreanFont.ApplyToChildren` 사용을 제거했다.
- 누락된 UI/root/text 참조는 조용히 fallback하지 않고 `InvalidOperationException`으로 실패하게 했다.
- `DungeonRuntimeLifetimeScope`에 `BuildingSummaryFormatter`를 `IBuildingSummaryFormatter`로 등록했다.

검증:

```text
Static:
- BuildingSummaryInfo.cs direct singleton/static lookup scan: 0 code hits.
- runtime script paths=287
- runtime inventory headings=287
- runtime inventory declarations=3680
- git diff --check scoped to changed code/docs: clean, CRLF warnings only.

Unity MCP:
- AssetDatabase.Refresh after new script/meta: compile success, isCompiling=False, isUpdating=False.
- Console after refresh: Error/Warning=0.
- Entered PlayMode in CharacterAiTestScene.
- BuildingSummary event smoke:
  - isPlaying=True
  - building=문, gridId=1
  - uiActive=True
  - hasNameText=True
  - hasStockReference=True
- MainCamera capture succeeded: 1920x1080, camera instance=64284.
- Capture limitation: Main Camera capture does not include Screen Space Overlay popup UI, so visual proof is scene-render proof; popup UI proof is the PlayMode event smoke above.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False.
- Final console check: Error/Warning=0.
```

### 166. Defense status runtime factory boundary

대상:

- `Assets/Scripts/Defense/DefenseStatusRuntimeService.cs`
- `Assets/Scripts/Defense/DefenseStatusRuntimeFactory.cs`
- `Assets/Scripts/Defense/DefenseStatusRuntimeFactory.cs.meta`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `DefenseStatusRuntimeService`가 상태 tick orchestration과 `DefenseStatusRuntime` 컴포넌트 부착을 동시에 맡고 있었다.
- 상태 서비스는 `Get/GetOrAdd/TickStatuses`의 정책 경계로 남기고, `AddComponent<DefenseStatusRuntime>`는 명시적인 factory 경계로 옮기는 편이 책임이 더 선명하다.
- 기존 Defense editor fixture는 VContainer를 거치지 않고 캐릭터/시설을 직접 생성하므로, 런타임 DI 요구사항이 강화될수록 fixture가 실제 계약을 따라오지 못했다.

변경:

- `IDefenseStatusRuntimeFactory`, `DefenseStatusRuntimeFactory`를 추가했다.
- `DefenseStatusRuntimeService`는 `IDefenseStatusRuntimeFactory`를 생성자 주입받아 컴포넌트 조회/부착을 위임한다.
- `DungeonRuntimeLifetimeScope`에 `DefenseStatusRuntimeFactory`를 `IDefenseStatusRuntimeFactory`로 등록하고, 기존 `DefenseStatusRuntimeService` 등록 앞에 배치했다.
- `DefenseFacilityDebugScenarios`는 수동 생성한 `AbilityWork`, `BuildableObject`, `CharacterStats`에 필요한 fixture 서비스를 명시적으로 주입하도록 보강했다.
- runtime inventory/coupling audit에서 `DefenseStatusRuntimeService`의 `RuntimeObjectCreation/SceneMutation` 플래그를 제거하고, 새 factory 파일을 해당 경계로 기록했다.

검증:

```text
Static:
- AddComponent<DefenseStatusRuntime> scan: DefenseStatusRuntimeFactory.cs only.
- new DefenseStatusRuntimeService(...) scan: Defense editor fixture only, with explicit DefenseStatusRuntimeFactory.
- git diff --check scoped to changed code/docs: clean, CRLF warnings only.
- runtime script paths=288
- runtime inventory headings=288
- runtime inventory declarations=3685

Unity MCP:
- AssetDatabase.Refresh after new factory/meta and fixture patches: compile success, isCompiling=False, isUpdating=False.
- DefenseFacilityDebugScenarios.RunAll(false): initially exposed missing fixture DI for AbilityWork, BuildableObject, CharacterStats.
- Fixture DI was fixed instead of weakening runtime requirements.
- DefenseFacilityDebugScenarios.RunAll(false) final result: passed=True.
- Entered PlayMode in CharacterAiTestScene.
- DefenseStatusRuntime smoke:
  - actor=테스트 직원
  - hadBefore=False
  - afterRuntime=True
  - sameResolved=True
  - tickDamage=0
- Camera capture by specific camera instance 64290 failed because the MCP capture tool could not resolve that instance ID.
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False.
- Console caveat: one remaining Error log is the known MCP camera-instance capture failure, not a project compile/runtime error.
```

### 167. Character runtime component factory boundaries

대상:

- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`
- `Assets/Scripts/Character/AI/CharacterSocialMemoryFactory.cs`
- `Assets/Scripts/Character/AI/CharacterSocialMemoryFactory.cs.meta`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleFactory.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleFactory.cs.meta`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `CharacterSocialMemoryService`와 `CharacterFeedbackBubbleService`가 각각 `IObjectResolver`를 알고, 컴포넌트가 없으면 `AddComponent`를 수행하고, 주입까지 처리하고 있었다.
- 두 서비스는 캐릭터 런타임 보조 컴포넌트를 “얻는다”는 정책 경계로 충분하고, 컴포넌트 부착/DI 주입/초기 바인딩은 Factory 경계가 맡는 편이 책임이 선명하다.
- 이 패턴은 166번 Defense status runtime factory 분리와 같은 결이며, CharacterActor 주입 시 자동 보강되는 런타임 컴포넌트 경로를 더 명시적으로 만든다.

변경:

- `ICharacterSocialMemoryFactory`, `CharacterSocialMemoryFactory`를 추가했다.
- `ICharacterFeedbackBubbleFactory`, `CharacterFeedbackBubbleFactory`를 추가했다.
- `CharacterSocialMemoryService`는 `ICharacterSocialMemoryFactory`를 주입받아 `GetOrAdd`를 위임한다.
- `CharacterFeedbackBubbleService`는 `ICharacterFeedbackBubbleFactory`를 주입받아 `GetOrAdd`를 위임한다.
- `DungeonRuntimeLifetimeScope`에 두 factory를 service보다 먼저 VContainer singleton으로 등록했다.
- Unity가 만든 최소 `.meta`에 `MonoImporter` 블록을 채워 script meta 형식을 맞췄다.
- runtime inventory/coupling audit에서 두 service의 `RuntimeObjectCreation/SceneMutation/DependencyInjection` 플래그를 제거하고, 새 factory 파일을 해당 경계로 기록했다.

검증:

```text
Static:
- CharacterSocialMemoryService.cs lines=22, flags=None.
- CharacterFeedbackBubbleService.cs lines=22, flags=None.
- AddComponent<CharacterSocialMemory> remains in CharacterSocialMemoryFactory.cs.
- AddComponent<CharacterFeedbackBubble> remains in CharacterFeedbackBubbleFactory.cs.
- git diff --check scoped to changed code/docs: clean.
- runtime script paths=290
- runtime inventory headings=290
- runtime inventory declarations=3693

Unity MCP:
- AssetDatabase.Refresh after new factory/meta files: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- Character runtime component smoke:
  - actor=테스트 직원
  - socialBefore=True
  - socialAfter=True
  - socialSame=True
  - bubbleBefore=True
  - bubbleAfter=True
  - bubbleSame=True
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False.
```

### 168. Facility evolution state component factory boundary

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionStateComponentFactory.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionStateComponentFactory.cs.meta`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `FacilityEvolutionStateService`가 진화 상태 접근 정책과 `FacilityEvolutionStateComponent` 부착을 동시에 맡고 있었다.
- 상태 서비스는 `GetOrAdd` 정책 경계로 남기고, 컴포넌트 생성/초기화는 별도 factory가 맡는 편이 166/167번에서 정리한 runtime component boundary와 일관된다.
- `FacilityEvolutionDebugScenarios`는 VContainer를 거치지 않는 editor fixture라 새 생성자 계약을 명시적으로 따라야 한다.

변경:

- `IFacilityEvolutionStateComponentFactory`, `FacilityEvolutionStateComponentFactory`를 추가했다.
- `FacilityEvolutionStateService`는 `IFacilityEvolutionStateComponentFactory`를 생성자 주입받아 `GetOrAdd`를 위임한다.
- `DungeonRuntimeLifetimeScope`에 `FacilityEvolutionStateComponentFactory`를 `IFacilityEvolutionStateComponentFactory`로 등록하고, 기존 state service 등록 앞에 배치했다.
- `FacilityEvolutionDebugScenarios`의 수동 fixture 두 곳도 `new FacilityEvolutionStateService(new FacilityEvolutionStateComponentFactory())`로 갱신했다.
- runtime inventory/coupling audit에서 `FacilityEvolutionState.cs`의 `RuntimeObjectCreation/SceneMutation` 플래그를 제거하고, 새 factory 파일을 해당 경계로 기록했다.

검증:

```text
Static:
- AddComponent<FacilityEvolutionStateComponent> scan: FacilityEvolutionStateComponentFactory.cs only.
- new FacilityEvolutionStateService(...) scan: editor fixture only, with explicit FacilityEvolutionStateComponentFactory.
- git diff --check scoped to changed code/docs: clean.
- runtime script paths=291
- runtime inventory headings=291
- runtime inventory declarations=3697

Unity MCP:
- Initial PlayMode attempt exposed compile errors in FacilityEvolutionDebugScenarios fixture constructors.
- Fixture constructors were fixed instead of weakening the runtime service contract.
- AssetDatabase.Refresh after fixture fix: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- Facility evolution state smoke:
  - facility=복도
  - before=False
  - after=True
  - same=True
  - baseId=복도
  - currentId=복도
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False.
```

### 169. Facility evolution record component factory boundary

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecord.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordComponentFactory.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordComponentFactory.cs.meta`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `FacilityEvolutionRecordComponentService`가 기록 조회/교체 정책과 `FacilityEvolutionRecordComponent` 부착을 동시에 맡고 있었다.
- 기록 서비스는 `GetRecord`, `GetOrAdd`, `ReplaceWith` 정책 경계로 남기고, 컴포넌트 생성은 별도 factory가 맡는 편이 166~168번 runtime component boundary와 일관된다.
- `FacilityEvolutionDebugScenarios`는 VContainer를 거치지 않는 editor fixture라 새 생성자 계약을 명시적으로 따라야 한다.

변경:

- `IFacilityEvolutionRecordComponentFactory`, `FacilityEvolutionRecordComponentFactory`를 추가했다.
- `FacilityEvolutionRecordComponentService`는 `IFacilityEvolutionRecordComponentFactory`를 생성자 주입받아 `GetOrAdd`를 위임한다.
- `DungeonRuntimeLifetimeScope`에 record factory를 record service보다 먼저 VContainer singleton으로 등록했다.
- `FacilityEvolutionDebugScenarios`의 수동 fixture 네 곳도 명시적인 record factory를 전달하도록 갱신했다.
- runtime inventory/coupling audit에서 `FacilityEvolutionRecord.cs`의 `RuntimeObjectCreation/SceneMutation` 플래그를 제거하고, 새 factory 파일을 해당 경계로 기록했다.

검증:

```text
Static:
- AddComponent<FacilityEvolutionRecordComponent> runtime scan: FacilityEvolutionRecordComponentFactory.cs only.
- Editor fixture still has editor-only AddComponent at FacilityEvolutionDebugScenarios.cs.
- new FacilityEvolutionRecordComponentService(...) scan: editor fixture only, with explicit FacilityEvolutionRecordComponentFactory.
- git diff --check scoped to changed code/docs: clean.
- runtime script paths=292
- runtime inventory headings=292
- runtime inventory declarations=3701

Unity MCP:
- AssetDatabase.Refresh after new factory/meta files: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- Facility evolution record smoke:
  - facility=복도
  - before=False
  - after=True
  - same=True
  - metric=7.5
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False.
```

### 170. Run result panel factory file boundary

대상:

- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`
- `Assets/Scripts/Meta/RunResultPanelFactory.cs`
- `Assets/Scripts/Meta/RunResultPanelFactory.cs.meta`
- `Assets/Scripts/Meta/Editor/RunResultPanelFactoryDebugScenarios.cs`
- `Assets/Scripts/Meta/Editor/RunResultPanelFactoryDebugScenarios.cs.meta`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `MetaProgressionRunResultServices.cs`에 런 결과 snapshot 빌드, 패널 표시 정책, Canvas/Text 오브젝트 생성 factory가 함께 들어 있었다.
- 생성 책임은 이미 `IRunResultPanelFactory`로 분리되어 있었지만, 파일 단위 감사에서는 결과 계산/표시 정책 파일이 계속 `RuntimeObjectCreation/SceneMutation/DependencyInjection` 후보로 잡혔다.
- 생성 factory를 별도 파일로 옮기면 런 결과 계산/표시 정책과 UI 오브젝트 생성 경계가 더 선명해진다.

변경:

- `IRunResultPanelFactory`, `RunResultPanelFactory`를 `RunResultPanelFactory.cs`로 이동했다.
- `MetaProgressionRunResultServices.cs`는 `MetaRunResultBuilder`, `IRunResultPanelService`, `RunResultPanelService`만 남도록 정리했다.
- `RunResultPanelFactoryDebugScenarios`를 Editor 전용 PlayMode smoke helper로 추가했다.
- `CharacterAiTestScene`의 기존 `DungeonRuntimeSystems` 스코프가 활성 상태인지 확인하는 helper를 추가했다.
- runtime inventory/coupling audit에서 `MetaProgressionRunResultServices.cs`의 생성/DI 플래그를 제거하고, 새 factory 파일을 생성 경계로 기록했다.

검증:

```text
Static:
- MetaProgressionRunResultServices.cs no longer contains new GameObject, AddComponent<RunResultPanel>, IObjectResolver, TMPro/UI/VContainer usings.
- RunResultPanelFactory.cs owns RunResultPanel Canvas/Text creation and IObjectResolver injection.
- runtime script paths=293
- runtime inventory headings=293
- runtime inventory declarations=3704
- scoped trailing-whitespace scan on changed code/docs: clean.

Unity MCP:
- AssetDatabase.Refresh after split/helper/meta updates: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- Initial smoke exposed that no active runtime container was visible to the smoke helper.
- Existing scene scope was confirmed active: DungeonRuntimeSystems.
- PlayMode smoke via VContainer:
  - scope=DungeonRuntimeSystems
  - factoryResolved=True
  - serviceResolved=True
  - panel=RunResultPanel
  - active=True
  - text=True
  - ownerText=True
  - canvasConfigured=True
  - textLength=205
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False, isUpdating=False.
- Console Error/Warning count: 0.
```

### 171. Offense panel factory file boundary

대상:

- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`
- `Assets/Scripts/Offense/OffensePanelFactory.cs`
- `Assets/Scripts/Offense/OffensePanelFactory.cs.meta`
- `Assets/Scripts/Offense/Editor/OffensePanelFactoryDebugScenarios.cs`
- `Assets/Scripts/Offense/Editor/OffensePanelFactoryDebugScenarios.cs.meta`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `OffenseRuntimeServices.cs`에 provider/query/catalog/panel-service 정책과 world-map/expedition 패널 Canvas 생성 factory가 함께 있었다.
- 생성 책임은 `IOffensePanelFactory`로 추상화되어 있었지만 파일 단위로는 서비스 파일이 계속 `RuntimeObjectCreation/DependencyInjection` 후보로 남았다.
- `OffensePanelUiFactory`가 저수준 UI primitive 생성을 맡고 있으므로, `OffensePanelFactory`는 조합/패널 컴포넌트 부착/DI 주입의 명시적인 factory 경계가 되는 편이 자연스럽다.

변경:

- `IOffensePanelFactory`, `OffensePanelFactory`를 `OffensePanelFactory.cs`로 이동했다.
- `OffenseRuntimeServices.cs`는 runtime provider, expedition member query, reward catalog, panel service만 남도록 정리했다.
- `OffensePanelFactoryDebugScenarios`를 Editor 전용 PlayMode smoke helper로 추가했다.
- runtime inventory/coupling audit에서 `OffenseRuntimeServices.cs`의 생성/DI 플래그를 제거하고, 새 factory 파일을 생성/scene/DI 경계로 기록했다.

검증:

```text
Static:
- OffenseRuntimeServices.cs no longer contains new GameObject, AddComponent<Offense*Panel>, IObjectResolver, TMPro/UnityEngine/VContainer usings.
- OffensePanelFactory.cs owns world-map/expedition panel component creation and IObjectResolver injection.
- runtime script paths=294
- runtime inventory headings=294
- runtime inventory declarations=3704
- scoped trailing-whitespace scan on changed code/docs: clean.

Unity MCP:
- AssetDatabase.Refresh after split/helper/meta updates: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- PlayMode smoke via VContainer:
  - scope=DungeonRuntimeSystems
  - factoryResolved=True
  - serviceResolved=True
  - world=(panel=OffenseWorldMapPanel, active=True, textCount=2, canvasConfigured=True)
  - expedition=(panel=OffenseExpeditionPanel, active=True, textCount=2, canvasConfigured=True)
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False, isUpdating=False.
- Console Error/Warning count: 0.
```

### 172. Facility evolution building replacer factory boundary

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionBuildingReplacerFactory.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionBuildingReplacerFactory.cs.meta`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionBuildingReplacerFactoryDebugScenarios.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionBuildingReplacerFactoryDebugScenarios.cs.meta`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `FacilityEvolutionRuntime.CreateEngine()`이 진화 런타임 오케스트레이션과 `GridBuildingFactory` 조립, `GridTexture` 참조, `IGridBuildingObjectFactory`, 생성된 `BuildableObject`에 대한 `IObjectResolver.Inject` 책임을 함께 들고 있었다.
- 시설 교체 자체는 `IFacilityEvolutionBuildingReplacer` 정책 경계로 이미 추상화되어 있었으므로, 그 replacer를 만드는 composition/DI 책임은 runtime이 아니라 별도 factory가 맡는 편이 맞다.
- 이 분리는 런타임이 `IObjectResolver`, `IGridTextureProvider`, `IGridBuildingObjectFactory`를 직접 알아야 하는 결합을 줄인다.

변경:

- `IFacilityEvolutionBuildingReplacerFactory`, `GridFacilityEvolutionBuildingReplacerFactory`를 추가했다.
- 새 factory가 `GridBuildingFactory(gridTextureProvider.Texture, InjectCreatedBuilding, gridBuildingObjectFactory)` 조립과 생성 건물 DI 주입 콜백을 맡는다.
- `FacilityEvolutionRuntime`은 `IFacilityEvolutionBuildingReplacerFactory`를 주입받고, 기본 replacer가 없을 때 `ResolveBuildingReplacerFactory().Create()`만 호출한다.
- `DungeonRuntimeLifetimeScope`에 `GridFacilityEvolutionBuildingReplacerFactory`를 singleton으로 등록했다.
- `FacilityEvolutionBuildingReplacerFactoryDebugScenarios`를 Editor 전용 PlayMode smoke helper로 추가했다.
- runtime inventory/coupling audit에서 `FacilityEvolutionRuntime.cs`의 `RuntimeObjectCreation` 플래그를 제거하고, 새 factory 파일을 생성/DI 경계로 기록했다.

검증:

```text
Static:
- FacilityEvolutionRuntime.cs no longer contains IObjectResolver, IGridTextureProvider, IGridBuildingObjectFactory, ResolveGridTextureProvider, ResolveObjectResolver, ResolveGridBuildingObjectFactory, or new GridBuildingFactory.
- GridFacilityEvolutionBuildingReplacerFactory owns GridBuildingFactory composition and created-building injection.
- runtime script paths=295
- runtime inventory headings=295
- runtime inventory declarations=3706
- scoped trailing-whitespace scan on changed code/docs: clean.
- git diff --check scoped to changed code/docs: clean.

Unity MCP:
- AssetDatabase.Refresh after split/helper/meta updates: compile success, isPlaying=False, isCompiling=False, isUpdating=False.
- Entered PlayMode in CharacterAiTestScene.
- Initial smoke exposed that CharacterAiTestScene has no scene FacilityEvolutionRuntime, while the factory itself resolved and created correctly.
- Smoke helper was tightened to create a temporary FacilityEvolutionRuntime and verify VContainer injection when no scene runtime exists.
- PlayMode smoke via VContainer:
  - scope=DungeonRuntimeSystems
  - factoryResolved=True
  - runtimeProviderResolved=True
  - replacer=GridFacilityEvolutionBuildingReplacer
  - rejectsNull=True
  - runtime=FacilityEvolutionRuntimeSmoke
  - runtimeReason=InvalidOperationException
  - temporaryRuntimeInjected=True
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False, isUpdating=False.
- Console Error/Warning count: 0.
```

### 173. Facility evolution record event recorder boundary

대상:

- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordEventRecorder.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordEventRecorder.cs.meta`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionRecordEventRecorderDebugScenarios.cs`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionRecordEventRecorderDebugScenarios.cs.meta`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`
- `docs/code-audit/runtime-scripts-function-inventory.md`
- `docs/code-audit/runtime-script-coupling-audit.md`

판단:

- `FacilityEvolutionRecordRuntime`이 EventBus 구독 lifecycle, 방문/매출/재고/범죄/방어/침공 이벤트 해석, 일일 기록 캐시, 방문자 성향 분류, 토큰/메트릭 계산을 한 MonoBehaviour 안에 함께 들고 있었다.
- 이벤트 구독은 씬 컴포넌트 책임으로 남겨도 되지만, 기록 계산과 캐시 상태는 씬 lifecycle과 분리된 서비스가 맡아야 테스트와 VContainer 검증이 쉽다.
- 기록 컴포넌트 생성은 이미 `IFacilityEvolutionRecordComponentService`/factory 경계로 분리되어 있으므로, 이번 분리는 그 위의 이벤트-기록 변환 책임을 낮추는 작업이다.

변경:

- `IFacilityEvolutionRecordEventRecorder`, `FacilityEvolutionRecordEventRecorder`를 추가했다.
- 방문자 unique/repeat 추적, 일일 clean streak 판정, stock consumed 누적, 범죄/방어/침공 기록, visitor mood/combat/noble 분류를 recorder로 이동했다.
- `FacilityEvolutionRecordRuntime`은 serialized threshold 값을 보유하고 `OnEnable/OnDisable` 이벤트 구독과 typed event delegation만 수행하게 줄였다.
- `DungeonRuntimeLifetimeScope`에 `FacilityEvolutionRecordEventRecorder`를 singleton으로 등록했다.
- `FacilityEvolutionDebugScenarios`의 수동 runtime 생성 경로를 새 recorder 주입 방식으로 수정했다.
- `FacilityEvolutionRecordEventRecorderDebugScenarios`를 Editor 전용 PlayMode smoke helper로 추가했다.
- runtime inventory/coupling audit에서 새 recorder 파일을 추가하고 `FacilityEvolutionRecordRuntime.cs`를 98 lines / EventBus+DI 경계로 갱신했다.

검증:

```text
Static:
- FacilityEvolutionRecordRuntime.cs is 98 lines / 13 declarations and no longer contains uniqueVisitorsByFacility, dayRecords, stockConsumedByFacility, IFacilityCandidateCache, IFacilityEvolutionRecordComponentService, ResolveFacilityCandidateCache, ResolveRecordComponentService, or visitor classification helpers.
- FacilityEvolutionRecordEventRecorder.cs owns the record-event conversion logic and receives IFacilityCandidateCache + IFacilityEvolutionRecordComponentService through constructor injection.
- runtime script paths=296
- runtime inventory headings=296
- runtime inventory declarations=3716

Unity MCP:
- Compile check after split/helper import: compilation success, isPlaying=False.
- FacilityEvolutionDebugScenarios.RunAll(log: true): passed=True.
- Entered PlayMode in CharacterAiTestScene without saving the dirty scene.
- PlayMode smoke via VContainer:
  - scope=DungeonRuntimeSystems
  - recorderResolved=True
  - recordsResolved=True
  - runtimeInjected=True
  - building=P1_BattleDining
  - visits=3
  - revenue=45
  - cleanToken=1
  - meatToken=2
- Default Unity_Camera_Capture succeeded as Scene View capture: 1920x1080, scene render visible.
- Visual object probe found one active ScreenSpaceOverlay UI canvas and transparent large touch/notice guards; no opaque generated smoke panel remained.
- Direct Main Camera capture by instance id failed because the MCP capture tool could not resolve the camera instance id; this produced one MCP tool error log, not a game-code compile/runtime failure.
- Exited PlayMode and confirmed isPlaying=False, pending=False, isCompiling=False, isUpdating=False.
```

1. 런타임 객체 생성/이벤트 경계 정리
   - `EventAlertSystem`/`EventAlertUiFactory`의 알림 오케스트레이션과 UI 생성 경계, `FacilityEvolutionRuntime`의 이벤트/진화 오케스트레이션, `SocialReputationRuntime`의 대형 서비스 책임처럼 아직 도메인 런타임과 이벤트/상태/표시 책임이 엮인 후보를 계속 낮춘다.
   - 단, 이미 factory/service 경계 안에 있는 `RuntimePanelFactories`, `UITabGeneratedPanelFactory`, `NoticeFeedItemFactory`류는 낮은 우선순위로 본다.
