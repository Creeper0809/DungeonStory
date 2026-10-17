#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class CharacterDeprivationExperienceFactDebugScenarios
{
    public static string Run()
    {
        _ = RunPrimitiveRunLifetimeContracts();
        _ = RunBreakdownRunLifetimeContracts();
        _ = RunBreakdownMovementDisposalContracts();
        var gameObject = new GameObject("Deprivation experience observer fixture");
        CharacterSO definition = null;
        using var facts = new DomainFactDispatcher();
        try
        {
            gameObject.AddComponent<SpriteRenderer>();
            var actor = gameObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            definition = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(CharacterType.NPC, "경험 관찰자", "Orc");
            actor.Initialization(definition);
            actor.Identity.SetPersistentId("character:deprivation-experience-test");
            using var binding = new CharacterDeprivationExperienceFactBinding().Connect(facts);
            var state = new CharacterDeprivationStateStore(new DungeonRuntimeAggregateRootStore());
            var consequences = new CharacterDeprivationConsequences(state,
                CharacterAiEditorTestDependencies.WorldRegistry, new EditorFixedGameCalendar(3), facts);
            consequences.RecordTaboo(actor, " 금기를 경험했다 ");
            var personal = actor.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo");
            Require(personal.domain == CharacterNarrativeDomain.Survival && personal.subjectId == string.Empty
                && personal.outcome == "금기를 경험했다" && personal.totalValue == 1f && personal.lastDay == 3
                && personal.count == 1 && state.Capture().Single().tabooMemories.Contains("금기를 경험했다"),
                "personal experience changed its state or narrative projection");
            var witnessed = new CharacterDeprivationExperienceFact(CharacterDeprivationExperienceKind.WitnessedTaboo,
                actor, "character:witness-source", "금기를 목격했다", -8f, 3);
            Require(facts.TryReserve(witnessed, out var lease, out string failure), failure);
            Require(facts.Publish(lease, witnessed).Completed, "witness projection did not complete");
            var witness = actor.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo-witness");
            Require(witness.domain == CharacterNarrativeDomain.Relationship && witness.subjectId == witnessed.SubjectId
                && witness.outcome == witnessed.Memory && witness.totalValue == -8f && witness.count == 1,
                "witness projection changed the original evidence");
            Require(!facts.Publish(lease, witnessed).Completed && witness.count == 1,
                "reusing one notification lease recorded a second experience");
            binding.Dispose();
            Require(!facts.TryReserve(witnessed, out _, out _) && witness.count == 1,
                "detached observer still accepted notifications");
            return "deprivation-personal-producer/witness-observer/exact-evidence/lease-and-detach=PASS;natural-AI=NOT-RUN";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    internal static int RunPrimitiveRunLifetimeContracts()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(CharacterPrimitiveSurvivalRunner);
        var runner = (CharacterPrimitiveSurvivalRunner)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        var runningField = type.GetField("runningActions", flags);
        var runs = (System.Collections.IDictionary)Activator.CreateInstance(runningField.FieldType);
        runningField.SetValue(runner, runs);
        var completionField = type.GetField("completion", flags);
        object completion = Activator.CreateInstance(completionField.FieldType, flags, null,
            new object[] { null, null, null, null, null, null }, null);
        completionField.SetValue(runner, completion);

        var id = new CharacterId("character:primitive-run-lifetime");
        var kind = CharacterPrimitiveSurvivalActionKind.FieldMeal;
        var intentKind = CharacterActionIntentKind.RoutineNeed;
        var lease = new CharacterActionIntentLease("survival:primitive:FieldMeal", intentKind, 1);
        Type runType = runningField.FieldType.GetGenericArguments()[1];
        object NewRun(CharacterActionIntentLease candidateLease = default) =>
            Activator.CreateInstance(runType, flags, null,
                new object[] { null, kind, intentKind, candidateLease.IsValid ? candidateLease : lease }, null);
        var runMethod = type.GetMethod("Run", flags);
        var currentMethod = type.GetMethod("IsCurrentRun", flags);
        var completeMethod = type.GetMethod("CompleteRunningAction", flags);
        var replacementMethod = type.GetMethod("ReplacementSharesIntent", flags);
        var sharesMethod = runType.GetMethod("SharesIntent", flags);
        System.Collections.IEnumerator Routine(object run) =>
            (System.Collections.IEnumerator)runMethod.Invoke(runner,
                new object[] { null, id, kind, lease, run });
        bool Current(object run) =>
            (bool)currentMethod.Invoke(runner, new object[] { id, run });
        bool Complete(object run) =>
            (bool)completeMethod.Invoke(runner, new object[] { id, run });
        bool ReplacementShares(object run, CharacterActionIntentLease candidateLease) =>
            (bool)replacementMethod.Invoke(runner,
                new object[] { id, run, null, candidateLease });
        int checks = 0;
        void Check(bool condition, string detail)
        {
            Require(condition, "primitive run lifetime: " + detail);
            checks++;
        }

        object first = NewRun();
        runs[id] = first;
        var old = Routine(first);
        runner.Reset();
        object replacement = NewRun();
        runs[id] = replacement;
        Check(!old.MoveNext() && Current(replacement),
            "old unstarted MoveNext removed the Reset replacement");
        Check(ReplacementShares(first, lease),
            "same owner/kind/epoch replacement was not recognized");
        Check(!ReplacementShares(first,
                new CharacterActionIntentLease("other", intentKind, lease.Epoch)),
            "different intent owner shared the replacement");
        Check(!ReplacementShares(first,
                new CharacterActionIntentLease(lease.OwnerId, CharacterActionIntentKind.EmergencyNeed, lease.Epoch)),
            "different intent kind shared the replacement");
        Check(!ReplacementShares(first,
                new CharacterActionIntentLease(lease.OwnerId, lease.Kind, lease.Epoch + 1)),
            "different intent epoch shared the replacement");
        Check(!Complete(first) && Current(replacement),
            "old explicit completion removed the Reset replacement");
        Check(Complete(replacement) && runs.Count == 0,
            "current completion did not remove its own marker");
        Check(!Complete(replacement), "repeated current completion succeeded");

        first = NewRun();
        runs[id] = first;
        old = Routine(first);
        runner.ReleaseActor(id);
        replacement = NewRun();
        runs[id] = replacement;
        Check(!old.MoveNext() && Current(replacement),
            "old unstarted MoveNext removed the ReleaseActor replacement");

        first = NewRun();
        runs[id] = first;
        old = Routine(first);
        Check(old.MoveNext() && old.Current is System.Collections.IEnumerator,
            "old primitive run did not suspend at its guarded action");
        runner.ReleaseActor(id);
        replacement = NewRun();
        runs[id] = replacement;
        Check(!old.MoveNext() && Current(replacement),
            "retired suspended MoveNext removed the replacement");
        ((IDisposable)old).Dispose();
        Check(Current(replacement),
            "retired suspended disposal removed the replacement");
        var current = Routine(replacement);
        Check(current.MoveNext() && current.Current is System.Collections.IEnumerator,
            "current primitive run did not expose its guarded action");
        ((IDisposable)current).Dispose();
        Check(runs.Count == 0, "current run disposal did not release its marker");

        object exact = NewRun();
        Check((bool)sharesMethod.Invoke(exact, new object[] { null, lease }),
            "exact intent lease did not match");
        Check(!(bool)sharesMethod.Invoke(exact, new object[] { null,
                new CharacterActionIntentLease("other", lease.Kind, lease.Epoch) }),
            "SharesIntent ignored the owner");
        Check(!(bool)sharesMethod.Invoke(exact, new object[] { null,
                new CharacterActionIntentLease(lease.OwnerId, CharacterActionIntentKind.EmergencyNeed, lease.Epoch) }),
            "SharesIntent ignored the kind");
        Check(!(bool)sharesMethod.Invoke(exact, new object[] { null,
                new CharacterActionIntentLease(lease.OwnerId, lease.Kind, lease.Epoch + 1) }),
            "SharesIntent ignored the epoch");
        runs[id] = exact;
        Check((bool)replacementMethod.Invoke(runner, new object[] { id, first, null, lease }),
            "ReplacementSharesIntent rejected the exact current lease");
        runs.Clear();
        string status;
        Check(!runner.TryStart(null, kind, out status) && runs.Count == 0,
            "TryStart admitted a null actor");

        int steps = 0, disposed = 0;
        System.Collections.IEnumerator Child()
        {
            try { steps++; yield return null; steps++; }
            finally { disposed++; }
        }
        System.Collections.IEnumerator Parent() { yield return Child(); steps += 100; }
        var runWhileCurrent = type.GetMethod("RunWhileCurrent",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        System.Collections.IEnumerator Guard(System.Collections.IEnumerator action, Func<bool> predicate) =>
            (System.Collections.IEnumerator)runWhileCurrent.Invoke(null, new object[] { action, predicate });

        first = NewRun();
        runs[id] = first;
        object guardedRun = first;
        var parent = Guard(Parent(), () => Current(guardedRun));
        Check(parent.MoveNext() && parent.Current is System.Collections.IEnumerator,
            "recursive primitive guard did not expose its child");
        var child = (System.Collections.IEnumerator)parent.Current;
        Check(child.MoveNext() && steps == 1,
            "recursive primitive child did not reach its first yield");
        runner.Reset();
        replacement = NewRun();
        runs[id] = replacement;
        Check(!child.MoveNext() && steps == 1 && disposed == 1,
            "retired recursive child continued or was not disposed");
        Check(!parent.MoveNext() && steps == 1 && Current(replacement),
            "retired recursive parent resumed or removed the replacement");
        steps = 0;
        disposed = 0;
        var completed = Guard(Child(), () => true);
        Check(completed.MoveNext() && !completed.MoveNext() && steps == 2 && disposed == 1,
            "current recursive guard changed normal child ordering or disposal");
        steps = 0;
        disposed = 0;
        var cancelled = Guard(Child(), () => true);
        Check(cancelled.MoveNext(), "current recursive guard could not start");
        ((IDisposable)cancelled).Dispose();
        Check(steps == 1 && disposed == 1,
            "explicit recursive guard disposal resumed or leaked its child");
        steps = 0;
        disposed = 0;
        parent = Guard(Parent(), () => true);
        Check(parent.MoveNext() && ((System.Collections.IEnumerator)parent.Current).MoveNext(),
            "recursive guard did not start before parent disposal");
        ((IDisposable)parent).Dispose();
        Check(steps == 1 && disposed == 1,
            "parent disposal did not dispose the suspended recursive child");
        runner.Reset();
        return checks;
    }

    internal static int RunBreakdownRunLifetimeContracts()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(CharacterBreakdownActionRunner);
        var runner = (CharacterBreakdownActionRunner)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        var field = type.GetField("runningActions", flags);
        var runs = (System.Collections.IDictionary)Activator.CreateInstance(field.FieldType);
        field.SetValue(runner, runs);
        var routines = new System.Collections.Generic.Dictionary<CharacterBreakdownKind,
            Func<CharacterActor, CharacterActionIntentLease, System.Collections.IEnumerator>>();
        type.GetField("actionRoutines", flags).SetValue(runner, routines);
        var id = new CharacterId("character:breakdown-run-lifetime");
        var kind = CharacterBreakdownKind.DesperateRelief;
        var lease = new CharacterActionIntentLease(CharacterBreakdownActionRunner.IntentOwnerId,
            CharacterActionIntentKind.Breakdown, 1);
        Type runType = field.FieldType.GetGenericArguments()[1];
        object NewRun(int generation = 1) => Activator.CreateInstance(runType, flags, null,
            new object[] { null, kind, generation, lease }, null);
        System.Collections.IEnumerator Routine(object run, int generation = 1) =>
            (System.Collections.IEnumerator)type.GetMethod("RunBreakdownAction", flags)
                .Invoke(runner, new object[] { null, id, kind, generation, lease, run });
        bool Current(object run) => (bool)type.GetMethod("IsCurrentRun", flags).Invoke(runner, new object[] { id, run });
        int checks = 0;
        void Check(bool condition, string detail) { Require(condition, "breakdown run lifetime: " + detail); checks++; }

        object first = NewRun();
        runs[id] = first;
        var old = Routine(first);
        runner.Reset();
        object replacement = NewRun();
        runs[id] = replacement;
        Check(!old.MoveNext() && Current(replacement), "old completion after Reset removed the replacement");
        Check((bool)type.GetMethod("ReplacementSharesIntent", flags).Invoke(runner,
            new object[] { id, first, null, lease }), "old scheduler failure cleanup could end a renewed replacement lease");
        Check(!(bool)type.GetMethod("ReplacementSharesIntent", flags).Invoke(runner,
            new object[] { id, replacement, null, lease }), "current run was incorrectly classified as its own replacement");
        Check(!(bool)type.GetMethod("CompleteRunningAction", flags).Invoke(runner, new object[] { id, first })
            && Current(replacement), "old explicit completion removed the replacement");
        Check((bool)type.GetMethod("CompleteRunningAction", flags).Invoke(runner, new object[] { id, replacement })
            && runs.Count == 0, "current completion did not release its own marker");
        Check(!(bool)type.GetMethod("CompleteRunningAction", flags).Invoke(runner, new object[] { id, replacement }),
            "repeated completion succeeded");

        first = NewRun();runs[id] = first;old = Routine(first);
        runner.ReleaseActor(id);replacement = NewRun();runs[id] = replacement;
        Check(!old.MoveNext() && Current(replacement), "old completion after ReleaseActor removed the replacement");
        Check(!Routine(replacement).MoveNext() && runs.Count == 0, "empty current routine left a running marker");

        System.Collections.IEnumerator OneYield() { yield return null; }
        routines[kind] = (_, _) => OneYield();
        first = NewRun();runs[id] = first;old = Routine(first);
        Check(old.MoveNext() && Current(first), "routine did not enter its child before cancellation");
        runner.Reset();replacement = NewRun();runs[id] = replacement;
        ((IDisposable)old).Dispose();
        Check(Current(replacement), "old suspended iterator disposal removed the replacement");
        var current = Routine(replacement);
        Check(current.MoveNext(), "replacement routine did not start");
        ((IDisposable)current).Dispose();
        Check(runs.Count == 0, "current iterator disposal did not release its own marker");

        bool Matches(object run, CharacterBreakdownKind candidateKind, int generation,
            CharacterActionIntentLease candidateLease) =>
            (bool)runType.GetMethod("Matches", flags).Invoke(run,
                new object[] { null, candidateKind, generation, candidateLease });
        Check(Matches(replacement, kind, 1, lease), "exact run binding did not match");
        Check(!Matches(replacement, CharacterBreakdownKind.Collapse, 1, lease), "different breakdown kind matched");
        Check(!Matches(replacement, kind, 2, lease), "different generation matched");
        Check(!Matches(replacement, kind, 1, new CharacterActionIntentLease(lease.OwnerId, lease.Kind, 2)),
            "different lease epoch matched");
        Check(!Matches(replacement, kind, 1, new CharacterActionIntentLease("other", lease.Kind, 1)),
            "different lease owner matched");
        Check(!Matches(replacement, kind, 1, new CharacterActionIntentLease(lease.OwnerId, CharacterActionIntentKind.None, 1)),
            "different lease kind matched");
        Check(!(bool)type.GetMethod("TryStart", flags).Invoke(runner, new object[] { null, kind, lease })
            && runs.Count == 0, "missing actor was admitted");

        // Exercise the production recursive coroutine gate with the actual run
        // registry. No native actor/Brain/scheduler is involved in this check.
        int steps = 0, disposed = 0;
        System.Collections.IEnumerator Child()
        {
            try { steps++; yield return null; steps++; }
            finally { disposed++; }
        }
        System.Collections.IEnumerator Parent() { yield return Child(); steps += 100; }
        System.Collections.IEnumerator Guard(System.Collections.IEnumerator action, Func<bool> predicate) =>
            (System.Collections.IEnumerator)type.GetMethod("RunWhileCurrent",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { action, predicate });
        first = NewRun();runs[id] = first;
        object guardedRun = first;
        var parent = Guard(Parent(), () => Current(guardedRun));
        Check(parent.MoveNext() && parent.Current is System.Collections.IEnumerator,
            "nested action was not wrapped by the same guard");
        var child = (System.Collections.IEnumerator)parent.Current;
        Check(child.MoveNext() && steps == 1, "current child did not reach its first yield");
        runner.Reset();replacement = NewRun();runs[id] = replacement;
        Check(!child.MoveNext() && steps == 1 && disposed == 1,
            "retired nested child continued or was not disposed");
        Check(!parent.MoveNext() && steps == 1 && Current(replacement),
            "retired parent resumed after the child or removed its replacement");
        steps = 0;disposed = 0;
        var completed = Guard(Child(), () => true);
        Check(completed.MoveNext() && !completed.MoveNext() && steps == 2 && disposed == 1,
            "current guard changed normal child ordering or disposal");
        steps = 0;disposed = 0;
        var cancelled = Guard(Child(), () => true);
        Check(cancelled.MoveNext(), "current guard could not start before explicit cancellation");
        ((IDisposable)cancelled).Dispose();
        Check(steps == 1 && disposed == 1, "explicit guard disposal resumed or leaked its child");
        steps = 0;disposed = 0;
        parent = Guard(Parent(), () => true);
        Check(parent.MoveNext() && ((System.Collections.IEnumerator)parent.Current).MoveNext(),
            "nested guard did not start before parent cancellation");
        ((IDisposable)parent).Dispose();
        Check(steps == 1 && disposed == 1, "parent cancellation did not dispose the suspended nested child");
        runner.Reset();
        return checks;
    }

    internal static int RunBreakdownMovementDisposalContracts()
    {
        var gameObject = new GameObject("Breakdown movement ownership fixture");
        try { return VerifyBreakdownMovementDisposalContracts(gameObject.AddComponent<AbilityMove>()); }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    // Only the actual outer movement iterator is advanced; the inner path/step
    // remains suspended. The managed host supplies a constructor-free shell.
    internal static int VerifyBreakdownMovementDisposalContracts(AbilityMove move)
    {
        int checks = 0;
        void Check(bool condition, string detail) { Require(condition, "breakdown movement disposal: " + detail); checks++; }
        var path = move.MoveByPath(null);
        Check(path.MoveNext() && move.ActiveMovementOperationOwnerForDiagnostics == "raw-path", "path ownership not acquired");
        ((IDisposable)path).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics.Length == 0, "path disposal retained raw-path owner");
        var step = move.MoveByStep(default);
        Check(step.MoveNext() && move.ActiveMovementOperationOwnerForDiagnostics == "raw-step", "step ownership not acquired");
        ((IDisposable)step).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics.Length == 0, "step disposal retained raw-step owner");

        path = move.MoveByPath(null);Require(path.MoveNext(), "old path did not start");
        step = move.MoveByStep(default);Require(step.MoveNext(), "replacement step did not start");
        int replacementVersion = move.MovementOperationVersionForDiagnostics;
        ((IDisposable)path).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics == "raw-step"
            && move.MovementOperationVersionForDiagnostics == replacementVersion,
            "old path disposal cleared a replacement step");
        ((IDisposable)step).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics.Length == 0, "replacement step disposal did not clear itself");

        var first = move.MoveByPath(null);Require(first.MoveNext(), "first path did not start");
        var next = move.MoveByPath(null);Require(next.MoveNext(), "second path did not start");
        replacementVersion = move.MovementOperationVersionForDiagnostics;
        ((IDisposable)first).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics == "raw-path"
            && move.MovementOperationVersionForDiagnostics == replacementVersion,
            "old path disposal cleared a replacement with the same owner label");
        ((IDisposable)next).Dispose();
        Check(move.ActiveMovementOperationOwnerForDiagnostics.Length == 0, "new path could not release itself");
        return checks;
    }

    private static void Require(bool condition, string detail)
    { if (!condition) throw new InvalidOperationException(detail); }
}
#endif
