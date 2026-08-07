using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FestivalDefinitionCatalog : IFestivalDefinitionCatalog
{
    private readonly IReadOnlyList<FestivalDefinitionSO> all;
    private readonly IReadOnlyDictionary<string, FestivalDefinitionSO> byId;

    public FestivalDefinitionCatalog(IGameContentDefinitionSource content)
    {
        all = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<FestivalDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        if (all.Count != 16
            || all.Any(value => string.IsNullOrWhiteSpace(value.StableId)
                || string.IsNullOrWhiteSpace(value.displayName)
                || value.dayOfSeason < 1
                || value.dayOfSeason > GameCalendarRules.DaysPerSeason
                || value.ValidateDefinition().Count > 0))
        {
            throw new InvalidOperationException(
                "The V20 festival catalog must contain exactly sixteen complete authored definitions.");
        }

        byId = all.ToDictionary(value => value.StableId, StringComparer.Ordinal);
    }

    public IReadOnlyList<FestivalDefinitionSO> All => all;

    public FestivalDefinitionSO Require(string festivalId)
    {
        string normalized = festivalId?.Trim() ?? string.Empty;
        return byId.TryGetValue(normalized, out FestivalDefinitionSO definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Unknown authored festival '{normalized}'.");
    }
}
