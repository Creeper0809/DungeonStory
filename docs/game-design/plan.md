# Behavior Designer 적극 커스터마이징 + Utility AI + Local LLM 설계

## Summary
- Behavior Designer를 우리 게임 전용 BT 런타임 기반으로 적극 수정한다.
- 큰 의사결정은 Behavior Designer BT, 세부 시설/작업 선택은 기존 Utility AI가 맡는다.
- LLM은 행동 실행자가 아니라 **페르소나, 장기 목표, 가중치, 말풍선** 생성기로 사용한다.
- 로컬 LLM은 개발 단계에서 Ollama/LM Studio localhost API, 출시 단계에서 llama.cpp/GGUF Unity 래퍼를 목표로 한다.
- LLM JSON 파싱, 큐 병목, 디렉터 컨텍스트 압축을 필수 설계로 포함한다.

## Key Changes
- **Behavior Designer Vendor 적극 수정**
  - `Assets/Behavior Designer` 런타임 코드를 직접 수정한다.
  - 목표:
    - `CharacterAiScheduler`가 Behavior Tree tick을 직접 예산 제어
    - 자동 Update tick 대신 수동 staggered tick 지원
    - 현재 branch/task/status를 UI에서 읽을 수 있는 accessor 추가
    - `CharacterBlackboard`, macro goal, utility action 상태를 BT 변수와 연결
  - 수정 지점에는 `DungeonStory patch` 주석을 남긴다.

- **BT 구조**
  - 캐릭터 프리팹에 Behavior Designer `BehaviorTree`를 붙인다.
  - 최상위 branch:
    - `Critical`: 사망, 퇴장, 전투, 분노, 구조
    - `MacroGoal`: LLM/규칙 기반 장기 목표
    - `UtilityDecision`: 기존 `AIBrain`/`AIActionSet` 실행
    - `Idle`: 배회, 대기, 말풍선
  - 커스텀 Task:
    - `HasCriticalState`
    - `HasMacroGoal`
    - `RunMacroGoal`
    - `RunUtilityDecision`
    - `RunIdleBehavior`
    - `EmitContextBubble`

- **Blackboard / 결정 관성**
  - `CharacterBlackboard` 컴포넌트 추가.
  - 저장 정보:
    - currentIntent
    - committedAction
    - committedDestination
    - minCommitUntil
    - failedBuildingCooldowns
    - recentFailureCounts
    - activeMacroGoal
  - `FacilityCandidateScorer`는 실패 쿨다운 시설을 후보에서 제외한다.
  - 이동 중인 action+destination은 최소 유지 시간 동안 보너스를 받는다.
  - Critical, 목적지 파괴, 경로 상실, 시설 이용 불가, patience 초과, macroGoal 전환만 commitment를 깰 수 있다.

- **LLM Persona**
  - `CustomerPersonaRuntime` 추가.
  - 생성 필드:
    - traitName
    - flavorText
    - selfCareMultiplier
    - curiosityMultiplier
    - shoppingMultiplier
    - patienceMultiplier
    - hungerCurveMultiplier
    - funCurveMultiplier
    - moodCurveMultiplier
    - preferredFacilityTags
  - 기존 `CharacterAiPersonality`와 `CharacterStats` 계산에 runtime persona 값을 곱한다.

- **LLM JSON 파싱**
  - `LlmJsonResponseParser` 추가.
  - 처리 순서:
    - 응답 앞뒤 마크다운 fence 제거
    - 첫 번째 `{`부터 마지막 `}`까지만 추출
    - trailing comma 제거
    - `JsonUtility.FromJson` 또는 명시 DTO 파서 호출
    - 필수 필드/범위 검증
  - json mode 또는 structured output을 지원하는 로컬 서버라면 요청 옵션에 활성화한다.
  - 파싱 실패는 숨기지 않고 로그/디버그 UI에 표시한다.

- **LLM 비동기 큐**
  - `LocalLlmRequestQueue` 추가.
  - 요청 타입:
    - `Persona`: 높음
    - `MacroGoal`: 중간
    - `BubbleLine`: 낮음
  - 제한:
    - `MaxQueueSize`
    - 요청별 timeout
    - 동시 실행 수 1~2개
  - Drop 정책:
    - 큐가 가득 차면 낮은 우선순위 말풍선부터 폐기
    - 오래 대기한 말풍선은 폐기
    - persona와 macro goal은 기본적으로 폐기하지 않고 timeout 오류로 표시
  - 말풍선 요청이 drop되면 원본 로그 문장을 사용한다.

- **LLM Macro Director**
  - `AiDirectorRuntime` 추가.
  - 호출 대상:
    - 반복 실패 고객
    - 불만/분노 임계치 도달
    - 네임드 고객
    - 하루/반나절 샘플링 대상
  - 출력 macro goal:
    - `Continue`
    - `SeekFood`
    - `SeekFun`
    - `AvoidFacility`
    - `Complain`
    - `ExitDungeon`
    - `Vandalize`

- **디렉터 컨텍스트 압축**
  - `AiDirectorContextAggregator` 추가.
  - LLM에 원본 NPC/시설 목록을 그대로 넘기지 않는다.
  - 집계 데이터 예:
    - 전체 손님 수
    - 평균 불만도
    - 평균 대기 시간
    - 대기열 상위 시설 3개
    - 재고 부족 시설 수
    - 경로 실패 다발 구역
    - 최근 반복 실패 상위 원인
    - 현재 대상 캐릭터의 최근 사건 5개
  - 캐릭터 개별 요청에는 “대상 캐릭터 + 요약된 던전 상태”만 전달한다.

- **로컬 LLM 런타임**
  - 공통 인터페이스:
    - `GeneratePersonaAsync`
    - `GenerateMacroGoalAsync`
    - `GenerateBubbleLineAsync`
  - 개발 단계:
    - Ollama/LM Studio OpenAI-compatible localhost REST API 사용.
  - 출시 단계:
    - llama.cpp 기반 Unity 래퍼 구현체 사용.
    - GGUF 모델은 `StreamingAssets` 또는 Addressables로 배포.
  - LLM 실패는 명시적으로 드러내고, 게임 진행은 기존 AI로 계속된다.

- **말풍선 / 로그**
  - `CharacterDialogueRuntime` 추가.
  - `CharacterLog` 이벤트를 받아 LLM 대사 요청 여부를 결정한다.
  - 대상 이벤트:
    - 재고 없음
    - 계산 대기 과다
    - 길 막힘
    - 돈 부족
    - 반복 실패
    - 분노
    - 퇴장
  - LLM 대사는 짧은 한 줄로 제한한다.

## Test Plan
- **Behavior Designer**
  - 자동 Update tick을 끄고 `CharacterAiScheduler` 수동 tick으로 동작하는지 확인.
  - 500 NPC에서 BT tick이 프레임별로 분산되는지 확인.
  - 현재 실행 branch/task/status가 UI에 표시되는지 확인.

- **BT + Utility**
  - Critical branch가 Utility보다 우선.
  - MacroGoal이 있으면 해당 branch 선택.
  - MacroGoal이 없으면 기존 Utility 행동 유지.
  - Utility 목적지 예약/해제가 정상 유지.

- **Blackboard**
  - 실패 시설 쿨다운 동안 재선택하지 않음.
  - 쿨다운 만료 후 후보 복귀.
  - commitment가 근소한 점수 역전을 막음.
  - 상위 interrupt는 commitment를 깨고 전환.

- **LLM JSON**
  - 정상 JSON 파싱.
  - markdown fence 포함 응답 파싱.
  - 앞뒤 설명문 포함 응답 파싱.
  - trailing comma 응답 보정.
  - 필수 필드 누락/범위 초과 응답 거부.

- **LLM Queue**
  - 큐 크기 초과 시 낮은 우선순위 말풍선 drop.
  - timeout된 말풍선은 원본 로그 사용.
  - persona/macro goal timeout은 오류 표시.
  - 큐 지연 중에도 기존 AI가 멈추지 않음.

- **Director Context**
  - 100 NPC/50 시설 원본 대신 집계 요약만 생성.
  - 대기열/불만/재고 부족 상위 정보가 포함됨.
  - prompt 크기가 설정한 최대 길이를 넘지 않음.

- **성능**
  - 100/300/500 NPC 스트레스 테스트.
  - LLM 큐 지연 중에도 기존 AI가 계속 동작.
  - BT + Utility + Blackboard 적용 후 프로파일링.

## Assumptions
- Behavior Designer vendor 코드는 적극적으로 수정한다.
- 수정 목적은 우리 게임 전용 BT 런타임화, 수동 tick, 디버그 노출, blackboard 연동이다.
- LLM은 행동 직접 실행자가 아니라 가중치/목표/대사 생성기다.
- 로컬 LLM 개발 기본값은 Ollama/LM Studio localhost API다.
- 출시 기본 방향은 llama.cpp Unity 래퍼 + GGUF 모델 번들이다.
- JSON 파싱 보정은 허용하지만, 의미를 추측해서 채우는 숨은 fallback은 만들지 않는다.
