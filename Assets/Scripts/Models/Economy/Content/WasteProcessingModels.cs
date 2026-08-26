using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public readonly struct WasteFeedResult
{
    public WasteFeedResult(
        bool succeeded,
        string itemId,
        WasteOriginKind origin,
        float contamination,
        float nutrition,
        float diseaseChance,
        WasteFeedOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        ItemId = itemId ?? string.Empty;
        Origin = origin;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
        Nutrition = Mathf.Clamp01(nutrition);
        DiseaseChance = Mathf.Clamp01(diseaseChance);
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public string ItemId { get; }
    public WasteOriginKind Origin { get; }
    public float Contamination { get; }
    public float Nutrition { get; }
    public float DiseaseChance { get; }
    public WasteFeedOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }

}

public readonly struct WasteDirectFeedCandidate
{
    public WasteDirectFeedCandidate(
        ItemStackId stackId,
        string itemId,
        WasteOriginKind origin,
        float contamination,
        float nutrition,
        float diseaseChance)
    {
        StackId = stackId;
        ItemId = itemId ?? string.Empty;
        Origin = origin;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
        Nutrition = Mathf.Clamp01(nutrition);
        DiseaseChance = Mathf.Clamp01(diseaseChance);
    }

    public ItemStackId StackId { get; }
    public string ItemId { get; }
    public WasteOriginKind Origin { get; }
    public float Contamination { get; }
    public float Nutrition { get; }
    public float DiseaseChance { get; }
    public bool IsValid => StackId.IsValid
        && !string.IsNullOrWhiteSpace(ItemId)
        && Origin != WasteOriginKind.Unknown
        && Nutrition > 0f;
}

public enum WasteFeedOutcomeCode
{
    None = 0,
    FeedDeliveryRequested,
    FeedConsumed
}

public readonly struct WastePolicyCommandResult
{
    public WastePolicyCommandResult(bool succeeded, DomainFailure failure)
    {
        Succeeded = succeeded;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public DomainFailure Failure { get; }
}

public readonly struct WasteFeedRequestResult
{
    public WasteFeedRequestResult(
        bool succeeded,
        string itemId,
        WasteFeedOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        ItemId = itemId ?? string.Empty;
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public string ItemId { get; }
    public WasteFeedOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }
}

public sealed class WasteProcessingOverview
{
    public int PlantWaste { get; set; }
    public int AnimalWaste { get; set; }
    public int MixedWaste { get; set; }
    public int ForbiddenWaste { get; set; }
    public int ToxicWaste { get; set; }
    public int ProcessingBills { get; set; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonWasteProcessingSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<WastePolicyData> policies = new List<WastePolicyData>();
}

public interface IWasteProcessingQuery
{
    int Version { get; }
    IReadOnlyList<WastePolicyData> Policies { get; }
    WastePolicyData GetPolicy(WasteOriginKind origin);
    WasteProcessingOverview CaptureOverview();
}

public interface IWastePolicyCommand
{
    WastePolicyCommandResult SetPolicy(WastePolicyData policy);
}

public interface IWasteFeedCommand
{
    WasteFeedRequestResult RequestDirectFeed(
        WildlifeDietType diet,
        Vector2Int destinationPosition,
        string destinationId);
}

public interface IWasteFeedCandidateQuery
{
    bool TryGetDirectFeedCandidate(
        WildlifeDietType diet,
        string destinationId,
        out WasteDirectFeedCandidate candidate,
        out DomainFailure failure);
}

public sealed class WasteProcessingRestoreCandidate
{
    internal WasteProcessingRestoreCandidate(WasteProcessingAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal WasteProcessingAggregateState State { get; }
}

public interface IWasteProcessingPersistence
{
    DungeonWasteProcessingSaveData Capture();
    WasteProcessingRestoreCandidate BuildRestore(
        DungeonWasteProcessingSaveData saveData);
    void Restore(WasteProcessingRestoreCandidate candidate);
}
