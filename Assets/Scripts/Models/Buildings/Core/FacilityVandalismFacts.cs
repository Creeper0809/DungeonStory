using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum FacilityVandalismSourceKind { Macro = 1, Deprivation = 2 }
public enum FacilityVandalismCommitPhase { None = 0, Applied = 1 }

/// <summary>Domain damage receipt. Publication references are opaque transport data.</summary>
[Serializable]
public sealed class FacilityVandalismCommitState
{
    public long operationSequence;
    public string facilityId = string.Empty;
    public string buildingDefinitionId = string.Empty;
    public string actorId = string.Empty;
    public string actorName = string.Empty;
    public string facilityName = string.Empty;
    public string source = string.Empty;
    public string reason = string.Empty;
    public int absoluteDay, positionX, positionY;
    public bool damagedBefore, damagedAfter, hasStructuralIntegrity;
    public float structuralHitPointsBefore, structuralHitPointsAfter;
    public int phase;
    public string publicationKey = string.Empty;
    public List<string> witnessPublicationKeys = new();

    public FacilityVandalismCommitState Clone()
    {
        var value = (FacilityVandalismCommitState)MemberwiseClone();
        value.witnessPublicationKeys = witnessPublicationKeys?.ToList();
        return value;
    }
    public bool IsEmpty => operationSequence == 0 && phase == 0
        && string.IsNullOrEmpty(facilityId) && string.IsNullOrEmpty(buildingDefinitionId)
        && string.IsNullOrEmpty(actorId) && string.IsNullOrEmpty(actorName) && string.IsNullOrEmpty(facilityName)
        && string.IsNullOrEmpty(source) && string.IsNullOrEmpty(reason) && string.IsNullOrEmpty(publicationKey)
        && absoluteDay == 0 && positionX == 0 && positionY == 0
        && !damagedBefore && !damagedAfter && !hasStructuralIntegrity
        && structuralHitPointsBefore == 0f && structuralHitPointsAfter == 0f
        && witnessPublicationKeys != null && witnessPublicationKeys.Count == 0;

    public bool IsValid(long nextOperationSequence, out string error)
    {
        error = string.Empty;
        if (IsEmpty)
        {
            if (nextOperationSequence == 1) return true;
            error = "An empty damage receipt requires the initial sequence.";
            return false;
        }
        try
        {
            ValidateFact(this);
            if (nextOperationSequence != operationSequence + 1L || !Opaque(publicationKey))
                throw new ArgumentException("Damage sequence or publication reference is invalid.");
            return true;
        }
        catch (ArgumentException exception) { error = exception.Message; return false; }
    }
    public static void ValidateFact(FacilityVandalismCommitState value)
    {
        if (value == null || value.operationSequence <= 0 || value.operationSequence == long.MaxValue
            || value.phase != (int)FacilityVandalismCommitPhase.Applied
            || !DomainFactValueSyntax.IsIdentity(value.facilityId)
            || !DomainFactValueSyntax.IsIdentity(value.buildingDefinitionId)
            || !DomainFactValueSyntax.IsIdentity(value.actorId)
            || !Text(value.actorName, 256) || !Text(value.facilityName, 256)
            || value.source is not ("macro" or "deprivation") || !Text(value.reason, 1024)
            || value.absoluteDay < 1 || value.damagedBefore || !value.damagedAfter
            || !Finite(value.structuralHitPointsBefore) || !Finite(value.structuralHitPointsAfter)
            || value.structuralHitPointsBefore < 0 || value.structuralHitPointsAfter < 0
            || value.structuralHitPointsBefore != value.structuralHitPointsAfter
            || !value.hasStructuralIntegrity && value.structuralHitPointsBefore != 0f
            || value.witnessPublicationKeys == null)
            throw new ArgumentException("Facility vandalism facts are invalid.");
        string previous = null;
        foreach (string reference in value.witnessPublicationKeys)
        {
            if (!Opaque(reference) || previous != null && string.CompareOrdinal(previous, reference) >= 0)
                throw new ArgumentException("Witness publication references must be ordered and unique.");
            previous = reference;
        }
    }
    private static bool Opaque(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
    private static bool Text(string value, int limit) => DomainFactValueSyntax.IsText(value, limit, false) && value.Trim() == value;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class FacilityVandalismFact
{
    private readonly FacilityVandalismCommitState data;
    public FacilityVandalismFact(FacilityVandalismCommitState data)
    {
        FacilityVandalismCommitState.ValidateFact(data);
        this.data = data.Clone();
        this.data.publicationKey = string.Empty;
    }
    public FacilityVandalismCommitState Snapshot() => data.Clone();
}

[Serializable]
public sealed class FacilityVandalismPendingSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<FacilityVandalismCommitState> pending = new();
}

/// <summary>Delivery lifetime is independent of the damaged building's lifetime.</summary>
public sealed class FacilityVandalismPendingState
{
    public const int Capacity = 2048;
    private readonly Dictionary<string, FacilityVandalismCommitState> pending = new(StringComparer.Ordinal);
    public int Count => pending.Count;
    public FacilityVandalismPendingSaveData Capture() => new()
    {
        pending = pending.Values.OrderBy(value => value.facilityId, StringComparer.Ordinal)
            .ThenBy(value => value.operationSequence).Select(value => value.Clone()).ToList()
    };
    public bool Contains(string publicationKey) => pending.ContainsKey(publicationKey);
    public void Add(FacilityVandalismCommitState value)
    {
        if (value == null || value.IsEmpty || !value.IsValid(checked(value.operationSequence + 1), out _)
            || pending.Count >= Capacity || pending.ContainsKey(value.publicationKey)
            || pending.Values.Any(existing => existing.facilityId == value.facilityId && existing.operationSequence == value.operationSequence))
            throw new InvalidOperationException("Facility vandalism pending identity or capacity conflict.");
        pending.Add(value.publicationKey, value.Clone());
    }
    public void Remove(string publicationKey)
    {
        if (!pending.Remove(publicationKey))
            throw new InvalidOperationException("Facility vandalism pending publication is missing.");
    }
    public static FacilityVandalismPendingState Restore(FacilityVandalismPendingSaveData save)
    {
        if (save == null || save.version != FacilityVandalismPendingSaveData.CurrentVersion || save.pending == null)
            throw new InvalidOperationException("Facility vandalism pending save is invalid.");
        var state = new FacilityVandalismPendingState();
        foreach (var entry in save.pending) state.Add(entry);
        return state;
    }
}
