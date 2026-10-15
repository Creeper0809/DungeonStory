using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class OffenseExpeditionResultFactMapping
{
    private const string DigestSchema =
        "offense-expedition-result-fact-v2";

    private static readonly JsonSerializerSettings DigestSettings = new()
    {
        Culture = CultureInfo.InvariantCulture,
        DefaultValueHandling = DefaultValueHandling.Include,
        Formatting = Formatting.None,
        MaxDepth = 128,
        NullValueHandling = NullValueHandling.Include,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        TypeNameHandling = TypeNameHandling.None
    };

    public static MigratedDomainFactCapture Capture(
        OffenseExpeditionResolvedFact fact)
    {
        OffenseExpeditionResolvedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        OffenseExpeditionResultFactData data = value.Snapshot();
        DungeonOffenseExpeditionResultSaveData result = data.result;
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OffenseExpeditionResult,
            "offense-expedition-result:" + result.expeditionId,
            Math.Max(1, data.absoluteDay),
            result.success
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            CreateDetails(data, Digest(data)));
    }

    public static MigratedProducerOutcomeDetails Details(
        OffenseExpeditionResolvedFact fact)
    {
        OffenseExpeditionResolvedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        OffenseExpeditionResultFactData data = value.Snapshot();
        return CreateDetails(data, Digest(data));
    }

    public static string Digest(OffenseExpeditionResolvedFact fact) =>
        Digest((fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot());

    private static MigratedProducerOutcomeDetails CreateDetails(
        OffenseExpeditionResultFactData data,
        string digest)
    {
        DungeonOffenseExpeditionResultSaveData result = data.result;
        var subjects = new List<MigratedProducerOutcomeSubject>();
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.ExpeditionKind,
            result.expeditionId,
            result.targetTitle,
            MigratedProducerOutcomeIds.ExpeditionRole);
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.WorldLocationKind,
            result.targetId,
            result.targetTitle,
            MigratedProducerOutcomeIds.TargetRole);
        if (!string.IsNullOrWhiteSpace(data.regionId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.WorldLocationKind,
                data.regionId,
                data.regionDisplayName,
                MigratedProducerOutcomeIds.DestinationRole);
        }
        if (!string.IsNullOrWhiteSpace(data.factionId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.FactionKind,
                data.factionId,
                data.factionId,
                MigratedProducerOutcomeIds.FactionRole);
        }
        foreach (OffenseExpeditionParticipantFactData participant in
                 data.participants)
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.CharacterKind,
                participant.characterId,
                participant.displayName,
                MigratedProducerOutcomeIds.MemberRole);
        }
        foreach (string itemId in result.itemReceipts
                     .Where(value => value != null)
                     .Select(value => value.itemId)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                itemId,
                itemId,
                MigratedProducerOutcomeIds.RewardRole);
        }
        foreach (string currencyId in result.currencyReceipts
                     .Where(value => value != null)
                     .Select(value => value.currencyId)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                currencyId,
                currencyId,
                MigratedProducerOutcomeIds.RewardRole);
        }
        foreach (DungeonOffenseArrivalReceiptSaveData arrival in
                 result.arrivalReceipts.Where(value => value != null))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.OperationKind,
                arrival.arrivalId,
                arrival.kind,
                MigratedProducerOutcomeIds.ArrivalRole);
        }

        GameplayEntityId expeditionEntity = subjects[0].EntityId;
        var metrics = new List<GameplayOutcomeMetric>
        {
            Metric(
                MigratedProducerOutcomeIds.PowerMetric,
                result.totalPower,
                MigratedProducerOutcomeIds.PointUnit,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.RequiredPowerMetric,
                result.requiredPower,
                MigratedProducerOutcomeIds.PointUnit,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.DangerMetric,
                result.danger,
                MigratedProducerOutcomeIds.PointUnit,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.DurationMetric,
                result.elapsedSeconds,
                MigratedProducerOutcomeIds.SecondUnit,
                expeditionEntity)
        };
        foreach (IGrouping<string, DungeonOffenseItemReceiptSaveData> item in
                 result.itemReceipts.Where(value => value != null)
                     .GroupBy(value => value.itemId, StringComparer.Ordinal))
        {
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.QuantityMetric,
                item.Sum(value => value.quantity),
                MigratedProducerOutcomeIds.CountUnit,
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.ItemDefinitionKind,
                    item.Key)));
        }
        foreach (IGrouping<string, DungeonOffenseCurrencyReceiptSaveData>
                 currency in result.currencyReceipts
                     .Where(value => value != null)
                     .GroupBy(value => value.currencyId, StringComparer.Ordinal))
        {
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.AmountMetric,
                currency.Sum(value => value.amount),
                MigratedProducerOutcomeIds.CurrencyUnit,
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.ItemDefinitionKind,
                    currency.Key)));
        }
        foreach (DungeonOffenseArrivalReceiptSaveData arrival in
                 result.arrivalReceipts.Where(value => value != null))
        {
            GameplayEntityId reference = new(
                MigratedProducerOutcomeIds.OperationKind,
                arrival.arrivalId);
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.SecuredCountMetric,
                arrival.securedAmount,
                MigratedProducerOutcomeIds.CountUnit,
                reference));
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.EscapedCountMetric,
                arrival.escapedAmount,
                MigratedProducerOutcomeIds.CountUnit,
                reference));
        }

        var facts = new List<GameplayOutcomeFact>
        {
            new(MigratedProducerOutcomeIds.TargetFact, result.targetId),
            new(
                MigratedProducerOutcomeIds.CampaignFact,
                "advanced=" + data.campaignAdvanced
                + "; worldSiteResolved=" + data.worldSiteResolved)
        };
        foreach (DungeonOffenseRewardGrantSaveData reward in
                 result.grantedRewards.Where(value => value != null))
        {
            facts.Add(RewardFact(
                "category=" + reward.category
                + "; label=" + TextOrNone(reward.label)
                + "; requested=" + Invariant(reward.requestedAmount)
                + "; granted=" + Invariant(reward.grantedAmount)
                + "; success=" + reward.success));
        }
        foreach (DungeonOffenseItemReceiptSaveData item in
                 result.itemReceipts.Where(value => value != null))
        {
            facts.Add(RewardFact(
                "item=" + item.itemId + "; kind=" + item.kind
                + "; quantity=" + Invariant(item.quantity)
                + "; instance=" + TextOrNone(item.instanceId)
                + "; durabilityLoss=" + item.durabilityLoss.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)));
        }
        foreach (DungeonOffenseTreatmentReceiptSaveData treatment in
                 result.treatmentReceipts.Where(value => value != null))
        {
            facts.Add(RewardFact(
                "treatment=" + treatment.kind
                + "; character=" + treatment.characterId
                + "; anatomy=" + TextOrNone(treatment.anatomyNodeId)
                + "; healed=" + treatment.healedAmount.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)));
        }
        foreach (DungeonOffenseCurrencyReceiptSaveData currency in
                 result.currencyReceipts.Where(value => value != null))
        {
            facts.Add(RewardFact(
                "currency=" + currency.currencyId
                + "; amount=" + Invariant(currency.amount)
                + "; operation=" + TextOrNone(currency.operationId)));
        }
        foreach (DungeonOffenseArrivalReceiptSaveData arrival in
                 result.arrivalReceipts.Where(value => value != null))
        {
            facts.Add(RewardFact(
                "arrival=" + arrival.arrivalId + "; kind=" + arrival.kind
                + "; requested=" + Invariant(arrival.requestedAmount)
                + "; materialized=" + Invariant(arrival.materializedAmount)
                + "; secured=" + Invariant(arrival.securedAmount)
                + "; escaped=" + Invariant(arrival.escapedAmount)
                + "; resolution=" + arrival.resolution));
        }
        string[] rewardDetails = facts
            .Where(value => value.FactId.Equals(
                MigratedProducerOutcomeIds.RewardFact))
            .Select((value, index) =>
                "entry[" + Invariant(index) + "]=" + value.Value)
            .ToArray();
        facts.RemoveAll(value => value.FactId.Equals(
            MigratedProducerOutcomeIds.RewardFact));
        if (rewardDetails.Length > 0)
        {
            string aggregate = string.Join(" | ", rewardDetails);
            if (aggregate.Length
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length)
            {
                throw new InvalidOperationException(
                    "Offense expedition reward evidence exceeds the fixed ledger fact-value capacity: utf16="
                    + Invariant(aggregate.Length) + ".");
            }
            facts.Add(RewardFact(aggregate));
        }

        MigratedProducerOutcomeSubject[] subjectArray = subjects.ToArray();
        GameplayOutcomeMetric[] metricArray = metrics.ToArray();
        GameplayOutcomeFact[] factArray = facts.ToArray();
        if (subjectArray.Length is < 1 or > 16
            || metricArray.Length > 16
            || factArray.Length > 14)
        {
            throw new InvalidOperationException(
                "Offense detailed outcome exceeds the fixed ledger capacity: "
                + "subjects=" + Invariant(subjectArray.Length)
                + "; metrics=" + Invariant(metricArray.Length)
                + "; facts=" + Invariant(factArray.Length) + ".");
        }
        return new MigratedProducerOutcomeDetails(
            subjectArray,
            metricArray,
            factArray,
            "원정 종결: expedition=" + result.expeditionId
            + "; target=" + result.targetId
            + "; success=" + result.success
            + "; members=" + Invariant(data.memberCount)
            + "; rewards=" + Invariant(result.grantedRewards.Count)
            + "; items=" + Invariant(result.itemReceipts.Count)
            + "; arrivals=" + Invariant(result.arrivalReceipts.Count)
            + "; currency=" + Invariant(result.currencyReceipts.Count),
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(result.expeditionId),
                "expedition-terminal-resolved"),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "domain-fact",
                    digest)
            });
    }

    private static string Digest(OffenseExpeditionResultFactData data)
    {
        Newtonsoft.Json.JsonSerializer serializer =
            Newtonsoft.Json.JsonSerializer.Create(DigestSettings);
        JToken token = JToken.FromObject(data, serializer);
        string canonical = Canonicalize(token).ToString(Formatting.None);
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        digest.Append(canonical);
        return digest.ComputeSha256();
    }

    private static JToken Canonicalize(JToken token)
    {
        if (token is JObject objectValue)
        {
            return new JObject(objectValue.Properties()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(
                    property.Name,
                    Canonicalize(property.Value))));
        }
        if (token is JArray arrayValue)
            return new JArray(arrayValue.Select(Canonicalize));
        return token.DeepClone();
    }

    private static void AddSubject(
        ICollection<MigratedProducerOutcomeSubject> subjects,
        GameplayEntityKindId kind,
        string stableId,
        string displayName,
        GameplayRoleId role)
    {
        string normalized = stableId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return;
        var candidate = new MigratedProducerOutcomeSubject(
            kind,
            normalized,
            string.IsNullOrWhiteSpace(displayName)
                ? normalized
                : displayName,
            role);
        if (subjects.Any(value => value.EntityId == candidate.EntityId))
            return;
        subjects.Add(candidate);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);

    private static GameplayOutcomeFact RewardFact(string value) => new(
        MigratedProducerOutcomeIds.RewardFact,
        value);

    private static string TextOrNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string Invariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
