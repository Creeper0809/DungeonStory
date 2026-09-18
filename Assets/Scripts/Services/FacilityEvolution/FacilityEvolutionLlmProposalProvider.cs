using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class FacilityEvolutionProposalReasonDto
{
    public string proposalId;
    public string reason;
}

[Serializable]
public sealed class FacilityEvolutionProposalJsonDto : ILlmJsonPayload
{
    public string[] proposalIds;
    public FacilityEvolutionProposalReasonDto[] reasons;
    public string[] mutationTags;
    public string flavorText;
    public float confidence;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (proposalIds == null || proposalIds.Length == 0)
        {
            error = "proposalIds must contain at least one proposal.";
            return false;
        }

        if (proposalIds.Any(string.IsNullOrWhiteSpace))
        {
            error = "proposalIds cannot contain a blank ID.";
            return false;
        }

        if (proposalIds.Distinct(StringComparer.Ordinal).Count() != proposalIds.Length)
        {
            error = "proposalIds cannot contain duplicate IDs.";
            return false;
        }

        if (mutationTags == null)
        {
            error = "mutationTags is required.";
            return false;
        }

        if (mutationTags.Any(string.IsNullOrWhiteSpace))
        {
            error = "mutationTags cannot contain blank tags.";
            return false;
        }

        if (mutationTags.Distinct(StringComparer.Ordinal).Count() != mutationTags.Length)
        {
            error = "mutationTags cannot contain duplicate tags.";
            return false;
        }

        if (reasons == null || reasons.Length != proposalIds.Length)
        {
            error = "reasons must match proposalIds count and order.";
            return false;
        }

        for (int index = 0; index < reasons.Length; index++)
        {
            FacilityEvolutionProposalReasonDto entry = reasons[index];
            if (entry == null
                || !string.Equals(
                    entry.proposalId,
                    proposalIds[index],
                    StringComparison.Ordinal))
            {
                error = "reasons must match proposalIds count and order.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.reason)
                || entry.reason.Length > 220)
            {
                error = "reasons.reason must contain 1-220 characters.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(flavorText) || flavorText.Length > 260)
        {
            error = "flavorText must contain 1-260 characters.";
            return false;
        }

        if (float.IsNaN(confidence)
            || float.IsInfinity(confidence)
            || confidence < 0f
            || confidence > 1f)
        {
            error = "confidence must be between 0 and 1.";
            return false;
        }

        return true;
    }

    public bool TryCreateRuntimeProposal(
        string facilityIdentitySummary,
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> validMutationTags,
        IReadOnlyDictionary<string, string> legalTailReasons,
        IReadOnlyDictionary<string, string> rejectedHintTexts,
        NarrativeGenerationTrace narrativeTrace,
        out FacilityEvolutionProposal proposal,
        out FacilityEvolutionProposalRejectionKind rejectionKind,
        out string error)
    {
        proposal = default;
        rejectionKind = FacilityEvolutionProposalRejectionKind.None;
        error = string.Empty;
        if (!Validate(out error))
        {
            rejectionKind = ResolvePayloadRejection(error);
            return false;
        }

        HashSet<string> validIds = new HashSet<string>(
            validCandidateIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<string> validMutations = new HashSet<string>(
            validMutationTags ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        string illegalProposalId = proposalIds.FirstOrDefault(id => !validIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(illegalProposalId))
        {
            rejectionKind = FacilityEvolutionProposalRejectionKind.IllegalProposalId;
            error = $"proposalIds contains an ID outside the legal candidate packet: {illegalProposalId}.";
            return false;
        }

        string illegalMutationTag = mutationTags.FirstOrDefault(tag => !validMutations.Contains(tag));
        if (!string.IsNullOrWhiteSpace(illegalMutationTag))
        {
            rejectionKind = FacilityEvolutionProposalRejectionKind.IllegalMutationTag;
            error = $"mutationTags contains a tag outside the legal candidate packet: {illegalMutationTag}.";
            return false;
        }

        Dictionary<string, string> proposalReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> selectedIds = new HashSet<string>(proposalIds, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in legalTailReasons
                     ?? new Dictionary<string, string>())
        {
            if (validIds.Contains(entry.Key) && !selectedIds.Contains(entry.Key))
            {
                proposalReasons.Add(entry.Key, entry.Value ?? string.Empty);
            }
        }
        foreach (FacilityEvolutionProposalReasonDto entry in reasons)
        {
            proposalReasons.Add(entry.proposalId, entry.reason.Trim());
        }

        proposal = new FacilityEvolutionProposal(
            facilityIdentitySummary,
            proposalIds,
            proposalReasons,
            mutationTags,
            flavorText.Trim(),
            confidence,
            FacilityEvolutionProposalSources.LocalLlm,
            "LLM proposal accepted without repair or filtering.",
            rejectedHintTexts,
            narrativeTrace);
        return true;
    }

    private static FacilityEvolutionProposalRejectionKind ResolvePayloadRejection(
        string error)
    {
        if (error != null && error.StartsWith("proposalIds cannot contain duplicate", StringComparison.Ordinal))
        {
            return FacilityEvolutionProposalRejectionKind.DuplicateProposalId;
        }

        if (error != null && error.StartsWith("mutationTags cannot contain duplicate", StringComparison.Ordinal))
        {
            return FacilityEvolutionProposalRejectionKind.DuplicateMutationTag;
        }

        if (error != null && error.StartsWith("reasons must match", StringComparison.Ordinal))
        {
            return FacilityEvolutionProposalRejectionKind.ReasonCountOrOrderMismatch;
        }

        return FacilityEvolutionProposalRejectionKind.InvalidPayload;
    }
}

public enum FacilityEvolutionProposalRejectionKind
{
    None,
    RuntimeUnavailable,
    RequestRejected,
    Cancelled,
    GenerationFailed,
    ExactKeyContractViolation,
    JsonParseFailed,
    InvalidPayload,
    DuplicateProposalId,
    DuplicateMutationTag,
    ReasonCountOrOrderMismatch,
    IllegalProposalId,
    IllegalMutationTag,
    PublicContextUnavailable
}

public sealed class CachedLocalLlmFacilityEvolutionProposalProvider : IFacilityEvolutionProposalProvider
{
    private readonly IFacilityEvolutionProposalProvider fallbackProvider;
    private readonly Func<ILocalLlmRuntime> llmRuntimeProvider;
    private readonly bool allowRequestsOutsidePlayMode;
    private readonly Dictionary<string, FacilityEvolutionProposal> cachedProposals =
        new Dictionary<string, FacilityEvolutionProposal>();
    private readonly HashSet<string> pendingSignatures = new HashSet<string>();
    private readonly HashSet<string> failedSignatures = new HashSet<string>();
    private readonly Dictionary<string, string> statusBySignature = new Dictionary<string, string>();
    private readonly Dictionary<string, NarrativeInferenceAuditRecord> auditByRequestKey =
        new Dictionary<string, NarrativeInferenceAuditRecord>(StringComparer.Ordinal);

    public CachedLocalLlmFacilityEvolutionProposalProvider(
        IFacilityEvolutionProposalProvider fallbackProvider,
        Func<ILocalLlmRuntime> llmRuntimeProvider,
        bool allowRequestsOutsidePlayMode = false)
    {
        this.fallbackProvider = fallbackProvider
            ?? throw new ArgumentNullException(nameof(fallbackProvider));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.allowRequestsOutsidePlayMode = allowRequestsOutsidePlayMode;
    }

    public string LastPrompt { get; private set; } = string.Empty;
    public string LastResponse { get; private set; } = string.Empty;
    public string LastStatusMessage { get; private set; } = string.Empty;
    public FacilityEvolutionProposalRejectionKind LastRejectionKind { get; private set; }
    public NarrativeInferenceAuditRecord LastAudit { get; private set; }

    public bool TryGetAudit(
        string requestKey,
        out NarrativeInferenceAuditRecord audit)
    {
        return auditByRequestKey.TryGetValue(
            requestKey?.Trim() ?? string.Empty,
            out audit);
    }

    public FacilityEvolutionProposal Propose(FacilityEvolutionContext context)
    {
        FacilityEvolutionProposal fallback = fallbackProvider.Propose(context);
        if (context == null || context.Facility == null || context.CandidateRecipes == null)
        {
            return fallback;
        }

        NarrativePublicPromptEnvelope promptEnvelope;
        try
        {
            promptEnvelope =
                FacilityEvolutionPromptFormatter.BuildPromptEnvelope(context);
        }
        catch (Exception error) when (
            error is InvalidOperationException || error is ArgumentException)
        {
            LastPrompt = string.Empty;
            LastResponse = string.Empty;
            LastAudit = default;
            LastRejectionKind =
                FacilityEvolutionProposalRejectionKind.PublicContextUnavailable;
            string publicContextFailureStatus =
                "LLM public context unavailable: " + error.Message;
            LastStatusMessage = publicContextFailureStatus;
            return Rewrap(
                fallback,
                FacilityEvolutionProposalSources.RuleBasedAfterLlmFailure,
                publicContextFailureStatus);
        }
        string signature = FacilityEvolutionPromptFormatter.BuildSignature(
            context,
            promptEnvelope.PublicContextSemanticHash);
        if (cachedProposals.TryGetValue(signature, out FacilityEvolutionProposal cached))
        {
            return cached;
        }

        if (!pendingSignatures.Contains(signature))
        {
            TryRequestProposal(signature, context, fallback, promptEnvelope);
        }

        string status = statusBySignature.TryGetValue(signature, out string message)
            ? message
            : "LLM proposal pending.";
        string source = failedSignatures.Contains(signature)
            ? FacilityEvolutionProposalSources.RuleBasedAfterLlmFailure
            : FacilityEvolutionProposalSources.RuleBasedWhileLlmPending;
        return Rewrap(fallback, source, status);
    }

    private void TryRequestProposal(
        string signature,
        FacilityEvolutionContext context,
        FacilityEvolutionProposal ruleProposal,
        NarrativePublicPromptEnvelope promptEnvelope)
    {
        string candidatePacketHash = NarrativeInferenceHash.ComputeSha256Utf8(signature);
        string requestKey = NarrativePublicContextIdentity.Bind(
            "facility-evolution:" + candidatePacketHash,
            promptEnvelope.PublicContextSemanticHash);
        string targetPersistentId = promptEnvelope.Material.SubjectId;
        if (!allowRequestsOutsidePlayMode && !Application.isPlaying)
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.RuntimeUnavailable,
                "LLM disabled outside play mode.");
            return;
        }

        ILocalLlmRuntime runtime = llmRuntimeProvider?.Invoke();
        if (runtime == null)
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.RuntimeUnavailable,
                "LLM unavailable: LocalLlmRequestQueue is missing.");
            return;
        }

        LastPrompt = promptEnvelope.Prompt;
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
        bool accepted = runtime.GenerateFacilityEvolutionAsync(
            promptEnvelope.Prompt,
            (result) =>
            OnLlmResult(
                signature,
                result,
                validCandidateIds,
                validMutationTags,
                ruleProposal,
                requestKey,
                candidatePacketHash,
                targetPersistentId));
        if (!accepted)
        {
            pendingSignatures.Remove(signature);
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.RequestRejected,
                "LLM failed: request was not accepted.");
        }
    }

    private void OnLlmResult(
        string signature,
        LocalLlmResult result,
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> validMutationTags,
        FacilityEvolutionProposal ruleProposal,
        string requestKey,
        string candidatePacketHash,
        string targetPersistentId)
    {
        pendingSignatures.Remove(signature);
        LastResponse = result.Content;
        if (result.IsCancelled)
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.Cancelled,
                "LLM request cancelled.");
            return;
        }

        if (!result.IsSuccess)
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.GenerationFailed,
                $"LLM failed: {result.Status} {result.Error}");
            return;
        }

        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
                result.Content,
                out string exactJson,
                out string exactError))
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.ExactKeyContractViolation,
                "LLM failed: " + exactError);
            return;
        }

        if (!LlmJsonResponseParser.TryParse(
                exactJson,
                out FacilityEvolutionProposalJsonDto dto,
                out string parseError))
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                FacilityEvolutionProposalRejectionKind.JsonParseFailed,
                "LLM failed: " + parseError);
            return;
        }

        if (!dto.TryCreateRuntimeProposal(
                ruleProposal.FacilityIdentitySummary,
                validCandidateIds,
                validMutationTags,
                ruleProposal.ProposalReasons,
                ruleProposal.RejectedHintTexts,
                result.NarrativeTrace,
                out FacilityEvolutionProposal proposal,
                out FacilityEvolutionProposalRejectionKind rejectionKind,
                out string validationError))
        {
            RecordFailure(
                signature,
                requestKey,
                candidatePacketHash,
                targetPersistentId,
                ruleProposal,
                rejectionKind,
                "LLM failed: " + validationError);
            return;
        }

        cachedProposals[signature] = proposal;
        failedSignatures.Remove(signature);
        LastRejectionKind = FacilityEvolutionProposalRejectionKind.None;
        LastAudit = new NarrativeInferenceAuditRecord(
            LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
            requestKey,
            candidatePacketHash,
            true,
            string.Empty,
            false,
            string.Empty,
            string.Join("|", proposal.ProposalIds),
            -1,
            targetPersistentId,
            NarrativeInferenceTimestamp.FromUtc(DateTime.UtcNow));
        auditByRequestKey[requestKey] = LastAudit;
        SetStatus(signature, proposal.StatusMessage);
    }

    private void RecordFailure(
        string signature,
        string requestKey,
        string candidatePacketHash,
        string targetPersistentId,
        FacilityEvolutionProposal ruleProposal,
        FacilityEvolutionProposalRejectionKind rejectionKind,
        string message)
    {
        failedSignatures.Add(signature);
        LastRejectionKind = rejectionKind;
        string safeMessage = message ?? string.Empty;
        LastAudit = new NarrativeInferenceAuditRecord(
            LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
            requestKey,
            candidatePacketHash,
            false,
            safeMessage,
            true,
            rejectionKind.ToString(),
            string.Join("|", ruleProposal.ProposalIds ?? Array.Empty<string>()),
            -1,
            targetPersistentId,
            NarrativeInferenceTimestamp.FromUtc(DateTime.UtcNow));
        auditByRequestKey[requestKey] = LastAudit;
        SetStatus(signature, safeMessage);
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
        if (context == null) return string.Empty;
        NarrativePublicContextMaterial material = BuildPublicMaterial(context);
        return BuildSignature(context, material.SemanticHash);
    }

    internal static string BuildSignature(
        FacilityEvolutionContext context,
        string publicContextSemanticHash)
    {
        if (context == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(FacilityEvolutionUtility.GetFacilityId(context.Facility != null ? context.Facility.BuildingData : null));
        builder.Append('|').Append(context.State != null ? context.State.StarGrade : 1);
        AppendList(builder, context.State != null ? context.State.LineageTags : Array.Empty<string>());
        foreach (FacilityEvolutionRecipeSO recipe in context.CandidateRecipes
                     ?? Array.Empty<FacilityEvolutionRecipeSO>())
        {
            if (recipe == null)
            {
                continue;
            }

            builder.Append("|recipe=").Append(recipe.EffectiveId)
                .Append("|result=")
                .Append(FacilityEvolutionUtility.GetFacilityId(recipe.resultBuilding));
            AppendList(
                builder,
                (recipe.allowedMutationTags ?? Array.Empty<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .OrderBy(tag => tag, StringComparer.Ordinal));
        }
        AppendPairs(builder, context.Profile != null ? context.Profile.Scores : null);
        AppendPairs(builder, context.Profile != null ? context.Profile.Metrics : null);
        AppendPairs(builder, context.Profile != null ? context.Profile.IdentityPressures : null);
        AppendTokenPairs(builder, context.Profile != null ? context.Profile.RecordTokens : null);
        builder.Append("|publicContext=")
            .Append(publicContextSemanticHash?.Trim() ?? string.Empty);
        return builder.ToString();
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        FacilityEvolutionContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.Facility == null)
            throw new InvalidOperationException(
                "Facility public context requires a live facility target.");

        BuildingInstanceId persistentId = context.Facility.PersistentInstanceId;
        if (!persistentId.IsValid)
        {
            throw new InvalidOperationException(
                "Facility public context requires a persistent facility ID.");
        }
        string stateSubjectId = context.State?.FacilityPersistentId?.Trim()
            ?? string.Empty;
        if (stateSubjectId.Length > 0
            && !string.Equals(
                stateSubjectId,
                persistentId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Facility public context target does not match its evolution state.");
        }
        string subjectId = persistentId.Value;

        string subjectName = FacilityShopService.GetBuildingName(
            context.Facility.BuildingData)?.Trim() ?? string.Empty;
        if (subjectName.Length == 0)
            throw new InvalidOperationException(
                "Facility public context requires a public facility name.");

        string[] recentEvents = (context.Profile?.RecentEvents
                ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        List<NarrativePublicFactInput> facts =
            new List<NarrativePublicFactInput>(recentEvents.Length);
        Dictionary<string, int> occurrenceByHash =
            new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < recentEvents.Length; index++)
        {
            string eventText = recentEvents[index];
            string eventHash = NarrativeInferenceHash.ComputeSha256Utf8(eventText);
            occurrenceByHash.TryGetValue(eventHash, out int occurrence);
            occurrence++;
            occurrenceByHash[eventHash] = occurrence;
            string sourceEventId = "facility-record-event:"
                + eventHash
                + ":occurrence:"
                + occurrence.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            facts.Add(new NarrativePublicFactInput(
                "facility-record-event",
                sourceEventId,
                subjectId,
                "Facility history event [evidence="
                    + sourceEventId
                    + "]: "
                    + eventText,
                100 + index,
                NarrativePublicFactCategory.PriorHistory,
                subjectId,
                string.Empty,
                null,
                1,
                null,
                null,
                new NarrativePublicEventInput(
                    sourceEventId,
                    string.Empty,
                    subjectId,
                    "facility-record-event",
                    string.Empty,
                    null,
                    1,
                    null)));
        }

        return NarrativePublicContextFactory.Build(
            LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
            subjectId,
            NarrativePublicSubjectKind.Facility,
            string.Empty,
            false,
            true,
            new[]
            {
                new NarrativePublicEntityInput(
                    subjectId,
                    NarrativePublicEntityKind.Facility,
                    subjectName)
            },
            facts,
            "facility-recent-events-v1");
    }

    public static NarrativePublicPromptEnvelope BuildPromptEnvelope(
        FacilityEvolutionContext context)
    {
        NarrativePublicContextMaterial material = BuildPublicMaterial(context);
        RoomProfile profile = context.Profile;
        FacilityEvolutionStateComponent state = context.State;
        FacilityIdentitySnapshot snapshot = new FacilityIdentitySnapshot(context);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are the narrative interpreter for a dungeon management game's facility lineage evolution.");
        builder.AppendLine("Game code already selected the candidate pool and will validate every hard condition.");
        builder.AppendLine("Do not invent candidate ids, facilities, costs, stats, or balance values.");
        builder.AppendLine("Use the identity pressures and dominant/conflicting signals as the main context summary.");
        builder.AppendLine("Return exactly this JSON shape with no extra keys:");
        builder.AppendLine("{\"proposalIds\":[\"candidate_id\"],\"mutationTags\":[\"tag\"],\"reasons\":[{\"proposalId\":\"candidate_id\",\"reason\":\"...\"}],\"flavorText\":\"...\",\"confidence\":0.0}");
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
        builder.AppendLine("recentEvents=see publicNarrativeContext");
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
        builder.AppendLine("Choose one or more distinct proposalIds only from the candidate pool. reasons must have the same count and proposalId order as proposalIds. mutationTags must be distinct values from allowedMutationTags. Reasons should explain the current context, not restate raw numbers only.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), material);
    }

    public static string BuildPrompt(FacilityEvolutionContext context)
    {
        return BuildPromptEnvelope(context).Prompt;
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
