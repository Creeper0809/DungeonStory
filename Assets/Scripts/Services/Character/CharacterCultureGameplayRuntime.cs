using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ICharacterCultureGameplayQuery
{
    float GetFacilityUtilityBias(
        CharacterActor actor,
        BuildableObject facility,
        FacilityRole role);

    float GetServiceIncidentWeight(
        CharacterId characterId,
        ServiceIncidentKind incident,
        IReadOnlyCollection<SpeciesCultureId> culturesPresent);
}

/// <summary>
/// Projects authored culture into real room selection and incident generation.
/// Text descriptions remain presentation only; gameplay reads typed room ranges,
/// facility ids, etiquette count, and authored inter-culture attitudes.
/// </summary>
public sealed class CharacterCultureGameplayRuntime :
    ICharacterCultureGameplayQuery
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly IRoomEnvironmentQuery rooms;

    public CharacterCultureGameplayRuntime(
        ICharacterNarrativeQuery narratives,
        ICharacterNarrativeCatalog catalog,
        IRoomEnvironmentQuery rooms)
    {
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public float GetFacilityUtilityBias(
        CharacterActor actor,
        BuildableObject facility,
        FacilityRole role)
    {
        if (actor?.Identity == null
            || facility?.BuildingData == null
            || !narratives.TryGet(
                actor.Identity.TypedPersistentId,
                out CharacterNarrativeSnapshot narrative))
        {
            return 0f;
        }

        SpeciesCultureDefinitionSO culture = catalog.Require(narrative.CultureId);
        CultureRoomPreferenceDefinition preference = culture.roomPreference
            ?? new CultureRoomPreferenceDefinition();
        float bias = (preference.preferredRoles & role) != 0 ? 0.08f : 0f;
        string definitionId = !string.IsNullOrWhiteSpace(
                facility.BuildingData.ContentDefinitionId)
            ? facility.BuildingData.ContentDefinitionId
            : $"building:{facility.BuildingData.id}";
        if ((culture.preferredFacilityIds ?? new List<string>()).Contains(
                definitionId,
                StringComparer.Ordinal))
        {
            bias += 0.12f;
        }

        if (!rooms.TryGetSnapshot(facility, out RoomEnvironmentSnapshot room)
            || !room.IsEnvironmentActive)
        {
            return Mathf.Clamp(bias - 0.08f, -0.2f, 0.2f);
        }

        float temperature = 1f - Mathf.Clamp01(
            Mathf.Abs(room.TemperatureC - preference.idealTemperatureC)
            / Mathf.Max(1f, preference.temperatureToleranceC));
        float ventilation = ThresholdScore(
            room.Ventilation,
            preference.minimumVentilation);
        float cleanliness = ThresholdScore(
            room.Cleanliness,
            preference.minimumCleanliness);
        float lighting = RangeScore(
            room.Lighting,
            preference.minimumLighting,
            preference.maximumLighting);
        float environment = (temperature + ventilation + cleanliness + lighting) / 4f;
        bias += (environment - 0.5f) * 0.16f;

        if (preference.prefersSharedSpace)
            bias += room.Area >= 12 && room.FreeCells >= 3 ? 0.05f : -0.05f;
        else if (preference.prefersPrivateSpace)
            bias += room.Area <= 8 && room.DoorCount > 0 ? 0.05f : -0.05f;
        return Mathf.Clamp(bias, -0.2f, 0.2f);
    }

    public float GetServiceIncidentWeight(
        CharacterId characterId,
        ServiceIncidentKind incident,
        IReadOnlyCollection<SpeciesCultureId> culturesPresent)
    {
        if (!narratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
            return 1f;
        SpeciesCultureDefinitionSO culture = catalog.Require(narrative.CultureId);
        if (incident == ServiceIncidentKind.ForbiddenMeal)
        {
            return Mathf.Clamp(
                1f + 0.2f * (culture.forbiddenItemIds?.Count ?? 0),
                1f,
                2f);
        }
        if (incident != ServiceIncidentKind.CulturalInsult)
            return 1f;

        SpeciesCultureId[] others = (culturesPresent ?? Array.Empty<SpeciesCultureId>())
            .Where(value => value.IsValid && !value.Equals(narrative.CultureId))
            .Distinct()
            .ToArray();
        if (others.Length == 0) return 0.35f;
        Dictionary<string, float> attitudes = (culture.otherCultureAttitudes
                ?? new List<V20WeightedId>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.id))
            .GroupBy(value => value.id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().weight,
                StringComparer.Ordinal);
        float averageAttitude = others
            .Select(value => attitudes.TryGetValue(value.Value, out float weight)
                ? weight
                : 1f)
            .Average();
        float etiquetteExposure = 0.25f * Math.Max(
            1,
            culture.etiquetteRules?.Count ?? 0);
        float attitudeRisk = Mathf.Max(0f, 1f - averageAttitude) * 0.75f
            - Mathf.Max(0f, averageAttitude - 1f) * 0.35f;
        return Mathf.Clamp(1f + etiquetteExposure + attitudeRisk, 0.25f, 3f);
    }

    private static float ThresholdScore(float actual, float minimum) =>
        minimum <= 0f ? 1f : Mathf.Clamp01(actual / minimum);

    private static float RangeScore(float actual, float minimum, float maximum)
    {
        if (actual >= minimum && actual <= maximum) return 1f;
        float distance = actual < minimum ? minimum - actual : actual - maximum;
        return 1f - Mathf.Clamp01(distance / 50f);
    }
}
