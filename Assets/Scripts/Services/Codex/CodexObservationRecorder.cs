using System;
using System.Linq;

public static class CodexObservationRecorder
{
    public static void ObserveCharacter(CodexState state, CharacterActor actor)
    {
        if (state == null || actor == null)
        {
            return;
        }

        CharacterIdentity identity = actor.Identity;
        ObserveSpecies(
            state,
            identity != null && identity.Data != null ? identity.Data.species : null,
            CodexInfoSource.Observation);

        if (!string.IsNullOrWhiteSpace(identity != null ? identity.SpeciesTag : null))
        {
            CodexEntryRecord entry = state.GetOrCreate(
                CodexEntryCategory.Monster,
                GetMonsterEntryId(identity.SpeciesTag),
                identity.SpeciesTag);
            entry.AddInfo($"관찰: {actor.name} 방문", CodexInfoSource.Observation);
        }
    }

    public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)
    {
        if (state == null || species == null)
        {
            return;
        }

        string entryId = GetMonsterEntryId(species.speciesTag);
        string title = !string.IsNullOrWhiteSpace(species.displayName)
            ? species.displayName
            : species.speciesTag;
        CodexEntryRecord entry = state.GetOrCreate(CodexEntryCategory.Monster, entryId, title);

        AddIfNotBlank(entry, species.shortDescription, source);
        foreach (string preferred in species.preferredFacilityLabels ?? Array.Empty<string>())
        {
            AddIfNotBlank(entry, $"선호: {preferred}", source);
        }

        foreach (string disliked in species.dislikedEnvironmentLabels ?? Array.Empty<string>())
        {
            AddIfNotBlank(entry, $"기피: {disliked}", source);
        }

        AddIfNotBlank(entry, $"사고 위험: {species.incidentName}", source);
        AddIfNotBlank(entry, species.incidentDescription, source);
        if (species.incidentMitigatingRoles != FacilityRole.None)
        {
            entry.AddInfo($"완화 역할: {CodexTextFormatter.FormatFacilityRoles(species.incidentMitigatingRoles)}", source);
        }
    }

    public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)
    {
        if (facility == null)
        {
            return;
        }

        ObserveFacility(state, facility.BuildingData, source);
    }

    public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)
    {
        if (state == null || building == null)
        {
            return;
        }

        CodexEntryRecord entry = state.GetOrCreate(
            CodexEntryCategory.Facility,
            CodexFacilityInfoWriter.GetFacilityEntryId(building),
            FacilityShopService.GetBuildingName(building));

        FacilityData facility = building.Facility;
        if (facility != null && facility.roles != FacilityRole.None)
        {
            entry.AddInfo($"역할: {CodexTextFormatter.FormatFacilityRoles(facility.roles)}", source);
        }

        if (facility != null && facility.HasSupportedWorkTypes)
        {
            entry.AddInfo($"작업: {CodexTextFormatter.FormatWorkTypes(facility.SupportedWorkTypeIds)}", source);
        }

        if (facility != null && facility.capacity > 0)
        {
            entry.AddInfo($"수용: {facility.capacity}", source);
        }

        if (building.RequiresStockForUse())
        {
            entry.AddInfo($"재고 필요: 내부 재고 {building.GetInternalStockCapacity()}", source);
        }

        string[] preferredSpeciesTags = building.GetPreferredSpeciesTags().ToArray();
        if (preferredSpeciesTags.Length > 0)
        {
            entry.AddInfo($"시너지 대상: {string.Join(", ", preferredSpeciesTags)}", source);
        }

        DefenseFacilityData defense = building.Defense;
        if (defense != null && defense.IsDefenseFacility)
        {
            entry.AddInfo($"별 등급: {defense.star}성", source);
            entry.AddInfo($"공격 컨셉: {CodexTextFormatter.FormatDefenseConcept(defense.concept)}", source);
            entry.AddInfo($"발동 조건: {CodexTextFormatter.FormatTriggerTimings(defense.triggerTimings)}", source);
            entry.AddInfo($"대상: {CodexTextFormatter.FormatTargetRule(defense.targetRule)}", source);
            if (defense.SupportsTrigger(DefenseTriggerTiming.GuardResponse)
                || defense.concept == DefenseAttackConcept.Guard)
            {
                entry.AddInfo("시너지 대상: 경비 직원", source);
            }

            foreach (string effect in CodexTextFormatter.FormatDefenseEffects(defense))
            {
                entry.AddInfo($"효과: {effect}", source);
            }
        }
    }

    private static void AddIfNotBlank(CodexEntryRecord entry, string text, CodexInfoSource source)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            entry.AddInfo(text, source);
        }
    }

    private static string GetMonsterEntryId(string speciesTag)
    {
        return $"monster:{(string.IsNullOrWhiteSpace(speciesTag) ? "unknown" : speciesTag)}";
    }
}
