using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Profiling;

// DTO 소유자가 전체 필드의 독립 복사를 명시적으로 지원하는 저장 표현에만 적용한다.
public interface IDungeonSaveReadSnapshot
{
    object CopyForSaveRead();
}

/// <summary>Exact numeric reader for section payloads and nested physical item JSON.</summary>
public static class DungeonSaveJson
{
    private static readonly ProfilerMarker ReadPayloadMarker = new("Save.ReadPayload");
    private static readonly ProfilerMarker ReadSnapshotCopyMarker = new("Save.ReadSnapshotCopy");
    private static readonly ProfilerMarker ReadTokenReplayMarker = new("Save.ReadTokenReplay");
    private static readonly ProfilerMarker ReadSourceMissMarker = new("Save.ReadSourceMiss");
    [ThreadStatic] private static ReadScope currentScope;
#if UNITY_EDITOR
    public static long EditorSourceParseCount { get; private set; }
    public static long EditorReplayCount { get; private set; }
    public static long EditorSnapshotCopyCount { get; private set; }
    public static long EditorStrictScalarParseCount { get; private set; }
    public static long EditorStrictScalarReplayCount { get; private set; }
    public static long EditorStrictScalarStreamingParseCount { get; private set; }
    public static long EditorStrictScalarDomParseCount { get; private set; }
    public static long EditorRootShapeReplayCount { get; private set; }
#endif

    // 읽은 토큰만 한 저장 작업 동안 재사용한다. 반환 DTO는 매번 새로 만든다.
    public static IDisposable BeginReadScope()
    {
        return currentScope = new ReadScope(currentScope);
    }

    // 작업별 메모만 보관한다. 각 단계가 끝나면 현재 스레드의 scope는 되돌린다.
    public sealed class ReadSession : IDisposable
    {
        private readonly int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly ReadScope data = new(null);
        private bool active;
        private bool disposed;

        public ReadSession() { }

        private ReadSession(ReadScope prepared) { data = prepared; }

        // 원 세션과 가변 메모를 공유하지 않는다. 새 스레드가 한 번만
        // 소비한 뒤 그 스레드에서 활성화와 폐기를 책임진다.
        public Func<ReadSession> PrepareDetachedContinuation()
        {
            RequireThread();
            if (disposed) throw new ObjectDisposedException(nameof(ReadSession));
            ReadScope prepared = data.CopyDetached();
            return () =>
            {
                ReadScope owned = System.Threading.Interlocked.Exchange(ref prepared, null);
                if (owned == null)
                    throw new InvalidOperationException("Detached save read continuation was already consumed.");
                return new ReadSession(owned);
            };
        }

        public IDisposable Activate()
        {
            RequireThread();
            if (disposed) throw new ObjectDisposedException(nameof(ReadSession));
            if (active) throw new InvalidOperationException("Save read session is already active.");
            var scope = new ReadScope(currentScope, data);
            currentScope = scope;
            active = true;
            return new Activation(this, scope);
        }

        public void Dispose()
        {
            RequireThread();
            if (active) throw new InvalidOperationException("Active save read session cannot be disposed.");
            data.Clear();
            disposed = true;
        }

        private void RequireThread()
        {
            if (threadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Save read session must stay on its owning thread.");
        }

        private sealed class Activation : IDisposable
        {
            private readonly ReadSession owner;
            private readonly ReadScope scope;
            private bool disposed;
            public Activation(ReadSession owner, ReadScope scope) { this.owner = owner; this.scope = scope; }
            public void Dispose()
            {
                if (disposed) return;
                owner.RequireThread();
                scope.Dispose();
                owner.active = false;
                disposed = true;
            }
        }
    }

    // 문서 끝까지 중복 키를 검사하고, 원래 값 변환을 거친 불변 문자열만 재사용한다.
    public static string ReadStrictStringProperty(string json, string propertyName)
    {
        var key = new ScalarReadKey(json, propertyName);
        ReadScope scope = currentScope;
        if (scope != null && scope.StrictScalars.TryGetValue(key, out string cached))
        {
#if UNITY_EDITOR
            EditorStrictScalarReplayCount++;
#endif
            return cached;
        }
#if UNITY_EDITOR
        EditorStrictScalarParseCount++;
#endif
        string result = ReadStrictScalar(json, propertyName);
        scope?.StrictScalars.Add(key, result);
        return result;
    }

    private static string ReadStrictScalar(string json, string propertyName)
    {
        if (json != null && propertyName != null)
        {
#if UNITY_EDITOR
            EditorStrictScalarStreamingParseCount++;
#endif
            try
            {
                if (TryReadStrictScalar(json, propertyName, out string value)) return value;
            }
            catch (JsonException)
            {
                // 실패를 성공으로 바꾸지 않는다. 기존 파서가 같은 원문을 판정해
                // 기존 허용 문법과 예외의 종류, 위치, 문구를 그대로 보존한다.
            }
        }
#if UNITY_EDITOR
        EditorStrictScalarDomParseCount++;
#endif
        return (string)JObject.Parse(json,
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error })[propertyName];
    }

    private static bool TryReadStrictScalar(string json, string propertyName, out string result)
    {
        result = null;
        using var text = new System.IO.StringReader(json);
        using var reader = new JsonTextReader(text);
        if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return false;
        var properties = new List<HashSet<string>> { new(StringComparer.Ordinal) };
        int objectDepth = 0;
        bool closed = false;
        JToken selected = null;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    objectDepth++;
                    if (properties.Count == objectDepth) properties.Add(new HashSet<string>(StringComparer.Ordinal));
                    else properties[objectDepth].Clear();
                    break;
                case JsonToken.EndObject:
                    objectDepth--;
                    if (objectDepth < 0) closed = true;
                    break;
                case JsonToken.PropertyName:
                    string name = (string)reader.Value;
                    if (!properties[objectDepth].Add(name)) return false;
                    if (objectDepth == 0 && string.Equals(name, propertyName, StringComparison.Ordinal))
                    {
                        do { if (!reader.Read()) return false; }
                        while (reader.TokenType == JsonToken.Comment);
                        selected = JToken.ReadFrom(reader, new JsonLoadSettings
                            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    }
                    break;
                case JsonToken.StartArray:
                case JsonToken.EndArray:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Null:
                case JsonToken.Date:
                case JsonToken.Comment:
                    break;
                default:
                    return false;
            }
            if (closed) break;
        }
        if (!closed) return false;
        while (reader.Read())
            if (reader.TokenType != JsonToken.Comment) return false;
        result = (string)selected;
        return true;
    }

    public static TPayload Deserialize<TPayload>(string json) where TPayload : class
    {
        using var readSample = ReadPayloadMarker.Auto();
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Save payload JSON is empty.");
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Double,
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
            MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore,
            ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace,
            CheckAdditionalContent = true
        };
        var key = new ReadKey(typeof(TPayload), json);
        ReadScope scope = currentScope;
        if (scope != null && scope.Snapshots.TryGetValue(key, out IDungeonSaveReadSnapshot snapshot))
        {
            using var copySample = ReadSnapshotCopyMarker.Auto();
#if UNITY_EDITOR
            EditorReplayCount++;
            EditorSnapshotCopyCount++;
#endif
            return (TPayload)snapshot.CopyForSaveRead();
        }
        if (scope != null && scope.Tokens.TryGetValue(key, out List<Token> tokens))
        {
            using var replaySample = ReadTokenReplayMarker.Auto();
#if UNITY_EDITOR
            EditorReplayCount++;
#endif
            using var replay = new ReplayReader(tokens);
            return JsonSerializer.Create(settings).Deserialize<TPayload>(replay)
                ?? throw new InvalidOperationException("Save payload JSON is null.");
        }
#if UNITY_EDITOR
        EditorSourceParseCount++;
#endif
        using var sourceSample = ReadSourceMissMarker.Auto();
        using var text = new System.IO.StringReader(json);
        using var source = new JsonTextReader(text);
        if (scope == null)
            return JsonSerializer.Create(settings).Deserialize<TPayload>(source)
                ?? throw new InvalidOperationException("Save payload JSON is null.");
        if (typeof(IDungeonSaveReadSnapshot).IsAssignableFrom(typeof(TPayload)))
        {
            TPayload parsed = JsonSerializer.Create(settings).Deserialize<TPayload>(source)
                ?? throw new InvalidOperationException("Save payload JSON is null.");
            object detached;
            using (ReadSnapshotCopyMarker.Auto())
                detached = ((IDungeonSaveReadSnapshot)parsed).CopyForSaveRead();
            if (detached == null || detached.GetType() != parsed.GetType() || ReferenceEquals(detached, parsed))
                throw new InvalidOperationException("Save read snapshot must provide an independent exact-type copy.");
            scope.Snapshots.Add(key, (IDungeonSaveReadSnapshot)detached);
#if UNITY_EDITOR
            EditorSnapshotCopyCount++;
#endif
            return parsed;
        }
        using var recorder = new RecordingReader(source);
        TPayload result = JsonSerializer.Create(settings).Deserialize<TPayload>(recorder)
            ?? throw new InvalidOperationException("Save payload JSON is null.");
        // 실패하거나 null인 입력은 메모에 넣지 않는다. 원문 참조와 DTO 타입을 함께 결박한다.
        scope.Tokens.Add(key, recorder.Tokens);
        return result;
    }

    // 원 DTO 형변환을 모두 검증하고 root 필드의 null 여부만 반환한다.
    // scope 내부 원본과 그 가변 참조는 호출자에게 내보내지 않는다.
    public static bool HasNonNullRootFields<TPayload>(string json, params string[] fieldNames)
        where TPayload : class
    {
        TPayload parsed;
        var key = new ReadKey(typeof(TPayload), json);
        ReadScope scope = currentScope;
        if (!string.IsNullOrWhiteSpace(json) && scope != null
            && scope.Snapshots.TryGetValue(key, out IDungeonSaveReadSnapshot snapshot))
        {
            parsed = (TPayload)snapshot;
#if UNITY_EDITOR
            EditorRootShapeReplayCount++;
#endif
        }
        else parsed = Deserialize<TPayload>(json);

        if (fieldNames == null) throw new ArgumentNullException(nameof(fieldNames));
        foreach (string name in fieldNames)
        {
            var field = typeof(TPayload).GetField(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null)
                throw new ArgumentException("Save shape field is missing: " + name, nameof(fieldNames));
            if (field.GetValue(parsed) == null) return false;
        }
        return true;
    }

    private readonly struct ReadKey : IEquatable<ReadKey>
    {
        private readonly Type type;
        private readonly string source;
        public ReadKey(Type type, string source) { this.type = type; this.source = source; }
        public bool Equals(ReadKey other) => type == other.type && ReferenceEquals(source, other.source);
        public override bool Equals(object other) => other is ReadKey key && Equals(key);
        public override int GetHashCode() => type.GetHashCode() ^ RuntimeHelpers.GetHashCode(source);
    }

    private readonly struct ScalarReadKey : IEquatable<ScalarReadKey>
    {
        private readonly string source;
        private readonly string property;
        public ScalarReadKey(string source, string property) { this.source = source; this.property = property; }
        public bool Equals(ScalarReadKey other) => ReferenceEquals(source, other.source)
            && string.Equals(property, other.property, StringComparison.Ordinal);
        public override bool Equals(object other) => other is ScalarReadKey key && Equals(key);
        public override int GetHashCode() => (source == null ? 0 : RuntimeHelpers.GetHashCode(source))
            ^ (property == null ? 0 : StringComparer.Ordinal.GetHashCode(property));
    }

    private sealed class ReadScope : IDisposable
    {
        private readonly ReadScope parent;
        private readonly bool ownsData;
        private bool disposed;
        public readonly Dictionary<ReadKey, List<Token>> Tokens;
        public readonly Dictionary<ReadKey, IDungeonSaveReadSnapshot> Snapshots;
        public readonly Dictionary<ScalarReadKey, string> StrictScalars;
        public ReadScope(ReadScope parent, ReadScope shared = null)
        {
            this.parent = parent;
            ownsData = shared == null;
            Tokens = shared?.Tokens ?? new();
            Snapshots = shared?.Snapshots ?? new();
            StrictScalars = shared?.StrictScalars ?? new();
        }
        public void Dispose()
        {
            if (disposed) return;
            if (!ReferenceEquals(currentScope, this))
                throw new InvalidOperationException("Save read scopes must close in reverse order.");
            currentScope = parent;
            if (ownsData) Clear();
            disposed = true;
        }
        public void Clear()
        {
            Tokens.Clear();
            Snapshots.Clear();
            StrictScalars.Clear();
        }

        public ReadScope CopyDetached()
        {
            var copy = new ReadScope(null);
            foreach (var pair in Tokens)
            {
                var tokens = new List<Token>(pair.Value.Count);
                foreach (Token token in pair.Value) tokens.Add(new Token(token.Kind, token.Value));
                copy.Tokens.Add(pair.Key, tokens);
            }
            foreach (var pair in Snapshots)
            {
                using var sample = ReadSnapshotCopyMarker.Auto();
                object detached = pair.Value.CopyForSaveRead();
                if (detached is not IDungeonSaveReadSnapshot snapshot
                    || detached.GetType() != pair.Value.GetType() || ReferenceEquals(detached, pair.Value))
                    throw new InvalidOperationException("Save read snapshot must provide an independent exact-type copy.");
                copy.Snapshots.Add(pair.Key, snapshot);
            }
            foreach (var pair in StrictScalars) copy.StrictScalars.Add(pair.Key, pair.Value);
            return copy;
        }
    }

    private readonly struct Token
    {
        public readonly JsonToken Kind;
        public readonly object Value;
        public Token(JsonToken kind, object value) { Kind = kind; Value = CopyValue(value); }
    }

    private static object CopyValue(object value) => value is byte[] bytes ? bytes.Clone() : value;

    private sealed class RecordingReader : JsonReader, IJsonLineInfo
    {
        private readonly JsonTextReader source;
        public readonly List<Token> Tokens = new();
        public RecordingReader(JsonTextReader source) { this.source = source; }
        private void Configure()
        {
            source.Culture = Culture;
            source.DateParseHandling = DateParseHandling;
            source.DateTimeZoneHandling = DateTimeZoneHandling;
            source.FloatParseHandling = FloatParseHandling;
            source.DateFormatString = DateFormatString;
            source.MaxDepth = MaxDepth;
        }
        private void Record()
        {
            SetToken(source.TokenType, source.Value);
            if (source.TokenType != JsonToken.None) Tokens.Add(new Token(source.TokenType, source.Value));
        }
        public override bool Read()
        {
            Configure();
            bool read = source.Read();
            if (read) Record();
            else SetToken(JsonToken.None);
            return read;
        }
        public override int? ReadAsInt32() { Configure(); var value = source.ReadAsInt32(); Record(); return value; }
        public override byte[] ReadAsBytes() { Configure(); var value = source.ReadAsBytes(); Record(); return value; }
        public override decimal? ReadAsDecimal() { Configure(); var value = source.ReadAsDecimal(); Record(); return value; }
        public override double? ReadAsDouble() { Configure(); var value = source.ReadAsDouble(); Record(); return value; }
        public override bool? ReadAsBoolean() { Configure(); var value = source.ReadAsBoolean(); Record(); return value; }
        public override string ReadAsString() { Configure(); var value = source.ReadAsString(); Record(); return value; }
        public override DateTime? ReadAsDateTime() { Configure(); var value = source.ReadAsDateTime(); Record(); return value; }
        public override DateTimeOffset? ReadAsDateTimeOffset() { Configure(); var value = source.ReadAsDateTimeOffset(); Record(); return value; }
        public bool HasLineInfo() => ((IJsonLineInfo)source).HasLineInfo();
        public int LineNumber => source.LineNumber;
        public int LinePosition => source.LinePosition;
        public override void Close() { source.Close(); base.Close(); }
    }

    private sealed class ReplayReader : JsonReader
    {
        private readonly List<Token> tokens;
        private int index;
        public ReplayReader(List<Token> tokens) { this.tokens = tokens; }
        public override bool Read()
        {
            if (index >= tokens.Count) { SetToken(JsonToken.None); return false; }
            Token token = tokens[index++];
            SetToken(token.Kind, CopyValue(token.Value));
            return true;
        }
    }
}
