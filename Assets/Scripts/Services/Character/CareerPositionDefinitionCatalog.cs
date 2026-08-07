using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CareerPositionDefinitionCatalog :
    ICareerPositionDefinitionCatalog
{
    private readonly IReadOnlyList<CareerPositionDefinitionSO> all;
    private readonly IReadOnlyDictionary<CareerPositionKind, CareerPositionDefinitionSO> byKind;

    public CareerPositionDefinitionCatalog(IGameContentDefinitionSource content)
    {
        all = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<CareerPositionDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.position)
            .ToArray();
        if (all.Count != 6
            || all.Any(value => value.position == CareerPositionKind.None
                || string.IsNullOrWhiteSpace(value.StableId)
                || string.IsNullOrWhiteSpace(value.displayName)
                || value.maximumOccupants != 1
                || value.scope == CareerPositionScopeKind.Facility
                    && string.IsNullOrWhiteSpace(value.requiredFacilityTag)))
        {
            throw new InvalidOperationException(
                "The V19 career-position catalog must contain six complete unique-position definitions.");
        }
        byKind = all.ToDictionary(value => value.position);
    }

    public IReadOnlyList<CareerPositionDefinitionSO> All => all;

    public CareerPositionDefinitionSO Require(CareerPositionKind position) =>
        byKind.TryGetValue(position, out CareerPositionDefinitionSO definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Unknown authored career position '{position}'.");
}
