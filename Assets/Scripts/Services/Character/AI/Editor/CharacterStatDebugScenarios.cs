using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compatibility menu entry retained for existing QA automation. The legacy
/// twelve-stat schema no longer exists; this scenario now verifies the V27
/// functional-capacity/performance authority and the separate need-state API.
/// </summary>
public static class CharacterStatDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run Performance Authority Scenarios")]
    public static void RunFromMenu() => RunAll();

    public static void RunAll()
    {
        V27CharacterPerformanceDebugScenarios.RunStructuralAudit();
        VerifyGenericPurchaseNeedEffect();
        VerifyRuntimeNeedAuthorityIsolation();
        Debug.Log("CharacterStatDebugScenarios passed: V27 performance authority and immutable need snapshots.");
    }

    private static void VerifyGenericPurchaseNeedEffect()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Generic Purchase Need Scenario Actor");
        CharacterSO characterData = ScriptableObject.CreateInstance<CharacterSO>();
        StatChange effect = ScriptableObject.CreateInstance<StatChange>();
        try
        {
            characterData.characterType = CharacterType.Customer;
            characterData.characterName = "Generic Purchase Need Scenario Actor";
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(characterData);

            effect.needId = "need:hunger";
            effect.value = 17;
            float before = actor.Stats.Stats[CharacterCondition.HUNGER];
            effect.Onbuy(actor.BuildingVisitor);
            float after = actor.Stats.Stats[CharacterCondition.HUNGER];
            Require(Mathf.Approximately(after, Mathf.Clamp(before + 17f, 0f, 100f)),
                $"Authored purchase effect changed hunger from {before} to {after}.");

            StatChange hamburger = AssetDatabase.LoadAssetAtPath<StatChange>(
                "Assets/Resources/SO/Stock/Item/Onbuy/Hamburger.asset");
            Require(hamburger != null && hamburger.needId == "need:hunger" && hamburger.value == 50,
                "Hamburger purchase effect was not migrated to need:hunger.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(characterData);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    public static void VerifyRuntimeNeedAuthorityIsolation()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Runtime Need Authority Scenario Actor");
        CharacterSO characterData = ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            characterData.characterType = CharacterType.Customer;
            characterData.characterName = "Runtime Need Authority Scenario Actor";
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(characterData);

            IReadOnlyDictionary<CharacterCondition, float> firstSnapshot = null;
            IReadOnlyDictionary<CharacterCondition, float> latestSnapshot = null;
            int notificationCount = 0;
            actor.Stats.OnStatChange += snapshot =>
            {
                notificationCount++;
                firstSnapshot ??= snapshot;
                latestSnapshot = snapshot;
            };

            Dictionary<CharacterCondition, float> assigned = new()
            {
                [CharacterCondition.HUNGER] = 40f,
                [CharacterCondition.SLEEP] = 80f,
                [CharacterCondition.FUN] = 70f,
                [CharacterCondition.MOOD] = 60f,
                [CharacterCondition.EXCRETION] = 90f,
                [CharacterCondition.HYGIENE] = 75f
            };
            actor.stats = assigned;
            assigned[CharacterCondition.HUNGER] = 0f;
            Require(Mathf.Approximately(actor.stats[CharacterCondition.HUNGER], 40f),
                "Assigned need dictionary still aliases CharacterStats authority.");

            actor.stats[CharacterCondition.HUNGER] = 65f;
            Require(notificationCount == 2,
                $"Controlled need writes should publish once; notifications={notificationCount}.");
            Require(firstSnapshot != null
                    && Mathf.Approximately(firstSnapshot[CharacterCondition.HUNGER], 40f),
                "Earlier need event snapshot changed after a later write.");
            Require(latestSnapshot != null
                    && Mathf.Approximately(latestSnapshot[CharacterCondition.HUNGER], 65f),
                "Latest need event snapshot did not contain the controlled write.");

            bool mutationRejected = false;
            try
            {
                ((IDictionary<CharacterCondition, float>)firstSnapshot)[CharacterCondition.HUNGER] = 1f;
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }

            Require(mutationRejected, "Need event snapshot allowed subscriber mutation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(characterData);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
