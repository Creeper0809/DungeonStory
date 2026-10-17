/// <summary>Quality domain facts acquire narrative identity and display only at this boundary.</summary>
public static class CombatEquipmentQualityFactMapping
{
    public static ProductQualityOutcomeReceipt Capture(CombatEquipmentQualityResolvedFact fact) => new(
        fact.OperationId, fact.OwnerRevision, fact.MakerCharacterId, fact.MakerDisplayName,
        fact.DefinitionId, fact.Quality, fact.AttemptIndex, fact.AbsoluteDay, fact.RejectedBelowMinimum);
}
