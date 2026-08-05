using System.Collections.Generic;

public interface ISurgicalProcedureCatalog
{
    IReadOnlyList<SurgicalProcedureSO> Procedures { get; }
    bool TryGet(string procedureId, out SurgicalProcedureSO procedure);
    IReadOnlyList<string> Validate();
}

public interface ISurgicalCorpseFreshnessRuntime
{
    bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds,
        out bool isFresh);
    IReadOnlyList<SurgicalCorpseFreshnessState> Capture();
}

public interface ISurgeryPolicyRuntime
{
    bool IsAutomaticEmergencySurgeryEnabled(SurgicalSubjectRef subject);
    void SetAutomaticEmergencySurgery(
        SurgicalSubjectRef subject,
        bool enabled);
}

public interface ISurgeryExtractionLedger
{
    bool IsExtracted(string corpseStackId, string nodeId);
    bool TryMarkExtracted(
        string corpseStackId,
        string nodeId,
        out DomainFailure failure);
    IReadOnlyList<CorpseSurgicalRecord> Capture();
}

public interface ISurgeryCommandService
{
    bool TrySchedule(
        SurgicalSubjectRef subject,
        string procedureId,
        string targetNodeId,
        string selectedPartInstanceId,
        string preferredDoctorId,
        string preferredFacilityId,
        out SurgeryOrder order,
        out DomainFailure failure);
    bool TryCancel(string orderId, out DomainFailure failure);
}
