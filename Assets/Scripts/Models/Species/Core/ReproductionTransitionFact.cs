using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class ReproductionTransitionPersonData
{
    public string characterId = string.Empty;
    public string name = string.Empty;
    public string role = string.Empty;
    public ReproductionTransitionPersonData Clone() => (ReproductionTransitionPersonData)MemberwiseClone();
}

[Serializable]
public sealed class ReproductionTransitionFactData
{
    public string processId = string.Empty;
    public string transitionKind = string.Empty;
    public ReproductionMode mode;
    public string phenotypeSpeciesId = string.Empty;
    public bool hasBefore;
    public ReproductionProcessStatus statusBefore, statusAfter;
    public ReproductionFailureCode failureBefore, failureAfter;
    public string cause = string.Empty;
    public string facilityId = string.Empty;
    public string facilityName = string.Empty;
    public string costs = string.Empty;
    public int absoluteDay;
    public long revisionBefore, revisionAfter;
    public int phaseBefore, phaseAfter, phaseElapsedBefore, phaseElapsedAfter, progressBefore, progressAfter;
    public int emergencyDeadlineDay;
    public string locationInstanceId = string.Empty;
    public string locationRegionId = string.Empty;
    public int locationX, locationY;
    public string parentPublicationKey = string.Empty;
    public string previousPublicationKey = string.Empty;
    public List<ReproductionTransitionPersonData> people = new();
    public ReproductionTransitionFactData Clone()
    {
        var copy = (ReproductionTransitionFactData)MemberwiseClone();
        copy.people = people?.Select(value => value?.Clone()).ToList();
        return copy;
    }
}

[Serializable]
public sealed class ReproductionPendingTransitionData
{
    public string publicationKey = string.Empty;
    public ReproductionTransitionFactData fact;
    public ReproductionPendingTransitionData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}

public readonly struct ReproductionFactLocation
{
    public ReproductionFactLocation(string instanceId, string regionId, int x, int y)
    { InstanceId = instanceId ?? string.Empty; RegionId = regionId ?? string.Empty; X = x; Y = y; }
    public string InstanceId { get; }
    public string RegionId { get; }
    public int X { get; }
    public int Y { get; }
}

/// <summary>Immutable gameplay facts, independent of log schema and presentation policy.</summary>
public sealed class ReproductionTransitionFact
{
    private readonly ReproductionTransitionFactData data;
    public ReproductionTransitionFact(ReproductionTransitionFactData source)
    { Validate(source); data = source.Clone(); }
    public ReproductionTransitionFactData Snapshot() => data.Clone();
    public static void Validate(ReproductionTransitionFactData value)
    {
        if (value == null || !ReproductionStableIdSyntax.IsValid(value.processId)
            || !ReproductionStableIdSyntax.IsValid(value.phenotypeSpeciesId)
            || !ReproductionStableIdSyntax.IsValid(value.facilityId)
            || value.transitionKind is not ("planned" or "started" or "daily-transition" or "carrier-death" or "emergency-extraction" or "birth-published")
            || !Enum.IsDefined(typeof(ReproductionMode), value.mode)
            || !Enum.IsDefined(typeof(ReproductionProcessStatus), value.statusBefore)
            || !Enum.IsDefined(typeof(ReproductionProcessStatus), value.statusAfter)
            || !Enum.IsDefined(typeof(ReproductionFailureCode), value.failureBefore)
            || !Enum.IsDefined(typeof(ReproductionFailureCode), value.failureAfter)
            || !Text(value.facilityName) || !Text(value.cause) || !Text(value.costs)
            || value.absoluteDay < 0 || value.revisionBefore < 0 || value.revisionBefore == long.MaxValue
            || value.revisionAfter != value.revisionBefore + 1
            || value.phaseBefore < 0 || value.phaseAfter < 0 || value.phaseElapsedBefore < 0 || value.phaseElapsedAfter < 0
            || value.progressBefore < 0 || value.progressAfter < 0 || value.emergencyDeadlineDay < 0
            || value.parentPublicationKey == null || value.previousPublicationKey == null
            || value.locationInstanceId == null || value.locationRegionId == null
            || value.people == null || value.people.Count < 2 || value.people.Count > 5)
            throw new ArgumentException("Reproduction transition fact is incomplete or inconsistent.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var person in value.people)
            if (person == null || !new CharacterId(person.characterId).IsValid || !Text(person.name)
                || person.role is not ("genetic-parent" or "reproduction-carrier" or "medical-responder" or "offspring")
                || !seen.Add(person.role + ":" + person.characterId))
                throw new ArgumentException("Reproduction transition person is invalid or duplicated.");
        if (value.people.Count(person => person.role == "reproduction-carrier") != 1
            || value.people.Count(person => person.role == "genetic-parent") is < 1 or > 2
            || value.people.Count(person => person.role == "offspring") != (value.transitionKind == "birth-published" ? 1 : 0)
            || value.people.Count(person => person.role == "medical-responder") != (value.transitionKind == "emergency-extraction" ? 1 : 0)
            || value.hasBefore == (value.transitionKind == "planned")
            || value.transitionKind == "planned" && (value.statusAfter != ReproductionProcessStatus.Planned || value.revisionAfter != 1)
            || value.transitionKind == "birth-published" && value.statusAfter != ReproductionProcessStatus.Completed
            || value.transitionKind == "emergency-extraction"
                && (value.statusBefore != ReproductionProcessStatus.WaitingForEmergencyExtraction
                    || value.statusAfter != ReproductionProcessStatus.Active || value.emergencyDeadlineDay < value.absoluteDay))
            throw new ArgumentException("Reproduction transition parent/carrier facts are missing.");
    }
    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
}
