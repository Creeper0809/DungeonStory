using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OffenseV17SaveSection : IDungeonSaveSection
{
    public const string Id = "offense.v17-world";

    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseTravelRuntime travel;
    private readonly IOffenseReturnSafetyRuntime returnSafety;
    private readonly IOffenseDecisionRuntime decisions;
    private readonly IOffenseBattleDirector battleDirector;
    private readonly IOffenseUrgentMitigationRuntime mitigation;
    private readonly IOffensePreparationService preparation;
    private readonly IOffenseExpeditionRuntimeProvider expeditions;

    public OffenseV17SaveSection(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseReturnSafetyRuntime returnSafety,
        IOffenseDecisionRuntime decisions,
        IOffenseBattleDirector battleDirector,
        IOffenseUrgentMitigationRuntime mitigation,
        IOffensePreparationService preparation,
        IOffenseExpeditionRuntimeProvider expeditions)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.returnSafety = returnSafety
            ?? throw new ArgumentNullException(nameof(returnSafety));
        this.decisions = decisions
            ?? throw new ArgumentNullException(nameof(decisions));
        this.battleDirector = battleDirector
            ?? throw new ArgumentNullException(nameof(battleDirector));
        this.mitigation = mitigation
            ?? throw new ArgumentNullException(nameof(mitigation));
        this.preparation = preparation
            ?? throw new ArgumentNullException(nameof(preparation));
        this.expeditions = expeditions
            ?? throw new ArgumentNullException(nameof(expeditions));
    }

    public string SectionId => Id;
    public int SectionVersion => 3;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        OffenseSaveSection.Id,
        OffenseRegionSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };

    public string Capture()
    {
        OffenseV17SaveData data = world.Capture();
        data.travelStates = new List<OffenseTravelStateData>(travel.Capture());
        data.returnSafety = new List<OffenseReturnSafetyStateData>(
            returnSafety.Capture());
        data.decisions = new List<OffenseDecisionStateData>(decisions.Capture());
        OffenseBattleDirectorStateData battle = battleDirector.Capture();
        data.battles = battle != null
            ? new List<OffenseBattleDirectorStateData> { battle }
            : new List<OffenseBattleDirectorStateData>();
        data.mitigationOrders =
            new List<OffenseUrgentMitigationOrderStateData>(
                mitigation.Capture());
        data.supplyPackages = new List<OffenseSupplyPackingStateData>(
            preparation.CapturePackingState());
        return JsonUtility.ToJson(data);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion < 1 || sectionVersion > SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        OffenseV17SaveData data = JsonUtility.FromJson<OffenseV17SaveData>(
            payloadJson ?? string.Empty) ?? new OffenseV17SaveData();
        world.Restore(data);
        preparation.RestorePackingState(
            sectionVersion >= 3
                ? data.supplyPackages
                : Array.Empty<OffenseSupplyPackingStateData>(),
            report);
        mitigation.Restore(
            sectionVersion >= 2
                ? data.mitigationOrders
                : Array.Empty<OffenseUrgentMitigationOrderStateData>());
        returnSafety.Restore(data.returnSafety);
        travel.Restore(data.travelStates);
        decisions.Restore(data.decisions);
        battleDirector.Clear();
        if (data.battles != null && data.battles.Count > 1)
        {
            throw new InvalidOperationException(
                "V17 supports only one active manual offense battle.");
        }

        if (data.battles != null && data.battles.Count == 1)
        {
            battleDirector.Restore(data.battles[0]);
        }

        if (expeditions.TryGetRuntime(out OffenseExpeditionRuntime runtime))
        {
            runtime.ResumeRestoredV17State();
        }
    }
}
