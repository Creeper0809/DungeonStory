# DungeonStory 도구 안내

`tools/`에는 설계 문서 모음이 아니라 실행 가능한 검증·생성 도구와 그 계약이 함께 있다.
Markdown 문서는 아래 목록이 전부이며, 현재 계약과 운영 절차이므로 임의로 삭제하거나
`Artifacts/`로 이동하지 않는다.

## 현재 구현 계약

- [게임 결과 서사 원장](Documentation/gameplay-outcome-narrative-ledger-plan.md) — 진행 중인 Phase80의 구조·저장·검증 계약
- [서사 공식 기계 계약](Documentation/narrative-formula-mechanics-contract.md) — 기술·후천 특성·장비·시설 공식의 C# 권위
- [LLM 모듈 선택 계약](Documentation/narrative-formula-llm-module-selection-contract.md) — 모델 선택 범위와 C# 검증 경계

## 실행 안내

- [V25 서사 학습 준비 증거 수집](Documentation/V25_NARRATIVE_TRAINING_PREFLIGHT.md) — 증거 수집 도구의 실행·실패 처리 절차
- [Assembly Migration Planner](AssemblyMigrationPlanner/README.md) — 어셈블리 분리 순서 분석기
- [Player Automation](player-automation/README.md) — 개발 빌드 전용 로컬 자동화 채널

## Phase80 검증 스키마

- [최종 생산자 manifest](Verification/GameplayOutcomeLedger/FINAL_MANIFEST_SCHEMA.md)
- [최종 출시 증거](Verification/GameplayOutcomeLedger/FINAL_RELEASE_EVIDENCE_SCHEMA.md)

## 보관 규칙

- 실행 코드와 직접 결합된 계약·README는 해당 도구 옆에 둔다.
- 실행 결과와 원시 증거는 새 버전의 `Artifacts/QA/` 또는 `Artifacts/Exports/`에 둔다.
- 완료된 일회성 계획을 `tools/`에 누적하지 않는다. 과거 내용은 Git 이력에서 복구한다.
- `__pycache__/`와 `*.pyc`는 파생 캐시이며 문서나 증거가 아니다.
