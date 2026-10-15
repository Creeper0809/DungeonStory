using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CombatCellShotFactData
{
    public string commandId = string.Empty;
    public int attackRevision;
    public string actorId = string.Empty;
    public string actorName = string.Empty;
    public int absoluteDay;
    public int targetX;
    public int targetY;
    public string weaponDefinitionId = string.Empty;
    public string weaponInstanceId = string.Empty;
    public CombatDamageType damageType;
    public CombatFireMode fireMode;
    public bool requiresAmmunition;
    public string ammunitionItemId = string.Empty;
    public int ammunitionBefore;
    public int ammunitionConsumed;
    public int ammunitionAfter;
    public float manaBefore;
    public float manaCost;
    public float manaSpent;
    public float manaAfter;
    public CombatCellShotFactData Clone() => (CombatCellShotFactData)MemberwiseClone();
}

/// <summary>An actual empty-cell shot; no actor references, display policy or storage identities.</summary>
public sealed class CombatCellShotFact
{
    private readonly CombatCellShotFactData data;
    public CombatCellShotFact(CombatCellShotFactData value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (!TryReadCommandSequence(value.commandId, out _)
            || value.attackRevision <= 0 || value.attackRevision == int.MaxValue
            || !DomainFactValueSyntax.IsIdentity(value.actorId) || !new CharacterId(value.actorId).IsValid
            || !DomainFactValueSyntax.IsText(value.actorName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || value.absoluteDay < 1
            || !DomainFactValueSyntax.IsIdentity(value.weaponDefinitionId)
            || !DomainFactValueSyntax.IsIdentity(value.weaponInstanceId) || !new ItemInstanceId(value.weaponInstanceId).IsValid
            || !Enum.IsDefined(typeof(CombatDamageType), value.damageType)
            || !Enum.IsDefined(typeof(CombatFireMode), value.fireMode)
            || value.ammunitionItemId == null
            || value.ammunitionBefore < 0 || value.ammunitionConsumed < 0
            || value.ammunitionConsumed > value.ammunitionBefore
            || value.ammunitionAfter != value.ammunitionBefore - value.ammunitionConsumed
            || value.requiresAmmunition && (!DomainFactValueSyntax.IsIdentity(value.ammunitionItemId) || value.ammunitionConsumed == 0)
            || !value.requiresAmmunition && (value.ammunitionConsumed != 0 || value.ammunitionBefore != 0 || value.ammunitionItemId.Length != 0)
            || !FiniteNonnegative(value.manaBefore) || !FiniteNonnegative(value.manaCost) || !FiniteNonnegative(value.manaSpent)
            || value.manaSpent > value.manaBefore || !FiniteNonnegative(value.manaAfter)
            || value.manaAfter != Math.Max(0f, value.manaBefore - value.manaCost)
            || value.manaSpent != value.manaBefore - value.manaAfter)
            throw new ArgumentException("combat-cell-shot-fact-invalid", nameof(value));
        data = value.Clone();
    }
    public string OperationId => data.commandId + ":" + data.attackRevision.ToString(CultureInfo.InvariantCulture);
    public CombatCellShotFactData Snapshot() => data.Clone();
    public static bool TryReadCommandSequence(string value, out int sequence)
    {
        const string prefix = "combat-command:";
        sequence = 0;
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string suffix = value.Substring(prefix.Length);
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
            && sequence > 0 && suffix == sequence.ToString(CultureInfo.InvariantCulture);
    }
    private static bool FiniteNonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

[Serializable]
public sealed class CombatCellShotPendingData
{
    public string publicationKey = string.Empty;
    public CombatCellShotFactData fact = new();
    public CombatCellShotPendingData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}
