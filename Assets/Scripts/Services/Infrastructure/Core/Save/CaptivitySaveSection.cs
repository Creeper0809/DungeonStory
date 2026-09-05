using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivitySaveSection :
    DungeonStrictJsonSaveSection<
        CaptivitySaveData,
        CaptivityRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "captivity";

    private static readonly string[] Dependencies =
    {
        "world.facilities",
        "characters.world",
        "items.physical",
        "combat.equipment",
        "combat.body-health"
    };

    private readonly ICaptivityPersistence persistence;

    public CaptivitySaveSection(ICaptivityPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CaptivitySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CaptivitySaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        CaptivitySaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.captives == null)
        {
            return;
        }

        bool captiveIdsChanged = false;
        for (int index = 0; index < payload.captives.Count; index++)
        {
            CaptiveState captive = payload.captives[index];
            if (captive == null)
            {
                continue;
            }

            string path = $"captives[{index}]";
            string previousCaptiveId = captive.captiveId;
            captive.captiveId = NormalizeV18CharacterReference(
                previousCaptiveId, report, path + ".captiveId");
            captiveIdsChanged |= !string.Equals(
                previousCaptiveId,
                captive.captiveId,
                StringComparison.Ordinal);
            captive.reservedCarrierId = NormalizeV18CharacterReference(
                captive.reservedCarrierId, report, path + ".reservedCarrierId");
            captive.reservedWardenId = NormalizeV18CharacterReference(
                captive.reservedWardenId, report, path + ".reservedWardenId");
            if (captive.status == CaptivityStatus.Minion)
            {
                CaptivityStateTransitionRules.ClearCaptiveOnlyState(captive);
            }
        }
        if (captiveIdsChanged)
        {
            payload.captives.Sort((left, right) => string.CompareOrdinal(
                left?.captiveId,
                right?.captiveId));
        }
    }

    protected override CaptivityRestoreCandidate BuildRestoreCandidate(
        CaptivitySaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CaptivityRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
