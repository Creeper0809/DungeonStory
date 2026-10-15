#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Characters;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Detached coverage for visitor population/actor save joins and population
/// restore eligibility. It does not create actors or publish a restored world.
/// </summary>
public static class VisitorPersistenceBindingDebugScenarios
{
    private const string VisitorId = "character:visitor-binding";
    private const string IncidentActorId = "character:incident-owned";
    private const string IdleProfileId = "character:idle-population";
    private const int VisitorDataId = 101;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        DateParseHandling = DateParseHandling.None,
        FloatParseHandling = FloatParseHandling.Double,
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        NullValueHandling = NullValueHandling.Include
    };

    public static bool RunAll(bool log = true)
    {
        try
        {
            VerifyLawfulAndUnownedBindings();
            VerifyMissingAndDuplicateActorRejections();
            VerifyPopulationStateRejections();
            VerifyActorIdentityRejections();
            VerifyRestorePreservesVisitorEligibility();
            VerifyRestoreVisitingPopulationCandidateProjection();
            if (log)
            {
                Debug.Log(
                    "VISITOR_PERSISTENCE_BINDING=PASS; "
                    + "scope=detached-save-join-population-domain-and-candidate-projection; "
                    + "world-publication=not-covered");
            }
            return true;
        }
        catch (Exception exception)
        {
            if (log)
            {
                Debug.LogError(
                    nameof(VisitorPersistenceBindingDebugScenarios)
                    + " failed: " + exception);
            }
            return false;
        }
    }

    private static void VerifyLawfulAndUnownedBindings()
    {
        BindingInput input = CreateValidBinding();
        input.Actors.Add(CreateActor(
            IncidentActorId,
            dataId: 202,
            CharacterType.Customer));
        input.Profiles.Add(CreateProfile(
            IdleProfileId,
            dataId: 303,
            visiting: false));

        RequireVisitorValidation(
            input,
            expectedSuccess: true,
            "lawful visitor plus incident actor and idle profile");
    }

    private static void VerifyMissingAndDuplicateActorRejections()
    {
        BindingInput missingActor = CreateValidBinding();
        missingActor.Actors.Clear();
        RequireVisitorValidation(
            missingActor,
            expectedSuccess: false,
            "visiting profile without actor");

        BindingInput duplicateActor = CreateValidBinding();
        duplicateActor.Actors.Add(CreateActor(
            VisitorId,
            VisitorDataId,
            CharacterType.Customer));
        RequireVisitorValidation(
            duplicateActor,
            expectedSuccess: false,
            "visiting profile with duplicate actors");
    }

    private static void VerifyPopulationStateRejections()
    {
        BindingInput nonVisiting = CreateValidBinding();
        nonVisiting.Profiles[0].isVisiting = false;
        nonVisiting.Profiles[0].settlementStanding =
            CharacterSettlementStanding.PreparedCandidate;
        RequireVisitorValidation(
            nonVisiting,
            expectedSuccess: false,
            "customer actor bound to non-visiting profile");

        BindingInput staff = CreateValidBinding();
        staff.Profiles[0].isStaff = true;
        RequireVisitorValidation(
            staff,
            expectedSuccess: false,
            "visiting staff profile");

        BindingInput dead = CreateValidBinding();
        dead.Profiles[0].isAlive = false;
        RequireVisitorValidation(
            dead,
            expectedSuccess: false,
            "dead visiting profile");
    }

    private static void VerifyActorIdentityRejections()
    {
        BindingInput wrongData = CreateValidBinding();
        wrongData.Actors[0].dataId = VisitorDataId + 1;
        RequireVisitorValidation(
            wrongData,
            expectedSuccess: false,
            "visitor actor data mismatch");

        BindingInput wrongType = CreateValidBinding();
        wrongType.Actors[0].characterType = CharacterType.NPC;
        RequireVisitorValidation(
            wrongType,
            expectedSuccess: false,
            "visitor actor type mismatch");

        BindingInput despawned = CreateValidBinding();
        despawned.Actors[0].lifecycleState = CharacterLifecycleState.Despawned;
        RequireVisitorValidation(
            despawned,
            expectedSuccess: false,
            "despawned visitor actor");
    }

    private static void VerifyRestorePreservesVisitorEligibility()
    {
        FakePopulationProfile source = new()
        {
            PersistentId = "character:population-domain:000001",
            CharacterDataId = VisitorDataId,
            IsAlive = true,
            IsStaff = false,
            IsVisiting = true,
            SettlementStanding = CharacterSettlementStanding.Visitor,
            VisitCount = 4,
            IsReady = true
        };
        byte[] before = SerializeBytes(source);
        CharacterPopulationDomain<FakePopulationProfile> population = new();

        population.Restore(
            new[] { source },
            value => value.Clone(),
            readyTarget: 1,
            readyLowWatermark: 0);

        FakePopulationProfile restored = population.Profiles.Single();
        Require(!ReferenceEquals(source, restored)
                && restored.IsVisiting
                && restored.SettlementStanding
                    == CharacterSettlementStanding.Visitor
                && population.CountAvailableReady() == 0,
            "Population restore cleared or aliased the visiting profile.");
        Require(population.AcquireVisitor(
                    VisitorDataId,
                    Array.Empty<string>()) == null,
            "A restored active visitor was eligible for a duplicate admission.");
        Require(before.SequenceEqual(SerializeBytes(source)),
            "Population restore mutated its serialized profile input.");

        population.ReleaseVisitor(restored);
        Require(!restored.IsVisiting
                && restored.VisitCount == 5
                && population.CountAvailableReady() == 1,
            "Released restored visitor did not return to the ready pool.");
        FakePopulationProfile reacquired = population.AcquireVisitor(
            VisitorDataId,
            Array.Empty<string>());
        Require(ReferenceEquals(restored, reacquired)
                && reacquired.IsVisiting
                && reacquired.IsReady
                && population.CountAvailableReady() == 0,
            "Released ready visitor could not be re-acquired.");
        Require(before.SequenceEqual(SerializeBytes(source)),
            "Release/re-acquire mutated the original restore input.");
    }

    private static void VerifyRestoreVisitingPopulationCandidateProjection()
    {
        IReadOnlyList<CharacterActor> actors = Array.Empty<CharacterActor>();
        IReadOnlyList<HaulDeliveryIntentSaveData> intents =
            Array.Empty<HaulDeliveryIntentSaveData>();
        CharacterId first = new("character:visitor-candidate:first");
        CharacterId second = new("character:visitor-candidate:second");
        CharacterId replacement = new("character:visitor-candidate:replacement");

        RestoreWorldCandidateIndex index = new();
        IRestoreVisitingPopulationCandidateQuery query = index;
        int initialRevision = index.Revision;
        Require(!query.TryGetVisitingPopulationIds(out _),
            "A new candidate index exposed a visiting-population projection.");

        List<CharacterId> source = new() { first, second };
        index.SetCharacterCandidate(actors, intents, source);
        Require(index.Revision == initialRevision + 1,
            "The valid visiting-population projection changed the revision incorrectly.");
        Require(query.TryGetVisitingPopulationIds(
                    out IReadOnlyList<CharacterId> published)
                && published.Count == 2
                && published[0].Equals(first)
                && published[1].Equals(second),
            "The valid visiting-population projection was not published.");

        source[0] = replacement;
        source.Add(replacement);
        Require(published.Count == 2
                && published[0].Equals(first)
                && published[1].Equals(second),
            "The visiting-population projection retained its mutable source list.");
        Require(published is ICollection<CharacterId> collection
                && collection.IsReadOnly
                && Throws<NotSupportedException>(() => collection.Add(replacement))
                && published.Count == 2,
            "The visiting-population projection exposed a mutable collection.");

        index.ClearCharacterCandidate();
        int clearedRevision = index.Revision;
        Require(clearedRevision == initialRevision + 2
                && !index.TryGetCharacters(out _)
                && !index.TryGetHaulDeliveryIntents(out _)
                && !query.TryGetVisitingPopulationIds(out _),
            "Clearing character candidates retained a published projection.");
        index.ClearCharacterCandidate();
        Require(index.Revision == clearedRevision,
            "Clearing an already-empty character candidate changed the revision.");

        RestoreWorldCandidateIndex oneArgument = new();
        oneArgument.SetCharacterCandidate(actors);
        Require(oneArgument.TryGetVisitingPopulationIds(out var oneArgumentIds)
                && oneArgumentIds.Count == 0,
            "The one-argument character candidate overload omitted its empty projection.");

        RestoreWorldCandidateIndex twoArguments = new();
        twoArguments.SetCharacterCandidate(actors, intents);
        Require(twoArguments.TryGetVisitingPopulationIds(out var twoArgumentIds)
                && twoArgumentIds.Count == 0,
            "The two-argument character candidate overload omitted its empty projection.");

        VerifyRejectedProjectionIsAtomic(
            actors,
            intents,
            new[] { first, first },
            typeof(InvalidOperationException),
            first,
            "duplicate visiting-population identity");
        VerifyRejectedProjectionIsAtomic(
            actors,
            intents,
            new[] { new CharacterId("not-a-character-id") },
            typeof(InvalidOperationException),
            first,
            "invalid visiting-population identity");
        VerifyRejectedProjectionIsAtomic(
            actors,
            intents,
            (IReadOnlyList<CharacterId>)null,
            typeof(ArgumentNullException),
            first,
            "null visiting-population identity list");
    }

    private static void VerifyRejectedProjectionIsAtomic(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyList<HaulDeliveryIntentSaveData> intents,
        IReadOnlyList<CharacterId> candidateIds,
        Type expectedException,
        CharacterId recoveryId,
        string scenario)
    {
        RestoreWorldCandidateIndex index = new();
        int revision = index.Revision;
        Require(Throws(
                expectedException,
                () => index.SetCharacterCandidate(
                    actors,
                    intents,
                    candidateIds)),
            scenario + " did not reject with " + expectedException.Name + ".");
        Require(index.Revision == revision
                && !index.TryGetCharacters(out _)
                && !index.TryGetHaulDeliveryIntents(out _)
                && !index.TryGetVisitingPopulationIds(out _),
            scenario + " partially published candidate state.");

        index.SetCharacterCandidate(
            actors,
            intents,
            new[] { recoveryId });
        Require(index.Revision == revision + 1
                && index.TryGetCharacters(out var publishedActors)
                && ReferenceEquals(actors, publishedActors)
                && index.TryGetHaulDeliveryIntents(out var publishedIntents)
                && ReferenceEquals(intents, publishedIntents)
                && index.TryGetVisitingPopulationIds(out var publishedIds)
                && publishedIds.Count == 1
                && publishedIds[0].Equals(recoveryId),
            scenario + " prevented a later valid candidate publication.");
    }

    private static BindingInput CreateValidBinding() => new()
    {
        Actors = new List<DungeonCharacterSaveData>
        {
            CreateActor(
                VisitorId,
                VisitorDataId,
                CharacterType.Customer)
        },
        Profiles = new List<WorldCharacterProfile>
        {
            CreateProfile(
                VisitorId,
                VisitorDataId,
                visiting: true)
        }
    };

    private static DungeonCharacterSaveData CreateActor(
        string persistentId,
        int dataId,
        CharacterType characterType) => new()
    {
        persistentId = persistentId,
        dataId = dataId,
        characterType = characterType,
        lifecycleState = CharacterLifecycleState.Active
    };

    private static WorldCharacterProfile CreateProfile(
        string persistentId,
        int dataId,
        bool visiting) => new()
    {
        persistentId = persistentId,
        characterDataId = dataId,
        displayName = persistentId,
        isStaff = false,
        isAlive = true,
        isVisiting = visiting,
        settlementStanding = visiting
            ? CharacterSettlementStanding.Visitor
            : CharacterSettlementStanding.PreparedCandidate
    };

    private static void RequireVisitorValidation(
        BindingInput input,
        bool expectedSuccess,
        string scenario)
    {
        byte[] before = SerializeBytes(input);
        DungeonGameRestoreReport report = new();

        CharacterWorldSaveValidation.ValidateVisitorBindings(
            input.Actors,
            input.Profiles,
            report);

        Require(report.Success == expectedSuccess,
            scenario + " verdict was " + report.Success
            + "; errors=" + string.Join(" | ", report.Errors));
        Require(before.SequenceEqual(SerializeBytes(input)),
            scenario + " mutated serialized actor/profile input bytes.");
    }

    private static byte[] SerializeBytes(object value) =>
        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
            value,
            Formatting.None,
            JsonSettings));

    private static bool Throws<TException>(Action action)
        where TException : Exception => Throws(typeof(TException), action);

    private static bool Throws(Type expectedException, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception exception)
        {
            return exception.GetType() == expectedException;
        }
    }

    private sealed class BindingInput
    {
        public List<DungeonCharacterSaveData> Actors { get; set; }
        public List<WorldCharacterProfile> Profiles { get; set; }
    }

    private sealed class FakePopulationProfile : ICharacterPopulationProfileState
    {
        public string PersistentId { get; set; }
        public int CharacterDataId { get; set; }
        public bool IsAlive { get; set; }
        public bool IsStaff { get; set; }
        public bool IsVisiting { get; set; }
        public CharacterSettlementStanding SettlementStanding { get; set; }
        public int VisitCount { get; set; }
        public bool IsReady { get; set; }

        public FakePopulationProfile Clone() => new()
        {
            PersistentId = PersistentId,
            CharacterDataId = CharacterDataId,
            IsAlive = IsAlive,
            IsStaff = IsStaff,
            IsVisiting = IsVisiting,
            SettlementStanding = SettlementStanding,
            VisitCount = VisitCount,
            IsReady = IsReady
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
