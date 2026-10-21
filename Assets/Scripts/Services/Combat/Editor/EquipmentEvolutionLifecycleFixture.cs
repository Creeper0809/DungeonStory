#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class EquipmentEvolutionLifecycleFixture
{
    public static void Run()
    {
        Require(Application.isBatchMode && !Application.isPlaying,
            "Equipment lifecycle verification requires isolated edit batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output),
            "Equipment lifecycle verification requires the launcher's explicit evidence directory.");
        string previous = Environment.GetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable);
        Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable,
            Path.Combine(output, "persistent-data"));
        try
        {
            // Edit batches do not install a game save root. Establish and validate a scoped
            // QA root before constructing any fixture components, then restore the caller.
            Require(DungeonPersistencePaths.IsIsolatedVerification, "Isolated fixture persistence was not established.");
            RunContracts();
        }
        finally { Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, previous); }
    }

    private static void RunContracts()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Equipment lifecycle verification requires isolated batch execution.");
        var rows = new List<string> { "contract\tstatus" };
        foreach (bool reforge in new[] { true, false })
        {
            string kind = reforge ? "reforge" : "reattunement";
            using (var subject = new Subject(reforge))
            {
                subject.Input.FailNextRequest = true;
                Require(!subject.Queue(out string failure) && failure == "c11-request-rejected"
                        && subject.Input.CloseCalls == 1 && subject.Input.ActiveCount == 0
                        && subject.ActiveOrders == 0 && subject.Physical.CommitAttempts == 0,
                    kind + " queue rejection did not clean up the opened input owner.");
                Require(subject.Queue(out failure), kind + " queue retry failed: " + failure);
                rows.Add(kind + "_queue_failure_cleanup_retry\tPASS");
                string before = subject.Capture();
                subject.Input.FailNextClose = true;
                Require(!subject.Cancel(out failure) && failure == "c11-close-rejected"
                        && subject.Capture() == before && subject.Input.ActiveCount == 1,
                    kind + " failed cancellation changed its order or released the input owner.");
                subject.RestoreFreshOwner(before, rejectFirst: true);
                Require(subject.Cancel(out failure), kind + " restored cancellation retry failed: " + failure);
                int closes = subject.Input.CloseCalls;
                Require(!subject.Cancel(out failure) && !string.IsNullOrEmpty(failure)
                        && subject.Input.CloseCalls == closes && subject.Input.ActiveCount == 0
                        && subject.ActiveOrders == 0 && subject.Physical.CommitAttempts == 0,
                    kind + " repeated cancellation performed another close or mutated materials.");
                Require(subject.Queue(out failure) && subject.Cancel(out failure),
                    kind + " could not queue and cancel again after the terminal order.");
                rows.Add(kind + "_cancel_failure_json_fresh_owner_retry_terminal_zero_effect\tPASS");
            }

            using (var subject = new Subject(reforge))
            {
                Require(subject.Queue(out string failure), kind + " material setup queue failed: " + failure);
                subject.DeliverMaterials();
                subject.Physical.FailNextAcknowledgement = true;
                Require(!subject.Apply(subject.RequiredWork, out failure) && failure == "c11-ack-rejected"
                        && subject.Physical.CommitAttempts == 1 && subject.Physical.UniqueCommitCount == 1 && subject.MaterialQuantity == 0,
                    kind + " injected acknowledgement loss did not leave one durable material transfer.");
                string pending = subject.Capture();
                WriteNew(kind + "-pending-material.json", pending);
                subject.RejectProgress(pending, subject.RequiredWork, state: EvolutionReforgeOrderState.InProgress);
                subject.RejectProgress(pending, subject.RequiredWork, state: EvolutionReforgeOrderState.Blocked);
                int closes = subject.Input.CloseCalls;
                Require(!subject.Cancel(out failure) && subject.Input.CloseCalls == closes,
                    kind + " allowed cancellation after material custody changed.");
                subject.RestoreFreshOwner(pending, rejectFirst: false);
                string equipmentBefore = JsonUtility.ToJson(subject.Equipment.Instance.evolution);
                subject.Input.FailNextClose = true;
                Require(!subject.Apply(subject.RequiredWork, out failure) && failure == "c11-close-rejected"
                        && subject.Physical.CommitAttempts == 2 && subject.Physical.UniqueCommitCount == 1 && subject.MaterialQuantity == 0
                        && subject.PublicationUpdates == 2
                        && JsonUtility.ToJson(subject.Equipment.Instance.evolution) == equipmentBefore,
                    kind + " failed completion close did not restore the equipment publication.");
                string closePending = subject.Capture();
                WriteNew(kind + "-pending-close.json", closePending);
                subject.VerifyPendingProgressBoundaries(closePending);
                rows.Add(kind + "_pending_completion_progress_boundaries\tPASS");
                subject.RestoreFreshOwner(closePending, rejectFirst: false);
                Require(subject.Apply(0, out failure), kind + " restored completion retry failed: " + failure);
                int updates = subject.Equipment.UpdateCalls;
                closes = subject.Input.CloseCalls;
                Require(subject.ActiveOrders == 0 && subject.Input.ActiveCount == 0
                        && subject.Physical.CommitAttempts == 2 && subject.Physical.UniqueCommitCount == 1 && subject.MaterialQuantity == 0
                        && !subject.Apply(1, out _) && !subject.Cancel(out _)
                        && subject.Equipment.UpdateCalls == updates && subject.Input.CloseCalls == closes,
                    kind + " terminal replay duplicated publication, close, or material consumption.");
                rows.Add(kind + "_material_ack_json_restore_completion_rollback_retry\tPASS");
            }
        }
        Require(EquipmentEvolutionMaterialOutboxFixture.Run(), "Existing material outbox restore/receipt joins failed.");
        rows.Add("existing_material_outbox_restore_joins\tPASS");
        EquipmentEvolutionInputOwnerDebugScenarios.RunAll();
        rows.Add("existing_input_owner_authority_release\tPASS");
        WriteNew("equipment-evolution-lifecycle.tsv", string.Join("\n", rows) + "\n");
        Debug.Log("EQUIPMENT_EVOLUTION_LIFECYCLE=PASS; contracts=10; controlled-runtime-ports; physical-material-transactions; fresh-parent-owner");
    }

    private sealed class Subject : IDisposable
    {
        private readonly bool reforge;
        private readonly GameObject facilityObject;
        private readonly BuildableObject facility;
        private readonly ResourceEconomyContentCatalog materials;
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IWorldItemStackRuntime world;
        private readonly EvolutionGameplayOutcomeEditorFixture outcomes;
        private readonly List<string> materialStacks = new();
        private readonly string catalyst = EvolutionCatalystItemId.BuildCatalyst("industry", 1);
        private readonly int usagePublicationCount;
        private EquipmentEvolutionRuntime runtime;
        private string orderId;
        private string destination;
        internal float RequiredWork { get; private set; }
        internal InputProbe Input { get; private set; }
        internal EquipmentProbe Equipment { get; }
        internal PhysicalProbe Physical { get; }
        internal int PublicationUpdates => Equipment.UpdateCalls - usagePublicationCount;
        internal int ActiveOrders => runtime.Capture().reforgeOrders.Count + runtime.Capture().reattunementOrders.Count;
        internal int MaterialQuantity => materialStacks.Sum(repository.GetEditorTestQuantity);

        internal Subject(bool reforge)
        {
            this.reforge = reforge;
            ResourceGameContentCatalog root = new(new UnityGameContentRootLoader());
            ResourceCombatEquipmentCatalog catalog = new(root);
            Require(catalog.TryGet("weapon:longsword", out CombatEquipmentDefinitionSO definition),
                "Authored longsword definition is missing.");
            materials = new ResourceEconomyContentCatalog(
                Resources.LoadAll<ResourceItemDefinitionSO>(ResourceItemDefinitionSO.ResourcePath),
                Resources.LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath),
                Resources.LoadAll<CropDefinitionSO>(CropDefinitionSO.ResourcePath),
                Resources.LoadAll<CraftMaterialDefinitionSO>(CraftMaterialDefinitionSO.ResourcePath));
            facilityObject = new GameObject("C11_EquipmentEvolution_Facility");
            facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(Resources.LoadAll<BuildingSO>(string.Empty).First(value => value != null
                && value.GetAbility<BuildingEquipmentCraftingAbility>()?.CraftableEquipmentIds
                    .Contains(definition.EquipmentId, StringComparer.Ordinal) == true), new Vector2Int(9, 4));
            var equipment = DispatchProxy.Create<ICombatEquipmentRuntime, EquipmentProbe>();
            Equipment = (EquipmentProbe)(object)equipment;
            Equipment.Definition = definition;
            Equipment.Instance = new CombatEquipmentInstance
            {
                instanceId = "equipment:c11:" + (reforge ? "reforge" : "reattunement"),
                definitionId = definition.EquipmentId, materialId = "material:iron",
                sourceStackId = "world-item:c11:equipment", worldState = CombatEquipmentWorldState.Stored,
                quality = CombatEquipmentQuality.Normal, durabilityRatio = 1, powerCharge = 100,
                evolution = new EquipmentEvolutionState()
            };
            if (!reforge)
            {
                Equipment.Instance.evolution.evolutionNodes.Add(new EvolutionNode
                { nodeId = "node:c11:historical", historical = true, mechanicallyUnlocked = true, uiVisible = true });
                Equipment.Instance.evolution.activeHistoricalNodeIds.Add("node:c11:historical");
            }
            IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
            repository = new WorldItemRepository(new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore());
            var mass = new PhysicalItemMassQuery(items);
            query = new WorldItemQueryService(items, mass, repository, EditorNullItemMarkerPresenter.Instance);
            Physical = new PhysicalProbe(new PhysicalItemBatchDispositionService(repository, mass, EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock()));
            world = DispatchProxy.Create<IWorldItemStackRuntime, WorldProbe>();
            ((WorldProbe)(object)world).Read = () => query.GetAllStacks().Concat(new[]
            {
                // Equipment state is an observed port in this fixture. Material custody uses
                // the real repository/disposition service; no synthetic equipment is consumed.
                new WorldItemStackSnapshot
                {
                    StackId = Equipment.Instance.sourceStackId, ItemId = PhysicalItemIds.ForEquipment(definition.EquipmentId),
                    ItemInstanceId = Equipment.Instance.instanceId, Quantity = 1,
                    State = string.IsNullOrEmpty(destination) ? WorldItemStackState.Stored : WorldItemStackState.FacilityBuffer,
                    DestinationId = destination ?? string.Empty,
                    Components = new[] { EquipmentItemStateCodec.Encode(Equipment.Instance) }
                }
            }).ToArray();
            outcomes = new EvolutionGameplayOutcomeEditorFixture("run:c11:" + (reforge ? "reforge" : "reattunement"));
            CreateRuntime();
            EquipmentEvolutionState prepared = runtime.RecordUsage(Equipment.Instance.instanceId,
                "combat:hit", Equipment.Instance.evolution.RequiredMastery, 1,
                "character:c11:smith", 0, new[] { "combat", "melee" });
            Require(prepared.reforgeReady && !string.IsNullOrEmpty(prepared.pendingHistoryHash)
                    && prepared.formulaEvidence.Count == 1,
                "Actual usage command did not freeze the original evidence required for reforge.");
            usagePublicationCount = Equipment.UpdateCalls;
        }

        private void CreateRuntime()
        {
            Input = new InputProbe();
            runtime = new EquipmentEvolutionRuntime(
                new EquipmentEvolutionFormulaCatalogs(
                    CharacterAiEditorTestDependencies.ContentDefinitions),
                (ICombatEquipmentRuntime)(object)Equipment,
                new UsageLedgerCompactor(), new EvolutionModuleRegistry(), materials, world, Physical, Input,
                new FacilityEvolutionStateComponentFactory(), new DungeonRuntimeAggregateRootStore(),
                new GameplayOutcomeNarrativeEvidenceQuery(outcomes.Ledger),
                new GameplayOutcomeEvidenceUseTransaction(outcomes.Ledger,
                    new GameplayOutcomeNarrativeEvidenceQuery(outcomes.Ledger)));
        }

        internal bool Queue(out string failure)
        {
            if (reforge)
            {
                if (!runtime.TryQueueReforge(Equipment.Instance.instanceId, facility, catalyst, string.Empty,
                        out EvolutionReforgeOrder order, out failure)) return false;
                orderId = order.orderId; destination = order.destinationId; RequiredWork = order.requiredWork;
            }
            else
            {
                if (!runtime.TryQueueReattunement(Equipment.Instance.instanceId, facility, "node:c11:historical", false,
                        catalyst, out EquipmentReattunementOrder order, out failure)) return false;
                orderId = order.orderId; destination = order.destinationId; RequiredWork = order.requiredWork;
            }
            return true;
        }

        internal bool Cancel(out string failure) => reforge
            ? runtime.CancelReforge(orderId, out failure) : runtime.CancelReattunement(orderId, out failure);
        internal bool Apply(float work, out string failure) => reforge
            ? runtime.ApplyReforgeWork(orderId, work, out _, out failure)
            : runtime.ApplyReattunementWork(orderId, work, out _, out failure);
        internal string Capture() => JsonUtility.ToJson(runtime.Capture());

        internal void DeliverMaterials()
        {
            var requirements = new Dictionary<string, int> { [catalyst] = 1 };
            if (reforge)
            {
                EvolutionReforgeOrder order = runtime.ReforgeOrders.Single(value => value.orderId == orderId);
                Require(order.stabilizerAmount == 0, "Fixture unexpectedly requested a stabilizer.");
                requirements.Add(order.primaryMaterialItemId, order.primaryMaterialAmount);
                requirements.Add(order.bindingItemId, order.bindingAmount);
            }
            foreach (var entry in requirements)
                materialStacks.Add(WorldItemRepositoryEditorAccess.AddStack(repository, entry.Key, entry.Value,
                    WorldItemStackState.FacilityBuffer, destinationId: destination));
        }

        internal void RestoreFreshOwner(string json, bool rejectFirst)
        {
            var save = JsonUtility.FromJson<EquipmentEvolutionSaveData>(json);
            CreateRuntime();
            EquipmentEvolutionRestoreCandidate candidate = runtime.BuildRestoreCandidate(save);
            if (rejectFirst)
            {
                Input.FailNextReplace = true;
                bool rejected = false;
                try { runtime.PublishRestoreCandidate(candidate); }
                catch (InvalidOperationException exception)
                { rejected = exception.Message.Contains("c11-restore-rejected"); }
                Require(rejected && ActiveOrders == 0 && Input.ActiveCount == 0,
                    "Rejected fresh-owner restore changed the target state.");
            }
            runtime.PublishRestoreCandidate(candidate);
            Require(Capture() == json && Input.ActiveCount == 1,
                "Fresh parent-owner JSON restore changed the pending order.");
        }

        internal void VerifyPendingProgressBoundaries(string json)
        {
            foreach (float invalid in new[] { -0.01f, RequiredWork + 1, float.NaN, float.PositiveInfinity })
                RejectProgress(json, invalid);
            RejectProgress(json, RequiredWork, consumed: false);
            RejectProgress(json, RequiredWork, state: EvolutionReforgeOrderState.WaitingForMaterials);
            var blocked = JsonUtility.FromJson<EquipmentEvolutionSaveData>(json);
            if (reforge) blocked.reforgeOrders.Single().state = EvolutionReforgeOrderState.Blocked;
            else blocked.reattunementOrders.Single().state = EvolutionReforgeOrderState.Blocked;
            runtime.BuildRestoreCandidate(blocked);
            Require(Capture() == json, "Progress candidate validation changed the current owner.");
        }

        internal void RejectProgress(string json, float progress, bool? consumed = null,
            EvolutionReforgeOrderState? state = null)
        {
            var changed = JsonUtility.FromJson<EquipmentEvolutionSaveData>(json);
            if (reforge)
            {
                EvolutionReforgeOrder order = changed.reforgeOrders.Single();
                order.completedWork = progress;
                if (consumed.HasValue) order.materialsConsumed = consumed.Value;
                if (state.HasValue) order.state = state.Value;
            }
            else
            {
                EquipmentReattunementOrder order = changed.reattunementOrders.Single();
                order.completedWork = progress;
                if (consumed.HasValue) order.materialsConsumed = consumed.Value;
                if (state.HasValue) order.state = state.Value;
            }
            try { runtime.BuildRestoreCandidate(changed); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Invalid pending completion progress was accepted.");
        }

        public void Dispose()
        {
            outcomes.Dispose();
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private sealed class InputProbe : IEquipmentEvolutionInputOwnerRuntime
    {
        private readonly HashSet<string> active = new(StringComparer.Ordinal);
        internal bool FailNextRequest, FailNextClose, FailNextReplace;
        internal int CloseCalls { get; private set; }
        internal int ActiveCount => active.Count;
        public bool TryOpen(EquipmentEvolutionInputOwnerDescriptor value,
            out EquipmentEvolutionInputOwnerProjection projection, out string failure)
        {
            projection = new EquipmentEvolutionInputOwnerProjection(100_000, 1, "capacity:c11");
            failure = string.Empty;
            return active.Add(value.OrderId);
        }
        public bool TryRequest(EquipmentEvolutionInputOwnerDescriptor value, out string failure)
        {
            failure = FailNextRequest ? "c11-request-rejected" : string.Empty;
            FailNextRequest = false;
            return failure.Length == 0;
        }
        public bool TryClose(EquipmentEvolutionInputOwnerDescriptor value, string reason, out string failure)
        {
            CloseCalls++;
            failure = FailNextClose ? "c11-close-rejected" : string.Empty;
            FailNextClose = false;
            if (failure.Length != 0) return false;
            Require(active.Remove(value.OrderId), "Input close was repeated for an absent owner.");
            return true;
        }
        public bool TryValidateAuthority(EquipmentEvolutionInputOwnerDescriptor value, out string failure)
        { failure = active.Contains(value.OrderId) ? string.Empty : "c11-input-owner-missing"; return failure.Length == 0; }
        public bool TryReplaceForRestore(IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor> values, out string failure)
        {
            failure = FailNextReplace ? "c11-restore-rejected" : string.Empty;
            FailNextReplace = false;
            if (failure.Length != 0) return false;
            Require(values.Select(value => value.OrderId).Distinct(StringComparer.Ordinal).Count() == values.Count,
                "Restore input owner set contained duplicates.");
            active.Clear();
            foreach (var value in values) active.Add(value.OrderId);
            return true;
        }
    }

    public class EquipmentProbe : DispatchProxy
    {
        internal CombatEquipmentInstance Instance;
        internal CombatEquipmentDefinitionSO Definition;
        internal int UpdateCalls;
        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            switch (method.Name)
            {
                case "get_ModuleInstances": return Array.Empty<EquipmentModuleInstance>();
                case nameof(ICombatEquipmentRuntime.TryGetDefinition):
                    arguments[1] = Definition;
                    return (string)arguments[0] == Definition.EquipmentId;
                case nameof(ICombatEquipmentRuntime.TryGetInstance):
                case nameof(ICombatEquipmentRuntime.TryGetInstanceBySourceStack):
                    bool found = (string)arguments[0] == (method.Name == nameof(ICombatEquipmentRuntime.TryGetInstance)
                        ? Instance.instanceId : Instance.sourceStackId);
                    arguments[1] = found ? Instance.Clone() : null;
                    return found;
                case nameof(ICombatEquipmentRuntime.TryUpdateEvolutionState):
                    Require((string)arguments[0] == Instance.instanceId, "Equipment publication targeted another owner.");
                    UpdateCalls++;
                    Instance.evolution = ((EquipmentEvolutionState)arguments[1]).Clone();
                    return true;
                default: throw new NotSupportedException("Unexpected equipment fixture call: " + method.Name);
            }
        }
    }

    public class WorldProbe : DispatchProxy
    {
        internal Func<IReadOnlyList<WorldItemStackSnapshot>> Read;
        protected override object Invoke(MethodInfo method, object[] arguments) =>
            method.Name == nameof(IWorldItemStackRuntime.GetAllStacks) ? Read()
                : throw new NotSupportedException("Unexpected world fixture call: " + method.Name);
    }

    private sealed class PhysicalProbe : IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;
        internal PhysicalProbe(IPhysicalItemBatchDispositionService inner) => this.inner = inner;
        internal bool FailNextAcknowledgement;
        internal int CommitAttempts;
        private readonly HashSet<string> commitIds = new(StringComparer.Ordinal);
        internal int UniqueCommitCount => commitIds.Count;
        public bool TryCommit(IReadOnlyList<PhysicalItemTransformInput> inputs, PhysicalItemDispositionKind kind,
            string operation, string reason, out PhysicalItemBatchDispositionReceipt receipt, out string failure)
        {
            CommitAttempts++;
            bool committed = inner.TryCommit(inputs, kind, operation, reason, out receipt, out failure);
            if (committed) commitIds.Add(receipt.CommitId);
            return committed;
        }
        public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> inputs, PhysicalItemDispositionKind kind,
            string operation, string reason, out PhysicalItemBatchDispositionReceipt receipt, out string failure)
        {
            CommitAttempts++;
            bool committed = inner.TryCommitPending(inputs, kind, operation, reason, out receipt, out failure);
            if (committed) commitIds.Add(receipt.CommitId);
            return committed;
        }
        public bool TryGetPending(string operation, out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operation, out receipt);
        public bool Acknowledge(string commit, out string failure)
        {
            failure = FailNextAcknowledgement ? "c11-ack-rejected" : string.Empty;
            FailNextAcknowledgement = false;
            return failure.Length == 0 && inner.Acknowledge(commit, out failure);
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void WriteNew(string name, string text)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "Explicit QA log path required.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(directory.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Evidence must remain inside project QA.");
        using var writer = new StreamWriter(new FileStream(Path.Combine(directory, name), FileMode.CreateNew));
        writer.Write(text);
    }
}
#endif
