using System;
using System.Collections.Generic;

public interface IOffenseContentCatalog
{
    IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes { get; }
    IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites { get; }
    IReadOnlyList<OffenseDecisionCardSO> DecisionCards { get; }
    IReadOnlyList<OffenseEncounterSO> Encounters { get; }
}

public sealed class DataCatalogOffenseContentCatalog : IOffenseContentCatalog
{
    private readonly IOffenseAuthoredContentPort authoredContent;

    public DataCatalogOffenseContentCatalog(
        IOffenseAuthoredContentPort authoredContent)
    {
        this.authoredContent = authoredContent
            ?? throw new ArgumentNullException(nameof(authoredContent));
    }

    public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
        GetOrdered(authoredContent.SiteArchetypes, item => item.siteTypeId);

    public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
        GetOrdered(authoredContent.UrgentSites, item => item.urgentSiteId);

    public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
        GetOrdered(authoredContent.DecisionCards, item => item.cardId);

    public IReadOnlyList<OffenseEncounterSO> Encounters =>
        GetOrdered(authoredContent.Encounters, item => item.encounterId);

    private static IReadOnlyList<T> GetOrdered<T>(
        IReadOnlyList<T> source,
        Func<T, string> idSelector)
        where T : DataScriptableObject
    {
        if (source == null || source.Count == 0)
        {
            throw new InvalidOperationException(
                $"GameContentCatalogSO has no registered {typeof(T).Name} definitions.");
        }

        List<T> values = new List<T>();
        Dictionary<int, T> valuesByNumericId = new Dictionary<int, T>();
        foreach (T value in source)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    "GameContentCatalogSO contains a null data definition.");
            }

            if (valuesByNumericId.TryGetValue(value.id, out T duplicate))
            {
                throw new InvalidOperationException(
                    $"Duplicate {typeof(T).Name} numeric compatibility ID "
                    + $"{value.id}: '{duplicate.name}' and '{value.name}'.");
            }

            valuesByNumericId.Add(value.id, value);
            values.Add(value);
        }

        values.Sort((left, right) => string.CompareOrdinal(
            idSelector(left),
            idSelector(right)));
        return values;
    }
}
