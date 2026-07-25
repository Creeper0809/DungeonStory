using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CharacterStatDebugScenarios
{
    private const string CustomStatId = "stat:alchemy";

    [MenuItem("DungeonStory/Debug/Character/Run Keyed Stat Scenarios")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        try
        {
            VerifyBuiltInCatalog();
            VerifyCustomStatComposition();
            VerifyMigratedAssets();
            VerifyGenericPurchaseNeedEffect();
            VerifyRuntimeStatAuthorityIsolation();
            Debug.Log("CharacterStatDebugScenarios passed: keyed storage, custom composition, asset migration, generic purchase effects, and immutable runtime snapshots.");
        }
        finally
        {
            CharacterStatCatalog.ResetToBuiltIns();
            CharacterNeedCatalog.ResetToBuiltIns();
        }
    }

    private static void VerifyBuiltInCatalog()
    {
        CharacterStatCatalog.ResetToBuiltIns();
        Require(CharacterStatCatalog.All.Count == 11, "Expected eleven built-in character stat definitions.");
        foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))
        {
            CharacterStatDefinition definition = CharacterStatCatalog.GetRequired(type);
            Require(!string.IsNullOrWhiteSpace(definition.Id), $"Missing stable id for {type}.");
        }

        FieldInfo[] fixedIntegerFields = typeof(CharacterStatBlock)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(int))
            .ToArray();
        Require(fixedIntegerFields.Length == 0,
            "CharacterStatBlock still contains fixed integer stat fields.");
    }

    private static void VerifyCustomStatComposition()
    {
        CharacterStatCatalog.Register(new CharacterStatDefinition(
            CustomStatId,
            "연금",
            95));

        CharacterStatBlock defaultBlock = CharacterStatBlock.CreateDefault(5);
        Require(defaultBlock.Get(CustomStatId) == 5,
            "CreateDefault did not enumerate a custom stat definition.");
        defaultBlock.Add(CustomStatId, 2);
        Require(defaultBlock.Get(CustomStatId) == 7,
            "Keyed stat addition failed.");

        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        CharacterSpeciesSO species = ScriptableObject.CreateInstance<CharacterSpeciesSO>();
        CharacterTraitSO trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
        try
        {
            data.baseStats = CharacterStatBlock.CreateDefault(5);
            data.baseStats.Set(CustomStatId, 4);
            species.statBonus = new CharacterStatBlock();
            species.statBonus.Set(CustomStatId, 3);
            trait.statBonus = new CharacterStatBlock();
            trait.statBonus.Set(CustomStatId, 2);
            data.species = species;
            data.traits = new[] { trait };

            CharacterRuntimeProfile profile = data.CreateRuntimeProfile();
            Require(profile.GetStat(CustomStatId) == 9,
                "Runtime profile did not compose a custom base/species/trait stat.");
            Require(profile.GetStat(CharacterStatType.Attack) == 5,
                "Legacy typed stat lookup changed during keyed composition.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
            UnityEngine.Object.DestroyImmediate(species);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void VerifyMigratedAssets()
    {
        CharacterStatCatalog.ResetToBuiltIns();
        int validated = 0;
        validated += ValidateAssets<CharacterSO>(asset => asset.baseStats);
        validated += ValidateAssets<CharacterSpeciesSO>(asset => asset.statBonus);
        validated += ValidateAssets<CharacterTraitSO>(asset => asset.statBonus);
        Require(validated == 17, $"Expected 17 migrated stat assets, found {validated}.");

        CharacterSO ownerOrc = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Orc.asset");
        Require(ownerOrc != null
                && ownerOrc.baseStats.Get(CharacterStatType.Attack) == 8
                && ownerOrc.baseStats.Get(CharacterStatType.Sales) == 4
                && ownerOrc.baseStats.Get(CharacterStatType.Cleaning) == 3,
            "Owner_Orc values changed during stat migration.");
    }

    private static int ValidateAssets<TAsset>(Func<TAsset, CharacterStatBlock> getBlock)
        where TAsset : ScriptableObject
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
            CharacterStatBlock block = asset != null ? getBlock(asset) : null;
            Require(block != null, $"{path} has no stat block.");
            Require(block.Entries.All(entry => entry != null && !string.IsNullOrWhiteSpace(entry.statId)),
                $"{path} contains an invalid stat entry.");
            Require(block.Entries.Select(entry => entry.statId).Distinct(StringComparer.Ordinal).Count()
                    == block.Entries.Count,
                $"{path} contains duplicate stat ids.");
            foreach (CharacterStatDefinition definition in CharacterStatCatalog.All)
            {
                Require(block.Entries.Any(entry => string.Equals(
                        entry.statId,
                        definition.Id,
                        StringComparison.Ordinal)),
                    $"{path} is missing built-in stat {definition.Id}.");
            }

            count++;
        }

        return count;
    }

    private static void VerifyGenericPurchaseNeedEffect()
    {
        CharacterNeedCatalog.ResetToBuiltIns();
        CharacterCondition customCondition = (CharacterCondition)950;
        CharacterNeedCatalog.Register(new CharacterNeedDefinition(
            "need:focus",
            customCondition,
            "집중",
            95,
            defaultValue: 30f,
            workerInitialValue: 30f,
            relatedFacilityRole: FacilityRole.Research,
            tags: CharacterNeedTag.None,
            survivalWeight: 0f,
            moodProfile: null));

        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Generic Purchase Need Scenario Actor");
        CharacterSO characterData = ScriptableObject.CreateInstance<CharacterSO>();
        StatChange effect = ScriptableObject.CreateInstance<StatChange>();
        try
        {
            characterData.characterType = CharacterType.Customer;
            characterData.characterName = "Generic Purchase Need Scenario Actor";
            characterData.baseStats = CharacterStatBlock.CreateDefault();
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(characterData);

            effect.needId = "need:focus";
            effect.value = 17;
            float before = actor.Stats.Stats[customCondition];
            effect.Onbuy(actor);
            float after = actor.Stats.Stats[customCondition];
            Require(Mathf.Approximately(before, 30f) && Mathf.Approximately(after, 47f),
                $"Generic purchase effect changed focus from {before} to {after}.");

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

    private static void VerifyRuntimeStatAuthorityIsolation()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Runtime Stat Authority Scenario Actor");
        CharacterSO characterData = ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            characterData.characterType = CharacterType.Customer;
            characterData.characterName = "Runtime Stat Authority Scenario Actor";
            characterData.baseStats = CharacterStatBlock.CreateDefault();
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

            Dictionary<CharacterCondition, float> assigned =
                new Dictionary<CharacterCondition, float>
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
                "Assigned stat dictionary still aliases CharacterStats authority.");

            actor.stats[CharacterCondition.HUNGER] = 65f;
            Require(notificationCount == 2,
                $"Controlled stat writes should publish once; notifications={notificationCount}.");
            Require(firstSnapshot != null
                    && Mathf.Approximately(firstSnapshot[CharacterCondition.HUNGER], 40f),
                "Earlier stat event snapshot changed after a later write.");
            Require(latestSnapshot != null
                    && Mathf.Approximately(latestSnapshot[CharacterCondition.HUNGER], 65f),
                "Latest stat event snapshot did not contain the controlled write.");

            bool mutationRejected = false;
            try
            {
                ((IDictionary<CharacterCondition, float>)firstSnapshot)[CharacterCondition.HUNGER] = 1f;
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }

            Require(mutationRejected, "Stat event snapshot allowed subscriber mutation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(characterData);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
