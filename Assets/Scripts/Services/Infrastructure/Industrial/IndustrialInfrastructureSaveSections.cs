using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed class PowerInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.power";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    private readonly IPowerInfrastructurePersistence persistence;

    public PowerInfrastructureSaveSection(
        IPowerInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonPowerInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "power infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "power infrastructure");
        ElectricalNetworkRestoreCandidate candidate =
            persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(
                    payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class FluidInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.fluids";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IFluidInfrastructurePersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;

    public FluidInfrastructureSaveSection(
        IFluidInfrastructurePersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonFluidInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "fluid infrastructure",
                report))
        {
            return;
        }

        try { IndustrialInfrastructureSaveValidation.Validate(IndustrialSaveSectionParsing.ReadFluid(payloadJson), report); }
        catch (InvalidOperationException error) { report.AddError("Fluid observation payload is invalid: " + error.Message); }
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "fluid infrastructure");
        DungeonFluidInfrastructureSaveData payload =
            IndustrialSaveSectionParsing.ReadFluid(payloadJson);
        FluidNetworkRestoreCandidate candidate =
            persistence.PrepareRestore(payload);
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }

    public static void ValidatePhysicalRestoreCandidate(
        DungeonFluidInfrastructureSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.nodes == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Fluid restore requires the incoming physical candidate.");
        }

        Dictionary<string, ManualWaterTransferSaveData> manualOwners =
            payload.nodes
                .Where(node => node != null)
                .SelectMany(node => node.pendingManualWaterTransfers
                    ?? new List<ManualWaterTransferSaveData>())
                .Where(owner => owner != null
                    && owner.transferredWaterUnits > 0)
                .ToDictionary(owner => owner.operationId, StringComparer.Ordinal);
        Dictionary<string, ContainerWaterFeedCommitSaveData> feedOwners =
            payload.nodes
                .Where(node => node?.pendingContainerFeed != null
                    && (ContainerWaterFeedCommitPhase)
                        node.pendingContainerFeed.phase
                        != ContainerWaterFeedCommitPhase.None)
                .Select(node => node.pendingContainerFeed)
                .ToDictionary(owner => owner.operationId, StringComparer.Ordinal);

        foreach (KeyValuePair<string, ManualWaterTransferSaveData> pair in
                 manualOwners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !MatchesManualWater(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Manual-water owner '{pair.Key}' has no exact incoming physical Transfer receipt.");
            }
        }
        foreach (KeyValuePair<string, ContainerWaterFeedCommitSaveData> pair in
                 feedOwners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !MatchesContainerFeed(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Container-water feed owner '{pair.Key}' has no exact incoming physical Transfer receipt.");
            }
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt == null)
            {
                continue;
            }
            if (string.Equals(
                    receipt.ReasonCode,
                    FluidPhysicalOperationIdentity.ManualReserveReasonCode,
                    StringComparison.Ordinal)
                && (!manualOwners.TryGetValue(
                        receipt.OperationId,
                        out ManualWaterTransferSaveData manualOwner)
                    || !MatchesManualWater(manualOwner, receipt)))
            {
                throw new InvalidOperationException(
                    $"Incoming manual-water Transfer '{receipt.OperationId}' has no exact fluid owner.");
            }
            if (string.Equals(
                    receipt.ReasonCode,
                    FluidPhysicalOperationIdentity.ContainerFeedReasonCode,
                    StringComparison.Ordinal)
                && (!feedOwners.TryGetValue(
                        receipt.OperationId,
                        out ContainerWaterFeedCommitSaveData feedOwner)
                    || !MatchesContainerFeed(feedOwner, receipt)))
            {
                throw new InvalidOperationException(
                    $"Incoming container-water feed Transfer '{receipt.OperationId}' has no exact fluid owner.");
            }
        }
    }

    private static bool MatchesManualWater(
        ManualWaterTransferSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            FluidPhysicalOperationIdentity.ManualReserveReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.RequestFingerprint,
            owner.requestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.physicalCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.transferredWaterUnits
        && receipt.InputMassGrams == owner.inputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.sourceStackIds,
            StringComparer.Ordinal);

    private static bool MatchesContainerFeed(
        ContainerWaterFeedCommitSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            owner.reasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.RequestFingerprint,
            owner.requestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.physicalCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.quantity
        && receipt.InputMassGrams == owner.inputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.sourceStackIds,
            StringComparer.Ordinal);
}

public sealed class ConveyorInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "infrastructure.conveyor";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IConveyorInfrastructurePersistence persistence;

    public ConveyorInfrastructureSaveSection(
        IConveyorInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonConveyorInfrastructureSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "conveyor infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "conveyor infrastructure");
        ConveyorRestoreState candidate = persistence.PrepareRestore(
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class AutomationInfrastructureSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.automation";
    private static readonly string[] Dependencies =
    {
        ProductionBillsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };
    private readonly IAutomationInfrastructurePersistence persistence;

    public AutomationInfrastructureSaveSection(
        IAutomationInfrastructurePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonAutomationSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "automation infrastructure",
                report))
        {
            return;
        }

        IndustrialInfrastructureSaveValidation.Validate(
            JsonUtility.FromJson<DungeonAutomationSaveData>(payloadJson),
            report);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "automation infrastructure");
        AutomationRestoreCandidate candidate = persistence.PrepareRestore(
            JsonUtility.FromJson<DungeonAutomationSaveData>(payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

public sealed class InfrastructureCommandOutcomeSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.infrastructure-command-outcomes";
    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        GameplayOutcomeLedgerSaveSection.Id
    };
    private readonly IInfrastructureObservationPersistence persistence;

    public InfrastructureCommandOutcomeSaveSection(
        IInfrastructureObservationPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonInfrastructureObservationSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (!IndustrialSaveSectionParsing.ValidateHeader(
                payloadJson,
                sectionVersion,
                SectionVersion,
                "infrastructure command outcomes",
                report))
        {
            return;
        }

        try
        {
            InfrastructureObservationSaveValidation.RequireValid(
                IndustrialSaveSectionParsing.ReadObservations(payloadJson));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            report.AddError(
                "Infrastructure command outcome payload is invalid: "
                + exception.Message);
        }
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IndustrialSaveSectionParsing.RequireHeader(
            payloadJson,
            sectionVersion,
            SectionVersion,
            "infrastructure command outcomes");
        InfrastructureObservationRestoreCandidate candidate =
            persistence.PrepareRestore(
                IndustrialSaveSectionParsing.ReadObservations(payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}

internal static class IndustrialSaveSectionParsing
{
    public static DungeonInfrastructureObservationSaveData ReadObservations(string json)
    {
        RequireShape(Parse(json), typeof(DungeonInfrastructureObservationSaveData));
        return JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
    }

    public static DungeonFluidInfrastructureSaveData ReadFluid(string json)
    {
        JObject root = Parse(json);
        if (root["version"]?.Type != JTokenType.Integer || root["nodes"] is not JArray nodes)
            throw new InvalidOperationException("industrial-fluid-current-shape-invalid");
        foreach (JToken token in nodes)
        {
            if (token is not JObject node || node["nextAutonomousOutcomeSequence"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("industrial-fluid-observation-sequence-missing");
            RequireShape(node["pendingAutonomousOutcomes"], typeof(List<FluidAutonomousOutcomeSaveData>));
        }
        return JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(json);
    }

    private static JObject Parse(string json)
    {
        try { return JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
        catch (Newtonsoft.Json.JsonException error) { throw new InvalidOperationException("industrial-json-invalid", error); }
    }

    // Current-format factual values must be present; Unity must not invent absent zero/default values.
    private static void RequireShape(JToken token, Type type)
    {
        if (token == null || token.Type == JTokenType.Null)
            throw new InvalidOperationException("industrial-observation-json-field-missing");
        if (type == typeof(string)) { RequireType(token, JTokenType.String); return; }
        if (type == typeof(bool)) { RequireType(token, JTokenType.Boolean); return; }
        if (type.IsEnum || type == typeof(int) || type == typeof(long)) { RequireType(token, JTokenType.Integer); return; }
        if (type == typeof(float))
        {
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                throw new InvalidOperationException("industrial-observation-json-number-invalid");
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not JArray list) throw new InvalidOperationException("industrial-observation-json-array-missing");
            foreach (JToken item in list) RequireShape(item, type.GetGenericArguments()[0]);
            return;
        }
        if (token is not JObject obj) throw new InvalidOperationException("industrial-observation-json-object-missing");
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            RequireShape(obj[field.Name], field.FieldType);
    }
    private static void RequireType(JToken token, JTokenType expected)
    {
        if (token.Type != expected) throw new InvalidOperationException("industrial-observation-json-type-invalid");
    }

    public static bool ValidateHeader(
        string payloadJson,
        int sectionVersion,
        int expectedVersion,
        string label,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != expectedVersion)
        {
            report.AddError(
                $"Unsupported {label} section version {sectionVersion}; "
                + $"expected {expectedVersion}.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            report.AddError($"Required {label} payload is missing.");
            return false;
        }

        return true;
    }

    public static void RequireHeader(
        string payloadJson,
        int sectionVersion,
        int expectedVersion,
        string label)
    {
        if (sectionVersion != expectedVersion
            || string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"Required {label} payload must use exact version "
                + $"{expectedVersion}.");
        }
    }
}
