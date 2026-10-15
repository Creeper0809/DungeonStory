using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable fatal health transition. Observation identities and presentation are not domain facts.</summary>
public sealed class CharacterFatalityFact
{
    internal const int MaximumPending = 512;
    private readonly CharacterFatalityFactData data;
    public CharacterFatalityFact(CharacterFatalityFactData value, bool proposalOnly = false)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        var id = new CharacterId(value.characterId);
        if (!id.IsValid || id.Value != value.characterId
            || !DomainFactValueSyntax.IsText(value.characterName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(value.reasonCode, 1024, true)
            || value.absoluteDay < 1 || !Enum.IsDefined(typeof(CharacterDeathCauseCode), value.cause)
            || !float.IsFinite(value.maximumHealth) || value.maximumHealth < 1f
            || !float.IsFinite(value.healthBefore) || value.healthBefore <= 0f || value.healthBefore > value.maximumHealth
            || !float.IsFinite(value.healthAfter) || value.healthAfter < 0f || value.healthAfter > value.maximumHealth
            || !proposalOnly && value.healthAfter != 0f)
            throw new ArgumentException("character-fatality-fact-invalid", nameof(value));
        data = value.Clone();
        IsProposal = proposalOnly;
    }
    public bool IsProposal { get; }
    public CharacterFatalityFactData Data => data.Clone();
    public float RatioBefore => data.healthBefore / data.maximumHealth;
    public bool CrossedCritical => RatioBefore > 0.20f;
    public bool BecameDowned => !data.downedBefore && data.downedAfter;
    public CharacterFatalityFactData Snapshot() => !IsProposal ? data.Clone()
        : throw new InvalidOperationException("A fatality proposal is not an applied fact.");

    internal static void ValidatePending(IReadOnlyList<CharacterFatalityPendingFactData> pending)
    {
        if (pending == null || pending.Count > MaximumPending)
            throw new ArgumentException("character-fatality-pending-list-invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pending)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.deathPublicationKey)
                || entry.criticalPublicationKey == null || entry.downedPublicationKey == null)
                throw new ArgumentException("character-fatality-pending-entry-invalid");
            var fact = new CharacterFatalityFact(entry.fact);
            if (!owners.Add(entry.fact.characterId))
                throw new ArgumentException("character-fatality-pending-owner-duplicate");
            if (fact.CrossedCritical != (entry.criticalPublicationKey.Length > 0)
                || fact.BecameDowned != (entry.downedPublicationKey.Length > 0))
                throw new ArgumentException("character-fatality-pending-transition-conflict");
            foreach (string key in new[] { entry.deathPublicationKey, entry.criticalPublicationKey, entry.downedPublicationKey })
                if (key.Length > 0 && (string.IsNullOrWhiteSpace(key) || !keys.Add(key)))
                    throw new ArgumentException("character-fatality-pending-identity-duplicate");
        }
    }
}

public readonly struct CharacterFatalCriticalTransitionFact
{
    public CharacterFatalCriticalTransitionFact(CharacterFatalityFact value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (!value.IsProposal && !value.CrossedCritical)
            throw new ArgumentException("fatal-critical-fact-has-no-transition");
    }
    public CharacterFatalityFact Value { get; }
}

public readonly struct CharacterFatalDownedFact
{
    public CharacterFatalDownedFact(CharacterFatalityFact value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (!value.IsProposal && !value.BecameDowned)
            throw new ArgumentException("fatal-downed-fact-has-no-transition");
    }
    public CharacterFatalityFact Value { get; }
}
