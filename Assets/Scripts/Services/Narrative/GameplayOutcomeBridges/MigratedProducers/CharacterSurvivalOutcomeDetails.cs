using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public readonly struct CharacterWaterOutcomeEvidence
{
    public CharacterWaterOutcomeEvidence(
        CharacterId characterId,
        string characterDisplayName,
        string sourceId,
        CharacterWaterSourceKind sourceKind,
        WorldWaterQuality quality,
        float consumed,
        string pathogenDiseaseId,
        float thirstBefore,
        float thirstAfter,
        float healthBefore,
        float healthAfter,
        float hygieneBefore,
        float hygieneAfter,
        float infectionBefore,
        float infectionAfter,
        float moodBefore,
        float moodAfter)
    {
        CharacterId = characterId;
        CharacterDisplayName = characterDisplayName?.Trim() ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
        SourceKind = sourceKind;
        Quality = quality;
        Consumed = consumed;
        PathogenDiseaseId = pathogenDiseaseId?.Trim() ?? string.Empty;
        ThirstBefore = thirstBefore;
        ThirstAfter = thirstAfter;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        HygieneBefore = hygieneBefore;
        HygieneAfter = hygieneAfter;
        InfectionBefore = infectionBefore;
        InfectionAfter = infectionAfter;
        MoodBefore = moodBefore;
        MoodAfter = moodAfter;
    }

    public CharacterId CharacterId { get; }
    public string CharacterDisplayName { get; }
    public string SourceId { get; }
    public CharacterWaterSourceKind SourceKind { get; }
    public WorldWaterQuality Quality { get; }
    public float Consumed { get; }
    public string PathogenDiseaseId { get; }
    public float ThirstBefore { get; }
    public float ThirstAfter { get; }
    public float HealthBefore { get; }
    public float HealthAfter { get; }
    public float HygieneBefore { get; }
    public float HygieneAfter { get; }
    public float InfectionBefore { get; }
    public float InfectionAfter { get; }
    public float MoodBefore { get; }
    public float MoodAfter { get; }
}

public static class CharacterSurvivalOutcomeDetails
{
    public static MigratedProducerOutcomeDetails Meal(
        CharacterId characterId,
        string characterDisplayName,
        in CharacterConsumablesOutcomeEvidence evidence,
        string summary)
    {
        if (!characterId.IsValid
            || !evidence.ItemDefinitionId.IsValid
            || evidence.ConsumedQuantity <= 0
            || evidence.ConsumedMassGrams <= 0L)
        {
            throw new ArgumentException("Complete meal custody evidence is required.");
        }

        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            characterId.Value);
        GameplayEntityId itemDefinition = new(
            MigratedProducerOutcomeIds.ItemDefinitionKind,
            evidence.ItemDefinitionId.Value);
        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            Subject(actor, characterDisplayName, MigratedProducerOutcomeIds.ActorRole),
            Subject(itemDefinition, evidence.ItemDefinitionId.Value,
                MigratedProducerOutcomeIds.ItemRole)
        };
        if (evidence.FacilityId.IsValid)
        {
            subjects.Add(Subject(
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.FacilityKind,
                    evidence.FacilityId.Value),
                evidence.FacilityId.Value,
                MigratedProducerOutcomeIds.FacilityRole));
        }
        string[] stacks = (evidence.SourceStackIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string stack in stacks)
        {
            subjects.Add(Subject(
                new GameplayEntityId(MigratedProducerOutcomeIds.ItemStackKind, stack),
                stack,
                MigratedProducerOutcomeIds.SourceRole));
        }
        if (stacks.Length == 0 || subjects.Count > 16)
            throw new ArgumentException("Meal source stack evidence is missing or exceeds capacity.");

        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                Metric(MigratedProducerOutcomeIds.QuantityMetric,
                    evidence.ConsumedQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    itemDefinition),
                Metric(MigratedProducerOutcomeIds.InputMassMetric,
                    evidence.ConsumedMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    itemDefinition),
                Metric(MigratedProducerOutcomeIds.HungerBeforeMetric,
                    evidence.Effects.HungerBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HungerAfterMetric,
                    evidence.Effects.HungerAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HealthBeforeMetric,
                    evidence.Effects.HealthBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HealthAfterMetric,
                    evidence.Effects.HealthAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodBeforeMetric,
                    evidence.Effects.MoodBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodAfterMetric,
                    evidence.Effects.MoodAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.ItemDefinitionFact,
                    evidence.ItemDefinitionId.Value),
                Fact(MigratedProducerOutcomeIds.SourceStackFact,
                    string.Join(",", stacks)),
                Fact(MigratedProducerOutcomeIds.PhysicalCommitFact,
                    RequireText(evidence.PhysicalCommitId, "physical commit")),
                Fact(MigratedProducerOutcomeIds.FacilityFact,
                    evidence.FacilityId.IsValid
                        ? evidence.FacilityId.Value
                        : "field"),
                Fact(MigratedProducerOutcomeIds.DetailFact,
                    "policyViolation=" + evidence.PolicyViolation
                    + ";contaminated=" + evidence.Contaminated
                    + ";contaminationDamage="
                    + evidence.ContaminationDamageApplied
                    + ";effectsSkippedReason=" + evidence.EffectsSkippedReason)
            },
            summary);
    }

    public static MigratedProducerOutcomeDetails Water(
        in CharacterWaterOutcomeEvidence evidence,
        string summary,
        GameplayOutcomeCausation causation = default)
    {
        if (!evidence.CharacterId.IsValid
            || string.IsNullOrWhiteSpace(evidence.SourceId)
            || evidence.Consumed <= 0f)
        {
            throw new ArgumentException("Complete water consumption evidence is required.");
        }
        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            evidence.CharacterId.Value);
        GameplayEntityId source = new(
            evidence.SourceKind switch
            {
                CharacterWaterSourceKind.Facility =>
                    MigratedProducerOutcomeIds.FacilityKind,
                CharacterWaterSourceKind.ItemInstance =>
                    MigratedProducerOutcomeIds.ItemKind,
                _ => MigratedProducerOutcomeIds.WaterSourceKind
            },
            evidence.SourceId);
        return new MigratedProducerOutcomeDetails(
            new[]
            {
                Subject(actor, evidence.CharacterDisplayName,
                    MigratedProducerOutcomeIds.ActorRole),
                Subject(source, evidence.SourceId,
                    evidence.SourceKind == CharacterWaterSourceKind.Facility
                        ? MigratedProducerOutcomeIds.FacilityRole
                        : MigratedProducerOutcomeIds.SourceRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.QuantityMetric,
                    evidence.Consumed,
                    MigratedProducerOutcomeIds.CountUnit,
                    source),
                Metric(MigratedProducerOutcomeIds.ThirstBeforeMetric,
                    evidence.ThirstBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.ThirstAfterMetric,
                    evidence.ThirstAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HealthBeforeMetric,
                    evidence.HealthBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HealthAfterMetric,
                    evidence.HealthAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HygieneBeforeMetric,
                    evidence.HygieneBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HygieneAfterMetric,
                    evidence.HygieneAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.InfectionBeforeMetric,
                    evidence.InfectionBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.InfectionAfterMetric,
                    evidence.InfectionAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodBeforeMetric,
                    evidence.MoodBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodAfterMetric,
                    evidence.MoodAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.IdentityFact, evidence.SourceId),
                Fact(MigratedProducerOutcomeIds.WaterQualityFact,
                    evidence.Quality.ToString()),
                Fact(MigratedProducerOutcomeIds.PathogenFact,
                    string.IsNullOrWhiteSpace(evidence.PathogenDiseaseId)
                        ? "none"
                        : evidence.PathogenDiseaseId)
            },
            summary,
            causation);
    }

    public static MigratedProducerOutcomeDetails FoulWaterConsequence(
        in CharacterWaterOutcomeEvidence evidence,
        string rootOperationId)
    {
        if (evidence.Quality == WorldWaterQuality.Clean)
            throw new ArgumentException("Clean water cannot create a foul-water consequence.");
        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            evidence.CharacterId.Value);
        return new MigratedProducerOutcomeDetails(
            new[]
            {
                Subject(actor, evidence.CharacterDisplayName,
                    MigratedProducerOutcomeIds.ActorRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.HealthBeforeMetric,
                    evidence.HealthBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HealthAfterMetric,
                    evidence.HealthAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HygieneBeforeMetric,
                    evidence.HygieneBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.HygieneAfterMetric,
                    evidence.HygieneAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.InfectionBeforeMetric,
                    evidence.InfectionBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.InfectionAfterMetric,
                    evidence.InfectionAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodBeforeMetric,
                    evidence.MoodBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor),
                Metric(MigratedProducerOutcomeIds.MoodAfterMetric,
                    evidence.MoodAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.WaterQualityFact,
                    evidence.Quality.ToString()),
                Fact(MigratedProducerOutcomeIds.PathogenFact,
                    string.IsNullOrWhiteSpace(evidence.PathogenDiseaseId)
                        ? "unknown"
                        : evidence.PathogenDiseaseId)
            },
            "오염수 후속 피해: health="
                + evidence.HealthBefore.ToString("0.###", CultureInfo.InvariantCulture)
                + "->"
                + evidence.HealthAfter.ToString("0.###", CultureInfo.InvariantCulture)
                + "; infection="
                + evidence.InfectionBefore.ToString("0.###", CultureInfo.InvariantCulture)
                + "->"
                + evidence.InfectionAfter.ToString("0.###", CultureInfo.InvariantCulture),
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(rootOperationId),
                "foul-water-consequence-of-consumption"));
    }

    public static MigratedProducerOutcomeDetails Rest(
        CharacterId characterId,
        string displayName,
        float before,
        float after,
        IReadOnlyList<string> activeConditionIds,
        string summary)
    {
        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            characterId.Value);
        string conditions = string.Join(",",
            (activeConditionIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));
        return new MigratedProducerOutcomeDetails(
            new[]
            {
                Subject(actor, displayName, MigratedProducerOutcomeIds.ActorRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.SleepBeforeMetric, before,
                    MigratedProducerOutcomeIds.PointUnit, actor),
                Metric(MigratedProducerOutcomeIds.SleepAfterMetric, after,
                    MigratedProducerOutcomeIds.PointUnit, actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.ActiveConditionsFact,
                    conditions.Length == 0 ? "none" : conditions)
            },
            summary);
    }

    public static MigratedProducerOutcomeDetails Proficiency(
        CharacterProficiencyAwardCommitReceipt receipt,
        string summary)
    {
        GameplayEntityId actor = new(
            MigratedProducerOutcomeIds.CharacterKind,
            receipt.CharacterId.Value);
        GameplayEntityId proficiency = new(
            MigratedProducerOutcomeIds.ProficiencyKind,
            receipt.ProficiencyId.Value);
        return new MigratedProducerOutcomeDetails(
            new[]
            {
                Subject(actor, receipt.CharacterId.Value,
                    MigratedProducerOutcomeIds.ActorRole),
                Subject(proficiency, receipt.ProficiencyId.Value,
                    MigratedProducerOutcomeIds.SourceRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.CurrentExperienceBeforeMetric,
                    receipt.BeforeCurrentMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.CurrentExperienceAfterMetric,
                    receipt.AfterCurrentMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.LifetimeExperienceBeforeMetric,
                    receipt.BeforeLifetimeMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.LifetimeExperienceAfterMetric,
                    receipt.AfterLifetimeMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.RankBeforeMetric,
                    (int)receipt.BeforeRank,
                    MigratedProducerOutcomeIds.EnumUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.RankAfterMetric,
                    (int)receipt.AfterRank,
                    MigratedProducerOutcomeIds.EnumUnit,
                    proficiency),
                Metric(MigratedProducerOutcomeIds.AbsoluteHourMetric,
                    receipt.AbsoluteHour,
                    MigratedProducerOutcomeIds.HourUnit,
                    actor)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.ProficiencyFact,
                    receipt.ProficiencyId.Value),
                Fact(MigratedProducerOutcomeIds.AwardKindFact,
                    receipt.AwardKind.ToString()),
                Fact(MigratedProducerOutcomeIds.SourceOperationFact,
                    receipt.CausalSourceOperationId)
            },
            summary,
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(receipt.CausalSourceOperationId),
                "proficiency-award-from-source-operation"));
    }

    private static MigratedProducerOutcomeSubject Subject(
        GameplayEntityId id,
        string display,
        GameplayRoleId role) => new(
        id.Kind,
        id.Value,
        string.IsNullOrWhiteSpace(display) ? id.Value : display,
        role);

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId id,
        string value) => new(id, RequireText(value, id.Value));

    private static string RequireText(string value, string label)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException(label + " is required.");
        return normalized;
    }
}
