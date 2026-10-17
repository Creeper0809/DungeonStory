using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

public sealed class SeasonalWildlifeArrivalFact
{
    private readonly SeasonalWildlifeArrivalFactData data;
    public SeasonalWildlifeArrivalFact(SeasonalWildlifeArrivalFactData value)
    {
        data = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
        SeasonalTerminalFactSyntax.Require(data.occurrenceId, data.speciesId, data.speciesDisplayName, data.absoluteDay, data.wildlifeIds);
        if (!SeasonalWildlifeArrivalRules.TryParseHabitat(data.habitatId, out _))
            throw new ArgumentException("seasonal-arrival-fact-habitat-invalid");
    }
    public SeasonalWildlifeArrivalFactData Snapshot() => data.Clone();
}

public sealed class SeasonalDriftCargoFact
{
    private readonly SeasonalDriftCargoFactData data;
    public SeasonalDriftCargoFact(SeasonalDriftCargoFactData value)
    {
        data = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
        SeasonalTerminalFactSyntax.Require(data.occurrenceId, data.itemId, data.itemDisplayName, data.absoluteDay, data.sourceStackIds);
        if (data.exactQuantity <= 0 || data.securedQuantity < 0 || data.expiredQuantity < 0
            || (long)data.securedQuantity + data.expiredQuantity > data.exactQuantity || data.unitMassGrams <= 0
            || data.expiredMassGrams < 0 || !DomainFactValueSyntax.IsText(data.dispositionCommitId, 4096, true))
            throw new ArgumentException("seasonal-cargo-fact-quantity-invalid");
    }
    public SeasonalDriftCargoFactData Snapshot() => data.Clone();
}

internal static class SeasonalTerminalFactSyntax
{
    internal static void Require(string occurrence, string definition, string name, int day, string[] entities)
    {
        if (!DomainFactValueSyntax.IsIdentity(occurrence) || !DomainFactValueSyntax.IsIdentity(definition)
            || !DomainFactValueSyntax.IsText(name, 4096, false) || name != name.Trim() || day < 1
            || entities == null || entities.Length == 0 || entities.Any(id => !DomainFactValueSyntax.IsIdentity(id))
            || entities.Distinct(StringComparer.Ordinal).Count() != entities.Length
            || !entities.SequenceEqual(entities.OrderBy(id => id, StringComparer.Ordinal)))
            throw new ArgumentException("seasonal-terminal-fact-invalid");
    }
}
