using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class WorkIdentityAppliedFactData
{
    public string deliveryId = string.Empty, producerStreamId = string.Empty, characterId = string.Empty;
    public string workId = string.Empty, productId = string.Empty, operationId = string.Empty;
    public string terminalOwnerId = string.Empty, childResultKey = string.Empty, targetEntityId = string.Empty;
    public string skillModifierProvenance = string.Empty, characterDisplayName = string.Empty;
    public int operationSequence, absoluteDay;
    public CharacterCommandOrigin origin;
    public float contributionWorkUnits;
    public bool requiresTerminalDelivery;

    public WorkIdentityAppliedFactData Clone() => (WorkIdentityAppliedFactData)MemberwiseClone();
    public WorkCompletionIdentityDeliveryRequest ToRequest() => new(deliveryId, producerStreamId, operationSequence,
        new CharacterId(characterId), workId, productId, origin, absoluteDay, operationId, terminalOwnerId,
        childResultKey, targetEntityId, contributionWorkUnits, skillModifierProvenance);
    public bool IsEmpty => deliveryId == string.Empty && producerStreamId == string.Empty && characterId == string.Empty
        && workId == string.Empty && productId == string.Empty && operationId == string.Empty && terminalOwnerId == string.Empty
        && childResultKey == string.Empty && targetEntityId == string.Empty && skillModifierProvenance == string.Empty
        && characterDisplayName == string.Empty && operationSequence == 0 && absoluteDay == 0 && origin == 0
        && contributionWorkUnits == 0 && !requiresTerminalDelivery;
}

// Domain receipt only: no narrative tiers, ledger identities, or display renderer.
public sealed class WorkIdentityAppliedFact
{
    private readonly WorkIdentityAppliedFactData data;
    public WorkIdentityAppliedFact(WorkCompletionIdentityDeliveryRequest request, string characterDisplayName, bool requiresTerminalDelivery)
        : this(new WorkIdentityAppliedFactData
        {
            deliveryId = request.DeliveryId, producerStreamId = request.ProducerStreamId, operationSequence = request.OperationSequence,
            characterId = request.Character.Value, workId = request.WorkId, productId = request.ProductId, origin = request.Origin,
            absoluteDay = request.AbsoluteDay, operationId = request.OperationId, terminalOwnerId = request.TerminalOwnerId,
            childResultKey = request.ChildResultKey, targetEntityId = request.TargetEntityId,
            contributionWorkUnits = request.ContributionWorkUnits, skillModifierProvenance = request.SkillModifierProvenance,
            characterDisplayName = characterDisplayName, requiresTerminalDelivery = requiresTerminalDelivery
        }) { }

    public WorkIdentityAppliedFact(WorkIdentityAppliedFactData value)
    {
        data = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
        if (!DomainFactValueSyntax.IsText(data.characterDisplayName, 4096, false)
            || data.characterDisplayName != data.characterDisplayName.Trim())
            throw new ArgumentException("work-identity-character-name-invalid");
        Request = data.ToRequest();
    }
    public WorkCompletionIdentityDeliveryRequest Request { get; }
    public string CharacterDisplayName => data.characterDisplayName;
    public bool RequiresTerminalDelivery => data.requiresTerminalDelivery;
    public WorkIdentityAppliedFactData Snapshot() => data.Clone();
}
