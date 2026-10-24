using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R19BattleCounterDebugScenarios
{
    private const string SporeBloomPath =
        "Assets/Resources/SO/V20/Combat/Modifiers/battlefield_spore-bloom.asset";
    private const string UnknownCounter = "counter:r19-unknown";

    [MenuItem("DungeonStory/V27/Content/Validate R19 Battle Counters")]
    public static void RunAll()
    {
        VerifyDeclaredCounterContract();
        VerifyConditionalLegacyAliases();
        VerifySporeBloomCounterApplication();
        VerifyDefinitionGuards();
        Debug.Log(
            "R19_BATTLE_COUNTERS=PASS; declared=41; canonical=32; aliases=9; "
            + "conditionalProjection=true; sporeBloomCountered=true; "
            + "definitionGuards=true");
    }

    private static void VerifyDeclaredCounterContract()
    {
        Require(OffenseBattleCounterRules.DeclaredTags.Count == 41,
            "Battle counter authority does not expose 41 declared tags.");
        Require(OffenseBattleCounterRules.DeclaredTags
                .Distinct(StringComparer.Ordinal).Count() == 41,
            "Battle counter authority contains duplicate tags.");
        Require(OffenseBattleCounterRules.LegacyAliasBindings.Count == 9,
            "Battle counter authority does not expose nine legacy aliases.");
        Require(OffenseBattleCounterRules.DeclaredTags.Count(tag =>
                    !OffenseBattleCounterRules.LegacyAliasBindings.ContainsKey(tag))
                == 32,
            "Battle counter authority changed the 32 canonical tags.");

        foreach (KeyValuePair<string, string> binding in
                 OffenseBattleCounterRules.LegacyAliasBindings)
        {
            Require(OffenseBattleCounterRules.IsDeclared(binding.Key),
                $"Legacy counter alias '{binding.Key}' is not declared.");
            Require(OffenseBattleCounterRules.IsDeclared(binding.Value),
                $"Canonical counter '{binding.Value}' is not declared.");
        }

        RequireReferenceKind(
            typeof(BattlefieldModifierDefinitionSO),
            "requiredCounterTag");
        RequireReferenceKind(typeof(EnemyArchetypeDefinitionSO), "counterTags");
        RequireReferenceKind(typeof(OffenseEncounterSO), "counterTags");
    }

    private static void VerifyConditionalLegacyAliases()
    {
        IReadOnlyCollection<string> empty = OffenseBattleCounterRules.Project(
            Array.Empty<OffenseBattleCombatant>());
        foreach (string alias in
                 OffenseBattleCounterRules.LegacyAliasBindings.Keys)
        {
            Require(!empty.Contains(alias, StringComparer.Ordinal),
                $"Empty party produced legacy counter alias '{alias}'.");
        }

        Dictionary<string, OffenseBattleCombatant> producers = new(
            StringComparer.Ordinal)
        {
            ["counter:mana-disrupt"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W07_Crossbow.asset",
                    ammunitionId: "ammo:mana-disruptor-bolt")),
            ["counter:split-pressure"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W06_Longbow.asset")),
            ["counter:smoke"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W18_Arquebus.asset",
                    ammunitionId: "ammo:smoke-cartridge")),
            ["counter:pin-leader"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W06_Longbow.asset")),
            ["counter:precision"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W24_SniperArquebus.asset")),
            ["counter:air-filter"] = Equipped(
                CombatWeaponSnapshot.CreateUnarmed(),
                new[]
                {
                    AuthoredArmor(
                        "Assets/Resources/SO/Combat/Equipment/A19_PowderCuirass.asset")
                }),
            ["counter:focus-support"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W07_Crossbow.asset")),
            ["counter:mobile"] = Equipped(
                AuthoredWeapon(
                    "Assets/Resources/SO/Combat/Equipment/W27_PoweredStrikingGauntlet.asset"))
        };

        foreach (KeyValuePair<string, string> expected in
                 OffenseBattleCounterRules.LegacyAliasBindings)
        {
            Require(producers.TryGetValue(expected.Value, out
                    OffenseBattleCombatant producer),
                $"No actual equipment projection fixture exists for '{expected.Value}'.");
            IReadOnlyCollection<string> projected =
                OffenseBattleCounterRules.Project(new[] { producer });
            Require(projected.Contains(expected.Value, StringComparer.Ordinal),
                $"Actual equipment did not produce '{expected.Value}'.");
            Require(projected.Contains(expected.Key, StringComparer.Ordinal),
                $"Canonical counter '{expected.Value}' did not produce alias '{expected.Key}'.");

            foreach (KeyValuePair<string, string> binding in
                     OffenseBattleCounterRules.LegacyAliasBindings)
            {
                Require(
                    projected.Contains(binding.Key, StringComparer.Ordinal)
                    == projected.Contains(binding.Value, StringComparer.Ordinal),
                    $"Alias '{binding.Key}' was projected without exactly following "
                    + $"canonical counter '{binding.Value}'.");
            }
        }
    }

    private static void VerifySporeBloomCounterApplication()
    {
        BattlefieldModifierDefinitionSO source =
            AssetDatabase.LoadAssetAtPath<BattlefieldModifierDefinitionSO>(
                SporeBloomPath);
        Require(source != null, "Spore bloom battlefield modifier was not found.");
        Require(source.requiredCounterTag == "counter:fungicide",
            "Spore bloom no longer requires the fungicide compatibility counter.");
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        BattlefieldModifierDefinitionSO clone = Object.Instantiate(source);
        try
        {
            OffenseBattleEncounterRules unprepared = Rules(clone);
            unprepared.EvaluatePartyCounters(Array.Empty<OffenseBattleCombatant>());
            float unpreparedAccuracy =
                unprepared.GetAccuracyMultiplier(OffenseBattleTeam.Allies);
            float unpreparedDamage =
                unprepared.GetDamageMultiplier(OffenseBattleTeam.Allies);
            Require(Mathf.Approximately(
                    unpreparedAccuracy,
                    clone.accuracyMultiplier),
                "Unprepared party did not retain the spore bloom accuracy penalty.");
            Require(Mathf.Approximately(
                    unpreparedDamage,
                    clone.damageMultiplier),
                "Unprepared party changed the spore bloom damage multiplier.");

            OffenseBattleCombatant protectedMember = Equipped(
                CombatWeaponSnapshot.CreateUnarmed(),
                new[]
                {
                    AuthoredArmor(
                        "Assets/Resources/SO/Combat/Equipment/A19_PowderCuirass.asset")
                });
            OffenseBattleEncounterRules prepared = Rules(clone);
            prepared.EvaluatePartyCounters(new[] { protectedMember });
            Require(prepared.AvailableCounterTags.Contains(
                    "counter:air-filter"),
                "Protective equipment did not produce its canonical air-filter counter.");
            Require(prepared.MatchedCounterTags.Contains("counter:fungicide"),
                "Spore bloom did not match the air-filter compatibility alias.");
            Require(Mathf.Approximately(
                    prepared.GetAccuracyMultiplier(OffenseBattleTeam.Allies),
                    1.03f),
                "Fungicide compatibility did not counter the spore accuracy penalty.");
            Require(Mathf.Approximately(
                    prepared.GetDamageMultiplier(OffenseBattleTeam.Allies),
                    1.03f),
                "Fungicide compatibility did not apply the matched counter damage bonus.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            Require(EditorJsonUtility.ToJson(source) == sourceJson,
                "Spore bloom counter fixture changed the source asset.");
            Require(EditorUtility.IsDirty(source) == sourceDirty,
                "Spore bloom counter fixture changed source dirty state.");
        }
    }

    private static void VerifyDefinitionGuards()
    {
        BattlefieldModifierDefinitionSO[] modifiers =
            LoadAll<BattlefieldModifierDefinitionSO>();
        EnemyArchetypeDefinitionSO[] enemies =
            LoadAll<EnemyArchetypeDefinitionSO>();
        OffenseEncounterSO[] encounters = LoadAll<OffenseEncounterSO>();
        Require(modifiers.Length > 0 && enemies.Length > 0 && encounters.Length > 0,
            "Battle counter definition assets were not found.");

        foreach (BattlefieldModifierDefinitionSO modifier in modifiers)
        {
            RequireClean(modifier.ValidateDefinition(),
                $"Battlefield modifier '{modifier.name}'");
        }
        foreach (EnemyArchetypeDefinitionSO enemy in enemies)
        {
            RequireClean(enemy.ValidateDefinition(),
                $"Enemy archetype '{enemy.name}'");
        }
        foreach (OffenseEncounterSO encounter in encounters)
        {
            RequireClean(encounter.ValidateDefinition(),
                $"Offense encounter '{encounter.name}'");
        }

        VerifyModifierRejectsUnknown(modifiers.First(value =>
            !string.IsNullOrWhiteSpace(value.requiredCounterTag)));
        VerifyEnemyRejectsUnknown(enemies[0]);
        VerifyEncounterRejectsUnknown(encounters[0]);
    }

    private static void VerifyModifierRejectsUnknown(
        BattlefieldModifierDefinitionSO source)
    {
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        BattlefieldModifierDefinitionSO clone = Object.Instantiate(source);
        try
        {
            string valid = clone.requiredCounterTag;
            clone.requiredCounterTag = UnknownCounter;
            RequireRejected(clone.ValidateDefinition(), UnknownCounter,
                "Battlefield modifier accepted an unknown battle counter.");
            clone.requiredCounterTag = valid;
            RequireClean(clone.ValidateDefinition(),
                "Repaired battlefield modifier");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            RequireSourceUnchanged(source, sourceJson, sourceDirty);
        }
    }

    private static void VerifyEnemyRejectsUnknown(
        EnemyArchetypeDefinitionSO source)
    {
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        EnemyArchetypeDefinitionSO clone = Object.Instantiate(source);
        try
        {
            List<string> valid = clone.counterTags.ToList();
            clone.counterTags = new List<string> { UnknownCounter };
            RequireRejected(clone.ValidateDefinition(), UnknownCounter,
                "Enemy archetype accepted an unknown battle counter.");
            clone.counterTags = valid;
            RequireClean(clone.ValidateDefinition(), "Repaired enemy archetype");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            RequireSourceUnchanged(source, sourceJson, sourceDirty);
        }
    }

    private static void VerifyEncounterRejectsUnknown(OffenseEncounterSO source)
    {
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        OffenseEncounterSO clone = Object.Instantiate(source);
        try
        {
            List<string> valid = clone.counterTags.ToList();
            clone.counterTags = new List<string> { UnknownCounter };
            RequireRejected(clone.ValidateDefinition(), UnknownCounter,
                "Offense encounter accepted an unknown battle counter.");
            clone.counterTags = valid;
            RequireClean(clone.ValidateDefinition(), "Repaired offense encounter");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            RequireSourceUnchanged(source, sourceJson, sourceDirty);
        }
    }

    private static OffenseBattleEncounterRules Rules(
        BattlefieldModifierDefinitionSO modifier) => new(
            OffenseEncounterObjective.DefeatAll,
            0,
            string.Empty,
            string.Empty,
            new[] { modifier },
            new[] { modifier.requiredCounterTag });

    private static OffenseBattleCombatant Equipped(
        CombatWeaponSnapshot weapon,
        IReadOnlyList<CombatArmorSnapshot> armor = null)
    {
        OffenseBattleCombatant combatant = new(
            "character:r19-counter",
            "R19 Counter Probe",
            "species:test",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 8f, 8f, 8f, 8f, 5f),
            100f,
            formation: OffenseFormationSlot.Front);
        combatant.SetCombatEquipment(
            weapon,
            armor ?? Array.Empty<CombatArmorSnapshot>());
        return combatant;
    }

    private static CombatWeaponSnapshot AuthoredWeapon(
        string path,
        string ammunitionId = "")
    {
        CombatWeaponSO definition =
            AssetDatabase.LoadAssetAtPath<CombatWeaponSO>(path);
        Require(definition != null,
            $"Authored counter weapon was not found at '{path}'.");
        CombatEquipmentInstance instance = new()
        {
            instanceId = "equipment:r19-counter",
            definitionId = definition.EquipmentId,
            quality = CombatEquipmentQuality.Normal,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch
            {
                ammunitionItemId = ammunitionId,
                remaining = string.IsNullOrWhiteSpace(ammunitionId) ? 0 : 1
            }
        };
        return definition.CreateSnapshot(instance);
    }

    private static CombatArmorSnapshot AuthoredArmor(string path)
    {
        CombatArmorSO definition =
            AssetDatabase.LoadAssetAtPath<CombatArmorSO>(path);
        Require(definition != null,
            $"Authored counter armor was not found at '{path}'.");
        CombatArmorPartValue part = definition.BodyPartDefense.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Authored counter armor '{definition.EquipmentId}' has no defense row.");
        return new CombatArmorSnapshot(
            "equipment:r19-protection",
            part.bodyPart,
            definition.Layer,
            CombatEquipmentQuality.Normal,
            1f,
            part.slashDefense,
            part.pierceDefense,
            part.bluntDefense,
            definitionId: definition.EquipmentId,
            roleFlags: definition.RoleFlags);
    }

    private static T[] LoadAll<T>() where T : ScriptableObject =>
        AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
            .Where(value => value != null)
            .Distinct()
            .ToArray();

    private static void RequireReferenceKind(Type ownerType, string fieldName)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing field '{ownerType.Name}.{fieldName}'.");
        ContentReferenceAttribute reference =
            field.GetCustomAttribute<ContentReferenceAttribute>();
        Require(reference?.Kind == ContentReferenceKind.BattleCounter,
            $"'{ownerType.Name}.{fieldName}' is not a battle counter reference.");
    }

    private static void RequireSourceUnchanged(
        Object source,
        string sourceJson,
        bool sourceDirty)
    {
        Require(EditorJsonUtility.ToJson(source) == sourceJson,
            $"Battle counter fixture changed source asset '{source.name}'.");
        Require(EditorUtility.IsDirty(source) == sourceDirty,
            $"Battle counter fixture changed dirty state for '{source.name}'.");
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string context)
    {
        Require(errors != null && errors.Count == 0,
            $"{context} failed validation: {string.Join(" | ", errors ?? Array.Empty<string>())}");
    }

    private static void RequireRejected(
        IReadOnlyList<string> errors,
        string expectedText,
        string message)
    {
        Require(errors != null && errors.Any(error =>
                error.Contains(expectedText, StringComparison.Ordinal)),
            message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
