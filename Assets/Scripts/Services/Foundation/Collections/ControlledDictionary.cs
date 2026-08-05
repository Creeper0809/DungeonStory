using System;
using System.Collections;
using System.Collections.Generic;

namespace DungeonStory.Foundation
{
    /// <summary>
    /// Narrow mutation port for a dictionary whose state remains owned by a
    /// domain aggregate. The adapter exposes IDictionary without copying or
    /// acquiring authority over the underlying state.
    /// </summary>
    public interface IControlledDictionaryStore<TKey, TValue>
    {
        IReadOnlyDictionary<TKey, TValue> Snapshot { get; }
        bool TryGetValue(TKey key, out TValue value);
        void SetValue(TKey key, TValue value);
        bool RemoveValue(TKey key);
        void ResetValues();
    }

    public delegate bool ControlledDictionaryTryGet<TKey, TValue>(
        TKey key,
        out TValue value);

    public sealed class DelegatingControlledDictionaryStore<TKey, TValue> :
        IControlledDictionaryStore<TKey, TValue>
    {
        private readonly Func<IReadOnlyDictionary<TKey, TValue>> snapshot;
        private readonly ControlledDictionaryTryGet<TKey, TValue> tryGetValue;
        private readonly Action<TKey, TValue> setValue;
        private readonly Func<TKey, bool> removeValue;
        private readonly Action resetValues;

        public DelegatingControlledDictionaryStore(
            Func<IReadOnlyDictionary<TKey, TValue>> snapshot,
            ControlledDictionaryTryGet<TKey, TValue> tryGetValue,
            Action<TKey, TValue> setValue,
            Func<TKey, bool> removeValue,
            Action resetValues)
        {
            this.snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
            this.tryGetValue = tryGetValue
                ?? throw new ArgumentNullException(nameof(tryGetValue));
            this.setValue = setValue
                ?? throw new ArgumentNullException(nameof(setValue));
            this.removeValue = removeValue
                ?? throw new ArgumentNullException(nameof(removeValue));
            this.resetValues = resetValues
                ?? throw new ArgumentNullException(nameof(resetValues));
        }

        public IReadOnlyDictionary<TKey, TValue> Snapshot => snapshot();
        public bool TryGetValue(TKey key, out TValue value) =>
            tryGetValue(key, out value);
        public void SetValue(TKey key, TValue value) => setValue(key, value);
        public bool RemoveValue(TKey key) => removeValue(key);
        public void ResetValues() => resetValues();
    }

    public sealed class ControlledDictionary<TKey, TValue> :
        IDictionary<TKey, TValue>
    {
        private readonly IControlledDictionaryStore<TKey, TValue> owner;

        public ControlledDictionary(
            IControlledDictionaryStore<TKey, TValue> owner)
        {
            this.owner = owner
                ?? throw new ArgumentNullException(nameof(owner));
        }

        public TValue this[TKey key]
        {
            get => owner.TryGetValue(key, out TValue value)
                ? value
                : throw new KeyNotFoundException(
                    $"Controlled value '{key}' does not exist.");
            set => owner.SetValue(key, value);
        }

        public ICollection<TKey> Keys => new List<TKey>(owner.Snapshot.Keys);
        public ICollection<TValue> Values =>
            new List<TValue>(owner.Snapshot.Values);
        public int Count => owner.Snapshot.Count;
        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException(
                    $"Controlled value '{key}' already exists.",
                    nameof(key));
            }

            owner.SetValue(key, value);
        }

        public bool ContainsKey(TKey key) => owner.TryGetValue(key, out _);
        public bool Remove(TKey key) => owner.RemoveValue(key);
        public bool TryGetValue(TKey key, out TValue value) =>
            owner.TryGetValue(key, out value);
        public void Add(KeyValuePair<TKey, TValue> item) =>
            Add(item.Key, item.Value);
        public void Clear() => owner.ResetValues();
        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            TryGetValue(item.Key, out TValue value)
            && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            Contains(item) && Remove(item.Key);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            owner.Snapshot.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
