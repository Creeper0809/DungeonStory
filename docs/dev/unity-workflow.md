# Unity 실행 안내

이 문서는 Editor 제어·컴파일·Play 작업 때만 읽는다. [루트 지침](../../AGENTS.md)의 배타적 작업 창이 우선하며, 문서만 수정하는 작업에는 Unity를 실행하지 않는다.

## 연결 방식

프로젝트의 현행 제어 경로는 공식 Unity CLI와 `com.unity.pipeline`이다. 구형 Assistant relay `unity-mcp`는 비활성 상태로 보존한다.
이 설명은 연결 허가가 아니다. 현재 사용자가 MCP 사용을 금지한 작업에서는 그 결정을 유지하고 서버를 임의 등록·재활성화하지 않는다.

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

### 포커스를 빼앗지 않는 실행

- 필수 경계는 **사용자 입력 데스크톱과 Unity 실행 데스크톱의 분리**다. 숨김 창 옵션은 보조 수단이며 그 자체를 안전 증거로 삼지 않는다.
- 제공 런처 `Tools/QA/IsolatedUnityLauncher.cs`는 별도 비대화형 window station과 desktop을 만들고 `CreateProcessW`의 `STARTUPINFO.lpDesktop`에 그 대상을 명시한다. Unity를 정지 상태로 생성해 UI 제한·kill-on-close job에 넣은 뒤 실행하며, 자식 프로세스까지 함께 관리한다. `SwitchDesktop`이나 사용자 창 활성화 API를 호출하지 않는다. Windows 작업 보기의 가상 데스크톱 전환으로 대체하지 않는다.
- 별도 데스크톱을 쓸 수 없으면 이미 허용·구성된 비대화형 세션 또는 VM을 사용한다. 계정·서비스·원격 환경을 임의로 새로 만들거나 현재 사용자 데스크톱 실행으로 폴백하지 않는다.
- 격리 환경에서도 `-batchmode -nographics`, 명시적 프로젝트/로그 경로, 단일 실행, 격리 저장 경로를 유지한다. 창 숨김 설정은 계속 사용하되 포커스 안전성의 근거로 내세우지 않는다.
- 실행 전 런처가 대상 데스크톱을 명시하고 현재 입력 데스크톱을 사용하지 않는지 확인한다. 순수 검사 후 OS가 비대화형 station임을 확인한 프로브에서만 창 생성·종료를 시험하고, 통과한 동일 격리 경로에서 Unity를 검증한다. 일반 시작뿐 아니라 PlayMode 전환·도메인 리로드·오류 창·종료 경계도 검증 대상이다. 현재 사용자 데스크톱에 미검증 프로세스를 띄우는 시험은 금지한다.
- **2026-09-26 검증:** 비대화형 station/desktop 자식의 실제 창 생성·종료, Unity 컴파일·원장 core 회귀, 공식 전체 월드 81-section 저장 왕복·실제 자동저장이 통과했다. 최신 `u3-isolated-campaign-r7`은 실제 GameplayScene 훈련 4회·레벨 1→5→공식 world 복원→fresh owner/pending→동일 요청 wire 재생까지 PASS했다. 통합 `u3-exact-json-integration-r1`은 142/142 PASS다. 첫 통제 production interaction의 요청 증거이며 전체 장기 학습 파이프라인이나 오류 창 등 모든 UI 경계까지 전수 검증했다는 뜻은 아니다. 실행별 소스 해시·증거와 실패 이력은 `progress.md`를 따른다.
- 진입점은 아래 `run_isolated_unity_batch.ps1`이다. 호출마다 현재 런처를 새 `.launcher` 폴더에 컴파일하고 순수 검사·소스/바이너리 해시를 남긴다. 숨김 실행만 하던 `run_hidden_unity_batch.ps1`은 명시 오류로 차단되어 있다. 격리 생성/프로브 실패 시 Unity는 시작하지 않으며 대화형 실행으로 돌아가지 않는다.
- `-SerializedStartup`은 전용 T01/T02 edit, 추가 selector 없는 crop/research/B129/B130/B131/B132 edit 검사, 명시 `-U0E09EnvironmentExposure` play 또는 추가 selector 없는 `ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch`·`GameWorldLineageDebugScenarios.RequestActualContainerForBatch` play에서만 사용할 수 있다. 정확한 edit entry는 crop 두 observer entry, ResearchWorkObserverDebugScenarios.RunFromBatch, RemainingPresentationObserverDebugScenarios.RunForBatch, MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch, ObserverCompositionClosureDebugScenarios.RunForBatch, PilotInputExperienceProfileExporter.RunForBatch이다. B132 edit는 기존100개의 source-bound 입력을 현행 경험 policy로 읽기 전용 재평가하며 Play/게임 저장/응답 생성을 수행하지 않는다. B131 전용 play는 실제 컨테이너의149 module/현재 fact binding exact-set만 짧게 읽고 create-only report 후 종료한다. B140 전용 play는 실제 GameplayScene 컨테이너의 저장 계보를 검사하며 별도 selector 없이 정확한 entry만 허용한다. 해당 자식 실행에만 import를 in-process, Unity worker와 BEE 빌드를1작업으로 제한하며 다른 앱·OS설정·기본 실행은 바꾸지 않는다. 메모리 보호 기준을 유지하며 launcher-context.txt로 적용 설정을 확인한다. entry 허용·잘못된 mode/접두 entry/추가 selector 거절을 순수 검사에서 확인한다. 설정·순수 검사 PASS는 현재 소스 컴파일/실제 실행 PASS를 대신하지 않는다.

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
- 현재 급여 checkpoint는 circus V7을 사용한다. process-local `nextCareAt`은 저장하지 않고 owner가 계산한 유한 0..5초 `remainingCareSeconds`를 새 clock에 복원한다. V6 QA 파일을 수정하거나 이관하지 않으며, 실제 새 V7 급여/저장 실행에서 만든 checkpoint만 재개한다. cold 음성 검사는 원본 불변 메모리 복사본의 manifest를 별도 봉인해 실제 잘못된 housing 결합이 Stage에서 거절되고 live 상태가 보존되는지 확인한다.
- 도축 완료 신원 pending cold 시험은 `-Mode play -U0E11Capture -ExecuteMethod CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch`와 `-CampaignResumeWorld <원 actual real-capture-slaughter-identity-pending-world.json 절대경로> -CampaignResumeSha256 <원 파일 SHA-256>` 쌍만 사용한다. 일반 capture/transport와 급여 cold는 기존 guard를 유지한다. 원 checkpoint/child/사체를 그대로 공식 복원하고 정상 owner Tick·독립 완료 이벤트·중복 방지를 검사한다. 잘못된 참조 음성 입력은 원본 파일을 바꾸지 않는 manifest 봉인 메모리 복사본이며 실제 실행 전에는 PASS가 아니다.
- 긴 작업은 기존 실행 handle로 확인한다. timeout만으로 작업 종료를 가정하거나 동일 명령을 재실행하지 않는다.
- 연결이 불명확하면 프로세스·로그 등 읽기 전용 증거로 현재 상태를 대조한다.
- 세션 중단·재개 시 과거 창 허가는 만료된다. 상태가 확인될 때까지 새 편집/실행을 시작하지 않는다.
- 미저장 변경을 잃을 수 있는 Editor 종료·재시작·씬 저장을 임의로 수행하지 않는다.
- 연결 확인만을 위해 테스트, 별도 Editor 실행, 씬 저장 또는 Editor 종료를 하지 않는다.
- 타입 위치는 원본 C#/asmdef에서 찾는다. Unity 메인 스레드에서 전체 AppDomain 타입을 열거하지 않는다.

## 성공 판정

B139 공개 스킬 근거 회귀는 `-Mode edit -SerializedStartup -ExecuteMethod V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch`를 사용한다. 정확한 entry만 허용하며 기존 고정 캡처로 공개 선정/근거 매핑/JSON 복원을 검사한다. 새 게임 실행이나 모델 추론이 아니며 파생 공개 입력은 create-only 출력과 비학습 scope 표식을 갖는다.

B141 전체 복원 검사는 현재 컴파일을 마친 뒤 Roslyn 공유 컴파일 서버가 같은 격리 job에서 약3GB working set을 유지하는 경우를 확인했다. 메모리 보호로 중단됐다면 본체와 소유 보조 프로세스를 구분해 계측한다. 컴파일 성공·source pin 무변경·기존 job 종료를 확인한 다음 새 출력 디렉터리에서 같은 검사를 실행하면 이미 검증된 current-source 캐시를 사용하며 보조 서버의 잔류 비용을 분리할 수 있다. 이는 B141 r3에서 실제 확인한 절차이지 모든 메모리 실패의 자동 재시도나 근본 해결 보장이 아니다. current compile 실패/소스 변경 시 이 절차로 구 DLL을 통과 처리하지 않으며, 보호 임계값·검사 범위는 유지한다. `WorldRestoreMemory-20261011-b141-r1/verification-r3.json`을 증거로 참조한다.

B134 이름/직급 회귀는 `-Mode edit -SerializedStartup -ExecuteMethod MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch`도 허용한다. 이 정확한 entry만 단일 import/worker로 실행하며, 잘못된 mode와 유사 접두 entry는 순수 검사에서 거절한다. 실제 CharacterActor의 개인 이름 포트·모델 이름/역할 분리와 시설 observer/저장·재전달 회귀를 확인한다.

현재 소스의 관련 컴파일 결과와 어셈블리 신선도를 확인한다.
`IsCompiling=false`, 콘솔 런타임 오류 0, 서버 발견만으로 현재 소스의 컴파일 성공을 판정하지 않는다.
현재 컴파일이 실패했는데 구형 DLL로 실행된 검사는 `NOT_VALID_CURRENT_SOURCE`이며 PASS 증거가 아니다.

필요한 Refresh·컴파일은 작성자를 동결한 뒤 관련 배치당 한 번 수행한다.
기존 경고와 새 오류를 구분하고, 실행한 경로·결과·남은 실패만 보고한다.
