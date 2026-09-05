using System;
using VContainer.Unity;

public sealed class DefenseFacilityInputOwnerLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IDefenseFacilityInputOwnerRuntime owner;
    private string unresolvedFailure = string.Empty;

    public DefenseFacilityInputOwnerLifecycleRuntime(
        IDefenseFacilityInputOwnerRuntime owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Start() => Reconcile();
    public void Tick() => Reconcile();

    public void ValidateBeforeCapture()
    {
        Reconcile();
        if (unresolvedFailure.Length == 0)
            return;
        throw new InvalidOperationException(
            "Defense input ownership is not capture-safe: "
            + unresolvedFailure);
    }

    private void Reconcile()
    {
        unresolvedFailure = owner.TryReconcileLive(out string failureReason)
            ? string.Empty
            : failureReason;
    }
}

/// <summary>
/// Rebuilds the defense input owner pairs inside the claim/profile restore
/// candidates after the facility candidate is published and before those two
/// shared authorities publish. The pairs are derived, not an additional save
/// authority.
/// </summary>
public sealed class DefenseFacilityInputOwnerRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "219.world.defense-facility-input-owners";

    private readonly DefenseFacilityRuntime defense;
    private readonly IDefenseFacilityInputOwnerRuntime owner;
    private bool active;
    private bool published;

    public DefenseFacilityInputOwnerRestoreParticipant(
        DefenseFacilityRuntime defense,
        IDefenseFacilityInputOwnerRuntime owner)
    {
        this.defense = defense ?? throw new ArgumentNullException(nameof(defense));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Defense input owner restore is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Defense input owner restore is not ready to publish.");
        }
        if (!owner.TryReconcileRestore(
                defense.States,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Defense input owner restore join failed: " + failureReason);
        }
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
        {
            throw new InvalidOperationException(
                "Defense input owner restore cannot complete.");
        }
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }
}
