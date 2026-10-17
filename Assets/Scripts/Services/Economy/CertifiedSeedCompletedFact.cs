using System;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Immutable certified-seed completion captured from the exact preallocated
/// physical output batch.
/// </summary>
public sealed class CertifiedSeedCompletedFact
{
    public CertifiedSeedCompletedFact(
        CertifiedSeedPlanExecutionReceipt sourceReceipt,
        string facilityDisplayName,
        string cropDisplayName,
        string outputItemDefinitionId,
        string outputItemDisplayName,
        int outputQuantity,
        long outputMassGrams,
        string outputOutcomeFingerprint,
        int absoluteDay,
        long ownerRevision)
    {
        SourceReceipt = sourceReceipt
            ?? throw new ArgumentNullException(nameof(sourceReceipt));
        if (!DomainFactValueSyntax.IsIdentity(sourceReceipt.ActionId)
            || !DomainFactValueSyntax.IsIdentity(sourceReceipt.OrderId)
            || sourceReceipt.OrderSequence < 0
            || !DomainFactValueSyntax.IsIdentity(sourceReceipt.FacilityInstanceId)
            || !DomainFactValueSyntax.IsIdentity(sourceReceipt.CropId)
            || !string.Equals(sourceReceipt.DestinationId,
                CertifiedSeedInputOwnerAuthority.BuildDestinationId(
                    sourceReceipt.FacilityInstanceId, sourceReceipt.CropId, sourceReceipt.OrderSequence),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "certified-seed-completion-source-invalid",
                nameof(sourceReceipt));
        }
        FacilityDisplayName = RequireDisplaySnapshot(
            facilityDisplayName,
            nameof(facilityDisplayName));
        CropDisplayName = RequireDisplaySnapshot(
            cropDisplayName,
            nameof(cropDisplayName));
        OutputItemDefinitionId = RequireIdentity(
            outputItemDefinitionId,
            nameof(outputItemDefinitionId));
        OutputItemDisplayName = RequireDisplaySnapshot(
            outputItemDisplayName,
            nameof(outputItemDisplayName));
        if (outputQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputQuantity));
        if (outputMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(outputMassGrams));
        OutputOutcomeFingerprint = RequireFingerprint(
            outputOutcomeFingerprint,
            nameof(outputOutcomeFingerprint));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        OutputQuantity = outputQuantity;
        OutputMassGrams = outputMassGrams;
        AbsoluteDay = absoluteDay;
        OwnerRevision = ownerRevision;
    }

    public CertifiedSeedPlanExecutionReceipt SourceReceipt { get; }
    public string FacilityDisplayName { get; }
    public string CropDisplayName { get; }
    public string OutputItemDefinitionId { get; }
    public string OutputItemDisplayName { get; }
    public int OutputQuantity { get; }
    public long OutputMassGrams { get; }
    public string OutputOutcomeFingerprint { get; }
    public int AbsoluteDay { get; }
    public long OwnerRevision { get; }

    public CertifiedSeedCompletionFactData Snapshot() => new()
    {
        actionId = SourceReceipt.ActionId,
        orderId = SourceReceipt.OrderId,
        orderSequence = SourceReceipt.OrderSequence,
        destinationId = SourceReceipt.DestinationId,
        facilityInstanceId = SourceReceipt.FacilityInstanceId,
        cropId = SourceReceipt.CropId,
        facilityDisplayName = FacilityDisplayName,
        cropDisplayName = CropDisplayName,
        outputItemDefinitionId = OutputItemDefinitionId,
        outputItemDisplayName = OutputItemDisplayName,
        outputQuantity = OutputQuantity,
        outputMassGrams = OutputMassGrams,
        outputOutcomeFingerprint = OutputOutcomeFingerprint,
        absoluteDay = AbsoluteDay,
        ownerRevision = OwnerRevision
    };

    public static CertifiedSeedCompletedFact FromSave(
        CertifiedSeedCompletionFactData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        CertifiedSeedPlanExecutionReceipt source =
            CertifiedSeedPlanExecutionReceipt.CaptureIdentifiers(
                data.actionId,
                data.orderId,
                data.orderSequence,
                data.destinationId,
                data.facilityInstanceId,
                data.cropId);
        return new CertifiedSeedCompletedFact(
            source,
            data.facilityDisplayName,
            data.cropDisplayName,
            data.outputItemDefinitionId,
            data.outputItemDisplayName,
            data.outputQuantity,
            data.outputMassGrams,
            data.outputOutcomeFingerprint,
            data.absoluteDay,
            data.ownerRevision);
    }

    private static string RequireDisplaySnapshot(
        string value,
        string parameterName) =>
        !DomainFactValueSyntax.IsText(
            value,
            DomainFactValueSyntax.MaximumNameCharacters,
            false)
            ? throw new ArgumentException(
                "An immutable display snapshot is required.",
                parameterName)
            : value;

    private static string RequireIdentity(
        string value,
        string parameterName) =>
        !DomainFactValueSyntax.IsIdentity(value)
            ? throw new ArgumentException(
                "A domain-fact identity is required.",
                parameterName)
            : value;

    private static string RequireFingerprint(
        string value,
        string parameterName)
    {
        if (value == null || value.Length != 64)
            throw new ArgumentException(
                "A lowercase SHA-256 fingerprint is required.",
                parameterName);
        foreach (char current in value)
        {
            if (!(current is >= '0' and <= '9'
                    or >= 'a' and <= 'f'))
            {
                throw new ArgumentException(
                    "A lowercase SHA-256 fingerprint is required.",
                    parameterName);
            }
        }
        return value;
    }
}
