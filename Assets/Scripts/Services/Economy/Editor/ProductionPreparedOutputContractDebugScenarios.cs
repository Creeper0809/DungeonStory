#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputContractDebugScenarios
{
    private const string RecipeId = "recipe:test:prepared-output";
    private const string DestinationId =
        "production-output:building:prepared-output-fixture";
    private static readonly ProductionBillId BillId =
        (ProductionBillId)"production-bill:1";

    [MenuItem("DungeonStory/Debug/Run Prepared Production Output Contracts")]
    public static void RunAll()
    {
        VerifyRoundTripAndTransitions();
        VerifyDeclaredExternalInputContract();
        VerifyInvalidStatesFailLoud();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_CONTRACT=PASS");
    }

    private static void VerifyDeclaredExternalInputContract()
    {
        ProductionPreparedOutputBatchSaveData batch = CreateResolvedBatch();
        ProductionPreparedOutputLineSaveData externalInput = new()
        {
            outputLineId = "output:external-input",
            role = ProductionOutputRole.DeclaredExternalInput,
            itemId = string.Empty,
            quantity = 0,
            componentPayload = "process-addition-receipt",
            componentFingerprint = Digest('7'),
            qualityPermille = 1000,
            rollKind = "process-addition",
            rollValue = 0L,
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 200L
        };
        externalInput.lineCommitId =
            ProductionPreparedOutputIdentity.BuildLineCommitId(
                batch.batchCommitId,
                externalInput.outputLineId);
        batch.totalDeclaredExternalInputMassGrams = 200L;
        batch.lines.Add(externalInput);
        batch.lines = batch.lines
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToList();
        ProductionPreparedOutputContract.ValidateForBill(
            batch,
            BillId,
            RecipeId,
            1,
            DestinationId);
        Require(
            ProductionOutputRoleRules.IsNonPhysical(externalInput.role)
            && !ProductionOutputRoleRules.IsPhysical(externalInput.role)
            && batch.lines.Count(value =>
                ProductionOutputRoleRules.IsPhysical(value.role)) == 1,
            "declared external input was classified as a physical output");

        string canonical = JsonUtility.ToJson(batch);
        ProductionPreparedOutputBatchSaveData decoded =
            JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(
                canonical);
        ProductionPreparedOutputContract.ValidateForBill(
            decoded,
            BillId,
            RecipeId,
            1,
            DestinationId);
        Require(
            decoded.totalDeclaredExternalInputMassGrams == 200L
            && string.Equals(
                canonical,
                JsonUtility.ToJson(decoded),
                StringComparison.Ordinal),
            "declared external-input batch receipt did not round-trip exactly");

        ProductionPreparedOutputBatchSaveData totalDrift = decoded.Clone();
        totalDrift.totalDeclaredExternalInputMassGrams = 199L;
        RequireInvalid(totalDrift, "declared external-input total drift");

        ProductionPreparedOutputBatchSaveData lineDrift = decoded.Clone();
        lineDrift.lines.Single(value =>
                value.role == ProductionOutputRole.DeclaredExternalInput)
            .exactMassGrams = 199L;
        RequireInvalid(lineDrift, "declared external-input line drift");

        ProductionPreparedOutputBatchSaveData physicalForgery = decoded.Clone();
        ProductionPreparedOutputLineSaveData forged = physicalForgery.lines
            .Single(value =>
                value.role == ProductionOutputRole.DeclaredExternalInput);
        forged.itemId = "material:test-output";
        forged.quantity = 1;
        RequireInvalid(
            physicalForgery,
            "declared external-input physical publication forgery");
    }

    private static void VerifyRoundTripAndTransitions()
    {
        ProductionBillRecord record = CreateRecord();
        ProductionPreparedOutputBatchSaveData resolved = CreateResolvedBatch();
        record.ResolvePreparedOutput(resolved);

        resolved.lines[1].quantity = 99;
        Require(record.preparedOutput.lines[1].quantity == 2,
            "aggregate retained a caller-owned prepared-output line");

        record.MarkPreparedOutputPublicationPrepared(Digest('b'));
        Require(record.preparedOutput.phase ==
                ProductionPreparedOutputPhase.PublicationPrepared,
            "prepared output did not enter PublicationPrepared");
        record.ReturnPreparedOutputToWaitingForSpace();
        Require(record.preparedOutput.phase ==
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace
                && record.preparedOutput.admissionFingerprint.Length == 0,
            "publication rollback did not return to durable waiting state");
        record.MarkPreparedOutputPublicationPrepared(Digest('b'));
        record.MarkPreparedOutputPhysicalBatchCommitted(new[]
        {
            Candidate(record.preparedOutput, "stack:prepared-output:0001", 1, 500L),
            Candidate(record.preparedOutput, "stack:prepared-output:0002", 1, 500L)
        });
        record.MarkPreparedOutputCompleted();

        string canonical = JsonUtility.ToJson(record.preparedOutput);
        ProductionPreparedOutputBatchSaveData decoded =
            JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(canonical);
        ProductionPreparedOutputContract.ValidateForBill(
            decoded,
            BillId,
            RecipeId,
            1,
            DestinationId);
        Require(string.Equals(canonical, JsonUtility.ToJson(decoded),
                StringComparison.Ordinal),
            "prepared output JSON round-trip changed canonical state");

        DungeonProductionBillSaveData snapshot = new()
        {
            version = DungeonProductionBillSaveData.CurrentVersion,
            bills = new List<ProductionBillSaveData>
            {
                new()
                {
                    billId = BillId.Value,
                    recipeId = RecipeId,
                    buildingInstanceId = "building:prepared-output-fixture",
                    cycleSequence = 1,
                    outputDestinationId = DestinationId,
                    materialDestinationId = "production-input:" + BillId.Value,
                    preparedOutput = decoded.Clone()
                }
            }
        };
        ProductionBillRestoreCandidate restore = ProductionBillRestoreCandidate.Create(
            snapshot,
            billVersion: 1,
            stockSensorVersion: 1);
        ProductionPreparedOutputBatchSaveData restored =
            restore.Bills.Single().preparedOutput;
        Require(string.Equals(
                canonical,
                JsonUtility.ToJson(restored),
                StringComparison.Ordinal),
            "prepared output aggregate restore changed durable authority");

        ProductionPreparedOutputBatchSaveData ruined = CreateRuinedBatch(
            includeProof: true);
        ProductionPreparedOutputContract.ValidateForBill(
            ruined,
            BillId,
            RecipeId,
            1,
            DestinationId);
        ProductionPreparedOutputBatchSaveData ruinedRoundTrip =
            JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(
                JsonUtility.ToJson(ruined));
        Require(
            string.Equals(
                ruinedRoundTrip.maximumMassProofDigest,
                ruined.maximumMassProofDigest,
                StringComparison.Ordinal)
            && ruinedRoundTrip.maximumBatchMassGrams ==
                ruined.maximumBatchMassGrams
            && string.Equals(
                ruinedRoundTrip.capacityClaimDigest,
                ruined.capacityClaimDigest,
                StringComparison.Ordinal),
            "ruined output proof did not survive clone/JSON round-trip");

        record.ClearCompletedPreparedOutput();
        ProductionPreparedOutputContract.ValidateForBill(
            record.preparedOutput,
            BillId,
            RecipeId,
            1,
            DestinationId);
        Require(record.preparedOutput.phase ==
                ProductionPreparedOutputPhase.Unresolved,
            "completed prepared output did not clear to canonical Unresolved");
    }

    private static void VerifyInvalidStatesFailLoud()
    {
        ProductionPreparedOutputBatchSaveData invalidPhase = CreateResolvedBatch();
        invalidPhase.phase = (ProductionPreparedOutputPhase)999;
        RequireInvalid(invalidPhase, "undefined phase");

        ProductionPreparedOutputBatchSaveData duplicateLine = CreateResolvedBatch();
        duplicateLine.lines.Add(duplicateLine.lines[1].Clone());
        RequireInvalid(duplicateLine, "duplicate line");

        ProductionPreparedOutputBatchSaveData nonCanonical = CreateResolvedBatch();
        nonCanonical.lines[1].outputLineId = " output:main";
        RequireInvalid(nonCanonical, "noncanonical line ID");

        ProductionPreparedOutputBatchSaveData capabilityFingerprintDrift =
            CreateResolvedBatch();
        capabilityFingerprintDrift.lines[1].outputCapabilityFingerprint =
            Digest('9');
        RequireInvalid(
            capabilityFingerprintDrift,
            "output capability fingerprint drift");

        ProductionPreparedOutputBatchSaveData capabilityVersionDrift =
            CreateResolvedBatch();
        capabilityVersionDrift.lines[1].outputCapabilityVersion++;
        RequireInvalid(capabilityVersionDrift, "output capability version drift");

        ProductionPreparedOutputBatchSaveData componentCodecVersionDrift =
            CreateResolvedBatch();
        componentCodecVersionDrift.lines[1].outputComponentCodecVersion++;
        RequireInvalid(componentCodecVersionDrift, "output component codec drift");

        ProductionPreparedOutputBatchSaveData declaredLossCapability =
            CreateResolvedBatch();
        declaredLossCapability.lines[0].outputCapabilityId =
            ProductionOutputCapabilityIds.StandardDefinition;
        RequireInvalid(
            declaredLossCapability,
            "declared loss with physical output capability");

        ProductionPreparedOutputBatchSaveData negativeMass = CreateResolvedBatch();
        negativeMass.lines[1].exactMassGrams = -1L;
        RequireInvalid(negativeMass, "negative line mass");

        ProductionPreparedOutputBatchSaveData staleSchema = CreateResolvedBatch();
        staleSchema.schemaVersion = 2;
        RequireInvalid(staleSchema, "stale capacity-source schema");

        ProductionPreparedOutputBatchSaveData futureSchema = CreateResolvedBatch();
        futureSchema.schemaVersion =
            ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion + 1;
        RequireInvalid(futureSchema, "unknown future capacity-source schema");

        ProductionPreparedOutputBatchSaveData invalidCapacityDigest =
            CreateResolvedBatch();
        invalidCapacityDigest.capacitySourceDigest = Digest('a').ToUpperInvariant();
        RequireInvalid(invalidCapacityDigest, "noncanonical capacity source digest");

        ProductionPreparedOutputBatchSaveData emptyCapacityDigest =
            CreateResolvedBatch();
        emptyCapacityDigest.capacitySourceDigest = string.Empty;
        RequireInvalid(emptyCapacityDigest, "empty capacity source digest");

        ProductionPreparedOutputBatchSaveData shortCapacityDigest =
            CreateResolvedBatch();
        shortCapacityDigest.capacitySourceDigest = new string('a', 63);
        RequireInvalid(shortCapacityDigest, "short capacity source digest");

        ProductionPreparedOutputBatchSaveData nonHexCapacityDigest =
            CreateResolvedBatch();
        nonHexCapacityDigest.capacitySourceDigest = new string('g', 64);
        RequireInvalid(nonHexCapacityDigest, "nonhex capacity source digest");

        ProductionPreparedOutputBatchSaveData partialMaximumProof =
            CreateResolvedBatch();
        partialMaximumProof.capacityClaimDigest = string.Empty;
        RequireInvalid(partialMaximumProof, "partial maximum-mass proof tuple");

        ProductionPreparedOutputBatchSaveData normalWithoutProof =
            CreateResolvedBatch();
        normalWithoutProof.maximumMassProofDigest = string.Empty;
        normalWithoutProof.maximumBatchMassGrams = 0L;
        normalWithoutProof.capacityClaimDigest = string.Empty;
        RequireInvalid(normalWithoutProof, "normal output without maximum proof");

        RequireInvalid(
            CreateRuinedBatch(includeProof: false),
            "ruined output without maximum-mass proof");

        ProductionPreparedOutputBatchSaveData invalidCycle = CreateResolvedBatch();
        invalidCycle.outputBufferCycleCapacity = 1;
        RequireInvalid(invalidCycle, "output-buffer cycle below authored range");

        ProductionPreparedOutputBatchSaveData excessiveCycle = CreateResolvedBatch();
        excessiveCycle.outputBufferCycleCapacity = 5;
        RequireInvalid(excessiveCycle, "output-buffer cycle above authored range");

        ProductionPreparedOutputBatchSaveData invalidProjectedCapacity =
            CreateResolvedBatch();
        invalidProjectedCapacity.projectedPortfolioCapacityGrams = 0L;
        RequireInvalid(invalidProjectedCapacity, "missing projected portfolio capacity");

        ProductionPreparedOutputBatchSaveData negativeProjectedCapacity =
            CreateResolvedBatch();
        negativeProjectedCapacity.projectedPortfolioCapacityGrams = -1L;
        RequireInvalid(negativeProjectedCapacity, "negative projected portfolio capacity");

        ProductionPreparedOutputBatchSaveData invalidMinimumCapacity =
            CreateResolvedBatch();
        invalidMinimumCapacity.requiredMinimumCapacityGrams = 3_999L;
        RequireInvalid(invalidMinimumCapacity, "capacity minimum arithmetic mismatch");

        ProductionPreparedOutputBatchSaveData excessiveMinimumCapacity =
            CreateResolvedBatch();
        excessiveMinimumCapacity.requiredMinimumCapacityGrams = 4_001L;
        RequireInvalid(excessiveMinimumCapacity, "excessive capacity minimum mismatch");

        ProductionPreparedOutputBatchSaveData overflowingMinimum =
            CreateResolvedBatch();
        overflowingMinimum.totalPhysicalMassGrams = long.MaxValue;
        RequireRejected(overflowingMinimum, "capacity minimum multiplication overflow");

        ProductionPreparedOutputBatchSaveData partial = CreatePublishedBatch();
        partial.physicalCandidates[0].quantity = 1;
        partial.physicalCandidates.RemoveAt(1);
        RequireInvalid(partial, "partial physical publication");

        ProductionPreparedOutputBatchSaveData extra = CreatePublishedBatch();
        ProductionPreparedOutputPhysicalCandidateSaveData duplicate =
            extra.physicalCandidates[0].Clone();
        duplicate.stackId = "stack:prepared-output:9999";
        extra.physicalCandidates.Add(duplicate);
        RequireInvalid(extra, "extra physical publication");

        ProductionPreparedOutputBatchSaveData unresolved =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolved.outcomeFingerprint = Digest('a');
        RequireInvalid(unresolved, "unresolved payload with prepared authority");

        ProductionPreparedOutputBatchSaveData unresolvedCapacity =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolvedCapacity.capacitySourceDigest = Digest('a');
        RequireInvalid(unresolvedCapacity, "unresolved payload with capacity authority");

        ProductionPreparedOutputBatchSaveData unresolvedCycle =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolvedCycle.outputBufferCycleCapacity = 4;
        RequireInvalid(unresolvedCycle, "unresolved payload with output-buffer cycle");

        ProductionPreparedOutputBatchSaveData unresolvedProjected =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolvedProjected.projectedPortfolioCapacityGrams = 1L;
        RequireInvalid(unresolvedProjected, "unresolved payload with projected capacity");

        ProductionPreparedOutputBatchSaveData unresolvedMinimum =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolvedMinimum.requiredMinimumCapacityGrams = 1L;
        RequireInvalid(unresolvedMinimum, "unresolved payload with minimum capacity");

        ProductionPreparedOutputBatchSaveData unresolvedProof =
            ProductionPreparedOutputBatchSaveData.Unresolved();
        unresolvedProof.maximumMassProofDigest = Digest('1');
        RequireInvalid(unresolvedProof, "unresolved payload with maximum-mass proof");

        ProductionBillRecord record = CreateRecord();
        RequireThrows(
            () => record.MarkPreparedOutputPublicationPrepared(Digest('b')),
            "invalid aggregate phase transition");
    }

    private static ProductionBillRecord CreateRecord()
    {
        ProductionBillRecord record = ProductionBillRecord.Create(
            BillId,
            RecipeId,
            new BuildingInstanceId("building:prepared-output-fixture"),
            ProductionOrderMode.RepeatCount,
            remainingCycles: 1,
            targetStock: 0,
            ProductionBatchStage.None,
            "production-input:" + BillId.Value);
        record.SetOutputDestination(DestinationId);
        return record;
    }

    private static ProductionPreparedOutputBatchSaveData CreateResolvedBatch()
    {
        string outcome = Digest('a');
        string batchCommit = ProductionPreparedOutputIdentity.BuildBatchCommitId(
            BillId,
            1,
            outcome);
        ProductionPreparedOutputLineSaveData loss = new()
        {
            outputLineId = "output:loss",
            role = ProductionOutputRole.DeclaredLoss,
            componentFingerprint = Digest('c'),
            qualityPermille = 1000,
            rollKind = "deterministic",
            rollValue = 0L,
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 50L
        };
        loss.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
            batchCommit,
            loss.outputLineId);
        ProductionPreparedOutputLineSaveData main = new()
        {
            outputLineId = "output:main",
            role = ProductionOutputRole.Main,
            itemId = "material:test-output",
            outputCapabilityId = ProductionOutputCapabilityIds.StandardDefinition,
            outputCapabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
            outputComponentCodecId =
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            outputComponentCodecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            outputCapabilityFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    "output:main",
                    "material:test-output",
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            quantity = 2,
            componentPayload = "components:none",
            componentFingerprint = Digest('d'),
            qualityPermille = 1000,
            rollKind = "deterministic",
            rollValue = 0L,
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 1000L
        };
        main.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
            batchCommit,
            main.outputLineId);
        return new ProductionPreparedOutputBatchSaveData
        {
            phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
            billId = BillId.Value,
            cycleSequence = 1,
            recipeId = RecipeId,
            destinationId = DestinationId,
            recipeDefinitionDigest = Digest('e'),
            migrationProfileDigest = Digest('f'),
            capacitySourceDigest = Digest('a'),
            maximumMassProofDigest = Digest('1'),
            maximumBatchMassGrams = 1_000L,
            capacityClaimDigest = Digest('2'),
            outputBufferCycleCapacity = 4,
            projectedPortfolioCapacityGrams = 4_000L,
            requiredMinimumCapacityGrams = 4_000L,
            outcomeFingerprint = outcome,
            batchCommitId = batchCommit,
            totalPhysicalMassGrams = 1000L,
            totalDeclaredLossMassGrams = 50L,
            lines = new List<ProductionPreparedOutputLineSaveData> { loss, main }
        };
    }

    private static ProductionPreparedOutputBatchSaveData CreatePublishedBatch()
    {
        ProductionPreparedOutputBatchSaveData batch = CreateResolvedBatch();
        batch.phase =
            ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending;
        batch.admissionFingerprint = Digest('b');
        batch.physicalCandidates = new List<
            ProductionPreparedOutputPhysicalCandidateSaveData>
        {
            Candidate(batch, "stack:prepared-output:0001", 1, 500L),
            Candidate(batch, "stack:prepared-output:0002", 1, 500L)
        };
        return batch;
    }

    private static ProductionPreparedOutputBatchSaveData CreateRuinedBatch(
        bool includeProof)
    {
        ProductionPreparedOutputBatchSaveData batch = CreateResolvedBatch();
        ProductionPreparedOutputLineSaveData main = batch.lines.Single(
            line => line.role == ProductionOutputRole.Main);
        main.quantity = 0;
        main.rollSucceeded = false;
        main.exactMassGrams = 0L;

        const string wasteLineId =
            ProductionRuinedBatchDispositionPlan.RecoverableWasteOutputLineId;
        const string wasteItemId = "waste:test-ruined";
        ProductionPreparedOutputLineSaveData waste = new()
        {
            outputLineId = wasteLineId,
            role = ProductionOutputRole.RecoverableWaste,
            itemId = wasteItemId,
            outputCapabilityId =
                ProductionOutputCapabilityIds.StandardDefinition,
            outputCapabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
            outputComponentCodecId =
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            outputComponentCodecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            outputCapabilityFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    wasteLineId,
                    wasteItemId,
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            quantity = 1,
            componentPayload = "components:none",
            componentFingerprint = Digest('7'),
            qualityPermille = 1000,
            rollKind = "ruined-batch",
            rollValue = 0L,
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 600L
        };
        waste.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
            batch.batchCommitId,
            waste.outputLineId);
        batch.lines.Add(waste);
        batch.lines = batch.lines
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToList();
        batch.totalPhysicalMassGrams = 600L;
        batch.maximumMassProofDigest = string.Empty;
        batch.maximumBatchMassGrams = 0L;
        batch.capacityClaimDigest = string.Empty;
        if (includeProof)
        {
            batch.maximumMassProofDigest = Digest('1');
            batch.maximumBatchMassGrams = 600L;
            batch.capacityClaimDigest = Digest('2');
        }
        return batch;
    }

    private static ProductionPreparedOutputPhysicalCandidateSaveData Candidate(
        ProductionPreparedOutputBatchSaveData batch,
        string stackId,
        int quantity,
        long massGrams)
    {
        ProductionPreparedOutputLineSaveData main = batch.lines.Single(
            line => line.role == ProductionOutputRole.Main);
        return new ProductionPreparedOutputPhysicalCandidateSaveData
        {
            stackId = stackId,
            batchCommitId = batch.batchCommitId,
            outputLineId = main.outputLineId,
            lineCommitId = main.lineCommitId,
            itemId = main.itemId,
            quantity = quantity,
            massGrams = massGrams,
            destinationId = batch.destinationId,
            state = ProductionPreparedPhysicalCandidateState.FacilityOutputBuffer
        };
    }

    private static void RequireInvalid(
        ProductionPreparedOutputBatchSaveData batch,
        string label) => RequireThrows(
        () => ProductionPreparedOutputContract.ValidateForBill(
            batch,
            BillId,
            RecipeId,
            1,
            DestinationId),
        label);

    private static void RequireRejected(
        ProductionPreparedOutputBatchSaveData batch,
        string label)
    {
        try
        {
            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                BillId,
                RecipeId,
                1,
                DestinationId);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or OverflowException)
        {
            return;
        }
        throw new InvalidOperationException(label);
    }

    private static void RequireThrows(Action action, string label)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Prepared-output contract accepted {label}.");
    }

    private static string Digest(char value) => new string(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
