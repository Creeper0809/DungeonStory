using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class DefenseServiceFactData
{
    public string facilityId = string.Empty, operationId = string.Empty;
    public long revision;
    public int absoluteDay, operationSequence;
    public DefenseFacilityPhysicalCommitKind kind;
    public DefenseFacilityServiceOutcomePhase phase;
    public DefenseFacilityOperationalState beforeState, afterState;
    public int beforeSupply, afterSupply;
    public string physicalOperationId = string.Empty, itemId = string.Empty;
    public long physicalOwnerRevision, inputMassGrams;
    public int inputQuantity;

    public DefenseServiceFactData Clone() => (DefenseServiceFactData)MemberwiseClone();
    public void Validate()
    {
        bool completed = phase == DefenseFacilityServiceOutcomePhase.Completed;
        if (!DomainFactValueSyntax.IsIdentity(facilityId) || !DomainFactValueSyntax.IsIdentity(operationId)
            || revision <= 0 || absoluteDay < 0 || operationSequence < 0
            || kind is not (DefenseFacilityPhysicalCommitKind.SupplyTransfer or DefenseFacilityPhysicalCommitKind.MaintenanceSink)
            || !Enum.IsDefined(typeof(DefenseFacilityServiceOutcomePhase), phase)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), beforeState)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), afterState)
            || beforeSupply < 0 || afterSupply < 0
            || operationId != Operation(facilityId, operationSequence, phase)
            || (completed
                ? !DomainFactValueSyntax.IsIdentity(physicalOperationId) || physicalOwnerRevision <= 0
                    || !DomainFactValueSyntax.IsIdentity(itemId) || inputQuantity <= 0 || inputMassGrams <= 0
                : physicalOperationId != string.Empty || physicalOwnerRevision != 0
                    || itemId != string.Empty || inputQuantity != 0 || inputMassGrams != 0))
            throw new ArgumentException("defense-service-fact-invalid");
    }
    public static string Operation(string facility, int sequence, DefenseFacilityServiceOutcomePhase phase) =>
        "defense-service:" + facility + ":" + sequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ":" + phase;
    public bool SameAs(DefenseServiceFactData other) => other != null
        && facilityId == other.facilityId && operationId == other.operationId && revision == other.revision
        && absoluteDay == other.absoluteDay && operationSequence == other.operationSequence && kind == other.kind && phase == other.phase
        && beforeState == other.beforeState && afterState == other.afterState && beforeSupply == other.beforeSupply && afterSupply == other.afterSupply
        && physicalOperationId == other.physicalOperationId && physicalOwnerRevision == other.physicalOwnerRevision
        && itemId == other.itemId && inputQuantity == other.inputQuantity && inputMassGrams == other.inputMassGrams;
}

/// <summary>Frozen service transition. The physical owner identity is domain data, not a ledger result key.</summary>
public sealed class DefenseServiceChangedFact
{
    private readonly DefenseServiceFactData data;
    public DefenseServiceChangedFact(DefenseServiceFactData data)
    { (data ?? throw new ArgumentNullException(nameof(data))).Validate(); this.data = data.Clone(); }
    public DefenseServiceFactData Snapshot() => data.Clone();
}
