using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class WorldResourceTransactionFaultDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-world-resource-transaction-fault-matrix.txt";

    [MenuItem("Tools/DungeonStory/Economy/Verify World Resource Transaction Fault Matrix")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        WriteIfChanged(ReportPath, report);
        Debug.Log(report);
    }

    public static string RunAll()
    {
        ProductionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
            "Assets/Resources/SO/Economy/Recipes/source_logging.asset");
        ResourceItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_log.asset");
        Require(recipe != null && item != null,
            "The authored logging recipe or log item is missing.");

        ResourceEconomyContentCatalog catalog = new(
            new[] { item },
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        FakeEnvironment environment = new();
        FakeOutputPublication output = new();
        FakeNodeHost nodes = new();
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        WorldResourceRuntime runtime = new(
            new WorldResourceEnvironmentDependencies(
                environment,
                catalog,
                bindings,
                new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                output),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157181),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());

        try
        {
            runtime.Tick();
            Require(runtime.NodeCount == 1,
                "The focused topology did not create exactly one node.");
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && work.Available,
                "The focused logging work was unavailable.");

            output.FailFirstCommitAfterAdmission = true;
            Require(!runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completedOnFault)
                && !completedOnFault,
                "The injected acknowledgement fault completed the cycle.");
            Require(output.PrepareCount == 1 && output.CommitCount == 1,
                "The first publication attempt did not prepare and commit once.");
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot retained)
                && retained.PendingOutputReady
                && retained.Available
                && Math.Abs(retained.CompletedWork - retained.RequiredWork) < 0.001f,
                "The frozen completed result was not retained for forward retry.");

            bool saveBlocked = false;
            try
            {
                runtime.Capture();
            }
            catch (InvalidOperationException exception)
            {
                saveBlocked = exception.Message.Contains(
                    "transient or poisoned output transaction",
                    StringComparison.Ordinal);
            }
            Require(saveBlocked,
                "Save capture was not blocked during a retained publication transaction.");

            Require(runtime.TryFinalizePendingOutput(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out bool completedOnRetry)
                && completedOnRetry,
                "The retained publication did not finalize on forward retry.");
            Require(output.PrepareCount == 1 && output.CommitCount == 2,
                "Retry prepared a second batch or skipped the retained transaction.");
            DungeonWorldResourceSaveData committed = runtime.Capture();
            WorldResourceSourceSaveData committedSource = committed.nodes
                .Single().sources.Single();
            Require(committedSource.remainingCycles == 0
                    && committedSource.completedCycleSequence == 1
                    && Math.Abs(committedSource.completedWork) < 0.001f
                    && committedSource.pendingOutput != null
                    && committedSource.pendingOutput.IsEmpty,
                "Source debit and frozen output did not finalize exactly once.");

            WorldResourceNode retainedNode = runtime.Nodes.Single();
            string committedFingerprint = Fingerprint(committed);
            environment.SetInvalidTopology();
            bool topologyRejected = false;
            try
            {
                runtime.Tick();
            }
            catch (InvalidOperationException)
            {
                topologyRejected = true;
            }
            Require(topologyRejected,
                "An unregistered topology capability was accepted.");
            Require(runtime.NodeCount == 1
                    && ReferenceEquals(runtime.Nodes.Single(), retainedNode)
                    && string.Equals(
                        Fingerprint(runtime.Capture()),
                        committedFingerprint,
                        StringComparison.Ordinal),
                "Rejected topology validation destroyed or mutated the live resource state.");

            environment.SetValidTopology(worldRevision: 3);
            runtime.Tick();
            DungeonWorldResourceSaveData rebuilt = runtime.Capture();
            WorldResourceSourceSaveData rebuiltSource = rebuilt.nodes
                .Single().sources.Single();
            Require(runtime.NodeCount == 1
                    && rebuiltSource.remainingCycles == 0
                    && rebuiltSource.completedCycleSequence == 1
                    && rebuiltSource.pendingOutput.IsEmpty,
                "A valid topology replacement did not preserve the committed source state.");
            VerifyFrozenRootSeedTamperRejected(recipe, item);
            VerifyZeroPhysicalOutputCycle(item);

            return string.Join("\n", new[]
            {
                "# V27 World Resource Transaction Fault Matrix",
                "schemaVersion=1",
                "recipe=source:logging",
                "prepareCount=1",
                "commitCount=2",
                "sourceDebitCount=1",
                "saveBlockedDuringRetained=true",
                "retryReusedFrozenTransaction=true",
                "invalidTopologyLiveStateUnchanged=true",
                "validTopologyRetainedSequence=1",
                "rootSeedTamperRejected=true",
                "maximumMassTamperRejected=true",
                "maximumSourceDigestTamperRejected=true",
                "zeroPhysicalOutputCycleExact=true",
                "RESULT=PASS"
            }) + "\n";
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
        }
    }

    private static void VerifyFrozenRootSeedTamperRejected(
        ProductionRecipeSO recipe,
        ResourceItemDefinitionSO item)
    {
        FakeNodeHost nodes = new();
        FakeOutputPublication output = new() { FailPrepare = true };
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        ResourceEconomyContentCatalog catalog = new(
            new[] { item },
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        WorldResourceRuntime runtime = new(
            new WorldResourceEnvironmentDependencies(
                new FakeEnvironment(),
                catalog,
                bindings,
                new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                output),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157181),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());
        try
        {
            runtime.Tick();
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && !runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completed)
                && !completed,
                "The root-seed fixture did not retain an unpublished frozen output.");
            string canonical = JsonUtility.ToJson(runtime.Capture());
            DungeonWorldResourceSaveData candidate = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            WorldResourcePendingOutputSaveData pending = candidate.nodes
                .Single().sources.Single().pendingOutput;
            Require(pending != null && !pending.IsEmpty,
                "The root-seed fixture did not capture a frozen output.");
            pending.rootSeed = checked(pending.rootSeed + 1);
            RequireThrows(
                () => runtime.BuildRestore(candidate),
                "A frozen WorldResource outcome accepted a different root seed.");

            DungeonWorldResourceSaveData massTamper = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            massTamper.nodes.Single().sources.Single().pendingOutput
                .maximumOutputMassGrams++;
            RequireThrows(
                () => runtime.BuildRestore(massTamper),
                "A frozen WorldResource outcome accepted a drifted maximum mass.");

            DungeonWorldResourceSaveData digestTamper = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            digestTamper.nodes.Single().sources.Single().pendingOutput
                .maximumOutputSourceDigest = new string('f', 64);
            RequireThrows(
                () => runtime.BuildRestore(digestTamper),
                "A frozen WorldResource outcome accepted a drifted maximum proof.");
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
        }
    }

    private static void VerifyZeroPhysicalOutputCycle(
        ResourceItemDefinitionSO item)
    {
        ProductionRecipeSO recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        FakeNodeHost nodes = new();
        WorldResourceRuntime runtime = null;
        try
        {
            recipe.Configure(
                "source:qa-zero-output",
                "QA zero output source",
                "Deterministic zero-output source fixture",
                "world-resource:qa",
                BuiltInWorkTypeIds.Logging.Value,
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:qa-zero-output",
                        ProductionOutputRole.Main,
                        item.ItemId,
                        1,
                        probability: 0f)
                });
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Fieldwork);
            recipe.ConfigureProcessClass(ProductionProcessClass.Gathering);
            recipe.ConfigureFlowRole(ProductionFlowRole.Source);
            ResourceEconomyContentCatalog catalog = new(
                new[] { item },
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            FakeEnvironment environment = new();
            FakeOutputPublication output = new();
            SingleTreeSourceBindingCatalog bindings =
                new(recipe.RecipeId);
            runtime = new WorldResourceRuntime(
                new WorldResourceEnvironmentDependencies(
                    environment,
                    catalog,
                    bindings,
                    new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                    output),
                new WorldResourceInfrastructureDependencies(nodes),
                new WorldResourceProgressionDependencies(
                    new RandomStreamProvider(157182),
                    new FakePersistentIds(),
                    new AlwaysResearched(),
                    new NoGrandProjectBenefits()),
                new DungeonRuntimeAggregateRootStore());
            runtime.Tick();
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completed)
                && completed,
                "A zero-physical-output source cycle did not complete.");
            WorldResourceSourceSaveData source = runtime.Capture().nodes
                .Single().sources.Single();
            Require(source.remainingCycles == 0
                    && source.completedCycleSequence == 1
                    && source.pendingOutput.IsEmpty
                    && output.PrepareCount == 0
                    && output.CommitCount == 0,
                "Zero-output completion published a fake item or failed exact source debit.");
        }
        finally
        {
            runtime?.Dispose();
            nodes.Dispose();
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static string Fingerprint(DungeonWorldResourceSaveData data)
    {
        StringBuilder value = new();
        foreach (WorldResourceNodeSaveData node in data.nodes
                     .OrderBy(entry => entry.buildingInstanceId,
                         StringComparer.Ordinal))
        {
            value.Append(node.buildingInstanceId).Append('@')
                .Append(node.gridX).Append(',').Append(node.gridY).Append('|');
            foreach (WorldResourceSourceSaveData source in node.sources
                         .OrderBy(entry => entry.workTypeId,
                             StringComparer.Ordinal))
            {
                value.Append(source.workTypeId).Append('|')
                    .Append(source.recipeId).Append('|')
                    .Append(source.completedWork.ToString("R",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append('|').Append(source.remainingCycles)
                    .Append('|').Append(source.completedCycleSequence)
                    .Append('|').Append(source.pendingOutput?.outcomeFingerprint)
                    .Append(';');
            }
        }
        return value.ToString();
    }

    private static void WriteIfChanged(string path, string contents)
    {
        string normalized = contents.Replace("\r\n", "\n");
        if (File.Exists(path)
            && string.Equals(
                File.ReadAllText(path, Encoding.UTF8),
                normalized,
                StringComparison.Ordinal))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Report directory is missing."));
        File.WriteAllText(
            path,
            normalized,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
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

    private sealed class FakeEnvironment : IWorldResourceEnvironmentPort
    {
        private WorldResourceTopologySnapshot topology;

        internal FakeEnvironment()
        {
            SetValidTopology(worldRevision: 1);
        }

        internal void SetValidTopology(int worldRevision)
        {
            topology = new WorldResourceTopologySnapshot(
                worldRevision,
                worldRevision,
                new[]
                {
                    new WorldResourceVisualSnapshot(
                        "qa:tree:1",
                        new Vector2Int(4, 5),
                        WorldResourceVisualKind.Tree)
                },
                Array.Empty<WorldResourceRenewablePatchSnapshot>());
        }

        internal void SetInvalidTopology()
        {
            topology = new WorldResourceTopologySnapshot(
                2,
                2,
                new[]
                {
                    new WorldResourceVisualSnapshot(
                        "qa:unknown:1",
                        new Vector2Int(4, 5),
                        (WorldResourceVisualKind)999)
                },
                Array.Empty<WorldResourceRenewablePatchSnapshot>());
        }

        public bool TryCaptureTopology(out WorldResourceTopologySnapshot value)
        {
            value = topology;
            return true;
        }

        public bool TryGetRenewablePatch(
            WildlifeHabitatPatchId patchId,
            out WorldResourceRenewablePatchSnapshot patch)
        {
            patch = default;
            return false;
        }

        public bool TryConsumeRenewablePatchExact(
            WildlifeHabitatPatchId patchId,
            float amount,
            out WorldResourceRenewableDebitReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public bool TryRollbackRenewablePatchDebit(
            WorldResourceRenewableDebitReceipt receipt) => false;

        public void RefreshRenewablePatch(WildlifeHabitatPatchId patchId)
        {
        }

        public void SetResourceVisualActive(string visualId, bool active)
        {
        }
    }

    private sealed class FakeOutputPublication :
        IWorldResourceOutputPublicationPort
    {
        private sealed class Token : IWorldResourceOutputPublicationToken
        {
        }

        internal bool FailFirstCommitAfterAdmission { get; set; }
        internal bool FailPrepare { get; set; }
        internal int PrepareCount { get; private set; }
        internal int CommitCount { get; private set; }

        public long GetDefinitionUnitMassGrams(string itemId) => 1_840L;

        public bool TryPrepare(
            WorldResourcePendingOutputSaveData pending,
            Vector2Int position,
            out WorldResourceOutputPublicationTransaction transaction,
            out string failureReason)
        {
            PrepareCount++;
            if (FailPrepare)
            {
                transaction = default;
                failureReason = "qa:prepare-blocked";
                return false;
            }
            transaction = new WorldResourceOutputPublicationTransaction(
                new Token());
            failureReason = string.Empty;
            return true;
        }

        public WorldResourceOutputCommitStatus CommitReleased(
            WorldResourceOutputPublicationTransaction transaction,
            Vector2Int position,
            out string failureReason)
        {
            CommitCount++;
            if (FailFirstCommitAfterAdmission && CommitCount == 1)
            {
                failureReason = "qa:acknowledgement-fault";
                return WorldResourceOutputCommitStatus.RetryableRetained;
            }
            failureReason = string.Empty;
            return WorldResourceOutputCommitStatus.Committed;
        }

        public bool TryRollback(
            WorldResourceOutputPublicationTransaction transaction,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private static WorldResourceSourceBindingCatalog CreateBuiltInBindings() =>
        new(new IWorldResourceSourceBindingContributor[]
        {
            new BuiltInWorldResourceSourceBindingContributor()
        });

    private sealed class FakeMaximumEnvelopeAuthority :
        IWorldResourceOutputMaximumEnvelopeAuthority
    {
        private readonly IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot>
            envelopes;
        private readonly IReadOnlyDictionary<string,
            WorldResourceOutputMaximumEnvelopeSnapshot> byRecipe;

        internal FakeMaximumEnvelopeAuthority(
            IResourceEconomyContentCatalog catalog,
            IWorldResourceSourceBindingCatalog bindings,
            IWorldResourceOutputPublicationPort output)
        {
            List<WorldResourceOutputMaximumEnvelopeSnapshot> captured = new();
            foreach (IGrouping<string, WorldResourceSourceBinding> group
                     in bindings.Bindings.GroupBy(
                             value => value.RecipeId,
                             StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (!catalog.TryGetRecipe(group.Key, out ProductionRecipeSO recipe))
                    continue;
                List<WorldResourceOutputMaximumLineSnapshot> lines = new();
                long total = 0L;
                foreach (ProductionOutputDefinition line in recipe
                             .CaptureCanonicalOutputs()
                             .OrderBy(value => value.OutputLineId,
                                 StringComparer.Ordinal))
                {
                    bool physical = ProductionOutputRoleRules.IsPhysical(
                            line.Role)
                        && line.Probability > 0f;
                    int quantity = physical ? line.Amount : 0;
                    long unit = physical
                        ? output.GetDefinitionUnitMassGrams(line.ItemId)
                        : 0L;
                    long mass = checked(unit * quantity);
                    total = checked(total + mass);
                    lines.Add(new WorldResourceOutputMaximumLineSnapshot(
                        line.OutputLineId,
                        line.Role,
                        line.ItemId,
                        line.Probability,
                        quantity,
                        unit,
                        mass,
                        physical ? new string('a', 64) : string.Empty));
                }
                string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
                CanonicalSemanticDigestBuilder digest = new();
                digest.Append("qa-world-resource-maximum@1");
                digest.Append(recipe.RecipeId);
                digest.Append(recipeDigest);
                digest.Append(total);
                captured.Add(new WorldResourceOutputMaximumEnvelopeSnapshot(
                    recipe.RecipeId,
                    group.Select(value => value.BindingId).ToArray(),
                    recipeDigest,
                    ProductionOutputFactor.One,
                    lines,
                    total,
                    1L,
                    new string('b', 64),
                    digest.ComputeSha256()));
            }
            envelopes = Array.AsReadOnly(captured.ToArray());
            byRecipe = captured.ToDictionary(
                value => value.RecipeId,
                value => value,
                StringComparer.Ordinal);
            CanonicalSemanticDigestBuilder authority = new();
            authority.Append("qa-world-resource-maximum-authority@1");
            foreach (WorldResourceOutputMaximumEnvelopeSnapshot value in captured)
                authority.Append(value.SourceDigest);
            AuthorityFingerprint = authority.ComputeSha256();
        }

        public string AuthorityFingerprint { get; }
        public IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot>
            Envelopes => envelopes;

        public WorldResourceOutputMaximumEnvelopeSnapshot Require(
            string recipeId) => byRecipe.TryGetValue(
            recipeId,
            out WorldResourceOutputMaximumEnvelopeSnapshot value)
                ? value
                : throw new InvalidOperationException(
                    "QA world-resource maximum proof is missing: " + recipeId);
    }

    private sealed class SingleTreeSourceBindingCatalog :
        IWorldResourceSourceBindingCatalog
    {
        private readonly WorldResourceSourceBinding binding;
        private readonly IReadOnlyList<WorldResourceSourceBinding> bindings;

        internal SingleTreeSourceBindingCatalog(string recipeId)
        {
            binding = new WorldResourceSourceBinding(
                "world-resource:qa-zero-output",
                WorldResourceSourceBindingKind.Visual,
                WorldResourceVisualKind.Tree,
                default,
                BuiltInWorkTypeIds.Logging,
                recipeId);
            bindings = Array.AsReadOnly(new[] { binding });
        }

        public IReadOnlyList<WorldResourceSourceBinding> Bindings => bindings;
        public string CatalogFingerprint { get; } = new string('c', 64);

        public WorldResourceSourceBinding RequireVisual(
            WorldResourceVisualKind kind) => kind == WorldResourceVisualKind.Tree
            ? binding
            : throw new InvalidOperationException(
                "The QA source binding supports only Tree.");

        public WorldResourceSourceBinding RequireRenewablePatch(
            WildlifeHabitatType habitatType) => throw new InvalidOperationException(
            "The QA source binding has no renewable patch.");
    }

    private sealed class FakeNodeHost : IWorldResourceNodeHostPort, IDisposable
    {
        private readonly List<GameObject> objects = new();

        public WorldResourceNode CreateNode(
            IWorldResourceRuntime runtime,
            BuildingInstanceId nodeId,
            Vector2Int position,
            string displayName)
        {
            GameObject target = new("QA_WorldResourceNode");
            objects.Add(target);
            WorldResourceNode node = target.AddComponent<WorldResourceNode>();
            node.Configure(runtime, nodeId, displayName);
            return node;
        }

        public void DestroyNode(WorldResourceNode node)
        {
            if (node == null)
                return;
            objects.Remove(node.gameObject);
            UnityEngine.Object.DestroyImmediate(node.gameObject);
        }

        public void MarkDynamicStateDirty()
        {
        }

        public void ResetCandidatesAndReplan()
        {
        }

        public void Dispose()
        {
            foreach (GameObject target in objects.ToArray())
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
            objects.Clear();
        }
    }

    private sealed class FakePersistentIds : IPersistentIdGenerator
    {
        private int next;

        public ItemInstanceId NewItemInstanceId() =>
            (ItemInstanceId)("item-instance:qa:" + ++next);

        public ItemStackId NewItemStackId() =>
            (ItemStackId)("stack:qa:" + ++next);

        public CharacterId NewCharacterId() =>
            (CharacterId)("character:qa:" + ++next);

        public BuildingInstanceId NewBuildingInstanceId() =>
            (BuildingInstanceId)("building:qa:world-resource:" + ++next);

        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() =>
            (WildlifeHabitatPatchId)("wildlife-habitat:qa:" + ++next);
    }

    private sealed class AlwaysResearched : IWorldResourceResearchPort
    {
        public bool IsCompleted(string researchId) => true;
    }

    private sealed class NoGrandProjectBenefits : IGrandProjectBenefitQuery
    {
        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;
    }
}
