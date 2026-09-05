#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputResumeAuthorityDebugScenarios
{
    private const string RecipeId = "recipe:hay-feed";
    private const string DestinationId =
        "production-output:building:prepared-resume-qa";
    private const string OutputLineId = "output:main";
    private const string OutcomeFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RecipeDigest =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ComponentFingerprint =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [MenuItem("DungeonStory/Debug/Economy/Run Prepared Output Resume Authority")]
    public static void RunAll()
    {
        VerifyCycleStartAndAdditionalCapacityPolicy();
        VerifyMigratedLegacyAuthorityDetection();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_RESUME_AUTHORITY=PASS");
    }

    private static void VerifyCycleStartAndAdditionalCapacityPolicy()
    {
        ProductionBillRecord record = ProductionBillRecord.Create(
            (ProductionBillId)"production-bill:prepared-resume-qa",
            RecipeId,
            (BuildingInstanceId)"building:prepared-resume-qa",
            ProductionOrderMode.RepeatCount,
            1,
            0,
            ProductionBatchStage.None,
            "production-input:prepared-resume-qa");
        record.SetOutputDestination(DestinationId);

        Require(
            ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            && ProductionPreparedOutputMigrationScope
                .RequiresAdditionalOutputCapacity(record),
            "A fresh migrated cycle did not require its initial capacity assessment.");

        record.SetMaterialsConsumed(true);
        Require(
            !ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            && !ProductionPreparedOutputMigrationScope
                .RequiresAdditionalOutputCapacity(record),
            "An active WIP cycle was treated as a second cycle start.");

        ProductionPreparedOutputBatchSaveData resolved = CreateResolved(record);
        record.ResolvePreparedOutput(resolved);
        Require(
            !ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            && ProductionPreparedOutputMigrationScope
                .RequiresAdditionalOutputCapacity(record),
            "Resolved output waiting for space did not retain its exact capacity need.");

        record.MarkPreparedOutputPublicationPrepared(Digest('d'));
        Require(
            !ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            && !ProductionPreparedOutputMigrationScope
                .RequiresAdditionalOutputCapacity(record),
            "An already-reserved prepared batch was charged capacity twice.");

        ProductionPreparedOutputBatchSaveData publication = record.preparedOutput;
        record.MarkPreparedOutputPhysicalBatchCommitted(new[]
        {
            new ProductionPreparedOutputPhysicalCandidateSaveData
            {
                stackId = "world-item-stack:prepared-resume-qa",
                batchCommitId = publication.batchCommitId,
                outputLineId = OutputLineId,
                lineCommitId = publication.lines[0].lineCommitId,
                itemId = publication.lines[0].itemId,
                quantity = publication.lines[0].quantity,
                massGrams = publication.lines[0].exactMassGrams,
                destinationId = publication.destinationId,
                state = ProductionPreparedPhysicalCandidateState
                    .FacilityOutputBuffer
            }
        });
        record.MarkPreparedOutputCompleted();
        Require(
            !ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            && !ProductionPreparedOutputMigrationScope
                .RequiresAdditionalOutputCapacity(record),
            "A committed prepared batch was charged output capacity again.");
    }

    private static void VerifyMigratedLegacyAuthorityDetection()
    {
        ProductionBillSaveData saved = new()
        {
            billId = "production-bill:prepared-resume-save-qa",
            recipeId = RecipeId,
            outputReservations = new List<ProductionOutputReservationSaveData>
            {
                new()
                {
                    itemId = "feed:hay",
                    amount = 1
                }
            }
        };
        Require(
            ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(saved),
            "A migrated count reservation was not recognized as legacy authority.");
        saved.outputReservations.Clear();
        Require(
            !ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(saved),
            "An empty migrated save was reported as legacy output authority.");
    }

    private static ProductionPreparedOutputBatchSaveData CreateResolved(
        ProductionBillRecord record)
    {
        string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
            record.billId,
            record.cycleSequence,
            OutcomeFingerprint);
        return new ProductionPreparedOutputBatchSaveData
        {
            phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
            billId = record.billId.Value,
            cycleSequence = record.cycleSequence,
            recipeId = record.recipeId,
            destinationId = record.outputDestinationId,
            recipeDefinitionDigest = RecipeDigest,
            migrationProfileDigest = new string('f', 64),
            capacitySourceDigest = new string('e', 64),
            outputBufferCycleCapacity = 4,
            projectedPortfolioCapacityGrams = 4_000L,
            requiredMinimumCapacityGrams = 4_000L,
            outcomeFingerprint = OutcomeFingerprint,
            batchCommitId = batchCommitId,
            totalPhysicalMassGrams = 1_000L,
            lines = new List<ProductionPreparedOutputLineSaveData>
            {
                new()
                {
                    outputLineId = OutputLineId,
                    role = ProductionOutputRole.Main,
                    itemId = "feed:hay",
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
                            OutputLineId,
                            "feed:hay",
                            ProductionOutputCapabilityIds.StandardDefinition,
                            ProductionOutputCapabilityIds.StandardDefinitionVersion,
                            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                    quantity = 1,
                    componentFingerprint = ComponentFingerprint,
                    rollKind = "resume-qa",
                    rollUpperExclusive = 1L,
                    rollSucceeded = true,
                    exactMassGrams = 1_000L,
                    lineCommitId = ProductionPreparedOutputIdentity
                        .BuildLineCommitId(batchCommitId, OutputLineId)
                }
            }
        };
    }

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
