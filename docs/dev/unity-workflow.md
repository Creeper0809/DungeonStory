# Unity 실행 안내

이 문서는 Editor 제어·컴파일·Play 작업 때만 읽는다. [루트 지침](../../AGENTS.md)의 배타적 작업 창이 우선하며, 문서만 수정하는 작업에는 Unity를 실행하지 않는다.

## 연결 방식

프로젝트의 현행 제어 경로는 공식 Unity CLI와 `com.unity.pipeline`이다. 구형 Assistant relay `unity-mcp`는 비활성 상태로 보존한다.
이 설명은 연결 허가가 아니다. 현재 사용자가 MCP 사용을 금지한 작업에서는 그 결정을 유지하고 서버를 임의 등록·재활성화하지 않는다.

명령은 실제 주 프로젝트를 지정한다. 별도 임시 Unity 프로젝트로 검증을 대체하지 않는다.

```powershell
unity command editor_status --project-path F:/01_Programming/01_Project/02_Unity/DungeonStory --format json
unity command console --level warn --project-path F:/01_Programming/01_Project/02_Unity/DungeonStory --format json
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

- Unity 담당자 한 명만 한 번에 하나의 명령을 실행한다.
- 긴 작업은 기존 실행 handle로 확인한다. timeout만으로 작업 종료를 가정하거나 동일 명령을 재실행하지 않는다.
- 연결이 불명확하면 프로세스·로그 등 읽기 전용 증거로 현재 상태를 대조한다.
- 세션 중단·재개 시 과거 창 허가는 만료된다. 상태가 확인될 때까지 새 편집/실행을 시작하지 않는다.
- 미저장 변경을 잃을 수 있는 Editor 종료·재시작·씬 저장을 임의로 수행하지 않는다.
- 연결 확인만을 위해 테스트, 별도 Editor 실행, 씬 저장 또는 Editor 종료를 하지 않는다.
- 타입 위치는 원본 C#/asmdef에서 찾는다. Unity 메인 스레드에서 전체 AppDomain 타입을 열거하지 않는다.

## 성공 판정

현재 소스의 관련 컴파일 결과와 어셈블리 신선도를 확인한다.
`IsCompiling=false`, 콘솔 런타임 오류 0, 서버 발견만으로 현재 소스의 컴파일 성공을 판정하지 않는다.
현재 컴파일이 실패했는데 구형 DLL로 실행된 검사는 `NOT_VALID_CURRENT_SOURCE`이며 PASS 증거가 아니다.

필요한 Refresh·컴파일은 작성자를 동결한 뒤 관련 배치당 한 번 수행한다.
기존 경고와 새 오류를 구분하고, 실행한 경로·결과·남은 실패만 보고한다.
