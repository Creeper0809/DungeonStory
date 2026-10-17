using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class ApparelWorkCompletedFactMapping
{
    private static readonly GameplayOutcomeFactId KindFact =
        new("apparel-order-kind");
    private static readonly GameplayOutcomeFactId MaterialSourcesFact =
        new("material-source-stack-ids");

    public static MigratedDomainFactCapture Capture(
        ApparelWorkCompletedFact fact)
    {
        ApparelWorkCompletedData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        data.Validate();

        ApparelWorkOrderOutcomeItemStateSaveData[] states = data.itemStates
            .OrderBy(value => value.itemInstanceId, StringComparer.Ordinal)
            .ToArray();
        var subjects = new List<MigratedProducerOutcomeSubject>(
            states.Length + 1)
        {
            new(
                MigratedProducerOutcomeIds.FacilityKind,
                data.facilityInstanceId,
                "의복 작업 시설",
                MigratedProducerOutcomeIds.FacilityRole)
        };
        foreach (ApparelWorkOrderOutcomeItemStateSaveData state in states)
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.ItemKind,
                state.itemInstanceId,
                state.apparelDisplayName,
                MigratedProducerOutcomeIds.ItemRole));
        }

        GameplayEntityId facilitySubject = subjects[0].EntityId;
        GameplayEntityId itemSubject = subjects[1].EntityId;
        var facts = new List<GameplayOutcomeFact>
        {
            Fact(MigratedProducerOutcomeIds.WorkOrderFact, data.orderId),
            Fact(
                MigratedProducerOutcomeIds.FacilityFact,
                data.facilityInstanceId),
            Fact(KindFact, data.kind.ToString()),
            Fact(
                MigratedProducerOutcomeIds.BatchFact,
                string.IsNullOrEmpty(data.materialCommitId)
                    ? "none"
                    : data.materialCommitId),
            Fact(
                MaterialSourcesFact,
                data.materialSourceStackIds.Count == 0
                    ? "none"
                    : string.Join(",", data.materialSourceStackIds
                        .OrderBy(value => value, StringComparer.Ordinal))),
            Fact(
                MigratedProducerOutcomeIds.ContributorsFact,
                CanonicalContributors(data.contributions))
        };
        facts.AddRange(StateFacts(
            "apparel-state-before-",
            states,
            value => value.beforeStatePayload));
        facts.AddRange(StateFacts(
            "apparel-state-after-",
            states,
            value => value.afterStatePayload));
        if (facts.Count > 14)
        {
            throw new InvalidOperationException(
                "apparel-terminal-fact-capacity-exceeded");
        }

        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CountMetric,
                    states.Length,
                    MigratedProducerOutcomeIds.CountUnit,
                    itemSubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CompletedWorkMetric,
                    Math.Round(data.completedWork * 1000d),
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    data.materialQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MassMetric,
                    data.materialMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    facilitySubject)
            },
            facts.ToArray(),
            Label(data.kind) + " 작업이 완료되어 "
            + states.Length.ToString(CultureInfo.InvariantCulture)
            + "벌의 의복 상태가 바뀌었다.",
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "apparel-work-context-sha256",
                    ComputeContextSha256(data))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ApparelWorkOrderTerminalResult,
            "apparel-work-order:order=" + data.orderId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(ApparelWorkCompletedData source)
    {
        ApparelWorkCompletedData data = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        data.Validate();

        var canonical = new StringBuilder();
        Append(canonical, "apparel-work-completed-context-v1");
        Append(canonical, data.orderId);
        Append(canonical, (int)data.kind);
        Append(canonical, data.facilityInstanceId);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.completedWork);
        Append(canonical, data.materialOperationId);
        Append(canonical, data.materialCommitId);

        string[] materialSources = data.materialSourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Append(canonical, materialSources.Length);
        foreach (string stackId in materialSources)
            Append(canonical, stackId);
        Append(canonical, data.materialQuantity);
        Append(canonical, data.materialMassGrams);

        CraftContributionSaveData[] contributions = data.contributions
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        Append(canonical, contributions.Length);
        foreach (CraftContributionSaveData contribution in contributions)
        {
            Append(canonical, contribution.characterId);
            Append(canonical, contribution.contributedWork);
            Append(canonical, contribution.relevantSkill);
        }

        ApparelWorkOrderOutcomeItemStateSaveData[] states = data.itemStates
            .OrderBy(value => value.itemInstanceId, StringComparer.Ordinal)
            .ToArray();
        Append(canonical, states.Length);
        foreach (ApparelWorkOrderOutcomeItemStateSaveData state in states)
        {
            Append(canonical, state.itemInstanceId);
            Append(canonical, state.stackId);
            Append(canonical, state.beforeStatePayload);
            Append(canonical, state.afterStatePayload);
            Append(canonical, state.apparelDisplayName);
        }

        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static IEnumerable<GameplayOutcomeFact> StateFacts(
        string prefix,
        IReadOnlyList<ApparelWorkOrderOutcomeItemStateSaveData> states,
        Func<ApparelWorkOrderOutcomeItemStateSaveData, string> payload)
    {
        const int MaximumChunkLength = 3800;
        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (ApparelWorkOrderOutcomeItemStateSaveData state in states)
        {
            string item = state.itemInstanceId + "=" + payload(state);
            string encoded = item.Length.ToString(
                    CultureInfo.InvariantCulture)
                + ":"
                + item;
            if (current.Length > 0
                && current.Length + encoded.Length > MaximumChunkLength)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }
            if (encoded.Length > MaximumChunkLength)
            {
                throw new InvalidOperationException(
                    "A single apparel state exceeds the bounded fact size.");
            }
            current.Append(encoded);
        }
        if (current.Length > 0)
            chunks.Add(current.ToString());
        return chunks.Select((value, index) => Fact(
            new GameplayOutcomeFactId(prefix + (index + 1)),
            value));
    }

    private static string CanonicalContributors(
        IEnumerable<CraftContributionSaveData> values)
    {
        string result = string.Join(",", (values
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(value => value != null
                && GameplayOutcomeStableIdSyntax.IsValid(value.characterId)
                && value.contributedWork > 0f)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId + "="
                + value.contributedWork.ToString(
                    "R",
                    CultureInfo.InvariantCulture)));
        return result.Length == 0 ? "none" : result;
    }

    private static string Label(ApparelWorkOrderKind kind) => kind switch
    {
        ApparelWorkOrderKind.Laundry => "세탁",
        ApparelWorkOrderKind.Drying => "건조",
        ApparelWorkOrderKind.Repair => "수리",
        ApparelWorkOrderKind.Alteration => "수선",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId id,
        string value) => new(id, value);

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, long value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, float value) =>
        Append(target, value.ToString("R", CultureInfo.InvariantCulture));
}
