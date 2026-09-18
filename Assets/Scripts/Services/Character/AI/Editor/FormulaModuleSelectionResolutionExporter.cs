#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Replays an immutable module-selection response set through the same C#
/// validators and formula optimizers used by gameplay. This is review evidence:
/// it is never sent back to the model and never commits gameplay state.
/// </summary>
public static partial class FormulaPresentationPilotExporter
{
    private const string ResolutionExportParent =
        "Artifacts/Exports/FormulaModuleSelectionResolution";

    private sealed class AllocationResidual
    {
        public AllocationResidual(int budget, int calculatedCost, string reason)
        {
            ResidualBudget = budget - calculatedCost;
            Reason = reason ?? string.Empty;
        }

        public int ResidualBudget { get; }
        public string Reason { get; }
    }

    private sealed class ResolvedFormulaAxes
    {
        public ResolvedFormulaAxes(
            NarrativeFormulaCapabilityDescriptor descriptor,
            IEnumerable<(string ParameterId, long Units)> parameters)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Parameters = (parameters ?? Array.Empty<(string ParameterId, long Units)>())
                .OrderBy(value => value.ParameterId, StringComparer.Ordinal).ToArray();
        }

        public NarrativeFormulaCapabilityDescriptor Descriptor { get; }
        public IReadOnlyList<(string ParameterId, long Units)> Parameters { get; }
    }

    public static void ExportResolvedModuleSelection100FromEnvironment()
    {
        string version = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_FORMULA_RESOLUTION_VERSION");
        string source = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_FORMULA_SOURCE_JSONL");
        string responses = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_FORMULA_RESPONSE_JSONL");
        string destination = ExportResolvedModuleSelection100(
            version, source, responses);
        Debug.Log("Formula module-selection resolution exported: " + destination);
    }

    public static string ExportResolvedModuleSelection100(
        string version,
        string sourceJsonlPath,
        string responseJsonlPath)
    {
        string canonicalVersion = RequireVersion(version);
        string sourcePath = Path.GetFullPath(sourceJsonlPath ?? string.Empty);
        string responsePath = Path.GetFullPath(responseJsonlPath ?? string.Empty);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Module-selection source JSONL is missing.", sourcePath);
        if (!File.Exists(responsePath))
            throw new FileNotFoundException("Module-selection response JSONL is missing.", responsePath);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string gameCommit = RequireGitCommit(projectRoot);
        CatalogContext catalogs = LoadAndValidateCatalogs();
        string combinedCatalogSha256 = BuildCombinedCatalogSha256(catalogs);
        IReadOnlyList<JObject> expected = BuildRows(
            catalogs, combinedCatalogSha256, gameCommit, RowsPerProfile, 0);
        ValidateRows(expected, RowsPerProfile);
        JObject[] supplied = ReadJsonl(sourcePath);
        JObject[] responses = ReadJsonl(responsePath);
        if (supplied.Length != expected.Count || responses.Length != expected.Count)
            throw new InvalidOperationException(
                $"Resolution export requires exactly {expected.Count} source and response rows.");
        for (int index = 0; index < expected.Count; index++)
        {
            if (!JToken.DeepEquals(expected[index], supplied[index]))
                throw new InvalidOperationException(
                    $"Source row {index + 1} does not match the current deterministic C# replay.");
        }

        List<JObject> first = BuildResolutionRows(catalogs, supplied, responses);
        List<JObject> second = BuildResolutionRows(catalogs, supplied, responses);
        if (!string.Equals(ToJsonl(first), ToJsonl(second), StringComparison.Ordinal))
            throw new InvalidOperationException("C# allocation replay is not deterministic.");
        ValidateResolutionRows(first, supplied, responses);

        string parent = Path.Combine(projectRoot,
            ResolutionExportParent.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, canonicalVersion);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException(
                "Formula module-selection resolution export is create-only: " + destination);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            const string rowsName = "formula_module_selection_resolution_100.jsonl";
            string rowsPath = Path.Combine(staging, rowsName);
            WriteJsonl(rowsPath, first);
            SourceDigest sourceDigest = CaptureSourceDigest(projectRoot, catalogs);
            string sourceManifestPath = Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? string.Empty, "manifest.json");
            if (!File.Exists(sourceManifestPath))
                throw new FileNotFoundException(
                    "The source pilot manifest is required beside the source JSONL.",
                    sourceManifestPath);
            JObject sourceManifest = JObject.Parse(File.ReadAllText(sourceManifestPath));
            int equipmentFormulaVersion = (int?)sourceManifest[
                "equipmentFormulaVersion"]
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks equipment formula version.");
            JArray equipmentFormulaTargetExclusions = sourceManifest[
                "equipmentFormulaTargetExclusions"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks equipment target exclusions.");
            JArray equipmentFormulaTargetEligibleDefinitionIds = sourceManifest[
                "equipmentFormulaTargetEligibleDefinitionIds"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks equipment target eligibility.");
            JArray equipmentFormulaTargetEnumeration = sourceManifest[
                "equipmentFormulaTargetEnumeration"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks equipment target enumeration.");
            JArray facilitySourceReplayExclusions = sourceManifest[
                "facilitySourceReplayExclusions"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks facility source replay exclusions.");
            JArray facilitySourceReplayEligibleRecipeIds = sourceManifest[
                "facilitySourceReplayEligibleRecipeIds"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks facility source replay eligibility.");
            JArray facilityFormulaRecipeEnumeration = sourceManifest[
                "facilityFormulaRecipeEnumeration"] as JArray
                ?? throw new InvalidOperationException(
                    "Source pilot manifest lacks facility recipe enumeration.");
            JObject manifest = new JObject
            {
                ["schemaVersion"] = 1,
                ["version"] = canonicalVersion,
                ["gameCommit"] = gameCommit,
                ["combinedCatalogSha256"] = combinedCatalogSha256,
                ["sourceDigest"] = sourceDigest.Digest,
                ["workingTreeDirty"] = sourceDigest.WorkingTreeDirty,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["postSelectionCSharpReplay"] = true,
                ["includedInLlmPrompt"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["rowCount"] = first.Count,
                ["profileCounts"] = new JObject(first
                    .GroupBy(row => (string)row["profileId"], StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new JProperty(group.Key, group.Count()))),
                ["strengthBandCounts"] = new JObject(first
                    .GroupBy(row => (string)row["strengthBand"]?["id"], StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new JProperty(group.Key, group.Count()))),
                ["equipmentFormulaVersion"] = equipmentFormulaVersion,
                ["facilitySourceReplayEligibleRecipeIds"] =
                    facilitySourceReplayEligibleRecipeIds.DeepClone(),
                ["facilitySourceReplayExclusions"] =
                    facilitySourceReplayExclusions.DeepClone(),
                ["narrativeBudgetDistribution"] = new JObject(first
                    .GroupBy(row => (string)row["profileId"], StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new JProperty(group.Key, new JObject(group
                        .GroupBy(row => (int)row["csharpAllocation"]?["narrativeBudget"])
                        .OrderBy(budgetGroup => budgetGroup.Key)
                        .Select(budgetGroup => new JProperty(
                            budgetGroup.Key.ToString(CultureInfo.InvariantCulture),
                            budgetGroup.Count())))))),
                ["equipmentFormulaTargetEligibleDefinitionIds"] =
                    equipmentFormulaTargetEligibleDefinitionIds.DeepClone(),
                ["equipmentFormulaTargetExclusions"] =
                    equipmentFormulaTargetExclusions.DeepClone(),
                ["equipmentFormulaTargetEnumeration"] =
                    equipmentFormulaTargetEnumeration.DeepClone(),
                ["facilityFormulaRecipeEnumeration"] =
                    facilityFormulaRecipeEnumeration.DeepClone(),
                ["sourceRowsSha256"] = Sha256File(sourcePath),
                ["sourcePilotManifestSha256"] = Sha256File(sourceManifestPath),
                ["responseRowsSha256"] = Sha256File(responsePath),
                ["resolutionRowsSha256"] = Sha256File(rowsPath),
                ["sourceFiles"] = new JArray(sourceDigest.Files.Select(value => new JObject
                {
                    ["path"] = value.Path,
                    ["sha256"] = value.Sha256
                }))
            };
            string manifestPath = Path.Combine(staging, "manifest.json");
            File.WriteAllText(manifestPath, CanonicalJson(manifest) + "\n",
                new UTF8Encoding(false));
            JObject delivery = new JObject
            {
                ["schemaVersion"] = 1,
                ["files"] = new JArray(new[] { rowsName, "manifest.json" }.Select(name =>
                    new JObject
                    {
                        ["path"] = name,
                        ["sha256"] = Sha256File(Path.Combine(staging, name))
                    }))
            };
            File.WriteAllText(Path.Combine(staging, "delivery_manifest.json"),
                CanonicalJson(delivery) + "\n", new UTF8Encoding(false));
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    private static List<JObject> BuildResolutionRows(
        CatalogContext catalogs,
        IReadOnlyList<JObject> sources,
        IReadOnlyList<JObject> responses)
    {
        List<JObject> rows = new(sources.Count);
        List<string> failures = new();
        for (int index = 0; index < sources.Count; index++)
        {
            try
            {
                JObject source = sources[index];
                JObject authored = responses[index];
                ValidateResponseBinding(index, source, authored);
                JObject response = RequireObject(authored["response"], "response");
                NarrativeFormulaModuleSelectionChoice choice = new(
                    RequireString(response, "selectionId"),
                    RequireStringArray(response, "positiveModuleIds"),
                    RequireStringArray(response, "drawbackModuleIds"),
                    RequireStringArray(response, "evidenceFactIds"));
                string profile = RequireString(source, "profileId");
                int rowIndex = ParsePilotIndex(RequireString(source, "targetPersistentId"));
                JObject allocation = profile switch
                {
                    "CharacterSkillModuleSelection" => ResolveSkillAllocation(
                        catalogs.Skills, source, choice, rowIndex),
                    "AcquiredTraitModuleSelection" => ResolveTraitAllocation(
                        catalogs.TraitSettings, catalogs.Traits, source, choice, rowIndex),
                    "EquipmentEvolutionModuleSelection" => ResolveEquipmentAllocation(
                        catalogs.Equipment, catalogs.EquipmentDefinitions, source, choice, rowIndex),
                    "FacilityEvolutionModuleSelection" => ResolveFacilityAllocation(
                        catalogs.Facilities, source, choice, rowIndex),
                    _ => throw new InvalidOperationException(
                        "Unknown module-selection profile: " + profile)
                };
                rows.Add(new JObject
                {
                    ["schemaVersion"] = 3,
                    ["ordinal"] = index + 1,
                    ["profileId"] = profile,
                    ["targetPersistentId"] = RequireString(source, "targetPersistentId"),
                    ["strengthBand"] = source["strengthBand"]?.DeepClone(),
                    ["selectionId"] = choice.SelectionId,
                    ["sourceRowHash"] = "sha256:" + Sha256Text(CanonicalJson(source)),
                    ["responseRowHash"] = "sha256:" + Sha256Text(CanonicalJson(authored)),
                    ["selectedPositiveModuleIds"] = new JArray(choice.PositiveModuleIds),
                    ["selectedDrawbackModuleIds"] = new JArray(choice.DrawbackModuleIds),
                    ["selectedEvidenceFactIds"] = new JArray(choice.EvidenceFactIds),
                    ["csharpAllocation"] = allocation
                });
            }
            catch (Exception error)
            {
                JObject source = sources[index];
                JObject authored = responses[index];
                JObject response = authored["response"] as JObject;
                failures.Add(string.Join(" | ",
                    "row=" + (index + 1).ToString(CultureInfo.InvariantCulture),
                    "profile=" + ((string)source["profileId"] ?? "?"),
                    "positive=" + string.Join(",", response?["positiveModuleIds"]
                        ?.Values<string>() ?? Array.Empty<string>()),
                    "drawback=" + string.Join(",", response?["drawbackModuleIds"]
                        ?.Values<string>() ?? Array.Empty<string>()),
                    error.Message));
            }
        }
        if (failures.Count > 0)
            throw new InvalidOperationException(
                "C# rejected " + failures.Count.ToString(CultureInfo.InvariantCulture)
                + " module-selection response rows:\n" + string.Join("\n", failures));
        return rows;
    }

    private static JObject ResolveSkillAllocation(
        CharacterSkillSystemSettingsSO settings,
        JObject source,
        NarrativeFormulaModuleSelectionChoice choice,
        int index)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        JObject request = RequireObject(source["moduleSelectionRequest"],
            "moduleSelectionRequest");
        PilotCharacterOwner owner = RequirePilotOwner(source, request, "skill", index);
        EvidenceBundle evidence = BuildCharacterEvidence(
            "skill", index, policy, StrengthBandFor(index), owner);
        CharacterSkillCandidateRule rule = SkillRuleFromJson(
            RequireObject(source["authoritativeSkillRule"], "authoritativeSkillRule"));
        CharacterSkillKind kind = RequireEnum<CharacterSkillKind>(
            RequireObject(request["skillContext"], "skillContext"), "skillKind");
        ValidateSkillContext(
            RequireObject(request["skillContext"], "skillContext"), rule, kind);
        int computedBudget = NarrativeFormulaCore.CalculateStrength(policy, evidence.Formula).Budget;
        if (rule.budget != computedBudget)
        {
            throw new InvalidOperationException(
                "Authoritative CharacterSkill pilot rule budget does not match its replayed evidence.");
        }
        CharacterSkillModuleRule[] legal = rule.allowedModuleIds.Select(settings.FindModule)
            .Where(value => value != null).OrderBy(value => value.id, StringComparer.Ordinal)
            .ToArray();
        if (legal.Length != rule.allowedModuleIds.Count)
            throw new InvalidOperationException(
                "Authoritative CharacterSkill pilot rule references a removed module.");
        string[] offeredPositive = ((JArray)request["moduleOffers"])
            .OfType<JObject>()
            .Where(value => string.Equals(
                RequireString(value, "polarity"), "positive", StringComparison.Ordinal))
            .Select(value => RequireString(value, "moduleId"))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!offeredPositive.SequenceEqual(rule.allowedModuleIds
                .OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterSkill source offer IDs diverged from its authoritative rule snapshot.");
        }
        string[] offeredDrawbacks = ((JArray)request["moduleOffers"])
            .OfType<JObject>()
            .Where(value => string.Equals(
                RequireString(value, "polarity"), "drawback",
                StringComparison.Ordinal))
            .Select(value => RequireString(value, "moduleId"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterSkillDrawbackCapabilityDefinition[] drawbackDefinitions =
            offeredDrawbacks.Select(value => settings.FindDrawback(value)
                ?? throw new InvalidOperationException(
                    $"Skill resolution source offers unknown drawback '{value}'."))
            .ToArray();
        HashSet<string> offeredEvidence = RequireStringArray(
                request, "evidenceFactIds")
            .ToHashSet(StringComparer.Ordinal);
        string[] qualifiedNegativeEvidence = ((JArray)source["evidenceProvenance"])
            .OfType<JObject>()
            .Where(value =>
            {
                JObject tuple = RequireObject(value["recordedTuple"], "recordedTuple");
                return NarrativeFormulaNegativeEvidence.Matches(
                        RequireString(tuple, "outcome"),
                        RequireString(tuple, "factId"))
                    && Enum.TryParse(RequireString(tuple, "domain"),
                        out CharacterNarrativeDomain domain)
                    && drawbackDefinitions.Any(definition =>
                        definition.domainAffinities.Contains(domain));
            })
            .Select(value => RequireString(value, "evidenceId"))
            .Where(offeredEvidence.Contains)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterSkillDraft draft = new()
        {
            unlockLevel = 1,
            kind = kind,
            requestedUltimateDomain = rule.ultimateDomain,
            requestKey = RequireString(source, "targetPersistentId"),
            rules = new List<CharacterSkillCandidateRule> { rule },
            candidates = new List<CharacterSkillInstance>(),
            frozenMechanics = new List<CharacterSkillInstance>(),
            formulaVersion = policy.FormulaVersion,
            formulaCatalogSha256 = settings.formulaPolicy.RequireCatalogSha256(),
            formulaBudget = rule.budget,
            nextPresentationIndex = 0,
            presentationState = CharacterSkillPresentationState.PresentationPending,
            moduleSelectionOffers = new List<CharacterSkillModuleOfferState>
            {
                new()
                {
                    selectionId = choice.SelectionId,
                    ruleId = rule.ruleId,
                    positiveModuleIds = offeredPositive.ToList(),
                    drawbackModuleIds = offeredDrawbacks.ToList(),
                    evidenceFactIds = RequireStringArray(request, "evidenceFactIds").ToList(),
                    qualifiedNegativeEvidenceFactIds =
                        qualifiedNegativeEvidence.ToList(),
                    maximumPositiveModules = (int)request["maximumPositiveModules"],
                    maximumDrawbackModules = (int)request["maximumDrawbackModules"]
                }
            }
        };
        CharacterSkillInstance resolved = CharacterSkillFormulaGeneration.ResolveModuleSelection(
            draft, choice, settings);
        if (rule.trigger == CharacterSkillTrigger.ManualWork
            && (resolved.manualDurationHours < 1 || resolved.manualCooldownDays < 1))
        {
            throw new InvalidOperationException(
                "Resolved ManualWork skill is not runtime-activation-valid.");
        }
        Dictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors = legal
            .Select(settings.RequireFormulaDescriptor)
            .ToDictionary(value => value.CapabilityId, StringComparer.Ordinal);
        Dictionary<string, string> displayNames = legal.ToDictionary(
            value => settings.RequireFormulaDescriptor(value).CapabilityId,
            value => value.displayName, StringComparer.Ordinal);
        AllocationResidual residual = ResolveSkillResidual(
            settings, rule, resolved);
        return AllocationJson(resolved.formulaVersion, resolved.formulaCatalogSha256,
            resolved.narrativeBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.drawbackId,
            resolved.drawbackEvidenceQualified, resolved.mechanicalDescription,
            resolved.formulaCapabilities.Select(value => CapabilityJson(
                value.capabilityId, value.formatterId, value.applicatorId,
                value.parameters.Select(parameter => (parameter.parameterId, parameter.units)),
                descriptors[value.capabilityId], displayNames[value.capabilityId], rule, kind)),
            burdenEffects: CharacterSkillBurdenEffects(settings, choice, resolved),
            residual: residual,
            skillContext: SkillContextJson(rule, kind),
            mechanicalSummary: BuildSkillMechanicalSummary(
                rule, kind, resolved, descriptors, displayNames));
    }

    private static PilotCharacterOwner RequirePilotOwner(
        JObject source,
        JObject request,
        string profile,
        int index)
    {
        PilotCharacterOwner expected = BuildPilotOwner(profile, index);
        if (!string.Equals(RequireString(source, "targetPersistentId"), expected.PersistentId,
                StringComparison.Ordinal)
            || !string.Equals(RequireString(request, "targetDisplayName"), expected.DisplayName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Pilot source owner context does not match its deterministic character identity.");
        }
        return expected;
    }

    private static AllocationResidual ResolveSkillResidual(
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillCandidateRule rule,
        CharacterSkillInstance resolved)
    {
        if (settings == null || rule == null || resolved == null)
            throw new ArgumentNullException("Skill residual evidence requires resolved formula inputs.");
        int residual = resolved.narrativeBudget - resolved.calculatedCost;
        if (residual < 0)
            throw new InvalidOperationException("Resolved CharacterSkill exceeded its narrative budget.");
        if (residual == 0) return new AllocationResidual(
            resolved.narrativeBudget, resolved.calculatedCost, "none");

        int positiveBudget = checked(resolved.narrativeBudget + resolved.drawbackCredit);
        bool anyExpandableAxis = false;
        bool allAxesAtMaximum = true;
        foreach (CharacterSkillFormulaCapabilityEnvelope capability in resolved.formulaCapabilities)
        {
            CharacterSkillModuleRule module = rule.allowedModuleIds
                .Select(settings.FindModule)
                .FirstOrDefault(value => value != null && string.Equals(
                    settings.RequireFormulaDescriptor(value).CapabilityId,
                    capability.capabilityId, StringComparison.Ordinal));
            if (module == null)
                throw new InvalidOperationException(
                    "Resolved CharacterSkill capability is absent from its authoritative rule.");
            NarrativeFormulaCapabilityDescriptor descriptor = settings.RequireFormulaDescriptor(module);
            foreach (CharacterSkillFormulaParameter parameter in capability.parameters)
            {
                NarrativeFormulaQuantizedRange range = descriptor.RequireRange(parameter.parameterId);
                if (parameter.units > range.MaximumUnits)
                    throw new InvalidOperationException(
                        "Resolved CharacterSkill parameter escaped its descriptor range.");
                if (parameter.units >= range.MaximumUnits) continue;
                allAxesAtMaximum = false;
                if (checked(resolved.positiveCost + range.CostPerQuantum) <= positiveBudget)
                    anyExpandableAxis = true;
            }
        }
        if (anyExpandableAxis)
        {
            throw new InvalidOperationException(
                "Selected CharacterSkill formula left an affordable next quantum; residual is not explainable.");
        }
        return new AllocationResidual(resolved.narrativeBudget, resolved.calculatedCost,
            allAxesAtMaximum ? "effective-cap-reached" : "next-quantum-exceeds-budget");
    }

    /// <summary>
    /// Gives the residual reason only after replaying the same terminal-grid
    /// test used by the formula optimizer: every applied axis is checked
    /// against the selected allocation and its next quantum cost.
    /// </summary>
    private static AllocationResidual ResolveFormulaResidual(
        int narrativeBudget,
        int positiveCost,
        int drawbackCredit,
        int calculatedCost,
        IEnumerable<ResolvedFormulaAxes> capabilities)
    {
        int residual = narrativeBudget - calculatedCost;
        if (residual < 0)
            throw new InvalidOperationException("Resolved formula exceeded its narrative budget.");
        if (residual == 0)
            return new AllocationResidual(narrativeBudget, calculatedCost, "none");

        int positiveBudget = checked(narrativeBudget + drawbackCredit);
        bool allAxesAtMaximum = true;
        bool affordableNextQuantum = false;
        ResolvedFormulaAxes[] resolved = (capabilities
                ?? Array.Empty<ResolvedFormulaAxes>())
            .Where(value => value != null).ToArray();
        if (resolved.Length == 0)
            throw new InvalidOperationException(
                "Residual evidence requires at least one resolved formula capability.");
        foreach (ResolvedFormulaAxes capability in resolved)
        {
            Dictionary<string, long> unitsByParameter = capability.Parameters
                .ToDictionary(value => value.ParameterId, value => value.Units,
                    StringComparer.Ordinal);
            foreach (string parameterId in capability.Descriptor.AppliedParameterIds)
            {
                if (!unitsByParameter.TryGetValue(parameterId, out long units))
                    throw new InvalidOperationException(
                        "Resolved formula omitted applied parameter '" + parameterId + "'.");
                NarrativeFormulaQuantizedRange range = capability.Descriptor.RequireRange(
                    parameterId);
                if (units < range.MinimumUnits || units > range.MaximumUnits)
                    throw new InvalidOperationException(
                        "Resolved formula parameter escaped its descriptor range.");
                if (units >= range.MaximumUnits)
                    continue;
                allAxesAtMaximum = false;
                if (checked(positiveCost + range.CostPerQuantum) <= positiveBudget)
                    affordableNextQuantum = true;
            }
        }
        if (affordableNextQuantum)
            throw new InvalidOperationException(
                "Selected formula left an affordable next quantum; residual is not explainable.");
        return new AllocationResidual(narrativeBudget, calculatedCost,
            allAxesAtMaximum ? "effective-cap-reached" : "next-quantum-exceeds-budget");
    }

    private static JObject ResolveTraitAllocation(
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        JObject source,
        NarrativeFormulaModuleSelectionChoice choice,
        int index)
    {
        if (choice.PositiveModuleIds.Count != 1 || choice.DrawbackModuleIds.Count > 1)
            throw new InvalidOperationException(
                "Acquired-trait review selection requires one benefit and at most one drawback.");
        JObject request = RequireObject(source["moduleSelectionRequest"],
            "moduleSelectionRequest");
        PilotCharacterOwner owner = RequirePilotOwner(source, request, "trait", index);
        EvidenceBundle evidence = BuildCharacterEvidence(
            "trait", index, settings.RequireFormulaPolicy(), StrengthBandFor(index), owner);
        CharacterAcquiredTraitPendingRequestState resolved =
            CharacterAcquiredTraitFormulaGeneration.FreezeSelectedForExport(
                RequireString(source, "targetPersistentId"), evidence.Ledger,
                "trait-review:" + index.ToString("D5", CultureInfo.InvariantCulture),
                "trait-review-key:" + index.ToString("D5", CultureInfo.InvariantCulture),
                3, settings, modules, choice.EvidenceFactIds,
                choice.PositiveModuleIds.Single(),
                choice.DrawbackModuleIds.SingleOrDefault());
        Dictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors = modules
            .Where(value => value != null).Select(value => value.RequireFormulaDescriptor())
            .Concat(settings.DrawbackCapabilities.Where(value => value != null)
                .Select(value => value.RequireFormulaDescriptor()))
            .ToDictionary(value => value.CapabilityId, StringComparer.Ordinal);
        Dictionary<string, string> displayNames = modules.Where(value => value != null)
            .ToDictionary(value => value.RequireFormulaDescriptor().CapabilityId,
                value => value.DisplayName, StringComparer.Ordinal);
        foreach (CharacterAcquiredTraitDrawbackCapabilityDefinition drawback
                 in settings.DrawbackCapabilities.Where(value => value != null))
            displayNames[drawback.RequireFormulaDescriptor().CapabilityId] = drawback.DisplayName;
        CharacterAcquiredTraitDrawbackCapabilityDefinition selectedDrawback =
            choice.DrawbackModuleIds.Count == 0
                ? null
                : settings.DrawbackCapabilities.Single(value => string.Equals(
                    value.DrawbackId, choice.DrawbackModuleIds.Single(),
                    StringComparison.Ordinal));
        CharacterAcquiredTraitFormulaCapabilityEnvelope[] positiveCapabilities = resolved
            .formulaCapabilities.Where(value => selectedDrawback == null
                || !string.Equals(value.capabilityId, selectedDrawback.CapabilityId,
                    StringComparison.Ordinal))
            .ToArray();
        // A selected drawback's severity is independently frozen to its accepted
        // credit before the benefit optimizer runs. Only benefit axes may spend a
        // remaining positive budget quantum; worsening that fixed burden is not a
        // legal residual-consumption action.
        AllocationResidual residual = ResolveFormulaResidual(
            resolved.narrativeBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, positiveCapabilities.Select(value =>
                new ResolvedFormulaAxes(descriptors[value.capabilityId],
                    value.parameters.Select(parameter =>
                        (parameter.parameterId, parameter.units)))));
        return AllocationJson(resolved.formulaVersion, resolved.formulaCatalogSha256,
            resolved.narrativeBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.drawbackId,
            resolved.drawbackEvidenceQualified, resolved.mechanicalDescription,
            positiveCapabilities
                .Select(value => CapabilityJson(
                value.capabilityId, value.formatterId, value.applicatorId,
                value.parameters.Select(parameter => (parameter.parameterId, parameter.units)),
                descriptors[value.capabilityId], displayNames[value.capabilityId])),
            burdenEffects: AcquiredTraitBurdenEffects(
                selectedDrawback, resolved, descriptors),
            residual: residual,
            mechanicalSummary: BuildTraitMechanicalSummary(
                resolved, modules, settings, selectedDrawback));
    }

    private static JObject ResolveEquipmentAllocation(
        EquipmentEvolutionFormulaCatalogSO catalog,
        IReadOnlyList<CombatEquipmentDefinitionSO> definitions,
        JObject source,
        NarrativeFormulaModuleSelectionChoice choice,
        int index)
    {
        string targetId = RequireString(source, "targetPersistentId");
        CombatEquipmentDefinitionSO definition = RequireEquipmentTarget(
            source, catalog, definitions);
        EquipmentGameplayFormulaReplayResult replay = EquipmentGameplayFormulaReplay.Capture(
            catalog, definition, targetId, index, StrengthBandFor(index).InfluenceUseCount);
        EvolutionNode resolved = EquipmentEvolutionRules.FreezeSelectedModuleForExport(
            replay.Node, replay.State, catalog, targetId, choice);
        Dictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors = catalog.capabilities
            .Where(value => value != null).Select(value => value.ToRuntime())
            .ToDictionary(value => value.CapabilityId, StringComparer.Ordinal);
        Dictionary<string, string> displayNames = catalog.capabilities
            .Where(value => value != null).ToDictionary(value => value.capabilityId,
                value => value.displayName, StringComparer.Ordinal);
        EvolutionModuleRegistry registry = new();
        foreach (EvolutionModuleDefinition module in registry.All.Where(value =>
                     value.ModuleId.StartsWith("equipment:", StringComparison.Ordinal)
                     && value.BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback))
        {
            NarrativeFormulaCapabilityDescriptor drawback =
                EvolutionModuleFormulaDescriptor.ForOptionalDrawback(
                    module, "equipment-drawback");
            descriptors[drawback.CapabilityId] = drawback;
            displayNames[drawback.CapabilityId] = module.DisplayName;
        }
        EvolutionModuleDefinition selectedModule = registry.All.Single(value =>
            string.Equals(value.ModuleId, choice.PositiveModuleIds.Single(),
                StringComparison.Ordinal));
        AllocationResidual residual = ResolveFormulaResidual(
            resolved.formulaBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.formulaCapabilities.Select(value =>
                new ResolvedFormulaAxes(descriptors[value.capabilityId],
                    value.parameters.Select(parameter =>
                        (parameter.parameterId, parameter.units)))));
        return AllocationJson(resolved.formulaVersion, resolved.formulaCatalogSha256,
            resolved.formulaBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.drawbackId,
            resolved.drawbackEvidenceQualified, resolved.mechanicalDescription,
            resolved.formulaCapabilities.Select(value => CapabilityJson(
                value.capabilityId, value.formatterId, value.applicatorId,
                value.parameters.Select(parameter => (parameter.parameterId, parameter.units)),
                descriptors[value.capabilityId], displayNames[value.capabilityId])),
            selectedModule.BurdenKind.ToString(), resolved.burdenEffectId,
            resolved.burdenPotencyMultiplier,
            EvolutionBurdenEffects(selectedModule, resolved, registry),
            residual: residual,
            mechanicalSummary: BuildEquipmentMechanicalSummary(
                catalog, selectedModule, resolved));
    }

    private static CombatEquipmentDefinitionSO RequireEquipmentTarget(
        JObject source,
        EquipmentEvolutionFormulaCatalogSO catalog,
        IReadOnlyList<CombatEquipmentDefinitionSO> definitions)
    {
        JObject target = RequireObject(source["authoritativeEquipmentTarget"],
            "authoritativeEquipmentTarget");
        if (!target.Properties().Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(new[] { "definitionId", "kind" }, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Equipment target snapshot escaped its closed source contract.");
        }
        string definitionId = RequireString(target, "definitionId");
        CombatEquipmentKind kind = RequireEnum<CombatEquipmentKind>(target, "kind");
        CombatEquipmentDefinitionSO definition = (definitions
                ?? Array.Empty<CombatEquipmentDefinitionSO>())
            .SingleOrDefault(value => value != null && string.Equals(value.EquipmentId,
                definitionId, StringComparison.Ordinal));
        if (definition == null)
        {
            throw new InvalidOperationException(
                "Equipment target snapshot refers to an unavailable immutable definition: "
                + definitionId);
        }
        if (definition.Kind != kind)
        {
            throw new InvalidOperationException(
                "Equipment target snapshot kind drifted from its immutable definition: "
                + definitionId);
        }
        if (!EquipmentGameplayFormulaReplay.IsDefinitionReplayEligible(
                catalog, definition, out string failureReason))
        {
            throw new InvalidOperationException(
                "Equipment target snapshot is no longer replay-eligible: "
                + definitionId + " (" + failureReason + ").");
        }
        return definition;
    }

    private static JObject ResolveFacilityAllocation(
        IReadOnlyList<FacilityEvolutionRecipeSO> recipes,
        JObject source,
        NarrativeFormulaModuleSelectionChoice choice,
        int index)
    {
        FacilityReplayPlan[] eligiblePlans = ResolveFacilityReplayPlans(recipes).Plans;
        if (eligiblePlans.Length == 0)
            throw new InvalidOperationException(
                "Facility resolution has no source-applicable controlled replay plans.");
        FacilityReplayPlan plan = eligiblePlans[index % eligiblePlans.Length];
        FacilityEvolutionRecipeSO recipe = plan.Recipe;
        string targetId = RequireString(source, "targetPersistentId");
        FacilityEvolutionState state = new()
        {
            facilityPersistentId = targetId,
            usageLedger = new UsageLedger(),
            formulaEvidence = new List<FacilityFormulaEvidenceRecord>(),
            evolutionNodes = new List<EvolutionNode>(),
            generation = 0
        };
        string displayName = plan.SourceDefinition.objectName ?? recipe.DisplayName;
        int replayVariationIndex = index / eligiblePlans.Length;
        BuildFacilityUsageLedger(
            state,
            index,
            replayVariationIndex,
            displayName,
            plan);
        FacilityEvolutionFormulaPresentationPendingSnapshot pending =
            PrepareFacilityStrengthBand(state, recipe, StrengthBandFor(index));
        EvolutionNode resolved = FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
            state, recipe, pending.node, choice);
        Dictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors = recipe
            .RequireFormulaCapabilities().Select(value => value.ToRuntime())
            .ToDictionary(value => value.CapabilityId, StringComparer.Ordinal);
        EvolutionModuleRegistry registry = new();
        Dictionary<string, string> displayNames = recipe.RequireFormulaCapabilities()
            .ToDictionary(value => value.capabilityId,
                value => registry.All.Single(module => string.Equals(
                    module.ModuleId, value.evolutionModuleId,
                    StringComparison.Ordinal)).DisplayName,
                StringComparer.Ordinal);
        foreach (EvolutionModuleDefinition module in registry.All.Where(value =>
                     value.ModuleId.StartsWith("facility:", StringComparison.Ordinal)
                     && value.BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback))
        {
            NarrativeFormulaCapabilityDescriptor drawback =
                EvolutionModuleFormulaDescriptor.ForOptionalDrawback(
                    module, "facility-drawback");
            descriptors[drawback.CapabilityId] = drawback;
            displayNames[drawback.CapabilityId] = module.DisplayName;
        }
        EvolutionModuleDefinition selectedModule = registry.All.Single(value =>
            string.Equals(value.ModuleId, choice.PositiveModuleIds.Single(),
                StringComparison.Ordinal));
        AllocationResidual residual = ResolveFormulaResidual(
            resolved.formulaBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.formulaCapabilities.Select(value =>
                new ResolvedFormulaAxes(descriptors[value.capabilityId],
                    value.parameters.Select(parameter =>
                        (parameter.parameterId, parameter.units)))));
        return AllocationJson(resolved.formulaVersion, resolved.formulaCatalogSha256,
            resolved.formulaBudget, resolved.positiveCost, resolved.drawbackCredit,
            resolved.calculatedCost, resolved.drawbackId,
            resolved.drawbackEvidenceQualified, resolved.mechanicalDescription,
            resolved.formulaCapabilities.Select(value => CapabilityJson(
                value.capabilityId, value.formatterId, value.applicatorId,
                value.parameters.Select(parameter => (parameter.parameterId, parameter.units)),
                descriptors[value.capabilityId], displayNames[value.capabilityId])),
            selectedModule.BurdenKind.ToString(), resolved.burdenEffectId,
            resolved.burdenPotencyMultiplier,
            EvolutionBurdenEffects(selectedModule, resolved, registry),
            residual: residual,
            mechanicalSummary: BuildEvolutionMechanicalSummary(
                "시설", selectedModule, resolved));
    }

    private static JObject AllocationJson(
        int formulaVersion,
        string formulaCatalogSha256,
        int narrativeBudget,
        int positiveCost,
        int drawbackCredit,
        int calculatedCost,
        string drawbackId,
        bool drawbackEvidenceQualified,
        string mechanicalDescription,
        IEnumerable<JObject> capabilities,
        string positiveBurdenKind = "NotApplicable",
        string appliedBurdenEffectId = "",
        float appliedBurdenPotency = 0f,
        IEnumerable<JObject> burdenEffects = null,
        AllocationResidual residual = null,
        JObject skillContext = null,
        string mechanicalSummary = null)
    {
        JArray capabilityValues = new(capabilities ?? Array.Empty<JObject>());
        JArray burdenValues = new(burdenEffects ?? Array.Empty<JObject>());
        AllocationResidual resolvedResidual = residual ?? throw new InvalidOperationException(
            "C# allocation must include replayed residual evidence.");
        if (string.IsNullOrWhiteSpace(mechanicalSummary))
            throw new InvalidOperationException(
                "C# allocation must include a typed mechanical summary.");
        JObject value = new()
        {
            ["formulaVersion"] = formulaVersion,
            ["formulaCatalogSha256"] = formulaCatalogSha256,
            ["narrativeBudget"] = narrativeBudget,
            ["positiveCost"] = positiveCost,
            ["drawbackCredit"] = drawbackCredit,
            ["calculatedCost"] = calculatedCost,
            ["drawbackId"] = drawbackId ?? string.Empty,
            ["drawbackEvidenceQualified"] = drawbackEvidenceQualified,
            ["positiveBurdenKind"] = positiveBurdenKind ?? "NotApplicable",
            ["appliedBurdenEffectId"] = appliedBurdenEffectId ?? string.Empty,
            ["appliedBurdenPotency"] = appliedBurdenPotency,
            ["mechanicalDescription"] = mechanicalDescription ?? string.Empty,
            ["mechanicalSummary"] = mechanicalSummary,
            ["residualBudget"] = resolvedResidual.ResidualBudget,
            ["residualReason"] = resolvedResidual.Reason,
            ["skillContext"] = skillContext?.DeepClone() ?? JValue.CreateNull(),
            ["capabilities"] = capabilityValues,
            ["burdenEffects"] = burdenValues
        };
        ValidateAllocation(value);
        return value;
    }

    private static JObject CapabilityJson(
        string capabilityId,
        string formatterId,
        string applicatorId,
        IEnumerable<(string ParameterId, long Units)> parameters,
        NarrativeFormulaCapabilityDescriptor descriptor,
        string displayName,
        CharacterSkillCandidateRule skillRule = null,
        CharacterSkillKind? skillKind = null)
    {
        (string ParameterId, long Units)[] applicable = (parameters
                ?? Array.Empty<(string ParameterId, long Units)>())
            .Where(parameter => IsReadableParameter(descriptor, parameter.ParameterId))
            .ToArray();
        return new JObject
        {
            ["capabilityId"] = capabilityId,
            ["displayName"] = ReadableCapabilityDisplayName(
                displayName, capabilityId, skillRule, skillKind),
            ["formatterId"] = formatterId,
            ["applicatorId"] = applicatorId,
            ["parameters"] = new JArray(applicable.Select(parameter => new JObject
            {
                ["parameterId"] = parameter.ParameterId,
                ["displayName"] = ParameterDisplayName(parameter.ParameterId),
                ["units"] = parameter.Units,
                ["formattedValue"] = FormatReadableParameter(
                    capabilityId, parameter.ParameterId,
                    descriptor.RequireRange(parameter.ParameterId), parameter.Units,
                    skillRule, skillKind)
            }))
        };
    }

    private static bool IsReadableParameter(
        NarrativeFormulaCapabilityDescriptor descriptor,
        string parameterId) => descriptor != null
            && descriptor.AppliedParameterIds.Contains(parameterId, StringComparer.Ordinal);

    private static string FormatReadableParameter(
        string capabilityId,
        string parameterId,
        NarrativeFormulaQuantizedRange range,
        long units,
        CharacterSkillCandidateRule skillRule = null,
        CharacterSkillKind? skillKind = null)
    {
        decimal value = range.ToDecimal(units);
        string number = value.ToString("0.###", CultureInfo.InvariantCulture);
        bool defenseUltimate = IsDefenseUltimate(skillRule, skillKind);
        return parameterId switch
        {
            NarrativeFormulaParameterIds.Duration => defenseUltimate && capabilityId == "dot"
                ? "즉시 결합 피해 합산 기간 " + number + "턴"
                : capabilityId == "mood"
                ? "기분 상태 " + number + "초"
                : "효과 지속 " + number + "턴",
            NarrativeFormulaParameterIds.Count => "적용 횟수 " + number + "회",
            NarrativeFormulaParameterIds.TargetCount => "효과 대상 " + number + "명",
            NarrativeFormulaParameterIds.Magnitude => FormatMagnitude(
                capabilityId, value, number, defenseUltimate),
            _ => ParameterDisplayName(parameterId) + " " + number
        };
    }

    private static string FormatMagnitude(
        string capabilityId,
        decimal value,
        string number,
        bool defenseUltimate)
    {
        string signedPercent = (value * 100m).ToString(
            value >= 0m ? "+0.###;-0.###;0" : "0.###", CultureInfo.InvariantCulture) + "%";
        string percent = (value * 100m).ToString("0.###", CultureInfo.InvariantCulture) + "%";
        return capabilityId switch
        {
            "work_speed" => "작업 속도 " + signedPercent,
            "output" => "생산량 " + signedPercent,
            "revenue" => "수익 " + signedPercent,
            // Repair and cleaning values are authored as percentage points and
            // consumed as value / 100f by their speed multiplier accessors.
            "repair" => "수리 속도 +" + number + "%",
            "cleaning" => "청소 속도 +" + number + "%",
            "stock" => "재고 생산 +" + number,
            "research" => "작업 완료로 집계된 매초 연구 진척 +" + number,
            "needs" => "가장 부족한 욕구 +" + number,
            "mood" => "기분 +" + number,
            "relationship" => "긍정 관계 감정 변화에 +"
                + (value / 100m).ToString("0.####", CultureInfo.InvariantCulture),
            "damage" => "기본 피해 ×" + number,
            "heal" => "대상 체력 회복 +" + number,
            "dot" when defenseUltimate => "즉시 결합 피해의 기간당 기준 +" + number,
            "dot" => "턴당 피해 +" + number,
            "vulnerability" => "받는 피해 " + signedPercent,
            "delay" => "주도권 페널티 " + number,
            "buff" => "대상 전투 능력 " + signedPercent,
            "debuff" => "대상 전투 능력 -" + (value * 100m)
                .ToString("0.###", CultureInfo.InvariantCulture) + "%",
            "reposition" => "대상 후열 방향 이동 " + number + "칸",
            "conditional_amplify" when defenseUltimate => "기본 피해 계수 ×" + number
                + "의 절반을 즉시 추가",
            "conditional_amplify" => "대상 체력 절반 이하 시 추가 기본 피해 "
                + signedPercent,
            "cooldown_adjust" => "대상 재사용 대기시간 -" + number + "턴",
            "guard" or "protect" => "대상이 받는 피해 -" + percent,
            _ => "효과량 " + number
        };
    }

    private static string BuildSkillMechanicalSummary(
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillInstance resolved,
        IReadOnlyDictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors,
        IReadOnlyDictionary<string, string> displayNames)
    {
        if (rule == null || resolved == null || descriptors == null || displayNames == null)
            throw new ArgumentNullException("Skill summary requires the resolved rule and descriptors.");
        string[] effects = (resolved.formulaCapabilities
                ?? new List<CharacterSkillFormulaCapabilityEnvelope>())
            .Select(value => BuildCapabilitySummaryLine(
                ReadableCapabilityDisplayName(displayNames[value.capabilityId],
                    value.capabilityId, rule, kind), value.capabilityId,
                value.parameters.Select(parameter => (parameter.parameterId, parameter.units)),
                descriptors[value.capabilityId], rule, kind))
            .ToArray();
        if (effects.Length == 0)
            throw new InvalidOperationException(
                "CharacterSkill summary requires at least one resolved capability.");
        if (IsDefenseUltimate(rule, kind))
        {
            return "C# 확정 방어 궁극기 즉시 결합 피해 — "
                + CharacterSkillPresentationSemantics.DescribeContext(rule, kind)
                + " 선택된 피해 항목을 합산해 침입자에게 한 번에 적용한다. "
                + string.Join(" / ", effects);
        }

        string timing;
        if (rule.trigger == CharacterSkillTrigger.ManualWork)
        {
            timing = "수동 작업 지속 "
                + resolved.manualDurationHours.ToString(CultureInfo.InvariantCulture) + "시간"
                + ", 수동 작업 재사용 대기 "
                + resolved.manualCooldownDays.ToString(CultureInfo.InvariantCulture) + "일";
        }
        else if (rule.trigger == CharacterSkillTrigger.ManualCombat)
        {
            timing = "전투 재사용 대기 "
            + resolved.cooldownTurns.ToString(CultureInfo.InvariantCulture) + "턴";
        }
        else timing = string.Empty;
        return "C# 확정 스킬 효과 — "
            + CharacterSkillPresentationSemantics.DescribeContext(rule, kind)
            + (timing.Length == 0 ? " " : " " + timing + ". ")
            + string.Join(" / ", effects);
    }

    private static string BuildTraitMechanicalSummary(
        CharacterAcquiredTraitPendingRequestState resolved,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitDrawbackCapabilityDefinition selectedDrawback)
    {
        if (resolved == null || modules == null || settings == null)
            throw new ArgumentNullException("Trait summary requires frozen effect overrides.");
        HashSet<string> selectedBenefits = (resolved.benefitModuleIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        CharacterAcquiredTraitModuleSO[] selectedBenefitModules = modules
            .Where(module => module != null && selectedBenefits.Contains(module.ModuleId))
            .OrderBy(module => module.ModuleId, StringComparer.Ordinal)
            .ToArray();
        IEnumerable<GameplayEffectBinding> benefitBindings = selectedBenefitModules
            .SelectMany(module => module.Effects.Where(binding => binding != null
                && !module.DrawbackBindingIds.Contains(binding.bindingId,
                    StringComparer.Ordinal)));
        IEnumerable<GameplayEffectBinding> drawbackBindings = selectedDrawback?.Effects
            ?.Where(binding => binding != null) ?? Array.Empty<GameplayEffectBinding>();
        Dictionary<string, GameplayEffectBinding> bindingById = benefitBindings
            .Concat(drawbackBindings)
            .ToDictionary(binding => binding.bindingId, binding => binding,
                StringComparer.Ordinal);
        string[] effects = (resolved.effectOverrides
                ?? new List<CharacterAcquiredTraitEffectOverride>())
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select(overrideValue =>
            {
                if (!bindingById.TryGetValue(overrideValue.bindingId,
                        out GameplayEffectBinding binding)
                    || binding.definition == null)
                {
                    throw new InvalidOperationException(
                        "Frozen trait effect override has no authored binding definition.");
                }
                return FormatTraitEffectOverride(binding, overrideValue.value);
            })
            .ToArray();
        string[] reactions = selectedBenefitModules
            .SelectMany(module => module.SpecialReactions
                .Where(reaction => reaction != null)
                .OrderBy(reaction => reaction.ReactionId, StringComparer.Ordinal)
                .Select(reaction => CharacterAcquiredTraitSpecialReactionMath
                    .FormatMechanicalDescription(reaction,
                        ResolveFrozenTraitReactionValue(resolved, module, reaction))))
            .ToArray();
        if (effects.Length == 0 && reactions.Length == 0)
            throw new InvalidOperationException(
                "Trait summary requires a frozen effect override or special reaction.");
        return "C# 확정 특성 효과 — " + string.Join(" / ", effects.Concat(reactions));
    }

    private static float ResolveFrozenTraitReactionValue(
        CharacterAcquiredTraitPendingRequestState resolved,
        CharacterAcquiredTraitModuleSO module,
        CharacterAcquiredTraitSpecialReactionDefinition reaction)
    {
        CharacterAcquiredTraitFormulaCapabilityEnvelope envelope =
            (resolved.formulaCapabilities
                ?? new List<CharacterAcquiredTraitFormulaCapabilityEnvelope>())
            .SingleOrDefault(value => value != null && string.Equals(
                value.capabilityId, module.CapabilityId, StringComparison.Ordinal));
        if (envelope == null)
            throw new InvalidOperationException(
                "Frozen trait reaction has no selected formula capability.");
        CharacterAcquiredTraitFormulaParameter magnitude = (envelope.parameters
                ?? new List<CharacterAcquiredTraitFormulaParameter>())
            .SingleOrDefault(value => value != null && string.Equals(
                value.parameterId, NarrativeFormulaParameterIds.Magnitude,
                StringComparison.Ordinal));
        if (magnitude == null)
            throw new InvalidOperationException(
                "Frozen trait reaction has no selected potency parameter.");
        float potencyScale = (float)module.RequireFormulaDescriptor()
            .RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.units);
        return CharacterAcquiredTraitSpecialReactionMath.ResolveActionValue(
            reaction, potencyScale);
    }

    private static string FormatTraitEffectOverride(
        GameplayEffectBinding binding,
        float value)
    {
        string condition = binding.condition == null
            ? string.Empty : " (조건: " + binding.condition.ConditionId + ")";
        string number = value.ToString("0.####", CultureInfo.InvariantCulture);
        string outcome = binding.definition.Operation switch
        {
            GameplayEffectOperation.AddFlat => "+" + number,
            GameplayEffectOperation.AddPercent => "+"
                + (value * 100f).ToString("0.####", CultureInfo.InvariantCulture) + "%",
            GameplayEffectOperation.Multiply => "×" + number,
            GameplayEffectOperation.Override => "=" + number,
            GameplayEffectOperation.ClampMinimum => "최소 " + number,
            GameplayEffectOperation.ClampMaximum => "최대 " + number,
            _ => throw new ArgumentOutOfRangeException()
        };
        return AcquiredTraitBurdenTargetCatalog.DisplayName(binding.definition.TargetId)
            + condition + " " + outcome;
    }

    private static string BuildEquipmentMechanicalSummary(
        EquipmentEvolutionFormulaCatalogSO catalog,
        EvolutionModuleDefinition selectedModule,
        EvolutionNode resolved)
    {
        if (catalog == null || selectedModule == null || resolved == null)
            throw new ArgumentNullException(
                "Equipment summary requires the frozen catalog and module definition.");
        string[] effects = (resolved.formulaCapabilities
                ?? new List<EquipmentEvolutionFormulaCapabilityEnvelope>())
            .Select(envelope =>
            {
                EquipmentEvolutionFormulaCapabilityDefinition definition =
                    catalog.RequireCapability(envelope.capabilityId);
                EquipmentEvolutionFormulaParameterEnvelope magnitude = envelope.parameters
                    .Single(value => string.Equals(value.parameterId,
                        NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal));
                decimal percent = definition.ToRuntime().RequireRange(
                    NarrativeFormulaParameterIds.Magnitude).ToDecimal(magnitude.units) * 100m;
                string verb = definition.modifierKind ==
                    EquipmentEvolutionFormulaModifierKind.MultiplierReduction
                    ? "감소" : "증가";
                return definition.displayName + ": "
                    + NarrativeBurdenStatCatalog.DisplayName(definition.statId) + " "
                    + percent.ToString("0.###", CultureInfo.InvariantCulture) + "% " + verb;
            })
            .ToArray();
        if (effects.Length == 0)
            throw new InvalidOperationException(
                "Equipment summary requires at least one frozen formula capability.");
        return "C# 확정 장비 진화 효과 — " + selectedModule.DisplayName
            + ": " + string.Join(" / ", effects);
    }

    private static string BuildEvolutionMechanicalSummary(
        string profileLabel,
        EvolutionModuleDefinition selectedModule,
        EvolutionNode resolved)
    {
        if (selectedModule == null || resolved == null)
            throw new ArgumentNullException("Evolution summary requires a frozen module definition.");
        string[] effects = (selectedModule.Benefits
                ?? Array.Empty<EvolutionEffectModifier>())
            .Select(value => FormatEvolutionModifier(value, resolved.potencyMultiplier))
            .ToArray();
        if (effects.Length == 0)
            throw new InvalidOperationException(
                "Evolution summary requires at least one authored benefit modifier.");
        return "C# 확정 " + profileLabel + " 진화 효과 — "
            + selectedModule.DisplayName + ": " + string.Join(" / ", effects);
    }

    private static string FormatEvolutionModifier(
        EvolutionEffectModifier modifier,
        float potency)
    {
        if (modifier == null || string.IsNullOrWhiteSpace(modifier.statId))
            throw new InvalidOperationException(
                "Evolution summary encountered an incomplete authored modifier.");
        float effective = modifier.additive != 0f
            ? modifier.additive * potency
            : 1f + (modifier.multiplier - 1f) * potency;
        return NarrativeBurdenStatCatalog.DisplayName(modifier.statId)
            + (modifier.additive != 0f
            ? " " + effective.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture)
            : " ×" + effective.ToString("0.####", CultureInfo.InvariantCulture));
    }

    private static string BuildCapabilitySummaryLine(
        string displayName,
        string capabilityId,
        IEnumerable<(string ParameterId, long Units)> parameters,
        NarrativeFormulaCapabilityDescriptor descriptor,
        CharacterSkillCandidateRule skillRule = null,
        CharacterSkillKind? skillKind = null)
    {
        string[] values = (parameters ?? Array.Empty<(string ParameterId, long Units)>())
            .Where(value => IsReadableParameter(descriptor, value.ParameterId))
            .Select(value => FormatReadableParameter(capabilityId, value.ParameterId,
                descriptor.RequireRange(value.ParameterId), value.Units, skillRule, skillKind))
            .ToArray();
        return values.Length == 0
            ? displayName
            : displayName + ": " + string.Join(" · ", values);
    }

    private static bool IsDefenseUltimate(
        CharacterSkillCandidateRule skillRule,
        CharacterSkillKind? skillKind) => skillRule != null
            && skillKind.HasValue
            && skillKind.Value == CharacterSkillKind.Ultimate
            && skillRule.ultimateDomain == CharacterUltimateDomain.Defense;

    private static string ReadableCapabilityDisplayName(
        string displayName,
        string capabilityId,
        CharacterSkillCandidateRule skillRule,
        CharacterSkillKind? skillKind)
    {
        if (!IsDefenseUltimate(skillRule, skillKind)) return displayName;
        return capabilityId switch
        {
            "dot" => "즉시 결합 피해",
            "conditional_amplify" => "즉시 추가 피해",
            _ => displayName
        };
    }

    private static IEnumerable<JObject> CharacterSkillBurdenEffects(
        CharacterSkillSystemSettingsSO settings,
        NarrativeFormulaModuleSelectionChoice choice,
        CharacterSkillInstance resolved)
    {
        if (choice.DrawbackModuleIds.Count == 0) return Array.Empty<JObject>();
        CharacterSkillDrawbackCapabilityDefinition definition = settings.FindDrawback(
            choice.DrawbackModuleIds.Single()) ?? throw new InvalidOperationException(
            "Resolved CharacterSkill drawback is no longer authored.");
        JObject line;
        if (resolved.drawbackEffect != null)
        {
            line = BurdenLineJson(
                definition.CapabilityId,
                AcquiredTraitBurdenTargetCatalog.DisplayName(
                    resolved.drawbackEffect.targetId),
                CharacterSkillFormulaGeneration.FormatDrawbackEffect(
                    resolved.drawbackEffect));
        }
        else
        {
            string unit = definition.applicationKind
                == CharacterSkillDrawbackApplicationKind.WorkCooldownDays
                    ? "일" : "턴";
            line = BurdenLineJson(
                definition.CapabilityId,
                "재사용 대기시간",
                "+" + resolved.drawbackCredit.ToString(
                    CultureInfo.InvariantCulture) + unit);
        }
        return new[]
        {
            BurdenEffectJson(
                resolved.drawbackId,
                definition.DisplayName,
                "OptionalDrawback",
                resolved.drawbackCredit,
                resolved.drawbackCredit,
                new[] { line })
        };
    }

    private static IEnumerable<JObject> AcquiredTraitBurdenEffects(
        CharacterAcquiredTraitDrawbackCapabilityDefinition definition,
        CharacterAcquiredTraitPendingRequestState resolved,
        IReadOnlyDictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors)
    {
        if (definition == null) return Array.Empty<JObject>();
        CharacterAcquiredTraitFormulaCapabilityEnvelope envelope =
            resolved.formulaCapabilities.Single(value => string.Equals(
                value.capabilityId, definition.CapabilityId, StringComparison.Ordinal));
        NarrativeFormulaCapabilityDescriptor descriptor = descriptors[definition.CapabilityId];
        CharacterAcquiredTraitFormulaParameter magnitude = envelope.parameters.Single(
            value => string.Equals(value.parameterId,
                NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal));
        NarrativeFormulaQuantizedRange range = descriptor.RequireRange(
            NarrativeFormulaParameterIds.Magnitude);
        float potency = (float)range.ToDecimal(magnitude.units);
        return new[]
        {
            BurdenEffectJson(
                definition.DrawbackId,
                definition.DisplayName,
                "OptionalDrawback",
                resolved.drawbackCredit,
                potency,
                definition.Effects.Select(binding =>
                {
                    float finalValue = binding.definition.Operation == GameplayEffectOperation.Multiply
                        ? 1f + (binding.value - 1f) * potency
                        : binding.value * potency;
                    string condition = binding.condition == null
                        ? string.Empty : " (" + binding.condition.ConditionId + ")";
                    return BurdenLineJson(
                        binding.definition.TargetId,
                        AcquiredTraitBurdenTargetCatalog.DisplayName(binding.definition.TargetId)
                            + condition,
                        binding.definition.Operation == GameplayEffectOperation.Multiply
                            ? "×" + finalValue.ToString("0.####", CultureInfo.InvariantCulture)
                            : finalValue.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture));
                }))
        };
    }

    private static IEnumerable<JObject> EvolutionBurdenEffects(
        EvolutionModuleDefinition primary,
        EvolutionNode resolved,
        EvolutionModuleRegistry registry)
    {
        List<JObject> effects = new();
        if (primary.BurdenKind is EvolutionModuleBurdenKind.OperatingCost
                or EvolutionModuleBurdenKind.InseparableRisk)
            effects.Add(EvolutionBurdenEffectJson(
                primary, primary.BurdenKind, 1f, 0));
        if (!string.IsNullOrEmpty(resolved.burdenEffectId))
        {
            if (!registry.TryGet(resolved.burdenEffectId,
                    out EvolutionModuleDefinition optional)
                || optional.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback)
                throw new InvalidOperationException(
                    "Resolved optional evolution drawback is no longer authored.");
            effects.Add(EvolutionBurdenEffectJson(optional,
                EvolutionModuleBurdenKind.OptionalDrawback,
                resolved.burdenPotencyMultiplier, resolved.drawbackCredit));
        }
        return effects;
    }

    private static JObject EvolutionBurdenEffectJson(
        EvolutionModuleDefinition module,
        EvolutionModuleBurdenKind kind,
        float potency,
        int budgetCredit) => BurdenEffectJson(
            module.ModuleId,
            module.DisplayName,
            kind.ToString(),
            budgetCredit,
            potency,
            module.Burdens.Select(value => BurdenLineJson(
                value.statId,
                EvolutionStatDisplayName(value.statId),
                value.additive != 0f
                    ? (value.additive * potency).ToString(
                        "+0.####;-0.####;0", CultureInfo.InvariantCulture)
                    : "×" + (1f + (value.multiplier - 1f) * potency)
                        .ToString("0.####", CultureInfo.InvariantCulture))));

    private static JObject BurdenEffectJson(
        string burdenId,
        string displayName,
        string kind,
        int budgetCredit,
        double potency,
        IEnumerable<JObject> effects) => new()
    {
        ["burdenId"] = burdenId,
        ["displayName"] = displayName,
        ["kind"] = kind,
        ["budgetCredit"] = budgetCredit,
        ["potency"] = potency,
        ["effects"] = new JArray(effects)
    };

    private static JObject BurdenLineJson(
        string effectId,
        string displayName,
        string formattedValue) => new()
    {
        ["effectId"] = effectId,
        ["displayName"] = displayName,
        ["formattedValue"] = formattedValue
    };

    private static string EvolutionStatDisplayName(string statId) =>
        NarrativeBurdenStatCatalog.DisplayName(statId);

    private static string ParameterDisplayName(string parameterId) => parameterId switch
    {
        NarrativeFormulaParameterIds.Magnitude => "효과량",
        NarrativeFormulaParameterIds.Duration => "지속",
        NarrativeFormulaParameterIds.Count => "횟수",
        NarrativeFormulaParameterIds.TargetCount => "대상 수",
        _ => parameterId
    };

    private static void ValidateResponseBinding(int index, JObject source, JObject authored)
    {
        if ((int?)authored["ordinal"] != index + 1
            || !string.Equals(RequireString(authored, "profileId"),
                RequireString(source, "profileId"), StringComparison.Ordinal)
            || !string.Equals(RequireString(authored, "targetPersistentId"),
                RequireString(source, "targetPersistentId"), StringComparison.Ordinal)
            || !string.Equals(RequireString(authored, "sourceRowHash"),
                "sha256:" + Sha256Text(CanonicalJson(source)), StringComparison.Ordinal)
            || (bool?)authored["humanApprovalClaimed"] != false)
            throw new InvalidOperationException(
                $"Response row {index + 1} is not bound to its source row.");
        JObject response = RequireObject(authored["response"], "response");
        string[] exactKeys =
        {
            "selectionId", "positiveModuleIds", "drawbackModuleIds",
            "evidenceFactIds", "displayName", "narrativeFlavor"
        };
        if (!response.Properties().Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(exactKeys.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Response row {index + 1} does not have the exact six-key contract.");
        JObject request = RequireObject(source["moduleSelectionRequest"],
            "moduleSelectionRequest");
        if (!string.Equals(RequireString(response, "selectionId"),
                RequireString(request, "selectionId"), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Response row {index + 1} has a stale selection ID.");
        Dictionary<string, string> offers = ((JArray)request["moduleOffers"])
            .OfType<JObject>().ToDictionary(value => RequireString(value, "moduleId"),
                value => RequireString(value, "polarity"), StringComparer.Ordinal);
        string[] positive = RequireStringArray(response, "positiveModuleIds");
        string[] drawbacks = RequireStringArray(response, "drawbackModuleIds");
        string[] evidence = RequireStringArray(response, "evidenceFactIds");
        if (positive.Length == 0
            || positive.Length > (int)request["maximumPositiveModules"]
            || drawbacks.Length > (int)request["maximumDrawbackModules"]
            || positive.Distinct(StringComparer.Ordinal).Count() != positive.Length
            || drawbacks.Distinct(StringComparer.Ordinal).Count() != drawbacks.Length
            || evidence.Length == 0
            || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length
            || positive.Any(value => !offers.TryGetValue(value, out string polarity)
                || !string.Equals(polarity, "positive", StringComparison.Ordinal))
            || drawbacks.Any(value => !offers.TryGetValue(value, out string polarity)
                || !string.Equals(polarity, "drawback", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Response row {index + 1} contains an illegal module subset.");
        HashSet<string> offeredEvidence = RequireStringArray(request, "evidenceFactIds")
            .ToHashSet(StringComparer.Ordinal);
        if (evidence.Any(value => !offeredEvidence.Contains(value)))
            throw new InvalidOperationException(
                $"Response row {index + 1} contains an invented evidence ID.");
    }

    private static void ValidateResolutionRows(
        IReadOnlyList<JObject> rows,
        IReadOnlyList<JObject> sources,
        IReadOnlyList<JObject> responses)
    {
        if (rows.Count != 100 || rows.Count != sources.Count || rows.Count != responses.Count)
            throw new InvalidOperationException("Resolution export must contain exactly 100 bound rows.");
        if (rows.Select(value => RequireString(value, "selectionId"))
                .Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidOperationException("Resolution selection IDs are not unique.");
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            JObject row = rows[rowIndex];
            if ((int?)row["schemaVersion"] != 3
                || row["strengthBand"] is not JObject strengthBand
                || !PilotStrengthBands.Any(value => string.Equals(
                    value.Id, (string)strengthBand["id"], StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Resolution rows require bound strength-band metadata.");
            ValidateAllocation(RequireObject(row["csharpAllocation"], "csharpAllocation"));
            bool skill = string.Equals((string)row["profileId"],
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal);
            if (skill != (row["csharpAllocation"]?["skillContext"] is JObject))
                throw new InvalidOperationException(
                    "Resolution skillContext presence must match the CharacterSkill profile.");
            if (skill && !JToken.DeepEquals(
                    sources[rowIndex]["moduleSelectionRequest"]?["skillContext"],
                    row["csharpAllocation"]?["skillContext"]))
            {
                throw new InvalidOperationException(
                    "Resolved CharacterSkill context diverged from the source request context.");
            }
        }
        Dictionary<string, int> minimumSpreads = new(StringComparer.Ordinal)
        {
            ["CharacterSkillModuleSelection"] = 8,
            ["AcquiredTraitModuleSelection"] = 2,
            ["EquipmentEvolutionModuleSelection"] = 2,
            ["FacilityEvolutionModuleSelection"] = 1
        };
        foreach ((string profile, int minimumSpread) in minimumSpreads)
        {
            JObject[] profileRows = rows.Where(value => string.Equals(
                    (string)value["profileId"], profile, StringComparison.Ordinal)).ToArray();
            int[] budgets = profileRows.Select(value =>
                (int)value["csharpAllocation"]?["narrativeBudget"]).ToArray();
            if (budgets.Max() - budgets.Min() < minimumSpread)
                throw new InvalidOperationException(
                    $"{profile} budget spread {budgets.Min()}-{budgets.Max()} is too narrow.");
            double[] byRank = profileRows.GroupBy(value =>
                    (int)value["strengthBand"]?["intendedRank"])
                .OrderBy(group => group.Key)
                .Select(group => group.Average(value =>
                    (int)value["csharpAllocation"]?["narrativeBudget"]))
                .ToArray();
            if (byRank.Length != PilotStrengthBands.Length
                || byRank.Zip(byRank.Skip(1), (left, right) => right >= left).Any(value => !value)
                || byRank[^1] <= byRank[0])
                throw new InvalidOperationException(
                    profile + " strength-band budgets are not monotonically distributed.");
        }
    }

    private static void ValidateAllocation(JObject value)
    {
        int budget = (int)value["narrativeBudget"];
        int positive = (int)value["positiveCost"];
        int credit = (int)value["drawbackCredit"];
        int calculated = (int)value["calculatedCost"];
        int residual = (int?)value["residualBudget"] ?? -1;
        string residualReason = (string)value["residualReason"] ?? string.Empty;
        string burdenEffectId = (string)value["appliedBurdenEffectId"] ?? string.Empty;
        double burdenPotency = (double?)value["appliedBurdenPotency"] ?? 0d;
        string burdenKind = (string)value["positiveBurdenKind"] ?? string.Empty;
        JArray capabilities = value["capabilities"] as JArray;
        JArray burdenEffects = value["burdenEffects"] as JArray;
        if (budget < 0 || positive < 0 || credit < 0 || calculated < 0
            || calculated != positive - credit || calculated > budget
            || burdenKind is not ("NotApplicable" or "None" or "OperatingCost"
                or "OptionalDrawback" or "InseparableRisk")
            || residual != budget - calculated
            || residualReason is not ("none" or "next-quantum-exceeds-budget"
                or "effective-cap-reached")
            || (residual == 0 && residualReason != "none")
            || (residual > 0 && residualReason == "none")
            || (burdenEffectId.Length == 0 && Math.Abs(burdenPotency) > 0.001d)
            || (burdenEffectId.Length > 0 && burdenPotency < 1d)
            || string.IsNullOrWhiteSpace((string)value["mechanicalDescription"])
            || string.IsNullOrWhiteSpace((string)value["mechanicalSummary"])
            || (value["skillContext"] is not JObject
                && value["skillContext"]?.Type != JTokenType.Null)
            || capabilities == null || capabilities.Count == 0
            || burdenEffects == null
            || burdenEffects.OfType<JObject>().Any(burden =>
                string.IsNullOrWhiteSpace((string)burden["burdenId"])
                || string.IsNullOrWhiteSpace((string)burden["displayName"])
                || (string)burden["kind"] is not ("OperatingCost"
                    or "OptionalDrawback" or "InseparableRisk")
                || (int?)burden["budgetCredit"] < 0
                || (double?)burden["potency"] <= 0d
                || burden["effects"] is not JArray effectLines
                || effectLines.Count == 0
                || effectLines.OfType<JObject>().Any(effect =>
                    string.IsNullOrWhiteSpace((string)effect["effectId"])
                    || string.IsNullOrWhiteSpace((string)effect["displayName"])
                    || string.IsNullOrWhiteSpace((string)effect["formattedValue"])))
            || (burdenKind is "OperatingCost" or "InseparableRisk"
                && !burdenEffects.OfType<JObject>().Any(burden =>
                    string.Equals((string)burden["kind"], burdenKind,
                        StringComparison.Ordinal)))
            || (burdenEffectId.Length > 0
                && !burdenEffects.OfType<JObject>().Any(burden =>
                    string.Equals((string)burden["burdenId"], burdenEffectId,
                        StringComparison.Ordinal)))
            || (!string.IsNullOrEmpty((string)value["drawbackId"])
                && burdenEffects.Count == 0)
            || capabilities.OfType<JObject>().Any(capability =>
                string.IsNullOrWhiteSpace((string)capability["capabilityId"])
                || capability["parameters"] is not JArray parameters
                || parameters.Count == 0
                || parameters.OfType<JObject>().Any(parameter =>
                    string.IsNullOrWhiteSpace((string)parameter["parameterId"])
                    || string.IsNullOrWhiteSpace((string)parameter["formattedValue"]))))
            throw new InvalidOperationException(
                "C# allocation is incomplete or escaped its formula budget.");
    }

    private static JObject[] ReadJsonl(string path) => File.ReadLines(path,
            new UTF8Encoding(false, true))
        .Select((line, index) => string.IsNullOrWhiteSpace(line)
            ? throw new InvalidOperationException(
                $"Blank JSONL line at {path}:{index + 1}.")
            : JObject.Parse(line))
        .ToArray();

    private static JObject RequireObject(JToken token, string label) => token as JObject
        ?? throw new InvalidOperationException(label + " must be an object.");

    private static string RequireString(JObject value, string key)
    {
        string result = (string)value[key] ?? string.Empty;
        if (result.Length == 0 || !string.Equals(result, result.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(key + " must be a canonical non-empty string.");
        return result;
    }

    private static string[] RequireStringArray(JObject value, string key)
    {
        if (value[key] is not JArray array || array.Any(item => item.Type != JTokenType.String))
            throw new InvalidOperationException(key + " must be a string array.");
        return array.Select(item => (string)item).ToArray();
    }

    private static int ParsePilotIndex(string targetPersistentId)
    {
        int separator = targetPersistentId.LastIndexOf(':');
        if (separator < 0 || !int.TryParse(targetPersistentId.Substring(separator + 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            || value < 0 || value >= RowsPerProfile)
            throw new InvalidOperationException(
                "Target ID does not contain a valid pilot row index: " + targetPersistentId);
        return value;
    }
}
#endif
