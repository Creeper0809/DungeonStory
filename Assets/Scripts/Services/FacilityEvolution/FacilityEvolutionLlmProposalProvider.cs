using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class FacilityEvolutionProposalReasonDto
{
    public string id;
    public string reason;
}

[Serializable]
public sealed class FacilityEvolutionProposalJsonDto : ILlmJsonPayload
{
    public string facilityIdentitySummary;
    public string[] proposalIds;
    public FacilityEvolutionProposalReasonDto[] reasons;
    public FacilityEvolutionProposalReasonDto[] rejectedHints;
    public string rejectedHintText;
    public string[] mutationTagSuggestions;
    public string flavorText;
    public float confidence;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(facilityIdentitySummary))
        {
            error = "facilityIdentitySummary is required.";
            return false;
        }

        if (facilityIdentitySummary.Length > 160)
        {
            error = "facilityIdentitySummary must be 160 characters or shorter.";
            return false;
        }

        if (flavorText != null && flavorText.Length > 260)
        {
            error = "flavorText must be 260 characters or shorter.";
            return false;
        }

        if (rejectedHintText != null && rejectedHintText.Length > 220)
        {
            error = "rejectedHintText must be 220 characters or shorter.";
            return false;
        }

        if (confidence < 0f || confidence > 1f)
        {
            error = "confidence must be between 0 and 1.";
            return false;
        }

        if (!ValidateReasonArray(reasons, "reasons", out error))
        {
            return false;
        }

        if (!ValidateReasonArray(rejectedHints, "rejectedHints", out error))
        {
            return false;
        }

        return true;
    }

    private static bool ValidateReasonArray(
        FacilityEvolutionProposalReasonDto[] entries,
        string label,
        out string error)
    {
        error = string.Empty;
        if (entries != null)
        {
            foreach (FacilityEvolutionProposalReasonDto reason in entries)
            {
                if (reason == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(reason.id))
                {
                    error = $"{label}.id is required.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(reason.reason) && reason.reason.Length > 220)
                {
                    error = $"{label}.reason must be 220 characters or shorter.";
                    return false;
                }
            }
        }

        return true;
    }

    public FacilityEvolutionProposal ToRuntimeProposal(
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> validMutationTags,
        out string statusMessage)
    {
        HashSet<string> validIds = new HashSet<string>(
            validCandidateIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<string> validMutations = new HashSet<string>(
            validMutationTags ?? Array.Empty<string>(),
            StringComparer.Ordinal);

        List<string> filteredProposalIds = (proposalIds ?? Array.Empty<string>())
            .Where((id) => !string.IsNullOrWhiteSpace(id) && validIds.Contains(id))
            .Distinct()
            .ToList();
        Dictionary<string, string> filteredReasons = new Dictionary<string, string>();
        foreach (FacilityEvolutionProposalReasonDto entry in reasons ?? Array.Empty<FacilityEvolutionProposalReasonDto>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !validIds.Contains(entry.id))
            {
                continue;
            }

            filteredReasons[entry.id] = entry.reason ?? string.Empty;
        }

        Dictionary<string, string> filteredHints = new Dictionary<string, string>();
        foreach (FacilityEvolutionProposalReasonDto entry in rejectedHints ?? Array.Empty<FacilityEvolutionProposalReasonDto>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !validIds.Contains(entry.id))
            {
                continue;
            }

            filteredHints[entry.id] = entry.reason ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(rejectedHintText))
        {
            foreach (string id in validIds)
            {
                if (!filteredHints.ContainsKey(id))
                {
                    filteredHints[id] = rejectedHintText;
                }
            }
        }

        List<string> filteredMutationTags = (mutationTagSuggestions ?? Array.Empty<string>())
            .Where((tag) => !string.IsNullOrWhiteSpace(tag)
                && (validMutations.Count == 0 || validMutations.Contains(tag)))
            .Distinct()
            .ToList();

        int droppedProposalCount = (proposalIds ?? Array.Empty<string>())
            .Count((id) => !string.IsNullOrWhiteSpace(id) && !validIds.Contains(id));
        int droppedMutationCount = (mutationTagSuggestions ?? Array.Empty<string>())
            .Count((tag) => !string.IsNullOrWhiteSpace(tag)
                && validMutations.Count > 0
                && !validMutations.Contains(tag));

        statusMessage = droppedProposalCount == 0 && droppedMutationCount == 0
            ? "LLM proposal accepted."
            : $"LLM proposal accepted with filtered ids={droppedProposalCount}, mutations={droppedMutationCount}.";

        return new FacilityEvolutionProposal(
            facilityIdentitySummary,
            filteredProposalIds,
            filteredReasons,
            filteredMutationTags,
            flavorText,
            confidence,
            FacilityEvolutionProposalSources.LocalLlm,
            statusMessage,
            filteredHints);
    }
}

public sealed class CachedLocalLlmFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider
{
    private readonly IFacilityEvolutionProposalProvider fallbackProvider;
    private readonly Func<ILocalLlmRuntime> llmRuntimeProvider;
    private readonly bool allowRequestsOutsidePlayMode;
    private readonly Dictionary<string, FacilityEvolutionProposal> cachedProposals =
        new Dictionary<string, FacilityEvolutionProposal>();
    private readonly HashSet<string> pendingSignatures = new HashSet<string>();
    private readonly Dictionary<string, string> statusBySignature = new Dictionary<string, string>();

    public CachedLocalLlmFacilityEvolutionProposalProvider(
        IFacilityEvolutionProposalProvider fallbackProvider = null,
        Func<ILocalLlmRuntime> llmRuntimeProvider = null,
        bool allowRequestsOutsidePlayMode = false)
    {
        this.fallbackProvider = fallbackProvider ?? new RuleBasedFacilityEvolutionProposalProvider();
        this.llmRuntimeProvider = llmRuntimeProvider;
        this.allowRequestsOutsidePlayMode = allowRequestsOutsidePlayMode;
    }

    public string LastPrompt { get; private set; } = string.Empty;
    public string LastResponse { get; private set; } = string.Empty;
    public string LastStatusMessage { get; private set; } = string.Empty;

    public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)
    {
        FacilityEvolutionProposal fallback = fallbackProvider.Propose(context);
        if (context == null || context.Facility == null || context.CandidateRecipes == null)
        {
            return fallback;
        }

        string signature = FacilityEvolutionPromptFormatter.BuildSignature(context);
        if (cachedProposals.TryGetValue(signature, out FacilityEvolutionProposal cached))
        {
            return cached;
        }

        if (!pendingSignatures.Contains(signature))
        {
            TryRequestProposal(signature, context);
        }

        string status = statusBySignature.TryGetValue(signature, out string message)
            ? message
            : "LLM proposal pending.";
        string source = status.StartsWith("LLM failed", StringComparison.Ordinal)
            || status.StartsWith("LLM unavailable", StringComparison.Ordinal)
            || status.StartsWith("LLM disabled", StringComparison.Ordinal)
                ? FacilityEvolutionProposalSources.RuleBasedAfterLlmFailure
                : FacilityEvolutionProposalSources.RuleBasedWhileLlmPending;
        return Rewrap(fallback, source, status);
    }

    private void TryRequestProposal(string signature, FacilityEvolutionContext context)
    {
        if (!allowRequestsOutsidePlayMode && !Application.isPlaying)
        {
            SetStatus(signature, "LLM disabled outside play mode.");
            return;
        }

        ILocalLlmRuntime runtime = llmRuntimeProvider?.Invoke();
        if (runtime == null)
        {
            SetStatus(signature, "LLM unavailable: LocalLlmRequestQueue is missing.");
            return;
        }

        string prompt = FacilityEvolutionPromptFormatter.BuildPrompt(context);
        LastPrompt = prompt;
        string[] validCandidateIds = context.CandidateRecipes
            .Where((recipe) => recipe != null)
            .Select((recipe) => recipe.EffectiveId)
            .ToArray();
        string[] validMutationTags = context.CandidateRecipes
            .Where((recipe) => recipe?.allowedMutationTags != null)
            .SelectMany((recipe) => recipe.allowedMutationTags)
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray();

        pendingSignatures.Add(signature);
        SetStatus(signature, "LLM proposal requested.");
        bool accepted = runtime.GenerateFacilityEvolutionAsync(prompt, (result) =>
            OnLlmResult(signature, result, validCandidateIds, validMutationTags));
        if (!accepted)
        {
            pendingSignatures.Remove(signature);
            SetStatus(signature, "LLM failed: request was not accepted.");
        }
    }

    private void OnLlmResult(
        string signature,
        LocalLlmResult result,
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> validMutationTags)
    {
        pendingSignatures.Remove(signature);
        LastResponse = result.Content;
        if (result.IsCancelled)
        {
            SetStatus(signature, "LLM request cancelled.");
            return;
        }

        if (!result.IsSuccess)
        {
            SetStatus(signature, $"LLM failed: {result.Status} {result.Error}");
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out FacilityEvolutionProposalJsonDto dto, out string parseError))
        {
            SetStatus(signature, $"LLM failed: {parseError}");
            return;
        }

        FacilityEvolutionProposal proposal = dto.ToRuntimeProposal(
            validCandidateIds,
            validMutationTags,
            out string statusMessage);
        cachedProposals[signature] = proposal;
        SetStatus(signature, statusMessage);
    }

    private void SetStatus(string signature, string message)
    {
        string safeMessage = message ?? string.Empty;
        statusBySignature[signature] = safeMessage;
        LastStatusMessage = safeMessage;
    }

    private static FacilityEvolutionProposal Rewrap(
        FacilityEvolutionProposal proposal,
        string source,
        string status)
    {
        return new FacilityEvolutionProposal(
            proposal.FacilityIdentitySummary,
            proposal.ProposalIds,
            proposal.ProposalReasons,
            proposal.MutationTagSuggestions,
            proposal.FlavorText,
            proposal.Confidence,
            source,
            status,
            proposal.RejectedHintTexts);
    }
}

public static class FacilityEvolutionPromptFormatter
{
    public static string BuildSignature(FacilityEvolutionContext context)
    {
        if (context == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(FacilityEvolutionUtility.GetFacilityId(context.Facility != null ? context.Facility.BuildingData : null));
        builder.Append('|').Append(context.State != null ? context.State.StarGrade : 1);
        AppendList(builder, context.State != null ? context.State.LineageTags : Array.Empty<string>());
        AppendList(builder, context.CandidateRecipes?.Select((recipe) => recipe != null ? recipe.EffectiveId : string.Empty));
        AppendPairs(builder, context.Profile != null ? context.Profile.Scores : null);
        AppendPairs(builder, context.Profile != null ? context.Profile.Metrics : null);
        AppendPairs(builder, context.Profile != null ? context.Profile.IdentityPressures : null);
        AppendTokenPairs(builder, context.Profile != null ? context.Profile.RecordTokens : null);
        return builder.ToString();
    }

    public static string BuildPrompt(FacilityEvolutionContext context)
    {
        RoomProfile profile = context.Profile;
        FacilityEvolutionStateComponent state = context.State;
        FacilityIdentitySnapshot snapshot = new FacilityIdentitySnapshot(context);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are the narrative interpreter for a dungeon management game's facility lineage evolution.");
        builder.AppendLine("Game code already selected the candidate pool and will validate every hard condition.");
        builder.AppendLine("Do not invent candidate ids, facilities, costs, stats, or balance values.");
        builder.AppendLine("Use the identity pressures and dominant/conflicting signals as the main context summary.");
        builder.AppendLine("Return exactly this JSON shape:");
        builder.AppendLine("{\"facilityIdentitySummary\":\"...\",\"proposalIds\":[\"candidate_id\"],\"reasons\":[{\"id\":\"candidate_id\",\"reason\":\"...\"}],\"rejectedHints\":[{\"id\":\"candidate_id\",\"reason\":\"...\"}],\"mutationTagSuggestions\":[\"tag\"],\"flavorText\":\"...\",\"confidence\":0.0}");
        builder.AppendLine();
        builder.AppendLine("Facility:");
        builder.AppendLine($"name={FacilityShopService.GetBuildingName(context.Facility != null ? context.Facility.BuildingData : null)}");
        builder.AppendLine($"currentFacilityId={FacilityEvolutionUtility.GetFacilityId(context.Facility != null ? context.Facility.BuildingData : null)}");
        builder.AppendLine($"starGrade={(state != null ? state.StarGrade : 1)}");
        builder.AppendLine($"lineageTags={JoinValues(state != null ? state.LineageTags : Array.Empty<string>())}");
        builder.AppendLine($"mutationTags={JoinValues(state != null ? state.MutationTags : Array.Empty<string>())}");
        builder.AppendLine();
        builder.AppendLine("Room profile:");
        builder.AppendLine($"usable={(profile != null && profile.IsUsable)} closed={(profile != null && profile.IsClosed)} hasDoor={(profile != null && profile.HasDoor)} area={(profile != null ? profile.Area : 0)}");
        builder.AppendLine($"tags={JoinValues(profile != null ? profile.Tags : Array.Empty<string>())}");
        builder.AppendLine($"scores={JoinPairs(profile != null ? profile.Scores : null)}");
        builder.AppendLine($"metrics={JoinPairs(profile != null ? profile.Metrics : null)}");
        builder.AppendLine($"recordTokens={JoinTokenPairs(profile != null ? profile.RecordTokens : null)}");
        builder.AppendLine($"identityPressures={JoinPairs(snapshot.IdentityPressures)}");
        builder.AppendLine($"dominantSignals={JoinValues(snapshot.DominantSignals)}");
        builder.AppendLine($"conflictingSignals={JoinValues(snapshot.ConflictingSignals)}");
        builder.AppendLine($"recentEvents={JoinValues(profile != null ? profile.RecentEvents.Take(6) : Array.Empty<string>())}");
        builder.AppendLine();
        builder.AppendLine("Candidate pool:");
        foreach (FacilityEvolutionRecipeSO recipe in context.CandidateRecipes ?? Array.Empty<FacilityEvolutionRecipeSO>())
        {
            if (recipe == null)
            {
                continue;
            }

            builder.AppendLine($"- id={recipe.EffectiveId}");
            builder.AppendLine($"  name={recipe.DisplayName}");
            builder.AppendLine($"  result={FacilityShopService.GetBuildingName(recipe.resultBuilding)}");
            builder.AppendLine($"  requiredScores={JoinRequirements(recipe.requiredRoomScores)}");
            builder.AppendLine($"  requiredMetrics={JoinRequirements(recipe.requiredRoomMetrics)}");
            builder.AppendLine($"  requiredTokens={JoinTokenRequirements(recipe.requiredRecordTokens)}");
            builder.AppendLine($"  identityWeights={JoinIdentityWeights(recipe.identityPressureWeights)}");
            builder.AppendLine($"  minimumIdentityScore={recipe.minimumIdentityScore:0.##}");
            builder.AppendLine($"  allowedMutationTags={JoinValues(recipe.allowedMutationTags)}");
        }

        builder.AppendLine();
        builder.AppendLine("Choose proposalIds only from the candidate pool. Reasons should explain the current context, not restate raw numbers only.");
        return builder.ToString();
    }

    private static void AppendPairs(StringBuilder builder, IReadOnlyDictionary<string, float> values)
    {
        if (values == null)
        {
            return;
        }

        foreach (KeyValuePair<string, float> pair in values.OrderBy((entry) => entry.Key))
        {
            builder.Append('|').Append(pair.Key).Append('=').Append(pair.Value.ToString("0.###"));
        }
    }

    private static void AppendTokenPairs(StringBuilder builder, IReadOnlyDictionary<string, int> values)
    {
        if (values == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> pair in values.OrderBy((entry) => entry.Key))
        {
            builder.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
        }
    }

    private static void AppendList(StringBuilder builder, IEnumerable<string> values)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append('|').Append(value);
            }
        }
    }

    private static string JoinValues(IEnumerable<string> values)
    {
        string[] filtered = values?
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .Take(16)
            .ToArray()
            ?? Array.Empty<string>();
        return filtered.Length > 0 ? string.Join(", ", filtered) : "none";
    }

    private static string JoinPairs(IReadOnlyDictionary<string, float> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", values
            .OrderByDescending((entry) => Mathf.Abs(entry.Value))
            .Take(18)
            .Select((entry) => $"{entry.Key}:{entry.Value:0.##}"));
    }

    private static string JoinTokenPairs(IReadOnlyDictionary<string, int> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", values
            .OrderByDescending((entry) => entry.Value)
            .Take(18)
            .Select((entry) => $"{entry.Key}:{entry.Value}"));
    }

    private static string JoinRequirements(FacilityEvolutionMetricRequirement[] requirements)
    {
        if (requirements == null || requirements.Length == 0)
        {
            return "none";
        }

        return string.Join(", ", requirements
            .Where((requirement) => !string.IsNullOrWhiteSpace(requirement.key))
            .Select((requirement) =>
            {
                List<string> clauses = new List<string>();
                if (requirement.requireMin) clauses.Add($">={requirement.minValue:0.##}");
                if (requirement.requireMax) clauses.Add($"<={requirement.maxValue:0.##}");
                return $"{requirement.key}{string.Join("/", clauses)}";
            }));
    }

    private static string JoinTokenRequirements(FacilityEvolutionTokenRequirement[] requirements)
    {
        if (requirements == null || requirements.Length == 0)
        {
            return "none";
        }

        return string.Join(", ", requirements
            .Where((requirement) => !string.IsNullOrWhiteSpace(requirement.key))
            .Select((requirement) => $"{requirement.key}>={Mathf.Max(1, requirement.minCount)}"));
    }

    private static string JoinIdentityWeights(FacilityEvolutionValue[] weights)
    {
        if (weights == null || weights.Length == 0)
        {
            return "none";
        }

        return string.Join(", ", weights
            .Where((weight) => !string.IsNullOrWhiteSpace(weight.key)
                && !Mathf.Approximately(weight.value, 0f))
            .Select((weight) => $"{weight.key}:{weight.value:0.##}"));
    }
}
