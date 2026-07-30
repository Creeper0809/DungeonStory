using System;
using System.Collections.Generic;

public interface IOffenseV17ContentCatalog
{
    IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes { get; }
    IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites { get; }
    IReadOnlyList<OffenseDecisionCardSO> DecisionCards { get; }
    IReadOnlyList<OffenseEncounterSO> Encounters { get; }
}

public sealed class DataCatalogOffenseV17ContentCatalog : IOffenseV17ContentCatalog
{
    private readonly IDataCatalog dataCatalog;

    public DataCatalogOffenseV17ContentCatalog(IDataCatalog dataCatalog)
    {
        this.dataCatalog = dataCatalog
            ?? throw new ArgumentNullException(nameof(dataCatalog));
    }

    public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
        GetOrdered<OffenseSiteArchetypeSO>((item) => item.siteTypeId);

    public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
        GetOrdered<OffenseUrgentSiteDefinitionSO>((item) => item.urgentSiteId);

    public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
        GetOrdered<OffenseDecisionCardSO>((item) => item.cardId);

    public IReadOnlyList<OffenseEncounterSO> Encounters =>
        GetOrdered<OffenseEncounterSO>((item) => item.encounterId);

    private IReadOnlyList<T> GetOrdered<T>(Func<T, string> idSelector)
        where T : DataScriptableObject
    {
        List<T> values = new List<T>();
        foreach (T value in dataCatalog.GetData<T>().Values)
        {
            if (value != null)
            {
                values.Add(value);
            }
        }

        values.Sort((left, right) => string.CompareOrdinal(
            idSelector(left),
            idSelector(right)));
        return values;
    }
}
