using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

using static CropPlotOperationIdentity;

internal sealed class CropPlotHarvestRuntime
{
    internal const string OutputOwnerDomainId = "economy.crop-harvest";
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICropEcologyHarvestTransactionService ecologyHarvests;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;
    private readonly IProductionDomainOutputPublicationService outputPublication;
    private readonly ICharacterPerformanceDefinitionMaximumQuery performanceMaximum;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IGameClock gameClock;
    private readonly IGameCalendar calendar;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly ICharacterPerformanceQuery performance;
    private readonly CropPlotFactPublication factPublication;

    internal CropPlotHarvestRuntime(CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterPerformanceQuery performance,
        CropPlotFactPublication factPublication)
    {
        this.aggregateRootStore = aggregateRootStore;
        ecologyHarvests = world.EcologyHarvests;
        outputCapabilities = world.OutputCapabilities;
        outputPublication = world.OutputPublication;
        performanceMaximum = world.PerformanceMaximum;
        effectBounds = world.EffectBounds;
        facilities = world.Facilities;
        gameClock = simulation.GameClock;
        calendar = simulation.Calendar;
        grandProjectBenefits = simulation.GrandProjectBenefits;
        extremeTraits = simulation.ExtremeTraits;
        runSeedProvider = simulation.RunSeedProvider;
        this.performance = performance;
        this.factPublication = factPublication;
    }

    private CropPlotAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState());
    private Dictionary<BuildingInstanceId, CropPlotState> states => aggregateState.States;

    internal IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
        CapturePendingOutputOwners() => states.Values
        .Where(state => state?.PendingHarvest != null
            && (state.PendingHarvest.phase ==
                    CropHarvestOutputPhase.OutputCommitted
                && state.PendingHarvest.outputPublication is
                    { outputAcknowledged: false }
                || state.PendingHarvest.phase ==
                    CropHarvestOutputPhase.OutputRestoredAwaitingFinalization
                && state.PendingHarvest.outputPublication is
                    {
                        outputAcknowledged: true,
                        restoredInCurrentTransaction: true
                    }))
        .OrderBy(state => state.PendingHarvest.operationId, StringComparer.Ordinal)
        .Select(state => new ProductionDomainOutputRestoreOwnerSnapshot(
            state.PendingHarvest.operationId,
            state.PendingHarvest.outputPublication,
            CaptureHarvestMaximumMassClaims(state.PendingHarvest)))
        .ToArray();

    internal IReadOnlyList<ProductionDomainOutputFacilityOwnerSnapshot>
        CaptureActiveOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid crop-plot facility ID is required.",
                nameof(facilityId));
        return states.Values
            .Where(state => state != null
                && state.PlotId.Equals(facilityId)
                && state.PendingHarvest != null
                && state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            .OrderBy(
                state => state.PendingHarvest.operationId,
                StringComparer.Ordinal)
            .Select(state =>
            {
                ValidateActiveHarvestOwner(state);
                return new ProductionDomainOutputFacilityOwnerSnapshot(
                    OutputOwnerDomainId,
                    state.PendingHarvest.operationId,
                    facilityId,
                    CaptureHarvestLifecycleFingerprint(state));
            })
            .ToArray();
    }

    internal bool FreezeHarvestOutcome(
        CropPlotState state,
        CropDefinitionSO crop,
        CharacterActor worker)
    {
        if (state.PendingHarvest.phase != CropHarvestOutputPhase.None)
            return true;
        string operationId = FormatHarvestOperationId(
            state.PlotId,
            state.NextHarvestOperationSequence);
        float extremeYieldMultiplier = 1f;
        float extremeSeedMultiplier = 1f;
        string[] yieldConditions = Array.Empty<string>();
        GoldenHarvestPreparedResolution golden = default;
        bool expectsGolden = !string.IsNullOrWhiteSpace(
            state.GoldenHarvestHarvesterId);
        bool goldenPrepared = expectsGolden
            && extremeTraits != null
            && runSeedProvider != null
            && worker != null
            && string.Equals(
                state.GoldenHarvestHarvesterId,
                worker.Identity?.PersistentId,
                StringComparison.Ordinal)
            && extremeTraits.TryPrepareGoldenHarvest(
                worker,
                state.PlotId.Value,
                operationId,
                unchecked((ulong)(uint)runSeedProvider.RunSeed),
                gameClock.Time,
                out golden);
        if (expectsGolden && !goldenPrepared)
            return false;
        if (goldenPrepared)
        {
            if (golden.Resolution.Outcome == ExtremeRiskOutcome.Jackpot)
            {
                yieldConditions = new[] { "state:golden-harvest-jackpot" };
                extremeSeedMultiplier = worker.GetDetailedStatMultiplier(
                    CropHarvestOutputRules.SeedYieldEffectTargetId,
                    yieldConditions);
            }
            else
            {
                extremeYieldMultiplier = golden.Resolution.PrimaryMultiplier;
                extremeSeedMultiplier = golden.Resolution.SecondaryMultiplier;
            }
        }

        bool ecologyPreparedOwned = false;
        try
        {
        CropEcologyPreparedHarvestSnapshot ecologyPrepared = ecologyHarvests
            .PrepareHarvest(operationId, state.PlotId.Value);
        ecologyPreparedOwned = true;
        float workerYieldMultiplier = 1f;
        if (worker != null)
        {
            CharacterPerformanceSnapshot yield = performance.Evaluate(
                worker,
                CropHarvestOutputRules.PerformanceFormulaId,
                new CharacterPerformanceEvaluationContext
                {
                    GameplayEffectContext = new GameplayEffectContext(
                        yieldConditions)
                });
            if (!yield.IsApplicable)
                throw new InvalidOperationException(
                    yield.Failure?.Message ?? "Harvest yield is unavailable.");
            workerYieldMultiplier = yield.Value;
        }
        bool indoor = state.Ability != null && state.Ability.Indoor;
        float outputMultiplier = indoor
            ? grandProjectBenefits.GetProductionOutputMultiplier("crop-indoor")
            : 1f;
        int primaryQuantityBeforeSeasonalLoss =
            CropHarvestOutputRules.ResolveHarvestQuantity(
            crop.Yield,
            outputMultiplier,
            workerYieldMultiplier,
            extremeYieldMultiplier,
            ecologyPrepared.Result.YieldMultiplier,
            facilities.FindOperational(ResearchFacilityCommandKind.SoilDiagnostics).Count > 0);
        CropSeasonalYieldDamageSaveData seasonalDamage =
            state.SeasonalYieldDamage
            ?? throw new InvalidOperationException(
                "Crop harvest has no seasonal yield-damage owner.");
        if (!seasonalDamage.IsEmpty)
            ValidateSeasonalYieldDamage(
                seasonalDamage,
                state.CropId,
                state.CycleExecutionReceipt,
                aggregateState.HandledSeasonalStartInstanceIds);
        int seasonalLossPercent = seasonalDamage.IsEmpty
            ? 0
            : seasonalDamage.primaryBatchLossPercent;
        int seasonalLossQuantity =
            CropHarvestOutputRules.ResolvePrimaryBatchLoss(
                primaryQuantityBeforeSeasonalLoss,
                seasonalLossPercent);
        int harvestQuantity = CropHarvestOutputRules.ApplyPrimaryBatchLoss(
            primaryQuantityBeforeSeasonalLoss,
            seasonalLossPercent);
        int seedQuantity = CropHarvestOutputRules.ResolveReturnedSeedQuantity(
            ecologyPrepared.Result.ReturnedSeedCount,
            extremeSeedMultiplier,
            facilities.FindOperational(ResearchFacilityCommandKind.SeedSelection).Count > 0);
        string harvesterId = worker?.Identity?.PersistentId ?? string.Empty;
        int completionAbsoluteDay = calendar.Day;
        string harvestLineId = CropHarvestOutputMaximumAuthority
            .HarvestOutputLineId(crop.CropId);
        string seedLineId = CropHarvestOutputMaximumAuthority
            .SeedOutputLineId(crop.CropId);
        ProductionOutputCapabilityDescriptor harvestCapability =
            outputCapabilities.CaptureDeclaredDescriptor(
                harvestLineId,
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seedCapability =
            outputCapabilities.CaptureDeclaredDescriptor(
                seedLineId,
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        CropHarvestOutputSaveData pending = new()
        {
            phase = CropHarvestOutputPhase.Frozen,
            operationSequence = state.NextHarvestOperationSequence,
            operationId = operationId,
            cropId = crop.CropId,
            indoor = indoor,
            harvesterId = harvesterId,
            outcomeId = goldenPrepared
                ? golden.Resolution.Outcome.ToString().ToLowerInvariant()
                : "normal",
            ecologyOutcomeFingerprint = ecologyPrepared.OutcomeFingerprint,
            ecologyBeforeFingerprint = ecologyPrepared.PlotBeforeFingerprint,
            ecologyAfterFingerprint = ecologyPrepared.PlotAfterFingerprint,
            goldenPrepared = goldenPrepared,
            goldenTraitDefinitionId = goldenPrepared
                ? golden.TraitDefinitionId
                : string.Empty,
            goldenOutcomeFingerprint = goldenPrepared
                ? golden.Fingerprint
                : string.Empty,
            goldenOutcome = goldenPrepared
                ? golden.Resolution.Outcome
                : ExtremeRiskOutcome.Normal,
            goldenPrimaryMultiplier = goldenPrepared
                ? golden.Resolution.PrimaryMultiplier
                : 1f,
            goldenSecondaryMultiplier = goldenPrepared
                ? golden.Resolution.SecondaryMultiplier
                : 1f,
            goldenRollHash = goldenPrepared
                ? golden.Resolution.FixedRollHash
                : 0UL,
            completionAbsoluteDay = completionAbsoluteDay,
            harvestItemId = crop.HarvestItemId,
            harvestQuantity = harvestQuantity,
            seedItemId = crop.SeedItemId,
            seedQuantity = seedQuantity,
            returnedSeedLot = ecologyPrepared.Result.ReturnedSeedLot.Clone(),
            maximumHarvestQuantity = CropHarvestOutputMaximumAuthority
                .ResolveMaximumHarvestQuantity(
                    crop,
                    indoor,
                    performanceMaximum),
            maximumSeedQuantity = CropHarvestOutputMaximumAuthority
                .ResolveMaximumReturnedSeedQuantity(effectBounds),
            seasonalDamageSourceEventInstanceId = seasonalDamage.IsEmpty
                ? string.Empty
                : seasonalDamage.sourceEventInstanceId,
            seasonalDamageSourceDefinitionId = seasonalDamage.IsEmpty
                ? string.Empty
                : seasonalDamage.sourceDefinitionId,
            seasonalPrimaryBatchLossPercent = seasonalLossPercent,
            primaryQuantityBeforeSeasonalLoss = seasonalDamage.IsEmpty
                ? 0
                : primaryQuantityBeforeSeasonalLoss,
            seasonalPrimaryLossQuantity = seasonalDamage.IsEmpty
                ? 0
                : seasonalLossQuantity,
            harvestCapability = ProductionOutputCapabilitySaveData.Freeze(
                harvestCapability),
            seedCapability = ProductionOutputCapabilitySaveData.Freeze(
                seedCapability),
            outputPublication = new ProductionDomainOutputPublicationSaveData()
        };
        if (pending.harvestQuantity > pending.maximumHarvestQuantity
            || pending.seedQuantity > pending.maximumSeedQuantity)
            throw new InvalidOperationException(
                "Crop harvest actual output exceeds its authored maximum proof.");
        state.PendingHarvest = pending;
        return true;
        }
        catch (Exception error)
        {
            List<Exception> compensationFailures = new();
            if (ecologyPreparedOwned
                && !ecologyHarvests.AbortPreparedHarvest(operationId))
                compensationFailures.Add(new InvalidOperationException(
                    "Crop ecology prepared harvest compensation failed."));
            if (goldenPrepared
                && (extremeTraits == null
                    || !extremeTraits.TryAbortPreparedGoldenHarvest(
                        golden.CharacterId,
                        golden.TraitDefinitionId,
                        operationId)))
                compensationFailures.Add(new InvalidOperationException(
                    "Golden Harvest prepared resolution compensation failed."));
            if (compensationFailures.Count == 0)
                throw;
            compensationFailures.Insert(0, error);
            throw new AggregateException(
                "Crop harvest freeze failed and compensation was incomplete.",
                compensationFailures);
        }
    }

    internal bool TryPublishPendingHarvest(
        CropPlotState state,
        CropDefinitionSO crop,
        out CropCycleExecutionReceiptSaveData terminalReceipt)
    {
        terminalReceipt = null;
        CropHarvestOutputSaveData pending = state.PendingHarvest;
        if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            return false;
        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                pending.outputPublication,
                out _))
        {
            ProductionDomainOutputPublicationResult publicationResult =
                outputPublication.EnsureCommitted(
                    pending.outputPublication,
                    CreateHarvestOutputPlan(state, pending));
            if (publicationResult.Status ==
                ProductionDomainOutputPublicationStatus.Conflict)
                throw new InvalidOperationException(
                    "Crop harvest output publication conflicted: "
                    + publicationResult.FailureReason);
            if (!publicationResult.IsCommitted)
            {
                state.BlockedReason = publicationResult.FailureReason;
                return false;
            }
            pending.phase = CropHarvestOutputPhase.OutputCommitted;
        }

        if (!pending.ecologyCommitted)
        {
            CropEcologyPreparedHarvestSnapshot committed = ecologyHarvests
                .CommitPreparedHarvest(pending.operationId);
            if (!string.Equals(
                    committed.OutcomeFingerprint,
                    pending.ecologyOutcomeFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop harvest ecology receipt drifted before commit.");
            pending.ecologyCommitted = true;
        }
        if (pending.goldenPrepared && !pending.goldenCommitted)
        {
            if (extremeTraits == null
                || !extremeTraits.TryCommitPreparedGoldenHarvest(
                    pending.harvesterId,
                    pending.goldenTraitDefinitionId,
                    pending.operationId,
                    out GoldenHarvestPreparedResolution committed)
                || !string.Equals(
                    committed.Fingerprint,
                    pending.goldenOutcomeFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop harvest Golden Harvest receipt drifted before commit.");
            pending.goldenCommitted = true;
        }
        if (!pending.outputPublication.outputAcknowledged
            && !outputPublication.TryAcknowledge(
                pending.outputPublication,
                out string acknowledgeFailure))
        {
            state.BlockedReason = acknowledgeFailure;
            return false;
        }
        pending.phase = CropHarvestOutputPhase
            .OutputRestoredAwaitingFinalization;
        // The crop terminal is the authority for the completion reaction. Keep
        // the pending owner until both child and identity are durably applied;
        // a deferred identity retries this same child without publishing output.
        if (pending.terminalReceipt == null)
            throw new InvalidOperationException("Crop terminal snapshot is missing.");
        if (pending.terminalReceipt.IsEmpty)
        {
            pending.terminalReceipt = CropPlanExecutionReceiptAuthority.Complete(
                state.CycleExecutionReceipt, pending, state.CurrentWater);
            pending.terminalPlotDisplayName = CropPlotFactPublication.RequirePlotDisplayName(state.Building);
            pending.terminalCropDisplayName = crop.DisplayName?.Trim() ?? string.Empty;
        }
        terminalReceipt = pending.terminalReceipt;
        CropCycleTerminalFact terminalFact = new(state.PlotId, terminalReceipt,
            pending.terminalPlotDisplayName, pending.terminalCropDisplayName,
            pending.completionAbsoluteDay, checked((long)pending.operationSequence + 1L));
        if (!factPublication.TryDeliverCropFact(terminalFact,
                ref pending.terminalPublicationKey, ref pending.terminalPublicationId,
                ref pending.terminalPublicationFingerprint, out string outcomeFailure))
        {
            state.BlockedReason = outcomeFailure;
            return false;
        }
        pending.cropOutcomeAcknowledged = true;
        if (!pending.completionEventPublished)
        {
            if (!string.IsNullOrEmpty(pending.harvesterId))
            {
                var completionFact = new CropHarvestCompletedFact(state.PlotId, pending, state.CycleExecutionReceipt);
                if (!factPublication.TryDeliverCropFact(completionFact, ref pending.completionPublicationKey,
                    ref pending.completionDeliveryId, ref pending.completionDeliveryFingerprint, out string failure))
                {
                    state.BlockedReason = failure;
                    return false;
                }
            }
            pending.completionEventPublished = true;
        }
        if (!pending.ecologyAcknowledged)
        {
            if (!ecologyHarvests.AcknowledgePreparedHarvest(pending.operationId))
                throw new InvalidOperationException(
                    "Crop harvest ecology receipt acknowledgement failed.");
            pending.ecologyAcknowledged = true;
        }
        if (pending.goldenPrepared && !pending.goldenAcknowledged)
        {
            if (extremeTraits == null
                || !extremeTraits.TryAcknowledgePreparedGoldenHarvest(
                    pending.harvesterId,
                    pending.goldenTraitDefinitionId,
                    pending.operationId))
                throw new InvalidOperationException(
                    "Crop harvest Golden Harvest receipt acknowledgement failed.");
            pending.goldenAcknowledged = true;
        }
        return true;
    }

    private static ProductionDomainOutputPublicationPlan CreateHarvestOutputPlan(
        CropPlotState state,
        CropHarvestOutputSaveData pending)
    {
        List<ProductionDomainOutputLine> lines = new()
        {
            new ProductionDomainOutputLine(
                pending.harvestCapability.outputLineId,
                pending.harvestItemId,
                pending.harvestQuantity,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>(),
                pending.harvestCapability.ToDescriptor())
        };
        if (pending.seedQuantity > 0)
        {
            lines.Add(new ProductionDomainOutputLine(
                pending.seedCapability.outputLineId,
                pending.seedItemId,
                pending.seedQuantity,
                string.Empty,
                new[]
                {
                    SeedLotItemStateCodec.Encode(pending.returnedSeedLot)
                },
                pending.seedCapability.ToDescriptor()));
        }
        return new ProductionDomainOutputPublicationPlan(
            HarvestOutputPublicationOperationPrefix,
            pending.operationId,
            HarvestOutputBatchCommitPrefix + pending.operationId,
            CaptureHarvestOutcomeFingerprint(state.PlotId, pending),
            state.Building,
            lines,
            FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
            ProductionDomainOutputAcknowledgementDisposition
                .ReleaseLooseOrDestination,
            new[]
            {
                new ProductionDomainOutputMaximumMassClaim(
                    pending.harvestCapability.ToDescriptor(),
                    pending.maximumHarvestQuantity),
                new ProductionDomainOutputMaximumMassClaim(
                    pending.seedCapability.ToDescriptor(),
                    pending.maximumSeedQuantity)
            });
    }

    private static IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
        CaptureHarvestMaximumMassClaims(CropHarvestOutputSaveData pending) =>
        new[]
        {
            new ProductionDomainOutputMaximumMassClaim(
                pending.harvestCapability.ToDescriptor(),
                pending.maximumHarvestQuantity),
            new ProductionDomainOutputMaximumMassClaim(
                pending.seedCapability.ToDescriptor(),
                pending.maximumSeedQuantity)
        };

    private static string CaptureHarvestLifecycleFingerprint(
        CropPlotState state)
    {
        CropHarvestOutputSaveData pending = state.PendingHarvest;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-harvest-facility-lifecycle@1");
        digest.Append(state.PlotId.Value);
        digest.Append((int)pending.phase);
        digest.Append(CaptureHarvestOutcomeFingerprint(state.PlotId, pending));
        digest.Append(pending.outputPublication?.batchCommitId ?? string.Empty);
        digest.Append(pending.outputPublication?.plannedOutputFingerprint
            ?? string.Empty);
        return digest.ComputeSha256();
    }

    private static void ValidateActiveHarvestOwner(CropPlotState state)
    {
        CropHarvestOutputSaveData pending = state?.PendingHarvest;
        if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            throw new InvalidOperationException(
                "Crop harvest lifecycle owner is empty.");
        if (pending.phase == CropHarvestOutputPhase.Frozen)
        {
            if (pending.outputPublication == null
                || !pending.outputPublication.IsEmpty)
                throw new InvalidOperationException(
                    "Frozen crop harvest already contains publication provenance.");
            return;
        }
        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                pending.outputPublication,
                out string failureReason)
            || pending.outputPublication.acknowledgementDisposition !=
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination
            || pending.phase == CropHarvestOutputPhase.OutputCommitted
                && pending.outputPublication.outputAcknowledged
            || pending.phase == CropHarvestOutputPhase
                    .OutputRestoredAwaitingFinalization
                && !pending.outputPublication.outputAcknowledged)
        {
            throw new InvalidOperationException(
                "Crop harvest active output owner is invalid: "
                + (pending.operationId ?? string.Empty) + ":" + failureReason);
        }
    }
}
