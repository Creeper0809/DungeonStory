using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CaptiveRansomAcceptedFactData
{
    public string captiveId = string.Empty;
    public string displayName = string.Empty;
    public string decisionMakerId = string.Empty;
    public CaptivityStatus previousStatus;
    public int amount;
    public float retaliationPressure;
    public long revision;
    public int absoluteDay;
    public CaptiveRansomAcceptedFactData Clone() => (CaptiveRansomAcceptedFactData)MemberwiseClone();
}

/// <summary>Frozen accepted ransom terms; payment and release remain separate owner effects.</summary>
public sealed class CaptiveRansomAcceptedFact
{
    private readonly CaptiveRansomAcceptedFactData value;
    public CaptiveRansomAcceptedFact(CaptiveRansomAcceptedFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.captiveId)
            || !DomainFactValueSyntax.IsText(source.displayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || source.displayName != source.displayName.Trim() || source.decisionMakerId == null
            || (source.decisionMakerId.Length > 0 && !DomainFactValueSyntax.IsIdentity(source.decisionMakerId))
            || !IsPreviousStatusValid(source.previousStatus) || source.amount <= 0
            || float.IsNaN(source.retaliationPressure) || float.IsInfinity(source.retaliationPressure)
            || source.retaliationPressure < 0f || source.retaliationPressure > 100f
            || source.revision <= 0L || source.absoluteDay < 0)
            throw new ArgumentException("captive-ransom-fact-invalid");
        value = source.Clone();
    }
    public CaptiveRansomAcceptedFactData Snapshot() => value.Clone();
    public static bool IsPreviousStatusValid(CaptivityStatus status) =>
        status is CaptivityStatus.AwaitingCapture or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort or CaptivityStatus.Confined or CaptivityStatus.Labor;
}

[Serializable]
public sealed class CaptiveRansomObservationData
{
    public CaptiveRansomAcceptedFactData fact = new();
    public string publicationKey = string.Empty;
    public bool pending;
    public bool HasFact => fact?.revision > 0L;
    public CaptiveRansomObservationData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, pending = pending };
}
