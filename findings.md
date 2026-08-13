# DungeonStory Current Findings

## 2026-08-13 queue-aware fallback rerun findings

- The same five-day live verifier now passes after replacing the fixed primitive-survival cutoff with projected need at the earliest queueable authored service ETA.
- Primitive latrine use fell from one false fallback to zero; typed meal execution failures fell from one to zero; harmful stalls, no-action failures and repeated execution failures are all zero.
- Actual labor increased from `52.524` to `62.485 WU/actor-day` (+18.96%) and output-equivalent labor increased from `48.190` to `57.321 WU/actor-day`. This confirms that the prior loss was AI arbitration/queue behavior, not a balance target that should be preserved.
- The five-day time budget is now `80.977s` active work, `27.940s` work transit, `0.449s` work queue, `36.020s` need service, `23.921s` need travel, `0.676s` need queue, `2.958s` other travel and `7.059s` idle/other per actor-day. Remaining non-work time is mostly authored service and travel rather than hidden AI failure.
- Food and water authority remained conserved: 15 physical meals/15 events and 16 physical water units/16 events; active meal plans ended at zero.
- The broad priority and customer/facility suites both pass. Their earlier order-dependent failures were test-fixture defects: a permissive room policy returned a null operational profile, and an assertion counted character-initialization replans instead of measuring the scenario delta.
- A stale editor-created `qa-destroy-ref` facility was found in `GameplayScene` by the scene leak validator and removed through Unity MCP. Console startup must be rechecked before closure.

## 2026-08-13 five-day queue/fallback findings

- The latest real five-day run completed 900 game seconds with three live founders, zero captured Console issues, zero harmful no-progress episodes, and `52.524 actual WU/actor-day`; it is not a pass because one primitive latrine fallback executed while an authored toilet was reachable.
- The primitive trace is causal: at game time `426.535`, Leon had excretion `14.308`; the toilet was on the same cell, structurally queueable, capacity 1, and occupied by one user. The fixed `emergencyStart - 10` rule treated this short occupancy as a missing facility and ran the primitive action even though the authored toilet became free before completion.
- Primitive fallback must therefore compare projected need after real service ETA, not a fixed need threshold. The ETA now includes estimated travel, active users, reservations ahead, effective capacity, authored use duration, and the actor stay-duration multiplier; timed need loss comes from the same `CharacterNeedStateService` authority as runtime decay.
- The five-day report also retained one typed `ConsumptionFailed` meal execution failure and recovered without repetition (`repeatedPeak=1`). The final state still showed `PhysicalConsumptionFailed` detail, so meal-plan contention remains a diagnostic/follow-up item even though physical stock/event totals stayed conserved.
- Focused customer AI regression initially failed because the delayed-purchase test depended on optional authored shop inventory. The transaction guard itself was correct; replacing the fixture with an isolated synthetic `RemainStock` made the action-replacement assertion deterministic and the full customer suite passed.
- A broad priority-corner suite invoked immediately after the customer suite exposed order-dependent fixture failures (`P1_RestRoom` admission rejected before first yield and destroyed-destination selection false). This is separate from the queue-ETA change and must be isolated before accepting the broad suite as evidence.

## 2026-08-13 AI runtime intersection findings

- The short isolated AI suites were already green, so the remaining failures are state-transition intersections rather than basic scoring: target destruction, action replacement, coroutine resumption, reservation invalidation, and long-run retries.
- `RunSelectedAction` discarded structured `AIActionFailure` data and returned one generic sentence. This made different causes look identical in reports and made the repeated-failure counters less actionable.
- `Facility.Linger` correctly stopped when the action token changed, but the outer coroutine continued after `yield return Linger(...)`. That allowed an obsolete interaction to progress into recovery, payment, completion, or occupancy cleanup.
- A facility can be destroyed between coroutine yields. Any post-yield Unity object access must be guarded; presentation labels also need capture before the first yield.
- The repeated-failure streak was reset by `NotifyActionStarted`, so a start-fail-replan-start loop never appeared as repeated failure. The streak now uses action+failure identity and a bounded time window instead.
- `SetVisitOutcome` accepts a null target for teardown cleanup, but the former abort path only called it while the facility was still alive. Destruction during an interaction could therefore leave the shopping visitor state at `InProgress`; abort now clears it to `Abandoned` even when the Unity target has already been destroyed.
- Action replacement is not itself an execution failure. The obsolete coroutine releases only its own facility occupancy and records abandonment; it must not request a replan that would erase the already-selected replacement action.
- Facility resource acquisition was ordered too early: water was consumed before movement, seat settling and service waiting. A later target destruction or action replacement could therefore spend clean water without applying the facility effect. Water/drain validation and consumption need to occur at the physical commit boundary after the last interruptible wait.
- Facility admission, service-session, plumbing, recreational-consumption and meal-consumption failures currently create activity facts but do not all reach `AIBrain` execution diagnostics. A five-day report can therefore claim zero execution failures while visible facility attempts repeatedly fail.
- `ShopCustomerInteractionService` has the same outer-coroutine continuation defect as `Facility`: movement/linger/checkout/purchase coroutines can stop internally when the action token changes, while the caller proceeds into cart, payment, stock decrement and revenue. Purchase/payment commit also happens after an internal delay without revalidating the owning action token.

## 2026-08-13 AI conflict and performance findings

- Yes: the roughly 20 WU result was primarily an AI/runtime conflict result, not the neutral-adult labor balance. Corrected five-day samples currently span 54.721-62.944 actual WU/adult-day. The old authored 99 WU assumption is also not validated by live play and must not be restored as the target merely because the conflicts were fixed.
- Five-day physical authority is coherent: 16 food and 16 water items were removed for 16 meal and 16 water-consumption events; active meal plans ended at zero; no primitive fallback was used while authored facilities were available.
- The latest five-day run spends, per actor-day, 71.278 s active work, 32.266 s work transit, 0.357 s work queue, 34.413 s need service, 32.080 s need travel, 0.208 s need queue, 3.028 s other travel and 6.370 s idle/other. The next balance pass should reduce excessive need travel through layout/technology rather than fabricate more work time.
- The 500-NPC CPU failure was not A*. The dominant cost was repeated editor-scene manager discovery beneath `GetNowXY`, which inflated world proximity and destination resolution. After explicit stress-grid binding, world proximity averages about 0.03 ms and destination resolution about 0.14 ms.
- A permanent editor grid cache is invalid because focused scenarios create and destroy multiple grids in the same frame. The correct test architecture is an explicit disposable override for long-lived stress worlds while ordinary fixtures retain priority-based discovery.
- Editor-wide GC is not an AI-owned allocation authority. The corrected profile reports scheduler-owned GC separately and writes `gcPassAuthority=ai-scheduler-thread`; Player-build whole-frame GC remains required for release evidence.
- Scene teardown can resume an interaction coroutine after its Unity facility object is destroyed. Values needed for completion diagnostics must be captured before the first yield, and physical commits must remain guarded by action and lease authority.

## 2026-08-13 same-floor five-day facility and meal-authority findings

- A facility meal buffer containing valid physical food was incorrectly reported as `DeliveryPending` whenever the shared exact-path broker deferred the query. Physical buffer availability is now independent of transient route-budget state; movement is still revalidated during selection and meal execution. A focused zero-budget regression passes.
- The former daily-routine fixture placed facilities on grid `y=0` and `y=2`, which are dungeon floors rather than visual rows. Its 30-cell need paths were therefore verifier-created stair travel, not an AI detour. The revised fixture keeps all needs on one floor and uses independent occupancy layers on one walkable service tile because the starter map has only 2-4 empty lateral building cells per floor.
- The valid five-day run records 62.944 actual WU/adult-day, physical meals/drinks 1.067/1.067 per adult-day, toilet/hygiene 0.8/0.933, primitive fallback 0, execution failures 0, no-action failures 0 and harmful stalls 0. Need travel falls to 24.756 seconds/adult-day and is fully attributed by need and path segment.
- The remaining recreation verifier failure (`0.533`) counted only visits selected through the Leisure branch. The authored sofa supports both Rest and Entertainment and always applies `fun +8`; a Rest-selected sofa visit is therefore a real recreation recovery. Cadence validation must count completed facilities whose authored recovery changes FUN, not the transient AI branch label.
- Project progress uses float while central labor uses milli-WU. The valid run differed by 0.014 WU over 854 WU; the causal audit tolerance is now 0.05 WU to cover representation quantization without hiding gameplay-scale loss.
- Meal-operation abort diagnostics now retain a bounded typed failure code/detail and carry it through the building visitor snapshot into the activity fact instead of collapsing every failure to `PhysicalConsumptionFailed`.

## 2026-08-13 AI wait-versus-survival diagnostic gap

- The long-run stalls are real observations: actors remained in `Wait` / short-chat cycles for 12-39 seconds while hunger or excretion was already below its authored routine threshold.
- Action execution failure counters were zero, but that does not prove food/toilet candidates were valid. `CharacterAiDecisionPipeline.TrySelectAndRunHierarchicalJobGiver` silently continues when a higher-ranked `CharacterAiJobGiver.TryEvaluate` rejects a candidate; this rejection is not currently included in the bounded runtime snapshot.
- `WaitJobGiver` and `WorkJobGiver` omit thirst from their strongest/survival pressure calculations. This independent priority defect can allow idle/work utility to remain too high while drinking is due.
- Stall evidence contains branch/action/phase/destination and the last committed action failure, but not the blackboard's selected utility, decision route, mood impulse or macro goal. Those fields are needed to distinguish unavailable survival facilities from mood bias, stale selection and reservation contention.
- The game-AI invariant is that a multi-frame action must expose continuing progress, and a lower-priority idle choice must not hide an unavailable higher-priority survival action. Unavailability remains a typed rejection rather than silently becoming social idle.
- The thirst omission spanned three live consumers: `CharacterAiDecisionContext.GetPriorityScore(Drink)`, `WorkJobGiver` survival pressure and `WaitJobGiver` strongest-need pressure. All three now use the same thirst authority.
- When a survival routine need is due but the selected action is still `Wait`, both the explicit wait action and ambient idle path now use static waiting with a `survival need wait` phase/detail. They no longer present short chat or wandering as if the need did not exist; the next decision still re-evaluates the typed survival candidates.
- Hierarchical and detailed job-giver selection now count evaluation rejections separately from committed execution failures. The fixed branch/failure matrix exposes cases such as `Eat/DestinationOccupied` and `Drink/PathSearchDeferred` without accumulating an unbounded event log.
- The priority corner-case suite also exposed a stale test fixture: reflection wrote a removed legacy shop `stocks` field, so the supposedly valid restock candidate did not exist. Switching the fixture to the public physical-stock debug command restored the intended Priority1 restock versus Priority2 repair comparison.
- The isolated five-day rerun proved the deeper cause of the long waits. `AIBrainCandidateSelector` incremented its processed-work counter for every action skipped by a JobGiver predicate. A destinationless `Wait` action late in the shared action array therefore exhausted the slice while merely skipping unrelated actions and returned `PathSearchDeferred` 94-99 times per actor. This was not a pathfinding failure.
- The same selector defect multiplied all JobGiver scans: 526-794 evaluation rejections per actor, 133-213 same-action restarts and 247-312 seconds in Wait. Actual labor fell to 23.041 WU/actor-day while the new evidence correctly retained the failure rather than hiding it.
- Predicate filtering now advances without consuming the expensive-evaluation slice. Only matching candidates that execute prepare/path/reservation/score work count against the budget. Priority, naturalness and full plan-AI regressions pass after this change.
- The second isolated run showed the first selector correction was incomplete: when the budget expired immediately after evaluating the matching action, unrelated trailing actions still left `NextActionIndex < actions.Length`, so the continuation remained pending. Actor-specific Wait deferrals (32/0/92) exposed this action-array-order dependency.
- The selector now skips trailing nonmatching actions before deciding whether a continuation is actually pending. A continuation survives only when another action matching the same JobGiver predicate remains to be evaluated.
- The third isolated run proves the selector repair: `Wait/PathSearchDeferred` is zero for every founder, no founder enters deprivation breakdown, and actual labor rises to `27.612 WU/actor-day`. The run still fails with 5/5/8 harmful stalls and recreation `0.467/day`, so the selector fix is necessary but not sufficient.
- A second concrete arbitration defect was then found in `BuildEmergencyJobGivers`: aggregate emergency entry could be caused by thirst, but the emergency candidate list omitted Drink and always included Wait. Once safe-drink startup was temporarily unavailable, Wait returned a handled decision and prevented the routine tree from evaluating Drink or any other ordinary need.
- Emergency selection now includes Drink at its authored emergency threshold and contains no terminal Wait fallback. If no emergency candidate can execute, the root decision falls through to routine utility and finally to the explicit observable idle path instead of falsely reporting emergency success.
- Current rejection evidence previously retained only the last evaluated branch, usually Work. Per-branch revision-stamped rejection state now preserves the reason for the actual urgent need without per-tick array clearing or an unbounded log.
- `CharacterBlackboard.ClearMacroGoal` was overwriting the current BT status with `Macro goal expired`, making live Wait stalls appear to be macro decisions. Macro expiration is now appended only to the bounded detailed trace; the current decision route remains owned by the decision that actually ran.

## 2026-08-13 five-day parallel-work recovery and diagnostics gap

- Unity's Console was insufficient compile evidence: it remained empty while `Editor.log` contained `CS0019` for `CharacterId == CharacterId`, which prevented the editor test assembly and menu from loading. Stable IDs implement `IEquatable<CharacterId>` but no equality operator; the live comparison must use `.Equals`.
- After fixing that compiler defect, the cross-adapter construction reservation regression passed through Unity MCP and the isolated five-day run reached `peakActive=3`, `peakEffective=2.60`, exact food/water physical accounting and zero final external-intent collision counters.
- The recovered run measures `29.119 actual WU/actor-day`, but it still fails recreation cadence (`0.4` against `0.6~1.4`). Per actor-day, `36.508s` is need travel, `24.556s` other travel and `39.097s` idle/other. These channels cannot yet distinguish legitimate waiting from oscillation or a hidden action failure.
- `AIBrain` currently exposes only the final action/phase/failure plus a short 24-entry memory. A 900-second verification can therefore erase the causal sequence that made the actor stall. Central hooks already exist at action start, phase change, replan, committed failure, no-action failure and candidate rejection; bounded cumulative counters and a no-progress observer can add evidence without retaining unbounded strings.
- The deterministic five-day run was still allowing `AiDirectorRuntime` to enqueue external local-LLM mood impulses. Those outputs are explicitly narrative-only, yet an unavailable host produced repeated request timeouts during the labor audit. This adds nondeterministic load and noisy diagnostics without contributing to work choice; the benchmark should suspend the director rather than suppress or reinterpret the failures.

## 2026-08-12 Phase 157 technology/founder WU findings

- The founder industry sampler now exposes distributions rather than only means. The no-reroll natural 3-person essential-industry range is 266.508/272.746/279.774 WU/day at p10/median/p90; compromise selection raises the median to 277.266 and upper-20 selection to 282.985.
- Applying the authoritative routine budgets changes the natural median essential-industry physical WU from 272.746 at no research to 352.251 at Endless. Applying process conversion and three members' domain automation changes the same median output-equivalent value to 546.377.
- Population and efficiency multiply as expected: checkpoint settlement output is 297.000 WU/day at Day 1, 5,041.278 at Day 400 and 12,658.420 at Day 960, or x1/x16.974/x42.621. The approved project curves prevent this total from entering one target: major research is 2.40 effective workers and a landmark is 5.00.
- `SettlementLaborBalanceRules.EvaluateTechnologyDailyRoutine` remains a balance/audit target, not a hidden global runtime buff. The deterministic report therefore does not invent shared maintenance, accident, spoilage or emergency-reserve deductions without a live settlement snapshot.
- Closure still needs a live PlayMode day trace proving that actual need decay, facility recovery, food selection, queues, travel and accepted work reproduce the authored routine bands. The current deterministic report is necessary distribution evidence but not that live proof.

## 2026-08-12 Phase 157 construction-project findings

- The ordinary project cap/contribution implementation is live rather than descriptive. A physical construction request withdrew two lumber from canonical warehouse stock, delivered it to the site buffer, transitioned the order to Ready and let three real founders contribute exactly 1.00/0.85/0.75 WU in one focused PlayMode vertical.
- `ItemQuantityReservationService` leases default to 15 game seconds and cap renewal at 45 seconds. `AbilityHaul` previously reserved a plan without renewing it, so a moving actor could reach the source after expiry and fail `Revalidate` at pickup. Hauling now renews each unique plan Lease immediately through `IItemTransferService`; this is a production runtime fix, not verifier accommodation.
- Storage identity is authoritative through `WarehouseStorageIdentity.RequireDestinationId(warehouse)`. The verifier's old `warehouse:{grid}:{x}:{y}` string produced stock that was physically present but invisible to the live warehouse query.
- Focused evidence is `Artifacts/QA/construction-project-playmode-report.txt`: maximum/automatic workers 4/4, active workers 3, effective workers/rate 2.60/2.60, accepted work 2.60, captured Warning/Error 0/0 and `RESULT=PASS`.
- The broad logistics verifier remains unsuitable as closure evidence until its unrelated legacy category-stock, repair and expedition assumptions are updated. Passing the focused construction vertical closes only the Phase 157 ordinary project-cap item.

## 2026-08-12 Phase 156 Unity MCP performance closure findings

- Unity restarted successfully, but the original MCP client session had stopped after a domain reload. Registering a fresh `relay_win.exe --mcp` session through Unity's own local MCP relay restored all 20 Editor tools; every subsequent compile, console, PlayMode and dynamic audit action used that MCP session.
- The largest allocation defect was not the quantity-lease ledger. `CharacterBodyHealthRuntime.Tick` evaluated the complete mana-recovery performance graph every frame for every healthy actor even when mana was full. Evaluating only while recovery is blocked or mana is below maximum reduced the normal x1 profile from 3,881.6 KB/frame average and 9,852.4 KB p95 to 309.4 KB/frame and 373.9 KB. The V27 live consumer audit still passed all 11 formulas, including mana recovery.
- The remaining official-soak p95 spike came from `FirstRunObjectiveRuntime`: it captured the full offense campaign every 0.25 seconds even when Resolve would return at an earlier onboarding milestone. Staging that capture behind the exact resolver gates reduced the final soak's Editor increment from 479.1 KB average / 2,518.0 KB p95 to 280.0 KB / 281.2 KB.
- Final Unity MCP release soak: frame p95 42.81 ms, retained Mono +19.40 MB, Editor burst average/max 1,011.6/70,669.6 KB, save reload 138 buildings/3 characters, captured Error/Warning 0/0, `RESULT=PASS`. The fixed 512 KB average / 2 MB p95 Editor budgets were not relaxed.
- The pointer-driven first-run verifier separately failed because its virtual shop click did not create the purchased physical blueprint; its initial and post-owner objective checks passed and Console remained 0/0. It was not repeated because the user prohibited mouse-input validation. A mouse-free Unity MCP contract exhaustively checked all 512 milestone combinations and proved the new offense-capture predicate exactly matches the resolver's offense-reaching states.
- Editor regression evidence is complete. Final performance authority still requires a standalone Player build run against the absolute 32 KB average / 128 KB p95 / 2 MB maximum limits.

## 2026-08-12 GC criterion correction

- 기존 release soak의 `avg GC <= 2 MB/frame`은 Editor 자체 할당, 4K UI, 검증기 관찰, 실제 게임 시뮬레이션을 구분하지 않는다. 이 값이 4.9 MB라는 사실만으로 물류 회귀라고 판정할 수 없고, 5 MB로 상향해서도 안 된다.
- 동일 Editor·해상도·월드에서 정지 baseline을 먼저 측정하고 활성값과의 차이를 회귀 게이트로 쓰는 것이 올바른 비교다. 절대값은 폭주 방지용 느슨한 guard로만 남긴다.
- 실제 출시 합격 권위는 Player 빌드의 steady-state 평균/p95/max와 장기 잔류 heap이다. Editor 증분은 빠른 회귀 탐지용이며 Player 기준을 대체하지 않는다.
- 공용 성능 보고서의 기존 GC 판정은 평균 64 KB만 확인하고 p95·최대값을 무시했으며, 잔류 Mono 기준도 16 MB로 별도 복제돼 있었다. 이를 단일 정책으로 치환하고 Editor baseline 표본 수까지 명시적 판정 조건으로 추가했다.
- 저장·로드, 명시적 대량 집약처럼 비주기 작업의 일시 할당은 steady-state 최대 프레임 기준에 섞으면 안 된다. 해당 작업은 완료 후 강제 수집 기준 잔류 Mono 64 MB와 Lease·물리 스택·Intent 장기 누적 0건을 별도 권위로 사용한다.
- Unity 프로파일러 raw capture는 이전에 native buffer에서 Editor를 종료시켰으므로 같은 설정을 반복하지 않는다. 기본 `ProfilerRecorder`의 GC counter만 쓰는 baseline/active 비교를 먼저 수행한다.

## 2026-08-12 Phase 156 final findings

- 수량 예약은 스택 잠금 bool이 아니라 `TotalQuantity - ReservedQuantity = AvailableQuantity` 권위로 동작한다. 예약 시 물리 분할이 없고, 실제 픽업에서만 자식이 생기며, Meal/ProductionInput 버퍼 도착 시 Lease Slice를 canonical stack으로 원자 재지정한다.
- 직접 소비·시설 버퍼 출력·전량 소모도 공용 Lease를 우회하면 다른 작업의 예약분을 침범할 수 있다. 이 경로들을 임시 `DirectPlayerOrder` Lease와 available-only 라우팅으로 통합한 뒤 관련 회귀 계약이 통과했다.
- `originStackId`와 현재 Slice stack ID는 같은 개념이 아니다. 집약 후 여러 작업이 한 canonical stack을 공유하므로 저장은 최초 출처와 저장 순간 물리 대상을 둘 다 보존해야 완전한 Grandfather 복원이 가능하다.
- 기존 작업의 claim 합이 물리 수량을 초과하면 일부 우선순위 복원이 아니라 전체 복원 실패가 맞다. 정상 복원은 작업 종류 우선순위와 무관하게 모든 기존 claim을 먼저 등록하고 신규 AI를 나중에 연다.
- 100개 동시 Lease 감사에서 예약 단계 물리 자식 0, 64개 즉시 집약·36개 지연, MaxStack 75 기준 canonical stack 2개, 100개 개별 소비 후 고아 물리 스택 0을 확인했다. 이 결과는 먼지 스택의 엔티티 수 상한을 증명하지만 프레임 GC 0을 뜻하지 않는다.
- 릴리스 soak의 기능 불변식과 재로드는 통과했다. 전체 Editor GC만 평균 4,900.2 KB/frame으로 실패했으며, Mono heap은 6.09 MB 감소했다. 따라서 메모리 누적과 프레임 할당은 분리해서 다뤄야 한다.
- raw allocation capture 실패는 관리 코드 예외가 아니라 Unity 네이티브 profiler buffer의 `profiling::Dispatcher::AcquireFreeBuffer` 크래시다. 보고서가 생성되지 않았으므로 새 물류 코드가 GC 원인이라는 결론도, 무관하다는 결론도 아직 낼 수 없다. 비충돌 캡처 설정으로 재검증해야 한다.
- Phase 156 코드·기능 회귀는 완료 상태지만 성능 수용 게이트와 Phase 155 순 WU 재계산은 열려 있다. 전체 밸런스 완료로 보고하면 안 된다.

- Full roster verdict: `S 3 / A 8 / B 10 / C 5 / D 22 / F 8`. Thirty of 56 traits either have their core identity inactive or apply a downside without the intended compensation. The generation distribution is valid, but the effective-value distribution is not yet balanced.
- Dominant live traits are Clean (cleaning +3, dexterity +1, global work +5%, accident -15%), Researcher (research +3 multiplied by research x1.18 and global x0.97, approximately x1.35 research), and Fast Learner (approved-work XP x1.30 plus research +4%). The most lopsided families are hygiene, risk, appetite, metabolism and mercy.
- Full evaluation and prioritized fix order are in `Artifacts/QA/v26-founder-trait-roster-evaluation.md`. Until runtime authority is corrected, founder production calculations must treat behavior/mood/event-only traits as zero-effect rather than crediting their descriptions.
- Full trait-roster evaluation live inventory succeeded through Unity MCP after two non-executing dynamic-compiler attempts. All 56 live assets expose the expected rarity/family metadata and the complete mechanical payload used for the evaluation.
- Runtime surface check: numeric stats/modifiers are projected directly; behavior preferences are collapsed into generic AI action utility through `CharacterAiPersonality`; event weights are stored on the runtime profile but have very few visible consumers; mood reactions enter `HeritableTraitRuntimeEffects`. The evaluation must therefore distinguish direct always-on effects from tag-driven effects whose trigger coverage may be incomplete.
- Critical authority mismatch: rolled founder traits live in `CharacterGrowthState.traitIds` and are present in `actor.profile` through the progression projector. However, AI trait utility reads `identity.Profile`, and mood reaction application iterates `actor.Identity.Data.traits`; both are the original authored-template profile/list, not the rolled growth-state traits. Therefore rolled-trait numeric modifiers work, but their behavior and mood identities generally do not. `GetEventWeightMultiplier` has no external runtime caller at all, so authored event weights are currently inert.
- Even after redirecting behavior lookup to the effective profile, only prefixes `work/research/career`, `food/health/rest/room`, `danger/safety/emergency/combat`, `item/choice`, and `service` map to generic AI actions. Authored prefixes such as `social`, `conflict`, `faction`, `culture`, `shift`, `travel`, `stock` and `facility` have no fallback mapping. Mood triggers are currently published only for meals (`food:sated/contaminated/safe-meal`), research completion, festivals (`festival:prepared`, `event:minor-success`, `culture:harmony`, `event:audience`) and invasion outcome (`event:combat-victory`, `danger:exposed`) plus explicitly published `CharacterTraitReactionEvent`s.
- Implemented result: 56 general traits now carry one selection family, common/uncommon/rare/exceptional weight 100/55/25/10, optional species eligibility and an earned-work-XP multiplier. Generated identities contain 1-4 traits and reject duplicate family, authored incompatibility, explicit pair conflict and species-ineligible candidates.
- Final no-reroll 100,000-profile result is 15.203/40.029/34.664/10.104% for 1/2/3/4 traits (mean 2.397). Per-trait occurrence fell monotonically from common 6.12% to uncommon 3.43%, rare 1.57% and exceptional 0.66%; all 56 were reachable, with zero family collisions and zero non-Slime leaks of trait 109.
- Fast Learner is exceptional and now multiplies approved-work XP by x1.30 through the runtime profile. Four traits survive JSON save/restore, the preparation UI renders four compact chips with rarity and family information, and dominant legacy traits were reduced or given explicit tradeoffs.
- Pre-change founder-trait audit: the start pool had 56 general traits and always selected exactly three. Selection enforced only three explicit ID pairs, ignored authored V20 incompatibility groups and did not filter the species-named Cold-resistant Slime trait.
- User target: founders should receive 1-4 traits, strong positives should be rarer, only one trait from the same functional family may appear, and traits should have clearer operational identities.
- Adopted natural distribution is 15/40/35/10% for 1/2/3/4 traits (mean 2.40). Common/uncommon/rare/exceptional weights are 100/55/25/10; family, explicit conflict and species eligibility are separate hard filters.
- Pre-change dominant legacy effects were not internally comparable: Clean was work x1.15/consumption x0.95/accident x0.80, Fighter was work x1.10/combat x1.25/accident x1.20, Researcher was research x1.30, while many V20 traits mostly altered behavior/mood/event weights. Fast Learner granted only research x1.04 and no learned-XP bonus.

## 2026-08-10 - Founder age/background proficiency baseline

- User correction: primary/secondary must be RimWorld-like learning identities, not only starting-XP labels. New target factors are primary x1.50, secondary x1.20 and unrelated x1.00 across real XP earning paths; hard combat/training daily caps remain unchanged and non-learning catch-up floors must not overshoot.
- The previous 36.1% Elder-condition rate leaves 63.9% of Elders healthy. With 5% Elder weight and nine equally covered primary histories, a specific healthy Elder primary appears in roughly 2.5% of natural seven-candidate rosters before trait filtering. Because unlimited manual reroll is accepted and specialization now has permanent growth value, the target is tightened to 65-80% any condition and 25-45% multiple, reducing that pre-trait roster chance to roughly 1%.
- User policy decision: unlimited manual reroll is intentionally allowed. It is not an exploit gate; balance evidence must use and label the no-reroll natural distribution so the baseline remains reproducible while players may voluntarily optimize beyond it.
- Primary/secondary specialization currently grants no permanent hidden multiplier. Its mechanical benefit is starting career XP: history primary gets +45 base plus up to +120 age-career XP, secondary gets +25 plus up to +60, then origin/context bonuses and the age cap apply. The resulting XP already changes speed, quality, accident risk and combat effects through the single proficiency authority.
- Existing initial age conditions are meaningful after onset: each begins Mild, damages affected anatomy by 5%, then progresses one severity per biological year without care. Final Elder targets are 65-80% with any condition and 25-45% with multiple; non-Elders remain condition-free at start.
- The starting packet is now one coherent profile instead of nine unrelated rolls: species-relative age sets the cap and career scale, past history sets primary/secondary specialization, and origin adds a smaller contextual bonus.
- Exact subgrades are IV -> III -> II -> I inside every major rank. Major-rank content requirements remain compatible, while work speed and accident risk now change continuously rather than jumping only at 100/400/1200/3000 XP.
- The 18,000-profile audit produced mean primary/secondary/unrelated XP of 125.58/81.83/31.88 and mean speed multipliers of 0.956/0.930/0.882. At 99 base WU/day this is about 94.6/92.1/87.3 effective WU/day.
- Older profiles are genuinely more experienced: mean primary XP by young/established/veteran/elder band is 89.43/128.11/172.42/212.55, with hard caps 99/174/249/399. No founder can start at Technician.
- Elder starting health uses an age-progressive accumulated prior-life burden while all resulting conditions reuse the existing CharacterLife/body-health path. In 18,000 samples, 655 profiles had an initial condition and 313 had multiple conditions; among 875 Elders this is 74.9% / 35.8%, with none outside the Elder band.
- The 20,000-roster no-reroll audit now establishes the three-person selection envelope. Choosing two of six candidates around the fixed owner raises mean best Fieldwork/Food/Construction/Crafting speed from 0.929/0.920/0.928/0.920 to 0.944/0.938/0.945/0.937, and raises all-four specialization coverage from 10.8% to 48.7%.
- Best distinct three-worker assignment totals rise only 1.5-1.7% (survival 2.757 -> 2.799, manufacturing 2.755 -> 2.801), so candidate choice matters without multiplying initial production. These values can now feed the first real food/building/item completion-time calculation.
- After final health retuning and a 5%-damage-equivalent selection penalty, balanced selection lowers Elder share from 5.0% to 4.7% and reduces selected initial-condition count from 3,461 to 2,560. A specified healthy Elder primary appears in roughly 1% of natural seven-candidate rosters before trait filtering.
- Specialization learning is now stored with the published proficiency: primary x1.50, secondary x1.20, unrelated x1.00. At 99 successful approved WU/day, mean-start Technician timing is approximately 23.1/33.5/46.5 full workdays before aptitude, difficulty, repetition and outcome modifiers.

## 2026-08-10 - 최초 3인방 리롤 권위 추적 중

- 현재 시작 준비 UI와 리롤 상태의 중심 구현은 `StartPartyPreparationService`, `StartPartyPreparationSnapshot`, `StartPartyPreparationUiController`다. UI 검증에는 구성원별 전체 리롤과 Identity 부분 리롤이 별도로 존재한다.
- 현행 종합 문서는 9개 숙련 각각을 결정론적 15~45 XP에서 생성하고, 과거의 공격·연구·민첩 등 별도 초기 능력치는 다시 굴리지 않는다고 명시한다. 따라서 화면상 `능력치 리롤`이 실제로 무엇을 바꾸는지는 구형 상세 능력치가 아니라 신원·적성·스킬·숙련 상태를 기준으로 분해해야 한다.
- 시작 명단은 선택한 사장 1명과 같은 종족 직원 후보 6명이다. 직원 2명만 선발되고 4명은 예비이며 선발/예비는 교체할 수 있다. 즉 플레이어는 리롤 전에 이미 같은 종족 후보 6명 중 2명을 고르는 선택권을 가진다.
- 구성원마다 Identity/Aptitude/Skill 부분 리롤이 각각 3회지만 전체 리롤에는 횟수·비용 제한이 없고 세 부분 리롤을 다시 3회로 충전한다. 현재 계약대로라면 원하는 조합이 나올 때까지 전체 리롤을 무한 반복할 수 있어 부분 리롤 제한이 실질적인 제약이 아니다.
- Identity는 이름·출신·특성을 다시 뽑고 기존 잠재력과 숙련 시드를 보존한다. Aptitude는 잠재력과 9숙련 시드를 다시 뽑고 신원을 보존한다. Skill은 미리 생성한 다음 액티브/패시브 묶음으로 교체한다.
- 다만 Identity와 Aptitude도 새 `CharacterProgression`을 만들고 공통 `ApplyRoll`에서 시작 스킬을 다시 생성하므로, 코드상 해당 그룹만 고립해서 바뀐다고 아직 단정할 수 없다. 스킬 시드와 최종 스냅샷을 더 추적해야 한다.
- 9개 숙련은 각 항목이 `stableHash(seed, proficiencyId, index) % 31 + 15`로 독립적인 15~45 정수 XP를 받는다. 합계 예산, 역할 보장, 한 명당 전문 분야 보장이나 파티 상호보완 제약은 없다. 모두 Skilled 기준 100 XP 미만이라 시작 등급은 전원 전 숙련 Apprentice다.
- 잠재력 기본 가중치는 설정이 없을 때 45/30/15/8/2이며 실제 설정 에셋을 확인해야 한다. 구형 `initialBaseStats` 입력은 `ApplyPreparedIdentity`에서 무시하고 빈 블록으로 덮으므로 현행 Aptitude의 실질 리롤 대상은 잠재력과 9숙련 패킷이다.
- 특성은 전체 특성 카탈로그를 ID 순으로 정규화한 뒤 무작위 순위를 매겨 충돌하지 않는 최대 3개를 채운다. 현재 서비스 호출은 종족·직업별 적합성 필터를 전달하지 않는다.
- 실제 설정 에셋도 잠재력 가중치가 평범/유망/우수/탁월/천재 = 45%/30%/15%/8%/2%다. 잠재력은 현재 시작 생산 숙련을 올리지 않고 스킬 희귀도 분포를 바꾼다. 천재는 일반/고급/희귀/영웅/전설이 15/25/30/21/9%, 평범은 60/27/10/2.7/0.3%다.
- 시작 스킬은 직원마다 액티브 1개와 패시브 1개가 즉시 확정되며 후보 선택 폭은 각각 1개다. 모듈 조합은 생성 규칙에서 허용된 첫 조합을 사용하므로 스킬 리롤은 선택지를 제시하기보다 새 결과 한 묶음으로 교체한다.
- 사장은 authored 고정 권능 4개를 사용하고 일반 시작 액티브/패시브 생성에서 제외된다. 그런데 공용 리롤 UI/API는 사장에게도 Skill 리롤을 노출·허용하므로, 사장의 Skill 리롤은 충전만 소비하고 고정 권능은 바꾸지 못하는 무효 선택일 가능성이 높다.
- UI의 Aptitude 탭은 잠재력, 9개 숙련 XP와 숙련 합계를 모두 공개한다. 따라서 플레이어는 6명의 직원 후보를 비교하고, 무제한 전체 리롤로 높은 합계·원하는 전문 숙련·높은 잠재력을 동시에 선별할 수 있다.
- 시작 확정 스냅샷에는 선택된 사장 1명과 직원 2명의 최종 성장/서사 상태만 들어간다. 예비 4명과 리롤 잔여 횟수는 저장되지 않으며 준비 화면을 벗어나면 폐기된다. 게임 적용 뒤에는 선택 결과가 기존 캐릭터 성장·서사 저장 권위로 들어간다.
- 새 런의 `runSeed`는 준비 화면 진입 때 현재 시각과 난이도로 생성된다. 그러나 준비 UI는 이 값을 최종 스냅샷에만 넣고 `RandomStreamProvider.Reseed(runSeed)`를 호출하지 않는다. 준비 리롤 난수는 `character:start-party-preparation` 스트림의 기존 root/state에 의존하며 새 런 시드에 직접 묶이지 않는다. 실제 `Reseed`는 게임 런 적용 뒤 `RunVariableSystem.StartRun`에서 일어난다.
- 전체 리롤 주사위는 직원에게만 표시되고, 부분 리롤 잔여 합이 0보다 클 때 활성화된다. 전체 리롤 자체는 횟수를 줄이지 않고 세 묶음을 3회로 복구하므로 처음부터 연속 전체 리롤이 무제한이다. 세 부분 횟수를 먼저 모두 소진하면 그때만 전체 버튼이 비활성화된다.
- 사장은 전체 리롤 주사위가 숨겨져 있지만 Identity/Aptitude/Skill 부분 주사위는 공용 섹션에서 노출된다. 또한 `사장 다시 선택`은 준비 서비스를 취소하고 7명 전체를 새로 만들기 때문에 준비 화면을 왕복하면 사장과 직원의 제한도 사실상 다시 시작할 수 있다.
- 시작 15~45 XP는 모두 Apprentice 등급이므로 작업속도는 동일한 0.85배, 사고 배율도 동일한 1.25배다. XP 차이는 연속 품질/상세 성능 점수와 Skilled까지 남은 거리만 바꾼다. 99 WU/일과 0.08 XP/WU를 전담 작업에 그대로 적용하면 45 XP 시작자는 약 6.9일, 15 XP 시작자는 약 10.7일 뒤 Skilled에 도달한다.
- 따라서 현행 `재능 리롤`로 높은 제작 XP를 뽑아도 첫날 제작속도는 오르지 않는다. 대신 초기 품질과 전투/상세 성능, 그리고 대략 3.8 전담 작업일의 승급 격차가 생긴다. 최초 생산속도 차이는 종족·특성·패시브·욕구/건강/동선 같은 다른 보정에서 더 크게 나올 수 있다.
- 현재 trait asset은 9개뿐이고 선택기는 가능한 3개를 항상 채우려 한다. 그중 `ColdResistantSlime`처럼 이름상 종족 전용으로 보이는 특성도 전역 풀에 들어가므로, 특성 정의에 별도 적용 필터가 있는지 확인하지 않으면 모든 종족이 이를 뽑을 수 있는 상태다.
- `CharacterTraitSO`와 `Trait_ColdResistantSlime.asset`에는 적용 종족 필드가 없고 시작 선택기도 종족을 전달하지 않는다. 따라서 내한성 점액 특성은 현재 오크·뱀파이어 등 모든 시작 종족이 실제로 뽑을 수 있다.

### Founder reroll audit conclusion

- 현재 리롤 결과를 최초 3인방의 기대 생산력 기준으로 바로 사용할 수 없다. 독립 15~45 난수는 역할 구성을 보장하지 않고, 직원 6명 선별과 무제한 전체 리롤이 분포를 플레이어의 인내심에 따라 임의로 왜곡한다.
- 다음 밸런스 단계에서는 먼저 총 숙련 예산, 구성원별 주/부 전문, 파티 최소 역할 커버리지, 종족 적합 특성, 리롤 비용/상한과 그룹 격리를 확정한 뒤 그 분포에서 초기 생산속도를 계산해야 한다.

## 2026-08-10 - 산업 체크포인트 플레이타임 축

- 현재 시간 권위는 게임 하루 180초이며 `Time.timeScale`의 영향을 받고, 플레이 배속은 x1~x5다.
- Day 1에서 시작하므로 절대일 D 도달까지의 순수 진행시간은 `(D - 1) × 180초 ÷ 배속`이다. 일시정지, 건설·생산 명령, UI 검토와 전투 의사결정 시간은 포함하지 않는다.
- 기존 문서에는 목표 완주 플레이타임이나 평균 배속 사용률 권위가 없다. 산업 표의 `유효 x1.5~x2.5` 체감 범위는 설계용 임시 가설이며, 이후 실제 플레이의 배속 혼합·정지 비율을 측정해 교체해야 한다.

## 2026-08-10 - Equipment readiness throughput gate passed

- The original failure was partly a demand-model error: it gave every newly combat-ready resident the checkpoint's latest expedition-grade set. The baseline only requires a weapon plus minimum protection for reserve readiness; contemporary advanced equipment belongs to the selected expedition party. The audit now measures those demands separately while retaining a full-reserve minimum-kit pressure indicator.
- The day-30 longsword/gambeson set was not manufacturable on its apparent early research path because both are authored growth equipment and require `component:growth-frame`, whose steel, machine-parts, precision-parts and modular-frame research belong to a later production chain. The replacement falchion/leather/wood-shield set uses real non-growth equipment, is physically one-hand compatible and still projects 1.26 times the merchant-road readiness requirement.
- Routine checkpoint quality now follows attainable production targets instead of granting aspirational quality to all output: Normal through day 120, Good at days 240/400 and Excellent at day 960. All pieces exceed 50% acceptance within ten attempts, while the prior Masterwork rune set did not.
- Research reachability must traverse the full manufacturing tree, not only `CombatEquipmentDefinitionSO.RequiredResearchId`. The audit now follows the cheapest-EWU upstream recipe for the default material and every component, then de-duplicates the research prerequisite closure.
- That traversal found 22 live item/recipe properties still referencing projects absorbed by V21 consolidation. Exactly 11 item/recipe pairs were normalized through `V21ResearchConsolidation` (`engineering-drawing` to `industry:powered-tools`, `rune-grid` to `industry:mana-power`, and the other mapped absorbed IDs). Final census is zero stale IDs; no duplicate research project was created.
- Final conservative production shares for the expedition party are 32.5%/24.2%/75.6%/90.9%/27.1% at days 30/120/240/400/960. Newly ready minimum kits use 0%/2.3%/2.0%/2.3%/1.1%. Six production checkpoints, six non-circular power checkpoints and the 180-project research/equipment audit pass with Console Error 0 / Warning 0.
- This is not live factory telemetry. Facility contention, stock starvation, net salvage after rejected quality, maintenance, repair/replacement losses and multi-seed battle win rates remain later gates.

## 2026-08-10 - Equipment readiness throughput audit boundary

- The existing checkpoint probe proves that one authored loadout has sufficient projected readiness power, but it creates equipment instances directly and therefore does not prove that the settlement can manufacture enough loadouts by the checkpoint.
- The production authority already exposes the required inputs: concrete primary material and component BOM, `V23BalanceWorkCalculator` direct craft work, `V23EmbeddedWorkValueCalculator` upstream EWU, equipment research IDs and authored crafting proficiency ranks.
- Throughput must separate three questions: can the required expedition party be equipped, can newly combat-ready residents be equipped as population grows, and what would it cost to refresh the entire reserve with contemporary gear. Treating full replacement as the only plan would incorrectly erase usable old gear; treating one representative member as enough would hide reserve shortages.
- Quality is a real throughput cost. Good/Excellent/Masterwork checkpoint snapshots cannot be granted directly in the production audit without reporting expected repeat attempts and rejected-output handling.
- `ResourceGameContentCatalog.GetAll<T>()` delegates to the domain catalog for generic definitions; root `ItemDefinitionSO` authority is `content.Items.Definitions`. EWU consumers that need the complete physical item catalog must use the latter, matching `V23BalanceAudit.EditorContentSource`.
- The first valid partial measurement already shows growth-frame-heavy checkpoint sets are expensive: day-30 one-party gross quality EWU is 171.9% of the conservative growth/production window, day-120 is 239.9%, and day-400 is 275.9%. These values must be rerun after the root-item correction but are not caused by the four missing late item entries.

## 2026-08-10 - Biological reproduction success was bypassed

- Every biological reproduction asset authored a 35% base success chance, but none contained the `Attempt` phase that calls `CalculateSuccessChance`; safe processes therefore entered pregnancy, egg, spore or division phases without a conception roll.
- All nine non-golem profiles now begin with a one-day attempt and the catalog contract rejects a biological profile that omits it. Golem assembly rejects the biological attempt phase.
- The 256-seed population audit shows balanced regular recruitment plus same-lineage births reaches median 64 residents at day 960, 16 below the target floor. The missing population should come from captive recruitment, faction joiners and constructed golems at roughly one adult per 60 days, not from silently restoring guaranteed births.
- Balanced stays inside total-population targets through day 400, while conservative and expansion policies reach 33 and 100 at day 960. Housing, food, medicine and mixed candidate scarcity can only lower these unconstrained envelopes.

## 2026-08-10 - Guest recruitment throughput gap closed

- Normal guest recruitment previously activated an adult immediately after two qualifying visits with no global pacing boundary. With visitor respawn measured in seconds, this could exceed the day-30 population band even though births remain dependents for hundreds of game days.
- Successful employee and mercenary recruitment now share a 10-absolute-day cadence. The last successful day is stored per recruited customer and the newest day is the single derived cooldown authority.
- Late campaign recruits previously received a high generic character level but retained only 15~45 XP in every real proficiency. Since generic level no longer raises work/combat performance, these adults were narratively experienced but mechanically novice.
- The two strongest starting proficiencies now receive campaign floors of 0/100/250/400/600 XP at 0/1/2/3/4+ completed targets. Expert and Master remain work/mentoring achievements.
- Focused recruitment rules, cooldown boundaries and strict save scenarios pass. The regular-recruit + same-lineage reproduction 1~960-day multi-seed envelope now passes; mixed-source population and physical-capacity PlayMode validation remain pending, so whole-population balance is not complete.

## 2026-08-10 population, proficiency and equipment checkpoint result

- 출생자를 즉시 노동력으로 계산하지 않는 1/30/120/240/400/960일 목표 밴드를 확정했다. 240일 이전 노동력 증가는 주로 성인 영입·포로 영입·골렘 조립에 의존하고, 생물 출생 세대는 종족에 따라 180~540일 뒤 성인이 된다.
- 캠페인 요구 전투력으로 아군 능력치를 역산하던 순환 검증과 별도로, 9종 숙련의 안전 훈련·일상 작업 성장치를 먼저 계산하고 실제 장비 카탈로그 스냅샷을 장착하는 비순환 검증기를 추가했다.
- 실제 장비 조합은 1일 창·천 두건, 30일 장검·누비옷·나무 방패, 120일 철퇴·사슬 셔츠·철 방패, 240일 에스터크·관절 판금·철 방패, 400일 동력 세트, 960일 룬 세트를 사용한다.
- 6개 체크포인트 모두 해당 캠페인의 최소 인원·요구 준비 전투력을 넘겼다. 비율은 1.11~1.83이며 이는 출발 가능성만 뜻한다. 지형·목표·탄약·부상·적 능력을 포함한 실제 승률은 아직 증명하지 않았다.
- 기존 권위 프로브도 함께 재실행해 200,000명 노화 표본, 4/6일 생물학적 노화율, 단골 영입 조건·활성화, 장기 비직원 인구 상한과 저장 복원을 통과했다. 이 검증은 생애·영입 규칙의 정확성을 증명하지만 날짜별 영입 발생량은 증명하지 않는다.
- 이론 인구 밴드 자체도 장시간 실측이 아니다. 다음 필수 증거는 실제 영입·출산·사망·부양·장비 생산을 돌린 다중 시드 장시간 인구 시뮬레이션이다.

## 2026-08-10 expedition loadout power authority

- 원정 준비 UI와 출발 기록이 이제 실제 장착 무기·방어구·방패를 같은 `ICombatEquipmentRuntime`에서 조회한다. 재고 보유량이나 정의 에셋만으로는 전투력이 오르지 않는다.
- 인물의 기반 전투력은 기존처럼 9종 숙련의 호환 투영과 현재 부상·건강 상태가 결정한다. 장비는 별도 성장 능력치가 아니라 해당 기반값에 붙는 제한된 준비도 기여다.
- 총 장비 기여는 인물 전투력의 60%, 무기 35%, 방어구 30%, 방패 15%로 제한했다. 따라서 인구 증가로 확보한 숙련 인력과 장비 생산·정비가 서로 보완하지만 어느 한쪽이 다른 쪽을 완전히 대체하지 않는다.
- 무기는 실효 DPS·관통·품질·재질·내구·최적 사거리 효율을, 방어구는 신체 부위별 피격 비중과 세 피해 유형 방어를, 방패는 정면 막기 확률과 방어를 사용한다. 탄약 무기가 비어 있으면 무기 기여가 절반으로 내려간다.
- 집중 프로브에서 비무장 < 초기 장비 < 후기 장비, 저품질·마모 장비의 하락, 미장전 원거리 무기의 하락, 방어구·방패의 추가 기여와 극단 장비의 60% 상한을 검증했다. 실제 전투 승률과 시대별 획득 가능 장비 분포는 다음 장기 체크포인트 감사 대상이다.

## 2026-08-10 early combat and expedition entry

- The authored invasion schedule already protected the intended settlement window: days 1~9 have no forced hostile encounter, days 10/20/30 use rehearsal strength, the first normal boss is day 40, and random invasion eligibility begins on day 31.
- The remaining loophole was voluntary expedition launch: the map and expedition runtime had no preparation authority and allowed a first-day departure.
- A pure date gate would contradict the design rule that dates do not directly lock content. `research:survival:field-rations` is the correct causal gate because it already requires food preservation plus logistics and unlocks the physical expedition-ration chain.
- Map information and recon remain available before the research. Only preparation and launch are blocked, preserving planning while making food/logistics preparation a real opportunity cost.
- UI-only blocking is insufficient. The same `BlueprintResearchState` check now exists at the application command boundary and at `OffenseExpeditionRuntime.TryStartExpedition`, with a focused direct-call bypass probe.

## 2026-08-10 long-horizon population/power audit start

- Population growth is not a single passive multiplier. New residents enter through authored reproduction processes, guest recruitment, captive recruitment and expedition rewards, so a valid checkpoint model must distinguish dependent children, working adults and combat-ready adults rather than compare raw headcount.
- Reproduction already has species-specific physical phase durations: fast lines such as slime/myconid/kobold differ materially from pregnancy and golem assembly. A single population percentage would erase their labor and dependency costs.
- Party power is computed from real characters and combat multipliers, while campaign enemy scaling uses authored target `requiredPower`. The missing proof is a shared 1/30/120/240/400/960-day matrix connecting resident composition, proficiency distribution and equipped loadouts to those authored threat checkpoints.
- Recruitment has explicit visit/candidate paths rather than a calendar population grant. Its throughput and onboarding equipment cost must be included in the same matrix before changing enemy numbers.
- The baseline currently lists date and population axes independently but does not define which working/dependent/combat-ready population bands should coexist at each date checkpoint.
- `CombatOutcomeBalanceCalibrationScenario.CreateParty` derives synthetic ally stats backward from each target's own `requiredPower`. That can validate encounter mechanics around a desired matchup, but it cannot prove that real residents can reach the required power through proficiency growth and obtainable equipment. A non-circular progression projection is required before combat balance can be called complete.
- The current expedition `CalculateMemberPower` uses compatibility stats derived from proficiencies and the character combat multiplier, but it does not read the equipped weapon, armor or shield. The launch record and expedition panel therefore report identical power for an unarmed resident and the same resident in a complete loadout even though battle resolution uses that equipment. This violates the requested population+growth+equipment balance coupling and is the first concrete runtime gap to fix.
- The nine-proficiency projection correctly derives Attack/Shooting/Strength/Toughness/Endurance from the relevant proficiency XP. The missing piece is not character growth authority; it is a loadout contribution added at the party-power query boundary without turning equipment into a second character stat system.

## 2026-08-10 - Phase 147 proficiency re-certification

- The requested authority split is mechanically verified: nine proficiencies own initial aptitude and progression, while the old detailed-stat values remain internal compatibility projections only.
- Work speed, completion quality and active-work accident risk are derived from the relevant proficiency at execution time; generic character level no longer grows a parallel set of work stats.
- Current focused catalog coverage is 31 work types, 419 buildings, 354 recipes, 61 combat equipment definitions and 56 apparel definitions. Full-world `68/68/68` and later gameplay balance domains still require renewed evidence.

## 2026-08-10 - Phase 147 전체 밸런스 다음 우선순위

- 최근 숙련 단일 권위 변경은 집중 하네스를 통과했지만 31종 작업·전체 authored 콘텐츠 매핑과 `68/68/68` 전체 저장은 변경 후 재인증이 필요하다.
- 종합 문서의 성장 호흡은 1~10일을 생존 거처, 10~30일을 예행 침입 단계로 규정한다. 따라서 첫날 강제 침입이나 원정이 가능·유리하다면 현재 설계 의도와 충돌한다.
- 다음 밸런스 변경은 별도 평화 자원을 만드는 방식이 아니라 절대 달력 기반 침입 스케줄 권위에 최소 개시일과 사전 경고를 두는 방식이어야 한다. 저장·재접속으로 초기화되는 별도 타이머는 허용하지 않는다.

## 2026-08-10 - V26 숙련 단일 권위 전환 결과

- 캐릭터 생성 시 별도 12종 능력치를 굴리던 구조를 중단하고, 9종 숙련이 각각 결정론적 `15~45 XP`에서 시작하도록 전환했다.
- 공용 캐릭터 레벨은 더 이상 작업·전투 수치를 올리지 않는다. 실제 성능 성장은 승인된 작업, 전투 증거와 멘토 수업으로 해당 숙련 XP가 변할 때만 발생한다.
- 작업 속도, 완성 품질과 현재 작업의 사고 위험은 `CharacterProficiencyEffectSnapshot`에서 직접 계산한다. 품질의 구형 `숙련 75% + 별도 능력치 25%` 혼합식과 사고 위험의 지구력·강인함 재조합은 제거했다.
- 구형 `CharacterStatType` 12종은 기존 전투·생리 호출부를 위한 내부 호환 투영으로만 남는다. 독립 생성·레벨 성장·저장·작업자 필터·플레이어 UI 권위로는 사용하지 않는다.
- 시작 편성, 캐릭터 상세 화면과 작업자 정책은 모두 9종 숙련을 직접 표시·선택한다. 구형 능력치 조건이 남은 개발 저장은 새 게임 필요 오류로 거부한다.
- 집중 하네스는 결정론적 시작 숙련, 100,000회 품질 표본, 960일 쇠퇴·평생 장부와 2,000명 지연 정산 0B를 통과했다. 전체 `68/68/68` 및 해상도별 포인터 흐름은 이번 전환 뒤 아직 재인증하지 않았다.
- 광범위 캐릭터 진행 디버그 묶음은 이 변경과 무관한 오래된 아키타입 ID·모듈 조합·루트 재고 fixture 문제를 함께 포함해 실패했다. 숙련 집중 증거와 구분하여 후속 정리해야 한다.

## 2026-08-10 - Phase 146 숙련 파생 성능 전환 조사

- 현재 `CharacterStatType`는 근접, 판매, 연구, 이동, 힘, 강인함, 민첩, 청소, 지구력, 사격, 회피, 의료의 12종이며 `CharacterSkillSystemSettings.asset`은 이를 각각 1~10, 총합 55로 독립 무작위 배분한다.
- `CharacterGrowthRules.AllocateGrowthPoints`는 공용 레벨 상승 때 12종 능력치에 별도 성장점을 배분한다. 이는 9종 숙련이 유일한 성장 권위라는 사용자 설계와 충돌한다.
- `CharacterProgressionProfileProjector`가 초기 능력치·종족·특성·레벨 성장·스킬·영구 보너스를 합쳐 `GetFinalStat`을 만들고, `CharacterStatsProjectionService`와 수술·전투·시작 편성 등 여러 경로가 이를 읽는다.
- `CharacterStatsProjectionService.GetWorkSpeedMultiplier`는 구형 `CharacterWorkStatRules`의 단일 능력치로 작업 속도를 계산하는 반면 V25 품질은 숙련 등급 75%와 별도 능력치 25%를 결합한다. 따라서 숙련과 독립 능력치가 같은 작업 결과에 이중 권위로 작용한다.
- 구형 `CharacterWorkStatRules`는 치료를 연구, 범용 시설 가동을 판매, 청소를 독립 청소 능력치로 연결한다. 새 9종 숙련 분류와 맞지 않는 실제 실행 경로다.
- 82개 에셋·프리팹·씬에 `baseStats` 또는 `statBonus` 직렬화 흔적이 있다. 에셋 필드를 즉시 삭제하면 대규모 직렬화 손실 위험이 있으므로 전환은 필드를 비권위 레거시 데이터로 남기고 모든 운영 읽기·쓰기를 차단한 뒤 별도 정리하는 순서가 안전하다.
- 직접 `GetFinalStat`을 쓰는 운영 경로는 시작 편성 능력, 캐릭터 진행 스킬 후보, 수술 위험 등이다. 이 경로들도 단일 파생 성능 조회로 전환해야 한다.

## 2026-08-10 - V25 9종 숙련 통합 완료

- 숙련 권위는 현장 작업, 건설·공학, 제작, 식량 생산, 학술, 의료, 사교, 근접 전투, 원거리 전투의 9종으로 축소됐고 31개 작업은 단일 숙련 또는 의도된 무숙련 작업으로 전수 분류됐다.
- 방어·포로 관리·사냥·룬 제작은 독립 숙련이 아니라 승인된 복합 계산을 사용한다. 청소·휴식·경비 대기는 숙련 XP를 지급하지 않는다.
- 일반 작업의 공용 레벨 XP 지급을 제거하고 승인된 기여 작업량만 숙련 XP를 원자적으로 지급한다. 반복 품질 파이프라인과 해체에는 감소 계수를 적용한다.
- 전문가·대가 쇠퇴는 절대시간 차이 기반 지연 정산이며, 2,000명 프로브에서 `0.459ms`, 현재 스레드 할당 `0B`, 시간당 전 주민 순회 없음으로 통과했다.
- 시설 419개, 조합식 354개, 전투 장비 61개, 의복 56개가 명시적 숙련 프로필을 가진다. 자동 부록은 `Artifacts/QA/v25-proficiency-authored-mapping.md`에 생성됐다.
- 멘토 배정·수업은 관계, 등급, XP 차이, 정원, 하루 한 번, 양쪽 30 WU, 실제 멘토 학원과 저장 복원 조건을 모두 사용한다.
- 100,000회 품질 표본, 960일 도달·쇠퇴 계산, 두 해상도 실제 포인터 흐름, 전체 저장 `68/68/68`과 Console `0/0`이 통과했다.
- 이 완료 판정은 V25 숙련 시스템 범위다. 전투 승률·전역 생산 경제·이정표 도달 시점까지 포함한 전체 게임 밸런스 완료 판정은 아니다.

## 2026-08-09 combat outcome continuation

- Root cause of the single-shot behavior is now concrete: after any ammo-consuming attack, `OffenseBattleSession.TryBasicAttack` asks `ICombatEquipmentRuntime.TryGetActiveWeapon` for a refreshed snapshot even when `TryConsumeLoadedAmmo` failed. `CombatEquipmentLoadoutRuntime.TryGetActiveWeapon` returns `true` with an unarmed snapshot for an unknown combatant, so manually composed encounter allies/enemies silently lose their ranged weapon after the first shot.
- This is broader than the calibration probe: encounter combatants receive equipment snapshots directly from the encounter factory, while the equipment runtime may not own corresponding physical instances. The battle runtime needs an explicit finite reserve-ammunition authority or a deliberate snapshot-only fallback; simply preserving the original snapshot would create infinite ammo.
- `OffenseBattlePersistenceState` currently saves health, formation, statuses and cooldowns but not the battle combatant's weapon/ammunition snapshot. Any local-only ammunition fallback would therefore reroll to the configured combatant's initial ammo after restore unless loaded/reserve ammo is added to the persistence contract or all combatants are guaranteed to use the physical equipment aggregate.
- Production enemy individuals are provisioned into the real `ICombatEquipmentRuntime`; `EnemyIndividualRuntime.EnsurePhysicalEquipment` creates an external weapon, assigns it, and loads exactly one magazine. It creates no reserve ammunition. Consequently one-shot behavior for capacity-1 crossbows is an actual gameplay rule, not only a probe artifact; player allies differ because their carry inventory can supply reloads.
- The correct fix must preserve the physical-ammunition contract: enemy reserve ammunition should exist in the same carry/item authority and be lootable/persistent, or encounter design must explicitly account for one loaded magazine. An invisible local integer reserve would violate the game's physical-item authority.
- Finite-magazine calibration exposed a second actual runtime issue: an empty ranged weapon has no automatic recovery path. `CreateEnemyCommand` only searches targets reachable with the current weapon; when none exist at the front it returns no command and the outer runtime guards forever. The correct finite-ammo fallback order is physical reload, another owned usable weapon, then the always-available unarmed attack, with each change consuming a normal battle action.
- The outcome probe currently builds exactly `requiredMembers` combatants, but `requiredMembers` is a launch minimum, not necessarily the intended balanced party size. Campaigns 1-2 therefore test a single adventurer, and CaptureLeader gives that lone member one capacity-1 crossbow. A representative calibration party must be defined separately from the minimum launch gate and must include enough nonlethal tools to make the objective mechanically feasible.
- Runtime launch rules correctly allow up to five members, but `OffenseExpeditionPanel` still displays and enforces a maximum of three. This UI/runtime disagreement prevents players from using the designed five-member party and materially distorts combat balance; it must be corrected before outcome calibration is considered representative.
- Party power is the additive weighted sum `Attack×1.4 + Strength×0.8 + Toughness×0.6 + Endurance×0.4 + MoveSpeed×0.25`, multiplied by character combat modifiers. The probe's inverse factor 3.45 is therefore correct only for equal-stat synthetic members; party size still adds an independent 70 HP per member and action-economy advantage that `requiredPower` alone does not encode.
- `EnemyEncounterFactory` currently feeds each target's UI-facing `requiredPower` into enemy health/attack/initiative scaling. This is circular: increasing the recommended party power simultaneously strengthens the enemy and prevents calibration from converging. Enemy campaign strength must use a fixed authored campaign reference, while `requiredPower` remains an independently calibrated player recommendation.
- The six current target values are `10/16/32/42/60/85` with minimum members `1/1/2/2/3/3`. These can seed fixed campaign enemy references without changing current enemy strength, after which recommendations can be changed safely.
- The power sweep proves capture failure is not corrected by raw party power: even at 8x power every capture encounter remains 0%, while several have zero severe casualties. Across every multiplier the capture specialist executes exactly one tranquilizer attack per sample at most, then never performs a second dose; peak sedation never exceeds 0.35 although the incapacitation threshold is 0.70.
- This isolates a real ammunition/action lifecycle problem rather than a hit-chance tuning problem. Capture darts do hit in most later-campaign samples, but the specialist cannot deliver the required second dose. Recommended power must not be increased to compensate for this broken objective path.
- `CombatRangeRules` maps formation distance 9 to `Medium`, and the authored crossbow supports targets through range 18, so range itself does not explain the capture objective's near-zero sedation.
- The outcome probe equips its first capture specialist with `ammo:tranquilizer-dart` and now directly selects the objective leader before general damage sorting. The dart profile is nonlethal with sedation potency 0.35 for 3 turns, so two successful hits before expiry should incapacitate the leader under the cumulative status rule.
- The remaining diagnostic must count actual capture shots, hits and per-turn sedation transitions. End-state sedation alone cannot distinguish target-selection failure, attack misses, status expiry between hits, or ammunition snapshot loss.

## 2026-08-09 - V25 narrative AI handoff authority

- The migration handoff belongs inside `tools/v25_narrative_training` so it is carried into the standalone workspace instead of remaining accessible only from the Unity repository.
- The current completed adapter is authoritative as a completed training artifact but not as a releasable model: its adjudication remains `REJECT_BEFORE_DPO` until a rebuilt dataset and new held-out evidence pass.
- The handoff preserves both sides of the split: training, review and evaluation move out; Unity schemas, DTOs, quality gates, host integration and deterministic gameplay fallback remain in DungeonStory.

## 2026-08-09 - standalone narrative workspace verified

- The standalone workspace is `F:\01_Programming\01_Project\02_Unity\DungeonStoryNarrativeAI`; it contains no `.codex/config.toml`, Unity MCP configuration or copied Unity runtime tree.
- File-level migration verification covered 36 tool files, 66 training-artifact files and the QA report with SHA-256 mismatch count zero before source removal.
- Authored Unity facts remain a deliberate read-only sibling dependency. `build_dataset.py`, `verify_dataset.py` and `apply_human_review.py` now accept `--content-root` and default to the sibling `DungeonStory` repository rather than pretending the AI workspace owns `Assets/Resources/SO`.
- The moved corpus passes the complete 50,000/40,000/38,000/6,000/2,000 contract and all 18 manifest hashes; the standalone reviewer suite passes 6/6.

## 2026-08-09 - AI workspace separation trigger

- The trusted project-local `.codex/config.toml` registers both `unity-mcp` and `dungeon-player` without `enabled = false`, so every Codex session opened against the DungeonStory root loads Unity tooling even for Colab training and dataset work.
- The current thread confirms that the project configuration exposed `dungeon-player` plus two discovered Unity MCP project endpoints. This increases tool context and couples AI-only work to a running Unity environment.
- A clean separation must move only standalone dataset, reviewer, training, evaluation, and notebook tooling. Unity runtime interfaces, static schemas, DTOs, fallback generators, and model packaging/import code remain in the game repository.
- The move destination must be outside `DungeonStory` and must not inherit its project `.codex/config.toml`. Existing Colab notebook and Drive paths need an explicit compatibility update rather than a blind directory move.
- The standalone training tool subtree currently contains 35 files / about 481 KB. It includes dataset generation, verification, SFT training, release evaluation, reviewer UI, Colab notebook, launchers, requirements, and packaging scripts.
- Generated V25 training artifacts currently contain 66 files / about 712 MB. They include raw/filtered/SFT/held-out JSONL archives, 8,000-review exports, manifests, audit reports, and review state; these should live with the AI workspace or external artifact storage, not under Unity.
- `tools/v25_narrative_training/train_sft.py` has 17 lines of existing uncommitted user work. Its move must preserve those changes and must not regenerate or overwrite the file.
- The current artifact inventory confirms that source tooling and generated corpora already form a bounded pair: `tools/v25_narrative_training` plus `Artifacts/Training/V25`.
- Repository path tracing found no Unity C# source dependency on `tools/v25_narrative_training` or `Artifacts/Training/V25`. The direct path dependencies are confined to the training scripts, reviewer documentation, notebook, project design document, and ignore rules.
- The Colab notebook currently assumes a complete DungeonStory checkout (`WORK_ROOT`) and hard-codes both `tools/v25_narrative_training/...` and `Artifacts/Training/V25/...`. It also writes checkpoints to `/content/drive/MyDrive/DungeonStory/V25`; workspace extraction therefore requires a notebook path migration with backward-compatible checkpoint discovery.
- Game-runtime AI integration is a separate retained surface under `Assets/Scripts/Models/AI`, `Assets/Scripts/Services/Character/AI`, `Assets/Scripts/Services/Infrastructure`, facility-evolution integration, UI query code, and `Assets/StreamingAssets/DungeonStoryLlm`. None of these runtime/packaging assets should be moved into the training workspace.
- `Assets/StreamingAssets/DungeonStoryLlm` already contains the host executable, llama.cpp runtime libraries, manifest, and currently mounted GGUF. This is a game distribution surface, not a training artifact directory.
- The previously documented `Artifacts/Review/V25` directory is not present in the current workspace. Human-review state must therefore be located or explicitly treated as absent before the move; the generated review CSVs under `Artifacts/Training/V25/review` are present but are not a substitute for reviewer progress.
- Git currently tracks 43 files across the candidate tool/artifact trees. Existing dirty state includes modified `.gitignore`, modified `train_sft.py`, and untracked `DungeonStory_V25_Colab_Pro.ipynb`, `run_sft_quality_smoke.py`, and the main design document. These cannot be dropped by a tracked-only move.
- The project parent has no existing AI workspace category. The least disruptive default destination is a sibling `F:\01_Programming\01_Project\02_Unity\DungeonStoryNarrativeAI`: it remains near the game but is outside the DungeonStory `.codex` ancestry and can have its own Git/config authority.


## 2026-08-09 - retail offer authority

- The modular facility builder currently authors a concrete retail offer for `tool:field-repair-kit` at a hard-coded 45 gold.
- Runtime applies the facility price multiplier and serving-worker revenue multiplier after that base price, while theft loss also uses the same priced stock cost.
- Retail base prices therefore need a catalog audit against the linked `SaleItem.ItemDefinitionId` EWU and must not remain disconnected literals in content builders.

## 2026-08-09 - current guest revenue paths

- Shop sales consume physical stock and credit `pricedStock.cost`, then apply the serving worker's revenue multiplier before treasury settlement.
- Guest meals consume a physical meal and credit exactly `meal.UnitPrice`; inability to pay produces a social/crime consequence rather than revenue.
- Because item `UnitPrice` is now EWU-normalized at internal cost, meal revenue at exactly 1.0× produces approximately zero gross margin before service labor and facility amortization. Ordinary food service therefore needs an explicit service markup authority to reach the 10–20% net target.
- Shop inventory can have a separate authored/displayed cost, so its margin must be audited against the consumed physical item's EWU rather than assumed from `UnitPrice`.

## 2026-08-09 - service and contract target bands

- The authoritative baseline sets ordinary service net margin at 10–20% and premium service at 20–35%, with higher accident, stock, skill and space burden.
- Recurring faction contracts should consume 1–3% of period production, crisis contracts 3–8%, strategic contracts 5–15%, and reach long-term break-even after 2–4 successful fulfillments.
- The initial money-mutation search located regional supply contracts but did not surface guest/service settlement through filename filtering, so service income likely routes through a different treasury/event abstraction.

## 2026-08-09 - calibrated market economy evidence

- The acyclic recalibration passes the authoritative V23 audit with `failures=0`.
- All 348 ordinary market-sellable positive-EWU items now recover 60.0–60.4% of internal EWU value; below-band and above-band counts are both zero.
- The market-eligible count fell from 357 to 348 because fourteen zero-EWU reproductive/progression items were excluded while five previously non-sellable or non-resource rows are outside the comparable set; no zero-EWU item remains in sale validation.
- All seven external procurement categories remain exactly 0.45 gold/EWU after EWU recalculation.
- Unity Console is Error 0 / Warning 0.

## 2026-08-09 - stable fallback profile design

- Stable fallback intrinsic values can be derived from `ResourceItemKind` plus semantic tags (`Arcane`, `Forbidden`, `Mineral`) while handling remains weight/stack based.
- This keeps high-care rune/medical/finished goods harder to work than waste/raw bulk goods without allowing gold price changes to alter labor time.

## 2026-08-09 - material fallback root cause

- The explicit `MaterialEconomicProfileSO` contract is already acyclic, but the fallback `ResourceMaterialEconomicProfileCatalog.Derive` computes intrinsic value from `log2(UnitPrice+1)`.
- Most current items rely on this fallback; therefore market calibration changed material work factors across the catalog.
- Handling difficulty is already based on physical weight and stackability and can remain. Only fallback intrinsic value must be derived from stable semantic kind/tags or explicitly authored profiles.

## 2026-08-09 - exact circular-authority files

- The material profile catalog and work calculator are isolated in `Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs` and `Assets/Scripts/Services/Economy/V23BalanceWorkCalculator.cs`.
- Other gameplay systems legitimately use `UnitPrice` for trade contracts and cheapest-fuel selection; only construction/crafting work must be prevented from reading that market field.

## 2026-08-09 - circular price/work authority discovered

- Normalizing `UnitPrice` exposed that V23 material handling/work calculation still indirectly depends on the market price field.
- This violates the intended direction `physical material properties + labor → EWU → gold value`; the current implementation allows `gold value → work → EWU → gold value` feedback.
- The many 2–20 WU recipe mismatches are not a reason to revert market normalization. The correct fix is to make material `IntrinsicValue` and `HandlingDifficulty` stable authored physical/economic properties independent of `UnitPrice`, then regenerate work once and audit the acyclic graph.

## 2026-08-09 - zero-EWU builder hooks

- V22 exposes a single `EnsureFiberItem(..., bool seed)` hook, so all four V22 seed lots can be made non-market in one place.
- Research-overhaul item creation has an explicit branch for physical equipment modules and the two zero-EWU progression IDs are known in the spec table, allowing a focused market-disable condition after `Configure`.

## 2026-08-09 - regeneration safety findings

- V19 seeds are authored directly in `V19CropEcologyContentAssetBuilder`; V22 fiber seeds are authored through `EnsureFiberItem(..., seed: true)` in `V22ApparelContentAssetBuilder`.
- The progression-only module and lineage seal are generated by `ResearchOverhaulContentAssetBuilder` as non-recipe finished goods. Their builder must explicitly disable market sale so regeneration cannot reopen the exploit.
- A one-time price calibrator is insufficient unless the audit remains a hard gate: existing builders still contain legacy nominal prices. The checked-in calibrated assets plus enforced audit will make future builder regressions fail visibly.

## 2026-08-09 - zero-EWU classification and normalization rule

- External EWU seeds are currently discovered only from recipe inputs that have no producing recipe. Crop seed lots are agriculture-domain reproductive stock rather than recipe inputs, so they correctly fall outside recipe EWU propagation but incorrectly inherit market sale eligibility.
- `item:equipment-module` and `item:lineage-seal` are unique progression objects, not ordinary wholesale goods. All fourteen zero-EWU items should have automatic market sale disabled rather than receive invented recipe labor.
- Ordinary positive-EWU resource items can use `UnitPrice = round(EWU × 1/3 gold)` and a quantization-corrected sale rate targeting 60% recovery.
- `offense:appraised-valuables` is a dedicated liquidation token and may retain `saleRate=1`; its unit price should instead be authored so full sale equals the same 60% EWU recovery.

## 2026-08-09 - shared price authority path

- `ItemDefinitionSO.unitPrice` is an editor-authored integer used when converting to the runtime item definition; it can be normalized centrally without adding a parallel runtime field.
- `V23EmbeddedWorkValueCalculator` is a runtime-service source file rather than an editor-folder implementation, so its external-leaf rules can be inspected and reused by editor normalization without creating a new assembly dependency.

## 2026-08-09 - measured market recovery distribution

- Of 357 market-eligible items, 343 have positive EWU and only 85 currently fall inside the 50–70% recovery band.
- Distribution is severely inconsistent: minimum 4.8%, p10 22.3%, median 51.4%, p90 129.1%, maximum 539.9%; 167 items are below target and 91 exceed it.
- The largest overpayment occurs on scarce raw resources such as mana crystal, lead ore, sulfur, gold ore, dark resin and rune dust; the largest underpayment occurs on labor-heavy components and tools.
- This proves `UnitPrice` itself lacks a consistent economy authority. Since it is the general item value consumed by multiple gameplay systems, calibrating it from EWU is preferable to hiding the mismatch solely inside hundreds of custom sale rates.
- Target authoring rule: ordinary item `UnitPrice ≈ EWU × 1/3 gold`, with its sale rate adjusted only to absorb integer-price quantization and hit a 60% wholesale recovery target.

## 2026-08-09 - first market audit result

- The report contains 357 sale rows, demonstrating that market eligibility is far broader than the authored economy builder's 0.6-rate resource set.
- Fourteen sellable definitions lack positive EWU: `item:equipment-module`, `item:lineage-seal`, and twelve `seed-lot:*` definitions.
- Special unique items and seeds cannot share ordinary manufactured-item recovery validation without an explicit acquisition-value rule; default market eligibility is currently hiding this missing authority.

## 2026-08-09 - sale audit compile preparation

- The audit now emits one `SALE_EWU` row per market-eligible `ResourceItemDefinitionSO`; recovery-band enforcement stays off for the first measurement pass.
- The newly referenced `MarketSaleEwuRow` value type still needs to be declared beside the existing `DismantleEwuRow` before Unity compilation.

## 2026-08-09 - market authority inventory

- Sellable resource assets almost universally carry the inherited `saleRate: 0.6`; only appraised valuables use `1.0` and unappraised loot uses `0`.
- Because `UnitPrice` is an integer shared by customer payments, fuel/feed choices, medicine, theft and other systems, changing it solely to repair wholesale recovery would distort unrelated gameplay.
- A per-item sale-rate can express wholesale recovery, but the current common 0.6 does not account for each item's EWU. The audit must expose the full recovery distribution before changing authored rates.
- The runtime requests the entire surplus before considering the minimum gold lot. Minimum sellable quantity must be derived before hauling so low-value single items do not occupy the sale buffer forever.

## 2026-08-09 - automatic surplus-sale baseline

- `ResourceStockPolicyRuntime` consumes physical buffer stock and credits `Mathf.Max(1, RoundToInt(amount × UnitPrice × saleRate))`.
- The forced minimum one gold creates a split-transaction exploit for lots worth less than one gold.
- `ResourceItemDefinitionSO` defaults `MarketSaleRate` to `0.6`; market eligibility is `UnitPrice > 0 && MarketSaleRate > 0`.
- The audit must measure only market-eligible physical items and compare sale proceeds with `EWU × 1/3 gold`; the ordinary recovery target is 50–70%.
- Runtime should calculate proceeds before consumption and leave sub-one-gold lots intact until a minimum sellable batch exists.

## 2026-08-09 - Phase 144 recipe calibration resume state

- The previously truncated report patch did apply: `V23BalanceAudit` contains both the `LOW-WORK RECIPE REVIEW CANDIDATES` section and `LOW_WORK` row emission.
- Recipe work still resolves through `V23BalanceWorkCalculator.ResolveProductionProcessClass`; this is the next authority to inspect because current low-end transform/source/sink work remains 4/8/6 WU.
- Whole-worktree Git statistics are temporarily blocked by the existing Git LFS temp-directory access issue, but bounded source searches succeed and no balance source edit was lost.

## 2026-08-09 - recipe process-authority defect

- `ProductionRecipeSO` now stores physical flow role, but it still does not store its actual process class. `V23BalanceWorkCalculator.ResolveProductionProcessClass` reconstructs the class by substring-searching recipe ID, facility tag, workstation tag and work-type ID.
- This can silently misclassify valid recipes (for example steel output implies heavy industry even if the actual operation is cutting, and a source recipe without one of the recognized verbs falls back to washing/grinding). It also makes generated work dependent on naming rather than authored physical process.
- The correct authority is a serialized `ProductionProcessClass` on each recipe, assigned by its content builder. Runtime calculation and audit should read that field directly; string inference may only be an editor migration helper for old assets and must not remain gameplay authority.
- The base-work formula itself matches the V23 contract structurally, but sinks currently force `expectedOutput` to one and can remain very cheap. Disposal needs its own minimum/complexity treatment plus an EWU recovery check rather than arbitrary global inflation.
- `ProductionProcessClass` already exists in `V23CraftingContracts.cs`, so no new enum or save contract is required. The missing step is simply persisting this existing definition authority on `ProductionRecipeSO`.
- Only the resource-economy and research-overhaul builders currently assign the newly added flow role. Workshop content attaches processing details to those same assets, so process-class authoring should be centralized in the two recipe-creation builders and preserved by workshop patching.
- Correction after full builder discovery: `ProductionWorkshopContentAssetBuilder` also creates 24 staged/work recipes in addition to patching legacy recipes. It is a third authoritative recipe-creation path and must author both flow role and process class for its new assets; its legacy patch must preserve or explicitly upgrade existing authority.
- Resource recipes already have semantic construction helpers (`SourceWork`, `Source`, `R`) and authored workstation/work types; these are natural places to require an explicit process class rather than infer it after asset creation.
- Research-overhaul recipes are generated from typed `ItemSpec` records containing item kind, ingredient tags, workstation and physical inputs. The builder should resolve an explicit process class once while authoring the asset, then persist it; runtime must never repeat the inference.
- Workshop recipes include passive/staged fermentation, aging and washing flows. Their process class must remain independent from `ProductionProcessKind` (active versus passive timing), because passive fermentation is still cooking/chemical work rather than a separate work-cost class.
- Resource builder helper boundaries allow a clean migration: `RecipeSpec` can persist the process class; `Source*` can author `Gathering`; `Sink` can author its disposal process; transform helper `R` can require or deterministically map exact workstation semantics at build time.
- Workshop creation currently writes neither flow role nor normalized V23 work for its 24 recipes. The same patch should author `Transform`, persist process class, and recalculate `requiredWork` after workshop supports/water/passive timing are configured so assembly complexity is included.
- The audit appendix/report still calls `ResolveProductionProcessClass`, so changing that resolver to return the serialized authority updates every report path without duplicating logic.
- Because enum value zero is already `Gathering`, a bare serialized field cannot distinguish a genuinely authored gathering recipe from an untouched legacy asset. Add an explicit serialized `processClassAuthored` bit and make audits/runtime fail loudly when false.
- Resource builder currently writes authored legacy work unchanged and does not run the V23 calculator. Normalization must occur after workstation/support configuration, otherwise support/water/passive complexity will be omitted.
- Production worker-stat mapping already consumes `ProductionProcessClass`, so moving it to the recipe SO also fixes worker eligibility/quality authority; this is not merely a report-label change.
- The live catalog uses a bounded facility/workstation vocabulary: basic production tags plus versioned v3/v19/v21/v22 workstation tags and six work types. This makes an editor-only exact semantic map practical; unknown tags can fail asset generation instead of falling through to a cheap default.
- A shared editor authoring resolver is preferable to duplicating three mappings. It should normalize only the mechanical `workstation:` namespace prefix, match complete stable tags, use flow/work type for genuine sources, and throw on every unclassified transform/sink.
- The odd facility tag `m06` must be inspected explicitly before mapping; it is likely a legacy surgery/medical recipe and should not inherit the generic 8-WU fallback.
- `m06` is the prosthetic fabrication station for arm, leg and artificial-eye recipes. These are precision assemblies that produce implant items; they should use `Precision`, not `Medical` procedure work and not the old 8-WU generic fallback.
- The current report predates the low-work diagnostic section. It confirms the quantitative defect (transform min 4, source min 8, four sinks fixed at 6) but must be regenerated after the new authority is materialized.
- Workshop patch coverage is complete at all seven mutation points: legacy patch, new active recipe, new passive batch, existing active recipe, existing passive batch, workstation move and support/water requirement patch.
- `ValidateRecipes` currently calls the calculator without checking authored process authority or authored-versus-calculated work equality. It must report both explicitly so a future builder cannot silently reintroduce stale work.
- Full creation-path discovery found two more production recipe owners: `V22ApparelContentAssetBuilder` and `SurgeryContentAssetBuilder`. The true authoritative set is therefore five builders, not three. Debug-only transient recipes need only be updated if their scenarios exercise V23 process resolution.
- Apparel recipes should author `SpinningWeavingWoodworking`; prosthetic item recipes should author `Precision`. Both builders must normalize calculated work and flow like the other catalog builders.
- Apparel builder creates textile chain recipes with one physical input/output and no special timing, so it can author `Transform + SpinningWeavingWoodworking` directly without using the facility map.
- Surgery builder creates exactly three prosthetic fabrication recipes at `m06`; it can author `Transform + Precision` directly.
- Debug fixtures that only test production bill mechanics currently do not call the V23 work resolver, so they do not need process authority unless a later focused audit invokes the calculator on them.

## 2026-08-09 - recipe runtime-work mismatch

- `CalculateRecipe` applies the weighted material work factor, but every current builder normalization path writes only `CalculateRecipeBaseWork` into `ProductionRecipeSO.requiredWork`. Runtime production therefore still ignores high-value/high-handling material work even though reports show the larger calculated value.
- Material work factors are centralized in `ResourceMaterialEconomicProfileCatalog` over `IGameContentDefinitionSource`. An editor authoring utility can instantiate the same calculator from live profile assets and write the exact final value, avoiding a duplicate formula.
- The audit should require `RequiredWork == CalculateRecipe(recipe)` within rounding tolerance after builders are fixed. This makes the runtime value and the displayed/audited value one authority.
- `ResourceMaterialEconomicProfileCatalog` derives missing profiles from the physical item's unit price, weight and stackability, so full normalization must occur only after all relevant item assets exist.
- The safe builder pattern is two-phase: author recipe shape/flow/process during creation, then run one root-bounded normalization pass after the builder has finished creating items and workshop requirements. This avoids per-recipe catalog scans and includes material factors exactly once.
- A shared editor-only asset content source can load ScriptableObjects under `Assets/Resources/SO`, build the same production calculator used by the audit, and normalize every recipe in a builder-owned root. Runtime remains free of AssetDatabase dependencies.
- Builder completion hooks are available: workshop `EnsureAssets`, research `EnsureAssets`, apparel `EnsureAssets`, and surgery `RebuildAll`. Each can normalize only the recipe root it owns, except workshop deliberately normalizes the whole recipe tree after patching legacy recipes.
- Surgery must normalize before its validation step; apparel/research can normalize after all items/recipes are created and before asset save/research-link validation.

## 2026-08-09 - balance authority enforcement verification

- The dedicated numerical authority is `docs/game-design/whole-game-balance-baseline.md`; the main design document links it at the document header and again in the content-authoring rules.
- Root `AGENT.md` already uses mandatory wording: every value/economy/difficulty/progression change must read and apply that authority before implementation.
- The agent gate explicitly covers facilities, rooms, storage/logistics/power, items/materials/recipes, equipment/apparel/ammunition, research, agriculture/livestock, medicine/disease/aging, combat/defense/expeditions/encounters, guests/events/festivals/factions/captivity, species/traits/families/population, and milestones/endless systems.
- The enforcement is evidence-based: it requires a balance-change record, physical BOM/work/execution/save ownership, anti-dominance and reversible-loop checks, deterministic audit coverage, and forbids claiming balance completion from a formula or catalog count alone.
- Focused integrity checks confirm all three authority/link files exist and `git diff --check` reports no Markdown whitespace error. The only output is the repository's existing LF-to-CRLF advisory.
- The exact `AGENT.md` gate is at lines 16-47. It includes every requested major content family and requires exception approval before implementing an intentional deviation.

## 2026-08-09 current base-model mount

- The gameplay composition was already wired to `LocalLlmRequestQueue`, but the repository had neither a GGUF nor an executable host, so the previous V25 state could only fail closed to deterministic prose.
- The official `ggml-org/Qwen3-1.7B-GGUF` Q4_K_M artifact is `1,282,439,264` bytes and fits the 1.5 GB model contract. Its mounted SHA-256 is `d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5`.
- llama.cpp `b10331` supports CPU inference, OpenAI-compatible chat completions, per-request JSON Schema constraints, prompt caching, API-key authentication, and explicit `--reasoning off`; this permits a fully offline base mount without Ollama or CUDA.
- A listening llama.cpp port is not a readiness signal. During model load `/health` returns HTTP 503, so the Unity launcher must wait for authenticated HTTP 200 before exposing the endpoint. The initial runtime smoke caught this and the corrected smoke passed.
- The current mount is deliberately labeled `base-untrained` and `releaseCertified=false`. It proves runtime integration and fallback behavior, not the creative quality or release eligibility of the future DungeonStory fine-tune.
- Windows child lifetime is protected by the existing kill-on-close Job Object. The real Unity smoke ended with zero remaining `DungeonStoryLlmHost` processes. The stock CPU-server development mount is Windows-only; Linux/Steam Deck still requires the dedicated native host path before certification.

## 2026-08-09 V25 narrative corpus research and generation

- The official `Qwen/Qwen3-1.7B` model card identifies the model as Apache-2.0, multilingual, and capable of a hard non-thinking switch through `enable_thinking=False`. It recommends current Transformers support and separate non-thinking sampling guidance. Source: https://huggingface.co/Qwen/Qwen3-1.7B
- The Qwen license permits derivative works subject to Apache-2.0 notice and attribution obligations; the release pipeline must preserve the upstream license and modification notices. Source: https://huggingface.co/Qwen/Qwen3-1.7B/blob/main/LICENSE
- National Institute of Korean Language search results confirm that dictionary/open-API and corpus assets have source-specific copyright rules. The generator will therefore use NIKL materials only to define lexical categories and validate words; it will not ingest or redistribute example sentences until a specific dataset license is verified. Source index: https://www.korean.go.kr/
- Phase 135 copyright boundary: no modern fantasy/martial-arts novel passages, fan wikis, or franchise proper nouns are copied. All training prose is newly composed from DungeonStory stable facts, controlled motif lexicons, and deterministic templates.
- `우리말샘` exposes an official copyright-policy and Open API entry, so it is suitable as a later word-validation authority but not as an assumed free sentence corpus. Source: https://opendict.korean.go.kr/
- The Academy of Korean Studies encyclopedia organizes folklore around reusable structural categories such as imitation tales, bargains, reversals, oath/ritual, place creation, lineage, and communal memory. Phase 135 uses only those high-level narrative categories; it does not copy encyclopedia prose or named-story plots into examples. Sources: https://encykorea.aks.ac.kr/Article/E0063887 and https://encykorea.aks.ac.kr/Article/E0015531
- Current Hugging Face TRL documentation accepts standard or conversational prompt/completion data and can compute loss only on completions. The generator will retain the rich audit envelope while also exporting a training projection with `prompt` and `completion` fields. Source: https://huggingface.co/docs/trl/en/dataset_formats
- No sufficiently precise data.go.kr result was found for bulk NIKL text reuse in this search round. Absence of a verified license is treated as denial: no dictionary example sentence or corpus sentence enters the generated dataset.

## 2026-08-09 V25 dedicated narrative inference

- The former release queue was coupled to an Ollama endpoint even though gameplay rules already had deterministic owners. The player path now launches only a hash-verified `DungeonStoryLlmHost`; the Ollama adapter is Editor-only.
- Equipment history had two authority leaks: string-fragment evidence classification and `playerVisible` gating mechanical effects. Typed evidence now ranks legal effects, while `mechanicallyUnlocked`, `narrativeReady`, and `uiVisible` are independent.
- Character skills, customer persona multipliers, facility proposal IDs, AI goals/impulses, and social reputation previously accepted model-authored mechanical values. V25 preserves their rule values and consumes only prose/trace; skills and equipment have deterministic offline fallbacks.
- Prefix affinity must include the knowledge and culture versions, not just EventId. Initial 2-4 perspectives now share one static-schema request but remain bound to unique persistent CharacterIds and knowledge snapshots.
- A deployable native host and fine-tuned GGUF are not present in the repository. Runtime and release gates therefore fail closed and this is intentionally not recorded as a completed release-model integration.

## 2026-08-08 V21 actionable alert persistence and dispatch

- `EventAlertChoice` previously persisted only label/description, so every callback-backed choice became inert after save restore. Choices now own a stable `ActionId`; alert records also own a stable `SourceId` so an active authored event projects to one alert instead of one alert per operating day.
- `V21ContentAlertChoiceActionDispatcher` decodes society-event, faction-chapter, faction-contract acceptance, and faction-contract outcome actions, rebuilds the current milestone/world snapshot, and calls the atomic `IContentResolutionService`. Failed dispatch leaves the alert open and does not dismiss the source event.
- Active society events and current faction chapters are now projected through the existing alert UI with persisted action IDs. Successful action choices publish their resolved typed-effect event and dismiss only that actionable alert.
- The alert choice cap is four, matching authored life events and service incidents; the old three-choice truncation silently removed a valid authored choice.
- The event-alert save section schema changed, so its exact section version is now 2. Offline Operation, Presentation, main, and Editor Roslyn compiles pass. Unity live-console verification remains blocked by the pre-existing four unanswered bridge requests.
- The same dispatcher now routes planned reproduction start, due festival resolution, recent funeral handling, counseling, and five age-treatment choices. Age treatment creates the existing persistent surgery order; reproduction starts the existing persistent process.
- Legacy festival attendance had ignored authored facility, item, participant, and outcome fields. `IFestivalCommand.Schedule/Resolve` now grades preparation, reserves exact stacks, applies the result to a detached psychosocial state, atomically consumes supplies, then publishes attendance/mood/grief/faction effects.
- Legacy funeral and counseling calls now require their authored operational facility plus `supply:funeral-preparation-kit` or `medical:trauma-care-kit`; both prepare psychosocial state before atomic consumption and publish only after success.

## 2026-08-08 reproduction hereditary authority correction

- Reproduction had mixed two independent ID domains: parent general `CharacterTraitSO` IDs were written into heritable-trait fields, while child general trait construction attempted to resolve inherited heritable IDs as ordinary traits. This made inherited physiology invalid or inert.
- Parent inheritance now reads expressed/latent IDs from `CharacterNarrativeSnapshot`; child general traits continue to come from its archetype, and the child narrative is registered separately with the inherited hereditary IDs.
- New narrative records with no authored hereditary list receive a deterministic, compatibility-filtered 2 expressed + 1 latent set from the exact 24-definition catalog, so hereditary runtime calculations have real inputs in ordinary runs.

## 2026-08-04 Phase 117 risk-classifier precision

- The first conservative classifier treated any mutable scalar or collection on a presentation `MonoBehaviour` as domain authority, producing false `ReviewRequired` results for ordinary view state.
- Presentation and device-edge paths now suppress mutable-field and local enum/delegate authority evidence, while explicit authority names, SO/content definitions, domain models, and runtime/service/policy roles still force named ownership or review.
- This precision change removes 43 false unapproved findings (`776 -> 733`) without approving a mixed owner by manifest explanation and preserves the plan's rule that genuine `ReviewRequired` sources must be split.
- Remaining presentation reviews are now concentrated in plausible mixed files such as feature query/command services, relocation targeting, detailed stats runtime, popup services, and view files that declare rule/service types.
- The host-owned Unity MCP relay is closed and its direct tool binding returns `Transport closed`, but the Unity package bridge itself is healthy. A project-scoped `relay_win.exe --mcp` session completed MCP initialization, discovered the live Editor named pipe, executed `Unity_ReadConsole`, and reported zero Error/Warning entries.
- The reusable project script terminates only the exact relay child it creates; it never restarts the Editor or synthesizes operating-system mouse/keyboard input.

## 2026-08-04 leaf named-assembly migration checkpoint

- The repository has a very large shared dirty worktree (`1913` changed paths in the current diff summary), so migration selection must be planner-driven and must avoid every concurrently active ownership area named by the root agent.
- This worker may move at most 15 source files, must preserve each original Unity `.meta` GUID, and must stop at semantic-planner/source/diff evidence without opening Unity or touching scenes.
- The local `dotnet` host contains only the runtime and no SDK, so `dotnet build` cannot be used for worker compilation evidence; the root agent owns the fresh Unity compile.
- The preceding strict-save checkpoint converted GrandProject, ResourceStockPolicy, RegionalSupplyContract, Faction, DungeonDebug, and RandomStream to current-version detached candidates with invalid-no-mutation and late-discard fixture coverage.
- `tools/AssemblyMigrationPlanner` uses Unity's bundled Roslyn compiler/runtime, so it remains runnable even though the machine-wide `dotnet` SDK is absent. Its deterministic report orders leaf/sink SCCs first and supports a project-source fallback when no current Bee response file is usable.
- The planner semantic self-test passes. Its input loader explicitly falls back from a stale Bee response to nearest-asmdef project scanning when source moves invalidate the response, which is the required clean/project-scan behavior for this dirty worktree.
- Fresh planner report: `885` Assembly-CSharp candidates, `8079` semantic file edges, `330` SCCs, `19` cyclic SCCs, and only `4` leaf SCCs; graph hash `4f09c016ce001adfb0638c90435c79b0bbf627353c9c12c92f9ee7c03e0a0b53`.
- The four leaf SCCs are single files: `GameDomainContentCatalogSO.cs`, `CharacterActorBridges.cs`, `DungeonFactionDefinitionSO.cs`, and `MetaProgressionRuntime.cs`. The active-area exclusion does not immediately rule out the content-catalog or faction-definition leaves, but semantic boundary inspection is required before selection.
- Migration batch order confirms these four isolated leaves precede one enormous cyclic SCC, so the safe checkpoint should select among the four leaves rather than attempting to split or move the active mega-SCC.
- `DungeonFactionDefinitionSO.cs` is the strongest semantic leaf: one serialized SO type, one Assembly-CSharp consumer (`FactionRuntime`), and egress only to `DungeonStory.Factions`, UnityEngine, and netstandard. Its original script GUID is `2141cf61d65c4574b72b89276d3dd67f`.
- The existing `DungeonStory.Factions` asmdef is currently pure (`noEngineReferences: true`), so putting the SO directly into that core folder would force the pure model assembly to take an engine dependency. Before moving, inspect the existing `DungeonStory.Content` direction or another established SO-domain pattern to avoid degrading the core boundary.
- `GameDomainContentCatalogSO` fans out to seven named domains and risks cycles; `MetaProgressionRuntime` is a VContainer MonoBehaviour with active Offense consumers; `CharacterActorBridges.cs` is empty. None is safer than the faction-definition leaf.
- Existing model-domain precedent supports Unity-aware domain asmdefs (`Economy`, `Species`, and `Wildlife` all use `noEngineReferences: false`). Therefore the faction SO can move into the existing `DungeonStory.Factions` assembly by changing that flag only; it requires no new assembly reference and cannot create an asmdef cycle.
- Repository-wide path search found no hard-coded validator/source-contract path for `Services/Factions/DungeonFactionDefinitionSO.cs`; the V18 validator mentions only the type name in the global runtime-SO synthesis regex. No validator path rewrite is needed for this leaf.
- The source and `.meta` moved together into `Models/Factions/Core`; old paths are absent and the preserved GUID is still `2141cf61d65c4574b72b89276d3dd67f`. The serialized SO now carries `[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]`.
- `DungeonStory.Factions.asmdef` now allows engine references but retains an empty named-assembly reference list, so the migration adds no Assembly-CSharp or cross-domain dependency.
- The post-move planner deliberately rejected the now-stale Bee source list and used `project-fallback`: `1120` candidates, `10646` semantic edges, `566` SCCs, `19` cyclic SCCs, and `106` leaf candidates; graph hash `cfdc4d413f40545208a56960aef58c6d964437f16047d7ab3ad1f2a53041d636`.
- Neither the old service path nor the new named-assembly path appears in the post-move Assembly-CSharp candidate list. A repository path scan excluding generated planner/baseline data also found no live hard-coded old source path, so no validator source-path patch is necessary.
- Targeted `git diff --check` passes. Fresh Unity compilation is intentionally deferred to the root agent as required by the assigned checkpoint boundary.

## 2026-08-03 Batch B survival and medical boundary findings

- A recovered need value is not proof that the exterior-water path ran: safe inventory and facility supplies can satisfy thirst first. The PlayMode fixture must exhaust safe stacks, disable facility supply, place an authored unsafe world source, and assert both source consumption and its health consequence.
- Breakdown execution needs a generation-aware handoff. Accepting a new Aggregate generation while an old coroutine still owns the per-character execution slot can strand the new state unless dispatch happens after slot release.
- Strict persistent building IDs make fixture cleanup part of correctness. A temporary facility created before being registered for teardown can leak after initialization failure and contaminate later scenarios.
- The first safe Medical assembly cut is anatomy content, not the whole surgery model. `SurgeryModels.cs` still mixes immutable definitions with `CharacterActor`, `WildlifeActor`, `BuildableObject`, and code-owned stat IDs, so DTOs and runtime ports must be separated before moving it.
- Splitting a default-assembly MonoBehaviour into partial files can change Unity's MonoScript ownership counts even when runtime behavior is unchanged. Architecture baselines must be reviewed by exact type/path delta rather than updated from counts alone.
- `EnvironmentalFieldRuntime.Tick` originally returned while the pre-run clock was paused before initializing its grid arrays. Owner selection starts the calendar and publishes day-one spoilage immediately, so food lookup could throw before the first unpaused tick. Initializing from the available grid before the pause check removes the startup race without adding a fallback cell value.
- CharacterSummary's generated close button called `OnClose()` directly instead of removing the popup through `IUiPopupService`. The stale stack entry was closed again on the next open after the new actor had been assigned, clearing the binding and leaving visible health controls as no-ops. The button now requests a stack-aware close, and opening closes previous popups before assigning the new actor.
- The reviewed Medical ownership delta is two moved default MonoScripts plus one new Unity adapter source: Unity default MonoScripts decrease `1032 -> 1031`, Roslyn default sources decrease `1051 -> 1050`, while mutable statics and large-constructor counts remain unchanged.

## 2026-08-03 Batch A concrete-runtime assembly audit

- All six concrete implementations currently depend on Unity lifecycle APIs or default-assembly concrete event/domain types. Moving any file wholesale into `DungeonStory.CoreSession` would require a named assembly to reference `Assembly-CSharp`, which Unity cannot support and which reverses the intended dependency direction.
- The valid atomic boundary for Batch A is therefore the six engine-independent Aggregate states, contracts, exact save participants, and duplicate-authority removal. Concrete Unity adapters remain at the edge until the corresponding event, item, wildlife, invasion, building, character, research, and economy ports are promoted during cross-domain closure.
- `ExperiencePacingRuntime` is the closest to portable but still uses `Mathf`, VContainer lifecycle, and a default event DTO. ExternalInfluence, RunFlow, RunVariable, DungeonDebug, and ServiceRooms have stronger concrete dependencies, so a blind asmdef move would be architecture regression rather than progress.

## 2026-08-03 Batch A synchronized cutover dependency shape

- The six components are not equally portable implementations. ExperiencePacing and DungeonDebug are mostly plain Aggregate services; RunVariable is a `MonoBehaviour`; ExternalInfluence and ServiceRooms directly depend on world items, wildlife/survival, power, research, buildings, and characters; RunFlow directly depends on invasion implementations. Moving the six files wholesale into the contract-only `DungeonStory.CoreSession` assembly would create reverse domain dependencies and would not be a valid atomic cutover.
- The shared migration seam therefore has to separate domain state/commands from Unity and cross-domain adapters for all six at once. The `CoreSession` assembly owns immutable component state, commands/queries/results, and transaction participant contracts; Infrastructure/Composition owns MonoBehaviour lifecycle and cross-domain adapter wiring. This is one synchronized shape applied across the set, not a sequence of six independent migrations.
- The current named assembly proves only contract ownership. Runtime implementations remain in default `Assembly-CSharp`, so no component is accepted as migrated despite the six-owner save fixture and clean player build.
- `DungeonStory.CoreSession` currently references only `DungeonStory.World` and forbids UnityEngine, while the existing runtime implementations consume many default-assembly concrete types. `DungeonStory.Infrastructure` references only Foundation and cannot legally host those implementations either. The atomic cut therefore needs explicit ports in the named assembly and composition adapters at the edge; merely adding asmdef references would invert the intended dependency graph.
- The current validator only ratchets three contract types (`IExperiencePacingRuntime`, `IDungeonRunFlowRuntime`, `IDungeonDebugModeService`) into CoreSession. ExternalInfluence, RunVariable, and ServiceRooms contracts are still default-assembly declarations, so the executable cutover matrix must extend the assembly check to all six components and distinguish domain implementation from Unity adapter ownership.
- ExternalInfluence's save DTO is primitive-only, but its query snapshot leaks `Vector2Int`; ServiceRooms mixes pure session records with `BuildableObject`/`CharacterActor`; RunVariable mixes state, Unity `Mathf`, localized presentation text, authored definitions, and effect interfaces in one model file. These types must be separated by role before their contracts can move without pulling Unity/presentation and concrete world entities into CoreSession.
- The existing `DungeonStory.World` primitive file itself imports UnityEngine and exposes `Vector2Int`, so using it as the CoreSession boundary does not make all state engine-independent. Batch A should introduce a primitive, serializable cell value owned by CoreSession (or Foundation) for external-raid state rather than widening the no-engine assembly to Unity types.
- Five of the six components currently retain the authored `CoreSessionRulesSO` asset directly. RunVariable already consumes a root-catalog-derived definition catalog. A shared immutable `CoreSessionRulesDefinition` projection created once by `ResourceGameContentCatalog` is therefore the first legitimate six-component content seam: five consumers stop retaining an SO, while RunVariable remains on the same root-derived definition pattern.
- The SO is already validated before `ResourceGameContentCatalog` becomes usable. Copying rehearsal bands, incident kinds, service research requirements, thresholds, costs, and limits into immutable read-only collections at catalog construction preserves authored authority while preventing runtime asset mutation and direct SO coupling.
- The architecture analyzer correctly rejected a single 18-parameter rules constructor. Matching the SO's authoring sections with three immutable runtime value objects keeps constructor limits intact and makes each rule family's ownership explicit without changing consumer-facing semantics.
- After the split, the root catalog/content proof, six-component save/integration fixture, V18 authority validator, Roslyn metrics, and Unity Console all pass together. This proves the shared content seam, but does not prove the remaining runtime/command/save/composition/presentation/legacy-removal matrix cells.
- ExternalInfluence's `Restore(..., DungeonGameRestoreReport)` parameter is unused; validation already belongs to its save section. Removing that save-framework parameter from the runtime port lets its enums, DTO, command/query contract, and a primitive cell snapshot move to CoreSession with only a Foundation dependency for `DomainFailure`.
- RunVariable save capture is currently duplicated inside `RunVariableSaveSection`, while restoration reconstructs runtime objects there and the section locates the MonoBehaviour through `DungeonSceneRuntimeReferences`. A CoreSession `IRunVariableRuntime` capture/restore port can make the save section depend on a real runtime boundary and remove this scene-reference bridge.
- Service domain enums (`ServiceCategory`, operation modes, stage mask) are pure but live beside Unity building abilities/SOs. Moving those enums and pure session records to CoreSession while leaving `BuildableObject`/`CharacterActor` request/view adapters at the Unity edge is the corresponding synchronized contract cut.
- CoreSession can reference Foundation without importing Unity APIs into its own source. That enables External/Service command results to reuse the single `DomainFailure` protocol while the asmdef retains `noEngineReferences: true`.
- Replacing RunVariableSaveSection's `DungeonSceneRuntimeReferences` dependency with `IRunVariableRuntime` removes a real composition-time locator. Capture/restore conversion now belongs to the runtime state boundary; the save section keeps only canonical payload validation and authored-reference validation.
- The synchronized contract move compiles with no Unity errors or warnings: External enums/DTO/query/runtime state, RunVariable difficulty/survival/category/save DTO/port, and Service enums/session/save/query contracts now load from `DungeonStory.CoreSession`; Unity building/character adapters remain at the default edge.

## 2026-08-03 Batch A integrated transaction boundary

- A meaningful vertical batch needs more than six green unit checks. The new fixture constructs the six production runtimes on one event bus and one `DungeonRuntimeAggregateRootStore`, performs cross-owner day progression plus External, RunVariable, Debug, and Service mutations, captures all six sections, and exercises presentation localization in the same call.
- Preflight rejection and late commit failure prove different invariants. Invalid ServiceRooms JSON must stop before any owner restore; a valid six-owner candidate followed by a failing final section must reach the final commit and then leave every live snapshot plus the published root revision unchanged. Both are now explicit production-registry scenarios.
- Test fakes that store state in private fields cannot prove rollback-free behavior because their fields sit outside the candidate Aggregate. The transaction fixture's owner fakes write their DTO types through the shared root, while RunVariable uses its actual Aggregate; candidate discard is therefore observable rather than inferred.
- `CoreSessionRulesSO` must cover every future day because ExperiencePacing intentionally throws when no band matches. Requiring the last band to end at `int.MaxValue` turns that runtime assumption into an authored-content validation rule. Concurrent incident count is independent of the number of incident kinds, so no artificial cross-field cap is valid.
- `DispatchProxy` is used only inside the Editor integration fixture to supply unused dependency surfaces. Production still receives explicit concrete capabilities through composition; the proxy cannot become a runtime Null Object or content fallback.

## 2026-08-03 Batch A command and presentation authority

- ExternalInfluence and ServiceRooms were still dual-purpose APIs: a domain mutation returned a localized Korean sentence, then UI, activity logs, and save-adjacent state copied that sentence as if it were a stable reason identifier. `DomainFailure` is now the only failure authority at those command boundaries; localization is presentation-only.
- Service availability had the same duplication in query form through `BlockedReason`, while mode changes embedded a success/failure `Message`. A query snapshot needs structured blockage state and a command result needs only success plus a failure code; success copy belongs to the presenter.
- Service-room link ordering still synthesized a key from legacy facility number and coordinates when a persistent ID was absent. That made service topology an exception to the V18 identifier contract. Requiring the typed building instance ID is safe because industrial topology and service hub IDs already use the same required identity path.
- Grouping font and failure-localization presentation dependencies as a top-level class created one additional default-assembly MonoScript even though no new file was added. Nesting the dependency value under the existing panel retains constructor grouping without expanding Unity's default MonoScript surface.
- The executable localization validator enumerates every `FailureCode`; adding a domain code without both Shared Table and Korean table entries now fails V18 immediately. The new command boundary therefore cannot silently regress to an untranslated code.

## 2026-08-03 Batch A scoped debug-rule ownership

- `DungeonDebugRuntimeRules` was a hidden global authority despite its `static readonly` wrapper: it retained a mutable mode-service reference and thread-static command depth, and 31 gameplay call sites could read it without declaring the dependency.
- The correct boundary is one scoped `DungeonDebugRuleRuntime`, with `IDungeonDebugRuleQuery` for gameplay reads and `IDungeonDebugRuleRuntime` only for command-depth mutation. ScriptableObject building conditions receive this capability through `BuildingConditionContext` rather than retaining runtime services.
- Explicit dependency routing exposed an existing eight-dependency `WorkOrderRuntime`; grouping workforce, clocks, and debug rules into `WorkOrderExecutionServices` reduced the large-constructor violation set instead of adding a ninth dependency.

## 2026-08-03 Batch A production-count ratchet

- The V18 validator enumerates top-level public production save sections but previously only checked staged-interface presence; it did not enforce the known total or rollback-free conversion count. Batch A can now ratchet `54 total / 32 rollback-free / 22 remaining`, making the plan's `28 → 22` exit counter executable.

## 2026-08-03 Batch A fixture construction strategy

- RunVariableRuntime is a MonoBehaviour whose section constructor only requires the component reference; strict preflight does not touch its uninjected Aggregate when the canonical DTO represents an unstarted run. A temporary GameObject plus empty authored catalogs can therefore test exact-version validation and invalid no-commit behavior without constructing the entire gameplay graph.
- The other five sections accept interface runtimes and can use counting fakes. One central callable Editor suite can prove canonical restore, invalid report/no restore call, required/preflight/rollback-free marker contracts, and the absence of optional-section interfaces for the entire Batch A set.

## 2026-08-03 Batch A V18 ratchet location

- `RuntimeAuthorityV18Validator.ValidateOrThrow()` keeps source-contract ratchets inline near the StaffDiscontent block. Add all six Batch A typed/marker/version/fallback prohibitions there, plus one callable Batch A strict-boundary fixture requirement, so future regressions cannot silently restore optional/migrating paths.

## 2026-08-03 Batch A fixture version impact

- PlayerFairness hardcodes ExternalInfluence V2 and constructs a scheduled raid without warning/sequence/current-day state. Update it to canonical V3 so its DTO round-trip remains representative of a payload accepted by the strict boundary.
- ServiceRoom's success log is the only hardcoded `service.rooms V1` text; the mapper scenario remains valid after switching to exact field copying, but the message and explicit payload-version assertion must reflect V2.

## 2026-08-03 Batch A post-edit call-site findings

- `IServiceProcessCatalog` intentionally exposes only `TryGet`; ServiceRooms validation must not assume a throwing `Get`. Production constructs `ServiceSessionRuntime` and all six sections through composition, so the added required dependencies are resolved by existing registrations rather than manual factories.
- `FirstRunObjectivePlayModeVerifier` is the only direct RunFlow reset call and must now pass explicit `bossCycle: 0`, matching the new non-legacy exact restore contract.

## 2026-08-03 Batch A staged cross-reference confirmation

- The generic section base validates JSON against the live world first, then commits staged sections in dependency order. Facility commit publishes a detached facility candidate to `RestoreWorldCandidateIndex`; Character commit consumes that Grid and publishes detached characters; ServiceRooms runs afterward and can validate saved hub/actor IDs against both candidate lists before replacing its own Aggregate root.
- Commit-time candidate validation remains rollback-free because all earlier world changes are detached transaction candidates and final live publication occurs only after every section commit succeeds. ServiceRooms must add errors and return before its root swap when candidate references are missing, never fall back to live registries or skip individual sessions.

## 2026-08-03 Batch A ServiceRooms candidate-reference strategy

- Save preflight runs before detached character/facility candidate creation, so validating session hub/actor IDs against the current live `IBuildingWorldQuery`/`ICharacterLifetimeQuery` would reject valid loads into a fresh world or validate the wrong world. Cross-aggregate references must use the existing restore-candidate index during staged preparation or the global aggregate reference preflight, not live runtime queries.
- `RestoreWorldCandidateIndex` already publishes detached facility and character candidate views and is used by AI lookups. ServiceRooms can structurally/authored-validate during ordinary preflight, then resolve hub/actor references against `IRestoreWorldCandidateQuery` during its stage after its declared world dependencies have prepared candidates.
- `ServiceSessionAggregateState` is a small replaceable root containing mode/session dictionaries, advertised categories, and a revision. Exact restore should construct this root without trimming/skipping; post-publication hub subscription is already revision-driven and staging-aware.

## 2026-08-03 Batch A call-site and fixture impact

- Batch A sections are composition-injected rather than manually instantiated in production, so dropping ExperiencePacing's obsolete RunFlow constructor dependency is low risk. Public DTOs and runtime methods are used by Editor fixtures, which must be updated in the same source batch.
- `IsFinalInvasionDefended` survives only in `NaturalRunPlayModeVerifier` and the RunFlow save path. The verifier already has authoritative evidence (`bossFightObserved`, defense trigger count, `!IsBossActive`), so the dead property can be removed without replacing it with another saved flag.
- `ServiceRoomDebugScenarios` hardcodes `service.rooms V1` and currently tests the lossy `ToSnapshot` mapper with a synthetic process ID. The fixture must move to the new exact payload version and add validator/no-mutation coverage using authored process/hub/actor references or a dedicated validator dependency seam.
- `DungeonDebugModeDebugScenarios` already exercises the 50-entry cap and transient reset, but it calls runtime restore directly. Add section-level exact-version, invalid-history, and staging-event suppression proof rather than treating this existing gameplay test as sufficient save preflight evidence.

## 2026-08-03 Batch A Experience/External final audit

- ExperiencePacing has a clean plain Aggregate but both runtime and section currently repair missing data, clamp masks/day, skip unknown concepts, and synthesize a missing section from RunFlow. V18 can remove the RunFlow dependency from the constructor, make the section required typed V1, validate mask/subset/active-day/concept order invariants, and perform one exact root replacement.
- ExternalInfluence clones its DTO, clamps scalars/days, trims/deduplicates both ID collections, supports V1 migration, and resets when missing. The section can become required typed and rollback-free once exact current-version validation rejects every repair case and restore only copies validated values.
- `ExternalInfluenceAggregateState.EcologyResolutionReported` is mutable gameplay state used to distinguish Resolved from Inactive, but it is not included in `DungeonExternalInfluenceSaveData`; every save/load currently loses this state. Move it into the versioned DTO (or otherwise persist it) and bump the section payload version instead of falsely claiming an exact V2 round trip.
- External ID lists are already captured sorted. Strict validation should require nonblank trimmed unique ascending IDs. Dread boss/affected IDs require active defense; armed and active are exclusive. Ecology scheduled/in-progress/resolved states are mutually exclusive, scheduled requires positive remaining time, inactive/non-scheduled states require zero remaining time, and active raid state requires positive sequence plus a warning.

## 2026-08-03 Batch A strict-section implementation pattern

- The existing `DungeonJsonSaveSection<T>` already rejects blank/invalid JSON, exact-matches section versions by default, runs typed validation before staging, and captures one immutable payload reference for commit. Batch A manual sections should inherit it instead of duplicating deserialization/stage plumbing.
- The proven `StaffDiscontentSaveSection` pattern is: embedded DTO `CurrentVersion`, constructor-required runtime, deterministic capture ordering, exhaustive typed validator, lossless snapshot mapping, plain Aggregate replacement, and `IDungeonRollbackFreeSaveSection`. Batch A should reuse this shape without adding compatibility migrations in V18.

## 2026-08-03 Batch A RunFlow invariants

- RunFlow canonical phase is fully derived: unfinished runs use Preparation for days 1–3, Growth 4–9, Escalation 10–29, and EndlessDefense from day 30; Victory/Defeat always use Finished. Restore currently ignores the saved phase and recomputes it, so exact preflight must verify this equality and then assign the validated phase directly.
- `finalInvasionDefended` is a dead legacy projection: runtime always reports false and the Aggregate has no field, while restore uses it only to promote `bossCycle`. It should be removed in a new section schema together with the optional legacy restore parameter and interface property, rather than retained as a second representation.
- Boss armed/active are transient mutually exclusive unfinished-run states. `bossCycle` is nonnegative, cannot exceed `ResolveBossCycleForDay(currentDay)`, and armed/active require a positive cycle; finished runs require both flags false. These conditions prevent current restore clamps/filtering from changing validated payloads.
- RunFlow already replaces a detached Aggregate and suppresses threat/director/owner projection during staging. After strict typed preflight and exact field assignment it qualifies as rollback-free; projection remains a post-publication responsibility.

## 2026-08-03 Batch A RunVariable model invariants

- `RunStartVariableSnapshot` normalizes difficulty/survival enums, trims species/layout/doctrine strings, clamps threat multiplier to at least `0.05`, and copies candidate lists. Strict preflight must require values already canonical (`value == Trim()`), defined enums, finite threat `>=0.05`, and non-null candidate lists so construction is lossless.
- `RunVariableAggregateState` clamps zero seed and day below 1; `ActiveRunVariable` clamps start/remaining values; `RunVariableState.Restore` filters null, non-Operation, and expired entries and narrows invasion definitions to the Invasion category. Validation therefore must require nonzero run seed, current/start day `>=1`, active remaining days `>=1`, unique known Operation definitions, and an empty or known Invasion definition before root replacement.
- Operation activation replaces an existing variable with the same definition ID and appends the new entry, so a valid runtime capture has unique operation IDs but not necessarily lexical order. Preserve list order exactly rather than imposing an unsupported sort; canonicality here means unique, non-null, authored entries in runtime order.

## 2026-08-03 Batch A RunVariable initial invariants

- `RunVariableSaveSection` is nominally V2 but the DTO has no embedded version and still accepts/mutates V1. Capture contains a dead `runtime == null` fallback despite constructor-enforced runtime presence; restore also contains an impossible runtime-null warning branch. Both must be removed under required dependency construction.
- Restore currently synthesizes missing start/list data, resolves a missing doctrine from species, skips unknown operation definitions, defaults a zero seed to 1, drops nonpositive random maxima, then reseeds and advances the shared random stream. This is lossy and makes `run.variables` a second RNG authority beside the dedicated random-stream save section.
- The clean V18 boundary should require one exact current DTO, validate every authored variable/doctrine reference and all nested lists before staging, replace the plain `RunVariableAggregateState` once, and remove reseed/replay side effects. The obsolete `randomDrawMaxima` replay field and unused legacy `difficulty` field are candidates for a section-schema bump/removal after fixture/call-site audit.

## 2026-08-03 Batch A DungeonDebug/ServiceRooms invariants

- `CreateContract` guarantees defined hub mode, known mask from the authored mode contract, finite nonnegative reception/waiting/payment/cleanup durations, strictly positive finite service duration, nonnegative price, payment/internal flags, and nonblank support IDs. Satisfaction is not clamped and therefore should only require finiteness. Support IDs are emitted in hub-link order, so strict validation must either prove that query order is canonical or canonicalize capture before requiring sorted order.
- ServiceRooms capture persists only active sessions ordered by `StartedAt` then `SessionId`; Completed/Cancelled payload entries are invalid. Its restored snapshot should preserve every field byte-for-byte after preflight, with no trimming/defaulting, and the runtime command boundary should reject stages absent from the active-stage mask so future captures remain valid.
- DungeonDebug save state contains only `debugModified` and the most recent command history. Capture preserves list order and caps it at 50; exact V1 validation can require a non-null list of at most 50 non-null entries with non-null strings. Runtime-generated text is not constrained enough to justify trimming, parsing, or arbitrary length repair.
- DungeonDebug owns a detached `DungeonDebugModeState`; restore can be rollback-free after replacing its candidate root exactly and suppressing `StateChanged` while staging. Overlay/cheat transient state is intentionally not part of the payload and remains reset by the new root.

## 2026-08-03 Batch A ServiceRooms contract follow-up

- Service process masks contain only Reception/Waiting/Service/Payment/Cleanup, and authored contracts expose nonnegative stage durations/base price, satisfaction, and required feature tags. Hub modes/categories/payment policies are closed enums. Strict save validation can reject unknown mask bits, non-finite or negative durations, negative prices, undefined enums, and noncanonical support IDs without reproducing gameplay calculations.
- The current save captures only `ActiveSessions`, so persisted Completed/Cancelled sessions are noncanonical even though the DTO enum can represent them. Active session stage should match the contract mask, but runtime currently allows arbitrary non-Completed `TrySetStage`; that runtime command boundary must be tightened or the save contract cannot guarantee stage/mask coherence.

## 2026-08-03 Batch A strict-save audit

- Service session creation requires an operational hub, a catalogued process supported by that hub with matching category/owner tag, capacity, one active session per actor, and a contract for the hub mode. A saved session therefore must preserve a nonblank unique `service:*` ID, matching hub/process/category, defined stage/mode/mask values, finite timestamps with `stageStartedAt >= startedAt`, and a non-null contract; these checks belong in preflight rather than restore-time dropping.
- Runtime completion is legal only from Service/Payment/Cleanup, commits payment at most once, and cancellation supplies a nonblank reason. `TrySetStage` currently permits arbitrary non-Completed stages, including Cancelled, so the save validator must enforce only invariants actually guaranteed by runtime rather than inventing a stricter transition graph. Contract numeric bounds and active-stage membership still require confirmation from `CreateContract` and the authored process definitions.
- ServiceRooms capture already canonicalizes hub IDs, advertised categories, and active sessions, while restore currently trims identifiers, defaults contracts, and silently drops inactive hubs, missing actors/processes, invalid sessions, and duplicate session IDs. Strict preflight must validate the exact authored hub/actor/process references and canonical hub/session ordering before the existing detached `ServiceSessionAggregateState` swap; no record may be repaired or skipped during restore.
- `ServiceRoomsSaveData` persists hub mode/category lists plus full session identity, actor/process/category/stage/timing/advertising/payment/cancellation/contract state. The remaining audit must derive contract and stage invariants from `ServiceSessionModels` and transition code before assigning the rollback-free marker.

- ExperiencePacing transition details confirm completed rehearsal bits remain scheduled; therefore `completed ⊆ scheduled`. An active rehearsal bit must be scheduled and not completed, and any active rehearsal introduces the Defense concept.
- RunFlow DTO persists `finalInvasionDefended`, but the Aggregate has no such field; the runtime derives it from boss-cycle state. V18 payload validation must require this legacy projection to equal its canonical derived value or remove it with a new exact DTO version; it cannot be allowed to raise `bossCycle` during restore.
- ExternalInfluence flag hierarchy from production transitions: armed and active are mutually exclusive; boss and affected-intruder IDs require active defense. Ecology scheduled/in-progress are mutually exclusive; scheduled requires positive remaining time, both active raid states require positive sequence and warning-issued, and inactive state requires zero remaining time. Current/mitigation days start at -1 and mitigation cannot be later than current day.
- ExternalInfluence scalar bounds are renown/dread/scouting `[0,999]`, hostile rumor/ecology `[0,100]`, last exposed-food pressure `[0,20]`, and finite nonnegative last weather pressure (generated values currently 4/8/12). These exact runtime-generated ranges can replace restore clamps.
- Existing regression surfaces are uneven: RunVariable and DungeonDebug expose `RunAll(bool)`, while ExperiencePacing and ServiceRooms expose menu-only `Run()`, and ExternalInfluence has no dedicated fixture. Batch A should normalize these to callable boolean suites or add one batch coordinator that invokes focused strict-boundary helpers without duplicating gameplay setup.
- ExperiencePacing's only rehearsal bits are days 10/20/30 (`mask 0b111`), and introduced concepts are captured in numeric order. Strict payload should require exact V1, current day ≥1, masks within 0–7, completed subset of scheduled, active day in {0,10,20,30} with coherent scheduled/completed membership, and sorted unique defined concepts.
- RunFlow restore already suppresses projection while staging, so root replacement can be rollback-free. Its public restore currently ignores the persisted phase and obsolete `finalInvasionDefended`; the V18 DTO should remove or require the legacy field false, validate phase as the exact day/outcome-derived value, and restore the validated fields without clamps.
- ServiceRooms already replaces a plain `ServiceSessionAggregateState` and suppresses hub subscription while the aggregate store is staging. Its restore remains lossy (null/default lists, trimmed/skipped hubs and sessions), so strict validation can make the existing root swap rollback-free; authored hub/actor/process references must be checked through its existing world/catalog dependencies.
- DungeonDebug also owns a replaceable plain `DungeonDebugModeState`, but restore invokes `StateChanged` after replacing the candidate root. During staging that event can leak presentation side effects; rollback-free conversion must suppress the event while `aggregateRootStore.IsRestoreStaging` and validate/copy the bounded command history exactly.
- RunVariable restore replaces a plain `RunVariableAggregateState`, but then reseeds the shared `IRandomStreamProvider`; that is an external side effect and conflicts with rollback-free publication because random-stream persistence has its own save authority. Batch A must remove RunVariable's restore-time RNG mutation and let the random-stream section own stream state.
- RunVariable capture/restore still contains impossible runtime-null skips despite constructor enforcement, V1 migration, doctrine fallback resolution, missing-list defaults, and unknown-definition warnings/skips. Exact V2 preflight must validate authored doctrine/variable references and canonical collections so valid restore constructs the root without fallback.
- ExternalInfluence payload contains bounded continuous pressures, raid/defense state flags, days/sequences, and two canonical ID sets. Current restore clamps every range and trims/deduplicates both sets; preflight must validate those exact bounds, flag hierarchy, finite values, nonnegative timers/sequences, canonical sorted unique IDs, and then copy without normalization.
- RunFlow already stores a replaceable `DungeonRunFlowAggregateState`, but `RestoreState` recomputes phase from day/outcome and clamps boss cycle rather than honoring the DTO. Strict validation should require the serialized phase/flags/cycle to match the runtime state machine, after which restore can replace the root without lossy derivation.
- ExternalInfluence already owns a replaceable plain `ExternalInfluenceAggregateState`; capture canonicalizes its two ID sets. Restore still accepts null/default clones and reports version errors after entry, so strict section preflight can make publication a single safe root swap.
- `DungeonRunVariableSaveData` currently has no embedded DTO version despite section version 2, while RunFlow also hardcodes section version 1 without a DTO version field. Batch A should add exact payload versions so same-section balance/schema changes cannot silently accept defaulted JSON.
- `RunVariableSaveSection` already uses the typed base but still accepts V1 migration, fills missing nested lists/start data, warns/skips unknown definitions, and has no rollback-free marker. V18 can require exact V2, authored catalog references, canonical order, and lossless detached Aggregate replacement.
- `DungeonDebugSaveSection` is a required presentation-phase section but manually stages default DTOs and mutates the debug-mode service directly. Its payload is small; strict conversion depends on confirming whether the debug service owns a replaceable plain root or still projects live state.
- `ServiceRoomsSaveSection` uses the typed base but has no strict validator/marker/version field. `ServiceSessionSaveData.ToSnapshot` trims IDs, drops invalid sessions, and defaults contracts; these lossy transforms must move behind exact preflight, with canonical hub/session ordering and valid process/actor/contract state.
- `ExperiencePacingSaveSection` is optional and manually staged. It accepts blank/malformed JSON as defaults, supports missing-section synthesis from RunFlow, and runtime restore clamps days/masks plus skips unknown concepts. It must become required exact-version typed preflight and publish only a validated plain Aggregate root.
- `ExternalInfluenceSaveSection` is optional, accepts V1-to-current migration, fabricates default DTOs, and resets state when missing. Batch A must reject legacy/missing payloads in V18 and validate the current DTO before a detached root replacement.
- `RunFlowSaveSection` is required but manually staged; it fabricates an empty DTO and clamps day/cycle during commit. It is a plain-state candidate for the shared typed JSON boundary once phase/outcome/flag/day/cycle invariants are preflighted.

## 2026-08-03 throughput-plan correction

- Phase 112 now distinguishes completed foundations from remaining work and is the sole active ledger. Non-save work is batched independently across atomic publication, executable architecture metrics, three asmdef waves, authored static/session closure, three responsibility-decomposition waves, UI boundaries, localization, content/duplicate authority audit, integrated save proof, and final gameplay/UI verification.
- Historical unchecked tasks in Phases 89–107 duplicated these same scopes. They are retired as planning entries and point to their authoritative Phase 112 batch, preventing completion counts and future agents from double-counting stale work.
- `CharacterSummeryInfo` has already been renamed to `CharacterSummaryInfo`, and the project already has `FailureCode`, `DomainFailure`, combat equipment/module adoption, String Table assets, and a V18 coverage validator. The remaining localization batch is an adoption/closure pass across other domain APIs, not foundation creation.
- There are 74 production C# files over 800 physical lines. This is an upper bound, not 74 proven violations: the final Roslyn gate must aggregate partial class declarations and apply 800 only to MonoBehaviour/Presenter and 1,200 to other runtime classes.
- Production `Bind*Runtime(...)` call sites are already `0`; late binding is a completed ratchet, not remaining work.
- No production C# file currently exceeds the 1,200-line runtime hard limit, but many files remain between roughly 1,035 and 1,099 lines. File length alone cannot prove the stricter 800-line MonoBehaviour/Presenter limit because partials and non-MonoBehaviour owners differ; the architecture-test batch must add a Roslyn class-kind/aggregate-line metric before defining the exact decomposition queue.
- Current large-file leaders include `WorkTaskExecutor`, `SurvivalFoodRuntime`, `CircusRuntime`, `FacilityInstanceEvolutionRuntime`, `Shop`, `DungeonGameplayPerformanceProbe`, `EquipmentEvolutionRuntime`, `SurgeryRuntime`, `AIBrain`, `ConveyorRuntime`, `AnimalHusbandryRuntime`, `ProductionBillRuntime`, `WildlifeActor`, and `Grid`. Several match the original priority list, while earlier Phase 110 already closed the global 1,200-line gate.
- Live measurement shows optional required-interface dependencies are already `0`, so this is a maintained ratchet rather than remaining implementation work. It must not be presented as an unfinished batch.
- The current reflection static-field query returns `3,110`, but it includes compiler-generated mutable caches in gameplay assemblies and is therefore not an actionable source violation count. The non-save plan must first replace this noisy reflection metric with the requested Roslyn/source allowlist rule, then batch only real authored mutable runtime state.
- Unity reports `1,039` top-level MonoScript gameplay types still loaded from `Assembly-CSharp`. Existing domain asmdefs cover Foundation, Infrastructure, Presentation, AI, Buildings, Characters, Combat, Invasion, Items, Offense, Rooms, Survival, Wildlife, Work, World, and Evolution, but the default-assembly migration is still a large concrete track.
- The user's follow-up is correct: save owners were detailed, while all non-save work remained compressed into four broad lines. The plan also contains older duplicated unchecked items across Phases 89–107, so Phase 112 must explicitly supersede them and provide one authoritative remaining-work ledger.
- `RuntimeAuthorityV18Validator` already exposes exact live queries for optional required-interface dependencies and mutable runtime static fields. These should become numerical batch gates instead of vague DI/static cleanup statements.
- `Assets/Architecture/runtime-architecture-baseline.json` currently has zero approved oversized-file violations. Remaining decomposition must therefore be driven by the named priority classes and hard line/dependency limits, not by carrying a nonzero waiver baseline.
- The original save-batch partition was a migration plan, not the current counter. The source ratchet now requires all 54 production sections to be strict rollback-free with an empty remaining set; loaded Unity acceptance of the complete 54-section graph is still pending.
- The previous one-owner loop repeated audit, compilation, Unity reload, V18 validation, Console inspection, and planning-file writes at the smallest unit. The revised plan preserves strict per-owner acceptance but amortizes tooling and documentation at the batch boundary.
- The save-owner batches do not redefine completion: each owner still needs exact versioning, required typed preflight, canonical lossless restore, rollback-free publication, invalid no-mutation proof, and no lossy restore fallback.

## 2026-08-03 StaffDiscontent strict invariant audit

- 전환 완료 후 Unity AppDomain에서 `StaffDiscontentSaveSection`이 rollback-free marker를 실제 구현하며 운영 비-marker section 수가 29에서 28로 감소했다. strict fixture와 V18 authority가 모두 PASS했고 Console은 0/0이다.
- RegularCustomer 선례처럼 이 규모의 검증은 별도 validator 타입 없이 section의 `ValidatePayload`에 둘 수 있다. V18 ratchet은 section의 marker/version/canonical ID/status hierarchy와 runtime의 clamp/default 제거를 고정하면 된다.
- `RegularCustomerDebugScenarios.VerifyStrictSaveBoundary`가 요구하는 검증 형태를 확인했다: source capture → target restore → exact recapture, preflight/rollback-free/required interface 검사, 변형 invalid JSON restore 실패 후 exact state 보존이다. Staff fixture도 동일 형식을 적용한다.
- V18 source ratchets는 실제 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`의 최근 strict section 묶음 뒤에 이어 추가해야 한다.
- DTO에는 아직 버전 필드가 없고 section은 공용 typed JSON base를 쓰지만 validator/rollback-free marker가 없다. exact V1 필드와 `ValidatePayload`를 추가할 수 있는 단순 경계다.
- Capture는 이미 staff ID를 ordinal 정렬한다. Restore의 null-list 기본화, null/blank skip, trim, 중복의 restore-time report는 모두 preflight로 이동하고 valid 경로는 목록을 그대로 snapshot으로 변환해야 한다.
- 저장된 `outcome`은 capture 경로가 항상 `None`으로 내보내며 runtime record에는 보관되지 않는다. strict payload는 `None`만 허용해야 손실 없는 왕복이 된다.
- `LocalRebellion` 진입은 `permanentLoss=true`, `localRebellion=true`, `LocalRebellionDays=1`을 만든다. `Departure`는 `permanentLoss=true`, `departed=true`를 만든다.
- 격리는 반란 상태에서만 성공하고 `isolated=true`, `ownerThreat=false`가 된다. 진압은 반란 상태에서만 성공하고 `suppressed=true`, `localRebellion=false`, `ownerThreat=false`, `permanentLoss=true`가 된다.
- 현재 restore는 display name 기본화, mood/day clamp를 수행하므로 preflight가 canonical text, enum, finite mood `[0,100]`, nonnegative days, status hierarchy를 먼저 보장해야 한다.
- 실제 fixture용 `CharacterSceneRuntimeReferences` 생성 경로는 공용 Editor 의존성 helper와 scene reference 생성자에 이미 존재한다. 다음 조회에서 생성자 계약과 기존 Staff fixture wiring만 좁게 확인한다.
- `CharacterSceneRuntimeReferences` 생성자는 8개 런타임 참조를 받지만 Staff section은 `StaffDiscontent`만 요구한다. fixture에서는 실제 Staff runtime을 넣고 나머지를 null로 둔 경량 reference를 안전하게 만들 수 있다.
- Staff runtime은 `RestoreSnapshots`에서 새 `StaffDiscontentState`를 만들고 Aggregate root를 교체하므로 Unity scene/presentation 객체를 mutate하지 않는다. validator가 normalization을 차단하면 rollback-free publication 조건에 맞는다.
- Snapshot/restore call-site 검색 결과 production 생성은 record capture와 save section뿐이고, 외부 debug 호출은 empty restore 하나뿐이다. constructor/record/state restore를 strict argument 계약으로 바꿔도 정상 호출을 깨지 않는다.
- 프로젝트에는 이미 `CharacterId` 값 타입이 있으며 Staff ID는 `CharacterIdentity`의 persistent ID에서 나온다. 현재 저장 DTO는 문자열이므로 이번 section은 최소 canonical nonblank/ordinal uniqueness를 강제하고, 후속 전역 typed-ID 단계에서 DTO 필드 자체를 값 타입 직렬화 계약으로 교체해야 한다.
- `CharacterId.IsValid` 자체도 현재 nonblank만 검사하며 persistent ID normalization은 trim뿐이다. 따라서 section validator의 `new CharacterId(id).Value == id && IsValid` 검사는 현재 typed-ID 계약과 정확히 일치한다; 임의 접두사 강제는 기존 fixture/콘텐츠를 잘못 배제한다.
- `StaffDiscontentSnapshot`과 `StaffDiscontentRecord.FromSnapshot`이 ID trim, blank-name fallback, mood/day clamp를 중복 수행하고 `StaffDiscontentState.Restore`도 null enumerable/default/skip을 허용한다. section preflight뿐 아니라 runtime restore 경로도 valid-only 계약으로 축소해야 우회 호출에서 손실을 숨기지 않는다.
- `MarkIsolated`/`MarkSuppressed`는 departed, already-suppressed, non-rebellion 상태에서 실패한다. 따라서 isolated는 active rebellion에만 허용하고 suppressed는 LocalRebellion stage의 영구손실이지만 active rebellion/owner threat는 false여야 한다. 격리 후 진압은 가능하므로 suppressed+isolated 조합은 허용해야 한다.
- 기존 Staff fixture의 `ScenarioRuntime`은 실제 `StaffDiscontentRuntime` MonoBehaviour와 공용 Editor DI helper를 이미 사용하고 cleanup도 갖춘다. `RunAll`에 저장 경계 시나리오를 추가하고 동일 runtime으로 canonical round-trip과 invalid no-mutation을 증명할 수 있다.
- Staff fixture의 save scenario 삽입 위치는 기존 7개 gameplay scenario 다음이며, helper는 `VerifyOwnerThreatEscalation` 뒤/`CreateStaff` 앞에 둘 수 있다. 기존 cleanup은 이름이 `StaffDiscontentRuntime_Test`인 모든 runtime을 제거한다.
- 최근 strict fixture들은 section의 `Capture()`와 `Restore(json, version, report)`를 직접 사용해 canonical round-trip과 invalid preflight를 증명한다. Staff도 이 경량 패턴을 쓰고, interface 검사는 sealed 타입의 불가능 pattern 오류를 피하도록 `object sectionContract`를 거친다.
- Capture는 record의 outcome을 기본 인자 `None`으로 생성한다. 상태 Aggregate가 저장할 수 있는 값은 record의 11개 필드이며 outcome은 이벤트 결과이지 저장 상태가 아니다. V1 DTO를 유지한다면 validator가 `None`을 고정해 손실 없는 직렬화 계약을 명시한다.
- 첫 기록 patch는 문서 제목을 `# DungeonStory Findings`로 잘못 추정해 실패했다. 실제 제목 `# DungeonStory Current Findings`를 확인해 수정했다.

## 2026-08-02 detached restore findings

- Payload preflight alone was insufficient because the registry deserialized each payload again during live commit. The prepare-all phase now materializes typed payloads before any live mutation.
- Physical items already had the correct internal seam (`StageRestore` plus `Commit`) but the save section bypassed it through the broad runtime `Restore` method. Exposing that seam as a dedicated staging capability made the item repository the first genuinely detached Aggregate restore.
- A detached DTO is not enough if commit clears a live dictionary and repopulates it entry by entry. `WorldItemRepositoryState` now owns stacks, indexes, caches, equipment, and modules as one replaceable root, so the authoritative item state changes in a single assignment.
- Strict physical-equipment validation correctly rejects a stored/loose equipment instance without a stack. Tests and Editor tools must create and link the unique stack rather than weakening that invariant.
- A staged delegate around an old runtime `Restore` is useful migration infrastructure, but it is not proof of an atomic world swap. Final completion still requires replaceable Aggregate state roots and removal of the legacy adapter/rollback path.
- Mandatory staging can be enforced without guessing from source text: the Registry rejects non-staged sections during composition and the Editor validator reflects every public gameplay SaveSection. Current coverage is 54/54, with staged missing-data behavior for every optional section.
- Broad regressions are catching architecture migration omissions: the combat suite exposed an Editor fixture that still passed `null` for a dependency that production now correctly requires.
- Source-wide counts must distinguish production code from Editor fixtures and validator literals. The apparent default-service and late-bind hits were confined to fixtures or the validator's forbidden-token list; the production code count is zero for all five active authority guards.
- Scene transitions need persistent state, but not static state: the existing `DontDestroyOnLoad` mailbox is the correct scoped owner for pending requests, messages, and the temporary transition host.
- A SaveSection can stage its JSON and still expose partial state if its runtime calls `Clear()` across several collections. A replaceable root must include the related sequence/version/view fields as well as the primary dictionary.
- Production orders and their stock-sensor installation state are one Aggregate because one saved production payload owns both. Sharing one state store prevents a restored order list from being observed with the previous sensor set.
- `combat.equipment` previously rewrote physical equipment after `items.physical` restored it. Removing that pass is both an atomicity fix and an authority fix: combat restore now owns only references and work queues.
- Restore-time defaults must be built without publishing side effects. Faction defaults are now computed from the strategic map in a detached state and only synchronize faction home sites after the final state assignment.
- Character exposure and protective workwear cannot be independent restore commits because one save section captures both. A shared environment state store keeps equipment protection queries from observing a new exposure set with old workwear data.
- A version check must happen before any reset. External influence previously reset the current run before rejecting an unsupported payload; detached construction now leaves live influence state untouched on validation failure.

## 2026-08-02 authored gameplay catalog findings

- Meta upgrades, run variables, owner doctrines, and invasion patterns were not fixed protocols: their costs, text, weights, target preferences, and effect parameters are editable game content. Freezing their dictionaries would remove mutation but would still leave code as a second content authority.
- The existing `GameDomainContentCatalogSO` is a safer migration root than creating temporary runtime SOs or manually fabricating new asset GUIDs. Inline serialized records let the authored root become authoritative before legacy writers are removed.
- Effect interfaces remain useful runtime behavior boundaries, but their instances are now projections of serialized effect descriptors. The SO owns values; the plain runtime catalog owns validated immutable behavior objects; neither owns run state.
- `MetaProgressionState` and `RunVariableState` previously reached hidden static catalogs. Requiring catalog contracts at construction makes their rules deterministic per scope and removes cross-test/global reset coupling.
- The remaining taxonomy registries need separate treatment: enum/ID mappings may become immutable protocol tables, while display text and balance values such as stock delivery weights belong in authored SO content.

## 2026-08-02 runtime registry findings

- A provider that only returns one property from a scoped registry is not a policy boundary; it hides composition failures and encourages call-site defaults.
- Required scene runtimes are now resolved once from typed domain registries. Missing runtime state is a composition error, not an empty save, zero seed, unavailable UI, or permissive unlock result.
- Research and equipment unlock checks now fail closed against the same `BlueprintResearchRuntime`; provider absence can no longer bypass locks.
- The local LLM provider remains justified because it selects between two environment-specific queue implementations. It is the only remaining `I*RuntimeProvider` interface.

## 2026-08-01 V18 Phase 90 findings

- The character-summary defect was responsibility coupling, not only file length: combat commands, health/captivity operations, AI diagnostics, progression confirmation, stock projection, popup lifecycle, and detailed-stat rendering all lived in one MonoBehaviour.
- The replacement keeps Unity button entry points on `CharacterSummaryInfo` but delegates rules and projections to narrowly injected presenters. This preserves generated-view bindings while preventing the view from owning combat, surgery, captivity, or stock state.
- `CharacterSummaryInfo` is now 729 lines with eight injected dependencies. Shell/status/growth/AI/health/captivity/combat presenters range from 147 to 516 lines.
- Seed-addressed content rolls were using `System.Random` directly in character growth, start variables, shops, evolution, and procedural audio. These were deterministic calculations rather than saved run streams, so they now use the explicit `DeterministicRandomSequence` contract; saved stochastic gameplay continues to use `IRandomStreamProvider`.
- The V18 validator passes with 772 authored items and 168 catalyst SOs after adding the new presentation boundaries and direct-RNG prohibition.

## 2026-08-01 Branched production network V3 audit

- The live economy contains 174 authored production recipes. Sixty-two produced item IDs are reused as inputs; 20 of those currently have exactly one recipe consumer, so the defect is catalog-wide rather than limited to the generated research-overhaul content.
- All 24 generated `ResearchOverhaul` recipes currently consume the placeholder `stock-item:1`, and most generated products have no real downstream consumer. The V3 builder must replace these inputs and index consumers outside recipe assets as well.
- `ProductionOrderMode` currently has only `RepeatCount` and `MaintainStock`; `ProductionBillStatus` has no output-space or stock-sensor state, and production outputs spawn directly instead of reserving a dedicated local output buffer.
- Existing conveyor code already owns overflow policies and cyclic-deadlock detection. V3 should extend the production boundary with local buffer backpressure instead of creating a second conveyor authority.
- Fuel support currently stores one exact `fuelItemId`. Resource items already expose ingredient tags and nutrition, but need authored fuel value and facility supply eligibility for deterministic multi-item fuel/feed selection.
- The worktree contains extensive user/previous-agent changes from the completed 168-research, equipment, medical, defense, and survival work. V3 edits must preserve and build on those changes without cleanup or rollback.
- `ResourceUsageIndex` already reverse-indexes recipes, crops, craft materials, and built-in sinks. It currently treats `sink:equipment-material:*`, generic meals, trade, fuel, and other synthetic sinks as consumers, so V3 should evolve this authority into the production dependency catalog instead of adding a parallel graph service.
- `ResearchOverhaulContentAssetBuilder` owns all 40 generated facilities and 24 generated item/recipe pairs. It currently assigns every facility `FacilityRole.Research`, generic `research-overhaul`/`rf##` tags, and every recipe a single `stock-item:1` input, making it the primary content rewrite boundary.
- Combat equipment definitions expose material families and stock-category amounts rather than concrete resource item inputs. The dependency catalog needs a deterministic material-family-to-resource mapping or explicit authored dependency inputs to count real equipment consumers.
- The first broad multi-search returned exit code 1 because one `rg` branch had no match, although other branches returned useful output. Follow-up inspection uses independent targeted reads so a no-match cannot mask successful results.


## 2026-08-01 168-node research and equipment overhaul

- The implementation starts from a heavily dirty 442-file worktree whose active unrelated work is character anatomy/medical expansion. Research/equipment changes must remain narrowly scoped and preserve every pre-existing edit.
- The live catalog currently contains 141 research assets. The approved target is 168: the three previously planned nodes plus research IDs 7224–7247.
- Current research prerequisites are bare `ResearchProjectSO` references, research save data is V3, and the tree has no shared reverse reward catalog or timing projection.
- Current combat equipment has 19 authored definitions, no research/tier/slot/lineage fields, and forge recipes expose every definition. Runtime crafting therefore has no authoritative research lock yet.
- The overhaul is intentionally new-run only for research/equipment V1–V3 data; no silent migration or default substitution is permitted.
- The final approved breadth queue measures 32.2 medieval days, 80.4 early-industrial days, 234.3 mature-industrial days, and 372.0 rune/abyssal days at 99 effective work per day.
- Reusing the serialized project unlock collection caused stale and multiply-owned facility rewards. The builder now reconstructs unlock collections and applies one canonical research owner per building ID.
- Existing equipment tests must explicitly inject completed research. Product runtime remains fail-closed when no research provider is available, preventing direct-call and restore bypasses.
- The save-slot catalog now checks research/equipment section versions before enabling Load, rather than waiting for restore to throw after scene launch.
- The research pointer verifier previously hard-coded a small catalog and searched only the current viewport for queue candidates; it now expects 168 and centers an available node before pointer interaction.


## 2026-07-26 V16 traversal-cache and wildlife timing findings

- `Grid.version` was serving two incompatible purposes: any content mutation invalidated structural path/room/facility caches. Moving wildlife and changing items therefore discarded otherwise valid routes.
- `Grid.StructuralVersion`/`TraversalVersion` now changes only for area, building, hallway, movement-blocking, or connection mutations. Full `version` still tracks every content change for consumers that need it.
- Wildlife hunt reachability previously depended on cached visitable-occupant positions. With dynamic occupancy excluded from traversal invalidation, the hunt query now checks the target actor's current Grid coordinate against the reachable-cell result.
- Wildlife arrival dwell mixed the caller-provided current time with a null-clock fallback of zero. Giving every actor a Unity game-clock fallback makes route start, route completion, threat interruption, and dwell expiry use one scaled time base.
- Focused Grid, Wildlife, and AI naturalness regressions pass after the changes.
- The 100-NPC EditMode stress scenario improved from roughly 353 seconds to 50.6 seconds. Broker path searches fell from 1,440 to 51 and budget deferrals from 16,461 to 50. Scheduler p95 is 0.73ms; the large max values are cold-path/test instrumentation spikes and require PlayMode profiling before final acceptance.

## 2026-07-21 Physical item and hauling implementation findings

- Current Editor Console baseline is clean through Unity MCP (`Error 0 / Warning 0`), but batchmode compilation cannot run while the interactive Unity Editor owns the project. MCP/Editor Console is the active compile source for this pass.
- `WarehouseInventory` currently lives inside `Buildings/SO/StockInfo.cs` and is consumed directly by stock delivery, shop restock, crafting, and expedition preparation. Physical items must be introduced as a runtime layer without mutating shared `BuildingSO` data.
- World info click selection currently checks characters before buildings and does not know item piles. Adding `GridLayer.Item` must not break `GridCell.GetBuilding()` callers that expect buildings even when another non-blocking occupant is on the same cell.
- Existing `AIActionSet.RequiresDestination=false` actions such as wait/look-around provide the right pattern for hauling: `AbilityHaul` should own pickup/dropoff pathing instead of forcing the generic AI destination contract to represent two legs.

- Final character-growth acceptance is green: combined EditMode regression, real-pointer P1/P2 (`18/18`), exclusive character/building selection, actual skill alert navigation, start-party generation, V3 save restore, and all three ultimate domains passed with Unity Console `Error 0 / Warning 0`.
- MCP `Camera_Capture` cannot render the live URP `Main Camera` directly in this editor/package combination, but a plain runtime camera copied from it renders the same transform and projection successfully. That capture is nonblank and provides independent world-only evidence alongside HUD `ScreenCapture` artifacts.
- Skill runtime audit found that `research`, `output`, `repair`, `stock`, `relationship`, and `revenue` modules only granted a generic mood factor. Their authored numeric variants now feed their actual subsystem paths, while management ultimates only join those contextual modifiers after their operating-day use limit has been marked.
- Defense automatic ultimates previously listened to the pre-target `InvasionStartedEvent`, so enemy-targeted effects had no intruder to affect. They now listen to `InvasionSpawnedEvent`, canonicalize actors by GameObject, and apply validated damage modules to the spawned intruder.
- Two inspection commands failed before code changes: one referenced the nonexistent `Assets/Scripts/FacilityShop/Shop.cs` instead of `Assets/Scripts/Buildings/Shop.cs`, and one PowerShell interpolation used `$i:` without braces. The follow-up reads used the resolved path and format operator.
- The actual skill-alert capture shows the prepared owner name as `유나 사장으로 시작`, confirming the duplicate-role notice fix. It also shows the event detail and `성장 탭 열기` command clearly above the world, while the character panel remains readable and no surfaces overlap incoherently.

## 2026-07-20 Confirmed character growth design

- The runtime character prefab contained both the empty legacy `Customer : CharacterActor` component and the canonical `CharacterActor`, so scene queries counted every spawned character twice. Start-party cleanup then destroyed the shared GameObject while trying to remove the apparent duplicate. Both character prefabs now keep only the canonical component, and start-party/save queries normalize actors by GameObject as a compatibility guard.
- The start-party pointer flow passed end to end with real LLM output, but the supposed mobile capture remained 1920x1080 because `Screen.SetResolution` does not resize this Editor Game View. Mobile bounds/capture evidence must use an Editor Game View size change or an equivalent actual render target before it can count as verified.
- Selecting fixed Game View sizes through `GameViewResolutionController` produces real 1600x900 and 900x1600 render targets. The portrait layout keeps all three member cards and the final action row in-bounds; no card or text overlaps were visible in the corrected capture.
- Growth-tab capture exposed a separate visible copy defect: authored owner names such as `슬라임 사장` were rendered as `슬라임 사장 사장으로 시작`. Owner selection now accepts the prepared identity name and avoids appending a duplicate role suffix.

- The legacy `CharacterProgression` model (`MaxLevel=20`, three equipped IDs, fixed unlock track, global `PowerMultiplier`) must be replaced rather than extended in parallel.
- Shared authored configuration belongs in one `CharacterSkillSystemSettingsSO` with managed-reference module rules. Character-specific skills, drafts, growth, ledgers, request state, and use limits belong to serializable runtime/save records.
- Normal active unlocks are at levels 1, 5, and 30; passives are automatic at level 1 and after level 25 plus narrative breadth; the narrative-derived ultimate arrives at level 50.
- Potential uses five display grades with 45/30/15/8/2 population weights and only modifies normal-active rarity rolls. A missed Rare-or-higher draft grants the next unlock a 1.5x upper-rarity weight modifier.
- Character preparation is a three-person roster (owner plus two same-species staff) with identity, aptitude, and skill reroll groups. World visitors require persistent individual profiles rather than respawning shared `CharacterSO` definitions.
- Save compatibility is deliberately broken for legacy progression data; save/load must preserve already-rolled rarity and candidates so reloading cannot reroll outcomes.
- `CharacterActor` already requires `CharacterProgression`, so the new per-character growth state can replace that component's legacy lists without adding another required prefab component.
- Combat applies the legacy level multiplier both in `CharacterActor.GetCombatPowerMultiplier` and `OffenseBattleFactory.CreatePlayerCombatant`; both applications must be removed so level growth comes only from allocated stat points.
- `CharacterStats.GetCharacterStat` is the narrow final-stat query used by battle and expedition power. It can compose identity profile stats with character-specific base-stat replacement, level growth, and conditional passive bonuses.
- Existing combat abilities are constructor-driven modules, so generated combat selections can be validated as string IDs and converted into `CharacterCombatAbilityDefinition` instances without storing polymorphic runtime effects in save data.
- The local LLM queue already supports prioritized request profiles and JSON mode. A dedicated skill profile can use the same queue while persistent retry keys and backoff live in the generation service.
- `OwnerSelectionPanel` is runtime-configured and pauses the simulation, making it the correct replacement surface for three-character preparation rather than introducing a disconnected scene-only mockup.
- Start preparation can remain instance-safe by generating skills on hidden `CharacterProgression` preview objects, then restoring their snapshots onto the real owner/staff actors only when the player confirms the party.
- Skill rerolls need request cancellation plus a per-growth revision because LLM callbacks can finish after identity or aptitude has changed. The generation service now ignores canceled requests and request keys include the revision.
- Existing customer `CharacterSO` assets can provide same-species staff visuals and authored species data without mutation: the spawned actor is converted to runtime `CharacterType.NPC`, receives `AbilityWork`, and owns its prepared growth snapshot.
- The first preparation PlayMode pass exposed bottom buttons parented directly under the modal surface; rebuilding member cards therefore left duplicate Start/Back controls. A dedicated preparation-action root fixes their lifecycle.
- Replacing base stats alone was insufficient: modifier queries still read `CharacterIdentity.Profile`, which is built from authored SO traits. The effective runtime profile must be rebuilt from the character's selected trait IDs and used by all modifier queries.
- Recruitment previously changed the live actor to NPC but never marked its persistent world profile as staff. Without promotion, a saved hired guest remained eligible for later visitor acquisition.

- Character progression must not be mutable state on `CharacterSO`; those assets are shared by every character using the same definition. Per-character level, XP, learned skills, and equipped slots need runtime/save ownership.

- The offense loop now has the missing expedition layer: preparation in the dungeon, route pressure, attrition, tactical formation combat, retreat, return, and reinvestment are separate decisions rather than one launch button followed by one boss battle.
- Ordinary battle victory no longer finalizes a target or heals the party. It returns to route choice with damage and stress intact; only the boss resolves the expedition and advances the campaign.
- The dungeon link is capability-based. Formal usable rooms and modular expedition-support abilities contribute preparation values, while supplies are withdrawn from and returned to the real warehouse inventory.
- A complete UI-event campaign passed all targets in order: `food_farm`, `merchant_road`, `old_armory`, `mana_ruins`, `rival_dungeon`, `truth_core`; final state was `truth=True` with six result records.
- Product-shell coverage separately proves pointer-driven owner selection, customer recruitment, map/composition, journey entry, first battle, and exact active-battle save/load without captured errors or warnings.
- Global button-text lookup is unsafe once event alerts repeat prior action labels. Offense verification now scopes clicks to the active map, expedition, or battle panel, matching the visible interaction surface.
- Immediate `CaptureScreenshotAsTexture` after a synchronous full campaign can capture before the next rendered frame. Scheduling `ScreenCapture.CaptureScreenshot` for the following frame produced valid visual evidence.

- The old offense lifecycle was the main reason it felt flat: launch opened one full encounter, every battle completion deleted the expedition, and victory fully healed survivors.
- Formation previously had no tactical effect. Source and target position constraints plus forward compaction are required before party order becomes a real decision.
- Dungeon/offense coupling now has an existing-compatible path: explicit `BuildingExpeditionSupportAbility` values override or extend role-based fallback contributions, so old content works before every asset is migrated.
- Warehouse inventory can safely back expedition supplies because aggregate availability, deterministic withdrawal, rollback, and return can be implemented without introducing a second resource ledger.

- The requested target is now a dungeon-linked multi-node expedition, not continued balancing of the existing one-target/one-battle flow.
- The existing offense lacks route decisions, supplies, formation constraints, persistent expedition attrition, camping, and room/facility recovery. These are product gaps, not presentation polish.
- The recently added campaign-order `+50%` stat multiplier closes a numerical test but works against the requested design; it must be removed as the real growth and preparation loop is introduced.
- The worktree is heavily dirty across scenes, prefabs, data assets, and gameplay scripts. Offense changes must remain tightly scoped and must not revert unrelated user changes.
- Stock is not a standalone subsystem folder. Runtime inventory is `WarehouseInventory` on warehouse buildables, with `SceneFacilityEvolutionWarehouseInventoryQuery` already providing aggregate query/withdraw/rollback patterns suitable for expedition supplies.
- Building functionality is already modular through `BuildingAbilityCollection`; expedition preparation/recovery should be added as ability modules and queried through capability interfaces instead of adding more fixed fields to `BuildingSO`.
- `OffenseExpeditionRuntime` currently owns the right lifecycle boundary but removes the active run on every `BattleCompleted`, fully heals victory survivors, grants target rewards immediately, and advances the world map. This method must become node-aware: ordinary victories return to the route, only boss victory finalizes the target, and survivors retain attrition.
- `OffenseSaveService` already pairs one active expedition with an exact battle snapshot. Its run payload can be extended in place with route, formation, stress, supplies, loot, and current-node fields while old saves default to a legacy boss-battle run.
- Offense panels are created through injected factories and `IOffensePanelService`; adding a dedicated route panel preserves the existing ownership pattern and avoids scene-authored UI dependencies.
- `OffenseExpeditionRun` can absorb the new journey state without changing its identity, target, or actor ownership. This lets battle, reward, campaign, and save systems migrate incrementally instead of creating a parallel offense runtime.

- Six campaign targets and truth-reveal victory already exist.
- `OffenseBattleSession`, inline combat abilities, six fixed encounters, direct command runtime, and a dedicated battle panel now exist.
- Product expedition start now creates a turn battle. Product UI, debug completion, reward probes, and PlayMode verification no longer resolve expeditions by timer or combat-power comparison.
- Staff identities now persist from run seed plus creation sequence, with owner fixed to `owner`.
- Save V2 captures the active battle and V1 active expeditions migrate to a first-turn battle.
- Title new-game now selects `DungeonDifficulty`; start/result persistence is explicit and still needs an end-to-end runtime audit.
- Runtime defense uses recurring `EndlessDefense` cycles, but two PlayMode verifiers still expected obsolete `FinalChallenge/TruthHunt`; those expectations were replaced.
- Scene runtimes are found through cached providers, so the turn engine can be a DI singleton without adding another scene-authored component.
- The standalone `OffenseBattlePanelFactory.cs` was visible to `AssetDatabase` but absent from Unity Bee's source list. Merging its factory/controller types into the already imported `OffenseBattlePanel.cs` resolved the actual Unity compile failure.
- Unity MCP is connected. The Editor is idle and the current Console count is `Error 0 / Warning 0`.
- `DungeonProductShellPlayModeVerifier` now drives difficulty selection, expedition target/start, guard, attack/target selection, dungeon switching, manual save/load, and exact battle-state comparison through pointer input.
- First current-build product-shell run reached PlayMode with no Console errors/warnings, but every synthetic Input System button click failed to invoke its callback. The report proves targets were active/interactable while Settings, Difficulty, and Owner state did not change. The later gameplay transition came from the verifier's direct duplicate-transition assertion, so this run is not product-path evidence.
- Captures confirm the failure boundary: the title capture is valid, while the alleged battle capture still shows the owner-selection modal.
- The queue-plus-`InputState.Change` fallback fixed current Editor pointer delivery. The next run passed title Settings, audio/accessibility tabs, Hard difficulty, owner selection, save/settings/title return, Continue, and load-failure handoff with `capturedErrors=0; capturedWarnings=0`.
- The remaining product-shell failure moved to offense navigation: `P1Action_OffenseTarget_0` had a screen center at y=0.71 and was active/interactable but clipped/covered by the bottom HUD. The verifier skipped the visible `월드맵 열기` and party-composition workflow, so its click did not select a target and no Start button appeared.
- The visible map flow now selects `food_farm` and opens composition correctly. The current capture shows `선택 인원 0/3` and `필요 인력 부족 0/1`: a clean new run has no eligible employee, so the verifier cannot start combat until it exercises or prepares the recruitment path.
- Recruitment currently marks only `RegularCustomerRecord.IsRecruited`. No listener or conversion changes the live actor from `CharacterType.Customer` to `CharacterType.NPC` or grants `AbilityWork`; therefore a normally recruited customer still fails offense eligibility (`NPC` + active `AbilityWork`). This is a real gameplay-loop gap, not just a verifier fixture issue.
- Recruitment also had no runtime component in `SampleScene`, and the scene spawner referenced only `TestCharacter` (`NPC`). The real customer asset (`Resources/SO/Character/New Character SO.asset`) was never included in the spawn list, so visits and recruitment could not occur naturally. The lifetime scope now owns the runtime, and the spawner merges customer catalog entries once after DI.
- Customer data IDs identify a customer definition, not a unique live visitor. Keeping only the data ID caused recruitment to convert an arbitrary matching actor. `RegularCustomerRecord.ActiveActor` now preserves the exact last visitor in memory, while restored records safely retain the scene-query fallback.
- Current product-shell report is `PRODUCT_SHELL PASS`. It proves recruitment, offense input, independent dungeon time, view switching, direct guard/attack targeting, and exact active-battle restore with `capturedErrors=0; capturedWarnings=0`.
- The room MCP capture originally lost its overlay between tool calls because normal hover polling cleared it on the next frame. Preparing the room and pausing PlayMode in the same editor command preserves the real renderer state; the capture then shows 4 active fill cells and 10 active outline segments on the intended sorting layers.
- `DungeonRunFlowPlayModeVerifier` now opens `SampleScene` before PlayMode, so it remains valid after introducing the separate title scene. It proves recurring day-10/day-20 bosses never grant Victory and only stage 6 `truth_core` does.
- The final independent domain pass reports `RoomSystem`, `RoomEnvironment`, `OffenseBattle`, `OffenseWorldMap`, and `OffenseReward` all successful with no console errors or warnings.
- The complete regression set is green: product shell, run flow, save UI, Unified UI, P1/P2 surfaces, character click priority, room inspection, and 29 implemented debug suites.
- A clean player-style run proved natural visits stopped at 15 because a visitor with no remaining visits was still forced through an optional look-around before exit. `AbilityShopping` now exits immediately when the visit cycle is complete, and its focused AI regression passes.
- The recruited prefab carries both legacy `Customer` and canonical `CharacterActor` components. Reference-only distinctness therefore rendered one employee twice and made the second lifecycle transition fail. Expedition discovery and launch now canonicalize actors by GameObject; the clean composition shows one row for one employee.
- World map and expedition composition canvases could stay open together. Because composition sorts above the map, a visible stage button could actually hit composition's Close button. `OffensePanelService` now hides the opposite panel before binding either surface.
- Stage 1 left the only employee at `13/120`, while no building ability or player action restores combat health. The campaign exposed a hard lock rather than attrition. Successful surviving members now receive full return treatment; retreat/failure health and permanent death remain consequential.
- The resource catalog contained only one customer definition even though campaign stages require two and then three non-owner members. Added natural Orc and Vampire customer definitions, bringing the recruitable pool to IDs `1,2,3` without bypassing visits or recruitment UI.
- Focused customer, staff, world-map, battle, and reward suites pass after these direct-play fixes.
- `SampleScene` had a serialized `RegularCustomerRuntime` while the lifetime scope also created one. Both listened to every visit, but UI recruitment and generic runtime lookup could observe different states. DI now reuses the scene runtime and only creates a fallback when a scene has none; a clean run reports exactly one instance.
- A second clean seed ran at X5 for almost two real minutes and settled at average satisfaction `62.0 / 65.1 / 71.7`, below the old recruit threshold `75`. Since the unmodified starter dungeon offers no clear way to correct that early random average, recruitment and therefore the campaign were seed-locked. The default/product candidate threshold is now `65`: three visits establish a regular and the fourth qualifying visit unlocks recruitment.
- The direct run won stages 1 and 2, then lost both selected employees at stage 3. Training facilities only add mood factors; they never improve combat stats, while the fixed encounter roster escalates far beyond the unchanged recruit stats.
- Required power was informational and the original encounter curve had no corresponding growth path. Campaign stages now grant a deterministic preparation multiplier derived from prior victories (`+50%` combat stats per completed stage, health unchanged). The map and composition UI expose the bonus and effective party power.
- A deterministic battle regression now runs the starter Orc/Vampire/Slime roster through all six encounters on Easy, Normal, and Hard and requires victory with every member alive. This is regression coverage only; final acceptance still requires the clean pointer-driven playthrough.

## 2026-07-20 Weak-link audit

- P0: World profiles use unique string IDs, but regular-customer, rumor, staff-discontent, and some evolution records still key characters by shared `CharacterSO.id`; distinct people using the same template can share visits, recruitment, trust, and complaints.
- P0: The six-target winning campaign previously ended with the party at levels 6/5/3, while the new curve requires 63,700 XP for level 50. Level-25/30/50 content is outside the current run loop.
- P0: Skill generation has no executable fallback. Start-party confirmation and the ready guest pool both wait on validated LLM results, so an unavailable local LLM can stop both new-game start and the customer economy.
- P0: Passive validation permits combat modules for DamageTaken/InvasionStarted/BattleCompleted, but the outside-combat executor implements only a subset. Valid generated passives can therefore have no mechanical effect.
- P1: Room quality has two definitions. UI/mood use `RoomEnvironmentSnapshot`, while AI utility uses the older area/door/furniture `RoomInstance.GetQualityScore`; a room can look excellent in the overlay without becoming equally desirable to AI.
- P1: Offense preparation checks usable rooms, then adds fixed bonuses per facility role/ability. It does not consume room environment scores, staffing quality, recent operation, or character mastery, so duplicate fixtures can replace good operation until caps are reached.
- P1: Generated actives are converted with source and target formation masks set to `Any`; authored species skills respect positions, but personalized skills bypass the Darkest-Dungeon-style formation layer.
- P1: World profiles persist growth and narrative, but actor binding/release does not persist social memory, mood history, or the profile's relationship score.
- P2: Unlimited full reroll also rerolls potential and restores all partial reroll charges, making rare potential primarily a patience check instead of a durable roster tradeoff.
- P2: Growth UI shows only total allocated points and skill descriptions. Per-stat growth causes and stored narrative reasons disappear after the acquisition alert, weakening player understanding of history-shaped growth.

## 2026-07-20 Closed-loop integration findings

- Building info craft buttons were visible but ignored when clicked in the first fade-in frame because the parent `CanvasGroup.interactable` stayed false until the 0.1s tween completed. The panel now enables interaction immediately, leaving the tween as visual-only feedback.
- JsonUtility can deserialize a null nested class field as a blank object; `DungeonOffenseSaveData.activeBattle` therefore came back non-null with empty IDs. Offense restore now treats blank battle IDs as no saved battle, and capture only persists active battles that match an active `InBattle` expedition.
- Equipment crafting PlayMode now proves the full non-scenario UI path: queried warehouse material is visible to the runtime, the building craft button creates a queue order, materials are withdrawn, `Craft` work completes the order, and the expedition loadout reserves the crafted item.
- The character growth tab previously displayed only total growth, so players could not tell whether a stat came from base rolls, species/traits, level growth, equipment, or a conditional passive. `CharacterProgression` now exposes the breakdown directly and the UI consumes that source.
- Runtime-generated UI can survive script changes inside the dirty scene. The first stat-breakdown test passed by text value while the screenshot still showed the old 30px summary region; `CharacterSummaryRuntimeLogFactory` now rebuilds the generated view when the expected `GrowthList` structure is missing.
- The first no-injection direct-play verifier did not fail because offense launch was too strict; it failed because the real AI never consumed recovery facilities after a stage-1 victory. Prior coverage only checked `AIRest.CanStart` and `FacilityCandidateScorer.GetNeedScore`, so `AIAction.CalculateScore` could still return 0 and prevent selection.
- The concrete recovery scoring gap was that `Rest.asset` still referenced the old `Sleep` stat consideration instead of `NeedRest`, and `ConsiderationFacilityNeed` rejected staff self-care when `AbilityShopping.visitCount` was 0. On-duty Hygiene also rejected workers despite stress recovery being part of the hygiene need score.
- Unity can keep running the previous assembly when a compile error exists elsewhere. The missing `restResolved` diagnostics were not an MCP truncation problem; `NaturalRunRuntimeDebugProbe` still called the removed `IGameDataProvider.GetGameData()` API, so StaffDuty was executing stale code.
- `CharacterAiActionCandidate.Action.destination` is intentionally null during scoring. Destination proof belongs at `AIActionSet.TryResolveDestinationWithFailure` or after `AIBrain.TryCommitActionCandidate`; tests that read candidate destination before commit can produce false failures even though the AI can select and use the facility.
- The next no-injection direct-play failure was within the expedition itself: a two-person stage-1 party could reach the boss after an elite node but lacked enough remaining health to finish it. The verifier now behaves like an actually cautious player by filling the three-person party, carrying available supplies, using medicine before routing, and visiting camps when attrition is visible.
- Debug and focused scenarios can leave event-listener MonoBehaviours alive without DI-owned UI/recorder dependencies. Throwing from those listeners pollutes the Console even when the domain scenario itself passes. Event-alert and facility-evolution listeners now keep runtime state but skip only their dependent side effects until injection exists.
- The direct run proved the old default recruitment cadence was too slow for the current 3-person stage-1 offense requirement: by Day 10 a customer had 3 visits and high satisfaction, but no 4th visit arrived during the recruitment wait. Default recruitment now promotes a satisfied 3-visit regular to a recruit candidate; custom rule tests still cover the stricter 4-visit path.
- Recruitment is now explicitly template-safe rather than template-consuming. `CharacterSO` remains a shared species/source template, while `RegularCustomerState` and the promoted world profile own the persistent recruited person. Tests should assert the persistent ID is recruited, not that the shared template can never spawn another person.
- Unity scene serialization can override constructor/default rule changes. `SampleScene` carried the old `RegularCustomerRuntime.rules.recruitCandidateVisitThreshold=4`, so the direct run still behaved like the old cadence until the scene value was changed to 3.

## 2026-07-21 Feature verification findings

- Unity can keep QA request files alive while compilation is broken, so a shell-side "waiting for report" loop is not evidence that a verifier is actually running. Compile errors must be cleared before accepting any PlayMode result.
- ProductShell and CharacterClick both exposed stale Input System mouse devices after scene/UI transitions. The reliable verifier pattern is to use a dedicated verification mouse, make it current, apply `InputState.Change`, queue the state event, and recreate the device if the position still does not move.
- RoomInspection was still closing only the legacy owner-selection flow. The current start-party preparation panel must be completed as part of any gameplay-scene verifier before testing top-right HUD toggles.
- Expedition equipment UI searches must be scoped to the active expedition panel. A global "contains Iron Edge" button search can hit the building crafting button instead of the expedition equip button after crafting leaves the building info surface open.
- Feature batch verification now runs without MCP by writing request markers and letting the Editor open `SampleScene`, enter PlayMode, attach each runner, and aggregate the report. MCP Camera_Capture evidence is still separate and cannot be substituted by this batch.
## 2026-07-20 Character progression audit

- `CharacterSO` and `CharacterRuntimeProfile` currently describe shared authored identity, species, traits, base stats, and derived multipliers. They do not own mutable per-character growth.
- `CharacterActor` requires split runtime components (`CharacterIdentity`, `CharacterStats`, lifecycle, log, etc.) but has no progression component yet.
- Per-character level, XP, learned skill IDs, and equipped skill IDs should therefore live in a new actor-owned runtime component and be serialized by the character world save service.
- Existing offense ability definitions should be reused as the shared skill catalog so progression does not create a second combat-skill system.
- `CharacterCombatAbilityCatalog.GetAbilities` currently grants every species/trait ability directly to the battle combatant. This is the single integration point to filter by a per-character equipped loadout.
- Deterministic XP award points are `BuildingTrainingAbility.ApplyUseCompleted`, successful completion in `WorkTaskExecutor`, and `OffenseExpeditionSystem.OnBattleCompleted`; each fires once per completed activity/result.
- The generated character summary already owns Status, Mood, and Records tabs. A fourth Growth tab can expose level/XP and learned/equipped skills without adding another popup.
- Existing species assets do not currently serialize authored combat ability collections, so the catalog fallback abilities are the effective species skills. Shared unlockable techniques are needed for meaningful level milestones beyond the single innate skill.
- `OffenseExpeditionService.CalculateMemberPower` originally read the base stats multiplier directly; it now goes through `CharacterActor.GetCombatPowerMultiplier` so composition power and actual battle scaling agree on level growth.
- The full-game save debug scenario had retained a legacy single-battle expectation. It now verifies the current route-choice state, and orphan battle snapshots no longer discard an otherwise valid saved journey; both focused progression and full-game round trips pass.

## 2026-07-21 Physical item and hauling findings

- `RegisterComponentOnNewGameObject<ItemPileInfoPanel>` only created the panel when something resolved it, so item pile click events had no listener until the lifetime-scope build callback explicitly resolved the singleton.
- The item marker fallback sprite used a tiny white texture with too high a pixels-per-unit value, making default stock markers effectively invisible in SceneView capture. Lowering fallback PPU gives a visible one-cell marker.
- Delivery/reward physicalization can be introduced safely before removing legacy warehouse deposits by making `WorldItemStackRuntime.TrySpawnStockDelivery` the first path and preserving direct deposit only when the physical runtime is unavailable.
- Shop restock already used a character movement route, while the older instant `RestockFrom` API remains for legacy/debug callers. Gameplay work execution should continue using the physical route.
- Purchase and shoplifting previously only changed money/events and shop stock. Adding carried items at purchase/theft time creates the downstream hook for exit, confiscation, recovery, and theft consequences.
- Restored hauling multipliers must not permanently override runtime option changes; the settings provider now reads the current user setting first.
- The first actual `AIHaul` PlayMode pass found that `AbilityHaul.StartHauling()` reserved a stack, then immediately called `StopHauling("restart")`, releasing and clearing that same job. The worker walked toward the default `(0,0)` route instead of the selected item; reserving after stopping fixes the runtime, not just the verifier.
- A second logistics pass showed loose items could be carried toward a far existing warehouse because warehouse selection used scene-query order. `WorldItemStackRuntime.TryFindWarehouseForStack` now chooses the nearest reachable delivery cell, which also prevents long, confusing haul routes in normal play.
- The physical logistics verifier now proves the full no-injection movement path: loose stack to warehouse, warehouse stock to facility buffer, craft input buffer consumption, crafted equipment output stack to equipment inventory, expedition packed stacks, and carried-weight UI all pass with Console `Error 0 / Warning 0`.
- The item-pile PlayMode verifier still carried a legacy owner-option click path, so the current start-party preparation UI could leave owner selection active and make pile UX verification fail before item interaction. Reusing `StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug()` fixes the verifier entry without changing gameplay item logic.
- File-request PlayMode runners are useful when MCP loses approval during PlayMode/domain reloads. The pile verifier now mirrors the logistics verifier's request-file pattern, which lets the Editor run the test from EditMode even when direct MCP command execution is flaky.
- The remaining warehouse link was conceptual: stock could be deposited into the aggregate `WarehouseInventory` while the player could not inspect a corresponding stored physical stack. Stored warehouse stacks now mirror aggregate stock and are hidden unless the `물품` view toggle is on, keeping ordinary play uncluttered while preserving physical inspection.
- Stored physical stacks must be the restore authority when present. V5 restore now makes warehouse aggregate stock follow stored stack totals, so save/load cannot silently resurrect an old direct-inventory value after items have entered the physical hauling system.
- `CharacterWorldSaveService` already captured and restored `CharacterCarryInventory`; the contract test now verifies that carried items also survive the full `DungeonGameSaveData` JSON boundary.
- Direct-play recruitment exposed a profile/actor authority gap: recruited visitors kept a customer `CharacterSO` template while their world profile had become staff. If later code reset only `CharacterIdentity.CharacterType`, the actor could be released like a visitor and disappear from expedition candidates. `CharacterPopulationService` now treats `WorldCharacterProfile.isStaff` as authoritative and `CharacterSpawner.Interact` refuses to return staff profiles to the visitor pool.
- Offense reward regressions had a stale stock expectation after physicalization. Rewards may now appear as loose dropoff stacks instead of immediate warehouse stock, so tests measure warehouse delta plus physical-stack delta. The recruit-candidate reward handler also intentionally grants at least two candidates.
- `CharacterCarryInventory` could be duplicated in editor/runtime fixtures, splitting carried items between two components and making theft/hauling assertions read the empty one. The carry inventory is now `DisallowMultipleComponent`, and item tests resolve it through `CharacterCarryInventory.Ensure`.

## 2026-07-22 Direct-play completion findings

- The final `truth_core` failure was not a hidden compile or MCP issue; it was a bad natural-play decision path. The verifier accepted "three legally deployable members" even when the final party was not the trained Lv50 core.
- Camp routing had an off-by-one supply assumption. Entering any node consumes one ration, so choosing a camp with exactly two rations left means there is no longer enough food to actually rest at the camp.
- Generated/offense skill verification must treat control and setup modules as real combat actions. Looking only for `OffenseDamageEffect` made valid vulnerability, delay, multi-target, and conditional-amplify skills invisible to the direct-play battle driver.
- Late-stage direct-play proof needs a stronger gate than `CanJoinExpedition`. The product rule correctly blocks only below 25% health or 80 stress, but a player-style final boss run should wait for a healthy trained party and spend camp/medicine before engaging.
- The previous 900x1600 HUD width gap is fixed. Upper-right controls are clamped to the canvas width, top/bottom tab buttons no longer retain template widths, and `Temp/resolution-matrix-report.txt` now verifies 900x1600 gameplay bounds with `RESULT=PASS; failures=0`.

## 2026-07-22 Work amount and construction findings

- GameplayScene PlayMode verifiers must first complete the current start-party fallback before testing world input. Otherwise the owner-selection overlay can remain visible while tests interact with UI behind it, producing false world-click failures.
- `Grid.CountBuilding(BuildingSO)` counts any occupant with the same `GridId`, including a construction site using the target building ID. Tests that need to prove "not instantly built" should inspect the target building layer at the footprint, not the aggregate count.
- Work target scoring needs an explicit facility-role fit term. Without it, a generally supported work type such as Guard can compete on unrelated facilities and flatten species/work specialization. Guard now favors Training/Security facilities, while Research favors Research facilities.
## 2026-07-23 Nameplate, wildlife motion, and zoom findings

- `WorldCharacterNameplate` inherited the actor sprite sorting layer and added only `+36` order. Dungeon walls, floors, and furniture can use later sorting layers or much higher orders, so the text can be visibly covered even while the character sprite itself remains readable.
- `CameraManager` already reads unscaled wheel and keyboard zoom input, but `blockWheelZoomOverUi` calls the broad `IUiPointerBlocker.IsPointerOverUi()`. Full-screen HUD graphics count as UI hits, which suppresses wheel zoom over most or all of the visible world.
- Wildlife ecology produces meaningful intents, but `ChooseReachablePosition` samples only `origin.x +/- distance`, picks an almost deterministic best score, and immediately becomes eligible for another decision after a route finishes. On a side-view exterior surface this presents as repeated left/right pacing.
- Natural wildlife motion needs to preserve the one-dimensional walkable surface while varying cadence and intent: weighted target choice, direction momentum, arrival dwell, sprite facing, and eased/bobbed locomotion are the appropriate fixes; allowing arbitrary vertical cells would reintroduce air/wall traversal bugs.
- The gameplay camera uses the URP `UnityEngine.Rendering.Universal.PixelPerfectCamera`, not the legacy `UnityEngine.U2D` component. Zoom must enter the URP component's Cinemachine-compatible mode before assigning a continuous orthographic size, or `OnPreCull` restores the baseline every frame.
- Runtime sampling after the wildlife changes showed intent-driven cadence rather than synchronized pacing: most animals remained at forage/drink/rest targets while only one moved at a time, and individual positions changed by only zero to two cells over the next sample window.

## 2026-07-23 Customer checkout patience audit

- Staffed shops already keep customers in `WaitForServingWorker` and raise `Operate` urgency, but the wait has no timeout or personality stage. A shop without a worker therefore holds every customer forever.
- `CharacterAiPersonality.patience` and trait/species `waitPatienceMultiplier` exist, but no runtime checkout path consumes the latter.
- `AbilityShopping` always counts a finished interaction as a completed visit. An abandoned checkout needs a separate outcome so the customer avoids that shop without consuming the remaining visit, allowing Utility AI to choose an alternative.
- The action-phase/nameplate path already exposes current AI phases. Checkout feedback can remain lightweight by updating that phase and using one-shot event alerts instead of adding a new always-visible panel.
- A queue reaction must not consume the customer's remaining visit. Marking only the abandoned facility as visited lets the existing shopping Utility AI select another reachable shop and naturally fall back to looking around or exiting when none remain.
- Runtime verification showed the staged path is deterministic at accelerated game speed: an impatient customer reached abandonment, released the queue, lost mood, retained a negative personal facility memory, emitted both service and abandonment alerts, and exposed `구매 포기` as the visible action phase.
# 2026-07-23 Paused stair and low-needs AI audit

- `CharacterVisual.HideForTraversal` uses `Time.realtimeSinceStartup`, `WaitForSecondsRealtime`, and an unscaled expiry check. Therefore the stair coroutine correctly pauses on scaled waits, but its visibility fail-safe restores the actor while the game remains paused.
- `CharacterAiDecisionContext.Capture` chooses its strongest need from every registered need. `FUN` can therefore drive `EmergencyScore`, while `BuildEmergencyJobGivers` has no fun response and falls through to work/wait.
- Emergency job construction currently adds only the single strongest need action. If that facility/action is unavailable, another simultaneously critical survival need is not tried before wait.
- `AIBrain.UseOwnerWorkActions` omits Eat, Rest, Toilet, and Hygiene. Owners with depleted needs have no self-care action to select.
- `AIEat.CanStart` rejects every on-duty worker. Hunger is not part of `WorkDutyController.ShouldTakeOffDuty`, so a starving on-duty worker can remain unable to eat indefinitely.
- Stair traversal itself already uses scaled `WaitForSeconds` and scaled movement. The only pause leak was `CharacterVisual`'s realtime fail-safe, so the correct fix is to scale the fail-safe rather than alter stair timing or DOTween globally.
- Low hunger should interrupt current work without switching the worker off duty. Sending hunger through the off-duty state breaks return-to-work semantics and makes ordinary meals look like schedule changes.
- Emergency selection must retain every urgent survival candidate in weighted order. Choosing only one need makes a missing facility turn a solvable hunger/rest/hygiene combination into a generic wait.
- The focused naturalness regression suite passes after excluding leisure from emergency selection, adding survival fallbacks, and exposing owner self-care actions. Two broader `StaffDutyDebugScenarios` cases around emergency-priority and expedition-return wake-up remain separate pre-existing failures and were not counted as proof for this fix.

# 2026-07-23 Stationary AI fallback audit

- The reported actor is not movement-locked. Its debug panel shows `Emergency -> WaitJobGiver`, no target, and every self-care action rejected by `CanStart`, so the pipeline intentionally commits a stationary wait.
- A wait action is currently allowed to be the terminal fallback even when the actor is healthy enough to move. Repeating that fallback makes a living actor look frozen despite the BT and scheduler continuing to tick.
- Low mood already has a mood-impulse model, but several impulse types map back to `Wait`; without a guaranteed locomotion/micro-action fallback, bad mood can still present as standing in place.
- `AIWait` does request a moving idle behavior, but high recent movement pressure selects `InspectFacilityIdleBehavior`, whose implementation is only `StartWait`. The unresolved emergency then selects the same branch again.
- Queue waiting is the only stationary wait that should remain intentionally static. Ambient inspection, weather shelter, complaint, and unresolved-need fallbacks need a bounded pause followed by movement or a fresh decision.
- Routine/job-giver mood bias alone was insufficient because the final routine-group multiplier could make ordinary work win again. Low-mood autonomy therefore has to be enforced once more at the final cross-group candidate comparison.
- The fixed idle path keeps deliberate queue/chat waits bounded, converts inspection and generic wait into reachable roaming, and ends a failed no-path wait quickly so the next decision can retry instead of holding an infinite action.
- A real GameplayScene probe with no LLM mood impulse held the actor at mood 17-20, selected `RoutineUtility -> Wait -> 기분 내키는 대로 배회`, and visited six distinct grid cells during the observation window.

# 2026-07-23 Dark survival V11 findings

- Runtime-only filth work targets cannot be resolved through the static `BuildingSO` catalog. Their information panel must format the runtime `WorldFilthWorkTarget` directly.
- Runtime work targets created outside prefab injection must receive `ConstructBuildableObject(...)` before initialization so grid and work services are available deterministically.
- Water Tilemap world X is mirrored from logical grid X in this project; verification must query the runtime grid-to-tile conversion rather than assume identical coordinates.
- `Tilemap.GetUsedTilesCount()` reports distinct tile assets, not occupied cells. Visual verification therefore counts source-cell tiles explicitly.
- Filth priority is most reliable as target-owned runtime state: it raises Clean urgency and wakes eligible workers without mutating shared SO data.
- Desperate drinking needs two distinct contracts: stored/clean facility water wins first, while disabling those sources must expose the external unsafe-water fallback and its infection cost.
- A permanent social-memory entry uses `validUntil = 0`; restore must retain zero-duration snapshots because zero means indefinite, not already expired.
- Mood modifies breakdown probability, while `selfCare` and `patience` provide a bounded personality adjustment. Even the most stable personality cannot suppress a forced 100-burden breakdown.

# 2026-07-23 Exterior habitat decoration findings

- The ecosystem already consumed `Grass` and `Brush` resources, but had no world visual bound to those values; the missing link was presentation, not another food simulation.
- The authored flower PNGs are imported as six full cluster sprites. Layering several clusters at stable offsets produces a readable dense patch and lets resource thresholds remove whole clusters without modifying source art.
- Trees and rocks must remain nonblocking visual decoration. They use `OutsideObject`, while wildlife remains on `Default`, so actors pass in front without changing pathfinding or grid occupancy.
- `Grass` and `Brush` intent filtering is required when habitat radii overlap; a foraging animal now consumes only forage patches and a drinking animal only water patches.
- Pure EditMode ecosystem contracts must not create scene decoration roots. Automatic decoration creation is PlayMode-only, while the focused visual contract explicitly creates and disposes its runtime.

# 2026-07-23 Exterior pond visibility findings

- The original default water selector admitted DropZone cells and picked the lowest X cells. That put two tiny sources at Grid `(0,0)` and `(1,0)`, visually buried beside the entrance instead of reading as an exterior water feature.
- A runtime Tile uses `TileFlags.LockColor` by default, so `Tilemap.SetColor` did not tint the generated white/gray sprite. Setting `TileFlags.None` is required for clean/unsafe/foul source colors to appear.
- The logical grid world position is the floor of a three-world-unit cell, while a Tilemap sprite is centered in that cell. A half-height water sprite therefore needs a `0.25 - CellWorldHeight/2` local Y offset to sit on the ground.
- The longest exterior surface run is Grid X `31..59`. Placing shallow water at `56..58` and deep water only at the outer boundary `59` keeps the pond reachable without partitioning the exterior route.

# 2026-07-23 Zoom sky and centered dungeon findings

- The solid sky previously followed only the physical world width. At maximum zoom-out the camera viewed Y `-6..15`, while the sky covered only about `-0.29..14.29`, exposing the camera clear color at the frame edges.
- Background coverage must be the union of padded physical-world bounds and the current orthographic viewport. Recomputing on camera position, size, or aspect changes keeps every zoom level covered without stretching decorative foreground sprites.
- The 27-column dungeon interior was authored at Grid X `4..30` inside a 60-column world. A centered start is X `17`, so both area tags and all 93 authored placements require the same `+13` translation.
- After centering, the entrance resolves to `(17,0)`, the drop zone to X `14..16`, and the interior to X `17..43`. The camera world X is `-29.5`, exactly matching the dungeon interior center.
- Runtime wall-tile inspection confirms outer-wall tiles at both centered boundaries X `17` and X `43`; the maximum zoom-out camera capture shows both edges in frame.

# 2026-07-23 Entrance outer-wall gap finding

- The entrance door correctly occupied Grid X `14..16`, but three invisible `ExteriorZoneMarker` instances shared X `13` through fixture/overlay layers.
- `GridWallTileCalculator` used `GridCell.HasOccupant()` for automatic side walls, so those nonstructural markers made X `13` look like a building and pushed the visible wall outward to X `12`.
- Automatic side-wall topology now reads only `Building` and `Hallway` structural content. Dynamic actors, items, wildlife, filth, construction overlays, and exterior markers no longer move outer walls.
- Fresh PlayMode verification reports rendered wall `X12=false`, `X13=true`; the entrance arch and wall are visually adjacent.

# 2026-07-23 Exact world click finding

- `WorldInfoClickSelectionService` first used exact `Physics2D.OverlapPointAll` hits, but then fell back to `GridCell.GetBuilding()` whenever no collider was hit.
- `GridCell.GetBuilding()` intentionally searches every occupant layer and ranks hallway last, so a bare floor click still returned the cell's `Hallway` object and opened corridor information.
- Ordinary facilities already own runtime colliders. Their selection now requires the pointer world point to overlap that collider; sharing a grid cell or being nearby is not sufficient.
- Structural wall and interior-door visuals are tile-based and do not always own colliders, so they retain a strictly same-cell `GridLayer.Building` fallback. Hallways, dungeon doors, and normal facilities are excluded from that fallback.
- The actual pointer regression clicked a facility collider and opened only that facility, clicked a collider-free hallway cell `(28, 0)` and opened nothing, then verified character-over-building priority. The report finished with zero failures, errors, or warnings.

# 2026-07-23 Consecutive wildlife click finding

- `WildlifeInfoPanel.OnTriggerEvent` assigned `current = wildlife` before calling `popupService.CloseAll()`.
- On the first click the panel was not yet in the popup stack, so the assignment survived. On a consecutive click, `CloseAll()` closed the already-open wildlife panel and `OnClose()` reset `current` to null after the new assignment.
- Clicking a building between wildlife clicks removed the wildlife panel from the popup stack, which explains why the next wildlife click appeared to work again.
- The panel now closes the prior popup stack first and assigns the clicked wildlife afterward. Repeated clicks therefore refresh and retain the same target instead of clearing it.
- The Input System regression performs two consecutive clicks at the same wildlife collider and verifies `CurrentWildlife` and the visible panel after both clicks.

# 2026-07-23 Wildlife horizontal-facing finding

- Wildlife facing used `step.To.x - step.From.x`, but this project's `Grid.GetWorldPos` maps logical X with `origin.x - gridX`; increasing Grid X therefore moves left on screen.
- The source animal sheets face right, so rightward world movement must keep `flipX=false` and leftward world movement must set `flipX=true`.
- Facing now uses the actual world-space X delta between movement-step endpoints, which remains correct if the Grid origin or coordinate mapping changes again.
- The focused natural-motion contract and live GameplayScene checks pass in both directions for every currently spawned species.

# 2026-07-23 Defense interception audit

- Invasion intruders currently run an independent movement coroutine and never enter an engaged state. Defense facilities can delay them, but guards do not stop that coroutine.
- `SuppressPriorityTarget` moves the guard onto the intruder's exact cell and applies one-way damage every `0.55s`; the intruder neither retaliates nor stops advancing.
- Guard work currently behaves like ordinary facility work. `InvasionSpawnedEvent` has no runtime listener that assigns on-duty Guard workers to an intruder.
- Character zero health immediately triggers death and despawn, so retreat and replacement policy directly controls guard survival.
- The existing boss-only owner rally chooses a shared hallway target rather than an Administration room. It must be replaced by evacuation for every invasion.
- The defense feature panel already owns threat, intruder, facility, and report sections, making it the correct home for policy editing and live engagement status. Several strings in that section are mojibake and need replacement while it is changed.
- The current top-level save version is V11 and invasion state already has a dedicated snapshot, so V12 can extend that boundary without mixing policy state into character or shared SO assets.

# 2026-07-23 Defense interception completion findings

- The live intruder coroutine must consult the engagement runtime before every Grid step. Merely pausing facility damage is insufficient because a previously started movement path can otherwise cross the frontline.
- Combat presentation must animate only the actor's visual child. Moving the actor root for a lunge corrupts logical Grid occupancy and can let the intruder or guard appear to cross the line.
- Policy switching keeps the same engagement and intruder reservation while swapping lead and reserve positions. Verification must follow the intruder runtime rather than assume a replacement engagement ID.
- Owner final defense can be planned while the intruder is still several cells away. `InterceptPlanned` is expected until the intruder reaches the reserved stop cell; only then does reciprocal combat begin.
- A real PlayMode run held the intruder at `(1,0)` against a lead at `(2,0)` for at least three exchanges, with reciprocal damage and no additional facility damage. Policy switching changed the lead without moving the intruder.
- The owner reached fallback evacuation cell `(41,2)`. After the non-owner frontline collapsed, final combat held the intruder at `(40,2)` against the owner at `(41,2)` for 20 exchanges with no reserve.
- Unity 6000.3.8 emits one editor-startup `The referenced script (Unknown) on this Behaviour is missing!` warning despite project-wide loaded-scene, prefab, ScriptableObject, animator, renderer-feature, and volume-profile scans finding no missing project scripts. Unity issue UUM-133323 lists the fix in 6000.3.12f1. After clearing that startup-only engine warning, the complete defense probe produced `Error 0 / Warning 0`.

# 2026-07-23 Developer mode findings

- Commands remain maintainable when each provider declares category, exact target contract, mutation status, and execution; the registry validates 112 unique IDs.
- Exact targeting uses pointer colliders for actors/items/facilities and only the resolved cursor cell for GridCell commands. There is no nearest-target fallback.
- Pure overlays do not mark a run modified. Stateful commands do, while palette visibility, targeting, cheats, and overlays reset when developer mode is disabled or a save is loaded.
- The palette remains non-modal at `1600x900` and becomes a bounded scrollable bottom sheet at `900x1600`.
- Camera Capture comparison confirmed the Grid overlay appears only while enabled and leaves no lines, labels, or pooled renderer residue after disable.

# 2026-07-23 Construction material delivery audit

- `WorkOrderRuntime.TryCreateConstructionOrder` immediately requests every missing material through `WorldItemStackRuntime.TryRequestFacilityDelivery`.
- Construction readiness correctly consumes only `FacilityBuffer` stock at the construction destination, so the expected final step is a worker deposit.
- The suspicious path is the delivery-request implementation: it may be representing demand by creating a visible `Loose` stack at the construction cell instead of reserving physical stock at its warehouse/source location.
- Correct behavior is source-preserving: order creation creates demand/reservations, pickup removes quantity from the warehouse stack, and only worker deposit creates the construction-site buffer.
- `TryRequestFacilityDelivery` delegates part of its work to a dedicated `RequestLooseStockDelivery` method. The construction bug is therefore localized around request-time stock conversion and the haul-plan candidate rules.
- Root cause confirmed: `TryRequestFacilityDelivery` calls `warehouse.Inventory.Withdraw(...)` immediately, removes the physical `Stored` quantity, then respawns it at the warehouse cell as a destination-tagged visible `Loose` stack.
- The stack is not teleported to the construction cell, but it is incorrectly dropped onto the warehouse floor before any worker pickup. That is the yellow pile visible immediately after placement.
- The fix must let destination-tagged warehouse stock remain `Stored` and hidden, make it haulable only as outbound reserved stock, and withdraw aggregate warehouse inventory only when the worker actually picks it up.
- Both pickup APIs currently only decrement the selected world stack and add it to `CharacterCarryInventory`; they do not touch `WarehouseInventory`. This confirms the aggregate withdrawal was intentionally front-loaded and must move into pickup for outbound stored stacks.
- Facility deposit already has the correct endpoint: carried items become `FacilityBuffer` only after the worker reaches the destination.
- The haul planner already understands destination-tagged stacks and routes them to `FacilityBuffer`, including multi-pickup plans and partial carry quantities.
- A focused extension is sufficient: allow only destination-tagged `Stored` stacks as outbound haul candidates, then perform the matching warehouse aggregate withdrawal atomically during pickup.
- Stored-stack save restoration currently rebuilds each warehouse aggregate from `destinationId` values prefixed with `warehouse:`. Overwriting that field with a construction destination would lose warehouse ownership on reload.
- Outbound stock therefore needs separate source-storage metadata. Cancellation must clear the delivery destination and merge the reserved quantity back into normal stored stock rather than deleting it.
- `DungeonPhysicalItemSaveData` currently enforces nested version 1 exactly. Source-storage ownership can be added as an optional serialized field while retaining version 1 compatibility; older V12 saves deserialize the new field as empty.
- `WarehouseInventory` exposes bounded `Withdraw` and `Deposit` operations but no reservation model. Reservation should remain physical-stack metadata, with pickup performing `Withdraw` and rolling back via `Deposit` if carry insertion fails.
- Existing item regressions explicitly expect warehouse stock to drop at request time, so they currently preserve the defect. They must assert stock remains unchanged and no visible loose stack appears until pickup, then assert the aggregate drops at pickup.
- `BuildPlacementUxPlayModeVerifier` bypasses hauling by spawning a `FacilityBuffer` directly at the site. It cannot be final proof for this fix and needs either a real-haul path or a separate focused pointer/play verifier.
- The first live haul exposed a second root cause: warehouse storage IDs use `BuildableObject.GridId`, which is the shared building-definition ID. Two warehouses of the same type both became `warehouse:1050`.
- The worker reached the reserved stack's physical warehouse, but pickup resolved the other same-type warehouse by the colliding ID and could not withdraw its stock. Warehouse ownership must use a per-building persistent/runtime instance key.
- Warehouse keys now use `building definition ID + center grid position`, which is stable across saves and unique for same-type warehouses. Legacy two-part IDs are normalized by matching the saved stack position during restore.

## 2026-07-23 Medieval dark fantasy combat V13

- The defense and offense loops previously owned separate damage assumptions. Both now route attacks through `ICombatResolutionService`, with adapters supplying real Grid distance/LOS or formation distance/cover.
- The active equipment instance, not a character template, is the authoritative source for range profiles, attack verb, quality, loaded ammunition, fire modes, recoverable throws, armor layers, shield state, and durability.
- Wildlife hunting still used a bespoke random hit roll after defense and offense had moved to the shared core. It now uses the same line-of-sight, friendly-fire, cover, range, evasion, body-part, and presentation rules.
- Wildlife uses a deliberately smaller body profile: head, torso, and combined limbs. Limb damage lowers mobility/evasion; vital-part destruction kills; the profile is persisted in wildlife save data.
- Ranged hunters now seek a valid firing cell instead of always pathing adjacent, refuse unsafe friendly-fire lines, reload from their physical carry inventory over scaled game time, and stop cleanly when ammo or a firing position is unavailable.
- A PlayMode command probe exposed a manual-move lock leak: owner evacuation could cancel the movement coroutine without clearing `AIBrain.manualCommandActive`. `AbilityMove.CancelActiveMovement` now completes cancelled manual commands and releases the lock.
- Live defense verification retained the intended phase order: 12-second external rally with guards waiting, dispatch only after breach, then four held reciprocal exchanges on adjacent cells.
- Unity Console finished at `Error 0 / Warning 0`. The MCP camera preview renderer failed twice for the live camera, while Unity's direct Game View screenshot succeeded.
## 2026-07-23 Construction material delivery

- The yellow pile was not a harmless preview. `TryRequestFacilityDelivery` withdrew aggregate warehouse stock and respawned it as a visible `Loose` stack at the warehouse cell as soon as the construction order was created.
- Construction readiness already consumed only `FacilityBuffer` stock, so the defect was isolated to the request/pickup boundary.
- Warehouse building-definition IDs are shared by every instance. Using only `GridId` as storage identity caused two same-type warehouses to collide; storage IDs now include the warehouse grid position.
- The correct three-stage ownership model is now explicit: ordinary hidden `Stored` stock, destination-reserved hidden `Stored` stock, then carried stock and destination `FacilityBuffer`.
- A delivery request does not alter aggregate warehouse inventory. Pickup atomically withdraws it, and failed carry insertion deposits it back.
- The pointer-driven build verifier previously spawned `FacilityBuffer` stacks directly. That shortcut was removed so it cannot hide a future request-time drop regression.
- The work-amount save contract had a stale `save.version == 9` assertion despite the product using V12; the assertion now follows `DungeonGameSaveData.CurrentVersion`.

## 2026-07-26 V16 integration audit

- `GameplayScene` contained both the production owner command controller and a priority-command duplicate, plus production and `_Test` regular-customer runtimes. Exact-one composition validation is required because first-match lookup silently accepts this corruption.
- `ExpeditionEquipmentRuntime` and `ICombatEquipmentRuntime` both authored inventory, loadouts, crafting, offense modifiers, and save data. Offense applied both bonus paths, so consolidation must remove the legacy stat block rather than adapt both indefinitely.
- The common combat runtime already owns persistent equipment instance IDs, quality, durability, ammunition, and active loadouts, making it the correct authority. Its missing piece was work-unit crafting and physical material/output integration.
- Offense weakening, prisoner rewards, special-monster rewards, and recruit rewards are counters only. They must become regional pressure or pending physical/persistent arrivals before the old reward-state fields can be removed.
- `SurvivalFoodRuntime` both withdraws food at daily settlement and allows real meal completion to consume food. Daily withdrawal must become forecast/reporting only to avoid double consumption.
- Exterior incidents currently advance through text and timers without persistent actors, inventories, theft stacks, rescue patients, or handler-owned stages.
- Circus fame and injury history are recorded but do not gate treatment, contracts, release, or performer availability.
- Blood and memory extraction currently collapse into generic Mana-style stock. They need Biological and Knowledge categories plus physical, work-based consumers.
- `CharacterAiPerfSettingsSO` and report types exist without a runtime recorder, so the current performance surface cannot provide trustworthy rolling avg/p95/max/GC/path-cache evidence.
- Offense targets currently carry no region or faction identity. The human/rival reward handlers only increment `OffenseRewardState` counters, and the terminal truth target still grants a meaningless rival weakening reward.
- `ExteriorActivityRuntime` already owns visible departure/return movement, a physical entry point, body-health checks, and medical-order creation. V16 return rewards should attach to this completed return boundary rather than create a parallel arrival animation service.
- Exterior incident persistence currently stores only kind, zone, text, and remaining seconds on a zone marker. There is no handler-owned actor, inventory, stage, or outcome state.
- V15 offense persistence serializes all abstract reward counters inside `DungeonOffenseRewardSaveData`; regional pressure and pending arrivals should be independent domain sections so the offense service no longer owns their restore order.
- Save restoration is already dependency-sorted by section and phase, so V16 can express the required equipment/items → characters/wildlife → captivity → arrivals → incidents → regions order without central orchestration.
- `InvasionIntruderRuntimeFactory` is the single constructor boundary for runtime intruder actors and is a suitable point for applying a captured regional pressure snapshot once per spawn.
- Offense enemy templates are materialized in `OffenseEncounterCatalog.CreateEnemies`; applying regional armament/manpower factors there avoids a second post-construction stat mutation path.
- Invasion intruder health and attack configuration is finalized in `InvasionIntruderRuntime.Initialize` inside `InvasionIntruderSystem.cs`, so regional modifiers should be supplied with the spawn settings before actor health is scaled.
- `CharacterPopulationService` already owns deterministic persistent IDs and full generated growth profiles, but exposes no API for adding a reward candidate. Extending that boundary avoids a second profile generator.
- Recruitment activation can bind an actor back to an existing population profile by matching `CharacterIdentity.PersistentId`; a reward candidate therefore needs a population profile and a matching `RegularCustomerRecord`, not a counter.
- Wildlife spawning is private to `WildlifeRuntime`. A narrow `TrySpawnArrival` method on `IWildlifeRuntime` can reuse the catalog, grid validation, hierarchy, actor initialization, and registry path without exposing general mutation internals.

## 2026-07-26 AI profile boundary and allocation findings

- `CharacterNeedCatalog.All` rebuilt a sorted array on every access. Survival scoring calls it
  for each AI candidate, making the catalog a measurable allocation hotspot at population scale.
- Offscreen/nonselected actors do not need full utility strings or breakdown objects. Retaining
  compact numeric scoring while collecting details only for selected diagnostics preserves
  decisions and substantially lowers garbage.
- `WorldCharacterNameplate` previously captured a complete deprivation snapshot only to display
  the highest burden and breakdown state. A narrow display-state query avoids grouping,
  dictionaries, and arrays on every visible nameplate update.
- The first PlayMode profile implementation sampled immediately after forced GC and reused the
  last warmup scheduler timing. That made a 2.6-second sample window report impossible 17-second
  scheduler frames. Discarding two transition frames produces coherent wall, frame, and
  scheduler timing.
- Unity 6000.3.8's Mono runtime returns zero from
  `GC.GetAllocatedBytesForCurrentThread()` even around a known 4KB allocation. Scheduler-only
  allocation is therefore explicitly reported as unsupported; `GC Allocated In Frame` remains
  the authoritative Editor-wide counter.
- The stabilized 100-character result is frame `2.77ms average / 3.42ms p95`, scheduler
  `0.370ms average / 0.497ms p95 / 0.632ms max`, all 100 trees ticked, and zero decision/path
  budget overflow.

## 2026-07-26 Weighted navigation and 500-character profile

- Unweighted BFS could not represent shallow-water speed, door policy, traversal penalties,
  or cost-aware target choice. Fixed destinations now use A*, while multi-target candidate
  scoring retains a weighted Dijkstra field.
- The current 60x3 gameplay and 96x3 stress grids are small enough that one Job per route
  would add more scheduling overhead than search work. The optimized weighted A* benchmark is
  about 11.3 microseconds per query.
- The largest apparent 500-character hotspot was an Editor test provider repeatedly calling
  `FindFirstObjectByType<GridSystemManager>`. Caching the fixture manager reduced the
  diagnostic average from 13.61 ms to 2.60 ms.
- The final staged 500-character profile passed: frame average/p95/max
  `3.39/4.37/15.40ms`, scheduler `1.228/1.809/2.580ms`, and no sampled frame exceeded
  16.67 ms.
- Broad multithreading is therefore deferred. Only immutable, batched offscreen scoring or
  route requests are safe future candidates; Unity objects, door access, reservations, and
  route commit remain main-thread responsibilities.

### V18 identity and physical-state follow-up

- Warehouse storage destinations were still generated from `GridId:centerX:centerY`, with `GetHashCode()`
  for non-building implementations. V18 now requires the warehouse's typed `BuildingInstanceId` at the interface.
- A physical-stack-derived stock query is safe as a non-owning index during cutover, but remaining
  `WarehouseInventory.Deposit/Withdraw` callers must move to the transfer service before its quantity dictionary
  and snapshot fields can be deleted.
- Warehouse snapshot V3 now proves that aggregate stock is not a save authority: only capacity and acceptance
  policy serialize, while the derived dictionary is cleared on config restore and rebuilt from physical stacks.
- The old equipment item component stored only identity, definition, material, quality, and durability.
  Ammunition, owner/world state, evolution, slots, and module condition were absent. Schema V2 carries the full
  equipment and attached-module payload as the prerequisite for removing separate equipment-instance persistence.

### V18 Phase 85 single-authority findings

- Removing only the combat save lists was insufficient: carried items retained `sourceStackId` and components but
  dropped `ItemInstanceId`, so equipment could fork when deposited into a new physical stack. The carried DTO and
  transfer API now preserve the typed instance identity explicitly.
- `SpawnUnique` always allocated a fresh instance ID. Crafting output and loadout drop therefore needed a separate
  `SpawnExistingUnique` path that materializes repository-owned unique state without minting another identity.
- The former deposit path searched equipment by its old source stack and synthesized a normal-quality replacement
  when lookup failed. This silently discarded durability, material, modules, and lineage; it now fails loudly.
- Combat material policies still keyed facilities as `definitionId:x:y`. Changing that key to the required
  `BuildingInstanceId` removed another coordinate-based persistence fallback exposed by the Phase 85 regressions.
- Equipment modules must be restored after equipment shells but before slot-reference sanitization, and tests must
  use a slot-bearing definition. The failed dagger fixture exposed that a valid zero-slot item correctly discards
  an impossible installed-module reference.
- Warehouse tests can use Editor-only physical-stock fixtures, but the production API no longer exposes aggregate
  writers. The old `Deposit/Withdraw/AddStock` names were removed so new tests cannot normalize a second authority.

## 2026-08-01 Branched production network V3

- The production dependency catalog now indexes recipe, equipment, construction, facility-supply,
  medical-procedure, and defense-ammunition consumers instead of treating recipes as the whole graph.
- Concrete recipe inputs contain no `stock-item:*`; flexible fuel/feed selection remains available only
  through value-bearing facility supply profiles and persists the selected concrete item ID.
- Shared intermediates require at least two real direct consumers, strategic intermediates require three,
  fake `sink:*` consumers are rejected, and post-acquisition conversion depth is capped at four.
- All production facilities own separate persistent input/output buffers. Output space is reserved before
  work starts, so a full output pauses only that bill and does not corrupt an upstream conveyor or worker.
- The old wort-only chain was removed. Malt, fermented liquor, grape juice, curd, dough, filling,
  salted meat, ration mixture, washed vegetables, and brined vegetables now branch into real products.
- Production order persistence is V4; research/equipment compatibility is V5 and rejects preceding V4 runs.
- Medical procedures are first-class research rewards. Dedicated construct-core engineering and dining
  operations facilities close the final direct-reward gaps without adding dummy recipes.
- Final Unity MCP regression report passes resource generation, equipment, the production graph,
  production runtime contracts, research/equipment validation, and pacing at 32.2/80.4/234.3/372.0 days.
- Unity MCP captured the active Main Camera at 1920x1080 and the final Console audit returned
  `Error 0 / Warning 0`.

## 2026-08-01 Item architecture V6 audit

- `ResourceItemDefinitionSO` mixes identity, economy, production classification, research,
  food, medicine, fuel, feed, and intermediate policy in one flat asset.
- `DungeonItemCatalogSO` is present but its serialized list is empty; runtime lookup falls through
  several hardcoded static definition classes and synthetic `stock-item:` / `equipment-item:` IDs.
- `ResourceItemDefinitionSO.ToDungeonItemDefinition` discards every optional behavior field, so
  consumers must query both the physical-item and resource-economy catalogs.
- `ResourceDungeonItemCatalogProvider.TryGetDefinition` currently fabricates a default definition
  for unknown IDs and therefore reports success for missing content.
- Mutable equipment and food state live in separate systems keyed back to a world stack ID, while
  corpse and contamination fields are embedded directly in the generic stack save DTO.
- The target must keep SOs immutable and consolidate authoring, without moving per-instance state
  into shared assets.
- `DungeonStory.Items` is a low-level assembly with no gameplay-model references, so canonical
  authoring belongs in the economy/model assembly while generic instance persistence stays in Items.
- A strict Resources index can load `ItemDefinitionSO` across `SO/**`; resource economy then becomes
  a typed projection over the same authored definitions instead of a second item authority.
- Stack compatibility must use definition ID plus stack-affecting component state, preventing
  fresh/spoiled, damaged/pristine, or provenance-bearing instances from merging by ID alone.
- The base resource builder intentionally precedes research-overhaul reward generation. A unified
  rebuild that stops after the base builder erases V3's extended item-consumer graph, so the item
  pipeline must run resource -> combat -> research/overhaul before indexing final definitions.
- `PhysicalItemDebugScenarios` still contains a legacy `save_v10_contract` assertion while the
  current global save contract is V17; that isolated failure is not an Item V6 schema regression.
- Final unified generation contains 296 canonical SO definitions. The dedicated generated folder
  contains 110 survival/wildlife/medical/special/equipment assets and all 110 reference the concrete
  `GenericItemDefinitionSO` script GUID; missing-script references are zero.
- Item V6, production V3, research/equipment, signature isolation, and pacing all pass together at
  32.2/80.4/234.3/372.0 days. Unity MCP captured Main Camera at 1920x1080 and Console ended 0/0.

## 2026-08-01 V18 authority-normalization audit

- Item V6 did not finish the single-authority cutover: `DungeonItemCatalogSO`, hardcoded `*ItemDefinitions`, installation/blueprint synthesis, `stock-item:*` conversion, and fabricated unknown definitions remain reachable at runtime.
- Warehouse aggregate counts and physical stacks are both mutable and separately persisted; equipment instances are also stored beside physical item components. These are P0 duplicate-authority defects.
- Persistent ownership still falls back to actor names, `GetInstanceID()`, positions, and definition IDs in multiple character, building, combat, wildlife, and reservation paths.
- `GameData` is a mutable ScriptableObject for money/calendar/speed state, and direct money access bypasses the transaction runtime in many callers.
- Both legacy offense and V17 offense runtimes/save sections are registered. The final system must retain V17 behavior and remove the legacy bridge and duplicate save ownership.
- Save restore validates section order but applies live sections sequentially, so a late failure can leave a partially restored world.
- The project has 784 runtime files and roughly 288K lines in default `Assembly-CSharp`, about 501 runtime interfaces, roughly 401 optional-interface parameter occurrences, and 12 product files above the existing 2,169-line ratchet.
- Architecture tests are stale: they still assert older save/version and resource-loading expectations and rely heavily on source substring/regex checks.
- The planning session-catchup helper failed after detecting 73 unsynced messages because the Windows CP949 console could not encode U+2013. Context was recovered from the planning files and `git diff --stat` instead.
- `DungeonItemCatalogSO.cs` still contains all forbidden Item V6 escape hatches in one place: `FromStockCategory`, equipment synthesis, `GetDefinitionOrDefault`, blueprint synthesis, installation-kit synthesis, and a fabricated generic definition for every unknown ID.
- `ResourceDungeonItemCatalogProvider` also has four optional constructor dependencies and constructs its own Resources loader/catalog, so strictness depends on how it was instantiated.
- The canonical `ResourceItemDefinitionCatalog.GetRequired` already has the desired fail-loud behavior; the old provider can become a thin projection over that catalog without inventing a second definition type.
- The root save version is still 17 and save-slot incompatibility strings are mojibake. Several debug scenarios still pin V16 or V17 explicitly, confirming the existing architecture ratchet is stale.
- `DungeonSaveSectionRegistry.RestoreAll` validates duplicate/missing envelopes but invokes live `Restore` methods in sequence immediately; it has no preflight/staging contract and labels current failures as V16.
- No authored `ItemDefinitionSO` has a `stock-item:*` ID. Those IDs remain synthetic runtime-only identifiers even though Item V6 reported a strict 296-definition catalog.
- Equipment IDs are authored (`equipment-item:*` has 86 YAML occurrences), but stock-category calls in surgery, survival, offense, shops, wildlife, grand projects, fluids, and tests would fail immediately under a strict catalog.
- Therefore Phase 83 must eliminate stock-category item creation at each call site before making the provider fail-loud. Recipe/material inputs become concrete item IDs; flexible facility fuel/feed remains tag/value selection rather than a fabricated item.
- The canonical catalog has no Water or Blueprint-category item entries. `resource:clean-water` exists but is incorrectly authored as General; seven facility-blueprint assets exist but have no corresponding item-definition SOs.
- A deterministic concrete default can replace remaining category-to-item spawn requests during the cutover: preserved ration, lumber, dagger, mana crystal, clean water, standard medicine, low fuel, arrow, blood, memory residue, and a real blueprint definition. This is a concrete-ID compatibility mapping, not a synthetic definition.
- `UnifiedItemDefinitionAssetBuilder` is currently still a second content source because it calls hardcoded `*ItemDefinitions` to generate canonical assets. After the existing assets are made complete, those branches must become explicit one-time migration code and runtime hardcoded definition methods must be removed.
- The resource loader already supports `LoadRequired<T>`, so one required `GameContentCatalogSO` bootstrap can replace item `Resources.LoadAll` without adding Addressables.
- `DefenseCombatPresentation` was another hidden authority path: it constructed static combat and item catalogs inside a MonoBehaviour. Its weapon sprite lookup can use the actor's already-injected `IWorldItemStackRuntime.CatalogProvider` and the authored equipment item ID instead.
- The explicit catalog cutover requires 604 definitions today: 296 pre-existing physical items, 301 building installation kits, and 7 research blueprints. This closes the two runtime synthesis categories without using dummy fallback definitions.

## 2026-08-01 V18 Phase 86 findings

- Mutable `GameData` was not isolated to one manager: UI pause flows, debug commands, settlement, shops, construction, and save restore all wrote its reactive fields. The cutover required named authorities plus updated Editor fixtures, not a type rename alone.
- Static run leakage existed in four independent forms: a user-settings `Current`, active carry inventories, cover durability by source ID, and skill execution/work snapshots. The correct scopes differ: a run service for cross-entity lookup and an actor component for actor-local transient state.
- Presentation and character-skill settings were still synthesized or resource-loaded outside the root catalog. Explicit root references now turn missing assets into boot/validation failures.
- Mandatory typed IDs correctly caused older test factories to fail at initialization. Tests now construct identities before domain initialization instead of weakening the runtime invariant.
- Phase 86 regression failures exposed stale V12 compatibility expectations and fixtures without physical stock views; both were corrected to the V18 boundary and derived-stock architecture.
# V18 Phases 87-88 findings

- The offense duplication was not limited to naming: four separately registered save sections captured overlapping expedition state. They are now one aggregate and only `offense.aggregate` is registered.
- Direct scene-runtime providers had leaked into recruitment, first-run objectives, codex, and the expedition feature UI. The new query/application boundary removes those cross-domain MonoBehaviour dependencies while keeping providers internal to offense persistence/composition.
- The former registry validated section presence only while mutating each section immediately. V18 restore now validates the manifest, envelopes, typed payloads, IDs, content references, and aggregate references before commit.
- Unity world replacement cannot be made by swapping a plain object graph because many current aggregates own scene objects. The implemented transaction therefore stages all serialized data first and captures a complete live rollback image before commit; an injected last-stage failure verifies no observable live state remains changed.
- Live PlayMode capture contains 54 sections and round-trips through the new manifest/preflight/transaction path successfully.
# V18 runtime authority findings — 2026-08-01

- The dominant defect was not raw class size but parallel ownership: SO/code fallbacks, physical/aggregate stock, physical/equipment instances, and multiple offense saves.
- A strict root catalog exposed real missing registrations immediately (`FacilityCrimeSettingsSO`, then `CharacterAiNaturalnessSettingsSO`); widening the editor collection scope to all authored Resources assets fixed the catalog rather than reintroducing fallbacks.
- Presentation assets were also bypassing the root through string paths. `GameMediaCatalogSO` now explicitly references audio, TMP font settings, title icon, and door material.
- Unity MCP import can report success before all dependent editor assemblies rebuild. DLL timestamps and `Editor.log` are the reliable freshness checks; clean compilation is required after cross-assembly signature changes.
- Large block rewrites must preserve UTF-8 explicitly. PowerShell's default `Get-Content` decoding corrupted Korean literals once; the affected files were reconstructed from Git UTF-8 sources before continuing.
- Character progression test fixtures were overwriting their immediate generator through a later generic dependency injection call. Reordering injection restored deterministic passive/active/ultimate tests.
- Strict persistent IDs correctly exposed a population promotion fixture that initialized an actor before assigning its profile ID; the fixture now assigns the persistent ID first.
- Current top remaining sizes are roughly 3.1k lines for deprivation, 3.0k performance probe, 2.6k equipment, 2.5k AI brain/offense, and 2.4k surgery. These require responsibility extraction, not region-only partial files.

## 2026-08-01 Phase 90 decomposition findings

- A line-count limit becomes useful only when it is a ratchet. Recording each existing exception with its current maximum allows legacy debt to compile while making every new violation and every regression fail immediately.
- `CombatEquipmentRuntime` mixed five ownership concerns: physical item state, character loadout references, crafting queues, module processing, and lineage-transfer orders. Extracting modules and lineage as state-transition Aggregates preserved the physical item repository as the only equipment/module authority.
- Persisting an equipment component was duplicated between the facade and module operations. `CombatEquipmentPhysicalStateWriter` now encodes repository-owned equipment plus attached modules through one path.
- Test-only absence is a capability, not `null`. Explicit empty catalogs and unavailable research capability objects preserve isolated fixtures without permitting production constructors to invent fallback rules.
- Deterministic seeded calculations and session randomness are different contracts. The former now uses a small deterministic sequence; the latter remains injected through `IRandomStreamProvider`.
- The project had no Unity Localization package or String Table assets, so merely introducing an error enum would still leave sentence ownership in code. Localization 1.5.9 and an active Korean `DomainFailures` table now provide a real presentation boundary.
- A String Table asset alone is not loadable at runtime: the active `LocalizationSettings`, registered locale, project locale, and Addressables localization groups must all exist. The validator now checks the authored settings/table, while the MCP regression proves runtime resolution.

## 2026-08-01 full-goal continuation audit

- The worktree contains 1,666 changed/untracked entries because the content migration authored hundreds of SO assets; unrelated user edits must continue to be preserved.
- The current architecture baseline still contains 53 oversized source exceptions. The largest are deprivation 3,410, performance diagnostics 3,245, AI brain 2,858, offense expedition 2,786, grid 2,567, surgery 2,565, and wildlife 2,513 lines.
- A broad source audit finds 738 production-code `out string failureReason/errorMessage/reason` declarations or call sites. The 21-code equipment slice is therefore only the first domain-error migration, not evidence that localization is globally complete.
- Fifteen production files still define or reference `*RuntimeProvider` types. Each must be classified as a real scene/capability boundary or removed as a policy-free wrapper.
- The runtime source scan still finds one direct `Resources.Load/LoadAll` occurrence and three `CreateInstance` occurrences; validator allowlists must be checked against the actual central loader/editor-only intent rather than assuming zero from a raw count.
- There are 850 non-Editor C# files under `Assets/Scripts`; an asmdef inventory must use filesystem enumeration because the first `rg --files` pipeline returned no entries despite the known Foundation assembly.
- `CharacterDeprivationRuntime` owned two clearly separable state groups before any pathfinding split: persistent deprivation state keyed by character and non-persistent safe-relief diagnostics. Moving both first reduces coupling for the later safe-drink planner extraction and prevents pathfinding code from becoming another save authority.

## 2026-08-01 deprivation decomposition findings

- 안전 음용의 “대상 선택·접근 예약”과 “코루틴 실행·재시도 제한”은 수명이 다르다. 전자는 `CharacterSafeDrinkPlanner`, 후자는 `CharacterSafeReliefRunner`가 소유해야 죽음·취소 시 예약과 실행 상태를 각각 명확히 해제할 수 있다.
- 붕괴 행동은 영속 결핍 상태를 소유하지 않는다. `CharacterBreakdownActionRunner`는 실행 중 actor ID와 코루틴 디스패치만 소유하고, 영속 상태는 계속 `CharacterDeprivationStateStore`에 남긴다.
- 이동 경로 재시도는 식수와 모든 붕괴 행동이 함께 사용하는 실제 정책이었다. `CharacterEmergencyMovement`로 추출해 두 행동 런너가 동일한 긴급 경로 실패 의미를 사용하게 했다.
- 감염, 금기 기억, 목격자 기분, 붕괴 종료는 행동 종류와 무관한 후속 효과다. `CharacterDeprivationConsequences`로 모아 행동 런너가 메인 런타임을 콜백 호스트로 참조하지 않도록 했다.
- 메인 런타임은 이제 tick/부담 계산/공개 질의/저장 조정에 집중하며 1,123줄이다. 이는 단순 partial 분할이 아니라 상태 권위와 실행 책임을 분리한 결과다.

## 2026-08-01 authoritative Phase 90 inventory

- 현재 기준선 예외는 52개다. 결핍 런타임 예외 1개가 실제 제한 충족으로 제거됐다.
- 프로젝트 asmdef는 플러그인 제외 18개이며 Foundation/Infrastructure/Presentation과 모델 계약 어셈블리는 존재한다. 그러나 대부분의 서비스 구현은 여전히 기본 `Assembly-CSharp`에 남아 있어 “asmdef가 없다”가 아니라 “구현 이동이 미완료”인 상태다.
- 비 Editor 직접 `Resources.Load` 1건은 루트 카탈로그만 읽는 승인된 `ResourcesAssetLoader`다. 비 Editor `CreateInstance` 3건은 콘텐츠 정의가 아니라 런타임 Tile 표현 생성이다. 현 validator가 금지하는 Definition/Settings/SO 합성과 구분된다.
- 실제 `*RuntimeProvider` 정의는 클래스 22개, 인터페이스 19개다. 이전 15개는 파일 수 기반 값이어서 최종 제거 조건의 정확한 분모로 쓰기에 부족했다.
- 다음 대형 예외는 성능 Probe 3,245줄, AI Brain 2,858줄, 원정 2,786줄, Grid 2,567줄, 수술 2,565줄 순이다.
- 성능 Probe의 3,245줄 중 570줄은 직렬화 모델·옵션, 약 1,100줄은 측정 월드 생성/밀집 시설 배치/스트레스 개체 생성, 약 370줄은 월드 상태 요약이었다. 이를 별도 소유자로 옮기자 MonoBehaviour는 실행 수명·ProfilerRecorder·파일 출력만 담당하게 됐다.
- 런타임 생성된 스트레스 테스트용 SO 복제본의 수명은 월드 구성기가 소유하며 `IDisposable`에서 파기한다. 콘텐츠 권위 SO 합성이 아니라 프로파일 전용 임시 복제라는 경계도 명확해졌다.
- `AIBrain.cs`는 뇌만 큰 것이 아니라 별도 런타임 객체인 `AIActionPlan`과 `AIAction` 548줄을 같은 파일에 담고 있었다. 이를 독립시킨 뒤에도 뇌 본체가 2,319줄이므로 행동 선택/제어 상태 분해가 계속 필요하다.
- Character stat 회귀는 코드 컴파일보다 강한 에셋 검사를 수행하며, 현행 `Customer_Orc.asset`에 `stat:shooting`이 없음을 드러냈다. 구조 리팩터링 회귀와 콘텐츠 완전성 실패를 분리 기록해야 한다.
- WorkAmount와 Combat 회귀의 실패는 새 코드 경로가 아니라 오래된 fixture가 필수 persistent building ID와 root anatomy catalog를 구성하지 않는 데서 발생한다. 최종 회귀는 엄격한 운영 계약을 약화시키지 않고 fixture를 갱신해야 한다.
- `WildlifeActor.Initialize`와 `NextRange`는 필수 난수 주입이 없을 때 `new RandomStreamProvider(1)`을 생성해 운영 규칙을 바꾸고 있었다. 이제 구성 전 초기화는 명시적으로 실패하고, 시각용 Sprite/Material만 재구축 가능한 별도 캐시에 남는다.
- AI 스케줄러의 힙 항목은 파생 인덱스이며 저장 권위가 아니다. actor 등록 집합과 due-time 사전을 참조하는 `CharacterAiDecisionSchedule`로 분리해 Clear/Remove/Schedule/Take가 한곳에서 버전 무효화를 수행하도록 했다.
- `CharacterEnvironmentRuntime.cs`에는 노출 상태 런타임과 770줄짜리 작업 배정 정책이 함께 있었고 두 객체 사이에 상태 권위 공유가 없었다. 파일 분리만으로도 실제 클래스 경계와 소스 책임이 일치했다.

## 2026-08-01 fluid-network findings

- 첫 유체망 추출 뒤 본체에 수질 변경과 스냅샷 조립 구현이 중복으로 남아 있었다. 최종 구조는 수질 변경을 `FluidNodeWaterRules`, 읽기 모델 조립을 `FluidNetworkSnapshotBuilder`, 실시간 조정을 `FluidNetworkRuntime`이 각각 소유한다.
- Unity MCP 동적 명령의 컴파일 성공은 변경된 프로젝트 어셈블리의 재컴파일을 뜻하지 않는다. `CompilationPipeline.RequestScriptCompilation` 완료와 Console 확인을 이후 모든 회귀 증명의 선행 조건으로 둔다.
- 강제 전체 빌드가 이전 집중 테스트에서 가려진 추출 오류를 드러냈다. 향후 분해 단계마다 전체 컴파일을 먼저 통과시켜야 한다.
- 산업 디버그 시나리오는 과거 141개 연구/32개 시설을 고정하고 있었다. 권위 에셋은 연구 168개, 산업 분야 45개, 상하수 분야 9개, 산업 시설 36개다.
- `ExteriorZoneMarker`는 단순 DTO가 아니라 시설 작업, 사건 상태, 저장 캡처, 그리드 등록/해제를 모두 소유하는 독립 런타임 객체였다. 같은 파일에 둘 이유가 없었고, 분리 후 외부 활동 조정기는 구역 생성·사건·원정 이동만 담당한다.
- Wildlife 오버레이는 생태 상태가 아니라 언제든 재구축 가능한 표현 캐시다. 별도 `IDisposable` 객체가 Sprite/Texture/Renderer 수명을 소유하게 해 생태 저장 권위와 분리했다.
- 기본 난수 폴백 제거 후 실패한 Wildlife 회귀는 운영 결함이 아니라 fixture DI 누락이었다. 테스트도 실제 `ConfigureRuntimeServices` 계약을 사용하도록 수정해야 엄격한 생성 경로가 유지된다.
- 축산의 자동 도축 후보 목록은 영속 상태가 아니라 정책 재평가용 재사용 버퍼다. 별도 평가기가 소유하게 하자 본체의 `animals`/`policies` 사전만 저장 권위로 남고 후보 그룹은 언제든 재구축 가능해졌다.
- 서커스 프로그램 예측은 공연 주문을 변경하지 않는 읽기 모델이다. `CircusProgramForecastService`로 분리하자 공연 주문 상태 전이와 UI용 예상 수익/위험 계산의 권위가 명확히 갈렸다.
- 산업 기능 표면과 탭 요약은 같은 도메인 데이터를 읽지만 다른 화면 계약이다. 별도 Presenter로 두면 한쪽 레이아웃 변경이 다른 쪽의 800줄 제한을 다시 침범하지 않는다.
- 캐릭터 요약 팩토리의 패널 경계/RectTransform 생성은 데이터 바인딩과 무관한 공용 View 구성 규칙이므로 `CharacterSummaryRuntimeLayout`이 소유한다.
- 해상도 후보 탐색은 설정 모달의 상태가 아니라 플랫폼에서 다시 계산할 수 있는 카탈로그다. 입력 단축키 MonoBehaviour도 모달 조정기와 별도 Unity 수명을 가진다.
- 사장 선택의 저장 모달 탐색과 라벨/레이아웃 생성은 선택 상태 전이와 무관한 View 규칙이며 별도 정적 도우미로 분리할 수 있다.
- 시설 정보 화면의 작업 버튼/진행도 렌더링은 시설 선택 상태를 소유하지 않는 순수 View 생성 책임이었다. `BuildingInfoActionViewFactory`로 옮기자 MonoBehaviour는 대상 추적과 갱신 수명만 담당한다.
- 구형 시설 납품 회귀는 물리 아이템 생성 실패 뒤 집계형 창고 입고로 내려가 결제 후 예외가 날 수 있는 실운영 결함을 드러냈다. 집계 쓰기 경로를 제거하고 물리 런타임 누락/생성 실패를 무변경 실패로 고정했다.
- 타이틀 캔버스와 EventSystem 생성은 화면 흐름 상태가 아니라 장면 UI 인프라 수명이다. 난이도/생존압 표시와 저장 슬롯 메타데이터 포맷도 입력 명령과 무관한 읽기 표현이므로 별도 소유자로 분리했다.
- 창고 기능 표면은 Query가 물리 재고/전망을 읽고 Command가 납품·정책·계약을 변경하는 명확한 경계를 이미 인터페이스로 갖고 있었지만 구현이 한 파일에 섞여 있었다. 구현 파일도 분리해 변경 방향과 소스 소유권을 일치시켰다.
- Unity MCP Console 조회는 실패한 프로젝트 컴파일을 한동안 0건으로 반환할 수 있었다. `Library/ScriptAssemblies` DLL 갱신 시각과 `Editor.log`의 `error CS`를 함께 확인해야 오래된 어셈블리로 회귀를 실행하는 오류를 막을 수 있다.
- 생산 작업대 연결선은 저장 상태가 아니라 선택 시 재구축되는 월드 표현이다. 패널 Presenter 밖의 전용 렌더러가 GameObject/Material 캐시를 소유하고, UI 행·버튼·진행도는 상태를 갖지 않는 View 팩토리로 분리하는 것이 맞다.
- 방어 기능 표면도 Query/Command 계약은 이미 분리돼 있었지만 두 구현과 Presenter가 한 소스에 묶여 있었다. 파일 경계를 계약 경계와 맞추자 화면 선택 상태, 방어 읽기 모델, 정책/시설 명령의 변경 이유가 분리됐다.
- 수술 창 파일에는 694줄 응용 서비스와 450줄 MonoBehaviour가 나란히 있었고 서로의 가변 상태를 공유하지 않았다. 파일 분리만으로 수술 규칙 조정과 반응형 UI 변경의 컴파일/탐색 경계가 명확해졌다.
- 연구 카탈로그를 168개로 확장한 뒤에도 수술·생산 작업대·종족 방어·서비스룸·경험 페이싱 픽스처가 141개를 고정하고 있었다. 한 회귀만 고치는 대신 모든 명시적 구형 연구 수 계약을 제거해야 전수 검증이 일관된다.
- 수술 회귀가 `RebuildAll()`을 호출해 작성된 콘텐츠를 검증 전에 덮어쓰고 있었다. 회귀는 `ValidateBuiltContent()`만 호출해야 하며, 연구 회귀도 빌더 실행 없이 현재 카탈로그를 검사해야 SO 최종 권위가 유지된다.
- 루트 도메인 카탈로그의 376번째 참조는 존재하는 AI 설정 에셋이었지만 YAML의 `m_Script`가 0으로 끊겨 있었다. GUID 복구 후 `ResourceGameContentCatalog` 전체 검증이 다시 통과했다.
- 연구 트리의 큰 줄 수는 단일 알고리즘 때문이 아니라 네 수명(데이터 표현, UI 요소 생성, 그래프 pan/zoom/center, 창 열림 중 일시정지)이 MonoBehaviour에 합쳐진 결과였다. 각 협력 객체는 저장 권위를 갖지 않고 입력과 파생 표현만 소유하므로 창 본체에는 선택·큐 명령·반응형 조정만 남겼다.
- 연구 저장 회귀는 실제 구현이 이미 섹션 V5 미만을 거부하는데도 과거 V3/V2 이관 이름과 더 짧은 구형 오류 문구를 기대했다. 테스트 이름과 판정을 현재 명시적 비호환 계약에 맞추되 운영 구현의 엄격한 거부 동작은 완화하지 않았다.
- 인스턴스 진화 화면의 장비 선택·안정제·정밀 재단조 선택은 시설 진화 상태가 아니라 화면 세션 상태다. 이를 `InstanceEquipmentEvolutionSection`이 소유하게 하고, 시설 Presenter는 시설 세대·후보·이전·촉매 선택만 조정하도록 경계를 맞췄다.
- 진화 효과 ID/촉매/상태의 표시명과 동적 GameObject 생성은 도메인 변경과 무관한 표현 책임이었다. 별도 Presentation/View로 옮긴 뒤에도 기존 시설·장비 진화 회귀가 동일하게 통과했다.
- 시작 파티 상세 탭의 선택 상태와 특성 툴팁 GameObject 수명은 준비 Aggregate가 아니라 화면 세션에 속한다. `StartPartyMemberDetailRenderer`가 이를 소유하고 Controller에는 사장 선택, 준비 시작/취소, 리롤/교체 명령, 런 시작 조정만 남겼다.
- 시작 준비 UI의 표시 규칙과 GameObject 생성 규칙을 별도 객체로 옮기자 Controller 의존성은 그대로 명시적으로 유지하면서도 표현 변경이 팀 구성 흐름을 다시 비대화하지 않게 됐다.
- 장비 진화의 방향 추론, 촉매 계열 배율, 재료 요구량 조립, 귀속 역사 노드 생성은 런타임 주문 목록을 소유하지 않는 결정적 규칙이다. `EquipmentEvolutionRules`로 이동해 주문 상태 전이/물리 재료 소비와 순수 계산 경계를 분리했다.
- `EquipmentEvolutionSaveData`, 런타임 인터페이스, 촉매 ID 파서는 가변 주문 구현과 독립된 계약이다. 별도 계약 소스로 이동하되 기존 `EquipmentEvolutionRuntime.GetCatalystFamilyPotencyScale` 호출자는 깨지지 않도록 전달 API를 남겼다.
- `AbilityMove`의 유휴 배회 후보 탐색과 경로 지원 형태 판정은 코루틴 상태와 독립된 질의다. 이동 중 재검증은 `AbilityMoveTraversalGuard`, 시각 방향/속도는 `CharacterMovementKinematics`, 막힘 후 AI 반응은 `GridMoveBlockedResponder`가 맡아 이동 요청 권위는 원본에 남겼다.
- Unity MCP 동적 명령 및 Console 0건만으로는 프로젝트 DLL 갱신을 증명할 수 없다. 이번에는 소스가 01:50 이후인데 DLL이 01:24/01:26에 머문 상태로 오래된 회귀가 실행됐다. 이후에는 명시적 `AssetDatabase.ImportAsset(...ImportRecursive...)`, 컴파일 완료, `Library/ScriptAssemblies` 시각 갱신을 모두 확인해야 한다.
- 긴 소스를 셸 출력에서 동적으로 추출할 때 도구 출력 축약 문자열이 실제 결과에 섞일 수 있다. `…`, `tokens truncated` 전수 검색과 실제 Unity 컴파일을 구조 추출 직후 필수 단계로 둔다.
- Grid placement 운영 경로는 VContainer 생성 콜백이 `BuildableObject.ConstructPersistentIdentity`를 호출하지만, Grid 픽스처의 콜백은 도메인 의존성만 구성하고 ID를 누락했다. 테스트 콜백도 운영 생성 계약과 동일하게 ID를 발급하도록 수정했다.
- 작성된 해부학 SO 12개는 이미 루트 도메인 카탈로그에 등록돼 있었다. 회귀 실패의 원인은 전투 fixture가 `Array.Empty<AnatomyProfileSO>()`로 빈 카탈로그를 명시 생성한 것이었다.
- 창고 fixture에 `IStockQuery`만 주입하면 초기화는 되지만 물리 저장소가 비어 있으므로 보충 작업이 선택되지 않는 것이 정상이다. 우선순위 회귀는 Editor 전용 물리 재고 질의를 통해 식량을 시드해야 하며 집계형 `Deposit` 경로를 되살려서는 안 된다.
- 거시 목표 실행은 AI 분기 순서와 다른 응용 책임이다. 목표 소비, 시설 회피, 불만·퇴장·파손 부작용, JobGiver 커밋을 `CharacterAiMacroDecisionRunner`가 소유하고 파이프라인은 분기 오케스트레이션만 유지한다.
- 컨베이어 필터는 경로 탐색 자체가 아니라 화물이 특정 노드에 입장할 수 있는지 판정하는 정책이다. 품목/카테고리뿐 아니라 금지품, 장비 재질·품질, 음식 신선도·오염을 한 정책 소유자가 판정해야 라우팅과 실제 이동이 같은 결과를 사용한다.
- 컨베이어 네트워크 상태는 저장 권위가 아니라 런타임 상태에서 재구축되는 Query 투영이다. 교착·무전력·의도 정지와 대표 막힘 원인은 별도 `ConveyorSnapshotProjector`에서 계산하도록 분리했다.
- 컨베이어 저장 변환은 노드/화물 Aggregate를 직접 운행하지 않는 순수 경계다. 복원 결과를 먼저 완성한 뒤 런타임 사전에 교체하게 해 부분 파싱 상태가 런타임에 노출되지 않는다.
- 작업 대상 적격성 평가는 후보 스캔 캐시와 별개인 정책 경계다. `WorkTargetEvaluator`가 작업 가능성·보충 공급·포로 노동·환경 판정을 담당하고, 선택기는 캐시와 점수 비교만 소유한다.
- Editor 회귀가 `FindObjectsByType<CharacterActor>`로 씬 전체를 훑으면 테스트가 만들지 않은 미주입 캐릭터 상태에 오염된다. fixture가 만든 명시적 참가자만 검증해야 작성된 SO 의존성 계약을 정확히 시험한다.
- 영속 ID 폴백 제거 후 공사 fixture도 운영 생성 경로처럼 배치 전에 `BuildingInstanceId`를 가져야 한다. 이름 기반 키를 되살리는 대신 고유 테스트 ID를 발급하니 작업 주문 생성·취소·고아 복구가 모두 동일 계약으로 통과한다.
- 신체 건강 런타임의 해부학 스냅샷·행동축·구형 표면 변환은 상태 소유권이 아니라 결정적 투영/정규화 규칙이다. 이를 별도 객체로 옮기면 가변 상태 사전과 생명주기 이벤트는 런타임에 남기면서 전투·수술이 같은 계산 결과를 계속 공유한다.
- LINQ의 `Select(ClonePart)`처럼 메서드 그룹으로 전달된 호출은 괄호 기반 호출부 검색에서 누락된다. 책임 이동 후에는 일반 호출뿐 아니라 메서드 그룹 식별자도 컴파일로 검증해야 한다.
- 한 소스에 두 개의 독립 MonoBehaviour가 함께 있으면 파일 줄 수뿐 아니라 탐색·생성 책임도 섞인다. 침공 감독자와 개별 침입자를 먼저 파일 단위로 분리하니 실제 초과 책임이 침입자 전술 루프라는 점이 명확해졌다.
- 침입자 경로 선택은 이동 코루틴의 상태 변경이 아니라 위험 인지도·작전 패턴·경로 고정 시간을 입력으로 받는 결정적 계획이다. 경로와 함께 인지도 버전/고정 만료를 결과 객체로 반환해야 계획 계산이 런타임 필드를 직접 소유하지 않는다.
- 생존 식량 런타임의 저장 복제·재고 인덱스·부패 컴포넌트 동기화·식사 원장은 하루 생존 상태 전이와 다른 변경 이유를 가진다. 각각을 별도 객체로 옮기면 `SurvivalFoodRuntime`에는 날씨/위험/시설 작업 조정만 남고 물리 아이템 권위도 명시된다.
- 물리 제작 회귀가 `EmptyResourceEconomyContentCatalog`로 실행되면 장비 재질 정책을 전혀 검증하지 못한다. 픽스처도 루트 `IGameContentCatalog`에서 장비·모듈·재질을 함께 구성하고 시설에 `BuildingInstanceId`를 발급해야 운영 경로와 같은 계약을 시험한다.

## 2026-08-02 shop/runtime authority findings

- 상점의 상품 재고는 물리 창고 재고와 동일한 개념이 아니다. 상품 진열/가격/재입고 주문 상태는 `ShopInventoryRuntime`이 소유하고, 시설 창고 수량은 계속 물리 아이템에서 파생되어야 한다.
- 특수 환경 시설 ID가 1500 이상에 존재하므로 에셋을 숫자 ID로 정렬한 위치와 카탈로그 코드 배열 위치를 비교하는 회귀는 잘못됐다. 코드 포함 여부를 ID/코드 키로 검증해야 한다.
- 창고는 용량만 설정된 채 비어 시작하는 것이 정상이다. `TotalStock == capacity`를 기대하던 회귀는 삭제된 집계 재고 시드 경로를 암묵적으로 요구하므로 `MaxCapacity == configured`와 `TotalStock == 0`을 별도로 검증해야 한다.
- `[RequireComponent]`가 테스트 GameObject에 컴포넌트를 추가해도 `CharacterActor`의 캐시된 `Identity`가 EditMode 생성 시점에 아직 연결되지 않을 수 있다. 영속 ID 픽스처는 `GetComponent<CharacterIdentity>()`로 실제 소유 컴포넌트에 직접 설정한 뒤 런타임을 사용해야 한다.
- 작성된 종족 SO 값과 Editor 빌더 입력이 다르면 어느 쪽을 고쳐도 다음 명시적 마이그레이션에서 되돌아간다. 최종 SO와 빌더 사양을 같은 변경에서 맞추되 회귀 실행 중 빌더를 자동 호출하지 않아야 한다.

## 2026-08-02 captivity authority findings

- 포로 정책 목록과 사용자 정책 시퀀스는 포로 상태 목록과 다른 변경 이유를 갖는다. `CaptivityPolicyRuntime`이 정책 복원·중복 검사·노역 재적용을 함께 소유해야 저장 시퀀스가 본체와 갈라지지 않는다.
- 공연 명성/특혜, 관리 상호작용 재료 예약, 호송 중 부모 Transform, 탈출 경로는 각각 다른 수명이다. 이를 별도 객체로 분리하면 포로 Aggregate의 영속 상태만 `CaptivityStateRuntime`에 남고 일시 호송 부모와 경로 실행은 저장되지 않는다.
- 감방을 `building.id + centerPos`로 식별하면 이동·동형 시설 배치에서 충돌한다. 포로 상태의 `housingBuildingId`도 다른 시설 참조와 동일하게 `BuildingInstanceId`만 저장해야 한다.
- Unity 응답 파일을 이용한 별도 Roslyn 컴파일은 MCP 장애 중 문법/타입 오류를 찾는 보조 수단으로 유효하지만, 새 소스를 명시적으로 추가해야 한다. 응답 파일은 마지막 Unity 빌드 당시 파일 목록만 포함하므로 최종 Unity 컴파일 증거를 대체하지 않는다.

## 2026-08-02 battle/grid ownership findings

- `OffenseBattleModel.cs`의 큰 크기는 하나의 세션 알고리즘만의 문제가 아니라 전투원 DTO, 저장 DTO, 세션, 조우 카탈로그 16개 타입이 한 파일에 있던 결과였다. 세션만 가변 전투 상태를 소유하고 나머지는 계약·순수 규칙·콘텐츠 조회로 분리할 수 있다.
- Grid의 수직 포털 목록과 최소 수직 이동 비용은 저장 상태가 아니라 현재 traversal link에서 완전히 재구축 가능한 휴리스틱 인덱스다. 별도 객체가 소유하면 Grid Aggregate는 셀/점유/경로 명령을 유지하면서 파생 캐시 저장 권위를 갖지 않는다.
- 인터페이스의 `new`는 실제 상속 멤버를 숨기는 위치에만 있어야 한다. 기반 gateway에 붙은 `new`와 파생 runtime에서 빠진 `new`가 동시에 12개 경고를 만들었고, 상속 방향에 맞춰 교정하니 동일 Roslyn 설정에서 Warning 0이 됐다.
- `OffenseExpeditionSystem.cs`는 원정 Aggregate와 689줄 UI MonoBehaviour를 함께 담고 있었다. 패널은 선택 멤버·버튼 GameObject·렌더링 수명만 소유하므로 별도 파일로 이동해도 원정 상태 권위와 공유할 가변 필드가 없다.
- 원정 런타임의 실제 결합점은 UI가 아니라 `이동 이벤트 → 전투 → 귀환 애니메이션 → 보상/캠페인 확정` 체인이었다. 이를 Target/Travel/Battle/Return/Finalizer 서비스로 나누자 Aggregate는 활성 원정 목록과 상태 전이만 유지하면서 1,200줄 제한 안으로 들어왔다.
- 생산 주문은 하나의 클래스 안에서 명령 처리뿐 아니라 출력 예약, 설비 유틸리티, 입력 선행 운반, 센서 설치 상태, 저장 매핑, UI 상태 투영까지 소유하고 있었다. 특히 출력 목적지와 센서 키가 `시설 숫자 ID + 좌표` 폴백을 공유해 시설 이동/복원에 취약했으며, 두 경로 모두 필수 `BuildingInstanceId`로 교체했다.

## 2026-08-02 equipment aggregate ownership findings

- 장비 런타임이 물리 아이템 저장소를 사용하더라도 생성자 안에서 통계 투영기·장착 저장소·부품·계보 구현을 직접 만들면 생성 경로별 규칙 권위가 다시 갈라진다. 이 객체들은 Composition Root가 동일한 싱글턴 그래프로 조립해야 한다.
- 장비 제작 큐와 캐릭터 장착 프로필은 장비 payload 자체와 수명이 다르다. 제작 Aggregate는 주문/재료 정책을, 캐릭터 장착 Aggregate는 장비 인스턴스 ID 참조만 소유하고, 내구도·품질·부품·계보는 계속 `IItemInstanceRepository` 한 곳에 남겨야 한다.
- 장비 SO의 구형 `CombatEquipmentCraftMaterial(StockCategory)` 필드는 작성 에셋에서는 이미 비어 있었지만 런타임 변환 코드가 살아 있어 추상 재료를 재도입할 수 있었다. 구형 필드가 채워진 콘텐츠는 구체 입력으로 추측 변환하지 말고 검증에서 거부해야 한다.
- 수리 주문 복원에서 재질 정의를 찾지 못했을 때 일반 재고로 대체하면 손상된 저장이나 누락 콘텐츠가 정상 수리처럼 진행된다. 구체 재질 아이템 ID를 복구하지 못한 주문은 명시적 실패로 폐기해야 한다.

## 2026-08-02 physical item aggregate findings

- 창고 집계 수량을 읽어 누락된 물리 스택을 복원하는 코드는 읽기 캐시가 아니라 두 번째 쓰기 권위다. 집계와 실물이 어긋났을 때 실물을 합성하면 오류를 은폐하고 저장 후 아이템이 증식하므로, 배송 가능 수량은 물리 저장소에서만 계산해야 한다.
- 원자 복원은 JSON 파싱만 선행해서는 부족하다. 저장 상태에 포함된 창고 목적지 키, 고유 장비와 스택의 상호 참조, 부품 소유 관계까지 스테이징 단계에서 검증해야 `repository.Clear()` 이후 예외가 발생하지 않는다.
- 물리 아이템 Aggregate의 읽기, 변경, 창고 물류, 절도는 같은 저장소를 사용하지만 서로 다른 책임이다. 불변 facet으로 노출하면 호출자는 필요한 capability만 의존하고 본체 생성자도 8개 의존성 제한을 지킬 수 있다.

## 2026-08-02 V18 ratchet and identity findings

- `AIBrain`은 하나의 불가분 알고리즘이 아니라 액션 콘텐츠 구성, 후보 평가 캐시, 재개형 스케줄링, 경로 검색 상태, 중단 정책, 명령 상태, 디버그 문구가 합쳐진 구조였다.
- 분리된 협력 객체가 각자의 캐시와 continuation을 소유한다. `AIBrain`은 캐릭터 대상 명령 경계로 남으며 점수 계산·경로 검색 continuation을 중복 저장하지 않는다.
- 후보 평가는 계속 구조화된 `AIActionFailure`를 반환한다. 새 협력 객체의 진단 문구는 전체 String Table 이전 전까지 안정적인 영문 메시지로 정규화했다.
- 방어 교전의 원거리 위치 탐색, 원거리 이동/사격, 저장 해석, 경비 AI pause 복원, 교전 승패 처리는 서로 다른 상태 수명주기다. 이들을 한 tick owner에 둘 이유가 없었으며 각각의 전용 객체로 이동했다.
- 방어 저장 복원은 저장 DTO 해석과 월드 명령 콜백을 분리한다. 저장 해석기는 경비·침입자 참조와 전선 셀을 검증한 뒤에만 교전 객체를 등록한다.

- 전역 파일 줄 수 2,169 같은 숫자 상한은 이미 비대해진 파일을 정상으로 고정한다. 1,200/800 목표와 경로별 현재 최대치를 가진 하나의 기준선 문서를 검증기와 테스트가 함께 읽어야 예외가 줄어들 때 즉시 삭제할 수 있다.
- 정적 가변 필드 총량 상한도 같은 결함이 있다. 필드 이름까지 고정한 재구축 캐시·표현 자산·프로파일러 승인 목록으로 바꾸면 새로운 런 상태가 기존 총량 아래에서 숨어 들어오지 못한다.
- 서식지 마커의 Unity 인스턴스 ID와 산업시설의 숫자 ID+좌표는 저장 왕복 시 동일성을 보장하지 않는다. 새 개체는 중앙 발급기의 타입 ID를 받고, 기존 시설 참조는 `RequirePersistentInstanceId()` 실패를 전파해야 한다.
- 씬 전환 요청은 씬보다 오래 살지만 정적 필드일 필요는 없다. `DontDestroyOnLoad` mailbox가 요청 상태를 소유하고 정적 참조는 재확보 가능한 Unity 객체 캐시로만 남기는 편이 수명과 권위를 분리한다.

- 수술의 환경 대기·복구 요청은 임상 진행률과 다른 수명이다. 필수 환경 capability를 묶음에서 강제하면 누락 시 임의 위험 계산으로 내려가는 이중 규칙을 제거할 수 있다.
- 수술 환자 입실과 재료 운반은 동일한 시설 목적지 ID를 공유하지만 임상 결과 권위는 갖지 않는다. 전용 물류 객체가 사체·환자·재료 버퍼를 함께 조정해야 부분 준비 상태가 일관된다.
- 전략 원정 화면은 화면 조정, 준비/세력 명령, 전투 카드, 동적 뷰 생성, 읽기 상세가 서로 독립적인 변경 이유를 가진다. partial 소스 경계를 이 책임 경계와 맞추면 각 Presenter 파일이 800줄 아래로 유지된다.
- 야생동물의 사냥 전투와 생태 행동은 같은 개체 목록을 보지만 권위가 다르다. 목록은 본체가 소유하고 전용 런타임은 공유 참조를 통해 전투 또는 행동만 변경하므로 별도 개체 저장을 만들지 않는다.
- 사냥 예약 키에서 캐릭터 이름으로 내리는 폴백은 동명이인과 이름 변경에 취약하다. 예약 시작도 `CharacterPersistentIdentity.Require`를 사용해야 저장·복원 식별자 계약과 일치한다.
# 2026-08-02 Authority Audit Corrections

- The former water/filth/GridTexture fallback was content synthesis disguised as presentation convenience. Shared authored colorable Tiles and ordinary SpriteRenderer views remove that hidden SO authority without duplicating hundreds of Tile assets.
- Building behavior had only eight actual runtime shells across 343 definitions. Storing `System.Type` in every BuildingSO made those assets code-serialization dependent; a fixed archetype protocol plus existing ability modules represents the same variation without reflection-driven construction.
- `WarehouseInventory` no longer owns quantities despite its legacy name: capacity/category policy is serialized, while totals are calculated through `IStockQuery` over physical item records. The remaining V1 snapshot migration was still misleading and has been removed.
- The plan previously overstated restore atomicity. `DungeonSaveSectionRegistry` preflights all payloads and rolls back on commit failure, but it still mutates the live world before rollback. Detached Aggregate staging and a single world swap remain required.
- Domain asmdefs exist for Foundation and a small set of core model contracts, but most gameplay code still compiles in `Assembly-CSharp`; the dependency-graph phase is not complete.

## 2026-08-02 treasury aggregate findings

- `economy.treasury` serialized one section but formerly restored six independent live owners in sequence. A failure in a later owner could expose a partially restored ledger, wage, procurement, overclock, or defense state.
- The six owners now project onto one replaceable `TreasuryEconomyAggregateStateStore`. Section staging creates a detached root, normalizes every subtree, and captures a commit that performs only one reference replacement.
- Individual runtime `Restore` entry points remain available for focused tests, but they copy the current aggregate and replace it after their subtree is complete; they no longer clear a live collection before validation finishes.

## 2026-08-02 composition-wide aggregate root findings

- Per-domain replaceable stores are insufficient by themselves: sequentially replacing several correct domain roots can still expose a mixed world if a later commit fails. They must project through one composition-owned root whose live reference is published once.
- A shallow candidate root is safe only when every migrated restore replaces its complete slot rather than mutating the shared old slot. Operational mutation still targets the live slot outside restore; restore paths construct detached dictionaries/DTOs and call `Replace`.
- Rebuildable presentation caches may still be invalidated during a failed restore, but authoritative persisted state remains unchanged. Such caches are not stored and recompute from the published root.
- Unity-object-heavy owners such as crop plots and world-resource nodes still combine authoritative save state with scene bindings and pending-restore fields. They require a DTO state slot plus a post-publish scene-binding projection, rather than moving their current Unity-reference dictionaries wholesale into the root.

## 2026-08-02 survival projection findings

- The `survival.deprivation` section already serialized deprivation, water, and filth together, but only deprivation previously used the detached root. Water and filth mutated live collections and Unity scene projections during commit, so a later-section failure could leave terrain, tilemaps, and cleaning targets inconsistent with the rolled-back DTO state.
- Persisted world state and Unity presentation must not share the same restore operation. Water/filth now replace detached DTO slots first; terrain, tilemaps, and work targets are reconstructed only when the runtime observes the newly published slot reference.
- Character consumables had the same clear-then-fill defect across diet and substance dictionaries. Treating its delivery and item-availability maps as state-owned transient data allows the complete runtime slot to be replaced without leaking old-run cache entries.

## 2026-08-02 captivity projection findings

- A replaceable root cannot work when helper runtimes retain a constructor-time `List<T>` reference. Captivity actor access and policy evaluation now resolve their lists through `CaptivityAggregateStateStore`, so a published root swap is visible to every collaborator.
- Door subject registries, carried Transform parents, wildlife capture flags, and actor warps are scene projections rather than persisted authority. Running them inside section commit breaks atomicity even when the DTO dictionary itself is detached.
- Restore now normalizes captive and captured-wildlife DTOs in candidate slots. Projection owners compare state references and perform external cleanup/rebinding only after publication; a discarded candidate leaves the live scene untouched.
- The architecture validator correctly rejected aggregate code added inside already bounded runtimes. Extracting state codecs, query views, projection owners, and performance sampling math restored the 1,200-line invariant without adding baseline exceptions.

## 2026-08-02 authored taxonomy findings

- 욕구·재고·시설 카테고리는 enum 자체는 저장 프로토콜이지만 표시명, 정렬, 초기값, 기분 곡선, 납품량·단가, 상점 가격 가중치는 변경 가능한 콘텐츠다. 이를 같은 정적 클래스에 두면 프로토콜과 밸런스 권위가 섞인다.
- SO 레코드를 불변 런타임 정의로 투영하고 `CharacterStats`, 생산/상점 서비스, Presenter에 카탈로그를 주입하면 런타임 전역 등록·리셋 없이 동일 데이터를 공유할 수 있다.
- 저장의 안정 ID 변환은 authored 표시 데이터 조회와 분리해야 한다. V18에서는 알려진 enum 값만 명시적 ID로 변환하고 알 수 없는 숫자·enum 이름 폴백은 손상된 저장을 은폐하지 않고 실패시킨다.

## 2026-08-02 composition-cycle findings

- `RegisterEntryPoint<T>` exposes implemented interfaces automatically. Removing an explicit `.As<IExteriorZoneQuery>()` was insufficient while `IExteriorActivityRuntime` still inherited the query contract; the contracts themselves had to be separated.
- A query that refreshes persisted state is a command in disguise. `FacilityEvolutionModifierQuery` re-entered room, filth, and building-ability services while production work was already dispatching an ability, creating a closed construction graph. Modifier evaluation now consumes the last committed component snapshot only.
- Wildlife butchery and deprivation share an event, not an ownership boundary. Publishing a typed taboo incident preserves synchronous consequences while avoiding a carcass-service → deprivation → filth → building-handler → carcass-service cycle.
- Scene-authored characters can be injected before entry points initialize, but presentation construction still requires identity. Persistent ID assignment therefore belongs before runtime/presentation bridge activation, not in save capture or a later registry pass.
- A valid `MonoScript` asset can still be the wrong script reference for a serialized component after class extraction. The scene retained `Assembly-CSharp::InvasionDirectorRuntime` data but pointed at `InvasionIntruderSystem.cs`; runtime missing-script checks exposed the stale GUID even though ordinary component-removal tooling could not repair it.
- Numeric `DataScriptableObject.id` collisions and stable string recipe-ID collisions are separate invariants. Both must fail editor validation, and the boot catalog must throw rather than keep whichever asset happened to load first.

## 2026-08-02 restore publication and session ownership findings

- Replacing an Aggregate slot is not sufficient when the same restore callback also updates a user setting, subscribes to Unity events, rebuilds markers, registers strategic sites, or warps actors. Those operations are projections and must observe publication, not candidate preparation.
- A staging-time `dirty` boolean is itself live mutation. Comparing the last projected Aggregate object or the shared root's published revision provides a stricter boundary: failed restores neither publish a new reference nor advance the revision.
- Physical hauling settings were incorrectly restored through `IDungeonUserSettingsService`, coupling a run save to persistent player preferences. They now occupy a replaceable runtime-state slot beside physical item state.
- `GameData` was already settings-only, but `GameManager` still created and owned the mutable `GameSessionState`. A scoped store now owns that state; the scene component forwards lifecycle and input only.
- The remaining hard atomicity boundary is the modular facility and character world reconstruction. It still clears and rebuilds live Unity objects during commit, so the rollback image cannot be removed until that work is prepared in a detached world representation and swapped after all sections succeed.

## 2026-08-02 detached facility-world findings

- `IGridSystemProvider` previously exposed only `GridSystemManager.grid`; there was no candidate or publication boundary. A narrow publisher now performs the checked Grid reference swap, while presentation notification is delayed until restored facilities are registered.
- Facility definition lookup, footprint collision checks, component injection, persistent-ID restore, and state-module decoding are all fallible. Performing them on inactive objects registered to an occupant-free layout copy prevents these failures from clearing the live facility world.
- Inactive candidates must also suppress external ownership. `BuildableObject` now withholds world-registry and paid-contract registration until publication, and contract removal happens synchronously when a live facility is destroyed so a delayed Unity `OnDestroy` cannot delete the replacement facility's contract by the same persistent ID.
- DTO aggregate publication alone cannot atomically publish Unity objects. Facilities and characters now participate in the same final publication boundary: facility staging exposes its detached Grid to character restore, while stable participant IDs publish quiescence, facilities, and characters in `050 -> 100 -> 200` order.
- A disabled character prefab is not automatically detached: dependency injection previously registered it in the lifetime registry and `CharacterLifecycle` subscribed to the live Grid manager even before activation. Candidate mode therefore has to begin before injection and propagate through runtime, presentation, carry, and lifecycle bridges.
- Reusing existing staff made character restore impossible to stage, because identity, abilities, health, inventory, progression, social memory, and transforms were overwritten one actor at a time. Creating complete inactive replacements makes those fallible mutations discardable and leaves old staff untouched until publication.
- The world save section must not quiesce live actors during its commit. That side effect is now a dedicated transaction participant, so a failed candidate build discards inactive objects without cancelling live work or movement.
- A detached Grid alone is not a sufficient restore view. Downstream sections also resolve facilities, warehouses, retail facilities, and characters while committing, so `RestoreWorldCandidateIndex` now redirects the ordinary world query interfaces without publishing candidates into live scene registries.
- Random streams require stable handles because most gameplay runtimes cache `IRandomStream` once in their constructors. Moving only the provider dictionary into the Aggregate root would leave those consumers attached to obsolete stream objects; provider-backed handles now resolve and advance the active root state on every call.
- A shallow candidate root is safe for `Replace`-style restore code but not for ordinary mutation: untouched slots initially point at the same object as the live root. `GetOrCreateWritable` now clones each slot on its first candidate-stage write and records candidate ownership for later writes.
- `RunVariableRuntime` formerly kept run seed, current day, replay maxima, and variable state on a scene MonoBehaviour. These values now occupy one root slot; restore builds the complete replacement before replaying the candidate random stream.
- Meta progression has two lifetimes: the external profile is intentionally merged, while run progress and the latest run result are exact save state. The profile slot now merges through copy-on-write, and the per-run tracker/lifecycle use complete replaceable slots so a rejected run cannot contaminate either lifetime.

## 2026-08-02 research restore authority audit

- The Unity Editor process and its restarted named-pipe bridge are alive, but the current Codex MCP client remains attached to the disposed pre-reload transport and returns `Transport closed`; Unity-native proof is deferred without using operating-system input.
- `BlueprintResearchSaveSection` is the next unclosed restore authority: its staged commit still clears and incrementally repopulates the live `BlueprintResearchState`, refreshes the queue, and restores knowledge-processing state. A later section failure can therefore leak research mutations unless research state is rebuilt off-world and published through the Aggregate root.
- `BlueprintResearchState` and `ResearchProjectRuntimeState` currently own readonly mutable collections, while `BlueprintResearchRuntime` owns one readonly state instance. That shape prevents root publication and forces in-place restore; the state needs an explicit deep-clone/build contract and runtime access through the active Aggregate slot.
- `KnowledgeResidueProcessingRuntime` has the same issue in a separate list plus sequence/transient fields. Its restore clears the live task list before validation, so it must either join the research Aggregate or receive its own replaceable root before the research save section is truly detached.
- Research gameplay mutations are not isolated to restore: queue ordering, progress, unlocks, and knowledge task delivery mutate the same objects during normal play. Root ownership therefore needs copy-on-write accessors for ordinary commands, not merely a `ReplaceState` used by loading.
- `RefreshProjectQueueAfterRestore` currently combines authoritative queue normalization (`TryResolveActiveProject`) with workforce/event notification. Queue normalization belongs in the detached decoded state; notification must observe a successfully published state reference so a failed restore cannot replan live workers.
- `DungeonRuntimeAggregateRootStore` deliberately shallow-copies untouched slots and requires `GetOrCreateWritable(factory, clone)` before any ordinary mutation during staging. Research facades must therefore return candidate-owned deep-cloned task/project objects before exposing mutable references such as `BlueprintResearchTask` or `ResearchProjectProgressState`.
- Existing root-backed runtimes reconcile external Unity/service projections by comparing `PublishedRestoreRevision`. Research queue/workforce notification should follow that pattern; a save-section commit must only replace root data.
- `TryResolveActiveProject` is itself authoritative normalization because it rewrites suspension reasons and the active project. It runs every second already, so the save section does not need to invoke it during commit; the first post-publication update can normalize the published queue and then emit one availability notification.
- The public research state leaks mutable task/progress objects. A root-aware `BlueprintResearchState` facade can preserve the existing API while routing all potentially mutable access (`Projects`, active task resolution, command methods) through deep copy-on-write Aggregate data.
- Editor scenarios frequently construct `BlueprintResearchRuntime` manually, and some deliberately use an uninjected component only as a standalone state container. The migration must preserve a local-state constructor path for tests while production `Construct` replaces the facade with one backed by the scoped Aggregate root.
- `KnowledgeResidueProcessingRuntime` is created only through composition in production, so adding the shared root store keeps its constructor at eight dependencies and can make its task/sequence state transactional without a second service locator.
- The first Foundation/runtime/Editor auxiliary compilation after the research and knowledge Aggregate cutover passes with Error 0 / Warning 0, including all known manual `BlueprintResearchRuntime.Construct` fixtures.
- Existing research scenarios verify direct V5 round-trip and V3 rejection but do not inject a later-section failure through `DungeonSaveSectionRegistry`; a new scenario must prove the published live research root remains unchanged when the candidate is discarded.
- Research now has a focused candidate-discard scenario: it stages different progress/queue data through the real V5 section, observes the candidate, discards the root, and requires the original live state plus publication revision to remain unchanged.
- The registry still captures and reapplies a rollback image after any commit failure. Aggregate-backed research no longer needs that repair path, but global rollback removal remains blocked on the remaining non-root Unity/runtime owners and participant publication failure semantics.
- The public-registry failure scenario compiles after replacing internal root calls with a one-shot late failure plus a discard observer. It proves the observer sees the original 7-work live state immediately after candidate discard, before rollback publication.
- Focused source review finds zero remaining research-save calls to live `ClearForRestore`, restore-time queue refresh, or legacy blueprint item materialization, and zero knowledge-task list fields outside the Aggregate state.

## 2026-08-02 remaining restore-owner audit

- A fresh scan of save-section commit paths identifies `CodexSaveSection` as the clearest next live-mutation owner: it calls `runtime.State.ClearForRestore()` and then recreates entries/lines one at a time during commit.
- Other candidates requiring follow-up include regular customers, facility shop, species, defense, exterior/waste/resource policies, and infrastructure runtimes. Their `Restore` methods must be inspected individually because several already replace root-backed state despite the generic method name.
- Codex has the same mutable-object leak pattern research had: a readonly `CodexState` owns a dictionary of mutable `CodexEntryRecord` objects, and `GetOrCreate` returns those records directly. A root-aware facade must deep-clone both the dictionary and every entry/line set before returning writable records during staging.
- `HasMemoryResidueClueAvailable` currently calls `GetOrCreate`, so a nominal query creates a discovered blank codex entry. The Aggregate migration should make this a pure snapshot lookup while preserving clue availability behavior.
- `CodexSaveSection` now preflights missing IDs, invalid enum values, and duplicate category/ID keys, builds a standalone state, then replaces the root slot. The only manual runtime construction site has been updated; no `ClearForRestore` reference remains in Codex or the full-save scenario.
- `RegularCustomerSaveSection` already constructs detached `RegularCustomerRecord` objects, but its final `runtime.State.Restore(records)` still targets a non-root state owner; there are no Aggregate-root references anywhere in the recruitment domain.
- Regular-customer state contained a second `recruitedCharacters` list that could diverge from each record's `IsRecruited` flag. The list is now eliminated; the public result view is deterministically derived from the authoritative record dictionary.
- Mutable records retain an `ActiveActor` runtime link that is not save data but must survive copy-on-write during normal gameplay. Deep cloning preserves that link while save decoding still constructs records with no actor and lets world activation reconnect them later.
- The production recruitment runtime remains at the eight-dependency boundary by combining activation and population into a typed character-lifecycle capability bundle before adding Aggregate-root ownership.
- Facility-shop saving currently duplicates research authority by capturing `research.State.UnlockedBuildingIds` into `DungeonFacilityShopSaveData` and restoring those IDs back into research after the research section. That field and dependency should be removed; research unlocks belong only to the research Aggregate.
- `DailyFacilityShopRuntime.RestoreState` clears unlock sets and calls `Refresh`, which emits `Refreshed` and runs auto-procurement during save commit. Offer lists are deterministic projections of day/catalog/run variables, so restore should replace day/unlock data and rebuild offers only after publication, without purchasing or alerts.
- Facility-shop offer day, basic-purchase unlocks, and acquired-blueprint IDs are one runtime aggregate. Giving the runtime a second direct root accessor would split the uninjected editor path from the unlock façade, so both date and unlock access now route through the same local/root-aware façade.
- Research-unlocked building IDs were duplicated in `DungeonFacilityShopSaveData` and written back after the research section. Removing that field, the research dependency, and the research runtime reference leaves `BlueprintResearchAggregateState` as the sole research-unlock authority.
- Daily offers are deterministic projections, not save authority. Candidate commit now replaces only the facility-shop aggregate; post-publication observation rebuilds offers without auto-procurement or alerts, while ordinary day refresh still performs those gameplay side effects.
- The facility-shop payload now validates offer day plus every saved building/blueprint ID against the authored catalog before commit. Duplicate, negative, or missing IDs fail preflight instead of being filtered into plausible state.
- A fresh post-facility scan found the four industrial infrastructure runtimes are the next genuine live owners: electrical and fluid clear node dictionaries, conveyor clears node/payload dictionaries, and automation clears both facility state and its power-demand registry during staged commit.
- Service-room code appeared suspicious by method name but already uses a `ServiceSessionAggregateState`; generic `Restore(...)` searches must be verified against ownership rather than treated as proof of live mutation.
- Industrial network summaries, topology versions, route caches, snapshot lists, and automation demand are projections. Persisted node/payload/facility values should swap through root slots, while topology/snapshot/demand rebuilding must observe the published revision rather than execute during candidate commit.
- `AutomationPowerDemandRegistry` did not need its own saved dictionary. Reading `AutomationAggregateState.Facilities[facilityId].Mode` directly makes power demand consistent with the candidate/live root and removes publication-order sensitivity between the automation and electrical tickables.
- All four industrial runtimes can preserve their existing interfaces while routing dictionary access through `GetOrCreateWritable`. During restore staging the first access deep-clones the shallow candidate slot; restore then replaces the complete slot and leaves topology, route, snapshot, warning timer, and payload-count caches untouched until publication.
- Industrial payloads previously normalized invalid values and overwrote duplicate IDs during restore. The new preflight rejects payload-version mismatch, blank/duplicate IDs, invalid enums, non-finite values, out-of-range percentages/freshness, and malformed conveyor payload stacks before any Aggregate replacement.
- `WorkOrderRuntime` is a remaining Unity-object-bound authority, not a simple dictionary conversion: restore destroys live `ConstructionSite` GameObjects, clears order/site maps, rebuilds orders, and creates construction sites while decoding. It needs a detached order DTO slot plus a transaction participant that prepares site objects against the candidate Grid and publishes them only after all sections commit.

## 2026-08-02 event-alert authority audit

- `EventAlertRuntime` still owned saved history, dismissal IDs, and its next numeric ID directly in MonoBehaviour collections. `RestoreHistory` destroyed/recreated Unity UI while the save registry's candidate root was active, so a later-section failure could visibly mutate the live world before publication.
- `EventAlertAggregateState` now owns those three persistent concerns and deep-clones mutable records, including runtime-only choice callbacks during copy-on-write. Selection, buttons, and detail visibility remain transient presentation state.
- The event-alert save section and direct save service now share `EventAlertSaveValidation`; invalid/null/duplicate IDs, unknown importance values, invalid counts/text, and more than three choices are rejected instead of filtered or normalized away.
- A public `DungeonSaveSectionRegistry` regression stages a dismissed candidate alert, injects one late failure, and observes the target immediately after candidate discard. It requires only the original live alert and zero presenter create/destroy calls before rollback publication.
- The source contract validator now requires the Aggregate slot, published-root revision observation, detached replacement, and the generic preflighted JSON save boundary.

## 2026-08-02 operating-day settlement authority audit

- The settlement persistence DTO was already an immutable snapshot, but the runtime restored it by `ResetLedger`, repopulating seven live collections, clearing report history, and rewriting debt/scalar fields during staged commit.
- `OperatingDaySettlementAggregateState` now owns all ledger dictionaries/lists, current counters, debt state, and report history. `LatestReport` is derived from history instead of being a second writable reference.
- `OperatingDaySettlementSaveValidation` rejects invalid root and nested report data before conversion: missing lists, duplicate keys/categories/days, negative amounts, non-finite mood values, invalid enums, malformed warehouse/supply/offer records, and history beyond 20 reports.
- Direct save-service restoration invokes the same validation as `OperatingDaySettlementSaveSection`; the section now uses the common `DungeonJsonSaveSection<T>` staged/preflight boundary.
- The public-registry regression replaces a candidate day/revenue/visit ledger, injects a one-shot later failure, and requires the observer to see the original live Aggregate immediately after discard.
- The settlement's existing eleven service dependencies remain a separate Phase 89/90 decomposition item. This change adds the Aggregate root explicitly and does not introduce another state owner or late-bind path.
## Work-order / construction-site restore authority audit (Phase 88 continuation)

- `WorkOrderRuntime.Restore` currently destroys every live `ConstructionSite`, clears both order dictionaries, and bumps the live candidate version before it has validated the complete snapshot.
- The same restore loop converts each DTO into mutable runtime state and immediately creates a Unity `GameObject`/`ConstructionSite`; a later duplicate, missing building definition, invalid grid position, or site creation failure therefore leaves a partially replaced live world.
- `ordersById` plus `nextOrderSequence` are the persisted work-order authority. `orderIdBySite` is a Unity-object projection and must be rebuilt only from a successfully published restore candidate.
- `WorkOrdersSaveSection.StageRestore` currently only deserializes JSON and defers all semantic validation and world mutation to `Commit`; it needs a shared strict validator and detached aggregate candidate before any live publication.
- The construction-site projection needs transaction-participant semantics: prepare against the restore candidate grid, publish only after aggregate publication, and destroy candidate objects on discard without touching the live sites.

## 2026-08-02 work-order detached Aggregate result

- `WorkOrderAggregateState` is now the sole owner of order records, next-ID sequence, and candidate version. `orderIdBySite` remains only a rebuildable Unity-object projection.
- `WorkOrderSaveValidation` rejects null/version-mismatched payloads, invalid or duplicate canonical IDs, sequence reuse, unknown work/building/item definitions, non-finite progress, terminal statuses, malformed material lists, abstract `stock-item:*` item inputs, duplicate construction targets, and mismatched construction destinations.
- `WorkOrderRuntime.Restore` no longer clears live orders or sites. It builds a complete replacement Aggregate and inactive `ConstructionSite` candidates on the shared facility candidate Grid; any footprint/build/injection/registration failure discards those candidates.
- The runtime is restore participant `150.world.construction-sites`, so publication follows facilities (`100`) and precedes characters (`200`). Failure discard leaves live site mappings untouched; successful publication removes old sites and makes the prepared sites visible.
- Work-order persistence now uses `DungeonJsonSaveSection<DungeonWorkOrderSaveData>` and explicitly depends on both the facility-world and physical-item sections.
- The work-order runtime source was kept within the 1,200-line boundary by separating save contracts, strict validation, and Aggregate state into focused source owners; `WorkAmountSystem.cs` is 1,185 lines after the cutover.
- Auxiliary Foundation, runtime, and Editor Roslyn builds pass. Unity MCP still returns `Transport closed`, so Unity-native menu execution and Console/capture proof remain pending.

## 2026-08-03 work-order rollback-free verification result

- `WorkOrdersSaveSection` now declares rollback-free staging, and capture persists active work as canonical resumable `Ready` state with no worker reservation. Validation requires canonical ascending `work:{sequence:D6}` IDs, exact work/material/destination state, no terminal or transient status, and authored references.
- construction sites are fully prepared inactive against the candidate Grid before live retirement. Successful publication uses synchronous world-replacement retirement; failed/partial candidates use synchronous discard and cannot leave detached GameObjects.
- Unity actual execution passed normal publication, invalid preflight preservation, one-commit late failure, root/candidate/live-site preservation, and the full WorkAmount suite. The later physical-item cutover did not regress this suite.

## 2026-08-02 wildlife restore authority audit

- `WildlifeRuntime.Restore` currently calls `ClearWildlife` before validating the full payload, destroys every live `WildlifeActor`, creates replacement `GameObject`s one at a time, and silently skips unknown species or blocked positions. A later failure therefore leaves a partial population.
- Wildlife persistence is split across four mutable owners: Actor MonoBehaviours, `WildlifeEcosystemRuntime` fields, `WildlifeCarcassService.freshnessByStackId`, and `WildlifeRuntime.foodRaidOrders`/sequence fields. All four are mutated during one save-section commit.
- `WildlifeSaveSection` still accepts and migrates V2 to V3 even though the V18 root explicitly rejects pre-V18 runs. The wildlife section should require its current version and fail invalid data rather than fabricate empty raid state.
- `WildlifeActor.Initialize` registers immediately with both the Grid and live world registry. Candidate construction needs an explicit detached flag: register against the candidate Grid, suppress live registry publication, then publish or discard in one transaction participant.
- Wildlife publication should sort after facilities, construction sites, and characters (`100 -> 150 -> 200 -> 250`) so restored animals see the final Grid/world before AI resumes.
- Food-raid entries that reference missing animals, duplicate wildlife IDs, unknown species, non-finite body/need values, malformed habitat patches/respawn records, or invalid carcass records must be rejected during shared preflight instead of normalized or skipped during commit.

## 2026-08-02 wildlife detached restore result

- `WildlifePopulationState` now owns the live actor list, behavior schedule, raid orders, sequence, initial-spawn flag, and carcass tick schedule. `WildlifeRuntime` accesses these through one replaceable population reference and rebuilds its hunt/behavior collaborators after publication.
- `WildlifeSaveValidation` rejects invalid current-version payloads, canonical-ID/sequence errors, unknown species, invalid health/need/enums, malformed carcass and ecosystem records, duplicate active raid ownership, and invalid typed habitat IDs. Terminal raid history may outlive its actor; nonterminal raid state may not.
- Restore participant `250.world.wildlife` builds every Actor as an inactive detached candidate on `RestoreWorldCandidateIndex`'s facility Grid. Candidate actors register only on that Grid; live world-registry publication is suppressed until the final participant boundary.
- Candidate commit does not clear or mutate the live population, ecosystem, carcass freshness, or raid-order state. Failure discards only candidate actors; publication replaces those projections after facilities (`100`), sites (`150`), and characters (`200`) are already published.
- `WildlifeSaveSection` now uses `DungeonJsonSaveSection<DungeonWildlifeSaveData>` and accepts only its current V3 section contract. The obsolete V2-to-V3 empty-raid migration was removed because the V18 root rejects old runs.
- World-reference validation runs after physical-item and facility candidates are staged. Carcass freshness must reference a matching physical carcass stack, while every saved habitat patch must cover a usable restored exterior cell.
- Normal habitat creation now emits typed `wildlife-habitat:*` IDs for authored, default, and water-source patches. The constructor no longer fabricates a GUID fallback, so a missing/legacy ID is an explicit contract failure.
- Carcass capture filters freshness records through the physical item authority, preventing deleted or mismatched stacks from becoming unloadable V18 saves.
- The main and restore partials total 1,198 lines, preserving the 1,200-line wildlife runtime boundary. Runtime and Editor auxiliary Roslyn compilation pass with Error 0 / Warning 0.
- PlayMode regressions now cover invalid preflight preserving actor identity, successful round-trip publishing replacement actors, and a one-shot later failure that discards the first candidate before rollback. Execution remains pending because Unity MCP still reports `Transport closed`.

## 2026-08-02 exterior-zone and return-arrival authority result

- `ExteriorZoneMarker` was previously captured by both the modular facility snapshot and `exterior.activities`, because it inherits `Facility`. Facility capture/clear now explicitly excludes it, leaving the exterior activity section as its only save owner.
- `ExteriorActivitySaveSection` now requires strict V3 preflight and delegates restore to participant `300.world.exterior-zones`. The coordinator creates inactive markers on the shared candidate Grid, restores typed building identity, indexes them for downstream queries, and does not replace live zone objects until publication.
- Exterior incident persistence no longer stores both summary incidents and detailed runtime incidents. Only detailed `incidentStates` remain; terminal history can outlive world references, while active incidents require restored characters, wildlife, and physical stacks.
- `OffenseReturnArrivalRuntime.Restore` previously cleared live queues and called `MaterializeReadyArrivals`, which could spawn prisoner or wildlife GameObjects during staged commit. Return queues, barriers, sequence, and retry time now live in `OffenseReturnArrivalAggregateState`.
- Return-arrival payloads now reject version/list errors, duplicate or noncanonical IDs, invalid enums/counts/risk, inconsistent escaped/materialized sets, and sequence reuse before commit. Restore swaps one detached Aggregate slot and schedules physical materialization for a later normal tick.
- Return-arrival dependencies are grouped into two explicit capability bundles, reducing the runtime constructor from fifteen direct dependencies to two.

## 2026-08-03 character-medical detached restore result

- The previous medical restore cleared live orders, treatment reservations, carry-parent state, and downed Grid occupants before validating all saved references. Invalid patients, rescuers, facilities, or occupied cells could therefore leave a partially replaced runtime.
- `CharacterMedicalAggregateState` is now the sole owner of medical orders and their canonical sequence. Capture and ordinary mutation use the active Aggregate root; restore creates a deep-cloned replacement state.
- `CharacterMedicalSaveValidation` rejects null/oversized lists, malformed or duplicate `medical:N` IDs, sequence reuse, invalid typed character/building IDs, invalid enums, non-finite work/treatment values, impossible carry/supply flags, duplicate active patients, and unknown authored treatment items.
- `CharacterMedicalRestoreCoordinator` validates candidate-world patients, rescuers, and treatment facilities, then registers downed occupants only on the detached facility Grid. Participant `350.world.medical` swaps the projection after facilities, sites, characters, wildlife, and exterior zones publish.
- Failed preparation removes only candidate Grid registrations. Successful publication removes old registrations using their recorded original Grid and position, preventing a world-swap lookup from detaching the wrong Grid.
- `CharacterMedicalSaveSection` now uses `DungeonJsonSaveSection<DungeonCharacterMedicalSaveData>` and the old warning-based restore call is gone. The composition root registers the medical runtime as a restore participant.
- Restore orchestration is a focused coordinator rather than a partial-class size escape; `CharacterMedicalRuntime.cs` is 1,199 lines and retains exactly eight required constructor dependencies.

## 2026-08-03 character combat-command detached restore result

- The old persistence helper released every live combat stance, unpaused actors, cleared commands, and then silently skipped missing actors or malformed reservations. A failure after that point could not preserve the live command projection.
- The old DTO omitted both `commandSequence` and `commandRevisions`, so loading an active run could reuse `combat-command:N` IDs and lower an actor's revision watermark.
- `CharacterCombatCommandAggregateState` now owns commands, stance membership, actor revision watermarks, and sequence. The V2 DTO captures all four and strict validation rejects malformed IDs, duplicates, terminal commands, invalid enums/timers, missing target contracts, revision mismatch, and sequence reuse.
- Candidate-world validation requires active restored stance actors, valid target cells, restored character/wildlife targets, and existing physical weapon instances. Restore only replaces the detached Aggregate root.
- Participant `400.world.combat-command-stances` applies AI pause and existing stance presentation after the detached character, wildlife, exterior, and medical participants have published. Failure discard never touches the live actor projection.
- `CharacterAiWorldRegistry.Wildlife` and its version now follow `RestoreWorldCandidateIndex`, closing a downstream query hole that otherwise exposed the retired live wildlife population during staged combat validation.
- `CharacterCombatCommandRuntime` remains exactly 1,200 lines while its constructor falls from fourteen direct dependencies to four parameters: combat capabilities, world capabilities, focused collaborators, and the Aggregate root.

## 2026-08-03 defense-tactical detached Aggregate result

- The former restore cleared `byActor`, fabricated missing reservation IDs, and silently dropped missing actors, duplicate cells, and invalid Grid positions. Its `sequence` was not saved, so IDs could be reused after load.
- `DefenseTacticalAggregateState` now owns reservations and sequence. Strict V2 validation rejects malformed/duplicate canonical IDs, actor/cell duplicates, invalid enums or scores, sequence reuse, unavailable candidate actors/targets, and blocked candidate cells.
- Restore performs no live clear and no fallback generation; it replaces one complete Aggregate slot only after structural and detached-world validation succeeds.
- The public PlayMode save path now includes an invalid-sequence regression that requires both the reservation view reference and serialized live state to remain unchanged.

## 2026-08-03 medical lifecycle and physical-supply findings

- `TryRequestTreatment` previously routed any injured actor through `NotifyCharacterDowned`, even when the body Aggregate reported the actor ambulatory. This created the observed `Completed -> Downed -> new medical:N` loop. Emergency rescue orders now require `CharacterBodyHealthSnapshot.Downed`.
- `CharacterMedicalRuntime.AdvanceTreatment` duplicated body-health recovery by calling `NotifyCharacterRecovered` after `ApplyTreatment` had already published the authoritative event. Removing that second writer and guarding both notification handlers against the body snapshot restores one lifecycle authority.
- The old verifier seeded only a `WarehouseInventory` category query fixture. It increased `Medicine` counts but created no `ItemInstanceId` or physical stack, so exact authored medicine delivery could never finish under the V18 item authority.
- Exact medicine already present in an order's facility buffer is now consumed before a haul request is created. This is valid for restored/prepositioned supplies and enabled the verifier to use real authored medicine SOs and physical stacks.
- Apparent rescue-command and transform failures were caused by autonomous owner AI reserving the same order and by facilities adjacent to the patient. Pausing all other rescuers and enforcing a minimum facility distance produced deterministic pointer-owned transport evidence.
- Live actor identity audit found no persistent-ID collision: `owner` and both `staff:*` identities were unique. The differing rescuer was a legitimate autonomous owner, not duplicate state authority.
- `CharacterCombatCommandRuntime` now completes rescue commands from `CharacterRecoveredEvent`; this is event-driven and does not depend on positive game-clock delta after the verifier pauses time.
- Runtime size validation is enforced after behavior changes. Medical supply and combat-command lifecycle responsibilities are separate partial sources, leaving every affected runtime file below 1,200 lines.

## 2026-08-03 rollback-boundary resumption audit

- `DungeonSaveSectionRegistry.RestoreAll` still captures `CaptureAll()` as a full rollback image before committing staged sections. A failed commit discards candidate roots/participants, then preflights, stages, commits, and publishes the captured live image again.
- Removing that image now would be unsafe: the Registry still acknowledges legacy sections whose runtime state is not a replaceable Aggregate or detached Unity-world candidate. The next work must identify and convert those owners, not delete the guard prematurely.
- The worktree is extensively dirty from the continuing V18 program. A normal `git diff --stat` invoked Git LFS clean filtering and failed because `.git/lfs/tmp` is read-only in the managed workspace; `git -c filter.lfs.process= -c filter.lfs.clean= -c filter.lfs.required=false diff --stat` is the safe read-only audit form here.
- The previous broad baseline output is too large for a single tool response. Subsequent audits must be domain-scoped and line-bounded so evidence is not lost to truncation.

## 2026-08-03 captivity/circus restore audit — initial evidence

- Captivity is only partially Aggregate-backed. `CaptivitySaveSection.StageRestore` still captures a DTO and calls `runtime.Restore` during commit; `CaptivityStateRuntime.Restore` clears/restores policy and captive collections while treating invalid or missing references as warnings.
- `CaptivityPolicyRuntime.Restore` clears the live policy list and skips invalid or duplicate policies. That is a live mutation plus permissive normalization, not detached candidate validation.
- Circus similarly builds a `CircusAggregateState`, but its save section invokes live `CircusRuntime.Restore`, and captured-wildlife restoration remains a separate mutable path with warning-based skipping and transient carry-parent clearing.
- These two sections are strong next candidates because their plain Aggregate types already exist, while their save boundaries still lack strict preflight, detached slot replacement, and candidate-world reference validation.

- The captivity save section has no semantic preflight: `JsonUtility.FromJson(...) ?? new CaptivitySaveData()` silently fabricates an empty payload and defers every rule to commit. `Restore` also duplicates this permissive path outside the staged boundary.
- `CaptivityStateRuntime.Restore` does replace `CaptivityAggregateState` before adding records, so it may already target the registry's candidate root; however it clamps negative sequences, skips duplicate/malformed captives, marks missing actors dead, and rewrites in-progress escort state instead of rejecting an invalid V18 snapshot. Strict validation is still required even if the root swap itself is detached.
- `CaptivityAggregateStateStore.Replace` delegates to `DungeonRuntimeAggregateRootStore`, confirming captivity DTO state is staged in the candidate Aggregate root rather than necessarily mutating the published root.
- The external door-access/carry projection is updated lazily by `CaptivityDoorAccessProjection.EnsureCurrent`; it removes previous door subjects, clears escort transient parents, then applies the published state. It is not itself a transaction participant, so its call sites and publication timing must be audited before declaring captivity rollback-independent.
- Captivity actor validation resolves through injected `ICharacterAiWorldRegistry.AllCharacters`, not through a cached actor list. Because the character world registry was previously made candidate-aware, this is the correct abstraction if its current implementation delegates to `RestoreWorldCandidateIndex`; that delegation must be verified directly.
- During staged restore, `doorAccessProjection.RestoreCompleted()` deliberately does nothing and normal tick later notices the published Aggregate reference. This avoids live door mutation before publication, but it leaves restoration consistency dependent on the next tick and does not provide an explicit participant ordering boundary.
- `CharacterAiWorldRegistry.AllCharacters` is candidate-aware: while restore candidates exist it returns `IRestoreWorldCandidateQuery.TryGetCharacters`, otherwise the live lifetime registry. Captivity can therefore validate character references against the detached character population without a new world lookup abstraction.
- Audit command note: two filename-pipeline searches for medical save sources returned exit code 1 because the relevant types are grouped in `CombatSaveSections.cs` and `CharacterMedicalRestoreRuntime.cs`, not files matching the assumed filename pattern. Symbol search (`rg -l "class CharacterMedical..."`) is the reliable lookup and located all three sources.
- The established medical pattern separates three responsibilities: strict DTO validation plus `CreateState`, runtime candidate-world validation/projection preparation, and an ordered `IDungeonRestoreTransactionParticipant` that publishes only after Aggregate-root publication.
- Captivity does not need a new inactive actor projection because characters already belong to the character-world participant. It does need the first two layers and a small participant for door/carry projection publication so success does not depend on a later tick.
- `CharacterMedicalSaveSection` demonstrates the intended concise boundary: inherit `DungeonJsonSaveSection<T>`, validate in `ValidatePayload`, and call a runtime restore that requires an active transaction. Captivity's hand-written `Restore`/`StageRestore` should be replaced with this form.
- Captivity V2 persists canonical sequences, policies, and a broad captive state including character/building/stack/item references, interaction progress, performer progression, and timed security state. Validation must cover all of these rather than only duplicate IDs.
- `CaptivityPolicyRuntime.Restore` currently masks corrupted V18 data by clamping the policy sequence, dropping malformed/duplicate policies, and fabricating built-in policies if the result is empty. It also may call `ApplyLabor`, which changes candidate character type/lifecycle while decoding state. The replacement builder should clone validated policies/captives directly and defer actor projection to the participant.
- Captive numeric invariants are explicit in the model: bounded 0–100 traits/health/pressure, nonnegative performer/injury/privilege/security counters, finite timed fields, defined status/milestone enums, and labor flags limited to `CaptiveLaborPermission.All`.
- `captureSequence` is only a monotonic event counter and is not encoded into `captiveId`; it therefore needs nonnegative validation but cannot be compared to captive IDs. `policySequence` can and should be compared to `captivity:custom:N` IDs.
- Active escort and interaction state has cross-reference contracts: carrier/warden must be candidate characters, housing must be a candidate captivity-capable building, restraint stack/item/quantity must agree with physical item state, and interaction IDs/destinations/work fields must form a coherent all-or-none set. Current restore rewrites escort state and ignores these contracts instead of validating them.
- `ICaptivityRuntime` still exposes the old warning-based `Restore` only; it needs `ValidateRestore(payload, report)` and report-based transaction restore like the medical runtime.
- `CaptivityRuntime` is registered as an entry point and multiple captivity interfaces, but not yet as `IDungeonRestoreTransactionParticipant`. Adding that service exposure is required for explicit post-root door/carry projection publication.
- Audit command note: PowerShell/Windows does not accept a wildcard embedded in the `rg` path argument (`CharacterMedicalRuntime*.cs` produced OS error 123). Use a directory path plus `-g 'CharacterMedicalRuntime*.cs'`.
- Medical runtime exposes the participant lifecycle as thin delegates to its restore coordinator. Captivity can use the same shape without growing its already size-constrained main runtime.
- `IWorldItemStackRuntime.GetAllStacks()` is available through the injected physical-item authority and can validate saved restraint stack/item/quantity against the detached physical-item candidate. `CaptivityInteractionRegistry.TryGet` can validate current interaction IDs against the authored runtime handler set.
- `WorldItemStackSnapshot` exposes the needed authority fields (`StackId`, `ItemId`, `Quantity`, `ReservedByPersistentId`, `DestinationId`). No captivity-specific editor test currently calls the warning-based restore API directly, so changing the public interface should have limited fixture fallout.
- Audit command note: a parallel search initially returned exit 1 because one assumed filename/type match was absent; broad parallel `rg` calls should be wrapped or issued independently when a no-match exit is acceptable.
- Typed `ItemDefinitionId` and `ItemStackId` value types already exist, and captivity housing capability is explicitly discoverable through `BuildingSO.GetCaptiveHousingAbility()`. Strict validation can avoid raw-string format guesses for these references.
- `CharacterId` currently validates any nonempty canonical string, while `BuildingInstanceId` requires `building:*` and `ItemStackId` requires `stack:*`. Captivity validation should use these actual contracts rather than impose a new character prefix.
- Valid captivity housing must have `BuildingCaptiveHousingAbility.IsValid` (`capacity > 0` and humanoid acceptance); a mere surviving building ID is insufficient for active confined/interaction states.
- `IDoorAccessSubjectRegistry` only exposes per-ID `SetCaptive`/`SetCapturedWildlife`; it has no replace-all/pointer-swap contract. A captivity participant cannot honestly promise a single non-failing door projection swap until this registry gains an Aggregate-backed subject set or replace operation.
- `DoorAccessService` owns mutable `HashSet<string>` memberships and every per-ID change calls `NotifyDoorPolicyChanged`, increments a version, clears path caches, and requests AI replans. Replaying a full captive list at publication is both non-atomic and unnecessarily noisy.
- The concrete `DoorAccessService` is already a singleton registered behind query/command/subject interfaces. A narrow `ReplaceCaptiveSubjects(IEnumerable<string>)` command on `IDoorAccessSubjectRegistry` can build a detached set first, swap membership in one method, and emit one policy-change notification.
- Door path-search caching already keys on `IDoorAccessQuery.DoorAccessVersion`. If door subject membership becomes an Aggregate-root slot, adding `DungeonRuntimeAggregateRootStore.PublishedRestoreRevision` to that version invalidates all cached routes immediately after one root publication without replaying per-captive notifications.
- Captivity restraint reservations can legitimately outlive their source world stack ID after the carrier picks the restraint into inventory; current code does not clear the saved stack fields on pickup/consume. Strict validation may require typed/coherent fields but cannot require the original world stack to still exist without first unifying carry inventory with physical items.
- In-flight escort parent transforms are transient. The existing restore intentionally resets `Escorting` to a safe non-carry state; a strict builder may retain that explicit canonicalization, but it must validate the source state first and clear all related transient reservation fields consistently.
- `CaptivityEscortRuntime.ClearTransientState()` is a non-failing dictionary clear; it is suitable for the participant's publication step. Door membership itself can now be staged in the new Aggregate-root door subject state, so the old lazy `CaptivityDoorAccessProjection` replay should be removed.
- Performer skill/fame are clamped to 0–100 and privilege tier is derived as 0–2. Captivity statuses used by runtime cover every enum value except `None`; strict save validation can reject `None` and out-of-range performer/milestone fields without rejecting authored flows.
- Fresh-source audit confirms the old `CaptivityDoorAccessProjection` and warning-based captivity restore signatures are gone. Remaining warning restores reported by the scoped scan belong to circus, invasion, and another combat section, which remain later conversion targets.
- The current runtime response file writes directly to Unity's Bee `Assembly-CSharp.dll`; newly added sources are not yet listed there, so the auxiliary runtime compile must append the four new `.cs` paths explicitly. Editor compilation must then reference the rebuilt runtime DLL without appending those partial/runtime sources again.
- Auxiliary runtime and Editor Roslyn compilation both pass after the captivity/door Aggregate edits. No existing captivity-specific invalid-preflight regression was found, so focused coverage must be added rather than inferred from other domains.
- The V18 validator still expects the removed `CaptivityDoorAccessProjection.EnsureCurrent` source contract. It must be ratcheted to require `DoorAccessSubjectAggregateState`, `ReplaceCaptiveSubjects`, strict `CaptivitySaveValidation`, the typed JSON section, and participant ID `450.world.captivity` instead.
- `CaptivityCircusDebugScenarios` is a 328-line pure contract suite and is the appropriate place for deterministic DTO validation/clone checks. A full live-world preflight-preservation check should be added to the existing PlayMode save verifier path separately.
- Editor code compiles in a separate assembly and cannot exercise internal runtime validators. The stable boundary is a public payload validator plus a public pure `CaptiveState -> CaptiveState` restore normalization function; the internal Aggregate builder remains hidden.
- `CombatV14PlayModeVerifier` already runs invalid medical, combat-command, defense-tactical, and equipment-maintenance payloads through the public `IDungeonGameSaveService` before tactical gameplay. Captivity should join this exact sequence and assert unchanged serialized captivity state, Aggregate published revision, and door-access version.
- The verifier can be started entirely through Unity MCP via `StartFromMenu()` and polled with `GetReport()`; it writes the authoritative report to `Artifacts/QA/combat-v14-playmode-report.txt` and uses Unity's virtual Input System/capture paths.
- The first live run exposed a real validator defect: authored built-in policies `captivity:forced-labor`, `captivity:performer`, and `captivity:corruption` are valid but were rejected because the new validator allowed only `captivity:standard` and `captivity:custom:N`. Every unrelated invalid-payload report therefore also contained captivity errors, meaning a normal save would have been unloadable. Built-in policy IDs must be explicitly recognized.
- `CAPTIVITY_PREFLIGHT_ATOMIC` itself passed with unchanged revision/door version/state. The overall run failed later at `POINTER_RELOAD` in a reused PlayMode/InputSystem session; this is separate from save preflight and will be rerun after a clean PlayMode restart.
- The clean rerun proves the built-in policy fix: unrelated medical/command/defense/maintenance preflight errors no longer contain captivity errors, and `CAPTIVITY_PREFLIGHT_ATOMIC=PASS` reports only the injected negative sequence.
- The clean run reached medical QA but failed rescue initiation because the verifier explicitly paused the rescuer, then used the combat-stance button before rescue without ensuring that stance activation left the actor eligible for `AbilityRescue`. This is verifier isolation/setup behavior, not a captivity restore mutation: Aggregate revision and door version stayed unchanged and all preflight checks passed.
- `TryIssueRescue` itself accepts a paused rescuer in combat stance; once a command exists, `TickRescue` briefly unpauses to start the ability and pauses again. The failure occurs before command creation, so the retry must target the UI selection/mode/right-click sequence rather than changing rescue runtime rules.
- Audit command note: a mixed `rg` command again used wildcard path arguments and returned Windows error 123 after useful partial output. Subsequent searches use directory roots with `-g` only.
- `OwnerCommandController` clears rescue input mode after one pointer attempt. The verifier did not confirm that its second single-selection click actually selected only the rescuer before activating stance/mode, and it made only one target attempt. A bounded three-attempt selection→stance→rescue-mode→right-click loop is the correct deterministic QA fix.
- `OwnerCommandController` exposes a public `SelectedActors` read-only view that prunes stale selections. The verifier can therefore assert a canonical one-actor selection before arming rescue mode instead of inferring selection from button state.
- The verifier source lives at `Assets/Scripts/Services/Combat/Editor/CombatV14PlayModeVerifier.cs`, not under `Assets/Tests/PlayMode`; future reads should locate it by symbol or exact tracked path.
- The bounded retry removed selection ambiguity but did not start rescue: the clean MCP run reports `attempts=3; selected=Sion; mode=None; stance=True`. Since rescue mode is one-shot and resets to `None`, the pointer command handler is receiving each click but declining/resolving the target before `TryIssueRescue` creates a command. Target raycast and patient commandability are now the narrowed fault boundary.
- `OwnerCommandController.TryIssuePriorityWorkCommand` reads a single `Physics2D.Raycast` hit at `IPlayerInputReader.MousePosition` and enters combat dispatch whenever the selected rescuer is in stance. Rescue mode resets only after a non-null downed `CharacterActor` reaches the rescue issue branch; the observed `mode=None` therefore strongly indicates the hit resolves to some collider, but the exact failure message/hit identity is not yet exposed by the verifier.
- Medical setup reuses the deterministic pointer layout, places rescuer and patient 4–8 clear horizontal cells apart, and keeps both actors paused. Geometry overlap between the two actors is therefore not the explanation.
- `TryIssueRescue` has only two rejection conditions after selection filtering: rescuer must be in combat stance and the raycast-resolved target instance must currently be `Downed`. It otherwise writes the command immediately. Because the final diagnostic samples after two frames, a third possibility remains: the pointer command may be created and then removed by `TickRescue` before the verifier observes it. The next check must capture issue/cancel behavior or sample on the first frame rather than assuming the command never existed.
- `TickRescue` does not normally remove a valid command: it resolves the patient, starts `AbilityRescue`, leaves the command in `Executing`, and only completes if the target vanished/recovered. With the patient still downed, an ephemeral successful command is unlikely. Capturing the controller's published `NoticeFeedEvent` will reveal the exact rejection text without adding production debug state.
- `IGameEventBus.Subscribe<TEvent>` is available from the existing runtime scope. The verifier currently resolves no event bus, so it can add a temporary `NoticeFeedEvent` subscription scoped to the rescue attempts and dispose it immediately afterward.
- `PublishCombatCommandResult` grades successful commands as `NoticeFeedEvent.Grade.NONE`, while only failures are warnings. Filtering the diagnostic subscription to non-`NONE` cannot distinguish a success that was immediately completed/cancelled; the last notice after each right click must be recorded regardless of grade.
- Continuing physical carry/treatment assertions after `POINTER_RESCUE_COMMAND` fails adds a 60-second wait and three derivative failures without new evidence. The verifier should yield-break immediately after the root pointer failure.
- The corrected notice capture is decisive: the controller publishes `1명 구조`, proving raycast resolution, downed target, selection, stance, and `TryIssueRescue` all succeed. The command is then removed before `RightClickActor` returns. The bug is in the command tick/participant lookup lifecycle, not pointer input.
- `CombatCommandParticipantQuery.FindCharacter` searches `ICharacterAiWorldRegistry.Characters`, while `CharacterAiWorldRegistry` separately exposes `AllCharacters` from its lifetime registry. The verifier discovers canonical active staff directly from the scene, so a scene actor missing from the active-AI registry can accept a UI command but be treated as nonexistent on the next combat tick. This split is the leading explanation for immediate cancellation.
- The active-vs-lifetime hypothesis is not yet proven: `CharacterMedicalRuntime` also resolves patients through `worldRegistry.Characters`, and its order remains alive for the downed Dion. Therefore the same active registry likely still contains the patient. The exact removal path must be found before changing participant lookup semantics.
- A scoped `rg "AllCharacters"` returned exit 1 because the combat directory has no such use; this was a no-match diagnostic, not a build failure.
- `AbilityRescue.StartRescue(patient)` reserves the medical order and starts a coroutine synchronously. `TickRescue` then immediately calls `actor.SetAiPaused(true)` after starting it; if pausing the brain invokes `AIRescue.OnStop`, that can stop the just-started coroutine and release the reservation. This explains the empty medical rescuer field, but by itself does not explain removal of the combat command, which should remain and retry.
- `AbilityRescue.RescueRoutine` contains the same `medicalRuntime.TryGetPatient(order, out patient)` condition twice. It is redundant and should be removed once the lifecycle defect is fixed, though it is not the source of command deletion.
- `CharacterLifecycle.SetAiPaused` only flips the pause flag and requests a replan when unpausing; it does not stop the rescue coroutine or remove the command. The pause-sequence hypothesis is therefore disproven.
- `CharacterCombatCommandRuntime.commands` resolves through `aggregateRootStore.GetOrCreateWritable(...)` on every access. If writable-root semantics clone or replace outside an active transaction, a command could be written to one root and read from another; the Aggregate store implementation is the next high-value inspection target.
- Aggregate-store inspection disproved that hypothesis: outside restore staging, `GetOrCreateWritable` returns the same live root state and does not clone or replace it.
- The terminal event proves the exact removal path is `TickRescue -> CompleteCommand("구조 대상 회복")`. Because the verifier's canonical patient remains downed, `CombatCommandParticipantQuery.FindCharacter` is returning a noncanonical same-ID actor whose lifecycle is active. Combat participant queries currently do not canonicalize `worldRegistry.Characters`, while the verifier and other mature callers explicitly use `CharacterActorCollection.GetCanonical`.
- Unity MCP dynamic command compilation cannot directly reference VContainer/Sirenix-backed project types in this environment; the attempted registry dump failed at compile time and made no state change. Diagnostics that need those references must live in the already-compiled Editor assembly or use source-level contracts.
- `CharacterActorCollection.GetCanonical` selects the base `CharacterActor` component on the same GameObject over derived compatibility components. Registry registration was bypassing this boundary, so it could store a derived actor with a separate lifecycle field while scene/test code used the canonical actor. Canonicalizing both register and unregister calls fixes the ownership boundary for all consumers, not only combat rescue.
- A previous wide source excerpt appeared to show a duplicated `TryGetPatient` condition in `AbilityRescue`, but the line-bounded UTF-8 reread proves the file contains only one condition. No cleanup is needed there; the failed combined patch changed nothing.
- Canonicalizing normal active/lifetime registration did not change the live failure. `CharacterAiWorldRegistry.Characters` can bypass those registries whenever `RestoreWorldCandidateIndex.TryGetCharacters` returns a candidate list, so a stale or noncanonical candidate projection is now the primary suspect. The diagnostic must report `aggregateRootStore.IsRestoreStaging` plus active/lifetime registry matches and component types.
- Compiled diagnostics disprove candidate leakage and duplicate registry identity: after all invalid preflights, `candidate=False`, `aggregateStaging=False`, and both active/lifetime views contain exactly the canonical downed Dion instance. The `구조 대상 회복` completion must therefore come from a transient `CharacterRecoveredEvent` or lifecycle transition rather than `CombatCommandParticipantQuery` returning another actor.
- `OnCharacterRecovered` and `TickRescue` currently use the identical terminal status text, so the new terminal event cannot distinguish an event-driven completion from the tick's state check. Recovery-event observation or a typed terminal cause is required next.
- Medical recovery handling already treats `ICharacterBodyHealthRuntime.GetSnapshot(actor).Downed` as the sole authority and ignores inconsistent recovery events. Combat command lifecycle lacked this guard, allowing the exact observed `Completed:구조 대상 회복` while the canonical patient remained downed. Applying the same guard is an authority correction, not a verifier workaround.
- One handler search again included a Windows wildcard path and emitted error 123 after useful `-g` results. The actual reads used explicit files/directories; no edit depended on the failed path segment.
- The body-authority guard resolves the regression completely: the same verifier now observes stabilization, physical parenting/carry, treatment, and recovered lifecycle, with no leaked restore candidate or Aggregate staging root. This proves the fix addresses the runtime lifecycle rather than merely relaxing the test.

## 2026-08-03 circus/captured-wildlife restore audit — initial evidence

- `CircusSaveSection` is still a hand-written warning-based staged boundary. It fabricates `new CircusSaveData()` for malformed/null JSON and calls live `runtime.Restore` during commit without semantic preflight.
- `CircusStateCodec.Restore` clamps sequence state, skips malformed/duplicate orders, cancels missing programs, and rewrites every nonterminal order to `Composition`. Those are lossy migrations inside current-version V18 restore, not strict validation.
- `WildlifeCaptureRuntime.Restore` similarly skips invalid states, rewrites five in-flight transport states to `Penned`, tolerates missing actors, replaces the captured-wildlife Aggregate, and performs live projection outside staging. `CircusRestoreProjection` then lazily releases orders/transient state on a later `EnsureCurrent` call.
- Both `CircusAggregateState` and `CapturedWildlifeAggregateState` already exist, so the correct direction mirrors captivity: strict DTO validation, candidate-world reference validation, Aggregate replacement during staging, and one ordered participant for all Unity/door/transient projection at publication.
- Circus V2 persists one monotonic `nextOrderSequence`, orders with stage/room/program/participant IDs plus phase/economy/risk/timing fields, and captured wildlife with pen/carrier/show references plus care/feed state. Strict validation must cover finite/nonnegative numbers, enum values, list/null/duplicate coherence, canonical `circus:<n>` IDs, authored programs, and cross-links between orders and captured wildlife.
- Current interfaces still expose warning-based `Restore(..., IList<string>)` on both circus and wildlife capture. They need report-based validation/restore contracts or a single circus coordinator API so UI and save sections cannot invoke permissive normalization directly.
- `CircusRuntime` already has lazy `CircusRestoreProjection` called from `Start`/`Tick`, and `WildlifeCaptureRuntime` has its own lazy actor/door projection. A transaction participant can make both explicit at publication, but door membership should be staged with `ReplaceCapturedWildlifeSubjects` rather than replayed per ID after publication.
- The combined circus participant should publish after captivity (`450`) and before any later dependent projection; `500.world.circus` is the natural ordering key. It must own both show-order Aggregate replacement and captured-wildlife Aggregate replacement so their cross-references cannot publish independently.
- To avoid adding another saved authority, terminal/transient state remains runtime-only: save validation builds fresh Aggregate roots, while publication clears access passes, return routes, and carry-parent projections. The save DTO remains the only serialization boundary.
- Runtime state semantics are now explicit: new captures are `AwaitingTransport` with a required carrier; `Transporting` keeps that carrier; pen-born/finished transport is `Penned` with no carrier; show assignment is `MovingToShow` with `assignedShowOrderId`; and escape is `Escaped` with `escaped=true`. The strict validator can enforce these combinations before any safe transient normalization.
- For current V2 snapshots, in-flight carrier/show transforms are not independently persisted. After validating their source references, the pure restore builder may canonicalize transport/show transient states back to `Penned` and nonterminal shows to `Composition`, but it must do so explicitly and deterministically rather than warning/skip during commit.
- The only non-save caller of `IWildlifeCaptureRuntime.Restore` is `GameplayPerformanceWorldConfigurator`, which abuses restore as a benchmark seeding mutation. It should call the existing explicit `TryRegisterPenBorn` command per spawned animal; then strict restore can require an active V18 transaction without retaining a compatibility escape hatch.
- Existing circus clone tests use intentionally informal IDs but never validate/restore them. New strict validation tests must construct canonical `circus:<n>`, real program IDs, and coherent stage/pen/participant fields rather than weakening production validation for clone-only fixtures.
- `BuildingInstanceId` already enforces the `building:*` protocol and should validate both stage and pen IDs. `IResourceEconomyContentCatalog.TryGetItem` can validate an optional `lastFeedItemId`, while `IWildlifeSpeciesCatalogProvider.TryGetSpecies` validates captured species IDs.
- Two path assumptions in the latest search were wrong (`Assets/Scripts/CharacterController.cs` and a guessed save-folder location for `DungeonRuntimeAggregateRootStore.cs`). Both exited 1 after useful output; the store will be located with `rg --files` before reading.
- Another exploratory search included a nonexistent `Assets/Scripts/Character` root and exited 1 after returning useful matches. The correct ability source is `Assets/Scripts/Services/Combat/AbilityRescue.cs`.
- Audit tooling repeated the known Windows error 123 by passing `CharacterCombatCommand*.cs` as a path to `rg`. Subsequent searches use the containing directory plus `-g` only.

## 2026-08-03 circus/captured-wildlife restore conversion — verified result

- Circus orders and captured wildlife now share one transactional publication boundary, `500.world.circus`. Both Aggregate slots and captured-wildlife door membership are written only to the detached candidate root; actor/carry/access-pass projection happens after root publication.
- Current-version corruption is no longer hidden. Duplicate/malformed IDs, sequence reuse, missing programs or cross-links, invalid enums/numbers, incoherent carrier/show/escape states, and invalid candidate-world stage/room/pen/actor/species/feed references fail the entire restore.
- Deterministic normalization is deliberately narrower than validation: a valid nonterminal show restarts at `Composition`, and valid in-flight wildlife returns to `Penned` with transient carrier/show references cleared. No invalid record is skipped, cancelled, clamped, or synthesized.
- Terminal show history does not require a still-existing stage or captured-wildlife record; only active orders require live world references. This avoids making ordinary post-show dismantling or later animal release invalidate otherwise valid saves.
- Restore publication must not invoke normal cancellation/release commands against the new Aggregate. The dedicated circus projection cleanup only disposes old access passes, clears transient routes, and releases actor pause/projection state.
- Benchmark/setup code was the sole non-save caller abusing wildlife restore. Replacing it with `TryRegisterPenBorn` allowed the permissive restore API to be deleted instead of retained as a compatibility backdoor.
- Evidence is direct: strict contract row PASS, V18 validator PASS, public save preflight rejection with unchanged JSON/revision/door version PASS, candidate cleanup PASS, full PlayMode `RESULT=PASS`, and final Unity Console Error 0 / Warning 0.

## 2026-08-03 invasion restore audit — next Phase 112 owner

- After the circus cutover, warning-based restore signatures remain in three invasion owners (response policy, defense engagement, owner evacuation) and one surgery owner. Invasion is the higher-risk next boundary because one save section restores all three plus threat, campaign, and active intruder GameObjects.
- `InvasionSaveSection` accepts section versions 1–3, creates empty DTOs for missing/malformed data, and commits directly into `InvasionSaveService.Restore`. The service mutates campaign and threat first, resets policies, destroys active intruders, restarts evacuation, and only then rebuilds engagements; a late failure cannot preserve the live run.
- Current restore is lossy: policy duplicates are skipped and missing assignments fall back to standard; campaign values are clamped/overwritten; intruder settings synthesize defaults; invalid intruders and engagements are skipped; invalid evacuation targets are silently recalculated. These behaviors must become either explicit version migration before V18 validation or hard current-version errors.
- No invasion Aggregate state exists yet. Pure authority should be grouped into an invasion Aggregate (threat, campaign, response policies and stable engagement DTO state), while candidate-only Unity state must be owned by an ordered restore participant.
- Active intruders cannot be restored by clearing the live director first. The director needs a detached candidate collection of inactive GameObjects, with validated runtime IDs/data/patterns/grid state and no live subscriptions or presentation until publication. Discard must destroy only candidate objects; publish swaps the collection, releases old objects, activates candidates, and attaches callbacks.
- Defense engagements must be built against candidate intruders plus candidate-world characters without starting movement/coroutines during staging. Guard preparation, reservations, combat presentation, and movement begin only after the invasion Aggregate and intruder collection are published.
- Owner evacuation requires the same split: validate the exact saved target rather than recalculating it, stage owner/target/status as data, then start movement only at publication. A bad target must reject the save and leave the existing evacuation untouched.
- A combined participant ordered after circus/candidate world state (provisionally `550.world.invasion`) is preferable to separate intruder/engagement/evacuation participants because their references form one consistency boundary.

## 2026-08-03 surgery restore audit — next Phase 112 owner

- `SurgeryPersistence.Restore` is a live mutation pipeline, not detached restoration. It cancels active transport, releases admitted patients, clears the live order list, then restores parts, storage, corpse freshness, extraction records, wildlife anatomy, and policies before it validates surgery orders.
- Corrupt current data is normalized or skipped instead of rejected: duplicate/blank order IDs are excluded with warnings, transient doctor/transport state is silently erased, missing procedures or facilities turn active work into cancelled history, and negative sequence/environment values are clamped.
- `DungeonSurgerySaveData` has no explicit payload version and combines at least seven mutable owners plus live Unity-world projections. Strict validation and one staged Aggregate/publication boundary are required before the rollback image can be removed.
- `SurgerySaveSection` still accepts section V2–V4, deserializes null/malformed payloads to an empty object, mutates old DTOs in place, and returns a delegate whose commit calls the warning-based live restore. Under the V18 incompatibility boundary it should accept one exact current section/payload version and use the typed JSON section/transaction participant pattern.
- The nested owners (`SurgicalPartRuntime`, storage state, corpse freshness, policy, extraction ledger, wildlife anatomy) each clear their own live dictionaries before filtering records. Because orders cross-reference parts, subjects, corpses, wildlife, physical stacks, and facilities, validating each list independently is insufficient; one candidate graph must validate all cross-links before any owner is replaced.
- Surgical parts, organ-storage fuel, and per-subject policy are ordinary runtime state held in independent mutable collections. They can share a replaceable surgery Aggregate slot while the existing runtimes remain command/query facades; item spawning/deletion and facility fuel delivery must stay runtime effects and must not run while a candidate snapshot is being validated.
- Corpse freshness restore immediately rebuilds against the live physical-item index, so it both filters saved state and synthesizes default freshness entries during commit. Candidate construction must instead validate exact saved corpse stack IDs against the staged physical-item world, then publish the validated map and only resume incremental indexing afterward.
- Wildlife anatomy restore silently resolves unknown profiles to a quadruped/humanoid fallback and fills missing nodes. In V18 current data, profile/species/node identity must be validated against authored anatomy and the staged wildlife world; deterministic node completion is acceptable only when the payload format explicitly omits derived nodes, not as repair for malformed saved node data.
- The existing invasion/circus cutovers establish the reusable pattern: a small domain-specific state store delegates copy-on-write and replacement to `DungeonRuntimeAggregateRootStore`, while a transaction participant stages Unity/world projections and publishes them only after the root swap. Surgery should follow this pattern rather than adding another bespoke rollback buffer.
- Circus shows that the transaction participant need not be the main gameplay runtime. A dedicated `SurgeryRestoreCoordinator` can validate the complete DTO/world graph, replace one candidate Aggregate, stage patient transport/AI projection, and publish it in order while `SurgeryRuntime` and its six supporting services remain focused command/query facades.
- `SurgeryRuntime` already receives four explicit capability bundles, so the Aggregate state store can be a fifth required constructor dependency without reviving the former 28-parameter constructor. The restore coordinator can consume the same four bundles plus root/state stores and stay below the eight-dependency composition limit.
- Surgery payload validation must treat every enum and float as untrusted JSON data: subject kind, order state, failure severity, environment resume stage, material quantities, stage work totals, risk probabilities/contributions, positions, timestamps, freshness, fuel, contamination, quality, and anatomy-node burdens all require finite/range/coherence checks before cloning into the Aggregate.
- The warning-based restore APIs on parts/storage, corpse freshness, policy, and wildlife anatomy are called only by `SurgeryPersistence`. One extraction-ledger fixture also calls its permissive restore directly; that fixture must construct/replace a surgery Aggregate snapshot instead of preserving a general mutation backdoor.
- Generated surgery order IDs use canonical `surgery:N` numbering and part IDs use the existing surgical-part sequence. Strict validation must require each stored sequence to be at least the largest canonical numeric suffix so a restored run cannot issue a colliding ID.
- Patient transport stores live carrier ability/coroutine state outside the DTO. A valid in-flight save cannot resume that Unity execution object exactly; candidate normalization should preserve the order/admission intent but clear `admissionMoveRequested`, `patientTransporterId`, and `patientTransportInProgress`, then let the published runtime deterministically request transport again. This is explicit transient normalization, not corruption repair.
- Publication must first cancel transports and clear AI/action projection from the previous orders using the captured old-order list, then project admitted patients and pending wildlife returns from the newly published Aggregate. It must not release saved physical materials or run ordinary surgery cancellation commands during staging.
- The authored `SurgicalProcedureSO.RequiredFacilityTags` and `ISurgicalFacilityQuery.Evaluate` provide the exact facility-capability check for each active order. World validation should require the saved facility ID to resolve to one live candidate building whose evaluated snapshot is available for those tags, not merely any building with a matching string ID.
- The existing `DungeonJsonSaveSection<TPayload>`/`InvasionSaveSection` pattern already supplies typed preflight and staged commit plumbing. Surgery should replace its hand-written V2–V4 delegate section with that exact base rather than duplicating deserialization and warning propagation.
- `SurgeryPlayModeVerifier` captured item and surgery sections separately and restored both runtimes directly during cleanup. That bypasses the very cross-section transaction being tested; cleanup should capture and restore one `DungeonGameSaveData` through `IDungeonGameSaveService` so candidate-world and Aggregate publication semantics are exercised.
- Focused post-cutover scans find no remaining surgery V2–V4 version branch, warning-based restore API, or direct `ISurgeryRuntime.Restore` caller. The remaining generic `runtime.Restore` hits belong to environment/medical sections and are not surgery bypasses.
### 침공 저장 감사 중 도구 출력 절단

- `InvasionPrimitives.cs`, `DefenseResponsePolicyRuntime.cs`, `InvasionDirectorRuntime.cs` 묶음 읽기가 도구 출력 한도로 절단되었다. 저장소 소스에는 절단 마커가 없으며, 근거 수집은 80줄 이하의 경계 읽기로 다시 수행한다.

## 2026-08-03 invasion restore conversion — verified result

- `DungeonInvasionSaveData`와 `InvasionSaveSection`은 정확한 V4만 허용하며, 위협·침입자·정책·교전·대피·5개 캠페인 분기의 null/ID/enum/수치/내부 참조를 라이브 변경 전에 검증한다. V1~V3와 빈 기본 DTO 복원은 더 이상 허용하지 않는다.
- 위협, 캠페인, 방어 정책은 한 `InvasionAggregateState`에 저장된다. 복원 중에는 `DungeonRuntimeAggregateRootStore`의 detached 후보만 바뀌고 캠페인 월드 거점 투영은 루트 게시 이후로 연기된다.
- active intruder는 기존 목록을 지우기 전에 비활성 RestoreCandidates 루트에서 준비된다. authored prefab과 정상 prefabless 구성 모두 detached 캐릭터 계약을 따르며, 상태·입구·격자·콘텐츠 검증 전에는 코루틴, raid-awareness 투영, `OnFinished` 구독이 시작되지 않는다.
- owner evacuation과 defense engagement도 후보 참조를 먼저 구축한다. 무효 대피 칸을 다시 계산하거나 사라진 경비/침입자를 건너뛰지 않으며, AI 정지·이동·전투 표현은 `550.world.invasion` 게시 시점에만 시작한다.
- 구형 `Restore(..., IList<string>)`, 침입자 경고/skip 복원, `RestoreFromLegacyPressure`, 설정 기본값 합성 경로를 삭제했다. 정책과 캠페인은 검증된 snapshot을 정확히 대체한다.
- Unity MCP 증거: EditMode threat/intruder/engagement 회귀 PASS, fresh/corrupt V4 validation PASS, 정상/후행 실패/V3 거부 원자 계약 PASS, active prefabless intruder 왕복 PASS, detached 후보 잔존 0, V18 authority PASS. 런타임 본체는 partial 분리 후 1,193줄과 1,093줄로 1,200줄 제한을 만족한다.

## 2026-08-03 surgery restore conversion — validator placement

- V18 validator의 정확한 구조 구간은 `RuntimeAuthorityV18Validator.cs` 780–872줄이다. 포획(450) → 서커스(500) → 침공(550) 순서로 Aggregate, strict validator, typed JSON section, ordered participant를 각각 강제하고 legacy warning/migration 토큰을 금지한다.
- 수술은 참여자 순서 `525.world.surgery`이므로 서커스 뒤·침공 앞에 동일한 네 가지 요구와 구형 Restore 금지를 배치하는 것이 기존 검증 체계와 일치한다.
- `SurgeryDebugScenarios.RunAll`은 9개의 순수/에셋 계약을 TSV로 기록하는 486줄 Editor 진입점이다. 여기에 strict V5 payload 계약을 추가하면 Unity MCP 동적 명령은 이 public 진입점만 호출해도 된다.
- 기존 `VerifyUniquePartSaveData`는 `DungeonSurgerySaveData`의 JSON 왕복만 확인하며 반환 문자열이 아직 “V16 section data”다. V5 `version`과 모든 필수 컬렉션/시퀀스를 채운 strict validator 검증으로 보완하고 문구를 V5로 고쳐야 한다.
- `SurgerySaveValidation.Validate`는 public static이며 필요한 의존성은 `ISurgicalProcedureCatalog`, `IAnatomyProfileCatalog`, `DungeonGameRestoreReport`뿐이다. 완전한 빈 V5 payload도 유효하므로, Editor 계약은 실제 authored catalogs로 빈 정상 DTO 통과·V4 거부·null collection 거부를 먼저 고정하고 최소 한 주문/부품 fixture로 ID/sequence/NaN/중복 교차 검증을 추가할 수 있다.
- `CreateState`는 검증 뒤 주문의 운반 실행 플래그만 명시적으로 초기화하고 나머지 Aggregate를 deep clone한다. 따라서 테스트는 이 transient normalization이 source DTO를 변경하지 않으면서 candidate만 정규화하는지도 직접 확인해야 한다.
- `SurgeryDebugScenarios`가 이미 같은 authored 폴더에서 42개 `SurgicalProcedureSO`와 12개 `AnatomyProfileSO`를 로드해 각각 `ResourceSurgicalProcedureCatalog`/`ResourceAnatomyProfileCatalog`를 생성한다. strict V5 검증 테스트는 새 대역이나 런타임 SO 합성 없이 이 에셋 카탈로그를 그대로 재사용할 수 있다.
- `DungeonSurgerySaveData`는 `version = 5`와 일곱 필수 컬렉션을 모두 빈 리스트로 초기화하고 두 시퀀스는 0이다. 따라서 새 DTO 자체가 canonical empty V5 fixture이며, JSON clone으로 V4/null collection/negative sequence 같은 독립 오염을 만들 수 있다.
- `SurgerySubjectPolicyState`는 subject ID와 자동 응급수술 플래그만 갖는다. 같은 canonical subject ID를 두 번 넣는 fixture가 다른 콘텐츠나 Unity 월드 없이 중복 상태 거부를 증명하는 가장 작은 strictness 사례다.
- 침공 원자 계약의 재사용 가능한 패턴은 live `DungeonRuntimeLifetimeScope`에서 runtime/root/participant를 resolve하고, 실제 capture/validate/restore를 위임하는 격리 typed section과 commit 시 한 번 실패하는 후행 section을 `DungeonSaveSectionRegistry`에 조립하는 방식이다. 정상 왕복, payload 변경 후 후행 실패 시 JSON 불변, 구형 section version 거부를 한 진입점에서 증명한다.
- 수술도 동일한 registry 경계를 쓰면 `IDungeonGameSaveService` 전체 매니페스트를 수동 수정할 필요 없이 실제 `SurgeryRestoreCoordinator`의 staging/rollback 참여를 검증할 수 있다. 격리 section의 순서는 수술 participant `525`가 게시된 뒤 후행 실패 section이 commit되는 형태여야 한다.
- 실제 `SurgerySaveSection`은 `ISurgeryRuntime.Capture`와 `SurgeryRestoreCoordinator.ValidateRestore/Restore`만 연결하며 section version은 정확한 V5다. 따라서 원자 계약용 격리 section도 이 세 호출을 그대로 위임하면 프로덕션 복원 경계를 우회하지 않는다.
- coordinator는 active V18 registry staging이 없으면 Restore를 거부하고, validation/world-reference 성공 뒤에만 후보 Aggregate를 교체한다. 현재 라이브 DTO의 `orderSequence`만 1 증가시키면 새 Unity 참조 없이도 유효한 서로 다른 후보를 만들 수 있어 후행 실패 rollback 불변 비교에 적합하다.
- `DungeonRuntimeAggregateRootStore.PublishedRestoreRevision`은 성공적인 후보 root 게시 때만 1 증가하고 discard에는 변하지 않는다. 수술 후행 실패 계약은 JSON 불변뿐 아니라 revision 불변과 `IsRestoreStaging == false`까지 함께 검사할 수 있다.
- 첫 원자 테스트 실패 원인은 소스상 명확하다. `DungeonSaveSectionRegistry.RestoreAll`은 commit 실패 시 후보 root를 discard한 뒤에도 모든 section의 `rollbackImage`를 다시 stage/commit하고 root를 publish한다. 따라서 수술 JSON은 보존돼도 `PublishedRestoreRevision`이 1 증가한다. 이는 Phase 112에 남아 있는 rollback-image 의존성 그 자체다.
- 현재 registry는 모든 section이 `IDungeonStagedSaveSection`인지 확인하지만 “commit이 detached candidate만 변경한다”는 더 강한 계약은 구분하지 않는다. rollback-free 경로를 안전하게 열려면 변환 완료 section을 명시하는 marker가 필요하고, registry에 포함된 전 section이 그 marker를 가질 때만 commit 실패 후 재적용을 생략해야 한다.
- 기존 `IDungeonRestoreTransactionParticipant` 문서는 Begin/Discard가 후보만 할당·해제하고 Publish는 실패하지 않는 pointer/visibility swap이어야 한다고 이미 명시한다. 따라서 추가 marker는 participant가 아니라 section commit의 live-mutation 부재만 선언하면 된다.
- generic `DungeonJsonSaveSection<T>` 자체는 여러 미전환 도메인도 사용하므로 base에 atomic marker를 붙이면 위험하다. `SurgerySaveSection`과 격리 원자 테스트 section처럼 변환 완료 owner가 개별 opt-in해야 한다.

## 2026-08-03 surgery restore conversion — verified result

- 수술의 주문·고유 부품·장기 보관·사체 신선도·대상 정책·적출 원장·동물 해부 상태와 두 시퀀스는 하나의 replaceable `SurgeryAggregateState`에 있다. 저장은 exact V5만 허용하고 invalid record를 skip/clamp/default하지 않는다.
- `525.world.surgery`는 후보 캐릭터·야생동물·시설·물리 스택·authored 절차/해부 프로필을 검증한 뒤 detached root만 교체한다. 운반 실행 상태만 명시적으로 재요청 가능한 transient 상태로 정규화하며 AI/운반/귀환 투영은 root 공개 이후다.
- `IDungeonRollbackFreeSaveSection`을 도입했다. registry의 모든 section이 candidate-only commit을 선언한 경우 commit 실패는 후보 root/Unity 후보를 discard하고 종료하며 rollback image를 재적용하지 않는다. 미전환 section이 포함된 registry는 기존 안전망을 계속 사용한다.
- Unity MCP 증거: V18 authority PASS(`save V18`, authored item 772, catalyst SO 168, legacy item authority 0), strict V5 contracts PASS, 정상/후행 실패/V4 거부 원자 계약 PASS, 실패 전후 수술 JSON과 published revision 불변, staging 잔존 없음, 최종 Console Error 0 / Warning 0.

## 2026-08-03 next rollback-image owner audit — initial scan

- 수술 전환 뒤 `Save|Restore|Persistence` 이름의 운영 파일에서 warning list를 직접 쓰는 곳은 `ModularFacilityWorldSaveService.cs` 하나만 남았다(`DungeonGameRestoreReport` 자체 제외). 시설은 inactive Unity candidate를 이미 사용하므로, warning/skip semantics와 section commit 소유권을 strict candidate-only로 닫으면 rollback-free 전환 효과가 크다.
- direct `IDungeonSaveSection` 파일은 여전히 방어·환경·종족·경제·생산·생존·물리 아이템·원정 등 여러 도메인에 남아 있다. 일부 파일은 테스트 nested section이나 typed section과 함께 있어 파일 단위 검색은 후보 목록일 뿐 완료 증거가 아니다. 다음 감사는 프로덕션 `ModularFacilityWorldSaveService`와 `ModularFacilityWorldSaveSection`의 정확한 복원 경로부터 시작한다.
- 시설 save section은 이미 typed JSON과 exact current-version validation을 사용하지만 rollback-free marker가 없고, `TryRestoreSnapshot`은 candidate factory/resolver/grid publisher가 하나라도 null이면 `ClearExistingBuildings` → session 적용 → live `RestoreBuilding`으로 즉시 되돌아가는 구형 직접 mutation fallback을 유지한다.
- section은 validation warnings를 버리고 restore 시 `worldReport.warnings`를 UI report로 옮긴다. migration warning과 state-module warning이 current V18 payload에서 어떤 의미인지 분리해야 하며, 구형 fallback을 삭제하려면 세 candidate 의존성을 생성자에서 필수화하고 모든 테스트/조립 경로를 갱신해야 한다.
- 운영 생성자는 이미 object factory/resolver/texture/relocation/session/grid publisher/candidate publisher를 모두 null 불가로 강제한다. 직접 live restore는 `#if UNITY_EDITOR` 2인자 생성자만 가능하게 만드는 테스트 전용 backdoor다.
- 이 2인자 생성자는 네 Editor 시나리오에서만 사용된다(진화 1, 시설 save/load 1, 건물 상태 persistence 2). 운영 fallback을 보존할 이유는 없으며, fixture를 candidate-capable 테스트 조립으로 바꾸거나 pure serialization/validation 목적에 맞는 좁은 테스트 대역으로 분리해야 한다.
- `ModularFacilitySaveLoadDebugScenarios`는 실제 source/target Grid와 stale buildings를 만들고 direct `TryRestoreSnapshot` 후 stale 파괴·게임데이터·건물 상태·레이어·JSON 왕복을 검사한다. 이 테스트는 삭제할 것이 아니라 transaction Begin → detached candidate 준비 → Publish 흐름으로 전환해야 한다.
- `BuildingStatePersistenceDebugScenarios`의 두 생성은 world V1 JSON 거부와 writer schema만 검사하며 Grid/Unity 복원을 전혀 사용하지 않는다. 이 둘은 시설 복원 서비스를 가짜 2인자 생성자로 만들지 말고 dependency-free strict facility save codec API를 호출하는 편이 책임에 맞다.
- 진화 시나리오의 네 번째 생성도 V2 JSON 거부만 확인한다. 세 schema/version fixture를 static strict codec으로 옮기면 2인자 서비스 생성자를 쓰는 곳은 실제 시설 save/load 회귀 하나만 남는다.
- 현재 `ToJson`은 null snapshot을 새 기본 DTO로 합성하지만 `FromJson`은 exact V4만 허용한다. strict codec은 serialize에서도 null을 거부해야 current-version 빈 저장을 누락 콘텐츠의 대체물로 만들지 않는다.
- `ResolveBuildingFactory`는 이미 주입된 `buildingFactory`가 있으면 그것을 사용한다. 따라서 Editor 전용 생성자를 candidate-capable 의존성(미리 조립된 factory + relocation/session/grid/candidate publishers)으로 바꾸면 테스트에서 prefab/object resolver를 재현하지 않고도 detached restore만 실행할 수 있다.
- 시설 save/load 시나리오에는 이미 DI가 적용된 `IGridBuildingFactory`와 source/target `GameSessionState`가 있다. 필요한 publisher/store 계약에는 운영 concrete `GridSystemProvider`, `RestoreWorldCandidateIndex`, `GameSessionStateStore`, `FacilityRelocationWorldService`가 존재하므로 생성자 의존성 수와 실제 조립 가능성을 확인한 뒤 fixture 전용 candidate-capable 생성자로 연결할 수 있다.
- 운영 `GridSystemProvider`와 `ScopedGameSessionStateStore`는 씬 manager/settings에 묶여 EditMode fixture에 직접 쓰기 어렵고, `FacilityRelocationWorldService`도 object factory/resolver가 필요하다. save/load fixture에는 세 좁은 대역(`TestGridSystemPublisher`, `TestSessionStateStore`, relocation no-op)과 실제 `RestoreWorldCandidateIndex`를 두는 편이 더 작고 명확하다.
- candidate publish는 live Grid 객체를 교체하므로 fixture는 publish 뒤 `TestGridSystemPublisher.CurrentGrid`를 target grid로 사용해야 한다. 기존 local target Grid에서 restored occupants를 찾는 방식은 direct-mutation 권위에 묶인 테스트다.
- `TryRestoreDetached`도 transaction 비활성 시 후보를 즉시 publish하는 두 번째 backdoor를 갖고 있다. `TryRestoreSnapshot`의 direct clear/restore 분기와 함께 삭제하고, 호출은 반드시 `BeginRestoreCandidate`가 선행된 registry transaction 안에서만 허용해야 한다.
- detached building creation은 state-module restore warning을 그대로 허용한다. 구조 검증은 null/빈 ID/중복/비양수 version/빈 payload만 확인하므로, warning 조건이 “저장에만 있거나 런타임에만 있는 module” 같은 lossy skip/default인지 확인해 current V4 strictness를 결정해야 한다.
- 시설 participant의 Publish는 후보 활성화 → 기존 시설 clear → Grid publish → session restore → detached publish 순서로 여러 fallible 호출을 포함한다. section commit을 rollback-free로 표시할 수는 있지만, 최종 one-shot world replacement 완료 전에는 publication 단계도 검증/비실패 swap으로 더 분리해야 한다.
- `BuildingStateModulePersistence.Restore`에서 유일한 warning은 현재 건물에 존재하지만 저장 payload에 없는 module을 기본값으로 유지하는 경우다. Capture가 모든 module을 저장하고 unknown/invalid/duplicate는 이미 error이므로, current-version payload에서 missing module도 콘텐츠 누락/스키마 불일치로 간주해 error로 바꾸는 것이 strict V18 원칙과 일치한다.
- 기존 진단 테스트는 missing module의 성공+warning을 의도적으로 기대한다. 이를 명시적 실패+module ID error로 고치고 warning collection/전달을 제거하면 시설 restore의 마지막 warning/default 경로를 닫을 수 있다.
- 시설 restore report의 다른 warning은 authored `BuildingSO` 배치 layer가 저장 layer와 달라졌을 때다. 저장된 runtime layer와 별개로 authored placement layer 불일치는 콘텐츠 계약 변경이므로 경고 후 계속하기보다 preflight error로 거부해야 한다.
- `migrationWarnings`와 `migratedFromVersion`은 `[NonSerialized]`이고 exact V4 `FromJson`은 migration을 수행하지 않는다. 이 비직렬 필드와 report warning API는 현행 복원에서 구형 의미만 남아 있으므로 삭제 가능하다.
- candidate-capable Editor 생성자는 미리 조립된 `IGridBuildingFactory`를 받을 수 있지만 publication은 `IGridTextureProvider.Texture.DrawBuilding`을 직접 호출한다. 기존 다른 Editor fixture는 null texture provider를 사용하므로, 시설 save/load 테스트에는 실제 `GridTexture` 컴포넌트를 만들거나 시각 publication을 별도 capability로 분리해야 한다. 운영 provider는 texture null을 허용하지 않는다.
- `GridTexture.DrawBuilding`은 missing building/tile/sprite/tilemap을 안전하게 no-op하므로, fixture GameObject에 실제 빈 `GridTexture` 컴포넌트를 붙인 provider를 사용할 수 있다. 운영 null 계약을 완화할 필요가 없다.
- `RestoreBuilding`은 transaction 비활성 fallback에서만 호출되는 live 생성/등록/state restore 구현이다. fallback 제거 후 메서드 전체를 삭제할 수 있으며, candidate 생성은 `TryCreateDetachedBuilding` 하나로 단일화된다.
- warning 필드 제거 후 남는 사용처는 시설 candidate의 state result 전달과 두 건의 BuildingState persistence 진단 문자열/기대뿐이다. legacy core-module migration 테스트는 실제 warning에 의존하지 않고 결과 문자열만 count를 출력하므로 strict error 전환과 충돌하지 않는다.
- 기존 V18 validator는 시설에 대해 `TryRestoreDetached` 존재만 요구하고 character restore가 candidate Grid를 읽는지만 검사한다. transaction-only 호출, rollback-free section, strict codec, direct `RestoreBuilding`/warning 부재는 아직 강제하지 않으므로 이번 전환에서 ratchet을 보강해야 한다.
- 첫 통합 회귀의 시설 NRE는 `TryCreateDetachedBuilding`이 test constructor에서 null인 `objectFactory`를 직접 호출한 정확한 조립 오류다. candidate 생성 abstraction은 `IGridBuildingObjectFactory`, 기존 fixture factory는 `IGridBuildingFactory`이므로 Editor 생성자도 detached object factory를 명시적으로 받아야 한다.
- `generic_stock_categories` fixture는 존재하지 않는 enum 777을 `StockCategoryPersistenceId.ToId`로 변환해 성공을 기대하는 낡은 테스트다. authored/fixed protocol cutover 이후 올바른 계약은 물리 수량이 snapshot에 없고 unknown category protocol은 거부된다는 것이므로 custom-ID 성공 문구를 제거해야 한다.
- detached 시설 생성은 `IGridBuildingObjectFactory.CreateDetached` 뒤 `IObjectResolver.Inject`를 별도로 호출한다. Editor constructor는 이미 주입된 ordinary factory만 받으면 안 되고, detached object factory와 명시적 building injector를 받아야 한다.
- 서비스 내부에 `Action<BuildableObject>` injector를 두면 운영 생성자는 `objectResolver.Inject`를, Editor fixture는 `CharacterAiEditorTestDependencies.Inject`(+Shop 보강)를 제공할 수 있다. 같은 object factory로 ordinary `GridBuildingFactory`도 조립할 수 있어 test-only resolver 구현이 필요 없다.
- `FacilityRuntimeStateModule`이 module version 1을 받아 `LegacyFacilityOperationalStateV1` 하나를 core/production/security로 분해하는 것이 legacy split 테스트의 근거였다. 그러나 현재 writer는 version 2만 쓰고 V17 이하는 복원하지 않는 확정 계획이므로 이 migration을 보존하는 것보다 version 1을 명시적으로 거부하는 편이 최종 계약에 맞다.
- `RestoreLegacyFacilityStateV1` 사용처는 legacy test와 save/load fixture 초기화뿐이다. fixture는 현행 `RestoreFacilityState`와 production module setter로 같은 상태를 만들 수 있으므로 legacy 모델·분기·테스트를 모두 제거할 수 있다. 별도 migration coverage 인터페이스는 필요 없다.
- 진화 determinism 실패는 시설 저장 변경과 무관하게 `EditorRuntimeReferenceFixtures.DungeonWithRunVariables`가 aggregate-root 주입이 필요한 `RunVariableRuntime`을 직접 노출하는 오래된 fixture 경로에서 발생한다. scenario가 필요한 것은 결정론 seed/run-variable query뿐이므로 fixture의 생성 경계를 canonical Aggregate store와 맞춰야 한다.
- `EditorRuntimeReferenceFixtures.DungeonWithRunVariables`는 disabled MonoBehaviour만 만들고 `RunVariableRuntime.Construct`의 8개 필수 의존성을 전혀 주입하지 않는다. `RunSeed` getter가 Aggregate root를 즉시 요구하므로 이 fixture는 구조 개편 이후 본질적으로 무효다.
- 운영 runtime에 Editor용 late-binding hook을 추가하는 것은 선택형 DI 제거 목표와 반대다. 진화 runtime이 결정론 seed만 필요하다면 scene-bound `RunVariableRuntime` 대신 좁은 seed/query 계약을 주입하도록 분리하거나, 해당 시나리오가 이미 제공하는 명시적 seed 경로를 사용해야 한다.
- `FacilityInstanceEvolutionRuntime`은 실제로 `DungeonSceneRuntimeReferences`에서 concrete `RunVariableRuntime`을 꺼내 `RunSeed` 하나만 읽는다. 이는 scene adapter/concrete 결합이며 테스트 fixture를 무효화한 직접 원인이다.
- 저장소에 공용 run-seed query 계약은 없다. `IRunSeedProvider { int RunSeed; }`를 도입해 `RunVariableRuntime`이 구현하고 시설 진화가 그 계약을 직접 받도록 바꾸면 운영 조립은 더 좁아지고 Editor determinism fixture는 plain fixed provider를 사용할 수 있다.

## 2026-08-03 modular facility restore conversion — verified result

- 시설 JSON은 dependency-free `ModularFacilityWorldSaveCodec`에서 exact current version만 역직렬화하며 serialize null 합성도 금지한다. schema/version fixture는 더 이상 restore service를 가짜 생성하지 않는다.
- `TryRestoreSnapshot`은 active V18 transaction이 없으면 실패한다. direct live clear/rebuild, transaction 없는 즉시 candidate publish, `RestoreBuilding`, Editor 2인자 backdoor를 삭제했고 section은 rollback-free commit을 선언한다.
- authored placement layer 불일치, missing/unknown/duplicate state module, legacy facility module v1은 모두 error다. current module default 유지 warning, migration warning DTO, facility restore warning report를 삭제했다.
- 전체 Editor 왕복은 11개 시설을 inactive replacement Grid에 구축한 뒤 stale 시설 2개를 제거하고 session/building state/layer/JSON을 정확히 보존했다. invalid overlap preflight는 live target을 건드리지 않고 거부됐다.
- 시설 진화가 concrete scene runtime 대신 `IRunSeedProvider`를 직접 받도록 좁혀 invalid Editor scene fixture 의존을 제거했다. Unity MCP 증거는 state persistence 7/7 PASS, facility round-trip 9/9 PASS, instance evolution PASS, V18 authority PASS, Console Error 0 / Warning 0이다.

## 2026-08-03 character world restore audit — initial scan

- 캐릭터 월드는 이미 detached candidate participant를 갖고 replacement facility Grid를 소비하지만 warning/default 경로가 다수 남아 있다: 빈 persistent state면 live actors 보존, owner snapshot이 없으면 current owner 유지, invalid position이면 nearest cell 이동, missing owner manager/profile/actor 참조 경고 등이 있다.
- section은 아직 rollback-free marker가 없고 `CharacterWorldSaveService.Restore`가 candidate 준비와 preserve-live 정책을 함께 결정한다. exact V18 복원에서는 저장 DTO가 빈 경우 “현재 캐릭터 유지”가 아니라 정확한 빈 세계 또는 필수 owner 누락 오류여야 하며, 좌표 보정도 실패로 바뀌어야 한다.
- `ValidateRestore`는 `actors`와 `populationProfiles`가 `null`이면 빈 목록으로 합성하고 owner를 `0..1`명으로 허용한다. V18 canonical payload라면 두 컬렉션의 존재와 정확히 한 명의 owner를 강제해야 빈 저장이 라이브 owner 보존으로 변질되지 않는다.
- 캐릭터 정의 검증이 authored `characterCatalog`뿐 아니라 현재 라이브 actor의 `Identity.Data` ID도 신뢰한다. 라이브에만 남은 정의가 저장 참조를 합법화할 수 있으므로 복원 사전 검증은 불변 콘텐츠 카탈로그만 권위로 사용해야 한다.
- null 항목을 오류로 기록한 뒤 `.Where(... != null)`로 계속 처리하는 구조는 진단에는 무해하지만, 실제 후보 준비에서도 같은 필터가 사용되면 손실 복원이 된다. 검증 실패 payload는 후보 준비 자체를 시작하지 않고 exact collection을 사용해야 한다.
- `ApplyActorState`는 저장 좌표가 유효하지 않으면 nearest walkable cell 또는 현재 transform 위치로 바꾸고 warning만 남긴다. exact V18 복원에서는 좌표의 격자 유효성·점유 가능성을 preflight에서 검증하고 후보 적용은 저장 좌표를 그대로 사용해야 한다.
- actor 하위 컬렉션도 `null → empty`, null 항목 제거, condition 중복은 `Last()` 선택, 알 수 없는 work type은 무시하는 식으로 보정된다. strict validator가 필수 컬렉션·항목·고유 키·작성된 work type을 모두 보장하고 적용 단계에서는 필터/기본값 없이 읽어야 한다.
- 저장된 lifecycle이 `Active`/`OnExpedition` 이외이면 복원 시 `Active`로 정규화된다. 저장 DTO가 표현 가능한 lifecycle을 exact 복원할 수 없다면 해당 상태를 캡처 대상에서 제외하거나 transient 필드로 명시해야 하며, 현재처럼 영속 필드로 저장한 뒤 warning으로 바꾸는 것은 허용할 수 없다.
- `DungeonCharacterWorldSaveData` 자체에는 버전/계약 표지가 없고 모든 하위 컬렉션과 snapshot이 필드 initializer로 기본 생성된다. JSON 필드 누락도 정상 empty/default처럼 보일 수 있으므로 section 버전만으로는 canonical payload를 판별할 수 없다. strict validation에서 필수 참조형 필드의 null을 전부 거부해야 한다.
- 서비스는 후보 참여자이지만 공개 계약에 `PrepareForWorldRestore()`와 `Restore(...)`가 남아 있어 section/coordinator 밖에서도 순서를 어겨 호출할 수 있다. 최소한 `Restore`가 active transaction을 강제하고, 장기적으로는 외부 계약을 capture/query와 transaction-owned restore 경계로 분리해야 한다.
- `Capture`는 persistent actor를 열거할 뿐 owner 존재를 보장하지 않는다. strict section이 정확히 한 owner를 요구하도록 바꾸면 캡처도 동일 invariant를 즉시 검사해야 자기 자신이 복원할 수 없는 V18 payload를 만들지 않는다.
- `BeginRestoreCandidate`는 `restoredActorsById`만 얕게 백업하고, `PrepareForWorldRestore`는 라이브 actor의 작업·이동·AI를 즉시 변경하는 별도 공개 단계다. 이 호출이 preflight/후보 준비 전에 실행되면 실패 복원이 라이브 행동 상태를 이미 건드릴 수 있으므로 coordinator 순서와 호출처를 확인하고 해당 변경을 commit 시점으로 옮겨야 한다.
- 현재 quiescence participant는 후보 준비가 끝난 뒤라도 publish 순서 `050`에서 라이브 캐릭터의 작업·이동·AI를 먼저 취소한다. 후행 facility/character participant publish가 실패하면 rollback-free 경계에서는 이 변경을 되돌릴 이미지가 없으므로, quiescence를 별도 선행 participant로 두면 캐릭터 section을 안전하게 rollback-free로 선언할 수 없다.
- `DungeonCharacterWorldSaveSection.ResolveRestoreGrid`는 replacement Grid가 없으면 라이브 Grid로 폴백한다. 시설 section이 필수 dependency인 V18 restore에서는 후보 Grid 누락을 오류로 처리해야 캐릭터 후보가 라이브 월드와 결합되지 않는다.
- `Restore`는 active transaction이 아니어도 즉시 publish하고, empty actor payload는 live actor 보존 candidate와 warning으로 성공시킨다. 또한 candidate 정의 사전에 현재 라이브 actor의 SO를 추가한다. 세 경로 모두 authored payload/catalog/candidate world만 신뢰하는 strict V18 계약과 충돌한다.
- owner 누락은 warning으로 현재 owner를 후보 view에 합성하고, 후보 생성자에도 `preserveLiveActors` 플래그가 전파된다. exact payload가 정확히 한 owner를 강제하면 이 두 분기와 `BuildCandidateCharacterView`의 live-owner 합성을 전부 삭제할 수 있다.
- `PublishCharacterCandidate`는 profile/reputation 복원, detached GameObject 활성화, owner publish, 기존 staff 파괴를 순차 수행한다. 모두 라이브 변경이며 중간 예외 가능성이 있으므로 rollback-free 선언 전에 입력·의존성 사전 검증을 강화하고 publish 경로가 no-fail인지 확인하거나 한 번에 교체 가능한 world root 뒤로 감싸야 한다.
- 후보 staging 시 `restoredActorsById`를 실제 공개 전에 후보 actor로 교체한다. 다른 후행 section이 candidate query를 통해 참조해야 한다면 별도 candidate index가 권위가 되어야 하고, live query인 `TryGetRestoredActor`가 준비 중 후보를 노출하는 것은 제거해야 한다.
- 현재 컬렉션 validator는 condition/work-priority 중복 일부만 검사하며 `workTypeId`의 실제 등록 여부, priority enum, mood/growth/narrative/social/carry 하위 참조의 non-null·범위 계약을 충분히 보지 않는다. 적용 코드가 모르는 work type을 조용히 건너뛰므로 strict preflight 범위를 확장해야 한다.
- `DetachedCharacterWorldCandidate` 생성자도 population profile null 항목을 제거하고 null reputation을 새 기본 snapshot으로 합성한다. candidate는 검증된 DTO를 그대로 deep-clone해야 하며, 생성자에서 데이터 손실/기본값을 만드는 정책을 제거해야 한다.
- `AddCandidateIdentity`는 null/empty ID를 조용히 return한다. preflight가 보장하더라도 commit-boundary 내부 방어는 실패해야 하며, silent omission은 `ActorsById`와 실제 후보 목록의 불일치를 숨긴다.
- `ApplyActorState`는 growth/narrative/log을 다시 `null → default/empty`로 합성하고 carry/social restore를 null 허용 호출한다. DTO initializer와 별개로 역직렬화된 explicit null을 strict validator에서 거부한 뒤 적용 경로의 `?? new`를 제거해야 한다.
- 캡처는 interaction mood factor와 최대 30개 로그만 영속화하는 명시적 축약 정책이다. 이 축약 자체는 정상이나, 복원 시 lifecycle/좌표/하위 객체를 보정하는 것과 구분해 계약·테스트에서 canonical capture → exact restore 범위를 고정해야 한다.
- `CharacterWorldSaveSection`은 typed JSON section이지만 아직 `IDungeonRollbackFreeSaveSection` marker가 없고 replacement Grid query를 선택적 폴백처럼 사용한다. 시설 후보가 필수인 V18 dependency임을 section 수준에서 강제한 뒤 marker를 붙여야 한다.
- 전용 캐릭터 월드 strict/atomic Editor 시나리오는 보이지 않고 progression/game-save의 간접 왕복만 있다. 빈 actor·owner 누락·invalid 좌표·null 필드·transaction 밖 restore·후행 commit 실패 불변성을 직접 고정하는 테스트가 필요하다.
- candidate index는 이미 Grid/시설/캐릭터를 transaction 동안 별도 read-only view로 노출한다. 따라서 live `restoredActorsById`를 staging 때 후보로 바꿀 필요가 없고, 후행 복원은 `IRestoreWorldCandidateQuery.TryGetCharacters`를 사용하도록 유지하면 된다.
- quiescence participant는 composition root에 별도 등록돼 있다. 캐릭터 publish 안에서 교체 직전 기존 actor를 정리하도록 합치면 이 선행 live mutation participant를 제거할 수 있고, transaction participant 수와 실패 표면도 줄어든다.
- `TryGetRestoredActor`는 Offense 복원이 transaction 안에서 캐릭터 후보 ID를 해석할 때 사용한다. live 인덱스를 staging에서 교체하는 대신, active transaction 동안 `stagedCandidate.ActorsById`를 우선 조회하고 publish 성공 후에만 live 인덱스를 교체하면 동일 기능을 권위 혼합 없이 유지할 수 있다.
- participant 순서는 facility `100`, construction `150`, character `200`이고 quiescence만 `050`이다. quiescence 제거 후 캐릭터 교체는 facility/Grid 및 construction candidate가 준비된 뒤 일어나며, 후행 도메인들은 candidate query/ID lookup으로 새 actor를 참조할 수 있다.
- 저장 위치 검증은 범위 검사인 `Grid.IsValidGridPos`뿐 아니라 replacement Grid의 `IsWalkable`도 요구해야 한다. 기존 nearest-cell 보정이 두 조건을 함께 대신했으므로 하나만 검사하면 벽/비통행 시설 위 좌표가 exact restore에서 살아남는다.
- `WorldCharacterProfile.Clone`와 `GlobalFacilityReputationSnapshot.Clone`도 null 하위 상태/항목을 기본값·필터로 보정한다. 복원 후보 생성에 이 Clone을 그대로 쓰려면 strict validator가 profile의 social/growth/narrative와 reputation의 rumors/reputation 컬렉션 및 항목을 먼저 완전하게 검사해야 한다.
- `CharacterGrowthState.Clone()`은 먼저 `EnsureCollections()`를 호출해 source DTO 자체의 null 필드를 채우고, null skill/draft/allocation 항목을 필터링한다. 복원 전 source 불변성까지 보장하려면 strict validation 후에만 호출하고 모든 컬렉션/항목을 non-null로 강제해야 한다.
- narrative facts도 `Facts` getter가 null 컬렉션을 생성하고 Clone이 null fact를 제거한다. strict validator는 `facts` 존재, null 항목 0, enum/유한 수치/비음수 카운트와 고유 `(domain,factId,subjectId)` 키를 검사해야 한다.
- social memory/reputation은 rumor와 `SocialMemoryFloat` 목록을 가진다. strict 검증은 모든 목록/항목 non-null, enum 정의, rumor 확률·잔여시간·수치 유한성, memory key 비어있지 않음과 중복 키 0을 요구해야 silent Clone 필터가 작동하지 않는다.
- carry inventory는 stack/item/definition ID, quantity, contamination, item component DTO를 포함한다. 아이템 Aggregate가 별도 권위이므로 캐릭터 carry snapshot이 실물 아이템 소유권을 중복 저장하는지 후속 감사가 필요하지만, 이번 cutover에서는 최소한 null/ID/수량/오염/중복 instance 계약을 strict하게 검증해야 한다.
- `CharacterCarryInventory.Restore` 자체도 null snapshot/목록/항목을 비우거나 건너뛰고 quantity·contamination을 보정한다. 캐릭터 preflight가 이 메서드 호출 전에 canonical carry DTO를 보장해야 하며, 이후 아이템 단일 권위 단계에서 이 별도 carried-item 상태를 `ItemInstanceId` 참조로 축소해야 한다.
- persistent actor 캡처는 `Despawned`와 dead만 제외하므로 `SpawningOutside`, `EnteringDungeon`, `ExitingDungeon`, 원정 준비/출발/귀환, `Downed`도 실제 저장될 수 있다. 복원에서 Active로 바꾸는 대신 각 상태를 정확히 재구성하거나 transient 상태를 영속 DTO에서 제거하는 명시적 계약이 필요하다.
- `CharacterLifecycle.SetLifecycleState`는 모든 enum 상태를 직접 설정하고 비-Active에서 AI 실행 상태를 정리하므로 DTO lifecycle을 그대로 적용할 기술 경로가 있다. 기존 Active/OnExpedition 특례와 warning normalization을 제거하고 exact 상태를 설정할 수 있다.
- `ApplyActorState`의 나머지 default/filter 경로는 strict preflight 후 제거 가능하다. work type은 validator에서 `WorkTypeCatalog.TryGet`을 요구하고 적용에서는 누락 시 예외, growth/narrative/carry/social/log은 non-null canonical 필드를 그대로 사용한다.
- `CharacterIdentity`의 role은 authored `CharacterSO`가 권위이고 런타임 setter가 없으며, character type만 명시적으로 변경 가능하다. 저장된 role이 definition role과 다르면 오류로 거부하고, character type은 저장값을 exact 적용해야 한다.
- `displayName`은 identity의 독립 상태가 아니라 initialized growth displayName → definition name → GameObject name의 파생값이다. DTO의 별도 `displayName`은 중복 권위이므로 이번 복원에서는 성장 상태와 일치하도록 검증하고, 후속 DTO 정리에서 제거하는 편이 맞다.
- `SocialMemoryFloat`는 단순 `(key,value)` DTO이므로 목록별 empty/duplicate key, non-finite value 검증으로 strict clone 계약을 고정할 수 있다.
- `CharacterExpeditionRecoveryState.CopyFrom/Clone`은 null을 0으로, stress를 0..100으로 clamp한다. preflight에서 snapshot non-null·finite·범위를 강제하면 복원 중 보정 없이 동일 값을 유지할 수 있다.
- `DungeonGameRestoreReport`는 error 목록을 공개하므로 캡처 직후 동일 `ValidateRestore`를 실행해 “캡처는 성공했지만 자기 자신이 복원 불가”한 payload를 즉시 예외로 바꿀 수 있다.
- 모든 lifecycle에 walkable cell을 강제하면 dungeon 밖에 있는 출입/원정 actor의 정상 캡처를 거부한다. spatially active한 `Active`/`Downed`만 replacement Grid walkability를 요구하고, 나머지는 저장된 grid 좌표를 상태와 함께 그대로 투영하는 계약으로 분리한다.
- V18 validator에는 아직 `050.world.characters.quiescence` 존재를 요구하는 이전 ratchet이 남아 있어 새 no-live-mutation staging 구조와 정면 충돌한다. 이를 active transaction, strict validator, rollback-free section, detached Grid 강제 요구로 교체하고 preserve/warning/nearest/direct-publish 패턴을 금지해야 한다.
- 캐릭터 월드 cutover 완료: 정확히 한 owner, authored character catalog, nested actor/profile/reputation/carry validator, lifecycle exact 적용, Active/Downed cell exact 검증, transaction-only staging, facility candidate Grid 필수, staged ID lookup과 live index 지연 교체를 적용했다. preserve-live actor, live SO 보충, nearest-cell 이동, warning/default, 직접 publish, 선행 quiescence participant는 제거됐다.
- `CharacterWorldSaveSection`은 rollback-free로 전환됐다. 실제 facility/character section과 participant를 격리 registry에 조립한 후행 고의 실패에서 owner·live Grid·Aggregate revision이 불변이고 candidate index/staging/detached actor가 모두 정리됨을 PlayMode에서 확인했다.
- 전체 V18 왕복은 owner progression Lv.4/XP19, active/passive skill, growth/narrative를 정확히 보존했고 direct restore, ownerless payload, invalid active cell, V17 root를 모두 명시적으로 거부했으며 restore warning은 0이었다.

## 2026-08-03 remaining Unity-object owner audit — wildlife first

- 남은 실제 Unity 후보 participant는 construction sites `150`, wildlife `250`, exterior zones `300`(상수 위치), medical/combat/captivity/circus/surgery/invasion이다. 현재 production rollback-free marker는 facility, character, surgery만 있으므로 registry 전체는 아직 rollback image를 유지한다.
- wildlife는 이미 exact payload version validator, replacement Grid/building/character candidate query, detached actor preparation, candidate index와 publish/discard 경계를 갖는다. section에 marker가 없고 direct transaction 외 Restore 차단·warning/default 부재·동기 candidate cleanup 증거가 충분한지 확인하면 다음 cutover로 가장 적합하다.
- exterior는 zone/character/wildlife 후보를 모두 결합해 wildlife 다음 순서가 자연스럽고, runtime의 일반 gameplay 위치 해석에는 nearest fallback이 있으므로 save restore 전용 coordinator와 혼동하지 않고 분리 감사해야 한다.
- construction sites도 detached 후보를 갖지만 1,200줄에 가까운 `WorkAmountSystem` 안에 work Aggregate와 Unity site publication이 결합돼 있어, wildlife보다 분해 판단이 크다. 우선 wildlife를 strict rollback-free로 완결한 뒤 construction/exterior를 이어간다.
- `WildlifeSaveSection`은 exact `DungeonWildlifeSaveData.CurrentVersion`과 runtime validator를 사용하지만 rollback-free marker가 없다. `WildlifeRuntime.Restore`는 active transaction, single stage, detached facility Grid를 이미 강제하며 transaction 밖 live publish 경로는 보이지 않는다.
- wildlife publish는 ecosystem/carcass 상태 복원 → 기존 actor 파괴 → population reference 교체 → 새 actor 활성화 순서다. rollback-free 선언 전 각 호출이 prevalidated/no-fail인지, discard가 detached actor를 동기 제거하는지, candidate factory가 null/default/filter를 만들지 않는지 확인해야 한다.
- actor 후보 준비는 authored species와 exact candidate Grid cell 점유를 검사하고 detached actor가 실제 Wildlife layer에 등록됐는지까지 확인한다. 실패하면 report error와 candidate discard로 종료하며 nearest-cell/default spawn 보정은 restore 경로에 없다.
- discard는 candidate population의 각 detached actor에 `DiscardDetachedRestore`를 호출한 뒤 모든 후보 목록/예약을 비운다. 실제 GameObject 제거 방식과 candidate DTO clone의 손실 여부를 다음 소스에서 확인한다.
- `WildlifeRestoreCandidate.Create`는 validator가 보장한 non-null food raid/ecosystem/carcass 컬렉션을 필터/default 없이 deep-copy한다. `NextCarcassTickAt`은 현재 clock에서 재생성되는 transient scheduler 값이고 `InitialSpawnCompleted=true`는 복원된 population이 초기 자동 spawn을 반복하지 않도록 하는 운영 상태다.
- 후보 population은 Unity actor 목록과 순수 예약/sequence 상태를 한 객체에 함께 보유한다. 이번 cutover에서는 detached publication 경계가 안전한지 증명하고, 장기적으로는 actor ID Aggregate와 Unity projection을 분리할 후보로 기록한다.
- `WildlifeActor.DiscardDetachedRestore`는 PlayMode에서 `Destroy`를 사용해 제거를 프레임 끝까지 지연한다. unpublished 후보는 외부 참조가 없어 동기 `DestroyImmediate`가 안전하며 rollback-free failure 직후 candidate leak 0을 증명하려면 캐릭터와 동일하게 즉시 제거해야 한다.
- `WildlifeCarcassService.RestoreFreshness`는 null enumerable을 empty로, null/empty entry를 skip하는 warning 없는 보정 경로다. strict save validator 뒤에서는 입력이 canonical이지만 공개 API가 다른 호출자에게 fallback을 제공하므로 호출처를 조사해 strict replacement 메서드 또는 사전조건 예외로 바꿔야 한다.
- carcass/ecosystem Restore 호출자는 wildlife transaction publication 한 곳뿐이다. 따라서 두 API를 “validated candidate replacement” 의미로 좁히고 null/invalid 입력을 예외로 거부해도 운영 호환 경로가 필요 없다.
- `WildlifeSaveValidation`은 payload/version/sequence/필수 목록, authored species, animal ID/health/state, physical carcass stack 교차참조, habitat patch의 replacement Grid 가용 cell까지 preflight한다. candidate/publish가 이 validator 계약을 다시 보정하지 않고 소비하도록 만들 수 있다.
- `WildlifeEcosystemRuntime.Restore`는 null→default, 압력/시간 clamp, null respawn skip을 수행하고 source DTO를 `pendingSaveData`로 보관한다. 유일 호출자가 validated candidate publication이므로 이 보정을 제거하고 null/duplicate를 예외로 거부한 exact replacement로 바꿔야 한다.
- ecosystem publication은 overlay/decoration을 즉시 clear하고 initialization을 reset한다. 후속 `RebuildPopulationRuntimes`가 pending patches를 replacement Grid에 적용하는지 확인해야 publish 중간 실패 표면과 live projection 교체 순서를 판단할 수 있다.
- `RebuildPopulationRuntimes`는 ecosystem을 초기화하지 않고 hunt/behavior facade만 새 population 컬렉션에 다시 결합한다. ecosystem patch 적용은 다음 Tick의 `EnsureInitialized`에서 live Grid provider를 조회하므로 facility participant가 먼저 replacement Grid를 publish한 뒤 적용된다.
- `EnsureInitialized`는 saved patch를 null/usable 필터로 제거하고, 결과가 0이면 scene/default patch 생성, water patch 교체, forage patch 보강을 수행한다. validator가 saved patch 가용성을 이미 증명해도 저장에 없던 patch를 추가·교체할 수 있어 exact round trip과 충돌한다. restore candidate publication 시 exact saved patch 집합을 적용하는 별도 strict 경로가 필요하다.
- `ApplyPendingRespawns`는 respawn 상태를 적용하지 않고 legacy version mismatch를 current로 덮어쓰기만 한다. version은 이미 exact validator가 보장하므로 이 mutation은 삭제 가능하며, 메서드명과 실제 동작 불일치도 함께 제거해야 한다.
- `IWildlifeEcosystemRuntime.Restore`가 공개 mutating API로 노출돼 strict candidate 경계를 표현하지 못한다. `PrepareRestoreCandidate(save, replacementGrid)`와 `PublishRestoreCandidate(candidate)`로 분리하면 patch 변환/가용성 검증은 stage에서, live pointer/list 교체는 participant publish에서 수행할 수 있다.
- carcass clone도 `remainingFreshnessSeconds`를 clamp한다. validator가 nonnegative finite를 보장하므로 candidate clone과 live replacement 모두 값을 그대로 복사하고 invalid direct 호출은 예외로 실패해야 exact round trip이 된다.
- `WildlifeHabitatPatch` constructor는 radius 0..12, capacity 최소 0.1, current/capacity, danger 0..1, tag trim/distinct를 보정한다. strict validator는 현재 radius 최소 1·capacity/danger 최소 0만 확인하므로 radius≤12, capacity≥0.1, danger≤1, canonical tag/ID 문자열을 추가 검증해야 candidate 변환이 값을 바꾸지 않는다.
- food-raid candidate clone도 null→default, text null→empty, stolen quantity clamp를 수행하며 유일 호출자는 validated restore candidate 생성이다. 이를 null 예외와 exact field copy로 바꿔 validator가 단일 canonicalization 권위가 되게 한다.
- 기존 wildlife PlayMode suite가 invalid preflight live actor 보존, 정상 detached actor publish, 후행 실패 candidate discard를 이미 실행한다. 이를 새 rollback-free marker/root revision/candidate index/detached object 0과 ecosystem JSON exact round trip까지 강화하면 별도 fixture를 만들 필요가 없다.
## 2026-08-03 V18 validator location correction

- `RuntimeAuthorityV18Validator`는 공용 `Editor/Validation` 폴더가 아니라 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`에 있다.
- 이후 wildlife ratchet은 이 실제 파일을 작은 범위로 읽고 수정한다. 파일명 검색이 아니라 타입 심볼 검색이 현재 저장소 구조에서 더 신뢰할 수 있다.
- 기존 wildlife ratchet은 detached actor 준비와 publication 지연까지만 확인하며, `IDungeonRollbackFreeSaveSection`, 생태계 후보 publication, carcass exact replacement, 구형 손실 복원 제거는 아직 검사하지 않는다.
- validator 자체와 wildlife 분해 파일 다수가 현재 untracked 상태이므로 넓은 Git 작업은 피하고 해당 텍스트 경로만 명시적으로 검증해야 한다.
- V18 validator는 이미 `RequireSourceContract`/`ForbidSourceContract` 방식으로 시설·캐릭터·수술 rollback-free 계약을 고정한다. wildlife도 같은 수준의 문자열 ratchet을 추가하는 것이 기존 검증 체계와 일치한다.
- 현재 wildlife 소스에는 필요한 새 심볼(`PrepareRestoreCandidate`, `PublishRestoreCandidate`, `ReplaceFreshnessValidated`, section marker)이 존재하며, 구형 `pendingSaveData`, `ApplyPendingRespawns`, `RestoreFreshness` 심볼은 검색 결과에서 사라졌다.
- `WildlifeSaveSection`의 실제 선언은 정확히 rollback-free marker를 포함하고, `WildlifeRestoreRuntime.Restore`는 활성 V18 transaction boundary가 없으면 즉시 실패한다.
- habitat save 복원은 `WildlifeHabitatPatch.FromSave`를 통하므로 exactness 판단은 해당 구현과 validator 범위를 함께 확인해야 한다. 일반 species definition 생성자의 clamp는 저장 후보 patch 복원과는 별개다.
- `WildlifeHabitatPatch` 생성자는 값을 clamp/trim하지만 strict save validation이 그 허용 범위와 canonical 문자열·중복 규칙을 먼저 강제하므로 정상 후보에서는 값 변환이 일어나지 않는다.
- patch validation은 복원 대상 Grid에서 실제 usable exterior cell 존재까지 확인한다. 후보 생성 시 같은 Grid 조건을 재확인하므로 publication 전에 세계 참조 불일치가 차단된다.
- wildlife normal roundtrip fixture가 `TryResolveSaveScenario`의 scope 출력을 discard하면서 아래에서 candidate index를 조회해 Editor 독립 컴파일이 실패했다. 세 번째 출력을 실제 `DungeonRuntimeLifetimeScope scope`로 받으면 의도한 검사가 성립한다.
- 같은 파일의 다른 `scope` 사용 지점은 모두 지역 선언 또는 out 변수와 연결되어 있어 이번 누락은 해당 메서드 한 곳으로 한정된다.
- scope 수정 후 `Assembly-CSharp-Editor.rsp` 독립 컴파일이 진단 0으로 통과했다. Unity import도 완료되어 새 ecosystem partial과 candidate에 정식 GUID가 부여됐다.
- 검증 진입점은 `RuntimeAuthorityV18Validator.ValidateOrThrow()`, wildlife EditMode 계약은 `WildlifeDebugScenarios.RunAll(true)`, PlayMode 계약은 `RunPlayModeSnapshot(true)`로 직접 호출할 수 있다.
- PlayMode 차단 ILPP 오류의 대상은 `BuildingStatePersistenceDebugScenarios` 안의 private nested `UnlistedWorkAbility`와 이를 generic base에 넣은 nested handler 한 쌍이다.
- 운영 handler들은 동일 generic base를 top-level concrete ability와 함께 정상 사용한다. 따라서 ILPP 취약점은 generic base 자체보다 Editor fixture의 private nested generic argument 형태에 국한될 가능성이 높다.
- fixture의 unlisted ability/handler/state module은 해당 시나리오 한 곳에서만 사용되며 파일은 이미 다른 top-level test module을 둔다. 세 테스트 타입을 top-level `internal`로 이동하면 행위·가시 범위를 유지하면서 Cecil nested generic 해석 경로를 제거할 수 있다.
- generic dispatcher 계약은 그대로 유지해야 하므로 handler를 비-generic으로 우회하지 않고 `BuildingAbilityWorkCompletedHandler<UnlistedWorkAbility>` 상속 자체는 보존한다.
- top-level 이동 후에도 ILPP가 같은 closed generic을 해석하지 못해 원인은 Editor-only ability를 runtime generic base에 닫는 형태 자체로 좁혀졌다. 이전의 “generic 상속 보존” 판단은 실제 Unity 컴파일 증거와 모순되어 폐기한다.
- dispatcher가 요구하는 실제 계약은 `AbilityTypes`와 `Apply(BuildingAbility, context)`뿐이다. fixture handler가 인터페이스를 직접 구현해도 미등록 concrete ability의 정확 타입 dispatch와 상태 모듈 persistence 검증 범위는 그대로 유지된다.
- unlisted generic 제거 뒤 Play 진입은 그 다음 Editor-only generic closure인 `DungeonJsonSaveSection<CharacterProgressionSavePlayModeFacade/MarkerPayload>`에서 멈췄다. Unity Jobs ILPP가 Editor assembly의 타입 인수로 runtime generic을 닫는 형태 전반에 취약하다.
- 실제 Editor fixture에서 자체 payload를 generic 인수로 쓰는 곳은 progression marker 2개, wildlife marker, surgery fail marker, invasion fail marker다. 실제 runtime DTO를 인수로 쓰는 Editor section은 이 문제 패턴이 아니므로 유지할 수 있다.
- `DungeonJsonSaveSection<T>`가 제공하는 marker fixture용 동작은 `{}` 캡처, exact section version 확인, no-op 또는 고의 실패 stage 생성뿐이다. 이 용도는 비-generic `IDungeonSaveSection`/`IDungeonStagedSaveSection` 구현으로 동일하게 표현 가능하다.
- wildlife의 fail-after-candidate section은 이미 비-generic staged section으로 구현되어 ILPP 안전한 참고 구현이다. progression과 wildlife marker부터 같은 패턴으로 전환하고 나머지 Editor-only payload closure도 일괄 제거해야 연쇄 Play 차단을 피할 수 있다.
- 공용 interface에는 staged section과 rollback-free marker가 분리되어 있다. 공용 Editor test base는 staged/preflight만 구현하고, 각 파생 fixture가 기존 의미대로 `IDungeonRollbackFreeSaveSection`을 선택적으로 선언해야 한다.
- `IDungeonSaveSectionPreflight`는 payload 문자열/버전 검증만 요구한다. marker fixture는 자체 생성한 작은 JSON만 다루므로 공용 비-generic base에서 exact version·non-empty object JSON을 검증하고 동일 `DungeonDelegateSaveRestoreStage`를 만들 수 있다.
- 공용 `DungeonDebugStagedSaveSection`을 추가하고 progression/wildlife/surgery/invasion marker·failure sections를 비-generic staged 구현으로 전환했다. rollback-free marker는 기존에 선언하던 세 fixture에만 유지했다.
- Editor 경로에 남은 `DungeonJsonSaveSection<T>` 두 건은 모두 runtime assembly DTO(`DungeonInvasionSaveData`, `DungeonSurgerySaveData`)를 사용한다. Editor-only payload type 및 unlisted generic handler 검색 결과는 0건이다.
- 실제 Play compile은 남은 `DungeonJsonSaveSection<DungeonInvasionSaveData>`에서도 ILPP 해석 실패를 재현했다. 따라서 문제 경계는 Editor assembly에서 runtime generic base를 상속하는 모든 closed type이며 DTO 출처로 구분할 수 없다.
- 남은 두 isolated typed section은 각 service/coordinator의 `Capture`, `ValidateRestore`, `Restore`만 위임한다. JSON deserialize와 staged delegate를 interface 직접 구현으로 옮기면 의미를 보존하면서 Editor generic base 상속을 0건으로 만들 수 있다.
- invasion/surgery isolated typed sections를 `IDungeonSaveSection`·preflight·staged 직접 구현으로 바꾸고 기존 typed DTO 검증과 동일 payload instance의 staged commit을 보존했다.
- Editor 경로의 runtime generic base 상속과 unlisted generic handler 검색이 모두 0건이 됐다. 다음 Unity import/ILPP 결과가 이 원인 분석의 결정적 검증이다.
- 그 다음 ILPP 대상은 `RunVariableDebugScenarios`의 private nested `TestGuestDemandEffect : IRunVariableMultiplierEffect<string>` 한 건이다. 운영 assembly의 동일 interface 구현들은 문제가 없고 Editor 구현만 실패한다.
- fixture는 runtime의 `RunGuestDemandEffect`와 중복되는 문자열 컨텍스트 multiplier를 테스트할 가능성이 높다. 사용 지점을 확인해 운영 구현 재사용 또는 비-generic test seam으로 치환할 수 있다.
- `TestGuestDemandEffect`는 두 곳에서 `TestSpecies`에 2.25배, 그 외 1배를 반환한다. `RunGuestDemandEffect("TestSpecies", 2.25f)`가 대소문자 비교까지 동일하게 구현하므로 fixture 의미 손실 없이 교체 가능하다.
- 운영 concrete effect를 재사용하면 중복 테스트 구현을 제거하고 실제 배포 코드까지 함께 검증하므로 테스트 신뢰도도 높아진다.
- test effect를 운영 `RunGuestDemandEffect`로 교체한 뒤 runtime generic 타입 14종의 Editor 사용을 교차 감사했다. 남은 22개 hit는 `SceneRuntimeRegistry<T>` 객체 생성과 validator 문자열뿐이며 Editor type 선언의 runtime generic 상속/구현은 0건이다.
- 따라서 현재 ILPP 연쇄의 알려진 source 원인은 모두 제거됐고 `RunVariableDebugScenarios.cs` import 후 전체 Editor assembly postprocess를 다시 실행하면 된다.
- 실제 다음 ILPP 실패는 `Data<int>`였지만 Editor 소스에는 closed `Data<int>` 사용이 없고 `RuntimeAuthorityV18Validator`의 `typeof(Data<>)` reflection 검사만 있다. runtime의 `GameSessionState`와 money services가 `Data<int>`를 소유한다.
- 이 결과는 ILPP 취약점이 상속/구현에만 국한되지 않고 Editor assembly의 runtime generic 타입 메타데이터 참조 전반까지 포함함을 보여준다. validator의 open generic reflection을 비-generic 판정(`FieldType.Name`/generic definition full name 문자열)으로 바꾸는 것이 다음 최소 수정이다.
- validator의 `typeof(Data<>)`를 generic definition FullName 문자열 비교로 바꿔 Editor metadata에서 Data generic 참조를 제거했다. Error message의 `Data<T>` 텍스트만 남는다.
- runtime generic cross-audit에서 실제 코드 사용으로 남은 것은 `CharacterAiEditorTestDependencies`의 `SceneRuntimeRegistry<T>` 다섯 생성뿐이다. 다음 ILPP 실행에서 문제가 되면 runtime composition helper로 묶거나 non-generic registry factory를 사용해야 한다.
- `Data.cs`는 현재 `Assembly-CSharp.rsp` 입력에 포함되고, Bee runtime DLL과 `Library/ScriptAssemblies` DLL의 SHA-256 및 길이가 정확히 일치한다. 단순 runtime DLL 복사 불일치는 아니다.
- 그러나 runtime DLL 최종 수정 시각은 wildlife 새 파일 import 시점(00:50)으로 고정돼 있고, 이후 변경은 모두 Editor 소스였다. ILPP가 runtime generic definition 자체를 못 찾는 이유를 좁히려면 Editor rsp의 참조 방식과 DLL metadata를 직접 확인해야 한다.
- Editor rsp는 runtime 구현 DLL이 아니라 `Library/Bee/.../Assembly-CSharp.ref.dll`을 참조한다. 보조 runtime csc 명령은 `-out`만 Temp로 덮고 rsp 안의 `-refout`을 덮지 않아 Bee reference assembly를 수동 컴파일 결과로 재작성했다.
- 수동 출력 파일명이 assembly identity가 되므로 Bee의 `Assembly-CSharp.ref.dll` 내부 이름이 `CodexWildlifeRuntimeImportedCheck`로 오염됐을 가능성이 매우 높다. 이것이면 Editor IL과 ILPP resolver의 Assembly-CSharp identity 불일치 및 generic resolve 연쇄를 모두 설명한다.
- binary 검사로 오염 identity를 직접 확인했고 Unity MCP runtime reimport가 정식 ref DLL을 재생성하자 ILPP 연쇄가 종료되고 PlayMode 진입이 복구됐다. 이후 보조 csc에는 반드시 Temp `-refout`도 함께 지정해야 한다.
- wildlife strict restore는 ecosystem patch JSON, actor instance replacement, candidate index cleanup, warning 0을 정상 왕복에서 확인했고, 고의 후반 실패에서는 live Grid/actors/root revision/detached candidate count를 모두 보존했다.
## 2026-08-03 exterior activity strict restore audit

- `ExteriorActivitySaveSection` is staged through `DungeonJsonSaveSection` and runtime transaction participation, but it does not declare `IDungeonRollbackFreeSaveSection`, so the registry still needs a rollback image when this section is present.
- `ExteriorActivityRuntime` delegates candidate preparation/publication to `ExteriorActivityRestoreCoordinator`; source search shows the coordinator clears and repopulates live zone/incident lists during publication.
- `ExteriorZoneMarker.RestoreState` clamps five saved fields. Whether this is lossy depends on preflight ranges; validation and candidate construction must be audited together before marking rollback-free.
- coordinator already requires an active V18 transaction, the detached facility Grid, candidate-aware building/character/wildlife references, and inactive zone objects. It publishes at ordered participant `300.world.exterior-zones`.
- publication retires every live zone before clearing/repopulating lists, then activates and registers each candidate one-by-one. This is not yet a demonstrably non-failing pointer/visibility swap and needs stronger prevalidation/publication semantics before adding the rollback-free marker.
- candidate creation catch still uses delayed `Destroy` when injection fails before the marker enters detached mode; this can leak an unpublished GameObject across a failed transaction frame and must be made synchronous.
- preflight validates all clamped zone fields against the exact same ranges and nonnegative integer bounds, so `ApplySaveData` does not change any valid saved value. Incident durations/progress and typed/canonical IDs are also validated before cloning.
- validation trims zone/incident/reference IDs for checks but does not always require the stored string itself to equal its trimmed form. Because clone/candidate state can retain whitespace even when references are looked up with trimmed strings, canonical equality must be added for exact round-trip guarantees.
- exterior zone detached lifecycle is inherited from `BuildableObject`; its publish/discard/retire implementation must be checked for synchronous cleanup and possible throwing operations before section marker promotion.
- `BuildableObject.DiscardDetachedRestore` and `RetireForWorldReplacement` both use delayed `Destroy` in PlayMode. That violates the same failed-candidate cleanup invariant already fixed for characters/wildlife and explains why exterior tests do not currently count detached leftovers.
- current exterior late-failure fixture appends a non-rollback-free fail section to the full live registry and expects `CommitCount == 2`, explicitly proving rollback-image replay rather than rollback-free publication. It must be replaced with an isolated all-marker registry and `CommitCount == 1`.
- normal roundtrip fixture compares zone IDs and instance replacement only; it should also compare exact zone/incident JSON and candidate index cleanup/warnings to prove that canonical save state survives unchanged.
- incident `Clone()` is field-for-field and only replaces null lists, which preflight already rejects. No numeric normalization occurs in incident candidate creation.
- coordinator currently sorts zones by type/ID while capture preserves live list order; this can change serialized order on a valid roundtrip. Preserving validated payload order in candidate construction is the simplest exact-state contract.
- `BuildableObject.PublishDetachedRestore` performs the same world-registry/contract publication already accepted by rollback-free facility restore. Exterior can use that boundary once candidate cleanup is synchronous and its fixture proves late-failure discard without rollback replay.
- existing V18 exterior ratchet checks transaction staging and detached publication but not the rollback-free section marker, synchronous destruction, canonical IDs, payload order, or one-commit late-failure proof.
- the production section/coordinator changes now satisfy those missing contracts; validator must make them non-regressible before Unity execution evidence is accepted.
- 첫 fixture patch가 동일한 호출 모양 때문에 invalid-preflight 메서드에 scope 변수를 넣고 normal-roundtrip에는 discard를 남겼다. compiler line evidence로 범위를 특정했으며 두 호출을 각각 원래 목적에 맞게 교정한다.
- 접객 작업 후보는 `ReceptionPoint` 하나에 한정되지 않는다. `ExteriorZoneMarker.CanRunReceptionWork`와 authored archetype 모두 `IncidentPoint`도 합법 접객 시설로 정의하므로 fixture가 첫 reception marker와 reference-equality를 요구한 것은 실제 규칙보다 좁은 잘못된 기대였다.
- 외부 사건 페이싱은 1~3일차 모든 자연 사건을 차단하고 `Thief`는 31일차부터 허용한다. PlayMode fixture는 기준 V18 저장을 캡처하고 테스트 안에서만 31일차로 전진한 뒤 사건/section 캡처를 확인하고 기준 저장을 원자 복원해야 날짜 잠금과 테스트 격리를 동시에 보존한다.
- 수정된 전체 외부 활동 PlayMode suite는 합법 접객 후보, 사건 생성/저장, invalid preflight live 보존, exact JSON 왕복, rollback-free 후행 실패 후보 정리를 모두 통과했다. 이어진 V18 authority는 772 authored items, 168 catalyst SOs, legacy authority 0으로 통과했고 Console은 Error 0 / Warning 0이다.

## 2026-08-03 physical-item reservation round-trip audit

- 전체 V18 왕복에서 달라진 것은 시작 자원 4스택의 `reservedByPersistentId="owner"`가 복원 뒤 빈 값이 되는 현상이다. 저장 DTO에는 예약 필드가 있으나 `WorldItemPersistenceService`는 restore candidate 생성 시 예약을 명시적으로 비우는 경로가 존재한다.
- 시작 자원은 `PreparedStartPartyGameplayApplier`가 `IWorldItemStackRuntime.SpawnStockAtDropoff`로 생성한다. 다음 감사는 이 호출이 넘기는 persistent owner ID와 `SpawnStockAtDropoff`의 예약 의미를 확인해, 운반 예약을 영속 상태로 둘지 capture에서 제거할지 결정해야 한다.
- 시작 보급품 호출은 예약자나 목적지 ID를 넘기지 않는 4인자 기본 overload다. 따라서 `owner` 값은 시작 파티 코드가 직접 저장한 것이 아니라 `SpawnStockAtDropoff`의 기본 목적지/드롭오프 구현 또는 후속 AI 예약에서 생긴다.
- `SpawnStockAtDropoff` 기본 overload는 loose 상태와 빈 destination으로만 스택을 만든다. 실제 `owner` 값은 생성 후 AI가 잡은 운반 예약이다.
- `WorldItemPersistenceService`는 capture 시 `reservedByPersistentId`를 그대로 DTO에 쓰지만 restore candidate에는 모든 일반 예약을 빈 값으로 만들고, combat-loadout direct pickup 예약만 source storage/loose 상태로 되돌린다. 즉 현재 구현 자체가 예약을 transient로 취급하면서도 비정규 payload를 캡처하는 단일 원인 불일치다.
- `PhysicalItemsSaveSection`은 아직 rollback-free marker가 없고 별도 preflight 인터페이스도 구현하지 않는다. section version은 exact로 검사하지만 내부 DTO는 `WorldItemPersistenceService.StageRestore`에서 null을 빈 월드로 합성하고 V1~current를 허용하며 invalid item entry를 skip한다.
- 따라서 예약 한 필드만 고치기보다 물리 아이템 owner를 current-version strict detached Aggregate로 닫는 것이 Phase 112 방향과 맞다. 최소 계약은 capture에서 transient 예약 제거, 저장 payload의 예약자 필드 금지, null/구버전/invalid entry 거부, rollback-free section marker, full exact round trip이다.
- live `WorldItemStackRecord.reservedByPersistentId`는 운반/직접 pickup 동작을 위한 런타임 필드이며 공개 저장 인터페이스의 필수 권위가 아니다. 현재 save DTO 선언은 Items 폴더 밖의 Foundation 계약에 있을 가능성이 있어 정의 위치와 버전 정책을 별도로 확인해야 한다.
- 물리 DTO는 `Assets/Scripts/Models/Items/Core/ItemPrimitives.cs`의 V6이며 예약 필드를 포함한다. 필드를 즉시 삭제하면 JSON/테스트 계약 파급이 커지므로 V18에서는 필드는 남기되 canonical 값은 빈 문자열로 고정하고 non-empty payload를 preflight에서 거부하는 방향이 안전하면서도 단일 권위 원칙에 맞다.
- StageRestore의 lossy 경로는 예약 외에도 null snapshot→빈 월드, V1~V5 migration, null stack list→empty, invalid entry skip, enum fallback, contamination clamp, legacy waste/component 합성, null hauling settings→default, component/value null filtering·trim·schema clamp까지 포함한다. strict V6 validator가 이를 선행해 정상 후보 변환이 필드 값을 바꾸지 않도록 해야 한다.
- commit은 hauling settings restore 후 repository state pointer replacement 두 단계다. rollback-free 선언 전 `ItemHaulingSettings` 복원이 검증 후 비실패인지, repository replacement가 단순 swap인지 확인하고 capture/restore preflight를 공용 validator로 묶어야 한다.
- `ResourceItemHaulingSettingsProvider.Restore`는 shared Aggregate root store에 새 runtime component를 replace하고, repository도 같은 root store에 detached state를 replace한다. V18 transaction staging 중에는 live root가 아니라 candidate root를 바꾸므로 section은 strict preflight 이후 rollback-free participant가 될 수 있다.
- hauling multiplier는 provider가 1..2.5 범위·0.05 단위로 반올림한다. save validator가 finite/range/step canonicality를 먼저 요구하면 restore의 `Normalize()`가 유효 payload를 바꾸지 않는다. null settings/default fallback은 거부해야 한다.
- 기존 `PhysicalItemDebugScenarios.VerifyRestoreReleasesTransientReservations`는 noncanonical payload에 예약자를 직접 넣고 restore가 조용히 비우기를 성공 조건으로 삼는다. strict V18에서는 이 테스트를 “live 예약은 capture에서 제외됨”과 “non-empty saved reservation은 preflight 거부·live 보존” 두 계약으로 교체해야 한다.
- 공용 `CreatePileSnapshot`에도 facility buffer 예약자가 박혀 있어 strict 전환 시 다른 pile/selection/roundtrip fixture를 함께 canonical payload로 바꿔야 한다.
- `ItemStackId`/`ItemInstanceId` 생성자는 trim 정규화하므로 strict validator는 `saved == typed.Value` equality도 검사해야 whitespace ID가 lookup 중 조용히 정규화되지 않는다. 기존 fixture의 `stack:*` 형식은 타입 계약상 유효하다.
- combat-loadout 예약은 단순 예약자 필드 제거만으로 부족하다. capture 시 저장된 source storage로 복귀한 durable 상태(state/destination/source/destination-position)를 출력해야 restore가 추가 정규화 없이 exact 후보를 만들 수 있다. 일반 운반 예약은 destination을 유지하고 예약자만 제외한다.
- Physical fixture는 `IWorldItemStackRuntime.TryReserveStoredItemForDirectPickup`을 공개 API로 이미 노출하므로, canonical snapshot을 restore한 뒤 실제 예약을 생성하고 capture 결과가 durable source 상태인지 검증할 수 있다. 반면 invalid payload 보존 검사는 section/preflight 수준에서 별도로 두는 편이 맞다.
- 최근 strict owner들은 typed `DungeonJsonSaveSection<T>`의 `ValidatePayload`를 공용 runtime validator에 연결하고 `IDungeonRollbackFreeSaveSection`을 선언한다. 물리 section은 custom staged 경계를 유지하더라도 동일한 explicit preflight+marker 계약을 구현해야 registry의 all-marker 원자 경로에 참여한다.
- registry 계약상 typed DTO section은 `IDungeonSaveSectionPreflight`를 구현해야 하며, staged commit은 모든 preflight/staging이 끝난 뒤 실행된다. 물리 section은 현재 이 preflight가 누락돼 있으므로 JSON deserialize→strict validator를 공용 helper로 만들고 StageRestore에서도 같은 helper를 재사용해야 한다.
- 프로젝트의 strict validator 관례는 DTO를 직접 변경하지 않고 `DungeonGameRestoreReport`에 모든 오류를 누적한다. direct runtime restore에는 같은 검증을 실행한 뒤 실패를 예외로 승격하는 thin wrapper가 필요하다.
- `ItemInstanceComponentSaveData.Clone`은 null을 제거하고 component ID/key를 trim하며 schema를 최소 1로 올린다. strict validator가 null/빈·비정규 ID/key, invalid kind, non-finite decimal, schema<1을 거부하면 candidate clone은 유효 입력을 바꾸지 않는다.
- physical capture는 stacks만 y/x/item ID로 정렬하고 tie-breaker stack ID가 없으며 unique item dictionary는 정렬하지 않는다. exact deterministic JSON을 위해 stack ID와 unique instance ID 정렬을 추가하고 validator도 그 canonical order를 확인해야 한다.
- `IDungeonItemCatalogProvider.TryGetDefinition`이 있으므로 strict validator는 unknown item을 예외 의존 없이 report에 누적하고, MaxStack/unique item 규칙까지 실제 authored/test 카탈로그 기준으로 검사할 수 있다.
- Physical fixture에는 simple `CharacterActor`와 test character-ID registry 조립이 이미 존재한다. 일반 운반 예약은 실제 runtime API로 생성해 capture omission을 검증할 수 있고, direct-pickup durable 복귀는 별도 warehouse-backed fixture가 필요할 수 있다.
- `TryReserveBestHaulJob` requires a real downstream destination, so a pile-only fixture may not produce a reservation. The lower-level `ItemReservationService` or an existing registered test warehouse should be used rather than fabricating DTO reservation state.
- `ItemReservationService.TryReserve` is the actual runtime mutation boundary and only needs the repository plus a null marker presenter. Physical fixture can reserve an existing canonical stack through this production service, prove live state is reserved, then prove `Capture()` emits an empty reservation without DTO fabrication.
- Editor fixture already provides singleton `EditorNullItemMarkerPresenter.Instance`, and `WorldItemStackRuntime` itself implements `IPhysicalItemRestoreStaging`, so strict section/preflight tests need no new mock surface.
- 구현 후 Physical item 전체 Editor 계약이 Unity 실제 컴파일/실행에서 통과했다. 따라서 transient reservation omission과 invalid payload 무변경 거부는 단위 수준에서 증명됐고, 남은 결정적 증거는 실제 start-party live world의 전체 V18 save→restore signature 일치다.
- 실제 start-party PlayMode 전체 V18 왕복도 54개 section, physical stacks 6→6, item signature diff 0으로 통과했다. 이로써 예약은 런타임 운반 capability의 transient 상태이고 물리 Aggregate의 durable 저장 권위에는 포함되지 않는다는 계약이 live 증거로 확정됐다.
- 기존 `RuntimeAuthorityV18Validator`에는 physical section/persistence strictness ratchet이 없고 장비 runtime이 repository를 직접 clear하지 않는지만 검사한다. 새 validator/rollback-free marker/capture omission/legacy migration 부재를 source contract로 고정해야 회귀 방지가 된다.

## 2026-08-03 remaining rollback-image owner inventory

- Unity runtime reflection 기준 public production save section 47개가 아직 `IDungeonRollbackFreeSaveSection`을 선언하지 않는다. 이미 strict detached Aggregate로 전환된 captivity/circus/invasion 등도 marker만 누락된 경우가 포함되어, “미전환 47개”가 모두 같은 작업량을 뜻하지는 않는다.
- 남은 목록은 전투 7, 산업 인프라 4, 경제/운영 다수, run/foundation, 생존/환경, 연구/메타/디버그 등이다. 다음 단계는 section별 staged commit이 candidate aggregate만 쓰는지와 publication side effect가 있는지를 분류해 안전한 marker-only 군과 추가 구현 필요 군을 분리하는 것이다.
- Captivity/Circus/Invasion section은 이미 typed strict validation과 detached runtime restore를 호출하지만 marker 선언이 실제로 빠져 있다. 이전 late-failure/invalid-preflight 증거가 있는 이 세 개는 우선 marker-only 전환 후보이며 validator ratchet도 현재 strict 호출만 검사하고 marker를 요구하지 않는지 확인해야 한다.
- V18 validator는 세 section에 대해 typed boundary와 coordinator participant만 요구하고 rollback-free marker는 요구하지 않는다(수술만 요구). 세 marker와 ratchet을 추가한 뒤 `CaptivityCircusDebugScenarios`, invasion threat/intruder/combat/defense suites로 회귀하면 이미 증명된 candidate publication 의미를 코드 계약에 반영할 수 있다.
- 세 marker와 ratchet 추가 후 모든 captivity/circus/invasion/defense 관련 suite 및 V18 authority가 통과했다. reflection 기준 rollback-image 의존 section은 47개에서 44개로 줄었으며, 이 목록을 진행률의 권위 있는 수치로 사용한다.

## 2026-08-03 combat save-owner audit

- combat의 남은 7개 중 equipment evolution과 body health는 custom section에서 V1/V2 migration, warning, null→default를 수행하므로 marker-only 전환 대상이 아니다. CombatEquipment도 null JSON→default를 허용하고 explicit preflight가 없다.
- CharacterMedical, DefenseTactical, EquipmentMaintenance, CharacterCombatCommand는 공용 typed preflight를 사용한다. 다음 감사는 각 runtime `Restore`가 candidate Aggregate root만 교체하고 projection을 published revision 뒤로 미루는지 확인해 marker-only 가능 여부를 결정한다.
- DefenseTactical와 EquipmentMaintenance restore는 strict validator 뒤 shared Aggregate root slot만 replace한다. DefenseTactical의 추가 변경은 rebuildable read-view cache invalidation뿐이며 durable live state를 쓰지 않는다.
- CharacterMedical과 CharacterCombatCommand는 각각 restore transaction participant를 갖고 active V18 transaction과 detached world references를 검증한 뒤 Aggregate candidate를 준비한다. CombatCommand publication은 actor AI pause/presentation projection을 적용하므로 prevalidation/non-failing publication 증거를 기존 tactical PlayMode 회귀와 함께 확인해야 한다.
- CharacterMedical candidate preparation의 lifecycle/order 정규화는 detached candidate character와 새 Aggregate state에 적용되고, downed Grid occupants도 candidate Grid에만 등록된다. publication은 검증된 registration/reservation projection 교체이며 이전 tactical/medical PlayMode suite가 late-failure 보존을 이미 확인했다.
- combat Editor의 공용 `CombatSystemDebugScenarios.RunAll(true)`가 네 marker 후보의 broad regression 진입점이다. 세 legacy/lossy owners(equipment/evolution/body-health)는 이번 marker 묶음에서 제외한다.
- 네 combat marker 추가 후 full CombatSystem contracts와 V18 authority가 통과했고 reflection count는 44→40이다. CharacterMedical/DefenseTactical/EquipmentMaintenance/CharacterCombatCommand는 이제 rollback-image 의존 목록에서 제거됐다.

## 2026-08-03 economy owner triage

- AnimalHusbandry, CropPlot, GrandProject save sections는 exact section version만 검사하고 empty/null JSON을 새 default DTO로 합성하며 공용 preflight도 없다. runtime이 Aggregate를 쓰더라도 현재 section 경계는 lossy이므로 marker-only 전환하면 안 된다.
- 남은 owner 중 공용 `DungeonJsonSaveSection<T>` 기반인 FacilityShop/RegularCustomer/StaffDiscontent/OperatingDay/EventAlert/Codex/ServiceRooms/Meta/RunVariable/Research/RandomStream부터 runtime publication 안전성을 감사하는 편이 marker-only 후보를 더 정확히 고를 수 있다.
- FacilityShop/RegularCustomer/StaffDiscontent는 typed base를 쓰지만 null list를 empty로 허용하거나 restore에서 invalid record를 skip/trim하며, StaffDiscontent는 별도 ValidatePayload조차 없다. 이 세 개도 marker-only 전환 대상이 아니다.
- OperatingDaySettlement와 EventAlert는 별도 strict validation을 공용 typed preflight에 연결한다. OperatingDay는 이전 detached Aggregate late-failure proof가 있어 우선 marker 후보이고, EventAlert save service의 restore state ownership을 추가 확인해야 한다.
- 두 operation save service 구현은 Operation 폴더가 아니라 Infrastructure의 `OperatingDaySettlementSaveService.cs`와 `EventAlertSaveService.cs`에 있다. section만 보고 소유권을 판단하지 않고 해당 Restore 구현을 기준으로 marker를 결정한다.
- OperatingDay/EventAlert service Restore는 validator 뒤 runtime state replace를 호출하지만 service 내부에는 source null→default fallback이 남아 있다. Registry preflight에서는 section validator가 null을 거부할 수 있으나 direct service 경계는 아직 strict하지 않다.
- 더 근본적으로 공용 `DungeonJsonSaveSection<T>.StageRestore`가 empty/null JSON과 null migration 결과를 새 DTO로 합성하고, `Capture()`도 null payload를 default로 바꾼다. `ValidatePayload`만 strict라 registry 정상 경로는 보호되지만 direct section.Restore와 fixture 경로가 우회한다. marker 확대 전에 이 공용 typed boundary를 strict deserialize/capture로 고치는 것이 20여 section을 동시에 정상화한다.
- strict base 변경 뒤 save registry suite의 유일한 실패는 aggregate-candidate commit failure 계약이다. 다른 dependency/staging/rollback/participant 계약은 통과했으므로 JSON strictness 전반이 아니라 해당 fixture section의 payload/marker 구성과 registry branch 선택을 조사해야 한다.
- 실패 fixture는 typed base를 사용하지 않는 두 직접 test section(`AggregateTransactionFakeSection`, `TransactionFakeSection`)으로 구성된다. 따라서 strict JSON base 변경이 직접 원인일 가능성은 낮고, 최근 production marker 증가가 TypeCache/registry 전역 판단에 간접 영향을 줬는지 또는 fixture rollback image 캡처가 strict owner 상태를 읽다가 실패했는지 registry report를 상세화해야 한다.
- 상세 진단 결과 durable root/last 값은 10/30으로 정확히 복원됐지만 `PublishedRestoreRevision`만 1이었다. 두 fake section이 rollback-free marker가 아니므로 registry가 의도대로 rollback image를 재공개한 결과이며, fixture의 “candidate discard/revision 0” 기대와 구성 자체가 모순된다.
- 이 계약은 aggregate fake와 “live 값을 건드리기 전에 실패하는 rollback-free fail section” 두 개로 구성해야 all-marker branch를 실제로 타고 revision 0/candidate discard를 증명한다. 기존 non-marker TransactionFake는 별도의 rollback-image 회귀에 유지한다.
- fixture를 실제 all-marker 구성으로 교정하자 strict typed base와 save registry 전체 suite가 통과했다. 공용 typed section은 이제 capture null, empty/invalid JSON, deserialize null, migration null을 default DTO로 숨기지 않는다.
- EventAlert runtime restore constructs a fresh `EventAlertAggregateState` and validates record identity again before replacement, so its durable state path is candidate-root friendly; remaining publication/UI cache mutations must be checked later in the method.
- OperatingDay runtime rebuilds a fresh Aggregate but uses clamps and conditional skips. Its section validator is intended to make these no-ops for valid payloads; exact range/list validation and final replacement path must be checked before adding the marker.
- EventAlert defers presentation rebuilding when `aggregateRootStore.IsRestoreStaging` and follows `PublishedRestoreRevision` in Update, so staged commit changes only the candidate root. Its validator requires the record list.
- OperatingDay validator requires mood/history and all nested lists/ranges; previous atomic fixture proved candidate-root late-failure preservation. The runtime's clamp/filter steps are no-ops for validated payloads, and final state writes only `RequireAggregateRoot().Replace(restored)`. Both operation sections are marker candidates once direct service null→default fallback is removed.

## 2026-08-03 foundation/run typed owner audit

- RunVariable still accepts V1, mutates payload during migration, warns/skips missing runtime/definitions, and defaults nested start/list state. It requires a real strict-current rewrite before marker promotion.
- RandomStream is a small candidate-root owner with existing failed-restore/live-handle registry tests, but validator currently treats null streams as empty, trims IDs, accepts noncanonical numeric strings, and capture does not explicitly sort. It can be fully strictified locally, then marked rollback-free and covered by the existing save-section suite.
- Codex, MetaProgression, ServiceRooms remain lossy: Codex permits null lists and skips invalid entries; Meta has no validator and merges/defaults nested state rather than replacing an exact Aggregate; ServiceRooms has no validator and defaults null payload/contract state.
- ExperiencePacing and ExternalInfluence are optional sections with missing-data fallback, version migration, warnings, and default DTO/reset behavior. They cannot be marked rollback-free until V18 makes them required current-version sections or models missing state as an explicit canonical payload.
- Codex runtime state already lives in `CodexAggregateState`; `ReplaceStateFromRestore` deep-clones into the shared candidate root when present and has no live projection side effect. Codex can be promoted after tightening its DTO lists/order/text/source validation and removing restore skips/defaults.
- Codex domain regression failures came from `CodexScenarioWorld.CreateFacility` constructing BuildableObjects without the now-mandatory `BuildingInstanceId`. The correct fixture fix is `RestorePersistentIdentity` with a typed `building:*` ID before `Initialization`, not reintroducing name-based runtime fallback.
- typed facility ID fixture fix 후 Codex 전체 domain contracts와 V18 ratchet이 통과했다. Codex는 strict canonical DTO→fresh CodexState→candidate Aggregate deep clone 경계로 rollback-image 목록에서 제거할 수 있다.
## 2026-08-03 V18 continuation resynchronization

- Phase 112의 현재 미완료 계약은 남은 Unity-object 저장 소유자를 strict detached/rollback-free 경계로 바꾸고 Registry rollback image 의존성을 제거하는 것이다.
- 작업 주문·건설 현장과 물리 아이템 V6 strict 전환 및 54-section live V18 왕복은 완료 증거가 있으며, 다음 판단은 기억상의 36개가 아니라 Unity TypeCache가 산출하는 실제 non-marker production section 수를 기준으로 한다.
- 계획/발견/진행 파일과 대규모 diff 통계를 한 호출에 합치면 출력이 절단되므로, 이후 owner 감사는 파일별 소범위 조회와 2-view 기록 규칙을 유지한다.

## 2026-08-03 remaining rollback-image owner recount and first candidates

- Unity `TypeCache`를 null namespace와 Editor assembly를 명시 처리해 다시 계산한 결과 non-rollback-free public production save section은 정확히 36개다.
- `DefenseFacilitySaveSection`과 `FactionSaveSection`은 둘 다 optional staged section이며, null/empty JSON을 기본 DTO로 합성하고 missing section도 기본/무변경 상태로 허용한다. 현재 상태로는 strict V18 marker 후보가 아니다.
- 두 runtime은 이미 각각 `DefenseFacilityAggregateState`/`FactionAggregateState` 형태의 plain-state 소유자를 사용하지만 `Restore`가 clamp/default/skip을 수행하는지와 실제 root publication이 단순 교체인지 추가 확인해야 한다. section strictification만으로 충분하다고 가정하지 않는다.
- Defense restore는 valid state dictionary를 새 `DefenseFacilityAggregateState`에 담아 `aggregateRootStore.Replace`만 수행한다. 현재 lossy 지점은 null/blank entry skip, duplicate last-write, condition clamp이며 strict validator가 이를 모두 사전 거부하면 publication 자체는 candidate-root 교체다.
- Faction restore도 새 `FactionAggregateState`를 교체하지만 authored catalog/world에서 default state를 먼저 생성한 뒤 저장된 faction만 덮어쓰고 day/sequence clamp, null skip을 수행한다. exact V18 계약으로 승격하려면 authored faction 전체성·route canonicality까지 검증하고 valid payload에서는 restore가 값과 순서를 바꾸지 않는다는 증거가 필요하다.
- 두 section의 missing/default 경로는 V17 이하 비호환 및 V18 required-section 원칙과 충돌하므로, rollback-free marker만 붙이는 대신 optional 인터페이스/StageMissing 경로 제거 여부를 기존 strict section 패턴과 대조한다.
- 기존 strict `CodexSaveSection`은 공용 `DungeonJsonSaveSection<T>`를 사용해 exact current DTO version, non-null payload, `ValidatePayload`, staged restore를 공유하고 marker만 추가한다. Defense도 같은 경계로 옮기는 것이 중복 custom section 로직을 유지하는 것보다 현재 아키텍처와 일치한다.
- Defense DTO는 version, 정렬된 facility state 목록, typed enum/수치/접근그룹/허용 ID/growth/blocked reason을 저장한다. strict validator는 DTO version, canonical facility ID와 order/uniqueness, building/cell reference, enum/finite/range/nonnegative fields, flags, canonical allowed IDs/growth/text를 검증해야 한다.
- 기존 Defense debug suite에는 save section 자체의 strict round-trip/invalid preservation 케이스가 보이지 않는다. section 변환과 함께 공용 save registry 또는 해당 suite에 public invalid-preflight live-state preservation 증거를 추가해야 marker가 단순 선언에 그치지 않는다.
- `BuildingInstanceId`는 입력을 trim한 뒤 `building:` prefix만 확인하므로 validator는 `saved == typed.Value`까지 비교해야 공백 ID를 조용히 정규화하지 않는다.
- Defense growth는 여섯 개의 nonnegative integer level이고 state에는 runtime-only처럼 보이는 blocked text도 저장된다. 모든 valid payload를 그대로 보존하려면 growth null/default 합성과 blocked text trimming을 금지하고 null/비정규 값을 reject해야 한다.
- 기존 defense fixture는 실제 시설·물리 아이템·전력·이벤트를 조립하는 `DefenseScenarioWorld`를 이미 가진다. 새 저장 검증은 이 world의 실제 runtime/root를 재사용해 capture → strict section restore와 invalid preflight 무변경을 증명하는 방향이 적합하다.
- 실제 fixture에는 Defense runtime 생성자가 없고 composition에서만 VContainer가 조립한다. section 단위 검증은 작은 `IDefenseFacilityRuntime` fake로 strict preflight 호출 여부와 restore 무변경을 증명하고, 실제 runtime의 root replacement는 기존 gameplay suite + 별도 capture/restore 검증으로 보강한다.
- runtime `Clone`은 null allowed-ID list와 null growth/text를 각각 empty/default로 합성한다. validator가 valid payload에서 이 null을 전부 거부해야 clone이 값 손실 없이 작동한다.
- `DoorAccessGroup.All`은 bit 0..6의 유일한 허용 마스크다. `allowedGroups & ~All == 0` 검증이 필요하며 개별 allowed ID는 `CharacterId`가 prefix를 강제하지 않으므로 nonblank/canonical/sorted/unique 계약으로 제한한다.
- 공용 typed base는 section version만 exact 검사하므로 Defense validator가 DTO 내부 `version == CurrentVersion`도 별도로 강제해야 한다.
- `SetAllowed(persistentId)`는 trim/중복 제거 후 list 끝에 추가하므로 live 허용-ID 순서는 결정적이지 않다. Capture/Clone에서 ordinal sort하고 validator는 strict ascending order를 요구해야 exact JSON과 deterministic save가 함께 성립한다.
- Defense `Clone`은 capture와 restore 두 곳에서만 사용된다. allowed-ID 정렬을 clone에 넣으면 capture는 canonical해지고, validator가 이미 정렬된 payload만 허용하므로 valid restore 값은 변하지 않는다.

## 2026-08-03 defense-facility strict cutover result

- `defense.facilities`는 required typed section으로 전환되어 empty/null/legacy/default/missing payload를 더 이상 합성하지 않으며, strict validator를 통과한 DTO만 candidate Aggregate root에 교체한다.
- capture는 facility ID와 allowed-character ID를 ordinal canonical order로 기록한다. invalid condition, unordered IDs, unknown flags, null growth/list/text, malformed typed IDs는 commit 전에 실패한다.
- 기존 Defense fixture도 production의 필수 building identity 계약에 맞춰 deterministic `building:defense-fixture:*` ID를 initialization 전에 받는다.
- Unity MCP에서 Defense 전체 suite, 신규 canonical round-trip/invalid no-mutation fixture, V18 authority가 통과했고 Console Error 0 / Warning 0이다. non-rollback-free production section count는 36에서 35로 감소했다.
## 2026-08-03 faction strict restore audit

- Authored faction catalog is already sorted by stable ID and runtime `Factions` capture is sorted by faction ID. A strict payload can therefore require the faction list to exactly match authored definitions in count and order.
- Routes are created as `faction-route:{++routeSequence}` and appended in sequence order. Capture currently preserves this order, so validator can require canonical route IDs, strict ascending sequence, uniqueness, and `max(route sequence) <= routeSequence`.
- Current restore clamps day/sequence, seeds authored defaults, skips null/unknown factions, accepts null routes, and JSON-clone defaults malformed nested state. These are all lossy paths that must be rejected before restore; valid payload may then reuse the existing root replacement.
- Route payload includes faction/type/status/path/index/progress/delay/strength/days/flags/reinforcement actor IDs/cargo. Strict validation must cover canonical faction references, enums, nonempty path and index bounds, finite progress/delay, strength/range/day relations, sorted unique actor IDs, concrete authored item IDs and positive cargo amounts.
- `IFactionRuntime` already exposes authored `Definitions`, so validator can check exact faction coverage without adding a new runtime method. `FactionSaveSection` can inject the already-registered `IDungeonItemCatalogProvider` to reject unknown cargo item IDs.
- Save section은 production 외 직접 생성되는 곳이 없어 constructor에 item catalog를 추가해도 composition 외 호출 파손은 없다. Faction/Species/Defense expansion validator가 기존 authored faction asset 검증 entry이므로 strict save fixture를 그 suite에 추가하는 것이 적합하다.
- Runtime invariants show trust is bounded `[-100,100]`; betrayal/restitution/unpaid/death/equipment counters and embargo days are nonnegative. Home coordinates are legitimate signed hex coordinates.
- Route strength is `[0,100]` because ambush can reduce it to zero and mark the route Lost. Segment progress is finite `[0,1)`, delay is finite/nonnegative, path index must address a non-null nonempty path, created/ETA days are positive with ETA not earlier than creation.
- Reinforcement actor IDs are deterministically `{routeId}:ally:{index}` in insertion order. Validator can require canonical exact prefix, contiguous positive suffix order and uniqueness; cargo item lookup can use `EditorItemCatalogFactory` in the fixture and the injected production item catalog at runtime.

## 2026-08-03 faction strict cutover result

- `world.factions`는 required typed/rollback-free section이 되었고 authored faction 정의 전체를 exact order로 저장하지 않거나 route/day/sequence/nested state가 비정규이면 commit 전에 실패한다.
- route cargo는 injected `IDungeonItemCatalogProvider`의 concrete authored item만 허용하고, capture는 numeric route sequence로 결정 정렬된다. valid restore는 기존 candidate `FactionAggregateState` root 교체만 수행한다.
- Expansion fixture가 canonical round-trip과 reversed faction/unknown cargo invalid no-mutation을 검증한다.
- Unity MCP에서 species/faction/defense expansion 전체, 168 research 검사, V18 authority가 통과했고 Console Error 0 / Warning 0이다. non-rollback-free production section count는 35에서 34로 감소했다.
## 2026-08-03 world-resource restore audit

- `economy.world-resources` is a custom staged section that accepts blank/null JSON as a default DTO and has no preflight/marker.
- Runtime capture already sorts nodes by node ID and sources by work-type ID. Restore clones the Aggregate root but accepts null payload/list, skips unknown/null node/source/recipe references, and clamps completed work/remaining cycles.
- If resource nodes are not initialized, restore stores a cloned pending DTO in the candidate root; after initialization the same permissive `ApplyRestore` path projects it. Strict validation therefore needs both syntax/catalog checks and exact correspondence to the current or deterministically rebuildable node/source set before marker promotion.
- Aggregate clone retains Unity `WorldResourceNode`, Grid and wildlife patch references. Publication is still root replacement, but it shares existing scene objects rather than preparing new ones; marking rollback-free is safe only if commit cannot call scene mutation and later projection consumes a fully validated pending DTO.
- Public runtime exposes only node objects plus capture. Validating against `runtime.Capture()` would reject a legitimate incoming node set if restore occurs before deterministic world-resource initialization, while accepting unknown topology would preserve skip/clamp loss. This owner needs an explicit topology catalog/candidate-aware validator and is not the next low-risk marker conversion.
## 2026-08-03 crop-plot restore audit

- Crop plots have the same topology timing problem as world resources: custom section fabricates defaults, runtime clones an Aggregate containing live `BuildableObject` references, synchronizes against building world during restore, stores pending DTO, then skips/clamps nested values.
- Existing crop fixture even mutates `growthHours=999` and relies on runtime normalization/ticking, so strict conversion would require separating gameplay mutation tests from save-contract tests plus candidate building topology validation. It is not a marker-only candidate and is deferred behind simpler plain-state owners.
## 2026-08-03 grand-project restore audit

- `economy.grand-projects` custom section fabricates empty DTOs for blank/null JSON, but runtime state is a plain `GrandProjectAggregateState` and publication ultimately replaces one Aggregate root.
- Runtime restore silently drops unknown active/completed project IDs, clamps work, trims/defaults destination, normalizes no-active state, deduplicates completed IDs, and defaults null status/list/state. These can all be moved to a strict preflight.
- Runtime already exposes its six project definitions and Capture emits one state DTO. A validator can require exact DTO version, non-null state/list/text, canonical known project IDs, sorted unique completed IDs, active/completed exclusivity, canonical destination formula, finite work within the active definition's required work, and zero/empty inactive state.
- No Unity object needs to be created or mutated during restore; next-evaluation/version live in the candidate Aggregate and state replacement is plain. GrandProject is a suitable next rollback-free conversion.
- Active destination is canonically `grand-project:{activeProjectId}` (the project ID itself already begins with `grand-project:`); inactive state requires empty destination and zero work. Completion clears active state and appends a known project ID.
- Existing production-economy fixture constructs `GrandProjectSaveSection` directly and already proves a normal round-trip. It can be extended in place with marker/preflight/required checks and an invalid duplicate/negative-work payload no-mutation assertion without new composition dependencies.
- Completed project insertion order reflects gameplay completion order, so Capture must sort it ordinal before validator requires strict order; completed membership semantics are order-independent.
- Unity `Library/ScriptAssemblies` and Bee runtime DLLs have identical SHA and contain the new `GrandProjectSaveValidation` symbol, while the live AppDomain still reports the old GrandProject section base/interfaces. The remaining issue is assembly reload timing/locking, not source compilation.
## 2026-08-03 grand-project strict cutover result

- `economy.grand-projects` now uses the shared typed JSON preflight boundary and publishes only a validated plain Aggregate root. It is required, rollback-free, and not optional.
- Unity's loaded AppDomain confirms the new generic base and interfaces; the prior mismatch was caused by Editor fixture CS8121 errors, not by the runtime DLL.
- `ProductionEconomyDebugScenarios.RunAll()` and V18 authority validation pass. The non-rollback-free production section count is now 33, and Unity Console reports Error 0 / Warning 0.
## 2026-08-03 stock-policy / regional-contract restore audit start

- `economy.stock-policies` and `economy.regional-contracts` are separate custom staged sections over plain runtime contracts, both still outside rollback-free publication.
- Their DTOs live together in `ResourceEconomyPlanningModels.cs` at schema version 1. They are promising paired candidates because they have no obvious Unity-object ownership at the save-section boundary; exact runtime normalization and catalog-reference behavior still needs inspection before conversion.
- Both current sections synthesize empty/default DTOs for blank, null, or malformed payloads. Both runtimes replace a plain Aggregate root, but restore currently performs lossy normalization.
- Stock-policy restore skips null/unknown items, normalizes thresholds/status, overwrites duplicate item IDs, and then fabricates defaults for every catalog item. A strict contract should instead require exact authored item coverage in canonical order, unique concrete IDs, valid enum, nonnegative ordered thresholds, and non-null status.
- Regional-contract restore clamps day/sequence, skips malformed entries, then calls `EnsureOffers`, which can mutate the restored candidate by expiring or generating offers. Strict restore must validate a canonical complete snapshot and publish it without generating new content during restore.
- Stock-policy runtime state is entirely plain data. Capture emits `policyView`, whose refresh path is the natural place to confirm deterministic ordering; restore can safely replace only the Aggregate root once preflight guarantees exact catalog coverage and canonical values.
- The existing shared `DungeonJsonSaveSection<T>` plus a small domain validator matches the proven GrandProject cutover pattern and avoids duplicating JSON/default/version logic.
- `ResourceStockPolicyRuntime.RefreshView` currently sorts by localized display name, which is unsuitable for canonical persistence. The persisted view should be ordered by immutable item definition ID; presentation can apply its own localized sort.
- `IsKnownPolicyItem` still accepts `PhysicalItemIds.TryGetStockCategory`, retaining an abstract stock-category backdoor. Strict SO authority requires concrete catalog item IDs only.
- There are no direct constructor call sites for `ResourceStockPolicySaveSection`; production composition can inject the content catalog when the strict section gains that required dependency. The production-economy Editor fixture is the right place for an explicit fake-runtime boundary test.
## 2026-08-03 stock-policy strict cutover result

- `economy.stock-policies` is now a required typed/preflight/rollback-free section. Its payload must contain every authored resource item exactly once in immutable item-ID order.
- Restore no longer skips unknown/null entries, normalizes thresholds, fills missing definitions, or accepts abstract stock-category IDs. It replaces only the validated plain Aggregate root.
- The production-economy Unity fixture proves canonical full-catalog round trip and invalid-threshold no-mutation. V18 authority passes with 772 authored items and the non-rollback-free section count is 32; Console Error 0 / Warning 0.
## 2026-08-03 regional-contract strict design

- Contract IDs are generated as `contract:{offeredDay}:{sequence}`; accepted deliveries use `regional-contract:{contractId}`. These formulas provide a canonical validator without depending on display strings.
- Runtime history is capped at 24 and newly generated contracts are append-only by offered day/sequence. Capture should explicitly sort by parsed numeric ID parts, while restore should clone the validated list verbatim and must not call `EnsureOffers`.
- Valid snapshots require positive days/sequence/reward/requirements, `nextOfferDay >= currentDay`, unique canonical contract IDs, `nextSequence` greater than every saved sequence, concrete catalog item requirements, valid status, canonical text, and status-appropriate destination identity.
- `Offered` and `Declined` contracts have no destination; `Accepted`, `Delivering`, `Completed`, and `Failed` contracts may carry the canonical delivery destination because completion/failure does not clear that field.
## 2026-08-03 regional-contract strict cutover result

- `economy.regional-contracts` is now required, typed/preflight, and rollback-free. Restore publishes only the validated plain contract Aggregate and never generates/expires offers as a side effect.
- Contract snapshots are canonical by offered day and numeric sequence; IDs, schedule, reward, status-specific destination, text, history bound, next sequence, and one/two concrete authored item requirements are validated before publication.
- The production-economy Unity fixture proves canonical nested round trip and invalid-destination no-mutation. V18 authority passes and the non-rollback-free section count is 31; Console Error 0 / Warning 0.
## 2026-08-03 next rollback-free candidate triage

- Remaining nearby economy/recruitment owners include RegularCustomer (1,057-line runtime), FacilityShop (1,086-line runtime), and TreasuryEconomy. The first two likely couple to authored/customer or facility presentation state and need broader audits.
- TreasuryEconomy has a small dedicated save boundary and is the next low-risk candidate to inspect before taking on the larger customer/shop owners.
- TreasuryEconomy is not actually a small semantic owner: one section aggregates ledger, employment, procurement, paid facilities, overclock, and treasury defense through six runtimes. Each `PopulateRestoreState` currently has its own normalization/reference rules, so it needs a composite validator rather than a marker-only conversion.
## 2026-08-03 regular-customer strict audit

- The section already uses the shared typed JSON base and runtime state replacement is a plain Aggregate; it is missing rollback-free marking and strict canonical validation.
- Restore constructs `RegularCustomerRecord`, whose constructor clamps satisfaction/visit count, fills blank display/species text, promotes boolean status hierarchy, and converts `RecruitCapability.None` to `All`. Preflight must reject every payload that would trigger those normalizations.
- Capture ordering currently uses default string comparison and must be changed to `StringComparer.Ordinal`. Payload IDs must be canonical, unique, and strictly ordered.
- Existing recruitment fixture has no save-boundary coverage, but it already creates/destroys a real `RegularCustomerRuntime` safely. A small fake `IRunCharacterCatalog` can reuse authored/test `CharacterSO` references without adding production dependencies.
- `RegularCustomerSaveSection` has no direct constructor call sites outside composition, so adding a marker and DTO version does not break manual production construction.
## 2026-08-03 regular-customer strict cutover result

- `recruitment.regular-customers` now carries an exact V1 DTO version and is typed/preflight/rollback-free. IDs are ordinal-canonical and source definitions, statistics, display data, recruitment hierarchy, and capabilities are validated before Aggregate replacement.
- Restore no longer skips missing records or relies on the record constructor to clamp/default/promote state. CharacterSO references remain immutable authored definitions; no scene actor is created or mutated during publication.
- Full RegularCustomer gameplay scenarios plus real-runtime canonical/invalid save coverage pass in Unity. V18 authority passes, non-rollback-free count is 30, and Console Error 0 / Warning 0.
## 2026-08-03 facility-shop strict audit

- FacilityShop already owns one plain Aggregate of offer day plus two ID sets. Capture sorts both sets and the V18 validator already prevents duplicating research unlock authority.
- Current payload validation accepts null lists and unordered IDs, while `RestoreState` clamps the day, drops negative IDs, and defaults null collections. Strict preflight can make those normalization branches unreachable.
- During transactional restore, `RestoreState` detects `aggregateRootStore.IsRestoreStaging` and suppresses offer projection. Projection is rebuilt only after `PublishedRestoreRevision` changes in `Update`, so the section can be rollback-free once payload validation is strict.
- The existing fixture already proves a discarded late candidate leaves the live shop untouched. Marking both the shop section and its throw-before-mutation failure fixture rollback-free turns this into direct all-marker discard-path evidence.
- The all-marker late-failure path correctly leaves `PublishedRestoreRevision` at 0. The prior non-marker rollback-image fixture expected revision 1 because rollback replay itself published a replacement; changing the expectation to 0 proves the live root was never replaced.
## 2026-08-03 facility-shop strict cutover result

- `facility-shop.state` now has exact V1 data and a required typed/preflight/rollback-free boundary. Unlock lists must be non-null, authored, unique, and strictly ascending.
- Restore no longer clamps the offer day or filters/defaults unlock IDs. During staging it replaces only the detached Aggregate; daily offer presentation is projected after a published revision.
- Full FacilityShop gameplay tests, invalid no-mutation, and all-marker late failure pass. The failed candidate leaves live state and published revision untouched at revision 0. V18 authority passes, non-rollback-free count is 29, and Console Error 0 / Warning 0.
## 2026-08-03 remaining-owner triage after facility shop

- Unity TypeCache confirms 29 production save sections still lack `IDungeonRollbackFreeSaveSection`. The list spans plain progression/pacing state, character substate, infrastructure networks, production, combat/offense, and world-topology owners.
- The next audit should prioritize ExperiencePacing, StaffDiscontent, ExternalInfluence, and DungeonDebug as likely plain-state boundaries, while deferring topology-heavy CropPlot/WorldResource and composite Treasury until their validators are explicit.
- ExperiencePacing and ExternalInfluence are still optional, version-migrating sections with missing-state reset semantics; they require explicit V18 compatibility decisions and are not marker-only candidates.
- StaffDiscontent already captures deterministic records through the typed JSON base and restores snapshots, but validation is embedded in restore and still trims/skips/defaults records. It is the strongest next strict Aggregate candidate.
- DungeonDebug synthesizes default payloads and directly restores presentation/debug service state. It is small but should be handled after deciding whether debug state is a required production save section at all.
## 2026-08-03 staff-discontent strict audit

- `StaffDiscontentRuntime.RestoreSnapshots` constructs a fresh plain `StaffDiscontentState` and replaces the Aggregate root; it does not mutate CharacterActor or presentation during restore.
- Current section has no `ValidatePayload`. Restore skips null/blank records, trims IDs, detects duplicates only during restore, and constructs snapshots that clamp mood/low-mood days. `FromSnapshot` additionally fills blank names and normalizes values.
- Strict preflight must make all of those transformations unreachable before adding rollback-free publication.
## 2026-08-03 Batch B remaining aggregate audit

- `ICharacterEnvironmentRuntime`, `ICharacterSpeciesRuntime`, and `IAnimalHusbandryRuntime` are wrapper aggregates over already-existing query/command/persistence contracts. Their remaining work is consumer migration plus deletion of the wrapper exposure, not a new state model.
- `ICharacterConsumablesRuntime` still combines diet policy, meal consumption, substance use, and persistence. Its result DTOs retain duplicate sentence-bearing `FailureReason` fields despite having failure codes; this boundary needs typed parameters and presentation-only localization before aggregate removal.
- `ISurvivalFoodRuntime` is the broadest remaining Batch B contract: persistence, UI queries, work commands, stock consumption, and debug mutation are all on one interface. It requires a deliberate query/command/persistence/debug split rather than a mechanical rename.
- Current concrete consumers confirm the split boundaries: save sections require persistence only; character/UI/operations panels require queries; work/building/AI flows require commands or targeted queries; debug command providers alone need debug mutation.
- `MealConsumptionResult` and `SubstanceUseResult` already carry `CharacterConsumablesFailureCode`, but duplicate localized/domain sentences through `FailureReason`. Facility, Shop, and `AbilityUseSubstance` read that sentence directly. The safe cutover is to replace it with typed parameters, return stable code names to non-UI diagnostics where unavoidable, and localize only at presentation boundaries.
- `CharacterSubstanceUseRequest.Reason` is command context rather than a failure result; it should be audited separately and not mechanically removed with result failures.
- A repository search included a nonexistent `Assets/Scripts/Services/Shop` path and returned exit 1; the actual shop consumer is `Assets/Scripts/Services/Buildings/Shop.cs` and was still found through the other roots.
- Batch B now has all six required named contract assemblies present (`DungeonStory.Characters`, `.Work`, `.Survival`, `.Medical`, `.Species`, `.Combat`). The engine-independent authored/runtime DTO seams therefore exist without forcing the Unity adapters themselves into a reverse dependency.
- Each of the seven Batch B owner sections is current-version, preflighted/staged through either the shared typed JSON section or an explicit candidate, and implements `IDungeonRollbackFreeSaveSection`: AnimalHusbandry, CharacterBodyHealth, CharacterConsumables, CharacterEnvironment, SpeciesRuntime, SurvivalResources, and DarkSurvival.

## 2026-08-03 Batch B broad-authority closeout audit

- Four broad runtime authorities remain: EnvironmentalField, EnvironmentalWorkwear, Surgery, and CharacterMedical. Surgery and CharacterMedical are assigned to independent workers; EnvironmentalField is explicitly Batch C-owned, while the main thread owns EnvironmentalWorkwear completion.
- `IEnvironmentalWorkwearRuntime` mixes equipped/stock queries, equip/auto-equip/unequip commands, and Capture/Restore/Reset persistence. The concrete runtime also permits a null research service, and WorkTaskExecutor/AbilityWork consume the broad port. The cutover must expose Query/Command/Persistence separately, make research mandatory, and preserve the shared CharacterEnvironment aggregate state.
- Workwear persistence is nested in the CharacterEnvironment aggregate rather than owning an independent save section. Its narrow persistence facet therefore prepares and validates an equipped-item map; CharacterEnvironment adds that map to the detached aggregate candidate and publishes once. This avoids a second direct workwear restore authority.
- WorkTaskExecutor does not need a query facet: it can issue `TryUnequip` and treat the typed `EnvironmentWorkwearNotEquipped` result as an idempotent no-op. This keeps its dependency count unchanged while maintaining strict Query/Command separation.
- The authoritative Batch B integration entry point is `BatchBCharacterSurvivalAuthorityDebugScenarios.RunAll()`. It executes survival/deprivation/dark-survival/species-husbandry/environment/wildlife/combat/save fixtures, verifies all seven strict save boundaries, enforces 39/54 rollback-free, checks removed wrappers and narrow facets, then runs V18 plus architecture validators.
- The required UI acceptance entry point is `CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestRunFromMenu()`, which owns the two-resolution EventSystem workflow and writes `Artifacts/QA/CharacterSummaryMedical/ui-matrix-report.txt`. `DarkSurvivalPlayModeVerifier.RequestRunFromMenu()` remains the separate dark-survival world/health pointer capture gate.

## 2026-08-03 Batch C seven-owner audit

- The exact seven remaining Batch C save sections are Power V1, Fluid V3, Conveyor V2, Automation V1, ProductionBills V4, WasteProcessing V1, and EnvironmentalField V1. Converting all seven from their current optional/staged or staged-only forms to strict required detached rollback-free boundaries moves the production total from 39/54 to exactly 46/54, leaving Batch D's eight.
- Broad authorities remain in `IElectricalNetworkRuntime`, `IWaterNetworkRuntime`, `IConveyorRuntime`/inherited command service, `IAutomationRuntime`, `IProductionBillRuntime`, `IWasteProcessingRuntime`, and `IEnvironmentalFieldRuntime`. Their results also retain raw messages/status sentences and several semantic null fallbacks.
- Conveyor currently removes a physical stack from the repository and copies the complete `WorldItemStackSaveData` into its own payload. The required cutover keeps the stack in the physical repository, adds an in-transit state, stores only `ItemStackId` plus segment/progress in the conveyor aggregate, and routes movement through `IItemTransferService`.
- Production bills currently key facilities with definition numeric ID plus coordinates, and EnvironmentalField thermostats use coordinates as owner keys. Batch C must use `BuildingInstanceId`; field cells may remain coordinate-indexed because the cell itself is spatial state.
- EnvironmentalField restore mutates live initialization state before validating the payload. It therefore needs a real detached `EnvironmentalFieldAggregateState` candidate and one final replacement, not merely a rollback-free marker or interface rename.
- Content-graph validation currently counts code-generated generic consumers for food, finished goods, fuel, and feed as real branches. Actual authored building supply profiles, recipes, equipment, surgery, construction, and gameplay item features must provide the reverse index; generic virtual consumer aliases cannot satisfy branch counts.
- All seven Batch C runtime/save/presenter surfaces remain in default Assembly-CSharp. Current largest sources are ProductionBillRuntime 1,180, ConveyorRuntime 1,178, FluidNetworkRuntime 1,126, and EnvironmentalFieldRuntime 870, so responsibility extraction must accompany the authority split instead of staying just below the limit.
- EnvironmentalField cannot claim rollback-free restore by adding a marker alone: its cells, topology, caches, and thermostat configuration must first form a detached Aggregate candidate and publish by one root replacement.
- Survival food cannot constructor-inject the field query directly because the field already depends on `ISurvivalEnvironmentQuery`; the startup bridge is the explicit cycle-breaking adapter. Its pre-start state can be an explicit `NoEnvironmentalFieldQuery`, eliminating semantic null DI while preserving weather fallback until the physical field initializes.
- Topology rebuild must delete thermostat overrides whose `BuildingInstanceId` no longer owns a configurable thermal emitter; otherwise a destroyed facility creates a save payload that strict restore correctly rejects. Pruning the derived stale owner at rebuild keeps capture self-consistent.
- The legacy `BuildingTemperatureAbility -> new BuildingThermalEmitterAbility` runtime conversion was another hidden content authority. Removing it makes authored `BuildingThermalEmitterAbility` modules the only physical-field source while Editor builders remain the explicit authoring path.

## 2026-08-04 authored production-consumer indexing

- `ResourceEconomyContentCatalog` was still manufacturing generic `service:*`, `commerce:*`, and generic kiln/boiler/incinerator/animal-pen links solely from item kind or tags. Those links were not backed by a recipe, equipment definition, substance definition, medical procedure, or authored facility supply profile, so they could falsely satisfy the two-branch intermediate-material rule.
- The reverse index now derives substance consumers from `SubstanceDefinitionSO`, ammunition consumers from each `CombatWeaponSO.AmmunitionItemId`, and fuel/feed consumers from each authored `BuildingFacilitySupplyAbility` profile that actually accepts the item. The generic built-in consumer generator has been removed.
- Food and finished-good consumption still require explicit authored consumer definitions before the full content graph can become a mandatory bootstrap gate; the honest index is expected to expose those missing authorities rather than silently treating aliases as gameplay branches.
- A second authority remains for substances: `SubstanceDefinitionSO` duplicates the same item ID, classification, addiction/overdose/tolerance/withdrawal/effect/duration data already authored in `SubstanceItemFeature`. `CharacterConsumablesRuntime` correctly reads the item feature, while `IResourceEconomyCatalog`, CharacterSummary, and ItemPile UI still read the parallel SO. The final cutover must project substance views from `ItemDefinitionSO` and delete the parallel definition/catalog path.
- Construction is not yet part of the concrete item dependency graph. `BuildingWorkAmountAbility` still authors one `StockCategory` plus an amount and can synthesize a General-category cost from money; `GetConstructionMaterials()` returns `Dictionary<StockCategory,int>`. Work execution and construction UI consume that abstract category. This must become an authored `IReadOnlyList<ItemAmountDefinition>` per building ability, be indexed as `ConstructionMaterial`, and reject missing concrete requirements rather than falling back to General stock.
- Terminal item use can be indexed without virtual service aliases when the capability itself is authored on the item SO: `FoodItemFeature` yields a character-consumption link, injury-capable `MedicineItemFeature` yields a treatment link, `InstallationItemFeature` points to its concrete building definition, and `BlueprintItemFeature` points to its concrete research target. Marketability was deliberately not counted as a production branch because commerce is not one of the approved direct-consumer edge kinds.

## 2026-08-04 concrete construction-material authority

- The construction worker migrated all 293 `BuildingSO` assets to explicit `ItemAmountDefinition` requirements: 101 legacy abilities were converted and 192 missing abilities were authored. Category/money-cost fallbacks, abstract save fields, and category reservations/consumption are gone; WorkOrder V3 and the construction UI use concrete item IDs.
- The dependency graph now indexes each `construction:{buildingId}` as an actual `ConstructionMaterial` consumer. Graph validation rejects a building with no work-amount authority, no material, a non-positive/blank/abstract/unknown material, or duplicate material IDs.
- The first honest Unity graph run after adding construction links failed with 70 findings: 9 loaded building definitions still lacked material authority and 61 produced items had no indexed real consumer. This is not a validator false positive: it exposes both a construction migration loading-scope gap and missing reverse-index/content links for ammunition variants, facility installation components, medical procedure parts, workwear, husbandry supplies, defense consumables, lineage/offense rewards, food/medicine, and other finished goods.
- After indexing only existing real systems (workwear definitions, explicit market-sale features, effective medicine use, and lineage transfer), the graph fell from 70 to 36 findings. Fixing all 343 cataloged buildings, authoring twilight beer/night spirit as actual `SubstanceItemFeature` consumables, and using fermented vinegar in two preserved-food recipes reduced it again to 32; the remaining list is exactly two defense ammunition items plus 30 research-overhaul facility/equipment/medical/agriculture components now assigned to parallel content work.

## 2026-08-04 production consumer closure

- The final 32 gaps required eight distinct authoritative paths rather than more recipes: equipment component inputs, concrete building construction materials, surgical procedure materials, crop-cycle supplies, stock-sensor installation, equipment-maintenance supplies, physical defense supplies, and expedition tools. These are now read directly by the systems that deliver and consume the physical item.
- Ammunition reverse indexing must enumerate `CombatWeaponSO.CompatibleAmmunitionItemIds`; indexing only the preferred legacy `AmmunitionItemId` hides steel/arcane arrows and bolt variants. The graph and runtime catalog now enumerate the complete typed list.
- Stock sensor panels, maintenance kits, fertilizer/substrate, toxic trap coating, and expedition field tools are not ordinary recipe inputs. Counting their real consumers therefore requires indexing their authored capability fields, while their runtime owners must independently prove delivery and consumption.
- The honest branched-production validator now passes after closing the 32-item backlog. No `sink:*` recipe or item-kind-derived generic service alias was added.
- `ItemTransferService` no longer exceeds the 1,200-line runtime limit. Facility-buffer transaction validation and mutation moved to a focused helper, and destination-priority ownership moved to the warehouse service without introducing a second inventory authority.
- The environmental solver's apparent 28ms p95 was not merely a noisy threshold: each access to `temperature`, `air`, `light`, barrier, door, duct, and exterior properties re-entered the scoped Aggregate root. Caching those array references once per fixed tick brought the loaded measurement to 17.648ms and makes the performance contract substantially less sensitive to preceding integration tests.
- Work-order Editor scenarios must not use `UnityGameClock` when their contract is unrelated to pause. A prior production scenario can leave `Time.timeScale == 0`, preventing orphan cleanup from ticking and producing an order-dependent false failure. Fixed game/UI clocks make the construction recovery proof isolated and deterministic.
## 2026-08-04 Batch B character/medical checklist reconciliation

- The Phase 112 wording was stale for three medical items: `ICharacterBodyHealthRuntime` has zero source references, `CharacterBodyHealthRuntime` implements separate Query/Command/Persistence facets, and its save section consumes only `ICharacterBodyHealthPersistence`.
- Surgery and character-medical orders no longer persist presentation sentences. `SurgeryOrder.statusData` is `SurgeryStatusData`; `CharacterMedicalOrder.statusCode` is `CharacterMedicalStatusCode`; no legacy `status`, `FailureReason`, or `out string failureReason` field remains in those contracts.
- The only literal `out string` left in `CharacterSurgeryWindow.cs` was a carcass-species parser result, not a domain failure. A value-returning parser now removes that UI boundary leak while preserving the existing `Try...` compatibility API for non-UI callers.
- Static String Table reconciliation found 280 distinct required keys across `FailureCode`, consumables failures, surgery statuses, surgery risk summaries, and character-medical statuses; shared data, Korean, and English tables each have zero missing keys.
- The genuine remaining Batch B structural item is concrete Unity-adapter assembly ownership. Pure Character/Work/Survival/Medical contracts and SO models are in named assemblies, but service adapters still depend on default-assembly types such as `CharacterActor`, `BuildableObject`, Grid/world-item ports, and VContainer composition. Moving those adapters safely is a broader default-assembly cutover, not a missing medical facet.

## 2026-08-04 production UI evidence and remaining routing gap

- A functional pointer PASS was not sufficient visual evidence: the first captures rendered only two of three route rows, overlaid the route panel on the building body, clipped portrait chrome, and wrote mojibake to the report. The acceptance gate now checks the building root, context panel, fixed controls, ScrollRect-visible controls, all route rows, and the third route row against screen bounds before capture.
- The accepted layout shows all three route policies at both required resolutions and hides the legacy building tabs/demolition control while the context action is active. Report writing uses BOM-bearing UTF-8 and re-reads Korean sentinel phrases before declaring PASS.
- `ProductionConsumerRoutePolicy` is authored, editable, and saved, but the current runtime search shows `ProductionRouteDistribution.SelectNext` is exercised only by the debug scenario. `ProductionConsumerRouteState.currentDemand/reservedQuantity/blockedReason` is not yet projected by a live routing query, and the UI rows currently show policy values rather than the requested live demand/reservation/block reason. Batch C therefore still needs an actual downstream route-state/query/dispatch implementation even though the UI pointer matrix itself is accepted.

## 2026-08-04 save-root and architecture-ratchet findings

- Seven of the final eight Batch D save sections are now strict detached/rollback-free in source. Raising the global validator from `46/8` to `54/0` before OffenseAggregate finishes would be false evidence, so the ratchet remains unchanged until the final section and the complete loaded type graph pass together.
- `DungeonSaveSectionRegistry.RestoreAll` previously called `CaptureAll()` unconditionally after every successful stage build, even for an all-marker registry. This defeated the intended rollback-free cost model. The capture is now conditional on a legacy section being present, and the failed aggregate-candidate regression asserts that neither rollback-free section is recaptured.
- The original `largeConstructor` metric treated DTO/value constructors as dependency injection and reported 90 violations, including requests, snapshots, and result values. Applying the requirement to operational owners only produces 32 actionable DI constructors; these remain real Batch E work and are not waived.
- The current Roslyn baseline verification is expected to fail while concurrent source migrations change the exact oversized/default-assembly sets. No baseline write is permitted until the active Offense, production-routing, and CoreSession-asmdef slices finish and their exact diffs are reviewed.

## 2026-08-04 all-marker save and live production source closure

- `OffenseAggregateSaveSection` is now the eighth and final Batch D strict boundary. It prepares expedition, world, region, battle, mitigation, preparation, field-medical, travel, decision, and return-arrival candidates before publication; its constructor is reduced from 13 dependencies to 5. Canonical, invalid-no-mutation, and late-discard Editor scenarios exist, but Unity-loaded execution is still required.
- The V18 source ratchet now requires all 54 production sections to implement `IDungeonRollbackFreeSaveSection`; the approved remaining set is empty. `DungeonSaveSectionRegistry` does not call `CaptureAll()` on an all-marker restore path. This remains source evidence until Unity TypeCache and the full restore fixture pass.
- The previously missing production seam is implemented in source. Four live demand providers feed route state, the physical output-buffer path invokes `SelectNext`, and fallback ordering plus fairness policies are connected. The merged Unity scenario remains the acceptance gate.
- Current Roslyn metrics are `1126 files / 3499 types / 0 mutable statics / 13 oversized types / 28 large constructors / 1058 default-assembly sources / 0 content escapes / 0 direct session mutations / 6504 raw Korean literals / 4 root catalog references`. This is not an accepted baseline; the 13/28/1058/6504 residuals remain open work.
- The top-level `DataManager` and `IDataScriptableObjectSource` were a second in-memory content authority even though their input already came from `IGameContentCatalog`. They are removed. The remaining numeric compatibility API is a read-only, rebuildable `GameContentDataCatalog` projection over the root SO catalog; a V18 source ratchet prevents the deleted cache/source types from returning. Typed stable-ID domain catalogs are still the target for retiring the numeric compatibility surface itself.

## 2026-08-04 merged authority audit findings

- Unity's first merged run used a DLL compiled just before the final save-ratchet source edits, so it reported the obsolete 46/8 constants even though the current validator source and TypeCache contract are 54/0. A fresh compile after all active file edits is required before treating those messages as implementation evidence.
- The audit still found a genuine atomicity violation: `OffenseAggregateSaveSection.CommitRestore` called `returnArrivals.Restore`, mutating the live return-arrival root before `PublishRestoreCandidate`. The return-arrival runtime now exposes `PrepareRestore` and non-failing `PublishRestore`, and the offense candidate owns that fourth detached candidate alongside expedition, region, and world.
- `SurvivalDebugScenarios` encoded an obsolete exact count of seven substances. The SO catalog now intentionally contains nine after twilight beer and night spirit became real physical substance items; the test now requires at least nine unique stable substance IDs and retains class-specific addiction invariants.
- Runtime class size must be measured per Roslyn type, not by total lines in a source file that can contain several small context classes. The duplicate file-length baseline gate contradicted the explicit 1,200/800 type contract and has been retired in favor of `BatchAArchitectureMetricsValidator`.

## 2026-08-04 responsibility and serialization findings

- Meaningful state/behavior collaborators can close the type-size gate without partial-class shuffling: the owner keeps serialized aggregate authority while per-entity occupancy, assignment, presentation, natural-condition, launch, decision, and execution policies move to plain C# collaborators. Current oversized and large-constructor sets are both empty.
- Unity Console compiler diagnostics can be returned as entries whose MCP `Type` is `Log`; filtering only `Error`/`Warning` can therefore produce a false clean result. Consolidated verification must inspect all Console entries for compiler `error CS` and `warning CS` messages after each assembly reload.
- Unity `JsonUtility` does not preserve null for an optional nested serializable class in the offense save DTO. An explicit presence bit is required; relying on `null` or accepting an empty object would either reject valid no-battle saves or permit hidden malformed state.
- New collaborator source files increased the default-assembly file counter while improving type responsibility. The architecture baseline must not be raised to bless that regression; actual named-assembly migration is required before the V18 authority gate can pass.
- Exact set hashes are useful only while a ratchet count is unchanged. Requiring an obsolete hash after a strict count reduction makes successful cleanup fail; both Roslyn and Unity architecture gates now accept lower counts and retain identity review for equal-count churn.
- Catalyst IDs had conflated 21 progression steps with a potency feature whose authored contract is 1-5. Treating the suffix as potency invalidated 128 authored SOs and leaked progression into effect/save fields. The corrected model keeps progression in identity/unlock rules and stores only derived grade in effect/save state.
- A source-path assertion in the V18 validator became stale when `DoorAccessSubjectAggregateState` moved to `DungeonStory.Buildings`. Named-assembly moves must update source-contract paths in the same change; otherwise a correct runtime type graph can still fail static authority validation.
## 2026-08-04 Unity MCP command-vs-project compilation boundary

- `Unity_RunCommand` reporting `isCompilationSuccessful=true` proves only that the dynamic command compiled against the currently loaded assemblies. It does not prove that the project source refresh which follows the command produced a new `Assembly-CSharp.dll`.
- A Captivity/Circus command completed all ten scenarios while the Console simultaneously contained fresh project compiler diagnostics from the active FacilityShop/Automation migrations. Therefore every acceptance run must additionally wait for `IsCompiling=false` and inspect all Console entry types for `error CS` and `warning CS`; command success alone is not acceptance evidence.
- The scenario result remains useful behavioral evidence for the loaded assembly, but it must be rerun after the source revision reaches Console Error 0 / Warning 0 before the batch is accepted.

## 2026-08-04 fresh-assembly recovery and V18 gap audit

- `OperatingDaySettlementRuntime.cs` contained a literal tool-output truncation marker. The source was rebuilt from the last known-good compiled type, malformed strings were replaced with ASCII Unicode escapes, ambiguous decompiler `Object` references were explicitly aliased to `UnityEngine.Object`, and duplicate declarations remain zero.
- An apparent zero-error checkpoint was rejected because 28 source files were newer than the loaded DLLs. The authoritative retry waited for domain reload and proved both `Assembly-CSharp` DLLs newer than every relevant source; source-newer counts are now zero.
- The fresh focused run passes Blueprint Research, Research Tree, the 168-node Research/Equipment overhaul, Branched Production, Facility Evolution, and Survival. Approved pacing remains `32.2/80.4/234.3/372.0` days.
- Architecture metrics were regenerated and pass at `1296 files / 4042 types / 0 mutable statics / 0 oversized types / 0 large constructors / 885 default-assembly files / 0 content escapes / 0 direct session mutations / 6505 raw Korean strings / 3 root catalogs`.
- Save-boundary audit found one formal Offense aggregate section but several public direct restore bypasses. `OffenseWorldStateSaveCodec.Restore`, `IOffenseSaveService.Restore`, and public subsystem restores sit outside the detached prepare/publish transaction and must be removed or internalized.
- Thirty-two sections still inherit the legacy one-parameter `DungeonJsonSaveSection<T>`, whose fallible candidate construction can occur at commit time. Operating Day is the first strict-candidate conversion; the other 31 require individual proof rather than marker-only upgrades.
- Runtime `ScriptableObject.CreateInstance`, `Resources.LoadAll`, `GetDefinitionOrDefault`, and `FromStockCategory` content fallbacks are zero. A representative-item authority remains in `CanonicalStockItemIds` and `DungeonItemCatalog.GetDefinition(StockCategory)` with eight production caller groups; that mapping is now a dedicated removal batch.
- The leaked `Character Model Scenario Character` fixture was confirmed and removed through Unity MCP, then the scene was saved and verified clean with zero matching objects.
- The saved GameplayScene currently contains 16 embedded `MonoScript` blocks, not the earlier estimate of 10: FacilityEvolutionStateComponent, CharacterSkillTransientState x2, FacilitySynthesisRuntime, OffenseRewardRuntime, CharacterActorPresentationBridge x3, StaffDiscontentRuntime, OperatingDaySettlementRuntime x3, DailyFacilityShopRuntime, BlueprintResearchRuntime, CodexRuntime, and RegularCustomerRuntime. These require Unity-side script rebinding/removal; text deletion is not accepted evidence.
- Unity's authored-script lookup resolves exactly one project MonoScript asset for eight embedded component types. `CharacterSkillTransientState` and `FacilityEvolutionStateComponent` resolve zero because their MonoBehaviour declarations still live in files named `CharacterSkillRuntimeEffects.cs` and `FacilityEvolutionState.cs`; they need class-name-matching script assets before all 16 embedded MonoScript objects can be rebound safely.
- After the authored Codex component was added, `DungeonRuntimeSystems` still exposes two Unity fake-null component slots at indices 12 and 21 even though `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` returns 0. Slot 21 corresponds to the old embedded Codex component; slot 12 is a second invalid component that must be identified before removing only the broken serialized entries.
- Fake-null slot 12 is `OffenseExpeditionRuntime`. Its script GUID points to `OffenseExpeditionSystem.cs`, while the MonoBehaviour class is named `OffenseExpeditionRuntime`; this also explains the earlier `ExtensionOfNativeClass` regression. The file identity must be renamed while preserving GUID `d577ed6425ec47ed8e60f245ce07336a` before removing/replacing serialized null slots.
- A menu-style regression that only logs failures can appear as PASS to an outer command unless its Console side effect is inspected. `CombatEquipmentMaterialDebugScenarios` exposed this: the wrapper returned normally while two cases logged an error. The fixture now uses the same explicit `BuildableObject` dependency composition as runtime, and the scenario's own success log is part of the accepted evidence.
- The current semantic asmdef graph has no leaf candidates; useful progress requires cutting a small cyclic boundary rather than waiting for a naturally movable file. Moving presentation ports/policy IDs and pure restore validation rules reduced cyclic SCCs 18 -> 16 without creating named-to-default backreferences.
- A generic Foundation extraction can reveal the stricter 800-line MonoBehaviour gate even when the overall runtime limit is 1,200. `CharacterStats` briefly measured 809 lines; extracting condition-penalty projection restored oversized type findings to zero instead of raising the baseline.
- Small cyclic SCCs were mostly concrete reverse references or partial-class ownership leaks, not reasons to move whole feature trees at once. Narrow command/query ports, callback bundles, pure restore policies, and cohesive context objects reduced 18 cyclic SCCs to one without adding named-to-default dependencies.
- Partial files are not automatically harmless organization. `DefenseEngagementRuntime.Restore.cs` formed a two-file semantic cycle despite containing only three public methods. Folding its public restore facade into the single runtime declaration and moving cell-reservation calculation to the existing intercept planner kept the runtime under 1,200 lines and removed the cycle.
- UI gestures should depend on an interaction sink, not their concrete window coordinator. `ResearchTreePanSurface` and `ResearchQueueRowDrag` now target `IResearchTreeInteractionSink`, which makes the interaction, viewport, and window files acyclic without changing pointer behavior.
- Integration commands must not run while a worker is between a source move and its `.meta` move. Unity correctly warned about `InvasionIntruderContentBinding.cs.meta` during that transient state; the original GUID was restored, but the checkpoint was rejected and scheduled for a stable rerun.
## 2026-08-04 Phase 116 integration findings

- A clean Editor.log tail is not sufficient when the loaded Assembly-CSharp DLL predates the latest source. The current relay disconnect therefore blocks acceptance only for the newest batch, not further source-level SCC decomposition.
- `SurvivalEnvironmentalFieldBridge` can live in Infrastructure when Survival owns only a narrow environmental sink contract; this removes the bridge from the default assembly without making Survival depend on Infrastructure.
- `WorkTargetCandidate` can move to the Work assembly when its persisted/domain-facing building reference is `IBuildingWorldEntryPort`; the sole `BuildableObject` cast belongs in the default runtime adapter rather than the candidate value itself.
- The latest authoritative source checkpoint is default assembly 856 and giant SCC 508 with all hard architecture violation counters at zero. Bee response files and generated csproj data are stale until Unity completes a fresh import.

## 2026-08-04 cohesive cluster findings

- The new settled checkpoint is default runtime 852 and giant SCC 504. Further progress requires moving the contract and its concrete adapter together; keeping a one-file cap would preserve the wrong architecture.
- Source-path ratchets can silently stop checking a moved implementation when they use optional `File.Exists` reads. The Blueprint research save check did exactly this until its path was updated to Infrastructure.
- `GameplayArchitectureRatchetTests.SourceBySuffix` had 16 unresolved calls across moved or split sources. A mechanical path audit is now part of each migration checkpoint; all 280 calls currently resolve uniquely.
- The Unity relay server listens on the configured ports but the editor has no live WebSocket connection after domain reload. Relay-only restart did not help, while `Temp/__Backupscenes/0.backup` is 59 minutes newer than the saved GameplayScene; editor restart is not an acceptable recovery action without first preserving that state.
- Survival deprivation diagnostics and their snapshot form one cohesive named-domain cluster. Moving the diagnostics class alone is invalid because a named assembly cannot reference the snapshot while it remains default-owned.

## 2026-08-04 first cluster barrier findings

- Cohesive clusters reduce the semantic cycle faster than file-count alone: default runtime ownership moved 852 -> 839 while the giant SCC moved 504 -> 490 in one barrier.
- Generic named contracts are an effective compile-time boundary when a default implementation must remain closed over `CharacterSO` or `CharacterActor`; the closed alias stays at composition/runtime edges without reflection, `object`, or service location.
- Moving immutable DTOs and snapshots alongside their aggregate preserves restore semantics while allowing staging stores and scene application adapters to remain outside the named domain.
- A named/global namespace move must audit type namespace imports separately from asmdef references. The warehouse cluster had a valid assembly edge but still needed an explicit namespace import for `ShopSaleItemDefinition`.
- The central planner barrier must be scheduled after all source lanes stop editing; otherwise changing input hashes prevent meaningful determinism evidence even when every individual change is valid.

## 2026-08-04 second cohesive-cluster findings

- Several apparent Character-to-default dependencies were broader than the rule required. `ResourceCharacterSpeciesCatalog` only needs `IGameContentDefinitionSource`; path-search and idle-wander rules only need grid position plus `GridTraversalContext`; movement-facing policy only needs a two-method character capability. Reusing these existing/narrow contracts removes edges without introducing a second catalog, state store, or fallback.
- Unity can regenerate Bee response files while the project-scoped MCP transport remains disconnected. Response-file freshness is useful compile input evidence, but it is not proof that the Editor accepted the assemblies or that serialized SO references survived import.
- For focused Unity Roslyn verification on Windows, passing the full response-file argument set directly exceeds the command-line limit. Generate a derived response file under `Library` and invoke `csc.dll` through Unity's bundled `NetCoreRuntime`; never overwrite Bee's real output DLLs.
- PowerShell's automatic `$args` variable must not be repurposed as a mutable compiler-argument list. Use a task-specific name such as `$compilerArgs`.

## 2026-08-04 third cohesive-cluster findings

- A leaf adapter can still hide a default-assembly dependency behind extension-method syntax. `BuildingConnectivityQueryAdapter` appeared to depend only on named Grid and Buildings contracts, but `Grid.IsConnected` resolved to `GridBuildingExtensions` in Assembly-CSharp. Focused compilation exposed the boundary; moving the pure occupant-path query onto `Grid` made the ownership explicit without duplicating state.
- MonoBehaviour source moves must preserve the `.meta` GUID even when the new filename is improved to match the class. `EventAlertRuntime` and `NoticeFeed` can reside entirely in Presentation because their Aggregate/event contracts are already named and Presentation owns TMP/UI/VContainer dependencies.
- Infrastructure runtime registries that wrap named Aggregate sessions can move as cohesive adapters when their concrete consumers remain at the Unity edge. Their type must be public across the assembly boundary, but mutation still remains encapsulated in the Aggregate session.
- Precompiled-plugin references do not imply that source extension modules are visible to a named assembly. DOTween core was referenced by Presentation, but `CanvasGroup.DOFade` lived in the default-assembly `DOTweenModuleUI.cs`; using the core `DOTween.To` API removes that hidden edge while preserving the same alpha interpolation.
- Broad scene-reference containers should not be injected into small UI constructors when the UI needs one object. Extracting the EventSystem-only bootstrap reference lets the title canvas live in Presentation without turning the full scene Aggregate into a Presentation dependency.
- Session clock/speed interfaces and user-settings DTO/contracts are shared protocols, while their Unity effects and persistence are Infrastructure. Moving only the protocols to CoreSession/Foundation enables presentation policy migration without moving mutable implementation state into an SO or introducing a second settings owner.
- A settings-aware MonoBehaviour is not a pure palette merely because its first type is static. `DungeonUiThemeRuntime` still styles default-owned room and building views, so only the stateless palette facade can move now; the component remains at the Unity adapter edge with its original serialized identity.
- Path-based architecture ratchets must be rerun after every source move even when semantic planner and focused compilation pass. The Invasion intruder planner move preserved its API and GUID but left one stale test suffix; a full 281-call uniqueness audit caught it before the Unity test boundary.

## 2026-08-04 localization and encoding authority audit

- The current ArchitectureMetrics Korean-literal rule covers `6,441` ordinary string literals across `401` non-Editor runtime files, but omits another `2,122` Korean interpolated-text segments across `229` files. The proven display/content debt is therefore at least `8,563` tokens, and the metric must include `InterpolatedStringTextSyntax` before it can be used as a closure gate.
- Of the `6,441` counted literals, `6,423` are valid Korean, `18` are confirmed mojibake, and U+FFFD replacement characters are `0`. The 18 damaged literals are confined to five runtime files: `DefenseFacilityRuntime.cs` (14), three CharacterCombatCommand runtime/contract files (one each), and `WildlifeRuntime.cs` (one).
- Only one String Table collection exists (`DomainFailures`): `296` shared keys with complete Korean and English entries. Exact raw-literal coverage is only `2/6,441`; UI/domain candidates total `3,482`, so localization is not close to completion.
- A UI-first role partition yields `2,089` UI display literals, `1,387` non-UI domain-error literals, `883` authored narrative/content literals, and `2,082` other literals. Recommended non-overlapping vertical cuts are Production presentation, Defense runtime/presentation, then Character narrative content.
- `ProductionRoutePanelPresenter` itself has no encoded mojibake. It contains four valid Korean UI tokens plus three English status templates not covered by existing tables; use dedicated `ProductionUI` keys for header, priority/weight/reserve controls, demand/reserved, blocked, and inactive-consumer text.

## 2026-08-04 risk-based assembly closure decision

- `Assembly-CSharp` file count is a poor completion oracle. A scene-bound adapter and a mutable gameplay Aggregate both count as one file even though only the latter threatens authority, determinism, and test isolation.
- The enforceable replacement is a reviewed role classification: `NamedRequired`, `DefaultAllowed`, or `ReviewRequired`. A mixed Unity/domain owner cannot be approved as an adapter; it must be split until gameplay state and rules are named-owned.
- The cycle gate must also be semantic rather than numeric: named asmdef cycles remain forbidden, no cyclic source SCC may contain a `NamedRequired` owner, and remaining default-edge code may not bypass command/query/capability/DTO boundaries between gameplay domains.
- This rescope is expected to remove roughly 70-85% of the former file-migration workload, but the estimate is scheduling guidance only. Completion is proven by empty reviewed violation sets, not by an estimated percentage or a chosen residual file count.

## 2026-08-04 Phase 117 ownership-classifier evidence

- The first syntax-based audit classifies every one of the `811` current default-runtime sources: `35 DefaultAllowed`, `441 NamedRequired`, and `335 ReviewRequired`. The honest `UnapprovedDefaultDomainAuthorityCount` is therefore `776`, not zero.
- `defaultAssemblyFiles` remains in the report as trend information but is no longer part of the baseline ratchet. The new unapproved-authority metric is emitted separately, and the current baseline intentionally does not approve this newly measured debt.
- Classification records exact syntax/type/source-role evidence. Mutable state, Aggregate/state/store/rules/policy/calculator/content/SO/persistence/command-query and deterministic service roles raise risk; a mixed Unity edge and domain owner remains `ReviewRequired` and cannot be downgraded to `DefaultAllowed` by a manifest explanation.
- The exact-path override manifest rejects wildcards, parent traversal, missing fields, duplicate normalized paths, deleted paths, named-assembly paths, and other stale entries. The separate Library report currently lists `22` cross-domain-cycle candidates with their referenced domain set.

## 2026-08-04 Environment work-policy boundary audit

- `EnvironmentWorkPolicy` mixed pure cooldown/failure/speed decisions with `CharacterActor`, `Grid`, workwear commands, and Unity coordinates. The named `DungeonStory.Environment` assembly already owned the core exposure math, so creating another assembly or moving Unity-facing types into the domain would have been the wrong boundary.
- Cold-work cooldown hysteresis, safety-exception classification, blocking-failure selection, and legacy speed selection now live beside `EnvironmentWorkRules` in the named Environment domain. The default edge only converts scene actors/cells and maps the typed decision to `DomainFailure` presentation parameters.
- The default type is now `EnvironmentWorkPolicyUnityAdapter`; classifier evidence changes from `NamedRequired` to `DefaultAllowed`. It remains visible in the cross-domain candidate report because it deliberately bridges Environment and Foundation contracts, but no gameplay decision is owned by that bridge.
- The source/meta rename preserves GUID `286444572b7d9f24db60fc3a64916ba7` exactly once. The old source/meta paths and old concrete type reference are absent; the interface contract and singleton composition registration remain unchanged.

## 2026-08-04 Character environment runtime boundary audit

- `CharacterEnvironmentRuntime` mixed deterministic exposure accumulation, band transitions, and movement/accuracy policy with `CharacterActor`, world queries, persistence projection, and Unity-side damage/effect dispatch. The pure transition now lives in the named `DungeonStory.Environment` domain as `CharacterEnvironmentRules.StepExposure`; the default edge is explicitly named `CharacterEnvironmentUnityAdapter`.
- The adapter projects the named result back into the unchanged `CharacterEnvironmentExposure` save DTO and retains the existing side effects, timers, capture/restore ordering, and aggregate publication. No V18 manifest, save version, DTO shape, or Character AI narrative source was changed.
- The classifier target changed from `NamedRequired` to `DefaultAllowed` with `unity-edge-suffix` evidence. The current report is `811 default / 79 allowed / 439 named / 293 review / 732 unapproved / 22 cross-domain candidates`; only the target transition and the one-count unapproved reduction are attributed to this cluster because other lanes are active.
- The runtime source/meta path remains unchanged to preserve serialized identity. GUID `1e8e23e7affbbc645a4ef3b83b17163f` occurs exactly once, and all old concrete-type references are absent.

## 2026-08-04 Character progression boundary audit in progress

- `CharacterProgression.cs` is an 877-line scene component that currently owns both deterministic experience/level transitions and Unity-facing `CharacterActor`, alerts, draft generation, triggered passives, and persistence projection. Moving the whole type would create the wrong boundary; the narrow cut is to extract only the pure progression transition into the existing named Characters domain.
- Save safety requires the existing `CharacterProgressionSnapshot`, capture/restore order, draft generation, profile warming, notifications, and actor vital recalculation to remain on the current adapter path. The first candidate for extraction is therefore the experience curve plus add/target-level transition result, not the persisted DTO or Character AI narrative behavior.

## 2026-08-04 Character progression boundary audit complete

- `CharacterProgressionRules` and immutable `CharacterProgressionTransition` now live in `DungeonStory.Characters`. They own the experience curve, experience addition, minimum-level advancement, reached-level sequence, ratio projection, and restore normalization without referencing `CharacterActor`, Unity scene objects, save services, or authored skill/narrative content.
- The existing `CharacterProgression` MonoBehaviour remains the compatibility and side-effect edge. It applies reached levels in their original order, so deterministic stat allocation, vital recalculation, logs, draft unlocks, and `Changed` publication preserve their former order and meaning. `CharacterProgressionSnapshot` shape and the V18 capture/restore call sites are unchanged.
- The target remains honestly `ReviewRequired` because it still serializes progression/growth state and contains the save snapshot beside a MonoBehaviour. This lane did not add an override or move that persisted authority merely to improve a metric. The useful boundary delta is that `CharacterProgression.cs` is absent from the current cross-domain candidate set while the new rules source is owned by the named Characters assembly.
- The original MonoScript GUID `badabbf33eed2ae46b77a5f13883bc2d` remains exactly once. The new named rules source has unique GUID `4b5a3cf2ed6845d8a76c50e0909a09c2`. No Character AI narrative, facility naturalness/utility, Defense Codex, save-version, save-service, DTO-shape, or restore-order source was edited by this lane.

## 2026-08-04 Environmental field boundary audit in progress

- `EnvironmentalFieldRuntime.cs` owns three distinct layers: strict save projection/restore mapping, a mutable array Aggregate and root store, and Unity Grid/building/power/clock projection. The save path already delegates detached candidate validation to named `EnvironmentalFieldRestoreRules`; no save defect was found, so the save section and DTO must remain untouched.
- The safe boundary is to move the array Aggregate plus diffusion, exterior exchange, air recovery/contamination, light relaxation, source-cell transitions, swaps, and version touch into the existing named Environment assembly. The default adapter should retain only Grid topology discovery, line-of-effect, authored building projection, power checks, and fixed-clock scheduling.
- The runtime can preserve exact Grid replacement semantics without default-owned gameplay state by holding a readonly `WeakReference<Grid>` projection cache. Source descriptors can also remain at the edge as immutable records; target overrides and simulation arrays remain named-owned.
- The earlier Character progression candidate-removal report disagreed with the root fresh analyzer and is withdrawn. Before this lane is handed off, CharacterProgression will be rerun through the same fresh analyzer and resolved without an exact-path override.

## 2026-08-04 Environmental field and Character progression boundary closure

- `EnvironmentalFieldAggregateState`, its Aggregate-root store, and all deterministic diffusion/source/buffer/version rules now live in named `DungeonStory.Environment`. `EnvironmentalFieldRuntimeApplicationAdapter` retains only fixed-clock scheduling, Grid topology and line-of-effect discovery, authored building/power projection, and the unchanged strict save mapping.
- A standalone legacy-equivalence probe compared 240 deterministic randomized grids and 16,234 assertions across temperature, air, light, barriers, doors, ducts, exterior exchange, source helpers, swaps, and version increments; all passed. Focused `DungeonStory.Environment` compilation also passes with zero diagnostics.
- The earlier Character progression candidate result was stale, not an analyzer defect. The actual remaining cross-domain edges were `IGameEventBus`/Operation presentation and Foundation deterministic-random construction. They now live in `CharacterProgressionNotificationApplicationAdapter` and `CharacterProgressionGrowthApplicationAdapter`; the state owner references only `DungeonStory.Characters`.
- Fresh ArchitectureMetrics reports both target paths absent from `crossDomainCycleCandidates`. The environment adapter and notification adapter are `DefaultAllowed`; `CharacterProgression` remains honestly `ReviewRequired` because it still owns serialized per-character state and its snapshot, but it is no longer cross-domain. No exact-path override, manifest approval, or classifier weakening was used.
- No environmental save section, DTO, payload version, restore order, or V18 compatibility rule changed. The original environment and Character progression GUIDs plus all three new source GUIDs are unique; the 48-asmdef graph has zero cycles.
- Unity comprehensive script validation passes for all changed runtime/rule sources. The merged Editor currently has one unrelated compile error in `GridFoundationDebugScenarios.cs` for missing `DungeonEntranceGridResolver`; this lane did not edit that source, so final Console-zero acceptance remains with the merged integration owner.

## 2026-08-04 External influence CoreSession/application boundary

- `ExternalInfluenceRuntime.cs` mixed root-Aggregate access and deterministic reputation, dread, hostile-rumor, scouting, ecology, raid, intel-payment, and invasion-defense state transitions with authored CoreSession content, Unity clock, money ledger, physical items, wildlife world state, and event presentation.
- Added named `DungeonStory.CoreSession.ExternalInfluenceAggregateStateStore` and `ExternalInfluenceDomainRules`. They now own all direct state changes, identifier normalization, threshold decisions, countdowns, daily pressure/report transitions, payment-state commits, raid lifecycle, and dread multipliers. The default `ExternalInfluenceRuntimeApplicationAdapter` retains only external capability checks/transactions, world snapshots, event subscriptions/alerts, JSON projection, and strict candidate publication.
- The existing V3 DTO, restore candidate, save section, validation order, participant order, V18 compatibility meaning, and transaction ledger owner/reason strings are unchanged. No save or contract source was edited; the adapter publishes through the named store into the same Aggregate root slot.
- A deterministic current-source probe passed 6,506 comparisons covering all extracted numeric clamps and transitions. Focused `DungeonStory.CoreSession` Roslyn compilation has zero diagnostics. Unity current-source fairness and content-authority scenarios pass, and direct strict validation accepts the canonical V3 payload while rejecting out-of-range renown.
- The combined Batch A suite reaches its unrelated presentation check and then fails in `DomainFailureLocalizer` because the current `InsufficientRenown` String Table format expects a different argument count. This is outside the external-influence boundary and was not hidden by changing the localizer or save fixture; focused external-influence validation passes and the final Console is Error 0 / Warning 0 after the diagnostic run is cleared.
- Fresh ArchitectureMetrics classifies the preserved target `DefaultAllowed` with application-adapter evidence and reports target candidate count 0 without an override. The observed shared-tree checkpoint is `1,368 files / 4,244 types / 817 default / 123 allowed / 408 named / 286 review / 694 unapproved / 11 cross-domain candidates`; only the target transition is attributed to this lane.
- The original MonoScript GUID `115d5aeafd549764a9fbff9b92d35017` and new named source GUID `c7ea3bfe8eec4f909347a5e0f48bf0e4` each occur exactly once. The 48-asmdef graph has zero cycles, old concrete construction/static-policy references are zero, and scoped `git diff --check` passes.

## 2026-08-04 World-simulation composition registration classifier

- `DungeonWorldSimulationRegistration.cs` contains one static extension method and no fields, properties, constructors, nested types, local state, assignments, loops, switches, policies, or calculations. Its only runtime branch is the existing scene-capability registration guard; all other calls are VContainer registration/exposure wiring.
- The ownership analyzer now recognizes composition registration only when the file is under `Services/Infrastructure/Registration`, the type is static and ends in `Registration`, every member is a static `void Register*` method whose first parameter is `IContainerBuilder`, and every invocation is registration/exposure wiring, `nameof`, or the approved scene-capability check. Local declarations, assignments, local functions, loops, and switches reject the shape.
- A separate three-source analyzer probe proves the boundary: pure registration is `DefaultAllowed`, while an otherwise identical registration with mutable static state and another with a local policy calculation both remain `ReviewRequired`. Existing `MetaRuntimeApplicationAdapter`, `OperatingDaySettlementRuntime`, and `ConstructionSite` also remain `ReviewRequired` with their mutable-state evidence.
- Fresh ArchitectureMetrics classifies `DungeonWorldSimulationRegistration` as `DefaultAllowed` with `composition-registration` evidence and target candidate count 0 without an exact-path override or baseline change. The observed shared-tree checkpoint is `1,368 files / 4,244 types / 816 default / 131 allowed / 408 named / 277 review / 685 unapproved / 9 cross-domain candidates`; only the target transition is attributed here.
- The registration source itself required no change in this lane. Its GUID `9296e3e24fa840b45a164c196fa08423` remains unique, the 48-asmdef graph has zero cycles, analyzer compilation and Unity comprehensive script validation pass, scoped diff whitespace is clean, and loaded Console remains Error 0 / Warning 0. Save/V18 sources and the ownership override manifest were not edited.

## 2026-08-04 Blueprint research runtime boundary

- The named Research assembly already contained the authoritative `ResearchProjectRuntimeState`, progress ratio/add/restore rules, queue entry mutations, and prerequisite/dependency ordering. The actual cross-domain defect was the scene component directly owning Foundation root-store projection, event publication, debug-cheat lookup, and the final node-state branch matrix.
- `BlueprintResearchApplicationAdapter` now contains only those Foundation-facing connections, while `ResearchProjectCoordinatorRules.EvaluateNodeState` owns the deterministic state precedence: missing, completed, active, queued/suspended, archived shortcut, prerequisite lock, required-blueprint transit/lock, facility lock, then available.
- The pre-injection fallback in the preserved MonoBehaviour is retained for Unity serialization/debug compatibility, but every constructed runtime delegates fact projection to `BlueprintResearchProjectCoordinator` and then uses the named decision path. Moving that projection out restored the runtime from 839 to 741 lines and returned `oversizedTypes` to 0. No research save source changed, and the loaded V5 `ResearchSaveValidation.RestoreProgressRatio(196, 560, 720)` result remains exactly `252`.
- Fresh ownership evidence for the target is `ReviewRequired` with only `mutable-domain-state-shape`, `runtime-service-role`, and `MonoBehaviour-scene-edge`; referenced domains are Research only and target cross-candidate count is 0. The Foundation adapter is `DefaultAllowed` with application-adapter evidence.

## 2026-08-04 Exterior incident authority defect

- The live incident had two clocks: `ExteriorActivityRuntime.TickIncidentStates` decreased `ExteriorIncidentRuntimeState.remainingSeconds`, then every `ExteriorZoneMarker.TickIncident` decreased a second private timer. Reception/patrol work could also clear only the marker while the saved runtime state remained active, and `ActiveIncidents` was sourced from marker copies while `Capture` used runtime states.
- The named generic Aggregate now owns the collection, countdown normalization, handler mutation boundary, active count, and history trim. Marker projection never advances or resolves time and cannot produce save data. Every handler tick/action returns a transition that updates or clears the matching marker after the authoritative state has settled.
- Restore still uses the frozen detached-zone candidate and exact V18 section contract. Publication replaces the Aggregate from candidate incident states and then rebuilds all marker projections; capture and active queries read the same Aggregate collection.
- Fresh ownership reports `ExteriorActivityRuntime` as `ReviewRequired`, referencing Exterior only, with target candidate count 0. `ExteriorActivityApplicationAdapter` is `DefaultAllowed` and contains the Foundation clock/random plus Environment hazard projection. Source gates pass with global candidates 6, oversized 0, `48/0` asmdefs, unique GUIDs, no save-source diff, and no marker incident timer/save source.

## 2026-08-04 Operating-day settlement authority defect

- The previous MonoBehaviour directly mutated every ledger collection and debt field while also scanning Unity buildings/characters, invoking employment and paid-facility settlement, raising alerts, and publishing reports. Repeated `OperatingDayEndedEvent` calls therefore had no domain idempotence barrier before irreversible economy ports.
- The named generic Aggregate avoids a reverse reference to default-owned report and stock-supply presentation types while still owning their history/list transitions. Primitive category IDs cross the boundary and are converted back to `StockCategory` only by the default persistence/report adapter.
- Settlement is a tokenized two-phase domain transition: begin freezes an immutable ledger request, external ports produce an immutable economy application, named rules calculate debt/shortfall effects, alert side effects are reflected into a refreshed immutable snapshot, and completion/history publication is followed by an explicit ledger finish. `LastSettledDay` rejects duplicate settlement before any port call.
- `LastSettledDay` is reconstructed from the newest existing report during the unchanged restore preparation, so the new idempotence state requires no save DTO/version change. Pending tokens are deliberately transient and are never serialized.
- A compatibility facade is necessary because authored scenes serialize the original MonoScript GUID and type name. Keeping logic in the `ApplicationAdapter` lets the analyzer recognize the actual Unity edge; the exact-path facade allowance documents that it owns no field or rule rather than hiding mixed ownership.

## 2026-08-04 Experience pacing authority findings

- The concrete defect was not the pacing DTO. `ExperiencePacingRuntime` directly owned the Aggregate-root lookup plus every day/mask/concept mutation while also resolving authored Content rules and subscribing to a Foundation event. That made one default file both state authority and cross-domain adapter.
- The named Aggregate now makes invalid or duplicate transitions unrepresentable through its command surface. It also validates detached candidates independently of the strict save section, so direct runtime publication cannot bypass the same invariants used by save restore.
- Keeping a legacy three-argument constructor as a partial runtime declaration inside the application adapter initially left that file `ReviewRequired` and in the cross-domain candidate set. Removing the compatibility surface and updating Editor callers made the adapter a recognized `DefaultAllowed` application edge without an override; global candidates fell from `3` to `2`.
- The frozen save wire contract remains payload version `1` within root V18. Capture emits ordered unique concepts; prepare validates before publication; publication revalidates and clones the candidate before replacing the live Aggregate state.
- Focused Roslyn and standalone probes are green. Unity MCP currently reports revoked approval, so current-loaded execution is an integration checkpoint rather than a source-lane failure.

## 2026-08-04 final-acceptance runner coverage findings

| Completion contract | Synchronous runner evidence |
|---|---|
| V18 manifest, authority, 54 strict sections | `RuntimeAuthorityV18Validator`, `DungeonSaveSectionDebugScenarios`, Batch A/B/C, Offense aggregate V18 |
| Authored content authority | localization synchronization/validation plus `BatchAContentAuthorityDebugScenarios` |
| Physical item, stock, equipment state | persistent identity, physical item/stock, equipment component/material, combat and research-equipment suites |
| Branched production and facility fuel/feed | Batch C, branched-network graph/value/distribution/save validation, production economy, industrial infrastructure |
| 168 research and equipment growth | research tree plus `ResearchEquipmentOverhaulDebugScenarios`, including 43 equipment, 20 modules, locks, module process/save, drops, and pacing |
| Exterior, OperatingDay, Experience, Service, DungeonRun | direct Exterior/OperatingDay/Experience/Service entries plus Batch A integrated CoreSession RunFlow/save fixture |
| Combat, medical, survival | combat and strict combat save, surgery/anatomy integration, Batch B and survival suites |
| Implemented game loops | broad implemented-scenario runner plus direct strategic physical expedition, journey, architecture, and Offense aggregate validation |

- The runner previously invoked OperatingDay only as one nested item inside `ImplementedScenarioDebugRunner`; the new direct entry makes the recent idempotence/ledger authority result independently visible in the final report. The new composition entry similarly makes the VContainer/Unity edge contract explicit.
- The runner is intentionally not a PlayMode or visual harness. Its report now names the deferred Unity MCP resolution/capture/Console gate, preventing a synchronous pass from being presented as complete UI acceptance.
- No callable synchronous scenario currently executes equipment history transfer end-to-end or proves expedition-death co-loss of an item and its installed modules. The research-equipment fixture covers module appraisal/restoration/install/remove damage and V6 save, but not lineage transfer.
- Combat production code implements reload, smoke exposure, and durability-based misfire, but the callable combat suite does not assert smoke/misfire or bow/crossbow/gun non-dominance scenarios. Live 54-section world round-trip and repeated scene/run static isolation also require the loaded PlayMode integration path. These gaps were not replaced with source-token assertions in the final runner.

## 2026-08-04 Dungeon run-flow authority findings

- `DungeonRunFlowRuntime` mixed Aggregate-root writes with authored pacing rules, Experience rehearsal coordination, invasion scene mutation, owner-run completion, alerts, and restore projection. The original type/GUID is serialized compatibility surface, so moving or renaming it would be unsafe.
- `DungeonRunFlowReducer` now receives one event and immutable authored rules, returns a replacement state plus an ordered effect list, and never invokes Unity or external runtimes. Monotonic day handling rejects duplicate and out-of-order days before phase, rehearsal, or boss effects can repeat.
- Rehearsal ownership remains singular in the existing Experience pacing Aggregate. RunFlow owns only the deterministic decision to evaluate that rehearsal and the feedback transition that either suppresses or arms the due recurring boss; it does not add a second persisted rehearsal clock or mask.
- Boss cycle and armed flags are committed before the adapter executes the invasion effect. Repeated scheduling feedback therefore produces no second arm/force effect, while boss start, boss defense, truth completion, owner completion, and post-finish days are also idempotent.
- The frozen save seam remains root V18 with `run.flow` payload V2, `LateRuntimeState`, Offense/Invasion dependencies, detached `BuildRestoreCandidate`, and single `PublishRestoreCandidate`. No new persisted reducer-only field or migration was introduced.
- The adapter is automatically `DefaultAllowed`; the fieldless runtime facade has a reviewed exact-path allowance rather than hidden domain state. Both targets are absent from the fresh cross-domain candidate set.

## 2026-08-05 final offline integration audit findings

- The final runner still has exactly 33 named steps. No extra top-level step was necessary: `PhysicalItemDebugScenarios.RunAll` now executes real queued lineage transfer work and verifies source equipment/seal consumption plus target physical properties/modules; `CombatSystemDebugScenarios.RunAll(false)` executes durability misfire, smoke exposure, ammunition, penetration, reload, cadence, and bow/crossbow/gun role assertions; `OffenseExpeditionDebugScenarios.RunAll(false)` delegates to `OffenseJourneyDebugScenarios`, whose death scenario calls the actual `OffenseExpeditionReturnPort.HandleMemberDeath` path and verifies equipment/module co-loss.
- The first focused compile exposed two real integration errors in the new regressions: a nonexistent `EquipmentEvolutionDirection.Defensive` value and a missing `DungeonStory.Foundation` import for `GameEventBus`. The owning random-stream lane corrected both to `Protection` and the proper import; the scoped source diff then passed.
- Fresh ArchitectureMetrics after those corrections reports `1,380 files / 4,275 types / 822 default / 141 allowed / 401 named / 280 review / 681 unapproved / 0 cross-domain candidates`. Mutable statics, oversized types, large constructors, content escapes, and direct session mutations are all `0`.
- The 49-asmdef graph has zero cycles. Four unresolved GUID-form asmdef references belong to the external `DamageNumbersPro` package and are outside the Assets-only name map; they are not graph cycles. All C# source metas exist and all 6,817 parsed asset GUIDs are unique.
- The final runner passes focused Assembly-CSharp-Editor compilation. A broader offline Editor compile cannot substitute for the root loaded gate because shared Bee reference artifacts are stale/overwritten (`Assembly-CSharp-exterior-check` and `ExperiencePacingAggregateProbe`) and do not expose the current environment interface; Unity reload must regenerate them.
- Global `git diff --check` is not clean: it reports 1,502 trailing-whitespace lines across 32 pre-existing/shared Unity-generated files, dominated by `GameplayScene.unity` (1,406) and `DungeonStory.slnx` (43). The audited runner, three new regression sources, architecture manifest, and planning documents pass scoped diff checks. These unrelated serialized files were deliberately not rewritten during the concurrent audit.

## 2026-08-04 final evidence-gap closure findings

- The lineage authority was already production-ready but unproven end to end. The new physical-item regression uses the actual seal stack, queue/work APIs, repository-backed equipment instances, evolution state, and module runtime instead of a definition or source-token check.
- The expedition return port is the authoritative death bridge. Exercising that port exposed no product defect: the equipment loadout runtime already marks both the unique equipment item and each installed module Lost as one death consequence.
- Gunpowder smoke did contain a product defect. `CombatResolutionService` previously placed `SmokeExposure` into target `Suppression` only for a misfire and emitted no smoke on normal hits or misses. The result contract now has a separate immutable smoke field, centralized result normalization attaches smoke to every executed gunpowder outcome, and `CombatResolutionService.Record` applies the exposure exactly once. Applying it from `CombatCommandResultApplier` was incorrect because Defense, Wildlife, Offense, and Circus share the resolver but do not all share that later applier.
- Offense ally attacker IDs are persistent character IDs and therefore resolve through `ICharacterWorldQuery`; generated enemy IDs do not. `CharacterEnvironmentRuntime.AddAirborneExposure` now rejects an ID with no live actor, preventing a smoke result from creating a phantom saved character-environment entry.
- The full-world facade's earlier `baselineRestored=true` was only a scenario-return assumption. It now compares canonical 54-section captures before and after execution, treats mismatch as a test failure, and performs a separately verified restore only for cleanup. Its Console capture also starts at PlayMode transition before gameplay scene composition and excludes stale EditMode history.
- The save round-trip fixture still expected a section-version-1 owner-doctrine fallback even though the V18 run-variable payload contract is strictly current V3. The fixture now injects an explicit V2 payload under the current section envelope, requires version rejection, and proves the failed restore leaves the canonical live state unchanged.
- `ICharacterEnvironmentExposureCommand` is the minimal mutation capability for this boundary. Its implementation clamps airborne exposure, refreshes the physiological band immediately, remains part of the existing environment Aggregate state, and adds no save field or V18 contract change.
- The synchronous final runner remains 33 steps because all three regressions are reached through existing composite entries. The 54-section live-world requirement is intentionally a distinct PlayMode facade so an offline Editor pass cannot impersonate live scene/container restoration evidence.
- The only unresolved verification is environmental, not a known source defect: Unity must regenerate stale Bee references before loaded compile/run. The isolated facade and smoke-focused compiles pass, while the stale whole-default response fails first on concurrently moved Operation/Exterior/Service/Run sources.

## 2026-08-05 final PlayMode facade static follow-up

- At this checkpoint the final coordinator covered Resolution, Research, Production, Service Room, Character Summary/Medical, and Full World, but did not yet include direct equipment/expedition evidence. This checkpoint was superseded later the same day by the current seven-target/30-capture matrix.
- `CharacterProgressionSavePlayModeFacade.Run` had no caller, so its ownerless/invalid-cell/rollback-free-late-failure and staff work-state round-trip contracts were not part of final acceptance. The existing Full World target now invokes it before the broad 54-section scenario and requires its result; the later equipment/expedition expansion owns the current target/capture contract.
- Resolution explicitly waits for an owner and a closed owner-selection surface before HUD checks. Research and Production use the 45-second party driver. Full World and Character Summary use synchronous fast commit followed by frame settling; their clearest remaining runtime risk is an owner/preparation composition failure, which will surface as an explicit target failure rather than a false pass.
- No Unity or MCP process was used. Local command-line compilation remains unavailable because neither `dotnet` nor the Visual Studio MSBuild installation can resolve `Microsoft.NET.Sdk`; loaded Unity compilation remains the required next gate.

## 2026-08-05 equipment/expedition final UI evidence closure

- The earlier final coordinator had no direct equipment or expedition evidence at the two required resolutions. Existing offense verifiers either lacked pointer input, ran the pointer flow outside their responsive matrix, or emitted a report marker incompatible with the final coordinator.
- Added a seventh `EquipmentExpeditionUiMatrix` target with four required fresh captures: equipment and expedition at both `1600x900` and `900x1600`. The final coordinator contract is now seven targets and 30 captures.
- The equipment matrix uses the authored `EquipmentProgressionCommandPanel` stable object names and Unity `EventSystem` pointer events to execute appraisal, restoration, rune tuning, installation, removal, lineage source/target/seal selection, and lineage confirmation. It asserts the lineage order was actually queued.
- Equipment instances and the lineage seal are materialized as physical items. Physical-item and equipment runtime snapshots are restored between resolution rows and during cleanup; the outer final coordinator remains the persistent-state snapshot authority around the PlayMode target.
- The verifier also captures the canonical `research.blueprints` and `offense.aggregate` save sections before seeding. Both sections are restored and compared byte-for-byte with their original captures before every resolution row and during final cleanup, so a standalone run cannot leave completed research or expedition/campaign state behind.
- Each resolution row explicitly clears the transient expedition and battle runtimes after restoring the same offense baseline. Both rows therefore start from an empty verifier-owned expedition session instead of inheriting the previous row's route/battle progress.
- The expedition matrix uses the live expedition panel and pointer-clicks a non-close journey action, requires the expedition phase/node to change, checks panel bounds, and captures the resulting surface at both resolutions.
- No Unity, MCP, helper, or operating-system mouse automation was used in this source-only lane. Direct Roslyn compilation passed for the current default runtime RSP plus the new progression panel and for the Editor RSP plus the new matrix verifier against that fresh runtime DLL. Loaded Unity PlayMode execution remains the root gate.

## 2026-08-05 final coordinator dirty-scene safety

- The only final-coordinator scene switch is `EditorSceneManager.OpenScene(..., OpenSceneMode.Single)`. It previously ran after persistence capture and evidence cleanup, so a dirty loaded scene could trigger Unity's blocking save/discard modal during unattended acceptance.
- Before the request is admitted, the coordinator now validates every distinct scene path required by the full suite against the currently loaded scenes. A dirty active Title scene therefore fails immediately because a later Gameplay target requires a switch; it cannot run Resolution first and fail late. This preflight occurs before state creation or persistence capture.
- `StartCurrentTarget` also validates each actual transition before any target-side mutation. A switch is rejected when any loaded scene is dirty, including an untitled scene whose path is reported as `<unsaved>`; no scene is saved, discarded, unloaded, or overwritten.
- `OpenScene` repeats the same validation immediately before the Unity API call as a defensive boundary. When the requested scene is already active, the existing no-open path is preserved. With clean loaded scenes, the existing seven-target/30-capture sequence is unchanged.
- A preflight rejection declares persistence restoration not required and does not touch a previous snapshot. Mid-run failures retain the existing captured-snapshot restore path.
- No Unity, MCP, helper, or operating-system input was used. Direct Roslyn compilation of the current Runtime and Editor response sets passed with zero errors.

## 2026-08-05 authored equipment-progression facility evidence

- The equipment matrix no longer renders every progression command on one arbitrary forge. It instantiates the authored RF42 appraisal table, RF43 restoration bench, RF44 precision fitting bench, I17 rune tuning room, and I18 lineage archive, plus S08 as a negative control.
- One grade-4 module is a real `item:equipment-module` unique physical item. The verifier routes the same stack through each facility-local `FacilityBuffer`, requires the destination to equal that facility's persistent ID, and pointer-clicks appraisal, restoration, rune tuning, installation, and removal only after delivery.
- Every facility render proves its allowed command prefixes are present and all other progression command prefixes are absent. The S08 forge must expose none of them.
- Precision installation requires both module and target equipment in RF44's local buffer. Installation must absorb the standalone module stack without marking the module lost; removal must recreate a standalone stack with the same module instance ID in RF44's buffer and apply the replacement/removal condition loss.
- Lineage source equipment, target equipment, and the regional seal are all routed to I18's local buffer before pointer selection and confirmation. Work is applied through I18, then source/seal consumption and target history inheritance are checked.
- The verifier emits `FACILITY_FLOW=RF42,RF43,RF44,I17,I18`; the final coordinator now requires that marker, so an older shallow equipment report cannot satisfy final acceptance. The suite remains seven targets and 30 captures.
- `item:equipment-module` is now an authored max-stack-one item registered exactly once in `ItemDefinitionCatalogSO`. An unattached module is saved as its own unique physical item with a typed `ItemInstanceId`, `sourceStackId`, and strict module component payload; installed modules remain embedded only in their equipment payload, and validation rejects detached/attached duplication or broken stack links.
- Destructive stack deletion and full consumption move a detached module to `Lost`, while the dedicated installation absorption path deliberately avoids the loss transition. Removal and occupied-slot replacement rematerialize the same module instance in the precision-fitting facility buffer with the required condition loss.
- The three former facility-less debug callers now use real facilities and physical buffers. Fresh Foundation, Items, Combat, default Runtime, and full Editor Roslyn compilation all pass with zero compiler output. Fresh ArchitectureMetrics also passes at `1,384 files / 4,314 types` with mutable statics, oversized types, large constructors, cross-domain cycle candidates, content escapes, and direct session mutations all `0`.
# 2026-08-05 post-Copilot acceptance findings

- The 18:50 final acceptance artifact is not current completion evidence: it reports 29/33 and predates a fresh Unity import that found source compiler errors.
- `DungeonGameRestoreReport.Success` is derived from its private error list and is intentionally read-only. Validation failures must flow through `AddError`; test diagnostics must not mutate the result flag.
- The temporary `Run V14 Combat Scenarios` menu had been narrowed to only the V18 body-health test, making the menu label and executed coverage disagree. It must always invoke the full combat suite; focused diagnostics may use separately named commands.
- Combat failures were previously flattened twice (`Combat/body-health fixture failed` and `Scenario returned false`). Returning a concrete failure collection from the fixture preserves exact scenario evidence without static mutable diagnostic state.
- `item:equipment-module` must remain non-craftable expedition loot. Its dependency graph needs authored external reward producers and real appraisal/restoration/fitting/tuning consumers; a fake recipe, sink, or item-ID skip would violate the production-graph contract.
## 2026-08-06 Phase 120 CharacterId integration audit

- Fresh loaded gates passed before repair (`Architecture 131/131`, transactional restore `33/33`, synchronous final acceptance `33/33`), but they did not cover operational actor creation followed by save capture and restore.
- Faction reinforcements are now created as `character:faction-route:*`, while `FactionPayloadValidation` still requires the former raw `faction-route:*` form. Any run with materialized reinforcements captures a payload that its own restore validator rejects.
- Offense return prisoners are now created as `character:return:*:prisoner:*`, while `OffenseReturnArrivalSaveValidation` still requires the former raw `return:*:prisoner:*` form. A materialized-prisoner run is likewise self-unrestorable.
- The early-V18 compatibility resolver only accepts raw `world:*` and `staff:*` IDs, although the same V18 generation previously assigned raw invasion, faction, return, and exterior incident IDs to `CharacterActor` instances.
- The global reflection normalizer guesses character references by field name and misses concrete save fields including `actorId`, `actorIds`, `preferredDoctorId`, and `doctorId`; it also cannot safely distinguish every generic `targetId`/`persistentId` from non-character IDs. Section/type-scoped normalization is required.
- `FinalAcceptanceReportPolicy.IsFreshPass` accepts any `RESULT=PASS` line and does not reject conflicting or duplicate result declarations, so a failed composite report can be misclassified.
- Content migration safety improved through owned-output saves and dirty preflight, but the provenance input hash omits code dependencies used to generate evolution catalyst definitions, and the root catalog's type-erased fields are only null-checked.
- Final PlayMode evidence remains missing. The active `Assets/Scenes/TitleScene.unity` is dirty in memory, so the project-scoped coordinator cannot safely switch scenes until the user explicitly saves or reverts it.

## 2026-08-06 final PlayMode composition and transition findings

- The first fresh Full World run proved the remaining mass injection failures were cascading from one composition-root cycle, not dozens of missing registrations. `OffenseWorldMapRuntime` eagerly required `IOffensePanelService`, whose constructor required the query/command interfaces implemented by that same runtime.
- The durable dependency direction is Presentation/Application -> campaign query/commands. World-map UI opening was removed from `IOffenseCampaignCommands` and moved to `OffenseApplication -> IOffensePanelService`; the save authority remains `OffenseCampaignRuntime`, so no V18 DTO, section, or restore contract changed.
- Unity PlayMode entry must not be requested in the same `EditorApplication.update` that opens the target scene. The standalone Full World retry demonstrated that this can freeze the Editor at `Entering Playmode`; the facade now returns after opening the scene and lets the next update request PlayMode.
- The editor later recovered and completed the request, so no restart is required. The fresh report narrows remaining work to four contract families: strict `CharacterId` ingress, authored faction/offense region canonicalization, body-health injury projection restoration, and early-V18 regular-customer ID normalization.

## 2026-08-06 Phase 122 closure findings

- Operational early-V18 IDs were a real compatibility surface, not arbitrary runtime strings. Invasion, faction-route, return-prisoner, and incident actors restore under canonical `character:` IDs, so every typed character reference to them must canonicalize before aggregate cross-reference preflight.
- A union field cannot preserve an ID merely because it is not `staff:` or `world:`. The correct discriminator is the exact `CharacterId.TryCanonicalizeV18Restore` grammar; only unrecognized wildlife/building/transaction/runtime identifiers remain untouched.
- Numeric `int.TryParse` alone is not a persistence grammar. It accepts `+1` and `01`, and equipment repair additionally authors a minimum-width `D6` suffix. Validators now reconstruct the exact invariant string emitted by each generator.
- Sequence watermark validation and generation must agree at the maximum value. Restoring `int.MaxValue` or `long.MaxValue` is safe only when the next command fails before state, physical items, reservations, or world actors are mutated.
- Consumables previously shared one prefix between external idempotency keys and automatic IDs. The `auto:v1` namespace removes that ambiguity while legacy exact D16 values remain reserved for V18 watermark compatibility.
- The former equipment/expedition verifier could click a covered or clipped Button by directly invoking pointer handlers. It now proves full ScrollRect visibility, actual top EventSystem raycast ownership, and successful dispatch before accepting the flow.
- The former research verifier proved layout and captures but not the actual detail contract. It now selects `research:equipment:powered-armor` and compares visible detail text against runtime progress, an independent deduplicated prerequisite DFS, work/day estimates, the reward catalog, and the exact lock blocker.
- Fresh non-PlayMode evidence is complete: Unity compile clean, architecture `131/131`, transactional restore `33/33`, synchronous final acceptance `33/33`, and ArchitectureMetrics hard gates all pass. The only missing completion evidence is the fresh seven-target/30-capture/54-section PlayMode matrix and Console `0/0`, which cannot safely start while the loaded Title scene remains dirty.

## 2026-08-06 Full World 54-section restoration narrowing

- The second standalone run preserved `registeredSections=54`, `capturedSections=54`, and `postRoundTripSections=54`; remaining failures are now concrete authored-data and fixture contracts rather than composition or section-registration defects.
- The root content catalog registered two SOs for every dungeon faction StableId: six obsolete shallow definitions and six richer authored definitions. The obsolete registrations were removed, and the runtime adapter now fails explicitly if a duplicate StableId returns.
- Human support sites persisted `region:human-campaign`, but that region does not exist in the strategic world. An empty authored region is the correct contract because strategic-site registration resolves it from the actual tile.
- Empty settlement state is still a valid state, but its persistence contract requires explicit empty collections. Passing null from the QA baseline fixture was invalid test setup rather than a reason to weaken the runtime contract.
- Invalid CharacterId acceptance tests should prove rejection atomicity and identify the exact offending serialized value; coupling the test to a particular validation layer's English phrase produced a false failure when an earlier aggregate validator rejected the same payload.
- Unity YAML list indentation is part of the serialized contract. Four retained faction references were briefly written at column zero, which truncated effective deserialization before `coreSessionRules`; the cascading missing-injection exceptions were downstream symptoms of that single malformed asset edit.
- `DungeonGameSaveDebugScenarios` was rewriting `research.blueprints` with hardcoded section version 3 even though `BlueprintResearchSaveSection` now registers version 5. QA mutation helpers must source version and restore phase from `IDungeonSaveSectionRegistry`, just like production manifests.
- Aggregate rejection was already atomic for `Named Hero`; the remaining contract failure was diagnostic. Including the exact raw ID in preflight errors makes the failing serialized value observable without weakening canonical-ID validation.
- Strict V18 validation correctly rejects QA-only identifiers and split state. A fake recipe ID is not authored content, an operation-variable start day cannot exceed the captured current day, and `facilityDamageCount` must equal the set of canonical damaged `BuildingInstanceId` values.
- Aggregate cross-reference preflight can reject a normalized legacy/canonical collision before the character section's own validator runs. The acceptance contract should assert the exact duplicate canonical ID and atomicity, not require one downstream layer's wording.
- V18's explicit no-migration boundary means a full-game round-trip fixture must not replace an authored current research reward with the removed `recipe_battlefield_dining_2` alias. Testing that alias inside a V18 manifest contradicts the compatibility policy.
- Removing the owner actor invalidates aggregate references before character-section owner-count validation. Either layer is a valid fail-closed boundary; the contract must identify the missing owner and prove the live/staged world stayed unchanged.

## 2026-08-06 physical projection and restore-report findings

- A stored equipment instance is not valid without a max-stack-one physical stack and `sourceStackId`. QA setup must use the same physical-item materialization/link path as production instead of inserting only into the equipment dictionary.
- `DungeonPhysicalItemSaveData.stacks` and `uniqueItems` are complementary projections of one item identity: the former owns location/quantity and the latter owns versioned unique components. Matching `ItemInstanceId` values across those collections are required, not duplicates; uniqueness is enforced within each collection.
- `DungeonCandidateSaveRestoreStage` records counts only when its candidate implements `IDungeonRestoreReportContributor`. Offense and invasion candidates previously published correct state but left `RestoredExpeditionCount` and `RestoredIntruderCount` at zero, producing false-negative round-trip reports.
- Authored NPC staff content is a real boot/runtime dependency. Creating a temporary `CharacterSO` in Editor QA concealed the catalog gap and violated the SO single-authority rule; the root catalog now contains an explicit staff definition.

## 2026-08-06 final integration findings

- Localized failure contracts cannot be validated by searching rendered English words. Production verification now checks locale-neutral structure and parameters, while domain code supplies only the parameters declared by `ProductionMaterialsMissing`.
- Market sale is a real dependency-graph consumer, but its demand query must be independent from the mutable stock-policy runtime. Inheriting the query interface from the runtime made VContainer select the runtime during query resolution and created a composition cycle. A read-only projection plus `RoutingOwnedExternally` exposes demand without duplicating hauling or settlement commands.
- Restore publication must not run normal-session population maintenance. Immediate replenishment after publishing a restored Character World changed the state before the canonical recapture and made an otherwise valid transactional restore appear non-deterministic.
- Non-interactive TMP labels created over buttons still receive raycasts unless explicitly disabled. Setting generated static labels to `raycastTarget=false` restored actual EventSystem top-hit ownership instead of weakening the verifier.
- A pointer verifier must wait after rebuilding a dynamic UI surface. Same-frame clicks observed stale layout/raycast geometry; a capture-ready frame boundary made the test match player-visible state.
- TMP's default Liberation Sans fallback was not valid Korean evidence and emitted thousands of glyph warnings. Verification must resolve the same Korean font service used by production UI.
- Unity layout groups can dirty serialized RectTransforms during verification even when gameplay state is restored. Cleanup is safe only for exact diagnosed residues and only after a save-as-copy byte comparison proves the scene matches the on-disk asset; the final cleanup met that condition.
- The final integrated evidence is authoritative: seven of seven targets, 30 fresh captures, Full World `54/54/54`, canonical persistence restoration, and Console warnings/errors/exceptions/asserts `0/0/0/0`.
# 2026-08-06 V19 life-simulation implementation baseline

- The worktree is clean before V19 work. The accepted V18 baseline has 54 staged rollback-free save sections, seven final PlayMode targets, 30 captures, and Console Error/Warning 0/0.
- `IGameCalendar` and `GameCalendarRuntime` currently expose day/hour/time-of-day only and hardcode 180 seconds per day. They have no year, season, climate front, or regional time projection.
- `CharacterRuntimeProfile` currently retains `CharacterSO Source`, `CharacterSpeciesSO`, and trait SO references; character saves contain no age, kinship, household, reproduction, disease-immunity, grief, trauma, or career state.
- Existing mood memory caps each factor independently, so one grief factor per deceased would bypass the approved aggregate -20 cap. V19 must project one aggregate grief factor.
- Existing A* already has `IGridTraversalCostPolicy` and `GridTraversalContext`, while environmental cells expose temperature/air/light plus a version. Child safety belongs in a typed actor-aware traversal policy and cache key, not in job filtering alone.
- Crop plots currently persist growth/water/yield only. Seed lots, fertility, rotation, pests, disease, and cultivar genomes require a new Aggregate while physical seeds remain under the item repository authority.
- Existing body-health owns infection and anatomy damage. Named pathogens, immunity, and outbreaks must project into that authority rather than introducing a second health pool.
- Current research assets are exactly 168 nodes ending at 7247. The approved 48-node manifest reaches 216 nodes; the 7271 prerequisite closure is 108 nodes / 95,448 work / 964.1 effective days.
- V19 is a deliberate new-game-only generation. V18 and below must be rejected before any live-state mutation with the approved incompatibility message.
- The current domain boundary supports an incremental V19 cut: CoreSession is engine-free, Characters owns typed `CharacterId`, Species owns authored species SOs, and Grid owns traversal-policy contracts.
- `CharacterSpeciesDefinitionSO` currently requires needs/environment/anatomy/incident content but has no required life-history or reproduction content. Those definitions belong in the same fail-closed catalog validation path.
- `GridTraversalContext` still stores a Unity `Object` and hashes it with `GetInstanceID()`. V19 must replace that cache/policy identity with `CharacterId`, movement intent, and a safety authorization token.
- Unity requires each serialized `ScriptableObject` type used as an asset to have a matching source filename/MonoScript. Grouping V19 SOs in one file compiled but created `m_Script: {fileID: 0}` assets, so the content contract now enforces one SO type per source file and repairs only its own newly generated population assets.
- Typed traversal contexts now carry `CharacterId`, movement intent, safety authorization, and environment/combat/life/policy revisions; Unity `Object` and `GetInstanceID()` are no longer part of the path key. Supervised routes deliberately bypass path-result caching so a moved supervisor cannot leave a reusable stale route.
- The existing default-assembly `CharacterDeathEvent` still carries `CharacterActor` for legacy consumers. The V19 serializable ID-only payload is therefore `CharacterLifeDeathRecord`; a later application adapter must translate the legacy Unity event once instead of allowing two same-named authorities.
- The environmental simulation has no authored fire authority yet. V19 hazard routing exposes an explicit overlay command, projects active combat and severe filth now, and leaves fire publication to the future fire Aggregate rather than inventing hidden state.
# V19 climate and population-health findings (2026-08-06)

- The approved seasonal curve can live under the existing absolute calendar without creating a second time authority. Five climate-zone SOs and six weather-front SOs now project deterministic daily weather from the calendar and random-stream state.
- Disease exposure must remain room/cell aggregated. The runtime therefore records one weighted exposure batch per disease and room instead of comparing every character pair.
- `condition:core-corrosion` cannot be represented honestly as a zero-duration contagious disease. It is now an explicitly chronic, non-contagious environmental condition with separate apply and maintenance-removal commands; it never creates vaccine or epidemic state.
- Existing body-health anatomy nodes remain the only physical-health authority. Population health emits daily burdens and the application adapter projects them into the matching breathing, digestion, filtration, consciousness, or core anatomy node.

# 2026-08-06 V19 character profile and aging-treatment findings

- `CharacterRuntimeProfile` can be value-only without moving authored stat calculation into runtime state: the factory resolves the root-catalog SOs once, copies immutable gameplay values, and returns a profile containing IDs and values only.
- The root catalog had 14 character archetypes but one legacy Adventurer archetype had no species definition. Treating that as a default player species would silently corrupt life rules, so Adventurer is now explicit enemy-only authored content while the nine player species remain unchanged.
- Long-term aging care must alter the daily life transition, not rewrite captured age afterward. Rune hibernation therefore applies a 0.25 biological-aging multiplier, chronic care freezes condition progression, and temporal stasis blocks both aging and new age-condition rolls only while its facility and power contract are currently valid.
- Temporal stasis maintenance runs before the daily life tick. A supply or power failure changes the effective mode to normal for that day and never creates retroactive catch-up aging.
- Whole-body regeneration follows the approved severity contract exactly: mild/moderate conditions resolve, severe conditions drop two stages, and critical/organ-loss states are preserved for the body-health/surgery authority.
- `CharacterDeathEvent` is now an ID/day/location/witness value payload. Actor lookup exists only in application adapters, so death persistence and social simulation no longer retain Unity objects.
- Age-condition severity changes now damage authored anatomy nodes through the existing body-health Aggregate. Fatal age-condition organ failure carries an explicit cause; the owner exception clamps the same authoritative health state to one instead of creating a second vitality authority.
- Kinship cold archival must prune links before tombstones. Otherwise a removed tombstone leaves an invalid saved relationship reference. The implemented order preserves parent edges reachable within depth three from living characters, retains recent deaths for 120 days, and only then aggregates unrelated old deaths by household/generation.
- Reproduction completion previously stopped at `Completed` and had no world publication consumer. A daily application adapter now advances processes, publishes one value-profile character, registers newborn/golem life state, writes parent or guardian links, and stores `resultCharacterId`; this ID is preflight-validated and prevents duplicate births after restore.
- Existing authored characters were being created with `ReproductiveRole.None`. Publication now deterministically derives the applicable role from the persistent CharacterId and authored reproduction mode, then rebuilds the immutable runtime profile before life registration.
- Whole-body regeneration used to change only biological-age and condition state. It now prevalidates all authored anatomy targets before consuming the physical treatment item and repairs the exact accumulated mild/moderate or severe-to-mild health fraction afterward.

## 2026-08-06 V19 funeral, career, and physical disease-route findings

- Funeral and festival rules cannot be represented as mood-only calls. The application service now requires the deceased's authored funeral culture, a live tombstone, living participants, and a built memorial facility with the exact semantic ritual tag before grief is converted.
- Career retirement needs enforcement at both assignment and continuation boundaries. Checking only the work picker leaves direct orders and already-running unsafe work as bypasses, so the same policy is applied by the handler registry and ongoing duty controller.
- Mentoring reuses the existing character progression XP authority and persists only assignment/idempotency state. It never stores a second skill ledger or copies active skills.
- Population health already owned probability, immunity, outbreaks, and anatomy burden, but only ambient air/droplet exposure reached it. Contaminated meals and successful world-water consumption now publish physical exposure, while the water aggregate persists a concrete disease ID and rejects non-water disease definitions.
- Slime contamination is now a real species incident handler. It creates physical filth and deterministically contaminates the nearest real water source within four cells with `disease:slime-blight`; no synthetic global infection scan is used.
- 2026-08-07 V19 cohesion review: line count is now a review signal rather than a decomposition command. `CharacterActor` 819, `CharacterBodyHealthRuntime` 1,291, and `DungeonAggregateReferencePreflight` 1,623 are cohesive Unity facade, health application/Aggregate boundary, and atomic cross-Aggregate preflight respectively. `PhysicalAgeTreatmentRuntime` needs ten explicit authorities to keep item consumption, life mutation, anatomy repair, facility/power validation, and calendar maintenance visible and atomic; a dependency bag or split command would weaken the design.
- Fresh ArchitectureMetrics passes at `1,431 files / 4,532 types / mutable statics 0 / review types 3 / hard oversized 0 / review constructors 1 / hard large constructors 0 / content escapes 0 / direct session mutations 0`.
- V19 short definition files are Unity-authored SO boundaries, the save sections are already co-located, and the event/application adapters perform real cross-domain projection rather than forwarding. No new V19 merge candidate was found.
- The current synchronous final acceptance is `33/33 PASS`. The final project-local Unity MCP PlayMode request was safely rejected before state capture because dirty `Assets/Scenes/GameplayScene.unity` would be unloaded by the required switch to Title. The rejection report proves `consoleWarnings=0`, `consoleErrors=0`, `consoleExceptions=0`, and no persistence snapshot was required; it is not final 32-capture evidence.
- The first standalone V19 UI retry exposed four stale root objects named `RegularCustomerRuntime_Test`/`RegularCustomerRewardPromotion_Test`. Those QA objects made the strict composition query see five `RegularCustomerRuntime` instances and disabled the LifetimeScope; all other injection exceptions were cascades. The four exact QA roots were removed with Unity Undo, `VerifyRuntimeEvents` now uses `try/finally`, and two consecutive scenario runs leave `runtimeCount=1; debris=0`.
- After cleanup, the standalone character-summary/medical matrix passes at both target resolutions with six fresh Unity captures. Population text includes life, kinship, disease/immunity, career, and child-safety sections; all tab/button/modal flows use EventSystem hit tests and dispatch. The verifier captured `errors=0`, `warnings=0`, and `RESULT=PASS`.
- Stale Console history from the rejected composition attempt was cleared with `Unity_ReadConsole(Action=Clear)` through the project-local MCP bridge. A fresh Error/Warning query returned zero entries; this is the current stopped-Editor baseline, while the separate seven-target/32-capture coordinator remains pending the dirty-scene gate.

## 2026-08-07 V19 final integration findings

- Asset count is not catalog authority. The project had 216 `ResearchProjectSO` assets while `GameDomainContentCatalogSO` exposed only 168, so runtime/UI validation correctly failed. Research rebuild now refreshes only the research slice of the authoritative catalog and preserves every other curated definition.
- A broad `Resources` reindex is unsafe when legacy shadow assets still exist. Six shallow dungeon-faction assets duplicated the six canonical authored StableIds and caused downstream composition failures; full rebuild filters those exact legacy shadows while runtime duplicate validation remains fail-closed.
- Responsive route editors need density policy based on actual branch count. The presenter selects compact rows for more than six consumers, and the factory remains the sole owner of row geometry. This fixes 11-route portrait visibility without merging policy and Unity construction responsibilities.
- Final-target timeout begins before Unity enters PlayMode, so it includes blocking scene activation time. A 900-second limit was invalid once warm-editor scene integration reached 915 seconds. The 1,800-second limit is not a weaker gameplay test; it prevents infrastructure time from preempting the verifier.
- Timeout resume is safe only when it revalidates the failed report, exact sequential PASS progress, fresh verifier reports and PNG dimensions, required report markers, persistence restoration, and a zero-warning/error/exception/assert console record. Any functional failure or stale/missing evidence rejects resume.
- Final authoritative evidence is now complete: Architecture `154/154`, Transactional Restore `33/33`, synchronous acceptance `33/33`, Full World `63/63/63`, research 216, final targets `7/7`, captures `32`, persistence restored, and Console `0/0/0/0`.
- The final cohesion scan remains `1,431 files / 4,532 types / mutable statics 0 / review types 3 / hard oversized 0 / review constructors 1 / hard large constructors 0`. Current review sizes are `CharacterActor 819`, `CharacterBodyHealthRuntime 1,291`, and `DungeonAggregateReferencePreflight 1,623`; no new over-separation merge candidate was introduced.
# 2026-08-07 V20 content-density implementation baseline

- The approved V20 scope keeps the existing 216 research nodes and adds exactly 450 hand-authored definitions; bulk template-generated content and research exclusivity are explicitly out of scope.
- V19 is already functionally complete with the final synchronous and Unity MCP gates recorded as passing. V20 is a new compatibility generation, not a continuation of unfinished V19 feature work.
- The authority boundary is fixed: ScriptableObjects own immutable authored content; plain C# Aggregates own mutable run state; save DTOs contain IDs and values only; root catalog registration is mandatory; missing content fails loudly.
- The requested runtime additions are character narrative, society events, faction campaigns, SO-authored enemies and encounters, ecology/health/cultivar expansions, nine non-terminal milestones, and deterministic EndlessAge composition.
- The existing worktree contains extensive user-owned changes and generated QA evidence. V20 implementation must use narrow patches and must not reset, normalize, or overwrite unrelated files.
- The persistent planning catch-up detected the approved V20 plan and implementation request as unsynchronized context. Phase 124 was added to the root plan rather than replacing the completed V19 history.
- Existing V19 catalogs currently hard-fail on exactly eight disease definitions and exactly four festivals. V20 must deliberately version those validators to 16 diseases and 16 festivals rather than merely adding assets.
- The project already has named Content, Offense, Factions, Wildlife, Run, Characters, Items, Economy, CoreSession, Infrastructure, and Foundation assemblies. V20 should extend these existing domain boundaries and avoid introducing a new assembly for every content subtype.
- Existing save sections implement strict preflight, detached staging, and rollback-free publication contracts. The five V20 sections must follow this established pattern and dependency topology instead of creating an alternate persistence framework.
- `GameDomainContentCatalogSO` stores heterogeneous immutable definitions and already exposes typed `GetAll<T>()`; V20 can register new SO types without adding parallel root catalogs.
- `CharacterTraitSO` currently exposes only stats, model modifiers, combat abilities, and thermal protection. V20 requires authored behavior/mood/event-weight consequences to satisfy the approved content contract.
- `OffenseEncounterSO` currently stores only strength range, elite/boss flags, and enemy count entries. Objectives, battlefield modifiers, deadlines, and reward/counter tags require a schema extension.
- Recoverable inspection error: initial guessed locations for `HeritableTraitDefinitionSO`, `FestivalDefinitionSO`, and wildlife definitions were wrong. The real files are under Species/Core and Wildlife/Core; the failed read changed nothing and those guesses will not be repeated.
- `HeritableTraitDefinitionSO` currently contains only ID, name, aptitude modifier, and compatible species tags. V20 must add category, incompatibility group, gameplay stat/mood/behavior consequences, and validation while retaining the existing fields for migration.
- `FestivalDefinitionSO` currently contains only ID/name/date and a grief-conversion flag. V20 must add physical input, facility, participation, and outcome records; the existing four assets need explicit migrated values.
- Wildlife definitions are currently converted to immutable runtime definitions with diet, habitats, needs, predation, husbandry, and yields. V20 ecology can extend this existing contract with nesting, seasonal activity, migration, disease-vector, and prey links rather than introducing a second wildlife state authority.
- The current `OffenseEncounterCatalog` is confirmed as a hard-coded enemy/ability content authority. Its ally projection is reusable, but enemy templates and abilities must be moved to root-catalog SO definitions and the static enemy factory removed after the runtime adapter is connected.
- Root save version is `DungeonGameSaveData.CurrentVersion = 19`; the approved V20 change point is `InfrastructureSavePrimitives.cs`. Existing QA and full-world constants still assert 63 sections and must move to 68 with the new manifest.
- Recoverable inspection issue: a broad registration search returned a non-zero pipeline status because of output limiting, while still producing valid matches. Future registration reads will target concrete files instead of broad pipelines.
- `IGameContentDefinitionSource` is the correct read-only authored-definition port and `ResourceGameContentCatalog` already projects `GameDomainContentCatalogSO` through it. New content catalogs should depend on this port rather than on Resources or the root implementation.
- Character life domain state is consolidated in `Models/Species/Core/CharacterLifeDomain.cs`, while the V19 section adapters are consolidated in `Services/Infrastructure/Save/V19SimulationSaveSections.cs`; guessed one-class-per-file locations were incorrect.
- Recoverable inspection errors: two guessed Character Life file paths did not exist, and one filename-pattern search returned no matches. A class-declaration search resolved the actual consolidated files; no files were changed by the failed reads.
- V19 save sections use `DungeonStrictJsonSaveSection<TPayload, TCandidate>`: raw JSON shape is checked before deserialization, a detached candidate is built during staging, and commit only publishes that validated candidate. V20 sections will use the same base and implement required-array checks.
- `CharacterLifeRuntime` uses `DungeonRuntimeAggregateRootStore` with read/current and clone-on-write access. Character narrative should use the same root-store publication model so full-world restore can swap one staged aggregate root atomically.
- Recoverable inspection error: the guessed standalone `DungeonRuntimeAggregateRootStore.cs` path does not exist. Its declaration will be located by type search before use; no mutation occurred.
- `DungeonRuntimeAggregateRootStore` is defined with the save contracts in Foundation, and VContainer already registers it once for all aggregate domains. Narrative and campaign state should be added to this shared root, not to new singleton dictionaries.
- `CharacterLifeApplicationAdapter` establishes the approved daily event pattern: subscribe through `IGameEventBus`, mutate the Aggregate once at day end, and publish value events. The narrative scheduler can follow it without per-frame evaluation.
- `DungeonCharacterRegistration` is the correct composition point for narrative catalog/runtime/adapters; save section registration remains centralized in `DungeonSaveRegistration`.
- Unity MonoScript stability requires each new concrete SO type to remain in a matching source file. Shared requirements, effects, choices, and metadata are consolidated in one contracts file to avoid artificial one-record files.
- The active project version is Unity `6000.3.8f1`. The main Editor process is responsive, but its Editor log and Offense assembly predate the V20 edits, so no compile success can be claimed until an explicit asset refresh occurs.
- The project-local player bridge is not a substitute for Editor commands and was unavailable during the first refresh check. No OS input or additional Unity process will be used to force import while the project lock is held.
- Project policy intentionally disables automatic Unity MCP registration and provides `tools/unity-mcp/Invoke-ProjectUnityMcp.ps1`, which resolves only the live Editor for this exact project and serializes an explicit tool call. This is the approved root-only Editor path.
- `Invoke-ProjectRefresh.ps1` performs a forced synchronous `AssetDatabase.Refresh` followed by a clean script compilation request through Unity MCP. It does not use OS input or start another Editor, so it is the correct compile gate.
- Unity's current MCP package exposes `Unity.RunCommand` and `Unity.GetConsoleLogs`; there is no separate refresh tool. A RunCommand-triggered domain reload necessarily disconnects the relay before its JSON-RPC response is flushed, so compiler success must be read from the actual Editor/Tundra output.
- Unity republishes its project-scoped connection at `C:\Users\vulpo\.unity\mcp\connections\bridge-ca5ada59-34544.json` after reload. Discovery retries should key off this exact file/Editor log rather than recursively scanning temporary directories.
- The ten current life-history species IDs are the exact case-sensitive tags `Adventurer`, `Beastkin`, `Demon`, `Golem`, `Harpy`, `Kobold`, `Myconid`, `Orc`, `Slime`, and `Vampire`. `CharacterSpeciesId` stores these tags directly, so default culture authoring must use them rather than invented `species:*` identifiers.
- Phase 124's first authored asset batch can safely live under a dedicated `Assets/Resources/SO/V20/Narrative` subtree. `GameDomainContentCatalogSO.SetDefinitions` sorts and de-duplicates references, so the builder can replace only the five V20 narrative types while preserving every unrelated user-owned catalog entry.
- The five narrative definition contracts already enforce the key authoring invariants: backgrounds require a memory, ambitions require a positive target and reward, major life events require 2-4 mechanical choices, automatic events require effects, cultures require exact 120-day assimilation and etiquette, and practices require a parent culture and success effect.
- The narrative authoring manifest now has an explicit count guard for 12/18/32/10/20 and separately asserts the 20 major plus 12 automatic event split. Its catalog update filters only the five owned V20 types, preserving unrelated definitions in the dirty root catalog.
- Unity's editor command completed the 92-definition asset transaction without a domain disconnect. This confirms the dedicated V20 subtree and type-scoped catalog replacement work with the current root catalog.
- The current save registry has 63 sections and central registration in `DungeonSaveRegistration.RegisterSections`. V20 can reach the required 68 without replacing existing section IDs by adding exactly five strict sections and changing the root compatibility generation from 19 to 20.
- `DungeonRuntimeAggregateRootStore` already supports clone-on-write detached restore candidates by aggregate state type. The five V20 aggregates should use `GetOrCreateWritable` and `Replace`, so a late restore failure remains rollback-free and does not mutate the live root.
- The root catalog currently contains exactly nine legacy `CharacterTraitSO` assets under `Character/Traits`; V20 should preserve their GUIDs and add 47 new assets for a total of 56. The extended trait contract now rejects stat-only definitions unless they also author a behavior preference, mood reaction, or event weight.
- No authored `HeritableTraitDefinitionSO` assets were found in the current resource tree. The planned 24 hereditary traits can therefore establish the sole asset authority without migrating a competing legacy asset set.
- Existing general trait numeric IDs are 101-109. V20 additions can use a non-overlapping 200-246 range while preserving the nine legacy GUIDs and identifiers.
- The trait builder enforces the exact hereditary category split (Anatomy 6, Metabolism 6, Arcane 4, Reproduction 4, Immunity/Longevity 4), validates all consequence records, and replaces only V20-owned ID ranges/types in the root catalog.
- The V19 festival assets do not satisfy the extended V20 physical-input/outcome contract until upgraded. The V20 world builder therefore owns and rewrites all 16 festival definitions while preserving the four established stable IDs.
- Seasonal event definitions already enforce at least two affected domains and a real mechanical effect. The authored batch fixes seven events per `Spring`, `Summer`, `Autumn`, and `Winter` for 28 total.
- The completed first authored block is exactly 203 net-new V20 definitions. The festival catalog contains 16 total because the 12 new festivals replace/extend the four preserved V19 stable IDs rather than counting the originals as new content.
- The six canonical factions for V20 long arcs are the existing dungeon factions: `faction:dungeon:beastkin`, `demon`, `golem`, `harpy`, `kobold`, and `myconid`. The parallel `Factions/Dungeons` assets share those IDs and must not be treated as additional factions.
- Faction arc/chapter/contract, guest request, and service incident contracts are already typed and require mechanical outcomes; chapters and incidents reject single-outcome narrative text.
- Non-craftable faction relics should be authored as `GenericItemDefinitionSO` assets in a dedicated V20 item subtree, registered in `ItemDefinitionCatalogSO`, and additionally referenced by each faction arc. They do not need a production feature or recipe; max stack one makes their physical identity explicit.
- The item catalog exposes an editor-only `SetDefinitions` API, so the faction builder can replace only the eighteen `relic:faction:*` IDs without invoking the legacy all-content rebuild.
- The faction/service batch contributes 100 net-new definitions to the 450 manifest: 82 domain definitions plus 18 physical relic items. Each arc owns exactly six chapters, three contracts, and three relic IDs, and chapter three names a real cross-faction dependency.
- The six existing encounter stable IDs are `encounter:01` through `encounter:06`. The V20 combat builder should preserve and rewrite those assets, then add 30 new encounter assets, while enemy archetypes/abilities/modifiers use dedicated new SO authority.
- `OffenseEncounterCatalog` still contains the forbidden code-built enemy templates and ability factories. Ally projection is independent and can remain; `CreateEnemies`, `GetEnemySummary`, the template class, and hardcoded enemy ability helpers must be replaced by injected SO catalogs.
- The combat assets now pass cross-reference validation from every enemy to its 1-3 abilities and from every encounter to registered enemy IDs. Each role also has a nonempty formation and at least one counter tag, preventing stat-only archetype duplication.
- `WildlifeSpeciesSO` has useful diet/habitat/husbandry data but lacks explicit predator/prey links, nest behavior, breeding season, migration, disease-vector, and seasonal-activity metadata. V20 needs to extend this existing authority rather than introduce a second wildlife definition type.
- Crop cultivars are already represented by `CropGenomeDefinitionSO` over six diploid loci. The 12 V20 cultivars can be additional genome assets referencing the eight canonical crop IDs, with tradeoffs expressed directly in allele values.
- Recoverable inspection errors: one guessed Population/Species asset directory did not exist and a PowerShell-incompatible wildcard was passed to `rg`. Direct `Get-ChildItem` plus YAML field matching resolved the exact species tags; neither failure changed files.
- User clarification locks the human-enemy model: the 25 human entries are offense/defense tactical archetypes, not 25 fixed recruit templates. Every spawned enemy must be a normal persistent character instance with deterministic variation in age, background, culture, general/hereditary traits, skills, ambition, injuries, and loyalty. Capture and recruitment preserve that state and CharacterId; the former military archetype becomes origin/training history only.
- Combat readability remains authored at the archetype layer (equipment family, core abilities, formation, target priorities, counter tags). Individual variation may change proficiency and personality but must not erase the role's intended counters.
- After adding the Wildlife -> CoreSession dependency, the live project-scoped Unity Console reports zero errors and zero warnings. The ecology contracts now compile and the asset builder can be executed.
- `V20EcologyContentAssetBuilder.Build()` completed inside Unity with both compilation and execution success. The 33-definition ecology contribution is now authored and root-registered, bringing the net-new V20 manifest to 432; only 9 milestones and 9 physical landmarks remain for the exact 450.
- The current offense path proves the user's concern: `OffenseEncounterCatalog.CreateEnemies` still constructs transient combatants from hard-coded `EnemyTemplate` values and generated string IDs, so no normal CharacterRuntimeProfile exists to preserve when captured.
- The character profile factory already provides the correct authored/value-only boundary, but `CharacterSpawnRequest` currently carries only archetype/species/visual/reproductive role/traits/aptitudes. V20 enemy individuality therefore needs a separate persisted origin/narrative record rather than polluting the immutable combat archetype or recreating state at recruitment.
- Captivity commands already operate on the real `CharacterActor` and `captiveId`. The safest cut is to ensure invasion/offense enemy publication creates a real persistent actor first, then let `TryRecruit` remove only captive control state while leaving identity/profile/body/narrative state untouched.
- `CaptivityRuntime.TryRecruit` already preserves the actor object and identity: it changes captive status, character type, AI pause, lifecycle, and door-access registration only. It does not replace profile, stats, injuries, or ID. The missing guarantee is entirely upstream: abstract offense combatants have no corresponding persistent character/profile before capture.
- Offense battle save currently persists combat numbers/statuses but omits display/species/archetype/origin/narrative identity. A V20 enemy-instance record must be authoritative outside the transient battle projection, while the battle section references its CharacterId.
- Offense prisoner rewards currently queue only an integer amount. On return, `TrySpawnPrisoner` deterministically creates a generic intruder from `IInvasionIntruderDataProvider`, initializes it only at materialization time, and assigns a new ID derived from arrival sequence. That path destroys encounter-specific identity by design and must carry saved enemy individual payloads/IDs instead.
- `CharacterActor.Initialize(CharacterSO, CharacterSpawnRequest)` already accepts a fully chosen profile while retaining the authored prefab/archetype asset. This enables enemy generation without runtime SO synthesis: choose IDs/aptitudes deterministically, call the normal profile factory through actor initialization, then persist origin/narrative state by CharacterId.
- Fresh life registration currently filters to owner or NPC workers. Captured intruders therefore need explicit life/narrative registration at enemy publication (not delayed until recruitment), otherwise their age and narrative could be regenerated or missing after recruitment.
- The current narrative Aggregate already supports deterministic background/default culture selection and 4/2 hereditary traits; extending its record with immutable enemy-origin fields and a bounded loyalty value avoids a parallel prisoner-personality store.
- The combat builder authors 36 roles over Human, Beastkin, Demon, Golem, Harpy, Myconid, Construct, and Truth tags. Enemy individual generation must map non-life tags such as `Construct`/`Truth` to an authored phenotype (Golem/Adventurer) while preserving the displayed tactical species tag separately.
- The narrative runtime/catalog code exists but `DungeonCharacterRegistration` does not register it yet. This explains why authored narrative content is not part of live character creation despite compiling; registration must be completed before enemy publication depends on it.
- The actual offense asmdef is `Assets/Scripts/Models/Offense/Core/DungeonStory.Offense.asmdef`; the prior miss was only a wrong directory level.
- The return-arrival Aggregate is the correct persistence owner for not-yet-materialized prisoners. Its state already survives expedition return barriers and contains deterministic arrival IDs; adding validated per-prisoner blueprints there closes retry/save identity loss without adding a sixth V20 section.
- Existing V18 return-arrival ID validation hardcodes `return:{n}:prisoner:{n}` suffixes. V20 may retain those CharacterIds while attaching individual blueprints, preserving stable cross-section references and minimizing migration surface within the new-game-only generation.
- The structural integration is in the intended location: `PopulatePrisonerIndividuals` runs immediately before the arrival enters aggregate state; restore validation runs before candidate creation; materialization reads the blueprint by the next unmaterialized index and registers life/narrative before publishing the downed actor.
- Enemy individual generation is deterministic from CharacterId + context + archetype ID, so save/load and retry do not consume mutable global RNG or generate a different recruit. The blueprint stores all chosen IDs/values; restoration validates authored references before actor creation.
- The canonical intruder CharacterSO stable ID is `character-archetype:2001`. Enemy profiles can use that visual/prefab archetype with a separately selected authored phenotype species; no runtime archetype SO is needed.
- The live content catalog is now safe to construct `EnemyCombatContentCatalog`: all 36 enemy assets have nonempty `training:*` IDs and valid generation bounds after the rebuild.
- VContainer registrations confirmed the narrative runtime was genuinely absent, not hidden in another module. It is now registered once with query/command/persistence facets, and the shared enemy/encounter catalog plus individual factory are registered in the existing offense composition boundary.
## Phase 124 enemy individuality findings

- The defense invasion path still initializes every intruder from one `CharacterSO` and assigns its persistent ID only inside `InvasionIntruderRuntime.PrepareBegin`; it does not currently use the new enemy individual blueprint.
- Captivity recruitment itself preserves an existing actor and CharacterId. The remaining defense defect is therefore generation and invasion persistence, not recruitment mutation.
- `CharacterLifePublicationService` samples initial age and birthday from a mutable shared random stream. Enemy blueprints must persist explicit chronological age, biological age, and birthday, then register through `ICharacterLifeCommand`, so queued/restored enemies do not change with spawn order.
- `DungeonStory.Invasion` already has a one-way reference to `DungeonStory.Characters`, so the shared serializable enemy blueprint belongs in the Characters model assembly. This lets invasion DTOs persist identity without making model code reference the default runtime assembly.
- The combat catalog contains 27 archetypes with `speciesTag=Human`, but exactly 25 belong to the five human enemy factions; the other two are neutral/shared human templates. Tests must classify by faction ID rather than treating species tag as faction membership.
- Stable FNV-style hash low bits were not sufficiently distributed for direct modulo selection over power-of-two content pools. A deterministic avalanche step is required before range selection; after it, the 100-instance probe exercises all twelve background definitions while preserving exact repeatability.
- The original V20 encounter assets authored objectives and battlefield modifiers, but the old battle session still treated every encounter as enemy extermination and ignored modifier values. Objective state now belongs to `OffenseBattleEncounterRules`, is persisted via reconstructible encounter content, and is evaluated at every battle boundary.
- Registering every transient enemy in Character Life/Narrative at spawn creates dangling cross-aggregate records after death or retreat. Keep the enemy blueprint in its owning combat/invasion aggregate and publish character domains only when a physical actor survives as a capture candidate.
- Faction effects need either one of the six canonical campaign faction IDs or an explicit contextual token. Silent semantic strings such as `faction:merchant-league` cannot be applied to the six-faction Aggregate and must fail catalog construction rather than fail years into a run.
- Society event caps need distinct ordinary and emergency counters. Counting total active events as ordinary capacity lets candidate ordering admit multiple emergencies and makes saved states impossible to validate consistently.
- The current V20 source layout is not excessively fragmented. The eleven sub-80-line files are Unity SO contracts or compact DTOs with independent asset/serialization identities; merging them would couple unrelated asset types. `V20CampaignRuntime.cs` is approximately 1,300 lines, but the size is a review signal rather than a failure and its catalog/contracts/aggregate rules remain one highly coupled feature boundary for now.

## Phase 125 design-document findings

- The previous 1,970-line overview was rich in subsystem detail but its top-level authority was still V17: it described 168 research nodes, a V17 save boundary, `truth_core` as the final run-ending victory, and pre-V19/V20 content totals. Adding isolated update notes would have left contradictory player expectations in one file.
- The consolidated document now treats the intended player experience as the organizing authority: physical place and logistics, memorable persistent people, progress with maintenance costs, multiple valid preparation paths, and history that survives across generations.
- Exact contracts and elastic catalog sizes are deliberately separated. Research 216, V20 net-new definitions 450, milestones/landmarks 9/9, and V20 save sections 68 are fixed; facilities and general items are described by approximate design scale plus catalog-authority language because those collections continue to grow.
- The 450-definition table contains 24 net-new category rows and sums exactly to 450. Preserved-and-rewritten definitions such as the original six encounters and four festivals are described in final totals but are not double-counted as net-new.
- The human-enemy explanation now distinguishes 25 faction combat archetypes from persistent individual characters. It explicitly preserves CharacterId, age, narrative, injury, loyalty, captivity, and recruitment continuity across offense and defense.
- V20 save architecture is documented as implemented while the unrun full 68/68/68 world round trip remains visibly deferred. Focused content, campaign, enemy-individual, encounter-objective, and modifier evidence is listed separately from final integration evidence.
- The rewritten Markdown is 1,009 lines and 55,786 bytes with 21 H2 sections and 66 H3 sections. It has zero stale V17/168/141/192 authority matches, zero Unicode replacement characters, twelve balanced code fences, and zero trailing-whitespace findings.
- The repository `.gitignore` ignores `docs/`, so the updated design document exists in the workspace but does not appear in `git diff` or `git status`. This is repository policy rather than a failed write; the file was read back and verified directly.

## Phase 126 exhaustive content-intent findings

- The facility asset subtree currently contains 349 `.asset` files, while the full V20 subtree contains 454. Raw file counts are not canonical content counts because buildings include structural pieces, duplicated/legacy paths, builder-owned partials, and potentially fixtures; canonical documentation must deduplicate by stable content identity and inspect ability modules.
- `BuildingSO` exposes presentation, authored `contentDefinitionId`, revision/source note, placement category/archetype, build conditions, unlock state, and polymorphic ability modules. An accurate facility intent entry must therefore use the stable ID plus actual ability/BOM/research data, not infer intent from filename alone.
- General traits directly author three player-facing consequence channels: behavior utility deltas, mood reactions with duration, and event-category weights. Every trait is validated to contain at least one of those consequences.
- Heritable traits directly author category, incompatibility, aptitude, species compatibility, and typed consequences for aptitude, environment, disease resistance, fertility, aging, anatomy capacity, or mana affinity. Combined hereditary modification is capped to ±25%.
- Life events, faction chapters, and service incidents validate two-to-four genuinely separate choices; automatic life events require effects. Seasonal events require at least two affected domains and a real mechanical effect. These typed fields are the authority for individual intent documentation.
- The first parallel discovery batch aborted because one optional `rg` pattern returned exit code 1 inside `Promise.all`. No files changed. Subsequent discovery uses sequential calls with optional-no-match exit normalization.
- Exactly 400 `BuildingSO` assets exist. Forty-two live under `Assets/Resources/SO/Buildings/RuntimeArchetypes` and are internal runtime archetype assets, not separately placeable design content. The exhaustive facility catalog scope is therefore 358: 349 placeable assets under `SO/Building` plus nine V20 milestone landmarks.
- The 358 facilities have unique numeric IDs and nonempty display names. Only the nine V20 landmarks currently carry the newer `contentDefinitionId`; older facilities remain canonically keyed by their stable numeric `DataScriptableObject.id`.
- Facility distribution is: root 9, captivity 10, combat 3, industrial 36, medical 13, modular 104, P1 34, production support 28, research-overhaul 96, service rooms 16, and landmarks 9. This is the documentation grouping, while individual entries remain mandatory.
- The V20 root contains the expected event-like concrete assets: 32 life events, 16 festivals, 28 seasonal world events, 36 faction chapters, 18 contracts, 14 guest requests, eight service incidents, 30 net-new encounter assets plus six preserved encounter assets elsewhere, and nine milestones/endings.
- The trait authority contains exactly 56 general trait assets (nine preserved plus 47 V20) and 24 hereditary trait assets.
- One attempted facility-list helper embedded PowerShell backtick-tab syntax inside a JavaScript template literal and failed at JavaScript parsing before any tool call or mutation. It was replaced with `[string]::Join([char]9, ...)`.
- Festival definitions are individually actionable rather than calendar flavor: each requires a concrete facility, physical item amounts, minimum participants, and separate success/partial/failure outcomes; some explicitly convert active grief.
- Encounter assets do not carry prose descriptions. Their individual intent must be documented from the authored objective, round/target conditions, battlefield modifiers, counter tags, reward items, and enemy compositions rather than invented narrative text.
- All 32 life-event names, descriptions, and authored choices were extracted from the generated SOs. Twenty are explicit two-way dilemmas and twelve are automatic history moments; the latter intentionally reward continuity without interrupting the player with a choice dialog.
- All 16 festivals carry distinct cultural or seasonal meanings. Their individual intent entries will connect the authored physical preparation to grief, mood, cohesion, or faction outcomes rather than merely list calendar dates.
- The guessed seasonal-event folder `V20/Society/SeasonalEvents` does not exist. The failed read was non-mutating; locate the class assets recursively instead of assuming the builder folder name.
- Located all 28 seasonal-event assets under `Assets/Resources/SO/V20/World/SeasonalEvents` and extracted their exact two-domain couplings. Each season contains seven authored pressures that connect farming, wildlife, health, logistics, expeditions, factions, guests, or facility operation.
- Extracted all 36 faction chapters. Their narrative conflicts are faction-specific, but every chapter currently reuses the same three mechanical stances (`support`, `bargain`, `refuse`) and the same rapport/obligation/grievance effect pattern. The design document must state this honestly rather than imply 36 mechanically unique choice sets.
- The generated faction-contract descriptions use generic templated copy and sometimes malformed Korean particles. Documentation should capture each contract's actual item, amount, deadline, and strategic purpose; the source copy remains a later content-polish defect.
- The exhaustive intent appendix now contains all 358 canonical player-facing facilities and all 32 life events as individual rows.
- The completed event intent catalog covers every authored player-facing event authority: 32 life events, 16 festivals, 28 seasonal events, 20 cultural practices, 36 faction chapters, 18 faction contracts, 14 guest requests, eight service incidents, 36 combat encounters, and nine milestones.
- The encounter builder is not fully bespoke authoring: it cycles six objectives and twelve battlefield modifiers over the ordered enemy array. Preserved encounter display names 03-06 do not match their current primary enemy faction/role. The document records the actual mechanics and flags these assets for rewrite instead of inventing nonexistent uniqueness.
- The completed trait intent catalog covers all 56 general traits and all 24 hereditary traits. The nine legacy general traits remain scalar-only and need behavior/mood/event channels for parity with V20 traits.
- Exact source-to-document ID comparison passes with zero missing and zero extra entries for 358 facilities and every event/trait category. The final Markdown has no replacement characters or trailing whitespace and retains balanced code fences.

## Phase 127 V21 research-expansion findings

- The user explicitly removed save-file migration from scope. V21 keeps only a clear V20-and-earlier incompatibility result; implementation must not add old-ID remapping, legacy DTO conversion, or partial restoration paths. Editor-time content rewrites used to author the V21 assets are not save migration.

- The live authored research catalog contains 216 project assets. The approved consolidation removes 36 stable IDs and keeps 180 survivor projects while preserving 138,824 total work.
- The existing reward index covers facilities, resource items, production recipes, combat equipment, and surgical procedures, but omits eight research-gated crops, twelve craft materials, and three environmental workwear definitions.
- Current project assets and research-gated definition assets are highly uneven: many projects expose one direct reward while fermentation, livestock cuisine, compost, vaccination, and other families expose large flat lists. V21 needs authored grouping plus a broader reverse index, not a second completion authority.
- Existing combat equipment authoring provides six starting definitions and research-gated weapon/armor/shield families. Sparse gates include ballistics, dark foundry, steel, tailoring, tanning, and powered armor; the V21 additions must fill distinct tactical roles rather than scalar duplicates.
- The repository working tree already contains user-owned planning-file changes and a deleted `COPILOT_HANDOFF.md`; Phase 127 must preserve that deletion and all unrelated content.
- The final authored V21 asset audit reports exactly 180 research projects, 180 unlock bundles, 61 combat equipment definitions, and 138,824 total research work. All 36 absorbed IDs have zero references in non-Editor runtime C#.
- `V21ResearchConsolidation` is compiled only under `UNITY_EDITOR` and documents itself as an asset-authoring map. Runtime compatibility exposes only the V21 equality check and the exact V20-or-earlier rejection reason, so it cannot become an implicit save migration table.
- The ten V21 ammunition IDs are all present as physical item definitions. `supply:defense-mixed-ammo-box` is intentionally a `FinishedGood`, preserving the requested ten ammunition kinds while still giving defense-supply research a physical consumable.
- A stale architecture ratchet still expected save root V20 and one research scenario still compared the V5 incompatibility copy. Both were updated to the shared V21 compatibility constant; no restoration behavior changed.
- After the final test-only edits Unity compiled all 2,172 evaluated items successfully. The project-scoped MCP relay subsequently timed out on dynamic validation responses despite a responsive Editor; no validation-failure marker was emitted, so a fresh live Console 0/0 capture remains deferred rather than claimed.
# 2026-08-08 Phase 128 actual-gameplay connection audit

- Registration and count validation are not sufficient evidence for V21. The accepted completion path is command/AI entry -> authoritative requirements -> physical reservation/durability -> domain effect -> atomic publish -> V21 restore.
- `V20CampaignRuntime` currently resolves society events and applies internal campaign effects before `V20CampaignApplicationAdapter` applies item/money effects. A later item failure therefore leaves the event resolved and campaign state mutated; this is the first blocking defect.
- `V20ContentEffectKind` declares mood, trauma, skill XP, health, relationship, work delay, disease exposure, and ambition progress effects that have no production executor. Requirement evaluation also substitutes total character/building counts for life-stage, trait, health, operational-state, and capability checks.
- V21 `GuestSupplies` attaches unrelated medical and operational goods to guest requests. These are fabricated sinks and must be removed before intended procedure/work consumers are counted.
- Loaded weapons retain only an ammunition count, so a selected physical ammunition ID is lost after reload/loading and special-ammunition behavior cannot be authoritative.
- Crop yield/seed-yield loci execute, but cold tolerance, heat tolerance, growth speed, and disease resistance do not participate in the authoritative crop calculations.
- The current worktree contains the broad user-owned V21 asset regeneration and prior phase changes. Phase 128 must use narrow patches and may not reset or normalize those changes.
- The aggregate-root store exposes detached staging only to the save registry. Ordinary gameplay commands mutate the live root directly, so content resolution needs its own prepared campaign candidate (or a carefully bounded public transaction) rather than abusing save restore.
- The item repository has stack reservations, but the current event adapter ignores them and merely recounts global stock before consuming stacks. Money also has no reservation token. On the single Unity main thread, a fully prepared batch can make commit operations non-failing, but the contract must reserve exact stack IDs and publish campaign/domain state last.
- Existing domain entry points can support typed effect handlers without a second state authority: actors expose mood and progression operations, `IGriefTraumaService` owns trauma/counseling, `ICharacterNarrativeCommand` owns ambition progress, and body/population-health commands own health/disease effects. The event result must carry participant IDs and contextual faction ID so those handlers do not infer targets from strings.
- Content resolution now prepares a detached campaign candidate, validates typed requirements and exact physical stack reservations, applies typed character/health/relationship/faction effects, commits non-failing item consumption, and publishes the campaign candidate last. A late material/effect failure no longer resolves the event or partially changes live Aggregate state.
- `GuestSupplies` was confirmed to be a fabricated-consumer table. Its builder now removes those links instead of creating them, and all 30 unrelated guest-request item requirements were removed while the 14 authored request requirements remain.
- Reproduction now has persisted planned/start transitions and a real command path. Cross-lineage and golem processes require the exact operational facility and physical inputs, validate a detached candidate, consume atomically, and publish last; Allowed policy evaluates proposals only every ten days.
- All five age treatments now enter through the existing surgery Aggregate. Their authored procedures require exact 8868-8872 facilities, clinician/patient work, physical materials, surgery environment, typed effects, and the existing surgery save section rather than direct age mutation.
- The 101 research-reward facilities no longer all advertise generic Research/Logistics roles. Builder profiles now assign administration, production, living, medical, industrial, rune-biomedical, greenhouse, or observation roles; age-treatment facilities expose the typed surgery capability.
- The V21 equipment assets existed outside the root catalog. Eighteen role-equipment definitions, five age procedures, and facilities 8897-8901 were added to the root catalog explicitly; the repeating-crossbow scenario's prior unknown-definition failure identified this real registration gap.
- Loaded ammunition now persists both ammunition definition ID and remaining count. A nonempty magazine accepts only the same ammunition type; changing type requires an empty magazine, and firing the last round clears the loaded type.
- Crop ecology previously consumed only Yield and SeedYield. `CropGenomePhenotype` now maps all six loci: cold/heat tolerance alter the live temperature band, growth speed alters Tick progress, disease resistance alters daily disease probability/progression, and yield/seed yield remain in harvest output. The phenotype is derived from the saved cultivar genome, so restore preserves every effect.
- Eleven authored choices/events used `WorkDelayDays`, but the atomic resolver explicitly rejected that effect. Society save V3 now owns scoped delay end-days; flood affects agriculture/haul work, road/whiteout affect expedition logistics, and unscoped service/life-event delays affect global work. `CharacterStatsProjectionService` consumes the persisted query, so the effect survives restore and changes completed work rather than only a debug snapshot.
- General-trait behavior preferences were validated but unread. Runtime profiles now compile them into capped Utility AI multipliers, while trait event weights and active-ambition related-event weights affect deterministic society-event and participant selection. Participant assignment is no longer biased toward the first stable CharacterId.
- Expressed hereditary definitions now have one runtime query authority backed by the narrative Aggregate. Slow aging modifies the daily biological-age increment, broad/toxin resistance modifies infection susceptibility, and success-rate/gestation/offspring stability modify conception and miscarriage calculations. Latent traits remain non-expressed and therefore do not contribute.
- Faction chapter `consume=true` requirements were previously checked but not converted into commit effects, so a chapter could advance without spending its promised items. `TryResolveChapter` now returns the exact consumed requirements, allowing `IContentResolutionService` to reserve and atomically consume them before publishing the staged chapter/faction state.
- The former 36-way faction duplication is removed in current assets and the rebuild path: 72 consumable support/bargain requirements, 72 operational facility requirements, 36 refusal pressures, six counterpart-faction mutations, and 36 unique mechanical choice signatures are present.
- Culture environment and etiquette fields were previously presentation-only free text. Ten typed room profiles now feed real facility scoring, while forbidden-food, etiquette, and inter-culture attitude data feed the society incident selector. The descriptive `environmentalPreferences` strings remain presentation text and no runtime rule parses them.
- Cultural-practice success was connected, but authored `neglectedEffects` had no command path and the saved participation record did not distinguish observance from neglect. The alert dispatcher now exposes a stable neglect action, applies only typed neglect effects, advances no assimilation, persists the outcome/cooldown, and round-trips it in `characters.narrative`.
- Inter-culture attitude weights previously influenced incident selection only. A newcomer now creates one bidirectional direct relationship memory against already initialized residents during its one-time background initialization, using each culture's independently authored attitude.
- `supply:greenhouse-nutrient` and `supply:inoculated-log` were still output-only goods after fake guest sinks were removed. The greenhouse and fungal shelf now own real `BuildingCropPlotAbility` cycles that request, haul, and consume those supplies together with the seed lot before growth starts; crop-plot persistence owns the resulting phase and genome state.
- The seven hereditary costs described in prose were absent from the consequence assets. They now use appended, serialization-safe consequence kinds and feed survival need growth, active-reproduction hunger, movement, mana-disease exposure, and the existing biological-aging projection.
- The nine legacy traits had placeholder `legacy-trait:*` behavior tags and no mood/event data. They now share the V20 three-part contract, and a typed reaction runtime translates concrete meal, research, invasion, festival, room-environment, and checkout-wait events into mood reactions instead of parsing display text.
- `tool:reinforced-restraint` and `tool:prisoner-work-kit` were still stackable output-only goods while captivity hardcoded and consumed `captivity:restraints`. They now have unique persistent item IDs and durability state. Captivity owns them only while actually equipped; otherwise the same physical instance remains in carry/world inventory. Other audited V21 output-only tools and records remain open and must not be counted as connected.
- Fertility treatment had no reproduction reference even though conception and miscarriage calculations were already centralized. It is now a saved optional process choice, paid at `TryStart`, and modifies those existing calculation inputs instead of creating a second fertility authority.
- The first fertility implementation exposed a request flag but the generated approval alert still had only one generic start action. This would have left treatment unreachable in normal play. Biological reproduction alerts now offer both paths; golem assembly continues to show only ordinary assembly.
- Latent hereditary traits were always exposed on the general snapshot while `medical:trait-analysis-kit` had no consumer. The narrative Aggregate now owns an analyzed flag and a separate visible-latent projection. Internal genetics still reads the latent authority, so discovery state cannot alter inheritance.
- Facility `8879` still carried the generic industrial-lab role after gaining a medical analysis command. Its authored profile and current asset now advertise Medical/Research and Treat/Research/Operate, allowing capability evaluation and the generated analysis alert to use the real facility role.
- The complete 8801-8901 facility set divides cleanly into 63 exact workstation-recipe executors and 38 typed command executors. The earlier no-recipe set shrank after correcting treated lumber to the 8816 workstation; zero facilities now rely on a generic role tag alone.
- Facility 8882 had a circular BOM: it produced the room-partition kit while also requiring that kit for its own construction. The kit belongs to 8883 family partitions and the authored BOM is corrected accordingly.
- Semantic tags were insufficient for the facility audit because unrelated buildings could satisfy them. Mentorship, pathogen diagnosis, weapon-pattern access, resonance tuning, secure trade, remote defense, crop/husbandry support, flow metering, and expedition planning now query exact typed facility commands.

## 2026-08-08 Phase 128 final integration findings

- The initial isolated full-world PlayMode gate found a real scene-composition defect before save capture: moving the shared world-map source file had left the authored scene GUID attached to `OffenseWorldMapPanel.cs`, so Unity could no longer materialize `OffenseWorldMapRuntime`. The runtime and panel now live in filename-matching source files, and the original GUID remains with `OffenseWorldMapRuntime.cs`.
- Enemy background faction reactions previously stopped at narrative metadata. Canonical faction-ID mapping now modifies persisted enemy loyalty, and captivity derives compliance and escape risk from that same narrative state. The focused enemy continuity scenario ratchets the deserter/legion mapping.
- Landmark visibility and placement previously trusted ordinary research/unlock state. Both construction UI and placement validation now query the milestone authority; all nine landmarks are locked before their matching milestone and become constructible after completion.
- The isolated five-route functional alert PlayMode facade passed reproduction, festival scheduling/resolution, funeral, counseling, and age-treatment dispatch with five persisted/dismissed actions.
- The focused V21 vertical gate passes after the scene-GUID repair, including 68-section atomic staging scenarios, 10,000 general-trait selections, 10,000 hereditary combinations, 2,000-by-three-generation narrative compression, all six crop loci, ecology/disease vectors, combat, campaign, faction authority, and all 101 research facilities.
- The current-code full-world gate now passes `68/68/68`: all registered sections were captured, restored, and recaptured; the canonical baseline matched; the live baseline was restored; and the integrated Console result was Error 0 / Warning 0.
- The first successful capture exposed that bulk `StockCategory.General` spawning selected a max-stack-one workwear definition by lexical ID and created 40 unauthoritative item instances. Bulk stock spawning now selects stackable definitions only; unique equipment remains owned by the equipment runtime.
- Active invasion enemies intentionally live in the invasion section rather than the resident character-world section. Cross-aggregate preflight now indexes their canonical CharacterIds so their saved life and combat-loadout state can reference the same persistent individual without spawning a duplicate resident.
- VContainer 1.19's circular-dependency scan revisited the same shared registration DAG from every root and made the production scope appear hung. The package is now embedded in the project with a memoized, cycle-safe traversal; a clean isolated run resolved the embedded package path and passed the full-world gate.
# Phase 129 research-node catalog findings

- The design authority already summarizes the 180-node/138,824-work V21 research contract, pacing, major branches, and unlock principles, but it does not yet contain an exhaustive node-by-node dictionary comparable to the facility, event, and trait appendices.
- The new catalog must follow current authored research assets and the reverse reward authority; presentation-only unlock bundles must not be mistaken for a second gameplay lock authority.
- `ResearchProjectAssetBuilder.CreateSpecs()` is the canonical authored source for stable ID, numeric ID, Korean name, description, field, work, and direct prerequisites. Rebuilt `ResearchProjectSO` assets persist the resolved references and causal prerequisite links.
- The builder captures merged unlocks, appends production/service/overhaul unlocks, rewrites absorbed research requirements, and then builds presentation bundles. Therefore documentation needs a reverse-indexed reward pass in addition to the project spec list.
- Current generated authority is exactly 180 `ResearchProjectSO` assets and 180 matching unlock-bundle assets.
- Content-owned reward declarations currently appear on 275 resource items, 265 production recipes, 47 surgical procedures, 12 craft materials, eight crops, four environmental workwear definitions, and 55 combat equipment definitions (28 weapons, 19 armors, eight shields). Generic item mirrors are not a separate research reward kind and must be deduplicated against their equipment/resource authority.
- Common YAML keys are stable enough for a read-only extractor: `projectId`, `displayName`, `description`, `field`, `requiredWork`, `prerequisiteId`, the polymorphic `BlueprintBuildingUnlock`/`BlueprintRecipeUnlock`, and content-specific identity plus `requiredResearchId`.
- The comprehensive document currently contains no stable `research:*` IDs, confirming the exhaustive node appendix is wholly missing rather than partially duplicated.
- Canonical `BuildingSO` reward names serialize as `objectName` and numeric `id`; production recipes use `recipeId`/`displayName`. This is sufficient to resolve project-owned building and recipe unlock entries without relying on filenames.
- The reproducible extractor passes the authority baseline: 180 projects, 138,824 total work, zero duplicate IDs, zero unresolved direct prerequisites, zero rewardless projects, and 919 deduplicated direct reward entries.
- Field distribution is 9/8/13/12/4/11/10/6/6/7/8/7/10/6/27/31/5 across the 17 `ResearchField` values; the largest appendices will be industry/automation (31) and surgery/transplant (27).
- The Markdown formatter now emits readable prerequisite names together with exact stable IDs and keeps recipes/results as distinct rewards even when their display names match.
- A review of the initial four fields caught blank duplicate rewards from unrecognized mirror assets and raw labels for project-owned recipes. These are extractor-only defects, not content defects; recognized `ResearchRewardCatalog` families and a global recipe identity map are now enforced.
- Removing unrecognized mirrors reduces the truthful reverse reward set from the prototype's 919 entries to 899 while retaining zero rewardless projects. Eleven rows in the first four field tables require regeneration.
- Three raw recipe IDs are authored `FacilitySynthesisRecipeSO` upgrades rather than production recipes: 잠금진열장 개조, 전투깃발 제작, 의식초점석 조율. They remain direct project unlocks and now receive their authored display names through the global recipe map.
- Fields 0-8 now render 79 unique node rows with nine field headings. The reviewed section contains no blank reward labels, raw `recipe_*` labels, or leaked PowerShell interpolation syntax.
- Fields 9-13 add 38 nodes for husbandry, metallurgy, textiles, cuisine, and pharmacology. The running document total is 117 nodes across 14 fields with zero formatter artifacts.
- All 17 fields are now present. Exact block comparison against regenerated Markdown passes, with 180 distinct stable IDs, 180 distinct numeric IDs, work sum 138,824, zero missing/extra nodes, zero malformed six-column rows, and zero formatter artifacts.
- The design document remains ignored by the repository-wide `docs/` rule, while the new verifier lives in the existing `Tools` tree. A small `.cmd` entry point handles Windows PowerShell 5's UTF-8-without-BOM and execution-policy behavior before invoking the source-derived `.ps1` implementation.
- Final validation passes after the navigation/tooling updates: 180 rows, 180 unique stable IDs, 180 unique numeric IDs, 138,824 documented work, 17 field headings, exact regenerated table match, zero malformed rows, zero formatter artifacts, zero replacement characters, balanced code fences, and zero trailing whitespace.

# Phase 130 V22 apparel/textile implementation findings

- The approved scope is a new runtime vertical, not a documentation-only expansion. Completion requires authored content, executable work orders, physical inventory effects, aggregate persistence, functional UI, and focused/full validation.
- Existing environmental workwear owns a species-oriented equipped map and loose-item fallback; V22 must move mutable slot authority to `CharacterApparelAggregate` and leave that runtime as a compatibility adapter.
- Existing physical stack signatures preserve exact state payloads, so V22 fiber components must expose a canonical signature containing only material item, four-tier quality, and three-band condition. Production day, exact quality, pathogen detail, and lot identity cannot participate.
- Existing item reservations have no time-bounded lease. Apparel workflows therefore need a scoped lease layer with invalidation and retry semantics rather than changing unrelated reservation behavior implicitly.
- The existing crop genome already has the required six loci and its positive bounds match V22's +16% growth/+10% yield normalization. Fiber quality should consume those loci through the approved penalties rather than adding a seventh locus.
- Facility IDs 9301-9314 and the six referenced research IDs are available. The reverse reward count must rise from 101 to 115 facilities without adding research nodes or work.
- Current content exposes exactly four environmental workwear identities, which will be reused as four entries of the 56-definition apparel catalog instead of duplicated.
- The V22 authored slice now resolves to 56 apparel definitions, 12 material definitions (10 woven plus leather/rune-leather), 4 crops, 12 six-locus genomes, 3 husbandry fiber outputs, 14 facilities, and 89 recipes. The focused Unity gate passes all counts and the 81-point yield/growth tradeoff grid.
- An invalid apparel lease originally retried only the same saved stack IDs, which could permanently strand a craft or medium repair after fire, contamination, compaction, or quantity loss. Revalidation now preserves valid delivered stacks first, but after invalidation performs a bounded policy-aware rebuild for substitutable inputs without ever replacing a persistent target garment with a different item.
- Focused asset-count checks were insufficient to catch zero-valued inherited `DataScriptableObject.id` fields. The production container's exact-type compatibility index exposed the collision; V22 focused validation now explicitly checks positive unique numeric IDs for apparel and textile materials.
- Expanding authored crops/genomes did not automatically update `CropEcologyRuntime` or `TryClaimInitialSeedGrant`; both retained exact V20 count assertions. The V22 authority is now consistently 12 crops, 32 genomes, and 12 base seed lots across assets, construction, and bootstrap.
- MaxStack-one does not imply combat equipment. Apparel is a persistent unique physical stack whose item-instance ID and `ItemInstanceComponentIds.Apparel` state live inline on the stack, while combat equipment and modules continue to require their dedicated authoritative unique-item registry entry.
- Existing cloth and dreamweave physical items had stack caps below the V22 fabric contract. The V22 builder now preserves their features and identity while normalizing every woven material to MaxStack 100; raw fiber and yarn remain MaxStack 200.
- Short wardrobe alteration previously overwrote the authored cut-opening mask, making “close/reopen” semantically incorrect. Short operations now mutate only `closedOpenings`; full tailoring remains the only path that changes size or cuts a new opening.
- The open Unity production report still carries 24 pre-existing V21 fake-consumer failures outside the V22 apparel slice. V22’s own focused gate is PASS, so the old failures must not be reported as V22 regressions or silently described as globally clean.
# V23 implementation findings - 2026-08-08

- The current worktree already contains broad V21/V22 user changes and generated assets; V23 changes must be additive and must not clean or reset unrelated files.
- `WorkOrderSaveData` currently persists one `reservedWorkerPersistentId`; there is no reusable rule-based worker policy or contribution ledger.
- Work eligibility already passes through `IWorkPolicyRegistry` and typed `IWorkStatPolicy` implementations, providing the correct integration boundary for V23 eligibility without bypassing safety/career rules.
- Existing V22 apparel runtime still uses `TextileQualityTier`, minimum material quality filters, quality-bearing stack codecs, and quality projection; these are direct grade-free migration targets.
- Production bills already support repeated/target-stock execution, but do not own craftsmanship-target pipelines or common worker-selection policies.
- V22 textile quality is embedded in the shared apparel definition file (`TextileQualityTier`, quality projection, provenance, and instance state), the apparel work-order runtime, item codecs, availability index, and crop/certified-seed paths; removal must be coordinated rather than deleting one enum in isolation.
- Relevant definition paths differ from their logical namespaces: buildings live under `Services/Buildings/SO`, production recipes under `Models/Economy/Content`, and combat save/runtime contracts under `Models/Combat/Core` plus `Services/Combat`.
- Building construction work is owned by `BuildingWorkAbility.constructionWorkRequired`; construction material requirements are exposed through the building ability accessors rather than fields directly on `BuildingSO`.
- Combat equipment craft orders already capture required work and material ID but lack worker policy, contributor ledger, fixed quality roll, and quality-target repetition state.
- Production recipe definitions expose explicit process kind, direct/preparation/finishing work, inputs, and outputs; they are suitable inputs to one central V23 calculator while preserving authored overrides.
- `WorkOrderRuntime.ApplyWork` is the atomic construction contribution boundary: it currently accepts any caller, writes a transient reserved worker ID, and deletes the order immediately before `ConstructionSite.CompleteConstruction`; V23 eligibility/contribution/quality completion must be inserted before that publication sequence.
- Work-order capture deliberately clears the reserved worker and restores in-progress orders as ready. This matches the V23 rule that leases are not saved, while policy, contribution, and fixed roll must be durable.
- `BuildingSO.Abilities` exposes the authored ability count, so the construction capability factor can be computed without reflection or string-tag guessing.
- VContainer registration for work orders and apparel is centralized in `DungeonWorldSimulationRegistration`; combat is registered separately in `DungeonCombatRegistration`.
- `CombatEquipmentDefinitionSO` provides kind, weight, primary material amount, components, era, and tier, enough to derive non-placeholder equipment work without changing the 61 definitions' public identity.
- `BuildingWorkAmountAbility` is the authoritative authored BOM/work component and already rejects empty, abstract, duplicate, or missing construction material definitions.
- Production input/output primitives expose canonical item IDs, integer amounts, and output probabilities; V23 recipe work can account for expected output quantity without reaching into serialized fields.
- Combat equipment form classification can use concrete weapon/armor/shield types, weapon gunpowder/range data, armor layer, occupied hands, tier, and era; it does not need display-name inference.
- Every `BuildableObject.Initialization` rebuilds its state-module set, so a craftsmanship module registered there will participate automatically in the existing modular-facility save section without adding a 69th section.
- `WorkOrderRuntime` already receives `IObjectResolver`; optional V23 services can be resolved through its `TryResolve` boundary without breaking the many direct editor-test constructors.
- The authoritative project currently contains 368 player-building assets, not the approximate 369 in the proposal. All 368 serialize a construction BOM; runtime representation archetypes remain excluded.
- The 24 production items previously labelled as fake-consumer failures already have real typed command consumers. The production validator omitted those domains, so V23 adds an explicit item-to-command-owner catalog instead of inventing recipes or sinks.
- Automatic rejected-output dismantling must consume the rejected unique item before publishing recovery. Persisting a `rejectedOutputConsumed` recovery obligation closes the output-full/save-restore duplication window for both apparel and combat equipment.
- Character stats are authored on a 0..10 scale while craftsmanship skill is 0..100. Equipment quality therefore projects the mean Dexterity/Research scale by multiplying their sum by five; passing the raw values made almost every result poor.
- Facility quality now affects completed-facility work throughput through the shared 0.70..1.40 projection multiplier, so construction craftsmanship is mechanically observable rather than display-only.
- Apparel and equipment now mirror construction's pre-consumption quality reachability rule. No eligible worker releases reservations into `WaitingForEligibleWorker`; an impossible target releases them into `TargetCurrentlyUnreachable` and reuses the saved attempt roll after conditions improve.
- Construction completion delegates return only success, not the created building. The completed facility can instead be resolved from the authoritative grid at the saved order position after publication.
## 2026-08-08 - Phase 132 gameplay UI/debug separation discovery

- User settings already persist `DungeonUserSettingsData.developerMode`; it defaults to `false`, is cloned with the rest of the settings object, and does not consume a world-save section.
- `DungeonUserSettingsService` already exposes `Changed` and applies presentation preferences, so Phase 132 should reuse this authority and present it to players as `Debug Mode` instead of adding a second setting.
- The settings runtime UI already has a fourth development page and a `developerModeToggle`, but its Korean literals are visibly mojibake in source/output and need player-facing copy cleanup.
- `GameplayScene.unity` has an active top-level `__Debug` root with two children. It must be classified before gating; an unrelated empty child named `Debug` also exists under another transform and should not be hidden by name alone.
- Debug-mode changes must remain presentation-only: overlays, raw identifiers, diagnostic counters, validation launchers, and direct debug controls may be hidden/revealed, while actionable failure reasons stay visible.

## 2026-08-08 - Phase 132 implementation and verification findings

- Runtime surfaces were classified as player-facing (construction, production, equipment, apparel, character, medical, research, faction, expedition, event/notice), advanced player policy (worker/material/quality/repeat settings), or debug-only (palette, raw IDs/enums, AI utility diagnostics, overlays and mutation commands).
- `DungeonUserSettingsData.developerMode` remains the single persisted authority. No world-save section or duplicate flag was added.
- `DungeonDebugSceneVisibilityController` only gates the active scene's top-level `__Debug` root and `__Runtime/Debug`; unrelated transforms named `Debug` are not affected.
- The character AI tab contains BT branches, utility candidates, timing, path budgets and raw memory diagnostics, so the tab is now hidden unless Debug Mode is enabled. Disabling Debug Mode while it is open returns the panel to the normal status tab.
- Player surfaces no longer expose the surgery order ID, doctor persistent ID, combat equipment definition ID, husbandry status enum, husbandry failure enum or V23 craftsmanship enum in the audited paths.
- Construction, production and apparel use player-facing stage/failure copy; worker/material/quality/repeat choices are placed behind progressive disclosure. Technical order/definition/state text is appended only while Debug Mode is enabled.
- Settings and building detail panels use short unscaled-time fade/scale choreography and respect Reduced Motion; gameplay authority and command timing are unchanged.
- First fresh PlayMode attempt revealed that the debug verifier called the full-world save without a prepared owner. The verifier now validates its owned `DungeonDebugRunSaveData` directly; the separate 68-section integration gate remains authoritative for full saves.
- Final MCP verification passed every pointer, visibility, command, targeting, overlay, metadata and reset assertion with Console Error 0 / Warning 0.
- Visual review: the 1600×900 palette is centered and legible; the 900×1600 sheet remains fully on-screen with scrollable command rows and reachable close/action controls.
## 2026-08-08 - V24 static structured narrative kickoff

- Current `LocalLlmRequestQueue` uses Ollama's OpenAI-compatible `/v1/chat/completions` endpoint with prompt-only JSON instructions and `response_format:{type:"json_object"}`; it has no per-profile schema authority.
- Local Ollama reports version 0.32.5 with `llama3.1:latest`; a read-only `/api/chat` probe accepted a JSON Schema in `format` and returned schema-shaped content.
- The repository already exposes nine request profiles and domain DTO validators. Character narrative state includes background, culture, ambition, expressed traits, revealed latent traits, recent events, origin faction/archetype, skills, age/life-stage records, and career/progression data.
- The worktree contains extensive user-owned V21-V23/UI changes. V24 edits must remain scoped to LLM/narrative files and preserve those changes.
- All nine profiles already route through one queue, but prompt ownership is distributed across character skills, AI director goals/impulses, persona, social rumor, character log, dialogue, facility evolution, and equipment/facility evolution history. The implementation needs a queue-level static schema authority plus shared context/quality utilities that prompt builders can adopt incrementally.
- Existing DTOs are split across `LlmJsonResponseParser`, `CharacterRecordJsonDto`, `CharacterSkillGenerationService`, `FacilityEvolutionLlmProposalProvider`, and `EvolutionHistoryNarrativeRuntime`; schema definitions must match these concrete wire shapes rather than introducing an unrelated envelope.
- The shared queue can switch transport without changing `ILocalLlmRuntime` or the many fake runtimes. It currently owns request construction and HTTP response extraction, making it the narrow integration point for static schema selection, native Ollama request/response wire shapes, capability status, schema diagnostics, and pre-callback reference validation.
- Existing request-bound identifiers (skill combinations, facility proposals, evolution evidence) are already validated in their domain handlers. V24 schemas should keep those fields as ordinary strings/arrays and leave request-local membership checks in C#, consistent with the static-schema rule.
# 2026-08-08 — V24 컨텍스트 연결 점검

- `NarrativeRequestContext.cs`의 최초 작성본은 터미널/패치 경로에서 한국어가 mojibake로 변형되어 따옴표까지 손상됐다. 프롬프트용 한국어는 Unicode escape로, 코드 식별용 문구는 ASCII로 유지해야 한다.
- 현재 중앙 큐의 기본 컨텍스트만으로는 `Persona`, `CharacterSkill`, `CharacterRecord`가 실제 인물 사실을 의무적으로 참조하지 않는다. 각 프롬프트 생성 지점에서 actor/progression 기반 컨텍스트를 명시적으로 붙여야 한다.
- 나이·배경·문화·야망은 `ICharacterLifeQuery` 및 `ICharacterNarrativeQuery`에 이미 권위가 있으므로, 공개 사실 투영기를 통해 LLM 컨텍스트에 넣는 것이 기존 Aggregate 권위를 보존하는 경로다.
- 9개 프로필은 요청 데이터와 무관한 고정 JSON Schema 문자열·UTF-8 바이트·SHA-256 해시를 소유한다. 요청별 Fxx/Mxx는 일반 문자열 배열이며 membership과 공개 범위는 C# 품질 게이트가 검증한다.
- `NarrativeRequestContextBuilder`는 표현 특성, 출신, 문화, 야망, 실제·생물학적 나이, 생애 단계, 경력, 부상, 최근 사건과 공개된 잠재 유전 특성을 최대 24개 사실로 결정론적으로 투영한다. 미공개 잠재 형질은 컨텍스트·응답 trace에 들어가지 않는다.
- 영속 프로필은 Hard Reject에서만 한 차례 교정 요청하며, 유효 참조 하나를 사용하고 금지 위반이 없는 투박한 문구는 Soft Pass로 채택된다. 문체 약함만으로 재요청하지 않는다.
- V24 집중 시나리오는 6/6 통과했다. 10,000개 서로 다른 참조 컨텍스트에서도 프로필별 schema hash/reference가 변하지 않았고, 잘못된 F99/M99는 Hard Reject, 유효 F01/M01의 투박한 응답은 Soft Pass, 충분히 근거 있는 응답은 Strong Pass가 됐다.
- 첫 실제 모델 smoke는 정적 검사가 놓친 CharacterSkill JSON 닫힘 오류와 모델 입력의 stable-id 노출을 발견했다. 둘을 고친 두 번째 smoke에서 9개 schema 모두 Ollama structured generation을 통과했고 8/9가 즉시 품질 게이트를 통과했다. 남은 BubbleLine의 F/M 배열 혼입은 정적 `^Fdd$`/`^Mdd$` 패턴으로 문법 단계에서 차단한다.
- 20/profile 1차 실측은 파싱 실패 0이었으나 167/180이었다. `BubbleLine` 12건이 선택 사항인 reference 배열을 과도하게 채워 실패했으므로, 말풍선의 정적 schema는 `line`만 허용하도록 좁혔다. 이는 사실 grounding이 선택 사항이라는 계약을 따르며, 허구 F 참조를 Soft Pass로 완화하지 않는다.
- 진화 계보의 참여자 CharacterId는 내부 fact stable ID로만 유지하고 모델이 보는 label에서는 제거했다. 모델은 “기록된 소유자·제작자·사용자의 기여”만 보며 영속 ID를 이름처럼 문구에 복사할 수 없다.
- 20/profile 2차 실측은 179/180 accepted, fallback 1/180이었으나 SocialRumor 한 건이 256-token 상한에서 닫는 괄호 전에 잘렸다. 이 프로필만 384-token 상한으로 높이고, 스킬·계보는 강한 판타지/무협, 인물 기록은 중간 강도, 말풍선은 자연 구어체라는 중앙 문체 지침을 최종 model prompt에 넣는다.
- 3차 실측에서 SocialRumor와 BubbleLine은 각각 20/20으로 수정이 확인됐다. 남은 한 건은 FacilityEvolution의 복합 배열이 기본 256-token 상한에 걸린 미완성 JSON이므로, 복잡도에 맞춰 768로 조정한다. 이는 생성 길이를 강제하는 값이 아니라 schema 완료 전 최대 허용량이다.
- 최종 20/profile 실측은 179/180 accepted(99.4%), parse failure 0, fallback 1/180(0.6%)로 PASS다. 유일 폴백은 SocialRumor가 F01을 targetCharacterId로 사용한 응답이며 C# quality gate가 `Unknown inline reference`로 정확히 Hard Reject했다. CharacterSkill, Persona, FacilityEvolution, EvolutionHistory, CharacterRecord와 BubbleLine은 모두 20/20이다.
- 최종 TTFT 중앙값은 profile별 697.1~700.1 ms, p95는 698.8~897.1 ms였다. 스키마 구조가 큰 영속 프로필도 정적 schema 재사용 상태에서 1초 미만 p95를 유지했다.
- Final clean Unity validation passed after clearing the Console: V24 focused scenarios 6/6, Error 0 / Warning 0.

## 2026-08-09 - Phase 135 official source boundaries

- The Korea Heritage Service intangible-heritage overview classifies transmitted culture into traditional craft, oral expression, ritual, lifestyle, play/festivals, and martial arts. V25 uses these categories only as scenario and motif taxonomies; it does not copy catalogue prose.
- The official archery heritage record and Heritage Channel material on `Muyedobotongji` support treating martial practice as trained technique, ritual, record, and community memory rather than as generic combat vocabulary. Only that high-level framing is encoded in generation rules.
- Our Korean Dictionary and the Encyclopedia of Korean Culture are reference boundaries for terminology and folklore categories. Their example sentences, entry prose, and modern creative text are not copied into the dataset.
- The generated corpus uses 12 backgrounds, 18 ambitions, 32 life events, 20 practices, 56 general traits, 24 heritable traits, 61 equipment definitions, and 368 building definitions read from authoritative Unity YAML assets.
- Pair-aware filtering produces exactly 40,000 records from 50,000 raw scenarios. The 38,000 SFT candidate set includes the 6,000 preference-review rows; 2,000 evaluation rows are isolated by whole scenario family with zero family leakage.
- Player-facing prose audit covers 78,998 fields. Korean coverage is 100%, selected generic fallback phrases are 0, vocabulary entropy is 9.045316 bits, and exact duplicates among fields at least 40 characters long are 505/50,835 (0.9934%). Short names and fixed labels are reported separately rather than disguised as unique prose.
- Same-seed regeneration produced 19/19 byte-identical files with zero missing paths and zero SHA-256 mismatches. Gzip headers use a fixed timestamp so compressed artifacts are reproducible.

## 2026-08-09 - Phase 136 local reviewer findings

- The merge contract accepts one combined CSV through `--review-csv`, so the safest UI boundary is immutable eight-chunk input, separate atomic JSON autosave, and explicit combined CSV export.
- Exact automatic review warnings can cover malformed JSON, unknown F/M references, generic cliches, repeated-particle patterns, duplicate candidates, and A/B mechanical-field divergence. Semantic voice quality remains a human decision.
- The original rejected generator used one fixed sentence (`전설의 운명이 깨어나 모든 것을 바꾸었다`) across most review pairs. It was absent from SFT chosen completions but made preference review trivial and would teach DPO to avoid one phrase rather than improve narrative quality.
- The rebuilt review package contains zero copies of that fixed fallback and zero manufactured cliche warnings. It contains 2,347 records with an invalid fact/motif reference in one blinded fact-distortion candidate; these remain useful hard-reject tests, not automatic verdicts.
- Hard negatives now follow three deterministic contrast classes: generic-but-safe prose, invented-fact contradiction, and awkward motif listing. All retain the chosen payload's fixed mechanical fields.
- Official TRL documentation confirms that conversational prompt-completion datasets compute loss on completions only; the SFT projection therefore trains on the grounded assistant completion and excludes rejected candidates. Official bitsandbytes documentation supports NF4 on Windows/NVIDIA for this GPU class.
- A deterministic 16-bit text SimHash grouped all 8,000 records into 256 coarse similarity buckets. The UI exposes these as navigation/batch scopes while keeping the current visible-page bulk limit at 20 records.
- Browser runtime discovery returned no available browser bindings, so the local UI cannot receive pointer/screenshot evidence in this session. The implementation must retain HTTP/API, static DOM, CSP, keyboard-contract and responsive-CSS tests as nonvisual evidence and report the visual limitation honestly.
- Reviewer state lives under `Artifacts/Review/V25`, outside the generated `Artifacts/Training/V25` tree. Regenerating the deterministic corpus therefore cannot erase human review progress.
- The correct training boundary is now explicit: grounded chosen completions train the initial SFT adapter first; the 6,000 preference rows are human-reviewed afterward and only those explicit A/B/rewrite/drop decisions may feed DPO. Synthetic `systemPreferred` values remain navigation metadata, not human labels.
- The repeated fixed rejection phrase was a dataset-construction shortcut, not a model-generated alternative or an SFT target. It has been removed from all rebuilt review artifacts and retained only as a fail-closed leakage assertion in build/train code.
- Sustained local QLoRA is not currently safe on this configuration. The strongest causal evidence is the NVIDIA kernel event sequence (`UVMLiteProcess` error, then recovery state `Node Reboot Required`) during full CUDA load; the final `0x1E` dump still requires elevated WinDbg access for exact stack attribution. The project and training output also sit on external USB Disk 2, which logged an I/O retry and left the step-20 optimizer checkpoint corrupt.
# Phase 139 Git publish findings - 2026-08-09

- Local `main` is clean and one commit ahead of `origin/main`; commit `a525783` was not accepted remotely.
- VS Code's Git log records `GH001` after a 354-second push. Two `adapter_model.safetensors` files are 133.05 MiB each and were stored as ordinary Git blobs because `.safetensors` has no LFS attribute.
- Two optimizer checkpoints are 67.98 MiB and 62.16 MiB; GitHub warned about them even though they are below the 100 MiB hard blob limit.
- The mounted `DungeonStory-Qwen3-1.7B-Q4_K_M.gguf` is correctly tracked by Git LFS and must remain a release artifact.
- Two broken loose refs under `refs/codex/turn-diffs/checkpoints` independently cause `git pull` and automatic `git gc`/repack to fail. They are ephemeral Codex checkpoint refs rather than gameplay branches.
- The training-model directory contains 41 generated files totaling 574.78 MiB, all added only by the unpushed commit. Nine Python bytecode-cache files were also accidentally committed.
- The only non-LFS blobs at or above 50 MiB are the two 133.05 MiB adapters and the 67.98/62.16 MiB optimizer checkpoints, all within the generated training-model directory.
- The two broken refs contain valid object IDs, but their full paths are 270 characters. Git for Windows fails to stat those paths and reports a synthetic zero invalid pointer. Moving them out of the active `refs` namespace fixes enumeration without affecting `main` or any gameplay branch.

## 2026-08-09 - Phase 140 Colab Pro migration findings

- The active Colab browser session is signed in and the home banner identifies the subscription as Colab Pro.
- The prior local `checkpoint-20` remains excluded: the Colab workflow must perform a clean canary in its own Drive directory and must never upload or resume that corrupt checkpoint.
- The existing training entry point already rejects CUDA compute capability below 8.0, so a T4 allocation will fail closed; L4/A100-class runtimes are the supported Colab targets.
- Colab exposes a direct notebook-upload control on the signed-in home page. The repository notebook is therefore usable before a Git push, while its own Git clone still guarantees that the training code and LFS corpus come from the published main branch.
- The uploaded Drive notebook preserved all 11 authored cells and Colab assigned an L4 GPU runtime, satisfying the supported Ampere-or-newer target before code execution.
- The runtime connection is live as a Google Compute Engine GPU backend with approximately 53 GB RAM and 236 GB ephemeral disk; code execution can proceed without changing runtime type.
- Cell 1 created isolated canary run ID `20260809T030616Z`. Cell 2 confirmed an NVIDIA L4 with 23,034 MiB VRAM and reached the expected explicit Google Drive access prompt.
- The transient Drive prompt is no longer visible, but cell 2 remains running at `drive.mount` with no success or error output. No new authorization tab is exposed; this is a permission handoff wait, not GPU or training activity.
- The user completed the account-scoped Drive consent manually and reported the notebook's write probe as PASS. The uploaded notebook tab remains available at its Drive URL for the remaining automated cells.
- Browser verification confirms `Mounted at /content/drive` and `Google Drive write probe: PASS` after L4 detection. The selective shallow clone/LFS materialization cell has been started and is currently running without an error output.
- Selective Git LFS materialization passed with the expected 9,127,778-byte SFT archive. The pinned package installation is now running; no training process has started.
- Two 15-second checks show the quiet pinned dependency installation is still active without stderr or failure output. The L4 runtime remains assigned; continued waiting is expected because the pinned CUDA Torch wheel is large.
- After a further wait the cell no longer exposes a progress bar or stop control, but its success print is absent from rendered text. This is an ambiguous terminal state, so the next action inspects the cell's error/output DOM rather than rerunning the install blindly.
- Targeted DOM inspection resolves the ambiguity: cell 4 still carries Colab's `running` state and its output container is empty, with no error node. The pinned install is still active rather than failed or completed.
- Pinned dependencies completed successfully. The authoritative preflight passed on NVIDIA L4 compute capability 8.9 with Torch 2.7.1+cu126/CUDA 12.6, exactly 38,000 valid records, and dataset SHA-256 `92a495f759c78cc2dd3f6bf73f8ad31a6a9ca93ac51384ad1d9c8a58b2f6c11f`.
- The first canary run-button action produced neither a running state nor visible output/error. No evidence indicates training started, so the next action diagnoses the cell execution count/runtime state before any retry.
- Cell-level diagnostics show no execution count, run/stop button, or error payload; the general runtime marker also does not expose a useful busy label. To avoid a duplicate attempt or `exist_ok=False` collision, the workflow waits for delayed iframe output before considering another launch action.
- After 30 additional seconds the cell is unchanged and a fresh semantic query exposes an enabled `셀 실행` control plus its editor. This confirms the first action did not launch training; a fresh run-button action is safe.
- The fresh forced semantic run removed the cell's run control and it remains absent after 10 seconds, indicating the canary command is now accepted and initializing. Colab has not yet surfaced subprocess stdout in the outer cell DOM.
- After another 60 seconds the canary cell still had no visible outer-DOM output; the later screenshot proved this was not active initialization.
- User screenshot revealed the terminal state: the forced second execution failed at `CANARY_OUTPUT.mkdir(..., exist_ok=False)` because the first attempt had already created that directory. This is not a training crash; it is the notebook's deliberate overwrite guard. The canary must continue in a fresh `-retryN` path.
- 2026-08-09: Colab 브라우저 제어 런타임이 대화 중 재초기화되어 첫 상태 조회에서 `browser is not defined`가 발생했다. 브라우저 런타임을 다시 연결했으며 Colab 탭이나 런타임 상태에는 변경을 가하지 않았다.
- 2026-08-09: 인앱 브라우저의 열린 Colab 탭은 `browser.user.openTabs()`에서 확인됐지만 이전 제어 탭 ID는 무효였다. 사용자 탭을 새로 claim해 셀 7의 코드와 오류 상태를 다시 확인했다.
- 2026-08-09: Colab 셀 1을 한 번 재실행해 새 canary 경로 `/content/drive/MyDrive/DungeonStory/V25/models/sft-qwen3-1.7b-canary-20260809T032433Z`를 생성 대상으로 확정했다. 기존 실패 경로는 보존된다.
- 2026-08-09: 새 canary 셀 7은 한 번만 실행했다. 10초 후 셀 실행 버튼이 다시 표시되어 프로세스가 조기에 종료됐으며, 출력은 Colab의 접힌 출력 영역 때문에 일반 셀 텍스트 조회에서 보이지 않았다.
- 2026-08-09: 새 경로 canary는 `CalledProcessError(exit status 1)`로 6초 만에 종료됐다. Colab 출력에는 자식 프로세스 stderr가 남지 않아 원인이 가려졌다. 로컬 노트북과 현재 Colab 셀 7을 `capture_output=True`로 보강하고, 기존 출력 경로는 `-retryN`으로 보존하도록 했다.
- 2026-08-09: Colab CodeMirror 편집기에 Playwright `fill()`을 사용하자 기존 셀을 교체하지 않고 앞에 새 코드가 붙었다. 결과는 27행의 `...above')import subprocess` SyntaxError였으며 셀 본문은 실행되지 않아 새 출력/학습 상태 변경은 없다.
- 2026-08-09: canary exit 1의 실제 원인은 `torchvision::_meta_registrations`에서 `RuntimeError: operator torchvision::nms does not exist`가 발생한 것이다. Colab 기본 torchvision wheel이 고정된 `torch 2.7.1+cu126`과 ABI 불일치하며, 이어서 PEFT가 `PreTrainedModel`을 import하지 못했다. V25는 텍스트 전용이므로 torchvision/torchaudio/torchtext를 설치 셀에서 제거하는 것이 최소·안전 수정이다.
- 2026-08-09: Colab 셀 4를 텍스트 전용 설치 계약으로 갱신하고 실행했다. 고정 SFT 의존성 재확인 후 torchvision, torchaudio, torchtext 제거가 성공했으며 셀 출력은 `incompatible optional PyTorch media packages removed`로 종료됐다.
- 2026-08-09: 새 환경 진단 셀에서 `CANARY_OUTPUT=...20260809T033159Z`, `torchvision_spec=None`, `pip show torchvision` return code 1을 확인했다. 셀 7 실행 번호는 여전히 `[12]`였고 패키지 정리·새 RUN_ID·환경 진단은 `[13]~[15]`였으므로, 수정 후 canary 클릭은 실제 실행되지 않았음이 확정됐다.
- 2026-08-09: torchvision 제거 뒤 첫 실제 canary `[16]`은 Transformers AutoModel 매핑이 설치된 `timm`을 통해 Gemma3nConfig를 로드하면서 `No module named torchvision`으로 종료됐다. 텍스트 전용 계약에 `timm` 제거와 `USE_TORCH=1, USE_TF=0, USE_FLAX=0`을 추가했다.
- 2026-08-09: 최종 수정 canary는 새 경로 `.../sft-qwen3-1.7b-canary-20260809T034004Z`, 실행 번호 `[19]`로 실제 시작했으며 30초 경과 후에도 progressbar 상태로 진행 중이다.
- 2026-08-09: 사용자 요청에 따라 26분 이상 정상 계산 중이던 canary `[19]`를 Colab 실행 중지 버튼으로 종료했다. 중지 후 progressbar가 사라진 것을 확인했다. 전체 SFT는 실시간 로그 출력 방식으로 별도 경로에서 시작한다.
- 2026-08-09: 전체 SFT 셀 `[20]`을 사용자 명시 승인으로 시작했다. 명령은 `/usr/bin/python3 -u ... --output /content/drive/MyDrive/DungeonStory/V25/models/sft-qwen3-1.7b-v1 --epochs 2`이며 stdout/stderr를 상속해 Colab 셀에서 실시간 표시한다. 1분 12초 시점에 GPU RAM 3.6/22.5GB, 시스템 RAM 5.1/53GB로 실행 중이다.
- 2026-08-09: Unity MCP 두 엔드포인트 모두 프로젝트 루트 `F:/01_Programming/01_Project/02_Unity/DungeonStory`를 정상 반환했다. Unity 6000.3.8f1은 비재생·비일시정지·비컴파일 상태이고 Console Error 0, 기존 Warning 3건이다.
# 2026-08-09 Phase 141 — 게임플레이 연결·밸런스 증거 감사

- `docs/generated/V21_Gameplay_Connection_Report.md`는 정의→실행 입구→실제 효과→저장 소유자 1,194행, 미연결 0건을 증명한다. 이는 기능 연결성 증거이며 재미·난이도·경제 균형의 증거는 아니다.
- `Artifacts/QA/final-acceptance-report.txt`는 동기식 Editor 회귀·소스/콘텐츠 계약 범위에서 33/33 통과했다. 보고서 자체가 외부 PlayMode UI 게이트를 별도 범위로 명시한다.
- `Artifacts/QA/final-playmode-acceptance-report.txt`는 7개 대상, 2개 해상도, 32개 캡처와 Console 0/0을 통과했다. 따라서 종합 문서의 일반 UI 해상도 게이트는 오래된 상태이며, 남은 UI 검증은 V22 의복 수직 흐름처럼 신규 도메인에 한정해 다시 적어야 한다.
- 전체 월드 저장은 현재 증거상 68/68/68, 정규 기준선 일치와 라이브 기준선 복원이 통과했다.
- 종합 문서가 명시한 미완료 핵심은 실제 2,000 AI 에이전트 3세대 장시간 성능, 전투 승률·이정표 도달 시점 밸런스 프로브, V22 의복 2,000명 0B/CPU 회귀 및 10년 로트 선형성, 의복 제작→착용→목욕→세탁→수선→개조→부위 소실 회수 수직 PlayMode다.
- 현재 QA 산출물 이름/내용 감사에서는 전투 승률 분포, 이정표 도달 분포, 2,000 실제 AI 장시간 성능을 최종 승인하는 별도 보고서를 찾지 못했다.
- V23 `V23BalanceAudit`는 모든 건설 시설의 BOM/작업량, 조합식 입력·출력/작업량, 장비 placeholder 작업량 제거, 해체 회수량이 투입량을 넘지 않는지 검사하고 `V23_BOM_Work_Quality_Appendix.md`와 `v23-balance-audit.txt`를 만들도록 구현돼 있다. 그러나 현재 두 생성 산출물은 존재하지 않아 최신 루트 카탈로그에 대한 감사 실행 증거가 빠져 있다.
- 욕구 밸런스 보정 산출물은 표준 충분 공급 조건에서 2,430 표본, 평균 작업 비율 약 0.747, 결핍 피해/붕괴 0을 기록한다. 반면 극단 압력·공급 조합은 대규모 결핍 피해와 붕괴를 의도적으로 포함하므로, 이 파일은 생존 욕구 매트릭스 보정 증거이지 전체 게임 밸런스 증거가 아니다.
- 실제 1,000명 성능 산출물은 존재한다. 대표 보고서는 평균 91.7 FPS와 p95 15.37ms를 통과했지만 p99 76.86ms는 60 FPS 기준을 통과하지 못했고, AI 스케줄러 자체는 GC 0B이나 상당한 판단 지연/예산 소진을 기록했다. 따라서 2,000명·3세대 장시간 최종 게이트를 대체할 수 없다.
# 2026-08-09 Phase 142 — 전 게임 이론 밸런스 기준 설계

- 현재 권위는 현실 180초=게임 1일, 계절 30일, 연 120일, 연구 기준 실효 가동률 55%다. V23에서는 1 작업량=중립 주민의 실제 작업 1초이므로 기준 성인 1명의 계획 가능 노동은 하루 99 작업량이다.
- 기존 장기 목표 창은 중세 기반 약 30일, 초기 산업 80~100일, 성숙 산업 200~240일, 후기 룬 산업 320~400일, 시간 고정 선행 약 964일이다. 일반 이정표는 120~240일 경쟁, 대이정표는 960~1,200일 목표다.
- 제작 깊이는 원료 이후 최대 4단계이고 주요 물자는 물리 아이템이므로, 새 추상 재화를 추가하지 않고 개발용 그림자 원가만 사용해야 한다.
- V23 완성 품질 점수 임계는 `<20 형편없음`, `<35 저급`, `<55 보통`, `<70 양호`, `<83 우수`, `<95 명품`, `>=95 전설`이며 성능 배율은 각각 0.70/0.82/1.00/1.08/1.16/1.26/1.40이다.
- 해체는 제작량의 20~35% 작업을 추가로 요구하고 기본 회수율은 35~75%다. 이 값은 반복 품질 파이프라인의 기대 시도 비용과 무한 가치 순환 검증에 반드시 포함해야 한다.
- 첫 검색 명령은 여러 검색어를 하나의 고정 문자열로 잘못 묶어 결과가 없었다. 후속 검색은 정규식 대안으로 성공했으며 같은 실패 명령은 반복하지 않는다.
- 시설·도구 보정과 복잡도 페널티를 0으로 둔 현재 3d21(-10~10) 품질식의 이론 분포는 다음과 같다. 숙련 25는 형편없음 12.31%/저급 46.57%/보통 39.82%/양호 1.30%, 숙련 50은 저급 7.34%/보통 58.42%/양호 31.15%/우수 3.09%, 숙련 75는 보통 12.31%/양호 46.57%/우수 33.78%/명품 7.23%/전설 0.11%, 숙련 100은 양호 7.34%/우수 33.78%/명품 39.76%/전설 19.12%다. 이 표를 품질 반복 생산의 기준 분포로 사용할 수 있다.
- 현재 전투 검증은 피해, 주도권, 엄호, 연막, 소환, 제압, 난이도 배율과 목표 판정 같은 규칙 계약을 폭넓게 검사하지만 조우별 대량 승률·영구 부상·탄약 소모·회복 비용 분포를 산출하지 않는다.
- 전투 검색은 유효 결과를 반환했지만 일부 검색 대상에서 일치가 없어 `rg` 종료 코드 1로 표시됐다. 읽은 결과는 유효하며 이후에는 존재하는 파일을 좁혀 검색한다.

## Phase 142 proposed authority

- 전역 비용은 단일 점수가 아니라 `[직접 작업량, 내재 작업량, 달력 지연, 공간, 전력, 가역 위험, 사회 부담, 비가역 위험, 플레이어 주의력]` 벡터로 본다. 내재 작업량은 물리 생산의 감사용 그림자 원가이며 게임 재화로 노출하지 않는다.
- `내재 작업량(output) = (입력 내재 작업량 + 직접 작업 + 예상 운반 + 에너지/정비 배부 + 평균 손실) / 기대 유효 출력`으로 재귀 계산한다. 사망·관계 파탄·고유 유물은 이 식에 넣지 않고 발생 확률과 회복 불가능성을 별도 게이트로 둔다.
- 기준 주민 1명은 하루 99 작업량, 시작 3명은 297 작업량이다. 정상 운영은 필수 유지 25~35%, 물류 12~20%, 성장·연구 35~50%, 비상 여유 10~20%를 목표로 한다. 자동화는 물류 노동을 35~55% 줄이되 전력·정비가 5~10%를 다시 소비해 순노동 회수 20~35%를 목표로 한다.
- 진행 기준점은 날짜 잠금이 아니라 분포다. 첫 일반 이정표는 p10 120일 이상, 중앙 180~220일, p90 300일 이하; 첫 대이정표는 p10 900일 이상, 중앙 1,020~1,100일, p90 1,250일 이하를 초기 목표로 둔다.
- 정상 충분 공급의 생존 기준은 평균 생존행동 비율 18~28%, 평균 작업 비율 55% 이상, 결핍 피해·붕괴 0, 작업 차단 5% 미만, 종족 결과 편차 15%p 이하다.
- 경제 순환은 일반 가역 루프 회수 가치 95% 미만, 해체·재건/품질 재굴림 루프 85% 미만을 요구한다. 외부 구매는 내재 원가보다 25~50% 비싸고 판매는 30~50% 낮게 받아 매입→제작→분해/판매 무한차익을 막는다.
- 품질 기준 분포상 숙련 25의 보통 이상은 41.12%, 숙련 50의 양호 이상은 34.24%, 숙련 75의 우수 이상은 41.12%, 숙련 100의 명품 이상은 58.88%다. 기본 반복 목표는 이 2~3회 기대 시도권에 두고, 기대 시도 20회를 넘는 목표에는 강한 경고를 준다.
- 조우 난도 목표는 준비된 동시대 파티 기준 일상 85~95%, 표준 65~80%, 위험 45~65%, 보스 초견 25~45%/정보·대응 준비 후 55~70% 승률이다. 승률과 함께 라운드, 탄약, 영구 부상, 회복 노동, 포획 성공과 손실 내재 작업량을 기록한다.
- 전역 검증 축은 날짜 1/3/10/30/60/120/240/400/960/1,200, 인구 3/10/30/100/500/2,000, 5기후, 9종족, 균형/연구집중/군사집중/서비스집중/자동화집중/계보집중 정책이다.
# 2026-08-09 Phase 143 — 밸런스 권위 문서와 에이전트 강제 절차

- 저장소 루트에는 실제 지침 파일 `AGENT.md`가 존재하고 `AGENTS.md`는 없다. 사용자가 말한 대상은 이 프로젝트의 실제 `AGENT.md`로 해석한다.
- 전체 게임 설계 문서는 계속 상위 제품 권위로 두고, 변동 가능한 수치 기준·공식·목표 분포·검증 절차는 별도 권위 문서로 분리해야 중복과 불일치를 피할 수 있다.
- 향후 에이전트 규칙은 시설·아이템뿐 아니라 레시피, 장비, 의복, 연구, 사건, 세력, 조우, 이정표와 기존 수치 변경까지 포함해야 한다.
- `AGENT.md`는 UTF-8로 읽으면 정상이며 상단에 Fallback Policy, Unity MCP, 구현 전 설계 게이트가 있다. 새 밸런스 게이트는 Fallback Policy 다음의 높은 가시성 위치가 적절하다.
- 기존 `docs/game-design/economy-and-time.md`는 경제·시간 영역 문서일 뿐 전투·사회·연구·이정표까지 포괄하는 전역 밸런스 권위는 없다. 새 문서는 `docs/game-design/whole-game-balance-baseline.md`로 두는 것이 기존 디렉터리 역할과 맞는다.
- 종합 문서는 상단에서 단일 기준 문서를 표방하고 V23 작업량 공식을 직접 포함한다. 새 문서를 별도 숫자 권위로 도입할 때 종합 문서 상단에 권위 관계와 링크를 명시하고, 기존 공식은 세부 구현 설명으로 유지하되 충돌 시 새 밸런스 기준 문서를 우선하도록 해야 한다.
## 2026-08-09 - Phase 143 balance authority findings

- 전역 밸런스의 숫자·분포·교환율·검증 절차 권위는 `docs/game-design/whole-game-balance-baseline.md`로 분리했다. 종합 문서는 게임 정체성·콘텐츠·시스템 계약을, 기준서는 수치 목표와 검증 절차를 소유한다.
- 새 기준서는 496행이며 시설·아이템·재료·조합식·장비·의복·연구·종족·농축산·의료·사건·세력·전투·이정표·엔드리스까지 전 영역을 다룬다.
- 저장소의 실제 지침 파일은 루트 `AGENT.md`다. 여기에 신규 콘텐츠와 모든 밸런스 수치 변경 시 기준서 선행 확인, 비용 벡터 산정, 대체재 비교, 악용 루프 검사, 자동 감사 연결과 결정론 검증을 의무화했다.
- 예외는 이유·교환·악용 방지·검증 근거와 명시적 설계 결정이 있어야 하며, 그렇지 않으면 `밸런스 검증 보류`로 남긴다.
- 종합 문서 상단과 콘텐츠 작성 원칙에서 기준서를 직접 연결했다.
- 대상 문서의 링크·헤더 구조를 확인했고 `git diff --check`가 통과했다. 이번 단계에서는 실제 게임플레이 수치를 변경하지 않았다.
- 기존 `.gitignore`의 `docs/` 규칙 때문에 권위 문서가 커밋 대상에서 제외되고 있었다. 다른 문서는 계속 제외하되 종합 문서와 전역 밸런스 기준서만 추적 예외로 열었다.
## 2026-08-09 - Phase 144 initial live-balance audit

- `V23BalanceWorkCalculator` and `IBalanceWorkCalculator` already exist, so live calibration should extend one shared calculator/audit path rather than introduce a second numerical authority.
- Live authored construction work is not yet aligned with the new baseline: many industrial facilities use roughly `36~60` work while the theoretical industrial-class reference is `280` before footprint/capability/material factors. Medical facilities span roughly `34~128`; modular furniture/support pieces cluster near `18.8~32.3`. These values need classification-aware comparison rather than a blind multiplier.
- `ResearchOverhaulContentAssetBuilder` still sets `constructionWorkRequired = 90f + index * 3f`, which directly violates the new prohibition on research/asset-index-based costs. This is a priority upstream defect.
- Recipe assets contain many hand-authored `requiredWork` values while the V23 calculator provides derived work formulas. The audit must determine which path is live authority and reject divergence instead of silently accepting both.
- Existing probes include `FullGameManualQaRuntimeProbe`, research/combat probes, numerous PlayMode verifiers, and V23 economy calculators, but the initial search did not reveal one whole-game baseline comparison report covering every catalog domain.
- The shared calculator is live, not documentation-only: construction work is queried from `WorkAmountSystem`, recipe work from `ProductionBillRuntime`/projection, equipment work from `CombatEquipmentCraftingRuntime`, and apparel work from `ApparelWorkOrderRuntime`.
- `V23BalanceAudit` already emits construction/recipe/equipment/apparel rows, but its visible validator checks only that calculated work is positive. It does not yet prove authored BOM coherence, target-band compliance, asset/runtime parity, throughput, affordability, or cross-system loops.
- Several consumers accept the calculator as optional and fall back to authored values. That preserves runtime compatibility but permits two balance outcomes depending on composition; the audit must verify production composition always supplies the calculator and eliminate silent balance-authority divergence.
- `V23BalanceWorkCalculator.ResolveProductionProcessClass` infers process class from string fragments. This is deterministic but fragile and can misclassify translated/new IDs; a typed authored process class or exhaustive audit is needed before treating the formula as final authority.
- The research facility builder also uses index-based `constructionCost = 80 + index * 4` and `maintenance = 1 + index / 12`, in addition to index-based work. All three must be replaced with role/BOM/capability-derived values.
- The same builder's `FacilityBomProfile.Legacy` path chooses its main construction material by index (`lumber` → `machine-parts` → `precision-parts`) and quantity by `4 + index / 10`. This is the most serious upstream violation because it makes BOM composition itself depend on catalog order. Every legacy facility spec needs a semantic BOM profile or explicit BOM.
- Eight semantic facility BOM profiles already exist (`ARecordDesk` through `HObservationTower`) and can be expanded/reused. This provides a migration path without hardcoding hundreds of unrelated one-off formulas.
- The research overhaul contains exactly 101 facilities: 53 already use semantic BOM profiles and 48 still use the legacy index path. The first patch can therefore remove the legacy branch completely while preserving the fixed facility count.
- `BuildingEconomyAbility.constructionCost` is a hidden legacy integer still projected through building accessors and several builders, while physical construction uses BOM plus work. It must be treated as a derived economic valuation/settlement field, not as permission to pay gold instead of materials; its live consumers need confirmation before changing its scale.
- Other facility builders (defense, captivity, medical, industrial, service rooms, modular) also author construction cost/work separately. After the 101 research facilities are corrected, the whole-game audit must apply the same semantic rule across those builders rather than declaring research facilities representative of all buildings.
- The research-overhaul item/recipe builder also assigns recipe work as `8 + index * 0.75`. Thus both facilities and 100+ physical recipe outputs can change cost when catalog ordering changes. Recipe work must be derived from typed process class, input/output count and material handling, matching `V23BalanceWorkCalculator`.
- The same item builder assigns `UnitPrice = 12 + index * 2` and `UnitWeight = 0.25 + index * 0.03`. These fields feed the fallback material economic profile (`log(UnitPrice)` and `sqrt(UnitWeight)`), so catalog order currently changes live recipe/construction work factors as well as trade value and hauling cost. This is an upstream economy authority defect, not cosmetic metadata.
- `ResourceItemKind` and `ResourceIngredientTag` already provide typed semantic inputs sufficient to derive stable base value/weight bands for generated items. The correction should use kind, ingredient family, input count/quantity, output amount and shared/consumable role, never the index.
- Construction orders use the shared calculator when registered and only fall back to authored `BuildingWorkAmountAbility` work otherwise. Because optional fallback exists, corrected builders should still author semantic fallback work even after production composition is verified.

- All 101 research reward facilities are authored as `1x1`. Their construction differentiation therefore comes primarily from execution classification, capability count, BOM/material factor, infrastructure and maintenance—not footprint. Semantic BOM and execution classification must carry the intended era/role difference.
- The baseline requires explicit recovery periods and processing capacity. Existing research facility construction code sets a uniform production buffer capacity of 4 and facility capacity of 1; those are later throughput-calibration targets and should not be silently varied during the initial BOM/work authority patch.


- The current generated V23 appendix/report was not found in the inspected output locations; only an `Artifacts/QA/NeedBalanceCalibration` entry appeared. The audit must be runnable and its evidence regenerated after strengthening.
- The V23 audit currently generates both a detailed appendix and a terse report. It should become the machine-readable baseline gate and include mismatch reasons, category distributions, affordability/throughput bands, and exploit-loop findings rather than adding a parallel report generator.

- The second targeted search named a nonexistent `Assets/Scripts/Services/Apparel/Editor` directory and exited 1 after still returning the valid references from other paths. Future commands will discover directories with `rg --files` before passing them to `rg`.

- The initial broad `rg | Select-Object` inventory commands returned exit code 1 after producing bounded output because the downstream limiter closed the pipeline. The evidence was readable, but future inventories should collect paths first or avoid treating a bounded pipeline as a success signal.
## Phase 144 Unity connection checkpoint (2026-08-09)

- Unity MCP is now exposed to this task. Two registered Unity MCP server instances are visible, each providing project data, editor control, command execution, console, asset, scene and profiler operations.
- Validation should therefore use Unity MCP first; mouse/keyboard automation is neither necessary nor appropriate for the remaining balance pass.

## Phase 144 first-patch static check (2026-08-09)

- The prohibited research catalog index no longer participates in price, weight, recipe work, construction work, maintenance, repair, cleaning or BOM selection. Remaining `index` uses only allocate stable asset IDs and filenames.
- `V23BalanceWorkCalculator.CalculateRecipeBaseWork` is structurally sound: it calculates the process/input/output/support complexity subtotal, applies the 0.65 output-quantity exponent, and leaves input-material difficulty to the instance calculation exactly once.

## Phase 144 Unity pre-compile state (2026-08-09)

- The connected editor is Unity `6000.3.8f1`, idle, not playing, not compiling and not updating.
- The pre-existing console has 0 errors and 3 warnings. One is Unity's Input Manager deprecation notice; two come from Unity MCP failing to parse the Microsoft Store signature of Codex. These are infrastructure warnings, not game-code compilation failures, but the final game gate still requires a freshly cleared 0/0 console after the verification run.

## Phase 144 first Unity compile (2026-08-09)

- The forced Unity refresh exposed one real compile error: `ResearchOverhaulContentAssetBuilder.cs:36` still references the removed `FacilityBomProfile.Legacy` default in a declaration. This is a bounded cleanup error from the semantic-profile migration, not a formula-design failure.

## Phase 144 research asset regeneration entrypoint (2026-08-09)

- `ResearchOverhaulContentAssetBuilder.EnsureAssets()` is the authoritative regeneration entrypoint. It creates the three required folders, rebuilds facilities, items and recipes, then reindexes item definitions through the root content catalog builder.
- The builder is public editor code and can be invoked directly through Unity MCP without menu/mouse interaction.

## Phase 144 existing V23 audit coverage (2026-08-09)

- `V23BalanceAudit.Generate()` already loads the root catalog, builds material/work/salvage services, validates 354 recipes, 61 combat equipment, 56 apparel and 12 apparel materials, writes an appendix/report, and throws on failures.
- Current building/recipe checks are only structural: unique IDs, nonempty physical BOMs, positive calculated work and no abstract `stock-item:*` inputs. They do not yet compare authored work to the theoretical baseline, measure category distributions, detect placeholder/generated source patterns, or test value/throughput loops.
- Equipment validation only rejects primary-material absence and the old `requiredCraftWork <= 6` placeholder; apparel validation only checks anatomy and material compatibility. This audit is a useful execution shell but is not sufficient evidence of whole-game balance.

## Phase 144 first live V23 audit result (2026-08-09)

- The regenerated catalog reaches the audit, but the audit currently fails 27 recipes because it requires both concrete inputs and outputs for every recipe.
- The failures are concentrated in authored source/gathering outputs (`source:*`, `recipe:resource:*`), spoilage/incineration transforms and animal-product sources. These are legitimate process-shape exceptions that must still be validated semantically; weakening the check globally would hide real empty recipes.
- Next correction: classify recipe process roles explicitly enough to allow inputless physical source recipes and outputless terminal disposal recipes while continuing to reject accidental empty production definitions.

## Phase 144 recipe-shape inspection (2026-08-09)

- `ProductionProcessKind` distinguishes only `WorkOnly` and `PassiveBatch`; it does not encode source versus disposal semantics.
- The failed definitions are authored as regular recipe assets across the research, resource and workshop builders. Stable ID prefixes (`source:*`, `recipe:resource:*`, `recipe:incinerate-*`) currently carry the missing semantic role.
- A safe interim audit can use narrowly enumerated stable-ID families for inputless sources and outputless disposal, while a later content-contract improvement should replace prefix inference with an explicit recipe flow role.

## Phase 144 recipe asset semantics (2026-08-09)

- Live source assets have zero inputs and one or more physical outputs; disposal assets have one or more physical inputs and zero outputs. Neither is actually an empty definition.
- `ResourceEconomyAssetBuilder` already expresses these concepts through separate `SourceWork`/source helpers and `Sink`, but serializes both into the same two-value `ProductionProcessKind` contract. The semantic distinction is lost after build.
- Research-generated `recipe:resource:*` entries are also inputless sources. Their work was recalculated to 20 WU by the new base-work formula, while legacy source assets still contain authored 10 WU and incineration remains 6 WU; this confirms that cross-builder work normalization is still required after the first patch.

## Phase 144 recipe flow authority insertion point (2026-08-09)

- `ProductionRecipeSO` can accept a new serialized `ProductionFlowRole` without changing runtime process timing. An editor-only `ConfigureFlowRole` setter lets builders persist the role while preserving existing `Configure` and `ConfigureWorkshop` call contracts.
- `ResourceEconomyAssetBuilder.BuildRecipes` has all inputs/outputs available at asset creation, so it can assign `Source`, `Sink` or `Transform` deterministically from the authored physical flow shape. Research-overhaul recipes can do the same after their concrete inputs and outputs are configured.

## Phase 144 explicit recipe flow implementation (2026-08-09)

- `ProductionRecipeSO` now owns a serialized `ProductionFlowRole` (`Transform`, `Source`, `Sink`) distinct from active/passive timing.
- Resource-economy assets assign the role from the authored physical flow shape during `Rebuild()`; research-overhaul assets assign inputless physical-resource outputs as `Source` and ordinary conversions as `Transform`.
- This converts a previously implicit builder-only concept into a catalog contract that runtime audits and future balance tools can validate without stable-ID prefix inference.

## Phase 144 audit-to-baseline gap (2026-08-09)

- The current generated report is only six summary counts plus `failures=0`; it contains no category distributions, EWU, throughput, payback, recovery or dominant-loop evidence.
- The live root catalog contains 405 player building definitions, not the older planning estimate of roughly 369. The audit must treat the root catalog count as authority and report the difference rather than hard-code the estimate.
- The theoretical authority requires 99 WU per worker-day, 55–65% of the starting party's first 3-day labor for a basic shelter, explicit facility BOM/work/capacity/maintenance/recovery, <95% reversible-loop recovery, <85% dismantle/rebuild recovery and staged facility payback bands. None of those criteria are represented in the existing report yet.

## Phase 144 building catalog scope correction (2026-08-09)

- The 405 `BuildingSO` count includes negative-ID technical representation assets. The generated appendix shows repeated directional/runtime variants with negative IDs before the actual player building catalog.
- Therefore the earlier inference that 405 are player-buildable definitions was wrong. The planned ~369 player-building count is consistent with 405 total minus 36 technical variants, and the audit must explicitly filter technical representations before balance counts/distributions.
- The current appendix also reveals that many legacy buildings still use a one-item BOM and classification-driven work. This is real evidence that construction BOM plausibility is not globally solved by the research-overhaul patch.

## Phase 144 technical building identification (2026-08-09)

- Negative building IDs are formally owned by `RuntimeBuildingArchetypeCatalog`: world resource/filth targets and zone-layer archetypes begin around `-1,950,000,000`.
- The authoritative `BuildingSO` implementation is `Assets/Scripts/Services/Buildings/SO/BuildingSO.cs`.
- Filtering `building.id < 0` is therefore grounded in the runtime archetype contract, not an arbitrary count workaround; player balance reports should use non-negative IDs while a separate integrity check continues to cover the technical archetypes.

## Phase 144 facility report observations (2026-08-09)

- The generated facility appendix is materially useful but currently includes technical negative-ID archetypes and therefore distorts player-facing distributions.
- Many ordinary positive-ID legacy facilities use one-unit BOMs such as one steel ingot while classification/ability multipliers produce 68–172 WU construction. That is labor-heavy but materially implausible for storage/service infrastructure.
- `RuntimeBuildingArchetypeCatalog` intentionally indexes all building IDs (including technical ones) for runtime lookup, so the root catalog must retain them; only balance-report scope should separate them.

## Phase 144 BuildingSO balance surface (2026-08-09)

- `BuildingSO` exposes effective gameplay classification, footprint, ability modules and legacy gold maintenance. Construction BOM/work are supplied through abilities/extensions rather than simple fields.
- Classification is inferred from typed abilities when not explicitly authored, so category-based reports can be generated consistently across legacy and newer assets.
- Positive-ID player buildings should be audited for physical BOM, calculated construction work, footprint, capabilities, maintenance and recovery; negative-ID runtime archetypes should be checked only for identity/availability and excluded from economic distributions.

## Phase 144 report enrichment hooks (2026-08-09)

- `V23BalanceAudit.BuildReport` currently receives only the already-selected building collection, so the generator should split all/player/runtime building arrays before validation and pass the runtime count separately.
- Physical construction BOM is available through `BuildingAbilityAccessors.GetConstructionMaterials`; calculated construction work and its classification are centralized in `V23BalanceWorkCalculator`.
- Construction categories already map typed facility use to the baseline classes (structure through landmark), making per-class count/min/median/p90/max reporting possible without adding new content fields.

## Phase 144 first quantitative live distribution (2026-08-09)

- Correct catalog scope is 363 positive-ID player buildings plus 42 negative-ID runtime archetypes (405 total). The design document's approximate 380-facility assumption remains non-contractual; live root catalog validation is authoritative.
- The research-overhaul patch materially improved workstation BOM diversity: 97 workstations now have median 5 BOM kinds and 18 units. However decoration (65), storage (40), many service/environment/defense entries and 19 industrial facilities still have one-kind BOM minima, confirming uneven builder quality.
- Construction work bands are coherent and monotonic by class: structure p50 30 WU, storage 76, workstation 168, service 214, environment 268, defense 336, medical 296, precision 340, industrial 468, arcane 560, landmarks about 2,412–2,568 WU.
- Landmark work is about 24.4–25.9 worker-days, below their final physical-material/pressure significance but not directly invalid because landmark payback is evaluated as civilization impact rather than ordinary economy recovery.
- Recipe work is heavily concentrated: transforms p50 16 WU/p90 48/max 222, sources p50 12, sinks fixed at 6. The flat 6-WU sink band and 4-WU transform minimum warrant targeted review for trivial disposal/conversion exploits.
- Enriched audit generation passes with Unity Console Error 0 / Warning 0.

## Phase 144 construction normalization surface (2026-08-09)

- Every physical facility BOM is owned by `BuildingWorkAmountAbility.SetConstructionMaterials`; the same ability also stores authored construction, repair, cleaning, research and operation work plus the default worker policy.
- Existing builders set this ability independently, which explains the uneven legacy BOM quality across domains.
- A central editor-time normalization pass can safely update weak functional-facility BOMs through the existing setter and then persist assets. It must preserve plausible handcrafted BOMs and allow one-material structure/decoration assets.

## Phase 144 shared construction material vocabulary (2026-08-09)

- Confirmed shared physical item IDs for semantic facility templates: lumber, stone block, treated lumber, iron/steel ingots, cloth, machine parts, precision parts, engineering drawings, rune conductor and mana alloy.
- These materials span structural frame, fittings, mechanisms, precision calibration and arcane conduction without inventing fake sinks or quality tiers.
- A normalizer can therefore add only functionally justified material categories and validate every referenced item against the root item catalog before mutating assets.

## Phase 144 low-BOM inventory interpretation (2026-08-09)

- A naive class-wide minimum flags 180 facilities: Storage 40, Environment 38, Workstation 33, Defense 30, Service 25 and Industrial 14.
- Many flags are true problems (steel-ingot-only shops, machine-parts-only advanced machinery, lumber-only incinerator), but others are legitimate simple furniture (wooden chair, shelf, torch, training dummy). Therefore minimum BOM diversity cannot be enforced solely from broad facility class.
- The correct patch boundary is the originating content builders and semantic facility profiles: simple furniture may remain one/two materials, while powered, plumbed, precision, defensive and industrial assets require functional fittings/components. The low-BOM list remains a diagnostic, not a hard failure yet.

## Phase 144 modular facility root cause (2026-08-09)

- `ModularFacilityAssetBuilder.CreateWorkAmountAbility` is the main source of 1000-series weak BOMs: phase 1 always becomes lumber, phase 2 stone, phase 3 machine parts, plus only five hard-coded installation-item exceptions.
- Construction work in the same builder is derived from legacy gold construction value (`12 + cells*6 + constructionValue*0.02`) rather than the centralized V23 balance calculator.
- This violates the baseline prohibition on phase/index-derived material logic and produces implausible results such as stone beds, lumber toilets/incinerators and machine-parts-only advanced facilities. The builder must be refactored to semantic profiles before regenerating its assets.

## Phase 144 modular semantic inputs available (2026-08-09)

- Each of the 104 modular facilities already has semantic fields sufficient for balancing: stable code, visual form, roles, supported work types, runtime type, traits, capacity, stock capacity, phase, footprint width and room contribution.
- The builder can derive construction profiles from visual form plus typed roles/work rather than adding 104 hand-authored arbitrary values.
- `BuildAll()` is the authoritative regeneration entrypoint; after refactoring it can rebuild all modular assets and preserve the catalog's exact 104-part contract.

## Phase 144 modular rebalance result (2026-08-09)

- Rebuilt all 104 modular facilities using form/trait/role-driven BOMs and baseline-class work instead of phase/gold-value formulas.
- Broad low-diversity review candidates fell from 180 to 123, a reduction of 57, while simple furniture intentionally remains simple.
- Examples now match physical intent: beds use lumber+cloth, toilets/sinks/drains use stone+iron, workbenches use treated lumber+iron+stone, production machinery adds machine/precision parts by actual production complexity, and mana facilities add rune conductors/mana alloy.
- The audit still passes with Console 0/0. Remaining candidates are dominated by legacy/P1 assets and false positives from broad class thresholds; the diagnostic must be refined before becoming a hard gate.

## Phase 144 P1/legacy facility scope and defense root cause (2026-08-09)

- Several low-ID P1 room/shop assets are legacy room aggregates that `ModularFacilityAssetBuilder.HideLegacyRoomAssets()` deliberately disables after decomposing them into modular facilities. They remain in the root catalog for compatibility and should be reported as deprecated compatibility assets, not balanced as current build choices.
- Active P1 defense assets are rebuilt by `P1DefenseFacilityAssetBuilder`, which repeats the same legacy pattern: construction work from gold cost and width, and a steel-ingot-only BOM.
- The next active-content correction should refactor P1 defense construction by defense mechanism (mechanical trap, fluid/poison, thermal vent, lightning/rune, guard facility) while the audit learns to separate deprecated compatibility assets from current player-buildable definitions.

## Phase 144 compatibility asset marking point (2026-08-09)

- Modular assets are created in `EnsureBuildingAsset`, while the 21 prior room aggregates are disabled in `HideLegacyRoomAssets`.
- A serialized compatibility-deprecation flag on `BuildingSO`, set false for rebuilt modular content and true for hidden legacy rooms, provides a stable scope boundary without confusing research-locked `unlocked=false` facilities.

## Phase 144 remaining BOM candidates by builder (2026-08-09)

- The 100 active review candidates group cleanly by asset source: Industrial 34, Modular 32, Medical 13, Captivity 9, P1 5, ServiceRooms 4 and Combat 3.
- Thirteen conveyor/logistics assets (IDs 9840–9852) are machine-parts-only, proving the industrial builder still applies a generic single-material construction rule to directional belts, ports, splitters, lifts and gates.
- This grouping gives a bounded builder-by-builder repair order; active content can be corrected without a global mass rewrite.

## Phase 144 industrial builder root cause (2026-08-09)

- `IndustrialInfrastructureAssetBuilder.EnsureBuilding` authors construction work as `24 + width*12` and a single material selected from facility code, with quantity `2 + width`.
- It then attaches typed power, fluid, conveyor, automation and processor abilities, so the semantic information needed for a proper BOM exists but is applied too late for the current construction formula.
- Refactor should build abilities first, then derive BOM/work from typed industrial capabilities (structure, cable/pipe, mechanisms, precision control, storage), not from code prefixes alone.

## Phase 144 industrial catalog shape (2026-08-09)

- The industrial builder owns utility conduits (U), machines/generators/storage/control (I), water/waste processors/fixtures and conveyor/logistics parts (C) under one `Spec` with typed ability factories.
- `EnsureAssets()` is the bounded regeneration entrypoint and also patches sanitation, production-fluid consumers and existing production facilities.
- Refactoring the single `EnsureBuilding` construction block will therefore update all 34 industrial low-BOM candidates consistently while preserving downstream patch steps.

## Phase 144 first industrial rebalance result (2026-08-09)

- Typed industrial rebuild reduced broad low-BOM candidates from 100 to 82. Industrial work is now concentrated around 400–520 WU and material diversity rose from one-kind machine-parts-only assets to median 3 kinds.
- The first pass exposed an ordering bug in the new semantic resolver: conveyors also carry a power utility connection, so checking utility before conveyor produced stone/iron/cloth conduit BOMs. The resolver now prioritizes conveyor mechanics.
- The original broad minimum of four industrial material kinds and three workstation/service kinds also over-flagged legitimate simple belts, hearths, tables and furniture. Review thresholds are being narrowed to detect missing functional categories, not reward material-count inflation.

## Phase 144 service-room automation root cause (2026-08-09)

- Four powered service-room supports are intentionally identified by `RequiresPower` but currently receive only two machine parts and 24.8 WU; non-powered supports receive only two lumber and 18.48 WU.
- This is far below the baseline service/environment work bands and omits frame, wiring and controls from powered automation.
- `ServiceRoomContentAssetBuilder.EnsureAssets()` is the bounded regeneration entrypoint; it should use structural materials for all supports and add machine/precision components plus higher work for powered supports.

## Phase 144 final five defense normalization point (2026-08-09)

- The four upgraded P1 traps and treasury bolt thrower are not created by the main defense spec list, but `EnhanceAllDefenseAssets()` already enumerates every active `BuildingDefenseAbility` asset after creation.
- This post-pass is the correct place to normalize any defense facility whose authored BOM lacks functional diversity, using the live defense concept/effect contract while preserving already-valid semantic BOMs.

## Phase 144 live defense normalization inputs (2026-08-09)

- `DefenseFacilityData` exposes the exact live concept, typed effect array, supply mode, power requirement/demand, family and trigger contract needed for post-build normalization.
- The post-pass can reuse the spec-driven material profiles with `(concept, width, effect count, treasury/power)` and only replace BOMs below two distinct material kinds.

## Phase 144 builder ownership conflict (2026-08-09)

- Re-running the defense builder after compatibility marking exposed that P1 Guard Room (ID 35) is both in the deprecated modular-replacement list and in the legacy defense spec list.
- The defense builder unconditionally set every spec asset `unlocked=true`, overriding the modular compatibility authority and causing the audit to fail.
- Resolution: compatibility deprecation is authoritative; the defense builder may keep the legacy asset mechanically valid for old references but must not unlock it when the explicit flag is set.

## Phase 144 defense builder semantic inputs (2026-08-09)

- Each active defense asset already carries stable name/ID, footprint, placement layer, attack concept, trigger timing, target rule, cooldown/period, supported work, staffing, typed effect assets and treasury-power behavior.
- These fields are sufficient to author construction BOMs by mechanism without adding per-index costs: physical defenses need frame/steel/mechanisms, poison/corrosion need sealed stone/metal plus containment, elemental defenses need mechanisms/rune conductors, and staffed guard facilities need structural/furnishing materials.

## Phase 144 defense concept vocabulary (2026-08-09)

- The defense domain has exactly seven stable concepts: None, Physical, Poison, Fire, Lightning, Ice and Guard.
- This is an appropriate exhaustive switch for construction profiles and avoids brittle display-name parsing.
## 2026-08-09 — V23 recipe process/work authority implementation

- `ProductionRecipeSO` now persists an explicitly authored `ProductionProcessClass`; runtime balance code fails loudly when that authority is missing instead of inferring a process from recipe names or tags.
- All five catalog recipe authoring paths now assign process class and flow role: resource economy, production workshop, research overhaul, V22 apparel, and surgery/prosthetics.
- A shared editor authoring utility maps exact workstation roles to process classes and normalizes persisted recipe work with the full material handling/value multiplier after the relevant catalogs are rebuilt.
- `V23BalanceAudit` now rejects recipes whose process class is unauthored or whose persisted `requiredWork` differs from the authoritative calculator.
- Scoped source `git diff --check` passed. Only repository line-ending advisories were reported; Unity compilation and content regeneration are still pending.
- The research-overhaul workstation `workstation:v3:subterranean` legitimately owns two different transform processes: nitrate fertilizer is chemical processing, while mushroom substrate is simple mixing. Facility-tag-only classification is therefore insufficient for this exact case; the authoring map must include the stable output item ID as a discriminator while remaining explicit and fail-loud.
- `SurgeryContentAssetBuilder.RebuildAll()` performs a broad `ResearchProjectAssetBuilder.Rebuild()` after medical assets, so it also runs the branched production consumer gate. The four reported zero-consumer products are created by research-overhaul recipes and are not referenced by any recipe inputs or typed runtime consumers. This confirms a real gameplay-link gap rather than a medical build failure.
- `V21OperationalContentLinkBuilder.WireInstallationComponents` is the existing authoritative path for adding physical installation items to the construction BOMs of facilities unlocked by a research project. It is therefore the correct owner for `factory-installation-plan`, `precision-gauge`, and `rune-bus-coupler`. `paper-paste` is not an installation part; it should be a real recipe input, most coherently in the later factory installation plan/drawing assembly chain.
- V21 research consolidation removes the original factory-layout and rune-grid project IDs. Installation wiring for these products must therefore match the exact `BuildingSemanticTagsAbility` research tag on the facility, not the merged survivor research package, or unrelated facilities would inherit the BOM item.
- Regenerated `Artifacts/QA/v23-balance-audit.txt` now reports `failures=0` for 342 active player buildings, 354 recipes, 61 equipment definitions, 56 apparel definitions, and 12 textile materials. Persisted recipe work equals the authoritative calculation across the catalog.
- Recipe work distribution is Transform min/p50/p90/max `8/26/72.8/174`, Source `4/6/11.6/32`, Sink `6/6/6/6`. The low-work review list is dominated by source harvesting/spoilage and waste incineration; the only transform under 10 work is plant-rot composting at 8. These rows are review candidates, not audit failures.
- The current material work-factor fallback derives intrinsic value from `UnitPrice`, which is acceptable for handling difficulty but is not an EWU authority. No graph-based embedded-work calculator currently exists in `V23BalanceAudit`; salvage is checked by quantity/profile only. A separate production-graph EWU authority is required before claims about reversible cycles, dismantle profitability, or market arbitrage are evidence-backed.
- Recipe definitions expose deterministic expected output (`amount × probability`) and the authoritative direct work already includes process complexity and material handling. EWU can therefore be propagated from Source recipes through Transform recipes without changing runtime inventory state. Passive processing, clean water, and wastewater are represented on the recipe and can contribute explicit standardized overhead.
- The root audit content source already exposes every `ItemDefinitionSO`, while recipe outputs provide canonical item ID, amount, and probability. No save/runtime API changes are needed for the initial shadow-cost audit.
- Many legitimate acquisition domains do not use `ProductionRecipeSO` (crops, livestock, disease samples, combat loot). EWU propagation must distinguish these external physical leaves from unresolved produced items. External leaves now use a documented kind/tag/weight acquisition-work formula with no market-price input and remain enumerated in the audit.
- Medical transplant facilities 9509/9510 exceeded the 85% dismantle EWU cap because the audit classified all non-arcane/non-industrial facilities as general salvage. Specialized medical facilities belong to the precision/industrial dismantle class; this aligns their reusable components and work loss with their actual construction complexity.
- All thirteen surgery facility abilities implement the authored `ISurgicalFacilityAbility` marker. This is the correct stable domain signal for construction and salvage classification. `BuildingArcaneSurgeryAbility` is the more specific arcane case; all other surgical/medical abilities should resolve to Medical before generic effective-use classification.
- EWU audit now resolves 352 produced/referenced items from 25 explicit external acquisition leaves, with zero unresolved items and zero non-convergent recipes. EWU distribution is min/p50/p90/max `3.6/54.96/263.41/1650.76`.
- Maximum-skill facility dismantle ratios now all remain below the 85% hard cap; the highest observed ratio is 81.0%. Medical facility 9510 falls to 76.7% after authored medical classification.
- Authored gold/EWU spans `0.03` to `3.00` around a median `0.29`, showing that market price is the next major imbalance even though production and salvage invariants pass.
# 2026-08-09 - Balance authority request revalidation

- The requested structure already exists in the active workspace: `docs/game-design/whole-game-balance-baseline.md` is the dedicated numerical/theoretical balance authority, while `docs/DungeonStory_Game_Design_and_Implementation.md` remains the overall design authority.
- Root `AGENT.md` is the intended mandatory gate for future facility, item, recipe, equipment, apparel, research, event, faction, encounter, milestone, and numerical-balance changes.
- `task_plan.md` records Phase 143 (authoritative baseline and agent enforcement) as completed; the current balance application work continues separately in Phase 144.
- Focused verification confirmed that `AGENT.md` contains a mandatory six-step balance workflow and explicit prohibitions against arbitrary index-based costs, fake sinks, formula-only completion claims, and downstream-only patches.
- The main design document links the baseline both in its authority header and in its content-writing rules.
- Only singular `AGENT.md` exists. Codex-compatible automatic discovery convention uses `AGENTS.md`, so a minimal root entrypoint should direct every future agent to the existing full guide without duplicating its contents.

## Phase 144 market formula authority (2026-08-09)

- Automatic excess-stock sale removes the physical items first, then credits `round(delivered × UnitPrice × MarketSaleRate)`, clamped to at least one gold. `ResourceItemDefinitionSO` defaults `MarketSaleRate` to 0.6.
- Auto-procurement does not calculate purchase price from `UnitPrice`; it consumes a `StockDeliveryOffer.cost` authored or generated elsewhere and derives unit price only for rule filtering. Purchase-offer construction is therefore the next authority to locate before rebasing gold values.
- Because sale proceeds clamp each completed delivery to at least one gold, low-EWU bulk goods can still become a rounding exploit unless the audit evaluates transaction batches as well as nominal per-unit ratios.
- Daily procurement offers are category-level rather than item-level: each authored `StockCategoryDefinition` supplies `DailyBaseAmount` and `DailyUnitCost`, daily quantity grows by `min(12, day/3)`, and final cost is `round(amount × DailyUnitCost × run-variable category multiplier)`.
- This means the current purchase side cannot yet be compared to a concrete item's EWU until `TryPurchaseDelivery` resolves the category into a specific physical item. The actual delivered item selection and category definitions must be audited before setting a gold/EWU conversion.
- `UnitPrice` is not only a trade label: it influences customer payment, reforge/overclock cost, fuel/feed choice, medicine selection inputs, theft priority, hauling priority, exterior trade valuation, and apparel's lowest-cost material policy. A global asset rewrite would therefore change AI and service behavior as well as money balance; price correction needs one explicit semantic authority and regression coverage for these consumers.
- Manual and automatic procurement both call `SpawnStockAtDropoff(StockCategory, amount, ...)`; the offer carries no concrete item ID. The category-to-item resolution inside `WorldItemStackRuntime` is therefore the true purchased commodity authority.
- Current authored daily category prices are Food 4, Water 3, General 6, Medicine 12, Weapon 10, Ammunition 4, Fuel 5 and Mana 9 gold per generated unit before run multipliers. Biological, Knowledge and Blueprint have zero daily amount and are not normally offered.
- Purchase currently spawns the physical delivery before charging the money account. It performs a pre-check, but the commit is not an atomic staged transaction. This is primarily a consistency concern; the balance audit should also verify that no failure path leaves spawned stock without payment.
- `WorldItemStackRuntime.SpawnStockAtDropoff` resolves a category by selecting the lexicographically first authored stackable item in that category. Daily market purchases therefore depend on unrelated stable-ID ordering, and the offer UI/cost cannot truthfully identify the purchased commodity.
- The correct authority boundary is an explicit `deliveryItemId` on each `StockCategoryDefinition`, copied into `StockDeliveryOffer`; purchase should spawn that exact item. Categories with no daily offer may leave it empty, while every positive `DailyBaseAmount` must point to an authored stackable item of the matching category.
- The V23 audit currently reports only the distribution of `UnitPrice/EWU`. It needs concrete item rows and procurement/sale transaction ratios before any price normalization can be justified.

### Concrete procurement balance record

- Definition IDs: `stock:food`, `stock:water`, `stock:general`, `stock:medicine`, `stock:weapon`, `stock:ammunition`, `stock:fuel`, `stock:mana` and their daily `StockDeliveryOffer` instances.
- Content type and era: external physical procurement available from early settlement onward; it is a shortage-recovery option, not a replacement for production.
- New player decision: pay a 25–50% EWU premium for a named concrete commodity now, versus spend labor/space/time on self-production.
- Physical input/output: gold transaction plus one explicitly authored stackable `deliveryItemId`; no category-to-item inference or abstract stock copy.
- Direct work/delay: no resident craft work; normal physical dropoff and hauling remain. Daily availability and run-variable multipliers provide calendar/risk pressure.
- Target exchange: purchase price `1.25–1.50 × EWU × goldConversion`; sale proceeds `0.50–0.70 × EWU × goldConversion`; ordinary services target 10–20% net margin.
- Existing alternative: internal gathering/production has lower gold cost but consumes BOM, work, facility capacity, hauling and calendar time.
- Dominance/exploit guard: concrete purchase item must match category; purchase→sale, purchase→craft→sale, purchase→dismantle and per-transaction rounding must never yield positive unbounded gold/EWU.
- Authority: immutable stock-category content definition owns the delivery item; `StockDeliveryOffer` carries the resolved item ID; money ledger and physical item runtime own mutable results; operating-day save DTO preserves an in-flight offer without creating a new authority.
- Failure policy: missing/mismatched/non-stackable delivery item fails catalog validation and offer creation loudly; no lexicographic or generic-item fallback.
- Verification: V23 audit rows for each offer's concrete item, purchase/EWU and sale/EWU; focused manual/auto purchase scenarios; save round trip; Unity Console; later multi-seed gold-flow simulation.
- Current balance state: authority design fixed, implementation and numeric calibration pending.

### Live procurement candidate evidence

- Unity catalog inspection confirms Food, General, Mana, Water, Medicine, Fuel and Ammunition have stackable physical candidates. `Weapon` has none because all actual combat equipment is unique (`MaxStack=1`) and must be created through the equipment authority.
- The currently offered Weapon category is therefore guaranteed to fail when purchased through `SpawnStockAtDropoff`; its daily generic offer must be disabled rather than inventing a stackable weapon token. Unique weapon commerce remains the facility/equipment shop path.
- Current lexicographic defaults demonstrate the severity of the ordering bug: Food resolves to fermented vinegar, General to a blacksteel defense plate, Medicine to resin balm, Fuel to a candle, Ammunition to armor-piercing cartridges, and Mana/Water happen to resolve sensibly only because their candidate sets are small.
- Semantically appropriate baseline delivery commodities are preserved ration, clean water, lumber, standard medicine, iron arrows, charcoal and mana crystal. Their exact EWU must determine daily unit price; the current category prices are not retained merely for compatibility.
### 2026-08-09 밸런스 기준 문서 권위와 자동 적용 경로

- 단일 이론 기준점은 `docs/game-design/whole-game-balance-baseline.md`다.
- 게임 정체성·콘텐츠 범위·시스템 규칙은 종합 설계 문서, 공통 수치·교환율·목표 밴드·검증 절차는 전역 밸런스 기준서, 개별 실행 수치는 ScriptableObject/루트 카탈로그, 통과 증거는 생성 QA 보고서가 각각 소유한다.
- Codex 자동 발견 파일명은 복수형 `AGENTS.md`이므로, 기존 상세 `AGENT.md`만으로 끝내지 않고 루트 `AGENTS.md`를 진입점으로 두었다.
- 새 시설·아이템 등을 추가할 때 반드시 기준 기록을 만들지 않으면 완료로 표시할 수 없도록 문서 경로와 절차가 양쪽 지침에서 검색 가능하다.
### 2026-08-09 구체 조달 품목 전환의 현재 API 경계

- `IWorldItemStackRuntime`는 현재 `StockCategory` 기반 드롭오프 생성만 제공한다. 정확한 물리 아이템 ID를 받는 드롭오프 API가 없으므로 카테고리 조달이 에셋 정렬 순서에 의존한다.
- `WorldItemStackRuntime`의 카테고리 생성 경로를 유지하더라도 외부 구매는 별도의 정확한 `itemId` API를 사용해야 한다. 기존 경로는 디버그/호환 용도로만 남길 수 있고 경제 구매 권위로 사용하면 안 된다.
- `WarehouseFeatureSurfacePresenter`는 이미 `ResolveItemName(itemId)`을 가지고 있으므로 조달 제안에 `itemId`를 보존하면 UI에서 구체 품목을 즉시 표시할 수 있다.
### 2026-08-09 구체 조달 ID 전파 범위

- `StockDeliveryOffer` 생성 지점은 일일 제안, 시설 디버그 시나리오 2곳, 저장 복원 1곳으로 한정된다.
- 외부 구매에서만 exact-item API로 전환해야 한다. 시작 보급, 전리품, 디버그 카테고리 생성 등 다른 `SpawnStockAtDropoff` 호출은 각 도메인의 기존 의미를 유지한다.
- 저장 권위는 `DungeonStockDeliveryOfferSaveData`이며 현재 category/amount/cost/sourceLabel만 보존한다. V26 같은 68개 섹션 내부 계약에서 `itemId`를 추가해 같은 제안이 복원되도록 해야 한다.
- 조달 카탈로그는 11개 카테고리 중 8개에 양수 일일 수량이 있다. 무기는 스택형 실물이 없어 일반 일일 제안을 0으로 내려야 하며, 생물/지식/청사진은 이미 0이다.
### 2026-08-09 exact-item 조달 세부 계약

- `StockDeliveryOffer.IsValid`는 현재 수량/비용만 검사한다. V26에서는 `itemId` 비어 있음도 무효로 해야 하며 일일 제안이 없는 카테고리는 애초에 제안 목록에 포함하지 않는다.
- 구매 실패 조건 디버그 시나리오는 실제 품목 `food:preserved-ration`을 사용하도록 갱신할 수 있다.
- `DungeonStockDeliveryOfferSaveData`에 `itemId`를 추가하고 검증 시 공백을 거부해야 동일 제안이 저장 왕복에서 변하지 않는다.
- ScriptableObject YAML에는 양수 일일 제안 7개만 concrete item을 기록한다. Weapon은 수량 0/빈 itemId, 이미 비활성인 Biological/Knowledge/Blueprint도 빈 itemId를 유지한다.
- UI는 기존 카테고리 표시 대신 `ResolveItemName(offer.itemId)`을 주명칭으로, 카테고리는 보조 정보로 표시한다.
### 2026-08-09 조달 카탈로그·EWU 감사 삽입점

- `IGameContentCatalog.Items`가 저자 아이템 카탈로그를 제공하므로 런타임 `AuthoredGameplayCatalog` 생성 시 delivery item의 존재, 스택 가능성, 카테고리 일치를 검증할 수 있다.
- V23 감사는 이미 ItemDefinitionSO 배열, EWU 스냅샷, GameDomainContentCatalogSO를 같은 실행에서 보유한다. 여기에 조달 정의 검증과 `PROCUREMENT_EWU` 행을 추가하는 것이 단일 감사 권위다.
- 현재 보고서는 전체 UnitPrice/EWU 분포만 기록하므로 실제 일일 구매 단가와 자동 판매 회수율의 차익을 직접 판정하지 못한다.
### 2026-08-09 조달 품목 저자 검증 데이터

- `ItemDefinitionCatalogSO.Definitions`에서 `ItemDefinitionSO.ItemId`, `StockCategory`, `MaxStack`을 직접 검증할 수 있다.
- 조달 품목은 ItemId 존재, 해당 StockCategory 일치, MaxStack > 1을 모두 만족해야 한다. 이는 카테고리 첫 항목 선택을 다시 도입하지 않는 fail-fast 계약이다.
- ItemDefinitionSO의 UnitPrice는 정수이며 여러 시스템의 상대 가치 입력이다. 조달 `dailyUnitCost`는 별도 외부 구매 단가로 유지하되 EWU 감사에서 두 축의 비율을 함께 검사한다.
### 2026-08-09 실제 조달 EWU 측정과 환율 결정

- 새 감사의 구조 검증은 failures=0, Console Error 0 / Warning 0이다.
- 현 조달 gold/EWU는 식량 0.090, 물 0.800, 목재 0.445, 약품 0.148, 탄약 0.198, 숯 0.259, 마나 0.964로 10배 이상 벌어져 있다. 카테고리 가격은 실제 품목 가치와 무관했다.
- 전 아이템 저자가 중앙 gold/EWU 0.29이고 자동 판매율 0.60을 적용하면 중앙 판매는 약 0.174 gold/EWU다. 표준 내부 환율을 `1 EWU = 1/3 gold`로 두면 중앙 판매가 환산 원가의 약 52%가 되어 기준서의 외부 판매 30~50% 할인 밴드에 들어간다.
- 외부 구매는 표준 환율에 35% 프리미엄을 적용해 목표 `0.45 gold/EWU`로 둔다. 허용 밴드는 기준서에 따라 0.417~0.500이다.
- 정수 unit price는 깨끗한 물처럼 EWU가 작은 품목에 큰 양자화 오차를 만든다. 저자 `dailyUnitCost`를 float로 바꾸고 실제 배치 비용만 정수 반올림하는 것이 동일 경제 규칙을 보존한다.
### 2026-08-09 외부 조달 가격 통과 결과

- 최종 조달 gold/EWU는 식량 0.450, 물 0.451, 목재 0.451, 약품 0.450, 탄약 0.450, 숯 0.450, 마나 0.450으로 수렴했다.
- 모든 값이 내부 환율 1/3 gold/EWU 대비 35% 프리미엄이며 허용 25~50% 프리미엄 안이다.
- `Artifacts/QA/v23-balance-audit.txt` failures=0, Unity Console Error 0 / Warning 0이다.
# 2026-08-09 V23 서비스·상점 가격 경로 확인

- `SaleItem`의 정확한 선언 위치는 `Assets/Scripts/Models/Economy/Content/SaleItem.cs`다.
- 상점의 실제 진열 가격은 `ShopInventoryRuntime`에서 `floor(SaleItem.cost × 시설 가격 배율)`로 계산된다.
- 현재 확인된 모듈형 상점 기본 판매 항목은 `tool:field-repair-kit`이며, 작성 가격은 45골드다. 이 값은 물품 EWU와 서비스 마진 기준으로 다시 산출해야 한다.
- 손님 결제액에는 담당 직원의 `RevenueMultiplier`가 추가로 적용된다. 이 배율은 서비스 품질 프리미엄으로 취급하되, 별도 비용·숙련 부담 없이 일반 서비스 순마진 상한을 넘지 않도록 범위와 감사 규칙이 필요하다.
# 2026-08-09 실제 소매 에셋·배율

- 실제 `SaleItem` 에셋은 4개다: 고기 파이(`food:meat-pie`, 200골드), 야전 수리 키트(45골드), 나무 방패(100골드), 장검(150골드).
- 상점에 직원이 없으면 시설 가격 배율은 1.0, 직원이 있으면 무조건 1.2다. 숙련이나 서비스 원가가 아니라 단순 존재 여부만으로 20%가 붙는다.
- 직원의 `RevenueMultiplier`는 `1 + clamp(revenue 모듈 합, 0, 3)`으로 최대 4배까지 가능하다. 물품 가격 1.2배와 중첩하면 4.8배 수입까지 발생할 수 있어 일반/고급 서비스 마진 기준을 크게 벗어난다.
- 현재 소매 가격 45/100/150/200은 보정된 물품 EWU와 연결되지 않은 수기 값이다. 서비스 가격 권위를 `물품 EWU 원가 + 시설 서비스 부담 + 숙련 프리미엄`으로 분리해야 한다.
- 보정된 내부 가치 기준으로 고기 파이는 `UnitPrice=20`, EWU 60.43이고 야전 수리 키트는 `UnitPrice=64`, EWU 192.74다. 기존 소매 수기 가격은 각각 200, 45로 한쪽은 내부 가치의 10배, 다른 쪽은 70% 수준이라 일관성이 없다.
- `ShopInventoryRuntime`은 `SaleItem.cost × 상점 재고 정의 배율`을 입고 단가로 저장하고, 결제 때 다시 직원 유무 배율을 적용한다. 따라서 가격 배율은 두 단계로 명시적으로 감사해야 한다.
# 2026-08-09 소매 가격 권위 설계 결론

- `ShopInventoryRuntime`은 `StockInfo.multifly`를 입고 시점의 1차 배율로, 직원 유무를 결제 시점의 2차 배율로 적용한다. 작업자 스킬 배율까지 합치면 가격 권위가 세 곳에 흩어져 있다.
- EWU 계산기는 장비를 포함한 아이템 정의 전체를 해석할 수 있으므로 `SaleItem.cost`도 같은 스냅샷에서 자동 보정할 수 있다.
- 소매 판매는 자동 외부 매각(원가 가치의 50~70% 회수)과 달리 직원·공간·대기열·절도 위험을 요구한다. 따라서 물품 내부가치 100%에 서비스 마진을 더하는 별도 기준이 맞다.
- 서비스실은 `directPrice + 지원 시설 revenueModifier`를 수입으로 사용한다. 상점뿐 아니라 식당·숙박·목욕·의료 서비스도 EWU 또는 서비스 시간 기준으로 함께 감사해야 한다.
# 2026-08-09 서비스 콘텐츠 가격 표본

- 상점 재고 정의 배율은 일반 1.0, 일부 고급 식당·전장 식당 1.1이다. 이 값은 고급 공간/서비스 부담을 반영하는 프리미엄으로 유지할 수 있다.
- 직접 서비스 가격은 세면·목욕 5, 배식 6, 숙박 8, 응급치료 10, 판매 카운터 4다. 중앙무대 기본 입장료는 12, 매표소 배율은 1.15다.
- 소매점의 직원 존재 고정 1.2배는 작업자 스킬 배율과 역할이 겹친다. 이를 제거하고, 기본 소매 가격 1.20배·재고/시설 프리미엄 최대 1.10배·숙련 프리미엄 최대 1.15배로 분리하면 최대 수입은 내부 가치 대비 약 1.52배이며 고급 서비스 상단에 맞출 수 있다.
- 모듈형 시설 빌더가 야전 수리 키트 가격 45를 다시 덮어쓸 수 있으므로, 보정 에셋만 고쳐서는 충분하지 않다. 빌더도 중앙 `CalculateRetailBasePrice`를 사용해야 한다.
- 서비스 가격 에셋은 소수의 직접 가격과 지원 보정으로 구성되어 있어, 소매 가격 정리 후 같은 감사기에 포함하기 적합하다.
- 소매 재고 에셋은 일반 상점 배율 1.0, 고급 식당 계열 1.1로 기준 범위 안이다. 판매 항목 네 개는 모듈형 판매 카운터에도 함께 연결된다.
- 소매 감사 기준은 일반 마진 `(기본판매가-내부가치)/기본판매가` 10~20%, 시설·숙련 최대 프리미엄 적용 시 20~35%로 구현했다.
- 일반 레시피 EWU 그래프는 장비 출력 아이템 두 개(장검·나무 방패)를 해석하지 않는다. 전투 장비 EWU는 `CombatEquipmentDefinitionSO`의 BOM과 `CalculateEquipment` 작업량을 별도 투영해야 한다.
- 전투 장비 정의에는 완제품 `ItemId`, 기본 재료 ID·수량, 추가 부품 입력, 계산된 제작 작업량이 모두 있다. 이를 사용하면 장비 완제품 EWU를 일반 레시피와 동일한 방향으로 계산할 수 있다.
- 장검은 철 2개 + 성장 골격 1개 + 제작 작업량 72, 나무 방패는 목재 3개 + 제작 작업량 80으로 정의되어 있다. 장비 EWU는 이 물리 입력 EWU와 제작 작업량의 합으로 계산할 수 있다.
- 기본 재료 `material:*`는 `CraftMaterialDefinitionSO.ItemId`를 통해 실제 물리 아이템으로 변환한다.
# 2026-08-09 소매 EWU 감사 통과

- 장비 EWU 투영과 소매 재보정 후 V23 감사가 통과했다.
- 네 소매 항목은 모두 `내부가치 × 1.20` 기본 가격을 사용하고, 재고 정의 최대 1.10·작업자 최대 1.15를 적용해 일반/고급 서비스 마진 구간을 지킨다.
- 가격은 UI 조회와 결제에서 같은 계산 경로를 사용하며 결제 후 이중 배율은 제거됐다.
- 최종 소매 표본: 장검 EWU 1463.70/내부 488/기본 586/일반 16.7%/고급 34.2%, 나무 방패 136.19/45/55/18.2%/35.3%, 고기 파이 60.43/20/24/16.7%/34.1%, 야전 수리 키트 192.74/64/77/16.9%/34.3%다.
- 감사 실패 0, Unity Console Error 0 / Warning 0이다.
- 서비스 프로세스는 식사 6, 판매 4, 숙박 8, 목욕 5, 의료 10을 직접 가격으로 사용하고 관리형은 +2, 자동화형은 +3이다.
- 목욕만 현재 명시적 사용 자원(깨끗한 물 0.45, 오수 0.45)을 갖는다. 다른 서비스는 서비스 시간·시설 점유·지원 설비만 가격 기반이므로 직접 서비스 감사에서는 최소한 노동 시간과 물 비용을 포함해야 한다.
- Dining/Retail 가격은 통합 서비스 세션의 `price=0`으로 두고 기존 식사·상점 물리 결제 경로를 사용한다. 같은 수입을 이중 지급하지 않는 구조다.
- 서비스 시간의 최종 폴백은 각 `BuildingSO.useDuration`이며, 서비스 프로세스 계약은 `Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs`에 있다.
- 직접 서비스 허브의 `useDuration`은 모두 1.5초 수준이어서 이를 그대로 1 EWU/초로 가격 원가로 쓰면 현재 5~10골드는 지나치게 높은 수익이 된다. 숙박은 장기 점유, 식사·의료는 물리 소모품 등 별도 실행 원가를 함께 보지 않고 가격만 낮추면 오히려 시스템 간 불일치가 생긴다.
- 따라서 직접 서비스 가격은 범주별 실제 효과·물품 소비 경로를 먼저 연결해 감사해야 하며, 단순 세션 표시시간만으로 일괄 재가격하지 않는다.
- 지역 공급 계약 런타임은 `Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs`에 있다. 보상은 요구 아이템 `UnitPrice×수량` 합에 1.35배와 대형 프로젝트 배율을 곱하고 최소 25골드를 강제한다.
- V20 정식 세력 계약은 별도 캠페인 계약(정기 20일/위기 7일/전략 45일)이며, 지역 공급 계약과 구분해야 한다.
- 지역 공급 계약은 3일마다 3개 제안을 만들고 기한도 3일이다. 요구량은 원료 20~80, 중간재 10~40, 완제품·식품·약품·탄약 2~12다.
- 현재 1.35배 보상에 대형 프로젝트 1.25배가 중첩되면 내부 가치 대비 1.6875배(순마진 40.7%)가 되어 고급 서비스 상한을 넘는다. 반면 최소 25골드는 저가 완제품 계약을 수 배~수십 배로 과대 보상할 수 있다.
- 기준에 맞는 조합은 기본 계약 1.20배(순마진 16.7%), 프로젝트 최대 1.25배 적용 후 1.50배(순마진 33.3%)이며, 고정 최소 25 대신 최소 1골드만 둔다.
- 지역 공급 계약 보상 공식을 기본 1.20배, 대형 프로젝트 포함 최대 1.50배로 변경했고, 저가 완제품은 계약 후보에서 제외해 정수 반올림 악용을 막았다.
- 지역 공급 계약 EWU 감사가 컴파일·실행 통과했다.
- 생산 회귀 실패는 테스트가 `BeginWork(null, ...)`를 호출하는데, V23 작업자 정책은 실제 작업 시작 시 유효한 작업자 핸들을 요구하기 때문에 발생한다. 런타임 정책을 약화시키면 안 되며 테스트용 작업자를 명시적으로 제공해야 한다.
- 오래된 생산 경제 픽스처에 실제 작업자 주입과 공정 분류를 추가한 뒤 `ProductionEconomyDebugScenarios.RunAll()`이 통과했다. 런타임의 작업자 자격 규칙은 완화하지 않았다.
## 2026-08-09 — verification note

- Unity MCP console access is available through the active `e6cee83c8654` endpoint. Final economy checks can therefore be run and inspected without desktop input automation.
- `Artifacts/QA/v23-balance-audit.txt` writes margins as `ordinary_margin=17.2 %`, so report parsers must allow whitespace before the percent sign.
- Authored faction contracts are not the same system as generated regional supply contracts. `FactionContractDefinitionSO` consumes physical requirements and grants typed faction effects. Completion IDs are stored, so each of the 18 current contracts is one-time per run, despite the design label "정기 계약" for supply contracts.
- Current authored tiers use deadlines Supply 20 days, Crisis 7 days, Strategic 45 days. Sample beastkin requirements are salted meat stew x12, standard medicine x6, and large shield x4; rewards are rapport 8/8/15 plus obligation token 1.
- All authored contract rewards are currently tier-uniform (rapport 8/8/15, obligation 1), while their physical costs differ by required item. Cost validation therefore needs an explicit settlement production-capacity denominator and a separate irreversible-asset rule for `item:lineage-seal` rather than forcing that seal into EWU pricing.
- The baseline defines 99 WU per adult-day and normal total-labor allocations, but it does not yet state the reference population/stage used for authored faction contract percentage denominators. That denominator must be added explicitly before a percentage audit can be authoritative.
- `V23BalanceAudit` already owns facility/recipe/EWU/procurement/retail/regional-contract/dismantle checks. Authored faction-contract burden should be added there and reuse its resolved EWU snapshot, not be implemented as a separate calculator.
- Current authored faction-contract EWU evidence is incomplete because several requirement IDs are not resolved by the production/EWU graph: large shield, maintenance parts, weather chart, field repair kit, engineering blueprint, mechanical parts, fungicide, climate chart and cultured mushroom. These must be traced as ID mismatches, missing recipes, equipment aliases or true external/irreversible items before percentage tuning.
- The resolved golem supply requirement (`component:precision-parts x12`) is a clear absolute-cost outlier at 6,539 EWU/66 WD versus other resolved supply contracts near 3–4.5 WD. Uniform amount 12 is not balanced across different item values.
- Legacy faction/service content still references non-catalog IDs such as `component:maintenance-parts`, `item:field-repair-kit`, and `medicine:fungicide`; current physical catalog equivalents are `tool:maintenance-kit`, `tool:field-repair-kit`, and `supply:fungicide`. Weather/climate/blueprint placeholders require deliberate mapping to current physical tools/documents rather than silent EWU seeding.
- `equipment:large-shield` is not a physical equipment item ID. Current shield items use `equipment-item:shield:*`; a contract should demand a concrete authored shield (likely tower shield for the intended large defensive role).
- Current authored equivalents support a full legacy-ID cleanup: tower shield, maintenance kit, weather observation kit, field repair kit, engineering drawing, machine parts, fungicide, and cave mushroom. Climate chart has no dedicated physical definition; using the weather observation kit preserves the information/field-planning intent without inventing another single-use item.
- Resolved authored-contract cost range is very wide: supply 1.09–66.05 WD, crisis 2.32–11.68 WD, strategic 2.69–27.56 WD, plus demon strategic's four irreversible seals. The fixed quantities 12/6/4 must be replaced with item-value-aware authored amounts and the percentage denominator must be explicit.
- `CreateContract` hardcodes the same amount array for all factions. Amounts must become per-faction authored data (or derived once during asset building from EWU-aware targets); runtime should not dynamically mutate physical requirements.
- Final authored burden ranges now satisfy the baseline: supply 1.4–2.9%, crisis 4.1–6.9%, strategic 5.0–12.0%; demon strategic is separately audited as one irreversible lineage seal. The audit uses one physical definition authority and no runtime scaling.
- Obligation tokens are mechanically meaningful for reinforcement availability, but current chapter trigger assets all have `minimumObligationTokens: 0`. Reward payback cannot be judged from rapport alone; reinforcement benefit/cost and whether using it consumes a token must be traced next.
- Reinforcement routes currently cost no cargo and only check for a positive obligation balance. Because the token is not decremented, one earned token can unlock unlimited reinforcements. This violates both the physical-economy principle and contract payback balance and must be fixed before reward valuation.
- Faction route states remain in the aggregate after arrival; this supports a deterministic per-faction/per-kind cooldown check from saved route history without adding a new save section. Exact cooldowns should be based on cargo/reinforcement value after their contents are audited.
- The reinforcement debit belongs after `domain.AddRoute(route)`: before that point path validation can still fail, while after it the route is a persistent physical commitment. This keeps failure atomicity without a new transaction layer.
- Trade/supply route cargo includes high-value steel, blacksteel, mana crystals, medicine and ammunition. Without repeat limits these routes are infinite physical item sources. Route-history cooldowns are mandatory even if relationship thresholds remain the primary unlock.
- `DungeonFactionDefinitionSO` is a separate Resources catalog (`SO/Factions/Dungeons`) rather than the root domain catalog. Its cargo audit must load those six assets explicitly and validate their physical item IDs against the same EWU snapshot.
- Route cargo EWU by faction shows large value variation: beastkin supply 892, harpy 1,083, myconid 1,885, kobold 2,469, demon 4,209, golem 4,952. A single uniform cooldown would make high-tier factions strictly better economically; cooldown needs to scale with cargo EWU or cargo amounts must be normalized.
- Proposed static pacing authority: reference daily productive capacity is 504.9 EWU (12 × 99 × 42.5%); trade dividends target at most 5% of that capacity per day, supply aid at most 10%. This yields value-scaled cooldowns of roughly 7–27 days for trade and 20–99 days for supply. Reinforcement uses a token and should additionally have a short anti-stacking cooldown.
- Implemented route pacing achieves trade 4.8–5.0% and supply 8.8–10.0% reference daily inflow. Cooldowns are serialized content (7–27 trade, 20–99 supply, 10 reinforcement), while last request time is derived from already-saved route history, so the 68-section save contract remains unchanged.
- Guest requests have a clear cross-item pricing defect: water x12 pays 240 gold while precision parts x4 pays only 80 gold, even though precision parts carry orders of magnitude more embedded labor. Reward must be derived from the concrete consumed items' calibrated internal gold value, with a premium-service margin band, not raw unit count.
# 2026-08-09 V25 SFT 완료 확인

- Colab A100 재개 학습이 `checkpoint-460`에서 시작해 프로세스 반환 코드 0으로 종료됐다.
- 재개 전 체크포인트에는 adapter, optimizer, scheduler, RNG, trainer state가 모두 존재했고 `global_step=460`, `max_steps=594`였다.
- 완료 직후 Colab 리소스는 시스템 RAM 3.4/167.1GB, GPU RAM 0.1/80GB로 반환되어 학습 프로세스가 종료된 상태다.
- 다음 단계는 Drive 최종 산출물·training_evidence 검증과 격리 표본 기반 생성 품질 검사다.
# 2026-08-09 V25 최종 SFT 산출물

- Drive 최종 폴더에 `adapter/adapter_model.safetensors`(139,512,976 bytes)와 `training_evidence.json`이 생성됐다.
- 학습 증거: 38,000 records, dataset SHA-256 `92a495f759c78cc2dd3f6bf73f8ad31a6a9ca93ac51384ad1d9c8a58b2f6c11f`, A100 80GB, SFT training loss `0.007303753625884225`.
- `globalStep=593`으로 기록됐지만 재개 원본 `trainer_state.json`의 `max_steps=594`와 1 step 차이가 있으므로, 품질 검사 전에 정상 epoch 종료인지 별도 판정해야 한다.
- 최종 어댑터와 tokenizer 파일 세트는 모두 존재한다.
# V25 품질 검사 도구 상태

- 기존 `evaluate_release.py`는 생성 결과 JSONL을 점수화하는 결정론적 게이트이며 모델 추론 자체는 수행하지 않는다.
- 격리 평가 후보 2,000건은 존재하지만 SFT 어댑터로 응답을 생성해 평가 입력으로 변환하는 전용 러너는 현재 도구 목록에 없다.
- 따라서 이번 검사는 Colab에서 최종 adapter를 로드해 격리 표본을 생성하고, 로컬/Colab 게이트 입력을 구성하는 단계가 필요하다.
# V25 격리 데이터 구조 확인

- Windows PowerShell 표시에서 보인 한글 깨짐은 출력 디코딩 문제였고, 파일 내부 UTF-8은 `\\uXXXX` 검사로 정상임을 확인했다.
- held-out에는 MultiPerspective를 포함한 10개 프로필이 있으며 각 `chosen` JSON은 프로필별 기계 필드와 서사 필드를 함께 가진다.
- 품질 검사는 생성 JSON의 프로필별 구조, 고정 기계 필드 보존, F/M 참조 범위, 내부 지침 누출, 한국어 텍스트, 이름·문장 중복과 다중 시점 차이를 분리해서 측정해야 한다.
# V25 held-out materialization finding (2026-08-09)

- `Artifacts/Training/V25/held_out_review_candidates_2000.jsonl.gz` in the active Colab clone is a Git LFS pointer (`version ...`) rather than gzip content.
- The completed SFT adapter remains intact; the quality run failed before loading or invoking the model.
- Git LFS materialization succeeded in Colab: the held-out archive is 1,033,968 bytes and starts with gzip magic `1f 8b`.
- The held-out contract uses `profileId` and `cultureStyleId`, not `profile` and `cultureId`. Allowed references are already provided structurally by `factPacket[].ref` and `motifPacket[].ref`, so the evaluator now validates against those arrays instead of scraping prompt text.

# V25 SFT held-out smoke result (2026-08-09)

- Real A100 inference completed for 100 records, 10 per profile, in 346.52 seconds.
- Aggregate automatic metrics: JSON parse 99%, shape 93%, mechanical-field preservation 99%, reference grounding 100%, automatic hard pass 93%, internal prompt/think leak 0.
- Profile failures are concentrated in `BubbleLine` (shape 40%) and one `SocialRumor` parse/mechanical failure (90%); the other eight profiles were 10/10 on the current automatic checks.
- Eight exact/near duplicate groups were detected, but the first-ten selection may contain repeated preference examples from the same `scenarioFamilyId`; deduplicate by scenario family before treating this as model collapse.
- These metrics do not adjudicate semantic invented lore or subtle factual contradiction; human review remains required.
- All eight exact duplicate groups came from identical prompts with the same `scenarioFamilyId`; they are dataset-row duplication, not evidence of cross-context mode collapse. No exact duplicate remained across unique scenario families in this smoke.
- Six BubbleLine failures are genuine profile-contract drift: the model emitted `text` or `response` instead of `line`, sometimes adding Korean-named reference arrays that the profile forbids.
- The SocialRumor failure is a genuine JSON syntax error (`targetFacilityId` rendered as `-1"`). Static per-profile constrained decoding should prevent these structural failures, but the raw SFT model alone does not meet the 100% parse/shape gate.
- Manual review of two unique contexts per profile found severe prose defects despite valid grounding: Korean particle errors (`채무을`, `혈청 검사대은`, `위험 구역의 친구을`), truncated/mechanical names (`유동·누구를먼 공명`), and cross-profile reuse of the same sentence skeletons.
- Common repeated skeletons include “그날의 판단은 … 경력보다 오래 공동체 안에 머물렀다”, “가장 안전한 답을 의심하게 했다”, and “약속을 확인하는 표식이 되었다”. This is a model/data style-collapse issue that constrained JSON decoding cannot repair.
- Quantified document frequency across the 100 outputs: career-linger 22%, promise-marker 18%, personal-end 15%, center-choice 14%, safe-answer 11%, glory/remaining-people 10%.
- Formal verdict: reject this adapter for DPO/release promotion; repair SFT data and profile isolation first. Full 2,000-record inference is intentionally not run because the 100-record early gate already failed.

# V25 SFT remediation root-cause trace (2026-08-09)

- All high-frequency collapsed sentences are authored verbatim in `tools/v25_narrative_training/build_dataset.py` lines 360-380. `prose()` selects exactly one opener, one middle, and one ending from small fixed arrays, so 38,000 records necessarily teach cross-profile sentence skeleton reuse.
- `PROFILE_CYCLES` mixes BubbleLine, SocialRumor, MacroGoal, and MoodImpulse inside the same scenario family and also includes BubbleLine in the correction family. Although each chosen BubbleLine payload is `{"line": ...}`, adjacent training distributions make profile-key confusion plausible for a 1.7B model.
- Dataset validation checks BubbleLine's exact chosen key set, but it does not measure cross-record n-gram document frequency, Korean particle correctness, malformed generated names, or profile-to-profile lexical leakage.
- `narrative_name()` creates its bridge by deleting spaces from the event name and taking the first four code points. This directly produces clipped compounds such as `누구를먼`; names need semantic lexeme selection, not substring slicing.
- `grounded_line()` is reused for CharacterSkill, EvolutionHistory, FacilityEvolution, Persona, MacroGoal, MoodImpulse, and both MultiPerspective voices. The shared builder, not the model alone, is the source of cross-profile voice collapse.
- Existing `attach()` correctly handles simple final-consonant particle choice, but many payload builders concatenate nouns and particles directly. A typed particle API plus a corpus-wide invalid-particle audit is needed; merely fixing three observed strings would leave the generator unsafe.
- The corpus audit measures exact duplicates, generic fallback phrases, vocabulary entropy, and global distinct-2/3, but these metrics can pass while a 5-gram appears in 10-22% of documents. It needs per-profile and cross-profile document-frequency gates.
- A larger defect exists in the SFT authority: paired family rows deliberately reuse the same context and prompt while changing the hidden `variant`, so the prompt does not determine the completion. The trained 38,000 records contain 8,089 identical-prompt groups with different chosen completions, affecting 16,178 records (42.6%).
- Conflict concentration is 7,600 MultiPerspective groups / 15,200 records and 489 CharacterSkill groups / 978 records. The model is being asked to memorize mutually inconsistent outputs for the same input; this must be removed before any prose tuning.
- Training itself uses completion-only loss, no packing, length 2,048, QLoRA and the intended batch/accumulation path. The primary defect is data authority, not an obvious packing or prompt-loss configuration error.
## 2026-08-09 - 손님 요청의 임시 물품 ID

- 손님 요청 빌더의 fail-fast 검증으로 `food:festival-sampler`, `food:luxury-feast`, `item:pathogen-sample`, `item:candle`, `item:reinforced-restraint`가 현재 루트 아이템 카탈로그에 없음을 확인했다.
- 이를 새 추상 아이템이나 임의 EWU로 되살리지 않는다. 이미 생산·획득 경로가 있는 `food:lavish-vegan`, `food:lavish-meat`, `sample:antigen:cave-flu`, `craft:candle`, `tool:reinforced-restraint`로 연결하는 것이 단일 물리 권위 원칙에 맞다.
- 항원 표본은 일반 제작재가 아니라 진단·질병 흐름에서 얻는 물리 표본이므로, 경제 감사에서 제작 EWU가 없다는 이유만으로 합성 제작법을 추가하면 안 된다. 필요하면 비제작 희귀 자산 예외로 분류한다.
- 현재 항원 표본은 내부 가격과 획득형 물리 정의가 있어 손님 요청 비용·보상 검증을 통과했다. 별도 합성 레시피나 예외 EWU 시드는 필요하지 않았다.
- 14개 손님 요청의 EWU는 약 45~2,180으로 넓지만, 기한·시설·서비스 유형이 다르므로 비용 자체를 동일화하지 않는다. 금화 보상은 실제 내부 물품 가치에 일관된 25% 서비스 프리미엄을 적용해 저가 물 요청 과대보상과 정밀 부품 과소보상을 제거했다.

## 2026-08-09 - 기존 금화 감사 범위

- V23 감사에는 이미 제작품 내부가치 대비 gold/EWU 분포, 외부 조달 마크업, 자동 판매 회수율, 소매 서비스 마진, 지역 공급 계약 마진과 판매 불가 희귀 자산 검사가 들어 있다.
- 따라서 새 가격 체계를 중복 도입하지 않고, 기존 감사의 실제 분포와 런타임 구매·판매 경로가 같은 권위를 사용하는지 확인한 뒤 빠진 차익 경로만 보강한다.
- 현재 수치 기준은 내부가치 `1 gold = 3 EWU`, 외부 구매 1.35배, 자동 판매 60% 회수, 일반 소매 1.20배, 서비스/프로젝트 프리미엄 상한을 사용한다.
- 감사 보고서 표본은 외부 구매 0.45 gold/EWU, 자동 판매 0.20 gold/EWU로 정확히 분리되어 단순 매입→재판매만으로는 원금의 약 44.4%만 회수된다. 손님 소매는 일반 순마진 약 16.7~18.2%, 작업자·시설 프리미엄 포함 약 34.1~35.3%다.
- 외부 구매 경로는 `StockInfo.TryPurchaseDelivery`에서 구체 ItemId를 물리 드롭오프에 생성한 다음에만 금화를 차감하는 구조다. 비용 생략은 디버그 규칙 질의를 명시적으로 거치므로 플레이 모드의 숨은 무료 생성 경로와 구분된다.
- 품질 미달 장비·의복의 `MarkForSale` 표식과 일반 생산 자동 판매는 별도 런타임 경로이므로, 다음에는 실제 판매 시 `MarketSaleRate`와 물리 스택 소비가 원자적으로 결합되는지 좁혀서 확인해야 한다.
- 자동 판매는 판매 버퍼의 실제 수량을 먼저 소비하고 성공한 경우에만 `SaleIncome`을 장부에 더하므로 저장 재시도 중복 입금 방지 순서가 맞다.
- 외부 구매는 현재 `SpawnItemAtDropoff` 후 `TrySpend` 순서이며, 결제 실패나 부분 생성 시 생성분 롤백이 없다. `CanSpend` 사전검사와 실제 차감 사이의 상태 변화, 부분 스폰 반환에서 무료 물품 또는 전액 결제 후 부분 납품이 생길 수 있다. 반드시 결제·생성 원자성 보강이 필요하다.
- 물리 아이템 인터페이스는 드롭오프 스폰 결과로 생성 수량만 반환하고 새 StackId 목록은 돌려주지 않는다. 따라서 현 API에서 완전 롤백을 억지로 구현하기보다 `결제 선행 → 실제 생성량에 비례한 미납분 환불`로 정산하면 부분 납품도 무료 물품·과금 손실 없이 처리할 수 있다.
- 비례 정산은 `ceil(총비용 × 실제수량 / 요청수량)`을 실제 비용으로 삼아 최소 단위 반올림을 통한 무료 물품을 방지한다. 생성 0이면 전액 환불한다.
- 기존 시설 디버그 시나리오는 자금 부족과 물리 런타임 누락만 검증하고, 결제 성공 뒤 스폰 실패·부분 스폰을 검증하지 않는다. 환불 정산과 함께 두 실패 시나리오를 추가해야 회귀를 막을 수 있다.
- 금고 장부 enum에는 환불 종류가 없다. `ShopPurchaseRefund`를 별도 값으로 추가하면 입금이 판매 수익으로 오인되지 않고, 구매 지출과 환불을 원인별로 감사할 수 있다. 기존 수치 ID는 유지하고 새 값만 추가한다.
- 시설 집중 시나리오의 기존 월드는 물리 아이템 런타임을 주입하지 않으며, `IWorldItemStackRuntime`은 큰 인터페이스라 작은 실패 스텁을 즉석 추가하면 유지보수 비용이 크다. 우선 실제 스폰 구현의 부분 생성 가능성을 확인하고, 정산 공식은 순수 함수로 분리해 전수 경계값을 검증한다.
- 실제 `WorldItemStackRuntime.SpawnItemAtDropoff`는 내부 Spawn이 만든 수량을 그대로 반환하고 `spawned == amount`일 때만 성공한다. 실패 반환에도 `spawned > 0`일 가능성을 계약상 열어 두므로 부분 납품 정산을 무시할 수 없다.
- 현재 `WorldItemSpawner` 구현은 유효 요청이면 스택을 분할·병합하며 요청량 전부를 생성하므로 정상 런타임에서 부분 생성은 발생하지 않는다. 그러나 인터페이스와 테스트 대역은 부분 수량을 허용하므로 구매 서비스는 여전히 그 경우를 안전하게 정산해야 한다.
- 기존 일일 납품 테스트는 과거 `Weapon` 추상 분류를 기대하고 있어 현재 실제 탄약 조달의 `Ammunition`으로 수정했다. 그럼에도 실패하므로 단순 카테고리 이름 외에 편집기 정적 카탈로그의 초기화 시점 또는 등록 수가 원인인지 런타임 값을 출력해야 한다.
- 연결된 e6/f764 Unity MCP 엔드포인트는 모두 동일한 8,407개 에셋 프로젝트를 보고한다. 도메인 리로드 뒤 어느 연결이 최신 어셈블리를 잡았는지 f764에서 새 진단 API를 호출해 판별한다.
- Unity 설치에는 실행용 `dotnet.exe`만 있고 SDK/MSBuild가 없어 편집기 밖 전체 C# 빌드는 사용할 수 없다. 이번 변경은 Unity의 다음 정상 도메인 리로드에서 최종 컴파일·시나리오 검증해야 한다.
- V23 감사에 구매 부분 정산 경계값 검사를 추가했다. 감사가 새 어셈블리에서 실행되면 결제 40/요청 5의 0·1·2·5개 납품과 1골드 최소 단위가 각각 0·8·16·40 및 최소 1골드로 검증된다.

## 2026-08-09 - 품질 미달품 판매 연결

- `RejectedOutputDisposition.MarkForSale`은 장비와 의복 완제품을 `sale:quality-rejected` 목적지로 생성하지만, 프로젝트 전체에 이 목적지를 읽는 런타임이 없다. 현재 선택지는 금화를 만들지도, 운반 주문을 만들지도 못한 채 출력 버퍼를 막는다.
- 일반 `ResourceStockPolicyRuntime`은 `stock-policy:sell:<itemId>`만 소유하고 플레이어가 해당 ItemId의 초과 판매 정책을 설정했을 때만 동작한다. 품질 파이프라인의 명시적 판매 처리는 별도 소비자 또는 공통 판매 정산 API로 연결해야 한다.
- 장비·의복은 수량 1 고유 아이템이고 품질 상태가 컴포넌트에 있으므로, 정산 시 정의 기본가뿐 아니라 완성 품질 배율을 읽어야 한다. 표식만으로 즉시 금화를 만들지 않고 실제 운반·소비 단계를 유지한다.
- 물리 아이템 카탈로그의 `DungeonItemDefinition`은 고유 장비·의복에도 ItemId, UnitPrice, MaxStack과 EquipmentId를 제공한다. 따라서 일반 자원 카탈로그에 억지 등록하지 않고 물리 카탈로그를 가격 권위로 사용할 수 있다.
2026-08-09: `sale:quality-rejected` 장비를 원시 물리 스택 삭제만으로 판매해서는 안 된다. CombatEquipmentRuntime의 고유 인스턴스 권위까지 함께 제거되는 경로가 확인되기 전에는 구현하지 않는다.
2026-08-09: PowerShell 5의 기본 `Get-Content` 출력에서 UTF-8 BOM 없는 한글 문서가 깨져 보였다. 파일 자체 손상 여부는 UTF-8 명시 읽기와 바이트 검증으로 구분해야 한다.
2026-08-09: 기준서 8장에는 이미 콘텐츠 종류별 필수 비교 항목이 있었다. 에이전트 준수를 더 강하게 만들기 위해 정의/카탈로그/실행기 위치와 자동 감사 ID를 변경 기록의 필수 필드로 추가했다.
2026-08-09: `ResourceStockPolicyRuntime`는 `IWorldItemStackRuntime`, drop zone, workforce, production bills, money와 clock에만 의존하며 일반 스택 판매를 물리 소비 후 장부 수입으로 처리할 수 있다. 반면 고유 전투 장비는 `IItemInstanceRepository.EquipmentInstances`에도 별도 권위가 있어 일반 판매 소비 경로를 그대로 쓰면 안 된다.
2026-08-09: 품질 미달 장비 제작 런타임은 생성 시 `sale:quality-rejected` 목적지를 지정하지만, 현재 검색상 이를 소비하는 실행기는 없다. 같은 런타임 안에는 자동 분해 시 물리 스택 삭제와 `Instances.Remove`를 함께 수행하는 코드가 존재한다.
2026-08-09: 일반 재고 판매는 `stock-policy:sell:<itemId>` 목적지에 운반된 `FacilityBuffer` 스택을 먼저 `TryConsumeFacilityItemBuffer`로 소비한 뒤 `SaleIncome` 장부를 기록한다. 품질 미달품도 같은 consume-before-income 원칙과 물리 운반을 따라야 한다.
2026-08-09: 전투 장비의 `TrySalvage`는 스택 삭제, 로드아웃 제거, EquipmentInstances 제거를 한 권위에서 수행한다. 현재 공개 계약에는 판매용 폐기 명령이 없으므로 이를 명시적으로 추가하거나 중립 포트로 추출해야 한다.
2026-08-09: 의복은 별도 고유 인스턴스 사전 없이 물리 스택 컴포넌트가 상태 권위라 물리 스택 삭제만으로 처분할 수 있다. 전투 장비는 반드시 EquipmentInstances와 sourceStackId를 함께 정리해야 한다.
2026-08-09: `IWorldItemStackRuntime`는 수량 기반 배송뿐 아니라 `TryRequestStackDelivery(ItemStackId, ...)`를 제공하므로 품질 미달 고유 장비를 정확한 StackId로 판매 집결점에 예약할 수 있다. 운반 완료 시 목적지 종류가 FacilityBuffer이면 실제 `TryDepositCarriedItemsToFacility`로 상태가 전환된다.
2026-08-09: CombatEquipmentRuntime과 ResourceStockPolicyRuntime은 같은 DI 컨테이너에서 싱글턴으로 등록되어 기술적으로 연결 가능하다. 다만 일반 재고 정책에 장비 도메인 책임을 직접 섞기보다 별도 품질 미달 판매 실행기 또는 좁은 처분 포트가 적합하다.
2026-08-09: `TryRouteFacilityOutput`는 일반 수량 스택을 제거한 뒤 새 스택을 Spawn하는 구현이며 인스턴스 컴포넌트·itemInstanceId를 전달하지 않는다. 고유 장비/의복 판매에는 사용할 수 없고, 정확한 StackId 배송으로 원본 레코드를 보존해야 한다.
2026-08-09: 현재 `WorldItemWarehouseService.TryRequestStackDelivery`는 FacilityOutputBuffer를 거부하며, 전체 고유 스택을 삭제 후 재생성할 때 `components`만 복사하고 별도 `itemInstanceId`는 넘기지 않는다. 고유 장비 배송 시 sourceStackId 연결이 깨질 수 있는 결함이다.
2026-08-09: 품질 미달 판매의 안전한 경로는 원본 StackId를 유지한 채 FacilityOutputBuffer→Loose outbound 상태로 전환하고, 실제 운반 후 FacilityBuffer에서 스택과 장비 인스턴스를 함께 소비하는 방식이다.
2026-08-09: `DeleteStack`는 고유 장비 스택이면 먼저 repository의 장비 손실 콜백을 호출한다. 판매용 명령이 이를 그대로 사용하면 장비를 '분실'로 기록한 뒤 제거할 수 있으므로, 판매 원인에 맞는 전용 제거 경로가 필요하다.
2026-08-09: 자동 판매율은 ResourceItemDefinitionSO의 MarketSaleRate에만 있고, V22 의복 자원 정의는 판매율 0으로 구성된다. 품질 미달 완제품 판매는 일반 자원 판매와 구분된 완제품 가치 계산이 필요하다.
2026-08-09: `IWorldItemStackRuntime.TryAbsorbUniqueItemStack` removes a unique physical stack without invoking the equipment-lost callback. This is the correct primitive for an explicit market sale, followed by loadout cleanup and EquipmentInstances removal in CombatEquipmentRuntime.
2026-08-09: Craftsmanship 품질의 기존 성능 배율은 Awful 0.70, Poor 0.82, Normal 1.00, Good 1.08, Excellent 1.16, Masterwork 1.26, Legendary 1.40이다. 완제품 판매 가치에도 같은 품질 배율을 재사용하면 새 품질 환산 권위를 만들지 않는다.
2026-08-09: `TryAbsorbUniqueItemStack`는 이미 `IEquipmentPhysicalItemGateway` 공개 계약에 포함되어 있어 새 Items 의존성 없이 CombatEquipmentRuntime의 판매 소비 명령에서 사용할 수 있다.
2026-08-09: WorldItemStackSnapshot은 ItemInstanceId를 노출한다. 판매 실행기는 StackId↔ItemInstanceId↔CombatEquipmentInstance 일치를 검증한 뒤 소비할 수 있다.
2026-08-09: 실제 물리 카탈로그는 ItemDefinitionSO가 최종 원본이며 ResourceDungeonItemCatalogProvider는 이를 DungeonItemDefinition으로 투영한다. 완제품 판매가는 이 카탈로그의 UnitPrice를 기준으로 해야 한다.
2026-08-09: CombatEquipmentAssetBuilder는 장비 제작 작업량을 형태·재료 수량·부품·정밀 단계로 계산하지만 장비 정의 자체에서 물리 UnitPrice를 설정하지 않는다. 별도 ItemDefinitionSO 생성/보정 경로를 확인해야 한다.
2026-08-09: ItemDefinitionSO.UnitPrice가 완제품을 포함한 모든 물리 아이템의 정수 내부 가격 권위이며 DungeonItemDefinition 투영에도 그대로 사용된다.
2026-08-09: 장비 물리 정의의 UnitPrice는 실제 에셋에 존재한다(예: 단검 32, 누비옷 501)하며 maxStack=1이다. 품질 미달 장비 판매는 이 내부가치에 60% 외부 회수율과 기존 품질 배율을 적용할 수 있다.
2026-08-09: 의복 PhysicalItemId는 `apparel:*`가 주류이며 기존 환경 작업복 일부는 `equipment:*`/`tool:*` ID를 재사용한다. 판매 소비자는 접두사가 아니라 ApparelItemState 컴포넌트로 판별해야 한다.
2026-08-09: 일반 판매 수입은 `IGameMoneyAccount.Add`와 `EconomyTransactionKind.SaleIncome`을 사용한다. 품질 미달 완제품도 동일 거래 종류를 쓰되 원인 ID/설명을 별도로 기록한다.
2026-08-09: ApparelItemStateCodec.TryRead는 물리 스택 Components에서 품질·재료·크기 상태를 복원하는 기존 권위다. 판매 품질 판정에 이 코덱을 사용한다.
2026-08-09: 의복 물리 정의는 `Assets/Resources/SO/Economy/Items/V22Apparel`에 ResourceItemDefinitionSO로 존재한다. 품질 미달 판매 실행기는 별도 신규 카탈로그 없이 기존 물리 카탈로그를 사용할 수 있다.
2026-08-09: 대표 의복 튜닉은 실제 물리 정의 `apparel:tunic`, UnitPrice 18, maxStack 1, MarketSaleRate 약 0.611을 가진다. 의복 판매는 ResourceItemDefinitionSO의 보정된 MarketSaleRate를 우선하고 장비는 0.60 기본 회수율을 사용해야 한다.
2026-08-09: IGameMoneyAccount.Add(amount, context)는 성공 거래를 장부에 기록하는 기존 API다.
2026-08-09: ProductionConsumerDemandAdapters 주석도 ResourceStockPolicyRuntime를 시장 운반·정산 권위로 명시한다. 별도 판매 런타임보다 이 런타임에 품질 미달 완제품 경로를 추가하는 것이 기존 책임과 일치한다.
2026-08-09: CombatEquipmentRuntime.TrySalvage는 DeleteStack을 사용해 일시적으로 Lost 상태를 거치지만 즉시 인스턴스를 제거한다. 새 판매 명령은 더 정확하게 TryAbsorbUniqueItemStack을 사용해 분실 의미를 발생시키지 않는다.
2026-08-09: ResourceStockPolicyRuntime.IsOutboundDestination는 판매뿐 아니라 regional-contract/grand-project 접두사도 제외한다. 품질 미달 판매 목적지는 이 기존 조건에 추가해야 한다.
2026-08-09: Unity Editor 감사 코드는 런타임 어셈블리의 internal 정적 메서드에 접근할 수 없었다. 결정론적 밸런스 계산기는 감사에서 직접 검증할 수 있도록 public 순수 함수로 노출해야 한다.
2026-08-09: V23BalanceAudit already validates external market recovery bands and purchase settlement boundaries. 품질 완제품 판매에는 품질 배율을 적용해도 최고 조건 `0.70×1.40=0.98`로 내부 UnitPrice를 넘지 않는 별도 단조성/상한 검증이 필요하다.
2026-08-09: PhysicalItemDebugScenarios는 실제 WorldItemStackRuntime·repository·CombatEquipmentRuntime을 조립하는 CreateRuntime fixture를 제공한다. 신규 고유 출력 배송 테스트에 이를 재사용할 수 있다.
2026-08-09: 기존 장비 물리 권위 시나리오는 CreateInstance→SpawnExistingUniqueItemAt→TryLinkToWorldStack 패턴을 제공한다. 신규 배송 테스트에서 같은 패턴으로 StackId, ItemInstanceId, Components와 Combat sourceStackId 보존을 검증할 수 있다.
2026-08-09: `TryConsumeForMarketSale` 공개 명령은 호출자가 올바르다고 가정하면 우회 판매가 가능하다. 명령 내부에서 FacilityBuffer, 품질 미달 판매 목적지, 수량 1, ItemInstanceId 일치를 다시 검증해야 한다.
2026-08-09: 신규 `quality_rejected_unique_delivery_identity` 시나리오는 PASS했다. StackId, ItemInstanceId, StackSignature, 목적지와 Combat sourceStackId가 보존되고 도착 전 판매 명령이 원자적으로 거부됐다.
2026-08-09: 전체 물리 계약 실패 3건은 신규 시나리오가 아닌 기존 facility delivery/craft material/module progression 시나리오다. 첫 실패는 단일 스택을 가정한 테스트, 나머지는 별도 기존 상태 권위 문제일 가능성이 있어 분리 검증이 필요하다.
# 2026-08-09 품질 미달 고유품 회수 조사

- `WorldItemStackRuntime.TryRouteStackToDestination`은 기존 레코드의 상태·목적지·위치만 갱신하므로 StackId, ItemInstanceId와 인스턴스 컴포넌트를 보존한다.
- 시장 도착 재현에는 이 API로 `FacilityBuffer / sale:quality-rejected` 상태를 만들 수 있다.
- `TryAbsorbUniqueItemStack`은 장비 분실 콜백 없이 물리 스택만 제거하므로 판매 시 전투 장비 Aggregate와 물리 아이템 Aggregate를 함께 정리하는 용도에 맞다.
- 기존 `facility_delivery_buffer` 시나리오는 목적지로 예약된 저장 스택이 정확히 하나라고 가정해 `SingleOrDefault`를 사용한다. 물리 전달이 여러 스택을 합산할 수 있는 계약이라면 총량·공통 목적지를 검증하도록 바꿔야 한다.
- `physical_craft_material_gate`는 주문 예약 직후 `HasPendingCraftWork`가 참이 되어 실패한다. 재료가 창고에 예약된 상태와 시설 버퍼에 실제 도착한 상태가 혼동되는지 제작 런타임을 확인해야 한다.
- 모듈 시나리오는 잘못된 시설 검사 직후 올바른 감정이 `EquipmentModuleMissing`으로 실패한다. 실패 경로가 모듈 또는 물리 스택을 변경하는지 조사해야 한다.
- `CombatEquipmentCraftingRuntime.HasPendingWork`가 현재 재료 도착 여부를 전혀 검사하지 않고 정의 ID만 맞으면 참을 반환한다. 이는 시나리오 문제가 아니라 실제 작업 AI가 미도착 주문을 가져갈 수 있는 런타임 결함이다.
- `EquipmentModuleRuntime.TryAppraise`는 올바른 연구와 시설 검증 후 `IsModuleInLocalBuffer`를 먼저 검사한다. 잘못된 시설 실패 경로 자체는 모듈을 변경하지 않으므로 로컬 버퍼 판정/생성 시점 연결을 더 좁혀야 한다.
- 모듈 로컬 버퍼 판정은 스택이 `Forbidden`이 아니고 `IsReserved`도 아니어야 한다. 생성 직후 스택이 자동 예약되는지가 감정 실패의 유력 원인이다.
- 제작 재료의 실제 소비는 `EnsureMaterialsReady`에서 시설 버퍼를 원자적으로 소비하며 수행된다. `HasPendingWork`는 소비 없이 동일 재료가 전부 도착했는지만 조회해야 한다.
- `SpawnExistingUniqueItemAt`은 새 고유 스택을 예약하지 않으며, 모듈 컴포넌트는 생성 직후 `PersistModulePhysicalState`가 붙인다. 따라서 모듈 실패는 예약 자체보다 시나리오 간 정적 런타임 오염 가능성도 확인해야 한다.
- 품질 미달 판매 시나리오가 모듈 시나리오 바로 앞에 추가되었으므로, `CreateRuntime`/`Dispose`의 싱글턴 정리가 불완전하면 새 시나리오가 다음 테스트에 영향을 줄 수 있다.
- 물리 시나리오의 `CreateRuntime`은 매번 새 저장소와 프록시를 만들고 프록시에 해당 스택 런타임을 직접 연결한다. 전역 싱글턴 경유가 아니므로 새 판매 시나리오의 직접적인 런타임 공유 가능성은 낮다.
- `WorldItemStackRuntime.Dispose`는 빈 구현이지만 테스트 런타임은 각 시나리오의 지역 프록시로 주입된다.
- 모듈 생성은 목적지 ID를 Trim만 하고 그대로 물리 스택에 기록한다. 감정 시설의 목적지와 생성 목적지가 달라지는 정규화 문제는 아니다.
- `TryAppraise`는 모듈 로컬 버퍼 확인 뒤 `component:material-test-coupon`, 검사 게이지, 룬 식별 렌즈를 모두 요구하며, 이 중 하나라도 없으면 같은 `EquipmentModuleMissing` 코드를 반환한다. 기존 시나리오는 세 물품을 전혀 공급하지 않았다.
- 물리 계약 전체 재실행에서 시설 전달 복수 스택, 제작 재료 도착 게이트, 품질 미달 고유 장비 시장 판매, 모듈 감정 물자 소비를 포함해 실패 0건을 확인했다.
- 현재 V23 감사 수치: 플레이어 시설 342, 조합식 354, 장비 61, 의복 56, EWU 해석 아이템 413, 미해결 0, 비수렴 0, 감사 실패 0.
- 금화 경제 마진은 기준 밴드에 들어오지만 감사 보고서의 `LOW_BOM` 의료 시설은 다음 시설·유지보수 단계에서 개별 개연성 검토 대상으로 남긴다.
- 다음 균형 단계의 기존 권위 검증 진입점은 `BatchBCharacterSurvivalAuthorityDebugScenarios.RunAll`, `BatchCProductionInfrastructureAuthorityDebugScenarios.RunAll`, `NeedBalanceCalibrationScenario.RunCalibration`이다.
- 필요 균형 캘리브레이션은 3/10/50명, 9종족, 3난도, 공급 조건과 시간 배율 결정론을 다룬다.
- 생존 필요 캘리브레이션 통과: 표준·충분 공급에서 생활 비율 평균 23.24%, 작업 비율 평균 74.68%, 대기열 2.08%, 결핍 피해 0, 붕괴 0, 종족 결과 편차 13.28%다.
- Batch C 실패 대부분은 V21/V20 시대의 수량·버전·키를 고정한 검증기와 현재 V23 계약의 불일치다. 다만 산업 프로젝트 `31/46`과 환경 저장 preflight 실패는 실제 누락인지 검증기 노후화인지 별도로 판정해야 한다.
- 산업 연구 수량 실패는 총 연구 180개는 맞지만 V21 통합 이후 `IndustryAndAutomation` 필드가 31개인데 검증기가 통합 전 46개를 기대하는 형태다. 연구 총수·보상 연결 감사와 함께 현재 필드 분포를 새 권위로 기록해야 한다.
- 환경 작업복 검증은 V22/V23에서 기존 작업복 4종을 통합했는데 3종을 고정 기대해 노후화되었다.
- `IndustrialInfrastructureStressProbe`는 10,000 셀의 전력·상수·하수 토폴로지와 2,000개 컨베이어 경로를 각각 10초/5초 이내로 검증한다.
- 스트레스 픽스처는 각 행 마지막 노드를 `ConveyorPortMode.Output`으로 만들고 앞 노드에서 그 셀로 연결하려 한다. 토폴로지의 `CanReceive`가 Output 포트를 수신 불가로 처리한다면 픽스처 또는 포트 의미가 서로 반대로 작성된 것이다.
- 경로 플래너의 목적지는 `Input` 포트를 제외하고 찾으므로 현재 이름상 Output 포트를 최종 소비 목적지로 취급한다. 그래프 수신 규칙도 이 의미와 일치해야 한다.
- 토폴로지 `CanReceive`는 포트 모드와 무관하게 모든 ConveyorPort를 수신 가능하게 하므로 출력 포트 차단이 원인은 아니다.
- 최소 2x1/2개 요청에서도 실패하므로 성공 경로 수와 경로 길이를 진단 메시지에 포함해 경로 탐색 실패와 길이 불일치를 분리해야 한다.
- 진단 결과 최소 그래프도 `NoRoute`로 실패했다. `outputDirections` 기본/명시 값은 `Vector2Int.right`이며 Output 포트는 목적지 ID를 가진다. 다음은 실제 생성된 첫 노드 outgoing과 마지막 노드 incoming 수를 기록한다.
- 최소 2x1 그래프는 `firstOutgoing=1`, `lastIncoming=1`로 방향 간선 자체는 정상이다. 실패는 목적지 포트 판정에 있다.
- 실제 원인은 스트레스 픽스처가 `ItemStackId("stress-stack")`을 사용해 `stack:` 영속 ID 계약을 위반한 것이었다. 경로 플래너는 잘못된 스택을 시작 단계에서 합법적으로 거부했다.
- 픽스처를 `stack:stress`로 수정한 뒤 10,000 유틸리티 셀/2,000 경로가 통과했다: 토폴로지 655.2ms, 경로 905.9ms, 측정 스레드 할당 0B.
- 연구 집중 검증은 `ResearchTreeDebugScenarios.RunAll`과 `ResearchEquipmentOverhaulDebugScenarios`에 있으며, 시간 고정 선행 폐쇄 90개/95,448 WU/964.12일을 직접 검증한다.
### 2026-08-09 — infection validation discovery

- Infection authority is concentrated in `Services/Character/DiseaseFieldResponseRuntime.cs`, `PopulationHealthRuntime.cs`, and `PhysicalVaccinationRuntime.cs`.
- There is no dedicated disease-named debug scenario/probe in the current source tree. The only bounded scenario reference is inside `V19SaveAtomicityDebugScenarios`, so disease balance will require inspecting the focused runtimes and either invoking their existing contracts indirectly or adding a dedicated deterministic calibration probe.
- The disease system exposes suitable deterministic seams (`RecordExposure`, `AdvanceToDay`, `Vaccinate`, epidemic snapshots, immunity, symptom/work/move multipliers), while physical field response and vaccination are separate item-consuming commands. A proper balance probe can therefore test epidemiology separately from physical atomicity, then compose both in a vertical contract.
- `PopulationHealthRuntime` enforces exactly 16 authored disease definitions and exposes deterministic symptom multipliers with a 0.2 floor. Work burden is strongest for consciousness/breathing targets (0.55/0.50 at full severity); move burden is strongest for breathing/core (0.48/0.40). This provides a measurable productivity-loss axis for the disease calibration.
- The actual epidemiology rules live in `Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs`; focused calibration should target this aggregate directly to avoid Unity/application adapter noise.
- Epidemiology contracts are explicit and currently match the design document: infection probability is capped at 80%; vaccination starts at 70 immunity with 0.05/day decay; recovery starts at 80 with 0.02/day decay; 3 diagnoses within 10 days declare an epidemic; 14 days without a new diagnosis ends it. These constants now need outcome-band calibration, not just structural verification.
- Existing `V19SaveAtomicityDebugScenarios` only references the population-health save section and does not exercise disease outcomes, confirming the missing balance probe.
- A disease balance probe can be self-contained without scene/bootstrap dependencies: `DiseaseDefinition` is a value type with a direct constructor and `PopulationHealthAggregateState` accepts an `IDiseaseDefinitionCatalog`. This follows the existing deterministic editor-scenario style used by the 200,000-character life probe.
- Existing authored core diseases span base infection 0.08–0.25, incubation 1–7 days, contagious duration 5–20 days, and severity 25–70. The calibration should use representative low/medium/high exposure profiles rather than a single synthetic pathogen.
### 2026-08-09 — population-health balance calibration

- New deterministic gate passed against all authored assets: 16 disease definitions, 15 contagious diseases, 100,000 samples per contagious disease and exposure profile.
- Full-day untreated infection probabilities span 7–25%; the 8-hour/0.50-environment mitigation profile reduces risk to one sixth; initial vaccination cuts risk to 30%, and day-30 immunity remains 68.5.
- The domain implementation correctly declares an epidemic at 3 diagnoses within 10 days, preserves that state through save restore, keeps it active through day 13 without cases, and closes it on day 14.
- Evidence: `Artifacts/QA/population-health-balance.txt`.
### 2026-08-09 — logistics/industrial validation discovery

- `IndustrialInfrastructureDebugScenarios` mixes useful current invariants with stale content-count assertions (`industryCount == 46`). Its directly useful focused checks cover sanitation/process fluids, work registration, conveyor state evaluation, utility-layer coexistence, automation power demand, save round-trip, item definitions, and the industry UI tab.
- The 100×100/2,000-route stress probe already passed after correcting its invalid fixture ID; utilization and labor-share balance remain unmeasured even though topology and route performance are now evidenced.
- The focused industrial run found a real current integration failure, not just a stale count: `장기 재생 수술실` advertises surgery work but has no process-fluid settings. This violates the rule that surgical consumers must physically connect to clean water and wastewater and requires a content fix before the industrial current-contract gate can pass.
- Exact asset inspection confirms building 8868 has surgery, production buffer, economy, room, and work-amount abilities, but no utility connection or process-fluid ability. Its construction BOM/work is already V23-authored (136 WU plus stone/steel/precision/rune/mana inputs), so the fix should add domain abilities without replacing its balance data.
- The missing medical utilities are already handled by `IndustrialInfrastructureAssetBuilder.PatchProcessFluidConsumers`: any facility supporting surgery gets clean-water/wastewater channels, 0.2 units of each per surgery cycle, manual-water fallback, and plumbing work. The defect is builder ordering/coverage: research-overhaul facilities created after that patch do not receive the existing rule.
- `ResearchOverhaulContentAssetBuilder.EnsureAssets()` currently builds facilities and recipes, then reindexes/normalizes, but does not rerun the industrial cross-domain patch. Future regeneration will therefore recreate the defect unless the patch is invoked from this authoritative build path.
### 2026-08-09 — research medical utility integration fix

- Fixed the authoritative research-overhaul facility builder so all five age-treatment surgery facilities receive clean-water and wastewater connections, plumbing work, manual-water fallback, and 0.2 clean-water/0.2 wastewater consumption per surgery cycle.
- Regenerated the research-overhaul assets and reran the focused industrial gate successfully: sanitation, work registration, co-located utilities, conveyor states, automation power, industrial save round-trip, item definitions, and industry UI all pass.
- Research-overhaul regeneration currently resets many V23-calibrated unit prices and rewards. The resulting audit failures include guest-request margins, market EWU recovery, and retail margins across many items. This demonstrates that builder order is itself a balance authority problem: cross-domain regeneration must finish with the V23 balance authoring pass.
- The post-generation economic pass is `V23MarketValueCalibrator`; `V23CraftingDebugScenarios` also runs the audit. The next recovery step is to inspect the calibrator's public entry point and ordering rather than hand-editing hundreds of generated prices.
- `V23MarketValueCalibrator.Apply()` recalculates recipe work, embedded work values, item unit prices, automatic sale rates, retail offers, and stock purchase costs. It does not update guest-request rewards, so an additional guest calibration path is required after item prices change.
- Guest rewards are authored as `V20ContentEffectKind.Money` effects and the audit expects `GoldEconomyBalanceRules.CalculatePremiumServiceReward(sum(consumed item UnitPrice × amount))`. There is no existing calibrator entry point, so `V23MarketValueCalibrator` should update those money effects after recalculating item prices.
- `V20ContentEffect.amount` is mutable float data, while the target reward is an integer. The calibrator can safely replace the existing Money effect's amount with the integer premium target and mark only the affected guest asset dirty.
### 2026-08-09 — regeneration-stable V23 economy

- Extended `V23MarketValueCalibrator` to recalculate all 14 guest-request Money rewards after item prices, using consumed physical requirements and the 25% premium-service margin rule.
- `ResearchOverhaulContentAssetBuilder.EnsureAssets()` now finishes with the V23 market calibration pass, preventing regenerated facilities/items/recipes from leaving stale prices and rewards.
- Recalibration completed and `V23BalanceAudit.Generate()` returned successfully again after the research-overhaul regeneration.
- Post-fix audit evidence ends with `failures=0`; the five affected age-treatment facilities now show a 76.3% skill-100 dismantle EWU recovery, safely below the 85% anti-reroll ceiling. Unity Console is Error 0 / Warning 0.
- Wildlife save version 4 is intentional and adds `lastDiseaseVectorAbsoluteDay`, linking wildlife disease-vector timing to deterministic save state. The player-fairness fixture is stale at V3 and should be updated to assert and round-trip the V4 field.
### 2026-08-09 — player-fairness save fixture

- Updated the fairness fixture from wildlife save V3 to the intentional V4 contract and added a round-trip assertion for `lastDiseaseVectorAbsoluteDay`.
- `PlayerFairnessDebugScenarios.RunAll(false)` now passes, including time-scale conservation, pause behavior, raid-food eligibility, external/wildlife/environment/surgery save state, surgery environment safety, intel-site availability, and visible contract forecast costs.
- The strict CharacterEnvironment save contract already has a focused validator inside `EnvironmentalFieldDebugScenarios` and prior architecture work records it passing. The earlier Batch C summary likely conflated other stale failures; rerun the focused environmental suite before changing save code.
- Current focused environmental failure is now isolated: one stale workwear count (3 vs the intentional 4) and one strict empty-payload preflight/publish failure. No other environmental checks were reported failing.
- The strict empty-payload fixture is incomplete for save V5: it initializes only `exposures` and `equippedWorkwear`, while V5 also requires non-null `equippedApparel` and `apparelWorkOrders`. This is a stale fixture omission, not evidence of a runtime restore bug.
- The V5 strict empty character-environment payload now passes far enough that the only reported failure is the legacy workwear item lookup for `equipment:cold-work-suit`; the save defect was a fixture omission.
- `equipment:cold-work-suit` resolves correctly, but its authored `maxStack` is 40. The focused test is correctly identifying a physical-authority defect: an equipable workwear/apparel instance with persistent identity cannot remain a bulk stack.
- `ResourceEconomyAssetBuilder` still authors legacy workwear max stacks as 50/40/25, while `V22ApparelContentAssetBuilder` maps those same item IDs into per-instance apparel definitions but does not currently normalize existing item stack size. The authoritative V22 builder must convert all mapped apparel item definitions to max stack 1.
- `BuildApparel()` skips any pre-existing physical item outright. This is the precise bug: legacy workwear keeps bulk max stacks. The fix should reconfigure existing mapped items with their current metadata but `maxStack = 1`, then continue.
- `ResourceItemDefinitionSO.Configure` exposes all metadata needed to preserve an existing item while changing stack size (`DisplayName`, `Description`, `StockCategory`, `Kind`, `IngredientTags`, `UnitPrice`, `UnitWeight`, `RequiredResearchId`). Normalizing an existing apparel item through this API is regeneration-safe.
### 2026-08-09 — unique physical apparel and environment contracts

- Updated the legacy resource builder and V22 apparel builder so all mapped apparel/workwear items are max-stack 1, including existing item definitions. V22 regeneration also reapplies V23 market calibration.
- Regenerated apparel/textile content; `EnvironmentalFieldDebugScenarios.RunAll()` now passes, including four physical workwear definitions and strict V5 character-environment save staging/commit.
- The subsequent V23 balance audit also passed, so unique apparel stacks did not break EWU or market balance.
### 2026-08-09 — maintenance/automation calibration seam

- Automation content exposes auditable authored inputs: assisted/automatic power demand, assisted work multiplier, automatic work rate, automatic quality cap, and maintenance per game hour. Power producers/consumers/storage likewise expose production, demand, minimum supply, capacity, transfer, and efficiency.
- A content-level infrastructure balance gate can now enforce positive maintenance/power costs and sensible mode progression; the 5–10% labor-share target still requires a representative PlayMode colony workload and belongs to the later end-to-end simulation gate.
### 2026-08-09 — infrastructure balance calibration

- New infrastructure content gate passed: 419 unique-ID building assets inspected, 28 automation facilities, 3 power producers, 57 power consumers, and 1 storage facility.
- All automation facilities have 1.35× assisted work, positive power cost, maintenance 1/hour, automatic power not below assisted power, positive automatic work, and a 0.50–0.90 quality cap.
- The combined stress gate passed 10,000 utility cells and 2,000 payload routes in 732.6 ms topology + 923.8 ms routing with measured allocation 0 bytes for both sections. Evidence: `Artifacts/QA/infrastructure-balance.txt`.
- These are content and algorithm baselines. Actual 5–10% maintenance/power labor share remains an end-to-end colony workload measurement, not something this asset audit can prove.
## 2026-08-09 전투·원정 밸런스 진입점

- `CombatSystemDebugScenarios.RunAll(bool)`은 전투 규칙·장비·의료 저장 계약을 검증하는 집중 진입점이다.
- `OffenseBattleDebugScenarios.RunAll(bool)`은 턴 전투 명령과 승패 상태를 검증하지만, 검색 결과만으로는 다중 시드 승률·소모량 분포 검증이 확인되지 않았다.
- `OffenseStrategicDebugScenarios.RunAll()`은 원정 거점·사건·골드 원자성 등 전략 계층 계약을 검증한다.
- `V20CampaignDebugScenarios.Run()`은 9개 이정표, 6×6 세력 장, 120일 자급, 보상·압력·랜드마크·저장을 검증하지만 도달 시점 분포를 측정하지 않는다.
- Combat/Offense/Run Editor 범위에는 명시적인 Monte Carlo 승률·백분위·1,000시드 밸런스 프로브가 없다.
- 전투·원정·캠페인 검증 클래스는 전역 정적 클래스로 노출되어 Unity 동적 명령에서 직접 호출할 수 있다.
- `OffenseBattleDebugScenarios`의 저장·시설 제작 픽스처 두 곳이 아직 V14 공통값 `requiredWork=6`을 직접 사용한다. V23에서 장비별 실제 작업량이 권위가 되었으므로 이 실패는 구형 픽스처일 가능성이 높다.
- 탄약 소비 권위 검증은 `ResourceItemKind.Ammunition`을 정확히 11개로 고정하고 활 3종·석궁 3종·화기 3종의 구형 탄약 목록을 완전 일치 비교한다. V21 이후 추가 특수 탄약과 역할형 무기가 있으므로 실제 카탈로그와 대조가 필요하다.
- 장비 정의는 `RequiredCraftWork`를 이미 권위로 노출하며, 저장 복원도 이 값을 기본 작업량으로 사용한다. 구형 픽스처는 단검 정의의 실제 작업량을 사용하도록 고치는 방향이 맞다.
- 전투 장비 정의는 서비스 폴더가 아니라 `Assets/Scripts/Models/Economy/Content` 아래에 있다.
- 두 구형 Offense 픽스처는 `requiredWork=6`뿐 아니라 시설의 `workUnitsPerCycle=6` 및 남은 작업 4를 함께 고정한다. 단검의 `RequiredCraftWork`를 한 번 읽어 픽스처 전체에서 사용해야 저장·작업 완료 의미가 유지된다.
- V23 비탄약 장비 제작 저장은 작업량 외에도 `qualityRoll != null` 및 `qualityRoll.attemptIndex == qualityAttemptIndex`를 필수로 검증한다. 저장 DTO의 다른 반복 제한 필드는 정상 기본값을 가진다. 현재 실패 원인은 작업량이 아니라 구형 픽스처가 품질 롤을 생략한 것이다.
- 진행 중이며 재료가 준비된 장비 제작 픽스처의 품질 단계는 `QualityTargetPipelineStage.Working`으로 표현할 수 있다. 품질 롤은 attempt 0과 고정 난수 3개를 명시하면 저장 재굴림 방지 계약도 함께 검증한다.
- 품질 롤 패치 후 Unity Console에는 컴파일 Error/Warning이 0건이다. 직전 동적 DLL 로드 실패는 프로젝트 코드 컴파일 오류가 아닌 Assistant 동적 명령 로드 경합으로 판단된다.
- 현재 전투 규칙/턴 전투 계약은 수정 후 통과한다.
- 원정 전략 11개 시나리오와 V20 캠페인·엔드리스 규칙 계약도 통과한다. 이는 기능·결정론·저장·물리 집결 증거이며 승률/도달일 분포 증거는 아니다.
- `OffenseBattleSession`은 전투원 목록, 라운드, 결과와 명령 실행 API를 공개하므로 실제 결정론적 자동 전투 프로브를 런타임 위에 만들 수 있다.
- 세션은 적 AI 명령을 생성하지만 아군 자동 명령 생성기는 공개하지 않는다. 밸런스 프로브는 합법 타깃·진형·능력을 이용한 결정론적 아군 정책을 별도로 작성해야 한다.
- `OffenseEncounterSO` 36개는 강도 범위, elite/boss, 6종 목표, 라운드 제한, 전장 변형, 카운터, 물리 보상과 적 아키타입 구성 범위를 직접 보유한다. 밸런스 분류와 보상 감사를 이 에셋에서 파생할 수 있다.
- `EnemyEncounterFactory`는 실제 적 개인·난이도 배율·거점 위험·전략 압력·능력·전장 변형을 전투원과 규칙으로 투영한다. 따라서 이 팩토리를 사용해야 authored 적 전투 밸런스를 측정할 수 있다.
- 전멸 외 목표는 보호/파괴 장치와 지휘관 생포 대상까지 런타임 전투원으로 생성되며, 조우 보상과 카운터 태그도 전투 규칙에 전달된다.
- 기존 Offense Editor 테스트에는 실제 `EnemyEncounterFactory` 구성 헬퍼가 없고, 실제 구성은 VContainer 등록 경로에 있다. 새 프로브가 카탈로그·개인 생성기 의존성을 직접 조립해야 한다.
- `EnemyCombatContentCatalog`은 적 36/조우 36/능력 18/전장 변형 12와 장비 참조를 전수 검증한다. `EnemyIndividualFactory`의 서사·생애 의존성은 무겁지만, 프로브용 최소 `IEnemyIndividualFactory`로 적 스탯 투영만 재사용할 수 있다.
- 기본 공격은 실제 `CombatResolutionService.Preview/Resolve`, 장비 스냅샷, 사거리·엄폐·방어구·방패를 사용한다. 장비 없는 프로브는 전부 비무장으로 축소되므로 밸런스 증거로 부적합하다.
- 무기는 `CombatWeaponSO.CreateSnapshot`으로 실제 형식·사거리·탄약·품질·재료·진화 배율을 투영할 수 있다. 방어구와 방패 투영은 현재 `CombatEquipmentLoadoutRuntime` 내부 구현을 재사용하거나 같은 규칙을 별도 헬퍼로 추출해야 한다.
- `EnemyIndividualFactory.EnsurePhysicalEquipment`는 실제 아키타입 무기·탄약·방어구·방패를 `ICombatEquipmentRuntime`에 생성·배정한다. 프로브용 최소 개인 팩토리가 이 부분만 동일하게 수행하면 전체 서사/생애 Aggregate 없이 실제 장비 전투를 구성할 수 있다.
- 공개 `CombatEquipmentEditorTestFactory.Create`로 프로브 전용 장비 런타임을 구성할 수 있다.
- 적 아키타입은 체력·공격·힘·강인함·민첩·속도, 실제 장비, 1~3 능력, 역할·전술 가중치·카운터·보상과 개인 편차를 모두 제공한다. 콘텐츠 예산 프로브는 이 필드들을 직접 사용할 수 있다.
- V23 경제 감사가 공개 `V23EmbeddedWorkValueCalculator`와 `EmbeddedWorkValueSnapshot`을 사용하므로 조우 보상도 같은 EWU 권위로 평가할 수 있다.
- 적 장비 방어는 부위별 베기/관통/둔기 값, 능력은 피해·지속피해·치유·지연·취약·제압·연막·소환·해제·방어의 typed 효과와 쿨다운으로 구성된다. 위협 예산은 단순 공격력뿐 아니라 이 값을 포함해야 한다.
- 최초 예산 보고서의 116개 실패 중 115개는 실제 존재하는 `offense:unappraised-loot`를 프로브가 일반 ItemDefinition 목록에서 누락한 수집 오류다.
- 남은 1개는 encounter ID 6개 묶음을 캠페인 진행 단계로 오인한 잘못된 가정이다. 1~6은 재작성 조우, 이후 번호는 역할형 조우이므로 난도 진행은 ID가 아니라 authored `minimumSiteStrength/maximumSiteStrength`, elite/boss 플래그로 판단해야 한다.
- 보상 전리품은 감정 전에는 판매 불가이고 `10 미감정 전리품 + 8 작업량 → 10 감정된 귀중품`으로 전환된다. 프로브는 미감정 전리품의 회수 가치를 감정 후 EWU로 투영해야 한다.
- `OffenseEncounterSO.minimumSiteStrength/maximumSiteStrength`는 현재 에셋 생성·검증·보고 외에는 선택이나 스케일링 런타임에서 사용되지 않는다. `EnemyEncounterFactory.SelectEncounter`는 캠페인 순서와 해시 변형만 사용한다.
- 실제 목표 난도 권위는 `OffenseTargetDefinition.danger`, `requiredPower`, `campaignOrder`에 있으며, 조우의 site-strength 필드는 현재 죽은 메타데이터다. 이를 그대로 밸런스 축으로 쓰면 잘못된 결론이 된다.
- `ResourceOffenseCampaignCatalog(ResourceGameContentCatalog)`로 실제 목표 정의를 읽을 수 있다. 조우 선택 공식이 `campaignOrder`별 6개 변형을 사용하므로 목표별 난도와 해당 6개 조우의 예산을 함께 감사하는 것이 올바른 축이다.
- 실제 조우 선택은 `campaignOrder` 1~6을 각각 `encounter:01~06`, `07~12`, `13~18`, `19~24`, `25~30`, `31~36`에 매핑한다. 따라서 조우 ID 6개 묶음은 표시명이 아니라 런타임 캠페인 목표와 연결된 올바른 진행 단위다.
- 목표 `requiredPower`는 10/16/32/42/60/85로 상승하지만 기존 적 생성은 이 값을 전혀 사용하지 않아 작성된 조우의 원시 위협 중앙값이 진행에 따라 역전되었다. `sqrt(requiredPower/10)`을 체력·공격·힘·강인함·사격 스탯에, 그 제곱근을 민첩·이동에 적용하는 단일 캠페인 스케일 권위를 추가했다.
- 캠페인 위협 예산은 원시 조우 예산에 `requiredPower/10`을 곱해 비교한다. 이는 콘텐츠 진행성 감사값이며 실제 파티 승률·부상·탄약 소모의 대체 증거는 아니다.
- 전략 월드의 동적 거점은 `strength × 12`를 권장 전투력으로 사용하고, 보상은 `OffenseSiteArchetypeSO`의 `baseAmount + (strength-1)×amountPerStrength`에서 생성된다. 고정 6단계 캠페인 목표 보상과 동적 거점 보상은 서로 다른 권위이므로 각각 감사를 거친 뒤 총 위험 대비 보상을 비교해야 한다.
- 고정 캠페인 목표 보상은 1단계 식재료 40+미감정 전리품 80, 2단계 잡화 30+미감정 전리품 120, 3단계 무기 25+설계도, 4단계 마력 35+설계도, 5단계 희귀 시설·후보·특수 동물, 6단계 미감정 전리품 500이다. 설계도·시설·인구·전략 압력은 EWU로 단순 환산하면 안 되므로 물리 보상과 고유 진행 보상을 분리해서 판정해야 한다.
- `OffenseMoneyRewardSpec`은 즉시 금화가 아니라 미감정 전리품 물리 스택을 생성한다. 보상 경제 감사에서는 표시상 Money라도 감정 작업을 포함한 물리 가치로 계산해야 한다.
- 전투·원정·캠페인 회귀 묶음과 V23 경제 감사가 세력 콘텐츠 재생성 뒤에도 통과했으며 Unity Console은 Error 0 / Warning 0이다.
- 14개 손님 요청의 물리 원가·금화 보상·순마진은 이미 V23 경제 감사가 `GoldEconomyBalanceRules.CalculatePremiumServiceReward`와 허용 순마진 밴드로 전수 검증한다. 전략 보고서에 같은 계산을 중복할 필요가 없다.
- 계절 사건 정의는 최소/최대 기간, 서로 다른 영향 도메인 2개 이상, 유효 기계 효과 존재만 검증한다. 일일 효과가 기간과 곱해질 때의 총 부담 상한은 아직 감사되지 않는다.
- 현재 28개 계절 사건은 모두 효과를 `startEffects` 한 번에 기록하고 `dailyEffects`는 비어 있다. `durationDays=maximumDurationDays`는 효과 지속시간이며 매일 중복 적용되는 비용은 아니므로 기존 콘텐츠에는 기간 곱 누적 폭주가 없다.
- 계절별 사건은 정확히 7개이고 기간은 1–6일, 단일 효과량은 위협 2–6, 질병 노출 7–12, 작업 지연 1–3일, 관계 4–5, 물자 6–8, 금화 -120/+180 범위다. 이 범위를 자동 계약으로 고정할 검증기가 아직 없다.
- 축제 비용은 기존 경제 감사에 포함되지 않으며, 성공/부분/실패 기분 순서와 참가자 1명당 물리 투입 EWU도 별도 게이트가 필요하다.
- 첫 축제 비용 보고서에서 핵 공명일은 397.58 EWU/인, 도구씨족 품평회는 195.09 EWU/인, 고일제는 89.12 EWU/인으로 다른 축제(대체로 8.5–74.5 EWU/인)보다 과도했다. 각각 정비 키트 10개, 기계 부품 8개, 맥주 10개의 작성 수량이 원인이다.
- 건조 과일·곡물 종자 로트·축제 모둠식·협약 깃발·양초는 물리 아이템이지만 제작 EWU 계산에서 해석되지 않는다. 이들의 실제 획득 경로를 확인하기 전 시장가만으로 덮으면 생산 연결 누락을 숨길 수 있다.
- 실제 카탈로그에는 `seed-lot:twilight-grain`과 양초 레시피가 이미 존재한다. 축제가 `item:seed-lot-grain`, `item:candle` 같은 오래된 별칭을 요구하는 정황이므로 새 복제품을 만들기보다 현재 물리 ID로 교정해야 한다.
- 나머지 구형 축제 참조는 현재 생산·수확품인 밤포도, 호화 채식 연회식, 몽직물 의식 장식으로 교체했다. 새 중복 아이템은 만들지 않았다.
- ID 교체와 1차 수량 조정 뒤 13개 축제는 8.52–79.52 EWU/인 범위에 들어왔다. 혈향 등불제 141.14, 긴밤 추모제 105.85 EWU/인은 양초 16/20개가 원인이므로 8/12개로 낮췄다.
- 종자 로트는 제작물이 아니라 수확 시 회수되는 특수 물리 상태라 EWU가 직접 없다. 축제 기회비용에서는 동일 작물 생산물 EWU의 1.5배로 평가해 실제 수확 권위와 연결한다.
- 나머지 구형 축제 참조는 현재 생산·수확품인 밤포도, 호화 채식 연회식, 몽직물 의식 장식으로 의미를 보존해 교체할 수 있다. 새 중복 아이템을 만들 필요가 없다.
- `seasonal:autumn-migration-window`는 자원 손실이 아닌 기회성 WorldFlag 사건이므로 정규화 위험 0은 정상이다. 계절 사건 게이트는 유효 효과 존재를 별도로 확인하고 위험 하한을 강제하지 않아야 한다.
- 원정대 권장 전투력은 구성원별 `공격×1.4 + 힘×0.8 + 강인함×0.6 + 지구력×0.4 + 이동×0.25`에 전투 배율을 곱한 합이다. 목표 `requiredPower`는 출정 차단값이 아니라 UI 비교값이며, 실제 전투 난도 스케일과 숫자 의미를 맞추려면 승률 프로브로 교정해야 한다.
- 실제 전투 시작 경로는 `OffenseEncounterCatalog.CreateAlly`로 아군 스탯을 만들고 장비·신체 상태를 연결한 뒤, `EnemyEncounterFactory.Create`의 적과 같은 `OffenseBattleSession`에 넣는다. 실제 승률 프로브는 이 경로를 재사용해야 하며 단순 가상 스탯 대결로 대체하면 안 된다.
- `OffenseBattleSession`은 적에게만 `CreateEnemyCommand`를 제공한다. 실제 자동 승률 프로브에는 아군의 사거리·능력·목표를 고르는 별도 결정론 정책이 필요하며, 단순 기본 공격만 쓰면 보호·탈출·생포 목표와 장비 역할을 잘못 평가한다.
- 세력 계약에는 이미 12명 기준 인구, 성인 1명당 99 작업/일, 생산 노동 비중 42.5%와 계약 종류별 생산 부담 밴드(정기 1–3%, 위기 3–8%, 전략 5–15%)가 코드 상수로 정의되어 있으나 현재 `ValidateDefinition`이나 QA가 실제 물품 EWU를 이 밴드와 대조하지 않는다.
- 기존 캠페인 검증은 9개 이정표를 모든 조건이 충족된 합성 스냅샷으로 120일 안에 열 수 있고 6×6 장·18계약 구조가 존재함을 증명하지만, 실제 생산·전투·세대 속도로 조건에 도달하는 시점을 측정하지 않는다.
- 첫 전략 감사의 9개 실패는 모두 제작 불가능한 고유 `item:lineage-seal`을 생산 EWU로 평가하려 한 데서 발생했다. 나머지 17개 계약은 선언된 부담 밴드에 실질적으로 들어왔고, 34개 일반 장은 bargain 비용이 support의 50–60%였다.
- 계보 인장은 지역 보스 첫 처치에서만 고유 물리 아이템으로 생성되므로 생산 EWU가 0인 것이 정상이다. 시장가 42를 대체 비용으로 쓰면 희소성과 진척 가치가 사라지므로, 비제작 전략 자원에는 별도 기회비용 권위가 필요하다.
- 전략 월드는 지역 ID를 변경 교역권·경쟁 던전 전초권·봉인 지대 세 종류로만 해석하고 계보 인장은 지역별 최초 보스 보상 한 번만 지급한다. 따라서 한 런의 최대 공급은 3개인데 악마 3장 support 6개, 6장 support 9개, 전략 계약 1개를 요구해 현재 콘텐츠는 물리적으로 달성 불가능하다.
- 제작 가능한 `tool:administrative-seal`은 철괴+종이를 문장 작업대에서 가공하는 행정 인장이고, 악마 6장 ‘새 인장’과 장기 계약의 의미에 맞는다. 악마 3장 ‘주조소 담보’는 본문과 동일하게 룬 도체를 요구하도록 장별 재료를 분리하는 것이 자연스럽다.
- 수정 후 악마 3장 비용은 support/bargain 880.76/440.38 EWU, 6장은 1194.23/663.46 EWU이며 할인 비율 50%/55.6%다. 악마 전략 계약은 행정 인장 18개, 2388.47 EWU로 45일 기준 생산력의 10.51%를 차지해 전략 계약 목표 5–15%에 들어온다.
- 전투 스케일·세력 비용·유한 고유 자원·계절 사건·축제·서비스 사고의 수치 계약을 `docs/game-design/whole-game-balance-baseline.md`에 권위 기준으로 고정했다.
- 현재 전투 콘텐츠 감사는 36개 조우의 규모와 캠페인 증가성을 증명하지만 실제 승률은 증명하지 않는다. 최종 균형 판정에는 여섯 목표를 수행하는 실제 아군 정책과 다중 시드 승률·부상·소모 측정이 필요하다.
- 전략 콘텐츠 감사는 계약 18개, 세력 장 36개, 이정표 9개, 계절 사건 28개, 축제 16개, 서비스 사고 8개를 전수 통과한다.
- 집중 최종 게이트 재실행 결과 연구 시대 도달은 32.2/80.4/234.3/372.0일, 질병 정의 16개, 기반시설 419개, 자동화 시설 28개, 10K 셀/2K 경로 스트레스와 68개 저장 섹션 계약이 통과했다.
- 편집기 정적 저장 계약 통과와 실제 플레이 월드의 캡처·복원·재캡처는 서로 다른 증거다. 후자는 PlayMode 전체 월드 러너로 다시 확인해야 한다.
- 전용 PlayMode 전체 월드 러너로 실제 68/68/68 캡처·복원·재캡처, 정규 기준선 일치와 라이브 기준선 복구를 확인했다. 저장 통합은 최종 통과 상태다.
- 첫 실제 전투 결과 계측은 36조우×32시드를 완주했지만 전투 114건이 명령 상한까지 종료되지 않았다. 보호 목표는 별도 보호 NPC가 없어 1인 권장 원정대에서 즉시 패배하고, 생포 목표는 비살상 탄약 준비가 없으며, 기본 공격 사거리 밖에서는 아군·적 AI 모두 이동 수단 없이 경비만 반복한다. 난이도 수치를 조정하기 전에 목표 조립과 행동 계약부터 고쳐야 한다.
- `ProtectTarget` 런타임은 `ObjectiveCombatantId`가 비어 있으면 첫 원정대원을 보호 대상으로 지정한다. 권장 인원 1인 캠페인에서는 별도 호위가 없어 시작 즉시 패배하므로 조우 팩토리가 비참여형 보호 NPC를 전투원으로 생성해야 한다.
- `CaptureLeader`는 살아 있는 `IsDowned` 상태만 승리로 인정한다. 일반 살상 장비만 든 파티는 지휘관을 죽여 실패할 수 있으므로 승률 프로브와 실제 준비 UI 모두 석궁 호환 `ammo:tranquilizer-dart` 같은 비살상 수단을 명시적으로 포함해야 한다.
- 원정 경로는 일반 전투를 깊이 1·위험 0.75·최대 2명, 정예 전투를 깊이 3·위험 약 1.0·최대 4명, 보스를 깊이 4·위험 1.0·전체 편성으로 조립한다. 첫 결과 프로브는 `routeNode=null`로 모든 조우를 전체 편성으로 생성해 난도를 과대평가했다.
- 실제 `SelectEncounter`도 노드 종류와 `encounter.elite/boss`를 연결하지 않아 보스 조우가 첫 경계 전투에 선택될 수 있었다. 캠페인 내 일반·정예·보스 풀을 경로 노드별로 분리해야 조우 이름과 실제 위험 단계가 일치한다.
- 2026-08-09: `ResolveOutcome`은 이미 생존 전력을 `!IsDead && !IsDowned`로 판정하고 성공한 명령마다 결과를 다시 계산한다. 따라서 남은 `DefeatAll` 교착 17건은 쓰러진 전투원을 생존자로 잘못 세는 문제가 아니다. 600명령 상한까지 양측이 전투 가능 상태로 남거나 유효 공격을 만들지 못하는 상태이므로, 종료 규칙을 바꾸기 전에 교착 표본의 체력·대형·현재 무기·탄약·행동 분포를 기록해야 한다.
- 2026-08-09: Unity의 보통 `RequestScriptCompilation()` 호출은 이번 세션에서 기본 어셈블리를 갱신하지 않았고, Bee 로그를 확인한 결과 강제 clean build가 실제 오류를 드러냈다. `ICombatEquipmentRuntime`은 fallback 선택기가 요구하는 세 메서드를 이미 모두 제공하지만 해당 포트 상속 선언만 빠져 있었다. 계약에 `ICombatFallbackWeaponRuntimePort`를 추가하는 것이 중복 어댑터 없이 기존 구현을 정확히 노출하는 수정이다.
- 2026-08-09: 최신 런타임이 실제로 컴파일된 뒤 36×32 전투 결과 프로브의 교착은 17건에서 0건으로 사라졌다. 원인은 종료 판정이 아니라 `ICombatEquipmentRuntime` 포트 선언 누락 때문에 유한 탄약 fallback 최신 코드가 기본 어셈블리에 반영되지 않았던 것이다.
- 2026-08-09: 기준 전력에서 캠페인 평균 승률은 43.8/49.5/41.7/77.1/69.8/53.6%지만, 같은 캠페인 내 목표별 승률은 0~100%까지 갈린다. 단일 캠페인 `requiredPower`만 바꾸면 해결할 수 없는 구조다. 특히 Escape/Survive는 과도하게 쉽고, 초기 Sabotage/Capture와 일부 Protect는 전력을 4~8배 올려도 낮다. 다음 교정 단위는 캠페인 숫자가 아니라 조우 목표·보호대상/파괴대상 내구·비살상 준비·목표별 AI 계약이다.
- 2026-08-09: 파워 스윕에서 캠페인 1은 1× 평균 37.5%, 4× 58.3%, 8× 83.3%인데 초기 파괴 목표는 8×에서도 12.5%에 불과하다. 캠페인 2 생포도 8×에서 25%다. 이는 권장 전력 부족이 아니라 두 발만 지급되는 진정 탄약, 목표 방어/내구, 목표별 시간·행동 기회의 문제다.
## 2026-08-09 V25 nine-proficiency integration

- Current authored background skill IDs are string-based and the normal work executor awards generic progression XP rather than profession-specific XP.
- The existing work contribution accumulator already retains worker contribution and a relevant-skill snapshot, so it is the safest authority for quality and atomic XP attribution.
- Existing mentorship state is persistent but targets generic character progression; it must gain a proficiency ID and award only after a real academy session.
- Git status currently requires an LFS-filter-disabled read path because the sandbox cannot write `.git/lfs/tmp`; this is a diagnostic constraint, not a source change.
- The worktree contains extensive pre-existing image/content/balance changes and deleted training artifacts. Phase 145 must only touch proficiency-related code, generated evidence, and the two design authorities.
- `CharacterNarrativeWorldSaveData` is version 3 and stores integer skill XP; `CharacterCareerWorldSaveData` is version 1 and mentorship is keyed by student with no proficiency ID.
- Successful facility work still calls `actor.Progression.AddExperience(5)`. Existing daily mentorship similarly awards generic progression XP after consuming career-ledger durability.
- `CraftContributionAccumulator` already captures contribution-weighted `relevantSkill`, allowing proficiency quality snapshots without changing completion-time quality determinism.
- The root `GameDomainContentCatalogSO` can index additional authored `ScriptableObject` definitions without creating a second catalog authority. `CharacterNarrativeCatalog` is the natural validator/query extension point for exactly nine proficiency definitions.
- One discovery read guessed `Assets/Scripts/Content/V20AuthoredContentSO.cs`; that file does not exist. No mutation occurred and the base type will be located before adding the proficiency SO.
- The actual authored base is `V20AuthoredContentContracts.cs`; it provides editor-only `ConfigureMetadata`, so a dedicated proficiency builder can create nine real assets and republish the existing domain catalog without runtime synthesis.
- `WorkerNarrativeQualificationQuery` already exposes XP/rank to V23 worker filters. Its projections can be upgraded to the new current proficiency state without changing worker-policy persistence shape.

## 2026-08-10 Phase 147 trait expansion findings

- The live balance authority still records 56 general founder traits, 1/2/3/4 counts at 15/40/35/10%, and rarity weights whose observed per-trait appearance rates are 6.12/3.43/1.57/0.66%.
- Nine proficiency authorities exist: fieldwork, construction-engineering, crafting, food-production, scholarship, medicine, social, melee-combat, and ranged-combat.
- Starting proficiency bonuses must apply before the final age cap of 99/174/249/399 XP; no trait may create a second persistent proficiency authority.
- The user rejected mirrored proficiency pairs and rigid equal deltas. Each mundane tradeoff must have a natural background/aptitude explanation and varied magnitudes.
- The user requested exactly 100 total traits, with larger simple-positive and simple-negative pools in addition to identity and extreme traits.
- Three formerly discussed extreme traits are explicitly excluded: high-stakes negotiator, death-and-treasure expedition, and deep salvage gambler. Seven extreme traits remain in scope.

## 2026-08-10 Phase 147 approved implementation contract

- The authoritative selectable founder roster must contain exactly 100 traits: retained IDs 101-109, 200-230, 235, 239, 245 plus new IDs 247-259, 300-306, 400-417, and 500-518.
- Retired selectable IDs are 231-234, 236-238, 240-244, and 246; they must not be silently substituted during restore.
- Shared numeric effects use stable-ID ScriptableObject definitions plus value bindings. Only heterogeneous identity behavior uses SerializeReference.
- CharacterGrowthState.traitIds and ResolveSelectedTraits() are the trait authority for projection, AI, mood, events, and UI.
- Mythic quality is appended as enum value 7, is unreachable from the normal score resolver, and can only be promoted by trait 300 on an eligible finished weapon, armor, shield, or apparel item using a saved deterministic roll.
- The existing Phase 147 record still reports the completed 56-trait audit and therefore must be revised only after the new 100-trait audit passes; it must not be overwritten pre-emptively.
- Unity MCP exposes editor command execution, console reads, resource reads, and script operations. Repository code will still be patched with apply_patch, while Unity compilation, asset creation/build menus, and runtime audits use Unity MCP as required.
- Current selection is rarity-only inside one flat candidate pool. `CharacterTraitSelectionRules.Select` already enforces family, incompatibility, species, exact target count, and fail-fast exhaustion, so polarity slot weighting and extreme attenuation belong there without creating a second selector.
- `CharacterProgressionProfileProjector.ResolveSelectedTraits(actor,state)` already exists and is used by effective numeric profile projection and several UIs. New shared effects can use this authority; remaining AI/mood call sites must be audited separately.
- Start-party founder traits are rolled in `StartPartyPreparationService`, while later population founders use `CharacterPopulationService.RollTraits`. Both call the same selection rule, making selection distribution centralization feasible.
- Starting proficiency data is stored in `CharacterGrowthState.startingProficiencies`; trait XP deltas should be applied once when the rolled profile and trait IDs are assembled, then clamped through the existing age-cap rule.
- `CharacterProgressionProfileProjector` currently falls back from empty `growthState.traitIds` to `CharacterSO.traits` and silently skips unknown IDs. The approved authority contract requires removing that fallback after initialization and throwing a diagnostic for missing selected IDs.
- Legacy numeric projection currently reads `trait.statBonus` and passes selected traits into `CharacterRuntimeProfileFactory`, which also consumes `CharacterModelModifiers`. Shared-effect rollout must avoid applying both paths; a staged migration flag or neutralized legacy payload is required.
- Start-party generation rolls trait IDs before building the age/origin/history profile, so trait starting-XP deltas can be applied in `ApplyStartingProfile` without changing RNG order. The later `CharacterPopulationService` path currently uses the old seed-only starting packet and will need the same central delta helper.
- `CharacterRuntimeProfile.Create` is the current shared numeric consumption point for species and selected trait stat/modifier payloads. It builds final stats/modifiers once, so it is the lowest-risk first integration point for shared trait effect projection while retaining existing public getters.
- `CharacterRuntimeProfile` separately derives behavior and event-weight dictionaries plus earned XP from legacy fields. These need migration-backed reads from identity rules/effect bindings to prevent new traits from appearing inert.
- Combat equipment and module definitions are plain ScriptableObjects rather than DataScriptableObjects. Shared-effect sourcing should therefore use their existing stable string IDs instead of assuming the numeric `DataScriptableObject.id` contract.
- `CharacterSpeciesSO` is a `SerializedScriptableObject` derivative with the same legacy stat/modifier/combat-ability split as traits. It can implement the shared source interface with its existing species stable ID while keeping legacy fields for migration.
- The root domain catalog accepts arbitrary ScriptableObject definitions, so shared effect and condition SO assets can be indexed alongside traits without adding a second catalog authority.
- Shared-effect integration now preserves legacy-only content by using legacy numeric fields only when a source has no new bindings. Once a source has shared bindings, numeric legacy fields are bypassed while legacy work/facility preference flags remain available during migration.
- `Combat`, `Content`, `Species`, and `Economy` are separate asmdefs and already reference `DungeonStory.Foundation`. The shared effect interfaces, binding DTOs, definition SOs, and contribution trace therefore belong in Foundation to avoid adding a new cyclic assembly.
- `CharacterGameplayEffectProjector` references `CharacterTraitSO` and `CharacterModelModifiers`, so it must remain in a higher assembly and be split from the dependency-free contract file.
- `DataScriptableObject` lives in `DungeonStory.Economy`, which already depends on Foundation. A Foundation effect contract cannot derive from it. Effect and condition definitions therefore use an equivalent local numeric-ID field on `ScriptableObject`; the stable `effectId`/`conditionId` remains the actual authority.
- Two post-audit trait consumers still read `Identity.Data.traits`: legacy trait mood reactions and V20 event content weighting. Both are runtime consumers, not migration readers, and must use `ResolveSelectedTraits()` to preserve the single saved authority.
- The first identity-state key fix preserved the character ID separately for capture grouping but the dictionary key could still collide when a component contained `+`. Percent-escaping separators preserves the requested stable string key while making capture/restore unambiguous.
- `PersistentNeedRule` was initially treated as an immediate mood-rule alias and did not retain deprivation streaks. It now requires saved first-deprived, last-satisfied, and last-applied-day state; typed deprived events remain the trigger authority.
- The central snapshot projector initially used `Mathf` without importing `UnityEngine`; this was found by static review while MCP compilation was unavailable.
- Combat craft orders used random GUIDs even though Mythic rolls require deterministic pipeline identity. A save-owned monotonic sequence is now the order pipeline authority; legacy saves derive a safe next sequence from retained orders.
- Existing equipment/apparel production target UIs already cap at enum value 6, so Mythic value 7 remains unselectable as a production target. Storage/conveyor maximum-quality controls already accept value 7.
- Automatic rejected-output paths cannot normally classify Mythic as rejected because target quality caps at Legendary, but the market settlement now also refuses a manually misrouted Mythic stack as defense in depth.
- Unity MCP is currently blocked below the project layer: `Unity_ManageEditor`, `Unity_ReadConsole`, and `Unity_RunCommand` all hang after the editor logged a Licensing Client disconnect. Static changes cannot be reported compiled or balance-complete until that transport/editor condition is repaired.

## 2026-08-10 Phase 147 post-recovery findings

- Unity compiler messages can arrive as Console `Log` entries in this project; checking only Error/Warning types can falsely report a clean compile. Final compile verification must also search all entries for `error CS` and `warning CS`.
- `CharacterDerivedStatsSnapshotProjector` originally called `GetActiveProfileSnapshot`, whose implementation invokes `GetOrCreate`. A supposedly read-only stat query therefore persisted default loadouts for temporary preparation/QA characters and broke strict save referential integrity. Effect projection must use the new non-mutating `TryGetActiveProfileSnapshot` path.
- `CharacterTransientGameplayEffectSourceQuery` should depend on `IBlueprintResearchStateService`, not the scene `BlueprintResearchRuntime` MonoBehaviour. This matches registered DI and retains the research aggregate as the completed-project authority.
- Current natural founder industry capacity is 273.113 required-industry WU/day and 91.493 dedicated crafting WU/day; best-of-3 is 277.614/92.822, best-of-20 is 283.518/94.770, and the authored theoretical bound is 412.925/137.642.
- The recipe-only throughput table has 64 accessible outputs at early research and 202 at mid research, but zero at no research. This is not sufficient evidence for day-one item production because field gathering and other non-recipe work paths are outside that median recipe query.
- World resource logging/gathering/quarry also executes the same authored `ProductionRecipeSO` source recipes, so the recipe scan does include those non-facility field outputs. Their zero no-research rate is intentional: every source recipe has an explicit research requirement.
- The minimum production-unlock closure is 36 research WU for every broad output class currently measured. Natural founder research capacity completes that in 0.389 day; therefore starting-stock adequacy, research-facility availability, and player command time—not founder reroll power—are the real Day 1 risks.
- `PostActionConsequenceRule` and `CharacterDirectOrderCostPreviewService` existed, but no live UI emitted `order:defer-cleaning`; production work-completion events were autonomous and therefore could not satisfy the direct-order contract. The authoritative interaction is the staff work-priority cell: only `Priority1 -> Priority2`, `Priority2 -> Priority3`, and `Priority3 -> Off` for cleaning are deferrals.
- Direct-order cost application belongs after the successful priority mutation, while the same preview must be rendered from the pre-click `current.Next()` state. This keeps restore/setup paths side-effect free and gives the player exact costs before committing.

## 2026-08-10 Phase 149 end-to-end connection audit

- The canonical `CharacterDerivedStatsSnapshotProjector.Project()` has no runtime caller. Live code uses a small set of incremental multiplier queries, so definition and projection audits can pass while domain outcomes remain unchanged.
- Standard authored targets with no live domain consumer are accident chance, cold/heat exposure, craft quality, salvage yield, haul capacity, fatigue rate, recovery speed, food-poisoning chance, relationship recovery and negative-mood duration.
- All retained custom targets are unconsumed by the shared system: temperature thresholds, alarm/sleep response, blunt damage, pain work penalty, danger/spoilage detection, medical aftermath, mentee XP, negotiation, combat stress, harvest/seed yield and arcane power/mana recovery. Harvest has a separate direct extreme-rule path, making its bindings dead duplicates.
- Consumption affects shopping count but not staff hunger depletion. Wait patience and crowd sensitivity primarily affect visitor/facility choice rather than founder work/life state.
- Thirty authored effect condition IDs have no runtime context producer. Identity events and effect conditions are separate systems; a mood event such as `food:sated` does not activate the numeric `state:sated` condition.
- Typed identity events have missing endpoints: death/product-quality/work-start/health-threshold/expedition have no publisher or subscriber; social conflict/apology/prisoner decision have subscribers but no publisher; research progress has a publisher but no subscriber.
- `PersistentNeedRule` does not tick absence. It only advances when an external deprived event is repeatedly published, and its authored duration field is not consumed.
- AI behavior matching collapses every `work:*` rule onto all work and every `food:*`/`rest:*`/`room:*` rule onto all self-care, while `social:*`, `medical:*`, `ritual:*`, `shift:*`, `consume:*` and `alert:*` never match.
- Extreme traits 302/304/305 expose service methods without a live UI caller. Trait 306 has a registered command service without any caller or spell-power consumer. Trait 301 has no authored/consumed pain-immunity field.
- Approval-spec coverage is currently 43 traits end-to-end and 57 traits with at least one missing or semantically wrong endpoint. This invalidates Phase 148 numerical balance closure until Phase 149 repairs and verifies live consumers.

## 2026-08-10 Phase 148 initial measurement boundary

- The existing founder-industry report samples the real three-founder generator but uses a 99 WU/person/day approved-work ceiling and only projects starting proficiency plus unconditional work/research speed effects.
- A trait cannot be collapsed into one WU score without erasing physical distinctions. The next audit must preserve separate time availability, food demand, accident loss, quality yield, behavior/mood interruption, and extreme-event outcome channels.
- The existing live-need calibration is the authoritative daily availability source: 180 seconds/day, standard-sufficient mean work ratio 74.6814%, life ratio 23.2364%, queue ratio 2.0801%, meals 1.0728/person/day, and p10 minimum need 36.825 with no normal-supply breakdowns.
- Applying that measured work ratio to the authored 99-WU ceiling gives 73.935 effective base WU/person/day before proficiency/trait speed, rather than the current founder report's pure 99-WU ceiling.
- Proficiency has three distinct continuous outputs: speed 0.85→1.30, quality score 25→100, and accident-risk multiplier 1.25→0.60. These can be projected independently from the same starting XP without inventing a scalar trait value.
- The shared `AccidentChance` multiplier is projected and displayed, but no generic production/work executor currently consumes it as a base accident roll. Therefore Phase 148 can report relative accident-risk multipliers, but absolute lost WU/material would be fabricated and must be marked `실행기 미연결` rather than assigned an assumed accident rate.
## Phase 149 connection-closure implementation findings (continued)

- `CharacterCarryInventory.GetBaseCarryLimit()` previously used only Strength, Endurance and the hauling harness; it never consumed `work:haul-capacity`.
- `CombatEquipmentRuntime.TrySalvage()` previously fixed recovery to `PrimaryMaterialAmount * 0.5 * durability`; it never consumed `work:salvage-yield` and has no worker argument, so a worker-authoritative salvage command boundary is required.
- `CharacterNeedStateService.CalculateTimedDecay()` was the authoritative staff hunger depletion path but multiplied only persona and species need profiles. The canonical character consumption projection is now also applied to hunger and food-driven excretion, closing the previously shopping-only trait effect.
- A canonical arbitrary detailed-stat query has been added at the shared projector/projection-service/CharacterStats/CharacterActor boundary. Domain systems can now supply their owned base value and condition IDs without inspecting traits or bindings.
- Live connection work now covers consumption-driven hunger/excretion, deterministic work-accident hazard, thermal offsets/exposure, equipment/apparel quality, carry capacity, work fatigue, healing, contaminated-meal poisoning, negative relationship recovery/apology recovery, negative mood duration, pain work penalty and blunt incoming damage.
- The work accident authority is approved physical work, not elapsed frames: `p = 1 - exp(-0.001 * acceptedWU * projectedAccidentMultiplier)`. A triggered accident deals 2 health damage, stops the current run, emits an explicit activity reason and records the proficiency outcome as accident/forced stop. Instant-work debug completion does not roll.
- `work:salvage-yield` still cannot be connected honestly at `CombatEquipmentRuntime.TrySalvage()` because the existing public command has no worker/character parameter. This requires an explicit worker-authoritative overload/caller migration rather than guessing a global trait owner.

## 2026-08-10 Phase 149 exhaustive current orphan inventory

- The current V26 founder catalogue has 45 unique effect targets: 40 reach at least one runtime consumer and 5 remain definition-only (`alarm-response-delay`, `sleep-recovery`, `medical:aftermath-duration`, `arcane:power`, `arcane:mana-recovery`). Salvage yield is only partially connected because normal dismantling lacks worker authority.
- Of 45 effect conditions, 9 have no producer at all and 7 more are unreachable because their upstream social, ritual or arcane command/event path has no live caller. Effective condition coverage is 29/45.
- The 62 authored identity event IDs have only 11 real domain production paths; 51 are never emitted. The 39 behavior/action tags have 20 real action/command paths and 19 never appear on an AI candidate.
- Seven of seventeen typed identity event types have a missing endpoint: death and research progress lack subscribers; work start and expedition outcome lack both; social conflict, apology and prisoner decision lack publishers.
- Persistent needs remain 1/9 complete. Sweet and salt needs can be satisfied but cannot become deprived from absence; six further needs have neither side connected.
- Extreme traits are 2 complete (300, 303), 1 partial (301 pain immunity), and 4 without live callers/domain consumers (302, 304, 305, 306).
- Cross-authority/UI gaps remain: status effect source collection is permanently empty; combat skill-track still reads `CharacterSO.traits`; one legacy mood adapter still reads `moodReactions`; founder tooltips still read cleared legacy numeric fields rather than shared effects/identity rules; contribution trace is not player-visible.
- The existing V26 deterministic audit calls extreme services directly and does not perform source-to-consumer, condition-producer, event-publisher, action-tag or UI coverage checks, so it can pass with these orphans.
- Full evidence and the per-trait missing-ID list are recorded in `Artifacts/QA/v26-founder-trait-connectivity-audit.md`. Unity is not compiling and Console currently contains zero errors; this is build evidence only, not connection evidence.

## 2026-08-10 Phase 149 connection repair update

- The five previously definition-only numeric targets now have live consumers: alert response delay, noisy-room sleep recovery, medical aftermath duration, arcane weapon power, and mana-block recovery. Arcane mana recovery is still only a partial domain connection because no character mana pool exists.
- Shared spending, stay duration, crowd sensitivity, and wait patience now combine trait/equipment/species/status/research sources instead of reading only the legacy runtime profile.
- Sedation is the first real `Status` gameplay-effect source. Its former direct multipliers were removed, preventing double application and proving the same projector can combine trait and status sources.
- Generic approved work now emits typed start/completion events, and real expedition, captivity, and friendly-fire flows emit their typed identity events. Daily absence clocks now drive combat inactivity, research inaccessibility, sweets, salt, and stimulation deprivation.
- Actual work state now produces `work:long-shift`, `work:on-schedule`, and `work:retry-after-failure`; current non-dry terrain produces `terrain:rough` and `accident:fall-slip`; airborne exposure during cooking produces `work:contaminated-food`.
- `CharacterRuntimeProfile` behavior matching is now exact, but 19 authored semantic tags still have no real AI candidate and several provisional work tags are over-broad. Exact matching prevents false positives but does not create the missing actions.
- Founder trait tooltips now describe shared bindings, conditions, polarity, rarity, family, and identity rules. Direct-order authored duration is no longer discarded.
- Last Stand now removes low-health/pain performance loss while active instead of multiplying its +50% effect by the same critical-health penalty.
- The research tree now has the first real caller for `TryForbiddenResearchLeap`; it selects a living trait-302 holder deterministically and reports the actual outcome. UI regression is still pending.
- The refreshed complete residual inventory is UTF-8 and lives at `Artifacts/QA/v26-founder-trait-connectivity-audit.md`: 2 partially connected targets, 6 missing conditions, 42 missing event IDs, 19 missing AI tags, 3 typed endpoint gaps, 3 incomplete persistent needs, and 3 incomplete extreme traits.

## 2026-08-10 Phase 149 residual inventory refresh

- Actual meal item tags now distinguish sweet, salted, and unfamiliar meals; their behavior tags and identity satisfaction/new-meal events have live producers.
- Approved work now emits failure, dangerous assignment/safe return, small-success, first-process-success, schedule reassignment, forced-day-shift, rough-terrain safe crossing, safe cold completion, and clinic-entry identity events.
- Actual rest emits a typed endpoint carrying recovery and room conditions, closing sufficient-rest, noisy-sleep, and private-rest event production. Typed identity endpoint coverage is now 18/18.
- The residual catalogue is now 2 partially connected numeric targets, 5 missing conditions, 28 missing identity event IDs, 13 missing AI candidate tags, 3 incomplete persistent needs, and 3 incomplete extreme traits.
- Nine currently produced AI tags are false-positive mappings rather than real connections: all Guard work is tagged as training/emergency-check/minor-alert, all Research work as new-process/inspect, and all Craft work as inspect/prototype/quality-first/new-process.

## 2026-08-11 Phase 149 연결성 종결 및 바닥값 재계산

- 1차 카탈로그 중심 source-to-consumer manifest는 `rows=234`, `targets=45`, `conditions=45`, `identity=63`, `behaviors=38`, `needs=9`, `extremes=7`, `orphans=0`으로 통과했다. 이 판정은 아래 동적 함수·필드 심층 감사로 대체되었으며 현재 권위 수치는 541행이다.
- gameplay mutation 함수 27개는 `GameplayEntryPoint`, `GameplayInternalOnly`, `GameplayMigrationOnly` 중 정확히 하나의 의도 속성을 가진다. 신규 직렬화 필드나 공개 명령이 소비자 없이 남는 경우 자동 감사에서 실패하도록 AGENT.md와 manifest 계약을 함께 고정했다.
- 동일 시드 자연 3인방에서 특성만 제거한 대조군과 비교하면 필수 산업 WU는 272.753→273.113(+0.13%), 최고 제작은 91.078→91.493(+0.46%), 음식 수요는 3.000→3.022(+0.72%), 기대 사고는 +0.23%, 성공 작업 XP 배율은 +0.02%였다. 특성을 하나의 WU 점수로 합치지 않고 작업·소비·사고·XP 축으로 분리한 결과다.
- 자연 3인방 절대값은 필수 산업 273.113 WU/일, 최고 제작 91.493 WU/일, 최고 연구 약 92.5 WU/일, 음식 수요 지수 3.022, 3개 산업 교대 기대 사고 0.323건/일, 기대 생명력 피해 0.647/일, 성공 작업 XP 7.922/인·일이다.
- 장비 준비 감사의 고정 99 WU 창립자 가정을 제거했다. 최초 3인은 자연 생성된 산업 속도 합과 최고 제작/연구 속도를 사용하고, 이후 인원만 중립 1.00으로 계산한다. Day 30/120/240/400/960의 6개 체크포인트는 모두 통과했다.
- 전체 월드 저장 내부 시나리오가 다른 테스트의 소유자 생성에 의존하던 문제를 제거했다. 침입 피해 표식도 임의 월드 오브젝트가 아니라 저장 기준선에 포함된 영속 시설에서만 선택한다.
- `enemy:settler-barricadier`의 spear+wood shield와 `enemy:neutral-clockwork`의 warhammer+iron shield는 각각 3손을 요구하는 불가능 조합이었다. 방패 역할을 보존하도록 falchion+wood shield, mace+iron shield로 교정했고 전체 적 장비 손 호환 감사가 통과했다.
- 공식 Full World PlayMode 왕복은 registered/captured/post `68/68/68`, baselineRestored=True, canonicalBaselineMatched=True, Console Warning/Error `0/0`으로 통과했다.
- 아직 완료되지 않은 것은 연결이 아니라 밸런스 정밀도다. p10/중앙/p90 일과표, 식사·수면·이동·기분 중단, 사고 발생 시점별 실제 손실 WU, 체크포인트별 창립자 숙련 성장과 품질 가치 분포를 다음 계산에 포함해야 한다.

## 2026-08-11 Phase 149 동적 함수·필드 심층 감사

- 이전 234행 감사는 effect ID와 27개 수동 명령 목록 중심이라 새 public API, `Start/Tick/Dispose`, 상태 저장 `Set/Restore/Remove`, lease 갱신·만료, 죽은 직렬화 필드가 목록 밖에 남을 수 있었다. “카탈로그 전수”였지만 “관련 함수·필드 전수”는 아니었다.
- 감사기를 동적 열거 방식으로 바꾼 결과 범위가 541행으로 늘었다: target 45, condition 45, identity 63, behavior 38, need 9, extreme 7, public API 104, private/internal/protected helper 77, serialized field 126. 최종 orphan은 0이다.
- 이 과정에서 실제 결함을 추가로 발견했다. 극한형 정체성 규칙과 공용 binding이 같은 수치 배율을 중복 소유했고, 사선 각성 전투력은 전투 스냅샷과 실제 명령에서 두 번 적용될 수 있었다. 정체성 규칙의 공용 수치를 제거하고 전투력은 `combat-power` 투영 한 번만 적용하도록 교정했다.
- 신들린 영감의 `mythicChance`와 `minimumContributionShare`는 SO에 직렬화되어 있었지만 제작기는 하드코딩 상수를 사용했다. 장비·의복 제작기가 선택된 실제 규칙 인스턴스를 읽도록 바꾸고 0%/100% 경계 검증을 추가했다.
- `GameplayEffectBinding.parameters`는 생산자도 소비자도 없는 죽은 확장점이었다. 타입과 필드를 제거하고 100개 창립자 특성 에셋을 Unity MCP로 다시 생성했다.
- 마력 과충전 결과 DTO의 위력·지속시간, 황금 수확 규칙의 성공 배율도 실제 소비 또는 공용 binding과 중복되어 제거했다. 극한형 규칙은 발동·확률·실패 비용·상태만, 공용 effect는 수치 결과만 소유한다.
- 구형 `CharacterTraitSO.moodReactions`는 신규 기분 권위가 아니므로 `GameplayMigrationOnly` 사유와 제거 조건을 붙였다. 실제 기분 변화는 typed event와 `CharacterMoodPolicyService`를 통과한다.
- AGENT.md에는 public 함수·직렬화 필드 자동 열거, 공용 수치 이중 직렬화 금지, authored 수치의 상수 복제 금지, 생명주기·저장 함수의 의도 속성 강제를 추가했다. 새 함수나 필드가 생기면 manifest 행 수가 자동 증가하고 연결 증거가 없으면 실패한다.
- 결정론적 창립자 감사는 100종/100,000회 리롤/신화 1,000,000회에서 통과했고 신화율은 3.0083%, 일반 신화는 0건이다. 공식 Full World PlayMode도 68/68/68, 기준선 복원, Console Warning/Error 0/0으로 통과했다.

## 2026-08-11 Phase 150 질병 세부 능력치 분리 착수

- 현재 `character:recovery-speed`의 유일한 질병 외 소비는 `CharacterStats.Heal`: HP·상처 회복량만 투영한다. 감염·면역 계산에는 사용되지 않는다.
- 감염 확률은 질병 기본 확률, 노출시간, 저장된 질병별 면역, 감수성, 환경계수로 계산한다. 감수성은 현재 종족 환경 프로필과 유전 `DiseaseResistance`만 반영하며 창립자 공용 효과는 없다.
- 비만성 질병의 회복일은 감염 순간 `잠복일+전염일`로 고정된다. 백신 면역은 70, 자연 완치 면역은 80, 치료로 조기 제거 시 35이며 일일 감소량은 각각 고정값이다.
- 따라서 신체 회복과 질병 면역이 합쳐진 것이 아니라, 창립자 특성 공용 효과에 질병 저항·회복·면역 획득·유지 축이 아직 없던 상태다. 네 축을 별도 안정 ID와 실제 도메인 소비자로 추가한다.

## 2026-08-11 Phase 150 질병 세부 능력치 분리 결과

- `character:recovery-speed`는 계속 `CharacterStats.Heal`만 소비하여 체력·상처 회복 전용이다.
- 새 공용 효과는 `character:disease-resistance`, `character:disease-recovery-speed`, `character:immunity-gain`, `character:immunity-retention` 네 개다. 특성·종족·장비·상태·연구가 동일한 `GameplayEffectDefinitionSO`를 참조할 수 있다.
- `CharacterPopulationDiseaseModifierQuery`가 캐릭터의 공용 효과, 종족 환경 감수성, 유전 질병 저항을 한 번만 합성한다. 감염 경로별 어댑터는 더 이상 특성을 직접 읽지 않으므로 콘텐츠 사건과 접촉 감염이 같은 계산을 사용한다.
- 질병 저항은 감수성의 나눗셈 항, 질병 회복은 감염 순간 비만성 전염 기간의 나눗셈 항, 면역 획득은 백신·자연 회복·조기 치료 면역 보상의 곱셈 항, 면역 유지는 일일 감소량의 나눗셈 항이다.
- 감염 시 계산한 `recoveryDay`를 기존 활성 질병 저장 데이터에 확정하여 장비 교체·연구 완료·저장 복원으로 기존 감염 기간을 재굴림하지 않는다. 만성 질환은 계속 `int.MaxValue`이며 명시적 제거만 허용한다.
- 유전 `recovery`는 질병 회복 속도에, `memory`는 면역 유지력에 합성된다. 기존 `all`/`toxin` 유전 저항은 감염 감수성에만 남는다.
- 빠른 회복(207)은 신체 회복 ×1.15와 별도로 네 질병 능력치 ×1.10을 가진다. 100개 창립자 특성 수와 선택 분포는 바뀌지 않았다.

## 2026-08-11 Phase 151 기능·숙련·세부 성능 단일 체계 착수

### 구현 중 확인된 연결 상태

- 기존 구조는 작업 속도·사고·음식 소비·질병·수술·전투가 서로 다른 계산 권위를 사용했다. 단순히 효과 ID가 존재하는 것만으로는 실제 상태 변화 연결을 증명할 수 없었다.
- `CharacterPerformanceQuery`는 N/A 입력을 가중치에서 제외하고 재정규화하며, 필수 기능 10% 미만이면 UI와 AI가 공유할 구조화 실패를 반환한다. 병목 상한은 `0.25 + 0.75 × 기능값`이다.
- 질병 저항과 질병 회복은 같은 회복 수치가 아니다. 정화 처리·활력 반응·동력 순환·자원 효율을 서로 다른 가중치로 사용하며 면역 획득·유지도 별도 공식과 별도 효과 대상을 사용한다.
- 전투의 기존 7개 구형 점수 입력은 근접 명중·근접 위력·원거리 명중·회피·전투 이동·방어 반응 Query 결과로 대체했다. 기존 전투 해석기의 점수 스케일 호환을 위해 공식 계수를 5점 기준 스케일로 변환한다.
- 구형 `CharacterStatType`, 성장·SO·저장·에디터 감사 참조는 아직 다수 남아 있으므로 현재 상태를 구조 전환 완료나 밸런스 완료로 보고할 수 없다.

- `CharacterStatType` 12종은 독립 성장·표시 권위에서 제거됐지만 전투, 수술 UI, 운반, 생산 공정, 야생동물, 침입과 원정이 여전히 호환 투영값을 직접 읽는다. 이 상태는 9종 숙련 단일 권위와 충돌한다.
- 해부학은 8개 `AnatomyFunction`, 5개 행동축, 9개 활동 배율을 이미 계산하지만 일부 소비자는 활동 배율을, 일부는 `Mobility`/`Manipulation`을 직접 곱한다.
- 작업 분류는 정확히 31종이며 현재 작업 정책은 다수가 1배를 반환한다. 숙련 프로필은 별도 경로에서 적용되고 사냥·룬 제작·간수의 보조 숙련 비중은 30~40%라 새 20% 상한과 불일치한다.
- 기존 연결성 감사의 `LiveFileContainsSymbol`은 소비자 심볼 문자열 존재만 검사하므로 의미상 관련된 모든 실행 경로와 실제 상태 변화를 증명하지 못한다.
- Phase 151은 13개 기능, 5개 복합 지표와 작업·전투·의료·생존 결과를 한 Query로 투영하고 구형 12능력치의 선언과 호출을 모두 제거한다.
- 결정론 검증은 저항 2배의 감염 위험 절반, 회복 2배의 저장된 전염 기간 단축, 면역 획득 1.2배의 백신 면역 84, 면역 유지 2배의 일일 감소 0.025, 중립값의 기존 백신 곡선 보존을 확인했다.
# 2026-08-11 Phase 151 live-consumer continuation

- The mana tick already mutates authoritative `CharacterBodyHealthState.currentMana`, but reaches it through the compatibility-style key `arcane:mana-recovery` on `CharacterStats.GetDetailedStatMultiplier`; the combat resolver similarly asks for `arcane:power`. Before changing callers, verify whether those keys are exact adapters to the V27 Query or still an independent mixing path.
- No direct consumer reference was found for the V27 medical formula IDs `performance:medical:treatment-speed`, `performance:medical:surgery-speed`, or `performance:medical:complication-risk`; their live treatment/surgery timing and complication mutation boundaries must be identified and wired explicitly.
- Two initially classified gaps already have live state consumers through stable target adapters: `CharacterAlarmResponseRuntime` changes the due time for pending alarm responses, and `CharacterIdentityDomainAdapters` changes negative relationship-memory duration. Their audit must prove the adapter resolves to the matching V27 formula rather than add a duplicate consumer.
- `CharacterAiNaturalness.ResolveWildlifeThreat` already changes the scan radius through `character:danger-detection`; this is the likely live risk-detection consumer and likewise needs adapter-to-formula verification.
- `CharacterStatsProjectionService.GetDetailedStatMultiplier` only invokes `CharacterGameplayEffectProjector`; it does not evaluate a `CharacterPerformanceFormulaDefinitionSO`. Therefore alarm, risk detection, relationship recovery, arcane power, and mana recovery are real state consumers of trait/equipment effects but are still disconnected from the 13-capacity + proficiency performance formula.
- Treatment efficiency is already a direct Query consumer in `CharacterMedicalRuntime`. `SurgeryWorkExecutionHandler` has a separate speed multiplier boundary; inspect that handler before adding surgery speed to avoid double application.
- Treatment progress currently consumes `performance:work:treat:speed` through `AbilityRescue.GetWorkSpeedMultiplier`; surgery progress consumes `performance:work:surgery:speed` through `ExecutePersistentWorkAmount`. The separately-authored medical treatment/surgery speed formulas duplicate the same capacity and proficiency inputs. Multiplying both would square the doctor contribution, so the catalog/execution-profile mapping must be consolidated or explicitly aliased rather than stacked.
- Surgery success is directly consumed by `SurgeryRiskEvaluator`; complication risk remains absent from the risk breakdown, whose infection/bleeding/organ/death values are currently derived only from failure, facility, procedure, patient instability, and compatibility.
- Work execution formula IDs are generated from `WorkTypeId` inside `CharacterStats`, `WorkAmountCalculator`, and `WorkTaskExecutor`; there is no authored execution-profile-to-formula reference yet. This hard-coded string construction conflicts with the requested SO-owned profile mapping and is the root of the treatment/surgery duplicate-speed ambiguity.
- Meal definitions carry an authored nutrition value and `CharacterConsumablesApplicationPorts.RecoverHunger` is the authoritative need mutation boundary. Nutrition efficiency should multiply this recovered amount there (with an explicit actor Query), not alter item quantity or food-consumption cadence.
- `CharacterMoodPolicyService.Apply` and `ApplySeconds` are the single typed mood-duration mutation boundary. They currently apply only `GameplayEffectTargetIds.NegativeMoodDuration`; replace that projection with the V27 negative-mood-duration formula so mental capacity and status/equipment/trait contributions participate exactly once.
- Relationship memory has two distinct outcomes: negative memory duration (rate controls expiry) and first-apology recovery magnitude. Both currently project only the effect target; both should evaluate the relationship-recovery formula with the matching condition context so the same capacity/proficiency authority governs the result.
- `CharacterConsumablesApplicationPorts` already receives `ICharacterPerformanceQuery`, but treats it as optional and only uses it for food-poisoning projection. It is the correct place to make Query mandatory and apply `performance:survival:nutrition-efficiency` to the authoritative hunger recovery amount.
- Several core consumers still accept Query as an optional constructor dependency. For the migrated channels, absence must throw during construction or evaluation; silently returning the unmodified base value would violate the no-fallback authority rule.
- `CharacterPerformanceQuery` already treats `AccidentRisk`, `Consumption`, and `Exposure` as inverse-capacity channels (`1 / capacityFactor`). Therefore complication risk is a multiplier centered on 1.0: it should multiply infection, bleeding, organ-damage, and death probabilities after the independent surgery-success calculation, never be reused as a success value.
- Formula effects are already projected inside Query. Migrating a consumer from `ProjectDetailedStat`/`GetDetailedStatMultiplier` to Query must remove the old projection call, otherwise trait/equipment effects would apply twice.
- Arcane power, mana recovery, alarm response, and AI risk detection can use `actor.Stats.EvaluatePerformance` directly; their owning runtimes do not need a second Query field. Alarm speed greater than 1 must divide the base delay, while risk detection and arcane power multiply their output and mana recovery multiplies recovered mana per game hour.
- `SurgeryRiskBreakdown` is a serializable runtime result with independent complication probabilities. Add an observable `complicationRiskMultiplier` field and multiply each complication probability by the Query result, clamped per probability; do not change the independently calculated success chance.
- Making Query mandatory exposed editor fixtures that constructed consumables ports or surgery risk evaluators without the authority. Those fixtures must inject a real/fake `ICharacterPerformanceQuery`; production code must not regain an optional fallback merely to keep old tests compiling.
- `CharacterAiEditorTestDependencies.InjectCharacterStats` still constructs `CharacterStats` without a performance Query. This is a legacy fixture gap, not evidence that runtime Query should be optional; focused tests need an explicit deterministic Query fake now, and the shared fixture should later be migrated as part of the remaining optional-dependency audit.
- To resolve duplicate treatment/surgery speed without squaring capacity and proficiency, each formula SO will optionally own an `executionWorkTypeId`. Query will resolve `(workType, result channel)` from authored assets. Treat and Surgery map their speed channel to the medical formulas; the duplicate Work-domain rows remain separate UI/audit definitions but are not stacked. All other work speed/accident channels map to their existing Work-domain formulas.
- This authored mapping also removes C# construction of `performance:work:{suffix}:{channel}` from live execution, satisfying the requirement that profile/formula assignment live in content rather than hard-coded string conventions.

## 2026-08-11 Phase 151 capacity-aware balance and injury closure

- The founder simulator now derives the initial Adventurer anatomy from the authored profile, applies age conditions as the same 5% per-node burden used by the live age adapter, resolves all 13 capacities, and evaluates the selected performance SO's required threshold, weighted contribution and bottleneck cap before proficiency and trait multipliers.
- The corrected 10,000-party no-reroll distribution is 266.508 / 272.746 / 279.774 p10/median/p90 essential-industry WU/day. Best-crafter median is 91.060, best-researcher median 92.367, food-demand median 3.000, and work-accident median 0.324 events/day.
- Accident risk cannot honestly be converted into a fixed two-day WU penalty: the live accident authority stops the current work and applies two damage. It now selects and damages an actual anatomy node, so subsequent capacity/performance changes depend on the struck organ and on real treatment. The focused PlayMode audit forces the path, observes node health loss, restores it, and reports 11 formulas / 12 consumers.
- Disease work loss is already consumed in `CharacterStatsProjectionService` through `IDiseaseSymptomEffectQuery`; severity and target-system-specific symptom multipliers are independent of disease resistance/recovery/immunity. An absolute campaign WU loss still requires authored exposure and treatment policy, so it is not synthesized from an arbitrary epidemic cadence.
- All 125 live operate-capable facilities now have explicit authored proficiency profiles. Runtime command-kind inference was removed; missing mappings fail. The V25 builder audit passes 419 buildings, 125 operations, 354 recipes, 61 equipment and 56 apparel definitions.
- Minimum readiness equipment supply now has an explicit rate/crossover table. At non-zero lower-bound demand windows, supply/demand is 40.216x, 48.436x, 42.121x and 93.481x; the new minimum kits finish at Days 32.238, 122.478, 243.799 and 405.991 respectively.
- `Assets` contains zero declarations, calls, serialized references or UI references to the five legacy stat symbols audited by Phase 151. The stale architecture test was converted from requiring the old stat catalog to requiring its absence and the new performance contracts/query.

## 2026-08-11 Phase 152 14-capacity transition findings

- Foundational `ResourceEfficiency` currently exists in four coupled surfaces: `CharacterFunctionalCapacityId`, `AnatomyFunction.ExpandedFunctions`, `AnatomyHealthSnapshot`, and multiple V27 formula input profiles. It is produced implicitly by circulation/intake organs rather than by a distinct authority, confirming that it is a derived result masquerading as anatomy.
- `MeleePowerInputs` currently substitutes circulation and mobility for raw force, while disease resistance/recovery/immunity all reuse purification, vitality and resource efficiency. This is exactly the coupling the approved `PhysicalPower` and `ImmuneDefense` capacities must remove.
- The existing final result consumers already use named Query formulas for haul capacity, melee power, disease resistance/recovery, immunity gain/retention, nutrition, fatigue and temperature. The correction can therefore preserve runtime consumer APIs and change their authored capacity inputs rather than introduce a second calculation path.
- The 13-capacity count and legacy wording are embedded in builder logs, structural audits, test fixtures and compatibility constructors; every occurrence must be migrated or explicitly retained only as an obsolete pre-V24 compatibility boundary.
- The least disruptive serialized enum transition is to reuse numeric slot 8 for `PhysicalPower`, keep existing precision/mobility/communication/arcane values 9..12 stable, and append `ImmuneDefense=13`. The V27 builder must delete the obsolete `capacity_resource-efficiency.asset` and rewrite every formula asset through Unity MCP before validation.
- `PhysicalPower` should be authored on the strongest mobility/manipulation/power-circulation anatomy node, while `ImmuneDefense` should prefer purification/vitality/power-circulation. Construct anatomy still needs numeric immune defense because its corrosion/contamination condition model is the non-biological equivalent; it should not receive a false biological N/A.
- Runtime consumers do not construct `AnatomyHealthSnapshot` directly except the character/wildlife health rules. Those constructors and the query can change together without adding save fields because node health remains the saved authority.
- Existing work formula generation reuses one capacity profile for speed, accident and applicable quality/yield channels. Force-heavy work therefore received dedicated `ForceWorkInputs`, construction/dismantle received `ConstructionInputs`, and rescue received `RescueInputs`; otherwise adding physical power only to a generic field profile would incorrectly make cleaning and delicate gathering strength-dominated.
- Detrimental result channels (`AccidentRisk`, `Consumption`, `Exposure`) already invert the weighted capacity factor in `CharacterPerformanceQuery`. The new resource-use/thermal/food-safety formulas must author positive bodily capability weights; the Query then turns lower capability into higher food demand, exposure or accident risk without negative weights.
- Directly damaging a selected slime `core` during the focused audit was not a reliable reversible fixture because the legacy/anatomy surface synchronization can normalize that node before the next query. Saved node rejection/mutation/infection burden is the appropriate species-neutral causality stimulus: it changes `FunctionalEfficiency`, survives the anatomy query boundary, and has an exact `TryReduceNodeBurden` rollback for biological and construct profiles.
- Asset regeneration confirms numeric slot 8 is now physical power in every rewritten formula asset and slot 13 is immune defense. No authored formula contains the obsolete resource-efficiency stable ID; resource use remains visible only as final food/nutrition/fatigue/temperature/mana results.
- The final foundational catalog contains exactly 14 assets. Static source audit finds no `CharacterFunctionalCapacityId.ResourceEfficiency`, `AnatomyFunction.ResourceEfficiency`, or ResourceEfficiency formula constant; the sole old stable-ID occurrence is the structural assertion that requires its absence.
- A reversible 20-point anatomy burden is the reliable end-to-end test stimulus: it crosses saved anatomy authority, capacity projection, formula evaluation and named domain consumers without assuming a biological healing path. It proves physical power changes haul/melee results and immune defense changes disease-resistance/immunity-gain results.
- Character detailed-stat UI enumerates the capacity snapshot rather than a fixed row list, so the fourteenth capacity is exposed without a parallel UI catalog. The two-resolution character-summary/medical matrix passed with no captured warnings or errors.
- The isolated official full-world round trip passed 68/68/68 with baseline restoration and canonical comparison. Because capacity snapshots remain derived, adding immune defense introduced no new mutable save field.
- This closes implementation and connectivity, not species balance. Healthy founder throughput remains unchanged by design; species role dominance and injury/disease sensitivity still require the planned quantitative multi-seed comparison.

## 2026-08-11 Phase 153 species allocation findings

- Exact authored species allocation now projects after anatomy and before formulas. Healthy dungeon-species capacity values remain within 0.80~1.25 and each species average remains <=1.05; broad physical species multipliers are absent.
- Equal-skill live results match the intended asymmetric bands. Neutral-fit work assignment is 0.963~1.018 and best representative assignment is 1.075~1.115. Kobold leads three representative roles; no species exceeds the three-role winner cap.
- A role-only Pareto check initially reported Beastkin over Orc. That was an incomplete comparison because species work aptitude is deliberately a growth/AI axis, not an instant speed modifier. Adding all 31 work-growth dimensions removes the false dominance: Orc retains repair aptitude that Beastkin lacks, and the final aptitude-aware Pareto count is zero.
- The 100,000-person actual starting-profile/trait/proficiency sample confirms individual variation exceeds species variation and a non-leading species p90 can beat the leading-species median in every audited role.
- Golem upkeep is an actual physical loop: charge threshold 35, mana crystal reservation, 100 WU progress, +50 charge, cancellation rollback, V3 progress restore, 2.5 anatomy burden per completed 100 WU, and power-core maintenance at integrity 50. The 30-day sufficient-supply result consumes three crystals, one lumber and 326 WU for a net 0.965 human-day ratio.
- Population disease previously reduced performance twice: daily infection burden degraded anatomy while `CharacterStatsProjectionService` also multiplied legacy symptom work/move speed. The direct multipliers were removed, leaving anatomy/capacity as the sole performance path; mood symptoms remain separate.
- The deterministic condition report covers 9x10,000 damage/disease samples and the 16 authored diseases without inventing treatment WU or death rates. An absolute treatment/death audit still requires a live policy-grounded medical schedule.
- The official 68-section strict save regression passes. The broader synchronous final acceptance is 15/33; stale save-version/content expectations and editor fixtures missing mandatory performance Query injection prevent an overall green gate, so balance completion remains blocked.
- The stale broader-regression finding is now resolved. Fresh final acceptance passes 33/33, and the seven-target PlayMode coordinator passes with 32 fresh captures, restored persistence, and Console Warning/Error/Exception/Assert 0/0.
- `OffenseExpeditionRuntime` cached a `BlueprintResearchState` object that becomes stale when research save restore replaces the runtime state. Expedition launch now reads the current `BlueprintResearchRuntime.State`, retaining the isolated-test fallback state only when no live runtime is attached.
- Rune-module tuning correctly requires both the I17 tuning workstation and an operational RF97 resonance-tuning support facility. The equipment/expedition UI matrix now authors both facilities instead of weakening the production requirement.
- Rest has no work-speed formula by design. AI work-target scoring now assigns rest a neutral efficiency score and leaves sleep/fatigue/wound recovery to their dedicated result channels, preventing `work:rest` from entering the 30 non-rest speed formulas.
- The remaining Phase 153 evidence gap is absolute treatment WU/recovery/death under a live medical scheduling policy. Static condition modifiers are insufficient to invent those values, so species balance remains below final-complete status despite the green implementation and regression gates.

## 2026-08-11 Phase 154 tavern physical recreation findings

- Food quality and substance mood were already separate live authorities. Meals recover authored nutrition and can produce meal-quality mood; recreational beverages already own duration, mood, tolerance, addiction, overdose, work-speed and combat modifiers.
- D12 was incorrectly authored as a meal facility with free hunger, mood and fun recovery. Because the meal interaction accepts only `FoodItemFeature`, it could not consume alcohol's `SubstanceItemFeature`; autonomous drinking instead consumed global stock without requiring the venue.
- D12 is now an entertainment venue with an authored recreational-substance service. A successful visit consumes exactly one allowed beverage from `facility-input:recreation-substance:{facilityId}`, applies the existing beverage runtime once, then adds venue fun and social/facility memory. It adds no nutrition and no second mood bonus.
- Stored or loose beverage stock can create a delivery request but cannot be consumed remotely. Policy rejection and missing local stock leave the physical stack unchanged. Automatic recreational use prefers the nearest eligible venue and keeps direct pickup only as a no-venue fallback.
- The focused deterministic audit proves physical stock `1->0`, active substance/work penalty, fun `+8`, and forbidden-policy stock preservation. The content rebuild, synchronous final acceptance 33/33, and PlayMode acceptance 7/7 with 32 fresh captures all pass; Unity Console Warning/Error/Exception/Assert is 0/0.
- During content regeneration, the broad modular-facility builder rewrote dependent facility assets. The resource-economy, combat-equipment, faction-service, research and survival patch builders were rerun in dependency order, restoring all 33 acceptance gates before final verification.

## 2026-08-11 Phase 155 daily schedule framing

- The next bottom-up balance layer is a daily clock budget, not another direct reduction applied to the existing 99-WU figure. First determine whether 99 WU is authored gross work capacity or already embeds time spent on survival actions.
- Technology affects three independent axes: visit frequency through need recovery/consumption efficiency, visit duration through service speed, and productive output through work/automation throughput. Collapsing them into one efficiency multiplier would double-count technologies that improve more than one surface.
- Movement and queueing must be explicit because better facilities can reduce visit count while centralized high-tier facilities can increase travel or congestion. Research itself is an opportunity cost and its benefit begins only after the actual unlock is completed.
- The baseline calls 99 WU a `planned` adult workday, and the only shared C# constant found so far is a faction-contract denominator. It is not evidence that the live 24-hour AI schedule can actually deliver 99 active WU after needs.
- An earlier measured work-availability ratio in the founder findings reduced the authored 99-WU ceiling to 73.935 base WU/person/day before proficiency and trait speed. That is a useful cross-check, but it must be reconstructed from the current live need/action timings rather than adopted as an unexplained 74.68% constant.
- `NeedBalanceCalibrationScenario` is the existing deterministic schedule authority candidate: its source exposes work-time drain fields for sleep, hunger/thirst, excretion and hygiene and already simulates agent metrics. Reuse/extend this path rather than creating a second spreadsheet-only need model.
- The current standard-sufficient calibration reports work 74.6814%, life 23.2364% and queue 2.0801% of a 180-second day. Taken directly, that is about 134.43 active work seconds/WU per neutral person before performance multipliers.
- The baseline 99 WU is independently defined as 55% of the same 180-second day. Multiplying 99 by the 74.6814% schedule ratio discounts daily availability twice. Phase 155 must replace the coarse 55% planning factor with a complete schedule-derived factor, or explicitly retain 55% as a separate off-duty/player-allocation envelope; it cannot treat both as need loss.
- The existing calibration omits the FUN/recreation need and uses synthetic queue/action times. Its 134.43 WU is therefore a diagnostic upper bound, not the final daily labor result the user requested.

### Phase 155 research and facility checkpoint audit

- The first service facilities are not research-gated by their current `unlocked` flag; their `unlockPhase` and construction WU still create progression costs. The audited baseline facilities use a common authored interaction duration of 1.5 seconds, while their capacities differ (mostly 1, with selected shared rooms at 2). A schedule checkpoint must therefore name a facility/layout package and pay its construction WU rather than treating a technology label as a free multiplier.
- The key late service checkpoint is `research:service-automation` (720 research WU). It requires `research:bath-business` (92), `research:hospitality-operations` (92), `research:industry:steam-power` (980), and `research:medical-reception` (92), plus the prerequisites of those projects. Its direct prerequisite subtotal alone is 1,976 WU before deeper prerequisite research and facility construction.
- Service automation unlocks SR05 heated serving table (building 1704, service work-speed x1.35), SR06 automated checkout (1705, x1.50), species seating SR07-SR09 (1706-1708), SR15 automatic room assignment (1714, x1.35), and SR16 automatic water/sewage control (1715, x1.35). These are support multipliers, not universal reductions in a resident's own meal/sleep/toilet duration; each benefit must be mapped to the matching service phase and actual installed support facility.
- The baseline survival/leisure buildings themselves carry non-trivial construction work: examples include D01 128 WU, D02/D04 168, D12 128, H01 128, H03 160, H04 208, R01/R02/R03/R04 168, Q01 168, Q02 468, and T01-T03 128. Technology schedules must report these one-time costs separately from the recurring daily time saved.
- Existing food nutrition spans roughly 34-36 for preserved/soup food, 38-42 for roots/roasts, 44-47 for eggs/garden/cheese, 50-52 for stew/pie, and 58-60 for lavish meals. Against neutral hunger depletion 50/day, food progression changes physical meal frequency from roughly 1.47/day at nutrition 34 to 0.83/day at nutrition 60 before species/trait modifiers; it does not directly shorten the common interaction duration.
- Recursive prerequisite closure gives usable checkpoint costs instead of the misleading direct-project costs: an early package of survival preservation + service flow + crop cuisine is 320 research WU; the lavish-cuisine closure is 3,552 research WU; lavish cuisine + service automation + powered tools is 6,540 research WU; adding automatic sanitation raises the union to 13,660 research WU. These totals still exclude building BOM/construction and recurring power/fuel.
- The current calibration's `lifeSeconds` is not a facility-derived quantity. It hard-codes hunger 8 s, thirst 5 s, sleep 18 s, toilet 7 s and hygiene 10 s, then adds a synthetic normal queue of 0.5-1.5 s per visit. Its reported 74.6814% work ratio therefore proves only that the chosen synthetic scenario is internally stable; it cannot authorize facility service time, layout travel, recreation, or a realistic sleep block.
- The actual neutral movement baseline is `CharacterSpeedType.Normal = 4`, projected as `4 / 3.5 = 1.142857` world cells per second before anatomy, fatigue, equipment and terrain. A three-cell one-way reference leg therefore costs 2.625 seconds. Daily layout loss must count path legs explicitly and cannot use a percentage detached from movement speed.

### Phase 155 candidate schedule values (pending user approval)

- A coherent replacement for the old 99-WU assumption is a four-stage active-time curve of approximately 82.21 / 90.35 / 100.21 / 111.36 seconds per neutral healthy adult per day. The stages use 8.0 / 7.5 / 7.0 / 6.5 hours of sleep, progressively denser layouts, lower queues and better food/sanitation/leisure service. This deliberately places the historical 99 WU near the mid-infrastructure checkpoint rather than at day-zero.
- Candidate neutral need contract: hunger 50/day with 65/35/75 thresholds; thirst 60/day with 60/35/75; sleep becomes awake-time/circadian demand with 60/30/90 and a real sleep-duration block; excretion remains 24/day + 0.05/active-work-second with 45/25/70; hygiene remains 18/day + 0.06/active-work-second with 40/20/65; FUN gains 16/day with 45/25/70. FUN and meal/substance mood remain separate authorities.
- Candidate T0 service assumptions are nutrition 36, water recovery 65, one 60-second sleep block, meal 4 s, drink 1.5 s, toilet 2.5 s, hygiene 4 s and recreation 8 s. The long-run visit rates solve to about 1.389 meals, 0.923 drinks, 1 sleep, 0.38 toilet, 0.38 hygiene and 0.889 recreation sessions/day.
- Candidate reference layout T0 is 3 cells per one-way leg at 1.142857 cells/s, with `1.4 + 1.15 x daily visits` legs after deterministic need-action chaining and 0.55 s expected queue per visit for a three-founder, capacity-one service cluster. This yields 76.47 s service, 18.60 s travel, 2.72 s queue and 82.21 s active work from the 180-second day.
- Purely scaling the existing Adventurer founder speed distribution by the candidate active-time curve gives provisional party p10/median/p90: T0 221.31/226.49/232.33, T1 243.22/248.92/255.33, T2 269.77/276.08/283.19, T3 299.78/306.80/314.70 WU/day. This is not the final audit because individual food-use, movement, species and conditional trait vectors must be applied per candidate before taking quantiles.
- Current founder generation explicitly fixes `speciesTag=Adventurer`; the earlier 10,000-party report does not sample dungeon-species distribution. Species variation can be reported for later residents, but applying a species distribution to the first three founders would be a separate founder-generation design change.

### Phase 155 live primitive audit

- The authored day is 180 real seconds. Neutral daily depletion is hunger 50, thirst 60, excretion 24 and hygiene 18. Work additionally drains sleep 0.35/s, excretion 0.05/s and hygiene 0.06/s.
- Routine/emergency/resume thresholds are hunger 65/35/75, thirst 60/35/75, sleep 60/30/70, excretion 45/25/70 and hygiene 40/20/65.
- FUN is absent from `SurvivalBalanceSettingsSO` and `CharacterNeedStateService.CalculateTimedDecay`; current staff fun can influence AI and be recovered by facilities, but it has no canonical daily depletion/response contract. A complete daily schedule must add one instead of estimating recreation from mood.
- Sleep has no passive daily depletion in the canonical table; it is drained by active work only. Hunger/thirst have only timed depletion, while excretion/hygiene have both timed and work depletion. This distinction must remain visible in the schedule equations.
- Authored modular recovery values are: D01 hunger 35, D02 48, D04 25; R01 sleep 35, R02 52, R03 42, R04 sleep 25/fun 8; H01 excretion 75, H03 hygiene 62, H04 sleep 12/hygiene 85; Q01/Q02 fun 10/12; T01/T02/T03 fun 18/16/12; D12 fun 8 after physical drink consumption. Mood bonuses are separate and must not count as need recovery time.
- Live `Facility.Interact` already owns a real service-duration boundary: a service-session contract can supply waiting/service/payment/cleanup seconds, otherwise `FacilityData.useDuration` is used. The current calibration's 8/5/18/7/10-second action constants bypass this authored boundary and therefore cannot measure technology-dependent facilities.
- The relevant base assets inspected so far all author `useDuration=1.5s`. Capacity is 1 for R01/R02/H01/H03/H04/D12/Q01/Q02/T01/T02/T03, while R03 and R04 have capacity 2. If accepted unchanged, recovery magnitude—not duration—is currently the main tier difference, and every facility tier needs a real capacity/queue comparison.
- Routine AI starts a need action at the authored routine threshold and considers it satisfied at the resume target. Workers cannot select leisure while on duty; leisure is allowed only during off-duty time. Therefore the work-duty schedule is a mandatory lower-level input, not optional flavor.
- Base recovery and capacity imply very different repeat cadence: R01 +35/cap1, R02 +52/cap1, R03 +42/cap2; H01 +75/cap1; H03 +62/cap1; H04 +85/cap1. These tier differences can reduce visit count or queue load even though every use currently lasts 1.5 seconds.
- Work has no universal authored fixed shift. Normal non-owner workers enter off duty when sleep <=30, mood <=25, excretion <=25 or hygiene <=20, and return after at least 8 seconds when sleep >=70, mood >=45, excretion >=55 and hygiene >=45. Owners bypass this off-duty transition. Operate/guard alone have a 45-second routine rotation plus 4-second cooldown.
- The live work loop applies canonical work depletion once per real second: sleep -0.35, excretion -0.05 and hygiene -0.06, with fatigue/species modifiers on sleep. It also applies a repeating work-fatigue mood factor. The serialized `sleepDrainPerWorkTick=3` is not the consumed live authority in this loop.
- Leisure selection is gated by off-duty state, so adding FUN depletion without a deliberate off-duty policy could leave on-duty workers unable to respond until another need or mood forces off duty. The Phase 155 proposal must define scheduled personal time or explicitly allow routine recreation interrupts.
- Live movement time is physical path distance divided by the character's evaluated movement speed, with terrain, anatomy, proficiency/effects, deprivation and equipment burden included. There is no universal hard-coded seconds-per-tile constant to reuse; the balance model needs an explicit reference layout/path-length distribution and must evaluate the same movement formula.
- `AbilitySchedule` currently initializes all 24 hours to WORK and exposes no authored schedule setter. It does not provide a sleep/free/work timetable. Consequently the present daily loop is need-threshold-driven continuous labor, not a designed shift system.
- Neutral archetype movement speed is derived from `CharacterSpeedType`; normal speed is 1 world unit/second (`3.5/3.5`). For a neutral unobstructed reference layout, one unit/tile of path is approximately one real second before the live movement formula's capacity, fatigue, terrain and equipment modifiers.
- Mood is recalculated as base mood plus authored need-band factors and timed interaction memories. FUN must remain a need feeding mood/AI; it must not be replaced by a direct daily mood tax or counted twice with drink/meal mood memories.
- Worker initial needs are hunger 80, thirst 85, sleep 85, fun 70, excretion 85 and hygiene 80. FUN is a real leisure/mood/director need with low/critical mood penalties -6/-12 and high bonus +4, but it is missing only from the depletion/response table; this is a concrete incomplete authority, not an optional new system.
- All six need mood profiles use critical <=15, low <=35 and high >=85. The schedule should normally service needs near routine thresholds and avoid relying on these mood penalty bands; mood-driven lost WU belongs in failure/tail scenarios, not the neutral baseline.
- Authored food nutrition spans a meaningful tech ladder: preserved ration/mushroom soup about 34-36, roasted/root meals 38-42, egg/garden/cheese meals 44-47, boar stew/meat pie 50-52 and lavish meals 58-60. Since neutral hunger drains 50/day, food progression should primarily reduce meals/day through nutrition, not shorten the fixed chewing/service animation unless a separate service technology says so.
- The existing preferred-water relief restores thirst by 65 while neutral thirst drains 60/day, giving about 0.92 successful drinks/day at steady state before species/pressure effects. The calibration's five-day 1.07/day includes initialization/transient effects and should not be treated as the asymptotic cadence.

## 2026-08-11 Phase 156 quantity-lease and meal-logistics recovery

- The approved Phase 156 design is below the daily WU calculation: meal cadence, physical food availability, route/queue time and spoilage cannot be measured honestly while one actor still locks an entire item stack.
- Repository search found no existing `ItemQuantityLease`, `IItemQuantityReservationService`, `CharacterMealPlan`, `BufferAggregationKey`, reservation-claim hint or meal quality/serving-role implementation. The existing `ReservedQuantity` hits belong to economy demand counters, not physical stack ownership.
- The current physical-item authority therefore still requires a new slice-based ledger and staged migration. Existing whole-stack APIs must be wrapped rather than deleted in one step so overlapping user work remains compilable while consumers move.
- Required ordering is fixed as ledger/snapshot -> extraction -> aggregation -> consumers -> meal transaction -> persistence/grandfathering -> diagnostics/audits. Phase 155 net-WU recalculation follows only after the live meal path is stable.
- The physical stack authority now exposes total/reserved/available quantities and no live availability path uses `ReservedByPersistentId`; the legacy field is emitted empty and rejected when non-empty in a new save.
- The quantity ledger owns deterministic per-stack totals, operation and slice indices, exact single/batch reservation, TTL revalidation, renewal, release, mutation and Grandfather claim restoration. Reservation itself creates no child entity.
- The first Unity-MCP contract run proves 10 one-unit leases on quantity 10, the 11th rejection, atomic batch rollback, 10 -> 9+1 extraction conservation, 100 carried children aggregating to 75+25, and two shared-stack Grandfather claims restoring without ownership change.
- A compatibility defect was found after the first pass: the old wrapper authored a random internal owner-operation ID and reserved whole stacks, so exact consumers could not find their leases. The wrapper now supports exact `ReservedItemConsumption` quantities and uses the domain operation ID; trait analysis, Golem recharge, reproduction, festivals and V20 content resolution use that path.
- Food content now has explicit quality band, serving role, nutrition, mood and freshness. Automatic meal choice uses available quantity, base mood excluding the active meal memory, Fine-by-default/Lavish-explicit policy, snack follow-up cooldown, seat-plus-quantity reservation, freshness ETA rejection and begin/commit physical validation.
- Remaining implementation gaps are explicit rather than hidden: precise region/A* routing is still a Manhattan bounded candidate heuristic; restore has exact claim reconstruction but no world-level scheduler gate/mutation barrier evidence yet; the 64-per-tick deferred aggregation queue, diagnostics and full profiler/PlayMode evidence also remain open.

## 2026-08-11 Phase 156 active transport ownership and aggregation findings

- The first partial-extraction implementation consumed the Lease at pickup. That conserved quantity but broke the approved ownership model because a carried child and later canonical buffer stack had no active Slice to retarget or grandfather through save/load.
- Carried quantities are now physical `WorldItemStackRecord` entries in `Carried/InTransit` state; the character carry inventory is their manifest. The quantity ledger moves its Slice from source to carried ID atomically and the world persistence snapshot therefore has a concrete preferred physical stack for ownership hints.
- Buffer aggregation can accept compatible targets that already contain other Lease quantities. It transfers quantity inside a rollback-capable transaction, retargets every affected Slice to the deterministic target packing, then removes the empty transport child. A failed retarget restores source/target quantities, components and transport state.
- Deterministic evidence: one 10-unit source becomes source 9 plus carried 1, then canonical buffer 6 with reserved 1, then exact consume returns buffer 5. In the 100-owner case, `MaxStack=75` produces 75/25 physical packing with 100 active claims and no transport dust after completion.
- The carried-save test initially failed because the Editor factory omitted `IItemQuantityReservationPersistence`, unlike production DI. Wiring the same port made claim hints and Grandfather restore observable in tests; the restored owner, carried stack ID, quantity and source remainder are unchanged.

## 2026-08-12 Phase 157 recovery and implementation boundary

- The approved WU plan is downstream of Phase 156's physical meal/logistics timings but introduces independent runtime authorities for labor accounting, project concurrency and threat response. It must not be implemented as one settlement-wide productivity multiplier.
- `WorkTypeCatalog` is the stable 31-work definition authority and currently has no emergency classification. `WorkTaskExecutor` is the central live progress/completion path, so ledger integration belongs there rather than in UI or AI scoring.
- Existing event-alert history is presentation state, not a threat-response authority. The new incident/alert service must own active incident IDs, desired/committed levels, hysteresis and epochs; presentation may project it but may not become authoritative.
- The save registry is fixed at 68 strict sections. Phase 157 should extend an appropriate existing section or existing runtime save payload rather than adding a 69th section unless the global save contract is deliberately revised and all official evidence is updated.
- Available tools expose the running-game `dungeon_player` MCP but currently do not expose Unity editor command, Console or PlayMode control. Source implementation can continue; Unity asset regeneration and validation must wait for an editor MCP capability rather than using shell or mouse automation.

## 2026-08-12 Phase 157 first vertical-slice findings

- The live executor has several work-loop variants, but all approved WU converges on `RecordApprovedWork`; connecting accounting there covers normal, persistent, work-order, restock and instant-debug progress without estimating output from AI intent.
- A reserve contribution cannot use the full remaining WU of a landmark or research order. The ledger clamps it to the approved 30-WU response window and tracks actual remaining work separately, preventing thousand-WU projects from manufacturing reserve capacity.
- Completion-token idempotence itself can become an unbounded memory leak over 960 days. The runtime retains only the most recent 4,096 tokens; older duplicate calls encounter a missing operation and cannot subtract twice.
- Existing event-alert history is preserved as presentation state. Threat state is appended to the same strict save section only to retain the 68-section contract; its runtime authority remains `SettlementAlertRuntime`, not the alert log.
- Invasion and body-health events provide the first real threat consumers. Repeated signals update one incident ID by monotonic revision, so sensor/event duplication changes severity without creating duplicate response jobs or incident counts.
- Runtime stage-level interruption still needs an explicit suspension transaction. Calling the existing abort path would release reservations and mark a safe emergency pause as failure, so it is intentionally not used as a fake implementation of Lease-preserving suspension.
- Productive reserve population is now derived from living, non-downed adult-or-elder character authority. Raw resident count is not valid emergency labor because children and downed residents cannot supply WU.
- The reserve target uses `max(12 WU, productive adults * 3 WU, highest authored P90 risk WU)` before the alert multiplier. Disaster costs are not guessed inside the planner; fire, medical, defense and breakdown domains must publish their P90 requirement.
- The previous alarm entry point listened to presentation logs and immediately released AI reservations. It now listens only to committed alert transitions. Running work is not destructively replanned until suspended-work persistence can preserve BOM, quantity Lease and facility state.
- Daily settlement labor now has separate fixed-point actual, process-output, domain-automation, loss, essential-maintenance and facility-maintenance channels. The saved 30-day per-capita median prevents reload from resetting progression evidence.
- Sovereignty no longer requires 20 residents. The temporal ending no longer requires 60 residents and instead accumulates a saved 120-day per-capita 2.00 / reserve coverage 1.20 streak. Monster Accord still needs real culture-acceptance and service-index producers; substituting unrelated WU would be invalid.
- The execution audit found two genuine accounting bypass families: captivity/performance sessions and four repair-domain loops calculated work directly. `WorkExecutionContext.RecordApprovedWork` now makes domain-owned progress report its accepted delta through the same proficiency, accident, emergency-ledger and settlement-labor gate as central work loops.
- Safe checkpoint suspension now covers external work orders, persistent progress callbacks, captivity interactions, performance preparation, structural repair, defense maintenance, equipment repair and automation maintenance. Local one-shot loops intentionally finish because their local progress is not save-authoritative; cancelling them would lose work.
- Automatic production WU is measured from authoritative production-bill progress before and after execution. A completed/reset bill credits only its pre-call remaining WU, preventing the final automation tick from over-reporting requested work.
- Alert `Capture()` previously rebuilt and sorted two lists on every read. It now caches collection-backed snapshots until an incident, coverage, timer, epoch or suspended-work mutation occurs, so repeated hot-path reads reuse the same collection payload.
- A Red-to-Amber downgrade could occur while a worker was waiting for the next safe checkpoint. Without cancellation, that stale request would suspend the worker after Red had already ended. Amber transition handling now cancels uncommitted suspension requests and preserves/journals only receipts that already reached a safe checkpoint.
- The current world has no authoritative settlement fire-spread runtime, so a live fire P90 producer cannot be truthfully attached yet. Invasion and medical P90 producers are live; equipment-breakdown/fire forecasts remain explicit incomplete work rather than invented constants masquerading as physical state.
- Unity MCP's `RegisterEntryPoint` compile failure was not a VContainer version mismatch: the new registration file alone omitted `using VContainer.Unity;`, while the established registration files import it.
- `BuiltInWorkTypeIds` names the canonical construction work ID `Construct`; Editor validation must use the same 31-work catalog instead of introducing a test-only `Construction` alias.
- The focused Phase 157 scenario now executes successfully in the live editor. This establishes structure/formula/save-state evidence, but does not yet prove ordinary facility construction concurrency UI, item-Lease preservation during suspension, or the multi-stage WU distribution.
- `ConstructionSite` and the shared building reservation boundary were strictly single-worker, so merely adding `ProjectWorkforceRuntime` to research/grand projects could never make Small/Medium/Industrial facility caps live. Parallel reservation must be opt-in on construction sites so ordinary facilities remain single-worker.
- Actual worker labor and accepted project progress must not use the same diminished value. Construction now records raw accepted labor WU while applying the worker-order contribution curve only to order progress; otherwise the 6th-8th worker would falsely appear to have worked less time.
- Running the 104-asset modular builder as one MCP command completes the physical imports but can exceed the MCP server's 300-second response timeout and leave the existing connection queue unresponsive. Future regeneration should use bounded builder batches or a request-file runner rather than one monolithic `Unity_RunCommand` call.
- Parallel capacity in `ConstructionSite` is insufficient if the upstream scheduler only recognizes an unstarted `Ready` order. Automatic fill must count both active workers and outstanding reservations, and continue requesting workers for `InProgress` construction until the authored automatic limit is reached; otherwise the live behavior silently remains single-worker despite a correct project curve.

## 2026-08-12 Phase 157 live daily-routine findings

- Construction does not execute through the generic `CheckActionWork` loop. Work-order, amount and persistent-amount loops each needed the same elapsed-game-time need depletion and routine interruption gate; applying depletion per real-time polling tick was incorrect under accelerated game time.
- A global “any routine need blocks all work” predicate collapses labor to zero because excretion and hygiene enter their routine bands well before they must preempt all activity. The correct boundary is a latch: only the need that actually interrupted work blocks re-entry, and only until that need reaches its authored `resumeTarget`.
- Marking `isBestActionEnd=true` at the end of shopping is insufficient. Shopping completion must guard against stale actions and explicitly request an immediate replan after releasing its reservation.
- The adaptive AI budget reached zero under the accelerated live fixture, leaving 73 starved decisions despite only three registered actors. A minimum progress floor of one decision and one path search per frame reduced starvation to one without removing the existing upper/time budgets.
- Warm-up and measured-day project authority must be separated. Recreating the order in the same frame as stopping the old work races the project-lease cleanup and produces `construction-workforce-blocked`; stop, wait for lease disposal, then cancel/recreate/activate.
- The latest valid live evidence before fresh-order reset showed all three founders entering construction and physical water consumption reaching 3/3. Meal completion remained 1-2/3 and central measured labor remained unproven because warm-up work contaminated the project window.
- A one-day sample was too sensitive to AI assignment and need-cycle phase. The accepted fixture now uses a 130-second warm-up plus five complete 180-second days and accumulates labor snapshots across all five daily accounting resets.
- Preserved-ration content currently authors 36 nutrition, not the planned 40. Starting hunger at 85 yields a finite-window expectation of about 1.2 meals per actor-day; the final live result was 1.133. Fixed `0.75~1.25` meal assertions are therefore invalid unless the fixture also fixes nutrition at 50.
- Safe drinking from a physical item stack and a water facility recovered thirst but did not publish `CharacterWaterConsumedEvent`; only world-source drinking did. All three safe-drink kinds now publish through one helper after successful physical consumption/use, allowing population-health and verifier consumers to observe the same authority.
- Raw project labor and project output are not interchangeable under the contribution curve. `WorkTaskExecutor` now records the raw-to-project contribution gap as loss, and the verifier compares physical progress with output-equivalent WU. Five-day rounding drift between float progress and per-tick milli-WU was 0.006 WU, so the causal audit uses an explicit 0.01 WU tolerance.
- Final five-day evidence passes, but it rejects the historical 99-WU assumption as a live result: three actors produced 73.419 WU across 15 actor-days, or 4.895 WU per actor-day. Each actor spent 510-599 of 900 measured seconds in idle/other; this is now the next bottom-up balance/debug boundary rather than a value to hide with a multiplier.
- The five-day fixture is not a new-run survival proof. `PreparedStartPartyGameplayApplier` gives three founders category stock `Food 15 / Water 15 / General 40 / Fuel 10 / Medicine 5`, a sewing kit, thread 12, mending scrap 6 and basic apparel, then calls `EnsureStarterDungeonShell`.
- The fallback starter shell creates only a two-cell-high hallway strip, one door and a right wall. It does not place a bed, meal facility, toilet, wash facility, recreation facility, well, warehouse or production station. `HasUsableStarterDungeon` even requires some pre-existing non-wall/door interior content before it considers the scene already usable, so new-run viability currently depends on scene-authored content or facility-free emergency actions rather than on the shell itself.
- Survival does contain facility-free breakdown fallbacks: the breakdown runner has separate emergency paths for eating physical food, drinking, sleeping, outdoor waste and other deprivation actions. These are last-resort consequences rather than a proven onboarding loop; their timing, resource selection and mood/health cost must be exercised from the exact starter state.
- The normal AI service path still explicitly searches Meal, Toilet and Hygiene facility roles. The cheapest authored examples exist (D01 simple hearth, H01 toilet, H03 sink), but no such facility is placed by `EnsureStarterDungeonShell`. A starting run therefore needs either player construction before routine thresholds or reliable fallback behavior until those projects complete.
- Starter category stock is resolved by lexicographically first stackable item ID in each category, not by an explicit onboarding loadout. This is fragile: adding an earlier-sorting item can silently change the actual starting food, water, material, fuel or medicine while `StarterSupplies` still reports only categories.
- Facility-free emergency outcomes are survivable but costly rather than equivalent facilities: floor collapse restores only 35 sleep and gives mood -5; an outdoor accident restores 90 excretion but removes 25 hygiene and creates waste/stain; unsafe emergency food can damage/infect; desperate water can fall through to contaminated sources or a self-harming taboo path. These are a death-spiral backstop, not a balanced opening routine.
- The minimum facility set implied by normal needs is at least D01 meal, R01 bed, H01 toilet and H03 sink, plus some recreation source if mood recovery is meant to function normally. None is included in the fallback shell.
- Authored construction requirements make the gap concrete. D01 needs 130 WU plus stone block 6 and iron ingot 2; each capacity-one R01 bed is about 168 WU plus lumber 5 and cloth 3; H01 needs 130 WU plus stone block 4 and iron ingot 2; H03 needs 160 WU plus stone block 6 and iron ingot 3 because its Hygiene trait adds infrastructure material on top of the sink form. A three-person normal-service set therefore needs D01 + three R01 beds + H01 + H03: about 924 construction WU and at least stone 16 / iron 7 / lumber 15 / cloth 9 before hauling and room work.
- At the accepted live rate of 4.895 WU per actor-day, 924 WU would take roughly 63 calendar days for the three-person party even if all founders could continuously contribute and all exact materials already existed. At the unproven authored 99-WU target it would still take about 3.1 calendar days for three workers. This enormous difference is why a true-start survival trace must precede technology balance.
- Unity MCP confirmed the lexicographic category bug is active, not hypothetical. The actual starting stacks are `Food 15 = craft:fermented-vinegar`, `Water 15 = resource:clean-water`, `General 40 = captivity:restraints`, `Fuel 10 = craft:candle`, and `Medicine 5 = craft:resin-balm`. The opening receives no stone, iron, lumber or cloth from `StarterSupplies`, and the Food category resolves to vinegar rather than a full meal. Under the intended empty-dungeon start this loadout cannot construct the minimum service set and is not a credible survival package.
- Two Unity MCP inspection commands failed before the untyped `SerializedObject` audit succeeded: dynamic commands cannot reference Sirenix-derived `ItemDefinitionSO`/`BuildingSO` without the Sirenix assembly, and the command rewriter moved a private nested helper class to namespace scope. Both failures were compile-only and changed no project state.
- `craft:fermented-vinegar` has no `FoodItemFeature`; it is a research-gated production intermediate (`research:cuisine:fermentation`) with market and production features only. `food:preserved-ration` is the actual preserved edible item (nutrition 36, freshness 1800, max stack 60), but the current lexicographic category grant does not select it. Normal meal planning therefore sees zero edible starter meals even though the UI/category total can say Food 15.
- The deprivation breakdown path ranks any `StockCategory.Food` item as edible even without `FoodItemFeature`, then restores 55 hunger and treats it as safe unless production tags mark it spoiled/forbidden. Consequently the founders may technically consume fermentation vinegar during a desperate-eating breakdown. This hides the starter-loadout bug behind an absurd emergency behavior instead of providing normal meals.
- With 15 vinegar and 15 clean water split across three founders, the stock is only five emergency servings/drinks each. At roughly 50 hunger loss per day and 65 thirst recovery per drink, the party has on the order of five to six days to establish a real food/water loop. Since the granted General stack is restraints rather than construction material, that deadline depends entirely on immediate gathering and conversion from the map and is not guaranteed by the starter package.

## 2026-08-12 true-start primitive survival implementation findings

- The explicit starter loadout and primitive survival runners are present, but the first live five-day verifier mixed two different claims: natural survival cadence and forced proof that every primitive action fires a fixed number of times. Its `field meal >=12`, `floor rest >=6` and `latrine >=6` assertions are not derivable from the authored need decay/recovery values.
- The starter item transfer exposed a real conservation bug: carried inventory was removed while a new Stored stack was spawned and the original physical Carried record survived. Preserving the globally unique carried-stack identity through deposit fixed the duplication; the latest live trace never exceeds the starter `24` rations or `30` water.
- The global facility-role cache can report `Meal, Purchase, Rest, Training, Research, Mana, Logistics` while an immediate authoritative candidate query reports zero starter service facilities. A role-presence bit is therefore too coarse to suppress a primitive action.
- Removing that coarse gate makes primitive actions eligible at their need thresholds, but natural AI still selects normal warehouse eating and fails to select latrine/hygiene before need collapse. The remaining cause is the survival replan/priority path, not action registration: live diagnostics show the primitive assets registered, `CanStart=true` and score `0.65` at the relevant thresholds.
- `CharacterAiDecisionPipeline.ShouldInterruptForSafeEmergencyRelief` currently interrupts a locked action only for emergency thirst. Hunger, sleep, excretion and hygiene can remain behind ordinary action commitments until a breakdown, contradicting the unified survival-need priority expected by the daily-WU model.

## 2026-08-12 true-start primitive survival closure findings

- The locked-action survival interrupt now covers hunger, sleep, excretion and hygiene when an actor-usable facility or a physically executable primitive action exists; thirst retains its dedicated safe-drink path.
- Primitive actions keep a `0.65` routine fallback score, rise to `1.00` only at their authored emergency threshold, and are ordered before the same-branch facility action for deterministic emergency tie-breaking. Proper facilities can still win routine selection through their higher scored service path.
- The independent natural five-day PlayMode gate passes: all three founders finish at `100` health with no active breakdown, consume `9` physical rations and `8` clean water, and never exceed the initial `24` ration / `30` water totals.
- The independent focused gate passes all four AI paths. Field meal consumes one ration, floor rest restores sleep without an item, primitive latrine restores excretion without an item, and bucket wash records exactly one clean-water cost in its completion event. World water can fall by more than one during this long focused trace because background founders still drink; the action-local event is the exact cost authority while the world total remains monotonic.
- Final Unity console after the natural run is Warning/Error `0/0`. Evidence: `Artifacts/QA/primitive-start-survival-5day-report.txt` and `Artifacts/QA/primitive-survival-focused-report.txt`.
- This closes the true-start survival transition, not Phase 157 net-WU balance. Technology-stage WU must now be recalculated with the observed primitive meal/drink/sanitation cadence.

## 2026-08-12 Phase 157 neutral-facility five-day recovery

- The latest neutral-facility five-day trace is not a completion result. It records actual/output-equivalent labor `192.172/177.592 WU`, or `12.811/11.839 WU per actor-day`, against the authored neutral target of `99 WU per actor-day`.
- Toilet and hygiene cadence now reaches `0.600` and `0.733` uses per actor-day and no primitive action leaks into a facility-present scenario, but recreation completes only `3` times across `15` actor-days (`0.200/day`).
- The live recreation action itself is executable: the preflight evaluates a valid entertainment destination and a nonzero utility. The remaining first-order defect is branch priority: `CharacterAiDecisionPipeline.GetGroupPriorityMultiplier` maps shopping/look-around to `LeisureVisit` but omits `CharacterAiBranch.LeisureVisit`, so the recreation job is multiplied by Idle priority.
- The shared incremental action selector previously kept only one predicate continuation. Root job-giver alternation discarded later predicates before they could finish, preventing recreation and reducing multi-worker construction access. It now keeps one continuation per predicate/method/context and the live recreation preflight resolves a valid QA sofa.
- This remains `밸런스 기준 배정/연결 검증 진행 중`, not a simulation-complete WU balance. Recreation priority/cadence, compact fixture travel, idle decomposition, item consumption consistency and project concurrency evidence remain open.
- Source confirmation: `CharacterAiRoutinePriority` already computes a dedicated leisure priority and `CharacterAiDecisionPipeline` records it, but `GetGroupPriorityMultiplier` omits the concrete `LeisureVisit` branch from the branch-to-group switch. This is a single-path classification defect rather than a missing recreation asset or facility consumer.
- `RecreationJobGiver` already uses `CharacterNeedAiThresholds.GetRoutineUtility(FUN)`, while its `NeedRecreation` action consideration goes through generic `FacilityCandidateScorer.GetNeedScore`. The same candidate is therefore gated by two different need curves. The action consideration must read the same authored routine-need authority for the pure Entertainment role; no new balance constant is justified.
- The correction is intentionally limited to a pure `FacilityRole.Entertainment` consideration. Mixed-role facilities keep the generic facility score, so the patch does not reinterpret other visit semantics or silently broaden the FUN consumer.
- The daily fixture currently chooses a construction placement nearest the founder origin, then five service placements nearest that construction access, but it never records or constrains the actual path distance. It also overwrites all measured founder positions with the construction access cell. The next verifier revision must expose actual service/construction path lengths and fail a supposedly compact fixture when those paths exceed its authored compact bound instead of hiding travel inside the WU result.
- Construction already has a live `IConstructionProjectWorkforceRuntime.TryCaptureConstructionProject` snapshot. The five-day verifier currently reports only the legacy `ReservedWorkerPersistentId`; that is insufficient evidence for the 2/3/4 multi-worker contract. Peak active and effective workers must be sampled during the observation window.
- The latest report shows why the labor number cannot yet be treated as a neutral layout result: the fixture construction site is at `(21,0)`, while final founders are at `(31..35,1)` and one live construction route is `17` cells. Logs also show work attempts against unrelated scene construction/building IDs and `no-workplace` failures. The verifier is currently measuring ambient GameplayScene work competition in addition to its QA project.
- Physical food authority is internally consistent in that run (`120 -> 102`, 18 consume events), but water diagnostics are not: 17 safe-drink requests, 15 starts, 14 successes and only 12 physical consume events. This mismatch is now a separate open connection issue rather than a reason to invent time loss.
- The verifier initially assigns all three founders an explicit priority target for the QA construction site, but that assignment is not maintained after a need interruption. Once the command target clears, the general selector can choose ambient GameplayScene work. A neutral fixture needs an explicit scoped assignment authority (or a dedicated isolated scene), not repeated ad-hoc target reissuance that would bypass normal AI costs.
- Source tracing confirms the direct priority command is intentionally one-shot: `WorkTaskExecutor` clears it whenever the assigned priority run ends, including a routine-need interruption. Reissuing it every frame would change gameplay semantics and is rejected as a verifier workaround. The fixture should instead isolate the eligible work pool while leaving the ordinary scheduler and interruption path intact.
- `WorkTypeCatalog.All` provides the complete authored work list, so the verifier can put every non-construction priority Off and leave Construct at Priority1 without adding a test-only selector. The QA site is an Industrial `InProgress` construction order with authored urgency 90; this should let normal selection return to it after self-care while excluding guard/operate/ambient-domain wandering.
- The verifier's new workforce gate is deliberately causal: it reads `IConstructionProjectWorkforceRuntime` during the live observation and fails unless an Industrial project exposes max 4 and reaches at least two active workers. The legacy `ReservedWorkerPersistentId` remains diagnostic only.
## Phase 157 five-day rerun after leisure/workforce fixes (2026-08-12)

- Unity MCP PlayMode rerun completed with Console Warning/Error `0/0`, but authored verification remains `RESULT=FAIL` because recreation averaged `0.4` uses/actor-day versus the `0.6~1.4` target.
- The industrial fixture reached `peakActive=3`, `peakEffective=2.6`, `max=4`, proving the live project workforce cap/contribution curve is consumed by the construction runtime.
- Physical consumption authority is consistent in this run: food depletion/events `17/17`, water depletion/events `14/14`.
- Actual labor remains far below the 99 WU authored baseline: `12.143 WU/actor-day`; sampled mean channels include only `14.833s` work active but `58.540s` need travel and `24.677s` other travel per actor-day.
- The local fixture facilities do not dominate selection. Actors finish far from the fixture and consume at several pre-existing meal buildings. `FacilityCandidateScorer` currently gives distance only `0.05` weight while preference/stock/room/memory/other biases can outweigh very large travel distances. This is a live AI selection issue, not merely a report-fixture issue.
- Leisure now resolves a valid entertainment action and completes six uses, so the previous branch/consideration mismatch was real and fixed, but cadence remains below target and needs tracing after travel selection is corrected.
- Incremental live destination selection asks `FacilityCandidateCache.TryGetNearestCandidates(..., 20, ...)`, but then chooses the highest general utility among that shortlist. Distance contributes only `0.05` in `ScoreCandidateWithBreakdownCore`; the resulting difference between a 5-cell and 20-cell facility is only about 0.006 utility, so ordinary preference/stock/room/memory terms can easily pay tens of seconds of travel. The observed migration to distant existing facilities is consistent with this formula.
- This must be fixed in the shared facility selector rather than by repeatedly forcing the QA facility, otherwise the benchmark would hide a live-game labor sink.
- The authored FUN pair is internally revealing: standard daily depletion is `8`, the basic sofa restores `8` after a roughly 10-second use, while the AI routine starts at `80` and its resume target is `88`. A resident delayed below 80 cannot become satisfied in one sofa session, so low-priority leisure competes again and is often deferred. The five-day endpoints match this (`80 - ~40 decay + 8*uses`). The baseline's one roughly 10-second leisure session/day therefore needs a coherent depletion/recovery/threshold pass after travel is fixed, not a verifier-only tolerance change.
- `FacilityScoringContext` currently carries reputation, room policy, and culture only; there is no authored travel-opportunity-cost setting. Any shared fix should add an explicit data-owned parameter or a clearly audited balance constant rather than silently hard-locking destinations.
- Added data-owned facility travel opportunity cost to `CharacterAiNaturalnessSettingsSO`: 4 free cells, 0.015 utility cost per additional cell, maximum 0.35. `FacilityCandidateScorer` now subtracts this cost after normal need/preference/room/culture/reputation utility, so a materially better distant facility remains selectable while routine equivalent facilities should remain local.
- Five-day rerun with the new travel cost improved need travel only `58.540 -> 53.801s/actor-day` and actual labor only `12.143 -> 12.745 WU/actor-day`; distant facilities are still selected. The direction is correct but the authored cost is too weak for daily self-care opportunity cost.
- The benchmark is also population-contaminated: it reports only three founders but the live scheduler had four registered actors at the end (five in the previous run), and event logs include `CharacterPrefab(Clone)` / world-character IDs outside the measured trio. Shared food/water and facility reservations are therefore affected by non-fixture residents. The 3-founder baseline must disable/unregister non-fixture actors during PlayMode setup before further numeric tuning.
- Water authority again disagreed (`stock depletion=16`, tracked consumed events=15`, safe-drink successes=18`), consistent with untracked consumers and/or an additional safe-drink event-accounting issue. Do not retune water cadence until population isolation makes the consumer set exact.
- `DailyRoutineWuPlayModeVerifier` captures `actors = FindActors()` only once after the initial warmup, but later `PrepareFixtureWorkOrderReset()` and `ActivateFixtureWorkOrder()` call `FindActors()` again. Visitors/new residents spawned after the original capture can therefore receive the fixture construction priority and consume fixture supplies while remaining absent from the three observation records. Population isolation must use a fixed actor-ID whitelist and suppress or quarantine later arrivals across every fixture operation.
- The source of later actors is the scene `CharacterSpawner.StartSpawn()` infinite coroutine (0.3-second checks). Disabling only the component would not be a sufficiently explicit coroutine stop; the verifier can stop the scene spawner's coroutines, choose exactly three work-capable founders, deactivate any already-spawned non-fixture actors, and use the fixed trio for all later construction reset/assignment calls.
- Implemented exact three-founder fixture isolation: stop and disable the scene spawner after prepared-party creation, select the first three deterministic work-capable living actors, deactivate already-present extras, use the fixed array for later construction assignment/reset, and fail if active population at the end differs from three.
- Isolated rerun proved the fixture now has exactly three scheduler/active actors and exact physical authority counts (`food 13/13`, `water 10/10`, Console 0/0). This makes the trace usable for diagnosis.
- The isolated run failed all routine cadences and one actor reached hunger/thirst/excretion/hygiene 0, entered a deprivation breakdown, and spent 405.9s in other travel. The low WU is therefore not a legitimate neutral-day balance result; it is dominated by a live need-action completion failure. Other two founders remained stable enough to work, so the next target is per-actor facility/meal failure tracing rather than broad WU retuning.
- Meal actions show `PolicyForbidden` failures even though the same preserved-ration fixtures later succeed. Need to capture failures by actor/role/operation and determine whether policy is being re-evaluated against a changed plan/item or a reservation handoff.
- Root cause found for repeated meal `PolicyForbidden`: the shared need response starts routine hunger at `65`, so AI travels to eat at 65 or below, but `CharacterConsumablesRuntime` independently forbids automatic meals above its hard-coded `RoutineHungerThreshold` (50). Characters therefore walk to a facility and are rejected until hunger drops another 15 points. This split authority directly creates repeated travel, delayed feeding, and possible deprivation collapse.
- The meal runtime must consume the same authored `CharacterNeedResponseProfile`/need-balance authority as AI rather than maintain a separate hunger threshold. The intended food-balance rule from the approved plan uses normal meal line 50, so either both authored response and AI must move to 50 or the consumable line must be raised coherently; independent constants are invalid.
- `CharacterConsumablesRuntime` is VContainer-registered and can directly receive `ICharacterNeedBalanceRuntime`; all direct editor test constructions are confined to `SurvivalDebugScenarios`. The clean fix is constructor injection plus replacement of its hard-coded 50/20 checks with `GetResponse(HUNGER).routineStart/emergencyStart`, then author the shared response to the approved 50/20 meal lines.
- Implemented constructor injection of `ICharacterNeedBalanceRuntime` into `CharacterConsumablesRuntime`; all routine/emergency meal checks now read the shared hunger response profile. Updated isolated editor constructions to provide the default need-balance runtime. Source defaults now author hunger routine/emergency at 50/20.
- Unity MCP authored and read back the live `SurvivalBalanceSettings.asset` hunger response as routine 50, emergency 20, resume 75. The first command attempt referenced a nonexistent convenience method and failed command compilation; the corrected `TryGetNeed` command succeeded.
- Five-day rerun after unifying hunger thresholds removed the reported `PolicyForbidden` failures, confirming the split-authority bug was real. However the run worsened: two founders reached hunger/thirst/excretion 0 and entered breakdowns, meals fell to 0.533/day, and peak construction concurrency fell to one. Thus the threshold mismatch was one defect, not the primary starvation cause.
- Stable physical authority/population still held (3 actors, food 8/8, water 10/10, Console 0/0). The next root cause is emergency/routine decision priority or action re-entry: actors spent hundreds of seconds in `otherTravel`/`idleOther` while at zero needs, which should be impossible if emergency branches preempt correctly.
- Root pipeline order does invoke `RunEmergencyDecision` before routine/idle, and emergency job givers include food/toilet/hygiene/rest based on shared thresholds. However the entire emergency selection is skipped unless the aggregate `CharacterAiDecisionContext.EmergencyScore >= 0.58`. A zero survival need can therefore still be ignored if this aggregate score underweights it; inspect the score formula next.
- Survival group mapping itself is present for Eat/Drink/Rest/Toilet/Hygiene, so the previously fixed leisure mapping is not the cause of zero-need starvation.
- `EmergencyScore` is the maximum of `StrongestNeedUrgency`, health/injury, and world risks—not an average—so a correctly authored zero survival need should exceed the 0.58 gate. The aggregate gate is therefore unlikely to explain starvation by itself. The failure lies after emergency entry: emergency job-giver evaluation/destination selection or candidate continuation.
- Emergency job-giver set omits `Drink`, but `RunEmergencyDecision` first calls `TryRunSafeEmergencyRelief`, which handles shared-threshold emergency thirst. Once a deprivation breakdown is active, safe relief deliberately refuses to run, so any delay before breakdown becomes irreversible until the breakdown ends. This amplifies, but does not originate, the scheduling defect.
- Root order evaluates an existing macro goal before `RunEmergencyDecision`. Current-action interruption protects running actions, but a macro goal can potentially be handled when no interruptible action is active and starve emergency selection. Inspect `HasMacroGoal`/macro runner emergency break conditions next.
- Macro goals are generally one-shot and clear after candidate success/failure; LLM macro output is narrative-only. This makes a five-day total lock unlikely.
- Found a more direct fixture defect: compact layout computes three distinct actor access cells, then overwrites `fixtureActorPositions` with the same construction access cell repeated three times. Every post-preflight/reset teleport stacks all founders on one tile, producing step-aside behavior, path contention, and asymmetric starvation. A neutral three-founder routine must preserve three distinct nearby cells.
- Removed the overlap overwrite and added a hard fixture gate requiring exactly three distinct founder cells. Unity MCP compilation succeeded.
- Distinct-cell rerun eliminated deprivation breakdowns and restored symmetric stable behavior: food 15/15, water 12/12, meals 1.0/day, drinks 0.8/day, toilet 0.6/day, peak construction workers 3/effective 2.6, central output and construction delta matched within 0.001 WU, Console 0/0.
- Actual labor improved `10.962 -> 19.882 WU/actor-day`, proving the overlap defect was major, but still far below 99. Remaining mean sinks are need travel 40.947s, other travel 38.589s, idle 41.354s, plus 15.794s work transit. Hygiene 0.467/day and recreation 0.533/day remain just below their current verifier gates.
- The current 0.015/cell travel opportunity cost reduced but did not localize daily services enough. Next calibration should increase the authored opportunity cost and inspect routine priority multiplication so needs are not deferred until emergency.
- Routine selection multiplies each job candidate by group priority. Survival priority becomes 35+need*30 only after urgency 0.25 and 95+ above 0.65, while on-duty work remains a valid candidate with domain down to 0.2. This is broadly sensible; the more immediate measured sink is distant destination choice and repeated resume travel rather than a missing survival group mapping.
- Increased the authored facility travel opportunity cost from 0.015/0.35 to 0.04 per paid cell / 0.65 maximum, retaining four free cells. Unity MCP compiled, wrote, and read back the settings successfully.
- The stronger 0.04/0.65 calibration is rejected by live evidence: it raised labor to 26.75 WU/day and reduced need travel to 31.35s, but lowered meal/toilet/hygiene/recreation cadence, caused two breakdowns, and produced two fixture damage-port errors. Reverted source and asset to 0.015/0.35 through Unity MCP. Use a near-equivalent-candidate rule instead of globally suppressing distant facility utility.
- Construction progress and central output differed by `0.936 WU` (`188.128` vs `187.192`); do not relax the 0.01 gate until deprivation/off-domain activity and capture boundary effects are isolated.
# 2026-08-12 measured WU authority correction

- The user is correct: the old `99 WU/adult-day` was derived from `100 scheduled work seconds × 0.99 transition efficiency`, not from live AI output.
- The cleanest current five-day neutral-facility sample is `19.882 actual WU/adult-day`; it had all three founders alive, no breakdown, exact physical consumable accounting, three-way construction concurrency, and Unity Console `0/0`.
- Later samples at `14.343` and `8.444 WU/adult-day` are rejected as balance baselines because a founder entered deprivation breakdown. The first also exposed an incomplete fixture damage port; the runner now excludes damage targets that cannot execute damage rules.
- The authored live baseline is therefore provisionally `20 WU/adult-day`. `99` remains only as `HistoricalTheoreticalCapacityWuPerAdultDay` for auditing the daily schedule envelope.
- Technology output-equivalent checkpoints now preserve the intended index while using live scale: Day 1 `20`, Day 30 `21.84`, Day 120 `25.08`, Day 240 `29.884`, Day 400 `33.948`, Day 960 `40` WU/adult-day.
- Direct fixed-99 consumers found in settlement WU, faction contract reference production, research-day reports, founder WU reports, population simulation text, and equipment-readiness text were migrated. Old balance-baseline records that contain numeric results computed from 99 remain historical evidence and require full recalculation rather than search-and-replace.
- The content assembly cannot reference the service-layer labor rules. Its contract mirror is authored as 20 and `V23BalanceAudit` now fails when it diverges from `SettlementLaborBalanceRules.BaselineWuPerAdultDay`.
- Unity MCP evidence after actual project recompilation: `liveBaseline=20`, `theoreticalEnvelope=99`, `endlessIndex=2.0`; `PHASE157_EMERGENCY_LABOR=PASS`; Console Warning/Error `0/0`.
# 2026-08-13 single AI intent authority findings

- Root collision was split ownership, not pathfinding alone. `AIBrain` exposed one anonymous `externallyDrivenActionActive` bool while safe drink, routine/emergency relief, primitive survival and breakdown coroutines independently began and ended it.
- A stale coroutine could therefore finish after a newer action started and call the anonymous end method, cancelling the newer action. Deferred safe-relief retries also held the actor lock while doing no action.
- Added `CharacterActionIntentLease(ownerId, kind, epoch)`. All physical commits and completion now require the exact current lease. A higher kind can preempt; equal/lower unrelated requests are rejected. Retry delay owns no lease.
- Direct player movement is still represented by the existing manual-command mode, but is now mutually exclusive with external intent acquisition. Starting it retires the previous autonomous external owner; autonomous actions cannot acquire while it is active.
- Breakdown relief, eating, drinking, collapse recovery, vandalism and assault now also revalidate the exact lease immediately before their physical mutation. Retiring the presentation owner alone is no longer sufficient to let an old breakdown coroutine damage or consume during a direct order.
- The first instrumented five-day sample made the next bug visible: Leon/Roma ended at breakdown epochs 83/29 because `RunViolentImpulse` ended its coroutine without ending `breakdown.active`.
- Violent impulse is now a bounded episode and calls the authoritative consequence service on completion, lowering mental-instability burden to 55 and releasing the brain. A later episode still requires burden to rebuild past 70 and pass the normal check.
- The next five-day sample confirmed collision removal: all three founders had 4 intent transitions, zero preemptions, zero rejected acquisitions and zero stale completions at the final snapshot; no actor remained in breakdown.
- That sample is not a WU baseline. It produced only 62.624 WU total / 4.175 per actor-day and peak construction workers 1. The actor diagnostics show work priority remains `Priority1`, but routine need interruptions/resumption fail to restore stable multi-worker execution. This is now a separate work-command/resume authority problem.
- Editor focused scenarios previously threw because their actor fixture constructed no world-item runtime yet `CharacterAiDecisionContext` legitimately reads carry inventory. The fixture now injects the real resource item catalog, hauling settings and one shared carry registry; runtime code retains strict dependency checks.

## 2026-08-13 parallel construction authority defect

- The Industrial construction site correctly authored a four-worker capacity, but reservation and arrival used different object identities. AI reservation stored the `CharacterActor` as `IBuildingCharacterPort`; `AllocateWorker` later queried with the actor's separate `CharacterBuildingVisitorAdapter`. Reference-keyed lookup therefore treated the same stable character as another worker's reservation and rejected all but the first arrival.
- Construction visual offsets were assigned from the current list count. Removing an earlier worker and adding a replacement could reuse an occupied offset, creating overlapping worker presentation and ambiguous diagnostics.
- The repair changes reservation identity to authoritative `CharacterId` and reserves a stable slot index that survives the Actor-to-Visitor boundary. This is an AI authority correction, not a WU balance change.

## 2026-08-13 primitive survival intent rejection and verifier ambiguity

- `CharacterPrimitiveSurvivalRunner.TryStart` removed its running-action record when a different external intent acquisition failed. That could make deprivation logic believe no primitive action was running while the previous coroutine and Brain lease still existed.
- Both primitive survival and safe-drink runners returned `true` when `AIBrain.TryBeginExternallyDrivenAction` rejected the request. Emergency self-care therefore reported handled without starting any action and could postpone the subsequent breakdown decision.
- A currently tracked primitive action remains a successful handled state; a rejected new Brain lease is now a real `false` result and does not mutate the older action record.
- The five-day verifier used one `FIVE_DAY_SURVIVAL` assertion for both elapsed simulation time and actor life state. It now reports `FIVE_DAY_ELAPSED` separately and includes dead/external-intent owner/epoch diagnostics per founder.
- The first instrumented replay exposed a non-repeatable owner death at 727.2/900 seconds while survival needs were nonlethal; the old report could not name its cause. The verifier now records `CharacterDeathEvent` cause/day/location and each founder's last 20 authoritative `health:damage` activities.
- The next clean replay completed 900.89 seconds with all three alive, no breakdown, no death event, exact ration/water depletion and six physical meals. This proves the intent-state fixes can sustain the party, but it is not yet a healthy-routine pass: one founder ended at hunger 0.24 and hygiene 0, and all founders accumulated starvation damage.
- Therefore the remaining defect is late emergency scheduling/action completion rather than missing physical food authority. Survival PASS alone must not be used as proof of natural AI behavior.
- 2026-08-13 AI survival acceptance gap: the five-day verifier previously required only survival and positive health, so founders ending at 93-94 HP after starvation damage still produced PASS. The authoritative scenario now captures initial health per character and requires no net health loss under the deterministic no-incident primitive-start run.
- 2026-08-13 routine scoring root cause: `AIPrimitiveSurvivalAction.AdjustScore` attenuated the whole maximum by `0.65`, capping every non-emergency routine need at 0.65 and jumping to 1.0 only at the emergency boundary. Preserving authored base-score attenuation while passing through continuous routine utility removes that discontinuity without globally increasing idle primitive behavior.
- 2026-08-13 five-day live failure after score continuity fix: all three founders consumed physical food and water but still ended at 93/95/95 HP. Every recorded damage event used the starvation source; dehydration produced none.
- 2026-08-13 primitive latrine routing defect: `TryGetDesignatedLatrinePosition` scanned the entire same-floor grid and broke equal-safety ties by lowest X, not distance. A founder could cross the whole map for primitive relief while hunger continued falling. The primitive search is now bounded to 8 cells and selects safety, then distance, then stable X.
- 2026-08-13 deprivation authority defect: starvation/dehydration damage checked only historical burden. Eating from hunger 0 to 35 stopped new burden growth but did not reduce burden below 70, so the actor kept taking starvation damage while no longer starving. Damage now additionally requires the current authoritative need to remain below 20 and resets the next damage grace interval while recovered.
- 2026-08-13 corrected live evidence: after bounding primitive latrine travel and gating damage by current need, the same five-day scenario completed at `100/100/100` founder HP with no damage activities. Rations changed `24->12`, water `30->10`, and all consumption/conservation gates passed, proving the behavior was not hidden by free recovery or synthetic inventory.
- 2026-08-13 AI stress fixture defect: the synthetic stress `CharacterSO` no longer satisfied the authoritative phenotype contract, and the shared movement fixture omitted `IDoorAccessQuery`. The latter generated repeated idle-wander `NullReferenceException`s even though the old stress validity predicate returned true. Stress actors now clone authored species data and the common fixture injects an explicit open traversal policy.
- 2026-08-13 AI performance finding: 500-NPC behavior reaches all registered trees and respects decision/path count budgets, but CPU cost is not acceptable. Detailed profiling attributes the dominant cost to action destination preparation/facility evaluation and world-signal capture. GC is a separate result: after warm-up, the detailed sample measured `0 B/frame`; the earlier 1.25 MB short-profile result included profile lifecycle contamination and is not steady-state authority.
- 2026-08-13 spatial-index lifecycle defect: world membership revisions advanced epochs but retained prior bucket dictionaries and signal-cache entries. Repeated worlds accumulated stale actors and stale snapshots. Membership revisions now rebuild the affected index once and clear dependent signal caches.

## 2026-08-13 500-NPC facility-path diagnosis

- Unity slow-operation traces isolated the dominant real path: `AIEat`/`AIRest` destination resolution repeatedly paid candidate distance and world-signal costs, reaching roughly 30-55 ms per action in a 500-character world.
- The distance spike was not ordinary dictionary lookup. `GridPathSearchResult.GetMoveCostTo(IGridOccupant)` lazily built its occupant-position cache by scanning every searched grid cell on the first facility score. The result now stores occupant move cost alongside occupant position during that one build, so later target costs are O(1).
- The same spatial proximity loop repeatedly evaluated `CharacterActor.IsDead`, which projects authoritative vitals. Spatial entries now snapshot dead state when the indexed actor is refreshed, as they already did position and worker role.
- Measured 500-NPC scheduler p95 improved from the previous traced ~83 ms to 43.223 ms after the occupant move-cost cache. Behavior remained valid and the focused naturalness, priority corner, staff, customer, work-priority and direct-command scenarios all passed.
- Remaining CPU authority is world-signal proximity / spatial-index work (decision-context world signal p95 about 9 ms). Performance is still incomplete; do not claim the 4 ms scheduler target or full AI completion.

## 2026-08-13 post-checkpoint AI profile and grid access finding

- The current 500-NPC / 180-frame PlayMode baseline is behavior-valid and allocation-free after warm-up, but CPU-invalid: scheduler average `34.061 ms`, p95 `57.069 ms`, with 679 decisions and at most one decision per sampled frame.
- A 60-frame detailed sample shows the scheduler wrapper is not the dominant cost: `BehaviorTree p95=51.64 ms` versus `Scheduler p95=51.66 ms`. The largest live categories are `Action.ResolveDestination p95=31.34 ms`, decision-context world signal about `8.41 ms`, and facility availability about `3.41 ms`.
- `Grid.SearchPath` already discovers every visitable occupant at a known cell and known cumulative move cost, but the result previously discarded that association and rebuilt it by scanning all visited cells on the first facility lookup. The implementation now carries an occupant-to-access dictionary from the search itself so facility distance and path-confidence lookups are O(1) without a second grid scan.
- Grid, AI naturalness, priority-corner, customer AI, and 100-NPC stress focused regressions all pass after the eager access-record change.
- One post-change profile pump collided with the automatic Editor PlayerLoop and emitted Unity's recursive PlayerLoop error. This is a test-harness invocation race, not gameplay evidence; the run was aborted, PlayMode stopped, and its performance result is discarded. Subsequent profiling must use either automatic Editor updates or paused manual stepping, never both concurrently.

## 2026-08-13 decision-local evaluation and profile isolation findings

- The frame-distributed 500-NPC profiler reset its detailed recorder before world staging, so the mandatory first Behavior Tree tick for all 500 actors contaminated category percentiles and filled the slow-operation trace before the measured frames began. The recorder is now reset after warm-up, GC baseline, and stabilization, immediately before the real sample window.
- Candidate selection already resolves `CanStart`, destination, and considerations into `AIBrainActionEvaluation`. `AIBrainCandidateCommitter` then removed that evaluation and recomputed the same action immediately before commit, repeating facility scans and world-signal capture in one root decision. Commit now reuses the decision-local cached evaluation and leaves mutable physical revalidation to `SetResolvedDestinationWithFailure`.
- Priority-corner, naturalness, customer, and 100-NPC stress regressions pass after the decision-local reuse change.
- Two isolated 500-NPC 60-frame samples remain CPU-invalid and have high editor-run variance: before commit reuse scheduler p95 was `65.277 ms`; the next sample after the change was `90.528 ms`. The latter also performed more broker searches and is not evidence that the code change regressed or improved performance. No performance claim is accepted from one short sample.
- Slow traces from the isolated sample still attribute large time to facility distance and world-signal intervals even though the distance lookup is dictionary-based. This must be separated with a deterministic microbenchmark or a lower-overhead profiler marker before further data-structure changes.
- The current synchronous AI conflict matrix is green across 13 groups: action metadata, intent arbitration, multi-need naturalness, priority/destination failures, customer/staff/owner behavior, direct commands, diagnostics, grid/path invalidation, work accounting, and survival/physical meal commit. This proves the recent patches preserve the covered transitions, but it does not prove multi-frame PlayMode cadence, five-day work resumption, reservation races, or 500-NPC performance.
- A dedicated candidate-commit regression now counts `CanStart` calls and requires exactly one call across selection plus commit. It passes, directly proving the cached decision evaluation is reused rather than inferred from timing.
## 2026-08-13 refreshed AI assembly findings

- Priority scenario failures after the real domain reload were caused by the test fixture lifecycle, not candidate selection: an active `GameObject` added `CharacterActor` before `AIBrain`, so `CharacterActor.Awake()` completed with `brain == null` and later reflection-based `Awake()` calls were blocked by `runtimeStateInitialized`.
- Rebuilding the fixture while inactive, injecting dependencies, and only then enabling it matches runtime prefab construction and removes the hidden stale-assembly dependence.
- After a real Unity refresh, priority/naturalness focused scenarios and the 13-group broad matrix (action descriptor, customer, staff duty, work priority, direct priority command, owner, feedback, grid, physical grid, work amount, survival) all pass with Console Warning/Error 0/0.
# 2026-08-13 five-day facility fallback authority findings

- The five-day neutral routine initially executed primitive survival despite authored toilet/hygiene facilities. The first concrete defect was `BuildableObject.IsGridVisitable` consuming `CanVisit(null)`: a full facility disappeared from structural reachability, so temporary capacity pressure was misclassified as missing infrastructure.
- Structural reachability now uses the non-capacity `CanQueueVisit` contract, while candidate commit still uses `CanVisit(actor)`. A focused occupied-toilet regression proves routine fallback waits for reachable infrastructure and hard emergency fallback remains available.
- Start-time telemetry exposed two further authority bypasses. `CharacterAiDecisionPipeline.RunEmergencyDecision` directly ran deprivation self-care before facility job givers, and `CharacterDeprivationRuntime.Update` independently started primitive actions at high simulation speed. Both bypassed normal facility candidates.
- Emergency selection now uses the normal job-giver/action path, and the deprivation high-speed safeguard calls the same `AIPrimitiveSurvivalAction.CanUsePrimitiveFallback` policy before starting anything. Primitive actions also revalidate mutable eligibility at candidate commit and immediately before execution.
- Latest Unity MCP five-day run: primitive survival `0`, harmful stalls `0`, execution failures `0`, no-action `0`; toilet `0.667`, hygiene `0.933`, recreation `0.8` uses per actor-day. This resolves the authored-facility-versus-primitive AI defect.
- The same run produced only `28.761 actual WU/adult-day`, with `94.095 s/adult-day` classified as need travel. This remains contaminated by route/layout/need-action selection and is not accepted as a production balance baseline. Do not lower BOM or work costs to fit it.
- The only verifier failure was a `0.466 WU` (`0.12%`) construction-output versus central-accounting boundary difference under a `0.01 WU` tolerance. Isolate observation boundary accounting before changing that gate.
# 2026-08-13 meal-facility availability authority finding

- The five-day trace seeded the local meal counter with a physical `FacilityBuffer` stack whose destination was exactly `facility-input:meal:{BuildingInstanceId}`; `CharacterConsumablesRuntime.GetMealDestinationId` produces the same string. The earlier destination-ID mismatch hypothesis was therefore disproven by source and live report evidence.
- `HasMealAvailable` calls `GetMealCandidates`, and that method performs the asynchronous exact actor-to-facility route query even for buffer-only availability. A `Pending` route returns an empty candidate list plus `routePending=true`; `HasMealAvailable` then reports `DeliveryPending` despite food already being physically present in the requested facility buffer.
- `FacilityCandidateScorer` deliberately excludes `DeliveryPending` from immediate meal candidates. Consequently a transient path-budget state makes the nearest supplied facility look physically empty and sends actors to a farther facility whose route happened to be cached/ready.
- The required repair is separation of concerns: physical buffer availability must not depend on asynchronous navigation readiness. Navigation remains authoritative at candidate/path commit, while delivery-pending remains reserved for an actually absent local serving with a viable/pending source-delivery route.
- After the split, the five-day trace proved the local meal counter `queueable=True` and `immediate=True`; meal paths began using the 5-9-cell local counter rather than only the 13-17-cell stove. The same trace exposed a separate scoring defect: a candidate 6 cells farther away won on `0.628` versus `0.526`, just outside the authored `0.06` equivalence tolerance. The shared equivalent-facility tolerance is now `0.12`, while species-affinity mismatches remain explicitly excluded from proximity substitution.
- The trace also surfaced an opaque `PhysicalConsumptionFailed` facility event. `TryGetMealOperationResult` previously discarded the actual aborted-plan reason after removing the active plan. A bounded operation-failure map now retains the terminal failure code/detail long enough for the waiting facility coroutine to report the real commit/revalidation outcome; successful/retried operations clear it.
- A second five-day sample had no `facility-distant-selection` event, proving the proximity equivalence nudge fired. Its remaining apparent 30-31-cell detours between facilities only 2-3 coordinate units apart were a verifier defect: grid `y` is a floor, not a visual row. The compact-layout helper selected facilities on floors 0 and 2 and then treated Manhattan displacement as local. AI correctly routed through stairs. The fixture now requires actors, all five facilities, and the construction site to share the origin floor and fails setup otherwise.
- Terminal meal failure diagnostics are intentionally bounded to 64 distinct operation IDs. A HashSet prevents repeated aborts of the same operation from consuming the FIFO budget; successful reuse clears the current failure entry.
## 2026-08-13 AI 진단 감사 발견

- 짧은 독립 회귀 12개 묶음은 모두 통과하고 Unity Console도 0/0이므로 현재 문제는 기본 행동 정의 누락보다는 장기 상태 교차, 실행 도중 권위 변경, 또는 진단 정보 소실 가능성이 높다.
- `AIBrain`과 `CharacterAiRuntimeDiagnosticsSnapshot`에는 `NoAction`, 실행 실패, 후보 거절, JobGiver 분기별 거절과 반복 실패 최고치가 이미 있다. 새 카운터 체계를 중복 도입할 필요는 없다.
- 반면 `CharacterAiDecisionPipeline.RunSelectedAction`은 `TryExecuteSelectedAiAction()`의 false 결과를 받을 때 `AIBrain.LastActionFailure`를 전달하지 않고 고정 문구로 바꾼다. 이 경로를 지나면 숫자상 실패는 보이지만 실제 NoPath/Destroyed/Occupied/CannotStart 이유가 결정 결과와 블랙보드 진단에서 사라진다.
- 기존 부하 도구의 다량 `EditorApplication.Step()` 호출은 Unity `JobTempAlloc` 수명 경고를 만들었다. 공식 장기 검증은 Start 후 자연 Editor 업데이트를 기다리고, 강제 Pump는 소수 프레임 개발 보조로만 제한해야 한다.
# 2026-08-13 AI concurrency and scheduler fairness findings

- Retail checkout had a real time-of-check/time-of-use race: two customers could both pass `stock > 0`, resume after the feedback delay, commit buyer state twice, and drive one remaining unit to `-1`.
- The purchase coroutine had no commit result, so the shop caller treated every returned coroutine as success. A typed resolved/committed/failure result is required across the visitor-port boundary.
- The scheduler's earlier actor-count safety logic silently widened the authored 16-decision ceiling. After restoring the hard ceiling, the strengthened 500-NPC audit exposed starvation rather than hiding it with burst work.
- `CharacterAiDecisionSchedule.Count` used heap entries, including invalidated reschedule tombstones, instead of live `dueTimes`; this inflated backlog accounting.
- A fixed minimum of one decision per frame cannot service 500 live requests inside the authored 2-second deferral bound. The fair service floor must derive from live scheduled actors and the target service horizon, remain under the authored hard ceiling, and be honored before the time-budget break.
- Using an uncapped slow-frame delta in that floor creates positive feedback: a hitch demands more decisions in the same frame. The delta is now bounded to two target frames.
- MCP dynamic-command compilation success is not project compilation authority. The first shop edit briefly left two project CS0246 errors while a command still ran against the old assembly. Every source change now requires editor compile-state plus Console evidence.
- The PlayMode profiler request flag could survive a rejected PlayMode entry. A stale EditMode request must be recovered explicitly and leave `stale-profile-request-recovered` evidence.
- Repeated `EditorApplication.Step()` inside an MCP command recursively enters PlayerLoop and causes TempJob warnings/errors. The verifier now requests the normal editor loop instead of bulk-stepping.
- Latest clean normal-loop 500-NPC fairness evidence: starvation `0`, maximum deferral `1.083s`, scheduler p95 `2.498ms`, scheduler-owned GC `0 B/frame`. Whole Editor frame p95 remains `19.07ms`, so overall 60-fps performance is not closed.
# 2026-08-13 AI multi-seed/runtime-lifecycle findings

- Seed `157181` initially failed only the physical construction vs central output-equivalent causal check: `805.851` vs `805.240 WU`.
- Two independent long-run defects were proven:
  - `WorkTaskExecutor` discarded sub-milli labor/output carries at every routine-need operation boundary.
  - `WorkOrderRecord.completedWork` accumulated thousands of small deltas directly in a `float`, drifting above the milli-WU ledger.
- The executor now preserves cumulative carries across operation boundaries. Work orders accumulate runtime progress in `double preciseCompletedWork` while retaining the existing float save contract.
- A teardown race also reproduced: the 5-day verifier removed a `ConstructionSite` while a work coroutine was finalizing, producing `MissingReferenceException` at `assignedTarget.name`. Finalization now detects Unity fake-null targets, emits typed `Destroyed / work-target-destroyed-after-execution`, releases reservations/context, and replans without dereferencing the target.
- Corrected seed `157181` passed five days: physical construction `768.725 WU`, central output equivalent `768.723 WU` (difference `0.002 WU`), harmful stalls `0`, console exception `0`.
- One recovered meal commit failure was still displayed as generic `PhysicalConsumptionFailed`. The core runtime already authored exact details, but `CharacterBuildingVisitorPort.TryGetMealConsumptionResult` discarded the failed result when the runtime returned `false`. The adapter now preserves failure code and parameters so `meal-lease-invalid-at-commit`, `meal-quantity-commit-failed`, etc. reach facility activity and AI blackboard diagnostics.
# 2026-08-13 seed 157183 AI failure root causes

- `CharacterActor.TryExecuteSelectedAiAction` has no idempotency boundary: a selected action whose `HasStarted` flag is already true can reach `ActionSet.Execute` again and start a duplicate coroutine. The repair must suppress the duplicate while retaining the running action and expose a bounded diagnostic counter rather than cancelling the valid work.
- Toilet and hygiene candidate scoring does not use the same authored routine thresholds as the execution/job-giver boundary. Raw need magnitude can therefore select a facility action that the action layer immediately rejects, producing repeated selection and re-entry. Selection and execution must consume one shared `CharacterNeedAiThresholds` projection.
- `BuildingOccupancyAssignment.TryBeginUse` calls `RecordFacilityUse` at admission. `FacilityVisitEvent`, completed-use count, cleanliness cost, and downstream cadence therefore include cancelled or replaced interactions. Admission and successful completion need separate APIs; cancellation only releases occupancy.
- Quantity leases validate against `ItemStackSignature`, which contains the exact mutable freshness component. Normal freshness decay during the four-second meal action changes the signature and invalidates the actor's own lease. Reservation identity must exclude continuously aging freshness while meal start/commit continues to validate current freshness, contamination, policy, and quantity explicitly.
- The seed's WU/physical mismatch is still an open observation-boundary issue. It must be remeasured after the above behavior fixes and not hidden by widening the verifier tolerance.
- The first post-repair seed `157183` rerun exposed the formerly opaque single meal failure as `PolicyForbidden:owner,not-hungry`, not a quantity-lease failure. A meal need can become satisfied after selection but before facility execution; that stale plan is a benign cancellation/replan and must not increment `ConsumptionFailed` or consume an item. Ritual-fast and physical commit failures remain typed hard failures.
- The same run measured toilet cadence at `0.8/actor-day` against the authored `0.3~0.7` lower-level calibration gate. This is now a separate balance/fixture discrepancy, not evidence that the AI is stuck; do not widen the range until the live depletion, work-depletion, facility recovery, and initial-state formula explain it.
# 2026-08-13 AI facility-action ownership root cause

- The live five-day seed 157183 initially recorded 24 `action-replaced-during-interaction` cancellations with zero path, reservation, execution, or harmful-stall failures.
- A first defect was `DungeonWorkforceReplanService.ShouldPreserveRunningNonWorkAction`: it inferred the current action category from `AbilityWork.isWorking`. That flag can remain true across the scheduling boundary where construction is interrupted for toilet, hygiene, meal, or recreation. The service therefore selected a self-care actor as a worker and stopped its active interaction.
- The preservation decision now reads the current `AIActionSet` `Work` semantic through `AIBrain.HasRunningWorkAction`. A regression explicitly holds stale `AbilityWork.isWorking=true` while a recreation action is active and proves the interaction is preserved.
- A second defect was ordinary construction/dismantle/material refresh calling workforce requests with `forceInterrupt:true`. This treated normal work availability like an emergency and bypassed the non-work preservation policy. Normal construction, dismantle, and material haul wakeups now use non-forced scheduling; medical/combat emergency callers retain explicit force semantics.
- Same-seed causal evidence: interaction replacements fell `24 -> 9 -> 0`; interrupted replans fell to zero; actual labor rose from `756.497 -> 787.083 -> 784.268 WU` across five days. The final run had execution failures 0, harmful stalls 0, primitive fallback 0, console issues 0.
- The remaining report failure is not an AI ownership failure: completed toilet use is 0.8/actor-day against the old baseline gate 0.3-0.7. The user previously requested roughly daily needs, so this must be handled as a separate balance-authority change, not hidden by relaxing an AI test ad hoc.
