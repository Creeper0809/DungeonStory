using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public sealed class FacilityEvolutionPanelCheckSnapshot
{
    public FacilityEvolutionPanelCheckSnapshot(
        string category,
        string label,
        bool passed,
        string detail)
    {
        Category = category ?? string.Empty;
        Label = label ?? string.Empty;
        Passed = passed;
        Detail = detail ?? string.Empty;
    }

    public string Category { get; }
    public string Label { get; }
    public bool Passed { get; }
    public string Detail { get; }
}

public sealed class FacilityEvolutionPanelCandidateSnapshot
{
    public FacilityEvolutionPanelCandidateSnapshot(
        string evolutionId,
        string displayName,
        string resultName,
        bool approved,
        bool usesIdentityPressure,
        string identityMessage,
        IReadOnlyList<FacilityEvolutionPanelCheckSnapshot> checks,
        string rejectedHint,
        string reason)
    {
        EvolutionId = evolutionId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        ResultName = resultName ?? string.Empty;
        Approved = approved;
        UsesIdentityPressure = usesIdentityPressure;
        IdentityMessage = identityMessage ?? string.Empty;
        Checks = checks ?? Array.Empty<FacilityEvolutionPanelCheckSnapshot>();
        RejectedHint = rejectedHint ?? string.Empty;
        Reason = reason ?? string.Empty;
    }

    public string EvolutionId { get; }
    public string DisplayName { get; }
    public string ResultName { get; }
    public bool Approved { get; }
    public bool UsesIdentityPressure { get; }
    public string IdentityMessage { get; }
    public IReadOnlyList<FacilityEvolutionPanelCheckSnapshot> Checks { get; }
    public string RejectedHint { get; }
    public string Reason { get; }
}

public sealed class FacilityEvolutionPanelSnapshot
{
    public FacilityEvolutionPanelSnapshot(
        string facilityName,
        int starGrade,
        IReadOnlyList<string> lineageTags,
        IReadOnlyList<string> mutationTags,
        bool roomUsable,
        float seatDensity,
        float luxuryPerSeat,
        IReadOnlyDictionary<string, float> identityPressures,
        IReadOnlyList<FacilityEvolutionPanelCandidateSnapshot> candidates)
    {
        FacilityName = facilityName ?? string.Empty;
        StarGrade = starGrade;
        LineageTags = lineageTags ?? Array.Empty<string>();
        MutationTags = mutationTags ?? Array.Empty<string>();
        RoomUsable = roomUsable;
        SeatDensity = seatDensity;
        LuxuryPerSeat = luxuryPerSeat;
        IdentityPressures = identityPressures ?? new Dictionary<string, float>();
        Candidates = candidates ?? Array.Empty<FacilityEvolutionPanelCandidateSnapshot>();
    }

    public string FacilityName { get; }
    public int StarGrade { get; }
    public IReadOnlyList<string> LineageTags { get; }
    public IReadOnlyList<string> MutationTags { get; }
    public bool RoomUsable { get; }
    public float SeatDensity { get; }
    public float LuxuryPerSeat { get; }
    public IReadOnlyDictionary<string, float> IdentityPressures { get; }
    public IReadOnlyList<FacilityEvolutionPanelCandidateSnapshot> Candidates { get; }
}

public interface IFacilityEvolutionPanelQuery
{
    FacilityEvolutionPanelSnapshot GetSnapshot(
        BuildableObject facility,
        bool includeRejected);
}

public interface IFacilityEvolutionPanelCommand
{
    bool TryEvolve(
        BuildableObject facility,
        string evolutionId,
        out FacilityEvolutionResult result);
}

public sealed class FacilityEvolutionPanelGateway :
    IFacilityEvolutionPanelQuery,
    IFacilityEvolutionPanelCommand
{
    private readonly FacilityEvolutionRuntime runtime;

    [Inject]
    public FacilityEvolutionPanelGateway(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
        : this((runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences))).Evolution)
    {
    }

    public FacilityEvolutionPanelGateway(FacilityEvolutionRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public FacilityEvolutionPanelSnapshot GetSnapshot(
        BuildableObject facility,
        bool includeRejected)
    {
        if (facility == null || facility.isDestroy)
        {
            return null;
        }

        FacilityEvolutionContext context = runtime.BuildContext(facility);
        RoomProfile profile = context.Profile;
        FacilityEvolutionPanelCandidateSnapshot[] candidates = runtime
            .GetCandidates(facility, includeRejected)
            .Where(candidate => candidate?.Recipe != null)
            .Select(candidate => new FacilityEvolutionPanelCandidateSnapshot(
                candidate.Recipe.EffectiveId,
                candidate.Recipe.DisplayName,
                FacilityShopService.GetBuildingName(candidate.Recipe.resultBuilding),
                candidate.Approved,
                candidate.IdentityScore.UsesIdentityPressure,
                candidate.IdentityScore.ToMessage(),
                candidate.Validation?.Checks?
                    .Take(8)
                    .Select(check => new FacilityEvolutionPanelCheckSnapshot(
                        check.Category,
                        check.Label,
                        check.Passed,
                        check.Detail))
                    .ToArray() ?? Array.Empty<FacilityEvolutionPanelCheckSnapshot>(),
                candidate.RejectedHintText,
                !string.IsNullOrWhiteSpace(candidate.Reason)
                    ? candidate.Reason
                    : candidate.Validation?.ToMessage()))
            .ToArray();

        return new FacilityEvolutionPanelSnapshot(
            FacilityShopService.GetBuildingName(facility.BuildingData),
            context.State.StarGrade,
            context.State.LineageTags.ToArray(),
            context.State.MutationTags.ToArray(),
            profile.IsUsable,
            profile.GetMetric(FacilityEvolutionTerms.SeatDensity),
            profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat),
            new Dictionary<string, float>(profile.IdentityPressures),
            candidates);
    }

    public bool TryEvolve(
        BuildableObject facility,
        string evolutionId,
        out FacilityEvolutionResult result)
    {
        result = default;
        if (facility == null || string.IsNullOrWhiteSpace(evolutionId))
        {
            return false;
        }

        FacilityEvolutionCandidate candidate = runtime
            .GetCandidates(facility, includeRejected: false)
            .FirstOrDefault(item => item?.Recipe != null
                && item.Recipe.EffectiveId == evolutionId);
        return candidate != null
            && candidate.Approved
            && runtime.TryEvolve(facility, candidate.Recipe, out result);
    }
}
