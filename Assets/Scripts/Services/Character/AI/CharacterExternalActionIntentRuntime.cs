using System;

internal enum CharacterExternalIntentAdmission { Rejected, Renewed, Begin, Preempt }

internal sealed class CharacterExternalActionIntentRuntime
{
    private bool manualCommandActive;
    private bool externallyDrivenActionActive;
    private bool externalReplanClearFailures;
    private string externalIntentOwnerId = string.Empty;
    private CharacterActionIntentKind externalIntentKind;
    private long externalIntentEpoch;
    private int externalIntentTransitionCount;
    private int externalIntentPreemptionCount;
    private int externalIntentRejectedCount;
    private int externalIntentStaleCompletionCount;
    private CharacterAiActionTerminalKind lastExternalIntentTerminalKind;
    private int externalIntentTerminalCount;

    internal CharacterExternalIntentAdmission Admit(string ownerId, CharacterActionIntentKind kind)
    {
        if (manualCommandActive)
        {
            externalIntentRejectedCount++;
            return CharacterExternalIntentAdmission.Rejected;
        }

        if (string.IsNullOrWhiteSpace(ownerId)
            || kind == CharacterActionIntentKind.None)
        {
            externalIntentRejectedCount++;
            return CharacterExternalIntentAdmission.Rejected;
        }

        if (externallyDrivenActionActive)
        {
            if (string.Equals(
                    externalIntentOwnerId,
                    ownerId,
                    StringComparison.Ordinal))
            {
                return CharacterExternalIntentAdmission.Renewed;
            }

            if (kind <= externalIntentKind)
            {
                externalIntentRejectedCount++;
                return CharacterExternalIntentAdmission.Rejected;
            }

            return CharacterExternalIntentAdmission.Preempt;
        }

        return CharacterExternalIntentAdmission.Begin;
    }

    internal void Begin(string ownerId, CharacterActionIntentKind kind)
    {
        externallyDrivenActionActive = true;
        externalIntentOwnerId = ownerId;
        externalIntentKind = kind;
        externalIntentEpoch = checked(externalIntentEpoch + 1L);
    }

    internal void RecordTransition(bool preempting)
    {
        externalIntentTransitionCount++;
        if (preempting)
        {
            externalIntentPreemptionCount++;
            lastExternalIntentTerminalKind =
                CharacterAiActionTerminalKind.Cancelled;
            externalIntentTerminalCount++;
        }
        else
        {
            lastExternalIntentTerminalKind =
                CharacterAiActionTerminalKind.None;
        }
        externalReplanClearFailures = false;
    }

    internal CharacterActionIntentLease CurrentLease => new CharacterActionIntentLease(externalIntentOwnerId, externalIntentKind, externalIntentEpoch);
    internal void BeginManualCommand() => manualCommandActive = true;
    internal void EndManualCommand() => manualCommandActive = false;
    internal void RequestClearFailures(bool clear) => externalReplanClearFailures |= clear;
    internal void RejectUpdate() => externalIntentRejectedCount++;
    internal void RejectStaleCompletion() => externalIntentStaleCompletionCount++;
    internal void ResetLifecyclePresence()
    {
        manualCommandActive = false;
        externallyDrivenActionActive = false;
        externalIntentOwnerId = string.Empty;
        externalIntentKind = CharacterActionIntentKind.None;
    }

    internal bool OwnsExternalIntent(in CharacterActionIntentLease lease)
    {
        return externallyDrivenActionActive
            && lease.IsValid
            && lease.Epoch == externalIntentEpoch
            && lease.Kind == externalIntentKind
            && string.Equals(
                lease.OwnerId,
                externalIntentOwnerId,
                StringComparison.Ordinal);
    }

    internal bool End(bool clearFailures, CharacterAiActionTerminalKind terminalKind)
    {
        bool shouldClearFailures =
            clearFailures || externalReplanClearFailures;
        externallyDrivenActionActive = false;
        externalIntentOwnerId = string.Empty;
        externalIntentKind = CharacterActionIntentKind.None;
        externalReplanClearFailures = false;
        lastExternalIntentTerminalKind = terminalKind ==
            CharacterAiActionTerminalKind.None
                ? CharacterAiActionTerminalKind.Completed
                : terminalKind;
        externalIntentTerminalCount++;
        return shouldClearFailures;
    }

    internal bool ManualCommandActive => manualCommandActive;
    internal bool ExternallyDrivenActionActive => externallyDrivenActionActive;
    internal string ExternalIntentOwnerId => externalIntentOwnerId;
    internal CharacterActionIntentKind ExternalIntentKind => externalIntentKind;
    internal long ExternalIntentEpoch => externalIntentEpoch;
    internal int ExternalIntentTransitionCount => externalIntentTransitionCount;
    internal int ExternalIntentPreemptionCount => externalIntentPreemptionCount;
    internal int ExternalIntentRejectedCount => externalIntentRejectedCount;
    internal int ExternalIntentStaleCompletionCount => externalIntentStaleCompletionCount;
    internal CharacterAiActionTerminalKind LastExternalIntentTerminalKind => lastExternalIntentTerminalKind;
    internal int ExternalIntentTerminalCount => externalIntentTerminalCount;
}
