# 게임 코드 구조 감사 - 2026-10-27

기준: `codex/fix-game-so-r26`, commit `c2ff169b8f25e802fd6a7300b38e8adb23e58258`.

## S01~S11 후속 수정

수정 branch: `codex/fix-structure-r27`. 아래 감사 본문은 수정 전 증거로 보존한다. 현재 상태는 이 표와 실행 기록을 따른다.

| 항목 | 적용 내용 | 검증 경계 |
|---|---|---|
| S01 | 일일 상품 구매 키를 상점 저장 권위로 이동. UI와 자동 구매가 동일 제한 사용. 내부 작업용 건물을 판매 후보에서 제외 | 구매 전/후 복원, 새 소유자, 실패 롤백, ACK 유실 재시도 PASS. 실제 UI와 새 프로세스 저장 로드/반복 로드31검사 PASS |
| S02 | Brain 상태 외부 쓰기 제한. 종료 콜백 재진입 차단과 한 번의 투영 복원 | 의료/수술 복원 실패 롤백, 실제 이동 소유권/종료, external intent와 작업 연결 복원 PASS |
| S03 | 장착 준비 규칙 공용화. 사용 중 슬롯에 교체 대상 표시 | 실제 감정/복원/장착, 빈 슬롯과 교체 UI, 거절 시 상태 불변 PASS |
| S04 | Inspector와 런타임의 효과 대상 매핑 공용화 | pending SerializedProperty와 정의 대상 매핑 PASS |
| S05 | 저장하는 능력 정의가 게시 ID 보호를 직접 선언 | 새 미등록 이름의 능력 보호, 변조 거절/복구, 기존28개 ID 보존 PASS |
| S06 | 사건 이동 증빙을 scoped 사건 서비스로 이동. 기존 DTO 보존 | 실제 이동 영수증 capture/JSON/새 소유자/동일 재시도, 취소/복원/release, 전체 저장 복원 PASS |
| S07 | 시설 접근/취소/결제 흐름과 소비 실행을 분리 | 실제 Interact 코루틴, 서비스 대기/효과 이후 저장 재개, 취소와 단일 결과 PASS. 통제 시계 검사이며 자연 플레이 전체 인증은 아님 |
| S08 | 세력 배송과 영수증 복구 소유자 분리. 조립은 기존 등록 위치로 이동. 인장 대기 롤백과 물리 기록 ID 경계 수정 | 소비/보상/부분 결과/ACK 유실 JSON 복원과 재시도 PASS. 실제 인장/숯8 운반, 보상 전 전체 저장 복원, 보상1회/ACK 후 복원 PASS |
| S09 | 공용 참조 카탈로그가 필요한 하위 카탈로그만 받도록 변경 | 기존 analyzer Verify PASS. 루트 직접 참조4에서3. baseline 무변경 |
| S10 | 서비스 hub/category 소유권 조건 공용화 | 후보 필터와 양방향 검사 불일치 거절 PASS |
| S11 | 공용 UI 생성 함수의 소유 위치를 기존 view factory로 이동 | presenter와 하위 panel 상호 helper 호출 제거. 실제 장착 UI 생성 PASS |

현재 실행 증거: 외부 `structure-remediation-r27/r189r27-impact-so-inspector-r27-focused`. Unity 컴파일 오류0, 실행 exit0, 소스 drift0. 실제 장비 소유자와 exact 목적지를 빠뜨렸던 기존 fixture도 함께 수정했다. r187 컴파일 실패와 r188 fixture 실패 기록은 보존한다.

실제 이동 증거: `r190r27-impact-so-inspector-r27-movement`,90초, exit0/컴파일 오류0/drift0. r189에서 r190으로 바뀐 입력은 생성된 구조 지표 JSON 하나이며 게임 소스는 동일하다. r190 배송 시도는 시작 파티 준비 단계 실패로 배송 PASS가 아니다. r191은 R27 검증 진입점에서 정식 시작 명령으로 준비하도록 변경했으며 게임 소스/에셋은 동일하다.

기존 저장 호환: 캐릭터 이동 DTO와 사건/시설 영수증 형식은 유지한다. 상점 V1에 선택적 일일 이력 필드를 추가했다. 과거 저장에 없던 구매 이력은 추정하지 않으며 기존 돈/해금/구매 순번을 보존한다. 새 저장에서 필드 일부 누락, null, 중복 또는 다른 날짜 키는 거절한다.

배송 후속: r192에서 공급 요청이 롤백으로 사라지는 결함, r193에서 URI escape를 포함한 물리 작업 ID의 기록 거절을 실제 재현했다. r193 효과 복구 재검사430초 PASS, r194 물리 ID/기존 키/JSON/재시도/변조 검사200초 PASS, 실제 배송130초 PASS. 원래 operation/commit 저장 문자열과 유효했던 기록 키는 바꾸지 않았다. 전역 stable ID 검사도 그대로이며 새 관찰 경계에서만 SHA256 키를 사용한다. 실제 배송은 통제된 겨울 날짜/직원 우선순위로 수행했으며 자연 계절 전체나 장기 AI 우선순위 인증은 아니다.

상점 후속: r194 실제 UI의 내부 음수 ID 건물 후보 혼입을 수정했다. r195는 추가 테스트 namespace 누락으로 컴파일 실패했고 r196에서 내부 후보/직접 구매 거절, 상점 복원과 재시도130초 및 실제 첫 진행150초 PASS했다. r196 cold-shop는 다음 날 저장의 일일 이력이 정상 초기화돼 검증 입력 조건에서 실패했다. r197은 실제 구매 직후 저장을 별도로 생성하며 기존 연구 완료/내구 장비 소모 저장도 유지한다. 검사 기준과 저장 원본은 완화하지 않았다.

최종 실제 실행: r197 첫 진행240초, 구매 당일 저장의 새 프로세스 로드100초/31검사, 구형 r182 저장의 새 프로세스 로드110초/23검사 PASS. 실제 UI 구매와 중복 거절, AI 운반/연구/기본 시설 재구매, 돈/물품/연구/상점 이력 보존, 반복 로드와 인물 중복 없음, 입력 저장 SHA 보존을 확인했다. 첫 진행의 오류/경고는0이며 통제된 시작 능력치를 사용한다. 화면 품질과 모델 추론, 자연 플레이 모든 조합은 실행 범위가 아니다.

[실행 증거](../../Artifacts/QA/StructureR27/verification.json)에 수용한9회 native 실행의 실제 source label, SHA와 이후 바뀐 입력을 기록했다. 과거 결과를 최종 소스 결과로 바꾸지 않는다. r197 이후 게임 구현은 동일하며 바뀐 두 Editor 파일은 아래 CI 정정이다.

최종 r199 CI5묶음은 effects1260초/core250초/character190초/crop200초/world240초 모두 PASS했다. 컴파일 오류0, 콘솔 오류0, 입력21736개 SHA drift0이며 [최종 CI 증거](../../Artifacts/QA/ImpactCI/verification.json)에 원본 로그와 현재 소스 지문을 연결했다. 월드 실제 저장 복원은 건물138/인물3/원정1/침입1 및 연구/방어/침입 실패 후 재시도를 확인했다. 기존 구조 baseline과 CI 검사 규칙은 완화하지 않았다. GitHub/main 통합 결과는 이 변경의 PR과 Actions 기록을 따른다.

CI 정정: 장비 capability ID 변조는 기존 module owner 검사가 더 이른 단계에서 거절한다. 구형 fixture는 이 거절과 게시 레코드 변경 거절을 각각 단언하도록 수정했으며 guard를 완화하지 않았다. 씬 지문은 이전 승인 R26 커밋의 테스트용 수동 캐릭터 후보 제거와 실제 씬 실행을 대조해 갱신했다. 씬 동등성 검사와 실행 전후 SHA 검사는 유지한다. r197 effects 실패와 r198 수동 중단은 전체 PASS로 취급하지 않는다.

## 판단

현재 코드는 핵심 저장 안전장치를 갖추었지만, 전체적으로 응집도와 결합도가 잘 정돈된 상태라고 평가하기 어렵다. 일부 공용 실행기가 도메인별 규칙과 복원 세부사항을 함께 알고 있고, UI와 Editor가 런타임 정책을 다시 구현한다. 클래스를 이미 나누었어도 상태 쓰기 권위와 상호 호출이 남아 있는 곳도 있다.

문제의 기준은 파일 길이가 아니라 변경 이유, 상태 쓰기 소유자, 규칙 중복, 실제 변경 전파다. 아래 11개 항목은 같은 원인을 묶은 개선 대상이다. 모든 항목이 현재 플레이에서 재현된 버그라는 뜻은 아니다. S01은 저장 복원 경로와 정책 상태의 불일치, S09는 현재 실행한 정적 검사 실패다. 나머지는 원본에서 확인한 구조 문제와 그로 인한 변경 위험이다.

저장 후보 검증, 원자적 게시, 물리 영수증, ACK, 재시도와 중복 방지는 실제 계약이다. 이 코드를 줄여서 단순하게 보이게 하는 방식은 권하지 않는다.

## 조사 범위와 방법

| 구분 | 확인 범위 |
|---|---|
| 게임 C# 파일 목록 | 4,076개. Runtime 2,983, Scripts 내부 Editor 1,034, 루트 Editor 41, Tests 18 |
| SDK 제외 | Behavior Designer, Plugins, ThirdParty의 C# 572개. 게임의 SDK 사용 경계는 게임 원본 조사에 포함 |
| 전체 정적 구조 | Runtime 2,983개를 기존 ArchitectureMetricsAnalyzer로 분석 |
| 클래스와 메서드 탐색 | Scripts 전체의 클래스 선언 8,021개. 그중 Runtime 선언 6,885개. 중첩 클래스와 같은 이름의 서로 다른 클래스 포함 |
| 의미 기반 파일 의존 그래프 | Assembly-CSharp의 게임 파일 2,198개. Bee 입력과 metadata 427개 사용, 누락 metadata 경로 0 |
| 명시적 어셈블리 참조 | Assets/Scripts의 asmdef 45개 선언 참조를 검사. 내부 순환 0 |
| 직접 원본 검토 | UI 명령과 조회, AI 행동 종료, 작업 실행, 이동, 시설 사용, 사건 처리, SO 선택과 게시 보호, 저장 조립 및 주요 복잡도 후보 |
| 인덱스 | KB freshness 성공. 일부 구체 타입 검색 0건은 원본 검색으로 보완. 인덱스 재생성 없음 |
| 실행 범위 | 외부 폴더에서 정적 분석 도구만 컴파일하고 실행. Unity 게임 컴파일, Play, 실제 저장 복원은 이번 조사에서 실행하지 않음 |

전체 파일 정적 수집과 위험 경계의 직접 검토를 수행했다. 4,076개 파일의 모든 함수와 모든 실행 조합을 사람이 의미 검토했다는 주장은 하지 않는다. 정적 그래프는 reflection, Unity 직렬화, 동적 등록과 실행 시 상태를 완전히 표현하지 못한다. 원본 Documents checkout의 별도 AI 작업은 이 기준에 섞지 않았다.

외부 증거 폴더: `C:/Users/vulpo/Documents/Codex/2026-10-27/code-structure-audit`.

- `metrics.json`, `ownership.json`: 현재 원본의 기존 구조 지표.
- `ArchitectureMetricsAnalyzer.log`: 기존 baseline 대조 실패 원인.
- `graph.json`, `AssemblyMigrationPlanner.log`: 파일 의존 그래프와 입력 hash.
- `classes.json`: 기존 Inventory를 외부에서 확장한 메서드 탐색 자료. branches는 분기 구문 개수이며 cyclomatic complexity나 품질 점수가 아니다.
- `input-files.json`, `coverage.json`, `review-verification.json`: 조사 입력 hash, 범위, 원본 보존과 문서 링크 검사.

## 우선순위

P1은 현재 상태와 정책의 불일치를 먼저 고칠 대상, P2는 변경과 확장을 어렵게 하거나 실수 시 핵심 동작에 영향을 줄 경계, P3는 인접 작업 때 작게 정리할 대상이다. P0 크래시나 데이터 손상을 이번 조사에서 재현하지는 않았다.

| ID | 우선순위 | 대상 | 핵심 문제 |
|---|---|---|---|
| S01 | P1 | 상점 UI와 상점 저장 | 일일 구매 제한의 별도 상태가 저장 복원과 자동 구매 경로에 연결되지 않음 |
| S02 | P2 | AIBrain와 작업/의료 복원 | 행동 상태와 종료 순서를 여러 호출자가 직접 맞춤 |
| S03 | P2 | 장비 장착 UI | 장착 조건을 런타임과 중복 구현하고 교체 슬롯을 빈 슬롯으로 표시 |
| S04 | P2 | SO 선택과 참조 검사 | 효과 대상 종류 매핑을 Inspector와 공용 카탈로그가 각각 소유 |
| S05 | P2 | 게시 ID 보호 | 중첩 저장 ID의 보호 대상 선언과 수집 명단이 분리되어 누락 가능 |
| S06 | P2 | 공용 이동과 사건 반응 | Society 전용 증빙이 이동 컴포넌트와 캐릭터 저장에 들어감 |
| S07 | P2 | Facility.Interact | 공용 방문 흐름, 소비 종류별 처리, 복원 상태, 표현이 한 실행기에 집중 |
| S08 | P2 | V20ContentResolutionService | 사건 실행과 세력 배송 복구 및 내부 서비스 조립 책임이 혼재 |
| S09 | P2 | ContentReferenceCatalog | 루트 카탈로그 직접 참조가 경계를 늘렸고 기존 구조 검사 실패 |
| S10 | P3 | 서비스 프로세스 소유권 검사 | 동일 hub/category 규칙을 후보 필터와 양방향 검사에 반복 |
| S11 | P3 | 장비 제작과 개량 UI | 공용 UI helper 때문에 presenter와 하위 panel이 양방향 의존 |

## 상세 근거

아래 링크와 행 번호는 수정 전 c2ff169b8 소스에 고정한다. 현재 수정 결과는 위 후속 표와 실행 증거를 따른다.

### S01. 일일 구매 제한을 UI 서비스가 소유

- 근거: [ShopFeatureSurfacePresenter.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Views/UI/ShopFeatureSurfacePresenter.cs) 323행의 `purchasedKeys`, 471행 중복 거절, 495행 성공 시 추가. 조회도 222행에서 이 집합을 사용한다. [FacilityShopSaveSection.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Infrastructure/Core/Save/FacilityShopSaveSection.cs) 30-39행은 날짜, 구매 순번과 해금 목록을 저장한다.
- 같은 scope에서 구매 전 저장으로 되돌려도 UI 집합은 남는다. 새 scope에서 구매 후 저장을 읽으면 집합은 비어 있다. [DungeonSaveUi.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Views/UI/DungeonSaveUi.cs) 397행의 같은-scope 로드와 [DungeonPresentationRegistration.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Infrastructure/Registration/DungeonPresentationRegistration.cs) 96행 singleton 등록을 대조했다. UI 서비스에는 restore 처리나 Refreshed 구독이 없다.
- [AutoProcurementRuntime.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs) 1212행은 같은 상점 런타임을 UI 집합 없이 호출한다. 현재 일일 제한은 호출 경로에 따라 달라진다.
- 반증 확인: [FacilityShopPurchaseJournal.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/FacilityShop/FacilityShopPurchaseJournal.cs) 20행은 pending을 저장하고 115-117행에서 전달 완료 항목을 제거한다. [DailyFacilityShopRuntime.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/FacilityShop/DailyFacilityShopRuntime.cs) 312행, 400-409행은 다음 구매 sequence로 새 identity를 만든다. 이 journal은 한 구매 사실의 재전달을 보호하며, 같은 일일 상품의 새 구매를 제한하는 목록은 아니다. 구매 사실 저장이나 재시도 보호가 없다고 판단하지 않는다.
- 정책 근거: [FirstRunObjectivePlayModeVerifier.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Run/Editor/FirstRunObjectivePlayModeVerifier.cs) 404-413행에서 일일 상품 중복 거절을 명시 검사한다. 현재 UI 제한이 단순 표시용이라는 해석과 맞지 않는다.
- 최소 개선: 일일 상품 구매 여부와 제한을 상점의 저장 가능한 권위 또는 기존 완료 구매 원장 조회에 통합한다. UI와 자동 구매는 같은 명령과 조회를 사용한다. 기본 상품의 의도된 반복 구매는 유지한다.
- 필요한 후속 검증: 같은 날 구매 전 저장 복원, 구매 후 새 프로세스 로드, UI와 자동 구매의 동일 상품 접근, 기본 상품 재구매. 이번 조사는 실행 재현이 아닌 소스 대조다.

### S02. AI 행동 상태의 외부 쓰기와 종료 책임 분산

- 근거: [AIBrain.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/AI/AIBrain.cs) 23, 26, 47행의 `bestAction`, `isBestActionEnd`, `isExecuted`가 외부 쓰기를 허용한다. 직접 대입 22곳이 생산 코드 8개 파일에 있다. runtime 파일 내부 Editor fixture 6곳은 이 수에서 제외했다.
- [CharacterActor.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/Core/CharacterActor.cs) 733행은 `HasStarted || isExecuted`로 실행 상태를 판단한다. [AbilityWork.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/Ability/AbilityWork.cs) 1473-1487행은 Brain의 종료 명령 뒤에 선택 행동을 직접 null로 만든다. [CharacterMedicalAggregateState.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Combat/CharacterMedicalAggregateState.cs) 124행과 [SurgeryRestoreCoordinator.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Medical/SurgeryRestoreCoordinator.cs) 1143행도 여러 필드를 개별 복원한다.
- 실제 결합은 Brain 중단 → AIWork.OnStop → AbilityWork.StopAssignedWorkFromAi → Brain.EndExpectedAction으로 되돌아오는 흐름이다. [WorkTaskExecutor.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs)도 여러 실패 분기와 882, 917행에서 종료를 수행한다. 종료 알림이나 자원 하나를 추가할 때 모든 경로의 순서와 중복 처리를 점검해야 한다.
- 최소 개선: Brain의 선택 행동 쓰기와 실행 상태 조회를 단일 진입점으로 제한하고 복원도 한 명령으로 다룬다. 작업 소유자는 작업 자원을 정리하고, 행동 종료를 요청한 Brain이 최종 행동 전이를 책임지게 한다. 기존 WorkRunRuntime의 종료 기능을 활용하며 또 다른 일반 Manager를 만들지 않는다.
- 보존할 것: 수동 이동과 external intent에서 별도 상태 의미가 있다. `isExecuted`를 단순 삭제해서는 안 된다. run ID, expected action, epoch와 멱등 종료는 필요한 보호다. 실제 중복 실행을 재현했다고 보고하지 않는다.

### S03. 장비 장착 정책을 UI가 다시 구현

- 근거: [EquipmentProgressionCommandPanel.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Views/Buildings/UI/EquipmentProgressionCommandPanel.cs) 780-787행은 확인 여부, 내구도0.75, 복원/조율 상태, 4등급 룬 조건을 검사한다. [EquipmentModuleRuntime.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs) 871-880행에 같은 조건이 있다.
- UI는 149행의 조건으로 후보를 숨긴다. 규칙을 런타임에서 변경하고 UI를 놓치면 가능한 명령이 화면에서 사라진다. 현재도 290-307행은 모든 대상 슬롯을 빈 슬롯으로 표시하지만 런타임 904-945행은 기존 부품 반환과 교체를 지원한다.
- 최소 개선: 장착 가능 여부와 차단 이유를 런타임의 기존 사전 판정에서 조회한다. UI는 빈 슬롯/교체 대상과 기존 부품을 표시한다. 최종 변경 명령의 재검증은 유지한다.
- 경계: UI가 실제 장착 상태를 직접 쓰는 문제는 아니다. 중복 정책과 사용자에게 보여 주는 의미의 불일치다.

### S04. 효과 대상 참조 종류 매핑 중복

- 근거: [ContentReferenceDrawer.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Editor/ContentReferenceDrawer.cs) 150행과 [ContentReferenceCatalog.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Items/ContentReferenceCatalog.cs) 555행이 `WeightedTarget` 경로와 `V20ContentEffectKind`의 대상 종류를 각각 매핑한다.
- 새로운 대상형 효과를 추가할 때 런타임 handler만 등록해도 되는 상태가 아니다. 이 두 authoring 분기를 함께 고쳐야 선택 목록과 시작 시 참조 검사가 일치한다. 누락 시 한쪽은 선택을 제공하지 않거나 참조 검사 대상에서 빠질 수 있다.
- 최소 개선: 미적용 SerializedProperty 값을 읽는 Editor 작업은 남기되, 읽은 enum/경로를 참조 종류로 바꾸는 순수 규칙은 하나로 통합한다. 이미 일부 종류는 `ContentReferenceCatalog.ResolveReference`를 재사용하고 있으므로 그 방식을 이어갈 수 있다.
- 경계: 사건 실행 handler의 등록 구조 전체가 실패했다는 뜻은 아니다. 현재 발견은 authoring과 검증 사이의 중복이다.

### S05. 중첩 저장 ID 보호의 수동 명단

- 근거: [BuildingAbilityContracts.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Models/Buildings/Core/BuildingAbilityContracts.cs) 213행의 기반 `abilityId`에 ContentIdentity가 붙는다. 그러나 [ContentIdentityProtection.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Editor/ContentIdentityProtection.cs) 696행의 직접 필드 수집과 별개로, 805행은 중첩 ability 네 구체 타입을 명시한 명단을 사용한다.
- 새 저장 상태 능력을 구현하고 런타임에 등록해도 이 명단을 빠뜨리면 해당 중첩 ID가 게시 baseline에 수집되지 않는다. 이번 커밋에서 StructuralIntegrity와 Cover를 명단에 추가한 변경 자체가 실제 변경 전파 사례다.
- 최소 개선: 저장에 사용되는 중첩 identity의 보호 선언을 수집기가 읽을 수 있게 하고, 등록된 저장 상태 소유자와 보호 대상 집합의 불일치를 검출한다. 기존 네 타입의 보호와 기존 게시 값을 보존한다.
- 경계: 모든 ability ID를 무조건 영구 고정하자는 뜻은 아니다. 저장에 쓰이지 않는 ID와 의도된 수정 가능 필드를 구분해야 한다. 현재 네 타입의 보호가 깨졌다는 주장은 하지 않는다.

### S06. 공용 이동이 Society 전용 증빙 소유

- 근거: [AbilityMove.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/Ability/AbilityMove.cs) 1102-1149행의 Society 전용 시작/receipt/해제/복원 API와 [SocietyResponseMovementProvenanceRuntime.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Character/Ability/SocietyResponseMovementProvenanceRuntime.cs) 7행. 이미 별도 클래스로 나누었지만 소유 경계는 여전히 공용 이동 안이다.
- [V20ObservedIncidentResponseRuntime.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Run/V20ObservedIncidentResponseRuntime.cs) 640, 733, 1139행이 이를 호출하고 대조한다. [CharacterWorldSaveService.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs) 1622행과 1787행도 사건 반응의 전용 필드를 직접 저장하고 복원한다.
- 사건 증빙 형식이나 목적지 신원을 바꾸면 사건 도메인, 공용 이동, 캐릭터 저장이 함께 바뀐다. 줄 수 분리만으로 결합이 줄어들지 않은 구체적인 사례다.
- 최소 개선: 사건 전용 provenance와 receipt는 사건 반응 소유자에 두고 이동은 operation/lease와 완료/취소 결과를 제공한다. 기존 DTO를 읽는 호환 경계는 유지한다.
- 보존할 것: 실제 도착, operation 일치, 증빙 중복 방지와 저장 후 복원. 단순 위치 비교로 대체하면 안 된다.

### S07. 시설 이용 실행기에 서로 다른 소비 규칙이 집중

- 근거: [Facility.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Buildings/Facility.cs) 115행의 Interact는 대기/접근, 서비스 접수/결제, 물 공급, 식사 대기, 기호품 소비, 경험, 로그와 완료 receipt를 함께 진행한다. 414행 기호품/식사 분기, 453행 이후 물 ticket, 556행 이후 식사 재개와15초 대기가 구체적인 도메인 책임이다.
- [FacilityUseExecutionState.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Buildings/FacilityUseExecutionState.cs) 92-96행에도 물과 식사의 전용 재개 상태가 있다. 새 소비 방식의 pending/취소 규칙을 넣으려면 공용 방문 코루틴과 공용 저장 상태에 함께 손을 대게 된다.
- 탐색 지표는 Interact761행, 분기 구문70개다. 이것만으로 갓 클래스라고 판정한 것이 아니다. 별도 변경 이유를 가진 소비 정책과 공용 방문 생명주기가 얽힌 점이 근거다.
- 최소 개선: 공통 방문 단계는 유지하고 이미 존재하는 식사/물/기호품 권위가 소비와 pending 결과를 제공하게 한다. 같은 클래스 내 반복 단계 처리부터 줄이고, 별도 상태 소유가 필요한 경계만 옮긴다. 70개 분기마다 클래스를 만드는 작업은 불필요하다.
- 경계: 코루틴이 단계 순서를 조립하는 것은 정상이다. 완료 원장과 복원 필드를 없애자는 지적이 아니다.

### S08. 사건 처리 서비스에 조립과 별도 배송 수명 혼재

- 근거: [V20ContentResolutionService.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Run/V20ContentResolutionService.cs) 52행은30개 인자를 받아 126-148행에서 planner, typed effects, rollback, transaction, journal과 life-event 명령을 직접 조립한다. 284행은 사건 선택 실행, 792행은 저장 캡처 경계, 924행 이후는 세력 계약의 물리 배송/ACK/owner 해제까지 담당한다.
- 인자 개수 자체보다, 효과 실행의 의존 변경과 세력 배송 복구 변경과 조립 변경이 같은 서비스에 모이는 것이 문제다. 일부 helper를 나눈 뒤에도 이 클래스가 전체 구성과 별도 수명의 실행을 동시에 알고 있다. internal 테스트용 생성자에도 조립 코드가 반복된다.
- 최소 개선: 기존 registration 경계에서 내부 조립을 수행하게 하고, 세력 배송은 이미 존재하는 배송 계약과 소유자 경계에 맞춰 책임을 좁힌다. 단순히 인자30개를 하나의 bag에 넣거나 helper별 파일을 더 늘리는 것은 해결이 아니다.
- 보존할 것: 사건 효과 rollback, 영수증 join, 물리 commit과 reward/ACK 간 저장 차단. 실제 배송 오류를 이번 조사에서 재현하지 않았다.

### S09. 루트 카탈로그 의존 경계와 현재 검사 실패

- 현재 기존 ArchitectureMetricsAnalyzer를 `--verify`로 실행한 결과 exit1: `RootCatalogReference grew from 3 to 4.` 큰 클래스 개수 때문에 실패한 것이 아니다.
- 추가 참조는 [ContentReferenceCatalog.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Items/ContentReferenceCatalog.cs) 32행의 `FromRoot(GameContentCatalogSO root)`다. [GameContentCatalog.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Items/GameContentCatalog.cs) 66행의 부팅 검증과 Editor 선택기가 이 경로를 사용한다.
- 루트 접근을 제한하던 경계에 참조 검사용 클래스가 추가로 결합됐다. IEnumerable 정의를 받는 생성자는 이미 있으므로, 기존 root 조립 소유자가 정의 집합을 넘기도록 좁힐 수 있다.
- 최소 개선: 루트에서 정의를 추출하는 책임을 기존 허용 경계에 두고 참조 검사기는 정의 집합을 받는다. baseline 숫자만 올려 통과시키는 방법을 기본 해법으로 삼지 않는다. 의도된 경계 변경이라면 실제 계약과 검토 근거부터 바꿔야 한다.
- 실행 결과는 로컬 정적 분석 실패다. 이번 조사에서 GitHub CI 실패를 확인한 것은 아니다. Assets/Architecture의 과거 보고서와 baseline은 덮어쓰지 않았다.

### S10. 서비스 소유권 규칙 반복

- 근거: [ContentReferenceCatalog.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Services/Items/ContentReferenceCatalog.cs) 155행 후보 필터, 796행 hub→process 검사, 801행 process→hub 검사가 `OwnerHubTag/ServiceHubTag`와 `ServiceCategory` 일치를 각각 작성한다.
- 소유권 조건을 바꾸면 선택 목록과 두 방향 검사를 모두 수정해야 한다. 단계가 세 번 존재하는 것보다 같은 술어가 세 곳에 있다는 것이 문제다.
- 최소 개선: hub/process의 일치 조건 한 함수를 재사용한다. 후보 제한과 양방향 검증 자체는 유지한다. 별도 registry나 클래스를 만들 필요는 없다.

### S11. UI helper로 생긴 불필요한 양방향 의존

- 근거: [EquipmentCraftingPanelPresenter.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Views/Buildings/UI/EquipmentCraftingPanelPresenter.cs) 22행은 개량 panel을 소유하고 58행에서 호출한다. 반대로 [EquipmentProgressionCommandPanel.cs](https://github.com/Creeper0809/DungeonStory/blob/c2ff169b8f25e802fd6a7300b38e8adb23e58258/Assets/Scripts/Views/Buildings/UI/EquipmentProgressionCommandPanel.cs) 192행과 295-310행 등은 제작 presenter의 CreateRow/AddLabel/AddButton/Sanitize를 호출한다.
- 현재 의미 그래프에서도 제작→개량 참조11회, 개량→제작 참조75회가 잡힌다. 이는 두 개의 실제 타입 사이 순환이며, DTO가 같은 파일에 있어서 생긴 통계만은 아니다.
- 최소 개선: 공유 UI 생성 함수만 기존 view factory 계층으로 옮긴다. 제작 화면이 개량 표시를 조립하는 한 방향 관계는 유지한다.
- 우선순위는 낮다. 이 순환 하나가 현재 게임을 막는다고 보지 않으며 수백 개 새 인터페이스를 만들 근거도 아니다.

## 전체 의존 구조의 해석

기본 어셈블리 게임 파일2,198개에서 방향 의존18,707개, 순환이 있는 SCC21개가 확인됐다. SCC는 서로 도달 가능한 파일 묶음이다. 최대 묶음은1,000개다. 이것은1,000개 독립 순환이나1,000개 결함이라는 뜻이 아니다.

큰 묶음에는 Character244, Infrastructure121, Narrative111, Offense67, Economy65, Items52, Buildings47, Combat46개 등 여러 영역이 함께 들어간다. CharacterActor를 참조하는 게임 파일604개, BuildableObject를 참조하는 게임 파일496개가 이 기본 어셈블리 그래프 안에 있다. 이 값은 공용 중심 타입의 변경 파급 단서이며 그 자체로 모두 잘못된 참조는 아니다.

명시적 game asmdef45개의 선언 참조는 순환0이다. 따라서 문제가 있는 곳은 어셈블리 사이 컴파일 순환이라기보다, 큰 기본 어셈블리 내부의 구체 서비스와 MonoBehaviour 간 경계다. 파일 단위 그래프에는 같은 파일에 선언된 계약과 구현 때문에 커 보이는 연결도 있다. SDK 소스를 입력 바인딩에 사용한 부분은 있어도 SCC의 게임 후보 수에는 포함하지 않았다.

우선은 S02의 쓰기 권위와 재진입 흐름, S06의 도메인 역참조, S11의 명확한 UI helper 순환부터 다루는 편이 타당하다. 큰 SCC 전체를 없애는 목표로 인터페이스를 대량 생산하는 것은 권하지 않는다.

## 큰 파일이지만 즉시 분리할 근거로 세지 않은 부분

| 대상 | 이번 판단 |
|---|---|
| FacilityOutputExactRouteService | 경로 custody, receipt, ACK, 복원과 checkpoint GC가 같은 물리 전달 불변식에 연결됨. 3,274행만으로 분리 판정하지 않음 |
| ApparelWorkOrderRuntime / EquipmentEvolutionRuntime | 긴 상태 전이와 물리 재시도 검증이 다수. 검증 제거나 형식적 partial 분할 대상 아님 |
| DungeonAggregateReferencePreflight | 여러 영역을 함께 보는 cross-save join은 필요한 조립 책임. 개별 join의 변경이 실제로 어려울 때 기존 validator 경계 활용. 외향 의존60개만으로 새 분리 항목을 만들지 않음 |
| DungeonWorldSimulationRegistration | Register 메서드1,404행이지만 분기 구문1개인 명시적 조립 목록. 실행 정책이 섞이지 않는 한 길이 자체가 갓 클래스의 근거는 아님 |
| CharacterAiDecisionSchedule / CandidateSelector | heap와 due-time은 같은 예약을 위한 자료구조이며 continuation과 commit 직전 재검사는 프레임 예산과 가변 상태 때문에 필요 |
| R26AnatomyPairGroupDrawer | 양쪽 게시 상태 확인과 두 노드 동시 수정은 실제 보호 계약. 인스펙터 검증을 줄일 근거 없음 |
| 팝업/feature presenter 조립 | UIManager, UiPopupService, UITabManager의 전달과 조립 자체는 역할에 부합 |

기존 크기 탐색 기준에 걸린 Runtime 타입123개 중2,000행 초과14개, 생성자 탐색 기준78개 중16인자 초과12개가 있다. 이 숫자를 결함 개수로 쓰지 않는다. 나머지 큰 타입도 자동 분리 대상으로 지정하지 않았다.

## 추가 변경 때 다시 볼 후보

- `ResourceUsageIndex.BuildStaticIndex`: 여러 영역의 생산/소비 지식을 수동 집계한다. 새로운 생산 경로를 추가할 때 누락될 위험은 있으나, 이번 조사에서 현재 특정 아이템의 누락을 입증하지 못했으므로 확정 결함에 포함하지 않았다.
- `WorldItemStackRuntime`: 저장, 물리 이동, 품목 전용 명령, 표시 토글까지 넓다. 이미 내부 소유자와 helper가 있어 public API 수만으로 재분리하지 않는다. 다음 실제 아이템 기능 변경 때 변경 전파와 단일 권위를 기준으로 좁혀야 한다.
- `CharacterBodyHealthRuntime`, `CaptivityRuntime`, `SurgeryRuntime`: 현재 상태 소유와 거래 경계를 확인할 필요가 큰 후보지만, 긴 API 목록만으로 서로 무관한 책임이라고 단정하지 않는다.

## 수정 순서와 완료 기준

1. S01 구매 제한 권위와 저장 복원 경계를 먼저 맞춘다. 중복 차감/배송 방지와 일일 제한을 구별해 검사한다.
2. S02 행동 상태와 종료 소유자를 좁힌다. 실행 중 중단, 작업 실패, 의료/수술 복원에서 행동 종료와 자원 해제가 한 번만 일어나는지 확인한다.
3. S03, S04, S05, S09의 중복 정책과 authoring 보호/등록 경계를 정리한다. UI와 Editor에서 같은 규칙을 읽는지와 기존 저장/게시 값 보존을 검사한다.
4. S06, S07, S08은 실제 책임을 옮기는 배치로 나눈다. 각 배치마다 해당 흐름의 실행과 pending 저장/복원/재시도를 확인한다. 기존 DTO와 receipt 계약을 먼저 유지한다.
5. S10, S11은 인접 변경 때 작은 함수/소유 위치 정리로 처리한다.

이번 결과는 조사 보고서다. 게임 코드와 에셋 수정, Unity 실행, 새 커밋/푸시와 main 병합은 수행하지 않았다. 이11개를 고치면 모든 구조 문제가 영구히 사라진다는 보증도 아니다.
