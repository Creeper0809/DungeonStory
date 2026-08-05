using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class RegionalSupplyContractLogisticsDependencies
{
    public RegionalSupplyContractLogisticsDependencies(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZones,
        ICharacterWorldQuery characterWorld)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        DropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        CharacterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IResourceEconomyContentCatalog Catalog { get; }
    public IWorldItemStackRuntime ItemRuntime { get; }
    public IWorldDropZoneQuery DropZones { get; }
    public ICharacterWorldQuery CharacterWorld { get; }
}

public sealed class RegionalSupplyContractSessionDependencies
{
    public RegionalSupplyContractSessionDependencies(
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IWorkforceReplanService workforce)
    {
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        Money = money ?? throw new ArgumentNullException(nameof(money));
        GameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
    }

    public IGameSessionStateProvider GameDataProvider { get; }
    public IGameMoneyAccount Money { get; }
    public IGameEventBus GameEventBus { get; }
    public IGameClock GameClock { get; }
    public IWorkforceReplanService Workforce { get; }
}

public sealed class RegionalSupplyContractApplicationAdapter :
    IRegionalSupplyContractWorldQuery,
    IRegionalSupplyContractCommandPort,
    IRegionalSupplyContractSessionPort
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IGameMoneyAccount money;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IWorkforceReplanService workforce;
    private readonly BlueprintResearchRuntime research;

    public RegionalSupplyContractApplicationAdapter(
        RegionalSupplyContractLogisticsDependencies logistics,
        RegionalSupplyContractSessionDependencies session,
        ProgressionSceneRuntimeReferences progressionRuntimes)
    {
        logistics = logistics ?? throw new ArgumentNullException(nameof(logistics));
        session = session ?? throw new ArgumentNullException(nameof(session));
        catalog = logistics.Catalog;
        itemRuntime = logistics.ItemRuntime;
        dropZones = logistics.DropZones;
        characterWorld = logistics.CharacterWorld;
        gameDataProvider = session.GameDataProvider;
        money = session.Money;
        gameEventBus = session.GameEventBus;
        gameClock = session.GameClock;
        workforce = session.Workforce;
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(RegionalSupplyContractRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
    }

    public IReadOnlyList<RegionalSupplyContractItemSnapshot> Items =>
        catalog.Items
            .Where(item => item != null)
            .Select(ToSnapshot)
            .ToArray();

    public int ResidentPopulation =>
        characterWorld.Characters.Count(IsResident);

    public int CompletedResearchCount =>
        research.State.Projects.CompletedProjectIds.Count;

    public bool IsPaused => gameClock.IsPaused;
    public float Time => gameClock.Time;

    public bool TryGetItem(
        string itemId,
        out RegionalSupplyContractItemSnapshot item)
    {
        if (catalog.TryGetItem(itemId, out ResourceItemDefinitionSO definition))
        {
            item = ToSnapshot(definition);
            return true;
        }

        item = null;
        return false;
    }

    public bool IsResearchCompleted(string researchId)
    {
        return research.State.Projects.IsCompleted(
            new ResearchProjectId(researchId));
    }

    public bool TryGetDeliveryDropoff(out Vector2Int dropoff) =>
        dropZones.TryGetDeliveryDropoff(out dropoff);

    public int CountAtDestination(
        string itemId,
        string destinationId,
        bool deliveredOnly)
    {
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && (!deliveredOnly
                    || stack.State == WorldItemStackState.FacilityBuffer))
            .Sum(stack => stack.Quantity);
    }

    public bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int dropoff,
        string destinationId,
        out int requested)
    {
        return itemRuntime.TryRequestItemDelivery(
            itemId,
            amount,
            dropoff,
            destinationId,
            out requested,
            out _);
    }

    public bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        return itemRuntime.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        return itemRuntime.ReleaseStacksByDestination(
            destinationId,
            releasePosition);
    }

    public void PrioritizeDestination(string destinationId)
    {
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            {
                itemRuntime.PrioritizeHaul(stack.StackId);
            }
        }
    }

    public void RequestHauler() =>
        workforce.RequestOneHaulerToReplan(forceInterrupt: false);

    public void AddContractIncome(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        money.Add(
            amount,
            new EconomyTransactionContext(
                EconomyTransactionKind.ContractIncome,
                "regional-supply",
                description: "지역 공급 계약"));
    }

    public bool TryGetCurrentDay(out int day)
    {
        if (gameDataProvider.TryGetSessionState(out GameSessionState data)
            && data?.day != null)
        {
            day = data.day.Value;
            return true;
        }

        day = 1;
        return false;
    }

    public IDisposable SubscribeDayStarted(Action<int> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return gameEventBus.Subscribe<OperatingDayStartedEvent>(
            eventType => handler(eventType.day));
    }

    private static RegionalSupplyContractItemSnapshot ToSnapshot(
        ResourceItemDefinitionSO item)
    {
        return new RegionalSupplyContractItemSnapshot(
            item.ItemId,
            item.DisplayName,
            item.Kind,
            item.UnitPrice,
            item.RequiredResearchId);
    }

    private static bool IsResident(CharacterActor actor)
    {
        return actor != null
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && (actor.IsOwner
                || StaffDiscontentService.IsTrackableStaff(actor));
    }
}
