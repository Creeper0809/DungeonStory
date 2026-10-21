#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RegionalSupplyContractTransferOutboxDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string ContractId = "contract:1:1";
    private static readonly string DestinationId =
        EconomyProjectInputOwnerAuthority
            .BuildRegionalContractDestinationId(ContractId);

    [MenuItem("DungeonStory/Debug/Economy/Run Regional Supply Transfer Outbox Contracts")]
    public static void RunAll()
    {
        Debug.Log(Verify());
    }

    public static void RunT07FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_T07_REGIONAL_SUPPLY_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_T07_REGIONAL_SUPPLY_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "T07 regional-supply report must be a create-only path under Artifacts/QA.");
        }

        VerifyGameplayOutcomeParent();
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "Report directory is unavailable."));
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_T07_REGIONAL_SUPPLY=PASS\n"
            + "status=accept,decline,deadline-failed,completed\n"
            + "recovery=reward-committed,ack-failed,json-restore,ack-only\n"
            + "physical=quantity2,mass-preserved,child-once,outcome-once,reward-once\n"
            + "restore=valid-pending,missing-receipt-rejected,orphan-receipt-rejected,mass-tamper-rejected\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_T07_REGIONAL_SUPPLY=PASS; report=" + fullPath);
    }

    public static string Verify()
    {
        MigratedProducerOutcomeEditorFixture outcomes = new(
            "regional-supply-t07-run");
        MigratedProducerOutcomeProbeTransaction outcomeProbe = new(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind
                .RegionalSupplyContractResult
        };
        RegionalSupplyContractOutcomeBridge outcomePort = new(outcomeProbe);
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        PhysicalFacilityItemSinkGateway gateway = new(
            new PhysicalStockQuery(repository, catalog, massQuery),
            dispositions);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            quantity: 3,
            state: WorldItemStackState.FacilityBuffer,
            destinationId: DestinationId,
            position: new Vector2Int(4, 2));

        string operationId =
            RegionalSupplyContractDeliveryOutbox.FormatOperationId(ContractId);
        Require(gateway.TryCommitTransferPending(
                DestinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ItemId] = 2
                },
                operationId,
                RegionalSupplyContractDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt physicalReceipt,
                out string commitFailure),
            "Regional supply physical transfer failed: " + commitFailure);

        EconomyProjectInputOwnerFixtureAuthority contractInputOwners =
            new(massQuery);
        RegionalSupplyContractState contract = CreateContract(
            contractInputOwners.Runtime);
        RegionalSupplyContractDeliveryOutbox.RecordPending(
            contract,
            ToContractReceipt(physicalReceipt));
        PhysicalItemRestoreCandidateDispositionSnapshot candidateReceipt =
            ToCandidateReceipt(physicalReceipt);
        DungeonRegionalSupplyContractSaveData pendingPayload = new()
        {
            contracts = new List<RegionalSupplyContractState>
            {
                contract.Clone()
            }
        };
        RegionalSupplyContractSaveSection.ValidatePhysicalRestoreCandidate(
            pendingPayload,
            new CandidateQuery(candidateReceipt));
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        pendingPayload,
                        CandidateQuery.Empty)),
            "Regional supply restore accepted a missing incoming physical Transfer.");
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        new DungeonRegionalSupplyContractSaveData(),
                        new CandidateQuery(candidateReceipt))),
            "Regional supply restore accepted an orphan incoming physical Transfer.");
        PhysicalItemRestoreCandidateDispositionSnapshot mismatchedReceipt = new(
            candidateReceipt.Kind,
            candidateReceipt.OperationId,
            candidateReceipt.ReasonCode,
            candidateReceipt.RequestFingerprint,
            candidateReceipt.SourceStackIds,
            candidateReceipt.Quantity,
            candidateReceipt.InputMassGrams + 1L,
            candidateReceipt.CommitId);
        Require(Throws(() =>
                RegionalSupplyContractSaveSection
                    .ValidatePhysicalRestoreCandidate(
                        pendingPayload,
                        new CandidateQuery(mismatchedReceipt))),
            "Regional supply restore accepted mismatched physical mass provenance.");
        VerifyPhysicalCandidateViewLifetime(candidateReceipt);
        VerifyWholeSaveRegistryJoin(contract, physicalReceipt);
        FailOnceCommandPort commands = new(gateway)
        {
            FailNextAcknowledgement = true
        };

        Require(!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                contract,
                commands,
                outcomePort,
                absoluteDay: 2,
                out _)
            && contract.status == RegionalSupplyContractStatus.Completed
            && contract.deliveryCommitPhase ==
                RegionalSupplyDeliveryCommitPhase.OutcomeCommitted
            && !string.IsNullOrWhiteSpace(
                contract.deliveryOutcomeResultKey)
            && commands.IncomePublicationCount == 1
            && commands.PublishedIncome == contract.rewardGold
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Acknowledgement fault did not retain exact transfer/reward outbox state.");
        _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
            outcomes,
            MigratedProducerOutcomeKind.RegionalSupplyContractResult,
            outcomeProbe);

        RegionalSupplyContractState restored =
            JsonUtility.FromJson<RegionalSupplyContractState>(
                JsonUtility.ToJson(contract));
        RegionalSupplyContractState tampered = restored.Clone();
        tampered.deliveryMassGrams++;
        Require(!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                tampered,
                commands,
                outcomePort,
                absoluteDay: 2,
                out _)
            && commands.IncomePublicationCount == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Tampered regional-supply provenance mutated physical or reward authority.");

        Require(RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                restored,
                commands,
                outcomePort,
                absoluteDay: 2,
                out string recoveryFailure),
            "Regional supply acknowledgement-only recovery failed: "
            + recoveryFailure);
        Require(restored.status == RegionalSupplyContractStatus.Completed
            && RegionalSupplyContractDeliveryOutbox.HasCanonicalEmpty(restored)
            && commands.IncomePublicationCount == 1
            && outcomeProbe.CommitCount(
                MigratedProducerOutcomeKind.RegionalSupplyContractResult) == 1
            && commands.SuccessfulAcknowledgements == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Regional supply recovery duplicated income or lost exact transfer quantity.");

        VerifyStatusTransitions();

        return "Regional supply transfer outbox contracts PASS.";
    }

    public static void VerifyGameplayOutcomeParent() => _ = Verify();

    private static void VerifyStatusTransitions()
    {
        using (RegionalStatusScenario scenario = new("regional-status-main"))
        {
            string contractId = scenario.FirstOffered.contractId;
            scenario.Probe.FaultMode =
                MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(!scenario.Runtime.Accept(contractId, out _)
                    && scenario.Find(contractId).status ==
                        RegionalSupplyContractStatus.Offered
                    && !scenario.Find(contractId).inputOwnerActive,
                "Regional acceptance reservation rejection mutated its owner.");

            scenario.Probe.FaultMode =
                MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(!scenario.Runtime.Accept(contractId, out _)
                    && scenario.Find(contractId).status ==
                        RegionalSupplyContractStatus.Offered
                    && !scenario.Find(contractId).inputOwnerActive,
                "Regional acceptance commit rejection did not roll back owner state.");

            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            string beforeTransition = JsonUtility.ToJson(
                scenario.Runtime.Capture());
            Require(scenario.Runtime.Accept(contractId, out _),
                "Regional acceptance did not reach the real runtime entry.");
            string afterTransition = JsonUtility.ToJson(
                scenario.Runtime.Capture());
            RegionalSupplyContractState accepted = scenario.Find(contractId);
            Require(accepted.status == RegionalSupplyContractStatus.Delivering
                    && accepted.inputOwnerActive
                    && !string.IsNullOrWhiteSpace(
                        accepted.acceptanceOutcomeResultKey)
                    && scenario.Probe.CommitCount(
                        MigratedProducerOutcomeKind
                            .RegionalSupplyContractResult) == 1,
                "Regional acceptance lost its exact parent outcome.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                MigratedProducerOutcomeKind.RegionalSupplyContractResult,
                scenario.Probe,
                beforeTransition,
                afterTransition);

            DungeonRegionalSupplyContractSaveData roundTrip =
                JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                    JsonUtility.ToJson(scenario.Runtime.Capture()));
            Require(roundTrip.contracts.Single(value =>
                        string.Equals(value.contractId, contractId,
                            StringComparison.Ordinal))
                    .acceptanceOutcomeResultKey
                    == accepted.acceptanceOutcomeResultKey,
                "Regional acceptance result key did not survive save roundtrip.");

            scenario.Session.RaiseDay(accepted.deadlineDay + 1);
            RegionalSupplyContractState failed = scenario.Find(contractId);
            Require(failed.status == RegionalSupplyContractStatus.Failed
                    && !failed.inputOwnerActive
                    && !string.IsNullOrWhiteSpace(
                        failed.terminalOutcomeResultKey)
                    && scenario.Probe.CommitCount(
                        MigratedProducerOutcomeKind
                            .RegionalSupplyContractResult) == 2,
                "Regional deadline failure lost its terminal parent outcome.");
        }

        using (RegionalStatusScenario scenario = new("regional-status-decline"))
        {
            string contractId = scenario.FirstOffered.contractId;
            Require(scenario.Runtime.Decline(contractId, out _),
                "Regional decline did not reach the real runtime entry.");
            RegionalSupplyContractState declined = scenario.Find(contractId);
            Require(declined.status == RegionalSupplyContractStatus.Declined
                    && !string.IsNullOrWhiteSpace(
                        declined.terminalOutcomeResultKey)
                    && scenario.Probe.CommitCount(
                        MigratedProducerOutcomeKind
                            .RegionalSupplyContractResult) == 1,
                "Regional decline lost its terminal parent outcome.");
        }
    }

    private static RegionalSupplyContractState CreateContract(
        IEconomyProjectInputOwnerPort inputOwners)
    {
        RegionalSupplyContractState contract = new()
        {
            contractId = ContractId,
            title = "QA lumber export",
            regionName = "QA",
            offeredDay = 1,
            deadlineDay = 4,
            rewardGold = 25,
            status = RegionalSupplyContractStatus.Delivering,
            destinationId = DestinationId,
            inputOwnerActive = true,
            inputDestinationX = 4,
            inputDestinationY = 2,
            lastStatus = "delivery",
            requirements = new List<RegionalSupplyContractRequirement>
            {
                new RegionalSupplyContractRequirement
                {
                    itemId = ItemId,
                    amount = 2
                }
            }
        };
        EnsureInputOwner(contract, inputOwners);
        return contract;
    }

    private static void EnsureInputOwner(
        RegionalSupplyContractState contract,
        IEconomyProjectInputOwnerPort inputOwners)
    {
        Dictionary<string, int> requirements = contract.requirements
            .GroupBy(value => value.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.amount),
                StringComparer.Ordinal);
        Require(inputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.RegionalContractDomain,
                contract.contractId,
                contract.destinationId,
                new Vector2Int(
                    contract.inputDestinationX,
                contract.inputDestinationY),
                EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                null,
                requirements,
                contract.inputCapacityGrams,
                contract.inputMassAuthorityRevision,
                contract.inputCapacityFingerprint,
                out EconomyProjectInputOwnerProjection projection,
                out string failureReason),
            "Regional supply exact input-owner staging failed: "
            + failureReason);
        contract.inputCapacityGrams = projection.CapacityGrams;
        contract.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        contract.inputCapacityFingerprint = projection.Fingerprint;
    }

    private static RegionalSupplyDeliveryTransferReceipt ToContractReceipt(
        PhysicalItemBatchDispositionReceipt receipt) => new()
    {
        operationId = receipt.OperationId,
        reasonCode = receipt.ReasonCode,
        commitId = receipt.CommitId,
        sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList(),
        quantity = receipt.Quantity,
        inputMassGrams = receipt.InputMassGrams
    };

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToCandidateReceipt(PhysicalItemBatchDispositionReceipt receipt) => new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "regional-supply-focused-fixture",
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void VerifyPhysicalCandidateViewLifetime(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        WorldItemStackRuntime candidateRuntime =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out IDungeonRestoreTransactionParticipant exactRouteLifetime);
        DungeonPhysicalItemSaveData snapshot = candidateRuntime.Capture();
        snapshot.pendingBatchDispositions.Add(
            new PhysicalItemBatchDispositionSaveData
            {
                kind = (int)receipt.Kind,
                operationId = receipt.OperationId,
                reasonCode = receipt.ReasonCode,
                requestFingerprint = receipt.RequestFingerprint,
                sourceStackIds = receipt.SourceStackIds.ToList(),
                quantity = receipt.Quantity,
                inputMassGrams = receipt.InputMassGrams,
                commitId = receipt.CommitId
            });

        IPhysicalItemRestoreStaging staging = candidateRuntime;
        IPhysicalItemRestoreCandidateQuery query =
            (candidateRuntime as object) as IPhysicalItemRestoreCandidateQuery;
        Require(query != null && !query.IsCandidateAvailable,
            "Physical restore candidate query leaked outside staging.");

        IDungeonSaveRestoreStage discarded = staging.StageTransactionalRestore(
            snapshot,
            EmptyRestoreWorldCandidates.Instance);
        Require(query.IsCandidateAvailable
            && query.TryGetPendingBatchDisposition(
                receipt.OperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot stagedReceipt)
            && stagedReceipt.InputMassGrams == receipt.InputMassGrams,
            "Physical restore candidate query did not expose the detached receipt.");
        ((IDungeonDiscardableSaveRestoreStage)discarded).Discard();
        Require(!query.IsCandidateAvailable,
            "Physical restore candidate query survived stage discard.");

        IDungeonSaveRestoreStage committed = staging.StageTransactionalRestore(
            snapshot,
            EmptyRestoreWorldCandidates.Instance);
        IDungeonRestoreTransactionParticipant lifetime = candidateRuntime;
        exactRouteLifetime.BeginRestoreCandidate();
        lifetime.BeginRestoreCandidate();
        DungeonGameRestoreReport report = new();
        committed.Commit(report);
        Require(report.Success
            && query.IsCandidateAvailable
            && candidateRuntime.TryGetPendingBatchPhysicalDisposition(
                receipt.OperationId,
                out PhysicalItemBatchDispositionReceipt restored)
            && restored.CommitId == receipt.CommitId,
            "Physical restore candidate query did not survive commit until cross-section publication.");
        exactRouteLifetime.PublishRestoreCandidate();
        lifetime.PublishRestoreCandidate();
        exactRouteLifetime.CompleteRestoreCandidate();
        lifetime.CompleteRestoreCandidate();
        Require(!query.IsCandidateAvailable,
            "Physical restore candidate query survived transaction completion.");
    }

    private static void VerifyWholeSaveRegistryJoin(
        RegionalSupplyContractState contract,
        PhysicalItemBatchDispositionReceipt sourceReceipt)
    {
        IDungeonItemCatalogProvider itemCatalog =
            EditorItemCatalogFactory.Create();
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        WorldItemStackRuntime items =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                itemCatalog,
                aggregateRootStore,
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out _,
                out _,
                out IDungeonRestoreTransactionParticipant exactRouteLifetime);
        try
        {
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                ItemId,
                quantity: 3,
                state: WorldItemStackState.FacilityBuffer,
                destinationId: DestinationId,
                position: new Vector2Int(4, 2));
            PhysicalFacilityItemSinkGateway gateway = new(
                new PhysicalStockQuery(
                    repository,
                    itemCatalog,
                    items.MassQuery),
                new PhysicalItemBatchDispositionService(
                    repository,
                    items.MassQuery,
                    EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock()));
            Require(gateway.TryCommitTransferPending(
                    DestinationId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [ItemId] = 2
                    },
                    sourceReceipt.OperationId,
                    RegionalSupplyContractDeliveryOutbox.TransferReason,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string commitFailure),
                "Whole-save regional physical transfer failed: "
                + commitFailure);

            EconomyProjectInputOwnerFixtureAuthority inputOwners =
                new(items.MassQuery);
            RegionalSupplyContractState owner = contract.Clone();
            EnsureInputOwner(owner, inputOwners.Runtime);
            owner.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
            owner.deliveryOperationId = string.Empty;
            owner.deliveryCommitId = string.Empty;
            owner.deliverySourceStackIds.Clear();
            owner.deliveryQuantity = 0;
            owner.deliveryMassGrams = 0L;
            owner.rewardTransactionId = string.Empty;
            owner.rewardBalanceBefore = 0;
            owner.rewardBalanceAfter = 0;
            owner.deliveryOutcomeResultKey = string.Empty;
            RegionalSupplyContractDeliveryOutbox.RecordPending(
                owner,
                ToContractReceipt(receipt));
            DungeonRegionalSupplyContractSaveData regionalPayload = new()
            {
                currentDay = 1,
                nextOfferDay = 4,
                nextSequence = 2,
                contracts = new List<RegionalSupplyContractState>
                {
                    owner
                }
            };
            RegistryRegionalRuntime regionalRuntime = new(regionalPayload);
            IResourceEconomyContentCatalog economyCatalog =
                new ResourceEconomyContentCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()));
            PhysicalItemsSaveSection physicalSection = new(
                items,
                items,
                EmptyRestoreWorldCandidates.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher
                    .IsolatedSectionFixtureOnly);
            RegionalSupplyContractSaveSection regionalSection = new(
                regionalRuntime,
                economyCatalog,
                items,
                inputOwners.RestoreRuntime);
            IDungeonSaveSection[] sections =
            {
                new RegistryDependencyStubSection(
                    ModularFacilityWorldSaveSection.Id,
                    DungeonSaveRestorePhase.World),
                new RegistryDependencyStubSection(
                    CharacterWorldSaveSection.Id,
                    DungeonSaveRestorePhase.Characters),
                physicalSection,
                new RegistryDependencyStubSection(
                    ResourceStockPolicySaveSection.Id,
                    DungeonSaveRestorePhase.RuntimeState,
                    PhysicalItemsSaveSection.Id),
                regionalSection
            };
            DungeonSaveSectionRegistry registry = new(
                sections,
                aggregateRootStore,
                new[]
                {
                    exactRouteLifetime,
                    (IDungeonRestoreTransactionParticipant)items
                }
                .Concat(inputOwners.RestoreParticipants)
                .ToArray());
            PhysicalItemBatchDispositionSaveData pendingPhysical =
                items.Capture().pendingBatchDispositions.Single(value =>
                    string.Equals(
                        value.operationId,
                        sourceReceipt.OperationId,
                        StringComparison.Ordinal));
            Require(!pendingPhysical.publicationExpected
                    && pendingPhysical.ownerRevision == 0L
                    && (pendingPhysical.sourceFacts?.Count ?? 0) == 0
                    && string.IsNullOrEmpty(pendingPhysical.publicationKey)
                    && string.IsNullOrEmpty(pendingPhysical.publicationId)
                    && string.IsNullOrEmpty(
                        pendingPhysical.publicationFingerprint)
                    && !pendingPhysical.publicationDelivered,
                "Regional fixture unexpectedly attached a partial physical "
                + "publication: expected="
                + pendingPhysical.publicationExpected
                + "; revision=" + pendingPhysical.ownerRevision
                + "; facts=" + (pendingPhysical.sourceFacts?.Count ?? 0)
                + "; key=" + pendingPhysical.publicationKey
                + "; id=" + pendingPhysical.publicationId
                + "; fingerprint="
                + pendingPhysical.publicationFingerprint
                + "; delivered=" + pendingPhysical.publicationDelivered);
            List<DungeonSaveSectionEnvelope> valid = registry.CaptureAll();
            DungeonGameRestoreReport validReport = new();
            Require(registry.RestoreAll(valid, validReport)
                    && validReport.Success
                    && !items.IsCandidateAvailable
                    && repository.GetEditorTestQuantity(stackId) == 1
                    && regionalRuntime.Contracts.Count == 1
                    && RegionalSupplyContractDeliveryOutbox.HasPending(
                        regionalRuntime.Contracts[0]),
                "Valid pending regional whole-save did not restore atomically: "
                + string.Join(" | ", validReport.Errors));

            RequireWholeSaveRejectedWithoutLeak(
                registry,
                RemovePhysicalPending(valid),
                items,
                "missing receipt");
            RequireWholeSaveRejectedWithoutLeak(
                registry,
                MutateRegionalOwner(valid, value =>
                    value.deliveryMassGrams = checked(
                        value.deliveryMassGrams + 1L)),
                items,
                "mismatched owner mass");
            RequireWholeSaveRejectedWithoutLeak(
                registry,
                MutateRegionalOwner(valid, ClearRegionalPendingOwner),
                items,
                "orphan incoming receipt");
        }
        finally
        {
            items.Dispose();
        }
    }

    private static void RequireWholeSaveRejectedWithoutLeak(
        DungeonSaveSectionRegistry registry,
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        IPhysicalItemRestoreCandidateQuery query,
        string caseName)
    {
        DungeonGameRestoreReport report = new();
        Require(!registry.RestoreAll(envelopes, report)
                && !report.Success
                && !query.IsCandidateAvailable,
            "Regional whole-save " + caseName
            + " did not reject atomically or leaked its candidate index.");
    }

    private static List<DungeonSaveSectionEnvelope> RemovePhysicalPending(
        IReadOnlyList<DungeonSaveSectionEnvelope> source)
    {
        List<DungeonSaveSectionEnvelope> clone = CloneEnvelopes(source);
        DungeonSaveSectionEnvelope physical = clone.Single(value =>
            string.Equals(
                value.sectionId,
                PhysicalItemsSaveSection.Id,
                StringComparison.Ordinal));
        DungeonPhysicalItemSaveData payload =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physical.payloadJson);
        payload.pendingBatchDispositions.Clear();
        physical.payloadJson = JsonUtility.ToJson(payload);
        return clone;
    }

    private static List<DungeonSaveSectionEnvelope> MutateRegionalOwner(
        IReadOnlyList<DungeonSaveSectionEnvelope> source,
        Action<RegionalSupplyContractState> mutation)
    {
        List<DungeonSaveSectionEnvelope> clone = CloneEnvelopes(source);
        DungeonSaveSectionEnvelope regional = clone.Single(value =>
            string.Equals(
                value.sectionId,
                RegionalSupplyContractSaveSection.Id,
                StringComparison.Ordinal));
        DungeonRegionalSupplyContractSaveData payload =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                regional.payloadJson);
        mutation(payload.contracts.Single());
        regional.payloadJson = JsonUtility.ToJson(payload);
        return clone;
    }

    private static List<DungeonSaveSectionEnvelope> CloneEnvelopes(
        IReadOnlyList<DungeonSaveSectionEnvelope> source) =>
        source.Select(value => new DungeonSaveSectionEnvelope
        {
            sectionId = value.sectionId,
            sectionVersion = value.sectionVersion,
            restorePhase = value.restorePhase,
            optional = value.optional,
            payloadJson = value.payloadJson
        }).ToList();

    private static void ClearRegionalPendingOwner(
        RegionalSupplyContractState owner)
    {
        owner.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
        owner.deliveryOperationId = string.Empty;
        owner.deliveryCommitId = string.Empty;
        owner.deliverySourceStackIds.Clear();
        owner.deliveryQuantity = 0;
        owner.deliveryMassGrams = 0L;
        owner.rewardTransactionId = string.Empty;
        owner.rewardBalanceBefore = 0;
        owner.rewardBalanceAfter = 0;
        owner.deliveryOutcomeResultKey = string.Empty;
    }

    private sealed class RegistryRegionalRuntime :
        IRegionalSupplyContractRuntime
    {
        private DungeonRegionalSupplyContractSaveData state;

        internal RegistryRegionalRuntime(
            DungeonRegionalSupplyContractSaveData state)
        {
            this.state = Clone(state);
        }

        public int Version { get; private set; }
        public bool IsUnlocked => true;
        public IReadOnlyList<RegionalSupplyContractState> Contracts =>
            state.contracts;
        public bool Accept(string contractId, out string message)
        {
            message = "fixture-only";
            return false;
        }
        public bool Decline(string contractId, out string message)
        {
            message = "fixture-only";
            return false;
        }
        public DungeonRegionalSupplyContractSaveData Capture() => Clone(state);
        public RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
            DungeonRegionalSupplyContractSaveData saveData) =>
            new(Clone(saveData));
        public void PublishRestoreCandidate(
            RegionalSupplyContractRestoreCandidate candidate)
        {
            state = Clone(candidate.Payload);
            Version++;
        }

        private static DungeonRegionalSupplyContractSaveData Clone(
            DungeonRegionalSupplyContractSaveData value) =>
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                JsonUtility.ToJson(value));
    }

    private sealed class RegistryDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        internal RegistryDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase phase,
            params string[] dependencies)
        {
            SectionId = sectionId;
            RestorePhase = phase;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{\"fixture\":true}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Invalid registry dependency fixture payload.");
            }
        }
        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RegionalStatusScenario : IDisposable
    {
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly EconomyProjectInputOwnerFixtureAuthority inputOwners;

        internal RegionalStatusScenario(string runId)
        {
            IDungeonItemCatalogProvider catalog =
                EditorItemCatalogFactory.Create();
            inputOwners = new EconomyProjectInputOwnerFixtureAuthority(
                new PhysicalItemMassQuery(catalog));
            outcomes = new MigratedProducerOutcomeEditorFixture(runId);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind
                    .RegionalSupplyContractResult
            };
            Session = new StatusSession();
            Runtime = new RegionalSupplyContractRuntime(
                new StatusWorld(),
                new StatusCommands(),
                Session,
                EmptyGrandProjectBenefits.Instance,
                new DungeonRuntimeAggregateRootStore(),
                inputOwners.Runtime,
                new RegionalSupplyContractOutcomeBridge(Probe));
            Runtime.Initialize();
        }

        internal RegionalSupplyContractRuntime Runtime { get; }
        internal MigratedProducerOutcomeEditorFixture Outcomes => outcomes;
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal StatusSession Session { get; }
        internal RegionalSupplyContractState FirstOffered =>
            Runtime.Contracts.First(value =>
                value.status == RegionalSupplyContractStatus.Offered);
        internal RegionalSupplyContractState Find(string contractId) =>
            Runtime.Contracts.Single(value => string.Equals(
                value.contractId,
                contractId,
                StringComparison.Ordinal));

        public void Dispose() => Runtime.Dispose();
    }

    private sealed class StatusWorld : IRegionalSupplyContractWorldQuery
    {
        private readonly RegionalSupplyContractItemSnapshot item = new(
            ItemId,
            "목재",
            ResourceItemKind.Raw,
            unitPrice: 10,
            requiredResearchId: string.Empty);

        public IReadOnlyList<RegionalSupplyContractItemSnapshot> Items =>
            new[] { item };
        public int ResidentPopulation => 3;
        public int CompletedResearchCount => 1;
        public bool IsResearchCompleted(string researchId) => true;
        public bool TryGetDeliveryDropoff(out Vector2Int dropoff)
        {
            dropoff = new Vector2Int(4, 2);
            return true;
        }
        public bool TryGetItem(
            string itemId,
            out RegionalSupplyContractItemSnapshot found)
        {
            found = string.Equals(itemId, item.ItemId, StringComparison.Ordinal)
                ? item
                : null;
            return found != null;
        }
        public int CountAtDestination(
            string itemId,
            string destinationId,
            bool deliveredOnly) => 0;
    }

    internal sealed class StatusSession : IRegionalSupplyContractSessionPort
    {
        private Action<int> dayStarted;
        private int day = 1;

        public bool IsPaused => false;
        public float Time { get; private set; }
        public bool TryGetCurrentDay(out int currentDay)
        {
            currentDay = day;
            return true;
        }
        public IDisposable SubscribeDayStarted(Action<int> handler)
        {
            dayStarted += handler;
            return new CallbackDisposable(() => dayStarted -= handler);
        }
        internal void RaiseDay(int nextDay)
        {
            day = nextDay;
            Time += 1f;
            dayStarted?.Invoke(day);
        }
    }

    private sealed class StatusCommands : IRegionalSupplyContractCommandPort
    {
        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int dropoff,
            string destinationId,
            out int requested)
        {
            requested = 0;
            return false;
        }
        public bool TryCommitDeliveryTransferPending(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            string reasonCode,
            out RegionalSupplyDeliveryTransferReceipt receipt,
            out string failureReason)
        {
            receipt = null;
            failureReason = "status-fixture-no-delivery";
            return false;
        }
        public bool TryGetPendingDeliveryTransfer(
            string operationId,
            out RegionalSupplyDeliveryTransferReceipt receipt)
        {
            receipt = null;
            return false;
        }
        public bool AcknowledgeDeliveryTransfer(
            string commitId,
            out string failureReason)
        {
            failureReason = "status-fixture-no-ack";
            return false;
        }
        public void PrioritizeDestination(string destinationId)
        {
        }
        public void RequestHauler()
        {
        }
        public bool TryAddContractIncome(
            int amount,
            string operationId,
            out RegionalSupplyRewardReceipt receipt,
            out string failureReason)
        {
            receipt = null;
            failureReason = "status-fixture-no-income";
            return false;
        }
    }

    private sealed class EmptyGrandProjectBenefits : IGrandProjectBenefitQuery
    {
        internal static readonly EmptyGrandProjectBenefits Instance = new();
        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private Action callback;
        internal CallbackDisposable(Action callback) =>
            this.callback = callback;
        public void Dispose()
        {
            Action current = callback;
            callback = null;
            current?.Invoke();
        }
    }

    private sealed class FailOnceCommandPort :
        IRegionalSupplyContractCommandPort
    {
        private readonly IPhysicalFacilityItemBatchTransferGateway gateway;

        internal FailOnceCommandPort(
            IPhysicalFacilityItemBatchTransferGateway gateway) =>
            this.gateway = gateway
                ?? throw new ArgumentNullException(nameof(gateway));

        internal bool FailNextAcknowledgement { get; set; }
        internal int IncomePublicationCount { get; private set; }
        internal int PublishedIncome { get; private set; }
        internal int SuccessfulAcknowledgements { get; private set; }

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int dropoff,
            string destinationId,
            out int requested)
        {
            requested = 0;
            return false;
        }

        public bool TryCommitDeliveryTransferPending(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            string reasonCode,
            out RegionalSupplyDeliveryTransferReceipt receipt,
            out string failureReason)
        {
            receipt = null;
            failureReason = "not used by focused recovery fixture";
            return false;
        }

        public bool TryGetPendingDeliveryTransfer(
            string operationId,
            out RegionalSupplyDeliveryTransferReceipt receipt)
        {
            receipt = null;
            if (!gateway.TryGetPending(
                    operationId,
                    out PhysicalItemBatchDispositionReceipt physical))
            {
                return false;
            }
            receipt = ToContractReceipt(physical);
            return true;
        }

        public bool AcknowledgeDeliveryTransfer(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-regional-supply-ack-failure";
                return false;
            }
            bool acknowledged = gateway.Acknowledge(
                commitId,
                out failureReason);
            if (acknowledged)
            {
                SuccessfulAcknowledgements++;
            }
            return acknowledged;
        }

        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition) => 0;

        public void PrioritizeDestination(string destinationId)
        {
        }

        public void RequestHauler()
        {
        }

        public bool TryAddContractIncome(
            int amount,
            string operationId,
            out RegionalSupplyRewardReceipt receipt,
            out string failureReason)
        {
            IncomePublicationCount++;
            PublishedIncome += amount;
            receipt = new RegionalSupplyRewardReceipt
            {
                operationId = operationId,
                transactionId = "economy-transaction:regional-supply:1",
                amount = amount,
                balanceBefore = 100,
                balanceAfter = checked(100 + amount)
            };
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly CandidateQuery Empty = new();

        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> pending;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] pending)
        {
            this.pending = pending ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => pending;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = pending.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.OperationId,
                    operationId,
                    StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class EmptyRestoreWorldCandidates :
        IRestoreWorldCandidateQuery
    {
        internal static readonly EmptyRestoreWorldCandidates Instance = new();

        public int Revision => 0;
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }

        public bool TryGetBuildings(
            out IReadOnlyList<BuildableObject> buildings)
        {
            buildings = Array.Empty<BuildableObject>();
            return true;
        }

        public bool TryGetCharacters(
            out IReadOnlyList<CharacterActor> characters)
        {
            characters = Array.Empty<CharacterActor>();
            return false;
        }

        public bool TryGetWildlife(
            out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = Array.Empty<WildlifeActor>();
            return false;
        }

        public bool TryGetExteriorZones(
            out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = Array.Empty<ExteriorZoneMarker>();
            return false;
        }
    }
}
#endif
