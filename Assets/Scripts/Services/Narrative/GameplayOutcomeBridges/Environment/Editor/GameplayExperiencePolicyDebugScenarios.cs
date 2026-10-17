using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class GameplayExperiencePolicyDebugScenarios
{
    private const string ActorId = "character:experience-policy";
    private const string OtherActorId = "character:experience-policy-other";
    private const string RunId = "run:experience-policy";
    private const string LocationId = "location:experience-policy";
    private const string RoomId = "room:experience-policy";

    public static int RunPureContracts()
    {
        var assertions = new ContractAssertions();
        var observer = new EnvironmentalExperienceObserver();
        VerifyImportancePolicy(assertions, observer);
        VerifyMalformedFactsAreUnassessed(assertions, observer);
        VerifyRegistryContracts(assertions, observer);
        VerifyRoutineObservation(assertions);
        VerifyEpisodeContinuity(assertions, observer);
        VerifySnapshotPersistence(assertions, observer);
        return assertions.Count;
    }

    private static void VerifyImportancePolicy(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer)
    {
        GameplayOutcomeEvidenceBindingSnapshot mild = Exposure(
            before: 87d,
            after: 86d,
            damage: 1d,
            bandBefore: 1,
            bandAfter: 1,
            sequence: 1);
        GameplayExperienceAssessmentSnapshot mildAssessment = ObserveUnchanged(
            assertions,
            observer,
            mild);
        RequireAssessment(
            assertions,
            mildAssessment,
            GameplayExperienceCategory.Mild,
            .30f,
            "minor-recorded-vitality-loss");

        GameplayOutcomeEvidenceBindingSnapshot differentStorageSignals = mild.Clone();
        differentStorageSignals.subjectSalience = 999f;
        differentStorageSignals.anchorRevision = 8472;
        differentStorageSignals.influenceUseCount = 73;
        differentStorageSignals.influenceRevision = 91;
        GameplayExperienceAssessmentSnapshot storageIndependent = ObserveUnchanged(
            assertions,
            observer,
            differentStorageSignals);
        assertions.Require(
            GameplayExperienceImportancePolicy.Equivalent(
                mildAssessment,
                storageIndependent),
            "Raw salience, anchor and influence storage signals changed experience importance.");

        RequireAssessment(
            assertions,
            ObserveUnchanged(assertions, observer, Exposure(100d, 100d, 0d, 1, 1, 2)),
            GameplayExperienceCategory.Routine,
            .10f,
            "exposure-without-vitality-loss");
        RequireAssessment(
            assertions,
            ObserveUnchanged(assertions, observer, Exposure(100d, 80d, 20d, 1, 2, 3)),
            GameplayExperienceCategory.Meaningful,
            .55f,
            "substantial-recorded-vitality-loss");
        RequireAssessment(
            assertions,
            ObserveUnchanged(assertions, observer, Exposure(100d, 40d, 60d, 1, 3, 4)),
            GameplayExperienceCategory.Intense,
            .80f,
            "lost-half-recorded-vitality");
        RequireAssessment(
            assertions,
            ObserveUnchanged(assertions, observer, Exposure(10d, 0d, 10d, 3, 4, 5)),
            GameplayExperienceCategory.Transformative,
            .95f,
            "recorded-vitality-exhausted");
    }

    private static void VerifyMalformedFactsAreUnassessed(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer)
    {
        GameplayOutcomeEvidenceBindingSnapshot missing = Exposure(
            100d, 80d, 20d, 1, 2, 10);
        RemoveMetric(missing, EnvironmentOutcomeIds.DamageMetric.Value);
        RequireUnassessed(assertions, ObserveUnchanged(assertions, observer, missing));

        GameplayOutcomeEvidenceBindingSnapshot nonFinite = Exposure(
            100d, 80d, 20d, 1, 2, 11);
        FindMetric(nonFinite, EnvironmentOutcomeIds.DamageMetric.Value).value = double.NaN;
        RequireUnassessed(assertions, ObserveUnchanged(assertions, observer, nonFinite));

        GameplayOutcomeEvidenceBindingSnapshot duplicate = Exposure(
            100d, 80d, 20d, 1, 2, 12);
        duplicate.metrics.Add(
            FindMetric(duplicate, EnvironmentOutcomeIds.DamageMetric.Value).Clone());
        RequireUnassessed(assertions, ObserveUnchanged(assertions, observer, duplicate));

        GameplayOutcomeEvidenceBindingSnapshot referenceMismatch = Exposure(
            100d, 80d, 20d, 1, 2, 13);
        FindMetric(referenceMismatch, EnvironmentOutcomeIds.DamageMetric.Value)
            .referenceId = OtherActorId;
        RequireUnassessed(
            assertions,
            ObserveUnchanged(assertions, observer, referenceMismatch));

        GameplayOutcomeEvidenceBindingSnapshot compacted = Exposure(
            100d, 80d, 20d, 1, 2, 14);
        compacted.sourceKind = "compacted";
        compacted.outcomeRunId = string.Empty;
        compacted.outcomeSequence = 0L;
        compacted.compactedMemoryId = "memory:experience-policy:14";
        RequireUnassessed(assertions, ObserveUnchanged(assertions, observer, compacted));

        GameplayOutcomeEvidenceBindingSnapshot inconsistent = Exposure(
            100d, 80d, 19d, 1, 2, 15);
        RequireUnassessed(
            assertions,
            ObserveUnchanged(assertions, observer, inconsistent));

        GameplayOutcomeEvidenceBindingSnapshot wrongUnit = Exposure(100d, 80d, 20d, 1, 2, 16);
        FindMetric(wrongUnit, EnvironmentOutcomeIds.DamageMetric.Value).unitId = "wrong-unit";
        RequireUnassessed(assertions, ObserveUnchanged(assertions, observer, wrongUnit));
    }

    private static void VerifyRoutineObservation(ContractAssertions assertions)
    {
        var fact = Exposure(100d, 100d, 0d, 0, 0, 17);
        fact.outcomeTypeId = "environment.control-changed";
        fact.subjectSalience = .99f;
        var routine = GameplayExperienceObserverRegistration.Default.Observe(fact);
        assertions.Require(routine.category == (int)GameplayExperienceCategory.Routine
            && routine.importance == .10f, "Routine action was inflated by raw salience.");
        fact.status = (int)GameplayOutcomeStatus.Failed;
        assertions.Require(GameplayExperienceObserverRegistration.Default.Observe(fact).category
            == (int)GameplayExperienceCategory.NotExperience, "A rejected setting became a personal experience.");
    }

    private static void VerifyRegistryContracts(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer)
    {
        assertions.Expect<ArgumentException>(
            () => new GameplayExperienceObserverRegistry(new IGameplayExperienceObserver[]
            {
                new StubObserver(
                    "experience:test-duplicate-id",
                    "test.experience-first"),
                new StubObserver(
                    "experience:test-duplicate-id",
                    "test.experience-second")
            }),
            "Duplicate observer IDs were accepted.");
        assertions.Expect<ArgumentException>(
            () => new GameplayExperienceObserverRegistry(new IGameplayExperienceObserver[]
            {
                new StubObserver(
                    "experience:test-first-observer",
                    "test.experience-duplicate-type"),
                new StubObserver(
                    "experience:test-second-observer",
                    "test.experience-duplicate-type")
            }),
            "Duplicate outcome-type registrations were accepted.");

        var registry = new GameplayExperienceObserverRegistry(
            new IGameplayExperienceObserver[] { observer });
        GameplayOutcomeEvidenceBindingSnapshot unknown = Exposure(
            100d, 80d, 20d, 1, 2, 20);
        unknown.outcomeTypeId = "test.experience-unregistered";
        GameplayExperienceAssessmentSnapshot result = ObserveUnchanged(
            assertions,
            registry,
            unknown);
        RequireUnassessed(assertions, result);
        assertions.Require(
            string.Equals(
                result.observerId,
                "experience:unregistered",
                StringComparison.Ordinal)
            && string.Equals(
                result.reasonCode,
                "observer-not-registered",
                StringComparison.Ordinal),
            "Unknown outcome type did not retain an explicit unregistered assessment.");
    }

    private static void VerifyEpisodeContinuity(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer)
    {
        var registry = new GameplayExperienceObserverRegistry(
            new IGameplayExperienceObserver[] { observer });
        GameplayOutcomeEvidenceBindingSnapshot previous = Exposure(
            100d, 80d, 20d, 1, 2, 30, day: 7);
        GameplayOutcomeEvidenceBindingSnapshot continuous = Exposure(
            80d, 70d, 10d, 2, 2, 31, day: 7);
        assertions.Require(
            registry.CanJoin(previous, continuous),
            "Continuous environmental observations did not join.");

        GameplayOutcomeEvidenceBindingSnapshot stableEndpoint = Exposure(
            100d, 100d, 0d, 0, 0, 32, day: 7);
        GameplayOutcomeEvidenceBindingSnapshot afterStable = Exposure(
            100d, 99d, 1d, 0, 1, 33, day: 7);
        assertions.Require(
            !registry.CanJoin(stableEndpoint, afterStable),
            "A stable endpoint was joined into a later exposure.");

        GameplayOutcomeEvidenceBindingSnapshot differentDay = continuous.Clone();
        differentDay.firstDay = 8;
        differentDay.lastDay = 8;
        assertions.Require(
            !registry.CanJoin(previous, differentDay),
            "Environmental observations across different days were joined.");

        GameplayOutcomeEvidenceBindingSnapshot differentLocation = continuous.Clone();
        differentLocation.locationId = "location:experience-policy-other";
        assertions.Require(
            !registry.CanJoin(previous, differentLocation),
            "Environmental observations across different locations were joined.");

        GameplayOutcomeEvidenceBindingSnapshot healthDiscontinuity = Exposure(
            79d, 70d, 9d, 2, 2, 34, day: 7);
        assertions.Require(
            !registry.CanJoin(previous, healthDiscontinuity),
            "Environmental observations with discontinuous health were joined.");

        GameplayOutcomeEvidenceBindingSnapshot differentActor = continuous.Clone();
        SetSubject(differentActor, OtherActorId);
        assertions.Require(
            !registry.CanJoin(previous, differentActor),
            "The observer registry joined environmental observations for different actors.");

        var sameStepBand = Exposure(100d, 100d, 0d, 0, 1, 35);
        var sameStepDamage = Exposure(100d, 99d, 1d, 0, 1, 36);
        sameStepBand.facts.Add(new GameplayOutcomeEvidenceFactBindingSnapshot
            { factId = EnvironmentOutcomeIds.EventKindFact.Value, value = "band-transition" });
        sameStepDamage.facts.Add(new GameplayOutcomeEvidenceFactBindingSnapshot
            { factId = EnvironmentOutcomeIds.EventKindFact.Value, value = "damage-tick" });
        foreach (string id in new[] { EnvironmentOutcomeIds.ColdBeforeMetric.Value,
                     EnvironmentOutcomeIds.ColdAfterMetric.Value, EnvironmentOutcomeIds.HeatBeforeMetric.Value,
                     EnvironmentOutcomeIds.HeatAfterMetric.Value, EnvironmentOutcomeIds.AirborneBeforeMetric.Value,
                     EnvironmentOutcomeIds.AirborneAfterMetric.Value })
        {
            sameStepBand.metrics.Add(Metric(id, 2d));
            sameStepDamage.metrics.Add(Metric(id, 2d));
        }
        assertions.Require(registry.CanJoin(sameStepBand, sameStepDamage),
            "Two receipts of the same observed state update were not joined.");
        FindMetric(sameStepDamage, EnvironmentOutcomeIds.ColdAfterMetric.Value).value = 3d;
        assertions.Require(!registry.CanJoin(sameStepBand, sameStepDamage),
            "Distinct state-update intervals were falsely joined.");
    }

    private static void VerifySnapshotPersistence(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer)
    {
        GameplayOutcomeEvidenceBindingSnapshot source = Exposure(
            100d, 80d, 20d, 1, 2, 40);
        source.experience = ObserveUnchanged(assertions, observer, source).Clone();
        GameplayOutcomeEvidenceBindingSnapshot clone = source.Clone();

        assertions.Require(
            !ReferenceEquals(source, clone)
            && !ReferenceEquals(source.experience, clone.experience)
            && !ReferenceEquals(
                source.experience.evidenceMetricIds,
                clone.experience.evidenceMetricIds)
            && !ReferenceEquals(source.metrics, clone.metrics)
            && !ReferenceEquals(source.metrics[0], clone.metrics[0]),
            "Binding clone retained mutable aliases.");
        assertions.Require(
            GameplayExperienceImportancePolicy.Equivalent(
                source.experience,
                clone.experience),
            "Binding clone changed the frozen assessment.");

        double originalMetric = source.metrics[0].value;
        clone.experience.reasonCode = "clone-only-reason";
        clone.experience.evidenceMetricIds.Add("clone-only-metric");
        clone.metrics[0].value = originalMetric + 1d;
        assertions.Require(
            !string.Equals(
                source.experience.reasonCode,
                clone.experience.reasonCode,
                StringComparison.Ordinal)
            && !source.experience.evidenceMetricIds.Contains(
                "clone-only-metric",
                StringComparer.Ordinal)
            && source.metrics[0].value.Equals(originalMetric),
            "Mutating a binding clone changed the source binding.");

        string rawBefore = RawFingerprint(source);
        GameplayOutcomeEvidenceBindingSnapshot restored =
            JsonUtility.FromJson<GameplayOutcomeEvidenceBindingSnapshot>(
                JsonUtility.ToJson(source));
        assertions.Require(restored != null, "Binding JSON round trip returned null.");
        assertions.Require(
            RawFingerprint(restored) == rawBefore,
            "Binding JSON round trip changed raw outcome evidence.");
        assertions.Require(
            GameplayExperienceImportancePolicy.Equivalent(
                source.experience,
                restored.experience),
            "Binding JSON round trip changed the frozen assessment.");
        assertions.Require(
            !ReferenceEquals(source.experience, restored.experience)
            && !ReferenceEquals(
                source.experience.evidenceMetricIds,
                restored.experience.evidenceMetricIds),
            "Binding JSON round trip retained assessment aliases.");
    }

    private static GameplayExperienceAssessmentSnapshot ObserveUnchanged(
        ContractAssertions assertions,
        EnvironmentalExperienceObserver observer,
        GameplayOutcomeEvidenceBindingSnapshot fact)
    {
        string before = RawFingerprint(fact);
        GameplayExperienceAssessmentSnapshot result = observer.Observe(fact);
        assertions.Require(
            RawFingerprint(fact) == before,
            "Environmental observation mutated raw gameplay-outcome evidence.");
        return result;
    }

    private static GameplayExperienceAssessmentSnapshot ObserveUnchanged(
        ContractAssertions assertions,
        GameplayExperienceObserverRegistry registry,
        GameplayOutcomeEvidenceBindingSnapshot fact)
    {
        string before = RawFingerprint(fact);
        GameplayExperienceAssessmentSnapshot result = registry.Observe(fact);
        assertions.Require(
            RawFingerprint(fact) == before,
            "Experience registry observation mutated raw gameplay-outcome evidence.");
        return result;
    }

    private static void RequireAssessment(
        ContractAssertions assertions,
        GameplayExperienceAssessmentSnapshot actual,
        GameplayExperienceCategory category,
        float importance,
        string reasonCode)
    {
        assertions.Require(actual != null, "Experience assessment is missing.");
        assertions.Require(
            actual.policyVersion == GameplayExperienceImportancePolicy.Version
            && string.Equals(
                actual.observerId,
                "experience:environment-exposure:v3",
                StringComparison.Ordinal),
            "Experience assessment policy or observer identity differs.");
        assertions.Require(
            actual.category == (int)category
            && actual.importance.Equals(importance)
            && string.Equals(actual.reasonCode, reasonCode, StringComparison.Ordinal),
            "Experience assessment category, importance or reason differs.");
        assertions.Require(
            actual.evidenceMetricIds != null
            && actual.evidenceMetricIds.SequenceEqual(
                new[]
                {
                    EnvironmentOutcomeIds.DamageMetric.Value,
                    EnvironmentOutcomeIds.HealthAfterMetric.Value,
                    EnvironmentOutcomeIds.HealthBeforeMetric.Value
                },
                StringComparer.Ordinal),
            "Experience assessment metric provenance differs.");
    }

    private static void RequireUnassessed(
        ContractAssertions assertions,
        GameplayExperienceAssessmentSnapshot actual)
    {
        assertions.Require(actual != null, "Explicit unassessed result is missing.");
        assertions.Require(
            actual.policyVersion == GameplayExperienceImportancePolicy.Version
            && actual.category == (int)GameplayExperienceCategory.Unassessed
            && actual.importance.Equals(0f),
            "Malformed or unsupported evidence did not remain explicitly unassessed.");
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Exposure(
        double before,
        double after,
        double damage,
        int bandBefore,
        int bandAfter,
        long sequence,
        int day = 5)
    {
        var result = new GameplayOutcomeEvidenceBindingSnapshot
        {
            schemaVersion = GameplayOutcomeEvidenceFormulaProjection.CurrentBindingSchemaVersion,
            sourceKind = "exact",
            publicFactId = "public-fact:experience-policy:" + sequence,
            outcomeRunId = RunId,
            outcomeSequence = sequence,
            outcomeTypeId = EnvironmentOutcomeIds.EnvironmentalExposureThreshold.Value,
            subjectKindId = EnvironmentOutcomeIds.CharacterKind.Value,
            subjectId = ActorId,
            perspectiveKind = (int)NarrativePerspectiveKind.Character,
            firstDay = day,
            lastDay = day,
            occurrenceCount = 1,
            anchorRevision = 0,
            status = (int)GameplayOutcomeStatus.Succeeded,
            subjectSalience = .01f,
            influenceUseCount = 0,
            influenceRevision = 0,
            canonicalFactText = "환경 노출 경험 원본",
            locationId = LocationId,
            roomId = RoomId,
            locationX = 2,
            locationY = 3,
            operationId = "operation:experience-policy:" + sequence,
            rootOperationId = "operation:experience-policy-root"
        };
        result.metrics = new List<GameplayOutcomeEvidenceMetricBindingSnapshot>
        {
            Metric(EnvironmentOutcomeIds.HealthBeforeMetric.Value, before),
            Metric(EnvironmentOutcomeIds.HealthAfterMetric.Value, after),
            Metric(EnvironmentOutcomeIds.DamageMetric.Value, damage),
            Metric(EnvironmentOutcomeIds.BandBeforeMetric.Value, bandBefore),
            Metric(EnvironmentOutcomeIds.BandAfterMetric.Value, bandAfter)
        };
        result.metricIds = result.metrics
            .Select(value => value.metricId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        result.metricReferenceIds = new List<string>
        {
            EnvironmentOutcomeIds.CharacterKind.Value + ":" + ActorId
        };
        return result;
    }

    private static GameplayOutcomeEvidenceMetricBindingSnapshot Metric(
        string metricId,
        double value) => new()
    {
        metricId = metricId,
        unitId = metricId == EnvironmentOutcomeIds.BandBeforeMetric.Value
            || metricId == EnvironmentOutcomeIds.BandAfterMetric.Value
            ? EnvironmentOutcomeIds.EnumUnit.Value
            : EnvironmentOutcomeIds.PointUnit.Value,
        referenceKindId = EnvironmentOutcomeIds.CharacterKind.Value,
        referenceId = ActorId,
        value = value,
        minimum = value,
        maximum = value,
        sampleCount = 1
    };

    private static GameplayOutcomeEvidenceMetricBindingSnapshot FindMetric(
        GameplayOutcomeEvidenceBindingSnapshot source,
        string metricId) => source.metrics.Single(value => value != null
            && string.Equals(value.metricId, metricId, StringComparison.Ordinal));

    private static void RemoveMetric(
        GameplayOutcomeEvidenceBindingSnapshot source,
        string metricId)
    {
        source.metrics.RemoveAll(value => value != null
            && string.Equals(value.metricId, metricId, StringComparison.Ordinal));
        source.metricIds.RemoveAll(value => string.Equals(
            value,
            metricId,
            StringComparison.Ordinal));
    }

    private static void SetSubject(
        GameplayOutcomeEvidenceBindingSnapshot source,
        string subjectId)
    {
        source.subjectId = subjectId;
        foreach (GameplayOutcomeEvidenceMetricBindingSnapshot metric in source.metrics)
            metric.referenceId = subjectId;
        source.metricReferenceIds = new List<string>
        {
            source.subjectKindId + ":" + subjectId
        };
    }

    private static string RawFingerprint(
        GameplayOutcomeEvidenceBindingSnapshot value)
    {
        var builder = new StringBuilder(1024);
        Append(builder, value.schemaVersion);
        Append(builder, value.sourceKind);
        Append(builder, value.publicFactId);
        Append(builder, value.outcomeRunId);
        Append(builder, value.outcomeSequence);
        Append(builder, value.compactedMemoryId);
        Append(builder, value.outcomeTypeId);
        Append(builder, value.subjectKindId);
        Append(builder, value.subjectId);
        Append(builder, value.perspectiveKind);
        Append(builder, value.firstDay);
        Append(builder, value.lastDay);
        Append(builder, value.occurrenceCount);
        Append(builder, value.anchorRevision);
        Append(builder, value.status);
        Append(builder, BitConverter.SingleToInt32Bits(value.subjectSalience));
        Append(builder, value.influenceUseCount);
        Append(builder, value.influenceRevision);
        Append(builder, value.canonicalFactText);
        Append(builder, value.locationId);
        Append(builder, value.roomId);
        Append(builder, value.locationX);
        Append(builder, value.locationY);
        Append(builder, value.operationId);
        Append(builder, value.parentOutcomeId);
        Append(builder, value.rootOperationId);
        Append(builder, value.causationRelationId);
        Append(builder, value.metricIds?.Count ?? -1);
        foreach (string metricId in value.metricIds ?? new List<string>())
            Append(builder, metricId);
        Append(builder, value.metricReferenceIds?.Count ?? -1);
        foreach (string referenceId in value.metricReferenceIds ?? new List<string>())
            Append(builder, referenceId);
        Append(builder, value.metrics?.Count ?? -1);
        foreach (GameplayOutcomeEvidenceMetricBindingSnapshot metric in
                 value.metrics ?? new List<GameplayOutcomeEvidenceMetricBindingSnapshot>())
        {
            if (metric == null)
            {
                Append(builder, "<null>");
                continue;
            }
            Append(builder, metric.metricId);
            Append(builder, metric.unitId);
            Append(builder, metric.referenceKindId);
            Append(builder, metric.referenceId);
            Append(builder, BitConverter.DoubleToInt64Bits(metric.value));
            Append(builder, BitConverter.DoubleToInt64Bits(metric.minimum));
            Append(builder, BitConverter.DoubleToInt64Bits(metric.maximum));
            Append(builder, metric.sampleCount);
        }
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, object value)
    {
        string text = value?.ToString() ?? "<null>";
        builder.Append(text.Length).Append(':').Append(text).Append('|');
    }

    private sealed class StubObserver : IGameplayExperienceObserver
    {
        internal StubObserver(string observerId, params string[] outcomeTypes)
        {
            ObserverId = observerId;
            OutcomeTypes = outcomeTypes;
        }

        public string ObserverId { get; }
        public IReadOnlyList<string> OutcomeTypes { get; }

        public GameplayExperienceAssessmentSnapshot Observe(
            GameplayOutcomeEvidenceBindingSnapshot fact) =>
            GameplayExperienceImportancePolicy.Assess(
                ObserverId,
                GameplayExperienceCategory.Routine,
                "test-observation");

        public bool CanJoin(
            GameplayOutcomeEvidenceBindingSnapshot previous,
            GameplayOutcomeEvidenceBindingSnapshot next) => false;
    }

    private sealed class ContractAssertions
    {
        internal int Count { get; private set; }

        internal void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            Count++;
        }

        internal void Expect<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                Count++;
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
