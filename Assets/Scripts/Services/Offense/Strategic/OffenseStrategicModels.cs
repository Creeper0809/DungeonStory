using System;
using System.Collections.Generic;
using UnityEngine;

public enum OffenseHexTerrain
{
    Plains,
    Forest,
    Hills,
    Marsh,
    Mountain,
    River
}

public enum OffenseWorldSiteState
{
    Hidden,
    Revealed,
    Engaged,
    Resolved,
    Expired
}

public enum OffenseUrgentSiteStage
{
    Signal,
    Warning,
    Crisis,
    Withdrawing,
    Expired,
    Destroyed
}

public enum OffenseTacticalTag
{
    None,
    Intercept,
    Maneuver,
    Break,
    Support,
    Execute
}

public enum OffenseChainState
{
    Full,
    Degraded,
    Residual,
    Broken
}

public enum OffenseCommandOutcome
{
    Executed,
    ClashLost,
    Unavailable,
    Retargeted,
    Cancelled,
    IllegalTarget
}

public enum OffenseFormationPosition
{
    FrontLeft,
    FrontRight,
    MiddleLeft,
    MiddleRight,
    RearLeft,
    RearRight
}

[Serializable]
public sealed class OffenseHexTileState
{
    public int q;
    public int r;
    public OffenseHexTerrain terrain;
    public string regionId;
    public bool hasRoad;
    public bool hasRiver;
    public bool blocked;

    public OffenseHexCoord Coord => new OffenseHexCoord(q, r);
}

[Serializable]
public sealed class OffenseWorldSiteStateData
{
    public string siteId;
    public string archetypeId;
    public string displayName;
    public int q;
    public int r;
    public string regionId;
    public string factionId;
    public OffenseWorldSiteState state;
    public bool fixedBoss;
    public int strength;
    public int createdDay;
    public int expiresDay;
    public StrategicPressureAxis pressureAxis;
    public float pressureAmount;

    public OffenseHexCoord Coord => new OffenseHexCoord(q, r);
    public bool IsActive => state is OffenseWorldSiteState.Hidden
        or OffenseWorldSiteState.Revealed
        or OffenseWorldSiteState.Engaged;
}

[Serializable]
public sealed class OffenseUrgentSiteStateData
{
    public string siteId;
    public string definitionId;
    public string displayName;
    public int q;
    public int r;
    public OffenseThreatModifierKind modifierKind;
    public OffenseUrgentSiteStage stage;
    public float stageElapsedHours;
    public float mitigation;

    public OffenseHexCoord Coord => new OffenseHexCoord(q, r);
    public bool IsActive => stage is not OffenseUrgentSiteStage.Expired
        and not OffenseUrgentSiteStage.Destroyed;

    public float Intensity => stage switch
    {
        OffenseUrgentSiteStage.Signal => 0.25f,
        OffenseUrgentSiteStage.Warning => 0.55f,
        OffenseUrgentSiteStage.Crisis => 1f,
        OffenseUrgentSiteStage.Withdrawing => 1f,
        _ => 0f
    };
}

[Serializable]
public sealed class OffenseTravelStateData
{
    public string expeditionId;
    public int currentQ;
    public int currentR;
    public int destinationQ;
    public int destinationR;
    public string destinationSiteId;
    public List<OffenseHexCoordSaveData> remainingPath = new List<OffenseHexCoordSaveData>();
    public float progressToNextTile;
    public float exposure;
    public bool pausedForDecision;
    public bool pausedForBattle;
    public int eventSequence;
    public float movementTimeMultiplier = 1f;
    public bool stranded;
    public string strandedReason = string.Empty;

    public OffenseHexCoord CurrentCoord => new OffenseHexCoord(currentQ, currentR);
    public OffenseHexCoord DestinationCoord => new OffenseHexCoord(destinationQ, destinationR);
}

[Serializable]
public sealed class FieldStabilizationState
{
    public string expeditionId = string.Empty;
    public string characterId = string.Empty;
    public string anatomyNodeId = string.Empty;
    public string consumedKitInstanceId = string.Empty;
    public bool active;
    public bool usedForNode;
    public float locomotionFloor = 0.5f;
    public float sustainFloor = 0.5f;
    public int appliedEventSequence;
}

[Serializable]
public sealed class OffenseCasualtyCarryState
{
    public string expeditionId = string.Empty;
    public string casualtyCharacterId = string.Empty;
    public string carrierCharacterId = string.Empty;
    public float casualtyBodyWeight;
    public float casualtyEquipmentWeight;
    public bool active;
}

[Serializable]
public sealed class OffenseStrandedState
{
    public string expeditionId = string.Empty;
    public int q;
    public int r;
    public float remainingSupply;
    public float estimatedSurvivalHours;
    public string reason = string.Empty;
    public bool active;
}

[Serializable]
public sealed class RescueConvoyState
{
    public string rescueExpeditionId = string.Empty;
    public string strandedExpeditionId = string.Empty;
    public bool dispatched;
    public bool merged;
    public List<string> rescuerCharacterIds = new List<string>();
    public List<string> protectedCasualtyIds = new List<string>();
}

[Serializable]
public sealed class OffenseHexCoordSaveData
{
    public int q;
    public int r;

    public OffenseHexCoord ToCoord() => new OffenseHexCoord(q, r);

    public static OffenseHexCoordSaveData From(OffenseHexCoord coord)
    {
        return new OffenseHexCoordSaveData { q = coord.Q, r = coord.R };
    }
}

[Serializable]
public sealed class OffenseReturnSafetyStateData
{
    public string expeditionId;
    public int safeStepBudget;
    public int protectedForcedCombatCount;
    public int nonCombatPitySteps;
}

[Serializable]
public sealed class OffenseDecisionStateData
{
    public string expeditionId;
    public string cardId;
    public int sequence;
    public OffenseDecisionStage stage;
    public int deterministicRoll;
    public bool resolved;
    public string selectedChoiceId;
}

[Serializable]
public sealed class OffenseCommandCardStateData
{
    public string instanceId;
    public OffenseBattleActionType actionType;
    public string sourceSkillId;
    public string displayName;
    public OffenseTacticalTag tacticalTag;
    public CombatDamageType damageType;
    public int executionStages;
    public int speed;
    public int power;
    public bool heldFromPreviousTurn;
}

[Serializable]
public sealed class OffenseCommandDeckStateData
{
    public string characterId;
    public List<OffenseCommandCardStateData> drawPile = new List<OffenseCommandCardStateData>();
    public List<OffenseCommandCardStateData> discardPile = new List<OffenseCommandCardStateData>();
    public List<OffenseCommandCardStateData> candidates = new List<OffenseCommandCardStateData>();
    public string heldCardInstanceId;
    public int shuffleCount;
    public float resolve;
    public bool ultimateUsed;
}

[Serializable]
public sealed class OffenseEnemyIntentStateData
{
    public string intentId;
    public string enemyId;
    public string targetCharacterId;
    public OffenseBattleActionType actionType;
    public string actionId;
    public string displayName;
    public OffenseTacticalTag tacticalTag;
    public int executionStages;
    public int speed;
    public int threat;
}

[Serializable]
public sealed class OffenseCommandQueueEntryData
{
    public int order;
    public string characterId;
    public string cardInstanceId;
    public string targetIntentId;
    public string targetCombatantId;
    public OffenseChainState chainState;
    public float inheritedChainMultiplier;
}

[Serializable]
public sealed class OffenseBattleDirectorStateData
{
    public string battleId;
    public int turn;
    public int resolutionAppliedTurn;
    public int finalizedTurn;
    public ulong rngState;
    public List<OffenseCommandDeckStateData> decks = new List<OffenseCommandDeckStateData>();
    public List<OffenseEnemyIntentStateData> enemyIntents = new List<OffenseEnemyIntentStateData>();
    public List<OffenseCommandQueueEntryData> commandQueue = new List<OffenseCommandQueueEntryData>();
}

[Serializable]
public sealed class OffenseWorldSaveData
{
    public const int CurrentVersion = 7;

    public int version = CurrentVersion;
    public int worldSeed;
    public int worldDay;
    public float worldHour;
    public List<OffenseHexTileState> tiles = new List<OffenseHexTileState>();
    public List<OffenseWorldSiteStateData> sites = new List<OffenseWorldSiteStateData>();
    public List<OffenseUrgentSiteStateData> urgentSites = new List<OffenseUrgentSiteStateData>();
    public List<OffenseTravelStateData> travelStates = new List<OffenseTravelStateData>();
    public List<OffenseReturnSafetyStateData> returnSafety = new List<OffenseReturnSafetyStateData>();
    public List<OffenseDecisionStateData> decisions = new List<OffenseDecisionStateData>();
    public List<OffenseBattleDirectorStateData> battles = new List<OffenseBattleDirectorStateData>();
    public List<OffenseUrgentMitigationOrderStateData> mitigationOrders =
        new List<OffenseUrgentMitigationOrderStateData>();
    public List<OffenseSupplyPackingStateData> supplyPackages =
        new List<OffenseSupplyPackingStateData>();
    public List<FieldStabilizationState> fieldStabilizations =
        new List<FieldStabilizationState>();
    public List<OffenseCasualtyCarryState> casualtyCarries =
        new List<OffenseCasualtyCarryState>();
    public List<OffenseStrandedState> strandedExpeditions =
        new List<OffenseStrandedState>();
    public List<RescueConvoyState> rescueConvoys =
        new List<RescueConvoyState>();
}

public readonly struct OffenseTravelProfile
{
    public OffenseTravelProfile(
        float roadMultiplier,
        float weatherMultiplier,
        float injuryMultiplier,
        float loadMultiplier)
    {
        RoadMultiplier = Mathf.Max(0.1f, roadMultiplier);
        WeatherMultiplier = Mathf.Max(0.1f, weatherMultiplier);
        InjuryMultiplier = Mathf.Max(0.1f, injuryMultiplier);
        LoadMultiplier = Mathf.Max(0.1f, loadMultiplier);
    }

    public float RoadMultiplier { get; }
    public float WeatherMultiplier { get; }
    public float InjuryMultiplier { get; }
    public float LoadMultiplier { get; }

    public static OffenseTravelProfile Default =>
        new OffenseTravelProfile(0.65f, 1f, 1f, 1f);
}

public readonly struct OffenseThreatModifierSnapshot
{
    public OffenseThreatModifierSnapshot(
        OffenseThreatModifierKind kind,
        float rawStrength,
        float mitigation,
        float effectiveStrength,
        int sourceCount)
    {
        Kind = kind;
        RawStrength = Mathf.Max(0f, rawStrength);
        Mitigation = Mathf.Clamp01(mitigation);
        EffectiveStrength = Mathf.Max(0f, effectiveStrength);
        SourceCount = Mathf.Max(0, sourceCount);
    }

    public OffenseThreatModifierKind Kind { get; }
    public float RawStrength { get; }
    public float Mitigation { get; }
    public float EffectiveStrength { get; }
    public int SourceCount { get; }
}

public readonly struct OffenseChainResolution
{
    public OffenseChainResolution(
        OffenseChainState state,
        float multiplier,
        OffenseTacticalTag lastTag,
        int skippedUnavailableSlots)
    {
        State = state;
        Multiplier = Mathf.Clamp01(multiplier);
        LastTag = lastTag;
        SkippedUnavailableSlots = Mathf.Max(0, skippedUnavailableSlots);
    }

    public OffenseChainState State { get; }
    public float Multiplier { get; }
    public OffenseTacticalTag LastTag { get; }
    public int SkippedUnavailableSlots { get; }
}
