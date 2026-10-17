using System;
using System.Globalization;
using System.Text;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class CharacterMedicalOutcomeIds
{
    public static readonly GameplayOutcomeTypeId Completed =
        new("character.medical-procedure-completed");
    public static readonly GameplayEntityKindId Character = new("character");
    public static readonly GameplayRoleId Patient = new("patient");
    public static readonly GameplayRoleId Clinician = new("clinician");
    public static readonly GameplayOutcomeTagId Medical = new("medical-procedure");
    public static readonly GameplayMetricUnitId Points = new("points");
    public static readonly GameplayMetricId HealthBefore = new("medical.health-before");
    public static readonly GameplayMetricId HealthAfter = new("medical.health-after");
    public static readonly GameplayMetricId BloodLossBefore = new("medical.blood-loss-before");
    public static readonly GameplayMetricId BloodLossAfter = new("medical.blood-loss-after");
    public static readonly GameplayMetricId PartDeficitBefore = new("medical.part-deficit-before");
    public static readonly GameplayMetricId PartDeficitAfter = new("medical.part-deficit-after");
    public static readonly GameplayMetricId AnatomyDeficitBefore = new("medical.anatomy-deficit-before");
    public static readonly GameplayMetricId AnatomyDeficitAfter = new("medical.anatomy-deficit-after");
    public static readonly GameplayMetricId AnatomyBleedingBefore = new("medical.anatomy-bleeding-before");
    public static readonly GameplayMetricId AnatomyBleedingAfter = new("medical.anatomy-bleeding-after");
    public static readonly GameplayMetricId SurfaceBleedingBefore = new("medical.surface-bleeding-before");
    public static readonly GameplayMetricId SurfaceBleedingAfter = new("medical.surface-bleeding-after");
    public static readonly GameplayMetricId CompletedWork = new("medical.completed-work");
    public static readonly GameplayMetricId RequiredWork = new("medical.required-work");
    public static readonly GameplayOutcomeFactId Procedure = new("medical.procedure");
    public static readonly GameplayOutcomeFactId Order = new("medical.order-id");
    public static readonly GameplayOutcomeFactId Facility = new("medical.facility-id");
    public static readonly GameplayOutcomeFactId MaterialKind = new("medical.material-kind");
    public static readonly GameplayOutcomeFactId MaterialItem = new("medical.material-item-id");
    public static readonly GameplayOutcomeFactId MaterialQuantity = new("medical.material-quantity");
    public static readonly GameplayOutcomeFactId AnatomyTransition = new("medical.anatomy-transition");
    public static readonly GameplayOutcomeFactId ClinicalResult = new("medical.clinical-result");

    public static GameplayResultKey ResultKey(string orderId, int sequence) => new(
        "character.medical",
        new GameplayOperationId($"medical-terminal:{orderId}:{sequence}"),
        sequence,
        0);
}

public readonly struct CharacterMedicalOutcomeVitals
{
    public CharacterMedicalOutcomeVitals(CharacterBodyHealthState state)
        : this(CharacterMedicalProcedureFact.CaptureVitals(state))
    {
    }

    public CharacterMedicalOutcomeVitals(CharacterMedicalVitalsFactData state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        Health = state.health;
        BloodLoss = state.bloodLoss;
        Downed = state.downed;
        float partDeficit = 0f;
        float surfaceBleeding = 0f;
        if (state.parts != null)
            foreach (CharacterMedicalPartFactData part in state.parts)
                if (part != null)
                {
                    partDeficit += Math.Max(0f, part.maxHealth - part.currentHealth);
                    surfaceBleeding += Math.Max(0f, part.bleedingPerSecond);
                }
        PartDeficit = partDeficit;
        SurfaceBleeding = surfaceBleeding;
        float anatomyDeficit = 0f;
        float anatomyBleeding = 0f;
        if (state.anatomyNodes != null)
            foreach (CharacterMedicalAnatomyFactData node in state.anatomyNodes)
                if (node != null)
                {
                    anatomyDeficit += Math.Max(0f, node.maxHealth - node.currentHealth);
                    anatomyBleeding += Math.Max(0f, node.bleedingPerSecond);
                }
        AnatomyDeficit = anatomyDeficit;
        AnatomyBleeding = anatomyBleeding;
        StringBuilder anatomy = new();
        if (state.anatomyNodes != null)
            foreach (CharacterMedicalAnatomyFactData node in state.anatomyNodes)
                if (node != null)
                {
                    if (anatomy.Length > 0) anatomy.Append(';');
                    anatomy.Append(node.nodeId)
                        .Append('=')
                        .Append(node.currentHealth.ToString("R", CultureInfo.InvariantCulture))
                        .Append('/')
                        .Append(node.maxHealth.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(node.bleedingPerSecond.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(node.missing ? '1' : '0');
                }
        AnatomyNodes = anatomy.Length == 0 ? "none" : anatomy.ToString();
    }

    public float Health { get; }
    public float BloodLoss { get; }
    public bool Downed { get; }
    public float PartDeficit { get; }
    public float SurfaceBleeding { get; }
    public float AnatomyDeficit { get; }
    public float AnatomyBleeding { get; }
    public string AnatomyNodes { get; }
}

public static class CharacterMedicalFactObservation
{
    public static CharacterMedicalOutcomeReceipt Capture(
        CharacterMedicalProcedureFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        if (fact.IsProposal)
            throw new InvalidOperationException(
                "character-medical-proposal-cannot-be-observed");

        CharacterMedicalProcedureFactData data = fact.Snapshot();
        var before = new CharacterMedicalOutcomeVitals(data.before);
        var after = new CharacterMedicalOutcomeVitals(data.after);
        return new CharacterMedicalOutcomeReceipt(
            CharacterMedicalOutcomeIds.ResultKey(data.orderId, data.sequence),
            data.absoluteDay,
            data.patientId,
            data.patientName,
            data.clinicianId,
            data.clinicianName,
            data.orderId,
            data.procedure,
            data.facilityId,
            data.materialKind,
            data.materialItemId,
            data.materialQuantity,
            data.completedWork,
            data.requiredWork,
            before,
            after);
    }

    public static OutcomeWriteRequirements Reserve(
        CharacterMedicalProcedureFact fact,
        long worldEpoch)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        CharacterMedicalProcedureFactData data = fact.Data;
        return CharacterMedicalOutcomeAdapter.Requirements(
            CharacterMedicalOutcomeIds.ResultKey(data.orderId, data.sequence),
            data.absoluteDay,
            worldEpoch);
    }
}

public readonly struct CharacterMedicalOutcomeReceipt
{
    public CharacterMedicalOutcomeReceipt(
        GameplayResultKey resultKey,
        int absoluteDay,
        string patientId,
        string patientName,
        string clinicianId,
        string clinicianName,
        string orderId,
        CharacterMedicalProcedureKind procedure,
        string facilityId,
        CharacterMedicalSupplyKind materialKind,
        string materialItemId,
        int materialQuantity,
        float completedWork,
        float requiredWork,
        in CharacterMedicalOutcomeVitals before,
        in CharacterMedicalOutcomeVitals after)
    {
        ResultKey = resultKey;
        AbsoluteDay = absoluteDay;
        PatientId = patientId ?? string.Empty;
        PatientName = patientName ?? string.Empty;
        ClinicianId = clinicianId ?? string.Empty;
        ClinicianName = clinicianName ?? string.Empty;
        OrderId = orderId ?? string.Empty;
        Procedure = procedure;
        FacilityId = facilityId ?? string.Empty;
        MaterialKind = materialKind;
        MaterialItemId = materialItemId ?? string.Empty;
        MaterialQuantity = materialQuantity;
        CompletedWork = completedWork;
        RequiredWork = requiredWork;
        Before = before;
        After = after;
    }

    public GameplayResultKey ResultKey { get; }
    public int AbsoluteDay { get; }
    public string PatientId { get; }
    public string PatientName { get; }
    public string ClinicianId { get; }
    public string ClinicianName { get; }
    public string OrderId { get; }
    public CharacterMedicalProcedureKind Procedure { get; }
    public string FacilityId { get; }
    public CharacterMedicalSupplyKind MaterialKind { get; }
    public string MaterialItemId { get; }
    public int MaterialQuantity { get; }
    public float CompletedWork { get; }
    public float RequiredWork { get; }
    public CharacterMedicalOutcomeVitals Before { get; }
    public CharacterMedicalOutcomeVitals After { get; }
}

public sealed class CharacterMedicalOutcomeAdapter :
    GameplayOutcomeAdapter<CharacterMedicalOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CharacterMedicalOutcomeIds.Completed;

    public override OutcomePrepareResult TryGetRequirements(
        in CharacterMedicalOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || receipt.AbsoluteDay <= 0
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.PatientId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.ClinicianId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.OrderId)
            || string.IsNullOrWhiteSpace(receipt.PatientName)
            || string.IsNullOrWhiteSpace(receipt.ClinicianName)
            || !Enum.IsDefined(typeof(CharacterMedicalProcedureKind), receipt.Procedure)
            || !Enum.IsDefined(typeof(CharacterMedicalSupplyKind), receipt.MaterialKind)
            || receipt.MaterialQuantity < 0
            || receipt.Before.AnatomyNodes == null
            || receipt.After.AnatomyNodes == null
            || receipt.Before.AnatomyNodes.Length
                + receipt.After.AnatomyNodes.Length + 4
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length
            || !float.IsFinite(receipt.CompletedWork)
            || !float.IsFinite(receipt.RequiredWork)
            || receipt.CompletedWork < 0f
            || receipt.RequiredWork < 0f)
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "medical-receipt-invalid");
        requirements = Requirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch);
        return OutcomePrepareResult.Prepared();
    }

    public static OutcomeWriteRequirements Requirements(
        GameplayResultKey key,
        int day,
        long epoch) => new(
        key,
        CharacterMedicalOutcomeIds.Completed,
        day,
        GameplayOutcomeStatus.Succeeded,
        epoch,
        key.CommitRevision,
        participantCount: 2,
        metricCount: 14,
        subjectCount: 2,
        tagCount: 1,
        anchorCount: 0,
        provenanceCount: 1,
        factCount: 8);

    public override OutcomePrepareResult TryWrite(
        in CharacterMedicalOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId patient = new(
            CharacterMedicalOutcomeIds.Character, receipt.PatientId);
        GameplayEntityId clinician = new(
            CharacterMedicalOutcomeIds.Character, receipt.ClinicianId);
        bool valid = builder.AddParticipant(new GameplayOutcomeParticipant(
                patient, CharacterMedicalOutcomeIds.Patient,
                GameplayParticipationKind.Direct, true,
                MigratedProducerOutcomeSnapshots.Name(
                    receipt.PatientId, receipt.PatientName)))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                clinician, CharacterMedicalOutcomeIds.Clinician,
                GameplayParticipationKind.Direct, true,
                MigratedProducerOutcomeSnapshots.Name(
                    receipt.ClinicianId, receipt.ClinicianName)))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                patient, 0.62f, NarrativeMemoryTier.Recent,
                false, false, 0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                clinician, 0.48f, NarrativeMemoryTier.Recent,
                false, false, 0))
            && Metric(ref builder, CharacterMedicalOutcomeIds.HealthBefore,
                receipt.Before.Health, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.HealthAfter,
                receipt.After.Health, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.BloodLossBefore,
                receipt.Before.BloodLoss, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.BloodLossAfter,
                receipt.After.BloodLoss, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.PartDeficitBefore,
                receipt.Before.PartDeficit, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.PartDeficitAfter,
                receipt.After.PartDeficit, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.AnatomyDeficitBefore,
                receipt.Before.AnatomyDeficit, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.AnatomyDeficitAfter,
                receipt.After.AnatomyDeficit, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.AnatomyBleedingBefore,
                receipt.Before.AnatomyBleeding, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.AnatomyBleedingAfter,
                receipt.After.AnatomyBleeding, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.SurfaceBleedingBefore,
                receipt.Before.SurfaceBleeding, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.SurfaceBleedingAfter,
                receipt.After.SurfaceBleeding, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.CompletedWork,
                receipt.CompletedWork, patient)
            && Metric(ref builder, CharacterMedicalOutcomeIds.RequiredWork,
                receipt.RequiredWork, patient)
            && builder.AddTag(CharacterMedicalOutcomeIds.Medical)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt", "character-medical-v1"))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.Procedure,
                receipt.Procedure == CharacterMedicalProcedureKind.Stabilization
                    ? "stabilization" : "treatment"))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.Order, receipt.OrderId))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.Facility,
                receipt.FacilityId.Length > 0 ? receipt.FacilityId : "none"))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.MaterialKind,
                receipt.MaterialKind.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.MaterialItem,
                receipt.MaterialItemId.Length > 0 ? receipt.MaterialItemId : "none"))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.MaterialQuantity,
                receipt.MaterialQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.AnatomyTransition,
                receipt.Before.AnatomyNodes + " -> " + receipt.After.AnatomyNodes))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterMedicalOutcomeIds.ClinicalResult,
                ClinicalResult(receipt)));
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "medical-receipt-write-failed");
    }

    private static bool Metric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        float value,
        GameplayEntityId patient) =>
        builder.AddMetric(new GameplayOutcomeMetric(
            id, value, CharacterMedicalOutcomeIds.Points, patient));

    private static string ClinicalResult(
        in CharacterMedicalOutcomeReceipt receipt)
    {
        if (receipt.Procedure == CharacterMedicalProcedureKind.Stabilization)
            return receipt.After.AnatomyBleeding < receipt.Before.AnatomyBleeding
                || receipt.After.SurfaceBleeding < receipt.Before.SurfaceBleeding
                ? "bleeding-stabilized" : "no-bleeding-change";
        bool improved = receipt.After.Health > receipt.Before.Health
            || receipt.After.BloodLoss < receipt.Before.BloodLoss
            || receipt.After.PartDeficit < receipt.Before.PartDeficit
            || receipt.After.AnatomyDeficit < receipt.Before.AnatomyDeficit;
        if (!improved) return "no-observable-change";
        return receipt.After.Downed
            ? "additional-treatment-required" : "recovered";
    }
}

public sealed class CharacterMedicalOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new MigratedProducerOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new MigratedProducerOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new MigratedProducerOutcomeConsolidator();

    public CharacterMedicalOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new CharacterMedicalOutcomeProjector(josa);
    public GameplayOutcomeTypeId OutcomeTypeId => CharacterMedicalOutcomeIds.Completed;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId role) =>
        role.Equals(CharacterMedicalOutcomeIds.Patient)
        || role.Equals(CharacterMedicalOutcomeIds.Clinician);
    public bool IsKnownMetric(GameplayMetricId id, GameplayMetricUnitId unit) =>
        unit.Equals(CharacterMedicalOutcomeIds.Points)
        && (id.Equals(CharacterMedicalOutcomeIds.HealthBefore)
            || id.Equals(CharacterMedicalOutcomeIds.HealthAfter)
            || id.Equals(CharacterMedicalOutcomeIds.BloodLossBefore)
            || id.Equals(CharacterMedicalOutcomeIds.BloodLossAfter)
            || id.Equals(CharacterMedicalOutcomeIds.PartDeficitBefore)
            || id.Equals(CharacterMedicalOutcomeIds.PartDeficitAfter)
            || id.Equals(CharacterMedicalOutcomeIds.AnatomyDeficitBefore)
            || id.Equals(CharacterMedicalOutcomeIds.AnatomyDeficitAfter)
            || id.Equals(CharacterMedicalOutcomeIds.AnatomyBleedingBefore)
            || id.Equals(CharacterMedicalOutcomeIds.AnatomyBleedingAfter)
            || id.Equals(CharacterMedicalOutcomeIds.SurfaceBleedingBefore)
            || id.Equals(CharacterMedicalOutcomeIds.SurfaceBleedingAfter)
            || id.Equals(CharacterMedicalOutcomeIds.CompletedWork)
            || id.Equals(CharacterMedicalOutcomeIds.RequiredWork));
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.MetricCount == 14
        && outcome.SubjectCount == 2
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 1
        && outcome.FactCount == 8
        && outcome.GetParticipant(0).RoleId.Equals(CharacterMedicalOutcomeIds.Patient)
        && outcome.GetParticipant(1).RoleId.Equals(CharacterMedicalOutcomeIds.Clinician)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("medical-outcome-shape-invalid");
}

internal sealed class CharacterMedicalOutcomeProjector :
    INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;
    public CharacterMedicalOutcomeProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant patient = outcome.GetParticipant(0);
        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            patient.DisplayName, KoreanJosaKind.Topic));
        bool stabilization = false;
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(CharacterMedicalOutcomeIds.Procedure))
            {
                stabilization = fact.Value == "stabilization";
                break;
            }
        }
        string action = stabilization ? "응급 안정화" : "치료";
        bool neutral = subject.RequiresNeutralFrame;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            neutral
                ? $"{patient.DisplayName.DisplayText}: {action}를 마쳤다."
                : $"{subject.Text} {action}를 마쳤다.",
            "character-medical-v1",
            neutral);
    }
}

public static class CharacterMedicalOutcomeRegistration
{
    public static void RegisterCharacterMedicalGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(CharacterMedicalOutcomeAdapter));
        builder.Register(resolver => new GameplayDomainFactObserverBinding<
                CharacterMedicalProcedureFact, CharacterMedicalOutcomeReceipt>(
                "log:character.medical-procedure",
                CharacterMedicalFactObservation.Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>(),
                CharacterMedicalFactObservation.Reserve), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<CharacterMedicalFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>()
            .As<IDungeonCapturedSavePreflightValidator>();
    }
}
