using System;
using System.Globalization;
using System.Linq;

public static class CharacterFacilityUseOutcomeDetails
{
    public static MigratedProducerOutcomeDetails Create(
        string facilityId,
        string facilityDisplayName,
        FacilityUseCommitState pending,
        GameplayOutcomeProvenanceReference[] provenance = null)
    {
        if (pending == null
            || string.IsNullOrWhiteSpace(facilityId)
            || string.IsNullOrWhiteSpace(pending.actorId)
            || string.IsNullOrWhiteSpace(pending.operationId))
        {
            throw new ArgumentException(
                "Facility-use outcome requires frozen actor, facility, and operation identities.");
        }

        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            pending.actorId);
        GameplayEntityId facility = new(
            MigratedProducerOutcomeIds.FacilityKind,
            facilityId);
        string[] children = (pending.childResultKeys ?? new())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] conditions = (pending.activeConditionIds ?? new())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new MigratedProducerOutcomeDetails(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    actor.Kind,
                    actor.Value,
                    string.IsNullOrWhiteSpace(pending.actorDisplayName)
                        ? actor.Value
                        : pending.actorDisplayName,
                    MigratedProducerOutcomeIds.ActorRole),
                new MigratedProducerOutcomeSubject(
                    facility.Kind,
                    facility.Value,
                    string.IsNullOrWhiteSpace(facilityDisplayName)
                        ? facility.Value
                        : facilityDisplayName,
                    MigratedProducerOutcomeIds.FacilityRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.HealthBeforeMetric,
                    pending.healthBefore, actor),
                Metric(MigratedProducerOutcomeIds.HealthAfterMetric,
                    pending.healthAfter, actor),
                Metric(MigratedProducerOutcomeIds.StressBeforeMetric,
                    pending.expeditionStressBefore, actor),
                Metric(MigratedProducerOutcomeIds.StressAfterMetric,
                    pending.expeditionStressAfter, actor),
                Metric(MigratedProducerOutcomeIds.MoodBeforeMetric,
                    pending.moodBefore, actor),
                Metric(MigratedProducerOutcomeIds.MoodAfterMetric,
                    pending.moodAfter, actor),
                Metric(MigratedProducerOutcomeIds.HungerBeforeMetric,
                    pending.hungerBefore, actor),
                Metric(MigratedProducerOutcomeIds.HungerAfterMetric,
                    pending.hungerAfter, actor),
                Metric(MigratedProducerOutcomeIds.FunBeforeMetric,
                    pending.funBefore, actor),
                Metric(MigratedProducerOutcomeIds.FunAfterMetric,
                    pending.funAfter, actor),
                Metric(MigratedProducerOutcomeIds.SleepBeforeMetric,
                    pending.sleepBefore, actor),
                Metric(MigratedProducerOutcomeIds.SleepAfterMetric,
                    pending.sleepAfter, actor),
                Metric(MigratedProducerOutcomeIds.ExcretionBeforeMetric,
                    pending.excretionBefore, actor),
                Metric(MigratedProducerOutcomeIds.ExcretionAfterMetric,
                    pending.excretionAfter, actor),
                Metric(MigratedProducerOutcomeIds.HygieneBeforeMetric,
                    pending.hygieneBefore, actor),
                Metric(MigratedProducerOutcomeIds.HygieneAfterMetric,
                    pending.hygieneAfter, actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.OperationFact,
                    pending.operationId),
                Fact(MigratedProducerOutcomeIds.FacilityRoleFact,
                    EmptyAsNone(pending.facilityRole)),
                Fact(MigratedProducerOutcomeIds.ServiceSessionFact,
                    EmptyAsNone(pending.serviceSessionId)),
                Fact(MigratedProducerOutcomeIds.OutcomeStatusFact,
                    ((CharacterFacilityUseOutcomeStatus)pending.outcomeStatus).ToString()),
                Fact(MigratedProducerOutcomeIds.ReasonFact,
                    EmptyAsNone(pending.failureCode)),
                Fact(MigratedProducerOutcomeIds.ActiveConditionsFact,
                    conditions.Length == 0 ? "none" : string.Join(",", conditions)),
                Fact(MigratedProducerOutcomeIds.ChildResultKeysFact,
                    children.Length == 0 ? "none" : string.Join(",", children)),
                Fact(MigratedProducerOutcomeIds.InjuryBeforeFact,
                    Number(pending.injuryBefore)),
                Fact(MigratedProducerOutcomeIds.InjuryAfterFact,
                    Number(pending.injuryAfter)),
                Fact(MigratedProducerOutcomeIds.MaximumHealthFact,
                    Number(pending.maximumHealthBefore)
                    + "->" + Number(pending.maximumHealthAfter))
            },
            BuildSummary(facilityDisplayName, pending),
            provenance: provenance);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayEntityId reference) => new(
        id,
        value,
        MigratedProducerOutcomeIds.PointUnit,
        reference);

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId id,
        string value) => new(id, value);

    private static string EmptyAsNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string Number(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildSummary(
        string facilityDisplayName,
        FacilityUseCommitState pending)
    {
        string facility = string.IsNullOrWhiteSpace(facilityDisplayName)
            ? "시설"
            : facilityDisplayName.Trim();
        string result = ((CharacterFacilityUseOutcomeStatus)pending.outcomeStatus)
            switch
            {
                CharacterFacilityUseOutcomeStatus.Completed => "이용을 마쳤다.",
                CharacterFacilityUseOutcomeStatus.Partial => "이용을 일부 마쳤다.",
                CharacterFacilityUseOutcomeStatus.Cancelled => "이용을 취소했다.",
                CharacterFacilityUseOutcomeStatus.Failed => "이용에 실패했다.",
                _ => "이용 결과가 확정됐다."
            };
        return facility + " " + result;
    }
}
