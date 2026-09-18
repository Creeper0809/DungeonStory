using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Real resolver with controlled, read-only ownership/projection inputs; not a natural equip test.</summary>
public static class Wim002MaterialProtectionDebugScenarios
{
    public static void RunProtectedPlay(DungeonRuntimeLifetimeScope scope)
    {
        Require(EditorApplication.isPlaying && Time.timeScale == 0f && scope?.Container != null,
            "Requires the paused protected main Play session.");
        IObjectResolver services = scope.Container;
        Require(services.Resolve<ICharacterEnvironmentProtectionResolver>()
            is CharacterEnvironmentProtectionResolver, "Live resolver registration missing.");
        IApparelDefinitionCatalog definitions = services.Resolve<IApparelDefinitionCatalog>();
        ITextileMaterialCatalog materials = services.Resolve<ITextileMaterialCatalog>();
        ApparelDefinitionSO[] garments = definitions.Definitions
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal).Take(2).ToArray();
        Require(garments.Length == 2 && materials.Definitions.Count > 0, "Authored catalog missing.");
        string materialId = materials.Definitions[0].MaterialId;
        GameObject obj = new("WIM002 controlled resolver witness");
        EnvironmentalWorkwearSO fixedWear = ScriptableObject.CreateInstance<EnvironmentalWorkwearSO>();
        try
        {
            CharacterActor actor = obj.AddComponent<CharacterActor>();
            actor.PrepareForComposition();
            CharacterAiEditorTestDependencies.Inject(obj);
            actor.data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                "Assets/Resources/SO/Character/Customer_Vampire.asset");
            Require(actor.data != null, "Authored witness missing.");
            actor.characterType = CharacterType.NPC;
            actor.RefreshAbilityCache();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new CharacterId("character:wim002:resolver"));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            Require(actor.IsUnpublishedComposition, "Controlled witness entered the live registry.");
            CharacterId id = CharacterPersistentIdentity.Require(actor);
            ThermalProtectionProfile traitBaseline = new();
            foreach (CharacterTraitSO trait in actor.Progression.ResolveSelectedTraits())
            {
                ThermalProtectionProfile authored = trait?.environmentalProtection;
                if (authored == null) continue;
                // Independent arithmetic over authored inputs, not Resolve/Add under test.
                traitBaseline.comfortMinimumOffset += authored.comfortMinimumOffset;
                traitBaseline.comfortMaximumOffset += authored.comfortMaximumOffset;
                traitBaseline.safeMinimumOffset += authored.safeMinimumOffset;
                traitBaseline.safeMaximumOffset += authored.safeMaximumOffset;
                traitBaseline.coldExposureMultiplier *= Mathf.Clamp(authored.coldExposureMultiplier, .05f, 2f);
                traitBaseline.heatExposureMultiplier *= Mathf.Clamp(authored.heatExposureMultiplier, .05f, 2f);
            }
            fixedWear.Configure("workwear:qa:wim002", garments[0].PhysicalItemId, "QA fixed protection",
                "Controlled fixed protection", Array.Empty<string>(), new ThermalProtectionProfile
                {
                    comfortMinimumOffset = -8f, safeMinimumOffset = -8f,
                    coldExposureMultiplier = 0.35f
                }, string.Empty);
            List<EquippedApparelSnapshot> equipped = new();
            List<WorldItemStackSnapshot> stacks = new();
            List<ApparelProjectionKey> observedKeys = new();
            int physicalVersion = 1;
            bool fitAllowed = true;
            IEnvironmentalWorkwearQuery workwear = Proxy<IEnvironmentalWorkwearQuery>((method, args) =>
            {
                if (method.Name == "TryGetEquipped")
                {
                    Require(id.Equals((CharacterId)args[0]), "Wrong wearer query.");
                    args[1] = fixedWear;
                    return true;
                }
                if (method.Name == "get_Version") return 1;
                throw Unexpected(method);
            });
            ICharacterApparelQuery apparel = Proxy<ICharacterApparelQuery>((method, args) =>
            {
                if (method.Name == "GetEquipped")
                {
                    Require(id.Equals((CharacterId)args[0]), "Wrong equipped owner.");
                    return equipped;
                }
                if (method.Name == "get_Version") return 1;
                throw Unexpected(method);
            });
            IWorldItemStackRuntime items = Proxy<IWorldItemStackRuntime>((method, args) =>
            {
                if (method.Name == "GetAllStacks") return stacks;
                if (method.Name == "get_ItemStackVersion") return physicalVersion;
                if (method.Name == "TryGetStack")
                {
                    WorldItemStackSnapshot found = stacks.SingleOrDefault(value => value.StackId == (string)args[0]);
                    args[1] = found;
                    return found != null;
                }
                // Any command here is a test failure, including otherwise successful writes.
                throw Unexpected(method);
            });
            IAnatomyAttachmentQuery anatomy = Proxy<IAnatomyAttachmentQuery>((method, args) =>
            {
                if (method.Name == "CanEquip")
                {
                    args[3] = new ApparelFitAssessment(ApparelBodyForm.Humanoid,
                        ApparelSizeClass.Medium, ((ApparelDefinitionSO)args[1]).OccupiedPoints,
                        ApparelModificationKind.None, false);
                    args[4] = fitAllowed ? DomainFailure.None
                        : new DomainFailure(FailureCode.ApparelAttachmentMissing, "controlled-missing-arm");
                    return fitAllowed;
                }
                throw Unexpected(method);
            });
            IApparelMaterialProjector projector = Proxy<IApparelMaterialProjector>((method, args) =>
            {
                if (method.Name != "GetOrCreate") throw Unexpected(method);
                ApparelProjectionKey key = (ApparelProjectionKey)args[0];
                observedKeys.Add(key);
                return key.DurabilityBand == 4
                    ? new ApparelDerivedStats(.6f, .4f, 0f, 0f, 0f, 100f, 1f, 0f, 1f)
                    : new ApparelDerivedStats(.2f, .1f, 0f, 0f, 0f, 100f, 1f, 0f, 1f);
            });
            CharacterEnvironmentProtectionResolver resolver = new(
                workwear, apparel, items, definitions, materials, projector, anatomy);
            Check(resolver.Resolve(actor), -8f, 0f, .35f, 1f, "fixed-only");
            AddGarment(0);
            Check(resolver.Resolve(actor), -9.2f, .8f, .3185f, .94f, "single-material");
            AddGarment(1);
            Check(resolver.Resolve(actor), -10f, 1.6f, .2975f, .88f, "outfit-cap/fixed-once");
            string before = Fingerprint(stacks);
            observedKeys.Clear();
            for (int i = 0; i < 3; i++)
                Check(resolver.Resolve(actor), -10f, 1.6f, .2975f, .88f, "repeat");
            Require(before == Fingerprint(stacks) && equipped.Count == 2,
                "Read-only resolve mutated physical or equipped authority.");
            Require(observedKeys.Count == 6 && observedKeys.All(key => key.DurabilityBand == 4
                && key.Condition == TextileConditionBand.Ready), "Unexpected projection count/state.");

            equipped.RemoveAt(1);
            stacks.RemoveAt(1);
            ApparelInstanceState damaged = Read(stacks[0]);
            damaged.durability = 26f;
            damaged.moisture = 100f;
            stacks[0] = CopyWith(stacks[0], stacks[0].DestinationId,
                new[] { ApparelItemStateCodec.Create(damaged) });
            physicalVersion++;
            observedKeys.Clear();
            Check(resolver.Resolve(actor), -8.4f, .2f, .3395f, .985f, "current-physical-state");
            Require(observedKeys.Count == 1 && observedKeys[0].DurabilityBand == 1
                && observedKeys[0].Condition == TextileConditionBand.Wet,
                "Current instance state did not reach projection key.");
            fitAllowed = false;
            Check(resolver.Resolve(actor), -8f, 0f, .35f, 1f, "temporarily-ineligible-fit");
            fitAllowed = true;
            Check(resolver.Resolve(actor), -8.4f, .2f, .3395f, .985f, "fit-recovered");
            string validDestination = stacks[0].DestinationId;
            stacks[0] = CopyWith(stacks[0], "apparel-equipped:character:wim002:other", stacks[0].Components);
            physicalVersion++;
            bool rejectedWrongOwner = false;
            try { resolver.Resolve(actor); }
            catch (InvalidOperationException error)
            {
                rejectedWrongOwner = error.Message.Contains("physical ownership");
            }
            Require(rejectedWrongOwner && stacks[0].Quantity == 1 && equipped.Count == 1,
                "Physical owner mismatch was not rejected without mutation.");
            stacks[0] = CopyWith(stacks[0], validDestination, stacks[0].Components);
            physicalVersion++;
            equipped.Clear();
            Check(resolver.Resolve(actor), -8f, 0f, .35f, 1f, "unequipped-no-stale-material");
            Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            File.WriteAllLines("Artifacts/QA/wim-implementation/wim-002-material-protection-focused.txt", new[]
            {
                "scope=protected-main-play;real-resolver;controlled-ownership-and-projection-inputs",
                "live-composition-registration=PASS",
                "fixed-only/single-material/outfit-cap/fixed-once=PASS",
                "read-only-repeat/current-physical-state/unequipped-no-stale-effect=PASS",
                "expected-fit-rejection-and-recovery/no-owner-mismatch-fallback=PASS",
                "not-covered=natural-equip;real-material-projector-numerics;waterproof-and-wear;whole-world-restore",
                "result=PASS"
            });

            void AddGarment(int index)
            {
                ApparelDefinitionSO definition = garments[index];
                string instanceId = "item-instance:wim002:" + index;
                equipped.Add(new EquippedApparelSnapshot(id, new ItemInstanceId(instanceId),
                    definition.ApparelId, definition.Layer, definition.OccupiedPoints));
                stacks.Add(new WorldItemStackSnapshot
                {
                    StackId = "stack:wim002:" + index, ItemInstanceId = instanceId,
                    ItemId = definition.PhysicalItemId, Quantity = 1, ContentRevision = 1,
                    State = WorldItemStackState.Carried,
                    DestinationId = CharacterApparelAggregate.EquippedDestinationPrefix + id.Value,
                    Components = new[] { ApparelItemStateCodec.Create(new ApparelInstanceState
                    {
                        apparelDefinitionId = definition.ApparelId, primaryMaterialId = materialId,
                        durability = 100f, size = ApparelSizeClass.Medium,
                        craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
                        deterministicBatchHash = (ulong)(index + 1)
                    }) }
                });
                physicalVersion++;
            }
            void Check(ThermalProtectionProfile actual, float minimum, float maximum,
                float cold, float heat, string stage) =>
                Wim002MaterialProtectionDebugScenarios.Check(actual, minimum, maximum,
                    cold, heat, stage, traitBaseline);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(obj);
            UnityEngine.Object.DestroyImmediate(fixedWear);
        }
    }

    private static ApparelInstanceState Read(WorldItemStackSnapshot stack)
    {
        Require(ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState state), "Unreadable component.");
        return state;
    }
    private static WorldItemStackSnapshot CopyWith(WorldItemStackSnapshot source, string destination,
        IReadOnlyList<ItemInstanceComponentSaveData> components) => new()
    {
        StackId = source.StackId, ItemInstanceId = source.ItemInstanceId, ItemId = source.ItemId,
        Quantity = source.Quantity, ContentRevision = source.ContentRevision + 1,
        State = source.State, DestinationId = destination, Components = components
    };
    private static string Fingerprint(IEnumerable<WorldItemStackSnapshot> stacks) => string.Join("|",
        stacks.Select(value => value.StackId + ":" + value.Quantity + ":" + value.ContentRevision
            + ":" + value.State + ":" + value.DestinationId + ":" + JsonUtility.ToJson(Read(value))));
    private static void Check(ThermalProtectionProfile actual, float minimum, float maximum,
        float cold, float heat, string stage, ThermalProtectionProfile traits)
    {
        Near(actual.comfortMinimumOffset, minimum + traits.comfortMinimumOffset, stage + " comfort-min");
        Near(actual.safeMinimumOffset, minimum + traits.safeMinimumOffset, stage + " safe-min");
        Near(actual.comfortMaximumOffset, maximum + traits.comfortMaximumOffset, stage + " comfort-max");
        Near(actual.safeMaximumOffset, maximum + traits.safeMaximumOffset, stage + " safe-max");
        Near(actual.coldExposureMultiplier, cold * traits.coldExposureMultiplier, stage + " cold");
        Near(actual.heatExposureMultiplier, heat * traits.heatExposureMultiplier, stage + " heat");
    }
    private static void Near(float actual, float expected, string stage) => Require(
        !float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) <= .00001f,
        stage + ": expected=" + expected + ", actual=" + actual);
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("WIM002: " + reason);
    }
    private static Exception Unexpected(MethodInfo method) =>
        new InvalidOperationException("Unexpected fixture call (or mutation): " + method.Name);
    private static T Proxy<T>(Func<MethodInfo, object[], object> invoke) where T : class
    {
        T instance = DispatchProxy.Create<T, QueryProxy>();
        ((QueryProxy)(object)instance).Handler = invoke;
        return instance;
    }
    public class QueryProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo targetMethod, object[] args) => Handler(targetMethod, args);
    }
}
