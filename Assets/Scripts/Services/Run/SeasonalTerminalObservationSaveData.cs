using System;
using System.Linq;
using DungeonStory.Foundation.Facts;


[Serializable]
public sealed class SeasonalWildlifeArrivalFactData
{
    public string occurrenceId = string.Empty, speciesId = string.Empty, speciesDisplayName = string.Empty, habitatId = string.Empty;
    public int absoluteDay;
    public string[] wildlifeIds = Array.Empty<string>();
    public SeasonalWildlifeArrivalFactData Clone()
    { var copy = (SeasonalWildlifeArrivalFactData)MemberwiseClone(); copy.wildlifeIds = wildlifeIds?.ToArray(); return copy; }
    public bool IsEmpty => occurrenceId == string.Empty && speciesId == string.Empty && speciesDisplayName == string.Empty
        && habitatId == string.Empty && absoluteDay == 0 && wildlifeIds?.Length == 0;
}

[Serializable]
public sealed class SeasonalDriftCargoFactData
{
    public string occurrenceId = string.Empty, itemId = string.Empty, itemDisplayName = string.Empty, dispositionCommitId = string.Empty;
    public int absoluteDay, exactQuantity, securedQuantity, expiredQuantity, positionX, positionY;
    public long unitMassGrams, expiredMassGrams;
    public string[] sourceStackIds = Array.Empty<string>();
    public SeasonalDriftCargoFactData Clone()
    { var copy = (SeasonalDriftCargoFactData)MemberwiseClone(); copy.sourceStackIds = sourceStackIds?.ToArray(); return copy; }
    public bool IsEmpty => occurrenceId == string.Empty && itemId == string.Empty && itemDisplayName == string.Empty
        && dispositionCommitId == string.Empty && absoluteDay == 0 && exactQuantity == 0 && securedQuantity == 0
        && expiredQuantity == 0 && positionX == 0 && positionY == 0 && unitMassGrams == 0 && expiredMassGrams == 0
        && sourceStackIds?.Length == 0;
}

[Serializable]
public sealed class SeasonalTerminalPublicationSaveData
{
    public string key = string.Empty, id = string.Empty, fingerprint = string.Empty;
    public bool IsEmpty => key == string.Empty && id == string.Empty && fingerprint == string.Empty;
    public void Validate(string completedKey, bool hasFact)
    {
        if (key == null || id == null || fingerprint == null || completedKey == null
            || !DomainFactValueSyntax.IsText(key, 4096, true) || key != key.Trim()
            || !hasFact && (!IsEmpty || completedKey.Length != 0)
            || key.Length == 0 && (id.Length != 0 || fingerprint.Length != 0 || completedKey.Length != 0)
            || completedKey.Length == 0 && (id.Length != 0 || fingerprint.Length != 0)
            || completedKey.Length != 0 && (key != completedKey || !DomainFactValueSyntax.IsText(id, 4096, false)
                || id != id.Trim() || fingerprint.Length != 64
                || fingerprint.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))))
            throw new InvalidOperationException("seasonal-terminal-publication-invalid");
    }
}
