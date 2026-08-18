#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class V27BalanceLiveRuntimeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-vertical-slice-playmode.txt";

    [MenuItem("DungeonStory/V27/Verify Live Vertical Slice Runtime")]
    public static void RunFromMenu()
    {
        string report;
        try
        {
            report = Run();
            Debug.Log(report);
        }
        catch (Exception exception)
        {
            report = "RESULT=FAIL; reason=" + exception.Message + "\n";
            Debug.LogError(report + exception);
        }
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
        {
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.Write(report);
            writer.Flush();
        });
        AssetDatabase.Refresh();
    }

    public static string Run()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("V27 live runtime verification requires PlayMode.");

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include)
            ?? throw new InvalidOperationException("DungeonRuntimeLifetimeScope is missing.");
        IBalanceWorkCalculator work = scope.Container.Resolve<IBalanceWorkCalculator>();
        IMaterialSalvageCalculator salvage =
            scope.Container.Resolve<IMaterialSalvageCalculator>();
        IMaterialEconomicProfileCatalog materials =
            scope.Container.Resolve<IMaterialEconomicProfileCatalog>();
        if (work is not V27BalanceWorkCalculator)
            throw new InvalidOperationException("Live IBalanceWorkCalculator is not V27.");

        BuildingSO d03 = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D03_조리손질대.asset")
            ?? throw new InvalidOperationException("D03 authority is missing.");
        ProductionRecipeSO sawmill = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes/recipe_sawmill_lumber.asset")
            ?? throw new InvalidOperationException("Sawmill recipe authority is missing.");

        V23BalanceWorkCalculator before = new V23BalanceWorkCalculator(materials);
        float beforeConstruction = before.CalculateConstruction(d03);
        float beforeRecipe = before.CalculateRecipe(sawmill);
        float construction = work.CalculateConstruction(d03);
        float recipe = work.CalculateRecipe(sawmill);
        MaterialSalvageResult dismantle = salvage.Calculate(
            DismantleTargetKind.GeneralFacility,
            construction,
            d03.GetConstructionMaterials(),
            100f);
        BuildingWorkAmountAbility authoredConstruction =
            d03.GetAbility<BuildingWorkAmountAbility>()
            ?? throw new InvalidOperationException(
                "D03 authored construction WU authority is missing.");
        RequireApproximately(
            construction,
            authoredConstruction.constructionWorkRequired,
            "D03 authored construction authority");
        if (construction < Mathf.Ceil(beforeConstruction * 1.5f)
            || construction > Mathf.Ceil(beforeConstruction * 2.25f))
        {
            throw new InvalidOperationException(
                "D03 construction escaped the approved 1.5-2.25 WU band: "
                + $"before={beforeConstruction}; after={construction}.");
        }
        RequireApproximately(recipe, sawmill.RequiredWork, "sawmill recipe");
        RequireApproximately(
            dismantle.RequiredWork,
            construction * 0.25f,
            "D03 dismantle");

        string recovered = string.Join(
            "|",
            dismantle.RecoveredMaterials
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .Select(value => value.ItemId + "=" + value.Amount));
        int inputQuantity = d03.GetConstructionMaterials().Sum(value => value.Amount);
        int recoveredQuantity = dismantle.RecoveredMaterials.Sum(value => value.Amount);
        if (recoveredQuantity <= 0 || recoveredQuantity >= inputQuantity)
        {
            throw new InvalidOperationException(
                "D03 recovery is not a strict physical loss: input="
                + inputQuantity + "; recovered=" + recoveredQuantity
                + "; detail=" + recovered);
        }

        return "RESULT=PASS; checks=4\n"
            + "PASS V27_LIVE_CONTAINER_WORK_AUTHORITY=V27BalanceWorkCalculator\n"
            + "PASS V27_LIVE_D03_CONSTRUCTION_WU=" + FloatToken(beforeConstruction)
            + "->" + FloatToken(construction) + "\n"
            + "PASS V27_LIVE_SAWMILL_RECIPE_WU_RECURRING=" + FloatToken(beforeRecipe)
            + "->" + FloatToken(recipe) + "\n"
            + "PASS V27_LIVE_D03_DISMANTLE_WU="
            + FloatToken(construction * 0.25f)
            + "->" + FloatToken(dismantle.RequiredWork)
            + "; recovered=" + recovered + "\n";
    }

    private static string FloatToken(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static void RequireApproximately(
        float actual,
        float expected,
        string label)
    {
        if (!Mathf.Approximately(actual, expected))
        {
            throw new InvalidOperationException(
                label + " mismatch; expected=" + expected + ", actual=" + actual + ".");
        }
    }
}
#endif
