#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

/// <summary>
/// A bounded live-world adapter for the existing C# catalog/Python plan protocol.
/// The macros issue existing live commands and capture actual requests; this is
/// not the synchronous fixture executor and does not claim complete ancestry.
/// </summary>
internal sealed class LiveNarrativeCausalPlan
{
    internal const string ActionId = "live.training-to-skill-trait-requests";
    internal const string TemplateId = "live-training-request-pair-v1";
    internal const string EquipmentActionId = "live.combat-to-equipment-request";
    internal const string FacilityActionId = "live.facility-visit-to-evolution-request";
    private const string RequestScope = "same-world executed request macro and actual captured request identity; not complete historical request ancestry";
    internal const string PlanFile = "live-input-plan.json";
    internal const string CatalogFile = "live-input-catalog.json";
    internal const string ExecutionFile = "live-plan-execution.json";
    internal static readonly string[] Files = { PlanFile, CatalogFile, ExecutionFile };
    private readonly CausalGameplayPlan plan;
    private readonly JObject before;
    private readonly string output;
    private bool completed;

    private LiveNarrativeCausalPlan(CausalGameplayPlan plan, JObject before, string output)
    {
        this.plan = plan;
        this.before = (JObject)before.DeepClone();
        this.output = output;
    }

    internal static JObject BuildCatalog(string version, string commit)
    {
        JObject catalog = new()
        {
        ["schemaVersion"] = 4,
        ["schema"] = "dungeonstory-causal-gameplay-action-catalog@4",
        ["kind"] = "unity-causal-gameplay-action-catalog",
        ["version"] = version,
        ["catalogVersion"] = "causal-actions-v4",
        ["gameCommit"] = commit,
        ["authority"] = "unity-csharp-authoritative-action-catalog",
        ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
        ["subjectRoles"] = new JArray(new JObject
        {
            ["roleId"] = "role:live-trainee", ["kind"] = "character",
            ["binding"] = "fixture-owned", ["profile"] = "authored-live-world-selected-actor"
        }),
        ["stateKeys"] = new JArray(
            Key("actor.alive", "bool"), Key("actor.level", "number"),
            Key("actor.traitScore", "number"), Key("actor.nextSkillUnlock", "number"),
            Key("request.skillCaptured", "bool"), Key("request.traitCaptured", "bool")),
        ["actions"] = new JArray(new JObject
        {
            ["actionId"] = ActionId, ["domainId"] = "live-training",
            ["productionEntry"] = "DungeonLiveNarrativeCampaignRunner.RunTrainingRequests",
            ["argumentSchema"] = new JObject(),
            ["outcomeKind"] = "Service", ["outcomeTypeId"] = "service.facility-visited",
            ["observedStateKeys"] = new JArray("actor.alive", "actor.level", "actor.traitScore",
                "actor.nextSkillUnlock", "request.skillCaptured", "request.traitCaptured"),
            ["subjectRoleIds"] = new JArray("role:live-trainee"),
            ["narrativeFunctionIds"] = new JArray("experience-before-skill-and-trait-requests"),
            ["maxUsesPerPlan"] = 1, ["hypotheticalEffectsAllowed"] = false,
            ["companionOutcomes"] = new JArray()
        }),
        ["scenarioTemplates"] = new JArray(new JObject
        {
            ["templateId"] = TemplateId,
            ["narrativeScope"] = "Bounded real training until the next skill gate and first trait request; no model response or fabricated XP",
            ["requiredActionIds"] = new JArray(ActionId), ["optionalActionIds"] = new JArray(),
            ["edges"] = new JArray(),
            ["initialGuards"] = new JArray(Guard("actor.alive", "equal", true),
                Guard("actor.traitScore", "less", 3),
                Guard("request.skillCaptured", "equal", false),
                Guard("request.traitCaptured", "equal", false))
        })
        };
        var roles = (JArray)catalog["subjectRoles"];
        foreach (string kind in new[] { "equipment", "facility" })
            roles.Add(new JObject
            {
                ["roleId"] = "role:live-" + kind, ["kind"] = kind == "equipment" ? "item" : kind,
                ["binding"] = "fixture-owned", ["profile"] = "authored-live-world-selected-" + kind
            });
        var keys = (JArray)catalog["stateKeys"];
        keys.Add(Key("equipment.affinity", "number"));
        keys.Add(Key("request.equipmentCaptured", "bool"));
        keys.Add(Key("facility.exists", "bool"));
        keys.Add(Key("facility.visitsObserved", "number"));
        keys.Add(Key("request.facilityPending", "bool"));
        keys.Add(Key("request.facilityCaptured", "bool"));
        ((JArray)catalog["actions"]).Add(RequestAction(EquipmentActionId, "live-combat",
            "RunEquipmentWitness", "Combat", "combat.attack-resolved",
            new JArray("actor.alive", "equipment.affinity", "request.equipmentCaptured"),
            new JArray("role:live-trainee", "role:live-equipment"), "experience-before-equipment-request"));
        ((JArray)catalog["actions"]).Add(RequestAction(FacilityActionId, "live-facility",
            "RunFacilityWitness", "Service", "service.facility-visited",
            new JArray("facility.exists", "facility.visitsObserved", "request.facilityPending", "request.facilityCaptured"),
            new JArray("role:live-facility"), "experience-before-facility-request"));
        ((JArray)catalog["scenarioTemplates"]).Add(RequestTemplate("live-equipment-request-v1", EquipmentActionId,
            "Real public combat with explicit equipment/ammunition and durable-target test preparation until a new equipment request",
            Guard("actor.alive", "equal", true), Guard("equipment.affinity", "equal", 0),
            Guard("request.equipmentCaptured", "equal", false)));
        ((JArray)catalog["scenarioTemplates"]).Add(RequestTemplate("live-facility-request-v1", FacilityActionId,
            "Real facility use and authorized evolution selection with explicit non-target furnishing preparation; no invented usage",
            Guard("facility.exists", "equal", true), Guard("facility.visitsObserved", "equal", 0),
            Guard("request.facilityPending", "equal", false), Guard("request.facilityCaptured", "equal", false)));
        return catalog;
    }

    private static JObject RequestAction(string id, string domain, string entry, string kind,
        string outcome, JArray keys, JArray roles, string function) => new()
    {
        ["actionId"] = id, ["domainId"] = domain,
        ["productionEntry"] = "DungeonLiveNarrativeCampaignRunner." + entry,
        ["argumentSchema"] = new JObject(), ["outcomeKind"] = kind, ["outcomeTypeId"] = outcome,
        ["observedStateKeys"] = keys, ["subjectRoleIds"] = roles,
        ["narrativeFunctionIds"] = new JArray(function), ["maxUsesPerPlan"] = 1,
        ["hypotheticalEffectsAllowed"] = false, ["companionOutcomes"] = new JArray()
    };

    private static JObject RequestTemplate(string id, string action, string scope, params JObject[] guards) => new()
    {
        ["templateId"] = id, ["narrativeScope"] = scope,
        ["requiredActionIds"] = new JArray(action), ["optionalActionIds"] = new JArray(),
        ["edges"] = new JArray(), ["initialGuards"] = new JArray(guards)
    };

    private static JObject Key(string id, string type) => new()
    {
        ["id"] = id, ["type"] = type, ["authority"] = "observed-from-gameplay-owner"
    };

    private static JObject Guard(string key, string comparison, object expected) => new()
    {
        ["sourceNodeId"] = "initial", ["stateKey"] = key,
        ["operator"] = comparison, ["expected"] = JToken.FromObject(expected)
    };

    internal static bool Requested => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_CAUSAL_PLAN_PATH"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_CAUSAL_CATALOG_PATH"));

    internal static LiveNarrativeCausalPlan Begin(string output, JObject before, string expectedAction = ActionId)
    {
        string planSource = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_CAUSAL_PLAN_PATH");
        string catalogSource = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_CAUSAL_CATALOG_PATH");
        Require(!string.IsNullOrWhiteSpace(planSource) && !string.IsNullOrWhiteSpace(catalogSource),
            "Live plan and catalog must be supplied together.");
        File.Copy(planSource, Path.Combine(output, PlanFile), overwrite: false);
        File.Copy(catalogSource, Path.Combine(output, CatalogFile), overwrite: false);
        CausalGameplayPlan plan = ProductionCausalPlanBatchExporter.LoadValidatedPlan(
            Path.Combine(output, PlanFile), Path.Combine(output, CatalogFile), out JObject catalog);
        Require(JToken.DeepEquals(catalog, BuildCatalog((string)catalog["version"], (string)catalog["gameCommit"])),
            "Live catalog differs from the current C# action contract.");
        string expectedTemplate = expectedAction switch
        {
            ActionId => TemplateId,
            EquipmentActionId => "live-equipment-request-v1",
            FacilityActionId => "live-facility-request-v1",
            _ => throw new InvalidOperationException("No live adapter for the requested action.")
        };
        Require(plan.TemplateId == expectedTemplate && plan.Nodes.Count == 1
            && plan.Nodes[0].ActionId == expectedAction && plan.Nodes[0].Arguments.Count == 0,
            "Unsupported live action or arguments; no gameplay was executed.");
        Require(expectedAction != ActionId || (int)before["actor.nextSkillUnlock"] > (int)before["actor.level"],
            "Live plan has no upcoming actual skill gate.");
        Require(!CausalGameplayPlanExecutor.Evaluate(plan.Nodes[0].Guards, before).Any(),
            "Live plan guard mismatch before gameplay.");
        return new LiveNarrativeCausalPlan(plan, before, output);
    }

    internal string SelectedActionId => plan.Nodes[0].ActionId;

    internal void Complete(JObject after, string ownerId, int commandCount, JArray requests)
    {
        Require(SelectedActionId == ActionId && !completed && commandCount > 0 && commandCount < 16,
            "Live plan must complete once within the existing training command bound.");
        Require((bool)after["request.skillCaptured"] && (bool)after["request.traitCaptured"]
            && (int)after["actor.level"] >= (int)before["actor.nextSkillUnlock"]
            && !CausalGameplayPlanExecutor.Evaluate(plan.Nodes[0].ExpectedState, after).Any(),
            "Live action did not reach its actual request gates or planned expected state.");
        Require(requests.Count == 2 && requests.OfType<JObject>().All(value => (string)value["ownerId"] == ownerId),
            "Live request pair must belong to the same actual trainee.");
        var commands = new JArray();
        for (int i = 1; i <= commandCount; i++)
        {
            string name = $"command-{i:D3}.json";
            string path = Path.Combine(output, name);
            JObject command = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            Require((string)command["actorId"] == ownerId
                && (string)command["command"] == "training-facility.interact"
                && (int)command["usesAfter"] == (int)command["usesBefore"] + 1
                && ((JArray)command["publishedOutcomes"]).OfType<JObject>().Any(value =>
                    (string)value["outcomeTypeId"] == "service.facility-visited"),
                "Live command is not a completed actual training visit: " + name);
            commands.Add(new JObject { ["path"] = name, ["sha256"] = FileHash(path) });
        }
        JObject receipt = new()
        {
            ["schema"] = "dungeonstory-live-causal-plan-execution@1", ["status"] = "completed",
            ["planId"] = plan.PlanId, ["planSha256"] = plan.PlanSha256,
            ["catalogSha256"] = plan.CatalogSha256, ["templateId"] = plan.TemplateId,
            ["nodeId"] = plan.Nodes[0].NodeId, ["actionId"] = SelectedActionId,
            ["ownerId"] = ownerId, ["before"] = before, ["after"] = after.DeepClone(),
            ["commands"] = commands, ["requests"] = requests,
            ["scope"] = "same-world executed training macro and actual captured request identities; not complete historical request ancestry",
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        };
        using (var stream = new FileStream(Path.Combine(output, ExecutionFile), FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(receipt.ToString());
        completed = true;
    }

    internal void CompleteRequest(JObject after, JObject request, params string[] commandFiles)
    {
        Require(!completed && (SelectedActionId == EquipmentActionId || SelectedActionId == FacilityActionId),
            "The live single-request action must complete once.");
        Require(!CausalGameplayPlanExecutor.Evaluate(plan.Nodes[0].ExpectedState, after).Any(),
            "Live action did not reach the planned expected state.");
        if (SelectedActionId == EquipmentActionId)
            Require((bool)after["actor.alive"] && (int)after["equipment.affinity"] >= 30
                && (bool)after["request.equipmentCaptured"] && commandFiles.SequenceEqual(new[] { "equipment-command-witness.json" }),
                "Actual equipment request was not reached.");
        else
            Require((bool)after["facility.exists"] && (int)after["facility.visitsObserved"] > 0
                && (bool)after["request.facilityPending"] && (bool)after["request.facilityCaptured"]
                && commandFiles.Length > 1 && commandFiles.Last() == "live-facility-evolution-command.json",
                "Actual facility visit and new request were not reached.");
        Require(commandFiles.Distinct(StringComparer.Ordinal).Count() == commandFiles.Length
            && commandFiles.All(name => Path.GetFileName(name) == name), "Invalid live command file set.");
        JObject receipt = new()
        {
            ["schema"] = "dungeonstory-live-causal-plan-execution@2", ["status"] = "completed",
            ["planId"] = plan.PlanId, ["planSha256"] = plan.PlanSha256,
            ["catalogSha256"] = plan.CatalogSha256, ["templateId"] = plan.TemplateId,
            ["nodeId"] = plan.Nodes[0].NodeId, ["actionId"] = SelectedActionId,
            ["ownerId"] = request["ownerId"], ["targetId"] = request["targetId"],
            ["before"] = before, ["after"] = after.DeepClone(),
            ["commands"] = new JArray(commandFiles.Select(name => new JObject
                { ["path"] = name, ["sha256"] = FileHash(Path.Combine(output, name)) })),
            ["requests"] = new JArray(request), ["scope"] = RequestScope,
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        };
        using (var stream = new FileStream(Path.Combine(output, ExecutionFile), FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(receipt.ToString());
        completed = true;
    }

    internal static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var hash = SHA256.Create();
        return "sha256:" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    internal static void VerifyCapturedRequests(string output)
    {
        JObject receipt = JObject.Parse(File.ReadAllText(Path.Combine(output, ExecutionFile), Encoding.UTF8));
        Require((string)receipt["status"] == "completed", "Live plan is incomplete.");
        var requests = (JArray)receipt["requests"];
        string[] witnessFiles = (string)receipt["actionId"] switch
        {
            ActionId => new[] { "dispatch-witness.json", "trait-dispatch-witness.json" },
            EquipmentActionId => new[] { "equipment-dispatch-witness.json" },
            FacilityActionId => new[] { "facility-dispatch-witness.json" },
            _ => throw new InvalidOperationException("Unknown completed live action.")
        };
        Require(requests.Count == witnessFiles.Length, "Live plan request set is incomplete.");
        foreach (string file in witnessFiles)
        {
            JObject witness = JObject.Parse(File.ReadAllText(Path.Combine(output, file), Encoding.UTF8));
            JObject request = requests.OfType<JObject>().Single(value =>
                (string)value["profileId"] == (string)witness["profileId"]);
            foreach (string key in new[] { "profileId", "correlationId", "wireSha256", "checkpointSha256" })
                Require(JToken.DeepEquals(request[key], witness[key]), "Live receipt/request mismatch: " + key);
            Require(JToken.DeepEquals(request["ownerId"], witness["dispatchEligibility"]["ownerId"]),
                "Live receipt belongs to a different owner.");
            if ((string)receipt["schema"] == "dungeonstory-live-causal-plan-execution@2")
                Require(JToken.DeepEquals(request["targetId"], witness["dispatchEligibility"]["targetId"])
                    && JToken.DeepEquals(receipt["ownerId"], request["ownerId"])
                    && JToken.DeepEquals(receipt["targetId"], request["targetId"]),
                    "Live receipt belongs to a different equipment/facility target.");
            Require((string)request["requestId"] == (string)witness["dispatchEligibility"]["requestId"],
                "Live receipt refers to a different actual pending request.");
        }
        foreach (JObject command in ((JArray)receipt["commands"]).OfType<JObject>())
            Require((string)command["sha256"] == FileHash(Path.Combine(output, (string)command["path"])),
                "Live plan command changed after completion.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
