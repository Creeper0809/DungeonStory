# main 추가 구조 조사 R31

작성일: 2026-10-29

## 결론

확장 조사에서 확인한 수정 대상은 총 6건이다. 앞서 기록한 2건에 UI 후보 선택 2건과 캐시 입력 누락 2건을 추가했다. 실제 호출부와 상태 소유자의 규칙, 계산 입력과 갱신 조건을 대조한 결과다. 이번 조사에서 새 P0 또는 실제 저장 손실은 확인하지 않았다.

| 우선순위 | 번호 | 확인한 문제 | 영향과 확인 수준 |
|---|---|---|---|
| P2 | R31-01 | 방문객 생성 취소가 방문 플래그만 되돌리고 정착 신분은 남긴다 | 실제 실패 분기의 상태 불일치를 소스로 확인. 이후 조회의 재변경과 저장 전파 경로도 확인. 씬 재현과 저장 실패 발생은 미검증 |
| P2 | R31-05 | 운반 가능성 캐시가 하중과 운반 능력을 갱신 조건에 포함하지 않는다 | 같은 Actor와 위치에서 능력 또는 적재량만 달라지면 이전 가능 여부를 재사용할 수 있음. 실제 AI 시작 판단에 연결 |
| P2 | R31-06 | 방 환경 캐시가 환경 필드의 Version을 보지 않는다 | 실제 온도와 공기, 조명 변경이 방 조회에 반영되지 않을 수 있음. 방 검사 화면과 시설의 방 조건에 연결 |
| P2 | R31-03 | 연구 UI가 해당 프로젝트에서 사용을 마친 첫 특성 보유자를 다시 선택한다 | 다른 미사용 연구자가 있어도 같은 UI가 첫 사람의 거절만 반복 |
| P2 | R31-04 | 수리 UI가 이미 수리 예약된 최저 내구도 장비를 다시 선택한다 | 침공 종료 대기가 유지되면 다른 손상 장비의 수동 예약에 접근하지 못함 |
| P2 | R31-02 | 생산 UI와 실행 소유자가 배치 용량 점유를 다르게 계산한다 | 실제 맥주 레시피와 발효조 정의에서 마감 대기 주문을 표시할 때 안내 불일치. 버튼 차단이나 실행 거절은 아님 |

권장 수정 순서는 위 표와 같다. 상태 취소와 행동 선택의 갱신을 먼저 고치고, 잘못된 후보 선택과 안내를 잇는다. 같은 원인의 반환 노출, 직접 쓰기, 조회 부작용을 별도 항목으로 늘리지 않았다. 복원 후보의 가변 참조 등 보류 12개도 근거와 미확인 조건을 기록했다. 그 후보를 현재 플레이 결함 또는 필수 수정으로 확정하지 않는다.

## 기준과 조사 범위

- 기준 main 및 HEAD: `1d28d006107a71c62c405086c05aca3f3f6ebefc`
- 기준 tree: `4ab00ddd5bd90d4fa2a909f845a19b6df8161fd5`
- 작업 브랜치: `codex/audit-structure-r31`
- 종료 시 원격 main을 다시 조회했다. HEAD와 원격 main의 분기 차이는 `0 / 0`이며 위 기준과 같다.
- R30 완료 표시를 증거로 대체하지 않았다. 방문객 생성과 취소, 인구 조회와 저장, 생산 지원 용량, 상점 재고, 경제 복원 후보, UI 명령 결과, 캐시 입력과 구독 수명을 현재 원본에서 추적했다. 아래 확장 조사 범위와 직접 검토 범위를 구분한다.
- 지식베이스의 `CharacterPopulationService`, `ProductionBuildingPanelPresenter`, `ShopInventoryRuntime`, `CharacterApparelPolicy`, `UIManager` 조회는 fresh였다. 경제 명령 조사에서 결과를 얻지 못한 쿼리는 원본 확인으로 전환했다. 인덱스를 재생성하지 않았다.
- 우리 게임 코드와 관련 에셋만 조사했다. SDK 구현, 원본 `Documents/DungeonStory`의 사용자 변경과 별도 작업은 제외했다.
- 읽기 전용 의미 조사다. 게임 소스, 에셋, 테스트, 설정, 생성 인덱스를 수정하지 않았다. Unity 컴파일, Play, 저장과 복원 실행, 재시도 실행은 하지 않았다.
- 이번 추가 조사 대상의 확인 결과이며 모든 함수와 가능한 수명 전환의 무결함을 보장하는 전수 실행 결과가 아니다.

외부 근거 폴더: `C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31`

## 확장 조사의 범위와 확인 수준

이번에는 특정 두 클래스만 보고 끝내지 않고 현재 게임 Runtime 입력 전체를 수집했다. 정적 목록 수집, 원본의 의미 검토, 실제 실행은 각각 다른 확인 수준이다.

| 조사 축 | 실제 수행 범위 | 의미와 한계 |
|---|---|---|
| 전체 Runtime 원본 | Assets/Scripts의 Editor 디렉터리 밖 C# 2,986개 목록과 파일별 SHA256. 같은 전체에 Roslyn 구문 수집 적용, 구문 오류 0 | 폴더 기준 목록이다. 내부 UNITY_EDITOR와 진단 경로는 실제 플레이 판정에서 다시 제외. 모든 함수의 의미 검토나 Unity 컴파일을 뜻하지 않음 |
| 기존 구조 계측 | 현재 저장소 ArchitectureMetricsAnalyzer를 외부 출력으로 실행. Runtime 타입 9,794개, 크기 검토 타입 122개, 생성자 검토 77개 | 도구 exit 0. 크기 상위 타입 15개와 생성자 12개는 검토 표식이며 필수 분할 목록이 아님. 기존 기준과 에셋 보고서를 바꾸지 않음 |
| 어셈블리 참조 | 게임 asmdef 47개, 그중 Scripts 45개와 Tests 2개. 내부 참조 183개 | 선언 참조 순환 0. 외부 SDK는 참조 이름만 확인 |
| 파일 의존 | 기존 AssemblyMigrationPlanner의 현행 파일시스템 입력에서 Editor를 제외한 기본 어셈블리 Runtime 2,201개, 연결 18,200개 | 순환 묶음 22개, 최대 988파일. named asmdef 소스 노드 785개는 이 도구 그래프의 범위 밖. 모든 Runtime의 완전한 의미 그래프라고 쓰지 않음 |
| 취소와 복원 | 전체 구문 수집의 Restore 또는 clear/throw 관련 메서드 2,176개를 탐색 후보로 수집. public clear/throw 79개 중 clear 뒤 직접 throw가 있는 14개를 원본에서 대조 | 나머지 65개는 직접 throw 순서 분류다. 내부 호출의 모든 예외와 복원 조합을 증명한 것이 아님 |
| UI 호출과 자격 | Views 175개 전체의 명령/버튼/자격 표식 목록, 47개 파일의 관련 경계를 직접 대조 | 간접 delegate와 모든 UI 상태를 실행하지 않음. 실제 후보에는 등록과 owner 판정을 별도 연결 |
| 공개 반환과 소유권 | Models/Services 2,749개에서 등록 후 같은 지역 객체를 반환하는 제한된 패턴 탐색. 일치 30개 전부 최소 분류 | 검증 5, 스칼라 7, 새 DTO 7, 내부 협력 8, 확인 대상 3. getter 전체를 잡는 완전한 분석기는 아님. 추가 공개 경계도 별도 추적 |
| 캐시와 수명 | Services/Views 넓은 입력 2,344개, 진단과 prototype 등을 제외한 선별 입력 2,310개. 이벤트형 대입 65곳, Subscribe 148파일, 명시 cache 필드 30파일, static 어휘 58곳 목록 | 모든 표식을 결함으로 세지 않았다. 실제 갱신 생산자와 소비자, 저장된 해제 핸들러, 등록 수명을 대조 |

큰 파일 의존 순환은 남아 있다. 다만 988파일은 988개 독립 결함이나 새로 만들 클래스 수를 뜻하지 않는다. 도구가 참조 묶음을 찾았다는 이유로 정상 조립 코드를 일괄 분할하지 않는다. 현행 그래프의 강한 결합 후보와 각 명령의 실제 계약 불일치를 구별한다.

분기 구문 표식이 35개 이상인 메서드 27개도 후보 목록에 포함했다. ContentReferenceCatalog의 정의 순회, 저장 검증기의 도메인 대조, WorkTaskExecutor의 작업 조립처럼 실제 역할을 확인한 사례를 분기 수만으로 God Class라고 판정하지 않았다. 이 27개 전체의 모든 실행 경로를 의미 검토했다는 뜻은 아니다. 신규 수정 대상은 조사 과정에서 실제 규칙 복제와 갱신 누락이 확인된 위 6건이다.

외부 원본 목록과 판정 근거:

- [전체 Runtime 목록](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded-inputs.json), [원본 SHA256](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/runtime-source-sha256.json)
- [현재 계측 결과](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/metrics.json), [Runtime 파일 그래프](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/runtime-file-graph.json), [asmdef 그래프](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/assembly-graph.json)
- [UI 조사와 제외 근거](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded-ui-calls.md), [반환 경계 분류](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded-result-boundaries.md), [캐시와 수명 조사](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded-lifecycle-caches.md)
- [취소와 복원 후보 판정](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/main-clear-throw-disposition.json), [메인의 계약 대조 기록](C:/Users/vulpo/Documents/Codex/2026-10-29/structure-audit-r31/expanded/main-contract-review.md)

## R31-01: 방문객 생성 취소가 상태 소유자의 정착 신분 전이를 우회함

### 확인한 호출과 상태 변화

1. [CharacterPopulationDomain.cs:37](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Characters/CharacterPopulationDomain.cs:37)은 준비된 방문객을 고른 뒤 [50](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Characters/CharacterPopulationDomain.cs:50)에서 `IsVisiting=true`로 바꾼다.
2. 이 프로퍼티는 단순 bool이 아니다. [CharacterSkillModels.cs:777](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs:777)의 구현은 true일 때 `settlementStanding=Visitor`, `isStaff=false`도 함께 적용한다. false일 때는 Visitor를 PreparedCandidate로 되돌린다.
3. [CharacterPopulationService.cs:130](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:130)과 [136](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:136)은 이 소유 상태의 원본을 생성 명령 결과로 반환한다.
4. 실제 호출자인 [CharacterSpawner.cs:327](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/CharacterSpawner.cs:327)은 입장 위치를 찾지 못하면 [329](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/CharacterSpawner.cs:329)에서 **필드 `isVisiting=false`만** 쓴다. 프리팹에 Actor가 없는 실패도 [341](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/CharacterSpawner.cs:341)에서 같다. 두 분기 모두 Actor를 바인딩하기 전에 반환한다.

| 단계 | isVisiting | settlementStanding | Actor 바인딩 |
|---|---|---|---|
| 준비 후보 | false | PreparedCandidate | 없음 |
| AcquireVisitor 성공 | true | Visitor | 아직 없음 |
| 입장 위치 실패 후 현재 취소 | false | Visitor | 없음 |
| 일관된 취소에 필요한 상태 | false | PreparedCandidate | 없음 |

입장 위치 실패는 [CharacterSpawner.cs:482](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/CharacterSpawner.cs:482)의 실제 거절 결과다. grid를 얻지 못하거나 입구와 대체 보행 위치를 찾지 못하면 false를 반환한다. 현재 프리팹이 깨졌다고 주장하지 않으며, 프리팹 실패는 동일한 취소 구현의 두 번째 경계로만 센다.

### 결합과 저장에 미치는 영향

- 생성 담당자가 인구 소유자의 두 필드 전이 규칙을 알아야 하는 구조다. 같은 값을 의미하는 공개 필드와 인터페이스 프로퍼티가 다른 전이를 수행한다.
- [CharacterPopulationService.cs:1092](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:1092)의 신분 조회는 `NormalizeStanding`을 호출한다. [1106](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:1106)과 [1123](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:1123)은 남은 Visitor 신분에서 `isVisiting=true`를 다시 쓴다. 명시 신분이 있으면 그대로 반환하는 [CharacterSettlementStanding.cs:36](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Characters/CharacterSettlementStanding.cs:36) 규칙 때문이다.
- 따라서 취소된 동일 ID에 신분 조회를 하면 Actor 없이 방문 중인 상태로 바뀌는 정적 호출 반례가 있다. [GetStanding(string):437](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:437)과 [GetSettlementPopulation:445](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:445)이 이 조회에 도달한다. 현재 게임에서 취소 직후 해당 ID를 조회하는 연속 흐름까지 확인한 것은 아니다.
- [CaptureProfiles:551](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:551)은 Actor가 있는 행만 동기화한 뒤 전체 인구를 복사한다. Actor가 없는 취소 행을 고치지 않는다. [CharacterSkillModels.cs:816](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs:816)의 Clone은 두 값을 그대로 복사하고 [CharacterWorldSaveService.cs:261](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs:261)이 저장 입력으로 받는다.
- 조회로 방문 플래그가 다시 켜진 뒤에는 [CharacterWorldSaveValidation.cs:16](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/CharacterWorldSaveValidation.cs:16)의 방문객과 Actor 대조에서 소유자 불일치가 된다. 이 후속 저장 거절은 실행 재현하지 않았으므로 발생했다고 보고하지 않는다.

주변 공개 경계도 같은 원인에 묶인다. [BindActor:164](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:164)는 전달받은 행을 ID로 소유 상태에 다시 대조하지 않고 [180](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:180)에서 Actor 맵에 보관한다. [TryGetProfile:523](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs:523)은 이미 복사본을 반환하므로 공개 행과 소유 행의 구별이 API마다 다르다. 다만 현재 정상 BindActor 호출부가 복사본을 넘겨 저장을 잃는 사례는 확인하지 않았다.

### 최소 수정 방향과 필요한 검증

- 인구 소유자에 생성 예약의 취소 명령을 두고 방문 플래그와 정착 신분을 함께 되돌린다. 완료 방문 횟수를 올리는 기존 ReleaseVisitor를 실패 취소에 그대로 재사용하면 안 된다.
- 생성 호출자는 식별자 또는 읽기 전용 결과를 받고 소유 상태를 직접 쓰지 않게 한다. BindActor가 소유 행을 식별자로 해결하도록 경계를 맞춘다. 별도 대형 계층이나 범용 예약 프레임워크는 필요 없다.
- 신분 정규화는 입력과 명령 경계에서 수행하고 조회가 취소 상태를 재변경하지 않게 한다. 기존 DTO 필드와 구형 저장 해석은 유지한다.
- 표적 검증: 입장 위치 실패와 Actor 누락 실패 후 두 필드, 미바인딩, 방문 횟수 무변경을 확인한다. 이어 신분 조회, Capture, 저장과 복원, 같은 ID 재시도 후 단일 Actor 바인딩을 확인한다. 성공 방문의 종료 횟수 증가와 기존 방문객 복원은 대표 회귀만 확인한다.

## R31-02: UI와 실행 소유자의 배치 용량 점유 기준이 다름

### 실제 콘텐츠와 상태 생산자

- [황혼 맥주 레시피:31](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Resources/SO/Economy/Recipes/Workshop/recipe_twilight_beer.asset:31)은 발효 지원을 요구하며 [49](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Resources/SO/Economy/Recipes/Workshop/recipe_twilight_beer.asset:49)의 마감 작업량이 2다.
- [WS03 수동 발효조:108](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS03_수동_발효조.asset:108)의 배치 용량은 1이다.
- [ProductionBillExecutionRuntime.cs:866](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillExecutionRuntime.cs:866)은 처리 시간이 끝나면 Finishing으로 바꾼다. 점유 노드 ID는 사이클의 최종 완료인 [691](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillExecutionRuntime.cs:691)까지 남는다. 따라서 마감 작업자를 기다리면서 노드 ID가 남은 상태가 실제 생산 경로에 있다.

### 서로 다른 판정

| 호출부 | 점유 주문 필터 | 마감 대기 1개가 있는 용량 1 발효조 |
|---|---|---|
| [ProductionBuildingPanelPresenter.cs:827](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:827) | 같은 OccupiedSupportNodeId인 모든 주문 | 점유 1, 배치 용량 부족 |
| [ProductionCycleUtilityService.cs:581](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:581) | 같은 노드이며 Processing인 다른 주문 | 점유 0, 여유 있음 |

연구와 지원 설비, 온도와 유틸리티가 정상인 동일 중간 상태에서 두 판정이 달라진다. UI가 실행 규칙을 별도로 재구현하면서 단계 필터를 빠뜨린 사례다.

### 영향과 한계

- [ProductionBuildingPanelPresenter.cs:439](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:439)의 실제 레시피 설명에 [836](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:836)의 용량 부족 문구가 표시된다. 해당 문자열은 1회 제작 버튼을 비활성화하지 않는다. 진행 불가나 실행 거절로 분류하지 않는다.
- [ProductionBillStateCodec.cs:731](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs:731)과 [736](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs:736)이 단계와 노드 ID를 저장한다. 복원된 같은 중간 상태에서도 두 필터는 계속 다르다. 저장 표현 변경은 필요 없다.
- 지원 설비를 여러 작업대가 공유한다는 별도 가정은 제외했다. [ProductionWorkshopRuntime.cs:231](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs:231)과 [267](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs:267)은 작업대 하나로 연결을 제한한다.

### 최소 수정 방향과 필요한 검증

- 기존 실행 소유자의 순수 용량 조회를 UI가 사용하거나, 현재 범위에서 두 호출부의 단계 필터를 공유한다. 파일 길이를 줄이기 위한 클래스 분리는 필요 없다.
- 표적 검증: Processing일 때 점유, Finishing일 때 실행 규칙과 같은 여유, 완료 후 점유 해제를 비교한다. 마감 대기 상태를 저장하고 복원한 뒤 UI 안내와 다음 주문의 지원 선택이 일치하는지 확인한다. 전체 레시피와 모든 시드 실행은 필요 없다.

## R31-03: 연구 UI가 이미 사용한 첫 연구자만 선택함

### 실제 경로와 반례

- [UITabManager.cs:276](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/UITabManager.cs:276)이 실제 연구 창을 만들고 [ResearchTreeWindow.cs:387](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/ResearchTreeWindow.cs:387)의 금단의 도약 버튼을 연결한다.
- [ResolveForbiddenLeapResearcher:826](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/ResearchTreeWindow.cs:826)은 살아 있으며 특성 302를 보유한 사람 중 ID 순 첫 사람만 고른다. [723](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/ResearchTreeWindow.cs:723)의 버튼 판정도 프로젝트별 사용 이력을 보지 않는다.
- 소유자는 [BlueprintResearchRuntime.cs:301](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs:301)에서 활성 연구를 정하고 [ForbiddenResearchLeapTraitRuntime.cs:35](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ForbiddenResearchLeapTraitRuntime.cs:35)에서 **그 캐릭터가 그 프로젝트에 이미 사용했는지** 검사한다.
- A와 B가 같은 특성을 보유할 수 있다. [StartPartyPreparationService.cs:754](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:754)는 멤버마다 독립 선택하고 [CharacterTraitSelectionRules.cs:40](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterTraitSelectionRules.cs:40)의 중복 제거 범위는 그 선택 호출 안이다. trait 302는 현행 선택 풀의 양수 가중치 대상이다.
- 새 활성 연구 P의 진행이 0이고 ID가 앞선 A와 뒤의 B가 모두 보유자라고 하자. 첫 사용으로 A의 이력이 게시된 뒤에도 P가 남는다. [현재 특성 에셋:68](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_302_forbidden-leap.asset:68)의 최대 진전은 25%다. 다음 클릭은 다시 A를 골라 거절되고, 아직 사용하지 않은 B를 고르는 UI 경로는 없다.

### 원인, 저장과 최소 수정

UI가 특성 보유 여부로 자체 후보를 고르고 소유자의 사용 가능 조건을 공유하지 않는 결합 문제다. 명령 실패는 메시지로 표시되므로 실패 은폐 항목으로 세지 않는다.

[ForbiddenResearchLeapPreparation.cs:48](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ForbiddenResearchLeapPreparation.cs:48)의 사용 이력은 캐릭터별 rule state다. 화면을 다시 열거나 거절 명령을 반복해도 미사용 B로 넘어가지 않는다. 기존 저장 표현과 연구의 게시/복원 계약을 유지하고, 활성 프로젝트의 적격 연구자를 부작용 없이 조회하는 owner 규칙을 UI에서 사용하게 하면 된다. 이력을 UI에 따로 보관하거나 명령을 시험 실행해 후보를 찾지 않는다.

표적 검증은 A 사용 후 B 선택, 모두 사용 후 명시적 불가 안내, 사용 이력을 저장하고 복원한 뒤 동일 선택을 포함한다. 현재 특정 seed에서 A/B를 실제 생성하거나 버튼을 실행한 증거는 없다.

## R31-04: 수리 UI가 이미 예약된 최저 내구도 장비를 반복 선택함

### 실제 경로와 반례

- [CharacterSummaryRuntimeLogFactory.cs:398](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/CharacterSummaryRuntimeLogFactory.cs:398)의 수리 버튼은 summary 명령을 거쳐 [CharacterSummaryCombatPresenter.cs:258](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/CharacterSummaryCombatPresenter.cs:258)에 도달한다.
- UI는 장착 방어구와 방패 중 내구도 0.999 미만을 고른 뒤 최저 내구도 하나를 선택한다. 기존 수리 주문은 후보 필터에 없다.
- [EquipmentMaintenanceRuntime.cs:765](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:765)은 같은 장비의 주문이 있으면 거절한다. [816](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:816)은 침공 중 탈착 금지일 때 PendingCombatEnd를 만들고 [832](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:832)은 이 상태에서 장비를 배송하거나 탈착하지 않는다.
- 침공이 활성이고 자동 수리가 꺼져 있으며 침공 중 탈착 금지인 정책을 사용한다. 이 정책들은 [OperationsFeatureSurfacePresenter.cs:407](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/Core/OperationsFeatureSurfacePresenter.cs:407)과 [420](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/Core/OperationsFeatureSurfacePresenter.cs:420)의 실제 버튼으로 설정할 수 있다. 장착 장비 A는 25%, B는 50%이며 수리 시설과 유효 재질을 갖췄다고 하자.
- 첫 클릭은 A를 대기 예약한다. A가 계속 장착되어 있으므로 다음 클릭도 A를 고르고 중복으로 거절된다. B는 같은 장비 중복 조건에 걸리지 않는데 이 버튼으로 B의 예약에 도달할 수 없다. 침공 종료 전의 예약 접근 문제이며, 모든 수리가 영구 정지한다고 주장하지 않는다.

### 저장과 최소 수정

주문은 [EquipmentMaintenanceRuntime.cs:634](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:634)의 저장 대상이다. [864](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:864)에서 침공이 끝나야 배송 단계로 바뀐다. 따라서 다음 프레임에 바로 장착 목록에서 사라진다고 가정할 수 없다.

owner의 기존 주문 여부를 포함한 수리 후보 조회를 사용하거나 장비 선택 UI에서 예약 상태를 구분한다. 중복 주문을 허용하거나 원장의 거절 검사를 낮추면 안 된다. 새로운 수리 계층을 만들 필요도 없다.

표적 검증은 침공 대기 상태에서 A 예약 후 B 예약, 같은 장비의 중복 차단, 대기 주문 저장과 복원 후 다음 후보 선택, 침공 종료 후 기존 배송 전이를 포함한다. 실제 버튼 실행과 복원은 이번에 하지 않았다.

## R31-05: 운반 가능성 캐시가 하중과 능력의 변경을 놓침

### 실제 계산 입력과 캐시 입력

[WorldItemHaulPlanningService.cs:148](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs:148)의 HasAvailablePlan은 다음 여섯 조건이 같으면 이전 bool을 반환한다.

| 현재 캐시 비교 | 실제 추가 계산 입력 |
|---|---|
| Actor 참조, Actor 위치, HaulJobVersion, WarehouseVersion, TraversalVersion, destination claim Revision | 운반 인벤토리의 현재 무게, 최대 허용 무게, 물리 질량 조회 결과 |

- 실제 core는 [944](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs:944)에서 운반 인벤토리를 얻고 [955](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs:955)에서 허용 수량을 계산한다.
- [1429](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs:1429)은 최대 허용 무게에서 현재 무게를 뺀다. 최대 무게는 [CharacterCarryInventory.cs:499](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs:499)의 실제 기능 수행 능력과 하네스 상태에 의존한다.
- 인벤토리 owner는 [CharacterCarryInventory.cs:423](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs:423)의 Changed를 제공하고 [967](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs:967) 등의 변경 명령에서 발행한다. 이 서비스의 캐시 비교와 생성자에는 해당 갱신 입력이 없다. Actor의 수행 능력 변화도 여섯 비교 조건과 별개다.
- 물리 질량의 별도 갱신 권위도 이미 존재한다. [PhysicalStockQuery.cs:149](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/PhysicalStockQuery.cs:149)은 ItemStackVersion과 massQuery.AuthorityRevision을 함께 비교하지만 이 bool 캐시는 후자를 비교하지 않는다. 질량 변경의 모든 생산자가 이번 반례로 확인된 것은 아니며 수정 시 함께 점검할 계산 입력이다.

### 반례와 실제 소비자

동일 Actor가 같은 위치에서 운반 가능한 수량 0으로 캐시된 뒤, 원본의 운반 능력이나 적재량이 바뀌어 1개 이상 허용되는 상태가 되어도 여섯 키가 같으면 core를 실행하지 않고 false를 돌려준다. 반대 방향의 낡은 true도 가능하다. 일반 물건 이동은 저장소 버전도 바꾸는 경우가 있으므로 모든 운반 변경이 이 문제를 일으킨다고 쓰지 않는다. 독립적인 능력과 하중 원본을 캐시 계약에 포함하지 않은 것이 문제다.

이는 진단용 조회가 아니다. [WorldItemStackRuntime.cs:1375](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/WorldItemStackRuntime.cs:1375)를 거쳐 [AbilityHaul.cs:986](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Items/AbilityHaul.cs:986)의 실제 작업 시작 판정에 들어간다. 지속 시간과 실제 플레이의 작업 정지는 실행 재현하지 않았다.

### 최소 수정과 검증

기존 운반 원본의 revision 또는 재계산한 현재 무게와 최대 허용량을 캐시 비교에 포함한다. 물리 질량 조회의 기존 revision도 포함할지 입력별로 결정한다. 인벤토리 Changed 구독 하나만 추가하고 능력 변경을 다시 누락하지 않게 한다. 새 전역 revision 체계나 Actor별 영구 구독 목록을 선제 도입할 필요는 없다.

현재 메서드는 비교 키를 먼저 바꾸고 core를 계산한다. 수정할 때 새 계산이 성공한 뒤 키와 bool을 함께 교체해, 계산 실패 후 이전 bool이 새 키의 값으로 남는 경계도 막는다. 이 실패 순서는 소스상 관찰이며 현재 씬에서 해당 예외가 발생한 결과는 아니다.

캐시는 저장 DTO의 권위가 아니므로 저장 표현을 바꾸지 않는다. 표적 검증은 다른 여섯 키를 고정한 채 하중과 능력을 각각 바꾸는 경우, false에서 true와 true에서 false, 실제 AI 시작 판단, 새 프로세스 또는 새 Actor 복원 후 재계산을 포함한다.

## R31-06: 방 환경 캐시가 실제 환경 필드 갱신을 놓침

### 원본과 갱신 조건의 불일치

- [RoomEnvironmentAdapter.cs:227](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:227)의 CachedSnapshot에는 GridVersion, FacilityStateVersion, FilthVersion만 있다. [298](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:298)의 재평가 조건도 이 셋뿐이며 시간 제한이 없다.
- 실제 evaluator는 [540](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:540)에서 environmentalField.TryGetAverage를 호출해 온도, 공기, 조명을 snapshot에 넣는다.
- [EnvironmentalFieldRuntime.cs:151](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:151)의 정상 시뮬레이션이 필드를 진행한다. [EnvironmentalFieldSimulationRules.cs:331](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs:331)은 새 필드 배열을 게시하고 [344](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs:344)에서 Version을 증가시킨다. [EnvironmentalFieldRuntime.cs:90](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:90)이 이 revision을 공개한다.
- 시설 배치와 동적 상태, 오염은 그대로인 방에서 필드만 변하면 직접 환경 조회는 새 값인데 방 snapshot은 이전 값이다. [WorldFilthRuntime.cs:386](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Survival/WorldFilthRuntime.cs:386)과 [412](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Survival/WorldFilthRuntime.cs:412)의 오염 생성/청소 revision은 환경 필드 tick의 대체 키가 아니다.

### 실제 영향과 제한

- [RoomInspectionPresentation.cs:367](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Rooms/Presentation/RoomInspectionPresentation.cs:367)은 이 snapshot의 온도와 섭씨, 환기, 조명을 표시한다.
- [FacilityInstanceEvolutionRuntime.cs:1091](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs:1091)은 현재 방 조건을 조립하고 [1405](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs:1405)은 snapshot의 온도를 포함한다. 실제 환경과 시설의 방 조건이 달라질 수 있는 경로다.
- 작업 시간과 시설 선호 점수는 현재 [RoomEnvironmentAdapter.cs:336](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:336)과 [369](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:369)에서 impressiveness와 cleanliness를 쓴다. 따라서 온도 변경만으로 작업 속도까지 잘못된다고 확대하지 않는다.

기존 IEnvironmentalFieldQuery.Version을 같은 계산 경계에서 비교하면 된다. snapshot을 두 번째 환경 원장으로 만들거나 필드를 저장 DTO에 중복 추가하지 않는다. 나머지 evaluator 입력도 현재 세 revision으로 갱신되는지 수정 배치에서 함께 확인한다.

여기도 키 대입이 evaluator 호출보다 앞선다. 새 snapshot 계산에 성공한 뒤 키와 값을 함께 게시하는 순서로 맞춘다. 실제 evaluator 예외의 정상 생산자는 이번에 확정하지 않았으므로 이를 별도 신규 결함 수로 늘리지 않는다.

표적 검증은 방 구조/시설/오염 revision을 고정한 환경 필드 변화, UI와 시설 조건 조회 일치, 환경 상태 저장과 복원 직후 첫 조회를 포함한다. 장기 성능 측정이나 전체 방 조합 실행은 필요 없다.

## 보류 후보와 필수 수정으로 올리지 않은 이유

| 번호 | 실제 확인한 형태 | 아직 필요한 근거 |
|---|---|---|
| H01 | [ProductionBillModels.cs:1717](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Production/Core/ProductionBillModels.cs:1717)의 복원 후보 Bills는 행까지 복제하지 않는다. 복원 게시 후 같은 행이 소유 상태가 된다 | [ProductionBillsSaveSection.cs:128](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/ProductionBillsSaveSection.cs:128)의 정상 단계는 후보를 소유자에게 넘긴다. 게시 후 외부가 변경하거나 재게시하는 게임 호출부는 확인하지 못했다. 의도된 소유권 이전을 무조건 조회 누출로 세지 않음 |
| H02 | [AnimalHusbandryAggregateState.cs:37](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Economy/Content/AnimalHusbandryAggregateState.cs:37)의 복원 후보가 public 가변 State를 보관한다. [AnimalHusbandryRuntime.cs:1176](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/Husbandry/AnimalHusbandryRuntime.cs:1176)의 복원으로 소유 상태가 된다 | [AnimalHusbandrySaveSection.cs:70](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Economy/Husbandry/AnimalHusbandrySaveSection.cs:70) 이후 외부 변경 경로는 확인하지 못했다. trusted 복원 이전 계약인지 먼저 결정해야 함 |
| H03 | [CharacterSummaryHealthPresenter.cs:87](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/CharacterSummaryHealthPresenter.cs:87) 등의 소비 정책 버튼이 owner 결과를 받지 않고 새로 표시한다 | 정상 버튼 인자와 실제 등록에서 거절을 생산하는 입력과 수명 전환이 미확정. 결과 유실 형태만으로 현재 사용자가 실패를 숨기게 된다고 단정하지 않음 |
| H04 | [UIBuildingInfo.cs:648](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Buildings/UI/UIBuildingInfo.cs:648)의 품질 반복 재개와 정지가 결과를 버린다 | 안전 반복 한도에서 재개가 거절되는 owner 조건은 있으나 그 완료 후 상태에 같은 버튼이 노출되는 실제 ConstructionSite 경로를 확인하지 못함 |
| H05 | [UIManager.cs:51](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/UIManager.cs:51)의 Start 구독과 [166](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/UIManager.cs:166)의 OnDisable 해제 사이에 재구독이 없다 | GameplayScene과 SampleScene의 소유 객체와 부모, 탭과 HUD 숨김, 씬 전환을 대조했지만 동일 인스턴스를 껐다 다시 켜는 게임 생산자는 미확인. 자식 Panel 숨김은 해당 수명 전환이 아님 |
| H06 | [CharacterSummaryStatusPresenter.cs:237](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/CharacterSummaryStatusPresenter.cs:237)은 직접 의복 선호를 지운 뒤 다음 선호를 지정한다 | [CharacterApparelRuntime.cs:1009](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs:1009)의 선택 항목과 명령 검증을 대조했다. 두 번째 명령만 실패하는 정상 동기 호출의 구체 입력을 확보하지 못했다. 사용할 수 없는 옷도 장래 선호로 보관하는 기존 정책과 구분해야 함 |
| H07 | [ResearchTreeWindow.cs:699](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/UI/ResearchTreeWindow.cs:699)의 선택 연구 상세에서 버튼을 누르지만 owner 명령은 활성 연구를 사용한다 | 선택한 queued/suspended 연구와 active 연구가 다른 UI 입력이 있다. 버튼을 전역 활성 연구 명령으로 의도한 계약이 미확정이므로 R31-03의 사용 이력 문제와 별도 필수 결함으로 세지 않음 |
| H08 | [ApparelBuildingPanelPresenter.cs:393](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs:393)의 건조 UI는 수분 20 이상만 고른다 | owner가 더 낮은 수분을 허용하는 형태는 확인. 경미한 습기 자동 건조와 수동 일괄 작업의 최소 효용 정책인지 구별해야 하며 현재 진행 피해는 미확정 |
| H09 | [SurvivalFoodStockRuntime.cs:1661](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs:1661)의 개요 캐시에 치료 성공 무효화가 명시되지 않는다 | 최대 0.5 game-second 만료가 있으므로 제한된 표시 지연이다. 현재 sampling 정책을 깨는 실제 소비자 반례 미확정 |
| H10 | [CharacterProgressionProfileProjector.cs:65](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterProgressionProfileProjector.cs:65)의 캐시 목록을 지운 뒤 missing/retired trait에서 실패하면 이전 view도 부분 변경될 수 있다 | 현행 생성/준비 경계가 누락 trait를 거절한다. 허용된 실제 입력이 실패 후 같은 projector를 계속 쓰는 생산자는 미확인 |
| H11 | [WorkTaskExecutor.cs:138](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:138)의 이동 의존 누락 분기가 기본 Completed로 AI 행동을 끝낸다 | 다른 명시 거절은 Failed를 전달한다. 정상 조립 이후 이 Actor에서 이동 컴포넌트 또는 grid만 누락되는 live 생산자는 미확정. 실패 분기 표현만으로 현재 작업 루프 정지로 세지 않음 |
| H12 | CharacterLife Register/TryGet, RegularCustomer TryRecruit, StaffDiscontent ProcessStaff, RunVariable 활성화가 가변 소유 행을 반환한다. 경험 episode 값 타입도 중첩 목록 참조를 공유할 수 있다 | 외부 반환 후 실제 변조는 확인하지 못했다. Codex 기록자의 AddInfo, Grief의 AdvanceToDay 등 실제 직접 쓰기는 도메인 소유 포트의 의도된 협력일 수 있다. 호출부와 내부 협력은 외부 무단 쓰기와 구분. 상세 심볼과 소비자는 반환 경계 보고서에 보존 |

## 재보고하지 않은 주변 경계

- Profiles와 TryGetProfile의 공개 조회는 현재 복사본이다. R30의 조회 수정 자체가 누락됐다고 쓰지 않는다. R31-01은 별도의 생성과 취소 명령 결과 경계다.
- 방문객 퇴장 rollback은 [CharacterVisitorExitOutcomeRuntime.cs:199](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterVisitorExitOutcomeRuntime.cs:199)에서 복원하고 전용 소유 프로필 포트로 다시 바인딩한다. 공개 TryGetProfile 복사본을 잘못 사용하는 사례로 세지 않는다.
- [ShopInventoryRuntime.cs:126](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Models/Buildings/Core/ShopInventoryRuntime.cs:126)의 Stocks와 Lots는 가변 행을 포함하지만, 실제 게임의 Inventory adapter와 CustomerInventory는 내부 협력 경계다. 확인한 고객 호출부는 행을 읽고 owner 명령에 전달한다. 외부에서 원장 행을 직접 고치는 새 생산자는 확인하지 못했다. 공개 형식만 보고 필수 수정으로 승격하지 않는다.
- [EquipmentEvolutionRuntime.cs:91](C:/Users/vulpo/.codex/worktrees/so-reference-authoring/DungeonStory/Assets/Scripts/Services/Combat/EquipmentEvolutionRuntime.cs:91)의 조회는 복사본이며 진화 초기화가 명령으로 옮겨졌다. 과거 조회 부작용을 재보고하지 않는다.
- 문 권한 붙여넣기와 개별 정책, 직원 명령, 제작과 재조율, 야생동물과 공연 예약의 검토한 버튼은 bool 또는 실패 설명을 전달한다. 문장 형태의 호출이라는 이유만으로 결과 유실로 세지 않는다.
- 복원 입력이 가변 DTO라는 사실, 작성 SO의 불변 사용 계약, 적 표시용 공통 호스트, 고정된 시나리오 의미 키만으로 새 구조 결함을 추가하지 않는다.

## 검증 기록

아래 사항을 외부 `verification.json`에 기록했다.

- 원격 main, HEAD, tree와 분기 차이
- 보고서의 소스 링크 존재와 줄 번호 유효성
- 보고서가 인용한 실제 파일의 SHA256
- `Assets`, `Packages`, `ProjectSettings`, `Tools`, `tools`, `Tests`, `docs_final`의 기준 대비 변경 없음
- 이번 변경 파일이 보고서와 root 계획 기록으로 한정되는지 확인
- 신규 문서의 중간 점과 whitespace 오류 여부

확장 문서의 소스 링크97개와 인용 파일54개, 외부 근거 링크10개가 유효했다. 전체 Runtime2,986개의 원본 SHA256 drift는0이다. 원격 main/HEAD/tree가 같고 분기0/0, 게임 입력 변경/추가0, 예상 밖 tracked 변경0이다. 기존 미추적 QA636개를 보존했다. 신규 문서의 중간 점과 끝 공백은0이며 root 문서의 `git diff --check`도 통과했다. 최종 결과는 `expanded/verification.json`, 초기 2건 조사 기록은 `initial-two-findings-verification.json`으로 구분한다.

Unity 컴파일과 실제 실행은 이번 조사에서 미실시다. 위의 수정 후 검증 목록은 앞으로 필요한 검증이며 이번 실행 PASS가 아니다.
