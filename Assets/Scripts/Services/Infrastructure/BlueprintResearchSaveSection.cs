using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed class BlueprintResearchRestoreCandidate
{
    internal BlueprintResearchRestoreCandidate(
        BlueprintResearchState research,
        KnowledgeResidueRestoreCandidate knowledge)
    {
        Research = research ?? throw new ArgumentNullException(nameof(research));
        Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
    }

    internal BlueprintResearchState Research { get; }
    internal KnowledgeResidueRestoreCandidate Knowledge { get; }
}

public sealed class BlueprintResearchSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonResearchSaveData,
        BlueprintResearchRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "research.blueprints";
    private const int CurrentVersion = 8;

    private static readonly string[] Dependencies =
    {
        WorkOrdersSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly BlueprintResearchRuntime runtime;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;
    private readonly IResearchProjectCatalog projectCatalog;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IFacilityBufferDestinationLifecycleCommand archiveDestinations;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IKnowledgeResidueDestinationRuntime knowledgeDestinations;

    public BlueprintResearchSaveSection(
        ProgressionSceneRuntimeReferences runtimeReferences,
        IFacilityShopCatalog facilityCatalog,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing,
        IResearchProjectCatalog projectCatalog,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IFacilityBufferDestinationLifecycleCommand archiveDestinations,
        IPhysicalItemMassQuery physicalMass,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IKnowledgeResidueDestinationRuntime knowledgeDestinations)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(BlueprintResearchSaveSection)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
        this.projectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.archiveDestinations = archiveDestinations
            ?? throw new ArgumentNullException(nameof(archiveDestinations));
        this.physicalMass = physicalMass
            ?? throw new ArgumentNullException(nameof(physicalMass));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.knowledgeDestinations = knowledgeDestinations
            ?? throw new ArgumentNullException(nameof(knowledgeDestinations));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override void ValidateRawPayload(string payloadJson) => ValidatePublicationJson(payloadJson);

    internal static void ValidatePublicationJson(string payloadJson)
    {
        JObject root = JObject.Parse(payloadJson);
        ValidateKnowledgePublicationJson(root);
        if (root["completedProjectResults"] is not JArray results
            || root["pendingWorkPublication"] is not JObject pending
            || pending["active"]?.Type != JTokenType.Boolean)
            throw new InvalidOperationException("Research V8 publication fields are missing or malformed.");
        foreach (JToken row in results)
            if (row is not JObject value || value["projectId"]?.Type != JTokenType.String
                || value["publicationKey"]?.Type != JTokenType.String)
                throw new InvalidOperationException("Research V8 completion reference is malformed.");
        if (pending["publicationKey"]?.Type != JTokenType.String)
            throw new InvalidOperationException("Research publication reference is missing.");
        if (!(bool)pending["active"])
        {
            if (!string.IsNullOrEmpty((string)pending["publicationKey"]))
                throw new InvalidOperationException("Inactive research publication retains an unacknowledged reference.");
            return;
        }
        if (pending["fact"] is not JObject fact || fact["hasEquipment"]?.Type != JTokenType.Boolean
            || fact["unlocks"] is not JArray unlocks)
            throw new InvalidOperationException("Pending research fact is missing.");
        RequireFields(fact, typeof(ResearchWorkPublicationData), "equipment");
        foreach (var unlock in unlocks)
        {
            if (unlock is not JObject value) throw new InvalidOperationException("Pending research unlock is missing.");
            RequireFields(value, typeof(ResearchUnlockFactData));
        }
        if ((bool)fact["hasEquipment"])
        {
            if (fact["equipment"] is not JObject equipment)
                throw new InvalidOperationException("Pending research equipment is missing.");
            RequireFields(equipment, typeof(ResearchEquipmentUseData));
        }
    }

    private static void ValidateKnowledgePublicationJson(JObject root)
    {
        if (root["knowledgeTasks"] is not JArray tasks)
            throw new InvalidOperationException("Research V8 knowledge tasks are missing.");
        foreach (JToken row in tasks)
        {
            if (row is not JObject task || task["completionPublicationKey"]?.Type != JTokenType.String
                || task["completionOwnerRevision"]?.Type != JTokenType.Integer
                || task["dispositionPhase"]?.Type != JTokenType.Integer
                || task["completionFact"] is not JObject fact
                || fact["experience"] is not JObject experience || fact["physical"] is not JObject physical
                || physical["sourceFacts"] is not JArray sources || physical["sourceStackIds"] is not JArray)
                throw new InvalidOperationException("Knowledge publication fields are missing or malformed.");
            RequireFields(experience, typeof(KnowledgeCompletionExperienceData));
            RequireFields(physical, typeof(PhysicalItemBatchDispositionSaveData));
            if ((int)task["dispositionPhase"] == (int)KnowledgeResidueDispositionPhase.AwaitingInput)
            {
                if ((long)task["completionOwnerRevision"] != 0
                    || (string)task["completionPublicationKey"] != string.Empty
                    || fact.ToObject<KnowledgeCompletionFactData>()?.IsEmpty != true)
                    throw new InvalidOperationException("Unapplied knowledge task retains a completion fact.");
                continue;
            }
            if ((int)task["dispositionPhase"] != (int)KnowledgeResidueDispositionPhase.RewardsApplied)
                throw new InvalidOperationException("Applied knowledge task has no complete frozen fact.");
            foreach (JToken source in sources)
            {
                if (source is not JObject value) throw new InvalidOperationException("Knowledge input source is missing.");
                RequireFields(value, typeof(PhysicalItemDispositionSourceFactSaveData));
            }
        }
    }

    private static void RequireFields(JObject value, Type type, string optional = null)
    {
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            if (field.Name != optional && (value[field.Name] == null || value[field.Name].Type == JTokenType.Null))
                throw new InvalidOperationException("Pending research fact field is missing: " + field.Name);
    }

    protected override DungeonResearchSaveData CapturePayload()
    {
        BlueprintResearchState state = runtime.State;
        bool usesProjectAuthority = projectCatalog.Projects.Count > 0;
        return new DungeonResearchSaveData
        {
            outcomeSequence = state.OutcomeSequence,
            pendingWorkPublication = state.PendingPublication ?? new(),
            nextKnowledgeTaskSequence = knowledgeProcessing.NextTaskSequence,
            knowledgeTaskIdentityGeneration =
                knowledgeProcessing.TaskIdentityGeneration,
            tasks = usesProjectAuthority
                ? new List<DungeonResearchTaskSaveData>()
                : state.Tasks
                    .Where(task => task?.Blueprint != null)
                    .Select(task => new DungeonResearchTaskSaveData
                    {
                        blueprintId = task.Blueprint.id,
                        progress = task.Progress
                    })
                    .ToList(),
            completedBlueprintIds = usesProjectAuthority
                ? new List<int>()
                : state.CompletedBlueprintIds.OrderBy(id => id).ToList(),
            unlockedBuildingIds = state.UnlockedBuildingIds
                .OrderBy(id => id)
                .ToList(),
            unlockedRecipeIds = state.UnlockedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            knowledgeTasks = knowledgeProcessing.Capture().ToList(),
            projectProgress = state.Projects.ProgressById.Values
                .OrderBy(progress => progress.ProjectId.Value, StringComparer.Ordinal)
                .Select(progress =>
                {
                    ResearchProjectSO project = projectCatalog.TryGet(
                            progress.ProjectId,
                            out ResearchProjectSO found)
                        ? found
                        : throw new InvalidOperationException(
                            $"Research progress references missing project '{progress.ProjectId.Value}'.");
                    return new DungeonResearchProjectProgressSaveData
                    {
                        projectId = progress.ProjectId.Value,
                        progress = progress.Progress,
                        requiredWorkAtCapture = project.RequiredWork
                    };
                })
                .ToList(),
            completedProjectIds = state.Projects.CompletedProjectIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            completedProjectResults = state.CompletedProjectResultKeys
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new DungeonResearchCompletionResultSaveData
                {
                    projectId = pair.Key,
                    publicationKey = pair.Value
                })
                .ToList(),
            projectQueue = state.Projects.Queue
                .Select(entry => new DungeonResearchQueueEntrySaveData
                {
                    projectId = entry.ProjectId.Value,
                    suspendedReason = entry.SuspendedReason
                })
                .ToList(),
            activeProjectId = state.Projects.ActiveProjectId.Value,
            materializeLegacyBlueprintItems = false,
            shopFulfillments = state.CaptureShopFulfillments()
                .Select(value => new FacilityShopFulfillmentSaveData
                {
                    purchaseResultKey = value.PurchaseResultKey,
                    offerTypeId = value.OfferTypeId,
                    offerDataId = value.OfferDataId,
                    itemDefinitionId = value.ItemDefinitionId,
                    itemDisplayName = value.ItemDisplayName,
                    phase = value.Phase,
                    stackId = value.StackId,
                    itemInstanceId = value.ItemInstanceId
                })
                .ToList()
        };
    }

    protected override BlueprintResearchRestoreCandidate BuildRestoreCandidate(
        DungeonResearchSaveData source)
    {
        BlueprintResearchState restored =
            ValidateAndBuildResearchState(source);
        ReconcileKnowledgeResiduePhysicalCandidate(
            source.knowledgeTasks,
            physicalCandidates);
        KnowledgeResidueRestoreCandidate knowledge =
            knowledgeProcessing.PrepareRestore(
                source.knowledgeTasks,
                source.nextKnowledgeTaskSequence,
                source.knowledgeTaskIdentityGeneration);
        return new BlueprintResearchRestoreCandidate(restored, knowledge);
    }

    protected override void ValidateParsedPayload(
        DungeonResearchSaveData source)
    {
        _ = ValidateAndBuildResearchState(source);
        _ = knowledgeProcessing.PrepareRestore(
            source.knowledgeTasks,
            source.nextKnowledgeTaskSequence,
            source.knowledgeTaskIdentityGeneration);
    }

    protected override void NormalizeRestorePayload(DungeonResearchSaveData payload, DungeonGameRestoreReport report)
    {
        if (payload.nextKnowledgeTaskSequence == 0)
        {
            payload.knowledgeTaskIdentityGeneration =
                KnowledgeResidueTaskIdentity.MigratedGeneration;
            payload.nextKnowledgeTaskSequence = 1;
            report?.AddWarning(
                "KnowledgeTaskIdentityHistoryMissing: legacy allocation history is unavailable; "
                + "future tasks were moved to the disjoint generation-2 namespace without guessing old IDs.");
        }
        else if (payload.knowledgeTaskIdentityGeneration == 0)
        {
            payload.knowledgeTaskIdentityGeneration =
                KnowledgeResidueTaskIdentity.OriginalGeneration;
        }
        payload.shopFulfillments ??= new List<FacilityShopFulfillmentSaveData>();
        if (payload.knowledgeTasks?.Any(task => task != null
                && task.dispositionPhase != KnowledgeResidueDispositionPhase.AwaitingInput
                && task.completionOwnerRevision == 0) == true)
            report?.AddWarning("KnowledgeLegacyCompletionPending: split-commit reward/input state requires reconciliation; "
                + "the runtime will not guess success or reapply a reward.");
    }

    private BlueprintResearchState ValidateAndBuildResearchState(
        DungeonResearchSaveData source)
    {
        RequireCollections(source);
        if (source.materializeLegacyBlueprintItems)
        {
            throw new InvalidOperationException(
                "Research V7 cannot materialize legacy blueprint items.");
        }

        Dictionary<int, FacilityBlueprintSO> blueprints = BuildBlueprintIndex();
        Dictionary<string, ResearchProjectSO> projects = BuildProjectIndex();
        HashSet<int> buildings = facilityCatalog.Buildings
            .Where(building => building != null)
            .Select(building => building.id)
            .ToHashSet();
        HashSet<string> recipes = CollectResearchRecipeIds(blueprints, projects);

        BlueprintResearchState restored = new BlueprintResearchState();
        restored.RestoreOutcomeSequence(source.outcomeSequence);
        RestoreBlueprintState(source, restored, blueprints, buildings, recipes);
        RestoreShopFulfillments(source.shopFulfillments, restored);
        RestoreProjectState(source, restored, projects);
        restored.RestorePendingPublication(source.pendingWorkPublication?.active == true ? source.pendingWorkPublication : null);
        return restored;
    }

    private static void RestoreShopFulfillments(
        IReadOnlyList<FacilityShopFulfillmentSaveData> saved,
        BlueprintResearchState restored)
    {
        foreach (FacilityShopFulfillmentSaveData row in saved
                     ?? Array.Empty<FacilityShopFulfillmentSaveData>())
        {
            if (row == null
                || string.IsNullOrWhiteSpace(row.purchaseResultKey)
                || string.IsNullOrWhiteSpace(row.offerTypeId)
                || row.offerDataId < 0
                || string.IsNullOrWhiteSpace(row.itemDefinitionId)
                || !Enum.IsDefined(typeof(FacilityShopFulfillmentPhase), row.phase)
                || (row.phase == FacilityShopFulfillmentPhase.Pending
                    && (!string.IsNullOrEmpty(row.stackId)
                        || !string.IsNullOrEmpty(row.itemInstanceId)))
                || (row.phase == FacilityShopFulfillmentPhase.Spawned
                    && (string.IsNullOrWhiteSpace(row.stackId)
                        || !string.IsNullOrEmpty(row.itemInstanceId)))
                || (row.phase == FacilityShopFulfillmentPhase.Delivered
                    && (string.IsNullOrWhiteSpace(row.stackId)
                        || string.IsNullOrWhiteSpace(row.itemInstanceId))))
                throw new InvalidOperationException(
                    "Research shop fulfillment payload is invalid.");
            restored.RestoreShopFulfillment(new FacilityShopFulfillmentRecord
            {
                PurchaseResultKey = row.purchaseResultKey,
                OfferTypeId = row.offerTypeId,
                OfferDataId = row.offerDataId,
                ItemDefinitionId = row.itemDefinitionId,
                ItemDisplayName = row.itemDisplayName ?? string.Empty,
                Phase = row.phase,
                StackId = row.stackId ?? string.Empty,
                ItemInstanceId = row.itemInstanceId ?? string.Empty
            });
        }
    }

    protected override void PublishRestoreCandidate(
        BlueprintResearchRestoreCandidate candidate)
    {
        BlueprintResearchRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (!restoreWorldCandidates.TryGetGrid(out Grid candidateGrid)
            || !restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings))
        {
            throw new InvalidOperationException(
                "Research restore requires the detached facility-world candidate before destination publication.");
        }

        RoomLayout candidateRooms = RoomDetector.Build(candidateGrid);
        FacilityBufferDestinationClaim[] archiveClaims =
            ResearchBlueprintArchiveDestinationAuthority.BuildClaims(
                candidateBuildings.Where(building =>
                    building != null
                    && building.IsDetachedRestoreCandidate
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsAuthoredArchiveFacility(building)
                    && candidateRooms.TryGetRoom(
                        building,
                        out RoomInstance room)
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsEligibleRoom(room)));
        FacilityBufferCapacityProfile[] archiveProfiles =
            ResearchBlueprintArchiveDestinationAuthority.BuildProfiles(
                candidateBuildings.Where(building =>
                    building != null
                    && building.IsDetachedRestoreCandidate
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsAuthoredArchiveFacility(building)
                    && candidateRooms.TryGetRoom(
                        building,
                        out RoomInstance room)
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsEligibleRoom(room)),
                facilityCatalog.Blueprints,
                physicalMass);
        if (!knowledgeDestinations.TryReplace(
                required.Knowledge.CaptureTasks(),
                candidateBuildings,
                out string knowledgeFailure))
        {
            throw new InvalidOperationException(
                "Knowledge residue destination restore failed: "
                + knowledgeFailure);
        }
        if (!archiveDestinations.TryReplaceOwnedAuthorities(
                ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                archiveClaims,
                archiveProfiles,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Research archive destination restore failed: "
                + failureReason);
        }

        runtime.ReplaceStateFromRestore(required.Research);
        knowledgeProcessing.Restore(required.Knowledge);
    }

    public static void ReconcileKnowledgeResiduePhysicalCandidate(
        IReadOnlyList<KnowledgeResidueTaskSaveData> tasks,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (query == null || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Research restore requires the incoming physical-item candidate.");
        }

        Dictionary<string, KnowledgeResidueTaskSaveData> owners =
            (tasks ?? Array.Empty<KnowledgeResidueTaskSaveData>())
            .Where(task => task != null)
            .ToDictionary(
                task => task.sinkOperationId,
                StringComparer.Ordinal);
        foreach (KnowledgeResidueTaskSaveData task in owners.Values
                     .OrderBy(value => value.sinkOperationId,
                         StringComparer.Ordinal))
        {
            bool hasReceipt = query.TryGetPendingBatchDisposition(
                task.sinkOperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot receipt);
            if (task.dispositionPhase ==
                KnowledgeResidueDispositionPhase.AwaitingInput)
            {
                if (hasReceipt || task.completionOwnerRevision != 0 || task.completionFact?.IsEmpty != true
                    || task.completionPublicationKey != string.Empty)
                    throw new InvalidOperationException("Unapplied knowledge owner has a pending debit or reward fact: " + task.taskId);
                continue;
            }

            if (!hasReceipt)
            {
                throw new InvalidOperationException(
                    "Knowledge residue task has no exact incoming Sink receipt: "
                    + task.sinkOperationId);
            }
            RequireKnowledgeResidueReceipt(task, receipt, true);
            KnowledgeResidueCompletionTransaction.RestoreFact(task);
            if (!receipt.PublicationExpected || receipt.PublicationKey != task.completionPublicationKey
                || receipt.OwnerRevision != task.completionOwnerRevision)
                throw new InvalidOperationException("Knowledge completion publication reference mismatch: " + task.taskId);
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt == null
                || !receipt.OperationId.StartsWith(
                    "research-knowledge-residue-sink:",
                    StringComparison.Ordinal)
                    && !string.Equals(
                        receipt.ReasonCode,
                        KnowledgeResidueDestinationAuthority.SinkReasonCode,
                        StringComparison.Ordinal))
            {
                continue;
            }
            if (!owners.TryGetValue(receipt.OperationId, out var owner))
            {
                throw new InvalidOperationException(
                    "Incoming knowledge residue Sink has no exact research owner: "
                    + receipt.OperationId);
            }
            RequireKnowledgeResidueReceipt(
                owner,
                receipt,
                owner.dispositionPhase !=
                    KnowledgeResidueDispositionPhase.AwaitingInput);
        }
    }

    private static void RequireKnowledgeResidueReceipt(
        KnowledgeResidueTaskSaveData task,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt,
        bool requireStoredReceipt)
    {
        bool matches = task != null
            && receipt != null
            && receipt.Kind == PhysicalItemDispositionKind.Sink
            && string.Equals(receipt.OperationId, task.sinkOperationId,
                StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, task.sinkReasonCode,
                StringComparison.Ordinal)
            && receipt.Quantity == 1
            && receipt.InputMassGrams == task.inputCapacityGrams
            && receipt.InputMassGrams > 0L
            && !string.IsNullOrWhiteSpace(receipt.RequestFingerprint)
            && !string.IsNullOrWhiteSpace(receipt.CommitId)
            && receipt.SourceStackIds.Count > 0;
        if (matches && requireStoredReceipt)
        {
            matches = string.Equals(
                    receipt.RequestFingerprint,
                    task.sinkRequestFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.CommitId,
                    task.sinkCommitId,
                    StringComparison.Ordinal)
                && receipt.InputMassGrams == task.sinkInputMassGrams
                && receipt.SourceStackIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        (task.sinkSourceStackIds ?? new List<string>())
                        .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal);
        }
        if (!matches)
        {
            throw new InvalidOperationException(
                "Knowledge residue Sink receipt does not match its research owner: "
                + (task?.sinkOperationId ?? "<missing>"));
        }
    }

    private Dictionary<int, FacilityBlueprintSO> BuildBlueprintIndex()
    {
        FacilityBlueprintSO[] source = facilityCatalog.Blueprints
            .Where(blueprint => blueprint != null)
            .ToArray();
        if (source.GroupBy(blueprint => blueprint.id).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Research restore cannot use a blueprint catalog with duplicate IDs.");
        }
        return source.ToDictionary(blueprint => blueprint.id);
    }

    private Dictionary<string, ResearchProjectSO> BuildProjectIndex()
    {
        ResearchProjectSO[] source = projectCatalog.Projects
            .Where(project => project != null)
            .ToArray();
        foreach (ResearchProjectSO project in source)
        {
            RequireCanonicalId(project.ProjectId.Value, "research project catalog");
        }
        if (source.GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Research restore cannot use a project catalog with duplicate IDs.");
        }
        return source.ToDictionary(
            project => project.ProjectId.Value,
            StringComparer.Ordinal);
    }

    private static HashSet<string> CollectResearchRecipeIds(
        IReadOnlyDictionary<int, FacilityBlueprintSO> blueprints,
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        return blueprints.Values
            .SelectMany(blueprint => blueprint.Unlocks
                .OfType<BlueprintRecipeUnlock>())
            .Concat(projects.Values.SelectMany(project => project.Unlocks
                .OfType<BlueprintRecipeUnlock>()))
            .Select(unlock => unlock.recipeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void RestoreBlueprintState(
        DungeonResearchSaveData source,
        BlueprintResearchState restored,
        IReadOnlyDictionary<int, FacilityBlueprintSO> blueprints,
        ISet<int> buildings,
        ISet<string> recipes)
    {
        HashSet<int> taskIds = new HashSet<int>();
        foreach (DungeonResearchTaskSaveData task in source.tasks)
        {
            if (task == null
                || !taskIds.Add(task.blueprintId)
                || !blueprints.TryGetValue(
                    task?.blueprintId ?? -1,
                    out FacilityBlueprintSO blueprint))
            {
                throw new InvalidOperationException(
                    $"Research task has a null, duplicate, or unknown blueprint id '{task?.blueprintId ?? -1}'.");
            }
            if (!IsFiniteInRange(task.progress, 0f, blueprint.researchWorkRequired))
            {
                throw new InvalidOperationException(
                    $"Research blueprint {task.blueprintId} has invalid progress.");
            }
            restored.RestoreTask(blueprint, task.progress);
        }

        HashSet<int> completedBlueprintIds = new HashSet<int>();
        foreach (int id in source.completedBlueprintIds)
        {
            if (!completedBlueprintIds.Add(id)
                || !blueprints.TryGetValue(id, out FacilityBlueprintSO blueprint))
            {
                throw new InvalidOperationException(
                    $"Completed research blueprint id '{id}' is duplicate or unknown.");
            }
            restored.RestoreCompletedBlueprintId(id);
            ApplyUnlocks(restored, blueprint.Unlocks);
        }

        HashSet<int> unlockedBuildings = new HashSet<int>();
        foreach (int id in source.unlockedBuildingIds)
        {
            if (!unlockedBuildings.Add(id) || !buildings.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Unlocked research building id '{id}' is duplicate or unknown.");
            }
            restored.RestoreUnlockedBuildingId(id);
        }

        HashSet<string> unlockedRecipes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in source.unlockedRecipeIds)
        {
            RequireCanonicalId(id, "unlocked research recipe");
            if (!unlockedRecipes.Add(id) || !recipes.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Unlocked research recipe id '{id}' is duplicate or unknown.");
            }
            restored.UnlockRecipe(id);
        }
    }

    private static void RestoreProjectState(
        DungeonResearchSaveData source,
        BlueprintResearchState restored,
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        HashSet<string> progressIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonResearchProjectProgressSaveData saved in source.projectProgress)
        {
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Research project progress collection contains null.");
            }
            RequireCanonicalId(saved.projectId, "research progress project");
            if (!progressIds.Add(saved.projectId)
                || !projects.TryGetValue(saved.projectId, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"Research progress project '{saved.projectId}' is duplicate or unknown.");
            }
            if (!float.IsFinite(saved.requiredWorkAtCapture)
                || saved.requiredWorkAtCapture < 1f
                || !IsFiniteInRange(
                    saved.progress,
                    0f,
                    saved.requiredWorkAtCapture))
            {
                throw new InvalidOperationException(
                    $"Research project '{saved.projectId}' has invalid captured work.");
            }
            restored.Projects.RestoreProgress(
                project,
                ResearchSaveValidation.RestoreProgressRatio(
                    saved.progress,
                    saved.requiredWorkAtCapture,
                    project.RequiredWork));
        }

        HashSet<string> completedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in source.completedProjectIds)
        {
            RequireCanonicalId(id, "completed research project");
            if (!completedIds.Add(id)
                || !projects.TryGetValue(id, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"Completed research project '{id}' is duplicate or unknown.");
            }
            restored.Projects.RestoreCompleted(project.ProjectId);
            ApplyUnlocks(restored, project.Unlocks);
            if (project.Blueprint != null)
            {
                restored.RestoreCompletedBlueprintId(project.Blueprint.id);
            }
        }

        HashSet<string> resultIds = new(StringComparer.Ordinal);
        foreach (DungeonResearchCompletionResultSaveData saved in
                 source.completedProjectResults
                     ?? new List<DungeonResearchCompletionResultSaveData>())
        {
            if (saved == null
                || !resultIds.Add(saved.projectId)
                || !completedIds.Contains(saved.projectId)
                || string.IsNullOrWhiteSpace(saved.publicationKey))
            {
                throw new InvalidOperationException(
                    "Completed research result key is missing or inconsistent.");
            }
            restored.RecordCompletedProjectResultKey(
                saved.projectId,
                saved.publicationKey);
        }

        HashSet<string> queuedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonResearchQueueEntrySaveData saved in source.projectQueue)
        {
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Research queue contains null.");
            }
            RequireCanonicalId(saved.projectId, "queued research project");
            RequireCanonicalTextOrEmpty(saved.suspendedReason, "research suspension reason");
            if (!queuedIds.Add(saved.projectId)
                || completedIds.Contains(saved.projectId)
                || !projects.ContainsKey(saved.projectId))
            {
                throw new InvalidOperationException(
                    $"Queued research project '{saved.projectId}' is duplicate, completed, or unknown.");
            }
            restored.Projects.RestoreQueueEntry(
                new ResearchProjectId(saved.projectId),
                saved.suspendedReason);
        }

        if (!string.IsNullOrEmpty(source.activeProjectId))
        {
            RequireCanonicalId(source.activeProjectId, "active research project");
            if (!queuedIds.Contains(source.activeProjectId))
            {
                throw new InvalidOperationException(
                    $"Active research project '{source.activeProjectId}' is not queued.");
            }
        }
        restored.Projects.RestoreActive(
            new ResearchProjectId(source.activeProjectId));
    }

    private static void ApplyUnlocks(
        BlueprintResearchState restored,
        IEnumerable<BlueprintUnlock> unlocks)
    {
        foreach (BlueprintBuildingUnlock unlock in
                 (unlocks ?? Array.Empty<BlueprintUnlock>())
                 .OfType<BlueprintBuildingUnlock>())
        {
            restored.RestoreUnlockedBuildingId(unlock.buildingId);
        }
        foreach (BlueprintRecipeUnlock unlock in
                 (unlocks ?? Array.Empty<BlueprintUnlock>())
                 .OfType<BlueprintRecipeUnlock>())
        {
            restored.UnlockRecipe(unlock.recipeId);
        }
    }

    private static void RequireCollections(DungeonResearchSaveData source)
    {
        if (source == null
            || source.tasks == null
            || source.completedBlueprintIds == null
            || source.unlockedBuildingIds == null
            || source.unlockedRecipeIds == null
            || source.knowledgeTasks == null
            || source.projectProgress == null
            || source.completedProjectIds == null
            || source.projectQueue == null
            || source.completedProjectResults == null
            || source.pendingWorkPublication == null
            || source.activeProjectId == null)
        {
            throw new InvalidOperationException(
                "Research V7 payload is missing a required field or collection.");
        }
    }

    private static bool IsFiniteInRange(float value, float minimum, float maximum)
        => ResearchSaveValidation.IsFiniteInRange(value, minimum, maximum);

    private static void RequireCanonicalId(string value, string label)
        => ResearchSaveValidation.RequireCanonicalId(value, label);

    private static void RequireCanonicalTextOrEmpty(string value, string label)
        => ResearchSaveValidation.RequireCanonicalTextOrEmpty(value, label);
}
