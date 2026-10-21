#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    private const string CombatOperationProvenanceKind = "domain-commit";
    public const string D02FocusedReportPath =
        "Artifacts/QA/d02-shared-combat-transaction-focused.txt";

    [MenuItem("DungeonStory/QA/Run D02 Shared Combat Transaction Focused")]
    public static void RunD02SharedCombatTransactionFocusedFromMenu()
    {
        string result = RunD02SharedCombatTransactionFocused();
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(D02FocusedReportPath, result + Environment.NewLine);
        Debug.Log(result);
    }

    public static string RunD02SharedCombatTransactionFocused()
    {
        var checks = new List<string>();
        VerifyD02CharacterFacadeSuccess(checks);
        VerifyD02QuantizedHealthDelta(checks, 10000f, 7.12345f, "large-health-fractional-damage");
        VerifyD02QuantizedHealthDelta(checks, 100f, 0.0031f, "ordinary-health-small-damage");
        VerifyD02QuantizedHealthRollback(checks);
        VerifyD02WildlifeFacadeSuccess(checks);
        VerifyD02LedgerCommitRollback(checks);
        VerifyD02BodyTransitionCommitRollback(checks);
        VerifyD02RequestParticipantMismatchRejected(checks);
        VerifyD02MinionProductionEntry(checks);
        VerifyD02WildlifeProductionEntry(checks);
        VerifyD02CircusProductionEntry(checks);
        VerifyD02DirectBodyBypassRemoved(checks);
        return "PASS | D02 shared combat transaction | checks="
            + checks.Count
            + " | "
            + string.Join("; ", checks);
    }

    public static void RunD02FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_D02_SHARED_COMBAT_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath)
            || !Path.IsPathRooted(reportPath))
        {
            throw new InvalidOperationException(
                "D02 batch verification requires an absolute report path.");
        }
        if (File.Exists(reportPath))
        {
            throw new IOException("D02 batch report already exists: " + reportPath);
        }

        string result = RunD02SharedCombatTransactionFocused();
        string directory = Path.GetDirectoryName(reportPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "D02 batch report path has no parent directory.");
        }
        Directory.CreateDirectory(directory);
        string report = string.Join(Environment.NewLine, new[]
        {
            "U0_D02_SHARED_COMBAT=PASS",
            "entries=wildlife-retaliation,minion-brawl,circus-exchange",
            "transaction=attack-and-damage-atomic,body-transition-atomic",
            "health=character-exact,wildlife-attacker-exact,production-entry-exact",
            "failure=ledger-rollback,body-transition-rollback,identity-mismatch-rejected",
            "bypass=direct-apply-callers-zero",
            "checks=" + result,
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false"
        }) + Environment.NewLine;
        File.WriteAllText(reportPath, report);
        Debug.Log("U0_D02_SHARED_COMBAT=PASS; report=" + reportPath);
    }

    public static void RunD08MinionParentFocused()
    {
        VerifyD08MinionParentSuccess();
        VerifyD08MinionParentRollback();
        Debug.Log("[D08 Minion Social Parent] PASS");
    }

    private static void VerifyD08MinionParentSuccess()
    {
        using var scenario = new D02Scenario(
            "d08-minion-parent-success",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 4f,
            includeSocialLife: true);
        object runtime = CreateD08MinionRuntime(scenario);
        CombatOutcomeParentContext context = CreateD08MinionParent(
            scenario,
            brawl: true);
        bool succeeded = (bool)InvokePrivate(
            runtime,
            "ApplyUnarmedAttack",
            scenario.OperationId,
            0L,
            scenario.Attacker,
            scenario.Target,
            context);
        Require(succeeded, "D08 minion production entry failed.");

        List<GameplayOutcomeSnapshot> exact = scenario.Ledger
            .CaptureGameplayOutcomes().exactOutcomes;
        GameplayOutcomeSnapshot social = exact.Single(value =>
            value.outcomeTypeId == SocialLifeOutcomeIds.ConflictResolved.Value);
        GameplayOutcomeId parentId = new(
            new GameplayOutcomeRunId(social.runId),
            social.sequence);
        GameplayOutcomeSnapshot damage = exact.Single(value =>
            value.outcomeTypeId
                == ProductionCombatOutcomeIds.CombatDamageResolved.Value);
        GameplayOutcomeSnapshot attack = exact.Single(value =>
            value.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CombatAttackResult)
                .OutcomeTypeId.Value);
        Require(exact.Count == 3
                && social.runId == parentId.RunId.Value
                && social.sequence == parentId.Sequence,
            "D08 parent and both combat rows were not committed together.");
        Require(IsChildOf(damage, parentId)
                && IsChildOf(attack, parentId)
                && damage.rootOperationId == scenario.OperationId
                && attack.rootOperationId == scenario.OperationId
                && social.absoluteDay == CharacterAiEditorTestDependencies.GameCalendar.Day
                && damage.absoluteDay == social.absoluteDay && attack.absoluteDay == social.absoluteDay,
            "D08 D02 combat rows do not point to the social-conflict parent.");
        GameplayOutcomeQueryPage actorHistory = scenario.Ledger.GetForEntity(
            new GameplayEntityId(
                SocialLifeOutcomeIds.CharacterKind,
                scenario.AttackerId),
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(actorHistory.Items.Count == 3,
            "D08 actor reverse query did not return parent and children.");
    }

    private static void VerifyD08MinionParentRollback()
    {
        using var scenario = new D02Scenario(
            "d08-minion-parent-rejected",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.RejectCommit,
            damage: 6f,
            includeSocialLife: true);
        object runtime = CreateD08MinionRuntime(scenario);
        CombatOutcomeParentContext context = CreateD08MinionParent(
            scenario,
            brawl: true);
        string bodyBefore = scenario.CaptureBody();
        bool succeeded = (bool)InvokePrivate(
            runtime,
            "ApplyUnarmedAttack",
            scenario.OperationId,
            0L,
            scenario.Attacker,
            scenario.Target,
            context);
        Require(!succeeded
                && scenario.ExactOutcomeCount == 0
                && string.Equals(
                    bodyBefore,
                    scenario.CaptureBody(),
                    StringComparison.Ordinal),
            "D08 rejected combat leaked parent, child or body mutation.");
    }

    private static object CreateD08MinionRuntime(D02Scenario scenario)
    {
        object runtime = FormatterServices.GetUninitializedObject(
            typeof(MinionSettlementSocialRuntime));
        SetField(runtime, "bodyHealth", scenario.Health);
        SetField(
            runtime,
            "performance",
            CharacterAiEditorTestDependencies.NeutralPerformance);
        SetField(
            runtime,
            "equipment",
            CharacterAiEditorTestDependencies.CombatEquipment);
        SetField(runtime, "combatTransactions", scenario.Facade);
        return runtime;
    }

    private static CombatOutcomeParentContext CreateD08MinionParent(
        D02Scenario scenario,
        bool brawl)
    {
        return new CombatOutcomeParentContext(new CombatConflictFactData
        {
            operationId = scenario.OperationId,
            instigator = new CombatFactParticipantData
            {
                kind = CombatFactParticipantKind.Character,
                id = scenario.AttackerId,
                name = scenario.Attacker.Identity.DisplayName
            },
            target = new CombatFactParticipantData
            {
                kind = CombatFactParticipantKind.Character,
                id = scenario.TargetId,
                name = scenario.Target.Identity.DisplayName
            },
            conflictId = brawl ? "minion-brawl" : "minion-argument",
            severity = brawl ? 5f : 2f,
            committedMoodDelta = 0f,
            origin = CharacterCommandOrigin.Autonomous,
            absoluteDay = Math.Max(
                1,
                CharacterAiEditorTestDependencies.GameCalendar.Day)
        });
    }

    private static bool IsChildOf(
        GameplayOutcomeSnapshot child,
        GameplayOutcomeId parent) => child != null
            && child.parentRunId == parent.RunId.Value
            && child.parentSequence == parent.Sequence
            && child.causationRelationId == "direct-effect";

    private static void VerifyD02CharacterFacadeSuccess(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "character-success",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 7f);
        float before = scenario.TargetHealth;
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Attacker),
            scenario.CharacterWeapon,
            CombatDamageType.Slash,
            revision: 3L);
        Require(result.Succeeded, "D02 character transaction failed: " + result.FailureReason);
        Require(Mathf.Approximately(before - scenario.TargetHealth, 7f),
            "D02 character transaction did not apply the exact body delta.");
        Require(scenario.Resolution.AppliedState == 1
                && scenario.Resolution.PendingCount == 0,
            "D02 character transaction did not complete resolver state.");
        scenario.RequireAttackAndDamage(
            scenario.OperationId,
            3L,
            "character",
            scenario.AttackerId,
            scenario.TargetId,
            scenario.CharacterWeapon.DefinitionId,
            7f);
        checks.Add("character-success");
    }

    private static void VerifyD02QuantizedHealthDelta(
        ICollection<string> checks, float health, float damage, string scope)
    {
        using var scenario = new D02Scenario(scope,
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None, damage: damage);
        scenario.Health.ConfigureVitals(scenario.Target, health, resetCurrentHealth: true);
        float before = scenario.TargetHealth;
        float expectedAfter = Mathf.Max(1f, before - damage);
        float expectedDelta = before - expectedAfter;
        Require(expectedDelta > 0f && !Mathf.Approximately(expectedDelta, damage),
            "D02 fractional fixture must exercise representable health quantization.");
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Attacker), scenario.CharacterWeapon,
            CombatDamageType.Slash, revision: 3L);
        Require(result.Succeeded, "D02 quantized health transaction failed: " + result.FailureReason);
        Require(scenario.TargetHealth == expectedAfter,
            "D02 quantized damage did not match the exact owned health value.");
        scenario.RequireAttackAndDamage(scenario.OperationId, 3L, "character",
            scenario.AttackerId, scenario.TargetId, scenario.CharacterWeapon.DefinitionId,
            expectedDelta);
        Require(scenario.Resolution.AppliedState == 1 && scenario.Resolution.PendingCount == 0,
            "D02 quantized health resolver did not complete exactly once.");
        checks.Add(scope);
    }

    private static void VerifyD02QuantizedHealthRollback(ICollection<string> checks)
    {
        using var scenario = new D02Scenario("quantized-health-commit-failure",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.RejectCommit, damage: 7.12345f);
        scenario.Health.ConfigureVitals(scenario.Target, 10000f, resetCurrentHealth: true);
        string bodyBefore = scenario.CaptureBody();
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Attacker), scenario.CharacterWeapon,
            CombatDamageType.Slash, revision: 5L);
        Require(!result.Succeeded && scenario.CaptureBody() == bodyBefore
                && scenario.Resolution.AppliedState == 0
                && scenario.Resolution.RollbackCount == 1
                && scenario.Resolution.PendingCount == 0 && scenario.ExactOutcomeCount == 0,
            "D02 quantized commit rejection must restore exact health/resolver and publish no outcome.");
        checks.Add("quantized-health-rollback");
    }

    private static void VerifyD02WildlifeFacadeSuccess(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "wildlife-success",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 6f,
            includeWildlife: true);
        float before = scenario.TargetHealth;
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Wildlife),
            scenario.WildlifeWeapon,
            CombatDamageType.Pierce,
            revision: 4L);
        Require(result.Succeeded, "D02 wildlife transaction failed: " + result.FailureReason);
        Require(Mathf.Approximately(before - scenario.TargetHealth, 6f),
            "D02 wildlife transaction did not apply the exact body delta.");
        scenario.RequireAttackAndDamage(
            scenario.OperationId,
            4L,
            "wildlife",
            scenario.Wildlife.WildlifeId,
            scenario.TargetId,
            scenario.WildlifeWeapon.DefinitionId,
            6f);
        checks.Add("wildlife-success");
    }

    private static void VerifyD02LedgerCommitRollback(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "ledger-commit-failure",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.RejectCommit,
            damage: 8f);
        string bodyBefore = scenario.CaptureBody();
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Attacker),
            scenario.CharacterWeapon,
            CombatDamageType.Slash,
            revision: 5L);
        Require(!result.Succeeded,
            "D02 injected attack-ledger commit failure unexpectedly succeeded.");
        Require(string.Equals(bodyBefore, scenario.CaptureBody(), StringComparison.Ordinal),
            "D02 attack-ledger failure did not restore the exact body state.");
        Require(scenario.Resolution.AppliedState == 0
                && scenario.Resolution.RollbackCount == 1
                && scenario.Resolution.PendingCount == 0,
            "D02 attack-ledger failure did not roll back resolver state.");
        Require(scenario.ExactOutcomeCount == 0,
            "D02 attack-ledger failure leaked an exact outcome.");
        checks.Add("ledger-rollback");
    }

    private static void VerifyD02BodyTransitionCommitRollback(
        ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "body-transition-failure",
            MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
            MigratedProducerOutcomeFaultMode.RejectCommit,
            damage: 100f,
            prepareThresholdState: true,
            nonlethal: true);
        string bodyBefore = scenario.CaptureBody();
        CharacterCombatTransactionResult result = scenario.Execute(
            new CombatParticipantRef(scenario.Attacker),
            scenario.CharacterWeapon,
            CombatDamageType.Slash,
            revision: 6L);
        Require(!result.Succeeded,
            "D02 injected health-transition commit failure unexpectedly succeeded.");
        Require(string.Equals(bodyBefore, scenario.CaptureBody(), StringComparison.Ordinal),
            "D02 health-transition failure did not restore the exact body state.");
        Require(scenario.Resolution.AppliedState == 0
                && scenario.Resolution.RollbackCount == 1
                && scenario.Resolution.PendingCount == 0,
            "D02 health-transition failure did not roll back resolver state.");
        Require(scenario.ExactOutcomeCount == 0,
            "D02 health-transition failure leaked an exact outcome.");
        checks.Add("body-transition-rollback");
    }

    private static void VerifyD02RequestParticipantMismatchRejected(
        ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "participant-mismatch",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 5f);
        CombatAttackRequest request = scenario.CreateRequest(
            new CombatParticipantRef(scenario.Attacker),
            scenario.CharacterWeapon,
            "character:not-the-attacker");
        string bodyBefore = scenario.CaptureBody();
        CharacterCombatTransactionResult result = scenario.Facade.ResolveAndApply(
            request,
            new CombatParticipantRef(scenario.Attacker),
            scenario.Target,
            CombatDamageType.Slash,
            "D02 mismatch",
            CharacterCommandOrigin.Autonomous,
            7L);
        Require(!result.Succeeded
                && result.FailureReason ==
                    "combat-transaction-request-participant-mismatch"
                && scenario.Resolution.ResolveCount == 0
                && scenario.ExactOutcomeCount == 0
                && string.Equals(bodyBefore, scenario.CaptureBody(), StringComparison.Ordinal),
            "D02 participant mismatch reached resolution, body mutation or ledger commit.");
        checks.Add("identity-mismatch-rejected");
    }

    private static void VerifyD02MinionProductionEntry(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "minion-entry",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 4f);
        object runtime = FormatterServices.GetUninitializedObject(
            typeof(MinionSettlementSocialRuntime));
        SetField(runtime, "bodyHealth", scenario.Health);
        SetField(runtime, "performance", CharacterAiEditorTestDependencies.NeutralPerformance);
        SetField(runtime, "equipment", CharacterAiEditorTestDependencies.CombatEquipment);
        SetField(runtime, "combatTransactions", scenario.Facade);
        InvokePrivate(
            runtime,
            "ApplyUnarmedAttack",
            scenario.OperationId,
            11L,
            scenario.Attacker,
            scenario.Target,
            default(CombatOutcomeParentContext));
        Require(scenario.Resolution.ResolveCount == 1,
            "D02 minion production entry did not enter the shared facade.");
        scenario.RequireAttackAndDamage(
            scenario.OperationId,
            11L,
            "character",
            scenario.AttackerId,
            scenario.TargetId,
            "combat:unarmed",
            4f);
        checks.Add("minion-production-entry");
    }

    private static void VerifyD02WildlifeProductionEntry(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "wildlife-entry",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 5f,
            includeWildlife: true);
        object runtime = FormatterServices.GetUninitializedObject(
            typeof(WildlifeHuntRuntime));
        SetField(runtime, "combatTransactions", scenario.Facade);
        SetField(runtime, "performance", CharacterAiEditorTestDependencies.NeutralPerformance);
        SetField(runtime, "worldUiHierarchy", new NoWorldUiHierarchy());
        InvokePrivate(
            runtime,
            "ApplyWildlifeRetaliation",
            scenario.OperationId,
            12L,
            scenario.Wildlife,
            scenario.Target,
            CharacterAiEditorTestDependencies.CombatEquipment,
            scenario.Health);
        Require(scenario.Resolution.ResolveCount == 1,
            "D02 wildlife retaliation entry did not enter the shared facade.");
        scenario.RequireAttackAndDamage(
            scenario.OperationId,
            12L,
            "wildlife",
            scenario.Wildlife.WildlifeId,
            scenario.TargetId,
            "combat:wildlife-natural",
            5f);
        checks.Add("wildlife-production-entry");
    }

    private static void VerifyD02CircusProductionEntry(ICollection<string> checks)
    {
        using var scenario = new D02Scenario(
            "circus-entry",
            MigratedProducerOutcomeKind.CombatAttackResult,
            MigratedProducerOutcomeFaultMode.None,
            damage: 3f);
        object runtime = FormatterServices.GetUninitializedObject(typeof(CircusRuntime));
        SetField(runtime, "captivity", new D02CaptivityQuery(
            scenario.Attacker,
            scenario.Target));
        SetField(runtime, "random", new RandomStreamProvider(7919).Get(
            "d02-circus-entry"));
        SetField(runtime, "bodyHealthQuery", scenario.Health);
        SetField(runtime, "equipment", CharacterAiEditorTestDependencies.CombatEquipment);
        SetField(runtime, "performance", CharacterAiEditorTestDependencies.NeutralPerformance);
        SetField(runtime, "combatTransactions", scenario.Facade);
        SetField(runtime, "clock", CharacterAiEditorTestDependencies.GameClock);
        var order = new CircusShowOrder
        {
            orderId = scenario.OperationId,
            performerIds = new List<string>
            {
                scenario.AttackerId,
                scenario.TargetId
            }
        };
        InvokePrivate(runtime, "ResolveCombatExchange", order);
        Require(scenario.Resolution.ResolveCount == 1,
            "D02 circus exchange entry did not enter the shared facade.");
        CombatAttackRequest request = scenario.Resolution.Requests.Single();
        string expectedTargetId = request.DefenderId;
        scenario.RequireAttackAndDamage(
            scenario.OperationId,
            CharacterAiEditorTestDependencies.GameClock.FrameCount,
            "character",
            request.AttackerId,
            expectedTargetId,
            "combat:unarmed",
            3f);
        checks.Add("circus-production-entry");
    }

    private static void VerifyD02DirectBodyBypassRemoved(ICollection<string> checks)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string[] relativePaths =
        {
            "Assets/Scripts/Services/Wildlife/WildlifeHuntRuntime.cs",
            "Assets/Scripts/Services/Captivity/MinionSettlementSocialRuntime.cs",
            "Assets/Scripts/Services/Captivity/CircusRuntime.cs"
        };
        foreach (string relativePath in relativePaths)
        {
            string source = File.ReadAllText(Path.Combine(root, relativePath));
            Require(!source.Contains("ApplyCombatResult(", StringComparison.Ordinal),
                "D02 direct body combat bypass remains in " + relativePath);
            Require(source.Contains(
                    "combatTransactions.ResolveAndApply(",
                    StringComparison.Ordinal),
                "D02 shared combat transaction call is missing in " + relativePath);
        }
        checks.Add("three-callers-no-direct-body-bypass");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private sealed class D02Scenario : IDisposable
    {
        private readonly List<Object> cleanup = new();
        private readonly OutcomeHarness harness;
        private readonly Grid wildlifeGrid;
        private readonly GameEventBus events;
        private readonly CharacterVitalsActivityObserver vitalsActivities;

        internal D02Scenario(
            string scope,
            MigratedProducerOutcomeKind faultKind,
            MigratedProducerOutcomeFaultMode faultMode,
            float damage,
            bool includeWildlife = false,
            bool prepareThresholdState = false,
            bool nonlethal = false,
            bool includeSocialLife = false)
        {
            OperationId = "combat:d02:" + scope;
            harness = new OutcomeHarness(
                faultKind,
                "d02-" + scope,
                includeCombatDamage: true,
                faultMode,
                deliveryFault: false,
                stableRunId: "run:d02:" + scope,
                includeSocialLife: includeSocialLife);
            var clock = new UnityGameClock();
            events = new GameEventBus();
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            Health = new CharacterBodyHealthRuntime(new DungeonStory.Foundation.UnityRealtimeClock(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                harness.AggregateRootStore,
                CharacterAiEditorTestDependencies.GameCalendar);
            Attacker = CreateCharacter(
                "D02 공격자",
                "character:d02:" + scope + ":attacker");
            Target = CreateCharacter(
                "D02 피격자",
                "character:d02:" + scope + ":target");
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(Attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(Target);
            Health.ConfigureFactPublication(harness.Facts, harness.Facts);
            if (prepareThresholdState)
            {
                Health.ApplyLocalizedDamage(
                    Target,
                    CombatBodyPart.Torso,
                    20f,
                    "d02-prior-wound",
                    allowDeath: false);
                Health.RestoreLegacyVitalsProjection(Target, 100f, 25f, 0.75f);
            }

            if (includeWildlife)
            {
                wildlifeGrid = new Grid(4, 1);
                for (int x = 0; x < wildlifeGrid.width; x++)
                {
                    wildlifeGrid.GetGridCell(new Vector2Int(x, 0))
                        .SetAreaType(GridCellAreaType.ExteriorPath);
                }
                var wildlifeObject = new GameObject("D02 야생동물");
                cleanup.Add(wildlifeObject);
                Wildlife = wildlifeObject.AddComponent<WildlifeActor>();
                Wildlife.ConfigureRuntimeServices(
                    TabooButcheryCausalTestPorts.Guard<IGridPathSearchBroker>(),
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    clock,
                    new RandomStreamProvider(3571),
                    TabooButcheryCausalTestPorts.Guard<IDoorAccessQuery>());
                Wildlife.Initialize(
                    wildlifeGrid,
                    WildlifeTestFixtures.MossBoar,
                    "wildlife:d02:" + scope,
                    new Vector2Int(1, 0));
            }

            Resolution = new D02Resolution(damage, nonlethal);
            var applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                Health,
                Health,
                Health,
                harness.CombatActions,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                CharacterAiEditorTestDependencies.GameCalendar);
            Facade = new CharacterCombatTransactionFacade(Resolution, applier);
            CharacterWeapon = CombatWeaponSnapshot.CreateUnarmed();
            WildlifeWeapon = new CombatWeaponSnapshot(
                "combat:wildlife-natural",
                string.Empty,
                CombatEquipmentKind.MeleeWeapon,
                new MeleeStrikeVerb
                {
                    attackTime = 1f,
                    baseDamage = damage,
                    penetration = 0f,
                    damageType = CombatDamageType.Pierce,
                    tracking = 0.08f
                },
                new[]
                {
                    new CombatRangeProfile
                    {
                        band = CombatRangeBand.Contact,
                        accuracyMultiplier = 1f,
                        damageMultiplier = 1f
                    }
                },
                1,
                CombatEquipmentQuality.Normal,
                string.Empty,
                0,
                0,
                0f,
                false,
                false,
                false);
        }

        internal CharacterBodyHealthRuntime Health { get; }
        internal CharacterActor Attacker { get; }
        internal CharacterActor Target { get; }
        internal WildlifeActor Wildlife { get; }
        internal D02Resolution Resolution { get; }
        internal CharacterCombatTransactionFacade Facade { get; }
        internal CombatWeaponSnapshot CharacterWeapon { get; }
        internal CombatWeaponSnapshot WildlifeWeapon { get; }
        internal string OperationId { get; }
        internal string AttackerId => CharacterPersistentIdentity.Require(Attacker).Value;
        internal string TargetId => CharacterPersistentIdentity.Require(Target).Value;
        internal float TargetHealth => Health.GetVitals(Target).CurrentHealth;
        internal int ExactOutcomeCount => harness.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count;
        internal GameplayOutcomeLedger Ledger => harness.Ledger;
        internal GameplayOutcomeRecorder Recorder => harness.Recorder;

        internal CharacterCombatTransactionResult Execute(
            CombatParticipantRef attacker,
            CombatWeaponSnapshot weapon,
            CombatDamageType damageType,
            long revision,
            CombatOutcomeParentContext parent = default)
        {
            CombatAttackRequest request = CreateRequest(attacker, weapon, attacker.Id);
            return Facade.ResolveAndApply(
                request,
                attacker,
                Target,
                damageType,
                "D02 focused combat",
                CharacterCommandOrigin.Autonomous,
                revision,
                parent);
        }

        internal CombatAttackRequest CreateRequest(
            CombatParticipantRef attacker,
            CombatWeaponSnapshot weapon,
            string requestAttackerId) => new(
                OperationId,
                requestAttackerId,
                TargetId,
                default,
                default,
                weapon,
                1,
                CombatFireMode.Aimed,
                default);

        internal string CaptureBody() => JsonUtility.ToJson(Health.Capture());

        internal void RequireAttackAndDamage(
            string operationId,
            long revision,
            string attackerKind,
            string attackerId,
            string targetId,
            string weaponDefinitionId,
            float damage)
        {
            List<GameplayOutcomeSnapshot> exact = harness.Ledger
                .CaptureGameplayOutcomes().exactOutcomes;
            string attackTypeId = MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CombatAttackResult)
                .OutcomeTypeId.Value;
            GameplayOutcomeSnapshot attack = exact.Single(value =>
                value != null
                && value.outcomeTypeId == attackTypeId
                && value.rootOperationId == operationId);
            GameplayOutcomeSnapshot damageOutcome = exact.Single(value =>
                value != null
                && value.outcomeTypeId
                    == ProductionCombatOutcomeIds.CombatDamageResolved.Value
                && value.operationId == operationId
                && value.commitRevision == revision);
            Require(exact.Count == 2,
                "D02 successful combat did not publish exactly attack + damage.");
            Require(attack.provenance.Count(value => value.kindId
                        == CombatOperationProvenanceKind) == 1
                    && attack.provenance.Single(value => value.kindId
                        == CombatOperationProvenanceKind).value
                        == operationId,
                "D02 attack lost or duplicated its original request operation identity.");
            Require(attack.participants.Any(value =>
                    value.entityKindId == attackerKind
                    && value.entityId == attackerId
                    && value.roleId == MigratedProducerOutcomeIds.ActorRole.Value)
                && attack.participants.Any(value =>
                    value.entityKindId == "character"
                    && value.entityId == targetId
                    && value.roleId == MigratedProducerOutcomeIds.TargetRole.Value),
                "D02 attack participants do not match the executed request.");
            Require(attack.facts.Any(value =>
                    value.factId
                        == CombatAttackEvidenceFactIds.WeaponDefinitionId.Value
                    && value.value == weaponDefinitionId),
                "D02 attack weapon evidence does not match the executed request.");
            Require(damageOutcome.participants.Any(value =>
                    value.entityKindId == attackerKind
                    && value.entityId == attackerId
                    && value.roleId == ProductionCombatOutcomeIds.AttackerRole.Value)
                && damageOutcome.participants.Any(value =>
                    value.entityKindId == "character"
                    && value.entityId == targetId
                    && value.roleId == ProductionCombatOutcomeIds.VictimRole.Value),
                "D02 damage participants do not match the executed request.");
            Require(damageOutcome.metrics.Any(value =>
                    value.metricId == ProductionCombatOutcomeIds.DamageMetric.Value
                    && Math.Abs(value.value - damage) <= 0.001d),
                "D02 damage metric does not match the applied body delta.");
        }

        public void Dispose()
        {
            vitalsActivities.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(Attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(Target);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
            harness.Dispose();
        }

        private CharacterActor CreateCharacter(string displayName, string id)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(CharacterType.NPC, displayName, "human");
            cleanup.Add(data);
            var actorObject = new GameObject(displayName);
            cleanup.Add(actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(id);
            actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    Health,
                    Health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            Health.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            return actor;
        }
    }

    private sealed class D02Resolution :
        ICombatResolutionService,
        ICombatResolutionTransactionService
    {
        private readonly float damage;
        private readonly bool nonlethal;
        private readonly Dictionary<string, int> pending = new(StringComparer.Ordinal);

        internal D02Resolution(float damage, bool nonlethal)
        {
            this.damage = damage;
            this.nonlethal = nonlethal;
        }

        internal List<CombatAttackRequest> Requests { get; } = new();
        internal int ResolveCount { get; private set; }
        internal int AppliedState { get; private set; }
        internal int RollbackCount { get; private set; }
        internal int PendingCount => pending.Count;

        public CombatAttackResult Resolve(CombatAttackRequest request)
        {
            CombatAttackResult result = ResolveDetached(request);
            if (!TryApplyResolvedResultMutation(request, result, out string failure))
                throw new InvalidOperationException(failure);
            CompleteResolvedResultMutation(request);
            return result;
        }

        public CombatAttackResult ResolveDetached(CombatAttackRequest request)
        {
            if (pending.ContainsKey(request.EventId))
                throw new InvalidOperationException("d02-resolution-already-pending");
            pending.Add(request.EventId, AppliedState);
            Requests.Add(request);
            ResolveCount++;
            return new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: damage,
                appliedDamage: damage,
                bleeding: 0f,
                suppression: 2f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                nonlethal: nonlethal);
        }

        public bool TryApplyResolvedResultMutation(
            CombatAttackRequest request,
            CombatAttackResult result,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!pending.ContainsKey(request.EventId))
            {
                failureReason = "d02-resolution-pending-missing";
                return false;
            }
            AppliedState++;
            return true;
        }

        public bool TryRollbackResolvedResultMutation(
            CombatAttackRequest request,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!pending.TryGetValue(request.EventId, out int before))
            {
                failureReason = "d02-resolution-pending-missing";
                return false;
            }
            AppliedState = before;
            pending.Remove(request.EventId);
            RollbackCount++;
            return true;
        }

        public void CompleteResolvedResultMutation(CombatAttackRequest request)
        {
            if (!pending.Remove(request.EventId))
                throw new InvalidOperationException("d02-resolution-pending-missing");
        }

        public CombatAttackPreview Preview(CombatAttackRequest request) => default;
        public float CalculateAttackInterval(
            CombatStatSnapshot attacker,
            CombatWeaponSnapshot weapon,
            CombatFireMode mode) => 1f;
        public float CalculateReloadTime(
            CombatStatSnapshot actor,
            CombatWeaponSnapshot weapon) => 1f;
        public float CalculateWeaponSwitchTime(
            CombatStatSnapshot actor,
            float weaponWeight) => 1f;
    }

    private sealed class D02CaptivityQuery : ICaptivityRuntime
    {
        private readonly Dictionary<string, CharacterActor> actors;

        internal D02CaptivityQuery(params CharacterActor[] actors)
        {
            this.actors = actors.ToDictionary(
                actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal);
        }

        public IReadOnlyList<CaptiveState> Captives => Array.Empty<CaptiveState>();
        public IReadOnlyList<CaptivePolicyData> Policies => Array.Empty<CaptivePolicyData>();
        public bool TryGetCaptive(string captiveId, out CaptiveState captive)
        {
            captive = null;
            return false;
        }
        public bool TryGetActor(string captiveId, out CharacterActor actor) =>
            actors.TryGetValue(captiveId ?? string.Empty, out actor);
        public bool TryGetHousing(string captiveId, out BuildableObject housing)
        {
            housing = null;
            return false;
        }
        public bool TryGetRehabilitationFacility(
            string captiveId,
            out BuildableObject facility)
        {
            facility = null;
            return false;
        }
        public bool IsCaptive(string persistentId) =>
            actors.ContainsKey(persistentId ?? string.Empty);
        public bool HasSecureHousing(
            CharacterActor captive,
            out BuildableObject housing,
            out string reason)
        {
            housing = null;
            reason = "d02-not-required";
            return false;
        }
    }
}
#endif
