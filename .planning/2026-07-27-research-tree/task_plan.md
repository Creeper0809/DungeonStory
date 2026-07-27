# Research Tree Implementation

## Goal
Replace the legacy blueprint card list with a deterministic full-screen research tree, physical blueprint archive requirements, queue semantics, save migration, and verified desktop/mobile interaction.

## Phases

### Phase 1: Baseline and contracts
- Status: complete
- Inspect current research, shop/reward, item, room, save, UI, settings, and pause APIs.
- Record compatibility constraints and current compile state.

### Phase 2: Project data, catalog, graph layout
- Status: complete
- Add `ResearchProjectSO`, IDs, blueprint rules, node states, catalog, and deterministic layered layout.
- Add 24-project asset builder and validation scenarios.

### Phase 3: Runtime queue and physical blueprints
- Status: complete
- Replace blueprint-task authority with project progress and queue state.
- Add archive ability/query, physical blueprint item mapping, blocking/suspension, and remove shop auto-queue.

### Phase 4: Full-screen Research Tree UI
- Status: complete
- Add dedicated research surface with pan/zoom/search/filter/detail/queue.
- Add valid queue insertion/reorder and responsive desktop/mobile presentation.
- Integrate close behavior and optional auto-pause.

### Phase 5: Save and content migration
- Status: complete
- Bump research save section while preserving global V16.
- Migrate legacy blueprint tasks/completions and materialize owned blueprints.
- Move unlock authority to projects and remove duplicate rewards.

### Phase 6: Verification
- Status: complete
- Compile with Console Error/Warning 0.
- Run EditMode/debug validation for graph, queue, archive, migration.
- Run PlayMode pointer interaction and physical blueprint flow.
- Capture 1600x900 and 900x1600 UI plus world camera evidence.

## Errors Encountered
- 2026-07-27: 초기 프로젝트 에셋 생성 당시 `ResearchProjectSO.cs.meta`에 `MonoImporter`가 없어 생성 에셋의 `m_Script`가 0으로 저장되고 타입 검색이 누락됐다. 메타를 복구하고 builder에 손상 에셋 재생성 검사를 추가했다.
- 2026-07-27: 첫 배치 검증에서 단일 레이어 그래프의 역방향 barycenter sweep가 인덱스 범위를 벗어났다. 레이어 수가 1 이하일 때 crossing reduction을 생략하도록 수정한다.
- 2026-07-27: 새 Research Tree 검증 MenuItem이 첫 도메인 리로드 직후 `ExecuteMenuItem`에서 아직 발견되지 않았다. 직접 정적 검증 호출과 재리로드 후 메뉴 경로를 병행 확인한다.
- 2026-07-27: 저장 섹션 경로를 리팩터링 이전 위치로 조회해 실패했다. `rg --files`로 현재 `Assets/Scripts/Services/Research` 위치를 확인한 뒤 계속 진행했다.
- 2026-07-27: `[SerializeReference]` 해금 목록을 SO 사이에서 직접 공유해 이관하자 프로젝트 에셋에는 빈 목록이 저장됐다. 지원 해금 타입을 깊은 복사하고 원본 설계도 목록을 비우도록 builder를 수정했다.
- 2026-07-27: 기존 P1 회귀는 선행 프로젝트가 자동 등록되는 새 큐에서 목표 프로젝트가 즉시 활성화된다고 가정했다. 테스트가 대상의 선행 완료를 명시하도록 수정했다.
| Error | Attempt | Resolution |
|---|---:|---|
| Parallel baseline read failed because `FacilityBlueprintSO.cs` path was assumed incorrectly | 1 | Locate the type with `rg` first, then read the discovered path. |
| Unity refresh command referenced `Unity.CompilationPipeline` instead of `UnityEditor.Compilation.CompilationPipeline` | 1 | Use `AssetDatabase.Refresh` directly and let Unity trigger compilation. |
| Dynamic Unity command assembly could not directly reference the editor-only `ResearchProjectAssetBuilder` type | 1 | Invoke the registered MenuItem through `EditorApplication.ExecuteMenuItem`. |
| Research asset MenuItem was not available immediately after source refresh | 1 | Wait for Unity domain reload/compilation, inspect Console, then invoke after the editor assembly is loaded. |
| Graph layout barycenter returned `double` from LINQ `Average()` | 1 | Cast the deterministic average to `float`. |
| Unity created 24 project assets without a script because `ResearchProjectSO` was declared in `ResearchProjectModels.cs` | 1 | Move the ScriptableObject to matching `ResearchProjectSO.cs` and rebuild only the generated research asset directory. |
| PowerShell symbol search used an improperly escaped quoted regex | 1 | Use literal `Select-String` or simpler patterns for follow-up inspection. |
| Regression verifiers called `TryGetBlueprint` on the common `FacilityShopOffer` base type | 1 | Pattern-match `FacilityBlueprintOffer` and read its `Blueprint` property. |
| New save/queue scenario used display-derived project IDs that did not match the generated stable IDs | 1 | Read generated assets and use `research:survival:medical` / `research:defense:watch`. |
| P0 verifier reported a clicked locked node as missing | 2 | Limit lookup to the visible tree and snapshot existence before the periodic refresh replaces the button object. |
| P1/P2 teardown reparented a feedback bubble while its actor was deactivating | 1 | Destroy the transient view instead of pooling it when the current parent hierarchy is deactivating. |

## Guardrails
- Preserve all unrelated dirty-worktree changes.
- Keep global save version V16.
- Never persist auto-layout positions in project assets.
- Research progress remains independent from queue membership.
- Blueprint requirements must come from physical archived items, not abstract ownership.
