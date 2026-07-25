#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterProgressionSavePlayModeFacade
{
    [MenuItem("DungeonStory/Debug/Character/Run Progression Save Round Trip")]
    public static void RunFromMenu()
    {
        if (!Run(out string message))
        {
            throw new InvalidOperationException(message);
        }

        Debug.Log(message);
    }

    public static bool Run(out string message)
    {
        if (!Application.isPlaying)
        {
            message = "Character progression save verification requires PlayMode.";
            return false;
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            message = "Dungeon runtime container is missing.";
            return false;
        }

        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        IOwnerRunManagerProvider ownerProvider = scope.Container.Resolve<IOwnerRunManagerProvider>();
        if (!ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager.CurrentOwnerActor == null)
        {
            message = "Owner runtime is missing.";
            return false;
        }

        DungeonGameSaveData baseline = saveService.Capture();
        try
        {
            CharacterActor owner = ownerManager.CurrentOwnerActor;
            owner.EnsureRuntimeState();
            CharacterProgressionSnapshot initialProgression = owner.Progression.CapturePersistentState();
            owner.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
                4,
                77,
                initialProgression.GrowthState,
                initialProgression.NarrativeLedger));
            CharacterProgressionSnapshot expectedProgression = owner.Progression.CapturePersistentState();
            string expectedGrowthJson = JsonUtility.ToJson(expectedProgression.GrowthState);
            string expectedNarrativeJson = JsonUtility.ToJson(expectedProgression.NarrativeLedger);

            DungeonGameSaveData captured = saveService.Capture();
            DungeonCharacterWorldSaveData savedCharacters =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                    captured,
                    CharacterWorldSaveSection.Id);
            DungeonCharacterSaveData savedOwner = savedCharacters.actors
                .FirstOrDefault(actor => actor != null && actor.isOwner);
            if (savedOwner == null
                || savedOwner.level != 4
                || savedOwner.currentExperience != 77
                || JsonUtility.ToJson(savedOwner.growth) != expectedGrowthJson
                || JsonUtility.ToJson(savedOwner.narrative) != expectedNarrativeJson)
            {
                message = "Captured game save did not contain the exact owner progression state.";
                return false;
            }

            DungeonGameSaveData parsed = saveService.FromJson(saveService.ToJson(captured, prettyPrint: true));
            DungeonGameSaveData incompatible = saveService.FromJson(saveService.ToJson(captured));
            incompatible.version = DungeonGameSaveData.CurrentVersion - 1;
            if (saveService.TryRestore(incompatible, out DungeonGameRestoreReport incompatibleReport)
                || !incompatibleReport.Errors.Any(error => error.Contains("호환", StringComparison.Ordinal)))
            {
                message = "Legacy growth save was not rejected with the new-game compatibility message.";
                return false;
            }

            owner.Progression.RestorePersistentState(1, 0, null, null);
            if (!saveService.TryRestore(parsed, out DungeonGameRestoreReport report))
            {
                message = "Progression game-save restore failed: " + string.Join(" | ", report.Errors);
                return false;
            }

            if (!ownerProvider.TryGetManager(out ownerManager)
                || ownerManager.CurrentOwnerActor == null)
            {
                message = "Restored owner runtime is missing.";
                return false;
            }

            CharacterProgression restored = ownerManager.CurrentOwnerActor.Progression;
            if (restored == null
                || restored.Level != 4
                || restored.CurrentExperience != 77
                || JsonUtility.ToJson(restored.GrowthState) != expectedGrowthJson
                || JsonUtility.ToJson(restored.NarrativeLedger) != expectedNarrativeJson)
            {
                message = restored == null
                    ? "Restored owner has no progression component."
                    : $"Progression mismatch after restore: Lv.{restored.Level}, XP {restored.CurrentExperience}, active={restored.ActiveSkills.Count}, passive={restored.PassiveSkills.Count}";
                return false;
            }

            message = $"CHARACTER_PROGRESSION_SAVE_ROUND_TRIP_PASSED Lv.{restored.Level} XP={restored.CurrentExperience} active={restored.ActiveSkills.Count} passive={restored.PassiveSkills.Count} legacyRejected=true warnings={report.Warnings.Count}";
            return true;
        }
        finally
        {
            if (!saveService.TryRestore(baseline, out DungeonGameRestoreReport baselineReport))
            {
                Debug.LogError("Failed to restore the progression verification baseline: "
                    + string.Join(" | ", baselineReport.Errors));
            }
        }
    }
}
#endif
