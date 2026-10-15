using System;
using DungeonStory.Foundation.Facts;

public enum CaptiveEscapeKind
{
    PhysicalEscape = 1,
    ArrivalCustodyLoss = 2,
    FalseComplianceBetrayal = 3,
    MinionControlBreak = 4
}

[Serializable]
public sealed class CaptiveEscapedFactData
{
    public string captiveId = string.Empty;
    public string displayName = string.Empty;
    public CaptiveEscapeKind kind;
    public string trigger = string.Empty;
    public CaptivityStatus previousStatus;
    public float retaliationPressure;
    public long revision;
    public int absoluteDay;
    public CaptiveEscapedFactData Clone() => (CaptiveEscapedFactData)MemberwiseClone();
}

/// <summary>A frozen custody transition, independent of log identity and experience judgment.</summary>
public sealed class CaptiveEscapedFact
{
    private readonly CaptiveEscapedFactData value;
    public CaptiveEscapedFact(CaptiveEscapedFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.captiveId)
            || !DomainFactValueSyntax.IsText(source.displayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || source.displayName != source.displayName.Trim()
            || !DomainFactValueSyntax.IsText(source.trigger, 512, false) || source.trigger != source.trigger.Trim()
            || !IsPreviousStatusValid(source.kind, source.previousStatus)
            || float.IsNaN(source.retaliationPressure) || float.IsInfinity(source.retaliationPressure)
            || source.retaliationPressure < 0f || source.retaliationPressure > 100f
            || source.revision <= 0L || source.absoluteDay < 0)
            throw new ArgumentException("captive-escape-fact-invalid");
        value = source.Clone();
    }
    public CaptiveEscapedFactData Snapshot() => value.Clone();
    public static bool IsBetrayal(CaptiveEscapeKind kind) =>
        kind is CaptiveEscapeKind.FalseComplianceBetrayal or CaptiveEscapeKind.MinionControlBreak;
    public static bool IsPreviousStatusValid(CaptiveEscapeKind kind, CaptivityStatus status) => kind switch
    {
        CaptiveEscapeKind.PhysicalEscape => status == CaptivityStatus.EscapeAttempt,
        CaptiveEscapeKind.ArrivalCustodyLoss => status == CaptivityStatus.AwaitingCapture,
        CaptiveEscapeKind.FalseComplianceBetrayal => status is CaptivityStatus.Confined or CaptivityStatus.Labor or CaptivityStatus.Performer,
        CaptiveEscapeKind.MinionControlBreak => status == CaptivityStatus.Minion,
        _ => false
    };
}

[Serializable]
public sealed class CaptiveEscapeObservationData
{
    public CaptiveEscapedFactData fact = new();
    public string publicationKey = string.Empty;
    public bool pending;
    public bool observersPending;
    public bool HasFact => fact?.revision > 0L;
    public CaptiveEscapeObservationData Clone() => new()
    {
        fact = fact?.Clone(), publicationKey = publicationKey,
        pending = pending, observersPending = observersPending
    };
}
