using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class SocialRitualFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        FestivalResolutionFact fact)
    {
        FestivalResolutionFactData data = fact.Snapshot();
        data.Validate();

        string summary = "축제 개최: occurrence=" + data.occurrenceId
            + "; festival=" + data.festivalId
            + "; day=" + data.absoluteDay.ToString(
                CultureInfo.InvariantCulture)
            + "; grade=" + data.grade
            + "; participants=["
            + string.Join(",", data.participantIds)
            + "]; assigned="
            + data.assignedParticipantCount.ToString(
                CultureInfo.InvariantCulture)
            + "; capacity="
            + data.venueCapacity.ToString(CultureInfo.InvariantCulture)
            + "; planned-seconds="
            + data.plannedDurationSeconds.ToString(
                "R",
                CultureInfo.InvariantCulture)
            + "; elapsed-seconds="
            + data.elapsedFestivalSeconds.ToString(
                "R",
                CultureInfo.InvariantCulture)
            + "; hazard-stop="
            + (data.stoppedByVenueHazard ? "true" : "false");
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.OperationKind,
                    data.occurrenceId,
                    string.IsNullOrWhiteSpace(data.displayName)
                        ? data.festivalId
                        : data.displayName,
                    MigratedProducerOutcomeIds.OperationRole)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FestivalCelebratedEvent,
            data.occurrenceId,
            data.absoluteDay,
            data.grade switch
            {
                FestivalResolutionGrade.Success =>
                    GameplayOutcomeStatus.Succeeded,
                FestivalResolutionGrade.Partial =>
                    GameplayOutcomeStatus.PartiallySucceeded,
                _ => GameplayOutcomeStatus.Failed
            },
            details);
    }

    internal static MigratedDomainFactCapture Capture(
        ObservedProductionLossLifeEventReceipt receipt)
    {
        List<MigratedProducerOutcomeSubject> subjects = new()
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.CharacterKind,
                receipt.StudentCharacterId.Value,
                receipt.StudentCharacterId.Value,
                MigratedProducerOutcomeIds.ActorRole),
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.CharacterKind,
                receipt.MentorCharacterId.Value,
                receipt.MentorCharacterId.Value,
                MigratedProducerOutcomeIds.WitnessRole),
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.FacilityKind,
                receipt.FacilityId.Value,
                receipt.FacilityId.Value,
                MigratedProducerOutcomeIds.FacilityRole)
        };
        if (!receipt.AcademyBuildingId.Equals(receipt.FacilityId))
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.FacilityKind,
                receipt.AcademyBuildingId.Value,
                receipt.AcademyBuildingId.Value,
                MigratedProducerOutcomeIds.SourceRole));
        }

        GameplayEntityId facilityEntity = new(
            MigratedProducerOutcomeIds.FacilityKind,
            receipt.FacilityId.Value);
        MigratedProducerOutcomeDetails details = new(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MassMetric,
                    receipt.DeclaredLossMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    facilityEntity)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.RecipeFact,
                    receipt.RecipeId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BillFact,
                    receipt.BillId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BatchFact,
                    receipt.BatchCommitId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.FingerprintFact,
                    receipt.OutcomeFingerprint),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ProficiencyFact,
                    receipt.ProficiencyId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.FacilityFact,
                    receipt.AcademyBuildingId.Value)
            },
            "생산 손실 목격: student=" + receipt.StudentCharacterId.Value
            + "; mentor=" + receipt.MentorCharacterId.Value
            + "; facility=" + receipt.FacilityId.Value
            + "; academy=" + receipt.AcademyBuildingId.Value
            + "; bill=" + receipt.BillId.Value
            + "; batch=" + receipt.BatchCommitId
            + "; loss-grams=" + receipt.DeclaredLossMassGrams,
            ProductionCompletionParentCausation.Capture(
                receipt.ParentPublication,
                receipt.BillId,
                receipt.CycleSequence));

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ObservedProductionLossLifeEventReceipt,
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    internal static MigratedDomainFactCapture Capture(
        ObservedLastLessonLifeEventReceipt receipt)
    {
        GameplayEntityId awardEntity = new(
            MigratedProducerOutcomeIds.ItemKind,
            receipt.AwardLedgerStackId);
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    receipt.MentorCharacterId.Value,
                    receipt.MentorCharacterId.Value,
                    MigratedProducerOutcomeIds.ActorRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    receipt.StudentCharacterId.Value,
                    receipt.StudentCharacterId.Value,
                    MigratedProducerOutcomeIds.TargetRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.FacilityKind,
                    receipt.AcademyBuildingId.Value,
                    receipt.AcademyBuildingId.Value,
                    MigratedProducerOutcomeIds.FacilityRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.ItemKind,
                    receipt.ProtectiveEquipmentInstanceId,
                    receipt.ProtectiveEquipmentInstanceId,
                    MigratedProducerOutcomeIds.ItemRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.ItemKind,
                    receipt.AwardLedgerStackId,
                    receipt.AwardLedgerStackId,
                    MigratedProducerOutcomeIds.DestinationRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    receipt.AwardBeforeContentRevision,
                    MigratedProducerOutcomeIds.RevisionUnit,
                    awardEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    receipt.AwardAfterContentRevision,
                    MigratedProducerOutcomeIds.RevisionUnit,
                    awardEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.DayMetric,
                    receipt.RetirementDecisionAbsoluteDay,
                    MigratedProducerOutcomeIds.DayUnit,
                    awardEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.DurationMetric,
                    receipt.RetirementDueAbsoluteDay
                        - receipt.RetirementDecisionAbsoluteDay,
                    MigratedProducerOutcomeIds.DayUnit,
                    awardEntity)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ProficiencyFact,
                    receipt.ProficiencyId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.RetirementEventFact,
                    receipt.RetirementEventId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.RetirementChoiceFact,
                    receipt.RetirementChoiceId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.EquipmentDefinitionFact,
                    receipt.ProtectiveEquipmentDefinitionId)
            },
            "마지막 가르침 목격: mentor="
            + receipt.MentorCharacterId.Value
            + "; student=" + receipt.StudentCharacterId.Value
            + "; academy=" + receipt.AcademyBuildingId.Value
            + "; proficiency=" + receipt.ProficiencyId.Value
            + "; equipment=" + receipt.ProtectiveEquipmentInstanceId
            + "; award=" + receipt.AwardLedgerStackId
            + "; revision=" + receipt.AwardBeforeContentRevision
            + "->" + receipt.AwardAfterContentRevision
            + "; day=" + receipt.AbsoluteDay
            + "; generation=" + receipt.Generation);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    internal static MigratedDomainFactCapture Capture(
        ObservedQuietPromotionLifeEventReceipt receipt)
    {
        GameplayEntityId characterEntity = new(
            MigratedProducerOutcomeIds.CharacterKind,
            receipt.CharacterId.Value);
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    receipt.CharacterId.Value,
                    receipt.CharacterId.Value,
                    MigratedProducerOutcomeIds.ActorRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentExperienceBeforeMetric,
                    receipt.BeforeCurrentMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentExperienceAfterMetric,
                    receipt.AfterCurrentMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.LifetimeExperienceBeforeMetric,
                    receipt.BeforeLifetimeMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.LifetimeExperienceAfterMetric,
                    receipt.AfterLifetimeMilliExperience,
                    MigratedProducerOutcomeIds.MilliExperienceUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.RankBeforeMetric,
                    (int)receipt.BeforeRank,
                    MigratedProducerOutcomeIds.EnumUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.RankAfterMetric,
                    (int)receipt.AfterRank,
                    MigratedProducerOutcomeIds.EnumUnit,
                    characterEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.AbsoluteHourMetric,
                    receipt.AbsoluteHour,
                    MigratedProducerOutcomeIds.HourUnit,
                    characterEntity)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ProficiencyFact,
                    receipt.ProficiencyId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AwardKindFact,
                    ((int)receipt.AwardKind).ToString(
                        CultureInfo.InvariantCulture)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    "generation:" + receipt.Generation)
            },
            "조용한 승진 목격: character=" + receipt.CharacterId.Value
            + "; proficiency=" + receipt.ProficiencyId.Value
            + "; rank=" + receipt.BeforeRank + "->" + receipt.AfterRank
            + "; current=" + receipt.BeforeCurrentMilliExperience
            + "->" + receipt.AfterCurrentMilliExperience
            + "; lifetime=" + receipt.BeforeLifetimeMilliExperience
            + "->" + receipt.AfterLifetimeMilliExperience
            + "; hour=" + receipt.AbsoluteHour
            + "; day=" + receipt.AbsoluteDay
            + "; generation=" + receipt.Generation);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    internal static MigratedDomainFactCapture Capture(
        ObservedFuneralLifeEventReceipt receipt)
    {
        MigratedProducerOutcomeSubject[] subjects = new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    receipt.DeceasedCharacterId.Value,
                    receipt.DeceasedCharacterId.Value,
                    MigratedProducerOutcomeIds.TargetRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.FacilityKind,
                    receipt.FacilityInstanceId,
                    receipt.FacilityInstanceId,
                    MigratedProducerOutcomeIds.FacilityRole)
            }
            .Concat(receipt.ParticipantCharacterIds.Select(value =>
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    value.Value,
                    value.Value,
                    MigratedProducerOutcomeIds.WitnessRole)))
            .ToArray();
        MigratedProducerOutcomeDetails details = new(
            subjects,
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CountMetric,
                    receipt.ParticipantCharacterIds.Count,
                    MigratedProducerOutcomeIds.CountUnit,
                    subjects[0].EntityId)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.FacilityFact,
                    receipt.FacilityInstanceId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    "generation:" + receipt.Generation)
            },
            "장례 목격: deceased=" + receipt.DeceasedCharacterId.Value
            + "; operation=" + receipt.SourceOperationId
            + "; facility=" + receipt.FacilityInstanceId
            + "; day=" + receipt.AbsoluteDay
            + "; generation=" + receipt.Generation
            + "; participants=" + string.Join(",",
                receipt.ParticipantCharacterIds.Select(value => value.Value)));

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    internal static MigratedDomainFactCapture Capture(
        ObservedLineageCompressionLifeEventReceipt receipt)
    {
        MigratedProducerOutcomeSubject character = new(
            MigratedProducerOutcomeIds.CharacterKind,
            receipt.CharacterId.Value,
            receipt.CharacterId.Value,
            MigratedProducerOutcomeIds.TargetRole);
        MigratedProducerOutcomeSubject household = new(
            MigratedProducerOutcomeIds.OperationKind,
            receipt.SummaryHouseholdId,
            receipt.SummaryHouseholdId,
            MigratedProducerOutcomeIds.OwnerRole);
        MigratedProducerOutcomeDetails details = new(
            new[] { character, household },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CountMetric,
                    receipt.SummaryArchivedCharacterCount,
                    MigratedProducerOutcomeIds.CountUnit,
                    household.EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    receipt.SummaryEarliestBirthDay,
                    MigratedProducerOutcomeIds.DayUnit,
                    household.EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    receipt.SummaryLatestDeathDay,
                    MigratedProducerOutcomeIds.DayUnit,
                    household.EntityId)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.CharacterFact,
                    receipt.CharacterId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SpeciesFact,
                    receipt.PhenotypeSpeciesId.Value),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.HouseholdFact,
                    receipt.SummaryHouseholdId)
            },
            "계보 압축 목격: character=" + receipt.CharacterId.Value
            + "; species=" + receipt.PhenotypeSpeciesId.Value
            + "; household=" + receipt.SummaryHouseholdId
            + "; generation=" + receipt.Generation
            + "; archive-day=" + receipt.ArchiveAbsoluteDay
            + "; archived-count=" + receipt.SummaryArchivedCharacterCount
            + "; life=" + receipt.BirthAbsoluteDay
            + "->" + receipt.DeathAbsoluteDay);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind
                .ObservedLineageCompressionLifeEventReceipt,
            receipt.SourceOperationId,
            receipt.ArchiveAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
