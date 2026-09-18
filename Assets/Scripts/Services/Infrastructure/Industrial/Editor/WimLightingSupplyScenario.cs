#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using VContainer;

// Runs within the existing main industrial world checkpoint and atomic cleanup.
internal static class WimLightingSupplyScenario
{
    internal static IEnumerator Run(DungeonRuntimeLifetimeScope scope, BuildableObject electric,
        BuildableObject torch, List<string> report)
    {
        var power = scope.Container.Resolve<IPowerInfrastructureQuery>();
        var field = scope.Container.Resolve<IEnvironmentalFieldQuery>();
        var survival = scope.Container.Resolve<ISurvivalEnvironmentQuery>();
        var refuel = scope.Container.Resolve<ISurvivalRefuelCompletionCommand>();
        var fuelPlans = scope.Container.Resolve<ISurvivalRefuelSupplyQuery>();
        var saved = scope.Container.Resolve<ISurvivalFoodPersistence>();
        var events = scope.Container.Resolve<IGameEventBus>();
        var physical = scope.Container.Resolve<IWorldItemStackRuntime>();
        string electricId = electric.RequirePersistentInstanceId().Value;
        bool Visible(BuildableObject building) => building.GetComponentInChildren<Light2D>(true).enabled;
        float Level(BuildableObject building)
        {
            Require(field.TryGetCell(building.centerPos, out var cell), "Actual light cell missing.");
            return cell.LightLevel;
        }
        torch.enabled = false;
        yield return Wait(() => power.IsPowered(electric) && Visible(electric) && !Visible(torch), "Authored arc lamp did not light.");
        float poweredLevel = Level(electric);
        var tabs = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
        Require(tabs != null, "Main tabs missing.");
        tabs.ToggleSelectButton(10);
        yield return null;
        Click("IndustryPowerConnection_" + electricId);
        yield return Wait(() => !power.IsPowered(electric) && !Visible(electric)
            && Level(electric) < poweredLevel - 0.01f, "Outage left visual or actual light contribution active.");
        float outageLevel = Level(electric);
        Click("IndustryPowerConnection_" + electricId);
        yield return Wait(() => power.IsPowered(electric) && Visible(electric)
            && Level(electric) > outageLevel, "Reconnected arc lamp did not restore field and visual light.");
        report.Add($"authored-I15-main-switch-field-Light2D={poweredLevel:0.###}->{outageLevel:0.###}->{Level(electric):0.###}");
        Click("IndustryPowerConnection_" + electricId);
        yield return Wait(() => !Visible(electric), "Could not isolate fuel light from electric light.");

        Require(torch.FacilityState.pendingFuel.phase == (int)FacilityFuelCommitPhase.None,
            "Lighting fixture cannot overwrite pending physical fuel ownership.");
        // A controlled empty facility checkpoint, not a global warehouse drain.
        // The enclosing verifier owns restoration of the full fixture world.
        torch.RestoreFacilityState(new FacilityRuntimeState());
        torch.enabled = true;
        yield return Wait(() => !survival.HasFuelSupply(torch) && !Visible(torch), "Empty authored torch still emits.");
        float emptyLevel = Level(torch);

        int quantity = torch.BuildingData.GetAbility<BuildingFuelConsumerAbility>().fuelPerRefuel;
        refuel.TryEnsureRefuelSupply(null, torch, out _, out _);
        Require(fuelPlans.TryGetRefuelSupplyPlan(torch, out SurvivalFacilityFuelSupplyPlan plan)
                && plan.RequiredQuantity == quantity,
            "Actual authored fuel destination/item plan unavailable.");
        int CountFuel() => physical.GetAllStacks().Where(stack => stack.ItemId == plan.ItemId).Sum(stack => stack.Quantity);
        int priorCount = CountFuel();
        Require(physical.SpawnItemAt(plan.ItemId, quantity, torch.centerPos,
                WorldItemStackState.FacilityBuffer, plan.DestinationId, out int added)
                && added == quantity,
            "Controlled arrived fuel lot was not admitted by the real facility owner.");
        Require(refuel.TryApplyRefuelWork(null, torch, out int consumed, out var failure)
            && consumed == quantity, "Physical arrived-only refuel failed: " + failure.Code);
        Require(CountFuel() == priorCount && survival.HasFuelSupply(torch),
            "Local charge did not consume exactly the arrived lot.");
        yield return Wait(() => Visible(torch) && Level(torch) > emptyLevel, "Actual refuel did not recover field and Light2D.");
        report.Add($"authored-E01-physical-refuel={quantity};field={emptyLevel:0.###}->{Level(torch):0.###};arrival=controlled-physical-fixture");

        // Other live consumers may use the same fuel during the preceding frame wait.
        // Replay/date assertions compare one synchronous observation window only.
        int beforeReplayCount = CountFuel();
        FacilityRuntimeStateModule module = new(torch);
        string coverage = module.CaptureState();
        Require(module.TryRestoreState(module.CurrentVersion, coverage, out string restoreFailure)
                && survival.HasFuelSupply(torch) && module.CaptureState() == coverage,
            "Local fuel current module restore failed: " + restoreFailure);
        Require(refuel.TryApplyRefuelWork(null, torch, out int replayConsumed, out _)
                && replayConsumed == 0 && CountFuel() == beforeReplayCount && module.CaptureState() == coverage,
            "Already charged refuel replay changed local state or stock.");
        float beforeDay = survival.GetRemainingFuelGameSeconds(torch);
        events.Publish(new OperatingDayStartedEvent(Math.Max(1, saved.Capture().lastProcessedDay + 1)));
        Require(Mathf.Approximately(survival.GetRemainingFuelGameSeconds(torch), beforeDay)
                && CountFuel() == beforeReplayCount,
            "Date boundary consumed global stock or expired remaining local operating time.");
        yield return Wait(() => survival.GetRemainingFuelGameSeconds(torch) < beforeDay - 0.25f,
            "Active authored light did not consume actual running game-time.");
        report.Add("local-fuel-current-module/replay/date-retention/running-debit=PASS");
        report.Add("scope=authored-main-I15-E01-real-power-and-light;controlled-arrived-fuel;natural-haul/full-world-restore/full-exhaustion=NOT_RUN");
    }

    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable() && EventSystem.current != null, "Main UI action missing: " + name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator Wait(Func<bool> condition, string message)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < 30f) yield return null;
        Require(condition(), message);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
