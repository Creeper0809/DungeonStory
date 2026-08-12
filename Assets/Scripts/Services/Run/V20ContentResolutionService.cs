using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public enum ContentResolutionRequestKind
{
    DailyEvaluation,
    SocietyChoice,
    FactionChapterChoice,
    FactionContractAccept,
    FactionContractOutcome,
    CulturalPractice,
    CulturalPracticeNeglect
}

public sealed class ContentResolutionRequest
{
    public string ActionId { get; set; } = string.Empty;
    public ContentResolutionRequestKind Kind { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public string ChoiceId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public bool ContractSucceeded { get; set; }
    public int AbsoluteDay { get; set; }
    public V20DailyEventContext DailyContext { get; set; }
    public RunMilestoneEvaluationSnapshot Requirements { get; set; }
    public string DefinitionId { get; set; } = string.Empty;
    public IReadOnlyList<string> ParticipantCharacterIds { get; set; } =
        Array.Empty<string>();
}

public sealed class ContentResolutionResult
{
    public string ActionId { get; internal set; } = string.Empty;
    public IReadOnlyList<V20ResolvedEventResult> Resolutions { get; internal set; } =
        Array.Empty<V20ResolvedEventResult>();
}

public enum FacilityCapabilityKind
{
    None,
    Meal,
    Purchase,
    Rest,
    Training,
    Research,
    Mana,
    Logistics,
    Toilet,
    Hygiene,
    Administration,
    Security,
    Entertainment,
    Medical
}

public interface IFacilityCapabilityQuery
{
    IReadOnlyList<BuildableObject> FindOperational(
        FacilityCapabilityKind capability,
        string buildingDefinitionId = "");

    IReadOnlyList<BuildableObject> FindOperational(
        ResearchFacilityCommandKind command);
}

public sealed class FacilityCapabilityQuery : IFacilityCapabilityQuery
{
    private readonly IBuildingWorldQuery world;

    public FacilityCapabilityQuery(IBuildingWorldQuery world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public IReadOnlyList<BuildableObject> FindOperational(
        FacilityCapabilityKind capability,
        string buildingDefinitionId = "")
    {
        string definitionId = buildingDefinitionId?.Trim() ?? string.Empty;
        FacilityRole role = ToRole(capability);
        return world.Buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.BuildingData != null
                && (definitionId.Length == 0
                    || string.Equals(
                        BuildingDefinitionId(value.BuildingData),
                        definitionId,
                        StringComparison.Ordinal))
                && (role == FacilityRole.None || value.SupportsFacilityRole(role))
                && IsOperational(value))
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<BuildableObject> FindOperational(
        ResearchFacilityCommandKind command)
    {
        if (command == ResearchFacilityCommandKind.None)
        {
            return Array.Empty<BuildableObject>();
        }

        return world.Buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.BuildingData != null
                && value.BuildingData.ResearchFacilityCommand == command
                && IsOperational(value))
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsOperational(BuildableObject building)
    {
        BuildingRoomOperationalSnapshot profile =
            building.GetRoomOperationalProfile();
        return profile != null && (!profile.HasRoom || profile.IsUsableRoom);
    }

    private static string BuildingDefinitionId(BuildingSO building) =>
        !string.IsNullOrWhiteSpace(building?.ContentDefinitionId)
            ? building.ContentDefinitionId
            : building == null
                ? string.Empty
                : $"building:{building.id}";

    private static FacilityRole ToRole(FacilityCapabilityKind capability) =>
        capability switch
        {
            FacilityCapabilityKind.Meal => FacilityRole.Meal,
            FacilityCapabilityKind.Purchase => FacilityRole.Purchase,
            FacilityCapabilityKind.Rest => FacilityRole.Rest,
            FacilityCapabilityKind.Training => FacilityRole.Training,
            FacilityCapabilityKind.Research => FacilityRole.Research,
            FacilityCapabilityKind.Mana => FacilityRole.Mana,
            FacilityCapabilityKind.Logistics => FacilityRole.Logistics,
            FacilityCapabilityKind.Toilet => FacilityRole.Toilet,
            FacilityCapabilityKind.Hygiene => FacilityRole.Hygiene,
            FacilityCapabilityKind.Administration => FacilityRole.Administration,
            FacilityCapabilityKind.Security => FacilityRole.Security,
            FacilityCapabilityKind.Entertainment => FacilityRole.Entertainment,
            FacilityCapabilityKind.Medical => FacilityRole.Medical,
            _ => FacilityRole.None
        };
}

public interface IContentRequirementEvaluator
{
    bool TryEvaluate(
        V20ContentRequirementSet requirements,
        RunMilestoneEvaluationSnapshot world,
        IReadOnlyList<string> participantCharacterIds,
        out DomainFailure failure);
}

public sealed class ContentRequirementEvaluator : IContentRequirementEvaluator
{
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterNarrativeQuery narrative;
    private readonly IFacilityCapabilityQuery facilities;

    public ContentRequirementEvaluator(
        ICharacterWorldQuery characters,
        ICharacterLifeQuery life,
        ICharacterNarrativeQuery narrative,
        IFacilityCapabilityQuery facilities)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
    }

    public bool TryEvaluate(
        V20ContentRequirementSet requirements,
        RunMilestoneEvaluationSnapshot world,
        IReadOnlyList<string> participantCharacterIds,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        requirements ??= new V20ContentRequirementSet();
        if (world == null)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (!(requirements.research ?? new()).All(value =>
                value != null && world.CompletedResearchIds.Contains(value.researchNumericId))
            || !(requirements.requiredFlags ?? new()).All(world.WorldFlags.Contains)
            || !(requirements.excludedFlags ?? new()).All(value => !world.WorldFlags.Contains(value))
            || !(requirements.worldMetrics ?? new()).All(value => value != null
                && world.WorldMetrics.TryGetValue(value.kind, out float actual)
                && actual >= value.minimumValue)
            || !(requirements.items ?? new()).All(value => value != null
                && world.ItemQuantities.TryGetValue(value.itemDefinitionId, out int count)
                && count >= value.amount)
            || !(requirements.factions ?? new()).All(value => value != null
                && world.Factions.TryGetValue(value.factionId, out FactionCampaignStateSaveData faction)
                && faction.rapport >= value.minimumRapport
                && faction.grievance <= value.maximumGrievance
                && faction.obligationTokens >= value.minimumObligationTokens))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        foreach (V20FacilityRequirement requirement in
                 requirements.facilities ?? new List<V20FacilityRequirement>())
        {
            if (requirement == null)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
            FacilityCapabilityKind capability = ParseCapability(
                requirement.capabilityId);
            if (!string.IsNullOrWhiteSpace(requirement.capabilityId)
                && capability == FacilityCapabilityKind.None)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
            int count = requirement.mustBeOperational
                ? facilities.FindOperational(
                    capability,
                    requirement.buildingDefinitionId).Count
                : world.FacilityCounts.TryGetValue(
                    V20CampaignRuntime.FacilityRequirementKey(requirement),
                    out int authoredCount)
                        ? authoredCount
                        : 0;
            if (count < requirement.minimumCount)
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
                return false;
            }
        }

        CharacterActor[] candidates = ResolveParticipants(participantCharacterIds);
        foreach (V20CharacterRequirement requirement in
                 requirements.characters ?? new List<V20CharacterRequirement>())
        {
            int match = Array.FindIndex(candidates, actor =>
                Matches(actor, requirement));
            if (match < 0)
            {
                failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
                return false;
            }
            candidates = candidates.Where((_, index) => index != match).ToArray();
        }
        return true;
    }

    private CharacterActor[] ResolveParticipants(
        IReadOnlyList<string> participantCharacterIds)
    {
        HashSet<string> requested = (participantCharacterIds
                ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        return characters.Characters
            .Where(value => value != null
                && value.Identity != null
                && value.CurrentHealth > 0f
                && (requested.Count == 0
                    || requested.Contains(value.Identity.PersistentId)))
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
    }

    private bool Matches(
        CharacterActor actor,
        V20CharacterRequirement requirement)
    {
        if (actor == null || requirement == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !life.TryGet(id, out CharacterLifeRecord record)
            || record.LifeStage < requirement.minimumLifeStage
            || record.LifeStage > requirement.maximumLifeStage
            || actor.CurrentHealth < requirement.minimumHealth)
        {
            return false;
        }
        HashSet<string> traits = new(StringComparer.Ordinal);
        foreach (CharacterTraitSO trait in actor.Progression?.ResolveSelectedTraits()
                     ?? Array.Empty<CharacterTraitSO>())
        {
            traits.Add(trait.DefinitionId.Value);
            traits.Add(trait.id.ToString());
        }
        if (narrative.TryGet(id, out CharacterNarrativeSnapshot snapshot))
        {
            traits.UnionWith(snapshot.ExpressedHeritableTraitIds
                ?? Array.Empty<string>());
        }
        return (string.IsNullOrWhiteSpace(requirement.requiredTraitId)
                || traits.Contains(requirement.requiredTraitId.Trim()))
            && (string.IsNullOrWhiteSpace(requirement.excludedTraitId)
                || !traits.Contains(requirement.excludedTraitId.Trim()));
    }

    private static FacilityCapabilityKind ParseCapability(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return FacilityCapabilityKind.None;
        string leaf = normalized.Split(':', '.', '/').Last();
        return Enum.TryParse(leaf, true, out FacilityCapabilityKind result)
            ? result
            : FacilityCapabilityKind.None;
    }
}

public interface IContentResolutionService
{
    bool TryExecute(
        ContentResolutionRequest request,
        out ContentResolutionResult result,
        out DomainFailure failure);
}

public static class V21ContentEffectExecutionRegistry
{
    private static readonly IReadOnlyDictionary<V20ContentEffectKind, string>
        Owners = new Dictionary<V20ContentEffectKind, string>
        {
            [V20ContentEffectKind.Mood] = "character mood command / characters",
            [V20ContentEffectKind.Trauma] = "grief-trauma command / characters.psychosocial",
            [V20ContentEffectKind.SkillExperience] = "character progression command / characters",
            [V20ContentEffectKind.Health] = "body-health command / characters.health",
            [V20ContentEffectKind.Relationship] = "social-memory command / characters",
            [V20ContentEffectKind.FactionRapport] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.FactionGrievance] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.FactionObligation] = "faction campaign command / factions.campaign",
            [V20ContentEffectKind.Money] = "money account command / session",
            [V20ContentEffectKind.ItemGrant] = "physical item command / items.world-stacks",
            [V20ContentEffectKind.ItemConsume] = "atomic item consumption / items.world-stacks",
            [V20ContentEffectKind.WorldFlag] = "campaign command / society.events",
            [V20ContentEffectKind.WorkDelayDays] = "campaign work-delay command / society.events",
            [V20ContentEffectKind.Threat] = "milestone pressure command / run.milestones",
            [V20ContentEffectKind.DiseaseExposure] = "population-health command / characters.health",
            [V20ContentEffectKind.AmbitionProgress] = "narrative command / characters.narrative",
            [V20ContentEffectKind.MilestonePressure] = "milestone pressure command / run.milestones"
        };

    public static bool HasExecutionOwner(V20ContentEffectKind kind) =>
        kind != V20ContentEffectKind.None && Owners.ContainsKey(kind);

    public static string DescribeOwner(V20ContentEffectKind kind) =>
        Owners.TryGetValue(kind, out string owner) ? owner : string.Empty;
}

public static class V21ContentEffectCommitPreflight
{
    public static bool TryPlanItemCosts(
        IEnumerable<V20ContentEffect> effects,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out IReadOnlyList<ReservedItemConsumption> costs,
        out string missingItemId)
    {
        List<ReservedItemConsumption> planned = new();
        missingItemId = string.Empty;
        WorldItemStackSnapshot[] available = (stacks
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && value.AvailableQuantity > 0)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        foreach (IGrouping<string, V20ContentEffect> group in (effects
                     ?? Array.Empty<V20ContentEffect>())
                 .Where(value => value != null
                     && value.IsValid
                     && value.kind == V20ContentEffectKind.ItemConsume)
                 .GroupBy(
                     value => value.targetId?.Trim() ?? string.Empty,
                     StringComparer.Ordinal)
                 .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int needed = group.Sum(value =>
                Math.Max(0, Mathf.RoundToInt(value.amount)));
            foreach (WorldItemStackSnapshot stack in available.Where(value =>
                         string.Equals(
                             value.ItemId,
                             group.Key,
                             StringComparison.Ordinal)))
            {
                int quantity = Math.Min(needed, stack.Quantity);
                if (quantity <= 0)
                {
                    continue;
                }
                planned.Add(new ReservedItemConsumption(stack.StackId, quantity));
                needed -= quantity;
                if (needed == 0)
                {
                    break;
                }
            }
            if (needed != 0)
            {
                missingItemId = group.Key;
                costs = Array.Empty<ReservedItemConsumption>();
                return false;
            }
        }

        costs = planned.AsReadOnly();
        return true;
    }
}

public static class V21ContentAlertActionIds
{
    private const string Prefix = "v21-content";

    public static string Society(string instanceId, string choiceId) =>
        Join("society", instanceId, choiceId);

    public static string FactionChapter(string factionId, string choiceId) =>
        Join("faction-chapter", factionId, choiceId);

    public static string FactionContractAccept(
        string factionId,
        string contractId) =>
        Join("faction-contract-accept", factionId, contractId);

    public static string FactionContractOutcome(
        string factionId,
        bool succeeded) =>
        Join("faction-contract-outcome", factionId, succeeded ? "success" : "failure");

    public static string ReproductionStart(
        string processId,
        bool useFertilityTreatment = false) =>
        Join(
            "reproduction-start",
            processId,
            useFertilityTreatment ? "treatment" : "standard");

    public static string Festival(string festivalId) =>
        Join("festival", festivalId, "resolve");

    public static string AgeTreatment(
        CharacterId patientId,
        AgeTreatmentKind treatment,
        string facilityInstanceId) =>
        Join(
            "age-treatment",
            patientId.Value,
            $"{(int)treatment},{facilityInstanceId?.Trim() ?? string.Empty}");

    public static string Funeral(CharacterId deceasedId) =>
        Join("social-care", deceasedId.Value, "funeral");

    public static string Counseling(CharacterId patientId) =>
        Join("social-care", patientId.Value, "counsel");

    public static string DiseaseResponse(
        CharacterId patientId,
        string diseaseId,
        string responseId,
        string facilityInstanceId) =>
        Join(
            "disease-response",
            patientId.Value,
            string.Join(",", diseaseId, responseId, facilityInstanceId));

    public static string CertifiedSeed(
        string cropId,
        string facilityInstanceId) =>
        Join("certified-seed", cropId, facilityInstanceId);

    public static string TraitAnalysis(CharacterId characterId) =>
        Join("trait-analysis", characterId.Value, "analyze");

    public static string CulturalPractice(
        string practiceId,
        IEnumerable<CharacterId> participantIds) =>
        Join(
            "cultural-practice",
            practiceId,
            string.Join(",", (participantIds ?? Array.Empty<CharacterId>())
                .Where(value => value.IsValid)
                .Select(value => value.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)));

    public static string CulturalPracticeNeglect(
        string practiceId,
        IEnumerable<CharacterId> participantIds) =>
        Join(
            "cultural-practice-neglect",
            practiceId,
            string.Join(",", (participantIds ?? Array.Empty<CharacterId>())
                .Where(value => value.IsValid)
                .Select(value => value.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)));

    public static bool TryParse(
        string actionId,
        out string kind,
        out string first,
        out string second)
    {
        kind = string.Empty;
        first = string.Empty;
        second = string.Empty;
        string[] segments = (actionId ?? string.Empty).Split('|');
        if (segments.Length != 4
            || !string.Equals(segments[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            kind = Uri.UnescapeDataString(segments[1]);
            first = Uri.UnescapeDataString(segments[2]);
            second = Uri.UnescapeDataString(segments[3]);
            return kind.Length > 0 && first.Length > 0 && second.Length > 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string Join(string kind, string first, string second) =>
        string.Join(
            "|",
            Prefix,
            Uri.EscapeDataString(kind?.Trim() ?? string.Empty),
            Uri.EscapeDataString(first?.Trim() ?? string.Empty),
            Uri.EscapeDataString(second?.Trim() ?? string.Empty));
}

public sealed class V21ContentAlertChoiceActionDispatcher :
    IEventAlertChoiceActionDispatcher
{
    private readonly IContentResolutionService content;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IReproductionCommand reproduction;
    private readonly IFestivalCommand festivals;
    private readonly IAgeTreatmentCommand ageTreatments;
    private readonly ISocialCareCommand socialCare;
    private readonly IDiseaseFieldResponseCommand diseaseResponses;
    private readonly ICertifiedSeedCommand certifiedSeeds;
    private readonly ITraitAnalysisCommand traitAnalysis;

    public V21ContentAlertChoiceActionDispatcher(
        IContentResolutionService content,
        IV20MilestoneWorldSnapshotQuery world,
        IGameCalendar calendar,
        IGameEventBus events,
        IReproductionCommand reproduction,
        IFestivalCommand festivals,
        IAgeTreatmentCommand ageTreatments,
        ISocialCareCommand socialCare,
        IDiseaseFieldResponseCommand diseaseResponses,
        ICertifiedSeedCommand certifiedSeeds,
        ITraitAnalysisCommand traitAnalysis)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.festivals = festivals
            ?? throw new ArgumentNullException(nameof(festivals));
        this.ageTreatments = ageTreatments
            ?? throw new ArgumentNullException(nameof(ageTreatments));
        this.socialCare = socialCare
            ?? throw new ArgumentNullException(nameof(socialCare));
        this.diseaseResponses = diseaseResponses
            ?? throw new ArgumentNullException(nameof(diseaseResponses));
        this.certifiedSeeds = certifiedSeeds
            ?? throw new ArgumentNullException(nameof(certifiedSeeds));
        this.traitAnalysis = traitAnalysis
            ?? throw new ArgumentNullException(nameof(traitAnalysis));
    }

    public bool TryDispatch(string actionId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!V21ContentAlertActionIds.TryParse(
                actionId,
                out string kind,
                out string first,
                out string second))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        int absoluteDay = Math.Max(1, calendar.Day);
        if (string.Equals(kind, "reproduction-start", StringComparison.Ordinal))
        {
            bool useTreatment = string.Equals(
                second,
                "treatment",
                StringComparison.Ordinal);
            if (!useTreatment
                && !string.Equals(second, "standard", StringComparison.Ordinal)
                && !string.Equals(second, "start", StringComparison.Ordinal))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            return reproduction.TryStart(first, useTreatment, out failure);
        }
        if (string.Equals(kind, "festival", StringComparison.Ordinal))
        {
            FestivalScheduleRequest schedule = new()
            {
                ActionId = actionId.Trim(),
                FestivalId = first,
                ParticipantIds = world.LivingCharacters
                    .Select(CharacterPersistentIdentity.Require)
                    .OrderBy(value => value.Value, StringComparer.Ordinal)
                    .ToArray()
            };
            return festivals.Schedule(
                    schedule,
                    out FestivalPreparedOrder order,
                    out failure)
                && festivals.Resolve(order, out failure);
        }
        if (string.Equals(kind, "age-treatment", StringComparison.Ordinal))
        {
            string[] details = second.Split(',');
            if (details.Length != 2
                || !int.TryParse(details[0], out int rawTreatment)
                || !Enum.IsDefined(typeof(AgeTreatmentKind), rawTreatment))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            AgeTreatmentOrderRequest treatment = new(
                new CharacterId(first),
                (AgeTreatmentKind)rawTreatment,
                preferredDoctorId: string.Empty,
                facilityInstanceId: details[1]);
            return ageTreatments.TryCreateOrder(
                treatment,
                out _,
                out failure);
        }
        if (string.Equals(kind, "social-care", StringComparison.Ordinal))
        {
            CharacterId subjectId = new(first);
            if (string.Equals(second, "funeral", StringComparison.Ordinal))
            {
                return socialCare.TryHoldFuneral(
                    actionId,
                    subjectId,
                    world.LivingCharacters
                        .Select(CharacterPersistentIdentity.Require)
                        .OrderBy(value => value.Value, StringComparer.Ordinal)
                        .ToArray(),
                    facilityInstanceId: string.Empty,
                    out failure);
            }
            if (string.Equals(second, "counsel", StringComparison.Ordinal))
            {
                return socialCare.TryCounsel(actionId, subjectId, out failure);
            }
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (string.Equals(kind, "disease-response", StringComparison.Ordinal))
        {
            string[] details = second.Split(',');
            if (details.Length != 3)
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            return diseaseResponses.TryApply(
                new CharacterId(first),
                details[0],
                details[1],
                details[2],
                out failure);
        }
        if (string.Equals(kind, "certified-seed", StringComparison.Ordinal))
        {
            return certifiedSeeds.TryPlan(
                actionId,
                first,
                second,
                out failure);
        }
        if (string.Equals(kind, "trait-analysis", StringComparison.Ordinal))
        {
            return traitAnalysis.TryAnalyze(
                new CharacterId(first),
                out _,
                out failure);
        }

        ContentResolutionRequest request = new()
        {
            ActionId = actionId.Trim(),
            AbsoluteDay = absoluteDay,
            Requirements = world.Build(absoluteDay)
        };
        switch (kind)
        {
            case "society":
                request.Kind = ContentResolutionRequestKind.SocietyChoice;
                request.InstanceId = first;
                request.ChoiceId = second;
                break;
            case "faction-chapter":
                request.Kind = ContentResolutionRequestKind.FactionChapterChoice;
                request.FactionId = first;
                request.ChoiceId = second;
                break;
            case "faction-contract-accept":
                request.Kind = ContentResolutionRequestKind.FactionContractAccept;
                request.FactionId = first;
                request.ContractId = second;
                break;
            case "faction-contract-outcome":
                request.Kind = ContentResolutionRequestKind.FactionContractOutcome;
                request.FactionId = first;
                request.ContractSucceeded = string.Equals(
                    second,
                    "success",
                    StringComparison.Ordinal);
                break;
            case "cultural-practice":
                request.Kind = ContentResolutionRequestKind.CulturalPractice;
                request.DefinitionId = first;
                request.ParticipantCharacterIds = second
                    .Split(',')
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                break;
            case "cultural-practice-neglect":
                request.Kind = ContentResolutionRequestKind.CulturalPracticeNeglect;
                request.DefinitionId = first;
                request.ParticipantCharacterIds = second
                    .Split(',')
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                break;
            default:
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
        }

        if (!content.TryExecute(request, out ContentResolutionResult result, out failure))
        {
            return false;
        }

        foreach (V20ResolvedEventResult resolved in result.Resolutions)
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                resolved.DefinitionId,
                resolved.ResolutionId,
                resolved.Effects,
                physicalEffectsApplied: true));
        }
        return true;
    }
}

public sealed class V20ContentResolutionService : IContentResolutionService
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly IContentRequirementEvaluator requirements;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterNarrativeQuery narrativeQuery;
    private readonly ICharacterNarrativeCommand narrative;
    private readonly IGriefTraumaService grief;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly IPopulationHealthService populationHealth;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;
    private readonly IWorldItemStackRuntime items;
    private readonly IItemTransferService transfers;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IGameMoneyAccount money;
    private readonly IFacilityCapabilityQuery facilities;

    public V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        ICharacterNarrativeCatalog narrativeCatalog,
        IContentRequirementEvaluator requirements,
        ICharacterWorldQuery characters,
        ICharacterNarrativeQuery narrativeQuery,
        ICharacterNarrativeCommand narrative,
        IGriefTraumaService grief,
        ICharacterBodyHealthCommand bodyHealth,
        IPopulationHealthService populationHealth,
        IDiseaseDefinitionCatalog diseases,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems,
        IWorldItemStackRuntime items,
        IItemTransferService transfers,
        IWorldDropZoneQuery dropZones,
        IGameMoneyAccount money,
        IFacilityCapabilityQuery facilities)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.narrativeQuery = narrativeQuery
            ?? throw new ArgumentNullException(nameof(narrativeQuery));
        this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.populationHealth = populationHealth ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public bool TryExecute(
        ContentResolutionRequest request,
        out ContentResolutionResult result,
        out DomainFailure failure)
    {
        result = null;
        failure = DomainFailure.None;
        if (request == null || string.IsNullOrWhiteSpace(request.ActionId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        RunMilestoneEvaluationSnapshot world = request.Requirements
            ?? request.DailyContext?.Requirements;
        if (!TryResolveRequestRequirements(
                request,
                world,
                out V20ContentRequirementSet requestedRequirements,
                out IReadOnlyList<string> requestedParticipants,
                out failure)
            || !requirements.TryEvaluate(
                requestedRequirements,
                world,
                requestedParticipants,
                out failure))
        {
            return false;
        }

        V20CampaignRuntime candidate = CreateCampaignCandidate();
        if (!TryResolveCandidate(
                candidate,
                request,
                out IReadOnlyList<V20ResolvedEventResult> resolved,
                out failure))
        {
            return false;
        }
        resolved = ExpandAmbitionCompletionRewards(candidate, resolved);
        foreach (V20ResolvedEventResult resolution in resolved)
        {
            if (!requirements.TryEvaluate(
                    RequirementsForResolvedEffect(resolution),
                    world,
                    resolution.ParticipantCharacterIds,
                    out failure))
            {
                return false;
            }
        }

        if (!TryPreflightEffects(
                request,
                resolved,
                out EffectCommitPlan plan,
                out failure))
        {
            return false;
        }

        if (!TryPrepareAdministrativeSeal(
                request,
                out WorldItemStackSnapshot administrativeSeal,
                out failure))
        {
            plan.Release(reservations);
            return false;
        }

        SeasonalEventAggregateState seasonal = live.PrepareSeasonal(
            candidate.CaptureSeasonal());
        SocietyEventAggregateState society = live.PrepareSociety(
            candidate.CaptureSociety());
        FactionCampaignAggregateState factions = live.PrepareFactions(
            candidate.CaptureFactions());
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            candidate.CaptureMilestones());

        ItemInstanceComponentSaveData previousSealDurability = null;
        if (administrativeSeal != null)
        {
            float current = DurableToolItemRules.ReadCurrentDurability(
                administrativeSeal.ItemId,
                administrativeSeal.Components);
            previousSealDurability = DurableToolItemRules.CreateDurability(
                administrativeSeal.ItemId,
                current);
            if (!items.TrySetInstanceComponent(
                    administrativeSeal.StackId,
                    DurableToolItemRules.CreateDurability(
                        administrativeSeal.ItemId,
                        current - 1f)))
            {
                plan.Release(reservations);
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    DurableToolItemRules.AdministrativeSeal);
                return false;
            }
        }

        if (!TryCommitEffects(request, resolved, plan, out failure))
        {
            if (administrativeSeal != null && previousSealDurability != null)
            {
                items.TrySetInstanceComponent(
                    administrativeSeal.StackId,
                    previousSealDurability);
            }
            plan.Release(reservations);
            return false;
        }

        live.PublishSeasonal(seasonal);
        live.PublishSociety(society);
        live.PublishFactions(factions);
        live.PublishMilestones(milestones);
        result = new ContentResolutionResult
        {
            ActionId = request.ActionId.Trim(),
            Resolutions = resolved
        };
        return true;
    }

    private bool TryPrepareAdministrativeSeal(
        ContentResolutionRequest request,
        out WorldItemStackSnapshot seal,
        out DomainFailure failure)
    {
        seal = null;
        failure = DomainFailure.None;
        if (request.Kind is not (
                ContentResolutionRequestKind.FactionChapterChoice
                or ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.FactionContractOutcome))
        {
            return true;
        }

        BuildableObject office = facilities
            .FindOperational(FacilityCapabilityKind.Administration)
            .FirstOrDefault();
        if (office == null)
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                FacilityCapabilityKind.Administration.ToString());
            return false;
        }

        string destinationId = office.PersistentInstanceId.Value;
        seal = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)
                && DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components) > 0f)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (seal != null)
        {
            return true;
        }

        if (!items.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)))
        {
            items.TryRequestItemDelivery(
                DurableToolItemRules.AdministrativeSeal,
                1,
                office.centerPos,
                destinationId,
                out _,
                out _);
        }

        failure = new DomainFailure(
            FailureCode.ServiceFeatureMissing,
            DurableToolItemRules.AdministrativeSeal);
        return false;
    }

    private V20CampaignRuntime CreateCampaignCandidate()
    {
        V20CampaignRuntime candidate = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        candidate.PublishSeasonal(candidate.PrepareSeasonal(live.CaptureSeasonal()));
        candidate.PublishSociety(candidate.PrepareSociety(live.CaptureSociety()));
        candidate.PublishFactions(candidate.PrepareFactions(live.CaptureFactions()));
        candidate.PublishMilestones(candidate.PrepareMilestones(live.CaptureMilestones()));
        return candidate;
    }

    private bool TryResolveCandidate(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request,
        out IReadOnlyList<V20ResolvedEventResult> resolved,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        resolved = Array.Empty<V20ResolvedEventResult>();
        bool succeeded;
        string rawFailure;
        V20ResolvedEventResult one;
        switch (request.Kind)
        {
            case ContentResolutionRequestKind.DailyEvaluation:
                if (request.DailyContext == null)
                {
                    failure = new DomainFailure(FailureCode.OperatingDayNotStarted);
                    return false;
                }
                resolved = candidate.EvaluateDaily(request.DailyContext);
                return true;
            case ContentResolutionRequestKind.SocietyChoice:
                succeeded = candidate.TryResolveSocietyEvent(
                    request.InstanceId,
                    request.ChoiceId,
                    request.Requirements,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.FactionChapterChoice:
                succeeded = candidate.TryResolveChapter(
                    request.FactionId,
                    request.ChoiceId,
                    request.Requirements,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.FactionContractAccept:
                succeeded = candidate.TryAcceptContract(
                    request.FactionId,
                    request.ContractId,
                    request.AbsoluteDay,
                    out rawFailure);
                one = default;
                break;
            case ContentResolutionRequestKind.FactionContractOutcome:
                succeeded = candidate.TryResolveContract(
                    request.FactionId,
                    request.ContractSucceeded,
                    request.Requirements,
                    out one,
                    out rawFailure);
                break;
            case ContentResolutionRequestKind.CulturalPractice:
            case ContentResolutionRequestKind.CulturalPracticeNeglect:
                CulturalPracticeDefinitionSO practice =
                    narrativeCatalog.Practices.FirstOrDefault(value =>
                        string.Equals(
                            value.StableId,
                            request.DefinitionId?.Trim(),
                            StringComparison.Ordinal));
                if (practice == null)
                {
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable);
                    return false;
                }
                bool neglected = request.Kind
                    == ContentResolutionRequestKind.CulturalPracticeNeglect;
                IReadOnlyList<V20ContentEffect> practiceEffects = neglected
                    ? practice.neglectedEffects
                        .Where(value => value != null)
                        .ToArray()
                    : WithConsumedRequirementItems(
                        practice.successEffects,
                        practice.requirements);
                candidate.ApplyResolvedEffects(practiceEffects);
                resolved = new[]
                {
                    new V20ResolvedEventResult(
                        practice.StableId,
                        neglected ? "neglected" : "performed",
                        practiceEffects,
                        request.ParticipantCharacterIds)
                };
                return true;
            default:
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
        }
        if (!succeeded)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        resolved = request.Kind == ContentResolutionRequestKind.FactionContractAccept
            ? Array.Empty<V20ResolvedEventResult>()
            : new[] { one };
        return true;
    }

    private IReadOnlyList<V20ResolvedEventResult>
        ExpandAmbitionCompletionRewards(
            V20CampaignRuntime candidate,
            IReadOnlyList<V20ResolvedEventResult> resolved)
    {
        List<V20ResolvedEventResult> expanded = (resolved
                ?? Array.Empty<V20ResolvedEventResult>())
            .ToList();
        foreach (V20ResolvedEventResult resolution in
                 expanded.ToArray())
        {
            int progress = resolution.Effects
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.AmbitionProgress)
                .Sum(value => Math.Max(0, Mathf.RoundToInt(value.amount)));
            if (progress <= 0)
            {
                continue;
            }
            foreach (string participantId in
                     resolution.ParticipantCharacterIds
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
            {
                if (!narrativeQuery.TryPreviewAmbitionProgress(
                        new CharacterId(participantId),
                        progress,
                        out AmbitionProgressPreview preview)
                    || !preview.Completes)
                {
                    continue;
                }
                V20ContentEffect[] rewards = preview.CompletionRewards
                    .Where(value => value != null && value.IsValid)
                    .ToArray();
                if (rewards.Length == 0)
                {
                    continue;
                }
                candidate.ApplyResolvedEffects(
                    rewards,
                    resolution.ContextFactionId);
                expanded.Add(new V20ResolvedEventResult(
                    $"ambition-reward:{preview.AmbitionId.Value}",
                    "completed",
                    rewards,
                    resolution.ParticipantCharacterIds,
                    resolution.ContextFactionId));
            }
        }
        return expanded.AsReadOnly();
    }

    private bool TryResolveRequestRequirements(
        ContentResolutionRequest request,
        RunMilestoneEvaluationSnapshot world,
        out V20ContentRequirementSet resolved,
        out IReadOnlyList<string> participants,
        out DomainFailure failure)
    {
        resolved = new V20ContentRequirementSet();
        participants = Array.Empty<string>();
        failure = DomainFailure.None;
        if (world == null)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (request.Kind == ContentResolutionRequestKind.DailyEvaluation
            || request.Kind == ContentResolutionRequestKind.FactionContractAccept)
        {
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.CulturalPractice
            || request.Kind == ContentResolutionRequestKind.CulturalPracticeNeglect)
        {
            CulturalPracticeDefinitionSO practice =
                narrativeCatalog.Practices.FirstOrDefault(value =>
                    string.Equals(
                        value.StableId,
                        request.DefinitionId?.Trim(),
                        StringComparison.Ordinal));
            if (practice == null)
            {
                return false;
            }
            participants = (request.ParticipantCharacterIds
                    ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (participants.Count == 0)
            {
                return false;
            }
            foreach (string participant in participants)
            {
                CharacterId characterId = new(participant);
                if (!narrativeQuery.TryGet(characterId, out _)
                    || !narrativeQuery.CanPerformPractice(
                            characterId,
                            practice.StableId,
                            request.AbsoluteDay,
                            out _))
                {
                    return false;
                }
            }
            resolved = request.Kind == ContentResolutionRequestKind.CulturalPractice
                ? practice.requirements
                : new V20ContentRequirementSet();
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.SocietyChoice)
        {
            V20ActiveEventSaveData active = live.ActiveSocietyEvents.FirstOrDefault(value =>
                string.Equals(value.instanceId, request.InstanceId?.Trim(), StringComparison.Ordinal));
            if (active == null) return false;
            participants = active.participantCharacterIds.AsReadOnly();
            V20AuthoredContentSO definition = ((ISocietyEventCatalog)catalog)
                .Require(active.definitionId);
            if (definition is GuestRequestDefinitionSO guest)
            {
                if (string.Equals(
                        request.ChoiceId,
                        "fulfill",
                        StringComparison.Ordinal)
                    && guest.kind == GuestRequestKind.Trade
                    && facilities.FindOperational(
                        ResearchFacilityCommandKind.SecureTradeVault).Count == 0)
                {
                    failure = new DomainFailure(
                        FailureCode.ServiceFeatureMissing,
                        "facility:secure-trade-vault");
                    return false;
                }
                resolved = string.Equals(request.ChoiceId, "fulfill", StringComparison.Ordinal)
                    ? guest.serviceRequirements
                    : new V20ContentRequirementSet();
                return true;
            }
            V20ChoiceDefinition choice = Choices(definition).FirstOrDefault(value =>
                string.Equals(value.choiceId, request.ChoiceId?.Trim(), StringComparison.Ordinal));
            if (choice == null) return false;
            resolved = choice.requirements;
            return true;
        }
        if (!live.TryGetFaction(request.FactionId, out FactionCampaignStateSaveData faction))
            return false;
        if (request.Kind == ContentResolutionRequestKind.FactionChapterChoice)
        {
            FactionChapterDefinitionSO chapter = catalog.Chapters.Single(value =>
                string.Equals(value.factionId, faction.factionId, StringComparison.Ordinal)
                && value.chapterNumber == faction.currentChapter);
            V20ChoiceDefinition choice = chapter.choices.FirstOrDefault(value =>
                string.Equals(value.choiceId, request.ChoiceId?.Trim(), StringComparison.Ordinal));
            if (choice == null) return false;
            resolved = MergeRequirements(chapter.triggerRequirements, choice.requirements);
            return true;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, faction.activeContractId, StringComparison.Ordinal));
        if (contract == null) return false;
        resolved = request.ContractSucceeded
            ? contract.completionRequirements
            : new V20ContentRequirementSet();
        return true;
    }

    private V20ContentRequirementSet RequirementsForResolvedEffect(
        V20ResolvedEventResult result)
    {
        if (result.ResolutionId == "started")
        {
            SeasonalWorldEventDefinitionSO seasonal = catalog.SeasonalEvents
                .FirstOrDefault(value => string.Equals(
                    value.StableId,
                    result.DefinitionId,
                    StringComparison.Ordinal));
            return seasonal?.triggerRequirements ?? new V20ContentRequirementSet();
        }
        if (result.ResolutionId == "automatic")
        {
            LifeEventDefinitionSO lifeEvent = catalog.LifeEvents.FirstOrDefault(value =>
                string.Equals(value.StableId, result.DefinitionId, StringComparison.Ordinal));
            return lifeEvent?.triggerRequirements ?? new V20ContentRequirementSet();
        }
        return new V20ContentRequirementSet();
    }

    private bool TryPreflightEffects(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out EffectCommitPlan plan,
        out DomainFailure failure)
    {
        plan = new EffectCommitPlan(request.ActionId);
        failure = DomainFailure.None;
        V20ContentEffect[] effects = resolutions.SelectMany(value => value.Effects)
            .Where(value => value != null && value.IsValid)
            .ToArray();
        plan.MoneyDelta = effects
            .Where(value => value.kind == V20ContentEffectKind.Money)
            .Sum(value => Mathf.RoundToInt(value.amount));
        if (plan.MoneyDelta < 0 && !money.CanSpend(-plan.MoneyDelta))
        {
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                (-plan.MoneyDelta).ToString(),
                money.Balance.ToString());
            return false;
        }
        if (!V21ContentEffectCommitPreflight.TryPlanItemCosts(
                effects,
                stock.GetAllStacks(),
                out IReadOnlyList<ReservedItemConsumption> itemCosts,
                out string missingItemId))
        {
            failure = new DomainFailure(
                FailureCode.ProductionMaterialsMissing,
                missingItemId);
            return false;
        }
        plan.ItemCosts.AddRange(itemCosts);
        if (plan.ItemCosts.Count > 0
            && !reservations.TryReserveQuantities(
                plan.ItemCosts,
                plan.ReservationOwnerId,
                ItemReservationPurpose.DirectPlayerOrder,
                $"content-resolution:{plan.ReservationOwnerId}:costs"))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }

        plan.Grants.AddRange(effects.Where(value =>
            value.kind == V20ContentEffectKind.ItemGrant
            && Mathf.RoundToInt(value.amount) > 0));
        Vector2Int grantDropoff = default;
        if (plan.Grants.Count > 0
            && (!dropZones.TryGetDeliveryDropoff(out grantDropoff)
                || plan.Grants.Any(value => !items.CatalogProvider.TryGetDefinition(
                    value.targetId,
                    out _))))
        {
            plan.Release(reservations);
            failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
            return false;
        }
        plan.Dropoff = grantDropoff;

        Dictionary<string, CharacterActor> actors = characters.Characters
            .Where(value => value != null && value.Identity != null)
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (V20ResolvedEventResult resolution in resolutions)
        {
            string[] targets = resolution.ParticipantCharacterIds
                .Where(actors.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (V20ContentEffect effect in resolution.Effects.Where(value =>
                         value != null && value.IsValid))
            {
                if (RequiresCharacter(effect.kind) && targets.Length == 0)
                {
                    plan.Release(reservations);
                    failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
                    return false;
                }
                if (effect.kind == V20ContentEffectKind.Relationship && targets.Length < 2)
                {
                    plan.Release(reservations);
                    failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                    return false;
                }
                if (effect.kind == V20ContentEffectKind.DiseaseExposure
                    && diseases.Definitions.All(value => !string.Equals(
                        value.Id,
                        effect.targetId,
                        StringComparison.Ordinal)))
                {
                    plan.Release(reservations);
                    failure = new DomainFailure(FailureCode.VaccineDefinitionMissing);
                    return false;
                }
            }
        }
        return true;
    }

    private bool TryCommitEffects(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        EffectCommitPlan plan,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TrySpawnGrants(plan, out failure))
        {
            plan.Release(reservations);
            return false;
        }
        EconomyTransactionContext transaction = new(
            plan.MoneyDelta >= 0
                ? EconomyTransactionKind.ContractIncome
                : EconomyTransactionKind.LegacyExpense,
            $"content:{request.ActionId}",
            description: request.Kind.ToString());
        if (plan.MoneyDelta > 0) money.Add(plan.MoneyDelta, transaction);
        else if (plan.MoneyDelta < 0
            && !money.TrySpend(-plan.MoneyDelta, transaction, out _))
        {
            RollbackGrants(plan);
            plan.Release(reservations);
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                (-plan.MoneyDelta).ToString(),
                money.Balance.ToString());
            return false;
        }
        if (!atomicItems.TryConsumeReserved(
                plan.ItemCosts,
                plan.ReservationOwnerId,
                out failure))
        {
            if (plan.MoneyDelta > 0)
            {
                money.TrySpend(plan.MoneyDelta, transaction, out _);
            }
            else if (plan.MoneyDelta < 0)
            {
                money.Add(-plan.MoneyDelta, transaction);
            }
            RollbackGrants(plan);
            plan.Release(reservations);
            return false;
        }
        ApplyTypedDomainEffects(request.AbsoluteDay, resolutions);
        if (plan.Grants.Count > 0)
        {
            transfers.ReleaseDestination(plan.GrantDestinationId, plan.Dropoff);
        }
        return true;
    }

    private bool TrySpawnGrants(
        EffectCommitPlan plan,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        foreach (V20ContentEffect grant in plan.Grants)
        {
            int amount = Math.Max(0, Mathf.RoundToInt(grant.amount));
            if (!transfers.TrySpawnItem(
                    grant.targetId,
                    amount,
                    plan.Dropoff,
                    WorldItemStackState.FacilityBuffer,
                    plan.GrantDestinationId,
                    out int spawned)
                || spawned != amount)
            {
                RollbackGrants(plan);
                failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
                return false;
            }
        }
        return true;
    }

    private void RollbackGrants(EffectCommitPlan plan)
    {
        if (plan.Grants.Count == 0) return;
        transfers.RemoveDestination(
            plan.GrantDestinationId,
            WorldItemStackState.FacilityBuffer);
    }

    private void ApplyTypedDomainEffects(
        int requestedAbsoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        int absoluteDay = Math.Max(1, requestedAbsoluteDay);
        Dictionary<string, CharacterActor> actors = characters.Characters
            .Where(value => value != null && value.Identity != null)
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (V20ResolvedEventResult resolution in resolutions)
        {
            CharacterActor[] participants = resolution.ParticipantCharacterIds
                .Where(actors.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(value => actors[value])
                .ToArray();
            foreach (V20ContentEffect effect in resolution.Effects
                         .Where(value => value != null && value.IsValid))
            {
                switch (effect.kind)
                {
                    case V20ContentEffectKind.Mood:
                        foreach (CharacterActor actor in participants)
                            actor.ApplyMoodFactor(
                                $"content:{resolution.DefinitionId}:{effect.targetId}",
                                effect.targetId,
                                effect.amount,
                                Math.Max(1, effect.durationDays) * 180f);
                        break;
                    case V20ContentEffectKind.Trauma:
                        foreach (CharacterActor actor in participants)
                            grief.ApplyTraumaDelta(
                                actor.Identity.TypedPersistentId,
                                $"content:{resolution.DefinitionId}:{effect.targetId}",
                                absoluteDay,
                                effect.amount);
                        break;
                    case V20ContentEffectKind.SkillExperience:
                        foreach (CharacterActor actor in participants)
                            actor.Progression?.AddExperience(Math.Max(0, Mathf.RoundToInt(effect.amount)));
                        break;
                    case V20ContentEffectKind.Health:
                        foreach (CharacterActor actor in participants)
                        {
                            if (effect.amount >= 0f)
                                bodyHealth.HealLegacyVitals(actor, effect.amount);
                            else
                                bodyHealth.ApplyLegacyDamage(
                                    actor,
                                    -effect.amount,
                                    resolution.DefinitionId,
                                    allowDeath: false);
                        }
                        break;
                    case V20ContentEffectKind.Relationship:
                        float sentiment = Mathf.Clamp(effect.amount / 10f, -1f, 1f);
                        for (int index = 0; index < participants.Length; index++)
                        for (int other = index + 1; other < participants.Length; other++)
                        {
                            participants[index].SocialMemory.RememberCharacterExperience(
                                participants[other], sentiment, resolution.DefinitionId);
                            participants[other].SocialMemory.RememberCharacterExperience(
                                participants[index], sentiment, resolution.DefinitionId);
                        }
                        break;
                    case V20ContentEffectKind.DiseaseExposure:
                        populationHealth.RecordExposure(
                            effect.targetId,
                            participants.Select(value => new PopulationExposureTarget(
                                value.Identity.TypedPersistentId,
                                1f)).ToArray(),
                            Math.Max(0.1f, Math.Abs(effect.amount)),
                            1f);
                        break;
                    case V20ContentEffectKind.AmbitionProgress:
                        foreach (CharacterActor actor in participants)
                        {
                            int amount = Mathf.RoundToInt(effect.amount);
                            if (narrativeQuery.TryPreviewAmbitionProgress(
                                    actor.Identity.TypedPersistentId,
                                    amount,
                                    out _))
                            {
                                narrative.AddAmbitionProgress(
                                    actor.Identity.TypedPersistentId,
                                    amount,
                                    absoluteDay);
                            }
                        }
                        break;
                }
            }
            if (resolution.DefinitionId.StartsWith("life-event:", StringComparison.Ordinal))
            {
                foreach (CharacterActor actor in participants)
                    narrative.RecordResolvedEvent(
                        actor.Identity.TypedPersistentId,
                        new NarrativeEventId(resolution.DefinitionId),
                        resolution.ResolutionId,
                        absoluteDay);
            }
            if (resolution.DefinitionId.StartsWith(
                    "practice:",
                    StringComparison.Ordinal))
            {
                CulturalPracticeDefinitionSO practice =
                    narrativeCatalog.Practices.Single(value => string.Equals(
                        value.StableId,
                        resolution.DefinitionId,
                        StringComparison.Ordinal));
                SpeciesCultureId practiceCultureId =
                    new(practice.cultureId);
                int assimilationDays = narrativeCatalog
                    .Require(practiceCultureId)
                    .assimilationDays;
                foreach (CharacterActor actor in participants)
                {
                    if (string.Equals(
                            resolution.ResolutionId,
                            "performed",
                            StringComparison.Ordinal))
                    {
                        narrative.RecordPracticeParticipation(
                            actor.Identity.TypedPersistentId,
                            practice.StableId,
                            practiceCultureId,
                            assimilationDays,
                            absoluteDay);
                    }
                    else
                    {
                        narrative.RecordPracticeNeglect(
                            actor.Identity.TypedPersistentId,
                            practice.StableId,
                            absoluteDay);
                    }
                }
            }
        }
    }

    private static bool RequiresCharacter(V20ContentEffectKind kind) =>
        kind is V20ContentEffectKind.Mood
            or V20ContentEffectKind.Trauma
            or V20ContentEffectKind.SkillExperience
            or V20ContentEffectKind.Health
            or V20ContentEffectKind.Relationship
            or V20ContentEffectKind.DiseaseExposure
            or V20ContentEffectKind.AmbitionProgress;

    private static IReadOnlyList<V20ContentEffect> WithConsumedRequirementItems(
        IEnumerable<V20ContentEffect> effects,
        V20ContentRequirementSet requirements)
    {
        List<V20ContentEffect> result = (effects
                ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .ToList();
        foreach (IGrouping<string, V20ItemAmountRequirement> group in
                 (requirements?.items
                     ?? new List<V20ItemAmountRequirement>())
                 .Where(value => value != null
                     && value.consume
                     && !string.IsNullOrWhiteSpace(
                         value.itemDefinitionId))
                 .GroupBy(
                     value => value.itemDefinitionId.Trim(),
                     StringComparer.Ordinal))
        {
            result.Add(new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = group.Key,
                amount = group.Sum(value => Math.Max(0, value.amount))
            });
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<V20ChoiceDefinition> Choices(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.choices,
            ServiceIncidentDefinitionSO value => value.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };

    private static V20ContentRequirementSet MergeRequirements(
        V20ContentRequirementSet first,
        V20ContentRequirementSet second) => new()
        {
            items = (first?.items ?? new()).Concat(second?.items ?? new()).ToList(),
            facilities = (first?.facilities ?? new()).Concat(second?.facilities ?? new()).ToList(),
            research = (first?.research ?? new()).Concat(second?.research ?? new()).ToList(),
            characters = (first?.characters ?? new()).Concat(second?.characters ?? new()).ToList(),
            factions = (first?.factions ?? new()).Concat(second?.factions ?? new()).ToList(),
            worldMetrics = (first?.worldMetrics ?? new()).Concat(second?.worldMetrics ?? new()).ToList(),
            requiredFlags = (first?.requiredFlags ?? new()).Concat(second?.requiredFlags ?? new()).ToList(),
            excludedFlags = (first?.excludedFlags ?? new()).Concat(second?.excludedFlags ?? new()).ToList()
        };

    private sealed class EffectCommitPlan
    {
        public EffectCommitPlan(string actionId)
        {
            string normalized = actionId?.Trim() ?? string.Empty;
            ReservationOwnerId = "content-resolution:" + normalized;
            GrantDestinationId = ReservationOwnerId + ":grants";
        }

        public string ReservationOwnerId { get; }
        public string GrantDestinationId { get; }
        public int MoneyDelta { get; set; }
        public Vector2Int Dropoff { get; set; }
        public List<ReservedItemConsumption> ItemCosts { get; } = new();
        public List<V20ContentEffect> Grants { get; } = new();

        public void Release(IItemReservationService service)
        {
            foreach (ReservedItemConsumption cost in ItemCosts)
                service.Release(cost.StackId, ReservationOwnerId);
        }
    }
}
