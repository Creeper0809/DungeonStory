#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Strict authored CharacterSkill case schema:
/// state={kind,potential,unlockLevel,requestRevision,pity}; events use only the
/// common bridge fields. At most 16 ordered events and 1,000 records per event
/// are accepted so fixture work stays bounded and every readable event can remain
/// inside the production public-context selection cap.
/// </summary>
public static class NarrativeControlledSkillCapture
{
    private const int MaximumEvents = 16;
    private const int MaximumRecordsPerEvent = 1000;
    private const string Producer =
        "CharacterSkillGenerationService.CreateDraft+"
        + "CharacterSkillCombinationCatalog.Build+"
        + "CharacterSkillCombinationSemanticsFactory.Create+"
        + "CharacterSkillPromptBuilder.BuildEnvelope";

    private static readonly HashSet<string> KnownOutcomes = typeof(CharacterActivityOutcomes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToHashSet(StringComparer.Ordinal);

    public static JObject Capture(JObject input)
    {
        ParsedCase parsed = Parse(input);
        CharacterSkillSystemSettingsSO settings = ResolveSettings();
        ValidateAuthoredSettings(settings);
        CharacterSkillGenerationService authority = new(
            new FixedSettingsProvider(settings),
            new UnavailableRuntimeProvider(),
            new FixedUiClock());

        using ControlledFixture fixture = new(parsed);
        ConfigureProgression(fixture.Progression, parsed);
        IReadOnlyList<NarrativeLedgerPublicationDescriptor> publications =
            RecordAuthoredEvents(fixture.Progression, parsed);

        // The contract requires exactly one production draft call at the caller's
        // revision. There is deliberately no revision search or fallback draft.
        CharacterSkillDraft draft = authority.CreateDraft(
            fixture.Progression,
            parsed.Kind,
            parsed.UnlockLevel,
            parsed.RequestRevision);
        IReadOnlyList<RuleOptions> ruleOptions = RequireAllLegalOptions(draft, settings);
        NarrativePublicContextMaterial publicMaterial =
            CharacterSkillPromptBuilder.BuildPublicMaterial(
                fixture.Progression,
                publications);
        NarrativePublicPromptEnvelope envelope = CharacterSkillPromptBuilder.BuildEnvelope(
            fixture.Progression,
            draft,
            settings,
            publicMaterial);
        if (envelope == null
            || !ReferenceEquals(envelope.Material, publicMaterial)
            || string.IsNullOrWhiteSpace(envelope.Prompt))
        {
            throw new InvalidOperationException(
                "Production CharacterSkill public prompt envelope is unavailable.");
        }

        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(publicMaterial);
        JObject request = BuildRequest(draft, settings, ruleOptions, envelope.Prompt);
        JArray fullLegalCandidates = BuildFullLegalCandidates(ruleOptions, settings, draft.kind);
        RequireRequestCandidateParity(request, fullLegalCandidates);

        return new JObject
        {
            ["scenarioId"] = BuildScenarioId(input),
            ["profileId"] = NarrativeMechanicScenarioProfiles.CharacterSkill,
            ["targetPersistentId"] = publicContext.TargetPersistentId,
            ["accepted"] = true,
            ["failureReason"] = string.Empty,
            ["publicFacts"] = ParseTrustedArray(publicContext.PublicFacts.ToCanonicalString()),
            ["publicNarrativeContext"] = ParseTrustedObject(
                publicContext.PublicNarrativeContext.ToCanonicalString()),
            ["request"] = request,
            ["fullLegalCandidates"] = fullLegalCandidates,
            ["authorityContext"] = BuildAuthorityContext(
                parsed,
                fixture.Progression,
                draft,
                settings,
                publicContext.MaterialSemanticHash)
        };
    }

    private static ParsedCase Parse(JObject input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        NarrativeControlledJson.RequireExactProperties(
            input,
            "CharacterSkill case",
            "caseId",
            "scenarioFamilyId",
            "profileId",
            "subject",
            "events",
            "state");
        string caseId = NarrativeControlledJson.RequireIdentifier(
            input,
            "caseId",
            "CharacterSkill case");
        string familyId = NarrativeControlledJson.RequireIdentifier(
            input,
            "scenarioFamilyId",
            "CharacterSkill case");
        string profileId = NarrativeControlledJson.RequireString(
            input,
            "profileId",
            "CharacterSkill case");
        if (!string.Equals(
                profileId,
                NarrativeMechanicScenarioProfiles.CharacterSkill,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"CharacterSkill capture cannot process profileId '{profileId}'.");
        }

        JObject subject = NarrativeControlledJson.RequireObject(
            input,
            "subject",
            "CharacterSkill case");
        NarrativeControlledJson.RequireExactProperties(
            subject,
            "CharacterSkill subject",
            "persistentId",
            "displayName",
            "background");
        string persistentId = NarrativeControlledJson.RequireIdentifier(
            subject,
            "persistentId",
            "CharacterSkill subject");
        CharacterId characterId = (CharacterId)persistentId;
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                $"CharacterSkill subject persistentId '{persistentId}' is not a valid CharacterId.");
        }
        string displayName = RequireOneLine(
            NarrativeControlledJson.RequireString(
                subject,
                "displayName",
                "CharacterSkill subject"),
            "CharacterSkill subject.displayName");
        string background = RequireOneLine(
            NarrativeControlledJson.RequireString(
                subject,
                "background",
                "CharacterSkill subject"),
            "CharacterSkill subject.background");

        JObject state = NarrativeControlledJson.RequireObject(
            input,
            "state",
            "CharacterSkill case");
        NarrativeControlledJson.RequireExactProperties(
            state,
            "CharacterSkill state",
            "kind",
            "potential",
            "unlockLevel",
            "requestRevision",
            "pity");
        CharacterSkillKind kind = RequireEnum<CharacterSkillKind>(
            NarrativeControlledJson.RequireString(
                state,
                "kind",
                "CharacterSkill state"),
            "CharacterSkill state.kind");
        CharacterPotentialGrade potential = RequireEnum<CharacterPotentialGrade>(
            NarrativeControlledJson.RequireString(
                state,
                "potential",
                "CharacterSkill state"),
            "CharacterSkill state.potential");
        int unlockLevel = NarrativeControlledJson.RequireInt32(
            state,
            "unlockLevel",
            "CharacterSkill state");
        int requestRevision = NarrativeControlledJson.RequireInt32(
            state,
            "requestRevision",
            "CharacterSkill state");
        bool pity = NarrativeControlledJson.RequireBoolean(
            state,
            "pity",
            "CharacterSkill state");
        if (unlockLevel < 1)
        {
            throw new ArgumentException("CharacterSkill unlockLevel must be at least 1.");
        }
        if (requestRevision < 0)
        {
            throw new ArgumentException("CharacterSkill requestRevision cannot be negative.");
        }
        if (pity && kind != CharacterSkillKind.Active)
        {
            throw new ArgumentException(
                "CharacterSkill pity=true is meaningful only for Active drafts.");
        }

        JArray eventArray = NarrativeControlledJson.RequireArray(
            input,
            "events",
            "CharacterSkill case");
        if (eventArray.Count == 0 || eventArray.Count > MaximumEvents)
        {
            throw new ArgumentException(
                $"CharacterSkill events count must be between 1 and {MaximumEvents}.");
        }
        List<ParsedEvent> events = new(eventArray.Count);
        HashSet<string> eventKeys = new(StringComparer.Ordinal);
        int previousDay = -1;
        foreach (JToken eventToken in eventArray)
        {
            if (eventToken is not JObject eventObject)
            {
                throw new ArgumentException("CharacterSkill events must contain only objects.");
            }
            NarrativeControlledJson.RequireExactProperties(
                eventObject,
                "CharacterSkill event",
                "eventKey",
                "text",
                "domain",
                "outcome",
                "day",
                "count",
                "valueDecimal");
            string eventKey = NarrativeControlledJson.RequireIdentifier(
                eventObject,
                "eventKey",
                "CharacterSkill event");
            if (!eventKeys.Add(eventKey))
            {
                throw new ArgumentException(
                    $"CharacterSkill eventKey '{eventKey}' is duplicated.");
            }
            string text = RequireOneLine(
                NarrativeControlledJson.RequireString(
                    eventObject,
                    "text",
                    "CharacterSkill event"),
                "CharacterSkill event.text");
            CharacterNarrativeDomain domain = RequireEnum<CharacterNarrativeDomain>(
                NarrativeControlledJson.RequireString(
                    eventObject,
                    "domain",
                    "CharacterSkill event"),
                "CharacterSkill event.domain");
            string outcome = NarrativeControlledJson.RequireString(
                eventObject,
                "outcome",
                "CharacterSkill event");
            if (!KnownOutcomes.Contains(outcome))
            {
                throw new ArgumentException(
                    $"CharacterSkill event outcome '{outcome}' is not a CharacterActivityOutcomes ID.");
            }
            int day = NarrativeControlledJson.RequireInt32(
                eventObject,
                "day",
                "CharacterSkill event");
            int count = NarrativeControlledJson.RequireInt32(
                eventObject,
                "count",
                "CharacterSkill event");
            if (day < 0)
            {
                throw new ArgumentException("CharacterSkill event day cannot be negative.");
            }
            if (day < previousDay)
            {
                throw new ArgumentException(
                    "CharacterSkill events must be ordered by nondecreasing day.");
            }
            previousDay = day;
            if (count < 1 || count > MaximumRecordsPerEvent)
            {
                throw new ArgumentException(
                    $"CharacterSkill event count must be between 1 and {MaximumRecordsPerEvent}.");
            }
            string valueDecimal = NarrativeControlledJson.RequireString(
                eventObject,
                "valueDecimal",
                "CharacterSkill event");
            decimal parsedDecimal = RequireDecimal(valueDecimal);
            float value = (float)parsedDecimal;
            events.Add(new ParsedEvent(
                eventKey,
                text,
                domain,
                outcome,
                day,
                count,
                valueDecimal,
                value));
        }

        return new ParsedCase(
            caseId,
            familyId,
            persistentId,
            displayName,
            background,
            kind,
            potential,
            unlockLevel,
            requestRevision,
            pity,
            events,
            (JObject)state.DeepClone());
    }

    private static void ConfigureProgression(CharacterProgression progression, ParsedCase parsed)
    {
        CharacterGrowthState growth = progression.GrowthState;
        growth.EnsureCollections();
        growth.initialized = true;
        growth.autoChooseDrafts = false;
        growth.potentialGrade = parsed.Potential;
        growth.generationSeed = StableSeed(parsed.CaseId);
        growth.origin = parsed.Background;
        growth.displayName = parsed.DisplayName;
        growth.nextActiveDraftHasPity = parsed.Pity;
        growth.skillGenerationRevision = parsed.RequestRevision;
        growth.activeSkills.Clear();
        growth.passiveSkills.Clear();
        growth.ultimate = null;
        growth.drafts.Clear();
        growth.pendingRequestKeys.Clear();
        progression.NarrativeLedger.facts.Clear();
    }

    private static IReadOnlyList<NarrativeLedgerPublicationDescriptor> RecordAuthoredEvents(
        CharacterProgression progression,
        ParsedCase parsed)
    {
        List<NarrativeLedgerPublicationDescriptor> descriptors = new(parsed.Events.Count);
        foreach (ParsedEvent authoredEvent in parsed.Events)
        {
            string factId = "controlled-event:" + authoredEvent.EventKey;
            for (int repetition = 0; repetition < authoredEvent.Count; repetition++)
            {
                progression.NarrativeLedger.Record(
                    authoredEvent.Domain,
                    factId,
                    parsed.PersistentId,
                    authoredEvent.Outcome,
                    authoredEvent.Value,
                    authoredEvent.Day);
            }
            descriptors.Add(new NarrativeLedgerPublicationDescriptor(
                authoredEvent.Domain,
                factId,
                parsed.PersistentId,
                authoredEvent.Text,
                authoredEvent.EventKey,
                new NarrativePublicEntityInput(
                    parsed.PersistentId,
                    NarrativePublicEntityKind.Character,
                    parsed.DisplayName)));
        }
        return descriptors;
    }

    private static IReadOnlyList<RuleOptions> RequireAllLegalOptions(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings)
    {
        if (draft?.rules == null || draft.rules.Count == 0 || draft.rules.Any(rule => rule == null))
        {
            throw new InvalidOperationException("Production CharacterSkill draft has no complete rules.");
        }
        int expectedCount = draft.kind == CharacterSkillKind.Active ? 3 : 1;
        if (draft.rules.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Production CharacterSkill draft returned {draft.rules.Count} rules; expected {expectedCount}.");
        }
        string duplicateRuleId = draft.rules
            .GroupBy(rule => rule.ruleId, StringComparer.Ordinal)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)?.Key;
        if (duplicateRuleId != null)
        {
            throw new InvalidOperationException(
                "Production CharacterSkill draft contains blank or duplicate rule IDs.");
        }

        List<RuleOptions> result = new(draft.rules.Count);
        foreach (CharacterSkillCandidateRule rule in draft.rules)
        {
            List<CharacterSkillAllowedCombination> options =
                CharacterSkillCombinationCatalog.Build(rule, settings, draft.kind);
            if (options.Count == 0
                || options.Any(option => option == null
                    || string.IsNullOrWhiteSpace(option.Id)
                    || string.IsNullOrWhiteSpace(option.MechanicalIdentity)))
            {
                throw new InvalidOperationException(
                    $"CharacterSkill rule '{rule.ruleId}' has an empty or invalid legal option pool.");
            }
            if (options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count()
                    != options.Count
                || options.Select(option => option.MechanicalIdentity)
                    .Distinct(StringComparer.Ordinal).Count() != options.Count)
            {
                throw new InvalidOperationException(
                    $"CharacterSkill rule '{rule.ruleId}' has duplicate legal option identities.");
            }
            result.Add(new RuleOptions(rule, options));
        }
        if (!HasDistinctMechanicalSelection(
                result.Select(value => value.Options).ToArray(),
                0,
                new HashSet<string>(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "CharacterSkill rule pools cannot provide distinct mechanical choices for every requested rule.");
        }
        return result;
    }

    private static bool HasDistinctMechanicalSelection(
        IReadOnlyList<List<CharacterSkillAllowedCombination>> pools,
        int index,
        ISet<string> chosen)
    {
        if (index >= pools.Count)
        {
            return true;
        }
        foreach (CharacterSkillAllowedCombination option in pools[index])
        {
            if (!chosen.Add(option.MechanicalIdentity))
            {
                continue;
            }
            if (HasDistinctMechanicalSelection(pools, index + 1, chosen))
            {
                return true;
            }
            chosen.Remove(option.MechanicalIdentity);
        }
        return false;
    }

    private static JObject BuildRequest(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        IReadOnlyList<RuleOptions> ruleOptions,
        string prompt)
    {
        return new JObject
        {
            ["candidateCount"] = draft.rules.Count,
            ["candidatePacket"] = CharacterSkillPromptBuilder.BuildCandidatePacket(draft, settings),
            ["kind"] = draft.kind.ToString(),
            ["prompt"] = prompt,
            ["requestKey"] = draft.requestKey,
            ["requestedUltimateDomain"] = draft.requestedUltimateDomain.ToString(),
            ["rules"] = new JArray(ruleOptions.Select(options =>
                BuildRule(options, settings, draft.kind))),
            ["unlockLevel"] = draft.unlockLevel
        };
    }

    private static JObject BuildRule(
        RuleOptions options,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        CharacterSkillCandidateRule rule = options.Rule;
        return new JObject
        {
            ["allowedModuleIds"] = new JArray((rule.allowedModuleIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)),
            ["allowedVariantIds"] = new JArray((rule.allowedVariantIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)),
            ["budget"] = rule.budget,
            ["combinationOptions"] = new JArray(options.Options.Select(option =>
                BuildCombination(option, rule, kind, settings))),
            ["cooldownTurns"] = rule.cooldownTurns,
            ["mechanicalPolicySource"] = rule.mechanicalPolicySource.ToString(),
            ["rarity"] = rule.rarity.ToString(),
            ["ruleId"] = rule.ruleId,
            ["target"] = rule.target.ToString(),
            ["targetPositions"] = CharacterSkillFormationRules.Format(rule.targetPositions),
            ["trigger"] = rule.trigger.ToString(),
            ["ultimateDomain"] = rule.ultimateDomain.ToString(),
            ["usableFrom"] = CharacterSkillFormationRules.Format(rule.usableFrom)
        };
    }

    private static JArray BuildFullLegalCandidates(
        IReadOnlyList<RuleOptions> ruleOptions,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        return new JArray(ruleOptions.Select(options => new JObject
        {
            ["options"] = new JArray(options.Options.Select(option =>
                BuildCombination(option, options.Rule, kind, settings))),
            ["ruleId"] = options.Rule.ruleId
        }));
    }

    private static JObject BuildCombination(
        CharacterSkillAllowedCombination option,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillCombinationSemanticsDto semantics =
            CharacterSkillCombinationSemanticsFactory.Create(option, rule, kind, settings);
        return new JObject
        {
            ["combinationId"] = option.Id,
            ["cost"] = option.Cost,
            ["mechanicalIdentity"] = option.MechanicalIdentity,
            ["modules"] = new JArray(option.Modules.Select(selection =>
                BuildModuleSelection(selection, settings))),
            ["ruleId"] = option.RuleId,
            ["semantics"] = BuildSemantics(semantics),
            ["signature"] = option.Signature
        };
    }

    private static JObject BuildModuleSelection(
        CharacterSkillModuleSelection selection,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillModuleRule module = settings.FindModule(selection.moduleId);
        CharacterSkillNumericVariant variant = module?.FindVariant(selection.variantId);
        if (module == null || variant == null)
        {
            throw new InvalidOperationException(
                $"Legal CharacterSkill option references missing '{selection?.moduleId}|{selection?.variantId}'.");
        }
        return new JObject
        {
            ["capabilityId"] = CharacterSkillModuleCapabilityRegistry
                .Require(module)
                .CapabilityId,
            ["count"] = variant.count,
            ["cost"] = variant.cost,
            ["duration"] = variant.duration,
            ["moduleClass"] = module.GetType().Name,
            ["moduleDisplayName"] = module.displayName,
            ["moduleId"] = module.id,
            ["primaryValueDecimal"] = Decimal(variant.primaryValue),
            ["secondaryValueDecimal"] = Decimal(variant.secondaryValue),
            ["variantDisplayName"] = variant.displayName,
            ["variantId"] = variant.id
        };
    }

    private static JObject BuildSemantics(CharacterSkillCombinationSemanticsDto semantics)
    {
        JObject result = new()
        {
            ["combinationId"] = semantics.combinationId,
            ["descriptionKo"] = semantics.descriptionKo,
            ["modules"] = new JArray(semantics.modules.Select(module => new JObject
            {
                ["descriptionKo"] = module.descriptionKo,
                ["effectKind"] = module.effectKind,
                ["executionScope"] = module.executionScope,
                ["moduleId"] = module.moduleId,
                ["terms"] = new JArray(module.terms),
                ["variantId"] = module.variantId
            })),
            ["schemaVersion"] = semantics.schemaVersion
        };
        string productionCanonical = CharacterSkillCombinationSemanticsFactory
            .SerializeCanonical(semantics);
        if (!string.Equals(
                NarrativeControlledJson.ToCanonicalString(result),
                productionCanonical,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Controlled CharacterSkill semantics diverged from the production canonical serializer.");
        }
        return result;
    }

    private static JObject BuildAuthorityContext(
        ParsedCase parsed,
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        string publicMaterialSemanticHash)
    {
        return new JObject
        {
            ["fixtureKind"] = "authored-controlled-scenario",
            ["fixtureGenerationSeed"] = progression.GrowthState.generationSeed,
            ["fixtureGenerationSeedSource"] = "sha256-utf8-caseId-first-32-bits",
            ["fixtureInactiveDuringCapture"] = true,
            ["producer"] = Producer,
            ["settingsAssetPath"] = RequireAssetPath(settings),
            ["sourceState"] = parsed.SourceState.DeepClone(),
            ["draft"] = new JObject
            {
                ["kind"] = draft.kind.ToString(),
                ["requestKey"] = draft.requestKey,
                ["requestRevision"] = parsed.RequestRevision,
                ["ruleCount"] = draft.rules.Count,
                ["unlockLevel"] = draft.unlockLevel
            },
            ["ledgerFacts"] = new JArray(progression.NarrativeLedger.Facts
                .Where(fact => fact != null)
                .OrderBy(fact => fact.factId, StringComparer.Ordinal)
                .Select(fact => new JObject
                {
                    ["count"] = fact.count,
                    ["day"] = fact.lastDay,
                    ["domain"] = fact.domain.ToString(),
                    ["factId"] = fact.factId,
                    ["milestoneCount"] = fact.milestoneCount,
                    ["outcome"] = fact.outcome,
                    ["subjectId"] = fact.subjectId,
                    ["totalValueDecimal"] = Decimal(fact.totalValue)
                })),
            ["publicMaterialSemanticHash"] = publicMaterialSemanticHash
        };
    }

    private static void RequireRequestCandidateParity(JObject request, JArray fullLegalCandidates)
    {
        JArray rules = request["rules"] as JArray
            ?? throw new InvalidOperationException("CharacterSkill request.rules is missing.");
        if (rules.Count != fullLegalCandidates.Count)
        {
            throw new InvalidOperationException(
                "CharacterSkill request/fullLegalCandidates rule counts diverged.");
        }
        for (int index = 0; index < rules.Count; index++)
        {
            JObject rule = (JObject)rules[index];
            JObject candidates = (JObject)fullLegalCandidates[index];
            if (!JToken.DeepEquals(rule["ruleId"], candidates["ruleId"])
                || !JToken.DeepEquals(rule["combinationOptions"], candidates["options"]))
            {
                throw new InvalidOperationException(
                    $"CharacterSkill request/fullLegalCandidates diverged at rule index {index}.");
            }
        }
    }

    private static CharacterSkillSystemSettingsSO ResolveSettings()
    {
        string[] paths = AssetDatabase.FindAssets("t:CharacterSkillSystemSettingsSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "controlled-skill-settings-count",
                $"Expected exactly one authored CharacterSkillSystemSettingsSO; found {paths.Length}.");
        }
        CharacterSkillSystemSettingsSO settings =
            AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(paths[0]);
        return settings != null
            ? settings
            : throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "controlled-skill-settings-load",
                $"Could not load authored CharacterSkillSystemSettingsSO at '{paths[0]}'.");
    }

    private static void ValidateAuthoredSettings(CharacterSkillSystemSettingsSO settings)
    {
        IReadOnlyList<CharacterSkillModuleRule> authored = settings.Modules;
        CharacterSkillModuleRule[] modules = authored?
            .Where(module => module != null)
            .ToArray() ?? Array.Empty<CharacterSkillModuleRule>();
        if (modules.Length == 0
            || modules.Length != (authored?.Count ?? 0)
            || modules.Any(module => string.IsNullOrWhiteSpace(module.id)
                || module.variants == null
                || module.variants.Count == 0
                || module.variants.Any(variant => variant == null
                    || string.IsNullOrWhiteSpace(variant.id)
                    || variant.cost <= 0))
            || modules.Select(module => module.id).Distinct(StringComparer.Ordinal).Count()
                != modules.Length)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "controlled-skill-settings-invalid",
                "Authored CharacterSkill settings contain a null, blank/duplicate ID, or invalid variant.");
        }
    }

    private static string RequireAssetPath(UnityEngine.Object value)
    {
        string path = AssetDatabase.GetAssetPath(value)?.Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(path)
            ? path
            : throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "controlled-skill-settings-path",
                "CharacterSkill settings are not a live authored asset.");
    }

    private static T RequireEnum<T>(string value, string label) where T : struct
    {
        if (!Enum.TryParse(value, ignoreCase: false, out T parsed)
            || !Enum.IsDefined(typeof(T), parsed)
            || !string.Equals(parsed.ToString(), value, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{label} has unknown enum name '{value}'.");
        }
        return parsed;
    }

    private static decimal RequireDecimal(string value)
    {
        int index = value.StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
        if (index >= value.Length)
        {
            throw new ArgumentException("valueDecimal must contain decimal digits.");
        }
        int integerStart = index;
        while (index < value.Length && char.IsDigit(value[index]))
        {
            index++;
        }
        int integerDigits = index - integerStart;
        if (integerDigits == 0
            || (integerDigits > 1 && value[integerStart] == '0'))
        {
            throw new ArgumentException(
                $"valueDecimal '{value}' is not a canonical base-10 decimal string.");
        }
        if (index < value.Length && value[index] == '.')
        {
            int fractionStart = ++index;
            while (index < value.Length && char.IsDigit(value[index]))
            {
                index++;
            }
            if (index == fractionStart)
            {
                throw new ArgumentException(
                    $"valueDecimal '{value}' requires digits after its decimal point.");
            }
        }
        if (index != value.Length
            || !decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            throw new ArgumentException(
                $"valueDecimal '{value}' is not a supported decimal string.");
        }
        return parsed;
    }

    private static string RequireOneLine(string value, string label)
    {
        if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
        {
            throw new ArgumentException($"{label} must be one readable line.");
        }
        return value;
    }

    private static int StableSeed(string value)
    {
        string hash = NarrativeControlledJson.Sha256Prefixed(
            System.Text.Encoding.UTF8.GetBytes(value));
        return unchecked((int)uint.Parse(
            hash.Substring(7, 8),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture));
    }

    private static string BuildScenarioId(JObject input)
    {
        string hash = NarrativeControlledJson.Sha256Prefixed(
            NarrativeControlledJson.ToCanonicalUtf8(input));
        return "controlled-character-skill:" + hash.Substring("sha256:".Length);
    }

    private static string Decimal(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static JObject ParseTrustedObject(string canonical) =>
        JObject.Parse(canonical);

    private static JArray ParseTrustedArray(string canonical) =>
        JArray.Parse(canonical);

    private sealed class FixedSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public FixedSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class UnavailableRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime() => throw new InvalidOperationException(
            "Controlled CharacterSkill capture never invokes a local LLM runtime.");
    }

    private sealed class FixedUiClock : IUiClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
    }

    private sealed class ControlledFixture : IDisposable
    {
        private readonly CharacterSO data;

        public ControlledFixture(ParsedCase parsed)
        {
            Root = CharacterAiPlanDebugFixtures.CreateActorObject(
                "NarrativeControlledSkill_" + parsed.CaseId);
            try
            {
                Root.SetActive(false);
                Actor = Root.GetComponent<CharacterActor>();
                if (Actor == null)
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill case '{parsed.CaseId}' could not create CharacterActor.");
                }
                data = CharacterAiPlanDebugFixtures.CreateCharacterData(
                    CharacterType.NPC,
                    parsed.DisplayName,
                    "human");
                Actor.EnsureRuntimeState();
                Actor.Identity.SetPersistentId(parsed.PersistentId);
                Actor.Initialization(data);
                Progression = Actor.Progression;
                if (Progression == null)
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill case '{parsed.CaseId}' could not create CharacterProgression.");
                }
            }
            catch
            {
                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
                if (data != null)
                {
                    UnityEngine.Object.DestroyImmediate(data);
                }
                throw;
            }
        }

        public GameObject Root { get; }
        public CharacterActor Actor { get; }
        public CharacterProgression Progression { get; }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
            if (data != null)
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }

    private sealed class ParsedCase
    {
        public ParsedCase(
            string caseId,
            string familyId,
            string persistentId,
            string displayName,
            string background,
            CharacterSkillKind kind,
            CharacterPotentialGrade potential,
            int unlockLevel,
            int requestRevision,
            bool pity,
            IReadOnlyList<ParsedEvent> events,
            JObject sourceState)
        {
            CaseId = caseId;
            FamilyId = familyId;
            PersistentId = persistentId;
            DisplayName = displayName;
            Background = background;
            Kind = kind;
            Potential = potential;
            UnlockLevel = unlockLevel;
            RequestRevision = requestRevision;
            Pity = pity;
            Events = events;
            SourceState = sourceState;
        }

        public string CaseId { get; }
        public string FamilyId { get; }
        public string PersistentId { get; }
        public string DisplayName { get; }
        public string Background { get; }
        public CharacterSkillKind Kind { get; }
        public CharacterPotentialGrade Potential { get; }
        public int UnlockLevel { get; }
        public int RequestRevision { get; }
        public bool Pity { get; }
        public IReadOnlyList<ParsedEvent> Events { get; }
        public JObject SourceState { get; }
    }

    private sealed class ParsedEvent
    {
        public ParsedEvent(
            string eventKey,
            string text,
            CharacterNarrativeDomain domain,
            string outcome,
            int day,
            int count,
            string valueDecimal,
            float value)
        {
            EventKey = eventKey;
            Text = text;
            Domain = domain;
            Outcome = outcome;
            Day = day;
            Count = count;
            ValueDecimal = valueDecimal;
            Value = value;
        }

        public string EventKey { get; }
        public string Text { get; }
        public CharacterNarrativeDomain Domain { get; }
        public string Outcome { get; }
        public int Day { get; }
        public int Count { get; }
        public string ValueDecimal { get; }
        public float Value { get; }
    }

    private sealed class RuleOptions
    {
        public RuleOptions(
            CharacterSkillCandidateRule rule,
            List<CharacterSkillAllowedCombination> options)
        {
            Rule = rule;
            Options = options;
        }

        public CharacterSkillCandidateRule Rule { get; }
        public List<CharacterSkillAllowedCombination> Options { get; }
    }
}
#endif
