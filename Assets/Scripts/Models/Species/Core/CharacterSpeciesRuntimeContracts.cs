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
        RechargeProgressWork = RechargeProgressWork
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
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSpeciesRuntimeSaveData
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public List<CharacterSpeciesRuntimeRecordSaveData> characters = new();
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
