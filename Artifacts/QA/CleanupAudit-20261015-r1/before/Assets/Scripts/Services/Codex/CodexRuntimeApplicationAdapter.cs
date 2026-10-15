using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public sealed class CodexRuntimeApplicationAdapter :
    ICodexRuntimeApplicationPort,
    ICodexReferenceSnapshotQueryPort
{
    private readonly IBlueprintResearchStateService researchStateService;
    private readonly ICodexReferenceCatalog referenceCatalog;
    private readonly IFacilitySynthesisRecipeQuery synthesisRecipeQuery;
    private readonly IGameEventBus gameEventBus;
    private readonly EventSubscriptionLifetime<CodexRuntime> lifetime;

    public CodexRuntimeApplicationAdapter(
        IBlueprintResearchStateService researchStateService,
        ICodexReferenceCatalog referenceCatalog,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery,
        IGameEventBus gameEventBus)
    {
        this.researchStateService = researchStateService
            ?? throw new ArgumentNullException(nameof(researchStateService));
        this.referenceCatalog = referenceCatalog
            ?? throw new ArgumentNullException(nameof(referenceCatalog));
        this.synthesisRecipeQuery = synthesisRecipeQuery
            ?? throw new ArgumentNullException(nameof(synthesisRecipeQuery));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        lifetime = new EventSubscriptionLifetime<CodexRuntime>(
            "A codex application adapter cannot bind more than one runtime.",
            "Codex application adapter received an event while unbound.");
    }

    public void Bind(CodexRuntime target)
    {
        if (!lifetime.BeginBinding(target))
        {
            return;
        }

        lifetime.Add(gameEventBus.Subscribe<FacilityEvolutionCompletedEvent>(OnEvolutionCompleted));
        lifetime.Add(gameEventBus.Subscribe<FacilitySynthesisCompletedEvent>(OnSynthesisCompleted));
        lifetime.Add(gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnResearchCompleted));
        lifetime.Add(gameEventBus.Subscribe<InvasionCombatReportReadyEvent>(OnCombatReportReady));
        lifetime.Add(gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnDefenseFacilityTriggered));
        lifetime.Add(gameEventBus.Subscribe<InvasionSpawnedEvent>(OnInvasionSpawned));
        lifetime.Add(gameEventBus.Subscribe<InvasionFacilityDamagedEvent>(OnInvasionFacilityDamaged));
        lifetime.Add(gameEventBus.Subscribe<FacilityVisitEvent>(OnFacilityVisit));
    }

    public void Unbind(CodexRuntime target)
    {
        lifetime.Unbind(target);
    }

    public CodexReferenceSnapshot Capture()
    {
        return CodexDomainSnapshotFactory.CreateReferenceSnapshot(
            referenceCatalog,
            researchStateService.GetState(),
            synthesisRecipeQuery);
    }

    public void PublishUpdated(CodexUpdatedEvent updatedEvent)
    {
        gameEventBus.Publish(updatedEvent);
    }

    public void RaiseAlert(CodexAlertRequest request)
    {
        gameEventBus.RaiseAlert(
            request.Title,
            request.Message,
            EventAlertImportance.Medium,
            request.Category);
    }

    private void OnFacilityVisit(FacilityVisitEvent eventData)
    {
        CodexCharacterObservationSnapshot character =
            CodexDomainSnapshotFactory.CreateCharacter(eventData.visitorActor);
        CodexFacilityObservationSnapshot facility =
            CodexDomainSnapshotFactory.CreateFacility(
                eventData.facility,
                CodexInfoSource.Observation);
        if (character != null || facility != null)
        {
            RequireRuntime().RecordCharacterVisit(character, facility);
        }
    }

    private void OnDefenseFacilityTriggered(DefenseFacilityTriggeredEvent eventData)
    {
        RequireRuntime().RecordDefenseObservation(
            CodexDomainSnapshotFactory.CreateDefenseObservation(eventData.report));
    }

    private void OnCombatReportReady(InvasionCombatReportReadyEvent eventData)
    {
        RequireRuntime().RecordCombatReport(
            CodexDomainSnapshotFactory.CreateCombatObservation(eventData.report));
    }

    private void OnInvasionFacilityDamaged(InvasionFacilityDamagedEvent eventData)
    {
        RequireRuntime().RecordFacilityDamage(
            CodexDomainSnapshotFactory.CreateFacilityDamageObservation(eventData.facility));
    }

    private void OnInvasionSpawned(InvasionSpawnedEvent eventData)
    {
        CodexCharacterObservationSnapshot intruder =
            CodexDomainSnapshotFactory.CreateCharacter(eventData.intruderActor);
        if (intruder != null)
        {
            RequireRuntime().RecordInvasionSpawned(intruder);
        }
    }

    private void OnResearchCompleted(BlueprintResearchCompletedEvent eventData)
    {
        RequireRuntime().RecordResearch(CodexDomainSnapshotFactory.CreateResearchObservation(
            eventData.unlockResult,
            researchStateService.GetState(),
            synthesisRecipeQuery));
    }

    private void OnSynthesisCompleted(FacilitySynthesisCompletedEvent eventData)
    {
        RequireRuntime().RecordSynthesis(CodexDomainSnapshotFactory.CreateSynthesisObservation(
            eventData.result,
            researchStateService.GetState(),
            synthesisRecipeQuery));
    }

    private void OnEvolutionCompleted(FacilityEvolutionCompletedEvent eventData)
    {
        CodexEvolutionObservationSnapshot snapshot =
            CodexDomainSnapshotFactory.CreateEvolutionObservation(eventData.result);
        if (snapshot != null)
        {
            RequireRuntime().RecordEvolution(snapshot);
        }
    }

    private CodexRuntime RequireRuntime()
    {
        return lifetime.RequireTarget();
    }
}

public static class CodexDomainSnapshotFactory
{
    public static CodexReferenceSnapshot CreateReferenceSnapshot(
        ICodexReferenceCatalog catalog,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery recipeQuery)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        return new CodexReferenceSnapshot(
            catalog.Species
                .Where(species => species != null)
                .Select(species => new CodexCharacterObservationSnapshot(
                    CreateSpeciesEntry(species, CodexInfoSource.System))),
            catalog.Facilities
                .Where(building => building != null)
                .Select(building => CreateFacility(building, CodexInfoSource.System)),
            CreateRecipeObservation(researchState, recipeQuery, CodexInfoSource.System));
    }

    public static CodexCharacterObservationSnapshot CreateCharacter(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterIdentity identity = actor.Identity;
        CharacterSpeciesSO species = identity?.Data?.species;
        string speciesTag = species != null ? species.speciesTag : identity?.SpeciesTag;
        if (string.IsNullOrWhiteSpace(speciesTag))
        {
            return null;
        }

        CodexEntryObservationSnapshot baseEntry = species != null
            ? CreateSpeciesEntry(species, CodexInfoSource.Observation)
            : new CodexEntryObservationSnapshot(
                CodexEntryCategory.Monster,
                GetMonsterEntryId(speciesTag),
                speciesTag,
                Array.Empty<CodexInfoLine>());
        return new CodexCharacterObservationSnapshot(MergeEntries(
            baseEntry,
            new CodexEntryObservationSnapshot(
                CodexEntryCategory.Monster,
                baseEntry.EntryId,
                baseEntry.Title,
                new[]
                {
                    new CodexInfoLine($"관찰: {actor.name} 방문", CodexInfoSource.Observation)
                })));
    }

    public static CodexFacilityObservationSnapshot CreateFacility(
        BuildableObject facility,
        CodexInfoSource source)
    {
        return CreateFacility(facility != null ? facility.BuildingData : null, source);
    }

    public static CodexFacilityObservationSnapshot CreateFacility(
        BuildingSO building,
        CodexInfoSource source)
    {
        if (building == null)
        {
            return null;
        }

        List<CodexInfoLine> lines = new List<CodexInfoLine>();
        FacilityData facility = building.Facility;
        if (facility != null && facility.roles != FacilityRole.None)
        {
            Add(lines, $"역할: {CodexDomainTextFormatter.FormatFacilityRoles(facility.roles)}", source);
        }

        if (facility != null && facility.HasSupportedWorkTypes)
        {
            Add(lines, $"작업: {CodexDomainTextFormatter.FormatWorkTypes(facility.SupportedWorkTypeIds)}", source);
        }

        if (facility != null && facility.capacity > 0)
        {
            Add(lines, $"수용: {facility.capacity}", source);
        }

        if (building.RequiresStockForUse())
        {
            Add(lines, $"재고 필요: 내부 재고 {building.GetInternalStockCapacity()}", source);
        }

        string[] preferredSpeciesTags = building.GetPreferredSpeciesTags()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (preferredSpeciesTags.Length > 0)
        {
            Add(lines, $"시너지 대상: {string.Join(", ", preferredSpeciesTags)}", source);
        }

        DefenseFacilityData defense = building.Defense;
        if (defense != null && defense.IsDefenseFacility)
        {
            Add(lines, $"별 등급: {defense.star}성", source);
            Add(lines, $"공격 컨셉: {CodexDomainTextFormatter.FormatDefenseConcept(defense.concept)}", source);
            Add(lines, $"발동 조건: {CodexDomainTextFormatter.FormatTriggerTimings(defense.triggerTimings)}", source);
            Add(lines, $"대상: {CodexDomainTextFormatter.FormatTargetRule(defense.targetRule)}", source);
            if (defense.SupportsTrigger(DefenseTriggerTiming.GuardResponse)
                || defense.concept == DefenseAttackConcept.Guard)
            {
                Add(lines, "시너지 대상: 경비 직원", source);
            }

            foreach (string effect in CodexDomainTextFormatter.FormatDefenseEffects(defense))
            {
                Add(lines, $"효과: {effect}", source);
            }
        }

        return new CodexFacilityObservationSnapshot(new CodexEntryObservationSnapshot(
            CodexEntryCategory.Facility,
            CodexFacilityInfoWriter.GetFacilityEntryId(building.id),
            FacilityShopService.GetBuildingName(building),
            lines));
    }

    public static CodexInvasionObservationSnapshot CreateDefenseObservation(
        DefenseActivationSnapshot report)
    {
        if (report == null)
        {
            return new CodexInvasionObservationSnapshot(
                Array.Empty<CodexFacilityObservationSnapshot>(),
                Array.Empty<string>());
        }

        List<string> observations = new List<string>();
        foreach (string tag in report.EffectTags ?? Array.Empty<string>())
        {
            observations.AddRange(CodexInvasionObservationMapper.FromEffectTag(tag));
        }

        if (report.MovementDelaySeconds > 0f)
        {
            observations.Add("약점: 감속");
        }

        if (report.TotalDamage > 0f)
        {
            observations.Add("약점: 피해 누적");
        }

        CodexFacilityObservationSnapshot facility =
            CreateFacility(report.Facility, CodexInfoSource.Observation);
        return new CodexInvasionObservationSnapshot(
            facility != null ? new[] { facility } : Array.Empty<CodexFacilityObservationSnapshot>(),
            observations);
    }

    public static CodexInvasionObservationSnapshot CreateCombatObservation(
        InvasionCombatReportSnapshot report)
    {
        if (report == null)
        {
            return new CodexInvasionObservationSnapshot(
                Array.Empty<CodexFacilityObservationSnapshot>(),
                Array.Empty<string>());
        }

        CodexFacilityObservationSnapshot[] facilities = (report.DamagedFacilities
                ?? Array.Empty<InvasionFacilitySnapshot>())
            .Select(item => CreateFacility(item?.Building, CodexInfoSource.Observation))
            .Where(item => item != null)
            .ToArray();
        List<string> observations = (report.Observations ?? Array.Empty<string>())
            .Select(CodexInvasionObservationMapper.NormalizeObservation)
            .ToList();
        if (facilities.Length > 0)
        {
            observations.Add("성향: 시설 파괴 우선");
        }

        return new CodexInvasionObservationSnapshot(facilities, observations);
    }

    public static CodexInvasionObservationSnapshot CreateFacilityDamageObservation(
        BuildableObject facility)
    {
        CodexFacilityObservationSnapshot snapshot =
            CreateFacility(facility, CodexInfoSource.Observation);
        return new CodexInvasionObservationSnapshot(
            snapshot != null ? new[] { snapshot } : Array.Empty<CodexFacilityObservationSnapshot>(),
            new[] { "성향: 시설 파괴 우선" });
    }

    public static CodexResearchObservationSnapshot CreateResearchObservation(
        BlueprintResearchUnlockResult result,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery recipeQuery)
    {
        List<CodexEntryObservationSnapshot> unlockEntries = new List<CodexEntryObservationSnapshot>();
        foreach (BlueprintUnlockRecord unlock in result.Unlocks ?? Array.Empty<BlueprintUnlockRecord>())
        {
            CodexFacilityObservationSnapshot facility =
                CreateFacility(unlock.Facility as BuildingSO, CodexInfoSource.Research);
            if (facility == null)
            {
                continue;
            }

            CodexEntryObservationSnapshot entry = facility.Entry;
            if (!string.IsNullOrWhiteSpace(unlock.CodexDetail))
            {
                entry = MergeEntries(entry, new CodexEntryObservationSnapshot(
                    entry.Category,
                    entry.EntryId,
                    entry.Title,
                    new[]
                    {
                        new CodexInfoLine(unlock.CodexDetail, CodexInfoSource.Research)
                    }));
            }

            unlockEntries.Add(entry);
        }

        return new CodexResearchObservationSnapshot(
            MergeEntrySet(unlockEntries),
            CreateRecipeObservation(researchState, recipeQuery, CodexInfoSource.System));
    }

    public static CodexRecipeObservationSnapshot CreateSynthesisObservation(
        FacilitySynthesisResult result,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery recipeQuery)
    {
        List<CodexEntryObservationSnapshot> entries =
            CreateRecipeObservation(researchState, recipeQuery, CodexInfoSource.System)
                .Entries
                .ToList();
        CodexFacilityObservationSnapshot resultFacility =
            CreateFacility(result.ResultBuilding, CodexInfoSource.Synthesis);
        if (resultFacility != null)
        {
            entries.Add(resultFacility.Entry);
        }

        if (result.Recipe != null)
        {
            AddRecipeEntries(entries, result.Recipe, true, CodexInfoSource.Synthesis);
        }

        return new CodexRecipeObservationSnapshot(MergeEntrySet(entries));
    }

    public static CodexRecipeObservationSnapshot CreateRecipeObservation(
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery recipeQuery,
        CodexInfoSource source)
    {
        if (recipeQuery == null)
        {
            throw new ArgumentNullException(nameof(recipeQuery));
        }

        List<CodexEntryObservationSnapshot> entries = new List<CodexEntryObservationSnapshot>();
        foreach (FacilitySynthesisRecipeSO recipe in recipeQuery.GetAllRecipes()
                     .Where(recipe => recipe != null)
                     .OrderBy(recipe => recipe.recipeId, StringComparer.Ordinal))
        {
            bool visible = recipeQuery.IsVisible(recipe, researchState);
            if (visible)
            {
                AddRecipeEntries(entries, recipe, true, source);
            }
            else if (recipe.IsSpecial && recipe.HasValidData)
            {
                entries.Add(new CodexEntryObservationSnapshot(
                    CodexEntryCategory.Facility,
                    $"special_recipe_hint:{recipe.recipeId}",
                    "미확인 특수 조합식",
                    new[]
                    {
                        new CodexInfoLine(BuildSpecialRecipeHint(recipe), CodexInfoSource.System)
                    }));
            }
        }

        return new CodexRecipeObservationSnapshot(MergeEntrySet(entries));
    }

    public static CodexEvolutionObservationSnapshot CreateEvolutionObservation(
        FacilityEvolutionResult result)
    {
        if (!result.Success || result.Recipe == null)
        {
            return null;
        }

        BuildingSO resultBuilding = result.ResultBuilding != null
            ? result.ResultBuilding.BuildingData
            : result.Recipe.resultBuilding;
        CodexFacilityObservationSnapshot facility =
            CreateFacility(resultBuilding, CodexInfoSource.Evolution);
        if (facility == null)
        {
            return null;
        }

        string resultName = FacilityShopService.GetBuildingName(resultBuilding);
        string sourceName = !string.IsNullOrWhiteSpace(result.SourceFacilityName)
            ? result.SourceFacilityName
            : "이전 시설";
        FacilityEvolutionProposal proposal = result.Proposal;
        List<CodexInfoLine> lines = new List<CodexInfoLine>
        {
            new CodexInfoLine(
                $"계보 진화: {sourceName} -> {resultName} ({result.ResultStarGrade}성)",
                CodexInfoSource.Evolution),
            new CodexInfoLine($"진화식: {result.Recipe.DisplayName}", CodexInfoSource.Evolution)
        };
        AddLabeled(lines, "정체성", proposal.FacilityIdentitySummary);
        AddLabeled(lines, "진화 기록", proposal.FlavorText);
        AddLabeled(lines, "해석 출처", proposal.Source);
        AddLabeled(
            lines,
            "변이",
            CodexTextFormatter.FormatEvolutionMutationTags(result.MutationTags));

        return new CodexEvolutionObservationSnapshot(MergeEntries(
            facility.Entry,
            new CodexEntryObservationSnapshot(
                facility.Entry.Category,
                facility.Entry.EntryId,
                facility.Entry.Title,
                lines)));
    }

    private static CodexEntryObservationSnapshot CreateSpeciesEntry(
        CharacterSpeciesSO species,
        CodexInfoSource source)
    {
        string speciesTag = !string.IsNullOrWhiteSpace(species.speciesTag)
            ? species.speciesTag
            : $"species-{species.id}";
        string title = !string.IsNullOrWhiteSpace(species.displayName)
            ? species.displayName
            : speciesTag;
        List<CodexInfoLine> lines = new List<CodexInfoLine>();
        Add(lines, species.shortDescription, source);
        foreach (string preferred in CodexTextFormatter.Canonicalize(species.preferredFacilityLabels))
        {
            Add(lines, $"선호: {preferred}", source);
        }

        foreach (string disliked in CodexTextFormatter.Canonicalize(species.dislikedEnvironmentLabels))
        {
            Add(lines, $"기피: {disliked}", source);
        }

        if (!string.IsNullOrWhiteSpace(species.incidentName))
        {
            Add(lines, $"사고 위험: {species.incidentName}", source);
        }

        Add(lines, species.incidentDescription, source);
        if (species.incidentMitigatingRoles != FacilityRole.None)
        {
            Add(
                lines,
                $"완화 역할: {CodexDomainTextFormatter.FormatFacilityRoles(species.incidentMitigatingRoles)}",
                source);
        }

        return new CodexEntryObservationSnapshot(
            CodexEntryCategory.Monster,
            GetMonsterEntryId(speciesTag),
            title,
            lines);
    }

    private static void AddRecipeEntries(
        ICollection<CodexEntryObservationSnapshot> entries,
        FacilitySynthesisRecipeSO recipe,
        bool reveal,
        CodexInfoSource source)
    {
        if (recipe == null || !recipe.HasValidData)
        {
            return;
        }

        string materials = string.Join(
            " + ",
            recipe.materialBuildings.Select(FacilityShopService.GetBuildingName));
        string resultName = FacilityShopService.GetBuildingName(recipe.resultBuilding);
        string line = reveal
            ? $"조합식: {materials} -> {resultName}"
            : BuildSpecialRecipeHint(recipe);
        foreach (BuildingSO building in recipe.materialBuildings
                     .Concat(new[] { recipe.resultBuilding })
                     .Where(building => building != null))
        {
            entries.Add(CreateLineEntry(building, line, source));
        }
    }

    private static CodexEntryObservationSnapshot CreateLineEntry(
        BuildingSO building,
        string line,
        CodexInfoSource source)
    {
        return new CodexEntryObservationSnapshot(
            CodexEntryCategory.Facility,
            CodexFacilityInfoWriter.GetFacilityEntryId(building.id),
            FacilityShopService.GetBuildingName(building),
            new[] { new CodexInfoLine(line, source) });
    }

    private static string BuildSpecialRecipeHint(FacilitySynthesisRecipeSO recipe)
    {
        string concept = recipe.resultBuilding != null && recipe.resultBuilding.Defense != null
            ? CodexDomainTextFormatter.FormatDefenseConcept(recipe.resultBuilding.Defense.concept)
            : "특수";
        return $"특수 조합식 힌트: {concept} 계열 연구 필요";
    }

    private static IReadOnlyList<CodexEntryObservationSnapshot> MergeEntrySet(
        IEnumerable<CodexEntryObservationSnapshot> entries)
    {
        return (entries ?? Array.Empty<CodexEntryObservationSnapshot>())
            .Where(entry => entry != null)
            .GroupBy(
                entry => $"{(int)entry.Category}:{entry.EntryId}",
                StringComparer.Ordinal)
            .Select(group => group.Aggregate(MergeEntries))
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CodexEntryObservationSnapshot MergeEntries(
        CodexEntryObservationSnapshot left,
        CodexEntryObservationSnapshot right)
    {
        if (left == null)
        {
            return right;
        }

        if (right == null)
        {
            return left;
        }

        if (left.Category != right.Category
            || !string.Equals(left.EntryId, right.EntryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot merge different codex entries.");
        }

        return new CodexEntryObservationSnapshot(
            left.Category,
            left.EntryId,
            left.Title,
            left.Lines.Concat(right.Lines));
    }

    private static void Add(
        ICollection<CodexInfoLine> lines,
        string text,
        CodexInfoSource source)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            lines.Add(new CodexInfoLine(text, source));
        }
    }

    private static void AddLabeled(
        ICollection<CodexInfoLine> lines,
        string label,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(new CodexInfoLine(
                $"{label}: {value}",
                CodexInfoSource.Evolution));
        }
    }

    private static string GetMonsterEntryId(string speciesTag)
    {
        return $"monster:{speciesTag.Trim()}";
    }
}

public static class CodexDomainTextFormatter
{
    public static string FormatFacilityRoles(FacilityRole roles)
    {
        return string.Join(", ", FacilityRoleCatalog
            .Enumerate(roles)
            .Select(definition => definition.RoomLabel));
    }

    public static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        if (workTypeIds == null)
        {
            return string.Empty;
        }

        return string.Join(", ", workTypeIds
            .Where(workTypeId => workTypeId.IsValid)
            .Select(workTypeId => WorkTypeCatalog.TryGet(
                    workTypeId,
                    out WorkTypeDefinition definition)
                ? definition.DisplayName
                : workTypeId.ToString()));
    }

    internal static string FormatLegacyWorkTypes(FacilityWorkType workTypes)
    {
        return string.Join(", ", FacilityWorkTypeMap
            .Enumerate(workTypes)
            .Select(definition => definition.DisplayName));
    }

    public static string FormatDefenseConcept(DefenseAttackConcept concept)
    {
        return concept switch
        {
            DefenseAttackConcept.Physical => "물리",
            DefenseAttackConcept.Poison => "독",
            DefenseAttackConcept.Fire => "화염",
            DefenseAttackConcept.Lightning => "번개",
            DefenseAttackConcept.Ice => "냉기",
            DefenseAttackConcept.Guard => "경비",
            _ => "없음"
        };
    }

    public static string FormatTriggerTimings(DefenseTriggerTiming timings)
    {
        if (timings == DefenseTriggerTiming.None)
        {
            return "없음";
        }

        return string.Join(", ", Enum.GetValues(typeof(DefenseTriggerTiming))
            .Cast<DefenseTriggerTiming>()
            .Where(timing => timing != DefenseTriggerTiming.None && (timings & timing) != 0)
            .Select(timing => timing switch
            {
                DefenseTriggerTiming.OnEnter => "입장 시",
                DefenseTriggerTiming.Periodic => "머무는 동안",
                DefenseTriggerTiming.Cooldown => "쿨타임",
                DefenseTriggerTiming.GuardResponse => "경비 반응",
                _ => timing.ToString()
            }));
    }

    public static string FormatTargetRule(DefenseTargetRule targetRule)
    {
        return targetRule switch
        {
            DefenseTargetRule.EnteringIntruder => "입장한 침입자",
            DefenseTargetRule.IntrudersInRoom => "방 안 침입자",
            DefenseTargetRule.AllIntrudersInRoom => "방 안 모든 침입자",
            DefenseTargetRule.GuardTarget => "경비 대상",
            _ => targetRule.ToString()
        };
    }

    public static IEnumerable<string> FormatDefenseEffects(DefenseFacilityData defense)
    {
        return (defense?.effectAssets ?? Array.Empty<DefenseEffectSO>())
            .Where(effect => effect != null)
            .Select(effect => effect.FormatSummary());
    }
}
