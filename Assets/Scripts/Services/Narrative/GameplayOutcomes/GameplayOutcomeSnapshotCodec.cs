using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

internal static class GameplayOutcomeSnapshotCodec
{
    public static GameplayOutcomeSnapshot Capture(GameplayOutcomeValuePage page)
    {
        GameplayOutcomeSnapshot snapshot = new GameplayOutcomeSnapshot
        {
            runId = page.OutcomeId.RunId.Value,
            sequence = page.Sequence,
            producerId = page.ResultKey.ProducerId,
            operationId = page.OperationId.Value,
            commitRevision = page.ResultKey.CommitRevision,
            localResultIndex = page.ResultKey.LocalResultIndex,
            outcomeTypeId = page.OutcomeTypeId.Value,
            absoluteDay = page.AbsoluteDay,
            status = page.Status,
            ownerRevision = page.OwnerRevision,
            commitGeneration = page.CommitGeneration,
            lifecycle = (GameplayOutcomeRecordLifecycle)(int)page.State,
            storageTier = page.StorageTier,
            locationId = page.Location.LocationId ?? string.Empty,
            roomId = page.Location.RoomId ?? string.Empty,
            locationX = page.Location.X,
            locationY = page.Location.Y,
            parentRunId = page.Causation.ParentOutcomeId.RunId.Value ?? string.Empty,
            parentSequence = page.Causation.ParentOutcomeId.Sequence,
            rootOperationId = page.Causation.RootOperationId.Value ?? string.Empty,
            causationRelationId = page.Causation.RelationId ?? string.Empty,
            anchorRevision = page.AnchorRevision,
            deliveryFaultCount = page.DeliveryFaultCount,
            lastDeliveryFaultCode = page.LastDeliveryFaultCode ?? string.Empty,
            immutablePayloadHash = page.MaterializeImmutablePayloadHash(),
            participants = new List<GameplayOutcomeParticipantSnapshot>(page.ParticipantCount),
            metrics = new List<GameplayOutcomeMetricSnapshot>(page.MetricCount),
            subjects = new List<GameplayOutcomeSubjectSnapshot>(page.SubjectCount),
            initialSubjects = new List<GameplayOutcomeSubjectSnapshot>(page.InitialSubjectCount),
            tags = new List<string>(page.TagCount),
            anchors = new List<NarrativeEvidenceReferenceSnapshot>(page.AnchorCount),
            initialAnchors = new List<NarrativeEvidenceReferenceSnapshot>(page.InitialAnchorCount),
            provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>(page.ProvenanceCount),
            facts = new List<GameplayOutcomeFactSnapshot>(page.FactCount)
        };
        for (int index = 0; index < page.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant value = page.Participants[index];
            snapshot.participants.Add(CaptureParticipant(value));
        }
        for (int index = 0; index < page.MetricCount; index++)
        {
            GameplayOutcomeMetric value = page.Metrics[index];
            snapshot.metrics.Add(new GameplayOutcomeMetricSnapshot
            {
                metricId = value.MetricId.Value,
                value = value.Value,
                unitId = value.UnitId.Value,
                referenceKindId = value.DefinitionOrInstanceId.Kind.Value ?? string.Empty,
                referenceId = value.DefinitionOrInstanceId.Value ?? string.Empty
            });
        }
        for (int index = 0; index < page.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink value = page.Subjects[index];
            snapshot.subjects.Add(new GameplayOutcomeSubjectSnapshot
            {
                entityKindId = value.SubjectId.Kind.Value,
                entityId = value.SubjectId.Value,
                salience = value.Salience,
                tier = value.Tier,
                isPinned = value.IsPinned,
                isOptionalWitness = value.IsOptionalWitness,
                anchorRevision = value.AnchorRevision,
                nextEvaluationDay = value.NextEvaluationDay,
                influenceUseCount = value.InfluenceUseCount,
                influenceRevision = value.InfluenceRevision
            });
        }
        for (int index = 0; index < page.InitialSubjectCount; index++)
        {
            GameplayOutcomeSubjectLink value = page.InitialSubjects[index];
            snapshot.initialSubjects.Add(new GameplayOutcomeSubjectSnapshot
            {
                entityKindId = value.SubjectId.Kind.Value,
                entityId = value.SubjectId.Value,
                salience = value.Salience,
                tier = value.Tier,
                isPinned = value.IsPinned,
                isOptionalWitness = value.IsOptionalWitness,
                anchorRevision = value.AnchorRevision,
                nextEvaluationDay = value.NextEvaluationDay,
                influenceUseCount = value.InfluenceUseCount,
                influenceRevision = value.InfluenceRevision
            });
        }
        for (int index = 0; index < page.TagCount; index++)
            snapshot.tags.Add(page.Tags[index].Value);
        for (int index = 0; index < page.AnchorCount; index++)
        {
            GameplayOutcomeAnchorRow value = page.Anchors[index];
            snapshot.anchors.Add(new NarrativeEvidenceReferenceSnapshot
            {
                subjectKindId = value.SubjectId.Kind.Value,
                subjectId = value.SubjectId.Value,
                anchorTypeId = value.Reference.AnchorTypeId,
                anchorId = value.Reference.AnchorId
            });
        }
        for (int index = 0; index < page.InitialAnchorCount; index++)
        {
            GameplayOutcomeAnchorRow value = page.InitialAnchors[index];
            snapshot.initialAnchors.Add(new NarrativeEvidenceReferenceSnapshot
            {
                subjectKindId = value.SubjectId.Kind.Value,
                subjectId = value.SubjectId.Value,
                anchorTypeId = value.Reference.AnchorTypeId,
                anchorId = value.Reference.AnchorId
            });
        }
        for (int index = 0; index < page.ProvenanceCount; index++)
        {
            GameplayOutcomeProvenanceReference value = page.Provenance[index];
            snapshot.provenance.Add(new GameplayOutcomeProvenanceReferenceSnapshot
            {
                kindId = value.KindId,
                value = value.Value
            });
        }
        for (int index = 0; index < page.FactCount; index++)
        {
            GameplayOutcomeFact value = page.Facts[index];
            snapshot.facts.Add(new GameplayOutcomeFactSnapshot
            {
                factId = value.FactId.Value,
                value = value.Value
            });
        }
        return snapshot;
    }

    internal static GameplayOutcomeParticipantSnapshot CaptureParticipant(
        in GameplayOutcomeParticipant value) => new GameplayOutcomeParticipantSnapshot
    {
        entityKindId = value.EntityId.Kind.Value,
        entityId = value.EntityId.Value,
        roleId = value.RoleId.Value,
        participationKind = value.ParticipationKind,
        hasPerceptionEvidence = value.HasPerceptionEvidence,
        displayText = value.DisplayName.DisplayText,
        displayRevision = value.DisplayName.DisplaySnapshotRevision,
        locale = value.DisplayName.Locale,
        pronunciationMode = value.DisplayName.PronunciationHint.Mode,
        pronunciationValue = value.DisplayName.PronunciationHint.Value,
        finalConsonant = value.DisplayName.PronunciationHint.ExplicitFinalConsonant,
        pronunciationRevision = value.DisplayName.PronunciationHint.Revision
    };

    internal static GameplayOutcomeParticipantSnapshot CloneParticipant(
        GameplayOutcomeParticipantSnapshot source)
    {
        if (source == null)
            return null;
        return new GameplayOutcomeParticipantSnapshot
        {
            entityKindId = source.entityKindId,
            entityId = source.entityId,
            roleId = source.roleId,
            participationKind = source.participationKind,
            hasPerceptionEvidence = source.hasPerceptionEvidence,
            displayText = source.displayText,
            displayRevision = source.displayRevision,
            locale = source.locale,
            pronunciationMode = source.pronunciationMode,
            pronunciationValue = source.pronunciationValue,
            finalConsonant = source.finalConsonant,
            pronunciationRevision = source.pronunciationRevision
        };
    }

    internal static bool HasSameParticipantPayload(
        GameplayOutcomeParticipantSnapshot left,
        GameplayOutcomeParticipantSnapshot right) =>
        left != null && right != null && CompareParticipants(left, right) == 0;

    internal static int CompareParticipants(
        GameplayOutcomeParticipantSnapshot left,
        GameplayOutcomeParticipantSnapshot right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int value = string.CompareOrdinal(left.entityKindId, right.entityKindId);
        if (value != 0) return value;
        value = string.CompareOrdinal(left?.entityId, right?.entityId);
        if (value != 0) return value;
        value = string.CompareOrdinal(left?.roleId, right?.roleId);
        if (value != 0) return value;
        value = left.participationKind.CompareTo(right.participationKind);
        if (value != 0) return value;
        value = left.hasPerceptionEvidence.CompareTo(right.hasPerceptionEvidence);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.displayRevision, right.displayRevision);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.displayText, right.displayText);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.locale, right.locale);
        if (value != 0) return value;
        value = left.pronunciationMode.CompareTo(right.pronunciationMode);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.pronunciationRevision, right.pronunciationRevision);
        if (value != 0) return value;
        value = string.CompareOrdinal(left.pronunciationValue, right.pronunciationValue);
        return value != 0 ? value : left.finalConsonant.CompareTo(right.finalConsonant);
    }
}
