using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Content.CoreSession
{
    public interface ICoreSessionRulesProvider
    {
        CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    public sealed class CoreRehearsalRule
    {
        public CoreRehearsalRule(
            int day,
            float powerMultiplier,
            float ownerDamageMultiplier,
            float retreatHealthRatio)
        {
            Day = day;
            PowerMultiplier = powerMultiplier;
            OwnerDamageMultiplier = ownerDamageMultiplier;
            RetreatHealthRatio = retreatHealthRatio;
        }

        public int Day { get; }
        public float PowerMultiplier { get; }
        public float OwnerDamageMultiplier { get; }
        public float RetreatHealthRatio { get; }
    }

    public sealed class CoreExternalProblemBand
    {
        public CoreExternalProblemBand(
            int lastDayInclusive,
            int maximumConcurrentProblems,
            IEnumerable<int> allowedIncidentKinds)
        {
            LastDayInclusive = lastDayInclusive;
            MaximumConcurrentProblems = maximumConcurrentProblems;
            AllowedIncidentKinds = Array.AsReadOnly(
                (allowedIncidentKinds ?? Array.Empty<int>()).ToArray());
        }

        public int LastDayInclusive { get; }
        public int MaximumConcurrentProblems { get; }
        public IReadOnlyList<int> AllowedIncidentKinds { get; }
    }

    public sealed class CoreServiceResearchRule
    {
        public CoreServiceResearchRule(
            int serviceCategory,
            int operationMode,
            string researchId)
        {
            ServiceCategory = serviceCategory;
            OperationMode = operationMode;
            ResearchId = researchId
                ?? throw new ArgumentNullException(nameof(researchId));
        }

        public int ServiceCategory { get; }
        public int OperationMode { get; }
        public string ResearchId { get; }
    }

    public sealed class CoreRunPacingRules
    {
        public CoreRunPacingRules(
            int randomInvasionStartDay,
            int growthStartDay,
            int escalationStartDay,
            int endlessDefenseStartDay,
            int firstBossDay,
            int bossIntervalDays,
            IEnumerable<CoreRehearsalRule> rehearsals,
            IEnumerable<CoreExternalProblemBand> externalProblemBands)
        {
            RandomInvasionStartDay = randomInvasionStartDay;
            GrowthStartDay = growthStartDay;
            EscalationStartDay = escalationStartDay;
            EndlessDefenseStartDay = endlessDefenseStartDay;
            FirstBossDay = firstBossDay;
            BossIntervalDays = bossIntervalDays;
            Rehearsals = Array.AsReadOnly(
                (rehearsals ?? throw new ArgumentNullException(nameof(rehearsals)))
                .ToArray());
            ExternalProblemBands = Array.AsReadOnly(
                (externalProblemBands
                    ?? throw new ArgumentNullException(nameof(externalProblemBands)))
                .ToArray());
        }

        public int RandomInvasionStartDay { get; }
        public int GrowthStartDay { get; }
        public int EscalationStartDay { get; }
        public int EndlessDefenseStartDay { get; }
        public int FirstBossDay { get; }
        public int BossIntervalDays { get; }
        public IReadOnlyList<CoreRehearsalRule> Rehearsals { get; }
        public IReadOnlyList<CoreExternalProblemBand> ExternalProblemBands { get; }
    }

    public sealed class CoreExternalInfluenceRules
    {
        public CoreExternalInfluenceRules(
            float renownIntelCost,
            int goldIntelCost,
            float scoutingIntelCost,
            float dreadDefenseCost,
            float ecologyRaidCountdownSeconds,
            float maximumRumorMitigation,
            int maximumRumorRenownCost,
            int maximumRumorGoldCost)
        {
            RenownIntelCost = renownIntelCost;
            GoldIntelCost = goldIntelCost;
            ScoutingIntelCost = scoutingIntelCost;
            DreadDefenseCost = dreadDefenseCost;
            EcologyRaidCountdownSeconds = ecologyRaidCountdownSeconds;
            MaximumRumorMitigation = maximumRumorMitigation;
            MaximumRumorRenownCost = maximumRumorRenownCost;
            MaximumRumorGoldCost = maximumRumorGoldCost;
        }

        public float RenownIntelCost { get; }
        public int GoldIntelCost { get; }
        public float ScoutingIntelCost { get; }
        public float DreadDefenseCost { get; }
        public float EcologyRaidCountdownSeconds { get; }
        public float MaximumRumorMitigation { get; }
        public int MaximumRumorRenownCost { get; }
        public int MaximumRumorGoldCost { get; }
    }

    public sealed class CoreDebugAndServiceRules
    {
        public CoreDebugAndServiceRules(
            int debugHistoryLimit,
            IEnumerable<CoreServiceResearchRule> serviceResearch)
        {
            DebugHistoryLimit = debugHistoryLimit;
            ServiceResearch = Array.AsReadOnly(
                (serviceResearch
                    ?? throw new ArgumentNullException(nameof(serviceResearch)))
                .ToArray());
        }

        public int DebugHistoryLimit { get; }
        public IReadOnlyList<CoreServiceResearchRule> ServiceResearch { get; }
    }

    /// <summary>
    /// Immutable runtime projection of the authored CoreSessionRulesSO asset.
    /// Runtime services retain this value object, never the ScriptableObject.
    /// </summary>
    public sealed class CoreSessionRulesDefinition
    {
        public CoreSessionRulesDefinition(
            CoreRunPacingRules runPacing,
            CoreExternalInfluenceRules externalInfluence,
            CoreDebugAndServiceRules debugAndServices)
        {
            RunPacing = runPacing
                ?? throw new ArgumentNullException(nameof(runPacing));
            ExternalInfluence = externalInfluence
                ?? throw new ArgumentNullException(nameof(externalInfluence));
            DebugAndServices = debugAndServices
                ?? throw new ArgumentNullException(nameof(debugAndServices));
        }

        public CoreRunPacingRules RunPacing { get; }
        public CoreExternalInfluenceRules ExternalInfluence { get; }
        public CoreDebugAndServiceRules DebugAndServices { get; }
        public int RandomInvasionStartDay => RunPacing.RandomInvasionStartDay;
        public int GrowthStartDay => RunPacing.GrowthStartDay;
        public int EscalationStartDay => RunPacing.EscalationStartDay;
        public int EndlessDefenseStartDay => RunPacing.EndlessDefenseStartDay;
        public int FirstBossDay => RunPacing.FirstBossDay;
        public int BossIntervalDays => RunPacing.BossIntervalDays;
        public IReadOnlyList<CoreRehearsalRule> Rehearsals =>
            RunPacing.Rehearsals;
        public IReadOnlyList<CoreExternalProblemBand> ExternalProblemBands =>
            RunPacing.ExternalProblemBands;
        public float RenownIntelCost => ExternalInfluence.RenownIntelCost;
        public int GoldIntelCost => ExternalInfluence.GoldIntelCost;
        public float ScoutingIntelCost => ExternalInfluence.ScoutingIntelCost;
        public float DreadDefenseCost => ExternalInfluence.DreadDefenseCost;
        public float EcologyRaidCountdownSeconds =>
            ExternalInfluence.EcologyRaidCountdownSeconds;
        public float MaximumRumorMitigation =>
            ExternalInfluence.MaximumRumorMitigation;
        public int MaximumRumorRenownCost =>
            ExternalInfluence.MaximumRumorRenownCost;
        public int MaximumRumorGoldCost =>
            ExternalInfluence.MaximumRumorGoldCost;
        public int DebugHistoryLimit => DebugAndServices.DebugHistoryLimit;
        public IReadOnlyList<CoreServiceResearchRule> ServiceResearch =>
            DebugAndServices.ServiceResearch;

        public bool TryGetRehearsal(
            int day,
            out CoreRehearsalRule rule)
        {
            rule = Rehearsals.FirstOrDefault(candidate =>
                candidate != null && candidate.Day == day);
            return rule != null;
        }

        public bool TryGetRequiredServiceResearch(
            int serviceCategory,
            int operationMode,
            out string researchId)
        {
            CoreServiceResearchRule rule = ServiceResearch.FirstOrDefault(
                candidate => candidate != null
                    && candidate.ServiceCategory == serviceCategory
                    && candidate.OperationMode == operationMode);
            researchId = rule?.ResearchId ?? string.Empty;
            return researchId.Length > 0;
        }
    }
}
