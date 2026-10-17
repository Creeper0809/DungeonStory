# WIM042 — Prepared candidate living information

Status: implementation patch ready for sole Unity operator review; PlayMode execution is pending. This batch deliberately contains **no WIM010 behavior change**, no asset/scene/save DTO edit, and no generated knowledge-base rebuild.

## Authority and scope

- Balance record read before implementation: `balance:wim:042:prepared-candidate-living-information` in `docs/game-design/whole-game-balance-baseline.md`.
- Balance impact: none. The patch adds a read-only preparation-screen projection; it creates no body/need state, consumes no items or work, advances no clock, and makes no candidate/trait/meta mutation.
- Knowledge-base query was previously rejected as stale for this task (`content-db` digest `5da7…`, knowledge-base digest `7be…`, 52 failures). Current C# and authored assets were used as source truth; no stale generated row was cited and no rebuild was run.
- No save ownership changes: prepared growth is already cloned into `PreparedStartPartyMemberSnapshot.growth` by `StartPartyPreparationService.CreateSnapshotMember`. Live diet policy remains absent until the consumables runtime applies its canonical default, and needs/thermal limits are recomputed from their existing authored definitions.

## Four displayed facts

| Fact | Read-only source | Projection / actual consumer | Deliberate non-claim |
|---|---|---|---|
| Initial health | `CharacterGrowthState.startingProfile.initialAgeConditionIds` | `StartPartyCandidateLivingSummaryProjector`; each carried ID is named and marked `AgeConditionSeverity.Mild`, matching `CharacterLifeRecord.AddInitialAgeConditions` and `CharacterAgeConditionBodyHealthAdapter` | No aggregate HP or a made-up health score |
| Diet | `CharacterConsumablesPolicyRules.DefaultDietPolicy` (`Free`) | candidate summary and panel | No species-diet-field restriction claim; culture/background meal restrictions are explicitly separate and resolve after the run starts |
| Sleep | authored `ICharacterNeedDefinitionCatalog` definition for `CharacterCondition.SLEEP` | `CharacterNeedDefinition.DefaultValue` and the prepared profile's sleep-rate multiplier | No current fatigue state is created or predicted |
| Climate | candidate `CharacterData` plus its already selected prepared traits through `ICharacterRuntimeProfileFactory` | `SpeciesThermalProfile` comfort/safe/lethal temperature bands | No current-temperature, equipment, fit score, or safe/unsafe verdict |

The actual production consumer is `OwnerSelectionPanel.BuildTabText` on the identity tab. The former fixed 250px `TextOverflowModes.Truncate` body is now a `RectMask2D` + vertical `ScrollRect`; its text content uses `TextOverflowModes.Overflow`, so long trait descriptions and the new facts remain reachable.

## Connection record

| Symbol | Role | Live path |
|---|---|---|
| `StartPartyCandidateLivingSummaryProjector.Create` | read-only derivation | prepared member + authored definitions → summary |
| `IStartPartyPreparationService.TryGetCandidateLivingSummary` | UI query boundary | `OwnerSelectionPanel.BuildTabText` |
| `PreparedStartPartyGameplayApplier` / `CharacterLifePublicationService` | existing game-start path | prepared `growth` snapshot → initial age-condition publication/body health |
| `CharacterConsumablesRuntime.GetDietPolicy` | existing diet authority | missing per-character policy → canonical `DefaultDietPolicy` |

Definitions/producers/authorities/live UI consumers: 4/4/4/1. New orphan IDs: 0. The added query has one production UI caller and one focused verifier caller; it has no mutation path.

## Focused verification staged for the Unity operator

`Assets/Scripts/Services/Character/AI/Editor/Wim042PreparedCandidateLivingInfoPlayModeVerifier.cs` adds menu command:

`DungeonStory/Debug/QA/Run WIM042 Prepared Candidate Living Information`

It uses the actual `OwnerOption_*` button via `EventSystem` and verifies:

1. all seven prepared roster entries resolve IDs/names/`Mild`, canonical `Free`, authored sleep default, and the same selected-trait thermal profile;
2. after the asynchronous preparation is ready, two production-panel renders leave both the exact `character:start-party-preparation` random-stream state/draw count and every living-summary fingerprint unchanged;
3. each visible selected-card body has the masked vertical scroll wiring and all four fact sections, then a real `EventSystem` drag moves an actually overflowing card and reaches its final lethal-climate section;
4. the ready gameplay snapshot keeps the prepared initial-condition IDs and selected trait IDs (the latter is the post-start climate-profile input);
5. skill reroll preserves the living summary, full reroll reprojects the current prepared state, and reserve swap preserves each candidate's facts;
6. the production `IPreparedStartPartyCommitService` path starts the prepared party, then all three live actors are resolved by the committed persistent IDs and checked for initial `Mild` conditions, canonical diet, authored initial sleep, and their selected-profile thermal bands;
7. cleanup pauses simulation around the post-commit read, calls no save API, restores time scale, and exits PlayMode so the committed fixture state is discarded.

Expected operator artifact: `Artifacts/QA/wim-042-prepared-candidate-living-information-playmode-report.txt` with all `PASS` rows, including `RERENDER_DOES_NOT_REROLL`, `EVENTSYSTEM_SCROLL_REACHES_FINAL_CLIMATE`, and all three `LIVE_*` groups. Operator must also visually confirm drag/wheel scrolling of a long identity card at the supported screen size; this worker did not invoke Unity, compile, or PlayMode.

## Static checks performed here

- Targeted `git diff --check`: no whitespace error in WIM042 files.
- Source call-site inventory confirms the query is consumed by the real `OwnerSelectionPanel`, not the obsolete alternative preparation controller.
- The panel grew from 776 to 828 lines solely for its existing card rendering/scroll lifetime; the living-fact derivation and UI text composition are separated into their own projector/presentation types.
