#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Executes a small, controlled long-horizon story through real gameplay
/// owners. The export is evidence of this path only; it is not natural-play
/// telemetry and is deliberately ineligible for training until reviewed.
/// </summary>
public static class LongHorizonNarrativeExecutionExporter
{
    private const string ExportParent =
        "Artifacts/Exports/LongHorizonNarrativeExecution";
    private const string Schema = "dungeonstory-long-horizon-execution@1";
    private const string ActorIdValue = "character:long-horizon:yeonhwa";
    private const string ActorDisplayName = "연화";
    private const long ProficiencyHour = 24L;

    public static void RunBatchFromEnvironment()
    {
        string version = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_LONG_HORIZON_VERSION");
        string destination = Export(version);
        Debug.Log("LONG_HORIZON_NARRATIVE_EXECUTION=PASS; path=" + destination);
    }

    public static string Export(string version)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            ".."));
        string parent = Path.Combine(
            projectRoot,
            ExportParent.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, canonicalVersion);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new InvalidOperationException(
                "Long-horizon execution export is create-only: " + destination);
        }

        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            JObject execution = ExecuteControlledRun(canonicalVersion);
            string executionPath = Path.Combine(
                staging,
                "long_horizon_execution.json");
            WriteJson(executionPath, execution);

            JObject manifest = new()
            {
                ["schemaVersion"] = 1,
                ["schema"] = Schema,
                ["version"] = canonicalVersion,
                ["gameCommit"] = ReadGit(projectRoot, "rev-parse HEAD"),
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["status"] = (string)execution["status"],
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["actualGameplayProductionEntries"] = true,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["replanRequired"] = (bool)execution["replanRequired"],
                ["checkpointRestoreVerified"] =
                    (bool)execution["checkpoint"]?["verified"],
                ["episodeCount"] = ((JArray)execution["episodes"]).Count,
                ["executionSha256"] = Sha256File(executionPath),
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            string manifestPath = Path.Combine(staging, "manifest.json");
            WriteJson(manifestPath, manifest);

            JObject delivery = new()
            {
                ["schemaVersion"] = 1,
                ["files"] = new JArray(
                    new[] { "long_horizon_execution.json", "manifest.json" }
                        .Select(name => new JObject
                        {
                            ["path"] = name,
                            ["sha256"] = Sha256File(Path.Combine(staging, name))
                        }))
            };
            WriteJson(Path.Combine(staging, "delivery_manifest.json"), delivery);
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            throw;
        }
    }

    private static JObject ExecuteControlledRun(string version)
    {
        string runId = "run:long-horizon:" + version;
        var episodes = new JArray();
        JObject initialState;
        JObject checkpointJson;
        JObject finalState;

        using (var first = LongHorizonSession.Create(runId))
        {
            initialState = first.CaptureState();
            JObject water = ExecuteWaterEpisode(first, initialState, "episode:001");
            episodes.Add(water);

            LongHorizonCheckpoint checkpoint = first.CaptureCheckpoint(
                new[] { (string)water["outcome"]?["outcomeId"] });
            JObject beforeRestore = first.CaptureState();
            string beforeHash = Sha256Json(beforeRestore);

            using LongHorizonSession restored = LongHorizonSession.Restore(
                runId,
                checkpoint);
            JObject afterRestore = restored.CaptureState();
            string afterHash = Sha256Json(afterRestore);
            bool actorPreserved = string.Equals(
                (string)beforeRestore["actor.id"],
                (string)afterRestore["actor.id"],
                StringComparison.Ordinal);
            bool outcomePreserved = checkpoint.OutcomeIds.All(
                restored.HasExactOutcome);
            bool checkpointVerified = JToken.DeepEquals(
                    beforeRestore,
                    afterRestore)
                && string.Equals(beforeHash, afterHash, StringComparison.Ordinal)
                && actorPreserved
                && outcomePreserved;
            Require(checkpointVerified, "Mid-run save/restore changed state or evidence identity.");

            checkpointJson = new JObject
            {
                ["afterEpisodeId"] = "episode:001",
                ["preRestoreState"] = beforeRestore.DeepClone(),
                ["postRestoreState"] = afterRestore.DeepClone(),
                ["preRestoreStateSha256"] = beforeHash,
                ["postRestoreStateSha256"] = afterHash,
                ["actorIdPreserved"] = actorPreserved,
                ["outcomeIdsPreserved"] = new JArray(checkpoint.OutcomeIds),
                ["verified"] = checkpointVerified
            };

            JObject rest = ExecuteRestEpisode(
                restored,
                afterRestore,
                water,
                "episode:002");
            episodes.Add(rest);
            JObject training = ExecuteTrainingEpisode(
                restored,
                (JObject)rest["postState"],
                rest,
                "episode:003");
            episodes.Add(training);
            finalState = restored.CaptureState();
            Require(
                JToken.DeepEquals(finalState, training["postState"]),
                "Final state diverged from the last observed episode state.");
        }

        JObject divergenceProbe = ExecuteDivergenceProbe(version);
        return new JObject
        {
            ["schemaVersion"] = 1,
            ["schema"] = Schema,
            ["runId"] = runId,
            ["character"] = new JObject
            {
                ["id"] = ActorIdValue,
                ["displayName"] = ActorDisplayName
            },
            ["status"] = "completed",
            ["replanRequired"] = false,
            ["failureReason"] = string.Empty,
            ["initialState"] = initialState,
            ["episodes"] = episodes,
            ["checkpoint"] = checkpointJson,
            ["finalState"] = finalState,
            ["divergenceProbe"] = divergenceProbe
        };
    }

    private static JObject ExecuteWaterEpisode(
        LongHorizonSession session,
        JObject before,
        string episodeId)
    {
        double thirst = Number(before, "actor.thirst");
        Require(thirst <= 20d, "Water episode decision guard did not match.");
        Require(
            session.ConsumeCleanWater(),
            "The clean-water production entry rejected the controlled action.");
        JObject after = session.CaptureState();
        GameplayOutcomeReplayIdentity outcome = session.RequireOutcome(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
        var mismatches = new JArray();
        CompareGreater(after, before, "actor.thirst", mismatches);
        CompareLess(after, before, "world.cleanWaterRemaining", mismatches);
        return Episode(
            episodeId,
            1,
            "갈증을 씻은 아침",
            "action:drink-clean-water",
            "CharacterWaterConsumptionCoordinator.TryConsumeWorldSource",
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            before,
            after,
            new JArray
            {
                Guard("initial", "actor.thirst", "<=", 20d, thirst)
            },
            new JArray
            {
                Predecessor(
                    "initial",
                    "observed-state",
                    "actor.thirst",
                    thirst)
            },
            outcome,
            mismatches);
    }

    private static JObject ExecuteRestEpisode(
        LongHorizonSession session,
        JObject before,
        JObject waterEpisode,
        string episodeId)
    {
        double sleep = Number(before, "actor.sleep");
        string waterOutcomeId = (string)waterEpisode["outcome"]?["outcomeId"];
        Require(sleep <= 20d, "Rest episode decision guard did not match.");
        Require(session.HasExactOutcome(waterOutcomeId),
            "Rest decision lost its prior committed water evidence.");
        session.Rest();
        JObject after = session.CaptureState();
        GameplayOutcomeReplayIdentity outcome = session.RequireOutcome(
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent);
        var mismatches = new JArray();
        CompareGreater(after, before, "actor.sleep", mismatches);
        return Episode(
            episodeId,
            2,
            "숨을 고른 낮",
            "action:rest",
            "CharacterStats.RecoverNeed",
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
            before,
            after,
            new JArray
            {
                Guard("episode:001", "outcomeId", "==", waterOutcomeId, waterOutcomeId),
                Guard("episode:001", "actor.sleep", "<=", 20d, sleep)
            },
            new JArray
            {
                Predecessor(
                    "episode:001",
                    "committed-outcome",
                    "outcomeId",
                    waterOutcomeId),
                Predecessor(
                    "episode:001",
                    "observed-state",
                    "actor.sleep",
                    sleep)
            },
            outcome,
            mismatches);
    }

    private static JObject ExecuteTrainingEpisode(
        LongHorizonSession session,
        JObject before,
        JObject restEpisode,
        string episodeId)
    {
        double sleep = Number(before, "actor.sleep");
        string restOutcomeId = (string)restEpisode["outcome"]?["outcomeId"];
        Require(sleep >= 30d, "Training episode decision guard did not match.");
        Require(session.HasExactOutcome(restOutcomeId),
            "Training decision lost its prior committed rest evidence.");
        session.Train();
        JObject after = session.CaptureState();
        GameplayOutcomeReplayIdentity outcome = session.RequireOutcome(
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt);
        var mismatches = new JArray();
        CompareGreater(
            after,
            before,
            "actor.proficiencyMilliExperience",
            mismatches);
        return Episode(
            episodeId,
            3,
            "회복 뒤의 첫 숙련",
            "action:train-proficiency",
            "CharacterNarrativeRuntime.AddDirectExperience",
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            before,
            after,
            new JArray
            {
                Guard("episode:002", "outcomeId", "==", restOutcomeId, restOutcomeId),
                Guard("episode:002", "actor.sleep", ">=", 30d, sleep)
            },
            new JArray
            {
                Predecessor(
                    "episode:002",
                    "committed-outcome",
                    "outcomeId",
                    restOutcomeId),
                Predecessor(
                    "episode:002",
                    "observed-state",
                    "actor.sleep",
                    sleep)
            },
            outcome,
            mismatches);
    }

    private static JObject ExecuteDivergenceProbe(string version)
    {
        string runId = "run:long-horizon-divergence:" + version;
        using LongHorizonSession session = LongHorizonSession.Create(runId);
        JObject before = session.CaptureState();
        Require(
            session.ConsumeCleanWater(),
            "Divergence probe could not execute its first production entry.");
        JObject after = session.CaptureState();
        GameplayOutcomeReplayIdentity outcome = session.RequireOutcome(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
        var mismatches = new JArray();
        if (Number(after, "actor.thirst") < 999d)
        {
            mismatches.Add(new JObject
            {
                ["key"] = "actor.thirst",
                ["expected"] = ">=999",
                ["actual"] = Number(after, "actor.thirst")
            });
        }
        Require(mismatches.Count == 1,
            "The divergence probe no longer exercises the replan boundary.");
        JObject row = Episode(
            "probe:episode:001",
            1,
            "의도적 불일치 감지",
            "action:drink-clean-water",
            "CharacterWaterConsumptionCoordinator.TryConsumeWorldSource",
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            before,
            after,
            new JArray
            {
                Guard(
                    "initial",
                    "actor.thirst",
                    "<=",
                    20d,
                    Number(before, "actor.thirst"))
            },
            new JArray
            {
                Predecessor(
                    "initial",
                    "observed-state",
                    "actor.thirst",
                    Number(before, "actor.thirst"))
            },
            outcome,
            mismatches);
        return new JObject
        {
            ["status"] = "replan-required",
            ["replanRequired"] = true,
            ["failureReason"] = "planned-vs-actual-mismatch",
            ["executedEpisodeCount"] = 1,
            ["executedEpisodes"] = new JArray(row),
            ["skippedEpisodeIds"] = new JArray(
                "probe:episode:002",
                "probe:episode:003")
        };
    }

    private static JObject Episode(
        string episodeId,
        int sequence,
        string title,
        string actionId,
        string productionEntry,
        MigratedProducerOutcomeKind kind,
        JObject before,
        JObject after,
        JArray guardFacts,
        JArray predecessors,
        GameplayOutcomeReplayIdentity outcome,
        JArray mismatches)
    {
        var deltas = new JArray();
        foreach (string key in new[]
        {
            "actor.thirst",
            "actor.sleep",
            "world.cleanWaterRemaining",
            "actor.proficiencyMilliExperience"
        })
        {
            if (!JToken.DeepEquals(before[key], after[key]))
            {
                deltas.Add(new JObject
                {
                    ["key"] = key,
                    ["before"] = before[key]?.DeepClone(),
                    ["after"] = after[key]?.DeepClone()
                });
            }
        }

        return new JObject
        {
            ["episodeId"] = episodeId,
            ["sequence"] = sequence,
            ["title"] = title,
            ["actionId"] = actionId,
            ["productionEntry"] = productionEntry,
            ["producerKind"] = kind.ToString(),
            ["decisionGuard"] = new JObject
            {
                ["matched"] = true,
                ["facts"] = guardFacts
            },
            ["predecessors"] = predecessors,
            ["preState"] = before.DeepClone(),
            ["postState"] = after.DeepClone(),
            ["deltas"] = deltas,
            ["outcome"] = new JObject
            {
                ["committed"] = true,
                ["kind"] = kind.ToString(),
                ["resultKey"] = outcome.ResultKey.ToString(),
                ["outcomeId"] = outcome.OutcomeId.ToString(),
                ["replayState"] = outcome.State.ToString(),
                ["canonicalPayloadSha256"] = outcome.CanonicalPayloadHash
            },
            ["plannedVsActual"] = new JObject
            {
                ["matched"] = mismatches.Count == 0,
                ["mismatches"] = mismatches
            }
        };
    }

    private static JObject Guard(
        string sourceEpisodeId,
        string key,
        string op,
        object expected,
        object observed) => new()
    {
        ["sourceEpisodeId"] = sourceEpisodeId,
        ["key"] = key,
        ["operator"] = op,
        ["expected"] = expected == null ? JValue.CreateNull() : JToken.FromObject(expected),
        ["observed"] = observed == null ? JValue.CreateNull() : JToken.FromObject(observed)
    };

    private static JObject Predecessor(
        string sourceEpisodeId,
        string relation,
        string key,
        object observed) => new()
    {
        ["sourceEpisodeId"] = sourceEpisodeId,
        ["relation"] = relation,
        ["key"] = key,
        ["observedValue"] = observed == null
            ? JValue.CreateNull()
            : JToken.FromObject(observed)
    };

    private static void CompareGreater(
        JObject after,
        JObject before,
        string key,
        JArray mismatches)
    {
        if (Number(after, key) <= Number(before, key))
        {
            mismatches.Add(new JObject
            {
                ["key"] = key,
                ["expected"] = "increase",
                ["before"] = Number(before, key),
                ["after"] = Number(after, key)
            });
        }
    }

    private static void CompareLess(
        JObject after,
        JObject before,
        string key,
        JArray mismatches)
    {
        if (Number(after, key) >= Number(before, key))
        {
            mismatches.Add(new JObject
            {
                ["key"] = key,
                ["expected"] = "decrease",
                ["before"] = Number(before, key),
                ["after"] = Number(after, key)
            });
        }
    }

    private static double Number(JObject state, string key) =>
        (double)(state?[key]
            ?? throw new InvalidOperationException(
                "Observed state is missing key '" + key + "'."));

    private static IEnumerable<JObject> SourceFiles(string projectRoot)
    {
        foreach (string relative in new[]
        {
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/LongHorizonNarrativeExecutionExporter.cs",
            "Assets/Scripts/Services/Survival/CharacterSafeReliefRunner.cs",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "Assets/Scripts/Services/Character/CharacterNarrativeRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/MigratedProducerOutcomeRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomePersistence.cs"
        })
        {
            string absolute = Path.Combine(
                projectRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
            yield return new JObject
            {
                ["path"] = relative,
                ["sha256"] = Sha256File(absolute)
            };
        }
    }

    private static string RequireVersion(string value)
    {
        string version = value?.Trim() ?? string.Empty;
        if (version.Length == 0 || version.Length > 96)
            throw new ArgumentException("A bounded export version is required.");
        for (int index = 0; index < version.Length; index++)
        {
            char current = version[index];
            if (!(char.IsLetterOrDigit(current)
                || current == '-'
                || current == '_'
                || current == '.'))
            {
                throw new ArgumentException(
                    "Export version contains an unsupported character.");
            }
        }
        return version;
    }

    private static string ReadGit(string projectRoot, string arguments)
    {
        var start = new DiagnosticsProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using DiagnosticsProcess process = DiagnosticsProcess.Start(start)
            ?? throw new InvalidOperationException("Failed to start git.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git failed: " + error);
        return output;
    }

    private static void WriteJson(string path, JToken value) =>
        File.WriteAllText(
            path,
            value.ToString(Formatting.Indented) + "\n",
            new UTF8Encoding(false));

    private static string Sha256Json(JToken value) =>
        Sha256Bytes(Encoding.UTF8.GetBytes(value.ToString(Formatting.None)));

    private static string Sha256File(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Hash source is missing.", path);
        using FileStream stream = File.OpenRead(path);
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(stream)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string Sha256Bytes(byte[] bytes)
    {
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(bytes)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal sealed class LongHorizonSession : IDisposable
    {
        private readonly CharacterSO actorData;
        private readonly CharacterProficiencyId proficiencyId;
        private readonly CharacterNarrativeRuntime narrative;
        private readonly DomainFactDispatcher proficiencyFacts;
        private readonly DomainFactDispatcher restFacts;
        private readonly DomainFactDispatcher waterFacts;
        private readonly MutableLongHorizonWaterQuery water;
        private readonly CharacterWaterConsumptionCoordinator waterConsumption;
        private readonly string actorIdValue;
        private readonly string actorDisplayName;
        private readonly string actorSpeciesId;

        private LongHorizonSession(
            string runId,
            LongHorizonCheckpoint checkpoint,
            string actorId = ActorIdValue,
            string displayName = ActorDisplayName,
            string speciesId = "Orc")
        {
            actorIdValue = CausalGameplaySyntax.RequireId(
                actorId,
                nameof(actorId));
            actorDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(
                    "Long-horizon actor display name is required.",
                    nameof(displayName))
                : displayName.Trim();
            actorSpeciesId = CausalGameplaySyntax.RequireId(
                speciesId,
                nameof(speciesId));
            Outcomes = new MigratedProducerOutcomeEditorFixture(runId);
            if (checkpoint != null)
            {
                GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
                    Outcomes.Ledger.PrepareGameplayOutcomeRestore(
                        checkpoint.OutcomeLedger);
                Outcomes.Ledger.BeginRestoreCandidate();
                Outcomes.Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
                Outcomes.Ledger.PublishRestoreCandidate();
                Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(
                    Outcomes.Transaction.PrepareRestore(
                        checkpoint.MigratedOutcomes));
            }
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder);
            proficiencyFacts = Outcomes.CreateProficiencyAwardFacts(Probe);
            restFacts = Outcomes.CreateCharacterRestFacts(Probe);

            GameObject actorObject = new("Long Horizon Narrative Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                actorDisplayName,
                actorSpeciesId);
            Actor.Initialization(actorData);
            Actor.Identity.SetPersistentId(actorIdValue);
            CharacterAiEditorTestDependencies
                .InjectCharacterStatsOutcomeForDiagnostics(
                    Actor.Stats,
                    restFacts,
                    restFacts);

            water = new MutableLongHorizonWaterQuery();
            if (checkpoint != null)
            {
                water.RestoreWaterSources(
                    checkpoint.WaterSources,
                    checkpoint.NextWaterSequence);
            }
            WorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    out _,
                    out _,
                    out _,
                    out IReservedItemTransferService reservedTransfers);
            var waterCommits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            waterFacts = new DomainFactDispatcher(
                new IDomainFactObserverBinding[]
                {
                    new CharacterWaterFactBinding(
                        new CharacterWaterFactObserver(
                            Outcomes.Transaction,
                            waterCommits))
                });
            var waterPublisher = new CharacterWaterFactPublisherProbe(
                waterFacts,
                Probe);
            var waterPublications = new CharacterWaterPublicationOutbox(
                new DungeonStory.Foundation.UnityUiClock(),
                new CharacterDeprivationStateStore(Outcomes.AggregateRootStore),
                waterPublisher,
                waterPublisher,
                new GameEventBus());
            waterConsumption = new CharacterWaterConsumptionCoordinator(
                itemStacks,
                reservedTransfers,
                water,
                DefaultCharacterNeedBalanceRuntime.Instance,
                EditorFixedGameCalendar.Instance,
                waterPublications);

            var content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new CharacterNarrativeCatalog(content);
            narrative = new CharacterNarrativeRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                content: content,
                domainFacts: proficiencyFacts);
            proficiencyId = BuiltInCharacterProficiencyIds.All[0];
            if (checkpoint == null)
            {
                Actor.stats[CharacterCondition.THIRST] = 15f;
                Actor.stats[CharacterCondition.SLEEP] = 20f;
                IReadOnlyList<CharacterStartingProficiencyExperience> starts =
                    CharacterStartingProficiencyRules.Create(7104);
                CharacterStartingProficiencyExperience awarded = starts.Single(
                    value => string.Equals(
                        value.proficiencyId,
                        proficiencyId.Value,
                        StringComparison.Ordinal));
                awarded.experience = 99;
                string registrationSpeciesId = catalog.Cultures[0].defaultSpeciesId;
                narrative.Register(
                    ActorId,
                    new CharacterSpeciesId(registrationSpeciesId),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    starts);
            }
            else
            {
                Actor.Stats.RestorePersistentState(
                    checkpoint.Stats,
                    checkpoint.CurrentHealth,
                    checkpoint.InjurySeverity,
                    checkpoint.BaseMood,
                    checkpoint.MoodFactors);
                narrative.PublishRestore(
                    narrative.PrepareRestore(checkpoint.Narrative));
            }
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal CharacterActor Actor { get; }
        internal CharacterId ActorId => new(actorIdValue);

        internal static LongHorizonSession Create(string runId) =>
            new(runId, null);

        internal static LongHorizonSession Create(
            string runId,
            string actorId,
            string displayName,
            string speciesId) =>
            new(runId, null, actorId, displayName, speciesId);

        internal static LongHorizonSession Restore(
            string runId,
            LongHorizonCheckpoint checkpoint) =>
            new(runId, checkpoint
                ?? throw new ArgumentNullException(nameof(checkpoint)));

        internal static LongHorizonSession Restore(
            string runId,
            LongHorizonCheckpoint checkpoint,
            string actorId,
            string displayName,
            string speciesId) =>
            new(
                runId,
                checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)),
                actorId,
                displayName,
                speciesId);

        internal bool ConsumeCleanWater() =>
            waterConsumption.TryConsumeWorldSource(Actor, water.SourceId);

        internal void Rest() => Actor.Stats.RecoverNeed(
            CharacterCondition.SLEEP,
            10f,
            CharacterNeedRecoverySource.Rest,
            Array.Empty<string>());

        internal void Train() => narrative.AddDirectExperience(
            ActorId,
            proficiencyId,
            1f,
            ProficiencyHour,
            applyLearningMultiplier: true);

        internal JObject CaptureState()
        {
            Require(
                narrative.TryGetProficiency(
                    ActorId,
                    proficiencyId,
                    ProficiencyHour,
                    out CharacterProficiencySnapshot proficiency),
                "Long-horizon proficiency state is unavailable.");
            return new JObject
            {
                ["actor.id"] = actorIdValue,
                ["actor.thirst"] = Actor.Stats.GetConditionValue(
                    CharacterCondition.THIRST,
                    0f),
                ["actor.sleep"] = Actor.Stats.GetConditionValue(
                    CharacterCondition.SLEEP,
                    0f),
                ["world.cleanWaterRemaining"] = water.Remaining,
                ["actor.proficiencyMilliExperience"] =
                    proficiency.CurrentMilliExperience
            };
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind) =>
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                Outcomes,
                kind,
                Probe);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                Outcomes.Ledger,
                identity);

        internal bool HasExactOutcome(string outcomeId)
        {
            if (!TryParseOutcomeId(outcomeId, out GameplayOutcomeId parsed))
                return false;
            return Outcomes.Ledger.TryGetExact(parsed, out _);
        }

        internal LongHorizonCheckpoint CaptureCheckpoint(
            IEnumerable<string> outcomeIds)
        {
            CharacterMoodSnapshot mood = Actor.Stats.GetMoodSnapshot();
            return new LongHorizonCheckpoint(
                Actor.Stats.StatSnapshot.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value),
                Actor.Stats.CurrentHealth,
                Actor.Stats.InjurySeverity,
                mood.BaseValue,
                mood.Factors
                    .Where(value => value != null
                        && value.Kind == CharacterMoodFactorKind.Interaction)
                    .Select(CloneMoodFactor)
                    .ToArray(),
                water.CaptureWaterSources(),
                water.NextWaterSequence,
                narrative.Capture(),
                Outcomes.Ledger.CaptureGameplayOutcomes(),
                Outcomes.Transaction.Capture(),
                outcomeIds?.Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray() ?? Array.Empty<string>());
        }

        public void Dispose()
        {
            waterFacts?.Dispose();
            restFacts?.Dispose();
            proficiencyFacts?.Dispose();
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            if (Actor != null)
                UnityEngine.Object.DestroyImmediate(Actor.gameObject);
        }

        private static CharacterMoodFactorSnapshot CloneMoodFactor(
            CharacterMoodFactorSnapshot source) => new(
            source.Id,
            source.Label,
            source.Value,
            source.Kind,
            source.RemainingSeconds);
    }

    internal sealed class LongHorizonCheckpoint
    {
        internal LongHorizonCheckpoint(
            IReadOnlyDictionary<CharacterCondition, float> stats,
            float currentHealth,
            float injurySeverity,
            float baseMood,
            IReadOnlyList<CharacterMoodFactorSnapshot> moodFactors,
            IReadOnlyList<WorldWaterSourceSaveData> waterSources,
            int nextWaterSequence,
            CharacterNarrativeWorldSaveData narrative,
            GameplayOutcomeLedgerSaveData outcomeLedger,
            DungeonMigratedProducerOutcomeSaveData migratedOutcomes,
            IReadOnlyList<string> outcomeIds)
        {
            Stats = stats;
            CurrentHealth = currentHealth;
            InjurySeverity = injurySeverity;
            BaseMood = baseMood;
            MoodFactors = moodFactors;
            WaterSources = waterSources;
            NextWaterSequence = nextWaterSequence;
            Narrative = narrative;
            OutcomeLedger = outcomeLedger;
            MigratedOutcomes = migratedOutcomes;
            OutcomeIds = outcomeIds;
        }

        internal IReadOnlyDictionary<CharacterCondition, float> Stats { get; }
        internal float CurrentHealth { get; }
        internal float InjurySeverity { get; }
        internal float BaseMood { get; }
        internal IReadOnlyList<CharacterMoodFactorSnapshot> MoodFactors { get; }
        internal IReadOnlyList<WorldWaterSourceSaveData> WaterSources { get; }
        internal int NextWaterSequence { get; }
        internal CharacterNarrativeWorldSaveData Narrative { get; }
        internal GameplayOutcomeLedgerSaveData OutcomeLedger { get; }
        internal DungeonMigratedProducerOutcomeSaveData MigratedOutcomes { get; }
        internal IReadOnlyList<string> OutcomeIds { get; }
    }

    private sealed class MutableLongHorizonWaterQuery : IWorldWaterQuery
    {
        private WorldWaterSourceSaveData state = new()
        {
            sourceId = "water:long-horizon:clean",
            gridX = 0,
            gridY = 0,
            terrainType = GridCellTerrainType.ShallowWater,
            quality = WorldWaterQuality.Clean,
            capacity = 10f,
            remaining = 10f,
            regenerationPerSecond = 0f,
            pathogenDiseaseId = string.Empty
        };

        internal string SourceId => state.sourceId;
        internal float Remaining => state.remaining;
        public int NextWaterSequence { get; private set; } = 2;

        public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources() =>
            new[] { Snapshot() };

        public bool TryGetSource(
            string sourceId,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return string.Equals(state.sourceId, sourceId, StringComparison.Ordinal);
        }

        public bool TryFindDrinkSource(
            Vector2Int origin,
            bool allowFoul,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return source.CanDrink;
        }

        public bool TryDrink(
            string sourceId,
            float amount,
            out WorldWaterQuality quality,
            out float consumed)
        {
            quality = state.quality;
            consumed = 0f;
            if (!string.Equals(state.sourceId, sourceId, StringComparison.Ordinal))
                return false;
            consumed = Mathf.Min(Mathf.Max(0f, amount), state.remaining);
            state.remaining -= consumed;
            return consumed > 0f;
        }

        public List<WorldWaterSourceSaveData> CaptureWaterSources() => new()
        {
            Clone(state)
        };

        public void RestoreWaterSources(
            IEnumerable<WorldWaterSourceSaveData> saveData,
            int nextSequence)
        {
            state = Clone(saveData?.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Long-horizon water checkpoint is empty."));
            NextWaterSequence = nextSequence;
        }

        public bool DebugCreateSource(
            Vector2Int position,
            WorldWaterQuality quality,
            float capacity,
            GridCellTerrainType terrainType,
            out string sourceId)
        {
            sourceId = string.Empty;
            return false;
        }

        public bool DebugSetSource(
            string sourceId,
            WorldWaterQuality quality,
            float capacity,
            float remaining) => false;

        private WorldWaterSourceSnapshot Snapshot() => new(
            state.sourceId,
            new Vector2Int(state.gridX, state.gridY),
            state.terrainType,
            state.quality,
            state.capacity,
            state.remaining,
            state.regenerationPerSecond,
            state.pathogenDiseaseId);

        private static WorldWaterSourceSaveData Clone(
            WorldWaterSourceSaveData source) => new()
        {
            sourceId = source.sourceId,
            gridX = source.gridX,
            gridY = source.gridY,
            terrainType = source.terrainType,
            quality = source.quality,
            capacity = source.capacity,
            remaining = source.remaining,
            regenerationPerSecond = source.regenerationPerSecond,
            pathogenDiseaseId = source.pathogenDiseaseId
        };
    }

    private static bool TryParseOutcomeId(
        string value,
        out GameplayOutcomeId outcomeId)
    {
        outcomeId = default;
        int separator = value?.LastIndexOf('/') ?? -1;
        if (separator <= 0
            || separator >= value.Length - 1
            || !long.TryParse(
                value.Substring(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long sequence)
            || sequence <= 0L)
        {
            return false;
        }
        try
        {
            outcomeId = new GameplayOutcomeId(
                new GameplayOutcomeRunId(value.Substring(0, separator)),
                sequence);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
#endif
