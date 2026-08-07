using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ArchitectureMetricsAnalyzer
{
    private const int SchemaVersion = 2;
    private const int OwnershipManifestSchemaVersion = 1;
    // File length and collaborator count are review signals, not architecture
    // defects by themselves. Keep the original limits visible in the report,
    // while reserving the release gate for extreme cases that require an
    // explicit structural decision.
    private const int RuntimeReviewLineLimit = 1200;
    private const int BehaviourReviewLineLimit = 800;
    private const int HardTypeLineLimit = 2000;
    private const int ConstructorReviewDependencyLimit = 8;
    private const int ConstructorHardDependencyLimit = 16;

    private static readonly string[] ContentEscapeTokens =
    {
        "ScriptableObject.CreateInstance",
        "Resources.LoadAll",
        "GetDefinitionOrDefault",
        "CreateRuntimeDefaults",
        "CreateFallbackDefinitions",
        "FromStockCategory"
    };

    private static readonly string[] SessionMutationMembers =
    {
        "money",
        "currentMoney",
        "currentDay",
        "gameSpeed",
        "isPaused"
    };

    private static readonly string[] SessionReactiveMembers =
    {
        "holdingMoney",
        "day",
        "gameSpeed",
        "curTime",
        "hour",
        "timeOfDay"
    };

    private static readonly HashSet<string> ApprovedSessionMutationOwners =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Assets/Scripts/Models/GameData/GameSessionState.cs",
            "Assets/Scripts/Services/Infrastructure/GameRuntimeServices.cs",
            "Assets/Scripts/Services/Infrastructure/GameSessionRuntimeServices.cs"
        };

    // The architectural limit concerns injected collaborators on operational
    // objects. Snapshot/result/request DTOs and immutable command definitions
    // may legitimately expose more than eight scalar fields and are not
    // dependency-injection surfaces. Operational command handlers are already
    // covered by Runtime/Service/Controller/Executor/etc. suffixes.
    private static readonly string[] DependencyOwnerSuffixes =
    {
        "Runtime",
        "Service",
        "Coordinator",
        "System",
        "Manager",
        "Controller",
        "Presenter",
        "Repository",
        "Store",
        "Gateway",
        "Provider",
        "Factory",
        "Bridge",
        "Scheduler",
        "Executor",
        "Applier",
        "Engine",
        "Section",
        "References",
        "Query",
        "Registry"
    };

    private static readonly string[] CompositionRootSuffixes =
    {
        "LifetimeScope",
        "CompositionRoot",
        "Installer",
        "Bootstrap"
    };

    private static readonly string[] DomainAuthoritySuffixes =
    {
        "Aggregate",
        "AggregateState",
        "State",
        "StateStore",
        "Store",
        "Rules",
        "Rule",
        "Policy",
        "Calculator",
        "Catalog",
        "Content",
        "Definition",
        "DefinitionSO",
        "SettingsSO",
        "Persistence",
        "Persistent",
        "SaveSection",
        "Command",
        "CommandHandler",
        "Query",
        "QueryService",
        "Snapshot",
        "Dto",
        "Ability",
        "Actor",
        "Blackboard",
        "Ledger",
        "Registry",
        "Planner",
        "Selector",
        "Evaluator",
        "Resolver",
        "Handler",
        "Executor",
        "Coordinator"
    };

    private static readonly string[] UnityEdgeSuffixes =
    {
        "Adapter",
        "Bridge",
        "Controller",
        "Factory",
        "Panel",
        "Presenter",
        "Renderer",
        "View",
        "ViewFactory",
        "Ui",
        "UI"
    };

    public static int Main(string[] args)
    {
        try
        {
            Arguments options = Arguments.Parse(args);
            Snapshot snapshot = Analyze(options.ProjectRoot, options.OwnershipManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath));
            File.WriteAllText(options.ReportPath, snapshot.ToJson(), new UTF8Encoding(false));
            Directory.CreateDirectory(Path.GetDirectoryName(options.OwnershipReportPath));
            File.WriteAllText(
                options.OwnershipReportPath,
                snapshot.ToOwnershipJson(),
                new UTF8Encoding(false));

            if (options.WriteBaseline)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(options.BaselinePath));
                File.WriteAllText(
                    options.BaselinePath,
                    snapshot.ToBaselineJson(),
                    new UTF8Encoding(false));
            }

            if (options.Verify)
            {
                VerifyBaseline(snapshot, options.BaselinePath);
            }

            Console.WriteLine(snapshot.ToConsoleSummary());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static Snapshot Analyze(string projectRoot, string ownershipManifestPath)
    {
        string scriptsRoot = Path.Combine(projectRoot, "Assets", "Scripts");
        if (!Directory.Exists(scriptsRoot))
        {
            throw new DirectoryNotFoundException(scriptsRoot);
        }

        string[] sourcePaths = Directory.GetFiles(
                scriptsRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsEditorSource(path))
            .OrderBy(path => NormalizeRelative(projectRoot, path), StringComparer.Ordinal)
            .ToArray();

        List<string> mutableStatics = new List<string>();
        List<string> reviewTypes = new List<string>();
        List<string> oversizedTypes = new List<string>();
        List<string> reviewConstructors = new List<string>();
        List<string> largeConstructors = new List<string>();
        List<string> defaultAssemblyFiles = new List<string>();
        List<DefaultOwnershipFinding> defaultOwnershipFindings =
            new List<DefaultOwnershipFinding>();
        List<string> contentEscapes = new List<string>();
        List<string> directSessionMutations = new List<string>();
        List<string> rawKoreanStrings = new List<string>();
        List<string> rootCatalogReferences = new List<string>();
        int typeCount = 0;

        foreach (string sourcePath in sourcePaths)
        {
            string relativePath = NormalizeRelative(projectRoot, sourcePath);
            string source = File.ReadAllText(sourcePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Latest),
                relativePath,
                Encoding.UTF8);
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
            TypeDeclarationSyntax[] types = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .ToArray();
            typeCount += types.Length;

            if (ResolveAssemblyName(sourcePath, scriptsRoot) == "Assembly-CSharp")
            {
                defaultAssemblyFiles.Add(relativePath);
                defaultOwnershipFindings.Add(
                    ClassifyDefaultOwnership(relativePath, root));
            }

            foreach (TypeDeclarationSyntax type in types)
            {
                string typeName = GetTypeName(type);
                int lines = tree.GetLineSpan(type.Span).EndLinePosition.Line
                    - tree.GetLineSpan(type.Span).StartLinePosition.Line + 1;
                bool behaviourOrPresenter = type.Identifier.ValueText.EndsWith(
                        "Presenter",
                        StringComparison.Ordinal)
                    || (type.BaseList != null && type.BaseList.Types.Any(baseType =>
                        baseType.Type.ToString().EndsWith(
                            "MonoBehaviour",
                            StringComparison.Ordinal)));
                int reviewLimit = behaviourOrPresenter
                    ? BehaviourReviewLineLimit
                    : RuntimeReviewLineLimit;
                if (lines > reviewLimit)
                {
                    reviewTypes.Add(
                        relativePath + "|" + typeName + "|" + lines + ">" + reviewLimit);
                }
                if (lines > HardTypeLineLimit)
                {
                    oversizedTypes.Add(
                        relativePath + "|" + typeName + "|" + lines + ">" + HardTypeLineLimit);
                }
            }

            foreach (FieldDeclarationSyntax field in root.DescendantNodes()
                         .OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(SyntaxKind.StaticKeyword)
                    || field.Modifiers.Any(SyntaxKind.ConstKeyword)
                    || field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                    || HasThreadStaticAttribute(field.AttributeLists)
                    || IsApprovedRuntimeRebuildableCache(field))
                {
                    continue;
                }

                string owner = GetContainingTypeName(field);
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    mutableStatics.Add(
                        relativePath + "|" + owner + "." + variable.Identifier.ValueText);
                }
            }

            foreach (PropertyDeclarationSyntax property in root.DescendantNodes()
                         .OfType<PropertyDeclarationSyntax>())
            {
                if (!property.Modifiers.Any(SyntaxKind.StaticKeyword)
                    || property.AccessorList == null
                    || !property.AccessorList.Accessors.Any(accessor =>
                        accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                        || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)))
                {
                    continue;
                }

                mutableStatics.Add(
                    relativePath + "|" + GetContainingTypeName(property)
                    + "." + property.Identifier.ValueText + "{set}");
            }

            foreach (EventFieldDeclarationSyntax eventField in root.DescendantNodes()
                         .OfType<EventFieldDeclarationSyntax>())
            {
                if (!eventField.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    continue;
                }

                string owner = GetContainingTypeName(eventField);
                foreach (VariableDeclaratorSyntax variable in
                         eventField.Declaration.Variables)
                {
                    mutableStatics.Add(
                        relativePath + "|" + owner + "."
                        + variable.Identifier.ValueText + "{event}");
                }
            }

            foreach (ConstructorDeclarationSyntax constructor in root.DescendantNodes()
                         .OfType<ConstructorDeclarationSyntax>())
            {
                int count = constructor.ParameterList.Parameters.Count;
                if (!IsDependencyInjectionOwner(constructor))
                {
                    continue;
                }
                string finding = relativePath + "|" + GetContainingTypeName(constructor)
                    + ".ctor|" + count;
                if (count > ConstructorReviewDependencyLimit)
                {
                    reviewConstructors.Add(finding);
                }
                if (count > ConstructorHardDependencyLimit)
                {
                    largeConstructors.Add(finding);
                }
            }

            foreach (string token in ContentEscapeTokens)
            {
                int count = CountOrdinal(source, token);
                if (count > 0)
                {
                    contentEscapes.Add(relativePath + "|" + token + "|" + count);
                }
            }

            foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes()
                         .OfType<AssignmentExpressionSyntax>())
            {
                string left = assignment.Left.ToString();
                if (!ApprovedSessionMutationOwners.Contains(relativePath)
                    && IsSessionMutationTarget(left))
                {
                    directSessionMutations.Add(
                        relativePath + "|" + GetContainingTypeName(assignment)
                        + "|" + NormalizeSyntax(left));
                }
            }


            foreach (ExpressionSyntax mutation in root.DescendantNodes()
                         .Where(node => node is PrefixUnaryExpressionSyntax prefix
                             && (prefix.IsKind(SyntaxKind.PreIncrementExpression)
                                 || prefix.IsKind(SyntaxKind.PreDecrementExpression))
                             || node is PostfixUnaryExpressionSyntax postfix
                             && (postfix.IsKind(SyntaxKind.PostIncrementExpression)
                                 || postfix.IsKind(SyntaxKind.PostDecrementExpression)))
                         .Cast<ExpressionSyntax>())
            {
                string operand = mutation switch
                {
                    PrefixUnaryExpressionSyntax prefix => prefix.Operand.ToString(),
                    PostfixUnaryExpressionSyntax postfix => postfix.Operand.ToString(),
                    _ => string.Empty
                };
                if (!ApprovedSessionMutationOwners.Contains(relativePath)
                    && IsSessionMutationTarget(operand))
                {
                    directSessionMutations.Add(
                        relativePath + "|" + GetContainingTypeName(mutation)
                        + "|" + NormalizeSyntax(operand));
                }
            }

            int catalogOrdinal = 0;
            foreach (IdentifierNameSyntax identifier in root.DescendantNodes()
                         .OfType<IdentifierNameSyntax>()
                         .Where(identifier => identifier.Identifier.ValueText
                             == "GameContentCatalogSO"))
            {
                rootCatalogReferences.Add(
                    relativePath + "|" + GetContainingTypeName(identifier)
                    + "|" + catalogOrdinal++);
            }

            foreach (LiteralExpressionSyntax literal in root.DescendantNodes()
                         .OfType<LiteralExpressionSyntax>()
                         .Where(literal => literal.IsKind(
                             SyntaxKind.StringLiteralExpression)))
            {
                string value = literal.Token.ValueText;
                if (!Regex.IsMatch(value, "[가-힣]"))
                {
                    continue;
                }

                rawKoreanStrings.Add(
                    relativePath + "|" + GetContainingTypeName(literal)
                    + "|" + HashValues(new[] { value }));
            }

            // Interpolated string text is represented by a distinct Roslyn
            // node, so scanning string literals alone silently undercounts
            // visible localization debt such as $"우선순위 {value}".
            foreach (InterpolatedStringTextSyntax text in root.DescendantNodes()
                         .OfType<InterpolatedStringTextSyntax>())
            {
                string value = text.TextToken.ValueText;
                if (!Regex.IsMatch(value, "[가-힣]"))
                {
                    continue;
                }

                rawKoreanStrings.Add(
                    relativePath + "|" + GetContainingTypeName(text)
                    + "|" + HashValues(new[] { value }));
            }
        }

        Dictionary<string, OwnershipOverrideEntry> ownershipOverrides =
            LoadAndValidateOwnershipManifest(
                projectRoot,
                scriptsRoot,
                ownershipManifestPath,
                defaultAssemblyFiles);
        ApplyOwnershipOverrides(defaultOwnershipFindings, ownershipOverrides);

        string[] unapprovedDefaultDomainAuthorities = defaultOwnershipFindings
            .Where(finding => finding.Classification != OwnershipClassification.DefaultAllowed)
            .Select(finding => finding.Path + "|" + finding.Classification)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CrossDomainCycleCandidate[] crossDomainCycleCandidates =
            defaultOwnershipFindings
                .Where(finding =>
                    finding.Classification != OwnershipClassification.DefaultAllowed
                    && string.IsNullOrWhiteSpace(finding.OverrideRationale)
                    && finding.ReferencedDomains.Length >= 2)
                .Select(finding => new CrossDomainCycleCandidate
                {
                    Path = finding.Path,
                    Classification = finding.Classification.ToString(),
                    ReferencedDomains = finding.ReferencedDomains
                })
                .OrderBy(candidate => candidate.Path, StringComparer.Ordinal)
                .ToArray();

        return new Snapshot
        {
            SourceFingerprint = HashSourceFiles(projectRoot, sourcePaths),
            RuntimeSourceFileCount = sourcePaths.Length,
            RuntimeTypeCount = typeCount,
            MutableStaticFieldCount = mutableStatics.Count,
            MutableStaticSetHash = HashValues(mutableStatics),
            MutableStatics = mutableStatics.ToArray(),
            ReviewTypeCount = reviewTypes.Count,
            ReviewTypes = reviewTypes.ToArray(),
            OversizedTypeCount = oversizedTypes.Count,
            OversizedTypeSetHash = HashValues(oversizedTypes),
            OversizedTypes = oversizedTypes.ToArray(),
            ReviewConstructorCount = reviewConstructors.Count,
            ReviewConstructors = reviewConstructors.ToArray(),
            LargeConstructorCount = largeConstructors.Count,
            LargeConstructorSetHash = HashValues(largeConstructors),
            LargeConstructors = largeConstructors.ToArray(),
            DefaultAssemblySourceFileCount = defaultAssemblyFiles.Count,
            DefaultAssemblySourceSetHash = HashValues(defaultAssemblyFiles),
            DefaultAssemblySources = defaultAssemblyFiles.ToArray(),
            DefaultOwnershipFindings = defaultOwnershipFindings
                .OrderBy(finding => finding.Path, StringComparer.Ordinal)
                .ToArray(),
            DefaultAllowedCount = defaultOwnershipFindings.Count(finding =>
                finding.Classification == OwnershipClassification.DefaultAllowed),
            NamedRequiredCount = defaultOwnershipFindings.Count(finding =>
                finding.Classification == OwnershipClassification.NamedRequired),
            ReviewRequiredCount = defaultOwnershipFindings.Count(finding =>
                finding.Classification == OwnershipClassification.ReviewRequired),
            UnapprovedDefaultDomainAuthorityCount =
                unapprovedDefaultDomainAuthorities.Length,
            UnapprovedDefaultDomainAuthoritySetHash =
                HashValues(unapprovedDefaultDomainAuthorities),
            UnapprovedDefaultDomainAuthorities = unapprovedDefaultDomainAuthorities,
            CrossDomainCycleCandidates = crossDomainCycleCandidates,
            ContentEscapeCount = contentEscapes.Count,
            ContentEscapeSetHash = HashValues(contentEscapes),
            ContentEscapes = contentEscapes.ToArray(),
            DirectSessionMutationCount = directSessionMutations.Count,
            DirectSessionMutationSetHash = HashValues(directSessionMutations),
            DirectSessionMutations = directSessionMutations.ToArray(),
            RawKoreanStringCount = rawKoreanStrings.Count,
            RawKoreanStringSetHash = HashValues(rawKoreanStrings),
            RawKoreanStringFiles = rawKoreanStrings
                .GroupBy(value => value.Split('|')[0], StringComparer.Ordinal)
                .Select(group => group.Key + "|" + group.Count())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            RootCatalogReferenceCount = rootCatalogReferences.Count,
            RootCatalogReferenceSetHash = HashValues(rootCatalogReferences),
            RootCatalogReferences = rootCatalogReferences.ToArray()
        };
    }

    private static DefaultOwnershipFinding ClassifyDefaultOwnership(
        string relativePath,
        CompilationUnitSyntax root)
    {
        List<string> authorityEvidence = new List<string>();
        List<string> edgeEvidence = new List<string>();
        bool approvedEdgeShape = false;
        bool presentationPath = relativePath.IndexOf(
            "/Views/",
            StringComparison.Ordinal) >= 0
            || relativePath.IndexOf(
                "/Presentation/",
                StringComparison.Ordinal) >= 0;
        bool unityIoPath = relativePath.IndexOf(
            "/Input/",
            StringComparison.Ordinal) >= 0
            || relativePath.IndexOf(
                "/Camera/",
                StringComparison.Ordinal) >= 0
            || relativePath.IndexOf(
                "/Audio/",
                StringComparison.Ordinal) >= 0
            || relativePath.IndexOf(
                "/VFX/",
                StringComparison.Ordinal) >= 0;
        bool unityEdgePath = presentationPath || unityIoPath;
        TypeDeclarationSyntax[] types = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .ToArray();

        foreach (TypeDeclarationSyntax type in types)
        {
            string name = type.Identifier.ValueText;
            string[] baseTypes = type.BaseList == null
                ? Array.Empty<string>()
                : type.BaseList.Types.Select(value => value.Type.ToString()).ToArray();
            bool monoBehaviour = baseTypes.Any(value =>
                value.EndsWith("MonoBehaviour", StringComparison.Ordinal)
                || value.EndsWith("BuildableObject", StringComparison.Ordinal));
            bool scriptableObject = baseTypes.Any(value =>
                value.EndsWith("ScriptableObject", StringComparison.Ordinal));
            bool strictSaveAdapter = name.EndsWith(
                    "SaveSection",
                    StringComparison.Ordinal)
                && baseTypes.Any(value => value.IndexOf(
                    "DungeonStrictJsonSaveSection",
                    StringComparison.Ordinal) >= 0);
            bool saveApplicationAdapter = name.EndsWith(
                    "SaveService",
                    StringComparison.Ordinal)
                && baseTypes.Any(value => value.StartsWith(
                        "I",
                        StringComparison.Ordinal)
                    && value.EndsWith(
                        "SaveService",
                        StringComparison.Ordinal));

            if (scriptableObject)
            {
                authorityEvidence.Add("type:" + name + ":ScriptableObject-content-authority");
            }
            if (!strictSaveAdapter && DomainAuthoritySuffixes.Any(suffix =>
                    name.EndsWith(suffix, StringComparison.Ordinal)))
            {
                authorityEvidence.Add("type:" + name + ":authority-suffix");
            }
            if (name.StartsWith("Ability", StringComparison.Ordinal))
            {
                authorityEvidence.Add("type:" + name + ":domain-ability-role");
            }
            if (name.EndsWith("SO", StringComparison.Ordinal))
            {
                authorityEvidence.Add("type:" + name + ":SO-definition-authority");
            }

            bool runtimeOrService = name.EndsWith("Runtime", StringComparison.Ordinal)
                || name.EndsWith("Service", StringComparison.Ordinal)
                || name.EndsWith("System", StringComparison.Ordinal);
            if (runtimeOrService && !saveApplicationAdapter)
            {
                authorityEvidence.Add("type:" + name + ":runtime-service-role");
            }

            // Mutable fields in a View/Input/Camera/Audio/VFX source are normally
            // transient presentation or device state. Treating every float,
            // string, and List on a MonoBehaviour as gameplay authority made the
            // ownership gate reward mechanical file movement. Explicit authority
            // roles (Runtime, Service, Policy, StateStore, SO, and so on) remain
            // evidence above and still force review or named ownership.
            if (!unityEdgePath && HasMutableDomainState(type, monoBehaviour))
            {
                authorityEvidence.Add("type:" + name + ":mutable-domain-state-shape");
            }

            bool compositionRoot = CompositionRootSuffixes.Any(suffix =>
                name.EndsWith(suffix, StringComparison.Ordinal));
            bool compositionRegistration =
                IsPureCompositionRegistration(relativePath, type);
            bool namedUnityEdge = UnityEdgeSuffixes.Any(suffix =>
                name.EndsWith(suffix, StringComparison.Ordinal));
            if (monoBehaviour)
            {
                edgeEvidence.Add("type:" + name + ":MonoBehaviour-scene-edge");
                approvedEdgeShape = true;
            }
            if (compositionRoot)
            {
                edgeEvidence.Add("type:" + name + ":composition-root");
                approvedEdgeShape = true;
            }
            if (compositionRegistration)
            {
                edgeEvidence.Add(
                    "type:" + name + ":composition-registration");
                approvedEdgeShape = true;
            }
            if (namedUnityEdge)
            {
                edgeEvidence.Add("type:" + name + ":unity-edge-suffix");
                if (name.EndsWith("Presenter", StringComparison.Ordinal)
                    || name.EndsWith("Panel", StringComparison.Ordinal)
                    || name.EndsWith("Renderer", StringComparison.Ordinal)
                    || name.EndsWith("View", StringComparison.Ordinal)
                    || name.EndsWith("Ui", StringComparison.Ordinal)
                    || name.EndsWith("UI", StringComparison.Ordinal)
                    || name.EndsWith("ApplicationAdapter", StringComparison.Ordinal)
                    || name.IndexOf("UnityAdapter", StringComparison.Ordinal) >= 0
                    || name.IndexOf("UnityBridge", StringComparison.Ordinal) >= 0)
                {
                    approvedEdgeShape = true;
                }
            }
            if (strictSaveAdapter)
            {
                edgeEvidence.Add("type:" + name + ":strict-save-adapter");
                approvedEdgeShape = true;
            }
            if (saveApplicationAdapter)
            {
                edgeEvidence.Add("type:" + name + ":save-application-adapter");
                approvedEdgeShape = true;
            }
        }

        if (!unityEdgePath)
        {
            foreach (EnumDeclarationSyntax enumDeclaration in root.DescendantNodes()
                         .OfType<EnumDeclarationSyntax>())
            {
                authorityEvidence.Add(
                    "enum:" + enumDeclaration.Identifier.ValueText + ":domain-protocol");
            }
            foreach (DelegateDeclarationSyntax delegateDeclaration in root.DescendantNodes()
                         .OfType<DelegateDeclarationSyntax>())
            {
                authorityEvidence.Add(
                    "delegate:" + delegateDeclaration.Identifier.ValueText
                    + ":domain-contract");
            }
        }

        if (relativePath.IndexOf("/Models/", StringComparison.Ordinal) >= 0)
        {
            authorityEvidence.Add("source-role:domain-model-path");
        }
        if (relativePath.IndexOf("/SO/", StringComparison.Ordinal) >= 0)
        {
            authorityEvidence.Add("source-role:SO-content-path");
        }

        if (presentationPath)
        {
            edgeEvidence.Add("source-role:presentation-path");
            approvedEdgeShape = true;
        }
        if (unityIoPath)
        {
            edgeEvidence.Add("source-role:unity-io-path");
            approvedEdgeShape = true;
        }

        string[] authority = authorityEvidence
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] edge = edgeEvidence
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        OwnershipClassification classification;
        if (authority.Length > 0 && edge.Length > 0)
        {
            classification = OwnershipClassification.ReviewRequired;
        }
        else if (authority.Length > 0)
        {
            classification = OwnershipClassification.NamedRequired;
        }
        else if (approvedEdgeShape && edge.Length > 0 && types.Length > 0)
        {
            classification = OwnershipClassification.DefaultAllowed;
        }
        else
        {
            classification = OwnershipClassification.ReviewRequired;
        }

        return new DefaultOwnershipFinding
        {
            Path = relativePath,
            AutomaticClassification = classification,
            Classification = classification,
            Evidence = authority.Concat(edge).ToArray(),
            ReferencedDomains = FindReferencedDomains(root),
            OverrideRationale = string.Empty,
            ReviewedBy = string.Empty
        };
    }

    private static bool HasMutableDomainState(
        TypeDeclarationSyntax type,
        bool monoBehaviour)
    {
        foreach (FieldDeclarationSyntax field in type.Members
                     .OfType<FieldDeclarationSyntax>())
        {
            if (field.Modifiers.Any(SyntaxKind.StaticKeyword)
                || field.Modifiers.Any(SyntaxKind.ConstKeyword)
                || field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                || IsApprovedApplicationAdapterTransientState(type, field))
            {
                continue;
            }

            if (!monoBehaviour || IsDomainStateTypeShape(field.Declaration.Type.ToString()))
            {
                return true;
            }
        }

        foreach (PropertyDeclarationSyntax property in type.Members
                     .OfType<PropertyDeclarationSyntax>())
        {
            bool writable = property.AccessorList != null
                && property.AccessorList.Accessors.Any(accessor =>
                    accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
            if (!writable)
            {
                continue;
            }

            if (!monoBehaviour || IsDomainStateTypeShape(property.Type.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPureCompositionRegistration(
        string relativePath,
        TypeDeclarationSyntax type)
    {
        if (relativePath.IndexOf(
                "/Services/Infrastructure/Registration/",
                StringComparison.Ordinal) < 0
            || type is not ClassDeclarationSyntax classDeclaration
            || !classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            || !classDeclaration.Identifier.ValueText.EndsWith(
                "Registration",
                StringComparison.Ordinal))
        {
            return false;
        }

        MethodDeclarationSyntax[] methods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .ToArray();
        if (methods.Length == 0
            || classDeclaration.Members.Any(member =>
                member is not MethodDeclarationSyntax))
        {
            return false;
        }

        foreach (MethodDeclarationSyntax method in methods)
        {
            if (!method.Modifiers.Any(SyntaxKind.StaticKeyword)
                || !string.Equals(
                    NormalizeSyntax(method.ReturnType.ToString()),
                    "void",
                    StringComparison.Ordinal)
                || !method.Identifier.ValueText.StartsWith(
                    "Register",
                    StringComparison.Ordinal)
                || method.ParameterList.Parameters.Count == 0
                || !NormalizeSyntax(
                        method.ParameterList.Parameters[0].Type?.ToString()
                            ?? string.Empty)
                    .EndsWith("IContainerBuilder", StringComparison.Ordinal)
                || method.Body == null
                || method.DescendantNodes().Any(node =>
                    node is AssignmentExpressionSyntax
                    || node is LocalDeclarationStatementSyntax
                    || node is LocalFunctionStatementSyntax
                    || node is ForStatementSyntax
                    || node is ForEachStatementSyntax
                    || node is WhileStatementSyntax
                    || node is DoStatementSyntax
                    || node is SwitchStatementSyntax))
            {
                return false;
            }

            foreach (InvocationExpressionSyntax invocation in
                     method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string invokedName = GetInvokedMethodName(invocation.Expression);
                bool registrationCall = invokedName.StartsWith(
                    "Register",
                    StringComparison.Ordinal);
                bool exposureCall = string.Equals(
                        invokedName,
                        "As",
                        StringComparison.Ordinal)
                    || string.Equals(
                        invokedName,
                        "AsSelf",
                        StringComparison.Ordinal);
                bool sceneCapabilityCheck = string.Equals(
                    invokedName,
                    "SupportsScene",
                    StringComparison.Ordinal);
                bool compileTimeName = string.Equals(
                    invokedName,
                    "nameof",
                    StringComparison.Ordinal);
                if (!registrationCall
                    && !exposureCall
                    && !sceneCapabilityCheck
                    && !compileTimeName)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string GetInvokedMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static bool IsApprovedApplicationAdapterTransientState(
        TypeDeclarationSyntax owner,
        FieldDeclarationSyntax field)
    {
        bool marked = field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute =>
                attribute.Name.ToString().EndsWith(
                    "ApplicationAdapterTransientState",
                    StringComparison.Ordinal)
                || attribute.Name.ToString().EndsWith(
                    "ApplicationAdapterTransientStateAttribute",
                    StringComparison.Ordinal));
        if (!marked
            || !field.Modifiers.Any(SyntaxKind.PrivateKeyword)
            || !owner.Identifier.ValueText.EndsWith(
                "ApplicationAdapter",
                StringComparison.Ordinal))
        {
            return false;
        }

        string type = NormalizeSyntax(field.Declaration.Type.ToString());
        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
        {
            string name = variable.Identifier.ValueText;
            bool subscription = string.Equals(type, "IDisposable", StringComparison.Ordinal)
                && name.EndsWith("Subscription", StringComparison.Ordinal);
            bool reentrancyGuard = string.Equals(type, "bool", StringComparison.Ordinal)
                && (name.StartsWith("synchronizing", StringComparison.Ordinal)
                    || name.StartsWith("applying", StringComparison.Ordinal));
            bool projectionRevision = string.Equals(type, "int", StringComparison.Ordinal)
                && name.EndsWith("Revision", StringComparison.Ordinal);
            if (!subscription && !reentrancyGuard && !projectionRevision)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDomainStateTypeShape(string typeName)
    {
        string normalized = NormalizeSyntax(typeName);
        string[] scalarTypes =
        {
            "bool", "byte", "sbyte", "short", "ushort", "int", "uint",
            "long", "ulong", "float", "double", "decimal", "string",
            "DateTime", "Guid"
        };
        if (scalarTypes.Any(value => string.Equals(
                normalized.TrimEnd('?'),
                value,
                StringComparison.Ordinal)))
        {
            return true;
        }

        return normalized.IndexOf("Dictionary<", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("HashSet<", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("Queue<", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("Stack<", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("List<", StringComparison.Ordinal) >= 0
            || normalized.EndsWith("State", StringComparison.Ordinal)
            || normalized.EndsWith("State?", StringComparison.Ordinal)
            || normalized.EndsWith("Snapshot", StringComparison.Ordinal)
            || normalized.EndsWith("Dto", StringComparison.Ordinal);
    }

    private static string[] FindReferencedDomains(CompilationUnitSyntax root)
    {
        HashSet<string> domains = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
                     root.ToFullString(),
                     "\\bDungeonStory\\.(?<domain>[A-Z][A-Za-z0-9_]*)"))
        {
            domains.Add(match.Groups["domain"].Value);
        }
        return domains.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, OwnershipOverrideEntry>
        LoadAndValidateOwnershipManifest(
            string projectRoot,
            string scriptsRoot,
            string manifestPath,
            IReadOnlyCollection<string> defaultAssemblyFiles)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "Default-assembly ownership override manifest is missing.",
                manifestPath);
        }

        OwnershipOverrideManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<OwnershipOverrideManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Default-assembly ownership override manifest is invalid JSON.",
                exception);
        }

        if (manifest == null || manifest.SchemaVersion != OwnershipManifestSchemaVersion)
        {
            throw new InvalidDataException(
                "Default-assembly ownership override manifest schema must be "
                + OwnershipManifestSchemaVersion + ".");
        }
        if (manifest.Entries == null)
        {
            throw new InvalidDataException(
                "Default-assembly ownership override manifest is missing 'entries'.");
        }

        HashSet<string> defaults = new HashSet<string>(
            defaultAssemblyFiles,
            StringComparer.Ordinal);
        Dictionary<string, OwnershipOverrideEntry> entries =
            new Dictionary<string, OwnershipOverrideEntry>(StringComparer.Ordinal);
        foreach (OwnershipOverrideEntry entry in manifest.Entries)
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.Path)
                || string.IsNullOrWhiteSpace(entry.Classification)
                || string.IsNullOrWhiteSpace(entry.Rationale)
                || string.IsNullOrWhiteSpace(entry.ReviewedBy))
            {
                throw new InvalidDataException(
                    "Every ownership override requires path, classification, rationale, and reviewedBy.");
            }

            string path = entry.Path.Replace('\\', '/');
            if (Path.IsPathRooted(path)
                || path.IndexOf('*') >= 0
                || path.IndexOf('?') >= 0
                || path.Split('/').Any(segment => segment == ".."))
            {
                throw new InvalidDataException(
                    "Ownership overrides must use one exact project-relative path: " + path);
            }
            if (!entries.TryAdd(path, entry))
            {
                throw new InvalidDataException(
                    "Duplicate ownership override entry: " + path);
            }
            if (!defaults.Contains(path))
            {
                string absolute = Path.Combine(
                    projectRoot,
                    path.Replace('/', Path.DirectorySeparatorChar));
                string reason = !File.Exists(absolute)
                    ? "source is missing"
                    : ResolveAssemblyName(absolute, scriptsRoot) != "Assembly-CSharp"
                        ? "source is no longer in the default runtime assembly"
                        : "source is not part of the runtime scan";
                throw new InvalidDataException(
                    "Stale ownership override entry '" + path + "': " + reason + ".");
            }
            if (!Enum.TryParse(
                    entry.Classification,
                    false,
                    out OwnershipClassification parsed))
            {
                throw new InvalidDataException(
                    "Unknown ownership classification for '" + path + "': "
                    + entry.Classification + ".");
            }
            entry.Path = path;
            entry.ParsedClassification = parsed;
        }
        return entries;
    }

    private static void ApplyOwnershipOverrides(
        IEnumerable<DefaultOwnershipFinding> findings,
        IReadOnlyDictionary<string, OwnershipOverrideEntry> overrides)
    {
        foreach (DefaultOwnershipFinding finding in findings)
        {
            if (!overrides.TryGetValue(finding.Path, out OwnershipOverrideEntry entry))
            {
                continue;
            }

            if (finding.AutomaticClassification == OwnershipClassification.ReviewRequired
                && entry.ParsedClassification == OwnershipClassification.DefaultAllowed)
            {
                throw new InvalidDataException(
                    "Mixed-owner ReviewRequired source cannot be approved as DefaultAllowed; split it first: "
                    + finding.Path);
            }
            finding.Classification = entry.ParsedClassification;
            finding.OverrideRationale = entry.Rationale.Trim();
            finding.ReviewedBy = entry.ReviewedBy.Trim();
            finding.Evidence = finding.Evidence
                .Concat(new[] { "exact-path-reviewed-override" })
                .ToArray();
        }
    }

    private static void VerifyBaseline(Snapshot snapshot, string baselinePath)
    {
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException("Architecture baseline is missing.", baselinePath);
        }

        string baseline = File.ReadAllText(baselinePath);
        foreach (Metric metric in snapshot.Metrics())
        {
            int maximum = ReadInt(baseline, "max" + metric.Name);
            string setHash = ReadString(baseline, metric.Name + "SetHash");
            if (metric.Count > maximum)
            {
                throw new InvalidOperationException(
                    metric.Name + " grew from " + maximum + " to " + metric.Count + ".");
            }
            // A ratchet must accept a strict reduction. Requiring the old set
            // hash after offenders have been removed makes every successful
            // cleanup fail until somebody rewrites the baseline. When the
            // count is unchanged, keep the hash check so same-size churn
            // cannot replace one known violation with another.
            if (metric.Count == maximum
                && !string.Equals(metric.SetHash, setHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    metric.Name + " violation set changed. Update the baseline only after reviewing the exact diff.");
            }
        }
    }

    private static int ReadInt(string json, string name)
    {
        Match match = Regex.Match(
            json,
            "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(?<value>[0-9]+)");
        if (!match.Success)
        {
            throw new InvalidDataException("Missing baseline number '" + name + "'.");
        }
        return int.Parse(match.Groups["value"].Value);
    }

    private static string ReadString(string json, string name)
    {
        Match match = Regex.Match(
            json,
            "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"(?<value>[0-9a-f]+)\\\"");
        if (!match.Success)
        {
            throw new InvalidDataException("Missing baseline string '" + name + "'.");
        }
        return match.Groups["value"].Value;
    }

    private static bool IsEditorSource(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.EndsWith("/Editor.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveAssemblyName(string sourcePath, string scriptsRoot)
    {
        DirectoryInfo current = new FileInfo(sourcePath).Directory;
        DirectoryInfo root = new DirectoryInfo(scriptsRoot);
        while (current != null && current.FullName.StartsWith(
                   root.FullName,
                   StringComparison.OrdinalIgnoreCase))
        {
            string asmdef = Directory.GetFiles(current.FullName, "*.asmdef")
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (asmdef != null)
            {
                Match name = Regex.Match(
                    File.ReadAllText(asmdef),
                    "\\\"name\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"");
                return name.Success ? name.Groups["value"].Value : Path.GetFileNameWithoutExtension(asmdef);
            }

            string asmref = Directory.GetFiles(current.FullName, "*.asmref")
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (asmref != null)
            {
                Match reference = Regex.Match(
                    File.ReadAllText(asmref),
                    "\\\"reference\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"");
                return reference.Success
                    ? reference.Groups["value"].Value
                    : Path.GetFileNameWithoutExtension(asmref);
            }
            current = current.Parent;
        }
        return "Assembly-CSharp";
    }

    private static bool HasThreadStaticAttribute(SyntaxList<AttributeListSyntax> attributes)
    {
        return attributes.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString().EndsWith("ThreadStatic", StringComparison.Ordinal)
            || attribute.Name.ToString().EndsWith("ThreadStaticAttribute", StringComparison.Ordinal));
    }

    private static bool IsApprovedRuntimeRebuildableCache(
        FieldDeclarationSyntax field)
    {
        bool marked = field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute =>
            attribute.Name.ToString().EndsWith(
                "RuntimeRebuildableCache",
                StringComparison.Ordinal)
            || attribute.Name.ToString().EndsWith(
                "RuntimeRebuildableCacheAttribute",
                StringComparison.Ordinal));
        if (!marked || !field.Modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        string type = field.Declaration.Type.ToString();
        return string.Equals(type, "Sprite", StringComparison.Ordinal)
            || string.Equals(type, "UnityEngine.Sprite", StringComparison.Ordinal)
            || string.Equals(type, "Material", StringComparison.Ordinal)
            || string.Equals(type, "UnityEngine.Material", StringComparison.Ordinal);
    }

    private static string GetContainingTypeName(SyntaxNode node)
    {
        TypeDeclarationSyntax type = node.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        return type == null ? "<global>" : GetTypeName(type);
    }

    private static bool IsDependencyInjectionOwner(
        ConstructorDeclarationSyntax constructor)
    {
        TypeDeclarationSyntax owner = constructor.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        if (owner == null)
        {
            return false;
        }

        string name = owner.Identifier.ValueText;
        if (CompositionRootSuffixes.Any(suffix =>
                name.EndsWith(suffix, StringComparison.Ordinal)))
        {
            return false;
        }

        return DependencyOwnerSuffixes.Any(suffix =>
            name.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static bool IsSessionMutationTarget(string expression)
    {
        string normalized = NormalizeSyntax(expression);
        bool reactiveValue = SessionReactiveMembers.Any(member =>
            normalized.EndsWith("." + member + ".Value", StringComparison.Ordinal)
            || string.Equals(
                normalized,
                member + ".Value",
                StringComparison.Ordinal));
        if (reactiveValue)
        {
            return true;
        }

        return SessionMutationMembers.Any(member =>
                normalized.EndsWith("." + member, StringComparison.Ordinal)
                || string.Equals(normalized, member, StringComparison.Ordinal))
            && (normalized.IndexOf("gameData", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("GameManager", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string GetTypeName(TypeDeclarationSyntax type)
    {
        return string.Join(
            ".",
            type.AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .Reverse()
                .Select(value => value.Identifier.ValueText));
    }

    private static int CountOrdinal(string value, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string NormalizeSyntax(string value)
    {
        return Regex.Replace(value, "\\s+", string.Empty);
    }

    private static string NormalizeRelative(string root, string path)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        return normalizedPath.Substring(normalizedRoot.Length).Replace('\\', '/');
    }

    private static string HashSourceFiles(string projectRoot, IEnumerable<string> sourcePaths)
    {
        using (SHA256 hash = SHA256.Create())
        using (MemoryStream stream = new MemoryStream())
        {
            foreach (string path in sourcePaths)
            {
                byte[] name = Encoding.UTF8.GetBytes(NormalizeRelative(projectRoot, path) + "\n");
                stream.Write(name, 0, name.Length);
                byte[] content = File.ReadAllBytes(path);
                stream.Write(content, 0, content.Length);
                stream.WriteByte((byte)'\n');
            }
            return ToHex(hash.ComputeHash(stream.ToArray()));
        }
    }

    private static string HashValues(IEnumerable<string> values)
    {
        string canonical = string.Join(
            "\n",
            values.OrderBy(value => value, StringComparer.Ordinal));
        using (SHA256 hash = SHA256.Create())
        {
            return ToHex(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }
    }

    private static string ToHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }
        return builder.ToString();
    }

    private sealed class Arguments
    {
        public string ProjectRoot;
        public string ReportPath;
        public string BaselinePath;
        public string OwnershipManifestPath;
        public string OwnershipReportPath;
        public bool WriteBaseline;
        public bool Verify;

        public static Arguments Parse(string[] args)
        {
            Arguments options = new Arguments();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument == "--project") options.ProjectRoot = args[++index];
                else if (argument == "--report") options.ReportPath = args[++index];
                else if (argument == "--baseline") options.BaselinePath = args[++index];
                else if (argument == "--ownership-manifest") options.OwnershipManifestPath = args[++index];
                else if (argument == "--ownership-report") options.OwnershipReportPath = args[++index];
                else if (argument == "--write-baseline") options.WriteBaseline = true;
                else if (argument == "--verify") options.Verify = true;
                else throw new ArgumentException("Unknown argument: " + argument);
            }

            if (string.IsNullOrWhiteSpace(options.ProjectRoot)
                || string.IsNullOrWhiteSpace(options.ReportPath)
                || string.IsNullOrWhiteSpace(options.BaselinePath)
                || string.IsNullOrWhiteSpace(options.OwnershipManifestPath)
                || string.IsNullOrWhiteSpace(options.OwnershipReportPath))
            {
                throw new ArgumentException(
                    "Required arguments: --project, --report, --baseline, --ownership-manifest, and --ownership-report.");
            }

            options.ProjectRoot = Path.GetFullPath(options.ProjectRoot);
            options.ReportPath = Path.GetFullPath(options.ReportPath);
            options.BaselinePath = Path.GetFullPath(options.BaselinePath);
            options.OwnershipManifestPath = Path.GetFullPath(options.OwnershipManifestPath);
            options.OwnershipReportPath = Path.GetFullPath(options.OwnershipReportPath);
            return options;
        }
    }

    private enum OwnershipClassification
    {
        DefaultAllowed,
        NamedRequired,
        ReviewRequired
    }

    private sealed class OwnershipOverrideManifest
    {
        public int SchemaVersion { get; set; }
        public OwnershipOverrideEntry[] Entries { get; set; }
    }

    private sealed class OwnershipOverrideEntry
    {
        public string Path { get; set; }
        public string Classification { get; set; }
        public string Rationale { get; set; }
        public string ReviewedBy { get; set; }
        public OwnershipClassification ParsedClassification { get; set; }
    }

    private sealed class DefaultOwnershipFinding
    {
        public string Path { get; set; }
        public OwnershipClassification AutomaticClassification { get; set; }
        public OwnershipClassification Classification { get; set; }
        public string[] Evidence { get; set; }
        public string[] ReferencedDomains { get; set; }
        public string OverrideRationale { get; set; }
        public string ReviewedBy { get; set; }
    }

    private sealed class CrossDomainCycleCandidate
    {
        public string Path { get; set; }
        public string Classification { get; set; }
        public string[] ReferencedDomains { get; set; }
    }

    private sealed class Metric
    {
        public string Name;
        public int Count;
        public string SetHash;
    }

    private sealed class Snapshot
    {
        public string SourceFingerprint;
        public int RuntimeSourceFileCount;
        public int RuntimeTypeCount;
        public int MutableStaticFieldCount;
        public string MutableStaticSetHash;
        public string[] MutableStatics;
        public int ReviewTypeCount;
        public string[] ReviewTypes;
        public int OversizedTypeCount;
        public string OversizedTypeSetHash;
        public string[] OversizedTypes;
        public int ReviewConstructorCount;
        public string[] ReviewConstructors;
        public int LargeConstructorCount;
        public string LargeConstructorSetHash;
        public string[] LargeConstructors;
        public int DefaultAssemblySourceFileCount;
        public string DefaultAssemblySourceSetHash;
        public string[] DefaultAssemblySources;
        public DefaultOwnershipFinding[] DefaultOwnershipFindings;
        public int DefaultAllowedCount;
        public int NamedRequiredCount;
        public int ReviewRequiredCount;
        public int UnapprovedDefaultDomainAuthorityCount;
        public string UnapprovedDefaultDomainAuthoritySetHash;
        public string[] UnapprovedDefaultDomainAuthorities;
        public CrossDomainCycleCandidate[] CrossDomainCycleCandidates;
        public int ContentEscapeCount;
        public string ContentEscapeSetHash;
        public string[] ContentEscapes;
        public int DirectSessionMutationCount;
        public string DirectSessionMutationSetHash;
        public string[] DirectSessionMutations;
        public int RawKoreanStringCount;
        public string RawKoreanStringSetHash;
        public string[] RawKoreanStringFiles;
        public int RootCatalogReferenceCount;
        public string RootCatalogReferenceSetHash;
        public string[] RootCatalogReferences;

        public IEnumerable<Metric> Metrics()
        {
            yield return NewMetric("MutableStatic", MutableStaticFieldCount, MutableStaticSetHash);
            yield return NewMetric("OversizedType", OversizedTypeCount, OversizedTypeSetHash);
            yield return NewMetric("LargeConstructor", LargeConstructorCount, LargeConstructorSetHash);
            // Phase 117 keeps ownership classifications as an audit report.
            // They are intentionally not a baseline ratchet: a default file is
            // changed only for a proven authority or boundary defect, not to
            // make a broad classifier count monotonically decrease.
            yield return NewMetric("ContentEscape", ContentEscapeCount, ContentEscapeSetHash);
            yield return NewMetric("DirectSessionMutation", DirectSessionMutationCount, DirectSessionMutationSetHash);
            // Raw Korean remains visible in the report for targeted UI audits.
            // It is not a monotonic ratchet because authored content and LLM
            // parser contracts intentionally contain Korean text.
            yield return NewMetric("RootCatalogReference", RootCatalogReferenceCount, RootCatalogReferenceSetHash);
        }

        public string ToJson()
        {
            return "{\n"
                + "  \"schemaVersion\": " + SchemaVersion + ",\n"
                + "  \"sourceFingerprint\": \"" + SourceFingerprint + "\",\n"
                + "  \"runtimeSourceFileCount\": " + RuntimeSourceFileCount + ",\n"
                + "  \"runtimeTypeCount\": " + RuntimeTypeCount + ",\n"
                + MetricJson("mutableStatic", MutableStaticFieldCount, MutableStaticSetHash) + ",\n"
                + StringArrayJson("mutableStatics", MutableStatics) + ",\n"
                + "  \"reviewTypeCount\": " + ReviewTypeCount + ",\n"
                + StringArrayJson("reviewTypes", ReviewTypes) + ",\n"
                + MetricJson("oversizedType", OversizedTypeCount, OversizedTypeSetHash) + ",\n"
                + StringArrayJson("oversizedTypes", OversizedTypes) + ",\n"
                + "  \"reviewConstructorCount\": " + ReviewConstructorCount + ",\n"
                + StringArrayJson("reviewConstructors", ReviewConstructors) + ",\n"
                + MetricJson("largeConstructor", LargeConstructorCount, LargeConstructorSetHash) + ",\n"
                + StringArrayJson("largeConstructors", LargeConstructors) + ",\n"
                + MetricJson("defaultAssemblySource", DefaultAssemblySourceFileCount, DefaultAssemblySourceSetHash) + ",\n"
                + StringArrayJson("defaultAssemblySources", DefaultAssemblySources) + ",\n"
                + "  \"defaultAllowedCount\": " + DefaultAllowedCount + ",\n"
                + "  \"namedRequiredCount\": " + NamedRequiredCount + ",\n"
                + "  \"reviewRequiredCount\": " + ReviewRequiredCount + ",\n"
                + MetricJson(
                    "unapprovedDefaultDomainAuthority",
                    UnapprovedDefaultDomainAuthorityCount,
                    UnapprovedDefaultDomainAuthoritySetHash) + ",\n"
                + StringArrayJson(
                    "unapprovedDefaultDomainAuthorities",
                    UnapprovedDefaultDomainAuthorities) + ",\n"
                + StringArrayJson(
                    "crossDomainCycleCandidates",
                    CrossDomainCycleCandidates.Select(candidate =>
                        candidate.Path + "|" + candidate.Classification + "|"
                        + string.Join(",", candidate.ReferencedDomains))) + ",\n"
                + MetricJson("contentEscape", ContentEscapeCount, ContentEscapeSetHash) + ",\n"
                + StringArrayJson("contentEscapes", ContentEscapes) + ",\n"
                + MetricJson("directSessionMutation", DirectSessionMutationCount, DirectSessionMutationSetHash) + ",\n"
                + StringArrayJson("directSessionMutations", DirectSessionMutations) + ",\n"
                + MetricJson("rawKoreanString", RawKoreanStringCount, RawKoreanStringSetHash) + ",\n"
                + StringArrayJson("rawKoreanStringFiles", RawKoreanStringFiles) + ",\n"
                + MetricJson("rootCatalogReference", RootCatalogReferenceCount, RootCatalogReferenceSetHash) + ",\n"
                + StringArrayJson("rootCatalogReferences", RootCatalogReferences) + "\n"
                + "}\n";
        }

        public string ToBaselineJson()
        {
            List<string> lines = new List<string>
            {
                "{",
                "  \"schemaVersion\": " + SchemaVersion + ","
            };
            Metric[] metrics = Metrics().ToArray();
            for (int index = 0; index < metrics.Length; index++)
            {
                Metric metric = metrics[index];
                lines.Add("  \"max" + metric.Name + "\": " + metric.Count + ",");
                lines.Add("  \"" + metric.Name + "SetHash\": \"" + metric.SetHash + "\""
                    + (index == metrics.Length - 1 ? string.Empty : ","));
            }
            lines.Add("}");
            return string.Join("\n", lines) + "\n";
        }

        public string ToOwnershipJson()
        {
            object report = new
            {
                schemaVersion = OwnershipManifestSchemaVersion,
                sourceFingerprint = SourceFingerprint,
                defaultAssemblyFileCount = DefaultAssemblySourceFileCount,
                defaultAllowedCount = DefaultAllowedCount,
                namedRequiredCount = NamedRequiredCount,
                reviewRequiredCount = ReviewRequiredCount,
                unapprovedDefaultDomainAuthorityCount =
                    UnapprovedDefaultDomainAuthorityCount,
                unapprovedDefaultDomainAuthoritySetHash =
                    UnapprovedDefaultDomainAuthoritySetHash,
                crossDomainCycleCandidateCount = CrossDomainCycleCandidates.Length,
                crossDomainCycleCandidates = CrossDomainCycleCandidates.Select(candidate => new
                {
                    path = candidate.Path,
                    classification = candidate.Classification,
                    referencedDomains = candidate.ReferencedDomains
                }),
                sources = DefaultOwnershipFindings.Select(finding => new
                {
                    path = finding.Path,
                    automaticClassification = finding.AutomaticClassification.ToString(),
                    classification = finding.Classification.ToString(),
                    evidence = finding.Evidence,
                    referencedDomains = finding.ReferencedDomains,
                    overrideRationale = finding.OverrideRationale,
                    reviewedBy = finding.ReviewedBy
                })
            };
            return JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions { WriteIndented = true })
                + "\n";
        }

        public string ToConsoleSummary()
        {
            return "Architecture metrics PASS: files=" + RuntimeSourceFileCount
                + ", types=" + RuntimeTypeCount
                + ", mutableStatics=" + MutableStaticFieldCount
                + ", reviewTypes=" + ReviewTypeCount
                + ", hardOversizedTypes=" + OversizedTypeCount
                + ", reviewConstructors=" + ReviewConstructorCount
                + ", hardLargeConstructors=" + LargeConstructorCount
                + ", defaultAssemblyFiles=" + DefaultAssemblySourceFileCount
                + ", defaultAllowed=" + DefaultAllowedCount
                + ", namedRequired=" + NamedRequiredCount
                + ", reviewRequired=" + ReviewRequiredCount
                + ", unapprovedDefaultDomainAuthorities="
                + UnapprovedDefaultDomainAuthorityCount
                + ", crossDomainCycleCandidates="
                + CrossDomainCycleCandidates.Length
                + ", contentEscapes=" + ContentEscapeCount
                + ", directSessionMutations=" + DirectSessionMutationCount
                + ", rawKoreanStrings=" + RawKoreanStringCount
                + ", rootCatalogReferences=" + RootCatalogReferenceCount + ".";
        }

        private static Metric NewMetric(string name, int count, string hash)
        {
            return new Metric { Name = name, Count = count, SetHash = hash };
        }

        private static string MetricJson(string name, int count, string hash)
        {
            return "  \"" + name + "Count\": " + count + ",\n"
                + "  \"" + name + "SetHash\": \"" + hash + "\"";
        }

        private static string StringArrayJson(string name, IEnumerable<string> values)
        {
            string[] encoded = (values ?? Array.Empty<string>())
                .Select(value => "\"" + EscapeJson(value) + "\"")
                .ToArray();
            return "  \"" + name + "\": [" + string.Join(", ", encoded) + "]";
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
