using System;
using System.Collections.Generic;

public sealed class ProductionFacilityDestructiveDrainSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonProductionFacilityDestructiveDrainSaveData,
        ProductionFacilityDestructiveDrainRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id =
        "economy.production-facility-destructive-drains";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id,
        ProductionPreparedOutputRoutingSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        EquipmentMaintenanceSaveSection.Id,
        CharacterEnvironmentSaveSection.Id,
        ProductionGenericBillTerminalDrainSaveSection.Id,
        CombatEquipmentTerminalDrainSaveSection.Id,
        ProductionApparelOrderTerminalDrainSaveSection.Id
    };

    private readonly IProductionFacilityDestructiveDrainPersistence persistence;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public ProductionFacilityDestructiveDrainSaveSection(
        IProductionFacilityDestructiveDrainPersistence persistence,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonProductionFacilityDestructiveDrainSaveData
        CapturePayload() => persistence.Capture();

    protected override ProductionFacilityDestructiveDrainRestoreCandidate
        BuildRestoreCandidate(
            DungeonProductionFacilityDestructiveDrainSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        ProductionFacilityDestructiveDrainRestoreCandidate candidate) =>
        persistence.Restore(candidate);

    protected override void PublishRestoreCandidateProjection(
        DungeonProductionFacilityDestructiveDrainSaveData payload,
        ProductionFacilityDestructiveDrainRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetDrain(payload);

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(payloadJson, "entries");
        if (!HasTopLevelProperty(payloadJson, "version")
            || !HasTopLevelProperty(payloadJson, "registryFingerprint")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedCheckpointSequence")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedSerializedByteDigest"))
        {
            throw new InvalidOperationException(
                "Production destructive-drain V3 payload is missing a required marker scalar field.");
        }
    }

    private static bool HasTopLevelProperty(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            return false;
        int depth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (character == '{' || character == '[')
            {
                depth++;
                continue;
            }
            if (character == '}' || character == ']')
            {
                depth--;
                continue;
            }
            if (character != '"')
                continue;

            int start = ++index;
            bool escaped = false;
            while (index < json.Length)
            {
                char current = json[index];
                if (!escaped && current == '"')
                    break;
                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }
            if (depth != 1
                || index >= json.Length
                || index - start != propertyName.Length
                || string.CompareOrdinal(
                    json,
                    start,
                    propertyName,
                    0,
                    propertyName.Length) != 0)
            {
                continue;
            }
            int separator = index + 1;
            while (separator < json.Length
                   && char.IsWhiteSpace(json[separator]))
            {
                separator++;
            }
            if (separator < json.Length && json[separator] == ':')
                return true;
        }
        return false;
    }
}
