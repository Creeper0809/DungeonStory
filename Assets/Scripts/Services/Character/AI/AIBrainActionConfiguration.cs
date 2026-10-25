using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal static class AIBrainActionConfiguration
{
    public static AIAction[] ConfigureOwner(
        ICharacterAiActionAssetCatalog catalog,
        IGameClock clock)
    {
        List<AIAction> actions = new List<AIAction>();
        AddRequiredAction<AIWork>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIRescue>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIHaul>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIHunt>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AISubstanceUse>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIDrink>(actions, catalog, CharacterAiBranch.Drink);
        AddRequiredAction<AIEat>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIPrimitiveFieldMeal>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIPrimitiveFloorRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIPrimitiveLatrine>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIPrimitiveBucketWash>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.LeisureVisit);
        AddRequiredAction<AIWait>(actions, catalog, CharacterAiBranch.Wait);
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
        AddRequiredAction<AIWork>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIWait>(actions, catalog, CharacterAiBranch.Wait);
        AddRequiredAction<AIEat>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIPrimitiveFieldMeal>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIPrimitiveFloorRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIPrimitiveLatrine>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIPrimitiveBucketWash>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.LeisureVisit);
        AddRequiredAction<AIRescue>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIHaul>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIHunt>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AISubstanceUse>(actions, catalog, CharacterAiBranch.Work);
        AddRequiredAction<AIDrink>(actions, catalog, CharacterAiBranch.Drink);
        // Off-duty staff can visit Training/Research/Mana under the existing
        // visitor policy. Fresh staff must not depend on a customer catalog.
        AddRequiredAction<AIShopping>(actions, catalog, CharacterAiBranch.Shopping);
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
        AddRequiredAction<AIEat>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIPrimitiveFieldMeal>(actions, catalog, CharacterAiBranch.Eat);
        AddRequiredAction<AIRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIPrimitiveFloorRest>(actions, catalog, CharacterAiBranch.Rest);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIPrimitiveLatrine>(actions, catalog, CharacterAiBranch.Toilet);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIPrimitiveBucketWash>(actions, catalog, CharacterAiBranch.Hygiene);
        AddRequiredAction<AIFacilityRoleAction>(actions, catalog, CharacterAiBranch.LeisureVisit);
        AddRequiredAction<AIDrink>(actions, catalog, CharacterAiBranch.Drink);
        AddRequiredAction<AIShopping>(actions, catalog, CharacterAiBranch.Shopping);
        AddRequiredAction<AILookAround>(actions, catalog, CharacterAiBranch.LookAround);
        AddRequiredAction<AIExitDungeon>(actions, catalog, CharacterAiBranch.ExitDungeon);
        return BindClocks(actions, clock);
    }

    private static void AddRequiredAction<T>(
        List<AIAction> actions,
        ICharacterAiActionAssetCatalog catalog,
        CharacterAiBranch branch)
        where T : AIActionSet
    {
        if (actions.Any(action => action?.actionset is T
            && action.actionset.Branch == branch))
        {
            return;
        }

        T required = RequireCatalog(catalog).GetRequiredAction<T>(branch);
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
