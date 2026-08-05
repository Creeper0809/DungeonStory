using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class SurvivalMealLedger
{
    private const int MaximumMealEntries = 512;
    private readonly IGameEventBus gameEventBus;

    public SurvivalMealLedger(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public int GetConsumed(DungeonSurvivalSaveData state, int day)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        return state.mealLedger
            .Where(entry => entry != null && entry.day == day)
            .Sum(entry => Mathf.Max(0, entry.amount));
    }

    public int GetConsumed(DungeonSurvivalSaveData state, string characterId, int day)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        string normalizedId = characterId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return 0;
        }

        return state.mealLedger
            .Where(entry => entry != null
                && entry.day == day
                && string.Equals(entry.characterId, normalizedId, StringComparison.Ordinal))
            .Sum(entry => Mathf.Max(0, entry.amount));
    }

    public IReadOnlyList<CharacterMealLedgerSaveData> GetRecent(
        DungeonSurvivalSaveData state,
        int maximumCount)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        return state.mealLedger
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.day)
            .ThenByDescending(entry => entry.mealId, StringComparer.Ordinal)
            .Take(Mathf.Clamp(maximumCount, 1, 100))
            .Select(SurvivalFoodStatePersistence.CloneMeal)
            .ToArray();
    }

    public void Record(
        DungeonSurvivalSaveData state,
        ref long sequence,
        CharacterActor consumer,
        BuildableObject facility,
        string itemId,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        bool policyViolation,
        bool contaminated)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        string characterId = CharacterPersistentIdentity.Require(consumer).Value;
        string facilityId = facility.RequirePersistentInstanceId().Value;
        int day = Mathf.Max(1, state.lastProcessedDay);
        state.lastProcessedDay = day;
        const int amount = 1;
        string mealId = $"meal:{day}:{characterId}:{++sequence}";
        state.mealLedger.Add(new CharacterMealLedgerSaveData
        {
            mealId = mealId,
            characterId = characterId,
            facilityId = facilityId,
            itemId = itemId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            dietClass = dietClass,
            quality = quality,
            nutrition = Mathf.Max(0f, nutrition),
            policyViolation = policyViolation,
            contaminated = contaminated,
            day = day,
            amount = amount
        });

        int removeCount = state.mealLedger.Count - MaximumMealEntries;
        if (removeCount > 0)
        {
            state.mealLedger.RemoveRange(0, removeCount);
        }

        gameEventBus.Publish(new CharacterMealConsumedEvent(
            mealId,
            characterId,
            facilityId,
            itemId,
            displayName,
            dietClass,
            quality,
            nutrition,
            policyViolation,
            contaminated,
            day,
            amount));
    }
}
