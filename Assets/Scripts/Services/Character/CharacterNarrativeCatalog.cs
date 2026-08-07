using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CharacterNarrativeCatalog : ICharacterNarrativeCatalog
{
    private readonly IReadOnlyDictionary<string, CharacterBackgroundDefinitionSO> backgrounds;
    private readonly IReadOnlyDictionary<string, CharacterAmbitionDefinitionSO> ambitions;
    private readonly IReadOnlyDictionary<string, LifeEventDefinitionSO> events;
    private readonly IReadOnlyDictionary<string, SpeciesCultureDefinitionSO> cultures;

    public CharacterNarrativeCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        Backgrounds = RequireDefinitions(content.GetAll<CharacterBackgroundDefinitionSO>(), 12, "background");
        Ambitions = RequireDefinitions(content.GetAll<CharacterAmbitionDefinitionSO>(), 18, "ambition");
        LifeEvents = RequireDefinitions(content.GetAll<LifeEventDefinitionSO>(), 32, "life event");
        Cultures = RequireDefinitions(content.GetAll<SpeciesCultureDefinitionSO>(), 10, "culture");
        Practices = RequireDefinitions(content.GetAll<CulturalPracticeDefinitionSO>(), 20, "cultural practice");
        backgrounds = Backgrounds.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        ambitions = Ambitions.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        events = LifeEvents.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        cultures = Cultures.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        foreach (SpeciesCultureDefinitionSO culture in Cultures)
        {
            if (Cultures.Count(value => string.Equals(value.defaultSpeciesId, culture.defaultSpeciesId, StringComparison.Ordinal)) != 1)
                throw new InvalidOperationException($"Species '{culture.defaultSpeciesId}' must have exactly one default culture.");
        }
    }

    public IReadOnlyList<CharacterBackgroundDefinitionSO> Backgrounds { get; }
    public IReadOnlyList<CharacterAmbitionDefinitionSO> Ambitions { get; }
    public IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    public IReadOnlyList<SpeciesCultureDefinitionSO> Cultures { get; }
    public IReadOnlyList<CulturalPracticeDefinitionSO> Practices { get; }

    public CharacterBackgroundDefinitionSO Require(CharacterBackgroundId id) => Require(backgrounds, id.Value, "background");
    public CharacterAmbitionDefinitionSO Require(CharacterAmbitionId id) => Require(ambitions, id.Value, "ambition");
    public LifeEventDefinitionSO Require(NarrativeEventId id) => Require(events, id.Value, "life event");
    public SpeciesCultureDefinitionSO Require(SpeciesCultureId id) => Require(cultures, id.Value, "culture");
    public SpeciesCultureDefinitionSO RequireDefaultCulture(string speciesId) => Cultures.Single(value =>
        string.Equals(value.defaultSpeciesId, speciesId?.Trim() ?? string.Empty, StringComparison.Ordinal));

    private static IReadOnlyList<T> RequireDefinitions<T>(IEnumerable<T> source, int expected, string label)
        where T : V20AuthoredContentSO
    {
        T[] values = (source ?? Array.Empty<T>()).Where(value => value != null)
            .OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        if (values.Length != expected)
            throw new InvalidOperationException($"V20 requires exactly {expected} {label} definitions, found {values.Length}.");
        List<string> errors = values.SelectMany(value => value.ValidateDefinition().Select(error => $"{value.name}: {error}")).ToList();
        if (values.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count() != values.Length)
            errors.Add($"V20 {label} ids are not unique.");
        if (errors.Count > 0) throw new InvalidOperationException($"V20 {label} content is invalid:\n" + string.Join("\n", errors));
        return values;
    }

    private static T Require<T>(IReadOnlyDictionary<string, T> source, string id, string label)
    {
        if (source.TryGetValue(id?.Trim() ?? string.Empty, out T value)) return value;
        throw new KeyNotFoundException($"Unknown V20 {label} '{id}'.");
    }
}
