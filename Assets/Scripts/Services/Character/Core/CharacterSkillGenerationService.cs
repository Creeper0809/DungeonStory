using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
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

[Serializable]
public sealed class CharacterSkillGenerationResponseDto : ILlmJsonPayload
{
    public List<CharacterSkillCandidateResponseDto> candidates = new List<CharacterSkillCandidateResponseDto>();

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (candidates == null || candidates.Count == 0)
        {
            error = "candidates is required.";
            return false;
        }

        if (candidates.Any(candidate => candidate == null))
        {
            error = "candidates cannot contain null.";
            return false;
        }

        return true;
    }
}

[Serializable]
public sealed class CharacterSkillCandidateResponseDto
{
    public int index;
    public string name = string.Empty;
    public string description = string.Empty;
    public string narrativeReason = string.Empty;
    public string trigger = string.Empty;
    public string target = string.Empty;
    public string ultimateDomain = string.Empty;
    public int cooldownTurns;
    public string combinationId = string.Empty;
    public List<CharacterSkillModuleResponseDto> modules = new List<CharacterSkillModuleResponseDto>();
}

[Serializable]
public sealed class CharacterSkillModuleResponseDto
{
    public string pairId = string.Empty;
    public string moduleId = string.Empty;
    public string variantId = string.Empty;
}

public sealed class CharacterSkillAllowedCombination
{
    public string Id { get; set; } = string.Empty;
    public int Cost { get; set; }
    public List<CharacterSkillModuleSelection> Modules { get; set; } = new List<CharacterSkillModuleSelection>();

    public string Signature => string.Join(",", Modules
        .Select(module => $"{module.moduleId}|{module.variantId}"));
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
            .Where(module => module != null)
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
            AddCombination(combinations, rule.budget, atoms[first]);
            for (int second = first + 1; second < atoms.Count; second++)
            {
                if (SameModule(atoms[first], atoms[second])
                    || (kind == CharacterSkillKind.Ultimate
                        && MixedUltimateDomains(atoms[first], atoms[second])))
                {
                    continue;
                }

                AddCombination(combinations, rule.budget, atoms[first], atoms[second]);
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

                    AddCombination(combinations, rule.budget, atoms[first], atoms[second], atoms[third]);
                }
            }
        }

        List<CharacterSkillAllowedCombination> result = combinations.Values
            .OrderByDescending(combination => combination.Modules.Count)
            .ThenByDescending(combination => combination.Cost)
            .ThenBy(combination => combination.Signature, StringComparer.Ordinal)
            .Take(Mathf.Max(1, maximumCount))
            .ToList();
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Id = $"c{i}";
        }

        return result;
    }

    private static void AddCombination(
        IDictionary<string, CharacterSkillAllowedCombination> combinations,
        int budget,
        params Atom[] atoms)
    {
        int cost = atoms.Sum(atom => atom.Variant.cost);
        if (cost > budget)
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
        combinations[signature] = new CharacterSkillAllowedCombination
        {
            Cost = cost,
            Modules = modules
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
}

public sealed class CharacterSkillGenerationService :
    ICharacterSkillGenerationService,
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
        public bool inFlight;
        public bool cancelled;
        public string correction = string.Empty;
        public string preparedPrompt = string.Empty;
    }

    private readonly ICharacterSkillSystemSettingsProvider settingsProvider;
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly IUiClock uiClock;
    private readonly Dictionary<string, PendingRequest> pending = new Dictionary<string, PendingRequest>();
    private readonly List<PendingRequest> tickBuffer = new List<PendingRequest>();

    public int PendingRequestCount => pending.Count;
    public string LastDiagnostic { get; private set; } = string.Empty;

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
        string actorId = progression.Actor != null
            ? CharacterPersistentIdentity.Require(progression.Actor).Value
            : $"unbound-growth:{growth.generationSeed}";

        string requestKey = $"skill:{actorId}:{kind}:{Mathf.Max(1, unlockLevel)}:r{Mathf.Max(0, revision)}";
        IRandomStream random = new DeterministicRandomSequence(
            CharacterGrowthRules.StableHash(requestKey));
        CharacterSkillDraft draft = new CharacterSkillDraft
        {
            unlockLevel = Mathf.Max(1, unlockLevel),
            kind = kind,
            requestKey = requestKey,
            requestedUltimateDomain = CharacterUltimateDomain.None
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
            draft.rules.Add(CreateCandidateRule(progression, kind, rarity, random));
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

        if (!pending.TryGetValue(draft.requestKey, out PendingRequest request))
        {
            request = new PendingRequest
            {
                progression = progression,
                draft = draft,
                nextAttemptAt = uiClock.Time
            };
            pending.Add(draft.requestKey, request);
        }
        else
        {
            if (!ReferenceEquals(request.progression, progression)
                || !ReferenceEquals(request.draft, draft))
            {
                request.preparedPrompt = string.Empty;
                request.correction = string.Empty;
            }

            request.progression = progression;
            request.draft = draft;
        }

        progression.MarkGenerationRequestPending(draft.requestKey);
        draft.requestSubmitted = true;
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
            correlatedRuntime?.CancelCharacterSkillRequest(pair.Key);
            pending.Remove(pair.Key);
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
        if (draft == null || draft.rules == null || draft.rules.Count == 0)
        {
            error = "Draft rules are missing.";
            return false;
        }

        if (!LlmJsonResponseParser.TryParse(
            response,
            out CharacterSkillGenerationResponseDto payload,
            out error))
        {
            return false;
        }

        if (payload.candidates.Count != draft.rules.Count)
        {
            error = $"Expected {draft.rules.Count} candidates, received {payload.candidates.Count}.";
            return false;
        }

        HashSet<int> seenIndexes = new HashSet<int>();
        HashSet<string> seenCombinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterSkillCandidateResponseDto candidate in payload.candidates.OrderBy(item => item.index))
        {
            if (candidate.index < 0
                || candidate.index >= draft.rules.Count
                || !seenIndexes.Add(candidate.index))
            {
                error = $"Candidate index {candidate.index} is invalid or duplicated.";
                return false;
            }

            if (!TryBuildSkill(draft, candidate, out CharacterSkillInstance skill, out error))
            {
                return false;
            }

            string combination = string.Join(",", skill.modules
                .Select(module => $"{module.moduleId}:{module.variantId}")
                .OrderBy(value => value, StringComparer.Ordinal));
            if (!seenCombinations.Add(combination))
            {
                error = "Candidate module combinations must be distinct.";
                return false;
            }

            skills.Add(skill);
        }

        return true;
    }

    private CharacterSkillCandidateRule CreateCandidateRule(
        CharacterProgression progression,
        CharacterSkillKind kind,
        CharacterSkillRarity rarity,
        IRandomStream random)
    {
        CharacterSkillTrigger trigger;
        CharacterSkillTarget target;
        if (kind == CharacterSkillKind.Active)
        {
            trigger = CharacterSkillTrigger.ManualCombat;
            CharacterSkillTarget[] targets =
            {
                CharacterSkillTarget.Enemy,
                CharacterSkillTarget.Self,
                CharacterSkillTarget.Ally
            };
            target = targets[random.NextInt(0, targets.Length)];
        }
        else if (kind == CharacterSkillKind.Ultimate)
        {
            trigger = CharacterSkillTrigger.ManualCombat;
            target = CharacterSkillTarget.Enemy;
        }
        else
        {
            trigger = CharacterGrowthRules.ChoosePassiveTrigger(progression.NarrativeLedger, random);
            target = CharacterSkillTarget.Self;
        }

        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            rarity = rarity,
            budget = settingsProvider.Settings.GetBudget(rarity),
            trigger = trigger,
            target = target
        };
        CharacterSkillFormationRules.Resolve(
            target,
            Array.Empty<CharacterSkillModuleSelection>(),
            out rule.usableFrom,
            out rule.targetPositions);
        IEnumerable<CharacterSkillModuleRule> available = settingsProvider.Settings.Modules
            .Where(module => module != null
                && (kind == CharacterSkillKind.Ultimate
                    ? module.allowedKinds == null || module.allowedKinds.Count == 0 || module.allowedKinds.Contains(kind)
                    : module.Allows(kind, trigger, target)))
            .Where(module => kind == CharacterSkillKind.Ultimate
                || CharacterSkillValidation.IsTargetCompatible(module.id, target));
        available = available.Where(module => !CharacterSkillValidation.WouldSelfTrigger(
            module.id,
            trigger));
        foreach (CharacterSkillModuleRule module in available)
        {
            rule.allowedModuleIds.Add(module.id);
            foreach (CharacterSkillNumericVariant variant in module.variants ?? new List<CharacterSkillNumericVariant>())
            {
                if (variant != null && variant.cost <= rule.budget)
                {
                    rule.allowedVariantIds.Add(variant.id);
                }
            }
        }

        rule.allowedModuleIds = rule.allowedModuleIds.Distinct(StringComparer.Ordinal).ToList();
        rule.allowedVariantIds = rule.allowedVariantIds.Distinct(StringComparer.Ordinal).ToList();
        return rule;
    }

    private void TrySubmit(PendingRequest request, float now)
    {
        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            ScheduleRetry(request, now);
            return;
        }

        request.inFlight = true;
        if (string.IsNullOrEmpty(request.preparedPrompt))
        {
            request.preparedPrompt = CharacterSkillPromptBuilder.Build(
                request.progression,
                request.draft,
                settingsProvider.Settings,
                request.correction);
        }

        string prompt = request.preparedPrompt;
        LastDiagnostic = $"submitted={request.draft.requestKey}; attempt={request.attempts + 1}; prompt={prompt.Length}";
        bool accepted = runtime is ICorrelatedCharacterSkillLlmRuntime correlatedRuntime
            ? correlatedRuntime.GenerateCharacterSkillAsync(
                request.draft.requestKey,
                prompt,
                result => HandleResult(request, result))
            : runtime.GenerateCharacterSkillAsync(prompt, result => HandleResult(request, result));
        if (!accepted)
        {
            request.inFlight = false;
            ScheduleRetry(request, uiClock.Time);
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

        request.inFlight = false;
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
            request.draft.candidates = skills;
            request.draft.isReady = true;
            request.draft.requestSubmitted = false;
            request.progression.MarkGenerationRequestCompleted(request.draft.requestKey);
            RemoveRequest(request.draft.requestKey);
            request.progression.OnDraftReady(request.draft);
            LastDiagnostic = $"ready={request.draft.requestKey}; candidates={skills.Count}";
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

        ScheduleRetry(request, uiClock.Time);
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

    private void ScheduleRetry(PendingRequest request, float now)
    {
        request.attempts++;
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        float delay = Mathf.Min(
            settings.maximumRetrySeconds,
            settings.initialRetrySeconds * Mathf.Pow(2f, Mathf.Min(8, request.attempts - 1)));
        request.nextAttemptAt = now + delay;
    }

    private void RemoveRequest(string requestKey)
    {
        if (!string.IsNullOrWhiteSpace(requestKey))
        {
            pending.Remove(requestKey);
        }
    }

    private bool TryBuildSkill(
        CharacterSkillDraft draft,
        CharacterSkillCandidateResponseDto candidate,
        out CharacterSkillInstance skill,
        out string error)
    {
        skill = null;
        error = string.Empty;
        CharacterSkillCandidateRule rule = draft.rules[candidate.index];
        if (string.IsNullOrWhiteSpace(candidate.name)
            || candidate.name.Trim().Length > 14
            || string.IsNullOrWhiteSpace(candidate.description)
            || candidate.description.Trim().Length > 90
            || string.IsNullOrWhiteSpace(candidate.narrativeReason)
            || candidate.narrativeReason.Trim().Length > 90)
        {
            error = "Skill text is missing or exceeds the player-facing length limit.";
            return false;
        }

        string visibleText = $"{candidate.name} {candidate.description} {candidate.narrativeReason}";
        if (visibleText.IndexOf("LLM", StringComparison.OrdinalIgnoreCase) >= 0
            || visibleText.Contains("생성 중")
            || visibleText.Contains("요청 키"))
        {
            error = "Technical generation text cannot appear in a skill.";
            return false;
        }

        CharacterUltimateDomain domain = CharacterUltimateDomain.None;
        CharacterSkillTrigger trigger = rule.trigger;
        CharacterSkillTarget target = rule.target;
        if (draft.kind == CharacterSkillKind.Ultimate)
        {
            if (!Enum.TryParse(candidate.ultimateDomain, true, out domain)
                || domain == CharacterUltimateDomain.None)
            {
                error = "Ultimate domain must be Offense, Defense, or Management.";
                return false;
            }

            trigger = domain switch
            {
                CharacterUltimateDomain.Defense => CharacterSkillTrigger.InvasionStarted,
                CharacterUltimateDomain.Management => CharacterSkillTrigger.OperatingDayStarted,
                _ => CharacterSkillTrigger.ManualCombat
            };
            if (!Enum.TryParse(candidate.target, true, out target))
            {
                error = $"Unknown target '{candidate.target}'.";
                return false;
            }
        }
        else
        {
            if (!Enum.TryParse(candidate.trigger, true, out CharacterSkillTrigger responseTrigger)
                || responseTrigger != trigger
                || !Enum.TryParse(candidate.target, true, out CharacterSkillTarget responseTarget)
                || responseTarget != target)
            {
                error = "The generated trigger or target differs from the game-authored rule.";
                return false;
            }
        }

        List<CharacterSkillModuleResponseDto> responseModules = candidate.modules
            ?? new List<CharacterSkillModuleResponseDto>();
        if (!string.IsNullOrWhiteSpace(candidate.combinationId))
        {
            if (responseModules.Count > 0)
            {
                error = "Use either combinationId or modules, not both.";
                return false;
            }

            CharacterSkillAllowedCombination combination = CharacterSkillCombinationCatalog
                .Build(rule, settingsProvider.Settings, draft.kind)
                .FirstOrDefault(item => string.Equals(
                    item.Id,
                    candidate.combinationId.Trim(),
                    StringComparison.Ordinal));
            if (combination == null)
            {
                error = $"Unknown combination '{candidate.combinationId}'.";
                return false;
            }

            responseModules = combination.Modules
                .Select(module => new CharacterSkillModuleResponseDto
                {
                    moduleId = module.moduleId,
                    variantId = module.variantId
                })
                .ToList();
        }

        if (responseModules.Count == 0)
        {
            error = "At least one module is required.";
            return false;
        }

        int cost = 0;
        HashSet<string> moduleIds = new HashSet<string>(StringComparer.Ordinal);
        List<CharacterSkillModuleSelection> selections = new List<CharacterSkillModuleSelection>();
        foreach (CharacterSkillModuleResponseDto moduleDto in responseModules)
        {
            string moduleId = moduleDto?.moduleId ?? string.Empty;
            string variantId = moduleDto?.variantId ?? string.Empty;
            if (moduleDto != null && !string.IsNullOrWhiteSpace(moduleDto.pairId))
            {
                string[] pair = moduleDto.pairId.Split('|');
                if (pair.Length != 2)
                {
                    error = $"Unknown module pair '{moduleDto.pairId}'.";
                    return false;
                }

                moduleId = pair[0].Trim();
                variantId = pair[1].Trim();
            }

            if (moduleDto == null
                || !moduleIds.Add(moduleId)
                || !rule.allowedModuleIds.Contains(moduleId, StringComparer.Ordinal))
            {
                error = $"Unknown or duplicated module '{moduleId}'.";
                return false;
            }

            CharacterSkillModuleRule module = settingsProvider.Settings.FindModule(moduleId);
            CharacterSkillNumericVariant variant = module?.FindVariant(variantId);
            if (module == null
                || variant == null
                || !rule.allowedVariantIds.Contains(variantId, StringComparer.Ordinal)
                || !CharacterSkillValidation.IsTargetCompatible(module.id, target)
                || !module.Allows(draft.kind, trigger, target))
            {
                error = $"Module pair '{moduleId}|{variantId}' has an invalid variant, trigger, or target.";
                return false;
            }

            if (draft.kind == CharacterSkillKind.Ultimate
                && (domain == CharacterUltimateDomain.Management) != (module is CharacterManagementSkillModuleRule))
            {
                error = "Ultimate modules do not match the selected domain.";
                return false;
            }

            if (CharacterSkillValidation.WouldSelfTrigger(module.id, trigger))
            {
                error = $"Module '{module.id}' would trigger itself recursively.";
                return false;
            }

            cost += variant.cost;
            selections.Add(new CharacterSkillModuleSelection
            {
                moduleId = module.id,
                variantId = variant.id
            });
        }

        if (cost > rule.budget)
        {
            error = $"Skill cost {cost} exceeds budget {rule.budget}.";
            return false;
        }

        CharacterSkillFormationRules.Resolve(
            target,
            selections,
            out OffenseFormationMask usableFrom,
            out OffenseFormationMask targetPositions);
        skill = new CharacterSkillInstance
        {
            id = $"{draft.requestKey}:{candidate.index}",
            displayName = candidate.name.Trim(),
            description = candidate.description.Trim(),
            narrativeReason = candidate.narrativeReason.Trim(),
            kind = draft.kind,
            rarity = rule.rarity,
            trigger = trigger,
            target = target,
            ultimateDomain = domain,
            cooldownTurns = Mathf.Clamp(candidate.cooldownTurns, 0, 9),
            usableFrom = usableFrom,
            targetPositions = targetPositions,
            modules = selections,
            requestKey = draft.requestKey
        };
        return true;
    }
}

public static class CharacterSkillValidation
{
    public static bool IsTargetCompatible(string moduleId, CharacterSkillTarget target)
    {
        string id = moduleId?.Trim() ?? string.Empty;
        bool enemy = target == CharacterSkillTarget.Enemy || target == CharacterSkillTarget.AllEnemies;
        bool friendly = target == CharacterSkillTarget.Self
            || target == CharacterSkillTarget.Ally
            || target == CharacterSkillTarget.AllAllies;
        return id switch
        {
            "damage" or "dot" or "vulnerability" or "delay" or "debuff" => enemy,
            "heal" or "guard" or "buff" or "cleanse" or "protect" or "cooldown_adjust" => friendly,
            "reposition" => enemy || friendly,
            "multi_target" or "conditional_amplify" => enemy || friendly,
            "work_speed" or "output" or "cleaning" or "repair" or "stock" or "research"
                or "needs" or "mood" or "relationship" or "revenue" => !enemy,
            _ => false
        };
    }

    public static bool WouldSelfTrigger(string moduleId, CharacterSkillTrigger trigger)
    {
        return (string.Equals(moduleId, "mood", StringComparison.Ordinal)
                && trigger == CharacterSkillTrigger.MoodChanged)
            || (string.Equals(moduleId, "needs", StringComparison.Ordinal)
                && trigger == CharacterSkillTrigger.NeedChanged)
            || (string.Equals(moduleId, "relationship", StringComparison.Ordinal)
                && trigger == CharacterSkillTrigger.RelationshipChanged);
    }
}

public static class CharacterSkillPromptBuilder
{
    public static string Build(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string correction = "")
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("당신은 던전 경영 RPG의 제한형 기술 조합기다.");
        builder.AppendLine("아래에 제시된 ID만 사용한다. 수치를 만들거나 기술적 생성 과정을 언급하지 않는다.");
        if (!string.IsNullOrWhiteSpace(correction))
        {
            string compactCorrection = correction.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (compactCorrection.Length > 180)
            {
                compactCorrection = compactCorrection.Substring(0, 180);
            }

            builder.AppendLine($"이전 응답 거부 이유={compactCorrection}. 이번 응답에서 반드시 고친다.");
        }
        builder.AppendLine($"kind={draft.kind}");
        builder.AppendLine($"candidateCount={draft.rules.Count}; candidateIndexes={string.Join(",", Enumerable.Range(0, draft.rules.Count))}");
        string characterName = progression.Actor?.Identity?.DisplayName;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            characterName = progression.GrowthState.displayName;
        }

        builder.AppendLine($"character={characterName ?? "인물"}");
        builder.AppendLine($"origin={progression.GrowthState.origin ?? string.Empty}");
        builder.AppendLine($"species={progression.Actor?.SpeciesTag ?? string.Empty}");
        builder.AppendLine($"potential={CharacterSkillDisplay.Potential(progression.GrowthState.potentialGrade)}");
        builder.AppendLine("facts:");
        foreach (CharacterNarrativeFact fact in progression.NarrativeLedger.Facts
            .Where(item => item != null)
            .OrderByDescending(item => item.milestoneCount)
            .ThenByDescending(item => item.lastDay)
            .Take(18))
        {
            builder.AppendLine(
                $"- domain={fact.domain}; fact={fact.factId}; subject={fact.subjectId}; outcome={fact.outcome}; count={fact.count}; total={fact.totalValue:0.##}");
        }

        builder.AppendLine("candidateRules (각 후보는 자기 줄의 exact 값을 그대로 사용):");
        for (int i = 0; i < draft.rules.Count; i++)
        {
            CharacterSkillCandidateRule rule = draft.rules[i];
            builder.AppendLine(
                $"- index={i}; rarity={rule.rarity}; budget={rule.budget}; trigger={rule.trigger}; target={rule.target}; usableFrom={CharacterSkillFormationRules.Format(rule.usableFrom)}; targetPositions={CharacterSkillFormationRules.Format(rule.targetPositions)}");
            List<CharacterSkillAllowedCombination> combinations =
                CharacterSkillCombinationCatalog.Build(rule, settings, draft.kind);
            builder.AppendLine("  combinationOptions=" + string.Join(",", combinations
                .Select(combination => $"{combination.Id}[{combination.Signature}]")));
        }

        builder.AppendLine("반드시 JSON 객체 하나만 반환한다.");
        builder.AppendLine("형식: {\"candidates\":[{\"index\":0,\"name\":\"12자 이하 한국어 이름\",\"description\":\"40자 이하 효과 설명\",\"narrativeReason\":\"35자 이하 획득 계기\",\"trigger\":\"규칙의 trigger\",\"target\":\"규칙의 target\",\"ultimateDomain\":\"None 또는 Offense/Defense/Management\",\"cooldownTurns\":0,\"combinationId\":\"c0\"}]}");
        builder.AppendLine("절대 규칙:");
        builder.AppendLine("1. candidates 수와 index는 candidateRules와 정확히 같아야 한다.");
        builder.AppendLine("2. trigger와 target은 해당 index 줄의 값을 철자와 대소문자까지 그대로 복사한다.");
        builder.AppendLine("3. combinationId는 해당 index의 combinationOptions에서 c로 시작하는 ID 하나만 정확히 고른다. modules 필드는 출력하지 않는다.");
        builder.AppendLine("4. requestKey, kind, rarity, trigger, target 값은 moduleId나 variantId로 쓰지 않는다.");
        if (draft.rules.Count > 1)
        {
            builder.AppendLine($"5. candidates 배열에는 정확히 {draft.rules.Count}개만 넣고 후보별로 서로 다른 효과 조합을 고른다.");
        }
        else
        {
            builder.AppendLine("5. candidates 배열에는 index 0 후보 하나만 넣는다. index 1이나 index 2를 만들지 않는다.");
        }
        builder.AppendLine("6. name, description, narrativeReason은 자연스러운 한국어로 쓴다. 일반 액티브와 패시브의 ultimateDomain은 None이다.");
        builder.AppendLine($"7. 각 후보의 name, description, narrativeReason 중 적어도 하나에는 캐릭터 이름 '{characterName}'을 정확히 넣고, description과 narrativeReason은 서로 다른 문장으로 쓴다.");
        builder.AppendLine("궁극기만 서사에 맞는 계열을 고른다.");
        return builder.ToString();
    }
}
