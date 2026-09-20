using System;
using System.Linq;

public sealed class ApparelPhysicalOutcomeAdapter :
    GameplayOutcomeAdapter<ApparelPhysicalOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.ApparelPhysicalCompleted;

    public override OutcomePrepareResult TryGetRequirements(
        in ApparelPhysicalOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.OwnerRevision,
            receipt.AbsoluteDay,
            currentWorldEpoch,
            receipt.HasProductQuality);
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision >= 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.FacilityPersistentId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.OrderId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.OutputItemId)
            && receipt.Result.IsCompleted
            && receipt.Result.InputMassGrams >= 0L
            && receipt.Result.OutputMassGrams >= 0L
            && receipt.AbsoluteDay >= 0
            && (receipt.QualitySchemaVersion == 0
                || receipt.QualitySchemaVersion == 1
                    && GameplayOutcomeStableIdSyntax.IsValid(
                        receipt.MakerCharacterId)
                    && HasValidName(receipt.MakerDisplayName)
                    && Enum.IsDefined(
                        typeof(CraftsmanshipQualityTier),
                        receipt.Quality)
                    && receipt.AttemptIndex == receipt.OwnerRevision);
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "apparel-physical-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in ApparelPhysicalOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            EvolutionOutcomeIds.FacilityKind,
            receipt.FacilityPersistentId);
        GameplayEntityId output = new(
            EvolutionOutcomeIds.ApparelKind,
            receipt.OutputItemId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                   facility,
                   EvolutionOutcomeIds.PhysicalTransactionOwnerRole,
                   GameplayParticipationKind.Direct,
                   true,
                   receipt.FacilityDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility, 0.55f, NarrativeMemoryTier.Recent, false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.PhysicalInputMassMetric,
                receipt.Result.InputMassGrams,
                EvolutionOutcomeIds.GramUnit,
                output))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.PhysicalOutputMassMetric,
                receipt.Result.OutputMassGrams,
                EvolutionOutcomeIds.GramUnit,
                output))
            && builder.AddTag(EvolutionOutcomeIds.PhysicalTransactionTag)
            && builder.AddAnchor(facility, HashEvidence(
                "apparel-order-sha256", receipt.OrderId))
            && builder.AddAnchor(facility, HashEvidence(
                "output-stack-sha256", receipt.Result.OutputStackId))
            && builder.AddAnchor(facility, HashEvidence(
                "output-instance-sha256", receipt.Result.OutputInstanceId));
        if (written && receipt.HasProductQuality)
        {
            GameplayEntityId maker = new(
                EvolutionOutcomeIds.CharacterKind,
                receipt.MakerCharacterId);
            written = builder.AddParticipant(new GameplayOutcomeParticipant(
                    maker,
                    EvolutionOutcomeIds.ProductMakerRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.MakerDisplayName))
                && builder.AddSubject(new GameplayOutcomeSubjectLink(
                    maker, 0.65f, NarrativeMemoryTier.Episodic, false, false, 0))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    EvolutionOutcomeIds.ProductQualityTierMetric,
                    (int)receipt.Quality,
                    EvolutionOutcomeIds.EnumUnit,
                    output))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    EvolutionOutcomeIds.ProductQualityAttemptMetric,
                    receipt.AttemptIndex,
                    EvolutionOutcomeIds.CountUnit,
                    output))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    EvolutionOutcomeIds.ProductRejectedMetric,
                    receipt.RejectedBelowMinimum ? 1d : 0d,
                    EvolutionOutcomeIds.BooleanUnit,
                    output))
                && builder.AddTag(EvolutionOutcomeIds.ProductQualityTag);
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : Failed("apparel-physical-write-failed");
    }

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        long ownerRevision,
        int absoluteDay,
        long worldEpoch,
        bool hasProductQuality) => new(
        resultKey,
        EvolutionOutcomeIds.ApparelPhysicalCompleted,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        ownerRevision,
        hasProductQuality ? 2 : 1,
        hasProductQuality ? 5 : 2,
        hasProductQuality ? 2 : 1,
        hasProductQuality ? 2 : 1,
        3);

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static bool HasValidName(
        DungeonStory.Narrative.Korean.KoreanNameSnapshot value) =>
        !string.IsNullOrWhiteSpace(value.DisplayText)
        && !string.IsNullOrWhiteSpace(value.DisplaySnapshotRevision)
        && !string.IsNullOrWhiteSpace(value.Locale);

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class ProductQualityOutcomeAdapter :
    GameplayOutcomeAdapter<ProductQualityOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.ProductQualityResolved;

    public override OutcomePrepareResult TryGetRequirements(
        in ProductQualityOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            1, 3, 1, 2, 1);
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision >= 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.MakerCharacterId)
            && HasValidName(receipt.MakerDisplayName)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.DefinitionId)
            && Enum.IsDefined(typeof(CraftsmanshipQualityTier), receipt.Quality)
            && receipt.AttemptIndex == receipt.OwnerRevision
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "product-quality-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in ProductQualityOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId maker = new(
            EvolutionOutcomeIds.CharacterKind,
            receipt.MakerCharacterId);
        GameplayEntityId product = new(
            EvolutionOutcomeIds.EquipmentKind,
            receipt.DefinitionId);
        return builder.AddParticipant(new GameplayOutcomeParticipant(
                   maker,
                   EvolutionOutcomeIds.ProductMakerRole,
                   GameplayParticipationKind.Direct,
                   true,
                   receipt.MakerDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                maker, 0.7f, NarrativeMemoryTier.Episodic, false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ProductQualityTierMetric,
                (int)receipt.Quality,
                EvolutionOutcomeIds.EnumUnit,
                product))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ProductQualityAttemptMetric,
                receipt.AttemptIndex,
                EvolutionOutcomeIds.CountUnit,
                product))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ProductRejectedMetric,
                receipt.RejectedBelowMinimum ? 1d : 0d,
                EvolutionOutcomeIds.BooleanUnit,
                product))
            && builder.AddTag(EvolutionOutcomeIds.EquipmentTag)
            && builder.AddTag(EvolutionOutcomeIds.ProductQualityTag)
            && builder.AddAnchor(maker, new NarrativeEvidenceReference(
                "product-definition-sha256",
                NarrativeInferenceHash.ComputeSha256Utf8(
                    receipt.DefinitionId)))
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.AdapterWriteFailed,
                    "product-quality-write-failed");
    }

    private static bool HasValidName(
        DungeonStory.Narrative.Korean.KoreanNameSnapshot value) =>
        !string.IsNullOrWhiteSpace(value.DisplayText)
        && !string.IsNullOrWhiteSpace(value.DisplaySnapshotRevision)
        && !string.IsNullOrWhiteSpace(value.Locale);
}

public sealed class AcquiredTraitInferenceOutcomeAdapter :
    GameplayOutcomeAdapter<AcquiredTraitInferenceOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.TraitInferenceCompleted;

    public override OutcomePrepareResult TryGetRequirements(
        in AcquiredTraitInferenceOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        CharacterAcquiredTraitInferenceAuditRecord audit = receipt.Result.Audit;
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            audit.ResultingRevision,
            1, 2, 1, 1, 3);
        bool valid = receipt.OperationId.IsValid
            && receipt.Result.Succeeded
            && audit.CommandKind == CharacterAcquiredTraitInferenceCommandKind.Completion
            && GameplayOutcomeStableIdSyntax.IsValid(audit.TargetPersistentId)
            && !string.IsNullOrWhiteSpace(audit.AuditId)
            && !string.IsNullOrWhiteSpace(audit.CandidatePacketHash)
            && audit.ExpectedRevision >= 0
            && audit.ResultingRevision > audit.ExpectedRevision
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "acquired-trait-inference-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in AcquiredTraitInferenceOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        CharacterAcquiredTraitInferenceAuditRecord audit = receipt.Result.Audit;
        GameplayEntityId character = new(
            EvolutionOutcomeIds.CharacterKind,
            audit.TargetPersistentId);
        return builder.AddParticipant(new GameplayOutcomeParticipant(
                   character,
                   EvolutionOutcomeIds.AcquiredTraitOwnerRole,
                   GameplayParticipationKind.Direct,
                   true,
                   receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character, 0.85f, NarrativeMemoryTier.Recent, true, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.TraitExpectedRevisionMetric,
                audit.ExpectedRevision,
                EvolutionOutcomeIds.RevisionUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.TraitResultingRevisionMetric,
                audit.ResultingRevision,
                EvolutionOutcomeIds.RevisionUnit,
                character))
            && builder.AddTag(EvolutionOutcomeIds.TraitInferenceTag)
            && builder.AddAnchor(character, HashEvidence(
                "acquired-trait-audit-sha256", audit.AuditId))
            && builder.AddAnchor(character, HashEvidence(
                "candidate-packet-sha256", audit.CandidatePacketHash))
            && builder.AddAnchor(character, HashEvidence(
                "selected-combination-sha256", audit.SelectedCombinationId))
                ? OutcomePrepareResult.Prepared()
                : Failed("acquired-trait-inference-write-failed");
    }

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class MemoryErasureBossAwardOutcomeAdapter :
    GameplayOutcomeAdapter<MemoryErasureBossAwardOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.MemoryErasureBossAwarded;

    public override OutcomePrepareResult TryGetRequirements(
        in MemoryErasureBossAwardOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch);
        bool valid = receipt.OperationId.IsValid
            && receipt.Result.Status == MemoryErasureSealBossAwardStatus.Awarded
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.Result.RegionId)
            && !string.IsNullOrWhiteSpace(receipt.Result.PhysicalCommitId)
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "memory-erasure-boss-award-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in MemoryErasureBossAwardOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId region = new(
            EvolutionOutcomeIds.OffenseRegionKind,
            receipt.Result.RegionId);
        return builder.AddParticipant(new GameplayOutcomeParticipant(
                   region,
                   EvolutionOutcomeIds.AwardedRegionRole,
                   GameplayParticipationKind.Direct,
                   true,
                   receipt.RegionDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                region, 0.9f, NarrativeMemoryTier.Recent, true, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.AwardedItemCountMetric,
                MemoryErasureSealItemRules.UseQuantity,
                EvolutionOutcomeIds.CountUnit,
                region))
            && builder.AddTag(EvolutionOutcomeIds.BossAwardTag)
            && builder.AddAnchor(region, HashEvidence(
                "physical-commit-sha256", receipt.Result.PhysicalCommitId))
                ? OutcomePrepareResult.Prepared()
                : Failed("memory-erasure-boss-award-write-failed");
    }

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        int absoluteDay,
        long worldEpoch) => new(
        resultKey,
        EvolutionOutcomeIds.MemoryErasureBossAwarded,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        0L,
        1, 1, 1, 1, 1);

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class ApparelChangeOutcomeAdapter :
    GameplayOutcomeAdapter<ApparelChangeOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.ApparelChanged;

    public override OutcomePrepareResult TryGetRequirements(
        in ApparelChangeOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            1, 2, 1, 1, 0);
        return ValidateCommon(receipt.OperationId, receipt.CharacterId.IsValid,
            receipt.ApparelId, receipt.AbsoluteDay);
    }

    public override OutcomePrepareResult TryWrite(
        in ApparelChangeOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            EvolutionOutcomeIds.CharacterKind,
            receipt.CharacterId.Value);
        GameplayEntityId apparel = new(
            EvolutionOutcomeIds.ApparelKind,
            receipt.ApparelId);
        return builder.AddParticipant(new GameplayOutcomeParticipant(
                   character,
                   EvolutionOutcomeIds.WearerRole,
                   GameplayParticipationKind.Direct,
                   true))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character, 0.45f, NarrativeMemoryTier.Recent, false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ApparelEquippedMetric,
                receipt.Equipped ? 1d : 0d,
                EvolutionOutcomeIds.BooleanUnit,
                apparel))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ApparelOriginMetric,
                (int)receipt.Origin,
                EvolutionOutcomeIds.EnumUnit,
                apparel))
            && builder.AddTag(EvolutionOutcomeIds.EquipmentTag)
                ? OutcomePrepareResult.Prepared()
                : Failed("apparel-change-write-failed");
    }

    private static OutcomePrepareResult ValidateCommon(
        GameplayOperationId operationId,
        bool entityValid,
        string definitionId,
        int absoluteDay) => operationId.IsValid
        && entityValid
        && GameplayOutcomeStableIdSyntax.IsValid(definitionId)
        && absoluteDay >= 0
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "apparel-change-receipt-invalid");

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class AcquiredTraitReactionOutcomeAdapter :
    GameplayOutcomeAdapter<AcquiredTraitReactionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.TraitReactionExecuted;

    public override OutcomePrepareResult TryGetRequirements(
        in AcquiredTraitReactionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            1, 1, 1, 1, 2);
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision >= 0L
            && receipt.CharacterId.IsValid
            && !string.IsNullOrWhiteSpace(receipt.TraitInstanceId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.ReactionId)
            && float.IsFinite(receipt.AppliedValue)
            && receipt.AbsoluteDay >= 0
            && Enum.IsDefined(typeof(CharacterAcquiredTraitReactionAction), receipt.Action);
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "trait-reaction-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in AcquiredTraitReactionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            EvolutionOutcomeIds.CharacterKind,
            receipt.CharacterId.Value);
        GameplayEntityId reaction = new(
            EvolutionOutcomeIds.TraitReactionKind,
            receipt.ReactionId);
        GameplayMetricId metric = receipt.Action ==
                CharacterAcquiredTraitReactionAction.GrantExperience
            ? EvolutionOutcomeIds.TraitExperienceMetric
            : EvolutionOutcomeIds.TraitMoodMetric;
        return builder.AddParticipant(new GameplayOutcomeParticipant(
                   character,
                   EvolutionOutcomeIds.TraitOwnerRole,
                   GameplayParticipationKind.Direct,
                   true))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character, 0.65f, NarrativeMemoryTier.Recent, false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                metric,
                receipt.AppliedValue,
                EvolutionOutcomeIds.EffectPointUnit,
                reaction))
            && builder.AddTag(EvolutionOutcomeIds.TraitTag)
            && builder.AddAnchor(character, HashEvidence(
                "trait-instance-sha256", receipt.TraitInstanceId))
            && builder.AddAnchor(character, HashEvidence(
                "trait-reaction-sha256", receipt.ReactionId))
                ? OutcomePrepareResult.Prepared()
                : Failed("trait-reaction-write-failed");
    }

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class FacilityEvolutionOutcomeAdapter :
    GameplayOutcomeAdapter<FacilityEvolutionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.FacilityEvolutionCompleted;

    public override OutcomePrepareResult TryGetRequirements(
        in FacilityEvolutionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.OwnerRevision,
            receipt.AbsoluteDay,
            receipt.HasMaterialCommit,
            currentWorldEpoch);
        bool materialValid = !receipt.HasMaterialCommit
            ? receipt.SourceStackIds.Count == 0
                && receipt.MaterialQuantity == 0
                && receipt.MaterialInputMassGrams == 0L
            : receipt.SourceStackIds.Count > 0
                && receipt.SourceStackIds.All(value => !string.IsNullOrWhiteSpace(value))
                && receipt.MaterialQuantity > 0
                && receipt.MaterialInputMassGrams > 0L;
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision > 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.FacilityPersistentId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.RecipeId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.ResultFacilityDefinitionId)
            && receipt.ResultStarGrade > 0
            && receipt.AbsoluteDay >= 0
            && materialValid;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "facility-evolution-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in FacilityEvolutionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            EvolutionOutcomeIds.FacilityKind,
            receipt.FacilityPersistentId);
        GameplayEntityId resultDefinition = new(
            EvolutionOutcomeIds.FacilityDefinitionKind,
            receipt.ResultFacilityDefinitionId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                EvolutionOutcomeIds.EvolvedFacilityRole,
                GameplayParticipationKind.Direct,
                true))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility, 0.9f, NarrativeMemoryTier.Recent, true, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.FacilityStarGradeMetric,
                receipt.ResultStarGrade,
                EvolutionOutcomeIds.GradeUnit,
                resultDefinition))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.MaterialQuantityMetric,
                receipt.MaterialQuantity,
                EvolutionOutcomeIds.CountUnit,
                resultDefinition))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.MaterialMassMetric,
                receipt.MaterialInputMassGrams,
                EvolutionOutcomeIds.GramUnit,
                resultDefinition))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.MaterialSourceCountMetric,
                receipt.SourceStackIds.Count,
                EvolutionOutcomeIds.CountUnit,
                resultDefinition))
            && builder.AddTag(EvolutionOutcomeIds.FacilityTag);
        if (written && receipt.HasMaterialCommit)
        {
            written = builder.AddAnchor(facility, HashEvidence(
                    "material-commit-sha256", receipt.MaterialCommitId))
                && builder.AddAnchor(facility, HashEvidence(
                    "material-source-set-sha256",
                    string.Join("|", receipt.SourceStackIds)));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : Failed("facility-evolution-write-failed");
    }

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        long ownerRevision,
        int absoluteDay,
        bool hasMaterialCommit,
        long worldEpoch) => new(
            resultKey,
            EvolutionOutcomeIds.FacilityEvolutionCompleted,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            worldEpoch,
            ownerRevision,
            1, 4, 1, 1, hasMaterialCommit ? 2 : 0);

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class MemoryErasureOutcomeAdapter :
    GameplayOutcomeAdapter<MemoryErasureOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EvolutionOutcomeIds.MemoryErasureTerminal;

    public override OutcomePrepareResult TryGetRequirements(
        in MemoryErasureOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        bool hasPhysicalCommit = !string.IsNullOrWhiteSpace(
            receipt.Result.PhysicalCommitId);
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            MapStatus(receipt.Result.Status),
            hasPhysicalCommit,
            currentWorldEpoch);
        bool valid = receipt.OperationId.IsValid
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.Result.TargetCharacterId)
            && !string.IsNullOrWhiteSpace(receipt.Result.TraitInstanceId)
            && !string.IsNullOrWhiteSpace(receipt.Result.AuditId)
            && receipt.Result.PreviousRevision >= 0
            && receipt.Result.CommittedRevision >= 0
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "memory-erasure-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in MemoryErasureOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            EvolutionOutcomeIds.CharacterKind,
            receipt.Result.TargetCharacterId);
        bool consumed = receipt.Result.Status == MemoryErasureSealUseStatus.Succeeded;
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                EvolutionOutcomeIds.ErasureTargetRole,
                GameplayParticipationKind.Direct,
                true))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character, consumed ? 1f : 0.6f,
                NarrativeMemoryTier.Recent, consumed, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.MemoryErasureStatusMetric,
                (int)receipt.Result.Status,
                EvolutionOutcomeIds.EnumUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.PreviousRevisionMetric,
                receipt.Result.PreviousRevision,
                EvolutionOutcomeIds.RevisionUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.CommittedRevisionMetric,
                receipt.Result.CommittedRevision,
                EvolutionOutcomeIds.RevisionUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                EvolutionOutcomeIds.ConsumedSealMetric,
                consumed ? MemoryErasureSealItemRules.UseQuantity : 0,
                EvolutionOutcomeIds.CountUnit,
                character))
            && builder.AddTag(EvolutionOutcomeIds.MemoryErasureTag)
            && builder.AddAnchor(character, HashEvidence(
                "memory-erasure-audit-sha256", receipt.Result.AuditId));
        if (written && !string.IsNullOrWhiteSpace(receipt.Result.PhysicalCommitId))
        {
            written = builder.AddAnchor(character, HashEvidence(
                "physical-commit-sha256", receipt.Result.PhysicalCommitId));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : Failed("memory-erasure-write-failed");
    }

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        int absoluteDay,
        GameplayOutcomeStatus status,
        bool hasPhysicalCommit,
        long worldEpoch) => new(
            resultKey,
            EvolutionOutcomeIds.MemoryErasureTerminal,
            absoluteDay,
            status,
            worldEpoch,
            0L,
            1, 4, 1, 1, hasPhysicalCommit ? 2 : 1);

    public static GameplayOutcomeStatus MapStatus(MemoryErasureSealUseStatus status) =>
        status switch
        {
            MemoryErasureSealUseStatus.Succeeded or
                MemoryErasureSealUseStatus.AlreadyCompleted =>
                GameplayOutcomeStatus.Succeeded,
            MemoryErasureSealUseStatus.InterruptedBeforePickup or
                MemoryErasureSealUseStatus.InterruptedAfterPickup =>
                GameplayOutcomeStatus.Cancelled,
            MemoryErasureSealUseStatus.TargetChanged or
                MemoryErasureSealUseStatus.InvalidRequest =>
                GameplayOutcomeStatus.Blocked,
            _ => GameplayOutcomeStatus.Failed
        };

    private static NarrativeEvidenceReference HashEvidence(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value ?? string.Empty));

    private static OutcomePrepareResult Failed(string detail) =>
        new(OutcomePrepareCode.AdapterWriteFailed, detail);
}
