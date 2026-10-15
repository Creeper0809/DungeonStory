#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public static class MigratedProducerProductionEntry57E2EDebugScenarios
{
    public static void RunBatch()
    {
        Require(RunAll(),
            "Migrated production-entry 72/72 batch returned false.");
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyExactCoverage();
            Require(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunAll(),
                "Combat/defense migrated production-entry scenarios failed.");
            Require(
                MigratedProducerRunOffenseEntryE2EDebugScenarios.RunAll(),
                "Run/offense migrated production-entry scenarios failed.");
            Require(
                MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.RunAll(),
                "Character/survival migrated production-entry scenarios failed.");
            Require(
                MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunAll(),
                "Economy migrated production-entry scenarios failed.");
            Require(
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunAll(),
                "Economy/service migrated production-entry scenarios failed.");
            Require(
                WorkOrderTerminalOutcomeProductionEntryE2EDebugScenarios.RunAll(),
                "Work-order terminal migrated production-entry scenarios failed.");
            Require(
                SeasonalWildlifeArrivalOutcomeDebugScenarios.RunAll(),
                "Seasonal wildlife arrival migrated production-entry scenarios failed.");
            Require(
                SeasonalDriftCargoOutcomeDebugScenarios.RunAll(),
                "Seasonal drift-cargo migrated production-entry scenarios failed.");
            Require(
                ApparelWorkOrderOutcomeDebugScenarios.RunAll(),
                "Apparel work-order migrated production-entry scenarios failed.");
            Require(
                CombatEquipmentCraftCompletionOutcomeDebugScenarios.RunAll(),
                "Combat equipment craft completion migrated production-entry scenarios failed.");
            Require(
                RepairWorkOutcomeDebugScenarios.RunAll(),
                "Repair work migrated production-entry scenarios failed.");
            Require(
                WorldResourceCycleOutcomeDebugScenarios.RunAll(),
                "World resource migrated production-entry scenarios failed.");
            Require(
                AnimalHusbandryOutcomeDebugScenarios.RunAll(),
                "Animal husbandry migrated production-entry scenarios failed.");
            Require(
                ResourceStockPolicySaleOutcomeE2EDebugScenarios.RunAll(),
                "Stock-policy sale migrated production-entry scenarios failed.");
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyExactCoverage()
    {
        MigratedProducerOutcomeKind[] observed =
            MigratedProducerCombatDefenseEntryE2EDebugScenarios.CoveredKinds
                .Concat(MigratedProducerRunOffenseEntryE2EDebugScenarios.CoveredKinds)
                .Concat(MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.CoveredKinds)
                .Concat(MigratedProducerEconomyEntryExtendedE2EDebugScenarios.CoveredKinds)
                .Concat(MigratedProducerEconomyServiceEntryE2EDebugScenarios.CoveredKinds)
                .Concat(WorkOrderTerminalOutcomeProductionEntryE2EDebugScenarios.CoveredKinds)
                .Concat(SeasonalWildlifeArrivalOutcomeDebugScenarios.CoveredKinds)
                .Concat(SeasonalDriftCargoOutcomeDebugScenarios.CoveredKinds)
                .Concat(ApparelWorkOrderOutcomeDebugScenarios.CoveredKinds)
                .Concat(CombatEquipmentCraftCompletionOutcomeDebugScenarios
                    .CoveredKinds)
                .Concat(RepairWorkOutcomeDebugScenarios.CoveredKinds)
                .Concat(WorldResourceCycleOutcomeDebugScenarios.CoveredKinds)
                .Concat(AnimalHusbandryOutcomeDebugScenarios.CoveredKinds)
                .Concat(ResourceStockPolicySaleOutcomeE2EDebugScenarios.CoveredKinds)
                .ToArray();
        MigratedProducerOutcomeKind[] expected =
            MigratedProducerOutcomeCatalog.Definitions
                .Select(definition => definition.Kind)
                .OrderBy(kind => (int)kind)
                .ToArray();

        MigratedProducerOutcomeKind[] duplicates = observed
            .GroupBy(kind => kind)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .OrderBy(kind => (int)kind)
            .ToArray();
        MigratedProducerOutcomeKind[] missing = expected.Except(observed)
            .OrderBy(kind => (int)kind)
            .ToArray();
        MigratedProducerOutcomeKind[] unexpected = observed.Except(expected)
            .OrderBy(kind => (int)kind)
            .ToArray();

        if (observed.Length != 72
            || expected.Length != 72
            || duplicates.Length > 0
            || missing.Length > 0
            || unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                "Migrated production-entry coverage is not exactly 72/72: "
                + "observed=" + observed.Length
                + "; expected=" + expected.Length
                + "; duplicates=" + Join(duplicates)
                + "; missing=" + Join(missing)
                + "; unexpected=" + Join(unexpected));
        }
    }

    private static string Join(IEnumerable<MigratedProducerOutcomeKind> kinds) =>
        string.Join(",", kinds.Select(kind => kind.ToString()));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
