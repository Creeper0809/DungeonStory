using System.Collections.Generic;

public interface ISocietyEventCatalog
{
    IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IFactionStoryCatalog
{
    IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IWorldEventCatalog
{
    IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    SeasonalWorldEventDefinitionSO Require(string id);
}

public interface IEndingCatalog
{
    IReadOnlyList<EndingDefinitionSO> All { get; }
    bool TryGet(string id, out EndingDefinitionSO definition);
    EndingDefinitionSO Require(string id);
}
