using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryAggregateState
{
    public Dictionary<WildlifeInstanceId, HusbandryAnimalState> Animals { get; } =
        new();
    public Dictionary<BuildingInstanceId, AnimalPenPolicyData> Policies { get; } =
        new();
    public List<AnimalHusbandryPendingOutcomeState> PendingOutcomes { get; } =
        new();
    public List<AnimalHusbandryPendingCommandFact> PendingCommandFacts { get; } = new();
    public long NextOutcomeSequence { get; set; } = 1L;
    public float NextTickAt { get; set; }
}

public sealed class AnimalHusbandryPendingCommandFact
{
    public AnimalHusbandryPendingCommandFact(string publicationKey, AnimalHusbandryCommandFact fact)
    {
        if (string.IsNullOrWhiteSpace(publicationKey) || publicationKey != publicationKey.Trim()
            || publicationKey.Length > 512 || publicationKey.Any(char.IsControl))
            throw new InvalidOperationException("A canonical pending husbandry publication key is required.");
        PublicationKey = publicationKey;
        Fact = fact ?? throw new ArgumentNullException(nameof(fact));
    }
    public string PublicationKey { get; }
    public AnimalHusbandryCommandFact Fact { get; }
    public AnimalHusbandryPendingCommandSaveData Capture() => new()
    { publicationKey = PublicationKey, fact = Fact.CaptureSaveData() };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryRestoreCandidate
{
    public AnimalHusbandryRestoreCandidate(AnimalHusbandryAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public AnimalHusbandryAggregateState State { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class AnimalHusbandryStateCodec
{
    public static AnimalHusbandryRestoreCandidate BuildRestore(
        DungeonAnimalHusbandrySaveData saveData,
        float nextTickAt,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (saveData == null)
        {
            throw new InvalidOperationException("Animal-husbandry payload is null.");
        }
        if (saveData.version != DungeonAnimalHusbandrySaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Animal-husbandry payload version {saveData.version} is unsupported.");
        }
        if (saveData.animals == null || saveData.penPolicies == null
            || saveData.pendingOutcomes == null || saveData.pendingCommandFacts == null
            || saveData.pendingCommandFacts.Count > 1)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload collections must not be null.");
        }
        if (getSpeciesProducts == null || containsItem == null)
        {
            throw new ArgumentNullException(
                getSpeciesProducts == null ? nameof(getSpeciesProducts) : nameof(containsItem));
        }

        AnimalHusbandryAggregateState restored = new()
        {
            NextTickAt = nextTickAt,
            NextOutcomeSequence = saveData.nextOutcomeSequence
        };
        if (restored.NextOutcomeSequence <= 0L)
        {
            throw new InvalidOperationException(
                "Animal-husbandry outcome sequence must be positive.");
        }
        WildlifeInstanceId previousAnimalId = default;
        foreach (HusbandryAnimalSaveData saved in saveData.animals)
        {
            HusbandryAnimalState state = BuildAnimal(saved, getSpeciesProducts, containsItem);
            if (previousAnimalId.IsValid
                && string.CompareOrdinal(previousAnimalId.Value, state.AnimalId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    "Animal-husbandry animals must be uniquely sorted by canonical instance ID.");
            }
            if (!restored.Animals.TryAdd(state.AnimalId, state))
            {
                throw new InvalidOperationException(
                    $"Duplicate husbandry animal '{state.AnimalId.Value}'.");
            }
            previousAnimalId = state.AnimalId;
        }

        BuildingInstanceId previousPenId = default;
        foreach (AnimalPenPolicySaveData saved in saveData.penPolicies)
        {
            AnimalPenPolicyData policy = BuildPolicy(saved, getSpeciesProducts);
            if (previousPenId.IsValid
                && string.CompareOrdinal(previousPenId.Value, policy.PenId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    "Animal-husbandry pen policies must be uniquely sorted by canonical building ID.");
            }
            if (!restored.Policies.TryAdd(policy.PenId, policy))
            {
                throw new InvalidOperationException(
                    $"Duplicate husbandry pen policy '{policy.PenId.Value}'.");
            }
            previousPenId = policy.PenId;
        }

        string previousOperationId = null;
        foreach (AnimalHusbandryPendingOutcomeSaveData saved in
                 saveData.pendingOutcomes)
        {
            AnimalHusbandryPendingOutcomeState pending = BuildPendingOutcome(
                saved,
                getSpeciesProducts,
                containsItem);
            if (previousOperationId != null
                && string.CompareOrdinal(
                    previousOperationId,
                    pending.OperationId) >= 0)
            {
                throw new InvalidOperationException(
                    "Animal-husbandry pending outcomes must be uniquely sorted by operation ID.");
            }
            if (AnimalHusbandryDomainOperationIdentity.RequireSequence(pending.OperationId,
                pending.Kind, pending.AnimalId.Value) >= restored.NextOutcomeSequence)
                throw new InvalidOperationException("Husbandry pending work sequence conflicts with its owner.");
            restored.PendingOutcomes.Add(pending);
            previousOperationId = pending.OperationId;
        }

        foreach (AnimalHusbandryPendingCommandSaveData saved in saveData.pendingCommandFacts)
        {
            if (saved == null || saved.fact == null)
                throw new InvalidOperationException("Husbandry pending command fact is missing.");
            AnimalHusbandryCommandFact fact;
            try { fact = AnimalHusbandryCommandFact.Restore(saved.fact, getSpeciesProducts, containsItem); }
            catch (ArgumentException exception)
            { throw new InvalidOperationException("Husbandry pending command fact is invalid.", exception); }
            if (fact.Sequence >= restored.NextOutcomeSequence
                || restored.PendingOutcomes.Any(value => value.OperationId == fact.OperationId))
                throw new InvalidOperationException("Husbandry pending command sequence conflicts with its owner.");
            restored.PendingCommandFacts.Add(new AnimalHusbandryPendingCommandFact(saved.publicationKey, fact));
        }

        ValidateAggregateReferences(restored);
        return new AnimalHusbandryRestoreCandidate(restored);
    }

    public static DungeonAnimalHusbandrySaveData Capture(
        AnimalHusbandryAggregateState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return new DungeonAnimalHusbandrySaveData
        {
            nextOutcomeSequence = state.NextOutcomeSequence,
            animals = state.Animals.Values
                .OrderBy(value => value.AnimalId.Value, StringComparer.Ordinal)
                .Select(CaptureAnimal)
                .ToList(),
            penPolicies = state.Policies.Values
                .OrderBy(value => value.PenId.Value, StringComparer.Ordinal)
                .Select(CapturePolicy)
                .ToList(),
            pendingOutcomes = state.PendingOutcomes
                .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                .Select(CapturePendingOutcome)
                .ToList(),
            pendingCommandFacts = state.PendingCommandFacts.Select(value => value.Capture()).ToList()
        };
    }

    internal static HusbandryAnimalState BuildAnimal(
        HusbandryAnimalSaveData saved,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload contains a null animal.");
        }

        WildlifeInstanceId animalId = new(saved.animalInstanceId);
        WildlifeSpeciesId speciesId = new(saved.speciesDefinitionId);
        BuildingInstanceId penId = new(saved.penBuildingInstanceId);
        RequireCanonical(animalId.Value, saved.animalInstanceId, animalId.IsValid, "animal instance");
        RequireCanonical(speciesId.Value, saved.speciesDefinitionId, speciesId.IsValid, "species definition");
        RequireCanonical(penId.Value, saved.penBuildingInstanceId, penId.IsValid, "pen building instance");
        IReadOnlyCollection<ItemDefinitionId> authoredProducts =
            getSpeciesProducts(speciesId);
        if (authoredProducts == null)
        {
            throw new InvalidOperationException(
                $"Unknown authored wildlife species '{speciesId.Value}'.");
        }
        if (!Enum.IsDefined(typeof(AnimalSex), saved.sex)
            || !Enum.IsDefined(typeof(AnimalHusbandryWorkKind), saved.pendingWorkKind)
            || !Enum.IsDefined(typeof(AnimalHusbandryStatusCode), saved.statusCode))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' contains an unknown enum value.");
        }
        RequireFiniteNonNegative(saved.ageDays, animalId, "age");
        RequireFiniteRange(saved.tamingProgress, 0f, 1f, animalId, "taming progress");
        RequireFiniteNonNegative(saved.pregnancyProgressDays, animalId, "pregnancy progress");
        RequireFiniteNonNegative(saved.breedingCooldownDays, animalId, "breeding cooldown");
        RequireFiniteNonNegative(saved.manureProgressDays, animalId, "manure progress");
        RequireFiniteNonNegative(saved.pendingWorkCompleted, animalId, "pending work");
        if (saved.readyManureCycles < 0
            || saved.pendingWorkContributions == null
            || saved.products == null
            || saved.statusParameters == null
            || saved.statusParameters.Any(parameter => parameter == null))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' contains invalid progress or status data.");
        }

        WildlifeInstanceId otherParentId = new(saved.otherParentAnimalInstanceId);
        if (otherParentId.Value.Length > 0)
        {
            RequireCanonical(
                otherParentId.Value,
                saved.otherParentAnimalInstanceId,
                otherParentId.IsValid,
                "other-parent animal instance");
        }

        ItemDefinitionId pendingProductId = new(saved.pendingProductItemDefinitionId);
        if (pendingProductId.IsValid && !containsItem(pendingProductId))
        {
            throw new InvalidOperationException(
                $"Unknown authored item '{pendingProductId.Value}'.");
        }
        if (saved.pendingWorkKind == AnimalHusbandryWorkKind.CollectProduct
            != pendingProductId.IsValid)
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' has inconsistent pending product work.");
        }

        HashSet<ItemDefinitionId> productIds = new();
        List<AnimalProductProgressState> products = new(saved.products.Count);
        string previousProductId = null;
        foreach (AnimalProductProgressSaveData product in saved.products)
        {
            if (product == null)
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' contains a null product progress record.");
            }
            ItemDefinitionId itemId = new(product.itemDefinitionId);
            RequireCanonical(itemId.Value, product.itemDefinitionId, itemId.IsValid, "product item definition");
            if (!containsItem(itemId))
            {
                throw new InvalidOperationException(
                    $"Unknown authored item '{itemId.Value}'.");
            }
            if (!authoredProducts.Contains(itemId))
            {
                throw new InvalidOperationException(
                    $"Item '{itemId.Value}' is not an authored husbandry product of species '{speciesId.Value}'.");
            }
            if (previousProductId != null
                && string.CompareOrdinal(previousProductId, itemId.Value) >= 0
                || !productIds.Add(itemId))
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' product IDs must be unique and sorted.");
            }
            RequireFiniteNonNegative(product.progressDays, animalId, "product progress");
            if (product.readyCycles < 0)
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' has a negative ready product count.");
            }
            products.Add(new AnimalProductProgressState
            {
                ItemId = itemId,
                ProgressDays = product.progressDays,
                ReadyCycles = product.readyCycles
            });
            previousProductId = itemId.Value;
        }

        if (pendingProductId.IsValid && !productIds.Contains(pendingProductId))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' pending product is absent from its product state.");
        }

        List<CraftContributionSaveData> contributions =
            BuildContributions(saved, animalId);

        return new HusbandryAnimalState
        {
            AnimalId = animalId,
            SpeciesId = speciesId,
            PenId = penId,
            Sex = saved.sex,
            AgeDays = saved.ageDays,
            Tamed = saved.tamed,
            TamingProgress = saved.tamingProgress,
            Pregnant = saved.pregnant,
            PregnancyProgressDays = saved.pregnancyProgressDays,
            OtherParentId = otherParentId,
            BreedingCooldownDays = saved.breedingCooldownDays,
            ManureProgressDays = saved.manureProgressDays,
            ReadyManureCycles = saved.readyManureCycles,
            SlaughterDesignated = saved.slaughterDesignated,
            AutoSlaughterDesignated = saved.autoSlaughterDesignated,
            PendingWorkKind = saved.pendingWorkKind,
            PendingProductItemId = pendingProductId,
            PendingWorkCompleted = saved.pendingWorkCompleted,
            PendingWorkContributions = contributions,
            StatusCode = saved.statusCode,
            StatusParameters = new List<string>(saved.statusParameters),
            Products = products
        };
    }

    internal static AnimalPenPolicyData BuildPolicy(
        AnimalPenPolicySaveData saved,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts)
    {
        if (saved == null || saved.allowedSpeciesDefinitionIds == null)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload contains a null pen policy or species list.");
        }
        BuildingInstanceId penId = new(saved.penBuildingInstanceId);
        RequireCanonical(penId.Value, saved.penBuildingInstanceId, penId.IsValid, "pen building instance");
        if (saved.maximumAnimals < 1
            || saved.adultFemaleLimit < 0
            || saved.adultMaleLimit < 0
            || saved.juvenileLimit < 0
            || saved.minimumBreedingFemales < 0
            || saved.minimumBreedingMales < 0)
        {
            throw new InvalidOperationException(
                $"Pen '{penId.Value}' contains invalid population limits.");
        }

        List<WildlifeSpeciesId> allowedSpecies = new(saved.allowedSpeciesDefinitionIds.Count);
        string previousSpeciesId = null;
        foreach (string rawSpeciesId in saved.allowedSpeciesDefinitionIds)
        {
            WildlifeSpeciesId speciesId = new(rawSpeciesId);
            RequireCanonical(speciesId.Value, rawSpeciesId, speciesId.IsValid, "allowed species definition");
            if (getSpeciesProducts(speciesId) == null)
            {
                throw new InvalidOperationException(
                    $"Unknown authored wildlife species '{speciesId.Value}'.");
            }
            if (previousSpeciesId != null
                && string.CompareOrdinal(previousSpeciesId, speciesId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    $"Pen '{penId.Value}' allowed species must be unique and sorted.");
            }
            allowedSpecies.Add(speciesId);
            previousSpeciesId = speciesId.Value;
        }

        return new AnimalPenPolicyData
        {
            PenId = penId,
            AllowedSpeciesIds = allowedSpecies,
            allowHerbivores = saved.allowHerbivores,
            allowOmnivores = saved.allowOmnivores,
            allowCarnivores = saved.allowCarnivores,
            allowScavengers = saved.allowScavengers,
            allowFemales = saved.allowFemales,
            allowMales = saved.allowMales,
            allowJuveniles = saved.allowJuveniles,
            maximumAnimals = saved.maximumAnimals,
            breedingAllowed = saved.breedingAllowed,
            protectPregnant = saved.protectPregnant,
            allowRiskyMixing = saved.allowRiskyMixing,
            adultFemaleLimit = saved.adultFemaleLimit,
            adultMaleLimit = saved.adultMaleLimit,
            juvenileLimit = saved.juvenileLimit,
            minimumBreedingFemales = saved.minimumBreedingFemales,
            minimumBreedingMales = saved.minimumBreedingMales
        };
    }

    private static void ValidateAggregateReferences(
        AnimalHusbandryAggregateState state)
    {
        foreach (HusbandryAnimalState animal in state.Animals.Values)
        {
            if (!state.Policies.ContainsKey(animal.PenId))
            {
                throw new InvalidOperationException(
                    $"Animal '{animal.AnimalId.Value}' references pen '{animal.PenId.Value}' without a saved policy.");
            }
            if (animal.OtherParentId.IsValid
                && (!state.Animals.TryGetValue(animal.OtherParentId, out HusbandryAnimalState parent)
                    || !parent.SpeciesId.Equals(animal.SpeciesId)))
            {
                throw new InvalidOperationException(
                    $"Animal '{animal.AnimalId.Value}' has an invalid other-parent reference.");
            }
        }
        foreach (AnimalHusbandryPendingOutcomeState pending in
                 state.PendingOutcomes)
        {
            if (!state.Policies.ContainsKey(pending.PenId)
                || pending.Kind != AnimalHusbandryOutcomeOperationKind.Slaughter
                    && !state.Animals.ContainsKey(pending.AnimalId))
            {
                throw new InvalidOperationException(
                    $"Pending husbandry outcome '{pending.OperationId}' has invalid aggregate references.");
            }
        }
    }

    internal static HusbandryAnimalSaveData CaptureAnimal(HusbandryAnimalState state) =>
        new()
        {
            animalInstanceId = state.AnimalId.Value,
            speciesDefinitionId = state.SpeciesId.Value,
            penBuildingInstanceId = state.PenId.Value,
            sex = state.Sex,
            ageDays = state.AgeDays,
            tamed = state.Tamed,
            tamingProgress = state.TamingProgress,
            pregnant = state.Pregnant,
            pregnancyProgressDays = state.PregnancyProgressDays,
            otherParentAnimalInstanceId = state.OtherParentId.Value,
            breedingCooldownDays = state.BreedingCooldownDays,
            manureProgressDays = state.ManureProgressDays,
            readyManureCycles = state.ReadyManureCycles,
            slaughterDesignated = state.SlaughterDesignated,
            autoSlaughterDesignated = state.AutoSlaughterDesignated,
            pendingWorkKind = state.PendingWorkKind,
            pendingProductItemDefinitionId = state.PendingProductItemId.Value,
            pendingWorkCompleted = state.PendingWorkCompleted,
            pendingWorkContributions = (state.PendingWorkContributions
                    ?? new List<CraftContributionSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            statusCode = state.StatusCode,
            statusParameters = new List<string>(
                state.StatusParameters ?? new List<string>()),
            products = (state.Products ?? new List<AnimalProductProgressState>())
                .Where(product => product != null)
                .OrderBy(product => product.ItemId.Value, StringComparer.Ordinal)
                .Select(product => new AnimalProductProgressSaveData
                {
                    itemDefinitionId = product.ItemId.Value,
                    progressDays = product.ProgressDays,
                    readyCycles = product.ReadyCycles
                })
                .ToList()
        };

    private static List<CraftContributionSaveData> BuildContributions(
        HusbandryAnimalSaveData saved,
        WildlifeInstanceId animalId)
    {
        var result = new List<CraftContributionSaveData>();
        string previous = null;
        foreach (CraftContributionSaveData value in saved.pendingWorkContributions)
        {
            string characterId = value?.characterId?.Trim() ?? string.Empty;
            if (value == null
                || characterId.Length == 0
                || !string.Equals(characterId, value.characterId, StringComparison.Ordinal)
                || !float.IsFinite(value.contributedWork)
                || value.contributedWork <= 0f
                || !float.IsFinite(value.relevantSkill)
                || value.relevantSkill < 0f
                || previous != null
                && string.CompareOrdinal(previous, characterId) >= 0)
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' has invalid or unsorted work contributions.");
            }
            result.Add(value.Clone());
            previous = characterId;
        }
        if (saved.pendingWorkKind == AnimalHusbandryWorkKind.None
            && result.Count > 0)
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' has contributions without pending work.");
        }
        return result;
    }

    private static AnimalHusbandryPendingOutcomeState BuildPendingOutcome(
        AnimalHusbandryPendingOutcomeSaveData saved,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (saved == null
            || string.IsNullOrWhiteSpace(saved.operationId)
            || !string.Equals(saved.operationId, saved.operationId.Trim(), StringComparison.Ordinal)
            || saved.operationId.Length > 512
            || saved.kind is < AnimalHusbandryOutcomeOperationKind.Tame
                or > AnimalHusbandryOutcomeOperationKind.Slaughter
            || saved.absoluteDay < 1
            || !((BuildingInstanceId)saved.penId).IsValid
            || !((WildlifeInstanceId)saved.animalId).IsValid
            || !((WildlifeSpeciesId)saved.speciesId).IsValid
            || string.IsNullOrWhiteSpace(saved.workerId)
            || string.IsNullOrWhiteSpace(saved.workerDisplayName)
            || string.IsNullOrWhiteSpace(saved.publicationKey)
            || saved.publicationKey != saved.publicationKey.Trim()
            || saved.publicationKey.Length > 512 || saved.publicationKey.Any(char.IsControl)
            || saved.completionPublicationKey == null
            || saved.completionPublicationKey != saved.completionPublicationKey.Trim()
            || saved.completionPublicationKey.Length > 512 || saved.completionPublicationKey.Any(char.IsControl)
            || saved.animalStatesBefore == null || saved.animalStatesBefore.Count != 1
            || saved.animalStatesAfter == null
            || saved.animalStatesAfter.Count != (saved.kind == AnimalHusbandryOutcomeOperationKind.Slaughter ? 0 : 1)
            || saved.contributions == null || saved.outputStacks == null
            || saved.outputStacks.Any(value => value == null)
            || saved.completedWorkMilliUnits <= 0
            || saved.outputQuantity < 0
            || saved.outputMassGrams < 0L)
        {
            throw new InvalidOperationException(
                "Animal-husbandry pending outcome is invalid.");
        }
        ItemDefinitionId outputItemId = new(saved.outputItemId);
        if (saved.completionIdentityRequired)
        {
            if (!Enum.IsDefined(typeof(CharacterCommandOrigin), saved.completionOrigin)
                || !float.IsFinite(saved.completionContributionWorkUnits)
                || saved.completionContributionWorkUnits <= 0f
                || saved.completionContributionWorkUnits > saved.completedWorkMilliUnits / 1000f + 0.001f
                || string.IsNullOrWhiteSpace(saved.completionSkillModifierProvenance)
                || saved.completionPublicationKey.Length > 0 && !saved.outcomeCommitted)
                throw new InvalidOperationException("Husbandry retained completion evidence is invalid.");
        }
        else if (saved.outcomeCommitted || saved.completionContributionWorkUnits != 0f
            || saved.completionOrigin != CharacterCommandOrigin.Autonomous
            || !string.IsNullOrEmpty(saved.completionPublicationKey)
            || !string.IsNullOrEmpty(saved.completionSkillModifierProvenance))
            throw new InvalidOperationException("Unowned husbandry completion contains retained identity evidence.");
        if ((saved.outputQuantity > 0) != outputItemId.IsValid
            || outputItemId.IsValid && !containsItem(outputItemId)
            || saved.outputQuantity > 0 && saved.outputStacks.Count == 0
            || saved.outputQuantity == 0
                && (saved.outputMassGrams != 0L
                    || saved.outputStacks.Count != 0
                    || !string.IsNullOrEmpty(saved.carcassStackId)))
        {
            throw new InvalidOperationException(
                "Animal-husbandry pending output evidence is inconsistent.");
        }
        var pending = new AnimalHusbandryPendingOutcomeState
        {
            OperationId = saved.operationId,
            Kind = saved.kind,
            AbsoluteDay = saved.absoluteDay,
            PenId = new BuildingInstanceId(saved.penId),
            AnimalId = new WildlifeInstanceId(saved.animalId),
            SpeciesId = new WildlifeSpeciesId(saved.speciesId),
            WorkerId = saved.workerId,
            WorkerDisplayName = saved.workerDisplayName,
            PublicationKey = saved.publicationKey,
            AnimalStatesBefore = saved.animalStatesBefore.Select(value =>
                BuildAnimal(value, getSpeciesProducts, containsItem)).ToList(),
            AnimalStatesAfter = saved.animalStatesAfter.Select(value =>
                BuildAnimal(value, getSpeciesProducts, containsItem)).ToList(),
            CompletedWorkMilliUnits = saved.completedWorkMilliUnits,
            Contributions = saved.contributions.ConvertAll(value => value?.Clone()),
            OutputItemId = outputItemId,
            OutputQuantity = saved.outputQuantity,
            OutputMassGrams = saved.outputMassGrams,
            OutputStacks = saved.outputStacks.ConvertAll(value =>
                new AnimalHusbandryOutputStack { StackId = value.stackId, Quantity = value.quantity }),
            CarcassStackId = saved.carcassStackId ?? string.Empty,
            LastFailure = saved.lastFailure ?? string.Empty,
            CompletionIdentityRequired = saved.completionIdentityRequired,
            OutcomeCommitted = saved.outcomeCommitted,
            CompletionPublicationKey = saved.completionPublicationKey,
            CompletionOrigin = saved.completionOrigin,
            CompletionContributionWorkUnits = saved.completionContributionWorkUnits,
            CompletionSkillModifierProvenance = saved.completionSkillModifierProvenance ?? string.Empty
        };
        try { _ = new AnimalHusbandryWorkFact(pending); }
        catch (ArgumentException exception)
        { throw new InvalidOperationException("Husbandry pending work fact is invalid.", exception); }
        return pending;
    }

    private static AnimalHusbandryPendingOutcomeSaveData CapturePendingOutcome(
        AnimalHusbandryPendingOutcomeState value) => new()
        {
            operationId = value.OperationId,
            kind = value.Kind,
            absoluteDay = value.AbsoluteDay,
            penId = value.PenId.Value,
            animalId = value.AnimalId.Value,
            speciesId = value.SpeciesId.Value,
            workerId = value.WorkerId,
            workerDisplayName = value.WorkerDisplayName,
            publicationKey = value.PublicationKey,
            animalStatesBefore = value.AnimalStatesBefore.Select(CaptureAnimal).ToList(),
            animalStatesAfter = value.AnimalStatesAfter.Select(CaptureAnimal).ToList(),
            completedWorkMilliUnits = value.CompletedWorkMilliUnits,
            contributions = value.Contributions.ConvertAll(entry => entry?.Clone()),
            outputItemId = value.OutputItemId.Value,
            outputQuantity = value.OutputQuantity,
            outputMassGrams = value.OutputMassGrams,
            outputStacks = value.OutputStacks.ConvertAll(entry => new AnimalHusbandryOutputStackSaveData
            { stackId = entry.StackId, quantity = entry.Quantity }),
            carcassStackId = value.CarcassStackId,
            lastFailure = value.LastFailure,
            completionIdentityRequired = value.CompletionIdentityRequired,
            outcomeCommitted = value.OutcomeCommitted,
            completionPublicationKey = value.CompletionPublicationKey,
            completionOrigin = value.CompletionOrigin,
            completionContributionWorkUnits = value.CompletionContributionWorkUnits,
            completionSkillModifierProvenance = value.CompletionSkillModifierProvenance
        };

    internal static AnimalPenPolicySaveData CapturePolicy(AnimalPenPolicyData policy) =>
        new()
        {
            penBuildingInstanceId = policy.PenId.Value,
            allowedSpeciesDefinitionIds = (policy.AllowedSpeciesIds
                    ?? new List<WildlifeSpeciesId>())
                .Select(id => id.Value)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            allowHerbivores = policy.allowHerbivores,
            allowOmnivores = policy.allowOmnivores,
            allowCarnivores = policy.allowCarnivores,
            allowScavengers = policy.allowScavengers,
            allowFemales = policy.allowFemales,
            allowMales = policy.allowMales,
            allowJuveniles = policy.allowJuveniles,
            maximumAnimals = policy.maximumAnimals,
            breedingAllowed = policy.breedingAllowed,
            protectPregnant = policy.protectPregnant,
            allowRiskyMixing = policy.allowRiskyMixing,
            adultFemaleLimit = policy.adultFemaleLimit,
            adultMaleLimit = policy.adultMaleLimit,
            juvenileLimit = policy.juvenileLimit,
            minimumBreedingFemales = policy.minimumBreedingFemales,
            minimumBreedingMales = policy.minimumBreedingMales
        };

    private static void RequireCanonical(
        string normalized,
        string raw,
        bool valid,
        string label)
    {
        if (!valid || !string.Equals(normalized, raw, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Animal-husbandry payload has a non-canonical {label} ID.");
        }
    }

    private static void RequireFiniteNonNegative(
        float value,
        WildlifeInstanceId animalId,
        string label) => RequireFiniteRange(value, 0f, float.MaxValue, animalId, label);

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        WildlifeInstanceId animalId,
        string label)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' has invalid {label}.");
        }
    }
}
