#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class EnvironmentalWorkwearPlannedOutputDebugScenarios
{
    [MenuItem("DungeonStory/V27/Physical Mass/Verify Workwear Planned Output")]
    public static void RunAll()
    {
        VerifyExactPublicationReplayAndAcknowledgement();
        VerifyDestinationMismatchDoesNotPublish();
        VerifyCapacityFailureDoesNotPublish();
        Debug.Log(
            "[V27][PhysicalMass] Environmental workwear planned-output scenarios PASS.");
    }

    private static void VerifyExactPublicationReplayAndAcknowledgement()
    {
        using Fixture fixture = new();
        ProductionOutputContext context = fixture.CreateContext(
            amount: 2,
            commitId: "production-output:qa:workwear:001");

        Require(
            fixture.Handler.TryProduceIdempotent(
                context,
                out DomainFailure firstFailure),
            "Workwear planned publication failed: " + Format(firstFailure));
        Require(
            fixture.Handler.TryCaptureCommittedOutput(
                context,
                out ProductionCommittedOutputSnapshot committed,
                out DomainFailure snapshotFailure)
            && committed != null
            && committed.ExactMassGrams == Fixture.UnitMassGrams * 2L
            && committed.Stacks.Count == 2
            && committed.Stacks.All(stack => string.Equals(
                stack.OutputLineId,
                context.OutputLineId,
                StringComparison.Ordinal)),
            "Workwear committed-output snapshot was not exact: "
            + Format(snapshotFailure));

        IReadOnlyList<WorldItemStackSnapshot> first = fixture.Query.GetAllStacks();
        Require(
            first.Count == 2
            && first.All(stack =>
                stack.ItemId == Fixture.WorkwearItemId
                && stack.Quantity == 1
                && stack.State == WorldItemStackState.FacilityOutputBuffer
                && stack.DestinationId == fixture.DestinationId
                && stack.Position == Fixture.Position
                && ((ItemInstanceId)stack.ItemInstanceId).IsValid
                && stack.Components.Count(component => component != null
                    && component.componentTypeId == ItemInstanceComponentIds.Apparel) == 1
                && !ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    context.CommitId))
            && first.Select(stack => stack.ItemInstanceId)
                .Distinct(StringComparer.Ordinal).Count() == 2,
            "Workwear publication did not create two exact unique apparel stacks.");
        Require(
            first.Select(stack =>
                ReadApparel(stack).deterministicBatchHash)
                .Distinct().Count() == 2,
            "Workwear output units reused the same deterministic batch identity.");
        Require(
            fixture.Publication.TryCaptureBatch(
                context.CommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out bool acknowledged,
                out _,
                out string captureFailure)
            && !acknowledged
            && batch.TotalQuantity == 2
            && batch.TotalMassGrams == Fixture.UnitMassGrams * 2L
            && batch.Stacks.All(stack => stack.OutputLineId.StartsWith(
                context.OutputLineId + ":unit:",
                StringComparison.Ordinal))
            && batch.Stacks.Select(stack => stack.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() == 2
            && fixture.Publication.CapturePendingRestoreBatchesForEditorTest()
                .Count == 1,
            "Workwear publication did not remain pending for its bill owner: "
                + captureFailure);

        Require(
            fixture.Handler.TryAcknowledge(
                context.CommitId,
                out DomainFailure firstAcknowledgementFailure),
            "Workwear explicit acknowledgement failed: "
            + Format(firstAcknowledgementFailure));
        Require(
            fixture.Publication.TryCaptureBatch(
                context.CommitId,
                allowAcknowledged: true,
                out _,
                out bool confirmedAcknowledged,
                out _,
                out string confirmationFailure)
            && confirmedAcknowledged
            && fixture.Publication.CapturePendingRestoreBatchesForEditorTest()
                .Count == 0,
            "Workwear explicit acknowledgement was not durable: "
            + confirmationFailure);

        string[] firstStackIds = first.Select(stack => stack.StackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        ulong[] firstHashes = first.Select(stack =>
                ReadApparel(stack).deterministicBatchHash)
            .OrderBy(value => value)
            .ToArray();
        fixture.Clock.TimeValue += GameCalendarRules.SecondsPerDay * 9f;
        Require(
            fixture.Handler.TryProduceIdempotent(
                context,
                out DomainFailure replayFailure),
            "Workwear replay failed: " + Format(replayFailure));
        Require(
            fixture.Handler.TryAcknowledge(
                context.CommitId,
                out DomainFailure acknowledgementFailure),
            "Workwear acknowledgement replay failed: "
            + Format(acknowledgementFailure));

        IReadOnlyList<WorldItemStackSnapshot> replay = fixture.Query.GetAllStacks();
        Require(
            replay.Count == 2
            && replay.Select(stack => stack.StackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(firstStackIds, StringComparer.Ordinal)
            && replay.Select(stack => ReadApparel(stack).deterministicBatchHash)
                .OrderBy(value => value)
                .SequenceEqual(firstHashes),
            "Workwear replay allocated a new stack, instance, or authored state.");

        ProductionOutputContext conflicting = fixture.CreateContext(
            amount: 2,
            commitId: context.CommitId,
            qualityModifier: 0.8f);
        Require(
            !fixture.Handler.TryProduceIdempotent(
                conflicting,
                out DomainFailure conflictFailure)
            && conflictFailure.IsFailure
            && fixture.Query.GetAllStacks().Count == 2,
            "A changed workwear request reused an unrelated durable commit.");
    }

    private static void VerifyDestinationMismatchDoesNotPublish()
    {
        using Fixture fixture = new();
        ProductionOutputContext invalid = fixture.CreateContext(
            amount: 1,
            commitId: "production-output:qa:workwear:destination-mismatch",
            destinationId: "production-output:wrong-facility");
        Require(
            !fixture.Handler.TryProduceIdempotent(
                invalid,
                out DomainFailure failure)
            && failure.IsFailure
            && fixture.Query.GetAllStacks().Count == 0
            && fixture.Publication.CapturePendingRestoreBatchesForEditorTest()
                .Count == 0,
            "A mismatched workwear destination mutated physical output authority.");
    }

    private static void VerifyCapacityFailureDoesNotPublish()
    {
        using Fixture fixture = new(maxCapacityGrams: 1_000L);
        ProductionOutputContext context = fixture.CreateContext(
            amount: 1,
            commitId: "production-output:qa:workwear:capacity");
        Require(
            !fixture.Handler.TryProduceIdempotent(
                context,
                out DomainFailure failure)
            && failure.Code == FailureCode.ProductionOutputSpaceUnavailable
            && fixture.Query.GetAllStacks().Count == 0
            && fixture.Publication.CapturePendingRestoreBatchesForEditorTest()
                .Count == 0,
            "A capacity-blocked workwear output leaked a stack or publication.");
    }

    private static ApparelInstanceState ReadApparel(WorldItemStackSnapshot stack)
    {
        Require(
            ApparelItemStateCodec.TryRead(
                stack.Components,
                out ApparelInstanceState state)
            && state != null,
            "Published workwear apparel state could not be decoded.");
        return state;
    }

    private static string Format(DomainFailure failure) =>
        failure.IsFailure
            ? failure.Code + "/" + string.Join("/", failure.Parameters.ToArray())
            : "none";

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        internal const string WorkwearItemId = "item:qa:workwear";
        internal const string TextileItemId = "item:qa:textile";
        internal const long UnitMassGrams = 1_150L;
        internal static readonly Vector2Int Position = new(12, 7);
        private static readonly BuildingInstanceId FacilityId =
            (BuildingInstanceId)"building:qa:workwear";

        private readonly GameObject facilityObject;
        private readonly ApparelDefinitionSO apparel;
        private readonly TextileMaterialDefinitionSO material;
        private readonly ProductionRecipeSO recipe;

        internal Fixture(long maxCapacityGrams = 10_000L)
        {
            facilityObject = new GameObject("V27 Workwear Output Fixture");
            BuildableObject buildable = facilityObject.AddComponent<BuildableObject>();
            apparel = ScriptableObject.CreateInstance<ApparelDefinitionSO>();
            apparel.Configure(
                "apparel:qa:workwear",
                WorkwearItemId,
                "QA Workwear",
                "",
                ApparelBodyForm.Humanoid,
                ApparelLayer.Accessory,
                ApparelFitMode.Adjustable,
                AnatomyAttachmentPoint.Torso,
                AnatomyAttachmentPoint.Torso,
                AnatomyAttachmentPoint.None,
                ApparelModificationKind.None,
                ApparelUseTag.Work,
                TextileMaterialTag.Woven | TextileMaterialTag.Plant,
                1f,
                1.15f,
                "");
            material = ScriptableObject.CreateInstance<TextileMaterialDefinitionSO>();
            material.Configure(
                "textile:qa:plant",
                TextileItemId,
                "QA Textile",
                "",
                TextileMaterialTag.Woven | TextileMaterialTag.Plant,
                0.4f,
                0.3f,
                0.2f,
                0.1f,
                0.1f,
                50f,
                1f,
                1f,
                "");
            recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
            recipe.Configure(
                "recipe:qa:workwear",
                "QA Workwear",
                "",
                "facility:qa:tailoring",
                "work:craft",
                "",
                1f,
                new[] { new ItemAmountDefinition(TextileItemId, 2) },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:qa/workwear",
                        ProductionOutputRole.Main,
                        WorkwearItemId,
                        1,
                        1f)
                });

            FixedCatalog catalog = new();
            FixedMassQuery mass = new();
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            Query = new WorldItemQueryService(
                catalog,
                mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            FacilityBufferDestinationClaimRegistry claims = new();
            DestinationId = ProductionOutputDestinationId
                .FromFacility(FacilityId)
                .Value;
            Require(
                claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        DestinationId,
                        Position,
                        ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                        DestinationId,
                        FacilityId.Value,
                        FacilityBufferDestinationAnchorKind.LiveBuilding),
                    out _,
                    out string claimFailure),
                "Workwear fixture destination claim failed: " + claimFailure);
            Admission = new FacilityBufferMassAdmissionService(
                claims,
                new EmptyOccupancy(),
                mass);
            FacilityBufferCapacityProfile profile = new(
                DestinationId,
                Position,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                DestinationId,
                FacilityId.Value,
                new PhysicalMassGrams(maxCapacityGrams),
                1L);
            Require(
                Admission.TryReplaceOwnedProfiles(
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                    new[] { profile },
                    out _,
                    out string profileFailure),
                "Workwear fixture capacity failed: " + profileFailure);
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                catalog,
                mass,
                Admission);
            Clock = new MutableClock();
            ProductionFacilityHandle handle = new(
                buildable,
                FacilityId,
                Position,
                false,
                string.Empty,
                false,
                Vector2Int.zero,
                "building:qa:workwear",
                "facility:qa:tailoring",
                2);
            Handler = new EnvironmentalWorkwearProductionOutputHandler(
                new FixedApparelCatalog(apparel),
                new FixedMaterialCatalog(material),
                Repository,
                mass,
                Clock,
                new FixedFacilityQuery(handle),
                new FixedDestinationAuthority(profile),
                new FixedCapacityProjector(),
                Admission,
                Publication,
                new ProductionOutputMaximumMassRegistry(
                    new IProductionOutputMaximumMassCapability[]
                    {
                        new ApparelFixtureStandardOutputHandler(),
                        new EnvironmentalWorkwearProductionOutputMaximumMassCapability(
                            new FixedApparelCatalog(apparel))
                    },
                    mass));
        }

        internal string DestinationId { get; }
        internal MutableClock Clock { get; }
        internal WorldItemRepository Repository { get; }
        internal WorldItemQueryService Query { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal EnvironmentalWorkwearProductionOutputHandler Handler { get; }

        internal ProductionOutputContext CreateContext(
            int amount,
            string commitId,
            string destinationId = null,
            float qualityModifier = 0.2f) => new(
            recipe,
            facilityObject.GetComponent<BuildableObject>(),
            null,
            "output:qa/workwear",
            WorkwearItemId,
            amount,
            destinationId ?? DestinationId,
            qualityModifier: qualityModifier,
            workerQuality: 0.7f,
            commitId: commitId);

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(apparel);
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private sealed class FixedApparelCatalog : IApparelDefinitionCatalog
    {
        private readonly ApparelDefinitionSO definition;
        internal FixedApparelCatalog(ApparelDefinitionSO definition) =>
            this.definition = definition;
        public IReadOnlyList<ApparelDefinitionSO> Definitions =>
            new[] { definition };
        public bool TryGet(string apparelId, out ApparelDefinitionSO value)
        {
            value = apparelId == definition.ApparelId ? definition : null;
            return value != null;
        }
        public bool TryGetByItemId(string itemId, out ApparelDefinitionSO value)
        {
            value = itemId == definition.PhysicalItemId ? definition : null;
            return value != null;
        }
        public int GetIndex(string apparelId) =>
            apparelId == definition.ApparelId ? 0 : -1;
    }

    private sealed class FixedMaterialCatalog : ITextileMaterialCatalog
    {
        private readonly TextileMaterialDefinitionSO definition;
        internal FixedMaterialCatalog(TextileMaterialDefinitionSO definition) =>
            this.definition = definition;
        public IReadOnlyList<TextileMaterialDefinitionSO> Definitions =>
            new[] { definition };
        public bool TryGet(string materialId, out TextileMaterialDefinitionSO value)
        {
            value = materialId == definition.MaterialId ? definition : null;
            return value != null;
        }
        public bool TryGetByItemId(
            string itemId,
            out TextileMaterialDefinitionSO value)
        {
            value = itemId == definition.PhysicalItemId ? definition : null;
            return value != null;
        }
        public int GetIndex(string materialId) =>
            materialId == definition.MaterialId ? 0 : -1;
    }

    private sealed class FixedCatalog : IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition definition = new(
            Fixture.WorkwearItemId,
            "QA Workwear",
            string.Empty,
            StockCategory.General,
            1,
            null,
            1.15f,
            1);
        public IReadOnlyList<DungeonItemDefinition> All => new[] { definition };
        public DungeonItemDefinition GetDefinition(string itemId) =>
            itemId == definition.ItemId
                ? definition
                : throw new KeyNotFoundException(itemId);
        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition value)
        {
            value = itemId == definition.ItemId ? definition : null;
            return value != null;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new PhysicalMassGrams(Fixture.UnitMassGrams)
            .Multiply(quantity);
    }

    private sealed class EmptyOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);
        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "not-used";
            return false;
        }
    }

    private sealed class FixedFacilityQuery : IProductionFacilityHandleQuery
    {
        private readonly ProductionFacilityHandle handle;
        internal FixedFacilityQuery(ProductionFacilityHandle handle) =>
            this.handle = handle;
        public ProductionFacilityHandle CaptureFacility(object runtimeObject) =>
            ReferenceEquals(runtimeObject, handle.RuntimeObject)
                ? handle
                : throw new InvalidOperationException("fixture-facility-mismatch");
    }

    private sealed class FixedCapacityProjector :
        IProductionOutputBufferCapacityProjector
    {
        public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
            ProductionFacilityHandle facility,
            ProductionOutputBatchMaximumMassProof maximumMassProof) => new(
            facility.OutputBufferCycleCapacity,
            maximumMassProof.MaximumBatchMassGrams,
            maximumMassProof.MaximumBatchMassGrams
                * facility.OutputBufferCycleCapacity,
            maximumMassProof.MaximumBatchMassGrams
                * facility.OutputBufferCycleCapacity,
            maximumMassProof.MaximumBatchMassGrams
                * facility.OutputBufferCycleCapacity,
            new string('a', 64));
    }

    private sealed class FixedDestinationAuthority :
        IProductionOutputDestinationAuthorityRuntime
    {
        private readonly FacilityBufferCapacityProfile profile;
        internal FixedDestinationAuthority(FacilityBufferCapacityProfile profile) =>
            this.profile = profile;
        public bool TryEnsure(
            ProductionFacilityHandle facility,
            long minimumMassCapacityGrams,
            out FacilityBufferCapacityProfile value,
            out string failureReason)
        {
            value = profile;
            failureReason = string.Empty;
            return minimumMassCapacityGrams <= profile.MaxMassGrams;
        }
        public bool TryValidate(
            ProductionFacilityHandle facility,
            out FacilityBufferCapacityProfile value,
            out string failureReason)
        {
            value = profile;
            failureReason = string.Empty;
            return true;
        }
        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryRevoke(
            BuildingInstanceId facilityId,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    internal sealed class MutableClock : IGameClock
    {
        internal float TimeValue { get; set; }
        public float DeltaTime => 0f;
        public float Time => TimeValue;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
#endif
