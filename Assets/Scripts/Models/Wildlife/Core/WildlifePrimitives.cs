using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeState
{
    Idle = 0,
    Grazing = 1,
    Fleeing = 2,
    Hunted = 3,
    Retaliating = 4,
    PredatorStalking = 5,
    Dead = 6,
    Leaving = 7
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeHabitatType
{
    Grass = 0,
    Water = 1,
    Burrow = 2,
    Brush = 3,
    Lair = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeDietType
{
    Herbivore = 0,
    Omnivore = 1,
    Carnivore = 2,
    Scavenger = 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WildlifeIntent
{
    Wander = 0,
    Forage = 1,
    Drink = 2,
    Rest = 3,
    ReturnToTerritory = 4,
    HuntPrey = 5,
    Flee = 6,
    LeaveMap = 7
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeButcherYield
{
    public string itemId = string.Empty;
    [Min(0)] public int amount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeSaveData
{
    public string wildlifeId = string.Empty;
    public string speciesId = string.Empty;
    public int health;
    public WildlifeState state = WildlifeState.Idle;
    public int gridX;
    public int gridY;
    public bool huntDesignated;
    public bool priorityHunt;
    public string reservedByPersistentId = string.Empty;
    public float fear;
    public float hunger;
    public float thirst;
    public WildlifeIntent intent = WildlifeIntent.Wander;
    public string intentReason = string.Empty;
    public bool hasTerritory;
    public int territoryX;
    public int territoryY;
    public bool hasHerdAnchor;
    public int herdAnchorX;
    public int herdAnchorY;
    public bool hasLastThreat;
    public int lastThreatX;
    public int lastThreatY;
    public bool hasCombatBodyProfile;
    public float headHealth;
    public float torsoHealth;
    public float limbHealth;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeCarcassFreshnessSaveData
{
    public string stackId = string.Empty;
    public string speciesId = string.Empty;
    public float remainingFreshnessSeconds;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHabitatPatchSaveData
{
    public string patchId = string.Empty;
    public string linkedWaterSourceId = string.Empty;
    public WildlifeHabitatType habitatType = WildlifeHabitatType.Grass;
    public int gridX;
    public int gridY;
    public int radius = 2;
    public float resourceCapacity = 1f;
    public float currentResource = 1f;
    public float regenPerSecond = 0.02f;
    public float danger;
    public List<string> preferredSpeciesTags = new List<string>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeSpeciesRespawnSaveData
{
    public string speciesId = string.Empty;
    public float remainingSeconds;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonWildlifeEcosystemSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public float recentHuntPressure;
    public float recentPredationPressure;
    public float globalRespawnRemainingSeconds;
    public List<WildlifeSpeciesRespawnSaveData> speciesRespawns =
        new List<WildlifeSpeciesRespawnSaveData>();
    public List<WildlifeHabitatPatchSaveData> patches =
        new List<WildlifeHabitatPatchSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonWildlifeSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int nextSequence = 1;
    public List<WildlifeSaveData> wildlife = new List<WildlifeSaveData>();
    public List<WildlifeCarcassFreshnessSaveData> carcasses =
        new List<WildlifeCarcassFreshnessSaveData>();
    public DungeonWildlifeEcosystemSaveData ecosystem =
        new DungeonWildlifeEcosystemSaveData();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct WildlifeEcosystemOverview
{
    public WildlifeEcosystemOverview(
        int patchCount,
        int grassPatchCount,
        int waterPatchCount,
        float foodAbundance01,
        float waterAbundance01,
        float predatorDanger01,
        float crowding01,
        int desiredWildlifeCount,
        int aliveWildlifeCount,
        float respawnRemainingSeconds)
    {
        PatchCount = Mathf.Max(0, patchCount);
        GrassPatchCount = Mathf.Max(0, grassPatchCount);
        WaterPatchCount = Mathf.Max(0, waterPatchCount);
        FoodAbundance01 = Mathf.Clamp01(foodAbundance01);
        WaterAbundance01 = Mathf.Clamp01(waterAbundance01);
        PredatorDanger01 = Mathf.Clamp01(predatorDanger01);
        Crowding01 = Mathf.Clamp01(crowding01);
        DesiredWildlifeCount = Mathf.Max(0, desiredWildlifeCount);
        AliveWildlifeCount = Mathf.Max(0, aliveWildlifeCount);
        RespawnRemainingSeconds = Mathf.Max(0f, respawnRemainingSeconds);
    }

    public int PatchCount { get; }
    public int GrassPatchCount { get; }
    public int WaterPatchCount { get; }
    public float FoodAbundance01 { get; }
    public float WaterAbundance01 { get; }
    public float PredatorDanger01 { get; }
    public float Crowding01 { get; }
    public int DesiredWildlifeCount { get; }
    public int AliveWildlifeCount { get; }
    public float RespawnRemainingSeconds { get; }
}
