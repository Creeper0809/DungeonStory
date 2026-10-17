using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Owns policy values and their retryable fact publication in the consumables aggregate.</summary>
internal sealed class CharacterConsumablesPolicyRuntime
{
    private readonly ICharacterConsumablesWorldPort world;
    private readonly ICharacterConsumablesInventoryPort inventory;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IDomainFactPublisher domainFacts;
    private readonly IGameCalendar calendar;

    internal CharacterConsumablesPolicyRuntime(
        ICharacterConsumablesWorldPort world,
        ICharacterConsumablesInventoryPort inventory,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDomainFactPublisher domainFacts,
        IGameCalendar calendar)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.aggregateRootStore = aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.domainFacts = domainFacts;
        this.calendar = calendar;
    }

    private CharacterConsumablesAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterConsumablesAggregateState());
    private CharacterConsumablesAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterConsumablesAggregateState(), state => state.Clone());

    public CharacterDietPolicyKind GetDietPolicy(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.DietPolicies.TryGetValue(characterId, out CharacterDietPolicyState state)
            ? state.policy
            : CharacterConsumablesPolicyRules.DefaultDietPolicy;

    public CharacterConsumablesPolicyChangeResult SetDietPolicy(
        CharacterId characterId,
        CharacterDietPolicyKind policy,
        string source = "runtime-command")
    {
        const string PolicyId = "character-diet";
        if (!characterId.IsValid
            || !Enum.IsDefined(typeof(CharacterDietPolicyKind), policy))
            return InvalidPolicyChange(PolicyId, characterId.Value, "diet-policy-invalid");
        CharacterDietPolicyKind before = GetDietPolicy(characterId);
        string beforeValue = CanonicalPolicyValue(before);
        string afterValue = CanonicalPolicyValue(policy);
        bool existed = ReadState.DietPolicies.TryGetValue(
            characterId,
            out CharacterDietPolicyState previous);
        return ApplyPolicyChange(
            characterId,
            PolicyId,
            "self",
            beforeValue,
            afterValue,
            source,
            () => WriteState.DietPolicies[characterId] = new CharacterDietPolicyState
            {
                characterId = characterId.Value,
                policy = policy
            },
            () =>
            {
                if (existed)
                    WriteState.DietPolicies[characterId] =
                        CharacterConsumablesStateRules.Clone(previous);
                else
                    WriteState.DietPolicies.Remove(characterId);
            });
    }

    public CharacterMealQualityLimit GetMealQualityLimit(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.MealQualityPolicies.TryGetValue(
            characterId,
            out CharacterMealQualityPolicyState state)
            ? state.maximumQuality
            : CharacterMealQualityLimit.Inherit;

    public CharacterConsumablesPolicyChangeResult SetMealQualityLimit(
        CharacterId characterId,
        CharacterMealQualityLimit qualityLimit,
        string source = "runtime-command")
    {
        const string PolicyId = "character-meal-quality";
        if (!characterId.IsValid
            || !Enum.IsDefined(typeof(CharacterMealQualityLimit), qualityLimit))
            return InvalidPolicyChange(
                PolicyId,
                characterId.Value,
                "meal-quality-policy-invalid");
        CharacterMealQualityLimit before = GetMealQualityLimit(characterId);
        bool existed = ReadState.MealQualityPolicies.TryGetValue(
            characterId,
            out CharacterMealQualityPolicyState previous);
        return ApplyPolicyChange(
            characterId,
            PolicyId,
            "self",
            CanonicalPolicyValue(before),
            CanonicalPolicyValue(qualityLimit),
            source,
            () => WriteState.MealQualityPolicies[characterId] =
                new CharacterMealQualityPolicyState
                {
                    characterId = characterId.Value,
                    maximumQuality = qualityLimit
                },
            () =>
            {
                if (existed)
                    WriteState.MealQualityPolicies[characterId] =
                        CharacterConsumablesStateRules.Clone(previous);
                else
                    WriteState.MealQualityPolicies.Remove(characterId);
            });
    }

    internal bool IsWithinMealQualityLimit(CharacterId characterId, MealQualityBand quality)
    {
        CharacterMealQualityLimit limit = GetMealQualityLimit(characterId);
        return quality <= (limit == CharacterMealQualityLimit.Inherit
            ? MealQualityBand.Fine
            : (MealQualityBand)(int)limit);
    }

    public bool IsMealAllowed(
        CharacterId characterId,
        CharacterConsumablesMealDefinitionSnapshot meal) =>
        meal.Id.IsValid
        && world.GetCultureMealPreference(characterId, meal.Id)
            != CharacterCultureMealPreference.Forbidden
        && CharacterConsumablesPolicyRules.AllowsMeal(
            GetDietPolicy(characterId),
            meal.DietClass,
            meal.ForbiddenIngredient);

    public CharacterSubstancePolicyState GetSubstancePolicy(
        CharacterId characterId,
        string substanceId)
    {
        if (!inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            return new CharacterSubstancePolicyState
            {
                characterId = characterId.Value,
                mode = SubstancePolicyMode.Forbidden
            };
        }
        CharacterSubstanceKey key = new(characterId, substance.Id);
        return ReadState.SubstancePolicies.TryGetValue(key, out CharacterSubstancePolicyState state)
            ? CharacterConsumablesStateRules.Clone(state)
            : new CharacterSubstancePolicyState
            {
                characterId = characterId.Value,
                itemDefinitionId = substance.Id.Value,
                mode = CharacterConsumablesPolicyRules.GetDefaultSubstancePolicy(
                    substance.Definition.UseClass),
                moodThreshold = 30f,
                scheduledHour = 20
            };
    }

    public CharacterConsumablesPolicyChangeResult SetSubstancePolicy(
        CharacterId characterId,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold,
        int scheduledHour,
        string source = "runtime-command")
    {
        if (!characterId.IsValid
            || !Enum.IsDefined(typeof(SubstancePolicyMode), mode)
            || float.IsNaN(moodThreshold) || float.IsInfinity(moodThreshold)
            || !inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            return InvalidPolicyChange(
                "character-substance",
                substanceId,
                "substance-policy-invalid");
        CharacterSubstancePolicyState before = GetSubstancePolicy(
            characterId,
            substance.Id.Value);
        CharacterSubstancePolicyState state = new()
        {
            characterId = characterId.Value,
            itemDefinitionId = substance.Id.Value,
            mode = mode,
            moodThreshold = Mathf.Clamp(moodThreshold, 0f, 100f),
            scheduledHour = Mathf.Clamp(scheduledHour, 0, 23)
        };
        CharacterSubstanceKey key = new(characterId, substance.Id);
        bool existed = ReadState.SubstancePolicies.TryGetValue(
            key,
            out CharacterSubstancePolicyState previous);
        return ApplyPolicyChange(
            characterId,
            "character-substance",
            substance.Id.Value,
            CanonicalSubstancePolicy(before),
            CanonicalSubstancePolicy(state),
            source,
            () => WriteState.SubstancePolicies[key] = state,
            () =>
            {
                if (existed)
                    WriteState.SubstancePolicies[key] =
                        CharacterConsumablesStateRules.Clone(previous);
                else
                    WriteState.SubstancePolicies.Remove(key);
            });
    }

    private CharacterConsumablesPolicyChangeResult ApplyPolicyChange(
        CharacterId characterId,
        string policyId,
        string scopeId,
        string beforeValue,
        string afterValue,
        string source,
        Action apply,
        Action rollback)
    {
        if (!ResumePendingPolicyFact(out string pendingFailure))
            return new CharacterConsumablesPolicyChangeResult(
                CharacterConsumablesPolicyChangeCode.OutcomeRejected,
                policyId, scopeId, beforeValue, afterValue,
                ReadState.PolicyRevision, pendingFailure);
        long beforeRevision = ReadState.PolicyRevision;
        if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
        {
            return new CharacterConsumablesPolicyChangeResult(
                CharacterConsumablesPolicyChangeCode.NoChange,
                policyId,
                scopeId,
                beforeValue,
                afterValue,
                beforeRevision,
                "policy-value-unchanged");
        }
        if (beforeRevision == long.MaxValue
            || !IsStablePolicyToken(source)
            || apply == null
            || rollback == null)
        {
            return InvalidPolicyChange(
                policyId,
                scopeId,
                "policy-change-context-invalid");
        }

        if (domainFacts == null || calendar == null || calendar.Day < 0)
            return InvalidPolicyChange(policyId, scopeId, "policy-publication-context-missing");
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor))
            return InvalidPolicyChange(policyId, scopeId, "policy-character-missing");

        long afterRevision = beforeRevision + 1L;
        CharacterConsumablesPolicyChangedFact fact = new(
            characterId, actor.DisplayName, actor.Position, policyId, scopeId,
            beforeValue, afterValue, source, beforeRevision, afterRevision, calendar.Day);
        if (!domainFacts.TryReserve(fact, out DomainFactLease lease, out string prepareFailure))
            return new CharacterConsumablesPolicyChangeResult(
                CharacterConsumablesPolicyChangeCode.OutcomeRejected,
                policyId, scopeId, beforeValue, afterValue, beforeRevision, prepareFailure);

        bool publicationStarted = false;
        try
        {
            apply();
            WriteState.PolicyRevision = afterRevision;
            WriteState.PendingPolicyFact = fact;
            publicationStarted = true;
            DomainFactDelivery delivery = domainFacts.Publish(lease, fact);
            if (!delivery.MayBeCommitted)
            {
                publicationStarted = false;
                rollback();
                WriteState.PolicyRevision = beforeRevision;
                WriteState.PendingPolicyFact = null;
                domainFacts.Cancel(lease);
                return new CharacterConsumablesPolicyChangeResult(
                    CharacterConsumablesPolicyChangeCode.OutcomeRejected,
                    policyId, scopeId, beforeValue, afterValue, beforeRevision, delivery.Detail);
            }
            if (delivery.Phase != DomainFactDeliveryPhase.InDoubt)
                WriteState.PendingPolicyFact = null;
            return new CharacterConsumablesPolicyChangeResult(
                CharacterConsumablesPolicyChangeCode.Changed,
                policyId,
                scopeId,
                beforeValue,
                afterValue,
                afterRevision,
                delivery.Phase == DomainFactDeliveryPhase.InDoubt
                    ? "policy-value-applied-publication-pending" : "policy-value-committed");
        }
        catch
        {
            // A subscriber exception after publication starts is ambiguous.
            // Keep the exact saved fact; never undo a possibly durable action.
            if (!publicationStarted)
            {
                rollback();
                WriteState.PolicyRevision = beforeRevision;
                WriteState.PendingPolicyFact = null;
                domainFacts.Cancel(lease);
            }
            throw;
        }
    }

    public bool ResumePendingPolicyFact(out string failure)
    {
        failure = string.Empty;
        CharacterConsumablesPolicyChangedFact pending = ReadState.PendingPolicyFact;
        if (pending == null) return true;
        if (domainFacts == null)
        { failure = "policy-fact-publisher-missing"; return false; }
        DomainFactDelivery delivery = domainFacts.Replay(pending);
        if (!delivery.MayBeCommitted || delivery.Phase == DomainFactDeliveryPhase.InDoubt)
        { failure = delivery.Detail; return false; }
        WriteState.PendingPolicyFact = null;
        return true;
    }

    private static CharacterConsumablesPolicyChangeResult InvalidPolicyChange(
        string policyId,
        string scopeId,
        string detailCode) => new(
        CharacterConsumablesPolicyChangeCode.InvalidRequest,
        policyId,
        scopeId,
        string.Empty,
        string.Empty,
        0L,
        detailCode);

    private static string CanonicalPolicyValue<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    private static string CanonicalSubstancePolicy(
        CharacterSubstancePolicyState state) =>
        CanonicalPolicyValue(state.mode) + ":"
        + state.moodThreshold.ToString("R", CultureInfo.InvariantCulture) + ":"
        + state.scheduledHour.ToString(CultureInfo.InvariantCulture);

    private static bool IsStablePolicyToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 192)
            return false;
        return value.All(character => character >= 'a' && character <= 'z'
            || character >= 'A' && character <= 'Z'
            || character >= '0' && character <= '9'
            || character is '.' or ':' or '_' or '-' or '/');
    }

    internal void ReconcilePersistentActors(IReadOnlyCollection<CharacterId> persistent)
    {
        // A saved pending fact must still agree with its policy value. Resolve its
        // acknowledgement before retiring that value, or leave both for retry.
        CharacterConsumablesPolicyChangedFact pending = ReadState.PendingPolicyFact;
        if (pending != null && !persistent.Contains(pending.CharacterId)
            && !ResumePendingPolicyFact(out string failure))
            throw new InvalidOperationException(
                "Consumables policy retirement blocked by pending publication: " + failure);
        CharacterConsumablesAggregateState snapshot = ReadState;
        CharacterId[] diet = snapshot.DietPolicies.Keys.Where(id => !persistent.Contains(id)).ToArray();
        CharacterId[] quality = snapshot.MealQualityPolicies.Keys.Where(id => !persistent.Contains(id)).ToArray();
        CharacterSubstanceKey[] substance = snapshot.SubstancePolicies.Keys
            .Where(key => !persistent.Contains(key.CharacterId)).ToArray();
        if (diet.Length == 0 && quality.Length == 0 && substance.Length == 0) return;
        CharacterConsumablesAggregateState state = WriteState;
        foreach (CharacterId id in diet) state.DietPolicies.Remove(id);
        foreach (CharacterId id in quality) state.MealQualityPolicies.Remove(id);
        foreach (CharacterSubstanceKey key in substance) state.SubstancePolicies.Remove(key);
    }

    private bool TryGetActor(
        CharacterId id,
        out CharacterConsumablesActorSnapshot actor)
    {
        if (id.IsValid && world.TryGetActor(id, out actor))
        {
            return true;
        }
        actor = default;
        return false;
    }

}
