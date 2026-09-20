using System;

public abstract class EnvironmentOutcomeAdapter<TReceipt> : GameplayOutcomeAdapter<TReceipt>
    where TReceipt : struct, IEnvironmentOutcomeReceipt
{
    protected abstract string FailurePrefix { get; }

    public override OutcomePrepareResult TryGetRequirements(
        in TReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        EnvironmentOutcomePayload payload = receipt.Payload;
        requirements = new OutcomeWriteRequirements(
            payload.ResultKey,
            OutcomeTypeId,
            payload.AbsoluteDay,
            payload.Status,
            currentWorldEpoch,
            payload.OwnerRevision,
            payload.Participants.Count,
            payload.Metrics.Count,
            payload.Subjects.Count,
            payload.Tags.Count,
            anchorCount: 0,
            provenanceCount: payload.Provenance.Count,
            factCount: payload.Facts.Count);

        return Validate(payload, out string failure)
            ? OutcomePrepareResult.Prepared()
            : Invalid(failure);
    }

    public override OutcomePrepareResult TryWrite(
        in TReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        EnvironmentOutcomePayload payload = receipt.Payload;
        if (!Validate(payload, out string failure))
            return Invalid(failure);

        for (int index = 0; index < payload.Participants.Count; index++)
        {
            if (!builder.AddParticipant(payload.Participants.Get(index)))
                return WriteFailed("participant");
        }
        for (int index = 0; index < payload.Metrics.Count; index++)
        {
            if (!builder.AddMetric(payload.Metrics.Get(index)))
                return WriteFailed("metric");
        }
        for (int index = 0; index < payload.Subjects.Count; index++)
        {
            if (!builder.AddSubject(payload.Subjects.Get(index)))
                return WriteFailed("subject");
        }
        for (int index = 0; index < payload.Tags.Count; index++)
        {
            if (!builder.AddTag(payload.Tags.Get(index)))
                return WriteFailed("tag");
        }
        // Environment outcomes intentionally use non-retention provenance rather
        // than evidence anchors. An anchor without its subject would be lossy.
        for (int index = 0; index < payload.Provenance.Count; index++)
        {
            if (!builder.AddProvenance(payload.Provenance.Get(index)))
                return WriteFailed("provenance");
        }
        for (int index = 0; index < payload.Facts.Count; index++)
        {
            if (!builder.AddFact(payload.Facts.Get(index)))
                return WriteFailed("fact");
        }
        if (payload.Location.HasLocation && !builder.SetLocation(payload.Location))
            return WriteFailed("location");
        if ((payload.Causation.HasParent
                || payload.Causation.RootOperationId.IsValid
                || !string.IsNullOrEmpty(payload.Causation.RelationId))
            && !builder.SetCausation(payload.Causation))
            return WriteFailed("causation");

        return OutcomePrepareResult.Prepared();
    }

    private bool Validate(EnvironmentOutcomePayload payload, out string failure)
    {
        failure = string.Empty;
        if (!payload.ResultKey.IsValid || payload.OutcomeTypeId != OutcomeTypeId)
            return Fail("identity", out failure);
        if (payload.AbsoluteDay < 0
            || payload.OwnerRevision < 0
            || !Enum.IsDefined(typeof(GameplayOutcomeStatus), payload.Status))
            return Fail("header", out failure);
        if (payload.Participants.Count == 0
            || payload.Subjects.Count == 0
            || payload.Tags.Count != 1
            || payload.Provenance.Count == 0
            || payload.Facts.Count == 0)
            return Fail("shape", out failure);

        for (int index = 0; index < payload.Participants.Count; index++)
        {
            GameplayOutcomeParticipant participant = payload.Participants.Get(index);
            if (!participant.EntityId.IsValid
                || !participant.RoleId.IsValid
                || participant.ParticipationKind != GameplayParticipationKind.Direct
                || !participant.HasPerceptionEvidence
                || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName))
                return Fail("participant", out failure);
        }
        for (int index = 0; index < payload.Metrics.Count; index++)
        {
            GameplayOutcomeMetric metric = payload.Metrics.Get(index);
            if (!metric.MetricId.IsValid
                || !metric.UnitId.IsValid
                || !metric.DefinitionOrInstanceId.IsValid
                || !double.IsFinite(metric.Value))
                return Fail("metric", out failure);
        }
        for (int index = 0; index < payload.Subjects.Count; index++)
        {
            GameplayOutcomeSubjectLink subject = payload.Subjects.Get(index);
            if (!subject.SubjectId.IsValid
                || subject.IsOptionalWitness
                || !float.IsFinite(subject.Salience)
                || subject.Salience < 0f
                || subject.Salience > 1f
                || !ContainsParticipant(payload.Participants, subject.SubjectId))
                return Fail("subject", out failure);
        }
        for (int index = 0; index < payload.Provenance.Count; index++)
        {
            if (!payload.Provenance.Get(index).IsValid)
                return Fail("provenance", out failure);
        }
        for (int index = 0; index < payload.Facts.Count; index++)
        {
            GameplayOutcomeFact fact = payload.Facts.Get(index);
            if (!fact.FactId.IsValid
                || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                    fact.Value,
                    GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                    allowEmpty: false))
                return Fail("fact", out failure);
        }
        return true;
    }

    private static bool ContainsParticipant(
        EnvironmentFixedBuffer16<GameplayOutcomeParticipant> participants,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < participants.Count; index++)
        {
            if (participants.Get(index).EntityId == subjectId)
                return true;
        }
        return false;
    }

    private static bool Fail(string reason, out string failure)
    {
        failure = reason;
        return false;
    }

    private OutcomePrepareResult Invalid(string reason) => new(
        OutcomePrepareCode.InvalidReceipt,
        FailurePrefix + "-" + reason + "-invalid");

    private OutcomePrepareResult WriteFailed(string reason) => new(
        OutcomePrepareCode.AdapterWriteFailed,
        FailurePrefix + "-" + reason + "-write-failed");
}

public sealed class CertifiedSeedCompletionOutcomeAdapter : EnvironmentOutcomeAdapter<CertifiedSeedCompletionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.CertifiedSeedCompleted;
    protected override string FailurePrefix => "certified-seed-completion";
}

public sealed class CropPlanGameplayOutcomeAdapter : EnvironmentOutcomeAdapter<CropPlanGameplayOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.CropPlanTerminal;
    protected override string FailurePrefix => "crop-plan";
}

public sealed class CropIrrigationSupplyOutcomeAdapter :
    EnvironmentOutcomeAdapter<CropIrrigationSupplyOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.CropIrrigationSupplied;
    protected override string FailurePrefix => "crop-irrigation";
}

public sealed class EnvironmentalFireIgnitionOutcomeAdapter :
    EnvironmentOutcomeAdapter<EnvironmentalFireIgnitionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireIgnition;
    protected override string FailurePrefix => "fire-ignition";
}

public sealed class EnvironmentalFireSuppressionOutcomeAdapter :
    EnvironmentOutcomeAdapter<EnvironmentalFireSuppressionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireSuppression;
    protected override string FailurePrefix => "fire-suppression";
}

public sealed class EnvironmentalFireDamageOutcomeAdapter :
    EnvironmentOutcomeAdapter<EnvironmentalFireDamageOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.FireDamage;
    protected override string FailurePrefix => "fire-damage";
}

public sealed class EnvironmentalFireFuelOutcomeAdapter : EnvironmentOutcomeAdapter<EnvironmentalFireFuelOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.FireFuelLoss;
    protected override string FailurePrefix => "fire-fuel";
}

public sealed class EnvironmentalFireWaterOutcomeAdapter : EnvironmentOutcomeAdapter<EnvironmentalFireWaterOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.FireWaterConsumed;
    protected override string FailurePrefix => "fire-water";
}

public sealed class PopulationDiseaseExposureOutcomeAdapter : EnvironmentOutcomeAdapter<PopulationDiseaseExposureOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.DiseaseRouteExposure;
    protected override string FailurePrefix => "disease-exposure";
}

public sealed class ProcessAccidentOutcomeAdapter : EnvironmentOutcomeAdapter<ProcessAccidentOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.ProcessAccident;
    protected override string FailurePrefix => "process-accident";
}

public sealed class RoomConditionOutcomeAdapter : EnvironmentOutcomeAdapter<RoomConditionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.RoomConditionChanged;
    protected override string FailurePrefix => "room-condition";
}

public sealed class RoomEnvironmentExperienceOutcomeAdapter : EnvironmentOutcomeAdapter<RoomEnvironmentExperienceOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.RoomExperienceApplied;
    protected override string FailurePrefix => "room-experience";
}

public sealed class SpeciesIncidentOutcomeAdapter : EnvironmentOutcomeAdapter<SpeciesIncidentOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => EnvironmentOutcomeIds.SpeciesIncidentTriggered;
    protected override string FailurePrefix => "species-incident";
}

public sealed class HarpyGaleRelocationOutcomeAdapter :
    EnvironmentOutcomeAdapter<HarpyGaleRelocationOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        EnvironmentOutcomeIds.HarpyGaleRelocation;
    protected override string FailurePrefix => "harpy-gale-relocation";
}
