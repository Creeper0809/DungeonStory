using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The exact corpse debit and resulting survivor state frozen for observation.</summary>
public sealed class CharacterCorpseConsumedFact
{
    private readonly CharacterCorpseConsumptionExperienceData experience;

    public CharacterCorpseConsumedFact(
        in PhysicalItemBatchDispositionReceipt physical,
        CharacterCorpseConsumptionExperienceData experience)
    {
        if (experience == null) throw new ArgumentNullException(nameof(experience));
        if (!DomainFactValueSyntax.IsIdentity(experience.characterId)
            || !DomainFactValueSyntax.IsIdentity(experience.victimId)
            || !DomainFactValueSyntax.IsText(experience.characterName,
                DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(experience.victimName,
                DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(experience.victimSpecies, 256, false)
            || !DomainFactValueSyntax.IsText(experience.deathReason, 1024, true)
            || !DomainFactValueSyntax.IsText(experience.memory, 4096, false))
            throw new ArgumentException("corpse-consumption-experience-identity-invalid", nameof(experience));
        if (experience.absoluteDay <= 0
            || experience.memoriesBefore < 0 || experience.memoriesAfter > 24
            || experience.memoriesAfter < experience.memoriesBefore
            || !Finite(experience.hungerBefore) || !Finite(experience.hungerAfter)
            || !Finite(experience.hygieneBefore) || !Finite(experience.hygieneAfter)
            || !Finite(experience.infectionBefore) || !Finite(experience.infectionAfter)
            || !Finite(experience.moodBefore) || !Finite(experience.moodAfter))
            throw new ArgumentException("corpse-consumption-experience-values-invalid", nameof(experience));

        Physical = new PhysicalItemDisposedFact(physical, experience.absoluteDay);
        PhysicalItemBatchDispositionReceipt exact = Physical.Receipt;
        if (exact.Kind != PhysicalItemDispositionKind.Sink
            || exact.Quantity != 1
            || exact.SourceStackIds.Count != 1
            || exact.SourceFacts.Count != 1
            || exact.SourceFacts[0].Quantity != 1
            || !string.Equals(exact.SourceFacts[0].ItemDefinitionId,
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                StringComparison.Ordinal))
            throw new ArgumentException("corpse-consumption-physical-source-invalid", nameof(physical));

        this.experience = experience.Clone();
    }

    public PhysicalItemDisposedFact Physical { get; }
    public CharacterCorpseConsumptionExperienceData Experience => experience.Clone();

    public CharacterCorpseConsumptionFactData Snapshot() => new()
    {
        experience = experience.Clone(),
        physical = CapturePhysical()
    };

    public static CharacterCorpseConsumedFact FromSave(CharacterCorpseConsumptionFactData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.experience == null || data.physical == null
            || data.physical.sourceStackIds == null || data.physical.sourceFacts == null)
            throw new ArgumentException("corpse-consumption-snapshot-incomplete", nameof(data));

        PhysicalItemDisposedFact restored = PhysicalItemDisposedFact.FromSave(data.physical);
        var fact = new CharacterCorpseConsumedFact(restored.Receipt, data.experience);
        if (!PhysicalEquals(data.physical, fact.CapturePhysical()))
            throw new ArgumentException("corpse-consumption-snapshot-not-canonical", nameof(data));
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
        && left.sourceFacts.Zip(right.sourceFacts, SourceFactEquals).All(value => value);

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

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
