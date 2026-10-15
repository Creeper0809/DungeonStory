using System;
using System.Linq;
using VContainer;

public static class SeasonalTerminalFactMapping
{
    public const string WildlifeArrivalObserverId =
        "log:seasonal.wildlife-arrival";
    public const string DriftCargoObserverId =
        "log:seasonal.drift-cargo";

    private static readonly GameplayOutcomeFactId HabitatFact =
        new("habitat-id");
    private static readonly GameplayEntityKindId WildlifeKind =
        new("wildlife");
    private static readonly GameplayOutcomeFactId OccurrenceFact =
        new("occurrence-id");
    private static readonly GameplayOutcomeFactId SecuredStacksFact =
        new("source-stack-ids");

    public static MigratedDomainFactCapture Capture(
        SeasonalWildlifeArrivalFact fact)
    {
        SeasonalWildlifeArrivalFactData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        MigratedProducerOutcomeSubject[] subjects = data.wildlifeIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => new MigratedProducerOutcomeSubject(
                WildlifeKind,
                value,
                data.speciesDisplayName,
                MigratedProducerOutcomeIds.TargetRole))
            .ToArray();
        GameplayEntityId metricSubject = subjects[0].EntityId;
        var details = new MigratedProducerOutcomeDetails(
            subjects,
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CountMetric,
                    subjects.Length,
                    MigratedProducerOutcomeIds.CountUnit,
                    metricSubject)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    data.occurrenceId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OccurrenceFact,
                    data.occurrenceId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SpeciesFact,
                    data.speciesId),
                new GameplayOutcomeFact(
                    HabitatFact,
                    data.habitatId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    string.Join(",", subjects.Select(value =>
                        value.EntityId.Value)))
            },
            data.speciesDisplayName + " " + subjects.Length
            + "마리의 계절 방문이 실제 개체 생성으로 완료되었습니다.");
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.SeasonalWildlifeArrivalResult,
            "seasonal-wildlife-arrival:occurrence=" + data.occurrenceId
            + ":species=" + data.speciesId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static MigratedDomainFactCapture Capture(
        SeasonalDriftCargoFact fact)
    {
        SeasonalDriftCargoFactData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        MigratedProducerOutcomeSubject[] subjects = data.sourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.ItemKind,
                value,
                data.itemDisplayName,
                MigratedProducerOutcomeIds.ItemRole))
            .ToArray();
        GameplayEntityId metricSubject = subjects[0].EntityId;
        var details = new MigratedProducerOutcomeDetails(
            subjects,
            new[]
            {
                Metric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    data.exactQuantity,
                    metricSubject),
                Metric(
                    MigratedProducerOutcomeIds.SecuredCountMetric,
                    data.securedQuantity,
                    metricSubject),
                Metric(
                    MigratedProducerOutcomeIds.ExpiredCountMetric,
                    data.expiredQuantity,
                    metricSubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MassMetric,
                    data.expiredMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    metricSubject)
            },
            new[]
            {
                Fact(
                    MigratedProducerOutcomeIds.OperationFact,
                    data.occurrenceId),
                Fact(OccurrenceFact, data.occurrenceId),
                Fact(
                    MigratedProducerOutcomeIds.IdentityFact,
                    data.itemId),
                Fact(
                    SecuredStacksFact,
                    string.Join(",", data.sourceStackIds.OrderBy(
                        value => value,
                        StringComparer.Ordinal))),
                Fact(
                    MigratedProducerOutcomeIds.BatchFact,
                    string.IsNullOrEmpty(data.dispositionCommitId)
                        ? "none"
                        : data.dispositionCommitId),
                Fact(
                    MigratedProducerOutcomeIds.DetailFact,
                    "unitMassGrams=" + data.unitMassGrams
                    + ";position=" + data.positionX + ":"
                    + data.positionY)
            },
            data.itemDisplayName + " 표류 화물 정산: 확보 "
            + data.securedQuantity + "개, 유실 "
            + data.expiredQuantity + "개.");
        GameplayOutcomeStatus status =
            data.securedQuantity == data.exactQuantity
                ? GameplayOutcomeStatus.Succeeded
                : data.securedQuantity > 0
                    ? GameplayOutcomeStatus.PartiallySucceeded
                    : GameplayOutcomeStatus.Failed;
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.SeasonalDriftCargoResult,
            "seasonal-drift-cargo:occurrence=" + data.occurrenceId,
            data.absoluteDay,
            status,
            details);
    }

    public static void Register(IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.RegisterMigratedDomainFact<SeasonalWildlifeArrivalFact>(
            WildlifeArrivalObserverId,
            Capture);
        builder.RegisterMigratedDomainFact<SeasonalDriftCargoFact>(
            DriftCargoObserverId,
            Capture);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId metric,
        long value,
        GameplayEntityId subject) => new(
            metric,
            value,
            MigratedProducerOutcomeIds.CountUnit,
            subject);

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId fact,
        string value) => new(fact, value);
}
