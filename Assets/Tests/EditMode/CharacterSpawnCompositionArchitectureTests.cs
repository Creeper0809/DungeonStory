using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class CharacterSpawnCompositionArchitectureTests
    {
        private static string ReadScript(string relativePath)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts",
                relativePath));
        }

        [Test]
        public void SpawnFactoryCompletesInactiveCompositionBeforePublication()
        {
            string source = ReadScript(
                "Services/Character/CharacterSpawnObjectFactory.cs");
            string implementation = source.Substring(source.IndexOf(
                "public sealed class CharacterSpawnObjectFactory",
                StringComparison.Ordinal));
            string createInactive = ExtractMethod(
                implementation,
                "public GameObject CreateInactive",
                "public GameObject CreateDetached");
            string createDetached = ExtractMethod(
                implementation,
                "public GameObject CreateDetached(GameObject characterPrefab)",
                "public void ComposeDetached");
            string composeDetached = ExtractMethod(
                implementation,
                "public void ComposeDetached",
                "private void ComposeInactive");
            string composeInactive = ExtractMethod(
                implementation,
                "private void ComposeInactive",
                "public void Inject(GameObject");
            string inject = ExtractMethod(
                implementation,
                "public void Inject(GameObject",
                "public void InjectAddedAbility");
            string publish = ExtractMethod(
                implementation,
                "public void Publish(GameObject",
                "public void PublishDetached");
            string publishDetached = ExtractMethod(
                implementation,
                "public void PublishDetached",
                "public void Destroy(");

            Assert.That(source, Does.Contain(
                "GameObject CreateInactive(GameObject characterPrefab);"));
            Assert.That(source, Does.Not.Contain(
                "GameObject Create(GameObject characterPrefab);"));
            AssertInOrder(
                createInactive,
                "stagingRoot.gameObject.SetActive(false);",
                "Object.Instantiate(",
                "characterObject.SetActive(false);",
                "compose?.Invoke(characterObject);",
                "ComposeInactive(characterObject);",
                "return characterObject;");
            Assert.That(createInactive, Does.Not.Contain(
                "DungeonRuntimeHierarchy.Characters"));
            AssertInOrder(
                createDetached,
                "candidateRoot.gameObject.SetActive(false);",
                "Object.Instantiate(",
                "characterObject.SetActive(false);",
                "compose?.Invoke(characterObject);",
                "ComposeDetached(characterObject);",
                "return characterObject;");
            AssertInOrder(
                composeDetached,
                "actor.PrepareForDetachedRestore();",
                "InjectComponents(",
                "component => component is not CharacterActor",
                "objectResolver.Inject(actor);");
            AssertInOrder(
                composeInactive,
                "actor.PrepareForComposition();",
                "InjectComponents(",
                "component => component is not CharacterActor",
                "objectResolver.Inject(actor);");
            AssertInOrder(
                inject,
                "actor.PrepareForComposition();",
                "component => component is not CharacterActor",
                "objectResolver.Inject(actor);");
            AssertInOrder(
                publish,
                "if (!actor.IsUnpublishedComposition)",
                "actor.RequireReadyForPublishedReactivation();",
                "characterObject.SetActive(true);",
                "actor.RequireCompositionReadyForPublication();",
                "DungeonRuntimeHierarchy.Parent(",
                "actor.PublishComposition();",
                "characterObject.SetActive(true);");
            Assert.That(publish, Does.Contain(
                "if (characterObject.activeSelf || characterObject.activeInHierarchy)"));
            AssertInOrder(
                publishDetached,
                "if (characterObject.activeSelf || characterObject.activeInHierarchy)",
                "actor.RequireDetachedReadyForPublication();",
                "DungeonRuntimeHierarchy.Parent(",
                "actor.PublishDetachedRestore();",
                "characterObject.SetActive(true);");
            Assert.That(source, Does.Not.Contain(
                "void Inject(MonoBehaviour component)"));
        }

        [Test]
        public void FirstStartPreservesSceneAuthoredRestoreAndPoolSemantics()
        {
            string actor = ReadScript(
                "Services/Character/Core/CharacterActor.cs");
            string coordinator = ReadScript(
                "Services/Character/Core/CharacterActorCollaborators.cs");
            string factory = ReadScript(
                "Services/Character/CharacterSpawnObjectFactory.cs");
            string ownerManager = ReadScript(
                "Services/Character/Core/OwnerRunManager.cs");

            AssertInOrder(
                actor,
                "abilityBridge.Initialize(abilityCache, data);",
                "lifecycleCoordinator.MarkInitializedBeforeFirstStart();");
            Assert.That(coordinator, Does.Contain(
                "bool skipInitialDataInitialization ="));
            Assert.That(coordinator, Does.Contain(
                "isPersistentRestore"));
            Assert.That(coordinator, Does.Contain(
                "|| initializedBeforeFirstStart"));
            Assert.That(coordinator, Does.Contain(
                "|| explicitInitializationCompleted"));
            AssertInOrder(
                coordinator,
                "if (!skipInitialDataInitialization)",
                "actor.Initialize(identity.Data);",
                "if (!isPersistentRestore)",
                "lifecycle.SnapToWalkableGridWhenReady()",
                "initializedBeforeFirstStart = false;",
                "stats?.BeginNeedDecaySchedule();");
            Assert.That(coordinator, Does.Contain(
                "if (unpublishedComposition || detachedRestoreCandidate)"));
            Assert.That(coordinator, Does.Contain(
                "initializedBeforeFirstStart = true;"));

            Assert.That(actor, Does.Contain(
                "public bool HasBeenPublished"));
            Assert.That(factory, Does.Contain(
                "actor.RequireReadyForPublishedReactivation();"));
            AssertInOrder(
                factory,
                "actor.RequireReadyForPublishedReactivation();",
                "DungeonRuntimeHierarchy.Parent(",
                "characterObject.SetActive(true);",
                "return;");

            Assert.That(coordinator, Does.Contain(
                "identity.Data == null"));
            Assert.That(coordinator, Does.Contain(
                "!identity.TypedPersistentId.IsValid"));
            Assert.That(coordinator, Does.Contain(
                "runtimeBridge?.IsConfigured != true"));
            string beginOwnerRestorePublication = ExtractMethod(
                ownerManager,
                "public OwnerRestorePublication BeginRestoreCandidatePublication(",
                "public void RollbackRestoreCandidatePublication(");
            string completeOwnerRestorePublication = ExtractMethod(
                ownerManager,
                "public void CompleteRestoreCandidatePublication(",
                "public void HandleOwnerDeath(");
            AssertInOrder(
                beginOwnerRestorePublication,
                "if (candidate.gameObject.activeSelf || candidate.gameObject.activeInHierarchy)",
                "candidate.RequireDetachedReadyForPublication();",
                "DungeonRuntimeHierarchy.Parent(",
                "candidate.PublishDetachedRestore();",
                "publication.MarkCandidatePublished();",
                "return publication;");
            Assert.That(beginOwnerRestorePublication, Does.Not.Contain(
                "SetActive(true)"));
            AssertInOrder(
                completeOwnerRestorePublication,
                "RequirePendingRestorePublication(publication);",
                "previousOwner.gameObject.SetActive(false);",
                "publication.Candidate.gameObject.SetActive(true);",
                "publication.MarkCompleted();");
        }

        [Test]
        public void UnpublishedCompositionSuppressesAllCharacterExternalEffects()
        {
            string actor = ReadScript(
                "Services/Character/Core/CharacterActor.cs");
            string coordinator = ReadScript(
                "Services/Character/Core/CharacterActorCollaborators.cs");
            string runtime = ReadScript(
                "Services/Character/Core/CharacterActorRuntimeBridge.cs");
            string presentation = ReadScript(
                "Services/Character/Core/CharacterActorPresentationBridge.cs");
            string lifecycle = ReadScript(
                "Services/Character/Core/CharacterLifecycle.cs");

            Assert.That(actor, Does.Contain(
                "public bool IsUnpublishedComposition"));
            Assert.That(actor, Does.Contain(
                "!IsDetachedRestoreCandidate && !IsUnpublishedComposition"));
            AssertInOrder(
                actor,
                "public void PrepareForComposition()",
                "lifecycleCoordinator.PrepareForComposition(",
                "internal void RequireCompositionReadyForPublication()",
                "public void PublishComposition()");

            Assert.That(coordinator, Does.Contain(
                "lifecycle.PrepareForComposition(actor);"));
            AssertInOrder(
                coordinator,
                "RequireCompositionReadyForPublication(",
                "runtimeBridge.PublishComposition();",
                "lifecycle.PublishComposition();",
                "presentationBridge.PublishComposition();",
                "unpublishedComposition = false;");

            Assert.That(runtime, Does.Contain(
                "if (!detachedRestoreCandidate && !unpublishedComposition)"));
            Assert.That(runtime, Does.Contain(
                "|| unpublishedComposition"));
            Assert.That(runtime, Does.Contain(
                "worldRegistry.RegisterCharacterLifetime(actor);"));

            Assert.That(presentation, Does.Contain(
                "if (!detachedRestoreCandidate && !unpublishedComposition)"));
            Assert.That(presentation, Does.Contain(
                "|| unpublishedComposition"));

            Assert.That(lifecycle, Does.Contain(
                "private bool IsPublicationSuppressed"));
            Assert.That(lifecycle, Does.Contain(
                "if (!IsPublicationSuppressed)"));
            Assert.That(lifecycle, Does.Contain(
                "public void PublishComposition()"));
        }

        [Test]
        public void ProductionCharacterCallersInitializeBeforeActivation()
        {
            string spawner = ReadScript(
                "Services/Character/CharacterSpawner.cs");
            string preparedParty = ReadScript(
                "Services/Character/Core/PreparedStartPartyGameplayApplier.cs");
            string recruitment = ReadScript(
                "Services/Recruitment/RecruitedCharacterActivationService.cs");
            string invasion = ReadScript(
                "Services/Invasion/InvasionIntruderFactory.cs");
            string invasionDirector = ReadScript(
                "Services/Invasion/InvasionDirectorRuntime.cs");
            string invasionRuntime = ReadScript(
                "Services/Invasion/InvasionIntruderSystem.cs");
            string diagnostics = ReadScript(
                "Services/Infrastructure/Diagnostics/GameplayPerformanceWorldConfigurator.cs");

            Assert.That(spawner, Does.Contain(
                "RequireCharacterObjectFactory().CreateInactive(characterPrefab)"));
            AssertInOrder(
                spawner,
                "spawnedCharacter.Initialize(characterData);",
                "RequireCharacterObjectFactory().Publish(spawnedCharacterGameobject);");
            string takeFromPool = ExtractMethod(
                spawner,
                "private void OnTakeFromPool",
                "private void OnReturnedToPool");
            Assert.That(takeFromPool, Does.Contain("poolGo.SetActive(false);"));

            string createStaff = ExtractMethod(
                preparedParty,
                "private CharacterActor CreateStaffActor",
                "private static void PrepareStaffComposition(");
            string prepareStaffComposition = ExtractMethod(
                preparedParty,
                "private static void PrepareStaffComposition(",
                "private void PlaceParty");
            AssertInOrder(
                createStaff,
                "CharacterId canonicalId =",
                "characterObjectFactory.CreateInactive(",
                "candidate => PrepareStaffComposition(candidate, canonicalId));",
                "actor.Initialize(staffData);",
                "CharacterId composedId = CharacterPersistentIdentity.Require(actor);",
                "if (!composedId.Equals(canonicalId))",
                "actor.RefreshAbilityCache();",
                "if (actor.TryGetAbility(out AbilityWork _))",
                "return actor;");
            AssertInOrder(
                prepareStaffComposition,
                "CharacterIdentity identity =",
                "identity.SetPersistentId(canonicalId);",
                "if (staffObject.GetComponent<AbilityWork>() == null)",
                "staffObject.AddComponent<AbilityWork>();");
            Assert.That(createStaff, Does.Not.Contain("SetActive(true)"));
            Assert.That(prepareStaffComposition, Does.Not.Contain("SetActive(true)"));
            AssertInOrder(
                preparedParty,
                "CharacterActor staff = CreateStaffActor(",
                "characterObjectFactory.Publish(staff.gameObject);");

            Assert.That(recruitment, Does.Contain(
                "characterObjectFactory.CreateInactive("));
            AssertInOrder(
                recruitment,
                "actor.Initialize(record.SourceData);",
                "characterObjectFactory.Publish(actor.gameObject);");
            Assert.That(recruitment, Does.Not.Contain(
                "PrepareForPersistentRestore()"));

            Assert.That(invasion, Does.Contain(
                "characterObjectFactory.CreateInactive("));
            string createIntruder = ExtractMethod(
                invasion,
                "public InvasionIntruderRuntime Create(GameObject intruderPrefab",
                "public InvasionIntruderRuntime CreateDetached(");
            string createInactivePrefablessIntruder = ExtractMethod(
                invasion,
                "private static GameObject CreateInactivePrefablessObject()",
                "public InvasionIntruderRuntime EnsureRuntime(");
            string ensureIntruderRuntime = ExtractMethod(
                invasion,
                "public InvasionIntruderRuntime EnsureRuntime(",
                "private void EnsureRuntimeComponents(");
            string publishIntruder = ExtractMethod(
                invasion,
                "public void Publish(InvasionIntruderRuntime runtime)",
                "public void DestroyDetached(");
            AssertInOrder(
                createIntruder,
                "bool prefabless = intruderPrefab == null;",
                "CreateInactivePrefablessObject()",
                "characterObjectFactory.CreateInactive(",
                "intruderObject.transform.position = position;",
                "return prefabless",
                "EnsureRuntime(intruderObject)",
                "ConfigureRuntime(intruderObject)");
            Assert.That(createIntruder, Does.Not.Contain("SetActive(true)"));
            AssertInOrder(
                createInactivePrefablessIntruder,
                "GameObject intruderObject = new GameObject(",
                "intruderObject.SetActive(false);",
                "stagingRoot.gameObject.SetActive(false);",
                "intruderObject.transform.SetParent(",
                "return intruderObject;");
            AssertInOrder(
                ensureIntruderRuntime,
                "EnsureRuntimeComponents(intruderObject);",
                "characterObjectFactory.Inject(intruderObject);",
                "return ConfigureRuntime(intruderObject);");
            Assert.That(ensureIntruderRuntime, Does.Not.Contain("SetActive(true)"));
            Assert.That(publishIntruder, Does.Contain(
                "characterObjectFactory.Publish(runtime.gameObject);"));
            AssertInOrder(
                invasionDirector,
                "runtime.PrepareBegin(",
                "factory.Publish(runtime);",
                "activeIntruders.Add(runtime);",
                "registered = true;",
                "runtime.OnFinished += OnIntruderFinished;",
                "runtime.StartPrepared(");
            string prepareBegin = ExtractMethod(
                invasionRuntime,
                "public void PrepareBegin(",
                "public void StartPrepared(");
            AssertInOrder(
                prepareBegin,
                "intruderActor.Initialize(data);",
                "intruderActor.Identity?.SetPersistentId(",
                "CharacterId.FromStableSuffix(runtimeId));");
            Assert.That(prepareBegin, Does.Not.Contain(
                "raidAwareness?.IdentifyOperation("));
            AssertInOrder(
                invasionRuntime,
                "public void StartPrepared(",
                "routine = StartCoroutine(",
                "raidAwareness?.IdentifyOperation(");
            AssertInOrder(
                invasionDirector,
                "catch (Exception exception)",
                "intruder = null;",
                "CleanupFailedSpawn(");
            AssertInOrder(
                invasionDirector,
                "private List<Exception> CleanupFailedSpawn(",
                "runtime.OnFinished -= OnIntruderFinished;",
                "activeIntruders.Remove(runtime);",
                "runtime.gameObject.SetActive(false);",
                "factory.DestroyDetached(runtime);",
                "campaignRuntime.ReplaceFromValidatedSnapshot(campaignSnapshot);",
                "externalInfluence.PublishRestoreCandidate(",
                "randomStreamProvider.RestoreStates(");

            string spawnStressCharacters = ExtractMethod(
                diagnostics,
                "private IEnumerator SpawnStressCharacters",
                "private IEnumerator SpawnStressLivestock");
            AssertInOrder(
                spawnStressCharacters,
                "spawner.characterPool.Get()",
                "characterObjectFactory.InjectAddedAbility(work);",
                "actor.Initialize(stressDefinition);",
                "actor.Identity?.SetPersistentId(",
                "actor.SetLifecycleState(CharacterLifecycleState.Active);",
                "characterObjectFactory.Publish(actorObject);",
                "created++;");
            Assert.That(spawnStressCharacters, Does.Not.Contain(
                "characterObjectFactory.Inject(actorObject);"));
        }

        [Test]
        public void AllProductionAndRestoreCallersUseThePublicationBoundary()
        {
            string faction = ReadScript(
                "Services/Factions/FactionReinforcementMaterializer.cs");
            string offense = ReadScript(
                "Services/Offense/OffenseReturnArrivalRuntime.cs");
            string exterior = ReadScript(
                "Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs");
            string save = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");

            string materializeReinforcements = ExtractMethod(
                faction,
                "internal void MaterializeReinforcements",
                "private FactionDefinitionSnapshot FindDefinition");
            string reinforcementPublicationRecovery = ExtractMethod(
                materializeReinforcements,
                "catch (Exception failure)",
                "domain.FinishReinforcementMaterialization(route);");
            string offenseTrySpawnPrisoner = ExtractMethod(
                offense,
                "private bool TrySpawnPrisoner",
                "private void ApplyDownedArrivalHealth");
            string exteriorTrySpawn = ExtractMethod(
                exterior,
                "public bool TrySpawn(",
                "public bool TryFind(");
            string prepareIncidentComposition = ExtractMethod(
                exterior,
                "private static void PrepareIncidentComposition(",
                "public bool TryFind(");
            string validateIncidentIdentity = ExtractMethod(
                exteriorTrySpawn,
                "CharacterId composedActorId = CharacterPersistentIdentity.Require(actor);",
                "// These are visitors, not promoted population staff.");

            AssertInOrder(
                materializeReinforcements,
                "CharacterId actorId = CharacterId.FromStableSuffix(",
                "characterFactory.CreateInactive(",
                "actor.Identity.SetPersistentId(actorId);",
                "actor.Initialize(template);",
                "characterFactory.Publish(instance);",
                "domain.AddReinforcementActor(route, actorId.Value);",
                "recorded = true;");
            AssertInOrder(
                reinforcementPublicationRecovery,
                "bool published = actor != null && actor.HasBeenPublished",
                "if (published)",
                "if (!recorded) domain.AddReinforcementActor(route, actorId.Value);",
                "else",
                "if (instance.activeSelf) instance.SetActive(false);",
                "characterFactory.Destroy(instance);",
                "throw;");
            Assert.That(materializeReinforcements, Does.Not.Contain(
                "RegisterCharacter(actor)"));
            Assert.That(materializeReinforcements, Does.Not.Contain(
                "RegisterCharacterLifetime(actor)"));
            AssertInOrder(
                offenseTrySpawnPrisoner,
                "EnemyIndividualBlueprint blueprint = enemyIndividuals.RequireBlueprint(",
                "CharacterId characterId = blueprint.CharacterId;",
                "characterFactory.CreateInactive(",
                "spawner.characterPrefab,",
                "candidate =>",
                "CharacterIdentity identity = candidate.GetComponent<CharacterIdentity>()",
                "?? throw new InvalidOperationException(\"Prisoner composition requires CharacterIdentity.\");",
                "identity.SetPersistentId(characterId);",
                "CharacterActor actor = characterObject.GetComponent<CharacterActor>();",
                "actorId = characterId.Value;",
                "actor.Initialize(data, blueprint.SpawnRequest);",
                "enemyIndividuals.EnsureCharacterDomains(blueprint);",
                "ApplyDownedArrivalHealth(actor);",
                "actor.SetLifecycleState(CharacterLifecycleState.Downed);",
                "characterFactory.Publish(characterObject);");
            int offenseInitialization = offenseTrySpawnPrisoner.IndexOf(
                "actor.Initialize(data, blueprint.SpawnRequest);",
                StringComparison.Ordinal);
            Assert.That(offenseInitialization, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                offenseTrySpawnPrisoner.Substring(offenseInitialization),
                Does.Not.Contain("SetPersistentId("),
                "A prisoner identity must be final before injection and initialization; post-initialize reassignment would orphan identity-keyed runtime state.");
            Assert.That(offenseTrySpawnPrisoner, Does.Not.Contain(
                "RegisterCharacter(actor)"));
            Assert.That(offenseTrySpawnPrisoner, Does.Not.Contain(
                "RegisterCharacterLifetime(actor)"));
            AssertInOrder(
                exteriorTrySpawn,
                "CharacterId canonicalActorId = new(actorId);",
                "objectFactory.CreateInactive(",
                "candidate => PrepareIncidentComposition(",
                "canonicalActorId));",
                "actor.Initialize(data);",
                "CharacterId composedActorId = CharacterPersistentIdentity.Require(actor);",
                "lifePublication.EnsureRegistered(",
                "objectFactory.Publish(instance);");
            AssertInOrder(
                prepareIncidentComposition,
                "CharacterIdentity identity = instance.GetComponent<CharacterIdentity>();",
                "if (identity == null)",
                "identity.SetPersistentId(canonicalActorId);");
            Assert.That(prepareIncidentComposition,
                Does.Not.Contain("SetActive(true)"));
            AssertInOrder(
                validateIncidentIdentity,
                "CharacterId composedActorId = CharacterPersistentIdentity.Require(actor);",
                "if (!composedActorId.Equals(canonicalActorId))",
                "objectFactory.Destroy(instance);",
                "actor = null;",
                "throw new InvalidOperationException(");
            string exteriorSpawnVisitor = ExtractMethod(
                exterior,
                "protected bool SpawnVisitor(",
                "protected void DespawnVisitors(");
            AssertInOrder(
                exteriorSpawnVisitor,
                "string actorId = CharacterId.FromStableSuffix(",
                ").Value;",
                "Actors.TryFind(actorId, out actor)",
                "Actors.TrySpawn(",
                "actorId,",
                "state.actorIds.Add(actorId);");
            Assert.That(exteriorTrySpawn, Does.Not.Contain(
                "RegisterCharacter(actor)"));
            Assert.That(exteriorTrySpawn, Does.Not.Contain(
                "RegisterCharacterLifetime(actor)"));
            AssertInOrder(
                save,
                "CharacterId canonicalStaffId = canonicalActorIds[staffSave];",
                "characterObjectFactory.CreateDetached(",
                "candidate => PrepareRestoredStaffComposition(",
                "canonicalStaffId,",
                "staffSave.characterType));",
                "candidates.Add(staffCandidate);",
                "staff.Initialize(staffData);",
                "ApplyActorState(grid, staff, staffSave);",
                "CharacterV18RestoreIdentityResolver.AddCandidate(",
                "catch (Exception exception)",
                "DestroyCharacterCandidates(candidates);");
            AssertInOrder(
                save,
                "CharacterRestoreCandidate staffCandidate =",
                "candidates.Add(staffCandidate);",
                "staff.Initialize(staffData);");
            Assert.That(save, Does.Not.Contain(
                "characterObjectFactory.Inject(staffObject)"));
            string prepareRestoredStaffComposition = ExtractMethod(
                save,
                "private static void PrepareRestoredStaffComposition(",
                "private void DestroyCharacterCandidates(");
            AssertInOrder(
                prepareRestoredStaffComposition,
                "CharacterIdentity identity =",
                "identity.SetPersistentId(canonicalId);",
                "if (characterType == CharacterType.NPC",
                "&& staffObject.GetComponent<AbilityWork>() == null)",
                "staffObject.AddComponent<AbilityWork>();");
            Assert.That(prepareRestoredStaffComposition,
                Does.Not.Contain("SetActive(true)"));
            AssertInOrder(
                save,
                "actor.RefreshAbilityCache();",
                "AbilityWork work = actor.GetAbility<AbilityWork>();",
                "bool requiresStaffWork = !source.isOwner",
                "&& source.characterType == CharacterType.NPC;",
                "if (requiresStaffWork && work == null)",
                "if (work == null",
                "if (work != null)",
                "work.SetDutyState(source.dutyState);",
                "work.WorkPriorities.GetPriority(workTypeId)",
                "work.CurrentDutyState != source.dutyState");
        }

        [Test]
        public void OwnerPrefabUsesTheSameInactiveCompositionBoundary()
        {
            string ownerFactory = ReadScript(
                "Services/Character/Core/OwnerCharacterFactory.cs");

            Assert.That(ownerFactory, Does.Contain(
                "ICharacterSpawnObjectFactory characterObjectFactory"));
            Assert.That(ownerFactory, Does.Contain(
                "characterObjectFactory.CreateInactive("));
            Assert.That(ownerFactory, Does.Contain(
                "characterObjectFactory.CreateDetached("));
            Assert.That(ownerFactory, Does.Not.Contain(
                "UnityEngine.Object.Instantiate("));
            AssertInOrder(
                ownerFactory,
                "CharacterActor owner = EnsureOwnerComponents(ownerObject);",
                "owner.EnsureRuntimeState();",
                "owner.Initialize(ownerData);",
                "characterObjectFactory.Publish(ownerObject);");
        }

        private static string ExtractMethod(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }

        private static void AssertInOrder(string source, params string[] markers)
        {
            int previous = -1;
            foreach (string marker in markers)
            {
                int current = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(previous), marker);
                previous = current;
            }
        }

    }
}
