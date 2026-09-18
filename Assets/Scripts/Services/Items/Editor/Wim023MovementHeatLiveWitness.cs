#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

// Root-owned bounded consumer proof; historical installation is preparation.
// The accepted combat and clinical witnesses are intentionally not rerun here.
public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    public const string Wim023MotionHeatReportPath =
        "Artifacts/QA/wim-implementation/wim-023-main-movement-heat-consumers.txt";

    private IEnumerator RunWim023MotionHeatConsumers(DungeonRuntimeLifetimeScope scope)
    {
        var beastkin = CreateWim023ConsumerActor(scope, "Beastkin");
        var motionBefore = ProbeWim023ActualMovement(scope, beastkin);
        beastkin = RestoreWim023ConsumerParts(scope, beastkin,
            "procedure:beastkin-sprint-joint", "leg:left", "leg:left", "leg:right");
        var motionAfter = ProbeWim023ActualMovement(scope, beastkin);
        Wim023Require(motionBefore.samples == motionAfter.samples
            && motionBefore.stride == motionAfter.stride,
            "Movement sampling cadence changed across installation.");
        float movementRatio = motionAfter.distance / motionBefore.distance;
        Wim023Require(Mathf.Abs(movementRatio - 1.08f) < .001f,
            "Actual paired-leg position displacement must increase 8%, not compound per leg: " + movementRatio);
        wim023ProducerLines.Add("PASS authored Beastkin paired leg:left/right -> validated current restore"
            + " -> main AbilityMove.Move2PosBySpeed -> actual transform commits; distance="
            + motionBefore.distance + "->" + motionAfter.distance + "; ratio=" + movementRatio
            + "; equal samples=" + motionAfter.samples + "; scheduler stride=" + motionAfter.stride
            + "; controlled dt=.001; no path/AI scheduling proof");

        var demon = CreateWim023ConsumerActor(scope, "Demon");
        var field = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        var originalField = field.Capture();
        var position = demon.GetNowXY();
        var hotField = field.Capture();
        hotField.cells.RemoveAll(x => x.x == position.x && x.y == position.y);
        hotField.cells.Add(new EnvironmentalCellSaveData
        {
            x = position.x, y = position.y, temperatureC = 50f, airQuality = 100f, lightLevel = 100f
        });
        hotField.cells.Sort((a, b) => checked(a.y * hotField.width + a.x)
            .CompareTo(checked(b.y * hotField.width + b.x)));
        try
        {
            field.Restore(field.PrepareRestore(hotField));
            float heatBefore = ProbeWim023ActualHeat(scope, demon);
            demon = RestoreWim023ConsumerParts(scope, demon,
                "procedure:demon-heat-sac", "heat-sac", "heat-sac");
            Wim023Require(demon.GetNowXY() == position, "Heat subject moved during preparation.");
            float heatAfter = ProbeWim023ActualHeat(scope, demon);
            float heatRatio = heatAfter / heatBefore;
            Wim023Require(Mathf.Abs(heatRatio - .8f) < .0001f,
                "Actual heat-exposure increment must decrease 20%: " + heatRatio);
            wim023ProducerLines.Add("PASS authored Demon heat-sac -> validated current restore"
                + " -> main CharacterEnvironmentUnityAdapter.Tick -> actual heatExposure delta; 50C/air100/light100"
                + "; 1-second gain=" + heatBefore + "->" + heatAfter + "; ratio=" + heatRatio
                + "; no exposure-output writes; injected clock restored");
        }
        finally { field.Restore(field.PrepareRestore(originalField)); }
        yield break;
    }

    private static CharacterActor CreateWim023ConsumerActor(DungeonRuntimeLifetimeScope scope, string species)
    {
        foreach (var existing in scope.Container.Resolve<ICharacterWorldQuery>().Characters.Where(x => x != null))
            existing.SetAiPaused(true);
        var data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/ExpandedSpecies/Customer_" + species + ".asset");
        var spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
        var anchor = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>()?.CurrentOwnerActor;
        var grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        Wim023Require(data != null && spawner?.characterPrefab != null && anchor != null
            && grid != null && grid.IsWalkable(anchor.GetNowXY()), "Authored actor/walkable fixture unavailable.");
        var factory = scope.Container.Resolve<ICharacterSpawnObjectFactory>();
        var id = new GuidPersistentIdGenerator().NewCharacterId();
        var instance = factory.CreateInactive(spawner.characterPrefab, candidate =>
        {
            var composing = candidate.GetComponent<CharacterActor>();
            composing.EnsureRuntimeState();
            composing.Identity.SetPersistentId(id);
            if (candidate.GetComponent<AbilityWork>() == null) candidate.AddComponent<AbilityWork>();
        });
        var actor = instance.GetComponent<CharacterActor>();
        scope.Container.Resolve<ICharacterNarrativeCommand>().Register(id,
            new CharacterSpeciesId(species), Array.Empty<string>(), Array.Empty<string>(),
            BuiltInCharacterProficiencyIds.All.Select(x => new CharacterStartingProficiencyExperience
                { proficiencyId = x.Value, experience = 100, learningMultiplier = 1f }).ToArray());
        actor.Initialize(data);
        actor.characterType = CharacterType.NPC;
        actor.transform.position = anchor.transform.position;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        factory.Publish(instance);
        actor.SetAiPaused(true);
        Wim023Require(actor.Identity.SpeciesTag == species && CharacterWorldPersistenceRules.IsPersistentActor(actor),
            "Authored species/persistent actor composition mismatch.");
        Wim023Require(scope.Container.Resolve<IAnatomyHealthRuntime>().GetAnatomySnapshot(actor).Nodes.Count > 0,
            "Canonical anatomy unavailable.");
        return actor;
    }

    private static CharacterActor RestoreWim023ConsumerParts(DungeonRuntimeLifetimeScope scope,
        CharacterActor actor, string procedureId, string intrinsicNode, params string[] targetNodes)
    {
        string actorId = actor.Identity.PersistentId;
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        // Allocate before Capture so the current item-instance sequence owns all identities.
        var instanceIds = targetNodes.Select(_ => scope.Container.Resolve<IItemInstanceRepository>()
            .AllocateItemInstanceId().Value).ToArray();
        var prepared = saves.FromJson(saves.ToJson(saves.Capture()));
        Wim023Require(DungeonSaveSectionPayload.TryRead<DungeonSurgerySaveData>(prepared,
            SurgerySaveSection.Id, out var surgery), "Surgery section missing.");
        Wim023Require(DungeonSaveSectionPayload.TryRead<DungeonCharacterBodyHealthSaveData>(prepared,
            "combat.body-health", out var body), "Body section missing.");
        var procedure = Resources.LoadAll<SurgicalProcedureSO>(SurgicalProcedureSO.ResourcePath)
            .Single(x => x.ProcedureId == procedureId);
        Wim023Require(procedure.TryGetInstallationEffect(out var effect), "Authored installation missing.");
        long grams = scope.Container.Resolve<IWorldItemStackRuntime>().MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)effect.requiredItemDefinitionId).Value;
        var partIds = new List<string>();
        for (int i = 0; i < targetNodes.Length; i++)
        {
            var node = body.characters.Single(x => x.characterId == actorId).anatomyNodes
                .Single(x => x.nodeId == targetNodes[i]);
            Wim023Require(!node.missing && string.IsNullOrEmpty(node.installedPartId)
                && node.currentHealth == node.maxHealth && node.maxHealth > 0,
                "Historical fixture requires intact uninstalled target: " + targetNodes[i]);
            string partId = "surgical-part:" + checked(++surgery.partSequence);
            string orderId = "surgery:" + checked(++surgery.orderSequence);
            string operation = SurgicalPartInstallationIdentity.FormatOperationId(orderId, partId);
            partIds.Add(partId);
            surgery.parts.Add(new SurgicalPartInstance
            {
                partInstanceId = partId, itemDefinitionId = effect.requiredItemDefinitionId,
                physicalItemInstanceId = instanceIds[i], nodeId = intrinsicNode, kind = effect.partKind,
                quality = 1f, displayName = "WIM023 authored consumer history",
                installed = true, installedSubjectId = actorId,
                installationOrderId = orderId, installationOperationId = operation,
                installationSubjectId = actorId, installationSourceStackId = "stack:wim023-consumer-history-" + i,
                installationCommitId = "physical-batch-disposition:1:" + operation + ":1:" + grams
            });
            surgery.orders.Add(new SurgeryOrder
            {
                orderId = orderId, procedureId = procedureId,
                subject = new SurgicalSubjectRef { kind = SurgicalSubjectKind.Character, subjectId = actorId },
                targetNodeId = targetNodes[i], selectedPartInstanceId = partId,
                state = SurgeryOrderState.Completed, resultRolled = true, resultSucceeded = true,
                resultOutcomeId = "success", resolvedEffectCount = 1
            });
            node.installedPartId = partId;
            node.installedPartKind = effect.partKind;
            node.installedPartEfficiency = 1f;
            node.recoveryPolicy = PartRecoveryPolicy.MaintenanceOnly;
        }
        DungeonSaveSectionPayload.Write(prepared, SurgerySaveSection.Id, DungeonSurgerySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, surgery);
        DungeonSaveSectionPayload.Write(prepared, "combat.body-health", DungeonCharacterBodyHealthSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, body);
        prepared.manifest = DungeonSaveManifest.Capture(prepared.sections);
        Wim023Require(saves.TryRestore(prepared, out var restored) && restored.Success,
            "Current whole-save historical installation failed: " + string.Join(" | ", restored.Errors));
        Wim023Pause(scope);
        var world = scope.Container.Resolve<ICharacterWorldQuery>();
        foreach (var existing in world.Characters.Where(x => x != null)) existing.SetAiPaused(true);
        actor = world.Characters.Single(x => x.Identity.PersistentId == actorId);
        Wim023Require(scope.Container.Resolve<ISurgicalPartRuntime>().Parts.Count(x => partIds.Contains(x.partInstanceId)
                && x.installed && x.installedSubjectId == actorId) == targetNodes.Length
            && !scope.Container.Resolve<IWorldItemStackRuntime>().GetAllStacks()
                .Any(x => instanceIds.Contains(x.ItemInstanceId)), "Installed ownership or physical duplication.");
        return actor;
    }

    private static (float distance, int samples, int stride) ProbeWim023ActualMovement(
        DungeonRuntimeLifetimeScope scope, CharacterActor actor)
    {
        var move = actor.GetComponent<AbilityMove>();
        var grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        var clockField = typeof(AbilityMove).GetField("gameClock", BindingFlags.Instance | BindingFlags.NonPublic);
        Wim023Require(move != null && grid != null && clockField != null, "Actual movement adapter unavailable.");
        var oldClock = clockField.GetValue(move);
        Vector3 start = actor.transform.position;
        Vector3 end = start + Vector3.right * .125f;
        Wim023Require(grid.GetXY(start) == grid.GetXY(end) && grid.IsWalkable(grid.GetXY(end)),
            "Movement probe must stay in the same proven walkable cell.");
        int stride = scope.Container.Resolve<ICharacterAiSchedulingService>().GetMovementFrameStride(actor);
        var pending = new Stack<IEnumerator>();
        int samples = 0;
        try
        {
            clockField.SetValue(move, new Wim023ConsumerClock(.001f));
            pending.Push(move.Move2PosBySpeed(end));
            while (pending.Count > 0 && samples < 128)
            {
                var current = pending.Peek();
                if (!current.MoveNext()) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { pending.Push(nested); continue; }
                Wim023Require(current.Current == null, "Unexpected time instruction in movement probe.");
                samples++;
                float distance = Vector3.Distance(start, actor.transform.position);
                if (distance <= .000001f) continue;
                Wim023Require(distance < .12f && move.LastGridMoveFailureReason == GridMoveFailureReason.None,
                    "Movement ended/clipped/failed before an interior position sample.");
                return (distance, samples, stride);
            }
            throw new InvalidOperationException("Actual movement produced no bounded position commit; reason="
                + move.LastGridMoveFailureReason);
        }
        finally
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            clockField.SetValue(move, oldClock);
            actor.transform.position = start;
        }
    }

    private static float ProbeWim023ActualHeat(DungeonRuntimeLifetimeScope scope, CharacterActor actor)
    {
        var adapter = scope.Container.Resolve<ICharacterEnvironmentStatusQuery>() as CharacterEnvironmentUnityAdapter;
        var clockField = typeof(CharacterEnvironmentUnityAdapter).GetField("clock",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Wim023Require(adapter != null && clockField != null, "Actual environment adapter unavailable.");
        var field = scope.Container.Resolve<IEnvironmentalFieldQuery>();
        Wim023Require(field.IsInitialized && field.TryGetCell(actor.GetNowXY(), out var cell)
            && cell.TemperatureC == 50f && cell.AirQuality == 100f && cell.LightLevel == 100f,
            "Actual hot field preparation changed.");
        var id = new CharacterId(actor.Identity.PersistentId);
        float before = adapter.GetExposure(id)?.heatExposure ?? 0f;
        float health = actor.CurrentHealth;
        var oldClock = clockField.GetValue(adapter);
        try
        {
            clockField.SetValue(adapter, new Wim023ConsumerClock(1f));
            adapter.Tick();
            var after = adapter.GetExposure(id);
            Wim023Require(after != null && after.heatExposure > before && after.heatExposure < 25f
                && actor.CurrentHealth == health, "Heat probe must gain below first band without health damage.");
            return after.heatExposure - before;
        }
        finally { clockField.SetValue(adapter, oldClock); }
    }

    private sealed class Wim023ConsumerClock : IGameClock
    {
        public Wim023ConsumerClock(float deltaTime) => DeltaTime = deltaTime;
        public float DeltaTime { get; }
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
#endif
