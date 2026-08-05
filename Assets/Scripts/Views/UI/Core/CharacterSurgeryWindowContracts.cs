using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurgeryPlanningSubject
{
    public SurgicalSubjectRef Subject { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<AnatomyNodeDefinition> Nodes { get; set; } =
        Array.Empty<AnatomyNodeDefinition>();
    public float Instability { get; set; }
    public float CorpseFreshnessSeconds { get; set; }
    public bool IsCorpse => Subject?.kind is SurgicalSubjectKind.HumanoidCorpse
        or SurgicalSubjectKind.WildlifeCorpse;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SurgeryUiCommandResult
{
    private SurgeryUiCommandResult(
        bool succeeded,
        string orderId,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        OrderId = orderId ?? string.Empty;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public string OrderId { get; }
    public DomainFailure Failure { get; }

    public static SurgeryUiCommandResult Success(string orderId) =>
        new SurgeryUiCommandResult(true, orderId, DomainFailure.None);

    public static SurgeryUiCommandResult Rejected(DomainFailure failure) =>
        new SurgeryUiCommandResult(false, string.Empty, failure);

    public static SurgeryUiCommandResult Rejected(
        FailureCode code,
        params string[] parameters) =>
        Rejected(new DomainFailure(code, parameters));
}

public readonly struct SurgeryWindowOption
{
    public SurgeryWindowOption(string id, string label)
    {
        Id = id ?? string.Empty;
        Label = label ?? string.Empty;
    }

    public string Id { get; }
    public string Label { get; }
}

public sealed class SurgeryWindowOptionsProjection
{
    public IReadOnlyList<SurgeryWindowOption> Procedures { get; set; } =
        Array.Empty<SurgeryWindowOption>();
    public IReadOnlyList<SurgeryWindowOption> Nodes { get; set; } =
        Array.Empty<SurgeryWindowOption>();
    public IReadOnlyList<SurgeryWindowOption> Parts { get; set; } =
        Array.Empty<SurgeryWindowOption>();
    public IReadOnlyList<SurgeryWindowOption> Doctors { get; set; } =
        Array.Empty<SurgeryWindowOption>();
    public IReadOnlyList<SurgeryWindowOption> Facilities { get; set; } =
        Array.Empty<SurgeryWindowOption>();
}

public readonly struct SurgeryWindowSelection
{
    public SurgeryWindowSelection(
        string procedureId,
        string nodeId,
        string partId,
        string doctorId,
        string facilityId)
    {
        ProcedureId = procedureId ?? string.Empty;
        NodeId = nodeId ?? string.Empty;
        PartId = partId ?? string.Empty;
        DoctorId = doctorId ?? string.Empty;
        FacilityId = facilityId ?? string.Empty;
    }

    public string ProcedureId { get; }
    public string NodeId { get; }
    public string PartId { get; }
    public string DoctorId { get; }
    public string FacilityId { get; }
}

public sealed class SurgeryWindowDetailsProjection
{
    public string ProcedureLabel { get; set; } = string.Empty;
    public string NodeLabel { get; set; } = string.Empty;
    public string PartLabel { get; set; } = string.Empty;
    public string DoctorLabel { get; set; } = string.Empty;
    public string FacilityLabel { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
}

public interface ICharacterSurgeryWindowQuery
{
    SurgeryWindowOptionsProjection GetOptions(
        SurgeryPlanningSubject subject,
        string procedureId);
    SurgeryWindowDetailsProjection GetDetails(
        SurgeryPlanningSubject subject,
        SurgeryWindowSelection selection);
}

public interface ICharacterSurgeryWindowCommand
{
    SurgeryUiCommandResult Schedule(
        SurgeryPlanningSubject subject,
        SurgeryWindowSelection selection);
    SurgeryUiCommandResult Cancel(SurgeryPlanningSubject subject);
}

public interface ICharacterSurgeryWindowView
{
    GameObject Root { get; }
    void Configure(
        ICharacterSurgeryWindowQuery query,
        ICharacterSurgeryWindowCommand commands,
        SurgeryPlanningSubject subject,
        ITmpKoreanFontService fonts,
        Action onClosed);
}

public interface ICharacterSurgeryWindowViewFactory
{
    ICharacterSurgeryWindowView Create(Transform parent);
}
