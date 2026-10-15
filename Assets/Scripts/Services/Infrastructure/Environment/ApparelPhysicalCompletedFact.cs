using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

/// <summary>The physical operation, not a narrative category or logging policy.</summary>
public enum ApparelPhysicalChangeKind { Craft = 0, RejectedDismantle = 1 }

public readonly struct ApparelPhysicalCompletedFact
{
    private readonly ApparelPhysicalCompletedFactData data;
    public ApparelPhysicalCompletedFact(ApparelPhysicalCompletedFactData data) =>
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
    public ApparelPhysicalCompletedFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("apparel-physical-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class ApparelPhysicalCompletedFactData
{
    public string operationId = string.Empty, orderId = string.Empty, facilityId = string.Empty,
        facilityName = string.Empty, outputItemId = string.Empty, outputStackId = string.Empty,
        outputInstanceId = string.Empty, makerId = string.Empty, makerName = string.Empty;
    public int kind, attemptIndex, absoluteDay, quality;
    public bool completed, hasProductQuality, rejectedBelowMinimum;
    public long inputMassGrams, outputMassGrams;

    public void Validate(bool requireCompleted = false)
    {
        if (!Enum.IsDefined(typeof(ApparelPhysicalChangeKind), kind) || attemptIndex < 0 || absoluteDay < 0
            || !DomainFactValueSyntax.IsIdentity(operationId) || !DomainFactValueSyntax.IsIdentity(orderId)
            || !DomainFactValueSyntax.IsIdentity(facilityId) || !DomainFactValueSyntax.IsIdentity(outputItemId)
            || !DomainFactValueSyntax.IsText(facilityName, 4096, false))
            throw new ArgumentException("apparel-physical-fact-identity-invalid");
        string expected = (kind == (int)ApparelPhysicalChangeKind.Craft
            ? "apparel-craft-output-batch:" : "apparel-rejected-dismantle:")
            + orderId + ":" + attemptIndex.ToString("D4", CultureInfo.InvariantCulture);
        if (operationId != expected || requireCompleted && !completed)
            throw new ArgumentException("apparel-physical-fact-operation-invalid");
        if (hasProductQuality
            ? kind != (int)ApparelPhysicalChangeKind.Craft || !DomainFactValueSyntax.IsIdentity(makerId)
                || !DomainFactValueSyntax.IsText(makerName, 4096, false)
                || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), quality)
            : makerId != string.Empty || makerName != string.Empty || quality != 0 || rejectedBelowMinimum)
            throw new ArgumentException("apparel-physical-fact-quality-invalid");
        if (!completed)
        {
            if (outputStackId != string.Empty || outputInstanceId != string.Empty
                || inputMassGrams != 0 || outputMassGrams != 0)
                throw new ArgumentException("apparel-physical-proposal-has-result");
            return;
        }
        if (inputMassGrams <= 0 || outputMassGrams < 0
            || (kind == (int)ApparelPhysicalChangeKind.Craft
                ? !DomainFactValueSyntax.IsIdentity(outputStackId)
                    || !DomainFactValueSyntax.IsIdentity(outputInstanceId) || outputMassGrams <= 0
                : outputInstanceId != string.Empty || outputMassGrams > inputMassGrams
                    || (outputMassGrams == 0 ? outputStackId != string.Empty
                        : !DomainFactValueSyntax.IsIdentity(outputStackId))))
            throw new ArgumentException("apparel-physical-fact-result-invalid");
    }
    public ApparelPhysicalCompletedFactData Clone()
    { Validate(); return (ApparelPhysicalCompletedFactData)MemberwiseClone(); }
}
