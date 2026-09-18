using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class OffenseAggregateSaveV18DebugScenarios
{
    // Fixture-local prices only: pair this with the actual settled full-save proof.
    public static void VerifyFrozenValuationAfterAuthorityChange()
    {
        const string itemId = "food:preserved-ration";
        MutableSettlementValueQuery query = new MutableSettlementValueQuery
        {
            Value = new V27EmbeddedWorkValueProjection(
                itemId, 1001L, 301L, "basis:fixture-a", "recipe:fixture-a")
        };
        OffenseItemValuationSnapshot original =
            OffenseExpeditionRun.CreateValuation(query, itemId, 3);
        var receipt = new OffenseExpeditionItemReceipt(
            OffenseExpeditionItemReceiptKind.SupplyReturned,
            itemId, 3, string.Empty, 0f, original);
        var result = new OffenseExpeditionResult(
            "expedition:frozen-value-proof", "food_farm", "Frozen value proof",
            true, 10f, 10f, 1f, 12f,
            Array.Empty<OffenseExpeditionMemberSnapshot>(), Array.Empty<string>(),
            itemReceipts: new[] { receipt });
        string originalDetail = result.ToDetailText();

        query.Value = new V27EmbeddedWorkValueProjection(
            itemId, 9007L, 4003L, "basis:fixture-b", "recipe:fixture-b");
        OffenseItemValuationSnapshot current =
            OffenseExpeditionRun.CreateValuation(query, itemId, 5);
        Require(current.state == OffenseSettlementValuationState.Valued
                && current.acquisitionMilliEwuPerUnit == 9007L
                && current.recoverableMilliEwuPerUnit == 4003L
                && current.quantity == 5 && current.basisId == "basis:fixture-b"
                && current.selectedSourceId == "recipe:fixture-b",
            "Changed fixture authority was not actually consumed by a new valuation.");

        OffenseExpeditionResult copied = result
            .WithGrantedRewards(Array.Empty<OffenseRewardGrantResult>())
            .WithArrivalReceipts(Array.Empty<OffenseExpeditionArrivalReceipt>())
            .WithAdditionalCurrencyReceipts(Array.Empty<OffenseExpeditionCurrencyReceipt>());
        OffenseExpeditionResult appended = copied.WithAdditionalItemReceipts(new[]
        {
            new OffenseExpeditionItemReceipt(
                OffenseExpeditionItemReceiptKind.RewardGranted,
                itemId, 5, string.Empty, 0f, current)
        });
        foreach (OffenseExpeditionResult history in new[] { result, copied, appended })
        {
            OffenseExpeditionItemReceipt saved = history.itemReceipts.Single(value =>
                value.kind == OffenseExpeditionItemReceiptKind.SupplyReturned);
            Require(saved.itemId == itemId && saved.quantity == 3
                    && saved.valuation.itemId == itemId && saved.valuation.quantity == 3
                    && saved.valuation.state == OffenseSettlementValuationState.Valued
                    && saved.valuation.acquisitionMilliEwuPerUnit == 1001L
                    && saved.valuation.recoverableMilliEwuPerUnit == 301L
                    && saved.valuation.basisId == "basis:fixture-a"
                    && saved.valuation.selectedSourceId == "recipe:fixture-a",
                "Current authority change revalued a historical settlement receipt.");
        }
        Require(result.ToDetailText() == originalDetail
                && copied.ToDetailText() == originalDetail
                && appended.itemReceipts.Count == 2
                && appended.itemReceipts[1].valuation.acquisitionMilliEwuPerUnit == 9007L
                && query.ReadCount == 2,
            "History presentation/copy re-read prices or lost the new receipt.");
        Debug.Log("PASS WIM053_FROZEN_VALUE_AUTHORITY_CHANGE: new valuation=B; historical quantity/value/provenance=A; copies/detail immutable; queryReads=2; fullSaveProofSeparate=true");
    }

    private sealed class MutableSettlementValueQuery : IV27EmbeddedWorkValueProjectionQuery
    {
        public V27EmbeddedWorkValueProjection Value { get; set; }
        public int ReadCount { get; private set; }
        public bool AuthorityAvailable => true;
        public string AuthorityFailure => string.Empty;
        public bool TryGet(string itemId, out V27EmbeddedWorkValueProjection value)
        {
            ReadCount++;
            value = Value;
            return string.Equals(itemId, value.ItemId, StringComparison.Ordinal);
        }
    }

    [MenuItem("Tools/DungeonStory/QA/V18/Offense Aggregate Save Proof")]
    public static void Run()
    {
        DungeonOffenseAggregateSaveData canonical = CreateCanonicalPayload();
        string sourceJson = JsonUtility.ToJson(canonical);
        OffenseAggregateRestorePlan plan =
            OffenseAggregateSaveValidation.BuildRestorePlan(canonical);
        string restoredJson = JsonUtility.ToJson(plan.Payload);
        Require(string.Equals(sourceJson, restoredJson, StringComparison.Ordinal),
            "Offense aggregate candidate round-trip is not canonical.");
        Require(!plan.Payload.expedition.hasActiveBattle
                && plan.Payload.expedition.activeBattle == null,
            "JsonUtility null materialization leaked an empty battle into the restore plan.");
        ValidateReturnArrivalCharacterIds();
        ValidateSettlementReceiptCompositeKeys();
        Require(typeof(DungeonOffenseAggregateSaveData).GetField("campaign")?.FieldType
                    == typeof(DungeonOffenseCampaignSaveData),
            "Offense campaign state is not owned by the aggregate payload.");
        string[] retiredExpeditionCampaignFields =
        {
            "reconLevel",
            "selectedTargetId",
            "knownTargetIds",
            "completedTargetIds",
            "revealedTruthTargetId"
        };
        foreach (string fieldName in retiredExpeditionCampaignFields)
        {
            Require(typeof(DungeonOffenseSaveData).GetField(fieldName) == null,
                $"Expedition payload still duplicates campaign field '{fieldName}'.");
        }

        DungeonOffenseAggregateSaveData hiddenBattle =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(sourceJson);
        hiddenBattle.expedition.activeBattle.battleId = "battle:hidden";
        bool hiddenBattleRejected = false;
        try
        {
            OffenseAggregateSaveValidation.BuildRestorePlan(hiddenBattle);
        }
        catch (InvalidOperationException)
        {
            hiddenBattleRejected = true;
        }
        Require(hiddenBattleRejected,
            "Battle data hidden behind hasActiveBattle=false was accepted.");

        object liveSentinel = new object();
        object liveState = liveSentinel;
        DungeonOffenseAggregateSaveData invalid =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(sourceJson);
        invalid.world.worldDay = 0;
        bool rejected = false;
        try
        {
            OffenseAggregateSaveValidation.BuildRestorePlan(invalid);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected && ReferenceEquals(liveState, liveSentinel),
            "Invalid offense payload was accepted or mutated live state.");

        DungeonOffenseAggregateSaveData invalidCampaign =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(sourceJson);
        invalidCampaign.campaign.selectedTargetId = "campaign:not-known";
        bool invalidCampaignRejected = false;
        try
        {
            OffenseAggregateSaveValidation.BuildRestorePlan(invalidCampaign);
        }
        catch (InvalidOperationException)
        {
            invalidCampaignRejected = true;
        }
        Require(invalidCampaignRejected,
            "Campaign selection outside the known-target set was accepted by aggregate validation.");

        Require(typeof(OffenseAggregateSaveSection).BaseType == typeof(
                DungeonStrictJsonSaveSection<
                    DungeonOffenseAggregateSaveData,
                    OffenseAggregateRuntimeRestoreCandidate>)
                && !typeof(IDungeonRestoreTransactionParticipant).IsAssignableFrom(
                    typeof(OffenseAggregateSaveSection)),
            "Offense save authority is not the strict candidate section.");
        bool published = false;
        DiscardProbeCandidate detachedCandidate = new DiscardProbeCandidate();
        IDungeonDiscardableSaveRestoreStage stage =
            new DungeonCandidateSaveRestoreStage<DiscardProbeCandidate>(
                OffenseAggregateSaveSection.Id,
                detachedCandidate,
                _ => published = true);
        stage.Discard();
        Require(!published
                && detachedCandidate.IsDiscarded
                && ReferenceEquals(liveState, liveSentinel),
            "A late restore failure did not discard the detached offense candidate.");

        Debug.Log(
            "OFFENSE_AGGREGATE_V18_PROOF_PASSED canonicalRoundTrip=true campaignAuthority=true expeditionCampaignDuplicates=0 optionalBattlePresence=true hiddenBattleRejected=true invalidNoMutation=true lateFailureDiscard=true settlementReceiptComposite=true");
    }

    private static DungeonOffenseAggregateSaveData CreateCanonicalPayload()
    {
        return new DungeonOffenseAggregateSaveData
        {
            version = DungeonOffenseAggregateSaveData.CurrentVersion,
            campaign = new DungeonOffenseCampaignSaveData
            {
                version = DungeonOffenseCampaignSaveData.CurrentVersion
            },
            expedition = new DungeonOffenseSaveData
            {
                version = DungeonOffenseSaveData.CurrentVersion
            },
            world = new OffenseWorldSaveData
            {
                version = OffenseWorldSaveData.CurrentVersion,
                worldSeed = 731,
                worldDay = 1,
                worldHour = 0f
            },
            regions = new DungeonOffenseRegionSaveData
            {
                version = DungeonOffenseRegionSaveData.CurrentVersion,
                regions =
                {
                    Region(
                        OffenseRegionRuntime.BorderTradeRegionId,
                        "Border Trade",
                        OffenseRegionRuntime.HumanFactionId),
                    Region(
                        OffenseRegionRuntime.RivalOutpostRegionId,
                        "Rival Outpost",
                        OffenseRegionRuntime.RivalFactionId),
                    Region(
                        OffenseRegionRuntime.SealedZoneRegionId,
                        "Sealed Zone",
                        OffenseRegionRuntime.SealFactionId)
                }
            },
            returnArrivals = new DungeonOffenseReturnArrivalSaveData
            {
                version = DungeonOffenseReturnArrivalSaveData.CurrentVersion,
                nextArrivalSequence = 1
            }
        };
    }

    private static void ValidateReturnArrivalCharacterIds()
    {
        const string legacyId = "return:1:prisoner:1";
        string canonicalId = CharacterId.FromStableSuffix(legacyId).Value;
        DungeonOffenseAggregateSaveData canonical = CreateCanonicalPayload();
        canonical.campaign.knownTargetIds.Add("target:return-id-proof");
        canonical.expedition.resultHistory.Add(
            new DungeonOffenseExpeditionResultSaveData
            {
                expeditionId = "expedition:return-id-proof",
                targetId = "target:return-id-proof",
                targetTitle = "Return ID Proof",
                arrivalReceipts = new List<DungeonOffenseArrivalReceiptSaveData>
                {
                    new()
                    {
                        arrivalId = "return:1",
                        kind = OffenseReturnArrivalKind.Prisoner.ToString(),
                        requestedAmount = 1,
                        materializedAmount = 1,
                        escapedAmount = 1,
                        resolution = OffenseExpeditionArrivalResolution.Escaped
                    }
                }
            });
        canonical.returnArrivals = new DungeonOffenseReturnArrivalSaveData
        {
            version = DungeonOffenseReturnArrivalSaveData.CurrentVersion,
            nextArrivalSequence = 2,
            arrivals = new List<OffenseReturnArrivalState>
            {
                new OffenseReturnArrivalState
                {
                    arrivalId = "return:1",
                    expeditionId = "expedition:return-id-proof",
                    targetId = "target:return-id-proof",
                    kind = OffenseReturnArrivalKind.Prisoner,
                    requestedAmount = 1,
                    stage = OffenseReturnArrivalStage.Escaped,
                    materializedIds = new List<string> { canonicalId },
                    escapedIds = new List<string> { canonicalId },
                    settledMaterializedAmount = 1,
                    settledSecuredAmount = 0,
                    settledEscapedAmount = 1,
                    prisonerIndividuals = new List<EnemyIndividualSaveData>
                    {
                        new EnemyIndividualSaveData
                        {
                            characterId = canonicalId
                        }
                    },
                    lastStatus = "escaped"
                }
            }
        };
        OffenseAggregateRestorePlan canonicalPlan =
            OffenseAggregateSaveValidation.BuildRestorePlan(canonical);
        Require(string.Equals(
                canonicalPlan.Payload.returnArrivals.arrivals[0]
                    .materializedIds[0],
                canonicalId,
                StringComparison.Ordinal),
            "Canonical return-prisoner CharacterId was rejected or changed by validation.");
        Require(canonicalPlan.Payload.expedition.resultHistory[0]
                .arrivalReceipts.Single().resolution
                == OffenseExpeditionArrivalResolution.Escaped,
            "Committed return-arrival result receipt was not preserved.");
        Type validationType = typeof(OffenseReturnArrivalRuntime).Assembly
            .GetType("OffenseReturnArrivalSaveValidation", true);
        MethodInfo createStrictState = validationType.GetMethod(
            "CreateStrictState",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(
                validationType.FullName,
                "CreateStrictState");
        object restoredArrivalState = createStrictState.Invoke(
            null,
            new object[] { canonicalPlan.Payload.returnArrivals, 0f });
        IList restoredArrivals = (IList)restoredArrivalState.GetType()
            .GetProperty(
                "Arrivals",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(restoredArrivalState)
            ?? throw new MissingMemberException(
                restoredArrivalState.GetType().FullName,
                "Arrivals");
        OffenseReturnArrivalState restoredFirst = restoredArrivals
            .Cast<OffenseReturnArrivalState>()
            .Single(value => value.arrivalId == "return:1");
        Require(restoredFirst.settledMaterializedAmount == 1
                && restoredFirst.settledSecuredAmount == 0
                && restoredFirst.settledEscapedAmount == 1,
            "Restoring terminal arrival A discarded its frozen settlement counts before arrival B could settle.");
        restoredArrivals.Add(new OffenseReturnArrivalState
        {
            arrivalId = "return:2",
            expeditionId = "expedition:return-id-proof-2",
            targetId = "target:return-id-proof-2",
            kind = OffenseReturnArrivalKind.SpecialWildlife,
            requestedAmount = 1,
            stage = OffenseReturnArrivalStage.Secured,
            settledMaterializedAmount = 1,
            settledSecuredAmount = 1,
            settledEscapedAmount = 0
        });
        Require(restoredFirst.settledMaterializedAmount == 1
                && restoredFirst.settledEscapedAmount == 1,
            "Settling arrival B rewrote restored terminal arrival A.");

        DungeonOffenseAggregateSaveData foreignOwner =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(
                JsonUtility.ToJson(canonical));
        DungeonOffenseExpeditionResultSaveData copiedResult =
            JsonUtility.FromJson<DungeonOffenseExpeditionResultSaveData>(
                JsonUtility.ToJson(
                    foreignOwner.expedition.resultHistory.Single()));
        copiedResult.expeditionId = "expedition:return-id-proof-foreign";
        copiedResult.targetId = "target:return-id-proof-foreign";
        copiedResult.targetTitle = "Foreign Arrival Owner";
        foreignOwner.campaign.knownTargetIds.Add(copiedResult.targetId);
        foreignOwner.expedition.resultHistory.Add(copiedResult);
        Require(RejectsOffenseAggregate(foreignOwner)
                && CapturePreflightRejectsArrivalJoin(foreignOwner),
            "A return-arrival receipt copied into a foreign expedition passed restore or capture ownership validation.");

        DungeonOffenseAggregateSaveData legacy =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(
                JsonUtility.ToJson(canonical));
        legacy.returnArrivals.arrivals[0].materializedIds[0] = legacyId;
        legacy.returnArrivals.arrivals[0].escapedIds[0] = legacyId;
        string legacyBefore = JsonUtility.ToJson(legacy);
        OffenseAggregateSaveValidation.BuildRestorePlan(legacy);
        Require(string.Equals(
                    JsonUtility.ToJson(legacy),
                    legacyBefore,
                    StringComparison.Ordinal),
            "Early-V18 return-prisoner ID was rejected or mutated at source.");

        DungeonOffenseAggregateSaveData malformed =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(legacyBefore);
        malformed.returnArrivals.arrivals[0].materializedIds[0] =
            "return:1:prisoner:01";
        Require(RejectsOffenseAggregate(malformed),
            "Return-prisoner compatibility accepted a non-exact legacy ID.");

        DungeonOffenseAggregateSaveData duplicate =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(legacyBefore);
        duplicate.returnArrivals.arrivals[0].requestedAmount = 2;
        duplicate.returnArrivals.arrivals[0].materializedIds.Add(canonicalId);
        Require(RejectsOffenseAggregate(duplicate),
            "Raw and canonical aliases bypassed duplicate return-prisoner detection.");
    }

    private static void ValidateSettlementReceiptCompositeKeys()
    {
        DungeonOffenseAggregateSaveData legal = CreateCanonicalPayload();
        legal.campaign.knownTargetIds.Add("target:receipt-key-proof");
        legal.expedition.resultHistory.Add(
            new DungeonOffenseExpeditionResultSaveData
            {
                expeditionId = "expedition:receipt-key-proof",
                targetId = "target:receipt-key-proof",
                targetTitle = "Receipt Key Proof",
                itemReceipts = new List<DungeonOffenseItemReceiptSaveData>
                {
                    Receipt("ammo:bolt-iron", "equipment:crossbow:proof"),
                    Receipt("ammo:incendiary-bolt", "equipment:crossbow:proof")
                }
            });
        OffenseAggregateSaveValidation.BuildRestorePlan(legal);

        DungeonOffenseAggregateSaveData duplicate =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(
                JsonUtility.ToJson(legal));
        duplicate.expedition.resultHistory[0].itemReceipts.Add(
            Receipt("ammo:bolt-iron", "equipment:crossbow:proof"));
        Require(RejectsOffenseAggregate(duplicate),
            "Duplicate kind/item/instance settlement receipt was accepted.");

        DungeonOffenseAggregateSaveData whitespaceItem =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(
                JsonUtility.ToJson(legal));
        whitespaceItem.expedition.resultHistory[0].itemReceipts[0].itemId =
            " ammo:bolt-iron";
        Require(RejectsOffenseAggregate(whitespaceItem),
            "A noncanonical whitespace item ID was accepted before receipt-key validation.");

        DungeonOffenseAggregateSaveData whitespaceInstance =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(
                JsonUtility.ToJson(legal));
        whitespaceInstance.expedition.resultHistory[0].itemReceipts[0]
            .instanceId = "equipment:crossbow:proof ";
        Require(RejectsOffenseAggregate(whitespaceInstance),
            "A noncanonical whitespace instance ID was accepted before receipt-key validation.");
    }

    private static DungeonOffenseItemReceiptSaveData Receipt(
        string itemId,
        string instanceId) => new()
    {
        kind = OffenseExpeditionItemReceiptKind.AmmunitionConsumed,
        itemId = itemId,
        quantity = 1,
        instanceId = instanceId,
        valuation = new DungeonOffenseItemValuationSaveData
        {
            itemId = itemId,
            quantity = 1,
            state = OffenseSettlementValuationState.UnvaluedItem
        }
    };

    private static bool RejectsOffenseAggregate(
        DungeonOffenseAggregateSaveData payload)
    {
        try
        {
            OffenseAggregateSaveValidation.BuildRestorePlan(payload);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool CapturePreflightRejectsArrivalJoin(
        DungeonOffenseAggregateSaveData payload)
    {
        MethodInfo method = typeof(DungeonAggregateReferencePreflight).GetMethod(
            "ValidateOffenseArrivalResultJoins",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(
                typeof(DungeonAggregateReferencePreflight).FullName,
                "ValidateOffenseArrivalResultJoins");
        DungeonGameRestoreReport report = new();
        method.Invoke(null, new object[] { payload, report });
        return !report.Success;
    }

    private static OffenseRegionState Region(
        string regionId,
        string displayName,
        string factionId)
    {
        return new OffenseRegionState
        {
            regionId = regionId,
            displayName = displayName,
            factionId = factionId
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class DiscardProbeCandidate :
        IDungeonDiscardableRestoreCandidate
    {
        internal bool IsDiscarded { get; private set; }

        public void Discard()
        {
            IsDiscarded = true;
        }
    }
}
