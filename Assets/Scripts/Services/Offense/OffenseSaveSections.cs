using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// The only V18 save authority for expedition preparation, travel, decisions,
/// battle, return, regional progress, and reward history.
/// </summary>
public sealed class OffenseAggregateSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonOffenseAggregateSaveData,
        OffenseAggregateRuntimeRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "offense.aggregate";

    private readonly IOffenseSaveService expedition;
    private readonly IOffenseCampaignRuntime campaign;
    private readonly OffenseExpeditionResultPublicationRuntime resultPublications;
    private readonly OffenseNodePublicationRuntime nodePublications;
    private readonly OffenseBattleCommandPublicationRuntime battleCommandPublications;
    private readonly OffenseWorldStateSaveCodec world;
    private readonly IOffenseRegionRuntime regions;
    private readonly IOffenseReturnArrivalRuntime returnArrivals;
    private readonly OffenseAggregateAuthoredReferenceValidator
        authoredReferences;
    public OffenseAggregateSaveSection(
        IOffenseSaveService expedition,
        IOffenseCampaignRuntime campaign,
        OffenseWorldStateSaveCodec world,
        IOffenseRegionRuntime regions,
        IOffenseReturnArrivalRuntime returnArrivals,
        OffenseAggregateAuthoredReferenceValidator authoredReferences,
        OffenseExpeditionResultPublicationRuntime resultPublications,
        OffenseNodePublicationRuntime nodePublications,
        OffenseBattleCommandPublicationRuntime battleCommandPublications)
    {
        this.expedition = expedition
            ?? throw new ArgumentNullException(nameof(expedition));
        this.resultPublications = resultPublications ?? throw new ArgumentNullException(nameof(resultPublications));
        this.nodePublications = nodePublications ?? throw new ArgumentNullException(nameof(nodePublications));
        this.battleCommandPublications = battleCommandPublications ?? throw new ArgumentNullException(nameof(battleCommandPublications));
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.returnArrivals = returnArrivals
            ?? throw new ArgumentNullException(nameof(returnArrivals));
        this.authoredReferences = authoredReferences
            ?? throw new ArgumentNullException(nameof(authoredReferences));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonOffenseAggregateSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        CharacterWorldSaveSection.Id,
        CharacterBodyHealthSaveSection.Id,
        WildlifeSaveSection.Id,
        CaptivitySaveSection.Id,
        ExteriorActivitySaveSection.Id
    };

    protected override DungeonOffenseAggregateSaveData CapturePayload()
    {
        var captured = new DungeonOffenseAggregateSaveData
        {
            version = DungeonOffenseAggregateSaveData.CurrentVersion,
            campaign = campaign.Capture(),
            expedition = expedition.Capture(),
            resultPublications = resultPublications.Capture(),
            nodePublications = nodePublications.Capture(),
            battleCommandPublications = battleCommandPublications.Capture(),
            world = world.CaptureState(),
            regions = regions.Capture(),
            returnArrivals = returnArrivals.Capture()
        };
        OffenseArrivalSettlementRuntime.ProjectTerminalReceipts(captured.expedition, captured.returnArrivals);
        return captured;
    }

    protected override void ValidateRawPayload(string payloadJson)
    {
        var root = JObject.Parse(payloadJson);
        ValidateResultPublicationJson(root["resultPublications"], typeof(OffenseExpeditionResultPublicationSaveData));
        ValidateResultPublicationJson(root["nodePublications"], typeof(OffenseNodePublicationSaveData));
        ValidateResultPublicationJson(root["battleCommandPublications"], typeof(OffenseBattleCommandPublicationSaveData));
        if (root["returnArrivals"]?["arrivals"] is not JArray arrivals)
            throw new InvalidOperationException("offense-arrival-json-list-missing");
        foreach (var arrival in arrivals)
            ValidateResultPublicationJson(arrival["settlement"], typeof(OffenseArrivalSettlementObservationData));
        var campaignData = root["campaign"] as JObject;
        if (campaignData?["version"]?.Type != JTokenType.Integer
            || campaignData["version"].Value<int>() != DungeonOffenseCampaignSaveData.CurrentVersion)
            throw new InvalidOperationException("offense-campaign-v2-json-invalid");
        var observation = campaignData["truthObservation"] as JObject;
        if (observation == null || observation["hasFact"]?.Type != JTokenType.Boolean
            || observation["pending"]?.Type != JTokenType.Boolean || observation["notificationPending"]?.Type != JTokenType.Boolean
            || observation["publicationKey"]?.Type != JTokenType.String || observation["fact"] is not JObject fact)
            throw new InvalidOperationException("offense-truth-observation-json-invalid");
        foreach (string name in new[] { "targetId", "targetDisplayName", "truthTitle", "truthText" })
            if (fact[name]?.Type != JTokenType.String) throw new InvalidOperationException("offense-truth-fact-json-missing:" + name);
        if (fact["ownerRevision"]?.Type != JTokenType.Integer || fact["absoluteDay"]?.Type != JTokenType.Integer)
            throw new InvalidOperationException("offense-truth-fact-json-number-invalid");
    }

    private static void ValidateResultPublicationJson(JToken token, Type type)
    {
        bool valid;
        if (type == typeof(string)) valid = token?.Type == JTokenType.String;
        else if (type == typeof(bool)) valid = token?.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token?.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token?.Type == JTokenType.Integer;
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray array) throw new InvalidOperationException("offense-result-json-list-missing:" + type.Name);
            foreach (var row in array) ValidateResultPublicationJson(row, type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject value) throw new InvalidOperationException("offense-result-json-object-missing:" + type.Name);
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                ValidateResultPublicationJson(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("offense-result-json-field-invalid:" + type.Name);
    }

    protected override void NormalizeRestorePayload(
        DungeonOffenseAggregateSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonOffenseAggregateSaveData payload)
    {
        OffenseAggregateRestorePlan plan =
            OffenseAggregateSaveValidation.BuildRestorePlan(payload);
        authoredReferences.Validate(plan);
    }

    protected override OffenseAggregateRuntimeRestoreCandidate
        BuildRestoreCandidate(DungeonOffenseAggregateSaveData payload)
    {
        OffenseAggregateRestorePlan plan =
            OffenseAggregateSaveValidation.BuildRestorePlan(payload);
        authoredReferences.Validate(plan);
        DungeonOffenseAggregateSaveData data = plan.Payload;
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        OffenseCampaignRestoreCandidate campaignCandidate =
            campaign.BuildRestoreCandidate(data.campaign);
        OffenseExpeditionRestoreCandidate expeditionCandidate =
            expedition.BuildRestoreCandidate(
                data.expedition,
                report,
                data.regions.regions,
                campaignCandidate.State);
        OffenseWorldRuntimeRestoreCandidate worldCandidate =
            world.BuildRestoreCandidate(data.world, report);
        OffenseRegionRuntime concreteRegions = regions as OffenseRegionRuntime
            ?? throw new InvalidOperationException(
                "Offense aggregate restore requires the canonical region runtime.");
        OffenseRegionRestoreCandidate regionCandidate =
            concreteRegions.PrepareRestore(data.regions);
        OffenseReturnArrivalRestoreCandidate returnArrivalCandidate =
            returnArrivals.BuildRestoreCandidate(data.returnArrivals, report);
        if (!report.Success
            || expeditionCandidate == null
            || campaignCandidate == null
            || worldCandidate == null
            || regionCandidate == null
            || returnArrivalCandidate == null)
        {
            throw new InvalidOperationException(
                "Offense aggregate candidate construction failed: "
                + string.Join(" | ", report.Errors));
        }
        return new OffenseAggregateRuntimeRestoreCandidate(
            campaignCandidate,
            expeditionCandidate,
            regionCandidate,
            worldCandidate,
            returnArrivalCandidate,
            OffenseExpeditionResultPublicationRuntime.ValidateAndClone(data.resultPublications),
            OffenseNodePublicationRuntime.ValidateAndClone(data.nodePublications),
            OffenseBattleCommandPublicationRuntime.ValidateAndClone(data.battleCommandPublications));
    }

    protected override void PublishRestoreCandidate(
        OffenseAggregateRuntimeRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));

        ((OffenseRegionRuntime)regions).PublishRestore(candidate.Regions);
        campaign.PublishRestoreCandidate(candidate.Campaign);
        world.PublishRestoreCandidate(candidate.World);
        expedition.PublishRestoreCandidate(candidate.Expedition);
        returnArrivals.PublishRestoreCandidate(candidate.ReturnArrivals);
        resultPublications.Restore(candidate.ResultPublications);
        nodePublications.Restore(candidate.NodePublications);
        battleCommandPublications.Restore(candidate.BattleCommandPublications);
    }
}

public sealed class OffenseAggregateRuntimeRestoreCandidate :
    IDungeonRestoreReportContributor
{
    internal OffenseAggregateRuntimeRestoreCandidate(
        OffenseCampaignRestoreCandidate campaign,
        OffenseExpeditionRestoreCandidate expedition,
        OffenseRegionRestoreCandidate regions,
        OffenseWorldRuntimeRestoreCandidate world,
        OffenseReturnArrivalRestoreCandidate returnArrivals,
        OffenseExpeditionResultPublicationSaveData resultPublications,
        OffenseNodePublicationSaveData nodePublications,
        OffenseBattleCommandPublicationSaveData battleCommandPublications)
    {
        Campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        Expedition = expedition
            ?? throw new ArgumentNullException(nameof(expedition));
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        World = world ?? throw new ArgumentNullException(nameof(world));
        ReturnArrivals = returnArrivals
            ?? throw new ArgumentNullException(nameof(returnArrivals));
        ResultPublications = resultPublications ?? throw new ArgumentNullException(nameof(resultPublications));
        NodePublications = nodePublications ?? throw new ArgumentNullException(nameof(nodePublications));
        BattleCommandPublications = battleCommandPublications ?? throw new ArgumentNullException(nameof(battleCommandPublications));
    }

    internal OffenseCampaignRestoreCandidate Campaign { get; }
    internal OffenseExpeditionRestoreCandidate Expedition { get; }
    internal OffenseRegionRestoreCandidate Regions { get; }
    internal OffenseWorldRuntimeRestoreCandidate World { get; }
    internal OffenseReturnArrivalRestoreCandidate ReturnArrivals { get; }
    internal OffenseExpeditionResultPublicationSaveData ResultPublications { get; }
    internal OffenseNodePublicationSaveData NodePublications { get; }
    internal OffenseBattleCommandPublicationSaveData BattleCommandPublications { get; }

    public void RecordRestoreResult(DungeonGameRestoreReport report)
    {
        (report ?? throw new ArgumentNullException(nameof(report)))
            .RecordRestoredExpeditions(
                Expedition.ActiveExpeditions?.Count ?? 0);
    }
}
