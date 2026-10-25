using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class R21CharacterAiActionAssetCatalogDebugScenarios
{
    [MenuItem("Tools/QA/R21/Character AI Action Asset Catalog")]
    public static void RunAll()
    {
        IGameContentCatalog root = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ICharacterAiActionAssetCatalog catalog =
            new ResourceCharacterAiActionAssetCatalog(root);
        ActionBinding[] bindings = CreateBindings();
        AIActionSet[] originals = bindings
            .Select(binding => binding.Resolve(catalog))
            .ToArray();

        Require(originals.Length == 19,
            $"Expected 19 required AI actions, found {originals.Length}.");
        Require(originals.Distinct().Count() == originals.Length,
            "Required AI action type and branch pairs did not resolve to 19 distinct assets.");
        Require(bindings.Select((binding, index) =>
                originals[index].GetType() == binding.Type
                && originals[index].Branch == binding.Branch
                && originals[index].CompositionRole
                    == CharacterAiActionCompositionRole.RequiredDefault)
                .All(value => value),
            "A required AI action resolved with the wrong type, branch, or composition role.");
        Require(originals.All(action => root.GetAll<AIActionSet>().Contains(action)),
            "A required AI action was resolved outside the root content catalog.");
        AIAction[] ownerActions = AIBrainActionConfiguration.ConfigureOwner(
            catalog,
            null);
        AIAction[] staffActions = AIBrainActionConfiguration.ConfigureStaff(
            Array.Empty<AIAction>(),
            catalog,
            null);
        Require(ownerActions.Length == 16
                && staffActions.Length == 17
                && ownerActions.All(action => originals.Contains(action.actionset))
                && staffActions.All(action => originals.Contains(action.actionset)),
            "Owner or staff production configuration did not retain the required root actions.");
        VerifyConfiguredVariants(catalog, originals);

        SourceState[] states = originals.Select(SourceState.Capture).ToArray();
        try
        {
            for (int index = 0; index < originals.Length; index++)
            {
                originals[index].name = "R21 Renamed Required Action";
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                AIActionSet resolved = bindings[index].Resolve(catalog);
                Require(ReferenceEquals(resolved, originals[index]),
                    $"Renaming changed {bindings[index].Type.Name} "
                    + $"branch {bindings[index].Branch} resolution.");
            }
        }
        finally
        {
            for (int index = 0; index < states.Length; index++)
            {
                states[index].RestoreNameAndDirtyState();
            }
        }

        foreach (SourceState state in states)
        {
            state.RequireUnchanged();
        }

        VerifyFailureBoundaries(root, originals);
        VerifyPublishedIdentityProtection(originals);
        VerifyInvalidCompositionRoleRejected(originals[0]);
        Debug.Log(
            "R21 character AI action catalog scenarios passed: "
            + "type, branch, and composition identity, rename stability, "
            + "configured-only root variants, owner and staff configuration, "
            + "published identity protection, missing and duplicate defaults, "
            + "wrong branch, and invalid enum rejection.");
    }

    private static void VerifyConfiguredVariants(
        ICharacterAiActionAssetCatalog catalog,
        IReadOnlyCollection<AIActionSet> rootActions)
    {
        AIWork staffVariant = ScriptableObject.CreateInstance<AIWork>();
        AIEat visitorVariant = ScriptableObject.CreateInstance<AIEat>();
        try
        {
            AIAction[] staff = AIBrainActionConfiguration.ConfigureStaff(
                new[] { new AIAction { actionset = staffVariant } },
                catalog,
                null);
            AIAction[] visitor = AIBrainActionConfiguration.EnsureVisitorActions(
                new[] { new AIAction { actionset = visitorVariant } },
                null,
                catalog,
                null);

            Require(staff.Count(action => action?.actionset is AIWork
                    && action.actionset.Branch == CharacterAiBranch.Work) == 1
                    && staff.Any(action => ReferenceEquals(
                        action.actionset,
                        staffVariant))
                    && !staff.Any(action => rootActions.Contains(action.actionset)
                        && action.actionset is AIWork),
                "Staff configuration replaced or duplicated its explicit work variant.");
            Require(visitor.Count(action => action?.actionset is AIEat
                    && action.actionset.Branch == CharacterAiBranch.Eat) == 1
                    && visitor.Any(action => ReferenceEquals(
                        action.actionset,
                        visitorVariant))
                    && !visitor.Any(action => rootActions.Contains(action.actionset)
                        && action.actionset is AIEat),
                "Visitor configuration replaced or duplicated its explicit eat variant.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(staffVariant);
            UnityEngine.Object.DestroyImmediate(visitorVariant);
        }
    }

    private static ActionBinding[] CreateBindings() => new[]
    {
        Bind<AIWait>(CharacterAiBranch.Wait),
        Bind<AIWork>(CharacterAiBranch.Work),
        Bind<AIEat>(CharacterAiBranch.Eat),
        Bind<AIRest>(CharacterAiBranch.Rest),
        Bind<AIFacilityRoleAction>(CharacterAiBranch.Toilet),
        Bind<AIFacilityRoleAction>(CharacterAiBranch.Hygiene),
        Bind<AIFacilityRoleAction>(CharacterAiBranch.LeisureVisit),
        Bind<AIShopping>(CharacterAiBranch.Shopping),
        Bind<AILookAround>(CharacterAiBranch.LookAround),
        Bind<AIExitDungeon>(CharacterAiBranch.ExitDungeon),
        Bind<AIHaul>(CharacterAiBranch.Work),
        Bind<AIRescue>(CharacterAiBranch.Work),
        Bind<AIHunt>(CharacterAiBranch.Work),
        Bind<AISubstanceUse>(CharacterAiBranch.Work),
        Bind<AIDrink>(CharacterAiBranch.Drink),
        Bind<AIPrimitiveFieldMeal>(CharacterAiBranch.Eat),
        Bind<AIPrimitiveFloorRest>(CharacterAiBranch.Rest),
        Bind<AIPrimitiveLatrine>(CharacterAiBranch.Toilet),
        Bind<AIPrimitiveBucketWash>(CharacterAiBranch.Hygiene)
    };

    private static ActionBinding Bind<T>(CharacterAiBranch branch)
        where T : AIActionSet => new(
            typeof(T),
            branch,
            catalog => catalog.GetRequiredAction<T>(branch));

    private static void VerifyFailureBoundaries(
        IGameContentCatalog root,
        IReadOnlyList<AIActionSet> registered)
    {
        AIWait requiredWait = registered.OfType<AIWait>().Single();
        AIWait unregistered = ScriptableObject.CreateInstance<AIWait>();
        AIWait configuredPeer = ScriptableObject.CreateInstance<AIWait>();
        AIWait duplicateDefault = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            unregistered.name = "Outside Root Catalog";
            configuredPeer.name = "Configured Root Peer";
            duplicateDefault.name = requiredWait.name;
            SetCompositionRole(
                duplicateDefault,
                CharacterAiActionCompositionRole.RequiredDefault);

            ICharacterAiActionAssetCatalog missing =
                new ResourceCharacterAiActionAssetCatalog(
                    new ActionContentCatalog(
                        root,
                        registered.Where(action => !ReferenceEquals(
                            action,
                            requiredWait))));
            ExpectFailure<InvalidOperationException>(
                () => missing.GetRequiredAction<AIWait>(CharacterAiBranch.Wait),
                "not registered");

            ICharacterAiActionAssetCatalog configuredPeerCatalog =
                new ResourceCharacterAiActionAssetCatalog(
                    new ActionContentCatalog(
                        root,
                        registered.Concat(new AIActionSet[] { configuredPeer })));
            Require(ReferenceEquals(
                    configuredPeerCatalog.GetRequiredAction<AIWait>(
                        CharacterAiBranch.Wait),
                    requiredWait),
                "A configured-only root peer displaced the required default.");
            // Match FromRoot's provider set: the generic domain view excludes
            // item definitions, domain-owned keys and the skill settings owner.
            ScriptableObject[] referenceDefinitions = root.Root.DomainCatalogs
                .OfType<GameDomainContentCatalogSO>()
                .SelectMany(domain => domain.Definitions.Concat(new ScriptableObject[] { domain }))
                .Concat(root.Items.Definitions.Cast<ScriptableObject>())
                .Concat(new ScriptableObject[] { root.CharacterSkillSettings }).Distinct().ToArray();
            ScriptableObject[] peerDefinitions = referenceDefinitions
                .Concat(new ScriptableObject[] { configuredPeer }).ToArray();
            IReadOnlyList<string> peerErrors = new ContentReferenceCatalog(peerDefinitions).Validate(peerDefinitions);
            Require(peerErrors.Count == 0,
                "Root definition validation rejected a configured-only action peer: "
                + string.Join(" | ", peerErrors.Take(5)));
            ScriptableObject[] duplicateDefinitions = referenceDefinitions
                .Concat(new ScriptableObject[] { duplicateDefault }).ToArray();
            Require(new ContentReferenceCatalog(duplicateDefinitions).Validate(duplicateDefinitions)
                    .Any(error => error.Contains("Required AI default", StringComparison.Ordinal)
                        && error.Contains("more than once", StringComparison.Ordinal)),
                "Root definition validation accepted duplicate required defaults.");

            ICharacterAiActionAssetCatalog demoted =
                new ResourceCharacterAiActionAssetCatalog(
                    new ActionContentCatalog(
                        root,
                        registered.Where(action => !ReferenceEquals(
                                action,
                                requiredWait))
                            .Concat(new AIActionSet[] { configuredPeer })));
            ExpectFailure<InvalidOperationException>(
                () => demoted.GetRequiredAction<AIWait>(CharacterAiBranch.Wait),
                "RequiredDefault");

            ICharacterAiActionAssetCatalog duplicate =
                new ResourceCharacterAiActionAssetCatalog(
                    new ActionContentCatalog(
                        root,
                        registered.Concat(new AIActionSet[] { duplicateDefault })));
            ExpectFailure<InvalidOperationException>(
                () => duplicate.GetRequiredAction<AIWait>(CharacterAiBranch.Wait),
                "exactly one RequiredDefault");

            ICharacterAiActionAssetCatalog production =
                new ResourceCharacterAiActionAssetCatalog(root);
            Require(!ReferenceEquals(
                    production.GetRequiredAction<AIWait>(CharacterAiBranch.Wait),
                    unregistered),
                "An unregistered AI action instance was selected.");
            ExpectFailure<InvalidOperationException>(
                () => production.GetRequiredAction<AIWait>(CharacterAiBranch.Work),
                "wrong branch");
            ExpectFailure<ArgumentOutOfRangeException>(
                () => production.GetRequiredAction<AIWait>(CharacterAiBranch.None),
                "non-None branch");
            ExpectFailure<ArgumentException>(
                () => production.GetRequiredAction<AIActionSet>(
                    CharacterAiBranch.Wait),
                "must be concrete");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unregistered);
            UnityEngine.Object.DestroyImmediate(configuredPeer);
            UnityEngine.Object.DestroyImmediate(duplicateDefault);
        }
    }

    private static void VerifyPublishedIdentityProtection(
        IReadOnlyList<AIActionSet> registered)
    {
        VerifyPublishedIdentityMutationRejected(
            registered.OfType<AIWait>().Single(),
            "compositionRole",
            (int)CharacterAiActionCompositionRole.ConfiguredOnly);
        VerifyPublishedIdentityMutationRejected(
            registered.OfType<AIFacilityRoleAction>().Single(action =>
                action.Role == FacilityRole.Toilet),
            "role",
            (int)FacilityRole.Hygiene);
    }

    private static void VerifyPublishedIdentityMutationRejected(
        AIActionSet source,
        string fieldPath,
        int changedValue)
    {
        SourceState state = SourceState.Capture(source);
        string assetPath = AssetDatabase.GetAssetPath(source);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        Require(!string.IsNullOrWhiteSpace(assetPath)
                && !string.IsNullOrWhiteSpace(guid),
            $"Cannot locate published AI action '{source.name}'.");

        Require(ContentIdentityProtection.TryLoadBaseline(
                out ContentIdentityProtection.Snapshot publication,
                out string publicationError), publicationError);
        ContentIdentityProtection.AssetRecord published = publication.assets
            .Single(value => string.Equals(value.guid, guid, StringComparison.Ordinal));
        Require(published.fields.Any(field => string.Equals(
                field.path,
                fieldPath,
                StringComparison.Ordinal)),
            $"Published AI action '{source.name}' did not capture '{fieldPath}'.");

        AIActionSet clone = UnityEngine.Object.Instantiate(source);
        try
        {
            using (var serialized = new SerializedObject(clone))
            {
                serialized.Update();
                SerializedProperty property = serialized.FindProperty(fieldPath);
                Require(property != null,
                    $"AI action clone has no serialized '{fieldPath}' field.");
                property.intValue = changedValue;
                Require(serialized.ApplyModifiedPropertiesWithoutUndo(),
                    $"AI action clone '{fieldPath}' mutation was not applied.");
            }

            ContentIdentityProtection.AssetRecord changed =
                ContentIdentityProtection.CaptureAsset(guid, clone);
            var baseline = new ContentIdentityProtection.Snapshot();
            baseline.assets.Add(published);
            var current = new ContentIdentityProtection.Snapshot();
            current.assets.Add(changed);
            IReadOnlyList<string> errors =
                ContentIdentityProtection.Validate(baseline, current);
            Require(errors.Any(error => error.IndexOf(
                    fieldPath,
                    StringComparison.Ordinal) >= 0),
                $"Published AI action identity '{fieldPath}' mutation was accepted.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }

        state.RequireUnchanged();
    }

    private static void VerifyInvalidCompositionRoleRejected(AIActionSet source)
    {
        SourceState state = SourceState.Capture(source);
        AIActionSet clone = UnityEngine.Object.Instantiate(source);
        try
        {
            SetSerializedInt(clone, "compositionRole", int.MaxValue);
            var catalog = new ContentReferenceCatalog(
                new ScriptableObject[] { clone });
            Require(catalog.Validate(new ScriptableObject[] { clone })
                    .Any(error => error.IndexOf(
                        "composition role",
                        StringComparison.OrdinalIgnoreCase) >= 0),
                "Content reference validation accepted an unknown AI action composition role.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }

        state.RequireUnchanged();
    }

    private static void SetCompositionRole(
        AIActionSet action,
        CharacterAiActionCompositionRole role) =>
        SetSerializedInt(action, "compositionRole", (int)role);

    private static void SetSerializedInt(
        ScriptableObject target,
        string fieldPath,
        int value)
    {
        using var serialized = new SerializedObject(target);
        serialized.Update();
        SerializedProperty property = serialized.FindProperty(fieldPath);
        Require(property != null,
            $"{target.GetType().Name} has no serialized '{fieldPath}' field.");
        property.intValue = value;
        Require(serialized.ApplyModifiedPropertiesWithoutUndo(),
            $"{target.GetType().Name}.{fieldPath} mutation was not applied.");
    }

    private static void ExpectFailure<TException>(
        Action action,
        string expectedMessage)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            Require(exception.Message.IndexOf(
                    expectedMessage,
                    StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected {typeof(TException).Name} containing "
                + $"'{expectedMessage}', got '{exception.Message}'.");
            return;
        }

        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} containing '{expectedMessage}'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ActionBinding
    {
        public ActionBinding(
            Type type,
            CharacterAiBranch branch,
            Func<ICharacterAiActionAssetCatalog, AIActionSet> resolve)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Branch = branch;
            Resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        }

        public Type Type { get; }
        public CharacterAiBranch Branch { get; }
        public Func<ICharacterAiActionAssetCatalog, AIActionSet> Resolve { get; }
    }

    private sealed class SourceState
    {
        private readonly AIActionSet source;
        private readonly string name;
        private readonly string json;
        private readonly bool dirty;

        private SourceState(AIActionSet source)
        {
            this.source = source
                ?? throw new ArgumentNullException(nameof(source));
            name = source.name;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        public static SourceState Capture(AIActionSet source) => new(source);

        public void RestoreNameAndDirtyState()
        {
            source.name = name;
            if (dirty)
            {
                EditorUtility.SetDirty(source);
            }
            else
            {
                EditorUtility.ClearDirty(source);
            }
        }

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"Fixture changed source AI action '{name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"Fixture changed dirty state for AI action '{name}'.");
        }
    }

    private sealed class ActionContentCatalog : IGameContentCatalog
    {
        private readonly IGameContentCatalog source;
        private readonly AIActionSet[] actions;

        public ActionContentCatalog(
            IGameContentCatalog source,
            IEnumerable<AIActionSet> actions)
        {
            this.source = source
                ?? throw new ArgumentNullException(nameof(source));
            this.actions = (actions ?? throw new ArgumentNullException(
                    nameof(actions)))
                .Where(action => action != null)
                .ToArray();
        }

        public GameContentCatalogSO Root => source.Root;
        public ItemDefinitionCatalogSO Items => source.Items;
        public WorldInteractionPresentationCatalogSO WorldPresentation =>
            source.WorldPresentation;
        public CharacterSkillSystemSettingsSO CharacterSkillSettings =>
            source.CharacterSkillSettings;
        public GameMediaCatalogSO Media => source.Media;
        public GameDomainContentCatalogSO Domain => source.Domain;
        public IReadOnlyList<ServiceProcessSO> ServiceProcesses =>
            source.ServiceProcesses;
        public RoomEnvironmentSettingsSO RoomEnvironmentSettings =>
            source.RoomEnvironmentSettings;
        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
            source.SiteArchetypes;
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
            source.UrgentSites;
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
            source.DecisionCards;
        public IReadOnlyList<OffenseEncounterSO> Encounters =>
            source.Encounters;

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            typeof(AIActionSet).IsAssignableFrom(typeof(T))
                ? actions.OfType<T>().ToArray()
                : source.GetAll<T>();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }
}
