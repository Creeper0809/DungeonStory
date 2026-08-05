using System;
using System.Collections.Generic;

public sealed class OffenseWorldRuntimeRestoreCandidate
{
    internal OffenseHexWorldRestoreCandidate World { get; set; }
    internal DungeonOffensePreparationService.PackingRestoreCandidate Preparation
        { get; set; }
    internal OffenseUrgentMitigationRestoreCandidate Mitigation { get; set; }
    internal OffenseFieldMedicalRestoreCandidate FieldMedical { get; set; }
    internal OffenseDictionaryRestoreCandidate<OffenseReturnSafetyStateData>
        ReturnSafety { get; set; }
    internal OffenseDictionaryRestoreCandidate<OffenseTravelStateData> Travel
        { get; set; }
    internal OffenseDictionaryRestoreCandidate<OffenseDecisionStateData> Decisions
        { get; set; }
    internal OffenseBattleDirectorRestoreCandidate BattleDirector { get; set; }
}

/// <summary>
/// Serializes the authored strategic-world modules for the expedition aggregate.
/// This is a codec only; it is deliberately not a separately registered save section.
/// </summary>
public sealed class OffenseWorldStateSaveCodec
{
    public const int CurrentVersion = OffenseWorldSaveData.CurrentVersion;
    public int SectionVersion => CurrentVersion;

    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseTravelRuntime travel;
    private readonly IOffenseReturnSafetyRuntime returnSafety;
    private readonly IOffenseDecisionRuntime decisions;
    private readonly IOffenseBattleDirector battleDirector;
    private readonly IOffenseUrgentMitigationRuntime mitigation;
    private readonly IOffensePreparationService preparation;
    private readonly OffenseExpeditionRuntime expeditions;
    private readonly IOffenseFieldMedicalRuntime fieldMedical;

    public OffenseWorldStateSaveCodec(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseReturnSafetyRuntime returnSafety,
        IOffenseDecisionRuntime decisions,
        IOffenseBattleDirector battleDirector,
        IOffenseUrgentMitigationRuntime mitigation,
        IOffensePreparationService preparation,
        OffenseSceneRuntimeReferences offenseRuntimes,
        IOffenseFieldMedicalRuntime fieldMedical)
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
        expeditions = (offenseRuntimes
                ?? throw new ArgumentNullException(nameof(offenseRuntimes)))
            .Expedition
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseWorldStateSaveCodec)} requires a loaded {nameof(OffenseExpeditionRuntime)}.");
        this.fieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
    }

    public string Capture()
    {
        return UnityEngine.JsonUtility.ToJson(CaptureState());
    }

    public OffenseWorldSaveData CaptureState()
    {
        OffenseWorldSaveData data = world.Capture();
        data.travelStates = new List<OffenseTravelStateData>(travel.Capture());
        data.returnSafety = new List<OffenseReturnSafetyStateData>(
            returnSafety.Capture());
        data.decisions = new List<OffenseDecisionStateData>(decisions.Capture());
        OffenseBattleDirectorStateData battle = battleDirector.Capture();
        data.battles = battle != null
            ? new List<OffenseBattleDirectorStateData> { battle }
            : new List<OffenseBattleDirectorStateData>();
        data.mitigationOrders =
            new List<OffenseUrgentMitigationOrderStateData>(mitigation.Capture());
        data.supplyPackages = new List<OffenseSupplyPackingStateData>(
            preparation.CapturePackingState());
        fieldMedical.Capture(data);
        return data;
    }

    public OffenseWorldRuntimeRestoreCandidate BuildRestoreCandidate(
        OffenseWorldSaveData data,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (data == null || data.version != OffenseWorldSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported offense world payload version {data?.version.ToString() ?? "null"}; expected {OffenseWorldSaveData.CurrentVersion}.");
        }

        OffenseHexWorldSimulation concreteWorld = world as OffenseHexWorldSimulation
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical world simulation.");
        OffenseReturnSafetyRuntime concreteReturnSafety =
            returnSafety as OffenseReturnSafetyRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical return-safety runtime.");
        OffenseTravelRuntime concreteTravel = travel as OffenseTravelRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical travel runtime.");
        OffenseDecisionRuntime concreteDecisions = decisions as OffenseDecisionRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical decision runtime.");
        OffenseBattleDirector concreteBattle = battleDirector as OffenseBattleDirector
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical strategic battle director.");
        OffenseUrgentMitigationRuntime concreteMitigation =
            mitigation as OffenseUrgentMitigationRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical mitigation runtime.");
        DungeonOffensePreparationService concretePreparation =
            preparation as DungeonOffensePreparationService
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical preparation runtime.");
        OffenseFieldMedicalRuntime concreteFieldMedical =
            fieldMedical as OffenseFieldMedicalRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical field-medical runtime.");

        OffenseHexWorldRestoreCandidate worldCandidate =
            concreteWorld.PrepareRestore(data);
        return new OffenseWorldRuntimeRestoreCandidate
        {
            World = worldCandidate,
            Preparation = concretePreparation.PreparePackingRestore(
                data.supplyPackages),
            Mitigation = concreteMitigation.PrepareRestore(
                data.mitigationOrders),
            FieldMedical = concreteFieldMedical.PrepareRestore(data),
            ReturnSafety = concreteReturnSafety.PrepareRestore(
                data.returnSafety),
            Travel = concreteTravel.PrepareRestore(
                data.travelStates,
                worldCandidate.Tiles),
            Decisions = concreteDecisions.PrepareRestore(data.decisions),
            BattleDirector = concreteBattle.PreparePersistentState(
                data.battles.Count == 1 ? data.battles[0] : null)
        };
    }

    public void PublishRestoreCandidate(
        OffenseWorldRuntimeRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        ((OffenseHexWorldSimulation)world).PublishRestore(candidate.World);
        ((DungeonOffensePreparationService)preparation).PublishPackingRestore(
            candidate.Preparation);
        ((OffenseUrgentMitigationRuntime)mitigation).PublishRestore(
            candidate.Mitigation);
        ((OffenseFieldMedicalRuntime)fieldMedical).PublishRestore(
            candidate.FieldMedical);
        ((OffenseReturnSafetyRuntime)returnSafety).PublishRestore(
            candidate.ReturnSafety);
        ((OffenseTravelRuntime)travel).PublishRestore(candidate.Travel);
        ((OffenseDecisionRuntime)decisions).PublishRestore(candidate.Decisions);
        ((OffenseBattleDirector)battleDirector).PublishPersistentState(
            candidate.BattleDirector);
    }
}
