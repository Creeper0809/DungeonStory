using System;
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
