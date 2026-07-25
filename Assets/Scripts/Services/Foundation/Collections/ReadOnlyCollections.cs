using System;
using System.Collections;
using System.Collections.Generic;

public static class EventPayloadSnapshot
{
    public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
        {
            return Array.Empty<T>();
        }

        T[] copy = new T[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public static class ReadOnlyView
{
    public static IReadOnlyList<T> List<T>(IList<T> source)
    {
        return new ListAdapter<T>(source ?? throw new ArgumentNullException(nameof(source)));
    }

    public static IReadOnlyCollection<T> Collection<T>(ICollection<T> source)
    {
        return new CollectionAdapter<T>(source ?? throw new ArgumentNullException(nameof(source)));
    }

    public static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IDictionary<TKey, TValue> source)
    {
        return new DictionaryAdapter<TKey, TValue>(
            source ?? throw new ArgumentNullException(nameof(source)));
    }

    private sealed class ListAdapter<T> : IReadOnlyList<T>
    {
        private readonly IList<T> source;

        public ListAdapter(IList<T> source)
        {
            this.source = source;
        }

        public int Count => source.Count;
        public T this[int index] => source[index];
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CollectionAdapter<T> : IReadOnlyCollection<T>
    {
        private readonly ICollection<T> source;

        public CollectionAdapter(ICollection<T> source)
        {
            this.source = source;
        }

        public int Count => source.Count;
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DictionaryAdapter<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> source;
        private readonly IReadOnlyCollection<TKey> keys;
        private readonly IReadOnlyCollection<TValue> values;

        public DictionaryAdapter(IDictionary<TKey, TValue> source)
        {
            this.source = source;
            keys = new CollectionAdapter<TKey>(source.Keys);
            values = new CollectionAdapter<TValue>(source.Values);
        }

        public int Count => source.Count;
        public TValue this[TKey key] => source[key];
        public IEnumerable<TKey> Keys => keys;
        public IEnumerable<TValue> Values => values;
        public bool ContainsKey(TKey key) => source.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => source.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
