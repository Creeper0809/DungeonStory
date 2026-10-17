using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The exact residue debit and resulting knowledge reward frozen for observation.</summary>
public sealed class KnowledgeResidueCompletedFact
{
    private readonly KnowledgeCompletionExperienceData experience;

    public KnowledgeResidueCompletedFact(
        PhysicalItemBatchDispositionReceipt input,
        KnowledgeCompletionExperienceData experience)
    {
        if (experience == null) throw new ArgumentNullException(nameof(experience));
        if (!DomainFactValueSyntax.IsIdentity(experience.taskId)
            || !DomainFactValueSyntax.IsIdentity(experience.facilityId)
            || !DomainFactValueSyntax.IsText(experience.facilityName,
                DomainFactValueSyntax.MaximumNameCharacters, false)
            || !Enum.IsDefined(typeof(KnowledgeResidueUse), experience.use))
        {
            throw new ArgumentException(
                "knowledge-completion-experience-identity-invalid",
                nameof(experience));
        }

        bool codex = experience.use == KnowledgeResidueUse.CodexAnalysis;
        if (codex
                ? !string.Equals(experience.regionId, string.Empty,
                        StringComparison.Ordinal)
                    || !string.Equals(experience.regionName, string.Empty,
                        StringComparison.Ordinal)
                    || !DomainFactValueSyntax.IsText(experience.codexClue,
                        DomainFactValueSyntax.MaximumNameCharacters, false)
                : !DomainFactValueSyntax.IsIdentity(experience.regionId)
                    || !DomainFactValueSyntax.IsText(experience.regionName,
                        DomainFactValueSyntax.MaximumNameCharacters, false)
                    || !string.Equals(experience.codexClue, string.Empty,
                        StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "knowledge-completion-experience-target-invalid",
                nameof(experience));
        }
        if (experience.absoluteDay <= 0
            || !Finite(experience.rewardBefore)
            || !Finite(experience.rewardAfter)
            || experience.rewardBefore < 0
            || (codex
                ? experience.rewardBefore != Math.Floor(experience.rewardBefore)
                    || experience.rewardAfter != experience.rewardBefore + 1
                    || experience.rewardAfter <= experience.rewardBefore
                : experience.rewardBefore >= 100
                    || experience.rewardAfter != Math.Min(100, experience.rewardBefore + 10)))
        {
            throw new ArgumentException(
                "knowledge-completion-experience-values-invalid",
                nameof(experience));
        }

        Physical = new PhysicalItemDisposedFact(input, experience.absoluteDay);
        PhysicalItemBatchDispositionReceipt exact = Physical.Receipt;
        if (exact.Kind != PhysicalItemDispositionKind.Sink
            || exact.Quantity != 1
            || exact.SourceStackIds.Count != 1
            || exact.SourceFacts.Count != 1
            || exact.SourceFacts[0].Quantity != 1
            || !string.Equals(
                exact.SourceFacts[0].ItemDefinitionId,
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "knowledge-completion-physical-source-invalid",
                nameof(input));
        }

        this.experience = experience.Clone();
    }

    public PhysicalItemDisposedFact Physical { get; }
    public KnowledgeCompletionExperienceData Experience => experience.Clone();

    public KnowledgeCompletionFactData Snapshot() => new()
    {
        experience = experience.Clone(),
        physical = CapturePhysical()
    };

    public static KnowledgeResidueCompletedFact FromSave(
        KnowledgeCompletionFactData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.experience == null || data.physical == null
            || data.physical.sourceStackIds == null
            || data.physical.sourceFacts == null)
        {
            throw new ArgumentException(
                "knowledge-completion-snapshot-incomplete",
                nameof(data));
        }

        PhysicalItemDisposedFact restored =
            PhysicalItemDisposedFact.FromSave(data.physical);
        var fact = new KnowledgeResidueCompletedFact(
            restored.Receipt,
            data.experience);
        if (!PhysicalEquals(data.physical, fact.CapturePhysical()))
        {
            throw new ArgumentException(
                "knowledge-completion-snapshot-not-canonical",
                nameof(data));
        }
        return fact;
    }

    private PhysicalItemBatchDispositionSaveData CapturePhysical()
    {
        PhysicalItemBatchDispositionReceipt receipt = Physical.Receipt;
        return new PhysicalItemBatchDispositionSaveData
        {
            kind = (int)receipt.Kind,
            operationId = receipt.OperationId,
            reasonCode = receipt.ReasonCode,
            requestFingerprint = receipt.RequestFingerprint,
            sourceStackIds = new List<string>(receipt.SourceStackIds),
            quantity = receipt.Quantity,
            inputMassGrams = receipt.InputMassGrams,
            commitId = receipt.CommitId,
            ownerRevision = receipt.OwnerRevision,
            publicationExpected = false,
            publicationKey = string.Empty,
            publicationId = string.Empty,
            publicationFingerprint = string.Empty,
            publicationDelivered = false,
            defaultObservationDay = Physical.AbsoluteDay,
            sourceFacts = receipt.SourceFacts.Select(value =>
                new PhysicalItemDispositionSourceFactSaveData
                {
                    stackId = value.StackId,
                    itemDefinitionId = value.ItemDefinitionId,
                    itemInstanceId = value.ItemInstanceId,
                    quantity = value.Quantity,
                    massGrams = value.MassGrams,
                    sourceX = value.SourcePosition.x,
                    sourceY = value.SourcePosition.y,
                    displayText = value.DisplayName
                }).ToList()
        };
    }

    internal static bool PhysicalEquals(
        PhysicalItemBatchDispositionSaveData left,
        PhysicalItemBatchDispositionSaveData right) =>
        left.kind == right.kind
        && left.operationId == right.operationId
        && left.reasonCode == right.reasonCode
        && left.requestFingerprint == right.requestFingerprint
        && left.quantity == right.quantity
        && left.inputMassGrams == right.inputMassGrams
        && left.commitId == right.commitId
        && left.ownerRevision == right.ownerRevision
        && left.publicationExpected == right.publicationExpected
        && left.publicationKey == right.publicationKey
        && left.publicationId == right.publicationId
        && left.publicationFingerprint == right.publicationFingerprint
        && left.publicationDelivered == right.publicationDelivered
        && left.defaultObservationDay == right.defaultObservationDay
        && left.sourceStackIds.SequenceEqual(right.sourceStackIds)
        && left.sourceFacts.Count == right.sourceFacts.Count
        && left.sourceFacts.Zip(right.sourceFacts, SourceFactEquals)
            .All(value => value);

    private static bool SourceFactEquals(
        PhysicalItemDispositionSourceFactSaveData left,
        PhysicalItemDispositionSourceFactSaveData right) =>
        left != null && right != null
        && left.stackId == right.stackId
        && left.itemDefinitionId == right.itemDefinitionId
        && left.itemInstanceId == right.itemInstanceId
        && left.quantity == right.quantity
        && left.massGrams == right.massGrams
        && left.sourceX == right.sourceX
        && left.sourceY == right.sourceY
        && left.displayText == right.displayText;

    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
