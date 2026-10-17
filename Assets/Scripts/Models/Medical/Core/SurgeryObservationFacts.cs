using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum SurgeryAnatomyAvailability
{
    Empty, CharacterUnavailable, WildlifeUnavailable, NoLivingAnatomy, NodeUnavailable, Available
}

// Observed medical state, not a formatted log. Missing subjects/nodes are explicit observations.
[Serializable]
public sealed class SurgeryAnatomyFactData
{
    public SurgeryAnatomyAvailability availability;
    public string profileId = string.Empty;
    public string nodeId = string.Empty;
    public bool alive;
    public float currentHealth;
    public float maxHealth;
    public float bleeding;
    public float infection;
    public bool missing;
    public string installedPartId = string.Empty;
    public SurgicalPartKind installedPartKind;
    public float rejection;
    public float mutation;
    public bool hasGolemState;
    public float charge;
    public float integrity;

    public SurgeryAnatomyFactData Clone() => (SurgeryAnatomyFactData)MemberwiseClone();
    public bool ContentEquals(SurgeryAnatomyFactData other) => other != null
        && availability == other.availability && profileId == other.profileId && nodeId == other.nodeId
        && alive == other.alive && currentHealth == other.currentHealth && maxHealth == other.maxHealth
        && bleeding == other.bleeding && infection == other.infection && missing == other.missing
        && installedPartId == other.installedPartId && installedPartKind == other.installedPartKind
        && rejection == other.rejection && mutation == other.mutation && hasGolemState == other.hasGolemState
        && charge == other.charge && integrity == other.integrity;
    public bool IsEmpty => availability == SurgeryAnatomyAvailability.Empty && HasNoNodeData
        && profileId == string.Empty && nodeId == string.Empty && !alive;
    private bool HasNoNodeData => currentHealth == 0 && maxHealth == 0 && bleeding == 0
        && infection == 0 && !missing && installedPartId == string.Empty && installedPartKind == 0
        && rejection == 0 && mutation == 0 && !hasGolemState && charge == 0 && integrity == 0;

    public void Validate(bool allowEmpty = false)
    {
        if (allowEmpty && IsEmpty) return;
        if (!Enum.IsDefined(typeof(SurgeryAnatomyAvailability), availability)
            || availability == SurgeryAnatomyAvailability.Empty
            || !SurgeryResolvedFactData.Text(profileId, true)
            || !SurgeryResolvedFactData.Text(nodeId, true)
            || !SurgeryResolvedFactData.Text(installedPartId, true)
            || !Enum.IsDefined(typeof(SurgicalPartKind), installedPartKind))
            throw new ArgumentException("surgery-anatomy-fact-invalid");
        if (availability != SurgeryAnatomyAvailability.Available)
        {
            if (!HasNoNodeData || alive
                || availability != SurgeryAnatomyAvailability.NodeUnavailable
                    && (profileId != string.Empty || nodeId != string.Empty))
                throw new ArgumentException("surgery-anatomy-unavailable-has-node-state");
            return;
        }
        if (!DomainFactValueSyntax.IsIdentity(profileId) || !DomainFactValueSyntax.IsIdentity(nodeId)
            || !FiniteNonnegative(currentHealth) || !FiniteNonnegative(maxHealth)
            || currentHealth > maxHealth || !FiniteNonnegative(bleeding) || !FiniteNonnegative(infection)
            || !FiniteNonnegative(rejection) || !FiniteNonnegative(mutation)
            || !FiniteNonnegative(charge) || !FiniteNonnegative(integrity)
            || !hasGolemState && (charge != 0 || integrity != 0))
            throw new ArgumentException("surgery-anatomy-node-state-invalid");
    }

    private static bool FiniteNonnegative(float value) => float.IsFinite(value) && value >= 0;
}

[Serializable]
public sealed class SurgeryResolvedFactData
{
    public string orderId = string.Empty;
    public string subjectId = string.Empty;
    public string subjectName = string.Empty;
    public SurgicalSubjectKind subjectKind;
    public string procedureId = string.Empty;
    public SurgicalProcedureKind procedureKind;
    public string doctorId = string.Empty;
    public string facilityId = string.Empty;
    public string targetNodeId = string.Empty;
    public CharacterCommandOrigin origin;
    public int absoluteDay;
    public float completedWork;
    public string resultId = string.Empty;
    public SurgeryFailureSeverity severity;
    public bool succeeded;
    public bool materialsConsumed;
    public bool processFluidConsumed;
    public bool anesthesiaConsumed;
    public long materialSinkInputMassGrams;
    public List<SurgicalMaterialRequirement> materials = new();
    public string materialSinkOperationId = string.Empty;
    public string materialSinkCommitId = string.Empty;
    public string materialTerminalParentOperationId = string.Empty;
    public string materialTerminalCommitId = string.Empty;
    public string replacementOperationId = string.Empty;
    public string replacementBatchCommitId = string.Empty;
    public string replacementOutputStackId = string.Empty;
    public string replacementOutputItemInstanceId = string.Empty;
    public SurgeryAnatomyFactData before = new();
    public SurgeryAnatomyFactData after = new();

    public bool IsEmpty => orderId == string.Empty && subjectId == string.Empty && subjectName == string.Empty
        && subjectKind == 0 && procedureId == string.Empty && procedureKind == 0 && doctorId == string.Empty
        && facilityId == string.Empty && targetNodeId == string.Empty && origin == 0 && absoluteDay == 0
        && completedWork == 0 && resultId == string.Empty && severity == 0 && !succeeded
        && !materialsConsumed && !processFluidConsumed && !anesthesiaConsumed && materialSinkInputMassGrams == 0
        && materials?.Count == 0 && materialSinkOperationId == string.Empty && materialSinkCommitId == string.Empty
        && materialTerminalParentOperationId == string.Empty && materialTerminalCommitId == string.Empty
        && replacementOperationId == string.Empty && replacementBatchCommitId == string.Empty
        && replacementOutputStackId == string.Empty && replacementOutputItemInstanceId == string.Empty
        && before?.IsEmpty == true && after?.IsEmpty == true;

    public SurgeryResolvedFactData Clone()
    {
        var copy = (SurgeryResolvedFactData)MemberwiseClone();
        copy.before = before?.Clone();
        copy.after = after?.Clone();
        copy.materials = CopyMaterials(materials);
        return copy;
    }

    public static List<SurgicalMaterialRequirement> CopyMaterials(IEnumerable<SurgicalMaterialRequirement> source) =>
        source?.Select(value => value == null ? null : new SurgicalMaterialRequirement
        { itemId = value.itemId, quantity = value.quantity, optional = value.optional }).ToList();

    public void Validate(bool proposal = false)
    {
        if (!DomainFactValueSyntax.IsIdentity(orderId) || !DomainFactValueSyntax.IsIdentity(subjectId)
            || !Text(subjectName, false)
            || subjectName.Length > DomainFactValueSyntax.MaximumNameCharacters
            || !DomainFactValueSyntax.IsIdentity(procedureId)
            || !DomainFactValueSyntax.IsIdentity(doctorId) || !DomainFactValueSyntax.IsIdentity(facilityId)
            || !Text(targetNodeId, true) || !Enum.IsDefined(typeof(SurgicalSubjectKind), subjectKind)
            || !Enum.IsDefined(typeof(SurgicalProcedureKind), procedureKind)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin)
            || !Enum.IsDefined(typeof(SurgeryFailureSeverity), severity) || absoluteDay <= 0
            || !float.IsFinite(completedWork) || completedWork <= 0 || materialSinkInputMassGrams < 0
            || materials == null || materials.Count > 128 || materials.Any(value => value == null
                || !DomainFactValueSyntax.IsIdentity(value.itemId) || value.quantity <= 0)
            || !Text(materialSinkOperationId, true) || !Text(materialSinkCommitId, true)
            || !Text(materialTerminalParentOperationId, true) || !Text(materialTerminalCommitId, true)
            || !Text(replacementOperationId, true) || !Text(replacementBatchCommitId, true)
            || !Text(replacementOutputStackId, true) || !Text(replacementOutputItemInstanceId, true)
            || before == null || after == null)
            throw new ArgumentException("surgery-resolved-fact-invalid");
        before.Validate(proposal);
        after.Validate(proposal);
        if (!proposal && (!Text(resultId, false)
            || succeeded && (severity != SurgeryFailureSeverity.None || resultId is not ("success" or "miracle"))
            || !succeeded && (severity == SurgeryFailureSeverity.None || resultId is not ("failure" or "severe-complication"))))
            throw new ArgumentException("surgery-resolved-fact-result-invalid");
    }

    internal static bool Text(string value, bool allowEmpty) =>
        DomainFactValueSyntax.IsText(value, 4096, allowEmpty) && value == value.Trim();
}

public readonly struct SurgeryResolvedFact
{
    private readonly SurgeryResolvedFactData data;
    public SurgeryResolvedFact(SurgeryResolvedFactData source, bool proposal = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(proposal);
        data = source.Clone();
    }
    public SurgeryResolvedFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("surgery-resolved-fact-uninitialized");
}

// Accepted labor attached to the already frozen clinical result. No ledger identity or notification state.
public readonly struct SurgeryWorkCompletedFact
{
    public SurgeryWorkCompletedFact(SurgeryResolvedFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        OrderId = source.orderId;
        ProcedureId = source.procedureId;
        DoctorId = source.doctorId;
        FacilityId = source.facilityId;
        ResultId = source.resultId;
        Succeeded = source.succeeded;
        Origin = source.origin;
        AbsoluteDay = source.absoluteDay;
        CompletedWork = source.completedWork;
    }
    public string OrderId { get; }
    public string ProcedureId { get; }
    public string DoctorId { get; }
    public string FacilityId { get; }
    public string ResultId { get; }
    public bool Succeeded { get; }
    public CharacterCommandOrigin Origin { get; }
    public int AbsoluteDay { get; }
    public float CompletedWork { get; }
}
