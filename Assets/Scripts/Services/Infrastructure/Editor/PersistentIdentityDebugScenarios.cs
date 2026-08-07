#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class PersistentIdentityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Infrastructure/Run V18 Persistent Identity Contracts")]
    public static void RunAll()
    {
        IPersistentIdGenerator generator = new GuidPersistentIdGenerator();
        ItemStackId stackA = generator.NewItemStackId();
        ItemStackId stackB = generator.NewItemStackId();
        ItemInstanceId item = generator.NewItemInstanceId();
        CharacterId character = generator.NewCharacterId();
        BuildingInstanceId buildingId = generator.NewBuildingInstanceId();

        Require(stackA.IsValid && stackB.IsValid && !stackA.Equals(stackB),
            "Item stack IDs are invalid or duplicated.");
        Require(item.IsValid && character.IsValid && buildingId.IsValid,
            "A typed persistent ID was invalid.");
        Require(CharacterId.Owner.IsValid
                && ((CharacterId)"character:fixture").IsValid
                && !((CharacterId)"Named Hero").IsValid
                && !((CharacterId)"building:fixture").IsValid,
            "CharacterId accepted a name-like or foreign typed persistent ID.");

        CharacterId invasionCharacter =
            CharacterId.FromStableSuffix("invasion:raid-a:intruder:1");
        CharacterId reinforcementCharacter =
            CharacterId.FromStableSuffix("faction-route:4:ally:2");
        CharacterId prisonerCharacter =
            CharacterId.FromStableSuffix("return:7:prisoner:3");
        CharacterId incidentCharacter =
            CharacterId.FromStableSuffix("incident:Thief:9:actor");
        Require(
            invasionCharacter.Value == "character:invasion:raid-a:intruder:1"
            && reinforcementCharacter.Value == "character:faction-route:4:ally:2"
            && prisonerCharacter.Value == "character:return:7:prisoner:3"
            && incidentCharacter.Value == "character:incident:Thief:9:actor",
            "Runtime-created character IDs did not preserve their deterministic source suffixes.");
        RequireThrows<ArgumentException>(
            () => CharacterId.FromStableSuffix(string.Empty),
            "An empty runtime character suffix was accepted.");
        RequireThrows<ArgumentException>(
            () => CharacterId.FromStableSuffix("character:already-scoped"),
            "An already-scoped CharacterId was accepted as a runtime suffix.");

        Require(
            CharacterId.TryCanonicalizeV18Restore(
                "world:-73:000042",
                out CharacterId legacyWorld,
                out bool worldWasLegacy)
            && worldWasLegacy
            && legacyWorld.Value == "character:world:-73:000042",
            "The legacy V18 world CharacterId was not normalized deterministically.");
        Require(
            CharacterId.TryCanonicalizeV18Restore(
                "staff:24680:01",
                out CharacterId legacyStaff,
                out bool staffWasLegacy)
            && staffWasLegacy
            && legacyStaff.Value == "character:staff:24680:01",
            "The legacy V18 staff CharacterId was not normalized deterministically.");
        Require(
            CharacterId.TryCanonicalizeV18Restore(
                "character:staff:24680:01",
                out CharacterId canonicalStaff,
                out bool canonicalWasLegacy)
            && !canonicalWasLegacy
            && canonicalStaff.Value == "character:staff:24680:01",
            "A canonical CharacterId changed during V18 restore resolution.");
        RequireLegacyOperationalId(
            "invasion:0123456789abcdef0123456789abcdef");
        RequireLegacyOperationalId("faction-route:4:ally:2");
        RequireLegacyOperationalId("return:7:prisoner:3");
        RequireLegacyOperationalId("incident:Thief:9:actor");
        Require(
            !CharacterId.TryCanonicalizeV18Restore(
                "staff:not-a-seed:01",
                out _,
                out _)
            && !CharacterId.TryCanonicalizeV18Restore(
                "world:73:42",
                out _,
                out _)
            && !CharacterId.TryCanonicalizeV18Restore(
                "invasion:raid-a:intruder:1",
                out _,
                out _)
            && !CharacterId.TryCanonicalizeV18Restore(
                "faction-route:04:ally:2",
                out _,
                out _)
            && !CharacterId.TryCanonicalizeV18Restore(
                "return:7:prisoner:03",
                out _,
                out _)
            && !CharacterId.TryCanonicalizeV18Restore(
                "incident:None:9:actor",
                out _,
                out _),
            "V18 restore compatibility accepted an ID that was not emitted by a legacy generator.");

        ValidateTypedRestoreNormalization();
        ValidateStrictSaveSectionRestorePipeline();
        ValidateWhitespaceRestoreRejection();

        GameObject characterObject = new("V18 Character Identity Contract");
        GameObject buildingObject = new("V18 Building Identity Contract");
        try
        {
            CharacterIdentity identity = characterObject.AddComponent<CharacterIdentity>();
            identity.SetPersistentId(character);
            Require(identity.TypedPersistentId.Equals(character),
                "CharacterIdentity did not retain its typed ID.");

            Facility building = buildingObject.AddComponent<Facility>();
            building.ConstructPersistentIdentity(generator);
            BuildingInstanceId assigned = building.RequirePersistentInstanceId();
            Require(assigned.IsValid,
                "BuildableObject did not receive a persistent building ID.");

            ModularFacilityBuildingSaveData save = new()
            {
                persistentInstanceId = assigned.Value,
                buildingId = 42
            };
            ModularFacilityBuildingSaveData restored =
                JsonUtility.FromJson<ModularFacilityBuildingSaveData>(
                    JsonUtility.ToJson(save));
            Require(restored != null
                    && ((BuildingInstanceId)restored.persistentInstanceId).Equals(assigned),
                "Building persistent ID did not survive DTO serialization.");

            string warehouseDestination =
                WarehouseStorageIdentity.RequireDestinationId(building);
            Require(string.Equals(
                    warehouseDestination,
                    WorldItemStackRuntime.WarehouseStorageDestinationPrefix + assigned.Value,
                    StringComparison.Ordinal),
                "Warehouse storage identity did not use the building instance ID.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(characterObject);
            UnityEngine.Object.DestroyImmediate(buildingObject);
        }

        Debug.Log(
            $"V18 PERSISTENT ID PASS: stack={stackA.Value}, item={item.Value}, "
            + $"character={character.Value}, building={buildingId.Value}");
    }

    private static void ValidateWhitespaceRestoreRejection()
    {
        const string canonicalId = "character:staff:24680:01";
        const string spacedCanonicalId = " character:staff:24680:01 ";
        Type resolver = typeof(CharacterWorldSaveService).Assembly.GetType(
            "CharacterV18RestoreIdentityResolver",
            throwOnError: true);

        MethodInfo tryResolve = resolver.GetMethod(
            "TryResolve",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(resolver.FullName, "TryResolve");
        object[] resolveArguments =
        {
            spacedCanonicalId,
            true,
            default(CharacterId),
            false
        };
        Require(
            !(bool)tryResolve.Invoke(null, resolveArguments),
            "The actual V18 restore resolver silently trimmed a CharacterId.");

        MethodInfo validateUniqueIds = resolver.GetMethod(
            "ValidateUniqueIds",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(
                resolver.FullName,
                "ValidateUniqueIds");
        DungeonGameRestoreReport resolverReport = new();
        validateUniqueIds.Invoke(
            null,
            new object[]
            {
                new[] { spacedCanonicalId },
                "whitespace regression character",
                resolverReport,
                true
            });
        Require(
            !resolverReport.Success
            && ContainsError(resolverReport, "invalid persistent ID"),
            "ValidateUniqueIds accepted a whitespace-padded CharacterId.");

        MethodInfo ensureUniqueIds = resolver.GetMethod(
            "EnsureUniqueIds",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(
                resolver.FullName,
                "EnsureUniqueIds");
        bool ensureRejectedWhitespace = false;
        try
        {
            ensureUniqueIds.Invoke(
                null,
                new object[]
                {
                    new[] { spacedCanonicalId },
                    "whitespace regression",
                    false
                });
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is InvalidOperationException)
        {
            ensureRejectedWhitespace = true;
        }
        Require(
            ensureRejectedWhitespace,
            "EnsureUniqueIds accepted a whitespace-padded CharacterId.");

        GameObject actorObject = new("V18 whitespace resolver actor");
        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            Dictionary<string, CharacterActor> actors =
                new(StringComparer.Ordinal) { [canonicalId] = actor };
            Dictionary<string, string> aliases = new(StringComparer.Ordinal);
            MethodInfo tryGetActor = resolver.GetMethod(
                "TryGetActor",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(
                    resolver.FullName,
                    "TryGetActor");
            object[] exactLookup = { actors, aliases, canonicalId, null };
            object[] spacedLookup =
                { actors, aliases, spacedCanonicalId, null };
            Require(
                (bool)tryGetActor.Invoke(null, exactLookup)
                && ReferenceEquals(exactLookup[3], actor),
                "TryGetActor rejected an exact canonical CharacterId.");
            Require(
                !(bool)tryGetActor.Invoke(null, spacedLookup)
                && spacedLookup[3] == null,
                "TryGetActor silently trimmed a whitespace-padded CharacterId.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }

        DungeonCharacterWorldSaveData characters = new()
        {
            actors = new List<DungeonCharacterSaveData>
            {
                new() { persistentId = spacedCanonicalId }
            },
            populationProfiles = new List<WorldCharacterProfile>
            {
                new() { persistentId = spacedCanonicalId }
            }
        };
        DungeonGameSaveData save = new();
        DungeonSaveSectionPayload.Write(
            save,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            characters);
        DungeonGameRestoreReport preflightReport = new();
        new DungeonAggregateReferencePreflight(
                new ResourceItemDefinitionCatalog(Array.Empty<ItemDefinitionSO>()),
                 new EmptyBuildingDefinitionLookup(),
                 new EmptyCombatEquipmentCatalog(),
                 new EmptyResourceEconomyContentCatalog(),
                 new EmptyCharacterLifeDefinitionCatalog(),
                 new EmptyDiseaseDefinitionCatalog())
            .Validate(save, preflightReport);
        Require(
            !preflightReport.Success
            && ContainsError(preflightReport, "exact canonical persistent ID"),
            "Aggregate reference preflight silently trimmed an actor or profile CharacterId.");

        CharacterCombatCommandSaveData combatCommands = new()
        {
            stanceCharacterIds = new List<string> { spacedCanonicalId }
        };
        DungeonGameRestoreReport combatCommandReport = new();
        CharacterCombatCommandSaveValidation.Validate(
            combatCommands,
            combatCommandReport);
        Require(
            !combatCommandReport.Success
            && ContainsError(combatCommandReport, "Combat stance"),
            "Combat-command validation accepted a whitespace-padded CharacterId.");

        CharacterCombatCommandSaveData combatWeapon = new()
        {
            commandSequence = 1,
            stanceCharacterIds = new List<string> { canonicalId },
            revisions = new List<CharacterCombatCommandRevisionSaveData>
            {
                new() { actorId = canonicalId, revision = 1 }
            },
            commands = new List<CharacterCombatCommand>
            {
                new()
                {
                    commandId = "combat-command:1",
                    actorId = canonicalId,
                    type = CombatCommandType.Move,
                    state = CharacterCombatCommandState.Queued,
                    hasTargetCell = true,
                    weaponInstanceId = " item-instance:fixture ",
                    revision = 1
                }
            }
        };
        DungeonGameRestoreReport combatWeaponReport = new();
        CharacterCombatCommandSaveValidation.Validate(
            combatWeapon,
            combatWeaponReport);
        Require(
            !combatWeaponReport.Success
            && ContainsError(combatWeaponReport, "weapon instance ID"),
            "Combat-command validation accepted a whitespace-padded ItemInstanceId.");

        DefenseTacticalCoordinatorSaveData defense = new()
        {
            sequence = 1,
            reservations = new List<CombatPositionReservation>
            {
                new()
                {
                    reservationId = "combat-position:1",
                    actorId = spacedCanonicalId,
                    targetId = string.Empty,
                    kind = CombatPositionReservationKind.Move
                }
            }
        };
        DungeonGameRestoreReport defenseReport = new();
        Type defenseValidator = typeof(DefenseTacticalCoordinator).Assembly.GetType(
            "DefenseTacticalSaveValidation",
            throwOnError: true);
        MethodInfo validateDefense = defenseValidator.GetMethod(
            "Validate",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(
                defenseValidator.FullName,
                "Validate");
        validateDefense.Invoke(
            null,
            new object[] { defense, defenseReport, null });
        Require(
            !defenseReport.Success
            && ContainsError(defenseReport, "invalid or duplicated"),
            "Defense-tactical validation accepted a whitespace-padded CharacterId.");

        DefenseTacticalCoordinator defenseRuntime = new(
            new OpenDefenseTacticalWorldQuery(),
            new DungeonRuntimeAggregateRootStore());
        string defenseCaptureBefore = JsonUtility.ToJson(
            defenseRuntime.Capture());
        bool reservedWhitespace = defenseRuntime.TryReserve(
            spacedCanonicalId,
            string.Empty,
            Vector2Int.zero,
            CombatPositionReservationKind.Move,
            0f,
            out _);
        string defenseCaptureAfter = JsonUtility.ToJson(
            defenseRuntime.Capture());
        Require(
            !reservedWhitespace
            && defenseRuntime.Reservations.Count == 0
            && string.Equals(
                defenseCaptureBefore,
                defenseCaptureAfter,
                StringComparison.Ordinal),
            "Defense-tactical command accepted whitespace or mutated its saved state.");
    }

    private static bool ContainsError(
        DungeonGameRestoreReport report,
        string fragment)
    {
        foreach (string error in report.Errors)
        {
            if (error?.IndexOf(fragment, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireLegacyOperationalId(string legacy)
    {
        Require(
            CharacterId.TryCanonicalizeV18Restore(
                legacy,
                out CharacterId canonical,
                out bool wasLegacy)
            && wasLegacy
            && canonical.Value == "character:" + legacy,
            $"The legacy V18 operational CharacterId '{legacy}' was not normalized deterministically.");
    }

    private static void ValidateTypedRestoreNormalization()
    {
        const string legacyStaff = "staff:24680:01";
        const string legacyWorld = "world:-73:000042";
        const string canonicalStaff = "character:staff:24680:01";
        const string canonicalWorld = "character:world:-73:000042";
        const string invasionRuntimeKey =
            "invasion:0123456789abcdef0123456789abcdef";
        const string wildlifeRuntimeKey = "wildlife:fixture-wolf";
        const string facilityRuntimeKey = "building-instance:fixture-forge";
        List<string> visitedPaths = new();

        string Rewrite(string value, string path)
        {
            visitedPaths.Add(path);
            return CharacterId.TryCanonicalizeV18Restore(
                    value,
                    out CharacterId canonical,
                    out bool wasLegacy)
                && wasLegacy
                    ? canonical.Value
                    : value ?? string.Empty;
        }

        CharacterCombatCommandSaveData commands = new()
        {
            stanceCharacterIds = new List<string> { legacyStaff },
            commands = new List<CharacterCombatCommand>
            {
                new()
                {
                    actorId = legacyWorld,
                    targetId = invasionRuntimeKey,
                    type = CombatCommandType.Attack
                },
                new()
                {
                    actorId = legacyStaff,
                    targetId = legacyWorld,
                    type = CombatCommandType.Rescue
                }
            },
            revisions = new List<CharacterCombatCommandRevisionSaveData>
            {
                new() { actorId = legacyStaff }
            }
        };
        DefenseTacticalCoordinatorSaveData defense = new()
        {
            reservations = new List<CombatPositionReservation>
            {
                new()
                {
                    actorId = legacyWorld,
                    targetId = wildlifeRuntimeKey
                }
            }
        };
        DungeonSurgerySaveData surgery = new()
        {
            orders = new List<SurgeryOrder>
            {
                new()
                {
                    preferredDoctorId = legacyStaff,
                    doctorId = legacyWorld,
                    patientTransporterId = legacyStaff,
                    subject = new SurgicalSubjectRef
                    {
                        kind = SurgicalSubjectKind.Character,
                        subjectId = legacyWorld
                    }
                },
                new()
                {
                    subject = new SurgicalSubjectRef
                    {
                        kind = SurgicalSubjectKind.Wildlife,
                        subjectId = legacyStaff
                    }
                }
            },
            parts = new List<SurgicalPartInstance>
            {
                new()
                {
                    donorId = wildlifeRuntimeKey,
                    installedSubjectId = wildlifeRuntimeKey
                }
            },
            policies = new List<SurgerySubjectPolicyState>
            {
                new() { subjectId = wildlifeRuntimeKey }
            }
        };
        DungeonExteriorActivitySaveData exterior = new()
        {
            incidentStates = new List<ExteriorIncidentRuntimeState>
            {
                new() { actorIds = new List<string> { legacyStaff, legacyWorld } }
            }
        };
        DungeonFactionSaveData factions = new()
        {
            routes = new List<FactionRouteState>
            {
                new() { reinforcementActorIds = new List<string> { legacyStaff } }
            }
        };
        DungeonRegularCustomerSaveData customers = new()
        {
            records = new List<DungeonRegularCustomerRecordSaveData>
            {
                new() { customerId = legacyWorld }
            }
        };
        DungeonInvasionSaveData invasion = new()
        {
            activeIntruders = new List<DungeonInvasionIntruderSaveData>
            {
                new() { runtimeId = invasionRuntimeKey }
            },
            responsePolicies = new DefenseResponsePolicySaveSnapshot
            {
                assignments = new List<DefensePolicyAssignmentSaveData>
                {
                    new() { characterId = legacyStaff }
                }
            },
            engagements = new DefenseEngagementSaveSnapshot
            {
                engagements = new List<DefenseEngagementSaveData>
                {
                    new()
                    {
                        intruderId = invasionRuntimeKey,
                        leadGuardId = legacyWorld,
                        reserveGuardId = legacyStaff
                    }
                }
            }
        };
        DungeonOffenseAggregateSaveData offense = new()
        {
            expedition = new DungeonOffenseSaveData
            {
                activeExpeditions = new List<DungeonOffenseExpeditionRunSaveData>
                {
                    new()
                    {
                        memberPersistentIds = new List<string> { legacyStaff },
                        protectedRescueMemberPersistentIds =
                            new List<string> { legacyWorld },
                        memberStates =
                            new List<DungeonOffenseExpeditionMemberStateSaveData>
                            {
                                new() { persistentId = legacyStaff }
                            }
                    }
                }
            },
            world = new OffenseWorldSaveData
            {
                fieldStabilizations = new List<FieldStabilizationState>
                {
                    new() { characterId = legacyWorld }
                }
            },
            returnArrivals = new DungeonOffenseReturnArrivalSaveData
            {
                arrivals = new List<OffenseReturnArrivalState>
                {
                    new()
                    {
                        kind = OffenseReturnArrivalKind.SpecialWildlife,
                        materializedIds = new List<string> { wildlifeRuntimeKey },
                        escapedIds = new List<string> { wildlifeRuntimeKey }
                    }
                }
            }
        };
        TreasuryEconomySaveData treasury = new()
        {
            transactionLedger = new EconomyTransactionLedgerSaveData
            {
                records = new List<EconomyTransactionRecord>
                {
                    new()
                    {
                        sourceId = facilityRuntimeKey,
                        targetId = wildlifeRuntimeKey
                    }
                }
            },
            employment = new EmploymentContractSaveData
            {
                wageStates = new List<EmployeeWageState>
                {
                    new() { characterId = legacyStaff }
                }
            }
        };

        V18TypedCharacterReferenceRestoreNormalizer.Normalize(commands, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(defense, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(surgery, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(exterior, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(customers, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(factions, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(invasion, Rewrite);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(offense, Rewrite);
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            treasury,
            Rewrite);

        Require(
            commands.stanceCharacterIds[0] == canonicalStaff
            && commands.commands[0].actorId == canonicalWorld
            && commands.commands[0].targetId == "character:" + invasionRuntimeKey
            && commands.commands[1].actorId == canonicalStaff
            && commands.commands[1].targetId == canonicalWorld
            && commands.revisions[0].actorId == canonicalStaff,
            "Typed V18 combat-command references were not normalized exactly.");
        Require(
            defense.reservations[0].actorId == canonicalWorld
            && defense.reservations[0].targetId == wildlifeRuntimeKey,
            "Typed V18 defense actor or runtime target was normalized incorrectly.");
        Require(
            surgery.orders[0].preferredDoctorId == canonicalStaff
            && surgery.orders[0].doctorId == canonicalWorld
            && surgery.orders[0].patientTransporterId == canonicalStaff
            && surgery.orders[0].subject.subjectId == canonicalWorld
            && surgery.orders[1].subject.subjectId == legacyStaff
            && surgery.parts[0].donorId == wildlifeRuntimeKey
            && surgery.parts[0].installedSubjectId == wildlifeRuntimeKey
            && surgery.policies[0].subjectId == wildlifeRuntimeKey,
            "Typed V18 surgery references were normalized incompletely or by guessing subject kind.");
        Require(
            exterior.incidentStates[0].actorIds[0] == canonicalStaff
            && exterior.incidentStates[0].actorIds[1] == canonicalWorld
            && customers.records[0].customerId == canonicalWorld
            && factions.routes[0].reinforcementActorIds[0] == canonicalStaff,
            "Typed V18 exterior, customer, or faction actor references were not normalized.");
        Require(
            invasion.activeIntruders[0].runtimeId == invasionRuntimeKey
            && invasion.engagements.engagements[0].intruderId == invasionRuntimeKey
            && invasion.responsePolicies.assignments[0].characterId == canonicalStaff
            && invasion.engagements.engagements[0].leadGuardId == canonicalWorld
            && invasion.engagements.engagements[0].reserveGuardId == canonicalStaff,
            "V18 invasion runtime keys changed or actor references were not normalized.");
        Require(
            offense.expedition.activeExpeditions[0].memberPersistentIds[0]
                == canonicalStaff
            && offense.expedition.activeExpeditions[0]
                .protectedRescueMemberPersistentIds[0] == canonicalWorld
            && offense.expedition.activeExpeditions[0].memberStates[0].persistentId
                == canonicalStaff
            && offense.world.fieldStabilizations[0].characterId == canonicalWorld
            && offense.returnArrivals.arrivals[0].materializedIds[0]
                == wildlifeRuntimeKey
            && offense.returnArrivals.arrivals[0].escapedIds[0]
                == wildlifeRuntimeKey,
            "Typed V18 offense references were not normalized.");
        Require(
            treasury.transactionLedger.records[0].sourceId == facilityRuntimeKey
            && treasury.transactionLedger.records[0].targetId == wildlifeRuntimeKey
            && treasury.employment.wageStates[0].characterId == canonicalStaff,
            "Treasury runtime keys changed or employment CharacterId was not normalized.");
        Require(
            visitedPaths.Contains("commands[0].actorId")
            && visitedPaths.Contains("orders[0].patientTransporterId")
            && visitedPaths.Contains("incidentStates[0].actorIds[1]")
            && visitedPaths.Contains("commands[1].targetId")
            && visitedPaths.Contains("commands[0].targetId")
            && !visitedPaths.Contains("reservations[0].targetId")
            && !visitedPaths.Contains("activeIntruders[0].runtimeId")
            && !visitedPaths.Contains("engagements.engagements[0].intruderId"),
            "Typed V18 normalization did not expose stable diagnostic paths.");

        Require(
            V18TypedCharacterReferenceRestoreNormalizer.RewriteLegacyReference(
                null,
                report: null,
                sectionId: "fixture.identity",
                path: "optionalCharacterId") == null
            && V18TypedCharacterReferenceRestoreNormalizer.RewriteLegacyReference(
                string.Empty,
                report: null,
                sectionId: "fixture.identity",
                path: "optionalCharacterId") == string.Empty,
            "Optional null or empty CharacterIds were not preserved.");
        RequireThrows<InvalidOperationException>(
            () => V18TypedCharacterReferenceRestoreNormalizer
                .RewriteLegacyReference(
                    " character:staff:24680:01",
                    report: null,
                    sectionId: "fixture.identity",
                    path: "requiredCharacterId"),
            "The common V18 normalizer accepted a whitespace CharacterId.");
        RequireThrows<InvalidOperationException>(
            () => V18TypedCharacterReferenceRestoreNormalizer
                .RewriteLegacyReference(
                    "character:",
                    report: null,
                    sectionId: "fixture.identity",
                    path: "requiredCharacterId"),
            "The common V18 normalizer accepted a malformed CharacterId.");
        RequireThrows<InvalidOperationException>(
            () => V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
                new DefenseTacticalCoordinatorSaveData
                {
                    reservations = new List<CombatPositionReservation>
                    {
                        new()
                        {
                            actorId = canonicalStaff,
                            targetId = " wildlife:fixture-wolf "
                        }
                    }
                },
                Rewrite),
            "A whitespace-padded union runtime ID passed normalization.");
    }

    private static void ValidateStrictSaveSectionRestorePipeline()
    {
        IdentityRestorePipelineFixtureSection section = new();
        IdentityRestorePipelineFixturePayload legacyPayload = new()
        {
            requiredCharacterId = "staff:24680:01",
            optionalCharacterId = string.Empty
        };
        DungeonGameRestoreReport report = new();
        IDungeonSaveRestoreStage stage = section.StageRestore(
            JsonUtility.ToJson(legacyPayload),
            section.SectionVersion,
            report);

        Require(
            section.PublishedCharacterId == null
            && report.Warnings.Count == 1,
            "Strict restore staging published early or omitted its legacy warning.");
        stage.Commit(report);
        Require(
            section.PublishedCharacterId == "character:staff:24680:01"
            && section.PublishedOptionalCharacterId == string.Empty,
            "Strict Parse-Normalize-Build-Stage restore did not publish canonical state.");

        RequireStrictPipelineRejects(
            section,
            " character:staff:24680:01",
            "Strict restore staging accepted a whitespace CharacterId.");
        RequireStrictPipelineRejects(
            section,
            "character:",
            "Strict restore staging accepted a malformed CharacterId.");
    }

    private static void RequireStrictPipelineRejects(
        IdentityRestorePipelineFixtureSection section,
        string characterId,
        string message)
    {
        IdentityRestorePipelineFixturePayload payload = new()
        {
            requiredCharacterId = characterId,
            optionalCharacterId = string.Empty
        };
        RequireThrows<InvalidOperationException>(
            () => section.StageRestore(
                JsonUtility.ToJson(payload),
                section.SectionVersion,
                new DungeonGameRestoreReport()),
            message);
    }

    [Serializable]
    private sealed class IdentityRestorePipelineFixturePayload
    {
        public string requiredCharacterId;
        public string optionalCharacterId;
    }

    private sealed class IdentityRestorePipelineFixtureCandidate
    {
        public IdentityRestorePipelineFixtureCandidate(
            string requiredCharacterId,
            string optionalCharacterId)
        {
            RequiredCharacterId = requiredCharacterId;
            OptionalCharacterId = optionalCharacterId;
        }

        public string RequiredCharacterId { get; }
        public string OptionalCharacterId { get; }
    }

    private sealed class IdentityRestorePipelineFixtureSection :
        DungeonStrictJsonSaveSection<
            IdentityRestorePipelineFixturePayload,
            IdentityRestorePipelineFixtureCandidate>
    {
        public string PublishedCharacterId { get; private set; }
        public string PublishedOptionalCharacterId { get; private set; }

        public override string SectionId => "fixture.identity";
        public override int SectionVersion => 1;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;

        protected override IdentityRestorePipelineFixturePayload CapturePayload() =>
            new();

        protected override void NormalizeRestorePayload(
            IdentityRestorePipelineFixturePayload payload,
            DungeonGameRestoreReport report)
        {
            payload.requiredCharacterId = NormalizeV18CharacterReference(
                payload.requiredCharacterId,
                report,
                "requiredCharacterId");
            payload.optionalCharacterId = NormalizeV18CharacterReference(
                payload.optionalCharacterId,
                report,
                "optionalCharacterId");
        }

        protected override IdentityRestorePipelineFixtureCandidate
            BuildRestoreCandidate(IdentityRestorePipelineFixturePayload payload) =>
            new(payload.requiredCharacterId, payload.optionalCharacterId);

        protected override void PublishRestoreCandidate(
            IdentityRestorePipelineFixtureCandidate candidate)
        {
            PublishedCharacterId = candidate.RequiredCharacterId;
            PublishedOptionalCharacterId = candidate.OptionalCharacterId;
        }
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class EmptyBuildingDefinitionLookup : IBuildingDefinitionLookup
    {
        public BuildingSO GetBuilding(int id) =>
            throw new KeyNotFoundException($"No building definition {id}.");
    }

    private sealed class OpenDefenseTacticalWorldQuery :
        IDefenseTacticalWorldQuery
    {
        public bool HasRestoreGrid => true;

        public bool IsOperationalCellWalkable(Vector2Int cell) => true;

        public bool IsRestoreCellWalkable(Vector2Int cell) => true;

        public IReadOnlyList<DefenseTacticalActorSnapshot> CaptureActors() =>
            Array.Empty<DefenseTacticalActorSnapshot>();

        public IReadOnlyCollection<string> CaptureTargetIds() =>
            Array.Empty<string>();
    }

    private sealed class EmptyCombatEquipmentCatalog : ICombatEquipmentCatalog
    {
        public IReadOnlyList<CombatEquipmentDefinitionSO> All =>
            Array.Empty<CombatEquipmentDefinitionSO>();

        public bool TryGet(
            string definitionId,
            out CombatEquipmentDefinitionSO definition)
        {
            definition = null;
            return false;
        }
    }

    private sealed class EmptyResourceEconomyContentCatalog :
        IResourceEconomyContentCatalog
    {
        public IReadOnlyList<ResourceItemDefinitionSO> Items =>
            Array.Empty<ResourceItemDefinitionSO>();
        public IReadOnlyList<ProductionRecipeSO> Recipes =>
            Array.Empty<ProductionRecipeSO>();
        public IReadOnlyList<CropDefinitionSO> Crops =>
            Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();

        public bool TryGetItem(
            string itemId,
            out ResourceItemDefinitionSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetRecipe(
            string recipeId,
            out ProductionRecipeSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetCrop(
            string cropId,
            out CropDefinitionSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetMaterial(
            string materialId,
            out CraftMaterialDefinitionSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetSubstance(
            string substanceId,
            out SubstanceDefinitionView definition)
        {
            definition = default;
            return false;
        }
    }

    private sealed class EmptyCharacterLifeDefinitionCatalog :
        ICharacterLifeDefinitionCatalog
    {
        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) =>
            throw new KeyNotFoundException(
                $"No life-history definition '{speciesId.Value}'.");

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => Array.Empty<AgeConditionDefinition>();

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            throw new KeyNotFoundException(
                $"No age-condition definition '{conditionId}'.");
    }

    private sealed class EmptyDiseaseDefinitionCatalog :
        IDiseaseDefinitionCatalog
    {
        public IReadOnlyList<DiseaseDefinition> Definitions =>
            Array.Empty<DiseaseDefinition>();

        public DiseaseDefinition Require(string diseaseId) =>
            throw new KeyNotFoundException(
                $"No disease definition '{diseaseId}'.");
    }
}
#endif
