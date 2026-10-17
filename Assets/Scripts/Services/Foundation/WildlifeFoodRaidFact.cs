using System;
using System.Collections.Generic;
using System.Linq;

// Exact domain facts retained after physical consumption; delivery identity is separate.
[Serializable]
public sealed class WildlifeFoodRaidFactData
{
    public string raidId = string.Empty;
    public string wildlifeId = string.Empty;
    public string wildlifeSpeciesId = string.Empty;
    public string wildlifeDisplayName = string.Empty;
    public string targetStackId = string.Empty;
    public string targetItemId = string.Empty;
    public string targetItemDisplayName = string.Empty;
    public string dispositionOperationId = string.Empty;
    public string dispositionReasonCode = string.Empty;
    public string dispositionCommitId = string.Empty;
    public string dispositionItemId = string.Empty;
    public string outcomeReason = string.Empty;
    public int requestedQuantity;
    public int startX;
    public int startY;
    public int startedAbsoluteDay;
    public int endX;
    public int endY;
    public int completedAbsoluteDay;
    public int stolenQuantity;
    public int dispositionQuantity;
    public float startedGameTime;
    public float completedGameTime;
    public long dispositionInputMassGrams;
    public long dispositionOwnerRevision;
    public List<string> dispositionSourceStackIds = new();

    public bool IsEmpty => dispositionSourceStackIds?.Count == 0
        && raidId == string.Empty
        && wildlifeId == string.Empty
        && wildlifeSpeciesId == string.Empty
        && wildlifeDisplayName == string.Empty
        && targetStackId == string.Empty
        && targetItemId == string.Empty
        && targetItemDisplayName == string.Empty
        && dispositionOperationId == string.Empty
        && dispositionReasonCode == string.Empty
        && dispositionCommitId == string.Empty
        && dispositionItemId == string.Empty
        && outcomeReason == string.Empty
        && requestedQuantity == 0
        && startX == 0
        && startY == 0
        && startedAbsoluteDay == 0
        && endX == 0
        && endY == 0
        && completedAbsoluteDay == 0
        && stolenQuantity == 0
        && dispositionQuantity == 0
        && startedGameTime == 0
        && completedGameTime == 0
        && dispositionInputMassGrams == 0
        && dispositionOwnerRevision == 0;

    public WildlifeFoodRaidFactData Clone()
    {
        var copy = (WildlifeFoodRaidFactData)MemberwiseClone();
        copy.dispositionSourceStackIds = dispositionSourceStackIds?.ToList();
        return copy;
    }

    public void Validate()
    {
        if (!Text(raidId) || !Text(wildlifeId) || !Text(wildlifeSpeciesId)
            || !Text(wildlifeDisplayName) || !Text(targetStackId) || !Text(targetItemId)
            || !Text(targetItemDisplayName) || !Text(dispositionCommitId) || !Text(outcomeReason)
            || dispositionOperationId != $"wildlife-food-raid:{raidId}:{wildlifeId}"
            || dispositionReasonCode != "wildlife-food-raid-consumed"
            || dispositionItemId != targetItemId
            || requestedQuantity != 1 || stolenQuantity != 1 || dispositionQuantity != 1
            || dispositionInputMassGrams <= 0 || dispositionOwnerRevision <= 0
            || dispositionSourceStackIds?.Count != 1
            || dispositionSourceStackIds[0] != targetStackId
            || !float.IsFinite(startedGameTime) || !float.IsFinite(completedGameTime)
            || startedGameTime < 0 || completedGameTime < startedGameTime
            || startedAbsoluteDay < 0 || completedAbsoluteDay < startedAbsoluteDay)
            throw new ArgumentException("wildlife-food-raid-fact-invalid");
    }

    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 4096 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public readonly struct WildlifeFoodRaidFact
{
    private readonly WildlifeFoodRaidFactData value;
    public WildlifeFoodRaidFact(WildlifeFoodRaidFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        value = source.Clone();
    }
    public WildlifeFoodRaidFactData Snapshot() => value?.Clone()
        ?? throw new InvalidOperationException("wildlife-food-raid-fact-uninitialized");
}
