using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Immutable state transition, including exact unlocks and physical equipment wear.</summary>
public sealed class ResearchWorkAppliedFact
{
    private readonly ResearchWorkPublicationData snapshot;

    public ResearchWorkAppliedFact(ResearchWorkPublicationData data)
    {
        snapshot = data?.Clone() ?? throw new ArgumentNullException(nameof(data));
        if (snapshot.revision <= 0 || snapshot.absoluteDay < 0
            || string.IsNullOrWhiteSpace(snapshot.subjectId) || snapshot.subjectId != snapshot.subjectId.Trim()
            || string.IsNullOrWhiteSpace(snapshot.subjectName)
            || snapshot.researcherId == null || snapshot.researcherName == null
            || snapshot.facilityId == null || snapshot.facilityName == null
            || !float.IsFinite(snapshot.before) || !float.IsFinite(snapshot.after)
            || !float.IsFinite(snapshot.required) || snapshot.required < 1
            || !float.IsFinite(snapshot.approvedWork) || snapshot.approvedWork < 0
            || snapshot.before < 0 || snapshot.before > snapshot.required
            || snapshot.after < 0 || snapshot.after > snapshot.required
            || !Enum.IsDefined(typeof(ResearchWorkCause), snapshot.cause)
            || snapshot.completed != (snapshot.after >= snapshot.required && snapshot.cause != ResearchWorkCause.KnowledgeProgress)
            || snapshot.unlocks == null || snapshot.unlocks.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.typeId) || string.IsNullOrWhiteSpace(value.valueId))
            || !snapshot.completed && snapshot.unlocks.Length != 0
            || snapshot.cause is ResearchWorkCause.Work or ResearchWorkCause.KnowledgeProgress && snapshot.after <= snapshot.before
            || snapshot.cause == ResearchWorkCause.ImmediateCompletion && snapshot.after != snapshot.required
            || snapshot.cause == ResearchWorkCause.KnowledgeProgress && (snapshot.isProject || snapshot.blueprintId != -1)
            || snapshot.cause != ResearchWorkCause.KnowledgeProgress && !snapshot.isProject && snapshot.blueprintId < 0
            || snapshot.cause == ResearchWorkCause.ForbiddenLeap && (!snapshot.isProject
                || !Enum.IsDefined(typeof(ExtremeRiskOutcome), snapshot.leapOutcome)
                || !float.IsFinite(snapshot.leapDelta) || !float.IsFinite(snapshot.aftermathUntil)))
            throw new ArgumentException("research-work-fact-invalid", nameof(data));
        Unlocks = Array.AsReadOnly(snapshot.unlocks.Select(value => new BlueprintUnlockRecord(
            value.typeId, value.categoryLabel, value.valueId, value.displayName, codexDetail: value.codexDetail)).ToArray());
        Equipment = snapshot.hasEquipment ? new ResearchEquipmentUseSnapshot(snapshot.equipment) : null;
        if (Equipment != null && Equipment.FacilityId != snapshot.facilityId)
            throw new ArgumentException("research-work-equipment-facility-mismatch", nameof(data));
    }

    public ResearchWorkPublicationData Capture() => snapshot.Clone();
    public long Revision => snapshot.revision;
    public int AbsoluteDay => snapshot.absoluteDay;
    public bool IsProject => snapshot.isProject;
    public int BlueprintId => snapshot.blueprintId;
    public string SubjectId => snapshot.subjectId;
    public string SubjectName => snapshot.subjectName;
    public string ResearcherId => snapshot.researcherId;
    public string ResearcherName => snapshot.researcherName;
    public string FacilityId => snapshot.facilityId;
    public string FacilityName => snapshot.facilityName;
    public float Before => snapshot.before;
    public float After => snapshot.after;
    public float Required => snapshot.required;
    public float ApprovedWork => snapshot.approvedWork;
    public bool Completed => snapshot.completed;
    public ResearchWorkCause Cause => snapshot.cause;
    public ExtremeRiskResolution Leap => new((ExtremeRiskOutcome)snapshot.leapOutcome,
        snapshot.leapPrimary, snapshot.leapSecondary, snapshot.leapDelta, snapshot.leapHash);
    public float AftermathUntil => snapshot.aftermathUntil;
    public IReadOnlyList<BlueprintUnlockRecord> Unlocks { get; }
    public ResearchEquipmentUseSnapshot Equipment { get; }
}
