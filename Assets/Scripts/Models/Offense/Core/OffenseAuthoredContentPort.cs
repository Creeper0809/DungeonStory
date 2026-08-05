using System.Collections.Generic;

public interface IOffenseAuthoredContentPort
{
    IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes { get; }
    IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites { get; }
    IReadOnlyList<OffenseDecisionCardSO> DecisionCards { get; }
    IReadOnlyList<OffenseEncounterSO> Encounters { get; }
}
