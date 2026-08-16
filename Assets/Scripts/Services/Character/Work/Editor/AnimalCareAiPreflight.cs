#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

internal sealed class AnimalCareAiPreflightSnapshot
{
    public bool AuthoredWorkType;
    public bool HusbandryAvailable;
    public AnimalHusbandryWorkSnapshot HusbandryWork;
    public bool SearchAvailable;
    public bool WorkAccessAvailable;
    public Vector2Int WorkAccessPosition;
    public int WorkAccessCost = int.MaxValue;
    public bool PolicyAvailable;
    public string PolicyReason = string.Empty;
    public bool CandidateAvailable;
    public bool CandidateTargetsPen;
    public WorkTargetCandidate Candidate;
    public WorkTargetCandidate RejectedCandidate;
    public bool AiWorkCatalogAvailable;
    public string AiWorkCatalog = string.Empty;
    public bool CandidateIndexPending;
    public bool CandidateIndexContainsPen;
    public int CandidateIndexRevision;
    public int CandidateDynamicRevision;
    public bool EmergencyGateActive;
    public long EmergencyGateEpoch;
    public WorkTypeId EmergencyGateWorkType;
    public bool EmergencyGateAllowsAnimalCare;
    public bool Passed => AuthoredWorkType
        && HusbandryAvailable
        && SearchAvailable
        && WorkAccessAvailable
        && PolicyAvailable
        && EmergencyGateAllowsAnimalCare
        && CandidateAvailable
        && CandidateTargetsPen
        && AiWorkCatalogAvailable;

    public string EarliestFailure
    {
        get
        {
            if (!AuthoredWorkType) return "authored-supported-work-type";
            if (!HusbandryAvailable) return "husbandry-work-query";
            if (!SearchAvailable) return "actor-path-search";
            if (!WorkAccessAvailable) return "work-access-path";
            if (!PolicyAvailable) return "work-policy";
            if (EmergencyGateActive && !EmergencyGateAllowsAnimalCare)
                return "emergency-response-gate";
            if (!CandidateAvailable || !CandidateTargetsPen)
                return "work-target-selector";
            if (!AiWorkCatalogAvailable) return "actor-aiwork-catalog";
            return string.Empty;
        }
    }

    public string Format()
    {
        string husbandry = HusbandryAvailable
            ? $"available={HusbandryWork.Available},kind={HusbandryWork.Kind},"
              + $"animal={HusbandryWork.AnimalId.Value},"
              + $"work={HusbandryWork.CompletedWork:0.###}/{HusbandryWork.RequiredWork:0.###}"
            : $"available={HusbandryWork.Available},failure={HusbandryWork.Failure.Code}";
        string candidate = CandidateAvailable
            ? $"valid={Candidate.IsValid},type={Candidate.WorkTypeId.Value},"
              + $"targetExact={CandidateTargetsPen},score={Candidate.Score:0.###},"
              + $"urgency={Candidate.UrgencyScore:0.###}"
            : $"valid={Candidate.IsValid},failure={Candidate.FailureKind}:"
              + $"{Candidate.FailureReason},rejected={RejectedCandidate.FailureKind}:"
              + RejectedCandidate.FailureReason;
        return $"passed={Passed};earliest={EarliestFailure};"
            + $"authored={AuthoredWorkType};husbandry=[{husbandry}];"
            + $"search={SearchAvailable};workAccess={WorkAccessAvailable}:"
            + $"{WorkAccessPosition}:cost={WorkAccessCost};"
            + $"policy={PolicyAvailable}:{PolicyReason};candidate=[{candidate}];"
            + $"emergencyGate={EmergencyGateActive}:{EmergencyGateEpoch}:"
            + $"{EmergencyGateWorkType.Value}:allows={EmergencyGateAllowsAnimalCare};"
            + $"aiWorkCatalog={AiWorkCatalogAvailable}:[{AiWorkCatalog}];"
            + $"candidateIndex=pending:{CandidateIndexPending},"
            + $"containsPen:{CandidateIndexContainsPen},"
            + $"revision:{CandidateIndexRevision},dynamic:{CandidateDynamicRevision}";
    }
}

internal static class AnimalCareAiPreflight
{
    public static AnimalCareAiPreflightSnapshot Capture(
        CharacterActor actor,
        AbilityWork work,
        BuildableObject pen,
        IAnimalHusbandryQuery husbandry,
        IWorkPolicyRegistry policies,
        IFacilityCandidateCache facilityCandidates)
    {
        AnimalCareAiPreflightSnapshot result = new();
        if (actor == null || work == null || pen == null)
        {
            return result;
        }

        WorkTypeId workTypeId = BuiltInWorkTypeIds.AnimalCare;
        result.EmergencyGateActive = work.HasEmergencyResponseWorkGateForDiagnostics;
        result.EmergencyGateEpoch = work.EmergencyResponseWorkEpochForDiagnostics;
        result.EmergencyGateWorkType =
            work.EmergencyResponseOnlyWorkTypeForDiagnostics;
        result.EmergencyGateAllowsAnimalCare =
            work.IsWorkTypeAllowedByEmergencyResponseGate(workTypeId);
        result.AuthoredWorkType =
            pen.BuildingData?.Facility?.SupportsWork(workTypeId) == true;
        result.HusbandryAvailable = husbandry != null
            && husbandry.TryGetWork(
                pen,
                actor,
                out result.HusbandryWork)
            && result.HusbandryWork.Available;

        AIBrain brain = actor.Brain;
        GridPathSearchResult search = brain?.GetPathSearch(actor);
        result.SearchAvailable = search != null;
        result.WorkAccessAvailable = search != null
            && WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                pen,
                search,
                out result.WorkAccessPosition);
        if (result.WorkAccessAvailable)
        {
            result.WorkAccessCost = search.GetMoveCostTo(
                result.WorkAccessPosition);
            result.WorkAccessAvailable = result.WorkAccessCost != int.MaxValue;
        }

        result.PolicyAvailable = policies != null
            && policies.IsAvailable(
                workTypeId,
                actor,
                pen,
                out result.PolicyReason);
        if (policies == null)
        {
            result.PolicyReason = "work policy registry missing";
        }

        result.CandidateAvailable = search != null
            && work.TryGetBestWorkCandidate(
                workTypeId,
                search,
                out result.Candidate);
        result.CandidateTargetsPen = result.CandidateAvailable
            && ReferenceEquals(
                WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                    result.Candidate),
                pen);
        if (!result.CandidateAvailable
            && result.EmergencyGateAllowsAnimalCare)
        {
            work.TryGetLastRejectedWorkCandidate(
                out result.RejectedCandidate);
        }

        AIWork[] authoredWorkActions = brain?.availableActions?
            .Select(action => action?.actionset)
            .OfType<AIWork>()
            .ToArray() ?? Array.Empty<AIWork>();
        result.AiWorkCatalogAvailable = authoredWorkActions.Any(action =>
            action.WorkTypeId == workTypeId
            || !action.WorkTypeId.IsValid);
        result.AiWorkCatalog = string.Join(
            ",",
            authoredWorkActions.Select(action =>
                action.WorkTypeId.IsValid
                    ? action.WorkTypeId.Value
                    : "<generic>"));

        if (facilityCandidates != null && pen.Grid != null)
        {
            FacilityWorkType legacy =
                FacilityWorkTypeMap.GetRequired(workTypeId);
            result.CandidateIndexPending =
                facilityCandidates.HasPendingIndexBuild;
            result.CandidateIndexContainsPen = facilityCandidates
                .GetWorkCandidates(pen.Grid, legacy)
                .Contains(pen);
            result.CandidateIndexRevision =
                facilityCandidates.CandidateIndexVersion;
            result.CandidateDynamicRevision =
                facilityCandidates.DynamicStateVersion;
        }
        return result;
    }
}
#endif
