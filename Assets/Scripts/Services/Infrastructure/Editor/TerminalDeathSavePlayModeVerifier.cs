#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Controlled real death commands and official whole-world persistence; not training data.</summary>
public static class TerminalDeathSavePlayModeVerifier
{
    public static IEnumerator Verify(ICollection<string> warnings, ICollection<string> errors)
    {
        var evidence = new List<string>();
        bool passed = false;
        IEnumerator checks = RunChecks(evidence);
        while (true)
        {
            bool moved;
            try { moved = checks.MoveNext(); }
            catch (Exception exception)
            {
                evidence.Add("failure=" + exception);
                break;
            }
            if (!moved)
            {
                passed = warnings.Count == 0 && errors.Count == 0;
                break;
            }
            yield return checks.Current;
        }
        evidence.Insert(0, "RESULT=" + (passed ? "PASS" : "FAIL"));
        evidence.Add("warningCount=" + warnings.Count + ";errorCount=" + errors.Count);
        evidence.AddRange(warnings.Select(value => "warning=" + value));
        evidence.AddRange(errors.Select(value => "error=" + value));
        evidence.Add("scope=controlled-production-death/full-world-save;trainingEligible=false;humanApprovalClaimed=false;fullAuditClosure=false");
        WriteNew("terminal-death-save-report.txt", string.Join("\n", evidence) + "\n");
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    private static IEnumerator RunChecks(List<string> evidence)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Terminal death/save verification requires isolated batch persistence.");
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value?.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Gameplay container did not become ready.");
        IGameSpeedController speed = scope.Container.Resolve<IGameSpeedController>();
        speed.SetPaused(true);
        if (scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out CharacterSpawner spawner))
            spawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        IOwnerRunManagerProvider owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
        Require(owners.TryGetManager(out OwnerRunManager manager), "Owner manager is missing.");
        if (manager.CurrentOwnerActor == null)
        {
            evidence.Add("controlledPartyBootstrap=" + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            for (int frame = 0; frame < 5; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(
            scope, out string preparation), "Tier-zero preparation failed: " + preparation);
        Require(owners.TryGetManager(out manager) && manager.CurrentOwnerActor != null,
            "Production party bootstrap did not publish an owner.");
        CharacterActor owner = manager.CurrentOwnerActor;
        CharacterActor staff = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .FirstOrDefault(value => value != null && !value.IsOwner && !value.IsDead
                && value.Identity?.CharacterType == CharacterType.NPC);
        Require(staff != null, "Production party bootstrap did not publish staff.");
        CharacterId staffId = CharacterPersistentIdentity.Require(staff);
        IGriefTraumaService grief = scope.Container.Resolve<IGriefTraumaService>();
        grief.Require(staffId);
        grief.Require(CharacterId.Owner);
        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        ICharacterBodyHealthCommand health = scope.Container.Resolve<ICharacterBodyHealthCommand>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        IMetaProgressionPersistencePort meta = scope.Container.Resolve<IMetaProgressionPersistencePort>();
        int completedBefore = meta.State.CompletedRunCount;
        int staffDeaths = 0, ownerDeaths = 0, results = 0;
        using IDisposable deaths = events.Subscribe<CharacterDeathEvent>(value =>
        {
            if (value.CharacterId.Equals(staffId)) staffDeaths++;
            if (value.CharacterId.Equals(CharacterId.Owner)) ownerDeaths++;
        });
        using IDisposable runResults = events.Subscribe<RunResultReadyEvent>(_ => results++);
        health.Kill(staff, CharacterDeathCauseCode.Unknown, "qa:staff-death-save");
        Require(staff.IsDead && staffDeaths == 1 && !grief.TryGet(staffId, out _),
            "Real staff death did not retire exactly its active psychosocial state.");
        Require(grief.TryGet(CharacterId.Owner, out CharacterGriefAggregate survivor)
            && survivor.Capture().grief.Any(value => value.deceasedCharacterId == staffId.Value),
            "Survivor grief history was lost with the deceased's active state.");
        DungeonGameSaveData staffWorld = saves.Capture();
        WriteNew("staff-death-world.json", saves.ToJson(staffWorld));
        Require(saves.TryRestore(staffWorld, out DungeonGameRestoreReport staffRestore),
            "Official staff-death world restore failed: " + string.Join(" | ", staffRestore.Errors));
        Require(owners.TryGetManager(out manager) && manager.CurrentOwnerActor != owner,
            "Staff-death world restore did not publish a fresh owner.");
        owner = manager.CurrentOwnerActor;
        Require(!grief.TryGet(staffId, out _) && grief.TryGet(CharacterId.Owner, out survivor)
            && survivor.Capture().grief.Any(value => value.deceasedCharacterId == staffId.Value),
            "Official world restore lost active retirement or survivor grief.");
        evidence.Add("staffDeath=PASS;events=1;activePsychosocialRetired=true;survivorGriefPreserved=true;officialFreshRestore=true;sections=" + staffWorld.sections.Count);

        string autosavePath = scope.Container.Resolve<IDungeonSaveSlotCatalog>()
            .GetPath(DungeonGameSaveSlotService.AutoSaveSlot);
        byte[] autosaveBefore = File.Exists(autosavePath) ? File.ReadAllBytes(autosavePath) : null;
        health.Kill(owner, CharacterDeathCauseCode.Unknown, "qa:owner-death-save");
        Require(owner.IsDead && manager.IsRunEnded && meta.HasEnded && ownerDeaths == 1 && results == 1,
            "Real owner death did not produce exactly one terminal run result.");
        Require(autosaveBefore == null ? !File.Exists(autosavePath)
            : File.ReadAllBytes(autosavePath).SequenceEqual(autosaveBefore),
            "Final autosave executed synchronously inside the death event.");
        // Exercise the registered production late-tick entry point, not a direct Save call.
        yield return null;
        yield return null;
        Require(File.Exists(autosavePath), "Production late tick did not persist the final autosave.");
        string terminalJson = File.ReadAllText(autosavePath);
        WriteNew("owner-death-autosave-world.json", terminalJson);
        DungeonGameSaveData terminal = saves.FromJson(terminalJson);
        DungeonCharacterWorldSaveData actors = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
            terminal, CharacterWorldSaveSection.Id);
        DungeonCharacterSaveData savedOwner = actors.actors.Single(value => value.isOwner);
        Require(savedOwner.persistentId == CharacterId.Owner.Value && savedOwner.currentHealth <= 0f
            && savedOwner.lifecycleState == CharacterLifecycleState.Despawned,
            "Final autosave did not retain the exact inert deceased owner.");
        int earned = meta.State.LifetimeEarnedCurrency;
        string ledgerBefore = terminal.sections.Single(value => value.sectionId == GameplayOutcomeLedgerSaveSection.Id).payloadJson;
        Require(scope.Container.Resolve<IDungeonGameSaveSlotService>().TryLoad(
            DungeonGameSaveSlotService.AutoSaveSlot, out DungeonGameRestoreReport terminalRestore),
            "Official final autosave load failed: " + string.Join(" | ", terminalRestore.Errors));
        Require(owners.TryGetManager(out manager), "Terminal load lost the owner manager.");
        evidence.Add("terminalLoadState=ownerFresh:" + (manager.CurrentOwnerActor != owner)
            + ";dead:" + manager.CurrentOwnerActor.IsDead + ";runEnded:" + manager.IsRunEnded
            + ";canRunAi:" + manager.CurrentOwnerActor.CanRunAi + ";metaEnded:" + meta.HasEnded
            + ";completedRuns:" + meta.State.CompletedRunCount + ";earned:" + meta.State.LifetimeEarnedCurrency);
        Require(owners.TryGetManager(out manager) && manager.CurrentOwnerActor != owner
            && manager.CurrentOwnerActor.IsDead && manager.IsRunEnded && !manager.CurrentOwnerActor.CanRunAi
            && meta.HasEnded && meta.State.CompletedRunCount == completedBefore + 1
            && meta.State.LifetimeEarnedCurrency == earned,
            "Terminal load revived the owner, restarted the run or duplicated meta currency.");
        Require(!grief.TryGet(CharacterId.Owner, out _) && !grief.TryGet(staffId, out _),
            "Terminal load restored retired psychosocial state.");
        DungeonGameSaveData restoredWorld = saves.Capture();
        WriteNew("restored-before-duplicate-world.json", saves.ToJson(restoredWorld));
        string restoredLedger = restoredWorld.sections.Single(
            value => value.sectionId == GameplayOutcomeLedgerSaveSection.Id).payloadJson;
        GameplayOutcomeLedgerSaveData expectedLedger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(ledgerBefore);
        // Official ledger restore advances exactly the world epoch to
        // invalidate stale command tokens. Require every other field/row to
        // match, then compare duplicate commands within that same epoch.
        expectedLedger.worldEpoch = checked(expectedLedger.worldEpoch + 1L);
        Require(JsonUtility.ToJson(expectedLedger)
            == JsonUtility.ToJson(JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(restoredLedger)),
            "Terminal restore changed the ledger beyond its required epoch advance.");
        evidence.Add("ledgerRestore=exactAllFieldsAndRows;requiredWorldEpochAdvance=1");
        owner = manager.CurrentOwnerActor;
        health.Kill(owner, CharacterDeathCauseCode.Unknown, "qa:duplicate-owner-death");
        manager.HandleOwnerDeath(owner, "qa:duplicate-owner-death");
        DungeonGameSaveData afterDuplicate = saves.Capture();
        WriteNew("restored-terminal-world.json", saves.ToJson(afterDuplicate));
        evidence.Add("duplicateState=ownerDeathEvents:" + ownerDeaths + ";runResults:" + results
            + ";completedRuns:" + meta.State.CompletedRunCount + ";earned:" + meta.State.LifetimeEarnedCurrency);
        Require(ownerDeaths == 1 && results == 1 && meta.State.CompletedRunCount == completedBefore + 1
            && meta.State.LifetimeEarnedCurrency == earned
            && afterDuplicate.sections.Single(value => value.sectionId == GameplayOutcomeLedgerSaveSection.Id).payloadJson == restoredLedger,
            "Terminal restore or repeated death duplicated outcomes or run rewards.");
        evidence.Add("ownerDeath=PASS;events=1;runResults=1;deferredProductionAutosave=true;ownerIdentityExact=true;officialFreshDiskLoad=true;deadOwnerInert=true;outcomesExact=true;rewardDuplicate=0;sections=" + terminal.sections.Count);
        ExpectRejected(scope, saves, terminalJson, "ownerless", save =>
        {
            DungeonCharacterWorldSaveData payload = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(save, CharacterWorldSaveSection.Id);
            payload.actors.RemoveAll(value => value.isOwner);
            DungeonSaveSectionPayload.Write(save, CharacterWorldSaveSection.Id, CharacterWorldSaveSection.CurrentVersion, DungeonSaveRestorePhase.Characters, payload);
        }, "owner");
        ExpectRejected(scope, saves, terminalJson, "nonterminal-dead-owner", save =>
        {
            DungeonRunFlowSaveData payload = DungeonSaveSectionPayload.ReadOrNew<DungeonRunFlowSaveData>(save, RunFlowSaveSection.Id);
            payload.phase = DungeonRunPhase.Growth;
            payload.outcome = DungeonRunOutcome.None;
            DungeonSaveSectionEnvelope section = save.sections.Single(value => value.sectionId == RunFlowSaveSection.Id);
            section.payloadJson = JsonUtility.ToJson(payload);
        }, "ownership");
        ExpectRejected(scope, saves, terminalJson, "missing-owner-tombstone", save =>
        {
            KinshipHouseholdWorldSaveData payload = DungeonSaveSectionPayload.ReadOrNew<KinshipHouseholdWorldSaveData>(save, KinshipHouseholdSaveSection.Id);
            payload.kinship.tombstones.RemoveAll(value => value.characterId == CharacterId.Owner.Value);
            save.sections.Single(value => value.sectionId == KinshipHouseholdSaveSection.Id).payloadJson = JsonUtility.ToJson(payload);
        }, "tombstone");
        evidence.Add("negativeBoundaries=PASS;ownerless/nonterminal-dead-owner/missing-owner-tombstone;liveOwnerUnchanged=true");
    }

    private static void ExpectRejected(DungeonRuntimeLifetimeScope scope, IDungeonGameSaveService saves,
        string terminalJson, string label, Action<DungeonGameSaveData> corrupt, string error)
    {
        IOwnerRunManagerProvider owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
        Require(owners.TryGetManager(out OwnerRunManager manager), "Owner manager is unavailable.");
        CharacterActor before = manager.CurrentOwnerActor;
        DungeonGameSaveData invalid = saves.FromJson(terminalJson);
        corrupt(invalid);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        WriteNew(label + "-world.json", saves.ToJson(invalid));
        Require(!saves.TryRestore(invalid, out DungeonGameRestoreReport rejected)
            && rejected.Errors.Any(value => value.Contains(error, StringComparison.OrdinalIgnoreCase))
            && manager.CurrentOwnerActor == before && manager.IsRunEnded && before.IsDead,
            "Invalid " + label + " world was not rejected atomically: " + string.Join(" | ", rejected.Errors));
    }

    private static void WriteNew(string name, string value)
    {
        string path = Path.Combine(Path.GetDirectoryName(DungeonPersistencePaths.Root), name);
        using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        writer.Write(value);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
#endif
