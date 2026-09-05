#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BatchCProductionInfrastructureAuthorityDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Validation/Run Batch C Production Infrastructure Authority")]
    public static void RunAll()
    {
        List<string> failures = new();

        Run("environmental field", EnvironmentalFieldDebugScenarios.RunAll, failures);
        Run("industrial infrastructure", IndustrialInfrastructureDebugScenarios.RunAll, failures);
        Run(
            "production workshop",
            () => failures.AddRange(ProductionWorkshopDebugScenarios.Validate()),
            failures);
        Run("production economy", ProductionEconomyDebugScenarios.RunAll, failures);

        if (!WorkAmountDebugScenarios.RunAll(false))
        {
            failures.Add("Concrete construction-material scenarios failed.");
        }
        failures.AddRange(BranchedProductionNetworkDebugScenarios.Validate());

        VerifySaveBoundary<
            PowerInfrastructureSaveSection,
            DungeonPowerInfrastructureSaveData>(failures);
        VerifySaveBoundary<
            FluidInfrastructureSaveSection,
            DungeonFluidInfrastructureSaveData>(failures);
        VerifySaveBoundary<
            ConveyorInfrastructureSaveSection,
            DungeonConveyorInfrastructureSaveData>(failures);
        VerifySaveBoundary<
            AutomationInfrastructureSaveSection,
            DungeonAutomationSaveData>(failures);
        VerifySaveBoundary<
            ProductionBillsSaveSection,
            DungeonProductionBillSaveData>(failures);
        VerifySaveBoundary<
            WasteProcessingSaveSection,
            DungeonWasteProcessingSaveData>(failures);
        VerifySaveBoundary<
            EnvironmentalFieldSaveSection,
            DungeonEnvironmentalFieldSaveData>(failures);

        Type[] productionSections = TypeCache
            .GetTypesDerivedFrom<IDungeonSaveSection>()
            .Where(type => type != null
                && type.IsClass
                && !type.IsAbstract
                && type.IsPublic)
            .Where(type => type.Assembly.GetName().Name.IndexOf(
                "Editor",
                StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();
        int rollbackFree = productionSections.Count(type =>
            typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(type));
        if (productionSections.Length != 74
            || rollbackFree != productionSections.Length)
        {
            failures.Add(
                "V20 requires all 74 production save sections to be rollback-free; "
                + $"found {rollbackFree}/{productionSections.Length}.");
        }

        failures.AddRange(
            RuntimeAuthorityV18Validator
                .FindRemovedBroadRuntimeWrapperReferences());
        failures.AddRange(
            RuntimeAuthorityV18Validator.FindNarrowRuntimeFacetViolations());
        Run(
            "Batch C final save boundary",
            () => RuntimeAuthorityV18Validator
                .ValidateBatchCFinalSaveBoundaryOrThrow(),
            failures);

        Run(
            "V19 authority",
            () => RuntimeAuthorityV18Validator.ValidateOrThrow(),
            failures);
        Run(
            "architecture metrics",
            () => BatchAArchitectureMetricsValidator.ValidateOrThrow(),
            failures);

        failures = failures
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(failure => failure, StringComparer.Ordinal)
            .ToList();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Batch C production/infrastructure authority failed:\n"
                + string.Join("\n", failures));
        }

        Debug.Log(
            "BATCH_C_PRODUCTION_INFRASTRUCTURE_AUTHORITY=PASS; "
            + $"save={rollbackFree}/74; strict=74/74; graph=PASS; architecture=PASS");
    }

    private static void VerifySaveBoundary<TSection, TPayload>(
        ICollection<string> failures)
        where TSection : IDungeonSaveSection
        where TPayload : class
    {
        Type sectionType = typeof(TSection);
        if (typeof(IOptionalDungeonSaveSection).IsAssignableFrom(sectionType)
            || !typeof(IDungeonSaveSectionPreflight).IsAssignableFrom(sectionType)
            || !typeof(IDungeonStagedSaveSection).IsAssignableFrom(sectionType)
            || !typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(sectionType))
        {
            failures.Add(
                $"{sectionType.Name} is not a required staged rollback-free boundary.");
        }

        object version = typeof(TPayload).GetField("CurrentVersion")?
            .GetRawConstantValue();
        if (version == null || Convert.ToInt32(version) <= 0)
        {
            failures.Add(
                $"{typeof(TPayload).Name} has no positive exact CurrentVersion.");
        }
    }

    private static void Run(
        string label,
        Action action,
        ICollection<string> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add($"{label}: {exception.Message}");
        }
    }
}
#endif
