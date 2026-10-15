using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class EvolutionCatalystDropObserverDebugScenarios
{
    public static void RunOrThrow()
    {
        ResourceItemDefinitionCatalog itemDefinitions = new(
            Resources.LoadAll<ItemDefinitionSO>(
                ItemDefinitionSO.UnifiedResourcePath));
        ResourceDungeonItemCatalogProvider dungeonItems =
            new(itemDefinitions);
        using WorldItemStackRuntime items = PhysicalItemDebugScenarios
            .CreateRuntimeForCrossDomainFixture(
                dungeonItems,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        GameEventBus events = new();
        ControlledDropZones dropZones = new();
        EvolutionCatalystDropRuntime runtime = new(
            events,
            items,
            dropZones,
            new RandomStreamProvider(105017));
        EvolutionCatalystDropAlertObserver observer = new(
            events,
            itemDefinitions);
        runtime.Start();
        runtime.Start();

        int factCount = 0;
        EvolutionCatalystDroppedEvent? lastFact = null;
        int alertCount = 0;
        EventAlertRequest lastAlert = null;
        using IDisposable factSubscription =
            events.Subscribe<EvolutionCatalystDroppedEvent>(value =>
            {
                factCount++;
                lastFact = value;
            });
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                EventAlertRequest request = value.request;
                if (request != null
                    && string.Equals(
                        request.Category,
                        "진화",
                        StringComparison.Ordinal))
                {
                    alertCount++;
                    lastAlert = request;
                }
            });

        try
        {
            observer.Initialize();
            observer.Initialize();

            Require(
                runtime.TryGrantOffenseCatalyst(
                    0f,
                    out string grantedItemId,
                    out string grantFailure),
                "The actual catalyst grant failed: " + grantFailure);
            Require(
                factCount == 1
                && lastFact.HasValue
                && lastFact.Value.ItemId == grantedItemId
                && lastFact.Value.Quantity == 1
                && lastFact.Value.Position == dropZones.ExpeditionPosition
                && lastFact.Value.Origin
                    == EvolutionCatalystDropOrigin.Expedition,
                "The committed catalyst drop did not publish its exact neutral fact.");
            WorldItemStackSnapshot grantedStack = items.GetAllStacks()
                .Single(stack =>
                    stack.ItemId == grantedItemId
                    && stack.Position == dropZones.ExpeditionPosition);
            Require(
                grantedStack.Quantity == 1
                && grantedStack.State == WorldItemStackState.Loose,
                "The catalyst grant did not create exactly one loose physical item.");
            string grantedDisplayName = itemDefinitions
                .GetRequired((ItemDefinitionId)grantedItemId)
                .DisplayName;
            Require(
                alertCount == 1
                && lastAlert != null
                && lastAlert.Title == "원정 촉매 회수"
                && lastAlert.Detail ==
                    $"{grantedDisplayName}이 하차장에 도착했습니다."
                && lastAlert.Importance == EventAlertImportance.Medium,
                "The expedition catalyst alert payload changed.");

            observer.Dispose();
            events.Publish(lastFact.Value);
            Require(
                alertCount == 1,
                "A disposed catalyst alert observer remained subscribed.");
            observer.Initialize();
            observer.Initialize();
            events.Publish(lastFact.Value);
            Require(
                alertCount == 2,
                "The reinitialized catalyst observer did not subscribe once.");

            dropZones.HasExpeditionDropoff = false;
            int quantityBeforeReject = TotalQuantity(items);
            int factsBeforeReject = factCount;
            int alertsBeforeReject = alertCount;
            Require(
                !runtime.TryGrantOffenseCatalyst(
                    0f,
                    out string missingDropoffItemId,
                    out string missingDropoffFailure)
                && missingDropoffItemId.Length == 0
                && !string.IsNullOrWhiteSpace(missingDropoffFailure),
                "Missing-dropoff catalyst request did not reject explicitly.");
            Require(
                TotalQuantity(items) == quantityBeforeReject
                && factCount == factsBeforeReject
                && alertCount == alertsBeforeReject,
                "Missing-dropoff rejection changed items or published a fact.");

            dropZones.HasExpeditionDropoff = true;
            Require(
                !runtime.TryGrantOffenseCatalyst(
                    525f,
                    out string invalidProgressionItemId,
                    out string invalidProgressionFailure)
                && invalidProgressionItemId.Length == 0
                && !string.IsNullOrWhiteSpace(invalidProgressionFailure),
                "Invalid-progression catalyst request did not reject explicitly.");
            Require(
                TotalQuantity(items) == quantityBeforeReject
                && factCount == factsBeforeReject
                && alertCount == alertsBeforeReject,
                "Invalid-progression rejection changed items or published a fact.");

            int quantityBeforeFault = TotalQuantity(items);
            int factsBeforeFault = factCount;
            int alertsBeforeFault = alertCount;
            const string FaultMessage = "catalyst-alert-display-failure";
            const string ExpectedWarning =
                "catalyst-drop-observation:InvalidOperationException:"
                + FaultMessage;
            int warningCount = 0;
            Application.LogCallback warningCapture =
                (condition, _, type) =>
                {
                    if (type == LogType.Warning
                        && string.Equals(
                            condition,
                            ExpectedWarning,
                            StringComparison.Ordinal))
                    {
                        warningCount++;
                    }
                };
            Application.logMessageReceived += warningCapture;
            try
            {
                using IDisposable throwingDisplay =
                    events.Subscribe<EventAlertRequestedEvent>(_ =>
                        throw new InvalidOperationException(FaultMessage));
                Require(
                    runtime.TryGrantDefenseCatalyst(
                        0f,
                        out string defenseItemId,
                        out string defenseFailure),
                    "Display failure changed the committed grant result: "
                    + defenseFailure);
                Require(
                    TotalQuantity(items) == quantityBeforeFault + 1
                    && factCount == factsBeforeFault + 1
                    && alertCount == alertsBeforeFault + 1,
                    "Display failure repeated or rolled back the committed catalyst drop.");
                Require(
                    lastFact.HasValue
                    && lastFact.Value.ItemId == defenseItemId
                    && lastFact.Value.Quantity == 1
                    && lastFact.Value.Position == dropZones.DeliveryPosition
                    && lastFact.Value.Origin
                        == EvolutionCatalystDropOrigin.Defense,
                    "The fault-path catalyst fact lost its committed evidence.");
                WorldItemStackSnapshot defenseStack = items.GetAllStacks()
                    .Single(stack =>
                        stack.ItemId == defenseItemId
                        && stack.Position == dropZones.DeliveryPosition);
                Require(
                    defenseStack.Quantity == 1
                    && lastAlert.Title == "침공 촉매 회수"
                    && lastAlert.Detail ==
                        $"{itemDefinitions.GetRequired((ItemDefinitionId)defenseItemId).DisplayName}이 하차장에 남았습니다."
                    && lastAlert.Importance == EventAlertImportance.Medium,
                    "The fault-path physical drop or defense alert payload changed.");
            }
            finally
            {
                Application.logMessageReceived -= warningCapture;
            }

            Require(
                warningCount == 1,
                "The swallowed display failure did not emit exactly one diagnostic warning.");
        }
        finally
        {
            observer.Dispose();
            runtime.Dispose();
        }
    }

    private static int TotalQuantity(WorldItemStackRuntime items) =>
        items.GetAllStacks().Sum(stack => stack.Quantity);

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }

    private sealed class ControlledDropZones : IWorldDropZoneQuery
    {
        public bool HasExpeditionDropoff { get; set; } = true;
        public bool HasDeliveryDropoff { get; set; } = true;
        public Vector2Int ExpeditionPosition { get; } = new(7, 11);
        public Vector2Int DeliveryPosition { get; } = new(13, 17);

        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = DeliveryPosition;
            return HasDeliveryDropoff;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = ExpeditionPosition;
            return HasExpeditionDropoff;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }
}
