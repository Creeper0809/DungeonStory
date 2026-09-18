#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class WimEquipmentMassPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-020-equipment-mass.txt";
    private const string PendingKey = "DungeonStory.WIM020.Pending";
    private static double deadline;

    [MenuItem("DungeonStory/QA/WIM/020 Equipment Physical Mass")]
    public static void RequestRun()
    {
        if (SessionState.GetBool(PendingKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run WIM-020 from Edit Mode.");
        Write("RUNNING\n");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.update -= Verify;
            EditorApplication.update += Verify;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Verify;
            SessionState.SetBool(PendingKey, false);
            Write("FAIL\nInterrupted before verification.\n");
        }
    }

    private static void Verify()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
        if (scope == null && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Verify;
        try
        {
            Require(scope != null, "Main gameplay container unavailable.");
            IPhysicalItemMassQuery mass = scope.Container.Resolve<IPhysicalItemMassQuery>();
            ICombatEquipmentCatalog catalog = scope.Container.Resolve<ICombatEquipmentCatalog>();
            CombatEquipmentStatProjector projector = scope.Container.Resolve<CombatEquipmentStatProjector>();
            IItemInstanceRepository repository = scope.Container.Resolve<IItemInstanceRepository>();
            CombatEquipmentRuntime runtime = scope.Container.Resolve<CombatEquipmentRuntime>();
            int definitions = 0;
            foreach (CombatEquipmentDefinitionSO definition in catalog.All)
            {
                long expected = mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)PhysicalItemIds.ForEquipment(definition.EquipmentId)).Value;
                Require(projector.GetPhysicalMass(definition).Value == expected, "Definition mass mismatch: " + definition.EquipmentId);
                Require(Mathf.Approximately(projector.Build(definition, null).Weight, expected / 1000f),
                    "Preview mass mismatch: " + definition.EquipmentId);
                definitions++;
            }
            Require(definitions > 0, "Empty equipment catalog.");
            VerifyLiveComposition(runtime, projector, catalog, repository, mass);
            PhysicalStockQueryV18DebugScenarios.RunWim020MassFocused();
            PhysicalItemDebugScenarios.RunCarryCapacityTargetFromMenu();
            Write("PASS\nmain-gameplay-container=PASS\nauthored-equipment-definitions=" + definitions
                + "\ncanonical-base-preview=PASS\nmodule-ammunition-live-burden=PASS"
                + "\nphysical-component-roundtrip=PASS\nmaterial-quality-durability-charge-invariant=PASS"
                + "\ninvalid-module-negative-ammo=PASS\nwarehouse-exact-gram-admission=PASS"
                + "\napparel-harness-single-count=PASS\ncarry-capacity-unchanged=PASS\n");
            Debug.Log("WIM-020 equipment mass PASS: " + ReportPath);
        }
        catch (Exception exception)
        {
            Write("FAIL\n" + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void VerifyLiveComposition(
        CombatEquipmentRuntime runtime, CombatEquipmentStatProjector projector,
        ICombatEquipmentCatalog catalog, IItemInstanceRepository repository,
        IPhysicalItemMassQuery mass)
    {
        const string actorId = "character:wim-020-mass-fixture";
        Require(runtime.GetCarriedWeight(actorId) == 0f, "Fixture actor ID is occupied.");
        string moduleId = "item-instance:wim-020-module-fixture";
        Require(!repository.EquipmentModules.ContainsKey(moduleId), "Fixture module ID is occupied.");
        // Initial fixture represents previously acquired equipment, not a research-unlocked crafting test.
        CombatEquipmentInstance equipment = new()
        {
            instanceId = repository.AllocateItemInstanceId().Value,
            definitionId = "weapon:crossbow", materialId = "material:iron",
            quality = CombatEquipmentQuality.Masterwork, worldState = CombatEquipmentWorldState.Stored
        };
        repository.EquipmentInstances.Add(equipment.instanceId, equipment);
        try
        {
            Require(catalog.TryGet(equipment.definitionId, out CombatEquipmentDefinitionSO definition), "Crossbow definition missing.");
            Require(runtime.TryAssignToCharacter(actorId, equipment.instanceId, out string failure), "Actual assign failed: " + failure);
            long baseMass = projector.GetPhysicalMass(definition, equipment).Value;
            long moduleMass = mass.GetDefinitionUnitMass((ItemDefinitionId)PhysicalItemIds.ForEquipmentModule()).Value;
            long ammoMass = mass.GetDefinitionUnitMass((ItemDefinitionId)CombatItemDefinitions.BoltItemId).Value;
            EquipmentModuleInstance module = new()
            {
                instanceId = moduleId, definitionId = "module:weapon:balanced-core",
                state = EquipmentModuleProcessState.Installed, attachedEquipmentInstanceId = equipment.instanceId
            };
            repository.EquipmentModules.Add(moduleId, module);
            equipment.moduleSlots.Add(new EquipmentModuleSlotState { slotIndex = 0, moduleInstanceId = moduleId });
            equipment.loadedAmmunition = new LoadedAmmunitionBatch
            {
                ammunitionItemId = CombatItemDefinitions.BoltItemId, remaining = 3
            };
            long expected = checked(baseMass + moduleMass + 3 * ammoMass);
            AssertMass(expected);
            equipment.durabilityRatio = 0.01f;
            equipment.powerCharge = 0f;
            equipment.quality = CombatEquipmentQuality.Poor;
            AssertMass(expected);
            Require(runtime.TryGetPreviewStats("weapon:longsword", "material:gold", out CombatEquipmentDerivedStats gold)
                && runtime.TryGetPreviewStats("weapon:longsword", "material:iron", out CombatEquipmentDerivedStats iron)
                && Mathf.Approximately(gold.Weight, iron.Weight), "Material weight multiplier still overrides physical mass.");

            equipment.loadedAmmunition.remaining = 2;
            AssertMass(expected - ammoMass);
            equipment.moduleSlots.Clear();
            AssertMass(baseMass + 2 * ammoMass);
            equipment.loadedAmmunition.remaining = -1;
            RequireThrows(() => projector.GetPhysicalMass(definition, equipment));
            equipment.loadedAmmunition.Clear();
            equipment.moduleSlots.Add(new EquipmentModuleSlotState { moduleInstanceId = "item-instance:missing-module" });
            RequireThrows(() => projector.GetPhysicalMass(definition, equipment));
            equipment.moduleSlots.Clear();
            Require(runtime.TryUnassignSlot(actorId, CombatEquipmentLoadoutSlot.Weapon, out failure), "Actual unassign failed: " + failure);
            Require(runtime.GetCarriedWeight(actorId) == 0f, "Unassigned gear remained in burden.");

            void AssertMass(long value)
            {
                Require(projector.GetPhysicalMass(definition, equipment).Value == value, "Live gram composition mismatch.");
                Require(Mathf.Approximately(runtime.GetCarriedWeight(actorId), value / 1000f), "Actual loadout burden mismatch.");
                Require(Mathf.Approximately(runtime.GetEquippedWeight(actorId), value / 1000f), "CharacterStats burden query mismatch.");
                EquipmentModuleInstance[] attached = equipment.moduleSlots.Count > 0 ? new[] { module } : Array.Empty<EquipmentModuleInstance>();
                ItemInstanceComponentSaveData component = EquipmentItemStateCodec.Encode(equipment, attached);
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(mass,
                    (ItemDefinitionId)PhysicalItemIds.ForEquipment(equipment.definitionId), equipment.instanceId, new[] { component });
                Require(mass.GetStackUnitMass(subject.ItemId, subject).Value == value, "Physical roundtrip differs from equipped mass.");
            }
        }
        finally
        {
            // All mutations belong to this isolated PlayMode fixture; never save the scene.
            equipment.moduleSlots.Clear();
            equipment.loadedAmmunition.Clear();
            runtime.TryUnassignSlot(actorId, CombatEquipmentLoadoutSlot.Weapon, out _);
            repository.EquipmentModules.Remove(moduleId);
            repository.EquipmentInstances.Remove(equipment.instanceId);
        }
    }

    private static void RequireThrows(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid mass state was not rejected.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, text, new System.Text.UTF8Encoding(false));
    }
}
#endif
