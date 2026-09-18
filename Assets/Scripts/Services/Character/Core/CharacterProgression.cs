using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public sealed class CharacterProgression : MonoBehaviour
{
    public const int MaxLevel = CharacterProgressionRules.MaxLevel;
    public const int NormalActiveSlots = 3;
    public const int PassiveSlots = 2;
    public const int MaxEquippedSkills = NormalActiveSlots;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(0)] private int currentExperience;
    [SerializeField] private CharacterGrowthState growthState = new CharacterGrowthState();
    [SerializeField] private CharacterNarrativeLedger narrativeLedger = new CharacterNarrativeLedger();
    [SerializeField] private CharacterAcquiredTraitAggregateState acquiredTraitState =
        new CharacterAcquiredTraitAggregateState();

    private readonly List<string> learnedSkillIds = new List<string>();
    private readonly List<string> equippedSkillIds = new List<string>();
    private CharacterActor actor;
    private ICharacterSkillGenerationService generationService;
    private ICharacterSkillSystemSettingsProvider settingsProvider;
    private CharacterProgressionNotificationApplicationAdapter notifications;
    private CharacterProgressionProfileProjector profileProjector;
    private IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction;
    private bool suppressPublicSkillNotifications;

    public CharacterActor Actor => actor;
    public CharacterSkillSystemSettingsSO SkillSettings => settingsProvider?.Settings
        ?? throw new InvalidOperationException(
            "Character progression has no authored skill settings.");
    public int Level => Mathf.Clamp(level, 1, MaxLevel);
    public int CurrentExperience => Level >= MaxLevel ? 0 : Mathf.Max(0, currentExperience);
    public int ExperienceToNextLevel => Level >= MaxLevel ? 0 : GetExperienceRequired(Level);
    public float ExperienceRatio => Level >= MaxLevel
        ? 1f
        : CharacterProgressionRules.GetExperienceRatio(Level, CurrentExperience);
    public CharacterGrowthState GrowthState
    {
        get
        {
            growthState ??= new CharacterGrowthState();
            growthState.EnsureCollections();
            return growthState;
        }
    }
    public CharacterNarrativeLedger NarrativeLedger =>
        narrativeLedger ??= new CharacterNarrativeLedger();
    public int AcquiredTraitRevision => acquiredTraitState?.revision ?? 0;
    public CharacterPotentialGrade PotentialGrade => GrowthState.potentialGrade;
    public IReadOnlyList<CharacterSkillInstance> ActiveSkills => GrowthState.activeSkills;
    public IReadOnlyList<CharacterSkillInstance> PassiveSkills => GrowthState.passiveSkills;
    public IReadOnlyList<CharacterSkillInstance> OwnerFixedSkills =>
        CharacterOwnerFixedSkillUtility.GetSkills(actor?.Identity?.Data);
    public CharacterSkillInstance Ultimate => GrowthState.ultimate;
    public IReadOnlyList<CharacterSkillDraft> Drafts => GrowthState.drafts;
    public IReadOnlyList<string> LearnedSkillIds
    {
        get
        {
            RebuildLegacySkillViews();
            return learnedSkillIds;
        }
    }
    public IReadOnlyList<string> EquippedSkillIds
    {
        get
        {
            RebuildLegacySkillViews();
            return equippedSkillIds;
        }
    }

    public event Action Changed;
    public event Action<CharacterSkillDraft> DraftReady;

    public void SetPublicSkillNotificationsSuppressed(bool suppressed)
    {
        suppressPublicSkillNotifications = suppressed;
    }

    [Inject]
    public void ConstructCharacterProgression(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionNotificationApplicationAdapter notifications,
        CharacterProgressionProfileProjector profileProjector,
        IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.notifications = notifications
            ?? throw new ArgumentNullException(nameof(notifications));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        this.evidenceUseTransaction = evidenceUseTransaction
            ?? throw new ArgumentNullException(nameof(evidenceUseTransaction));
        CompleteConfigurationIfReady();
    }

    public void ConstructCharacterProgression(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionNotificationApplicationAdapter notifications,
        CharacterProgressionProfileProjector profileProjector)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.notifications = notifications
            ?? throw new ArgumentNullException(nameof(notifications));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        CompleteConfigurationIfReady();
    }

    public void ConfigurePreview(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionProfileProjector profileProjector)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        CompleteConfigurationIfReady();
    }

    public static int GetExperienceRequired(int currentLevel)
    {
        return CharacterProgressionRules.GetExperienceRequired(currentLevel);
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        CompleteConfigurationIfReady();
    }

    public int AddExperience(int amount)
    {
        EnsureInitialized();
        if (amount <= 0 || Level >= MaxLevel)
        {
            return 0;
        }

        CharacterProgressionTransition transition = CharacterProgressionRules.AddExperience(
            level,
            currentExperience,
            amount);
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        foreach (int reachedLevel in transition.ReachedLevels)
        {
            AllocateStatsForReachedLevel(reachedLevel);
        }

        if (transition.HasLevelChanged)
        {
            actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
            actor?.AddLog($"레벨 {level}에 도달했다.");
        }

        EnsureUnlockedDrafts();
        NotifyChangedWithoutAffectingCommittedState();
        return transition.LevelDelta;
    }

    public bool EnsureMinimumLevel(int targetLevel, string reason = "")
    {
        EnsureInitialized();
        CharacterProgressionTransition transition =
            CharacterProgressionRules.EnsureMinimumLevel(
                level,
                currentExperience,
                targetLevel);
        if (!transition.HasLevelChanged)
        {
            return false;
        }

        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        foreach (int reachedLevel in transition.ReachedLevels)
        {
            AllocateStatsForReachedLevel(reachedLevel);
        }

        actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            actor?.AddLog(reason);
        }

        EnsureUnlockedDrafts();
        NotifyChangedWithoutAffectingCommittedState();
        return true;
    }

    public void SetAutoChooseSkillDrafts(bool autoChoose)
    {
        EnsureInitialized();
        GrowthState.autoChooseDrafts = autoChoose;
        if (autoChoose)
        {
            foreach (CharacterSkillDraft draft in GrowthState.drafts
                .Where(item => item != null
                    && item.kind == CharacterSkillKind.Active
                    && item.isReady
                    && !item.permanentlyChosen)
                .OrderBy(item => item.unlockLevel)
                .ToArray())
            {
                int bestIndex = ChooseBestCandidateIndex(draft);
                TryChooseActiveSkill(draft.unlockLevel, bestIndex, confirmed: true, out _);
            }
        }

        NotifyChangedWithoutAffectingCommittedState();
    }

    public bool TryChooseActiveSkill(
        int unlockLevel,
        int candidateIndex,
        bool confirmed,
        out string message)
    {
        EnsureInitialized();
        CharacterSkillDraft draft = GrowthState.drafts.FirstOrDefault(item => item != null
            && item.kind == CharacterSkillKind.Active
            && item.unlockLevel == unlockLevel);
        if (draft == null || !draft.isReady)
        {
            message = "아직 선택할 기술이 없습니다.";
            return false;
        }

        if (draft.permanentlyChosen)
        {
            CharacterSkillInstance chosen = draft.ChosenSkill;
            if (chosen != null
                && !GrowthState.activeSkills.Any(skill => skill != null
                    && string.Equals(skill.id, chosen.id, StringComparison.Ordinal)))
            {
                GrowthState.activeSkills.Add(chosen.Clone());
                RebuildLegacySkillViews();
                NotifyChangedWithoutAffectingCommittedState();
            }

            message = "이미 확정된 기술입니다.";
            return candidateIndex == draft.chosenIndex;
        }

        if (candidateIndex < 0 || candidateIndex >= draft.candidates.Count)
        {
            message = "선택할 기술 후보가 올바르지 않습니다.";
            return false;
        }

        if (!confirmed)
        {
            message = "이 기술은 선택 후 바꿀 수 없습니다. 한 번 더 확인해 주세요.";
            return false;
        }

        if (GrowthState.activeSkills.Count >= GetSlotProfile().NormalActiveSlots)
        {
            message = "일반 액티브 슬롯이 모두 찼습니다.";
            return false;
        }

        CharacterSkillInstance selectedSkill = draft.candidates[candidateIndex];
        if (!TryCommitFormulaEvidence(selectedSkill, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = candidateIndex;
                GrowthState.activeSkills.Add(selectedSkill.Clone());
                GrowthState.nextActiveDraftHasPity = draft.grantsUpperRarityPity;
            }, out message))
        {
            return false;
        }
        message = $"{draft.candidates[candidateIndex].displayName}을(를) 영구 확정했습니다.";
        RebuildLegacySkillViews();
        NotifyChangedWithoutAffectingCommittedState();
        return true;
    }

    public bool TryToggleEquipped(string skillId, out string message)
    {
        message = "기술은 종류별 고정 슬롯에 영구 배치됩니다.";
        return false;
    }

    public bool IsLearned(string skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId)
            && LearnedSkillIds.Contains(skillId, StringComparer.Ordinal);
    }

    public bool IsEquipped(string skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId)
            && EquippedSkillIds.Contains(skillId, StringComparer.Ordinal);
    }

    public IReadOnlyList<CharacterTraitSO> ResolveSelectedTraits()
    {
        EnsureInitialized();
        return RequireProfileProjector().ResolveSelectedTraits(
            actor,
            GrowthState);
    }

    public CharacterAcquiredTraitAggregateState CaptureAcquiredTraitState()
    {
        acquiredTraitState ??= new CharacterAcquiredTraitAggregateState();
        acquiredTraitState.EnsureCollections();
        return acquiredTraitState.Clone();
    }

    [GameplayInternalOnly(
        "Acquired-trait manifestation and erasure services commit a fully validated candidate state.",
        "CharacterAcquiredTrait manifestation/erasure transaction services")]
    public bool TryCommitAcquiredTraitState(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        CharacterAcquiredTraitAggregateState current =
            CaptureAcquiredTraitState();
        List<CharacterAcquiredTraitValidationIssue> issues = ValidateAcquiredTraitCandidate(
            current, candidate, expectedRevision, settings, moduleDefinitions);
        if (issues.Count > 0)
        {
            validationIssues = issues;
            return false;
        }

        acquiredTraitState = candidate.Clone();
        validationIssues = Array.Empty<CharacterAcquiredTraitValidationIssue>();
        Changed?.Invoke();
        return true;
    }

    [GameplayInternalOnly(
        "Formula acquired-trait completion atomically publishes a validated candidate and consumes its exact projected evidence once.",
        "CharacterAcquiredTrait formula manifestation transaction service")]
    public bool TryCommitAcquiredTraitStateWithFormulaEvidence(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        IEnumerable<string> evidenceFactIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        CharacterAcquiredTraitAggregateState current = acquiredTraitState?.Clone()
            ?? new CharacterAcquiredTraitAggregateState();
        current.EnsureCollections();
        CharacterAcquiredTraitModuleSO[] definitions = (moduleDefinitions
            ?? Array.Empty<CharacterAcquiredTraitModuleSO>()).ToArray();
        List<CharacterAcquiredTraitValidationIssue> issues = ValidateAcquiredTraitCandidate(
            current, candidate, expectedRevision, settings, definitions);
        string[] evidence = (evidenceFactIds ?? Array.Empty<string>()).ToArray();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = (evidenceBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.Clone()).ToArray();
        HashSet<string> exactIds = exactBindings
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        if (evidence.Length == 0
            || evidence.Any(value => !IsCanonicalProjectedTraitEvidenceId(value))
            || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length)
        {
            issues.Add(new CharacterAcquiredTraitValidationIssue(
                CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                "Formula acquired-trait evidence IDs must be non-empty, distinct canonical public-fact:sha256 IDs."));
        }

        List<CharacterNarrativeFact> selectedFacts = new List<CharacterNarrativeFact>();
        if (issues.All(value => value.Code != CharacterAcquiredTraitValidationIssueCode.InvalidEvidence))
        {
            foreach (string evidenceId in evidence)
            {
                if (exactIds.Contains(evidenceId))
                    continue;
                if (!CharacterAcquiredTraitEvidenceProjection.TryResolve(
                        NarrativeLedger,
                        evidenceId,
                        out CharacterNarrativeFact[] resolved,
                        out string resolutionError)
                    || resolved.Length != 1
                    || resolved[0].influenceUseCount == int.MaxValue)
                {
                    issues.Add(new CharacterAcquiredTraitValidationIssue(
                        CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                        resolved.Length == 1 && resolved[0].influenceUseCount == int.MaxValue
                            ? $"Formula acquired-trait evidence '{evidenceId}' exhausted its influence counter."
                            : resolutionError));
                    continue;
                }
                selectedFacts.Add(resolved[0]);
            }
        }

        if (candidate != null && evidence.Length > 0)
        {
            HashSet<string> previousIds = new HashSet<string>(
                (current.instances ?? new List<CharacterAcquiredTraitInstanceState>())
                .Where(value => value != null)
                .Select(value => value.instanceId),
                StringComparer.Ordinal);
            CharacterAcquiredTraitInstanceState[] published = (candidate.instances
                    ?? new List<CharacterAcquiredTraitInstanceState>())
                .Where(value => value != null
                    && value.formulaVersion > 0
                    && value.acceptedRevision == candidate.revision
                    && !previousIds.Contains(value.instanceId))
                .ToArray();
            string[] canonicalEvidence = evidence.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] publishedExact = published.Length == 1
                ? (published[0].evidenceBindings
                        ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                    .Where(value => value != null)
                    .Select(value => value.publicFactId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (published.Length != 1
                || published[0].evidenceFactIds == null
                || !published[0].evidenceFactIds.SequenceEqual(canonicalEvidence, StringComparer.Ordinal)
                || !publishedExact.SequenceEqual(
                    exactIds.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || exactIds.Any(value => !evidence.Contains(value, StringComparer.Ordinal)))
            {
                issues.Add(new CharacterAcquiredTraitValidationIssue(
                    CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                    "Formula acquired-trait commit must publish exactly one new v1 instance with the exact consumed evidence IDs."));
            }
        }

        if (issues.Count > 0)
        {
            validationIssues = issues;
            return false;
        }

        CharacterAcquiredTraitAggregateState frozenCandidate = candidate.Clone();
        CharacterAcquiredTraitAggregateState previous = acquiredTraitState?.Clone()
            ?? new CharacterAcquiredTraitAggregateState();
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        if (exactBindings.Length > 0)
        {
            CharacterAcquiredTraitInstanceState published = frozenCandidate.instances
                .Single(value => value != null
                    && value.formulaVersion > 0
                    && value.acceptedRevision == frozenCandidate.revision);
            string prepareFailure = evidenceUseTransaction == null
                ? "evidence-transaction-unavailable"
                : string.Empty;
            if (evidenceUseTransaction == null
                || !evidenceUseTransaction.TryPrepareBindings(
                    "acquired-trait",
                    string.IsNullOrWhiteSpace(published.presentationId)
                        ? published.instanceId
                        : published.presentationId,
                    exactBindings,
                    out prepared,
                    out prepareFailure)
                || !prepared.TryCommitAnchors(out prepareFailure))
            {
                prepared?.Cancel();
                issues.Add(new CharacterAcquiredTraitValidationIssue(
                    CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                    "Gameplay-outcome evidence anchor rejected: " + prepareFailure));
                validationIssues = issues;
                return false;
            }
        }
        try
        {
            acquiredTraitState = frozenCandidate;
            foreach (CharacterNarrativeFact fact in selectedFacts)
                fact.influenceUseCount++;
            prepared?.CompleteOwnerCommit();
        }
        catch
        {
            acquiredTraitState = previous;
            foreach (CharacterNarrativeFact fact in selectedFacts)
                fact.influenceUseCount--;
            if (prepared != null && !prepared.TryRollbackAnchors(out string rollbackFailure))
                Debug.LogError("Acquired-trait evidence anchor rollback failed: "
                    + rollbackFailure);
            throw;
        }
        NotifyChangedWithoutAffectingCommittedState();
        validationIssues = Array.Empty<CharacterAcquiredTraitValidationIssue>();
        return true;
    }

    private void NotifyChangedWithoutAffectingCommittedState()
    {
        Delegate[] handlers = Changed?.GetInvocationList();
        if (handlers == null) return;
        for (int index = 0; index < handlers.Length; index++)
        {
            try
            {
                ((Action)handlers[index]).Invoke();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                                               and not StackOverflowException
                                               and not AccessViolationException)
            {
                Debug.LogException(exception);
            }
        }
    }

    private List<CharacterAcquiredTraitValidationIssue> ValidateAcquiredTraitCandidate(
        CharacterAcquiredTraitAggregateState current,
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions)
    {
        List<CharacterAcquiredTraitValidationIssue> issues = new List<CharacterAcquiredTraitValidationIssue>();
        if (expectedRevision < 0
            || current == null
            || current.revision != expectedRevision
            || expectedRevision == int.MaxValue
            || candidate == null
            || candidate.revision != expectedRevision + 1)
        {
            issues.Add(new CharacterAcquiredTraitValidationIssue(
                CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                $"Acquired-trait commit expected revision {expectedRevision}, "
                + $"current revision is {current?.revision.ToString() ?? "missing"}, and candidate revision is "
                + $"{candidate?.revision.ToString() ?? "missing"}."));
        }
        if (candidate != null)
        {
            issues.AddRange(CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                candidate,
                NarrativeLedger,
                settings,
                moduleDefinitions));
        }
        return issues;
    }

    private static bool IsCanonicalProjectedTraitEvidenceId(string value)
    {
        string prefix = CharacterAcquiredTraitEvidenceProjection.ProjectedFactIdPrefix;
        return value != null
            && value.Length == prefix.Length + 64
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && value.Skip(prefix.Length).All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    [GameplayInternalOnly(
        "A failed cross-aggregate erasure transaction restores the exact pre-commit acquired-trait aggregate.",
        "MemoryErasureSealTransactionService")]
    public bool TryRollbackAcquiredTraitState(
        CharacterAcquiredTraitAggregateState rollbackState,
        int expectedCurrentRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        CharacterAcquiredTraitAggregateState current =
            CaptureAcquiredTraitState();
        List<CharacterAcquiredTraitValidationIssue> issues = new();
        if (rollbackState == null
            || expectedCurrentRevision <= 0
            || current.revision != expectedCurrentRevision
            || rollbackState.revision != expectedCurrentRevision - 1)
        {
            issues.Add(new CharacterAcquiredTraitValidationIssue(
                CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                "Acquired-trait rollback does not match the currently published "
                + $"revision {current.revision}."));
        }
        if (rollbackState != null)
        {
            issues.AddRange(
                CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                    rollbackState,
                    NarrativeLedger,
                    settings,
                    moduleDefinitions));
        }
        if (issues.Count > 0)
        {
            validationIssues = issues;
            return false;
        }

        // Restore the exact previous revision instead of publishing a second
        // gameplay transition. This method is only legal while the paired
        // physical mutation is still held by its synchronous rollback handle.
        acquiredTraitState = rollbackState.Clone();
        validationIssues = Array.Empty<CharacterAcquiredTraitValidationIssue>();
        Changed?.Invoke();
        return true;
    }

    public CharacterRuntimeProfile GetEffectiveRuntimeProfile()
    {
        EnsureInitialized();
        return RequireProfileProjector().GetEffectiveRuntimeProfile(
            actor,
            GrowthState);
    }

    public CharacterSkillSlotProfile GetSlotProfile()
    {
        return CharacterSkillSlotProfile.For(actor?.Identity?.Data, actor != null && actor.IsOwner);
    }

    public void RecordNarrative(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value = 0f,
        int day = 0,
        bool triggerPassives = true,
        GameplayNarrativeEventContext eventContext = null)
    {
        if (string.IsNullOrWhiteSpace(factId)) return;
        RecordNarrative(
            domain,
            factId,
            subjectId,
            outcome,
            value,
            day,
            CharacterNarrativeEvidenceMetadata.Ordinary(domain, factId),
            triggerPassives,
            eventContext);
    }

    public void RecordNarrative(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        CharacterNarrativeEvidenceMetadata evidenceMetadata,
        bool triggerPassives = true,
        GameplayNarrativeEventContext eventContext = null)
    {
        NarrativeLedger.Record(domain, factId, subjectId, outcome, value, day, evidenceMetadata, eventContext);
        EnsureUnlockedDrafts();
        if (triggerPassives)
        {
            TriggerPassivesForNarrativeDomain(domain);
        }

        Changed?.Invoke();
    }

    public void RecordNarrative(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        float importancePoints,
        bool triggerPassives = true)
    {
        RecordNarrative(
            domain,
            factId,
            subjectId,
            outcome,
            value,
            day,
            new CharacterNarrativeEvidenceMetadata(
                eventGroupKey,
                actionKey,
                relationshipKey,
                importancePoints),
            triggerPassives);
    }

    public bool CanUseUltimate(CharacterUltimateDomain domain, int serial)
    {
        return Ultimate != null
            && Ultimate.ultimateDomain == domain
            && GrowthState.useLimits.CanUse(domain, serial);
    }

    public bool TryMarkUltimateUsed(CharacterUltimateDomain domain, int serial)
    {
        if (!CanUseUltimate(domain, serial))
        {
            return false;
        }

        GrowthState.useLimits.MarkUsed(domain, serial);
        Changed?.Invoke();
        return true;
    }

    public void MarkGenerationRequestPending(string requestKey)
    {
        if (!string.IsNullOrWhiteSpace(requestKey)
            && !GrowthState.pendingRequestKeys.Contains(requestKey, StringComparer.Ordinal))
        {
            GrowthState.pendingRequestKeys.Add(requestKey);
        }
    }

    public void MarkGenerationRequestCompleted(string requestKey)
    {
        GrowthState.pendingRequestKeys.RemoveAll(key => string.Equals(key, requestKey, StringComparison.Ordinal));
    }

    public void OnDraftReady(CharacterSkillDraft draft)
    {
        if (draft == null || !draft.isReady)
        {
            return;
        }

        DraftReady?.Invoke(draft);
        if (draft.kind == CharacterSkillKind.Active)
        {
            if (GrowthState.autoChooseDrafts)
            {
                int bestIndex = ChooseBestCandidateIndex(draft);
                TryChooseActiveSkill(draft.unlockLevel, bestIndex, confirmed: true, out _);
            }
            else if (!suppressPublicSkillNotifications)
            {
                notifications?.NotifyActiveDraftReady(
                    actor,
                    draft.unlockLevel,
                    RequestGrowthTab);
            }
        }
        else if (draft.kind == CharacterSkillKind.Passive)
        {
            CommitAutomaticPassive(draft);
        }
        else if (draft.kind == CharacterSkillKind.Ultimate)
        {
            CommitAutomaticUltimate(draft);
        }

        NotifyChangedWithoutAffectingCommittedState();
    }

    private void RequestGrowthTab()
    {
        if (actor == null)
        {
            return;
        }

        notifications?.ShowGrowth(actor);
    }

    public CharacterProgressionSnapshot CapturePersistentState()
    {
        EnsureInitialized();
        return new CharacterProgressionSnapshot(
            Level,
            CurrentExperience,
            GrowthState.Clone(),
            NarrativeLedger.Clone(),
            CaptureAcquiredTraitState());
    }

    public void RestorePersistentState(CharacterProgressionSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        CharacterAcquiredTraitAggregateState restoredAcquiredTraits =
            snapshot.AcquiredTraitState.Clone();
        ValidateRestoredFormulaState(snapshot.GrowthState, snapshot.NarrativeLedger);
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> acquiredTraitIssues =
            CharacterAcquiredTraitStateValidator.ValidatePersistentState(
                restoredAcquiredTraits,
                snapshot.NarrativeLedger);
        if (acquiredTraitIssues.Count > 0)
        {
            throw new InvalidOperationException(
                "Character progression restore rejected acquired-trait state: "
                + string.Join(" | ", acquiredTraitIssues));
        }

        generationService?.CancelRequests(this);
        CharacterProgressionTransition transition =
            CharacterProgressionRules.NormalizeRestoredState(
                snapshot.Level,
                snapshot.CurrentExperience);
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        growthState = snapshot.GrowthState?.Clone() ?? new CharacterGrowthState();
        narrativeLedger = snapshot.NarrativeLedger?.Clone() ?? new CharacterNarrativeLedger();
        acquiredTraitState = restoredAcquiredTraits;
        InvalidateEffectiveRuntimeProfile();
        GrowthState.EnsureCollections();
        RebuildLegacySkillViews();
        EnsureInitialized();
        WarmEffectiveRuntimeProfile();
        EnsureUnlockedDrafts();
        Changed?.Invoke();
    }

    public void RestorePersistentState(
        int restoredLevel,
        int restoredExperience,
        IEnumerable<string> restoredLearnedSkillIds,
        IEnumerable<string> restoredEquippedSkillIds)
    {
        CharacterProgressionTransition transition =
            CharacterProgressionRules.NormalizeRestoredState(
                restoredLevel,
                restoredExperience);
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        EnsureInitialized();
        Changed?.Invoke();
    }

    public void ApplyPreparedIdentity(
        string displayName,
        string origin,
        IEnumerable<int> traitIds,
        CharacterPotentialGrade potential,
        int generationSeed,
        bool autoChooseDrafts,
        int? startingProficiencySeed = null,
        CharacterStartingProfileState startingProfile = null,
        IEnumerable<CharacterStartingProficiencyExperience>
            preparedStartingProficiencies = null,
        int maximumTraitCount = 4)
    {
        if (maximumTraitCount is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTraitCount));
        }

        generationService?.CancelRequests(this);
        GrowthState.skillGenerationRevision++;
        GrowthState.initialized = true;
        GrowthState.displayName = displayName?.Trim() ?? string.Empty;
        GrowthState.origin = origin?.Trim() ?? string.Empty;
        GrowthState.traitSelectionAuthorityVersion =
            CharacterGrowthState.CurrentTraitSelectionAuthorityVersion;
        GrowthState.traitSelectionAuthorityOrigin =
            CharacterTraitSelectionAuthorityOrigin.PreparedSelection;
        GrowthState.traitIds = traitIds?
            .Distinct()
            .Take(maximumTraitCount)
            .ToList() ?? new List<int>();
        GrowthState.startingProfile = startingProfile?.Clone()
            ?? new CharacterStartingProfileState();
        GrowthState.startingProficiencies = (preparedStartingProficiencies
                ?? CharacterStartingProficiencyRules.Create(
                    startingProficiencySeed ?? generationSeed))
            .Select(value => value.Clone())
            .ToList();
        CharacterStartingProficiencyRules.Validate(
            GrowthState.startingProficiencies);
        GrowthState.potentialGrade = potential;
        GrowthState.generationSeed = generationSeed;
        GrowthState.autoChooseDrafts = autoChooseDrafts;
        GrowthState.activeSkills.Clear();
        GrowthState.passiveSkills.Clear();
        GrowthState.ultimate = null;
        GrowthState.drafts.Clear();
        GrowthState.pendingRequestKeys.Clear();
        GrowthState.nextActiveDraftHasPity = false;
        acquiredTraitState = new CharacterAcquiredTraitAggregateState();
        InvalidateEffectiveRuntimeProfile();
        WarmEffectiveRuntimeProfile();
        EnsureUnlockedDrafts();
        actor?.Stats?.RecalculateVitals(resetCurrentHealth: true);
        Changed?.Invoke();
    }

    private void CompleteConfigurationIfReady()
    {
        if (profileProjector == null)
        {
            return;
        }

        EnsureInitialized();
        WarmEffectiveRuntimeProfile();
        EnsureUnlockedDrafts();
    }

    private void EnsureInitialized()
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        GrowthState.EnsureCollections();
        acquiredTraitState ??= new CharacterAcquiredTraitAggregateState();
        acquiredTraitState.EnsureCollections();
        if (actor?.Identity?.Data == null)
        {
            return;
        }

        RequireProfileProjector().EnsureInitialized(
            actor,
            GrowthState,
            SkillSettings);
    }

    private void EnsureUnlockedDrafts()
    {
        if (generationService == null || !GrowthState.initialized)
        {
            return;
        }

        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        foreach (int unlockLevel in settings.activeUnlockLevels.Where(unlock => unlock <= Level))
        {
            EnsureDraft(CharacterSkillKind.Active, unlockLevel);
        }

        if (GrowthState.passiveSkills.Count == 0
            && !HasDraft(CharacterSkillKind.Passive, 1))
        {
            EnsureDraft(CharacterSkillKind.Passive, 1);
        }

        if (Level >= settings.secondPassiveMinimumLevel
            && GrowthState.passiveSkills.Count < GetSlotProfile().PassiveSlots
            && NarrativeLedger.MeaningfulRecordCount >= settings.secondPassiveMinimumRecords
            && NarrativeLedger.MeaningfulDomainCount >= settings.secondPassiveMinimumDomains)
        {
            EnsureDraft(CharacterSkillKind.Passive, settings.secondPassiveMinimumLevel);
        }

        if (Level >= MaxLevel && GrowthState.ultimate == null)
        {
            EnsureDraft(CharacterSkillKind.Ultimate, MaxLevel);
        }

        ResumePendingRequests();
    }

    private void EnsureDraft(CharacterSkillKind kind, int unlockLevel)
    {
        if (HasDraft(kind, unlockLevel))
        {
            return;
        }

        CharacterSkillDraft draft = generationService.CreateDraft(
            this,
            kind,
            unlockLevel,
            GrowthState.skillGenerationRevision);
        GrowthState.drafts.Add(draft);
        generationService.RequestDraft(this, draft);
    }

    private bool HasDraft(CharacterSkillKind kind, int unlockLevel)
    {
        return GrowthState.drafts.Any(item => item != null
            && item.kind == kind
            && item.unlockLevel == unlockLevel);
    }

    private void ResumePendingRequests()
    {
        if (generationService == null)
        {
            return;
        }

        foreach (CharacterSkillDraft draft in GrowthState.drafts.Where(item => item != null
            && !item.isReady
            && !item.permanentlyChosen))
        {
            generationService.RequestDraft(this, draft);
        }
    }

    private void AllocateStatsForReachedLevel(int reachedLevel)
    {
        // V26: generic character levels unlock narrative skills only.
        // Proficiency grows exclusively from approved work, combat, and mentoring.
    }


    private void TriggerPassivesForNarrativeDomain(CharacterNarrativeDomain domain)
    {
        CharacterSkillTrigger trigger = domain switch
        {
            CharacterNarrativeDomain.Work or CharacterNarrativeDomain.FacilityUse => CharacterSkillTrigger.WorkCompleted,
            CharacterNarrativeDomain.Need => CharacterSkillTrigger.NeedChanged,
            CharacterNarrativeDomain.Mood => CharacterSkillTrigger.MoodChanged,
            CharacterNarrativeDomain.Relationship => CharacterSkillTrigger.RelationshipChanged,
            CharacterNarrativeDomain.Invasion => CharacterSkillTrigger.InvasionStarted,
            CharacterNarrativeDomain.Injury => CharacterSkillTrigger.DamageTaken,
            _ => CharacterSkillTrigger.BattleCompleted
        };
        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(actor, trigger);
    }

    private void CommitAutomaticPassive(CharacterSkillDraft draft)
    {
        if (draft.permanentlyChosen
            || draft.candidates == null
            || draft.candidates.Count == 0
            || GrowthState.passiveSkills.Count >= GetSlotProfile().PassiveSlots)
        {
            return;
        }

        CharacterSkillInstance skill = draft.candidates[0].Clone();
        if (!TryCommitFormulaEvidence(skill, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = 0;
                GrowthState.passiveSkills.Add(skill);
            }, out string evidenceError))
        {
            throw new InvalidOperationException(evidenceError);
        }
        if (!suppressPublicSkillNotifications)
        {
            NotifySkillUnlockedWithoutAffectingCommittedState(
                skill,
                isUltimate: false);
        }
    }

    private void CommitAutomaticUltimate(CharacterSkillDraft draft)
    {
        if (draft.permanentlyChosen || draft.candidates == null || draft.candidates.Count == 0)
        {
            return;
        }

        CharacterSkillInstance skill = draft.candidates[0].Clone();
        if (!TryCommitFormulaEvidence(skill, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = 0;
                GrowthState.ultimate = skill;
            }, out string evidenceError))
        {
            throw new InvalidOperationException(evidenceError);
        }
        if (!suppressPublicSkillNotifications)
        {
            NotifySkillUnlockedWithoutAffectingCommittedState(
                GrowthState.ultimate,
                isUltimate: true);
        }
    }

    private void NotifySkillUnlockedWithoutAffectingCommittedState(
        CharacterSkillInstance skill,
        bool isUltimate)
    {
        if (notifications == null)
            return;
        try
        {
            notifications.NotifySkillUnlocked(skill, isUltimate);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
                                           and not StackOverflowException
                                           and not AccessViolationException)
        {
            Debug.LogException(exception);
        }
    }

    private int ChooseBestCandidateIndex(CharacterSkillDraft draft)
    {
        if (draft?.formulaVersion > 0)
        {
            return 0;
        }
        int bestIndex = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < draft.candidates.Count; i++)
        {
            CharacterSkillInstance candidate = draft.candidates[i];
            float score = (int)candidate.rarity * 100f;
            foreach (CharacterSkillModuleSelection module in candidate.modules ?? new List<CharacterSkillModuleSelection>())
            {
                score += 1f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private bool TryCommitFormulaEvidence(
        CharacterSkillInstance skill,
        Action ownerCommit,
        out string error)
    {
        error = string.Empty;
        if (skill == null)
        {
            error = "Cannot commit a null CharacterSkill.";
            return false;
        }
        if (ownerCommit == null)
        {
            error = "CharacterSkill owner commit is missing.";
            return false;
        }
        if (skill.formulaVersion == 0)
        {
            ownerCommit();
            return true;
        }
        try
        {
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                skill,
                SkillSettings);
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }

        string[] ids = (skill.evidenceIds ?? new List<string>()).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            error = "Formula skill evidence IDs are blank or duplicated.";
            return false;
        }
        CharacterNarrativeFact[] factValues = NarrativeLedger.Facts
            .Where(value => value != null).ToArray();
        if (factValues.Select(CharacterSkillFormulaGeneration.BuildEvidenceId)
            .Distinct(StringComparer.Ordinal).Count() != factValues.Length)
        {
            error = "The narrative ledger contains duplicate formula evidence identities.";
            return false;
        }
        Dictionary<string, CharacterNarrativeFact> facts = factValues
            .ToDictionary(CharacterSkillFormulaGeneration.BuildEvidenceId, StringComparer.Ordinal);
        List<CharacterNarrativeFact> selected = new List<CharacterNarrativeFact>();
        HashSet<string> exactIds = (skill.evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (exactIds.Contains(id))
                continue;
            if (!facts.TryGetValue(id, out CharacterNarrativeFact fact)
                || fact.influenceUseCount == int.MaxValue)
            {
                error = $"Formula skill references unknown or exhausted evidence '{id}'.";
                return false;
            }
            selected.Add(fact);
        }
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        if (exactIds.Count > 0)
        {
            if (evidenceUseTransaction == null
                || !evidenceUseTransaction.TryPrepareBindings(
                    "character-skill",
                    skill.presentationId,
                    skill.evidenceBindings,
                    out prepared,
                    out error))
                return false;
            if (!prepared.TryCommitAnchors(out error))
            {
                prepared.Cancel();
                return false;
            }
        }
        try
        {
            foreach (CharacterNarrativeFact fact in selected)
                fact.influenceUseCount++;
            ownerCommit();
            prepared?.CompleteOwnerCommit();
            return true;
        }
        catch
        {
            foreach (CharacterNarrativeFact fact in selected)
                fact.influenceUseCount--;
            if (prepared != null && !prepared.TryRollbackAnchors(out string rollbackFailure))
                Debug.LogError("CharacterSkill evidence anchor rollback failed: " + rollbackFailure);
            throw;
        }
    }

    private void ValidateRestoredFormulaState(
        CharacterGrowthState restoredGrowth,
        CharacterNarrativeLedger restoredLedger)
    {
        if (restoredGrowth == null) return;
        IEnumerable<CharacterSkillInstance> committed =
            (restoredGrowth.activeSkills ?? new List<CharacterSkillInstance>())
            .Concat(restoredGrowth.passiveSkills ?? new List<CharacterSkillInstance>())
            .Concat(restoredGrowth.ultimate == null
                ? Array.Empty<CharacterSkillInstance>()
                : new[] { restoredGrowth.ultimate });
        IEnumerable<CharacterSkillInstance> pending =
            (restoredGrowth.drafts ?? new List<CharacterSkillDraft>())
            .Where(value => value != null)
            .SelectMany(value => value.frozenMechanics ?? new List<CharacterSkillInstance>());
        HashSet<string> availableEvidence = new HashSet<string>(
            (restoredLedger?.Facts ?? Array.Empty<CharacterNarrativeFact>())
                .Where(value => value != null)
                .Select(CharacterSkillFormulaGeneration.BuildEvidenceId),
            StringComparer.Ordinal);
        foreach (CharacterSkillInstance skill in committed.Concat(pending)
            .Where(value => value != null && value.formulaVersion > 0))
        {
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(skill, SkillSettings);
            if ((skill.evidenceIds ?? new List<string>()).Any(id => !availableEvidence.Contains(id)))
                throw new InvalidOperationException(
                    $"Character progression restore rejected unknown formula evidence on skill '{skill.id}'.");
        }
        foreach (CharacterSkillInstance skill in committed
            .Where(value => value != null && value.formulaVersion > 0 && !value.IsReady))
            throw new InvalidOperationException(
                $"Character progression restore rejected unpublished formula skill '{skill.id}'.");
    }

    private void RebuildLegacySkillViews()
    {
        learnedSkillIds.Clear();
        equippedSkillIds.Clear();
        foreach (CharacterSkillInstance skill in GrowthState.activeSkills
            .Concat(GrowthState.passiveSkills)
            .Append(GrowthState.ultimate)
            .Where(item => item != null && item.IsReady))
        {
            if (!learnedSkillIds.Contains(skill.id, StringComparer.Ordinal))
            {
                learnedSkillIds.Add(skill.id);
            }

            if (skill.kind == CharacterSkillKind.Active
                && !equippedSkillIds.Contains(skill.id, StringComparer.Ordinal))
            {
                equippedSkillIds.Add(skill.id);
            }
        }
    }

    private void InvalidateEffectiveRuntimeProfile()
    {
        RequireProfileProjector().Invalidate();
    }

    private void WarmEffectiveRuntimeProfile()
    {
        RequireProfileProjector().Warm(actor, GrowthState);
    }

    private CharacterProgressionProfileProjector RequireProfileProjector()
    {
        return profileProjector
            ?? throw new InvalidOperationException(
                "Character progression profile projector is not configured.");
    }
}

public sealed class CharacterProgressionSnapshot
{
    public CharacterProgressionSnapshot(
        int level,
        int currentExperience,
        CharacterGrowthState growthState,
        CharacterNarrativeLedger narrativeLedger)
        : this(
            level,
            currentExperience,
            growthState,
            narrativeLedger,
            new CharacterAcquiredTraitAggregateState())
    {
    }

    public CharacterProgressionSnapshot(
        int level,
        int currentExperience,
        CharacterGrowthState growthState,
        CharacterNarrativeLedger narrativeLedger,
        CharacterAcquiredTraitAggregateState acquiredTraitState)
    {
        Level = Mathf.Clamp(level, 1, CharacterProgression.MaxLevel);
        CurrentExperience = Mathf.Max(0, currentExperience);
        GrowthState = growthState?.Clone() ?? new CharacterGrowthState();
        NarrativeLedger = narrativeLedger?.Clone() ?? new CharacterNarrativeLedger();
        AcquiredTraitState = (acquiredTraitState
            ?? throw new ArgumentNullException(nameof(acquiredTraitState))).Clone();
    }

    public CharacterProgressionSnapshot(
        int level,
        int currentExperience,
        IEnumerable<string> learnedSkillIds,
        IEnumerable<string> equippedSkillIds)
        : this(level, currentExperience, new CharacterGrowthState(), new CharacterNarrativeLedger())
    {
    }

    public int Level { get; }
    public int CurrentExperience { get; }
    public CharacterGrowthState GrowthState { get; }
    public CharacterNarrativeLedger NarrativeLedger { get; }
    public CharacterAcquiredTraitAggregateState AcquiredTraitState { get; }
    public IReadOnlyList<string> LearnedSkillIds => GrowthState.activeSkills?
        .Where(item => item != null).Select(item => item.id).ToArray()
        ?? Array.Empty<string>();
    public IReadOnlyList<string> EquippedSkillIds => LearnedSkillIds;
}
