using System;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Items/Item Hauling Settings", order = 1)]
public sealed class ItemHaulingSettingsSO : ScriptableObject
{
    public const string ResourcePath = "SO/Items/ItemHaulingSettings";

    [SerializeField, Range(1f, 2.5f)] private float maxCarryMultiplier = 1.5f;

    public float MaxCarryMultiplier => Mathf.Clamp(maxCarryMultiplier, 1f, 2.5f);
}

public interface IItemHaulingSettingsProvider
{
    float MaxCarryMultiplier { get; }
    ItemHaulingSettingsSnapshot Capture();
    void Restore(ItemHaulingSettingsSnapshot snapshot);
}

internal sealed class ItemHaulingSettingsRuntimeState
{
    internal float? RestoredMultiplier { get; set; }
}

public sealed class ResourceItemHaulingSettingsProvider : IItemHaulingSettingsProvider
{
    private readonly ItemHaulingSettingsSO settings;
    private readonly IDungeonUserSettingsService userSettings;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private ItemHaulingSettingsRuntimeState state =>
        aggregateRootStore.GetOrCreate(() => new ItemHaulingSettingsRuntimeState());

    public ResourceItemHaulingSettingsProvider(
        IGameContentCatalog content,
        IDungeonUserSettingsService userSettings,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        settings = (content ?? throw new ArgumentNullException(nameof(content)))
            .RequireSingle<ItemHaulingSettingsSO>();
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public float MaxCarryMultiplier
    {
        get
        {
            float value = state.RestoredMultiplier
                ?? userSettings.Current.maxCarryMultiplier;

            if (value <= 0f)
            {
                value = settings.MaxCarryMultiplier;
            }

            return Mathf.Clamp(Mathf.Round(value / 0.05f) * 0.05f, 1f, 2.5f);
        }
    }

    public ItemHaulingSettingsSnapshot Capture()
    {
        return new ItemHaulingSettingsSnapshot
        {
            maxCarryMultiplier = MaxCarryMultiplier
        };
    }

    public void Restore(ItemHaulingSettingsSnapshot snapshot)
    {
        if (snapshot == null)
        {
            aggregateRootStore.Replace(new ItemHaulingSettingsRuntimeState());
            return;
        }

        snapshot.Normalize();
        aggregateRootStore.Replace(new ItemHaulingSettingsRuntimeState
        {
            RestoredMultiplier = snapshot.maxCarryMultiplier
        });
    }
}
