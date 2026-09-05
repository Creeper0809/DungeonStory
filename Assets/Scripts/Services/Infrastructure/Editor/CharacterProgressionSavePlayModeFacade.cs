#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterProgressionSavePlayModeFacade
{
    private const string LegacyStaffWorkRoundTripId = "staff:24680:01";
    private const string CanonicalStaffWorkRoundTripId =
        "character:staff:24680:01";

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

        scope.Container.Resolve<ICharacterBodyHealthQuery>()
            .GetSnapshot(ownerManager.CurrentOwnerActor);
        DungeonGameSaveData baseline = saveService.Capture();
        string pristineBaselineCanonical = Canonicalize(baseline);
        string pristineBaselineJson = saveService.ToJson(
            baseline,
            prettyPrint: false);
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
            staffContract.persistentId = LegacyStaffWorkRoundTripId;
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
                CharacterWorldSaveSection.CurrentVersion,
                DungeonSaveRestorePhase.Characters,
                savedCharacters);

            DungeonCharacterBodyHealthSaveData bodyHealth =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterBodyHealthSaveData>(
                    captured,
                    CharacterBodyHealthSaveSection.Id);
            CharacterBodyHealthState ownerBodyHealth = bodyHealth.characters
                .FirstOrDefault(state => state != null
                    && string.Equals(
                        state.characterId,
                        savedOwner.persistentId,
                        StringComparison.Ordinal));
            if (ownerBodyHealth == null)
            {
                message = "Legacy V18 integration fixture requires captured owner body-health state.";
                return false;
            }
            CharacterBodyHealthState staffBodyHealth =
                JsonUtility.FromJson<CharacterBodyHealthState>(
                    JsonUtility.ToJson(ownerBodyHealth));
            staffBodyHealth.characterId = LegacyStaffWorkRoundTripId;
            bodyHealth.characters.Add(staffBodyHealth);
            bodyHealth.characters = bodyHealth.characters
                .OrderBy(
                    state => ReferenceEquals(state, staffBodyHealth)
                        ? CanonicalStaffWorkRoundTripId
                        : state?.characterId,
                    StringComparer.Ordinal)
                .ToList();
            string expectedCanonicalBodyHealthIds = CanonicalizeBodyHealthIds(
                bodyHealth,
                LegacyStaffWorkRoundTripId,
                CanonicalStaffWorkRoundTripId);
            DungeonSaveSectionPayload.Write(
                captured,
                CharacterBodyHealthSaveSection.Id,
                DungeonCharacterBodyHealthSaveData.CurrentVersion,
                DungeonSaveRestorePhase.RuntimeState,
                bodyHealth);

            CharacterLifeWorldSaveData life =
                DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(
                    captured,
                    CharacterLifeSaveSection.Id);
            CharacterLifeRecordSaveData ownerLife = life.characters
                .FirstOrDefault(record => record != null
                    && string.Equals(
                        record.characterId,
                        savedOwner.persistentId,
                        StringComparison.Ordinal));
            if (ownerLife == null)
            {
                message = "Legacy V18 integration fixture requires a captured owner life record.";
                return false;
            }
            CharacterLifeRecordSaveData staffLife =
                JsonUtility.FromJson<CharacterLifeRecordSaveData>(
                    JsonUtility.ToJson(ownerLife));
            staffLife.characterId = CanonicalStaffWorkRoundTripId;
            life.characters.Add(staffLife);
            life.characters = life.characters
                .OrderBy(record => record?.characterId, StringComparer.Ordinal)
                .ToList();
            DungeonSaveSectionPayload.Write(
                captured,
                CharacterLifeSaveSection.Id,
                CharacterLifeWorldSaveData.CurrentVersion,
                DungeonSaveRestorePhase.Characters,
                life);

            ICharacterWorldSaveService directWorldSave =
                scope.Container.Resolve<ICharacterWorldSaveService>();
            IGridSystemProvider directGridProvider =
                scope.Container.Resolve<IGridSystemProvider>();
            if (!directGridProvider.TryGetGrid(out Grid directGrid))
            {
                message = "Legacy V18 input mutation fixture has no live grid.";
                return false;
            }
            string characterPayloadBeforePrepare = JsonUtility.ToJson(savedCharacters);
            CharacterWorldRestoreCandidate inputMutationCandidate =
                directWorldSave.PrepareRestoreCandidate(directGrid, savedCharacters);
            inputMutationCandidate.Discard();
            if (!string.Equals(
                    characterPayloadBeforePrepare,
                    JsonUtility.ToJson(savedCharacters),
                    StringComparison.Ordinal))
            {
                message = "Character-world candidate preparation mutated its legacy input DTO.";
                return false;
            }

            DungeonGameSaveData parsed = saveService.FromJson(saveService.ToJson(captured, prettyPrint: true));
            string parsedInputJson = saveService.ToJson(parsed, prettyPrint: false);
            DungeonGameSaveData incompatible = saveService.FromJson(saveService.ToJson(captured));
            incompatible.version = DungeonGameSaveData.CurrentVersion - 1;
            if (saveService.TryRestore(incompatible, out DungeonGameRestoreReport incompatibleReport)
                || !incompatibleReport.Errors.Any(error => string.Equals(
                    error,
                    DungeonSaveCompatibility.PreV24IncompatibilityReason,
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
            if (!string.Equals(
                    parsedInputJson,
                    saveService.ToJson(parsed, prettyPrint: false),
                    StringComparison.Ordinal))
            {
                message = "Full legacy V18 restore mutated the input save DTO graph.";
                return false;
            }
            if (!report.Warnings.Any(warning => warning.Contains(
                    "characters.world",
                    StringComparison.Ordinal)
                    && warning.Contains(
                        $"'{LegacyStaffWorkRoundTripId}' -> '{CanonicalStaffWorkRoundTripId}'",
                        StringComparison.Ordinal))
                || !report.Warnings.Any(warning => warning.Contains(
                    CharacterBodyHealthSaveSection.Id,
                    StringComparison.Ordinal)
                    && warning.Contains(
                        $"'{LegacyStaffWorkRoundTripId}' -> '{CanonicalStaffWorkRoundTripId}'",
                        StringComparison.Ordinal)))
            {
                message = "Legacy V18 restore did not report exact source-to-canonical mappings.";
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
                    LegacyStaffWorkRoundTripId,
                    out CharacterActor restoredStaff)
                || !worldSave.TryGetRestoredActor(
                    CanonicalStaffWorkRoundTripId,
                    out CharacterActor restoredStaffByCanonicalId)
                || !ReferenceEquals(restoredStaff, restoredStaffByCanonicalId))
            {
                message = "Restored legacy staff actor is not available through both legacy and canonical lookup.";
                return false;
            }

            if (!string.Equals(
                    restoredStaff.Identity?.PersistentId,
                    CanonicalStaffWorkRoundTripId,
                    StringComparison.Ordinal))
            {
                message = "Legacy V18 staff restore did not assign a canonical CharacterId to the actor.";
                return false;
            }

            DungeonCharacterWorldSaveData parsedCharactersAfterRestore =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                    parsed,
                    CharacterWorldSaveSection.Id);
            if (!parsedCharactersAfterRestore.actors.Any(actor => actor != null
                    && string.Equals(
                        actor.persistentId,
                        LegacyStaffWorkRoundTripId,
                        StringComparison.Ordinal)))
            {
                message = "Legacy V18 restore mutated its input save payload.";
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

            DungeonGameSaveData firstRecapture = saveService.Capture();
            DungeonCharacterSaveData recapturedStaff =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                        firstRecapture,
                        CharacterWorldSaveSection.Id)
                    .actors
                    .SingleOrDefault(actor => actor != null
                        && string.Equals(
                            actor.persistentId,
                            CanonicalStaffWorkRoundTripId,
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

            DungeonCharacterBodyHealthSaveData recapturedBodyHealth =
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterBodyHealthSaveData>(
                    firstRecapture,
                    CharacterBodyHealthSaveSection.Id);
            string firstCanonicalBodyHealthIds = CanonicalizeBodyHealthIds(
                recapturedBodyHealth);
            if (!string.Equals(
                    expectedCanonicalBodyHealthIds,
                    firstCanonicalBodyHealthIds,
                    StringComparison.Ordinal))
            {
                message = "Character body-health IDs were not exact after canonical restore: "
                    + $"expected={expectedCanonicalBodyHealthIds}; "
                    + $"actual={firstCanonicalBodyHealthIds}.";
                return false;
            }

            if (!HasExactTransientSkillIdentity(
                    ownerManager.CurrentOwnerActor,
                    CharacterId.Owner)
                || !HasExactTransientSkillIdentity(
                    restoredStaff,
                    (CharacterId)CanonicalStaffWorkRoundTripId))
            {
                message = "Restored owner/staff transient skill state retained a noncanonical CharacterId.";
                return false;
            }

            string firstIdentityProjection = CanonicalizeIdentityProjection(
                firstRecapture);
            DungeonGameSaveData secondRestoreInput = saveService.FromJson(
                saveService.ToJson(firstRecapture, prettyPrint: false));
            if (!saveService.TryRestore(
                    secondRestoreInput,
                    out DungeonGameRestoreReport secondRestoreReport))
            {
                message = "Second canonical character restore failed: "
                    + string.Join(" | ", secondRestoreReport.Errors);
                return false;
            }

            DungeonGameSaveData secondRecapture = saveService.Capture();
            string secondIdentityProjection = CanonicalizeIdentityProjection(
                secondRecapture);
            if (!string.Equals(
                    firstIdentityProjection,
                    secondIdentityProjection,
                    StringComparison.Ordinal))
            {
                message = "Repeated canonical character restore changed actor/body identity state: "
                    + $"first={firstIdentityProjection}; second={secondIdentityProjection}.";
                return false;
            }

            if (!ownerProvider.TryGetManager(out ownerManager)
                || ownerManager.CurrentOwnerActor == null
                || !worldSave.TryGetRestoredActor(
                    CanonicalStaffWorkRoundTripId,
                    out CharacterActor secondRestoredStaff)
                || !HasExactTransientSkillIdentity(
                    ownerManager.CurrentOwnerActor,
                    CharacterId.Owner)
                || !HasExactTransientSkillIdentity(
                    secondRestoredStaff,
                    (CharacterId)CanonicalStaffWorkRoundTripId))
            {
                message = "Repeated restore did not preserve canonical owner/staff transient identities.";
                return false;
            }

            message = $"CHARACTER_WORLD_V18_CONTRACTS_PASSED Lv.{restored.Level} XP={restored.CurrentExperience} active={restored.ActiveSkills.Count} passive={restored.PassiveSkills.Count} staffWorkRoundTrip=true legacyNormalized=true identityDoubleRestore=true transientIdentityCanonical=true inputUnchanged=true directRejected=true ownerlessRejected=true invalidCellRejected=true rollbackFreeLateFailure=true preV18Rejected=true warnings={report.Warnings.Count}";
            return true;
        }
        finally
        {
            DungeonGameSaveData pristineBaseline = saveService.FromJson(
                pristineBaselineJson);
            if (!saveService.TryRestore(
                    pristineBaseline,
                    out DungeonGameRestoreReport baselineReport))
            {
                throw new InvalidOperationException(
                    "Failed to restore the progression verification baseline: "
                    + string.Join(" | ", baselineReport.Errors));
            }

            DungeonGameSaveData restoredBaseline = saveService.Capture();
            string restoredBaselineCanonical = Canonicalize(restoredBaseline);
            if (!string.Equals(
                    pristineBaselineCanonical,
                    restoredBaselineCanonical,
                    StringComparison.Ordinal))
            {
                DungeonCharacterWorldSaveData expectedCharacters =
                    DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                        pristineBaseline,
                        CharacterWorldSaveSection.Id);
                DungeonCharacterWorldSaveData actualCharacters =
                    DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                        saveService.Capture(),
                        CharacterWorldSaveSection.Id);
                ICharacterLifetimeQuery lifetime =
                    scope.Container.Resolve<ICharacterLifetimeQuery>();
                string liveActors = string.Join(
                    ",",
                    lifetime.AllCharacters.Select(actor => actor == null
                        ? "<null>"
                        : $"{actor.Identity?.PersistentId ?? "<none>"}"
                            + $"[active={actor.gameObject.activeInHierarchy};"
                            + $"type={actor.Identity?.CharacterType};"
                            + $"work={actor.TryGetAbility(out AbilityWork _)};"
                            + $"state={actor.CurrentLifecycleState};dead={actor.IsDead}]"));
                throw new InvalidOperationException(
                    "Progression verification baseline restore returned success "
                    + "without restoring the exact captured aggregate state. "
                    + "expectedActors="
                    + string.Join(",", expectedCharacters.actors.Select(actor => actor?.persistentId))
                    + "; actualActors="
                    + string.Join(",", actualCharacters.actors.Select(actor => actor?.persistentId))
                    + "; lifetime=" + liveActors);
            }
        }
    }

    private static string CanonicalizeBodyHealthIds(
        DungeonCharacterBodyHealthSaveData bodyHealth,
        string legacyId = null,
        string canonicalId = null)
    {
        return string.Join(",", (bodyHealth?.characters
                ?? new List<CharacterBodyHealthState>())
            .Where(state => state != null)
            .Select(state => string.Equals(
                    state.characterId,
                    legacyId,
                    StringComparison.Ordinal)
                ? canonicalId
                : state.characterId)
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string CanonicalizeIdentityProjection(DungeonGameSaveData save)
    {
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                save,
                CharacterWorldSaveSection.Id);
        DungeonCharacterBodyHealthSaveData bodyHealth =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterBodyHealthSaveData>(
                save,
                CharacterBodyHealthSaveSection.Id);
        string actorIds = string.Join(",", (characters.actors
                ?? new List<DungeonCharacterSaveData>())
            .Where(actor => actor != null)
            .Select(actor => actor.persistentId)
            .OrderBy(value => value, StringComparer.Ordinal));
        return "actors=" + actorIds + ";body=" + CanonicalizeBodyHealthIds(bodyHealth);
    }

    private static bool HasExactTransientSkillIdentity(
        CharacterActor actor,
        CharacterId expectedId)
    {
        CharacterSkillTransientState transient = actor != null
            ? actor.GetComponent<CharacterSkillTransientState>()
            : null;
        FieldInfo characterIdField = typeof(CharacterSkillTransientState)
            .GetField("characterId", BindingFlags.Instance | BindingFlags.NonPublic);
        return transient != null
            && transient.IsConfigured
            && characterIdField?.GetValue(transient) is CharacterId actualId
            && actualId.Equals(expectedId);
    }

    private static string Canonicalize(DungeonGameSaveData save)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(save?.version ?? 0).Append('\n');
        AppendCanonicalField(builder, save?.sceneName);
        foreach (DungeonSaveSectionEnvelope section in
                 save?.sections?
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.sectionId, StringComparer.Ordinal)
                 ?? Enumerable.Empty<DungeonSaveSectionEnvelope>())
        {
            AppendCanonicalField(builder, section.sectionId);
            builder.Append(section.sectionVersion).Append('\n');
            builder.Append((int)section.restorePhase).Append('\n');
            builder.Append(section.optional ? '1' : '0').Append('\n');
            AppendCanonicalField(builder, section.payloadJson);
        }

        return builder.ToString();
    }

    private static void AppendCanonicalField(StringBuilder builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length).Append(':').Append(safe).Append('\n');
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

        if (!RejectsLegacyCanonicalCollision(
                saveService,
                ownerProvider,
                liveOwner,
                baseline,
                candidates,
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
            CharacterWorldSaveSection.CurrentVersion,
            DungeonSaveRestorePhase.Characters,
            ownerlessCharacters);
        bool ownerlessRestored = saveService.TryRestore(
            ownerless,
            out DungeonGameRestoreReport ownerlessReport);
        bool identifiedOwnerless = ownerlessReport.Errors.Any(error =>
            error.Contains("owner", StringComparison.OrdinalIgnoreCase)
            && (error.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || error.Contains(
                    "exactly one owner actor",
                    StringComparison.Ordinal)));
        bool ownerlessOwnerUnchanged = OwnerIsUnchanged(
            ownerProvider,
            liveOwner);
        bool ownerlessStagedCharacters = candidates.TryGetCharacters(out _);
        if (ownerlessRestored
            || !identifiedOwnerless
            || !ownerlessOwnerUnchanged
            || ownerlessStagedCharacters)
        {
            failure =
                "Ownerless character payload did not fail atomically. "
                + $"restored={ownerlessRestored}, identifiedOwnerless={identifiedOwnerless}, "
                + $"ownerUnchanged={ownerlessOwnerUnchanged}, "
                + $"stagedCharacters={ownerlessStagedCharacters}, "
                + $"errors={string.Join(" | ", ownerlessReport.Errors)}";
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
            CharacterWorldSaveSection.CurrentVersion,
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
            CharacterWorldSaveSection.CurrentVersion,
            DungeonSaveRestorePhase.Characters,
            characters);

        bool restored = saveService.TryRestore(
            invalidSave,
            out DungeonGameRestoreReport report);
        bool identifiesInvalidValue = report.Errors.Any(error =>
            error.Contains(invalidId, StringComparison.Ordinal));
        bool ownerUnchanged = OwnerIsUnchanged(ownerProvider, liveOwner);
        bool stagedCharacters = candidates.TryGetCharacters(out _);
        if (restored
            || !identifiesInvalidValue
            || !ownerUnchanged
            || stagedCharacters)
        {
            failure =
                $"Character world did not atomically reject the {label} CharacterId '{invalidId}'. "
                + $"restored={restored}, identifiedInvalidValue={identifiesInvalidValue}, "
                + $"ownerUnchanged={ownerUnchanged}, stagedCharacters={stagedCharacters}, "
                + $"errors={string.Join(" | ", report.Errors)}";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool RejectsLegacyCanonicalCollision(
        IDungeonGameSaveService saveService,
        IOwnerRunManagerProvider ownerProvider,
        CharacterActor liveOwner,
        DungeonGameSaveData baseline,
        IRestoreWorldCandidateQuery candidates,
        out string failure)
    {
        const string legacyId = "staff:13579:01";
        const string canonicalId = "character:staff:13579:01";
        DungeonGameSaveData collision = saveService.FromJson(
            saveService.ToJson(baseline));
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                collision,
                CharacterWorldSaveSection.Id);
        DungeonCharacterSaveData owner = characters.actors
            .Single(actor => actor != null && actor.isOwner);
        DungeonCharacterSaveData legacy = JsonUtility.FromJson<DungeonCharacterSaveData>(
            JsonUtility.ToJson(owner));
        legacy.isOwner = false;
        legacy.persistentId = legacyId;
        DungeonCharacterSaveData canonical = JsonUtility.FromJson<DungeonCharacterSaveData>(
            JsonUtility.ToJson(owner));
        canonical.isOwner = false;
        canonical.persistentId = canonicalId;
        characters.actors.Add(legacy);
        characters.actors.Add(canonical);
        DungeonSaveSectionPayload.Write(
            collision,
            CharacterWorldSaveSection.Id,
            CharacterWorldSaveSection.CurrentVersion,
            DungeonSaveRestorePhase.Characters,
            characters);

        bool restored = saveService.TryRestore(
            collision,
            out DungeonGameRestoreReport report);
        bool identifiedCollision = report.Errors.Any(error =>
            error.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            && error.Contains(canonicalId, StringComparison.Ordinal));
        bool ownerUnchanged = OwnerIsUnchanged(ownerProvider, liveOwner);
        bool stagedCharacters = candidates.TryGetCharacters(out _);
        if (restored
            || !identifiedCollision
            || !ownerUnchanged
            || stagedCharacters)
        {
            failure =
                "Legacy/canonical CharacterId collision did not fail atomically. "
                + $"restored={restored}, identifiedCollision={identifiedCollision}, "
                + $"ownerUnchanged={ownerUnchanged}, stagedCharacters={stagedCharacters}, "
                + $"errors={string.Join(" | ", report.Errors)}";
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

        MarkerDependencySection sessionDependency =
            new MarkerDependencySection(FoundationSessionSaveSection.Id);
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
                sessionDependency,
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
            CreateEnvelope(sessionDependency),
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
