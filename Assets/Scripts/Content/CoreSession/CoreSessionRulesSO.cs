using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonStory.Content.CoreSession
{
    [Serializable]
    public sealed class RehearsalRule
    {
        [Min(1)] public int day = 10;
        [Range(0.05f, 1f)] public float powerMultiplier = 0.25f;
        [Range(0f, 1f)] public float ownerDamageMultiplier = 0.2f;
        [Range(0f, 1f)] public float retreatHealthRatio = 0.55f;
    }

    [Serializable]
    public sealed class ExternalProblemBand
    {
        [Min(1)] public int lastDayInclusive = 3;
        [Min(0)] public int maximumConcurrentProblems;
        public List<int> allowedIncidentKinds = new();
    }

    [Serializable]
    public sealed class ServiceResearchRule
    {
        [Min(0)] public int serviceCategory;
        [Min(0)] public int operationMode;
        public string researchId = string.Empty;
    }

    [CreateAssetMenu(
        fileName = "CoreSessionRules",
        menuName = "DungeonStory/Content/Core Session Rules",
        order = -98)]
    public sealed class CoreSessionRulesSO : ScriptableObject
    {
        [Header("Run pacing")]
        [SerializeField, Min(1)] private int randomInvasionStartDay = 31;
        [SerializeField, Min(1)] private int growthStartDay = 4;
        [SerializeField, Min(1)] private int escalationStartDay = 10;
        [SerializeField, Min(1)] private int endlessDefenseStartDay = 40;
        [SerializeField, Min(1)] private int firstBossDay = 40;
        [SerializeField, Min(1)] private int bossIntervalDays = 10;
        [SerializeField] private List<RehearsalRule> rehearsals = new();
        [SerializeField] private List<ExternalProblemBand> externalProblemBands =
            new();

        [Header("External influence")]
        [SerializeField, Min(0f)] private float renownIntelCost = 10f;
        [SerializeField, Min(0)] private int goldIntelCost = 200;
        [SerializeField, Min(0f)] private float scoutingIntelCost = 60f;
        [SerializeField, Min(0f)] private float dreadDefenseCost = 15f;
        [SerializeField, Min(0.1f)] private float ecologyRaidCountdownSeconds = 60f;
        [SerializeField, Min(0.1f)] private float maximumRumorMitigation = 15f;
        [SerializeField, Min(0)] private int maximumRumorRenownCost = 10;
        [SerializeField, Min(0)] private int maximumRumorGoldCost = 200;

        [Header("Debug and services")]
        [SerializeField, Min(1)] private int debugHistoryLimit = 50;
        [SerializeField] private List<ServiceResearchRule> serviceResearch =
            new();

        public int RandomInvasionStartDay => randomInvasionStartDay;
        public int GrowthStartDay => growthStartDay;
        public int EscalationStartDay => escalationStartDay;
        public int EndlessDefenseStartDay => endlessDefenseStartDay;
        public int FirstBossDay => firstBossDay;
        public int BossIntervalDays => bossIntervalDays;
        public IReadOnlyList<RehearsalRule> Rehearsals => rehearsals;
        public IReadOnlyList<ExternalProblemBand> ExternalProblemBands =>
            externalProblemBands;
        public float RenownIntelCost => renownIntelCost;
        public int GoldIntelCost => goldIntelCost;
        public float ScoutingIntelCost => scoutingIntelCost;
        public float DreadDefenseCost => dreadDefenseCost;
        public float EcologyRaidCountdownSeconds =>
            ecologyRaidCountdownSeconds;
        public float MaximumRumorMitigation => maximumRumorMitigation;
        public int MaximumRumorRenownCost => maximumRumorRenownCost;
        public int MaximumRumorGoldCost => maximumRumorGoldCost;
        public int DebugHistoryLimit => debugHistoryLimit;
        public IReadOnlyList<ServiceResearchRule> ServiceResearch =>
            serviceResearch;

        public bool TryGetRehearsal(int day, out RehearsalRule rule)
        {
            rule = rehearsals.FirstOrDefault(candidate =>
                candidate != null && candidate.day == day);
            return rule != null;
        }

        public bool TryGetRequiredServiceResearch(
            int serviceCategory,
            int operationMode,
            out string researchId)
        {
            ServiceResearchRule rule = serviceResearch.FirstOrDefault(
                candidate => candidate != null
                    && candidate.serviceCategory == serviceCategory
                    && candidate.operationMode == operationMode);
            researchId = rule?.researchId?.Trim() ?? string.Empty;
            return researchId.Length > 0;
        }

        public CoreSessionRulesDefinition CreateRuntimeDefinition()
        {
            IReadOnlyList<string> errors = ValidateDefinition();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot project invalid core-session rules:\n"
                    + string.Join("\n", errors));
            }

            return new CoreSessionRulesDefinition(
                new CoreRunPacingRules(
                    randomInvasionStartDay,
                    growthStartDay,
                    escalationStartDay,
                    endlessDefenseStartDay,
                    firstBossDay,
                    bossIntervalDays,
                    rehearsals.Select(rule => new CoreRehearsalRule(
                        rule.day,
                        rule.powerMultiplier,
                        rule.ownerDamageMultiplier,
                        rule.retreatHealthRatio)),
                    externalProblemBands.Select(band =>
                        new CoreExternalProblemBand(
                            band.lastDayInclusive,
                            band.maximumConcurrentProblems,
                            band.allowedIncidentKinds))),
                new CoreExternalInfluenceRules(
                    renownIntelCost,
                    goldIntelCost,
                    scoutingIntelCost,
                    dreadDefenseCost,
                    ecologyRaidCountdownSeconds,
                    maximumRumorMitigation,
                    maximumRumorRenownCost,
                    maximumRumorGoldCost),
                new CoreDebugAndServiceRules(
                    debugHistoryLimit,
                    serviceResearch.Select(rule =>
                        new CoreServiceResearchRule(
                            rule.serviceCategory,
                            rule.operationMode,
                            rule.researchId))));
        }

        public IReadOnlyList<string> ValidateDefinition()
        {
            List<string> errors = new();
            if (!(1 <= growthStartDay
                    && growthStartDay < escalationStartDay
                    && escalationStartDay < endlessDefenseStartDay
                    && endlessDefenseStartDay <= firstBossDay)
                || randomInvasionStartDay <= escalationStartDay
                || bossIntervalDays <= 0)
            {
                errors.Add("Core-session day thresholds are inconsistent.");
            }

            int[] rehearsalDays = rehearsals
                .Where(rule => rule != null)
                .Select(rule => rule.day)
                .ToArray();
            if (rehearsalDays.Length != 3
                || rehearsalDays.Any(day => day <= 0)
                || rehearsalDays.Distinct().Count() != rehearsalDays.Length
                || !rehearsalDays.SequenceEqual(rehearsalDays.OrderBy(day => day)))
            {
                errors.Add("Rehearsal rules must be non-empty, unique, and ordered.");
            }
            if (rehearsals.Any(rule => rule == null
                    || rule.powerMultiplier < 0.05f
                    || rule.powerMultiplier > 1f
                    || rule.ownerDamageMultiplier < 0f
                    || rule.ownerDamageMultiplier > 1f
                    || rule.retreatHealthRatio < 0f
                    || rule.retreatHealthRatio > 1f))
            {
                errors.Add("Rehearsal multipliers are outside their authored ranges.");
            }

            int previousDay = 0;
            foreach (ExternalProblemBand band in externalProblemBands)
            {
                if (band == null
                    || band.lastDayInclusive <= previousDay
                    || band.maximumConcurrentProblems < 0
                    || band.allowedIncidentKinds == null
                    || band.allowedIncidentKinds.Any(kind => kind <= 0)
                    || band.allowedIncidentKinds.Distinct().Count()
                        != band.allowedIncidentKinds.Count)
                {
                    errors.Add("External-problem bands must be ordered and valid.");
                    break;
                }
                previousDay = band.lastDayInclusive;
            }
            if (externalProblemBands.Count == 0
                || externalProblemBands[^1] == null
                || externalProblemBands[^1].lastDayInclusive != int.MaxValue)
            {
                errors.Add(
                    "External-problem bands must cover every future game day.");
            }

            if (renownIntelCost <= 0f
                || goldIntelCost <= 0
                || scoutingIntelCost <= 0f
                || dreadDefenseCost <= 0f
                || ecologyRaidCountdownSeconds <= 0f
                || maximumRumorMitigation <= 0f
                || maximumRumorRenownCost <= 0
                || maximumRumorGoldCost <= 0
                || debugHistoryLimit <= 0)
            {
                errors.Add("Core-session costs and limits must be positive.");
            }

            if (serviceResearch.Any(rule => rule == null
                    || rule.serviceCategory < 0
                    || rule.serviceCategory > 4
                    || rule.operationMode < 1
                    || rule.operationMode > 2
                    || string.IsNullOrWhiteSpace(rule.researchId)
                    || !string.Equals(
                        rule.researchId,
                        rule.researchId.Trim(),
                        StringComparison.Ordinal))
                || serviceResearch
                    .Where(rule => rule != null)
                    .GroupBy(rule => (rule.serviceCategory, rule.operationMode))
                    .Any(group => group.Count() > 1))
            {
                errors.Add("Service research rules must be canonical and unique.");
            }

            return errors;
        }
    }
}
