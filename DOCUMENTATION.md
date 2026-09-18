# DungeonStory 문서 안내

문서가 많아도 목적별 진입점은 다섯 곳뿐이다.

| 찾는 내용 | 먼저 볼 곳 | 취급 |
|---|---|---|
| 현행 게임 설계·시스템 구조 | [`docs/README.md`](docs/README.md) | 사람이 읽는 기본 문서 |
| 현재 구현 사실 | C# 소스와 Unity 작성 자산 | 최상위 권위 |
| 런타임 계약·검증 도구 | [`tools/README.md`](tools/README.md) | 구현 계약·운영 안내·검증 스키마 |
| 생성된 지식베이스·콘텐츠 DB | [`docs_final/README.md`](docs_final/README.md) | 생성 절차로 갱신; 직접 편집 금지 |
| QA·export·실행 증거 | `Artifacts/QA/`, `Artifacts/Exports/`, `Artifacts/Review/` | 버전 고정; 덮어쓰기·합치기 금지 |

## 현재 작업에서 자주 보는 문서

- 게임 결과 서사 원장: [`tools/Documentation/gameplay-outcome-narrative-ledger-plan.md`](tools/Documentation/gameplay-outcome-narrative-ledger-plan.md)
- 서사 공식 기계 계약: [`tools/Documentation/narrative-formula-mechanics-contract.md`](tools/Documentation/narrative-formula-mechanics-contract.md)
- LLM 모듈 선택 계약: [`tools/Documentation/narrative-formula-llm-module-selection-contract.md`](tools/Documentation/narrative-formula-llm-module-selection-contract.md)
- 시스템 구현 권위: [`docs/system-implementation-checklist.md`](docs/system-implementation-checklist.md)
- 전체 밸런스 기준: [`docs/game-design/whole-game-balance-baseline.md`](docs/game-design/whole-game-balance-baseline.md)

## 기록 성격

- 루트 `task_plan.md`, `findings.md`, `progress.md`는 현재 작업 연속성을 위한 요약이다. 이전 전문은 Git 이력에서 복구한다.
- `.planning/`은 진행 중에만 사용하는 Git 비추적 로컬 임시 기록이다. 현재 구현 권위나 영구 보관소가 아니며, 작업 종료 후 삭제할 수 있다.
- `docs/generated/`, `docs/implementation-reports/`, `docs/qa/`는 특정 시점의 생성·검증 결과다.
- `wiki/`는 사이트 애플리케이션이며, `docs_final/`을 읽기 전용 입력으로 사용한다.
- 루트의 대형 audit 문서는 특정 감사의 원문이다. 현행 설계 진입점은 `docs/README.md`다.

## 새 문서를 둘 위치

- 현행 설계 설명: `docs/`의 기존 분류 아래
- 구현 계약과 검증 절차: `tools/Documentation/`
- 자동 생성 문서: 생성기가 관리하는 `docs_final/` 또는 `docs/generated/`
- 버전별 실행·검수 증거: 새 이름의 `Artifacts/.../` 디렉터리
- 세션 임시 기록: `.planning/<날짜-작업명>/` (Git 비추적·완료 후 정리 가능)

기존 증거를 보기 좋게 만들기 위해 이동하거나 이름을 바꾸지 않는다. 링크 인덱스로 탐색하고, 오래된 기록은 해당 버전 위치에 그대로 보존한다.
