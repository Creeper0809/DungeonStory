using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DungeonStory.Foundation
{
    public interface ISceneRuntimeRegistry<T> where T : class
    {
        int Version { get; }
        IReadOnlyList<T> Entries { get; }
        bool Register(T entry);
        bool Unregister(T entry);
        void Clear();
    }

    public sealed class SceneRuntimeRegistry<T> : ISceneRuntimeRegistry<T> where T : class
    {
        private readonly List<T> entries = new List<T>();
        private readonly ReadOnlyCollection<T> readOnlyEntries;

        public SceneRuntimeRegistry()
        {
            readOnlyEntries = entries.AsReadOnly();
        }

        public int Version { get; private set; }

        public IReadOnlyList<T> Entries
        {
            get
            {
                PruneDestroyedEntries();
                return readOnlyEntries;
            }
        }

        public bool Register(T entry)
        {
            if (!IsAlive(entry) || entries.Contains(entry))
            {
                return false;
            }

            entries.Add(entry);
            IncrementVersion();
            return true;
        }

        public bool Unregister(T entry)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            IncrementVersion();
            return true;
        }

        public void Clear()
        {
            if (entries.Count == 0)
            {
                return;
            }

            entries.Clear();
            IncrementVersion();
        }

        private void PruneDestroyedEntries()
        {
            bool changed = false;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (IsAlive(entries[index]))
                {
                    continue;
                }

                entries.RemoveAt(index);
                changed = true;
            }

            if (changed)
            {
                IncrementVersion();
            }
        }

        private void IncrementVersion()
        {
            unchecked
            {
                Version++;
            }
        }

        private static bool IsAlive(T entry)
        {
            if (entry == null)
            {
                return false;
            }

            return entry is not UnityEngine.Object unityObject || unityObject != null;
        }
    }
}
