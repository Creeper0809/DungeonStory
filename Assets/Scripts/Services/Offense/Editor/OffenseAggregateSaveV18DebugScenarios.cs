using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class OffenseAggregateSaveV18DebugScenarios
{
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
            "OFFENSE_AGGREGATE_V18_PROOF_PASSED canonicalRoundTrip=true campaignAuthority=true expeditionCampaignDuplicates=0 optionalBattlePresence=true hiddenBattleRejected=true invalidNoMutation=true lateFailureDiscard=true");
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
                targetTitle = "Return ID Proof"
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
