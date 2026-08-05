using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHabitatPatch
{
    private readonly List<string> preferredSpeciesTags;

    public WildlifeHabitatPatch(
        string patchId,
        WildlifeHabitatType habitatType,
        Vector2Int center,
        int radius,
        float resourceCapacity,
        float currentResource,
        float regenPerSecond,
        float danger,
        IEnumerable<string> preferredSpeciesTags = null,
        string linkedWaterSourceId = "")
    {
        WildlifeHabitatPatchId typedId = (WildlifeHabitatPatchId)patchId;
        if (!typedId.IsValid)
        {
            throw new ArgumentException(
                $"Wildlife habitat patch requires a typed persistent ID; received '{patchId}'.",
                nameof(patchId));
        }

        PatchId = typedId.Value;
        HabitatType = habitatType;
        Center = center;
        Radius = Mathf.Clamp(radius, 0, 12);
        ResourceCapacity = Mathf.Max(0.1f, resourceCapacity);
        CurrentResource = Mathf.Clamp(currentResource, 0f, ResourceCapacity);
        RegenPerSecond = Mathf.Max(0f, regenPerSecond);
        Danger = Mathf.Clamp01(danger);
        LinkedWaterSourceId = linkedWaterSourceId ?? string.Empty;
        this.preferredSpeciesTags = (preferredSpeciesTags ?? Enumerable.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public string PatchId { get; }
    public WildlifeHabitatType HabitatType { get; }
    public Vector2Int Center { get; }
    public int Radius { get; }
    public float ResourceCapacity { get; private set; }
    public float CurrentResource { get; private set; }
    public float RegenPerSecond { get; }
    public float Danger { get; }
    public string LinkedWaterSourceId { get; }
    public IReadOnlyList<string> PreferredSpeciesTags => preferredSpeciesTags;
    public float Resource01 => Mathf.Clamp01(
        CurrentResource / Mathf.Max(0.1f, ResourceCapacity));
    public bool IsDepleted => CurrentResource <= ResourceCapacity * 0.06f;

    public bool Contains(Vector2Int position) =>
        Mathf.Abs(position.x - Center.x) + Mathf.Abs(position.y - Center.y) <= Radius;

    public bool IsPreferredBy(WildlifeSpeciesDefinition species)
    {
        if (species == null || preferredSpeciesTags.Count == 0)
        {
            return true;
        }

        return preferredSpeciesTags.Any(tag =>
            string.Equals(tag, species.SpeciesId, StringComparison.Ordinal)
            || string.Equals(tag, species.DisplayName, StringComparison.Ordinal));
    }

    public void Tick(float deltaTime)
    {
        if (RegenPerSecond <= 0f || CurrentResource >= ResourceCapacity)
        {
            return;
        }

        CurrentResource = Mathf.Min(
            ResourceCapacity,
            CurrentResource + RegenPerSecond * Mathf.Max(0f, deltaTime));
    }

    public float Consume(float amount)
    {
        float consumed = Mathf.Min(Mathf.Max(0f, amount), CurrentResource);
        CurrentResource -= consumed;
        return consumed;
    }

    public void SynchronizeResource(float capacity, float current)
    {
        ResourceCapacity = Mathf.Max(0.1f, capacity);
        CurrentResource = Mathf.Clamp(current, 0f, ResourceCapacity);
    }

    public DungeonWildlifeEcosystemSaveData CaptureStandalone()
    {
        DungeonWildlifeEcosystemSaveData data = new DungeonWildlifeEcosystemSaveData();
        data.patches.Add(Capture());
        return data;
    }

    public WildlifeHabitatPatchSaveData Capture() => new WildlifeHabitatPatchSaveData
    {
        patchId = PatchId,
        linkedWaterSourceId = LinkedWaterSourceId,
        habitatType = HabitatType,
        gridX = Center.x,
        gridY = Center.y,
        radius = Radius,
        resourceCapacity = ResourceCapacity,
        currentResource = CurrentResource,
        regenPerSecond = RegenPerSecond,
        danger = Danger,
        preferredSpeciesTags = preferredSpeciesTags.ToList()
    };

    public static WildlifeHabitatPatch FromSave(WildlifeHabitatPatchSaveData saveData)
    {
        if (saveData == null)
        {
            return null;
        }

        return new WildlifeHabitatPatch(
            saveData.patchId,
            saveData.habitatType,
            new Vector2Int(saveData.gridX, saveData.gridY),
            saveData.radius,
            saveData.resourceCapacity,
            saveData.currentResource,
            saveData.regenPerSecond,
            saveData.danger,
            saveData.preferredSpeciesTags,
            saveData.linkedWaterSourceId);
    }
}
