#if UNITY_EDITOR
using System;
using UnityEngine;

public static class LedgerB1RegressionIsolationRunner
{
    private const string TargetEnvironmentVariable =
        "DUNGEONSTORY_LEDGER_B1_TARGET";

    public static void Run()
    {
        string target = Environment.GetEnvironmentVariable(
                TargetEnvironmentVariable)
            ?.Trim();
        if (string.IsNullOrEmpty(target))
        {
            throw new InvalidOperationException(
                TargetEnvironmentVariable + " is required.");
        }

        switch (target)
        {
            case "typed-evidence-anchor-restore":
                Require(WorkCompletionIdentityOutcomeDebugScenarios.RunAll(
                    logSuccess: true), target);
                EnvironmentControlExposureOutcomeDebugScenarios.RunAnchoredEvidenceRestore();
                KnowledgeResearchOutcomeDebugScenarios.RunAnchoredEvidenceRestore();
                break;
            case "migrated-production-entry-57":
            case "migrated-production-entry-58":
                Require(
                    MigratedProducerProductionEntry57E2EDebugScenarios
                        .RunAll(throwOnFailure: true),
                    target);
                break;
            case "causal-observed-career-arc-owner":
                Require(
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .RunCausalOwnerFamily(log: true),
                    target);
                break;
            case "dungeon-space-outcome":
                Require(
                    DungeonSpaceExpansionOutcomeDebugScenarios
                        .RunAll(logSuccess: true),
                    target);
                Require(
                    DungeonSpaceExpansionDebugScenarios
                        .RunOutcomeIntegrationScenarios(logSuccess: true),
                    target + ":integration");
                break;
            case "apparel-physical":
                ApparelPhysicalTransactionDebugScenarios.RunAll();
                break;
            case "apparel-rejected-dismantle":
                ApparelRejectedDismantlePhysicalTransactionDebugScenarios
                    .RunAll();
                break;
            case "blueprint-research":
                Require(
                    BlueprintResearchDebugScenarios.RunAll(logSuccess: true),
                    target);
                break;
            case "environment-crop-production-entry":
                Require(
                    EnvironmentCropProductionEntryDebugScenarios
                        .RunAll(log: true),
                    target);
                break;
            case "wildlife-food-raid-production-entry":
                _ = WildlifeFoodRaidOutboxDebugScenarios
                    .VerifyPendingDispositionOutbox();
                break;
            case "service-session-terminal-production-entry":
                Require(
                    ServiceSessionOutcomeDebugScenarios.RunAll(log: true),
                    target);
                break;
            case "policy-skill-equipment-outcomes":
                Require(
                    LedgerB1NewOutcomeDebugScenarios.RunAll(logSuccess: true),
                    target);
                break;
            case "evolution-typed-identities":
                Require(
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .RunAll(log: true),
                    target + ":acquired-trait-memory");
                Require(
                    FacilityEvolutionDebugScenarios.RunAll(log: true),
                    target + ":facility-evolution");
                break;
            case "age-condition-anatomy":
                Require(
                    AgeConditionAnatomyProjectionDebugScenarios
                        .RunAll(logSuccess: true),
                    target);
                break;
            case "disease-anatomy-burden":
                Require(
                    DiseaseAnatomyBurdenProjectionDebugScenarios
                        .RunAll(logSuccess: true),
                    target);
                break;
            case "environment-control-exposure":
                Require(
                    EnvironmentControlExposureOutcomeDebugScenarios
                        .RunAll(log: true),
                    target);
                break;
            case "visitor-exit-outcome":
                Require(
                    VisitorExitOutcomeDebugScenarios.RunAll(log: true),
                    target);
                break;
            case "facility-vandalism-outcome":
                Require(
                    FacilityVandalismOutcomeDebugScenarios.RunAll(log: true),
                    target);
                break;
            case "character-skill-activation-outcome":
                Require(
                    CharacterSkillActivationOutcomeDebugScenarios.RunAll(
                        log: true),
                    target);
                break;
            case "work-completion-identity-outcome":
                Require(
                    WorkCompletionIdentityOutcomeDebugScenarios.RunAll(
                        logSuccess: true),
                    target);
                break;
            case "offense-ultimate-atomic-public-entry":
                Require(
                    OffenseRewardDebugScenarios
                        .VerifyOffenseUltimateAtomicPublicEntry(),
                    target);
                break;
            case "retail-revenue-skill-provenance":
                Require(
                    MigratedProducerEconomyEntryExtendedE2EDebugScenarios
                        .VerifyRetailRevenueSkillProvenance(),
                    target);
                break;
            case "relationship-skill-provenance":
                Require(
                    CharacterAiPlanDebugScenarios
                        .VerifyRelationshipSkillProvenance(),
                    target);
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown B1 regression isolation target: " + target);
        }

        Debug.Log("LEDGER_B1_ISOLATION=PASS; target=" + target);
    }

    private static void Require(bool condition, string target)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "B1 isolated regression returned false: " + target);
        }
    }
}
#endif
