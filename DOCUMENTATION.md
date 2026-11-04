# DungeonStory 문서 안내

문서가 많아도 목적별 진입점은 다섯 곳뿐이다.

| 찾는 내용 | 먼저 볼 곳 | 취급 |
|---|---|---|
| 현행 게임 설계·시스템 구조 | [`docs/README.md`](docs/README.md) | 사람이 읽는 기본 문서 |
| 현재 구현 사실 | C# 소스와 Unity 작성 자산 | 최상위 권위 |
| 런타임 계약·검증 도구 | [`tools/README.md`](tools/README.md) | 구현 계약·운영 안내·검증 스키마 |
| 생성된 지식베이스·콘텐츠 DB | [`docs_final/README.md`](docs_final/README.md) | 생성 절차로 갱신; 직접 편집 금지 |
| 고정 검사 입력 | [`required-fixtures.json`](tools/Repository/required-fixtures.json) | 기존 검사가 읽는 입력만 Git에 보관 |

## 현재 작업에서 자주 보는 문서

- 에이전트 공통 원칙: [`AGENTS.md`](AGENTS.md); 작업별 상세는 [검증 도구](docs/dev/verification.md), [Unity 실행](docs/dev/unity-workflow.md), [아키텍처 변경](docs/dev/architecture-review.md)
- 게임 결과 서사 원장: [`tools/Documentation/gameplay-outcome-narrative-ledger-plan.md`](tools/Documentation/gameplay-outcome-narrative-ledger-plan.md)
- 서사 공식 기계 계약: [`tools/Documentation/narrative-formula-mechanics-contract.md`](tools/Documentation/narrative-formula-mechanics-contract.md)
- LLM 모듈 선택 계약: [`tools/Documentation/narrative-formula-llm-module-selection-contract.md`](tools/Documentation/narrative-formula-llm-module-selection-contract.md)
- 시스템 구현 권위: [`docs/system-implementation-checklist.md`](docs/system-implementation-checklist.md)
- 전체 밸런스 기준: [`docs/game-design/whole-game-balance-baseline.md`](docs/game-design/whole-game-balance-baseline.md)

## 기록 성격

- 루트 `HANDOFF.md`, `task_plan.md`, `findings.md`, `progress.md`는 현재 작업 연속성을 위한 로컬 기록이며 Git에서 제외한다. 이전 전문은 Git 이력에서 복구한다.
- `.planning/`은 진행 중에만 사용하는 Git 비추적 로컬 임시 기록이다. 현재 구현 권위나 영구 보관소가 아니며, 작업 종료 후 삭제할 수 있다.
- `docs/generated/`, `docs/implementation-reports/`, `docs/qa/`는 특정 시점의 생성·검증 결과다.
- `wiki/`는 사이트 애플리케이션이며, `docs_final/`을 읽기 전용 입력으로 사용한다.
- 루트의 대형 audit 문서는 특정 감사의 원문이다. 현행 설계 진입점은 `docs/README.md`다.

## 새 문서를 둘 위치

- 현행 설계 설명: `docs/`의 기존 분류 아래
- 개발 워크플로·도구 사용법: `docs/dev/` (해당 작업에 필요한 문서만 열람)
- 구현 계약과 검증 절차: `tools/Documentation/`
- 자동 생성 문서: 생성기가 관리하는 `docs_final/` 또는 `docs/generated/`
- 로컬 실행과 검수 결과: Git에서 제외되는 `Artifacts/.../` 디렉터리
- 세션 임시 기록: `.planning/<날짜-작업명>/` (Git 비추적·완료 후 정리 가능)

고정 검사 입력의 바이트와 기존 검사 기준은 유지한다. 일반 로그와 내보내기를 공유하려고 전체 `Artifacts/`를 추가하지 않는다. 과거 감사 문서가 참조하는 실행 원문은 해당 문서 작성 커밋에서 확인한다.
