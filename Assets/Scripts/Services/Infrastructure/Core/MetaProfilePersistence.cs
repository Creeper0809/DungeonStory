using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonMetaProfileData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int lifetimeEarnedCurrency;
    public int spentCurrency;
    public int completedRunCount;
    public List<DungeonStringIntSaveEntry> upgradeLevels = new List<DungeonStringIntSaveEntry>();
    public List<string> preservedRecipeIds = new List<string>();
}

public interface IMetaProfileStore
{
    string ProfilePath { get; }
    bool TryLoad(out DungeonMetaProfileData profile);
    void Save(MetaProgressionState state);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaProfileStore : IMetaProfileStore
{
    public MetaProfileStore()
    {
        ProfilePath = Path.Combine(Application.persistentDataPath, "Profile", "meta-profile.json");
    }

    public string ProfilePath { get; }

    public bool TryLoad(out DungeonMetaProfileData profile)
    {
        profile = null;
        if (!File.Exists(ProfilePath))
        {
            return false;
        }

        try
        {
            DungeonMetaProfileData loaded = JsonUtility.FromJson<DungeonMetaProfileData>(
                File.ReadAllText(ProfilePath));
            if (loaded == null || loaded.version != DungeonMetaProfileData.CurrentVersion)
            {
                return false;
            }

            profile = loaded;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Meta profile load failed: {exception.Message}");
            return false;
        }
    }

    public void Save(MetaProgressionState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        DungeonMetaProfileData profile = new DungeonMetaProfileData
        {
            lifetimeEarnedCurrency = state.LifetimeEarnedCurrency,
            spentCurrency = state.SpentCurrency,
            completedRunCount = state.CompletedRunCount,
            upgradeLevels = state.UpgradeLevels
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new DungeonStringIntSaveEntry { key = pair.Key, value = pair.Value })
                .ToList(),
            preservedRecipeIds = state.PreservedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
        };

        string directory = Path.GetDirectoryName(ProfilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = ProfilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(profile, true));
        File.Copy(temporaryPath, ProfilePath, true);
        File.Delete(temporaryPath);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaProfilePersistenceService :
    IStartable,
    IDisposable
{
    private const string AutoSaveSlot = "autosave";
    private readonly IMetaProfileStore store;
    private readonly IMetaProgressionPersistencePort runtime;
    private readonly IDungeonGameSaveSlotService slotService;
    private readonly IGameEventBus gameEventBus;
    private IDisposable runResultSubscription;
    private IDisposable upgradeSubscription;
    private bool started;

    public MetaProfilePersistenceService(
        IMetaProfileStore store,
        IMetaProgressionPersistencePort runtime,
        IDungeonGameSaveSlotService slotService,
        IGameEventBus gameEventBus)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        LoadProfile();
        runResultSubscription = gameEventBus.Subscribe<RunResultReadyEvent>(OnTriggerEvent);
        upgradeSubscription = gameEventBus.Subscribe<MetaUpgradePurchasedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        if (!started)
        {
            return;
        }

        runResultSubscription?.Dispose();
        runResultSubscription = null;
        upgradeSubscription?.Dispose();
        upgradeSubscription = null;
        started = false;
    }

    public void OnTriggerEvent(RunResultReadyEvent eventType)
    {
        SaveProfile();
        try
        {
            slotService.Save(AutoSaveSlot);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Final run autosave failed: {exception.Message}");
        }
    }

    public void OnTriggerEvent(MetaUpgradePurchasedEvent eventType)
    {
        SaveProfile();
    }

    public void SaveProfile()
    {
        store.Save(runtime.State);
    }

    private void LoadProfile()
    {
        if (!store.TryLoad(out DungeonMetaProfileData profile))
        {
            return;
        }

        runtime.State.Restore(
            profile.lifetimeEarnedCurrency,
            profile.spentCurrency,
            (profile.upgradeLevels ?? new List<DungeonStringIntSaveEntry>())
                .Where(entry => entry != null)
                .Select(entry => new KeyValuePair<string, int>(entry.key, entry.value)),
            profile.preservedRecipeIds,
            profile.completedRunCount);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRunTransitionService : IDungeonRunTransitionService
{
    private readonly IMetaProfileStore profileStore;
    private readonly IMetaProgressionPersistencePort runtime;
    private readonly IMetaRunSceneTransitionPort sceneNavigator;

    public DungeonRunTransitionService(
        IMetaProfileStore profileStore,
        IMetaProgressionPersistencePort runtime,
        IMetaRunSceneTransitionPort sceneNavigator)
    {
        this.profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
    }

    public bool IsTransitioning => sceneNavigator.IsTransitioning;

    public void StartNextRun()
    {
        if (IsTransitioning)
        {
            return;
        }

        profileStore.Save(runtime.State);

        sceneNavigator.StartNewRun();
    }
}
