using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public sealed class CharacterProgression : MonoBehaviour
{
    public const int MaxLevel = 50;
    public const int NormalActiveSlots = 3;
    public const int PassiveSlots = 2;
    public const int MaxEquippedSkills = NormalActiveSlots;
    private const string TraitResourcePath = "SO/Character/Traits";

    private static readonly Dictionary<int, CharacterTraitSO> TraitCache =
        new Dictionary<int, CharacterTraitSO>();
    private static readonly ProfilerMarker EffectiveProfileBuildMarker =
        new ProfilerMarker("Character.Progression.BuildEffectiveProfile");
    private static IResourcesAssetLoader traitResourcesAssetLoader;

    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(0)] private int currentExperience;
    [SerializeField] private CharacterGrowthState growthState = new CharacterGrowthState();
    [SerializeField] private CharacterNarrativeLedger narrativeLedger = new CharacterNarrativeLedger();

    private readonly List<string> learnedSkillIds = new List<string>();
    private readonly List<string> equippedSkillIds = new List<string>();
    private CharacterActor actor;
    private int initializedDataInstanceId;
    private ICharacterSkillGenerationService generationService;
    private ICharacterSkillSystemSettingsProvider settingsProvider;
    private IGameEventBus gameEventBus;
    private CharacterRuntimeProfile effectiveRuntimeProfile;
    private int effectiveRuntimeProfileKey;
    private readonly List<CharacterTraitSO> resolvedSelectedTraits =
        new List<CharacterTraitSO>();
    private IReadOnlyList<CharacterTraitSO> resolvedSelectedTraitsView;
    private int resolvedSelectedTraitsKey = int.MinValue;
    private bool suppressPublicSkillNotifications;

    public CharacterActor Actor => actor;
    public int Level => Mathf.Clamp(level, 1, MaxLevel);
    public int CurrentExperience => Level >= MaxLevel ? 0 : Mathf.Max(0, currentExperience);
    public int ExperienceToNextLevel => Level >= MaxLevel ? 0 : GetExperienceRequired(Level);
    public float ExperienceRatio => Level >= MaxLevel
        ? 1f
        : Mathf.Clamp01(CurrentExperience / (float)Mathf.Max(1, ExperienceToNextLevel));
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
        IGameEventBus gameEventBus = null,
        IResourcesAssetLoader resourcesAssetLoader = null)
    {
        this.generationService = generationService
            ?? throw new ArgumentNullException(nameof(generationService));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.gameEventBus = gameEventBus ?? this.gameEventBus;
        traitResourcesAssetLoader = resourcesAssetLoader ?? traitResourcesAssetLoader;
        EnsureInitialized();
        EnsureUnlockedDrafts();
    }

    public static int GetExperienceRequired(int currentLevel)
    {
        int normalizedLevel = Mathf.Clamp(currentLevel, 1, MaxLevel - 1);
        return 20 + Mathf.FloorToInt((normalizedLevel - 1) / 10f) * 5;
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        EnsureInitialized();
        WarmEffectiveRuntimeProfile();
        EnsureUnlockedDrafts();
    }

    public int AddExperience(int amount)
    {
        EnsureInitialized();
        if (amount <= 0 || Level >= MaxLevel)
        {
            return 0;
        }

        int previousLevel = Level;
        currentExperience += amount;
        while (level < MaxLevel && currentExperience >= GetExperienceRequired(level))
        {
            currentExperience -= GetExperienceRequired(level);
            level++;
            AllocateStatsForReachedLevel(level);
        }

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExperience = 0;
        }

        if (level != previousLevel)
        {
            actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
            actor?.AddLog($"레벨 {level}에 도달했다.");
        }

        EnsureUnlockedDrafts();
        Changed?.Invoke();
        return level - previousLevel;
    }

    public bool EnsureMinimumLevel(int targetLevel, string reason = "")
    {
        EnsureInitialized();
        int clampedTarget = Mathf.Clamp(targetLevel, 1, MaxLevel);
        if (Level >= clampedTarget)
        {
            return false;
        }

        int previousLevel = Level;
        while (level < clampedTarget)
        {
            level++;
            AllocateStatsForReachedLevel(level);
        }

        if (level >= MaxLevel)
        {
            level = MaxLevel;
        }

        currentExperience = 0;
        actor?.Stats?.RecalculateVitals(resetCurrentHealth: false);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            actor?.AddLog(reason);
        }

        EnsureUnlockedDrafts();
        Changed?.Invoke();
        return Level > previousLevel;
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
        if (!GrowthState.initialized)
        {
            return actor?.Identity?.Profile?.GetStat(statType) ?? 5;
        }

        int value = GrowthState.initialBaseStats.Get(statType);
        CharacterSO data = actor?.Identity?.Data;
        value += data?.species?.statBonus?.Get(statType) ?? 0;
        foreach (CharacterTraitSO trait in ResolveSelectedTraits())
        {
            value += trait?.statBonus?.Get(statType) ?? 0;
        }

        value += GrowthState.levelGrowthStats.Get(statType);
        value += GetConditionalPassiveStatBonus(statType);
        return Mathf.Max(0, value);
    }

    public CharacterStatBreakdown GetStatBreakdown(CharacterStatType statType)
    {
        EnsureInitialized();
        if (!GrowthState.initialized)
        {
            int fallback = actor?.Identity?.Profile?.GetStat(statType) ?? 5;
            return new CharacterStatBreakdown(statType, fallback, 0, 0, 0, fallback);
        }

        int baseValue = GrowthState.initialBaseStats.Get(statType);
        int speciesTrait = GetSpeciesTraitStatBonus(statType);
        int levelGrowth = GrowthState.levelGrowthStats.Get(statType);
        int conditionalPassive = GetConditionalPassiveStatBonus(statType);
        int finalValue = Mathf.Max(0, baseValue + speciesTrait + levelGrowth + conditionalPassive);
        return new CharacterStatBreakdown(
            statType,
            baseValue,
            speciesTrait,
            levelGrowth,
            conditionalPassive,
            finalValue);
    }

    public int GetBaseStat(CharacterStatType statType)
    {
        EnsureInitialized();
        return GrowthState.initialized
            ? GrowthState.initialBaseStats.Get(statType)
            : actor?.Identity?.Profile?.GetStat(statType) ?? 5;
    }

    public int GetSpeciesTraitStatBonus(CharacterStatType statType)
    {
        int value = 0;
        CharacterSO data = actor?.Identity?.Data;
        value += data?.species?.statBonus?.Get(statType) ?? 0;
        foreach (CharacterTraitSO trait in ResolveSelectedTraits())
        {
            value += trait?.statBonus?.Get(statType) ?? 0;
        }

        return value;
    }

    public int GetLevelGrowthStat(CharacterStatType statType)
    {
        EnsureInitialized();
        return GrowthState.initialized
            ? GrowthState.levelGrowthStats.Get(statType)
            : 0;
    }

    public int GetCurrentConditionalPassiveStatBonus(CharacterStatType statType)
    {
        EnsureInitialized();
        return GrowthState.initialized
            ? GetConditionalPassiveStatBonus(statType)
            : 0;
    }

    public int GetFinalStat(string statId)
    {
        return CharacterStatCatalog.TryGet(statId, out CharacterStatDefinition definition)
            && definition.LegacyType.HasValue
                ? GetFinalStat(definition.LegacyType.Value)
                : 0;
    }

    public IReadOnlyList<CharacterTraitSO> ResolveSelectedTraits()
    {
        CharacterSO data = actor?.Identity?.Data;
        int key = data != null
            ? BuildEffectiveRuntimeProfileKey(data)
            : 0;
        if (resolvedSelectedTraitsKey == key
            && resolvedSelectedTraitsView != null)
        {
            return resolvedSelectedTraitsView;
        }

        resolvedSelectedTraits.Clear();
        IReadOnlyList<int> traitIds = GrowthState.traitIds;
        if (traitIds == null || traitIds.Count == 0)
        {
            IReadOnlyList<CharacterTraitSO> sourceTraits = data?.traits;
            if (sourceTraits != null)
            {
                for (int index = 0; index < sourceTraits.Count; index++)
                {
                    CharacterTraitSO trait = sourceTraits[index];
                    if (trait != null)
                    {
                        resolvedSelectedTraits.Add(trait);
                    }
                }
            }
        }
        else
        {
            EnsureTraitCache();
            for (int index = 0; index < traitIds.Count; index++)
            {
                if (TraitCache.TryGetValue(
                        traitIds[index],
                        out CharacterTraitSO trait)
                    && trait != null)
                {
                    resolvedSelectedTraits.Add(trait);
                }
            }
        }

        resolvedSelectedTraitsKey = key;
        resolvedSelectedTraitsView ??= ReadOnlyView.List(resolvedSelectedTraits);
        return resolvedSelectedTraitsView;
    }

    public CharacterRuntimeProfile GetEffectiveRuntimeProfile()
    {
        CharacterSO data = actor?.Identity?.Data;
        if (data == null)
        {
            return actor?.Identity?.Profile;
        }

        int key = BuildEffectiveRuntimeProfileKey(data);

        if (effectiveRuntimeProfile == null || effectiveRuntimeProfileKey != key)
        {
            effectiveRuntimeProfileKey = key;
            using (EffectiveProfileBuildMarker.Auto())
            {
                effectiveRuntimeProfile = CharacterRuntimeProfile.From(data, ResolveSelectedTraits());
            }
        }

        return effectiveRuntimeProfile;
    }

    private int BuildEffectiveRuntimeProfileKey(CharacterSO data)
    {
        IReadOnlyList<int> traitIds = GrowthState.traitIds;
        if (traitIds == null || traitIds.Count == 0)
        {
            return data.GetInstanceID();
        }

        unchecked
        {
            uint sum = 0u;
            uint squaredSum = 0u;
            uint xor = 0u;
            for (int index = 0; index < traitIds.Count; index++)
            {
                uint mixed = MixTraitId((uint)traitIds[index]);
                sum += mixed;
                squaredSum += mixed * mixed;
                xor ^= mixed;
            }

            int key = data.GetInstanceID();
            key = (key * 397) ^ traitIds.Count;
            key = (key * 397) ^ (int)sum;
            key = (key * 397) ^ (int)squaredSum;
            key = (key * 397) ^ (int)xor;
            return key;
        }
    }

    private static uint MixTraitId(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
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
                gameEventBus?.RaiseAlert(
                    "기술 선택 가능",
                    $"{actor?.Identity?.DisplayName ?? "인물"}의 Lv.{draft.unlockLevel} 액티브 후보가 준비되었습니다.",
                    EventAlertImportance.Medium,
                    "성장",
                    new[]
                    {
                        new EventAlertChoice(
                            "성장 탭 열기",
                            "후보 3개를 확인합니다.",
                            RequestGrowthTab)
                    });
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

        gameEventBus?.ShowInfo(actor);
        gameEventBus?.Publish(new CharacterGrowthTabRequestedEvent(actor));
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
        level = Mathf.Clamp(snapshot.Level, 1, MaxLevel);
        currentExperience = level >= MaxLevel
            ? 0
            : Mathf.Clamp(snapshot.CurrentExperience, 0, GetExperienceRequired(level) - 1);
        growthState = snapshot.GrowthState?.Clone() ?? new CharacterGrowthState();
        narrativeLedger = snapshot.NarrativeLedger?.Clone() ?? new CharacterNarrativeLedger();
        InvalidateEffectiveRuntimeProfile();
        GrowthState.EnsureCollections();
        initializedDataInstanceId = actor?.Identity?.Data != null
            ? actor.Identity.Data.GetInstanceID()
            : 0;
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
        level = Mathf.Clamp(restoredLevel <= 0 ? 1 : restoredLevel, 1, MaxLevel);
        currentExperience = level >= MaxLevel
            ? 0
            : Mathf.Clamp(restoredExperience, 0, GetExperienceRequired(level) - 1);
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

    private void EnsureInitialized()
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        GrowthState.EnsureCollections();
        if (actor == null || actor.Identity == null || actor.Identity.Data == null)
        {
            return;
        }

        int dataInstanceId = actor.Identity.Data.GetInstanceID();
        if (initializedDataInstanceId == dataInstanceId && GrowthState.initialized)
        {
            return;
        }

        initializedDataInstanceId = dataInstanceId;
        if (!GrowthState.initialized)
        {
            CharacterSkillSystemSettingsSO settings = settingsProvider?.Settings
                ?? CharacterSkillSystemSettingsSO.CreateRuntimeDefaults();
            string seedSource = actor.Identity.PersistentId;
            if (string.IsNullOrWhiteSpace(seedSource))
            {
                seedSource = $"{actor.Identity.Data.id}:{actor.Identity.DisplayName}:{GetInstanceID()}";
            }

            int seed = CharacterGrowthRules.StableHash(seedSource);
            System.Random random = new System.Random(seed);
            GrowthState.initialized = true;
            GrowthState.generationSeed = seed;
            GrowthState.displayName = actor.Identity.DisplayName;
            GrowthState.origin = actor.Identity.GetSpeciesShortDescription();
            GrowthState.initialBaseStats = actor.Identity.Data.baseStats != null
                ? CharacterSkillModelUtility.CopyStats(actor.Identity.Data.baseStats)
                : CharacterGrowthRules.RollInitialStats(settings, random);
            GrowthState.levelGrowthStats = new CharacterStatBlock();
            GrowthState.potentialGrade = CharacterGrowthRules.RollPotential(settings, random);
            GrowthState.traitIds = actor.Identity.Data.traits?
                .Where(item => item != null)
                .Select(item => item.id)
                .Distinct()
                .Take(3)
                .ToList() ?? new List<int>();
            GrowthState.autoChooseDrafts = actor.Identity.CharacterType == CharacterType.Customer;
            InvalidateEffectiveRuntimeProfile();
        }

        RebuildLegacySkillViews();
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
        CharacterSkillSystemSettingsSO settings = settingsProvider?.Settings
            ?? CharacterSkillSystemSettingsSO.CreateRuntimeDefaults();
        int points = CharacterGrowthRules.GetGrowthPointsForLevel(reachedLevel);
        System.Random random = new System.Random(GrowthState.generationSeed ^ (reachedLevel * 486187739));
        CharacterGrowthRules.AllocateGrowthPoints(
            GrowthState,
            NarrativeLedger,
            reachedLevel,
            points,
            settings.levelGrowthStatCap,
            settings.identityGrowthWeight,
            random);
    }

    private int GetConditionalPassiveStatBonus(CharacterStatType statType)
    {
        int bonus = 0;
        IReadOnlyList<CharacterSkillInstance> passives =
            GrowthState.passiveSkills;
        for (int passiveIndex = 0;
            passiveIndex < passives.Count;
            passiveIndex++)
        {
            CharacterSkillInstance passive = passives[passiveIndex];
            if (passive == null)
            {
                continue;
            }

            bool conditionActive = passive.trigger switch
            {
                CharacterSkillTrigger.DamageTaken => actor != null && actor.InjurySeverity >= 0.25f,
                CharacterSkillTrigger.NeedChanged => actor?.Stats != null && actor.Stats.Mood < 50f,
                CharacterSkillTrigger.MoodChanged => actor?.Stats != null && actor.Stats.Mood >= 70f,
                _ => false
            };
            if (!conditionActive)
            {
                continue;
            }

            IReadOnlyList<CharacterSkillModuleSelection> modules =
                passive.modules;
            if (modules == null)
            {
                continue;
            }

            for (int moduleIndex = 0;
                moduleIndex < modules.Count;
                moduleIndex++)
            {
                CharacterSkillModuleSelection module = modules[moduleIndex];
                if ((statType == CharacterStatType.Attack || statType == CharacterStatType.Strength)
                    && string.Equals(module.moduleId, "buff", StringComparison.Ordinal))
                {
                    bonus += 2;
                }
                else if (statType == CharacterStatType.Endurance
                    && string.Equals(module.moduleId, "protect", StringComparison.Ordinal))
                {
                    bonus += 2;
                }
            }
        }

        return bonus;
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
            gameEventBus?.RaiseAlert(
                skill.displayName,
                $"{skill.narrativeReason}\n{skill.description}",
                EventAlertImportance.Medium,
                "성장");
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
            gameEventBus?.RaiseAlert(
                GrowthState.ultimate.displayName,
                $"{GrowthState.ultimate.narrativeReason}\n{GrowthState.ultimate.description}",
                EventAlertImportance.High,
                "성장");
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
        effectiveRuntimeProfile = null;
        effectiveRuntimeProfileKey = 0;
        resolvedSelectedTraitsKey = int.MinValue;
    }

    private void WarmEffectiveRuntimeProfile()
    {
        if (actor?.Identity?.Data != null)
        {
            GetEffectiveRuntimeProfile();
        }
    }

    private static void EnsureTraitCache()
    {
        if (TraitCache.Count > 0)
        {
            return;
        }

        IResourcesAssetLoader loader = traitResourcesAssetLoader ?? new UnityResourcesAssetLoader();
        foreach (CharacterTraitSO trait in loader.LoadAllOptional<CharacterTraitSO>(TraitResourcePath))
        {
            if (trait != null)
            {
                TraitCache[trait.id] = trait;
            }
        }
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

public readonly struct CharacterGrowthTabRequestedEvent
{
    public CharacterGrowthTabRequestedEvent(CharacterActor actor)
    {
        Actor = actor;
    }

    public CharacterActor Actor { get; }
}
