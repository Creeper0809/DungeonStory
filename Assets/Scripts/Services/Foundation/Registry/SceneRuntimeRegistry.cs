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
        private readonly HashSet<T> entrySet = new HashSet<T>();
        private readonly ReadOnlyCollection<T> readOnlyEntries;

        public SceneRuntimeRegistry()
        {
            readOnlyEntries = entries.AsReadOnly();
        }

        public int Version { get; private set; }

        public IReadOnlyList<T> Entries => readOnlyEntries;

        public bool Register(T entry)
        {
            if (!IsAlive(entry) || !entrySet.Add(entry))
            {
                return false;
            }

            entries.Add(entry);
            IncrementVersion();
            return true;
        }

        public bool Unregister(T entry)
        {
            if (entry == null || !entrySet.Remove(entry))
            {
                return false;
            }

            entries.Remove(entry);
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
            entrySet.Clear();
            IncrementVersion();
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
