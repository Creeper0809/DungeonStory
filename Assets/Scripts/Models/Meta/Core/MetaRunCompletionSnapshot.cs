using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

// Frozen settlement inputs, owned by the meta runtime, not the run or log observer.
public sealed class MetaRunCompletionSnapshot
{
    private readonly OwnerRunCompletionFactData fact;
    public MetaRunCompletionSnapshot(OwnerRunCompletionFactData fact, int runSequence,
        RunResultSnapshot result, IEnumerable<string> recipeCandidates, int preservedRecipeSlots)
    {
        if (fact == null || result == null || recipeCandidates == null)
            throw new InvalidOperationException("meta-completion-snapshot-missing");
        fact.Validate();
        if (runSequence < 1 || preservedRecipeSlots < 0 || result.ownerName != fact.displayName || result.endReason != fact.reason
            || result.outcome != fact.outcome || result.survivedOperatingDays != fact.absoluteDay
            || !float.IsFinite(result.survivalSeconds)
            || result.legacyCurrency != MetaProgressionCalculator.CalculateLegacyCurrency(result))
            throw new InvalidOperationException("meta-completion-snapshot-conflict");
        string[] recipes = recipeCandidates.ToArray();
        if (recipes.Any(id => string.IsNullOrWhiteSpace(id) || id != id.Trim())
            || recipes.Distinct(StringComparer.Ordinal).Count() != recipes.Length
            || result.firstUnlockedRecipeCount != recipes.Length)
            throw new InvalidOperationException("meta-completion-recipes-conflict");
        this.fact = fact.Clone();
        RunSequence = runSequence;
        Result = result;
        PreservedRecipeSlots = preservedRecipeSlots;
        RecipeCandidates = ReadOnlyView.List(recipes.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
    public OwnerRunCompletionFactData Fact => fact.Clone();
    public int RunSequence { get; }
    public RunResultSnapshot Result { get; }
    public int PreservedRecipeSlots { get; }
    public IReadOnlyList<string> RecipeCandidates { get; }

    public bool Matches(string ownerName, string reason, DungeonRunOutcome outcome) =>
        fact.displayName == ownerName && fact.reason == reason && fact.outcome == outcome;
}
