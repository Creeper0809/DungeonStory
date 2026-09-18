#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

// Root-owned integration witness. A validated historical save is controlled
// preparation; only the ensuing registered runtime consumer is claimed live.
public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    public const string Wim023ConsumerReportPath =
        "Artifacts/QA/wim-implementation/wim-023-main-installed-combat-consumer.txt";

    private IEnumerator RunWim023InstalledCombatConsumer(DungeonRuntimeLifetimeScope scope)
    {
        var world = scope.Container.Resolve<ICharacterWorldQuery>();
        foreach (var existing in world.Characters.Where(x => x != null)) existing.SetAiPaused(true);
        var data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/ExpandedSpecies/Customer_Kobold.asset");
        Wim023Require(data != null, "Actual authored Kobold archetype missing.");
        var spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
        Wim023Require(spawner?.characterPrefab != null, "Actual character prefab unavailable.");
        var factory = scope.Container.Resolve<ICharacterSpawnObjectFactory>();
        var anchor = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>()?.CurrentOwnerActor;
        var grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        Wim023Require(anchor != null && grid != null && grid.IsWalkable(anchor.GetNowXY()),
            "Prepared owner is not on a proven walkable cell.");
        var id = new GuidPersistentIdGenerator().NewCharacterId();
        var actorObject = factory.CreateInactive(spawner.characterPrefab, candidate =>
        {
            var composing = candidate.GetComponent<CharacterActor>();
            composing.EnsureRuntimeState();
            composing.Identity.SetPersistentId(id);
            if (candidate.GetComponent<AbilityWork>() == null) candidate.AddComponent<AbilityWork>();
        });
        var actor = actorObject.GetComponent<CharacterActor>();
        scope.Container.Resolve<ICharacterNarrativeCommand>().Register(
            new CharacterId(actor.Identity.PersistentId), new CharacterSpeciesId("Kobold"),
            Array.Empty<string>(), Array.Empty<string>(),
            BuiltInCharacterProficiencyIds.All.Select(proficiency => new CharacterStartingProficiencyExperience
            {
                proficiencyId = proficiency.Value, experience = 100, learningMultiplier = 1f
            }).ToArray());
        actor.Initialize(data);
        // Same controlled persistent role composition as PreparedStartPartyGameplayApplier;
        // keep the authored species/profile, not the transient customer lifetime.
        actor.characterType = CharacterType.NPC;
        actor.transform.position = anchor.transform.position;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        factory.Publish(actorObject);
        actor.SetAiPaused(true);
        Wim023Require(actor.Identity.SpeciesTag == "Kobold"
            && world.Characters.Any(x => x == actor) && grid.IsWalkable(actor.GetNowXY()),
            "Kobold identity/world publication or walkable placement missing.");
        Wim023Require(CharacterWorldPersistenceRules.IsPersistentActor(actor),
            "Consumer fixture must be a persistent NPC before whole-save capture.");
        // Populate canonical body state before capturing the complete world.
        var anatomy = scope.Container.Resolve<IAnatomyHealthRuntime>();
        var initialAnatomy = anatomy.GetAnatomySnapshot(actor);
        Wim023Require(initialAnatomy.Nodes != null && initialAnatomy.Nodes.Count > 0,
            "Actual anatomy snapshot missing.");
        string actorId = actor.Identity.PersistentId;
        var performance = scope.Container.Resolve<ICharacterPerformanceQuery>();
        var bodies = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        var baseline = CombatRuntimeStatFactory.Create(actor, bodies.GetSnapshot(actor), performance);
        float baselineSpeed = actor.GetMoveSpeed();
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        string instanceId = scope.Container.Resolve<IItemInstanceRepository>().AllocateItemInstanceId().Value;
        var prepared = saves.FromJson(saves.ToJson(saves.Capture()));
        Wim023Require(DungeonSaveSectionPayload.TryRead<DungeonSurgerySaveData>(
            prepared, SurgerySaveSection.Id, out var surgery), "Whole-save surgery section missing.");
        Wim023Require(DungeonSaveSectionPayload.TryRead<DungeonCharacterBodyHealthSaveData>(
            prepared, "combat.body-health", out var body), "Whole-save body section missing.");
        var node = body.characters.Single(x => x.characterId == actorId).anatomyNodes
            .Single(x => x.nodeId == "balance-tail");
        Wim023Require(!node.missing && string.IsNullOrEmpty(node.installedPartId)
            && node.currentHealth == node.maxHealth && node.maxHealth > 0,
            "Consumer fixture requires an intact authored balance-tail, not overwritten prior equipment.");
        var procedure = Resources.LoadAll<SurgicalProcedureSO>(SurgicalProcedureSO.ResourcePath)
            .Single(x => x.ProcedureId == "procedure:kobold-tail-balance");
        Wim023Require(procedure.TryGetInstallationEffect(out var install), "Authored installation effect missing.");
        string partId = "surgical-part:" + checked(++surgery.partSequence);
        string orderId = "surgery:" + checked(++surgery.orderSequence);
        string operation = SurgicalPartInstallationIdentity.FormatOperationId(orderId, partId);
        long mass = scope.Container.Resolve<IWorldItemStackRuntime>().MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)install.requiredItemDefinitionId).Value;
        surgery.parts.Add(new SurgicalPartInstance
        {
            partInstanceId = partId, itemDefinitionId = install.requiredItemDefinitionId,
            physicalItemInstanceId = instanceId, nodeId = "balance-tail", kind = install.partKind,
            quality = 1f, displayName = "WIM023 authored tail consumer witness",
            installed = true, installedSubjectId = actorId,
            installationOrderId = orderId, installationOperationId = operation,
            installationSubjectId = actorId, installationSourceStackId = "stack:wim023-tail-history",
            installationCommitId = "physical-batch-disposition:1:" + operation + ":1:" + mass
        });
        surgery.orders.Add(new SurgeryOrder
        {
            orderId = orderId, procedureId = procedure.ProcedureId,
            subject = new SurgicalSubjectRef { kind = SurgicalSubjectKind.Character, subjectId = actorId },
            targetNodeId = "balance-tail", selectedPartInstanceId = partId,
            state = SurgeryOrderState.Completed, resultRolled = true, resultSucceeded = true,
            resultOutcomeId = "success", resolvedEffectCount = 1
        });
        node.installedPartId = partId;
        node.installedPartKind = install.partKind;
        node.installedPartEfficiency = 1f;
        node.recoveryPolicy = PartRecoveryPolicy.MaintenanceOnly;
        DungeonSaveSectionPayload.Write(prepared, SurgerySaveSection.Id, DungeonSurgerySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, surgery);
        DungeonSaveSectionPayload.Write(prepared, "combat.body-health", DungeonCharacterBodyHealthSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, body);
        prepared.manifest = DungeonSaveManifest.Capture(prepared.sections);
        Wim023Require(saves.TryRestore(prepared, out var restored) && restored.Success,
            "Registered whole-save installed preparation rejected: " + string.Join(" | ", restored.Errors));
        Wim023Pause(scope);
        actor = scope.Container.Resolve<ICharacterWorldQuery>().Characters.Single(x => x.Identity.PersistentId == actorId);
        actor.SetAiPaused(true);
        performance = scope.Container.Resolve<ICharacterPerformanceQuery>();
        bodies = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        var enhanced = CombatRuntimeStatFactory.Create(actor, bodies.GetSnapshot(actor), performance);
        Wim023Require(Mathf.Abs(enhanced.EvasionChanceBonus - baseline.EvasionChanceBonus - .03f) < .00001f,
            "Main performance consumer did not receive authored +3 percentage points.");
        Wim023Require(Mathf.Abs(enhanced.Evasion - baseline.Evasion) < .00001f
            && Mathf.Abs(enhanced.MoveSpeed - baseline.MoveSpeed) < .00001f
            && Mathf.Abs(actor.GetMoveSpeed() - baselineSpeed) < .00001f,
            "Tail chance effect unexpectedly changed movement/functional stat or preparation changed baseline.");
        // Independent literal rule for the unarmed/no-shield/no-suppression
        // fixture. Never use Preview/Resolve itself to derive the expected roll.
        var weapon = CombatWeaponSnapshot.CreateUnarmed();
        Wim023Require(Mathf.Abs(weapon.Verb.tracking - .08f) < .00001f,
            "Unarmed tracking changed; review this independent fixture expectation.");
        float oldRaw = .02f + baseline.Evasion * .01f
            + baseline.MoveSpeed * .003f + baseline.EvasionChanceBonus - .08f;
        float oldThreshold = Mathf.Clamp(oldRaw, 0f, .35f);
        float newThreshold = Mathf.Clamp(oldRaw + .03f, 0f, .35f);
        Wim023Require(newThreshold > oldThreshold,
            "Fixture is fully clipped by a chance bound; cannot demonstrate the authored increment.");
        float roll = (oldThreshold + newThreshold) * .5f;
        CombatAttackResult Resolve(CombatStatSnapshot defender, string eventId)
        {
            var resolution = new CombatResolutionService(new Wim023CombatRolls(0f, roll, .5f),
                null, null, scope.Container.Resolve<ICharacterEnvironmentStatusQuery>(),
                scope.Container.Resolve<IEnvironmentalFieldQuery>(), world,
                scope.Container.Resolve<ICharacterEnvironmentExposureCommand>());
            return resolution.Resolve(new CombatAttackRequest(eventId, actorId, actorId,
                baseline, defender, weapon, 1, CombatFireMode.Aimed, default));
        }
        var beforeHit = Resolve(baseline, "combat:wim023:baseline");
        var afterDodge = Resolve(enhanced, "combat:wim023:installed");
        Wim023Require(beforeHit.Executed && beforeHit.Hit && !beforeHit.Evaded && beforeHit.AppliedDamage > 0f,
            "Baseline attack did not land at the independently fixed roll.");
        Wim023Require(afterDodge.Executed && afterDodge.Evaded && !afterDodge.Hit && afterDodge.AppliedDamage == 0f,
            "Actual combat resolution did not turn the same attack into an evasion.");
        Wim023Require(scope.Container.Resolve<ISurgicalPartRuntime>().Parts.Single(x => x.partInstanceId == partId)
                .installedSubjectId == actorId
            && !scope.Container.Resolve<IWorldItemStackRuntime>().GetAllStacks().Any(x => x.ItemInstanceId == instanceId),
            "Installed ownership lost or duplicate physical part appeared.");
        wim023ProducerLines.Add("PASS authored Kobold (controlled persistent NPC/AbilityWork and proficiency100) -> registered restore -> installed part -> main performance/body"
            + " -> CombatRuntimeStatFactory -> CombatResolutionService.Resolve; same roll=" + roll
            + "; bonus=" + baseline.EvasionChanceBonus + "->" + enhanced.EvasionChanceBonus
            + "; damage=" + beforeHit.AppliedDamage + "->" + afterDodge.AppliedDamage
            + "; evaded=false->true; installed physical duplicates=0");
        yield break;
    }

    private sealed class Wim023CombatRolls : ICombatRandomSource
    {
        private readonly float[] values;
        private int next;
        public Wim023CombatRolls(params float[] values) => this.values = values;
        public float Next01()
        {
            if (next >= values.Length) throw new InvalidOperationException("Unexpected combat RNG draw.");
            return values[next++];
        }
    }
}
#endif
