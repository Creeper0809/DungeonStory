#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Operation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

// Focused WIM-040 delta witness. The previously accepted callback/cap/cooldown,
// ownership, save and alert scenarios remain owned by their existing runner.
public static class Wim040IncidentCompletionDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-040-incident-completion-focused.txt";

    [MenuItem("DungeonStory/QA/Run WIM-040 Incident Completion Focused")]
    public static string RunAll() => Run(ReportPath, createOnly: false);

    public static string RunForArchitectureBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Incident completion verification requires isolated batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Incident completion verification requires its isolated evidence directory.");
        string report = Path.Combine(output, "wim-040-incident-completion-focused.txt");
        if (File.Exists(report))
            throw new IOException("Incident completion evidence already exists: " + report);
        return Run(report, createOnly: true);
    }

    private static string Run(string reportPath, bool createOnly)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        try
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            VerifyConsumedContamination(catalog);
            VerifyCulturalConflictReceipt(catalog);
            VerifyResponseReceiptLifecycle(catalog);
            VerifyResponseFailureLifecycle(catalog);
            VerifyTransferReceiptLifecycle(catalog);
            RunIncidentTransferOwnershipContracts();
            VerifyLifeEventOccurrencePolicy(catalog);
            VerifyObservedLifeEventReceiptLifecycle(catalog);
            VerifyProductionLossProducerPublication();
            VerifyLineageCompressionProducer(catalog);
            WriteReport(
                reportPath,
                "result=PASS\n"
                + "PASS consumed contaminated meal creates only Contamination with exact meal operation evidence and survives current JSON\n"
                + "PASS committed cross-culture visitor-facility receipt creates CulturalInsult with exact replay and current JSON\n"
                + "PASS replace/emergency-care accept pending, preserve owner/receipt through current JSON, and publish authored effects only after receipt\n"
                + "PASS failed/cancelled responses release active owners, preserve an effect-free terminal history, and replay only the exact terminal payload\n"
                + "PASS transfer accepts pending, freezes its exact isolation destination, survives current JSON and publishes only after arrival receipt\n"
                + "PASS transient transfer attempts require exact mover identity, retain preemption evidence, release abandoned attempts, and reset through the adapter restore hook without weakening the tick fence; movement provenance requires operation/external/facility/destination\n"
                + "PASS only the authored retirement request participates in generic daily life-event cadence; the other 31 definitions remain externally observed\n"
                + "PASS funeral and lineage-compression receipts create their typed life-event payload, replay exactly after current Society restore, reject conflict/tamper/orphan state and remain durable beyond the ordinary resolved-history cap\n"
                + "PASS actual production completion publishes an exact actor-bound declared-loss source, while non-actor compatibility publication does not invent one\n"
                + "PASS apprentice-mistake and last-lesson pending occurrences retain exact source/participant joins and replay no state after current Society restore\n"
                + "PASS KinshipHouseholdRuntime archives an actual cold tombstone once, commits the story-compressed receipt and WorldFlag, and publishes one resolved alert/effect event\n"
                + "scope=isolated production catalog/campaign receipt owner, actual production-loss source publication and actual lineage archive producer; last-lesson CareerApplicationAdapter tick and funeral transaction require separate live witnesses\n",
                createOnly);
            return "PASS " + reportPath;
        }
        catch (Exception error)
        {
            WriteReport(reportPath, "result=FAIL\n" + error, createOnly);
            throw;
        }
    }

    internal static void RunIncidentTransferOwnershipContracts()
    {
        GameObject first = new("C16-transfer-first");
        GameObject restored = new("C16-transfer-restored");
        try
        {
            VerifyIncidentTransferOwnershipContracts(
                first.AddComponent<AbilityMove>(), restored.AddComponent<AbilityMove>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(restored);
        }
    }

    // The managed host supplies constructor-free component shells and invokes only
    // the managed ownership fields. The Unity runner supplies actual components.
    internal static void VerifyIncidentTransferOwnershipContracts(AbilityMove first, AbilityMove restored)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        Type ownerType = typeof(V20ObservedIncidentResponseRuntime);
        var owner = (V20ObservedIncidentResponseRuntime)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(ownerType);
        var cacheField = ownerType.GetField("startedIncidentTransferOperations", flags);
        var cache = (System.Collections.IDictionary)Activator.CreateInstance(cacheField.FieldType);
        cacheField.SetValue(owner, cache);
        Type attemptType = cacheField.FieldType.GetGenericArguments()[1];
        var take = ownerType.GetMethod("TryTakeStartedTransfer", flags);
        void Remember(string id) => cache[id] = Activator.CreateInstance(attemptType,
            flags, null, new object[] { first, first.MovementOperationVersionForDiagnostics }, null);
        bool Take(string id, AbilityMove movement, out int version)
        {
            object[] args = { id, movement, 0 };
            bool found = (bool)take.Invoke(owner, args);
            version = (int)args[2];
            return found;
        }
        void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("C16: " + message);
        }

        int initialVersion = first.MovementOperationVersionForDiagnostics;
        Require(initialVersion == restored.MovementOperationVersionForDiagnostics,
            "fixture must reproduce equal versions on different movers");
        Remember("transfer:same");
        Require(Take("transfer:same", first, out int version) && version == initialVersion
            && !Take("transfer:same", first, out _), "exact attempt must be consumed once");
        Remember("transfer:restored");
        Require(!Take("transfer:restored", restored, out _) && cache.Count == 0,
            "restored mover must not inherit the previous actor's started-here decision");

        Remember("transfer:preempted");
        var ownership = (CharacterMovementOperationRuntime)typeof(AbilityMove)
            .GetField("movementOwnership", flags).GetValue(first);
        ownership.InvalidateMovementOperation();
        Require(Take("transfer:preempted", first, out version) && version == initialVersion
            && first.MovementOperationVersionForDiagnostics != version,
            "same mover must preserve its old version so the caller detects preemption");

        Remember("transfer:kept");
        Remember("transfer:abandoned");
        ownerType.GetMethod("ForgetAbandonedTransferAttempts", flags).Invoke(owner,
            new object[] { new[] { new V20ActiveEventSaveData { observedIncidentResponse =
                new ObservedIncidentResponseSaveData { externalOperationId = "transfer:kept" } } } });
        Require(cache.Count == 1 && cache.Contains("transfer:kept"),
            "disappeared responses must release their mover references");

        var adapter = (V20CampaignApplicationAdapter)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(V20CampaignApplicationAdapter));
        typeof(V20CampaignApplicationAdapter).GetField("incidentResponseRuntime", flags).SetValue(adapter, owner);
        ownerType.GetField("advancingIncidentResponses", flags).SetValue(owner, true);
        ((IDungeonSaveRestoreCompletedHook)adapter).OnRestoreCompleted();
        Require(cache.Count == 0 && (bool)ownerType.GetField("advancingIncidentResponses", flags).GetValue(owner),
            "restore must clear attempts without reopening the active tick fence");
        adapter.Tick(); // Reentry must return before querying uninitialized collaborators.
        Remember("transfer:dispose");
        adapter.Dispose();
        Require(cache.Count == 0, "adapter disposal must release attempts");

        var destination = new Vector2Int(3, 4);
        var facilityId = new BuildingInstanceId("building:c16");
        var response = new ObservedIncidentResponseSaveData
            { operationId = "response:c16", externalOperationId = "transfer:c16" };
        var movements = new SocietyResponseMovementProvenanceRuntime();
        ownerType.GetField("responseMovements", flags).SetValue(owner, movements);
        movements.GetState(first).RestoreSocietyResponseMovementProvenance(response.operationId,
            response.externalOperationId, facilityId.Value, 3, 4, string.Empty);
        var owns = ownerType.GetMethod("OwnsTransferMovement",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        bool Owns(AbilityMove movement, ObservedIncidentResponseSaveData candidate,
            BuildingInstanceId facility, Vector2Int position) =>
            (bool)owns.Invoke(owner, new object[] { movement, candidate, facility, position });
        Require(Owns(first, response, facilityId, destination), "exact provenance must match");
        Require(!Owns(restored, response, facilityId, destination), "same destination on another mover is insufficient");
        Require(!Owns(first, new ObservedIncidentResponseSaveData
            { operationId = "other", externalOperationId = response.externalOperationId }, facilityId, destination),
            "different response must not own movement");
        Require(!Owns(first, new ObservedIncidentResponseSaveData
            { operationId = response.operationId, externalOperationId = "other" }, facilityId, destination),
            "different external operation must not own movement");
        Require(!Owns(first, response, new BuildingInstanceId("building:other"), destination),
            "different facility must not own movement");
        Require(!Owns(first, response, facilityId, new Vector2Int(4, 4)), "different destination must not own movement");
        Require(!Owns(null, response, facilityId, destination) && !Owns(first, null, facilityId, destination),
            "missing owner or response must not match");
        movements.GetState(first).TryReleaseSocietyResponseMovementProvenance(response.operationId, response.externalOperationId);
        Require(!Owns(first, response, facilityId, destination), "released provenance must not own later movement");
    }

    private static void WriteReport(string path, string content, bool createOnly)
    {
        using var stream = new FileStream(path,
            createOnly ? FileMode.CreateNew : FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static void VerifyConsumedContamination(
        V20StoryContentCatalog catalog)
    {
        using var facts = NewCampaign(catalog);
        V20CampaignRuntime campaign = facts.Campaign;
        ObservedMealIncidentSnapshot source = Evidence(
            "contamination",
            violation: false,
            downed: false,
            contaminated: true);
        ObservedMealIncidentCaptureResult captured =
            ((ISocietyObservedMealIncidentCommand)campaign)
                .CaptureObservedMealIncident(source, Context());
        Require(
            captured.Created
            && captured.IncidentKind == ServiceIncidentKind.Contamination,
            "Exact contaminated consumption did not create Contamination.");
        V20ActiveEventSaveData occurrence =
            campaign.ActiveSocietyEvents.Single();
        ObservedMealIncidentEvidenceSaveData evidence =
            occurrence.observedMealIncident;
        Require(
            occurrence.definitionId == "service-incident:contamination"
            && occurrence.participantCharacterIds.SequenceEqual(
                new[] { source.TargetCharacterId.Value })
            && evidence.operationId == source.OperationId.Value
            && evidence.itemDefinitionId == source.ItemDefinitionId.Value
            && evidence.itemStackId == source.ItemStackId.Value
            && evidence.incidentKind == ServiceIncidentKind.Contamination
            && !evidence.observedPolicyViolation
            && !evidence.observedDowned
            && evidence.observedCause.Contains("실제로 섭취한 식사의 오염")
            && !evidence.observedCause.Contains("객실"),
            "Contamination evidence invented a room source or lost exact meal facts.");

        string saved = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(saved)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == saved,
            "Current Society JSON changed frozen contamination evidence.");
    }

    private static void VerifyCulturalConflictReceipt(
        V20StoryContentCatalog catalog)
    {
        using var facts = NewCampaign(catalog);
        V20CampaignRuntime campaign = facts.Campaign;
        ObservedCulturalConflictIncidentSnapshot source = new(
            "social-conflict-operation:wim040:cultural",
            new CharacterId("character:wim040:cultural:worker"),
            new CharacterId("character:wim040:cultural:customer"),
            new CharacterId("character:wim040:cultural:customer"),
            new SpeciesCultureId("culture:wim040:worker"),
            new SpeciesCultureId("culture:wim040:customer"),
            new BuildingInstanceId("building:wim040:visitor-service"),
            new CoreGridCell(8, 9),
            42);
        ISocietyObservedMealIncidentCommand command = campaign;
        ObservedMealIncidentCaptureResult created =
            command.CaptureObservedCulturalConflictIncident(
                source,
                Context());
        ObservedMealIncidentCaptureResult replay =
            command.CaptureObservedCulturalConflictIncident(
                source,
                Context());
        Require(
            created.Created
            && replay.Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && created.IncidentKind == ServiceIncidentKind.CulturalInsult,
            "Committed cultural-conflict receipt did not create/replay exactly.");
        ObservedMealIncidentEvidenceSaveData evidence = campaign
            .ActiveSocietyEvents.Single().observedMealIncident;
        Require(
            evidence.sourceKind == ObservedIncidentSourceKind.SocialConflict
            && evidence.operationId == source.OperationId
            && evidence.instigatorCharacterId
                == source.InstigatorCharacterId.Value
            && evidence.targetCharacterId
                == source.CustomerCharacterId.Value
            && evidence.otherParticipantCharacterId
                == source.InstigatorCharacterId.Value
            && evidence.instigatorCultureId
                == source.InstigatorCultureId.Value
            && evidence.targetCultureId == source.TargetCultureId.Value
            && evidence.facilityInstanceId == source.FacilityInstanceId.Value,
            "Cultural conflict evidence lost actual identity/culture/facility facts.");
        string saved = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(saved)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == saved,
            "Current Society JSON changed cultural-conflict evidence.");
    }

    private static void VerifyResponseReceiptLifecycle(
        V20StoryContentCatalog catalog)
    {
        VerifyReceiptChoice(
            catalog,
            Evidence(
                "replacement",
                violation: true,
                fieldMeal: false),
            "replace",
            "consumable-operation:wim040-response:",
            replacement: true);
        VerifyReceiptChoice(
            catalog,
            Evidence("emergency-care", violation: false, downed: true),
            "emergency-care",
            "medical-order:wim040:customer",
            replacement: false);
    }

    private static void VerifyReceiptChoice(
        V20StoryContentCatalog catalog,
        ObservedMealIncidentSnapshot source,
        string choiceId,
        string externalOperationId,
        bool replacement)
    {
        using var facts = NewCampaign(catalog);
        V20CampaignRuntime campaign = facts.Campaign;
        ObservedMealIncidentCaptureResult captured =
            ((ISocietyObservedMealIncidentCommand)campaign)
                .CaptureObservedMealIncident(source, Context());
        Require(captured.Created, "Response fixture was not created.");
        bool accepted = campaign.TryResolveSocietyEvent(
            captured.OccurrenceInstanceId,
            choiceId,
            new RunMilestoneEvaluationSnapshot(),
            out V20ResolvedEventResult acceptedResult,
            out string acceptedFailure);
        V20ActiveEventSaveData active = campaign.ActiveSocietyEvents.Single();
        string responseOperationId = active.observedIncidentResponse.operationId;
        string actualExternalOperationId = replacement
            ? responseOperationId
            : externalOperationId;
        Require(
            accepted
            && string.IsNullOrEmpty(acceptedFailure)
            && string.IsNullOrEmpty(acceptedResult.DefinitionId)
            && active.observedIncidentResponse.phase
                == ObservedIncidentResponsePhase.Accepted
            && active.selectedChoiceId == choiceId
            && !active.resolved,
            $"'{choiceId}' did not enter Society-owned pending state.");
        string acceptedJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(acceptedJson)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == acceptedJson,
            $"'{choiceId}' accepted owner changed during current JSON restore.");

        ISocietyObservedIncidentResponseCommand responses = campaign;
        Require(
            responses.TryRecordObservedIncidentResponseStarted(
                active.instanceId,
                responseOperationId,
                actualExternalOperationId,
                out string startFailure)
            && string.IsNullOrEmpty(startFailure),
            $"'{choiceId}' did not preserve its external operation owner.");
        string pendingJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(pendingJson)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == pendingJson,
            $"'{choiceId}' pending owner changed during current JSON restore.");

        string receiptId = replacement
            ? $"physical-meal-consumed:{responseOperationId}:stack:wim040:replacement"
            : $"medical-stabilized:{actualExternalOperationId}";
        Require(
            responses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                responseOperationId,
                actualExternalOperationId,
                receiptId,
                out string receiptFailure)
            && string.IsNullOrEmpty(receiptFailure)
            && responses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                responseOperationId,
                actualExternalOperationId,
                receiptId,
                out _),
            $"'{choiceId}' receipt was not exact-once replayable.");
        string receiptJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(receiptJson)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == receiptJson,
            $"'{choiceId}' receipt owner changed during current JSON restore.");
        bool resolved = campaign.TryResolveSocietyEvent(
            active.instanceId,
            choiceId,
            new RunMilestoneEvaluationSnapshot(),
            out V20ResolvedEventResult terminal,
            out string terminalFailure);
        V20ActiveEventSaveData recent = campaign
            .RecentResolvedSocietyEvents.Single();
        Require(
            resolved
            && string.IsNullOrEmpty(terminalFailure)
            && terminal.DefinitionId.Length > 0
            && recent.observedIncidentResponse.phase
                == ObservedIncidentResponsePhase.EffectsPublished
            && recent.observedIncidentResponse.receiptId == receiptId,
            $"'{choiceId}' did not wait for and freeze its actual receipt.");
    }

    private static void VerifyResponseFailureLifecycle(
        V20StoryContentCatalog catalog)
    {
        VerifyResponseFailure(
            catalog,
            Evidence("failed", violation: true, fieldMeal: false),
            "replace",
            cancelled: false,
            "replacement-meal-operation-failed:PhysicalConsumptionFailed");
        VerifyResponseFailure(
            catalog,
            Evidence("cancelled", violation: false, downed: true),
            "emergency-care",
            cancelled: true,
            "emergency-care-order-cancelled");
    }

    private static void VerifyResponseFailure(
        V20StoryContentCatalog catalog,
        ObservedMealIncidentSnapshot source,
        string choiceId,
        bool cancelled,
        string reason)
    {
        using var facts = NewCampaign(catalog);
        V20CampaignRuntime campaign = facts.Campaign;
        ObservedMealIncidentCaptureResult captured =
            ((ISocietyObservedMealIncidentCommand)campaign)
                .CaptureObservedMealIncident(source, Context());
        Require(captured.Created, "Response terminal fixture was not created.");
        Require(
            campaign.TryResolveSocietyEvent(
                captured.OccurrenceInstanceId,
                choiceId,
                new RunMilestoneEvaluationSnapshot(),
                out V20ResolvedEventResult accepted,
                out string acceptedFailure)
            && string.IsNullOrEmpty(accepted.DefinitionId)
            && string.IsNullOrEmpty(acceptedFailure),
            $"'{choiceId}' response terminal fixture was not accepted.");

        V20ActiveEventSaveData active = campaign.ActiveSocietyEvents.Single();
        ObservedIncidentResponseSaveData response = active.observedIncidentResponse;
        ISocietyObservedIncidentResponseCommand responses = campaign;
        Require(
            responses.TryRecordObservedIncidentResponseFailure(
                active.instanceId,
                response.operationId,
                reason,
                cancelled,
                out string terminalFailure)
            && string.IsNullOrEmpty(terminalFailure)
            && responses.TryRecordObservedIncidentResponseFailure(
                active.instanceId,
                response.operationId,
                reason,
                cancelled,
                out _)
            && !responses.TryRecordObservedIncidentResponseFailure(
                active.instanceId,
                response.operationId,
                reason + ":conflict",
                cancelled,
                out _),
            $"'{choiceId}' response terminal state was not exact-once.");
        string terminalJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(terminalJson)));
        ObservedIncidentResponsePhase expected = cancelled
            ? ObservedIncidentResponsePhase.Cancelled
            : ObservedIncidentResponsePhase.Failed;
        V20ActiveEventSaveData terminal = campaign
            .RecentResolvedSocietyEvents.Single();
        Require(
            campaign.ActiveSocietyEvents.Count == 0
            && campaign.GetOwnedCustomerIds().Count == 0
            && terminal.resolved
            && terminal.observedIncidentResponse.phase == expected
            && terminal.observedIncidentResponse.failureReason == reason
            && terminal.terminalEffectKinds.Count == 0,
            $"'{choiceId}' response failure did not release its active owner or preserve an effect-free terminal record.");
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == terminalJson,
            $"'{choiceId}' response terminal history changed during current JSON restore.");
    }

    private static void VerifyTransferReceiptLifecycle(
        V20StoryContentCatalog catalog)
    {
        using var facts = NewCampaign(catalog);
        V20CampaignRuntime campaign = facts.Campaign;
        ObservedMealIncidentCaptureResult captured =
            ((ISocietyObservedMealIncidentCommand)campaign)
            .CaptureObservedMealIncident(
                Evidence("transfer", violation: false, downed: true),
                Context());
        Require(captured.Created, "Transfer fixture was not created.");
        Require(campaign.TryResolveSocietyEvent(
            captured.OccurrenceInstanceId,
            "transfer",
            new RunMilestoneEvaluationSnapshot(),
            out V20ResolvedEventResult accepted,
            out string acceptedFailure)
            && string.IsNullOrEmpty(accepted.DefinitionId)
            && string.IsNullOrEmpty(acceptedFailure),
            "Transfer did not enter Society-owned pending state.");
        V20ActiveEventSaveData active = campaign.ActiveSocietyEvents.Single();
        ObservedIncidentResponseSaveData response = active.observedIncidentResponse;
        BuildingInstanceId facilityId = new("building:wim040:isolation");
        string externalOperationId = response.operationId
            + ":destination:12:7:"
            + facilityId.Value;
        string receiptId = "character-arrived:"
            + externalOperationId
            + ":"
            + active.observedMealIncident.targetCharacterId;
        ISocietyObservedIncidentResponseCommand responses = campaign;
        Require(
            responses.TryRecordObservedIncidentResponseStarted(
                active.instanceId,
                response.operationId,
                externalOperationId,
                out string startFailure)
            && string.IsNullOrEmpty(startFailure),
            "Transfer did not freeze its exact facility/destination operation.");
        string pendingJson = JsonUtility.ToJson(campaign.CaptureSociety());
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(pendingJson)));
        Require(
            JsonUtility.ToJson(campaign.CaptureSociety()) == pendingJson,
            "Transfer pending destination changed during current JSON restore.");
        Require(
            responses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                externalOperationId,
                receiptId,
                out string receiptFailure)
            && string.IsNullOrEmpty(receiptFailure)
            && responses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                externalOperationId,
                receiptId,
                out _),
            "Transfer arrival receipt was not exact-once replayable.");
        Require(campaign.TryResolveSocietyEvent(
                active.instanceId,
                "transfer",
                new RunMilestoneEvaluationSnapshot(),
                out V20ResolvedEventResult terminal,
                out string terminalFailure)
            && string.IsNullOrEmpty(terminalFailure)
            && !string.IsNullOrEmpty(terminal.DefinitionId),
            "Transfer published authored effects before/without its arrival receipt.");
        V20ActiveEventSaveData resolved = campaign
            .RecentResolvedSocietyEvents.Single();
        Require(resolved.observedIncidentResponse.phase
                == ObservedIncidentResponsePhase.EffectsPublished
            && string.Equals(
                resolved.observedIncidentResponse.externalOperationId,
                externalOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                resolved.observedIncidentResponse.receiptId,
                receiptId,
                StringComparison.Ordinal),
            "Transfer terminal history lost its destination/arrival identity.");
    }

    private static void VerifyLifeEventOccurrencePolicy(
        V20StoryContentCatalog catalog)
    {
        LifeEventDefinitionSO[] definitions = catalog.LifeEvents
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        LifeEventDefinitionSO[] daily = definitions
            .Where(value => value.occurrencePolicy
                == LifeEventOccurrencePolicy.DailyCadence)
            .ToArray();
        LifeEventDefinitionSO[] observedOnly = definitions
            .Where(value => value.occurrencePolicy
                == LifeEventOccurrencePolicy.ExternalObservedOnly)
            .ToArray();
        Require(
            definitions.Length == 32
            && daily.Length == 1
            && daily[0].StableId == "life-event:retirement-request"
            && observedOnly.Length == 31,
            "Life-event occurrence policy did not fail closed to one authored daily candidate and 31 observed-only definitions.");
    }

    private static void VerifyObservedLifeEventReceiptLifecycle(
        V20StoryContentCatalog catalog)
    {
        Wim040ObservedLifeEventReceiptDebugFixture.Run(catalog);
    }

    private static void VerifyProductionLossProducerPublication()
    {
        RecordingObservedCareerLifeEvents observed = new();
        ProductionRecipeExecutionReceiptAuthority authority = new(observed);
        ProductionBillId billId = new(
            "production-bill:wim040:career-loss");
        BuildingInstanceId facilityId = new(
            "building:wim040:career-production");
        ProductionPreparedOutputBatchSaveData completed =
            Wim040ObservedLifeEventReceiptDebugFixture
                .CreateCompletedLossBatch();

        Require(authority.TryPublishCompleted(
                billId,
                1,
                "recipe:wim040:career-loss",
                facilityId,
                "production-input:wim040:career-loss",
                1,
                1_050L,
                completed,
                out string compatibilityFailure),
            compatibilityFailure);
        Require(observed.ProductionLossCalls == 0,
            "The non-actor compatibility publication invented a production-loss actor.");

        const string StudentId = "character:wim040:career:student";
        Require(!authority.TryPublishCompleted(
                billId, 1, "recipe:wim040:career-loss", facilityId, StudentId,
                "production-input:wim040:career-loss", 1, 1_050L, completed,
                out string missingParentFailure)
                && missingParentFailure == "production-declared-loss-parent-fact-missing"
                && observed.ProductionLossCalls == 0,
            "Actor-bound declared loss bypassed its mandatory production parent fact.");

        var root = new DungeonRuntimeAggregateRootStore();
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new ProductionCompletedOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new ProductionCompletedOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId("run:wim040:completion-parent"), 1);
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new DungeonStory.Foundation.GameEventBus());
        using var completionFacts = new DomainFactDispatcher();
        completionFacts.Subscribe("production.completion-observer",
            new GameplayDomainFactObserver<ProductionCycleCompletedFact, ProductionCompletedOutcomeReceipt>(
                ProductionCompletionFactObservation.Capture, registry, ledger, recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        var owner = ProductionBillRecord.Create(billId, "recipe:wim040:career-loss", facilityId,
            ProductionOrderMode.RepeatCount, 1, 0, ProductionBatchStage.None,
            "production-material:" + billId.Value);
        new ProductionAggregateStateSession(root).AddBill(owner);
        var observations = new ProductionCompletionObservationRuntime(completionFacts, completionFacts);
        var parentFact = Wim040ObservedLifeEventReceiptDebugFixture.CreateCompletedLossFact(StudentId);
        Require(observations.TryPrepare(owner, () => parentFact, out var stage, out string parentFailure), parentFailure);
        try { Require(observations.Commit(owner, stage).Completed, "Production parent fact was not committed."); }
        finally { stage.Cancel(); }
        authority = new ProductionRecipeExecutionReceiptAuthority(observed, completionFacts, completionFacts,
            new ProductionAggregateStateStore(root));
        Require(authority.TryPublishCompleted(
                billId,
                1,
                "recipe:wim040:career-loss",
                facilityId,
                StudentId,
                "production-input:wim040:career-loss",
                1,
                1_050L,
                completed,
                out string actorFailure),
            actorFailure);
        ProductionDeclaredLossCycleReceipt source = observed.LastProductionLoss;
        Require(observed.ProductionLossCalls == 1
            && source != null
            && source.BillId.Equals(billId)
            && source.CycleSequence == 1
            && source.RecipeId == "recipe:wim040:career-loss"
            && source.FacilityId.Equals(facilityId)
            && source.WorkerPersistentId == StudentId
            && source.BatchCommitId == completed.batchCommitId
            && source.OutcomeFingerprint == completed.outcomeFingerprint
            && source.DeclaredLossMassGrams == 50L
            && source.SourceOperationId
                == "production-declared-loss:" + completed.batchCommitId,
            "Actor production completion did not publish its exact declared-loss source fact.");
    }

    private static void VerifyLineageCompressionProducer(
        V20StoryContentCatalog catalog)
    {
        DungeonRuntimeAggregateRootStore root = new();
        V20CampaignRuntime campaign = new(root, catalog);
        MigratedProducerOutcomeEditorFixture outcomeFixture = new(
            "wim040-lineage-compression",
            root);
        using var socialFacts = new SocialRitualFactEditorFixture(outcomeFixture);
        DungeonStory.Foundation.GameEventBus events = new();
        EventAlertRequest alert = null;
        V20ContentEffectsResolvedEvent effectEvent = default;
        int alertCount = 0;
        int effectEventCount = 0;
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                alertCount++;
                alert = value.request;
            });
        using IDisposable effectSubscription =
            events.Subscribe<V20ContentEffectsResolvedEvent>(value =>
            {
                effectEventCount++;
                effectEvent = value;
            });
        using var presentation = new SeasonalSocialPresentationObserver(events);
        presentation.Initialize();
        KinshipHouseholdRuntime social = new(
            root,
            campaign,
            events,
            socialFacts.Journal);
        CharacterId archivedId =
            new("character:wim040:actual-lineage-archive");
        social.ArchiveDeath(
            archivedId,
            new CharacterSpeciesId("species:human"),
            birthAbsoluteDay: 1,
            deathAbsoluteDay: 10,
            famous: false,
            new HouseholdId("household:wim040:actual-lineage-archive"),
            generation: 2);

        social.ArchiveColdData(
            currentAbsoluteDay: 200,
            Array.Empty<CharacterId>());

        KinshipHouseholdWorldSaveData socialState = social.Capture();
        SocietyEventWorldSaveData society = campaign.CaptureSociety();
        RunMilestoneWorldSaveData milestones = campaign.CaptureMilestones();
        LineageSummarySaveData committedSummary =
            socialState.kinship.lineageSummaries.Single();
        Require(
            socialState.kinship.tombstones.All(value => value == null
                || value.characterId != archivedId.Value)
            && committedSummary.householdId
                == "household:wim040:actual-lineage-archive"
            && committedSummary.generation == 2
            && committedSummary.archivedCharacterCount == 1
            && committedSummary.earliestBirthDay == 1
            && committedSummary.latestDeathDay == 10,
            "Actual lineage archive producer did not remove the cold tombstone and retain its summary.");
        ObservedLifeEventReceiptSaveData committedReceipt =
            society.successfulLifeEventOperations.Single();
        V20ActiveEventSaveData committedOccurrence =
            society.recentResolvedEvents.Single();
        Require(
            committedReceipt.sourceKind
                == ObservedLifeEventSourceKind.LineageCompression
            && committedReceipt.sourceOperationId
                == "kinship-lineage:" + archivedId.Value
            && committedReceipt.occurrenceInstanceId
                == committedOccurrence.instanceId
            && committedOccurrence.definitionId
                == "life-event:story-compressed"
            && committedOccurrence.participantCharacterIds.SequenceEqual(
                new[] { archivedId.Value }),
            "Actual lineage archive producer did not commit exactly one story-compressed Society receipt.");
        Require(
            milestones.worldFlags.Contains(
                "life-event:story-compressed",
                StringComparer.Ordinal),
            "Actual lineage archive producer did not commit the authored WorldFlag.");
        Require(
            alertCount == 1
            && alert != null
            && alert.IsResolved
            && alert.SourceId == society.recentResolvedEvents[0].instanceId
            && effectEventCount == 1
            && effectEvent.DefinitionId == "life-event:story-compressed"
            && effectEvent.PhysicalEffectsApplied,
            "Actual lineage archive producer did not publish exactly one resolved alert/effect event.");

        string socialJson = JsonUtility.ToJson(socialState);
        string societyJson = JsonUtility.ToJson(society);
        string milestoneJson = JsonUtility.ToJson(milestones);
        social.PublishRestore(social.PrepareRestore(
            JsonUtility.FromJson<KinshipHouseholdWorldSaveData>(socialJson)));
        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(societyJson)));
        campaign.PublishMilestones(campaign.PrepareMilestones(
            JsonUtility.FromJson<RunMilestoneWorldSaveData>(milestoneJson)));
        Require(
            JsonUtility.ToJson(social.Capture()) == socialJson
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyJson
            && JsonUtility.ToJson(campaign.CaptureMilestones()) == milestoneJson,
            "Actual lineage archive producer state changed during current aggregate JSON restore.");
        social.ArchiveColdData(
            currentAbsoluteDay: 201,
            Array.Empty<CharacterId>());
        Require(
            alertCount == 1
            && effectEventCount == 1
            && JsonUtility.ToJson(social.Capture()) == socialJson
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyJson
            && JsonUtility.ToJson(campaign.CaptureMilestones()) == milestoneJson,
            "Re-running the actual lineage archive producer duplicated its event, effect, or alert.");
    }

    private static Wim040ObservedIncidentDebugScenarios.ObservedIncidentFactFixture NewCampaign(
        V20StoryContentCatalog catalog) =>
        new(catalog, "wim040-completion");

    private static V20DailyEventContext Context() => new()
    {
        AbsoluteDay = 42,
        RunSeed = 157181,
        Season = Season.Spring
    };

    private static ObservedMealIncidentSnapshot Evidence(
        string suffix,
        bool violation,
        bool downed = false,
        bool contaminated = false,
        bool fieldMeal = true) => new(
        new ConsumableOperationId(
            "consumable-operation:wim040:completion:" + suffix),
        new CharacterId("character:wim040:completion:customer"),
        new ItemDefinitionId("food:lavish-meat"),
        new ItemStackId("stack:wim040:completion:meal"),
        fieldMeal
            ? default
            : new BuildingInstanceId("building:wim040:meal-service"),
        fieldMeal,
        new CoreGridCell(3, 4),
        absoluteDay: 42,
        observedPolicyViolation: violation,
        observedDowned: downed,
        observedDead: false,
        observedContaminated: contaminated);

    private static void Require(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static void MustReject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }
        throw new InvalidOperationException(reason);
    }

    private sealed class RecordingObservedCareerLifeEvents :
        IObservedCareerLifeEventCommand
    {
        public int ProductionLossCalls { get; private set; }
        public ProductionDeclaredLossCycleReceipt LastProductionLoss
            { get; private set; }

        public bool TryCaptureProductionDeclaredLoss(
            ProductionDeclaredLossCycleReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            ProductionLossCalls++;
            LastProductionLoss = source;
            stateChanged = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureLastLesson(
            CareerMentorshipSnapshot mentorship,
            CharacterCareerSnapshot retirement,
            CombatEquipmentInstance protectiveEquipment,
            CareerMentorshipAwardCommitReceipt award,
            int absoluteDay,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureQuietPromotion(
            CharacterProficiencyAwardCommitReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = string.Empty;
            return true;
        }
    }
}
#endif
