using System;

internal sealed class OwnerCommandTargetingSession
{
    internal sealed class Intent
    {
        internal Intent(CombatCommandType combatMode, CharacterActor source,
            string skillId, long selectionRevision)
        {
            CombatMode = combatMode;
            ManualSource = source;
            ManualSkillId = skillId;
            SelectionRevision = selectionRevision;
        }

        internal CombatCommandType CombatMode { get; }
        internal CharacterActor ManualSource { get; }
        internal string ManualSkillId { get; }
        internal long SelectionRevision { get; }
        internal bool IsManual => !string.IsNullOrEmpty(ManualSkillId);
        internal bool IsCompleting { get; set; }
    }

    private Intent current;

    internal Intent Current => current;
    internal bool HasPending => current != null;
    internal bool HasManual => current?.IsManual == true;
    internal CombatCommandType CombatMode => current?.CombatMode ?? CombatCommandType.None;

    internal void BeginCombat(CombatCommandType mode, long selectionRevision)
    {
        current = mode == CombatCommandType.None
            ? null
            : new Intent(mode, null, null, selectionRevision);
    }

    internal void BeginManual(CharacterActor source, string skillId, long selectionRevision)
    {
        if (ReferenceEquals(source, null))
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(skillId))
            throw new ArgumentException("A manual targeting intent requires a skill ID.", nameof(skillId));
        current = new Intent(CombatCommandType.None, source, skillId, selectionRevision);
    }

    internal bool TryBeginManualCompletion(out Intent intent)
    {
        intent = current;
        if (intent == null || !intent.IsManual || intent.IsCompleting)
            return false;
        intent.IsCompleting = true;
        return true;
    }

    internal void FinishManualCompletion(Intent intent, bool success)
    {
        if (intent == null)
            return;
        intent.IsCompleting = false;
        if (success)
            Consume(intent);
    }

    internal void Consume(Intent intent)
    {
        // A synchronous domain observer may have installed a newer intent.
        if (ReferenceEquals(current, intent))
            current = null;
    }

    internal void SynchronizeSelection(long revision)
    {
        if (current != null && current.SelectionRevision != revision)
            current = null;
    }

    internal bool Cancel()
    {
        bool cancelled = current != null;
        current = null;
        return cancelled;
    }
}
