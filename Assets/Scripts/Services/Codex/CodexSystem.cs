using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public enum CodexEntryCategory
{
    Monster,
    Invasion,
    Facility
}

public enum CodexInfoSource
{
    System,
    Observation,
    Research,
    Synthesis,
    Evolution
}

public readonly struct CodexInfoLine
{
    public CodexInfoLine(string text, CodexInfoSource source)
    {
        Text = text ?? string.Empty;
        Source = source;
    }

    public string Text { get; }
    public CodexInfoSource Source { get; }
}

public sealed class CodexEntrySnapshot
{
    public CodexEntryCategory category;
    public string entryId;
    public string title;
    public bool discovered;
    public CodexInfoLine[] lines = Array.Empty<CodexInfoLine>();

    public string ToDisplayText()
    {
        List<string> result = new List<string>
        {
            string.IsNullOrWhiteSpace(title) ? entryId : title
        };

        if (lines == null || lines.Length == 0)
        {
            result.Add("- 정보 없음");
        }
        else
        {
            result.AddRange(lines.Select((line) => $"- {line.Text}"));
        }

        return string.Join("\n", result);
    }
}

public sealed class CodexEntryRecord
{
    private readonly List<CodexInfoLine> lines = new List<CodexInfoLine>();
    private readonly HashSet<string> lineSet = new HashSet<string>();
    private readonly IReadOnlyList<CodexInfoLine> linesView;

    public CodexEntryRecord(CodexEntryCategory category, string entryId, string title)
    {
        Category = category;
        EntryId = entryId ?? string.Empty;
        Title = string.IsNullOrWhiteSpace(title) ? EntryId : title;
        Discovered = true;
        linesView = ReadOnlyView.List(lines);
    }

    public CodexEntryCategory Category { get; }
    public string EntryId { get; }
    public string Title { get; private set; }
    public bool Discovered { get; private set; }
    public IReadOnlyList<CodexInfoLine> Lines => linesView;

    public void Rename(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }
    }

    public bool AddInfo(string text, CodexInfoSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim();
        if (!lineSet.Add(normalized))
        {
            return false;
        }

        lines.Add(new CodexInfoLine(normalized, source));
        Discovered = true;
        return true;
    }

    public CodexEntrySnapshot ToSnapshot()
    {
        return new CodexEntrySnapshot
        {
            category = Category,
            entryId = EntryId,
            title = Title,
            discovered = Discovered,
            lines = lines.ToArray()
        };
    }
}

public sealed class CodexState
{
    private readonly Dictionary<string, CodexEntryRecord> entries = new Dictionary<string, CodexEntryRecord>();
    private readonly IReadOnlyCollection<CodexEntryRecord> entriesView;

    public CodexState()
    {
        entriesView = ReadOnlyView.Collection(entries.Values);
    }

    public IReadOnlyCollection<CodexEntryRecord> Entries => entriesView;

    public CodexEntryRecord GetOrCreate(CodexEntryCategory category, string entryId, string title)
    {
        string key = GetKey(category, entryId);
        if (!entries.TryGetValue(key, out CodexEntryRecord entry))
        {
            entry = new CodexEntryRecord(category, entryId, title);
            entries[key] = entry;
        }
        else
        {
            entry.Rename(title);
        }

        return entry;
    }

    public bool AddInfo(CodexEntryCategory category, string entryId, string title, string info, CodexInfoSource source)
    {
        return GetOrCreate(category, entryId, title).AddInfo(info, source);
    }

    public bool HasInfo(CodexEntryCategory category, string entryId, string info)
    {
        string key = GetKey(category, entryId);
        return entries.TryGetValue(key, out CodexEntryRecord entry)
            && entry.Lines.Any((line) => line.Text == info);
    }

    public IReadOnlyList<CodexEntrySnapshot> GetSnapshots(CodexEntryCategory category)
    {
        return entries.Values
            .Where((entry) => entry.Category == category)
            .OrderBy((entry) => entry.Title)
            .Select((entry) => entry.ToSnapshot())
            .ToList();
    }

    public CodexEntrySnapshot GetSnapshot(CodexEntryCategory category, string entryId)
    {
        return entries.TryGetValue(GetKey(category, entryId), out CodexEntryRecord entry)
            ? entry.ToSnapshot()
            : null;
    }

    public void ClearForRestore()
    {
        entries.Clear();
    }

    private static string GetKey(CodexEntryCategory category, string entryId)
    {
        return $"{category}:{entryId ?? string.Empty}";
    }
}

public struct CodexUpdatedEvent
{
    public CodexEntryCategory category;
    public string entryId;

    public CodexUpdatedEvent(CodexEntryCategory category, string entryId)
    {
        this.category = category;
        this.entryId = entryId ?? string.Empty;
    }
}

public static class CodexService
{
    public const string BreakthroughIntruderId = CodexInvasionRecorder.BreakthroughIntruderId;

    public static void ImportReferenceData(
        CodexState state,
        BlueprintResearchState researchState,
        ICodexReferenceImporter referenceImporter)
    {
        if (referenceImporter == null)
        {
            throw new ArgumentNullException(nameof(referenceImporter));
        }

        referenceImporter.Import(state, researchState);
    }

    public static void ObserveCharacter(CodexState state, CharacterActor actor)
    {
        CodexObservationRecorder.ObserveCharacter(state, actor);
    }

    public static void ObserveSpecies(CodexState state, CharacterSpeciesSO species, CodexInfoSource source)
    {
        CodexObservationRecorder.ObserveSpecies(state, species, source);
    }

    public static void ObserveFacility(CodexState state, BuildableObject facility, CodexInfoSource source)
    {
        CodexObservationRecorder.ObserveFacility(state, facility, source);
    }

    public static void ObserveFacility(CodexState state, BuildingSO building, CodexInfoSource source)
    {
        CodexObservationRecorder.ObserveFacility(state, building, source);
    }

    public static void RecordDefenseObservation(CodexState state, DefenseActivationSnapshot report)
    {
        CodexInvasionRecorder.RecordDefenseObservation(state, report);
    }

    public static void RecordCombatReport(CodexState state, InvasionCombatReportSnapshot report)
    {
        CodexInvasionRecorder.RecordCombatReport(state, report);
    }

    public static void RecordFacilityDamage(CodexState state, BuildableObject facility)
    {
        CodexInvasionRecorder.RecordFacilityDamage(state, facility);
    }

    public static void RecordResearch(
        CodexState state,
        BlueprintResearchUnlockResult unlockResult,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery,
        IFacilityShopCatalog facilityShopCatalog)
    {
        CodexRecipeRecorder.RecordResearch(
            state,
            unlockResult,
            researchState,
            synthesisRecipeQuery,
            facilityShopCatalog);
    }

    public static void RecordSynthesis(
        CodexState state,
        FacilitySynthesisResult result,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery)
    {
        CodexRecipeRecorder.RecordSynthesis(state, result, researchState, synthesisRecipeQuery);
    }

    public static void RecordEvolution(CodexState state, FacilityEvolutionResult result)
    {
        CodexEvolutionRecorder.Record(state, result);
    }

    public static void ImportSynthesisRecipes(
        CodexState state,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery)
    {
        CodexRecipeRecorder.ImportSynthesisRecipes(state, researchState, synthesisRecipeQuery);
    }

    public static void SeedBreakthroughIntruder(CodexState state)
    {
        CodexInvasionRecorder.SeedBreakthroughIntruder(state);
    }

}

public class CodexRuntime : MonoBehaviour
{
    [SerializeField] private bool importReferenceDataOnAwake = true;

    private readonly CodexState state = new CodexState();
    private IBlueprintResearchStateService blueprintResearchStateService;
    private ICodexReferenceImporter referenceImporter;
    private IFacilitySynthesisRecipeQuery synthesisRecipeQuery;
    private IFacilityShopCatalog facilityShopCatalog;
    private IGameEventBus gameEventBus;
    private IDisposable evolutionCompletedSubscription;
    private IDisposable synthesisCompletedSubscription;
    private IDisposable researchCompletedSubscription;
    private IDisposable combatReportReadySubscription;
    private IDisposable defenseFacilityTriggeredSubscription;
    private IDisposable invasionSpawnedSubscription;
    private IDisposable invasionFacilityDamagedSubscription;
    private IDisposable facilityVisitSubscription;

    public CodexState State => state;

    [Inject]
    public void ConstructCodexRuntime(
        IBlueprintResearchStateService blueprintResearchStateService,
        ICodexReferenceImporter referenceImporter,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery,
        IFacilityShopCatalog facilityShopCatalog,
        IGameEventBus gameEventBus)
    {
        this.blueprintResearchStateService = blueprintResearchStateService
            ?? throw new ArgumentNullException(nameof(blueprintResearchStateService));
        this.referenceImporter = referenceImporter
            ?? throw new ArgumentNullException(nameof(referenceImporter));
        this.synthesisRecipeQuery = synthesisRecipeQuery
            ?? throw new ArgumentNullException(nameof(synthesisRecipeQuery));
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    public BlueprintResearchState ResearchState
    {
        get { return ResolveResearchStateService().GetState(); }
    }

    private void Start()
    {
        if (importReferenceDataOnAwake)
        {
            ImportReferenceData();
        }
    }

    public void ImportReferenceData()
    {
        CodexService.ImportReferenceData(state, ResearchState, ResolveReferenceImporter());
        PublishUpdated(CodexEntryCategory.Facility, "reference");
        PublishUpdated(CodexEntryCategory.Monster, "reference");
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    private IBlueprintResearchStateService ResolveResearchStateService()
    {
        return blueprintResearchStateService
            ?? throw new InvalidOperationException($"{nameof(CodexRuntime)} requires {nameof(IBlueprintResearchStateService)} injection.");
    }

    private ICodexReferenceImporter ResolveReferenceImporter()
    {
        return referenceImporter
            ?? throw new InvalidOperationException($"{nameof(CodexRuntime)} requires {nameof(ICodexReferenceImporter)} injection.");
    }

    private IFacilitySynthesisRecipeQuery ResolveSynthesisRecipeQuery()
    {
        return synthesisRecipeQuery
            ?? throw new InvalidOperationException($"{nameof(CodexRuntime)} requires {nameof(IFacilitySynthesisRecipeQuery)} injection.");
    }

    private IFacilityShopCatalog ResolveFacilityShopCatalog()
    {
        return facilityShopCatalog
            ?? throw new InvalidOperationException($"{nameof(CodexRuntime)} requires {nameof(IFacilityShopCatalog)} injection.");
    }

    public IReadOnlyList<CodexEntrySnapshot> GetEntries(CodexEntryCategory category)
    {
        return state.GetSnapshots(category);
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        CodexService.ObserveCharacter(state, eventType.visitorActor);
        CodexService.ObserveFacility(state, eventType.facility, CodexInfoSource.Observation);
        PublishUpdated(CodexEntryCategory.Monster, eventType.visitorActor != null ? eventType.visitorActor.Identity.SpeciesTag : string.Empty);
        PublishUpdated(CodexEntryCategory.Facility, eventType.facility != null ? eventType.facility.id.ToString() : string.Empty);
    }

    public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)
    {
        CodexService.RecordDefenseObservation(state, eventType.report);
        PublishUpdated(CodexEntryCategory.Facility, "defense");
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void OnTriggerEvent(InvasionCombatReportReadyEvent eventType)
    {
        CodexService.RecordCombatReport(state, eventType.report);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
    {
        CodexService.RecordFacilityDamage(state, eventType.facility);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void OnTriggerEvent(InvasionSpawnedEvent eventType)
    {
        CodexService.SeedBreakthroughIntruder(state);
        CodexService.ObserveCharacter(state, eventType.intruderActor);
        PublishUpdated(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        CodexService.RecordResearch(
            state,
            eventType.unlockResult,
            ResearchState,
            ResolveSynthesisRecipeQuery(),
            ResolveFacilityShopCatalog());
        PublishUpdated(CodexEntryCategory.Facility, "research");
    }

    public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)
    {
        CodexService.RecordSynthesis(state, eventType.result, ResearchState, ResolveSynthesisRecipeQuery());
        PublishUpdated(CodexEntryCategory.Facility, "synthesis");
    }

    public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)
    {
        CodexService.RecordEvolution(state, eventType.result);
        PublishUpdated(CodexEntryCategory.Facility, "evolution");
    }

    private void PublishUpdated(CodexEntryCategory category, string entryId)
    {
        (gameEventBus
            ?? throw new InvalidOperationException($"{nameof(CodexRuntime)} requires {nameof(IGameEventBus)} injection."))
            .Publish(new CodexUpdatedEvent(category, entryId));
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        evolutionCompletedSubscription?.Dispose();
        evolutionCompletedSubscription = null;
        synthesisCompletedSubscription?.Dispose();
        synthesisCompletedSubscription = null;
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
        combatReportReadySubscription?.Dispose();
        combatReportReadySubscription = null;
        defenseFacilityTriggeredSubscription?.Dispose();
        defenseFacilityTriggeredSubscription = null;
        invasionSpawnedSubscription?.Dispose();
        invasionSpawnedSubscription = null;
        invasionFacilityDamagedSubscription?.Dispose();
        invasionFacilityDamagedSubscription = null;
        facilityVisitSubscription?.Dispose();
        facilityVisitSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        evolutionCompletedSubscription ??=
            gameEventBus.Subscribe<FacilityEvolutionCompletedEvent>(OnTriggerEvent);
        synthesisCompletedSubscription ??=
            gameEventBus.Subscribe<FacilitySynthesisCompletedEvent>(OnTriggerEvent);
        researchCompletedSubscription ??=
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
        combatReportReadySubscription ??=
            gameEventBus.Subscribe<InvasionCombatReportReadyEvent>(OnTriggerEvent);
        defenseFacilityTriggeredSubscription ??=
            gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnTriggerEvent);
        invasionSpawnedSubscription ??=
            gameEventBus.Subscribe<InvasionSpawnedEvent>(OnTriggerEvent);
        invasionFacilityDamagedSubscription ??=
            gameEventBus.Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        facilityVisitSubscription ??=
            gameEventBus.Subscribe<FacilityVisitEvent>(OnTriggerEvent);
    }
}
