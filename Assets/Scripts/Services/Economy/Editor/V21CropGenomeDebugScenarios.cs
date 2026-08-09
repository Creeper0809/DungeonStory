#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class V21CropGenomeDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Economy/Verify V21 Crop Genome Loci")]
    public static void VerifyFromMenu()
    {
        RunAll();
        Debug.Log("V21_CROP_GENOME_LOCI=PASS");
    }

    public static bool RunAll()
    {
        CultivarGenomeSaveData resistant = Genome("genome:test:resistant", 2);
        CultivarGenomeSaveData vulnerable = Genome("genome:test:vulnerable", -2);
        CropEcologyAggregateState state = new();
        state.RegisterBaseGenome(resistant);
        state.RegisterBaseGenome(vulnerable);
        state.Sow("plot:resistant", CropFamilyGroup.Grain, Seed(resistant));
        state.Sow("plot:vulnerable", CropFamilyGroup.Grain, Seed(vulnerable));

        CropGenomePhenotype strong = state.GetPhenotype("plot:resistant");
        CropGenomePhenotype weak = state.GetPhenotype("plot:vulnerable");
        Require(strong.ColdToleranceDegrees == 5f && weak.ColdToleranceDegrees == -5f,
            "Cold-tolerance locus did not change the lower temperature allowance.");
        Require(strong.HeatToleranceDegrees == 5f && weak.HeatToleranceDegrees == -5f,
            "Heat-tolerance locus did not change the upper temperature allowance.");
        Require(strong.GrowthMultiplier > 1f && weak.GrowthMultiplier < 1f,
            "Growth-speed locus did not change authoritative growth speed.");
        Require(strong.YieldMultiplier > 1f && weak.YieldMultiplier < 1f,
            "Yield locus did not change authoritative harvest yield.");
        Require(strong.DiseaseRiskMultiplier < 1f && weak.DiseaseRiskMultiplier > 1f,
            "Disease-resistance locus did not change authoritative disease risk.");
        Require(strong.SeedYieldBonus == 1 && weak.SeedYieldBonus == -1,
            "Seed-yield locus did not change returned physical seed count.");

        CropEcologyWorldSaveData diseaseFixture = state.Capture();
        foreach (CropEcologyPlotSaveData plot in diseaseFixture.plots)
            plot.diseasePressure = 100f;
        CropEcologyAggregateState diseaseState = CropEcologyAggregateState.Restore(diseaseFixture);
        diseaseState.AdvanceDay("plot:resistant", false, () => 0.18d);
        diseaseState.AdvanceDay("plot:vulnerable", false, () => 0.18d);
        IReadOnlyList<CropEcologyPlotSaveData> diseasePlots = diseaseState.Plots;
        Require(diseasePlots[0].disease == CropDiseaseKind.None,
            "Resistant cultivar ignored its reduced disease probability.");
        Require(diseasePlots[1].disease == CropDiseaseKind.GrainFiberRust,
            "Vulnerable cultivar ignored its increased disease probability.");

        CropHarvestEcologyResult strongHarvest = state.Harvest("plot:resistant", () => 0.5d);
        CropHarvestEcologyResult weakHarvest = state.Harvest("plot:vulnerable", () => 0.5d);
        Require(strongHarvest.YieldMultiplier > weakHarvest.YieldMultiplier,
            "Yield phenotype was not consumed by harvest calculation.");
        Require(strongHarvest.ReturnedSeedCount == 4 && weakHarvest.ReturnedSeedCount == 2,
            "Seed-yield phenotype was not consumed by physical seed return.");

        CropEcologyAggregateState restored = CropEcologyAggregateState.Restore(diseaseState.Capture());
        CropGenomePhenotype restoredStrong = restored.GetPhenotype("plot:resistant");
        Require(Mathf.Approximately(
                strong.DiseaseRiskMultiplier,
                restoredStrong.DiseaseRiskMultiplier),
            "Crop genome phenotype changed across save restoration.");
        return true;
    }

    private static CultivarGenomeSaveData Genome(string id, int allele) => new()
    {
        genomeId = id,
        cropId = "crop:test",
        generation = 0,
        loci = new List<DiploidLocusSaveData>
        {
            Locus(CropGenomeLocus.ColdTolerance, allele),
            Locus(CropGenomeLocus.HeatTolerance, allele),
            Locus(CropGenomeLocus.GrowthSpeed, allele),
            Locus(CropGenomeLocus.Yield, allele),
            Locus(CropGenomeLocus.DiseaseResistance, allele),
            Locus(CropGenomeLocus.SeedYield, allele)
        }
    };

    private static DiploidLocusSaveData Locus(CropGenomeLocus locus, int allele) => new()
    {
        locus = locus,
        alleleA = allele,
        alleleB = allele
    };

    private static SeedLotState Seed(CultivarGenomeSaveData genome) => new()
    {
        cropId = genome.cropId,
        cultivarGenomeId = genome.genomeId,
        generation = genome.generation,
        pathogenLoad = 0f
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
