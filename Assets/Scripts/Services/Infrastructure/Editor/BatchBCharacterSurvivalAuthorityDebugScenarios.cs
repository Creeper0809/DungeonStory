#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BatchBCharacterSurvivalAuthorityDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Validation/Run Batch B Character Survival Authority")]
    public static void RunAll()
    {
        List<string> failures = new List<string>();
        failures.AddRange(SurvivalDebugScenarios.RunAll());
        failures.AddRange(CharacterDeprivationAuthorityDebugScenarios.RunAll());
        failures.AddRange(DarkSurvivalDebugScenarios.RunAll(false));
        BatchBSpeciesHusbandryDebugScenarios.RunAll();
        EnvironmentalFieldDebugScenarios.RunAll();
        if (!WildlifeDebugScenarios.RunAll(false))
        {
            failures.Add("Wildlife authored-content fixture failed.");
        }
        IReadOnlyList<string> combatFailures = CombatSystemDebugScenarios.ValidateAll();
        foreach (string combatFailure in combatFailures)
        {
            failures.Add($"Combat/body-health fixture failed: {combatFailure}");
        }
        if (!DungeonSaveSectionDebugScenarios.RunAll(false))
        {
            failures.Add("Atomic save-registry fixture failed.");
        }

        VerifySaveBoundary<
            CharacterBodyHealthSaveSection,
            DungeonCharacterBodyHealthSaveData>(
            DungeonCharacterBodyHealthSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<
            CharacterEnvironmentSaveSection,
            DungeonCharacterEnvironmentSaveData>(
            DungeonCharacterEnvironmentSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<
            CharacterConsumablesSaveSection,
            DungeonCharacterConsumablesSaveData>(
            DungeonCharacterConsumablesSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<SurvivalResourcesSaveSection, DungeonSurvivalSaveData>(
            DungeonSurvivalSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<DarkSurvivalSaveSection, DungeonDarkSurvivalSaveData>(
            DungeonDarkSurvivalSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<SpeciesRuntimeSaveSection, CharacterSpeciesRuntimeSaveData>(
            CharacterSpeciesRuntimeSaveData.CurrentVersion,
            failures);
        VerifySaveBoundary<AnimalHusbandrySaveSection, DungeonAnimalHusbandrySaveData>(
            DungeonAnimalHusbandrySaveData.CurrentVersion,
            failures);

        Type[] productionSections = TypeCache.GetTypesDerivedFrom<IDungeonSaveSection>()
            .Where(type => type != null && type.IsClass && !type.IsAbstract && type.IsPublic)
            .Where(type => type.Assembly.GetName().Name.IndexOf(
                "Editor", StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();
        int rollbackFree = productionSections.Count(type =>
            typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(type));
        if (productionSections.Length != 54
            || rollbackFree != productionSections.Length)
        {
            failures.Add(
                $"The final V18 boundary requires all 54 save sections to be rollback-free; found {rollbackFree}/{productionSections.Length}.");
        }

        IReadOnlyList<string> removedWrapperViolations =
            RuntimeAuthorityV18Validator
                .FindRemovedBroadRuntimeWrapperReferences();
        if (removedWrapperViolations.Count > 0)
        {
            failures.AddRange(removedWrapperViolations);
        }

        IReadOnlyList<string> runtimeFacetViolations =
            RuntimeAuthorityV18Validator.FindNarrowRuntimeFacetViolations();
        if (runtimeFacetViolations.Count > 0)
        {
            failures.AddRange(runtimeFacetViolations);
        }

        VerifyCharacterMedicalContracts(failures);

        try
        {
            RuntimeAuthorityV18Validator.ValidateOrThrow();
            BatchAArchitectureMetricsValidator.ValidateOrThrow();
        }
        catch (Exception exception)
        {
            failures.Add("Architecture/V18 ratchet failed: " + exception.Message);
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Batch B character/survival authority failed:\n"
                + string.Join("\n", failures));
        }

        Debug.Log(
            "BATCH_B_CHARACTER_SURVIVAL_AUTHORITY=PASS; "
            + $"save={rollbackFree}/54; strict=54/54; architecture=PASS");
    }

    private static void VerifySaveBoundary<TSection, TPayload>(
        int expectedVersion,
        ICollection<string> failures)
        where TSection : IDungeonSaveSection
        where TPayload : class, new()
    {
        Type sectionType = typeof(TSection);
        bool commonTypedBoundary = InheritsStrictTypedBoundary(
            sectionType,
            typeof(TPayload));
        bool explicitCandidateBoundary =
            typeof(IDungeonSaveSectionPreflight).IsAssignableFrom(sectionType)
            && typeof(IDungeonStagedSaveSection).IsAssignableFrom(sectionType);
        if ((!commonTypedBoundary && !explicitCandidateBoundary)
            || !typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(sectionType))
        {
            failures.Add(
                $"{sectionType.Name} is not a strict typed rollback-free boundary.");
        }

        object constant = typeof(TPayload).GetField("CurrentVersion")?
            .GetRawConstantValue();
        if (constant == null || Convert.ToInt32(constant) != expectedVersion)
        {
            failures.Add(
                $"{typeof(TPayload).Name} does not expose expected exact V{expectedVersion}.");
        }
    }

    private static bool InheritsStrictTypedBoundary(
        Type sectionType,
        Type payloadType)
    {
        for (Type current = sectionType; current != null; current = current.BaseType)
        {
            if (!current.IsGenericType
                || current.GetGenericTypeDefinition()
                    != typeof(DungeonStrictJsonSaveSection<,>))
            {
                continue;
            }

            return current.GetGenericArguments()[0] == payloadType;
        }

        return false;
    }

    private static void VerifyCharacterMedicalContracts(
        ICollection<string> failures)
    {
        Type bodyHealthRuntime = typeof(CharacterBodyHealthRuntime);
        Type[] requiredFacets =
        {
            typeof(ICharacterBodyHealthQuery),
            typeof(ICharacterBodyHealthCommand),
            typeof(ICharacterBodyHealthPersistence)
        };
        foreach (Type facet in requiredFacets)
        {
            if (!facet.IsAssignableFrom(bodyHealthRuntime))
            {
                failures.Add(
                    $"CharacterBodyHealthRuntime does not expose {facet.Name}.");
            }
        }

        if (bodyHealthRuntime.Assembly.GetType(
                "ICharacterBodyHealth" + "Runtime",
                throwOnError: false) != null)
        {
            failures.Add(
                "The broad body-health runtime authority still exists.");
        }

        Type byReferenceString = typeof(string).MakeByRefType();
        string[] surgeryUiOutStringMethods = typeof(CharacterSurgeryWindowService)
            .GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == byReferenceString))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (surgeryUiOutStringMethods.Length > 0)
        {
            failures.Add(
                "Surgery UI still exposes out-string data/failure paths: "
                + string.Join(", ", surgeryUiOutStringMethods));
        }

        if (typeof(SurgeryOrder).GetField("status") != null
            || typeof(SurgeryOrder).GetField("statusData")?.FieldType
                != typeof(SurgeryStatusData))
        {
            failures.Add(
                "SurgeryOrder must persist SurgeryStatusData instead of a display string.");
        }

        if (typeof(CharacterMedicalOrder).GetField("status") != null
            || typeof(CharacterMedicalOrder).GetField("statusCode")?.FieldType
                != typeof(CharacterMedicalStatusCode))
        {
            failures.Add(
                "CharacterMedicalOrder must persist CharacterMedicalStatusCode instead of a display string.");
        }
    }
}
#endif
