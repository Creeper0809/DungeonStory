#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class ArcaneOverchargeA07DebugScenarios
{
    private sealed class UnexpectedOwnerDeath : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason) =>
            throw new InvalidOperationException(
                "Nonlethal A07 scenario unexpectedly reached owner death: " + reason);
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time { get; set; }
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedMana : ICharacterManaQuery
    {
        public float Current { get; set; } = 20f;

        public CharacterManaSnapshot GetMana(CharacterActor actor) =>
            new(Current, 100f, recoveryBlocked: false);

        public bool CanSpendMana(
            CharacterActor actor,
            float amount,
            out string failureReason)
        {
            failureReason = Current >= amount ? string.Empty : "insufficient";
            return Current >= amount;
        }
    }

    private sealed class FaultSelfDamageRuntime :
        IArcaneOverchargeSelfDamageRuntime
    {
        private sealed class Prepared :
            IArcaneOverchargeSelfDamagePreparation
        {
            private readonly FaultSelfDamageRuntime owner;
            private readonly IArcaneOverchargeSelfDamagePreparation actual;
            private bool finished;

            internal Prepared(
                FaultSelfDamageRuntime owner,
                IArcaneOverchargeSelfDamagePreparation actual)
            {
                this.owner = owner;
                this.actual = actual;
            }

            public float PreviousHealth => actual.PreviousHealth;
            public float ResultingHealth => actual.ResultingHealth;

            public void Complete()
            {
                if (finished)
                    return;
                actual.Complete();
                owner.Completed++;
                finished = true;
            }

            public bool TryRollback(out string failureReason)
            {
                failureReason = string.Empty;
                if (finished)
                {
                    failureReason = "fixture-already-finished";
                    return false;
                }
                if (!actual.TryRollback(out failureReason))
                    return false;
                owner.RolledBack++;
                finished = true;
                return true;
            }
        }

        internal bool Reject;
        internal int Attempts;
        internal int Completed;
        internal int RolledBack;
        private readonly IArcaneOverchargeSelfDamageRuntime actual;

        internal FaultSelfDamageRuntime(
            IArcaneOverchargeSelfDamageRuntime actual)
        {
            this.actual = actual;
        }

        public bool TryPrepare(
            CharacterActor actor,
            float amount,
            out IArcaneOverchargeSelfDamagePreparation preparation,
            out string failureReason)
        {
            Attempts++;
            preparation = null;
            if (Reject)
            {
                failureReason = "fixture-self-damage-rejected";
                return false;
            }
            if (!actual.TryPrepare(
                    actor,
                    amount,
                    out IArcaneOverchargeSelfDamagePreparation prepared,
                    out failureReason))
                return false;
            preparation = new Prepared(this, prepared);
            failureReason = string.Empty;
            return true;
        }
    }

    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A07 requires a new create-only output directory.");
        }

        Directory.CreateDirectory(output);
        try
        {
            JObject result = VerifyRejectedEffectsResumeAndReplay();
            WriteReport(output, new JObject
            {
                ["schema"] = "a07-arcane-overcharge@1",
                ["passed"] = true,
                ["rowId"] = "A07",
                ["controlledIntegration"] = true,
                ["actualGameplayScene"] = false,
                ["coverage"] = new JObject
                {
                    ["healthRejectionLeavesLeaseOnly"] = true,
                    ["identityStateRoundTrip"] = true,
                    ["durabilityRejectionPreservesHealthOnce"] = true,
                    ["resumeCompletesMissingEffectOnly"] = true,
                    ["terminalReplayNoDuplicate"] = true,
                    ["exactBeforeAfterRecorded"] = true,
                    ["productionBodyHealthMutation"] = true,
                    ["partialBodyEquipmentIdentityFreshRestore"] = true
                },
                ["operation"] = result,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            Debug.Log("A07 arcane overcharge scenarios: PASS");
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["schema"] = "a07-arcane-overcharge@1",
                ["passed"] = false,
                ["rowId"] = "A07",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    private static JObject VerifyRejectedEffectsResumeAndReplay()
    {
        GameObject host = new("A07 Arcane Overcharge Audit");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Initialize(AssetDatabase.FindAssets("t:CharacterSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
                .First(value => value != null
                    && value.id > 0
                    && value.species != null));
            const string actorId = "character:a07:arcane-overcharge";
            actor.Identity.SetPersistentId(actorId);
            actor.Progression.ApplyPreparedIdentity(
                "A07 Arcane Overcharge",
                "audit",
                new[] { 306 },
                CharacterPotentialGrade.Ordinary,
                7007,
                autoChooseDrafts: false);

            CombatEquipmentRuntime actualEquipment = CreateEquipmentRuntime(
                out WorldItemRepository repository);
            CombatEquipmentInstance weapon = actualEquipment.CreateInstance(
                "weapon:rune-blade",
                CombatEquipmentQuality.Normal);
            Require(actualEquipment.TryAssignToCharacter(
                    actorId,
                    weapon.instanceId,
                    out string assignFailure),
                "Arcane weapon assignment failed: " + assignFailure);
            D13FaultInjectingEquipmentRuntime equipment = new(actualEquipment);
            FixedClock clock = new() { Time = 70f };
            FixedMana mana = new() { Current = 20f };
            CharacterBodyHealthRuntime body = CreateBodyHealthRuntime(clock);
            BindBodyHealth(actor, body);
            body.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            Require(Mathf.Approximately(actor.MaxHealth, 100f)
                && Mathf.Approximately(actor.CurrentHealth, 100f),
                "Actor does not project the actual test body-health authority.");
            FaultSelfDamageRuntime selfDamage = new(
                new ArcaneOverchargeSelfDamageRuntime(body)) { Reject = true };
            CharacterIdentityStateStore store = new();
            ExtremeTraitRuntime traits = new(store);
            ArcaneOverchargeCommandRuntime command = new(
                traits,
                equipment,
                clock,
                mana,
                selfDamage);

            Require(!command.TryActivate(
                    actor,
                    weapon.instanceId,
                    out _,
                    out string healthFailure)
                && healthFailure == "fixture-self-damage-rejected",
                "Health rejection was not surfaced.");
            Require(traits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot leaseOnly)
                && !leaseOnly.SelfDamageApplied
                && !leaseOnly.EquipmentDurabilityApplied
                && !leaseOnly.EffectsCompleted
                && leaseOnly.FailureCount == 1
                && leaseOnly.LastFailureCode == "self-damage-rejected"
                && Mathf.Approximately(body.GetVitals(actor).CurrentHealth, 100f)
                && Mathf.Approximately(actor.CurrentHealth, 100f)
                && equipment.ArcaneOverchargeDurabilityAttempts == 0,
                "Health rejection did not leave one explicit lease-only state.");
            string activationId = leaseOnly.ActivationId;

            List<CharacterIdentityRuntimeStateSaveData> identitySave =
                RoundTrip(store.Capture());
            CharacterIdentityStateStore restoredStore = new();
            restoredStore.Restore(
                identitySave,
                actor.Progression.ResolveSelectedTraits());
            traits = new ExtremeTraitRuntime(restoredStore);
            command = new ArcaneOverchargeCommandRuntime(
                traits,
                equipment,
                clock,
                mana,
                selfDamage);
            Require(traits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot restoredLease)
                && restoredLease.ActivationId == activationId
                && restoredLease.FailureCount == 1
                && !restoredLease.SelfDamageApplied,
                "Lease-only identity state did not survive JSON restore.");

            selfDamage.Reject = false;
            equipment.RejectArcaneOverchargeDurabilityDamage = true;
            clock.Time = 71f;
            mana.Current = 90f;
            Require(!command.TryActivate(
                    actor,
                    weapon.instanceId,
                    out _,
                    out string durabilityFailure)
                && durabilityFailure.Contains(
                    "장비 내구도 적용이 거절",
                    StringComparison.Ordinal),
                "Durability rejection was not surfaced after lease restore.");
            Require(traits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot healthApplied)
                && healthApplied.ActivationId == activationId
                && healthApplied.SelfDamageApplied
                && !healthApplied.EquipmentDurabilityApplied
                && !healthApplied.EffectsCompleted
                && healthApplied.FailureCount == 2
                && healthApplied.LastFailureCode
                    == "equipment-durability-rejected"
                && Mathf.Approximately(healthApplied.HealthBefore, 100f)
                && Mathf.Approximately(healthApplied.HealthAfter, 85f)
                && Mathf.Approximately(body.GetVitals(actor).CurrentHealth, 85f)
                && Mathf.Approximately(actor.CurrentHealth, 85f)
                && selfDamage.Attempts == 2
                && selfDamage.Completed == 1
                && equipment.ArcaneOverchargeDurabilityAttempts == 1
                && actualEquipment.TryGetInstance(
                    weapon.instanceId,
                    out CombatEquipmentInstance rejectedWeapon)
                && Mathf.Approximately(rejectedWeapon.durabilityRatio, 1f),
                "Durability rejection did not preserve exactly one health mutation.");

            List<CharacterIdentityRuntimeStateSaveData> partialIdentitySave =
                RoundTrip(restoredStore.Capture());
            DungeonCharacterBodyHealthSaveData partialBodySave =
                RoundTripOwner(body.Capture());
            DungeonCombatEquipmentSaveData partialEquipmentSave =
                RoundTripOwner(actualEquipment.Capture());
            List<ItemInstanceComponentSaveData> partialPhysicalSave =
                RoundTripOwner(repository.EquipmentInstances.Values
                    .OrderBy(value => value.instanceId, StringComparer.Ordinal)
                    .Select(value => EquipmentItemStateCodec.Encode(value))
                    .ToList());
            int healthAttemptsBeforeRestore = selfDamage.Attempts;
            int healthCompletionsBeforeRestore = selfDamage.Completed;
            int durabilityAttemptsBeforeRestore =
                equipment.ArcaneOverchargeDurabilityAttempts;

            CharacterBodyHealthRuntime restoredBody =
                CreateBodyHealthRuntime(clock);
            BindBodyHealth(actor, restoredBody);
            restoredBody.PublishRestore(
                restoredBody.PrepareRestore(partialBodySave));
            CombatEquipmentRuntime restoredEquipment = CreateEquipmentRuntime(
                out WorldItemRepository restoredRepository);
            foreach (ItemInstanceComponentSaveData component in partialPhysicalSave)
            {
                Require(EquipmentItemStateCodec.TryDecodeFull(
                        component,
                        out EquipmentPhysicalStatePayload payload,
                        out string decodeFailure),
                    "Saved overcharge equipment component rejected: " + decodeFailure);
                restoredRepository.EquipmentInstances.Add(
                    payload.equipment.instanceId,
                    payload.equipment.Clone());
            }
            restoredEquipment.PublishRestoreCandidate(
                restoredEquipment.BuildRestoreCandidate(partialEquipmentSave));
            CharacterIdentityStateStore partialStore = new();
            partialStore.Restore(
                partialIdentitySave,
                actor.Progression.ResolveSelectedTraits());
            traits = new ExtremeTraitRuntime(partialStore);
            body = restoredBody;
            actualEquipment = restoredEquipment;
            equipment = new D13FaultInjectingEquipmentRuntime(actualEquipment);
            selfDamage = new FaultSelfDamageRuntime(
                new ArcaneOverchargeSelfDamageRuntime(body));
            command = new ArcaneOverchargeCommandRuntime(
                traits, equipment, clock, mana, selfDamage);
            Require(traits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot restoredPartial)
                && restoredPartial.ActivationId == activationId
                && restoredPartial.SelfDamageApplied
                && !restoredPartial.EquipmentDurabilityApplied
                && !restoredPartial.EffectsCompleted
                && restoredPartial.FailureCount == 2
                && Mathf.Approximately(actor.CurrentHealth, 85f)
                && actualEquipment.TryGetInstance(
                    weapon.instanceId,
                    out CombatEquipmentInstance restoredWeapon)
                && Mathf.Approximately(restoredWeapon.durabilityRatio, 1f)
                && JToken.DeepEquals(JToken.FromObject(partialBodySave),
                    JToken.FromObject(body.Capture()))
                && JToken.DeepEquals(JToken.FromObject(partialEquipmentSave),
                    JToken.FromObject(actualEquipment.Capture())),
                "Partially applied identity/body/equipment did not restore exactly.");
            clock.Time = 72f;
            Require(command.TryActivate(
                    actor,
                    weapon.instanceId,
                    out ArcaneOverchargeActivation activation,
                    out string completionFailure),
                "Pending overcharge did not resume: " + completionFailure);
            Require(traits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot completed)
                && completed.ActivationId == activationId
                && completed.SelfDamageApplied
                && completed.EquipmentDurabilityApplied
                && completed.EffectsCompleted
                && completed.FailureCount == 2
                && Mathf.Approximately(completed.HealthBefore, 100f)
                && Mathf.Approximately(completed.HealthAfter, 85f)
                && Mathf.Approximately(completed.DurabilityBefore, 1f)
                && Mathf.Approximately(completed.DurabilityAfter, .75f)
                && Mathf.Approximately(activation.SelfDamageFraction, .15f)
                && Mathf.Approximately(
                    activation.EquipmentDurabilityFraction,
                    .25f)
                && selfDamage.Attempts == 0
                && selfDamage.Completed == 0
                && equipment.ArcaneOverchargeDurabilityAttempts == 1
                && Mathf.Approximately(actor.CurrentHealth, 85f)
                && actualEquipment.TryGetInstance(
                    weapon.instanceId,
                    out CombatEquipmentInstance completedWeapon)
                && Mathf.Approximately(completedWeapon.durabilityRatio, .75f),
                "Resume did not apply only the missing durability effect.");

            Require(!command.TryActivate(
                    actor,
                    weapon.instanceId,
                    out _,
                    out _)
                && selfDamage.Attempts == 0
                && equipment.ArcaneOverchargeDurabilityAttempts == 1
                && Mathf.Approximately(body.GetVitals(actor).CurrentHealth, 85f)
                && Mathf.Approximately(actor.CurrentHealth, 85f)
                && actualEquipment.TryGetInstance(
                    weapon.instanceId,
                    out CombatEquipmentInstance replayWeapon)
                && Mathf.Approximately(replayWeapon.durabilityRatio, .75f),
                "Terminal retry duplicated an overcharge effect.");

            List<CharacterIdentityRuntimeStateSaveData> terminalSave =
                RoundTrip(partialStore.Capture());
            CharacterIdentityStateStore terminalStore = new();
            terminalStore.Restore(
                terminalSave,
                actor.Progression.ResolveSelectedTraits());
            ExtremeTraitRuntime terminalTraits = new(terminalStore);
            ArcaneOverchargeCommandRuntime terminalCommand = new(
                terminalTraits,
                equipment,
                clock,
                mana,
                selfDamage);
            Require(terminalTraits.TryGetArcaneOverchargeOperation(
                    actor,
                    out ArcaneOverchargeOperationSnapshot terminal)
                && terminal.EffectsCompleted
                && terminal.ActivationId == activationId
                && !terminalCommand.TryActivate(
                    actor,
                    weapon.instanceId,
                    out _,
                    out _)
                && selfDamage.Attempts == 0
                && equipment.ArcaneOverchargeDurabilityAttempts == 1
                && Mathf.Approximately(actor.CurrentHealth, 85f),
                "Terminal identity restore allowed duplicate effects.");

            return new JObject
            {
                ["activationId"] = activationId,
                ["equipmentInstanceId"] = weapon.instanceId,
                ["healthBefore"] = completed.HealthBefore,
                ["healthAfter"] = completed.HealthAfter,
                ["durabilityBefore"] = completed.DurabilityBefore,
                ["durabilityAfter"] = completed.DurabilityAfter,
                ["selfDamageAttempts"] =
                    healthAttemptsBeforeRestore + selfDamage.Attempts,
                ["selfDamageCompletions"] =
                    healthCompletionsBeforeRestore + selfDamage.Completed,
                ["durabilityAttempts"] =
                    durabilityAttemptsBeforeRestore
                        + equipment.ArcaneOverchargeDurabilityAttempts,
                ["productionBodyHealthAfter"] = body.GetVitals(actor).CurrentHealth,
                ["actorHealthAfter"] = actor.CurrentHealth,
                ["selfDamageAttemptsAfterRestore"] = selfDamage.Attempts,
                ["freshBodyEquipmentIdentityRestore"] = true,
                ["rejectionCount"] = completed.FailureCount,
                ["terminalReplayMutationCount"] = 0,
                ["identitySaveRestore"] = "PASS"
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static CombatEquipmentRuntime CreateEquipmentRuntime(
        out WorldItemRepository repository)
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        return CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(content),
            repository,
            new CharacterCarryInventoryRegistry(),
            materialCatalog: new ResourceEconomyContentCatalog(content),
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: new ResourceEquipmentModuleCatalog(content),
            itemStackRuntime:
                UnavailableEquipmentPhysicalItemGateway.Instance,
            calendar: new EditorFixedGameCalendar(137, 9));
    }

    private static CharacterBodyHealthRuntime CreateBodyHealthRuntime(
        IGameClock clock) => new(
            new UnityRealtimeClock(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            clock,
            CharacterAiEditorTestDependencies.GameEvents,
            new DynamicFrameWorkBudget(clock, new UnityUiClock()),
            new ResourceAnatomyProfileCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
            new DungeonRuntimeAggregateRootStore(),
            CharacterAiEditorTestDependencies.GameCalendar);

    private static void BindBodyHealth(
        CharacterActor actor,
        CharacterBodyHealthRuntime body) => actor.Stats.ConstructCharacterVitals(
            new CharacterStatsVitalsService(
                body,
                body,
                CharacterAiEditorTestDependencies.GameEvents,
                new CharacterDeathEventFactory(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    CharacterAiEditorTestDependencies.GameCalendar),
                new UnexpectedOwnerDeath()));

    private static T RoundTripOwner<T>(T value) =>
        JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

    private static List<CharacterIdentityRuntimeStateSaveData> RoundTrip(
        IReadOnlyList<CharacterIdentityRuntimeStateSaveData> value) =>
        JsonConvert.DeserializeObject<
            List<CharacterIdentityRuntimeStateSaveData>>(
            JsonConvert.SerializeObject(value))
        ?? throw new InvalidOperationException(
            "Identity state JSON round trip returned null.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteReport(string output, JObject report)
    {
        string path = Path.Combine(output, "report.json");
        if (File.Exists(path))
            throw new IOException("A07 report already exists: " + path);
        using FileStream stream = new(path, FileMode.CreateNew);
        using StreamWriter writer = new(stream);
        writer.Write(report.ToString(Formatting.Indented));
    }
}
#endif
