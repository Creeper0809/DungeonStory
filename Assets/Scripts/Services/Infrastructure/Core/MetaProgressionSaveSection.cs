using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaProgressionSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonMetaProgressionSaveData,
        MetaProgressionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "meta.progression";

    private readonly IMetaProgressionPersistencePort runtime;

    public MetaProgressionSaveSection(
        IMetaProgressionPersistencePort runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonMetaProgressionSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireObjectArrayFieldsWhenTopLevelBooleanTrue(
            payloadJson,
            "hasLatestResult",
            "latestResult",
            "completedMilestoneIds",
            "committedChoices");
        ValidatePendingCompletionJson(payloadJson);
    }

    public static void ValidatePendingCompletionJson(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings
        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["hasPendingCompletion"]?.Type != JTokenType.Boolean)
            throw new InvalidOperationException("meta-completion-flag-missing");
        if (root["hasPendingCompletion"].Value<bool>())
            ValidatePendingShape(root["pendingCompletion"], typeof(MetaRunCompletionSnapshotSaveData));
    }

    private static void ValidatePendingShape(JToken token, Type type)
    {
        if (token == null) throw new InvalidOperationException("meta-completion-field-missing:" + type.Name);
        bool valid;
        if (type == typeof(string)) valid = token.Type == JTokenType.String;
        else if (type == typeof(float)) valid = token.Type is JTokenType.Float or JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = token.Type == JTokenType.Integer;
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray array) throw new InvalidOperationException("meta-completion-array-missing");
            foreach (var value in array) ValidatePendingShape(value, type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not JObject row) throw new InvalidOperationException("meta-completion-object-missing");
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                ValidatePendingShape(row[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("meta-completion-field-invalid:" + type.Name);
    }

    protected override DungeonMetaProgressionSaveData CapturePayload()
    {
        DungeonMetaProgressionSaveData destination =
            new DungeonMetaProgressionSaveData();
        destination.lifetimeEarnedCurrency = runtime.State.LifetimeEarnedCurrency;
        destination.spentCurrency = runtime.State.SpentCurrency;
        destination.completedRunCount = runtime.State.CompletedRunCount;
        destination.upgradeLevels = runtime.State.UpgradeLevels
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DungeonStringIntSaveEntry
            {
                key = pair.Key,
                value = pair.Value
            })
            .ToList();
        destination.preservedRecipeIds = runtime.State.PreservedRecipeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        destination.runProgress = new DungeonMetaRunProgressSaveData
        {
            elapsedSeconds = runtime.RunProgress.ElapsedSeconds,
            currentDay = runtime.RunProgress.CurrentDay,
            settlementCount = runtime.RunProgress.SettlementCount,
            defendedInvasionCount = runtime.RunProgress.DefendedInvasionCount,
            maxThreatStage = runtime.RunProgress.MaxThreatStage,
            finalInvasionThreat = runtime.RunProgress.FinalInvasionThreat,
            offenseSuccessCount = runtime.RunProgress.OffenseSuccessCount,
            discoveredFacilityIds =
                runtime.RunProgress.DiscoveredFacilityIds.OrderBy(id => id).ToList(),
            unlockedRecipeIds = runtime.RunProgress.UnlockedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
        };
        destination.ended = runtime.HasEnded;
        destination.hasLatestResult = runtime.LatestResult != null;
        if (runtime.LatestResult != null)
        {
            destination.latestResult = ToSaveData(runtime.LatestResult);
        }
        var pending = runtime.PendingCompletion;
        destination.hasPendingCompletion = pending != null;
        if (pending != null)
            destination.pendingCompletion = new MetaRunCompletionSnapshotSaveData
            {
                fact = pending.Fact,
                runSequence = pending.RunSequence,
                preservedRecipeSlots = pending.PreservedRecipeSlots,
                result = ToSaveData(pending.Result),
                recipeCandidates = pending.RecipeCandidates.ToList()
            };

        return destination;
    }

    protected override MetaProgressionRestoreCandidate BuildRestoreCandidate(
        DungeonMetaProgressionSaveData payload) =>
        runtime.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        MetaProgressionRestoreCandidate candidate) =>
        runtime.Restore(candidate);

    private static DungeonRunResultSaveData ToSaveData(RunResultSnapshot result)
    {
        return new DungeonRunResultSaveData
        {
            ownerName = result.ownerName,
            endReason = result.endReason,
            survivalSeconds = result.survivalSeconds,
            survivedOperatingDays = result.survivedOperatingDays,
            settlementCount = result.settlementCount,
            defendedInvasionCount = result.defendedInvasionCount,
            maxThreatStage = result.maxThreatStage,
            finalInvasionThreat = result.finalInvasionThreat,
            firstDiscoveredFacilityCount = result.firstDiscoveredFacilityCount,
            firstUnlockedRecipeCount = result.firstUnlockedRecipeCount,
            offenseSuccessCount = result.offenseSuccessCount,
            difficultyMultiplier = result.difficultyMultiplier,
            difficulty = result.difficulty,
            survivalPressure = result.survivalPressure,
            legacyCurrency = result.legacyCurrency,
            outcome = result.outcome,
            completedMilestoneIds = result.completedMilestoneIds.ToList(),
            committedChoices = result.committedChoices
                .Select(value => new DungeonCommittedRunChoiceSaveData
                {
                    kind = value.Kind,
                    ownerId = value.OwnerId,
                    definitionId = value.DefinitionId,
                    instanceId = value.InstanceId,
                    choiceId = value.ChoiceId,
                    operationId = value.OperationId,
                    ordinal = value.Ordinal
                })
                .ToList()
        };
    }

}
