using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Balance;

[BalanceCaptureFactory]
public sealed class SurvivalContinuityCatalogQuery :
    ISurvivalContinuityCatalogQuery
{
    public const string GrainPorridgeItemId = "food:grain-porridge";
    public const float SafeDrinkRecovery = 65f;
    private const decimal EffectiveWuPerAdultDay = 45m;

    private readonly IResourceEconomyContentCatalog content;
    private readonly ICharacterNeedBalanceRuntime needs;

    public SurvivalContinuityCatalogQuery(
        IResourceEconomyContentCatalog content,
        ICharacterNeedBalanceRuntime needs)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.needs = needs ?? throw new ArgumentNullException(nameof(needs));
    }

    public IReadOnlyList<SurvivalContinuityPathSnapshot> CapturePaths(
        PopulationStageContext context)
    {
        ResourceItemDefinitionSO meal = RequireItem(GrainPorridgeItemId);
        ResourceItemDefinitionSO water = RequireItem(
            PrimitiveSurvivalBalanceAuthority.CleanWaterItemId);
        if (!meal.IsMeal || meal.Nutrition <= 0f)
            throw new InvalidOperationException("The field-meal fallback has no nutrition authority.");
        if (water.MaxStack <= 0)
            throw new InvalidOperationException("Clean water has no physical stack authority.");

        List<SurvivalContinuityPathSnapshot> values = new()
        {
            Primary("service:food", "facility:meal-service", GrainPorridgeItemId),
            Primitive(
                context,
                "service:food",
                "survival:field-meal",
                CharacterCondition.HUNGER,
                meal.Nutrition,
                PrimitiveSurvivalBalanceAuthority.FieldMealSeconds,
                GrainPorridgeItemId,
                1,
                moodDelta: meal.MealMood),
            Primary("service:water", "facility:safe-drink", string.Empty),
            Primitive(
                context,
                "service:water",
                "survival:safe-drink",
                CharacterCondition.THIRST,
                SafeDrinkRecovery,
                0f,
                PrimitiveSurvivalBalanceAuthority.CleanWaterItemId,
                1,
                moodDelta: 2f),
            Primary("service:sleep", "facility:bed", string.Empty),
            Primitive(
                context,
                "service:sleep",
                "survival:floor-rest",
                CharacterCondition.SLEEP,
                PrimitiveSurvivalBalanceAuthority.FloorRestRecovery,
                PrimitiveSurvivalBalanceAuthority.FloorRestSeconds,
                string.Empty,
                0,
                PrimitiveSurvivalBalanceAuthority.FloorRestMoodDelta,
                PrimitiveSurvivalBalanceAuthority.FloorRestHygieneDelta),
            Primary("service:hygiene", "facility:hygiene", string.Empty),
            Primitive(
                context,
                "service:hygiene",
                "survival:bucket-wash",
                CharacterCondition.HYGIENE,
                PrimitiveSurvivalBalanceAuthority.BucketWashRecovery,
                PrimitiveSurvivalBalanceAuthority.BucketWashSeconds,
                PrimitiveSurvivalBalanceAuthority.CleanWaterItemId,
                1),
            Primary("service:excretion", "facility:toilet", string.Empty),
            Primitive(
                context,
                "service:excretion",
                "survival:primitive-latrine",
                CharacterCondition.EXCRETION,
                PrimitiveSurvivalBalanceAuthority.LatrineRecovery,
                PrimitiveSurvivalBalanceAuthority.LatrineSeconds,
                string.Empty,
                0,
                PrimitiveSurvivalBalanceAuthority.LatrineMoodDelta,
                PrimitiveSurvivalBalanceAuthority.LatrineHygieneDelta,
                PrimitiveSurvivalBalanceAuthority.LatrineWaste,
                PrimitiveSurvivalBalanceAuthority.LatrineStain)
        };
        return values
            .OrderBy(value => value.ServiceId, StringComparer.Ordinal)
            .ThenBy(value => value.PathId, StringComparer.Ordinal)
            .ToArray();
    }

    private ResourceItemDefinitionSO RequireItem(string itemId)
    {
        return content.TryGetItem(itemId, out ResourceItemDefinitionSO item)
            ? item
            : throw new InvalidOperationException(
                $"Survival continuity item authority is missing: {itemId}.");
    }

    private static SurvivalContinuityPathSnapshot Primary(
        string serviceId,
        string pathId,
        string requiredItemId) => new(
        serviceId,
        pathId,
        isPrimitive: false,
        capacityPermille: 1000,
        recurringMilliWuPerDay: 0,
        requiredPhysicalItemIds: string.IsNullOrEmpty(requiredItemId)
            ? Array.Empty<string>()
            : new[] { requiredItemId });

    private SurvivalContinuityPathSnapshot Primitive(
        PopulationStageContext context,
        string serviceId,
        string pathId,
        CharacterCondition condition,
        float recovery,
        float durationSeconds,
        string requiredItemId,
        int physicalInputQuantity,
        float moodDelta = 0f,
        float hygieneDelta = 0f,
        float waste = 0f,
        float stain = 0f)
    {
        decimal recoveryUnits = Finite(recovery, serviceId + ":recovery");
        decimal duration = Finite(durationSeconds, serviceId + ":duration");
        decimal daySeconds = Finite(needs.DayLengthSeconds, "survival:day-seconds");
        decimal dailyDepletion = checked(
            Finite(
                needs.GetDailyDepletion(condition),
                serviceId + ":daily-depletion")
            + Finite(
                needs.GetWorkDepletion(condition, (float)daySeconds),
                serviceId + ":work-depletion"));
        if (dailyDepletion <= 0m || recoveryUnits <= 0m || daySeconds <= 0m)
        {
            throw new InvalidOperationException(
                $"Survival continuity authority is non-positive: {serviceId}.");
        }
        decimal actionsPerDay = checked(
            context.Population * dailyDepletion / recoveryUnits);
        decimal opportunityWu = checked(
            actionsPerDay * duration / daySeconds * EffectiveWuPerAdultDay);
        return new SurvivalContinuityPathSnapshot(
            serviceId,
            pathId,
            isPrimitive: true,
            capacityPermille: 1000,
            recurringMilliWuPerDay: checked((int)decimal.Ceiling(opportunityWu * 1000m)),
            requiredPhysicalItemIds: string.IsNullOrEmpty(requiredItemId)
                ? Array.Empty<string>()
                : new[] { requiredItemId },
            actionDurationMilliseconds: checked((int)decimal.Ceiling(duration * 1000m)),
            physicalInputQuantity: physicalInputQuantity,
            recoveryMilliUnits: checked((int)decimal.Ceiling(recoveryUnits * 1000m)),
            moodDeltaMilliUnits: Milli(moodDelta),
            hygieneDeltaMilliUnits: Milli(hygieneDelta),
            wasteMilliUnits: PositiveMilli(waste),
            stainMilliUnits: PositiveMilli(stain));
    }

    private static decimal Finite(float value, string context)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidOperationException($"Non-finite continuity value: {context}.");
        return (decimal)value;
    }

    private static int Milli(float value) => checked((int)decimal.Round(
        Finite(value, "continuity:delta") * 1000m,
        0,
        MidpointRounding.AwayFromZero));

    private static int PositiveMilli(float value)
    {
        decimal decimalValue = Finite(value, "continuity:positive");
        if (decimalValue < 0m)
            throw new InvalidOperationException("A positive continuity cost was negative.");
        return checked((int)decimal.Ceiling(decimalValue * 1000m));
    }
}
