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

    private readonly List<string> learnedSkillIds = new List<string>();
    private readonly List<string> equippedSkillIds = new List<string>();
    private CharacterActor actor;
    private ICharacterSkillGenerationService generationService;
    private ICharacterSkillSystemSettingsProvider settingsProvider;
    private CharacterProgressionNotificationApplicationAdapter notifications;
    private CharacterProgressionProfileProjector profileProjector;
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
        Changed?.Invoke();
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
        Changed?.Invoke();
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

        Changed?.Invoke();
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
                Changed?.Invoke();
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

        draft.permanentlyChosen = true;
        draft.chosenIndex = candidateIndex;
        GrowthState.activeSkills.Add(draft.candidates[candidateIndex].Clone());
        GrowthState.nextActiveDraftHasPity = draft.grantsUpperRarityPity;
        message = $"{draft.candidates[candidateIndex].displayName}을(를) 영구 확정했습니다.";
        RebuildLegacySkillViews();
        Changed?.Invoke();
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

    public int GetFinalStat(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetFinalStat(
            actor,
            GrowthState,
            statType);
    }

    public CharacterStatBreakdown GetStatBreakdown(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetStatBreakdown(
            actor,
            GrowthState,
            statType);
    }

    public int GetBaseStat(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetBaseStat(
            actor,
            GrowthState,
            statType);
    }

    public int GetSpeciesTraitStatBonus(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetSpeciesTraitStatBonus(
            actor,
            GrowthState,
            statType);
    }

    public int GetLevelGrowthStat(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetLevelGrowthStat(
            GrowthState,
            statType);
    }

    public int GetCurrentConditionalPassiveStatBonus(CharacterStatType statType)
    {
        EnsureInitialized();
        return RequireProfileProjector().GetConditionalPassiveStatBonus(
            actor,
            GrowthState,
            statType);
    }

    public int GetFinalStat(string statId)
    {
        return CharacterStatCatalog.TryGet(
                statId,
                out CharacterStatDefinition definition)
            && definition.LegacyType.HasValue
                ? GetFinalStat(definition.LegacyType.Value)
                : 0;
    }

    public IReadOnlyList<CharacterTraitSO> ResolveSelectedTraits()
    {
        EnsureInitialized();
        return RequireProfileProjector().ResolveSelectedTraits(
            actor,
            GrowthState);
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
        bool triggerPassives = true)
    {
        NarrativeLedger.Record(domain, factId, subjectId, outcome, value, day);
        EnsureUnlockedDrafts();
        if (triggerPassives)
        {
            TriggerPassivesForNarrativeDomain(domain);
        }

        Changed?.Invoke();
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

        Changed?.Invoke();
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
            NarrativeLedger.Clone());
    }

    public void RestorePersistentState(CharacterProgressionSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
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
        InvalidateEffectiveRuntimeProfile();
        GrowthState.EnsureCollections();
        EnsureMedicalStat();
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
        CharacterStatBlock initialStats,
        CharacterPotentialGrade potential,
        int generationSeed,
        bool autoChooseDrafts)
    {
        generationService?.CancelRequests(this);
        GrowthState.skillGenerationRevision++;
        GrowthState.initialized = true;
        GrowthState.displayName = displayName?.Trim() ?? string.Empty;
        GrowthState.origin = origin?.Trim() ?? string.Empty;
        GrowthState.traitIds = traitIds?.Distinct().Take(3).ToList() ?? new List<int>();
        GrowthState.initialBaseStats = CharacterSkillModelUtility.CopyStats(initialStats);
        EnsureMedicalStat();
        GrowthState.levelGrowthStats = new CharacterStatBlock();
        GrowthState.potentialGrade = potential;
        GrowthState.generationSeed = generationSeed;
        GrowthState.autoChooseDrafts = autoChooseDrafts;
        GrowthState.allocatedGrowthPoints = 0;
        GrowthState.activeSkills.Clear();
        GrowthState.passiveSkills.Clear();
        GrowthState.ultimate = null;
        GrowthState.drafts.Clear();
        GrowthState.pendingRequestKeys.Clear();
        GrowthState.nextActiveDraftHasPity = false;
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
        if (actor?.Identity?.Data == null)
        {
            return;
        }

        RequireProfileProjector().EnsureInitialized(
            actor,
            GrowthState,
            SkillSettings);
    }

    private void EnsureMedicalStat()
    {
        RequireProfileProjector().EnsureMedicalStat(GrowthState);
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
        CharacterSkillSystemSettingsSO settings = SkillSettings;
        CharacterProgressionGrowthApplicationAdapter.AllocateGrowthPoints(
            GrowthState,
            NarrativeLedger,
            reachedLevel,
            settings);
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

        draft.permanentlyChosen = true;
        draft.chosenIndex = 0;
        CharacterSkillInstance skill = draft.candidates[0].Clone();
        GrowthState.passiveSkills.Add(skill);
        if (!suppressPublicSkillNotifications)
        {
            notifications?.NotifySkillUnlocked(skill, isUltimate: false);
        }
    }

    private void CommitAutomaticUltimate(CharacterSkillDraft draft)
    {
        if (draft.permanentlyChosen || draft.candidates == null || draft.candidates.Count == 0)
        {
            return;
        }

        draft.permanentlyChosen = true;
        draft.chosenIndex = 0;
        GrowthState.ultimate = draft.candidates[0].Clone();
        if (!suppressPublicSkillNotifications)
        {
            notifications?.NotifySkillUnlocked(
                GrowthState.ultimate,
                isUltimate: true);
        }
    }

    private int ChooseBestCandidateIndex(CharacterSkillDraft draft)
    {
        int bestIndex = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < draft.candidates.Count; i++)
        {
            CharacterSkillInstance candidate = draft.candidates[i];
            float score = (int)candidate.rarity * 100f;
            foreach (CharacterSkillModuleSelection module in candidate.modules ?? new List<CharacterSkillModuleSelection>())
            {
                score += module.moduleId switch
                {
                    "damage" when GetFinalStat(CharacterStatType.Attack) >= 7 => 20f,
                    "heal" when GetFinalStat(CharacterStatType.Research) >= 7 => 16f,
                    "guard" when GetFinalStat(CharacterStatType.Toughness) >= 7 => 16f,
                    "delay" when GetFinalStat(CharacterStatType.Dexterity) >= 7 => 14f,
                    _ => 1f
                };
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
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

public readonly struct CharacterStatBreakdown
{
    public CharacterStatBreakdown(
        CharacterStatType statType,
        int baseValue,
        int speciesTraitValue,
        int levelGrowthValue,
        int conditionalPassiveValue,
        int finalValue)
    {
        StatType = statType;
        BaseValue = baseValue;
        SpeciesTraitValue = speciesTraitValue;
        LevelGrowthValue = levelGrowthValue;
        ConditionalPassiveValue = conditionalPassiveValue;
        FinalValue = finalValue;
    }

    public CharacterStatType StatType { get; }
    public int BaseValue { get; }
    public int SpeciesTraitValue { get; }
    public int LevelGrowthValue { get; }
    public int ConditionalPassiveValue { get; }
    public int FinalValue { get; }
}

public sealed class CharacterProgressionSnapshot
{
    public CharacterProgressionSnapshot(
        int level,
        int currentExperience,
        CharacterGrowthState growthState,
        CharacterNarrativeLedger narrativeLedger)
    {
        Level = Mathf.Clamp(level, 1, CharacterProgression.MaxLevel);
        CurrentExperience = Mathf.Max(0, currentExperience);
        GrowthState = growthState?.Clone() ?? new CharacterGrowthState();
        NarrativeLedger = narrativeLedger?.Clone() ?? new CharacterNarrativeLedger();
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
    public IReadOnlyList<string> LearnedSkillIds => GrowthState.activeSkills?
        .Where(item => item != null).Select(item => item.id).ToArray()
        ?? Array.Empty<string>();
    public IReadOnlyList<string> EquippedSkillIds => LearnedSkillIds;
}
