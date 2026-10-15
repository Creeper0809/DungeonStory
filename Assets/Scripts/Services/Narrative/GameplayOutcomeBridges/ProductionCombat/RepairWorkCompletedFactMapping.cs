using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class RepairWorkCompletedFactMapping
{
    private static readonly GameplayOutcomeFactId KindFact =
        new("repair-kind");
    private static readonly GameplayOutcomeFactId TargetFact =
        new("repair-target-id");
    private static readonly GameplayOutcomeFactId TargetDefinitionFact =
        new("repair-target-definition-id");
    private static readonly GameplayOutcomeFactId MaterialSourcesFact =
        new("repair-material-sources");
    public static MigratedDomainFactCapture Capture(
        RepairWorkCompletedFact fact)
    {
        RepairWorkOutcomeSaveData owner = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        RequireValid(owner);

        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            new(
                MigratedProducerOutcomeIds.FacilityKind,
                owner.facilityId,
                owner.facilityId,
                MigratedProducerOutcomeIds.FacilityRole)
        };
        if (!string.Equals(
                owner.targetEntityId,
                owner.facilityId,
                StringComparison.Ordinal))
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                owner.kind == RepairWorkOutcomeKind.EquipmentDurability
                    ? MigratedProducerOutcomeIds.ItemKind
                    : MigratedProducerOutcomeIds.FacilityKind,
                owner.targetEntityId,
                owner.targetEntityId,
                MigratedProducerOutcomeIds.TargetRole));
        }

        CraftContributionSaveData lead = (owner.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null && value.contributedWork > 0f)
            .OrderByDescending(value => value.contributedWork)
            .ThenBy(value => value.characterId, StringComparer.Ordinal)
            .FirstOrDefault();
        string actorId = lead?.characterId ?? owner.initiatorCharacterId;
        subjects.Add(new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            actorId,
            actorId,
            MigratedProducerOutcomeIds.ActorRole));

        GameplayEntityId target = subjects[
            string.Equals(
                owner.targetEntityId,
                owner.facilityId,
                StringComparison.Ordinal)
                ? 0
                : 1].EntityId;
        double totalWork = (owner.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null)
            .Sum(value => Math.Max(0d, value.contributedWork));
        int materialQuantity = (owner.materialSources
                ?? new List<RepairWorkMaterialSourceSaveData>())
            .Where(value => value != null)
            .Sum(value => Math.Max(0, value.quantity));
        string materialSources = CanonicalMaterialSources(
            owner.materialSources);
        string contributors = CanonicalContributors(owner.contributions);
        if (materialSources.Length > 3800 || contributors.Length > 3800)
        {
            throw new InvalidOperationException(
                "repair-work-outcome-fact-capacity-exceeded");
        }

        GameplayMetricUnitId primaryUnit = owner.primaryUnit switch
        {
            RepairWorkPrimaryUnit.Flag => MigratedProducerOutcomeIds.EnumUnit,
            RepairWorkPrimaryUnit.Point => MigratedProducerOutcomeIds.PointUnit,
            RepairWorkPrimaryUnit.Percent => MigratedProducerOutcomeIds.PointUnit,
            RepairWorkPrimaryUnit.Ratio => MigratedProducerOutcomeIds.RatioUnit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(owner.primaryUnit),
                owner.primaryUnit,
                null)
        };
        GameplayOutcomeMetric[] metrics =
        {
            new(
                MigratedProducerOutcomeIds.PreviousMetric,
                owner.primaryBefore,
                primaryUnit,
                target),
            new(
                MigratedProducerOutcomeIds.CurrentMetric,
                owner.primaryAfter,
                primaryUnit,
                target),
            new(
                MigratedProducerOutcomeIds.DeltaMetric,
                owner.primaryAfter - owner.primaryBefore,
                primaryUnit,
                target),
            new(
                MigratedProducerOutcomeIds.CompletedWorkMetric,
                Math.Round(totalWork * 1000d),
                MigratedProducerOutcomeIds.MilliWorkUnit,
                target),
            new(
                MigratedProducerOutcomeIds.InputQuantityMetric,
                materialQuantity,
                MigratedProducerOutcomeIds.CountUnit,
                target),
            new(
                MigratedProducerOutcomeIds.InputMassMetric,
                owner.materialMassGrams,
                MigratedProducerOutcomeIds.GramUnit,
                target)
        };
        GameplayOutcomeFact[] facts =
        {
            Fact(MigratedProducerOutcomeIds.OperationFact, owner.operationId),
            Fact(MigratedProducerOutcomeIds.FacilityFact, owner.facilityId),
            Fact(KindFact, StableKind(owner.kind)),
            Fact(TargetFact, owner.targetEntityId),
            Fact(
                TargetDefinitionFact,
                string.IsNullOrEmpty(owner.targetDefinitionId)
                    ? "none"
                    : owner.targetDefinitionId),
            Fact(
                MigratedProducerOutcomeIds.BeforeFact,
                CanonicalState(owner, before: true)),
            Fact(
                MigratedProducerOutcomeIds.AfterFact,
                CanonicalState(owner, before: false)),
            Fact(MigratedProducerOutcomeIds.ContributorsFact, contributors),
            Fact(
                new GameplayOutcomeFactId("repair-completion-worker"),
                owner.completionWorkerId),
            Fact(
                new GameplayOutcomeFactId("repair-completion-origin"),
                WorkCompletionIdentityOutcomeIds.StableOrigin(
                    owner.completionOrigin)),
            Fact(
                new GameplayOutcomeFactId("repair-completion-work"),
                owner.completionContributionWork.ToString(
                    "R",
                    CultureInfo.InvariantCulture)),
            Fact(
                MigratedProducerOutcomeIds.MaterialInputOperationFact,
                string.IsNullOrEmpty(owner.materialOperationId)
                    ? "none"
                    : owner.materialOperationId),
            Fact(
                MigratedProducerOutcomeIds.BatchFact,
                string.IsNullOrEmpty(owner.materialCommitId)
                    ? "none"
                    : owner.materialCommitId),
            Fact(MaterialSourcesFact, materialSources)
        };
        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            metrics,
            facts,
            StableKind(owner.kind)
                + " 수리가 실제 상태 변경과 함께 완료되었다.",
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "repair-context-sha256",
                    ComputeContextSha256(owner))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.RepairWorkResult,
            owner.operationId,
            owner.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(
        RepairWorkOutcomeSaveData source)
    {
        RepairWorkOutcomeSaveData owner = source
            ?? throw new ArgumentNullException(nameof(source));
        RequireValid(owner);

        var canonical = new StringBuilder();
        Append(canonical, "repair-work-context-v1");
        Append(canonical, owner.operationId);
        Append(canonical, (int)owner.kind);
        Append(canonical, owner.facilityId);
        Append(canonical, owner.targetEntityId);
        Append(canonical, owner.targetDefinitionId);
        Append(canonical, owner.initiatorCharacterId);
        Append(canonical, owner.startedAbsoluteDay);
        Append(canonical, owner.completedAbsoluteDay);
        Append(canonical, owner.boundsKnown);
        Append(canonical, (int)owner.primaryUnit);
        Append(canonical, owner.primaryBefore);
        Append(canonical, owner.primaryAfter);
        Append(canonical, owner.secondaryBefore);
        Append(canonical, owner.secondaryAfter);
        Append(canonical, owner.terminal);
        Append(canonical, owner.completionWorkerId);
        Append(canonical, (int)owner.completionOrigin);
        Append(canonical, owner.completionContributionWork);

        CraftContributionSaveData[] contributions = (owner.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        Append(canonical, contributions.Length);
        foreach (CraftContributionSaveData contribution in contributions)
        {
            Append(canonical, contribution.characterId);
            Append(canonical, contribution.contributedWork);
            Append(canonical, contribution.relevantSkill);
        }

        Append(canonical, owner.materialOperationId);
        Append(canonical, owner.materialCommitId);
        Append(canonical, owner.materialMassGrams);
        RepairWorkMaterialSourceSaveData[] materialSources =
            (owner.materialSources
                    ?? new List<RepairWorkMaterialSourceSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.stackId, StringComparer.Ordinal)
                .ToArray();
        Append(canonical, materialSources.Length);
        foreach (RepairWorkMaterialSourceSaveData material in materialSources)
        {
            Append(canonical, material.stackId);
            Append(canonical, material.itemId);
            Append(canonical, material.quantity);
        }

        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static void RequireValid(RepairWorkOutcomeSaveData owner)
    {
        if (!RepairWorkOutcomeValidation.IsValid(
                owner,
                requireTerminal: true,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
    }

    private static string CanonicalContributors(
        IEnumerable<CraftContributionSaveData> source)
    {
        string value = string.Join(",", (source
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(item => item != null && item.contributedWork > 0f)
            .OrderBy(item => item.characterId, StringComparer.Ordinal)
            .Select(item => item.characterId + "="
                + item.contributedWork.ToString(
                    "R",
                    CultureInfo.InvariantCulture)));
        return value.Length == 0 ? "none" : value;
    }

    private static string CanonicalMaterialSources(
        IEnumerable<RepairWorkMaterialSourceSaveData> source)
    {
        string value = string.Join(",", (source
                ?? Array.Empty<RepairWorkMaterialSourceSaveData>())
            .Where(item => item != null)
            .OrderBy(item => item.stackId, StringComparer.Ordinal)
            .Select(item => item.stackId + "=" + item.itemId + "="
                + item.quantity.ToString(CultureInfo.InvariantCulture)));
        return value.Length == 0 ? "none" : value;
    }

    private static string CanonicalState(
        RepairWorkOutcomeSaveData owner,
        bool before) => "primary="
        + (before ? owner.primaryBefore : owner.primaryAfter).ToString(
            "R",
            CultureInfo.InvariantCulture)
        + ";secondary="
        + (before ? owner.secondaryBefore : owner.secondaryAfter).ToString(
            "R",
            CultureInfo.InvariantCulture);

    private static string StableKind(RepairWorkOutcomeKind kind) => kind switch
    {
        RepairWorkOutcomeKind.GeneralDamage => "general-damage",
        RepairWorkOutcomeKind.StructuralIntegrity => "structural-integrity",
        RepairWorkOutcomeKind.DefenseFacility => "defense-facility",
        RepairWorkOutcomeKind.EquipmentDurability => "equipment-durability",
        RepairWorkOutcomeKind.AutomationMaintenance => "automation-maintenance",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
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

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");

    private static void Append(StringBuilder target, float value) =>
        Append(target, value.ToString("R", CultureInfo.InvariantCulture));
}
