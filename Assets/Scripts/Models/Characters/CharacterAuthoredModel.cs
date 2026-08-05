using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterStatEntry
{
    public string statId;
    public int value;

    public CharacterStatEntry()
    {
    }

    public CharacterStatEntry(string statId, int value)
    {
        this.statId = statId;
        this.value = value;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class CharacterStatBlock
{
    [SerializeField]
    private List<CharacterStatEntry> entries = new();

    public IReadOnlyList<CharacterStatEntry> Entries =>
        entries ??= new List<CharacterStatEntry>();

    public bool HasAnyValue => Entries.Any(entry => entry != null && entry.value != 0);

    public static CharacterStatBlock CreateDefault(int value = 5)
    {
        CharacterStatBlock block = new();
        foreach (CharacterStatDefinition definition in CharacterStatCatalog.All)
        {
            block.Set(definition.Id, value);
        }

        return block;
    }

    public int Get(CharacterStatType type)
    {
        return Get(CharacterStatCatalog.GetRequired(type).Id);
    }

    public int Get(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId) || entries == null)
        {
            return 0;
        }

        int total = 0;
        foreach (CharacterStatEntry entry in entries)
        {
            if (entry != null
                && string.Equals(entry.statId, statId, StringComparison.Ordinal))
            {
                total += entry.value;
            }
        }

        return total;
    }

    public bool Contains(string statId)
    {
        return !string.IsNullOrWhiteSpace(statId)
            && entries != null
            && entries.Any(entry => entry != null
                && string.Equals(entry.statId, statId, StringComparison.Ordinal));
    }

    public void Set(CharacterStatType type, int value)
    {
        Set(CharacterStatCatalog.GetRequired(type).Id, value);
    }

    public void Set(string statId, int value)
    {
        string normalizedId = NormalizeId(statId);
        entries ??= new List<CharacterStatEntry>();
        CharacterStatEntry existing = entries.FirstOrDefault(entry => entry != null
            && string.Equals(entry.statId, normalizedId, StringComparison.Ordinal));
        if (existing == null)
        {
            entries.Add(new CharacterStatEntry(normalizedId, value));
            return;
        }

        existing.value = value;
        entries.RemoveAll(entry => entry != null
            && !ReferenceEquals(entry, existing)
            && string.Equals(entry.statId, normalizedId, StringComparison.Ordinal));
    }

    public void Add(string statId, int value)
    {
        Set(statId, Get(statId) + value);
    }

    public void Add(CharacterStatBlock other)
    {
        if (other == null)
        {
            return;
        }

        foreach (CharacterStatEntry entry in other.Entries)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.statId))
            {
                Add(entry.statId, entry.value);
            }
        }
    }

    private static string NormalizeId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            throw new ArgumentException("Character stat id is required.", nameof(statId));
        }

        return statId.Trim();
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class CharacterModelModifiers
{
    [Min(0f)] public float consumptionMultiplier = 1f;
    [Min(0f)] public float spendingMultiplier = 1f;
    [Min(0f)] public float waitPatienceMultiplier = 1f;
    [Min(0f)] public float crowdSensitivityMultiplier = 1f;
    [Min(0f)] public float accidentChanceMultiplier = 1f;
    [Min(0f)] public float workSpeedMultiplier = 1f;
    [Min(0f)] public float researchSpeedMultiplier = 1f;
    [Min(0f)] public float combatPowerMultiplier = 1f;
    [Min(0f)] public float moveSpeedMultiplier = 1f;
    [Min(0f)] public float stayDurationMultiplier = 1f;
    public FacilityRole preferredFacilityRoles;
    public FacilityRole dislikedFacilityRoles;
    [SerializeField] internal FacilityWorkType preferredWorkTypes;
    [SerializeField] internal FacilityWorkType dislikedWorkTypes;

    public IEnumerable<WorkTypeId> PreferredWorkTypeIds =>
        EnumerateWorkTypeIds(preferredWorkTypes);
    public IEnumerable<WorkTypeId> DislikedWorkTypeIds =>
        EnumerateWorkTypeIds(dislikedWorkTypes);
    public FacilityWorkType PreferredLegacyWorkTypes => preferredWorkTypes;
    public FacilityWorkType DislikedLegacyWorkTypes => dislikedWorkTypes;

    public void SetWorkPreferences(
        FacilityWorkType preferred,
        FacilityWorkType disliked)
    {
        preferredWorkTypes = preferred;
        dislikedWorkTypes = disliked;
    }

    public void Multiply(CharacterModelModifiers other)
    {
        if (other == null)
        {
            return;
        }

        consumptionMultiplier *= Math.Max(0f, other.consumptionMultiplier);
        spendingMultiplier *= Math.Max(0f, other.spendingMultiplier);
        waitPatienceMultiplier *= Math.Max(0f, other.waitPatienceMultiplier);
        crowdSensitivityMultiplier *= Math.Max(0f, other.crowdSensitivityMultiplier);
        accidentChanceMultiplier *= Math.Max(0f, other.accidentChanceMultiplier);
        workSpeedMultiplier *= Math.Max(0f, other.workSpeedMultiplier);
        researchSpeedMultiplier *= Math.Max(0f, other.researchSpeedMultiplier);
        combatPowerMultiplier *= Math.Max(0f, other.combatPowerMultiplier);
        moveSpeedMultiplier *= Math.Max(0f, other.moveSpeedMultiplier);
        stayDurationMultiplier *= Math.Max(0f, other.stayDurationMultiplier);
        preferredFacilityRoles |= other.preferredFacilityRoles;
        dislikedFacilityRoles |= other.dislikedFacilityRoles;
        preferredWorkTypes |= other.preferredWorkTypes;
        dislikedWorkTypes |= other.dislikedWorkTypes;
    }

    private static IEnumerable<WorkTypeId> EnumerateWorkTypeIds(
        FacilityWorkType workTypes)
    {
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
        {
            if (TryGetLegacyType(
                    definition.WorkTypeId,
                    out FacilityWorkType legacyType)
                && (workTypes & legacyType) != 0)
            {
                yield return definition.WorkTypeId;
            }
        }
    }

    private static bool TryGetLegacyType(
        WorkTypeId workTypeId,
        out FacilityWorkType legacyType)
    {
        if (workTypeId == BuiltInWorkTypeIds.Operate) legacyType = FacilityWorkType.Operate;
        else if (workTypeId == BuiltInWorkTypeIds.Restock) legacyType = FacilityWorkType.Restock;
        else if (workTypeId == BuiltInWorkTypeIds.Construct) legacyType = FacilityWorkType.Construct;
        else if (workTypeId == BuiltInWorkTypeIds.Repair) legacyType = FacilityWorkType.Repair;
        else if (workTypeId == BuiltInWorkTypeIds.Clean) legacyType = FacilityWorkType.Clean;
        else if (workTypeId == BuiltInWorkTypeIds.Research) legacyType = FacilityWorkType.Research;
        else if (workTypeId == BuiltInWorkTypeIds.Guard) legacyType = FacilityWorkType.Guard;
        else if (workTypeId == BuiltInWorkTypeIds.Reception) legacyType = FacilityWorkType.Reception;
        else if (workTypeId == BuiltInWorkTypeIds.Rescue) legacyType = FacilityWorkType.Rescue;
        else if (workTypeId == BuiltInWorkTypeIds.Rest) legacyType = FacilityWorkType.Rest;
        else if (workTypeId == BuiltInWorkTypeIds.Craft) legacyType = FacilityWorkType.Craft;
        else if (workTypeId == BuiltInWorkTypeIds.Haul) legacyType = FacilityWorkType.Haul;
        else if (workTypeId == BuiltInWorkTypeIds.Hunt) legacyType = FacilityWorkType.Hunt;
        else if (workTypeId == BuiltInWorkTypeIds.Butcher) legacyType = FacilityWorkType.Butcher;
        else if (workTypeId == BuiltInWorkTypeIds.DrawWater) legacyType = FacilityWorkType.DrawWater;
        else if (workTypeId == BuiltInWorkTypeIds.Cook) legacyType = FacilityWorkType.Cook;
        else if (workTypeId == BuiltInWorkTypeIds.Treat) legacyType = FacilityWorkType.Treat;
        else if (workTypeId == BuiltInWorkTypeIds.Surgery) legacyType = FacilityWorkType.Surgery;
        else if (workTypeId == BuiltInWorkTypeIds.Refuel) legacyType = FacilityWorkType.Refuel;
        else if (workTypeId == BuiltInWorkTypeIds.Warden) legacyType = FacilityWorkType.Warden;
        else if (workTypeId == BuiltInWorkTypeIds.Perform) legacyType = FacilityWorkType.Perform;
        else if (workTypeId == BuiltInWorkTypeIds.Gather) legacyType = FacilityWorkType.Gather;
        else if (workTypeId == BuiltInWorkTypeIds.Sow) legacyType = FacilityWorkType.Sow;
        else if (workTypeId == BuiltInWorkTypeIds.Harvest) legacyType = FacilityWorkType.Harvest;
        else if (workTypeId == BuiltInWorkTypeIds.Logging) legacyType = FacilityWorkType.Logging;
        else if (workTypeId == BuiltInWorkTypeIds.Quarry) legacyType = FacilityWorkType.Quarry;
        else if (workTypeId == BuiltInWorkTypeIds.AnimalCare) legacyType = FacilityWorkType.AnimalCare;
        else if (workTypeId == BuiltInWorkTypeIds.GrandProject) legacyType = FacilityWorkType.GrandProject;
        else if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation) legacyType = FacilityWorkType.ThreatMitigation;
        else if (workTypeId == BuiltInWorkTypeIds.Plumbing) legacyType = FacilityWorkType.Plumbing;
        else
        {
            legacyType = FacilityWorkType.None;
            return false;
        }

        return true;
    }
}
