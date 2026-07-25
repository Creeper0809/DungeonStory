using System;
using UnityEngine;

[Serializable]
public struct FacilityNeedRecoveryData
{
    public float sleep;
    public float mood;
    public float fun;
    public float hunger;
    public float excretion;
    public float hygiene;

    public bool HasEffect => !Mathf.Approximately(sleep, 0f)
        || !Mathf.Approximately(mood, 0f)
        || !Mathf.Approximately(fun, 0f)
        || !Mathf.Approximately(hunger, 0f)
        || !Mathf.Approximately(excretion, 0f)
        || !Mathf.Approximately(hygiene, 0f);
}

[Serializable]
public sealed class FacilityRuntimeState
{
    [Min(0)] public int completedUses;
    [Min(0)] public int completedWorkCycles;
    [Range(0f, 100f)] public float cleanliness = 100f;

    public FacilityRuntimeState Clone()
    {
        return new FacilityRuntimeState
        {
            completedUses = completedUses,
            completedWorkCycles = completedWorkCycles,
            cleanliness = cleanliness
        };
    }

    public void CopyFrom(FacilityRuntimeState source)
    {
        if (source == null)
        {
            completedUses = 0;
            completedWorkCycles = 0;
            cleanliness = 100f;
            return;
        }

        completedUses = Mathf.Max(0, source.completedUses);
        completedWorkCycles = Mathf.Max(0, source.completedWorkCycles);
        cleanliness = Mathf.Clamp(source.cleanliness, 0f, 100f);
    }
}

[Serializable]
public sealed class LegacyFacilityOperationalStateV1
{
    [Min(0)] public int completedUses;
    [Min(0)] public int completedWorkCycles;
    [Min(0)] public int producedStock;
    [Min(0)] public int alarmCharges;
    [Range(0f, 100f)] public float cleanliness = 100f;

}

public interface IBuildingUnlockStateView
{
    bool IsBuildingUnlocked(int buildingId);
}

public static class FacilityProgression
{
    public static int GetCurrentPhase(GameData gameData)
    {
        int day = gameData != null && gameData.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
        return Mathf.Clamp(1 + ((day - 1) / 3), 1, 3);
    }

    public static bool IsUnlocked(
        BuildingSO building,
        GameData gameData,
        IBuildingUnlockStateView unlockState = null)
    {
        if (building == null)
        {
            return false;
        }

        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.IgnoreUnlocks))
        {
            return true;
        }

        if (unlockState != null && unlockState.IsBuildingUnlocked(building.id))
        {
            return true;
        }

        if (!building.unlocked)
        {
            return false;
        }

        return !building.IsModularFacility() || GetCurrentPhase(gameData) >= building.GetUnlockPhase();
    }

    public static int GetRefund(BuildingSO building)
    {
        if (building == null)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.FloorToInt(
            building.GetConstructionCost() * building.GetDemolitionRefundRate()));
    }
}
