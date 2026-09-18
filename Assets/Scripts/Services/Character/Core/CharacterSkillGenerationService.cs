using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public interface ICharacterSkillGenerationService
{
    CharacterSkillDraft CreateDraft(
        CharacterProgression progression,
        CharacterSkillKind kind,
        int unlockLevel,
        int revision = 0);

    void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft);

    void CancelRequests(CharacterProgression progression);

    bool TryValidateResponse(
        CharacterSkillDraft draft,
        string response,
        out List<CharacterSkillInstance> skills,
        out string error);
}

/// <summary>
/// Read-only production diagnostics for asynchronous skill preparation.
/// Population/profile callers use this to distinguish a legitimately pending
/// request from a stalled generator without advancing or completing requests.
/// </summary>
public interface ICharacterSkillGenerationDiagnostics
{
    int PendingRequestCount { get; }
    string LastDiagnostic { get; }
    float RequestTimeoutSeconds { get; }
    bool IsProviderCircuitOpen { get; }
    float ProviderCircuitCooldownRemainingSeconds { get; }
    int ProviderCircuitTripCount { get; }
    NarrativeInferenceAuditRecord? LastInferenceAudit { get; }
}

[Serializable]
public sealed class CharacterSkillGenerationResponseDto : ILlmJsonPayload
{
    public string presentationId = string.Empty;
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;
    [Obsolete("Legacy replay only. The CharacterSkill profile exact-key gate rejects this field.")]
    public List<CharacterSkillCandidateResponseDto> candidates =
        new List<CharacterSkillCandidateResponseDto>();

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(presentationId)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "presentationId, displayName, and narrativeFlavor are required.";
            return false;
        }
        const string prefix = "presentation:skill:";
        if (presentationId.Length != prefix.Length + 64
            || !presentationId.StartsWith(prefix, StringComparison.Ordinal)
            || presentationId.Skip(prefix.Length).Any(character =>
                !Uri.IsHexDigit(character) || char.IsUpper(character)))
        {
            error = "presentationId must be a canonical presentation:skill:<sha256> identifier.";
            return false;
        }
        if ((displayName + narrativeFlavor).Any(character =>
            character is >= '0' and <= '9'
            or >= '０' and <= '９'
            or '%'
            or '％'))
        {
            error = "displayName and narrativeFlavor cannot restate mechanical numbers or percentages.";
            return false;
        }
        return true;
    }
}

[Serializable]
public sealed class CharacterSkillModuleSelectionResponseDto : ILlmJsonPayload
{
    public string selectionId = string.Empty;
    public List<string> positiveModuleIds = new List<string>();
    public List<string> drawbackModuleIds = new List<string>();
    public List<string> evidenceFactIds = new List<string>();
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(selectionId)
            || positiveModuleIds == null
            || drawbackModuleIds == null
            || evidenceFactIds == null
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "The six module-selection response fields are required.";
            return false;
        }
        if ((displayName + narrativeFlavor).Any(character =>
            character is >= '0' and <= '9'
            or >= '０' and <= '９'
            or '%'
            or '％'))
        {
            error = "displayName and narrativeFlavor cannot invent or restate mechanical numbers.";
            return false;
        }
        return true;
    }
}

[Serializable]
public sealed class CharacterSkillLegacyGenerationResponseDto : ILlmJsonPayload
{
    public List<CharacterSkillCandidateResponseDto> candidates =
        new List<CharacterSkillCandidateResponseDto>();

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (candidates == null || candidates.Count == 0 || candidates.Any(value => value == null))
        {
            error = "Legacy candidates are required and cannot contain null.";
            return false;
        }
        return true;
    }
}

[Serializable]
public sealed class CharacterSkillCandidateResponseDto
{
    public string ruleId = string.Empty;
    public string combinationId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string narrativeReason = string.Empty;
}

public sealed class CharacterSkillAllowedCombination
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public int Cost { get; set; }
    public List<CharacterSkillModuleSelection> Modules { get; set; } = new List<CharacterSkillModuleSelection>();

    public string Signature => string.Join(",", Modules
        .Select(module => $"{module.moduleId}|{module.variantId}"));
    public string MechanicalIdentity { get; set; } = string.Empty;
}

public static class CharacterSkillRuleIdentity
{
    public static void Ensure(CharacterSkillDraft draft)
    {
        if (draft?.rules == null)
        {
            return;
        }

        Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (CharacterSkillCandidateRule rule in draft.rules.Where(value => value != null))
        {
            if (draft.kind == CharacterSkillKind.Ultimate
                && rule.ultimateDomain == CharacterUltimateDomain.None)
            {
                bool requested = draft.requestedUltimateDomain != CharacterUltimateDomain.None;
                rule.ultimateDomain = requested
                    ? draft.requestedUltimateDomain
                    : CharacterUltimateDomain.Offense;
                rule.mechanicalPolicySource = requested
                    ? CharacterSkillMechanicalPolicySource.RequestedUltimateDomain
                    : CharacterSkillMechanicalPolicySource.LegacyDeterministicDefault;
                rule.trigger = rule.ultimateDomain switch
                {
                    CharacterUltimateDomain.Defense => CharacterSkillTrigger.InvasionStarted,
                    CharacterUltimateDomain.Management => CharacterSkillTrigger.OperatingDayStarted,
                    _ => CharacterSkillTrigger.ManualCombat
                };
            }
            string signature = CanonicalSignature(draft.kind, rule);
            occurrences.TryGetValue(signature, out int occurrence);
            occurrences[signature] = occurrence + 1;
            if (string.IsNullOrWhiteSpace(rule.ruleId))
            {
                rule.ruleId = "skill-rule:"
                    + NarrativeInferenceHash.ComputeSha256Utf8(
                        (draft.requestKey ?? string.Empty)
                        + "|" + signature
                        + "|duplicate=" + occurrence);
            }
        }
    }

    private static string CanonicalSignature(
        CharacterSkillKind kind,
        CharacterSkillCandidateRule rule)
    {
        return string.Join("|",
            kind,
            rule.rarity,
            rule.budget,
            rule.trigger,
            rule.target,
            rule.targetingMode,
            rule.effectArea,
            rule.areaSize,
            rule.ultimateDomain,
            rule.cooldownTurns,
            rule.manualDurationHours,
            rule.manualCooldownDays,
            string.Join(",", (rule.allowedModuleIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", (rule.allowedVariantIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)));
    }
}

public static class CharacterSkillCombinationCatalog
{
    private sealed class Atom
    {
        public CharacterSkillModuleRule Module;
        public CharacterSkillNumericVariant Variant;
    }

    public static List<CharacterSkillAllowedCombination> Build(
        CharacterSkillCandidateRule rule,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind,
        int maximumCount = 12)
    {
        if (rule == null || settings == null)
        {
            return new List<CharacterSkillAllowedCombination>();
        }

        List<Atom> atoms = rule.allowedModuleIds
            .Select(settings.FindModule)
            .Where(module => module != null
                && module.Allows(kind, rule.trigger, rule.target)
                && CharacterSkillValidation.IsTargetCompatible(module, rule.target)
                && MatchesMechanicalDomain(kind, rule.ultimateDomain, module)
                && !CharacterSkillValidation.WouldSelfTrigger(module, rule.trigger)
                && (module is not CharacterManagementSkillModuleRule
                    || CharacterSkillRuntimeEffects.IsManagementModuleReachable(
                        kind,
                        rule.trigger,
                        module)))
            .SelectMany(module => (module.variants ?? new List<CharacterSkillNumericVariant>())
                .Where(variant => variant != null
                    && variant.cost <= rule.budget
                    && rule.allowedVariantIds.Contains(variant.id, StringComparer.Ordinal))
                .Select(variant => new Atom { Module = module, Variant = variant }))
            .OrderBy(atom => atom.Module.id, StringComparer.Ordinal)
            .ThenBy(atom => atom.Variant.id, StringComparer.Ordinal)
            .ToList();

        Dictionary<string, CharacterSkillAllowedCombination> combinations =
            new Dictionary<string, CharacterSkillAllowedCombination>(StringComparer.Ordinal);
        for (int first = 0; first < atoms.Count; first++)
        {
            AddCombination(combinations, rule, kind, atoms[first]);
            for (int second = first + 1; second < atoms.Count; second++)
            {
                if (SameModule(atoms[first], atoms[second])
                    || (kind == CharacterSkillKind.Ultimate
                        && MixedUltimateDomains(atoms[first], atoms[second])))
                {
                    continue;
                }

                AddCombination(combinations, rule, kind, atoms[first], atoms[second]);
                for (int third = second + 1; third < atoms.Count; third++)
                {
                    if (SameModule(atoms[first], atoms[third])
                        || SameModule(atoms[second], atoms[third])
                        || (kind == CharacterSkillKind.Ultimate
                            && (MixedUltimateDomains(atoms[first], atoms[third])
                                || MixedUltimateDomains(atoms[second], atoms[third]))))
                    {
                        continue;
                    }

                    AddCombination(combinations, rule, kind, atoms[first], atoms[second], atoms[third]);
                }
            }
        }

        List<CharacterSkillAllowedCombination> result = combinations.Values
            .OrderByDescending(combination => combination.Modules.Count)
            .ThenByDescending(combination => combination.Cost)
            .ThenBy(combination => combination.Signature, StringComparer.Ordinal)
            .Take(Mathf.Max(1, maximumCount))
            .ToList();
        return result;
    }

    internal static List<CharacterSkillAllowedCombination> RequireLegalCombinations(
        CharacterSkillCandidateRule rule,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        List<CharacterSkillAllowedCombination> combinations = Build(rule, settings, kind);
        if (combinations.Count == 0)
        {
            throw new InvalidOperationException(
                "CharacterSkill rule has zero legal combinations; ruleId="
                + (rule?.ruleId ?? "<missing>")
                + "; kind=" + kind
                + "; trigger=" + (rule?.trigger.ToString() ?? "<missing>")
                + ".");
        }

        return combinations;
    }

    private static void AddCombination(
        IDictionary<string, CharacterSkillAllowedCombination> combinations,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        params Atom[] atoms)
    {
        int cost = atoms.Sum(atom => atom.Variant.cost);
        if (cost > rule.budget)
        {
            return;
        }

        List<CharacterSkillModuleSelection> modules = atoms
            .OrderBy(atom => atom.Module.id, StringComparer.Ordinal)
            .Select(atom => new CharacterSkillModuleSelection
            {
                moduleId = atom.Module.id,
                variantId = atom.Variant.id
            })
            .ToList();
        string signature = string.Join(",", modules
            .Select(module => $"{module.moduleId}|{module.variantId}"));
        string mechanicalIdentity = string.Join("|",
            kind,
            rule.rarity,
            rule.trigger,
            rule.target,
            rule.targetingMode,
            rule.effectArea,
            rule.areaSize,
            rule.ultimateDomain,
            rule.cooldownTurns,
            rule.manualDurationHours,
            rule.manualCooldownDays,
            signature);
        combinations[signature] = new CharacterSkillAllowedCombination
        {
            Id = "skill-combination:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    (rule.ruleId ?? string.Empty) + "|" + mechanicalIdentity),
            RuleId = rule.ruleId ?? string.Empty,
            Cost = cost,
            Modules = modules,
            MechanicalIdentity = mechanicalIdentity
        };
    }

    private static bool SameModule(Atom left, Atom right)
    {
        return string.Equals(left.Module.id, right.Module.id, StringComparison.Ordinal);
    }

    private static bool MixedUltimateDomains(Atom left, Atom right)
    {
        return (left.Module is CharacterManagementSkillModuleRule)
            != (right.Module is CharacterManagementSkillModuleRule);
    }

    private static bool MatchesMechanicalDomain(
        CharacterSkillKind kind,
        CharacterUltimateDomain domain,
        CharacterSkillModuleRule module)
    {
        if (kind != CharacterSkillKind.Ultimate)
        {
            return true;
        }
        return domain == CharacterUltimateDomain.Management
            ? module is CharacterManagementSkillModuleRule
            : module is not CharacterManagementSkillModuleRule;
    }
}

public sealed class CharacterSkillGenerationService :
    ICharacterSkillGenerationService,
    ICharacterSkillGenerationDiagnostics,
    ITickable
{
    private const int MaximumConcurrentRequests = 2;
    private const int MaximumSubmissionsPerTick = 1;
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterSkillGenerationService.Tick");

    private sealed class PendingRequest
    {
        public CharacterProgression progression;
        public CharacterSkillDraft draft;
        public int attempts;
        public float nextAttemptAt;
        public float submittedAt;
        public bool inFlight;
        public bool cancelled;
        public string correction = string.Empty;
        public string preparedPrompt = string.Empty;
        public NarrativePublicContextMaterial publicMaterial;
        public string transportRequestKey = string.Empty;
    }

    private readonly ICharacterSkillSystemSettingsProvider settingsProvider;
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly IUiClock uiClock;
    private readonly IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery;
    private readonly Dictionary<string, PendingRequest> pending = new Dictionary<string, PendingRequest>();
    private readonly List<PendingRequest> tickBuffer = new List<PendingRequest>();
    private float providerUnhealthyUntil;

    public int PendingRequestCount => pending.Count;
    public string LastDiagnostic { get; private set; } = string.Empty;
    public float RequestTimeoutSeconds => ResolveRequestTimeoutSeconds();
    public bool IsProviderCircuitOpen => IsProviderCircuitOpenAt(uiClock.Time);
    public float ProviderCircuitCooldownRemainingSeconds => Mathf.Max(
        0f,
        providerUnhealthyUntil - uiClock.Time);
    public int ProviderCircuitTripCount { get; private set; }
    public NarrativeInferenceAuditRecord? LastInferenceAudit { get; private set; }

    [Inject]
    public CharacterSkillGenerationService(
        ICharacterSkillSystemSettingsProvider settingsProvider,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IUiClock uiClock,
        IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidenceQuery)
    {
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.outcomeEvidenceQuery = outcomeEvidenceQuery
            ?? throw new ArgumentNullException(nameof(outcomeEvidenceQuery));
    }

    public CharacterSkillGenerationService(
        ICharacterSkillSystemSettingsProvider settingsProvider,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IUiClock uiClock)
    {
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public CharacterSkillDraft CreateDraft(
        CharacterProgression progression,
        CharacterSkillKind kind,
        int unlockLevel,
        int revision = 0)
    {
        if (progression == null)
        {
            throw new ArgumentNullException(nameof(progression));
        }

        CharacterGrowthState growth = progression.GrowthState;
        growth.EnsureCollections();
        settingsProvider.Settings.RequireFormulaCatalog();
        NarrativeFormulaStrengthPolicy formulaPolicy =
            settingsProvider.Settings.RequireFormulaPolicy();
        string actorId = progression.Actor != null
            ? CharacterPersistentIdentity.Require(progression.Actor).Value
            : $"unbound-growth:{growth.generationSeed}";
        List<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidence =
            GameplayOutcomeEvidenceFormulaProjection.CaptureExact(
                progression.Actor == null || outcomeEvidenceQuery == null
                    ? Array.Empty<GameplayOutcomeNarrativeEvidenceSource>()
                    : outcomeEvidenceQuery.GetForCharacter(
                        actorId, 24, 0f, includeCompacted: false));
        int formulaBudget = CharacterSkillFormulaGeneration
            .CalculateNarrativeBudget(
                progression,
                settingsProvider.Settings,
                outcomeEvidence);

        string requestKey = $"skill:{actorId}:{kind}:{Mathf.Max(1, unlockLevel)}:r{Mathf.Max(0, revision)}";
        IRandomStream random = new DeterministicRandomSequence(
            CharacterGrowthRules.StableHash(requestKey));
        CharacterSkillDraft draft = new CharacterSkillDraft
        {
            unlockLevel = Mathf.Max(1, unlockLevel),
            kind = kind,
            requestKey = requestKey,
            requestedUltimateDomain = CharacterUltimateDomain.None,
            formulaVersion = formulaPolicy.FormulaVersion,
            formulaCatalogSha256 = settingsProvider.Settings.formulaPolicy.RequireCatalogSha256()
        };

        int candidateCount = kind == CharacterSkillKind.Active ? 3 : 1;
        for (int i = 0; i < candidateCount; i++)
        {
            CharacterSkillRarity rarity = kind switch
            {
                CharacterSkillKind.Active => CharacterGrowthRules.RollRarity(
                    settingsProvider.Settings,
                    growth.potentialGrade,
                    growth.nextActiveDraftHasPity,
                    random),
                CharacterSkillKind.Passive => unlockLevel >= 25
                    ? CharacterSkillRarity.Rare
                    : CharacterSkillRarity.Advanced,
                CharacterSkillKind.Ultimate => CharacterSkillRarity.Legendary,
                _ => CharacterSkillRarity.Common
            };
            draft.rules.Add(CreateCandidateRule(
                progression,
                kind,
                rarity,
                draft.requestedUltimateDomain,
                formulaBudget,
                random));
        }
        CharacterSkillRuleIdentity.Ensure(draft);
        if (formulaPolicy.FormulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion)
        {
            CharacterSkillFormulaGeneration.InitializeModuleSelection(
                progression, draft, settingsProvider.Settings, outcomeEvidence);
        }
        else
        {
            CharacterSkillFormulaGeneration.FreezeMechanics(
                progression, draft, settingsProvider.Settings);
        }

        if (kind == CharacterSkillKind.Active)
        {
            draft.grantsUpperRarityPity = draft.rules.All(rule => rule.rarity < CharacterSkillRarity.Rare);
        }

        return draft;
    }

    public void RequestDraft(CharacterProgression progression, CharacterSkillDraft draft)
    {
        if (progression == null
            || draft == null
            || draft.isReady
            || draft.permanentlyChosen
            || string.IsNullOrWhiteSpace(draft.requestKey))
        {
            return;
        }
        CharacterSkillRuleIdentity.Ensure(draft);
        bool moduleSelection = draft.formulaVersion >=
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        int expectedCount = draft.kind == CharacterSkillKind.Active ? 3 : 1;
        bool invalidSelectionState = moduleSelection
            ? draft.moduleSelectionOffers == null
                || draft.moduleSelectionOffers.Count != expectedCount
                || draft.nextPresentationIndex < 0
                || draft.nextPresentationIndex >= draft.moduleSelectionOffers.Count
                || (draft.candidates?.Count ?? 0) != draft.nextPresentationIndex
                || (draft.frozenMechanics?.Count ?? 0) != draft.nextPresentationIndex
            : draft.frozenMechanics == null
                || draft.frozenMechanics.Count != expectedCount
                || draft.nextPresentationIndex < 0
                || draft.nextPresentationIndex >= draft.frozenMechanics.Count
                || (draft.candidates?.Count ?? 0) != draft.nextPresentationIndex
                || (draft.kind == CharacterSkillKind.Active
                    && draft.frozenMechanics.Select(value => value?.formulaCapabilities?.FirstOrDefault()?.capabilityId)
                        .Distinct(StringComparer.Ordinal).Count() != 3);
        if (draft.formulaVersion <= 0 || invalidSelectionState)
        {
            throw new InvalidOperationException(
                "CharacterSkill request state is incomplete or stale; legacy variants are load-only.");
        }
        if (draft.presentationState == CharacterSkillPresentationState.AwaitingNarrativeRetry)
        {
            draft.presentationState = CharacterSkillPresentationState.PresentationPending;
            draft.presentationFailureCount = 0;
        }

        if (!pending.TryGetValue(draft.requestKey, out PendingRequest request))
        {
            if (!TryBuildLivePublicMaterial(
                    progression,
                    out NarrativePublicContextMaterial publicMaterial,
                    out string unavailableReason))
            {
                RegisterPresentationFailure(
                    new PendingRequest { progression = progression, draft = draft },
                    unavailableReason,
                    removeExisting: false);
                return;
            }

            request = new PendingRequest
            {
                progression = progression,
                draft = draft,
                nextAttemptAt = uiClock.Time,
                publicMaterial = publicMaterial
            };
            request.transportRequestKey = BuildTransportRequestKey(request);
            pending.Add(draft.requestKey, request);
        }
        else
        {
            if (!ReferenceEquals(request.progression, progression)
                || !ReferenceEquals(request.draft, draft))
            {
                request.cancelled = true;
                if (request.inFlight
                    && llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
                    && runtime is ICorrelatedCharacterSkillLlmRuntime correlatedRuntime)
                    correlatedRuntime.CancelCharacterSkillRequest(request.transportRequestKey);
                request.progression?.MarkGenerationRequestCompleted(request.draft?.requestKey);
                RemoveRequest(draft.requestKey);
                if (!TryBuildLivePublicMaterial(
                        progression,
                        out NarrativePublicContextMaterial publicMaterial,
                        out string unavailableReason))
                {
                    RegisterPresentationFailure(
                        new PendingRequest { progression = progression, draft = draft },
                        unavailableReason,
                        removeExisting: false);
                    return;
                }

                request = new PendingRequest
                {
                    progression = progression,
                    draft = draft,
                    nextAttemptAt = uiClock.Time,
                    publicMaterial = publicMaterial
                };
                request.transportRequestKey = BuildTransportRequestKey(request);
                pending.Add(draft.requestKey, request);
            }

            request.progression = progression;
            request.draft = draft;
        }

        progression.MarkGenerationRequestPending(draft.requestKey);
        draft.requestSubmitted = true;
    }

    private static bool TryBuildLivePublicMaterial(
        CharacterProgression progression,
        out NarrativePublicContextMaterial publicMaterial,
        out string unavailableReason)
    {
        try
        {
            publicMaterial = CharacterSkillPromptBuilder.BuildPublicMaterial(progression);
            unavailableReason = string.Empty;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            publicMaterial = null;
            unavailableReason = string.IsNullOrWhiteSpace(exception.Message)
                ? "Public narrative context is not currently readable."
                : exception.Message.Trim();
            return false;
        }
    }

    private static string NormalizeDiagnostic(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? "<none>" : normalized.Replace("\r", " ").Replace("\n", " ");
    }

    public void CancelRequests(CharacterProgression progression)
    {
        if (progression == null)
        {
            return;
        }

        ICorrelatedCharacterSkillLlmRuntime correlatedRuntime =
            llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
                ? runtime as ICorrelatedCharacterSkillLlmRuntime
                : null;
        foreach (KeyValuePair<string, PendingRequest> pair in pending
            .Where(pair => pair.Value?.progression == progression)
            .ToArray())
        {
            pair.Value.cancelled = true;
            correlatedRuntime?.CancelCharacterSkillRequest(
                pair.Value.transportRequestKey);
            pending.Remove(pair.Key);
            pair.Value.draft.requestSubmitted = false;
            pair.Value.progression.MarkGenerationRequestCompleted(pair.Key);
        }
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (pending.Count == 0)
        {
            return;
        }

        float now = uiClock.Time;
        if (IsProviderCircuitOpenAt(now))
        {
            return;
        }

        int inFlightCount = 0;
        tickBuffer.Clear();
        foreach (PendingRequest request in pending.Values)
        {
            tickBuffer.Add(request);
            if (request?.inFlight == true)
            {
                inFlightCount++;
            }
        }

        int submissionCount = 0;
        for (int index = 0; index < tickBuffer.Count; index++)
        {
            PendingRequest request = tickBuffer[index];
            if (request == null
                || request.cancelled
                || request.progression == null
                || request.draft == null
                || request.draft.isReady)
            {
                RemoveRequest(request?.draft?.requestKey);
                continue;
            }

            if (request.inFlight
                && HasTimedOut(request, now))
            {
                OpenProviderCircuit(request, now);
                return;
            }

            if (!request.inFlight
                && now >= request.nextAttemptAt
                && inFlightCount < MaximumConcurrentRequests
                && submissionCount < MaximumSubmissionsPerTick)
            {
                submissionCount++;
                TrySubmit(request, now);
                if (request.inFlight)
                {
                    inFlightCount++;
                }
            }
        }
    }

    public bool TryValidateResponse(
        CharacterSkillDraft draft,
        string response,
        out List<CharacterSkillInstance> skills,
        out string error)
    {
        skills = new List<CharacterSkillInstance>();
        error = string.Empty;
        if (draft == null || draft.formulaVersion <= 0)
        {
            error = "A formula draft is required; legacy candidate selection is load-only.";
            return false;
        }
        if (draft.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion)
            return TryValidateModuleSelectionResponse(draft, response, out skills, out error);
        if (draft.frozenMechanics == null || draft.frozenMechanics.Count == 0)
        {
            error = "A frozen legacy formula draft is required.";
            return false;
        }
        if (draft.nextPresentationIndex < 0
            || draft.nextPresentationIndex >= draft.frozenMechanics.Count)
        {
            error = "The formula draft has no pending presentation slot.";
            return false;
        }

        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.CharacterSkill.Id,
                response,
                out _,
                out error)
            || !LlmJsonResponseParser.TryParse(
            response,
            out CharacterSkillGenerationResponseDto payload,
            out error))
        {
            return false;
        }
        CharacterSkillInstance frozen = draft.frozenMechanics[draft.nextPresentationIndex];
        if (frozen == null
            || frozen.formulaVersion != draft.formulaVersion
            || !string.Equals(
                frozen.formulaCatalogSha256,
                draft.formulaCatalogSha256,
                StringComparison.Ordinal)
            || !string.Equals(payload.presentationId, frozen.presentationId, StringComparison.Ordinal))
        {
            error = "The presentationId is stale or does not match the frozen mechanics.";
            return false;
        }
        string displayName = payload.displayName?.Trim() ?? string.Empty;
        string narrativeFlavor = payload.narrativeFlavor?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > 14
            || narrativeFlavor.Length == 0 || narrativeFlavor.Length > 90)
        {
            error = "Presentation text is missing or exceeds the player-facing length limit.";
            return false;
        }
        string visibleText = displayName + " " + narrativeFlavor;
        if (visibleText.IndexOf("LLM", StringComparison.OrdinalIgnoreCase) >= 0
            || visibleText.Contains("생성 중")
            || visibleText.Contains("요청 키"))
        {
            error = "Technical generation text cannot appear in a skill presentation.";
            return false;
        }
        CharacterSkillInstance skill = frozen.Clone();
        CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
            skill,
            settingsProvider.Settings);
        skill.displayName = displayName;
        skill.narrativeFlavor = narrativeFlavor;
        skill.description = skill.mechanicalDescription;
        skill.narrativeReason = narrativeFlavor;
        skills.Add(skill);
        return true;
    }

    private bool TryValidateModuleSelectionResponse(
        CharacterSkillDraft draft,
        string response,
        out List<CharacterSkillInstance> skills,
        out string error)
    {
        skills = new List<CharacterSkillInstance>();
        error = string.Empty;
        if (draft.nextPresentationIndex < 0
            || draft.nextPresentationIndex >= (draft.moduleSelectionOffers?.Count ?? 0))
        {
            error = "The formula draft has no pending module-selection slot.";
            return false;
        }
        if (!LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id, response,
                out CharacterSkillModuleSelectionResponseDto payload, out error))
            return false;
        string displayName = payload.displayName?.Trim() ?? string.Empty;
        string narrativeFlavor = payload.narrativeFlavor?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > 14
            || narrativeFlavor.Length == 0 || narrativeFlavor.Length > 90)
        {
            error = "Presentation text is missing or exceeds the player-facing length limit.";
            return false;
        }
        try
        {
            CharacterSkillInstance skill = CharacterSkillFormulaGeneration.ResolveModuleSelection(
                draft,
                new NarrativeFormulaModuleSelectionChoice(
                    payload.selectionId, payload.positiveModuleIds,
                    payload.drawbackModuleIds, payload.evidenceFactIds),
                settingsProvider.Settings);
            skill.displayName = displayName;
            skill.narrativeFlavor = narrativeFlavor;
            skill.description = skill.mechanicalDescription;
            skill.narrativeReason = narrativeFlavor;
            skills.Add(skill);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                         or ArgumentException
                                         or OverflowException)
        {
            error = exception.Message;
            return false;
        }
    }

    private CharacterSkillCandidateRule CreateCandidateRule(
        CharacterProgression progression,
        CharacterSkillKind kind,
        CharacterSkillRarity rarity,
        CharacterUltimateDomain requestedUltimateDomain,
        int formulaBudget,
        IRandomStream random)
    {
        CharacterSkillTrigger trigger;
        CharacterSkillTarget target;
        CharacterUltimateDomain ultimateDomain = CharacterUltimateDomain.None;
        CharacterSkillTargetingMode ruleTargetingMode =
            CharacterSkillTargetingMode.Self;
        CharacterSkillEffectArea ruleEffectArea =
            CharacterSkillEffectArea.Single;
        int ruleAreaSize = 1;
        int ruleManualDurationHours = 0;
        CharacterSkillMechanicalPolicySource policySource =
            CharacterSkillMechanicalPolicySource.AuthoredRule;
        if (kind == CharacterSkillKind.Active)
        {
            CharacterSkillTrigger narrativeTrigger =
                CharacterGrowthRules.ChoosePassiveTrigger(
                    progression.NarrativeLedger,
                    random);
            bool manualWork = narrativeTrigger is CharacterSkillTrigger.WorkStarted
                or CharacterSkillTrigger.WorkCompleted;
            if (manualWork)
            {
                trigger = CharacterSkillTrigger.ManualWork;
                List<CharacterSkillTargetingMode> affordableModes = new();
                if (CountAffordableManualCapabilities(
                        CharacterSkillTarget.Self,
                        CharacterSkillEffectArea.Single,
                        1,
                        formulaBudget) >= 3)
                    affordableModes.Add(CharacterSkillTargetingMode.Self);
                if (CountAffordableManualCapabilities(
                        CharacterSkillTarget.Ally,
                        CharacterSkillEffectArea.Single,
                        1,
                        formulaBudget) >= 3)
                {
                    affordableModes.Add(CharacterSkillTargetingMode.PlayerSelected);
                    affordableModes.Add(CharacterSkillTargetingMode.DeterministicRandom);
                }
                if (CountAffordableManualCapabilities(
                        CharacterSkillTarget.Ally,
                        CharacterSkillEffectArea.Dungeon,
                        1,
                        formulaBudget) >= 3)
                    affordableModes.Add(CharacterSkillTargetingMode.AllEligible);
                if (affordableModes.Count == 0)
                    throw new InvalidOperationException(
                        "ManualWork active rules cannot afford three distinct capabilities.");
                ruleTargetingMode = affordableModes[
                    random.NextInt(0, affordableModes.Count)];
                target = ruleTargetingMode == CharacterSkillTargetingMode.Self
                    ? CharacterSkillTarget.Self
                    : CharacterSkillTarget.Ally;
                if (ruleTargetingMode == CharacterSkillTargetingMode.Self)
                {
                    ruleEffectArea = CharacterSkillEffectArea.Single;
                    ruleAreaSize = 1;
                }
                else if (ruleTargetingMode == CharacterSkillTargetingMode.AllEligible)
                {
                    ruleEffectArea = CharacterSkillEffectArea.Dungeon;
                    ruleAreaSize = 1;
                }
                else
                {
                    (CharacterSkillEffectArea Area, int Size)[] affordableAreas =
                    {
                        (CharacterSkillEffectArea.Single, 1),
                        (CharacterSkillEffectArea.Room, 1),
                        (CharacterSkillEffectArea.Square, 3),
                        (CharacterSkillEffectArea.Square, 5),
                        (CharacterSkillEffectArea.Square, 7)
                    };
                    affordableAreas = affordableAreas.Where(value =>
                            CountAffordableManualCapabilities(
                                target,
                                value.Area,
                                value.Size,
                                formulaBudget) >= 3)
                        .ToArray();
                    if (affordableAreas.Length == 0)
                        throw new InvalidOperationException(
                            "ManualWork active rules cannot afford three distinct capabilities.");
                    (ruleEffectArea, ruleAreaSize) = affordableAreas[
                        random.NextInt(0, affordableAreas.Length)];
                }
                ruleManualDurationHours = GameCalendarRules.HoursPerDay;
            }
            else
            {
                trigger = CharacterSkillTrigger.ManualCombat;
                CharacterSkillTarget[] targets =
                {
                    CharacterSkillTarget.Enemy,
                    CharacterSkillTarget.Self,
                    CharacterSkillTarget.Ally
                };
                target = targets[random.NextInt(0, targets.Length)];
                ruleTargetingMode = target == CharacterSkillTarget.Self
                    ? CharacterSkillTargetingMode.Self
                    : CharacterSkillTargetingMode.PlayerSelected;
            }
        }
        else if (kind == CharacterSkillKind.Ultimate)
        {
            bool hasRequestedDomain = requestedUltimateDomain != CharacterUltimateDomain.None;
            ultimateDomain = hasRequestedDomain
                ? requestedUltimateDomain
                : CharacterUltimateDomain.Offense;
            policySource = hasRequestedDomain
                ? CharacterSkillMechanicalPolicySource.RequestedUltimateDomain
                : CharacterSkillMechanicalPolicySource.LegacyDeterministicDefault;
            trigger = ultimateDomain switch
            {
                CharacterUltimateDomain.Defense => CharacterSkillTrigger.InvasionStarted,
                CharacterUltimateDomain.Management => CharacterSkillTrigger.OperatingDayStarted,
                _ => CharacterSkillTrigger.ManualCombat
            };
            // Management ultimates are consumed by owner-management runtime
            // paths, so their formula modules must target the owner rather
            // than the combat-only Enemy default used by offense/defense.
            target = ultimateDomain == CharacterUltimateDomain.Management
                ? CharacterSkillTarget.Self
                : CharacterSkillTarget.Enemy;
        }
        else
        {
            trigger = CharacterGrowthRules.ChoosePassiveTrigger(progression.NarrativeLedger, random);
            target = CharacterSkillTarget.Self;
        }

        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            rarity = rarity,
            budget = formulaBudget,
            trigger = trigger,
            target = target,
            targetingMode = ruleTargetingMode,
            effectArea = ruleEffectArea,
            areaSize = ruleAreaSize,
            ultimateDomain = ultimateDomain,
            cooldownTurns = 0,
            manualDurationHours = ruleManualDurationHours,
            manualCooldownDays = trigger == CharacterSkillTrigger.ManualWork ? 1 : 0,
            mechanicalPolicySource = policySource
        };
        CharacterSkillFormationRules.Resolve(
            target,
            Array.Empty<CharacterSkillModuleSelection>(),
            out rule.usableFrom,
            out rule.targetPositions);
        IEnumerable<CharacterSkillModuleRule> available = settingsProvider.Settings.Modules
            .Where(module => module != null
                && module.Allows(kind, trigger, target)
                && CharacterSkillValidation.IsTargetCompatible(module, target)
                && CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    kind, rule, module))
            .Where(module => kind != CharacterSkillKind.Ultimate
                || (ultimateDomain == CharacterUltimateDomain.Management
                    ? module is CharacterManagementSkillModuleRule
                    : module is not CharacterManagementSkillModuleRule));
        available = available.Where(module => !CharacterSkillValidation.WouldSelfTrigger(
            module,
            trigger));
        foreach (CharacterSkillModuleRule module in available)
        {
            rule.allowedModuleIds.Add(module.id);
        }

        rule.allowedModuleIds = rule.allowedModuleIds.Distinct(StringComparer.Ordinal).ToList();
        rule.allowedVariantIds = rule.allowedVariantIds.Distinct(StringComparer.Ordinal).ToList();
        return rule;
    }

    private int CountAffordableManualCapabilities(
        CharacterSkillTarget target,
        CharacterSkillEffectArea area,
        int areaSize,
        int budget)
    {
        NarrativeFormulaGenerationCostContext authored = settingsProvider.Settings
            .formulaPolicy.RequireGenerationCostContext(
                CharacterSkillTrigger.ManualWork,
                target);
        NarrativeFormulaGenerationCostContext context = new(
            authored.TriggerFrequencyUnits,
            authored.GuaranteedProc,
            CharacterSkillAreaRules.ResolveCostTargetCount(
                area,
                areaSize,
                authored.TargetCount));
        return settingsProvider.Settings.Modules
            .Where(module => module != null
                && module.Allows(
                    CharacterSkillKind.Active,
                    CharacterSkillTrigger.ManualWork,
                    target)
                && CharacterSkillValidation.IsTargetCompatible(module, target)
                && CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    CharacterSkillKind.Active,
                    CharacterSkillTrigger.ManualWork,
                    CharacterUltimateDomain.None,
                    module)
                && !CharacterSkillValidation.WouldSelfTrigger(
                    module,
                    CharacterSkillTrigger.ManualWork)
                && CharacterSkillRuntimeEffects.IsManagementModuleReachable(
                    CharacterSkillKind.Active,
                    CharacterSkillTrigger.ManualWork,
                    module))
            .Select(module => settingsProvider.Settings.RequireFormulaDescriptor(module))
            .Where(descriptor => descriptor.CalculateContextCost(context, 1) <= budget)
            .Select(descriptor => descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private void TrySubmit(PendingRequest request, float now)
    {
        if (request.publicMaterial == null
            && !TryBuildLivePublicMaterial(
                request.progression,
                out request.publicMaterial,
                out string unavailableReason))
        {
            RegisterPresentationFailure(request, unavailableReason, removeExisting: false);
            return;
        }
        if (string.IsNullOrWhiteSpace(request.transportRequestKey))
            request.transportRequestKey = BuildTransportRequestKey(request);
        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            RegisterPresentationFailure(request, "runtime-unavailable", removeExisting: false);
            return;
        }

        request.inFlight = true;
        request.submittedAt = now;
        if (string.IsNullOrEmpty(request.preparedPrompt))
        {
            request.preparedPrompt = CharacterSkillPromptBuilder.BuildEnvelope(
                request.progression,
                request.draft,
                settingsProvider.Settings,
                request.publicMaterial,
                request.correction).Prompt;
        }

        string prompt = request.preparedPrompt;
        LastDiagnostic = $"submitted={request.draft.requestKey}; attempt={request.attempts + 1}; prompt={prompt.Length}";
        bool moduleSelection = request.draft.formulaVersion >=
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        bool accepted;
        if (moduleSelection)
        {
            accepted = runtime is ICharacterSkillModuleSelectionLlmRuntime selectionRuntime
                && selectionRuntime.GenerateCharacterSkillModuleSelectionAsync(
                    request.transportRequestKey,
                    prompt,
                    result => HandleResult(request, result));
        }
        else
        {
            accepted = runtime is ICorrelatedCharacterSkillLlmRuntime correlatedRuntime
                ? correlatedRuntime.GenerateCharacterSkillAsync(
                    request.transportRequestKey,
                    prompt,
                    result => HandleResult(request, result))
                : runtime.GenerateCharacterSkillAsync(prompt, result => HandleResult(request, result));
        }
        if (!accepted)
        {
            request.inFlight = false;
            request.submittedAt = 0f;
            RecordAudit(
                request,
                false,
                "Character skill request was not accepted.",
                false,
                string.Empty,
                null);
            RegisterPresentationFailure(
                request,
                "Character skill request was not accepted.",
                removeExisting: false);
        }
    }

    private void HandleResult(PendingRequest request, LocalLlmResult result)
    {
        if (request == null || request.draft == null || request.progression == null)
        {
            return;
        }

        if (request.cancelled)
        {
            return;
        }

        if (result.Status == LocalLlmRequestStatus.TimedOut)
        {
            OpenProviderCircuit(request, uiClock.Time);
            return;
        }

        request.inFlight = false;
        request.submittedAt = 0f;
        List<CharacterSkillInstance> skills = null;
        string validationError = string.Empty;
        bool valid = result.IsSuccess
            && TryValidateResponse(
                request.draft,
                result.Content,
                out skills,
                out validationError);
        if (valid
            && !TryValidateNarrativeText(request.progression, skills, out validationError))
        {
            valid = false;
        }

        if (valid)
        {
            foreach (CharacterSkillInstance skill in skills)
            {
                skill.narrativeTrace = result.NarrativeTrace;
                request.draft.candidates.Add(skill);
                if (request.draft.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion)
                    request.draft.frozenMechanics.Add(skill.Clone());
            }
            RecordAudit(request, true, string.Empty, false, string.Empty, skills);
            request.draft.nextPresentationIndex++;
            int expectedCount = request.draft.formulaVersion >=
                    CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                ? request.draft.moduleSelectionOffers.Count
                : request.draft.frozenMechanics.Count;
            if (request.draft.nextPresentationIndex < expectedCount)
            {
                request.attempts = 0;
                request.correction = string.Empty;
                request.preparedPrompt = string.Empty;
                request.transportRequestKey = BuildTransportRequestKey(request);
                request.nextAttemptAt = uiClock.Time;
                LastDiagnostic = $"presentation-ready={request.draft.requestKey}; next={request.draft.nextPresentationIndex}";
                return;
            }

            request.draft.isReady = true;
            request.draft.presentationState = CharacterSkillPresentationState.Ready;
            request.draft.requestSubmitted = false;
            request.progression.MarkGenerationRequestCompleted(request.draft.requestKey);
            RemoveRequest(request.draft.requestKey);
            request.progression.OnDraftReady(request.draft);
            LastDiagnostic = $"ready={request.draft.requestKey}; candidates={request.draft.candidates.Count}";
            return;
        }

        if (result.IsSuccess)
        {
            request.correction = validationError;
            request.preparedPrompt = string.Empty;
            LastDiagnostic = $"rejected={request.draft.requestKey}; reason={validationError}; response={result.Content.Length}";
        }
        else
        {
            LastDiagnostic = $"failed={request.draft.requestKey}; status={result.Status}; error={result.Error}";
        }
        RecordAudit(
            request,
            false,
            result.IsSuccess ? validationError : result.Error,
            false,
            string.Empty,
            skills);
        RegisterPresentationFailure(
            request,
            result.IsSuccess ? validationError : "provider-result-" + result.Status,
            removeExisting: false);
    }

    private static bool TryValidateNarrativeText(
        CharacterProgression progression,
        IEnumerable<CharacterSkillInstance> skills,
        out string error)
    {
        error = string.Empty;
        string characterName = progression?.Actor?.Identity?.DisplayName;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            characterName = progression?.GrowthState?.displayName;
        }

        foreach (CharacterSkillInstance skill in skills ?? Enumerable.Empty<CharacterSkillInstance>())
        {
            string displayName = skill?.displayName ?? string.Empty;
            string description = skill?.description ?? string.Empty;
            string reason = skill?.narrativeReason ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(characterName)
                && displayName.IndexOf(characterName, StringComparison.Ordinal) < 0
                && description.IndexOf(characterName, StringComparison.Ordinal) < 0
                && reason.IndexOf(characterName, StringComparison.Ordinal) < 0)
            {
                error = $"Every skill must mention the character name '{characterName}' in its player-facing text.";
                return false;
            }

            if (string.Equals(description.Trim(), reason.Trim(), StringComparison.Ordinal))
            {
                error = "Skill description and narrative reason must not repeat the same sentence.";
                return false;
            }
        }

        return true;
    }

    private void RegisterPresentationFailure(
        PendingRequest request,
        string reason,
        bool removeExisting)
    {
        if (request?.draft == null || request.progression == null)
            throw new InvalidOperationException("Presentation failure lost its owning draft.");
        request.inFlight = false;
        request.submittedAt = 0f;
        request.draft.presentationFailureCount++;
        request.attempts++;
        request.correction = reason?.Trim() ?? string.Empty;
        request.preparedPrompt = string.Empty;
        LastDiagnostic = $"presentation-failed={request.draft.requestKey}; count={request.draft.presentationFailureCount}; reason={NormalizeDiagnostic(reason)}";
        if (request.draft.presentationFailureCount >= 5)
        {
            request.cancelled = true;
            request.draft.presentationState = CharacterSkillPresentationState.AwaitingNarrativeRetry;
            request.draft.requestSubmitted = false;
            request.progression.MarkGenerationRequestCompleted(request.draft.requestKey);
            RemoveRequest(request.draft.requestKey);
            return;
        }
        request.draft.presentationState = CharacterSkillPresentationState.PresentationPending;
        if (!pending.ContainsKey(request.draft.requestKey))
        {
            pending.Add(request.draft.requestKey, request);
            request.progression.MarkGenerationRequestPending(request.draft.requestKey);
            request.draft.requestSubmitted = true;
        }
        if (removeExisting)
        {
            request.transportRequestKey = BuildTransportRequestKey(request);
        }
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        request.nextAttemptAt = uiClock.Time + Mathf.Min(
            settings.maximumRetrySeconds,
            settings.initialRetrySeconds * Mathf.Pow(2f, Mathf.Min(8, request.attempts - 1)));
    }

    private static string BuildTransportRequestKey(PendingRequest request)
    {
        if (request.draft.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion)
        {
            CharacterSkillModuleOfferState offer =
                request.draft.moduleSelectionOffers[request.draft.nextPresentationIndex];
            return NarrativePublicContextIdentity.Bind(
                request.draft.requestKey + ":" + offer.selectionId,
                request.publicMaterial.SemanticHash);
        }
        CharacterSkillInstance frozen = request.draft.frozenMechanics[request.draft.nextPresentationIndex];
        return NarrativePublicContextIdentity.Bind(
            request.draft.requestKey + ":" + frozen.presentationId,
            request.publicMaterial.SemanticHash);
    }

    private bool HasTimedOut(PendingRequest request, float now)
    {
        if (request == null || !request.inFlight)
        {
            return false;
        }

        return now - request.submittedAt >= ResolveRequestTimeoutSeconds();
    }

    private float ResolveRequestTimeoutSeconds()
    {
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        return Mathf.Max(
            settings.initialRetrySeconds,
            settings.maximumRetrySeconds);
    }

    private bool IsProviderCircuitOpenAt(float now) =>
        now < providerUnhealthyUntil;

    private float ResolveProviderCircuitCooldownSeconds()
    {
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        return Mathf.Max(
            ResolveRequestTimeoutSeconds(),
            settings.maximumRetrySeconds);
    }

    private void OpenProviderCircuit(PendingRequest timedOutRequest, float now)
    {
        if (timedOutRequest?.draft == null
            || timedOutRequest.progression == null)
        {
            throw new InvalidOperationException(
                "Provider timeout did not retain a complete skill-generation request.");
        }

        float cooldown = ResolveProviderCircuitCooldownSeconds();
        providerUnhealthyUntil = Mathf.Max(
            providerUnhealthyUntil,
            now + cooldown);
        ProviderCircuitTripCount++;
        RecordAudit(
            timedOutRequest,
            false,
            "Accepted CharacterSkill presentation request timed out.",
            false,
            string.Empty,
            null);
        RegisterPresentationFailure(
            timedOutRequest,
            "accepted-request-timeout",
            removeExisting: false);
    }

    private void RemoveRequest(string requestKey)
    {
        if (!string.IsNullOrWhiteSpace(requestKey))
        {
            pending.Remove(requestKey);
        }
    }

    private void RecordAudit(
        PendingRequest request,
        bool succeeded,
        string validationError,
        bool fallbackUsed,
        string fallbackReason,
        IReadOnlyCollection<CharacterSkillInstance> skills)
    {
        if (request?.draft == null)
        {
            return;
        }
        string targetId = request.progression?.Actor?.Identity?.PersistentId
            ?? request.progression?.GrowthState?.displayName
            ?? string.Empty;
        string packet = request.draft.formulaVersion >=
                CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
            ? CharacterSkillPromptBuilder.BuildModuleSelectionPacket(
                request.draft, settingsProvider.Settings)
            : CharacterSkillPromptBuilder.BuildPresentationPacket(request.draft);
        string selectedIds = skills == null
            ? string.Empty
            : string.Join(",", skills
                .Where(skill => skill != null)
                .Select(skill => skill.presentationId));
        LastInferenceAudit = new NarrativeInferenceAuditRecord(
            request.draft.formulaVersion >= CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion
                ? LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                : LocalLlmRequestProfiles.CharacterSkill.Id,
            request.transportRequestKey,
            NarrativeInferenceHash.ComputeSha256Utf8(packet),
            succeeded,
            validationError,
            fallbackUsed,
            fallbackReason,
            selectedIds,
            -1,
            targetId,
            NarrativeInferenceTimestamp.FromUtc(DateTime.UtcNow));
    }

}

public static class CharacterSkillValidation
{
    public static bool IsTargetCompatible(
        string capabilityId,
        CharacterSkillTarget target)
    {
        return CharacterSkillModuleCapabilityRegistry
            .Require(capabilityId)
            .IsTargetCompatible(target);
    }

    public static bool IsTargetCompatible(
        CharacterSkillModuleRule module,
        CharacterSkillTarget target)
    {
        return CharacterSkillModuleCapabilityRegistry
            .Require(module)
            .IsTargetCompatible(target);
    }

    public static bool WouldSelfTrigger(
        string capabilityId,
        CharacterSkillTrigger trigger)
    {
        return CharacterSkillModuleCapabilityRegistry
            .Require(capabilityId)
            .WouldSelfTrigger(trigger);
    }

    public static bool WouldSelfTrigger(
        CharacterSkillModuleRule module,
        CharacterSkillTrigger trigger)
    {
        return CharacterSkillModuleCapabilityRegistry
            .Require(module)
            .WouldSelfTrigger(trigger);
    }
}

public static class CharacterSkillPromptBuilder
{
    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression)
    {
        return NarrativeRequestContextBuilder.BuildPublicMaterialForProgression(
            LocalLlmRequestProfiles.CharacterSkill.Id,
            progression,
            requireCharacterFact: true,
            requireMotif: true);
    }

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        CharacterProgression progression,
        IEnumerable<NarrativeLedgerPublicationDescriptor> ledgerPublicationDescriptors)
    {
        if (ledgerPublicationDescriptors == null)
            throw new ArgumentNullException(nameof(ledgerPublicationDescriptors));
        return NarrativeRequestContextBuilder.BuildPublicMaterialForProgression(
            LocalLlmRequestProfiles.CharacterSkill.Id,
            progression,
            requireCharacterFact: true,
            requireMotif: true,
            requiredOriginalFactIds: null,
            ledgerPublicationDescriptors);
    }

    public static string Build(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string correction = "")
    {
        return BuildEnvelope(
            progression,
            draft,
            settings,
            BuildPublicMaterial(progression),
            correction).Prompt;
    }

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string correction = "")
    {
        return BuildEnvelope(
            progression,
            draft,
            settings,
            BuildPublicMaterial(progression),
            correction);
    }

    public static NarrativePublicPromptEnvelope BuildEnvelope(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        NarrativePublicContextMaterial publicMaterial,
        string correction = "")
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (publicMaterial == null) throw new ArgumentNullException(nameof(publicMaterial));
        string subjectId = progression.Actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (!string.Equals(
                publicMaterial.ProfileId,
                LocalLlmRequestProfiles.CharacterSkill.Id,
                StringComparison.Ordinal)
            || publicMaterial.SubjectKind != NarrativePublicSubjectKind.Character
            || !string.Equals(publicMaterial.SubjectId, subjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Character skill prompt material does not match its target character.");
        }
        bool moduleSelection = draft.formulaVersion >=
            CharacterSkillFormulaGeneration.ModuleSelectionFormulaVersion;
        bool hasPending = moduleSelection
            ? draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < (draft.moduleSelectionOffers?.Count ?? 0)
            : draft.nextPresentationIndex >= 0
                && draft.nextPresentationIndex < (draft.frozenMechanics?.Count ?? 0);
        if (draft.formulaVersion <= 0
            || draft.presentationState != CharacterSkillPresentationState.PresentationPending
            || !hasPending)
        {
            throw new InvalidOperationException(
                "Character skill presentation requires one frozen formula mechanic at the pending index.");
        }
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(moduleSelection
            ? "당신은 던전 경영 RPG 기술에 어울리는 기능 모듈과 실제 근거를 고르고 이름과 서사를 작성한다."
            : "당신은 던전 경영 RPG 기술의 이름과 서사 표현만 작성한다.");
        builder.AppendLine(moduleSelection
            ? "C#이 합법인 개별 모듈 전체를 제공한다. 모듈만 선택하며 수치와 비용은 응답 후 C#이 계산한다."
            : "C#이 아래 기술 효과를 이미 확정했다. 효과, 비용, 대상, 조건을 선택하거나 바꾸지 않는다.");
        if (!string.IsNullOrWhiteSpace(correction))
        {
            string compactCorrection = correction.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (compactCorrection.Length > 180)
            {
                compactCorrection = compactCorrection.Substring(0, 180);
            }

            builder.AppendLine($"이전 응답 거부 이유={compactCorrection}. 이번 응답에서 반드시 고친다.");
        }
        builder.Append(moduleSelection
            ? BuildModuleSelectionPacket(draft, settings)
            : BuildPresentationPacket(draft));
        string characterName = progression.Actor?.Identity?.DisplayName;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            characterName = progression.GrowthState.displayName;
        }

        builder.AppendLine($"character={characterName ?? "인물"}");
        builder.AppendLine($"origin={progression.GrowthState.origin ?? string.Empty}");
        builder.AppendLine($"species={progression.Actor?.SpeciesTag ?? string.Empty}");
        builder.AppendLine($"potential={CharacterSkillDisplay.Potential(progression.GrowthState.potentialGrade)}");
        builder.AppendLine("반드시 JSON 객체 하나만 반환한다.");
        builder.AppendLine(moduleSelection
            ? "형식: {\"selectionId\":\"제공된 값 그대로\",\"positiveModuleIds\":[\"1~3개\"],\"drawbackModuleIds\":[\"선택적 해로운 모듈 0~1개\"],\"evidenceFactIds\":[\"제공된 근거\"],\"displayName\":\"14자 이하 한국어 이름\",\"narrativeFlavor\":\"90자 이하 획득 서사\"}"
            : "형식: {\"presentationId\":\"제공된 값 그대로\",\"displayName\":\"14자 이하 한국어 이름\",\"narrativeFlavor\":\"90자 이하 획득 서사\"}");
        builder.AppendLine("절대 규칙:");
        builder.AppendLine(moduleSelection
            ? "1. 최상위에는 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor만 출력한다."
            : "1. 최상위에는 presentationId, displayName, narrativeFlavor 세 문자열만 출력한다.");
        builder.AppendLine(moduleSelection
            ? "2. selectionId는 제공된 값을 한 글자도 바꾸지 않고, 제공되지 않은 모듈이나 근거 ID를 만들지 않는다."
            : "2. presentationId는 제공된 값을 한 글자도 바꾸지 않는다.");
        builder.AppendLine(moduleSelection
            ? "3. positiveModuleIds에는 어울리는 이로운 모듈을 최소 1개 선택한다. drawbackModules가 제공되면 부정 사건이 실제 설명에 필요할 때만 최대 1개 선택하고 그 부정 근거 ID를 반드시 함께 인용한다. 단점 설명의 '함께 선택 금지 이로운 기능'과 겹치는 모듈은 고르지 않는다. 수치, 비용, 조합 ID는 출력하지 않는다."
            : "3. 조합, 후보, 규칙, 효과, 태그, 선택 인덱스, 수치, 비용 또는 다른 기계 필드를 출력하지 않는다.");
        builder.AppendLine("4. 공개 사실에 없는 사건이나 결과를 만들지 않는다.");
        builder.AppendLine($"5. displayName 또는 narrativeFlavor에 캐릭터 이름 '{characterName}'을 정확히 넣는다.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), publicMaterial);
    }

    public static string BuildModuleSelectionPacket(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings)
    {
        NarrativeFormulaModuleSelectionRequest request =
            CharacterSkillFormulaGeneration.BuildModuleSelectionRequest(draft, settings);
        CharacterSkillCandidateRule rule = draft.rules[draft.nextPresentationIndex];
        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine($"selectionId={request.SelectionId}");
        builder.AppendLine($"skillKind={draft.kind}; trigger={rule.trigger}; target={rule.target}; targeting={rule.targetingMode}; area={rule.effectArea}; areaSize={rule.areaSize}; ultimateDomain={rule.ultimateDomain}");
        builder.AppendLine("skillContext=" + CharacterSkillPresentationSemantics
            .DescribeContext(rule, draft.kind));
        builder.AppendLine($"positiveModuleLimit=1..{request.MaximumPositiveModules}");
        builder.AppendLine("positiveModules:");
        foreach (NarrativeFormulaModuleOffer offer in request.Offers
                     .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive))
        {
            CharacterSkillModuleRule module = settings.FindModule(offer.ModuleId)
                ?? throw new InvalidOperationException(
                    "Live module-selection packet references an unknown module '"
                    + offer.ModuleId + "'.");
            NarrativeFormulaCapabilityDescriptor descriptor = settings
                .RequireFormulaDescriptor(module);
            if (!CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    draft.kind, rule, module))
            {
                throw new InvalidOperationException(
                    "Live module-selection packet contains a capability whose applied axes "
                    + "are not consumed by its runtime context: '" + offer.ModuleId + "'.");
            }
            builder.AppendLine($"- id={offer.ModuleId}; meaning="
                + CharacterSkillPresentationSemantics.DescribeCapability(
                    descriptor, rule, draft.kind));
        }
        builder.AppendLine("drawbackModules:");
        foreach (NarrativeFormulaModuleOffer offer in request.Offers
                     .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback))
            builder.AppendLine($"- id={offer.ModuleId}; meaning={offer.SemanticDescription}");
        if (request.MaximumDrawbackModules == 0) builder.AppendLine("- none (return [])");
        builder.AppendLine("evidenceFactIds=" + string.Join(",", request.EvidenceFactIds));
        GameplayOutcomeEvidenceFormulaProjection.AppendPromptFacts(
            builder,
            draft.outcomeEvidenceBindings);
        return builder.ToString();
    }

    public static string BuildPresentationPacket(CharacterSkillDraft draft)
    {
        if (draft?.frozenMechanics == null
            || draft.nextPresentationIndex < 0
            || draft.nextPresentationIndex >= draft.frozenMechanics.Count)
            throw new InvalidOperationException("CharacterSkill has no frozen presentation at the pending index.");
        CharacterSkillInstance frozen = draft.frozenMechanics[draft.nextPresentationIndex]
            ?? throw new InvalidOperationException("CharacterSkill frozen presentation is null.");
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"presentationId={frozen.presentationId}");
        builder.AppendLine($"mechanicalDescription={frozen.mechanicalDescription}");
        builder.AppendLine("evidenceIds=" + string.Join(",", frozen.evidenceIds
            ?? new List<string>()));
        return builder.ToString();
    }

    public static string BuildCandidatePacket(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings)
    {
        if (draft == null || settings == null)
        {
            return string.Empty;
        }
        CharacterSkillRuleIdentity.Ensure(draft);
        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine($"kind={draft.kind}");
        builder.AppendLine(
            $"semanticsSchemaVersion={CharacterSkillCombinationSemanticsFactory.SchemaVersion}");
        builder.AppendLine($"candidateCount={draft.rules?.Count ?? 0}");
        builder.AppendLine("candidateRules:");
        foreach (CharacterSkillCandidateRule rule in draft.rules ?? new List<CharacterSkillCandidateRule>())
        {
            if (rule == null)
            {
                continue;
            }
            builder.AppendLine(
                $"- ruleId={rule.ruleId}; rarity={rule.rarity}; budget={rule.budget}; trigger={rule.trigger}; target={rule.target}; targeting={rule.targetingMode}; area={rule.effectArea}; areaSize={rule.areaSize}; durationHours={rule.manualDurationHours}; cooldownDays={rule.manualCooldownDays}; ultimateDomain={rule.ultimateDomain}; cooldownTurns={rule.cooldownTurns}; usableFrom={CharacterSkillFormationRules.Format(rule.usableFrom)}; targetPositions={CharacterSkillFormationRules.Format(rule.targetPositions)}");
            List<CharacterSkillAllowedCombination> combinations =
                CharacterSkillCombinationCatalog.RequireLegalCombinations(
                    rule,
                    settings,
                    draft.kind);
            List<(CharacterSkillAllowedCombination Combination,
                    CharacterSkillCombinationSemanticsDto Semantics)> projected = combinations
                .Select(combination => (
                    combination,
                    CharacterSkillCombinationSemanticsFactory.Create(
                        combination,
                        rule,
                        draft.kind,
                        settings)))
                .ToList();
            SortedDictionary<string, string> definitions =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (CharacterSkillModuleSemanticsDto module in projected
                         .SelectMany(value => value.Semantics.modules))
            {
                string key = CharacterSkillCombinationSemanticsFactory.GetSemanticKey(
                    rule,
                    draft.kind,
                    module);
                string json = CharacterSkillCombinationSemanticsFactory
                    .SerializeModuleCanonical(module);
                if (definitions.TryGetValue(key, out string existing)
                    && !string.Equals(existing, json, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill semantic key '{key}' resolved to different public definitions.");
                }
                definitions[key] = json;
            }
            builder.AppendLine("  semanticDefinitions:");
            foreach (KeyValuePair<string, string> definition in definitions)
            {
                builder.AppendLine($"  - key={definition.Key}; json={definition.Value}");
            }
            builder.AppendLine("  combinationOptions=" + string.Join(",", projected
                .Select(value =>
                {
                    string semanticKeys = string.Join("+", value.Semantics.modules.Select(module =>
                        CharacterSkillCombinationSemanticsFactory.GetSemanticKey(
                            rule,
                            draft.kind,
                            module)));
                    return $"{value.Combination.Id}[{value.Combination.Signature}]"
                        + $"{{semanticKeys={semanticKeys}}}";
                })));
        }
        return builder.ToString();
    }
}
