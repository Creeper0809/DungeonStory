using System;

public sealed class ResourceStockSaleFactMapping
{
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public ResourceStockSaleFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public MigratedDomainFactCapture Capture(ResourceStockSaleFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        ResourceStockPolicyPendingSale value = fact.CaptureDomainState();
        if (!fact.Completed)
        {
            return new MigratedDomainFactCapture(
                MigratedProducerOutcomeKind.ResourceStockPolicySaleResult,
                fact.OperationId,
                fact.AbsoluteDay,
                GameplayOutcomeStatus.Succeeded,
                ResourceStockPolicySaleOutcomeDetails.Preview(
                    value.operationId,
                    value.itemId,
                    value.destinationId,
                    value.quantity,
                    value.inputMassGrams,
                    value.proceeds,
                    value.unitPrice,
                    value.saleRateBasisPoints,
                    qualityRejected: false,
                    plannedSourceStackIds: value.sourceStackIds),
                reservationOnly: true);
        }

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ResourceStockPolicySaleResult,
            fact.OperationId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CreateCompletedDetails(value));
    }

    public MigratedDomainFactCapture Capture(QualityRejectedSaleFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        QualityRejectedSalePending value = fact.CaptureDomainState();
        if (!fact.Completed)
        {
            return new MigratedDomainFactCapture(
                MigratedProducerOutcomeKind.QualityRejectedSaleResult,
                fact.OperationId,
                fact.AbsoluteDay,
                GameplayOutcomeStatus.Succeeded,
                ResourceStockPolicySaleOutcomeDetails.Preview(
                    value.operationId,
                    value.itemId,
                    value.destinationId,
                    value.quantity,
                    value.inputMassGrams,
                    value.proceeds,
                    value.unitPrice,
                    value.saleRateBasisPoints,
                    qualityRejected: true,
                    itemInstanceId: value.itemInstanceId,
                    sourceStackId: value.sourceStackId,
                    qualityTier: value.qualityTier),
                reservationOnly: true);
        }

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.QualityRejectedSaleResult,
            fact.OperationId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CreateCompletedDetails(value));
    }

    private MigratedProducerOutcomeDetails CreateCompletedDetails(
        ResourceStockPolicyPendingSale value)
    {
        GameplayOutcomeId physicalOutcomeId =
            RequirePhysicalOutcome(
                value.operationId,
                value.physicalOutcomeOwnerRevision);
        return ResourceStockPolicySaleOutcomeDetails.Create(
            value,
            physicalOutcomeId);
    }

    private MigratedProducerOutcomeDetails CreateCompletedDetails(
        QualityRejectedSalePending value)
    {
        GameplayOutcomeId physicalOutcomeId =
            RequirePhysicalOutcome(
                value.operationId,
                value.physicalOutcomeOwnerRevision);
        return ResourceStockPolicySaleOutcomeDetails.Create(
            value,
            physicalOutcomeId);
    }

    private GameplayOutcomeId RequirePhysicalOutcome(
        string operationId,
        long ownerRevision)
    {
        GameplayResultKey key =
            ResourceStockPolicySaleOutcomeDetails.PhysicalResultKey(
                operationId,
                ownerRevision);
        if (!key.IsValid
            || !diagnostics.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            || identity.ResultKey != key
            || !identity.OutcomeId.IsValid
            || !identity.HasCanonicalPayloadHash)
        {
            throw new InvalidOperationException(
                "The canonical physical sale outcome identity is unavailable.");
        }
        return identity.OutcomeId;
    }
}
