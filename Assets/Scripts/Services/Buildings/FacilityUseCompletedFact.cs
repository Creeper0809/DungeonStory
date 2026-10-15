using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

// Detached facility-use results contain no actor, definition, logger or native identity.
public sealed class FacilityUseCompletedFact
{
    private readonly FacilityUseCommitState data;
    public FacilityUseCompletedFact(string facilityId, string facilityDisplayName, FacilityUseCommitState source)
    {
        if (!DomainFactValueSyntax.IsIdentity(facilityId)
            || !DomainFactValueSyntax.IsText(facilityDisplayName, 4096, false)
            || source == null || source.absoluteDay < 0 || source.operationSequence is <= 0 or long.MaxValue
            || source.operationId != OperationIdentity(facilityId, source.operationSequence))
            throw new ArgumentException("facility-use-fact-identity-invalid");
        var state = new FacilityRuntimeState
        { nextUseOperationSequence = source.operationSequence + 1, pendingUses = new List<FacilityUseCommitState> { source.Clone() } };
        if (!state.IsValid(out string failure)) throw new ArgumentException("facility-use-fact-invalid:" + failure);
        FacilityId = facilityId;
        FacilityDisplayName = facilityDisplayName;
        data = source.Clone();
        data.phase = (int)FacilityUseCommitPhase.EffectsCommitted;
        data.publicationKey = string.Empty;
        data.facilityDisplayName = facilityDisplayName;
    }
    public string FacilityId { get; }
    public string FacilityDisplayName { get; }
    public string OperationId => data.operationId;
    public int AbsoluteDay => data.absoluteDay;
    public FacilityUseCommitState Snapshot() => data.Clone();
    public static string OperationIdentity(string facilityId, long sequence) =>
        "facility-visit:facility=" + facilityId + ":use=" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
