using System;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public enum PrimitiveSurvivalTransition
{
    Started,
    Failed,
    MealSourceChanged,
    PathSearchPending,
    YieldedToFacility,
    FieldMealCompleted,
    Completed
}

/// <summary>
/// A transient notification of a primitive-survival decision or completion.
/// Gameplay state and durable publication remain owned by the runner.
/// </summary>
public readonly struct PrimitiveSurvivalChangedEvent
{
    private readonly PrimitiveSurvivalPendingSaveData pending;

    public PrimitiveSurvivalChangedEvent(
        PrimitiveSurvivalTransition transition,
        CharacterActor actor,
        string kind = "",
        CharacterActionIntentKind intentKind = CharacterActionIntentKind.None,
        long epoch = 0L,
        Vector2Int position = default,
        string phase = "",
        int sourceRevision = 0,
        string sourceStackId = "",
        Vector2Int sourcePosition = default,
        FacilityRole facilityRole = FacilityRole.None,
        string reasonCode = "",
        string detail = "",
        string displayName = "",
        float recovery = 0f,
        PrimitiveSurvivalPendingSaveData pending = null,
        string publicationId = "")
    {
        Transition = transition;
        Actor = actor;
        Kind = kind ?? string.Empty;
        IntentKind = intentKind;
        Epoch = epoch;
        Position = position;
        Phase = phase ?? string.Empty;
        SourceRevision = sourceRevision;
        SourceStackId = sourceStackId ?? string.Empty;
        SourcePosition = sourcePosition;
        FacilityRole = facilityRole;
        ReasonCode = reasonCode ?? string.Empty;
        Detail = detail ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Recovery = recovery;
        this.pending = pending?.Clone();
        PublicationId = publicationId ?? string.Empty;
        Validate();
    }

    public PrimitiveSurvivalTransition Transition { get; }
    public CharacterActor Actor { get; }
    public string Kind { get; }
    public CharacterActionIntentKind IntentKind { get; }
    public long Epoch { get; }
    public Vector2Int Position { get; }
    public string Phase { get; }
    public int SourceRevision { get; }
    public string SourceStackId { get; }
    public Vector2Int SourcePosition { get; }
    public FacilityRole FacilityRole { get; }
    public string ReasonCode { get; }
    public string Detail { get; }
    public string DisplayName { get; }
    public float Recovery { get; }
    public PrimitiveSurvivalPendingSaveData Pending => pending?.Clone();
    public string PublicationId { get; }

    internal void Validate()
    {
        if (!Enum.IsDefined(typeof(PrimitiveSurvivalTransition), Transition)
            || Actor == null
            || !CharacterPersistentIdentity.TryGet(Actor, out CharacterId actorId)
            || !Enum.IsDefined(typeof(CharacterActionIntentKind), IntentKind)
            || !Enum.IsDefined(typeof(FacilityRole), FacilityRole)
            || Epoch < 0L
            || SourceRevision < 0
            || !float.IsFinite(Recovery)
            || Recovery < 0f)
        {
            throw Invalid();
        }

        switch (Transition)
        {
            case PrimitiveSurvivalTransition.Started:
                if (!PrimitiveSurvivalKindNames.IsValid(Kind)
                    || IntentKind == CharacterActionIntentKind.None
                    || Epoch <= 0L)
                    throw Invalid();
                break;
            case PrimitiveSurvivalTransition.Failed:
                if (!PrimitiveSurvivalKindNames.IsValid(Kind)
                    || string.IsNullOrWhiteSpace(ReasonCode)
                    || string.IsNullOrWhiteSpace(Detail))
                {
                    throw Invalid();
                }
                break;
            case PrimitiveSurvivalTransition.MealSourceChanged:
                if (string.IsNullOrWhiteSpace(Phase)
                    || string.IsNullOrWhiteSpace(SourceStackId))
                {
                    throw Invalid();
                }
                break;
            case PrimitiveSurvivalTransition.PathSearchPending:
            case PrimitiveSurvivalTransition.YieldedToFacility:
                if (!PrimitiveSurvivalKindNames.IsValid(Kind)
                    || string.IsNullOrWhiteSpace(Phase)
                    || FacilityRole != PrimitiveSurvivalKindNames.FacilityRoleFor(Kind))
                {
                    throw Invalid();
                }
                break;
            case PrimitiveSurvivalTransition.FieldMealCompleted:
                if (string.IsNullOrWhiteSpace(SourceStackId)
                    || string.IsNullOrWhiteSpace(DisplayName))
                {
                    throw Invalid();
                }
                break;
            case PrimitiveSurvivalTransition.Completed:
                ValidateCompletion(actorId);
                break;
            default:
                throw Invalid();
        }
    }

    private void ValidateCompletion(CharacterId actorId)
    {
        if (pending == null
            || !pending.IsPending
            || !PrimitiveSurvivalKindNames.IsCompletionActionId(
                pending.actionId)
            || !DomainFactValueSyntax.IsIdentity(PublicationId))
        {
            throw Invalid();
        }

        try
        {
            PrimitiveSurvivalCompletedFact.ValidateSaved(
                pending,
                actorId.Value);
        }
        catch (InvalidOperationException)
        {
            throw Invalid();
        }
    }

    private static ArgumentException Invalid() =>
        new("primitive-survival-event-invalid");
}

internal static class PrimitiveSurvivalKindNames
{
    internal const string FieldMeal = "FieldMeal";
    internal const string FloorRest = "FloorRest";
    internal const string Latrine = "Latrine";
    internal const string BucketWash = "BucketWash";

    internal static bool IsValid(string value) =>
        string.Equals(value, FieldMeal, StringComparison.Ordinal)
        || string.Equals(value, FloorRest, StringComparison.Ordinal)
        || string.Equals(value, Latrine, StringComparison.Ordinal)
        || string.Equals(value, BucketWash, StringComparison.Ordinal);

    internal static bool IsFloorRest(string value) =>
        string.Equals(value, FloorRest, StringComparison.Ordinal);

    internal static bool IsLatrine(string value) =>
        string.Equals(value, Latrine, StringComparison.Ordinal);

    internal static bool IsBucketWash(string value) =>
        string.Equals(value, BucketWash, StringComparison.Ordinal);

    internal static FacilityRole FacilityRoleFor(string value)
    {
        if (string.Equals(value, FieldMeal, StringComparison.Ordinal))
            return FacilityRole.Meal;
        if (IsFloorRest(value))
            return FacilityRole.Rest;
        if (IsLatrine(value))
            return FacilityRole.Toilet;
        if (IsBucketWash(value))
            return FacilityRole.Hygiene;
        return FacilityRole.None;
    }

    internal static bool IsCompletionActionId(string value) =>
        string.Equals(value, "survival:floor-rest", StringComparison.Ordinal)
        || string.Equals(
            value,
            "survival:primitive-latrine",
            StringComparison.Ordinal)
        || string.Equals(value, "survival:bucket-wash", StringComparison.Ordinal);
}
