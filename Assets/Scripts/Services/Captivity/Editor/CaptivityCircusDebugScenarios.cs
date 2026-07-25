#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Run("interaction_registry_and_materials", VerifyInteractions, lines, errors);
        Run("circus_registry_and_programs", VerifyCircusPrograms, lines, errors);
        Run("circus_and_wildlife_save_clone", VerifyCircusSaveModels, lines, errors);
        Run("captivity_facility_assets", VerifyFacilityAssets, lines, errors);

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
