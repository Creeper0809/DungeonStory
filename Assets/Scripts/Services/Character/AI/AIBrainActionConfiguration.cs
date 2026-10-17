using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal static class AIBrainActionConfiguration
{
    private const string WaitActionPath = "SO/AI/Action/Wait";
    private const string WorkActionPath = "SO/AI/Action/Work";
    private const string EatActionPath = "SO/AI/Action/Eat";
    private const string RestActionPath = "SO/AI/Action/Rest";
    private const string ToiletActionPath = "SO/AI/Action/Toilet";
    private const string HygieneActionPath = "SO/AI/Action/Hygiene";
    private const string RecreationActionPath = "SO/AI/Action/Recreation";
    private const string ShoppingActionPath = "SO/AI/Action/Shopping";
    private const string LookAroundActionPath = "SO/AI/Action/LookAround";
    private const string ExitDungeonActionPath = "SO/AI/Action/ExitDungeon";
    private const string HaulActionPath = "SO/AI/Action/Haul";
    private const string RescueActionPath = "SO/AI/Action/Rescue";
    private const string HuntActionPath = "SO/AI/Action/Hunt";
    private const string SubstanceUseActionPath = "SO/AI/Action/SubstanceUse";
    private const string DrinkActionPath = "SO/AI/Action/Drink";
    private const string PrimitiveFieldMealActionPath = "SO/AI/Action/PrimitiveFieldMeal";
    private const string PrimitiveFloorRestActionPath = "SO/AI/Action/PrimitiveFloorRest";
    private const string PrimitiveLatrineActionPath = "SO/AI/Action/PrimitiveLatrine";
    private const string PrimitiveBucketWashActionPath = "SO/AI/Action/PrimitiveBucketWash";

    public static AIAction[] ConfigureOwner(
        ICharacterAiActionAssetCatalog catalog,
        IGameClock clock)
    {
        List<AIAction> actions = new List<AIAction>();
        AddRequiredAction(actions, catalog, WorkActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIRescue>(actions, catalog, RescueActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIHaul>(actions, catalog, HaulActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIHunt>(actions, catalog, HuntActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AISubstanceUse>(actions, catalog, SubstanceUseActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIDrink>(actions, catalog, DrinkActionPath, CharacterAiBranch.Drink);
        AddRequiredAction(actions, catalog, EatActionPath, CharacterAiBranch.Eat);
        AddSpecialAction<AIPrimitiveFieldMeal>(actions, catalog, PrimitiveFieldMealActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, catalog, RestActionPath, CharacterAiBranch.Rest);
        AddSpecialAction<AIPrimitiveFloorRest>(actions, catalog, PrimitiveFloorRestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, catalog, ToiletActionPath, CharacterAiBranch.Toilet);
        AddSpecialAction<AIPrimitiveLatrine>(actions, catalog, PrimitiveLatrineActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, catalog, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddSpecialAction<AIPrimitiveBucketWash>(actions, catalog, PrimitiveBucketWashActionPath, CharacterAiBranch.Hygiene);
        AddRequiredAction(actions, catalog, RecreationActionPath, CharacterAiBranch.LeisureVisit);
        AddRequiredAction(actions, catalog, WaitActionPath, CharacterAiBranch.Wait);
        return BindClocks(actions, clock);
    }

    public static AIAction[] ConfigureOwner(
        AIAction[] configured,
        ICharacterAiActionAssetCatalog catalog,
        IGameClock clock)
    {
        AIAction[] retained = (configured ?? Array.Empty<AIAction>())
            .Where(action => action?.actionset != null
                && action.actionset.Branch != CharacterAiBranch.ExitDungeon)
            .GroupBy(action => action.actionset)
            .Select(group => group.First()).ToArray();
        List<AIAction> actions = ConfigureOwner(catalog, clock)
            .Select(required => retained.FirstOrDefault(action =>
                ReferenceEquals(action.actionset, required.actionset)) ?? required).ToList();
        actions.AddRange(retained.Where(action => !actions.Any(existing =>
            ReferenceEquals(existing.actionset, action.actionset))));
        return BindClocks(actions, clock);
    }

    public static AIAction[] ConfigureStaff(
        AIAction[] configured,
        ICharacterAiActionAssetCatalog catalog,
        IGameClock clock)
    {
        List<AIAction> actions = configured != null
            ? configured
                .Where(action => action?.actionset != null
                    && action.actionset.Branch != CharacterAiBranch.ExitDungeon)
                .ToList()
            : new List<AIAction>();
        AddRequiredAction(actions, catalog, WorkActionPath, CharacterAiBranch.Work);
        AddRequiredAction(actions, catalog, WaitActionPath, CharacterAiBranch.Wait);
        AddRequiredAction(actions, catalog, EatActionPath, CharacterAiBranch.Eat);
        AddSpecialAction<AIPrimitiveFieldMeal>(actions, catalog, PrimitiveFieldMealActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, catalog, RestActionPath, CharacterAiBranch.Rest);
        AddSpecialAction<AIPrimitiveFloorRest>(actions, catalog, PrimitiveFloorRestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, catalog, ToiletActionPath, CharacterAiBranch.Toilet);
        AddSpecialAction<AIPrimitiveLatrine>(actions, catalog, PrimitiveLatrineActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, catalog, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddSpecialAction<AIPrimitiveBucketWash>(actions, catalog, PrimitiveBucketWashActionPath, CharacterAiBranch.Hygiene);
        AddRequiredAction(actions, catalog, RecreationActionPath, CharacterAiBranch.LeisureVisit);
        AddSpecialAction<AIRescue>(actions, catalog, RescueActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIHaul>(actions, catalog, HaulActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIHunt>(actions, catalog, HuntActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AISubstanceUse>(actions, catalog, SubstanceUseActionPath, CharacterAiBranch.Work);
        AddSpecialAction<AIDrink>(actions, catalog, DrinkActionPath, CharacterAiBranch.Drink);
        // Off-duty staff can visit Training/Research/Mana under the existing
        // visitor policy. Fresh staff must not depend on a customer catalog.
        AddRequiredAction(actions, catalog, ShoppingActionPath, CharacterAiBranch.Shopping);
        return BindClocks(actions, clock);
    }

    public static AIAction[] NormalizeConfigured(
        AIAction[] configured,
        string ownerName,
        IGameClock clock)
    {
        if (configured == null)
        {
            Debug.LogError($"{ownerName}: AI actions are not configured.");
            return Array.Empty<AIAction>();
        }

        AIAction[] normalized = configured
            .Where(action => action?.actionset != null)
            .ToArray();
        if (normalized.Length != configured.Length)
        {
            Debug.LogWarning($"{ownerName}: Removed null AI action entries. Configure the missing actions on the prefab or asset.");
        }

        if (normalized.Length == 0)
        {
            Debug.LogError($"{ownerName}: AI actions are empty after validation.");
        }

        return BindClocks(normalized, clock);
    }

    public static AIAction[] EnsureVisitorActions(
        AIAction[] configured,
        CharacterActor actor,
        ICharacterAiActionAssetCatalog catalog,
        IGameClock clock)
    {
        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return configured ?? Array.Empty<AIAction>();
        }

        List<AIAction> actions = configured != null
            ? configured.ToList()
            : new List<AIAction>();
        AddRequiredAction(actions, catalog, EatActionPath, CharacterAiBranch.Eat);
        AddSpecialAction<AIPrimitiveFieldMeal>(actions, catalog, PrimitiveFieldMealActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, catalog, RestActionPath, CharacterAiBranch.Rest);
        AddSpecialAction<AIPrimitiveFloorRest>(actions, catalog, PrimitiveFloorRestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, catalog, ToiletActionPath, CharacterAiBranch.Toilet);
        AddSpecialAction<AIPrimitiveLatrine>(actions, catalog, PrimitiveLatrineActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, catalog, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddSpecialAction<AIPrimitiveBucketWash>(actions, catalog, PrimitiveBucketWashActionPath, CharacterAiBranch.Hygiene);
        AddRequiredAction(actions, catalog, RecreationActionPath, CharacterAiBranch.LeisureVisit);
        AddSpecialAction<AIDrink>(actions, catalog, DrinkActionPath, CharacterAiBranch.Drink);
        AddRequiredAction(actions, catalog, ShoppingActionPath, CharacterAiBranch.Shopping);
        AddRequiredAction(actions, catalog, LookAroundActionPath, CharacterAiBranch.LookAround);
        AddRequiredAction(actions, catalog, ExitDungeonActionPath, CharacterAiBranch.ExitDungeon);
        return BindClocks(actions, clock);
    }

    private static void AddRequiredAction(
        List<AIAction> actions,
        ICharacterAiActionAssetCatalog catalog,
        string resourcePath,
        CharacterAiBranch branch)
    {
        if (actions.Any(action => action?.actionset != null && action.actionset.Branch == branch))
        {
            return;
        }

        actions.Add(new AIAction
        {
            actionset = RequireCatalog(catalog).GetRequiredAction(resourcePath, branch)
        });
    }

    private static void AddSpecialAction<T>(
        List<AIAction> actions,
        ICharacterAiActionAssetCatalog catalog,
        string resourcePath,
        CharacterAiBranch branch)
        where T : AIActionSet
    {
        if (actions.Any(action => action?.actionset is T))
        {
            return;
        }

        AIActionSet actionSet = RequireCatalog(catalog)
            .GetRequiredAction(resourcePath, branch);
        T required = actionSet as T
            ?? throw new InvalidOperationException(
                $"Required AI action '{resourcePath}' has type {actionSet.GetType().Name}; expected {typeof(T).Name}.");
        actions.Add(new AIAction { actionset = required });
    }

    private static AIAction[] BindClocks(
        IEnumerable<AIAction> source,
        IGameClock clock)
    {
        List<AIAction> ordered = source?.ToList() ?? new List<AIAction>();
        MoveFacilityBeforePrimitive<AIPrimitiveFieldMeal>(ordered, CharacterAiBranch.Eat);
        MoveFacilityBeforePrimitive<AIPrimitiveFloorRest>(ordered, CharacterAiBranch.Rest);
        MoveFacilityBeforePrimitive<AIPrimitiveLatrine>(ordered, CharacterAiBranch.Toilet);
        MoveFacilityBeforePrimitive<AIPrimitiveBucketWash>(ordered, CharacterAiBranch.Hygiene);
        AIAction[] actions = ordered.ToArray();
        if (clock == null)
        {
            return actions;
        }

        foreach (AIAction action in actions)
        {
            action?.BindClock(clock);
        }

        return actions;
    }

    private static void MoveFacilityBeforePrimitive<T>(
        List<AIAction> actions,
        CharacterAiBranch branch)
        where T : AIPrimitiveSurvivalAction
    {
        while (true)
        {
            int primitiveIndex = actions.FindIndex(action =>
                action?.actionset is T);
            int facilityIndex = primitiveIndex < 0
                ? -1
                : actions.FindIndex(
                    primitiveIndex + 1,
                    action => action?.actionset != null
                        && action.actionset.Branch == branch
                        && action.actionset is not AIPrimitiveSurvivalAction);
            if (facilityIndex < 0)
            {
                return;
            }

            AIAction facility = actions[facilityIndex];
            actions.RemoveAt(facilityIndex);
            actions.Insert(primitiveIndex, facility);
        }
    }

    private static ICharacterAiActionAssetCatalog RequireCatalog(
        ICharacterAiActionAssetCatalog catalog)
    {
        return catalog
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires {nameof(ICharacterAiActionAssetCatalog)} injection.");
    }
}
