using System;
using DungeonStory.Foundation.Facts;

public enum InvasionLifecycleStage { Started = 1, Breached = 2, FacilityDamaged = 3, FrontCollapsed = 4, Resolved = 5 }
public enum InvasionLifecycleTargetKind { None, Facility, Defender, ScheduledOperation }
public enum InvasionResolutionKind { None, Suppressed, DefenseFailed, DefenderSurvived, DefenderFell, IntruderDefeated, Retreated }

[Serializable]
public sealed class InvasionLifecycleFactData
{
    public string runtimeId = string.Empty, intruderId = string.Empty, intruderName = string.Empty;
    public long sequence;
    public int absoluteDay, damageIndexBefore, damageIndexAfter, cellX, cellY;
    public InvasionLifecycleStage stage;
    public InvasionIntruderState stateBefore, stateAfter;
    public float riskBefore, riskAfter;
    public string parentPublicationKey = string.Empty;
    public InvasionLifecycleTargetKind targetKind;
    public string targetId = string.Empty, targetName = string.Empty, reason = string.Empty;
    public InvasionResolutionKind resolution;
    public bool defended, milestonePressure, hornExpected;
    public string sourceOwnerId = string.Empty;
    public DurableEquipmentUseFactData horn;
    public InvasionLifecycleFactData Clone()
    { var copy = (InvasionLifecycleFactData)MemberwiseClone(); copy.horn = horn?.Clone(); return copy; }
    public void Validate(bool proposal)
    {
        if (!DomainFactValueSyntax.IsIdentity(runtimeId) || !DomainFactValueSyntax.IsIdentity(intruderId)
            || !DomainFactValueSyntax.IsText(intruderName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || sequence < 1 || absoluteDay < 0 || damageIndexBefore < 0 || damageIndexAfter < damageIndexBefore
            || !Enum.IsDefined(typeof(InvasionLifecycleStage), stage) || !Enum.IsDefined(typeof(InvasionLifecycleTargetKind), targetKind)
            || !Enum.IsDefined(typeof(InvasionIntruderState), stateBefore) || !Enum.IsDefined(typeof(InvasionIntruderState), stateAfter)
            || !Enum.IsDefined(typeof(InvasionResolutionKind), resolution)
            || !float.IsFinite(riskBefore) || !float.IsFinite(riskAfter) || riskBefore < 0 || riskAfter < 0
            || !DomainFactValueSyntax.IsText(parentPublicationKey, 2048, stage == InvasionLifecycleStage.Started)
            || !DomainFactValueSyntax.IsText(reason, 1024, true))
            throw new ArgumentException("invasion-lifecycle-fact-invalid");
        bool start = stage == InvasionLifecycleStage.Started;
        if (start != (sequence == 1) || start != string.IsNullOrEmpty(parentPublicationKey)
            || !start && (milestonePressure || hornExpected || horn != null || !string.IsNullOrEmpty(sourceOwnerId))
            || !start && targetKind == InvasionLifecycleTargetKind.ScheduledOperation
            || start && (stateBefore != InvasionIntruderState.None || damageIndexBefore != 0 || damageIndexAfter != 0 || riskBefore != 0)
            || milestonePressure && (targetKind != InvasionLifecycleTargetKind.ScheduledOperation || !DomainFactValueSyntax.IsIdentity(sourceOwnerId))
            || !milestonePressure && (targetKind == InvasionLifecycleTargetKind.ScheduledOperation || sourceOwnerId != string.Empty)
            || start && targetKind is not InvasionLifecycleTargetKind.None and not InvasionLifecycleTargetKind.ScheduledOperation
            || (stage == InvasionLifecycleStage.Resolved) != (resolution != InvasionResolutionKind.None)
            || stage != InvasionLifecycleStage.Resolved && defended
            || stage == InvasionLifecycleStage.FacilityDamaged && (targetKind != InvasionLifecycleTargetKind.Facility || damageIndexAfter != checked(damageIndexBefore + 1))
            || stage != InvasionLifecycleStage.FacilityDamaged && damageIndexAfter != damageIndexBefore
            || stage == InvasionLifecycleStage.Breached && targetKind != InvasionLifecycleTargetKind.None
            || stage is InvasionLifecycleStage.FrontCollapsed or InvasionLifecycleStage.Resolved
                && targetKind is not InvasionLifecycleTargetKind.None and not InvasionLifecycleTargetKind.Defender
            || stage == InvasionLifecycleStage.Resolved
                && defended == (resolution is InvasionResolutionKind.DefenseFailed or InvasionResolutionKind.DefenderFell))
            throw new ArgumentException("invasion-lifecycle-stage-conflict");
        if (targetKind == InvasionLifecycleTargetKind.None ? targetId != string.Empty || targetName != string.Empty
            : !DomainFactValueSyntax.IsIdentity(targetId) || !DomainFactValueSyntax.IsText(targetName, DomainFactValueSyntax.MaximumNameCharacters, false))
            throw new ArgumentException("invasion-lifecycle-target-invalid");
        if (!proposal && hornExpected != (horn != null) || !hornExpected && horn != null)
            throw new ArgumentException("invasion-lifecycle-horn-fact-missing");
        horn?.Validate();
    }
    public bool SameProposal(InvasionLifecycleFactData other) => other != null && runtimeId == other.runtimeId
        && intruderId == other.intruderId && intruderName == other.intruderName && sequence == other.sequence
        && absoluteDay == other.absoluteDay && damageIndexBefore == other.damageIndexBefore && damageIndexAfter == other.damageIndexAfter
        && cellX == other.cellX && cellY == other.cellY && stage == other.stage && stateBefore == other.stateBefore
        && (stage == InvasionLifecycleStage.Started || stateAfter == other.stateAfter)
        && riskBefore == other.riskBefore && riskAfter == other.riskAfter && parentPublicationKey == other.parentPublicationKey
        && targetKind == other.targetKind && targetId == other.targetId && targetName == other.targetName && reason == other.reason
        && resolution == other.resolution && defended == other.defended && milestonePressure == other.milestonePressure
        && hornExpected == other.hornExpected && sourceOwnerId == other.sourceOwnerId;
}

public sealed class InvasionLifecycleFact
{
    private readonly InvasionLifecycleFactData data;
    public bool IsProposal { get; }
    public InvasionLifecycleFact(InvasionLifecycleFactData data, bool isProposal = false)
    { (data ?? throw new ArgumentNullException(nameof(data))).Validate(isProposal); this.data = data.Clone(); IsProposal = isProposal; }
    public InvasionLifecycleFactData Snapshot() => data.Clone();
    public bool MatchesProposal(InvasionLifecycleFact proposed) => proposed?.IsProposal == true && data.SameProposal(proposed.data);
}
