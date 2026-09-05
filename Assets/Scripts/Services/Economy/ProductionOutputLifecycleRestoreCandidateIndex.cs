using System;
using UnityEngine;

public interface IProductionOutputLifecycleRestoreCandidatePublisher
{
    void SetWorld(ModularFacilityWorldSaveData payload);
    void SetCharacters(DungeonCharacterWorldSaveData payload);
    void SetPhysicalItems(DungeonPhysicalItemSaveData payload);
    void SetProduction(DungeonProductionBillSaveData payload);
    void SetRouting(ProductionPreparedOutputRoutingSaveData payload);
    void SetCombat(DungeonCombatEquipmentSaveData payload);
    void SetMaintenance(CombatEquipmentMaintenanceSaveData payload);
    void SetEnvironment(DungeonCharacterEnvironmentSaveData payload);
    void SetGenericTerminalDrains(
        DungeonProductionGenericBillTerminalDrainSaveData payload);
    void SetCombatTerminalDrains(
        DungeonCombatEquipmentTerminalDrainSaveData payload);
    void SetApparelTerminalDrains(
        DungeonProductionApparelOrderTerminalDrainSaveData payload);
    void SetDrain(DungeonProductionFacilityDestructiveDrainSaveData payload);
}

public interface IProductionOutputLifecycleRestoreCandidateQuery
{
    bool IsCandidateActive { get; }
    int PublishedSourceCount { get; }
    bool IsGenericTerminalDrainCandidateAvailable { get; }
    bool IsCombatTerminalDrainCandidateAvailable { get; }
    bool IsApparelTerminalDrainCandidateAvailable { get; }
    bool TryCapture(
        out ProductionOutputLifecycleRestoreCandidateBundle bundle);
    bool TryCaptureGenericTerminalDrains(
        out DungeonProductionGenericBillTerminalDrainSaveData payload);
    bool TryCaptureCombatTerminalDrains(
        out DungeonCombatEquipmentTerminalDrainSaveData payload);
    bool TryCaptureApparelTerminalDrains(
        out DungeonProductionApparelOrderTerminalDrainSaveData payload);
}

public interface IProductionFacilityDestructiveDrainCandidateValidator
{
    void Validate(
        ProductionOutputLifecycleRestoreCandidateBundle bundle,
        DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
        DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
        DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
        DungeonProductionFacilityDestructiveDrainSaveData drain);
}

/// <summary>
/// Immutable, normalized eight-section input to destructive-drain restore
/// validation. DTO references are private to the assembly and are cloned at
/// publication, so later section publication cannot mutate this snapshot.
/// </summary>
public sealed class ProductionOutputLifecycleRestoreCandidateBundle
{
    internal ProductionOutputLifecycleRestoreCandidateBundle(
        ModularFacilityWorldSaveData world,
        DungeonCharacterWorldSaveData characters,
        DungeonPhysicalItemSaveData physicalItems,
        DungeonProductionBillSaveData production,
        ProductionPreparedOutputRoutingSaveData routing,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        PhysicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        Production = production
            ?? throw new ArgumentNullException(nameof(production));
        Routing = routing ?? throw new ArgumentNullException(nameof(routing));
        Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        Maintenance = maintenance
            ?? throw new ArgumentNullException(nameof(maintenance));
        Environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        ManifestFingerprint = ComputeManifestFingerprint();
    }

    public const string Schema =
        "production-output-lifecycle-restore-candidates@3";

    internal ModularFacilityWorldSaveData World { get; }
    internal DungeonCharacterWorldSaveData Characters { get; }
    internal DungeonPhysicalItemSaveData PhysicalItems { get; }
    internal DungeonProductionBillSaveData Production { get; }
    internal ProductionPreparedOutputRoutingSaveData Routing { get; }
    internal DungeonCombatEquipmentSaveData Combat { get; }
    internal CombatEquipmentMaintenanceSaveData Maintenance { get; }
    internal DungeonCharacterEnvironmentSaveData Environment { get; }

    public string ManifestFingerprint { get; }

    private string ComputeManifestFingerprint()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        Append(digest, ModularFacilityWorldSaveSection.Id, World);
        Append(digest, CharacterWorldSaveSection.Id, Characters);
        Append(digest, PhysicalItemsSaveSection.Id, PhysicalItems);
        Append(digest, ProductionBillsSaveSection.Id, Production);
        Append(digest, ProductionPreparedOutputRoutingSaveSection.Id, Routing);
        Append(digest, CombatEquipmentSaveSection.Id, Combat);
        Append(digest, EquipmentMaintenanceSaveSection.Id, Maintenance);
        Append(digest, CharacterEnvironmentSaveSection.Id, Environment);
        return digest.ComputeSha256();
    }

    private static void Append<T>(
        CanonicalSemanticDigestBuilder digest,
        string sectionId,
        T payload)
        where T : class
    {
        digest.Append(sectionId);
        digest.Append(JsonUtility.ToJson(payload));
    }
}

/// <summary>
/// Transaction-scoped normalized DTO index. All eight source sections publish
/// exactly once from their real Commit path. The drain section can validate
/// only after the complete source set exists. Complete, rollback, and discard
/// erase every reference so no candidate can leak into the next restore.
/// </summary>
public sealed class ProductionOutputLifecycleRestoreCandidateIndex :
    IProductionOutputLifecycleRestoreCandidatePublisher,
    IProductionOutputLifecycleRestoreCandidateQuery,
    IDungeonRestoreTransactionParticipant
{
    private readonly IProductionFacilityDestructiveDrainCandidateValidator
        drainValidator;

    private bool active;
    private bool published;
    private bool drainValidated;
    private ModularFacilityWorldSaveData world;
    private DungeonCharacterWorldSaveData characters;
    private DungeonPhysicalItemSaveData physicalItems;
    private DungeonProductionBillSaveData production;
    private ProductionPreparedOutputRoutingSaveData routing;
    private DungeonCombatEquipmentSaveData combat;
    private CombatEquipmentMaintenanceSaveData maintenance;
    private DungeonCharacterEnvironmentSaveData environment;
    private DungeonProductionGenericBillTerminalDrainSaveData
        genericTerminalDrains;
    private DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains;
    private DungeonProductionApparelOrderTerminalDrainSaveData
        apparelTerminalDrains;
    private DungeonProductionFacilityDestructiveDrainSaveData drain;

    public ProductionOutputLifecycleRestoreCandidateIndex(
        IProductionFacilityDestructiveDrainCandidateValidator drainValidator)
    {
        this.drainValidator = drainValidator
            ?? throw new ArgumentNullException(nameof(drainValidator));
    }

    public string ParticipantId =>
        "998.economy.production-output-lifecycle-restore-index";

    public bool IsCandidateActive => active;

    public bool IsGenericTerminalDrainCandidateAvailable =>
        active && genericTerminalDrains != null;

    public bool IsCombatTerminalDrainCandidateAvailable =>
        active && combatTerminalDrains != null;

    public bool IsApparelTerminalDrainCandidateAvailable =>
        active && apparelTerminalDrains != null;

    public int PublishedSourceCount =>
        (world != null ? 1 : 0)
        + (characters != null ? 1 : 0)
        + (physicalItems != null ? 1 : 0)
        + (production != null ? 1 : 0)
        + (routing != null ? 1 : 0)
        + (combat != null ? 1 : 0)
        + (maintenance != null ? 1 : 0)
        + (environment != null ? 1 : 0);

    public void BeginRestoreCandidate()
    {
        if (active
            || PublishedSourceCount != 0
            || genericTerminalDrains != null
            || combatTerminalDrains != null
            || apparelTerminalDrains != null
            || drain != null)
        {
            throw new InvalidOperationException(
                "Production-output lifecycle restore index contains a stale candidate.");
        }

        active = true;
        published = false;
        drainValidated = false;
    }

    public void SetWorld(ModularFacilityWorldSaveData payload) =>
        world = SetExactlyOnce(
            ModularFacilityWorldSaveSection.Id,
            world,
            payload);

    public void SetCharacters(DungeonCharacterWorldSaveData payload) =>
        characters = SetExactlyOnce(
            CharacterWorldSaveSection.Id,
            characters,
            payload);

    public void SetPhysicalItems(DungeonPhysicalItemSaveData payload) =>
        physicalItems = SetExactlyOnce(
            PhysicalItemsSaveSection.Id,
            physicalItems,
            payload);

    public void SetProduction(DungeonProductionBillSaveData payload) =>
        production = SetExactlyOnce(
            ProductionBillsSaveSection.Id,
            production,
            payload);

    public void SetRouting(ProductionPreparedOutputRoutingSaveData payload) =>
        routing = SetExactlyOnce(
            ProductionPreparedOutputRoutingSaveSection.Id,
            routing,
            payload);

    public void SetCombat(DungeonCombatEquipmentSaveData payload) =>
        combat = SetExactlyOnce(
            CombatEquipmentSaveSection.Id,
            combat,
            payload);

    public void SetMaintenance(CombatEquipmentMaintenanceSaveData payload) =>
        maintenance = SetExactlyOnce(
            EquipmentMaintenanceSaveSection.Id,
            maintenance,
            payload);

    public void SetEnvironment(DungeonCharacterEnvironmentSaveData payload) =>
        environment = SetExactlyOnce(
            CharacterEnvironmentSaveSection.Id,
            environment,
            payload);

    public void SetGenericTerminalDrains(
        DungeonProductionGenericBillTerminalDrainSaveData payload) =>
        genericTerminalDrains = SetExactlyOnce(
            ProductionGenericBillTerminalDrainSaveSection.Id,
            genericTerminalDrains,
            payload);

    public void SetCombatTerminalDrains(
        DungeonCombatEquipmentTerminalDrainSaveData payload) =>
        combatTerminalDrains = SetExactlyOnce(
            CombatEquipmentTerminalDrainSaveSection.Id,
            combatTerminalDrains,
            payload);

    public void SetApparelTerminalDrains(
        DungeonProductionApparelOrderTerminalDrainSaveData payload) =>
        apparelTerminalDrains = SetExactlyOnce(
            ProductionApparelOrderTerminalDrainSaveSection.Id,
            apparelTerminalDrains,
            payload);

    public void SetDrain(
        DungeonProductionFacilityDestructiveDrainSaveData payload)
    {
        RequireActive(ProductionFacilityDestructiveDrainSaveSection.Id);
        if (drain != null)
        {
            throw new InvalidOperationException(
                "Restore candidate slot was published more than once: "
                + ProductionFacilityDestructiveDrainSaveSection.Id);
        }

        DungeonProductionFacilityDestructiveDrainSaveData candidate =
            Clone(payload, ProductionFacilityDestructiveDrainSaveSection.Id);
        ProductionOutputLifecycleRestoreCandidateBundle bundle =
            CaptureRequired();
        DungeonProductionGenericBillTerminalDrainSaveData generic =
            genericTerminalDrains == null
                ? null
                : Clone(
                    genericTerminalDrains,
                    ProductionGenericBillTerminalDrainSaveSection.Id);
        DungeonCombatEquipmentTerminalDrainSaveData combatTerminal =
            combatTerminalDrains == null
                ? null
                : Clone(
                    combatTerminalDrains,
                    CombatEquipmentTerminalDrainSaveSection.Id);
        DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminal =
            apparelTerminalDrains == null
                ? null
                : Clone(
                    apparelTerminalDrains,
                    ProductionApparelOrderTerminalDrainSaveSection.Id);
        drainValidator.Validate(
            bundle,
            generic,
            combatTerminal,
            apparelTerminal,
            candidate);
        drain = candidate;
        drainValidated = true;
    }

    public bool TryCapture(
        out ProductionOutputLifecycleRestoreCandidateBundle bundle)
    {
        if (!active || PublishedSourceCount != 8)
        {
            bundle = null;
            return false;
        }

        bundle = new ProductionOutputLifecycleRestoreCandidateBundle(
            world,
            characters,
            physicalItems,
            production,
            routing,
            combat,
            maintenance,
            environment);
        return true;
    }

    public bool TryCaptureGenericTerminalDrains(
        out DungeonProductionGenericBillTerminalDrainSaveData payload)
    {
        if (!IsGenericTerminalDrainCandidateAvailable)
        {
            payload = null;
            return false;
        }

        payload = Clone(
            genericTerminalDrains,
            ProductionGenericBillTerminalDrainSaveSection.Id);
        return true;
    }

    public bool TryCaptureCombatTerminalDrains(
        out DungeonCombatEquipmentTerminalDrainSaveData payload)
    {
        if (!IsCombatTerminalDrainCandidateAvailable)
        {
            payload = null;
            return false;
        }

        payload = Clone(
            combatTerminalDrains,
            CombatEquipmentTerminalDrainSaveSection.Id);
        return true;
    }

    public bool TryCaptureApparelTerminalDrains(
        out DungeonProductionApparelOrderTerminalDrainSaveData payload)
    {
        if (!IsApparelTerminalDrainCandidateAvailable)
        {
            payload = null;
            return false;
        }

        payload = Clone(
            apparelTerminalDrains,
            ProductionApparelOrderTerminalDrainSaveSection.Id);
        return true;
    }

    public void PublishRestoreCandidate()
    {
        RequireActive(ParticipantId);
        if (PublishedSourceCount != 8)
        {
            throw new InvalidOperationException(
                $"Production-output lifecycle restore index is incomplete: {PublishedSourceCount}/8.");
        }
        if (drain != null && !drainValidated)
        {
            throw new InvalidOperationException(
                "Production destructive-drain candidate was not cross-validated.");
        }

        published = true;
    }

    public void RollbackPublishedRestoreCandidate() => Clear();

    public void CompleteRestoreCandidate() => Clear();

    public void DiscardRestoreCandidate() => Clear();

    private ProductionOutputLifecycleRestoreCandidateBundle CaptureRequired()
    {
        if (!TryCapture(out ProductionOutputLifecycleRestoreCandidateBundle bundle))
        {
            throw new InvalidOperationException(
                $"Production destructive-drain restore requires all eight normalized source candidates; found {PublishedSourceCount}/8.");
        }

        return bundle;
    }

    private T SetExactlyOnce<T>(
        string sectionId,
        T current,
        T payload)
        where T : class
    {
        RequireActive(sectionId);
        if (published)
        {
            throw new InvalidOperationException(
                "Restore candidate publication is already sealed.");
        }
        if (current != null)
        {
            throw new InvalidOperationException(
                "Restore candidate slot was published more than once: "
                + sectionId);
        }

        return Clone(payload, sectionId);
    }

    private void RequireActive(string sectionId)
    {
        if (!active)
        {
            throw new InvalidOperationException(
                "Restore candidate publication occurred outside the active transaction: "
                + sectionId);
        }
    }

    private static T Clone<T>(T payload, string sectionId)
        where T : class
    {
        if (payload == null)
        {
            throw new ArgumentNullException(
                nameof(payload),
                "Restore candidate payload is null: " + sectionId);
        }

        string json = JsonUtility.ToJson(payload);
        T clone = JsonUtility.FromJson<T>(json);
        return clone ?? throw new InvalidOperationException(
            "Restore candidate payload clone failed: " + sectionId);
    }

    private void Clear()
    {
        world = null;
        characters = null;
        physicalItems = null;
        production = null;
        routing = null;
        combat = null;
        maintenance = null;
        environment = null;
        genericTerminalDrains = null;
        combatTerminalDrains = null;
        apparelTerminalDrains = null;
        drain = null;
        drainValidated = false;
        published = false;
        active = false;
    }
}

/// <summary>
/// Explicit opt-out for isolated section fixtures that do not execute a whole
/// registry transaction. Production composition never registers this object.
/// </summary>
public static class ProductionOutputLifecycleRestoreCandidatePublisher
{
    public static IProductionOutputLifecycleRestoreCandidatePublisher
        IsolatedSectionFixtureOnly { get; } = new DisabledPublisher();

    private sealed class DisabledPublisher :
        IProductionOutputLifecycleRestoreCandidatePublisher
    {
        public void SetWorld(ModularFacilityWorldSaveData payload) { }
        public void SetCharacters(DungeonCharacterWorldSaveData payload) { }
        public void SetPhysicalItems(DungeonPhysicalItemSaveData payload) { }
        public void SetProduction(DungeonProductionBillSaveData payload) { }
        public void SetRouting(ProductionPreparedOutputRoutingSaveData payload) { }
        public void SetCombat(DungeonCombatEquipmentSaveData payload) { }
        public void SetMaintenance(CombatEquipmentMaintenanceSaveData payload) { }
        public void SetEnvironment(DungeonCharacterEnvironmentSaveData payload) { }
        public void SetGenericTerminalDrains(
            DungeonProductionGenericBillTerminalDrainSaveData payload) { }
        public void SetCombatTerminalDrains(
            DungeonCombatEquipmentTerminalDrainSaveData payload) { }
        public void SetApparelTerminalDrains(
            DungeonProductionApparelOrderTerminalDrainSaveData payload) { }
        public void SetDrain(
            DungeonProductionFacilityDestructiveDrainSaveData payload) { }
    }
}
