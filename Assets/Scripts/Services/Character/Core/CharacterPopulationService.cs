using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public interface ICharacterPopulationService
{
    IReadOnlyList<WorldCharacterProfile> Profiles { get; }
    WorldCharacterProfile AcquireVisitor(
        CharacterSO characterData,
        IEnumerable<string> unavailableProfileIds = null);
    void BindActor(WorldCharacterProfile profile, CharacterActor actor);
    void RefreshProfile(CharacterActor actor);
    void ReleaseVisitor(CharacterActor actor);
    void PromoteToStaff(CharacterActor actor);
    bool TryGetProfile(CharacterActor actor, out WorldCharacterProfile profile);
    List<WorldCharacterProfile> CaptureProfiles();
    void RestoreProfiles(IEnumerable<WorldCharacterProfile> profiles);
}

public sealed class CharacterPopulationService : ICharacterPopulationService, IDisposable
{
    private sealed class PendingProfilePreparation
    {
        public WorldCharacterProfile profile;
        public CharacterProgression progression;
        public GameObject previewObject;
    }

    private const string TraitResourcePath = "SO/Character/Traits";
    private const string CharacterResourcePath = "SO/Character";
    private const int MaximumConcurrentPreparations = 2;

    private static readonly string[] GivenNames =
    {
        "리온", "미루", "세나", "로웬", "이안", "노아", "테오", "루아",
        "에린", "카일", "리브", "모라", "유나", "다인", "벨", "레오"
    };

    private static readonly string[] Origins =
    {
        "북쪽 항구촌",
        "안개 낀 구릉",
        "붉은 구릉",
        "왕도 변두리",
        "깊은 숲 마을",
        "몰락한 공방",
        "서부 용병 주둔지",
        "지하 수로 정착지"
    };

    private readonly ICharacterSkillSystemSettingsProvider settingsProvider;
    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private readonly ICharacterSkillGenerationService skillGenerationService;
    private readonly IRunVariableRuntimeProvider runVariableRuntimeProvider;
    private readonly List<WorldCharacterProfile> profiles = new List<WorldCharacterProfile>();
    private readonly Dictionary<CharacterActor, WorldCharacterProfile> actors =
        new Dictionary<CharacterActor, WorldCharacterProfile>();
    private readonly Dictionary<string, PendingProfilePreparation> pendingPreparations =
        new Dictionary<string, PendingProfilePreparation>(StringComparer.Ordinal);
    private CharacterTraitSO[] traitPool;
    private CharacterSO[] customerTemplates;
    private int creationSerial;
    private bool reachedReadyTarget;
    private bool replenishing;
    private bool pumpingPreparations;

    public CharacterPopulationService(
        ICharacterSkillSystemSettingsProvider settingsProvider,
        IResourcesAssetLoader resourcesAssetLoader,
        ICharacterSkillGenerationService skillGenerationService)
        : this(settingsProvider, resourcesAssetLoader, skillGenerationService, null)
    {
    }

    [Inject]
    public CharacterPopulationService(
        ICharacterSkillSystemSettingsProvider settingsProvider,
        IResourcesAssetLoader resourcesAssetLoader,
        ICharacterSkillGenerationService skillGenerationService,
        IRunVariableRuntimeProvider runVariableRuntimeProvider)
    {
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
        this.skillGenerationService = skillGenerationService
            ?? throw new ArgumentNullException(nameof(skillGenerationService));
        this.runVariableRuntimeProvider = runVariableRuntimeProvider;
    }

    public IReadOnlyList<WorldCharacterProfile> Profiles => profiles;

    public WorldCharacterProfile AcquireVisitor(
        CharacterSO characterData,
        IEnumerable<string> unavailableProfileIds = null)
    {
        if (characterData == null || characterData.characterType != CharacterType.Customer)
        {
            return null;
        }

        EnsurePreparedPool();

        HashSet<string> unavailable = new HashSet<string>(
            unavailableProfileIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        WorldCharacterProfile returning = profiles
            .Where(profile => profile != null
                && profile.isAlive
                && !profile.isStaff
                && !profile.isVisiting
                && profile.IsReady
                && !unavailable.Contains(profile.persistentId)
                && profile.characterDataId == characterData.id)
            .OrderBy(profile => profile.visitCount)
            .ThenBy(profile => profile.persistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (returning != null)
        {
            returning.isVisiting = true;
            EnsurePreparedPool();
            return returning;
        }

        return null;
    }

    public void BindActor(WorldCharacterProfile profile, CharacterActor actor)
    {
        if (profile == null || actor == null)
        {
            return;
        }

        actor.EnsureRuntimeState();
        actor.Identity?.SetPersistentId(profile.persistentId);
        actors[actor] = profile;
        profile.isVisiting = !profile.isStaff;
        ApplyStaffRuntimeState(profile, actor);
        CharacterProgression progression = actor.Progression;
        if (profile.growth != null && profile.growth.initialized)
        {
            progression?.RestorePersistentState(new CharacterProgressionSnapshot(
                profile.level,
                profile.currentExperience,
                profile.growth,
                profile.narrative));
        }
        else
        {
            progression?.ApplyPreparedIdentity(
                profile.displayName,
                profile.origin,
                profile.growth?.traitIds,
                profile.growth?.initialBaseStats,
                profile.growth?.potentialGrade ?? CharacterPotentialGrade.Ordinary,
                profile.growth?.generationSeed ?? CharacterGrowthRules.StableHash(profile.persistentId),
                autoChooseDrafts: true);
        }

        actor.SocialMemory?.RestoreSnapshot(profile.socialMemory);
        ApplyStaffRuntimeState(profile, actor);
    }

    public void ReleaseVisitor(CharacterActor actor)
    {
        if (!TryGetProfile(actor, out WorldCharacterProfile profile))
        {
            return;
        }

        SynchronizeProfile(profile, actor);
        if (profile.isStaff)
        {
            profile.isVisiting = false;
            ApplyStaffRuntimeState(profile, actor);
            EnsurePreparedPool();
            return;
        }

        profile.isVisiting = false;
        profile.visitCount++;
        actors.Remove(actor);
        EnsurePreparedPool();
    }

    public void RefreshProfile(CharacterActor actor)
    {
        if (TryGetProfile(actor, out WorldCharacterProfile profile))
        {
            SynchronizeProfile(profile, actor);
            ApplyStaffRuntimeState(profile, actor);
        }
    }

    public void PromoteToStaff(CharacterActor actor)
    {
        if (!TryGetProfile(actor, out WorldCharacterProfile profile))
        {
            return;
        }

        profile.isStaff = true;
        profile.isVisiting = false;
        ApplyStaffRuntimeState(profile, actor);
        SynchronizeProfile(profile, actor);
        EnsurePreparedPool();
    }

    public bool TryGetProfile(CharacterActor actor, out WorldCharacterProfile profile)
    {
        profile = null;
        if (actor == null)
        {
            return false;
        }

        if (actors.TryGetValue(actor, out profile) && profile != null)
        {
            return true;
        }

        string persistentId = actor.Identity?.PersistentId;
        profile = profiles.FirstOrDefault(candidate => candidate != null
            && string.Equals(candidate.persistentId, persistentId, StringComparison.Ordinal));
        if (profile != null)
        {
            actors[actor] = profile;
            ApplyStaffRuntimeState(profile, actor);
            return true;
        }

        return false;
    }

    public List<WorldCharacterProfile> CaptureProfiles()
    {
        foreach (PendingProfilePreparation pending in pendingPreparations.Values.ToArray())
        {
            SynchronizePendingProfile(pending);
        }

        foreach (KeyValuePair<CharacterActor, WorldCharacterProfile> pair in actors.ToArray())
        {
            if (pair.Key != null && pair.Value != null)
            {
                SynchronizeProfile(pair.Value, pair.Key);
            }
        }

        return profiles
            .Where(profile => profile != null)
            .Select(profile => profile.Clone())
            .ToList();
    }

    public void RestoreProfiles(IEnumerable<WorldCharacterProfile> restoredProfiles)
    {
        CancelAllPreparations();
        actors.Clear();
        profiles.Clear();
        List<WorldCharacterProfile> restored = restoredProfiles?
            .Where(profile => profile != null)
            .Select(profile => profile.Clone())
            .ToList() ?? new List<WorldCharacterProfile>();
        WorldCharacterProfile invalid = restored.FirstOrDefault(profile => string.IsNullOrWhiteSpace(profile.persistentId));
        if (invalid != null)
        {
            throw new InvalidOperationException("A world character profile is missing its persistent ID.");
        }

        string duplicateId = restored
            .GroupBy(profile => profile.persistentId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(duplicateId))
        {
            throw new InvalidOperationException(
                $"Duplicate world character profile ID '{duplicateId}' cannot be restored.");
        }

        profiles.AddRange(restored);
        foreach (WorldCharacterProfile profile in profiles)
        {
            profile.isVisiting = false;
        }

        creationSerial = profiles
            .Select(profile => ParseSerial(profile.persistentId))
            .DefaultIfEmpty(0)
            .Max();
        int availableReady = CountAvailableReadyProfiles();
        reachedReadyTarget = availableReady >= settingsProvider.Settings.guestReadyTarget;
        replenishing = reachedReadyTarget
            && availableReady <= settingsProvider.Settings.guestReadyLowWatermark;
        EnsurePreparedPool();
    }

    public void Dispose()
    {
        CancelAllPreparations();
        actors.Clear();
    }

    private void EnsurePreparedPool()
    {
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        int target = Mathf.Max(1, settings.guestReadyTarget);
        int lowWatermark = Mathf.Clamp(settings.guestReadyLowWatermark, 0, target);
        int availableReady = CountAvailableReadyProfiles();
        int availableOrQueued = CountAvailableOrQueuedProfiles();

        if (!reachedReadyTarget)
        {
            replenishing = true;
        }
        else if (!replenishing && availableReady <= lowWatermark)
        {
            replenishing = true;
        }

        if (replenishing)
        {
            CharacterSO[] templates = GetCustomerTemplates();
            int maximumAlive = Mathf.Max(target, settings.maximumAliveNonStaffGuests);
            while (availableOrQueued < target
                && CountAliveNonStaff() < maximumAlive
                && templates.Length > 0)
            {
                CharacterSO template = templates[creationSerial % templates.Length];
                profiles.Add(CreateProfile(template));
                availableOrQueued = CountAvailableOrQueuedProfiles();
            }
        }

        if (availableReady >= target)
        {
            reachedReadyTarget = true;
            replenishing = false;
        }

        PumpPreparations();
    }

    private void PumpPreparations()
    {
        if (pumpingPreparations)
        {
            return;
        }

        pumpingPreparations = true;
        try
        {
            while (pendingPreparations.Count < MaximumConcurrentPreparations)
            {
                WorldCharacterProfile next = profiles.FirstOrDefault(profile => profile != null
                    && profile.isAlive
                    && !profile.isStaff
                    && !profile.isVisiting
                    && !profile.IsReady
                    && !pendingPreparations.ContainsKey(profile.persistentId));
                if (next == null)
                {
                    break;
                }

                BeginPreparation(next);
            }
        }
        finally
        {
            pumpingPreparations = false;
        }
    }

    private void BeginPreparation(WorldCharacterProfile profile)
    {
        if (profile == null
            || profile.IsReady
            || pendingPreparations.ContainsKey(profile.persistentId))
        {
            return;
        }

        CharacterSO characterData = GetCustomerTemplates()
            .FirstOrDefault(candidate => candidate.id == profile.characterDataId);
        if (characterData == null)
        {
            return;
        }

        GameObject preview = new GameObject($"WorldProfilePreparation_{profile.persistentId}")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        CharacterProgression progression = preview.AddComponent<CharacterProgression>();
        progression.ConstructCharacterProgression(skillGenerationService, settingsProvider);
        progression.SetPublicSkillNotificationsSuppressed(true);
        PendingProfilePreparation pending = new PendingProfilePreparation
        {
            profile = profile,
            progression = progression,
            previewObject = preview
        };
        pendingPreparations[profile.persistentId] = pending;
        progression.Changed += () => HandlePreparationChanged(profile.persistentId);
        progression.RestorePersistentState(new CharacterProgressionSnapshot(
            Mathf.Max(1, profile.level),
            Mathf.Max(0, profile.currentExperience),
            profile.growth,
            profile.narrative));
        TryCompletePreparation(profile.persistentId);
    }

    private void HandlePreparationChanged(string persistentId)
    {
        TryCompletePreparation(persistentId);
    }

    private void TryCompletePreparation(string persistentId)
    {
        if (!pendingPreparations.TryGetValue(persistentId, out PendingProfilePreparation pending)
            || pending.progression == null)
        {
            return;
        }

        SynchronizePendingProfile(pending);
        if (!pending.profile.IsReady)
        {
            return;
        }

        pendingPreparations.Remove(persistentId);
        skillGenerationService.CancelRequests(pending.progression);
        DestroyPreview(pending.previewObject);
        EnsurePreparedPool();
    }

    private static void SynchronizePendingProfile(PendingProfilePreparation pending)
    {
        if (pending?.profile == null || pending.progression == null)
        {
            return;
        }

        CharacterProgressionSnapshot snapshot = pending.progression.CapturePersistentState();
        pending.profile.level = snapshot.Level;
        pending.profile.currentExperience = snapshot.CurrentExperience;
        pending.profile.growth = snapshot.GrowthState.Clone();
        pending.profile.narrative = snapshot.NarrativeLedger.Clone();
        pending.profile.displayName = pending.profile.growth.displayName;
        pending.profile.origin = pending.profile.growth.origin;
    }

    private void CancelAllPreparations()
    {
        foreach (PendingProfilePreparation pending in pendingPreparations.Values.ToArray())
        {
            if (pending.progression != null)
            {
                skillGenerationService.CancelRequests(pending.progression);
            }

            DestroyPreview(pending.previewObject);
        }

        pendingPreparations.Clear();
    }

    private CharacterSO[] GetCustomerTemplates()
    {
        customerTemplates ??= resourcesAssetLoader
            .LoadAllRequired<CharacterSO>(CharacterResourcePath)
            .Where(candidate => candidate != null && candidate.characterType == CharacterType.Customer)
            .GroupBy(candidate => candidate.id)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.id)
            .ToArray();
        return customerTemplates;
    }

    private int CountAvailableReadyProfiles()
    {
        return profiles.Count(profile => profile != null
            && profile.isAlive
            && !profile.isStaff
            && !profile.isVisiting
            && profile.IsReady);
    }

    private int CountAvailableOrQueuedProfiles()
    {
        return profiles.Count(profile => profile != null
            && profile.isAlive
            && !profile.isStaff
            && !profile.isVisiting);
    }

    private static void DestroyPreview(GameObject preview)
    {
        if (preview == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(preview);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }

    private WorldCharacterProfile CreateProfile(CharacterSO data)
    {
        creationSerial++;
        int runSeed = runVariableRuntimeProvider != null
            && runVariableRuntimeProvider.TryGetRuntime(out RunVariableRuntime runtime)
                ? runtime.RunSeed
                : 0;
        string persistentId = $"world:{runSeed}:{creationSerial:D6}";
        int seed = CharacterGrowthRules.StableHash(persistentId);
        System.Random random = new System.Random(seed);
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        CharacterGrowthState growth = new CharacterGrowthState
        {
            initialized = true,
            autoChooseDrafts = true,
            generationSeed = seed,
            displayName = $"{GivenNames[random.Next(GivenNames.Length)]} {creationSerial}",
            origin = $"{data.SpeciesTag} · {Origins[random.Next(Origins.Length)]}",
            potentialGrade = CharacterGrowthRules.RollPotential(settings, random),
            initialBaseStats = CharacterGrowthRules.RollInitialStats(settings, random),
            levelGrowthStats = new CharacterStatBlock(),
            traitIds = RollTraits(settings, random)
        };
        growth.EnsureCollections();
        return new WorldCharacterProfile
        {
            persistentId = persistentId,
            characterDataId = data.id,
            displayName = growth.displayName,
            origin = growth.origin,
            growth = growth,
            narrative = new CharacterNarrativeLedger()
        };
    }

    private List<int> RollTraits(
        CharacterSkillSystemSettingsSO settings,
        System.Random random)
    {
        traitPool ??= resourcesAssetLoader
            .LoadAllRequired<CharacterTraitSO>(TraitResourcePath)
            .Where(trait => trait != null)
            .OrderBy(trait => trait.id)
            .ToArray();
        List<int> selected = new List<int>(3);
        foreach (CharacterTraitSO candidate in traitPool.OrderBy(_ => random.Next()))
        {
            bool conflicts = settings.traitConflicts.Any(rule => rule != null
                && ((rule.firstTraitId == candidate.id && selected.Contains(rule.secondTraitId))
                    || (rule.secondTraitId == candidate.id && selected.Contains(rule.firstTraitId))));
            if (selected.Contains(candidate.id) || conflicts)
            {
                continue;
            }

            selected.Add(candidate.id);
            if (selected.Count >= 3)
            {
                break;
            }
        }

        return selected;
    }

    private int CountAliveNonStaff()
    {
        return profiles.Count(profile => profile != null && profile.isAlive && !profile.isStaff);
    }

    private static void SynchronizeProfile(WorldCharacterProfile profile, CharacterActor actor)
    {
        CharacterProgressionSnapshot snapshot = actor.Progression?.CapturePersistentState();
        if (snapshot != null)
        {
            profile.level = snapshot.Level;
            profile.currentExperience = snapshot.CurrentExperience;
            profile.growth = snapshot.GrowthState.Clone();
            profile.narrative = snapshot.NarrativeLedger.Clone();
            profile.displayName = profile.growth.displayName;
            profile.origin = profile.growth.origin;
        }

        profile.isAlive = !actor.IsDead;
        profile.socialMemory = actor.SocialMemory?.CaptureSnapshot()
            ?? new CharacterSocialMemorySnapshot();
        ApplyStaffRuntimeState(profile, actor);
    }

    private static void ApplyStaffRuntimeState(WorldCharacterProfile profile, CharacterActor actor)
    {
        if (profile == null || actor == null || !profile.isStaff)
        {
            return;
        }

        actor.EnsureRuntimeState();
        actor.characterType = CharacterType.NPC;
        actor.Identity?.SetCharacterType(CharacterType.NPC);
        actor.RefreshAbilityCache();
    }

    private static int ParseSerial(string persistentId)
    {
        string suffix = persistentId?.Split(':').LastOrDefault();
        return int.TryParse(suffix, out int serial) ? serial : 0;
    }
}
