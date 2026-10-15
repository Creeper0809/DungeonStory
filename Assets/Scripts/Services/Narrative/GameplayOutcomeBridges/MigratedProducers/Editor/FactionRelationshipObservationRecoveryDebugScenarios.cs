#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static partial class MigratedProducerRunOffenseEntryE2EDebugScenarios
{
    private const string B130TargetFaction = "faction:b130:target";
    private const string B130WitnessOne = "faction:b130:witness-one";
    private const string B130WitnessTwo = "faction:b130:witness-two";
    private const string B130LootItem = "resource:clean-water";

    public static void RunRelationshipObservationBatch()
    {
        foreach (FactionRelationshipObservationKind kind in new[]
                 {
                     FactionRelationshipObservationKind.Trust,
                     FactionRelationshipObservationKind.Betrayal
                 })
        {
            for (int mode = 0; mode <= 2; mode++)
                VerifyRelationshipAcknowledgement(kind, mode);
        }

        VerifyRelationshipRecoveryBlocksNewEffects();
        foreach (FactionRelationshipObservationKind kind in new[]
                 {
                     FactionRelationshipObservationKind.Trust,
                     FactionRelationshipObservationKind.Betrayal
                 })
        {
            for (int mode = 0; mode <= 1; mode++)
            {
                RelationshipObservationBundle checkpoint =
                    CapturePendingRelationshipCheckpoint(kind, mode);
                if (kind == FactionRelationshipObservationKind.Betrayal
                    && mode == 0)
                    VerifyRelationshipCheckpointTamper(checkpoint);
                VerifyFreshRelationshipRecovery(checkpoint, kind, mode);
            }
        }
        VerifySequentialTrustRevisions();
        VerifyRelationshipDefinitiveRejection(
            FactionRelationshipObservationKind.Trust);
        VerifyRelationshipDefinitiveRejection(
            FactionRelationshipObservationKind.Betrayal);
        Debug.Log("B130_FACTION_RELATIONSHIP_OBSERVATION=PASS");
    }

    private static void VerifyRelationshipAcknowledgement(
        FactionRelationshipObservationKind kind,
        int mode)
    {
        using var context = new RelationshipObservationContext(
            $"run:b130:ack:{(int)kind}:{mode}",
            MigratedProducerOutcomeFaultMode.None);
        context.Transport.Target = FactType(kind);
        context.Transport.Mode = mode;
        int fences = 0;
        context.Transport.During = () =>
        {
            RequireObservationFence(() => context.Runtime.Capture());
            RequireObservationFence(context.Runtime.Tick);
            RequireObservationFence(() => context.Apply(kind));
            fences++;
        };

        string effectsBefore = context.CaptureEffects();
        int lootBefore = context.LootQuantity;
        Dictionary<string, int> trustBefore = context.CaptureTrust();
        Require(
            context.Apply(kind),
            $"Relationship {kind} mode {mode} reported an uncertain publication as rejected.");
        DungeonFactionSaveData pending = context.Runtime.Capture();
        string effectsAfter = context.CaptureEffects();
        int exactBeforeRecovery = context.ExactCount;

        Require(
            fences == 1
            && pending.relationshipObservationSequence == 1
            && pending.relationshipObservations.Count == 1
            && pending.relationshipObservations[0].kind == kind
            && pending.relationshipObservations[0].frozenFingerprint.Length == 64
            && pending.relationshipObservations[0].frozenFingerprint
                == pending.relationshipObservations[0].ComputeFingerprint()
            && exactBeforeRecovery == (mode == 0 ? 0 : 1)
            && effectsAfter != effectsBefore,
            $"Relationship {kind} mode {mode} did not retain one frozen uncertain observation and one domain effect.");
        if (kind == FactionRelationshipObservationKind.Trust)
        {
            FactionRelationshipObservationData fact =
                pending.relationshipObservations[0];
            Require(
                context.CaptureTrust()[B130TargetFaction]
                    == trustBefore[B130TargetFaction] + 7
                && context.LootQuantity == lootBefore
                && fact.ownerIdentity == B130TargetFaction
                && fact.factionId == B130TargetFaction
                && fact.factionDisplayName == "관계 관찰 대상"
                && fact.previousTrust == trustBefore[B130TargetFaction]
                && fact.currentTrust
                    == context.CaptureTrust()[B130TargetFaction]
                && fact.reason == "b130-direct-trust",
                "Direct trust publication did not apply exactly one trust effect.");
        }
        else
        {
            Dictionary<string, int> trustAfter = context.CaptureTrust();
            FactionRelationshipObservationData fact =
                pending.relationshipObservations[0];
            Require(
                trustAfter.All(value => value.Value < trustBefore[value.Key])
                && context.LootQuantity > lootBefore
                && fact.ownerIdentity
                    == "faction-betrayal:" + B130TargetFaction + ":9"
                && fact.actualLootValue > 0
                && fact.transitions.Count == trustBefore.Count
                && fact.transitions.All(value =>
                    value.previousTrust == trustBefore[value.factionId]
                    && value.currentTrust == trustAfter[value.factionId]),
                "Direct betrayal publication did not apply one trust transition set and one loot grant.");
        }

        context.Transport.During = null;
        context.Transport.Mode = -1;
        context.Runtime.Tick();
        DungeonFactionSaveData recovered = context.Runtime.Capture();
        Require(
            recovered.relationshipObservationSequence == 1
            && recovered.relationshipObservations.Count == 0
            && context.ExactCount == 1
            && context.CaptureEffects() == effectsAfter,
            $"Relationship {kind} mode {mode} recovery repeated effects or failed to acknowledge the exact native record.");
        string stable = context.CaptureAllState();
        context.Runtime.Tick();
        Require(
            context.CaptureAllState() == stable && context.ExactCount == 1,
            $"Relationship {kind} mode {mode} repeated recovery was not idempotent.");
    }

    private static void VerifyRelationshipRecoveryBlocksNewEffects()
    {
        using var context = new RelationshipObservationContext(
            "run:b130:blocked-recovery",
            MigratedProducerOutcomeFaultMode.None);
        context.Transport.Target = typeof(FactionBetrayalDomainFact);
        context.Transport.Mode = 0;
        Require(
            context.Apply(FactionRelationshipObservationKind.Betrayal),
            "Blocked-recovery setup betrayal failed.");
        string effects = context.CaptureEffects();
        DungeonFactionSaveData pending = context.Runtime.Capture();

        context.Transport.Mode = -1;
        context.Transport.BlockRecovery = true;
        Require(
            !context.Runtime.TryAdjustTrust(
                B130TargetFaction,
                5,
                "b130-blocked-new-command",
                out string message)
            && message.Contains("b130-controlled-relationship-recovery-failure",
                StringComparison.Ordinal)
            && context.Transport.ReplayBatchCalls == 1
            && context.CaptureEffects() == effects,
            "A failed relationship recovery did not reject the next command before new effects.");
        DungeonFactionSaveData after = context.Runtime.Capture();
        Require(
            after.relationshipObservationSequence
                == pending.relationshipObservationSequence
            && after.relationshipObservations.Count == 1
            && context.ExactCount == 0,
            "A failed relationship recovery changed its sequence, pending fact, or native ledger.");

        context.Transport.BlockRecovery = false;
        context.Runtime.Tick();
        Require(
            context.Runtime.Capture().relationshipObservations.Count == 0
            && context.ExactCount == 1
            && context.CaptureEffects() == effects,
            "Blocked relationship recovery did not resume its frozen fact exactly once.");
    }

    private static RelationshipObservationBundle
        CapturePendingRelationshipCheckpoint(
            FactionRelationshipObservationKind kind,
            int mode)
    {
        using var context = new RelationshipObservationContext(
            FreshRelationshipRunId(kind, mode),
            MigratedProducerOutcomeFaultMode.None);
        context.Transport.Target = FactType(kind);
        context.Transport.Mode = mode;
        Require(
            context.Apply(kind),
            $"Fresh relationship restore setup {kind}/{mode} failed.");
        RelationshipObservationBundle checkpoint = context.CaptureBundle();
        FactionCargoSaveValidation.ValidateJson(
            JsonUtility.ToJson(checkpoint.faction));
        RequireRelationshipJoin(checkpoint, true);
        Require(
            checkpoint.faction.relationshipObservations.Count == 1
            && checkpoint.faction.relationshipObservations[0].kind == kind
            && context.ExactCount == (mode == 0 ? 0 : 1)
            && (kind == FactionRelationshipObservationKind.Trust
                ? context.CaptureTrust()[B130TargetFaction] == 67
                : context.LootQuantity > 0),
            $"Fresh relationship restore setup {kind}/{mode} did not include its pending native, faction, physical, and campaign state.");
        return checkpoint;
    }

    private static void VerifyRelationshipCheckpointTamper(
        RelationshipObservationBundle checkpoint)
    {
        string json = JsonUtility.ToJson(checkpoint);

        RelationshipObservationBundle keyTamper =
            JsonUtility.FromJson<RelationshipObservationBundle>(json);
        keyTamper.faction.relationshipObservations[0].publicationKey += ":wrong";
        RequireRelationshipJoin(keyTamper, false);

        RelationshipObservationBundle nameTamper =
            JsonUtility.FromJson<RelationshipObservationBundle>(json);
        nameTamper.faction.relationshipObservations[0]
            .transitions[0].factionDisplayName += " 변조";
        RequireRelationshipJoin(nameTamper, false);

        RelationshipObservationBundle dayTamper =
            JsonUtility.FromJson<RelationshipObservationBundle>(json);
        dayTamper.faction.relationshipObservations[0].absoluteDay--;
        RequireRelationshipJoin(dayTamper, false);

        RelationshipObservationBundle quantityTamper =
            JsonUtility.FromJson<RelationshipObservationBundle>(json);
        quantityTamper.faction.relationshipObservations[0].actualLootValue++;
        RequireRelationshipJoin(quantityTamper, false);
    }

    private static void VerifyFreshRelationshipRecovery(
        RelationshipObservationBundle checkpoint,
        FactionRelationshipObservationKind kind,
        int mode)
    {
        string frozenKey = checkpoint.faction.relationshipObservations
            .Single().publicationKey;
        string expectedEffects = RelationshipObservationContext
            .CaptureEffects(checkpoint.faction, checkpoint.campaign,
                checkpoint.items);
        using var restored = new RelationshipObservationContext(
            FreshRelationshipRunId(kind, mode),
            MigratedProducerOutcomeFaultMode.None);
        restored.Restore(checkpoint);
        Require(
            restored.Runtime.Capture().relationshipObservations.Count == 0
            && restored.Runtime.Capture().relationshipObservationSequence == 1
            && restored.ExactCount == 1
            && restored.CaptureEffects() == expectedEffects,
            $"Fresh relationship recovery {kind}/{mode} repeated campaign/physical effects or did not clear the frozen observation.");
        restored.RequireExactIdentity(
            frozenKey,
            checkpoint.faction.relationshipObservations.Single());
        string stable = restored.CaptureAllState();
        restored.Runtime.Tick();
        Require(
            restored.CaptureAllState() == stable && restored.ExactCount == 1,
            $"Fresh relationship recovery {kind}/{mode} was not idempotent.");
    }

    private static void VerifySequentialTrustRevisions()
    {
        using var context = new RelationshipObservationContext(
            "run:b130:sequential-trust",
            MigratedProducerOutcomeFaultMode.None);
        Require(
            context.Apply(FactionRelationshipObservationKind.Trust),
            "First sequential trust observation failed.");
        GameplayResultKey firstKey = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            B130TargetFaction,
            1);
        Require(
            context.TryGetIdentity(firstKey, out var firstIdentity),
            "First sequential trust observation lost its native identity.");

        Require(
            context.Apply(FactionRelationshipObservationKind.Trust),
            "Second sequential trust observation failed.");
        GameplayResultKey secondKey = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            B130TargetFaction,
            2);
        DungeonFactionSaveData saved = context.Runtime.Capture();
        Require(
            context.TryGetIdentity(secondKey, out var secondIdentity)
            && context.TryGetIdentity(firstKey, out var firstIdentityAfter)
            && firstKey.ToString() != secondKey.ToString()
            && !firstIdentity.OutcomeId.Equals(secondIdentity.OutcomeId)
            && firstIdentity.OutcomeId.Equals(firstIdentityAfter.OutcomeId)
            && context.ExactCount == 2
            && saved.relationshipObservationSequence == 2
            && saved.relationshipObservations.Count == 0
            && context.CaptureTrust()[B130TargetFaction] == 74,
            "Sequential trust observations reused a pending native key or failed to advance two exact revisions.");
    }

    private static string FreshRelationshipRunId(
        FactionRelationshipObservationKind kind,
        int mode) => $"run:b130:fresh-restore:{(int)kind}:{mode}";

    private static void VerifyRelationshipDefinitiveRejection(
        FactionRelationshipObservationKind kind)
    {
        using var context = new RelationshipObservationContext(
            "run:b130:rejected:" + (int)kind,
            MigratedProducerOutcomeFaultMode.RejectCommit);
        string effects = context.CaptureEffects();
        DungeonFactionSaveData before = context.Runtime.Capture();
        Require(
            !context.Apply(kind),
            $"Definitively rejected relationship {kind} was reported as successful.");
        DungeonFactionSaveData after = context.Runtime.Capture();
        Require(
            context.CaptureEffects() == effects
            && after.relationshipObservationSequence
                == before.relationshipObservationSequence
            && after.relationshipObservations.Count == 0
            && context.ExactCount == 0,
            $"Definitively rejected relationship {kind} did not atomically roll back effects and sequence.");
    }

    private static Type FactType(FactionRelationshipObservationKind kind) =>
        kind switch
        {
            FactionRelationshipObservationKind.Trust =>
                typeof(FactionTrustChangedDomainFact),
            FactionRelationshipObservationKind.Betrayal =>
                typeof(FactionBetrayalDomainFact),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static void RequireObservationFence(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "faction-observation-active")
        {
            return;
        }
        throw new InvalidOperationException(
            "Relationship observation publishing did not fence owner reentry.");
    }

    private static void RequireRelationshipJoin(
        RelationshipObservationBundle checkpoint,
        bool expected)
    {
        var report = new DungeonGameRestoreReport();
        FactionObservationPreflight.Validate(
            checkpoint.faction,
            checkpoint.ledger,
            checkpoint.counters,
            report);
        Require(
            report.Success == expected,
            "Relationship observation join mismatch: "
            + string.Join("; ", report.Errors));
    }

    [Serializable]
    private sealed class RelationshipObservationBundle
    {
        public DungeonFactionSaveData faction;
        public FactionCampaignWorldSaveData campaign;
        public DungeonPhysicalItemSaveData items;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }

    private sealed class RelationshipObservationContext : IDisposable
    {
        private static readonly string TrustOutcomeType =
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.FactionTrustChangedEvent)
            .OutcomeTypeId.Value;

        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly DomainFactDispatcher facts;
        private readonly WorldItemStackRuntime items;
        private readonly MultiFactionCampaignDouble campaign;

        internal RelationshipObservationContext(
            string runId,
            MigratedProducerOutcomeFaultMode fault)
        {
            outcomes = new MigratedProducerOutcomeEditorFixture(runId);
            var probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder)
            {
                TargetKind =
                    MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                FaultMode = fault
            };
            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            items = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    itemCatalog,
                    out WorldItemRepository repository,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _);
            FactionAggregateState state = CreateState();
            campaign = new MultiFactionCampaignDouble(state.Factions.Values);
            var allianceBudget =
                new ResourceFactionAllianceBenefitBudgetApplicationAdapter();
            state.AllianceBenefitAuthorityDigest =
                allianceBudget.AuthorityDigest;
            state.AllianceBenefitBalanceMilliEwu =
                allianceBudget.CapacityMilliEwu;
            state.AllianceBenefitLastRefillDay = state.CurrentDay;
            facts = CreateDomainFacts(outcomes, probe);
            Transport = new FactionObservationTestTransport(facts);
            Runtime = new FactionRuntimeApplicationAdapter(
                state,
                PositiveGameClock.Instance,
                new GameEventBus(),
                new ScenarioMoneyAccount(),
                campaign,
                campaign,
                campaign,
                Transport,
                Transport,
                outcomes.AggregateRootStore,
                dropZones: FixedDropZone.Instance,
                itemRuntime: items,
                definitions: CreateDefinitions(),
                world: new FactionWorldDouble(),
                allianceBenefitBudget: allianceBudget,
                itemSpawner: new WorldItemSpawner(
                    itemCatalog,
                    repository,
                    EditorNullItemMarkerPresenter.Instance));
        }

        internal FactionRuntimeApplicationAdapter Runtime { get; }
        internal FactionObservationTestTransport Transport { get; }
        internal int ExactCount => outcomes.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null
                && value.outcomeTypeId == TrustOutcomeType);
        internal int LootQuantity => items.GetAllStacks()
            .Where(value => value != null && value.ItemId == B130LootItem)
            .Sum(value => value.AvailableQuantity);

        internal bool Apply(FactionRelationshipObservationKind kind)
        {
            return kind switch
            {
                FactionRelationshipObservationKind.Trust =>
                    Runtime.TryAdjustTrust(
                        B130TargetFaction,
                        7,
                        "b130-direct-trust",
                        out _),
                FactionRelationshipObservationKind.Betrayal =>
                    Runtime.TryBetray(B130TargetFaction, 50, out _),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, null)
            };
        }

        internal Dictionary<string, int> CaptureTrust() => new[]
            {
                B130TargetFaction,
                B130WitnessOne,
                B130WitnessTwo
            }
            .ToDictionary(
                factionId => factionId,
                factionId =>
                {
                    Require(
                        Runtime.TryGetFaction(
                            factionId,
                            out DungeonFactionState faction),
                        "Relationship fixture lost faction " + factionId + ".");
                    return faction.trust;
                },
                StringComparer.Ordinal);

        internal string CaptureEffects() => CaptureEffects(
            Runtime.Capture(),
            campaign.CaptureFactions(),
            items.Capture());

        internal static string CaptureEffects(
            DungeonFactionSaveData faction,
            FactionCampaignWorldSaveData campaign,
            DungeonPhysicalItemSaveData items)
        {
            string trust = string.Join(
                ",",
                faction.factions
                    .OrderBy(value => value.factionId, StringComparer.Ordinal)
                    .Select(value => value.factionId + "=" + value.trust));
            return trust + "|" + JsonUtility.ToJson(campaign)
                + "|" + JsonUtility.ToJson(items);
        }

        internal string CaptureAllState() =>
            JsonUtility.ToJson(Runtime.Capture()) + "|"
            + JsonUtility.ToJson(campaign.CaptureFactions()) + "|"
            + JsonUtility.ToJson(items.Capture()) + "|"
            + JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes())
            + "|" + JsonUtility.ToJson(outcomes.Transaction.Capture());

        internal RelationshipObservationBundle CaptureBundle() =>
            JsonUtility.FromJson<RelationshipObservationBundle>(
                JsonUtility.ToJson(new RelationshipObservationBundle
                {
                    faction = Runtime.Capture(),
                    campaign = campaign.CaptureFactions(),
                    items = items.Capture(),
                    ledger = outcomes.Ledger.CaptureGameplayOutcomes(),
                    counters = outcomes.Transaction.Capture()
                }));

        internal void Restore(RelationshipObservationBundle checkpoint)
        {
            RequireRelationshipJoin(checkpoint, true);
            GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
                outcomes.Ledger.PrepareGameplayOutcomeRestore(
                    checkpoint.ledger);
            outcomes.Ledger.BeginRestoreCandidate();
            outcomes.Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
            outcomes.Ledger.PublishRestoreCandidate();
            outcomes.Ledger.CompleteRestoreCandidate();
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(checkpoint.counters));
            campaign.PublishFactions(
                campaign.PrepareFactionsRestore(checkpoint.campaign));
            items.Restore(checkpoint.items);
            Runtime.PublishRestoreCandidate(
                Runtime.PrepareRestoreCandidate(checkpoint.faction));
            Runtime.Tick();
        }

        internal void RequireExactIdentity(
            string publicationKey,
            FactionRelationshipObservationData frozen)
        {
            Require(
                GameplayDomainFactDeliveryQuery.TryReadKey(
                    publicationKey, out GameplayResultKey key)
                && TryGetIdentity(key, out var identity)
                && identity.HasCanonicalPayloadHash
                && outcomes.Ledger.TryGetExact(identity.OutcomeId, out var exact)
                && exact.outcomeTypeId == TrustOutcomeType
                && exact.absoluteDay == frozen.absoluteDay
                && (frozen.kind == FactionRelationshipObservationKind.Trust
                    ? HasParticipant(
                            exact,
                            "faction",
                            frozen.factionId,
                            "faction")
                        && HasMetric(
                            exact,
                            "trust-before",
                            frozen.factionId,
                            frozen.previousTrust)
                        && HasMetric(
                            exact,
                            "trust-after",
                            frozen.factionId,
                            frozen.currentTrust)
                        && HasMetric(
                            exact,
                            "delta",
                            frozen.factionId,
                            frozen.currentTrust - frozen.previousTrust)
                    : HasParticipant(
                            exact,
                            "faction",
                            B130TargetFaction,
                            "target")
                        && HasParticipant(
                            exact,
                            "faction",
                            B130WitnessOne,
                            "witness")
                        && HasParticipant(
                            exact,
                            "faction",
                            B130WitnessTwo,
                            "witness")
                        && frozen.transitions.All(value =>
                            HasMetric(
                                exact,
                                "trust-before",
                                value.factionId,
                                value.previousTrust)
                            && HasMetric(
                                exact,
                                "trust-after",
                                value.factionId,
                                value.currentTrust))
                        && exact.metrics.Any(value => value != null
                            && value.metricId == "value"
                            && value.value == frozen.actualLootValue)),
                "Recovered relationship observation lacked its exact frozen native identity.");
        }

        internal bool TryGetIdentity(
            GameplayResultKey key,
            out GameplayOutcomeReplayIdentity identity) =>
            outcomes.Ledger.TryGetResultIdentity(key, out identity);

        public void Dispose()
        {
            Runtime.Dispose();
            facts.Dispose();
            items.Dispose();
        }

        private static FactionAggregateState CreateState()
        {
            var state = new FactionAggregateState { CurrentDay = 9 };
            foreach (string factionId in new[]
                     {
                         B130TargetFaction,
                         B130WitnessOne,
                         B130WitnessTwo
                     })
            {
                state.Factions.Add(
                    factionId,
                    new DungeonFactionState
                    {
                        factionId = factionId,
                        discovered = true,
                        trust = 60,
                        homeQ = 1,
                        homeR = 1
                    });
            }
            return state;
        }

        private static IReadOnlyList<FactionDefinitionSnapshot>
            CreateDefinitions() => new[]
            {
                CreateDefinition(
                    B130TargetFaction,
                    "관계 관찰 대상",
                    includeLoot: true),
                CreateDefinition(
                    B130WitnessOne,
                    "관계 관찰 목격자 하나",
                    includeLoot: false),
                CreateDefinition(
                    B130WitnessTwo,
                    "관계 관찰 목격자 둘",
                    includeLoot: false)
            };

        private static FactionDefinitionSnapshot CreateDefinition(
            string factionId,
            string displayName,
            bool includeLoot) => new(
            factionId,
            displayName,
            "species:test",
            "B130 세력 관계 관찰 복구 검증",
            Array.Empty<string>(),
            Array.Empty<string>(),
            string.Empty,
            includeLoot
                ? new[]
                {
                    new FactionCargoLine
                    {
                        itemId = B130LootItem,
                        amount = 100
                    }
                }
                : Array.Empty<FactionCargoLine>(),
            Array.Empty<FactionCargoLine>(),
            FactionRouteEconomicPolicyDescriptor.Create(
                FactionRouteEconomicPolicyIds.PaidMarketPurchase,
                1),
            FactionRouteEconomicPolicyDescriptor.Create(
                FactionRouteEconomicPolicyIds.AllianceBenefit,
                1),
            7,
            20,
            10);
    }
}
#endif
