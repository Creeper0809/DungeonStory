using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatCommandType
{
    None = 0,
    Move = 1,
    Attack = 2,
    ForceFire = 3,
    MoveToCover = 4,
    SwitchWeapon = 5,
    Reload = 6,
    SetFireMode = 7,
    HoldFire = 8,
    Rescue = 9
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterCombatCommandState
{
    Queued = 0,
    Moving = 1,
    Aiming = 2,
    Executing = 3,
    WaitingForAmmo = 4,
    Blocked = 5,
    Completed = 6,
    Cancelled = 7
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatCommand
{
    public string commandId = string.Empty;
    public string actorId = string.Empty;
    public CombatCommandType type;
    public CharacterCombatCommandState state;
    public string targetId = string.Empty;
    public int targetX;
    public int targetY;
    public bool hasTargetCell;
    public bool forceFire;
    public string weaponInstanceId = string.Empty;
    public CombatFireMode fireMode = CombatFireMode.Aimed;
    public string status = string.Empty;
    public float attackCooldownRemaining;
    public float reloadRemaining;
    public int revision;

    public Vector2Int TargetCell
    {
        get => new Vector2Int(targetX, targetY);
        set
        {
            targetX = value.x;
            targetY = value.y;
            hasTargetCell = true;
        }
    }

    public CharacterCombatCommand Clone()
    {
        return (CharacterCombatCommand)MemberwiseClone();
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterCombatCommandTerminatedEvent
{
    public CharacterCombatCommandTerminatedEvent(
        string commandId,
        string actorId,
        CombatCommandType type,
        CharacterCombatCommandState finalState,
        string status)
    {
        CommandId = commandId ?? string.Empty;
        ActorId = actorId ?? string.Empty;
        Type = type;
        FinalState = finalState;
        Status = status ?? string.Empty;
    }

    public string CommandId { get; }
    public string ActorId { get; }
    public CombatCommandType Type { get; }
    public CharacterCombatCommandState FinalState { get; }
    public string Status { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatCommandRevisionSaveData
{
    public string actorId = string.Empty;
    public int revision;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatCommandSaveData
{
    public List<string> stanceCharacterIds = new List<string>();
    public List<CharacterCombatCommand> commands = new List<CharacterCombatCommand>();
    public List<CharacterCombatCommandRevisionSaveData> revisions = new();
    public int commandSequence;
}
