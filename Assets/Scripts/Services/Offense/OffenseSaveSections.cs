using System;
using System.Collections.Generic;

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
        OffenseAggregateAuthoredReferenceValidator authoredReferences)
    {
        this.expedition = expedition
            ?? throw new ArgumentNullException(nameof(expedition));
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
        return new DungeonOffenseAggregateSaveData
        {
            version = DungeonOffenseAggregateSaveData.CurrentVersion,
            campaign = campaign.Capture(),
            expedition = expedition.Capture(),
            world = world.CaptureState(),
            regions = regions.Capture(),
            returnArrivals = returnArrivals.Capture()
        };
    }

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
            returnArrivalCandidate);
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
    }
}

public sealed class OffenseAggregateRuntimeRestoreCandidate
{
    internal OffenseAggregateRuntimeRestoreCandidate(
        OffenseCampaignRestoreCandidate campaign,
        OffenseExpeditionRestoreCandidate expedition,
        OffenseRegionRestoreCandidate regions,
        OffenseWorldRuntimeRestoreCandidate world,
        OffenseReturnArrivalRestoreCandidate returnArrivals)
    {
        Campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        Expedition = expedition
            ?? throw new ArgumentNullException(nameof(expedition));
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        World = world ?? throw new ArgumentNullException(nameof(world));
        ReturnArrivals = returnArrivals
            ?? throw new ArgumentNullException(nameof(returnArrivals));
    }

    internal OffenseCampaignRestoreCandidate Campaign { get; }
    internal OffenseExpeditionRestoreCandidate Expedition { get; }
    internal OffenseRegionRestoreCandidate Regions { get; }
    internal OffenseWorldRuntimeRestoreCandidate World { get; }
    internal OffenseReturnArrivalRestoreCandidate ReturnArrivals { get; }
}
