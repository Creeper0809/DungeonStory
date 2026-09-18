using System;
using System.Linq;

public sealed class ProductionCompletedOutcomeAdapter :
    GameplayOutcomeAdapter<ProductionCompletedOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCombatOutcomeIds.ProductionCompleted;

    public override OutcomePrepareResult TryGetRequirements(
        in ProductionCompletedOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.BillId.IsValid
            || receipt.CycleSequence <= 0
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.RecipeId)
            || !receipt.FacilityId.IsValid
            || receipt.ProducedQuantity < 0
            || receipt.ProducedMassGrams < 0L
            || receipt.DeclaredLossMassGrams < 0L
            || receipt.AbsoluteDay <= 0
            || receipt.HasWorker
                && !GameplayOutcomeStableIdSyntax.IsValid(receipt.WorkerPersistentId))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "production-completed-receipt-invalid");
        }

        int actorCount = receipt.HasWorker ? 2 : 1;
        int metricCount = checked(8 + receipt.OutputLines.Count * 2);
        int provenanceCount = checked(
            6
            + (receipt.HasWipInput ? 1 : 0)
            + receipt.OutputLines.Sum(value => 1 + value.CommitIds.Count)
            + receipt.PhysicalStacks.Count
            + receipt.PhysicalStacks.Count(value =>
                !string.IsNullOrEmpty(value.ItemInstanceId))
            + receipt.PhysicalStacks.Count(value =>
                !string.IsNullOrEmpty(value.SourceCommitId))
            + receipt.PhysicalStacks.Count(value =>
                !string.IsNullOrEmpty(value.ComponentSignature)));
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.CycleSequence,
            actorCount,
            metricCount,
            actorCount,
            1,
            0,
            provenanceCount: provenanceCount);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in ProductionCompletedOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new GameplayEntityId(
            ProductionCombatOutcomeIds.FacilityKind,
            receipt.FacilityId.Value);
        GameplayEntityId recipe = new GameplayEntityId(
            ProductionCombatOutcomeIds.RecipeKind,
            receipt.RecipeId);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                ProductionCombatOutcomeIds.ProducerRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.FacilityDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.7f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0)))
        {
            return Failed("production-facility-write-failed");
        }

        if (receipt.HasWorker)
        {
            GameplayEntityId worker = new GameplayEntityId(
                ProductionCombatOutcomeIds.CharacterKind,
                receipt.WorkerPersistentId);
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    worker,
                    ProductionCombatOutcomeIds.WorkerRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.WorkerDisplayName))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    worker,
                    0.65f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0)))
            {
                return Failed("production-worker-write-failed");
            }
        }

        if (!builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.ProducedQuantityMetric,
                receipt.ProducedQuantity,
                ProductionCombatOutcomeIds.CountUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.ProducedMassMetric,
                receipt.ProducedMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.DeclaredLossMetric,
                receipt.DeclaredLossMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.DeclaredExternalInputMetric,
                receipt.DeclaredExternalInputMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.WipInputQuantityMetric,
                receipt.WipInputQuantity,
                ProductionCombatOutcomeIds.CountUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.WipInputMassMetric,
                receipt.WipInputMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.CleanWaterMetric,
                receipt.CleanWaterMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.WastewaterMetric,
                receipt.WastewaterMassGrams,
                ProductionCombatOutcomeIds.GramUnit,
                recipe))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "production-bill",
                receipt.BillId.Value))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "recipe-definition",
                receipt.RecipeId))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "production-batch",
                receipt.BatchCommitId))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "outcome-fingerprint",
                receipt.OutcomeFingerprint))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "planned-output-fingerprint",
                receipt.PlannedOutputFingerprint))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "output-destination",
                receipt.DestinationId))
            || !builder.AddTag(ProductionCombatOutcomeIds.ProductionTag))
        {
            return Failed("production-metric-write-failed");
        }

        if (receipt.HasWipInput
            && !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "wip-input-commit",
                receipt.WipInputCommitId)))
        {
            return Failed("production-wip-provenance-write-failed");
        }

        for (int index = 0; index < receipt.OutputLines.Count; index++)
        {
            ProductionOutcomeLineSnapshot line = receipt.OutputLines[index];
            GameplayEntityId item = new GameplayEntityId(
                ProductionCombatOutcomeIds.ItemKind,
                line.ItemId);
            if (!builder.AddMetric(new GameplayOutcomeMetric(
                    ProductionCombatOutcomeIds.ProducedQuantityMetric,
                    line.Quantity,
                    ProductionCombatOutcomeIds.CountUnit,
                    item))
                || !builder.AddMetric(new GameplayOutcomeMetric(
                    ProductionCombatOutcomeIds.ProducedMassMetric,
                    line.MassGrams,
                    ProductionCombatOutcomeIds.GramUnit,
                    item))
                || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "output-capability-fingerprint",
                    line.CapabilityFingerprint)))
            {
                return Failed("production-output-line-write-failed");
            }

            for (int commitIndex = 0;
                commitIndex < line.CommitIds.Count;
                commitIndex++)
            {
                if (!builder.AddProvenance(
                        new GameplayOutcomeProvenanceReference(
                            "output-line-commit",
                            line.CommitIds[commitIndex])))
                {
                    return Failed("production-output-commit-write-failed");
                }
            }
        }

        for (int index = 0; index < receipt.PhysicalStacks.Count; index++)
        {
            ProductionOutcomePhysicalStackSnapshot stack =
                receipt.PhysicalStacks[index];
            if (!builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "physical-output-stack",
                    stack.StackId))
                || !string.IsNullOrEmpty(stack.ItemInstanceId)
                && !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "physical-item-instance",
                    stack.ItemInstanceId))
                || !string.IsNullOrEmpty(stack.SourceCommitId)
                && !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "physical-source-commit",
                    stack.SourceCommitId))
                || !string.IsNullOrEmpty(stack.ComponentSignature)
                && !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "physical-component-signature",
                    stack.ComponentSignature)))
            {
                return Failed("production-output-stack-write-failed");
            }
        }

        return OutcomePrepareResult.Prepared();
    }

    private static OutcomePrepareResult Failed(string detail) =>
        new OutcomePrepareResult(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class CombatDamageOutcomeAdapter :
    GameplayOutcomeAdapter<CombatDamageOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ProductionCombatOutcomeIds.CombatDamageResolved;

    public override OutcomePrepareResult TryGetRequirements(
        in CombatDamageOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!GameplayOutcomeStableIdSyntax.IsValid(receipt.AttackOperationId)
            || receipt.AttackRevision < 0L
            || !receipt.AttackerId.IsValid
            || !receipt.VictimId.IsValid
            || !float.IsFinite(receipt.ActualDamage)
            || receipt.ActualDamage <= 0f
            || receipt.AbsoluteDay <= 0)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "combat-damage-receipt-invalid");
        }

        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch,
            receipt.AttackRevision);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in CombatDamageOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId attacker = new GameplayEntityId(
            ProductionCombatOutcomeIds.CharacterKind,
            receipt.AttackerId.Value);
        GameplayEntityId victim = new GameplayEntityId(
            ProductionCombatOutcomeIds.CharacterKind,
            receipt.VictimId.Value);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                attacker,
                ProductionCombatOutcomeIds.AttackerRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.AttackerDisplayName))
            || !builder.AddParticipant(new GameplayOutcomeParticipant(
                victim,
                ProductionCombatOutcomeIds.VictimRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.VictimDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                attacker,
                0.75f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                victim,
                0.95f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ProductionCombatOutcomeIds.DamageMetric,
                receipt.ActualDamage,
                ProductionCombatOutcomeIds.HealthPointUnit,
                victim))
            || !builder.AddTag(ProductionCombatOutcomeIds.CombatTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "attack-operation",
                receipt.AttackOperationId))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCombatOutcomeIds.DamageTypeFact,
                receipt.DamageType.ToString()))
            || !builder.AddFact(new GameplayOutcomeFact(
                ProductionCombatOutcomeIds.BodyPartFact,
                receipt.BodyPart.ToString()))
            || !builder.SetLocation(new GameplayLocationReference(
                string.Empty,
                string.Empty,
                receipt.VictimCell.x,
                receipt.VictimCell.y)))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "combat-damage-write-failed");
        }

        return OutcomePrepareResult.Prepared();
    }

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        int absoluteDay,
        long worldEpoch,
        long ownerRevision) =>
        new OutcomeWriteRequirements(
            resultKey,
            ProductionCombatOutcomeIds.CombatDamageResolved,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            ownerRevision,
            2,
            1,
            2,
            1,
            0,
            provenanceCount: 1,
            factCount: 2);
}
