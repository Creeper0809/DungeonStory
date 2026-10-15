using System;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation.Facts;

/// <summary>Exact physical debit and its fire context, with no receipt or logging policy dependency.</summary>
public abstract class EnvironmentalFireResourceFact
{
    private readonly EnvironmentalFireResourceRecord snapshot;
    protected EnvironmentalFireResourceFact(EnvironmentalFireResourceRecord source, EnvironmentalFireResourceUse expected)
    {
        if (source == null || source.use != expected || source.absoluteDay <= 0 || source.physical == null)
            throw new ArgumentException("fire-resource-fact-invalid");
        var physical = PhysicalItemDisposedFact.FromSave(source.physical);
        var receipt = physical.Receipt;
        if (receipt.Kind != PhysicalItemDispositionKind.Sink || source.absoluteDay != physical.AbsoluteDay
            || source.physical.commitId != receipt.CommitId || source.physical.publicationExpected
            || source.physical.publicationDelivered || source.physical.publicationKey != string.Empty
            || source.physical.publicationId != string.Empty || source.physical.publicationFingerprint != string.Empty
            || receipt.SourceFacts.Any(value => value.SourcePosition.x != source.positionX || value.SourcePosition.y != source.positionY))
            throw new ArgumentException("fire-resource-debit-invalid");
        if (expected == EnvironmentalFireResourceUse.FuelLoss)
        {
            if (!DomainFactValueSyntax.IsIdentity(source.targetId) || receipt.SourceFacts.Count != 1
                || receipt.SourceFacts[0].StackId != source.targetId
                || receipt.ReasonCode != EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode
                || source.fireId != string.Empty || source.leaseId != string.Empty
                || source.workerId != string.Empty || source.workerName != string.Empty)
                throw new ArgumentException("fire-resource-fuel-context-invalid");
        }
        else
        {
            if (source.targetId != string.Empty || !DomainFactValueSyntax.IsIdentity(source.fireId)
                || !DomainFactValueSyntax.IsIdentity(source.leaseId) || !DomainFactValueSyntax.IsIdentity(source.workerId)
                || !DomainFactValueSyntax.IsText(source.workerName, DomainFactValueSyntax.MaximumNameCharacters, false)
                || receipt.ReasonCode != EnvironmentalFireWorldAdapter.WaterSinkReasonCode
                || receipt.SourceFacts.Any(value => value.ItemDefinitionId != EnvironmentalFireWorldAdapter.CleanWaterItemId
                    || value.DisplayName != receipt.SourceFacts[0].DisplayName))
                throw new ArgumentException("fire-resource-water-context-invalid");
        }
        snapshot = source.Clone();
        snapshot.publicationKey = string.Empty;
        Physical = physical;
    }
    public PhysicalItemDisposedFact Physical { get; }
    public EnvironmentalFireResourceRecord Snapshot() => snapshot.Clone();
}

public sealed class EnvironmentalFireFuelLostFact : EnvironmentalFireResourceFact
{
    public EnvironmentalFireFuelLostFact(EnvironmentalFireResourceRecord value) : base(value, EnvironmentalFireResourceUse.FuelLoss) { }
}
public sealed class EnvironmentalFireWaterConsumedFact : EnvironmentalFireResourceFact
{
    public EnvironmentalFireWaterConsumedFact(EnvironmentalFireResourceRecord value) : base(value, EnvironmentalFireResourceUse.SuppressionWater) { }
}
