# V25 서사 학습 준비 증거 수집

## 실행

Unity Editor가 이 저장소에서 열려 있고 Play/컴파일/import/다른 명령이 끝난 상태에서 실행한다. 기본 Python 대신 NarrativeAI 검증 의존성이 설치된 환경을 사용한다.

```powershell
F:/01_Programming/01_Project/02_Unity/DungeonStoryNarrativeAI/.venv-scenarios/Scripts/python.exe -B -X utf8 tools/Documentation/capture_v25_narrative_training_preflight.py --narrative-ai-root F:/01_Programming/01_Project/02_Unity/DungeonStoryNarrativeAI --version <새로운-버전>
```

결과는 `Artifacts/QA/NarrativeTrainingPreflight/<버전>`, `Artifacts/Exports/NarrativeMechanicCatalog/<버전>` 및 전달용 `Artifacts/Exports/NarrativeTrainingPreflight/<버전>`에 생성된다. 기존 디렉터리가 있으면 거부한다. baseline도 별도 보존한다.

- 공식 CLI의 detached job ID를 보존하고 한 번에 하나만 실행한다. 40초 대기 종료는 작업 실패/종료가 아니다. 같은 ID의 상태를 확인하고 이어 기다린다. 최대 대기 후에도 실행 중이면 중단하고 ID를 보고하며 재실행하지 않는다.
- 현재 컴파일 오류/Play/import 상태와 최신 C# 소스 대비 Editor DLL 시각을 검사한다. C#를 수정한 후에는 정상 재컴파일을 먼저 완료해야 한다.
- 실제 Unity 카탈로그/패킷의 canonical bytes와 Python 직렬화를 대조한다. 서로 다른 provider의 독립 패키지 캡처 2회를 비교한다. Python 소비부에 양성·음성 패킷을 재생한다.
- 기존 정확한 23개 focused 메서드를 실제 호출한다. 기술 선택/후보·예산/중복, 장비 실패 감사, 시설 추천 보존, 후천 특성 이정표·효과·소거/원자성·저장 검사를 포함한다. assertion 개수를 부풀려 검사 건수로 표시하지 않는다. 넓은 타입 검색이나 전체 회귀 재실행은 하지 않는다.
- 모든 명령 인수/원시 출력/종료 코드/경과 시간을 보존한다. 4개 게이트 증거 파일과 현재 inputDigest를 C# exporter의 TestEvidence에 전달한다. export 후 다시 source/dirty/catalog 일치와 독립 handoff 검증을 수행한다.
- 학습 승인을 요청하지 않는다. `humanApprovalClaimed=false`, `trainingEligible=false`. 기존 검수/평가 fixture를 새 학습 입력으로 재분류하지 않는다. 게임 규칙·수치·자산·씬 변경은 없다.

## 실제 런타임 입력 수집 경로와 이번 범위

코드에서 확인한 수집 경계는 다음과 같다.

1. `Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs`의 공개 맥락 구성과 `Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs`의 `BuildPromptEnvelope`가 실제 인물/시설 상태의 공개 사실을 구성한다. 각 프로필의 호출자는 동일 시점의 C# 합법 후보를 보유한다.
2. `Assets/Scripts/Models/AI/Core/NarrativePublicContext.cs`의 `ToModelInput`/`AppendToPrompt`가 공개 투영을 생성한다. `NarrativePublicModelInput.cs`의 `CaptureSnapshot`/`TryCaptureSnapshotFromPrompt`는 canonical 공개 사실·생애 맥락을 읽는 경계다. 공개 투영만 추출하면 **기계 후보 전체를 대체할 수 없으므로**, 해당 요청의 후보 DTO도 함께 보존해야 한다.
3. `LocalLlmRequestQueue.cs`가 profile/prompt/correlationId를 큐에 넣고 `NarrativeStructuredOutputCore.cs`가 공개 입력/요청 맥락 일치를 검증한다. 감사용 내부 context나 모델 응답을 통째로 학습 입력에 넣지 않는다.

이 진입점들은 공개 입력을 구성/검증하지만, 이번에 조사한 경로에는 자연 플레이 세션을 별도 학습 묶음으로 기록하는 검증된 family/seed/save/replay exporter가 없다. 이번 도구는 이 runtime hook을 추가하거나 Play를 시작하지 않으며 자연 플레이/저장 재생을 실행하지 않았다. 따라서 신규 자연 플레이 샘플은 **0건 / NOT_COLLECTED**다. 기본 export의 100건은 기존 controlled Editor fixture이고 수집 성공으로 세지 않는다.

다음 수집에는 별도 save/replay 또는 실제 플레이 실행을 정하고, 요청 시점의 공개 입력+전 후보 DTO+게임/dirty 해시와 save/replay 해시·tick·seed를 같은 스냅샷으로 기록해야 한다. 인물 생애/장비 계보/시설 사건의 권위 식별자로 familyId를 만들고 calibration/evaluation과 중복·변형 누수를 차단한 뒤 소규모 입력을 검수한다. 제공되지 않은 과거·이름·사건은 만들지 않는다. 한국어 교사 답안/검수와 모델 품질 게이트는 별도다.

기존 기본 모델의 의미·서사 품질 실패는 이 증거 연결만으로 해결되지 않는다. 50,000건 생성, SFT/DPO, GGUF 출시 변환과 승격은 계속 차단한다.
