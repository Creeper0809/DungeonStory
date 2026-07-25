using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct FacilityEvolutionMutationResult
{
    public FacilityEvolutionMutationResult(
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, string> reasons)
    {
        Tags = tags ?? Array.Empty<string>();
        Reasons = reasons ?? new Dictionary<string, string>();
    }

    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyDictionary<string, string> Reasons { get; }
}

public interface IFacilityEvolutionMutationResolver
{
    FacilityEvolutionMutationResult Resolve(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal);
}

public sealed class DefaultFacilityEvolutionMutationResolver : IFacilityEvolutionMutationResolver
{
    private const int MaxMutationTagsPerEvolution = 2;

    public FacilityEvolutionMutationResult Resolve(
        FacilityEvolutionContext context,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal)
    {
        if (context == null || recipe == null || recipe.allowedMutationTags == null)
        {
            return new FacilityEvolutionMutationResult(Array.Empty<string>(), null);
        }

        HashSet<string> allowed = new HashSet<string>(
            recipe.allowedMutationTags.Where((tag) => !string.IsNullOrWhiteSpace(tag)),
            StringComparer.Ordinal);
        if (allowed.Count == 0)
        {
            return new FacilityEvolutionMutationResult(Array.Empty<string>(), null);
        }

        RoomProfile profile = context.Profile;
        FacilityEvolutionIdentityScore identityScore =
            FacilityIdentityPressureUtility.ScoreRecipe(profile, recipe);
        Dictionary<string, string> reasons = new Dictionary<string, string>();
        List<string> ordered = new List<string>();

        foreach (string tag in proposal.MutationTagSuggestions ?? Array.Empty<string>())
        {
            TryAddMutation(tag, allowed, profile, reasons, ordered, "제안과 현재 기록이 일치");
        }

        foreach (string tag in allowed
            .OrderByDescending((tag) => GetTagEvidenceScore(profile, tag))
            .ThenBy((tag) => tag))
        {
            if (ordered.Count >= MaxMutationTagsPerEvolution)
            {
                break;
            }

            float pressure = profile != null ? profile.GetIdentityPressure(tag) : 0f;
            float evidence = GetTagEvidenceScore(profile, tag);
            bool overachieved = identityScore.UsesIdentityPressure
                && identityScore.Score >= Mathf.Clamp01(identityScore.MinimumScore + 0.2f)
                && evidence >= 0.4f;
            bool stronglyTyped = pressure >= 0.5f || evidence >= 0.65f;
            if (overachieved || stronglyTyped)
            {
                TryAddMutation(tag, allowed, profile, reasons, ordered, "정체성 과달성");
            }
        }

        return new FacilityEvolutionMutationResult(ordered, reasons);
    }

    private static void TryAddMutation(
        string tag,
        HashSet<string> allowed,
        RoomProfile profile,
        Dictionary<string, string> reasons,
        List<string> ordered,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(tag)
            || !allowed.Contains(tag)
            || ordered.Contains(tag)
            || ordered.Count >= MaxMutationTagsPerEvolution
            || !HasEvidence(profile, tag))
        {
            return;
        }

        ordered.Add(tag);
        reasons[tag] = reason;
    }

    private static bool HasEvidence(RoomProfile profile, string tag)
    {
        return GetTagEvidenceScore(profile, tag) >= 0.35f;
    }

    private static float GetTagEvidenceScore(RoomProfile profile, string tag)
    {
        if (profile == null || string.IsNullOrWhiteSpace(tag))
        {
            return 0f;
        }

        float pressure = profile.GetIdentityPressure(tag);
        float score = Mathf.Clamp01(profile.GetScore(tag) / 40f);
        float token = profile.GetToken(tag) > 0 ? 0.6f : 0f;
        float generic = Mathf.Max(pressure, score, token);

        if (string.Equals(tag, FacilityEvolutionTerms.Combat, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.MercenaryHangout) >= 2 ? 0.75f : 0f,
                profile.GetScore(FacilityEvolutionTerms.Combat) >= 24f ? 0.7f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Brutal, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.FrequentBrawls) > 0 ? 0.75f : 0f,
                profile.GetToken(FacilityEvolutionTerms.IntruderBloodied) > 0 ? 0.7f : 0f,
                profile.GetMetric(FacilityEvolutionTerms.BrawlCount) > 0f ? 0.65f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Luxury, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) >= 2f ? 0.75f : 0f,
                profile.GetIdentityPressure(FacilityEvolutionTerms.Luxury));
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Noble, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.NoblePatronage) >= 2 ? 0.8f : 0f,
                profile.GetMetric(FacilityEvolutionTerms.NobleVisitorRatio) >= 0.5f ? 0.75f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Security, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.GuardRallyPoint) > 0 ? 0.75f : 0f,
                profile.GetToken(FacilityEvolutionTerms.IntruderBloodied) > 0 ? 0.75f : 0f,
                profile.GetScore(FacilityEvolutionTerms.Defense) >= 20f ? 0.65f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Outlaw, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.OutlawRumor) > 0 ? 0.75f : 0f,
                profile.GetMetric(FacilityEvolutionTerms.CrimeCount) > 0f ? 0.65f : 0f,
                profile.GetMetric(FacilityEvolutionTerms.TheftCount) > 0f ? 0.65f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Service, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.CleanServiceStreak) > 0 ? 0.75f : 0f,
                profile.GetMetric(FacilityEvolutionTerms.ServiceQuality) >= 0.7f ? 0.65f : 0f);
        }

        if (string.Equals(tag, FacilityEvolutionTerms.Crowd, StringComparison.Ordinal))
        {
            return Mathf.Max(
                generic,
                profile.GetToken(FacilityEvolutionTerms.HighTurnoverService) > 0 ? 0.75f : 0f,
                profile.GetIdentityPressure(FacilityEvolutionTerms.Crowd));
        }

        return generic;
    }
}
