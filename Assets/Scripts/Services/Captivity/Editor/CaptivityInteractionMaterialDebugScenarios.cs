using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CaptivityInteractionMaterialDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/Captivity Interaction Materials")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CAPTIVITY_INTERACTION_MATERIAL_PASS");
    }

    public static void RunAll()
    {
        VerifyDeterministicMaximumCategoryEnvelope();
        VerifyCommittedTokenRoundTrip();
        VerifyRestoreParticipantPublishesBeforeSharedAuthorities();
        VerifyRuntimeUsesExactOwnerBoundary();
    }

    private static void VerifyRestoreParticipantPublishesBeforeSharedAuthorities()
    {
        CaptiveState captive = new()
        {
            captiveId = "character:qa-restore-captive"
        };
        FixedRestoreStateQuery states = new(captive);
        RecordingRestoreAuthority authority = new();
        CaptivityInteractionMaterialRestoreParticipant participant = new(
            states,
            authority);

        participant.BeginRestoreCandidate();
        participant.PublishRestoreCandidate();
        participant.CompleteRestoreCandidate();
        Require(authority.CallCount == 1
            && ReferenceEquals(authority.LastStates, states.Captives)
            && string.CompareOrdinal(
                participant.ParticipantId,
                "220.world.facility-buffer-destinations") < 0,
            "Interaction material restore did not publish into the shared candidate before claim/profile authority publication.");

        authority.AllowReplace = false;
        participant.BeginRestoreCandidate();
        bool rejected = false;
        try
        {
            participant.PublishRestoreCandidate();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        participant.DiscardRestoreCandidate();
        Require(rejected && authority.CallCount == 2,
            "Interaction material restore failure did not fail-loud before shared authority publication.");
    }

    private static void VerifyDeterministicMaximumCategoryEnvelope()
    {
        CaptiveState state = new()
        {
            captiveId = "character:qa-captive",
            currentInteractionId = "captivity:qa"
        };
        FixedHandler handler = new();
        FixedCatalog catalog = new(
            new CaptivityInteractionMaterialMassDefinition(
                (ItemDefinitionId)"food:light",
                StockCategory.Food,
                new PhysicalMassGrams(250L)),
            new CaptivityInteractionMaterialMassDefinition(
                (ItemDefinitionId)"food:heavy",
                StockCategory.Food,
                new PhysicalMassGrams(600L)),
            new CaptivityInteractionMaterialMassDefinition(
                (ItemDefinitionId)"material:general",
                StockCategory.General,
                new PhysicalMassGrams(900L)));

        Require(CaptivityInteractionMaterialAuthority.TryProject(
                state,
                handler,
                (BuildingInstanceId)"building:qa-cell",
                new Vector2Int(7, 9),
                catalog,
                out CaptivityInteractionMaterialProjection first,
                out string failure), failure);
        Require(first.CapacityGrams == 2_100L,
            "Category capacity did not use 2*max(Food)+1*max(General)." );

        FixedCatalog shuffled = new(catalog.CaptureAll().Reverse().ToArray());
        Require(CaptivityInteractionMaterialAuthority.TryProject(
                state,
                handler,
                (BuildingInstanceId)"building:qa-cell",
                new Vector2Int(7, 9),
                shuffled,
                out CaptivityInteractionMaterialProjection second,
                out failure), failure);
        Require(string.Equals(first.DestinationId, second.DestinationId,
                StringComparison.Ordinal)
            && string.Equals(first.Fingerprint, second.Fingerprint,
                StringComparison.Ordinal),
            "Category envelope depended on catalog enumeration order.");
        Require(first.DestinationId.StartsWith(
                ReservedTargetDestinationIdentity.ExactFacilityInputPrefix,
                StringComparison.Ordinal),
            "Interaction material destination is not an exact facility input.");
    }

    private static void VerifyCommittedTokenRoundTrip()
    {
        const string operation = "captivity-interaction-sink:qa";
        const string commit =
            "physical-batch-disposition:3:captivity-interaction-sink:qa:3:2100";
        string destination =
            "facility-input:exact:captivity.interaction:v1:qa";
        string token = "captivity-interaction-material-commit:v1:"
            + Encode(destination) + ":" + Encode(operation) + ":"
            + Encode(commit) + ":3:2100";
        Require(CaptivityInteractionMaterialAuthority.TryParseCommittedToken(
                token,
                out string restoredDestination,
                out string operationId,
                out string commitId,
                out int quantity,
                out long grams)
            && restoredDestination == destination
            && operationId == operation
            && commitId == commit
            && quantity == 3
            && grams == 2_100L,
            "Committed Sink token did not round-trip exactly.");
    }

    private static void VerifyRuntimeUsesExactOwnerBoundary()
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root unavailable.");
        string interaction = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs"));
        string owner = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Captivity/"
            + "CaptivityInteractionMaterialRuntime.cs"));
        string captivity = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Captivity/CaptivityRuntime.cs"));
        string lifecycle = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Captivity/"
            + "CaptivityInteractionMaterialLifecycleRuntime.cs"));
        string registration = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Infrastructure/Registration/"
            + "DungeonWorldSimulationRegistration.cs"));
        Require(!interaction.Contains("TryConsumeFacilityBuffer(",
                StringComparison.Ordinal)
            && !interaction.Contains("TryRequestFacilityDelivery(",
                StringComparison.Ordinal)
            && interaction.Contains("materials.TryCommitSink(",
                StringComparison.Ordinal)
            && interaction.Contains("materials.TryOpenAndRequest(",
                StringComparison.Ordinal),
            "Interaction orchestration retained raw category consumption/delivery.");
        Require(owner.Contains("PhysicalItemDispositionKind.Sink",
                StringComparison.Ordinal)
            && owner.Contains("FacilityBufferDestinationAnchorKind.LiveFacility",
                StringComparison.Ordinal)
            && owner.Contains("ExactGramRequired", StringComparison.Ordinal)
            && owner.Contains("TryReleaseAtOwnerPosition(",
                StringComparison.Ordinal),
            "Exact owner lost typed Sink, LiveFacility, gram, or carried-aware close.");
        Require(captivity.Contains(
                "interactionMaterialLifecycle.ValidateBeforeCapture(Captives)",
                StringComparison.Ordinal)
            && !captivity.Contains(
                "interactionMaterialLifecycle.TryReplaceRestoreAuthorities(",
                StringComparison.Ordinal)
            && lifecycle.Contains(
                "materials.TryReplace(",
                StringComparison.Ordinal)
            && lifecycle.Contains(
                "217.world.captivity-interaction-material",
                StringComparison.Ordinal)
            && registration.Contains(
                "Register<CaptivityInteractionMaterialRuntime>(",
                StringComparison.Ordinal)
            && registration.Contains(
                "Register<CaptivityInteractionMaterialLifecycleRuntime>(",
                StringComparison.Ordinal)
            && registration.Contains(
                "Register<CaptivityInteractionMaterialRestoreParticipant>(",
                StringComparison.Ordinal),
            "Captivity current-format save/restore did not join exact material authority.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string Encode(string value) => Convert.ToBase64String(
            Encoding.UTF8.GetBytes(value ?? string.Empty))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FixedRestoreStateQuery :
        ICaptivityRestoreStateQuery
    {
        internal FixedRestoreStateQuery(params CaptiveState[] states) =>
            Captives = states ?? Array.Empty<CaptiveState>();

        public IReadOnlyList<CaptiveState> Captives { get; }
    }

    private sealed class RecordingRestoreAuthority :
        ICaptivityInteractionMaterialRestoreAuthority
    {
        internal bool AllowReplace = true;
        internal int CallCount;
        internal IReadOnlyList<CaptiveState> LastStates;

        public bool TryReplaceRestoreAuthorities(
            IReadOnlyList<CaptiveState> candidateStates,
            out string failureReason)
        {
            CallCount++;
            LastStates = candidateStates;
            failureReason = AllowReplace
                ? string.Empty
                : "qa-intentional-rejection";
            return AllowReplace;
        }
    }

    private sealed class FixedCatalog :
        ICaptivityInteractionMaterialMassCatalog
    {
        private readonly IReadOnlyList<
            CaptivityInteractionMaterialMassDefinition> definitions;

        internal FixedCatalog(
            params CaptivityInteractionMaterialMassDefinition[] definitions)
        {
            this.definitions = definitions ?? Array.Empty<
                CaptivityInteractionMaterialMassDefinition>();
        }

        public long AuthorityRevision => 77L;

        public IReadOnlyList<CaptivityInteractionMaterialMassDefinition>
            CaptureAll() => definitions;
    }

    private sealed class FixedHandler : ICaptivityInteractionHandler
    {
        private static readonly IReadOnlyDictionary<StockCategory, int>
            Requirements = new Dictionary<StockCategory, int>
            {
                [StockCategory.Food] = 2,
                [StockCategory.General] = 1
            };

        public string InteractionId => "captivity:qa";
        public string DisplayName => "QA";
        public CaptiveInteractionKind Kind => CaptiveInteractionKind.Persuasion;
        public float RequiredWork => 1f;
        public IReadOnlyDictionary<StockCategory, int> MaterialRequirements =>
            Requirements;

        public bool CanExecute(
            CaptivityInteractionContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public CaptivityInteractionResult Execute(
            CaptivityInteractionContext context) => new(true, "qa");
    }
}
