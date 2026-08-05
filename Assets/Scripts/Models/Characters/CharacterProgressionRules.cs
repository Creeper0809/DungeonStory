using System;
using System.Collections.Generic;

namespace DungeonStory.Characters
{
    public readonly struct CharacterProgressionTransition
    {
        public CharacterProgressionTransition(
            int previousLevel,
            int level,
            int currentExperience,
            int[] reachedLevels)
        {
            PreviousLevel = previousLevel;
            Level = level;
            CurrentExperience = currentExperience;
            ReachedLevels = reachedLevels ?? Array.Empty<int>();
        }

        public int PreviousLevel { get; }
        public int Level { get; }
        public int CurrentExperience { get; }
        public IReadOnlyList<int> ReachedLevels { get; }
        public int LevelDelta => Level - PreviousLevel;
        public bool HasLevelChanged => Level != PreviousLevel;
    }

    public static class CharacterProgressionRules
    {
        public const int MaxLevel = 50;

        public static int GetExperienceRequired(int currentLevel)
        {
            int normalizedLevel = Clamp(currentLevel, 1, MaxLevel - 1);
            return 20 + ((normalizedLevel - 1) / 10) * 5;
        }

        public static float GetExperienceRatio(int currentLevel, int currentExperience)
        {
            if (currentLevel >= MaxLevel)
            {
                return 1f;
            }

            int required = Math.Max(1, GetExperienceRequired(currentLevel));
            int normalizedExperience = Math.Max(0, currentExperience);
            return Math.Min(1f, normalizedExperience / (float)required);
        }

        public static CharacterProgressionTransition AddExperience(
            int currentLevel,
            int currentExperience,
            int amount)
        {
            int level = Clamp(currentLevel, 1, MaxLevel);
            int previousLevel = level;
            if (amount <= 0 || level >= MaxLevel)
            {
                return new CharacterProgressionTransition(
                    previousLevel,
                    level,
                    currentExperience,
                    Array.Empty<int>());
            }

            int experience = currentExperience + amount;
            List<int> reachedLevels = new List<int>();
            while (level < MaxLevel && experience >= GetExperienceRequired(level))
            {
                experience -= GetExperienceRequired(level);
                level++;
                reachedLevels.Add(level);
            }

            if (level >= MaxLevel)
            {
                level = MaxLevel;
                experience = 0;
            }

            return new CharacterProgressionTransition(
                previousLevel,
                level,
                experience,
                reachedLevels.ToArray());
        }

        public static CharacterProgressionTransition EnsureMinimumLevel(
            int currentLevel,
            int currentExperience,
            int targetLevel)
        {
            int level = Clamp(currentLevel, 1, MaxLevel);
            int previousLevel = level;
            int clampedTarget = Clamp(targetLevel, 1, MaxLevel);
            if (level >= clampedTarget)
            {
                return new CharacterProgressionTransition(
                    previousLevel,
                    level,
                    currentExperience,
                    Array.Empty<int>());
            }

            int[] reachedLevels = new int[clampedTarget - level];
            for (int index = 0; index < reachedLevels.Length; index++)
            {
                reachedLevels[index] = level + index + 1;
            }

            return new CharacterProgressionTransition(
                previousLevel,
                clampedTarget,
                0,
                reachedLevels);
        }

        public static CharacterProgressionTransition NormalizeRestoredState(
            int restoredLevel,
            int restoredExperience)
        {
            int level = Clamp(restoredLevel, 1, MaxLevel);
            int experience = level >= MaxLevel
                ? 0
                : Clamp(restoredExperience, 0, GetExperienceRequired(level) - 1);
            return new CharacterProgressionTransition(
                level,
                level,
                experience,
                Array.Empty<int>());
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }
}
