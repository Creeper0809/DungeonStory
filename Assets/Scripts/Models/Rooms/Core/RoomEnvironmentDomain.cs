using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Rooms
{
    public enum RoomEnvironmentStatus
    {
        Usable,
        OpenBoundary,
        MissingDoor,
        SelfContained
    }

    public sealed class RoomEnvironmentFormulaSettings
    {
        public RoomEnvironmentFormulaSettings(
            float spaciousAreaMinimum,
            float spaciousAreaMaximum,
            float spaciousAreaWeight,
            float spaciousFreeCellWeight,
            float beautyBaseline,
            float luxuryMultiplier,
            float beautyDamagePenalty,
            float beautyCrowdingThreshold,
            float beautyCrowdingPenalty,
            float cleanlinessBaseline,
            float hygieneContributionMaximum,
            float cleanStreakContribution,
            float cleanStreakContributionMaximum,
            float cleanlinessDamagePenalty,
            float cleanlinessCrowdingThreshold,
            float cleanlinessCrowdingPenalty,
            float impressivenessBeautyWeight,
            float impressivenessSpaciousnessWeight,
            float impressivenessCleanlinessWeight,
            float impressivenessQualityWeight)
        {
            SpaciousAreaMinimum = spaciousAreaMinimum;
            SpaciousAreaMaximum = Math.Max(spaciousAreaMinimum + 1f, spaciousAreaMaximum);
            SpaciousAreaWeight = spaciousAreaWeight;
            SpaciousFreeCellWeight = spaciousFreeCellWeight;
            BeautyBaseline = beautyBaseline;
            LuxuryMultiplier = luxuryMultiplier;
            BeautyDamagePenalty = beautyDamagePenalty;
            BeautyCrowdingThreshold = beautyCrowdingThreshold;
            BeautyCrowdingPenalty = beautyCrowdingPenalty;
            CleanlinessBaseline = cleanlinessBaseline;
            HygieneContributionMaximum = hygieneContributionMaximum;
            CleanStreakContribution = cleanStreakContribution;
            CleanStreakContributionMaximum = cleanStreakContributionMaximum;
            CleanlinessDamagePenalty = cleanlinessDamagePenalty;
            CleanlinessCrowdingThreshold = cleanlinessCrowdingThreshold;
            CleanlinessCrowdingPenalty = cleanlinessCrowdingPenalty;
            ImpressivenessBeautyWeight = impressivenessBeautyWeight;
            ImpressivenessSpaciousnessWeight = impressivenessSpaciousnessWeight;
            ImpressivenessCleanlinessWeight = impressivenessCleanlinessWeight;
            ImpressivenessQualityWeight = impressivenessQualityWeight;
        }

        public float SpaciousAreaMinimum { get; }
        public float SpaciousAreaMaximum { get; }
        public float SpaciousAreaWeight { get; }
        public float SpaciousFreeCellWeight { get; }
        public float BeautyBaseline { get; }
        public float LuxuryMultiplier { get; }
        public float BeautyDamagePenalty { get; }
        public float BeautyCrowdingThreshold { get; }
        public float BeautyCrowdingPenalty { get; }
        public float CleanlinessBaseline { get; }
        public float HygieneContributionMaximum { get; }
        public float CleanStreakContribution { get; }
        public float CleanStreakContributionMaximum { get; }
        public float CleanlinessDamagePenalty { get; }
        public float CleanlinessCrowdingThreshold { get; }
        public float CleanlinessCrowdingPenalty { get; }
        public float ImpressivenessBeautyWeight { get; }
        public float ImpressivenessSpaciousnessWeight { get; }
        public float ImpressivenessCleanlinessWeight { get; }
        public float ImpressivenessQualityWeight { get; }
    }

    public readonly struct RoomEnvironmentFormulaInput
    {
        public RoomEnvironmentFormulaInput(
            int area,
            int occupiedCells,
            float luxury,
            float hygiene,
            int cleanServiceStreak,
            int damagedFixtures,
            int fixtureCount,
            float operationalCleanliness,
            float worldFilthPenalty,
            float qualityScore)
        {
            Area = Math.Max(1, area);
            OccupiedCells = Math.Max(0, occupiedCells);
            Luxury = luxury;
            Hygiene = hygiene;
            CleanServiceStreak = Math.Max(0, cleanServiceStreak);
            DamagedFixtures = Math.Max(0, damagedFixtures);
            FixtureCount = Math.Max(0, fixtureCount);
            OperationalCleanliness = operationalCleanliness;
            WorldFilthPenalty = Math.Max(0f, worldFilthPenalty);
            QualityScore = Mathf.Clamp01(qualityScore);
        }

        public int Area { get; }
        public int OccupiedCells { get; }
        public float Luxury { get; }
        public float Hygiene { get; }
        public int CleanServiceStreak { get; }
        public int DamagedFixtures { get; }
        public int FixtureCount { get; }
        public float OperationalCleanliness { get; }
        public float WorldFilthPenalty { get; }
        public float QualityScore { get; }
    }

    public readonly struct RoomEnvironmentMetrics
    {
        public RoomEnvironmentMetrics(float spaciousness, float beauty, float cleanliness, float impressiveness)
        {
            Spaciousness = Mathf.Clamp(spaciousness, 0f, 100f);
            Beauty = Mathf.Clamp(beauty, 0f, 100f);
            Cleanliness = Mathf.Clamp(cleanliness, 0f, 100f);
            Impressiveness = Mathf.Clamp(impressiveness, 0f, 100f);
        }

        public float Spaciousness { get; }
        public float Beauty { get; }
        public float Cleanliness { get; }
        public float Impressiveness { get; }
    }

    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomEnvironmentEvaluator")]
    public static class RoomEnvironmentFormula
    {
        public static RoomEnvironmentMetrics Evaluate(
            RoomEnvironmentFormulaInput input,
            RoomEnvironmentFormulaSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            float occupiedRatio = Mathf.Clamp01((float)input.OccupiedCells / input.Area);
            float freeCellRatio = 1f - occupiedRatio;
            float damagedRatio = input.FixtureCount > 0
                ? (float)input.DamagedFixtures / input.FixtureCount
                : 0f;
            float normalizedArea = Mathf.InverseLerp(
                settings.SpaciousAreaMinimum,
                settings.SpaciousAreaMaximum,
                input.Area);
            float spaciousness = 100f * (
                normalizedArea * settings.SpaciousAreaWeight
                + freeCellRatio * settings.SpaciousFreeCellWeight);
            float beauty = settings.BeautyBaseline
                + input.Luxury * settings.LuxuryMultiplier
                - damagedRatio * settings.BeautyDamagePenalty
                - Mathf.Max(0f, occupiedRatio - settings.BeautyCrowdingThreshold)
                    * settings.BeautyCrowdingPenalty;
            float cleanliness = settings.CleanlinessBaseline
                + Mathf.Min(settings.HygieneContributionMaximum, input.Hygiene)
                + Mathf.Min(
                    settings.CleanStreakContributionMaximum,
                    input.CleanServiceStreak * settings.CleanStreakContribution)
                - damagedRatio * settings.CleanlinessDamagePenalty
                - Mathf.Max(0f, occupiedRatio - settings.CleanlinessCrowdingThreshold)
                    * settings.CleanlinessCrowdingPenalty
                + (input.OperationalCleanliness - 50f) * 0.2f
                - input.WorldFilthPenalty;
            float impressiveness = beauty * settings.ImpressivenessBeautyWeight
                + spaciousness * settings.ImpressivenessSpaciousnessWeight
                + cleanliness * settings.ImpressivenessCleanlinessWeight
                + input.QualityScore * 100f * settings.ImpressivenessQualityWeight;
            return new RoomEnvironmentMetrics(spaciousness, beauty, cleanliness, impressiveness);
        }

        public static RoomEnvironmentStatus ResolveStatus(RoomInstance room)
        {
            if (room == null || room.IsSelfContained)
            {
                return RoomEnvironmentStatus.SelfContained;
            }

            if (room.OpenBoundaryCount > 0)
            {
                return RoomEnvironmentStatus.OpenBoundary;
            }

            return room.HasDoor ? RoomEnvironmentStatus.Usable : RoomEnvironmentStatus.MissingDoor;
        }
    }

    public enum RoomExperienceActivity
    {
        FacilityUse,
        Shopping,
        Work
    }

    public readonly struct RoomMoodDecision
    {
        public RoomMoodDecision(float impressionMood, float cleanlinessMood, float durationSeconds)
        {
            ImpressionMood = impressionMood;
            CleanlinessMood = cleanlinessMood;
            DurationSeconds = Math.Max(0f, durationSeconds);
        }

        public float ImpressionMood { get; }
        public float CleanlinessMood { get; }
        public float DurationSeconds { get; }
        public bool HasEffect => !Mathf.Approximately(ImpressionMood, 0f)
            || !Mathf.Approximately(CleanlinessMood, 0f);
    }

    public static class RoomExperienceRules
    {
        public static RoomMoodDecision Evaluate(
            float impressiveness,
            float cleanliness,
            float awfulMood,
            float poorMood,
            float goodMood,
            float excellentMood,
            float filthyMood,
            float dirtyMood,
            float cleanMood,
            float durationSeconds)
        {
            float impression = impressiveness < 20f ? awfulMood
                : impressiveness < 40f ? poorMood
                : impressiveness < 60f ? 0f
                : impressiveness < 80f ? goodMood
                : excellentMood;
            float clean = cleanliness < 20f ? filthyMood
                : cleanliness < 40f ? dirtyMood
                : cleanliness < 80f ? 0f
                : cleanMood;
            return new RoomMoodDecision(impression, clean, durationSeconds);
        }
    }
}
