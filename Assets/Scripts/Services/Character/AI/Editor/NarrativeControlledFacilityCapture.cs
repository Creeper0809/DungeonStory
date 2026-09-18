#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only boundary for one caller-authored, synthetic facility-evolution
/// recommendation.  It creates no assets and never applies an evolution: the
/// fixture exists only long enough to ask the production candidate and public
/// context authorities what may be recommended.
/// </summary>
public static class NarrativeControlledFacilityCapture
{
    private const string ProfileId = "FacilityEvolution";

    private sealed class AuthoredEvent
    {
        public string EventKey;
        public string Text;
        public string Domain;
        public string Outcome;
        public int Day;
        public int Count;
        public string ValueDecimal;

        public JObject ToAuditJson()
        {
            return new JObject
            {
                ["count"] = Count,
                ["day"] = Day,
                ["domain"] = Domain,
                ["eventKey"] = EventKey,
                ["outcome"] = Outcome,
                ["text"] = Text,
                ["valueDecimal"] = ValueDecimal
            };
        }
    }

    private sealed class InputCase
    {
        public string CaseId;
        public string ScenarioFamilyId;
        public string SubjectPersistentId;
        public string SubjectDisplayName;
        public string SubjectBackground;
        public IReadOnlyList<AuthoredEvent> Events;
        public string SourceFacilityId;
        public int StarGrade;
        public string[] LineageTags;
        public string[] MutationTags;
        public string[] UnlockedResearchIds;
    }

    private sealed class AuthoredRecipeQuery : IFacilityEvolutionRecipeQuery,
        IFacilityEvolutionRecipeProvider
    {
        private readonly IReadOnlyList<FacilityEvolutionRecipeSO> recipes;
        private readonly IFacilityEvolutionStateComponentFactory states;

        public AuthoredRecipeQuery(
            IReadOnlyList<FacilityEvolutionRecipeSO> recipes,
            IFacilityEvolutionStateComponentFactory states)
        {
            this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            this.states = states ?? throw new ArgumentNullException(nameof(states));
        }

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes() => recipes;

        public bool IsVisible(
            FacilityEvolutionRecipeSO recipe,
            BlueprintResearchState researchState)
        {
            return FacilityEvolutionService.IsRecipeVisible(recipe, researchState, null);
        }

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(
            BlueprintResearchState researchState)
        {
            return recipes.Where(recipe => IsVisible(recipe, researchState)).ToArray();
        }

        public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
            BuildableObject facility,
            BlueprintResearchState researchState)
        {
            return FacilityEvolutionService.GetSourceCandidates(
                facility,
                recipes,
                researchState,
                this,
                states);
        }
    }

    private sealed class FacilityFixture : IDisposable
    {
        private FacilityFixture(
            GameObject root,
            BuildableObject facility,
            FacilityEvolutionStateComponent state,
            BlueprintResearchState research,
            FacilityEvolutionContext context,
            IReadOnlyList<FacilityEvolutionRecipeSO> legalCandidates)
        {
            Root = root;
            Facility = facility;
            State = state;
            Research = research;
            Context = context;
            LegalCandidates = legalCandidates;
        }

        public GameObject Root { get; }
        public BuildableObject Facility { get; }
        public FacilityEvolutionStateComponent State { get; }
        public BlueprintResearchState Research { get; }
        public FacilityEvolutionContext Context { get; }
        public IReadOnlyList<FacilityEvolutionRecipeSO> LegalCandidates { get; }

        public static FacilityFixture Create(
            InputCase input,
            BuildingSO source,
            IReadOnlyList<FacilityEvolutionRecipeSO> recipes)
        {
            GameObject root = new GameObject("NarrativeControlledFacility_" + input.CaseId);
            try
            {
                root.SetActive(false);
                BuildableObject facility = root.AddComponent<BuildableObject>();
                BuildingInstanceId persistentId = (BuildingInstanceId)input.SubjectPersistentId;
                if (!persistentId.IsValid)
                {
                    throw Unsupported(
                        "controlled-facility-subject-id-invalid",
                        "Facility subject.persistentId must be a valid building:<id> value.");
                }

                facility.RestorePersistentIdentity(persistentId);
                CharacterAiEditorTestDependencies.Inject(facility);
                facility.Initialization(source, Vector2Int.zero);

                FacilityEvolutionStateComponentFactory states =
                    new FacilityEvolutionStateComponentFactory();
                FacilityEvolutionStateComponent state = states.GetOrAdd(facility);
                ApplyAuthoredState(state, input);
                RequireExactFacilityState(state, input, source);

                BlueprintResearchState research = new BlueprintResearchState();
                foreach (string researchId in input.UnlockedResearchIds)
                {
                    if (!research.UnlockRecipe(researchId))
                    {
                        throw Unsupported(
                            "controlled-facility-research-state-duplicate",
                            "Facility unlockedResearchIds contains a duplicate value: "
                            + researchId + ".");
                    }
                }

                AuthoredRecipeQuery query = new AuthoredRecipeQuery(recipes, states);
                IReadOnlyList<FacilityEvolutionRecipeSO> legal = query.GetSourceCandidates(
                    facility,
                    research);
                if (legal == null || legal.Count == 0)
                {
                    throw Unsupported(
                        "controlled-facility-no-source-candidates",
                        "Production FacilityEvolutionService.GetSourceCandidates returned no "
                        + "visible source/star-grade candidates for source facility '"
                        + input.SourceFacilityId + "'.");
                }

                RoomProfile profile = new RoomProfile(facility, null);
                // The formatter is the authority for public facility facts.  Keep the
                // caller-authored background explicit as a public fact, followed by
                // each authored event in its original order.  No room score, room
                // tag, resource, or completion state is supplied to this fixture.
                profile.AddRecentEvent("Facility background: " + input.SubjectBackground);
                foreach (AuthoredEvent authoredEvent in input.Events)
                {
                    profile.AddRecentEvent(FormatPublicEventText(authoredEvent));
                }

                FacilityEvolutionContext context = new FacilityEvolutionContext(
                    facility,
                    state,
                    profile,
                    legal);
                return new FacilityFixture(root, facility, state, research, context, legal);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }

    /// <summary>
    /// Captures exactly one FacilityEvolution case. Input must already have been
    /// parsed with duplicate-property rejection by the batch exporter; a JObject
    /// cannot retain duplicate property evidence after Newtonsoft materializes it.
    /// </summary>
    public static JObject Capture(JObject input)
    {
        InputCase authored = ParseInput(input);
        FacilityEvolutionRecipeSO[] recipes = LoadValidRecipes();
        BuildingSO source = ResolveSourceFacility(authored.SourceFacilityId);
        ValidateAuthoredStateReferences(authored, source, recipes);
        RequireSubjectNameMatchesSource(authored.SubjectDisplayName, source);

        using (FacilityFixture fixture = FacilityFixture.Create(authored, source, recipes))
        {
            NarrativePublicContextMaterial material =
                FacilityEvolutionPromptFormatter.BuildPublicMaterial(fixture.Context);
            NarrativePublicPromptEnvelope envelope =
                FacilityEvolutionPromptFormatter.BuildPromptEnvelope(fixture.Context);
            NarrativeMechanicScenarioPublicContext publicContext =
                NarrativeMechanicScenarioPublicContextSerializer.Serialize(material);
            if (!string.Equals(
                    publicContext.TargetPersistentId,
                    authored.SubjectPersistentId,
                    StringComparison.Ordinal))
            {
                throw Unsupported(
                    "controlled-facility-public-subject-mismatch",
                    "The production facility formatter did not retain subject.persistentId "
                    + "as the public-context target.");
            }

            JArray publicFacts = ParseArray(
                publicContext.PublicFacts.ToCanonicalString(),
                "controlled-facility-public-facts-invalid");
            JObject publicNarrativeContext = ParseObject(
                publicContext.PublicNarrativeContext.ToCanonicalString(),
                "controlled-facility-public-context-invalid");
            RequirePublicText(publicFacts, authored.SubjectBackground, "subject.background");
            foreach (AuthoredEvent authoredEvent in authored.Events)
            {
                RequirePublicText(
                    publicFacts,
                    FormatPublicEventText(authoredEvent),
                    "events metadata/text");
            }

            JArray candidates = new JArray();
            JArray candidateIds = new JArray();
            JArray candidateOrder = new JArray();
            foreach (FacilityEvolutionRecipeSO recipe in fixture.LegalCandidates)
            {
                candidates.Add(ProjectCandidate(recipe, candidates.Count));
                candidateIds.Add(recipe.EffectiveId);
                candidateOrder.Add(recipe.EffectiveId);
            }

            JObject request = new JObject
            {
                ["candidateIds"] = candidateIds,
                ["prompt"] = envelope.Prompt,
                ["requestSignature"] = FacilityEvolutionPromptFormatter.BuildSignature(
                    fixture.Context)
            };

            JObject stateAudit = new JObject
            {
                ["lineageTags"] = ToArray(authored.LineageTags),
                ["mutationTags"] = ToArray(authored.MutationTags),
                ["starGrade"] = authored.StarGrade,
                ["unlockedResearchIds"] = ToArray(authored.UnlockedResearchIds)
            };
            JObject authorityContext = new JObject
            {
                ["authoredEvents"] = new JArray(authored.Events.Select(value => value.ToAuditJson())),
                ["candidateOrder"] = candidateOrder,
                ["candidateSelectionAuthority"] =
                    "FacilityEvolutionService.GetSourceCandidates",
                ["recommendationBoundary"] =
                    "Candidate inclusion confirms only source matching, recipe visibility, and star-grade eligibility. "
                    + "This synthetic fixture does not claim room conditions, identity pressures, record tokens, "
                    + "materials, costs, building replacement, or final evolution are satisfied.",
                ["sourceFacilityDisplayName"] = FacilityShopService.GetBuildingName(source),
                ["sourceFacilityId"] = authored.SourceFacilityId,
                ["state"] = stateAudit,
                ["structuredFormatterMetadata"] =
                    "FacilityEvolutionPromptFormatter assigns its own public-event day/count fields. "
                    + "The caller-authored day, count, domain, outcome, and valueDecimal are conveyed "
                    + "in each formatter event text; this capture does not claim exact structured metadata parity.",
                ["subjectDisplayName"] = authored.SubjectDisplayName,
                ["subjectPersistentId"] = authored.SubjectPersistentId
            };

            return new JObject
            {
                ["accepted"] = true,
                ["authorityContext"] = authorityContext,
                ["failureReason"] = string.Empty,
                ["fullLegalCandidates"] = candidates,
                ["profileId"] = ProfileId,
                ["publicFacts"] = publicFacts,
                ["publicNarrativeContext"] = publicNarrativeContext,
                ["request"] = request,
                ["scenarioId"] = "controlled-facility-evolution:" + authored.CaseId,
                ["targetPersistentId"] = publicContext.TargetPersistentId
            };
        }
    }

    private static InputCase ParseInput(JObject input)
    {
        RequireExactKeys(
            input,
            "case",
            "caseId",
            "scenarioFamilyId",
            "profileId",
            "subject",
            "events",
            "state");

        string profileId = RequireText(input["profileId"], "profileId");
        if (!string.Equals(profileId, ProfileId, StringComparison.Ordinal))
        {
            throw Unsupported(
                "controlled-facility-profile-unsupported",
                "NarrativeControlledFacilityCapture only accepts profileId '"
                + ProfileId + "'.");
        }

        JObject subject = RequireObject(input["subject"], "subject");
        RequireExactKeys(subject, "subject", "persistentId", "displayName", "background");

        JObject state = RequireObject(input["state"], "state");
        RequireExactKeys(
            state,
            "state",
            "sourceFacilityId",
            "starGrade",
            "lineageTags",
            "mutationTags",
            "unlockedResearchIds");

        return new InputCase
        {
            CaseId = RequireText(input["caseId"], "caseId"),
            ScenarioFamilyId = RequireText(input["scenarioFamilyId"], "scenarioFamilyId"),
            SubjectPersistentId = RequireText(subject["persistentId"], "subject.persistentId"),
            SubjectDisplayName = RequireText(subject["displayName"], "subject.displayName"),
            SubjectBackground = RequireText(subject["background"], "subject.background"),
            Events = ParseEvents(RequireArray(input["events"], "events")),
            SourceFacilityId = RequireText(state["sourceFacilityId"], "state.sourceFacilityId"),
            StarGrade = RequireInt(state["starGrade"], "state.starGrade", 1),
            LineageTags = ParseTextArray(state["lineageTags"], "state.lineageTags"),
            MutationTags = ParseTextArray(state["mutationTags"], "state.mutationTags"),
            UnlockedResearchIds = ParseTextArray(
                state["unlockedResearchIds"],
                "state.unlockedResearchIds")
        };
    }

    private static IReadOnlyList<AuthoredEvent> ParseEvents(JArray values)
    {
        if (values.Count == 0)
        {
            throw Unsupported(
                "controlled-facility-events-empty",
                "Facility controlled input requires one or more ordered events.");
        }

        HashSet<string> eventKeys = new HashSet<string>(StringComparer.Ordinal);
        List<AuthoredEvent> result = new List<AuthoredEvent>(values.Count);
        int previousDay = -1;
        for (int index = 0; index < values.Count; index++)
        {
            JObject value = RequireObject(values[index], "events[" + index + "]");
            RequireExactKeys(
                value,
                "events[" + index + "]",
                "eventKey",
                "text",
                "domain",
                "outcome",
                "day",
                "count",
                "valueDecimal");
            string eventKey = RequireText(value["eventKey"], "events.eventKey");
            if (!eventKeys.Add(eventKey))
            {
                throw Unsupported(
                    "controlled-facility-event-key-duplicate",
                    "Facility controlled input has a duplicate eventKey: " + eventKey + ".");
            }

            string decimalText = RequireText(
                value["valueDecimal"],
                "events.valueDecimal");
            if (!decimal.TryParse(
                    decimalText,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                throw Unsupported(
                    "controlled-facility-event-decimal-invalid",
                    "Facility events.valueDecimal must be an invariant decimal string.");
            }

            string domainText = RequireText(value["domain"], "events.domain");
            if (!Enum.TryParse(
                    domainText,
                    ignoreCase: false,
                    out CharacterNarrativeDomain domain)
                || !Enum.IsDefined(typeof(CharacterNarrativeDomain), domain)
                || !string.Equals(domain.ToString(), domainText, StringComparison.Ordinal))
            {
                throw Unsupported(
                    "controlled-facility-event-domain-unknown",
                    "Facility events.domain must name an authored CharacterNarrativeDomain: "
                    + domainText + ".");
            }

            int day = RequireInt(value["day"], "events.day", 0);
            if (day < previousDay)
            {
                throw Unsupported(
                    "controlled-facility-event-day-order",
                    "Facility events must be ordered by nondecreasing day.");
            }
            previousDay = day;

            result.Add(new AuthoredEvent
            {
                EventKey = eventKey,
                Text = RequireText(value["text"], "events.text"),
                Domain = domainText,
                Outcome = RequireText(value["outcome"], "events.outcome"),
                Day = day,
                Count = RequireInt(value["count"], "events.count", 1),
                ValueDecimal = decimalText
            });
        }

        return result;
    }

    private static void ValidateAuthoredStateReferences(
        InputCase input,
        BuildingSO source,
        IReadOnlyList<FacilityEvolutionRecipeSO> recipes)
    {
        HashSet<string> knownLineageTags = new HashSet<string>(
            FacilityEvolutionUtility.GetDefaultLineageTags(source)
                .Where(tag => !string.IsNullOrWhiteSpace(tag)),
            StringComparer.Ordinal);
        HashSet<string> knownMutationTags = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> knownResearchIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (FacilityEvolutionRecipeSO recipe in recipes)
        {
            foreach (BuildingSO building in (recipe.fromFacilities ?? Array.Empty<BuildingSO>())
                         .Concat(new[] { recipe.resultBuilding }))
            {
                foreach (string tag in FacilityEvolutionUtility.GetDefaultLineageTags(building))
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        knownLineageTags.Add(tag);
                    }
                }
            }
            foreach (string tag in recipe.fromLineageTags ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    knownLineageTags.Add(tag);
                }
            }
            foreach (string tag in recipe.allowedMutationTags ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    knownMutationTags.Add(tag);
                }
            }
            if (!string.IsNullOrWhiteSpace(recipe.requiredResearchRecipeId))
            {
                knownResearchIds.Add(recipe.requiredResearchRecipeId);
            }
        }

        RequireKnownValues(
            input.LineageTags,
            knownLineageTags,
            "controlled-facility-lineage-tag-unknown",
            "state.lineageTags");
        RequireKnownValues(
            input.MutationTags,
            knownMutationTags,
            "controlled-facility-mutation-tag-unknown",
            "state.mutationTags");
        RequireKnownValues(
            input.UnlockedResearchIds,
            knownResearchIds,
            "controlled-facility-research-id-unknown",
            "state.unlockedResearchIds");
    }

    private static void ApplyAuthoredState(FacilityEvolutionStateComponent state, InputCase input)
    {
        FacilityEvolutionStateSnapshot snapshot = state.CreateSnapshot();
        snapshot.starGrade = input.StarGrade;
        snapshot.lineageTags = input.LineageTags.ToArray();
        snapshot.mutationTags = input.MutationTags.ToArray();
        state.ApplySnapshot(snapshot);
    }

    private static void RequireExactFacilityState(
        FacilityEvolutionStateComponent state,
        InputCase input,
        BuildingSO source)
    {
        if (state == null
            || state.StarGrade != input.StarGrade
            || !SameOrdinalSet(state.LineageTags, input.LineageTags)
            || !SameOrdinalSet(state.MutationTags, input.MutationTags)
            || !string.Equals(
                state.CurrentFacilityId,
                FacilityEvolutionUtility.GetFacilityId(source),
                StringComparison.Ordinal))
        {
            throw Unsupported(
                "controlled-facility-state-application-mismatch",
                "The production facility evolution state did not retain the exact authored "
                + "source, star grade, lineage-tag, and mutation-tag membership.");
        }
    }

    private static bool SameOrdinalSet(
        IEnumerable<string> left,
        IEnumerable<string> right)
    {
        return new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal)
            .SetEquals(right ?? Array.Empty<string>());
    }

    private static FacilityEvolutionRecipeSO[] LoadValidRecipes()
    {
        string[] paths = AssetDatabase.FindAssets("t:FacilityEvolutionRecipeSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0)
        {
            throw Unsupported(
                "controlled-facility-recipes-missing",
                "No valid authored FacilityEvolutionRecipeSO assets are available.");
        }

        List<FacilityEvolutionRecipeSO> recipes =
            new List<FacilityEvolutionRecipeSO>(paths.Length);
        foreach (string path in paths)
        {
            FacilityEvolutionRecipeSO recipe =
                AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(path);
            if (recipe == null)
            {
                throw Unsupported(
                    "controlled-facility-recipe-load-failed",
                    "An authored FacilityEvolutionRecipeSO asset could not be loaded: "
                    + path + ".");
            }
            if (!recipe.HasValidData)
            {
                throw Unsupported(
                    "controlled-facility-recipe-invalid",
                    "An authored FacilityEvolutionRecipeSO asset is invalid and cannot be "
                    + "silently omitted: " + path + ".");
            }
            recipes.Add(recipe);
        }

        return recipes
            .OrderBy(recipe => recipe.EffectiveId, StringComparer.Ordinal)
            .ToArray();
    }

    private static BuildingSO ResolveSourceFacility(string sourceFacilityId)
    {
        int sourceBuildingId = ParseCanonicalBuildingDefinitionId(sourceFacilityId);
        BuildingSO[] matches = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null
                && building.id == sourceBuildingId)
            .ToArray();
        if (matches.Length != 1)
        {
            throw Unsupported(
                matches.Length == 0
                    ? "controlled-facility-source-unknown"
                    : "controlled-facility-source-ambiguous",
                matches.Length == 0
                    ? "state.sourceFacilityId does not resolve to one authored BuildingSO: "
                    + sourceFacilityId + "."
                    : "state.sourceFacilityId resolves to multiple authored BuildingSO assets: "
                    + sourceFacilityId + ".");
        }
        return matches[0];
    }

    private static int ParseCanonicalBuildingDefinitionId(string value)
    {
        const string prefix = "building:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw Unsupported(
                "controlled-facility-source-id-format",
                "state.sourceFacilityId must be a canonical building:<positive-integer> ID.");
        }

        string numericText = value.Substring(prefix.Length);
        if (!int.TryParse(
                numericText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int buildingId)
            || buildingId <= 0
            || !string.Equals(
                numericText,
                buildingId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            throw Unsupported(
                "controlled-facility-source-id-format",
                "state.sourceFacilityId must be a canonical building:<positive-integer> ID.");
        }

        return buildingId;
    }

    private static void RequireSubjectNameMatchesSource(string subjectDisplayName, BuildingSO source)
    {
        string sourceName = FacilityShopService.GetBuildingName(source)?.Trim() ?? string.Empty;
        if (sourceName.Length == 0)
        {
            throw Unsupported(
                "controlled-facility-source-name-empty",
                "The authored source facility has no display name.");
        }

        bool exact = string.Equals(subjectDisplayName, sourceName, StringComparison.Ordinal);
        bool explicitAlias = subjectDisplayName.StartsWith(
                sourceName + " — ",
                StringComparison.Ordinal)
            || subjectDisplayName.StartsWith(sourceName + ": ", StringComparison.Ordinal)
            || subjectDisplayName.StartsWith(sourceName + " (", StringComparison.Ordinal);
        if (!exact && !explicitAlias)
        {
            throw Unsupported(
                "controlled-facility-subject-name-source-mismatch",
                "subject.displayName must equal the authored source facility name or begin "
                + "with that name as an explicit authored alias; result-facility names cannot "
                + "masquerade as the source.");
        }
    }

    private static string FormatPublicEventText(AuthoredEvent value)
    {
        if (value == null)
        {
            throw Unsupported(
                "controlled-facility-event-null",
                "Facility controlled input cannot contain a null event.");
        }

        return "[authored facility event day="
            + value.Day.ToString(CultureInfo.InvariantCulture)
            + "; count=" + value.Count.ToString(CultureInfo.InvariantCulture)
            + "; domain=" + value.Domain
            + "; outcome=" + value.Outcome
            + "; valueDecimal=" + value.ValueDecimal
            + "] " + value.Text;
    }

    private static JObject ProjectCandidate(FacilityEvolutionRecipeSO recipe, int index)
    {
        if (recipe == null || !recipe.HasValidData)
        {
            throw Unsupported(
                "controlled-facility-candidate-invalid",
                "Production source candidates contain an invalid FacilityEvolutionRecipeSO.");
        }

        return new JObject
        {
            ["allowedMutationTags"] = ToArray(recipe.allowedMutationTags ?? Array.Empty<string>()),
            ["candidateIndex"] = index,
            ["displayName"] = recipe.DisplayName,
            ["recipeId"] = recipe.EffectiveId,
            ["requiredStarGrade"] = recipe.requiredStarGrade
        };
    }

    private static void RequireKnownValues(
        IEnumerable<string> values,
        ISet<string> known,
        string failureCode,
        string fieldName)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!known.Contains(value))
            {
                throw Unsupported(
                    failureCode,
                    fieldName + " contains no authored facility reference: " + value + ".");
            }
        }
    }

    private static string[] ParseTextArray(JToken token, string fieldName)
    {
        JArray values = RequireArray(token, fieldName);
        List<string> result = new List<string>(values.Count);
        HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = RequireText(values[index], fieldName + "[" + index + "]");
            if (!distinct.Add(value))
            {
                throw Unsupported(
                    "controlled-facility-state-array-duplicate",
                    fieldName + " cannot contain duplicate values: " + value + ".");
            }
            result.Add(value);
        }
        return result.ToArray();
    }

    private static void RequireExactKeys(
        JObject value,
        string label,
        params string[] expectedKeys)
    {
        if (value == null)
        {
            throw Unsupported(
                "controlled-facility-object-missing",
                label + " must be an object.");
        }

        HashSet<string> expected = new HashSet<string>(expectedKeys, StringComparer.Ordinal);
        HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (JProperty property in value.Properties())
        {
            if (!actual.Add(property.Name))
            {
                throw Unsupported(
                    "controlled-facility-input-key-duplicate",
                    label + " contains duplicate key '" + property.Name + "'.");
            }
            if (!expected.Contains(property.Name))
            {
                throw Unsupported(
                    "controlled-facility-input-key-unknown",
                    label + " contains unsupported key '" + property.Name + "'.");
            }
        }

        string missing = expected.FirstOrDefault(key => !actual.Contains(key));
        if (missing != null)
        {
            throw Unsupported(
                "controlled-facility-input-key-missing",
                label + " is missing required key '" + missing + "'.");
        }
    }

    private static JObject RequireObject(JToken token, string fieldName)
    {
        if (token is not JObject value)
        {
            throw Unsupported(
                "controlled-facility-input-object-invalid",
                fieldName + " must be a JSON object.");
        }
        return value;
    }

    private static JArray RequireArray(JToken token, string fieldName)
    {
        if (token is not JArray value)
        {
            throw Unsupported(
                "controlled-facility-input-array-invalid",
                fieldName + " must be a JSON array.");
        }
        return value;
    }

    private static string RequireText(JToken token, string fieldName)
    {
        if (token == null || token.Type != JTokenType.String)
        {
            throw Unsupported(
                "controlled-facility-input-text-invalid",
                fieldName + " must be a non-empty JSON string.");
        }

        string value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw Unsupported(
                "controlled-facility-input-text-noncanonical",
                fieldName + " must be a non-empty, already-trimmed string.");
        }
        return value;
    }

    private static int RequireInt(JToken token, string fieldName, int minimum)
    {
        if (token == null || token.Type != JTokenType.Integer)
        {
            throw Unsupported(
                "controlled-facility-input-integer-invalid",
                fieldName + " must be an integer JSON number.");
        }

        long value = token.Value<long>();
        if (value < minimum || value > int.MaxValue)
        {
            throw Unsupported(
                "controlled-facility-input-integer-range",
                fieldName + " is outside the supported range.");
        }
        return (int)value;
    }

    private static void RequirePublicText(JArray publicFacts, string text, string sourceField)
    {
        bool found = publicFacts.OfType<JObject>().Any(fact =>
            (fact["text"]?.Value<string>() ?? string.Empty).IndexOf(
                text,
                StringComparison.Ordinal) >= 0);
        if (!found)
        {
            throw Unsupported(
                "controlled-facility-public-fact-missing",
                "The production public formatter did not preserve " + sourceField
                + " text in publicFacts.");
        }
    }

    private static JArray ParseArray(string json, string failureCode)
    {
        try
        {
            return JArray.Parse(json);
        }
        catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonReaderException)
        {
            throw Unsupported(failureCode, "Canonical public facts could not be parsed: " + error.Message);
        }
    }

    private static JObject ParseObject(string json, string failureCode)
    {
        try
        {
            return JObject.Parse(json);
        }
        catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonReaderException)
        {
            throw Unsupported(failureCode, "Canonical public narrative context could not be parsed: " + error.Message);
        }
    }

    private static JArray ToArray(IEnumerable<string> values)
    {
        return new JArray((values ?? Array.Empty<string>()).Select(value => new JValue(value)));
    }

    private static NarrativeMechanicCatalogUnsupportedSourceException Unsupported(
        string code,
        string message)
    {
        return new NarrativeMechanicCatalogUnsupportedSourceException(code, message);
    }
}
#endif
