#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterProgressionSavePlayModeFacade
{
    private const string StaffWorkRoundTripId =
        "staff:character-world-work-round-trip";

    [MenuItem("DungeonStory/Debug/Character/Run Progression Save Round Trip")]
    public static void RunFromMenu()
    {
        if (!Run(out string message))
        {
            throw new InvalidOperationException(message);
        }

        Debug.Log(message);
    }

    public static bool Run(out string message)
    {
        if (!Application.isPlaying)
        {
            message = "Character progression save verification requires PlayMode.";
            return false;
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            message = "Dungeon runtime container is missing.";
            return false;
        }

        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        IOwnerRunManagerProvider ownerProvider = scope.Container.Resolve<IOwnerRunManagerProvider>();
        if (!ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager.CurrentOwnerActor == null)
        {
            message = "Owner runtime is missing.";
            return false;
        }

        DungeonGameSaveData baseline = saveService.Capture();
        try
        {
            if (!ValidateStrictCharacterRestoreContracts(
                    scope,
                    saveService,
                    ownerManager.CurrentOwnerActor,
                    baseline,
                    out string strictFailure))
            {
                message = strictFailure;
                return false;
            }

            CharacterActor owner = ownerManager.CurrentOwnerActor;
            owner.EnsureRuntimeState();
            CharacterProgressionSnapshot initialProgression = owner.Progression.CapturePersistentState();
            owner.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
                4,
                77,
                initialProgression.GrowthState,
                initialProgression.NarrativeLedger));
            CharacterProgressionSnapshot expectedProgression = owner.Progression.CapturePersistentState();
            int expectedLevel = expectedProgression.Level;
            int expectedExperience = expectedProgression.CurrentExperience;
            string expectedGrowthJson = JsonUtility.ToJson(expectedProgression.GrowthState);
            string expectedNarrativeJson = JsonUtility.ToJson(expectedProgression.NarrativeLedger);

            DungeonGameSaveData captured = saveService.Capture();
            DungeonCharacterWorldSaveData savedCharacters =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                    captured,
                    CharacterWorldSaveSection.Id);
            DungeonCharacterSaveData savedOwner = savedCharacters.actors
                .FirstOrDefault(actor => actor != null && actor.isOwner);
            bool ownerPresent = savedOwner != null;
            bool levelMatches = ownerPresent && savedOwner.level == expectedLevel;
            bool experienceMatches = ownerPresent
                && savedOwner.currentExperience == expectedExperience;
            bool growthMatches = ownerPresent
                && JsonUtility.ToJson(savedOwner.growth) == expectedGrowthJson;
            bool narrativeMatches = ownerPresent
                && JsonUtility.ToJson(savedOwner.narrative) == expectedNarrativeJson;
            if (!ownerPresent
                || !levelMatches
                || !experienceMatches
                || !growthMatches
                || !narrativeMatches)
            {
                message = "Captured game save did not contain the exact owner progression state: "
                    + $"owner={ownerPresent}; level={savedOwner?.level}/{expectedLevel}:{levelMatches}; "
                    + $"xp={savedOwner?.currentExperience}/{expectedExperience}:{experienceMatches}; "
                    + $"growth={growthMatches}; narrative={narrativeMatches}.";
                return false;
            }

            IRunCharacterCatalog characterCatalog =
                scope.Container.Resolve<IRunCharacterCatalog>();
            CharacterSO staffData = characterCatalog.Characters
                .FirstOrDefault(candidate => candidate != null
                    && candidate.characterType == CharacterType.NPC
                    && candidate.role != CharacterRole.Owner);
            if (staffData == null)
            {
                message = "Character work-state round trip requires an authored NPC staff definition.";
                return false;
            }

            DungeonCharacterSaveData staffContract =
                JsonUtility.FromJson<DungeonCharacterSaveData>(
                    JsonUtility.ToJson(savedOwner));
            staffContract.persistentId = StaffWorkRoundTripId;
            staffContract.dataId = staffData.id;
            staffContract.isOwner = false;
            staffContract.displayName = "Character World Work Round Trip";
            staffContract.characterType = staffData.characterType;
            staffContract.role = staffData.role;
            staffContract.workPriorities = new List<DungeonCharacterWorkPrioritySaveData>
            {
                new DungeonCharacterWorkPrioritySaveData
                {
                    workTypeId = BuiltInWorkTypeIds.Haul.Value,
                    priority = WorkPriorityLevel.Priority1
                }
            };
            staffContract.dutyState = AbilityWork.DutyState.OffDuty;
            savedCharacters.actors.Add(staffContract);
            DungeonSaveSectionPayload.Write(
                captured,
                CharacterWorldSaveSection.Id,
                1,
                DungeonSaveRestorePhase.Characters,
                savedCharacters);

            DungeonGameSaveData parsed = saveService.FromJson(saveService.ToJson(captured, prettyPrint: true));
            DungeonGameSaveData incompatible = saveService.FromJson(saveService.ToJson(captured));
            incompatible.version = DungeonGameSaveData.CurrentVersion - 1;
            if (saveService.TryRestore(incompatible, out DungeonGameRestoreReport incompatibleReport)
                || !incompatibleReport.Errors.Any(error => string.Equals(
                    error,
                    DungeonSaveCompatibility.PreV18IncompatibilityReason,
                    StringComparison.Ordinal)))
            {
                message = "Legacy growth save was not rejected with the new-game compatibility message.";
                return false;
            }

            owner.Progression.RestorePersistentState(1, 0, null, null);
            if (!saveService.TryRestore(parsed, out DungeonGameRestoreReport report))
            {
                message = "Progression game-save restore failed: " + string.Join(" | ", report.Errors);
                return false;
            }

            if (!ownerProvider.TryGetManager(out ownerManager)
                || ownerManager.CurrentOwnerActor == null)
            {
                message = "Restored owner runtime is missing.";
                return false;
            }

            CharacterProgression restored = ownerManager.CurrentOwnerActor.Progression;
            if (restored == null
                || restored.Level != expectedLevel
                || restored.CurrentExperience != expectedExperience
                || JsonUtility.ToJson(restored.GrowthState) != expectedGrowthJson
                || JsonUtility.ToJson(restored.NarrativeLedger) != expectedNarrativeJson)
            {
                message = restored == null
                    ? "Restored owner has no progression component."
                    : $"Progression mismatch after restore: Lv.{restored.Level}, XP {restored.CurrentExperience}, active={restored.ActiveSkills.Count}, passive={restored.PassiveSkills.Count}";
                return false;
            }

            ICharacterWorldSaveService worldSave =
                scope.Container.Resolve<ICharacterWorldSaveService>();
            if (!worldSave.TryGetRestoredActor(
                    StaffWorkRoundTripId,
                    out CharacterActor restoredStaff))
            {
                message = "Restored staff work-state contract actor is missing.";
                return false;
            }

            AbilityWork restoredWork = restoredStaff.GetAbility<AbilityWork>();
            if (restoredWork == null
                || restoredWork.CurrentDutyState != AbilityWork.DutyState.OffDuty
                || restoredWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)
                    != WorkPriorityLevel.Priority1)
            {
                message = "Restored staff did not preserve AbilityWork duty and priority state.";
                return false;
            }

            DungeonCharacterSaveData recapturedStaff =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                        saveService.Capture(),
                        CharacterWorldSaveSection.Id)
                    .actors
                    .SingleOrDefault(actor => actor != null
                        && string.Equals(
                            actor.persistentId,
                            StaffWorkRoundTripId,
                            StringComparison.Ordinal));
            if (recapturedStaff == null
                || recapturedStaff.dutyState != AbilityWork.DutyState.OffDuty
                || recapturedStaff.workPriorities.SingleOrDefault(priority =>
                        priority != null
                        && string.Equals(
                            priority.workTypeId,
                            BuiltInWorkTypeIds.Haul.Value,
                            StringComparison.Ordinal))?.priority
                    != WorkPriorityLevel.Priority1)
            {
                message = "Restored staff AbilityWork state was not stable on recapture.";
                return false;
            }

            message = $"CHARACTER_WORLD_V18_CONTRACTS_PASSED Lv.{restored.Level} XP={restored.CurrentExperience} active={restored.ActiveSkills.Count} passive={restored.PassiveSkills.Count} staffWorkRoundTrip=true directRejected=true ownerlessRejected=true invalidCellRejected=true rollbackFreeLateFailure=true legacyRejected=true warnings={report.Warnings.Count}";
            return true;
        }
        finally
        {
            if (!saveService.TryRestore(baseline, out DungeonGameRestoreReport baselineReport))
            {
                Debug.LogError("Failed to restore the progression verification baseline: "
                    + string.Join(" | ", baselineReport.Errors));
            }
        }
    }

    private static bool ValidateStrictCharacterRestoreContracts(
        DungeonRuntimeLifetimeScope scope,
        IDungeonGameSaveService saveService,
        CharacterActor liveOwner,
        DungeonGameSaveData baseline,
        out string failure)
    {
        ICharacterWorldSaveService worldSave =
            scope.Container.Resolve<ICharacterWorldSaveService>();
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        IRestoreWorldCandidateQuery candidates =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        IOwnerRunManagerProvider ownerProvider =
            scope.Container.Resolve<IOwnerRunManagerProvider>();
        if (!gridProvider.TryGetGrid(out Grid liveGrid))
        {
            failure = "Strict character restore verification has no live Grid.";
            return false;
        }

        DungeonCharacterWorldSaveData canonical =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                baseline,
                CharacterWorldSaveSection.Id);
        CharacterWorldRestoreCandidate directCandidate =
            worldSave.PrepareRestoreCandidate(liveGrid, canonical);
        bool directRejected = false;
        try
        {
            worldSave.StageRestoreCandidate(directCandidate);
        }
        catch (InvalidOperationException exception)
        {
            directRejected = exception.Message.Contains(
                "staging is not active",
                StringComparison.Ordinal);
        }
        finally
        {
            directCandidate.Discard();
        }
        if (!directRejected || candidates.TryGetCharacters(out _))
        {
            failure = "Character restore did not reject a direct live-world call.";
            return false;
        }

        if (!RejectsInvalidCharacterId(
                saveService,
                ownerProvider,
                liveOwner,
                baseline,
                candidates,
                "Named Hero",
                "name-like",
                out failure)
            || !RejectsInvalidCharacterId(
                saveService,
                ownerProvider,
                liveOwner,
                baseline,
                candidates,
                "building:fixture",
                "building-prefix",
                out failure))
        {
            return false;
        }

        DungeonGameSaveData ownerless = saveService.FromJson(
            saveService.ToJson(baseline));
        DungeonCharacterWorldSaveData ownerlessCharacters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                ownerless,
                CharacterWorldSaveSection.Id);
        ownerlessCharacters.actors.RemoveAll(actor => actor != null && actor.isOwner);
        DungeonSaveSectionPayload.Write(
            ownerless,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            ownerlessCharacters);
        if (saveService.TryRestore(ownerless, out DungeonGameRestoreReport ownerlessReport)
            || !ownerlessReport.Errors.Any(error => error.Contains(
                "exactly one owner actor",
                StringComparison.Ordinal))
            || !OwnerIsUnchanged(ownerProvider, liveOwner)
            || candidates.TryGetCharacters(out _))
        {
            failure = "Ownerless character payload did not fail atomically.";
            return false;
        }

        DungeonGameSaveData invalidPosition = saveService.FromJson(
            saveService.ToJson(baseline));
        DungeonCharacterWorldSaveData invalidCharacters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                invalidPosition,
                CharacterWorldSaveSection.Id);
        DungeonCharacterSaveData invalidOwner = invalidCharacters.actors
            .Single(actor => actor != null && actor.isOwner);
        invalidOwner.lifecycleState = CharacterLifecycleState.Active;
        invalidOwner.gridX = int.MinValue;
        invalidOwner.gridY = int.MinValue;
        DungeonSaveSectionPayload.Write(
            invalidPosition,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            invalidCharacters);
        if (saveService.TryRestore(invalidPosition, out DungeonGameRestoreReport positionReport)
            || !positionReport.Errors.Any(error => error.Contains(
                "not walkable in the candidate grid",
                StringComparison.Ordinal))
            || !OwnerIsUnchanged(ownerProvider, liveOwner)
            || candidates.TryGetCharacters(out _))
        {
            failure = "Invalid character position did not fail atomically.";
            return false;
        }

        if (!ValidateRollbackFreeLateFailure(
                scope,
                baseline,
                liveOwner,
                liveGrid,
                candidates,
                out failure))
        {
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool RejectsInvalidCharacterId(
        IDungeonGameSaveService saveService,
        IOwnerRunManagerProvider ownerProvider,
        CharacterActor liveOwner,
        DungeonGameSaveData baseline,
        IRestoreWorldCandidateQuery candidates,
        string invalidId,
        string label,
        out string failure)
    {
        DungeonGameSaveData invalidSave = saveService.FromJson(
            saveService.ToJson(baseline));
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                invalidSave,
                CharacterWorldSaveSection.Id);
        DungeonCharacterSaveData owner = characters.actors
            .Single(actor => actor != null && actor.isOwner);
        owner.persistentId = invalidId;
        DungeonSaveSectionPayload.Write(
            invalidSave,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            characters);

        if (saveService.TryRestore(
                invalidSave,
                out DungeonGameRestoreReport report)
            || !report.Errors.Any(error => error.Contains(
                "no valid persistent ID",
                StringComparison.Ordinal))
            || !OwnerIsUnchanged(ownerProvider, liveOwner)
            || candidates.TryGetCharacters(out _))
        {
            failure = $"Character world accepted a {label} CharacterId '{invalidId}'.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateRollbackFreeLateFailure(
        DungeonRuntimeLifetimeScope scope,
        DungeonGameSaveData baseline,
        CharacterActor liveOwner,
        Grid liveGrid,
        IRestoreWorldCandidateQuery candidates,
        out string failure)
    {
        IDungeonSaveSectionRegistry liveRegistry =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        IDungeonSaveSection facilitySection = liveRegistry.OrderedSections
            .Single(section => section.SectionId == ModularFacilityWorldSaveSection.Id);
        IDungeonSaveSection characterSection = liveRegistry.OrderedSections
            .Single(section => section.SectionId == CharacterWorldSaveSection.Id);
        IDungeonRestoreTransactionParticipant facilityParticipant =
            scope.Container.Resolve<IModularFacilityWorldSaveService>()
                as IDungeonRestoreTransactionParticipant;
        IDungeonRestoreTransactionParticipant characterParticipant =
            scope.Container.Resolve<ICharacterWorldSaveService>()
                as IDungeonRestoreTransactionParticipant;
        if (facilityParticipant == null || characterParticipant == null)
        {
            failure = "Character atomic verification could not resolve world participants.";
            return false;
        }

        MarkerDependencySection runDependency =
            new MarkerDependencySection(RunVariableSaveSection.Id);
        MarkerDependencySection metaDependency =
            new MarkerDependencySection(MetaProgressionSaveSection.Id);
        FailAfterCharacterSection failSection = new FailAfterCharacterSection();
        DungeonRuntimeAggregateRootStore isolatedRoot =
            new DungeonRuntimeAggregateRootStore();
        DungeonSaveSectionRegistry isolated = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                runDependency,
                metaDependency,
                facilitySection,
                characterSection,
                failSection
            },
            isolatedRoot,
            new[] { facilityParticipant, characterParticipant });
        List<DungeonSaveSectionEnvelope> envelopes = new List<DungeonSaveSectionEnvelope>
        {
            CreateEnvelope(runDependency),
            CreateEnvelope(metaDependency),
            CloneEnvelope(baseline, ModularFacilityWorldSaveSection.Id),
            CloneEnvelope(baseline, CharacterWorldSaveSection.Id),
            CreateEnvelope(failSection)
        };
        int detachedBefore = CountDetachedCharacterCandidates();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = isolated.RestoreAll(envelopes, report);
        IOwnerRunManagerProvider ownerProvider =
            scope.Container.Resolve<IOwnerRunManagerProvider>();
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        bool sameGrid = gridProvider.TryGetGrid(out Grid currentGrid)
            && ReferenceEquals(currentGrid, liveGrid);
        bool candidateIndexClear = !candidates.TryGetGrid(out _)
            && !candidates.TryGetBuildings(out _)
            && !candidates.TryGetCharacters(out _);
        int detachedAfter = CountDetachedCharacterCandidates();
        if (restored
            || report.Success
            || !report.Errors.Any(error => error.Contains(
                FailAfterCharacterSection.FailureMessage,
                StringComparison.Ordinal))
            || !OwnerIsUnchanged(ownerProvider, liveOwner)
            || !sameGrid
            || !candidateIndexClear
            || isolatedRoot.IsRestoreStaging
            || isolatedRoot.PublishedRestoreRevision != 0
            || detachedAfter != detachedBefore)
        {
            failure = "Rollback-free late character failure changed live state: "
                + $"restored={restored}; report={report.Success}; owner={OwnerIsUnchanged(ownerProvider, liveOwner)}; "
                + $"grid={sameGrid}; indexClear={candidateIndexClear}; staging={isolatedRoot.IsRestoreStaging}; "
                + $"revision={isolatedRoot.PublishedRestoreRevision}; detached={detachedBefore}->{detachedAfter}; "
                + $"errors={string.Join(" | ", report.Errors)}";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static DungeonSaveSectionEnvelope CreateEnvelope(
        IDungeonSaveSection section)
    {
        return new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            payloadJson = section.Capture()
        };
    }

    private static DungeonSaveSectionEnvelope CloneEnvelope(
        DungeonGameSaveData save,
        string sectionId)
    {
        DungeonSaveSectionEnvelope source = save.sections.Single(
            envelope => envelope != null && envelope.sectionId == sectionId);
        return new DungeonSaveSectionEnvelope
        {
            sectionId = source.sectionId,
            sectionVersion = source.sectionVersion,
            restorePhase = source.restorePhase,
            optional = source.optional,
            payloadJson = source.payloadJson
        };
    }

    private static int CountDetachedCharacterCandidates()
    {
        return Resources.FindObjectsOfTypeAll<CharacterActor>()
            .Count(actor => actor != null && actor.IsDetachedRestoreCandidate);
    }

    private static bool OwnerIsUnchanged(
        IOwnerRunManagerProvider ownerProvider,
        CharacterActor expected)
    {
        return ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ReferenceEquals(ownerManager.CurrentOwnerActor, expected)
            && expected != null
            && expected.gameObject.activeInHierarchy;
    }

    private sealed class MarkerDependencySection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;

        public MarkerDependencySection(string id)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Foundation;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
        }
    }

    private sealed class FailAfterCharacterSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public const string FailureMessage =
            "Intentional failure after character candidate staging.";

        public override string SectionId => "character.debug.fail-after-staging";
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;
        public override System.Collections.Generic.IReadOnlyList<string> DependsOn =>
            new[] { CharacterWorldSaveSection.Id };

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
            throw new InvalidOperationException(FailureMessage);
        }
    }
}
#endif
