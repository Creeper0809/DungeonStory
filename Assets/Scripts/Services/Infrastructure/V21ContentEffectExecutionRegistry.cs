using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public sealed class V21ContentEffectExecutionRegistry
{
    // Registrations are immutable. The same contract drives execution, rollback
    // requirements and authoritative receipt observation; there is no global override.
    internal static readonly V21ContentEffectExecutionRegistry Default =
        new(V20ContentEffectHandlers.Create());

    private readonly Dictionary<V20ContentEffectKind, V20ContentEffectHandler> handlers;

    internal V21ContentEffectExecutionRegistry(IEnumerable<V20ContentEffectHandler> registrations)
    {
        if (registrations == null) throw new ArgumentNullException(nameof(registrations));
        handlers = new Dictionary<V20ContentEffectKind, V20ContentEffectHandler>();
        foreach (V20ContentEffectHandler handler in registrations)
            if (handler == null || !handlers.TryAdd(handler.Kind, handler))
                throw new InvalidOperationException("Null or duplicate content effect handler.");
        Handlers = Array.AsReadOnly(handlers.Values.ToArray());
    }

    internal IReadOnlyList<V20ContentEffectHandler> Handlers { get; }
    internal bool TryGet(V20ContentEffectKind kind, out V20ContentEffectHandler handler) =>
        handlers.TryGetValue(kind, out handler);
    internal V20ContentEffectHandler Require(V20ContentEffectKind kind) =>
        handlers.TryGetValue(kind, out V20ContentEffectHandler handler)
            ? handler
            : throw new InvalidOperationException($"Content effect '{kind}' has no registered handler.");

    public static bool HasExecutionOwner(V20ContentEffectKind kind) =>
        Default.TryGet(kind, out _);

    public static string DescribeOwner(V20ContentEffectKind kind) =>
        Default.TryGet(kind, out V20ContentEffectHandler handler) ? handler.Owner : string.Empty;
}

internal enum V20ContentEffectStage { Typed, Campaign, Physical, Retirement }

[Flags]
internal enum V20ContentEffectPersistenceOwners
{
    None = 0, BodyHealth = 1, Psychosocial = 2, PopulationHealth = 4, Narrative = 8
}

internal sealed class V20ContentEffectHandler
{
    internal V20ContentEffectHandler(
        V20ContentEffectKind kind, string owner, string semantic, V20ContentEffectStage stage,
        Func<V20ContentEffectObservation, string> observe,
        Func<V20ContentEffectObservation, V20ContentEffectObservation, float> appliedAmount,
        int minimumParticipants = 0, bool participantTargeted = false, bool forwardRecovery = false,
        V20ContentEffectPersistenceOwners persistenceOwners = V20ContentEffectPersistenceOwners.None,
        Action<V20ContentTypedEffectRuntime, V20ContentEffectInvocation> apply = null,
        Action<V20CampaignEffectRuntime, V20ContentEffect, string> applyCampaign = null,
        Func<V20ContentTypedEffectRuntime, V20ContentEffectInvocation, DomainFailure> preflight = null,
        Func<V20ContentEffect, float> expectedAmount = null, bool skipUnchanged = false,
        Func<V20ContentEffectObservation, V20ContentEffectObservation, string> resolveTarget = null,
        GameplayMetricUnitId? metricUnit = null)
    {
        if (kind == V20ContentEffectKind.None || !Enum.IsDefined(typeof(V20ContentEffectKind), kind)
            || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(semantic)
            || !Enum.IsDefined(typeof(V20ContentEffectStage), stage)
            || minimumParticipants < 0 || minimumParticipants > 0 && !participantTargeted
            || metricUnit.HasValue && !metricUnit.Value.IsValid
            || (persistenceOwners & ~(V20ContentEffectPersistenceOwners.BodyHealth
                | V20ContentEffectPersistenceOwners.Psychosocial | V20ContentEffectPersistenceOwners.PopulationHealth
                | V20ContentEffectPersistenceOwners.Narrative)) != 0
            || stage == V20ContentEffectStage.Typed && apply == null
            || stage == V20ContentEffectStage.Campaign && applyCampaign == null
            || stage != V20ContentEffectStage.Typed && apply != null
            || stage != V20ContentEffectStage.Campaign && applyCampaign != null)
            throw new ArgumentException("Content effect registration is incomplete or has an invalid execution stage.");
        Kind = kind;
        Owner = owner;
        Semantic = semantic;
        Stage = stage;
        Observe = observe ?? throw new ArgumentNullException(nameof(observe));
        AppliedAmount = appliedAmount ?? throw new ArgumentNullException(nameof(appliedAmount));
        MinimumParticipants = minimumParticipants;
        ParticipantTargeted = participantTargeted;
        RequiresForwardRecovery = forwardRecovery;
        PersistenceOwners = persistenceOwners;
        Apply = apply;
        ApplyCampaign = applyCampaign;
        Preflight = preflight;
        ExpectedAmount = expectedAmount ?? (effect => effect.amount);
        SkipUnchanged = skipUnchanged;
        ResolveTarget = resolveTarget;
        MetricUnit = metricUnit ?? MigratedProducerOutcomeIds.PointUnit;
    }

    internal V20ContentEffectKind Kind { get; }
    internal string Owner { get; }
    internal string Semantic { get; }
    internal V20ContentEffectStage Stage { get; }
    internal int MinimumParticipants { get; }
    internal bool ParticipantTargeted { get; }
    internal bool RequiresForwardRecovery { get; }
    internal V20ContentEffectPersistenceOwners PersistenceOwners { get; }
    internal Action<V20ContentTypedEffectRuntime, V20ContentEffectInvocation> Apply { get; }
    internal Action<V20CampaignEffectRuntime, V20ContentEffect, string> ApplyCampaign { get; }
    internal Func<V20ContentTypedEffectRuntime, V20ContentEffectInvocation, DomainFailure> Preflight { get; }
    internal Func<V20ContentEffectObservation, string> Observe { get; }
    internal Func<V20ContentEffectObservation, V20ContentEffectObservation, float> AppliedAmount { get; }
    internal Func<V20ContentEffect, float> ExpectedAmount { get; }
    internal bool SkipUnchanged { get; }
    internal GameplayMetricUnitId MetricUnit { get; }
    internal Func<V20ContentEffectObservation, V20ContentEffectObservation, string> ResolveTarget { get; }
}

internal readonly struct V20ContentEffectInvocation
{
    internal V20ContentEffectInvocation(int absoluteDay, V20ResolvedEventResult resolution,
        V20ContentEffect effect, CharacterActor[] participants)
    {
        AbsoluteDay = Math.Max(1, absoluteDay);
        Resolution = resolution;
        Effect = effect;
        Participants = participants;
    }
    internal int AbsoluteDay { get; }
    internal V20ResolvedEventResult Resolution { get; }
    internal V20ContentEffect Effect { get; }
    internal CharacterActor[] Participants { get; }
}

internal readonly struct V20ContentEffectObservation
{
    internal V20ContentEffectObservation(V20ContentEffectOwnerSnapshot snapshot,
        V20ResolvedEventResult resolution, V20ContentEffect effect,
        string targetId, IReadOnlyList<string> participants)
    {
        Snapshot = snapshot;
        Resolution = resolution;
        Effect = effect;
        TargetId = targetId;
        Participants = participants;
    }
    internal V20ContentEffectOwnerSnapshot Snapshot { get; }
    internal V20ResolvedEventResult Resolution { get; }
    internal V20ContentEffect Effect { get; }
    internal string TargetId { get; }
    internal IReadOnlyList<string> Participants { get; }
}

