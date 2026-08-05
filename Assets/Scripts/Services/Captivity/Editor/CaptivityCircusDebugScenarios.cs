#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

public static class CaptivityCircusDebugScenarios
{
    private const string ReportPath = "Temp/captivity-circus-contracts.tsv";

    [MenuItem("DungeonStory/Debug/Captivity/Run Captivity And Circus Contracts")]
    public static void RunFromMenu()
    {
        bool success = RunAll(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Captivity and circus contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        CaptivityFacilityAssetBuilder.BuildAll();
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("door_default_and_precedence", VerifyDoorAccessPolicy, lines, errors);
        Run("door_presets", VerifyDoorAccessPresets, lines, errors);
        Run("captive_thresholds_and_clone", VerifyCaptiveThresholds, lines, errors);
        Run("captivity_save_validation", VerifyCaptivitySaveValidation, lines, errors);
        Run("interaction_registry_and_materials", VerifyInteractions, lines, errors);
        Run("circus_registry_and_programs", VerifyCircusPrograms, lines, errors);
        Run("circus_persistent_identity", VerifyCircusPersistentIdentity, lines, errors);
        Run("circus_and_wildlife_save_clone", VerifyCircusSaveModels, lines, errors);
        Run("circus_save_validation", VerifyCircusSaveValidation, lines, errors);
        Run("captivity_facility_assets", VerifyFacilityAssets, lines, errors);
        Run("constructor_facades", VerifyConstructorFacades, lines, errors);
        Run(
            "captivity_late_publish_failure_preserves_escort_parents",
            VerifyLatePublishFailurePreservesEscortParents,
            lines,
            errors);

        File.WriteAllLines(ReportPath, lines);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log($"Captivity and circus contracts PASS. Report: {ReportPath}");
        }

        return errors.Count == 0;
    }

    private static string VerifyDoorAccessPolicy()
    {
        DoorAccessPolicyState state = new DoorAccessPolicyState();
        Require(state.AllowedGroups == DoorAccessGroup.All, "new door was not open to every group");
        Require(!state.IsRestricted, "new all-access door was marked restricted");

        state.SetGroupAllowed(DoorAccessGroup.Captive, false);
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "captive group remained allowed");
        state.SetIndividualRule("captive:one", DoorAccessIndividualRule.Allow);
        Require(
            state.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Allow,
            "individual allow was not stored");
        state.SetIndividualRule("captive:one", DoorAccessIndividualRule.Deny);
        Require(
            state.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Deny,
            "individual deny did not override allow");

        DoorAccessPolicyState clone = state.Clone();
        Require(clone.AllowedGroups == state.AllowedGroups, "door policy clone lost groups");
        Require(
            clone.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Deny,
            "door policy clone lost individual deny");
        return "all-access default and deny-over-allow precedence preserved";
    }

    private static string VerifyDoorAccessPresets()
    {
        DoorAccessPolicyState state = new DoorAccessPolicyState();
        state.ApplyPreset(DoorAccessPreset.Cell);
        Require(state.IsGroupAllowed(DoorAccessGroup.Owner), "cell denied owner");
        Require(state.IsGroupAllowed(DoorAccessGroup.Staff), "cell denied staff");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "cell allowed captive");
        Require(!state.IsGroupAllowed(DoorAccessGroup.CaptiveWildlife), "cell allowed captive wildlife");

        state.ApplyPreset(DoorAccessPreset.AnimalPen);
        Require(state.IsGroupAllowed(DoorAccessGroup.CaptiveWildlife), "animal pen denied captive wildlife");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Wildlife), "animal pen allowed ordinary wildlife");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "animal pen allowed captive");
        return "cell and animal-pen presets separate their contained groups";
    }

    private static string VerifyCaptiveThresholds()
    {
        CaptiveState state = new CaptiveState
        {
            captiveId = "captive:test",
            status = CaptivityStatus.Confined,
            compliance = 49f,
            health = 100f,
            trust = 70f,
            grudge = 30f,
            corruption = 59f
        };

        Require(!state.CanLabor, "labor unlocked below compliance 50");
        state.compliance = 50f;
        Require(state.CanLabor, "labor did not unlock at compliance 50");
        state.health = 39f;
        Require(!state.CanLabor, "labor remained available below health 40");
        state.health = 40f;
        Require(state.CanRecruit, "recruitment thresholds rejected a valid captive");
        state.corruption = 60f;
        Require(!state.CanRecruit, "recruitment allowed corruption 60");
        state.corruption = 80f;
        Require(state.CanBecomeMinion, "minion conversion did not unlock at corruption 80");

        CaptiveState clone = state.Clone();
        clone.will = 1f;
        Require(!Mathf.Approximately(state.will, clone.will), "captive clone shared mutable state");
        return "labor, recruitment, and minion thresholds are exact";
    }

    private static string VerifyInteractions()
    {
        ICaptivityInteractionHandler[] handlers =
        {
            new CaptivityPersuasionHandler(),
            new CaptivityIsolationHandler(),
            new CaptivityCoercionHandler(),
            new CaptivityInterrogationHandler(),
            new CaptivityIndoctrinationHandler(),
            new CaptivityBrandingHandler(),
            new CaptivityBloodExtractionHandler(),
            new CaptivityMemoryExtractionHandler(),
            new CaptivityForcedModificationHandler(),
            new CaptivityCorruptionRitualHandler()
        };
        CaptivityInteractionRegistry registry = new CaptivityInteractionRegistry(handlers);
        Require(registry.All.Count == 10, $"expected 10 interactions, got {registry.All.Count}");
        Require(
            handlers.Select(handler => handler.InteractionId).Distinct(StringComparer.Ordinal).Count()
            == handlers.Length,
            "interaction ids were not unique");
        foreach (ICaptivityInteractionHandler handler in handlers)
        {
            Require(handler.RequiredWork > 0f, $"{handler.InteractionId} had no work amount");
            Require(
                handler.MaterialRequirements.Count > 0
                && handler.MaterialRequirements.All(requirement => requirement.Value > 0),
                $"{handler.InteractionId} had no physical material requirement");
            Require(
                registry.TryGet(handler.InteractionId, out ICaptivityInteractionHandler resolved)
                && ReferenceEquals(handler, resolved),
                $"{handler.InteractionId} did not resolve from the registry");
        }

        bool duplicateRejected = false;
        try
        {
            _ = new CaptivityInteractionRegistry(
                new ICaptivityInteractionHandler[]
                {
                    new CaptivityPersuasionHandler(),
                    new CaptivityPersuasionHandler()
                });
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        Require(duplicateRejected, "duplicate interaction id was accepted");
        return "10 registered interactions require work and hauled materials";
    }

    private static string VerifyCaptivitySaveValidation()
    {
        CaptivitySaveData payload = new CaptivitySaveData
        {
            captureSequence = 4,
            policySequence = 0,
            policies = new List<CaptivePolicyData>
            {
                new CaptivePolicyData
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            },
            captives = new List<CaptiveState>
            {
                new CaptiveState
                {
                    captiveId = "captive:test",
                    displayName = "테스트 포로",
                    speciesTag = "human",
                    status = CaptivityStatus.AwaitingCapture,
                    policyId = CaptivityPolicyIds.Standard
                }
            }
        };
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(payload, validReport);
        Require(
            validReport.Success,
            $"valid captivity payload failed: {string.Join(" | ", validReport.Errors)}");

        CaptivitySaveData duplicate = CloneCaptivityPayload(payload);
        duplicate.captives.Add(duplicate.captives[0].Clone());
        DungeonGameRestoreReport duplicateReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(duplicate, duplicateReport);
        Require(
            !duplicateReport.Success,
            "duplicate captive ID passed strict validation");

        CaptivitySaveData badSequence = CloneCaptivityPayload(payload);
        badSequence.policies.Add(new CaptivePolicyData
        {
            policyId = "captivity:custom:2",
            displayName = "사용자 정책"
        });
        badSequence.policySequence = 1;
        DungeonGameRestoreReport sequenceReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(badSequence, sequenceReport);
        Require(
            !sequenceReport.Success,
            "reused captivity policy sequence passed strict validation");

        CaptivitySaveData escort = CloneCaptivityPayload(payload);
        CaptiveState escortState = escort.captives[0];
        escortState.status = CaptivityStatus.Escorting;
        escortState.reservedCarrierId = "staff:carrier";
        escortState.housingBuildingId = "building:test-cell";
        escortState.restraintStackId = "stack:restraint";
        escortState.restraintItemId = CaptivityItemDefinitions.RestraintsItemId;
        escortState.restraintQuantity = 1;
        escortState.restrained = true;
        DungeonGameRestoreReport escortReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(escort, escortReport);
        Require(
            escortReport.Success,
            $"valid escort payload failed: {string.Join(" | ", escortReport.Errors)}");
        CaptiveState normalized = CaptivitySaveValidation
            .CreateRestoredCaptive(escortState);
        Require(
            normalized.status == CaptivityStatus.AwaitingCapture
            && normalized.reservedCarrierId.Length == 0
            && normalized.housingBuildingId.Length == 0
            && normalized.restraintStackId.Length == 0
            && !normalized.restrained,
            "transient escort state was not normalized in the detached candidate");
        return "strict DTO validation rejects duplicates/sequence reuse and safely resets transient escort state";
    }

    private static CaptivitySaveData CloneCaptivityPayload(
        CaptivitySaveData source)
    {
        return new CaptivitySaveData
        {
            version = source.version,
            captureSequence = source.captureSequence,
            policySequence = source.policySequence,
            policies = source.policies.Select(policy => policy.Clone()).ToList(),
            captives = source.captives.Select(captive => captive.Clone()).ToList()
        };
    }

    private static string VerifyCircusPrograms()
    {
        ICircusProgramHandler[] handlers =
        {
            new NonlethalActCircusProgram(),
            new DangerousStuntCircusProgram(),
            new CaptiveDuelCircusProgram(),
            new BeastShowCircusProgram(),
            new BeastArenaCircusProgram(),
            new PublicPunishmentCircusProgram(),
            new ExecutionPlayCircusProgram(),
            new PublicCorruptionRitualCircusProgram()
        };
        CircusProgramRegistry registry = new CircusProgramRegistry(handlers);
        IReadOnlyList<CircusProgramModule> programs = registry.Definitions;
        Require(programs.Count == 8, $"expected 8 circus programs, got {programs.Count}");
        Require(
            programs.Select(program => program.programId).Distinct(StringComparer.Ordinal).Count()
            == programs.Count,
            "circus program ids were not unique");
        Require(
            programs.Any(program => program.requiresWildlife && program.usesCombat),
            "wildlife combat program was missing");
        Require(
            programs.Any(program => program.requiresCaptive && !program.publiclyCruel),
            "non-cruel captive performance was missing");
        Require(
            programs.Count(program => program.publiclyCruel) >= 4,
            "cruel performance range was incomplete");
        return "8 programs cover nonlethal, dangerous, combat, wildlife, and cruel shows";
    }

    private static string VerifyCircusSaveModels()
    {
        CircusShowOrder order = new CircusShowOrder
        {
            orderId = "show:test",
            performerIds = new List<string> { "captive:one" },
            wildlifeIds = new List<string> { "wildlife:one" },
            audienceIds = new List<string> { "customer:one" },
            performerPositions = new List<Vector2Int> { new Vector2Int(1, 2) }
        };
        CircusShowOrder clone = order.Clone();
        clone.performerIds.Add("captive:two");
        Require(order.performerIds.Count == 1, "circus order clone shared performer list");

        CapturedWildlifeState wildlife = new CapturedWildlifeState
        {
            wildlifeId = "wildlife:one",
            transportState = CapturedWildlifeTransportState.Penned,
            escapeRisk = 42f,
            foodDeliveryPending = true,
            lastCareStatus = "먹이 대기"
        };
        CapturedWildlifeState wildlifeClone = wildlife.Clone();
        wildlifeClone.escapeRisk = 1f;
        Require(
            Mathf.Approximately(wildlife.escapeRisk, 42f),
            "captured wildlife clone shared state");
        return "show collections and wildlife care state clone independently";
    }

    private static string VerifyCircusPersistentIdentity()
    {
        GameObject firstRuntime = new("circus-identity-first");
        GameObject secondRuntime = new("circus-identity-second");
        try
        {
            Require(
                firstRuntime.GetInstanceID() != secondRuntime.GetInstanceID(),
                "circus identity fixture did not create distinct Unity objects");

            CircusCombatant first = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:one"),
                firstRuntime,
                () => true);
            CircusCombatant reconstructed = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:one"),
                secondRuntime,
                () => true);
            CircusCombatant differentId = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:two"),
                firstRuntime,
                () => true);
            CircusCombatant differentKind = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Wildlife,
                    "character:persistent:one"),
                firstRuntime,
                () => true);

            Require(
                first.Equals(reconstructed),
                "same persistent circus identity changed across Unity objects");
            Require(
                first.GetHashCode() == reconstructed.GetHashCode(),
                "same persistent circus identity produced different hashes");
            Require(
                !first.Equals(differentId),
                "different circus persistent IDs compared equal");
            Require(
                !first.Equals(differentKind),
                "different circus combatant kinds compared equal");
            Require(
                new HashSet<CircusCombatant>
                {
                    first,
                    reconstructed,
                    differentId,
                    differentKind
                }.Count == 3,
                "circus identity set did not collapse only the reconstructed combatant");

            return "stable kind/id identity survives Unity-object reconstruction";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstRuntime);
            UnityEngine.Object.DestroyImmediate(secondRuntime);
        }
    }

    private static string VerifyCircusSaveValidation()
    {
        CircusProgramRegistry programs = new CircusProgramRegistry(
            new ICircusProgramHandler[] { new TrophyDisplayCircusProgram() });
        CircusShowOrder order = new CircusShowOrder
        {
            orderId = "circus:1",
            stageId = "building:stage",
            stagePosition = new Vector2Int(4, 5),
            roomId = 3,
            programId = "circus:trophy-display",
            state = CircusShowState.Performing,
            wildlifeIds = new List<string> { "wildlife:one" },
            wildlifePositions = new List<Vector2Int> { new Vector2Int(4, 6) },
            preparationWorkRequired = 10f,
            preparationWorkCompleted = 8f,
            elapsedShowSeconds = 4f,
            showDurationSeconds = 20f,
            nextCombatExchangeAt = 0.5f,
            phaseElapsedSeconds = 2f,
            ticketPrice = 1,
            venueAccidentDamageMultiplier = 1f,
            venueFilthMultiplier = 1f,
            statusMessage = "running"
        };
        CapturedWildlifeState wildlife = new CapturedWildlifeState
        {
            wildlifeId = "wildlife:one",
            speciesId = "wildlife:test",
            penId = "building:pen",
            penPosition = new Vector2Int(1, 1),
            capturePosition = new Vector2Int(2, 2),
            assignedShowOrderId = order.orderId,
            transportState = CapturedWildlifeTransportState.MovingToShow,
            nextCareAt = 1f,
            lastCareStatus = "moving"
        };
        CircusSaveData payload = new CircusSaveData
        {
            nextOrderSequence = 1,
            orders = new List<CircusShowOrder> { order },
            capturedWildlife = new List<CapturedWildlifeState> { wildlife }
        };

        DungeonGameRestoreReport valid = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(payload, programs, valid);
        Require(valid.Success,
            "valid circus V2 payload failed: " + string.Join(" | ", valid.Errors));

        CircusSaveData duplicate = CloneCircusPayload(payload);
        duplicate.orders.Add(duplicate.orders[0].Clone());
        DungeonGameRestoreReport duplicateReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(duplicate, programs, duplicateReport);
        Require(!duplicateReport.Success, "duplicate circus order was accepted");

        CircusSaveData sequenceReuse = CloneCircusPayload(payload);
        sequenceReuse.nextOrderSequence = 0;
        DungeonGameRestoreReport sequenceReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(sequenceReuse, programs, sequenceReport);
        Require(!sequenceReport.Success, "circus order sequence reuse was accepted");

        CircusSaveData incoherent = CloneCircusPayload(payload);
        incoherent.capturedWildlife[0].transportState =
            CapturedWildlifeTransportState.Penned;
        DungeonGameRestoreReport incoherentReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(incoherent, programs, incoherentReport);
        Require(!incoherentReport.Success,
            "penned wildlife with an active show assignment was accepted");

        CircusShowOrder restoredOrder =
            CircusSaveValidation.CreateRestoredOrder(order);
        Require(
            restoredOrder.state == CircusShowState.Composition
            && restoredOrder.statusMessage == "circus.preparation-resumed",
            "active circus order was not normalized to composition");
        CapturedWildlifeState normalized =
            CircusSaveValidation.CreateRestoredCapturedWildlife(wildlife);
        Require(
            normalized.transportState == CapturedWildlifeTransportState.Penned
            && normalized.assignedShowOrderId.Length == 0
            && normalized.reservedCarrierId.Length == 0,
            "in-flight wildlife was not normalized to a safe pen state");
        return "strict V2 validation rejects duplicates/sequence reuse/coherence errors and normalizes transient work";
    }

    private static CircusSaveData CloneCircusPayload(CircusSaveData source)
    {
        return new CircusSaveData
        {
            version = source.version,
            nextOrderSequence = source.nextOrderSequence,
            orders = source.orders.Select(order => order.Clone()).ToList(),
            capturedWildlife = source.capturedWildlife
                .Select(state => state.Clone())
                .ToList()
        };
    }

    private static string VerifyFacilityAssets()
    {
        VerifyFacility<BuildingCaptiveHousingAbility>(
            "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset",
            1200,
            FacilityRole.None);
        VerifyFacility<BuildingCircusStageAbility>(
            "Assets/Resources/SO/Building/Captivity/CS01_중앙무대.asset",
            1201,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingAudienceSeatingAbility>(
            "Assets/Resources/SO/Building/Captivity/CS02_관람석.asset",
            1202,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingBeastPenAbility>(
            "Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset",
            1203,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusTicketBoothAbility>(
            "Assets/Resources/SO/Building/Captivity/CT01_매표소.asset",
            1204,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusGamblingAbility>(
            "Assets/Resources/SO/Building/Captivity/CG01_도박창구.asset",
            1205,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusAnnouncerAbility>(
            "Assets/Resources/SO/Building/Captivity/CA01_진행자단상.asset",
            1206,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusHazardAbility>(
            "Assets/Resources/SO/Building/Captivity/CH01_위험장치.asset",
            1207,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusTreatmentZoneAbility>(
            "Assets/Resources/SO/Building/Captivity/CM01_치료구역.asset",
            1208,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingPublicPunishmentAbility>(
            "Assets/Resources/SO/Building/Captivity/CP02_공개형벌장치.asset",
            1209,
            FacilityRole.Entertainment);
        return "captivity and circus facilities retain ids, roles, and abilities";
    }

    private static string VerifyConstructorFacades()
    {
        Type runtimeAssemblyType = typeof(CaptivityRuntime);
        Type restoreCoordinator = runtimeAssemblyType.Assembly.GetType(
            "CircusRestoreCoordinator",
            throwOnError: true);
        Type restoreStateContext = runtimeAssemblyType.Assembly.GetType(
            "CircusRestoreStateContext",
            throwOnError: true);
        Type[] ownedTypes =
        {
            typeof(CaptivityRuntime),
            typeof(CaptivityCharacterContext),
            typeof(CaptivityWorldContext),
            typeof(CaptivitySessionContext),
            typeof(WildlifeCaptureRuntime),
            typeof(WildlifeCaptureWorldContext),
            typeof(WildlifeCaptureCareContext),
            typeof(WildlifeCaptureSessionContext),
            typeof(CircusRuntime),
            typeof(CircusProgramContext),
            typeof(CircusWorldContext),
            typeof(CircusCombatContext),
            typeof(CircusSessionContext),
            restoreCoordinator,
            restoreStateContext
        };
        string[] violations = ownedTypes
            .SelectMany(type => type.GetConstructors(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                .Select(constructor => new
                {
                    Type = type,
                    Count = constructor.GetParameters().Length
                }))
            .Where(entry => entry.Count > 8)
            .Select(entry => $"{entry.Type.Name}:{entry.Count}")
            .ToArray();
        Require(
            violations.Length == 0,
            "constructor dependency limit exceeded: "
            + string.Join(", ", violations));

        RequireNullGuard(
            () => new CaptivityRuntime(null, null, null),
            nameof(CaptivityRuntime));
        RequireNullGuard(
            () => new WildlifeCaptureRuntime(null, null, null),
            nameof(WildlifeCaptureRuntime));
        RequireNullGuard(
            () => new CircusRuntime(null, null, null, null),
            nameof(CircusRuntime));
        RequireReflectedNullGuard(restoreCoordinator);
        return "runtime constructors 3/3/4 and restore coordinator 3; all contexts <= 6";
    }

    private static string VerifyLatePublishFailurePreservesEscortParents()
    {
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        TrackingEscortRestoreLifecycle escort = new();
        escort.SetParent("captive:one", "housing:old");
        escort.SetParent("captive:two", "carrier:old");
        string originalParents = escort.CaptureParents();
        EscortProjectionRestoreParticipant escortParticipant = new(escort);
        LateFailingRestoreParticipant lateParticipant = new();
        DungeonSaveSectionRegistry registry = new(
            Array.Empty<IDungeonSaveSection>(),
            aggregateRootStore,
            new IDungeonRestoreTransactionParticipant[]
            {
                escortParticipant,
                lateParticipant
            });

        DungeonGameRestoreReport failureReport = new();
        bool restored = registry.RestoreAll(
            Array.Empty<DungeonSaveSectionEnvelope>(),
            failureReport);
        Require(
            escort.CaptureParents() == originalParents,
            "captivity publish discarded live escort parent state");
        Require(!restored, "late participant did not fail publication");
        Require(
            failureReport.Errors.Any(error => error.Contains(
                lateParticipant.ParticipantId,
                StringComparison.Ordinal)),
            "registry did not report the late participant publication failure");
        Require(
            escort.CaptureParents() == originalParents,
            "captivity rollback did not preserve the exact escort parent map");
        Require(
            escort.ClearCount == 0,
            "captivity rollback cleared escort transient state");

        lateParticipant.ThrowOnPublish = false;
        DungeonGameRestoreReport successReport = new();
        restored = registry.RestoreAll(
            Array.Empty<DungeonSaveSectionEnvelope>(),
            successReport);
        Require(restored && successReport.Success,
            "successful registry publication did not complete");
        Require(
            escort.ClearCount == 1 && escort.CaptureParents().Length == 0,
            "successful completion did not finalize escort transient state");

        return "late failure retains exact parent map; completion clears it once";
    }

    private static T Uninitialized<T>()
        where T : class
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }

    private static void RequireNullGuard(Action create, string typeName)
    {
        try
        {
            create();
        }
        catch (ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeName} accepted missing required dependencies.");
    }

    private static void RequireReflectedNullGuard(Type type)
    {
        ConstructorInfo constructor = type.GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            .Single();
        try
        {
            constructor.Invoke(new object[] { null, null, null });
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{type.Name} accepted missing required dependencies.");
    }

    private static void VerifyFacility<TAbility>(
        string path,
        int expectedId,
        FacilityRole expectedRole)
        where TAbility : BuildingAbility
    {
        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        Require(asset != null, $"facility asset missing: {path}");
        Require(asset.id == expectedId, $"{path} id was {asset.id}");
        Require(asset.GetAbility<TAbility>() != null, $"{path} missing {typeof(TAbility).Name}");
        FacilityData facility = asset.Facility;
        Require(facility != null, $"{path} missing facility settings");
        Require(
            expectedRole == FacilityRole.None || (facility.roles & expectedRole) != 0,
            $"{path} missing role {expectedRole}");
    }

    private sealed class TrackingEscortRestoreLifecycle :
        ICaptivityEscortRestoreLifecycle
    {
        private readonly SortedDictionary<string, string> parents =
            new(StringComparer.Ordinal);

        public int ClearCount { get; private set; }

        public void SetParent(string captiveId, string parentId)
        {
            parents[captiveId] = parentId;
        }

        public string CaptureParents() => string.Join(
            "|",
            parents.Select(entry => $"{entry.Key}={entry.Value}"));

        public void ClearTransientState()
        {
            ClearCount++;
            parents.Clear();
        }

        public void RestoreCaptiveParent(string captiveId)
        {
        }
    }

    private sealed class EscortProjectionRestoreParticipant :
        IDungeonRestoreTransactionParticipant
    {
        private readonly TrackingEscortRestoreLifecycle escort;
        private bool publicationPending;

        public EscortProjectionRestoreParticipant(
            TrackingEscortRestoreLifecycle escort)
        {
            this.escort = escort
                ?? throw new ArgumentNullException(nameof(escort));
        }

        public string ParticipantId => "450.test.captivity-projection";

        public void BeginRestoreCandidate()
        {
            publicationPending = false;
        }

        public void PublishRestoreCandidate()
        {
            publicationPending = true;
        }

        public void RollbackPublishedRestoreCandidate()
        {
            publicationPending = false;
        }

        public void CompleteRestoreCandidate()
        {
            if (!publicationPending)
            {
                throw new InvalidOperationException(
                    "Escort projection completion had no publication.");
            }
            escort.ClearTransientState();
            publicationPending = false;
        }

        public void DiscardRestoreCandidate()
        {
            publicationPending = false;
        }
    }

    private sealed class LateFailingRestoreParticipant :
        IDungeonRestoreTransactionParticipant
    {
        public bool ThrowOnPublish { get; set; } = true;

        public string ParticipantId => "999.test.late-failure";

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException(
                    "Intentional late participant publication failure.");
            }
        }

        public void RollbackPublishedRestoreCandidate()
        {
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private static void Run(
        string name,
        Func<string> scenario,
        ICollection<string> lines,
        ICollection<string> errors)
    {
        try
        {
            string details = scenario();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string message = $"{name}: {exception.Message}";
            lines.Add($"{name}\tFAIL\t{exception.Message}");
            errors.Add(message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
