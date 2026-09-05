using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CharacterBodyHealthStateRules
{
    private readonly IAnatomyProfileCatalog anatomyProfiles;

    public CharacterBodyHealthStateRules(IAnatomyProfileCatalog anatomyProfiles)
    {
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
    }

    public AnatomyProfileDefinition ResolveForSpecies(string speciesTag)
    {
        return anatomyProfiles.GetForSpecies(speciesTag);
    }

    public bool TryResolveProfile(
        string profileId,
        out AnatomyProfileDefinition profile) =>
        anatomyProfiles.TryGet(profileId, out profile);

    public void EnsureParts(CharacterBodyHealthState state)
    {
        state.parts ??= new List<CharacterBodyPartHealthState>();
        EnsurePart(state, CombatBodyPart.Head, 18f);
        EnsurePart(state, CombatBodyPart.Torso, 45f);
        EnsurePart(state, CombatBodyPart.LeftArm, 22f);
        EnsurePart(state, CombatBodyPart.RightArm, 22f);
        EnsurePart(state, CombatBodyPart.LeftLeg, 26f);
        EnsurePart(state, CombatBodyPart.RightLeg, 26f);
    }

    public void EnsurePart(CharacterBodyHealthState state, CombatBodyPart bodyPart, float maxHealth)
    {
        CharacterBodyPartHealthState part = state.parts.FirstOrDefault(item => item.bodyPart == bodyPart);
        if (part == null)
        {
            state.parts.Add(new CharacterBodyPartHealthState
            {
                bodyPart = bodyPart,
                maxHealth = maxHealth,
                currentHealth = maxHealth
            });
            return;
        }

        part.maxHealth = Mathf.Max(1f, part.maxHealth);
        part.currentHealth = Mathf.Clamp(part.currentHealth, 0f, part.maxHealth);
        part.bleedingPerSecond = Mathf.Max(0f, part.bleedingPerSecond);
    }

    public CharacterBodyHealthSnapshot BuildSnapshot(CharacterBodyHealthState state)
    {
        CharacterBodyPartHealthState head = state.parts.First(part => part.bodyPart == CombatBodyPart.Head);
        CharacterBodyPartHealthState torso = state.parts.First(part => part.bodyPart == CombatBodyPart.Torso);
        CharacterBodyPartHealthState leftArm = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftArm);
        CharacterBodyPartHealthState rightArm = state.parts.First(part => part.bodyPart == CombatBodyPart.RightArm);
        CharacterBodyPartHealthState leftLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftLeg);
        CharacterBodyPartHealthState rightLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.RightLeg);
        GetPhysicalCapacity(
            state,
            out float consciousness,
            out float manipulation,
            out float mobility);
        return new CharacterBodyHealthSnapshot(
            state.parts.Select(ClonePart).ToArray(),
            state.bloodLoss,
            state.suppression,
            consciousness,
            manipulation,
            mobility,
            state.downed);
    }

    public void UpdateDowned(CharacterBodyHealthState state)
    {
        GetPhysicalCapacity(
            state,
            out float consciousness,
            out _,
            out float mobility);
        if (state.downed)
        {
            state.downed = consciousness < 0.35f
                || mobility < 0.3f
                || state.bloodLoss >= 70f;
            return;
        }

        state.downed = consciousness < 0.25f || mobility < 0.2f;
    }

    public void GetPhysicalCapacity(
        CharacterBodyHealthState state,
        out float consciousness,
        out float manipulation,
        out float mobility)
    {
        if (state.anatomyNodes != null && state.anatomyNodes.Count > 0)
        {
            AnatomyHealthSnapshot anatomy = BuildAnatomySnapshot(state);
            GetLegacySurfaceCapacity(
                state,
                out float surfaceConsciousness,
                out float surfaceManipulation,
                out float surfaceMobility);
            consciousness = Mathf.Min(anatomy.MentalMaintenance, surfaceConsciousness)
                * Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
            manipulation = Mathf.Min(anatomy.PrecisionManipulation, surfaceManipulation);
            mobility = Mathf.Min(anatomy.PhysicalMobility, surfaceMobility);
            return;
        }

        GetLegacySurfaceCapacity(
            state,
            out consciousness,
            out manipulation,
            out mobility);
        consciousness *= Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
    }

    public void GetLegacySurfaceCapacity(
        CharacterBodyHealthState state,
        out float consciousness,
        out float manipulation,
        out float mobility)
    {
        float head = 1f;
        float torso = 1f;
        float leftArm = 1f;
        float rightArm = 1f;
        float leftLeg = 1f;
        float rightLeg = 1f;

        for (int i = 0; i < state.parts.Count; i++)
        {
            CharacterBodyPartHealthState part = state.parts[i];
            if (part == null)
            {
                continue;
            }

            switch (part.bodyPart)
            {
                case CombatBodyPart.Head:
                    head = part.HealthRatio;
                    break;
                case CombatBodyPart.Torso:
                    torso = part.HealthRatio;
                    break;
                case CombatBodyPart.LeftArm:
                    leftArm = part.HealthRatio;
                    break;
                case CombatBodyPart.RightArm:
                    rightArm = part.HealthRatio;
                    break;
                case CombatBodyPart.LeftLeg:
                    leftLeg = part.HealthRatio;
                    break;
                case CombatBodyPart.RightLeg:
                    rightLeg = part.HealthRatio;
                break;
            }
        }

        consciousness = Mathf.Min(head, torso);
        manipulation = (leftArm + rightArm) * 0.5f;
        mobility = (leftLeg + rightLeg) * 0.5f;
    }

    public CharacterBodyHealthState CloneState(CharacterBodyHealthState source)
    {
        return new CharacterBodyHealthState
        {
            characterId = source.characterId ?? string.Empty,
            maxHealth = Mathf.Max(1f, source.maxHealth),
            currentHealth = Mathf.Clamp(
                source.currentHealth,
                0f,
                Mathf.Max(1f, source.maxHealth)),
            injurySeverity = Mathf.Clamp01(source.injurySeverity),
            anatomyProfileId = source.anatomyProfileId ?? string.Empty,
            parts = source.parts?.Select(ClonePart).ToList() ?? new List<CharacterBodyPartHealthState>(),
            anatomyNodes = source.anatomyNodes?.Select(CloneAnatomyNode).ToList()
                ?? new List<AnatomyNodeHealthState>(),
            bloodLoss = Mathf.Clamp(source.bloodLoss, 0f, 100f),
            suppression = Mathf.Clamp(source.suppression, 0f, 100f),
            burningDamagePerSecond = Mathf.Max(0f, source.burningDamagePerSecond),
            burningRemainingSeconds = Mathf.Max(0f, source.burningRemainingSeconds),
            sedationRatio = Mathf.Clamp01(source.sedationRatio),
            sedationRemainingSeconds = Mathf.Max(0f, source.sedationRemainingSeconds),
            manaBlockedRemainingSeconds = Mathf.Max(0f, source.manaBlockedRemainingSeconds),
            maxMana = Mathf.Max(1f, source.maxMana),
            currentMana = Mathf.Clamp(
                source.currentMana,
                0f,
                Mathf.Max(1f, source.maxMana)),
            downed = source.downed,
            lastDamageReason = source.lastDamageReason ?? string.Empty
        };
    }

    public CharacterBodyPartHealthState ClonePart(CharacterBodyPartHealthState source)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = source.bodyPart,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond
        };
    }

    public AnatomyProfileDefinition ResolveProfile(string profileId)
    {
        return anatomyProfiles.TryGet(profileId, out AnatomyProfileDefinition profile)
            ? profile
            : anatomyProfiles.GetDefaultHumanoid();
    }

    public void EnsureAnatomy(
        CharacterBodyHealthState state,
        AnatomyProfileDefinition profile)
    {
        if (state == null || profile == null)
        {
            return;
        }

        state.anatomyProfileId = profile.ProfileId;
        state.anatomyNodes ??= new List<AnatomyNodeHealthState>();
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            AnatomyNodeHealthState node = state.anatomyNodes.FirstOrDefault(
                candidate => string.Equals(
                    candidate.nodeId,
                    definition.NodeId,
                    StringComparison.Ordinal));
            if (node == null)
            {
                node = new AnatomyNodeHealthState
                {
                    nodeId = definition.NodeId,
                    maxHealth = definition.MaxHealth,
                    currentHealth = definition.MaxHealth,
                    installedPartKind = SurgicalPartKind.NaturalOrgan,
                    installedPartEfficiency = 1f
                };
                state.anatomyNodes.Add(node);
            }

            node.maxHealth = Mathf.Max(1f, node.maxHealth);
            node.currentHealth = Mathf.Clamp(node.currentHealth, 0f, node.maxHealth);
            node.bleedingPerSecond = Mathf.Max(0f, node.bleedingPerSecond);
            node.infection = Mathf.Clamp(node.infection, 0f, 100f);
            node.installedPartEfficiency = Mathf.Max(0f, node.installedPartEfficiency);
        }

        state.anatomyNodes.RemoveAll(node =>
            node == null || !profile.TryGetNode(node.nodeId, out _));
        SyncAnatomySurfaceNodesFromLegacy(state);
    }

    public AnatomyHealthSnapshot BuildAnatomySnapshot(CharacterBodyHealthState state)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        EnsureAnatomy(state, profile);
        float mentalMaintenance = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.MentalMaintenance, 1f);
        float visualDiscernment = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.VisualDiscernment, 1f);
        float auditorySensing = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.AuditorySensing, 1f);
        float respiratoryExchange = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.RespiratoryExchange, 1f);
        float powerCirculation = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.PowerCirculation, 1f)
            * Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
        float intakeProcessing = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.IntakeProcessing, 1f);
        float purificationProcessing = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.PurificationProcessing, 1f);
        float vitalityResponse = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.VitalityResponse, 1f);
        float physicalPower = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.PhysicalPower, 1f);
        float precisionManipulation = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.PrecisionManipulation, 1f);
        float physicalMobility = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.PhysicalMobility, 1f);
        float communication = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.Communication, 1f);
        float arcaneConduction = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.ArcaneConduction, 1f);
        float immuneDefense = CalculateFunctionEfficiency(
            state, profile, AnatomyFunction.ImmuneDefense, 1f);
        return new AnatomyHealthSnapshot(
            profile.ProfileId,
            state.anatomyNodes.Select(CloneAnatomyNode).ToArray(),
            mentalMaintenance,
            visualDiscernment,
            auditorySensing,
            respiratoryExchange,
            powerCirculation,
            intakeProcessing,
            purificationProcessing,
            vitalityResponse,
            physicalPower,
            precisionManipulation,
            physicalMobility,
            communication,
            arcaneConduction,
            immuneDefense);
    }

    public float CalculateFunctionEfficiency(
        CharacterBodyHealthState state,
        AnatomyProfileDefinition profile,
        AnatomyFunction function,
        float defaultValue)
    {
        float weightedTotal = 0f;
        float totalWeight = 0f;
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            if ((definition.ExpandedFunctions & function) == 0)
            {
                continue;
            }

            AnatomyNodeHealthState node = FindAnatomyNode(state, definition.NodeId);
            float weight = Mathf.Max(0.01f, definition.CapacityWeight);
            weightedTotal += (node?.FunctionalEfficiency ?? 0f) * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f
            ? Mathf.Max(0f, weightedTotal / totalWeight)
            : Mathf.Max(0f, defaultValue);
    }

    public float GetStateBleeding(CharacterBodyHealthState state)
    {
        if (state.anatomyNodes != null && state.anatomyNodes.Count > 0)
        {
            return state.anatomyNodes.Sum(node =>
                Mathf.Max(0f, node.bleedingPerSecond));
        }

        return state.parts.Sum(part =>
            Mathf.Max(0f, part.bleedingPerSecond));
    }

    public void ApplyLegacyDamageToAnatomy(
        CharacterBodyHealthState state,
        CombatBodyPart bodyPart,
        float damage,
        float bleeding)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        AnatomyNodeDefinition definition = profile.Nodes.FirstOrDefault(node =>
            node.MapsToLegacyBodyPart && node.LegacyBodyPart == bodyPart);
        if (definition == null)
        {
            return;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(state, definition.NodeId);
        if (node == null)
        {
            return;
        }

        node.currentHealth = Mathf.Max(0f, node.currentHealth - damage);
        node.bleedingPerSecond += Mathf.Max(0f, bleeding);
    }

    public void SyncAnatomySurfaceNodesFromLegacy(CharacterBodyHealthState state)
    {
        if (state?.anatomyNodes == null || state.anatomyNodes.Count == 0)
        {
            return;
        }

        foreach (CharacterBodyPartHealthState legacy in state.parts)
        {
            string nodeId = GetSurfaceNodeId(state.anatomyProfileId, legacy.bodyPart);
            AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
            if (node == null || node.missing)
            {
                continue;
            }

            node.currentHealth = node.maxHealth * legacy.HealthRatio;
            node.bleedingPerSecond = legacy.bleedingPerSecond;
        }
    }

    public void SyncLegacySurfaceNode(
        CharacterBodyHealthState state,
        string nodeId)
    {
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            return;
        }

        CombatBodyPart? bodyPart = GetLegacyBodyPart(state.anatomyProfileId, nodeId);
        if (!bodyPart.HasValue)
        {
            return;
        }

        CharacterBodyPartHealthState legacy = state.parts.FirstOrDefault(
            part => part.bodyPart == bodyPart.Value);
        if (legacy == null)
        {
            return;
        }

        legacy.currentHealth = legacy.maxHealth * node.HealthRatio;
        legacy.bleedingPerSecond = node.bleedingPerSecond;
    }

    public void KillForDestroyedVitalNode(
        CharacterActor actor,
        CharacterBodyHealthState state,
        string nodeId,
        CharacterDeathCauseCode deathCause,
        string reasonCode)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition)
            || !definition.Vital)
        {
            return;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node != null && (node.missing || node.currentHealth <= 0f) && !actor.IsDead)
        {
            if (actor.IsOwner
                && deathCause == CharacterDeathCauseCode.AgeConditionOrganFailure)
            {
                state.currentHealth = Mathf.Max(1f, state.currentHealth);
                state.injurySeverity = Mathf.Clamp01(
                    1f - (state.currentHealth / Mathf.Max(1f, state.maxHealth)));
                actor.Stats?.ApplyVitalsProjection(new CharacterVitalsSnapshot(
                    state.maxHealth,
                    state.currentHealth,
                    state.injurySeverity));
                return;
            }

            actor.Die(
                deathCause,
                string.IsNullOrWhiteSpace(reasonCode)
                    ? $"anatomy:vital-function-loss:{definition.NodeId}"
                    : reasonCode);
        }
    }

    public AnatomyNodeHealthState FindAnatomyNode(
        CharacterBodyHealthState state,
        string nodeId)
    {
        return state?.anatomyNodes?.FirstOrDefault(node =>
            node != null && string.Equals(
                node.nodeId,
                nodeId?.Trim(),
                StringComparison.Ordinal));
    }

    public string GetSurfaceNodeId(
        string profileId,
        CombatBodyPart bodyPart)
    {
        if (string.Equals(profileId, "anatomy:slime", StringComparison.Ordinal))
        {
            return bodyPart == CombatBodyPart.Head ? "core" : "membrane";
        }

        if (string.Equals(profileId, "anatomy:quadruped", StringComparison.Ordinal))
        {
            return bodyPart switch
            {
                CombatBodyPart.Head => "head",
                CombatBodyPart.Torso => "torso",
                CombatBodyPart.LeftLeg => "forelegs",
                CombatBodyPart.RightLeg => "hindlegs",
                _ => "torso"
            };
        }

        return bodyPart switch
        {
            CombatBodyPart.Head => "head",
            CombatBodyPart.Torso => "torso",
            CombatBodyPart.LeftArm => "arm:left",
            CombatBodyPart.RightArm => "arm:right",
            CombatBodyPart.LeftLeg => "leg:left",
            CombatBodyPart.RightLeg => "leg:right",
            _ => "torso"
        };
    }

    public CombatBodyPart? GetLegacyBodyPart(
        string profileId,
        string nodeId)
    {
        if (string.Equals(profileId, "anatomy:slime", StringComparison.Ordinal))
        {
            return nodeId == "membrane" ? CombatBodyPart.Torso : null;
        }

        if (string.Equals(profileId, "anatomy:quadruped", StringComparison.Ordinal))
        {
            return nodeId switch
            {
                "head" => CombatBodyPart.Head,
                "torso" => CombatBodyPart.Torso,
                "forelegs" => CombatBodyPart.LeftLeg,
                "hindlegs" => CombatBodyPart.RightLeg,
                _ => null
            };
        }

        return nodeId switch
        {
            "head" => CombatBodyPart.Head,
            "torso" => CombatBodyPart.Torso,
            "arm:left" => CombatBodyPart.LeftArm,
            "arm:right" => CombatBodyPart.RightArm,
            "leg:left" => CombatBodyPart.LeftLeg,
            "leg:right" => CombatBodyPart.RightLeg,
            _ => null
        };
    }

    public AnatomyNodeHealthState CloneAnatomyNode(
        AnatomyNodeHealthState source)
    {
        if (source == null)
        {
            return null;
        }

        return new AnatomyNodeHealthState
        {
            nodeId = source.nodeId ?? string.Empty,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond,
            infection = source.infection,
            missing = source.missing,
            installedPartId = source.installedPartId ?? string.Empty,
            installedPartKind = source.installedPartKind,
            installedPartEfficiency = source.installedPartEfficiency,
            rejectionBurden = source.rejectionBurden,
            mutationBurden = source.mutationBurden,
            moduleBonus = source.moduleBonus,
            recoveryPolicy = source.recoveryPolicy
        };
    }

    public PartRecoveryPolicy ResolveRecoveryPolicy(
        SurgicalPartKind partKind)
    {
        return partKind switch
        {
            SurgicalPartKind.NaturalOrgan => PartRecoveryPolicy.Natural,
            SurgicalPartKind.ArcaneGraft => PartRecoveryPolicy.AssistedRegeneration,
            SurgicalPartKind.Prosthetic => PartRecoveryPolicy.MaintenanceOnly,
            SurgicalPartKind.Implant => PartRecoveryPolicy.MaintenanceOnly,
            _ => PartRecoveryPolicy.Natural
        };
    }

    public AnatomyHealthSnapshot EmptyAnatomySnapshot()
    {
        return new AnatomyHealthSnapshot(
            string.Empty,
            Array.Empty<AnatomyNodeHealthState>(),
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f);
    }

    public CharacterBodyHealthSnapshot EmptySnapshot()
    {
        return new CharacterBodyHealthSnapshot(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }
}

/// <summary>
/// The sole mutable authority for a character's vital and anatomical health.
/// Unity components keep display projections only; save restore swaps this aggregate
/// through <see cref="DungeonRuntimeAggregateRootStore"/>.
/// </summary>
internal sealed class CharacterVitalsAggregateState
{
    private readonly Dictionary<CharacterId, CharacterBodyHealthState> characters =
        new Dictionary<CharacterId, CharacterBodyHealthState>();

    internal IReadOnlyDictionary<CharacterId, CharacterBodyHealthState> Characters =>
        characters;

    internal bool TryGet(
        CharacterId characterId,
        out CharacterBodyHealthState state)
    {
        return characters.TryGetValue(characterId, out state);
    }

    internal void Set(CharacterId characterId, CharacterBodyHealthState state)
    {
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                "A valid CharacterId is required for character vitals.",
                nameof(characterId));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.characterId = characterId.Value;
        characters[characterId] = state;
    }

    internal CharacterVitalsAggregateState Clone(
        CharacterBodyHealthStateRules stateRules)
    {
        if (stateRules == null)
        {
            throw new ArgumentNullException(nameof(stateRules));
        }

        CharacterVitalsAggregateState clone = new CharacterVitalsAggregateState();
        foreach (KeyValuePair<CharacterId, CharacterBodyHealthState> entry in characters)
        {
            clone.Set(entry.Key, stateRules.CloneState(entry.Value));
        }

        return clone;
    }
}

internal sealed class CharacterVitalsRestoreCandidate :
    CharacterBodyHealthRestoreCandidate
{
    internal CharacterVitalsRestoreCandidate(CharacterVitalsAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterVitalsAggregateState State { get; }
}

/// <summary>
/// Owns character-vitals aggregate access, legacy projection, and persistence.
/// Combat/anatomy orchestration remains in <see cref="CharacterBodyHealthRuntime"/>.
/// </summary>
internal sealed class CharacterVitalsAuthority
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CharacterBodyHealthStateRules stateRules;

    internal CharacterVitalsAuthority(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CharacterBodyHealthStateRules stateRules)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.stateRules = stateRules
            ?? throw new ArgumentNullException(nameof(stateRules));
    }

    internal int PublishedRestoreRevision =>
        aggregateRootStore.PublishedRestoreRevision;

    internal bool IsRestoreStaging => aggregateRootStore.IsRestoreStaging;

    internal CharacterVitalsAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterVitalsAggregateState());

    internal CharacterVitalsAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterVitalsAggregateState(),
            source => source.Clone(stateRules));

    internal CharacterVitalsSnapshot GetVitals(CharacterBodyHealthState state) =>
        state == null
            ? DefaultVitals()
            : new CharacterVitalsSnapshot(
                state.maxHealth,
                state.currentHealth,
                state.injurySeverity);

    internal CharacterVitalsSnapshot GetVitals(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.TryGet(characterId, out CharacterBodyHealthState state)
            ? GetVitals(state)
            : DefaultVitals();

    internal void Configure(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float maximumHealth,
        bool resetCurrentHealth)
    {
        float previousMaximum = Mathf.Max(1f, state.maxHealth);
        float nextMaximum = Mathf.Max(1f, maximumHealth);
        state.maxHealth = nextMaximum;
        if (resetCurrentHealth || state.currentHealth <= 0f)
        {
            state.currentHealth = nextMaximum;
        }
        else
        {
            float ratio = state.currentHealth / previousMaximum;
            state.currentHealth = Mathf.Clamp(nextMaximum * ratio, 1f, nextMaximum);
        }

        UpdateInjuryProjection(state);
        Project(actor, state);
    }

    internal void RestoreLegacyProjection(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float maximumHealth,
        float currentHealth,
        float injurySeverity)
    {
        state.maxHealth = Mathf.Max(1f, maximumHealth);
        state.currentHealth = Mathf.Clamp(currentHealth, 0f, state.maxHealth);
        // Injury severity is a projection of current/max health. Character-world
        // compatibility input must never become a second mutable authority.
        UpdateInjuryProjection(state);
        Project(actor, state);
    }

    internal void Damage(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath)
    {
        if (actor == null || state == null || amount <= 0f || state.currentHealth <= 0f)
        {
            return;
        }

        state.currentHealth = Mathf.Max(
            allowDeath ? 0f : 1f,
            state.currentHealth - amount);
        UpdateInjuryProjection(state);
        Project(actor, state);
        actor.Stats?.NotifyAggregateDamage(
            amount,
            reasonCode,
            allowDeath && state.currentHealth <= 0f,
            deathCause);
    }

    internal void Heal(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount)
    {
        if (actor == null || state == null || amount <= 0f || state.currentHealth <= 0f)
        {
            return;
        }

        float before = state.currentHealth;
        state.currentHealth = Mathf.Min(state.maxHealth, state.currentHealth + amount);
        UpdateInjuryProjection(state);
        Project(actor, state);
        float applied = state.currentHealth - before;
        if (applied > 0f)
        {
            actor.Stats?.NotifyAggregateHealing(applied);
        }
    }

    internal void Scale(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        state.maxHealth = Mathf.Max(1f, state.maxHealth * safeMultiplier);
        state.currentHealth = Mathf.Clamp(
            state.currentHealth * safeMultiplier,
            0f,
            state.maxHealth);
        UpdateInjuryProjection(state);
        Project(actor, state);
    }

    internal void SetInjurySeverity(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float injurySeverity)
    {
        state.injurySeverity = Mathf.Clamp01(injurySeverity);
        state.currentHealth = Mathf.Clamp(
            state.maxHealth * (1f - state.injurySeverity),
            1f,
            state.maxHealth);
        Project(actor, state);
        actor.Stats?.NotifyAggregateInjurySeverity(state.injurySeverity);
    }

    internal void Kill(
        CharacterActor actor,
        CharacterBodyHealthState state,
        CharacterDeathCauseCode cause,
        string reasonCode)
    {
        bool alreadyDead = state.currentHealth <= 0f;
        state.currentHealth = 0f;
        state.injurySeverity = 1f;
        Project(actor, state);
        if (!alreadyDead)
        {
            actor.Stats?.NotifyAggregateDeath(cause, reasonCode);
        }
    }

    internal void Project(
        CharacterActor actor,
        CharacterBodyHealthState state)
    {
        actor?.Stats?.ApplyVitalsProjection(GetVitals(state));
    }

    internal DungeonCharacterBodyHealthSaveData Capture()
    {
        return new DungeonCharacterBodyHealthSaveData
        {
            version = DungeonCharacterBodyHealthSaveData.CurrentVersion,
            characters = ReadState.Characters.Values
                .Select(CloneForCapture)
                .OrderBy(state => state.characterId, StringComparer.Ordinal)
                .ToList()
        };
    }

    internal void ValidateRestore(DungeonCharacterBodyHealthSaveData saveData, DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (saveData == null)
        {
            report.AddError("Character body-health payload is null.");
            return;
        }
        if (saveData.version != DungeonCharacterBodyHealthSaveData.CurrentVersion)
        {
            report.AddError($"Character body-health payload V{saveData.version} is incompatible; "
                + $"expected V{DungeonCharacterBodyHealthSaveData.CurrentVersion}.");
        }
        if (saveData.characters == null)
        {
            report.AddError("Character body-health payload has a null character list.");
            return;
        }

        HashSet<CharacterId> characterIds = new HashSet<CharacterId>();
        string previousCharacterId = null;
        foreach (CharacterBodyHealthState state in saveData.characters)
        {
            string rawCharacterId = state?.characterId ?? string.Empty;
            CharacterId characterId = new CharacterId(rawCharacterId);
            if (state == null || !characterId.IsValid
                || !string.Equals(characterId.Value, rawCharacterId, StringComparison.Ordinal)
                || !characterIds.Add(characterId) || previousCharacterId != null
                && string.CompareOrdinal(previousCharacterId, rawCharacterId) >= 0)
            {
                report.AddError("Character body-health payload contains a null, non-canonical, "
                    + "duplicate, or unordered CharacterId.");
                continue;
            }
            previousCharacterId = rawCharacterId;
            ValidateCharacterState(state, report);
        }
    }

    internal CharacterBodyHealthRestoreCandidate PrepareRestore(
        DungeonCharacterBodyHealthSaveData saveData)
    {
        DungeonGameRestoreReport validation = new DungeonGameRestoreReport();
        ValidateRestore(saveData, validation);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Character body-health restore rejected an invalid V5 candidate: "
                + string.Join(" | ", validation.Errors));
        }

        CharacterVitalsAggregateState restoredAggregate = new CharacterVitalsAggregateState();
        foreach (CharacterBodyHealthState source in saveData.characters)
        {
            CharacterId characterId = new CharacterId(source.characterId);
            CharacterBodyHealthState restored = stateRules.CloneState(source);
            restoredAggregate.Set(characterId, restored);
        }

        return new CharacterVitalsRestoreCandidate(restoredAggregate);
    }

    internal void PublishRestore(CharacterBodyHealthRestoreCandidate candidate)
    {
        if (candidate is not CharacterVitalsRestoreCandidate prepared)
        {
            throw new InvalidOperationException(
                "Character body-health restore candidate has the wrong owner.");
        }

        aggregateRootStore.Replace(prepared.State);
    }

    private CharacterBodyHealthState CloneForCapture(CharacterBodyHealthState source)
    {
        CharacterBodyHealthState clone = stateRules.CloneState(source);
        clone.parts = clone.parts.OrderBy(part => part.bodyPart).ToList();
        clone.anatomyNodes = clone.anatomyNodes.OrderBy(
            node => node.nodeId, StringComparer.Ordinal).ToList();
        return clone;
    }

    private void ValidateCharacterState(CharacterBodyHealthState state, DungeonGameRestoreReport report)
    {
        if (!IsFiniteInRange(state.maxHealth, 1f, float.MaxValue)
            || !IsFiniteInRange(state.currentHealth, 0f, state.maxHealth)
            || !IsFiniteInRange(state.injurySeverity, 0f, 1f)
            || !IsFiniteInRange(state.bloodLoss, 0f, 100f)
            || !IsFiniteInRange(state.suppression, 0f, 100f)
            || !IsFiniteInRange(state.burningDamagePerSecond, 0f, float.MaxValue)
            || !IsFiniteInRange(state.burningRemainingSeconds, 0f, float.MaxValue)
            || !IsFiniteInRange(state.sedationRatio, 0f, 1f)
            || !IsFiniteInRange(state.sedationRemainingSeconds, 0f, float.MaxValue)
            || !IsFiniteInRange(state.manaBlockedRemainingSeconds, 0f, float.MaxValue)
            || !IsFiniteInRange(state.maxMana, 1f, float.MaxValue)
            || !IsFiniteInRange(state.currentMana, 0f, state.maxMana)
            || state.lastDamageReason == null || !string.Equals(state.lastDamageReason,
                state.lastDamageReason.Trim(), StringComparison.Ordinal))
        {
            report.AddError($"Character body-health '{state.characterId}' has invalid "
                + "vital numeric or reason state.");
        }

        float expectedInjury = 1f - state.currentHealth / state.maxHealth;
        if (!Mathf.Approximately(state.injurySeverity, expectedInjury))
        {
            report.AddError($"Character body-health '{state.characterId}' has an "
                + "inconsistent injury projection.");
        }
        CharacterBodyHealthState downedCandidate = stateRules.CloneState(state);
        stateRules.UpdateDowned(downedCandidate);
        if (downedCandidate.downed != state.downed)
        {
            report.AddError($"Character body-health '{state.characterId}' has an "
                + "inconsistent downed projection.");
        }

        if (!stateRules.TryResolveProfile(state.anatomyProfileId,
                out AnatomyProfileDefinition profile)
            || !string.Equals(profile.ProfileId, state.anatomyProfileId,
                StringComparison.Ordinal))
        {
            report.AddError($"Character body-health '{state.characterId}' references "
                + $"unknown anatomy profile '{state.anatomyProfileId}'.");
            return;
        }

        ValidateParts(state, report);
        ValidateAnatomyNodes(state, profile, report);
    }

    private static void ValidateParts(CharacterBodyHealthState state, DungeonGameRestoreReport report)
    {
        int requiredCount = Enum.GetValues(typeof(CombatBodyPart)).Length;
        if (state.parts == null || state.parts.Count != requiredCount)
        {
            report.AddError($"Character body-health '{state.characterId}' must contain "
                + $"exactly {requiredCount} body parts.");
            return;
        }

        HashSet<CombatBodyPart> bodyParts = new HashSet<CombatBodyPart>();
        int previous = -1;
        foreach (CharacterBodyPartHealthState part in state.parts)
        {
            int value = part == null ? -1 : (int)part.bodyPart;
            if (part == null
                || !Enum.IsDefined(typeof(CombatBodyPart), part.bodyPart)
                || value <= previous
                || !bodyParts.Add(part.bodyPart)
                || !IsFiniteInRange(part.maxHealth, 1f, float.MaxValue)
                || !IsFiniteInRange(part.currentHealth, 0f, part.maxHealth)
                || !IsFiniteInRange(part.bleedingPerSecond, 0f, float.MaxValue))
            {
                report.AddError($"Character body-health '{state.characterId}' has a null, "
                    + "duplicate, unordered, or invalid body part.");
                continue;
            }
            previous = value;
        }
    }

    private static void ValidateAnatomyNodes(CharacterBodyHealthState state,
        AnatomyProfileDefinition profile, DungeonGameRestoreReport report)
    {
        if (state.anatomyNodes == null
            || state.anatomyNodes.Count != profile.Nodes.Count)
        {
            report.AddError($"Character body-health '{state.characterId}' does not contain "
                + "the exact authored anatomy node set.");
            return;
        }

        HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);
        string previousNodeId = null;
        foreach (AnatomyNodeHealthState node in state.anatomyNodes)
        {
            string nodeId = node?.nodeId ?? string.Empty;
            bool validInstalledId = string.IsNullOrEmpty(node?.installedPartId)
                || node.installedPartId.StartsWith("surgical-part:", StringComparison.Ordinal);
            if (node == null
                || string.IsNullOrWhiteSpace(nodeId)
                || !string.Equals(nodeId, nodeId.Trim(), StringComparison.Ordinal)
                || !profile.TryGetNode(nodeId, out _)
                || !nodeIds.Add(nodeId)
                || previousNodeId != null
                    && string.CompareOrdinal(previousNodeId, nodeId) >= 0
                || !validInstalledId
                || node.installedPartId == null
                || !string.Equals(node.installedPartId, node.installedPartId.Trim(),
                    StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(SurgicalPartKind), node.installedPartKind)
                || !Enum.IsDefined(typeof(PartRecoveryPolicy), node.recoveryPolicy)
                || !IsFiniteInRange(node.maxHealth, 1f, float.MaxValue)
                || !IsFiniteInRange(node.currentHealth, 0f, node.maxHealth)
                || !IsFiniteInRange(node.bleedingPerSecond, 0f, float.MaxValue)
                || !IsFiniteInRange(node.infection, 0f, 100f)
                || !IsFiniteInRange(
                    node.installedPartEfficiency,
                    0f,
                    CharacterAnatomyStateBounds.MaximumInstalledPartEfficiency)
                || !IsFiniteInRange(node.rejectionBurden, 0f, 100f)
                || !IsFiniteInRange(node.mutationBurden, 0f, 100f)
                || !IsFiniteInRange(
                    node.moduleBonus,
                    CharacterAnatomyStateBounds.MinimumModuleBonus,
                    CharacterAnatomyStateBounds.MaximumModuleBonus)
                || node.missing && node.currentHealth > 0f)
            {
                report.AddError($"Character body-health '{state.characterId}' has a null, "
                    + "unknown, duplicate, unordered, or invalid anatomy node.");
                continue;
            }
            previousNodeId = nodeId;
        }
    }

    private static bool IsFiniteInRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static CharacterVitalsSnapshot DefaultVitals() =>
        new CharacterVitalsSnapshot(100f, 100f, 0f);

    private static void UpdateInjuryProjection(CharacterBodyHealthState state)
    {
        state.injurySeverity = Mathf.Clamp01(
            1f - state.currentHealth / Mathf.Max(1f, state.maxHealth));
    }
}

/// <summary>
/// Publishes the UI, mood, activity, lifecycle, and run effects produced by
/// authoritative vital changes without making <see cref="CharacterStats"/> own them.
/// </summary>
internal static class CharacterVitalsSideEffectAdapter
{
    internal static void NotifyDamage(
        CharacterStats owner,
        CharacterLog log,
        float amount,
        string reason,
        bool died,
        CharacterDeathCauseCode deathCause)
    {
        CharacterPerformanceSnapshot negativeMoodDuration = owner.EvaluatePerformance(
            CharacterPerformanceFormulaIds.NegativeMoodDuration);
        if (negativeMoodDuration.IsApplicable)
        {
            owner.ApplyMoodFactor(
                "health:injury",
                "몸을 다침",
                -Mathf.Clamp(amount * 0.25f, 2f, 10f),
                180f,
                2);
        }
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Damaged,
            string.IsNullOrWhiteSpace(reason)
                ? $"피해 {amount:0.#}"
                : $"피해 {amount:0.#}: {reason}",
            actionId: "health:damage",
            reasonCode: reason,
            value: amount,
            sentiment: -0.8f,
            bubbleEligible: true));

        if (died)
        {
            owner.NotifyAggregateDeath(
                deathCause,
                reason);
        }
    }

    internal static void NotifyHealing(
        CharacterStats owner,
        CharacterLog log,
        float amount)
    {
        owner.ApplyMoodFactor(
            "health:relief",
            "치료받아 안도함",
            Mathf.Clamp(amount * 0.15f, 1f, 6f),
            120f,
            1);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            $"회복 {amount:0.#}",
            actionId: "health:heal",
            value: amount,
            sentiment: 0.55f));
    }

    internal static float NotifyInjurySeverity(
        CharacterLog log,
        float value)
    {
        float injurySeverity = Mathf.Clamp01(value);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Changed,
            $"부상도 변경: {Mathf.RoundToInt(injurySeverity * 100f)}%",
            actionId: "health:injury-severity",
            value: injurySeverity,
            sentiment: -injurySeverity));
        return injurySeverity;
    }

    internal static void NotifyDeath(
        CharacterStats owner,
        CharacterActor actor,
        CharacterIdentity identity,
        CharacterVisual visual,
        CharacterLifecycle lifecycle,
        CharacterLog log,
        IGameEventBus gameEventBus,
        ICharacterDeathEventFactory deathEventFactory,
        IOwnerRunLifecycleService ownerRunLifecycleService,
        CharacterDeathCauseCode cause,
        string reasonCode)
    {
        if (lifecycle != null
            && lifecycle.CurrentState == CharacterLifecycleState.Despawned)
        {
            return;
        }

        visual?.SetRenderersVisible(true);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Defeated,
            string.IsNullOrWhiteSpace(reasonCode) ? "사망" : $"사망: {reasonCode}",
            actionId: "health:death",
            reasonCode: reasonCode,
            value: 1f,
            sentiment: -1f,
            bubbleEligible: true));
        lifecycle?.SetLifecycleState(CharacterLifecycleState.Despawned);

        (gameEventBus
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterBodyHealthStateRules)} requires {nameof(IGameEventBus)} before publishing a death."))
            .Publish((deathEventFactory
                ?? throw new InvalidOperationException(
                    $"{nameof(CharacterBodyHealthStateRules)} requires {nameof(ICharacterDeathEventFactory)} before publishing a death."))
                .Create(actor, cause));

        if (identity != null && identity.IsOwner && actor != null)
        {
            (ownerRunLifecycleService
                ?? throw new InvalidOperationException(
                    $"{nameof(CharacterBodyHealthStateRules)} requires {nameof(IOwnerRunLifecycleService)} for owner death handling."))
                .HandleOwnerDeath(actor, reasonCode);
        }
    }
}
