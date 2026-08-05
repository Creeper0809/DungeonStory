#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EvolutionCatalystProgressionAssetMigration
{
    private const string DefinitionsRoot =
        "Assets/Resources/SO/Items/Definitions";
    private const int FamilyCount = 7;
    private const int ExpectedDefinitionCount =
        (FamilyCount + 1) * EvolutionCatalystProgression.MaximumLevel;

    [MenuItem(
        "Tools/DungeonStory/Content/Migrations/"
        + "Migrate Evolution Catalyst Progression")]
    public static void Run()
    {
        int migrated = MigrateAndValidate();
        Debug.Log(
            $"[Evolution Catalyst Migration] PASS: {migrated} definitions migrated.");
    }

    public static int MigrateAndValidate()
    {
        GenericItemDefinitionSO[] definitions = AssetDatabase
            .FindAssets("t:GenericItemDefinitionSO", new[] { DefinitionsRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GenericItemDefinitionSO>)
            .Where(definition => definition != null)
            .Where(IsCatalystOrResidue)
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length != ExpectedDefinitionCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedDefinitionCount} catalyst definitions, "
                + $"but found {definitions.Length}.");
        }

        foreach (GenericItemDefinitionSO definition in definitions)
        {
            MigrateDefinition(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateDefinitions(definitions);
        return definitions.Length;
    }

    private static bool IsCatalystOrResidue(GenericItemDefinitionSO definition)
    {
        return EvolutionCatalystItemId.TryParseCatalyst(
                definition.ItemId,
                out _)
            || EvolutionCatalystItemId.TryParseResidue(
                definition.ItemId,
                out _);
    }

    private static void MigrateDefinition(GenericItemDefinitionSO definition)
    {
        if (EvolutionCatalystItemId.TryParseCatalyst(
                definition.ItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            Configure(
                definition,
                catalyst.progressionLevel,
                catalyst.potency,
                catalyst.family,
                false);
            return;
        }

        if (!EvolutionCatalystItemId.TryParseResidue(
                definition.ItemId,
                out int progressionLevel))
        {
            throw new InvalidOperationException(
                $"Unsupported catalyst definition '{definition.ItemId}'.");
        }

        Configure(
            definition,
            progressionLevel,
            EvolutionCatalystProgression.GetPotencyGrade(progressionLevel),
            "universal",
            true);
    }

    private static void Configure(
        GenericItemDefinitionSO definition,
        int progressionLevel,
        int potency,
        string family,
        bool residue)
    {
        string displayName = residue
            ? $"범용 촉매 잔재 진행 {progressionLevel} · {potency}등급"
            : $"{EvolutionCatalystItemDefinitions.GetFamilyDisplayName(family)} "
                + $"촉매 진행 {progressionLevel} · {potency}등급";
        string description = residue
            ? "촉매를 분해해 얻은 잔재. 정제하거나 다음 진행 단계로 합칠 수 있다."
            : "시설 개조와 장비 조율에 사용하는 진화 촉매.";
        int price = residue
            ? Mathf.Max(
                1,
                EvolutionCatalystItemDefinitions.GetCatalystValue(
                    progressionLevel) / 3)
            : EvolutionCatalystItemDefinitions.GetCatalystValue(
                progressionLevel);

        definition.ConfigureCore(
            definition.ItemId,
            displayName,
            description,
            definition.StockCategory,
            price,
            definition.UnitWeight,
            definition.MaxStack,
            definition.Sprite);
        definition.SetFeature(new EvolutionCatalystItemFeature
        {
            family = family,
            potency = potency,
            residue = residue
        });
        EditorUtility.SetDirty(definition);
    }

    private static void ValidateDefinitions(
        GenericItemDefinitionSO[] definitions)
    {
        foreach (GenericItemDefinitionSO definition in definitions)
        {
            int progressionLevel;
            string family;
            bool residue;
            if (EvolutionCatalystItemId.TryParseCatalyst(
                    definition.ItemId,
                    out EquipmentCatalystDefinition catalyst))
            {
                progressionLevel = catalyst.progressionLevel;
                family = catalyst.family;
                residue = false;
            }
            else if (EvolutionCatalystItemId.TryParseResidue(
                         definition.ItemId,
                         out progressionLevel))
            {
                family = "universal";
                residue = true;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid catalyst definition ID '{definition.ItemId}'.");
            }

            int expectedPotency =
                EvolutionCatalystProgression.GetPotencyGrade(
                    progressionLevel);
            if (!definition.TryGetFeature(
                    out EvolutionCatalystItemFeature feature)
                || feature.potency != expectedPotency
                || feature.residue != residue
                || !string.Equals(
                    feature.family,
                    family,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Catalyst projection mismatch for '{definition.ItemId}'.");
            }

            string[] errors = definition.ValidateDefinition().ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Catalyst definition '{definition.ItemId}' is invalid: "
                    + string.Join(" | ", errors));
            }
        }
    }
}
#endif
