using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class WastePolicyChangedFactData
{
    public string operationId = string.Empty;
    public long ownerRevision;
    public int absoluteDay;
    public WasteOriginKind origin;
    public WasteDispositionKind beforeDisposition, afterDisposition;
    public bool beforeEnabled, afterEnabled;
    public float beforeMaximumFeedContamination, afterMaximumFeedContamination;
    public WastePolicyChangedFactData Clone() => (WastePolicyChangedFactData)MemberwiseClone();
}

// Only domain policy values and their original command identity/time cross this boundary.
public sealed class WastePolicyChangedFact
{
    private readonly WastePolicyChangedFactData data;
    public WastePolicyChangedFact(WastePolicyChangedFactData value)
    {
        data = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
        if (data.ownerRevision <= 0 || data.ownerRevision == long.MaxValue || data.absoluteDay < 1
            || data.origin is < WasteOriginKind.Plant or > WasteOriginKind.Forbidden
            || data.operationId != OperationIdFor(data.origin, data.ownerRevision)
            || !Enum.IsDefined(typeof(WasteDispositionKind), data.beforeDisposition)
            || !Enum.IsDefined(typeof(WasteDispositionKind), data.afterDisposition)
            || !float.IsFinite(data.beforeMaximumFeedContamination) || !float.IsFinite(data.afterMaximumFeedContamination)
            || data.beforeMaximumFeedContamination is < 0 or > 100 || data.afterMaximumFeedContamination is < 0 or > 100)
            throw new InvalidOperationException("waste-policy-fact-invalid");
    }
    public string OperationId => data.operationId;
    public long OwnerRevision => data.ownerRevision;
    public int AbsoluteDay => data.absoluteDay;
    public WasteOriginKind Origin => data.origin;
    public WasteDispositionKind BeforeDisposition => data.beforeDisposition;
    public WasteDispositionKind AfterDisposition => data.afterDisposition;
    public bool BeforeEnabled => data.beforeEnabled;
    public bool AfterEnabled => data.afterEnabled;
    public float BeforeMaximumFeedContamination => data.beforeMaximumFeedContamination;
    public float AfterMaximumFeedContamination => data.afterMaximumFeedContamination;
    public WastePolicyChangedFactData Snapshot() => data.Clone();
    public WastePolicyData BeforePolicy() => new()
    { origin = Origin, disposition = BeforeDisposition, enabled = BeforeEnabled, maximumFeedContamination = BeforeMaximumFeedContamination };
    public WastePolicyData AfterPolicy() => new()
    { origin = Origin, disposition = AfterDisposition, enabled = AfterEnabled, maximumFeedContamination = AfterMaximumFeedContamination };
    public static string OperationIdFor(WasteOriginKind origin, long revision) => "waste-policy:"
        + ((int)origin).ToString(CultureInfo.InvariantCulture) + ":command:" + revision.ToString(CultureInfo.InvariantCulture);
    public static bool SamePolicy(WastePolicyData left, WastePolicyData right) => left != null && right != null
        && left.origin == right.origin && left.disposition == right.disposition && left.enabled == right.enabled
        && left.maximumFeedContamination.Equals(right.maximumFeedContamination);
}

[Serializable]
public sealed class WastePolicyObservationSaveData
{
    public WastePolicyChangedFactData fact = new();
    public string publicationKey = string.Empty, publicationId = string.Empty, publicationFingerprint = string.Empty;
    public WastePolicyObservationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId, publicationFingerprint = publicationFingerprint };
    public void Validate(long nextPolicySequence)
    {
        var frozen = new WastePolicyChangedFact(fact);
        if (frozen.OwnerRevision != nextPolicySequence - 1
            || !DomainFactValueSyntax.IsText(publicationKey, 4096, false) || publicationKey != publicationKey.Trim()
            || publicationId == null || publicationFingerprint == null
            || (publicationId.Length == 0) != (publicationFingerprint.Length == 0)
            || publicationId.Length > 0 && (!DomainFactValueSyntax.IsText(publicationId, 4096, false)
                || publicationId != publicationId.Trim() || !IsFingerprint(publicationFingerprint)))
            throw new InvalidOperationException("waste-policy-observation-invalid");
    }
    public static bool IsFingerprint(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
