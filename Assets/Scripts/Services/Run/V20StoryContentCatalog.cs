using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;


public sealed class V20StoryContentCatalog :
    ISocietyEventCatalog,
    IFactionStoryCatalog,
    IWorldEventCatalog,
    IEndingCatalog
{
    private readonly Dictionary<string, V20AuthoredContentSO> byId;
    public V20StoryContentCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        LifeEvents = Exact(content.GetAll<LifeEventDefinitionSO>(), 32, "life events");
        GuestRequests = Exact(content.GetAll<GuestRequestDefinitionSO>(), 13, "guest requests");
        ServiceIncidents = Exact(content.GetAll<ServiceIncidentDefinitionSO>(), 8, "service incidents");
        Arcs = Exact(content.GetAll<FactionArcDefinitionSO>(), 6, "faction arcs");
        Chapters = Exact(content.GetAll<FactionChapterDefinitionSO>(), 36, "faction chapters");
        Contracts = Exact(content.GetAll<FactionContractDefinitionSO>(), 24, "faction contracts");
        SeasonalEvents = Exact(content.GetAll<SeasonalWorldEventDefinitionSO>(), 28, "seasonal events");
        EndlessCrisisPolicyDefinitionSO[] endlessPolicies = (content
                .GetAll<EndlessCrisisPolicyDefinitionSO>()
                ?? Array.Empty<EndlessCrisisPolicyDefinitionSO>())
            .Where(value => value != null)
            .ToArray();
        if (endlessPolicies.Length != 1)
            throw new InvalidOperationException(
                $"Expected one endless crisis policy, found {endlessPolicies.Length}.");
        IReadOnlyList<string> endlessErrors = endlessPolicies[0]
            .ValidateDefinition();
        if (endlessErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" | ", endlessErrors));
        EndlessCrisisPolicy = endlessPolicies[0];
        HashSet<string> weatherFrontIds = (content
                .GetAll<WeatherFrontDefinitionSO>()
                ?? Array.Empty<WeatherFrontDefinitionSO>())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.stableId))
            .Select(value => value.stableId)
            .ToHashSet(StringComparer.Ordinal);
        All = Exact(content.GetAll<EndingDefinitionSO>(), 9, "milestones");
        Encounters = content.GetAll<OffenseEncounterSO>()
            .Where(value => value != null)
            .OrderBy(value => value.encounterId, StringComparer.Ordinal)
            .ToArray();
        Diseases = content.GetAll<DiseaseDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        Wildlife = content.GetAll<WildlifeSpeciesSO>()
            .Where(value => value != null)
            .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        ValidateSeasonalWildlifeArrivals(SeasonalEvents, Wildlife);
        BattlefieldModifiers = content.GetAll<BattlefieldModifierDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        V20AuthoredContentSO[] definitions = LifeEvents.Cast<V20AuthoredContentSO>()
            .Concat(GuestRequests).Concat(ServiceIncidents).Concat(Arcs).Concat(Chapters)
            .Concat(Contracts).Concat(SeasonalEvents).Concat(All).ToArray();
        byId = definitions.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        HashSet<string> chapterIds = Chapters.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contractIds = Contracts.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FactionArcDefinitionSO arc in Arcs)
        {
            if (arc.chapterIds.Any(id => !chapterIds.Contains(id))
                || arc.contractIds.Any(id => !contractIds.Contains(id)))
                throw new InvalidOperationException(
                    $"Faction arc '{arc.StableId}' has a broken chapter or contract reference.");
        }
        ValidateSeasonalContracts(Contracts, SeasonalEvents, Arcs);
        ValidateSeasonalWeatherFrontRequirements(
            SeasonalEvents,
            weatherFrontIds);
        ValidateFactionEffectTargets(definitions, Arcs);
        ValidateFactionChapterSignatures(Chapters);
    }

    public IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    public IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    public IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    public IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    public IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    public IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    public IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    public EndlessCrisisPolicyDefinitionSO EndlessCrisisPolicy { get; }
    public IReadOnlyList<EndingDefinitionSO> All { get; }
    public IReadOnlyList<OffenseEncounterSO> Encounters { get; }
    public IReadOnlyList<DiseaseDefinitionSO> Diseases { get; }
    public IReadOnlyList<WildlifeSpeciesSO> Wildlife { get; }
    public IReadOnlyList<BattlefieldModifierDefinitionSO> BattlefieldModifiers { get; }

    V20AuthoredContentSO ISocietyEventCatalog.Require(string id) => RequireAny(id);
    V20AuthoredContentSO IFactionStoryCatalog.Require(string id) => RequireAny(id);
    SeasonalWorldEventDefinitionSO IWorldEventCatalog.Require(string id) =>
        RequireAny(id) as SeasonalWorldEventDefinitionSO
        ?? throw new KeyNotFoundException($"Unknown seasonal event '{id}'.");
    public bool TryGet(string id, out EndingDefinitionSO definition)
    {
        definition = All.FirstOrDefault(value => string.Equals(value.StableId, Normalize(id), StringComparison.Ordinal));
        return definition != null;
    }
    public EndingDefinitionSO Require(string id) => TryGet(id, out EndingDefinitionSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown milestone '{id}'.");

    private V20AuthoredContentSO RequireAny(string id) => byId.TryGetValue(Normalize(id), out V20AuthoredContentSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown V20 story definition '{id}'.");
    private static IReadOnlyList<T> Exact<T>(IEnumerable<T> source, int count, string label) where T : V20AuthoredContentSO
    {
        T[] values = (source ?? Array.Empty<T>()).Where(value => value != null).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        List<string> errors = values.SelectMany(value => value.ValidateDefinition()).ToList();
        if (values.Length != count) errors.Add($"Expected {count} {label}, found {values.Length}.");
        if (values.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count() != values.Length) errors.Add($"Duplicate {label} ids.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
        return values;
    }

    private static void ValidateFactionEffectTargets(
        IEnumerable<V20AuthoredContentSO> definitions,
        IEnumerable<FactionArcDefinitionSO> arcs)
    {
        HashSet<string> factionIds = arcs.Select(value => value.factionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contextualTargets = new(StringComparer.Ordinal)
        {
            "requesting-faction",
            "affected-faction"
        };
        foreach ((string ownerId, V20ContentEffect effect) in EnumerateEffects(definitions))
        {
            if (effect == null || effect.kind is not (
                    V20ContentEffectKind.FactionRapport
                    or V20ContentEffectKind.FactionGrievance
                    or V20ContentEffectKind.FactionObligation))
                continue;
            if (!factionIds.Contains(effect.targetId)
                && !contextualTargets.Contains(effect.targetId))
                throw new InvalidOperationException(
                    $"V20 definition '{ownerId}' references unknown faction effect target '{effect.targetId}'.");
        }
    }

    private static void ValidateSeasonalContracts(
        IEnumerable<FactionContractDefinitionSO> contracts,
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        IEnumerable<FactionArcDefinitionSO> arcs)
    {
        HashSet<string> eventIds = seasonalEvents
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> factionIds = arcs
            .Select(value => value.factionId)
            .ToHashSet(StringComparer.Ordinal);
        FactionContractDefinitionSO[] seasonal = contracts
            .Where(value => !string.IsNullOrEmpty(value.seasonalEventId))
            .ToArray();
        if (seasonal.Any(value => !eventIds.Contains(value.seasonalEventId)
                || !factionIds.Contains(value.factionId)))
            throw new InvalidOperationException(
                "Seasonal faction contract references an unknown event or faction.");
        if (seasonal.GroupBy(
                    value => $"{value.seasonalEventId}|{value.factionId}",
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            throw new InvalidOperationException(
                "Seasonal faction contracts require one authored contract per event/faction pair.");
    }

    private static void ValidateSeasonalWeatherFrontRequirements(
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        HashSet<string> weatherFrontIds)
    {
        foreach (SeasonalWorldEventDefinitionSO definition in seasonalEvents)
        {
            if (definition.requiredWeatherFrontId.Length > 0
                && !weatherFrontIds.Contains(definition.requiredWeatherFrontId))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' references unknown weather front "
                    + $"'{definition.requiredWeatherFrontId}'.");
        }
    }

    private static void ValidateSeasonalWildlifeArrivals(
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        IEnumerable<WildlifeSpeciesSO> wildlife)
    {
        Dictionary<string, WildlifeSpeciesSO> speciesById =
            (wildlife ?? Array.Empty<WildlifeSpeciesSO>())
            .Where(value => value != null)
            .ToDictionary(value => value.SpeciesId, StringComparer.Ordinal);
        foreach (SeasonalWorldEventDefinitionSO definition in
                 seasonalEvents.Where(value =>
                     value?.wildlifeArrivalProfile?.IsConfigured == true))
        {
            speciesById.TryGetValue(
                definition.wildlifeArrivalProfile.speciesId,
                out WildlifeSpeciesSO speciesAsset);
            SeasonalWildlifeArrivalRules.RequireValidAuthoredProfile(
                definition,
                speciesAsset?.ToDefinition());
        }
    }

    private static void ValidateFactionChapterSignatures(
        IEnumerable<FactionChapterDefinitionSO> chapters)
    {
        IGrouping<string, FactionChapterDefinitionSO> duplicate = chapters
            .GroupBy(FactionChapterMechanicalSignature, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException(
                "Faction chapters share a mechanical choice signature: "
                + string.Join(", ", duplicate.Select(value => value.StableId)));
    }

    public static string FactionChapterMechanicalSignature(
        FactionChapterDefinitionSO chapter) => string.Join(
        "||",
        (chapter?.choices ?? new List<V20ChoiceDefinition>())
        .OrderBy(value => value.choiceId, StringComparer.Ordinal)
        .Select(value =>
        {
            string items = string.Join(",", (value.requirements?.items ?? new())
                .Where(item => item != null)
                .OrderBy(item => item.itemDefinitionId, StringComparer.Ordinal)
                .Select(item => $"{item.itemDefinitionId}:{item.amount}:{item.consume}"));
            string facilities = string.Join(",", (value.requirements?.facilities ?? new())
                .Where(facility => facility != null)
                .OrderBy(facility => facility.buildingDefinitionId, StringComparer.Ordinal)
                .ThenBy(facility => facility.capabilityId, StringComparer.Ordinal)
                .Select(facility => $"{facility.buildingDefinitionId}:{facility.capabilityId}:{facility.minimumCount}:{facility.mustBeOperational}"));
            string effects = string.Join(",", (value.effects ?? new())
                .Where(effect => effect != null)
                .OrderBy(effect => effect.kind)
                .ThenBy(effect => effect.targetId, StringComparer.Ordinal)
                .Select(effect => $"{effect.kind}:{effect.targetId}:{effect.amount}:{effect.durationDays}"));
            return $"{value.choiceId}[{items}][{facilities}][{effects}]";
        }));

    private static IEnumerable<(string OwnerId, V20ContentEffect Effect)> EnumerateEffects(
        IEnumerable<V20AuthoredContentSO> definitions)
    {
        foreach (V20AuthoredContentSO definition in definitions)
        {
            IEnumerable<V20ContentEffect> effects = definition switch
            {
                LifeEventDefinitionSO value => (value.automaticEffects ?? new())
                    .Concat((value.choices ?? new()).SelectMany(choice => choice.effects ?? new())),
                GuestRequestDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                ServiceIncidentDefinitionSO value => (value.responses ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionChapterDefinitionSO value => (value.choices ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionContractDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                SeasonalWorldEventDefinitionSO value => (value.startEffects ?? new())
                    .Concat(value.dailyEffects ?? new())
                    .Concat(value.endEffects ?? new()),
                EndingDefinitionSO value => (value.permanentRewards ?? new())
                    .Concat(value.counterPressures ?? new()),
                _ => Array.Empty<V20ContentEffect>()
            };
            foreach (V20ContentEffect effect in effects)
                yield return (definition.StableId, effect);
        }
    }
    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}
