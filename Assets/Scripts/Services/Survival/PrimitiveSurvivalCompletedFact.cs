using System;
using DungeonStory.Foundation.Facts;

/// <summary>Frozen business result of an improvised rest, latrine or wash.</summary>
public sealed class PrimitiveSurvivalCompletedFact
{
    private readonly PrimitiveSurvivalPendingSaveData data;
    public PrimitiveSurvivalCompletedFact(PrimitiveSurvivalPendingSaveData source, bool reservationOnly = false)
    {
        Validate(source, reservationOnly);
        data = source.Clone();
        ReservationOnly = reservationOnly;
    }
    public bool ReservationOnly { get; }
    public PrimitiveSurvivalPendingSaveData Snapshot() => data.Clone();

    public static void ValidateSaved(PrimitiveSurvivalPendingSaveData source, string characterId)
    {
        if (source == null || source.version != PrimitiveSurvivalPendingSaveData.CurrentVersion)
            throw new InvalidOperationException("primitive-survival-save-version-invalid");
        _ = source.GetRestObservation();
        if (!source.IsPending)
        {
            if (source.actionId != string.Empty || source.operationId != string.Empty
                || source.characterId != string.Empty || source.characterName != string.Empty
                || source.publicationKey != string.Empty || source.waterStackId != string.Empty
                || source.restPublicationKey != string.Empty || source.GetRestObservation() != null
                || source.rollbackBlocked || source.rollbackFailure != string.Empty
                || source.absoluteDay != 0 || source.recovery != 0f || source.physicalItemCount != 0
                || source.sleepAfter != 0f || source.hygieneAfter != 0f || source.excretionAfter != 0f
                || source.targetGridX != 0 || source.targetGridY != 0 || source.wasteFilthId != string.Empty
                || source.stainFilthId != string.Empty || source.wasteAmountAfter != 0f || source.stainAmountAfter != 0f)
                throw new InvalidOperationException("primitive-survival-empty-save-has-data");
            return;
        }
        Validate(source, false);
        if (source.characterId != characterId || !DomainFactValueSyntax.IsText(source.publicationKey, 4096, false))
            throw new InvalidOperationException("primitive-survival-saved-owner-key-invalid");
    }

    private static void Validate(PrimitiveSurvivalPendingSaveData source, bool proposed)
    {
        if (source == null || source.version != PrimitiveSurvivalPendingSaveData.CurrentVersion
            || !DomainFactValueSyntax.IsIdentity(source.operationId)
            || !DomainFactValueSyntax.IsIdentity(source.characterId)
            || !DomainFactValueSyntax.IsText(source.characterName, 16384, true)
            || !DomainFactValueSyntax.IsText(source.publicationKey, 4096, true)
            || !DomainFactValueSyntax.IsText(source.rollbackFailure, 16384, true)
            || source.rollbackBlocked != (source.rollbackFailure.Length > 0)
            || source.absoluteDay <= 0 || !Range(source.sleepAfter) || !Range(source.hygieneAfter)
            || !Range(source.excretionAfter))
            throw new InvalidOperationException("primitive-survival-fact-invalid");
        bool floor = source.actionId == "survival:floor-rest";
        bool latrine = source.actionId == "survival:primitive-latrine";
        bool wash = source.actionId == "survival:bucket-wash";
        var rest = source.GetRestObservation();
        if (floor && !proposed)
        {
            if (!DomainFactValueSyntax.IsText(source.restPublicationKey, 4096, false))
                throw new InvalidOperationException("primitive-survival-rest-proof-missing");
            rest?.Validate(source.characterId);
            if (rest != null
                && (rest.publicationKey != source.restPublicationKey
                    || rest.fact.context?.eventInstanceId != source.operationId
                    || rest.fact.after != source.sleepAfter))
                throw new InvalidOperationException("primitive-survival-rest-child-conflict");
        }
        else if (source.restPublicationKey != string.Empty || rest != null)
            throw new InvalidOperationException("primitive-survival-unexpected-rest-proof");
        float expectedRecovery = floor ? PrimitiveSurvivalBalanceAuthority.FloorRestRecovery
            : latrine ? PrimitiveSurvivalBalanceAuthority.LatrineRecovery : PrimitiveSurvivalBalanceAuthority.BucketWashRecovery;
        if (!(floor || latrine || wash) || source.recovery != expectedRecovery
            || source.physicalItemCount != (wash ? 1 : 0)
            || (wash ? !DomainFactValueSyntax.IsIdentity(source.waterStackId) : source.waterStackId != string.Empty))
            throw new InvalidOperationException("primitive-survival-fact-action-invalid");
        if (latrine && !proposed)
        {
            if (!DomainFactValueSyntax.IsIdentity(source.wasteFilthId)
                || !DomainFactValueSyntax.IsIdentity(source.stainFilthId) || source.wasteFilthId == source.stainFilthId
                || !Range(source.wasteAmountAfter) || source.wasteAmountAfter <= 0f
                || !Range(source.stainAmountAfter) || source.stainAmountAfter <= 0f)
                throw new InvalidOperationException("primitive-survival-filth-fact-invalid");
        }
        else if (source.targetGridX != 0 || source.targetGridY != 0 || source.wasteFilthId != string.Empty
            || source.stainFilthId != string.Empty || source.wasteAmountAfter != 0f || source.stainAmountAfter != 0f)
            throw new InvalidOperationException("primitive-survival-unexpected-filth-fact");
    }
    private static bool Range(float value) => float.IsFinite(value) && value >= 0f && value <= 100f;
}
