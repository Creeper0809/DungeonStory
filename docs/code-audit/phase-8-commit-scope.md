# Phase 8 Commit Scope Manifest

## Purpose

This manifest isolates the current DI and over-separation refactor from the broader dirty worktree. Use only the files below when staging or reviewing this refactor batch.

## Scope Rule

- Stage paths explicitly.
- Do not use `git add .`.
- Re-check `git status --short -- <paths>` before committing.
- Treat unrelated modified/untracked files outside this manifest as out of scope.

## Phase 7 Refactor Files

Runtime code:

- `Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs`
- `Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs`
- `Assets/Scripts/Codex/CodexRecordSummaryQuery.cs`
- `Assets/Scripts/Offense/OffenseTabSummaryQuery.cs`
- `Assets/Scripts/Operation/OperationTabSummaryQuery.cs`
- `Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs`
- `Assets/Scripts/UI/UiPopupService.cs`
- `Assets/Scripts/Buildings/UI/UIBuildingInfo.cs`
- `Assets/Scripts/Character/Core/CharacterActor.cs`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionService.cs`
- `Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs`
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`

Editor compile fixture updated because runtime contracts changed:

- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`

Removed runtime files:

- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs.meta`
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs.meta`

Audit and planning artifacts:

- `OVER_SEPARATION_AUDIT.md`
- `task_plan.md`
- `progress.md`
- `findings.md`
- `docs/code-audit/phase-8-commit-scope.md`

## Follow-Up Files To Add After Phase 10/11

Phase 10 provider helper/refactor files:

- `Assets/Scripts/Infrastructure/CachedSceneRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/CachedSceneRuntimeProvider.cs.meta`
- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/InvasionThreatRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/LocalLlmRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/RegularCustomerRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/RuntimePanelProviders.cs`
- `Assets/Scripts/Infrastructure/SocialReputationRuntimeProvider.cs`
- `Assets/Scripts/Infrastructure/StaffDiscontentRuntimeProvider.cs`
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`

Phase 11 automated/debug verification files:

- `Assets/Scripts/Editor/RefactorFollowupDebugScenarios.cs`
- `Assets/Scripts/Editor/RefactorFollowupDebugScenarios.cs.meta`
- Updated progress/findings/test result logs.

## Commit Checklist

Before committing:

- Unity compile succeeds.
- Manual PlayMode flow verification has passed.
- Watch-provider refactor compile and PlayMode checks pass.
- Automated debug verification scenario passes.
- `git diff --check` passes on scoped files.
- `git status --short -- <manifest paths>` shows only intended paths.
