#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class TreasuryEconomySaveDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Economy/Verify Treasury Strict Save")]
    public static void RunAll()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Play Mode is required.");
        }
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope?.Container == null)
        {
            throw new InvalidOperationException("Runtime scope is missing.");
        }

        ITreasuryEconomyPersistence persistence =
            scope.Container.Resolve<ITreasuryEconomyPersistence>();
        TreasuryEconomySaveSection section =
            new TreasuryEconomySaveSection(persistence);
        Require(
            section is IDungeonSaveSectionPreflight
            && section is IDungeonRollbackFreeSaveSection,
            "Treasury save section is not strict and rollback-free.");

        string before = section.Capture();
        TreasuryEconomySaveData valid =
            JsonUtility.FromJson<TreasuryEconomySaveData>(before);
        ((IDungeonSaveSectionPreflight)section).ValidatePayload(
            before,
            section.SectionVersion,
            new DungeonGameRestoreReport());

        TreasuryEconomySaveData invalid =
            JsonUtility.FromJson<TreasuryEconomySaveData>(before);
        Require(invalid?.autoProcurement != null, "Treasury fixture is missing.");
        invalid.autoProcurement.dailyBudget = -1;
        bool rejected = false;
        try
        {
            ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                JsonUtility.ToJson(invalid),
                section.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Negative treasury budget was accepted.");
        Require(
            string.Equals(before, section.Capture(), StringComparison.Ordinal),
            "Failed treasury preflight mutated live state.");
        Require(
            valid.version == TreasuryEconomySaveData.CurrentVersion,
            "Treasury capture did not emit the current payload version.");
        Debug.Log("BATCH_D_TREASURY_SAVE=PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
