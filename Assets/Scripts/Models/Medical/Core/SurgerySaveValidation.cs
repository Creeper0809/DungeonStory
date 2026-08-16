using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class SurgerySaveValidation
{
    public const int MaximumOrders = 512;
    public const int MaximumParts = 2048;
    public const int MaximumStorageStates = 512;
    public const int MaximumCorpseRecords = 4096;
    public const int MaximumPolicies = 4096;
    public const int MaximumWildlifeStates = 2048;
    public const int MaximumAnatomyNodesPerSubject = 128;

    private const string OrderPrefix = "surgery:";
    private const string PartPrefix = "surgical-part:";
    private const string MaterialDestinationPrefix = "surgery-materials:";

    public static void Validate(
        DungeonSurgerySaveData payload,
        ISurgicalProcedureCatalog procedures,
        IAnatomyProfileCatalog anatomyProfiles,
        DungeonGameRestoreReport report)
    {
        if (procedures == null)
        {
            throw new ArgumentNullException(nameof(procedures));
        }
        if (anatomyProfiles == null)
        {
            throw new ArgumentNullException(nameof(anatomyProfiles));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Surgery payload is null.");
            return;
        }
        if (payload.version != DungeonSurgerySaveData.CurrentVersion)
        {
            report.AddError(
                $"Surgery payload version {payload.version} is invalid.");
        }
        if (payload.orders == null
            || payload.parts == null
            || payload.organStorageStates == null
            || payload.corpseFreshness == null
            || payload.policies == null
            || payload.corpseRecords == null
            || payload.wildlifeAnatomy == null)
        {
            report.AddError("Surgery payload is missing a required collection.");
            return;
        }
        if (payload.orderSequence < 0 || payload.partSequence < 0)
        {
            report.AddError("Surgery sequences cannot be negative.");
        }

        HashSet<string> orderIds = ValidateOrders(
            payload.orders,
            payload.orderSequence,
            procedures,
            report);
        HashSet<string> partIds = ValidateParts(
            payload.parts,
            payload.partSequence,
            orderIds,
            report);
        ValidateStorage(payload.organStorageStates, report);
        ValidateCorpseFreshness(payload.corpseFreshness, report);
        ValidatePolicies(payload.policies, report);
        ValidateCorpseRecords(payload.corpseRecords, report);
        ValidateWildlifeAnatomy(
            payload.wildlifeAnatomy,
            anatomyProfiles,
            partIds,
            report);
        ValidateOrderPartLinks(payload.orders, partIds, report);
    }

    public static SurgeryAggregateState CreateState(
        DungeonSurgerySaveData payload)
    {
        SurgeryAggregateState state = new()
        {
            OrderSequence = payload.orderSequence,
            PartSequence = payload.partSequence
        };
        foreach (SurgeryOrder source in payload.orders)
        {
            SurgeryOrder order = SurgeryStateCloner.CloneOrder(source);
            order.admissionMoveRequested = false;
            order.patientTransporterId = string.Empty;
            order.patientTransportInProgress = false;
            state.Orders.Add(order);
        }
        state.Parts.AddRange(payload.parts.Select(SurgeryStateCloner.ClonePart));
        foreach (SurgicalOrganStorageState source in payload.organStorageStates)
        {
            state.OrganStorage.Add(source.facilityId, source.Clone());
        }
        foreach (SurgicalCorpseFreshnessState source in payload.corpseFreshness)
        {
            state.CorpseFreshness.Add(source.stackId, source.Clone());
        }
        foreach (SurgerySubjectPolicyState source in payload.policies)
        {
            state.Policies.Add(
                source.subjectId,
                source.automaticEmergencySurgery);
        }
        foreach (CorpseSurgicalRecord source in payload.corpseRecords)
        {
            state.ExtractedNodesByCorpse.Add(
                source.stackId,
                new HashSet<string>(
                    source.extractedNodeIds,
                    StringComparer.Ordinal));
        }
        foreach (WildlifeAnatomyState source in payload.wildlifeAnatomy)
        {
            state.WildlifeAnatomy.Add(
                source.wildlifeId,
                SurgeryStateCloner.CloneWildlifeAnatomy(source));
        }
        return state;
    }

    private static HashSet<string> ValidateOrders(
        IReadOnlyList<SurgeryOrder> orders,
        int sequence,
        ISurgicalProcedureCatalog procedures,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (orders.Count > MaximumOrders)
        {
            report.AddError($"Surgery order count exceeds {MaximumOrders}.");
        }
        int largestSequence = 0;
        foreach (SurgeryOrder order in orders)
        {
            if (order == null)
            {
                report.AddError("Surgery payload contains a null order.");
                continue;
            }
            string id = RequireCanonicalId(order.orderId, "order", report);
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate surgery order ID '{id}'.");
            }
            if (!TryParseNumericSuffix(id, OrderPrefix, out int orderSequence))
            {
                report.AddError(
                    $"Surgery order ID '{id}' is not a canonical positive decimal ID.");
            }
            else
            {
                largestSequence = Math.Max(largestSequence, orderSequence);
            }
            if (!Enum.IsDefined(typeof(SurgeryOrderState), order.state)
                || !Enum.IsDefined(typeof(SurgeryFailureSeverity), order.failureSeverity)
                || !Enum.IsDefined(
                    typeof(SurgeryOrderState),
                    order.environmentResumeStage))
            {
                report.AddError($"Surgery order '{id}' contains an invalid enum.");
            }
            if (string.IsNullOrWhiteSpace(order.procedureId)
                || !procedures.TryGet(order.procedureId, out _))
            {
                report.AddError(
                    $"Surgery order '{id}' references unknown procedure '{order.procedureId}'.");
            }
            ValidateSubject(order.subject, $"order '{id}'", report);
            if (order.IsActive && string.IsNullOrWhiteSpace(order.facilityId))
            {
                report.AddError($"Active surgery order '{id}' has no facility.");
            }
            if (order.IsActive
                && !string.Equals(
                    order.materialDestinationId,
                    MaterialDestinationPrefix + id,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Active surgery order '{id}' has non-canonical material destination "
                    + $"'{order.materialDestinationId}'.");
            }
            if (order.risk == null
                || order.materials == null
                || order.reachedClinicalStages == null
                || order.statusData == null
                || order.environmentWait == null
                || order.environmentRecovery == null)
            {
                report.AddError($"Surgery order '{id}' is missing required state.");
                continue;
            }
            ValidateStatus(order.statusData, id, "status", report);
            ValidateStatus(order.environmentWait, id, "environment wait", report);
            ValidateStatus(
                order.environmentRecovery,
                id,
                "environment recovery",
                report);
            ValidateOrderNumbers(order, id, report);
            ValidateRisk(order.risk, id, report);
            ValidateMaterials(order.materials, id, report);
            ValidateReachedStages(order.reachedClinicalStages, id, report);
            ValidateTransportCoherence(order, id, report);
        }
        if (sequence < largestSequence)
        {
            report.AddError(
                $"Surgery order sequence {sequence} is below stored ID {largestSequence}.");
        }
        return ids;
    }

    private static HashSet<string> ValidateParts(
        IReadOnlyList<SurgicalPartInstance> parts,
        int sequence,
        ISet<string> orderIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (parts.Count > MaximumParts)
        {
            report.AddError($"Surgical part count exceeds {MaximumParts}.");
        }
        int largestSequence = 0;
        foreach (SurgicalPartInstance part in parts)
        {
            if (part == null)
            {
                report.AddError("Surgery payload contains a null part.");
                continue;
            }
            string id = RequireCanonicalId(part.partInstanceId, "part", report);
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate surgical part ID '{id}'.");
            }
            if (!TryParseNumericSuffix(id, PartPrefix, out int partSequence))
            {
                report.AddError(
                    $"Surgical part ID '{id}' is not a canonical positive decimal ID.");
            }
            else
            {
                largestSequence = Math.Max(largestSequence, partSequence);
            }
            if (!Enum.IsDefined(typeof(SurgicalPartKind), part.kind))
            {
                report.AddError($"Surgical part '{id}' has an invalid kind.");
            }
            if (string.IsNullOrWhiteSpace(part.nodeId)
                || string.IsNullOrWhiteSpace(part.displayName))
            {
                report.AddError($"Surgical part '{id}' lacks node/display identity.");
            }
            if (!IsFinitePositive(part.quality)
                || !IsFiniteNonNegative(part.freshnessSeconds)
                || !IsFiniteRange(part.contamination, 0f, 100f)
                || !IsFiniteNonNegative(part.specialEffectStrength))
            {
                report.AddError($"Surgical part '{id}' has invalid numeric state.");
            }
            if (!string.IsNullOrEmpty(part.reservedOrderId)
                && !orderIds.Contains(part.reservedOrderId))
            {
                report.AddError(
                    $"Surgical part '{id}' references missing order '{part.reservedOrderId}'.");
            }
            if (part.installed && string.IsNullOrWhiteSpace(part.installedSubjectId))
            {
                report.AddError($"Installed surgical part '{id}' has no subject.");
            }
            if (!part.installed && !string.IsNullOrEmpty(part.installedSubjectId))
            {
                report.AddError($"Loose surgical part '{id}' has an installed subject.");
            }
        }
        if (sequence < largestSequence)
        {
            report.AddError(
                $"Surgical part sequence {sequence} is below stored ID {largestSequence}.");
        }
        return ids;
    }

    private static void ValidateStorage(
        IReadOnlyList<SurgicalOrganStorageState> states,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (states.Count > MaximumStorageStates)
        {
            report.AddError($"Organ storage count exceeds {MaximumStorageStates}.");
        }
        foreach (SurgicalOrganStorageState state in states)
        {
            string id = state == null
                ? string.Empty
                : RequireCanonicalId(state.facilityId, "organ storage", report);
            if (state == null)
            {
                report.AddError("Surgery payload contains null organ storage state.");
                continue;
            }
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate organ storage ID '{id}'.");
            }
            if (!IsFiniteNonNegative(state.fuelSecondsRemaining))
            {
                report.AddError($"Organ storage '{id}' has invalid fuel state.");
            }
        }
    }

    private static void ValidateCorpseFreshness(
        IReadOnlyList<SurgicalCorpseFreshnessState> states,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (states.Count > MaximumCorpseRecords)
        {
            report.AddError($"Corpse freshness count exceeds {MaximumCorpseRecords}.");
        }
        foreach (SurgicalCorpseFreshnessState state in states)
        {
            string id = state == null
                ? string.Empty
                : RequireCanonicalId(state.stackId, "corpse freshness", report);
            if (state == null)
            {
                report.AddError("Surgery payload contains null corpse freshness state.");
                continue;
            }
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate corpse freshness ID '{id}'.");
            }
            if (!IsFiniteNonNegative(state.remainingFreshnessSeconds))
            {
                report.AddError($"Corpse freshness '{id}' has invalid time.");
            }
        }
    }

    private static void ValidatePolicies(
        IReadOnlyList<SurgerySubjectPolicyState> policies,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (policies.Count > MaximumPolicies)
        {
            report.AddError($"Surgery policy count exceeds {MaximumPolicies}.");
        }
        foreach (SurgerySubjectPolicyState policy in policies)
        {
            string id = policy == null
                ? string.Empty
                : RequireCanonicalId(policy.subjectId, "policy subject", report);
            if (policy == null)
            {
                report.AddError("Surgery payload contains a null policy.");
                continue;
            }
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate surgery policy subject '{id}'.");
            }
        }
    }

    private static void ValidateCorpseRecords(
        IReadOnlyList<CorpseSurgicalRecord> records,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (records.Count > MaximumCorpseRecords)
        {
            report.AddError($"Extraction record count exceeds {MaximumCorpseRecords}.");
        }
        foreach (CorpseSurgicalRecord record in records)
        {
            string id = record == null
                ? string.Empty
                : RequireCanonicalId(record.stackId, "extraction corpse", report);
            if (record == null || record.extractedNodeIds == null)
            {
                report.AddError("Surgery payload contains an incomplete extraction record.");
                continue;
            }
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                report.AddError($"Duplicate extraction corpse ID '{id}'.");
            }
            HashSet<string> nodes = new(StringComparer.Ordinal);
            foreach (string nodeId in record.extractedNodeIds)
            {
                string canonical = RequireCanonicalId(
                    nodeId,
                    $"extraction node for '{id}'",
                    report);
                if (!string.IsNullOrEmpty(canonical) && !nodes.Add(canonical))
                {
                    report.AddError(
                        $"Extraction corpse '{id}' repeats node '{canonical}'.");
                }
            }
        }
    }

    private static void ValidateWildlifeAnatomy(
        IReadOnlyList<WildlifeAnatomyState> states,
        IAnatomyProfileCatalog profiles,
        ISet<string> partIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> wildlifeIds = new(StringComparer.Ordinal);
        if (states.Count > MaximumWildlifeStates)
        {
            report.AddError($"Wildlife anatomy count exceeds {MaximumWildlifeStates}.");
        }
        foreach (WildlifeAnatomyState state in states)
        {
            string id = state == null
                ? string.Empty
                : RequireCanonicalId(state.wildlifeId, "wildlife anatomy", report);
            if (state == null || state.nodes == null)
            {
                report.AddError("Surgery payload contains incomplete wildlife anatomy.");
                continue;
            }
            if (!string.IsNullOrEmpty(id) && !wildlifeIds.Add(id))
            {
                report.AddError($"Duplicate wildlife anatomy ID '{id}'.");
            }
            if (string.IsNullOrWhiteSpace(state.profileId)
                || !profiles.TryGet(state.profileId, out AnatomyProfileDefinition profile))
            {
                report.AddError(
                    $"Wildlife anatomy '{id}' references unknown profile '{state.profileId}'.");
                continue;
            }
            if (state.nodes.Count > MaximumAnatomyNodesPerSubject)
            {
                report.AddError(
                    $"Wildlife anatomy '{id}' exceeds {MaximumAnatomyNodesPerSubject} nodes.");
            }
            HashSet<string> nodes = new(StringComparer.Ordinal);
            foreach (AnatomyNodeHealthState node in state.nodes)
            {
                if (node == null)
                {
                    report.AddError($"Wildlife anatomy '{id}' contains a null node.");
                    continue;
                }
                string nodeId = RequireCanonicalId(
                    node.nodeId,
                    $"anatomy node for '{id}'",
                    report);
                if (!string.IsNullOrEmpty(nodeId) && !nodes.Add(nodeId))
                {
                    report.AddError(
                        $"Wildlife anatomy '{id}' repeats node '{nodeId}'.");
                }
                if (!profile.TryGetNode(nodeId, out _))
                {
                    report.AddError(
                        $"Wildlife anatomy '{id}' has unknown node '{nodeId}'.");
                }
                ValidateAnatomyNode(node, id, nodeId, partIds, report);
            }
            foreach (AnatomyNodeDefinition definition in profile.Nodes)
            {
                if (!nodes.Contains(definition.NodeId))
                {
                    report.AddError(
                        $"Wildlife anatomy '{id}' is missing node '{definition.NodeId}'.");
                }
            }
        }
    }

    private static void ValidateOrderPartLinks(
        IEnumerable<SurgeryOrder> orders,
        ISet<string> partIds,
        DungeonGameRestoreReport report)
    {
        foreach (SurgeryOrder order in orders.Where(order => order != null))
        {
            if (!string.IsNullOrEmpty(order.selectedPartInstanceId)
                && !partIds.Contains(order.selectedPartInstanceId))
            {
                report.AddError(
                    $"Surgery order '{order.orderId}' references missing part '{order.selectedPartInstanceId}'.");
            }
        }
    }

    private static void ValidateSubject(
        SurgicalSubjectRef subject,
        string owner,
        DungeonGameRestoreReport report)
    {
        if (subject == null
            || !Enum.IsDefined(typeof(SurgicalSubjectKind), subject.kind)
            || string.IsNullOrWhiteSpace(subject.subjectId))
        {
            report.AddError($"Surgery {owner} has an invalid subject.");
            return;
        }
        RequireCanonicalId(subject.subjectId, $"subject for {owner}", report);
    }

    private static void ValidateOrderNumbers(
        SurgeryOrder order,
        string id,
        DungeonGameRestoreReport report)
    {
        float[] nonNegative =
        {
            order.requiredWork,
            order.completedWork,
            order.anesthesiaWork,
            order.incisionWork,
            order.procedureWork,
            order.sutureWork,
            order.nextAdmissionRetryAt,
            order.createdAt,
            order.recoveryUntil,
            order.environmentStableSeconds
        };
        if (nonNegative.Any(value => !IsFiniteNonNegative(value)))
        {
            report.AddError($"Surgery order '{id}' has invalid time/work state.");
        }
        if (order.completedWork > order.requiredWork + 0.001f)
        {
            report.AddError($"Surgery order '{id}' exceeds required work.");
        }
    }

    private static void ValidateRisk(
        SurgeryRiskBreakdown risk,
        string id,
        DungeonGameRestoreReport report)
    {
        if (!Enum.IsDefined(typeof(SurgeryRiskSummaryCode), risk.summaryCode))
        {
            report.AddError($"Surgery order '{id}' has an invalid risk summary code.");
        }
        float[] probabilities =
        {
            risk.successChance,
            risk.infectionChance,
            risk.bleedingChance,
            risk.organDamageChance,
            risk.deathChance
        };
        float[] values =
        {
            risk.medicalContribution,
            risk.dexterityContribution,
            risk.researchContribution,
            risk.facilityContribution,
            risk.cleanlinessContribution,
            risk.medicineContribution,
            risk.anesthesiaContribution,
            risk.difficultyPenalty,
            risk.instabilityPenalty,
            risk.compatibilityPenalty,
            risk.environmentSuccessPenalty,
            risk.environmentInfectionPenalty,
            risk.environmentBleedingPenalty,
            risk.environmentOrganDamagePenalty,
            risk.environmentInstabilityAdded
        };
        if (probabilities.Any(value => !IsFiniteRange(value, 0f, 1f))
            || values.Any(value => !IsFinite(value))
            || risk.environmentStagesEvaluated < 0)
        {
            report.AddError($"Surgery order '{id}' has invalid risk state.");
        }
    }

    private static void ValidateMaterials(
        IEnumerable<SurgicalMaterialRequirement> materials,
        string id,
        DungeonGameRestoreReport report)
    {
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (SurgicalMaterialRequirement material in materials)
        {
            if (material == null
                || string.IsNullOrWhiteSpace(material.itemId)
                || material.quantity <= 0)
            {
                report.AddError($"Surgery order '{id}' has invalid material state.");
                continue;
            }
            string itemId = material.itemId.Trim();
            if (itemId != material.itemId || !itemIds.Add(itemId))
            {
                report.AddError(
                    $"Surgery order '{id}' has duplicate/noncanonical material '{material.itemId}'.");
            }
        }
    }

    private static void ValidateReachedStages(
        IEnumerable<SurgeryOrderState> stages,
        string id,
        DungeonGameRestoreReport report)
    {
        HashSet<SurgeryOrderState> unique = new();
        foreach (SurgeryOrderState stage in stages)
        {
            if (!Enum.IsDefined(typeof(SurgeryOrderState), stage)
                || !unique.Add(stage))
            {
                report.AddError($"Surgery order '{id}' has invalid stage history.");
            }
        }
    }

    private static void ValidateTransportCoherence(
        SurgeryOrder order,
        string id,
        DungeonGameRestoreReport report)
    {
        if (order.patientTransportInProgress
            && string.IsNullOrWhiteSpace(order.patientTransporterId))
        {
            report.AddError(
                $"Surgery order '{id}' has transport in progress without transporter.");
        }
        if (!order.patientAdmitted
            && (order.subjectAiWasPaused || order.patientReturnRequested))
        {
            report.AddError(
                $"Surgery order '{id}' has admitted-patient state without admission.");
        }
        if (order.state == SurgeryOrderState.EnvironmentWaiting
            && order.environmentResumeStage is not SurgeryOrderState.Anesthetizing
                and not SurgeryOrderState.Incision
                and not SurgeryOrderState.Procedure
                and not SurgeryOrderState.Suturing)
        {
            report.AddError(
                $"Surgery order '{id}' has invalid environment resume stage.");
        }
        if (order.state == SurgeryOrderState.EnvironmentWaiting
            && order.environmentWait.code is not SurgeryStatusCode.EnvironmentUnsafe
                and not SurgeryStatusCode.EnvironmentStabilizing)
        {
            report.AddError(
                $"Surgery order '{id}' has no typed environment wait status.");
        }
    }

    private static void ValidateStatus(
        SurgeryStatusData status,
        string orderId,
        string fieldName,
        DungeonGameRestoreReport report)
    {
        if (!Enum.IsDefined(typeof(SurgeryStatusCode), status.code)
            || !Enum.IsDefined(typeof(SurgeryOrderState), status.stage)
            || !IsFinite(status.scalarValue)
            || !IsFinite(status.secondaryScalarValue)
            || !IsFinite(status.tertiaryScalarValue)
            || status.countValue < 0)
        {
            report.AddError(
                $"Surgery order '{orderId}' contains invalid {fieldName} code or parameters.");
        }
    }

    private static void ValidateAnatomyNode(
        AnatomyNodeHealthState node,
        string wildlifeId,
        string nodeId,
        ISet<string> partIds,
        DungeonGameRestoreReport report)
    {
        if (!Enum.IsDefined(typeof(SurgicalPartKind), node.installedPartKind)
            || !Enum.IsDefined(typeof(PartRecoveryPolicy), node.recoveryPolicy)
            || !IsFinitePositive(node.maxHealth)
            || !IsFiniteRange(node.currentHealth, 0f, node.maxHealth)
            || !IsFiniteNonNegative(node.bleedingPerSecond)
            || !IsFiniteRange(node.infection, 0f, 100f)
            || !IsFiniteNonNegative(node.installedPartEfficiency)
            || !IsFiniteRange(node.rejectionBurden, 0f, 100f)
            || !IsFiniteRange(node.mutationBurden, 0f, 100f)
            || !IsFinite(node.moduleBonus))
        {
            report.AddError(
                $"Wildlife anatomy '{wildlifeId}' node '{nodeId}' has invalid state.");
        }
        if (!string.IsNullOrEmpty(node.installedPartId)
            && !partIds.Contains(node.installedPartId))
        {
            report.AddError(
                $"Wildlife anatomy '{wildlifeId}' node '{nodeId}' references missing part '{node.installedPartId}'.");
        }
    }

    private static string RequireCanonicalId(
        string value,
        string label,
        DungeonGameRestoreReport report)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
        {
            report.AddError($"Surgery {label} ID is blank or noncanonical.");
        }
        return canonical;
    }

    private static bool TryParseNumericSuffix(
        string value,
        string prefix,
        out int sequence)
    {
        sequence = 0;
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = value.Substring(prefix.Length);
        return int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return IsFinite(value) && value >= 0f;
    }

    private static bool IsFinitePositive(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFiniteRange(float value, float minimum, float maximum)
    {
        return IsFinite(value) && value >= minimum && value <= maximum;
    }
}
