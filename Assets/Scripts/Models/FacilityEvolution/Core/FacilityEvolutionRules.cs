using System;
using System.Collections.Generic;

namespace DungeonStory.FacilityEvolution
{
    public static class FacilityEvolutionProgressionRules
    {
        public static float RequiredMastery(int generation) =>
            120f + 60f * Math.Max(0, generation);

        public static float ModificationWork(float baseConstructionWork, int generation) =>
            Math.Max(1f, baseConstructionWork)
            * (0.5f + 0.15f * (float)Math.Sqrt(Math.Max(0, generation) + 1f));

        public static float RecalibrationWork(float baseConstructionWork, int generation) =>
            ModificationWork(baseConstructionWork, generation) * 0.75f;

        public static float RelocationDismantleWork(float baseConstructionWork) =>
            Math.Max(1f, baseConstructionWork) * 0.25f;

        public static float RelocationReinstallWork(float baseConstructionWork) =>
            Math.Max(1f, baseConstructionWork) * 0.5f;
    }

    public static class FacilityEvolutionRecordRules
    {
        public static FacilityEvolutionRecordSnapshot ConsumeTokens(
            FacilityEvolutionRecordSnapshot record,
            IReadOnlyDictionary<string, int> requirements)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var tokens = new System.Collections.Generic.Dictionary<string, int>(record.Tokens, StringComparer.Ordinal);
            foreach (var requirement in requirements ?? new System.Collections.Generic.Dictionary<string, int>())
            {
                int required = Math.Max(1, requirement.Value);
                if (!tokens.TryGetValue(requirement.Key, out int current) || current < required)
                    throw new InvalidOperationException($"Insufficient facility evolution token '{requirement.Key}'.");
            }
            foreach (var requirement in requirements ?? new System.Collections.Generic.Dictionary<string, int>())
                tokens[requirement.Key] -= Math.Max(1, requirement.Value);
            return new FacilityEvolutionRecordSnapshot(record.Metrics, tokens, record.RecentEvents);
        }
    }
}
