using System;
using System.Collections.Generic;
using UnityEngine;

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

[Serializable]
public sealed class CharacterCombatCommandSaveData
{
    public List<string> stanceCharacterIds = new List<string>();
    public List<CharacterCombatCommand> commands = new List<CharacterCombatCommand>();
}

public interface ICharacterCombatCommandRuntime
{
    IReadOnlyList<CharacterCombatCommand> ActiveCommands { get; }
    bool IsInCombatStance(CharacterActor actor);
    bool SetCombatStance(CharacterActor actor, bool enabled, out string message);
    bool TryIssueMove(CharacterActor actor, Vector2Int destination, out string message);
    bool TryIssueMoveToCover(
        CharacterActor actor,
        Vector2Int destination,
        out string message);
    bool TryIssueAttack(
        CharacterActor actor,
        CombatParticipantRef target,
        bool forceFire,
        out string message);
    bool TryIssueForceFireAtCell(
        CharacterActor actor,
        Vector2Int targetCell,
        out string message);
    bool TryIssueReload(CharacterActor actor, out string message);
    bool TryIssueSwitchWeapon(CharacterActor actor, out string message);
    bool TrySetFireMode(CharacterActor actor, CombatFireMode mode, out string message);
    bool TrySetHoldFire(CharacterActor actor, bool holdFire, out string message);
    bool TryIssueRescue(CharacterActor rescuer, CharacterActor patient, out string message);
    bool TryGetCommand(CharacterActor actor, out CharacterCombatCommand command);
    void CancelCommand(CharacterActor actor, string reason);
    CharacterCombatCommandSaveData Capture();
    void Restore(CharacterCombatCommandSaveData saveData, IList<string> warnings);
}

public interface IPlayerCombatCommandSource
{
    CharacterActor SelectedActor { get; }
    IReadOnlyList<CharacterActor> SelectedActors { get; }
    CombatCommandType CombatInputMode { get; }
    bool HasCombatStanceSelection { get; }
    void SetCombatInputMode(CombatCommandType mode);
    bool ToggleSelectedCombatStance(out string message);
    bool TryReloadSelected(out string message);
    bool TrySwitchSelectedWeapons(out string message);
    bool TrySetSelectedFireMode(CombatFireMode mode, out string message);
    bool TrySetSelectedHoldFire(bool holdFire, out string message);
}

public sealed class UnavailablePlayerCombatCommandSource :
    IPlayerCombatCommandSource
{
    private static readonly IReadOnlyList<CharacterActor> NoSelection =
        Array.Empty<CharacterActor>();

    public CharacterActor SelectedActor => null;
    public IReadOnlyList<CharacterActor> SelectedActors => NoSelection;
    public CombatCommandType CombatInputMode => CombatCommandType.None;
    public bool HasCombatStanceSelection => false;

    public void SetCombatInputMode(CombatCommandType mode)
    {
    }

    public bool ToggleSelectedCombatStance(out string message)
    {
        return Unavailable(out message);
    }

    public bool TryReloadSelected(out string message)
    {
        return Unavailable(out message);
    }

    public bool TrySwitchSelectedWeapons(out string message)
    {
        return Unavailable(out message);
    }

    public bool TrySetSelectedFireMode(
        CombatFireMode mode,
        out string message)
    {
        return Unavailable(out message);
    }

    public bool TrySetSelectedHoldFire(bool holdFire, out string message)
    {
        return Unavailable(out message);
    }

    private static bool Unavailable(out string message)
    {
        message = "이 씬에서는 전투 명령을 사용할 수 없습니다.";
        return false;
    }
}
