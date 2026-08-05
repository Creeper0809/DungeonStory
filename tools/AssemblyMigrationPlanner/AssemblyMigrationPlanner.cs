using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AssemblyMigrationPlanner
{
    private const int SchemaVersion = 1;
    private const string CandidateRoot = "Assets/Scripts/";
    private static readonly SymbolDisplayFormat TypeDisplayFormat =
        SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static int Main(string[] args)
    {
        try
        {
            Arguments options = Arguments.Parse(args);
            if (options.SelfTest)
            {
                RunSelfTest();
                Console.WriteLine("AssemblyMigrationPlanner self-test PASS.");
                return 0;
            }

            InputSet input = InputLoader.Load(options.ProjectRoot, options.ResponsePath);
            PlannerReport report = Analyze(input);
            string json = Serialize(report);
            string directory = Path.GetDirectoryName(options.ReportPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(options.ReportPath, json, new UTF8Encoding(false));
            Console.WriteLine(
                "AssemblyMigrationPlanner PASS: candidates=" + report.CandidateFileCount
                + ", edges=" + report.EdgeCount
                + ", sccs=" + report.SccCount
                + ", cyclicSccs=" + report.CyclicSccCount
                + ", leaves=" + report.LeafCandidates.Length
                + ", graphHash=" + report.GraphHash + ".");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static PlannerReport Analyze(InputSet input)
    {
        CSharpParseOptions parseOptions = new CSharpParseOptions(
            input.LanguageVersion,
            DocumentationMode.None,
            SourceCodeKind.Regular,
            input.Defines);
        SourceUnit[] units = input.Sources
            .OrderBy(source => source.Path, StringComparer.Ordinal)
            .Select(source => new SourceUnit
            {
                Path = source.Path,
                IsCandidate = source.IsCandidate,
                Tree = CSharpSyntaxTree.ParseText(
                    source.Text,
                    parseOptions,
                    source.Path,
                    Encoding.UTF8)
            })
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Assembly-CSharp-MigrationAnalysis",
            units.Select(unit => unit.Tree),
            input.References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                deterministic: true,
                concurrentBuild: true));

        SymbolOwnerIndex owners = BuildOwnerIndex(compilation, units);
        SourceAnalysis[] analyses = new SourceAnalysis[units.Length];
        Parallel.For(
            0,
            units.Length,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            index => analyses[index] = AnalyzeSource(compilation, units[index], owners));

        return BuildReport(input, units, owners, analyses);
    }

    private static SymbolOwnerIndex BuildOwnerIndex(
        CSharpCompilation compilation,
        IEnumerable<SourceUnit> units)
    {
        SymbolOwnerIndex index = new SymbolOwnerIndex();
        foreach (SourceUnit unit in units.OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            SemanticModel model = compilation.GetSemanticModel(unit.Tree, true);
            SyntaxNode root = unit.Tree.GetRoot();
            IEnumerable<SyntaxNode> declarations = root.DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax
                    || node is DelegateDeclarationSyntax);
            foreach (SyntaxNode declaration in declarations)
            {
                INamedTypeSymbol symbol = declaration is BaseTypeDeclarationSyntax baseType
                    ? model.GetDeclaredSymbol(baseType)
                    : model.GetDeclaredSymbol((DelegateDeclarationSyntax)declaration);
                if (symbol == null) continue;
                index.Add(symbol.OriginalDefinition, unit.Path, unit.IsCandidate);
            }
        }
        index.Freeze();
        return index;
    }

    private static SourceAnalysis AnalyzeSource(
        CSharpCompilation compilation,
        SourceUnit unit,
        SymbolOwnerIndex owners)
    {
        SemanticModel model = compilation.GetSemanticModel(unit.Tree, true);
        SyntaxNode root = unit.Tree.GetRoot();
        SourceAnalysis result = new SourceAnalysis(unit.Path, unit.IsCandidate);
        result.DeclaredTypes.AddRange(owners.TypesDeclaredAt(unit.Path));

        foreach (SimpleNameSyntax name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            HashSet<INamedTypeSymbol> referencedTypes = new HashSet<INamedTypeSymbol>(
                NamedTypeSymbolComparer.Instance);
            SymbolInfo symbolInfo = model.GetSymbolInfo(name);
            AddReferencedTypes(symbolInfo.Symbol, referencedTypes);
            TypeInfo typeInfo = model.GetTypeInfo(name);
            AddReferencedType(typeInfo.Type, referencedTypes);
            AddReferencedType(typeInfo.ConvertedType, referencedTypes);

            HashSet<string> occurrenceTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (INamedTypeSymbol referencedType in referencedTypes)
            {
                string typeName = referencedType.ToDisplayString(TypeDisplayFormat);
                IReadOnlyList<SymbolOwner> sourceOwners = owners.Find(referencedType);
                if (sourceOwners.Count > 0)
                {
                    foreach (SymbolOwner owner in sourceOwners)
                    {
                        if (string.Equals(owner.Path, unit.Path, StringComparison.Ordinal)) continue;
                        string key = "source|" + owner.Path + "|" + typeName;
                        if (occurrenceTargets.Add(key))
                            result.AddReference(ReferenceHit.Source(owner.Path, owner.IsCandidate, typeName));
                    }
                }
                else
                {
                    string assemblyName = referencedType.ContainingAssembly == null
                        ? "<unknown>"
                        : referencedType.ContainingAssembly.Identity.Name;
                    string key = "metadata|" + assemblyName + "|" + typeName;
                    if (occurrenceTargets.Add(key))
                        result.AddReference(ReferenceHit.Metadata(assemblyName, typeName));
                }
            }
        }
        return result;
    }

    private static void AddReferencedTypes(
        ISymbol symbol,
        ISet<INamedTypeSymbol> output)
    {
        if (symbol == null) return;
        if (symbol is IAliasSymbol alias)
        {
            AddReferencedTypes(alias.Target, output);
            return;
        }
        if (symbol is ITypeSymbol type)
        {
            AddReferencedType(type, output);
            return;
        }
        if (symbol.ContainingType != null)
            AddReferencedType(symbol.ContainingType, output);
        switch (symbol)
        {
            case IFieldSymbol field: AddReferencedType(field.Type, output); break;
            case IPropertySymbol property: AddReferencedType(property.Type, output); break;
            case IEventSymbol eventSymbol: AddReferencedType(eventSymbol.Type, output); break;
            case ILocalSymbol local: AddReferencedType(local.Type, output); break;
            case IParameterSymbol parameter: AddReferencedType(parameter.Type, output); break;
        }
    }

    private static void AddReferencedType(
        ITypeSymbol type,
        ISet<INamedTypeSymbol> output)
    {
        if (type == null) return;
        if (type is IArrayTypeSymbol array)
        {
            AddReferencedType(array.ElementType, output);
            return;
        }
        if (type is IPointerTypeSymbol pointer)
        {
            AddReferencedType(pointer.PointedAtType, output);
            return;
        }
        if (!(type is INamedTypeSymbol named)) return;
        if (named.TypeKind == TypeKind.Error || named.IsAnonymousType) return;
        output.Add(named.OriginalDefinition);
        foreach (ITypeSymbol argument in named.TypeArguments)
            AddReferencedType(argument, output);
    }

    private static PlannerReport BuildReport(
        InputSet input,
        SourceUnit[] units,
        SymbolOwnerIndex owners,
        SourceAnalysis[] analyses)
    {
        string[] candidates = units.Where(unit => unit.IsCandidate)
            .Select(unit => unit.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> candidateSet = new HashSet<string>(candidates, StringComparer.Ordinal);
        Dictionary<EdgeKey, EdgeAccumulator> edges = new Dictionary<EdgeKey, EdgeAccumulator>();
        Dictionary<string, List<BoundaryAccumulator>> egress = candidates.ToDictionary(
            path => path,
            path => new List<BoundaryAccumulator>(),
            StringComparer.Ordinal);
        Dictionary<string, List<BoundaryAccumulator>> ingress = candidates.ToDictionary(
            path => path,
            path => new List<BoundaryAccumulator>(),
            StringComparer.Ordinal);

        foreach (SourceAnalysis analysis in analyses.OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            foreach (ReferenceGroup group in analysis.Groups())
            {
                if (analysis.IsCandidate)
                {
                    if (group.TargetIsCandidate && candidateSet.Contains(group.Target))
                    {
                        AddEdge(edges, analysis.Path, group.Target, group.Count, group.Types);
                    }
                    else
                    {
                        AddBoundary(egress[analysis.Path], group.TargetKind, group.Target, group.Count, group.Types);
                    }
                }
                else if (group.TargetIsCandidate && candidateSet.Contains(group.Target))
                {
                    AddBoundary(ingress[group.Target], "source", analysis.Path, group.Count, group.Types);
                }
            }
        }

        Dictionary<string, string[]> adjacency = candidates.ToDictionary(
            path => path,
            path => edges.Keys.Where(key => key.Source == path)
                .Select(key => key.Target)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        SccResult scc = SccResult.Create(candidates, adjacency);
        MigrationBatchDto[] batches = BuildMigrationBatches(scc, adjacency);
        Dictionary<string, string> sccByFile = scc.Components
            .SelectMany(component => component.Files.Select(file => new { file, component.Id }))
            .ToDictionary(value => value.file, value => value.Id, StringComparer.Ordinal);

        Dictionary<string, List<EdgeAccumulator>> incomingEdges = candidates.ToDictionary(
            path => path,
            path => new List<EdgeAccumulator>(),
            StringComparer.Ordinal);
        Dictionary<string, List<EdgeAccumulator>> outgoingEdges = candidates.ToDictionary(
            path => path,
            path => new List<EdgeAccumulator>(),
            StringComparer.Ordinal);
        foreach (EdgeAccumulator edge in edges.Values)
        {
            outgoingEdges[edge.Source].Add(edge);
            incomingEdges[edge.Target].Add(edge);
        }

        FileNodeDto[] files = candidates.Select(path =>
        {
            SccComponent component = scc.Components.Single(value => value.Id == sccByFile[path]);
            return new FileNodeDto
            {
                Path = path,
                DeclaredTypes = owners.TypesDeclaredAt(path).ToArray(),
                SccId = component.Id,
                SccSize = component.Files.Length,
                IsLeaf = component.Files.Length == 1 && adjacency[path].Length == 0,
                Outgoing = outgoingEdges[path]
                    .OrderBy(edge => edge.Target, StringComparer.Ordinal)
                    .Select(ToOutgoingDto).ToArray(),
                Incoming = incomingEdges[path]
                    .OrderBy(edge => edge.Source, StringComparer.Ordinal)
                    .Select(ToIncomingDto).ToArray(),
                BoundaryEgress = ToBoundaryDtos(egress[path]),
                BoundaryIngress = ToBoundaryDtos(ingress[path])
            };
        }).ToArray();

        SccDto[] sccDtos = scc.Components.Select(component => new SccDto
        {
            Id = component.Id,
            Files = component.Files,
            IsCyclic = component.Files.Length > 1
                || adjacency[component.Files[0]].Contains(component.Files[0], StringComparer.Ordinal),
            IncomingSccIds = CrossSccIds(component, scc.Components, adjacency, false),
            OutgoingSccIds = CrossSccIds(component, scc.Components, adjacency, true)
        }).ToArray();
        string[] edgeCanon = edges.Values
            .OrderBy(edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(edge => edge.Target, StringComparer.Ordinal)
            .Select(edge => edge.Source + "->" + edge.Target + "|" + edge.Count + "|"
                + string.Join(",", edge.Types.OrderBy(value => value, StringComparer.Ordinal)))
            .ToArray();
        string[] sccCanon = scc.Components.Select(component =>
            component.Id + "|" + string.Join(",", component.Files)).ToArray();
        string[] orderCanon = batches.Select(batch =>
            batch.Order + "|" + batch.SccId + "|" + string.Join(",", batch.Files)).ToArray();

        return new PlannerReport
        {
            SchemaVersion = SchemaVersion,
            InputMode = input.InputMode,
            ResponseFile = input.ResponsePath,
            LanguageVersion = input.LanguageVersion.ToDisplayString(),
            SourceFileCount = units.Length,
            CandidateFileCount = candidates.Length,
            MetadataReferenceCount = input.References.Length,
            MissingMetadataReferences = input.MissingReferences,
            InputHash = input.InputHash,
            NodeHash = HashValues(files.Select(file => file.Path + "|" + string.Join(",", file.DeclaredTypes))),
            EdgeHash = HashValues(edgeCanon),
            SccHash = HashValues(sccCanon),
            MigrationOrderHash = HashValues(orderCanon),
            GraphHash = HashValues(edgeCanon.Concat(sccCanon).Concat(orderCanon)),
            EdgeCount = edges.Count,
            SccCount = sccDtos.Length,
            CyclicSccCount = sccDtos.Count(value => value.IsCyclic),
            LeafCandidates = files.Where(file => file.IsLeaf).Select(file => file.Path).ToArray(),
            Files = files,
            Sccs = sccDtos,
            MigrationBatches = batches
        };
    }

    private static void AddEdge(
        IDictionary<EdgeKey, EdgeAccumulator> edges,
        string source,
        string target,
        int count,
        IEnumerable<string> types)
    {
        EdgeKey key = new EdgeKey(source, target);
        if (!edges.TryGetValue(key, out EdgeAccumulator edge))
        {
            edge = new EdgeAccumulator(source, target);
            edges.Add(key, edge);
        }
        edge.Count += count;
        edge.Types.UnionWith(types);
    }

    private static void AddBoundary(
        IList<BoundaryAccumulator> values,
        string kind,
        string counterpart,
        int count,
        IEnumerable<string> types)
    {
        BoundaryAccumulator value = values.FirstOrDefault(existing =>
            existing.Kind == kind && existing.Counterpart == counterpart);
        if (value == null)
        {
            value = new BoundaryAccumulator(kind, counterpart);
            values.Add(value);
        }
        value.Count += count;
        value.Types.UnionWith(types);
    }

    private static EdgeDto ToOutgoingDto(EdgeAccumulator edge) => new EdgeDto
    {
        File = edge.Target,
        ReferenceCount = edge.Count,
        ReferencedTypes = edge.Types.OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };

    private static EdgeDto ToIncomingDto(EdgeAccumulator edge) => new EdgeDto
    {
        File = edge.Source,
        ReferenceCount = edge.Count,
        ReferencedTypes = edge.Types.OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };

    private static BoundaryReferenceDto[] ToBoundaryDtos(IEnumerable<BoundaryAccumulator> values) =>
        values.OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Counterpart, StringComparer.Ordinal)
            .Select(value => new BoundaryReferenceDto
            {
                Kind = value.Kind,
                Counterpart = value.Counterpart,
                ReferenceCount = value.Count,
                ReferencedTypes = value.Types.OrderBy(type => type, StringComparer.Ordinal).ToArray()
            }).ToArray();

    private static string[] CrossSccIds(
        SccComponent component,
        SccComponent[] all,
        IReadOnlyDictionary<string, string[]> adjacency,
        bool outgoing)
    {
        HashSet<string> own = new HashSet<string>(component.Files, StringComparer.Ordinal);
        IEnumerable<SccComponent> matches = all.Where(other => other.Id != component.Id &&
            (outgoing
                ? component.Files.Any(file => adjacency[file].Any(other.Files.Contains))
                : other.Files.Any(file => adjacency[file].Any(own.Contains))));
        return matches.Select(value => value.Id).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static MigrationBatchDto[] BuildMigrationBatches(
        SccResult scc,
        IReadOnlyDictionary<string, string[]> adjacency)
    {
        Dictionary<string, SccComponent> remaining = scc.Components.ToDictionary(
            component => component.Id,
            component => component,
            StringComparer.Ordinal);
        Dictionary<string, string> owner = scc.Components
            .SelectMany(component => component.Files.Select(file => new { file, component.Id }))
            .ToDictionary(value => value.file, value => value.Id, StringComparer.Ordinal);
        List<MigrationBatchDto> result = new List<MigrationBatchDto>();
        int order = 1;
        while (remaining.Count > 0)
        {
            SccComponent[] ready = remaining.Values.Where(component =>
                    component.Files.SelectMany(file => adjacency[file])
                        .Select(file => owner[file])
                        .Where(id => id != component.Id)
                        .All(id => !remaining.ContainsKey(id)))
                .OrderBy(component => component.Files[0], StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
                throw new InvalidOperationException("SCC condensation graph unexpectedly contains a cycle.");
            foreach (SccComponent component in ready)
            {
                result.Add(new MigrationBatchDto
                {
                    Order = order++,
                    SccId = component.Id,
                    IsCyclic = component.Files.Length > 1
                        || adjacency[component.Files[0]].Contains(component.Files[0], StringComparer.Ordinal),
                    Files = component.Files
                });
                remaining.Remove(component.Id);
            }
        }
        return result.ToArray();
    }

    private static string Serialize(PlannerReport report)
    {
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(report, options) + "\n";
    }

    private static string HashValues(IEnumerable<string> values)
    {
        string canonical = string.Join("\n", values.OrderBy(value => value, StringComparer.Ordinal));
        using (SHA256 hash = SHA256.Create())
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }
    }

    private static void RunSelfTest()
    {
        MetadataReference core = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        InputSet input = new InputSet
        {
            InputMode = "self-test",
            ResponsePath = "<self-test>",
            LanguageVersion = LanguageVersion.CSharp9,
            Defines = Array.Empty<string>(),
            References = new[] { core },
            MissingReferences = Array.Empty<string>(),
            Sources = new[]
            {
                SourceInput.Test("Assets/Scripts/A.cs", "class A { B value; }", true),
                SourceInput.Test("Assets/Scripts/B.cs", "class B { C Make() => new C(); }", true),
                SourceInput.Test("Assets/Scripts/C.cs", "class C { B Back; }", true),
                SourceInput.Test("Assets/Scripts/D.cs", "class D { }", true),
                SourceInput.Test("Assets/Scripts/E.cs", "using Alias = C; class E { Alias value; }", true),
                SourceInput.Test("Assets/Vendor/Vendor.cs", "class Vendor { A value; }", false)
            },
            InputHash = "self-test"
        };
        PlannerReport first = Analyze(input);
        PlannerReport second = Analyze(input);
        Require(first.GraphHash == second.GraphHash, "graph hash is not deterministic");
        Require(first.MigrationOrderHash == second.MigrationOrderHash, "order hash is not deterministic");
        Require(Serialize(first) == Serialize(second), "JSON output is not deterministic");
        Require(first.Files.Single(file => file.Path.EndsWith("A.cs", StringComparison.Ordinal))
            .Outgoing.Any(edge => edge.File.EndsWith("B.cs", StringComparison.Ordinal)), "A -> B missing");
        Require(first.Files.Single(file => file.Path.EndsWith("E.cs", StringComparison.Ordinal))
            .Outgoing.Any(edge => edge.File.EndsWith("C.cs", StringComparison.Ordinal)), "alias E -> C missing");
        Require(first.Sccs.Any(component => component.Files.Length == 2
            && component.Files.Any(file => file.EndsWith("B.cs", StringComparison.Ordinal))
            && component.Files.Any(file => file.EndsWith("C.cs", StringComparison.Ordinal))), "B/C SCC missing");
        Require(first.LeafCandidates.Any(file => file.EndsWith("D.cs", StringComparison.Ordinal)), "D leaf missing");
        Require(first.Files.Single(file => file.Path.EndsWith("A.cs", StringComparison.Ordinal))
            .BoundaryIngress.Any(value => value.Counterpart.EndsWith("Vendor.cs", StringComparison.Ordinal)),
            "boundary ingress missing");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Self-test failed: " + message + ".");
    }

    private sealed class Arguments
    {
        public string ProjectRoot;
        public string ReportPath;
        public string ResponsePath;
        public bool SelfTest;

        public static Arguments Parse(string[] args)
        {
            Arguments result = new Arguments();
            for (int index = 0; index < args.Length; index++)
            {
                string value = args[index];
                if (value == "--self-test") result.SelfTest = true;
                else if (value == "--project") result.ProjectRoot = RequireValue(args, ref index, value);
                else if (value == "--report") result.ReportPath = RequireValue(args, ref index, value);
                else if (value == "--rsp") result.ResponsePath = RequireValue(args, ref index, value);
                else throw new ArgumentException("Unknown argument: " + value);
            }
            if (result.SelfTest) return result;
            if (string.IsNullOrWhiteSpace(result.ProjectRoot) || string.IsNullOrWhiteSpace(result.ReportPath))
                throw new ArgumentException("Required arguments: --project and --report.");
            result.ProjectRoot = Path.GetFullPath(result.ProjectRoot);
            result.ReportPath = Path.GetFullPath(result.ReportPath);
            if (!string.IsNullOrWhiteSpace(result.ResponsePath))
                result.ResponsePath = Path.GetFullPath(result.ResponsePath);
            return result;
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length) throw new ArgumentException("Missing value for " + option + ".");
            return args[index];
        }
    }

    private sealed class SourceUnit
    {
        public string Path;
        public bool IsCandidate;
        public SyntaxTree Tree;
    }

    private sealed class SourceAnalysis
    {
        private readonly Dictionary<string, ReferenceGroup> groups = new Dictionary<string, ReferenceGroup>(StringComparer.Ordinal);
        public readonly string Path;
        public readonly bool IsCandidate;
        public readonly List<string> DeclaredTypes = new List<string>();

        public SourceAnalysis(string path, bool isCandidate)
        {
            Path = path;
            IsCandidate = isCandidate;
        }

        public void AddReference(ReferenceHit hit)
        {
            string key = hit.TargetKind + "|" + hit.Target;
            if (!groups.TryGetValue(key, out ReferenceGroup group))
            {
                group = new ReferenceGroup(hit.TargetKind, hit.Target, hit.TargetIsCandidate);
                groups.Add(key, group);
            }
            group.Count++;
            group.Types.Add(hit.Type);
        }

        public IEnumerable<ReferenceGroup> Groups() => groups.Values
            .OrderBy(value => value.TargetKind, StringComparer.Ordinal)
            .ThenBy(value => value.Target, StringComparer.Ordinal);
    }

    private sealed class ReferenceHit
    {
        public string TargetKind;
        public string Target;
        public bool TargetIsCandidate;
        public string Type;

        public static ReferenceHit Source(string path, bool candidate, string type) => new ReferenceHit
        {
            TargetKind = "source", Target = path, TargetIsCandidate = candidate, Type = type
        };

        public static ReferenceHit Metadata(string assembly, string type) => new ReferenceHit
        {
            TargetKind = "metadata", Target = assembly, TargetIsCandidate = false, Type = type
        };
    }

    private sealed class ReferenceGroup
    {
        public readonly string TargetKind;
        public readonly string Target;
        public readonly bool TargetIsCandidate;
        public int Count;
        public readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal);

        public ReferenceGroup(string kind, string target, bool candidate)
        {
            TargetKind = kind; Target = target; TargetIsCandidate = candidate;
        }
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly string Source;
        public readonly string Target;
        public EdgeKey(string source, string target) { Source = source; Target = target; }
        public bool Equals(EdgeKey other) => Source == other.Source && Target == other.Target;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => unchecked((Source.GetHashCode() * 397) ^ Target.GetHashCode());
    }

    private sealed class EdgeAccumulator
    {
        public readonly string Source;
        public readonly string Target;
        public int Count;
        public readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal);
        public EdgeAccumulator(string source, string target) { Source = source; Target = target; }
    }

    private sealed class BoundaryAccumulator
    {
        public readonly string Kind;
        public readonly string Counterpart;
        public int Count;
        public readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal);
        public BoundaryAccumulator(string kind, string counterpart) { Kind = kind; Counterpart = counterpart; }
    }

    private sealed class NamedTypeSymbolComparer : IEqualityComparer<INamedTypeSymbol>
    {
        public static readonly NamedTypeSymbolComparer Instance = new NamedTypeSymbolComparer();
        public bool Equals(INamedTypeSymbol x, INamedTypeSymbol y) => SymbolEqualityComparer.Default.Equals(x, y);
        public int GetHashCode(INamedTypeSymbol obj) => SymbolEqualityComparer.Default.GetHashCode(obj);
    }

    private sealed class SymbolOwnerIndex
    {
        private readonly Dictionary<INamedTypeSymbol, List<SymbolOwner>> bySymbol =
            new Dictionary<INamedTypeSymbol, List<SymbolOwner>>(NamedTypeSymbolComparer.Instance);
        private readonly Dictionary<string, SortedSet<string>> typesByPath =
            new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        public void Add(INamedTypeSymbol symbol, string path, bool candidate)
        {
            if (!bySymbol.TryGetValue(symbol, out List<SymbolOwner> owners))
            {
                owners = new List<SymbolOwner>();
                bySymbol.Add(symbol, owners);
            }
            if (!owners.Any(owner => owner.Path == path)) owners.Add(new SymbolOwner(path, candidate));
            if (!typesByPath.TryGetValue(path, out SortedSet<string> types))
            {
                types = new SortedSet<string>(StringComparer.Ordinal);
                typesByPath.Add(path, types);
            }
            types.Add(symbol.ToDisplayString(TypeDisplayFormat));
        }

        public void Freeze()
        {
            foreach (List<SymbolOwner> owners in bySymbol.Values)
                owners.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        }

        public IReadOnlyList<SymbolOwner> Find(INamedTypeSymbol symbol) =>
            bySymbol.TryGetValue(symbol.OriginalDefinition, out List<SymbolOwner> owners)
                ? owners : Array.Empty<SymbolOwner>();

        public IEnumerable<string> TypesDeclaredAt(string path) =>
            typesByPath.TryGetValue(path, out SortedSet<string> types) ? types : Enumerable.Empty<string>();
    }

    private sealed class SymbolOwner
    {
        public readonly string Path;
        public readonly bool IsCandidate;
        public SymbolOwner(string path, bool candidate) { Path = path; IsCandidate = candidate; }
    }

    private sealed class SccResult
    {
        public SccComponent[] Components;

        public static SccResult Create(string[] nodes, IReadOnlyDictionary<string, string[]> adjacency)
        {
            int nextIndex = 0;
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
            Stack<string> stack = new Stack<string>();
            HashSet<string> onStack = new HashSet<string>(StringComparer.Ordinal);
            List<string[]> components = new List<string[]>();

            Action<string> visit = null;
            visit = node =>
            {
                indices[node] = nextIndex;
                lowLinks[node] = nextIndex++;
                stack.Push(node);
                onStack.Add(node);
                foreach (string target in adjacency[node])
                {
                    if (!indices.ContainsKey(target))
                    {
                        visit(target);
                        lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                    }
                    else if (onStack.Contains(target))
                    {
                        lowLinks[node] = Math.Min(lowLinks[node], indices[target]);
                    }
                }
                if (lowLinks[node] != indices[node]) return;
                List<string> component = new List<string>();
                string value;
                do
                {
                    value = stack.Pop();
                    onStack.Remove(value);
                    component.Add(value);
                } while (value != node);
                components.Add(component.OrderBy(path => path, StringComparer.Ordinal).ToArray());
            };

            foreach (string node in nodes) if (!indices.ContainsKey(node)) visit(node);
            SccComponent[] stable = components.OrderBy(component => component[0], StringComparer.Ordinal)
                .Select((component, index) => new SccComponent
                {
                    Id = "scc-" + (index + 1).ToString("D4"), Files = component
                }).ToArray();
            return new SccResult { Components = stable };
        }
    }

    private sealed class SccComponent
    {
        public string Id;
        public string[] Files;
    }

    private sealed class PlannerReport
    {
        public int SchemaVersion;
        public string InputMode;
        public string ResponseFile;
        public string LanguageVersion;
        public int SourceFileCount;
        public int CandidateFileCount;
        public int MetadataReferenceCount;
        public string[] MissingMetadataReferences;
        public string InputHash;
        public string NodeHash;
        public string EdgeHash;
        public string SccHash;
        public string MigrationOrderHash;
        public string GraphHash;
        public int EdgeCount;
        public int SccCount;
        public int CyclicSccCount;
        public string[] LeafCandidates;
        public FileNodeDto[] Files;
        public SccDto[] Sccs;
        public MigrationBatchDto[] MigrationBatches;
    }

    private sealed class FileNodeDto
    {
        public string Path;
        public string[] DeclaredTypes;
        public string SccId;
        public int SccSize;
        public bool IsLeaf;
        public EdgeDto[] Outgoing;
        public EdgeDto[] Incoming;
        public BoundaryReferenceDto[] BoundaryEgress;
        public BoundaryReferenceDto[] BoundaryIngress;
    }

    private sealed class EdgeDto
    {
        public string File;
        public int ReferenceCount;
        public string[] ReferencedTypes;
    }

    private sealed class BoundaryReferenceDto
    {
        public string Kind;
        public string Counterpart;
        public int ReferenceCount;
        public string[] ReferencedTypes;
    }

    private sealed class SccDto
    {
        public string Id;
        public bool IsCyclic;
        public string[] Files;
        public string[] IncomingSccIds;
        public string[] OutgoingSccIds;
    }

    private sealed class MigrationBatchDto
    {
        public int Order;
        public string SccId;
        public bool IsCyclic;
        public string[] Files;
    }

    private sealed class InputSet
    {
        public string InputMode;
        public string ResponsePath;
        public LanguageVersion LanguageVersion;
        public string[] Defines;
        public MetadataReference[] References;
        public string[] MissingReferences;
        public SourceInput[] Sources;
        public string InputHash;
    }

    private sealed class SourceInput
    {
        public string Path;
        public string Text;
        public bool IsCandidate;
        public static SourceInput Test(string path, string text, bool candidate) =>
            new SourceInput { Path = path, Text = text, IsCandidate = candidate };
    }

    private static class InputLoader
    {
        public static InputSet Load(string projectRoot, string explicitResponsePath)
        {
            string responsePath = explicitResponsePath;
            if (string.IsNullOrWhiteSpace(responsePath)) responsePath = FindNewestResponseFile(projectRoot);
            if (!string.IsNullOrWhiteSpace(responsePath) && File.Exists(responsePath))
            {
                try
                {
                    return LoadResponseFile(projectRoot, responsePath);
                }
                catch (FileNotFoundException)
                {
                    // Source moves make Bee's last response file stale until Unity refreshes.
                    // A current filesystem fallback is more useful than forcing every batch
                    // to manufacture a temporary response file or compile the whole project.
                }
            }

            return LoadFallback(projectRoot);
        }

        private static string FindNewestResponseFile(string projectRoot)
        {
            string artifacts = Path.Combine(projectRoot, "Library", "Bee", "artifacts");
            if (!Directory.Exists(artifacts)) return null;
            return Directory.GetFiles(artifacts, "Assembly-CSharp.rsp", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static InputSet LoadResponseFile(string projectRoot, string responsePath)
        {
            string[] lines = File.ReadAllLines(responsePath);
            List<string> defines = new List<string>();
            List<string> sourcePaths = new List<string>();
            List<string> referencePaths = new List<string>();
            LanguageVersion language = LanguageVersion.CSharp9;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (TryOption(line, "define", out string defineValue))
                    defines.AddRange(Unquote(defineValue).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                else if (TryOption(line, "r", out string reference)
                    || TryOption(line, "reference", out reference))
                    referencePaths.Add(ResolvePath(projectRoot, Unquote(reference)));
                else if (TryOption(line, "langversion", out string languageValue)
                    && LanguageVersionFacts.TryParse(Unquote(languageValue), out LanguageVersion parsed))
                    language = parsed;
                else if (!line.StartsWith("-", StringComparison.Ordinal)
                    && !line.StartsWith("/", StringComparison.Ordinal)
                    && Unquote(line).EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    sourcePaths.Add(ResolvePath(projectRoot, Unquote(line)));
            }
            return CreateInput("bee-rsp", projectRoot, responsePath, language, defines, sourcePaths, referencePaths, lines);
        }

        private static InputSet LoadFallback(string projectRoot)
        {
            string scripts = Path.Combine(projectRoot, "Assets", "Scripts");
            if (!Directory.Exists(scripts)) throw new DirectoryNotFoundException(scripts);
            string[] sources = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(path => IsDefaultAssemblyFallback(path, scripts))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            List<string> references = new List<string>();
            AddDlls(references, Path.Combine(projectRoot, "Library", "ScriptAssemblies"), SearchOption.TopDirectoryOnly);
            string unityData = ResolveUnityData(projectRoot);
            AddDlls(references, Path.Combine(unityData, "Managed"), SearchOption.AllDirectories);
            AddDlls(references, Path.Combine(unityData, "NetStandard", "ref", "2.1.0"), SearchOption.TopDirectoryOnly);
            return CreateInput("project-fallback", projectRoot, string.Empty, LanguageVersion.CSharp9,
                Array.Empty<string>(), sources, references, Array.Empty<string>());
        }

        private static InputSet CreateInput(
            string mode,
            string projectRoot,
            string responsePath,
            LanguageVersion language,
            IEnumerable<string> defines,
            IEnumerable<string> sourcePaths,
            IEnumerable<string> referencePaths,
            IEnumerable<string> responseLines)
        {
            List<string> missingSources = new List<string>();
            SourceInput[] sources = sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => NormalizePath(projectRoot, path), StringComparer.Ordinal)
                .Select(path =>
                {
                    if (!File.Exists(path)) { missingSources.Add(path); return null; }
                    string relative = NormalizePath(projectRoot, path);
                    return new SourceInput
                    {
                        Path = relative,
                        Text = File.ReadAllText(path),
                        IsCandidate = relative.StartsWith(CandidateRoot, StringComparison.Ordinal)
                    };
                }).Where(value => value != null).ToArray();
            if (missingSources.Count > 0)
                throw new FileNotFoundException("Response file contains missing sources: " + string.Join(", ", missingSources.Take(5)));

            List<string> missing = new List<string>();
            List<MetadataReference> references = new List<MetadataReference>();
            foreach (string path in referencePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!File.Exists(path)) { missing.Add(NormalizePath(projectRoot, path)); continue; }
                try { references.Add(MetadataReference.CreateFromFile(path)); }
                catch (BadImageFormatException) { missing.Add(NormalizePath(projectRoot, path) + " [bad image]"); }
            }
            string[] inputValues = responseLines.Select(line => "rsp|" + line)
                .Concat(sources.Select(source => "src|" + source.Path + "|" + HashValues(new[] { source.Text })))
                .Concat(missing.Select(path => "missing|" + path)).ToArray();
            return new InputSet
            {
                InputMode = mode,
                ResponsePath = string.IsNullOrEmpty(responsePath) ? string.Empty : NormalizePath(projectRoot, responsePath),
                LanguageVersion = language,
                Defines = defines.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                References = references.ToArray(),
                MissingReferences = missing.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Sources = sources,
                InputHash = HashValues(inputValues)
            };
        }

        private static bool TryOption(string line, string name, out string value)
        {
            string dash = "-" + name + ":";
            string slash = "/" + name + ":";
            if (line.StartsWith(dash, StringComparison.OrdinalIgnoreCase))
            {
                value = line.Substring(dash.Length); return true;
            }
            if (line.StartsWith(slash, StringComparison.OrdinalIgnoreCase))
            {
                value = line.Substring(slash.Length); return true;
            }
            value = null; return false;
        }

        private static string Unquote(string value)
        {
            string trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"'
                ? trimmed.Substring(1, trimmed.Length - 2) : trimmed;
        }

        private static string ResolvePath(string root, string path) =>
            Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));

        private static string NormalizePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(fullRoot.Length).Replace('\\', '/')
                : fullPath.Replace('\\', '/');
        }

        private static bool IsDefaultAssemblyFallback(string path, string scriptsRoot)
        {
            DirectoryInfo current = new FileInfo(path).Directory;
            string root = Path.GetFullPath(scriptsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            while (current != null && (current.FullName + Path.DirectorySeparatorChar)
                .StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.GetFiles(current.FullName, "*.asmdef").Length > 0
                    || Directory.GetFiles(current.FullName, "*.asmref").Length > 0) return false;
                current = current.Parent;
            }
            return true;
        }

        private static void AddDlls(ICollection<string> output, string root, SearchOption option)
        {
            if (!Directory.Exists(root)) return;
            foreach (string path in Directory.GetFiles(root, "*.dll", option)) output.Add(path);
        }

        private static string ResolveUnityData(string projectRoot)
        {
            string versionFile = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            string line = File.ReadLines(versionFile).First(value => value.StartsWith("m_EditorVersion:", StringComparison.Ordinal));
            string version = line.Substring(line.IndexOf(':') + 1).Trim();
            return Path.Combine("C:\\Program Files\\Unity\\Hub\\Editor", version, "Editor", "Data");
        }
    }
}
