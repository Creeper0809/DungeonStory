#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// This fixture deliberately lives in the runtime assembly so it can verify the
// internal receipt owner without widening the production API. Player builds do
// not compile it. Actual funeral/archive producers are verified separately.
public static class Wim040ObservedLifeEventReceiptDebugFixture
{
    public static void Run(V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = NewCampaign(catalog);
        ObservedFuneralLifeEventReceipt funeral = new(
            "funeral-operation:wim040:grave-visit",
            new CharacterId("character:wim040:deceased"),
            "building:wim040:memorial",
            absoluteDay: 42,
            generation: 1,
            new[]
            {
                new CharacterId("character:wim040:participant:b"),
                new CharacterId("character:wim040:participant:a")
            });
        ObservedLifeEventCommitResult funeralCommit =
            campaign.RecordObservedFuneralLifeEvent(funeral);
        Require(
            funeralCommit.StateChanged
            && funeralCommit.Resolution.HasValue
            && funeralCommit.Resolution.Value.DefinitionId
                == "life-event:grave-visit"
            && funeralCommit.Resolution.Value.ParticipantCharacterIds
                .SequenceEqual(new[] { "character:wim040:participant:a" })
            && funeralCommit.Resolution.Value.Effects.Count == 1
            && funeralCommit.Resolution.Value.Effects[0].kind
                == V20ContentEffectKind.Trauma,
            "Observed funeral did not resolve one deterministic participant with the authored Trauma payload.");

        SocietyEventWorldSaveData afterFuneral = campaign.CaptureSociety();
        Require(
            afterFuneral.successfulLifeEventOperations.Count == 1
            && afterFuneral.recentResolvedEvents.Count == 1
            && afterFuneral.successfulLifeEventOperations[0].sourceKind
                == ObservedLifeEventSourceKind.Funeral
            && afterFuneral.successfulLifeEventOperations[0].sourceOperationId
                == funeral.SourceOperationId
            && afterFuneral.successfulLifeEventOperations[0].canonicalPayload
                == funeral.CanonicalPayload
            && afterFuneral.successfulLifeEventOperations[0].occurrenceInstanceId
                == afterFuneral.recentResolvedEvents[0].instanceId,
            "Observed funeral did not retain its exact durable source-to-occurrence join.");
        string afterFuneralJson = JsonUtility.ToJson(afterFuneral);
        Require(
            !campaign.RecordObservedFuneralLifeEvent(funeral).StateChanged
            && JsonUtility.ToJson(campaign.CaptureSociety()) == afterFuneralJson,
            "Exact funeral replay changed the Society receipt owner.");
        MustReject(
            () => campaign.RecordObservedFuneralLifeEvent(
                new ObservedFuneralLifeEventReceipt(
                    funeral.SourceOperationId,
                    funeral.DeceasedCharacterId,
                    "building:wim040:other-memorial",
                    funeral.AbsoluteDay,
                    funeral.Generation,
                    funeral.ParticipantCharacterIds)),
            "Conflicting funeral payload was accepted for the same source operation.");
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == afterFuneralJson,
            "Rejected funeral conflict changed live Society state.");

        CharacterTombstoneSaveData tombstone = new()
        {
            characterId = "character:wim040:lineage",
            phenotypeSpeciesId = "species:human",
            householdId = "household:wim040:lineage",
            birthAbsoluteDay = 1,
            deathAbsoluteDay = 10,
            famous = false,
            generation = 2
        };
        LineageSummarySaveData summary = new()
        {
            householdId = tombstone.householdId,
            generation = tombstone.generation,
            archivedCharacterCount = 1,
            earliestBirthDay = tombstone.birthAbsoluteDay,
            latestDeathDay = tombstone.deathAbsoluteDay
        };
        ObservedLineageCompressionLifeEventReceipt lineage = new(
            tombstone,
            archiveAbsoluteDay: 200,
            summary);
        ObservedLifeEventCommitResult lineageCommit =
            campaign.RecordObservedLineageCompressionLifeEvent(lineage);
        Require(
            lineageCommit.StateChanged
            && lineageCommit.Resolution.HasValue
            && lineageCommit.Resolution.Value.DefinitionId
                == "life-event:story-compressed"
            && lineageCommit.Resolution.Value.ParticipantCharacterIds
                .SequenceEqual(new[] { tombstone.characterId })
            && lineageCommit.Resolution.Value.Effects.Count == 1
            && lineageCommit.Resolution.Value.Effects[0].kind
                == V20ContentEffectKind.WorldFlag,
            "Observed lineage compression did not resolve the removed tombstone with the authored WorldFlag payload.");
        string completeJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(completeJson)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == completeJson
            && !campaign.RecordObservedFuneralLifeEvent(funeral).StateChanged
            && !campaign.RecordObservedLineageCompressionLifeEvent(lineage)
                .StateChanged,
            "Observed life-event receipt or replay contract changed during current-format Society restore.");

        SocietyEventWorldSaveData duplicated =
            JsonUtility.FromJson<SocietyEventWorldSaveData>(completeJson);
        duplicated.successfulLifeEventOperations.Add(
            duplicated.successfulLifeEventOperations[0]);
        MustReject(
            () => campaign.PrepareSociety(duplicated),
            "Duplicate observed life-event receipt was accepted.");
        SocietyEventWorldSaveData tampered =
            JsonUtility.FromJson<SocietyEventWorldSaveData>(completeJson);
        tampered.successfulLifeEventOperations[0].canonicalPayload += ":tampered";
        MustReject(
            () => campaign.PrepareSociety(tampered),
            "Tampered observed life-event payload was accepted.");
        SocietyEventWorldSaveData orphaned =
            JsonUtility.FromJson<SocietyEventWorldSaveData>(completeJson);
        string orphanOccurrence = orphaned.successfulLifeEventOperations[0]
            .occurrenceInstanceId;
        orphaned.recentResolvedEvents.RemoveAll(value => value != null
            && string.Equals(
                value.instanceId,
                orphanOccurrence,
                StringComparison.Ordinal));
        MustReject(
            () => campaign.PrepareSociety(orphaned),
            "Observed life-event receipt without its exact resolved occurrence was accepted.");
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == completeJson,
            "Rejected observed life-event restore candidate changed live Society state.");

        V20CampaignRuntime retention = NewCampaign(catalog);
        const int retainedReceiptCount = 270;
        for (int index = 0; index < retainedReceiptCount; index++)
        {
            ObservedFuneralLifeEventReceipt retainedReceipt =
                CreateRetainedReceipt(index);
            ObservedLifeEventCommitResult retained =
                retention.RecordObservedFuneralLifeEvent(retainedReceipt);
            Require(retained.StateChanged && retained.Resolution.HasValue,
                "Receipt-owned life event was lost before the ordinary history cap.");
        }
        SocietyEventWorldSaveData retainedState = retention.CaptureSociety();
        Require(
            retainedState.successfulLifeEventOperations.Count
                == retainedReceiptCount
            && retainedState.recentResolvedEvents.Count
                == retainedReceiptCount,
            "Receipt-owned resolved events were pruned by the ordinary 256-entry history cap.");
        string retainedJson = JsonUtility.ToJson(retainedState);
        retention.PublishSociety(retention.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(retainedJson)));
        Require(
            JsonUtility.ToJson(retention.CaptureSociety()) == retainedJson,
            "Receipt-owned history beyond the ordinary cap changed during current-format restore.");
        Require(
            !retention.RecordObservedFuneralLifeEvent(
                CreateRetainedReceipt(0)).StateChanged
            && !retention.RecordObservedFuneralLifeEvent(
                CreateRetainedReceipt(retainedReceiptCount - 1)).StateChanged
            && JsonUtility.ToJson(retention.CaptureSociety()) == retainedJson,
            "Restored receipt-owned history lost exact-once replay at the retention boundary.");

        VerifyCareerObservedReceiptLifecycle(catalog);
    }

    private static void VerifyCareerObservedReceiptLifecycle(
        V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = NewCampaign(catalog);
        CharacterId mentorId = new("character:wim040:career:mentor");
        CharacterId studentId = new("character:wim040:career:student");
        BuildingInstanceId academyId = new("building:wim040:career:academy");
        CharacterProficiencyId proficiencyId = new("proficiency:crafting");
        CareerMentorshipSnapshot mentorship = new(
            mentorId,
            studentId,
            academyId,
            proficiencyId,
            1,
            0,
            0f,
            0f);
        ProductionDeclaredLossCycleReceipt productionLoss = new(
            new ProductionBillId("production-bill:wim040:career-loss"),
            1,
            "recipe:wim040:career-loss",
            new BuildingInstanceId("building:wim040:career-production"),
            studentId.Value,
            CreateCompletedLossBatch());
        ObservedProductionLossLifeEventReceipt observedLoss = new(
            productionLoss,
            mentorship,
            absoluteDay: 42,
            generation: 1);
        ObservedLifeEventCommitResult lossCommit =
            campaign.RecordObservedProductionLossLifeEvent(observedLoss);
        Require(lossCommit.StateChanged && !lossCommit.Resolution.HasValue,
            "Production declared loss did not create one pending apprentice-mistake occurrence.");

        CharacterCareerSnapshot retirement = new(
            mentorId,
            retired: false,
            default,
            string.Empty,
            0,
            0f,
            RetirementScheduleStatus.Pending,
            "life-event:retirement-request",
            "one-more-season",
            40,
            70,
            0);
        CombatEquipmentInstance protectiveEquipment = new()
        {
            instanceId = "equipment:wim040:last-lesson:blast-coat",
            definitionId = "armor:powder-cuirass",
            ownerCharacterId = mentorId.Value,
            worldState = CombatEquipmentWorldState.Equipped
        };
        const string LedgerStackId = "stack:wim040:last-lesson:career-ledger";
        const long BeforeRevision = 4L;
        const long AfterRevision = 5L;
        CareerMentorshipAwardCommitReceipt award =
            CareerMentorshipAwardCommitReceipt.Restore(
                academyId,
                LedgerStackId,
                BeforeRevision,
                AfterRevision,
                "career-mentorship-award:" + LedgerStackId + ":5");
        ObservedLastLessonLifeEventReceipt lastLesson = new(
            mentorship,
            retirement,
            protectiveEquipment,
            award,
            absoluteDay: 42,
            generation: 1);
        ObservedLifeEventCommitResult lessonCommit =
            campaign.RecordObservedLastLessonLifeEvent(lastLesson);
        Require(lessonCommit.StateChanged && !lessonCommit.Resolution.HasValue,
            "Durable last mentorship award did not create one pending last-lesson occurrence.");

        SocietyEventWorldSaveData state = campaign.CaptureSociety();
        V20ActiveEventSaveData lossOccurrence = state.activeEvents.Single(value =>
            value != null
            && value.definitionId == "life-event:apprentice-mistake");
        V20ActiveEventSaveData lessonOccurrence = state.activeEvents.Single(value =>
            value != null
            && value.definitionId == "life-event:last-lesson");
        Require(
            !lossOccurrence.resolved
            && lossOccurrence.participantCharacterIds.SequenceEqual(
                new[] { studentId.Value })
            && !lessonOccurrence.resolved
            && lessonOccurrence.participantCharacterIds.SequenceEqual(
                new[] { mentorId.Value })
            && state.successfulLifeEventOperations.Count == 2
            && state.successfulLifeEventOperations.Any(value => value != null
                && value.sourceKind
                    == ObservedLifeEventSourceKind.ProductionDeclaredLoss
                && value.sourceOperationId == observedLoss.SourceOperationId
                && value.canonicalPayload == observedLoss.CanonicalPayload
                && value.occurrenceInstanceId == lossOccurrence.instanceId)
            && state.successfulLifeEventOperations.Any(value => value != null
                && value.sourceKind
                    == ObservedLifeEventSourceKind.LastMentorshipLesson
                && value.sourceOperationId == lastLesson.SourceOperationId
                && value.canonicalPayload == lastLesson.CanonicalPayload
                && value.occurrenceInstanceId == lessonOccurrence.instanceId),
            "Career observed-life receipts lost their exact source, participant, or pending occurrence join.");

        string json = JsonUtility.ToJson(state);
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(json)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == json
            && !campaign.RecordObservedProductionLossLifeEvent(observedLoss)
                .StateChanged
            && !campaign.RecordObservedLastLessonLifeEvent(lastLesson)
                .StateChanged
            && JsonUtility.ToJson(campaign.CaptureSociety()) == json,
            "Career observed-life receipts changed or replayed after current Society restore.");
    }

    public static ProductionPreparedOutputBatchSaveData
        CreateCompletedLossBatch()
    {
        ProductionBillId billId = new(
            "production-bill:wim040:career-loss");
        const string RecipeId = "recipe:wim040:career-loss";
        BuildingInstanceId facilityId = new(
            "building:wim040:career-production");
        string outcome = Digest('a');
        string batchCommitId =
            ProductionPreparedOutputIdentity.BuildBatchCommitId(
                billId,
                1,
                outcome);
        ProductionPreparedOutputLineSaveData loss = new()
        {
            outputLineId = "output:loss",
            role = ProductionOutputRole.DeclaredLoss,
            componentFingerprint = Digest('c'),
            qualityPermille = 1000,
            rollKind = "deterministic",
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 50L
        };
        loss.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
            batchCommitId,
            loss.outputLineId);
        const string MainLineId = "output:main";
        const string ItemId = "material:wim040:career-output";
        ProductionPreparedOutputLineSaveData main = new()
        {
            outputLineId = MainLineId,
            role = ProductionOutputRole.Main,
            itemId = ItemId,
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
                    MainLineId,
                    ItemId,
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            quantity = 1,
            componentPayload = "components:none",
            componentFingerprint = Digest('d'),
            qualityPermille = 1000,
            rollKind = "deterministic",
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 1_000L
        };
        main.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
            batchCommitId,
            main.outputLineId);
        string destination =
            ProductionOutputDestinationId.FromFacility(facilityId).Value;
        return new ProductionPreparedOutputBatchSaveData
        {
            phase = ProductionPreparedOutputPhase.Completed,
            billId = billId.Value,
            cycleSequence = 1,
            recipeId = RecipeId,
            destinationId = destination,
            recipeDefinitionDigest = Digest('e'),
            migrationProfileDigest = Digest('f'),
            capacitySourceDigest = Digest('1'),
            maximumMassProofDigest = Digest('2'),
            maximumBatchMassGrams = 1_000L,
            capacityClaimDigest = Digest('3'),
            outputBufferCycleCapacity = 4,
            projectedPortfolioCapacityGrams = 4_000L,
            requiredMinimumCapacityGrams = 4_000L,
            outcomeFingerprint = outcome,
            admissionFingerprint = Digest('4'),
            batchCommitId = batchCommitId,
            totalPhysicalMassGrams = 1_000L,
            totalDeclaredLossMassGrams = 50L,
            lines = new List<ProductionPreparedOutputLineSaveData>
            {
                loss,
                main
            },
            physicalCandidates = new List<
                ProductionPreparedOutputPhysicalCandidateSaveData>
            {
                new()
                {
                    stackId = "stack:wim040:career-output",
                    batchCommitId = batchCommitId,
                    outputLineId = main.outputLineId,
                    lineCommitId = main.lineCommitId,
                    itemId = main.itemId,
                    quantity = 1,
                    massGrams = 1_000L,
                    destinationId = destination,
                    state = ProductionPreparedPhysicalCandidateState
                        .FacilityOutputBuffer
                }
            }
        };
    }

    private static string Digest(char value) => new(value, 64);

    private static ObservedFuneralLifeEventReceipt CreateRetainedReceipt(
        int index)
    {
        string suffix = index.ToString("D3");
        return new ObservedFuneralLifeEventReceipt(
            "funeral-operation:wim040:retained:" + suffix,
            new CharacterId("character:wim040:retained-deceased:" + suffix),
            "building:wim040:memorial",
            absoluteDay: 42,
            generation: 1,
            new[]
            {
                new CharacterId(
                    "character:wim040:retained-participant:" + suffix)
            });
    }

    private static V20CampaignRuntime NewCampaign(
        V20StoryContentCatalog catalog) =>
        new(new DungeonRuntimeAggregateRootStore(), catalog);

    private static void MustReject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(reason);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
#endif
