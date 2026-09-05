using System;
using System.Collections.Generic;

public interface ICaptivityInteractionMaterialRestoreAuthority
{
    bool TryReplaceRestoreAuthorities(
        IReadOnlyList<CaptiveState> candidateStates,
        out string failureReason);
}

/// <summary>
/// Owner-domain save/restore adapter. Captivity state remains the operation
/// authority; this adapter only rebuilds the complete exact claim/profile set
/// and refuses capture when a live owner, facility, mass revision or committed
/// Sink token no longer joins that projection.
/// </summary>
public sealed class CaptivityInteractionMaterialLifecycleRuntime :
    ICaptivityInteractionMaterialRestoreAuthority
{
    private readonly CaptivityInteractionMaterialRuntime materials;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly IBuildingWorldQuery buildings;

    public CaptivityInteractionMaterialLifecycleRuntime(
        CaptivityInteractionMaterialRuntime materials,
        CaptivityInteractionRegistry interactions,
        IBuildingWorldQuery buildings)
    {
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
        this.interactions = interactions
            ?? throw new ArgumentNullException(nameof(interactions));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
    }

    public bool TryReplaceRestoreAuthorities(
        IReadOnlyList<CaptiveState> candidateStates,
        out string failureReason) => materials.TryReplace(
        candidateStates,
        interactions,
        buildings.Buildings,
        out failureReason);

    public bool CanCapture(
        IReadOnlyList<CaptiveState> currentStates,
        out string failureReason) => materials.TryReplace(
        currentStates ?? Array.Empty<CaptiveState>(),
        interactions,
        buildings.Buildings,
        out failureReason);

    public void ValidateBeforeCapture(
        IReadOnlyList<CaptiveState> currentStates)
    {
        if (CanCapture(currentStates, out string failureReason))
            return;
        throw new InvalidOperationException(
            "Captivity interaction material authority is not save-safe: "
            + failureReason);
    }
}

/// <summary>
/// Rebuilds the captivity interaction material claim/profile projection from
/// the staged Captivity aggregate before the shared claim/profile candidates
/// publish. The later Captivity participant owns only its aggregate state.
/// </summary>
public sealed class CaptivityInteractionMaterialRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "217.world.captivity-interaction-material";

    private readonly ICaptivityRestoreStateQuery captivity;
    private readonly ICaptivityInteractionMaterialRestoreAuthority authority;
    private bool active;
    private bool published;

    public CaptivityInteractionMaterialRestoreParticipant(
        ICaptivityRestoreStateQuery captivity,
        ICaptivityInteractionMaterialRestoreAuthority authority)
    {
        this.captivity = captivity
            ?? throw new ArgumentNullException(nameof(captivity));
        this.authority = authority
            ?? throw new ArgumentNullException(nameof(authority));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Captivity interaction material restore is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Captivity interaction material restore is not ready to publish.");
        }
        if (!authority.TryReplaceRestoreAuthorities(
                captivity.Captives,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Captivity interaction material restore join failed: "
                + failureReason);
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
                "Captivity interaction material restore cannot complete.");
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
