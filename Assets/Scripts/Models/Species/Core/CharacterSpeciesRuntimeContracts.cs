using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesRuntimeState
{
    public CharacterId CharacterId { get; set; }
    public CharacterSpeciesId SpeciesId { get; set; }
    public float Charge { get; set; } = 100f;
    public float Integrity { get; set; } = 100f;
    public float NextIncidentAt { get; set; }
    public string LastIncidentId { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public float WearWorkRemainder { get; set; }
    public int CompletedWorkIndex { get; set; }
    public string RechargeWorkerId { get; set; } = string.Empty;
    public string RechargeFacilityId { get; set; } = string.Empty;
    public string RechargeMaterialStackId { get; set; } = string.Empty;
    public float RechargeProgressWork { get; set; }
    public string RechargeOperationId { get; set; } = string.Empty;
    public long RechargeEpisodeRevision { get; set; }
    public bool RechargeMaterialCommitted { get; set; }
    public bool RechargeChargeApplied { get; set; }
    public bool RechargeObservationCompleted { get; set; }
    public string RechargePhysicalEvidence { get; set; } = string.Empty;
    public float RechargeChargeBefore { get; set; }
    public float RechargeRequestedCharge { get; set; }
    public long GolemEpisodeRevision { get; set; }
    public bool DischargeEpisodeActive { get; set; }
    public List<CharacterSpeciesWorkContributionState> AppliedWorkContributions
        { get; set; } = new();

    public CharacterSpeciesRuntimeState Clone() => new()
    {
        CharacterId = CharacterId,
        SpeciesId = SpeciesId,
        Charge = Charge,
        Integrity = Integrity,
        NextIncidentAt = NextIncidentAt,
        LastIncidentId = LastIncidentId ?? string.Empty,
        IncidentCount = IncidentCount,
        WearWorkRemainder = WearWorkRemainder,
        CompletedWorkIndex = CompletedWorkIndex,
        RechargeWorkerId = RechargeWorkerId ?? string.Empty,
        RechargeFacilityId = RechargeFacilityId ?? string.Empty,
        RechargeMaterialStackId = RechargeMaterialStackId ?? string.Empty,
        RechargeProgressWork = RechargeProgressWork,
        RechargeOperationId = RechargeOperationId ?? string.Empty,
        RechargeEpisodeRevision = RechargeEpisodeRevision,
        RechargeMaterialCommitted = RechargeMaterialCommitted,
        RechargeChargeApplied = RechargeChargeApplied,
        RechargeObservationCompleted = RechargeObservationCompleted,
        RechargePhysicalEvidence = RechargePhysicalEvidence ?? string.Empty,
        RechargeChargeBefore = RechargeChargeBefore,
        RechargeRequestedCharge = RechargeRequestedCharge,
        GolemEpisodeRevision = GolemEpisodeRevision,
        DischargeEpisodeActive = DischargeEpisodeActive,
        AppliedWorkContributions = (AppliedWorkContributions
                ?? new List<CharacterSpeciesWorkContributionState>())
            .ConvertAll(value => value?.Clone())
    };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesWorkContributionState
{
    public string SourceOperationId { get; set; } = string.Empty;
    public string WorkTypeId { get; set; } = string.Empty;
    public float CompletedWork { get; set; }

    public CharacterSpeciesWorkContributionState Clone() => new()
    {
        SourceOperationId = SourceOperationId ?? string.Empty,
        WorkTypeId = WorkTypeId ?? string.Empty,
        CompletedWork = CompletedWork
    };
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesRuntimeRecordSaveData
{
    public string characterInstanceId = string.Empty;
    public string speciesDefinitionId = string.Empty;
    public float charge = 100f;
    public float integrity = 100f;
    public float nextIncidentAt;
    public string lastIncidentId = string.Empty;
    public int incidentCount;
    public float wearWorkRemainder;
    public int completedWorkIndex;
    public string rechargeWorkerId = string.Empty;
    public string rechargeFacilityId = string.Empty;
    public string rechargeMaterialStackId = string.Empty;
    public float rechargeProgressWork;
    public string rechargeOperationId = string.Empty;
    public long rechargeEpisodeRevision;
    public bool rechargeMaterialCommitted;
    public bool rechargeChargeApplied;
    public bool rechargeObservationCompleted;
    public string rechargePhysicalEvidence = string.Empty;
    public float rechargeChargeBefore;
    public float rechargeRequestedCharge;
    public long golemEpisodeRevision;
    public bool dischargeEpisodeActive;
    public List<CharacterSpeciesWorkContributionSaveData> appliedWorkContributions = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesWorkContributionSaveData
{
    public string sourceOperationId = string.Empty;
    public string workTypeId = string.Empty;
    public float completedWork;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesRuntimeSaveData
{
    public const int CurrentVersion = 5;
    public int version = CurrentVersion;
    public List<CharacterSpeciesRuntimeRecordSaveData> characters = new();
    public List<SpeciesActionObservationSaveData> pendingObservations = new();
}

/// <summary>Pending delivery or recharge-worker acknowledgement; not a completed history.</summary>
[Serializable]
public sealed class SpeciesActionObservationSaveData
{
    public int actionType; // 0 upkeep, 1 species incident, 2 physical gale
    public string publicationKey = string.Empty;
    public bool rollbackBlocked;
    public string rollbackFailure = string.Empty;
    public GolemUpkeepFactSaveData golem;
    public SpeciesIncidentFactSaveData incident;
    public HarpyGaleFactSaveData harpy;

    public string CharacterId => actionType == 0 ? golem?.characterId : actionType == 1 ? incident?.characterId : harpy?.characterId;
    public string OperationId => actionType == 0 ? golem?.operationId : actionType == 1 ? incident?.operationId : harpy?.operationId;
    public long Revision => (actionType == 0 ? golem?.revision : actionType == 1 ? incident?.revision : harpy?.revision) ?? 0L;
    public SpeciesActionObservationSaveData Clone() => new()
    {
        publicationKey = publicationKey, actionType = actionType,
        rollbackBlocked = rollbackBlocked, rollbackFailure = rollbackFailure,
        golem = actionType == 0 ? golem?.Clone() : null,
        incident = actionType == 1 ? incident?.Clone() : null,
        harpy = actionType == 2 ? harpy?.Clone() : null
    };

    public void Validate()
    {
        if (actionType < 0 || actionType > 2
            || (actionType == 0 ? golem == null : actionType == 1 ? incident == null : harpy == null)
            || !DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(publicationKey, 1024, false)
            || publicationKey != publicationKey.Trim())
            throw new InvalidOperationException("species-observation-invalid");
        if (!DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(rollbackFailure, 4096, !rollbackBlocked)
            || !rollbackBlocked && rollbackFailure.Length != 0)
            throw new InvalidOperationException("species-observation-rollback-invalid");
        if (actionType == 0) golem.Validate();
        else if (actionType == 1) incident.Validate();
        else harpy.Validate();
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SpeciesIncidentTriggeredEvent
{
    public SpeciesIncidentTriggeredEvent(
        CharacterId characterId,
        CharacterSpeciesId speciesId,
        string incidentId,
        Vector2Int position,
        string summary)
    {
        CharacterId = characterId;
        SpeciesId = speciesId;
        IncidentId = incidentId ?? string.Empty;
        Position = position;
        Summary = summary ?? string.Empty;
    }

    public CharacterId CharacterId { get; }
    public CharacterSpeciesId SpeciesId { get; }
    public string IncidentId { get; }
    public Vector2Int Position { get; }
    public string Summary { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICharacterSpeciesQuery
{
    bool TryGet(
        CharacterId characterId,
        out CharacterSpeciesRuntimeState state);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICharacterSpeciesCommand
{
    bool RepairIntegrity(
        CharacterId characterId,
        float amount,
        out DomainFailure failure);
    bool RecordCompletedWork(
        CharacterId characterId,
        string workTypeId,
        float completedWork,
        out DomainFailure failure);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICharacterSpeciesContributionCommand
{
    bool RecordCompletedWork(
        CharacterId characterId,
        string sourceOperationId,
        string workTypeId,
        float completedWork,
        out DomainFailure failure);
}
