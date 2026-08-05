using System;
using UnityEditor;
using UnityEngine;

public static class StrictProgressionCombatSaveDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run Strict Progression Combat Save Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Strict progression/combat save scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        bool markerContracts = VerifyMarkerContracts();
        bool evolutionRestore = VerifyEvolutionRestoreIsStrictAndDetached();
        bool success = markerContracts && evolutionRestore;
        if (success && logSuccess)
        {
            Debug.Log(
                "STRICT_PROGRESSION_COMBAT_SAVE_SCENARIOS_PASSED sections=4 invalidMutation=0 legacyAccepted=0");
        }

        return success;
    }

    private static bool VerifyMarkerContracts()
    {
        Type[] sectionTypes =
        {
            typeof(BlueprintResearchSaveSection),
            typeof(CombatEquipmentSaveSection),
            typeof(EquipmentEvolutionSaveSection),
            typeof(MetaProgressionSaveSection)
        };
        foreach (Type sectionType in sectionTypes)
        {
            if (!typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(sectionType)
                || !typeof(IDungeonSaveSectionPreflight).IsAssignableFrom(sectionType)
                || !typeof(IDungeonStagedSaveSection).IsAssignableFrom(sectionType))
            {
                Debug.LogError($"Strict save contracts are missing from {sectionType.Name}.");
                return false;
            }
        }

        return true;
    }

    private static bool VerifyEvolutionRestoreIsStrictAndDetached()
    {
        EvolutionPersistenceFake persistence = new EvolutionPersistenceFake();
        EquipmentEvolutionSaveSection section =
            new EquipmentEvolutionSaveSection(persistence);
        string validJson = JsonUtility.ToJson(new EquipmentEvolutionSaveData());

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        IDungeonSaveRestoreStage staged = section.StageRestore(
            validJson,
            3,
            report);
        if (persistence.CommitCount != 0)
        {
            Debug.LogError("Equipment evolution staging mutated the live aggregate.");
            return false;
        }

        staged.Commit(report);
        if (persistence.CommitCount != 1)
        {
            Debug.LogError("Equipment evolution candidate was not committed exactly once.");
            return false;
        }

        EquipmentEvolutionSaveData invalid = new EquipmentEvolutionSaveData();
        invalid.reforgeOrders.Add(new EvolutionReforgeOrder
        {
            orderId = " invalid-order "
        });
        bool invalidRejected = ThrowsInvalidOperation(() =>
            section.StageRestore(
                JsonUtility.ToJson(invalid),
                3,
                new DungeonGameRestoreReport()));
        bool legacyRejected = ThrowsInvalidOperation(() =>
            section.StageRestore(
                validJson,
                2,
                new DungeonGameRestoreReport()));

        if (!invalidRejected || !legacyRejected || persistence.CommitCount != 1)
        {
            Debug.LogError(
                "Equipment evolution accepted an invalid/legacy payload or mutated live state.");
            return false;
        }

        return true;
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class EvolutionPersistenceFake : IEquipmentEvolutionPersistence
    {
        public int CommitCount { get; private set; }

        public EquipmentEvolutionSaveData Capture() =>
            new EquipmentEvolutionSaveData();

        public EquipmentEvolutionRestoreCandidate BuildRestoreCandidate(
            EquipmentEvolutionSaveData saveData) =>
            EquipmentEvolutionRestoreBuilder.Build(saveData);

        public void PublishRestoreCandidate(
            EquipmentEvolutionRestoreCandidate candidate)
        {
            _ = candidate ?? throw new ArgumentNullException(nameof(candidate));
            CommitCount++;
        }
    }
}
