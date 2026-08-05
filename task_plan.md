# DungeonStory Active Plan

## Phase 118 - 기능 중심 최종 마감 계획 (현재 권위 계획)

이 단계는 아래의 두 가지 기계적 목표를 폐기한다.

- `Assembly-CSharp`의 게임플레이 파일 수를 0으로 만드는 전면 asmdef 이동
- 현재 정상 동작하는 V18 저장 구조를 다시 분해하거나 재설계하는 작업

저장과 어셈블리는 더 이상 별도 구현 작업이 아니다. 기존 구조를 동결하고,
현재 기능에서 실제 결함이 발견될 때만 해당 결함 범위 안에서 수정한다.

### 남은 필수 작업

1. **실제 권위 결함 마감** — 완료
   - 중복 상태 소유, 도메인 우회 쓰기, asmdef 순환 후보를 0으로 유지한다.
   - mutable static, 거대 타입, 과도한 생성자, 런타임 콘텐츠 합성,
     세션 직접 변경 하드 게이트를 0으로 유지한다.
2. **핵심 게임 기능 회귀 검증** — 구현 완료, Unity 적재 검증 대기
   - 장비 계보 이전: 원본/인장 소비, 새 장비 속성 유지, 역사 이전.
   - 원정 사망: 장비와 장착 부품의 동시 유실.
   - 화약 무기: 탄약, 재장전, 연기, 오발, 관통 및 활/석궁과의 역할 분리.
   - 168개 연구, 분기형 생산망, 시설 연료/사료, 장비 잠금과 개량 부품.
3. **통합 경계 확인** — 구현 작업이 아닌 회귀 게이트
   - 현재 소스로 Unity 컴파일 오류/경고 0.
   - V18과 54개 저장 섹션의 전체 월드 왕복이 기존 상태를 정확히 복원.
   - 실패 복원이 라이브 월드를 부분 변경하지 않음.
4. **사용자 화면 검증**
   - Unity MCP와 Unity EventSystem만 사용한다.
   - `1600x900`, `900x1600`에서 연구/생산/장비/원정 관련 포인터 흐름과
     캡처를 확인한다.
   - 사용자에게 실제 노출되는 깨진 문자열, 누락 키, 포맷 불일치만 수정한다.
5. **최종 승인**
   - 동기 기능 회귀 33단계 PASS.
   - PlayMode 수용성 매트릭스 PASS.
   - 최종 Unity Console Error 0 / Warning 0.

### 명시적 보류 작업

- 단지 파일 수를 줄이기 위한 asmdef 이동과 인터페이스 재배치.
- 현재 실패 증거가 없는 저장 DTO/섹션/버전의 추가 리팩터링.
- 화면에 노출되지 않는 의도된 한국어 콘텐츠·파서 토큰의 일괄 치환.
- 숫자 감소만을 목적으로 하는 Provider/인터페이스/어댑터 정리.

### 수정 후 완료 기준

완료 여부는 구조 변경량이나 이동한 파일 수로 판단하지 않는다. 현재 소스가
실제 Unity에서 컴파일되고, 핵심 게임 회귀와 두 해상도 UI 흐름이 통과하며,
Console이 Error 0 / Warning 0인지를 기준으로 판단한다.

---

### Phase 117 - Risk-based domain boundary closure (authoritative current plan)

> This phase supersedes every older requirement that mechanically demanded
> `Assembly-CSharp` runtime ownership reach zero. File count remains an
> informational trend only; moving an approved Unity-edge adapter solely to
> reduce that number is explicitly out of scope.

#### 2026-08-04 실행 범위 축소 확정

- `Assembly-CSharp` 잔존 파일 수와 `UnapprovedDefaultDomainAuthorityCount`는
  완료 게이트가 아니라 감사 지표로만 기록한다. 수치를 줄이기 위한 전면 이동은
  중단한다.
- 어셈블리 분리는 다음 실제 결함을 제거할 때만 수행한다.
  - 동일 상태를 둘 이상의 런타임이 쓰는 이중 권위
  - 다른 도메인의 가변 상태를 포트 없이 직접 변경하는 경계
  - 저장·복원 원자성, 결정론, 콘텐츠 단일 원본을 깨뜨리는 의존성
  - asmdef 순환 또는 테스트/컴파일을 막는 역참조
- 이미 통과한 V18 저장 구조는 동결한다. 현재 회귀에서 구체적 결함이 재현되지
  않는 한 저장 섹션 재분해·DTO 재설계·버전 변경을 하지 않는다.
- 현지화는 사용자에게 실제 노출되는 화면, 오류, 깨진 인코딩과 placeholder
  불일치만 닫는다. LLM parser 토큰이나 의도된 한국어 콘텐츠를 숫자 감소만을
  위해 옮기지 않는다.
- 최종 완료 기준은 현재 소스의 Unity 컴파일, 핵심 게임플레이 회귀, 168 연구와
  생산/장비 회귀, 두 해상도 UI 캡처, Console Error 0 / Warning 0이다.

#### 남은 실행 순서

1. **진행 중 경계만 마감한다.** Blueprint Research처럼 실제 교차 도메인 후보인
   작업과 DomainFailure 296키의 fallback/placeholder 결함을 완료한다.
2. **잔여 후보를 위험도로 판정한다.** 최신 cross-domain 후보 각각을 조사해
   이중 권위·직접 상태 변경·순환이 재현되는 것만 수정한다. 단순 Unity adapter,
   save adapter, composition wiring은 근거를 기록하고 그대로 둔다.
3. **기능 회귀를 우선한다.** 아이템/SO 단일 원본, 물리 재고·장비 인스턴스,
   생산 분기·시설 공급, 연구 168개, 장비 잠금·부품·계보를 현재 소스에서 검증한다.
4. **사용자 노출면을 닫는다.** String Table builder를 Unity에서 실행하고 실제
   화면의 누락 키, 깨진 한글, format 인자 불일치만 수정한다.
5. **Unity MCP로 최종 인수한다.** `1600x900`과 `900x1600` 포인터 흐름 및
   캡처, 전체 회귀, V18 validator, 54개 저장 섹션, Console 0/0을 한 번에 증명한다.

#### 명시적으로 제외하는 작업

- 모든 기본 어셈블리 파일을 named asmdef로 옮기는 작업
- 분리 효과 없이 파일 수·인터페이스 수·라인 수만 줄이는 기계적 리팩터링
- 정상 통과 중인 V18 저장 섹션을 다시 묶거나 재작성하는 작업
- 사용자에게 노출되지 않는 의도된 한국어 데이터와 LLM 계약의 전면 현지화

#### Revised completion contract

- [x] Freeze the completed V18 save architecture. Do not start another save
  refactor unless a current round-trip, atomicity, or compatibility regression
  proves a concrete defect.
- [x] Add a source-syntax ownership classifier and reviewed manifest for every
  runtime file that remains in default `Assembly-CSharp`.
  - `NamedRequired`: mutable domain state/Aggregates, state stores, pure rules
    and calculators, content/SO definition authority, persistent contracts,
    domain command/query policy, deterministic reward/research/production logic.
  - `DefaultAllowed`: scene-bound `MonoBehaviour` adapters, Unity input/camera/
    audio/VFX bridges, prefab/view wiring, Presentation-only Views, and the
    Composition Root that assembles named implementations.
  - `ReviewRequired`: any file that mixes an allowed Unity edge with gameplay
    state or rules. It must be split; it cannot be approved by an explanation
    alone.
- [x] Treat `UnapprovedDefaultDomainAuthorityCount` and `defaultAssemblyFiles`
  as review metrics only. Neither count has a zero target; only concrete
  authority conflicts and unsafe cross-domain mutations are blockers.
- [x] Reduce cross-domain cyclic-boundary violations to `0` in the current
  source audit:
  - asmdef cycles remain `0`;
  - no cyclic source SCC may contain a `NamedRequired` owner;
  - no remaining default-edge SCC may directly bypass the approved command,
    query, capability, or DTO boundary between gameplay domains.
- [x] Keep mutable statics, oversized types, large constructors, runtime
  content synthesis/escapes, and direct session mutations at `0`.
- [ ] Finish localization by user-visible vertical surface rather than by blind
  literal replacement: visible mojibake `0`, visible missing keys `0`, and
  visible placeholder mismatches `0`. Intentional parser/content Korean is not
  a completion metric.
- [ ] Run the fresh Unity integration boundary: compile diagnostics `0`, V18
  validator, all 54 save sections, gameplay/domain regression matrix,
  `1600x900` and `900x1600` pointer captures, Console Error `0` / Warning `0`.

#### Execution batches

1. **Stabilize the current source batch.** Finish the in-flight Production UI,
   Defense localization, Invasion policy, and Offense reward-policy cuts;
   regenerate the semantic graph and compile on fresh Unity assemblies.
2. **Install the risk classifier.** Replace the `defaultAssemblyFiles == 0`
   completion gate with the reviewed role manifest plus
   `UnapprovedDefaultDomainAuthorityCount` and cross-domain-cycle gates.
3. **Cut only proven high-risk owners.** Review the remaining cross-domain
   candidates and change a file only when a duplicate writer, unsafe direct
   mutation, deterministic/save defect, or cyclic dependency is demonstrated.
   `NamedRequired` and `ReviewRequired` classifications alone do not authorize
   a migration; approved Unity-edge adapters stay in place.
   - Character progression boundary: the experience curve and deterministic
     add/minimum/restore transitions now belong to named Characters. The
     public scene component and save snapshot remain at the existing edge;
     Foundation/Operation notifications and deterministic random-stream
     allocation are isolated behind explicit application adapters. A fresh
     analyzer now sees only the Characters domain on the state owner, so the
     target is absent from the cross-domain candidate set without an override.
   - Environmental field boundary: the array Aggregate, root-store access,
     diffusion/exterior exchange, source transitions, buffer swaps, and version
     touch now belong to named Environment. The preserved default source/GUID
     is a Grid/building/power/clock application adapter and is `DefaultAllowed`.
     Randomized legacy-equivalence coverage passes for 240 grid scenarios.
   - External influence boundary: named CoreSession now owns the Aggregate
     store plus reputation/dread/rumor/scouting, daily ecology pressure, raid,
     intel-payment, and invasion-defense transitions. The preserved default
     source/GUID is a Content/clock/economy/item/wildlife/event application
     adapter and is absent from the fresh cross-domain candidate set.
   - Composition registration boundary: a default static `*Registration` is
     allowed only under `Infrastructure/Registration` when every member is a
     stateless `void Register*` method rooted at `IContainerBuilder` and its
     body contains registration/exposure wiring only. World-simulation wiring
     satisfies this shape; mutable or calculating lookalikes remain review.
4. **Close localization vertically.** Production UI -> Defense UI/failures ->
   Character narrative/templates -> remaining UI/domain clusters. Generate
   String Tables through Unity Editor builders; do not hand-author YAML.
5. **Final acceptance.** Re-run the complete current-source regression and
   capture matrix through Unity MCP only. Save remains a regression gate, not
   an implementation workstream.

#### Scope reduction and scheduling rule

- Current default runtime ownership is approximately `811` files. This plan
  does **not** estimate completion from `811 -> 0`; the classifier audit will
  produce the exact `NamedRequired` residual set.
- Expected migration reduction is substantial (working estimate `70-85%` of
  the former assembly-move workload), but the estimate is not an acceptance
  criterion. Only the reviewed violation sets determine completion.
- An approved adapter must not be moved merely because it is easy. Every move
  must remove a domain-authority violation, a cross-domain cycle, or a proven
  hidden dependency.

### Phase 116 - Default-assembly giant-SCC decomposition

- [x] Remove the independent RunFlow, Offense save, Invasion save, Medical supply, Husbandry, Circus, Staff UI, CharacterCombatCommand, Fluid, CharacterSurgery, Captivity restore, Grid construction, DefenseEngagement, ResearchTree, and CharacterSummary cyclic boundaries without named-to-default backreferences.
- [x] Reduce the semantic planner from 18 cyclic SCCs to the single remaining default-assembly giant SCC while preserving Unity script GUIDs and the V18 restore ordering contracts.
- [x] Keep the architecture ratchets at mutable statics 0, oversized types 0, large constructors 0, content escapes 0, and direct session mutations 0; reduce raw Korean literals below the prior 6,462 checkpoint.
- [x] Complete the BuildableObject, InvasionIntruder, Offense world-map, CharacterActor visitor, Shop customer, Environment bridge, Survival bridge, and WorkTargetCandidate bounded cuts inside the giant SCC.
- [ ] Reconnect the project-scoped Unity MCP after the domain-reload relay disconnect, refresh Unity, regenerate the Bee-bound graph, and rerun the complete V18/domain regression matrix on fresh DLLs.
- [x] Retire the mechanical zero-default-assembly target in favor of the
  risk-based ownership and cyclic-boundary gates in Phase 117.
  - Current execution mode: use cohesive 2-10 source clusters after the safe one-file leaves were exhausted by concrete `CharacterSO`, `BuildableObject`, `WarehouseInventory`, and Offense enum boundaries.
  - Historical checkpoint: `815` default-assembly files and one cyclic SCC of `470` files; this count is no longer the completion oracle.
- [ ] Finish localization and the required `1600x900` / `900x1600` Unity MCP pointer-and-capture evidence with Console Error 0 / Warning 0.

### Phase 115 - Fresh V18 integration and cyclic-SCC reduction

- [x] Run the merged V18 authority, all 54 save sections, Batch B/C, physical-item, persistent-ID, and Offense aggregate/world-map/journey regressions on fresh Unity assemblies.
- [x] Run Blueprint Research, Research Tree, 168-node research/equipment, branched production, Facility Evolution, Survival, Combat, strict combat-save, material-equipment, and Captivity/Circus regressions together.
- [x] Correct the material-equipment Editor fixture to inject the required facility-evolution state instead of weakening `BuildableObject` initialization.
- [x] Move the Operations presentation boundary and WildlifeCapture restore validation policy into named assemblies while preserving Unity script GUIDs.
- [x] Generalize the controlled-stat dictionary through Foundation and restore the hard size gates to mutable statics 0 / oversized types 0 / large constructors 0.
- [x] Complete the Invasion, Captivity/Circus, medical, production, and presentation SCC cuts; their current continuation is tracked by Phase 116.
- [x] Retire the zero-file migration target; Phase 117 now owns the remaining
  risk-classified domain boundaries. Localization and two-resolution Unity MCP
  proof remain final gates.

### Phase 114 - Leaf named-assembly migration checkpoint

- [x] Finish the assigned strict-save six-section source checkpoint and record the unavailable local SDK build boundary.
- [x] Run the semantic AssemblyMigrationPlanner in clean/project-scan mode and exclude active Offense, modular/character world save, work-order, service-room, and combat-save ownership.
- [x] Select the smallest safe leaf SCC/file batch, capped at 15 source files, and map it to existing domain asmdefs without an Assembly-CSharp dependency.
- [x] Move sources with original `.meta` GUIDs, add `MovedFrom` only where serialized type identity requires it, and repair port/asmdef boundaries.
- [x] Confirm that no architecture-validator source path targets this leaf and finish with planner/source/diff checkpoints; Unity compilation remains a root-agent responsibility.

### Phase 113 - Completion-audit corrections and final closure

- [x] Reject the stale-DLL clean result, recover the truncated Operating Day source, and establish a fresh Unity compile with source-newer counts `0/0`.
- [x] Re-run the 168-node research/equipment, branched production, Facility Evolution, and Survival focused regressions on the fresh assemblies.
- [x] Remove top-level Offense direct-restore bypasses and the category-to-representative-item runtime authority.
- [x] Finish strict detached-candidate conversion for every remaining one-parameter `DungeonJsonSaveSection<T>`; source usage is 0 and the 54-section late-failure regression passes.
- [x] Remove the remaining public Offense subsystem restore bypasses and merge the legacy campaign world map into the strategic Offense aggregate.
- [x] Rebind all embedded GameplayScene MonoScripts, remove the leaked scenario character, and save the scene through Unity MCP.
- [x] Retire the mechanical zero-file goal; Phase 117 requires zero
  unapproved domain authorities and zero cross-domain cyclic-boundary
  violations instead.
- [x] Run the complete V18, physical item, production, combat, Offense, and architecture regressions on fresh DLLs.
- [ ] Verify the required `1600x900` and `900x1600` pointer workflows and captures through Unity MCP only.
- [ ] Complete a requirement-by-requirement source/asset/runtime audit and finish with Console Error `0` / Warning `0`.

> Audit correction: older phases marked generic save staging or full authority closure as complete based on marker/source checks. The 2026-08-04 audit found 32 one-parameter generic save sections whose fallible candidate construction can still occur during commit. Phase 113 is the authoritative reopening until those concrete boundaries are converted and verified.

### Phase 107 — Unity composition and clean-run recovery

- [x] Break the exterior-zone query cycle by projecting physical zone markers through a read-only `IExteriorZoneQuery` owner.
- [x] Replace wildlife-carcass → deprivation direct mutation with a scoped taboo incident event.
- [x] Make facility-evolution modifier evaluation a pure component-state query instead of re-entering the evolution command runtime.
- [x] Assign mandatory `CharacterId` values before character presentation/runtime bridges query persisted character state.
- [x] Repair stale `InvasionDirectorRuntime` scene script references without discarding serialized invasion state.
- [x] Reassign eight colliding production numeric IDs and make duplicate catalog IDs fail composition; add editor validation coverage.
- [x] Remove temporary VContainer diagnostics and dependency probes after the complete composition probe passed.
- [x] Planning entry retired; remaining full import, Run Flow, and Console proof is tracked only by Phase 112 final verification batches.

### Phase 106 — Detached save staging cutover

- [x] Add an explicit immutable `IDungeonSaveRestoreStage` contract and split registry restore into preflight, prepare-all, and commit phases.
- [x] Guarantee that a staging failure leaves every live section untouched; retain rollback only as a transitional guard for legacy commit implementations.
- [x] Connect physical items to their existing detached `WorldItemRestoreState` and commit only after every section has prepared successfully.
- [x] Move all generic JSON sections, the offense Aggregate, and all seven combat sections to prepare payloads before live mutation.
- [x] Add and pass a focused staging-failure regression; update the stale combat DI fixture exposed by the full combat regression.
- [x] Convert all 54 public SaveSection implementations to mandatory detached preparation, including optional missing-data stages.
- [x] Remove the transitional legacy-section adapter and add a reflection gate that rejects any new direct-restore section.
- [x] Remove mutable scene-transition statics and resolve the scoped navigator in diagnostics instead of constructing fallback clock/time-scale services.
- [x] Reconfirm the runtime and Editor assemblies compile cleanly; runtime scans report zero optional interface parameters, fallback infrastructure construction, runtime SO synthesis, item-definition fallback, and late runtime binds.
- [x] Cut production bills + stock sensors and combat loadouts + craft/history orders over to shared replaceable Aggregate state stores; remove combat's second write to physical equipment state.
- [x] Convert the independent economy, species, staff-discontent, deprivation, ledger, and debug restore collections to build-then-swap roots.
- [x] Convert faction, experience pacing, meta progression, defense facilities, and the combined exposure/workwear environment Aggregate to detached state replacement.
- [x] Delay physical-item markers, warehouse normalization, faction sites, husbandry reconciliation, service-hub subscriptions, run-flow effects, and captivity projections until the shared Aggregate root is actually published.
- [x] Move `GameSessionState` ownership out of `GameManager` into a scoped store and route modular-world session restoration through its explicit restore API.
- [x] Add a composition-wide `IDungeonRestoreTransactionParticipant` lifecycle so inactive Unity candidates can begin, publish, and discard with the same save transaction as DTO Aggregate roots.
- [x] Build modular facilities on an occupant-free Grid with inactive GameObjects, restored modules, persistent IDs, and no world/contract registration before replacing the live facility Grid.
- [x] Create owner and staff restore candidates under an inactive hierarchy; suppress lifetime/world/AI/presentation/Grid-event registration until their full state is applied and explicitly published.
- [x] Planning entry retired; remaining candidate-world indexing and publication work is tracked only by Phase 112 Batch E.
- [x] Planning entry retired; remaining Aggregate-root and rollback-image work is tracked only by Phase 112 save batches and Batch E.

### Phase 104 — Root-SO gameplay catalog cutover

- [x] Add authored meta-upgrade, run-variable, owner-doctrine, and invasion-pattern records to `GameDomainContentCatalogSO`.
- [x] Migrate the exact 9/14/3/6 live definitions and effect parameters into `GameDomainContentCatalog.asset`.
- [x] Replace four mutable static dictionaries with one injected immutable `AuthoredGameplayCatalog` projection.
- [x] Remove production `Register`, `ResetToBuiltIns`, runtime-reset hooks, code fallback construction, and all production references to the four legacy catalogs.
- [x] Make meta progression state and run-variable state retain their required catalog authority explicitly.
- [x] Add V18 validation for authored counts, required IDs, projection construction, and forbidden legacy catalog classes.
- [x] Freeze character-stat, work-type, and facility-role enum/bit mappings as immutable protocol tables; remove their global registration/reset APIs.
- [x] Move the remaining character-need, stock-category, and building-category balance/display records into authored SO content.
- [x] Import and execute the new catalog projection through Unity MCP.

### Phase 102 — Authored presentation and building archetype authority

- [x] Remove every runtime `ScriptableObject.CreateInstance` path; authored water/filth tiles now come from `GameContentCatalogSO`.
- [x] Replace the `GridTexture` runtime Tile wrapper with rebuildable `SpriteRenderer` presentation objects.
- [x] Remove `BuildingSO.type` and `AddComponent(Type)` from runtime construction.
- [x] Migrate all 343 `BuildingSO` assets from Odin `System.RuntimeType` nodes to one of eight fixed runtime archetypes.
- [x] Remove implicit `ItemDefinitionId -> string` conversion and reject modular-facility V1/V2 migration inside the V18 generation.
- [x] Load wildlife SOs through the root domain-content catalog instead of `Resources.LoadAll` and code fallback insertion.
- [x] Add V18 regression gates for authored world tiles, runtime SO synthesis, building archetypes, and legacy Type nodes.
- [x] Planning entry retired; final asset import/meta/graph proof is tracked only by Phase 112 Batch M/N.

### Phase 103 — Remaining authority gaps

- [x] Authored need/work/facility-role/stock/building authority completed in Phase 104; final asset closure is tracked by Phase 112 Batch M.
- [x] Planning entry retired; remaining atomic world-swap work is tracked only by Phase 112 Batch E.
- [x] Planning entry retired; remaining default-assembly migration is tracked only by Phase 112 Batches G/H.
- [x] Planning entry retired; final regressions and captures are tracked only by Phase 112 Batch N.

### Phase 101 — Policy-free runtime provider removal

- [x] Replace facility, progression, run-variable, offense, and invasion forwarding providers with scoped domain runtime registries.
- [x] Make required runtime absence a composition failure instead of an empty/default save or gameplay result.
- [x] Keep `ILocalLlmRuntimeProvider` as the sole provider boundary because it has two environment-specific implementations.
- [x] Update runtime and Editor fixtures; auxiliary Roslyn compilation passes with Error 0 / Warning 0.
- [x] Planning entry retired; final provider/import regression is tracked only by Phase 112 Batch N.

## V18 Runtime Authority Normalization (Active)

| Phase | Scope | Status |
|---|---|---|
| 82 | Freeze V18 incompatibility boundary and authority baseline | Completed |
| 83 | Establish `GameContentCatalogSO` root and strict domain projections | Completed |
| 84 | Introduce typed persistent IDs and remove persistence fallbacks | Completed |
| 85 | Make physical item repository authoritative for stock and equipment | Completed |
| 86 | Move mutable `GameData` and static run state into scoped services | Completed |
| 87 | Consolidate offense state into one aggregate/save section | Completed |
| 88 | Stage and preflight restore before live-world commit | In Progress — prepare-all pipeline and detached physical-item state are live; final Aggregate-root swap remains |
| 89 | Remove runtime SO synthesis, catalog bypasses, optional DI, and late provider binding | In Progress — runtime synthesis/provider paths removed; code-owned catalogs remain |
| 90 | Split oversized runtime/UI classes, domain errors, localization, and domain asmdefs | In Progress |
| 91 | Run full regressions, two-resolution MCP UI proof, and Console Error/Warning 0 | Pending |

Current Phase 90 order:

1. ~~Rename `CharacterSummeryInfo` to `CharacterSummaryInfo` without breaking Unity GUID references.~~ Completed.
2. ~~Extract character-summary tab presenters and view models until the coordinator is below 800 lines.~~ Completed: coordinator 729 lines, 8 injected dependencies, presenters 147–516 lines.
3. Remove direct runtime `System.Random` construction and pin the rule in the V18 validator. Completed.
4. Split save DTO/query/policy responsibilities from the remaining largest runtime/UI classes. In progress: all UI exceptions are removed; `EquipmentEvolutionRuntime` is 1,176 lines, `AbilityMove` is 1,200 lines, and `CombatEquipmentRuntime` is 864 lines. The oversized-source baseline now contains 6 runtime entries.
5. Add `FailureCode + parameters` contracts and String Table presentation mapping. In progress: equipment module/lineage commands now return `DomainFailure`; the Korean `DomainFailures` Unity String Table has 21 validated entries.
6. Move only `NamedRequired` gameplay authority out of `Assembly-CSharp` and
   retain reviewed Unity-edge adapters under the Phase 117 manifest.
7. Replace the remaining regex ratchet with source-syntax and assembly/asset-graph validation.

The V18 validator currently enforces save V18, strict authored item authority, no legacy
item catalogs, no runtime content-SO synthesis, no direct runtime `Resources.Load`, no optional
interface injection, scoped session state, physical equipment authority, and one offense aggregate.
It now also enforces the character-summary size/dependency boundaries and zero direct runtime
`System.Random` construction.

## Goal

Complete V16 by removing isolated or duplicate gameplay authorities and proving that the connected
equipment, offense rewards, arrivals, exterior incidents, nutrition, circus resources, and AI
performance paths work together:

```text
physical production and meals -> persistent characters/items/equipment
-> offense and exterior outcomes -> physical arrivals and regional pressure
-> captivity/circus/survival follow-up work
-> V16 save round trip, pointer UI, visual evidence, and performance closure
```

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit offense, facilities, rooms, stock, staff, rewards, and save contracts | Completed |
| 2 | Implement route nodes, supplies, stress, formation, retreat, and expedition state | Completed |
| 3 | Connect dungeon rooms/facilities/stock to preparation, recovery, and expedition modifiers | Completed |
| 4 | Replace offense UI with preparation, route, node, and formation-aware battle surfaces | Completed |
| 5 | Persist and restore active multi-node expeditions with migration | Completed |
| 6 | Verify formulas and state transitions in EditMode | Completed |
| 7 | Verify pointer-driven recruitment, journey, battle, save/restore, and `truth_core` completion with MCP captures | Completed |
| 8 | Audit character identity, stats, training, battle abilities, UI, and save ownership for per-character progression | Completed |
| 9 | Implement per-character level, experience, learned skills, and equipped skill slots | Completed |
| 10 | Connect training and offense outcomes to experience and skill unlocks | Completed |
| 11 | Surface level, experience, learned/equipped skills in character and offense UI | Completed |
| 12 | Persist progression and migrate existing characters and saves | Completed |
| 13 | Verify progression formulas, combat skill legality, UI input, and save round trip | Completed |
| 14 | Replace legacy progression with level-50 potential, stat growth, narrative ledger, modular skills, passives, and ultimates | Completed |
| 15 | Add constrained LLM skill generation, validation, persistent retry, and hidden request state | Completed |
| 16 | Replace owner selection with three-character start preparation and persistent world population | Completed |
| 17 | Integrate growth/event UI, save V3 incompatibility handling, and combat/operation triggers | Completed |
| 18 | Verify growth generation, save restore, pointer workflows, world population, ultimates, captures, and regressions | Completed |
| 19 | Audit weak links between completed gameplay systems and prioritize missing feedback loops | Completed |
| 20 | Unify persistent character identity, social memory, and V4 save validation | Completed |
| 21 | Rebalance level-50 progression and connect generated skill modules to runtime events and formations | Completed |
| 22 | Share cached room environment queries with AI, mood, guest, and work duration systems | Completed |
| 23 | Add equipment catalog, crafting queue, expedition loadout, death loss, and facility recovery | Completed |
| 24 | Surface stat breakdowns, crafting, equipment, stress, and readiness in product UI | Completed |
| 25 | Add deterministic EditMode and pointer-driven PlayMode coverage for the new closed loop | Completed |
| 26 | Direct-play the campaign through `truth_core`, capture desktop/mobile/world evidence, and clear the Console | Completed |
| 27 | Add physical item catalog, world stack runtime, pile marker, and V5 save payloads | Completed |
| 28 | Connect delivery, rewards, warehouse aggregation, carried inventory, and hauling limits | Completed |
| 29 | Add Haul work type, AI hauling action, pickup/dropoff pathing, and overburden movement penalty | Completed |
| 30 | Add item pile UX with marker badges, list/detail panel, Alt-click override, and character-first selection | Completed |
| 31 | Convert shop restock, purchases/theft, crafting input/output, and expedition packing to physical stack flows | Completed |
| 32 | Add item/hauling EditMode coverage for stack merging, reservation, weight, save restore, and pile UX sorting | Completed |
| 33 | Add pointer-driven PlayMode coverage for item piles, hauling, warehouse/shop/craft/expedition flows | Completed |
| 34 | Capture stack marker, pile list/detail, carry UI, and clear Console Error/Warning 0 | Completed |
| 35 | Split new-run owner and start-party preparation into a dedicated preparation scene | Completed |
| 36 | Add owner fixed skill slots and reserve staff roster preparation | Completed |
| 37 | Build owner-select and RimWorld-style party preparation UI | Completed |
| 38 | Verify preparation scene navigation, selection, reroll, start handoff, and compile state | Completed |
| 39 | Fix start-preparation roster drag swap, RimWorld-style detail layout, dice reroll placement, and start-button gate | Completed |
| 40 | Add unified work-order runtime, construction sites, work units, and V9 save payloads | Completed |
| 41 | Route placement, AI work, materials, crafting, research, cooking, butchering, water, treatment, and refuel through work units | Completed |
| 42 | Surface construction/work progress in UI and character labels | Completed |
| 43 | Verify compile, focused contracts, pointer gameplay, save/restore, and visual captures for work progress | Completed |
| 44 | Diagnose and fix world nameplate occlusion and readability across dungeon layers | Completed |
| 45 | Replace wildlife horizontal oscillation with habitat-aware varied path movement and stable intent timing | Completed |
| 46 | Restore player camera zoom input with unscaled controls and verify nameplates, wildlife motion, and zoom in PlayMode | Completed |
| 47 | Audit staffed checkout waiting, customer patience, mood, memory, and alternate-shop handoff | Completed |
| 48 | Add patience-scaled checkout stages, service calls, complaints, abandonment, and alternate shopping | Completed |
| 49 | Surface checkout wait position, elapsed time, and reactions through character phases and event alerts | Completed |
| 50 | Verify patience rules, visit handoff, personal facility memory, PlayMode behavior, and Console state | Completed |
| 51 | Audit paused stair traversal visibility and multi-low-need AI triage | Completed |
| 52 | Make stair traversal visibility obey scaled simulation time | Completed |
| 53 | Fix survival-only emergency triage, fallback selection, and worker/owner self-care access | Completed |
| 54 | Add paused traversal and combined low-need regression coverage | Completed |
| 55 | Verify the fixes in PlayMode and clear Console errors/warnings | Completed |
| 56 | Audit repeated emergency wait and stationary-character fallback paths | Completed |
| 57 | Replace stationary wait fallback with contextual micro-actions and reachable roaming | Completed |
| 58 | Connect low mood to bounded autonomous impulses instead of passive waiting | Completed |
| 59 | Add anti-stall detection, retry/backoff, and regression coverage | Completed |
| 60 | Verify moving fallback and low-mood behavior in PlayMode | Completed |
| 61 | Audit the live research, reward, equipment, expedition, save, and UI contracts for the 168-node overhaul | Completed |
| 62 | Add causal prerequisite links, reward reverse indexing, 168 research specs, effort bands, and timing simulation | Completed |
| 63 | Add research-linked facilities, production items, recipes, and the 24-equipment content expansion | Completed |
| 64 | Enforce equipment research locks and implement tier, growth-slot, ammunition, smoke, reload, and misfire rules | Completed |
| 65 | Add expedition-only module instances, deterministic drops, appraisal/restoration/fitting/tuning, loss, and persistence | Completed |
| 66 | Add lineage seals, transfer orders, category-safe history inheritance, and form-neutral evolution contracts | Completed |
| 67 | Upgrade research/equipment saves to V4 incompatibility, expose unlock/lock/module/lineage UI, and add validation | Completed |
| 68 | Run focused compile, deterministic scenarios, pacing/content/save/UI verification, regenerate assets, and update docs | Completed |
| 69 | Audit the live recipe, item, equipment, construction, medical, supply, bill, conveyor, save, and UI graphs for V3 | Completed |
| 70 | Add production dependency contracts, reverse indexing, depth/branch validation, and concrete supply metadata | Completed |
| 71 | Re-author concrete branched intermediates, recipes, research rewards, facilities, equipment materials, and consumers | Completed |
| 72 | Implement repeat-forever/stock-sensor gating, local output buffers, fair branch distribution, fuel/feed selection, and V5 persistence | Completed |
| 73 | Surface dependency branches, route policy, stock-sensor unlock, and distinct blocked states in production/research/equipment UI | Completed |
| 74 | Add deterministic graph, runtime, logistics, save, compatibility, pacing, and two-resolution pointer coverage | Completed |
| 75 | Regenerate assets, compile, run focused and broad regressions, update docs, and clear Console Error/Warning 0 | Completed |
| 76 | Audit every item-definition authority, lookup fallback, feature field, instance side table, and save bridge | In Progress |
| 77 | Introduce one canonical ItemDefinitionSO base, composable immutable features, typed IDs, and strict validation | Pending |
| 78 | Migrate resource, equipment, survival, medical, wildlife, industrial, and special items into generated canonical assets | Pending |
| 79 | Replace permissive and hardcoded lookup chains with one indexed catalog and compatibility-only read adapters | Pending |
| 80 | Add generic versioned item-instance components, stack signatures, save persistence, and equipment/freshness bridges | Pending |
| 81 | Regenerate assets, compile, run item/production/equipment/save regressions, update docs, MCP capture, and clear Console | Pending |

## Product Decisions

- Party size remains 3 and the owner cannot join.
- Party positions are front, middle, and rear; skills declare usable and target positions.
- An expedition contains multiple connected nodes, not one battle.
- Supplies come from dungeon stock and are consumed during the expedition.
- Health and stress persist between nodes. Retreat preserves survivors and collected loot but forfeits unsecured rewards.
- Dungeon rooms and facility abilities provide preparation capacity, recovery, scouting, and supply efficiency.
- Death remains permanent. Returning survivors recover through dungeon services rather than automatic full healing.
- Campaign regions end in bosses; only the final `truth_core` boss reveals the truth and wins the run.
- The temporary campaign-order combat-stat multiplier is not part of the target design and must be removed.
- Character level, experience, learned skills, and equipped skills are per-character runtime/save state; `CharacterSO` remains immutable species/archetype authoring data.
- Skill definitions are shared data, while unlock and loadout state belong to each character instance.
- Character skill slots are fixed at one species active, three normal actives, two passives, and one ultimate.
- Potential affects only normal-active rarity odds; traits remain identity modifiers and passives remain event-triggered learned abilities.
- Generated skill state, drafts, narrative facts, retry keys, and use limits are per-world-character save data, never mutable shared ScriptableObject state.
- Skill rules choose rarity, budget, allowed module IDs, and variants before the LLM; invalid output retries under the same hidden request key with no player-facing fallback or generation status.
- The run begins with an owner and two same-species employees after all three have a selected level-one active and validated first passive.
- Old progression saves are intentionally incompatible with the new growth schema and must start a new game.
- `CharacterIdentity.PersistentId` is the sole runtime identity; template IDs never key per-person state.
- Room quality affects facility choice, mood, guests, and eligible work duration only; it never modifies offense stats directly.
- Facilities affect expeditions through crafted equipment and completed recovery use, not ambient combat bonuses.
- Generated skills have rule-authored formation masks and every accepted module must execute in an allowed runtime context.
- V4 rejects duplicate persistent IDs and V3-or-older saves instead of silently merging or migrating person state.
- Gold remains abstract money; non-gold delivery, reward, loot, crafting, shop, and expedition supplies become physical world stacks.
- `DungeonItemCatalogSO` owns item authoring; runtime stack/carry/save state must not be stored on shared ScriptableObjects.
- Item click priority is Character > Item > Building, with Alt-click forcing item pile selection on occupied cells.
- Warehouse inventory becomes an aggregate view of stored physical stacks while loose/carried/reserved items remain visible as separate states.
- Hauling capacity is character-owned and overburden affects movement speed, not global time scale.
- Stored warehouse stacks are hidden in normal play and become visible only through the `물품` view toggle; when stored stacks exist in V5 saves, they resynchronize the warehouse aggregate on restore.
- New runs use `StartPreparationScene` between title and gameplay. Gameplay-scene owner selection remains only as a direct-scene QA fallback.
- Owners have four fixed owner-skill slots in addition to normal generated growth slots. Fixed owner skills are authored static identity data, not LLM-generated or rerolled state.
- Start preparation contains one locked owner, two selected same-species staff, and four reserve staff candidates. Only selected staff enter the run.
- Start preparation treats the owner as ready through fixed owner skills; only selected staff must complete first active/passive start choices.
- Selected and reserve staff can be swapped by dragging roster cards onto each other.
- Player-placed buildings become construction sites with material delivery and work-unit progress; default/new-run seed buildings remain completed.
- Shared SOs may define static work requirements, but delivered materials, reservations, completed work, and queues are runtime/save state.
- Production recipes may reference only concrete item IDs; abstract `stock-item:*` matching remains available only through facility fuel/feed supply profiles.
- Every shared `Intermediate` item has at least two real downstream consumers, while fake `sink:*` recipes never satisfy branch validation.
- Production transformations after raw acquisition have a maximum dependency depth of four; single-purpose assemblies are finished installation components rather than fake intermediates.
- Facility input/output buffers, reservations, distribution policies, pending order-mode transitions, and chosen concrete supplies are per-facility runtime/save state, never mutable shared asset state.
- V3 production-network content intentionally rejects the preceding V4 research/equipment compatibility generation and requires a new V5 run.
- Item authoring has one authority: every physical item is an `ItemDefinitionSO` asset indexed by one strict catalog; domain catalogs are derived read-only views.
- Optional item behavior is authored through immutable feature modules, while freshness, durability, quality, provenance, and other mutable values use versioned runtime instance components.

## Verification Gate

1. Runtime and Editor assemblies compile with Console `Error 0 / Warning 0`.
2. Route generation, node transitions, supplies, stress, formation legality, retreat, death, loot, and boss completion have deterministic tests.
3. Dungeon stock and eligible room/facility effects visibly change expedition preparation and outcomes.
4. UI pointer input can prepare a party, buy/load supplies, choose route branches, resolve nodes, issue formation-valid combat commands, camp, and retreat.
5. Save/load restores the exact route node, party order, health, stress, supplies, loot, battle turn, cooldowns, and statuses without duplication.
6. A clean direct-player run completes all regions and reveals the truth at `truth_core`; no scenario-state injection counts as completion evidence.
7. MCP captures prove readable preparation, route, combat, return, and truth-result screens without overlap or input leakage.
8. Physical item work compiles cleanly before verification; no PlayMode result counts while Unity is running stale assemblies.
9. Stack pile list/detail, carried weight, hauling, warehouse/shop/crafting/expedition item flows, and Alt-click priority are verified by actual pointer/UI tests.
10. V5 save checks include world stacks, stored-warehouse mirrors, hauling settings, and per-character carried inventory.
11. Start preparation checks include title-to-preparation routing, owner fixed skill display, selected/reserve staff swap, prepared snapshot handoff, and no gameplay owner-selection panel in the product flow.
12. V9 work-order checks include construction-site placement, material delivery, partial progress, save/restore, and final building replacement without instant completion.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---|---|
| Single-battle campaign became unwinnable at stage 3 | Direct Normal playthrough | Rejected the thin-loop balance patch; redesign offense around persistent multi-node expedition progression and dungeon support. |
| Initial audit searched a nonexistent `Assets/Scripts/Stock` folder | 1 | Located stock runtime under `Buildings/SO/StockInfo.cs` and warehouse query services. |
| Reward regression still expected launch-to-boss and victory full heal | 6 | Reworked it to traverse route nodes, resolve encounters, defeat the boss, grant rewards, and retain survivor injuries. |
| Product-shell verification clicked a recruit card behind the bottom HUD | 7 | Scrolled the card into a 140px bottom-safe region; the pointer-driven product shell then passed. |
| Full-campaign UI verifier selected old alert buttons with matching labels | 7 | Scoped pointer lookup to the active offense map, expedition, or battle panel. |
| QA batch runner introduced `CS1626` by yielding inside `try/catch` | 25 | Moved coroutine yields outside the exception-handled block before rerunning feature tests. |
| Physical item pile PlayMode verifier still used the legacy owner-option flow | 30 | Replaced it with the current start-party fast commit path and added a request-file runner; the pile verifier then passed. |
| Physical item batch wait script looked for `PhysicalItemPile: PASS` while the report writes `[PASS] PhysicalItemPile` | 33 | Treated the shell wait exit code as a harness-string mismatch, then verified the actual batch report, target reports, and Console directly. |
| Recruited staff disappeared from later direct-play expedition candidates | 26 | Made `WorldCharacterProfile.isStaff` authoritative during population bind/refresh/promote/release and prevented the spawner from returning staff profiles to the visitor pool. |
| Offense reward regression still expected instant warehouse stock after physicalization | 26 | Updated reward tests to accept warehouse delta plus physical dropoff stack delta, and aligned recruit-candidate expectations with the handler's minimum-two rule. |
| Physical item theft test could read a duplicate empty carry inventory | 32 | Marked `CharacterCarryInventory` as single-instance and updated fixtures to resolve inventories through `CharacterCarryInventory.Ensure`. |
| Runtime visual-inspection command referenced TMP and wildlife properties that do not exist | 46 | Read the concrete APIs, then queried the TMP `MeshRenderer` sorting data and `WildlifeActor.DisplayName`; the corrected command passed. |
| First zoom persistence fix targeted the legacy `UnityEngine.U2D.PixelPerfectCamera` type | 46 | Runtime component inspection found `UnityEngine.Rendering.Universal.PixelPerfectCamera`; switched the alias to the URP type and reran the pointer test successfully. |
| Pond route probe called a nonexistent two-argument `Grid.SearchPath` overload | 47 | Read the concrete Grid API and used `GetMovePath(start, endPredicate)` instead. |
| Pond route probe started on the occupied entrance-door cell and reported no generic move path | 47 | Tested continuity from the first exterior surface cell; the exterior route and all shallow pond cells are reachable while only the boundary deep-water cell blocks movement. |
| Unity MCP approval was revoked during exact world-click verification | 48 | Used the compiled in-project UI regression request runner to execute the same Input System pointer path and collect the final report without bypassing gameplay input. |
| Physical delivery worker reached the source but could not pick up | Construction material delivery PlayMode attempt 1 | Found warehouse storage IDs were based on shared building definition `GridId`; replace them with a unique building instance key before rerunning. |
| Physical logistics rerun request did not auto-enter PlayMode | Construction material delivery PlayMode attempt 2 | Request file remained pending with Editor idle; enter PlayMode explicitly so the registered runner consumes the same request. |
| PlayMode could not start after warehouse-key migration edit | Construction material delivery compile attempt 2 | Preserved `IWarehouseFacility` type while matching a `BuildableObject` instead of passing the narrowed base type to the storage-key helper. |
| Combined regional-pressure patch did not match mojibake reward strings | V16 regional pressure attempt 1 | Split the change into focused structural patches and replace handler bodies using ASCII-only method boundaries. |
| Assumed `InvasionIntruderRuntime.cs` was a standalone file | V16 invasion pressure audit | Located the runtime in `InvasionIntruderSystem.cs` and switched subsequent reads to the concrete file. |
| MCP checkpoint command referenced the wrong `CompilationPipeline` namespace | V16 regional pressure compile attempt 1 | Use the MCP command's own pre-execution project compilation with a simple `AssetDatabase.Refresh` command. |
| Initial item-authority audit assumed `WildlifeItemDefinitions.cs` lived under `Services/Wildlife` | 76 | Locate the symbol by `rg --files` before reading the concrete definition file. |
| Assumed character body-health models were in a standalone file | V16 return-arrival audit | Locate the concrete interface by symbol before reading; the captivity eligibility check itself was confirmed in `CaptivityRuntime.cs`. |

## Dark Survival V11 Completion

- [x] Add per-character deprivation burdens, health damage, probabilistic/forced breakdowns, and BT priority handling.
- [x] Add desperate relief, unsafe-water drinking, starvation violence/cannibalism, collapse, and nonlethal suppression paths.
- [x] Add physical exterior water, floor filth, wall stains, clean work targets, humanoid corpse metadata, and emergency butchery.
- [x] Add the character health tab, world breakdown warning, filth information/priority command, and V11 persistence.
- [x] Verify focused EditMode contracts, pointer-driven PlayMode behavior, camera/screen captures, and Console `Error 0 / Warning 0`.

## Exterior Habitat Decoration Completion

- [x] Build one static wildlife decoration palette from the authored TINY FOREST flower, tree, and rock sprites.
- [x] Place deterministic, nonblocking flowers, trees, and rocks only on walkable exterior surface cells.
- [x] Bind Grass/Brush flower density to habitat resource so grazing removes flowers and regeneration restores them progressively.
- [x] Keep decoration runtime state derived from habitat patches; do not add duplicate save data or per-decoration SO assets.
- [x] Verify EditMode contracts, the live herbivore grazing loop, hierarchy cleanup, PlayMode snapshot, camera capture, and Console `Error 0 / Warning 0`.

## Exterior Pond Visibility Completion

- [x] Exclude the entrance and drop zone from default water generation.
- [x] Place one bounded four-cell pond at the outer edge of the longest exterior surface run.
- [x] Ground-align the water visual, unlock per-cell tint, and render a readable pixel-water strip above terrain.
- [x] Keep three shallow cells walkable, the outer deep cell blocked, and the exterior route connected.
- [x] Verify runtime positions, tile occupancy, camera capture, focused contracts, and Console `Error 0 / Warning 0`.

## Zoom Sky / Centered Dungeon Completion

- [x] Resize and reposition the solid sky from the live orthographic camera viewport whenever zoom, aspect, or camera position changes.
- [x] Center the 27-column dungeon interior inside the 60-column physical world and shift every authored GameplayScene placement by the same offset.
- [x] Center the gameplay camera on the resolved dungeon interior at scene start.
- [x] Verify left and right outer-wall tiles after the shift and visually inspect the maximum zoom-out frame.
- [x] Run physical-world, background-lighting, and grid-foundation regressions with Console `Error 0 / Warning 0`.

### Verification Notes

- Minimum zoom: camera Y `1.25..7.75`, sky Y `-2..11`, coverage `true`.
- Maximum zoom-out: camera Y `-6..15`, sky Y `-8..17`, coverage `true`.
- Runtime dungeon interior is Grid X `17..43`, authored placement shift is `+13`, and camera X matches the dungeon world center at `-29.5`.
- A broader runtime-composition policy scan still reports unrelated pre-existing direct-access violations in other systems; the focused changed-surface regressions pass.

## Entrance Outer-Wall Adjacency Fix

- [x] Reproduce the one-cell gap beside the centered dungeon entrance.
- [x] Exclude characters, wildlife, items, and nonstructural exterior markers from automatic side-wall structure detection.
- [x] Confirm the outer wall moves from Grid X `12` to the correct adjacent cell X `13` beside the three-cell dungeon door.
- [x] Add a marker-overlap regression and verify the repaired entrance with Unity MCP Camera Capture.
- [x] Run grid visual, foundation, and physical-world regressions with Console `Error 0 / Warning 0`.

## Exact Facility World Click Completion

- [x] Remove the arbitrary `GridCell.GetBuilding()` fallback from ordinary facility selection.
- [x] Require an actual `Physics2D.OverlapPointAll` collider hit for facilities and construction sites.
- [x] Keep exact-cell fallback only for structural walls and interior doors that are rendered without normal colliders.
- [x] Reject hallway/floor definitions even if a hallway collider is present.
- [x] Verify actual facility click, bare hallway click, character-over-building priority, and exclusive info panels through Input System pointer events.
- [x] Finish with the UI regression batch at `RESULT=PASS`, captured `Error 0 / Warning 0`.

## Consecutive Wildlife Click Completion

- [x] Reproduce the same-animal consecutive click failure in the popup lifecycle.
- [x] Close the previously registered popup before assigning the newly clicked wildlife target.
- [x] Add current-target/open-state diagnostics and a repeated-event regression.
- [x] Add two consecutive Input System pointer clicks to the world-info PlayMode verifier.
- [x] Verify wildlife contracts, UI regression batch, and Console `Error 0 / Warning 0`.

## Wildlife World-Facing Completion

- [x] Trace wildlife facing against the project's mirrored Grid-to-world X mapping.
- [x] Derive horizontal facing from world-space movement instead of logical Grid X delta.
- [x] Update the natural-motion regression to assert left/right in world space.
- [x] Verify every wildlife species present in GameplayScene in both horizontal directions.
- [x] Finish with wildlife contracts and Console `Error 0 / Warning 0`.

## Defense Interception And Engagement V12

- [x] Audit current invasion movement, guard commands, defense UI, DI, persistence, and compile state.
- [x] Add adjacent-cell interception, reciprocal combat, one lead guard, and one replacement guard.
- [x] Add RimWorld-style defense policies assigned per guard and owner evacuation to an administration room.
- [x] Connect manual suppression, skill events, combat presentation, player-facing status, and defense UI.
- [x] Persist policies, assignments, owner evacuation, and active engagements in V12 saves.
- [x] Verify focused contracts, pointer-driven PlayMode combat, captures, and Console gameplay `Error 0 / Warning 0` after the known Unity 6000.3.8 startup warning.

### Defense Decisions

- Automatic interception is limited to on-duty non-owner staff with Guard priority enabled.
- Melee combat is one blocker versus one intruder on separate adjacent cells; a second guard may wait behind for replacement but cannot attack through the lead guard.
- Defense behavior is configured through named policies and each guard is assigned one policy.
- The owner never auto-dispatches. Every invasion cancels the owner's current action and evacuates them to an Administration room, or the farthest reachable interior safe cell when no valid room exists.
- Empty frontline means the intruder resumes advancing immediately; zero health continues to use the existing permanent-death flow.

## Developer Mode And Debug Palette

- [x] Add settings schema V2 with developer mode disabled by default and a dedicated Development tab.
- [x] Add a center-top Debug button, responsive non-modal palette, search, numeric input, eight command tabs, and exact world targeting.
- [x] Register 112 modular commands across cheats, spawning, characters, building/work, survival/wildlife, defense/events, overlays, and history.
- [x] Connect persistent `debugModified` metadata and a 50-entry command history while resetting transient cheats and overlays after load.
- [x] Verify pointer targeting, Shift repeat, right-click/Escape cancellation, commands, invasions, save behavior, overlays, and both supported aspect ratios.
- [x] Finish with EditMode PASS, PlayMode `RESULT=PASS`, Camera Capture comparison, and Console `Error 0 / Warning 0`.

## Construction Material Physical Delivery

- [x] Trace construction placement, delivery request, warehouse reservation, pickup, and site deposit.
- [x] Remove any construction-site material spawning or teleporting at placement time.
- [x] Keep materials physically stored until a worker picks up the reserved quantity and deposits it into the site buffer.
- [x] Add regressions for no-stock waiting, partial reservation, pickup, deposit, and construction readiness.
- [x] Verify the live placement-to-haul flow and Console `Error 0 / Warning 0`.

## Medieval Dark Fantasy Combat V13

- [x] Add shared melee, ranged, and recoverable-throw resolution with range bands, fire modes, evasion, directional cover, friendly-fire gating, armor penetration, body parts, bleeding, suppression, and pause-safe presentation.
- [x] Add individual weapon, armor, shield, and ammunition data plus persistent equipment instances, quality, armor durability, loadouts, reloads, crafting recipes, and V13 save state.
- [x] Connect defense to rally-time physical loadout pickup, post-breach melee interception, ranged line-of-sight combat, reciprocal damage, owner evacuation, and recoverable thrown equipment.
- [x] Connect offense to the same resolver, formation distance, cover, weapon switching, ammunition, body-part injuries, suppression turn loss, and persistent return-state wounds.
- [x] Add combat UI, cover buildings, exact multi-select/direct movement commands, fire-mode/hold-fire controls, and player-facing combat status.
- [x] Connect wildlife hunting and retaliation to the shared combat resolver, ranged firing positions, scaled-time reloads, simplified persistent body profiles, and real armor/body damage on hunters.
- [x] Verify static combat/offense/defense/priority/wildlife contracts, PlayMode wildlife loop, defense rally and engagement, direct movement and cancellation, visual capture, and Console `Error 0 / Warning 0`.

### V13 Verification Notes

- Defense PlayMode: rally held outside, four reciprocal exchanges on distinct adjacent cells, both sides damaged, intruder movement and facility attacks locked, owner evacuation and save snapshot valid.
- Player command PlayMode: `Cain (19,0) -> (17,0)` completed with manual lock released; a second move cancelled immediately also released its lock.
- Wildlife PlayMode: runtime snapshot and hunt/carcass/butcher loop passed; limb injury lowers mobility and survives capture/restore.
- `ScreenCapture`: `Artifacts/QA/combat-v13-defense-final.png`.
- Unity MCP `Camera_Capture` was attempted twice against the live Main Camera but the connector returned `Failed to render scene preview`; the direct Game View capture rendered correctly.

## V16 Isolated Feature Integration

- [x] Audit duplicate scene runtimes, legacy equipment, abstract rewards, food consumption, exterior incidents, circus milestones, extract resources, and AI performance diagnostics.
- [x] Remove duplicate GameplayScene command/customer runtimes and enforce exact-one composition lookup.
- [x] Remove the legacy expedition equipment stack and make common combat equipment authoritative for crafting, storage, loadout, defense, and offense.
- [x] Replace abstract offense weakening and reward counters with regional pressure and physical return arrivals.
- [x] Connect exterior incidents, reception, patrol readiness, weather, sanitation, and night danger to physical actors and outcomes.
- [x] Remove daily abstract food withdrawal and make completed character meals the sole nutrition consumption path.
- [x] Connect circus fame milestones, injury gating, Biological blood, and Knowledge memory residue to work-unit consumers.
- [x] Wire allocation-free AI performance recording and remove unused expedition support ability and mojibake on changed surfaces.
- [x] Finish broad regressions, pointer-driven PlayMode verification, captures, performance checks, and Console `Error 0 / Warning 0`.

### V16 Performance Closure

- [x] Split full Grid content changes from structural/traversal changes so items, wildlife, and filth do not invalidate route and room caches.
- [x] Preserve current wildlife target reachability without relying on stale cached occupant positions.
- [x] Repair wildlife arrival dwell to use one game-clock time base and pass Grid/Wildlife/AI focused regressions.
- [x] Re-run 100-NPC EditMode stress: elapsed `353s -> 50.6s`, broker searches `1440 -> 51`, deferrals `16461 -> 50`, Scheduler p95 `0.73ms`.
- [x] Run PlayMode profiling and the broad V16/domain regression matrix.
- [x] Perform current visual capture and final stopped-editor Console audit.

### V16 Verification Notes

- Broad domain matrix passed for V16 integration, save sections, survival, exterior activity,
  captivity/circus, offense reward/battle, combat, defense, work amount, Grid, AI naturalness,
  wildlife, and physical items.
- Pointer-driven UI verification passed `21/21` rows at `1600x900` and `900x1600`, including
  alert right-click dismissal, with captured `Error 0 / Warning 0`.
- The stabilized 100-character PlayMode profile recorded frame `2.77ms average / 3.42ms p95`
  and scheduler `0.370ms average / 0.497ms p95 / 0.632ms max`, with all 100 behavior trees
  ticked and no decision/path-budget overflow.
- Unity Editor-wide GC averaged `182KB/frame`; subtracting the measured one-character Editor
  baseline of about `120KB/frame` leaves about `62KB/frame` attributable to the stress world.
  The Mono backend does not support `GC.GetAllocatedBytesForCurrentThread`, so the report marks
  scheduler-only allocation as unsupported instead of falsely reporting zero.
- `Artifacts/QA/v16-gameplay-world.png` and
  `Temp/p1-p2-ui-surface-verification.png` provide current world and HUD evidence. Direct
  `Camera_Capture` still returns `Failed to render scene preview`; direct Game View capture works.

### V16 Decisions

- V16 is new-game only; V15 and older saves are rejected with a Korean explanation.
- Common combat equipment is the only authoritative equipment runtime.
- Food is consumed only when a character completes a real meal.
- Prisoners, special wildlife, and recruits return as physical or persistent world entities rather than counters.
- Strategic pressure is regional with a 25% same-faction spillover.
- Blood and memory residue remain physical resources with multiple work-based consumers.

## V17 Weighted Navigation and 500-Character Performance

- [x] Add deterministic terrain/traversal costs and weighted path results.
- [x] Use exact A* for fixed destinations and weighted Dijkstra for multi-target selection.
- [x] Add versioned broker caching, bounded search budgets, and reusable search workspaces.
- [x] Replace per-frame actor polling with due-time scheduling and immediate dirty wakeups.
- [x] Remove benchmark scene scans and hot-path decision/presentation allocations.
- [x] Pass focused Grid/100-character regressions and the staged 500-character profile.

### V17 Verification Notes

- 500 actors, 600 sampled frames: 3.39 ms average, 4.37 ms p95, 15.40 ms maximum,
  and 0 frames over 16.67 ms.
- Scheduler average/p95/max: 1.228/1.809/2.580 ms.
- Broker: 527 searches, 8,674 cache hits, bounded at 7 searches and 8 deferrals per frame.
- Incremental GC after the same-world Editor baseline: 36.0 KB/frame.
- Per-request Jobs/Burst are intentionally deferred: current weighted A* measures about
  11.3 microseconds/query, below practical scheduling overhead. Future parallelization must
  batch immutable offscreen work.

## Item Architecture V6

- [x] Phase 76: Audit all item-definition authorities, hardcoded fallbacks, side tables, saves, and generators.
- [x] Phase 77: Add canonical `ItemDefinitionSO`, typed IDs, composable features, and strict validation.
- [x] Phase 78: Generate canonical SO assets for resource, equipment, survival, medical, wildlife, industrial, and special items.
- [x] Phase 79: Replace permissive lookup chains with one strict indexed catalog and compatibility-only adapters.
- [x] Phase 80: Add versioned instance components, stack signatures, persistence, hauling propagation, equipment and freshness bridges.
- [x] Phase 81: Regenerate, clean-compile, run V3/research/pacing regressions, update docs, capture through Unity MCP, and finish Console 0/0.

### Item V6 Verification Notes

- 296 canonical item SOs; 43 equipment item features; duplicate IDs 0; invalid features 0.
- 110 generated compatibility/equipment assets, all with valid concrete script references.
- Stack-component signature isolation and hauling/carry-save/deposit propagation pass.
- Production V3 and research/equipment regressions pass; pacing is 32.2/80.4/234.3/372.0 days.
- Unity MCP Main Camera capture is 1920x1080; final Console is Error 0 / Warning 0.
- The legacy physical-item verifier still names its global save assertion `save_v10_contract`
  and expects V10 although the current global contract is V17.

## Runtime Data, SO, and Save Authority Normalization V18

- [x] Phase 82: Establish the V18 new-game-only boundary and executable architecture baseline.
- [x] Phase 83: Make authored SO catalogs the only content-definition authority and remove item fallbacks.
- [x] Phase 84: Introduce mandatory typed persistent IDs for item, character, and building instances.
- [x] Phase 85: Make physical item instances authoritative for warehouse stock and equipment state.
- [x] Phase 86: Move mutable `GameData` and static run state into scoped session services.
- [x] Phase 87: Consolidate legacy and V17 offense into one runtime and save aggregate.
- [x] Phase 88: Add staged, preflighted, atomic aggregate restore for V18 saves.
- [x] Phase 89 planning entry retired: optional required-interface DI and `Bind*Runtime` are already zero; remaining asmdef/static closure is tracked only by Phase 112 Batches F–I.
- [x] Phase 90 planning entry retired: remaining Roslyn validation, decomposition, and localization adoption are tracked only by Phase 112 Batches F/J/K/L.
- [x] Phase 91 planning entry retired: the full regression/capture gate is tracked only by Phase 112 Batch N.

### Phase 88 detached-root follow-up

- [x] Make physical items, production, combat equipment, character environment, and treasury economy restore through replaceable Aggregate roots.
- [x] Add a composition-wide candidate root and publish migrated Aggregate slots with one successful root swap.
- [x] Remove combat equipment's duplicate physical equipment/module restore writes.
- [x] Move dark-survival deprivation, world-water, world-filth, and character-consumable state into detached Aggregate slots; delay Unity terrain/tile/work-target projection until the published root is observed.
- [x] Move husbandry animals/policies, captives/policies/sequences, and captured wildlife into detached Aggregate slots; defer door, carry-parent, actor capture, warp, and other scene projections until publication.
- [x] Move deterministic random-stream state into the composition root while preserving stable injected stream handles across root publication/discard.
- [x] Move run seed/day/variable/replay state into the composition root and add type-level copy-on-write for mutations during shallow candidate staging.
- [x] Make meta-profile merge copy-on-write and move per-run meta progress/result lifecycle into replaceable root slots.
- [x] Move research task/progress/queue/unlock state and knowledge-residue processing into replaceable Aggregate slots; defer queue/workforce projection until publication.
- [x] Move Codex entry/title/information-line state into a deep-copy Aggregate slot and replace live clear/repopulate restore with strict detached decoding.
- [x] Move regular-customer visit/recruitment records into one deep-copy Aggregate slot and derive recruited-result views instead of storing a second list.
- [x] Move facility-shop offer day and purchase unlocks into one Aggregate slot, remove duplicated research unlock data from its save section, and rebuild deterministic offers only outside candidate commit.
- [x] Move power, fluid, conveyor, and automation infrastructure state into four deep-copy Aggregate slots; make automation demand a root-derived projection and add strict industrial save preflight.
- [x] Move event-alert history, dismissals, and ID sequencing into a deep-copy Aggregate slot; validate one DTO contract at every restore entry point and rebuild Unity UI only after root publication.
- [x] Move operating-day ledgers, debt, and report history into a deep-copy Aggregate slot; share strict nested payload validation and prove candidate discard preserves the live ledger.
- [x] Move work-order progress/sequence state into a deep-copy Aggregate slot; prepare construction sites inactive on the detached facility Grid and publish them in the `100 facilities -> 150 sites -> 200 characters` world boundary.
- [x] Move wildlife population/raid scheduling into one runtime Aggregate, prepare inactive actors on the detached Grid, and publish population, ecosystem, and carcass projections at participant `250.world.wildlife`.
- [x] Make exterior activity the sole owner of exterior-zone markers, exclude them from facility persistence, and publish inactive restored zones at participant `300.world.exterior-zones`.
- [x] Move offense return-arrival queues/barriers into a replaceable Aggregate and defer prisoner/wildlife materialization until normal post-publication ticking.
- [x] Planning entry retired; the exact remaining save-owner list and rollback-image removal are tracked only by Phase 112 Batches A–E.

### Phase 96 — AIBrain responsibility closure

- [x] Replace the 12-parameter AIBrain construction path with explicit decision/execution capability bundles.
- [x] Extract authored action-list configuration from mutable decision state.
- [x] Give action evaluation, cooldowns, resumable candidate scoring, continuation policy, path search, and debug formatting dedicated owners.
- [x] Reduce `AIBrain` from 2,319 lines to the enforced 1,200-line runtime boundary and remove its baseline exception.
- [x] Planning entry retired; final AI import/regression proof is tracked only by Phase 112 Batch N.

### Phase 97 — Defense engagement responsibility closure

- [x] Replace the 16-parameter defense engagement constructor with two explicit eight-capability service bundles.
- [x] Move ranged-position planning and ranged-support movement/fire state to dedicated owners.
- [x] Move defense save mapping/restore interpretation, guard pause control, and engagement combat lifecycle to dedicated owners.
- [x] Reduce `DefenseEngagementRuntime` from 2,258 lines to the enforced 1,200-line boundary and remove its baseline exception.
- [x] Planning entry retired; final defense regression proof is tracked only by Phase 112 Batch N.

### Phase 98 — Surgery runtime responsibility closure

- [x] Replace the 28-parameter surgery constructor with four explicit capability bundles.
- [x] Move order validation, save mapping, environment recovery, and patient/material logistics to dedicated owners.
- [x] Remove stock-category-derived medical material IDs in favor of concrete authored item IDs.
- [x] Reduce `SurgeryRuntime` from 2,565 lines to 1,168 lines and remove its baseline exception.
- [x] Planning entry retired; final surgery/save regression proof is tracked only by Phase 112 Batch N.

### Phase 99 — Strategic offense presentation closure

- [x] Separate strategic preparation/factions, encounters, view construction, and detail projection from the screen coordinator.
- [x] Keep every strategic presentation source below the 800-line Presenter limit.
- [x] Reduce `OffenseWorldMapPanelStrategic.cs` from 2,044 lines to 528 lines and remove its baseline exception.
- [x] Planning entry retired; both strategic layouts and pointer flow are tracked only by Phase 112 Batch N.

### Phase 100 — Wildlife runtime responsibility closure

- [x] Replace the 20-parameter wildlife constructor with world, combat, and execution capability bundles.
- [x] Move hunt combat to `WildlifeHuntRuntime` and food-raid/ecology movement to `WildlifeBehaviorRuntime`.
- [x] Remove the hunter-name reservation-key fallback and require typed character identity.
- [x] Reduce `WildlifeRuntime` from 2,513 lines to 921 lines; all runtime helpers remain below 1,200 lines.
- [x] Remove the final runtime architecture-baseline exception; remaining exception count is zero.
- [x] Run wildlife, hunt, food-raid, save, and ecology regressions through Unity after MCP reconnects.

### V18 Decisions

- V17 and older saves are rejected with `대규모 데이터·식별자·저장 구조 개편 이전 저장 — 새 게임 필요`; there is no automatic migration.
- Authored SO assets and one explicit root catalog are the content source of truth. Editor builders are bootstrap/migration tools only.
- Each phase removes the old write path before completion; no dual-write compatibility layer may survive a phase boundary.
- ScriptableObjects contain immutable authored definitions only. Mutable run state belongs to scoped plain C# services and versioned save DTOs.
- Derived indexes are allowed only when they are non-persistent and fully rebuildable from authoritative state.

### V18 Authority Verification Notes

- Global save root is V18 and V17-or-older slots are rejected through one compatibility policy with the exact new-game-required message.
- `GameContentCatalogSO` is the single Resources bootstrap root; its explicit item catalog currently contains 772 validated SO definitions.
- The obsolete `DungeonItemCatalogSO` type/asset, code-owned item-definition factories, unknown-item synthesis, and abstract `stock-item:*` authored inputs are removed.
- Dynamic evolution drops resolve to 147 authored catalyst SOs and 21 authored residue SOs; potency is bounded to the authored 1-21 range.
- `RuntimeAuthorityV18Validator` passes with legacy item authority 0, duplicate/invalid item definitions 0, and Unity Console Error 0 / Warning 0.
- Item stacks, unique items, characters, buildings, and warehouse destinations now use distinct typed persistent IDs; warehouse storage keys no longer fall back to grid coordinates or object hashes.
- The registered `IStockQuery` is a rebuildable view over physical stacks, and equipment item-state schema V2 round-trips the full equipment snapshot plus attached module state.
- `WarehouseInventory` owns only capacity/category policy; all runtime and Editor aggregate `Deposit/Withdraw/AddStock` entry points are gone.
- `items.physical` V6 is the only equipment/module instance save authority. `combat.equipment` V6 stores loadouts, work orders, material policies, lineage orders, and seal claims only.
- Equipment creation, physical materialization, carry, storage, facility buffering, and save/restore preserve one typed `ItemInstanceId`; mismatched and duplicate identities fail explicitly.
- Phase 85 focused contracts pass for physical items, physical stock queries, building persistence, facility evolution, combat equipment, material equipment, and the 168-node research/equipment overhaul.
- `GameData` now contains authored starting settings only. Mutable money, calendar, pause, and speed live in a plain run-scoped `GameSessionState` and are changed through `IGameMoneyAccount`, `IGameCalendar`, and `IGameSpeedController`.
- Character carry lookup, combat-cover durability, skill execution deduplication, user settings, and presentation/skill catalog access no longer use static mutable run registries or runtime SO synthesis.
- The root catalog explicitly references authored world-presentation and character-skill settings assets; the V18 validator enforces this SO/session boundary and the removed global registries.
- Phase 86 focused regressions pass for V18 authority, physical items, combat, facility shop, operating-day settlement, developer mode, invasion, and UI lighting. Unity Console is Error 0 / Warning 0.
- The four offense save authorities were replaced by the sole `offense.aggregate` section. All V17 names and the late strategic runtime bind were removed, and non-offense runtime code now uses `IOffenseQuery`/`IOffenseApplication` rather than scene MonoBehaviour providers.
- Expedition rewards materialize through the physical reward item sink; reward state no longer writes aggregate warehouse stock. Strategic, expedition, map, reward, recruitment, and save-section regressions pass together.
- V18 saves now carry an explicit compatibility manifest. Restore preflights manifest/sections, typed JSON, persistent identities, authored item/building references, and offense-to-character references before mutation.
- A full rollback image is captured before commit. The injected final-section failure regression proves earlier mutations are reverted, and the live 54-section PlayMode save round trip passes.
- Phase 90 decomposition progress: `CharacterDeprivationRuntime` is 1,123 lines and no longer needs an architecture-baseline exception; safe-relief planning/execution, emergency movement, breakdown actions, world access, and consequences are focused collaborators below their limits.
- Phase 90 decomposition progress: `FluidNetworkRuntime` is 1,199 lines after extracting node-water rules and snapshot projection. The architecture baseline is down to 40 exceptions, clean Unity compilation is Error 0 / Warning 0, and the V18 authority plus industrial infrastructure regressions pass.
- Phase 90 decomposition progress: `ExteriorActivityRuntime` is 1,101 lines after moving the stateful `ExteriorZoneMarker` facility into its own source owner. The baseline is down to 39 exceptions and exterior regressions pass.
- Phase 90 decomposition progress: `WildlifeEcosystemRuntime` is 1,142 lines after separating habitat definitions/markers and the rebuildable overlay cache. Wildlife regressions pass and the baseline is down to 38 exceptions.
- Phase 90 decomposition progress: `AnimalHusbandryRuntime` is 1,200 lines after moving auto-slaughter/compatibility policy and reusable work rules into focused collaborators. Clean compilation and V18 validation pass; 37 exceptions remain.
- Phase 90 decomposition progress: `CircusRuntime` is 1,200 lines after extracting forecast/venue calculations, combatant values, and world queries. Captivity/circus regressions pass; 36 exceptions remain.
- Phase 90 decomposition progress: the industrial surface presenter is 781 lines and the character-summary runtime factory is 800 lines after extracting their separate tab/layout owners. UI architecture validation passes; 34 exceptions remain.
- Phase 90 decomposition progress: settings UI is 788 lines and owner selection is 765 lines after extracting platform resolution/input and view-only rules. Owner regressions pass; 32 exceptions remain.
- Phase 90 decomposition progress: `UIBuildingInfo` is 774 lines after extracting action/progress/status view creation. Facility fixtures now provide typed building IDs and stock-query capability, aggregate stock-supply fallback is removed, and V18/facility regressions pass; 31 exceptions remain.
- Phase 90 decomposition progress: `DungeonTitleUiController` is 796 lines after extracting canvas/EventSystem lifetime and title text/slot formatting. Clean compilation and V18 validation pass; 30 exceptions remain.
- Phase 90 decomposition progress: the warehouse feature source is 745 lines after extracting mutation commands. Production fixtures now issue typed building IDs; clean DLL rebuild, V18, UI architecture, and production-economy regressions pass; 29 exceptions remain.
- Phase 90 decomposition progress: `ProductionBuildingPanelPresenter` is 752 lines after extracting workshop-link rendering and stateless production view creation. Clean compilation and production/UI regressions pass; 28 exceptions remain.
- Phase 90 decomposition progress: the defense model/presenter source is 412 lines after moving query and command implementations to their own owners. Defense threat/engagement/report regressions pass; 27 exceptions remain.
- Phase 90 decomposition progress: surgery application service and MonoBehaviour view are separate 693/457-line owners. All stale 141-research fixture assertions are updated to 168, and surgery regressions pass; 26 exceptions remain.
- Phase 90 decomposition progress: the operations model/presenter source is 532 lines after extracting query and command owners. A broken AI settings script reference is repaired, surgery/research tests no longer rebuild content, and operations/content regressions pass; 25 exceptions remain.
- Phase 90 decomposition progress: `WorkTargetSelector` is 1,160 lines after extracting target eligibility, environment assessment, exterior-work rules, and scan state. Isolated UI participants and typed construction-site fixture IDs repair two stale test authorities. Clean compilation, V18 authority, work-priority/corner-case/work-amount/naturalness regressions pass; 16 exceptions remain.
- Phase 90 decomposition progress: `CharacterBodyHealthRuntime` is 1,050 lines after moving contracts and deterministic state normalization/anatomy projection into focused owners. Clean compilation, V18 authority, combat, anatomy-medical integration, and surgery regressions pass; 15 exceptions remain.
- Phase 90 decomposition progress: invasion director and intruder are separate owners; defense observation, awareness-aware path planning, and combat math/status rules are extracted. `InvasionIntruderRuntime` is exactly 1,200 lines. Clean compilation plus threat/intruder/engagement/report regressions pass; 14 exceptions remain.
- Phase 90 decomposition progress: `SurvivalFoodRuntime` is 1,192 lines after extracting state persistence, physical-stock access, spoilage/freshness synchronization, meal ledger, health rules, and facility-work rules. Its meal ledger now requires typed building identity. The physical-craft fixture uses the root SO material catalog and a persistent facility ID. Clean compilation plus V18 authority, survival, physical-stock, and physical-item regressions pass; 13 exceptions remain.
- Phase 90 decomposition progress: `Shop` is 1,196 lines after extracting product inventory/pricing, crime resolution, service completion, and save/read contracts. The legacy money adapter, implicit feedback fallback, and cached mutable session provider are removed. The architecture baseline is down to 12 exceptions; facility regressions are being normalized to mandatory typed IDs and physical-stock-only warehouse semantics.
- Phase 90 decomposition progress: `CaptivityRuntime` is 1,197 lines after separating policy ownership, performer progression, management interactions, escort state, escape planning, and lifecycle/save state. Housing persistence now uses `BuildingInstanceId` rather than type/coordinates. The architecture baseline is down to 11 exceptions; standalone Unity Roslyn compilation is Error 0 / Warning 0 while Unity MCP regression execution awaits bridge recovery.
- Phase 90 decomposition progress: `OffenseBattleModel` now contains only the 1,170-line battle session; contracts, encounter content, and deterministic session rules have separate owners. `Grid` is 1,166 lines after separating cell rules, path results, search workspaces, and traversal-heuristic indexing. The architecture baseline is down to 9 exceptions and standalone Unity Roslyn compilation remains Error 0 / Warning 0.
- Phase 90 decomposition progress: the 689-line `OffenseExpeditionPanel` MonoBehaviour now has its own source owner instead of sharing `OffenseExpeditionSystem.cs` with the expedition Aggregate. The remaining expedition runtime is 2,105 lines and still requires strategic-travel/battle-result decomposition before its exception can be removed.
- Phase 91 expedition aggregate decomposition: `OffenseExpeditionRuntime` is now 1,117 lines. Field mobility, result finalization, asynchronous return processing, strategic target/travel handling, battle launch, and battle completion are explicit services; the runtime line-limit exception was removed.
- Phase 92 production-order decomposition: `ProductionBillRuntime` is now 1,164 lines. Output reservations, stock-sensor ownership, utility validation, input logistics, save mapping, and query projection each have one explicit owner; the production line-limit exception was removed.
- Phase 93 equipment aggregate decomposition: `CombatEquipmentRuntime` is now 864 lines with exactly eight required constructor dependencies. Craft orders/material policies/unlock checks live in `CombatEquipmentCraftingRuntime`; loadout references, hand/layer policy, snapshots, confiscation, and character-death loss live in `CombatEquipmentLoadoutRuntime`; physical equipment and module payloads remain owned only by `IItemInstanceRepository`. The runtime no longer constructs its own policy implementations, equipment crafting no longer converts `StockCategory` to an abstract item, and the line-limit exception was removed.
- Phase 94 physical-item aggregate decomposition: `WorldItemStackRuntime` is now 1,030 lines with exactly eight required dependencies. Persistence, warehouse routing, theft, and read/mutation facets have explicit owners. Restore now validates a complete `WorldItemRestoreState` before clearing the live repository, and warehouse stock is never synthesized from `WarehouseInventory`. The line-limit exception was removed; five oversized-runtime exceptions remain.
- Phase 95 V18 architecture-ratchet repair: stale V15/2,169-line tests now enforce save V18 and the shared 1,200/800 architecture baseline. Mutable static declarations use an explicit cache/profiler approval set instead of a numeric allowance. Wildlife habitat and industrial infrastructure persistence now require typed generated IDs; scene-transition requests live in a persistent mailbox rather than static fields.

### Phase 105 — Authored taxonomy authority cutover

- [x] Author 6 character needs, 11 stock categories, and 8 building categories on `GameDomainContentCatalogSO`.
- [x] Project those records through the immutable, injected `AuthoredGameplayCatalog`.
- [x] Remove the three mutable static catalogs and all production/Editor call sites.
- [x] Keep stock persistence IDs as a fixed V18 protocol while display and balance data remain SO-authored.
- [x] Pass Unity V18 authority and authored taxonomy validation after a Unity-native reimport.
- [x] Build facilities and characters as inactive restore candidates and register both as final transaction participants.
- [x] Restore characters against the detached facility Grid and quiesce the live character world only at final publication.
- [x] Planning entry retired; this scope is tracked only by the exact Phase 112 Batches A–E.

### Phase 106 — Character medical detached restore authority

- [x] Move medical orders and sequence into one replaceable Aggregate slot.
- [x] Reject malformed medical payloads and broken patient/facility references before live publication.
- [x] Prepare downed-character Grid occupants on the detached facility Grid and publish them at participant order `350.world.medical`.
- [x] Convert `combat.medical` to the shared typed JSON preflight boundary and remove the legacy warning/skip restore path.
- [x] Split restore orchestration from `CharacterMedicalRuntime`; the runtime source is 1,199 lines and has eight required dependencies.
- [x] Add a public save-service PlayMode regression for invalid medical preflight preserving the live Aggregate view.
- [x] Execute medical PlayMode restore scenarios and Console verification through Unity MCP after transport recovery.

### Phase 107 — Character combat-command detached restore authority

- [x] Store combat commands, stance membership, actor revisions, and the command ID sequence in one Aggregate.
- [x] Upgrade `combat.commands` to strict V2 typed preflight; do not migrate or normalize the removed V1 contract.
- [x] Validate candidate actors, targets, cells, and physical weapon instances before replacing the Aggregate slot.
- [x] Publish AI pause/stance presentation only at participant order `400.world.combat-command-stances`.
- [x] Replace fourteen direct runtime dependencies and four internally constructed collaborators with three explicit capability groups plus the Aggregate root.
- [x] Make wildlife world queries candidate-aware so downstream combat target validation never observes the retired live population.
- [x] Add public save-service invalid-preflight preservation coverage and pass runtime/Editor auxiliary compilation.
- [x] Convert defense-tactical reservations and equipment-maintenance orders, the remaining legacy combat save boundaries.

### Phase 108 — Defense-tactical reservation Aggregate

- [x] Persist the reservation ID sequence with the reservation set in one Aggregate.
- [x] Upgrade `combat.defense-tactics` to strict V2 typed preflight.
- [x] Validate canonical IDs, actor/cell uniqueness, enums/scores, candidate actors/targets, and candidate Grid walkability before replacement.
- [x] Replace one Aggregate slot without clearing or normalizing live reservations.
- [x] Add public save-service invalid-preflight preservation coverage and V18 source ratchets.
- [x] Convert `combat.equipment-maintenance`, the final legacy combat save boundary in this group.

### Phase 109 — Equipment-maintenance Aggregate and final combat save boundary

- [x] Move maintenance policies, assignments, orders, and both ID sequences into one replaceable Aggregate.
- [x] Upgrade `combat.equipment-maintenance` to strict V2 typed preflight with authored item/facility/equipment validation.
- [x] Remove coordinate-derived facility persistence, duplicate material fields, warning normalization, and live-clear restore.
- [x] Add public invalid-preflight preservation coverage and V18 source ratchets.

### Phase 110 — Runtime size and MonoScript identity closure

- [x] Split `BuildableObject`, husbandry work rules, and fluid-network projection below runtime limits.
- [x] Move run-variable and meta-run Aggregate types away from scene-bound MonoScript GUID owners.
- [x] Keep medical and combat-command behavior additions below 1,200 lines through focused partial owners.
- [x] Pass auxiliary runtime/Editor compilation and Unity V18 authority validation.

### Phase 111 — Unity MCP tactical and medical regression closure

- [x] Replace category-only medicine fixtures with authored physical medicine stacks.
- [x] Make body health the sole downed/recovered authority and prevent ambulatory rescue-order fabrication.
- [x] Complete rescue commands from the authoritative recovery event.
- [x] Isolate autonomous rescuers and deterministic pointer layout in the PlayMode verifier.
- [x] Pass tactical controls, strict save preflights, physical rescue parenting, bed treatment, recovery hysteresis, command cleanup, and Console Error 0 / Warning 0.
- [x] Capture the verified gameplay region through Unity MCP only; do not use operating-system mouse/keyboard automation.

### Phase 112 — Remaining full V18 program

- [x] Convert captivity state and door-access subjects to strict detached Aggregate restoration with public invalid-preflight preservation proof.
- [x] Restore canonical character registry ownership and guard rescue completion with authoritative body-health state; pass the full Unity MCP tactical/medical regression with Console Error 0 / Warning 0.
- [x] Convert circus orders and captured wildlife to strict combined `500.world.circus` restoration, stage door membership, remove restore-based fixture seeding, and prove public invalid-preflight preservation.
- [x] Convert invasion threat/campaign/policies, active intruders, owner evacuation, and defense engagements to strict V4 restoration through one `550.world.invasion` participant; prove active prefabless candidate cleanup and rollback preservation.
- [x] Convert surgery orders/parts/storage/policies/corpse/anatomy state to strict V5 restoration through `525.world.surgery`; add an opt-in rollback-free section contract and prove failed detached candidates leave JSON and the published root revision unchanged.
- [x] Remove modular-facility live restore and warning/default backdoors; require transaction-only inactive candidates, exact codec/module versions, rollback-free section commits, and replacement-Grid round-trip proof.
- [x] Convert character-world restore to authored-catalog-only detached candidates; require one owner, strict nested state and exact cells/lifecycle, remove preserve-live/nearest/direct-publish/quiescence paths, and prove rollback-free late-failure cleanup plus full V18 round trip.
- [x] Convert wildlife ecosystem, population, raid, and carcass freshness to strict detached candidates; remove default-generation and warning/clamp restore paths, require rollback-free publication, and prove exact patch round trip plus synchronous failed-candidate cleanup.
- [x] Convert exterior-zone restore to strict canonical detached candidates; preserve payload order, require rollback-free synchronous publication/cleanup, and prove exact round trip plus late-failure live-world preservation.
- [x] Convert construction work orders/sites to canonical detached Aggregate and inactive Unity candidates; remove transient worker persistence, require synchronous replacement cleanup and rollback-free publication, and prove normal/invalid/late-failure contracts.
- [x] Convert physical items to strict current-version detached Aggregate restoration; omit transient hauling reservations, preserve durable direct-pickup source state, reject lossy legacy/default payloads, and prove the full 54-section live V18 round trip with zero item diff.
- [x] Convert defense facilities, factions, and grand projects to required typed rollback-free save boundaries with canonical payload validation and Unity regression proof.
- [x] Convert stock policies, regional supply contracts, regular customers, and facility shop state to strict required rollback-free snapshots; prove canonical/invalid and all-marker late-failure behavior.
- [x] Convert staff-discontent state to exact V1 required typed rollback-free restoration; remove trim/clamp/skip fallbacks and prove canonical round-trip plus invalid hierarchy preservation.

#### Phase 112 throughput contract — vertical batches

- Current source contract is fixed at 54 production save sections: 54 strict rollback-free, 0 remaining. Loaded Unity acceptance of all 54 remains pending under Batch D/F and the final root gates.
- Each domain batch is one atomic vertical deliverable. Save state, runtime authority, SO/catalog authority, dependency boundary, class responsibility, UI/error boundary, and tests are changed in the same working set rather than completed as separate internal phases.
- Atomic applies to the execution method, not only the completion checkbox: do not migrate owner 1 to completion and then repeat the same stack for owner 2. Establish each shared seam once, cut the complete owner set through it in the same revision, and remove the superseded paths across the set only after the integrated fixture passes.
- Audit all owners in a batch once, introduce shared contracts/helpers once, cut every owner over, remove all legacy paths, then compile/reload and verify once at the batch boundary. Individual owners or architectural layers are never reported as completed milestones.
- `State/Save`, `SO/Content`, `Runtime/Statics`, `Assembly/Responsibility`, and `Presentation/Verification` are simultaneous exit dimensions, not sub-batches or an execution order. A batch has only `in progress` or `completed` state.
- There are no "completed sibling" owners inside a batch. A failure is diagnosed at the narrowest owner, but the entire batch remains one unaccepted working set and is verified again through the shared boundary after the fix.
- A save section counts as converted only when it is required, exact-version typed, strict-preflighted, rollback-free, free of trim/clamp/skip/default restore fallbacks, and covered by canonical round-trip plus invalid no-mutation proof.
- Update `task_plan.md`, `findings.md`, and `progress.md` at a vertical-batch boundary or on a concrete failure. Report exact counters and named gates, never subjective whole-project percentages.

#### Completed foundation — maintain, do not redo

- [x] V18 new-game-only boundary and manifest compatibility; V17-and-earlier restoration remains rejected.
- [x] Root SO/content catalog authority, authored item definitions, typed persistent IDs, physical item/warehouse/equipment single ownership, scoped session state, and consolidated offense runtime/save Aggregate.
- [x] Runtime content `ScriptableObject.CreateInstance`, policy-free runtime providers, optional required-interface DI, and production `Bind*Runtime(...)` call sites are zero and remain ratcheted.
- [x] `CharacterSummaryInfo`, `FailureCode`/`DomainFailure`, String Tables, and initial combat-equipment failure-code adoption exist; each vertical batch completes adoption for its own domain.

#### Vertical execution batches

- [x] Batch A — core/session and transaction authority.
  - This is one `CoreSession` migration, not six owner migrations collected under one heading. `ExperiencePacing`, `ExternalInfluence`, `RunFlow`, `RunVariable`, `DungeonDebug`, and `ServiceRooms` are components of the same cutover unit and have no independent implementation order, completion state, compile gate, or acceptance result.
  - Establish one shared seam first: the authored `CoreSession` content projection, scoped root/transaction boundary, command/query/result contracts, exact V18 capture/restore manifest, presentation mapping, asmdef references, and composition registration table must describe all six components before any legacy implementation is removed.
  - Perform one synchronized replacement pass across all six components: move engine-independent Aggregate state and contracts to `DungeonStory.CoreSession`, route every persisted mutation/query through the shared seam, register every save participant in the same detached transaction, and delete all six sets of duplicate state/static/direct-save paths. Concrete Unity lifecycle and cross-domain adapters remain at the default edge until their event/item/invasion/building/character ports exist; moving them wholesale is forbidden because it would create a reverse dependency on `Assembly-CSharp`.
  - Keep one executable cutover matrix keyed by the six components and the same seven columns: `Content`, `Runtime state`, `Command/query`, `Save participant`, `Composition`, `Presentation`, and `Legacy removal`. Work may be edited file by file, but testing and acceptance start only when every cell is implemented.
  - Existing save/content/static changes are unaccepted working-set material until assembly ownership, command/query presentation boundaries, localized failures, and cross-owner transaction behavior are implemented in the same revision.
  - One integrated fixture must exercise all six owners through authored lookup, commands/queries, mutation, capture, invalid preflight, detached restore, final-section failure, publication, and presentation mapping. Separate narrow PASS results cannot complete the batch.
  - One boundary verification: Roslyn/reflection/asset graph, auxiliary compile, Unity reload, the integrated six-component fixture, V18 authority, and Console Error 0 / Warning 0 must pass together. Concrete-adapter UI pointer flows remain part of Batch E/F verification after their cross-domain ports exist.
  - Accepted evidence: all six Aggregate states and contracts are owned by `DungeonStory.CoreSession`; duplicate state declarations are absent; content authority, RunVariable state behavior, the six-component detached restore/failure fixture, V18 authority, and architecture ratchets pass in one loaded Unity revision. Metrics are `1093 files / 3287 types / 24 mutable statics / 13 oversized / 96 large constructors / 1054 default sources`, and Console is Error 0 / Warning 0. This completes the batch as one unit; no owner received an independent completion state.

- [ ] Batch B — characters, survival, work, and medical authority.
  - Atomic owner set: `AnimalHusbandry`, `CharacterBodyHealth`, `CharacterConsumables`, `CharacterEnvironment`, `SpeciesRuntime`, `SurvivalResources`, and `DarkSurvival` plus their character/work/medical UI and content.
  - One shared cutover moves authored species/needs/roles/diets/materials, entity state and IDs, seven exact save boundaries, Characters/Work/Survival/Medical asmdefs, confirmed class splits, command/query presenters, and localized failures together. Active inventory and event/skill deduplication become scoped in the same change; names, coordinates, and `GetInstanceID()` remain forbidden persistence keys.
  - One boundary proves all seven owners, `22 → 15`, character-summary/medical pointer flows, static reset, save round trip, V18, and Console 0/0 together.
  - Current accepted evidence: the integrated seven-owner survival fixture, Dark Survival PlayMode report, V18 authority, architecture ratchets, and Console Error 0 / Warning 0 pass together. Deprivation consecutive-run isolation, typed Husbandry failure/status presentation, narrow CharacterSummary consumables contracts, and anatomy plus surgery DTO/SO ownership in `DungeonStory.Medical` are covered.
  - The Unity EventSystem pointer matrix passes at `1600x900` and `900x1600`, including CharacterSummary close/reopen, health tab, automatic emergency surgery toggle/restore, surgery modal/footer flow, bounds, labels, four captures, and captured Error/Warning 0.
  - The stale medical residuals are closed: `ICharacterBodyHealthRuntime`, `ISurgeryRuntime`, and `ICharacterMedicalRuntime` are absent; body health uses Query/Command/Persistence facets; surgery and medical save typed statuses; the surgery UI has no by-reference string result; and all 280 required shared/ko/en String Table keys exist. The remaining Batch B exit item is concrete Characters/Work/Survival/Medical Unity-adapter ownership, which is part of the Batch E default-assembly cutover rather than another medical state migration.

- [ ] Batch C — production, facilities, automation, and world-resource authority.
  - Atomic owner set: `AutomationInfrastructure`, `ConveyorInfrastructure`, `FluidInfrastructure`, `PowerInfrastructure`, `ProductionBills`, `WasteProcessing`, and `EnvironmentalField` plus their buildings, resources, routing UI, and root content graph.
  - One shared cutover moves recipes/archetypes/capabilities/supplies/buffers/routes/branches, scoped facility/world state, seven exact save boundaries, Buildings/Production/Automation/World/Wildlife asmdefs, confirmed runtime splits, passive production presenters, and localized failures together while preserving physical items and transactional transfer as the only stock authority.
  - One boundary proves all seven owners, `15 → 8`, dependency depth/branching, buffer/backpressure/fair routing, responsive pointer flows, save round trip, V18, and Console 0/0 together.
  - Accepted UI evidence: Unity MCP/EventSystem pointer flows pass at `1600x900` and `900x1600`; both captures keep the complete building/context surfaces and all three route rows in bounds, including the third row; stock-sensor physical install preserves the existing RepeatForever bill before explicit MaintainStock conversion; route policy edits and `WaitingForOutputSpace` pass; the report proves UTF-8 Korean round-trip and captured Console 0/0. `ProductionBuildingPanelPresenter` is 770 lines and `UIBuildingInfo` is 783 lines.
  - Live routing source is now connected: recipe/construction/equipment/surgery demand providers populate `currentDemand`, `reservedQuantity`, and `blockedReason`; the output-buffer path calls `ProductionDistributionPlanner.SelectNext`, physically transfers the selected stack, and implements demand/minimum/target/warehouse/overflow/local fallback with strict, weighted, ratio, blocked-skip, reservation-cap, and starvation-aging behavior. Batch C still requires the integrated Unity-loaded scenario and Console 0/0 against the merged source revision before acceptance.

- [ ] Batch D — combat, equipment, economy, research, progression, and offense authority.
  - Atomic owner set: `CropPlot`, `WorldResource`, `TreasuryEconomy`, `CombatEquipment`, `EquipmentEvolution`, `OffenseAggregate`, `BlueprintResearch`, and `MetaProgression` plus their catalogs and screens.
  - One shared cutover moves equipment/module/lineage/research/reward/economy/world/offense/progression content, money/ID/cover/strategy state, eight exact save boundaries, Combat/Economy/Research/Offense/Captivity/Defense/Invasion asmdefs, confirmed class splits, command/query screens, and localized failures together; duplicate authority for any concept must be zero.
  - One boundary proves all eight owners, `8 → 0`, the complete 54-section round trip, equipment loss/lineage, 168 research, economy/defense/offense pointer flows, V18, and Console 0/0 together.
  - Current source state: all eight owners—Research V5, CombatEquipment V6, EquipmentEvolution V3, MetaProgression V1, CropPlot V2, WorldResource V2, TreasuryEconomy V3, and OffenseAggregate V2—use exact-current strict detached candidates and rollback-free boundaries. The source ratchet now requires `54/54` with an empty remaining set.
  - The all-marker registry path skips rollback-image capture, and its failed-final-commit fixture proves zero additional section captures plus zero published-root mutation. Batch D remains unaccepted until Unity loads all 54 types, canonical/invalid/late-failure scenarios pass in one revision, and Console is Error 0 / Warning 0.

- [ ] Batch E — cross-domain edges and composition closure.
  - Move Presentation to query/command contracts, Unity adapters and save/content loaders to Infrastructure, and all construction to Composition roots. Split the six Batch A concrete adapters only after their event/item/invasion/building/character ports have entered named contract assemblies; never make a named assembly depend on `Assembly-CSharp` to force an early move.
  - **Superseded by Phase 117:** do not reduce every default-assembly
    MonoScript to zero. Reduce unapproved domain authorities and cross-domain
    cyclic-boundary violations to zero while retaining reviewed Unity-edge
    adapters; asmdef cycles and reverse Presentation/Infrastructure references
    must still be zero.
  - Enforce executable gates: optional required-interface DI `0`, production `Bind*Runtime` `0`, authored mutable runtime statics `0`, direct `GameData` mutation `0`, invalid persistence keys `0`, runtime classes `≤1200`, MonoBehaviour/Presenter classes `≤800`, constructor dependencies `≤8`, architecture waivers `0`.
  - Finish String Table coverage for every non-`None` failure code, remove duplicate UI literals, normalize broken encoding, and rerun the complete root catalog/asset graph with runtime SO synthesis, gameplay `Resources.LoadAll`, code-owned mutable catalogs, destructive Editor builders, duplicate IDs, and broken references all at `0`.
  - The Roslyn constructor gate now measures operational DI owners rather than value/DTO field constructors. Its current actionable count is 32; the current default-assembly source count is 1,058 while the first CoreSession runtime leaf migration is active. Neither count is accepted as a new baseline yet.
  - Current merged source closes the operational constructor gate at `0` without waivers. Mutable runtime statics, content escapes, and direct session mutations also remain `0`. The remaining exit work is the Phase 117 ownership classification, typed-ID retirement of numeric compatibility consumers, and localization/encoding cleanup.
  - Current Roslyn evidence also closes the real oversized-type set at `0`. Grid, Economy content, Character mood/needs, Buildings leaf contracts, AI/Characters/Work leaves, and the first Combat contract are now in named assemblies. The semantic planner is deterministic with no missing metadata references. Historical default-file counts remain trend data only; Phase 117 owns the remaining risk-classified cutover.

- [ ] Batch F — integrated gameplay, save, and UI proof.
  - Execute V18 manifest/header/section/reference validation, full detached staging, injected late-failure no-mutation, repeated new game/save/load/scene transitions, stable typed IDs, physical stock/equipment/lineage/expedition loss, money ledger, and static-state leak checks as one clean-run matrix.
  - Run all research, production branching/buffers/supplies, facilities, combat/equipment/modules/lineage, medical, defense, wildlife, offense, and economy regressions.
  - Verify `1600×900` and `900×1600` through Unity MCP captures only; never use operating-system mouse/keyboard automation.
  - Regenerate `OVER_SEPARATION_AUDIT.md`, `task_plan.md`, `findings.md`, and `progress.md` from the same validator output and finish Unity Console Error `0` / Warning `0`.

#### Phase 117 risk-based Blueprint research cut

- [x] Preserve the `BlueprintResearchRuntime` public/serialized compatibility surface while moving Foundation root/event/debug composition to an application adapter.
- [x] Keep named Research ownership of queue, progress, dependency, and work rules; add the final node lock/unlock decision matrix there.
- [x] Leave Research V4/V5 DTOs, `requiredWorkAtCapture`, 168-node completion semantics, and restore order unchanged.
- [x] Pass Unity compile/execution, node-state and work-ratio probes, fresh analyzer target candidate `0`, asmdef cycle `0`, unique GUID, scoped diff, and Console Error `0` / Warning `0`.

#### Phase 117 risk-based Exterior incident authority cut

- [x] Replace runtime-list plus marker dual countdowns with one named Exterior incident Aggregate.
- [x] Make marker incident state projection-only; remove marker ticking, self-resolution, and save-data production.
- [x] Route handler time/stage changes, query, overview, capture, history trimming, and restore publication through the Aggregate without changing the V18 DTO/section/version/order.
- [x] Add deterministic Aggregate and handler query/capture/restore agreement regressions; pass named compile, standalone probe, target candidate `0`, oversized `0`, asmdef cycle `0`, GUID/meta, and scoped diff gates.
- [ ] Rerun the loaded PlayMode regression and Console 0/0 in the root integration gate after Unity MCP approval is restored.

#### Phase 117 risk-based operating-day settlement cut

- [x] Move revenue, visits, stock, incidents, debt, shortfall, report history, and settlement idempotence into one named Operation Aggregate/domain service.
- [x] Preserve the original MonoScript GUID/type through a fieldless facade and leave Unity snapshots, economy ports, alerts, and event publication in an application adapter.
- [x] Preserve OperatingDay save DTOs, section ID/version, canonical order, validation, detached prepare, and single-pointer publication without editing save sources.
- [x] Add duplicate day-start/end and money-ledger one-time regressions; pass the standalone domain harness, focused named/default compilation, target candidate `0`, asmdef cycle `0`, GUID/meta, and scoped diff gates.
- [ ] Run the loaded operating-day debug scenarios and final Console 0/0 in the root integration gate after concurrent source lanes settle.

#### Phase 117 risk-based Experience pacing authority cut

- [x] Move current day, rehearsal masks/active day, and introduced concepts into one named CoreSession Aggregate with monotonic/idempotent transitions.
- [x] Isolate Foundation event subscription and authored Content rule lookup in a recognized application adapter; leave the runtime with no direct state writes or cross-domain references.
- [x] Preserve the frozen V18 pacing section ID, DTO version, restore phase/dependency, detached preparation, and single publication while isolating the strict save adapter.
- [x] Add monotonic day, duplicate transition, concept uniqueness, mask/day/active invariants, exact capture/prepare/publish round-trip, and invalid candidate regressions.
- [x] Pass named/focused/Editor compilation, standalone transition/save probe, target candidate `0`, oversized `0`, asmdef cycle `0`, GUID/meta, and scoped whitespace/diff gates.
- [ ] Run the loaded pacing scenario and final Console 0/0 in the root integration gate after Unity MCP approval is restored.

#### Phase 117 final-acceptance runner coverage audit

- [x] Map every synchronous final-runner step to V18/54, content, item/equipment, production/supply, research, CoreSession, combat/medical/survival, and implemented-gameplay completion contracts.
- [x] Add the missing callable synchronous entries for runtime composition, OperatingDay authority, strategic physical expedition, expedition journey/architecture, and Offense aggregate V18.
- [x] Keep the report at `Artifacts/QA/final-acceptance-report.txt` and label PlayMode UI/resolution captures plus Console 0/0 as a deferred external Unity MCP gate.
- [x] Pass focused Assembly-CSharp-Editor compilation, unique runner GUID/meta, scoped diff, and trailing-whitespace checks.
- [x] Add callable regression evidence for equipment lineage transfer, expedition equipment/module co-loss, and firearm smoke/misfire plus bow/crossbow/gun role balance; retain the live full-world 54-section round trip with run/scene isolation for the loaded Unity gate.
- [x] Keep canonical shop-category drift under `Batch A content authority` and stable `(Kind, Id)` circus identity under `Implemented gameplay scenarios`; the synchronous runner remains 33 top-level steps.
- [ ] Run the loaded 33-step runner after the merged Unity refresh and require both nested shop-category and circus-identity contracts to pass before accepting the synchronous gate.
- [ ] Run the separate Unity MCP `1600x900` and `900x1600` pointer/capture matrix and final Console Error 0 / Warning 0 after project approval is restored.

#### Phase 117 risk-based Dungeon run-flow authority cut

- [x] Move day, phase, outcome, recurring-boss scheduling, and terminal transition decisions into the pure named `DungeonRunFlowReducer` with ordered effects.
- [x] Preserve the original `DungeonRunFlowRuntime` GUID/type as a fieldless compatibility facade and isolate pacing, invasion, owner, alert, and restore projection in `DungeonRunFlowApplicationAdapter`.
- [x] Preserve the frozen root V18/run-flow V2 strict save contract, section ID, restore phase/dependencies, detached candidate preparation, and single publication.
- [x] Add duplicate day-10 rehearsal, day-40/day-50 boss schedule, boss start/resolution, truth completion, deterministic sequence, and existing save round-trip regressions.
- [x] Pass named/default/Editor focused compilation, standalone reducer harness, fresh analyzer target candidate `0` and hard gates `0`, oversized `0`, asmdef cycle `0`, unique GUID/meta, save-contract, encoding, and scoped diff gates.
- [ ] Run the loaded RunFlow PlayMode regression and final Console Error 0 / Warning 0 in the root integration gate after concurrent source lanes settle.

#### Phase 118 equipment/expedition UI evidence matrix

- [x] Add a dedicated final coordinator target for equipment and expedition UI instead of treating unrelated responsive screenshots as evidence.
- [x] Exercise equipment appraisal, restoration, rune tuning, installation, removal, and lineage source/target/seal/confirm through Unity EventSystem pointer events.
- [x] Exercise a live expedition journey action through Unity EventSystem and require the phase/node state to change.
- [x] Require equipment and expedition captures at both `1600x900` and `900x1600`; the current final contract is seven targets and 30 captures.
- [x] Restore seeded physical-item and combat-equipment runtime snapshots and retain the final coordinator persistence snapshot boundary.
- [x] Capture and canonically restore the research and offense Aggregate save sections for every resolution row and final cleanup, including standalone runs.
- [x] Clear verifier-owned expedition and battle state after each offense baseline restore so the two resolution rows cannot accumulate journey progress.
- [x] Preflight every scene path required by the whole suite before state/persistence capture, then recheck actual `OpenSceneMode.Single` transitions; preserve the clean seven-target flow and never save/discard user scene changes automatically.
- [x] Replace the arbitrary equipment forge surface with authored RF42/RF43/RF44/I17/I18 facility panels, require facility-local physical delivery, and reject progression commands on S08 or the wrong dedicated facility.
- [x] Prove standalone module-stack absorption on install, same-instance facility-buffer rematerialization on removal, and I18-local source/target/seal lineage confirmation and work application.
- [x] Make detached modules authored unique physical items with strict save/restore linkage; reject detached/attached duplication, mark destructive removal as Lost, and keep installation absorption non-destructive.
- [x] Remove all facility-less progression command callers and pass fresh Foundation, Items, Combat, Runtime, Editor, ArchitectureMetrics, asset-meta, GUID, and scoped diff gates.
- [x] Require the authored facility-flow marker in the final coordinator without changing the seven-target/30-capture contract.
- [ ] Run the loaded seven-target matrix in Unity and require 30 fresh captures plus Console Error 0 / Warning 0.

#### Phase 117 final offline integration audit

- [x] Rerun fresh ArchitectureMetrics after all visible source-lane changes and confirm every hard gate plus global cross-domain candidate count is `0`.
- [x] Confirm the 49-asmdef graph has zero cycles, all C# sources have metas, and all 6,817 asset GUID records are unique.
- [x] Prove the 33-step final runner reaches the physical lineage transfer, expedition-death equipment/module co-loss, and gunpowder smoke/misfire/ranged-role regressions through its existing scenario calls.
- [x] Pass the focused Assembly-CSharp-Editor runner compilation and scoped source/document whitespace checks.
- [ ] Regenerate stale Unity/Bee assembly artifacts, run loaded synchronous/PlayMode acceptance, and finish Console Error 0 / Warning 0 in the root Unity MCP gate.
- [x] Preserve the pre-existing Unity-serialized trailing whitespace reported by global `git diff --check` (`1,502` lines across `32` files). Bulk normalization would rewrite unrelated user-owned scene/prefab data; the final-audit source/doc scope itself is clean.

#### Phase 117 acceptance evidence-gap closure

- [x] Execute physical lineage transfer through its real queue/work/physical-item authority and verify source/seal consumption plus target history and property/module preservation.
- [x] Execute expedition death through `OffenseExpeditionReturnPort` and verify unique equipment and installed module co-loss plus loadout removal.
- [x] Separate gunpowder smoke from suppression, apply hit/miss/misfire smoke exactly once at the shared resolver boundary, and prove authored bow/crossbow/gun combat roles through actual resolution/preview/timing APIs.
- [x] Add a fresh-request PlayMode facade for the actual 54-section full-world round trip, pre-composition warning/error capture, canonical baseline restoration proof, and EditMode return.
- [x] Replace the invalid legacy owner-doctrine fallback fixture with strict current-version rejection and canonical live-state non-mutation proof.
- [x] Pass named Combat, focused smoke consumer, isolated full-world facade, GUID/meta, and scoped diff gates.
- [ ] After Unity refreshes stale Bee artifacts, run the loaded synchronous runner and `DungeonFullWorldRoundTripPlayModeFacade`, require fresh `RESULT=PASS`, and finish Console Error 0 / Warning 0.
