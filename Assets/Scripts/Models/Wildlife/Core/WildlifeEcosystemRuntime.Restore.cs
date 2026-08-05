using System;
using System.Collections.Generic;

public sealed partial class WildlifeEcosystemRuntime
{
    public WildlifeEcosystemRestoreCandidate PrepareRestoreCandidate(
        DungeonWildlifeEcosystemSaveData saveData,
        IWildlifeGridPort restoreGrid)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        if (restoreGrid == null)
        {
            throw new ArgumentNullException(nameof(restoreGrid));
        }

        if (saveData.version != DungeonWildlifeEcosystemSaveData.CurrentVersion
            || saveData.speciesRespawns == null
            || saveData.patches == null
            || !IsFiniteAtLeast(saveData.recentHuntPressure, 0f)
            || !IsFiniteAtLeast(saveData.recentPredationPressure, 0f)
            || !IsFiniteAtLeast(saveData.globalRespawnRemainingSeconds, 0f))
        {
            throw new InvalidOperationException(
                "Wildlife ecosystem restore candidate is not canonical.");
        }

        float now = gameClock.Time;
        Dictionary<string, float> candidateRespawns =
            new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (WildlifeSpeciesRespawnSaveData entry in saveData.speciesRespawns)
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.speciesId)
                || !IsFiniteAtLeast(entry.remainingSeconds, 0f)
                || !candidateRespawns.TryAdd(
                    entry.speciesId,
                    now + entry.remainingSeconds))
            {
                throw new InvalidOperationException(
                    "Wildlife ecosystem restore candidate contains an invalid species respawn.");
            }
        }

        List<WildlifeHabitatPatch> candidatePatches =
            new List<WildlifeHabitatPatch>(saveData.patches.Count);
        foreach (WildlifeHabitatPatchSaveData entry in saveData.patches)
        {
            WildlifeHabitatPatch patch = WildlifeHabitatPatch.FromSave(entry);
            if (patch == null || !IsPatchOnUsableExterior(restoreGrid, patch))
            {
                throw new InvalidOperationException(
                    $"Wildlife ecosystem restore candidate contains an unusable habitat patch '{entry?.patchId}'.");
            }

            candidatePatches.Add(patch);
        }

        return new WildlifeEcosystemRestoreCandidate(
            restoreGrid,
            saveData.recentHuntPressure,
            saveData.recentPredationPressure,
            now + saveData.globalRespawnRemainingSeconds,
            candidateRespawns,
            candidatePatches);
    }

    public void PublishRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate)
    {
        WildlifeEcosystemRestoreTransaction transaction =
            ApplyRestoreCandidate(candidate);
        CompleteRestore(transaction);
    }

    public WildlifeEcosystemRestoreTransaction ApplyRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        float previousRecentHuntPressure = recentHuntPressure;
        float previousRecentPredationPressure = recentPredationPressure;
        float previousNextGlobalRespawnAt = nextGlobalRespawnAt;
        Dictionary<string, float> previousSpeciesRespawnAt = speciesRespawnAt;
        List<WildlifeHabitatPatch> previousPatches = patches;
        bool previousInitialized = initialized;
        IWildlifeGridPort previousInitializedGrid = initializedGrid;
        float previousNextPatchTickAt = nextPatchTickAt;
        float previousNextOverlayRefreshAt = nextOverlayRefreshAt;
        bool previousDerivedPresentationDirty = derivedPresentationDirty;
        float restoredNextPatchTickAt = gameClock.Time + PatchTickInterval;
        float restoredNextOverlayRefreshAt = gameClock.Time + OverlayRefreshInterval;

        WildlifeEcosystemRestoreTransaction transaction =
            new WildlifeEcosystemRestoreTransaction(
                this,
                rollback: () =>
                {
                    RequireAppliedRestoreCandidate(candidate);
                    recentHuntPressure = previousRecentHuntPressure;
                    recentPredationPressure = previousRecentPredationPressure;
                    nextGlobalRespawnAt = previousNextGlobalRespawnAt;
                    speciesRespawnAt = previousSpeciesRespawnAt;
                    patches = previousPatches;
                    initialized = previousInitialized;
                    initializedGrid = previousInitializedGrid;
                    nextPatchTickAt = previousNextPatchTickAt;
                    nextOverlayRefreshAt = previousNextOverlayRefreshAt;
                    derivedPresentationDirty = previousDerivedPresentationDirty;
                },
                complete: () =>
                {
                    RequireAppliedRestoreCandidate(candidate);
                    try
                    {
                        presentation.Clear();
                    }
                    catch
                    {
                        // Presentation is derived and cannot invalidate a committed root swap.
                    }
                });

        recentHuntPressure = candidate.RecentHuntPressure;
        recentPredationPressure = candidate.RecentPredationPressure;
        nextGlobalRespawnAt = candidate.NextGlobalRespawnAt;
        speciesRespawnAt = candidate.SpeciesRespawnAt;
        patches = candidate.Patches;
        initialized = true;
        initializedGrid = candidate.Grid;
        nextPatchTickAt = restoredNextPatchTickAt;
        nextOverlayRefreshAt = restoredNextOverlayRefreshAt;
        derivedPresentationDirty = true;
        return transaction;
    }

    public void RollbackRestore(
        WildlifeEcosystemRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Rollback(this);
    }

    public void CompleteRestore(
        WildlifeEcosystemRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Complete(this);
    }

    private void RequireAppliedRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate)
    {
        if (candidate == null
            || !ReferenceEquals(speciesRespawnAt, candidate.SpeciesRespawnAt)
            || !ReferenceEquals(patches, candidate.Patches)
            || !ReferenceEquals(initializedGrid, candidate.Grid))
        {
            throw new InvalidOperationException(
                "Wildlife ecosystem restore transaction is no longer the active state.");
        }
    }

    private void RebuildDerivedPresentationIfNeeded(IWildlifeGridPort grid)
    {
        if (!derivedPresentationDirty || grid == null)
        {
            return;
        }

        derivedPresentationDirty = false;
        presentation.Rebuild(grid, patches);

        if (presentation.OverlayEnabled)
        {
            presentation.RefreshOverlay(grid, patches);
        }
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum;
    }
}
