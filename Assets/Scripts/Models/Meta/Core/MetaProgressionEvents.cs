using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct RunResultReadyEvent
{
    public RunResultSnapshot result { get; }

    public RunResultReadyEvent(RunResultSnapshot result)
    {
        this.result = result;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct MetaUpgradePurchasedEvent
{
    public MetaUpgradePurchasedEvent(string upgradeId)
    {
        UpgradeId = upgradeId ?? string.Empty;
    }

    public string UpgradeId { get; }
}

public interface IDungeonRunTransitionService
{
    bool IsTransitioning { get; }
    void StartNextRun();
}

public interface IMetaRunSceneTransitionPort
{
    bool IsTransitioning { get; }
    void StartNewRun();
}
