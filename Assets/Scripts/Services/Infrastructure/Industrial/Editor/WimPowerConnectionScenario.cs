#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

internal static class WimPowerConnectionScenario
{
    internal static IEnumerator Run(DungeonRuntimeLifetimeScope scope, BuildableObject generator,
        BuildableObject consumer, BuildableObject connector, List<string> report)
    {
        var query = scope.Container.Resolve<IPowerInfrastructureQuery>();
        var commands = scope.Container.Resolve<IPowerInfrastructureCommand>();
        var persistence = scope.Container.Resolve<IPowerInfrastructurePersistence>();
        var fluids = scope.Container.Resolve<IFluidInfrastructurePersistence>();
        var automation = scope.Container.Resolve<IAutomationInfrastructureCommand>();
        var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(game != null, "Main game manager missing.");
        bool wasPaused = game.isPause;
        game.isPause = true;
        try
        {
            yield return null;
            Require(automation.SetMode(consumer, AutomationMode.Automatic).Succeeded,
                "Cannot enable actual automatic power demand.");
            Require(query.IsPowered(consumer), "Main authored production facility not initially powered.");
            Require(query.TryGetNode(connector, out var connectorState) && connectorState.ConnectionEnabled
                && connectorState.HasControllableConnection && connectorState.MaximumThroughput > 0,
                "Authored default-open passage is not connected.");
            string before = JsonUtility.ToJson(persistence.Capture());
            string waterBefore = JsonUtility.ToJson(fluids.Capture());
            string generatorId = generator.RequirePersistentInstanceId().Value;
            string consumerId = consumer.RequirePersistentInstanceId().Value;
            string connectorId = connector.RequirePersistentInstanceId().Value;
            var tabs = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
            Require(tabs != null, "Main tab manager missing.");
            tabs.ToggleSelectButton(10);
            yield return null;

            Click("IndustryPowerConnection_" + consumerId);
            yield return null;
            Require(!query.IsPowered(consumer) && query.TryGetNode(consumer, out var off)
                && !off.ConnectionEnabled, "Main consumer disconnect did not stop actual power.");
            Require(JsonUtility.ToJson(fluids.Capture()) == waterBefore, "Power switch changed fluid authority.");
            report.Add("main-EventSystem-consumer-disconnect-stops-live-power-fluid-unchanged=PASS");

            string disconnected = JsonUtility.ToJson(persistence.Capture());
            Require(commands.SetConnectionEnabled(consumer, true).Succeeded && query.IsPowered(consumer),
                "Reconnection did not restore actual consumer supply.");
            persistence.Restore(persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(disconnected)));
            Require(!query.IsPowered(consumer) && JsonUtility.ToJson(persistence.Capture()) == disconnected,
                "Disconnected current-format state did not survive domain restore.");
            var invalid = JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(disconnected);
            invalid.nodes.Single(node => node.buildingInstanceId == consumerId).connectionState = 0;
            bool rejected = false;
            try { persistence.PrepareRestore(invalid); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && JsonUtility.ToJson(persistence.Capture()) == disconnected,
                "Missing connection state partially changed live state.");
            Require(!commands.SetConnectionEnabled(null, true).Succeeded
                && JsonUtility.ToJson(persistence.Capture()) == disconnected, "Invalid command changed live state.");
            report.Add("serialized-disconnection-restore-and-invalid-command-restore-atomicity=PASS");

            Refresh();
            Click("IndustryPowerConnection_" + consumerId);
            yield return null;
            Require(query.IsPowered(consumer), "Main UI reconnect did not resume production power.");
            Click("IndustryPowerConnection_" + connectorId);
            yield return null;
            Require(query.TryGetNode(connector, out var isolated) && !isolated.ConnectionEnabled && !isolated.Powered,
                "Main utility connector disconnect failed.");
            var isolatedNetwork = query.Networks.Single(network => network.NetworkId == isolated.NetworkId);
            Require(isolatedNetwork.ProductionPerSecond == 0 && isolatedNetwork.SuppliedPerSecond == 0,
                "Disconnected connector still shares generation or supply.");
            var fuelOff = persistence.Capture().nodes.Single(node => node.buildingInstanceId == generatorId);
            Refresh();
            var fuelAgain = persistence.Capture().nodes.Single(node => node.buildingInstanceId == generatorId);
            Require(fuelAgain.fuelSeconds == fuelOff.fuelSeconds
                && fuelAgain.nextFuelOperationSequence == fuelOff.nextFuelOperationSequence,
                "Read projection consumed fuel.");
            Require(!commands.SetConnectionEnabled(generator, false).Succeeded,
                "Generator without connection capability accepted a switch command.");
            report.Add("authored-utility-disconnect-isolates-node-and-noncontrollable-generator-rejected=PASS");
            Click("IndustryPowerConnection_" + connectorId);
            yield return null;
            Require(query.IsPowered(consumer), "Main connector reconnect did not retain production supply.");
            persistence.Restore(persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(before)));
            Require(JsonUtility.ToJson(persistence.Capture()) == before, "Original power state changed on round trip.");
            report.Add("main-reconnect-and-original-domain-state-round-trip=PASS");
            report.Add("connection-scope=authored-production-consumer-utility-connector-and-existing-full-fuel-custody-restore");
            report.Add("throughput-branch-boundaries=separate-pure-flow-contract-report");
        }
        finally { game.isPause = wasPaused; }
    }

    private static void Refresh() => UnityEngine.Object.FindObjectsByType<P0FeatureSurfacePanel>(FindObjectsSortMode.None)
        .First(panel => panel.gameObject.activeInHierarchy).Refresh();

    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable() && EventSystem.current != null, "Main UI action missing: " + name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
