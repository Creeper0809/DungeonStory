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
        VerifyPreparedHarvestTransactions();
        return true;
    }

    private static void VerifyPreparedHarvestTransactions()
    {
        const string plotId = "plot:prepared-transaction";
        const string operationId = "crop-harvest:prepared-idempotence";
        CropEcologyAggregateState state = PreparedState(plotId);
        int randomDrawCount = 0;
        CropEcologyPreparedHarvestSnapshot prepared = state.PrepareHarvest(
            operationId,
            plotId,
            () =>
            {
                randomDrawCount++;
                return 0.5d;
            });
        Require(randomDrawCount > 0,
            "Prepared harvest did not consume its ecology random vector.");
        int frozenRandomDrawCount = randomDrawCount;

        CropEcologyPreparedHarvestSnapshot repeated = state.PrepareHarvest(
            operationId,
            plotId,
            () => throw new InvalidOperationException(
                "Idempotent prepare rerolled its frozen ecology outcome."));
        RequirePreparedHarvestEqual(prepared, repeated,
            "Repeated prepare did not return the frozen ecology outcome.");
        Require(randomDrawCount == frozenRandomDrawCount,
            "Repeated prepare consumed additional ecology random draws.");

        CropEcologyAggregateState restoredUncommitted =
            CropEcologyAggregateState.Restore(state.Capture());
        Require(restoredUncommitted.TryGetPreparedHarvest(
                operationId,
                out CropEcologyPreparedHarvestSnapshot uncommittedAfterRestore),
            "Uncommitted prepared harvest disappeared across capture and restore.");
        RequirePreparedHarvestEqual(prepared, uncommittedAfterRestore,
            "Uncommitted prepared harvest changed across capture and restore.");
        CropEcologyPreparedHarvestSnapshot repeatedAfterRestore =
            restoredUncommitted.PrepareHarvest(
                operationId,
                plotId,
                () => throw new InvalidOperationException(
                    "Restored prepared harvest rerolled its frozen ecology outcome."));
        RequirePreparedHarvestEqual(prepared, repeatedAfterRestore,
            "Restored prepare did not preserve ecology idempotence.");

        CropEcologyPreparedHarvestSnapshot committed =
            restoredUncommitted.CommitPreparedHarvest(operationId);
        Require(committed.Committed,
            "Prepared ecology harvest did not enter the committed phase.");
        CropEcologyAggregateState restoredCommitted =
            CropEcologyAggregateState.Restore(restoredUncommitted.Capture());
        Require(restoredCommitted.TryGetPreparedHarvest(
                operationId,
                out CropEcologyPreparedHarvestSnapshot committedAfterRestore),
            "Committed prepared harvest disappeared across capture and restore.");
        RequirePreparedHarvestEqual(committed, committedAfterRestore,
            "Committed prepared harvest changed across capture and restore.");

        ExpectInvalidOperation(
            () => restoredCommitted.AbandonPlot(plotId),
            "A plot with an unacknowledged committed harvest was abandoned.");
        Require(restoredCommitted.AcknowledgePreparedHarvest(operationId),
            "Committed prepared harvest could not be acknowledged.");
        Require(restoredCommitted.AbandonPlot(plotId),
            "Plot could not be abandoned after its prepared harvest was acknowledged.");

        VerifyDuplicatePreparedPlotRestoreIsRejected(plotId);
        VerifyPreparedHarvestSemanticTamperIsRejected(plotId);
    }

    private static void VerifyDuplicatePreparedPlotRestoreIsRejected(string plotId)
    {
        CropEcologyAggregateState first = PreparedState(plotId);
        first.PrepareHarvest("crop-harvest:duplicate-a", plotId, () => 0.5d);
        CropEcologyAggregateState second = PreparedState(plotId);
        second.PrepareHarvest("crop-harvest:duplicate-b", plotId, () => 0.5d);

        CropEcologyWorldSaveData duplicate = first.Capture();
        duplicate.preparedHarvests.Add(second.Capture().preparedHarvests[0].Clone());
        ExpectRestoreRejected(
            duplicate,
            "Restore accepted two individually valid prepared harvests for the same plot.");
    }

    private static void VerifyPreparedHarvestSemanticTamperIsRejected(string plotId)
    {
        CropEcologyAggregateState uncommitted = PreparedState(plotId);
        uncommitted.PrepareHarvest(
            "crop-harvest:semantic-uncommitted",
            plotId,
            () => 0.5d);

        CropEcologyWorldSaveData versionTamper = uncommitted.Capture();
        versionTamper.version = CropEcologyWorldSaveData.CurrentVersion - 1;
        ExpectRestoreRejected(versionTamper,
            "Restore accepted a pre-v3 prepared-harvest payload.");

        CropEcologyWorldSaveData livePlotTamper = uncommitted.Capture();
        livePlotTamper.plots[0].fertility -= 1f;
        ExpectRestoreRejected(livePlotTamper,
            "Restore accepted an uncommitted receipt whose live plot drifted from plotBefore.");

        CropEcologyWorldSaveData phaseTamper = uncommitted.Capture();
        phaseTamper.preparedHarvests[0].committed = true;
        ExpectRestoreRejected(phaseTamper,
            "Restore accepted a committed receipt without its generated genome publication.");

        CropEcologyWorldSaveData transitionTamper = uncommitted.Capture();
        transitionTamper.preparedHarvests[0].plotAfter.fertility += 1f;
        ExpectRestoreRejected(transitionTamper,
            "Restore accepted a tampered prepared-harvest plot transition.");

        CropEcologyWorldSaveData seedTamper = uncommitted.Capture();
        seedTamper.preparedHarvests[0].returnedSeedLot.cropId = "crop:tampered";
        ExpectRestoreRejected(seedTamper,
            "Restore accepted a returned seed lot that contradicted the frozen harvest.");

        CropEcologyAggregateState committed = PreparedState(plotId);
        const string committedOperation = "crop-harvest:semantic-committed";
        committed.PrepareHarvest(committedOperation, plotId, () => 0.5d);
        committed.CommitPreparedHarvest(committedOperation);
        CropEcologyWorldSaveData missingGenome = committed.Capture();
        string generatedGenomeId =
            missingGenome.preparedHarvests[0].generatedGenome.genomeId;
        Require(missingGenome.activeCultivars.RemoveAll(value =>
                string.Equals(value.genomeId, generatedGenomeId, StringComparison.Ordinal)) == 1,
            "Committed ecology fixture did not publish exactly one generated genome.");
        ExpectRestoreRejected(missingGenome,
            "Restore accepted a committed receipt without its published generated genome.");
    }

    private static CropEcologyAggregateState PreparedState(string plotId)
    {
        CultivarGenomeSaveData genome = Genome("genome:test:prepared-base", 2);
        CropEcologyAggregateState state = new();
        state.RegisterBaseGenome(genome);
        state.Sow(plotId, CropFamilyGroup.Grain, Seed(genome));
        return state;
    }

    private static void RequirePreparedHarvestEqual(
        CropEcologyPreparedHarvestSnapshot expected,
        CropEcologyPreparedHarvestSnapshot actual,
        string message)
    {
        Require(string.Equals(expected.OperationId, actual.OperationId, StringComparison.Ordinal)
            && string.Equals(expected.PlotId, actual.PlotId, StringComparison.Ordinal)
            && string.Equals(
                expected.OutcomeFingerprint,
                actual.OutcomeFingerprint,
                StringComparison.Ordinal)
            && expected.Committed == actual.Committed
            && Mathf.Approximately(
                expected.Result.YieldMultiplier,
                actual.Result.YieldMultiplier)
            && expected.Result.ReturnedSeedCount == actual.Result.ReturnedSeedCount
            && SeedLotEquals(
                expected.Result.ReturnedSeedLot,
                actual.Result.ReturnedSeedLot),
            message);
    }

    private static bool SeedLotEquals(SeedLotState expected, SeedLotState actual) =>
        expected != null
        && actual != null
        && string.Equals(expected.cropId, actual.cropId, StringComparison.Ordinal)
        && string.Equals(
            expected.cultivarGenomeId,
            actual.cultivarGenomeId,
            StringComparison.Ordinal)
        && expected.generation == actual.generation
        && Mathf.Approximately(expected.pathogenLoad, actual.pathogenLoad);

    private static void ExpectRestoreRejected(
        CropEcologyWorldSaveData data,
        string message) => ExpectInvalidOperation(
        () => CropEcologyAggregateState.Restore(data),
        message);

    private static void ExpectInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
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
