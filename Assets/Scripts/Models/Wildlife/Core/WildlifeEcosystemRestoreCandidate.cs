using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeEcosystemRestoreCandidate
{
    internal WildlifeEcosystemRestoreCandidate(
        IWildlifeGridPort grid,
        float recentHuntPressure,
        float recentPredationPressure,
        float nextGlobalRespawnAt,
        Dictionary<string, float> speciesRespawnAt,
        List<WildlifeHabitatPatch> patches)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        RecentHuntPressure = recentHuntPressure;
        RecentPredationPressure = recentPredationPressure;
        NextGlobalRespawnAt = nextGlobalRespawnAt;
        SpeciesRespawnAt = speciesRespawnAt
            ?? throw new ArgumentNullException(nameof(speciesRespawnAt));
        Patches = patches ?? throw new ArgumentNullException(nameof(patches));
    }

    internal IWildlifeGridPort Grid { get; }
    internal float RecentHuntPressure { get; }
    internal float RecentPredationPressure { get; }
    internal float NextGlobalRespawnAt { get; }
    internal Dictionary<string, float> SpeciesRespawnAt { get; }
    internal List<WildlifeHabitatPatch> Patches { get; }
}

public sealed class WildlifeEcosystemRestoreTransaction
{
    private readonly WildlifeEcosystemRuntime owner;
    private Action rollback;
    private Action complete;

    internal WildlifeEcosystemRestoreTransaction(
        WildlifeEcosystemRuntime owner,
        Action rollback,
        Action complete)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal void Rollback(WildlifeEcosystemRuntime expectedOwner)
    {
        Action action = RequireActive(expectedOwner, rollback);
        action();
        rollback = null;
        complete = null;
    }

    internal void Complete(WildlifeEcosystemRuntime expectedOwner)
    {
        Action action = RequireActive(expectedOwner, complete);
        action();
        complete = null;
        rollback = null;
    }

    private Action RequireActive(
        WildlifeEcosystemRuntime expectedOwner,
        Action action)
    {
        if (!ReferenceEquals(owner, expectedOwner) || action == null)
        {
            throw new InvalidOperationException(
                "Wildlife ecosystem restore transaction has the wrong owner or is already finished.");
        }

        return action;
    }
}
