using System;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable result facts. Delivery/output acknowledgment markers are not part of the result.</summary>
public sealed class CaptivityInteractionResolvedFact
{
    private readonly CaptivityInteractionTerminalState value;
    public string CaptiveId { get; }
    public long OwnerRevision { get; }

    public CaptivityInteractionResolvedFact(string captiveId, CaptivityInteractionTerminalState source, long ownerRevision)
    {
        if (!DomainFactValueSyntax.IsIdentity(captiveId) || source == null || !source.HasOutcome
            || ownerRevision <= 0L || source.absoluteDay < 0
            || !DomainFactValueSyntax.IsIdentity(source.wardenId) || !DomainFactValueSyntax.IsIdentity(source.facilityId)
            || !DomainFactValueSyntax.IsIdentity(source.interactionId)
            || !Name(source.captiveDisplayName) || !Name(source.wardenDisplayName) || !Name(source.facilityDisplayName)
            || !Name(source.interactionDisplayName) || !Text(source.message, 4096)
            || !Enum.IsDefined(typeof(CaptiveInteractionKind), source.interactionKind)
            || !Percentage(source.willBefore)
            || !Percentage(source.willAfter)
            || !Percentage(source.fearBefore)
            || !Percentage(source.fearAfter)
            || !Percentage(source.trustBefore)
            || !Percentage(source.trustAfter)
            || !Percentage(source.grudgeBefore)
            || !Percentage(source.grudgeAfter)
            || !Percentage(source.corruptionBefore)
            || !Percentage(source.corruptionAfter)
            || !NonNegative(source.bodyDamageAmount) || !NonNegative(source.bodyHealthBefore)
            || !NonNegative(source.bodyHealthAfter) || !NonNegative(source.bodyMaximumHealth)
            || (source.HasOutput ? !DomainFactValueSyntax.IsIdentity(source.outputItemId)
                || source.outputOperationId != CaptivityInteractionAttemptIdentity.FormatOutputOperationId(captiveId, source.attemptId)
                : source.outputAmount != 0 || source.outputItemId != string.Empty || source.outputOperationId != string.Empty))
            throw new ArgumentException("captivity-interaction-fact-invalid");
        bool damageValid = source.bodyDamageAmount > 0f
            ? source.success && source.bodyHealthBefore > 0f
                && source.bodyMaximumHealth + 0.001f >= source.bodyHealthBefore
                && Math.Abs(source.bodyHealthAfter - Math.Max(0f, source.bodyHealthBefore - source.bodyDamageAmount)) <= 0.001f
            : source.bodyHealthBefore == 0f && source.bodyHealthAfter == 0f && source.bodyMaximumHealth == 0f;
        if (!damageValid || (!source.success && (source.HasOutput || source.bodyDamageAmount > 0f
            || Math.Abs(source.willAfter - source.willBefore) >= 0.0001f
            || Math.Abs(source.fearAfter - source.fearBefore) >= 0.0001f
            || Math.Abs(source.trustAfter - source.trustBefore) >= 0.0001f
            || Math.Abs(source.grudgeAfter - source.grudgeBefore) >= 0.0001f
            || Math.Abs(source.corruptionAfter - source.corruptionBefore) >= 0.0001f)))
            throw new ArgumentException("captivity-interaction-effects-invalid");
        CaptiveId = captiveId;
        OwnerRevision = ownerRevision;
        value = source.Clone();
        value.outcomeRevision = ownerRevision;
        value.publicationKey = string.Empty;
        value.deliveryPending = false;
        value.outputCommitId = string.Empty;
        value.outputPublished = false;
        value.bodyObserversPending = false;
    }
    public CaptivityInteractionTerminalState Snapshot() => value.Clone();
    public string ComputeSemanticDigest()
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append("captivity-interaction-result-v1");
        digest.Append(CaptiveId); digest.Append(OwnerRevision); digest.AppendEnum(value.interactionKind);
        digest.Append(value.attemptId);
        digest.Append(value.interactionId);
        digest.Append(value.captiveDisplayName);
        digest.Append(value.absoluteDay);
        digest.Append(value.interactionDisplayName);
        digest.Append(value.wardenId);
        digest.Append(value.wardenDisplayName);
        digest.Append(value.facilityId);
        digest.Append(value.facilityDisplayName);
        digest.Append(value.resultGridX);
        digest.Append(value.resultGridY);
        digest.Append(value.success);
        digest.Append(value.message);
        digest.Append(value.outputItemId);
        digest.Append(value.outputAmount);
        digest.Append(value.outputOperationId);
        digest.AppendFloat(value.willBefore);
        digest.AppendFloat(value.willAfter);
        digest.AppendFloat(value.fearBefore);
        digest.AppendFloat(value.fearAfter);
        digest.AppendFloat(value.trustBefore);
        digest.AppendFloat(value.trustAfter);
        digest.AppendFloat(value.grudgeBefore);
        digest.AppendFloat(value.grudgeAfter);
        digest.AppendFloat(value.corruptionBefore);
        digest.AppendFloat(value.corruptionAfter);
        digest.AppendFloat(value.bodyDamageAmount);
        digest.AppendFloat(value.bodyHealthBefore);
        digest.AppendFloat(value.bodyHealthAfter);
        digest.AppendFloat(value.bodyMaximumHealth);
        return digest.ComputeSha256();
    }
    private static bool Text(string value, int limit) => DomainFactValueSyntax.IsText(value, limit, false) && value == value.Trim();
    private static bool Name(string value) => Text(value, DomainFactValueSyntax.MaximumNameCharacters);
    private static bool NonNegative(float value) => float.IsFinite(value) && value >= 0f;
    private static bool Percentage(float value) => NonNegative(value) && value <= 100f;
}
