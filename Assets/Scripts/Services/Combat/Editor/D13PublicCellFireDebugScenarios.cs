#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    public static void RunD13PublicCellFireBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("D13 requires a new output directory.");
        Directory.CreateDirectory(output);
        var checks = new JArray();
        try
        {
            VerifyD13PublicRepeatAndRestore(checks);
            VerifyD13PublicReloadAndRestore(checks);
            VerifyD13PresentationFailure(checks);
            VerifyD13PublicRollback(checks, rejectAmmo: true);
            VerifyD13PublicRollback(checks, rejectAmmo: false);
            VerifyD13ManaCostEpsilon(checks);
            WriteReportCreateOnly(Path.Combine(output, "report.json"), new JObject
            {
                ["passed"] = true, ["checks"] = checks,
                ["entry"] = "TryIssueForceFireAtCell -> TickFrame -> CombatCellShotJournal -> DomainFactDispatcher",
                ["scope"] = "controlled Editor composition; actual command, mana, equipment, neutral fact journal and ledger owners",
                ["presentationExceptionVerified"] = true,
                ["presentationExceptionScope"] = "injected presentation-port exception, not GPU/UI rendering",
                ["fullD13Closure"] = false, ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            WriteReportCreateOnly(Path.Combine(output, "report.json"), new JObject
            { ["passed"] = false, ["checks"] = checks, ["failure"] = error.ToString() });
            throw;
        }
    }

    private static void WriteReportCreateOnly(string path, JObject report)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(report.ToString());
    }

    private static void VerifyD13PublicRepeatAndRestore(JArray checks)
    {
        using var fixture = new D13PublicCellFireFixture(MigratedProducerOutcomeFaultMode.None);
        fixture.Issue();
        int issuedRevision = fixture.Command.revision;
        int issuedWatermark = fixture.Runtime.Capture().revisions.Single().revision;
        float manaBefore = fixture.Mana;
        fixture.Tick(0.01f);
        Require(fixture.Mana == manaBefore - 10f && fixture.Ammo == 0
                && fixture.Command.revision == issuedRevision + 1 && fixture.OutcomeCount == 1
                && fixture.Runtime.Capture().revisions.Single().revision == issuedWatermark + 1,
            "D13 public first fire did not consume real MP/ammunition exactly once.");
        fixture.RequireCellOnlyOutcomes();
        checks.Add("public-entry: real MP -10, ammunition -1, revision +1, one cell-only result");

        string saved = JsonUtility.ToJson(fixture.Runtime.Capture());
        string beforeRestore = fixture.CaptureResourcesAndLedger();
        fixture.RestoreCommandIntoNewRuntime(saved);
        Require(fixture.CaptureResourcesAndLedger() == beforeRestore,
            "D13 command restoration altered resource or ledger owners.");
        var expectedRestored = JsonUtility.FromJson<CharacterCombatCommandSaveData>(saved);
        expectedRestored.commands.Single().state = CharacterCombatCommandState.Queued;
        Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == JsonUtility.ToJson(expectedRestored),
            "D13 command JSON round trip changed identity/revision/cooldown.");
        fixture.LoadOneArrow();
        fixture.Tick(0.01f);
        Require(fixture.Mana == manaBefore - 10f && fixture.Ammo == 1
                && fixture.OutcomeCount == 1 && fixture.Command.revision == issuedRevision + 1,
            "D13 restored cooldown replay consumed costs or duplicated the result.");
        fixture.Tick(2f);
        Require(fixture.Mana == manaBefore - 20f && fixture.Ammo == 0
                && fixture.OutcomeCount == 2 && fixture.Command.revision == issuedRevision + 2,
            "D13 restored public command did not advance to a distinct next shot.");
        fixture.RequireCellOnlyOutcomes();
        checks.Add("command-save/new-runtime: exact identity and cooldown; no immediate duplicate; next revision fires once");

        var identities = fixture.Harness.Ledger.CaptureGameplayOutcomes().exactOutcomes
            .Select(value => value.operationId).ToArray();
        Require(identities.All(value => !string.IsNullOrWhiteSpace(value))
                && identities.Distinct(StringComparer.Ordinal).Count() == 2,
            "D13 repeated public shots reused an operation identity.");
        checks.Add("repeated shots have two distinct operation identities and no invented damage/victim");

        var invalid = fixture.Runtime.Capture();
        invalid.revisions.Single().revision = invalid.commands.Single().revision - 1;
        string resourceBeforeInvalid = fixture.CaptureResourcesAndLedger();
        bool rejected = false;
        try { fixture.RestoreCommandIntoNewRuntime(JsonUtility.ToJson(invalid)); }
        catch (InvalidOperationException error)
        { rejected = error.Message.Contains("revision watermark", StringComparison.Ordinal); }
        Require(rejected && fixture.Runtime.Capture().commands.Count == 0
                && fixture.CaptureResourcesAndLedger() == resourceBeforeInvalid,
            "D13 invalid watermark save was accepted or partially applied.");
        checks.Add("invalid revision watermark still rejected before publication; resources and ledger unchanged");
    }

    private static void VerifyD13PublicRollback(JArray checks, bool rejectAmmo)
    {
        using var fixture = new D13PublicCellFireFixture(rejectAmmo
            ? MigratedProducerOutcomeFaultMode.None : MigratedProducerOutcomeFaultMode.RejectCommit);
        fixture.Equipment.RejectAmmunitionConsumption = rejectAmmo;
        fixture.Issue();
        int issuedRevision = fixture.Command.revision;
        int issuedWatermark = fixture.Runtime.Capture().revisions.Single().revision;
        float manaBefore = fixture.Mana;
        int ammoBefore = fixture.Ammo;
        fixture.Tick(0.01f);
        Require(fixture.Mana == manaBefore && fixture.Ammo == ammoBefore
                && fixture.Command.revision == issuedRevision
                && fixture.Runtime.Capture().revisions.Single().revision == issuedWatermark
                && fixture.Command.attackCooldownRemaining == 0f && fixture.OutcomeCount == 0,
            "D13 rejected public shot leaked resources, revision, cooldown or outcome.");
        if (rejectAmmo)
        {
            Require(fixture.Equipment.AmmunitionConsumeAttempts == 1
                    && fixture.Equipment.ManaObservedAtRejectedConsumption == manaBefore - 10f,
                "D13 ammunition failure was not injected after the real MP spend.");
        }
        Require(fixture.Command.state == CharacterCombatCommandState.Blocked
                && !string.IsNullOrWhiteSpace(fixture.Command.status),
            "D13 public failure was hidden instead of exposing a blocked reason.");
        checks.Add(rejectAmmo
            ? "ammo-rejection-after-real-MP-spend: exact refund, zero result, blocked reason"
            : "ledger-commit-rejection: real ammunition/MP restored, unchanged revision/cooldown, zero result");
    }

    private static void VerifyD13ManaCostEpsilon(JArray checks)
    {
        const float requestedMana = 10f - 0.00005f;
        using (var fixture = new D13PublicCellFireFixture(
                   MigratedProducerOutcomeFaultMode.None))
        {
            fixture.ReduceManaTo(requestedMana);
            float manaBefore = fixture.Mana;
            int ammoBefore = fixture.Ammo;
            fixture.Issue();
            int revisionBefore = fixture.Command.revision;
            fixture.Tick(0.01f);
            Require(manaBefore < 10f
                    && 10f - manaBefore <= 0.0001f
                    && fixture.Mana == 0f
                    && fixture.Ammo == ammoBefore - 1
                    && fixture.Command.revision == revisionBefore + 1
                    && fixture.OutcomeCount == 1,
                "D13 mana epsilon shot did not clamp the accepted real spend to zero exactly once.");
        }

        using (var fixture = new D13PublicCellFireFixture(
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            fixture.ReduceManaTo(requestedMana);
            float manaBefore = fixture.Mana;
            int ammoBefore = fixture.Ammo;
            fixture.Issue();
            int revisionBefore = fixture.Command.revision;
            fixture.Tick(0.01f);
            Require(manaBefore < 10f
                    && 10f - manaBefore <= 0.0001f
                    && fixture.Mana == manaBefore
                    && fixture.Ammo == ammoBefore
                    && fixture.Command.revision == revisionBefore
                    && fixture.OutcomeCount == 0,
                "D13 rejected mana epsilon shot over-refunded or leaked ammunition, revision or outcome.");
        }
        checks.Add("mana-cost epsilon: accepted low balance clamps to zero; rejected commit restores exact prior balance");
    }

    private static void VerifyD13PublicReloadAndRestore(JArray checks)
    {
        using var fixture = new D13PublicCellFireFixture(MigratedProducerOutcomeFaultMode.None);
        fixture.Issue();
        fixture.Tick(0.01f);
        float manaBeforeReload = fixture.Mana;
        int carryBeforeReload = fixture.CarriedAmmo;
        fixture.IssueReload();
        fixture.Tick(0.01f);
        Require(fixture.Command.type == CombatCommandType.Reload
                && fixture.Command.revision > 0 && fixture.Command.reloadRemaining > 0f,
            "D13 public reload must preserve a positive serializable revision.");
        string json = JsonUtility.ToJson(fixture.Runtime.Capture());
        fixture.RestoreCommandIntoNewRuntime(json);
        fixture.Tick(0.1f);
        Require(fixture.Ammo == 0 && fixture.CarriedAmmo == carryBeforeReload,
            "D13 reload restore consumed ammunition before its timer completed.");
        fixture.Tick(2f);
        Require(fixture.Runtime.Capture().commands.Count == 0
                && fixture.Ammo == 1 && fixture.CarriedAmmo == carryBeforeReload - 1
                && fixture.Mana == manaBeforeReload && fixture.OutcomeCount == 1,
            "D13 restored reload lost completion intent or duplicated cost/attack outcome.");
        checks.Add("public reload-save/restore: positive revision, timer preserved, one carried arrow loaded, no second shot");
    }

    private static void VerifyD13PresentationFailure(JArray checks)
    {
        using var fixture = new D13PublicCellFireFixture(MigratedProducerOutcomeFaultMode.None);
        fixture.Presentation.Reject = true;
        int expectedErrorCount = 0;
        void Observe(string message, string stack, LogType type)
        {
            if (type == LogType.Error && message ==
                "Cell-fire presentation failed after durable commit: InvalidOperationException")
                expectedErrorCount++;
        }
        Application.logMessageReceived += Observe;
        try
        {
            fixture.Issue();
            float manaBefore = fixture.Mana;
            fixture.Tick(0.01f);
            Require(fixture.Presentation.Attempts == 1 && expectedErrorCount == 1
                    && fixture.Mana == manaBefore - 10f && fixture.Ammo == 0 && fixture.OutcomeCount == 1,
                "D13 presentation failure rolled back or hid a durable shot.");
            fixture.LoadOneArrow();
            fixture.Tick(0.01f);
            Require(fixture.Presentation.Attempts == 1 && expectedErrorCount == 1
                    && fixture.Mana == manaBefore - 10f && fixture.Ammo == 1 && fixture.OutcomeCount == 1,
                "D13 presentation failure retried the committed shot during cooldown.");
            fixture.RequireCellOnlyOutcomes();
            checks.Add("injected presentation exception: explicit error, durable cost/result retained, cooldown prevents duplicate");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    private sealed class D13PublicCellFireFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly CharacterActor actor;
        private readonly CharacterBodyHealthRuntime health;
        private readonly D13Ports ports;
        private readonly Grid previousGrid;
        private readonly CombatEquipmentRuntime equipmentOwner;
        private readonly CharacterCarryInventory carry;
        private readonly string actorId;
        private readonly string weaponId;
        private DungeonRuntimeAggregateRootStore commandRoot;
        internal OutcomeHarness Harness { get; }
        internal D13FaultInjectingEquipmentRuntime Equipment { get; }
        internal D13PresentationProbe Presentation { get; private set; }
        internal CharacterCombatCommandRuntime Runtime { get; private set; }
        internal float Mana => health.GetMana(actor).Current;
        internal int Ammo => Equipment.TryGetActiveWeapon(actorId, out var weapon) ? weapon.LoadedAmmo : -1;
        internal int OutcomeCount => Harness.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
        internal int CarriedAmmo => carry.CountItem("ammo:rune-cartridge");
        internal CharacterCombatCommand Command => Runtime.Capture().commands.Single();

        internal D13PublicCellFireFixture(MigratedProducerOutcomeFaultMode fault)
        {
            Harness = new OutcomeHarness(MigratedProducerOutcomeKind.CombatAttackResult,
                "d13-public", true, fault, deliveryFault: false);
            var world = CharacterAiEditorTestDependencies.WorldRegistry;
            world.TryGetGrid(out previousGrid);
            try
            {
            ports = new D13Ports(new Grid(6, 1));
            world.SetGrid(ports.Grid);
            var data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.Customer, "룬 사수", "human");
            cleanup.Add(data);
            var go = new GameObject("D13 public rune archer");
            cleanup.Add(go);
            actor = go.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(go);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.Customer;
            actorId = "character:d13-public:" + Guid.NewGuid().ToString("N");
            actor.Identity.SetPersistentId(actorId);
            world.RegisterCharacter(actor);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            health = CharacterAiEditorTestDependencies.BodyHealthRuntime;
            health.ConfigureVitals(actor, 100f, resetCurrentHealth: true);

            var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var carryRegistry = new CharacterCarryInventoryRegistry();
            carry = go.GetComponent<CharacterCarryInventory>() ?? go.AddComponent<CharacterCarryInventory>();
            carry.Construct(carryRegistry);
            carry.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new() { carriedStackId = "d13:carried-arrow", sourceStackId = "d13:source-arrow",
                        itemId = "ammo:rune-cartridge", quantity = 3 }
                }
            });
            equipmentOwner = CombatEquipmentEditorTestFactory.Create(
                new ResourceCombatEquipmentCatalog(content),
                new WorldItemRepository(new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore()),
                carryRegistry, new ResourceEconomyContentCatalog(content),
                EmptyEvolutionModuleRegistry.Instance, EditorAllResearchRuntimeProvider.Instance,
                new ResourceEquipmentModuleCatalog(content), UnavailableEquipmentPhysicalItemGateway.Instance,
                calendar: CharacterAiEditorTestDependencies.GameCalendar);
            Equipment = new D13FaultInjectingEquipmentRuntime(equipmentOwner)
            { ObserveMana = () => Mana };
            var instance = Equipment.CreateInstance("weapon:rune-bow", CombatEquipmentQuality.Normal);
            weaponId = instance.instanceId;
            Require(Equipment.TryAssignToCharacter(actorId, weaponId, out string failure),
                "D13 fixture could not equip authored rune bow: " + failure);
            LoadOneArrow();
            Require(Mana >= 20f && Ammo == 1, "D13 fixture requires real mana and one loaded arrow.");
            CreateRuntime();
            }
            catch { Dispose(); throw; }
        }

        private void CreateRuntime()
        {
            commandRoot = new DungeonRuntimeAggregateRootStore();
            var world = CharacterAiEditorTestDependencies.WorldRegistry;
            var performance = CharacterAiEditorTestDependencies.NeutralPerformance;
            var sight = new GridCombatLineOfSightService(ports, world);
            var cover = new GridCombatCoverQuery();
            var tactical = new DefenseTacticalCoordinator(ports, commandRoot);
            var path = new GridPathSearchBroker(ports,
                performanceRecorder: CharacterAiEditorTestDependencies.TestPerformanceRecorder,
                doorAccessQuery: null, costPolicy: null);
            var coverDurability = new NoCoverDurabilityRegistry();
            var ui = new NoWorldUiHierarchy();
            Presentation ??= new D13PresentationProbe(new CombatCommandProjectilePresentation(ports, ui));
            var events = new GameEventBus();
            var applier = new CombatCommandResultApplier(Equipment, health, health, health,
                Harness.CombatActions, coverDurability,
                ports, ui, events, world, world, new RoomFacilityPolicyService(RoomRegistry.EditorCache),
                CharacterAiEditorTestDependencies.GameCalendar);
            Runtime = new CharacterCombatCommandRuntime(
                new CharacterCombatCommandCombatServices(Equipment, ports,
                    new CombatFiringSolutionService(sight, cover, ports,
                        new UnityCombatRandomSource(new RandomStreamProvider(391))),
                    sight, cover, ports, health, health, health, ports,
                    new ExtremeTraitRuntime(new CharacterIdentityStateStore()), performance),
                new CharacterCombatCommandWorldServices(ports, tactical, path, world, ports,
                    coverDurability, events, ui),
                new CharacterCombatCommandCollaborators(new CombatAttackPositionPlanner(sight, tactical, path),
                    new CombatFallbackWeaponSelector(Equipment), applier,
                    new CombatCommandParticipantQuery(world, health, performance), ports, Presentation),
                commandRoot,
                Harness.Facts,
                Harness.Facts,
                CharacterAiEditorTestDependencies.GameCalendar);
        }

        internal void Issue()
        {
            Require(Runtime.SetCombatStance(actor, true, out string stance), "D13 stance failed: " + stance);
            Require(Runtime.TryIssueForceFireAtCell(actor, new Vector2Int(3, 0), out string message),
                "D13 public command failed: " + message);
        }
        internal void Tick(float delta) { ports.Advance(delta); Runtime.TickFrame(); }
        internal void IssueReload() => Require(Runtime.TryIssueReload(actor, out string message),
            "D13 public reload command failed: " + message);
        internal void LoadOneArrow() => Require(
            Equipment.TryLoadExternalAmmunition(weaponId, "ammo:rune-cartridge", 1),
            "D13 fixture could not load the authored compatible arrow.");
        internal void ReduceManaTo(float targetMana)
        {
            float before = Mana;
            float amount = before - targetMana;
            Require(targetMana > 0f && amount > 0f,
                "D13 fixture low-mana target must be below the current real balance.");
            Require(health.TrySpendMana(actor, amount, out string failure),
                "D13 fixture could not establish the real low-mana boundary: "
                    + failure);
            Require(Mana < 10f && Mathf.Abs(Mana - targetMana) <= 0.00001f,
                "D13 fixture low-mana setup drifted outside the requested boundary.");
        }
        internal string CaptureResourcesAndLedger() => Mana.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "|" + JsonUtility.ToJson(equipmentOwner.Capture())
            + "|" + JsonUtility.ToJson(carry.Capture())
            + "|" + JsonUtility.ToJson(Harness.Ledger.CaptureGameplayOutcomes());
        internal void RestoreCommandIntoNewRuntime(string json)
        {
            CreateRuntime();
            var section = new CharacterCombatCommandSaveSection(Runtime);
            var sections = section.DependsOn.Select(id =>
                (IDungeonSaveSection)new D13ExistingOwnerMarker(id)).Append(section).ToArray();
            var registry = new DungeonSaveSectionRegistry(sections, commandRoot,
                new IDungeonRestoreTransactionParticipant[] { Runtime });
            var envelopes = registry.CaptureAll();
            envelopes.Single(value => value.sectionId == section.SectionId).payloadJson = json;
            var report = new DungeonGameRestoreReport();
            Require(registry.RestoreAll(envelopes, report),
                "D13 official command save section restore failed: " + string.Join(" | ", report.Errors));
        }
        internal void RequireCellOnlyOutcomes()
        {
            foreach (var row in Harness.Ledger.CaptureGameplayOutcomes().exactOutcomes)
            {
                Require(row.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                            MigratedProducerOutcomeKind.CombatAttackResult).OutcomeTypeId.Value
                        && row.subjects.Any(subject => subject.entityKindId == "grid-cell"
                            && subject.entityId == "grid-cell:3:0")
                        && row.facts.Any(fact => fact.factId == "detail"
                            && fact.value.Contains("hit=0;damage=0", StringComparison.Ordinal)),
                    "D13 empty-cell fire invented a victim or damage result.");
            }
        }
        public void Dispose()
        {
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry.SetGrid(previousGrid);
            for (int index = cleanup.Count - 1; index >= 0; --index)
                if (cleanup[index] != null) UnityEngine.Object.DestroyImmediate(cleanup[index]);
            Harness.Dispose();
        }
    }

    private sealed class D13PresentationProbe : ICombatCommandProjectilePresentation
    {
        private readonly ICombatCommandProjectilePresentation inner;
        internal D13PresentationProbe(ICombatCommandProjectilePresentation inner) { this.inner = inner; }
        internal bool Reject;
        internal int Attempts;
        public void Launch(Vector3 start, Vector3 end, CombatWeaponSnapshot weapon)
        {
            Attempts++;
            if (Reject) throw new InvalidOperationException("injected-d13-presentation-failure");
            inner.Launch(start, end, weapon);
        }
    }

    // This focused round trip replaces only the command owner. Its body,
    // equipment and world remain live and are compared byte-for-byte above.
    private sealed class D13ExistingOwnerMarker : DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        internal D13ExistingOwnerMarker(string id) { SectionId = id; }
        public override string SectionId { get; }
        public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
        protected override void CommitMarker(DungeonGameRestoreReport report) { }
    }

    // Explicit scene/clock boundaries. Target resolution and reload are outside
    // this empty-cell scenario; unexpected use fails, never returns fake success.
    private sealed class D13Ports : IGameClock, IGridSystemProvider,
        IDefenseTacticalWorldQuery, ICombatAffiliationService, ICombatAmmoResupplyRuntime,
        ICombatResolutionService, ICharacterCombatUiTextQuery
    {
        internal D13Ports(Grid grid) { Grid = grid; }
        public Grid Grid { get; }
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float delta) { DeltaTime = delta; Time += delta; FrameCount++; }
        public GridSystemManager Manager => throw new InvalidOperationException("D13 has no scene manager.");
        public bool TryGetManager(out GridSystemManager manager) { manager = null; return false; }
        public bool TryGetGrid(out Grid grid) { grid = Grid; return true; }
        public bool HasRestoreGrid => true;
        public bool IsOperationalCellWalkable(Vector2Int cell) => Grid.IsWalkable(cell);
        public bool IsRestoreCellWalkable(Vector2Int cell) => Grid.IsWalkable(cell);
        public IReadOnlyList<DefenseTacticalActorSnapshot> CaptureActors() =>
            CharacterAiEditorTestDependencies.WorldRegistry.Characters
                .Select(value => new DefenseTacticalActorSnapshot(
                    CharacterPersistentIdentity.Require(value).Value, !value.IsDead)).ToArray();
        public IReadOnlyCollection<string> CaptureTargetIds() => Array.Empty<string>();
        public CombatRelationship GetRelationship(CombatParticipantRef source, CombatParticipantRef target) =>
            CombatRelationship.Neutral;
        public bool IsProtectedFromAutomaticFire(CombatParticipantRef source, CombatParticipantRef target) => true;
        public bool IsResupplying(CharacterActor value) => false;
        public bool TryRequestAmmoResupply(CharacterActor value, out string message) =>
            throw new InvalidOperationException("Unexpected D13 resupply path.");
        public CombatAttackResult Resolve(CombatAttackRequest request) =>
            throw new InvalidOperationException("Empty-cell fire must not resolve a victim.");
        public CombatAttackPreview Preview(CombatAttackRequest request) =>
            throw new InvalidOperationException("Unexpected D13 preview.");
        public float CalculateAttackInterval(CombatStatSnapshot actor, CombatWeaponSnapshot weapon, CombatFireMode mode) => 1f;
        public float CalculateReloadTime(CombatStatSnapshot actor, CombatWeaponSnapshot weapon) => 1f;
        public float CalculateWeaponSwitchTime(CombatStatSnapshot actor, float weight) =>
            throw new InvalidOperationException("Unexpected D13 weapon switch.");
        public string Get(CharacterCombatUiTextId id) => id.ToString();
        public string GetFireModeName(CombatFireMode mode) => mode.ToString();
    }
}
#endif
