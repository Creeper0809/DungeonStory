using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public sealed class CharacterProgression : MonoBehaviour, ICharacterTraitInferenceAuthority
{
    private const string PreparedOriginFactPrefix = "identity:prepared-origin:";
    private const string PreparedHistoryFactPrefix = "identity:prepared-history:";
    private const string PreparedIdentityEventGroup = "identity:prepared";

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
    private IGameEventBus levelEvents;
    private CharacterProgressionProfileProjector profileProjector;
    private IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction;
    private IDomainFactPublisher facts;
    private CharacterSkillAcquisitionPublicationRuntime skillAcquisitions;
    private bool acquisitionRollbackPending;
    private CharacterTraitInferenceTransaction traitInference;
    private CharacterTraitInferenceTransaction TraitInference =>
        traitInference ??= new CharacterTraitInferenceTransaction(this);
    private IGameSessionStateProvider sessionStateProvider;
    private ICharacterSkillActivationTransaction skillActivationTransactions;
    private bool suppressPublicSkillNotifications;
    private bool preparationPreview;

    public CharacterActor Actor => actor;
    public CharacterSkillSystemSettingsSO SkillSettings => settingsProvider?.Settings
        ?? throw new InvalidOperationException(
            "Character progression has no authored skill settings.");
    public int Level
    { get { RequireTraitStateAvailable(); return Mathf.Clamp(level, 1, MaxLevel); } }
    public int CurrentExperience => Level >= MaxLevel ? 0 : Mathf.Max(0, currentExperience);
    public int ExperienceToNextLevel => Level >= MaxLevel ? 0 : GetExperienceRequired(Level);
    public float ExperienceRatio => Level >= MaxLevel
        ? 1f
        : CharacterProgressionRules.GetExperienceRatio(Level, CurrentExperience);
    public CharacterGrowthState GrowthState
    {
        get
        {
            RequireTraitStateAvailable();
            if (acquisitionRollbackPending)
                throw new InvalidOperationException("skill-acquisition-evidence-rollback-pending");
            return (growthState ?? new CharacterGrowthState()).Clone();
        }
    }

    private CharacterGrowthState OwnedGrowth
    {
        get
        {
            RequireTraitStateAvailable();
            if (acquisitionRollbackPending)
                throw new InvalidOperationException("skill-acquisition-evidence-rollback-pending");
            growthState ??= new CharacterGrowthState();
            growthState.EnsureCollections();
            return growthState;
        }
    }
    internal int SkillGenerationRevision => OwnedGrowth.skillGenerationRevision;
    internal string InitializedDisplayName
    {
        get
        {
            CharacterGrowthState current = OwnedGrowth;
            return current.initialized ? current.displayName : string.Empty;
        }
    }
    public CharacterNarrativeLedger NarrativeLedger
    {
        get
        {
            RequireTraitStateAvailable();
            return narrativeLedger ??= new CharacterNarrativeLedger();
        }
    }
    public int AcquiredTraitRevision
    { get { RequireTraitStateAvailable(); return acquiredTraitState?.revision ?? 0; } }
    public CharacterPotentialGrade PotentialGrade => OwnedGrowth.potentialGrade;
    public IReadOnlyList<CharacterSkillInstance> ActiveSkills => OwnedGrowth.activeSkills.Select(value => value.Clone()).ToArray();
    public IReadOnlyList<CharacterSkillInstance> PassiveSkills => OwnedGrowth.passiveSkills.Select(value => value.Clone()).ToArray();
    public IReadOnlyList<CharacterSkillInstance> OwnerFixedSkills =>
        CharacterOwnerFixedSkillUtility.GetSkills(actor?.Identity?.Data, settingsProvider?.Settings);
    public CharacterSkillInstance Ultimate => OwnedGrowth.ultimate?.Clone();
    public IReadOnlyList<CharacterSkillDraft> Drafts => OwnedGrowth.drafts.Select(value => value?.Clone()).ToArray();
    public IReadOnlyList<string> LearnedSkillIds
    {
        get
        {
            RebuildLegacySkillViews();
            return learnedSkillIds.ToArray();
        }
    }
    public IReadOnlyList<string> EquippedSkillIds
    {
        get
        {
            RebuildLegacySkillViews();
            return equippedSkillIds.ToArray();
        }
    }

    public event Action Changed;
    public event Action<CharacterSkillDraft> DraftReady;

    internal ICharacterSkillActivationTransaction SkillActivationTransactions =>
        skillActivationTransactions;

    [Inject]
    public void ConstructSkillActivationRuntime(
        ICharacterSkillActivationTransaction transactions)
    {
        skillActivationTransactions = transactions
            ?? throw new ArgumentNullException(nameof(transactions));
    }

    internal void PublishSkillActivationChanged() => Changed?.Invoke();

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
        IGameEventBus gameEventBus,
        IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction,
        IDomainFactPublisher facts = null,
        CharacterSkillAcquisitionPublicationRuntime skillAcquisitions = null,
        IGameSessionStateProvider sessionStateProvider = null)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.notifications = notifications
            ?? throw new ArgumentNullException(nameof(notifications));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        levelEvents = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.evidenceUseTransaction = evidenceUseTransaction
            ?? throw new ArgumentNullException(nameof(evidenceUseTransaction));
        this.facts = facts;
        this.skillAcquisitions = skillAcquisitions;
        this.sessionStateProvider = sessionStateProvider;
        preparationPreview = false;
        CompleteConfigurationIfReady();
    }

    public void ConstructCharacterProgression(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionNotificationApplicationAdapter notifications,
        CharacterProgressionProfileProjector profileProjector,
        IGameEventBus gameEventBus)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.notifications = notifications
            ?? throw new ArgumentNullException(nameof(notifications));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        levelEvents = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        preparationPreview = false;
        CompleteConfigurationIfReady();
    }

    public void ConfigurePreview(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionProfileProjector profileProjector)
        => ConfigurePreparedGeneration(generationService, settingsProvider, profileProjector, preview: true);

    private void ConfigurePreparedGeneration(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionProfileProjector profileProjector,
        bool preview)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.profileProjector = profileProjector
            ?? throw new ArgumentNullException(nameof(profileProjector));
        preparationPreview = preview;
        CompleteConfigurationIfReady();
    }

    public void ConfigurePopulationPreparation(
        ICharacterSkillGenerationService generationService,
        ICharacterSkillSystemSettingsProvider settingsProvider,
        CharacterProgressionProfileProjector profileProjector,
        IGameplayOutcomeEvidenceUseTransaction evidenceUseTransaction,
        IDomainFactPublisher facts,
        CharacterSkillAcquisitionPublicationRuntime skillAcquisitions,
        IGameSessionStateProvider sessionStateProvider)
    {
        // Unlike a reroll preview, a population profile already owns a registered
        // world identity. Its automatic acquisitions use the same publication
        // transaction as a spawned actor, before generation can invoke callbacks.
        this.evidenceUseTransaction = evidenceUseTransaction
            ?? throw new ArgumentNullException(nameof(evidenceUseTransaction));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.skillAcquisitions = skillAcquisitions
            ?? throw new ArgumentNullException(nameof(skillAcquisitions));
        this.sessionStateProvider = sessionStateProvider
            ?? throw new ArgumentNullException(nameof(sessionStateProvider));
        ConfigurePreparedGeneration(generationService, settingsProvider, profileProjector, preview: false);
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
        RequireTraitStateAvailable();
        EnsureInitialized();
        if (amount <= 0 || Level >= MaxLevel)
        {
            return 0;
        }

        CharacterProgressionTransition transition = CharacterProgressionRules.AddExperience(
            level,
            currentExperience,
            amount);
        CharacterLevelChangedEvent? levelChange = CaptureLevelTransition(
            transition, CharacterLevelChangeCause.Experience);
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        CompleteExperienceTransition(transition, levelChange);
        return transition.LevelDelta;
    }

    private void CompleteExperienceTransition(
        CharacterProgressionTransition transition,
        CharacterLevelChangedEvent? levelChange)
    {
        foreach (int reachedLevel in transition.ReachedLevels)
        {
            AllocateStatsForReachedLevel(reachedLevel);
        }

        if (transition.HasLevelChanged)
        {
            actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
            PublishLevelTransition(levelChange);
        }

        EnsureUnlockedDrafts();
        NotifyChangedWithoutAffectingCommittedState();
    }

    public bool EnsureMinimumLevel(
        int targetLevel,
        CharacterLevelChangeCause cause = CharacterLevelChangeCause.MinimumLevelRequirement)
    {
        RequireTraitStateAvailable();
        if (!Enum.IsDefined(typeof(CharacterLevelChangeCause), cause)
            || cause == CharacterLevelChangeCause.Experience)
            throw new ArgumentException("minimum-level-cause-invalid", nameof(cause));
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

        CharacterLevelChangedEvent? levelChange = CaptureLevelTransition(transition, cause);
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        foreach (int reachedLevel in transition.ReachedLevels)
        {
            AllocateStatsForReachedLevel(reachedLevel);
        }

        actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
        PublishLevelTransition(levelChange);

        EnsureUnlockedDrafts();
        NotifyChangedWithoutAffectingCommittedState();
        return true;
    }

    private CharacterLevelChangedEvent? CaptureLevelTransition(
        CharacterProgressionTransition transition,
        CharacterLevelChangeCause cause)
    {
        if (!transition.HasLevelChanged || actor == null)
            return null;
        if (levelEvents == null)
            throw new InvalidOperationException("character-level-event-channel-missing");
        return new CharacterLevelChangedEvent(
            actor.BuildingCharacterId, transition.PreviousLevel, transition.Level, cause);
    }

    private void PublishLevelTransition(CharacterLevelChangedEvent? levelChange)
    {
        if (!levelChange.HasValue)
            return;
        try { levelEvents.Publish(levelChange.Value); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // Observation failure must not interrupt already committed progression
            // or make the caller retry the XP award. Keep the failure explicit.
            Debug.LogException(exception, this);
        }
    }

    public void SetAutoChooseSkillDrafts(bool autoChoose)
    {
        EnsureInitialized();
        OwnedGrowth.autoChooseDrafts = autoChoose;
        if (autoChoose)
        {
            foreach (CharacterSkillDraft draft in OwnedGrowth.drafts
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
        if (!TryRecoverSkillAcquisition(out message)) return false;
        if (actor != null && actor.IsDead)
        {
            message = "사망한 캐릭터는 새 기술을 확정할 수 없습니다.";
            return false;
        }
        EnsureInitialized();
        CharacterSkillDraft draft = OwnedGrowth.drafts.FirstOrDefault(item => item != null
            && !item.IsInvalidated()
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
                && !OwnedGrowth.activeSkills.Any(skill => skill != null
                    && string.Equals(skill.id, chosen.id, StringComparison.Ordinal)))
            {
                OwnedGrowth.activeSkills.Add(chosen.Clone());
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

        if (OwnedGrowth.activeSkills.Count >= GetSlotProfile().NormalActiveSlots)
        {
            message = "일반 액티브 슬롯이 모두 찼습니다.";
            return false;
        }

        CharacterSkillInstance selectedSkill = draft.candidates[candidateIndex];
        if (!TryCommitFormulaEvidence(selectedSkill, draft.unlockLevel, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = candidateIndex;
                OwnedGrowth.activeSkills.Add(selectedSkill.Clone());
                OwnedGrowth.nextActiveDraftHasPity = draft.grantsUpperRarityPity;
            }, out message))
        {
            if (string.Equals(message, "evidence-binding-invalid", StringComparison.Ordinal))
            {
                InvalidateEvidenceDraft(draft, "STALE_EVIDENCE: selected skill evidence changed before confirmation.");
                EnsureUnlockedDrafts();
            }
            return false;
        }
        message = $"{draft.candidates[candidateIndex].displayName}을(를) 영구 확정했습니다."
            + (string.IsNullOrEmpty(message) ? string.Empty : " (" + message + ")");
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
            OwnedGrowth);
    }

    private bool traitReactionRollbackPending;
    private bool memoryErasureRollbackPending;

    string ICharacterTraitInferenceAuthority.RequireCharacterId() =>
        CharacterPersistentIdentity.Require(actor).Value;
    string ICharacterTraitInferenceAuthority.IdentityDisplayName =>
        actor.Identity?.DisplayName;
    string ICharacterTraitInferenceAuthority.GrowthDisplayName =>
        OwnedGrowth.displayName;
    IGameplayOutcomeEvidenceUseTransaction
        ICharacterTraitInferenceAuthority.EvidenceUseTransaction => evidenceUseTransaction;

    List<CharacterAcquiredTraitValidationIssue>
        ICharacterTraitInferenceAuthority.ValidateAcquiredTraitCandidate(
            CharacterAcquiredTraitAggregateState current,
            CharacterAcquiredTraitAggregateState candidate,
            int expectedRevision,
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions) =>
        ValidateAcquiredTraitCandidate(current, candidate, expectedRevision, settings, moduleDefinitions);

    void ICharacterTraitInferenceAuthority.ReplaceAcquiredTraitState(
        CharacterAcquiredTraitAggregateState state) => acquiredTraitState = state;

    void ICharacterTraitInferenceAuthority.PublishInferredTraitChanged() =>
        NotifyChangedWithoutAffectingCommittedState();

    private void RequireTraitStateAvailable()
    {
        if (traitInference?.IsRollbackPending == true)
            throw new InvalidOperationException("trait-inference-evidence-rollback-pending");
        if (traitReactionRollbackPending)
            throw new InvalidOperationException("trait-reaction-effect-rollback-pending");
        if (memoryErasureRollbackPending)
            throw new InvalidOperationException("memory-erasure-effect-rollback-pending");
    }

    internal void SetTraitReactionRecoveryBlocked(bool blocked) => traitReactionRollbackPending = blocked;
    internal void SetMemoryErasureRecoveryBlocked(bool blocked) => memoryErasureRollbackPending = blocked;

    // The reaction coordinator stages gameplay state without notifying independent consumers.
    // A failed observation can then roll back only this unpublished mutation.
    internal bool TryStageAcquiredTraitReaction(
        CharacterAcquiredTraitAggregateState candidate, int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings, IEnumerable<CharacterAcquiredTraitModuleSO> modules,
        int experienceAmount, out CharacterProgressionTransition transition, out string failure)
    {
        transition = default;
        var before = CaptureAcquiredTraitState();
        var issues = ValidateAcquiredTraitCandidate(before, candidate, expectedRevision, settings, modules);
        if (experienceAmount < 0 || experienceAmount > int.MaxValue - currentExperience)
            issues.Add(new(CharacterAcquiredTraitValidationIssueCode.Conflict, "trait-reaction-experience-overflow"));
        failure = string.Join(" | ", issues);
        if (issues.Count != 0) return false;
        var frozen = candidate.Clone();
        transition = CharacterProgressionRules.AddExperience(level, currentExperience, experienceAmount);
        acquiredTraitState = frozen;
        level = transition.Level;
        currentExperience = transition.CurrentExperience;
        return true;
    }

    internal bool TryRollbackAcquiredTraitReaction(CharacterAcquiredTraitAggregateState before,
        int expectedRevision, int previousLevel, int previousExperience,
        int expectedLevel, int expectedExperience)
    {
        if (acquiredTraitState?.revision != expectedRevision || level != expectedLevel
            || currentExperience != expectedExperience) return false;
        acquiredTraitState = before.Clone();
        level = previousLevel;
        currentExperience = previousExperience;
        return true;
    }

    internal void PublishAcquiredTraitReaction(CharacterProgressionTransition transition, bool experience)
    {
        if (experience) CompleteExperienceTransition(transition,
            CaptureLevelTransition(transition, CharacterLevelChangeCause.Experience));
        else NotifyChangedWithoutAffectingCommittedState();
    }

    public CharacterAcquiredTraitAggregateState CaptureAcquiredTraitState()
    {
        RequireTraitStateAvailable();
        acquiredTraitState ??= new CharacterAcquiredTraitAggregateState();
        acquiredTraitState.EnsureCollections();
        return acquiredTraitState.Clone();
    }

    [GameplayInternalOnly(
        "Acquired-trait services commit a fully validated candidate state.",
        "CharacterAcquiredTrait manifestation/erasure transaction services")]
    public bool TryCommitAcquiredTraitState(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
    {
        var current = CaptureAcquiredTraitState();
        var issues = ValidateAcquiredTraitCandidate(current, candidate, expectedRevision, settings, moduleDefinitions);
        validationIssues = issues;
        if (issues.Count != 0) return false;
        acquiredTraitState = candidate.Clone();
        NotifyChangedWithoutAffectingCommittedState();
        return true;
    }

    [GameplayInternalOnly("Accepts the actual inferred trait and publishes its frozen domain fact.",
        "CharacterAcquiredTraitInferenceService")]
    public bool TryCommitInferredTrait(
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        IEnumerable<string> evidenceFactIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        CharacterAcquiredTraitInferenceAuditRecord audit,
        IDomainFactPublisher publisher,
        CharacterTraitInferencePublicationRuntime publications,
        out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues)
        => TraitInference.TryCommitInferredTrait(candidate, expectedRevision, settings, moduleDefinitions, evidenceFactIds, evidenceBindings, audit, publisher, publications, out validationIssues);

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
            OwnedGrowth);
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
        if (eventContext == null)
        {
            string actorId = actor != null
                ? CharacterPersistentIdentity.Require(actor).Value
                : "character:unbound";
            long sequence = Math.Max(1L, NarrativeLedger.nextEventSequence);
            eventContext = new GameplayNarrativeEventContext
            {
                eventInstanceId = "narrative:" + actorId + ":" + sequence,
                actorId = actorId,
                actorDisplayName = actor?.Identity?.DisplayName
                    ?? (actor != null ? actor.name : null)
                    ?? actorId,
                sequence = sequence
            };
        }
        NarrativeLedger.Record(domain, factId, subjectId, outcome, value, day, evidenceMetadata, eventContext);
        EnsureUnlockedDrafts();
        if (triggerPassives)
        {
            TriggerPassivesForNarrativeDomain(
                domain,
                eventContext.eventInstanceId);
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
            && OwnedGrowth.useLimits.CanUse(domain, serial);
    }

    public bool TryMarkUltimateUsed(CharacterUltimateDomain domain, int serial)
    {
        if (!CanUseUltimate(domain, serial))
        {
            return false;
        }

        OwnedGrowth.useLimits.MarkUsed(domain, serial);
        Changed?.Invoke();
        return true;
    }

    internal CharacterSkillUseLimitState CaptureSkillUseLimits() =>
        OwnedGrowth.useLimits.Clone();

    internal void RestoreSkillUseLimits(CharacterSkillUseLimitState snapshot)
    {
        OwnedGrowth.useLimits = (snapshot
            ?? throw new ArgumentNullException(nameof(snapshot))).Clone();
    }

    internal void RecordManualSkillCooldown(string skillId, int useCount,
        long readyAbsoluteHour)
    {
        if (string.IsNullOrWhiteSpace(skillId) || useCount < 1 || readyAbsoluteHour < 0)
            throw new ArgumentException("Invalid manual skill cooldown command.");
        CharacterSkillUseLimitState limits = OwnedGrowth.useLimits;
        limits.manualSkillCooldowns ??= new List<CharacterManualSkillCooldownState>();
        var cooldown = limits.manualSkillCooldowns.SingleOrDefault(value =>
            value != null && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
        if (cooldown == null)
        {
            cooldown = new CharacterManualSkillCooldownState { skillId = skillId };
            limits.manualSkillCooldowns.Add(cooldown);
        }
        cooldown.useCount = useCount;
        cooldown.readyAbsoluteHour = readyAbsoluteHour;
    }

    internal void ApplyManualSkillBuff(string sourceId, CharacterSkillInstance skill,
        long absoluteHour, long expiresAbsoluteHour)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || skill == null
            || absoluteHour < 0 || expiresAbsoluteHour <= absoluteHour)
            throw new ArgumentException("Invalid manual skill buff command.");
        CharacterSkillUseLimitState limits = OwnedGrowth.useLimits;
        limits.manualSkillBuffs ??= new List<CharacterManualSkillBuffState>();
        limits.manualSkillBuffs.RemoveAll(value => value == null
            || value.expiresAbsoluteHour <= absoluteHour
            || value.sourceCharacterId == sourceId && value.skillId == skill.id);
        limits.manualSkillBuffs.Add(new CharacterManualSkillBuffState
        {
            sourceCharacterId = sourceId,
            skillId = skill.id,
            expiresAbsoluteHour = expiresAbsoluteHour,
            frozenSkill = skill.Clone()
        });
    }

    internal void RecordAutomaticSkillUse(CharacterAutomaticSkillActivationState activation,
        CharacterUltimateDomain domain, int serial, bool markUltimateUsed)
    {
        if (activation == null || string.IsNullOrWhiteSpace(activation.eventKeySha256))
            throw new ArgumentException("Invalid automatic skill use command.");
        CharacterSkillUseLimitState limits = OwnedGrowth.useLimits;
        limits.automaticSkillActivations ??= new List<CharacterAutomaticSkillActivationState>();
        if (limits.automaticSkillActivations.Any(value =>
                value?.eventKeySha256 == activation.eventKeySha256))
            throw new InvalidOperationException("Automatic skill use is already committed.");
        limits.automaticSkillActivations.Add(activation.Clone());
        if (markUltimateUsed) limits.MarkUsed(domain, serial);
    }

    internal void RecordSkillActivationCommit(long revision,
        CharacterSkillActivationCommitState committed)
    {
        CharacterSkillUseLimitState limits = OwnedGrowth.useLimits;
        if (committed == null || committed.ownerRevision != revision
            || revision != checked(limits.activationSequence + 1L))
            throw new InvalidOperationException("Skill activation revision changed before commit.");
        limits.activationSequence = revision;
        limits.lastSkillActivation = committed.Clone();
    }

    internal void FinalizeSkillActivationObservation(long revision, string fingerprint)
    {
        CharacterSkillUseLimitState limits = OwnedGrowth.useLimits;
        if (limits.activationSequence != revision || limits.lastSkillActivation == null
            || limits.lastSkillActivation.ownerRevision != revision
            || string.IsNullOrWhiteSpace(fingerprint))
            throw new InvalidOperationException("Skill activation observation has no matching owner.");
        limits.lastSkillActivation.canonicalPayloadHash = fingerprint;
        limits.lastSkillActivation.observationPending = false;
    }

    internal void EnsurePreparedStartingDraft(CharacterSkillKind kind, int unlockLevel)
    {
        CharacterSkillDraft draft = OwnedGrowth.drafts.FirstOrDefault(item => item != null
            && !item.IsInvalidated() && item.kind == kind && item.unlockLevel == unlockLevel);
        if (draft == null)
        {
            draft = generationService.CreateDraft(this, kind, unlockLevel,
                OwnedGrowth.skillGenerationRevision);
            OwnedGrowth.drafts.Add(draft);
        }
        if (!draft.permanentlyChosen && !draft.isReady)
            generationService.RequestDraft(this, draft);
    }

    public void MarkGenerationRequestPending(string requestKey)
    {
        if (!string.IsNullOrWhiteSpace(requestKey)
            && !OwnedGrowth.pendingRequestKeys.Contains(requestKey, StringComparer.Ordinal))
        {
            OwnedGrowth.pendingRequestKeys.Add(requestKey);
        }
    }

    public void MarkGenerationRequestCompleted(string requestKey)
    {
        OwnedGrowth.pendingRequestKeys.RemoveAll(key => string.Equals(key, requestKey, StringComparison.Ordinal));
    }

    public void OnDraftReady(CharacterSkillDraft draft)
    {
        if (!TryRecoverSkillAcquisition(out string recoveryFailure))
            throw new InvalidOperationException(recoveryFailure);
        if (draft == null || !draft.isReady)
        {
            return;
        }

        DraftReady?.Invoke(draft.Clone());
        if (draft.kind == CharacterSkillKind.Active)
        {
            if (OwnedGrowth.autoChooseDrafts)
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
            OwnedGrowth.Clone(),
            NarrativeLedger.Clone(),
            CaptureAcquiredTraitState());
    }

    public void RestorePersistentState(CharacterProgressionSnapshot snapshot)
    {
        RequireTraitStateAvailable();
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
        OwnedGrowth.EnsureCollections();
        RebuildLegacySkillViews();
        EnsureInitialized();
        WarmEffectiveRuntimeProfile();
        if (actor == null || !actor.IsDetachedRestoreCandidate)
        {
            EnsureUnlockedDrafts();
        }
        Changed?.Invoke();
    }

    internal void ResumePendingGenerationAfterWorldRestore()
    {
        if (actor != null
            && (actor.IsDetachedRestoreCandidate || actor.IsUnpublishedComposition))
        {
            throw new InvalidOperationException(
                "Character skill generation cannot resume before world publication completes.");
        }

        // A world restore reinstalls the saved progression, not a new skill
        // unlock event. In particular, incident visitors can legitimately have
        // no narrative evidence and no drafts yet. Resume only saved requests;
        // real identity, narrative and level transitions still create drafts.
        if (actor != null && actor.IsDead)
        {
            generationService?.CancelRequests(this);
            return;
        }
        // Restoring a world is not a new narrative retry. Keep exhausted
        // presentation requests and their failure diagnostics exactly as saved.
        ResumePendingRequests(retryDeferredPresentations: false);
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
        int maximumTraitCount = 4,
        bool ensureInitialDrafts = true)
    {
        if (maximumTraitCount is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTraitCount));
        }

        generationService?.CancelRequests(this);
        OwnedGrowth.skillGenerationRevision++;
        OwnedGrowth.initialized = true;
        OwnedGrowth.displayName = displayName?.Trim() ?? string.Empty;
        OwnedGrowth.origin = origin?.Trim() ?? string.Empty;
        OwnedGrowth.traitSelectionAuthorityVersion =
            CharacterGrowthState.CurrentTraitSelectionAuthorityVersion;
        OwnedGrowth.traitSelectionAuthorityOrigin =
            CharacterTraitSelectionAuthorityOrigin.PreparedSelection;
        OwnedGrowth.traitIds = traitIds?
            .Distinct()
            .Take(maximumTraitCount)
            .ToList() ?? new List<int>();
        OwnedGrowth.startingProfile = startingProfile?.Clone()
            ?? new CharacterStartingProfileState();
        OwnedGrowth.startingProficiencies = (preparedStartingProficiencies
                ?? CharacterStartingProficiencyRules.Create(
                    startingProficiencySeed ?? generationSeed))
            .Select(value => value.Clone())
            .ToList();
        CharacterStartingProficiencyRules.Validate(
            OwnedGrowth.startingProficiencies);
        OwnedGrowth.potentialGrade = potential;
        OwnedGrowth.generationSeed = generationSeed;
        OwnedGrowth.autoChooseDrafts = autoChooseDrafts;
        OwnedGrowth.activeSkills.Clear();
        OwnedGrowth.passiveSkills.Clear();
        OwnedGrowth.ultimate = null;
        OwnedGrowth.drafts.Clear();
        OwnedGrowth.pendingRequestKeys.Clear();
        OwnedGrowth.nextActiveDraftHasPity = false;
        acquiredTraitState = new CharacterAcquiredTraitAggregateState();
        InvalidateEffectiveRuntimeProfile();
        WarmEffectiveRuntimeProfile();
        ReplacePreparedIdentityNarrativeEvidence(OwnedGrowth.startingProfile);
        if (ensureInitialDrafts)
        {
            EnsureUnlockedDrafts();
        }
        // Prepared previews own real identity/profile/progression state so public
        // narrative generation can remain authoritative, but they are not live
        // characters and do not have the injected vitals projection/runtime.
        if (actor != null && !actor.IsUnpublishedComposition)
        {
            actor.Stats?.RecalculateVitals(resetCurrentHealth: true);
        }
        Changed?.Invoke();
    }

    private void ReplacePreparedIdentityNarrativeEvidence(
        CharacterStartingProfileState profile)
    {
        if (profile?.prepared != true)
        {
            NarrativeLedger.facts ??= new List<CharacterNarrativeFact>();
            NarrativeLedger.facts.RemoveAll(IsPreparedIdentityNarrativeFact);
            return;
        }

        NarrativeLedger.facts ??= new List<CharacterNarrativeFact>();
        NarrativeLedger.facts.RemoveAll(IsPreparedIdentityNarrativeFact);
        AppendPreparedIdentityNarrativeEvidence(NarrativeLedger, profile);
    }

    internal static CharacterNarrativeLedger
        CreatePreparedIdentityNarrativeLedger(
            CharacterStartingProfileState profile)
    {
        var ledger = new CharacterNarrativeLedger();
        AppendPreparedIdentityNarrativeEvidence(ledger, profile);
        return ledger;
    }

    private static void AppendPreparedIdentityNarrativeEvidence(
        CharacterNarrativeLedger ledger,
        CharacterStartingProfileState profile)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        if (profile?.prepared != true)
            throw new InvalidOperationException(
                "Prepared identity narrative evidence requires a prepared starting profile.");
        string originId = RequirePreparedProfileValue(
            profile.originId,
            nameof(profile.originId));
        string originDisplayName = RequirePreparedProfileValue(
            profile.originDisplayName,
            nameof(profile.originDisplayName));
        string historyId = RequirePreparedProfileValue(
            profile.historyId,
            nameof(profile.historyId));
        string historyDisplayName = RequirePreparedProfileValue(
            profile.historyDisplayName,
            nameof(profile.historyDisplayName));
        string primaryProficiencyId = RequirePreparedProfileValue(
            profile.primaryProficiencyId,
            nameof(profile.primaryProficiencyId));
        CharacterNarrativeDomain historyDomain =
            ResolvePreparedHistoryDomain(primaryProficiencyId);

        RecordPreparedIdentityNarrative(
            ledger,
            CharacterNarrativeDomain.Survival,
            PreparedOriginFactPrefix + originId,
            originId,
            originDisplayName,
            "identity:origin:" + originId);
        RecordPreparedIdentityNarrative(
            ledger,
            historyDomain,
            PreparedHistoryFactPrefix + historyId,
            historyId,
            historyDisplayName,
            "identity:history:" + historyId);
    }

    private static void RecordPreparedIdentityNarrative(
        CharacterNarrativeLedger ledger,
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string displayName,
        string actionKey)
    {
        ledger.Record(
            domain,
            factId,
            subjectId,
            displayName,
            value: 0f,
            day: 0,
            metadata: new CharacterNarrativeEvidenceMetadata(
                PreparedIdentityEventGroup,
                actionKey,
                string.Empty,
                CharacterNarrativeEvidenceMetadata.OrdinaryImportance));
    }

    private static bool IsPreparedIdentityNarrativeFact(
        CharacterNarrativeFact fact) =>
        fact != null
        && (fact.factId?.StartsWith(
                PreparedOriginFactPrefix,
                StringComparison.Ordinal) == true
            || fact.factId?.StartsWith(
                PreparedHistoryFactPrefix,
                StringComparison.Ordinal) == true);

    private static string RequirePreparedProfileValue(
        string value,
        string fieldName)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0)
        {
            throw new InvalidOperationException(
                $"Prepared character identity requires {fieldName} before skill generation.");
        }

        return canonical;
    }

    private static CharacterNarrativeDomain ResolvePreparedHistoryDomain(
        string primaryProficiencyId)
    {
        CharacterProficiencyId proficiencyId = new(primaryProficiencyId);
        if (proficiencyId == BuiltInCharacterProficiencyIds.MeleeCombat
            || proficiencyId == BuiltInCharacterProficiencyIds.RangedCombat)
        {
            return CharacterNarrativeDomain.Combat;
        }

        if (proficiencyId == BuiltInCharacterProficiencyIds.Medicine)
        {
            return CharacterNarrativeDomain.Injury;
        }

        if (proficiencyId == BuiltInCharacterProficiencyIds.Social)
        {
            return CharacterNarrativeDomain.Relationship;
        }

        if (proficiencyId == BuiltInCharacterProficiencyIds.Fieldwork)
        {
            return CharacterNarrativeDomain.Expedition;
        }

        if (proficiencyId == BuiltInCharacterProficiencyIds.ConstructionEngineering
            || proficiencyId == BuiltInCharacterProficiencyIds.Crafting
            || proficiencyId == BuiltInCharacterProficiencyIds.FoodProduction
            || proficiencyId == BuiltInCharacterProficiencyIds.Scholarship)
        {
            return CharacterNarrativeDomain.Work;
        }

        throw new InvalidOperationException(
            "Prepared character identity has no narrative-domain mapping for primary proficiency '"
            + primaryProficiencyId + "'.");
    }

    private void CompleteConfigurationIfReady()
    {
        if (profileProjector == null)
        {
            return;
        }

        EnsureInitialized();
        WarmEffectiveRuntimeProfile();
        // Composition binds authored data before the prepared/save snapshot
        // installs its narrative evidence. Do not create drafts from that
        // transient empty ledger; explicit restore/identity/narrative paths
        // resume generation after their authoritative state is installed.
        if (actor != null
            && (actor.IsUnpublishedComposition || actor.IsDetachedRestoreCandidate))
        {
            return;
        }
        EnsureUnlockedDrafts();
    }

    private void EnsureInitialized()
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        OwnedGrowth.EnsureCollections();
        acquiredTraitState ??= new CharacterAcquiredTraitAggregateState();
        acquiredTraitState.EnsureCollections();
        if (actor?.Identity?.Data == null)
        {
            return;
        }

        RequireProfileProjector().EnsureInitialized(
            actor,
            OwnedGrowth,
            SkillSettings);
    }

    private void EnsureUnlockedDrafts()
    {
        if (actor != null && actor.IsDead)
        {
            generationService?.CancelRequests(this);
            return;
        }
        if (generationService == null || !OwnedGrowth.initialized)
        {
            return;
        }

        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        foreach (int unlockLevel in settings.activeUnlockLevels.Where(unlock => unlock <= Level))
        {
            EnsureDraft(CharacterSkillKind.Active, unlockLevel);
        }

        if (OwnedGrowth.passiveSkills.Count == 0
            && !HasDraft(CharacterSkillKind.Passive, 1))
        {
            EnsureDraft(CharacterSkillKind.Passive, 1);
        }

        if (Level >= settings.secondPassiveMinimumLevel
            && OwnedGrowth.passiveSkills.Count < GetSlotProfile().PassiveSlots
            && NarrativeLedger.MeaningfulRecordCount >= settings.secondPassiveMinimumRecords
            && NarrativeLedger.MeaningfulDomainCount >= settings.secondPassiveMinimumDomains)
        {
            EnsureDraft(CharacterSkillKind.Passive, settings.secondPassiveMinimumLevel);
        }

        if (Level >= MaxLevel && OwnedGrowth.ultimate == null)
        {
            EnsureDraft(CharacterSkillKind.Ultimate, MaxLevel);
        }

        ResumePendingRequests(retryDeferredPresentations: true);
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
            OwnedGrowth.skillGenerationRevision);
        OwnedGrowth.drafts.Add(draft);
        generationService.RequestDraft(this, draft);
    }

    private bool HasDraft(CharacterSkillKind kind, int unlockLevel)
    {
        return OwnedGrowth.drafts.Any(item => item != null
            && !item.IsInvalidated()
            && item.kind == kind
            && item.unlockLevel == unlockLevel);
    }

    internal void InvalidateEvidenceDraft(CharacterSkillDraft draft, string reason)
    {
        if (draft == null || draft.permanentlyChosen || draft.IsInvalidated())
            return;
        draft.presentationState = CharacterSkillPresentationState.InvalidatedEvidence;
        draft.presentationCorrection = reason;
        draft.isReady = false;
        draft.requestSubmitted = false;
        MarkGenerationRequestCompleted(draft.requestKey);
    }

    internal void ResumeInvalidatedDrafts()
    {
        if (actor != null && !actor.IsUnpublishedComposition && !actor.IsDetachedRestoreCandidate)
            EnsureUnlockedDrafts();
    }

    private void ResumePendingRequests(bool retryDeferredPresentations)
    {
        if (generationService == null)
        {
            return;
        }

        foreach (CharacterSkillDraft draft in OwnedGrowth.drafts.Where(item => item != null
            && !item.IsInvalidated()
            && !item.isReady
            && !item.permanentlyChosen
            && (retryDeferredPresentations
                || item.presentationState != CharacterSkillPresentationState.AwaitingNarrativeRetry)))
        {
            generationService.RequestDraft(this, draft);
        }
    }

    private void AllocateStatsForReachedLevel(int reachedLevel)
    {
        // V26: generic character levels unlock narrative skills only.
        // Proficiency grows exclusively from approved work, combat, and mentoring.
    }

    private void TriggerPassivesForNarrativeDomain(
        CharacterNarrativeDomain domain,
        string eventId)
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
        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(
            new CharacterSkillExecutionContext(actor, trigger, eventId));
    }

    private void CommitAutomaticPassive(CharacterSkillDraft draft)
    {
        if (draft.permanentlyChosen
            || draft.candidates == null
            || draft.candidates.Count == 0
            || OwnedGrowth.passiveSkills.Count >= GetSlotProfile().PassiveSlots)
        {
            return;
        }

        CharacterSkillInstance skill = draft.candidates[0].Clone();
        if (!TryCommitFormulaEvidence(skill, draft.unlockLevel, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = 0;
                OwnedGrowth.passiveSkills.Add(skill);
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
        if (!TryCommitFormulaEvidence(skill, draft.unlockLevel, () =>
            {
                draft.permanentlyChosen = true;
                draft.chosenIndex = 0;
                OwnedGrowth.ultimate = skill;
            }, out string evidenceError))
        {
            throw new InvalidOperationException(evidenceError);
        }
        if (!suppressPublicSkillNotifications)
        {
            NotifySkillUnlockedWithoutAffectingCommittedState(
                OwnedGrowth.ultimate,
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
        int unlockLevel,
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
        if (preparationPreview)
        {
            // A reroll candidate owns only its prepared selection. It has no
            // gameplay outcome identity, journal or gameplay acquisition revision.
            // Population preparation uses the world transaction below instead.
            if (actor?.IsUnpublishedComposition != true
                || OwnedGrowth.startingProfile?.prepared != true || exactIds.Count != 0)
            { error = "skill-acquisition-preview-contract-invalid"; return false; }
            CharacterGrowthState beforePreview = OwnedGrowth.Clone();
            int[] beforePreviewUses = selected.Select(value => value.influenceUseCount).ToArray();
            try
            {
                foreach (CharacterNarrativeFact fact in selected) fact.influenceUseCount++;
                ownerCommit();
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            {
                growthState = beforePreview;
                for (int i = 0; i < selected.Count; i++) selected[i].influenceUseCount = beforePreviewUses[i];
                RebuildLegacySkillViews();
                error = "skill-acquisition-preview-command:" + exception.Message;
                return false;
            }
        }
        long beforeAcquisitionRevision = OwnedGrowth.skillAcquisitionRevision;
        if (beforeAcquisitionRevision == long.MaxValue)
        {
            error = "Character skill acquisition revision is exhausted.";
            return false;
        }
        long afterAcquisitionRevision = beforeAcquisitionRevision + 1L;
        if (this.facts == null || skillAcquisitions == null)
        { error = "skill-acquisition-observation-not-configured"; return false; }
        string characterId;
        CharacterSkillAcquiredFact acquired;
        try
        {
            characterId = CharacterPersistentIdentity.Require(actor).Value;
            acquired = new CharacterSkillAcquiredFact(new CharacterSkillAcquiredFactData
            {
                characterId = characterId, characterDisplayName = actor.Identity?.DisplayName ?? string.Empty,
                beforeRevision = beforeAcquisitionRevision, afterRevision = afterAcquisitionRevision,
                unlockLevel = unlockLevel, skill = skill, absoluteDay = CurrentAbsoluteDay()
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { error = "skill-acquisition-fact:" + exception.Message; return false; }
        CharacterGrowthState beforeGrowth = OwnedGrowth.Clone();
        int[] beforeUses = selected.Select(value => value.influenceUseCount).ToArray();
        if (!skillAcquisitions.TryBegin(characterId, out error)) return false;
        DomainFactLease lease = default;
        PreparedGameplayOutcomeEvidenceUse prepared = null;
        bool ownerApplied = false;
        bool retained = false;
        bool Rollback()
        {
            // Roll back input retention before making the owner rollback visible.
            try { prepared?.Cancel(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException) { return false; }
            acquisitionRollbackPending = false;
            if (ownerApplied)
            {
                growthState = beforeGrowth;
                for (int i = 0; i < selected.Count; i++) selected[i].influenceUseCount = beforeUses[i];
                RebuildLegacySkillViews();
            }
            return true;
        }
        try
        {
            if (!this.facts.TryReserve(acquired, out lease, out error)) return false;
            if (exactIds.Count > 0)
            {
                if (evidenceUseTransaction == null
                    || !evidenceUseTransaction.TryPrepareBindings("character-skill", skill.presentationId,
                        skill.evidenceBindings, out prepared, out error)) return false;
                if (!prepared.TryCommitAnchors(out error)) return false;
            }
            ownerApplied = true;
            foreach (CharacterNarrativeFact fact in selected)
                fact.influenceUseCount++;
            ownerCommit();
            OwnedGrowth.skillAcquisitionRevision = afterAcquisitionRevision;
            // Finish the fallible evidence guard before publication; its sealed
            // tokens still support rollback on a known pre-commit rejection.
            if (prepared != null && !prepared.TrySealOwnerCommitForJointOutcome(out error)) return false;
            DomainFactDelivery delivered = skillAcquisitions.PublishApplied(acquired, lease);
            retained = delivered.MayBeCommitted;
            if (!retained) { error = delivered.Detail; return false; }
            prepared?.CompleteJointOutcomeCommit();
            if (!delivered.Completed) error = delivered.Detail;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            error = "skill-acquisition-command:" + exception.Message;
            return false;
        }
        finally
        {
            try
            {
                if (!retained && !Rollback())
                {
                    acquisitionRollbackPending = true;
                    skillAcquisitions.RetainRollback(characterId, Rollback);
                    error = "skill-acquisition-evidence-rollback-pending:" + error;
                }
            }
            finally
            {
                try { if (lease.IsValid) this.facts.Cancel(lease); }
                finally { skillAcquisitions.End(characterId); }
            }
        }
    }

    public bool TryRecoverSkillAcquisition(out string error)
    {
        error = string.Empty;
        return skillAcquisitions == null || skillAcquisitions.RecoverPending(out error);
    }

    private int CurrentAbsoluteDay() =>
        sessionStateProvider != null
        && sessionStateProvider.TryGetSessionState(out GameSessionState state)
        && state?.day != null
            ? Mathf.Max(0, state.day.Value)
            : 0;

    private void ValidateRestoredFormulaState(
        CharacterGrowthState restoredGrowth,
        CharacterNarrativeLedger restoredLedger)
    {
        if (restoredGrowth == null) return;
        foreach (CharacterSkillDraft draft in (restoredGrowth.drafts ?? new List<CharacterSkillDraft>())
            .Where(value => value != null))
            draft.RestoreRequestPublicContext();
        CharacterSkillInstance[] committed =
            (restoredGrowth.activeSkills ?? new List<CharacterSkillInstance>())
            .Concat(restoredGrowth.passiveSkills
                ?? new List<CharacterSkillInstance>())
            .Concat(restoredGrowth.ultimate == null
                ? Array.Empty<CharacterSkillInstance>()
                : new[] { restoredGrowth.ultimate })
            .ToArray();
        (restoredGrowth.useLimits
            ?? throw new InvalidOperationException(
                "Character progression restore rejected missing skill use limits."))
            .ValidatePersistentState(
                CharacterPersistentIdentity.Require(actor).Value,
                committed.Concat(OwnerFixedSkills));
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
            HashSet<string> exactEvidence = (skill.evidenceBindings
                    ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                .Where(value => value != null)
                .Select(value => value.publicFactId)
                .ToHashSet(StringComparer.Ordinal);
            if ((skill.evidenceIds ?? new List<string>()).Any(id =>
                    !availableEvidence.Contains(id)
                    && !exactEvidence.Contains(id)))
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
        foreach (CharacterSkillInstance skill in OwnedGrowth.activeSkills
            .Concat(OwnedGrowth.passiveSkills)
            .Append(OwnedGrowth.ultimate)
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
        RequireProfileProjector().Warm(actor, OwnedGrowth);
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
