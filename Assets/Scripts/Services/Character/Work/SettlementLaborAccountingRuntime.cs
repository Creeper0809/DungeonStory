using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

public enum SettlementLaborContributionChannel
{
    ActualLabor = 0,
    ConvertedProcessOutput = 1,
    DomainAutomation = 2,
    FuelMaintenanceAccidentSpoilageLoss = 3,
    EssentialMaintenance = 4,
    EquipmentFacilityMaintenance = 5
}

public readonly struct SettlementLaborContribution
{
    public SettlementLaborContribution(
        string operationId,
        long eventSequence,
        SettlementLaborContributionChannel channel,
        long milliWu,
        string domainId)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        EventSequence = eventSequence;
        Channel = channel;
        MilliWu = milliWu;
        DomainId = domainId?.Trim() ?? string.Empty;
    }

    public string OperationId { get; }
    public long EventSequence { get; }
    public SettlementLaborContributionChannel Channel { get; }
    public long MilliWu { get; }
    public string DomainId { get; }
}

public readonly struct SettlementLaborDailyRecord
{
    public SettlementLaborDailyRecord(
        int absoluteDay,
        long actualLaborMilliWu,
        long outputEquivalentMilliWu,
        long realizedGrowthMilliWu,
        long guaranteedGrowthMilliWu,
        int productiveAdultCount,
        float perCapitaNetWuIndex)
    {
        AbsoluteDay = absoluteDay;
        ActualLaborMilliWu = actualLaborMilliWu;
        OutputEquivalentMilliWu = outputEquivalentMilliWu;
        RealizedGrowthMilliWu = realizedGrowthMilliWu;
        GuaranteedGrowthMilliWu = guaranteedGrowthMilliWu;
        ProductiveAdultCount = productiveAdultCount;
        PerCapitaNetWuIndex = perCapitaNetWuIndex;
    }

    public int AbsoluteDay { get; }
    public long ActualLaborMilliWu { get; }
    public long OutputEquivalentMilliWu { get; }
    public long RealizedGrowthMilliWu { get; }
    public long GuaranteedGrowthMilliWu { get; }
    public int ProductiveAdultCount { get; }
    public float PerCapitaNetWuIndex { get; }
}

public readonly struct SettlementLaborAccountingSnapshot
{
    public SettlementLaborAccountingSnapshot(
        long actualLaborMilliWu,
        long convertedProcessOutputMilliWu,
        long domainAutomationMilliWu,
        long lossMilliWu,
        long essentialMaintenanceMilliWu,
        long equipmentFacilityMaintenanceMilliWu,
        long outputEquivalentMilliWu,
        long realizedGrowthMilliWu,
        long guaranteedGrowthMilliWu,
        int completedDayCount,
        float rollingPerCapitaNetWuMedian,
        SettlementLaborDailyRecord latestDay,
        long revision)
    {
        ActualLaborMilliWu = actualLaborMilliWu;
        ConvertedProcessOutputMilliWu = convertedProcessOutputMilliWu;
        DomainAutomationMilliWu = domainAutomationMilliWu;
        LossMilliWu = lossMilliWu;
        EssentialMaintenanceMilliWu = essentialMaintenanceMilliWu;
        EquipmentFacilityMaintenanceMilliWu = equipmentFacilityMaintenanceMilliWu;
        OutputEquivalentMilliWu = outputEquivalentMilliWu;
        RealizedGrowthMilliWu = realizedGrowthMilliWu;
        GuaranteedGrowthMilliWu = guaranteedGrowthMilliWu;
        CompletedDayCount = completedDayCount;
        RollingPerCapitaNetWuMedian = rollingPerCapitaNetWuMedian;
        LatestDay = latestDay;
        Revision = revision;
    }

    public long ActualLaborMilliWu { get; }
    public long ConvertedProcessOutputMilliWu { get; }
    public long DomainAutomationMilliWu { get; }
    public long LossMilliWu { get; }
    public long EssentialMaintenanceMilliWu { get; }
    public long EquipmentFacilityMaintenanceMilliWu { get; }
    public long OutputEquivalentMilliWu { get; }
    public long RealizedGrowthMilliWu { get; }
    public long GuaranteedGrowthMilliWu { get; }
    public int CompletedDayCount { get; }
    public float RollingPerCapitaNetWuMedian { get; }
    public SettlementLaborDailyRecord LatestDay { get; }
    public long Revision { get; }
}

public interface ISettlementLaborAccountingService
{
    EmergencyAccountingResult Record(SettlementLaborContribution contribution);
    SettlementLaborAccountingSnapshot Capture();
}

public interface ISettlementLaborPersistence
{
    DungeonStory.Infrastructure.SettlementLaborSaveData CaptureLaborSaveData();
    void RestoreLaborSaveData(
        DungeonStory.Infrastructure.SettlementLaborSaveData saveData);
}

/// <summary>
/// Daily authority for actual, output-equivalent, realized-growth and
/// guaranteed-growth WU. Domain automation cannot be transferred to another
/// domain through this API; producers must publish their own converted output.
/// </summary>
public sealed class SettlementLaborAccountingRuntime :
    ISettlementLaborAccountingService,
    ISettlementLaborPersistence,
    IStartable,
    IDisposable
{
    private const int RollingDayCount = 30;

    private readonly IGameEventBus events;
    private readonly ISettlementEmergencyReserveTargetQuery reserveTarget;
    private readonly Dictionary<ContributionKey, long> lastSequenceByContribution =
        new Dictionary<ContributionKey, long>();
    private readonly SettlementLaborDailyRecord[] daily =
        new SettlementLaborDailyRecord[RollingDayCount];
    private readonly float[] medianScratch = new float[RollingDayCount];

    private IDisposable dayEndedSubscription;
    private DailyTotals current;
    private int dailyHead;
    private int dailyCount;
    private float rollingMedian;
    private SettlementLaborDailyRecord latest;
    private long revision;

    public SettlementLaborAccountingRuntime(
        IGameEventBus events,
        ISettlementEmergencyReserveTargetQuery reserveTarget)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.reserveTarget = reserveTarget
            ?? throw new ArgumentNullException(nameof(reserveTarget));
    }

    public void Start() => dayEndedSubscription ??=
        events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    public EmergencyAccountingResult Record(SettlementLaborContribution contribution)
    {
        if (contribution.OperationId.Length == 0
            || contribution.DomainId.Length == 0
            || contribution.EventSequence < 0L
            || contribution.MilliWu < 0L
            || !Enum.IsDefined(
                typeof(SettlementLaborContributionChannel),
                contribution.Channel))
        {
            return EmergencyAccountingResult.Fail(
                "SettlementLaborContributionInvalid",
                "Labor contributions require operation/domain IDs, a valid channel, sequence and non-negative milli-WU.");
        }

        ContributionKey key = new ContributionKey(
            contribution.OperationId,
            contribution.Channel);
        if (lastSequenceByContribution.TryGetValue(key, out long previous)
            && contribution.EventSequence <= previous)
        {
            return EmergencyAccountingResult.Ok("duplicate-labor-contribution-ignored");
        }

        Add(ref current, contribution.Channel, contribution.MilliWu);
        lastSequenceByContribution[key] = contribution.EventSequence;
        revision = checked(revision + 1L);
        return EmergencyAccountingResult.Ok("labor-contribution-recorded");
    }

    public SettlementLaborAccountingSnapshot Capture()
    {
        SettlementEmergencyReserveTargetSnapshot reserve =
            reserveTarget.CaptureTarget();
        long transferableOutput = Math.Max(
            0L,
            checked(current.ActualLaborMilliWu
                + current.ConvertedProcessOutputMilliWu
                - current.LossMilliWu));
        long outputEquivalent = checked(
            transferableOutput + current.DomainAutomationMilliWu);
        long realizedGrowth = Math.Max(
            0L,
            checked(transferableOutput
                - current.EssentialMaintenanceMilliWu
                - current.EquipmentFacilityMaintenanceMilliWu));
        long guaranteedGrowth = Math.Max(
            0L,
            checked(realizedGrowth - reserve.TargetMilliWu));
        return new SettlementLaborAccountingSnapshot(
            current.ActualLaborMilliWu,
            current.ConvertedProcessOutputMilliWu,
            current.DomainAutomationMilliWu,
            current.LossMilliWu,
            current.EssentialMaintenanceMilliWu,
            current.EquipmentFacilityMaintenanceMilliWu,
            outputEquivalent,
            realizedGrowth,
            guaranteedGrowth,
            dailyCount,
            rollingMedian,
            latest,
            revision);
    }

    public DungeonStory.Infrastructure.SettlementLaborSaveData CaptureLaborSaveData()
    {
        DungeonStory.Infrastructure.SettlementLaborSaveData result =
            new DungeonStory.Infrastructure.SettlementLaborSaveData
            {
                actualLaborMilliWu = current.ActualLaborMilliWu,
                convertedProcessOutputMilliWu =
                    current.ConvertedProcessOutputMilliWu,
                domainAutomationMilliWu = current.DomainAutomationMilliWu,
                lossMilliWu = current.LossMilliWu,
                essentialMaintenanceMilliWu = current.EssentialMaintenanceMilliWu,
                equipmentFacilityMaintenanceMilliWu =
                    current.EquipmentFacilityMaintenanceMilliWu
            };
        int oldest = (dailyHead - dailyCount + RollingDayCount) % RollingDayCount;
        for (int index = 0; index < dailyCount; index++)
        {
            SettlementLaborDailyRecord record =
                daily[(oldest + index) % RollingDayCount];
            result.dailyRecords.Add(
                new DungeonStory.Infrastructure.SettlementLaborDailySaveData
                {
                    absoluteDay = record.AbsoluteDay,
                    actualLaborMilliWu = record.ActualLaborMilliWu,
                    outputEquivalentMilliWu = record.OutputEquivalentMilliWu,
                    realizedGrowthMilliWu = record.RealizedGrowthMilliWu,
                    guaranteedGrowthMilliWu = record.GuaranteedGrowthMilliWu,
                    productiveAdultCount = record.ProductiveAdultCount,
                    perCapitaNetWuIndex = record.PerCapitaNetWuIndex
                });
        }
        foreach (KeyValuePair<ContributionKey, long> pair in
                 lastSequenceByContribution)
        {
            result.contributionSequences.Add(
                new DungeonStory.Infrastructure.SettlementLaborSequenceSaveData
                {
                    operationId = pair.Key.OperationId,
                    channel = (int)pair.Key.Channel,
                    lastSequence = pair.Value
                });
        }
        result.contributionSequences.Sort((left, right) =>
        {
            int operation = string.CompareOrdinal(
                left.operationId,
                right.operationId);
            return operation != 0
                ? operation
                : left.channel.CompareTo(right.channel);
        });
        return result;
    }

    public void RestoreLaborSaveData(
        DungeonStory.Infrastructure.SettlementLaborSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        current = new DailyTotals
        {
            ActualLaborMilliWu = saveData.actualLaborMilliWu,
            ConvertedProcessOutputMilliWu = saveData.convertedProcessOutputMilliWu,
            DomainAutomationMilliWu = saveData.domainAutomationMilliWu,
            LossMilliWu = saveData.lossMilliWu,
            EssentialMaintenanceMilliWu = saveData.essentialMaintenanceMilliWu,
            EquipmentFacilityMaintenanceMilliWu =
                saveData.equipmentFacilityMaintenanceMilliWu
        };
        Array.Clear(daily, 0, daily.Length);
        dailyCount = saveData.dailyRecords.Count;
        dailyHead = dailyCount % RollingDayCount;
        for (int index = 0; index < dailyCount; index++)
        {
            DungeonStory.Infrastructure.SettlementLaborDailySaveData source =
                saveData.dailyRecords[index];
            daily[index] = new SettlementLaborDailyRecord(
                source.absoluteDay,
                source.actualLaborMilliWu,
                source.outputEquivalentMilliWu,
                source.realizedGrowthMilliWu,
                source.guaranteedGrowthMilliWu,
                source.productiveAdultCount,
                source.perCapitaNetWuIndex);
        }
        latest = dailyCount > 0 ? daily[dailyCount - 1] : default;
        rollingMedian = dailyCount > 0 ? CalculateMedian() : 0f;

        lastSequenceByContribution.Clear();
        for (int index = 0; index < saveData.contributionSequences.Count; index++)
        {
            DungeonStory.Infrastructure.SettlementLaborSequenceSaveData source =
                saveData.contributionSequences[index];
            lastSequenceByContribution.Add(
                new ContributionKey(
                    source.operationId,
                    (SettlementLaborContributionChannel)source.channel),
                source.lastSequence);
        }
        revision = checked(revision + 1L);
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        SettlementEmergencyReserveTargetSnapshot reserve =
            reserveTarget.CaptureTarget();
        long transferableOutput = Math.Max(
            0L,
            checked(current.ActualLaborMilliWu
                + current.ConvertedProcessOutputMilliWu
                - current.LossMilliWu));
        long output = checked(
            transferableOutput + current.DomainAutomationMilliWu);
        long realized = Math.Max(
            0L,
            checked(transferableOutput
                - current.EssentialMaintenanceMilliWu
                - current.EquipmentFacilityMaintenanceMilliWu));
        long guaranteed = Math.Max(
            0L,
            checked(realized - reserve.TargetMilliWu));
        float index = reserve.ProductiveAdultCount > 0
            ? realized / (float)EmergencyWuUnits.UnitsPerWu
                / reserve.ProductiveAdultCount
                / SettlementLaborAuthority.EffectiveOutputWuPerAdultDay
            : 0f;
        latest = new SettlementLaborDailyRecord(
            ended.day,
            current.ActualLaborMilliWu,
            output,
            realized,
            guaranteed,
            reserve.ProductiveAdultCount,
            index);
        daily[dailyHead] = latest;
        dailyHead = (dailyHead + 1) % RollingDayCount;
        dailyCount = Math.Min(RollingDayCount, dailyCount + 1);
        rollingMedian = CalculateMedian();
        current = default;
        lastSequenceByContribution.Clear();
        revision = checked(revision + 1L);
    }

    private float CalculateMedian()
    {
        for (int index = 0; index < dailyCount; index++)
        {
            medianScratch[index] = daily[index].PerCapitaNetWuIndex;
        }
        Array.Sort(medianScratch, 0, dailyCount);
        int middle = dailyCount / 2;
        return dailyCount % 2 == 0
            ? (medianScratch[middle - 1] + medianScratch[middle]) * 0.5f
            : medianScratch[middle];
    }

    private static void Add(
        ref DailyTotals totals,
        SettlementLaborContributionChannel channel,
        long milliWu)
    {
        switch (channel)
        {
            case SettlementLaborContributionChannel.ActualLabor:
                totals.ActualLaborMilliWu = checked(
                    totals.ActualLaborMilliWu + milliWu);
                break;
            case SettlementLaborContributionChannel.ConvertedProcessOutput:
                totals.ConvertedProcessOutputMilliWu = checked(
                    totals.ConvertedProcessOutputMilliWu + milliWu);
                break;
            case SettlementLaborContributionChannel.DomainAutomation:
                totals.DomainAutomationMilliWu = checked(
                    totals.DomainAutomationMilliWu + milliWu);
                break;
            case SettlementLaborContributionChannel.FuelMaintenanceAccidentSpoilageLoss:
                totals.LossMilliWu = checked(totals.LossMilliWu + milliWu);
                break;
            case SettlementLaborContributionChannel.EssentialMaintenance:
                totals.EssentialMaintenanceMilliWu = checked(
                    totals.EssentialMaintenanceMilliWu + milliWu);
                break;
            case SettlementLaborContributionChannel.EquipmentFacilityMaintenance:
                totals.EquipmentFacilityMaintenanceMilliWu = checked(
                    totals.EquipmentFacilityMaintenanceMilliWu + milliWu);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
        }
    }

    private readonly struct ContributionKey : IEquatable<ContributionKey>
    {
        public ContributionKey(
            string operationId,
            SettlementLaborContributionChannel channel)
        {
            OperationId = operationId;
            Channel = channel;
        }

        public string OperationId { get; }
        public SettlementLaborContributionChannel Channel { get; }

        public bool Equals(ContributionKey other) =>
            Channel == other.Channel
            && string.Equals(OperationId, other.OperationId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ContributionKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(OperationId),
            (int)Channel);
    }

    private struct DailyTotals
    {
        public long ActualLaborMilliWu;
        public long ConvertedProcessOutputMilliWu;
        public long DomainAutomationMilliWu;
        public long LossMilliWu;
        public long EssentialMaintenanceMilliWu;
        public long EquipmentFacilityMaintenanceMilliWu;
    }
}
