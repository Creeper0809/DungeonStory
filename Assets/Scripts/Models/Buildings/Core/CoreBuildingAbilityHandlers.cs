using System;

namespace DungeonStory.Buildings
{
    public readonly struct BuildingCoreAbilityWorkContext
    {
        public BuildingCoreAbilityWorkContext(
            IBuildingVisitorPort actor,
            IBuildingCoreWorkTargetPort target,
            string workTypeId)
        {
            Actor = actor;
            Target = target;
            WorkTypeId = workTypeId?.Trim() ?? string.Empty;
        }

        public IBuildingVisitorPort Actor { get; }
        public IBuildingCoreWorkTargetPort Target { get; }
        public string WorkTypeId { get; }
    }

    public interface IBuildingCoreWorkTargetPort : IBuildingWorldEntryPort
    {
        string DisplayName { get; }
        bool IsExteriorZone { get; }
        string ExteriorZoneId { get; }
        float ReceptionReadiness { get; }
        float PatrolReadiness { get; }
        float Cleanliness { get; }
        float Damage { get; }
        void ApplyReceptionWork(float readinessGain, float impressionBonus);
        void ApplyPatrolWork(float readinessGain, float detectionBonus);
        void RecordOutdoorRest();
        void ApplyExteriorCleanWork(float amount);
        void ApplyExteriorRepairWork(float amount);
    }

    public interface IBuildingCoreFacilityEffectsPort
    {
        int ApplyProduction(
            IBuildingVisitorPort actor,
            IBuildingCoreWorkTargetPort target,
            ProductionBuildingAbilityWork ability,
            string workTypeId,
            float evolutionOutputMultiplier);

        int ApplyCleaning(
            IBuildingVisitorPort actor,
            IBuildingCoreWorkTargetPort target,
            CleaningBuildingAbilityWork ability,
            string workTypeId);

        int ApplySecurity(
            IBuildingVisitorPort actor,
            IBuildingCoreWorkTargetPort target,
            SecurityBuildingAbilityWork ability,
            string workTypeId);
    }

    public readonly struct ProductionBuildingAbilityWork
    {
        public ProductionBuildingAbilityWork(
            string abilityId,
            StockCategory outputCategory,
            int amount)
        {
            AbilityId = abilityId?.Trim() ?? string.Empty;
            OutputCategory = outputCategory;
            Amount = amount;
        }

        public string AbilityId { get; }
        public StockCategory OutputCategory { get; }
        public int Amount { get; }
    }

    public readonly struct CleaningBuildingAbilityWork
    {
        public CleaningBuildingAbilityWork(float restoredCleanliness)
        {
            RestoredCleanliness = restoredCleanliness;
        }

        public float RestoredCleanliness { get; }
    }

    public readonly struct SecurityBuildingAbilityWork
    {
        public SecurityBuildingAbilityWork(
            int maxAlarmCharges,
            int chargesPerGuardWork,
            string abilityId)
        {
            MaxAlarmCharges = maxAlarmCharges;
            ChargesPerGuardWork = chargesPerGuardWork;
            AbilityId = abilityId?.Trim() ?? string.Empty;
        }

        public int MaxAlarmCharges { get; }
        public int ChargesPerGuardWork { get; }
        public string AbilityId { get; }
    }

    public readonly struct ReceptionBuildingAbilityWork
    {
        public ReceptionBuildingAbilityWork(
            float readinessGain,
            float firstImpressionBonus,
            float moodBonus,
            float moodDurationSeconds)
        {
            ReadinessGain = readinessGain;
            FirstImpressionBonus = firstImpressionBonus;
            MoodBonus = moodBonus;
            MoodDurationSeconds = moodDurationSeconds;
        }

        public float ReadinessGain { get; }
        public float FirstImpressionBonus { get; }
        public float MoodBonus { get; }
        public float MoodDurationSeconds { get; }
    }

    public readonly struct PatrolPostBuildingAbilityWork
    {
        public PatrolPostBuildingAbilityWork(
            float patrolReadinessGain,
            float incidentDetectionBonus)
        {
            PatrolReadinessGain = patrolReadinessGain;
            IncidentDetectionBonus = incidentDetectionBonus;
        }

        public float PatrolReadinessGain { get; }
        public float IncidentDetectionBonus { get; }
    }

    public readonly struct OutdoorRestBuildingAbilityWork
    {
        public OutdoorRestBuildingAbilityWork(
            float moodBonus,
            float stressRecovery,
            float moodDurationSeconds)
        {
            MoodBonus = moodBonus;
            StressRecovery = stressRecovery;
            MoodDurationSeconds = moodDurationSeconds;
        }

        public float MoodBonus { get; }
        public float StressRecovery { get; }
        public float MoodDurationSeconds { get; }
    }

    public readonly struct ExteriorMaintenanceBuildingAbilityWork
    {
        public ExteriorMaintenanceBuildingAbilityWork(
            float cleanlinessGain,
            float damageReduction)
        {
            CleanlinessGain = cleanlinessGain;
            DamageReduction = damageReduction;
        }

        public float CleanlinessGain { get; }
        public float DamageReduction { get; }
    }

    public sealed class ProductionBuildingAbilityHandler
    {
        private const string OperateWorkType = "work:operate";
        private const string ResearchWorkType = "work:research";
        private readonly IBuildingCoreFacilityEffectsPort effects;

        public ProductionBuildingAbilityHandler(IBuildingCoreFacilityEffectsPort effects)
        {
            this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public int Apply(
            ProductionBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context,
            float evolutionOutputMultiplier)
        {
            if (context.Target == null
                || ability.AbilityId.Length == 0
                || ability.Amount <= 0
                || (context.WorkTypeId != OperateWorkType
                    && context.WorkTypeId != ResearchWorkType))
            {
                return 0;
            }

            return effects.ApplyProduction(
                context.Actor,
                context.Target,
                ability,
                context.WorkTypeId,
                evolutionOutputMultiplier);
        }
    }

    public sealed class CleaningBuildingAbilityHandler
    {
        private const string CleanWorkType = "work:clean";
        private readonly IBuildingCoreFacilityEffectsPort effects;

        public CleaningBuildingAbilityHandler(IBuildingCoreFacilityEffectsPort effects)
        {
            this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public int Apply(
            CleaningBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            if (context.Target == null
                || context.WorkTypeId != CleanWorkType)
            {
                return 0;
            }

            return effects.ApplyCleaning(
                context.Actor,
                context.Target,
                ability,
                context.WorkTypeId);
        }
    }

    public sealed class SecurityBuildingAbilityHandler
    {
        private const string GuardWorkType = "work:guard";
        private readonly IBuildingCoreFacilityEffectsPort effects;

        public SecurityBuildingAbilityHandler(IBuildingCoreFacilityEffectsPort effects)
        {
            this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public int Apply(
            SecurityBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            if (context.Target == null
                || ability.AbilityId.Length == 0
                || context.WorkTypeId != GuardWorkType)
            {
                return 0;
            }

            return effects.ApplySecurity(
                context.Actor,
                context.Target,
                ability,
                context.WorkTypeId);
        }
    }

    public sealed class ReceptionBuildingAbilityHandler
    {
        private const string ReceptionWorkType = "work:reception";

        public int Apply(
            ReceptionBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            IBuildingCoreWorkTargetPort target = context.Target;
            if (target == null
                || !target.IsExteriorZone
                || context.WorkTypeId != ReceptionWorkType)
            {
                return 0;
            }

            target.ApplyReceptionWork(
                ability.ReadinessGain,
                ability.FirstImpressionBonus);
            if (context.Actor != null && Math.Abs(ability.MoodBonus) > 0.0001f)
            {
                context.Actor.ApplyMoodFactor(
                    $"exterior:reception:{target.ExteriorZoneId}",
                    "방문객 맞이를 마침",
                    ability.MoodBonus,
                    ability.MoodDurationSeconds,
                    1);
            }

            BuildingAbilityWorkActivityRecorder.Record(
                context,
                $"{target.DisplayName}에서 방문객 맞이 준비를 마쳤다.",
                "exterior-reception",
                target.ReceptionReadiness);
            return 0;
        }
    }

    public sealed class PatrolPostBuildingAbilityHandler
    {
        private const string GuardWorkType = "work:guard";

        public int Apply(
            PatrolPostBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            IBuildingCoreWorkTargetPort target = context.Target;
            if (target == null
                || !target.IsExteriorZone
                || context.WorkTypeId != GuardWorkType)
            {
                return 0;
            }

            target.ApplyPatrolWork(
                ability.PatrolReadinessGain,
                ability.IncidentDetectionBonus);
            BuildingAbilityWorkActivityRecorder.Record(
                context,
                $"{target.DisplayName} 순찰을 마쳐 외부 동선을 안전하게 했다.",
                "exterior-patrol",
                target.PatrolReadiness);
            return 0;
        }
    }

    public sealed class OutdoorRestBuildingAbilityHandler
    {
        private const string RestWorkType = "work:rest";

        public int Apply(
            OutdoorRestBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            IBuildingCoreWorkTargetPort target = context.Target;
            if (target == null
                || !target.IsExteriorZone
                || context.WorkTypeId != RestWorkType)
            {
                return 0;
            }

            context.Actor?.ApplyMoodFactor(
                $"exterior:rest:{target.ExteriorZoneId}",
                "바깥 공기를 쐼",
                ability.MoodBonus,
                ability.MoodDurationSeconds,
                1);
            context.Actor?.ApplyExpeditionRecovery(
                0f,
                0f,
                ability.StressRecovery);
            target.RecordOutdoorRest();
            BuildingAbilityWorkActivityRecorder.Record(
                context,
                $"{target.DisplayName}에서 잠깐 숨을 돌렸다.",
                "exterior-rest",
                ability.MoodBonus);
            return 0;
        }
    }

    public sealed class ExteriorMaintenanceBuildingAbilityHandler
    {
        private const string CleanWorkType = "work:clean";
        private const string RepairWorkType = "work:repair";

        public int Apply(
            ExteriorMaintenanceBuildingAbilityWork ability,
            BuildingCoreAbilityWorkContext context)
        {
            IBuildingCoreWorkTargetPort target = context.Target;
            if (target == null
                || !target.IsExteriorZone
                || (context.WorkTypeId != CleanWorkType
                    && context.WorkTypeId != RepairWorkType))
            {
                return 0;
            }

            if (context.WorkTypeId == CleanWorkType)
            {
                target.ApplyExteriorCleanWork(ability.CleanlinessGain);
                BuildingAbilityWorkActivityRecorder.Record(
                    context,
                    $"{target.DisplayName} 주변을 치워 외부 동선을 깨끗하게 했다.",
                    "exterior-clean",
                    target.Cleanliness);
                return 0;
            }

            target.ApplyExteriorRepairWork(ability.DamageReduction);
            BuildingAbilityWorkActivityRecorder.Record(
                context,
                $"{target.DisplayName} 주변 손상을 보수했다.",
                "exterior-repair",
                target.Damage);
            return 0;
        }
    }

    internal static class BuildingAbilityWorkActivityRecorder
    {
        public static void Record(
            BuildingCoreAbilityWorkContext context,
            string factText,
            string reasonCode,
            float value)
        {
            context.Actor?.RecordActivity(
                context.Target,
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Work,
                    BuildingActivityOutcomes.Completed,
                    factText,
                    context.WorkTypeId,
                    string.Empty,
                    reasonCode,
                    value,
                    0,
                    false));
        }
    }
}
