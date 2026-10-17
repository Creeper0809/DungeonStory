#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class ExteriorActivityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Exterior activity scenarios failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity PlayMode Snapshot")]
    public static void RunPlayModeSnapshotFromMenu()
    {
        bool success = RunPlayModeRuntimeSnapshot(true);
        if (!success)
        {
            Debug.LogError("Exterior activity PlayMode snapshot failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Exterior/Run Informant Carry Current Save")]
    public static void RunInformantCarryCurrentSaveFromMenu()
    {
        RunInformantCarryCurrentSaveScenario(true);
    }

    public static bool RunInformantCarryCurrentSaveScenario(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario(
            "playmode Informant carry and current-save ownership",
            VerifyPlayModeIncidentAndSaveCapture,
            errors);
        if (errors.Count > 0)
        {
            Debug.LogError(
                "Informant carry current-save scenario failed: "
                + string.Join(", ", errors));
            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Informant carry current-save scenario passed.");
        }

        return true;
    }

    public static bool RunVisitorSpawnProfileRegression(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario(
            "playmode exterior visitor spawn profile registration",
            VerifyVisitorSpawnProfileRegistration,
            errors);
        if (errors.Count > 0)
        {
            Debug.LogError(
                "Exterior visitor spawn profile regression failed: "
                + string.Join(", ", errors));
            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior visitor spawn profile regression passed.");
        }

        return true;
    }

    public static IEnumerator VerifyInjuredReturneeSave(
        ICollection<string> warnings, ICollection<string> errors)
    {
        var evidence = new List<string>();
        bool passed = false;
        IEnumerator checks = RunInjuredReturneeSaveChecks(evidence);
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
        (checks as IDisposable)?.Dispose();
        evidence.Insert(0, "RESULT=" + (passed ? "PASS" : "FAIL"));
        evidence.Add("warningCount=" + warnings.Count + ";errorCount=" + errors.Count);
        evidence.AddRange(warnings.Select(value => "warning=" + value));
        evidence.AddRange(errors.Select(value => "error=" + value));
        evidence.Add("scope=controlled-real-rescue-request/full-world-save;naturalRecoveryOrDepartureClaimed=false;trainingEligible=false;humanApprovalClaimed=false;fullAuditClosure=false");
        WriteInjuredReturneeEvidence("injured-returnee-save-report.txt", string.Join("\n", evidence) + "\n");
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    private static IEnumerator RunInjuredReturneeSaveChecks(List<string> evidence)
    {
        void Require(bool condition, string failure)
        {
            if (!condition) throw new InvalidOperationException(failure);
        }
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Injured visitor save regression requires isolated batch persistence.");
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = FindScope();
            if (scope?.Container == null) { scope = null; yield return null; }
        }
        Require(scope?.Container != null, "Gameplay container did not become ready.");
        Require(CharacterProgressionDebugScenarios.VerifyWorldRestoreGenerationContract(
            out string generationContract), "World generation resume contract failed: " + generationContract);
        evidence.Add("worldGenerationResume=" + generationContract);
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
        string sourcePath = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        string expectedHash = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256");
        Require(!string.IsNullOrWhiteSpace(sourcePath) && !string.IsNullOrWhiteSpace(expectedHash),
            "A hashed official checkpoint with exterior zones is required.");
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        using (SHA256 sha = SHA256.Create())
            Require(string.Equals(BitConverter.ToString(sha.ComputeHash(sourceBytes)).Replace("-", ""),
                expectedHash, StringComparison.OrdinalIgnoreCase), "Checkpoint SHA-256 mismatch.");
        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        Require(saves.TryRestore(saves.FromJson(File.ReadAllText(sourcePath)), out DungeonGameRestoreReport sourceRestore),
            "Official source restore failed: " + DescribeRestoreErrors(sourceRestore));
        speed.SetPaused(true);
        ICharacterAiWorldRegistry world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        ICharacterPopulationService population = scope.Container.Resolve<ICharacterPopulationService>();
        ICharacterMedicalQuery medical = scope.Container.Resolve<ICharacterMedicalQuery>();
        ICharacterBodyHealthQuery body = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        IGriefTraumaService grief = scope.Container.Resolve<IGriefTraumaService>();
        IExteriorIncidentRuntime incidents = scope.Container.Resolve<IExteriorIncidentRuntime>();
        scope.Container.Resolve<IExperiencePacingRuntime>().AdvanceToDay(31);
        HashSet<string> beforeIds = incidents.IncidentStates.Select(value => value.incidentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(incidents.TryStartIncident(ExteriorIncidentKind.InjuredReturnee),
            "Actual injured-returnee rescue request was not admitted.");
        ExteriorIncidentRuntimeState request = incidents.IncidentStates.Single(value =>
            value.kind == ExteriorIncidentKind.InjuredReturnee && !beforeIds.Contains(value.incidentId));
        string actorId = request.actorIds.Single();
        CharacterActor actor = world.Characters.Single(value => value.Identity?.PersistentId == actorId);
        Require(request.IsTerminal && request.outcome == ExteriorIncidentOutcome.RescueOrdered
            && actor.Identity.CharacterType == CharacterType.Customer && body.GetSnapshot(actor).Downed
            && !CharacterWorkRoleUtility.IsWorker(actor) && !actor.TryGetAbility(out AbilityWork _),
            "Rescue request changed visitor identity, injury or worker capability.");
        Require(population.TryGetProfile(actor, out WorldCharacterProfile profile)
            && profile.persistentId == actorId && profile.isVisiting && !profile.isStaff
            && profile.settlementStanding == CharacterSettlementStanding.Visitor,
            "Terminal rescue request did not hand the exact actor to visiting population.");
        string orderId = medical.ActiveOrders.Single(value => value.IsActive && value.patientId == actorId).orderId;
        // Explicit disposable psychosocial setup, not a natural emotion example.
        grief.Require(new CharacterId(actorId));
        DungeonGameSaveData captured = saves.Capture();
        WriteInjuredReturneeEvidence("injured-returnee-active-world.json", saves.ToJson(captured));
        string medicalBefore = captured.sections.Single(value => value.sectionId == "combat.medical").payloadJson;
        string psychosocialBefore = captured.sections.Single(value => value.sectionId == "characters.psychosocial").payloadJson;
        Require(saves.TryRestore(captured, out DungeonGameRestoreReport restored),
            "Official injured-visitor world restore failed: " + DescribeRestoreErrors(restored));
        CharacterActor fresh = world.Characters.Single(value => value.Identity?.PersistentId == actorId);
        Require(fresh != actor && fresh.Identity.CharacterType == CharacterType.Customer
            && body.GetSnapshot(fresh).Downed && !fresh.TryGetAbility(out AbilityWork _)
            && !CharacterWorkRoleUtility.IsWorker(fresh)
            && population.TryGetProfile(fresh, out profile) && profile.isVisiting && !profile.isStaff
            && profile.persistentId == actorId
            && medical.ActiveOrders.Count(value => value.IsActive && value.patientId == actorId
                && value.orderId == orderId) == 1 && grief.TryGet(new CharacterId(actorId), out _),
            "Fresh restore lost exact visiting patient ownership or fabricated staff capability.");
        DungeonGameSaveData after = saves.Capture();
        Require(after.sections.Single(value => value.sectionId == "combat.medical").payloadJson == medicalBefore
            && after.sections.Single(value => value.sectionId == "characters.psychosocial").payloadJson == psychosocialBefore,
            "Official round-trip changed medical or psychosocial authority.");
        WriteInjuredReturneeEvidence("injured-returnee-restored-world.json", saves.ToJson(after));
        DungeonGameSaveData invalid = saves.FromJson(saves.ToJson(after));
        var charactersSection = invalid.sections.Single(value => value.sectionId == CharacterWorldSaveSection.Id);
        DungeonCharacterWorldSaveData characters = DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
            invalid, CharacterWorldSaveSection.Id);
        characters.actors.RemoveAll(value => value.persistentId == actorId);
        characters.populationProfiles.RemoveAll(value => value.persistentId == actorId);
        charactersSection.payloadJson = DungeonCharacterWorldSaveCodec.Serialize(characters);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        Require(!saves.TryRestore(invalid, out DungeonGameRestoreReport rejected)
            && rejected.Errors.Any(value => value.Contains("Medical order", StringComparison.Ordinal)
                && value.Contains(actorId, StringComparison.Ordinal))
            && rejected.Errors.Any(value => value.Contains("Psychosocial character", StringComparison.Ordinal)
                && value.Contains(actorId, StringComparison.Ordinal))
            && world.Characters.Single(value => value.Identity?.PersistentId == actorId) == fresh,
            "Missing patient world was not rejected before live-owner mutation.");
        evidence.Add("actualRescueRequest=PASS;terminalRequest=true;actor=" + actorId + ";medicalOrder=" + orderId);
        evidence.Add("populationHandoff=exactCanonicalVisitor;staff=false;authoredWorkBefore=false;authoredWorkAfter=false");
        evidence.Add("officialFreshRestore=PASS;medicalAndPsychosocialByteExact=true;duplicateActivePatientOrders=0;sections=" + after.sections.Count);
        evidence.Add("missingPatientNegative=PASS;medicalAndPsychosocialRefsRejected=true;liveActorUnchanged=true");
        Debug.Log("INJURED_RETURNEE_SAVE_HANDOFF_PASS: exact visiting patient retained through terminal request and official full-world restore.");
    }

    public static IEnumerator VerifyDepartureExposureSave(
        ICollection<string> warnings, ICollection<string> errors)
    {
        var evidence = new List<string>();
        bool passed = false;
        IEnumerator checks = RunDepartureExposureChecks(evidence);
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
        (checks as IDisposable)?.Dispose();
        evidence.Insert(0, "RESULT=" + (passed ? "PASS" : "FAIL"));
        evidence.Add("warningCount=" + warnings.Count + ";errorCount=" + errors.Count);
        evidence.AddRange(warnings.Select(value => "warning=" + value));
        evidence.AddRange(errors.Select(value => "error=" + value));
        evidence.Add("scope=controlled-real-exterior-spawn-departure/full-world-save;exposureSetup=explicit-owner-command;naturalPlayClaimed=false;trainingEligible=false;humanApprovalClaimed=false;fullAuditClosure=false");
        WriteInjuredReturneeEvidence("exterior-departure-exposure-report.txt", string.Join("\n", evidence) + "\n");
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    private static IEnumerator RunDepartureExposureChecks(List<string> evidence)
    {
        void Require(bool condition, string failure)
        {
            if (!condition) throw new InvalidOperationException(failure);
        }
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Departure regression requires isolated batch persistence.");
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = FindScope();
            if (scope?.Container == null) { scope = null; yield return null; }
        }
        Require(scope?.Container != null, "Gameplay container did not become ready.");
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
        string sourcePath = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        string expectedHash = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256");
        Require(!string.IsNullOrWhiteSpace(sourcePath) && !string.IsNullOrWhiteSpace(expectedHash),
            "An exact official checkpoint is required.");
        using (SHA256 sha = SHA256.Create())
            Require(string.Equals(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(sourcePath))).Replace("-", ""),
                expectedHash, StringComparison.OrdinalIgnoreCase), "Checkpoint SHA-256 mismatch.");
        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        Require(saves.TryRestore(saves.FromJson(File.ReadAllText(sourcePath)), out DungeonGameRestoreReport sourceRestore),
            "Official source restore failed: " + DescribeRestoreErrors(sourceRestore));
        speed.SetPaused(true);
        ICharacterAiWorldRegistry world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        IExteriorIncidentActorService actors = scope.Container.Resolve<IExteriorIncidentActorService>();
        IPopulationHealthService health = scope.Container.Resolve<IPopulationHealthService>();
        IPopulationHealthPersistence healthSave = scope.Container.Resolve<IPopulationHealthPersistence>();
        ICharacterLifeQuery life = scope.Container.Resolve<ICharacterLifeQuery>();
        IExteriorIncidentRuntime incidents = scope.Container.Resolve<IExteriorIncidentRuntime>();
        CharacterActor owner = world.Characters.Single(value => value != null && value.IsOwner);
        CharacterId ownerId = CharacterPersistentIdentity.Require(owner);
        HashSet<string> previousIncidents = incidents.IncidentStates.Select(value => value.incidentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(incidents.TryStartIncident(ExteriorIncidentKind.MerchantCart), "Real merchant incident request failed.");
        ExteriorIncidentRuntimeState merchant = incidents.IncidentStates.Single(value =>
            value.kind == ExteriorIncidentKind.MerchantCart && !previousIncidents.Contains(value.incidentId));
        string actorId = merchant.actorIds.Single();
        Require(actors.TryFind(actorId, out CharacterActor visitor), "Actual incident merchant was not published.");
        string diseaseId = scope.Container.Resolve<IDiseaseDefinitionCatalog>().Definitions
            .First(value => value.Contagious).Id;
        // Explicit isolated health setup, never a natural exposure/training example.
        health.RecordExposure(diseaseId, new[] {
            new PopulationExposureTarget(ownerId, 1f),
            new PopulationExposureTarget(new CharacterId(actorId), 1f) }, 0.25f, 1f);
        PopulationHealthWorldSaveData exposed = healthSave.Capture();
        Require(exposed.pendingExposures.Any(value => value.characterId == ownerId.Value && value.diseaseId == diseaseId)
            && exposed.pendingExposures.Count(value => value.characterId == actorId) == 1,
            "Both exact alive targets must have pending exposure before departure.");
        string exposedJson = JsonUtility.ToJson(exposed);
        // Disabling is not permanent departure; pending health must survive it and official owner replacement.
        visitor.gameObject.SetActive(false);
        Require(JsonUtility.ToJson(healthSave.Capture()) == exposedJson, "Disable erased pending health.");
        visitor.gameObject.SetActive(true);
        DungeonGameSaveData active = saves.Capture();
        WriteInjuredReturneeEvidence("exterior-exposure-active-world.json", saves.ToJson(active));
        Require(saves.TryRestore(saves.FromJson(saves.ToJson(active)), out DungeonGameRestoreReport activeRestore),
            "Alive exposure JSON restore failed: " + DescribeRestoreErrors(activeRestore));
        Require(actors.TryFind(actorId, out CharacterActor fresh) && fresh != visitor
            && JsonUtility.ToJson(healthSave.Capture()) == exposedJson,
            "Fresh owner publication erased or changed alive exposure.");
        Require(life.TryGet(new CharacterId(actorId), out CharacterLifeRecord retainedLife),
            "Merchant life history must be published before departure.");
        Require(!fresh.Stats.IsDead,
            "Merchant must depart alive, not through a death cleanup.");
        string retainedLifeJson = JsonUtility.ToJson(retainedLife.Capture());
        evidence.Add("activeFreshOwner=PASS;disablePreservesExposure=true;exactActor=" + actorId);
        exposed.pendingExposures.RemoveAll(value => value.characterId == actorId);
        string expectedAfter = JsonUtility.ToJson(exposed);
        // Existing controlled integration boundary advances the actual aggregate
        // and merchant timeout handler, not the calendar or a fabricated result.
        // This elapsed input is test-only and is never claimed as natural play.
        MethodInfo tickIncidents = typeof(ExteriorActivityRuntime).GetMethod(
            "TickIncidentStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(tickIncidents != null, "Owned incident tick integration boundary is missing.");
        merchant = incidents.IncidentStates.Single(value => value.incidentId == merchant.incidentId);
        tickIncidents.Invoke(scope.Container.Resolve<IExteriorActivityRuntime>(),
            new object[] { merchant.remainingSeconds + 1f });
        Require(incidents.IncidentStates.Single(value => value.incidentId == merchant.incidentId).stage
            == ExteriorIncidentStage.TimedOut, "Actual merchant timeout handler did not resolve the incident.");
        Require(!actors.TryFind(actorId, out _) && life.TryGet(new CharacterId(actorId), out retainedLife)
            && JsonUtility.ToJson(retainedLife.Capture()) == retainedLifeJson,
            "Actual departure failed or changed retained life history.");
        string afterDeparture = JsonUtility.ToJson(healthSave.Capture());
        // Capture immediately, before any day transition can expire the orphan and mask the defect.
        DungeonGameSaveData departed = saves.Capture();
        WriteInjuredReturneeEvidence("exterior-exposure-departed-world.json", saves.ToJson(departed));
        Require(afterDeparture == expectedAfter, "Departure must remove only this exact actor's pending exposure.");
        actors.Despawn(actorId);
        Require(JsonUtility.ToJson(healthSave.Capture()) == afterDeparture,
            "Duplicate departure changed retained health or durable exposure identity.");
        Require(saves.TryRestore(saves.FromJson(saves.ToJson(departed)), out DungeonGameRestoreReport departedRestore)
            && JsonUtility.ToJson(healthSave.Capture()) == afterDeparture && !actors.TryFind(actorId, out _),
            "Departed world fresh JSON restore failed: " + DescribeRestoreErrors(departedRestore));
        DungeonGameSaveData invalid = saves.FromJson(saves.ToJson(departed));
        var healthSection = invalid.sections.Single(value => value.sectionId == "population.epidemics");
        PopulationHealthWorldSaveData malformed = JsonUtility.FromJson<PopulationHealthWorldSaveData>(healthSection.payloadJson);
        malformed.pendingExposures.Add(new DiseaseExposureSaveData {
            characterId = actorId, diseaseId = diseaseId, weightedExposureHours = 0.25f, susceptibility = 1f });
        healthSection.payloadJson = JsonUtility.ToJson(malformed);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        Require(!saves.TryRestore(invalid, out DungeonGameRestoreReport rejected)
            && rejected.Errors.Any(value => value.Contains("Disease exposure", StringComparison.Ordinal)
                && value.Contains(actorId, StringComparison.Ordinal))
            && JsonUtility.ToJson(healthSave.Capture()) == afterDeparture && !actors.TryFind(actorId, out _),
            "Missing actor exposure was not rejected before live health mutation.");
        evidence.Add("actualAliveDeparture=PASS;immediateOfficialCapture=true;sections=" + departed.sections.Count);
        evidence.Add("exactHealthRetirement=PASS;otherCharacterExposureAndHealthByteExact=true;durableExposureRevisionPreserved=true;duplicateDepartureMutation=0");
        evidence.Add("departedFreshJsonRestore=PASS;missingActorExposureAtomicRejection=PASS;retainedLifeHistoryAlive=true");
        Debug.Log("EXTERIOR_DEPARTURE_EXPOSURE_SAVE_PASS: exact pending health retired before world identity removal; strict fresh JSON save retained.");
        VerifyActiveExteriorShopliftingOwner(scope, evidence);
    }

    private static void VerifyActiveExteriorShopliftingOwner(
        DungeonRuntimeLifetimeScope scope, List<string> evidence)
    {
        void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
        var incidents = scope.Container.Resolve<IExteriorIncidentRuntime>();
        var actors = scope.Container.Resolve<IExteriorIncidentActorService>();
        var campaign = scope.Container.Resolve<V20CampaignRuntime>();
        var diagnostics = scope.Container.Resolve<IV20ObservedMealIncidentDiagnostic>();
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var bus = scope.Container.Resolve<IGameEventBus>();
        var beforeIds = incidents.IncidentStates.Select(value => value.incidentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(incidents.TryStartIncident(ExteriorIncidentKind.Thief),
            "Actual active-exterior shoplifting fixture could not start a Thief incident.");
        var incident = incidents.IncidentStates.Single(value => !beforeIds.Contains(value.incidentId));
        string actorId = incident.actorIds.Single();
        bool visitorFound = actors.TryFind(actorId, out CharacterActor visitor);
        Require(!incident.IsTerminal && visitorFound
            && visitor.Identity.CharacterType == CharacterType.Customer,
            "Actual Thief was not an exclusively owned nonterminal exterior Customer.");
        var shop = UnityEngine.Object.FindObjectsByType<Shop>(FindObjectsSortMode.None)
            .Where(value => value != null && value.HasAvailableStock)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal).FirstOrDefault();
        Require(shop != null, "No existing authored retail stock for the real shoplifting producer.");
        var product = shop.ProductSnapshots.First(value => value.Quantity > 0);
        string facilityId = shop.PersistentInstanceId.Value;
        int quantityBefore = shop.CurrentStock;
        string societyBefore = JsonUtility.ToJson(campaign.CaptureSociety());
        long sequenceBefore = ReadOwnerBoundaryLedger(saves.Capture())
            .Select(value => (long)value["sequence"]).DefaultIfEmpty(0L).Max();
        var crimes = new List<FacilityCrimeEvent>();
        var random = scope.Container.Resolve<IRandomStreamProvider>().Get("shop-runtime");
        using (bus.Subscribe<FacilityCrimeEvent>(value => crimes.Add(value)))
        {
            try
            {
                // Explicit Editor-only risk input. The stock, physical sink,
                // canonical commit, event publisher and registered observer are real.
                shop.ConfigureCrimeForDiagnostics(new OwnerBoundaryCrimeInput(), random);
                Require(shop.TryResolveCheckoutCrime(visitor.BuildingVisitor,
                    new[] { new RemainStock(product.Id, product.Name, product.Price, 1,
                        Array.Empty<OnBuyItemSO>()) }), "Actual shoplifting commit failed.");
            }
            finally
            {
                shop.ConfigureCrimeForDiagnostics(scope.Container.Resolve<IFacilityCrimeRiskEvaluator>(), random);
            }
        }
        Require(crimes.Count == 1 && crimes[0].actor == visitor && crimes[0].facility == shop
            && crimes[0].committedLot?.quantity == 1 && shop.CurrentStock == quantityBefore - 1,
            "Real shoplifting did not consume exactly one retail unit and publish one exact callback.");
        FacilityCrimeEvent committed = crimes[0];
        ObservedMealIncidentCaptureResult refusal = diagnostics.LastObservedShopliftingIncidentResult;
        bool ExactRefusal() => !diagnostics.LastObservedShopliftingIncidentCallbackFailure.IsFailure
            && diagnostics.LastObservedShopliftingIncidentResult.Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && diagnostics.LastObservedShopliftingIncidentResult.IncidentKind == ServiceIncidentKind.Theft
            && diagnostics.LastObservedShopliftingIncidentResult.Failure.Parameters.Length == 2
            && diagnostics.LastObservedShopliftingIncidentResult.Failure.Parameters[1]
                == "observed-service-incident-target-owned-by-active-exterior";
        WriteInjuredReturneeEvidence("active-exterior-shoplifting-callback.json", new JObject
        {
            ["incidentId"] = incident.incidentId, ["stageDuringCallback"] = (int)incident.stage,
            ["actorId"] = actorId, ["facilityId"] = facilityId,
            ["commitOperationId"] = committed.commitOperationId,
            ["committedLot"] = JObject.FromObject(committed.committedLot),
            ["quantityBefore"] = quantityBefore, ["quantityAfter"] = shop.CurrentStock,
            ["disposition"] = refusal.Disposition.ToString(), ["incidentKind"] = refusal.IncidentKind.ToString(),
            ["failureParameters"] = new JArray(refusal.Failure.Parameters.ToArray()),
            ["callbackFailure"] = diagnostics.LastObservedShopliftingIncidentCallbackFailure.ToString(),
            ["exactRefusal"] = ExactRefusal(), ["crimeRiskInputControlled"] = true,
            ["naturalPlayClaimed"] = false, ["trainingEligible"] = false
        }.ToString());
        Require(ExactRefusal() && !incidents.IncidentStates.Single(value => value.incidentId == incident.incidentId).IsTerminal
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyBefore
            && !campaign.GetOwnedCustomerIds().Any(value => value.Value == actorId),
            "Registered shoplifting callback acquired a second Society owner or failed its explicit refusal.");
        DungeonGameSaveData captured = saves.Capture();
        var outcomes = ReadOwnerBoundaryLedger(captured).Where(value => (long)value["sequence"] > sequenceBefore
            && (string)value["outcomeTypeId"] == "service.facility-crime").ToArray();
        Require(outcomes.Length == 1 && (int)outcomes[0]["lifecycle"] == 6
            && outcomes[0]["facts"].Any(value => (string)value["factId"] == "summary"
                && ((string)value["value"]).Contains("operation=" + committed.commitOperationId, StringComparison.Ordinal)
                && ((string)value["value"]).Contains("actor=" + actorId, StringComparison.Ordinal)),
            "Exact canonical shoplifting result did not survive the ownership refusal.");
        WriteInjuredReturneeEvidence("active-exterior-shoplifting-world.json", saves.ToJson(captured));
        bool restoredOk = saves.TryRestore(saves.FromJson(saves.ToJson(captured)), out DungeonGameRestoreReport restored);
        bool freshFound = actors.TryFind(actorId, out CharacterActor fresh);
        Require(restoredOk && freshFound && fresh != visitor
            && !incidents.IncidentStates.Single(value => value.incidentId == incident.incidentId).IsTerminal,
            "Active-exterior shoplifting fresh JSON restore failed: " + DescribeRestoreErrors(restored));
        Shop freshShop = UnityEngine.Object.FindObjectsByType<Shop>(FindObjectsSortMode.None)
            .Single(value => value.PersistentInstanceId.Value == facilityId);
        string ledgerAfterRestore = ReadOwnerBoundaryLedger(saves.Capture()).ToString();
        for (int attempt = 0; attempt < 2; attempt++)
            bus.Publish(new FacilityCrimeEvent(fresh.BuildingVisitor, freshShop, committed.kind,
                committed.detail, committed.lossValue, committed.committedLot, committed.commitOperationId));
        Require(ExactRefusal() && freshShop.CurrentStock == quantityBefore - 1
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyBefore
            && ReadOwnerBoundaryLedger(saves.Capture()).ToString() == ledgerAfterRestore,
            "Restored committed crime redelivery changed physical stock, canonical ledger or Society ownership.");
        WriteInjuredReturneeEvidence("active-exterior-shoplifting-restored-world.json", saves.ToJson(saves.Capture()));
        WriteInjuredReturneeEvidence("active-exterior-shoplifting-result.json", new JObject
        {
            ["passed"] = true, ["actorId"] = actorId, ["incidentId"] = incident.incidentId,
            ["canonicalIdentity"] = outcomes[0].DeepClone(), ["freshActorReplaced"] = true,
            ["officialSections"] = captured.sections.Count, ["actualCallbackCount"] = 1,
            ["restoredRedeliveries"] = 2, ["secondSocietyOwnerCount"] = 0,
            ["physicalConsumedQuantity"] = 1, ["strictSaveRestoreGuardsChanged"] = false,
            ["crimeRiskInputControlled"] = true, ["trainingEligible"] = false,
            ["naturalPlayClaimed"] = false, ["allObservedCallbackTypesClaimed"] = false
        }.ToString());
        evidence.Add("activeExteriorShoplifting=PASS;actualStockSinkAndCanonicalCommit=1;registeredCallbackRefusal=exact;freshWorldRestore=PASS;redeliveryMutation=0;actor=" + actorId);
    }

    private static JArray ReadOwnerBoundaryLedger(DungeonGameSaveData save) =>
        (JArray)JObject.Parse(save.sections.Single(value => value.sectionId == "world.gameplay-outcome-ledger").payloadJson)["exactOutcomes"];

    private sealed class OwnerBoundaryCrimeInput : IFacilityCrimeRiskEvaluator
    {
        public float CalculateShopliftingChance(FacilityCrimeRiskContext context) => 1f;
        public float CalculateOperationalRisk(FacilityCrimeRiskContext context) => 1f;
        public bool ShouldTriggerCrime(float chance, float roll) => true;
    }

    private static void WriteInjuredReturneeEvidence(string name, string value)
    {
        string path = Path.Combine(Path.GetDirectoryName(DungeonPersistencePaths.Root), name);
        using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        writer.Write(value);
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("save V19 contains exterior snapshot", VerifySaveV19ContainsExteriorSnapshot, errors);
        RunScenario("reception work type is registered", VerifyReceptionWorkTypeIsRegistered, errors);
        RunScenario("exterior runtime state is not a ScriptableObject", VerifyRuntimeStateIsNotScriptableObject, errors);
        RunScenario("exterior ability contracts expose expected work types", VerifyExteriorAbilityContracts, errors);
        RunScenario(
            "incident Aggregate owns handler time and stage transitions",
            VerifyIncidentAggregateAuthorityAgreement,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity scenarios passed.");
        }

        return true;
    }

    public static bool RunPlayModeRuntimeSnapshot(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("playmode runtime services and zones", VerifyPlayModeRuntimeServicesAndZones, errors);
        RunScenario("playmode reception work candidate", VerifyPlayModeReceptionWorkCandidate, errors);
        RunScenario(
            "playmode Informant carry and current-save ownership",
            VerifyPlayModeIncidentAndSaveCapture,
            errors);
        RunScenario(
            "playmode incident handler transition agrees with query capture restore",
            VerifyIncidentHandlerTransitionAgreement,
            errors);
        RunScenario(
            "playmode invalid exterior preflight preserves live zones",
            VerifyInvalidExteriorPreflightPreservesLiveZones,
            errors);
        RunScenario(
            "playmode exterior roundtrip publishes detached zones",
            VerifyExteriorRoundtripPublishesDetachedZones,
            errors);
        RunScenario(
            "playmode later failure discards exterior candidate",
            VerifyLaterFailureDiscardsExteriorCandidate,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity PlayMode snapshot passed.");
        }

        return true;
    }

    private static bool VerifySaveV19ContainsExteriorSnapshot()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonExteriorActivitySaveData());
        DungeonExteriorActivitySaveData exterior =
            DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                save,
                ExteriorActivitySaveSection.Id);
        return DungeonGameSaveData.CurrentVersion == 24
            && save.version == DungeonGameSaveData.CurrentVersion
            && exterior.version == DungeonExteriorActivitySaveData.CurrentVersion;
    }

    private static bool VerifyReceptionWorkTypeIsRegistered()
    {
        bool resolved = WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Reception, out WorkTypeDefinition task);
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        return resolved
            && task != null
            && task.WorkTypeId == BuiltInWorkTypeIds.Reception
            && priorities.GetPriority(BuiltInWorkTypeIds.Reception) == WorkPriorityLevel.Priority2;
    }

    private static bool VerifyRuntimeStateIsNotScriptableObject()
    {
        return !typeof(ExteriorActivityRuntime).IsSubclassOf(typeof(ScriptableObject))
            && !typeof(ExteriorZoneMarker).IsSubclassOf(typeof(ScriptableObject))
            && typeof(MonoBehaviour).IsAssignableFrom(typeof(ExteriorZoneMarker));
    }

    private static bool VerifyExteriorAbilityContracts()
    {
        IBuildingExteriorWorkRuntimeAbility reception = BuildingAbilityRuntimeCatalog.CreateBuiltIns().Bind(new BuildingAbility[] { new BuildingReceptionAbility() }).OfType<IBuildingExteriorWorkRuntimeAbility>().Single();
        IBuildingExteriorWorkRuntimeAbility patrol = BuildingAbilityRuntimeCatalog.CreateBuiltIns().Bind(new BuildingAbility[] { new BuildingPatrolPostAbility() }).OfType<IBuildingExteriorWorkRuntimeAbility>().Single();
        IBuildingExteriorWorkRuntimeAbility rest = BuildingAbilityRuntimeCatalog.CreateBuiltIns().Bind(new BuildingAbility[] { new BuildingOutdoorRestAbility() }).OfType<IBuildingExteriorWorkRuntimeAbility>().Single();
        IBuildingExteriorWorkRuntimeAbility maintenance = BuildingAbilityRuntimeCatalog.CreateBuiltIns().Bind(new BuildingAbility[] { new BuildingExteriorMaintenanceAbility() }).OfType<IBuildingExteriorWorkRuntimeAbility>().Single();
        return reception.SupportsExteriorWork(BuiltInWorkTypeIds.Reception)
            && patrol.SupportsExteriorWork(BuiltInWorkTypeIds.Guard)
            && rest.SupportsExteriorWork(BuiltInWorkTypeIds.Rest)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Clean)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Repair)
            && !maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Reception);
    }

    private static bool VerifyIncidentAggregateAuthorityAgreement()
    {
        ExteriorIncidentRuntimeState state = new ExteriorIncidentRuntimeState
        {
            incidentId = "incident:Thief:1",
            kind = ExteriorIncidentKind.Thief,
            zoneId = "exterior:DropZone:1:1",
            stage = ExteriorIncidentStage.Active,
            durationSeconds = 10f,
            remainingSeconds = 10f
        };
        DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> aggregate = CreateIncidentAggregate();
        aggregate.Add(state);
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> transition = aggregate.Tick(
                2f,
                (current, _) =>
                {
                    current.remainingSeconds = 5f;
                    current.stage = ExteriorIncidentStage.Interacting;
                }).Single();

        ExteriorIncidentRuntimeState queried = aggregate.States.Single();
        DungeonExteriorActivitySaveData captured =
            new DungeonExteriorActivitySaveData
            {
                incidentStates = aggregate.States
                    .Select(current => current.Clone())
                    .ToList()
            };
        DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> restoredAggregate =
            CreateIncidentAggregate();
        restoredAggregate.ReplaceAll(
            captured.incidentStates.Select(current => current.Clone()));
        ExteriorIncidentRuntimeState restored =
            restoredAggregate.States.Single();
        bool restoredMatchesCapturedState =
            restored.stage == queried.stage
            && Mathf.Approximately(
                restored.remainingSeconds,
                queried.remainingSeconds);
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> resolved = restoredAggregate.Mutate(
                restored,
                current =>
                {
                    current.remainingSeconds = 0f;
                    current.stage = ExteriorIncidentStage.Resolved;
                });

        return Mathf.Approximately(transition.RemainingSeconds, 5f)
            && queried.stage == ExteriorIncidentStage.Interacting
            && Mathf.Approximately(queried.remainingSeconds, 5f)
            && restoredMatchesCapturedState
            && resolved.IsTerminal
            && restoredAggregate.ActiveCount == 0
            && typeof(ExteriorZoneMarker).GetMethod(
                "TickIncident",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic) == null;
    }

    private static DungeonStory.Exterior.ExteriorIncidentAggregate<
        ExteriorIncidentRuntimeState> CreateIncidentAggregate()
    {
        return new DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState>(
                current => current.IsTerminal,
                current => current.remainingSeconds,
                (current, remainingSeconds) =>
                    current.remainingSeconds = remainingSeconds);
    }

    private static bool VerifyPlayModeRuntimeServicesAndZones()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery query = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorActivityOverviewSnapshot overview = query.GetOverview();
        HashSet<ExteriorZoneType> types = query.Zones.Select(zone => zone.ZoneType).ToHashSet();
        return overview.ZoneCount >= 7
            && types.Contains(ExteriorZoneType.DropZone)
            && types.Contains(ExteriorZoneType.ReceptionPoint)
            && types.Contains(ExteriorZoneType.GuardPost)
            && types.Contains(ExteriorZoneType.PatrolPoint)
            && types.Contains(ExteriorZoneType.OutdoorRestSpot)
            && types.Contains(ExteriorZoneType.ExpeditionStaging)
            && types.Contains(ExteriorZoneType.IncidentPoint);
    }

    private static bool VerifyVisitorSpawnProfileRegistration()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Exterior visitor spawn profile regression requires Play Mode.");
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException(
                "Dungeon runtime scope is unavailable for exterior visitor spawn verification.");
        }

        IExteriorIncidentActorService service =
            scope.Container.Resolve<IExteriorIncidentActorService>();
        ICharacterAiWorldRegistry world =
            scope.Container.Resolve<ICharacterAiWorldRegistry>();
        ICharacterLifeQuery life = scope.Container.Resolve<ICharacterLifeQuery>();
        ICharacterNarrativeQuery narratives =
            scope.Container.Resolve<ICharacterNarrativeQuery>();
        ICharacterNarrativeCatalog narrativeCatalog =
            scope.Container.Resolve<ICharacterNarrativeCatalog>();
        IGriefTraumaService grief = scope.Container.Resolve<IGriefTraumaService>();
        IPsychosocialPersistence psychosocial =
            scope.Container.Resolve<IPsychosocialPersistence>();
        string retainedPsychosocialBefore = JsonUtility.ToJson(psychosocial.Capture());
        IRunCharacterCatalog characterCatalog =
            scope.Container.Resolve<IRunCharacterCatalog>();

        CharacterArchetypeId expectedArchetypeId =
            new CharacterArchetypeId("character-archetype:9010");
        CharacterSO expectedArchetype = characterCatalog.Characters
            .Single(value => value != null
                && value.DefinitionId.Equals(expectedArchetypeId));
        CharacterSpeciesId expectedSpeciesId = new CharacterSpeciesId("Human");
        SpeciesCultureId expectedCultureId = new SpeciesCultureId(
            narrativeCatalog.RequireDefaultCulture(expectedSpeciesId.Value).StableId);
        CharacterActor owner = world.Characters.Single(value =>
            value != null && value.IsOwner);
        Vector2Int spawnPosition = owner.GetNowXY();
        ExteriorIncidentKind[] kinds =
        {
            ExteriorIncidentKind.MerchantCart,
            ExteriorIncidentKind.Informant,
            ExteriorIncidentKind.Thief,
            ExteriorIncidentKind.InjuredReturnee
        };
        string regressionId = Guid.NewGuid().ToString("N");
        List<string> createdActorIds = new List<string>(kinds.Length);
        try
        {
            foreach (ExteriorIncidentKind kind in kinds)
            {
                CharacterId expectedCharacterId = CharacterId.FromStableSuffix(
                    $"qa:exterior-visitor-profile:{kind.ToString().ToLowerInvariant()}:{regressionId}");
                createdActorIds.Add(expectedCharacterId.Value);
                bool spawned = service.TrySpawn(
                    kind,
                    expectedCharacterId.Value,
                    spawnPosition,
                    downed: false,
                    out CharacterActor actor,
                    out string failureReason);
                CharacterType expectedType = CharacterType.Customer;
                bool hasLife = life.TryGet(
                    expectedCharacterId,
                    out CharacterLifeRecord lifeRecord);
                bool hasNarrative = narratives.TryGet(
                    expectedCharacterId,
                    out CharacterNarrativeSnapshot narrative);
                bool exactProfile = actor?.Identity?.Profile != null
                    && actor.Identity.Profile.CharacterArchetypeId.Equals(
                        expectedArchetype.DefinitionId);
                bool exactCharacterId = actor != null
                    && CharacterPersistentIdentity.Require(actor).Equals(
                        expectedCharacterId);
                bool exactSpecies = actor != null
                    && string.Equals(
                        actor.SpeciesTag,
                        expectedSpeciesId.Value,
                        StringComparison.Ordinal)
                    && hasLife
                    && lifeRecord.PhenotypeSpeciesId.Equals(expectedSpeciesId);
                bool exactType = actor?.Identity != null
                    && actor.Identity.CharacterType == expectedType
                    && actor.characterType == expectedType;
                bool exactCulture = hasNarrative
                    && narrative.CultureId.Equals(expectedCultureId);
                if (!spawned
                    || actor == null
                    || !exactProfile
                    || !exactCharacterId
                    || !exactSpecies
                    || !exactType
                    || !hasLife
                    || !hasNarrative
                    || !exactCulture)
                {
                    throw new InvalidOperationException(
                        $"Exterior visitor profile mismatch for {kind}. "
                        + $"spawned={spawned}, failure={failureReason}, actor={actor != null}, "
                        + $"profile={exactProfile}, characterId={exactCharacterId}, "
                        + $"species={exactSpecies}, type={exactType}, life={hasLife}, "
                        + $"narrative={hasNarrative}, culture={exactCulture}.");
                }
                // Explicit regression setup, not a naturally generated
                // emotion/training example: give this disposable actor a
                // psychosocial record so its real departure must retire it.
                grief.Require(expectedCharacterId);
                if (!grief.TryGet(expectedCharacterId, out _))
                    throw new InvalidOperationException("Visitor psychosocial setup did not publish.");
            }
        }
        finally
        {
            for (int index = createdActorIds.Count - 1; index >= 0; index--)
            {
                service.Despawn(createdActorIds[index]);
                if (grief.TryGet(new CharacterId(createdActorIds[index]), out _))
                    throw new InvalidOperationException(
                        "Departed incident actor retained active psychosocial ownership.");
                service.Despawn(createdActorIds[index]);
            }
        }

        if (!string.Equals(retainedPsychosocialBefore,
                JsonUtility.ToJson(psychosocial.Capture()), StringComparison.Ordinal))
            throw new InvalidOperationException("Incident departure changed retained psychosocial records.");
        return true;
    }

    private static bool VerifyPlayModeIncidentAndSaveCapture()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "[initialized] Informant carry current-save scenario requires Play Mode.");
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException(
                "[initialized] Dungeon runtime scope is unavailable.");
        }

        ExteriorActivityRuntime exteriorRuntime =
            scope.Container.Resolve<ExteriorActivityRuntime>();
        IExteriorIncidentRuntime incidentRuntime = exteriorRuntime;
        IExperiencePacingRuntime pacing = scope.Container.Resolve<IExperiencePacingRuntime>();
        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        ICharacterAiWorldRegistry world =
            scope.Container.Resolve<ICharacterAiWorldRegistry>();
        ICharacterProficiencyQuery proficiencies =
            scope.Container.Resolve<ICharacterProficiencyQuery>();
        ICharacterLifeQuery life = scope.Container.Resolve<ICharacterLifeQuery>();
        ICharacterNarrativeQuery narratives =
            scope.Container.Resolve<ICharacterNarrativeQuery>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        DungeonGameSaveData baseline = saveService.Capture();
        HashSet<string> previousIncidentIds = incidentRuntime.IncidentStates
            .Where(state => state != null)
            .Select(state => state.incidentId)
            .ToHashSet(StringComparer.Ordinal);
        bool baselineRestored = false;
        string baselineRestoreErrors = "<restore not attempted>";
        try
        {
            pacing.AdvanceToDay(Math.Max(31, pacing.CurrentDay));
            bool started = incidentRuntime.TryStartIncident(
                ExteriorIncidentKind.Informant,
                "정보상이 변경 교역권의 소식을 들고 왔습니다.");
            ExteriorIncidentRuntimeState startedState = incidentRuntime
                .IncidentStates
                .SingleOrDefault(state => state != null
                    && state.kind == ExteriorIncidentKind.Informant
                    && !previousIncidentIds.Contains(state.incidentId));
            string actorId = startedState?.actorIds?.SingleOrDefault();
            CharacterId characterId = new(actorId);
            CharacterActor actor = world.Characters.SingleOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.Identity?.PersistentId,
                    actorId,
                    StringComparison.Ordinal));
            bool hasLife = characterId.IsValid && life.TryGet(characterId, out _);
            CharacterNarrativeSnapshot narrative = null;
            bool hasNarrative = characterId.IsValid
                && narratives.TryGet(characterId, out narrative);
            bool hasCanonicalProficiencies = hasNarrative
                && narrative.Proficiencies.Count == 9;
            bool hasFieldwork = characterId.IsValid
                && proficiencies.TryGetProficiency(
                    characterId,
                    BuiltInCharacterProficiencyIds.Fieldwork,
                    calendar.AbsoluteHour,
                    out _);
            if (!started
                || startedState == null
                || !characterId.IsValid
                || actor == null
                || actor.Identity.CharacterType != CharacterType.Customer
                || !hasLife
                || !hasCanonicalProficiencies
                || !hasFieldwork)
            {
                throw new InvalidOperationException(
                    "[initialized] Informant actor did not complete canonical incident identity, "
                    + "Customer type, life, narrative, and fieldwork initialization. "
                    + $"started={started}, state={startedState != null}, id={characterId.IsValid}, "
                    + $"actor={actor != null}, life={hasLife}, narrative={hasNarrative}, "
                    + $"proficiencies={hasCanonicalProficiencies}, fieldwork={hasFieldwork}.");
            }

            float carryCapacity = actor.CarryInventory.GetBaseCarryLimit();
            if (!(carryCapacity > 0f)
                || float.IsNaN(carryCapacity)
                || float.IsInfinity(carryCapacity))
            {
                throw new InvalidOperationException(
                    $"[initialized] Informant carry query returned invalid capacity {carryCapacity}.");
            }

            bool hadAbilityWork = actor.GetAbility<AbilityWork>() != null;
            AIActionSet[] initialActionCatalogue = actor.Brain?.availableActions?
                .Select(action => action?.actionset)
                .ToArray();
            if (initialActionCatalogue == null
                || initialActionCatalogue.Length == 0
                || initialActionCatalogue.Any(action => action == null))
            {
                throw new InvalidOperationException(
                    "[initialized] Informant visitor action catalogue is missing or invalid.");
            }

            DungeonExteriorActivitySaveData exterior = exteriorRuntime.Capture();
            DungeonGameSaveData save = saveService.Capture();
            DungeonExteriorActivitySaveData savedExterior =
                DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                    save,
                    ExteriorActivitySaveSection.Id);
            DungeonCharacterWorldSaveData savedCharacters =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                    save,
                    CharacterWorldSaveSection.Id);
            CharacterLifeWorldSaveData savedLife =
                DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(
                    save,
                    CharacterLifeSaveSection.Id);
            CharacterNarrativeWorldSaveData savedNarrative =
                DungeonSaveSectionPayload.ReadOrNew<CharacterNarrativeWorldSaveData>(
                    save,
                    CharacterNarrativeSaveSection.Id);
            bool savedActor = savedCharacters.actors.Any(value => value != null
                    && string.Equals(
                        value.persistentId,
                        actorId,
                        StringComparison.Ordinal)
                    && value.characterType == CharacterType.Customer);
            bool savedActorLife = savedLife.characters.Any(value => value != null
                    && string.Equals(
                        value.characterId,
                        actorId,
                        StringComparison.Ordinal));
            bool savedActorNarrative = savedNarrative.characters.Any(value => value != null
                    && string.Equals(
                        value.characterId,
                        actorId,
                        StringComparison.Ordinal));
            bool activeJoinCaptured = exterior.zones.Count >= 7
                && exterior.incidentStates.Count >= 1
                && save.version == DungeonGameSaveData.CurrentVersion
                && savedExterior.zones.Count >= 7
                && savedExterior.incidentStates.Count >= 1
                && savedActor
                && savedActorLife
                && savedActorNarrative;
            if (!activeJoinCaptured)
            {
                throw new InvalidOperationException(
                    "[activeJoin] Active Informant ownership did not join exterior, character, "
                    + "life, and narrative current-save sections. "
                    + $"actor={savedActor}, life={savedActorLife}, "
                    + $"narrative={savedActorNarrative}, saveVersion={save.version}.");
            }

            bool currentRestoreAttempted = saveService.TryRestore(
                save,
                out DungeonGameRestoreReport currentRestoreReport);
            if (!currentRestoreAttempted || currentRestoreReport?.Success != true)
            {
                throw new InvalidOperationException(
                    "[restore] Active Informant current-save restore failed. errors="
                    + DescribeRestoreErrors(currentRestoreReport));
            }

            ExteriorIncidentRuntimeState restoredState = incidentRuntime
                .IncidentStates
                .SingleOrDefault(state => state != null
                    && string.Equals(
                        state.incidentId,
                        startedState?.incidentId,
                        StringComparison.Ordinal));
            CharacterActor restoredActor = world.Characters.SingleOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.Identity?.PersistentId,
                    actorId,
                    StringComparison.Ordinal));
            bool restoredFieldwork = proficiencies.TryGetProficiency(
                characterId,
                BuiltInCharacterProficiencyIds.Fieldwork,
                calendar.AbsoluteHour,
                out _);
            float restoredCarryCapacity = restoredActor != null
                ? restoredActor.CarryInventory.GetBaseCarryLimit()
                : 0f;
            bool restoredHasAbilityWork = restoredActor != null
                && restoredActor.GetAbility<AbilityWork>() != null;
            AIActionSet[] restoredActionCatalogue = restoredActor?.Brain?.availableActions?
                .Select(action => action?.actionset)
                .ToArray();
            bool actionCataloguePreserved = restoredActionCatalogue != null
                && restoredActionCatalogue.Length == initialActionCatalogue.Length
                && restoredActionCatalogue
                    .Select((action, index) => ReferenceEquals(
                        action,
                        initialActionCatalogue[index]))
                    .All(matches => matches);
            bool restoredCarryReady = restoredState != null
                && !restoredState.IsTerminal
                && restoredActor != null
                && restoredActor.Identity.CharacterType == CharacterType.Customer
                && restoredFieldwork
                && restoredHasAbilityWork == hadAbilityWork
                && actionCataloguePreserved
                && restoredCarryCapacity > 0f
                && !float.IsNaN(restoredCarryCapacity)
                && !float.IsInfinity(restoredCarryCapacity)
                && restoredState.remainingSeconds > 0f
                && !float.IsNaN(restoredState.remainingSeconds)
                && !float.IsInfinity(restoredState.remainingSeconds);
            if (!restoredCarryReady)
            {
                throw new InvalidOperationException(
                    "[afterRestoreCarry] Restored Informant did not retain active ownership, "
                    + "Customer identity, fieldwork proficiency, carry capability, or timeout authority. "
                    + $"state={restoredState != null}, actor={restoredActor != null}, "
                    + $"fieldwork={restoredFieldwork}, workBefore={hadAbilityWork}, "
                    + $"workAfter={restoredHasAbilityWork}, "
                    + $"actionsPreserved={actionCataloguePreserved}, carry={restoredCarryCapacity}, "
                    + $"remaining={restoredState?.remainingSeconds}.");
            }

            MethodInfo tickIncidents = typeof(ExteriorActivityRuntime).GetMethod(
                "TickIncidentStates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (tickIncidents == null)
            {
                throw new InvalidOperationException(
                    "[terminal] Existing TickIncidentStates integration boundary was not found.");
            }

            // Focused integration tick across the restored timeout boundary; this is not a
            // measurement of naturally elapsed incident time.
            float timeoutTickSeconds = restoredState.remainingSeconds + 0.01f;
            tickIncidents.Invoke(exteriorRuntime, new object[] { timeoutTickSeconds });

            ExteriorIncidentRuntimeState terminalState = incidentRuntime
                .IncidentStates
                .SingleOrDefault(state => state != null
                    && string.Equals(
                        state.incidentId,
                        startedState?.incidentId,
                        StringComparison.Ordinal));
            bool terminalActorRemoved = terminalState?.IsTerminal == true
                && world.Characters.All(candidate => candidate == null
                    || !string.Equals(
                        candidate.Identity?.PersistentId,
                        actorId,
                        StringComparison.Ordinal));
            if (!terminalActorRemoved)
            {
                throw new InvalidOperationException(
                    "[terminal] Timeout integration tick did not terminalize the Informant and "
                    + $"remove its live actor. terminal={terminalState?.IsTerminal == true}.");
            }

            DungeonGameSaveData terminalSave = saveService.Capture();
            bool terminalLifeRemoved = !life.TryGet(characterId, out _);
            bool terminalNarrativeRetained = narratives.TryGet(characterId, out _);
            if (!terminalLifeRemoved || !terminalNarrativeRetained)
            {
                throw new InvalidOperationException(
                    "[cleanup] Terminal Informant did not prune life while retaining durable "
                    + $"narrative history. lifeRemoved={terminalLifeRemoved}, "
                    + $"narrativeRetained={terminalNarrativeRetained}.");
            }

            bool terminalRestoreAttempted = saveService.TryRestore(
                terminalSave,
                out DungeonGameRestoreReport terminalRestoreReport);
            if (!terminalRestoreAttempted || terminalRestoreReport?.Success != true)
            {
                throw new InvalidOperationException(
                    "[cleanup] Terminal Informant current-save restore failed. errors="
                    + DescribeRestoreErrors(terminalRestoreReport));
            }

            ExteriorIncidentRuntimeState restoredTerminalState = incidentRuntime
                .IncidentStates
                .SingleOrDefault(state => state != null
                    && string.Equals(
                        state.incidentId,
                        startedState.incidentId,
                        StringComparison.Ordinal));
            bool terminalJoinRestored = restoredTerminalState?.IsTerminal == true
                && world.Characters.All(candidate => candidate == null
                    || !string.Equals(
                        candidate.Identity?.PersistentId,
                        actorId,
                        StringComparison.Ordinal))
                && !life.TryGet(characterId, out _)
                && narratives.TryGet(characterId, out _);
            if (!terminalJoinRestored)
            {
                throw new InvalidOperationException(
                    "[cleanup] Restored terminal Informant retained live actor/life authority or "
                    + "lost durable narrative history.");
            }
        }
        finally
        {
            try
            {
                bool baselineRestoreAttempted = saveService.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport baselineRestoreReport);
                baselineRestored = baselineRestoreAttempted
                    && baselineRestoreReport?.Success == true;
                baselineRestoreErrors = DescribeRestoreErrors(baselineRestoreReport);
            }
            catch (Exception exception)
            {
                baselineRestoreErrors = exception.ToString();
                Debug.LogException(exception);
            }

            if (!baselineRestored)
            {
                Debug.LogError(
                    "[cleanup] Informant carry current-save baseline restore failed. errors="
                    + baselineRestoreErrors);
            }
        }

        if (!baselineRestored)
        {
            throw new InvalidOperationException(
                "[cleanup] Informant carry current-save baseline restore failed. errors="
                + baselineRestoreErrors);
        }

        return true;
    }

    private static string DescribeRestoreErrors(DungeonGameRestoreReport report)
    {
        return report == null
            ? "<missing report>"
            : report.Errors.Count == 0
                ? "<none>"
                : string.Join(" | ", report.Errors);
    }

    private static bool VerifyIncidentHandlerTransitionAgreement()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope?.Container == null)
        {
            return false;
        }

        ExteriorActivityRuntime runtime =
            scope.Container.Resolve<ExteriorActivityRuntime>();
        ExteriorIncidentHandlerRegistry registry =
            scope.Container.Resolve<ExteriorIncidentHandlerRegistry>();
        IDungeonGameSaveService saveService =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IExperiencePacingRuntime pacing =
            scope.Container.Resolve<IExperiencePacingRuntime>();
        FieldInfo handlersField = typeof(ExteriorIncidentHandlerRegistry)
            .GetField(
                "handlers",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler> handlers =
            handlersField?.GetValue(registry)
                as Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler>;
        if (handlers == null
            || !handlers.TryGetValue(
                ExteriorIncidentKind.Thief,
                out IExteriorIncidentHandler originalHandler))
        {
            return false;
        }

        DungeonGameSaveData baseline = saveService.Capture();
        bool baselineRestored = false;
        try
        {
            handlers[ExteriorIncidentKind.Thief] =
                new IncidentAuthorityProbeHandler();
            pacing.AdvanceToDay(Math.Max(31, pacing.CurrentDay));
            if (!runtime.TryStartIncident(
                    ExteriorIncidentKind.Thief,
                    "incident authority probe"))
            {
                return false;
            }

            ExteriorIncidentRuntimeState started = runtime.IncidentStates
                .Single(state => !state.IsTerminal
                    && state.kind == ExteriorIncidentKind.Thief);
            typeof(ExteriorActivityRuntime)
                .GetMethod(
                    "TickIncidentStates",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(runtime, new object[] { 2f });

            ExteriorIncidentRuntimeState queried = runtime.IncidentStates
                .Single(state => state.incidentId == started.incidentId);
            ExteriorIncidentSaveData active = runtime.ActiveIncidents
                .Single(state => state.incidentId == started.incidentId);
            ExteriorIncidentRuntimeState captured = runtime.Capture()
                .incidentStates
                .Single(state => state.incidentId == started.incidentId);
            ExteriorZoneMarker marker = runtime.Zones
                .Single(zone => zone.ZoneId == queried.zoneId);
            bool transitionAgrees =
                queried.stage == ExteriorIncidentStage.Interacting
                && Mathf.Approximately(queried.remainingSeconds, 37f)
                && Mathf.Approximately(active.remainingSeconds, 37f)
                && captured.stage == queried.stage
                && Mathf.Approximately(
                    captured.remainingSeconds,
                    queried.remainingSeconds)
                && marker.ActiveIncidentId == queried.incidentId
                && Mathf.Approximately(
                    marker.IncidentRemainingSeconds,
                    queried.remainingSeconds);
            if (!transitionAgrees)
            {
                return false;
            }

            DungeonGameSaveData snapshot = saveService.Capture();
            if (!saveService.TryRestore(
                    snapshot,
                    out DungeonGameRestoreReport restoreReport)
                || !restoreReport.Success)
            {
                return false;
            }

            ExteriorIncidentRuntimeState restored = runtime.IncidentStates
                .Single(state => state.incidentId == queried.incidentId);
            ExteriorIncidentRuntimeState recaptured = runtime.Capture()
                .incidentStates
                .Single(state => state.incidentId == queried.incidentId);
            ExteriorZoneMarker restoredMarker = runtime.Zones
                .Single(zone => zone.ZoneId == restored.zoneId);
            return restored.stage == ExteriorIncidentStage.Interacting
                && Mathf.Approximately(restored.remainingSeconds, 37f)
                && recaptured.stage == restored.stage
                && Mathf.Approximately(
                    recaptured.remainingSeconds,
                    restored.remainingSeconds)
                && restoredMarker.ActiveIncidentId == restored.incidentId
                && Mathf.Approximately(
                    restoredMarker.IncidentRemainingSeconds,
                    restored.remainingSeconds);
        }
        finally
        {
            handlers[ExteriorIncidentKind.Thief] = originalHandler;
            baselineRestored = saveService.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport restoreReport)
                && restoreReport.Success;
            if (!baselineRestored)
            {
                Debug.LogError(
                    "Exterior incident authority probe could not restore its baseline.");
            }
        }
    }

    private static bool VerifyInvalidExteriorPreflightPreservesLiveZones()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out IDungeonGameSaveService saveService,
                out _))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        if (before.Length == 0)
        {
            return false;
        }

        DungeonGameSaveData invalid = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                invalid,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData payload)
            || payload.zones.Count == 0)
        {
            return false;
        }

        payload.zones[0].buildingInstanceId =
            "invalid-exterior-building-id";
        DungeonSaveSectionPayload.Write(
            invalid,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            payload);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

        bool restored = saveService.TryRestore(
            invalid,
            out DungeonGameRestoreReport report);
        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        return !restored
            && !report.Success
            && before.SequenceEqual(after)
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate);
    }

    private static bool VerifyExteriorRoundtripPublishesDetachedZones()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out IDungeonGameSaveService saveService,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] expectedIds = before
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (expectedIds.Length == 0)
        {
            return false;
        }

        DungeonGameSaveData snapshot = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                snapshot,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData capturedExterior))
        {
            return false;
        }

        string expectedJson = JsonUtility.ToJson(capturedExterior);
        if (!saveService.TryRestore(
                snapshot,
                out DungeonGameRestoreReport report)
            || !report.Success)
        {
            return false;
        }

        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] restoredIds = after
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string restoredJson = JsonUtility.ToJson(exteriorRuntime.Capture());
        IRestoreWorldCandidateQuery candidates =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        return expectedIds.SequenceEqual(restoredIds)
            && string.Equals(
                expectedJson,
                restoredJson,
                StringComparison.Ordinal)
            && before.All(previous => after.All(current =>
                !ReferenceEquals(previous, current)))
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate)
            && !candidates.TryGetExteriorZones(out _)
            && report.Warnings.Count == 0;
    }

    private static bool VerifyLaterFailureDiscardsExteriorCandidate()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out _,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        IReadOnlyList<ExteriorZoneMarker> previousView = exteriorRuntime.Zones;
        object previousZonesRoot = GetPrivateField<object>(
            exteriorRuntime,
            "zones");
        object previousIncidentRoot = GetPrivateField<object>(
            exteriorRuntime,
            "incidentAggregate");
        int previousIncidentSequence = GetPrivateField<int>(
            exteriorRuntime,
            "incidentSequence");
        float previousConditionTick = GetPrivateField<float>(
            exteriorRuntime,
            "nextConditionTick");
        float previousIncidentCheck = GetPrivateField<float>(
            exteriorRuntime,
            "nextIncidentCheck");
        string previousCapture = JsonUtility.ToJson(exteriorRuntime.Capture());
        string[] expectedIds = before
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (expectedIds.Length == 0)
        {
            return false;
        }

        IDungeonSaveSectionRegistry liveRegistry =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        IDungeonSaveSection facilitySection = liveRegistry.OrderedSections
            .Single(section =>
                section.SectionId == ModularFacilityWorldSaveSection.Id);
        IDungeonSaveSection exteriorSection = liveRegistry.OrderedSections
            .Single(section =>
                section.SectionId == ExteriorActivitySaveSection.Id);
        MarkerDependencySection runDependency =
            new MarkerDependencySection(RunVariableSaveSection.Id);
        MarkerDependencySection metaDependency =
            new MarkerDependencySection(MetaProgressionSaveSection.Id);
        MarkerDependencySection itemDependency =
            new MarkerDependencySection(PhysicalItemsSaveSection.Id);
        MarkerDependencySection workDependency =
            new MarkerDependencySection(WorkOrdersSaveSection.Id);
        MarkerDependencySection characterDependency =
            new MarkerDependencySection(CharacterWorldSaveSection.Id);
        MarkerDependencySection wildlifeDependency =
            new MarkerDependencySection(WildlifeSaveSection.Id);
        DungeonRuntimeAggregateRootStore isolatedRoot =
            new DungeonRuntimeAggregateRootStore();
        IDungeonRestoreTransactionParticipant facilityParticipant =
            scope.Container.Resolve<IModularFacilityWorldSaveService>()
                as IDungeonRestoreTransactionParticipant;
        if (facilityParticipant == null)
        {
            return false;
        }
        LateExteriorParticipantFaultProbe lateParticipant = new(
            exteriorRuntime,
            before);

        DungeonSaveSectionRegistry testRegistry =
            new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[]
                {
                    runDependency,
                    metaDependency,
                    itemDependency,
                    facilitySection,
                    workDependency,
                    characterDependency,
                    wildlifeDependency,
                    exteriorSection
                },
                isolatedRoot,
                new IDungeonRestoreTransactionParticipant[]
                {
                    facilityParticipant,
                    exteriorRuntime,
                    lateParticipant
                });

        List<DungeonSaveSectionEnvelope> snapshot =
            testRegistry.CaptureAll();
        DungeonSaveSectionEnvelope exteriorEnvelope = snapshot.Single(
            envelope => envelope.sectionId == ExteriorActivitySaveSection.Id);
        DungeonExteriorActivitySaveData exteriorPayload =
            JsonUtility.FromJson<DungeonExteriorActivitySaveData>(
                exteriorEnvelope.payloadJson);
        exteriorPayload.incidentStates.Clear();
        exteriorEnvelope.payloadJson = JsonUtility.ToJson(exteriorPayload);
        IRestoreWorldCandidateQuery candidateIndex =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        if (!gridProvider.TryGetGrid(out Grid liveGrid))
        {
            return false;
        }

        int detachedBefore = CountDetachedExteriorCandidates();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = testRegistry.RestoreAll(snapshot, report);
        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] restoredIds = after
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        bool sameGrid = gridProvider.TryGetGrid(out Grid afterGrid)
            && ReferenceEquals(liveGrid, afterGrid);
        bool candidateIndexClear = !candidateIndex.TryGetGrid(out _)
            && !candidateIndex.TryGetBuildings(out _)
            && !candidateIndex.TryGetExteriorZones(out _);
        int detachedAfter = CountDetachedExteriorCandidates();
        bool exactRollback = !restored
            && !report.Success
            && report.Errors.Any(error => error.Contains(
                LateExteriorParticipantFaultProbe.FailureMessage,
                StringComparison.Ordinal))
            && lateParticipant.PublishCount == 1
            && lateParticipant.RollbackCount == 1
            && lateParticipant.CompleteCount == 0
            && lateParticipant.ObservedOldActiveAndCandidateHidden
            && before.SequenceEqual(after)
            && ReferenceEquals(previousView, exteriorRuntime.Zones)
            && ReferenceEquals(
                previousZonesRoot,
                GetPrivateField<object>(exteriorRuntime, "zones"))
            && ReferenceEquals(
                previousIncidentRoot,
                GetPrivateField<object>(exteriorRuntime, "incidentAggregate"))
            && previousIncidentSequence == GetPrivateField<int>(
                exteriorRuntime,
                "incidentSequence")
            && Mathf.Approximately(
                previousConditionTick,
                GetPrivateField<float>(exteriorRuntime, "nextConditionTick"))
            && Mathf.Approximately(
                previousIncidentCheck,
                GetPrivateField<float>(exteriorRuntime, "nextIncidentCheck"))
            && string.Equals(
                previousCapture,
                JsonUtility.ToJson(exteriorRuntime.Capture()),
                StringComparison.Ordinal)
            && expectedIds.SequenceEqual(restoredIds)
            && sameGrid
            && candidateIndexClear
            && !isolatedRoot.IsRestoreStaging
            && isolatedRoot.PublishedRestoreRevision == 0
            && detachedAfter == detachedBefore
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate);
        if (!exactRollback)
        {
            return false;
        }

        DungeonGameRestoreReport successReport = new DungeonGameRestoreReport();
        bool completed = testRegistry.RestoreAll(snapshot, successReport);
        ExteriorZoneMarker[] completedZones = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        bool successCandidateIndexClear = !candidateIndex.TryGetGrid(out _)
            && !candidateIndex.TryGetBuildings(out _)
            && !candidateIndex.TryGetExteriorZones(out _);
        return completed
            && successReport.Success
            && lateParticipant.PublishCount == 2
            && lateParticipant.RollbackCount == 1
            && lateParticipant.CompleteCount == 1
            && completedZones.Length == before.Length
            && completedZones.All(zone => before.All(old =>
                !ReferenceEquals(zone, old)))
            && completedZones.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate)
            && before.All(zone => zone == null || !zone.gameObject.activeSelf)
            && successCandidateIndexClear
            && isolatedRoot.PublishedRestoreRevision == 1;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target?.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Missing private field '{fieldName}' on {target?.GetType().Name ?? "null"}.");
        }

        return (T)field.GetValue(target);
    }

    private static bool TryResolveSaveScenario(
        out ExteriorActivityRuntime exteriorRuntime,
        out IDungeonGameSaveService saveService,
        out DungeonRuntimeLifetimeScope scope)
    {
        exteriorRuntime = null;
        saveService = null;
        scope = null;
        if (!Application.isPlaying)
        {
            return false;
        }

        scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        exteriorRuntime = scope.Container.Resolve<ExteriorActivityRuntime>();
        saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        return exteriorRuntime.Zones.Any(zone => zone != null);
    }

    private static bool VerifyPlayModeReceptionWorkCandidate()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery zoneQuery = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorZoneMarker reception = zoneQuery.GetZones(ExteriorZoneType.ReceptionPoint).FirstOrDefault();
        if (reception == null || !reception.CanRunReceptionWork)
        {
            Debug.LogWarning($"Reception marker unavailable. marker={reception != null}, canRun={reception != null && reception.CanRunReceptionWork}");
            return false;
        }

        AbilityWork[] workers = UnityEngine.Object.FindObjectsByType<AbilityWork>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(work => work != null
                && work.WorkerActor != null)
            .ToArray();
        GameObject temporaryWorkerObject = null;
        if (workers.Length == 0
            && TryCreateTemporaryReceptionWorker(zoneQuery, reception, out AbilityWork temporaryWorker, out temporaryWorkerObject))
        {
            workers = new[] { temporaryWorker };
        }

        Debug.Log($"Reception candidate workers={workers.Length}");
        try
        {
            foreach (AbilityWork work in workers)
            {
                work.EnsureWorkReferences();
                Grid grid = work.CachedGrid;
                if (grid == null)
                {
                    Debug.LogWarning($"Reception candidate skipped actor={work.WorkerActor.name} reason=no-grid");
                    continue;
                }

                GridPathSearchResult search = grid.SearchPath(work.WorkerActor.GetNowXY());
                work.SetWorkPriority(BuiltInWorkTypeIds.Reception, WorkPriorityLevel.Priority1, search);
                bool canStart = work.CanStartWorkAction(BuiltInWorkTypeIds.Reception, search);
                bool found = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Reception,
                    search,
                    out WorkTargetCandidate candidate);
                ExteriorZoneMarker candidateZone =
                    WorkTargetCandidateRuntimeAdapter
                        .ResolveBuilding<ExteriorZoneMarker>(candidate);
                BuildableObject candidateBuilding =
                    WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
                bool isRegisteredReceptionZone = candidateZone != null
                    && candidateZone.CanRunReceptionWork
                    && zoneQuery.Zones.Contains(candidateZone);
                bool positionReachable = search.GetReachablePositions().Contains(reception.GridPosition);
                bool buildingReachable = search.GetAllReachableBuilding().Contains(reception);
                Debug.Log(
                    $"Reception candidate probe actor={work.WorkerActor.name} "
                    + $"pos={work.WorkerActor.GetNowXY()} "
                    + $"hasExteriorQuery={work.HasExteriorZoneQuery} "
                    + $"receptionPos={reception.GridPosition} "
                    + $"positionReachable={positionReachable} "
                    + $"buildingReachable={buildingReachable} "
                    + $"canStart={canStart} found={found} "
                    + $"candidateValid={candidate.IsValid} "
                    + $"candidateType={candidate.WorkTypeId} "
                    + $"candidateBuilding={(candidateBuilding != null ? candidateBuilding.name : "null")} "
                    + $"sameReception={candidateBuilding == reception} "
                    + $"registeredReceptionZone={isRegisteredReceptionZone} "
                    + $"lastRejected={work.LastRejectedWorkCandidate.FailureKind}");
                if (canStart
                    && found
                    && candidate.IsValid
                    && candidate.WorkTypeId == BuiltInWorkTypeIds.Reception
                    && isRegisteredReceptionZone)
                {
                    return true;
                }
            }
        }
        finally
        {
            if (temporaryWorkerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryWorkerObject);
            }
        }

        return false;
    }

    private static bool TryCreateTemporaryReceptionWorker(
        IExteriorZoneQuery zoneQuery,
        ExteriorZoneMarker reception,
        out AbilityWork work,
        out GameObject workerObject)
    {
        work = null;
        workerObject = null;
        if (zoneQuery == null || reception == null)
        {
            return false;
        }

        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null || !grid.TryFindNearestWalkablePosition(reception.GridPosition, out Vector2Int spawnPosition))
        {
            return false;
        }

        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset");
        if (data == null)
        {
            return false;
        }

        workerObject = new GameObject("ExteriorReceptionCandidateWorker");
        workerObject.AddComponent<SpriteRenderer>();
        workerObject.AddComponent<CharacterActor>();
        workerObject.AddComponent<AbilityMove>();
        work = workerObject.AddComponent<AbilityWork>();
        workerObject.AddComponent<AIBrain>();
        workerObject.transform.position = grid.GetWorldPos(spawnPosition);

        CharacterAiEditorTestDependencies.Inject(workerObject);
        CharacterActor character = workerObject.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        work = workerObject.GetComponent<AbilityWork>();
        typeof(AbilityWork)
            .GetField("exteriorZoneQuery", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(work, zoneQuery);
        work.EnsureWorkReferences();
        return work != null;
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private static int CountDetachedExteriorCandidates()
    {
        return Resources.FindObjectsOfTypeAll<ExteriorZoneMarker>()
            .Count(zone => zone != null && zone.IsDetachedRestoreCandidate);
    }

    private sealed class MarkerDependencySection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;

        public MarkerDependencySection(string id)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Foundation;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
        }
    }

    private sealed class LateExteriorParticipantFaultProbe :
        IDungeonRestoreTransactionParticipant
    {
        internal const string FailureMessage =
            "Intentional later participant failure after exterior publication.";

        private readonly ExteriorActivityRuntime runtime;
        private readonly IReadOnlyList<ExteriorZoneMarker> previousZones;
        private bool failNextPublish = true;

        internal LateExteriorParticipantFaultProbe(
            ExteriorActivityRuntime runtime,
            IReadOnlyList<ExteriorZoneMarker> previousZones)
        {
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));
            this.previousZones = previousZones
                ?? throw new ArgumentNullException(nameof(previousZones));
        }

        public string ParticipantId => "999.debug.exterior-late-participant";
        internal int PublishCount { get; private set; }
        internal int RollbackCount { get; private set; }
        internal int CompleteCount { get; private set; }
        internal bool ObservedOldActiveAndCandidateHidden { get; private set; }

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            PublishCount++;
            ObservedOldActiveAndCandidateHidden =
                previousZones.All(zone => zone != null
                    && zone.gameObject.activeSelf
                    && !zone.IsDetachedRestoreCandidate)
                && runtime.Zones.Count > 0
                && runtime.Zones.All(zone => zone != null
                    && !zone.gameObject.activeSelf
                    && zone.IsDetachedRestoreCandidate);
            if (!failNextPublish)
            {
                return;
            }

            failNextPublish = false;
            throw new InvalidOperationException(FailureMessage);
        }

        public void RollbackPublishedRestoreCandidate()
        {
            RollbackCount++;
        }

        public void CompleteRestoreCandidate()
        {
            CompleteCount++;
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private sealed class IncidentAuthorityProbeHandler :
        IExteriorIncidentHandler
    {
        public ExteriorIncidentKind Kind => ExteriorIncidentKind.Thief;
        public string DefaultText => "incident authority probe";
        public float DurationSeconds => 90f;

        public bool TryBegin(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            out string failureReason)
        {
            state.stage = ExteriorIncidentStage.Active;
            failureReason = string.Empty;
            return zone != null;
        }

        public void Tick(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            float deltaTime)
        {
            state.remainingSeconds = 37f;
            state.stage = ExteriorIncidentStage.Interacting;
        }

        public void Restore(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone)
        {
        }

        public bool TryExecutePrimaryAction(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            out string message)
        {
            state.remainingSeconds = 0f;
            state.stage = ExteriorIncidentStage.Resolved;
            message = "resolved";
            return true;
        }
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }
}
#endif
