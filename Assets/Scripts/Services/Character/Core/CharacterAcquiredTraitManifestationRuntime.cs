using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using VContainer;
using VContainer.Unity;

public enum CharacterAcquiredTraitManifestationStatus
{
    PendingRestored,
    PendingInvalid,
    SubmissionSucceeded,
    SubmissionRejected,
    DispatchAccepted,
    DispatchRejected,
    TransportFailed,
    TransportTimedOut,
    TransportCancelled,
    CompletionSucceeded,
    CompletionRejected,
    StaleCallbackRejected,
    TargetUnavailable
}

public readonly struct CharacterAcquiredTraitManifestationDiagnostic
{
    public CharacterAcquiredTraitManifestationDiagnostic(
        CharacterAcquiredTraitManifestationStatus status,
        string targetPersistentId,
        string requestId,
        string requestKey,
        string candidatePacketHash,
        int manifestationMilestone,
        int attempt,
        float nextRetryAt,
        CharacterAcquiredTraitInferenceIssueCode issueCode,
        LocalLlmRequestStatus? transportStatus,
        string detail)
    {
        Status = status;
        TargetPersistentId = targetPersistentId?.Trim() ?? string.Empty;
        RequestId = requestId?.Trim() ?? string.Empty;
        RequestKey = requestKey?.Trim() ?? string.Empty;
        CandidatePacketHash = candidatePacketHash?.Trim() ?? string.Empty;
        ManifestationMilestone = manifestationMilestone;
        Attempt = Math.Max(0, attempt);
        NextRetryAt = Math.Max(0f, nextRetryAt);
        IssueCode = issueCode;
        TransportStatus = transportStatus;
        Detail = detail?.Trim() ?? string.Empty;
    }

    public string ProfileId => LocalLlmRequestProfiles.AcquiredTrait.Id;
    public CharacterAcquiredTraitManifestationStatus Status { get; }
    public string TargetPersistentId { get; }
    public string RequestId { get; }
    public string RequestKey { get; }
    public string CandidatePacketHash { get; }
    public int ManifestationMilestone { get; }
    public int Attempt { get; }
    public float NextRetryAt { get; }
    public CharacterAcquiredTraitInferenceIssueCode IssueCode { get; }
    public LocalLlmRequestStatus? TransportStatus { get; }
    public string Detail { get; }
    public bool IsSuccess =>
        Status == CharacterAcquiredTraitManifestationStatus.CompletionSucceeded;
    public bool IsFailure => Status is
        CharacterAcquiredTraitManifestationStatus.PendingInvalid or
        CharacterAcquiredTraitManifestationStatus.SubmissionRejected or
        CharacterAcquiredTraitManifestationStatus.DispatchRejected or
        CharacterAcquiredTraitManifestationStatus.TransportFailed or
        CharacterAcquiredTraitManifestationStatus.TransportTimedOut or
        CharacterAcquiredTraitManifestationStatus.TransportCancelled or
        CharacterAcquiredTraitManifestationStatus.CompletionRejected or
        CharacterAcquiredTraitManifestationStatus.StaleCallbackRejected or
        CharacterAcquiredTraitManifestationStatus.TargetUnavailable;
}

public readonly struct CharacterAcquiredTraitManifestationDiagnosticEvent
{
    public CharacterAcquiredTraitManifestationDiagnosticEvent(
        CharacterAcquiredTraitManifestationDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }

    public CharacterAcquiredTraitManifestationDiagnostic Diagnostic { get; }
}

public interface ICharacterAcquiredTraitManifestationDiagnostics
{
    event Action<CharacterAcquiredTraitManifestationDiagnostic>
        DiagnosticPublished;

    int PendingRequestCount { get; }

    bool TryGetLatestDiagnostic(
        string targetPersistentId,
        out CharacterAcquiredTraitManifestationDiagnostic diagnostic);
}

public static class CharacterAcquiredTraitPromptBuilder
{
    private sealed class PromptModuleOffer
    {
        public PromptModuleOffer(
            string moduleId,
            NarrativeFormulaModulePolarity polarity,
            string semanticDescription)
        {
            ModuleId = moduleId;
            Polarity = polarity;
            SemanticDescription = semanticDescription;
        }

        public string ModuleId { get; }
        public NarrativeFormulaModulePolarity Polarity { get; }
        public string SemanticDescription { get; }
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));
        return BuildPublicMaterial(progression, packet.evidenceFactIds);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return BuildPublicMaterial(
            progression,
            packet.evidenceFactIds,
            ledgerPublicationDescriptors);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression,
        IEnumerable<string> projectedOrLegacyEvidenceFactIds)
    {
        return BuildPublicMaterialCore(
            progression,
            projectedOrLegacyEvidenceFactIds,
            ledgerPublicationDescriptors: null);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression,
        IEnumerable<string> projectedOrLegacyEvidenceFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return BuildPublicMaterialCore(
            progression,
            projectedOrLegacyEvidenceFactIds,
            ledgerPublicationDescriptors);
    }

    private static NarrativePublicContextMaterial BuildPublicMaterialCore(
        CharacterProgression progression,
        IEnumerable<string> projectedOrLegacyEvidenceFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        HashSet<string> originalFactIds = new(StringComparer.Ordinal);
        foreach (string evidenceId in projectedOrLegacyEvidenceFactIds
                 ?? Array.Empty<string>())
        {
            if (!CharacterAcquiredTraitEvidenceProjection.TryResolve(
                    progression.NarrativeLedger,
                    evidenceId,
                    out CharacterNarrativeFact[] resolved,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }
            originalFactIds.UnionWith(resolved.Select(value => value.factId));
        }
        return ledgerPublicationDescriptors == null
            ? BuildPublicMaterialForLedgerEvidence(progression, originalFactIds)
            : BuildPublicMaterialForLedgerEvidence(
                progression,
                originalFactIds,
                ledgerPublicationDescriptors);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterialForLedgerEvidence(
        CharacterProgression progression,
        IEnumerable<string> originalEvidenceFactIds)
    {
        return NarrativeRequestContextBuilder.BuildPublicMaterialForProgression(
            LocalLlmRequestProfiles.AcquiredTrait.Id,
            progression,
            requireCharacterFact: false,
            requireMotif: false,
            originalEvidenceFactIds);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterialForLedgerEvidence(
        CharacterProgression progression,
        IEnumerable<string> originalEvidenceFactIds,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return NarrativeRequestContextBuilder.BuildPublicMaterialForProgression(
            LocalLlmRequestProfiles.AcquiredTrait.Id,
            progression,
            requireCharacterFact: false,
            requireMotif: false,
            originalEvidenceFactIds,
            ledgerPublicationDescriptors);
    }

    public static IReadOnlyList<string> BuildProjectedEvidenceFactIds(
        NarrativePublicContextMaterial publicMaterial,
        IEnumerable<string> originalEvidenceFactIds)
    {
        if (publicMaterial == null) throw new ArgumentNullException(nameof(publicMaterial));
        if (!string.Equals(
                publicMaterial.ProfileId,
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                StringComparison.Ordinal)
            || publicMaterial.SubjectKind != NarrativePublicSubjectKind.Character)
        {
            throw new InvalidOperationException(
                "Acquired-trait evidence projection requires AcquiredTrait character material.");
        }
        HashSet<string> originals = (originalEvidenceFactIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (originals.Count == 0)
            throw new InvalidOperationException(
                "Acquired-trait evidence projection requires original ledger fact IDs.");
        HashSet<string> eventFactIds = publicMaterial.Events
            .Select(value => value.FactId)
            .ToHashSet(StringComparer.Ordinal);
        string[] projected = publicMaterial.PublicFacts
            .Where(value => originals.Contains(value.OriginalFactId)
                && eventFactIds.Contains(value.FactId))
            .Select(value => value.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string missing = originals.FirstOrDefault(original =>
            !publicMaterial.PublicFacts.Any(value =>
                string.Equals(value.OriginalFactId, original, StringComparison.Ordinal)
                && eventFactIds.Contains(value.FactId)));
        if (missing != null)
        {
            throw new InvalidOperationException(
                $"Acquired-trait evidence '{missing}' has no selected public event.");
        }
        return Array.AsReadOnly(projected);
    }

    public static string Build(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        return BuildEnvelope(
            progression,
            packet,
            BuildPublicMaterial(progression, packet)).Prompt;
    }

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        return BuildEnvelope(
            progression,
            packet,
            BuildPublicMaterial(progression, packet));
    }

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet,
        NarrativePublicContextMaterial publicMaterial)
    {
        return BuildEnvelope(
            progression,
            packet,
            publicMaterial,
            null,
            null);
    }

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet,
        NarrativePublicContextMaterial publicMaterial,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules)
    {
        if (progression == null)
            throw new ArgumentNullException(nameof(progression));
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));
        if (publicMaterial == null)
            throw new ArgumentNullException(nameof(publicMaterial));
        if (!string.Equals(
                publicMaterial.ProfileId,
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                StringComparison.Ordinal)
            || publicMaterial.SubjectKind != NarrativePublicSubjectKind.Character
            || !string.Equals(
                publicMaterial.SubjectId,
                packet.targetPersistentId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Acquired-trait prompt material does not match its request target.");
        }
        CharacterAcquiredTraitAggregateState aggregate = progression.CaptureAcquiredTraitState();
        if (aggregate.TryGetPendingRequest(packet.requestId,
                out CharacterAcquiredTraitPendingRequestState frozen)
            && frozen.formulaVersion > 0)
        {
            IReadOnlyList<PromptModuleOffer> revalidatedSelection = null;
            if (frozen.presentationState ==
                CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
            {
                if ((settings == null) != (modules == null))
                {
                    throw new InvalidOperationException(
                        "Acquired-trait module revalidation requires both settings and modules.");
                }
                revalidatedSelection = settings != null && modules != null
                    ? RebuildModuleSelection(
                        progression,
                        frozen,
                        settings,
                        modules)
                    : RequireQualitativePersistedSelection(frozen);
            }
            return BuildFormulaEnvelope(
                packet,
                frozen,
                publicMaterial,
                revalidatedSelection);
        }
        foreach (string evidenceFactId in packet.evidenceFactIds ?? new List<string>())
        {
            if (!evidenceFactId.StartsWith(
                    CharacterAcquiredTraitEvidenceProjection.ProjectedFactIdPrefix,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Acquired-trait live packet contains legacy unprojected evidence '{evidenceFactId}'.");
            }
            bool projected = publicMaterial.PublicFacts.Any(value =>
                string.Equals(
                    value.FactId,
                    evidenceFactId,
                    StringComparison.Ordinal)
                && publicMaterial.Events.Any(eventValue => string.Equals(
                    eventValue.FactId,
                    value.FactId,
                    StringComparison.Ordinal)));
            if (!projected)
            {
                throw new InvalidOperationException(
                    $"Acquired-trait evidence '{evidenceFactId}' is absent from public material.");
            }
        }
        string computedHash = CharacterAcquiredTraitRequestPacketAuthority
            .ComputeHash(packet);
        if (!string.Equals(
                computedHash,
                packet.candidatePacketHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Acquired-trait prompt requires an exact canonical packet hash.");
        }

        StringBuilder builder = new(4096);
        builder.AppendLine("당신은 던전 경영 RPG의 후천 특성 서술 선택기다.");
        builder.AppendLine(
            "C#이 아래의 모든 기계 수치, 예산, 충돌, 후보 조합을 이미 확정했다.");
        builder.AppendLine(
            "당신은 combinationOptions 중 하나를 그대로 고르고 표시 문구만 작성한다.");
        builder.Append("profile=")
            .AppendLine(LocalLlmRequestProfiles.AcquiredTrait.Id);
        builder.Append("settingsId=").AppendLine(packet.settingsId);
        builder.Append("targetPersistentId=")
            .AppendLine(packet.targetPersistentId);
        builder.Append("requestId=").AppendLine(packet.requestId);
        builder.Append("requestKey=").AppendLine(packet.requestKey);
        builder.Append("candidatePacketHash=")
            .AppendLine(packet.candidatePacketHash);
        builder.Append("manifestationMilestone=")
            .AppendLine(packet.manifestationMilestone.ToString(
                CultureInfo.InvariantCulture));
        builder.Append("rarity=").AppendLine(packet.rarity);
        builder.Append("budget=").AppendLine(packet.budget.ToString(
            CultureInfo.InvariantCulture));
        builder.Append("maximumActiveTraits=")
            .AppendLine(packet.maximumActiveTraits.ToString(
                CultureInfo.InvariantCulture));
        builder.Append("eligibleDomains=")
            .AppendLine(Join(packet.eligibleDomains));
        builder.AppendLine("modules:");
        foreach (CharacterAcquiredTraitModulePacketDto module in packet.modules)
        {
            builder.Append("- moduleId=").Append(module.moduleId)
                .Append("; displayName=").Append(CanonicalText(module.displayName))
                .Append("; description=").Append(CanonicalText(module.description))
                .Append("; cost=").Append(module.cost.ToString(
                    CultureInfo.InvariantCulture))
                .Append("; domains=").Append(Join(module.domainAffinities))
                .Append("; conflicts=").AppendLine(Join(module.conflictGroups));
        }
        builder.AppendLine("combinationOptions:");
        foreach (CharacterAcquiredTraitCombinationPacketDto option
                 in packet.combinationOptions)
        {
            builder.Append("- combinationId=").Append(option.combinationId)
                .Append("; moduleIds=").Append(Join(option.moduleIds))
                .Append("; totalCost=").Append(option.totalCost.ToString(
                    CultureInfo.InvariantCulture))
                .Append("; domains=").Append(Join(option.domainAffinities))
                .Append("; conflicts=").AppendLine(Join(option.conflictGroups));
        }
        builder.Append("evidenceFactIds=")
            .AppendLine(Join(packet.evidenceFactIds));
        builder.AppendLine("반드시 JSON 객체 하나만 반환한다.");
        builder.AppendLine(
            "형식: {\"combinationId\":\"제공된 조합 ID\",\"displayName\":\"40자 이하 한국어 이름\",\"description\":\"180자 이하 한국어 설명\",\"narrativeReason\":\"180자 이하 경험 근거\",\"evidenceFactIds\":[\"제공된 사실 ID\"]}");
        builder.AppendLine("절대 규칙:");
        builder.AppendLine(
            "1. combinationId는 combinationOptions에 있는 값을 한 글자도 바꾸지 않고 하나만 고른다.");
        builder.AppendLine(
            "2. evidenceFactIds는 제공된 evidenceFactIds의 비어 있지 않은 부분집합이며 중복 없이 오름차순으로 쓴다.");
        builder.AppendLine(
            "3. 예산, 비용, 효과값, 모듈 목록, 충돌, 희귀도나 다른 기계 필드를 출력하지 않는다.");
        builder.AppendLine(
            "4. 수치를 만들거나 후보를 변형하지 않고, 선택한 조합과 실제 경험이 드러나는 자연스러운 한국어만 쓴다.");
        builder.AppendLine(
            "5. 최상위 키는 combinationId, displayName, description, narrativeReason, evidenceFactIds만 정확히 한 번씩 쓴다.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), publicMaterial);
    }

    private static NarrativePublicPromptEnvelope BuildFormulaEnvelope(
        CharacterAcquiredTraitRequestPacketDto packet,
        CharacterAcquiredTraitPendingRequestState frozen,
        NarrativePublicContextMaterial publicMaterial,
        IReadOnlyList<PromptModuleOffer> revalidatedSelection)
    {
        if (frozen.presentationState != CharacterAcquiredTraitPresentationState.PresentationPending
            && frozen.presentationState != CharacterAcquiredTraitPresentationState.ModuleSelectionPending
            || !string.Equals(frozen.targetPersistentId, packet.targetPersistentId, StringComparison.Ordinal)
            || !string.Equals(frozen.requestKey, packet.requestKey, StringComparison.Ordinal)
            || !string.Equals(frozen.candidatePacketHash, packet.candidatePacketHash, StringComparison.Ordinal)
            || !frozen.evidenceFactIds.SequenceEqual(packet.evidenceFactIds, StringComparer.Ordinal))
            throw new InvalidOperationException("Acquired-trait formula presentation packet diverged from persisted mechanics.");
        if (frozen.presentationState == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            if (revalidatedSelection == null)
                throw new InvalidOperationException(
                    "Acquired-trait module offers require qualitative revalidation.");
            StringBuilder selectionBuilder = new(4096);
            selectionBuilder.AppendLine("당신은 던전 경영 RPG의 후천 특성 모듈 선택기이자 표현 작가다.");
            selectionBuilder.AppendLine("C#이 아래의 개별 합법 모듈과 실제 원장 근거만 제공한다. 어울리는 이로운 모듈 하나와 선택적으로 해로운 모듈 하나를 고른다. 수치와 비용은 C#이 선택 뒤 계산한다.");
            selectionBuilder.Append("profile=").AppendLine(LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id);
            selectionBuilder.Append("selectionId=").AppendLine(frozen.moduleSelectionId);
            selectionBuilder.AppendLine("moduleOffers:");
            foreach (PromptModuleOffer offer in revalidatedSelection
                         .OrderBy(value => value.Polarity)
                         .ThenBy(value => value.ModuleId, StringComparer.Ordinal))
            {
                selectionBuilder.Append("- moduleId=").Append(offer.ModuleId)
                    .Append("; polarity=").Append(offer.Polarity)
                    .Append("; meaning=").AppendLine(CanonicalText(offer.SemanticDescription));
            }
            selectionBuilder.Append("evidenceFactIds=").AppendLine(Join(frozen.evidenceFactIds));
            selectionBuilder.AppendLine("반드시 JSON 객체 하나만 반환한다.");
            selectionBuilder.AppendLine("형식: {\"selectionId\":\"제공된 값 그대로\",\"positiveModuleIds\":[\"이로운 모듈 ID 하나\"],\"drawbackModuleIds\":[\"선택적 해로운 모듈 ID\"],\"evidenceFactIds\":[\"제공된 사실 ID\"],\"displayName\":\"32자 이하 한국어 이름\",\"narrativeFlavor\":\"180자 이하 획득 서사\"}");
            selectionBuilder.AppendLine("절대 규칙:");
            selectionBuilder.AppendLine("1. 최상위 키는 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor만 정확히 한 번씩 쓴다.");
            selectionBuilder.AppendLine("2. positiveModuleIds는 Positive 제안 중 정확히 하나다. drawbackModuleIds는 Drawback 제안 중 0개 또는 1개다.");
            selectionBuilder.AppendLine("3. evidenceFactIds는 제공된 실제 사실 ID의 비어 있지 않은 부분집합이다.");
            selectionBuilder.AppendLine("4. 효과 ID나 수치, 비용, 배율을 만들거나 출력하지 않는다.");
            selectionBuilder.AppendLine("5. 이름과 서사는 선택한 모듈과 인용한 실제 기록이 왜 이어지는지 자연스럽게 드러내야 한다.");
            return NarrativePublicPromptEnvelope.Create(selectionBuilder.ToString(), publicMaterial);
        }
        StringBuilder builder = new(2048);
        builder.AppendLine("당신은 던전 경영 RPG의 후천 특성 표현 작가다.");
        builder.AppendLine("C#이 기능, 수치, 비용, 근거와 효과를 모두 확정했다. 새 게임 사실이나 수치를 만들지 마라.");
        builder.Append("profile=").AppendLine("AcquiredTrait");
        builder.Append("presentationId=").AppendLine(frozen.presentationId);
        builder.Append("mechanicalDescription=").AppendLine(CanonicalText(frozen.mechanicalDescription));
        builder.AppendLine("반드시 JSON 객체 하나만 반환한다.");
        builder.AppendLine("형식: {\"presentationId\":\"제공된 값 그대로\",\"displayName\":\"32자 이하 한국어 이름\",\"narrativeFlavor\":\"180자 이하 획득 서사\"}");
        builder.AppendLine("절대 규칙:");
        builder.AppendLine("1. 최상위 키는 presentationId, displayName, narrativeFlavor 세 문자열만 정확히 한 번씩 쓴다.");
        builder.AppendLine("2. presentationId는 제공된 값을 한 글자도 바꾸지 않는다.");
        builder.AppendLine("3. displayName과 narrativeFlavor에 숫자, 퍼센트, 비용, 효과 수치나 기계 규칙을 쓰지 않는다.");
        builder.AppendLine("4. 제공된 공개 사실에서만 자연스러운 한국어 획득 서사를 쓴다.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), publicMaterial);
    }

    private static IReadOnlyList<PromptModuleOffer> RebuildModuleSelection(
        CharacterProgression progression,
        CharacterAcquiredTraitPendingRequestState frozen,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        if (frozen.formulaVersion != policy.FormulaVersion
            || !string.Equals(
                frozen.formulaCatalogSha256,
                settings.FormulaPolicy.RequireCatalogSha256(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Acquired-trait module selection no longer matches the authored formula catalog.");
        }
        NarrativeFormulaModuleSelectionRequest current =
            CharacterAcquiredTraitFormulaGeneration.PrepareModuleSelection(
                progression,
                frozen.requestId,
                frozen.requestKey,
                frozen.manifestationMilestone,
                settings,
                modules,
                frozen.evidenceFactIds,
                frozen.evidenceBindings);
        string[] currentBenefits = current.Offers
            .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
            .Select(value => value.ModuleId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] currentDrawbacks = current.Offers
            .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
            .Select(value => value.ModuleId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitModuleOfferState[] persistedOffers =
            (frozen.moduleOffers ?? new List<CharacterAcquiredTraitModuleOfferState>())
            .Where(value => value != null)
            .OrderBy(value => value.moduleId, StringComparer.Ordinal)
            .ToArray();
        NarrativeFormulaModuleOffer[] currentOffers = current.Offers
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        bool sameOffers = persistedOffers.Length == currentOffers.Length
            && persistedOffers.Zip(currentOffers, (persisted, authored) =>
                    string.Equals(
                        persisted.moduleId,
                        authored.ModuleId,
                        StringComparison.Ordinal)
                    && persisted.polarity == authored.Polarity)
                .All(value => value);
        if (!string.Equals(
                frozen.moduleSelectionId,
                current.SelectionId,
                StringComparison.Ordinal)
            || !currentBenefits.SequenceEqual(
                frozen.offeredBenefitModuleIds ?? new List<string>(),
                StringComparer.Ordinal)
            || !currentDrawbacks.SequenceEqual(
                frozen.offeredDrawbackModuleIds ?? new List<string>(),
                StringComparer.Ordinal)
            || !current.EvidenceFactIds.SequenceEqual(
                frozen.evidenceFactIds ?? new List<string>(),
                StringComparer.Ordinal)
            || !sameOffers)
        {
            throw new InvalidOperationException(
                "Acquired-trait module offers diverged from the authored selection.");
        }
        return Array.AsReadOnly(current.Offers
            .Select(value => new PromptModuleOffer(
                value.ModuleId,
                value.Polarity,
                value.SemanticDescription))
            .ToArray());
    }

    private static IReadOnlyList<PromptModuleOffer> RequireQualitativePersistedSelection(
        CharacterAcquiredTraitPendingRequestState frozen)
    {
        CharacterAcquiredTraitModuleOfferState[] persisted =
            (frozen.moduleOffers ?? new List<CharacterAcquiredTraitModuleOfferState>())
            .Where(value => value != null)
            .ToArray();
        if (persisted.Any(value => ContainsMechanicalNumber(value.semanticDescription)))
        {
            throw new InvalidOperationException(
                "Legacy acquired-trait module prose requires authoritative content revalidation.");
        }
        return Array.AsReadOnly(persisted
            .Select(value => new PromptModuleOffer(
                value.moduleId,
                value.polarity,
                value.semanticDescription))
            .ToArray());
    }

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character => character is >= '0' and <= '9'
            or >= '０' and <= '９' or '%' or '％');

    private static string Join(IEnumerable<string> values) =>
        string.Join(",", values ?? Array.Empty<string>());

    private static string CanonicalText(string value) =>
        (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
}

/// <summary>
/// Observes published, living character progression authorities and transports
/// their exact persisted acquired-trait request through the local LLM queue.
/// Retry timing is transient; request identity, packet hash, and registered
/// revision remain owned by CharacterProgression's persisted aggregate.
/// </summary>
public sealed class CharacterAcquiredTraitManifestationRuntime :
    ICharacterAcquiredTraitManifestationDiagnostics,
    IStartable,
    ITickable,
    IDisposable
{
    private const int MaximumConcurrentRequests = 2;
    private const int MaximumSubmissionsPerTick = 1;
    private const float InitialRetrySeconds = 2f;
    private const float MaximumRetrySeconds = 60f;
    private const float AcceptedRequestTimeoutSeconds = 75f;
    private const string RequestIdPrefix = "acquired-trait-request:";
    private const string RequestKeyPrefix = "acquired-trait-key:";

    private sealed class PendingDispatch
    {
        public CharacterAcquiredTraitRequestPacketDto Packet;
        public string Prompt;
        public NarrativePublicContextMaterial PublicMaterial;
        public int RegisteredRevision;
        public int Attempts;
        public float NextAttemptAt;
        public float SubmittedAt;
        public int DispatchGeneration;
        public bool InFlight;

        public bool Matches(CharacterAcquiredTraitPendingRequestState pending) =>
            pending != null
            && Packet != null
            && string.Equals(Packet.targetPersistentId,
                pending.targetPersistentId, StringComparison.Ordinal)
            && string.Equals(Packet.requestId,
                pending.requestId, StringComparison.Ordinal)
            && string.Equals(Packet.requestKey,
                pending.requestKey, StringComparison.Ordinal)
            && string.Equals(Packet.candidatePacketHash,
                pending.candidatePacketHash, StringComparison.Ordinal)
            && Packet.manifestationMilestone == pending.manifestationMilestone
            && RegisteredRevision == pending.registeredRevision
            && Packet.evidenceFactIds != null
            && pending.evidenceFactIds != null
            && Packet.evidenceFactIds.SequenceEqual(
                pending.evidenceFactIds,
                StringComparer.Ordinal);
    }

    private sealed class Observation
    {
        public CharacterActor Actor;
        public CharacterProgression Progression;
        public Action ChangedHandler;
        public PendingDispatch Pending;
        public bool Dirty = true;
    }

    private readonly ICharacterWorldQuery world;
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly IGameCalendar calendar;
    private readonly IUiClock uiClock;
    private readonly IGameEventBus events;
    private readonly IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery;
    private readonly IAcquiredTraitInferenceOutcomeCommitter outcomeCommitter;
    private readonly CharacterAcquiredTraitSettingsSO settings;
    private readonly CharacterAcquiredTraitModuleSO[] modules;
    private readonly CharacterAcquiredTraitInferenceService inference;
    private readonly Dictionary<string, Observation> observations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterAcquiredTraitManifestationDiagnostic>
        latestDiagnostics = new(StringComparer.Ordinal);
    private int observedWorldVersion = int.MinValue;
    private bool disposed;

    [Inject]
    public CharacterAcquiredTraitManifestationRuntime(
        IGameContentDefinitionSource content,
        ICharacterWorldQuery world,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IGameCalendar calendar,
        IUiClock uiClock,
        IGameEventBus events,
        IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery,
        IAcquiredTraitInferenceOutcomeCommitter outcomeCommitter)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.outcomeEvidenceQuery = outcomeEvidenceQuery
            ?? throw new ArgumentNullException(nameof(outcomeEvidenceQuery));
        this.outcomeCommitter = outcomeCommitter
            ?? throw new ArgumentNullException(nameof(outcomeCommitter));

        settings = content.RequireSingle<CharacterAcquiredTraitSettingsSO>()
            ?? throw new InvalidOperationException(
                "Acquired-trait manifestation settings are missing.");
        modules = (content.GetAll<CharacterAcquiredTraitModuleSO>()
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        string[] definitionErrors = settings.ValidateDefinition()
            .Concat(modules.SelectMany(value => value.ValidateDefinition()))
            .Concat(modules.GroupBy(value => value.ModuleId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group =>
                    $"Duplicate acquired-trait module '{group.Key}'."))
            .ToArray();
        if (modules.Length == 0 || definitionErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Acquired-trait manifestation content is invalid: "
                + (modules.Length == 0
                    ? "No acquired-trait modules are authored."
                    : string.Join(" | ", definitionErrors)));
        }
        inference = new CharacterAcquiredTraitInferenceService(settings, modules);
    }

    public event Action<CharacterAcquiredTraitManifestationDiagnostic>
        DiagnosticPublished;

    public int PendingRequestCount => observations.Values.Count(value =>
        value?.Pending != null);

    public bool TryGetLatestDiagnostic(
        string targetPersistentId,
        out CharacterAcquiredTraitManifestationDiagnostic diagnostic)
    {
        string canonical = targetPersistentId?.Trim() ?? string.Empty;
        return latestDiagnostics.TryGetValue(canonical, out diagnostic);
    }

    [GameplayInternalOnly(
        "Composition starts authoritative character observation.",
        "IStartable|DungeonCharacterRegistration")]
    public void Start()
    {
        if (disposed)
            return;
        ReconcileWorld(force: true);
    }

    [GameplayInternalOnly(
        "The registered entry point advances acquired-trait observation and bounded dispatch.",
        "ITickable|DungeonCharacterRegistration")]
    public void Tick()
    {
        if (disposed)
            return;
        ReconcileWorld(force: false);
        KeyValuePair<string, Observation>[] ordered = observations
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (KeyValuePair<string, Observation> pair in ordered)
        {
            Observation observation = pair.Value;
            if (!IsPublishedLiving(observation.Actor))
            {
                RemoveObservation(
                    pair.Key,
                    "Character is no longer a published living world actor.",
                    publishDiagnostic: true);
                observedWorldVersion = int.MinValue;
                continue;
            }
            if (observation.Dirty)
                Evaluate(observation);
        }

        float now = uiClock.Time;
        foreach (Observation observation in observations
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                     .Select(pair => pair.Value)
                     .ToArray())
        {
            PendingDispatch pending = observation.Pending;
            if (pending?.InFlight == true
                && now - pending.SubmittedAt >= AcceptedRequestTimeoutSeconds)
            {
                Timeout(observation, pending, now);
            }
        }

        int inFlight = observations.Values.Count(value =>
            value?.Pending?.InFlight == true);
        int submitted = 0;
        foreach (Observation observation in observations
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                     .Select(pair => pair.Value))
        {
            PendingDispatch pending = observation.Pending;
            if (pending == null
                || pending.InFlight
                || now < pending.NextAttemptAt
                || inFlight >= MaximumConcurrentRequests
                || submitted >= MaximumSubmissionsPerTick)
            {
                continue;
            }
            submitted++;
            if (TryDispatch(observation, pending, now))
                inFlight++;
        }
    }

    [GameplayInternalOnly(
        "Composition cancels transient transports without consuming persisted gates.",
        "IDisposable|DungeonCharacterRegistration")]
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (string targetId in observations.Keys.ToArray())
            RemoveObservation(targetId, string.Empty, publishDiagnostic: false);
        observations.Clear();
    }

    private void ReconcileWorld(bool force)
    {
        if (!force && observedWorldVersion == world.CharacterVersion)
            return;
        observedWorldVersion = world.CharacterVersion;
        CharacterActor[] actors = CharacterActorCollection
            .DistinctByGameObject(world.Characters)
            .Where(IsPublishedLiving)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        string duplicateId = actors
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateId != null)
        {
            throw new InvalidOperationException(
                $"Acquired-trait producer found duplicate live character ID '{duplicateId}'.");
        }

        HashSet<string> currentIds = actors
            .Select(value => value.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string removed in observations.Keys
                     .Where(value => !currentIds.Contains(value))
                     .ToArray())
        {
            RemoveObservation(
                removed,
                "Character left the authoritative world query.",
                publishDiagnostic: true);
        }
        foreach (CharacterActor actor in actors)
        {
            string targetId = actor.Identity.PersistentId;
            if (observations.TryGetValue(targetId, out Observation existing)
                && ReferenceEquals(existing.Actor, actor))
            {
                continue;
            }
            if (existing != null)
            {
                RemoveObservation(
                    targetId,
                    "Authoritative character instance was replaced.",
                    publishDiagnostic: true);
            }

            Observation observation = new()
            {
                Actor = actor,
                Progression = actor.Progression
            };
            observation.ChangedHandler = () => observation.Dirty = true;
            observation.Progression.Changed += observation.ChangedHandler;
            observations.Add(targetId, observation);
        }
    }

    private void Evaluate(Observation observation)
    {
        observation.Dirty = false;
        CharacterAcquiredTraitAggregateState state = observation.Progression
            .CaptureAcquiredTraitState();
        CharacterAcquiredTraitPendingRequestState[] persisted =
            (state.pendingRequests
                ?? new List<CharacterAcquiredTraitPendingRequestState>())
            .Where(value => value != null)
            .ToArray();
        if (persisted.Length > 1)
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.PendingInvalid,
                observation.Actor.Identity.PersistentId,
                null,
                0,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed,
                null,
                "More than one persisted acquired-trait request exists for one character.");
            return;
        }
        if (persisted.Length == 1)
        {
            if (observation.Pending?.Matches(persisted[0]) == true)
                return;
            CancelTransport(observation.Pending);
            observation.Pending = null;
            RestorePending(observation, state, persisted[0]);
            return;
        }

        if (observation.Pending != null)
        {
            CancelTransport(observation.Pending);
            observation.Pending = null;
        }
        if (!CharacterAcquiredTraitExperienceScore.TryCalculate(
                observation.Progression.NarrativeLedger,
                out int score,
                out string scoreError))
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.SubmissionRejected,
                observation.Actor.Identity.PersistentId,
                null,
                0,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.InvalidEvidence,
                null,
                scoreError);
            return;
        }

        int milestone = settings.ManifestationGates
            .Where(value => value != null
                && value.MeaningfulRecordMilestone <= score
                && !state.HasProcessedMilestone(value.MeaningfulRecordMilestone))
            .Select(value => value.MeaningfulRecordMilestone)
            .OrderBy(value => value)
            .FirstOrDefault();
        if (milestone <= 0)
            return;

        string[] evidence = CharacterAcquiredTraitMeaningfulLedgerValidator
            .RequireMeaningfulFactIds(observation.Progression.NarrativeLedger)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                observation.Progression,
                evidence);
        List<GameplayOutcomeEvidenceBindingSnapshot> exactBindings =
            GameplayOutcomeEvidenceFormulaProjection.CaptureExact(
                outcomeEvidenceQuery.GetForCharacter(
                    observation.Actor.Identity.PersistentId,
                    maximumCount: 32,
                    minimumSalience: 0f,
                    includeCompacted: false));
        publicMaterial = GameplayOutcomeEvidencePublicMaterialComposer.AddExactEvidence(
            publicMaterial, exactBindings);
        IReadOnlyList<string> localProjectedEvidence =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                evidence);
        string[] projectedEvidence = localProjectedEvidence
            .Concat(exactBindings.Select(value => value.publicFactId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        BuildRequestIdentity(
            observation.Actor.Identity.PersistentId,
            milestone,
            projectedEvidence,
            publicMaterial.SemanticHash,
            out string requestId,
            out string requestKey);
        CharacterAcquiredTraitInferenceCommandResult result =
            inference.SubmitMilestone(
                observation.Progression,
                new CharacterAcquiredTraitSubmissionCommand(
                    observation.Actor.Identity.PersistentId,
                    requestId,
                    requestKey,
                    milestone,
                    projectedEvidence,
                    exactBindings,
                    state.revision,
                    CurrentTimestamp()));
        Publish(
            result.Succeeded
                ? CharacterAcquiredTraitManifestationStatus.SubmissionSucceeded
                : CharacterAcquiredTraitManifestationStatus.SubmissionRejected,
            observation.Actor.Identity.PersistentId,
            result.Packet,
            0,
            0f,
            result.Audit.IssueCode,
            null,
            result.Audit.ValidationError);
        if (result.Succeeded)
        {
            observation.Pending = CreatePending(
                observation.Progression,
                result.Packet,
                result.Audit.RegisteredRevision,
                uiClock.Time,
                publicMaterial);
        }
    }

    private void RestorePending(
        Observation observation,
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitPendingRequestState persisted)
    {
        if (persisted.formulaVersion > 0
            && persisted.presentationState == CharacterAcquiredTraitPresentationState.AwaitingNarrativeRetry)
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.CompletionRejected,
                persisted.targetPersistentId,
                null,
                persisted.presentationFailureCount,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.InvalidNarrative,
                null,
                "Acquired-trait presentation awaits an explicit narrative retry after five failures.");
            return;
        }
        string legacyEvidence = (persisted.evidenceFactIds ?? new List<string>())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)
                && !value.StartsWith(
                    CharacterAcquiredTraitEvidenceProjection.ProjectedFactIdPrefix,
                    StringComparison.Ordinal));
        if (legacyEvidence != null)
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.PendingInvalid,
                persisted.targetPersistentId,
                null,
                0,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.PacketMismatch,
                null,
                $"Legacy acquired-trait pending evidence '{legacyEvidence}' cannot be submitted as public context without rewriting persisted identity.");
            return;
        }
        HashSet<string> exactIds = (persisted.evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        string[] localEvidence = (persisted.evidenceFactIds ?? new List<string>())
            .Where(value => !exactIds.Contains(value))
            .ToArray();
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(
                observation.Progression,
                localEvidence);
        publicMaterial = GameplayOutcomeEvidencePublicMaterialComposer.AddExactEvidence(
            publicMaterial, persisted.evidenceBindings);
        BuildRequestIdentity(
            persisted.targetPersistentId,
            persisted.manifestationMilestone,
            persisted.evidenceFactIds,
            publicMaterial.SemanticHash,
            out string expectedRequestId,
            out string expectedRequestKey);
        if (!string.Equals(
                persisted.requestId,
                expectedRequestId,
                StringComparison.Ordinal)
            || !string.Equals(
                persisted.requestKey,
                expectedRequestKey,
                StringComparison.Ordinal))
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.PendingInvalid,
                persisted.targetPersistentId,
                null,
                0,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.PacketMismatch,
                null,
                "Persisted acquired-trait identity does not match the current public context.");
            return;
        }
        CharacterAcquiredTraitSubmissionCommand command = new(
            persisted.targetPersistentId,
            persisted.requestId,
            persisted.requestKey,
            persisted.manifestationMilestone,
            persisted.evidenceFactIds,
            persisted.evidenceBindings,
            state.revision,
            CurrentTimestamp());
        if (persisted.formulaVersion > 0)
        {
            CharacterAcquiredTraitRequestPacketDto formulaPacket = new()
            {
                settingsId = settings.SettingsId,
                targetPersistentId = persisted.targetPersistentId,
                requestId = persisted.requestId,
                requestKey = persisted.requestKey,
                manifestationMilestone = persisted.manifestationMilestone,
                evidenceFactIds = persisted.evidenceFactIds.ToList(),
                candidatePacketHash = persisted.candidatePacketHash
            };
            observation.Pending = CreatePending(
                observation.Progression,
                formulaPacket,
                persisted.registeredRevision,
                uiClock.Time,
                publicMaterial);
            Publish(
                CharacterAcquiredTraitManifestationStatus.PendingRestored,
                persisted.targetPersistentId,
                formulaPacket,
                0,
                uiClock.Time,
                CharacterAcquiredTraitInferenceIssueCode.None,
                null,
                string.Empty);
            return;
        }
        if (!CharacterAcquiredTraitRequestPacketAuthority.TryBuild(
                command,
                observation.Progression.NarrativeLedger,
                state,
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto packet,
                out CharacterAcquiredTraitInferenceIssueCode issue,
                out string error)
            || !string.Equals(
                packet.candidatePacketHash,
                persisted.candidatePacketHash,
                StringComparison.Ordinal))
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.PendingInvalid,
                persisted.targetPersistentId,
                packet,
                0,
                0f,
                issue == CharacterAcquiredTraitInferenceIssueCode.None
                    ? CharacterAcquiredTraitInferenceIssueCode.PacketMismatch
                    : issue,
                null,
                string.IsNullOrWhiteSpace(error)
                    ? "Persisted acquired-trait packet hash cannot be reconstructed exactly."
                    : error);
            return;
        }

        observation.Pending = CreatePending(
            observation.Progression,
            packet,
            persisted.registeredRevision,
            uiClock.Time,
            publicMaterial);
        Publish(
            CharacterAcquiredTraitManifestationStatus.PendingRestored,
            persisted.targetPersistentId,
            packet,
            0,
            uiClock.Time,
            CharacterAcquiredTraitInferenceIssueCode.None,
            null,
            string.Empty);
    }

    private bool TryDispatch(
        Observation observation,
        PendingDispatch pending,
        float now)
    {
        if (!IsPublishedLiving(observation.Actor)
            || !StillOwnsPersistedPending(observation, pending))
        {
            observation.Dirty = true;
            return false;
        }

        pending.Attempts = pending.Attempts == int.MaxValue
            ? int.MaxValue
            : pending.Attempts + 1;
        CharacterAcquiredTraitAggregateState dispatchState = observation.Progression
            .CaptureAcquiredTraitState();
        dispatchState.TryGetPendingRequest(
            pending.Packet.requestId,
            out CharacterAcquiredTraitPendingRequestState persistedPending);
        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            ScheduleRetry(observation, pending, now);
            Publish(
                CharacterAcquiredTraitManifestationStatus.DispatchRejected,
                observation.Actor.Identity.PersistentId,
                pending.Packet,
                pending.Attempts,
                pending.NextAttemptAt,
                CharacterAcquiredTraitInferenceIssueCode.None,
                null,
                "The local LLM runtime does not expose correlated AcquiredTrait dispatch.");
            return false;
        }

        pending.InFlight = true;
        pending.SubmittedAt = now;
        int generation = ++pending.DispatchGeneration;
        Action<LocalLlmResult> callback = result =>
            HandleResult(observation, pending, generation, result);
        bool accepted;
        if (persistedPending?.presentationState
            == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            if (runtime is not ICorrelatedAcquiredTraitModuleSelectionLlmRuntime selector)
            {
                pending.InFlight = false;
                pending.SubmittedAt = 0f;
                ScheduleRetry(observation, pending, now);
                Publish(
                    CharacterAcquiredTraitManifestationStatus.DispatchRejected,
                    observation.Actor.Identity.PersistentId,
                    pending.Packet,
                    pending.Attempts,
                    pending.NextAttemptAt,
                    CharacterAcquiredTraitInferenceIssueCode.None,
                    null,
                    "The local LLM runtime does not expose correlated AcquiredTraitModuleSelection dispatch.");
                return false;
            }
            accepted = selector.GenerateAcquiredTraitModuleSelectionAsync(
                pending.Packet.requestKey, pending.Prompt, callback);
        }
        else
        {
            if (runtime is not ICorrelatedAcquiredTraitLlmRuntime correlated)
            {
                pending.InFlight = false;
                pending.SubmittedAt = 0f;
                ScheduleRetry(observation, pending, now);
                Publish(
                    CharacterAcquiredTraitManifestationStatus.DispatchRejected,
                    observation.Actor.Identity.PersistentId,
                    pending.Packet,
                    pending.Attempts,
                    pending.NextAttemptAt,
                    CharacterAcquiredTraitInferenceIssueCode.None,
                    null,
                    "The local LLM runtime does not expose correlated AcquiredTrait dispatch.");
                return false;
            }
            accepted = correlated.GenerateAcquiredTraitAsync(
                pending.Packet.requestKey, pending.Prompt, callback);
        }
        if (!accepted
            && ReferenceEquals(observation.Pending, pending)
            && pending.InFlight
            && pending.DispatchGeneration == generation)
        {
            pending.InFlight = false;
            pending.SubmittedAt = 0f;
            ScheduleRetry(observation, pending, uiClock.Time);
            Publish(
                CharacterAcquiredTraitManifestationStatus.DispatchRejected,
                observation.Actor.Identity.PersistentId,
                pending.Packet,
                pending.Attempts,
                pending.NextAttemptAt,
                CharacterAcquiredTraitInferenceIssueCode.None,
                null,
                "LocalLlmRequestQueue rejected the correlated AcquiredTrait request.");
            return false;
        }
        if (accepted
            && ReferenceEquals(observation.Pending, pending)
            && pending.InFlight
            && pending.DispatchGeneration == generation)
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.DispatchAccepted,
                observation.Actor.Identity.PersistentId,
                pending.Packet,
                pending.Attempts,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.None,
                null,
                string.Empty);
            return true;
        }
        return false;
    }

    private void HandleResult(
        Observation observation,
        PendingDispatch pending,
        int generation,
        LocalLlmResult result)
    {
        if (disposed)
            return;
        if (!ReferenceEquals(observation.Pending, pending)
            || !pending.InFlight
            || pending.DispatchGeneration != generation)
        {
            Publish(
                CharacterAcquiredTraitManifestationStatus.StaleCallbackRejected,
                pending.Packet.targetPersistentId,
                pending.Packet,
                pending.Attempts,
                pending.NextAttemptAt,
                CharacterAcquiredTraitInferenceIssueCode.StaleCallback,
                result.Status,
                "A stale or duplicate AcquiredTrait transport callback was ignored.");
            return;
        }

        pending.InFlight = false;
        pending.SubmittedAt = 0f;
        if (!result.IsSuccess)
        {
            ScheduleRetry(observation, pending, uiClock.Time);
            Publish(
                result.Status switch
                {
                    LocalLlmRequestStatus.TimedOut =>
                        CharacterAcquiredTraitManifestationStatus.TransportTimedOut,
                    LocalLlmRequestStatus.Cancelled =>
                        CharacterAcquiredTraitManifestationStatus.TransportCancelled,
                    _ => CharacterAcquiredTraitManifestationStatus.TransportFailed
                },
                pending.Packet.targetPersistentId,
                pending.Packet,
                pending.Attempts,
                pending.NextAttemptAt,
                CharacterAcquiredTraitInferenceIssueCode.None,
                result.Status,
                result.Error);
            return;
        }
        if (!IsPublishedLiving(observation.Actor)
            || !StillOwnsPersistedPending(observation, pending))
        {
            ScheduleRetry(observation, pending, uiClock.Time);
            Publish(
                CharacterAcquiredTraitManifestationStatus.StaleCallbackRejected,
                pending.Packet.targetPersistentId,
                pending.Packet,
                pending.Attempts,
                pending.NextAttemptAt,
                CharacterAcquiredTraitInferenceIssueCode.StaleCallback,
                result.Status,
                "AcquiredTrait callback target or persisted pending identity changed.");
            observation.Dirty = true;
            return;
        }

        int expectedRevision = observation.Progression.AcquiredTraitRevision;
        CharacterAcquiredTraitInferenceCommandResult completion =
            inference.CompleteMilestone(
                observation.Progression,
                new CharacterAcquiredTraitCompletionCommand(
                    pending.Packet.targetPersistentId,
                    pending.Packet.requestId,
                    pending.Packet.requestKey,
                    pending.Packet.candidatePacketHash,
                    pending.RegisteredRevision,
                    expectedRevision,
                    result.Content,
                    CurrentTimestamp()),
                pending.Packet,
                outcomeCommitter);
        if (completion.Succeeded)
        {
            observation.Pending = null;
            Publish(
                CharacterAcquiredTraitManifestationStatus.CompletionSucceeded,
                pending.Packet.targetPersistentId,
                pending.Packet,
                pending.Attempts,
                0f,
                CharacterAcquiredTraitInferenceIssueCode.None,
                result.Status,
                string.Empty);
            return;
        }

        ScheduleRetry(observation, pending, uiClock.Time);
        Publish(
            CharacterAcquiredTraitManifestationStatus.CompletionRejected,
            pending.Packet.targetPersistentId,
            pending.Packet,
            pending.Attempts,
            pending.NextAttemptAt,
            completion.Audit.IssueCode,
            result.Status,
            completion.Audit.ValidationError);
    }

    private void Timeout(
        Observation observation,
        PendingDispatch pending,
        float now)
    {
        pending.InFlight = false;
        pending.SubmittedAt = 0f;
        pending.DispatchGeneration++;
        CancelCorrelated(pending.Packet.requestKey);
        ScheduleRetry(observation, pending, now);
        Publish(
            CharacterAcquiredTraitManifestationStatus.TransportTimedOut,
            observation.Actor.Identity.PersistentId,
            pending.Packet,
            pending.Attempts,
            pending.NextAttemptAt,
            CharacterAcquiredTraitInferenceIssueCode.None,
            LocalLlmRequestStatus.TimedOut,
            "Accepted AcquiredTrait request exceeded its bounded callback deadline.");
    }

    private void RemoveObservation(
        string targetPersistentId,
        string reason,
        bool publishDiagnostic)
    {
        string targetId = targetPersistentId?.Trim() ?? string.Empty;
        if (!observations.TryGetValue(targetId, out Observation observation))
            return;
        observation.Progression.Changed -= observation.ChangedHandler;
        PendingDispatch pending = observation.Pending;
        if (pending != null)
        {
            pending.InFlight = false;
            pending.DispatchGeneration++;
            CancelCorrelated(pending.Packet.requestKey);
            if (publishDiagnostic)
            {
                Publish(
                    CharacterAcquiredTraitManifestationStatus.TargetUnavailable,
                    targetId,
                    pending.Packet,
                    pending.Attempts,
                    0f,
                    CharacterAcquiredTraitInferenceIssueCode.None,
                    LocalLlmRequestStatus.Cancelled,
                    reason);
            }
        }
        observations.Remove(targetId);
    }

    private void CancelTransport(PendingDispatch pending)
    {
        if (pending == null)
            return;
        pending.InFlight = false;
        pending.DispatchGeneration++;
        CancelCorrelated(pending.Packet?.requestKey);
    }

    private void CancelCorrelated(string requestKey)
    {
        if (llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
            && runtime is ICorrelatedAcquiredTraitLlmRuntime correlated)
        {
            correlated.CancelAcquiredTraitRequest(requestKey);
        }
    }

    private PendingDispatch CreatePending(
        CharacterProgression progression,
        CharacterAcquiredTraitRequestPacketDto packet,
        int registeredRevision,
        float nextAttemptAt,
        NarrativePublicContextMaterial publicMaterial)
    {
        CharacterAcquiredTraitRequestPacketDto clone = packet?.Clone()
            ?? throw new ArgumentNullException(nameof(packet));
        NarrativePublicPromptEnvelope envelope = CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
            progression,
            clone,
            publicMaterial,
            settings,
            modules);
        return new PendingDispatch
        {
            Packet = clone,
            Prompt = envelope.Prompt,
            PublicMaterial = envelope.Material,
            RegisteredRevision = registeredRevision,
            NextAttemptAt = nextAttemptAt
        };
    }

    private static bool StillOwnsPersistedPending(
        Observation observation,
        PendingDispatch pending)
    {
        CharacterAcquiredTraitAggregateState state = observation.Progression
            .CaptureAcquiredTraitState();
        return state.TryGetPendingRequest(
                pending.Packet.requestId,
                out CharacterAcquiredTraitPendingRequestState persisted)
            && pending.Matches(persisted);
    }

    private void ScheduleRetry(
        Observation observation,
        PendingDispatch pending,
        float now)
    {
        if (pending.Attempts >= 5 && TryMarkAwaitingNarrativeRetry(observation, pending))
        {
            pending.NextAttemptAt = float.PositiveInfinity;
            return;
        }
        int exponent = Math.Min(5, Math.Max(0, pending.Attempts - 1));
        float delay = Math.Min(
            MaximumRetrySeconds,
            InitialRetrySeconds * (1 << exponent));
        pending.NextAttemptAt = now + delay;
    }

    private bool TryMarkAwaitingNarrativeRetry(
        Observation observation,
        PendingDispatch dispatch)
    {
        CharacterAcquiredTraitAggregateState current = observation.Progression
            .CaptureAcquiredTraitState();
        if (!current.TryGetPendingRequest(dispatch.Packet.requestId,
                out CharacterAcquiredTraitPendingRequestState pending)
            || pending.formulaVersion < 1
            || (pending.presentationState != CharacterAcquiredTraitPresentationState.PresentationPending
                && pending.presentationState != CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
            || current.revision == int.MaxValue)
            return false;
        CharacterAcquiredTraitAggregateState candidate = current.Clone();
        candidate.revision = checked(current.revision + 1);
        CharacterAcquiredTraitPendingRequestState candidatePending = candidate.pendingRequests
            .Single(value => value != null && string.Equals(value.requestId,
                dispatch.Packet.requestId, StringComparison.Ordinal));
        candidatePending.presentationFailureCount = 5;
        candidatePending.presentationState = CharacterAcquiredTraitPresentationState.AwaitingNarrativeRetry;
        if (!observation.Progression.TryCommitAcquiredTraitState(
                candidate,
                current.revision,
                settings,
                modules,
                out _))
            return false;
        observation.Pending = null;
        return true;
    }

    private NarrativeInferenceTimestamp CurrentTimestamp() =>
        NarrativeInferenceTimestamp.FromGameTick(
            Math.Max(0L, calendar.AbsoluteHour));

    private static bool IsPublishedLiving(CharacterActor actor) =>
        actor != null
        && actor.HasBeenPublished
        && !actor.IsUnpublishedComposition
        && !actor.IsDetachedRestoreCandidate
        && !actor.IsDead
        && actor.Progression != null
        && actor.Identity != null
        && actor.Identity.TypedPersistentId.IsValid;

    private void Publish(
        CharacterAcquiredTraitManifestationStatus status,
        string targetPersistentId,
        CharacterAcquiredTraitRequestPacketDto packet,
        int attempt,
        float nextRetryAt,
        CharacterAcquiredTraitInferenceIssueCode issueCode,
        LocalLlmRequestStatus? transportStatus,
        string detail)
    {
        CharacterAcquiredTraitManifestationDiagnostic diagnostic = new(
            status,
            targetPersistentId,
            packet?.requestId,
            packet?.requestKey,
            packet?.candidatePacketHash,
            packet?.manifestationMilestone ?? 0,
            attempt,
            nextRetryAt,
            issueCode,
            transportStatus,
            detail);
        if (diagnostic.TargetPersistentId.Length > 0)
            latestDiagnostics[diagnostic.TargetPersistentId] = diagnostic;
        events.Publish(new CharacterAcquiredTraitManifestationDiagnosticEvent(
            diagnostic));
        DiagnosticPublished?.Invoke(diagnostic);
    }

    private void BuildRequestIdentity(
        string targetPersistentId,
        int milestone,
        IEnumerable<string> evidenceFactIds,
        string publicContextSemanticHash,
        out string requestId,
        out string requestKey)
    {
        string seed = settings.SettingsId + "|"
            + targetPersistentId + "|"
            + milestone.ToString(CultureInfo.InvariantCulture) + "|"
            + string.Join("|", evidenceFactIds ?? Array.Empty<string>());
        string hash = NarrativeInferenceHash.ComputeSha256Utf8(
            NarrativePublicContextIdentity.Bind(seed, publicContextSemanticHash));
        const string hashPrefix = "sha256:";
        string suffix = hash.StartsWith(hashPrefix, StringComparison.Ordinal)
            ? hash.Substring(hashPrefix.Length)
            : hash;
        requestId = RequestIdPrefix + suffix;
        requestKey = RequestKeyPrefix + suffix;
    }
}
