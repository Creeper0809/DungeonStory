using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Reads an unpublished candidate's prepared profile and real progression ledger.
/// Gameplay narrative/life registration happens only after that candidate is published.
/// </summary>
public sealed class PreparedCharacterSkillNarrativeContext : ICharacterSkillNarrativeContext, IDisposable
{
    private bool disposed;

    public NarrativePublicContextMaterial BuildSkillMaterialWithRequiredFacts(
        CharacterProgression progression, IEnumerable<string> requiredProjectedFactIds)
    {
        CharacterActor actor = RequireCandidate(progression);
        var projection = new NarrativePublicCharacterProjection(
            AddPreparedFacts,
            value => value.SpeciesTag,
            id => string.Equals(id, actor.Identity.PersistentId, StringComparison.Ordinal)
                ? new NarrativePublicEntityInput(id, NarrativePublicEntityKind.Character, actor.Identity.DisplayName)
                : null);
        return projection.Build(LocalLlmRequestProfiles.CharacterSkill.Id, actor, true, true,
            null, null, includeCurrentNeeds: false, requiredProjectedFactIds);
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetOutcomeEvidence(CharacterProgression progression)
    {
        RequireCandidate(progression);
        // An unpublished candidate has no gameplay run or committed gameplay outcomes.
        // Its selected origin/history evidence comes from its real progression ledger.
        return Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
    }

    public bool TryResolveExactBinding(GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        RequireActive();
        // Importing gameplay outcome receipts into a preparation request is invalid.
        return false;
    }

    public void Dispose() => disposed = true;

    private CharacterActor RequireCandidate(CharacterProgression progression)
    {
        RequireActive();
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        CharacterActor actor = progression.Actor;
        if (actor == null || !actor.IsUnpublishedComposition
            || progression.GrowthState.startingProfile?.prepared != true)
            throw new InvalidOperationException("Preparation skill context requires an unpublished prepared candidate.");
        return actor;
    }

    private void RequireActive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PreparedCharacterSkillNarrativeContext));
    }

    private static void AddPreparedFacts(
        ICollection<NarrativePublicFactInput> facts, CharacterActor actor, string subjectId)
    {
        double years = actor.Progression.GrowthState.startingProfile.biologicalAgeYears;
        if (double.IsNaN(years) || double.IsInfinity(years) || years < 0)
            throw new InvalidOperationException("Prepared candidate biological age must be finite and nonnegative.");
        NarrativePublicCharacterProjection.AddPublicFact(facts, "Life", "prepared-biological-age", subjectId,
            "Prepared biological age: " + years.ToString("0.0", CultureInfo.InvariantCulture) + " years",
            86, NarrativePublicFactCategory.Background);
    }
}
