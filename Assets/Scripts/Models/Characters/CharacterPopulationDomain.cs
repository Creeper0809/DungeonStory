using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Characters
{
    public interface ICharacterPopulationProfileState
    {
        string PersistentId { get; }
        int CharacterDataId { get; }
        bool IsAlive { get; set; }
        bool IsStaff { get; set; }
        bool IsVisiting { get; set; }
        int VisitCount { get; set; }
        bool IsReady { get; }
    }

    public sealed class CharacterPopulationDomain<TProfile>
        where TProfile : class, ICharacterPopulationProfileState
    {
        private readonly List<TProfile> profiles = new();
        private int creationSerial;
        private bool reachedReadyTarget;
        private bool replenishing;

        public IReadOnlyList<TProfile> Profiles => profiles;
        public int CreationSerial => creationSerial;

        public TProfile AcquireVisitor(
            int characterDataId,
            IEnumerable<string> unavailableProfileIds)
        {
            HashSet<string> unavailable = new(
                unavailableProfileIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            TProfile returning = profiles
                .Where(profile => profile != null
                    && profile.IsAlive
                    && !profile.IsStaff
                    && !profile.IsVisiting
                    && profile.IsReady
                    && !unavailable.Contains(profile.PersistentId)
                    && profile.CharacterDataId == characterDataId)
                .OrderBy(profile => profile.VisitCount)
                .ThenBy(profile => profile.PersistentId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (returning != null)
            {
                returning.IsVisiting = true;
            }

            return returning;
        }

        public void Add(TProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            profiles.Add(profile);
        }

        public bool TryGet(string persistentId, out TProfile profile)
        {
            profile = profiles.FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.PersistentId,
                    persistentId,
                    StringComparison.Ordinal));
            return profile != null;
        }

        public void ReleaseVisitor(TProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.IsVisiting = false;
            if (!profile.IsStaff)
            {
                profile.VisitCount++;
            }
        }

        public void PromoteToStaff(TProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.IsStaff = true;
            profile.IsVisiting = false;
        }

        public List<TProfile> Capture(Func<TProfile, TProfile> clone)
        {
            if (clone == null)
            {
                throw new ArgumentNullException(nameof(clone));
            }

            return profiles
                .Where(profile => profile != null)
                .Select(clone)
                .ToList();
        }

        public void Restore(
            IEnumerable<TProfile> restoredProfiles,
            Func<TProfile, TProfile> clone,
            int readyTarget,
            int readyLowWatermark)
        {
            if (clone == null)
            {
                throw new ArgumentNullException(nameof(clone));
            }

            List<TProfile> restored = restoredProfiles?
                .Where(profile => profile != null)
                .Select(clone)
                .ToList() ?? new List<TProfile>();
            if (restored.Any(profile => string.IsNullOrWhiteSpace(profile.PersistentId)))
            {
                throw new InvalidOperationException(
                    "A world character profile is missing its persistent ID.");
            }

            string duplicateId = restored
                .GroupBy(profile => profile.PersistentId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(duplicateId))
            {
                throw new InvalidOperationException(
                    $"Duplicate world character profile ID '{duplicateId}' cannot be restored.");
            }

            profiles.Clear();
            profiles.AddRange(restored);
            foreach (TProfile profile in profiles)
            {
                profile.IsVisiting = false;
            }

            creationSerial = profiles
                .Select(profile => ParseSerial(profile.PersistentId))
                .DefaultIfEmpty(0)
                .Max();
            int availableReady = CountAvailableReady();
            reachedReadyTarget = availableReady >= readyTarget;
            replenishing = reachedReadyTarget
                && availableReady <= readyLowWatermark;
        }

        public string NextPersistentId(int runSeed)
        {
            creationSerial++;
            return $"world:{runSeed}:{creationSerial:D6}";
        }

        public bool ShouldReplenish(int target, int lowWatermark)
        {
            int availableReady = CountAvailableReady();
            if (!reachedReadyTarget)
            {
                replenishing = true;
            }
            else if (!replenishing && availableReady <= lowWatermark)
            {
                replenishing = true;
            }

            return replenishing;
        }

        public void CompleteReplenishment(int target)
        {
            if (CountAvailableReady() < target)
            {
                return;
            }

            reachedReadyTarget = true;
            replenishing = false;
        }

        public int CountAvailableReady() => profiles.Count(profile => profile != null
            && profile.IsAlive
            && !profile.IsStaff
            && !profile.IsVisiting
            && profile.IsReady);

        public int CountAvailableOrQueued() => profiles.Count(profile => profile != null
            && profile.IsAlive
            && !profile.IsStaff
            && !profile.IsVisiting);

        public int CountAliveNonStaff() => profiles.Count(profile => profile != null
            && profile.IsAlive
            && !profile.IsStaff);

        public TProfile FindNextPreparation(ICollection<string> pendingIds) =>
            profiles.FirstOrDefault(profile => profile != null
                && profile.IsAlive
                && !profile.IsStaff
                && !profile.IsVisiting
                && !profile.IsReady
                && (pendingIds == null || !pendingIds.Contains(profile.PersistentId)));

        private static int ParseSerial(string persistentId)
        {
            string suffix = persistentId?.Split(':').LastOrDefault();
            return int.TryParse(suffix, out int serial) ? serial : 0;
        }
    }

    public sealed class CharacterPopulationAggregate<TProfile, TActor, TPreparation>
        where TProfile : class, ICharacterPopulationProfileState
        where TActor : class
        where TPreparation : class
    {
        private CharacterPopulationDomain<TProfile> population = new();
        private Dictionary<TActor, TProfile> actors = new();
        private Dictionary<string, TPreparation> preparations =
            new(StringComparer.Ordinal);

        public CharacterPopulationDomain<TProfile> Population => population;
        public Dictionary<TActor, TProfile> Actors => actors;
        public Dictionary<string, TPreparation> Preparations => preparations;

        public CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation>
            BuildRestore(CharacterPopulationDomain<TProfile> restoredPopulation)
        {
            return new CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation>(
                this,
                restoredPopulation ?? throw new ArgumentNullException(nameof(restoredPopulation)),
                population,
                actors,
                preparations);
        }

        public void Apply(
            CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation> restore)
        {
            restore = RequireRestore(restore);
            restore.RequirePrepared(population, actors, preparations);
            population = restore.RestoredPopulation;
            actors = new Dictionary<TActor, TProfile>();
            preparations = new Dictionary<string, TPreparation>(StringComparer.Ordinal);
            restore.MarkApplied(population, actors, preparations);
        }

        public void Rollback(
            CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation> restore,
            Action<IReadOnlyCollection<TPreparation>> retirePreparations)
        {
            restore = RequireRestore(restore);
            restore.RequireApplied(population, actors, preparations);
            Dictionary<string, TPreparation> discardedPreparations = preparations;
            population = restore.PreviousPopulation;
            actors = restore.PreviousActors;
            preparations = restore.PreviousPreparations;
            retirePreparations?.Invoke(discardedPreparations.Values);
            discardedPreparations.Clear();
            restore.MarkFinished();
        }

        public void Complete(
            CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation> restore,
            Action<IReadOnlyCollection<TPreparation>> retirePreparations)
        {
            restore = RequireRestore(restore);
            restore.RequireApplied(population, actors, preparations);
            retirePreparations?.Invoke(restore.PreviousPreparations.Values);
            restore.PreviousPreparations.Clear();
            restore.MarkFinished();
        }

        private CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation>
            RequireRestore(
                CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation> restore)
        {
            if (restore == null || !restore.IsOwnedBy(this))
            {
                throw new InvalidOperationException(
                    "Character population aggregate restore has the wrong owner.");
            }

            return restore;
        }
    }

    public sealed class CharacterPopulationAggregateRestore<TProfile, TActor, TPreparation>
        where TProfile : class, ICharacterPopulationProfileState
        where TActor : class
        where TPreparation : class
    {
        private readonly CharacterPopulationAggregate<TProfile, TActor, TPreparation> owner;
        private CharacterPopulationDomain<TProfile> appliedPopulation;
        private Dictionary<TActor, TProfile> appliedActors;
        private Dictionary<string, TPreparation> appliedPreparations;
        private bool applied;
        private bool finished;

        internal CharacterPopulationAggregateRestore(
            CharacterPopulationAggregate<TProfile, TActor, TPreparation> owner,
            CharacterPopulationDomain<TProfile> restoredPopulation,
            CharacterPopulationDomain<TProfile> previousPopulation,
            Dictionary<TActor, TProfile> previousActors,
            Dictionary<string, TPreparation> previousPreparations)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            RestoredPopulation = restoredPopulation
                ?? throw new ArgumentNullException(nameof(restoredPopulation));
            PreviousPopulation = previousPopulation
                ?? throw new ArgumentNullException(nameof(previousPopulation));
            PreviousActors = previousActors
                ?? throw new ArgumentNullException(nameof(previousActors));
            PreviousPreparations = previousPreparations
                ?? throw new ArgumentNullException(nameof(previousPreparations));
        }

        internal CharacterPopulationDomain<TProfile> RestoredPopulation { get; }
        internal CharacterPopulationDomain<TProfile> PreviousPopulation { get; }
        internal Dictionary<TActor, TProfile> PreviousActors { get; }
        internal Dictionary<string, TPreparation> PreviousPreparations { get; }

        internal bool IsOwnedBy(
            CharacterPopulationAggregate<TProfile, TActor, TPreparation> expectedOwner) =>
            ReferenceEquals(owner, expectedOwner);

        internal void RequirePrepared(
            CharacterPopulationDomain<TProfile> currentPopulation,
            Dictionary<TActor, TProfile> currentActors,
            Dictionary<string, TPreparation> currentPreparations)
        {
            if (applied
                || finished
                || !ReferenceEquals(PreviousPopulation, currentPopulation)
                || !ReferenceEquals(PreviousActors, currentActors)
                || !ReferenceEquals(PreviousPreparations, currentPreparations))
            {
                throw new InvalidOperationException(
                    "Character population aggregate restore is no longer prepared.");
            }
        }

        internal void MarkApplied(
            CharacterPopulationDomain<TProfile> population,
            Dictionary<TActor, TProfile> actors,
            Dictionary<string, TPreparation> preparations)
        {
            appliedPopulation = population;
            appliedActors = actors;
            appliedPreparations = preparations;
            applied = true;
        }

        internal void RequireApplied(
            CharacterPopulationDomain<TProfile> currentPopulation,
            Dictionary<TActor, TProfile> currentActors,
            Dictionary<string, TPreparation> currentPreparations)
        {
            if (!applied
                || finished
                || !ReferenceEquals(appliedPopulation, currentPopulation)
                || !ReferenceEquals(appliedActors, currentActors)
                || !ReferenceEquals(appliedPreparations, currentPreparations))
            {
                throw new InvalidOperationException(
                    "Character population aggregate restore is no longer active.");
            }
        }

        internal void MarkFinished()
        {
            appliedPopulation = null;
            appliedActors = null;
            appliedPreparations = null;
            finished = true;
        }
    }
}
