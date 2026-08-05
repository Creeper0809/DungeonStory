using System.Collections.Generic;
using UnityEngine;

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
    CharacterCombatCommandRestoreCandidate PrepareRestore(
        CharacterCombatCommandSaveData saveData);
    void PublishRestore(CharacterCombatCommandRestoreCandidate candidate);
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
