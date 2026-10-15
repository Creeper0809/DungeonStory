using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>Immutable policy transition owned by Survival, with no logging or presentation contract.</summary>
public sealed class CharacterConsumablesPolicyChangedFact
{
    public CharacterConsumablesPolicyChangedFact(CharacterId characterId, string displayName,
        Vector2Int position, string policyId, string scopeId, string beforeValue, string afterValue,
        string source, long beforeRevision, long afterRevision, int absoluteDay)
    {
        if (!characterId.IsValid || string.IsNullOrWhiteSpace(displayName)
            || !Token(policyId) || !Token(scopeId) || !Token(source)
            || beforeRevision < 0 || beforeRevision == long.MaxValue
            || afterRevision != beforeRevision + 1 || absoluteDay < 0
            || beforeValue == afterValue || !ValidValue(policyId, beforeValue)
            || !ValidValue(policyId, afterValue)
            || (policyId != "character-substance" && scopeId != "self"))
            throw new ArgumentException("Invalid consumables policy transition.");
        CharacterId = characterId; DisplayName = displayName; Position = position;
        PolicyId = policyId; ScopeId = scopeId; BeforeValue = beforeValue; AfterValue = afterValue;
        Source = source; BeforeRevision = beforeRevision; AfterRevision = afterRevision;
        AbsoluteDay = absoluteDay;
    }

    public CharacterId CharacterId { get; }
    public string DisplayName { get; }
    public Vector2Int Position { get; }
    public string PolicyId { get; }
    public string ScopeId { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }
    public string Source { get; }
    public long BeforeRevision { get; }
    public long AfterRevision { get; }
    public int AbsoluteDay { get; }

    public CharacterConsumablesPolicyPendingFactSaveData Capture() => new()
    {
        characterId = CharacterId.Value, displayName = DisplayName,
        positionX = Position.x, positionY = Position.y, policyId = PolicyId, scopeId = ScopeId,
        beforeValue = BeforeValue, afterValue = AfterValue, source = Source,
        beforeRevision = BeforeRevision, afterRevision = AfterRevision, absoluteDay = AbsoluteDay
    };

    public static CharacterConsumablesPolicyChangedFact Restore(CharacterConsumablesPolicyPendingFactSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        CharacterId id = new(data.characterId);
        if (id.Value != data.characterId) throw new ArgumentException("Noncanonical policy actor identity.");
        return new CharacterConsumablesPolicyChangedFact(id, data.displayName,
            new Vector2Int(data.positionX, data.positionY), data.policyId, data.scopeId,
            data.beforeValue, data.afterValue, data.source, data.beforeRevision, data.afterRevision, data.absoluteDay);
    }

    internal static string CanonicalSubstanceValue(CharacterSubstancePolicyState state) =>
        state.mode.ToString().ToLowerInvariant() + ":"
        + state.moodThreshold.ToString("R", CultureInfo.InvariantCulture) + ":"
        + state.scheduledHour.ToString(CultureInfo.InvariantCulture);

    private static bool ValidValue(string policy, string value)
    {
        if (!Token(value)) return false;
        if (policy == "character-diet") return CanonicalEnum<CharacterDietPolicyKind>(value);
        if (policy == "character-meal-quality") return CanonicalEnum<CharacterMealQualityLimit>(value);
        if (policy != "character-substance") return false;
        string[] parts = value.Split(':');
        return parts.Length == 3 && CanonicalEnum<SubstancePolicyMode>(parts[0])
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float mood)
            && !float.IsNaN(mood) && !float.IsInfinity(mood) && mood >= 0 && mood <= 100
            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int hour)
            && hour >= 0 && hour <= 23
            && mood.ToString("R", CultureInfo.InvariantCulture) == parts[1]
            && hour.ToString(CultureInfo.InvariantCulture) == parts[2];
    }

    private static bool CanonicalEnum<T>(string value) where T : struct, Enum =>
        Enum.GetValues(typeof(T)).Cast<T>().Any(item => item.ToString().ToLowerInvariant() == value);
    private static bool Token(string value) => !string.IsNullOrEmpty(value) && value.Length <= 192
        && value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'
            || c >= '0' && c <= '9' || c is '.' or ':' or '_' or '-' or '/');
}

[Serializable]
public sealed class CharacterConsumablesPolicyPendingFactSaveData
{
    public string characterId;
    public string displayName;
    public int positionX;
    public int positionY;
    public string policyId;
    public string scopeId;
    public string beforeValue;
    public string afterValue;
    public string source;
    public long beforeRevision;
    public long afterRevision;
    public int absoluteDay;
}
