# 확장성 아키텍처 설계 감사

- 작성일: 2026-07-16
- 범위: `Assets/Scripts` 런타임 코드, 관련 Editor 빌더/드로어, `BuildingSO` 직렬화 자산, 저장 구조, `ProjectSettings/EditorSettings.asset`
- 목적: 최근 제거한 고정형 `BuildingSO` 능력 필드와 같은 종류의 설계 미스를 찾아 실제 런타임 위험과 확장 비용으로 우선순위를 정한다.
- 제외: 단순히 파일이 크거나 enum/switch가 있다는 이유만으로는 결함으로 분류하지 않았다. 닫힌 상태 머신, 전송용 enum, 응집된 레시피 DTO는 제외했다.

## 결론

확정된 핵심 문제는 개별 클래스의 코드 스타일보다 **확장 축이 여러 중앙 명단에 중복되어 있다는 것**이다. 새 콘텐츠가 자기 데이터와 동작만 추가해서 끝나지 않고 enum, 저장 DTO, UI 포매터, 룸 추론, AI 분기, 구성 루트까지 함께 수정해야 한다.

가장 먼저 고쳐야 할 것은 다음 다섯 축이다.

| 우선순위 | 축 | 감사 당시 위험 |
|---|---|---|
| P0 | 건물 능력의 이중 권위 | 새 능력 목록과 28비트 `FacilityFunction`이 동시에 동작을 결정한다. |
| P0 | 모듈 비인지 저장 | 새 상태형 능력이 정상 실행돼도 저장 DTO에 직접 추가하지 않으면 조용히 유실된다. |
| P0 | 고정 재고 스냅샷 | 런타임 dictionary에 추가한 재고 종류가 저장/복원에서 사라질 수 있다. |
| P0 | 수동 DI 명단과 무음 fallback | 주입 대상 누락이 컴파일되고 연구, 불만, 사망, 클릭 규칙이 조용히 꺼진다. |
| P0 | 정적 이벤트 버스 수명/재진입 | 도메인 리로드가 꺼진 환경에서 이전 Play 세션 구독자가 남고, 콜백 중 구독 변경 시 중복/누락 호출이 가능하다. |

## 구현 완료 상태 (2026-07-17)

아래 P0/P1/P2 본문은 최초 감사의 근거를 보존한 기록이다. 현재 소스는 Phase 72-74에서 해당 확장 경계를 실제로 교체했고, 이번 후속 감사에서 같은 계열의 잔여 결함도 추가로 제거했다.

| 재발한 설계 미스 | 실제 문제 | 적용한 경계 |
|---|---|---|
| `FacilityData` 선택 정책의 이중 권위 | 재고, 직원 서비스, 방 역할, 독립 방 정책이 능력 목록과 고정 필드에 동시에 존재했다. | 정책을 present-only ability로 이전하고 레거시 필드와 호환 fallback을 삭제했다. |
| 종족 선호가 모든 시설 데이터에 포함됨 | 일부 시설만 필요한 affinity가 공통 facility core를 다시 비대하게 만들었다. | `BuildingSpeciesAffinityAbility`로 분리하고 22개 실제 설정을 자산에 이관했다. |
| 판매·보충·저장이 구체 `Shop`을 앎 | 독립 capability가 한 구현 타입에 묶여 새 판매 시설 또는 재고 구현이 중앙 코드를 수정해야 했다. | `IRetailFacility`, `IRestockableFacility`, `IRetailStockStateOwner` 계약으로 실행과 저장을 분리했다. |
| 능력 목록 전체 교체가 공개됨 | fixture/editor 코드가 이미 설정한 능력을 나중 할당 한 번으로 조용히 지울 수 있었다. | 목록을 private serialized 소유권으로 닫고 읽기 view와 `ReplaceAbilities`만 제공한다. |
| 진화 지표의 소유권이 없음 | 운영 기록의 기본값 `SeatCount=0`이 실제 방 구조에서 계산한 좌석 수를 덮어썼다. | 구조 지표, 동적 기록 지표, 파생 지표를 분류했다. 구조 기록은 실제 구조가 없을 때만 fallback으로 사용하고 파생 지표는 evaluator만 계산한다. |
| 표시명과 정확한 타입이 의미 ID 역할을 함 | `Door`/`문`, 배경 sprite 이름, `GetType()==typeof(Door)`가 동작과 씬 구성을 바꿨다. | 문 판정은 component/typed metadata, 배경은 명시 참조, 던전 입구 여부는 가상 capability로 전환했다. |
| 카탈로그가 있어도 일부 소비자가 enum을 직접 열거함 | 런타임 등록 콘텐츠가 저장, 방, UI 중 한 경로에서 누락될 수 있었다. | 작업, 욕구, 역할, 재고, 건물 category 소비자를 해당 definition catalog로 통일했다. |

현재 `BuildingSO`의 다형 능력 목록은 private serialized aggregate이며 능력 subtype이 설정과 capability를 함께 소유한다. 선택 정책, 종족 affinity, 범죄 modifier, 품질, 생산, 조명, 재고, 좌석, 서비스, 방 역할은 공통 고정 필드나 이름 규칙을 다시 사용하지 않는다. 상태형 능력은 module ID/version/payload 계약으로 저장되며 테스트용 미등록 모듈도 중앙 DTO 수정 없이 왕복한다.

후속 전수 스캔 결과, 런타임에는 구체 `Shop` 타입 의존, `GetType()==typeof(...)` 확장 분기, `FacilityRole`/`BuildingCategory`의 직접 enum 열거, 문 이름 판정, 던전 배경 sprite 이름 판정, 공개 능력 목록 교체가 남아 있지 않다. `restockRequestThreshold`라는 직렬화 이름은 레거시 `FacilityData`가 아니라 13개 시설의 `BuildingInternalStockAbility` 설정 안에만 남아 있으며, 이 값은 새 구조의 의도된 세부 조정값이다. 남은 switch/enum은 불만 결과, 이동 종류, UI 표면 계열, 저장 wire mode처럼 의도적으로 닫힌 상태 또는 전송 계약이다. UI 테마의 로컬 `Background` sprite 확인과 hierarchy 이름 조회는 gameplay 의미 ID가 아니므로 이번 범위에서 별도 분해하지 않았다.

최종 검증은 Unity 실제 Roslyn으로 runtime/editor assembly를 순차 컴파일한 뒤 수행했다. 정적 회귀 묶음은 taxonomy, composition, persistence, crime risk, modular facility, facility evolution, room/environment, grid, work priority, meta를 모두 통과했다. 실제 PlayMode 포인터 검증은 P0의 상점·창고·운영·연구 상태 변화를 모두 확인했고 P1/P2는 `18/18`, 캡처된 Error/Warning은 `0/0`이었다. Unity 콘솔도 Error/Warning `0/0`이며 Main Camera 1920x1080 캡처에서 실제 던전, 방, 문, 가구, 캐릭터가 비어 있지 않게 렌더링됨을 확인했다.

## 판정 기준

다음 중 하나를 만족할 때만 설계 결함으로 확정했다.

1. 정상적인 새 콘텐츠 한 종류를 추가하는 데 소유 도메인 밖의 중앙 enum, switch, UI, 저장 코드를 함께 수정해야 한다.
2. 선택 기능인데도 모든 자산 또는 모든 런타임 인스턴스가 관련 데이터/상태를 가진다.
3. 표시 문자열이나 번역 문구가 내부 ID 또는 행동 프로토콜 역할을 한다.
4. 필수 의존성이 빠졌는데 초기화 실패 대신 정상값처럼 보이는 no-op 결과를 반환한다.
5. 메모리에서는 지원되는 값이 저장, 복원, 정산, UI 중 일부에서 조용히 누락될 수 있다.

## P0 발견 사항

### P0-1. 건물 능력이 아직 완전히 모듈화되지 않음

**근거**

- `BuildingSO`는 다형 능력 목록과 별개로 `FacilityData`, `DefenseFacilityData`, `FacilityEvolutionContributionData`, `FacilityOperationalData`를 항상 소유한다: `Assets/Scripts/Buildings/SO/BuildingSO.cs:181`, `:190`, `:209-212`.
- `FacilityOperationalData`는 추출 완료 필드를 `Legacy compatibility only`라고 표시하면서도 28개 역할의 `FacilityFunction`을 계속 권위 데이터로 가진다: `Assets/Scripts/Buildings/FacilityOperationalData.cs:5`, `:60-89`.
- 실제 동작도 능력 타입이 아니라 이 비트마스크를 다시 분기한다: `Assets/Scripts/Buildings/ModularFacilityRuntimeEffects.cs:66`, `:171-179`, `:214-254`.
- 방의 운영 성향도 같은 레거시 마스크를 읽는다: `Assets/Scripts/Rooms/RoomFacilityPolicy.cs:206-215`.
- 자산 측정 결과 105개 건물 계열 자산 모두가 `FacilityData`를, 104개가 방어 데이터를, 103개가 진화 데이터를 직렬화한다. 방어 데이터 104개 중 92개는 비활성이다.
- 새 능력 체계를 사용하는 74개 자산 중 73개가 여전히 `FacilityFunction` 비트마스크를 가진다. 반면 추출된 legacy 용량/비용 값은 74개 모두 0이다.

**문제**

새 능력 클래스 하나를 리스트에 추가해도 런타임 효과, 방 추론, 상점 동작을 위해 중앙 enum과 소비자들을 다시 수정해야 한다. Inspector 모양만 리스트가 되었고 행동 권위는 아직 이중화되어 있다.

**목표 구조**

- `BuildingSO`에는 배치, 외형, 안정 ID처럼 모든 건물에 공통인 값만 남긴다.
- 선택 기능은 존재하는 모듈만 직렬화한다.
- 런타임은 `FacilityFunction`이 아니라 `TryGetAbility<T>()`, capability query, 또는 등록된 handler를 통해 기능을 조회한다.
- 방 역할, 생산 출력, 판매/보관 카테고리는 해당 능력이 직접 제공하는 읽기 전용 capability로 통합한다.
- 함수 비트 소비자를 먼저 0개로 만든 뒤 legacy 필드와 `FacilityOperationalData`를 제거한다.

**완료 조건**

새 건물 능력을 추가할 때 `BuildingSO`, 중앙 enum, 룸 정책, 공용 효과 resolver를 수정하지 않고 새 능력 타입과 그 전용 handler/test만 추가한다.

### P0-2. 상태형 건물 모듈이 저장 시스템에 자동 참여하지 않음

**근거**

- 저장 DTO가 창고와 상점을 고정 필드로 직접 안다: `Assets/Scripts/Infrastructure/ModularFacilityWorldSaveService.cs:385`, `:397-401`.
- 캡처는 `building is IWarehouseFacility`, `building is Shop`을 직접 검사한다: 같은 파일 `:421-432`.
- 복원도 동일한 타입 검사를 반복하고 타입이 달라지면 보관된 상태를 보고 없이 무시한다: 같은 파일 `:235-247`.
- 저장 버전은 `CurrentVersion = 1`이고 미래 버전만 거부한다. 과거 버전 migrator는 없다: 같은 파일 `:26`, `:124`.
- 모든 `BuildableObject`가 사용 횟수뿐 아니라 생산량과 경보 충전까지 포함한 `FacilityOperationalState`를 가진다: `Assets/Scripts/Buildings/BuildableObject.cs:31`, `Assets/Scripts/Buildings/FacilityOperationalData.cs:94-129`.

**문제**

조명, 생산, 좌석처럼 새 모듈을 실행 가능하게 만드는 작업과 저장 가능하게 만드는 작업이 분리되어 있다. 테스트가 빠지면 플레이 중에는 정상인데 로드 후만 망가지는 가장 위험한 종류의 누락이 된다.

**목표 구조**

- 상태를 가진 모듈은 `IStatefulBuildingModule` 계약을 구현한다.
- 계약은 안정적인 `moduleId`, payload schema version, capture, restore, migrate를 제공한다.
- 건물 저장은 모듈 목록을 열거해 `{moduleId, version, payload}`로 기록한다.
- 알 수 없는 모듈, 누락된 모듈, migration 실패는 명시적으로 보고한다.
- 생산량과 경보 충전은 공용 `FacilityOperationalState`에서 각 능력 상태로 옮긴다.

**완료 조건**

테스트용 상태 모듈 하나를 추가했을 때 중앙 저장 DTO와 capture/restore switch를 수정하지 않고 왕복 저장 테스트가 통과한다.

### P0-3. 재고 카테고리는 런타임만 열려 있고 저장은 닫혀 있음

**근거**

- 런타임 `WarehouseInventory`는 `Dictionary<StockCategory, int>`를 사용한다: `Assets/Scripts/Buildings/SO/StockInfo.cs:19`.
- 스냅샷은 다시 `food`, `general`, `weapon`, `mana` 네 필드로 펼친다: 같은 파일 `:155-163`.
- 캡처와 복원이 네 종류를 직접 열거한다: 같은 파일 `:122-150`.
- 초기 분배와 일일 납품도 네 종류를 반복한다: 같은 파일 `:47-59`, `:293-296`.
- 운영일 정산 DTO와 문구도 동일한 네 필드를 다시 가진다: `Assets/Scripts/Operation/OperatingDaySettlement.cs:34-37`, `:576-579`.

**문제**

다섯 번째 재고 종류를 dictionary에 넣는 코드는 정상 작동해도 저장/로드 후 수량이 사라진다. 화면이나 정산에서만 빠지는 부분 지원도 생긴다.

**목표 구조**

- 스냅샷을 versioned `{categoryId, amount}` 목록으로 저장한다.
- `StockCategoryDefinition`이 안정 ID, 표시명, 정렬 순서, 기본 납품 규칙을 소유한다.
- UI와 정산은 고정 네 열 대신 스냅샷/정의 catalog를 열거한다.
- 기존 네 필드 save는 v1 migrator에서 목록으로 변환한다.

### P0-4. 구성 루트의 수동 주입 명단과 no-op fallback이 결합됨

**근거**

- `DungeonRuntimeLifetimeScope.InjectSceneComponents`가 씬 주입 대상 50종을 타입별 `foreach`로 직접 열거한다: `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs:299-550`.
- 새 씬 컴포넌트가 이 명단에서 빠졌는지 검증하는 구성 계약 테스트는 찾지 못했다.
- 주입이 없으면 소유자 사망 처리가 빈 메서드가 된다: `Assets/Scripts/Character/Core/CharacterStats.cs:515-518`.
- 직원 불만은 작업 효율 1, 작업 차단 false, 반란 false로 사라진다: 같은 파일 `:520-531`, `Assets/Scripts/Character/Ability/AbilityWork.cs:703-714`.
- 연구 서비스는 연구 작업 없음으로 바뀐다: `Assets/Scripts/Character/Ability/AbilityWork.cs:683-700`.
- 캐릭터 우선 클릭도 모든 질의가 false가 된다: `Assets/Scripts/Buildings/BuildableObject.cs:708-733`.

**문제**

의존성 추가 시 구성 루트를 빼먹어도 컴파일과 플레이 진입은 성공한다. 결과는 예외가 아니라 일부 규칙만 사라지는 형태여서 UI 클릭 중첩, 연구 정지, AI 규칙 누락처럼 추적하기 어려운 회귀가 된다.

**목표 구조**

- 기능별 installer에서 씬 컴포넌트와 서비스를 함께 등록한다.
- VContainer hierarchy registration 또는 검증된 injectable marker 수집으로 50종 수동 명단을 없앤다.
- 필수 의존성은 `Awake`/초기화 시 컴포넌트 경로와 함께 fail-fast한다.
- no-op은 선택적인 시각 효과와 LLM 기능에만 허용한다.
- 테스트 대체물은 production fallback이 아니라 테스트 composition root에서 명시적으로 넣는다.

### P0-5. 정적 이벤트 버스가 현재 PlayMode 설정과 안전하지 않음

**근거**

- 구독 저장소는 static dictionary다: `Assets/Scripts/Utils/EventObserver.cs:9`.
- 발행은 live list를 뒤에서부터 직접 순회한다: 같은 파일 `:52-59`.
- `SubsystemRegistration` 또는 플레이 진입 reset이 없다.
- 프로젝트는 Enter Play Mode options를 켜고 domain reload와 scene reload를 모두 끈 값 `3`을 사용한다: `ProjectSettings/EditorSettings.asset:27-28`.

**문제**

콜백이 다른 listener를 제거하면 index가 이동해 현재 listener가 두 번 호출되거나 다른 listener가 건너뛰어질 수 있다. 도메인 리로드가 없으므로 이전 Play 세션의 정적 구독도 남을 수 있다.

**목표 구조**

- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`에서 정적 상태를 초기화한다.
- 발행 시 subscriber snapshot을 만들어 순회한다.
- 파괴된 Unity object listener를 제거한다.
- listener 예외를 다음 listener 전파와 분리할지 정책을 명시하고 테스트한다.

## P1 발견 사항

### P1-1. 작업 종류와 욕구 종류가 부분 확장 가능한 척하는 닫힌 taxonomy임

**작업 우선순위**

- 8개 작업이 catalog 배열과 표시명에 직접 반복된다: `Assets/Scripts/Character/Work/WorkPriorityProfile.cs:51-74`.
- `WorkPriorityProfile`이 8개 필드와 get/set switch를 가진다: 같은 파일 `:262`, `:293-334`.
- UI가 별도 표시명 switch를 또 가진다: `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs:426-437`.
- 구체 작업 종류는 40개가 넘는 소스 파일에서 직접 참조된다.

**욕구/상태**

- 값 저장은 dictionary지만 기분 요인은 5개 욕구를 직접 등록한다: `Assets/Scripts/Character/Core/CharacterMoodModel.cs:130-142`.
- AI 작업 선택, 디렉터 prompt, 근무 회복이 각 욕구를 다시 열거한다: `Assets/Scripts/Character/AI/CharacterAiJobGiver.cs:191-194`, `Assets/Scripts/Character/AI/AiDirectorRuntime.cs:189-193`, `Assets/Scripts/Character/Work/WorkDutyController.cs:27-32`.
- 생산 코드 21개 파일이 현재 여섯 조건을 구체적으로 참조한다.

**권장**

작업과 욕구 각각에 안정 ID, 표시명, 정렬, 기본값/곡선, 관련 capability를 가진 정의 catalog를 둔다. 일반 UI와 prompt는 catalog를 열거하고, 특수 행동만 등록 handler가 처리한다.

### P1-2. 캐릭터 로그 표시 문자열이 내부 이벤트 프로토콜임

**근거**

- 게임플레이가 `AddLog(string)`을 호출하고 태그를 원문에서 다시 추론한다: `Assets/Scripts/Character/Core/CharacterLog.cs:53-58`, `:241`.
- 태그는 중복 병합에 쓰이고 원문은 LLM 표시 지연 여부까지 결정한다: 같은 파일 `:91-105`.
- narrative eligibility가 `StartsWith`, `IndexOf`로 문장을 해석한다: `Assets/Scripts/Character/AI/CharacterLogNarrativeService.cs:125-141`.
- fallback/검증도 regex와 단어 token으로 원문 의미를 재구성한다: 같은 파일 `:630-700`, `:764-907`.

**문제**

문구를 자연스럽게 고치거나 번역하면 기록 병합, LLM 요청, fallback 동작이 바뀔 수 있다. 표시 텍스트가 ID와 이벤트 데이터 역할을 동시에 한다.

**권장**

`CharacterActivityEvent`에 event kind, actor, action/work ID, target/place, outcome, reason code, 수치를 저장한다. 집계와 판단은 구조화 필드로 하고 deterministic/LLM renderer만 문장을 만든다. `AddLog(string)`은 명시적인 debug/free-text 용도로 제한한다.

### P1-3. Run variable과 Meta upgrade가 고정 effect bag/분리 권위 구조임

- `RunVariableDefinition`이 모든 선택 효과 필드를 동시에 가진다: `Assets/Scripts/Run/RunVariableModel.cs:78-93`.
- 각 효과 차원마다 별도 중앙 reader가 있다: `Assets/Scripts/Run/RunVariableEffects.cs:5-84`.
- catalog는 14개 code-defined record를 만들며 사용하지 않는 필드는 중립값으로 남긴다: `Assets/Scripts/Run/RunVariableCatalog.cs:34-115`.
- Meta catalog는 이름/비용을, system은 ID별 수치 효과를 따로 소유한다: `Assets/Scripts/Meta/MetaProgressionCatalog.cs:4-66`, `Assets/Scripts/Meta/MetaProgressionSystem.cs:71-99`, `:215`.

Run variable과 meta definition 모두 typed effect 목록 또는 안정 ID로 등록되는 effect handler를 사용해야 한다. 정의가 표시 데이터만 있고 실제 효과가 없는 상태를 검증에서 거부해야 한다.

### P1-4. AI action 자산은 다형적이지만 orchestration은 concrete type에 닫혀 있음

- action subtype을 branch로 바꾸는 switch가 두 군데 중복된다: `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs:679-692`, `Assets/Scripts/Character/AI/CharacterMoodImpulseUtility.cs:207-220`.
- JobGiver가 action subtype별 클래스를 가진다: `Assets/Scripts/Character/AI/CharacterAiJobGiver.cs:317-471`.
- Behavior Designer task도 같은 subtype match를 반복한다: `Assets/Scripts/Character/AI/CharacterAiBehaviorTasks.cs:567-638`.
- 성격과 고객 persona도 concrete action type을 검사한다: `Assets/Scripts/Character/AI/CharacterAiPersonality.cs:13-33`, `Assets/Scripts/Character/AI/CustomerPersonaRuntime.cs:152-172`.

action definition 또는 등록 descriptor가 branch, semantic tag, label, 실행 adapter를 제공해야 한다. 성격은 concrete class 대신 tag를 점수화한다.

### P1-5. 방 역할이 시설 역할을 복제하고 레거시 정책이 실제 오판을 만듦

- `RoomRole`이 11개 `FacilityRole`을 복제하고 양방향 22개 매핑을 수동 유지한다: `Assets/Scripts/Rooms/RoomRole.cs:4`, `:24-52`.
- 같은 11개 순서 배열이 한 파일에 두 번 있다: `Assets/Scripts/Rooms/RoomEnvironment.cs:96-108`, `:355-367`.
- 이름/짧은 이름/UI label/색상 switch가 분산되어 있다: 같은 파일 `:370-448`, `Assets/Scripts/UI/P1P2FeatureSurfacePanel.cs:920-936`, `Assets/Scripts/Rooms/RoomEnvironmentSettingsSO.cs:95-111`.
- 레거시 `FacilityFunction` 기반 판매 추론은 food/general/weapon만 처리하고 실제 `StockCategory.Mana` 신호를 누락한다: `Assets/Scripts/Rooms/RoomFacilityPolicy.cs:206-215`.

하나의 역할 정의가 ID, 시설 alias, 방 이름, 짧은 이름, 색상, 정렬을 소유해야 한다. 방 정책은 역할 enum 추론이 아니라 설치된 ability capability를 집계해야 한다.

### P1-6. 전체 기능 UI가 하나의 presenter와 문구 기반 라우팅에 집중됨

- `P0FeatureSurfacePanel.cs` 1,076줄과 `P1P2FeatureSurfacePanel.cs` 956줄이 하나의 partial runtime class다: `Assets/Scripts/UI/P0FeatureSurfacePanel.cs:43`, `Assets/Scripts/UI/P1P2FeatureSurfacePanel.cs:6`.
- 이 클래스는 14개 서비스를 주입받고 거의 모든 도메인의 조회, 명령, 포매팅, 동적 UI 생성을 소유한다: `Assets/Scripts/UI/P0FeatureSurfacePanel.cs:74`.
- 탭 ID는 한국어 버튼 label dictionary에서 역산한다: `Assets/Scripts/UI/UITabManager.cs:46`, `:335-377`.
- 콘텐츠는 magic integer switch로 선택한다: `Assets/Scripts/UI/P0FeatureSurfacePanel.cs:235-269`.

탭마다 presenter/view-model을 등록하고 `UITabManager`는 안정적인 serialized `TabId`만 선택해야 한다. label은 표시 전용이어야 한다.

## P2 발견 사항

| 항목 | 근거 | 권장 방향 |
|---|---|---|
| 방어 effect SO가 가짜 다형성 | `DefenseEffectSO.Apply`가 static resolver에 위임하고 resolver/Codex가 6개 enum을 switch한다. `Assets/Scripts/Defense/DefenseEffectSO.cs:40-46`, `Assets/Scripts/Defense/DefenseFacilitySystem.cs:275-365`, `Assets/Scripts/Codex/CodexTextFormatter.cs:129-136` | 실행과 표시 metadata를 concrete effect strategy가 함께 소유한다. |
| 청사진 unlock이 세 배열로 고정 | DTO 세 배열, 적용 세 loop, Codex는 두 loop만 반복한다. `Assets/Scripts/FacilityShop/FacilityBlueprintSO.cs:12-14`, `Assets/Scripts/Research/BlueprintResearchSystem.cs:273-301`, `Assets/Scripts/Codex/CodexRecipeRecorder.cs:26-34` | `IBlueprintUnlock` entry 목록으로 apply/summary/Codex를 통합한다. |
| 시설 anchor가 네 종류 고정 | kind enum, override bool/offset 쌍, 데이터 switch와 기본 위치 switch가 중복된다. `Assets/Scripts/Buildings/SO/BuildingSO.cs:50-83`, `Assets/Scripts/Buildings/BuildableObject.cs:158-188` | 목적 ID 기반 anchor slot 목록으로 바꾸고 같은 목적의 복수 slot을 허용한다. |
| 범죄 정책이 건물마다 복제 | `FacilityData`의 위험 계수를 utility가 직접 읽고 104개 중 103개 자산이 같은 기본값을 반복한다. `Assets/Scripts/Buildings/SO/BuildingSO.cs:110-117`, `Assets/Scripts/Buildings/FacilityCrimeRiskUtility.cs:48-161` | 공용 `FacilityCrimeSettingsSO`와 선택적인 건물별 modifier로 분리한다. |
| 종족 incident가 multiplier switch | 세 incident가 한 utility에서 `1.2/1.1/1.05`로 바뀐다. `Assets/Scripts/Character/SO/CharacterSpeciesSO.cs:4-25`, `Assets/Scripts/Buildings/FacilityCrimeRiskUtility.cs:169-176` | multiplier가 전부면 종족 데이터 값으로, 행동이 커지면 strategy로 옮긴다. |
| `StatChange`가 이름과 달리 배고픔만 변경 | `Assets/Scripts/Buildings/SO/StatChange.cs:6-11` | target condition과 amount를 직렬화하거나 음식 전용 이름으로 제한한다. |
| 캐릭터 기본 stat block이 9개 필드 고정 | enum과 필드, `HasAnyValue`, `Get`, `Add`가 같은 종류를 반복한다. `Assets/Scripts/Character/SO/CharacterModelData.cs:6-75` | stat 종류가 콘텐츠 확장 축이면 keyed entries로 전환한다. 세트가 고정 계약이면 현재 구조를 유지하고 완전성 테스트만 둔다. |

## 확인했지만 결함으로 분류하지 않은 것

- `Consideration` 하위 클래스는 추상 점수 계약으로 소비되며 concrete subtype 중앙 dispatch가 없다. AI action을 고칠 때 참고할 좋은 패턴이다.
- `IBuildingCondition` 목록은 조건별 구현이 동작을 소유한다. 청사진 unlock과 run effect가 따라갈 수 있는 구조다.
- `OnBuyItemSO[]` 실행 자체는 다형적이다. 문제는 그중 `StatChange` payload가 hunger에 고정된 점이다.
- `GridBuildingObjectFactory`는 `BuildingSO.type`을 `AddComponent(Type)`으로 생성하며 concrete building switch가 없다. null/상속 사전 검증만 보강하면 된다.
- `FacilityEvolutionRecipeSO`, `FacilitySynthesisRecipeSO`는 응집된 도메인 레코드다. 필드 수가 많다는 이유로 분해할 대상이 아니다.
- `StaffDiscontentSystem` switch는 한 도메인 안의 유한 상태 전이를 구현한다. 콘텐츠 플러그인인 척하지 않으므로 대상이 아니다.
- `CharacterMoodFactorKind`와 `AIActionPlanKind`는 작은 표시/실행 상태다.
- 공용 `IUiPointerBlocker`는 배치, 철거, 월드 클릭 경로에서 사용되고 누락 시 fail-fast한다. 입력 경계는 이번 감사의 결함이 아니다.
- `SocialReputationRuntime` static instance는 `OnDisable`에서 구독과 instance를 정리한다.

## 권장 마이그레이션 순서

1. **안전망부터 추가**: 이벤트 버스 reset/reentrancy 테스트, DI 구성 완전성 PlayMode 테스트, save round-trip contract를 먼저 만든다.
2. **P0 런타임 실패 제거**: 필수 DI no-op을 fail-fast로 바꾸고 이벤트 버스를 snapshot 발행 및 subsystem reset으로 고친다.
3. **저장 모듈 registry 도입**: 기존 warehouse/shop/v1 save를 adapter와 migrator로 감싼 뒤 새 상태 모듈 계약을 만든다.
4. **건물 행동 권위 단일화**: `FacilityFunction` 소비자를 capability query로 옮기고 운영 state를 능력별로 분리한다.
5. **재고/작업/욕구/방 역할 catalog화**: 안정 ID와 metadata를 한곳에 두고 UI/정산/prompt를 열거형 표시로 바꾼다.
6. **구조화 이벤트와 AI descriptor 도입**: 로그 문자열 파싱을 제거하고 AI subtype 중앙 매핑을 descriptor로 이동한다.
7. **UI presenter 분해**: 안정 `TabId` 이후에 탭별 presenter를 분리한다. 도메인 모델이 정리되기 전에 UI만 쪼개면 중복 formatter가 더 늘어난다.
8. **P2 authoring 정리**: 방어 effect, blueprint unlock, anchor, 범죄 설정을 작은 단위로 이전한다.

빅뱅 교체는 피한다. 각 단계에서 기존 직렬화 자산과 저장 파일을 읽는 compatibility adapter를 유지하고, 새 writer만 새 구조를 쓰도록 전환한 뒤 구형 reader를 제거한다.

## 필수 회귀 계약

- 새 테스트 능력을 추가해도 `FacilityFunction`, `BuildingSO`, 룸 정책을 수정하지 않고 배치/실행/표시가 된다.
- 테스트 상태 모듈의 값이 저장/로드 후 동일하며 알 수 없는 module ID가 로그 없이 버려지지 않는다.
- 다섯 번째 테스트 재고 category가 seed, 입출고, 정산, 저장/복원, UI를 모두 통과한다.
- 모든 활성 씬 `[Inject]` 대상의 필수 의존성이 해결되며 한 대상을 일부러 누락하면 PlayMode 시작에서 실패한다.
- 이벤트 listener가 자기 자신 또는 다른 listener를 제거해도 한 발행에서 각 listener가 최대 한 번 호출된다.
- domain reload 없이 PlayMode를 두 번 실행해도 이전 listener가 호출되지 않는다.
- 로그 문구/번역을 바꿔도 event kind, 집계 키, LLM eligibility가 변하지 않는다.
- 새 작업/욕구/방 역할 정의를 추가하면 UI와 formatter가 중앙 switch 수정 없이 자동 열거한다.
