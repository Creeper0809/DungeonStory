using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class CharacterPopulationApplicationAdapter : IDisposable
{
    private sealed class PendingProfilePreparation
    {
        public readonly WorldCharacterProfile profile;
        public readonly CharacterProgression progression;
        public readonly GameObject previewObject;

        public PendingProfilePreparation(
            WorldCharacterProfile profile,
            CharacterProgression progression,
            GameObject previewObject)
        {
            this.profile = profile;
            this.progression = progression;
            this.previewObject = previewObject;
        }
    }

    private const int MaximumConcurrentPreparations = 2;
    private const string CustomerTemplateCache = "customer-templates";
    private const string TraitPoolCache = "trait-pool";

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
    private readonly IGameContentCatalog content;
    private readonly ICharacterSkillGenerationService skillGenerationService;
    private readonly RunVariableRuntime runVariables;
    private readonly IFactionContractQuery factionContracts;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly CharacterPopulationAggregate<
        WorldCharacterProfile,
        CharacterActor,
        PendingProfilePreparation> populationAggregate = new();
    private readonly List<CharacterTraitSO> traitPool = new();
    private readonly List<CharacterSO> customerTemplates = new();
    private readonly HashSet<string> initializedCaches = new(StringComparer.Ordinal);
    [ApplicationAdapterTransientState]
    private bool applyingPreparations;

    private CharacterPopulationDomain<WorldCharacterProfile> population =>
        populationAggregate.Population;
    private Dictionary<CharacterActor, WorldCharacterProfile> actors =>
        populationAggregate.Actors;
    private Dictionary<string, PendingProfilePreparation> pendingPreparations =>
        populationAggregate.Preparations;

    [Inject]
    public CharacterPopulationApplicationAdapter(
        ICharacterSkillSystemSettingsProvider settingsProvider,
        IGameContentCatalog content,
        ICharacterSkillGenerationService skillGenerationService,
        DungeonSceneRuntimeReferences sceneRuntimes,
        IFactionContractQuery factionContracts,
        IRunCharacterCatalog characterCatalog)
    {
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.skillGenerationService = skillGenerationService
            ?? throw new ArgumentNullException(nameof(skillGenerationService));
        runVariables = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterPopulationApplicationAdapter)} requires a loaded {nameof(RunVariableRuntime)}.");
        this.factionContracts = factionContracts
            ?? throw new ArgumentNullException(nameof(factionContracts));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyList<WorldCharacterProfile> Profiles => population.Profiles;

    public WorldCharacterProfile AcquireVisitor(
        CharacterSO characterData,
        IEnumerable<string> unavailableProfileIds = null)
    {
        if (characterData == null || characterData.characterType != CharacterType.Customer)
        {
            return null;
        }

        EnsurePreparedPool();

        WorldCharacterProfile returning = population.AcquireVisitor(
            characterData.id,
            unavailableProfileIds);
        if (returning != null)
        {
            EnsurePreparedPool();
            return returning;
        }

        return null;
    }

    public bool TryCreateRecruitCandidate(
        out WorldCharacterProfile profile,
        out CharacterSO sourceData)
    {
        CharacterSO[] templates = GetCustomerTemplates();
        if (templates.Length == 0)
        {
            profile = null;
            sourceData = null;
            return false;
        }

        sourceData = templates[population.CreationSerial % templates.Length];
        profile = CreateProfile(sourceData);
        population.Add(profile);

        // Offense rewards are explicit player-facing candidates, so their
        // identity and skill preparation takes priority over pool replenishment.
        BeginPreparation(profile);
        return true;
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
            population.ReleaseVisitor(profile);
            ApplyStaffRuntimeState(profile, actor);
            EnsurePreparedPool();
            return;
        }

        population.ReleaseVisitor(profile);
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

        population.PromoteToStaff(profile);
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
        if (population.TryGet(persistentId, out profile))
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

        return population.Capture(profile => profile.Clone());
    }

    public void RestoreProfiles(IEnumerable<WorldCharacterProfile> restoredProfiles)
    {
        CharacterPopulationRestoreTransaction transaction =
            ApplyRestoreCandidate(BuildRestoreCandidate(restoredProfiles));
        CompleteRestore(transaction);
        ReplenishPreparedPoolBestEffort();
    }

    public CharacterPopulationRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldCharacterProfile> restoredProfiles)
    {
        CharacterPopulationDomain<WorldCharacterProfile> restored = new();
        restored.Restore(
            restoredProfiles,
            profile => profile.Clone(),
            settingsProvider.Settings.guestReadyTarget,
            settingsProvider.Settings.guestReadyLowWatermark);
        return new CharacterPopulationRestoreCandidate(this, restored);
    }

    public CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
        CharacterPopulationRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        CharacterPopulationDomain<WorldCharacterProfile> restored =
            candidate.Peek(this);
        CharacterPopulationAggregateRestore<
            WorldCharacterProfile,
            CharacterActor,
            PendingProfilePreparation> aggregateRestore =
            populationAggregate.BuildRestore(restored);

        CharacterPopulationRestoreTransaction transaction =
            new CharacterPopulationRestoreTransaction(
            this,
            rollback: () =>
            {
                populationAggregate.Rollback(
                    aggregateRestore,
                    RetirePreparationsBestEffort);
            },
            complete: () =>
            {
                populationAggregate.Complete(
                    aggregateRestore,
                    RetirePreparationsBestEffort);
            });

        candidate.Consume(this, restored);
        populationAggregate.Apply(aggregateRestore);
        return transaction;
    }

    public void RollbackRestore(CharacterPopulationRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Rollback(this);
    }

    public void CompleteRestore(CharacterPopulationRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Complete(this);
    }

    public void ReplenishPreparedPoolBestEffort()
    {
        try
        {
            EnsurePreparedPool();
        }
        catch
        {
            // Pool replenishment is derived work. A committed restore must remain committed
            // even when preview creation or progression preparation is temporarily unavailable.
        }
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
        int availableOrQueued = population.CountAvailableOrQueued();

        if (population.ShouldReplenish(target, lowWatermark))
        {
            CharacterSO[] templates = GetCustomerTemplates();
            int maximumAlive = Mathf.Max(target, settings.maximumAliveNonStaffGuests);
            while (availableOrQueued < target
                && population.CountAliveNonStaff() < maximumAlive
                && templates.Length > 0)
            {
                CharacterSO template = templates[population.CreationSerial % templates.Length];
                population.Add(CreateProfile(template));
                availableOrQueued = population.CountAvailableOrQueued();
            }
        }

        population.CompleteReplenishment(target);

        PumpPreparations();
    }

    private void PumpPreparations()
    {
        if (applyingPreparations)
        {
            return;
        }

        applyingPreparations = true;
        try
        {
            while (pendingPreparations.Count < MaximumConcurrentPreparations)
            {
                WorldCharacterProfile next = population.FindNextPreparation(
                    pendingPreparations.Keys);
                if (next == null)
                {
                    break;
                }

                BeginPreparation(next);
            }
        }
        finally
        {
            applyingPreparations = false;
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
        progression.ConfigurePreview(
            skillGenerationService,
            settingsProvider,
            new CharacterProgressionProfileProjector(content));
        progression.SetPublicSkillNotificationsSuppressed(true);
        PendingProfilePreparation pending = new(
            profile,
            progression,
            preview);
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
        CancelPreparations(pendingPreparations);
    }

    private void CancelPreparations(
        Dictionary<string, PendingProfilePreparation> preparations)
    {
        if (preparations == null)
        {
            return;
        }

        foreach (PendingProfilePreparation pending in preparations.Values.ToArray())
        {
            if (pending.progression != null)
            {
                skillGenerationService.CancelRequests(pending.progression);
            }

            DestroyPreview(pending.previewObject);
        }

        preparations.Clear();
    }

    private void RetirePreparationsBestEffort(
        IReadOnlyCollection<PendingProfilePreparation> preparations)
    {
        if (preparations == null)
        {
            return;
        }

        foreach (PendingProfilePreparation pending in preparations.ToArray())
        {
            try
            {
                if (pending.progression != null)
                {
                    skillGenerationService.CancelRequests(pending.progression);
                }
            }
            catch
            {
                // Retirement cannot invalidate a completed aggregate-root swap.
            }

            try
            {
                DestroyPreview(pending.previewObject);
            }
            catch
            {
                // Preview cleanup is best effort and never part of committed state.
            }
        }
    }

    private CharacterSO[] GetCustomerTemplates()
    {
        if (initializedCaches.Add(CustomerTemplateCache))
        {
            customerTemplates.AddRange(characterCatalog.Characters
                .Where(candidate => candidate != null
                    && candidate.characterType == CharacterType.Customer)
                .GroupBy(candidate => candidate.id)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.id));
        }
        return customerTemplates
            .Where(IsRecruitmentEligible)
            .ToArray();
    }

    private bool IsRecruitmentEligible(CharacterSO candidate)
    {
        CharacterSpeciesSO species = candidate?.species;
        bool recruitmentUnlocked = species != null
            && !string.IsNullOrWhiteSpace(species.homeFactionId)
            && factionContracts.IsContractUnlocked(
                species.homeFactionId,
                FactionContractKind.Recruitment);
        return CharacterSpawnRules.IsRecruitmentEligible(
            species != null,
            species?.ownerSelectable == true,
            species?.homeFactionId,
            recruitmentUnlocked);
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
        int runSeed = runVariables.RunSeed;
        string persistentId = population.NextPersistentId(runSeed);
        int creationSerial = population.CreationSerial;
        int seed = CharacterGrowthRules.StableHash(persistentId);
        IRandomStream random = new DeterministicRandomSequence(seed);
        CharacterSkillSystemSettingsSO settings = settingsProvider.Settings;
        CharacterGrowthState growth = new CharacterGrowthState
        {
            initialized = true,
            autoChooseDrafts = true,
            generationSeed = seed,
            displayName = $"{GivenNames[random.NextInt(0, GivenNames.Length)]} {creationSerial}",
            origin = $"{data.SpeciesTag} · {Origins[random.NextInt(0, Origins.Length)]}",
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
        IRandomStream random)
    {
        if (initializedCaches.Add(TraitPoolCache))
        {
            traitPool.AddRange(content.GetAll<CharacterTraitSO>()
                .Where(trait => trait != null)
                .OrderBy(trait => trait.id));
        }
        List<int> selected = new List<int>(3);
        foreach (CharacterTraitSO candidate in traitPool.OrderBy(_ => random.NextInt(0, int.MaxValue)))
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

}
