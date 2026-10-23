# Unity 실행 안내

이 문서는 Editor 제어·컴파일·Play 작업 때만 읽는다. [루트 지침](../../AGENTS.md)의 배타적 작업 창이 우선하며, 문서만 수정하는 작업에는 Unity를 실행하지 않는다.

## 연결 방식

프로젝트의 현행 제어 경로는 공식 Unity CLI와 `com.unity.pipeline`이다. 구형 Assistant relay `unity-mcp`는 비활성 상태로 보존한다.
이 설명은 연결 허가가 아니다. 현재 사용자가 MCP 사용을 금지한 작업에서는 그 결정을 유지하고 서버를 임의 등록·재활성화하지 않는다.

V27 headless 증거 실행은 `DUNGEONSTORY_V27_HEADLESS_EVIDENCE=1`을 명시한다. 기존 `V27CiEvidenceBatch` 부트스트랩이 고정 Assistant 패키지의 `RelayService.SuppressAutoStart()` 테스트 API를 호출해 사용하지 않는 편집기 relay의 자동 시작만 억제한다. 패키지의 internal 타입 때문에 정확한 public 메서드를 reflection으로 연결하며 API가 없거나 달라지면 실행을 실패시킨다. 실제 Assistant 호출의 lazy initialization과 게임 콘솔 오류 검사는 유지한다. 패키지의 180초 정상 유휴 종료가 오류로 기록되는 서버 실행 문제를 피하기 위한 설정이며 일반 Editor에는 적용하지 않는다.

명령은 실제 주 프로젝트를 지정한다. 별도 임시 Unity 프로젝트로 검증을 대체하지 않는다.

```powershell
unity command editor_status --project-path C:/Users/vulpo/Documents/DungeonStory --format json
unity command console --level warn --project-path C:/Users/vulpo/Documents/DungeonStory --format json
```

위 예시는 기존 프로젝트 지침의 CLI 진입점이다. 실행 환경의 명령·옵션 지원을 확인하고 사용한다.
필요한 한정 C# 검증은 `unity command eval --code ...`에 같은 `--project-path`를 전달한다.
구형 문서의 `Unity_RunCommand`·`Unity_GetConsoleLogs`를 현재 연결이 제공한다고 가정하지 않는다.

## 작업 창 전환

| 단계 | 담당자 행동 |
|---|---|
| 편집 준비 | Play 종료, 실행 중인 명령 없음, 컴파일/import/reload 종료와 미저장 상태를 확인하고 `EDIT_READY`를 전달 |
| `SOURCE_EDIT` | 지정 파일만 편집. Unity 명령·명시적 Refresh·컴파일·Play를 실행하지 않음 |
| 편집 동결 | 모든 작성자의 `READY/FROZEN`을 확인하고 편집 창을 닫음 |
| `UNITY_VERIFY` | 대기 중인 import/컴파일/reload 종료와 현재 소스 반영을 확인한 뒤 필요한 검증을 직렬 실행 |
| 다음 수정 | 검증 종료를 확인하고 새 편집 창을 연 뒤 수정 |

Unity 사전 검사·읽기 전용 Editor 명령도 Unity 실행으로 취급한다.
검증 중 발견한 코드·테스트 수정은 다음 편집 창으로 모은다. 자동 import 설정을 임의 변경하지 않는다.

## 실행과 복구

첫 진행의 좁은 서버 검증은 기존 `FirstRunObjectivePlayModeVerifier.RequestGameplayProgressionForBatch`를 격리된 Linux headless batch의 `-executeMethod`로 실행한다(`-quit` 없이 runner가 Play 종료 후 자체 종료). `DungeonPersistencePaths.IsIsolatedVerification`을 요구하며 사용자 persistence snapshot을 읽지 않는다. 기존 통제 파티 기술로 GameplayScene에 직접 진입하고, 실제 raycast를 통과한 UI pointer callback→상점 결제/물리 배송→설계도 AI 운반→연구 작업·완료와 기본 시설 재구매를 검사한다. 최초 Loose 물품 발행 관측까지만 직원 AI를 멈췄다가 원상복구하며 초기 설계도 운반은 실제 AI로 실행한다. 연구 계측은 다른 직원·생존 결핍·도구 공급·운반 action을 통제하고, committed 작업 주기마다 한 번의 실제 명령으로 같은 연구대를 유지한다. 대기 중 반복 replan으로 후보 계산을 초기화하지 않는다. 총45초 연구 budget과 실제 승인 작업/마모·도구 회수/교체·104-section aggregate capture/preflight/sealed JSON·첫 프로젝트 완료를 요구한다. 잘못된 기본 구매의 무변경·동일 키 재시도, 일일 상품 중복 거절도 확인한다. 로그 폴더에 create-only `first-run-progression.txt`를 남긴다. Title/준비 화면의 모델 추론·장치 입력 polling·픽셀 렌더링은 이 실행 범위가 아니고 screenshot은 `NOT_RUN`으로 기록한다. Windows 격리 런처의 allowlist를 확장한 entry는 아니다.

C1 입력/targeting 경계는 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch`로 검증한다. selector/resume 없는 정확한 entry만 허용한다. 실제 GameplayScene 준비 직원과 기존 직원 관리 UI의 Button pointer event, 자동 Play Update/OnDisable, production GraphicRaycaster/UI 차단 및 camera/collider/PhysicsWorldPointerRaycaster를 연결한다. 입력은 검사용 IPlayerInputReader이며 사용자 장치·포커스를 조작하지 않는다. 로컬 실제 skill transaction/observer로 무효 대상 재시도·하나의 cooldown/buff/outcome·중복/취소 무효과를 확인하고 공식 baseline world를 복원한다. pending skill 영수증의 공식 전체 월드 복원을 뜻하지 않는다. 실행별 create-only `skill-targeting-actual.txt`와 baseline JSON을 남긴다.

C5 노동/도구 경계는 `-Mode edit -ExecuteMethod ArchitectureRefactorVerificationBatch.RunCaptivityLabor`로 검증한다. 기존 내부 배치의 검증용 저장 루트와 create-only `captivity-labor-boundary.txt`를 사용한다. 실제 native actor/work·물리 repository/spawner·batch disposition에 통제 배송 요청을 연결해 허용 임계값/실패 원복, 도구 할당 ACK 후 종결 실패의 JSON 재시도, 노동 권한/타입/수명, 근무 시 마모와 파손 도구 반납 실패/무중복 재시도를 확인한다. 원 D09 전환6·shared care owner5·노역 outbox/도구 정의 검사도 유지하며 과거 고정 보고서를 덮어쓰지 않는다. 자연 운반/직원 AI나 pending labor의 공식 전체 월드 복원을 주장하지 않는다.

C9 특수 능력 경계는 `-Mode edit -ExecuteMethod ArchitectureRefactorVerificationBatch.RunExtremeTraits`로 기존 여섯 capability 계약과 Golden Harvest jackpot/loss 실제 crop/물리/JSON 재시도 본문을 실행하며 create-only `extreme-trait-boundary.txt`를 남긴다. 과거 V26 고정 보고서나 AssetDatabase.Refresh를 호출하지 않는다. 이어 별도 새 디렉토리에서 `-Mode edit -ExecuteMethod ArcaneOverchargeA07DebugScenarios.RunBatch`를 실행해 실제 body health/equipment/identity의 부분 실패·fresh JSON 복원·누락 효과만 재시도와 terminal 무중복을 검사한다. 두 entry는 SerializedStartup selector를 사용하지 않으며 자연 UI/AI나 capability 전체 월드 복원을 주장하지 않는다.

### 포커스를 빼앗지 않는 실행

- 사건 효과 표적 회귀와 변경 영향 CI도 같은 격리 런처에서 실행한다. `-SerializedStartup`과 함께 `V20ContentResolutionRecoveryDebugScenarios.RunPrioritizedEffectBoundaries`, `V27CiEvidenceBatch.RunImpactEffectContracts`·`RunImpactCoreContracts`·`RunImpactCharacterContracts`는 `-Mode edit`, `RunImpactWorldRestore`·`RunImpactCrops`는 `-Mode play`를 사용한다. 여섯 정확한 진입점은 추가 selector/resume을 받지 않으며 edit 실행도 전용 persistence 경로를 사용한다. 해당 자식 프로세스에만 headless relay 억제 환경을 설정하고 기존 OS 격리 probe·메모리·시간 제한을 유지한다. `RunImpactCrops`는 기존 고정 crop 보고서를 재작성하므로 실행 전 보존하고 실제 결과를 별도 증거로 회수한 후 역사적 보고서 원본을 복구한다.

- 필수 경계는 **사용자 입력 데스크톱과 Unity 실행 데스크톱의 분리**다. 숨김 창 옵션은 보조 수단이며 그 자체를 안전 증거로 삼지 않는다.
- 직렬 시작은 job thread와 import process를 별도로 제한한다. `-job-worker-count 1`과 함께 `-desiredWorkerCount 1 -standbyWorkerCount 0`을 사용한다. in-process 초기 refresh 이후 Play 진입에서 import worker가 생성될 수 있으므로 초기 refresh 옵션만으로 프로세스 수 제한을 주장하지 않는다. 이 옵션은 해당 세션에만 적용된다([Unity 6000.3 CLI](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html#asset-import-arguments)).
- 제공 런처 `Tools/QA/IsolatedUnityLauncher.cs`는 별도 비대화형 window station과 desktop을 만들고 `CreateProcessW`의 `STARTUPINFO.lpDesktop`에 그 대상을 명시한다. Unity를 정지 상태로 생성해 UI 제한·kill-on-close job에 넣은 뒤 실행하며, 자식 프로세스까지 함께 관리한다. `SwitchDesktop`이나 사용자 창 활성화 API를 호출하지 않는다. Windows 작업 보기의 가상 데스크톱 전환으로 대체하지 않는다.
- 별도 데스크톱을 쓸 수 없으면 이미 허용·구성된 비대화형 세션 또는 VM을 사용한다. 계정·서비스·원격 환경을 임의로 새로 만들거나 현재 사용자 데스크톱 실행으로 폴백하지 않는다.
- 격리 환경에서도 `-batchmode -nographics`, 명시적 프로젝트/로그 경로, 단일 실행, 격리 저장 경로를 유지한다. 창 숨김 설정은 계속 사용하되 포커스 안전성의 근거로 내세우지 않는다.
- 실행 전 런처가 대상 데스크톱을 명시하고 현재 입력 데스크톱을 사용하지 않는지 확인한다. 순수 검사 후 OS가 비대화형 station임을 확인한 프로브에서만 창 생성·종료를 시험하고, 통과한 동일 격리 경로에서 Unity를 검증한다. 일반 시작뿐 아니라 PlayMode 전환·도메인 리로드·오류 창·종료 경계도 검증 대상이다. 현재 사용자 데스크톱에 미검증 프로세스를 띄우는 시험은 금지한다.
- **2026-09-26 검증:** 비대화형 station/desktop 자식의 실제 창 생성·종료, Unity 컴파일·원장 core 회귀, 공식 전체 월드 81-section 저장 왕복·실제 자동저장이 통과했다. 최신 `u3-isolated-campaign-r7`은 실제 GameplayScene 훈련 4회·레벨 1→5→공식 world 복원→fresh owner/pending→동일 요청 wire 재생까지 PASS했다. 통합 `u3-exact-json-integration-r1`은 142/142 PASS다. 첫 통제 production interaction의 요청 증거이며 전체 장기 학습 파이프라인이나 오류 창 등 모든 UI 경계까지 전수 검증했다는 뜻은 아니다. 실행별 소스 해시·증거와 실패 이력은 `progress.md`를 따른다.
- 진입점은 아래 `run_isolated_unity_batch.ps1`이다. 호출마다 현재 런처를 새 `.launcher` 폴더에 컴파일하고 순수 검사·소스/바이너리 해시를 남긴다. 숨김 실행만 하던 `run_hidden_unity_batch.ps1`은 명시 오류로 차단되어 있다. 격리 생성/프로브 실패 시 Unity는 시작하지 않으며 대화형 실행으로 돌아가지 않는다.
- `-SerializedStartup`은 추가 selector/resume 입력 없는 `ArchitectureRefactorVerificationBatch.RunCore`·`ArchitectureRefactorVerificationBatch.RunCampaign`·`ArchitectureRefactorVerificationBatch.RunProductionBills`·`ArchitectureRefactorVerificationBatch.RunBuildings`·`ArchitectureRefactorVerificationBatch.RunDefense`·`ArchitectureRefactorVerificationBatch.RunBuildingVisual` edit, 전용 T01/T02 edit, 추가 selector 없는 crop/research/B129/B130/B131/B132 edit 검사, 명시 `-U0E09EnvironmentExposure` play 또는 추가 selector 없는 `ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch`·`GameWorldLineageDebugScenarios.RequestActualContainerForBatch`·`NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch` play에서만 사용할 수 있다. 정확한 edit entry는 crop 두 observer entry, ResearchWorkObserverDebugScenarios.RunFromBatch, RemainingPresentationObserverDebugScenarios.RunForBatch, MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch, ObserverCompositionClosureDebugScenarios.RunForBatch, PilotInputExperienceProfileExporter.RunForBatch이다. B132 edit는 기존100개의 source-bound 입력을 현행 경험 policy로 읽기 전용 재평가하며 Play/게임 저장/응답 생성을 수행하지 않는다. B131 전용 play는 실제 컨테이너의149 module/현재 fact binding exact-set만 짧게 읽고 create-only report 후 종료한다. B140 전용 play는 실제 GameplayScene 컨테이너의 저장 계보를 검사하며 별도 selector 없이 정확한 entry만 허용한다. narrative context lifetime 전용 play는 실제 GameplayScene의 request context resolution과 통제한 복수 컨테이너의 Dispose/recreate 수명, 이미 만든 public context의 고정 상태를 검사한다. 이어 실제 StartPreparationScene UI에서 후보 생성·문맥 JSON 왕복·후보 교체의 기존 요청 취소·전체 취소 후 재시작·Title 이동 후 context 폐기를 검사해 create-only report를 남긴다. 준비 후보의 실제 프로필/원장을 사용하며 모델 응답이나 스킬 선택 완료를 주입하지 않는다. 모델 추론이나 준비 파티의 Gameplay 게시 완료 증거는 아니며 B131 observer entry에는 포함하지 않는다. 해당 자식 실행에만 import를 in-process, Unity worker와 BEE 빌드를1작업으로 제한하며 다른 앱·OS설정·기본 실행은 바꾸지 않는다. 메모리 보호 기준을 유지하며 launcher-context.txt로 적용 설정을 확인한다. entry 허용·잘못된 mode/접두 entry/추가 selector 거절을 순수 검사에서 확인한다. 설정·순수 검사 PASS는 현재 소스 컴파일/실제 실행 PASS를 대신하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/QA/run_isolated_unity_batch.ps1 `
  -ExecuteMethod GameplayLedgerAuditRemediationDebugScenarios.RunLedgerTemporalBatch `
  -OutputDirectory C:\Users\vulpo\Documents\DungeonStory\Artifacts\QA\<새-실행-폴더> `
  -Mode edit
```

`ExecutionPolicy Bypass`는 해당 자식 PowerShell에만 적용하며 시스템 정책을 바꾸지 않는다. `play`와 `campaign`은 `DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch`만 허용한다. `campaign`은 응답 없이 실제 요청을 관측하는 U3 통제 시험이다. 프로젝트 버전의 Unity 경로를 기본 선택하고 설치가 다르면 명시적 `-UnityExecutable`을 준다. 기존 증거 경로와 실행 중인 Unity가 있으면 거절한다. 테스트 산출물은 `<실행-폴더>/unity-evidence`, OS 격리 증거와 Unity 로그는 `<실행-폴더>`, 빌드/런처 로그는 `<실행-폴더>.launcher`에 남는다. 프로세스 exit0만으로 테스트 PASS를 선언하지 않는다.

- 시설 campaign의 실제 미완료 저장 또는 `-MealContinuation`의 현재 스키마와 호환되는 식사 중 QA checkpoint를 새 프로세스에서 이어 실행할 때만 `-CampaignResumeWorld <이전 QA JSON 절대경로> -CampaignResumeSha256 <파일 SHA-256>`를 함께 지정한다. 같은 프로젝트 `Artifacts/QA` 아래 원본 파일만 읽으며 재분석 지점·해시 불일치·부분 옵션은 Unity 시작 전에 거절한다. 기본 실행은 상속된 resume 환경변수를 제거하고 자동으로 최신 파일을 고르지 않는다.
- 재개 시험은 공식 전체 월드 복원과 fresh owner/session/faction/원장 보존을 먼저 확인한다. 이미 결제된 route를 재사용하며 이전 skill/trait PASS를 이번 실행의 PASS로 복사하지 않는다. 240실시간 초의 한정 실행마다 진행 시각을 기록하고 미완료 상태를 별도 checkpoint로 보존한다. 게임 시간 부족을 교역 실패/완료로 위장하지 않는다.

- `-Mode campaign -MedicalContinuation`은 실제 의료 owner의 공식 전체 월드 복원 시험이다. fixture 질병/물자와 통제 배송/치료 명령을 사용하며 자연 AI·질병 발생 시험이 아니다. FacilityContinuation 및 CampaignResumeWorld와 함께 쓸 수 없다. 같은 격리 런처/프로브와 create-only 출력 정책을 사용하고, 상속된 의료 시험 환경변수는 제거한 뒤 명시 옵션일 때만 주입한다.

- `-Mode campaign -ShopContinuation`은 실제 상점 사용 owner의 공식 전체 월드 복원 시험이다. 일반 campaign과 달리 손님의 기술 준비에는 실제 합법 요청을 읽은 명시적 시험 응답을 사용하며 모델 추론·학습 데이터가 아니다. 응답은 생산 품질 검사와 기술 parser를 그대로 거친다. FacilityContinuation, MedicalContinuation 및 CampaignResumeWorld와 함께 쓸 수 없으며, 상속된 상점 continuation 환경변수는 제거한 뒤 이 명시 옵션일 때만 주입한다.

- `-Mode campaign -MealContinuation`은 실제 authored D04와 owner `AIEat`의 공식 전체 월드 복원 시험이다. 통제된 허기와 배송 완료 음식으로 whole-world `eating`, `effects-published`, `completed` 각 상태의 저장·복원을 확인한다. FacilityContinuation, MedicalContinuation 및 ShopContinuation과 함께 쓸 수 없으며, 상속된 식사 continuation 환경변수는 제거한 뒤 이 명시 옵션일 때만 주입한다. resume 옵션이 없는 기존 r5는 같은 프로세스 안에서 이 세 상태를 복원하는 범위를 그대로 유지한다. 손님 결제, 자연 음식 생산 및 훈련은 이 시험 범위가 아니다.
- Meal cold-resume는 `-MealContinuation`에 현재 스키마(v15, CON03 물리 context/효과 미적용 사유와 A08 단식 후속 전달 표식 포함) 식사 중 QA checkpoint의 명시적 `-CampaignResumeWorld` 경로와 `-CampaignResumeSha256` 해시를 함께 지정한다. 구형 v12/v13/v14 QA를 수정하거나 현재 계약으로 위장하지 않는다. 새 격리 프로세스에서 정확히 하나의 `Eating` plan과 정확한 owner가 checkpoint에 있는지, 복원 전 raw clock이 저장된 `createdAt`보다 작은지 확인한 뒤 재개한다. 과거 r8/r9 및 v14 실행은 당시 소스의 역사적 증거이며 v15 PASS가 아니다. 이 cold 모드는 같은 프로세스 검증을 대체하지 않는 별도 증거다.
- 현재 MealContinuation은 위 식사 시험 뒤 실제 AIDrink의 이동 중 예약 저장·거절 무변경·성공 복원 후 transient 예약 정리·재계획 소비1회·완료 재복원도 검사한다. 음수 원본과 복원본은 각각 `drink-*-world.json`에 보존한다. 이 플래그의 resume 입력은 식사 중 checkpoint만 지원하며, 이 뒤의 음수 시험을 cold 음수 복원으로 부르지 않는다. 새 게임에서 식사 시작에 실패하면 물 branch는 미실행이다. 실패는 `failure-world.json`과 관측 보고로 보존하며 기존 성공 checkpoint 재개를 fresh 진입 PASS로 대신하지 않는다.
- `-Mode campaign -DrinkContinuation -CampaignResumeWorld <음수 이동 중 QA JSON 절대경로> -CampaignResumeSha256 <SHA-256>`는 별도 cold 음수 시험이다. 다른 continuation 플래그와 상호 배타이며 명시 resume 쌍이 필수다. 실제 QA 저장의 personal water claim1개/비교용 다른 예약을 확인하고 새 프로세스의 raw 시각이 저장 timeline보다 작은지 검사한다. 다른 physical 상태/needs 보존·transient claim 제거·실제 재계획/소비1회·완료 재복원을 검증한다. 실행하지 않은 식사 assertion은 false로 유지하며 보고 범위를 분리한다. 상속된 drink 환경변수는 제거하고 명시 옵션에서만 주입한다.
- MealContinuation의 late-publication 분기는 실제81개 section/등록된 모든 production participant를 재사용한 registry에 마지막 통제 fault를 추가한다. 즉시 전체 snapshot과actor/action/lease/revision/임시scale byte·reference 동일을 검사한다. 다음 paused frame에는 무복원 대조군에서도 갱신되는 wildlife.runtime RNG와 module.instanceEvolution.facilityStateVersion 두 필드만 비교 모델에서 정규화하며 나머지모든값/원본은그대로다. 원래 coroutine의 재계획 없는 완료까지별도로검증한다. 이것을 모든생산자/시간anchor/자연게임플레이 검증으로확대하지않는다.

- Unity 담당자 한 명만 한 번에 하나의 명령을 실행한다.
- WIM044 경력/은퇴 통합은 `-Mode play -ExecuteMethod V20CampaignDebugScenarios.RequestWim044IsolatedBatch`만 사용한다. 추가 selector/resume 인자는 거절한다. 기존 전체 검사와 경력 실제 Tick·숙련도 승급·저장 왕복 판정은 유지하며, 보고서와 여섯 단계 world JSON은 새 `unity-evidence` 아래 create-only로 기록한다. 역사적 `Artifacts/QA/wim-implementation/wim-044-retirement-choice-schedule.txt`를 덮어쓰는 일반 메뉴 entry를 배치에서 호출하지 않는다. 생산 UI dispatcher 호출은 격리 Unity 내부 경로이며 사용자 데스크톱 입력 자동화가 아니다.
- 급여 완료 cold 시험은 `-Mode play -U0E11Capture -ExecuteMethod CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch`에 명시적 `-CampaignResumeWorld <급여 완료 QA world 절대경로> -CampaignResumeSha256 <64자리 SHA-256>` 쌍만 허용한다. 기존 campaign 자동 재개와 분리된 환경변수로 공식 전체 복원·원 소비/효과·동일 commit 재전달·정상 돌봄 주기를 검사한다. 같은 world의 `.observation.json`은 원본 해시와 정확히 결박하며 두 파일을 수정하지 않는다. 일반 capture/transport는 resume를 계속 거절하고 다른 selector(명시 false 포함)도 함께 쓰지 않는다. 코드 존재만으로 cold 실행 PASS를 의미하지 않는다.
- 현재 급여 checkpoint는 circus V8을 사용한다. process-local `nextCareAt`은 저장하지 않고 owner가 계산한 유한 0..5초 `remainingCareSeconds`를 새 clock에 복원한다. V6 QA 파일을 수정하거나 이관하지 않으며, 실제 새 V8 급여/저장 실행에서 만든 checkpoint만 재개한다. cold 음성 검사는 원본 불변 메모리 복사본의 manifest를 별도 봉인해 실제 잘못된 housing 결합이 Stage에서 거절되고 live 상태가 보존되는지 확인한다.
- 도축 완료 신원 pending cold 시험은 `-Mode play -U0E11Capture -ExecuteMethod CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch`와 `-CampaignResumeWorld <원 actual real-capture-slaughter-identity-pending-world.json 절대경로> -CampaignResumeSha256 <원 파일 SHA-256>` 쌍만 사용한다. 일반 capture/transport와 급여 cold는 기존 guard를 유지한다. 원 checkpoint/child/사체를 그대로 공식 복원하고 정상 owner Tick·독립 완료 이벤트·중복 방지를 검사한다. 잘못된 참조 음성 입력은 원본 파일을 바꾸지 않는 manifest 봉인 메모리 복사본이며 실제 실행 전에는 PASS가 아니다.
- 긴 작업은 기존 실행 handle로 확인한다. timeout만으로 작업 종료를 가정하거나 동일 명령을 재실행하지 않는다.
- 연결이 불명확하면 프로세스·로그 등 읽기 전용 증거로 현재 상태를 대조한다.
- 세션 중단·재개 시 과거 창 허가는 만료된다. 상태가 확인될 때까지 새 편집/실행을 시작하지 않는다.
- 미저장 변경을 잃을 수 있는 Editor 종료·재시작·씬 저장을 임의로 수행하지 않는다.
- 연결 확인만을 위해 테스트, 별도 Editor 실행, 씬 저장 또는 Editor 종료를 하지 않는다.
- 타입 위치는 원본 C#/asmdef에서 찾는다. Unity 메인 스레드에서 전체 AppDomain 타입을 열거하지 않는다.

## 성공 판정

캐릭터 복원 후보 수명은 `-Mode play -SerializedStartup -ExecuteMethod GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch`, 복원 적용/후처리 실패 계약은 `GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch`의 전용 play entry로 검증한다. 두 entry는 selector/resume 없이만 허용한다. 전자는 detached 후보 취소·stale token·stage·재시도와 live world 무변경을, 후자는 통제 commit/completion/hook/rollback 실패와 실제 컨테이너의 적용 결과·저장/진행/UI 차단을 구분한다. 전체 정상 월드 복원은 기존 `RequestActualContainerForBatch`에서 별도로 검사하며 모든 실행은 동일 OS 격리·메모리 보호를 유지한다.

캐릭터 정의/실행 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunCharacterDefinitions`로 검증한다. 정확한 entry만 selector/resume 없이 허용하며 다른 mode/유사 prefix는 순수 guard/native 자체 검사에서 거절한다. authored 캐릭터 정의의 factory/editor profile·JSON 복원·fixed-skill clone과 실제 personality semantic query/null 실패 순서, 기존 character model/action descriptor/work priority 소비자를 검사하고 실행별 create-only `character-definition-boundary.tsv`를 남긴다. 실제 GameplayScene 전체 복원은 별도 play entry이며 동일 OS 격리 probe와 메모리 보호를 유지한다.


EquipmentEvolution 런타임 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunEquipmentEvolution`로 검증한다. 정확한 entry만 selector/resume 없이 허용하며 다른 mode·유사 prefix와 추가 입력은 순수 guard/native 자체 검사에서 거절한다. 통제된 runtime queue의 수락·취소·재시도, JSON fresh-owner restore, 기존 lower-material outbox/input owner 회귀를 검사한다. 실제 자연 UI나 장기 campaign 실행은 이 범위에 포함하지 않는다.


Consumables policy 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunConsumablePolicies`로 검증한다. 정확한 entry만 selector/resume 없이 허용하며 다른 mode·유사 prefix와 추가 입력은 순수 guard/native 자체 검사에서 거절한다. 통제된 policy reserve/publish failure, JSON parent restore, ACK replay와 persistent actor retirement 회귀를 검사한다. 자연 UI/AI 실행이나 full cold-world 증거는 이 범위에 포함하지 않는다.


Consumables 실행 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunConsumableExecution`로 검증한다. 정확한 entry만 selector/resume 없이 허용하며 다른 mode·유사 prefix와 추가 입력은 순수 guard/native 자체 검사에서 거절한다. 통제된 실제 runtime의 meal/substance/detox/시간효과, ACK 손실, JSON fresh owner restore와 동일 재시도를 검사한다. 자연 UI/AI 실행이나 full cold world 증거로 확대하지 않는다.


이동 소유권 분리는 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestActualContainerForBatch`로 검증한다. 정확한 fresh entry만 selector/resume 없이 허용한다. pure operation/receipt 8묶음 뒤 실제 GameplayScene의 protected route·선점 거절·도착 후 소유·취소, 실제 캐릭터 DTO의 receipt JSON/fresh owner/동일 재시도·해제를 확인한다. QA response ID는 campaign response가 아니므로 전역 복원 전에 해제하며 기존 official world 저장을 복원해 새 actor에 transient movement가 이어지지 않는지 검증한다. 기존 Brain/behavior tree fault runner의 core2/destinationless6을 실행별 새 QA 디렉터리에 기록한다. 과거 보고서를 덮어쓰지 않고 runner의 기존270초 실패 한계·OS 격리·메모리 보호를 유지한다. 전체104-section 복원/변조 검사는 별도 world entry다.

MovementOwnership의 destinationless/deprivation/primitive runner는 각각 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestDestinationlessForBatch`, `RequestDeprivationForBatch`, `RequestPrimitiveForBatch`의 전용 fresh process 경계에서 실행한다. 기존 actual movement 8/core2 PASS 뒤 destinationless6·deprivation5·primitive6을 메모리 보호를 위해 분리하며 assertion 270초, 단위 대기 20초, 1GiB/2GiB 메모리 임계값과 OS 격리 probe는 변경하지 않는다.

Restock 전용 play는 기존 WimDoorConsumer의 pending/no-path, door cancellation, 실제 pickup/retail transfer와 자연 retry·수량 보존을 유지한다. 같은 실행 중 실제 Pending 예약과 물리 pickup 후 운반 상태를 각각 공식 whole-world JSON으로 캡처한다. 원래 시나리오 종료 후 각 JSON을 세 번 새 actor/shop/물리 원장으로 복원한다. 두 번째 취소 묶음 앞에서는 취소하지 않은 이전 cargo owner를 새 owner로 교체해 예약·운반품 보존을 먼저 확인하고, 각 취소 묶음에서 기존 작업 취소를 두 번씩 호출해 lease/shop admission 제거·상점 재고/물리 수량 보존·운반 물품의 현재 칸 회수를 확인한다. 이전 coroutine을 복원하지 않으며 finally에서 실행 전 baseline을 공식 복원하고 모든 정리 실패를 실패 보고에 보존한다. checkpoint JSON과 보고서는 실행별 evidence 경로에 create-only로 작성한다. 2026-10-17 current native r7은 정상 pickup/shop 입고와 selector reinjection을 통과한 뒤 pending world 복원 후 취소에서 예약 잔류를 재현했다. participant226은 기존 shop/quantity/fresh actor의 정확한 join으로 transient 취소 handle만 재연결하고, rollback·이전 actor retirement에서는 물리 명령 없이 그 handle을 분리한다. 이 수정과 live-owner 교체 검증의 실제 실행 결과는 해당 후속 QA 영수증을 따른다. 사고 경로와 디스크 slot 저장/로드는 이 entry의 검증 범위에 포함하지 않는다.

WorkAccident 전용 play는 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch`로 연결한다. 통제된 production 경계에서 `RecordApprovedWork`가 노동 원장을 먼저 기록한 뒤 사고를 적용하고 `AbortWorkRun`으로 종료하는 경로와, 후속 실패로 남은 기존 `WorkOrderRuntime` 후보 게시/완료를 `OnRestoreCompleted`에서 재시도하는 경로를 확인한다. 이는 controlled native 범위이며 whole-world restore나 자연 사고확률·UI 훈련 증거가 아니다. 화재 후처리 실패 입력은 실제 D04 Operate profile의 runtime-only SO clone에 명시적으로 구성하며 원 자산은 수정하지 않는다. 이 변형의 작업/화재 조합을 자연 콘텐츠 연결의 증거로 확대하지 않는다.

FactionReinforcement 전용 play는 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch`로 연결한다. 현재 entry는 실제 GameplayScene의 실제 campaign 관계/동맹/지원 요청 명령과 도착 fact producer로 live faction root에 partial 및 post-publication response-loss 경로를 생성해 공식 whole-world pending/completed JSON 복원, fresh actual actor/root, production Tick의 현재 root 완료·이전 객체 무변경·무중복 및 정상 entry/조립을 검사하고 baseline world를 복원한다. 여행 clock과 publisher 응답만 통제하고 원 영수증/aggregate 검증을 유지한다. 실행별 create-only checkpoint4와 faction-reinforcement-actual.txt를 남긴다. 이전 local route-only JSON entry 본문은 보존하지만 이번 scope와 구분한다. 자연 strategic route 생성이나 LLM 훈련 증거는 아니다.

치료 owner 검사는 `-Mode edit -ExecuteMethod ArchitectureRefactorVerificationBatch.RunTreatmentExecution`로 실행한다. 기존 내부 직원/유료 환자의 치료·해독 work/physical/service fixture와 연료 공급 검사를 재사용하며 기존 scoped QA persistence와 create-only `treatment-execution-boundary.txt`를 쓴다. 실제 Unity actor/물리 소비·치료 효과/결제·공식 physical/service/survival/consumables section 복원과 ACK 실패·소유자 상실·같은 요청 retry를 검사한다. 자연 운반/AI·의료 UI·whole-world 치료 복원으로 주장하지 않으며, 과거 WIM-062 메뉴의 고정 보고서를 덮어쓰지 않는다. 이 entry는 SerializedStartup을 사용하지 않는다.

작물 저장/복원 책임 분리는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunCropPersistence`로 검증한다. 정확한 entry만 selector/resume 없이 허용한다. 동일 runtime의 root 교체 두 번, 잘못된 candidate의 무변경 거절, DTO 깊은 복사와 공개 작업 ID를 검사하고 실제 파종/관개/수확/물 보충/치료/황금 수확의 기존 복원·재시도를 실행한다. 실행별 create-only `crop-persistence-boundary.tsv`, `crop-query-cache-boundary.tsv`, `crop-command-observers.txt`와 PASS marker를 남긴다. 조회 cache의 명령/복원 invalidation·이전 view 격리와 기존 실제 terminal/완료 사실 게시의 reserve 거절·ACK 손실·새 runtime 복원·정확 키/중복 방지를 함께 검사한다. 전체 GameplayScene 공식 복원은 별도 play entry이며 OS 격리 프로브와 메모리 보호는 동일하다.

건물 정의/렌더링 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunBuildingVisual`로 검증한다. exact entry만 selector/resume 없이 허용하고 잘못된 mode/prefix는 순수 guard/native 자체 검사에서 거절한다. 이전 nested enum과 generic dictionary/comparer의 Odin binding, legacy5에셋과3개 authored scene의 원 YAML key/reference,8종 component mapping/생성·거절, 실제 Tilemap 그리기/삭제와 기존 visual alignment·모듈러 에셋/건물 상태 복원을 검사한다. 실행별 create-only TSV를 남기며 asset/scene 저장은 하지 않는다. 전체 GameplayScene 공식 복원은 별도 play entry다.

방어 효과 정의/실행 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunDefense`로 검증한다. 정확한 entry만 허용하고 selector/resume·다른 mode·유사 prefix를 순수 guard/native 자체 검사에서 거절한다. 실제 D06의 부분 효과·게시 실패 rollback·저장, 발동/효과 ACK 손실 후 fresh-owner 복원·동일 재시도,6개 효과의 pure 명령 순서와 authored SO JSON/type/field 왕복을 검사해 실행별 create-only 보고서를 남긴다. 실제 GameplayScene 복원은 별도 play 검사이며 동일 OS 격리/메모리 보호를 유지한다.

건물 정의/실행 경계는 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunBuildings`로 검증한다. 정확한 entry만 허용하고 selector/resume·다른 mode·유사 prefix를 순수 guard/native 자체 검사에서 거절한다. 정의13종 JSON/타입/ID·실행 capability 등록, Inspector managed-reference/revision 교체·미등록/중복/무효 factory 거절과 재시도, authored 에셋 로딩/인스턴스·state module 저장 왕복·범죄/외부 작업·production 명령/출력을 검사한다. 실행별 create-only `building-definition-contracts.tsv`와 `ARCHITECTURE_BUILDINGS_BATCH`를 기록한다. 실제 GameplayScene 전체 월드 복원은 별도 play entry이며 격리 OS probe·메모리 보호를 동일하게 적용한다.

생산 주문 owner 분리의 표적 검증은 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunProductionBills`를 사용한다. 정확한 entry만 허용하고 selector/resume·다른 mode·유사 prefix를 순수 guard/native 자체 검사에서 거절한다. 실제 명령 게시 재진입/ACK before·after JSON fresh owner 복원, 정상·부패성 준비 출력 저장 왕복, 실제 bill WIP 영수증 GC의 missing/duplicate/candidate identity/revision drift/정확 rollback/재시도를 검사해 `ARCHITECTURE_PRODUCTION_BILLS_BATCH`를 기록한다. GC 하위 child receipt는 통제 입력이므로 자연 시설 파괴 전체 검증으로 확대하지 않는다. 실제 GameplayScene 전체 월드 복원은 별도 play entry로 확인한다. 동일 격리 OS probe·메모리 보호를 유지한다.

V20 사건/효과 owner 분리의 표적 검증은 `-Mode edit -SerializedStartup -ExecuteMethod ArchitectureRefactorVerificationBatch.RunCampaign`을 사용한다. 정확한 entry만 허용하며 selector/resume와 다른 mode/유사 접두 entry는 시작 전에 거절한다. 기존 milestone/faction/candidate join, 일일 효과 날짜와 root 교체, 사건 게시 재진입, 응답/생애/손님 영수증 및 복원·재시도를 실행하고 `ARCHITECTURE_CAMPAIGN_BATCH`를 기록한다. 전체 core 검사는 같은 campaign 묶음을 호출한 뒤 기존 물리 검사를 계속한다. campaign 전용 PASS를 전체 core PASS로 보고하지 않는다. 과거 WIM040 전역 보고서를 덮어쓰지 않고 실행별 evidence 디렉터리에 create-only 보고서를 쓴다. 격리 프로브·메모리 보호 기준은 동일하다.

B139 공개 스킬 근거 회귀는 `-Mode edit -SerializedStartup -ExecuteMethod V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch`를 사용한다. 정확한 entry만 허용하며 기존 고정 캡처로 공개 선정/근거 매핑/JSON 복원을 검사한다. 새 게임 실행이나 모델 추론이 아니며 파생 공개 입력은 create-only 출력과 비학습 scope 표식을 갖는다.

B141 전체 복원 검사는 현재 컴파일을 마친 뒤 Roslyn 공유 컴파일 서버가 같은 격리 job에서 약3GB working set을 유지하는 경우를 확인했다. 메모리 보호로 중단됐다면 본체와 소유 보조 프로세스를 구분해 계측한다. 컴파일 성공·source pin 무변경·기존 job 종료를 확인한 다음 새 출력 디렉터리에서 같은 검사를 실행하면 이미 검증된 current-source 캐시를 사용하며 보조 서버의 잔류 비용을 분리할 수 있다. 이는 B141 r3에서 실제 확인한 절차이지 모든 메모리 실패의 자동 재시도나 근본 해결 보장이 아니다. current compile 실패/소스 변경 시 이 절차로 구 DLL을 통과 처리하지 않으며, 보호 임계값·검사 범위는 유지한다. `WorldRestoreMemory-20261011-b141-r1/verification-r3.json`을 증거로 참조한다.

B134 이름/직급 회귀는 `-Mode edit -SerializedStartup -ExecuteMethod MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch`도 허용한다. 이 정확한 entry만 단일 import/worker로 실행하며, 잘못된 mode와 유사 접두 entry는 순수 검사에서 거절한다. 실제 CharacterActor의 개인 이름 포트·모델 이름/역할 분리와 시설 observer/저장·재전달 회귀를 확인한다.

현재 소스의 관련 컴파일 결과와 어셈블리 신선도를 확인한다.
`IsCompiling=false`, 콘솔 런타임 오류 0, 서버 발견만으로 현재 소스의 컴파일 성공을 판정하지 않는다.
현재 컴파일이 실패했는데 구형 DLL로 실행된 검사는 `NOT_VALID_CURRENT_SOURCE`이며 PASS 증거가 아니다.

필요한 Refresh·컴파일은 작성자를 동결한 뒤 관련 배치당 한 번 수행한다.
기존 경고와 새 오류를 구분하고, 실행한 경로·결과·남은 실패만 보고한다.


물리 철거의 실제 저장·복원/재시도는 `-Mode play -SerializedStartup -ExecuteMethod PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch`의 전용 fresh entry로 실행한다. selector/resume/다른 mode는 허용하지 않는다. 기존 authored P03 sawmill의 prepared-output destructive-drain coroutine와 검증식은 그대로 재사용한다. 격리 batch만 기존 byte-preserving sanitized Gameplay scene lease의 정확한 경로에서 공식 fast-party command로 시작 파티를 준비한다. 현재 필수 철거 participant8의 정확한 ID 집합과 registry@5 지문을 함께 검사한다. 필요한 연구는 기존 정상 완료 명령으로 준비해 unlock과 완료 영수증의 부모 참조를 함께 게시한다. 저장 preflight를 우회하거나 완료 상태만 직접 주입하지 않는다. 구형 준비 UI 가정에서 멈춘 native r3 이후의 진입 수정이며, 수동 verification의 Title/준비 UI 경로는 유지한다. batch 보고서는 통제한 시작 파티 준비를 명시하고 사람의 준비 UI·모델 추론을 통과했다고 주장하지 않는다. batch request/report는 실행별 `unity-evidence` 아래에 create-only로 작성하며 기존 Temp/menu request·report와 사용자 persistence snapshot을 사용하지 않는다. launcher의 격리 저장 root와 OS station/desktop probe를 요구하고, 보고서·Play 종료·소유 start-scene lease 정리 뒤 종료 코드를 반환한다. 메모리 보호/timeout·사용자 데스크톱 비간섭 기준은 같다. 중첩 coroutine의 factory/MoveNext/Current/Dispose 예외와 중도 중단도 원래 예외를 보존하며 실패 영수증·종료 코드1로 연결한다. 이미 작성한 보고서는 덮어쓰지 않고 별도 오류 기록을 남기며, 보고서 작성 실패에도 소유 request 정리와 Play 종료를 시도한다. 소유 scene lease의 복구 실패도 종료 실패로 처리한다.

MovementOwnership의 base entry는 actual movement/receipt·공식 actor/work-module graph 복원과 core2까지만 실행한다. destinationless6/deprivation5/primitive6은 위 전용 entry에서 각각 새 프로세스로 실행하며 생략하지 않는다. restock entry는 실제 pending 작업 중 selector 재주입에서 같은 command/executor/run/cargo·lease payload를 보존하고 기존 NoPath/cancel·natural retry 경로를 이어서 확인한다.

의료 이송의 실제 확인은 `-Mode play -SerializedStartup -ExecuteMethod MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch` 전용 fresh entry를 사용한다. plain Play도 허용하되 다른 mode·selector·resume·접두 entry는 거절한다. 기존 isolated Gameplay/공식 시작 파티 뒤, batch의 방문객 준비는 기존 population preview의 level1 통제 후보와 Progression 선택/자동 습득 명령을 사용한다. population owner의 Changed 동기화·준비 완료·pending 요청 퇴역을 거치며 실제 프로필 DTO의 준비 플래그/성장 복사로 통과시키지 않는다. 준비 UI나 모델 추론은 검증 범위가 아니다. 검증 전 방문객0을 확인한 batch만 자동 생성 coroutine을 중단하며 명시적 TrySpawnCharacter/입장과 기존 respawn Update는 유지한다. 공식 복원 뒤에는 Unity의 지연 폐기 frame 경계를 기다리고 같은 profile의 중복 준비 owner를 거절한다. 일반 Customer도 독립적으로 저장되는 현행 계약에 따라 incoming ordinary/Society 소유권과 물품·돈 보존을 정확히 검사한다. 해결된 사건의 방문객은 기존 BeginExit 명령으로 퇴장 작업을 시작한 뒤 public 인계와 정확한 퇴역을 검사한다. 기존40초 방문객 입장 예산과 실제 TrySpawnCharacter/입장 경로를 유지한 뒤 WIM040의 실제 물리 식사→신체 붕괴→Society transfer 선택→staff AIRescue와 공식 in-memory capture/JSON/restore를 재사용한다. batch는 기존 고정 수동 보고서를 건드리지 않고 실행별 `society-medical-transfer-actual.txt`를 create-only로 남긴다. 자동 disk persistence는 격리 root의 해당 실행에서 정지하며 이것은 disk slot load·치료 완료 증거가 아니다. 기존 coordinator가 결과/실패·iterator 정리 뒤 종료 코드를 소유하고, C3와 C16은 공통 Editor coroutine observer로 nested/factory/disposal/중도 중단 실패를 기록한다. 메모리·시간·비간섭 기준은 기존과 같다. 이 entry의 코드 존재/순수 검사/컴파일과 실제 실행 PASS를 구분한다.
