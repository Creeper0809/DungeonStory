using System;
using DungeonStory.Foundation.Facts;

/// <summary>The already resolved attempt. Observers cannot reroll or change its quality.</summary>
public sealed class CombatEquipmentQualityResolvedFact
{
    public CombatEquipmentQualityResolvedFact(string operationId, int attemptIndex,
        string makerCharacterId, string makerDisplayName, string definitionId,
        CraftsmanshipQualityTier quality, int absoluteDay, bool rejectedBelowMinimum)
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId) || attemptIndex < 0
            || !DomainFactValueSyntax.IsIdentity(makerCharacterId)
            || !DomainFactValueSyntax.IsText(makerDisplayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsIdentity(definitionId)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), quality) || absoluteDay < 0)
            throw new ArgumentException("combat-quality-resolved-fact-invalid");
        OperationId = operationId; AttemptIndex = attemptIndex; MakerCharacterId = makerCharacterId;
        MakerDisplayName = makerDisplayName; DefinitionId = definitionId; Quality = quality;
        AbsoluteDay = absoluteDay; RejectedBelowMinimum = rejectedBelowMinimum;
    }

    public string OperationId { get; }
    public int AttemptIndex { get; }
    public long OwnerRevision => AttemptIndex;
    public string MakerCharacterId { get; }
    public string MakerDisplayName { get; }
    public string DefinitionId { get; }
    public CraftsmanshipQualityTier Quality { get; }
    public int AbsoluteDay { get; }
    public bool RejectedBelowMinimum { get; }

    public static CombatEquipmentQualityResolvedFact FromOrder(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null || !order.attemptOutcomeResolved || order.qualityOutcomeSchemaVersion != 1
            || !Enum.IsDefined(typeof(CombatEquipmentQuality), order.resolvedQuality)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), order.minimumQuality)
            || order.outputOperationId != CombatEquipmentCraftOutputOutbox.FormatOperationId(order.orderId, order.qualityAttemptIndex))
            throw new InvalidOperationException("combat-quality-resolved-owner-invalid");
        return new CombatEquipmentQualityResolvedFact(order.outputOperationId, order.qualityAttemptIndex,
            order.resolvedMakerCharacterId, order.resolvedMakerDisplayName, order.definitionId,
            (CraftsmanshipQualityTier)(int)order.resolvedQuality, order.resolvedAbsoluteDay,
            (int)order.resolvedQuality < (int)order.minimumQuality);
    }

    public static void ValidateOwner(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null || order.qualityOutcomeResultKey == null)
            throw new InvalidOperationException("combat-quality-observation-owner-missing");
        if (!order.attemptOutcomeResolved || order.outputPhase == CombatEquipmentCraftOutputPhase.LegacyUniqueOutput)
        {
            if (order.qualityOutcomeSchemaVersion != 0 || order.qualityOutcomeCommitted || order.qualityOutcomeResultKey.Length != 0)
                throw new InvalidOperationException("combat-quality-unresolved-observation");
            return;
        }
        _ = FromOrder(order);
        if (!DomainFactValueSyntax.IsText(order.qualityOutcomeResultKey, 2048, !order.qualityOutcomeCommitted)
            || (order.outputPublication?.outputPublished == true || order.completionEffectsPublished
                || order.materialTransferAcknowledged || order.outputMarketRouted) && !order.qualityOutcomeCommitted)
            throw new InvalidOperationException("combat-quality-observation-state-conflict");
    }
}
