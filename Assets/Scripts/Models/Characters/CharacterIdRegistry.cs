using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Characters
{
    public readonly struct CharacterIdentitySnapshot
    {
        public CharacterIdentitySnapshot(
            bool hasIdentity,
            bool isOwner,
            CharacterId persistentId)
        {
            HasIdentity = hasIdentity;
            IsOwner = isOwner;
            PersistentId = persistentId;
        }

        public bool HasIdentity { get; }
        public bool IsOwner { get; }
        public CharacterId PersistentId { get; }
    }

    /// <summary>
    /// Narrow identity boundary implemented by the scene adapter. The registry owns ID
    /// policy; the adapter owns actor canonicalization, lifetime enumeration, and writes.
    /// </summary>
    public interface ICharacterIdentityRegistryPort
    {
        CharacterIdentitySnapshot CaptureIdentity();
        IReadOnlyCollection<CharacterId> CaptureAssignedIds();
        void EnsureRuntimeState();
        void AssignPersistentId(CharacterId persistentId);
    }

    [MovedFrom(
        true,
        sourceAssembly: "Assembly-CSharp",
        sourceNamespace: "",
        sourceClassName: "CharacterIdRegistry")]
    public sealed class CharacterIdRegistry
    {
        private readonly IPersistentIdGenerator persistentIds;

        public CharacterIdRegistry(IPersistentIdGenerator persistentIds)
        {
            this.persistentIds = persistentIds
                ?? throw new ArgumentNullException(nameof(persistentIds));
        }

        public bool TryGetPersistentId(
            ICharacterIdentityRegistryPort identity,
            out CharacterId persistentId)
        {
            if (identity == null)
            {
                persistentId = default;
                return false;
            }

            CharacterIdentitySnapshot snapshot = identity.CaptureIdentity();
            persistentId = snapshot.PersistentId;
            return snapshot.HasIdentity && persistentId.IsValid;
        }

        public CharacterId GetOrAssignPersistentId(
            ICharacterIdentityRegistryPort identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            identity.EnsureRuntimeState();
            CharacterIdentitySnapshot snapshot = identity.CaptureIdentity();
            if (!snapshot.HasIdentity)
            {
                throw new InvalidOperationException(
                    "CharacterActor requires CharacterIdentity.");
            }

            if (snapshot.IsOwner)
            {
                identity.AssignPersistentId(CharacterId.Owner);
                return CharacterId.Owner;
            }

            if (snapshot.PersistentId.IsValid)
            {
                return snapshot.PersistentId;
            }

            HashSet<CharacterId> usedIds = new(
                identity.CaptureAssignedIds()
                ?? Array.Empty<CharacterId>());
            CharacterId candidate;
            do
            {
                candidate = persistentIds.NewCharacterId();
            }
            while (usedIds.Contains(candidate));

            identity.AssignPersistentId(candidate);
            return candidate;
        }
    }
}
