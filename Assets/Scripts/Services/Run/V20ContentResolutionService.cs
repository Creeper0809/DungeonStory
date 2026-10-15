using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public enum ContentResolutionRequestKind
{
    DailyEvaluation,
    SocietyChoice,
    FactionChapterChoice,
    FactionContractAccept,
    SeasonalFactionContractAccept,
    FactionContractOutcome,
    CulturalPractice,
    CulturalPracticeNeglect,
    GuestRequestDeliveryOutcome
}

public enum ContentResolutionDisposition
{
    Terminal = 0,
    AcceptedPending = 1
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
    public ContentResolutionDisposition Disposition { get; internal set; }
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
    Medical,
    IsolationRecovery
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
        bool requiresIsolationRecovery = capability
            == FacilityCapabilityKind.IsolationRecovery;
        FacilityRole role = requiresIsolationRecovery
            ? FacilityRole.Medical
            : ToRole(capability);
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
                && (!requiresIsolationRecovery
                    || value.BuildingData.Abilities
                        .OfType<ISurgicalFacilityAbility>()
                        .Any(ability =>
                            (ability.FacilityTags
                                & SurgeryFacilityTag.IsolationRecovery)
                            == SurgeryFacilityTag.IsolationRecovery))
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
        if (building?.BuildingData == null)
        {
            return false;
        }

        if (!building.BuildingData.RequiresRoomRole())
        {
            return true;
        }

        BuildingRoomOperationalSnapshot profile =
            building.GetRoomOperationalProfile();
        return profile?.IsUsableRoom == true;
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

    bool IsLivingCharacterAtStage(
        CharacterId characterId,
        CharacterLifeStage lifeStage);
}

public sealed class ContentRequirementEvaluator : IContentRequirementEvaluator
{
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifeQuery life;
    private readonly IKinshipQuery kinship;
    private readonly ICharacterNarrativeQuery narrative;
    private readonly IFacilityCapabilityQuery facilities;

    public ContentRequirementEvaluator(
        ICharacterWorldQuery characters,
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        ICharacterNarrativeQuery narrative,
        IFacilityCapabilityQuery facilities)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
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

    public bool IsLivingCharacterAtStage(
        CharacterId characterId,
        CharacterLifeStage lifeStage)
    {
        if (!characterId.IsValid
            || kinship.TryGetTombstone(characterId, out _)
            || !life.TryGet(characterId, out CharacterLifeRecord record)
            || record.LifeStage != lifeStage)
        {
            return false;
        }

        CharacterActor present = characters.Characters.FirstOrDefault(actor =>
            actor?.Identity != null
            && string.Equals(
                actor.Identity.PersistentId,
                characterId.Value,
                StringComparison.Ordinal));
        return present == null
            || (!present.IsDead
                && present.CurrentHealth > 0f
                && present.CurrentLifecycleState !=
                    CharacterLifecycleState.Despawned);
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

public interface IRetirementScheduleContentQuery
{
    bool CanScheduleRetirement(CharacterId characterId);
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
            [V20ContentEffectKind.MilestonePressure] = "milestone pressure command / run.milestones",
            [V20ContentEffectKind.RetirementSchedule] =
                "career retirement schedule / characters.career"
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

    public static string SeasonalFactionContractAccept(
        string occurrenceId,
        string contractId) =>
        Join("seasonal-faction-contract-accept", occurrenceId, contractId);

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

    public static string Festival(string festivalId, int occurrenceYear) =>
        Join("festival", festivalId, $"resolve,{Math.Max(1, occurrenceYear)}");

    public static string FestivalSkip(string festivalId, int occurrenceYear) =>
        Join("festival", festivalId, $"skip,{Math.Max(1, occurrenceYear)}");

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
    IEventAlertChoiceActionDispatcher,
    IEventAlertChoiceActionDispositionDispatcher
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

    public bool TryDispatch(string actionId, out DomainFailure failure) =>
        TryDispatch(actionId, out _, out failure);

    public bool TryDispatch(
        string actionId,
        out EventAlertChoiceActionDisposition disposition,
        out DomainFailure failure)
    {
        disposition = EventAlertChoiceActionDisposition.Terminal;
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
            string[] details = second.Split(',');
            string festivalAction = details[0];
            int occurrenceYear = calendar.Year;
            bool occurrenceBound = details.Length == 2;
            if (details.Length == 2
                && (!int.TryParse(details[1], out occurrenceYear)
                    || occurrenceYear <= 0))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            if (details.Length > 2)
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            if (string.Equals(festivalAction, "skip", StringComparison.Ordinal))
            {
                return festivals.Schedule(
                    new FestivalScheduleRequest
                    {
                        ActionId = actionId.Trim(),
                        FestivalId = first,
                        OccurrenceYear = occurrenceYear,
                        Decline = true
                    },
                    out _,
                    out failure);
            }
            if (!string.Equals(festivalAction, "resolve", StringComparison.Ordinal))
            {
                failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
                return false;
            }
            FestivalScheduleRequest schedule = new()
            {
                ActionId = actionId.Trim(),
                FestivalId = first,
                OccurrenceYear = occurrenceYear,
                ParticipantIds = world.LivingCharacters
                    .Select(CharacterPersistentIdentity.Require)
                    .OrderBy(value => value.Value, StringComparer.Ordinal)
                    .ToArray()
            };
            if (!festivals.Schedule(
                    schedule,
                    out FestivalPreparedOrder order,
                    out failure))
                return false;
            if (!occurrenceBound)
                return festivals.Resolve(order, out failure);
            disposition = EventAlertChoiceActionDisposition.AcceptedPending;
            return true;
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
            case "seasonal-faction-contract-accept":
                request.Kind =
                    ContentResolutionRequestKind.SeasonalFactionContractAccept;
                request.InstanceId = first;
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

        disposition = result.Disposition ==
                ContentResolutionDisposition.AcceptedPending
            ? EventAlertChoiceActionDisposition.AcceptedPending
            : EventAlertChoiceActionDisposition.Terminal;

        foreach (V20ResolvedEventResult resolved in result.Resolutions)
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                resolved,
                physicalEffectsApplied: true));
        }
        return true;
    }
}

public sealed class V20ContentResolutionService :
    IContentResolutionService,
    IObservedCareerLifeEventCommand,
    IRetirementScheduleContentQuery,
    IFactionContractDeliveryQuery,
    IFactionContractDeliveryResolutionCommand,
    IDungeonSaveCaptureGuard
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly IContentRequirementEvaluator requirements;
    private readonly ICharacterWorldQuery characters;
    private readonly ICareerPersistence careers;
    private readonly ICareerService careerQuery;
    private readonly IGameCalendar calendar;
    private readonly IV20MilestoneWorldSnapshotQuery milestoneWorld;
    private readonly ICharacterNarrativeQuery narrativeQuery;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly ICharacterNarrativeCommand narrative;
    private readonly IGriefTraumaService grief;
    private readonly ICharacterBodyHealthCommand bodyHealth;
    private readonly IPopulationHealthService populationHealth;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IGameMoneyAccount money;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IPhysicalFacilityItemBatchTransferGateway
        factionContractTransfers;
    private readonly IEconomyProjectInputOwnerPort factionContractInputOwners;
    private readonly RunAdministrativeSealDurableEquipmentRuntime
        administrativeSealEquipment;
    private readonly ISocialRitualFactPublisher factPublisher;

    private sealed class CharacterEffectOwnerSnapshot
    {
        internal CharacterEffectOwnerSnapshot(CharacterActor actor)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Stats = actor.Stats?.CaptureMoodDeliveryTransactionState();
            Progression = actor.Progression?.CapturePersistentState();
            Social = actor.SocialMemory?.CaptureSnapshot();
        }

        internal CharacterActor Actor { get; }
        internal CharacterMoodDeliveryTransactionSnapshot Stats { get; }
        internal CharacterProgressionSnapshot Progression { get; }
        internal CharacterSocialMemorySnapshot Social { get; }
    }

    private sealed class EffectOwnerSnapshot
    {
        internal SeasonalEventWorldSaveData Seasonal;
        internal SocietyEventWorldSaveData Society;
        internal FactionCampaignWorldSaveData Factions;
        internal RunMilestoneWorldSaveData Milestones;
        internal DungeonPhysicalItemSaveData Items;
        internal int MoneyBalance;
        internal CharacterCareerWorldSaveData Careers;
        internal DungeonCharacterBodyHealthSaveData BodyHealth;
        internal CharacterPsychosocialWorldSaveData Psychosocial;
        internal PopulationHealthWorldSaveData PopulationHealth;
        internal CharacterNarrativeWorldSaveData Narrative;
        internal CharacterEffectOwnerSnapshot[] Characters =
            Array.Empty<CharacterEffectOwnerSnapshot>();
    }

    private sealed class EffectCommitReferences
    {
        internal static EffectCommitReferences From(
            V20ContentResolutionRecoverySaveData recovery) => new()
        {
            MoneyTransactionId = recovery?.moneyTransactionId ?? string.Empty,
            ItemConsumptionOperationId = recovery?.itemConsumptionOperationId
                ?? string.Empty,
            ItemGrantBatchCommitId = recovery?.itemGrantBatchCommitId
                ?? string.Empty,
            DurableEquipmentStackId = recovery?.durableEquipmentStackId
                ?? string.Empty,
            DurableEquipmentItemId = recovery?.durableEquipmentItemId
                ?? string.Empty,
            DurableEquipmentContext = recovery?.durableEquipmentContext
                ?? string.Empty
        };

        internal string MoneyTransactionId { get; set; } = string.Empty;
        internal string ItemConsumptionOperationId { get; set; } = string.Empty;
        internal string ItemGrantBatchCommitId { get; set; } = string.Empty;
        internal string DurableEquipmentStackId { get; private set; } =
            string.Empty;
        internal string DurableEquipmentItemId { get; private set; } =
            string.Empty;
        internal string DurableEquipmentContext { get; private set; } =
            string.Empty;

        internal void SetDurableEquipment(
            DurableFacilityEquipmentUseEvidence equipment)
        {
            if (equipment == null)
                return;
            DurableEquipmentStackId = equipment.StackId;
            DurableEquipmentItemId = equipment.ItemId;
            DurableEquipmentContext = equipment.ToCanonicalString();
        }

        internal string ChildCommitId(V20ContentEffectKind kind) => kind switch
        {
            V20ContentEffectKind.Money => MoneyTransactionId,
            V20ContentEffectKind.ItemConsume => ItemConsumptionOperationId,
            V20ContentEffectKind.ItemGrant => ItemGrantBatchCommitId,
            _ => string.Empty
        };
    }

    public V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        ICharacterNarrativeCatalog narrativeCatalog,
        IContentRequirementEvaluator requirements,
        ICharacterWorldQuery characters,
        ICareerPersistence careers,
        ICareerService careerQuery,
        IGameCalendar calendar,
        IV20MilestoneWorldSnapshotQuery milestoneWorld,
        ICharacterNarrativeQuery narrativeQuery,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterNarrativeCommand narrative,
        IGriefTraumaService grief,
        ICharacterBodyHealthCommand bodyHealth,
        IPopulationHealthService populationHealth,
        IDiseaseDefinitionCatalog diseases,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems,
        IWorldItemStackRuntime items,
        IPhysicalItemExactSourcePublicationService exactSources,
        IWorldDropZoneQuery dropZones,
        IGameMoneyAccount money,
        IFacilityCapabilityQuery facilities,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IPhysicalFacilityItemBatchTransferGateway factionContractTransfers,
        IEconomyProjectInputOwnerPort factionContractInputOwners,
        RunAdministrativeSealDurableEquipmentRuntime administrativeSealEquipment,
        ISocialRitualFactPublisher factPublisher)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.careerQuery = careerQuery
            ?? throw new ArgumentNullException(nameof(careerQuery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.milestoneWorld = milestoneWorld
            ?? throw new ArgumentNullException(nameof(milestoneWorld));
        this.narrativeQuery = narrativeQuery
            ?? throw new ArgumentNullException(nameof(narrativeQuery));
        this.proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.exactSources = exactSources
            ?? throw new ArgumentNullException(nameof(exactSources));
        this.dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.physicalDispositions = physicalDispositions
            ?? throw new ArgumentNullException(nameof(physicalDispositions));
        this.factionContractTransfers = factionContractTransfers
            ?? throw new ArgumentNullException(nameof(factionContractTransfers));
        this.factionContractInputOwners = factionContractInputOwners
            ?? throw new ArgumentNullException(nameof(factionContractInputOwners));
        this.administrativeSealEquipment = administrativeSealEquipment
            ?? throw new ArgumentNullException(nameof(administrativeSealEquipment));
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
    }

    internal V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        IContentRequirementEvaluator requirements,
        ICharacterWorldQuery characters,
        IStockQuery stock,
        IAtomicItemConsumptionService atomicItems,
        ISocialRitualFactPublisher factPublisher,
        ICharacterNarrativeCommand narrative = null,
        IPhysicalItemExactSourcePublicationService exactSources = null,
        ICharacterNarrativeCatalog narrativeCatalog = null,
        IItemReservationService reservations = null,
        IWorldItemStackRuntime items = null,
        IWorldDropZoneQuery dropZones = null,
        IGameMoneyAccount money = null,
        ICharacterBodyHealthCommand bodyHealth = null,
        ICareerPersistence careers = null,
        ICareerService careerQuery = null,
        IPopulationHealthService populationHealth = null,
        IDiseaseDefinitionCatalog diseases = null,
        IGriefTraumaService grief = null)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.narrativeCatalog = narrativeCatalog;
        this.requirements = requirements
            ?? throw new ArgumentNullException(nameof(requirements));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.careers = careers;
        this.careerQuery = careerQuery;
        calendar = null;
        milestoneWorld = null;
        narrativeQuery = narrative as ICharacterNarrativeQuery;
        proficiencyQuery = null;
        this.narrative = narrative;
        this.grief = grief;
        this.bodyHealth = bodyHealth;
        this.populationHealth = populationHealth;
        this.diseases = diseases;
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations;
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items;
        this.exactSources = exactSources;
        this.dropZones = dropZones;
        this.money = money;
        facilities = null;
        physicalDispositions = null;
        factionContractTransfers = null;
        factionContractInputOwners = null;
        administrativeSealEquipment = null;
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
    }

    private EffectOwnerSnapshot CaptureEffectOwners(
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        V20ResolvedEventResult[] concrete = (resolutions
                ?? Array.Empty<V20ResolvedEventResult>())
            .ToArray();
        HashSet<string> participantIds = concrete
            .SelectMany(value => value.ParticipantCharacterIds)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        CharacterEffectOwnerSnapshot[] actorSnapshots = characters.Characters
            .Where(actor => actor?.Identity != null
                && participantIds.Contains(actor.Identity.PersistentId))
            .GroupBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .Select(group => new CharacterEffectOwnerSnapshot(group.First()))
            .ToArray();

        bool HasEffect(V20ContentEffectKind kind) => concrete.Any(value =>
            value.Effects.Any(effect => effect?.kind == kind));
        ICharacterBodyHealthPersistence bodyPersistence =
            bodyHealth as ICharacterBodyHealthPersistence;
        IPsychosocialPersistence psychosocialPersistence =
            grief as IPsychosocialPersistence;
        IPopulationHealthPersistence populationPersistence =
            populationHealth as IPopulationHealthPersistence;
        ICharacterNarrativePersistence narrativePersistence =
            narrative as ICharacterNarrativePersistence;
        bool requiresNarrative = HasEffect(V20ContentEffectKind.AmbitionProgress)
            || concrete.Any(value =>
                value.DefinitionId.StartsWith(
                    "life-event:",
                    StringComparison.Ordinal)
                || value.DefinitionId.StartsWith(
                    "practice:",
                    StringComparison.Ordinal));
        if (HasEffect(V20ContentEffectKind.Health) && bodyPersistence == null
            || HasEffect(V20ContentEffectKind.Trauma)
                && psychosocialPersistence == null
            || HasEffect(V20ContentEffectKind.DiseaseExposure)
                && populationPersistence == null
            || requiresNarrative && narrativePersistence == null)
        {
            throw new InvalidOperationException(
                "V20 content effects require rollback-capable persistence owners.");
        }

        return new EffectOwnerSnapshot
        {
            Seasonal = live.CaptureSeasonal(),
            Society = live.CaptureSociety(),
            Factions = live.CaptureFactions(),
            Milestones = live.CaptureMilestones(),
            Items = items?.Capture(),
            MoneyBalance = money?.Balance ?? 0,
            Careers = careers?.Capture(),
            BodyHealth = bodyPersistence?.Capture(),
            Psychosocial = psychosocialPersistence?.Capture(),
            PopulationHealth = populationPersistence?.Capture(),
            Narrative = narrativePersistence?.Capture(),
            Characters = actorSnapshots
        };
    }

    private void RestoreEffectOwners(
        EffectOwnerSnapshot snapshot,
        string operationId)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Items != null)
            items.Restore(snapshot.Items);
        if (money != null)
        {
            money.SetBalance(
                snapshot.MoneyBalance,
                new EconomyTransactionContext(
                    EconomyTransactionKind.LegacyExpense,
                    "content-rollback:" + (operationId?.Trim() ?? string.Empty),
                    description: "v20-content-resolution-rollback"));
        }
        foreach (CharacterEffectOwnerSnapshot character in
                 snapshot.Characters)
        {
            if (character.Stats != null)
                character.Actor.Stats?.RestoreMoodDeliveryTransactionState(
                    character.Stats);
            if (character.Progression != null)
                character.Actor.Progression?.RestorePersistentState(
                    character.Progression);
            if (character.Social != null)
                character.Actor.SocialMemory?.RestoreSnapshot(character.Social);
        }
        if (snapshot.BodyHealth != null
            && bodyHealth is ICharacterBodyHealthPersistence bodyPersistence)
        {
            bodyPersistence.PublishRestore(
                bodyPersistence.PrepareRestore(snapshot.BodyHealth));
        }
        if (snapshot.Psychosocial != null
            && grief is IPsychosocialPersistence psychosocialPersistence)
        {
            psychosocialPersistence.PublishRestore(
                psychosocialPersistence.PrepareRestore(
                    snapshot.Psychosocial));
        }
        if (snapshot.PopulationHealth != null
            && populationHealth is IPopulationHealthPersistence
                populationPersistence)
        {
            populationPersistence.PublishRestore(
                populationPersistence.PrepareRestore(
                    snapshot.PopulationHealth));
        }
        if (snapshot.Narrative != null
            && narrative is ICharacterNarrativePersistence
                narrativePersistence)
        {
            narrativePersistence.PublishRestore(
                narrativePersistence.PrepareRestore(snapshot.Narrative));
        }
        if (snapshot.Careers != null && careers != null)
            careers.PublishRestore(careers.PrepareRestore(snapshot.Careers));
        live.PublishContentResolution(
            live.PrepareSeasonal(snapshot.Seasonal),
            live.PrepareSociety(snapshot.Society),
            live.PrepareFactions(snapshot.Factions),
            live.PrepareMilestones(snapshot.Milestones));
    }

    private void RestoreTypedEffectOwners(EffectOwnerSnapshot snapshot)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        foreach (CharacterEffectOwnerSnapshot character in snapshot.Characters)
        {
            if (character.Stats != null)
                character.Actor.Stats?.RestoreMoodDeliveryTransactionState(
                    character.Stats);
            if (character.Progression != null)
                character.Actor.Progression?.RestorePersistentState(
                    character.Progression);
            if (character.Social != null)
                character.Actor.SocialMemory?.RestoreSnapshot(character.Social);
        }
        if (snapshot.BodyHealth != null
            && bodyHealth is ICharacterBodyHealthPersistence bodyPersistence)
        {
            bodyPersistence.PublishRestore(
                bodyPersistence.PrepareRestore(snapshot.BodyHealth));
        }
        if (snapshot.Psychosocial != null
            && grief is IPsychosocialPersistence psychosocialPersistence)
        {
            psychosocialPersistence.PublishRestore(
                psychosocialPersistence.PrepareRestore(snapshot.Psychosocial));
        }
        if (snapshot.PopulationHealth != null
            && populationHealth is IPopulationHealthPersistence populationPersistence)
        {
            populationPersistence.PublishRestore(
                populationPersistence.PrepareRestore(snapshot.PopulationHealth));
        }
        if (snapshot.Narrative != null
            && narrative is ICharacterNarrativePersistence narrativePersistence)
        {
            narrativePersistence.PublishRestore(
                narrativePersistence.PrepareRestore(snapshot.Narrative));
        }
        if (snapshot.Careers != null && careers != null)
            careers.PublishRestore(careers.PrepareRestore(snapshot.Careers));
    }

    public bool CanScheduleRetirement(CharacterId characterId)
    {
        if (!IsEligibleLivingElder(characterId)) return false;
        CharacterCareerAggregate candidate = careers.PrepareRestore(
            careers.Capture());
        return candidate.CanScheduleRetirement(characterId, out _);
    }

    internal void RequireCanCommitObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt)
    {
        V20CampaignRuntime candidate = CreateCampaignCandidate();
        ObservedLifeEventCommitResult observed =
            candidate.RecordObservedFuneralLifeEvent(receipt);
        if (!observed.StateChanged)
            throw new InvalidOperationException("Observed funeral was already committed.");
        RequireObservedLifeEventEffectsPreflight(
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            observed.Resolution.HasValue
                ? new[] { observed.Resolution.Value }
                : Array.Empty<V20ResolvedEventResult>());
    }

    internal IReadOnlyList<V20ResolvedEventResult>
        CommitObservedFuneralLifeEvent(
            ObservedFuneralLifeEventReceipt receipt,
            DomainFactLease sourceLease) =>
        CommitObservedLifeEvents(
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            candidate => new[]
            {
                candidate.RecordObservedFuneralLifeEvent(receipt)
            }, DomainFactEmission.Create(sourceLease, receipt));

    [GameplayInternalOnly(
        "Commits the source-owned apprentice-mistake occurrence.",
        "ProductionRecipeExecutionReceiptAuthority only")]
    bool IObservedCareerLifeEventCommand.TryCaptureProductionDeclaredLoss(
        ProductionDeclaredLossCycleReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            CharacterId studentId = new(source.WorkerPersistentId);
            if (!studentId.IsValid)
                throw new ArgumentException(
                    "Production-loss actor has no valid character identity.");
            CareerMentorshipSnapshot[] matches = careerQuery.Mentorships
                .Where(value => value.StudentCharacterId.Equals(studentId))
                .ToArray();
            if (matches.Length == 0)
                return true;
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"Production-loss student '{studentId.Value}' has duplicate active mentorships.");

            int absoluteDay = Math.Max(1, calendar.Day);
            ObservedProductionLossLifeEventReceipt receipt = new(
                source,
                matches[0],
                absoluteDay,
                ResolveGeneration(absoluteDay));
            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-production-loss-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate.RecordObservedProductionLossLifeEvent(
                        receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                    RestoreObservedSociety(societyBefore);
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-production-loss-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Commits the source-owned last-lesson occurrence.",
        "CareerApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureLastLesson(
        CareerMentorshipSnapshot mentorship,
        CharacterCareerSnapshot retirement,
        CombatEquipmentInstance protectiveEquipment,
        CareerMentorshipAwardCommitReceipt award,
        int absoluteDay,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            if (absoluteDay != Math.Max(1, calendar.Day)
                || !careerQuery.TryGet(
                    mentorship.MentorCharacterId,
                    out CharacterCareerSnapshot currentRetirement)
                || currentRetirement.Retired != retirement.Retired
                || currentRetirement.RetirementScheduleStatus
                    != retirement.RetirementScheduleStatus
                || !string.Equals(
                    currentRetirement.RetirementEventId,
                    retirement.RetirementEventId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    currentRetirement.RetirementChoiceId,
                    retirement.RetirementChoiceId,
                    StringComparison.Ordinal)
                || currentRetirement.RetirementDecisionAbsoluteDay
                    != retirement.RetirementDecisionAbsoluteDay
                || currentRetirement.RetirementDueAbsoluteDay
                    != retirement.RetirementDueAbsoluteDay
                || currentRetirement.RetirementTerminalAbsoluteDay
                    != retirement.RetirementTerminalAbsoluteDay
                || !careerQuery.Mentorships.Any(value =>
                    value.MentorCharacterId.Equals(
                        mentorship.MentorCharacterId)
                    && value.StudentCharacterId.Equals(
                        mentorship.StudentCharacterId)
                    && value.AcademyBuildingId.Equals(
                        mentorship.AcademyBuildingId)
                    && value.ProficiencyId.Equals(mentorship.ProficiencyId)))
            {
                throw new InvalidOperationException(
                    "Last-lesson source no longer matches the active mentorship day.");
            }

            ObservedLastLessonLifeEventReceipt receipt = new(
                mentorship,
                retirement,
                protectiveEquipment,
                award,
                absoluteDay,
                ResolveGeneration(absoluteDay));
            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-last-lesson-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate.RecordObservedLastLessonLifeEvent(
                        receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                    RestoreObservedSociety(societyBefore);
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-last-lesson-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Commits the source-owned quiet-promotion occurrence.",
        "ObservedProficiencyPromotionApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureQuietPromotion(
        CharacterProficiencyAwardCommitReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            ObservedLifeEventReceiptSaveData[] existing = live.CaptureSociety()
                .successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind
                        == ObservedLifeEventSourceKind.ProficiencyPromotion
                    && string.Equals(
                        value.sourceOperationId,
                        source.SourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (existing.Length > 1)
                throw new InvalidOperationException(
                    $"Quiet-promotion source '{source.SourceOperationId}' has duplicate receipts.");
            if (existing.Length == 1)
            {
                if (!ObservedQuietPromotionLifeEventReceipt.TryParse(
                        existing[0].canonicalPayload,
                        out ObservedQuietPromotionLifeEventReceipt committed)
                    || !string.Equals(
                        committed.SourceOperationId,
                        source.SourceOperationId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Quiet-promotion source '{source.SourceOperationId}' conflicts with its committed receipt.");
                }
                return true;
            }

            if (source.AbsoluteHour != Math.Max(0L, calendar.AbsoluteHour)
                || !proficiencyQuery.TryGetProficiency(
                    source.CharacterId,
                    source.ProficiencyId,
                    source.AbsoluteHour,
                    out CharacterProficiencySnapshot current)
                || current.CurrentMilliExperience
                    != source.AfterCurrentMilliExperience
                || current.LifetimeMilliExperience
                    != source.AfterLifetimeMilliExperience)
            {
                throw new InvalidOperationException(
                    "Quiet-promotion source no longer matches the committed proficiency state.");
            }

            int absoluteDay = Math.Max(1, calendar.Day);
            ObservedQuietPromotionLifeEventReceipt receipt = new(
                source,
                absoluteDay,
                ResolveGeneration(absoluteDay));
            LifeEventDefinitionSO definition = catalog.LifeEvents.Single(
                value => string.Equals(
                    value.StableId,
                    "life-event:quiet-promotion",
                    StringComparison.Ordinal));
            if (!definition.automatic
                || definition.automaticEffects == null
                || definition.automaticEffects.Count == 0
                || definition.automaticEffects.Any(value => value == null
                    || !value.IsValid
                    || value.kind != V20ContentEffectKind.Mood))
            {
                throw new InvalidOperationException(
                    "Quiet-promotion transactional effects must remain mood-only.");
            }

            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            CharacterActor owner = characters.Characters.SingleOrDefault(
                value => value != null
                    && value.Identity != null
                    && string.Equals(
                        value.Identity.PersistentId,
                        receipt.CharacterId.Value,
                        StringComparison.Ordinal));
            if (owner?.Stats == null)
            {
                failureReason =
                    "observed-quiet-promotion-owner-unavailable";
                return false;
            }
            CharacterMoodDeliveryTransactionSnapshot moodBefore =
                owner.Stats.CaptureMoodDeliveryTransactionState();
            ICharacterNarrativePersistence narrativePersistence =
                narrative as ICharacterNarrativePersistence;
            if (narrativePersistence == null)
            {
                failureReason =
                    "observed-quiet-promotion-narrative-owner-unavailable";
                return false;
            }
            CharacterNarrativeWorldSaveData narrativeBefore =
                narrativePersistence.Capture();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-quiet-promotion-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate
                        .RecordObservedQuietPromotionLifeEvent(receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                {
                    RestoreQuietPromotionOwner(
                        societyBefore,
                        owner,
                        moodBefore,
                        narrativePersistence,
                        narrativeBefore);
                }
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-quiet-promotion-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Publishes one prepared observed life-event candidate.",
        "IObservedCareerLifeEventCommand implementations only")]
    private bool CommitObservedPendingLifeEvent(
        string operationId,
        int absoluteDay,
        DomainFactEmission sourceFact,
        Func<V20CampaignRuntime, ObservedLifeEventCommitResult> observe)
    {
        bool stateChanged = false;
        CommitObservedLifeEvents(
            operationId,
            absoluteDay,
            candidate =>
            {
                ObservedLifeEventCommitResult result = observe(candidate);
                stateChanged = result.StateChanged;
                return new[] { result };
            }, sourceFact);
        return stateChanged;
    }

    private void RestoreObservedSociety(SocietyEventWorldSaveData snapshot) =>
        live.PublishSociety(live.PrepareSociety(
            snapshot ?? throw new ArgumentNullException(nameof(snapshot))));

    private void RestoreQuietPromotionOwner(
        SocietyEventWorldSaveData societyBefore,
        CharacterActor owner,
        CharacterMoodDeliveryTransactionSnapshot moodBefore,
        ICharacterNarrativePersistence narrativePersistence,
        CharacterNarrativeWorldSaveData narrativeBefore)
    {
        RestoreObservedSociety(societyBefore);
        owner.Stats.RestoreMoodDeliveryTransactionState(
            moodBefore ?? throw new ArgumentNullException(nameof(moodBefore)));
        narrativePersistence.PublishRestore(
            narrativePersistence.PrepareRestore(
                narrativeBefore
                ?? throw new ArgumentNullException(nameof(narrativeBefore))));
    }

    private int ResolveGeneration(int absoluteDay)
    {
        RunMilestoneEvaluationSnapshot snapshot = milestoneWorld.Build(
            absoluteDay);
        return Mathf.Max(
            0,
            Mathf.FloorToInt(snapshot.WorldMetrics.TryGetValue(
                V20WorldMetricKind.CompletedGenerations,
                out float generations)
                    ? generations
                    : 0f));
    }

    private IReadOnlyList<V20ResolvedEventResult> CommitObservedLifeEvents(
        string operationId,
        int absoluteDay,
        Func<V20CampaignRuntime, IReadOnlyList<ObservedLifeEventCommitResult>>
            observe,
        DomainFactEmission sourceFact = null)
    {
        V20CampaignRuntime candidate = CreateCampaignCandidate();
        IReadOnlyList<ObservedLifeEventCommitResult> observed =
            observe?.Invoke(candidate)
            ?? throw new ArgumentNullException(nameof(observe));
        V20ResolvedEventResult[] resolutions = observed
            .Where(value => value.Resolution.HasValue)
            .Select(value => value.Resolution.Value)
            .ToArray();
        if (!observed.Any(value => value.StateChanged))
            return resolutions;

        ContentResolutionRequest request = new()
        {
            ActionId = operationId,
            AbsoluteDay = Math.Max(1, absoluteDay)
        };
        if (!TryPreflightEffects(
                request,
                resolutions,
                out EffectCommitPlan plan,
                out DomainFailure preflightFailure))
            throw new InvalidOperationException(
                $"Observed life-event effect preflight failed: {preflightFailure.Code}.");

        SeasonalEventAggregateState seasonal =
            live.PrepareSeasonal(candidate.CaptureSeasonal());
        SocietyEventAggregateState society =
            live.PrepareSociety(candidate.CaptureSociety());
        FactionCampaignAggregateState factions =
            live.PrepareFactions(candidate.CaptureFactions());
        RunMilestoneAggregateState milestones =
            live.PrepareMilestones(candidate.CaptureMilestones());
        EffectOwnerSnapshot ownersBefore = CaptureEffectOwners(resolutions);
        string stateBeforeFingerprint =
            CaptureEffectOwnerFingerprint(ownersBefore);
        string stateAfterFingerprint = string.Empty;
        // Observed parent and child facts are one completion boundary. Pure typed
        // effects have captured rollback-capable owners, so a definite rejection
        // must not leave them in forward recovery without their parent receipt.
        // Monetary/physical commits still retain the existing forward-only boundary.
        bool requiresForwardRecovery = RequiresForwardRecovery(
            plan,
            resolutions) && (sourceFact == null || plan.MoneyDelta != 0
                || plan.ItemCosts.Count > 0 || plan.Grants.Count > 0);
        V20ContentResolutionRecoveryPhase effectPhase =
            V20ContentResolutionRecoveryPhase.Prepared;
        EffectCommitReferences childCommits = new();
        bool recoveryPublished = false;
        try
        {
            if (!TryCommitEffects(
                    request,
                    resolutions,
                    plan,
                    out DomainFailure effectFailure,
                    out effectPhase,
                    out childCommits))
            {
                throw new InvalidOperationException(
                    $"Observed life-event effect commit failed: {effectFailure.Code}.");
            }
            stateAfterFingerprint = CaptureEffectOwnerFingerprint(
                CaptureEffectOwners(resolutions));
            if (requiresForwardRecovery)
            {
                UpsertRecovery(
                    milestones,
                    CreateRecovery(
                        request,
                        resolutions,
                        plan.Dropoff,
                        V20ContentResolutionRecoveryPhase.TypedEffectsApplied,
                        childCommits,
                        stateBeforeFingerprint,
                        stateAfterFingerprint));
            }
            live.PublishContentResolution(
                seasonal,
                society,
                factions,
                milestones);
            recoveryPublished = requiresForwardRecovery;
            PublishResolvedFacts(
                operationId,
                absoluteDay,
                resolutions,
                childCommits,
                stateBeforeFingerprint,
                stateAfterFingerprint,
                sourceFact);
            if (requiresForwardRecovery)
            {
                AdvanceContentResolutionRecovery(
                    operationId,
                    V20ContentResolutionRecoveryPhase.OutcomesCommitted);
                RemoveContentResolutionRecovery(operationId);
            }
        }
        catch
        {
            if (requiresForwardRecovery
                && effectPhase
                    >= V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
            {
                if (effectPhase
                    == V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
                {
                    RestoreTypedEffectOwners(ownersBefore);
                }
                if (!recoveryPublished)
                {
                    UpsertRecovery(
                        milestones,
                        CreateRecovery(
                            request,
                            resolutions,
                            plan.Dropoff,
                            effectPhase,
                            childCommits,
                            stateBeforeFingerprint,
                            string.Empty));
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones);
                }
            }
            else
            {
                RestoreEffectOwners(ownersBefore, operationId);
            }
            plan.Release(reservations);
            throw;
        }
        return resolutions;
    }

    private void RequireObservedLifeEventEffectsPreflight(
        string operationId,
        int absoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        ContentResolutionRequest request = new()
        {
            ActionId = operationId,
            AbsoluteDay = Math.Max(1, absoluteDay)
        };
        if (!TryPreflightEffects(
                request,
                resolutions,
                out EffectCommitPlan plan,
                out DomainFailure failure))
            throw new InvalidOperationException(
                $"Observed life-event effect preflight failed: {failure.Code}.");
        plan.Release(reservations);
    }

    private static V20ContentResolutionRecoverySaveData CreateRecovery(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        Vector2Int grantDropoff,
        V20ContentResolutionRecoveryPhase phase,
        EffectCommitReferences childCommits,
        string stateBeforeFingerprint,
        string stateAfterFingerprint) => new()
    {
        actionId = request.ActionId.Trim(),
        absoluteDay = Math.Max(1, request.AbsoluteDay),
        requestKind = request.Kind,
        phase = phase,
        grantDropoffX = grantDropoff.x,
        grantDropoffY = grantDropoff.y,
        childCommitEvidenceVersion = 1,
        moneyTransactionId = childCommits?.MoneyTransactionId ?? string.Empty,
        itemConsumptionOperationId = childCommits
            ?.ItemConsumptionOperationId ?? string.Empty,
        itemGrantBatchCommitId = childCommits?.ItemGrantBatchCommitId
            ?? string.Empty,
        durableEquipmentEvidenceVersion = string.IsNullOrWhiteSpace(
            childCommits?.DurableEquipmentContext)
                ? 0
                : 1,
        durableEquipmentStackId = childCommits?.DurableEquipmentStackId
            ?? string.Empty,
        durableEquipmentItemId = childCommits?.DurableEquipmentItemId
            ?? string.Empty,
        durableEquipmentContext = childCommits?.DurableEquipmentContext
            ?? string.Empty,
        outcomeEvidenceVersion = 1,
        stateBeforeFingerprint = stateBeforeFingerprint ?? string.Empty,
        stateAfterFingerprint = stateAfterFingerprint ?? string.Empty,
        resolutions = (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .Select(ToRecovery)
            .ToList()
    };

    private static V20ResolvedEventRecoverySaveData ToRecovery(
        V20ResolvedEventResult value) => new()
    {
        definitionId = value.DefinitionId,
        resolutionId = value.ResolutionId,
        effects = value.Effects.Select(effect => new V20ContentEffect
        {
            kind = effect.kind,
            targetId = effect.targetId,
            amount = effect.amount,
            durationDays = effect.durationDays
        }).ToList(),
        participantCharacterIds = value.ParticipantCharacterIds.ToList(),
        contextFactionId = value.ContextFactionId,
        instanceId = value.InstanceId,
        displayName = value.DisplayName,
        riskTier = value.RiskTier,
        riskReason = value.RiskReason,
        terminalActionLabel = value.TerminalActionLabel,
        terminalOutcomeNarrative = value.TerminalOutcomeNarrative,
        terminalEffectKinds = value.TerminalEffectKinds.ToList(),
        observedCause = value.ObservedCause,
        occurrenceInstanceId = value.OccurrenceInstanceId,
        applications = value.Applications
            .Select(application => application.Clone())
            .ToList()
    };

    private static V20ResolvedEventResult FromRecovery(
        V20ResolvedEventRecoverySaveData value) => new(
        value.definitionId,
        value.resolutionId,
        (value.effects ?? new List<V20ContentEffect>()).Select(effect =>
            new V20ContentEffect
            {
                kind = effect.kind,
                targetId = effect.targetId,
                amount = effect.amount,
                durationDays = effect.durationDays
            }).ToArray(),
        value.participantCharacterIds?.ToArray() ?? Array.Empty<string>(),
        value.contextFactionId,
        value.instanceId,
        value.displayName,
        value.riskTier,
        value.riskReason,
        value.terminalActionLabel,
        value.terminalOutcomeNarrative,
        value.terminalEffectKinds?.ToArray()
            ?? Array.Empty<V20ContentEffectKind>(),
        value.observedCause,
        value.occurrenceInstanceId,
        value.applications?.Select(application => application?.Clone())
            .Where(application => application != null)
            .ToArray()
            ?? Array.Empty<V20EffectApplicationEvidence>());

    private static void UpsertRecovery(
        RunMilestoneAggregateState milestones,
        V20ContentResolutionRecoverySaveData recovery)
    {
        milestones.Data.pendingContentResolutions.RemoveAll(value =>
            value != null && string.Equals(
                value.actionId,
                recovery.actionId,
                StringComparison.Ordinal));
        milestones.Data.pendingContentResolutions.Add(recovery);
        milestones.Data.pendingContentResolutions = milestones.Data
            .pendingContentResolutions
            .OrderBy(value => value.actionId, StringComparer.Ordinal)
            .ToList();
    }

    private bool TryRecoverPendingContentResolutions(
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            live.CaptureMilestones());
        V20ContentResolutionRecoverySaveData[] pending = milestones.Data
            .pendingContentResolutions
            .OrderBy(value => value.actionId, StringComparer.Ordinal)
            .ToArray();
        foreach (V20ContentResolutionRecoverySaveData row in pending)
        {
            V20ContentResolutionRecoverySaveData current = row;
            string actionId = current.actionId;
            V20ResolvedEventResult[] resolved = current.resolutions
                .Select(FromRecovery)
                .ToArray();
            EffectCommitReferences childCommits = EffectCommitReferences.From(
                current);
            if (!TryValidateRecoveredChildCommits(
                    current,
                    resolved,
                    out failure))
            {
                return false;
            }
            if (current.phase == V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
            {
                EffectCommitPlan plan = new(current.actionId)
                {
                    Dropoff = new Vector2Int(
                        current.grantDropoffX,
                        current.grantDropoffY)
                };
                plan.Grants.AddRange(resolved
                    .SelectMany(value => value.Effects)
                    .Where(value => value.kind
                        == V20ContentEffectKind.ItemGrant));
                if (plan.Grants.Count > 0)
                {
                    if (!exactSources.TryPrepare(
                            plan.CreateGrantPublicationPlan(),
                            out PhysicalItemExactSourcePublicationTransaction
                                transaction,
                            out _)
                        || !string.Equals(
                            transaction.BatchCommitId,
                            childCommits.ItemGrantBatchCommitId,
                            StringComparison.Ordinal)
                        || !exactSources.TryCommitReleased(
                            transaction,
                            plan.Dropoff,
                            "content-resolution-grant",
                            out PhysicalItemExactSourcePublicationReceipt receipt,
                            out _)
                        || !string.Equals(
                            receipt.BatchCommitId,
                            childCommits.ItemGrantBatchCommitId,
                            StringComparison.Ordinal))
                    {
                        failure = new DomainFailure(
                            FailureCode.ProductionOutputUnavailable);
                        return false;
                    }
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.ItemGrantsCommitted;
                live.PublishMilestones(milestones);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
            {
                EffectOwnerSnapshot before = CaptureEffectOwners(resolved);
                string recoveryBeforeFingerprint =
                    current.outcomeEvidenceVersion == 1
                    && !string.IsNullOrWhiteSpace(
                        current.stateBeforeFingerprint)
                        ? current.stateBeforeFingerprint
                        : CaptureEffectOwnerFingerprint(before);
                CharacterCareerAggregate latestCareer = null;
                try
                {
                    ContentResolutionRequest recoveryRequest = new()
                    {
                        ActionId = current.actionId,
                        AbsoluteDay = current.absoluteDay,
                        Kind = current.requestKind
                    };
                    if (!TryPrepareRetirementSchedule(
                            recoveryRequest,
                            resolved,
                            out RetirementSchedulePlan retirementPlan,
                            out DomainFailure retirementFailure))
                    {
                        failure = retirementFailure;
                        return false;
                    }
                    ApplyTypedDomainEffects(current.absoluteDay, resolved);
                    if (retirementPlan.IsRequired)
                    {
                        latestCareer = careers.PrepareRestore(careers.Capture());
                        string retirementFailureReason =
                            "character is no longer an eligible living elder";
                        if (!IsEligibleLivingElder(retirementPlan.CharacterId)
                            || !latestCareer.TryScheduleRetirement(
                                retirementPlan.CharacterId,
                                retirementPlan.EventId,
                                retirementPlan.ChoiceId,
                                retirementPlan.DecisionAbsoluteDay,
                                retirementPlan.DueAbsoluteDay,
                                out retirementFailureReason))
                        {
                            throw new InvalidOperationException(
                                "Retirement recovery could not schedule the frozen decision: "
                                + retirementFailureReason);
                        }
                    }
                }
                catch
                {
                    RestoreTypedEffectOwners(before);
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        "content-typed-effect-recovery-failed");
                    return false;
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.TypedEffectsApplied;
                current.outcomeEvidenceVersion = 1;
                current.stateBeforeFingerprint = recoveryBeforeFingerprint;
                EffectOwnerSnapshot after = CaptureEffectOwners(resolved);
                after.Milestones = milestones.Data;
                if (latestCareer != null)
                    after.Careers = latestCareer.CaptureWorld();
                resolved = AttachApplicationEvidence(
                    resolved,
                    before,
                    after,
                    childCommits).ToArray();
                current.resolutions = resolved.Select(ToRecovery).ToList();
                current.stateAfterFingerprint =
                    CaptureEffectOwnerFingerprint(after);
                if (latestCareer != null)
                {
                    live.PublishContentResolution(
                        live.PrepareSeasonal(live.CaptureSeasonal()),
                        live.PrepareSociety(live.CaptureSociety()),
                        live.PrepareFactions(live.CaptureFactions()),
                        milestones,
                        latestCareer);
                }
                else
                {
                    live.PublishMilestones(milestones);
                }
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.TypedEffectsApplied)
            {
                try
                {
                    PublishResolvedFacts(
                        current.actionId,
                        current.absoluteDay,
                        resolved,
                        childCommits,
                        string.IsNullOrWhiteSpace(
                            current.stateBeforeFingerprint)
                            ? "legacy-unavailable"
                            : current.stateBeforeFingerprint,
                        string.IsNullOrWhiteSpace(
                            current.stateAfterFingerprint)
                            ? CaptureEffectOwnerFingerprint(
                                CaptureEffectOwners(resolved))
                            : current.stateAfterFingerprint);
                }
                catch
                {
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        "content-outcome-recovery-failed");
                    return false;
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.OutcomesCommitted;
                live.PublishMilestones(milestones);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.OutcomesCommitted)
            {
                milestones.Data.pendingContentResolutions.RemoveAll(value =>
                    value != null && string.Equals(
                        value.actionId,
                        current.actionId,
                        StringComparison.Ordinal));
                live.PublishMilestones(milestones);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
            }
        }
        return true;
    }

    private bool TryValidateRecoveredChildCommits(
        V20ContentResolutionRecoverySaveData recovery,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        int moneyDelta = (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .SelectMany(value => value.Effects)
            .Where(value => value != null
                && value.IsValid
                && value.kind == V20ContentEffectKind.Money)
            .Sum(value => Mathf.RoundToInt(value.amount));
        if (moneyDelta == 0)
            return true;

        EconomyTransactionKind kind = moneyDelta > 0
            ? EconomyTransactionKind.ContractIncome
            : EconomyTransactionKind.LegacyExpense;
        string sourceId = "content:" + recovery.actionId;
        if (money is not IGameMoneyTransactionQuery query
            || !query.TryGetSuccessfulTransaction(
                kind,
                sourceId,
                out EconomyTransactionRecord record)
            || record == null
            || record.amount != moneyDelta
            || !string.Equals(
                record.transactionId,
                recovery.moneyTransactionId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "content-money-recovery-evidence-missing");
            return false;
        }
        return true;
    }

    private void AdvanceContentResolutionRecovery(
        string actionId,
        V20ContentResolutionRecoveryPhase phase)
    {
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            live.CaptureMilestones());
        V20ContentResolutionRecoverySaveData recovery = milestones.Data
            .pendingContentResolutions.Single(value => value != null
                && string.Equals(
                    value.actionId,
                    actionId?.Trim(),
                    StringComparison.Ordinal));
        recovery.phase = phase;
        live.PublishMilestones(milestones);
    }

    private void RemoveContentResolutionRecovery(string actionId)
    {
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            live.CaptureMilestones());
        int removed = milestones.Data.pendingContentResolutions.RemoveAll(value =>
            value != null && string.Equals(
                value.actionId,
                actionId?.Trim(),
                StringComparison.Ordinal));
        if (removed != 1)
        {
            throw new InvalidOperationException(
                "Completed content resolution did not own exactly one recovery row.");
        }
        live.PublishMilestones(milestones);
    }

    public bool TryExecute(
        ContentResolutionRequest request,
        out ContentResolutionResult result,
        out DomainFailure failure)
    {
        factPublisher.AssertIdle();
        result = null;
        failure = DomainFailure.None;
        string requestedActionId = request?.ActionId?.Trim() ?? string.Empty;
        V20ContentResolutionRecoverySaveData requestedRecovery =
            requestedActionId.Length == 0
                ? null
                : live.CaptureMilestones().pendingContentResolutions
                    .SingleOrDefault(value => value != null && string.Equals(
                        value.actionId,
                        requestedActionId,
                        StringComparison.Ordinal));
        if (!TryRecoverPendingContentResolutions(out failure))
        {
            return false;
        }
        if (request == null || string.IsNullOrWhiteSpace(request.ActionId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (requestedRecovery != null)
        {
            V20ResolvedEventResult[] recoveredResolutions = requestedRecovery
                .resolutions
                .Select(FromRecovery)
                .ToArray();
            ContentResolutionRequest recoveredRequest = new()
            {
                ActionId = requestedRecovery.actionId,
                AbsoluteDay = requestedRecovery.absoluteDay,
                Kind = requestedRecovery.requestKind,
                InstanceId = recoveredResolutions
                    .Select(value => value.InstanceId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            };
            result = new ContentResolutionResult
            {
                ActionId = requestedRecovery.actionId,
                Resolutions = recoveredResolutions,
                Disposition = IsPendingGuestRequestChoice(
                        CreateCampaignCandidate(),
                        recoveredRequest)
                    ? ContentResolutionDisposition.AcceptedPending
                    : ContentResolutionDisposition.Terminal
            };
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.FactionContractOutcome
            && request.ContractSucceeded
            && !TryValidateLiveFactionDeliveryReceipt(
                request.FactionId,
                out failure))
        {
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
        RecordCommittedChoice(candidate, request, resolved);
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
        if (!TryPrepareRetirementSchedule(
                request,
                resolved,
                out RetirementSchedulePlan retirementPlan,
                out failure))
        {
            plan.Release(reservations);
            return false;
        }

        BuildableObject administrationOffice = null;
        if (RequiresAdministrativeSeal(request.Kind))
        {
            administrationOffice = facilities
                .FindOperational(FacilityCapabilityKind.Administration)
                .FirstOrDefault();
            if (administrationOffice == null)
            {
                plan.Release(reservations);
                failure = new DomainFailure(
                    FailureCode.ServiceFeatureMissing,
                    FacilityCapabilityKind.Administration.ToString());
                return false;
            }
        }

        bool acceptedMaterialOwnerEnsured = false;
        string acceptedMaterialDestinationId = string.Empty;
        if ((request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept)
            && !TryEnsureAcceptedMaterialInputOwner(
                candidate,
                request.FactionId,
                out acceptedMaterialOwnerEnsured,
                out acceptedMaterialDestinationId,
                out failure))
        {
            plan.Release(reservations);
            return false;
        }

        SeasonalEventAggregateState seasonal;
        SocietyEventAggregateState society;
        FactionCampaignAggregateState factions;
        RunMilestoneAggregateState milestones;
        try
        {
            seasonal = live.PrepareSeasonal(candidate.CaptureSeasonal());
            society = live.PrepareSociety(candidate.CaptureSociety());
            factions = live.PrepareFactions(candidate.CaptureFactions());
            milestones = live.PrepareMilestones(candidate.CaptureMilestones());
        }
        catch
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "candidate-prepare-failed");
            throw;
        }

        EffectOwnerSnapshot ownersBefore;
        try
        {
            ownersBefore = CaptureEffectOwners(resolved);
        }
        catch
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "effect-owner-capture-failed");
            plan.Release(reservations);
            throw;
        }
        string stateBeforeFingerprint =
            CaptureEffectOwnerFingerprint(ownersBefore);
        string stateAfterFingerprint = string.Empty;

        bool requiresForwardRecovery = RequiresForwardRecovery(plan, resolved);
        DomainFailure effectFailure = DomainFailure.None;
        V20ContentResolutionRecoveryPhase effectPhase =
            V20ContentResolutionRecoveryPhase.Prepared;
        EffectCommitReferences childCommits = new();
        Exception transactionFailure = null;
        bool durableRecoveryPending = false;
        bool recoveryPublished = false;
        DurableFacilityEquipmentUseEvidence administrativeEquipment = null;
        bool CommitResolution()
        {
            try
            {
                if (!TryCommitEffects(
                        request,
                        resolved,
                        plan,
                        out effectFailure,
                        out effectPhase,
                        out childCommits))
                {
                    return false;
                }
                childCommits.SetDurableEquipment(administrativeEquipment);

                CharacterCareerAggregate latestCareer = null;
                if (retirementPlan.IsRequired)
                {
                    latestCareer = careers.PrepareRestore(careers.Capture());
                    string retirementFailure =
                        "character is no longer an eligible living elder";
                    if (!IsEligibleLivingElder(retirementPlan.CharacterId)
                        || !latestCareer.TryScheduleRetirement(
                            retirementPlan.CharacterId,
                            retirementPlan.EventId,
                            retirementPlan.ChoiceId,
                            retirementPlan.DecisionAbsoluteDay,
                            retirementPlan.DueAbsoluteDay,
                            out retirementFailure))
                    {
                        throw new InvalidOperationException(
                            "Retirement eligibility changed after external effects; "
                            + "latest career state was preserved. "
                            + retirementFailure);
                    }
                }

                EffectOwnerSnapshot ownersAfter = CaptureEffectOwners(resolved);
                ownersAfter.Seasonal = seasonal.Data;
                ownersAfter.Society = society.Data;
                ownersAfter.Factions = factions.Data;
                ownersAfter.Milestones = milestones.Data;
                if (latestCareer != null)
                    ownersAfter.Careers = latestCareer.CaptureWorld();
                resolved = AttachApplicationEvidence(
                    resolved,
                    ownersBefore,
                    ownersAfter,
                    childCommits);
                stateAfterFingerprint = CaptureEffectOwnerFingerprint(
                    ownersAfter);

                if (requiresForwardRecovery)
                {
                    UpsertRecovery(
                        milestones,
                        CreateRecovery(
                            request,
                            resolved,
                            plan.Dropoff,
                            V20ContentResolutionRecoveryPhase.TypedEffectsApplied,
                            childCommits,
                            stateBeforeFingerprint,
                            stateAfterFingerprint));
                }
                if (latestCareer != null)
                {
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones,
                        latestCareer);
                }
                else
                {
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones);
                }
                recoveryPublished = requiresForwardRecovery;
                PublishResolvedFacts(
                    request.ActionId,
                    request.AbsoluteDay,
                    resolved,
                    childCommits,
                    stateBeforeFingerprint,
                    stateAfterFingerprint);
                if (requiresForwardRecovery)
                {
                    AdvanceContentResolutionRecovery(
                        request.ActionId,
                        V20ContentResolutionRecoveryPhase.OutcomesCommitted);
                    RemoveContentResolutionRecovery(request.ActionId);
                }
                return true;
            }
            catch (Exception exception)
            {
                transactionFailure = exception;
                if (requiresForwardRecovery && effectPhase
                    >= V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
                {
                    if (!recoveryPublished)
                    {
                        if (effectPhase
                            >= V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
                        {
                            RestoreTypedEffectOwners(ownersBefore);
                            effectPhase = V20ContentResolutionRecoveryPhase
                                .ItemGrantsCommitted;
                        }
                        UpsertRecovery(
                            milestones,
                            CreateRecovery(
                                request,
                                resolved,
                                plan.Dropoff,
                                effectPhase,
                                childCommits,
                                stateBeforeFingerprint,
                                string.Empty));
                        live.PublishContentResolution(
                            seasonal,
                            society,
                            factions,
                            milestones);
                        recoveryPublished = true;
                    }
                    durableRecoveryPending = true;
                    return true;
                }
                return false;
            }
        }

        bool transactionCommitted;
        try
        {
            transactionCommitted = administrationOffice != null
                ? administrativeSealEquipment.TryCommitResolution(
                    administrationOffice.RequirePersistentInstanceId(),
                    administrationOffice.centerPos,
                    context =>
                    {
                        administrativeEquipment =
                            new DurableFacilityEquipmentUseEvidence(context);
                        return CommitResolution();
                    },
                    out _,
                    out _)
                : CommitResolution();
        }
        catch (Exception exception)
        {
            transactionFailure ??= exception;
            transactionCommitted = false;
        }

        if (durableRecoveryPending)
        {
            plan.Release(reservations);
            if (transactionFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(transactionFailure)
                    .Throw();
            }
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "content-resolution-recovery-pending");
            return false;
        }

        if (!transactionCommitted)
        {
            RestoreEffectOwners(ownersBefore, request.ActionId);
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "content-transaction-failed");
            plan.Release(reservations);
            if (transactionFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(transactionFailure)
                    .Throw();
            }
            failure = effectFailure.IsFailure
                ? effectFailure
                : new DomainFailure(
                    FailureCode.ServiceFeatureMissing,
                    DurableToolItemRules.AdministrativeSeal);
            return false;
        }
        result = new ContentResolutionResult
        {
            ActionId = request.ActionId.Trim(),
            Resolutions = resolved,
            Disposition = IsPendingGuestRequestChoice(candidate, request)
                ? ContentResolutionDisposition.AcceptedPending
                : ContentResolutionDisposition.Terminal
        };
        if (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept)
            AdvanceFactionContractDeliveries(
                Math.Max(1, request.AbsoluteDay),
                request.Requirements);
        return true;
    }

    private void PublishResolvedFacts(
        string operationId,
        int absoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        EffectCommitReferences childCommits,
        string stateBeforeFingerprint,
        string stateAfterFingerprint,
        DomainFactEmission sourceFact = null)
    {
        var concrete = (resolutions ?? Array.Empty<V20ResolvedEventResult>()).ToArray();
        var leases = new List<DomainFactLease>();
        var emissions = new List<DomainFactEmission>();
        try
        {
            for (int index = 0; index < concrete.Length; index++)
            {
                var fact = new ContentResolutionFact(new ContentResolutionFactData
                {
                    operationId = operationId.Trim(), absoluteDay = Math.Max(1, absoluteDay),
                    resolutionIndex = index, resolution = ToRecovery(concrete[index]),
                    moneyTransactionId = childCommits?.MoneyTransactionId ?? string.Empty,
                    itemConsumptionOperationId = childCommits?.ItemConsumptionOperationId ?? string.Empty,
                    itemGrantBatchCommitId = childCommits?.ItemGrantBatchCommitId ?? string.Empty,
                    durableEquipmentStackId = childCommits?.DurableEquipmentStackId ?? string.Empty,
                    durableEquipmentItemId = childCommits?.DurableEquipmentItemId ?? string.Empty,
                    durableEquipmentContext = childCommits?.DurableEquipmentContext ?? string.Empty,
                    stateBeforeFingerprint = stateBeforeFingerprint ?? string.Empty,
                    stateAfterFingerprint = stateAfterFingerprint ?? string.Empty
                });
                if (!factPublisher.TryReserve(fact, out var lease, out string failure))
                    throw new ContentResolutionFactRejectedException("content-resolution-fact-reservation:" + failure);
                leases.Add(lease);
                emissions.Add(DomainFactEmission.Create(lease, fact));
            }
            if (sourceFact != null) emissions.Add(sourceFact);
            if (emissions.Count == 0) return;
            var results = new DomainFactDelivery[emissions.Count];
            if (!factPublisher.PublishBatch(emissions, results, out string detail)
                || results.Any(value => !value.MayBeCommitted))
                throw new ContentResolutionFactRejectedException("content-resolution-fact-publication:" + detail);
            // Uncertain acknowledgement keeps the effects. Recovery replays only frozen facts.
        }
        catch
        {
            foreach (var lease in leases) factPublisher.Cancel(lease);
            if (sourceFact != null) factPublisher.Cancel(sourceFact.Lease);
            throw;
        }
    }

    private static string CaptureEffectOwnerFingerprint(
        EffectOwnerSnapshot snapshot)
    {
        snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        var parts = new List<string>
        {
            JsonUtility.ToJson(snapshot.Seasonal),
            JsonUtility.ToJson(snapshot.Society),
            JsonUtility.ToJson(snapshot.Factions),
            JsonUtility.ToJson(snapshot.Milestones),
            JsonUtility.ToJson(snapshot.Items),
            snapshot.MoneyBalance.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            JsonUtility.ToJson(snapshot.Careers),
            JsonUtility.ToJson(snapshot.BodyHealth),
            JsonUtility.ToJson(snapshot.Psychosocial),
            JsonUtility.ToJson(snapshot.PopulationHealth),
            JsonUtility.ToJson(snapshot.Narrative)
        };
        foreach (CharacterEffectOwnerSnapshot character in snapshot.Characters
                     .OrderBy(value => value.Actor.Identity?.PersistentId,
                         StringComparer.Ordinal))
        {
            parts.Add(character.Actor.Identity?.PersistentId ?? string.Empty);
            parts.Add(JsonUtility.ToJson(character.Stats));
            parts.Add(JsonUtility.ToJson(character.Progression));
            parts.Add(JsonUtility.ToJson(character.Social));
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(
            string.Join("\n", parts));
    }

    private static bool RequiresAdministrativeSeal(
        ContentResolutionRequestKind kind) =>
        kind is ContentResolutionRequestKind.FactionChapterChoice
            or ContentResolutionRequestKind.FactionContractAccept
            or ContentResolutionRequestKind.SeasonalFactionContractAccept
            or ContentResolutionRequestKind.FactionContractOutcome;

    private V20CampaignRuntime CreateCampaignCandidate()
    {
        V20CampaignRuntime candidate = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog,
            physicalDispositions,
            live.SeasonalFeedSelfHeatingTargets);
        candidate.PublishSeasonal(candidate.PrepareSeasonal(live.CaptureSeasonal()));
        candidate.PublishSociety(candidate.PrepareSociety(live.CaptureSociety()));
        candidate.PublishFactions(candidate.PrepareFactions(live.CaptureFactions()));
        candidate.PublishMilestones(candidate.PrepareMilestones(live.CaptureMilestones()));
        return candidate;
    }

    private static void RecordCommittedChoice(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolved)
    {
        if (request.Kind is not (ContentResolutionRequestKind.SocietyChoice
            or ContentResolutionRequestKind.GuestRequestDeliveryOutcome
            or ContentResolutionRequestKind.FactionChapterChoice))
        {
            return;
        }

        if (request.Kind == ContentResolutionRequestKind.GuestRequestDeliveryOutcome
            && string.Equals(
                request.ChoiceId,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal))
        {
            return;
        }

        if (request.Kind == ContentResolutionRequestKind.SocietyChoice
            && (resolved?.Count ?? 0) == 0)
        {
            return;
        }

        if (resolved == null || resolved.Count != 1)
        {
            throw new InvalidOperationException(
                "Explicit content choice must resolve exactly one authored decision.");
        }

        V20ResolvedEventResult decision = resolved[0];
        bool society = request.Kind is ContentResolutionRequestKind.SocietyChoice
            or ContentResolutionRequestKind.GuestRequestDeliveryOutcome;
        candidate.RecordCommittedChoice(
            society
                ? CommittedRunChoiceKind.SocietyEventChoice
                : CommittedRunChoiceKind.FactionChapterChoice,
            society
                ? V20CampaignRuntime.SocietyChoiceOwnerId
                : decision.ContextFactionId,
            decision.DefinitionId,
            society ? request.InstanceId : decision.DefinitionId,
            decision.ResolutionId,
            request.ActionId);
    }

    private static bool IsPendingGuestRequestChoice(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request) =>
        request.Kind == ContentResolutionRequestKind.SocietyChoice
        && candidate.ActiveSocietyEvents.Any(value => value != null
            && string.Equals(
                value.instanceId,
                request.InstanceId?.Trim(),
                StringComparison.Ordinal)
            && ((value.guestDelivery?.phase
                        ?? GuestRequestDeliveryPhase.None)
                    != GuestRequestDeliveryPhase.None
                || (value.observedIncidentResponse?.phase
                        ?? ObservedIncidentResponsePhase.None)
                    != ObservedIncidentResponsePhase.None));

    private bool TryPrepareRetirementSchedule(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out RetirementSchedulePlan plan,
        out DomainFailure failure)
    {
        plan = default;
        failure = DomainFailure.None;
        V20ResolvedEventResult[] scheduled = resolutions
            .Where(value => value.Effects.Any(effect => effect != null
                && effect.kind == V20ContentEffectKind.RetirementSchedule))
            .ToArray();
        if (scheduled.Length == 0)
            return true;
        if (request.Kind != ContentResolutionRequestKind.SocietyChoice
            || scheduled.Length != 1)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        V20ResolvedEventResult resolution = scheduled[0];
        V20ContentEffect[] effects = resolution.Effects
            .Where(effect => effect != null
                && effect.kind == V20ContentEffectKind.RetirementSchedule)
            .ToArray();
        string[] participants = resolution.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (effects.Length != 1 || participants.Length != 1)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        CharacterId characterId = new(participants[0]);
        V20ContentEffect effect = effects[0];
        int decisionAbsoluteDay = Math.Max(1, request.AbsoluteDay);
        int dueAbsoluteDay;
        try
        {
            dueAbsoluteDay = checked(decisionAbsoluteDay + effect.durationDays);
        }
        catch (OverflowException)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        CharacterCareerAggregate candidate = careers.PrepareRestore(
            careers.Capture());
        if (effect.durationDays < 0
            || !IsEligibleLivingElder(characterId)
            || !candidate.TryScheduleRetirement(
                characterId,
                resolution.DefinitionId,
                resolution.ResolutionId,
                decisionAbsoluteDay,
                dueAbsoluteDay,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        plan = new RetirementSchedulePlan(
            characterId,
            resolution.DefinitionId,
            resolution.ResolutionId,
            decisionAbsoluteDay,
            dueAbsoluteDay);
        return true;
    }

    private bool IsEligibleLivingElder(CharacterId characterId) =>
        requirements.IsLivingCharacterAtStage(
            characterId,
            CharacterLifeStage.Elder);

    private readonly struct RetirementSchedulePlan
    {
        public RetirementSchedulePlan(
            CharacterId characterId,
            string eventId,
            string choiceId,
            int decisionAbsoluteDay,
            int dueAbsoluteDay)
        {
            CharacterId = characterId;
            EventId = eventId;
            ChoiceId = choiceId;
            DecisionAbsoluteDay = decisionAbsoluteDay;
            DueAbsoluteDay = dueAbsoluteDay;
            IsRequired = true;
        }

        public CharacterId CharacterId { get; }
        public string EventId { get; }
        public string ChoiceId { get; }
        public int DecisionAbsoluteDay { get; }
        public int DueAbsoluteDay { get; }
        public bool IsRequired { get; }
    }

    public bool RequiresAdvance => live.CaptureFactions().factions
        .Where(value => value != null)
        .Any(value => FactionContractDeliveryOutbox.HasPending(value)
            || value.deliveryTerminalCleanupPending
            || (!string.IsNullOrWhiteSpace(value.activeContractId)
                && catalog.Contracts.Any(contract =>
                    contract != null
                    && string.Equals(
                        contract.StableId,
                        value.activeContractId,
                        StringComparison.Ordinal)
                    && FactionContractMaterialRules.HasMaterialRequirements(
                        contract))));

    public void ValidateBeforeCapture()
    {
        factPublisher.AssertIdle();
        FactionCampaignStateSaveData[] states = live.CaptureFactions().factions
            .Where(value => value != null)
            .ToArray();
        Dictionary<string, FactionCampaignStateSaveData> campaignPending = states
            .Where(FactionContractDeliveryOutbox.HasPending)
            .ToDictionary(
                value => value.deliveryOperationId,
                value => value,
                StringComparer.Ordinal);

        foreach (FactionCampaignStateSaveData state in campaignPending.Values)
        {
            if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
                || !factionContractTransfers.TryGetPending(
                    state.deliveryOperationId,
                    out PhysicalItemBatchDispositionReceipt physical)
                || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                    state,
                    physical))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed a campaign/physical "
                    + $"receipt transition for '{state.factionId}'.");
            }
        }

        foreach (FactionCampaignStateSaveData state in states.Where(value =>
                     !string.IsNullOrEmpty(value.activeContractOccurrenceId)
                     && !string.IsNullOrEmpty(value.activeContractId)))
        {
            FactionContractDefinitionSO contract = catalog.Contracts
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.StableId,
                        state.activeContractId,
                        StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(value.seasonalEventId));
            if (contract == null
                || !FactionContractMaterialRules.HasMaterialRequirements(contract))
                continue;
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId,
                state.activeContractOccurrenceId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for seasonal occurrence "
                    + $"'{state.activeContractOccurrenceId}'.");
            }
        }

        foreach (FactionContractDefinitionSO contract in catalog.Contracts
                     .Where(value => string.IsNullOrEmpty(value.seasonalEventId))
                     .Where(FactionContractMaterialRules.HasMaterialRequirements))
        {
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for '{contract.StableId}'.");
            }
        }

        SocietyEventWorldSaveData societySnapshot = live.CaptureSociety();
        _ = live.PrepareSociety(societySnapshot);
        foreach (V20ActiveEventSaveData active in societySnapshot.activeEvents
                     .Where(value => value != null
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             != GuestRequestDeliveryPhase.None))
        {
            GuestRequestDeliverySaveData delivery = active.guestDelivery;
            GuestRequestDefinitionSO definition = catalog.GuestRequests
                .Single(value => string.Equals(
                    value.StableId,
                    active.definitionId,
                    StringComparison.Ordinal));
            if (delivery.inputOwnerActive
                && !factionContractInputOwners.TryValidate(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    active.instanceId,
                    delivery.destinationId,
                    new Vector2Int(
                        delivery.destinationX,
                        delivery.destinationY),
                    EconomyProjectInputOwnerAnchorKind.LiveFacility,
                    delivery.venueFacilityInstanceId,
                    GuestRequestDeliveryMaterialRules
                        .BuildMaterialRequirements(definition),
                    delivery.inputCapacityGrams,
                    delivery.inputMassAuthorityRevision,
                    delivery.inputCapacityFingerprint,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    $"Guest-request save capture crossed input owner '{active.instanceId}': "
                    + ownerFailure);
            }

            string operationId = GuestRequestDeliveryOutbox.FormatOperationId(
                active.instanceId);
            bool hasPhysical = factionContractTransfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt physicalReceipt);
            bool hasSaved = GuestRequestDeliveryOutbox.IsCanonicalReceipt(
                delivery);
            if (hasPhysical != hasSaved
                || hasSaved && !GuestRequestDeliveryOutbox.MatchesSaved(
                    delivery,
                    physicalReceipt))
            {
                throw new InvalidOperationException(
                    $"Guest-request save capture crossed physical receipt '{operationId}' for occurrence '{active.instanceId}'.");
            }
        }
    }

    public IReadOnlyList<FactionContractView> GetContracts(string factionId)
    {
        string normalizedFactionId = factionId?.Trim() ?? string.Empty;
        if (!live.TryGetFaction(
                normalizedFactionId,
                out FactionCampaignStateSaveData state))
            return Array.Empty<FactionContractView>();

        WorldItemStackSnapshot[] physical = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .ToArray();
        bool hasAdministration = facilities
            .FindOperational(FacilityCapabilityKind.Administration)
            .Count > 0;
        bool hasDeliveryDropoff = dropZones.TryGetDeliveryDropoff(out _);
        return catalog.Contracts
            .Where(value => string.Equals(
                value.factionId,
                normalizedFactionId,
                StringComparison.Ordinal))
            .Select(contract => (
                Contract: contract,
                Occurrence: FindActiveSeasonalContractOccurrence(
                    contract,
                    normalizedFactionId)))
            .Where(value => string.IsNullOrEmpty(
                    value.Contract.seasonalEventId)
                || value.Occurrence != null)
            .OrderBy(value => value.Contract.kind)
            .ThenBy(value => value.Contract.StableId, StringComparer.Ordinal)
            .Select(value => BuildFactionContractView(
                value.Contract,
                value.Occurrence,
                state,
                physical,
                hasAdministration,
                hasDeliveryDropoff))
            .ToArray();
    }

    private V20ActiveEventSaveData FindActiveSeasonalContractOccurrence(
        FactionContractDefinitionSO contract,
        string factionId)
    {
        if (contract == null || string.IsNullOrEmpty(contract.seasonalEventId))
            return null;
        return live.ActiveSeasonalEvents.FirstOrDefault(value => value != null
            && !value.resolved
            && string.Equals(
                value.definitionId,
                contract.seasonalEventId,
                StringComparison.Ordinal)
            && string.Equals(
                value.contextFactionId,
                factionId,
                StringComparison.Ordinal));
    }

    public void AdvanceFactionContractDeliveries(
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements)
    {
        if (requirements == null)
            return;
        foreach (FactionCampaignStateSaveData snapshot in
                 live.CaptureFactions().factions
                     .Where(value => value != null)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            AdvanceFactionContractDelivery(
                snapshot.factionId,
                Math.Max(1, absoluteDay),
                requirements);
        }
    }

    private void AdvanceFactionContractDelivery(
        string factionId,
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements)
    {
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return;

        if (state.deliveryTerminalCleanupPending)
        {
            if (!TryRetireFactionContractInputOwner(
                    state,
                    out string retireFailure))
            {
                PublishDeliveryFailure(factionId, retireFailure);
                return;
            }
            state = RequireLiveFaction(factionId);
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.RewardPublished)
        {
            if (!TryGetMatchingPendingReceipt(
                    state,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string receiptFailure))
            {
                PublishDeliveryFailure(factionId, receiptFailure);
                return;
            }
            if (!factionContractTransfers.Acknowledge(
                    receipt.CommitId,
                    out string acknowledgementFailure))
            {
                PublishDeliveryFailure(factionId, acknowledgementFailure);
                return;
            }
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryClearDeliveryOutbox(
                            factionId,
                            out mutationFailure),
                "faction-contract-outbox-clear");
            return;
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.PhysicalCommitted)
        {
            ContentResolutionRequest outcome = new()
            {
                ActionId = V21ContentAlertActionIds.FactionContractOutcome(
                    factionId,
                    succeeded: true),
                Kind = ContentResolutionRequestKind.FactionContractOutcome,
                FactionId = factionId,
                ContractSucceeded = true,
                AbsoluteDay = absoluteDay,
                Requirements = requirements
            };
            if (!TryExecute(outcome, out _, out DomainFailure outcomeFailure))
                PublishDeliveryFailure(
                    factionId,
                    outcomeFailure.Code.ToString());
            return;
        }

        if (string.IsNullOrWhiteSpace(state.activeContractId))
            return;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return;
        if (!state.activeContractInputOwnerActive
            || string.IsNullOrWhiteSpace(state.activeContractDestinationId))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-input-owner-inactive");
            return;
        }

        string operationId = FactionContractDeliveryOutbox.FormatOperationId(
            contract.StableId,
            state.activeContractOccurrenceId);
        if (factionContractTransfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt existingReceipt))
        {
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryRecordDeliveryReceipt(
                            factionId,
                            FactionContractDeliveryReceipt.FromPhysical(
                                existingReceipt),
                            out mutationFailure),
                "faction-contract-existing-receipt-record");
            return;
        }

        Vector2Int destination = new(
            state.activeContractDestinationX,
            state.activeContractDestinationY);
        bool requestFailed = false;
        string requestFailure = string.Empty;
        foreach (KeyValuePair<string, int> required in material)
        {
            int assigned = CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer
                    or WorldItemStackState.Carried
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer);
            int missing = Math.Max(0, required.Value - assigned);
            if (missing <= 0)
                continue;
            if (!items.TryRequestItemDelivery(
                    required.Key,
                    missing,
                    destination,
                    state.activeContractDestinationId,
                    out int requested,
                    out requestFailure)
                || requested != missing)
            {
                requestFailed = true;
                if (string.IsNullOrWhiteSpace(requestFailure))
                {
                    requestFailure =
                        $"faction-contract-delivery-request-partial:"
                        + $"{required.Key}:{requested}/{missing}";
                }
                break;
            }
        }
        if (requestFailed)
        {
            PublishDeliveryFailure(factionId, requestFailure);
            return;
        }

        bool allArrived = material.All(required =>
            CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State == WorldItemStackState.FacilityBuffer)
            >= required.Value);
        if (!allArrived)
        {
            PublishDeliveryFailure(factionId, string.Empty);
            return;
        }

        if (absoluteDay > state.activeContractDeadlineAbsoluteDay)
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-deadline-expired-before-transfer");
            return;
        }
        if (!this.requirements.TryEvaluate(
                FactionContractMaterialRules.BuildNonMaterialRequirements(
                    contract),
                requirements,
                Array.Empty<string>(),
                out DomainFailure eligibilityFailure))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-non-material-condition:"
                + eligibilityFailure.Code);
            return;
        }

        if (!factionContractTransfers.TryCommitTransferPending(
                state.activeContractDestinationId,
                material,
                operationId,
                FactionContractDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt transferReceipt,
                out string transferFailure))
        {
            PublishDeliveryFailure(factionId, transferFailure);
            return;
        }
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryRecordDeliveryReceipt(
                        factionId,
                        FactionContractDeliveryReceipt.FromPhysical(
                            transferReceipt),
                        out mutationFailure),
            "faction-contract-receipt-record");
    }

    private bool TryEnsureAcceptedMaterialInputOwner(
        V20CampaignRuntime candidate,
        string factionId,
        out bool ensured,
        out string destinationId,
        out DomainFailure failure)
    {
        ensured = false;
        destinationId = string.Empty;
        failure = DomainFailure.None;
        if (!candidate.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return true;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return true;

        destinationId = state.activeContractDestinationId;
        if (!factionContractInputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationOwnerId,
                destinationId,
                new Vector2Int(
                    state.activeContractDestinationX,
                    state.activeContractDestinationY),
                EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                string.Empty,
                material,
                0L,
                0L,
                string.Empty,
                out EconomyProjectInputOwnerProjection projection,
                out string ownerFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        ensured = true;
        if (!((IFactionCampaignDeliveryCommand)candidate)
                .TrySetInputOwnerProjection(
                    factionId,
                    projection.CapacityGrams,
                    projection.MassAuthorityRevision,
                    projection.Fingerprint,
                    out string projectionFailure))
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                true,
                destinationId,
                projectionFailure);
            ensured = false;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        return true;
    }

    private void CompensateAcceptedMaterialOwnerOrThrow(
        bool ensured,
        string destinationId,
        string reason)
    {
        if (!ensured)
            return;
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                destinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Faction-contract accept failed ({reason}) and input-owner compensation failed: "
                + failureReason);
        }
    }

    private bool TryValidateLiveFactionDeliveryReceipt(
        string factionId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        if (!FactionContractMaterialRules.HasMaterialRequirements(contract))
            return true;
        if (!TryGetMatchingPendingReceipt(state, out _, out _))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        return true;
    }

    private bool TryGetMatchingPendingReceipt(
        FactionCampaignStateSaveData state,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
            || !factionContractTransfers.TryGetPending(
                state.deliveryOperationId,
                out receipt)
            || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                state,
                receipt))
        {
            failureReason =
                "faction-contract-delivery-receipt-missing-or-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryRetireFactionContractInputOwner(
        FactionCampaignStateSaveData state,
        out string failureReason)
    {
        if (state == null || !state.activeContractInputOwnerActive)
        {
            failureReason = string.Empty;
            return true;
        }
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out failureReason))
            return false;
        PublishFactionMutation(
            state.factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryMarkInputOwnerRetired(
                        state.factionId,
                        out mutationFailure),
            "faction-contract-input-owner-retire");
        return true;
    }

    private delegate bool FactionCandidateMutation(
        V20CampaignRuntime candidate,
        out string failure);

    private void PublishFactionMutation(
        string factionId,
        FactionCandidateMutation mutation,
        string operation)
    {
        V20CampaignRuntime candidate = CreateCampaignCandidate();
        if (!mutation(candidate, out string failure))
            throw new InvalidOperationException(
                $"{operation} rejected faction '{factionId}': {failure}");
        live.PublishFactions(live.PrepareFactions(candidate.CaptureFactions()));
    }

    private void PublishDeliveryFailure(string factionId, string reason)
    {
        FactionCampaignStateSaveData state = RequireLiveFaction(factionId);
        string canonical = reason?.Trim() ?? string.Empty;
        if (string.Equals(
                state.deliveryFailureReason,
                canonical,
                StringComparison.Ordinal))
            return;
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TrySetDeliveryFailure(
                        factionId,
                        canonical,
                        out mutationFailure),
            "faction-contract-delivery-status");
    }

    private FactionCampaignStateSaveData RequireLiveFaction(string factionId)
    {
        if (!live.TryGetFaction(factionId, out FactionCampaignStateSaveData state))
            throw new InvalidOperationException(
                $"Unknown faction contract campaign '{factionId}'.");
        return state;
    }

    private FactionContractView BuildFactionContractView(
        FactionContractDefinitionSO contract,
        V20ActiveEventSaveData occurrence,
        FactionCampaignStateSaveData state,
        IReadOnlyList<WorldItemStackSnapshot> physical,
        bool hasAdministration,
        bool hasDeliveryDropoff)
    {
        bool seasonal = !string.IsNullOrEmpty(contract.seasonalEventId);
        string occurrenceId = occurrence?.instanceId ?? string.Empty;
        bool lifecycleOccurrenceMatches = !seasonal
            || string.Equals(
                state.activeContractOccurrenceId,
                occurrenceId,
                StringComparison.Ordinal);
        bool active = lifecycleOccurrenceMatches
            && string.Equals(
                state.activeContractId,
                contract.StableId,
                StringComparison.Ordinal);
        bool acceptedThisOccurrence = !seasonal
            || string.Equals(
                    state.lastSeasonalContractId,
                    contract.StableId,
                    StringComparison.Ordinal)
                && string.Equals(
                    state.lastSeasonalOccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal);
        bool rewardPublished = acceptedThisOccurrence
            && state.completedContractIds.Contains(
            contract.StableId,
            StringComparer.Ordinal);
        bool deliveryForView = lifecycleOccurrenceMatches
            && string.Equals(
                state.deliveryContractId,
                contract.StableId,
                StringComparison.Ordinal);
        bool settlingCompletedDelivery = rewardPublished
            && deliveryForView
            && FactionContractDeliveryOutbox.HasPending(state);
        bool completed = rewardPublished && !settlingCompletedDelivery;
        bool failed = acceptedThisOccurrence
            && state.failedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal);
        bool terminalBusy = FactionContractDeliveryOutbox.HasPending(state)
            || state.deliveryTerminalCleanupPending;
        bool stateAllowsAccept = !active
            && !rewardPublished
            && !failed
            && string.IsNullOrWhiteSpace(state.activeContractId)
            && !terminalBusy;
        bool material = FactionContractMaterialRules.HasMaterialRequirements(
            contract);
        bool canAccept = stateAllowsAccept
            && hasAdministration
            && (!material || hasDeliveryDropoff);
        string disabledReason = string.Empty;
        if (!canAccept)
        {
            if (!stateAllowsAccept)
            {
                disabledReason = active
                    ? "진행 중"
                    : settlingCompletedDelivery
                        ? "보상 반영·인도 확인 중"
                        : completed
                            ? "완료됨"
                            : failed
                                ? "실패 이력"
                                : terminalBusy
                                    ? "이전 인도 정산 중"
                                    : "다른 계약 진행 중";
            }
            else
            {
                disabledReason = !hasAdministration
                    ? "운영 중인 행정 시설이 필요함"
                    : "유효한 물자 인도 지점이 필요함";
            }
        }

        string destinationId = active
            || deliveryForView
                ? state.activeContractDestinationId
                : string.Empty;
        IReadOnlyList<FactionContractItemProgressView> itemViews =
            FactionContractMaterialRules.BuildMaterialRequirements(contract)
                .Select(requirement =>
                {
                    int assigned = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State is WorldItemStackState.Loose
                            or WorldItemStackState.Stored
                            or WorldItemStackState.FacilityOutputBuffer);
                    int hauling = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State is WorldItemStackState.Carried
                            or WorldItemStackState.InTransit);
                    int arrived = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State ==
                            WorldItemStackState.FacilityBuffer);
                    int delivered = deliveryForView
                        && FactionContractDeliveryOutbox.HasPending(state)
                            ? requirement.Value
                            : rewardPublished
                                ? requirement.Value
                                : 0;
                    string displayName = items.CatalogProvider.TryGetDefinition(
                            requirement.Key,
                            out DungeonItemDefinition definition)
                        ? definition.DisplayName
                        : requirement.Key;
                    return new FactionContractItemProgressView(
                        requirement.Key,
                        displayName,
                        requirement.Value,
                        assigned,
                        hauling,
                        arrived,
                        delivered);
                })
                .ToArray();
        return new FactionContractView
        {
            ContractId = contract.StableId,
            OccurrenceId = occurrenceId,
            AcceptActionId = seasonal
                ? V21ContentAlertActionIds.SeasonalFactionContractAccept(
                    occurrenceId,
                    contract.StableId)
                : V21ContentAlertActionIds.FactionContractAccept(
                    contract.factionId,
                    contract.StableId),
            DisplayName = contract.DisplayName,
            Description = contract.Description,
            Kind = contract.kind,
            DeadlineDays = contract.deadlineDays,
            DeadlineAbsoluteDay = active
                ? state.activeContractDeadlineAbsoluteDay
                : occurrence?.deadlineAbsoluteDay ?? 0,
            IsActive = active,
            IsCompleted = completed,
            IsFailed = failed,
            CanAccept = canAccept,
            DisabledReason = disabledReason,
            EligibilityReason = canAccept
                ? "수락 가능 · 행정 시설의 관리 인장 사용 시 확정"
                : disabledReason,
            StatusReason = active
                || deliveryForView
                    ? state.deliveryFailureReason
                    : failed
                        ? "기한 만료 또는 계약 실패"
                    : string.Empty,
            CompletionConditions = FormatRequirements(
                contract.completionRequirements),
            SuccessEffects = FormatEffects(contract.successEffects),
            FailureEffects = FormatEffects(contract.failureEffects),
            Items = itemViews
        };
    }

    private IReadOnlyList<string> FormatRequirements(
        V20ContentRequirementSet source)
    {
        source ??= new V20ContentRequirementSet();
        List<string> lines = new();
        foreach (V20ItemAmountRequirement value in source.items
                     ?? new List<V20ItemAmountRequirement>())
        {
            if (value == null)
                continue;
            string itemName = items.CatalogProvider.TryGetDefinition(
                    value.itemDefinitionId,
                    out DungeonItemDefinition definition)
                ? definition.DisplayName
                : value.itemDefinitionId;
            lines.Add($"{itemName} {value.amount}개 · "
                + (value.consume ? "실물 인도" : "보유"));
        }
        foreach (V20FacilityRequirement value in source.facilities
                     ?? new List<V20FacilityRequirement>())
        {
            if (value == null)
                continue;
            string facility = !string.IsNullOrWhiteSpace(value.capabilityId)
                ? value.capabilityId
                : value.buildingDefinitionId;
            lines.Add($"시설 {facility} {value.minimumCount}개"
                + (value.mustBeOperational ? " · 운영 중" : string.Empty));
        }
        foreach (V20ResearchRequirement value in source.research
                     ?? new List<V20ResearchRequirement>())
        {
            if (value != null)
                lines.Add($"연구 #{value.researchNumericId} 완료");
        }
        foreach (V20CharacterRequirement value in source.characters
                     ?? new List<V20CharacterRequirement>())
        {
            if (value == null)
                continue;
            string traits = string.IsNullOrWhiteSpace(value.requiredTraitId)
                ? string.Empty
                : $" · 특성 {value.requiredTraitId}";
            string excluded = string.IsNullOrWhiteSpace(value.excludedTraitId)
                ? string.Empty
                : $" · 제외 특성 {value.excludedTraitId}";
            lines.Add($"인물 {value.minimumLifeStage}~{value.maximumLifeStage}"
                + $" · 건강 {value.minimumHealth} 이상{traits}{excluded}");
        }
        foreach (V20FactionRequirement value in source.factions
                     ?? new List<V20FactionRequirement>())
        {
            if (value != null)
            {
                lines.Add($"세력 {value.factionId} · 관계 {value.minimumRapport} 이상"
                    + $" · 불만 {value.maximumGrievance} 이하"
                    + $" · 의무 {value.minimumObligationTokens} 이상");
            }
        }
        foreach (V20WorldMetricRequirement value in source.worldMetrics
                     ?? new List<V20WorldMetricRequirement>())
        {
            if (value != null)
                lines.Add($"{value.kind} {value.minimumValue:0.##} 이상");
        }
        lines.AddRange((source.requiredFlags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"상태 {value} 필요"));
        lines.AddRange((source.excludedFlags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"상태 {value} 없어야 함"));
        return lines.Count > 0
            ? lines.AsReadOnly()
            : new[] { "추가 완료 조건 없음" };
    }

    private static IReadOnlyList<string> FormatEffects(
        IEnumerable<V20ContentEffect> source)
    {
        string[] lines = (source ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null && value.IsValid)
            .Select(value =>
            {
                string target = string.IsNullOrWhiteSpace(value.targetId)
                    ? string.Empty
                    : $" · 대상 {value.targetId}";
                string duration = value.durationDays > 0
                    ? $" · {value.durationDays}일"
                    : string.Empty;
                return $"{value.kind} {value.amount:+0.##;-0.##;0}"
                    + target
                    + duration;
            })
            .ToArray();
        return lines.Length > 0 ? lines : new[] { "없음" };
    }

    private int CountFactionContractQuantity(
        string destinationId,
        string itemId,
        Func<WorldItemStackSnapshot, bool> predicate) =>
        CountPhysical(
            items.GetAllStacks(),
            destinationId,
            itemId,
            predicate);

    private static int CountPhysical(
        IEnumerable<WorldItemStackSnapshot> source,
        string destinationId,
        string itemId,
        Func<WorldItemStackSnapshot, bool> predicate) =>
        string.IsNullOrWhiteSpace(destinationId)
            ? 0
            : (source ?? Array.Empty<WorldItemStackSnapshot>())
                .Where(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        itemId,
                        StringComparison.Ordinal)
                    && (predicate?.Invoke(stack) ?? true))
                .Sum(stack => stack.Quantity);

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
            case ContentResolutionRequestKind.GuestRequestDeliveryOutcome:
                succeeded = candidate.TryPublishGuestRequestDeliveryOutcome(
                    request.InstanceId,
                    request.ChoiceId,
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
                FactionContractDefinitionSO acceptedContract = catalog.Contracts
                    .FirstOrDefault(value => string.Equals(
                        value.StableId,
                        request.ContractId?.Trim(),
                        StringComparison.Ordinal)
                        && string.Equals(
                            value.factionId,
                            request.FactionId?.Trim(),
                            StringComparison.Ordinal));
                if (FactionContractMaterialRules.HasMaterialRequirements(
                        acceptedContract))
                {
                    if (!dropZones.TryGetDeliveryDropoff(
                            out Vector2Int contractDropoff))
                    {
                        failure = new DomainFailure(
                            FailureCode.ProductionOutputUnavailable);
                        return false;
                    }
                    FactionContractDeliveryTarget target = new(
                        EconomyProjectInputOwnerAuthority
                            .BuildFactionContractDestinationId(
                                acceptedContract.StableId),
                        contractDropoff);
                    succeeded = ((IFactionCampaignDeliveryCommand)candidate)
                        .TryAcceptContract(
                            request.FactionId,
                            request.ContractId,
                            request.AbsoluteDay,
                            target,
                            out rawFailure);
                }
                else
                {
                    succeeded = candidate.TryAcceptContract(
                        request.FactionId,
                        request.ContractId,
                        request.AbsoluteDay,
                        out rawFailure);
                }
                one = default;
                break;
            case ContentResolutionRequestKind.SeasonalFactionContractAccept:
                V20ActiveEventSaveData seasonalOccurrence = candidate
                    .ActiveSeasonalEvents.FirstOrDefault(value => value != null
                        && !value.resolved
                        && string.Equals(
                            value.instanceId,
                            request.InstanceId?.Trim(),
                            StringComparison.Ordinal));
                FactionContractDefinitionSO seasonalContract = catalog.Contracts
                    .FirstOrDefault(value => seasonalOccurrence != null
                        && string.Equals(
                            value.StableId,
                            request.ContractId?.Trim(),
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.factionId,
                            seasonalOccurrence.contextFactionId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.seasonalEventId,
                            seasonalOccurrence.definitionId,
                            StringComparison.Ordinal));
                if (seasonalContract == null
                    || !FactionContractMaterialRules.HasMaterialRequirements(
                        seasonalContract)
                    || !dropZones.TryGetDeliveryDropoff(
                        out Vector2Int seasonalDropoff))
                {
                    failure = new DomainFailure(
                        FailureCode.ProductionOutputUnavailable);
                    return false;
                }
                request.FactionId = seasonalContract.factionId;
                succeeded = ((IFactionCampaignDeliveryCommand)candidate)
                    .TryAcceptSeasonalContract(
                        seasonalOccurrence.instanceId,
                        seasonalContract.StableId,
                        new FactionContractDeliveryTarget(
                            FactionContractDeliveryOutbox.FormatDestinationId(
                                seasonalContract.StableId,
                                seasonalOccurrence.instanceId),
                            seasonalDropoff),
                        out rawFailure);
                one = default;
                break;
            case ContentResolutionRequestKind.FactionContractOutcome:
                bool deliveredMaterial = request.ContractSucceeded
                    && candidate.TryGetFaction(
                        request.FactionId,
                        out FactionCampaignStateSaveData deliveryState)
                    && FactionContractDeliveryOutbox.HasPending(deliveryState);
                succeeded = deliveredMaterial
                    ? ((IFactionCampaignDeliveryCommand)candidate)
                        .TryResolveDeliveredContract(
                            request.FactionId,
                            request.Requirements,
                            out one,
                            out rawFailure)
                    : candidate.TryResolveContract(
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
            failure = rawFailure?.StartsWith(
                    "BLOCKED_CONTRACT:",
                    StringComparison.Ordinal) == true
                ? new DomainFailure(
                    FailureCode.ServiceProcessContractMissing,
                    "wim040-incident-response",
                    rawFailure)
                : new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        resolved = (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept
            || request.Kind == ContentResolutionRequestKind.SocietyChoice
                && string.IsNullOrEmpty(one.DefinitionId))
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
            || (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept))
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
                resolved = string.Equals(
                        request.ChoiceId,
                        "fulfill",
                        StringComparison.Ordinal)
                    && GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(
                        guest)
                    ? GuestRequestDeliveryMaterialRules
                        .BuildNonDeliveryRequirements(guest)
                    : string.Equals(
                            request.ChoiceId,
                            "fulfill",
                            StringComparison.Ordinal)
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
        if (request.Kind ==
            ContentResolutionRequestKind.GuestRequestDeliveryOutcome)
        {
            V20ActiveEventSaveData active = live.ActiveSocietyEvents
                .FirstOrDefault(value => value != null && string.Equals(
                    value.instanceId,
                    request.InstanceId?.Trim(),
                    StringComparison.Ordinal));
            GuestRequestDefinitionSO guest = active == null
                ? null
                : ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                    as GuestRequestDefinitionSO;
            if (guest == null)
                return false;
            participants = active.participantCharacterIds.AsReadOnly();
            resolved = new V20ContentRequirementSet();
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
            ? FactionContractMaterialRules.HasMaterialRequirements(contract)
                ? new V20ContentRequirementSet()
                : contract.completionRequirements
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
        if (plan.MoneyDelta != 0
            && (money is not IIdempotentGameMoneyAccount
                || money is not IGameMoneyTransactionQuery))
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                "content-money-transaction-authority");
            return false;
        }
        if (plan.MoneyDelta < 0 && !money.CanSpend(-plan.MoneyDelta))
        {
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                (-plan.MoneyDelta).ToString());
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
        out DomainFailure failure,
        out V20ContentResolutionRecoveryPhase recoveryPhase,
        out EffectCommitReferences childCommits)
    {
        failure = DomainFailure.None;
        recoveryPhase = V20ContentResolutionRecoveryPhase.Prepared;
        childCommits = new EffectCommitReferences
        {
            ItemConsumptionOperationId = plan.ItemCosts.Count > 0
                ? plan.ReservationOwnerId
                : string.Empty,
            ItemGrantBatchCommitId = plan.Grants.Count > 0
                ? plan.CreateGrantPublicationPlan().BatchCommitId
                : string.Empty
        };
        PhysicalItemExactSourcePublicationTransaction grantTransaction = default;
        bool hasPreparedGrants = plan.Grants.Count > 0;
        if (hasPreparedGrants
            && !exactSources.TryPrepare(
                plan.CreateGrantPublicationPlan(),
                out grantTransaction,
                out _))
        {
            plan.Release(reservations);
            failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
            return false;
        }
        if (hasPreparedGrants
            && !string.Equals(
                grantTransaction.BatchCommitId,
                childCommits.ItemGrantBatchCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared content grant did not preserve its deterministic batch identity.");
        }
        EconomyTransactionContext transaction = new(
            plan.MoneyDelta >= 0
                ? EconomyTransactionKind.ContractIncome
                : EconomyTransactionKind.LegacyExpense,
            $"content:{request.ActionId}",
            description: request.Kind.ToString());
        IIdempotentGameMoneyAccount idempotentMoney =
            money as IIdempotentGameMoneyAccount;
        bool moneyCommitted = plan.MoneyDelta > 0
            ? idempotentMoney.TryCreditOnce(
                plan.MoneyDelta,
                transaction,
                out _)
            : plan.MoneyDelta < 0
                ? idempotentMoney.TrySpendOnce(
                    -plan.MoneyDelta,
                    transaction,
                    out _,
                    out _)
                : true;
        if (!moneyCommitted)
        {
            RollbackPreparedGrantsOrThrow(
                grantTransaction,
                hasPreparedGrants,
                "money-commit-failed");
            plan.Release(reservations);
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                (-plan.MoneyDelta).ToString());
            return false;
        }
        if (plan.MoneyDelta != 0)
        {
            if (money is not IGameMoneyTransactionQuery moneyQuery
                || !moneyQuery.TryGetSuccessfulTransaction(
                    transaction.kind,
                    transaction.sourceId,
                    out EconomyTransactionRecord moneyRecord)
                || moneyRecord == null
                || moneyRecord.amount != plan.MoneyDelta
                || string.IsNullOrWhiteSpace(moneyRecord.transactionId))
            {
                throw new InvalidOperationException(
                    "Committed content money effect is missing its exact transaction record.");
            }
            childCommits.MoneyTransactionId = moneyRecord.transactionId;
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
            RollbackPreparedGrantsOrThrow(
                grantTransaction,
                hasPreparedGrants,
                "item-consumption-failed");
            plan.Release(reservations);
            return false;
        }
        recoveryPhase = V20ContentResolutionRecoveryPhase.ItemCostsCommitted;
        PhysicalItemExactSourcePublicationReceipt grantReceipt = default;
        if (hasPreparedGrants
            && !exactSources.TryCommitReleased(
                grantTransaction,
                plan.Dropoff,
                "content-resolution-grant",
                out grantReceipt,
                out string grantCommitFailure))
        {
            throw new InvalidOperationException(
                "Content item costs committed but exact grant publication could not finalize: "
                + grantCommitFailure);
        }
        if (hasPreparedGrants
            && !string.Equals(
                grantReceipt.BatchCommitId,
                childCommits.ItemGrantBatchCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Committed content grant receipt changed its prepared batch identity.");
        }
        recoveryPhase = V20ContentResolutionRecoveryPhase.ItemGrantsCommitted;
        ApplyTypedDomainEffects(request.AbsoluteDay, resolutions);
        recoveryPhase = V20ContentResolutionRecoveryPhase.TypedEffectsApplied;
        return true;
    }

    private static bool RequiresForwardRecovery(
        EffectCommitPlan plan,
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.MoneyDelta != 0
            || plan.ItemCosts.Count > 0
            || plan.Grants.Count > 0)
        {
            return true;
        }

        return (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .SelectMany(value => value.Effects)
            .Any(effect => effect != null && effect.kind is
                V20ContentEffectKind.Health
                or V20ContentEffectKind.Trauma
                or V20ContentEffectKind.DiseaseExposure
                or V20ContentEffectKind.AmbitionProgress);
    }

    private void RollbackPreparedGrantsOrThrow(
        PhysicalItemExactSourcePublicationTransaction transaction,
        bool prepared,
        string reasonCode)
    {
        if (!prepared)
            return;
        if (!exactSources.TryRollback(
                transaction,
                reasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Prepared content grant rollback failed: " + failureReason);
        }
    }

    private static IReadOnlyList<V20ResolvedEventResult>
        AttachApplicationEvidence(
            IReadOnlyList<V20ResolvedEventResult> resolutions,
            EffectOwnerSnapshot before,
            EffectOwnerSnapshot after,
            EffectCommitReferences childCommits)
    {
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));
        return (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .Select(resolution => resolution.WithApplications(
                CreateApplicationEvidence(
                    resolution,
                    before,
                    after,
                    childCommits)))
            .ToArray();
    }

    private static IReadOnlyList<V20EffectApplicationEvidence>
        CreateApplicationEvidence(
            V20ResolvedEventResult resolution,
            EffectOwnerSnapshot before,
            EffectOwnerSnapshot after,
            EffectCommitReferences childCommits)
    {
        var result = new List<V20EffectApplicationEvidence>();
        string[] participants = resolution.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        for (int effectIndex = 0;
             effectIndex < resolution.Effects.Count;
             effectIndex++)
        {
            V20ContentEffect effect = resolution.Effects[effectIndex];
            if (effect == null || !effect.IsValid)
                continue;
            string childCommitId = childCommits?.ChildCommitId(effect.kind)
                ?? string.Empty;
            if (RequiresCharacter(effect.kind)
                || effect.kind == V20ContentEffectKind.RetirementSchedule)
            {
                foreach (string participantId in participants)
                {
                    bool targetExists = HasCharacterOwner(before, participantId)
                        || effect.kind == V20ContentEffectKind.RetirementSchedule
                            && HasCareer(before, participantId);
                    string beforeValue = CharacterEffectValue(
                        before,
                        resolution,
                        effect,
                        participantId,
                        participants);
                    string afterValue = CharacterEffectValue(
                        after,
                        resolution,
                        effect,
                        participantId,
                        participants);
                    float appliedAmount = ResolveCharacterAppliedAmount(
                        effect,
                        before,
                        after,
                        participantId,
                        beforeValue,
                        afterValue);
                    result.Add(new V20EffectApplicationEvidence
                    {
                        effectIndex = effectIndex,
                        targetId = participantId,
                        status = ResolveCharacterApplicationStatus(
                            effect,
                            targetExists,
                            appliedAmount,
                            beforeValue,
                            afterValue),
                        requestedAmount = effect.amount,
                        appliedAmount = appliedAmount,
                        beforeValue = beforeValue,
                        afterValue = afterValue,
                        semantic = EffectSemantic(effect.kind),
                        childCommitId = childCommitId
                    });
                }
                continue;
            }

            string actualTarget = ResolveGlobalEffectTarget(
                resolution,
                effect,
                before,
                after);
            string globalBefore = GlobalEffectValue(
                resolution,
                effect,
                actualTarget,
                before);
            string globalAfter = GlobalEffectValue(
                resolution,
                effect,
                actualTarget,
                after);
            float globalApplied = ResolveGlobalAppliedAmount(
                effect,
                globalBefore,
                globalAfter);
            result.Add(new V20EffectApplicationEvidence
            {
                effectIndex = effectIndex,
                targetId = actualTarget,
                status = string.Equals(
                        globalBefore,
                        globalAfter,
                        StringComparison.Ordinal)
                    ? V20EffectApplicationStatus.NoOp
                    : V20EffectApplicationStatus.Applied,
                requestedAmount = effect.amount,
                appliedAmount = globalApplied,
                beforeValue = globalBefore,
                afterValue = globalAfter,
                semantic = EffectSemantic(effect.kind),
                childCommitId = childCommitId
            });
        }
        return result.AsReadOnly();
    }

    private static V20EffectApplicationStatus ResolveCharacterApplicationStatus(
        V20ContentEffect effect,
        bool targetExists,
        float appliedAmount,
        string beforeValue,
        string afterValue)
    {
        if (!targetExists)
            return V20EffectApplicationStatus.Skipped;
        bool changed = !string.Equals(
            beforeValue,
            afterValue,
            StringComparison.Ordinal);
        if (effect.kind == V20ContentEffectKind.DiseaseExposure && !changed)
            return V20EffectApplicationStatus.Skipped;
        float expected = effect.kind switch
        {
            V20ContentEffectKind.SkillExperience =>
                Math.Max(0, Mathf.RoundToInt(effect.amount)),
            V20ContentEffectKind.Relationship =>
                Mathf.Clamp(effect.amount / 10f, -1f, 1f),
            V20ContentEffectKind.DiseaseExposure =>
                Math.Max(0.1f, Math.Abs(effect.amount)),
            V20ContentEffectKind.AmbitionProgress =>
                Mathf.RoundToInt(effect.amount),
            V20ContentEffectKind.RetirementSchedule => effect.durationDays,
            _ => effect.amount
        };
        if (!Mathf.Approximately(appliedAmount, expected))
            return V20EffectApplicationStatus.Clamped;
        return changed
            ? V20EffectApplicationStatus.Applied
            : V20EffectApplicationStatus.NoOp;
    }

    private static float ResolveCharacterAppliedAmount(
        V20ContentEffect effect,
        EffectOwnerSnapshot before,
        EffectOwnerSnapshot after,
        string participantId,
        string beforeValue,
        string afterValue)
    {
        switch (effect.kind)
        {
            case V20ContentEffectKind.Mood:
                return MoodFactorValue(after, participantId, effect)
                    - MoodFactorValue(before, participantId, effect);
            case V20ContentEffectKind.Trauma:
                return TraumaValue(after, participantId)
                    - TraumaValue(before, participantId);
            case V20ContentEffectKind.SkillExperience:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : Math.Max(0, Mathf.RoundToInt(effect.amount));
            case V20ContentEffectKind.Health:
                return HealthValue(after, participantId)
                    - HealthValue(before, participantId);
            case V20ContentEffectKind.Relationship:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : Mathf.Clamp(effect.amount / 10f, -1f, 1f);
            case V20ContentEffectKind.DiseaseExposure:
                return ExposureValue(after, participantId, effect.targetId)
                    - ExposureValue(before, participantId, effect.targetId);
            case V20ContentEffectKind.AmbitionProgress:
                return AmbitionProgress(after, participantId)
                    - AmbitionProgress(before, participantId);
            case V20ContentEffectKind.RetirementSchedule:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : effect.durationDays;
            default:
                return 0f;
        }
    }

    private static string CharacterEffectValue(
        EffectOwnerSnapshot snapshot,
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        string participantId,
        IReadOnlyList<string> participants)
    {
        if (!HasCharacterOwner(snapshot, participantId)
            && effect.kind != V20ContentEffectKind.RetirementSchedule)
            return "unavailable";
        return effect.kind switch
        {
            V20ContentEffectKind.Mood =>
                FormatFloat(MoodFactorValue(snapshot, participantId, effect)),
            V20ContentEffectKind.Trauma =>
                FormatFloat(TraumaValue(snapshot, participantId)),
            V20ContentEffectKind.SkillExperience =>
                ProgressionValue(snapshot, participantId),
            V20ContentEffectKind.Health =>
                FormatFloat(HealthValue(snapshot, participantId)),
            V20ContentEffectKind.Relationship =>
                RelationshipValue(snapshot, participantId, participants),
            V20ContentEffectKind.DiseaseExposure =>
                DiseaseExposureValue(
                    snapshot,
                    participantId,
                    effect.targetId),
            V20ContentEffectKind.AmbitionProgress =>
                AmbitionValue(snapshot, participantId),
            V20ContentEffectKind.RetirementSchedule =>
                RetirementValue(snapshot, participantId),
            _ => "unavailable"
        };
    }

    private static string ResolveGlobalEffectTarget(
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        EffectOwnerSnapshot before,
        EffectOwnerSnapshot after)
    {
        if (effect.kind == V20ContentEffectKind.Money)
            return "treasury";
        if (effect.kind is V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation)
        {
            string changed = (after.Factions?.factions
                    ?? new List<FactionCampaignStateSaveData>())
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.factionId)
                .FirstOrDefault(factionId => !string.Equals(
                    FactionValue(before, factionId, effect.kind),
                    FactionValue(after, factionId, effect.kind),
                    StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(changed))
                return changed;
            if (!string.IsNullOrWhiteSpace(effect.targetId))
                return effect.targetId;
            if (!string.IsNullOrWhiteSpace(resolution.ContextFactionId))
                return resolution.ContextFactionId;
        }
        if (effect.kind == V20ContentEffectKind.WorkDelayDays)
        {
            string changed = (after.Society?.workDelays
                    ?? new List<V20WorkDelaySaveData>())
                .Where(value => value != null)
                .Select(value => value.scopeId)
                .FirstOrDefault(scope => !string.Equals(
                    WorkDelayValue(before, scope),
                    WorkDelayValue(after, scope),
                    StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(changed))
                return changed;
        }
        return string.IsNullOrWhiteSpace(effect.targetId)
            ? "content-resolution"
            : effect.targetId;
    }

    private static string GlobalEffectValue(
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        string targetId,
        EffectOwnerSnapshot snapshot) => effect.kind switch
    {
        V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation =>
            FactionValue(snapshot, targetId, effect.kind),
        V20ContentEffectKind.Money => snapshot.MoneyBalance.ToString(
            System.Globalization.CultureInfo.InvariantCulture),
        V20ContentEffectKind.ItemGrant
            or V20ContentEffectKind.ItemConsume =>
            ItemQuantity(snapshot, targetId).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
        V20ContentEffectKind.WorldFlag =>
            BoolValue(snapshot.Milestones?.worldFlags?.Contains(
                targetId,
                StringComparer.Ordinal) == true),
        V20ContentEffectKind.WorkDelayDays => WorkDelayValue(snapshot, targetId),
        V20ContentEffectKind.Threat
            or V20ContentEffectKind.MilestonePressure =>
            BoolValue(snapshot.Milestones?.activePressureIds?.Contains(
                targetId,
                StringComparer.Ordinal) == true),
        _ => "unavailable"
    };

    private static float ResolveGlobalAppliedAmount(
        V20ContentEffect effect,
        string beforeValue,
        string afterValue)
    {
        if (effect.kind is V20ContentEffectKind.WorldFlag
            or V20ContentEffectKind.Threat
            or V20ContentEffectKind.MilestonePressure)
            return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                ? 0f
                : 1f;
        if (effect.kind == V20ContentEffectKind.ItemConsume)
            return Mathf.RoundToInt(effect.amount);
        if (effect.kind is V20ContentEffectKind.ItemGrant
            or V20ContentEffectKind.Money
            or V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation
            or V20ContentEffectKind.WorkDelayDays)
            return Mathf.RoundToInt(effect.amount);
        return effect.amount;
    }

    private static string EffectSemantic(V20ContentEffectKind kind) => kind switch
    {
        V20ContentEffectKind.Mood => "mood-factor",
        V20ContentEffectKind.Trauma => "trauma",
        V20ContentEffectKind.SkillExperience => "skill-experience",
        V20ContentEffectKind.Health => "health-change-not-pressure",
        V20ContentEffectKind.Relationship => "relationship-sentiment",
        V20ContentEffectKind.FactionRapport => "faction-rapport",
        V20ContentEffectKind.FactionGrievance => "faction-grievance",
        V20ContentEffectKind.FactionObligation => "faction-obligation",
        V20ContentEffectKind.Money => "treasury-balance",
        V20ContentEffectKind.ItemGrant => "item-grant",
        V20ContentEffectKind.ItemConsume => "item-consumption",
        V20ContentEffectKind.WorldFlag => "world-flag",
        V20ContentEffectKind.WorkDelayDays => "work-delay",
        V20ContentEffectKind.Threat => "pressure-activated-not-health-damage",
        V20ContentEffectKind.DiseaseExposure =>
            "exposure-pending-not-infection",
        V20ContentEffectKind.AmbitionProgress => "ambition-progress",
        V20ContentEffectKind.MilestonePressure =>
            "pressure-activated-not-health-damage",
        V20ContentEffectKind.RetirementSchedule =>
            "retirement-scheduled-not-character-exit",
        _ => "unsupported"
    };

    private static bool HasCharacterOwner(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Characters?.Any(value =>
        value?.Actor?.Identity != null
        && string.Equals(
            value.Actor.Identity.PersistentId,
            characterId,
            StringComparison.Ordinal)) == true;

    private static bool HasCareer(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Careers?.characters?.Any(value =>
        value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal)) == true;

    private static CharacterEffectOwnerSnapshot CharacterOwner(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Characters?.FirstOrDefault(value =>
        value?.Actor?.Identity != null
        && string.Equals(
            value.Actor.Identity.PersistentId,
            characterId,
            StringComparison.Ordinal));

    private static float MoodFactorValue(
        EffectOwnerSnapshot snapshot,
        string characterId,
        V20ContentEffect effect)
    {
        string factorSuffix = ":" + effect.targetId;
        CharacterMoodMemory factor = CharacterOwner(snapshot, characterId)
            ?.Stats?.Factors?.FirstOrDefault(value => value != null
                && value.Id.StartsWith("content:", StringComparison.Ordinal)
                && value.Id.EndsWith(factorSuffix, StringComparison.Ordinal));
        return factor?.TotalValue ?? 0f;
    }

    private static float HealthValue(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.BodyHealth?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.currentHealth ?? 0f;

    private static float TraumaValue(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Psychosocial?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.trauma ?? 0f;

    private static string ProgressionValue(
        EffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterProgressionSnapshot value = CharacterOwner(snapshot, characterId)
            ?.Progression;
        return value == null
            ? "unavailable"
            : "level=" + value.Level + ",xp=" + value.CurrentExperience;
    }

    private static string RelationshipValue(
        EffectOwnerSnapshot snapshot,
        string characterId,
        IReadOnlyList<string> participants)
    {
        CharacterSocialMemorySnapshot social = CharacterOwner(snapshot, characterId)
            ?.Social;
        if (social == null)
            return "unavailable";
        return string.Join(",", participants
            .Where(value => !string.Equals(
                value,
                characterId,
                StringComparison.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(other => other + "=" + FormatFloat(
                social.characterSentiments?
                    .FirstOrDefault(value => value != null && string.Equals(
                        value.key,
                        other,
                        StringComparison.Ordinal))?.value ?? 0f)));
    }

    private static float ExposureValue(
        EffectOwnerSnapshot snapshot,
        string characterId,
        string diseaseId) => snapshot?.PopulationHealth?.pendingExposures
        ?.FirstOrDefault(value => value != null
            && string.Equals(value.characterId, characterId, StringComparison.Ordinal)
            && string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal))
        ?.weightedExposureHours ?? 0f;

    private static string DiseaseExposureValue(
        EffectOwnerSnapshot snapshot,
        string characterId,
        string diseaseId)
    {
        bool infected = snapshot?.PopulationHealth?.characters
            ?.FirstOrDefault(value => value != null && string.Equals(
                value.characterId,
                characterId,
                StringComparison.Ordinal))?.activeDiseases?.Any(value =>
                value != null && string.Equals(
                    value.diseaseId,
                    diseaseId,
                    StringComparison.Ordinal)) == true;
        return "exposureHours="
            + FormatFloat(ExposureValue(snapshot, characterId, diseaseId))
            + ",infected=" + BoolValue(infected);
    }

    private static int AmbitionProgress(
        EffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Narrative?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.ambitionProgress ?? 0;

    private static string AmbitionValue(
        EffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterNarrativeSaveData value = snapshot?.Narrative?.characters
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.characterId,
                characterId,
                StringComparison.Ordinal));
        return value == null
            ? "unavailable"
            : "id=" + (value.activeAmbitionId ?? string.Empty)
                + ",status=" + value.ambitionStatus
                + ",progress=" + value.ambitionProgress;
    }

    private static string RetirementValue(
        EffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterCareerSaveData value = snapshot?.Careers?.characters
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.characterId,
                characterId,
                StringComparison.Ordinal));
        return value == null
            ? "unavailable"
            : "status=" + value.retirementScheduleStatus
                + ",due=" + value.retirementDueAbsoluteDay
                + ",terminal=" + value.retirementTerminalAbsoluteDay
                + ",retired=" + BoolValue(value.retired);
    }

    private static string FactionValue(
        EffectOwnerSnapshot snapshot,
        string factionId,
        V20ContentEffectKind kind)
    {
        FactionCampaignStateSaveData value = snapshot?.Factions?.factions
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.factionId,
                factionId,
                StringComparison.Ordinal));
        if (value == null)
            return "unavailable";
        return (kind switch
        {
            V20ContentEffectKind.FactionRapport => value.rapport,
            V20ContentEffectKind.FactionGrievance => value.grievance,
            V20ContentEffectKind.FactionObligation => value.obligationTokens,
            _ => 0
        }).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int ItemQuantity(
        EffectOwnerSnapshot snapshot,
        string itemId) => snapshot?.Items?.stacks?
        .Where(value => value != null && string.Equals(
            value.itemId,
            itemId,
            StringComparison.Ordinal))
        .Sum(value => Math.Max(0, value.quantity)) ?? 0;

    private static string WorkDelayValue(
        EffectOwnerSnapshot snapshot,
        string scopeId) => (snapshot?.Society?.workDelays
            ?.FirstOrDefault(value => value != null && string.Equals(
                value.scopeId,
                scopeId,
                StringComparison.Ordinal))?.untilAbsoluteDayExclusive ?? 0)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFloat(float value) => value.ToString(
        "R",
        System.Globalization.CultureInfo.InvariantCulture);

    private static string BoolValue(bool value) => value ? "true" : "false";

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
        }

        public string ReservationOwnerId { get; }
        public int MoneyDelta { get; set; }
        public Vector2Int Dropoff { get; set; }
        public List<ReservedItemConsumption> ItemCosts { get; } = new();
        public List<V20ContentEffect> Grants { get; } = new();

        public PhysicalItemExactSourcePublicationPlan CreateGrantPublicationPlan()
        {
            FacilityBufferPlannedOutputSlice[] outputs = Grants
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.ItemGrant)
                .GroupBy(value => value.targetId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select((group, index) =>
                {
                    int quantity = group.Sum(value =>
                        Math.Max(0, Mathf.RoundToInt(value.amount)));
                    ItemDefinitionId itemId = (ItemDefinitionId)group.Key;
                    return new FacilityBufferPlannedOutputSlice(
                        $"grant:{index:D4}:{group.Key}",
                        PhysicalItemMassSubject.ForDefinition(itemId),
                        quantity);
                })
                .ToArray();
            return new PhysicalItemExactSourcePublicationPlan(
                "run.v20-grant",
                ReservationOwnerId,
                Dropoff,
                outputs);
        }

        public void Release(IItemReservationService service)
        {
            foreach (ReservedItemConsumption cost in ItemCosts)
                service.Release(cost.StackId, ReservationOwnerId);
        }
    }
}
