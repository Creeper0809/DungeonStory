using System;

internal static class TradeInventoryOutcomeSchema
{
    private static readonly GameplayRoleId[] KnownRoles =
    {
        TradeInventoryOutcomeIds.ActorRole,
        TradeInventoryOutcomeIds.CustomerRole,
        TradeInventoryOutcomeIds.FacilityRole,
        TradeInventoryOutcomeIds.ItemRole,
        TradeInventoryOutcomeIds.SourceRole,
        TradeInventoryOutcomeIds.DestinationRole,
        TradeInventoryOutcomeIds.ContractRole,
        TradeInventoryOutcomeIds.ProjectRole,
        TradeInventoryOutcomeIds.WarehouseRole,
        TradeInventoryOutcomeIds.ResourceRole,
        TradeInventoryOutcomeIds.UpgradeRole,
        TradeInventoryOutcomeIds.BeneficiaryRole,
        TradeInventoryOutcomeIds.PolicyRole
    };

    private static readonly (GameplayMetricId Id, GameplayMetricUnitId Unit)[]
        KnownMetrics =
    {
        (TradeInventoryOutcomeIds.KindMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.CountUnit),
        (TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.CountUnit),
        (TradeInventoryOutcomeIds.OutputQuantityMetric, TradeInventoryOutcomeIds.CountUnit),
        (TradeInventoryOutcomeIds.MassMetric, TradeInventoryOutcomeIds.GramUnit),
        (TradeInventoryOutcomeIds.InputMassMetric, TradeInventoryOutcomeIds.GramUnit),
        (TradeInventoryOutcomeIds.OutputMassMetric, TradeInventoryOutcomeIds.GramUnit),
        (TradeInventoryOutcomeIds.LossMassMetric, TradeInventoryOutcomeIds.GramUnit),
        (TradeInventoryOutcomeIds.TareDestroyedMassMetric, TradeInventoryOutcomeIds.GramUnit),
        (TradeInventoryOutcomeIds.GoldMetric, TradeInventoryOutcomeIds.GoldUnit),
        (TradeInventoryOutcomeIds.CostMetric, TradeInventoryOutcomeIds.GoldUnit),
        (TradeInventoryOutcomeIds.RevenueMetric, TradeInventoryOutcomeIds.GoldUnit),
        (TradeInventoryOutcomeIds.LossValueMetric, TradeInventoryOutcomeIds.GoldUnit),
        (TradeInventoryOutcomeIds.BeforeValueMetric, TradeInventoryOutcomeIds.CountUnit),
        (TradeInventoryOutcomeIds.AfterValueMetric, TradeInventoryOutcomeIds.CountUnit),
        (TradeInventoryOutcomeIds.CategoryMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.DispositionMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.ResultRevisionMetric, TradeInventoryOutcomeIds.RevisionUnit),
        (TradeInventoryOutcomeIds.SourceXMetric, TradeInventoryOutcomeIds.CellUnit),
        (TradeInventoryOutcomeIds.SourceYMetric, TradeInventoryOutcomeIds.CellUnit),
        (TradeInventoryOutcomeIds.DestinationXMetric, TradeInventoryOutcomeIds.CellUnit),
        (TradeInventoryOutcomeIds.DestinationYMetric, TradeInventoryOutcomeIds.CellUnit),
        (TradeInventoryOutcomeIds.BeforeDispositionMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.AfterDispositionMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.BeforeEnabledMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.AfterEnabledMetric, TradeInventoryOutcomeIds.EnumUnit),
        (TradeInventoryOutcomeIds.BeforePercentMetric, TradeInventoryOutcomeIds.PercentUnit),
        (TradeInventoryOutcomeIds.AfterPercentMetric, TradeInventoryOutcomeIds.PercentUnit)
    };

    internal static bool IsKnownRole(GameplayRoleId role)
    {
        for (int index = 0; index < KnownRoles.Length; index++)
            if (KnownRoles[index].Equals(role)) return true;
        return false;
    }

    internal static bool IsKnownMetric(
        GameplayMetricId metric,
        GameplayMetricUnitId unit)
    {
        for (int index = 0; index < KnownMetrics.Length; index++)
            if (KnownMetrics[index].Id.Equals(metric)
                && KnownMetrics[index].Unit.Equals(unit)) return true;
        return false;
    }

    internal static GameplayOutcomeTagId DomainTag(TradeInventoryOutcomeKind kind) =>
        TradeInventoryOutcomeDefinitions.Get(kind).DomainTag;

    internal static bool ValidateReceipt(
        in TradeInventoryOutcomeReceipt receipt,
        out string detail)
    {
        detail = string.Empty;
        if ((int)receipt.Kind < 1
            || (int)receipt.Kind > TradeInventoryOutcomeDefinitions.All.Count
            || !receipt.OperationId.IsValid
            || receipt.ResultSequence < 0L
            || receipt.OwnerRevision < 0L
            || receipt.AbsoluteDay < 0
            || (int)receipt.Status < (int)GameplayOutcomeStatus.Succeeded
            || (int)receipt.Status > (int)GameplayOutcomeStatus.Cancelled)
        {
            detail = "trade-inventory-scalar-invalid";
            return false;
        }

        if (receipt.ParticipantCount == 0)
        {
            detail = "trade-inventory-participant-invalid";
            return false;
        }
        for (int index = 0; index < receipt.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant value = receipt.GetParticipant(index);
            if (!value.EntityId.IsValid
                || !IsKnownRole(value.RoleId)
                || (int)value.ParticipationKind
                    < (int)GameplayParticipationKind.Direct
                || (int)value.ParticipationKind
                    > (int)GameplayParticipationKind.GroupObservation
                || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                    value.DisplayName))
            {
                detail = "trade-inventory-participant-invalid";
                return false;
            }
            for (int prior = 0; prior < index; prior++)
            {
                GameplayOutcomeParticipant existing =
                    receipt.GetParticipant(prior);
                if (existing.EntityId.Equals(value.EntityId)
                    && existing.RoleId.Equals(value.RoleId))
                {
                    detail = "trade-inventory-participant-duplicate";
                    return false;
                }
            }
        }

        if (receipt.MetricCount == 0)
        {
            detail = "trade-inventory-metric-invalid";
            return false;
        }
        for (int index = 0; index < receipt.MetricCount; index++)
        {
            GameplayOutcomeMetric value = receipt.GetMetric(index);
            if (!IsMetricValueValid(value)
                || !value.DefinitionOrInstanceId.IsValid
                || !IsKnownMetric(value.MetricId, value.UnitId))
            {
                detail = "trade-inventory-metric-invalid";
                return false;
            }
        }

        if (receipt.SubjectCount == 0)
        {
            detail = "trade-inventory-subject-invalid";
            return false;
        }
        for (int index = 0; index < receipt.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink value = receipt.GetSubject(index);
            if (!value.SubjectId.IsValid
                || !HasParticipant(receipt, value.SubjectId)
                || !float.IsFinite(value.Salience)
                || value.Salience < 0f
                || value.Salience > 1f
                || (int)value.Tier < (int)NarrativeMemoryTier.Recent
                || (int)value.Tier > (int)NarrativeMemoryTier.Forgotten
                || value.AnchorRevision < 0)
            {
                detail = "trade-inventory-subject-invalid";
                return false;
            }
            for (int prior = 0; prior < index; prior++)
                if (receipt.GetSubject(prior).SubjectId.Equals(value.SubjectId))
                {
                    detail = "trade-inventory-subject-duplicate";
                    return false;
                }
        }
        for (int index = 0; index < receipt.FactCount; index++)
        {
            GameplayOutcomeFact value = receipt.GetFact(index);
            if (!value.FactId.IsValid
                || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                    value.Value,
                    GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                    allowEmpty: false))
            {
                detail = "trade-inventory-fact-invalid";
                return false;
            }
            for (int prior = 0; prior < index; prior++)
                if (receipt.GetFact(prior).FactId.Equals(value.FactId))
                {
                    detail = "trade-inventory-fact-duplicate";
                    return false;
                }
        }

        TradeInventoryOutcomeDefinition contract =
            TradeInventoryOutcomeDefinitions.Get(receipt.Kind);
        if (!HasReceiptContract(
                receipt,
                contract,
                TradeInventoryOutcomeIds.Slug(receipt.Kind)))
        {
            detail = "trade-inventory-semantic-contract-invalid";
            return false;
        }
        return true;
    }

    internal static OutcomeValidationResult ValidateRead(
        in GameplayOutcomeReadView outcome)
    {
        if (outcome.Status is not (GameplayOutcomeStatus.Succeeded
                or GameplayOutcomeStatus.PartiallySucceeded)
            || outcome.AnchorCount != 0
            || outcome.ParticipantCount == 0
            || outcome.MetricCount == 0
            || outcome.SubjectCount == 0
            || outcome.ProvenanceCount < 2)
            return OutcomeValidationResult.Reject("trade-inventory-shape-invalid");

        GameplayOutcomeMetric? kindMetric = null;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!IsKnownMetric(metric.MetricId, metric.UnitId)
                || !IsMetricValueValid(metric))
                return OutcomeValidationResult.Reject("trade-inventory-metric-contract-invalid");
            if (metric.MetricId.Equals(TradeInventoryOutcomeIds.KindMetric))
            {
                if (kindMetric.HasValue)
                    return OutcomeValidationResult.Reject("trade-inventory-kind-duplicate");
                kindMetric = metric;
            }
        }
        if (!kindMetric.HasValue
            || kindMetric.Value.Value != Math.Truncate(kindMetric.Value.Value)
            || kindMetric.Value.Value < 1d
            || kindMetric.Value.Value > TradeInventoryOutcomeDefinitions.All.Count)
            return OutcomeValidationResult.Reject("trade-inventory-kind-invalid");
        TradeInventoryOutcomeKind kind = (TradeInventoryOutcomeKind)(int)kindMetric.Value.Value;
        string slug = TradeInventoryOutcomeIds.Slug(kind);
        if (!kindMetric.Value.DefinitionOrInstanceId.Kind.Equals(
                TradeInventoryOutcomeIds.OutcomeKind)
            || !string.Equals(
                kindMetric.Value.DefinitionOrInstanceId.Value,
                slug,
                StringComparison.Ordinal))
            return OutcomeValidationResult.Reject("trade-inventory-kind-ref-invalid");

        TradeInventoryOutcomeDefinition contract =
            TradeInventoryOutcomeDefinitions.Get(kind);
        for (int definitionIndex = 0;
             definitionIndex < contract.RequiredRoles.Count;
             definitionIndex++)
        {
            GameplayRoleId role = contract.RequiredRoles[definitionIndex];
            bool found = false;
            for (int index = 0; index < outcome.ParticipantCount; index++)
            {
                GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
                if (!IsKnownRole(participant.RoleId)
                    || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName))
                    return OutcomeValidationResult.Reject("trade-inventory-participant-contract-invalid");
                found |= participant.RoleId.Equals(role);
            }
            if (!found)
                return OutcomeValidationResult.Reject("trade-inventory-required-role-missing");
        }
        for (int definitionIndex = 0;
             definitionIndex < contract.RequiredMetrics.Count;
             definitionIndex++)
        {
            GameplayMetricId required =
                contract.RequiredMetrics[definitionIndex];
            bool found = false;
            for (int index = 0; index < outcome.MetricCount; index++)
                found |= outcome.GetMetric(index).MetricId.Equals(required);
            if (!found)
                return OutcomeValidationResult.Reject("trade-inventory-required-metric-missing");
        }
        for (int definitionIndex = 0;
             definitionIndex < contract.RequiredFacts.Count;
             definitionIndex++)
        {
            bool found = false;
            for (int index = 0; index < outcome.FactCount; index++)
                found |= outcome.GetFact(index).FactId.Equals(
                    contract.RequiredFacts[definitionIndex]);
            if (!found)
                return OutcomeValidationResult.Reject(
                    "trade-inventory-required-fact-missing");
        }

        if (!HasTag(outcome, TradeInventoryOutcomeIds.TradeInventoryTag)
            || !HasTag(outcome, contract.DomainTag)
            || !HasProvenance(outcome, TradeInventoryOutcomeIds.RuntimeReceiptProvenance)
            || !HasProvenance(outcome, new GameplayOutcomeProvenanceReference(
                "source-receipt",
                slug)))
            return OutcomeValidationResult.Reject("trade-inventory-audit-contract-invalid");

        bool revisionFound = false;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(TradeInventoryOutcomeIds.ResultRevisionMetric))
                continue;
            revisionFound = metric.Value == outcome.OwnerRevision
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.OperationKind)
                && string.Equals(
                    metric.DefinitionOrInstanceId.Value,
                    outcome.OperationId.Value,
                    StringComparison.Ordinal);
        }
        return revisionFound
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("trade-inventory-owner-revision-contract-invalid");
    }

    private static bool HasReceiptContract(
        in TradeInventoryOutcomeReceipt receipt,
        TradeInventoryOutcomeDefinition contract,
        string slug)
    {
        int kindCount = 0;
        int revisionCount = 0;
        for (int index = 0; index < receipt.MetricCount; index++)
        {
            GameplayOutcomeMetric value = receipt.GetMetric(index);
            if (value.MetricId.Equals(TradeInventoryOutcomeIds.KindMetric)
                && value.UnitId.Equals(TradeInventoryOutcomeIds.EnumUnit)
                && value.Value == (int)receipt.Kind
                && value.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.OutcomeKind)
                && string.Equals(
                    value.DefinitionOrInstanceId.Value,
                    slug,
                    StringComparison.Ordinal))
                kindCount++;
            if (value.MetricId.Equals(
                    TradeInventoryOutcomeIds.ResultRevisionMetric)
                && value.UnitId.Equals(
                    TradeInventoryOutcomeIds.RevisionUnit)
                && value.Value == receipt.OwnerRevision
                && value.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.OperationKind)
                && string.Equals(
                    value.DefinitionOrInstanceId.Value,
                    receipt.OperationId.Value,
                    StringComparison.Ordinal))
                revisionCount++;
        }
        if (kindCount != 1 || revisionCount != 1
            || !HasTag(receipt, TradeInventoryOutcomeIds.TradeInventoryTag)
            || !HasTag(receipt, contract.DomainTag)
            || !HasProvenance(
                receipt,
                TradeInventoryOutcomeIds.RuntimeReceiptProvenance)
            || !HasProvenance(
                receipt,
                new GameplayOutcomeProvenanceReference("source-receipt", slug))
            || HasDuplicateTagsOrProvenance(receipt))
            return false;
        for (int index = 0; index < contract.RequiredRoles.Count; index++)
            if (!HasRole(receipt, contract.RequiredRoles[index])) return false;
        for (int index = 0; index < contract.RequiredMetrics.Count; index++)
            if (!HasMetric(receipt, contract.RequiredMetrics[index])) return false;
        for (int index = 0; index < contract.RequiredFacts.Count; index++)
            if (!HasFact(receipt, contract.RequiredFacts[index])) return false;
        return true;
    }

    private static bool HasParticipant(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayEntityId entityId)
    {
        for (int index = 0; index < receipt.ParticipantCount; index++)
            if (receipt.GetParticipant(index).EntityId.Equals(entityId))
                return true;
        return false;
    }

    private static bool IsMetricValueValid(in GameplayOutcomeMetric metric)
    {
        if (!double.IsFinite(metric.Value))
            return false;
        if (metric.MetricId.Equals(TradeInventoryOutcomeIds.SourceXMetric)
            || metric.MetricId.Equals(TradeInventoryOutcomeIds.SourceYMetric)
            || metric.MetricId.Equals(TradeInventoryOutcomeIds.DestinationXMetric)
            || metric.MetricId.Equals(TradeInventoryOutcomeIds.DestinationYMetric))
        {
            return metric.UnitId.Equals(TradeInventoryOutcomeIds.CellUnit)
                && metric.Value == Math.Truncate(metric.Value)
                && metric.Value >= int.MinValue
                && metric.Value <= int.MaxValue
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.ItemStackKind);
        }
        if (metric.Value < 0d)
            return false;
        if (metric.MetricId.Equals(TradeInventoryOutcomeIds.KindMetric))
        {
            return metric.Value == Math.Truncate(metric.Value)
                && metric.Value >= 1d
                && metric.Value <= TradeInventoryOutcomeDefinitions.All.Count
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.OutcomeKind);
        }
        if (metric.MetricId.Equals(TradeInventoryOutcomeIds.DispositionMetric))
        {
            return metric.Value == Math.Truncate(metric.Value)
                && (int)metric.Value
                    >= (int)PhysicalItemDispositionKind.Transfer
                && (int)metric.Value
                    <= (int)PhysicalItemDispositionKind.Sink;
        }
        if (metric.MetricId.Equals(
                TradeInventoryOutcomeIds.BeforeDispositionMetric)
            || metric.MetricId.Equals(
                TradeInventoryOutcomeIds.AfterDispositionMetric))
        {
            return metric.UnitId.Equals(TradeInventoryOutcomeIds.EnumUnit)
                && metric.Value == Math.Truncate(metric.Value)
                && metric.Value >= (int)WasteDispositionKind.Store
                && metric.Value <= (int)WasteDispositionKind.Incinerate
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.WastePolicyInstanceKind);
        }
        if (metric.MetricId.Equals(TradeInventoryOutcomeIds.BeforeEnabledMetric)
            || metric.MetricId.Equals(TradeInventoryOutcomeIds.AfterEnabledMetric))
        {
            return metric.UnitId.Equals(TradeInventoryOutcomeIds.EnumUnit)
                && (metric.Value == 0d || metric.Value == 1d)
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.WastePolicyInstanceKind);
        }
        if (metric.MetricId.Equals(TradeInventoryOutcomeIds.BeforePercentMetric)
            || metric.MetricId.Equals(TradeInventoryOutcomeIds.AfterPercentMetric))
        {
            return metric.UnitId.Equals(TradeInventoryOutcomeIds.PercentUnit)
                && metric.Value >= 0d
                && metric.Value <= 100d
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    TradeInventoryOutcomeIds.WastePolicyInstanceKind);
        }
        if (metric.UnitId.Equals(TradeInventoryOutcomeIds.EnumUnit)
            || metric.UnitId.Equals(TradeInventoryOutcomeIds.CountUnit)
            || metric.UnitId.Equals(TradeInventoryOutcomeIds.GramUnit)
            || metric.UnitId.Equals(TradeInventoryOutcomeIds.RevisionUnit))
        {
            return metric.Value == Math.Truncate(metric.Value);
        }
        return true;
    }

    private static bool HasRole(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayRoleId role)
    {
        for (int index = 0; index < receipt.ParticipantCount; index++)
            if (receipt.GetParticipant(index).RoleId.Equals(role)) return true;
        return false;
    }

    private static bool HasMetric(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayMetricId metric)
    {
        for (int index = 0; index < receipt.MetricCount; index++)
            if (receipt.GetMetric(index).MetricId.Equals(metric)) return true;
        return false;
    }

    private static bool HasFact(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayOutcomeFactId fact)
    {
        for (int index = 0; index < receipt.FactCount; index++)
            if (receipt.GetFact(index).FactId.Equals(fact)) return true;
        return false;
    }

    private static bool HasTag(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayOutcomeTagId tag)
    {
        for (int index = 0; index < receipt.TagCount; index++)
            if (receipt.GetTag(index).Equals(tag)) return true;
        return false;
    }

    private static bool HasProvenance(
        in TradeInventoryOutcomeReceipt receipt,
        GameplayOutcomeProvenanceReference expected)
    {
        for (int index = 0; index < receipt.ProvenanceCount; index++)
            if (receipt.GetProvenance(index).Equals(expected)) return true;
        return false;
    }

    private static bool HasDuplicateTagsOrProvenance(
        in TradeInventoryOutcomeReceipt receipt)
    {
        for (int index = 0; index < receipt.TagCount; index++)
            for (int prior = 0; prior < index; prior++)
                if (receipt.GetTag(index).Equals(receipt.GetTag(prior)))
                    return true;
        for (int index = 0; index < receipt.ProvenanceCount; index++)
            for (int prior = 0; prior < index; prior++)
                if (receipt.GetProvenance(index).Equals(
                    receipt.GetProvenance(prior)))
                    return true;
        return false;
    }

    private static bool HasTag(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeTagId tag)
    {
        for (int index = 0; index < outcome.TagCount; index++)
            if (outcome.GetTag(index).Equals(tag)) return true;
        return false;
    }

    private static bool HasProvenance(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeProvenanceReference expected)
    {
        for (int index = 0; index < outcome.ProvenanceCount; index++)
            if (outcome.GetProvenance(index).Equals(expected)) return true;
        return false;
    }

}

public sealed class TradeInventoryOutcomeAdapter :
    GameplayOutcomeAdapter<TradeInventoryOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        TradeInventoryOutcomeIds.Result;

    public override OutcomePrepareResult TryGetRequirements(
        in TradeInventoryOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            TradeInventoryOutcomeIds.Result,
            receipt.AbsoluteDay,
            receipt.Status,
            currentWorldEpoch,
            receipt.OwnerRevision,
            receipt.ParticipantCount,
            receipt.MetricCount,
            receipt.SubjectCount,
            receipt.TagCount,
            0,
            receipt.ProvenanceCount,
            receipt.FactCount);
        return TradeInventoryOutcomeSchema.ValidateReceipt(receipt, out string detail)
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, detail);
    }

    public override OutcomePrepareResult TryWrite(
        in TradeInventoryOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        bool written = true;
        for (int index = 0; index < receipt.ParticipantCount; index++)
            written &= builder.AddParticipant(receipt.GetParticipant(index));
        for (int index = 0; index < receipt.MetricCount; index++)
            written &= builder.AddMetric(receipt.GetMetric(index));
        for (int index = 0; index < receipt.SubjectCount; index++)
            written &= builder.AddSubject(receipt.GetSubject(index));
        for (int index = 0; index < receipt.TagCount; index++)
            written &= builder.AddTag(receipt.GetTag(index));
        for (int index = 0; index < receipt.ProvenanceCount; index++)
            written &= builder.AddProvenance(receipt.GetProvenance(index));
        for (int index = 0; index < receipt.FactCount; index++)
            written &= builder.AddFact(receipt.GetFact(index));
        if (receipt.Location.HasLocation)
            written &= builder.SetLocation(receipt.Location);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "trade-inventory-write-failed");
    }
}
