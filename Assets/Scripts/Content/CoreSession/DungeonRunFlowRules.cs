using System;

namespace DungeonStory.Content.CoreSession
{
    public static class DungeonRunFlowRules
    {
        public static DungeonRunPhase ResolvePhaseForDay(
            int day,
            CoreSessionRulesDefinition rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (day >= rules.EndlessDefenseStartDay)
            {
                return DungeonRunPhase.EndlessDefense;
            }
            if (day >= rules.EscalationStartDay)
            {
                return DungeonRunPhase.Escalation;
            }
            if (day >= rules.GrowthStartDay)
            {
                return DungeonRunPhase.Growth;
            }
            return DungeonRunPhase.Preparation;
        }

        public static int ResolveBossCycleForDay(
            int day,
            CoreSessionRulesDefinition rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            return day < rules.FirstBossDay
                ? 0
                : 1 + Math.Max(
                    0,
                    (day - rules.FirstBossDay) / rules.BossIntervalDays);
        }
    }
}
