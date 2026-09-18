#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Root-owned causal/save witness; no authored asset or production state edits.
public static class Wim040ObservedIncidentDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-040-observed-incident-focused.txt";

    public static string RunAll()
    {
        RunEligibilityFocused();
        var lines = File.ReadAllLines(ReportPath).ToList();
        if (lines[0] != "result=PASS") return string.Join("\n", lines);
        lines.RemoveAt(0);
        try
        {
            var catalog = new V20StoryContentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            RunNonIncidentJsonRoundTrip(catalog, lines);
            RunCaptureAndRestore(catalog, lines);
            RunShopliftingReceiptFreeze(lines);
            RunShopliftingCaptureAndRestore(catalog, lines);
            RunBrawlCaptureAndRestore(catalog, lines);
            lines.Insert(0, "result=PASS");
        }
        catch (Exception error)
        {
            lines.Insert(0, "result=FAIL");
            lines.Add(error.ToString());
        }
        lines.Add("scope=controlled immutable observed inputs into production command/current Society state; not proof of actual Customer meal/combat callbacks, exit behavior, whole-world restore or medical treatment");
        File.WriteAllLines(ReportPath, lines);
        return string.Join("\n", lines);
    }

    private static void RunNonIncidentJsonRoundTrip(V20StoryContentCatalog catalog, List<string> lines)
    {
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var create = typeof(V20CampaignRuntime).GetMethod("CreateEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(create != null, "Existing occurrence producer missing.");
        var context = new V20DailyEventContext { AbsoluteDay = 42, RunSeed = 157181, Season = Season.Spring };
        var seasonal = new SeasonalEventWorldSaveData();
        seasonal.activeEvents.Add((V20ActiveEventSaveData)create.Invoke(campaign,
            new object[] { catalog.SeasonalEvents.First().StableId, context, 3, Array.Empty<string>(), null }));
        var seasonJson = JsonUtility.ToJson(seasonal);
        campaign.PublishSeasonal(campaign.PrepareSeasonal(JsonUtility.FromJson<SeasonalEventWorldSaveData>(seasonJson)));
        Require(campaign.ActiveSeasonalEvents.Count == 1 && !campaign.ActiveSeasonalEvents.Single().hasObservedMealIncident,
            "Valid seasonal JSON was rejected as a meal incident.");
        var guest = catalog.GuestRequests.First();
        var society = new SocietyEventWorldSaveData();
        society.activeEvents.Add((V20ActiveEventSaveData)create.Invoke(campaign,
            new object[] { guest.StableId, context, 3, Array.Empty<string>(), guest }));
        campaign.PublishSociety(campaign.PrepareSociety(JsonUtility.FromJson<SocietyEventWorldSaveData>(JsonUtility.ToJson(society))));
        Require(campaign.ActiveSocietyEvents.Single().definitionId == guest.StableId
            && !campaign.ActiveSocietyEvents.Single().hasObservedMealIncident
            && !campaign.GetOwnedCustomerIds().Any(), "Valid guest JSON acquired a fabricated incident owner.");
        var malformed = JsonUtility.FromJson<SeasonalEventWorldSaveData>(seasonJson);
        malformed.activeEvents[0].observedMealIncident = new ObservedMealIncidentEvidenceSaveData
        { operationId = "consumable-operation:wim040:unexpected" };
        MustReject(() => campaign.PrepareSeasonal(malformed), "Populated evidence on a non-incident was silently ignored.");
        lines.Add("PASS actual seasonal/guest occurrence JSON round-trip keeps absent evidence absent; populated unexpected evidence rejected");
    }

    private static void RunCaptureAndRestore(V20StoryContentCatalog catalog, List<string> lines)
    {
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var command = (ISocietyObservedMealIncidentCommand)campaign;
        var owned = (ISocietyIncidentOwnedCharacterIdQuery)campaign;
        var context = new V20DailyEventContext { AbsoluteDay = 42, RunSeed = 157181, Season = Season.Spring };
        context.ParticipantCharacterIds.Add("character:wim040:unrelated-staff");
        string original = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedMealIncident(Evidence("none", violation: false), context).Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && command.CaptureObservedMealIncident(Evidence("dead", dead: true), context).Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && JsonUtility.ToJson(campaign.CaptureSociety()) == original,
            "Absent risk/dead target created an occurrence or changed history.");
        lines.Add("PASS no observed violation/collapse and already-dead target are no-op, not cadence fillers");

        var evidence = Evidence("first");
        var created = command.CaptureObservedMealIncident(evidence, context);
        Require(created.Created && created.IncidentKind == ServiceIncidentKind.ForbiddenMeal,
            "Observed dietary violation did not create its specific incident.");
        var occurrence = campaign.ActiveSocietyEvents.Single();
        var frozen = occurrence.observedMealIncident;
        Require(occurrence.instanceId == created.OccurrenceInstanceId
            && occurrence.participantCharacterIds.SequenceEqual(new[] { evidence.TargetCharacterId.Value })
            && occurrence.hasObservedMealIncident && frozen != null && frozen.operationId == evidence.OperationId.Value
            && frozen.targetCharacterId == evidence.TargetCharacterId.Value
            && frozen.itemDefinitionId == "food:lavish-meat" && frozen.itemStackId == evidence.ItemStackId.Value
            && frozen.fieldMeal && frozen.facilityInstanceId == string.Empty
            && frozen.locationX == 1 && frozen.locationY == 0 && frozen.absoluteDay == 42
            && frozen.observedPolicyViolation && !frozen.observedDowned && !frozen.observedDead
            && !string.IsNullOrWhiteSpace(frozen.observedCause) && !frozen.observedCause.Contains("조리표"),
            "Actual target/frozen cause identity changed or unobserved cooking-error cause was invented.");
        string afterCreate = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedMealIncident(evidence, context).Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && command.CaptureObservedMealIncident(Evidence("first", target: "character:wim040:other-customer"), context).Disposition
                == ObservedMealIncidentCaptureDisposition.RejectedPayloadConflict
            && JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate,
            "Same operation replay/conflicting target was not idempotent and fail-closed.");
        lines.Add("PASS exact captured target/meal/field location; exact replay no-op; changed target rejected without state mutation");

        campaign.PublishSociety(campaign.PrepareSociety(JsonUtility.FromJson<SocietyEventWorldSaveData>(afterCreate)));
        Require(JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate
            && owned.GetOwnedCustomerIds().Select(x => x.Value).SequenceEqual(new[] { evidence.TargetCharacterId.Value }),
            "Current JSON restore changed frozen evidence or exact active target ownership.");
        var malformed = JsonUtility.FromJson<SocietyEventWorldSaveData>(afterCreate);
        malformed.activeEvents[0].observedMealIncident = null;
        MustReject(() => campaign.PrepareSociety(malformed), "Missing active incident evidence accepted.");
        malformed = JsonUtility.FromJson<SocietyEventWorldSaveData>(afterCreate);
        malformed.successfulIncidentOperations.Add(malformed.successfulIncidentOperations[0]);
        MustReject(() => campaign.PrepareSociety(malformed), "Duplicate successful operation accepted.");
        Require(JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate, "Failed restore candidate changed live state.");
        lines.Add("PASS current JSON preserves frozen evidence/owner; missing evidence and duplicate operation reject atomically");

        Require(command.TryResolveObservedMealIncidentTargetDeath(evidence.TargetCharacterId, 42, out var terminal, out _)
            && terminal.Effects.Count == 0 && !owned.GetOwnedCustomerIds().Any(),
            "Target death did not release incident ownership without fictional treatment/effects.");
        var pruned = campaign.CaptureSociety();
        pruned.recentResolvedEvents.Clear();
        campaign.PublishSociety(campaign.PrepareSociety(pruned));
        string beforeReplay = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedMealIncident(evidence, context).Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && JsonUtility.ToJson(campaign.CaptureSociety()) == beforeReplay,
            "Pruning resolved UI history reopened the same consumed operation.");
        lines.Add("PASS death terminal releases owner without effects; durable operation replay remains blocked after recent-history pruning");

        var medical = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var medicalResult = ((ISocietyObservedMealIncidentCommand)medical)
            .CaptureObservedMealIncident(Evidence("both", downed: true), context);
        Require(medicalResult.Created && medicalResult.IncidentKind == ServiceIncidentKind.MedicalCollapse
            && medical.ActiveSocietyEvents.Count == 1
            && medical.CaptureSociety().successfulIncidentOperations.Count == 1
            && medical.ActiveSocietyEvents.Single().observedMealIncident.observedPolicyViolation
            && medical.ActiveSocietyEvents.Single().observedMealIncident.observedDowned
            && medical.ActiveSocietyEvents.Single().observedMealIncident.operationId == "consumable-operation:wim040:both"
            && medical.ActiveSocietyEvents.Single().observedMealIncident.observedCause.Contains("쓰러")
            && !medical.ActiveSocietyEvents.Single().observedMealIncident.observedCause.Contains("식중독"),
            "Downed priority duplicated one meal or invented a food-poisoning diagnosis.");
        var blocked = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var blockedState = new SocietyEventWorldSaveData();
        blockedState.cooldowns.Add(new V20EventCooldownSaveData
        { definitionId = catalog.ServiceIncidents.Single(x => x.kind == ServiceIncidentKind.ForbiddenMeal).StableId, availableAbsoluteDay = 999 });
        blocked.PublishSociety(blocked.PrepareSociety(blockedState));
        string beforeBlocked = JsonUtility.ToJson(blocked.CaptureSociety());
        Require(((ISocietyObservedMealIncidentCommand)blocked).CaptureObservedMealIncident(Evidence("blocked"), context).Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && JsonUtility.ToJson(blocked.CaptureSociety()) == beforeBlocked,
            "Existing cooldown was bypassed or rejected event became a delayed queue.");
        lines.Add("PASS both observed risks create medical incident only; no diagnosis invention; existing cooldown remains no-op without pending queue");
    }

    private static void RunShopliftingCaptureAndRestore(
        V20StoryContentCatalog catalog,
        List<string> lines)
    {
        var campaign = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        var command = (ISocietyObservedMealIncidentCommand)campaign;
        var owned = (ISocietyIncidentOwnedCharacterIdQuery)campaign;
        var context = new V20DailyEventContext
        {
            AbsoluteDay = 42,
            RunSeed = 157181,
            Season = Season.Spring
        };
        var evidence = ShopliftingEvidence();
        string original = JsonUtility.ToJson(campaign.CaptureSociety());
        var malformed = ShopliftingEvidence(
            operationId: "shoplifting-commit:wim040:malformed",
            quantity: 2);
        Require(command.CaptureObservedShopliftingIncident(malformed, context)
                .Disposition == ObservedMealIncidentCaptureDisposition.NotEligible
            && JsonUtility.ToJson(campaign.CaptureSociety()) == original,
            "A non-exact shoplifting lot created or queued an incident.");

        var created = command.CaptureObservedShopliftingIncident(
            evidence,
            context);
        Require(created.Created
            && created.IncidentKind == ServiceIncidentKind.Theft,
            "The exact committed shop lot did not create Theft.");
        var occurrence = campaign.ActiveSocietyEvents.Single();
        var frozen = occurrence.observedMealIncident;
        Require(occurrence.participantCharacterIds.SequenceEqual(
                new[] { evidence.TargetCharacterId.Value })
            && occurrence.hasObservedMealIncident
            && frozen != null
            && frozen.sourceKind == ObservedIncidentSourceKind.Shoplifting
            && frozen.operationId == evidence.OperationId
            && frozen.sourceOperationId == evidence.SourceOperationId
            && frozen.targetCharacterId == evidence.TargetCharacterId.Value
            && frozen.itemDefinitionId == evidence.ItemDefinitionId.Value
            && frozen.saleItemId == evidence.SaleItemId
            && frozen.sourceStackId == evidence.SourceStackId
            && frozen.quantity == 1
            && frozen.unitMassGrams == 750L
            && frozen.facilityInstanceId == evidence.FacilityInstanceId.Value
            && frozen.locationX == 4
            && frozen.locationY == 2
            && frozen.lossValue == 70
            && frozen.observedCause.Contains("실제 절도 커밋"),
            "Theft did not freeze its exact operation, lot, target, and facility facts.");

        string afterCreate = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedShopliftingIncident(evidence, context)
                .Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && command.CaptureObservedShopliftingIncident(
                    ShopliftingEvidence(target: "character:wim040:other-thief"),
                    context).Disposition
                == ObservedMealIncidentCaptureDisposition.RejectedPayloadConflict
            && JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate,
            "Theft operation replay or conflicting target was not fail-closed.");

        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(afterCreate)));
        Require(JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate
            && owned.GetOwnedCustomerIds().Select(value => value.Value)
                .SequenceEqual(new[] { evidence.TargetCharacterId.Value }),
            "Theft JSON restore changed frozen evidence or target ownership.");
        Require(command.TryResolveObservedMealIncidentTargetDeath(
                evidence.TargetCharacterId,
                42,
                out var terminal,
                out _)
            && terminal.Effects.Count == 0
            && !owned.GetOwnedCustomerIds().Any(),
            "Theft target death did not release ownership without effects.");
        string afterDeath = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedShopliftingIncident(evidence, context)
                .Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && JsonUtility.ToJson(campaign.CaptureSociety()) == afterDeath,
            "Theft target death reopened its already committed operation.");
        lines.Add("PASS exact committed shop lot creates Theft only; frozen lot/target/facility survives JSON; replay/conflict/death lifetime remain fail-closed");
    }

    private static void RunShopliftingReceiptFreeze(List<string> lines)
    {
        var source = new RetailStockLotSnapshot
        {
            saleItemId = 7,
            itemDefinitionId = "resource:iron-ore",
            sourceStackId = "stack:wim040:retail-source",
            quantity = 1,
            unitMassGrams = 750L,
            sourceOperationId = "retail-source:wim040:exact-lot"
        };
        const string operationId = "shoplifting-commit:wim040:exact-unit";
        var committed = new FacilityCrimeEvent(
            null,
            null,
            FacilityCrimeKind.Shoplifting,
            "committed",
            70,
            source,
            operationId);
        source.quantity = 9;
        source.sourceOperationId = "retail-source:wim040:mutated";
        Require(committed.commitOperationId == operationId
            && committed.committedLot != null
            && !ReferenceEquals(source, committed.committedLot)
            && committed.committedLot.quantity == 1
            && committed.committedLot.sourceOperationId
                == "retail-source:wim040:exact-lot",
            "Facility crime receipt did not freeze the exact committed lot and operation.");
        lines.Add("PASS FacilityCrimeEvent freezes its exact post-Sink shoplifting lot and unit operation receipt");
    }

    private static void RunBrawlCaptureAndRestore(
        V20StoryContentCatalog catalog,
        List<string> lines)
    {
        var campaign = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        var command = (ISocietyObservedMealIncidentCommand)campaign;
        var owned = (ISocietyIncidentOwnedCharacterIdQuery)campaign;
        var context = new V20DailyEventContext
        {
            AbsoluteDay = 42,
            RunSeed = 157181,
            Season = Season.Spring
        };
        var evidence = BrawlEvidence();
        string original = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedBrawlIncident(
                    BrawlEvidence(actualDamage: 0f),
                    context).Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && command.CaptureObservedBrawlIncident(
                    BrawlEvidence(other: "character:wim040:brawl-customer"),
                    context).Disposition
                == ObservedMealIncidentCaptureDisposition.NotEligible
            && JsonUtility.ToJson(campaign.CaptureSociety()) == original,
            "Non-positive or self Brawl evidence changed Society state.");

        var created = command.CaptureObservedBrawlIncident(evidence, context);
        Require(created.Created
            && created.IncidentKind == ServiceIncidentKind.Brawl,
            "Exact committed combat evidence did not create Brawl.");
        var occurrence = campaign.ActiveSocietyEvents.Single();
        var frozen = occurrence.observedMealIncident;
        Require(occurrence.participantCharacterIds.SequenceEqual(
                new[] { evidence.TargetCharacterId.Value })
            && frozen.sourceKind == ObservedIncidentSourceKind.Combat
            && frozen.operationId == evidence.AttackOperationId
            && frozen.sourceOperationId == evidence.AttackOperationId
            && frozen.targetCharacterId == evidence.TargetCharacterId.Value
            && frozen.otherParticipantCharacterId
                == evidence.OtherParticipantCharacterId.Value
            && frozen.customerWasAttacker == evidence.CustomerWasAttacker
            && Mathf.Approximately(frozen.actualDamage, evidence.ActualDamage)
            && frozen.facilityInstanceId == evidence.FacilityInstanceId.Value
            && frozen.locationX == 5
            && frozen.locationY == 3
            && frozen.observedCause.Contains("실제 전투 피해"),
            "Brawl did not freeze attack, participants, damage, and facility receipt.");

        string afterCreate = JsonUtility.ToJson(campaign.CaptureSociety());
        Require(command.CaptureObservedBrawlIncident(evidence, context)
                .Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && command.CaptureObservedBrawlIncident(
                    BrawlEvidence(actualDamage: 3f),
                    context).Disposition
                == ObservedMealIncidentCaptureDisposition
                    .RejectedPayloadConflict
            && JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate,
            "Brawl replay or changed damage payload was not fail-closed.");

        campaign.PublishSociety(campaign.PrepareSociety(
            JsonUtility.FromJson<SocietyEventWorldSaveData>(afterCreate)));
        Require(JsonUtility.ToJson(campaign.CaptureSociety()) == afterCreate
            && owned.GetOwnedCustomerIds().Select(value => value.Value)
                .SequenceEqual(new[] { evidence.TargetCharacterId.Value }),
            "Brawl JSON restore changed evidence or Customer ownership.");
        lines.Add("PASS committed Brawl evidence freezes attack/damage/participants/facility; current JSON and replay/conflict boundaries hold");
    }

    private static ObservedMealIncidentSnapshot Evidence(string suffix, bool violation = true, bool downed = false,
        bool dead = false, string target = "character:wim040:customer") => new(
        new ConsumableOperationId("consumable-operation:wim040:" + suffix), new CharacterId(target),
        new ItemDefinitionId("food:lavish-meat"), new ItemStackId("stack:wim040:meal"),
        default, true, new CoreGridCell(1, 0),
        42, violation, downed, dead);

    private static ObservedShopliftingIncidentSnapshot ShopliftingEvidence(
        string operationId = "shoplifting-commit:wim040:exact-unit",
        int quantity = 1,
        string target = "character:wim040:thief") => new(
        operationId,
        "retail-source:wim040:exact-lot",
        new CharacterId(target),
        new ItemDefinitionId("resource:iron-ore"),
        7,
        string.Empty,
        "stack:wim040:retail-source",
        quantity,
        750L,
        string.Empty,
        new BuildingInstanceId("building:wim040:shop"),
        new CoreGridCell(4, 2),
        42,
        70);

    private static ObservedBrawlIncidentSnapshot BrawlEvidence(
        float actualDamage = 2.5f,
        string other = "character:wim040:brawl-staff") => new(
        "combat-command:wim040:brawl:7",
        new CharacterId("character:wim040:brawl-customer"),
        new CharacterId(other),
        true,
        actualDamage,
        new BuildingInstanceId("building:wim040:visitor-service"),
        new CoreGridCell(5, 3),
        42);

    private static void MustReject(Action action, string reason)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(reason);
    }

    public static string RunEligibilityFocused()
    {
        var lines = new List<string>();
        try
        {
            var source = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var catalog = new V20StoryContentCatalog(source);
            Require(catalog.ServiceIncidents.Count == 8, "Actual eight-incident catalog changed.");
            foreach (int seed in new[] { 157181, 157182, 157183 })
            {
                var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
                // Suppress other authored categories through the real cooldown
                // authority. Empty incident observations must not fill their gap.
                var state = new SocietyEventWorldSaveData { lastEvaluationAbsoluteDay = 41 };
                foreach (var definition in catalog.LifeEvents.Cast<V20AuthoredContentSO>().Concat(catalog.GuestRequests))
                    state.cooldowns.Add(new V20EventCooldownSaveData
                    { definitionId = definition.StableId, availableAbsoluteDay = 999 });
                campaign.PublishSociety(campaign.PrepareSociety(state));
                var context = new V20DailyEventContext { AbsoluteDay = 42, RunSeed = seed, Season = Season.Spring };
                context.ParticipantCharacterIds.Add("character:wim040:unexposed-a");
                context.ParticipantCharacterIds.Add("character:wim040:unexposed-b");
                campaign.EvaluateDaily(context);
                Require(!campaign.ActiveSocietyEvents.Any(x => catalog.ServiceIncidents.Any(d => d.StableId == x.definitionId)),
                    "Evidence-free cadence created an incident for an unexposed participant; seed=" + seed);
                lines.Add("PASS no observed incident generated from ordinary daily cadence; seed=" + seed);
            }
            lines.Insert(0, "result=PASS");
        }
        catch (Exception error)
        {
            lines.Insert(0, "result=FAIL");
            lines.Add(error.ToString());
        }
        lines.Add("scope=local actual catalog/campaign daily eligibility with other definitions on valid cooldown; not physical meal callback, Customer lifetime, whole-world restore, response work or full WIM040 completion");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        return string.Join("\n", lines);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
