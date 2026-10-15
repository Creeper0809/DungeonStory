using System;
using UnityEngine;

public sealed class ShopUseExecutionStateModule : IBuildingStateModule
{
    private readonly Shop owner;
    public ShopUseExecutionStateModule(Shop owner) => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public string ModuleId => BuildingStateModuleIds.ShopInteraction;
    public int CurrentVersion => ShopInteractionState.CurrentVersion;
    // Inline Unity serialization materializes null nested classes as empty
    // objects. An absent patience/committed lot is meaningful execution state.
    public string CaptureState() => Newtonsoft.Json.JsonConvert.SerializeObject(owner.CaptureInteractionState());

    public bool TryRestoreState(int version, string payload, out string error)
    {
        error = string.Empty;
        try
        {
            if (version != CurrentVersion)
                throw new InvalidOperationException("Unsupported shop interaction version: " + version);
            ShopInteractionState restored = DungeonSaveJson.Deserialize<ShopInteractionState>(payload);
            if (restored == null)
                throw new InvalidOperationException("Shop interaction state is missing.");
            if (!restored.IsValid(owner.RequirePersistentInstanceId().Value, out error))
                return false;
            owner.RestoreInteractionState(restored);
            return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
    }
}
