using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable health transition, without record kinds, salience or presentation.</summary>
public sealed class CharacterHealingFact
{
    internal const int MaximumPending = 512;
    private readonly CharacterHealingFactData data;

    public CharacterHealingFact(CharacterHealingFactData value, bool proposalOnly = false)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        var id = new CharacterId(value.characterId);
        if (!id.IsValid || id.Value != value.characterId
            || !DomainFactValueSyntax.IsText(value.characterName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || value.absoluteDay <= 0 || !ValidHealth(value.healthBefore, value.maximumHealthBefore)
            || !ValidHealth(value.healthAfter, value.maximumHealthAfter))
            throw new ArgumentException("character-healing-fact-invalid", nameof(value));
        data = value.Clone();
        IsProposal = proposalOnly;
    }

    public bool IsProposal { get; }
    public CharacterHealingFactData Data => data.Clone();
    public float RatioBefore => data.healthBefore / Math.Max(1f, data.maximumHealthBefore);
    public float RatioAfter => data.healthAfter / Math.Max(1f, data.maximumHealthAfter);
    public bool CrossedCritical => RatioBefore > 0.20f && RatioAfter <= 0.20f
        || RatioBefore <= 0.20f && RatioAfter > 0.20f;
    public bool Recovered => data.downedBefore && !data.downedAfter;
    public CharacterHealingFactData Snapshot() => !IsProposal ? data.Clone()
        : throw new InvalidOperationException("A healing proposal is not an applied fact.");

    internal static void ValidatePending(IReadOnlyList<CharacterHealingPendingFactData> pending)
    {
        if (pending == null || pending.Count > MaximumPending)
            throw new ArgumentException("character-healing-pending-list-invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pending)
        {
            if (entry == null || entry.criticalPublicationKey == null || entry.recoveryPublicationKey == null)
                throw new ArgumentException("character-healing-pending-entry-invalid");
            var fact = new CharacterHealingFact(entry.fact);
            if (!owners.Add(entry.fact.characterId))
                throw new ArgumentException("character-healing-pending-owner-duplicate");
            if (!fact.CrossedCritical && !fact.Recovered
                || fact.CrossedCritical != (entry.criticalPublicationKey.Length > 0)
                || fact.Recovered != (entry.recoveryPublicationKey.Length > 0))
                throw new ArgumentException("character-healing-pending-transition-conflict");
            foreach (var key in new[] { entry.criticalPublicationKey, entry.recoveryPublicationKey })
                if (key.Length > 0 && (string.IsNullOrWhiteSpace(key) || !keys.Add(key)))
                    throw new ArgumentException("character-healing-pending-identity-duplicate");
        }
    }

    private static bool ValidHealth(float health, float maximum) => float.IsFinite(health)
        && float.IsFinite(maximum) && maximum >= 1f && health >= 0f && health <= maximum;
}

public readonly struct CharacterCriticalHealthTransitionFact
{
    public CharacterCriticalHealthTransitionFact(CharacterHealingFact value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (!value.IsProposal && !value.CrossedCritical)
            throw new ArgumentException("critical-health-fact-has-no-transition");
    }
    public CharacterHealingFact Value { get; }
}

public readonly struct CharacterMobilityRecoveredFact
{
    public CharacterMobilityRecoveredFact(CharacterHealingFact value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (!value.IsProposal && !value.Recovered)
            throw new ArgumentException("mobility-recovery-fact-has-no-transition");
    }
    public CharacterHealingFact Value { get; }
}
