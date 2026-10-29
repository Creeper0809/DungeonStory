#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static partial class FacilitySynthesisDebugScenarios
{
    internal const string CausalSynthesisOwnerRestoreKind =
        "owner-family:facility-synthesis-lineage";
    internal const string CausalSynthesisFirstActionId =
        "action:facility-synthesis-patrol-board";
    internal const string CausalSynthesisSecondActionId =
        "action:facility-synthesis-battle-banner";

    private const string CausalSynthesisOwnerKind =
        "owner-kind:facility-synthesis-lineage";
    private const string CausalSynthesisStateSchema =
        "dungeonstory-causal-facility-synthesis-owner-state@2";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalSynthesisFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A facility-synthesis checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalSynthesisContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CausalSynthesisFirstActionId,
                "facility",
                FacilitySynthesisOutcomeIds.Completed,
                context.CaptureState,
                context.ExecuteFirst,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalSynthesisOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence),
            new DelegateProductionCausalActionFixture(
                CausalSynthesisSecondActionId,
                "facility",
                FacilitySynthesisOutcomeIds.Completed,
                () => new Dictionary<string, object>(),
                context.ExecuteSecond,
                context.RequireSecondOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalSynthesisOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence)
        };
    }

    internal static bool RunCausalSynthesisOwnerFamily(bool log = false)
    {
        const string OwnerId = "facility-synthesis:causal-focused:banner-lineage";
        JObject firstCheckpoint;
        using (var fixtures = new CausalSynthesisFixtureSet(
                   CreateCausalSynthesisFixtures(
                       "run:causal-facility-synthesis:first",
                       OwnerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalSynthesisFirstActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require((int)before["facilitySynthesis.stage"] == 0
                    && (int)before["facilitySynthesis.liveBuildingCount"] == 3
                    && (long)before["facilitySynthesis.outcomeRevision"] == 0L,
                "Facility synthesis did not start from the exact authored world.");
            Require(first.Execute(),
                "The real patrol-board synthesis did not execute.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The patrol-board synthesis has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["facilitySynthesis.stage"] == 1
                    && (int)after["facilitySynthesis.liveBuildingCount"] == 2
                    && (long)after["facilitySynthesis.outcomeRevision"] == 1L
                    && (int)after["facilitySynthesis.exactOutcomeCount"] == 1,
                "The patrol-board synthesis did not publish its exact owner state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject secondCheckpoint;
        using (var fixtures = new CausalSynthesisFixtureSet(
                   CreateCausalSynthesisFixtures(
                       "run:causal-facility-synthesis:second",
                       OwnerId,
                       firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalSynthesisFirstActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                CausalSynthesisSecondActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require((int)restored["facilitySynthesis.stage"] == 1
                    && (long)restored["facilitySynthesis.outcomeRevision"] == 1L
                    && !first.Execute(),
                "Fresh composition restore lost or replayed patrol-board synthesis.");
            Require(second.Execute(),
                "The real battle-banner synthesis did not execute.");
            Require(second.RequireExactOutcome().HasCanonicalPayloadHash,
                "The battle-banner synthesis has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["facilitySynthesis.stage"] == 2
                    && (int)after["facilitySynthesis.liveBuildingCount"] == 1
                    && (long)after["facilitySynthesis.outcomeRevision"] == 2L
                    && (int)after["facilitySynthesis.exactOutcomeCount"] == 2,
                "The battle-banner synthesis did not extend the same owner.");
            secondCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)second)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CausalSynthesisFixtureSet(
                   CreateCausalSynthesisFixtures(
                       "run:causal-facility-synthesis:terminal",
                       OwnerId,
                       secondCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalSynthesisFirstActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                CausalSynthesisSecondActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require((int)restored["facilitySynthesis.stage"] == 2
                    && !first.Execute()
                    && !second.Execute(),
                "Terminal facility-synthesis owner was not idempotent.");
        }

        JObject tamperedOwner = (JObject)firstCheckpoint.DeepClone();
        ((JObject)tamperedOwner["domainState"])["ownerId"] =
            "facility-synthesis:tampered-owner";
        RequireCausalSynthesisRestoreRejected(
            tamperedOwner,
            OwnerId,
            "A tampered facility-synthesis owner identity was accepted.");

        JObject tamperedWorld = (JObject)firstCheckpoint.DeepClone();
        JArray buildings = (JArray)((JObject)((JObject)tamperedWorld[
            "domainState"])["worldState"])["buildings"];
        JObject survivor = buildings
            .OfType<JObject>()
            .Single(value => string.Equals(
                (string)value["persistentInstanceId"],
                CausalSynthesisContext.SurvivorId(OwnerId),
                StringComparison.Ordinal));
        survivor["persistentInstanceId"] =
            "building:facility-synthesis:tampered-survivor";
        RequireCausalSynthesisRestoreRejected(
            tamperedWorld,
            OwnerId,
            "A tampered facility-synthesis survivor identity was accepted.");

        Require(VerifyOutcomeCommitFailureRestoresAllMaterials(),
            "Facility synthesis rejection did not restore every material.");

        if (log)
            Debug.Log("Facility synthesis two-stage causal owner family PASS");
        return true;
    }

    public static void RunCausalSynthesisOwnerFamilyBatch()
    {
        try
        {
            Require(
                RunCausalSynthesisOwnerFamily(log: true),
                "Facility synthesis causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void RequireCausalSynthesisRestoreRejected(
        JObject checkpoint,
        string ownerId,
        string message)
    {
        bool rejected = false;
        try
        {
            using var ignored = new CausalSynthesisFixtureSet(
                CreateCausalSynthesisFixtures(
                    "run:causal-facility-synthesis:tampered",
                    ownerId,
                    checkpoint));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private sealed class CausalSynthesisContext : IDisposable
    {
        private const string WarningBellAsset = "G02_경보종";
        private const string FactionFlagAsset = "E05_세력깃발";
        private const string PatrolBoardAsset = "G03_순찰상황판";
        private const string BattleBannerAsset = "G05_전투깃발";
        private const string PatrolRecipeAsset = "RS_PatrolBoard";
        private const string BannerRecipeAsset = "RS_BattleBanner";

        private readonly string ownerId;
        private readonly string survivorId;
        private readonly string firstFlagId;
        private readonly string secondFlagId;
        private readonly SynthesisScenarioWorld world;
        private readonly FacilitySynthesisRecipeSO firstRecipe;
        private readonly FacilitySynthesisRecipeSO secondRecipe;
        private readonly GameplayOutcomeLedger ledger;
        internal readonly SynthesisObservationFixture Observations;
        private readonly FacilitySynthesisRuntime runtime;
        private readonly ModularFacilityWorldSaveService worldSave;
        private BuildableObject survivor;
        private BuildableObject firstFlag;
        private BuildableObject secondFlag;
        private int stage;
        private long firstRevision;
        private long secondRevision;
        private bool disposed;

        internal CausalSynthesisContext(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            survivorId = "building:causal-facility-synthesis:" + scope
                + ":survivor";
            firstFlagId = "building:causal-facility-synthesis:" + scope
                + ":flag-one";
            secondFlagId = "building:causal-facility-synthesis:" + scope
                + ":flag-two";
            world = new SynthesisScenarioWorld();
            try
            {
                firstRecipe = LoadRecipe(PatrolRecipeAsset);
                secondRecipe = LoadRecipe(BannerRecipeAsset);
                Require(firstRecipe != null && secondRecipe != null,
                    "Facility synthesis causal recipes are missing.");
                BlueprintResearchRuntime research = world.CreateResearchRuntime();
                Require(research.StateForDebug.UnlockRecipe(
                        secondRecipe.requiredResearchRecipeId),
                    "Facility synthesis causal prerequisite research was not unlocked.");

                string ledgerRunId = HasPersistentOwner
                    ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                        this.ownerId,
                        CausalSynthesisOwnerKind)
                    : causalRunId + ":facility-synthesis-ledger";
                GameplayOutcomeRegistry registry = new(
                    new IGameplayOutcomeDescriptor[]
                    {
                        new FacilitySynthesisOutcomeDescriptor(
                            new KoreanJosaFormatter())
                    },
                    new IGameplayOutcomeAdapterRegistration[]
                    {
                        new FacilitySynthesisOutcomeAdapter()
                    });
                ledger = new GameplayOutcomeLedger(
                    registry,
                    GameplayOutcomeBufferLimits.Default,
                    new GameplayOutcomeRunId(ledgerRunId),
                    1L);
                Observations = new SynthesisObservationFixture(registry, ledger, world.Events);
                runtime = world.CreateRuntime(Observations.Outbox);
                worldSave = CreateWorldSave(world);

                survivor = world.Place(WarningBellAsset, new Vector2Int(4, 0));
                firstFlag = world.Place(FactionFlagAsset, new Vector2Int(11, 0));
                secondFlag = world.Place(FactionFlagAsset, new Vector2Int(18, 0));
                BindStableIdentity(survivor, survivorId);
                BindStableIdentity(firstFlag, firstFlagId);
                BindStableIdentity(secondFlag, secondFlagId);

                if (checkpoint != null)
                {
                    JObject domain = ProductionCausalOwnerCheckpoint
                        .RestoreFamilyCommon(
                            checkpoint,
                            this.ownerId,
                            CausalSynthesisOwnerKind,
                            CausalSynthesisOwnerRestoreKind,
                            ledger);
                    RestoreDomainState(domain);
                }
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? CausalSynthesisOwnerKind
            : string.Empty;

        internal static string SurvivorId(string stableOwnerId)
        {
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(
                    CausalGameplaySyntax.RequireId(
                        stableOwnerId,
                        nameof(stableOwnerId)))
                .Substring("sha256:".Length, 24);
            return "building:causal-facility-synthesis:" + scope
                + ":survivor";
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            return new Dictionary<string, object>
            {
                ["facilitySynthesis.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "facility-synthesis:ephemeral",
                ["facilitySynthesis.stage"] = stage,
                ["facilitySynthesis.survivorId"] = survivorId,
                ["facilitySynthesis.definitionId"] = CurrentDefinitionId(),
                ["facilitySynthesis.displayName"] = CurrentDisplayName(),
                ["facilitySynthesis.facilityLevel"] = survivor.FacilityLevel,
                ["facilitySynthesis.outcomeRevision"] =
                    survivor.SynthesisOutcomeRevision,
                ["facilitySynthesis.liveBuildingCount"] = LiveBuildings().Count,
                ["facilitySynthesis.exactOutcomeCount"] = ledger
                    .CaptureGameplayOutcomes().exactOutcomes.Count,
                ["facilitySynthesis.battleBannerUnlocked"] =
                    runtime.ResearchState.UnlockedRecipeIds.Contains(
                        secondRecipe.requiredResearchRecipeId,
                        StringComparer.Ordinal),
                ["facilitySynthesis.patrolBoardCompleted"] = stage >= 1,
                ["facilitySynthesis.battleBannerCompleted"] = stage >= 2
            };
        }

        internal bool ExecuteFirst()
        {
            ThrowIfDisposed();
            if (stage != 0)
                return false;
            if (!runtime.TrySynthesize(
                    firstRecipe,
                    new[] { survivor, firstFlag },
                    out FacilitySynthesisResult result)
                || !result.Success
                || result.ResultBuilding == null
                || result.OutcomeOwnerRevision != 1L)
            {
                return false;
            }
            survivor = result.ResultBuilding;
            firstFlag = null;
            firstRevision = result.OutcomeOwnerRevision;
            stage = 1;
            ValidateState();
            GameplayOutcomeReplayIdentity identity = RequireFirstOutcome(false);
            return identity.CanonicalPayloadHash?.Length == 64;
        }

        internal bool ExecuteSecond()
        {
            ThrowIfDisposed();
            if (stage != 1)
                return false;
            if (!runtime.TrySynthesize(
                    secondRecipe,
                    new[] { survivor, secondFlag },
                    out FacilitySynthesisResult result)
                || !result.Success
                || result.ResultBuilding == null
                || result.OutcomeOwnerRevision != 2L)
            {
                return false;
            }
            survivor = result.ResultBuilding;
            secondFlag = null;
            secondRevision = result.OutcomeOwnerRevision;
            stage = 2;
            ValidateState();
            GameplayOutcomeReplayIdentity identity = RequireSecondOutcome(false);
            return identity.CanonicalPayloadHash?.Length == 64;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireFirstOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireSecondOutcome() =>
            RequireSecondOutcome(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            CaptureNarrativeEvidence(GameplayEntityId subject, int maximumCount) =>
            ProductionCausalNarrativeEvidence.Capture(
                ledger, subject, maximumCount);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException(
                    "Ephemeral facility-synthesis fixtures have no checkpoint.");
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalSynthesisOwnerKind,
                CausalSynthesisOwnerRestoreKind,
                CaptureDomainState(),
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            world?.Dispose();
            Observations?.Dispose();
        }

        private JObject CaptureDomainState()
        {
            ModularFacilityWorldSaveData snapshot =
                worldSave.CreateSnapshot(world.Grid);
            return new JObject
            {
                ["schemaVersion"] = 2,
                ["schema"] = CausalSynthesisStateSchema,
                ["stage"] = stage,
                ["ownerId"] = ownerId,
                ["survivorId"] = survivorId,
                ["firstFlagId"] = firstFlagId,
                ["secondFlagId"] = secondFlagId,
                ["worldState"] = ParseCausalSynthesisJson(
                    worldSave.ToJson(snapshot),
                    "world state"),
                ["firstRevision"] = firstRevision,
                ["secondRevision"] = secondRevision,
                ["pendingFacts"] = JObject.Parse(DungeonCharacterWorldSaveCodec.SerializeFields(Observations.Outbox.Capture()))
            };
        }

        private void RestoreDomainState(JObject domain)
        {
            RequireCausalSynthesisFields(
                domain,
                "facility synthesis domain state",
                "schemaVersion",
                "schema",
                "stage",
                "ownerId",
                "survivorId",
                "firstFlagId",
                "secondFlagId",
                "worldState",
                "firstRevision",
                "secondRevision",
                "pendingFacts");
            if ((int)domain["schemaVersion"] != 2
                || !string.Equals(
                    (string)domain["schema"],
                    CausalSynthesisStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["ownerId"],
                    ownerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["survivorId"],
                    survivorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["firstFlagId"],
                    firstFlagId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["secondFlagId"],
                    secondFlagId,
                    StringComparison.Ordinal)
                || domain["worldState"] is not JObject worldJson)
            {
                throw new InvalidOperationException(
                    "Facility synthesis checkpoint identity is invalid.");
            }

            int restoredStage = (int)domain["stage"];
            if (restoredStage is < 0 or > 2)
                throw new InvalidOperationException(
                    "Facility synthesis checkpoint stage is invalid.");
            ModularFacilityWorldSaveData snapshot = worldSave.FromJson(
                worldJson.ToString(Formatting.None));
            ModularFacilityWorldRestoreReport report =
                worldSave.ValidateRestore(world.Grid, snapshot);
            if (!report.Success)
                throw new InvalidOperationException(
                    "Facility synthesis world checkpoint is invalid: "
                    + string.Join(" | ", report.errors));

            ModularFacilityWorldRestoreCandidate candidate = null;
            try
            {
                candidate = worldSave.PrepareRestoreCandidate(
                    world.Grid,
                    snapshot);
                worldSave.BeginRestoreCandidate();
                worldSave.StageRestoreCandidate(candidate);
                worldSave.PublishRestoreCandidate();
                worldSave.CompleteRestoreCandidate();
            }
            catch
            {
                candidate?.Discard();
                worldSave.DiscardRestoreCandidate();
                throw;
            }

            stage = restoredStage;
            string pendingJson = domain["pendingFacts"].ToString(Formatting.None);
            FacilitySynthesisFactSaveSection.ValidateJson(pendingJson);
            var pendingFacts = DungeonSaveJson.Deserialize<FacilitySynthesisFactSaveData>(pendingJson);
            foreach (var pending in pendingFacts.pending)
                FacilitySynthesisFactPreflight.ValidateObservation(pending, ledger.CaptureGameplayOutcomes());
            Observations.Outbox.BeginRestoreCandidate();
            Observations.Outbox.Restore(pendingFacts);
            Observations.Outbox.PublishRestoreCandidate();
            Observations.Outbox.CompleteRestoreCandidate();
            firstRevision = (long)domain["firstRevision"];
            secondRevision = (long)domain["secondRevision"];
            BindRestoredBuildings();
            ValidateState();
            if (!JToken.DeepEquals(domain, CaptureDomainState()))
                throw new InvalidOperationException(
                    "Facility synthesis world changed during fresh restore.");
        }

        private void BindRestoredBuildings()
        {
            Dictionary<string, BuildableObject> live = LiveBuildings()
                .ToDictionary(
                    value => value.RequirePersistentInstanceId().Value,
                    StringComparer.Ordinal);
            if (!live.TryGetValue(survivorId, out survivor))
                throw new InvalidOperationException(
                    "Facility synthesis survivor is missing after restore.");
            firstFlag = live.TryGetValue(firstFlagId, out BuildableObject first)
                ? first
                : null;
            secondFlag = live.TryGetValue(secondFlagId, out BuildableObject second)
                ? second
                : null;
        }

        private void ValidateState()
        {
            ThrowIfDisposed();
            if (survivor == null
                || survivor.isDestroy
                || !string.Equals(
                    survivor.RequirePersistentInstanceId().Value,
                    survivorId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility synthesis survivor identity changed.");
            }

            BuildingSO expected = stage switch
            {
                0 => LoadBuilding(WarningBellAsset),
                1 => LoadBuilding(PatrolBoardAsset),
                2 => LoadBuilding(BattleBannerAsset),
                _ => null
            };
            int expectedBuildingCount = 3 - stage;
            int exactCount = ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            if (expected == null
                || survivor.id != expected.id
                || survivor.SynthesisOutcomeRevision != stage
                || LiveBuildings().Count != expectedBuildingCount
                || exactCount != stage
                || !runtime.ResearchState.UnlockedRecipeIds.Contains(
                    secondRecipe.requiredResearchRecipeId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility synthesis world state does not match its causal stage.");
            }

            bool firstAlive = firstFlag != null && !firstFlag.isDestroy;
            bool secondAlive = secondFlag != null && !secondFlag.isDestroy;
            if ((stage == 0 && (!firstAlive || !secondAlive))
                || (stage == 1 && (firstAlive || !secondAlive))
                || (stage == 2 && (firstAlive || secondAlive)))
            {
                throw new InvalidOperationException(
                    "Facility synthesis material lifecycle does not match its stage.");
            }

            if (stage == 0
                && (firstRevision != 0L || secondRevision != 0L))
                throw new InvalidOperationException(
                    "Initial facility synthesis state contains terminal revisions.");
            if (stage >= 1)
                RequireFirstOutcome(false);
            if (stage >= 2)
                RequireSecondOutcome(false);
            if (stage < 2 && secondRevision != 0L)
                throw new InvalidOperationException(
                    "Second synthesis revision appeared before its stage.");
        }

        private GameplayOutcomeReplayIdentity RequireFirstOutcome(
            bool throwOnFailure) => RequireExact(
                firstRecipe,
                LoadBuilding(PatrolBoardAsset),
                LoadBuilding(WarningBellAsset),
                firstFlagId,
                firstRevision,
                throwOnFailure,
                "patrol-board");

        private GameplayOutcomeReplayIdentity RequireSecondOutcome(
            bool throwOnFailure) => RequireExact(
                secondRecipe,
                LoadBuilding(BattleBannerAsset),
                LoadBuilding(PatrolBoardAsset),
                secondFlagId,
                secondRevision,
                throwOnFailure,
                "battle-banner");

        private GameplayOutcomeReplayIdentity RequireExact(
            FacilitySynthesisRecipeSO recipe,
            BuildingSO resultDefinition,
            BuildingSO survivorMaterialDefinition,
            string consumedFlagId,
            long revision,
            bool throwOnFailure,
            string label)
        {
            if (revision <= 0L)
            {
                if (throwOnFailure)
                    throw new InvalidOperationException(
                        "The " + label + " synthesis revision is missing.");
                return default;
            }
            GameplayResultKey key = FacilitySynthesisOutcomeIds.ResultKey(
                survivorId,
                revision);
            string resultDefinitionId = BuildingDefinitionIdentity.Resolve(
                resultDefinition);
            string expectedMaterials = string.Join(",",
                new[]
                {
                    (Id: survivorId, Definition: BuildingDefinitionIdentity.Resolve(
                        survivorMaterialDefinition)),
                    (Id: consumedFlagId, Definition: BuildingDefinitionIdentity.Resolve(
                        LoadBuilding(FactionFlagAsset)))
                }
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => value.Definition));
            if (ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.outcomeTypeId
                    == FacilitySynthesisOutcomeIds.Completed.Value
                && exact.ownerRevision == revision
                && HasCausalSynthesisFact(
                    exact,
                    FacilitySynthesisOutcomeIds.RecipeIdFact.Value,
                    recipe.recipeId)
                && HasCausalSynthesisFact(
                    exact,
                    FacilitySynthesisOutcomeIds.ResultDefinitionFact.Value,
                    resultDefinitionId)
                && HasCausalSynthesisFact(
                    exact,
                    FacilitySynthesisOutcomeIds.MaterialDefinitionsFact.Value,
                    expectedMaterials)
                && HasCausalSynthesisMetric(
                    exact,
                    FacilitySynthesisOutcomeIds.OwnerRevisionMetric.Value,
                    revision)
                && HasCausalSynthesisMetric(
                    exact,
                    FacilitySynthesisOutcomeIds.InheritedLevelMetric.Value,
                    1d)
                && HasCausalSynthesisMetric(
                    exact,
                    FacilitySynthesisOutcomeIds.MaterialCountMetric.Value,
                    2d)
                && exact.participants.Any(value => value != null
                    && value.entityId == consumedFlagId))
            {
                return identity;
            }
            if (throwOnFailure)
                throw new InvalidOperationException(
                    "The " + label
                    + " facility synthesis exact outcome is missing.");
            return default;
        }

        private List<BuildableObject> LiveBuildings() => world.Grid
            .FindAllOccupants(null)
            .OfType<BuildableObject>()
            .Where(value => value != null && !value.isDestroy)
            .Distinct()
            .OrderBy(value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToList();

        private string CurrentDefinitionId() =>
            BuildingDefinitionIdentity.Resolve(survivor.BuildingData);

        private string CurrentDisplayName() =>
            FacilityShopService.GetBuildingName(survivor.BuildingData);

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(CausalSynthesisContext));
        }
    }

    private sealed class CausalSynthesisFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal CausalSynthesisFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));
        }

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class CausalSynthesisGridPublisher : IGridSystemPublisher
    {
        private readonly SynthesisScenarioWorld world;

        internal CausalSynthesisGridPublisher(SynthesisScenarioWorld world) =>
            this.world = world ?? throw new ArgumentNullException(nameof(world));

        public bool TryPublishGrid(
            Grid expectedCurrent,
            Grid replacement,
            out string failureReason) =>
            world.TryPublishGrid(
                expectedCurrent,
                replacement,
                out failureReason);

        public void CompleteGridPublication()
        {
        }
    }

    private sealed class CausalSynthesisSessionStore : IGameSessionStateStore
    {
        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = null;
            return false;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
        }
    }

    private sealed class CausalSynthesisRelocationWorldService :
        IFacilityRelocationWorldService
    {
        public bool CanRelocate(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is not part of facility synthesis.";
            return false;
        }

        public bool TryPackAtDestination(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is not part of facility synthesis.";
            return false;
        }

        public bool TryCompleteRelocation(
            BuildableObject packedSource,
            out BuildableObject relocated,
            out string failureReason)
        {
            relocated = null;
            failureReason = "Relocation is not part of facility synthesis.";
            return false;
        }

        public void RestorePackedPresentation(BuildableObject packedSource)
        {
        }
    }

    private sealed class CausalSynthesisGridTextureProvider :
        IGridTextureProvider
    {
        internal CausalSynthesisGridTextureProvider(GridTexture texture) =>
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));

        public GridTexture Texture { get; }
    }

    private static ModularFacilityWorldSaveService CreateWorldSave(
        SynthesisScenarioWorld world)
    {
        GameObject textureObject = new(
            "Causal Facility Synthesis GridTexture");
        world.TrackObject(textureObject);
        GridTexture texture = textureObject.AddComponent<GridTexture>();
        BuildingSO[] definitions =
        {
            LoadBuilding("G02_경보종"),
            LoadBuilding("E05_세력깃발"),
            LoadBuilding("G03_순찰상황판"),
            LoadBuilding("G05_전투깃발")
        };
        Dictionary<int, BuildingSO> byId = definitions
            .Where(value => value != null)
            .GroupBy(value => value.id)
            .ToDictionary(group => group.Key, group => group.First());
        return new ModularFacilityWorldSaveService(
            id => byId.TryGetValue(id, out BuildingSO value) ? value : null,
            new GridBuildingObjectFactory(),
            InjectBuilding,
            new CausalSynthesisGridTextureProvider(texture),
            new CausalSynthesisRelocationWorldService(),
            new CausalSynthesisSessionStore(),
            new CausalSynthesisGridPublisher(world),
            new RestoreWorldCandidateIndex());
    }

    private static void InjectBuilding(BuildableObject building)
    {
        CharacterAiEditorTestDependencies.Inject(building);
        if (building is Shop shop)
            CharacterAiEditorTestDependencies.InjectShop(shop);
    }

    private static void BindStableIdentity(
        BuildableObject building,
        string persistentId)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        var id = new BuildingInstanceId(persistentId);
        building.RestorePersistentIdentity(id);
        FacilityEvolutionStateComponent state = building
            .GetComponent<FacilityEvolutionStateComponent>();
        if (state == null)
            return;
        FacilityEvolutionState evolution = state.InstanceEvolution;
        evolution.facilityPersistentId = id.Value;
        state.ReplaceInstanceEvolution(evolution);
    }

    private static bool HasCausalSynthesisFact(
        GameplayOutcomeSnapshot exact,
        string factId,
        string expected) => exact.facts.Any(value => value != null
            && string.Equals(value.factId, factId, StringComparison.Ordinal)
            && string.Equals(value.value, expected, StringComparison.Ordinal));

    private static bool HasCausalSynthesisMetric(
        GameplayOutcomeSnapshot exact,
        string metricId,
        double expected) => exact.metrics.Any(value => value != null
            && string.Equals(value.metricId, metricId, StringComparison.Ordinal)
            && Math.Abs(value.value - expected) < 0.0001d);

    private static JObject ParseCausalSynthesisJson(
        string json,
        string label)
    {
        try
        {
            return JObject.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Could not serialize facility synthesis " + label + ".",
                exception);
        }
    }

    private static void RequireCausalSynthesisFields(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            throw new InvalidOperationException(
                label + " fields mismatch. actual="
                + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
