#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DungeonStory.Content.CoreSession;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class RuntimeAuthorityV18Validator
{
    private const string LegacyCatalogAssetPath =
        "Assets/Resources/SO/Items/DungeonItemCatalog.asset";

    private static readonly string[] RequiredItemIds =
    {
        "resource:clean-water",
        "ammo:arrow",
        "ammo:paper-cartridge",
        "evolution:catalyst:offense:1",
        "evolution:residue:21"
    };

    [MenuItem("Tools/DungeonStory/Validation/Validate V19 Runtime Authority")]
    public static void ValidateMenu()
    {
        Debug.Log(ValidateOrThrow());
    }

    public static string ValidateV19ValueContractsOrThrow()
    {
        List<string> errors = new();
        ValidateV19ValueContracts(errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        return "V19_VALUE_EVENTS=PASS";
    }

    public static IReadOnlyList<string> FindOptionalRuntimeInterfaceDependencies()
    {
        List<string> violations = new List<string>();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<object>()
                     .Where(type => type != null
                         && !type.IsAbstract
                         && IsGameplayRuntimeAssembly(type.Assembly)))
        {
            IEnumerable<System.Reflection.MethodBase> injectionPoints = type
                .GetConstructors(
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance)
                .Where(constructor => constructor.IsPublic
                    || HasInjectAttribute(constructor))
                .Cast<System.Reflection.MethodBase>()
                .Concat(type.GetMethods(
                        System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.DeclaredOnly)
                    .Where(HasInjectAttribute));

            foreach (System.Reflection.MethodBase injectionPoint in injectionPoints)
            {
                foreach (System.Reflection.ParameterInfo parameter in injectionPoint
                             .GetParameters()
                             .Where(parameter => parameter.ParameterType.IsInterface
                                 && parameter.HasDefaultValue
                                 && !IsCollectionContract(parameter.ParameterType)))
                {
                    violations.Add(
                        $"{type.FullName}.{injectionPoint.Name}: "
                        + $"{parameter.ParameterType.Name} {parameter.Name}");
                }
            }
        }

        return violations
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindMutableRuntimeStaticFields()
    {
        List<string> fields = new();
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain
                     .GetAssemblies()
                     .Where(IsGameplayRuntimeAssembly))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
            }

            foreach (Type type in types)
            {
                if (type.IsDefined(
                        typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
                        false))
                {
                    continue;
                }

                foreach (System.Reflection.FieldInfo field in type.GetFields(
                             System.Reflection.BindingFlags.Static
                             | System.Reflection.BindingFlags.Public
                             | System.Reflection.BindingFlags.NonPublic
                             | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral
                        || field.IsInitOnly
                        || field.IsDefined(typeof(ThreadStaticAttribute), false)
                        || field.IsDefined(
                            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
                            false))
                    {
                        continue;
                    }

                    fields.Add(
                        $"{type.FullName}.{field.Name}:{field.FieldType.Name}");
                }
            }
        }

        return fields
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasInjectAttribute(System.Reflection.MemberInfo member)
    {
        return member.GetCustomAttributes(false).Any(attribute =>
            string.Equals(
                attribute.GetType().Name,
                "InjectAttribute",
                StringComparison.Ordinal));
    }

    private static bool IsGameplayRuntimeAssembly(System.Reflection.Assembly assembly)
    {
        string name = assembly?.GetName().Name ?? string.Empty;
        return name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) < 0
            && (name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)
                || name.StartsWith("DungeonStory.", StringComparison.Ordinal));
    }

    private static bool IsCollectionContract(Type type)
    {
        if (type == null) return false;
        string fullName = type.IsGenericType
            ? type.GetGenericTypeDefinition().FullName
            : type.FullName;
        return fullName != null
            && fullName.StartsWith(
                "System.Collections.Generic.I",
                StringComparison.Ordinal);
    }

    public static IReadOnlyList<string>
        FindRemovedBroadRuntimeWrapperReferences()
    {
        string[] removedIdentifiers =
        {
            "ICharacterEnvironment" + "Runtime",
            "IAnimalHusbandry" + "Runtime",
            "ISurvivalFood" + "Runtime",
            "ICharacterConsumables" + "Runtime",
            "ICharacterSpecies" + "Runtime",
            "ISurgery" + "Runtime",
            "ICharacterMedical" + "Runtime",
            "IEnvironmentalWorkwear" + "Runtime",
            "IEnvironmentalField" + "Runtime",
            "IElectricalNetwork" + "Runtime",
            "IWaterNetwork" + "Runtime",
            "IConveyor" + "Runtime",
            "IAutomation" + "Runtime",
            "IProductionBill" + "Runtime",
            "IWasteProcessing" + "Runtime"
        };
        List<string> violations = new();
        foreach (string path in Directory.GetFiles(
                     "Assets/Scripts",
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string syntax = MaskCSharpTriviaAndLiterals(
                File.ReadAllText(path));
            foreach (string identifier in removedIdentifiers)
            {
                Match match = Regex.Match(
                    syntax,
                    $@"\b{Regex.Escape(identifier)}\b",
                    RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    continue;
                }

                int line = 1;
                for (int index = 0; index < match.Index; index++)
                {
                    if (syntax[index] == '\n')
                    {
                        line++;
                    }
                }
                violations.Add(
                    $"Removed broad runtime wrapper '{identifier}' returned at "
                    + $"{path.Replace('\\', '/')}:{line}.");
            }
        }

        return violations
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindNarrowRuntimeFacetViolations()
    {
        const string worldRegistration =
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs";
        const string characterRegistration =
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs";
        const string combatRegistration =
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs";
        RuntimeFacetContract[] contracts =
        {
            new(
                typeof(EnvironmentalFieldRuntimeApplicationAdapter),
                worldRegistration,
                typeof(IEnvironmentalFieldQuery),
                typeof(IEnvironmentalFieldCommand),
                typeof(IEnvironmentalFieldPersistence)),
            new(
                typeof(CharacterEnvironmentUnityAdapter),
                worldRegistration,
                typeof(ICharacterEnvironmentStatusQuery),
                typeof(ICharacterEnvironmentWorkContext),
                typeof(ICharacterEnvironmentPersistence)),
            new(
                typeof(EnvironmentalWorkwearRuntime),
                worldRegistration,
                typeof(IEnvironmentalWorkwearQuery),
                typeof(IEnvironmentalWorkwearCommand),
                typeof(IEnvironmentalWorkwearPersistence)),
            new(
                typeof(AnimalHusbandryRuntime),
                worldRegistration,
                typeof(IAnimalHusbandryQuery),
                typeof(IAnimalHusbandryCommand),
                typeof(IAnimalHusbandryPersistence),
                typeof(IAnimalPenCompatibilityQuery)),
            new(
                typeof(SurvivalFoodRuntime),
                worldRegistration,
                typeof(ISurvivalFoodQuery),
                typeof(ISurvivalFoodCommand),
                typeof(ISurvivalFoodPersistence),
                typeof(ISurvivalFoodDebugCommand),
                typeof(ICharacterNutritionRuntime),
                typeof(ISurvivalEnvironmentQuery),
                typeof(ISurvivalStorageEnvironmentSink)),
            new(
                typeof(CharacterConsumablesRuntime),
                worldRegistration,
                typeof(ICharacterConsumablesApplication),
                typeof(ICharacterConsumablesPersistence)),
            new(
                typeof(CharacterConsumablesCompatibilityAdapter),
                worldRegistration,
                typeof(ICharacterConsumablesQuery),
                typeof(ICharacterConsumablesCommand),
                typeof(ICharacterDietPolicyRuntime),
                typeof(IMealConsumptionRuntime),
                typeof(ICharacterSubstanceRuntime)),
            new(
                typeof(CharacterSpeciesRuntime),
                characterRegistration,
                typeof(ICharacterSpeciesQuery),
                typeof(ICharacterSpeciesCommand),
                typeof(ICharacterSpeciesPersistence)),
            new(
                typeof(SurgeryRuntime),
                combatRegistration,
                typeof(ISurgeryQuery),
                typeof(ISurgeryWorkCommand),
                typeof(ISurgeryPersistence),
                typeof(ISurgeryCommandService)),
            new(
                typeof(CharacterMedicalRuntime),
                combatRegistration,
                typeof(ICharacterMedicalQuery),
                typeof(ICharacterMedicalCommand),
                typeof(ICharacterMedicalPersistence))
        };

        List<string> violations = new();
        foreach (RuntimeFacetContract contract in contracts)
        {
            string registration = File.Exists(contract.RegistrationPath)
                ? MaskCSharpTriviaAndLiterals(
                    File.ReadAllText(contract.RegistrationPath))
                : string.Empty;
            string entryPointAnchor =
                $"RegisterEntryPoint<{contract.RuntimeType.Name}>";
            string serviceAnchor =
                $"Register<{contract.RuntimeType.Name}>";
            int start = registration.IndexOf(
                entryPointAnchor,
                StringComparison.Ordinal);
            if (start < 0)
            {
                start = registration.IndexOf(
                    serviceAnchor,
                    StringComparison.Ordinal);
            }
            int end = start < 0
                ? -1
                : registration.IndexOf(';', start);
            string block = start >= 0 && end > start
                ? registration.Substring(start, end - start)
                : string.Empty;
            if (block.Length == 0)
            {
                violations.Add(
                    $"{contract.RuntimeType.Name} has no entry-point registration block in {contract.RegistrationPath}.");
                continue;
            }

            foreach (Type facet in contract.Facets)
            {
                if (!facet.IsAssignableFrom(contract.RuntimeType))
                {
                    violations.Add(
                        $"{contract.RuntimeType.Name} no longer implements {facet.Name}.");
                }
                if (!block.Contains(
                        $".As<{facet.Name}>()",
                        StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{contract.RuntimeType.Name} registration no longer exposes {facet.Name}.");
                }
            }
        }

        return violations
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string MaskCSharpTriviaAndLiterals(string source)
    {
        char[] masked = (source ?? string.Empty).ToCharArray();
        CSharpMaskState state = CSharpMaskState.Code;
        for (int index = 0; index < masked.Length; index++)
        {
            char current = masked[index];
            char next = index + 1 < masked.Length
                ? masked[index + 1]
                : '\0';
            if (state == CSharpMaskState.Code)
            {
                if (current == '/' && next == '/')
                {
                    masked[index] = masked[index + 1] = ' ';
                    index++;
                    state = CSharpMaskState.LineComment;
                }
                else if (current == '/' && next == '*')
                {
                    masked[index] = masked[index + 1] = ' ';
                    index++;
                    state = CSharpMaskState.BlockComment;
                }
                else if (current == '"')
                {
                    bool verbatim = index > 0 && masked[index - 1] == '@';
                    masked[index] = ' ';
                    state = verbatim
                        ? CSharpMaskState.VerbatimString
                        : CSharpMaskState.String;
                }
                else if (current == '\'')
                {
                    masked[index] = ' ';
                    state = CSharpMaskState.Character;
                }
                continue;
            }

            if (current == '\r' || current == '\n')
            {
                if (state == CSharpMaskState.LineComment)
                {
                    state = CSharpMaskState.Code;
                }
                else if (state is CSharpMaskState.String
                    or CSharpMaskState.Character)
                {
                    state = CSharpMaskState.Code;
                }
                continue;
            }

            masked[index] = ' ';
            if (state == CSharpMaskState.BlockComment
                && current == '*' && next == '/')
            {
                masked[index + 1] = ' ';
                index++;
                state = CSharpMaskState.Code;
            }
            else if (state == CSharpMaskState.String)
            {
                if (current == '\\' && index + 1 < masked.Length)
                {
                    masked[index + 1] = ' ';
                    index++;
                }
                else if (current == '"')
                {
                    state = CSharpMaskState.Code;
                }
            }
            else if (state == CSharpMaskState.VerbatimString
                && current == '"')
            {
                if (next == '"')
                {
                    masked[index + 1] = ' ';
                    index++;
                }
                else
                {
                    state = CSharpMaskState.Code;
                }
            }
            else if (state == CSharpMaskState.Character)
            {
                if (current == '\\' && index + 1 < masked.Length)
                {
                    masked[index + 1] = ' ';
                    index++;
                }
                else if (current == '\'')
                {
                    state = CSharpMaskState.Code;
                }
            }
        }

        return new string(masked);
    }

    private enum CSharpMaskState
    {
        Code,
        LineComment,
        BlockComment,
        String,
        VerbatimString,
        Character
    }

    private sealed class RuntimeFacetContract
    {
        internal RuntimeFacetContract(
            Type runtimeType,
            string registrationPath,
            params Type[] facets)
        {
            RuntimeType = runtimeType;
            RegistrationPath = registrationPath;
            Facets = facets;
        }

        internal Type RuntimeType { get; }
        internal string RegistrationPath { get; }
        internal IReadOnlyList<Type> Facets { get; }
    }

    private sealed class StrictSaveSectionContract
    {
        internal StrictSaveSectionContract(
            Type sectionType,
            Type payloadType,
            Type persistenceType,
            Type candidateType,
            string sectionSourcePath,
            string runtimeSourcePath,
            string sectionId,
            int sectionVersion,
            DungeonSaveRestorePhase restorePhase,
            string candidateBuilderMethod)
        {
            SectionType = sectionType;
            PayloadType = payloadType;
            PersistenceType = persistenceType;
            CandidateType = candidateType;
            SectionSourcePath = sectionSourcePath;
            RuntimeSourcePath = runtimeSourcePath;
            SectionId = sectionId;
            SectionVersion = sectionVersion;
            RestorePhase = restorePhase;
            CandidateBuilderMethod = candidateBuilderMethod;
        }

        internal Type SectionType { get; }
        internal Type PayloadType { get; }
        internal Type PersistenceType { get; }
        internal Type CandidateType { get; }
        internal string SectionSourcePath { get; }
        internal string RuntimeSourcePath { get; }
        internal string SectionId { get; }
        internal int SectionVersion { get; }
        internal DungeonSaveRestorePhase RestorePhase { get; }
        internal string CandidateBuilderMethod { get; }
    }

    private static void ValidateBatchCStrictSaveBoundaries(
        IReadOnlyCollection<Type> productionSaveSections,
        ICollection<string> errors)
    {
        StrictSaveSectionContract[] contracts =
        {
            new(
                typeof(EnvironmentalFieldSaveSection),
                typeof(DungeonEnvironmentalFieldSaveData),
                typeof(IEnvironmentalFieldPersistence),
                typeof(DungeonStory.Environment.EnvironmentalFieldRestoreCandidate),
                "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldSaveSection.cs",
                "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs",
                EnvironmentalFieldSaveSection.Id,
                2,
                DungeonSaveRestorePhase.LateRuntimeState,
                "PrepareRestore"),
            new(
                typeof(ProductionBillsSaveSection),
                typeof(DungeonProductionBillSaveData),
                typeof(IProductionBillPersistence),
                typeof(ProductionBillRestoreCandidate),
                "Assets/Scripts/Services/Economy/ProductionBillsSaveSection.cs",
                "Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs",
                ProductionBillsSaveSection.Id,
                5,
                DungeonSaveRestorePhase.RuntimeState,
                "BuildRestore"),
            new(
                typeof(WasteProcessingSaveSection),
                typeof(DungeonWasteProcessingSaveData),
                typeof(IWasteProcessingPersistence),
                typeof(WasteProcessingRestoreCandidate),
                "Assets/Scripts/Services/Economy/Waste/WasteProcessingSaveSection.cs",
                "Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs",
                WasteProcessingSaveSection.Id,
                2,
                DungeonSaveRestorePhase.LateRuntimeState,
                "BuildRestore"),
            new(
                typeof(PowerInfrastructureSaveSection),
                typeof(DungeonPowerInfrastructureSaveData),
                typeof(IPowerInfrastructurePersistence),
                typeof(ElectricalNetworkRestoreCandidate),
                "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveSections.cs",
                "Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs",
                PowerInfrastructureSaveSection.Id,
                2,
                DungeonSaveRestorePhase.RuntimeState,
                "PrepareRestore"),
            new(
                typeof(FluidInfrastructureSaveSection),
                typeof(DungeonFluidInfrastructureSaveData),
                typeof(IFluidInfrastructurePersistence),
                typeof(FluidNetworkRestoreCandidate),
                "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveSections.cs",
                "Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs",
                FluidInfrastructureSaveSection.Id,
                4,
                DungeonSaveRestorePhase.RuntimeState,
                "PrepareRestore"),
            new(
                typeof(ConveyorInfrastructureSaveSection),
                typeof(DungeonConveyorInfrastructureSaveData),
                typeof(IConveyorInfrastructurePersistence),
                typeof(ConveyorRestoreState),
                "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveSections.cs",
                "Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPersistence.cs",
                ConveyorInfrastructureSaveSection.Id,
                3,
                DungeonSaveRestorePhase.LateRuntimeState,
                "PrepareRestore"),
            new(
                typeof(AutomationInfrastructureSaveSection),
                typeof(DungeonAutomationSaveData),
                typeof(IAutomationInfrastructurePersistence),
                typeof(AutomationRestoreCandidate),
                "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureSaveSections.cs",
                "Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs",
                AutomationInfrastructureSaveSection.Id,
                2,
                DungeonSaveRestorePhase.LateRuntimeState,
                "PrepareRestore")
        };

        foreach (StrictSaveSectionContract contract in contracts)
        {
            ValidateStrictSaveSectionContract(
                productionSaveSections,
                contract,
                errors);
        }

        ValidateStrongIdentifierRuntimeBoundaries(errors);
    }

    private static void ValidateStrictSaveSectionContract(
        IReadOnlyCollection<Type> productionSaveSections,
        StrictSaveSectionContract contract,
        ICollection<string> errors)
    {
        Type sectionType = contract.SectionType;
        if (!productionSaveSections.Contains(sectionType))
        {
            errors.Add(
                $"Batch C required save section {sectionType.FullName} is not registered as a production section type.");
        }
        if (!sectionType.IsPublic || !sectionType.IsSealed)
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} must remain a public sealed composition boundary.");
        }

        Type[] requiredContracts =
        {
            typeof(IDungeonSaveSection),
            typeof(IDungeonSaveSectionPreflight),
            typeof(IDungeonStagedSaveSection),
            typeof(IDungeonRollbackFreeSaveSection)
        };
        foreach (Type requiredContract in requiredContracts.Where(candidate =>
                     !candidate.IsAssignableFrom(sectionType)))
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} no longer implements {requiredContract.Name}.");
        }
        if (typeof(IOptionalDungeonSaveSection).IsAssignableFrom(sectionType)
            || typeof(IDungeonStagedOptionalSaveSection).IsAssignableFrom(sectionType))
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} must be Required, not optional.");
        }

        System.Reflection.FieldInfo sectionIdField = sectionType.GetField(
            "Id",
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.FlattenHierarchy);
        string declaredSectionId = sectionIdField?.IsLiteral == true
            ? sectionIdField.GetRawConstantValue() as string
            : null;
        if (!string.Equals(
                declaredSectionId,
                contract.SectionId,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} must keep exact ID '{contract.SectionId}'.");
        }

        System.Reflection.FieldInfo versionField = contract.PayloadType.GetField(
            "CurrentVersion",
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.FlattenHierarchy);
        int declaredVersion = versionField?.IsLiteral == true
            ? (int)versionField.GetRawConstantValue()
            : -1;
        if (declaredVersion != contract.SectionVersion)
        {
            errors.Add(
                $"Batch C payload {contract.PayloadType.Name} must remain exact V{contract.SectionVersion}, found V{declaredVersion}.");
        }

        System.Reflection.ConstructorInfo[] constructors = sectionType
            .GetConstructors(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
        if (constructors.Length != 1
            || constructors[0].GetParameters().Length != 1
            || constructors[0].GetParameters()[0].ParameterType
                != contract.PersistenceType
            || constructors[0].GetParameters()[0].HasDefaultValue)
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} must require exactly one {contract.PersistenceType.Name} dependency.");
        }

        RequireMethodContract(
            errors,
            sectionType,
            nameof(IDungeonSaveSectionPreflight.ValidatePayload),
            typeof(void),
            typeof(string),
            typeof(int),
            typeof(DungeonGameRestoreReport));
        RequireMethodContract(
            errors,
            sectionType,
            nameof(IDungeonStagedSaveSection.StageRestore),
            typeof(IDungeonSaveRestoreStage),
            typeof(string),
            typeof(int),
            typeof(DungeonGameRestoreReport));
        RequireMethodContract(
            errors,
            contract.PersistenceType,
            "Capture",
            contract.PayloadType);
        RequireMethodContract(
            errors,
            contract.PersistenceType,
            contract.CandidateBuilderMethod,
            contract.CandidateType,
            contract.PayloadType);
        RequireMethodContract(
            errors,
            contract.PersistenceType,
            "Restore",
            typeof(void),
            contract.CandidateType);
        if (HasMethodContract(
                contract.PersistenceType,
                "Restore",
                typeof(void),
                contract.PayloadType))
        {
            errors.Add(
                $"{contract.PersistenceType.Name} restored raw {contract.PayloadType.Name} state instead of a detached candidate.");
        }

        if (!File.Exists(contract.SectionSourcePath))
        {
            errors.Add(
                $"Batch C save section source is missing: {contract.SectionSourcePath}.");
            return;
        }

        string declaredSource = ExtractDeclaredTypeSource(
            File.ReadAllText(contract.SectionSourcePath),
            sectionType.Name);
        string compactSource = Regex.Replace(
            MaskCSharpTriviaAndLiterals(declaredSource),
            @"\s+",
            " ");
        string[] requiredSourceTokens =
        {
            contract.PayloadType.Name + ".CurrentVersion",
            "JsonUtility.FromJson<" + contract.PayloadType.Name + ">",
            contract.CandidateType.Name + " candidate",
            "new DungeonDelegateSaveRestoreStage",
            "persistence.Restore(candidate)"
        };
        foreach (string token in requiredSourceTokens.Where(token =>
                     !compactSource.Contains(token, StringComparison.Ordinal)))
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} lost staged/detached source contract '{token}'.");
        }
        if (!compactSource.Contains(
                contract.RestorePhase.ToString(),
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Batch C save section {sectionType.Name} must restore in {contract.RestorePhase}.");
        }

        if (File.Exists(contract.RuntimeSourcePath))
        {
            string runtimeSource = MaskCSharpTriviaAndLiterals(
                File.ReadAllText(contract.RuntimeSourcePath));
            if (Regex.IsMatch(
                    runtimeSource,
                    $@"\bpublic\s+void\s+Restore\s*\(\s*{Regex.Escape(contract.PayloadType.Name)}\b",
                    RegexOptions.CultureInvariant))
            {
                errors.Add(
                    $"{contract.RuntimeSourcePath} reintroduced direct raw-snapshot Restore({contract.PayloadType.Name}).");
            }
        }
    }

    private static void RequireMethodContract(
        ICollection<string> errors,
        Type owner,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        if (!HasMethodContract(
                owner,
                methodName,
                returnType,
                parameterTypes))
        {
            errors.Add(
                $"{owner.Name} must expose exact {returnType.Name} {methodName}({string.Join(", ", parameterTypes.Select(type => type.Name))}).");
        }
    }

    private static bool HasMethodContract(
        Type owner,
        string methodName,
        Type returnType,
        params Type[] parameterTypes)
    {
        return owner.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance)
            .Any(method => method.Name == methodName
                && method.ReturnType == returnType
                && method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(parameterTypes));
    }

    private static string ExtractDeclaredTypeSource(
        string source,
        string typeName)
    {
        string declaration = "public sealed class " + typeName;
        int start = source.IndexOf(declaration, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int nextPublic = source.IndexOf(
            "\npublic sealed class ",
            start + declaration.Length,
            StringComparison.Ordinal);
        int nextInternal = source.IndexOf(
            "\ninternal static class ",
            start + declaration.Length,
            StringComparison.Ordinal);
        int end = new[] { nextPublic, nextInternal }
            .Where(index => index >= 0)
            .DefaultIfEmpty(source.Length)
            .Min();
        return source.Substring(start, end - start);
    }

    private static void ValidateStrongIdentifierRuntimeBoundaries(
        ICollection<string> errors)
    {
        Type[] identifierBoundaryTypes =
        {
            typeof(IProductionBillOrderCommand),
            typeof(IProductionBillWorkExecution),
            typeof(ProductionBillRuntime),
            typeof(IConveyorPayloadTransaction),
            typeof(IConveyorRoutingService)
        };
        HashSet<string> forbiddenRawStringParameters = new(
            new[]
            {
                "billId",
                "stackId",
                "fromBuildingId",
                "buildingInstanceId",
                "itemInstanceId"
            },
            StringComparer.OrdinalIgnoreCase);
        foreach (Type owner in identifierBoundaryTypes)
        {
            foreach (System.Reflection.MethodInfo method in owner.GetMethods(
                         System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.Instance))
            {
                foreach (System.Reflection.ParameterInfo parameter in method
                             .GetParameters()
                             .Where(parameter => parameter.ParameterType == typeof(string)
                                 && forbiddenRawStringParameters.Contains(
                                     parameter.Name ?? string.Empty)))
                {
                    errors.Add(
                        $"{owner.Name}.{method.Name} reintroduced raw string identifier parameter '{parameter.Name}'.");
                }
            }
        }

        foreach (Type identifierType in new[]
                 {
                     typeof(ProductionBillId),
                     typeof(ItemStackId),
                     typeof(BuildingInstanceId)
                 })
        {
            if (identifierType.GetMethods(
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static)
                .Any(method => method.Name == "op_Implicit"
                    && (method.ReturnType == typeof(string)
                        || method.ReturnType == identifierType
                        || method.GetParameters().Any(parameter =>
                            parameter.ParameterType == typeof(string)))))
            {
                errors.Add(
                    $"{identifierType.Name} reintroduced implicit string compatibility.");
            }
        }
    }

    public static string ValidateBatchCFinalSaveBoundaryOrThrow()
    {
        List<string> errors = new();
        Type[] saveSectionTypes = TypeCache.GetTypesDerivedFrom<IDungeonSaveSection>()
            .Where(type => type != null
                && type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && IsGameplayRuntimeAssembly(type.Assembly))
            .ToArray();
        int rollbackFree = saveSectionTypes.Count(type =>
            typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(type));
        if (saveSectionTypes.Length != 68
            || rollbackFree != saveSectionTypes.Length)
        {
            errors.Add(
                $"V21 save ratchet expected all 68 sections to be rollback-free; found {rollbackFree} rollback-free / {saveSectionTypes.Length - rollbackFree} remaining across {saveSectionTypes.Length}.");
        }

        HashSet<Type> expectedRemaining = new();
        HashSet<Type> actualRemaining = saveSectionTypes
            .Where(type => !typeof(IDungeonRollbackFreeSaveSection)
                .IsAssignableFrom(type))
            .ToHashSet();
        if (!actualRemaining.SetEquals(expectedRemaining))
        {
            errors.Add(
                "Batch D requires an empty remaining rollback set.");
        }

        foreach (Type type in saveSectionTypes.Where(type =>
                     !typeof(IDungeonStagedSaveSection).IsAssignableFrom(type)))
        {
            errors.Add($"Save section {type.FullName} bypasses detached staging.");
        }
        foreach (Type type in saveSectionTypes.Where(type =>
                     typeof(IOptionalDungeonSaveSection).IsAssignableFrom(type)
                     && !typeof(IDungeonStagedOptionalSaveSection)
                         .IsAssignableFrom(type)))
        {
            errors.Add(
                $"Optional save section {type.FullName} bypasses staged missing-data restore.");
        }

        errors.AddRange(FindRemovedBroadRuntimeWrapperReferences());
        ValidateBatchCStrictSaveBoundaries(saveSectionTypes, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Batch C final save-boundary validation failed:\n"
                + string.Join("\n", errors));
        }

        return "V21 SAVE BOUNDARY PASS: all 68 sections are rollback-free with strict staged/detached restore boundaries.";
    }

    public static string ValidateOrThrow()
    {
        List<string> errors = new();
        int currentSaveVersion = (int)typeof(DungeonGameSaveData)
            .GetField(nameof(DungeonGameSaveData.CurrentVersion))
            .GetRawConstantValue();
        if (currentSaveVersion != 21)
        {
            errors.Add($"Save root must be V21, found V{currentSaveVersion}.");
        }

        Type[] saveSectionTypes = TypeCache.GetTypesDerivedFrom<IDungeonSaveSection>()
            .Where(type => type != null
                && type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && IsGameplayRuntimeAssembly(type.Assembly))
            .ToArray();
        if (saveSectionTypes.Length != 68)
        {
            errors.Add(
                $"Production save-section count must remain 68, found {saveSectionTypes.Length}.");
        }
        int rollbackFreeSaveSectionCount = saveSectionTypes.Count(type =>
            typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(type));
        if (rollbackFreeSaveSectionCount != saveSectionTypes.Length)
        {
            errors.Add(
                "Batch D final save ratchet requires every production save section to be "
                + $"rollback-free; found {rollbackFreeSaveSectionCount} rollback-free and "
                + $"{saveSectionTypes.Length - rollbackFreeSaveSectionCount} remaining.");
        }
        HashSet<Type> expectedRemainingRollbackSections = new();
        HashSet<Type> actualRemainingRollbackSections = saveSectionTypes
            .Where(type => !typeof(IDungeonRollbackFreeSaveSection)
                .IsAssignableFrom(type))
            .ToHashSet();
        if (!actualRemainingRollbackSections.SetEquals(
                expectedRemainingRollbackSections))
        {
            errors.Add(
                "Batch D remaining rollback set must be empty. Expected: "
                + string.Join(", ", expectedRemainingRollbackSections
                    .Select(type => type.Name)
                    .OrderBy(name => name, StringComparer.Ordinal))
                + "; actual: "
                + string.Join(", ", actualRemainingRollbackSections
                    .Select(type => type.Name)
                    .OrderBy(name => name, StringComparer.Ordinal))
                + ".");
        }
        foreach (Type type in saveSectionTypes.Where(type =>
                     !typeof(IDungeonStagedSaveSection).IsAssignableFrom(type)))
        {
            errors.Add($"Save section {type.FullName} bypasses detached staging.");
        }
        foreach (Type type in saveSectionTypes.Where(type =>
                     typeof(IOptionalDungeonSaveSection).IsAssignableFrom(type)
                     && !typeof(IDungeonStagedOptionalSaveSection).IsAssignableFrom(type)))
        {
            errors.Add($"Optional save section {type.FullName} mutates missing-data state directly.");
        }

        const string typedSaveBoundaryPath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/DungeonJsonSaveSection.cs";

        RequireSourceContract(
            errors,
            typedSaveBoundaryPath,
            "ParsePayload(payloadJson)",
            "Typed save sections must share one strict JSON deserialization boundary.");
        RequireSourceContract(
            errors,
            typedSaveBoundaryPath,
            "capture returned a null payload",
            "Typed save capture must reject missing domain state instead of fabricating a default DTO.");
        ForbidSourceContract(
            errors,
            typedSaveBoundaryPath,
            "class DungeonJsonSaveSection<",
            "The legacy payload-only save boundary must be removed after all sections adopt detached candidates.");
        ForbidSourceContract(
            errors,
            typedSaveBoundaryPath,
            "CapturePayload() ?? new TPayload()",
            "Typed save capture must not replace missing state with a default DTO.");
        ForbidSourceContract(
            errors,
            typedSaveBoundaryPath,
            "JsonUtility.FromJson<TPayload>(payloadJson) ?? new TPayload()",
            "Typed save restore must not replace invalid or null JSON with a default DTO.");

        if (!DungeonSaveCompatibility.TryGetIncompatibilityReason(
                20,
                out string preV21Reason)
            || !string.Equals(
                preV21Reason,
                DungeonSaveCompatibility.PreV21IncompatibilityReason,
                StringComparison.Ordinal))
        {
            errors.Add("V20 and older saves are not rejected with the V21 new-game message.");
        }

        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
            GameContentCatalogSO.ResourcePath);
        if (root == null)
        {
            errors.Add("The required GameContentCatalogSO Resources root is missing.");
        }
        else if (root.GetItemDefinitions<ItemDefinitionCatalogSO>() == null)
        {
            errors.Add("The V19 content root has no item catalog.");
        }
        else
        {
            errors.AddRange(root.GetItemDefinitions<ItemDefinitionCatalogSO>().ValidateCatalog());
            ResourceItemDefinitionCatalog catalog = new(
                root.GetItemDefinitions<ItemDefinitionCatalogSO>().Definitions);
            foreach (string itemId in RequiredItemIds)
            {
                if (!catalog.TryGet((ItemDefinitionId)itemId, out _))
                {
                    errors.Add($"Required authored item '{itemId}' is missing.");
                }
            }

            int catalystCount = root.GetItemDefinitions<ItemDefinitionCatalogSO>().Definitions.Count(definition =>
                definition != null
                && definition.TryGetFeature(out EvolutionCatalystItemFeature _));
            if (catalystCount != 168)
            {
                errors.Add($"Expected 168 explicit catalyst/residue SOs, found {catalystCount}.");
            }

            if (catalog.TryGet((ItemDefinitionId)"missing:v19-validator", out _))
            {
                errors.Add("The strict item catalog fabricated an unknown item.");
            }

            try
            {
                catalog.GetRequired((ItemDefinitionId)"missing:v19-validator");
                errors.Add("GetRequired did not fail for an unknown item.");
            }
            catch (KeyNotFoundException)
            {
                // Expected: missing content must fail loudly.
            }
        }

        if (root != null
            && (root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>() == null
                || root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>().WorldWaterTile == null
                || root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>().WorldFilthTile == null))
        {
            errors.Add(
                "World presentation catalog must author water and filth tiles.");
        }

        if (AssetDatabase.LoadMainAssetAtPath(LegacyCatalogAssetPath) != null)
        {
            errors.Add("The legacy DungeonItemCatalog asset still exists.");
        }

        if (File.Exists("Assets/DataManager.cs")
            || File.Exists(
                "Assets/Scripts/Services/Infrastructure/DataScriptableObjectSource.cs"))
        {
            errors.Add(
                "Legacy DataManager/content-source caches still exist outside the root catalog projection.");
        }
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/DataCatalogService.cs",
            "GameContentDataCatalog(IGameContentCatalog content)",
            "Numeric compatibility lookup must be a rebuildable projection of the immutable root content catalog.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/DataCatalogService.cs",
            "ReadOnlyDictionary<int, T>",
            "The compatibility catalog must not expose mutable content dictionaries.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs",
            "Register<GameContentDataCatalog>",
            "Composition must resolve the compatibility index directly from the root content catalog.");

        errors.AddRange(FindRemovedBroadRuntimeWrapperReferences());
        errors.AddRange(FindNarrowRuntimeFacetViolations());
        ValidateBatchCStrictSaveBoundaries(saveSectionTypes, errors);
        ValidateSources(errors);
        errors.AddRange(FindOptionalRuntimeInterfaceDependencies()
            .Select(value => $"Optional runtime interface dependency: {value}"));
        ValidateAssets(errors);
        ValidateWarehouseAuthority(errors);
        ValidateUniqueItemAuthority(errors);
        ValidatePersistentIdentityAuthority(errors);
        ValidateBuildingArchetypeAuthority(root, errors);
        ValidateAuthoredGameplayContent(root, errors);
        ValidateFixedTaxonomyAuthority(errors);
        ValidateCharacterAuthoredModelAuthority(errors);
        ValidateSpeciesCombatPayloadAuthority(errors);
        ValidateSpeciesAssemblyAuthority(root, errors);
        ValidateSessionAndScopedStateAuthority(root, errors);
        ValidateOffenseAggregateAuthority(errors);
        ValidateReplaceableAggregateRoots(errors);
        ValidateCharacterSummaryDecomposition(errors);
        ValidatePresentationDependencyCuts(errors);
        ValidateDomainFailureLocalization(errors);
        try
        {
            BatchAArchitectureMetricsValidator.ValidateOrThrow();
        }
        catch (Exception exception)
        {
            errors.Add($"Batch A architecture metrics failed: {exception.Message}");
        }
        try
        {
            BatchAContentAuthorityDebugScenarios.ValidateOrThrow();
        }
        catch (Exception exception)
        {
            errors.Add($"Batch A content authority failed: {exception.Message}");
        }
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "V19 runtime authority validation failed:\n" + string.Join("\n", errors));
        }

        int itemCount = root.GetItemDefinitions<ItemDefinitionCatalogSO>().Definitions.Count;
        return $"V19 AUTHORITY PASS: save V19, {itemCount} authored items, "
            + "168 catalyst SOs, legacy item authority 0, abstract stock assets 0.";
    }

    private static void ValidateSources(ICollection<string> errors)
    {
        string[] forbiddenTokens =
        {
            "DungeonItemCatalogSO",
            "GetDefinitionOrDefault(",
            "FromStockCategory(",
            "class DataManager",
            "DataManagerCatalog",
            "IDataScriptableObjectSource",
            "ResourceDataScriptableObjectSource",
            "ResearchBlueprintItemDefinitions",
            "CharacterSpeciesExpansionDefaults",
            "CharacterSpeciesResourceLookup",
            "CreateFallbackDefinitions(",
            "CreateRuntimeDefaults(",
            "IFactionRuntimeProvider",
            "FactionRuntimeProvider",
            "public static class MetaProgressionCatalog",
            "public static class RunVariableCatalog",
            "public static class OwnerDoctrineCatalog",
            "public static class InvasionIntruderPatternCatalog",
            "public static class CharacterNeedCatalog",
            "public static class StockCategoryCatalog",
            "public static class BuildingCategoryCatalog",
            "service:character-meal",
            "service:guest-meal",
            "service:medical-treatment",
            "service:substance-policy",
            "combat:ammunition",
            "commerce:trade-contract",
            "facility:kiln:fuel",
            "facility:boiler:fuel",
            "facility:incinerator:fuel",
            "facility:animal-pen:feed"
        };

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)
                || string.Equals(
                    path,
                    "Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    errors.Add($"Runtime source '{path}' contains forbidden token '{token}'.");
                }
            }

            if (source.Contains("new DungeonItemDefinition(", StringComparison.Ordinal))
            {
                errors.Add($"Runtime source '{path}' constructs a code-owned item definition.");
            }

            if (!string.Equals(
                    path,
                    "Assets/Scripts/Services/Infrastructure/Core/ResourcesAssetLoader.cs",
                    StringComparison.Ordinal)
                && source.Contains("Resources.Load", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Runtime source '{path}' bypasses the explicit root content catalog.");
            }

            if (source.Contains(
                    "ScriptableObject.CreateInstance",
                    StringComparison.Ordinal)
                || Regex.IsMatch(
                    source,
                    @"\bCreateInstance<[^>]*(?:DefinitionSO|SettingsSO|BuildingSO|CharacterSO|CharacterSpeciesSO|DungeonFactionDefinitionSO|Tile)[^>]*>"))
            {
                errors.Add(
                    $"Runtime source '{path}' synthesizes an authored content ScriptableObject.");
            }

            if (Regex.IsMatch(source, @"new\s+(?:System\.)?Random\s*\("))
            {
                errors.Add(
                    $"Runtime source '{path}' directly constructs System.Random instead of using a scoped or deterministic random contract.");
            }

            if (Regex.IsMatch(
                    source,
                    @"new\s+(?:UnityGameClock|UnityUiClock|UnityGameTimeScaleController|ResourceGameContentCatalog|UnityGameContentRootLoader)\s*\("))
            {
                errors.Add(
                    $"Runtime source '{path}' constructs a default infrastructure service outside composition.");
            }

            if (Regex.IsMatch(
                    source,
                    @"\bBind[A-Za-z0-9_]*Runtime\s*\("))
            {
                errors.Add(
                    $"Runtime source '{path}' uses a late runtime-binding path.");
            }

            if (Regex.IsMatch(
                    source,
                    "\\$\\\"(?:research|research-archive|building|crop-plot|pen|defense):[^\\\"\\r\\n]*centerPos"))
            {
                errors.Add(
                    $"Runtime source '{path}' derives a persistent facility key from coordinates.");
            }
        }
    }

    private static void ValidateBuildingArchetypeAuthority(
        GameContentCatalogSO root,
        ICollection<string> errors)
    {
        if (typeof(BuildingSO).GetField("type") != null)
        {
            errors.Add("BuildingSO still serializes a System.Type runtime component.");
        }

        if (typeof(ItemDefinitionId).GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static)
            .Any(method => method.Name == "op_Implicit"))
        {
            errors.Add("ItemDefinitionId still permits an implicit string conversion.");
        }

        GameDomainContentCatalogSO domainCatalog = root?.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault();
        if (domainCatalog == null)
        {
            errors.Add("The root content catalog has no single domain catalog.");
            return;
        }

        BuildingSO[] definitions = domainCatalog.GetAll<BuildingSO>().ToArray();
        if (definitions.Length == 0)
        {
            errors.Add("The domain content catalog has no BuildingSO definitions.");
        }

        foreach (BuildingSO definition in definitions)
        {
            if (definition == null || !definition.runtimeArchetype.IsDefined())
            {
                errors.Add(
                    $"Building '{definition?.name ?? "<missing>"}' has no valid runtime archetype.");
            }
        }

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:BuildingSO",
                     new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            string source = File.ReadAllText(path);
            if (source.Contains("System.RuntimeType", StringComparison.Ordinal)
                || source.Contains("- Name: type", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Building asset '{path}' still stores the legacy runtime Type node.");
            }
        }

        const string factoryPath =
            "Assets/Scripts/Services/Grid/Building/GridBuildingObjectFactory.cs";
        string factorySource = File.ReadAllText(factoryPath);
        if (Regex.IsMatch(factorySource, @"AddComponent\s*\(\s*[^)]*(?:\.type|System\.Type)"))
        {
            errors.Add("Grid building factory still creates components from System.Type.");
        }
    }

    private static void ValidateReplaceableAggregateRoots(
        ICollection<string> errors)
    {
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "DungeonRuntimeAggregateRootStore",
            "Migrated runtime aggregates must share one composition-wide replaceable root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "PublishRestoreStaging",
            "The save registry must publish detached aggregate state only after every stage succeeds.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "rollbackImage = rollbackFree",
            "An all-marker V18 restore must not capture a redundant live rollback image.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "rollbackImage = CaptureAll();",
            "The save registry must not capture a rollback image unconditionally.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "IDungeonRestoreTransactionParticipant",
            "Unity world candidates must share the save registry begin/publish/discard transaction boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "Grid candidateGrid = liveGrid.CreateDetachedLayoutCopy();",
            "Facility restore must prepare an occupant-free candidate grid before replacing the live world.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "Facility-world candidate publication requires an active, empty V18 transaction slot.",
            "Facility restore must not publish or mutate the live Grid outside the save registry transaction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveCodec.cs",
            "ModularFacilityWorldSaveService.CurrentVersion",
            "Facility JSON must pass through the exact-version, dependency-free strict codec.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "DungeonStrictJsonSaveSection<\n        ModularFacilityWorldSaveData,\n        ModularFacilityWorldRestoreCandidate>",
            "Facility section commits must write only to detached candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "public ModularFacilityWorldRestoreCandidate PrepareRestoreCandidate(",
            "Facility-world staging must construct its complete detached Grid before commit.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "public bool TryRestoreSnapshot(",
            "Facility-world runtime must not expose a direct restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "RestoreBuilding(",
            "Facility restore must not retain the direct live-building reconstruction fallback.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "AddWarning(",
            "Facility restore must reject authored-layer and state-module mismatches instead of warning and defaulting.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "ResolveRestoreGrid",
            "Character restore must consume the detached facility grid while a world candidate is active.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "Character restore candidate staging is not active or already has a value.",
            "Character restore must not publish or mutate live actors outside the save registry transaction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveValidation.cs",
            "ValidatePopulationProfiles",
            "Character payloads must pass strict nested actor, profile, reputation, and carry validation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonCharacterWorldSaveData,\n        CharacterWorldRestoreCandidate>",
            "Character section commits must stage detached actors without requiring a rollback image.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "public CharacterWorldRestoreCandidate PrepareRestoreCandidate(",
            "Character-world staging must build detached actors before commit.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "public int Restore(Grid grid, DungeonCharacterWorldSaveData",
            "Character-world runtime must not expose a direct restore bypass.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "detached facility-world candidate grid",
            "Character restore must fail when the required facility candidate Grid is absent.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "PreserveLiveActors",
            "Character restore must never preserve or mix live actors into an authored save candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterV18RestoreIdentityResolver.cs",
            "AddWarning(",
            "Character restore must reject malformed state instead of warning and defaulting.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "V18 legacy CharacterId normalized in 'characters.world'",
            "Supported V18 character-ID normalization must remain visible in the restore report.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "TryFindNearestWalkablePosition",
            "Character restore must not move saved actors to a different cell.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "CharacterWorldRestoreQuiescenceParticipant",
            "Live actor quiescence must occur inside final character publication, not in an earlier participant.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs",
            "CharacterWorldRestoreQuiescenceParticipant",
            "Composition must not reintroduce an early live-character mutation participant.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "stagedCandidate.ActorsById",
            "Downstream restore lookups must read staged character IDs without replacing the live index.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/CharacterSpawnObjectFactory.cs",
            "actor.IsDetachedRestoreCandidate",
            "Discarded detached character candidates must be identified before destruction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/CharacterSpawnObjectFactory.cs",
            "DestroyImmediate",
            "Unpublished detached character candidates must be removed synchronously on transaction discard.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/RestoreWorldCandidateIndex.cs",
            "IRestoreWorldCandidateQuery",
            "Downstream restore sections must resolve one shared detached world view.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/CharacterAiWorldRegistry.cs",
            "restoreCandidates.TryGetBuildings",
            "Building queries must redirect to detached facilities while restore staging is active.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Random/RandomStreamProvider.cs",
            "RandomStreamAggregateState",
            "Saved deterministic random streams must swap with the shared runtime aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/RunVariableContracts.cs",
            "RunVariableAggregateState",
            "Run seed, day, variable state, and replay history must share the replaceable aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Meta/Core/MetaRunProgressTracker.cs",
            "MetaRunProgressAggregateState",
            "Per-run meta progress must not remain as mutable tracker fields during restore.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Meta/Core/MetaRunLifecycleAggregateState.cs",
            "MetaRunLifecycleAggregateState",
            "Run completion and latest-result state must publish with the aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchContracts.cs",
            "BlueprintResearchAggregateState",
            "Research tasks, unlocks, progress, and queue state must share one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Research/Core/KnowledgeResidueContracts.cs",
            "KnowledgeResidueAggregateState",
            "Knowledge-residue tasks and delivery sequencing must restore through a replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs",
            "PublishedRestoreRevision",
            "Research workforce and availability projection must wait for aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Codex/Core/CodexSystem.cs",
            "CodexAggregateState",
            "Codex discoveries and information lines must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Recruitment/RegularCustomerSystem.cs",
            "RegularCustomerAggregateState",
            "Regular-customer visit and recruitment records must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/FacilityShop/Core/FacilityShopDomain.cs",
            "FacilityShopAggregateState",
            "Facility-shop day and unlock records must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/FacilityShop/Core/FacilityShopDomain.cs",
            "FacilityShopApplication",
            "Facility-shop state changes must pass through the named application boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/FacilityShop/Core/FacilityShopDomain.cs",
            "FacilityShopUnlockState",
            "Facility-shop runtime access must use the shared aggregate-backed state session.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/FacilityShop/DailyFacilityShopRuntime.cs",
            "PublishedRestoreRevision",
            "Facility-shop offers must be projected only after aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureAggregateStates.cs",
            "ElectricalNetworkAggregateState",
            "Electrical infrastructure must restore through a replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureAggregateStates.cs",
            "FluidNetworkAggregateState",
            "Fluid infrastructure must restore through a replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureAggregateStates.cs",
            "ConveyorAggregateState",
            "Conveyor infrastructure must restore through a replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Automation/Core/AutomationCoreModels.cs",
            "AutomationAggregateState",
            "Automation infrastructure must restore through a replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Automation/Core/DungeonStory.Automation.asmdef",
            "\"DungeonStory.Foundation\"",
            "Automation state must depend only on the Foundation identity boundary.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Automation/Core/DungeonStory.Automation.asmdef",
            "Assembly-CSharp",
            "Automation state must not depend on the default gameplay assembly.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Production/Core/DungeonStory.Production.asmdef",
            "\"DungeonStory.Foundation\"",
            "Production state must depend on the Foundation identity boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Production/Core/DungeonStory.Production.asmdef",
            "\"DungeonStory.Work\"",
            "Production snapshots must depend on the named Work contract boundary.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Production/Core/DungeonStory.Production.asmdef",
            "Assembly-CSharp",
            "Production state must not depend on the default gameplay assembly.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Operation/Core/EventAlertAggregateState.cs",
            "EventAlertAggregateState",
            "Event-alert history, dismissal state, and ID sequencing must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/EventAlertRuntime.cs",
            "PublishedRestoreRevision",
            "Event-alert buttons and detail selection must be projected only after aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Operation/Core/OperatingDaySettlementDomain.cs",
            "OperatingDaySettlementAggregateState",
            "Operating-day ledgers, debt, and report history must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Work/WorkOrderAggregateState.cs",
            "WorkOrderAggregateState",
            "Work-order progress, ID sequencing, and scheduling version must restore through one replaceable aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifePopulationState.cs",
            "WildlifePopulationState",
            "Wildlife actors, raid orders, scheduling, and sequencing must have one runtime aggregate owner.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeRuntime.cs",
            "IDungeonRestoreTransactionParticipant",
            "Wildlife restore must participate in detached world publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRestoreCoordinator.cs",
            "SetExteriorZoneCandidate",
            "Exterior zones must be indexed as detached candidates until atomic publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs",
            "300.world.exterior-zones",
            "Exterior-zone publication must follow facilities, characters, and wildlife.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalAggregateState.cs",
            "OffenseReturnArrivalAggregateState",
            "Offense return queues and barriers must restore through one replaceable Aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalAggregateState.cs",
            "CharacterMedicalAggregateState",
            "Medical orders and their sequence must restore through one replaceable Aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalRestoreRuntime.cs",
            "350.world.medical",
            "Medical downed-character Grid projection must publish after the detached world candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandAggregateState.cs",
            "CharacterCombatCommandAggregateState",
            "Combat commands, stance, revisions, and sequence must have one replaceable Aggregate owner.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterCombatCommandRestoreCoordinator.cs",
            "400.world.combat-command-stances",
            "Combat stance Unity projection must publish after restored characters and medical projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/CharacterAiWorldRegistry.cs",
            "CharacterActorCollection.GetCanonical",
            "Character world registration must store canonical actor components rather than compatibility subclasses.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterCombatCommandUnityLifecycleAdapter.cs",
            "bodyHealth.GetSnapshot(patient).Downed",
            "Combat rescue completion must defer to the authoritative body-health downed state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandModels.cs",
            "CharacterCombatCommandTerminatedEvent",
            "Combat command terminal transitions must remain observable without adding a second saved state owner.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandRuntime.Lifecycle.cs",
            "FindCompletedRescues",
            "Combat command lifecycle selection must remain a pure named-assembly policy.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandRuntime.Lifecycle.cs",
            "CharacterActor",
            "Named combat lifecycle policy must not depend on Unity character objects.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs",
            "RegisterEntryPoint<CharacterCombatCommandUnityLifecycleAdapter>",
            "Combat command ticking, subscriptions, and cleanup must run through the Unity lifecycle adapter.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Combat/Core/DefenseTacticalAggregateState.cs",
            "DefenseTacticalAggregateState",
            "Defense position reservations and their sequence must restore through one replaceable Aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceAggregateState.cs",
            "EquipmentMaintenanceAggregateState",
            "Equipment-maintenance policies, assignments, orders, and sequences must restore through one replaceable Aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs",
            "150.world.construction-sites",
            "Construction-site Unity objects must publish after the detached facility grid and before restored characters.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs",
            "TryPrepareConstructionSiteCandidate",
            "Construction sites must be prepared inactive on the restore candidate grid before publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "GetOrCreateWritable",
            "Operational mutation during restore staging must clone shallow-root slots before writing.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Grid/Building/GridBuildingObjectFactory.cs",
            "CreateDetached",
            "Facility restore candidates must be created inactive and without live tile projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.cs",
            "PrepareForDetachedRestore",
            "Detached facilities must not register world services before publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "ValidateRestore",
            "Character identity and definition failures must be rejected before facility-world commit.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
            "PrepareForDetachedRestore",
            "Character candidates must suppress runtime and presentation publication before injection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/CharacterSpawnObjectFactory.cs",
            "CreateDetached",
            "Staff restore must construct candidates below an inactive hierarchy.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Core/OwnerRunManager.cs",
            "PublishRestoreCandidate",
            "Owner-manager state must change only after the detached owner is fully prepared.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Items/WorldItemRepository.cs",
            "DungeonRuntimeAggregateRootStore",
            "Physical item repository state must participate in the composition-wide aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Items/ItemHaulingSettingsSO.cs",
            "ItemHaulingSettingsRuntimeState",
            "Restored hauling settings must live in the detached aggregate root instead of mutating user settings.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
            "PublishedRestoreRevision",
            "Physical item markers and warehouse projections must wait for aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Factions/FactionRuntime.cs",
            "DungeonRuntimeAggregateRootStore",
            "Faction and strategic-route state must participate in the composition-wide aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Factions/FactionRuntime.cs",
            "PublishedRestoreRevision",
            "Faction world-site projection must wait for aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Meta/Core/MetaProgressionModel.cs",
            "DungeonRuntimeAggregateRootStore",
            "Meta progression state must participate in the composition-wide aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs",
            "SurvivalFoodAggregateState",
            "Survival food and meal-sequence state must restore as one aggregate root slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalSaveSections.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonSurvivalSaveData,\n        SurvivalFoodRestoreCandidate>",
            "Survival resources persistence must use the common strict typed boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalSaveSections.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonDarkSurvivalSaveData,\n        DarkSurvivalRestoreCandidate>",
            "Deprivation persistence must stage its complete character, filth, and water candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalSaveSections.cs",
            "IDungeonRollbackFreeSaveSection",
            "Survival resources persistence must be rollback-free after V5 preflight.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalFoodStatePersistence.cs",
            "GetMealSequence",
            "Survival meal IDs must restore their persisted maximum sequence instead of using ledger length.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalFoodStatePersistence.cs",
            "actor?.name",
            "Survival health restore must not use a display name as a persistence-key fallback.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs",
            "MealSequence = restored.mealLedger.Count",
            "Survival meal sequence must not reset to a bounded ledger count after restore.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/CharacterDeprivationStateStore.cs",
            "CharacterDeprivationAggregateState",
            "Character deprivation state must participate in the composition-wide aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/WorldWaterRuntime.cs",
            "WorldWaterAggregateState",
            "World-water state must restore through the detached aggregate root before terrain projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Survival/WorldFilthRuntime.cs",
            "WorldFilthAggregateState",
            "World-filth state must restore through the detached aggregate root before visual and work-target projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs",
            "CharacterConsumablesAggregateState",
            "Character diet, substance, and delivery state must restore through one detached aggregate slot.");
        const string characterConsumablesRuntimePath =
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs";
        const string characterConsumablesSavePath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/CharacterConsumablesSaveSection.cs";
        RequireSourceContract(
            errors,
            characterConsumablesSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonCharacterConsumablesSaveData,\n        CharacterConsumablesRestoreCandidate>",
            "Character-consumables persistence must use the strict Infrastructure candidate boundary.");
        RequireSourceContract(
            errors,
            characterConsumablesSavePath,
            "persistence.PublishRestoreCandidate(candidate)",
            "Character-consumables commit must publish only its detached restore candidate.");
        RequireSourceContract(
            errors,
            characterConsumablesRuntimePath,
            "public CharacterConsumablesRestoreCandidate BuildRestoreCandidate(",
            "Character-consumables staging must build a validated detached candidate.");
        RequireSourceContract(
            errors,
            characterConsumablesRuntimePath,
            "public void PublishRestoreCandidate(",
            "Character-consumables runtime must publish only an explicit candidate.");
        ForbidSourceContract(
            errors,
            characterConsumablesRuntimePath,
            "public void Restore(DungeonCharacterConsumablesSaveData",
            "Character-consumables runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesPersistenceContracts.cs",
            "public DungeonCharacterConsumablesSaveData Payload",
            "Character-consumables restore candidates must remain opaque.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/AnimalHusbandryAggregateState.cs",
            "AnimalHusbandryAggregateState",
            "Animal and pen-policy state must replace one detached aggregate slot before capture reconciliation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Captivity/Core/CaptivityAggregateState.cs",
            "CaptivityAggregateState",
            "Captives, policies, and their sequences must share one replaceable aggregate state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Buildings/Core/DoorAccessSubjectAggregateState.cs",
            "PublishedRestoreRevision",
            "Captive and captured-wildlife door groups must follow the published aggregate root revision.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Captivity/Core/CaptivitySaveValidation.cs",
            "MaximumCaptives",
            "Captivity payloads must be strictly validated before candidate state creation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/CaptivityRestoreCoordinator.cs",
            "450.world.captivity",
            "Captivity restore must publish through an ordered transaction participant.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CaptivitySaveSection.cs",
            "DungeonStrictJsonSaveSection<",
            "Captivity persistence must use the common strict typed preflight boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CaptivitySaveSection.cs",
            "CaptivityRestoreCandidate>",
            "Captivity persistence must publish only a validated detached candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CaptivitySaveSection.cs",
            "IDungeonRollbackFreeSaveSection",
            "Captivity restore must publish only its detached aggregate and door-subject candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Captivity/Core/CapturedWildlifeAggregateState.cs",
            "CapturedWildlifeAggregateState",
            "Captured wildlife must restore through a detached aggregate slot before actor projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Captivity/Core/CircusAggregateState.cs",
            "CircusAggregateState",
            "Circus orders and sequence must restore through one detached aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs",
            "MaximumCapturedWildlife",
            "Circus and captured-wildlife V2 payloads must fail strict validation before candidate construction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Save/CircusRestoreCoordinator.cs",
            "500.world.circus",
            "Circus orders and captured wildlife must publish through one ordered restore participant.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CircusSaveSection.cs",
            "DungeonStrictJsonSaveSection<",
            "Circus persistence must use the common strict typed preflight boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CircusSaveSection.cs",
            "CircusRestoreCandidate>",
            "Circus persistence must publish only a validated detached candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/CircusSaveSection.cs",
            "IDungeonRollbackFreeSaveSection",
            "Circus restore must publish only its detached order and captured-wildlife candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs",
            "ReplaceCapturedWildlifeSubjects",
            "Captured-wildlife door membership must be staged as Aggregate state before publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryAggregateState.cs",
            "SurgeryAggregateState",
            "Surgery orders, parts, storage, policies, corpse state, and anatomy must share one replaceable Aggregate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgerySaveValidation.cs",
            "DungeonSurgerySaveData.CurrentVersion",
            "Surgery V7 payloads must be strictly validated before candidate construction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "SurgeryStatusData statusData",
            "Surgery order status must persist a stable typed code and scalar/ID payload.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "SurgeryStatusData environmentWait",
            "Surgery environment waits must persist typed status data.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "SurgeryStatusData environmentRecovery",
            "Surgery environment recovery must persist typed status data.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgerySaveValidation.cs",
            "ValidateStatus(order.statusData",
            "Surgery V7 restore must reject unknown typed status codes and parameters.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "public string status",
            "Surgery orders must not expose a legacy string status projection.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "environmentWaitReason",
            "Surgery environment waits must not persist completed sentences.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "environmentRecoveryWorkStatus",
            "Surgery environment recovery must not persist completed sentences.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "public string summary",
            "Surgery risk persistence must not store a completed summary sentence.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "SurgeryRiskSummaryCode summaryCode",
            "Surgery risk persistence must store a stable summary code.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgerySaveValidation.cs",
            "Enum.IsDefined(typeof(SurgeryRiskSummaryCode), risk.summaryCode)",
            "Surgery V7 restore must reject unknown risk summary codes.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/AnatomyRuntimeContracts.cs",
            "out string failureReason",
            "Medical anatomy command ports must return localization-neutral DomainFailure values.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs",
            "out string failureReason",
            "Surgery command ports must return localization-neutral DomainFailure values.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs",
            "public interface ISurgery" + "Runtime",
            "Surgery must not expose query, work commands, and persistence through a broad runtime authority.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs",
            "public interface ISurgeryQuery",
            "Surgery reads must use the narrow query facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs",
            "public interface ISurgeryWorkCommand",
            "Surgery work mutation must use the narrow command facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs",
            "public interface ISurgeryPersistence",
            "Surgery save capture must use the narrow persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "private readonly ISurgeryPersistence persistence;",
            "Surgery save section must depend only on the persistence facet.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryWorkExecutionHandler.cs",
            "CharacterSurgeryUiText",
            "Surgery work execution must emit stable codes without depending on presentation localization.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgicalPartProductionOutputHandler.cs",
            "GetRequired(new ItemDefinitionId(context.ItemId))",
            "Surgical part production must obtain authored display data from the authoritative item SO catalog.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "DungeonStrictJsonSaveSection<",
            "Surgery persistence must use the strict detached-candidate boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "IDungeonRollbackFreeSaveSection",
            "Surgery restore must discard failed detached candidates without replaying a live rollback image.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "CharacterMedicalSaveSection :\n    DungeonStrictJsonSaveSection<",
            "Character medical restore must publish only its detached aggregate and downed-occupant candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalModels.cs",
            "public interface ICharacterMedical" + "Runtime",
            "Character medical query, command, and persistence must not share a broad runtime authority.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalModels.cs",
            "public interface ICharacterMedicalQuery",
            "Character medical reads must use the narrow query facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalModels.cs",
            "public interface ICharacterMedicalCommand",
            "Character medical mutations must use the narrow command facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CharacterMedicalModels.cs",
            "public interface ICharacterMedicalPersistence",
            "Character medical save and restore must use the narrow persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "private readonly ICharacterMedicalPersistence persistence;",
            "Character medical save section must depend only on the persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "DefenseTacticalSaveSection :\n    DungeonStrictJsonSaveSection<",
            "Defense tactical restore must replace only its detached aggregate state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "EquipmentMaintenanceSaveSection :\n    DungeonStrictJsonSaveSection<",
            "Equipment maintenance restore must replace only its detached aggregate state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "CharacterCombatCommandSaveSection :\n    DungeonStrictJsonSaveSection<",
            "Combat command restore must publish only its detached aggregate and stance projection candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgeryRestoreCoordinator.cs",
            "525.world.surgery",
            "Surgery state and Unity projections must publish through one ordered restore participant.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryModels.cs",
            "void Restore(DungeonSurgerySaveData",
            "Surgery runtime contracts must not expose warning-based direct restore mutation.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Medical/Core/SurgeryPersistence.cs",
            "Restore(",
            "Surgery persistence must capture only; restore belongs to the strict transaction coordinator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs",
            "RestoreParts(",
            "Surgical part state must be replaced only through the surgery Aggregate candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionAggregateState.cs",
            "InvasionAggregateState",
            "Threat, campaign, and defense policy state must share one replaceable invasion Aggregate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveValidation.cs",
            "DungeonInvasionSaveData.CurrentVersion",
            "Invasion V5 payloads must be strictly validated before candidate construction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionSaveSection.cs",
            "DungeonStrictJsonSaveSection<",
            "Invasion persistence must use the strict detached-candidate boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionSaveSection.cs",
            "IDungeonRollbackFreeSaveSection",
            "Invasion restore must publish only its detached aggregate and intruder candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveService.cs",
            "550.world.invasion",
            "Invasion state and mutually referential Unity candidates must publish through one restore participant.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveService.cs",
            "IDungeonDiscardableRestoreCandidate",
            "Invasion preflight and failed stages must discard detached intruder, evacuation, and engagement candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveService.cs",
            "public interface IInvasionSaveRuntimePort",
            "Named invasion save authority must reach scene runtimes only through an explicit port.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/InvasionSaveRuntimeAdapter.cs",
            "InvasionSaveRuntimeAdapter : IInvasionSaveRuntimePort",
            "Default-assembly invasion scene state must be isolated behind the named save port.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/InvasionSaveRuntimeAdapter.cs",
            "aggregateRootStore.Replace(candidate.State);",
            "Invasion section commit must replace only the already prepared Aggregate candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveService.cs",
            "InvasionSceneRuntimeReferences",
            "Named invasion save authority must not reference default-assembly scene runtime types.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderFactory.cs",
            "CreateDetachedPrefablessObject",
            "Prefabless invasion intruders must restore as inactive detached candidates.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Invasion/Core/InvasionSaveService.cs",
            "RestoreFromLegacyPressure",
            "Invasion V4 restore must not retain legacy pressure migration.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionOwnerEvacuationService.cs",
            "warnings?.Add",
            "Owner evacuation restore must reject invalid candidates instead of warning and recalculating.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/DefenseEngagementPersistence.cs",
            "warnings?.Add",
            "Defense engagement restore must reject invalid references instead of skipping them.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Invasion/DefenseEngagementRuntime.Restore.cs",
            "Defense engagement restore must not reintroduce a partial runtime cycle.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/DefenseEngagementRuntime.cs",
            "persistence.PublishRestoreCandidate(",
            "Defense engagement restore publication must remain owned by the single runtime declaration.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/DefenseEngagementRuntime.cs",
            "partial class DefenseEngagementRuntime",
            "Defense engagement runtime must remain a single non-partial declaration.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Diagnostics/GameplayPerformanceWorldConfigurator.cs",
            "TryRegisterPenBorn",
            "Performance fixture seeding must use an explicit wildlife command instead of restore mutation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/ServiceRooms/Core/ServiceSessionAggregate.cs",
            "class ServiceSessionAggregate",
            "Service hub modes, advertised categories, sessions, and version transitions must be owned by the named ServiceRooms aggregate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/CoreSessionAggregateStates.cs",
            "ServiceSessionAggregateState",
            "CoreSession must not retain a second mutable service-session authority.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/CoreSessionAggregateStates.cs",
            "DungeonRunFlowAggregateState",
            "Run phase, outcome, day, and boss flags must restore through one aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Run/DungeonRunFlowApplicationAdapter.cs",
            "PublishedRestoreRevision",
            "Run-flow threat and owner projections must wait for aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "PublishedRestoreRevision",
            "Service-hub subscriptions must wait for aggregate publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "hub.OnBuildingDestroyed -= handler",
            "Service-hub projection must unsubscribe removed or restored hubs.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/ServiceRooms/Core/ServiceSessionAggregate.cs",
            "ServiceSessionEconomicCommand",
            "Service completion must emit one aggregate-owned economic command.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "Dictionary<string, ServiceSessionSnapshot>",
            "The Unity service-session adapter must not retain a second session dictionary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs",
            "WorldResourceAggregateState",
            "World-resource work state and scene indexes must restore through a detached aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Economy/CropPlotRuntime.cs",
            "CropPlotAggregateState",
            "Crop-plot work state and scene indexes must restore through a detached aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs",
            "GrandProjectAggregateState",
            "Grand-project progress, version, and scheduling state must share one aggregate slot.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs",
            "ProductionAggregateStateStore",
            "Production orders and stock sensors must share one replaceable state root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs",
            "rawBillId = saved.billId ?? string.Empty",
            "Production-bill restore must retain the raw bill ID for exact typed-ID validation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs",
            "rawBuildingId = saved.buildingInstanceId ?? string.Empty",
            "Production-bill restore must retain the raw building ID for exact typed-ID validation.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs",
            "nextBillSequence == int.MaxValue",
            "Production-bill creation must fail before the persistent ID sequence overflows.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceSaveValidation.cs",
            "TryParseRepairOrderId",
            "Equipment repair restore must validate the runtime-authored padded ID grammar.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceSaveValidation.cs",
            "ToString(\"D6\", CultureInfo.InvariantCulture)",
            "Equipment repair restore must accept exactly the runtime-authored D6 suffix.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs",
            "orderSequence == int.MaxValue",
            "Equipment repair creation must fail before the persistent ID sequence overflows.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Combat/CombatEquipmentStateServices.cs",
            "CombatEquipmentRuntimeStateStore",
            "Combat loadout, crafting, and lineage work must share one replaceable state root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs",
            "CharacterEnvironmentAggregateStateStore",
            "Character exposure and protective workwear must share one replaceable state root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs",
            "EnvironmentalFieldAggregateStateStore",
            "Environmental-field state must restore through one replaceable Aggregate root.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFieldModels.cs",
            "public interface IEnvironmentalFieldPersistence",
            "Environmental-field save preparation must use a narrow persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFieldModels.cs",
            "public string buildingInstanceId",
            "Environmental thermostats must persist their owning BuildingInstanceId.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFieldModels.cs",
            "public int x;\n    public int y;\n    public float targetTemperatureC;",
            "Environmental thermostats must not use coordinates as persistent owner identity.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldSaveSection.cs",
            "private readonly IEnvironmentalFieldPersistence persistence;",
            "Environmental-field save must depend only on its persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldSaveSection.cs",
            "IDungeonRollbackFreeSaveSection",
            "Environmental-field save must publish only its detached Aggregate candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldSaveSection.cs",
            "IOptionalDungeonSaveSection",
            "Environmental-field state is required in V18 saves.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs",
            "new BuildingThermalEmitterAbility",
            "Environmental thermal capabilities must come from authored BuildingSO abilities.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs",
            "public interface IEnvironmentalWorkwearPersistence",
            "Protective workwear save preparation must use a narrow persistence facet.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs",
            "IEnvironmentalWorkwearPersistence workwear",
            "Character environment restore must consume only the workwear persistence facet.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs",
            "this.research = research;",
            "Protective workwear research authority is mandatory and must not silently accept null.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs",
            "research != null",
            "Protective workwear research checks must not use a semantic null fallback.");
        const string namedEnvironmentCorePath =
            "Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs";
        const string namedEnvironmentFieldPath =
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFieldDomain.cs";
        const string namedEnvironmentPolicyPath =
            "Assets/Scripts/Models/Environment/Core/EnvironmentPolicyDomain.cs";
        RequireSourceContract(
            errors,
            namedEnvironmentFieldPath,
            "public static class EnvironmentalFieldRestoreRules",
            "Environmental-field restore preflight must be owned by the named Environment domain.");
        RequireSourceContract(
            errors,
            namedEnvironmentFieldPath,
            "BuildingInstanceId BuildingId",
            "Named thermostat state must retain typed BuildingInstanceId ownership.");
        RequireSourceContract(
            errors,
            namedEnvironmentFieldPath,
            "public void Commit(EnvironmentalFieldRestoreCandidate candidate)",
            "Named environmental state must publish only a validated detached candidate.");
        ForbidSourceContract(
            errors,
            namedEnvironmentFieldPath,
            "UnityEngine",
            "Named environmental restore state must remain independent of Unity scene types.");
        RequireSourceContract(
            errors,
            namedEnvironmentCorePath,
            "EnvironmentalWorkwearDefinitionSnapshot",
            "Protective workwear content must expose an immutable named definition snapshot.");
        RequireSourceContract(
            errors,
            namedEnvironmentCorePath,
            "RequiredResearchId",
            "Protective workwear snapshots must retain their authored research requirement.");
        RequireSourceContract(
            errors,
            namedEnvironmentCorePath,
            "CharacterEnvironmentRules",
            "Character exposure and thermal rules must be owned by the named Environment domain.");
        RequireSourceContract(
            errors,
            namedEnvironmentPolicyPath,
            "ExternalInfluenceRules",
            "External influence validation must delegate to a named pure rule owner.");
        ForbidSourceContract(
            errors,
            namedEnvironmentPolicyPath,
            "UnityEngine",
            "Named external-influence rules must remain engine-free.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs",
            "ScriptableObject.CreateInstance",
            "Protective workwear runtime must not synthesize missing authored SO content.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/EconomyTransactionLedgerRuntime.cs",
            "TreasuryEconomyAggregateStateStore",
            "Treasury ledger, contracts, procurement, overclock, and defense must share one replaceable state root.");

        const string physicalSectionPath =
            "Assets/Scripts/Services/Items/PhysicalItemsSaveSection.cs";
        RequireSourceContract(
            errors,
            physicalSectionPath,
            "IDungeonSaveSectionPreflight",
            "Physical item restore must reject invalid payloads before staging.");
        RequireSourceContract(
            errors,
            physicalSectionPath,
            "IDungeonRollbackFreeSaveSection",
            "Physical item restore must commit only to the detached aggregate root.");
        RequireSourceContract(
            errors,
            physicalSectionPath,
            "PhysicalItemSaveValidation.Validate",
            "Physical item section preflight must share the runtime's strict validator.");

        const string physicalPersistencePath =
            "Assets/Scripts/Services/Items/WorldItemPersistenceService.cs";
        RequireSourceContract(
            errors,
            physicalPersistencePath,
            "reservedByPersistentId = string.Empty",
            "Physical item capture must omit transient hauling reservations.");
        RequireSourceContract(
            errors,
            physicalPersistencePath,
            ".ThenBy(stack => stack.stackId, StringComparer.Ordinal)",
            "Physical stack capture must use a deterministic persistent-ID tie breaker.");
        ForbidSourceContract(
            errors,
            physicalPersistencePath,
            "CreateLegacyComponents",
            "Physical item restore must not synthesize legacy mutable-state components.");
        ForbidSourceContract(
            errors,
            physicalPersistencePath,
            "snapshot.version < 1",
            "Physical item restore must accept only the exact current payload version.");
        ForbidSourceContract(
            errors,
            physicalPersistencePath,
            "RestoreDirectPickupStack(record)",
            "Physical item restore must not normalize transient reservations after preflight.");

        const string physicalValidationPath =
            "Assets/Scripts/Services/Items/PhysicalItemSaveValidation.cs";
        RequireSourceContract(
            errors,
            physicalValidationPath,
            "snapshot.version != DungeonPhysicalItemSaveData.CurrentVersion",
            "Physical item validation must reject every non-current payload version.");
        RequireSourceContract(
            errors,
            physicalValidationPath,
            "contains transient reservation state",
            "Physical item validation must reject persisted hauling reservations.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/OperatingDaySaveSections.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonOperatingDaySettlementSaveData,\n        OperatingDaySettlementRestoreCandidate>",
            "Operating-day settlement restore must build a detached candidate before commit.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/OperatingDaySettlementSaveService.cs",
            "runtime.PrepareRestoreCandidate(restored)",
            "Operating-day restore must finish Aggregate construction during staging.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/OperatingDaySettlementSaveService.cs",
            "runtime.PublishRestoreCandidate(candidate",
            "Operating-day commit must publish only the prepared candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/OperatingDaySaveSections.cs",
            "EventAlertSaveSection :\n    DungeonStrictJsonSaveSection<",
            "Event-alert restore must replace only its detached aggregate and defer presentation projection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/EventAlertSaveService.cs",
            "runtime.PrepareRestoreHistory",
            "Event-alert staging must finish record construction before commit.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/EventAlertSaveService.cs",
            "runtime.PublishRestoreHistory(candidate)",
            "Event-alert commit must publish only its prepared Aggregate candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/OperatingDaySettlementSaveService.cs",
            "source ??= new DungeonOperatingDaySettlementSaveData()",
            "Operating-day restore must reject null payloads instead of fabricating empty state.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/EventAlertSaveService.cs",
            "source ??= new DungeonEventAlertSaveData()",
            "Event-alert restore must reject null payloads instead of fabricating empty state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/RandomStreamSaveSection.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonRandomStreamsSaveData,\n        RandomStreamRestoreCandidate>",
            "Random-stream restore must replace only its detached aggregate state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/RandomStreamSaveSection.cs",
            "randomStreamProvider.RestoreStates(candidate)",
            "Random-stream commit must publish only its detached candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/RandomStreamSaveSection.cs",
            ".OrderBy(snapshot => snapshot.StreamId, StringComparer.Ordinal)",
            "Random-stream capture must be deterministic.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Core/Save/RandomStreamSaveSection.cs",
            "payload.streams\n                 ?? new List<DungeonRandomStreamStateSaveData>()",
            "Random-stream restore must reject a missing list instead of fabricating empty state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonSaveSectionDebugScenarios.cs",
            "random stream strict boundary",
            "Random-stream fixtures must enforce strict current-version restore.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonSaveSectionDebugScenarios.cs",
            "rootStore.PublishedRestoreRevision == revisionBefore",
            "Random-stream late failure must discard its unpublished root candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Codex/Core/CodexSaveSection.cs",
            "IDungeonRollbackFreeSaveSection",
            "Codex restore must replace only its detached aggregate state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Codex/Core/CodexSaveSection.cs",
            "entries are not in canonical category/ID order",
            "Codex payloads must preserve deterministic authored order.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Codex/Core/CodexSaveSection.cs",
            "source.entries\n                     ?? new List<DungeonCodexEntrySaveData>()",
            "Codex restore must reject missing entries instead of skipping them.");
        const string defenseFacilitySavePath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/DefenseFacilitySaveSection.cs";
        RequireSourceContract(
            errors,
            defenseFacilitySavePath,
            "DefenseFacilitySaveSection :\n    DungeonStrictJsonSaveSection<",
            "Defense-facility persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            defenseFacilitySavePath,
            "persistence.PrepareRestoreState(payload)",
            "Defense-facility staging must build the complete Aggregate candidate.");
        RequireSourceContract(
            errors,
            defenseFacilitySavePath,
            "persistence.PublishRestoreState(candidate)",
            "Defense-facility commit must publish only its prepared Aggregate candidate.");
        RequireSourceContract(
            errors,
            defenseFacilitySavePath,
            "DefenseFacilitySaveRules.Validate(payload)",
            "Defense-facility section preflight must share its strict payload validator.");
        ForbidSourceContract(
            errors,
            defenseFacilitySavePath,
            "IOptionalDungeonSaveSection",
            "V18 defense-facility state must be required instead of fabricating defaults for a missing section.");
        const string defenseFacilityValidationPath =
            "Assets/Scripts/Models/Defense/Core/DefenseDomain.cs";
        RequireSourceContract(
            errors,
            defenseFacilityValidationPath,
            "data.version != DefenseFacilitySaveData.CurrentVersion",
            "Defense-facility payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            defenseFacilityValidationPath,
            "payload is not in canonical facility-ID order",
            "Defense-facility payloads must preserve deterministic facility ordering.");
        ForbidSourceContract(
            errors,
            defenseFacilitySavePath,
            "new DefenseFacilitySaveData()",
            "Defense-facility restore must not synthesize missing or invalid state.");
        const string factionSavePath =
            "Assets/Scripts/Services/Factions/FactionSaveSection.cs";
        RequireSourceContract(
            errors,
            factionSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonFactionSaveData,\n        FactionRestoreCandidate>",
            "Faction persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            factionSavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Faction commit must publish only its detached Aggregate candidate.");
        RequireSourceContract(
            errors,
            factionSavePath,
            "runtime.PrepareRestoreCandidate(payload)",
            "Faction staging must prepare a detached candidate instead of restoring a DTO.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Factions/FactionRuntime.cs",
            "public void Restore(DungeonFactionSaveData",
            "Faction runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Factions/FactionRuntime.cs",
            "public FactionRestoreCandidate BuildRestore",
            "Faction runtime must use the explicit prepare-candidate API.");
        RequireSourceContract(
            errors,
            factionSavePath,
            "FactionPayloadValidation.Validate(",
            "Faction section preflight must share its strict payload validator.");
        ForbidSourceContract(
            errors,
            factionSavePath,
            "IOptionalDungeonSaveSection",
            "V18 faction state must be required instead of regenerating neutral factions.");
        const string factionValidationPath =
            "Assets/Scripts/Models/Factions/Core/FactionPayloadValidation.cs";
        RequireSourceContract(
            errors,
            factionValidationPath,
            "data.version != DungeonFactionSaveData.CurrentVersion",
            "Faction payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            factionValidationPath,
            "does not contain every authored faction exactly once",
            "Faction payloads must preserve the complete authored faction set.");
        RequireSourceContract(
            errors,
            factionValidationPath,
            "!itemExists(itemId)",
            "Faction cargo must reference concrete authored item definitions.");
        RequireSourceContract(
            errors,
            factionSavePath,
            "itemCatalog.TryGetDefinition(itemId, out _)",
            "Faction save preflight must project the authored item catalog into validation.");
        ForbidSourceContract(
            errors,
            factionSavePath,
            "new DungeonFactionSaveData()",
            "Faction restore must not synthesize missing or invalid state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Factions/Editor/SpeciesFactionDefenseExpansionDebugScenarios.cs",
            "ValidateFactionLateFailureDiscard(",
            "Faction strict save must prove registry late-failure discard.");
        const string grandProjectSavePath =
            "Assets/Scripts/Services/Economy/Planning/GrandProjectSaveSection.cs";
        RequireSourceContract(
            errors,
            grandProjectSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonGrandProjectSaveData,\n        GrandProjectRestoreCandidate>",
            "Grand-project persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            grandProjectSavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Grand-project commit must publish only its detached restore candidate.");
        RequireSourceContract(
            errors,
            grandProjectSavePath,
            "GrandProjectSaveValidation.Validate(",
            "Grand-project section preflight must share its strict payload validator.");
        const string grandProjectRuntimePath =
            "Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs";
        RequireSourceContract(
            errors,
            grandProjectRuntimePath,
            "public void PublishRestoreCandidate(",
            "Grand-project persistence must publish only an explicit detached candidate.");
        ForbidSourceContract(
            errors,
            grandProjectRuntimePath,
            "public void Restore(DungeonGrandProjectSaveData",
            "Grand-project runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            grandProjectRuntimePath,
            "public DungeonGrandProjectSaveData Payload",
            "Grand-project restore candidates must remain opaque outside their aggregate.");
        const string grandProjectValidationPath =
            "Assets/Scripts/Models/Economy/Content/GrandProjectSaveValidation.cs";
        RequireSourceContract(
            errors,
            grandProjectValidationPath,
            "data.version != DungeonGrandProjectSaveData.CurrentVersion",
            "Grand-project payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            grandProjectValidationPath,
            "Inactive grand-project state must have an empty destination and zero work",
            "Grand-project restore must reject lossy inactive-state normalization.");
        ForbidSourceContract(
            errors,
            grandProjectSavePath,
            "new DungeonGrandProjectSaveData()",
            "Grand-project restore must not synthesize missing or invalid state.");
        const string stockPolicySavePath =
            "Assets/Scripts/Services/Economy/Planning/ResourceStockPolicySaveSection.cs";
        RequireSourceContract(
            errors,
            stockPolicySavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonResourceStockPolicySaveData,\n        ResourceStockPolicyRestoreCandidate>",
            "Stock-policy persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            stockPolicySavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Stock-policy commit must publish only its detached restore candidate.");
        RequireSourceContract(
            errors,
            stockPolicySavePath,
            "runtime.PrepareRestoreCandidate(payload)",
            "Stock-policy staging must prepare a detached candidate instead of restoring a DTO.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs",
            "public void Restore(DungeonResourceStockPolicySaveData",
            "Stock-policy runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs",
            "public ResourceStockPolicyRestoreCandidate BuildRestore",
            "Stock-policy runtime must use the explicit prepare-candidate API.");
        RequireSourceContract(
            errors,
            stockPolicySavePath,
            "ResourceStockPolicySaveValidation.Validate(",
            "Stock-policy section preflight must share its strict payload validator.");
        const string stockPolicyValidationPath =
            "Assets/Scripts/Models/Economy/Content/ResourceStockPolicySaveValidation.cs";
        RequireSourceContract(
            errors,
            stockPolicyValidationPath,
            "data.version != DungeonResourceStockPolicySaveData.CurrentVersion",
            "Stock-policy payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            stockPolicyValidationPath,
            "does not contain every authored item exactly once",
            "Stock-policy payloads must preserve exact authored item coverage.");
        RequireSourceContract(
            errors,
            stockPolicyValidationPath,
            "catalog.TryGetItem(policy.itemId, out _)",
            "Stock policies must reference concrete authored item definitions.");
        ForbidSourceContract(
            errors,
            stockPolicySavePath,
            "new DungeonResourceStockPolicySaveData()",
            "Stock-policy restore must not synthesize missing or invalid state.");
        const string removedStockCategoryLookup =
            "TryGetStock" + "Category";
        ForbidSourceInvocationAcrossScripts(
            errors,
            nameof(PhysicalItemIds),
            removedStockCategoryLookup,
            "Deleted stock-category lookup calls must remain at zero across runtime and editor sources.");
        const string regionalContractSavePath =
            "Assets/Scripts/Services/Economy/Planning/RegionalSupplyContractSaveSection.cs";
        RequireSourceContract(
            errors,
            regionalContractSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonRegionalSupplyContractSaveData,\n        RegionalSupplyContractRestoreCandidate>",
            "Regional-contract persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            regionalContractSavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Regional-contract commit must publish only its detached restore candidate.");
        RequireSourceContract(
            errors,
            regionalContractSavePath,
            "runtime.PrepareRestoreCandidate(payload)",
            "Regional-contract staging must prepare a detached candidate instead of restoring a DTO.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs",
            "public void Restore(DungeonRegionalSupplyContractSaveData",
            "Regional-contract runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs",
            "public RegionalSupplyContractRestoreCandidate BuildRestore",
            "Regional-contract runtime must use the explicit prepare-candidate API.");
        RequireSourceContract(
            errors,
            regionalContractSavePath,
            "RegionalSupplyContractSaveValidation.Validate(",
            "Regional-contract section preflight must share its strict payload validator.");
        const string regionalContractValidationPath =
            "Assets/Scripts/Models/Economy/Content/RegionalSupplyContractSaveValidation.cs";
        RequireSourceContract(
            errors,
            regionalContractValidationPath,
            "data.version != DungeonRegionalSupplyContractSaveData.CurrentVersion",
            "Regional-contract payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            regionalContractValidationPath,
            "regional-contract:{contract.contractId}",
            "Regional-contract destinations must be derived from canonical contract IDs.");
        RequireSourceContract(
            errors,
            regionalContractValidationPath,
            "catalog.TryGetItem(requirement.itemId, out _)",
            "Regional contracts must require concrete authored items.");
        ForbidSourceContract(
            errors,
            regionalContractSavePath,
            "new DungeonRegionalSupplyContractSaveData()",
            "Regional-contract restore must not synthesize missing or invalid state.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs",
            "EnsureOffers(restored)",
            "Regional-contract restore must not generate or expire offers while publishing a save snapshot.");
        const string productionEconomyFixturePath =
            "Assets/Scripts/Services/Economy/Editor/ProductionEconomyDebugScenarios.cs";
        RequireSourceContract(
            errors,
            productionEconomyFixturePath,
            "ValidateEconomyPlanningLateFailureDiscard();",
            "Economy planning strict saves must prove registry late-failure discard.");
        RequireSourceContract(
            errors,
            productionEconomyFixturePath,
            "aggregateRootStore.PublishedRestoreRevision == revisionBefore",
            "Economy planning late-failure proof must reject candidate publication.");

        string equipmentPath =
            "Assets/Scripts/Services/Combat/CombatEquipmentRuntime.cs";
        string equipmentSource = File.Exists(equipmentPath)
            ? File.ReadAllText(equipmentPath)
            : string.Empty;
        if (equipmentSource.Contains("instances.Clear()", StringComparison.Ordinal)
            || equipmentSource.Contains(
                "moduleInstances.Clear()",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Combat equipment restore must not rewrite physical item repository state.");
        }

        const string researchSavePath =
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchSaveSection.cs";
        string researchSaveSource = File.Exists(researchSavePath)
            ? File.ReadAllText(researchSavePath)
            : string.Empty;
        if (researchSaveSource.Contains(
                "ClearForRestore",
                StringComparison.Ordinal)
            || researchSaveSource.Contains(
                "runtime.RefreshProjectQueueAfterRestore()",
                StringComparison.Ordinal)
            || researchSaveSource.Contains(
                "runtime.EnsureAcquiredBlueprintItemsMaterialized()",
                StringComparison.Ordinal)
            || researchSaveSource.Contains(
                "MigrateLegacyBlueprintResearch",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Research restore must build detached state without clearing live progress or projecting legacy items/workforce state.");
        }

        const string codexSavePath =
            "Assets/Scripts/Models/Codex/Core/CodexSaveSection.cs";
        string codexSaveSource = File.Exists(codexSavePath)
            ? File.ReadAllText(codexSavePath)
            : string.Empty;
        if (codexSaveSource.Contains("ClearForRestore", StringComparison.Ordinal)
            || codexSaveSource.Contains(
                "runtime.State.GetOrCreate",
                StringComparison.Ordinal)
            || codexSaveSource.Contains(
                "runtime.State.AddInfo",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Codex restore must populate a detached state before replacing the aggregate slot.");
        }

        const string regularCustomerSavePath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/RegularCustomerSaveSection.cs";
        string regularCustomerSaveSource = File.Exists(regularCustomerSavePath)
            ? File.ReadAllText(regularCustomerSavePath)
            : string.Empty;
        if (regularCustomerSaveSource.Contains(
                "runtime.State.Restore",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Regular-customer restore must replace detached records instead of clearing live state.");
        }
        RequireSourceContract(
            errors,
            regularCustomerSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonRegularCustomerSaveData,\n        RegularCustomerRestoreCandidate>",
            "Regular-customer persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Recruitment/RegularCustomerSystem.cs",
            "ReplaceAggregate(prepared.State)",
            "Regular-customer commit must publish only its prepared Aggregate candidate.");
        RequireSourceContract(
            errors,
            regularCustomerSavePath,
            "payload.version != DungeonRegularCustomerSaveData.CurrentVersion",
            "Regular-customer payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            regularCustomerSavePath,
            "non-canonical, or unordered record ID",
            "Regular-customer IDs must be canonical and deterministically ordered.");
        RequireSourceContract(
            errors,
            regularCustomerSavePath,
            "invalid recruitment status hierarchy",
            "Regular-customer restore must reject lossy status promotion.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Recruitment/Editor/RegularCustomerDebugScenarios.cs",
            "Injected late regular-customer restore failure.",
            "Regular-customer proof must verify a late registry failure leaves live Aggregate state untouched.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Recruitment/Editor/RegularCustomerDebugScenarios.cs",
            "IDungeonStagedSaveSection,\n        IDungeonRollbackFreeSaveSection",
            "Regular-customer late-failure proof must execute the all-marker discard path.");
        ForbidSourceContract(
            errors,
            regularCustomerSavePath,
            "source.records\n                     ??",
            "Regular-customer restore must not synthesize a missing record list.");

        const string facilityShopSavePath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/FacilityShopSaveSection.cs";
        string facilityShopSaveSource = File.Exists(facilityShopSavePath)
            ? File.ReadAllText(facilityShopSavePath)
            : string.Empty;
        const string facilityShopDataPath =
            "Assets/Scripts/Models/Economy/Content/DungeonFacilityShopSaveData.cs";
        string facilityShopDataSource = File.Exists(facilityShopDataPath)
            ? File.ReadAllText(facilityShopDataPath)
            : string.Empty;
        if (facilityShopSaveSource.Contains(
                "BlueprintResearchRuntime",
                StringComparison.Ordinal)
            || facilityShopSaveSource.Contains(
                "research.State",
                StringComparison.Ordinal)
            || facilityShopSaveSource.Contains(
                "unlockedBuildingIds",
                StringComparison.Ordinal)
            || facilityShopDataSource.Contains(
                "unlockedBuildingIds",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Facility-shop save must not duplicate or restore research unlock authority.");
        }
        RequireSourceContract(
            errors,
            facilityShopSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonFacilityShopSaveData,\n        FacilityShopRestoreCandidate>",
            "Facility-shop persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/FacilityShop/Core/FacilityShopDomain.cs",
            "aggregateRootStore.Replace(candidate.State)",
            "Facility-shop commit must publish only its prepared Aggregate candidate.");
        RequireSourceContract(
            errors,
            facilityShopSavePath,
            "payload.version != DungeonFacilityShopSaveData.CurrentVersion",
            "Facility-shop payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            facilityShopSavePath,
            "duplicate or unordered",
            "Facility-shop unlock IDs must be canonical and deterministically ordered.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/FacilityShop/FacilityShopSystem.cs",
            "Mathf.Max(1, offerDay)",
            "Facility-shop restore must not clamp an invalid offer day.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/FacilityShop/Editor/FacilityShopDebugScenarios.cs",
            "IDungeonStagedSaveSection,\n        IDungeonRollbackFreeSaveSection",
            "Facility-shop late-failure proof must execute the all-marker discard path.");

        const string staffDiscontentSavePath =
            "Assets/Scripts/Services/Character/Work/StaffDiscontentSaveSection.cs";
        RequireSourceContract(
            errors,
            staffDiscontentSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonStaffDiscontentSaveData,\n        StaffDiscontentRestoreCandidate>",
            "Staff-discontent persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/Work/StaffDiscontentRuntime.cs",
            "aggregateRootStore.Replace(candidate.State)",
            "Staff-discontent commit must publish only its prepared state candidate.");
        RequireSourceContract(
            errors,
            staffDiscontentSavePath,
            "payload.version != DungeonStaffDiscontentSaveData.CurrentVersion",
            "Staff-discontent payloads must reject legacy DTO versions.");
        RequireSourceContract(
            errors,
            staffDiscontentSavePath,
            "non-canonical, duplicate, or unordered staff ID",
            "Staff-discontent IDs must be canonical and deterministically ordered.");
        RequireSourceContract(
            errors,
            staffDiscontentSavePath,
            "invalid terminal-status hierarchy",
            "Staff-discontent restore must reject lossy terminal-state normalization.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/Editor/StaffDiscontentDebugScenarios.cs",
            "Injected late staff-discontent restore failure.",
            "Staff-discontent proof must verify a late registry failure leaves live state untouched.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/Editor/StaffDiscontentDebugScenarios.cs",
            "IDungeonStagedSaveSection,\n        IDungeonRollbackFreeSaveSection",
            "Staff-discontent late-failure proof must execute the all-marker discard path.");
        const string staffDiscontentRuntimePath =
            "Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs";
        ForbidSourceContract(
            errors,
            staffDiscontentRuntimePath,
            "Mathf.Clamp(snapshot.mood",
            "Staff-discontent restore must not clamp invalid saved mood.");
        ForbidSourceContract(
            errors,
            staffDiscontentRuntimePath,
            "savedRecords ?? Array.Empty<StaffDiscontentSnapshot>()",
            "Staff-discontent restore must reject a missing snapshot collection.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/AI/Editor/StaffDiscontentDebugScenarios.cs",
            "VerifyStrictSaveBoundary",
            "Staff-discontent scenarios must prove strict round-trip and invalid preflight preservation.");

        const string experiencePacingPath =
            "Assets/Scripts/Services/Run/ExperiencePacingRuntime.cs";
        const string experiencePacingSavePath =
            "Assets/Scripts/Services/Run/ExperiencePacingSaveSection.cs";
        RequireSourceContract(
            errors,
            experiencePacingSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonExperiencePacingSaveData,\n        ExperiencePacingAggregateState>",
            "Experience-pacing persistence must stage a detached aggregate candidate.");
        RequireSourceContract(
            errors,
            experiencePacingSavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Experience-pacing commit must publish only its validated candidate.");
        RequireSourceContract(
            errors,
            experiencePacingSavePath,
            "runtime.PrepareRestoreCandidate(payload)",
            "Experience-pacing staging must prepare a detached candidate instead of restoring a DTO.");
        ForbidSourceContract(
            errors,
            experiencePacingPath,
            "public void Restore(DungeonExperiencePacingSaveData",
            "Experience-pacing runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            experiencePacingPath,
            "IExperiencePacingRestorePublisher",
            "Experience-pacing restore must use its single runtime candidate port.");
        ForbidSourceContract(
            errors,
            experiencePacingPath,
            "IOptionalDungeonSaveSection",
            "Experience-pacing V18 saves must not synthesize a missing section.");

        const string externalInfluenceSavePath =
            "Assets/Scripts/Services/Infrastructure/Core/Save/ExternalInfluenceSaveSection.cs";
        RequireSourceContract(
            errors,
            externalInfluenceSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonExternalInfluenceSaveData,\n        ExternalInfluenceRestoreCandidate>",
            "External-influence persistence must be required, typed, and rollback-free.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentSaveSection.cs",
            "DungeonStrictJsonSaveSection<\n        DungeonCharacterEnvironmentSaveData,\n        CharacterEnvironmentRestoreCandidate>",
            "Character-environment persistence must publish one validated aggregate candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs",
            "DiscardStages(stages, report)",
            "Strict registry failures must discard uncommitted detached candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs",
            "public const int CurrentVersion = 3;",
            "External-influence V3 must persist ecology-resolution state.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs",
            "out DomainFailure failure",
            "External-influence commands must return localization-neutral failures.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs",
            "out string failureReason);",
            "External-influence commands must not expose completed UI strings.");
        ForbidSourceContract(
            errors,
            externalInfluenceSavePath,
            "sectionVersion == 1",
            "External-influence V18 saves must not migrate legacy payloads.");

        const string runFlowSavePath =
            "Assets/Scripts/Services/Run/RunFlowSaveSection.cs";
        RequireSourceContract(
            errors,
            runFlowSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonRunFlowSaveData,\n        DungeonRunFlowAggregateState>",
            "Run-flow persistence must stage a detached aggregate candidate.");
        RequireSourceContract(
            errors,
            runFlowSavePath,
            "restorePublisher.PublishRestoreState(candidate)",
            "Run-flow commit must publish only its validated candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/RunVariableContracts.cs",
            "finalInvasionDefended",
            "Run-flow save state must not retain the dead final-defense projection.");

        const string runVariableSavePath =
            "Assets/Scripts/Services/Run/RunVariableSaveSection.cs";
        RequireSourceContract(
            errors,
            runVariableSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonRunVariableSaveData,\n        RunVariableAggregateState>",
            "Run-variable persistence must stage a detached aggregate candidate.");
        RequireSourceContract(
            errors,
            runVariableSavePath,
            "restorePublisher.PublishRestoreState(candidate)",
            "Run-variable commit must publish only its validated candidate.");
        ForbidSourceContract(
            errors,
            runVariableSavePath,
            "SupportsSectionVersion",
            "Run-variable V18 saves must not accept legacy section versions.");
        RequireSourceContract(
            errors,
            runVariableSavePath,
            "private readonly IRunVariableRuntime runtime;",
            "Run-variable persistence must depend on the CoreSession runtime port.");
        ForbidSourceContract(
            errors,
            runVariableSavePath,
            "DungeonSceneRuntimeReferences",
            "Run-variable persistence must not locate its runtime through scene references.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Run/RunVariableSystem.cs",
            "provider.Reseed(restoredSeed)",
            "Run-variable restore must not duplicate random-stream save authority.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "lateDiscardJson",
            "Core-session strict saves must prove staged candidates are discarded after a late failure.");

        const string dungeonDebugSavePath =
            "Assets/Scripts/Services/Debugging/DungeonDebugSaveSection.cs";
        RequireSourceContract(
            errors,
            dungeonDebugSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonDebugRunSaveData,\n        DungeonDebugRestoreCandidate>",
            "Dungeon-debug persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            dungeonDebugSavePath,
            "debugModeService.PublishRestoreCandidate(candidate)",
            "Dungeon-debug commit must publish only its detached Aggregate candidate.");
        RequireSourceContract(
            errors,
            dungeonDebugSavePath,
            "debugModeService.PrepareRestoreCandidate(payload)",
            "Dungeon-debug staging must prepare a detached candidate instead of restoring a DTO.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Debugging/DungeonDebugRuntime.cs",
            "public void Restore(DungeonDebugRunSaveData",
            "Dungeon-debug runtime must not expose a direct DTO restore bypass.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Debugging/DungeonDebugRuntime.cs",
            "public DungeonDebugRestoreCandidate BuildRestore",
            "Dungeon-debug runtime must use the explicit prepare-candidate API.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Debugging/DungeonDebugRuntime.cs",
            "!aggregateRootStore.IsRestoreStaging",
            "Dungeon-debug restore must suppress presentation events while staging.");

        const string serviceRoomsSavePath =
            "Assets/Scripts/Services/ServiceRooms/ServiceRoomsSaveSection.cs";
        RequireSourceContract(
            errors,
            serviceRoomsSavePath,
            "DungeonStrictJsonSaveSection<\n        ServiceRoomsSaveData,\n        ServiceRoomsRestoreCandidate>",
            "Service-room persistence must use the strict typed rollback-free boundary.");
        RequireSourceContract(
            errors,
            serviceRoomsSavePath,
            "runtime.PublishRestoreCandidate(candidate)",
            "Service-room commit must publish only its detached Aggregate candidate.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "public void Restore(\n        ServiceRoomsSaveData",
            "Service-room runtime must not expose a direct live-state restore bypass.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "restoreWorldCandidates.TryGetBuildings",
            "Service-room restore must resolve references against detached world candidates.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "out DomainFailure failure",
            "Service-room commands must return localization-neutral failures.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "out string failureReason",
            "Service-room commands must not expose completed UI strings.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "public string BlockedReason",
            "Service-room query snapshots must expose a failure code, not a display sentence.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "public string Message",
            "Service-room command results must expose a failure code, not a display sentence.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs",
            "building.RequirePersistentInstanceId().Value",
            "Service-room topology must use the required building instance ID.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs",
            "building.centerPos.x",
            "Service-room topology must not synthesize a persistent key from coordinates.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceRoomsSaveSection.cs",
            "sessionId?.Trim()",
            "Service-room restore must not repair saved session IDs.");

        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyRestoreBoundary",
            "Batch A must prove canonical restore and invalid no-mutation for its strict sections.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyAtomicBatchBoundary",
            "Batch A must reject all six owners atomically when one owner payload is invalid.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "DungeonSaveSectionRegistry registry = new(sections, store);",
            "Batch A atomic proof must execute through the production save registry.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyFinalSectionFailure",
            "Batch A must prove that a final-section failure discards every owner candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "store.PublishedRestoreRevision == revisionBefore",
            "Batch A final-failure proof must preserve the published Aggregate root revision.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyIntegratedRuntimeFlow",
            "Batch A requires one six-owner authored command/query/capture flow.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyCoreSessionStateOwnership",
            "Batch A fixture must prove named-assembly ownership for all six aggregate states.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "VerifyRunVariableDoctrineEdge",
            "Batch A fixture must prove explicit doctrine-catalog effect edges.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "DomainFailureLocalizer localizer = new();",
            "Batch A integration must exercise its presentation localization adapter.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchACoreSessionSaveDebugScenarios.cs",
            "IDungeonSaveSection[] ownerSections",
            "Batch A integration must capture all six owner sections after mutation.");

        const string coreSessionRulesPath =
            "Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs";
        RequireSourceContract(
            errors,
            "Assets/Scripts/Content/DungeonStory.Content.asmdef",
            "\"DungeonStory.Foundation\"",
            "The Content assembly must depend only on the Foundation contract layer.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Content/DungeonStory.Content.asmdef",
            "Assembly-CSharp",
            "The Content assembly must not depend on the default gameplay assembly.");
        const string coreSessionAssemblyPath =
            "Assets/Scripts/Models/CoreSession/DungeonStory.CoreSession.asmdef";
        const string coreSessionContractsPath =
            "Assets/Scripts/Models/CoreSession/CoreSessionContracts.cs";
        RequireSourceContract(
            errors,
            coreSessionAssemblyPath,
            "\"DungeonStory.Foundation\"",
            "Core-session commands must reuse the Foundation failure protocol.");
        RequireSourceContract(
            errors,
            coreSessionAssemblyPath,
            "\"DungeonStory.World\"",
            "Core-session contracts must depend on the World primitive boundary.");
        RequireSourceContract(
            errors,
            coreSessionAssemblyPath,
            "\"noEngineReferences\": true",
            "Core-session contracts must remain free of Unity engine dependencies.");
        ForbidSourceContract(
            errors,
            coreSessionAssemblyPath,
            "Assembly-CSharp",
            "Core-session contracts must not reference the default gameplay assembly.");
        RequireSourceContract(
            errors,
            coreSessionContractsPath,
            "public interface IExperiencePacingRuntime",
            "Experience pacing must expose its contract from the CoreSession assembly.");
        RequireSourceContract(
            errors,
            coreSessionContractsPath,
            "public interface IDungeonRunFlowRuntime",
            "Run flow must expose its contract from the CoreSession assembly.");
        RequireSourceContract(
            errors,
            coreSessionContractsPath,
            "public interface IDungeonDebugModeService",
            "Dungeon debug must expose its contract from the CoreSession assembly.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs",
            "public interface IExternalInfluenceRuntime",
            "External influence must expose its runtime port from CoreSession.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/RunVariableContracts.cs",
            "public interface IRunVariableRuntime",
            "Run variables must expose their save/runtime port from CoreSession.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "public interface IServiceAvailabilityQuery",
            "Service rooms must expose pure query contracts from CoreSession.");

        const string coreSessionAggregateStatesPath =
            "Assets/Scripts/Models/CoreSession/CoreSessionAggregateStates.cs";
        const string externalInfluenceContractsPath =
            "Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs";
        const string runVariableContractsPath =
            "Assets/Scripts/Models/CoreSession/RunVariableContracts.cs";
        (string typeName, string expectedPath)[] coreSessionStateOwners =
        {
            (nameof(ExperiencePacingAggregateState), coreSessionAggregateStatesPath),
            (nameof(ExternalInfluenceAggregateState), externalInfluenceContractsPath),
            (nameof(DungeonRunFlowAggregateState), coreSessionAggregateStatesPath),
            (nameof(RunVariableAggregateState), runVariableContractsPath),
            (nameof(DungeonDebugModeState), coreSessionAggregateStatesPath)
        };
        foreach ((string typeName, string expectedPath) in coreSessionStateOwners)
        {
            RequireOnlySourceDeclaration(
                errors,
                typeName,
                expectedPath,
                $"Batch A state '{typeName}' must have one declaration owned by CoreSession.");
        }
        RequireOnlySourceDeclaration(
            errors,
            nameof(DungeonStory.ServiceRooms.ServiceSessionAggregate),
            "Assets/Scripts/Models/ServiceRooms/Core/ServiceSessionAggregate.cs",
            "ServiceSessionAggregate must have one declaration owned by ServiceRooms.");

        string runVariableContractsSource = File.Exists(runVariableContractsPath)
            ? File.ReadAllText(runVariableContractsPath)
            : string.Empty;
        if (runVariableContractsSource.Contains(
                "OwnerDoctrines",
                StringComparison.Ordinal)
            || runVariableContractsSource.Contains(
                "ToSummaryText",
                StringComparison.Ordinal)
            || runVariableContractsSource.Contains(
                "ToDetailText",
                StringComparison.Ordinal)
            || Regex.IsMatch(runVariableContractsSource, "[가-힣]"))
        {
            errors.Add(
                "Run-variable CoreSession contracts must not own doctrine catalogs "
                + "or presentation formatting/text.");
        }

        const string runVariableEffectsPath =
            "Assets/Scripts/Services/Run/RunVariableEffects.cs";
        const string runVariableRuntimePath =
            "Assets/Scripts/Services/Run/RunVariableSystem.cs";
        RequireSourceContract(
            errors,
            runVariableEffectsPath,
            "IOwnerDoctrineDefinitionCatalog ownerDoctrines",
            "Run-variable effects must receive the owner-doctrine catalog explicitly.");
        RequireSourceContract(
            errors,
            runVariableEffectsPath,
            "ResolveOwnerDoctrineEffects(state, ownerDoctrines)",
            "Run-variable effect aggregation must use the explicitly supplied doctrine catalog.");
        ForbidSourceContract(
            errors,
            runVariableEffectsPath,
            "state.OwnerDoctrines",
            "Run-variable effects must not recover doctrine authority from aggregate state.");
        RequireSourceContract(
            errors,
            runVariableRuntimePath,
            "ResolveOwnerDoctrineCatalog()",
            "Run-variable runtime must pass its injected doctrine catalog at each effect edge.");
        string runVariableRuntimeSource = File.Exists(runVariableRuntimePath)
            ? File.ReadAllText(runVariableRuntimePath)
            : string.Empty;
        string[] doctrineEffectEdges =
        {
            "GetGuestDemandMultiplier",
            "GetStockCostMultiplier",
            "GetFacilityShopCostMultiplier",
            "GetBlueprintCostMultiplier",
            "GetThreatRiseMultiplier",
            "GetWarningThresholdMultiplier",
            "ApplyInvasionSettings"
        };
        foreach (string edge in doctrineEffectEdges)
        {
            if (!Regex.IsMatch(
                    runVariableRuntimeSource,
                    $@"RunVariableEffects\.{Regex.Escape(edge)}\(\s*state,\s*ResolveOwnerDoctrineCatalog\(\)",
                    RegexOptions.CultureInvariant))
            {
                errors.Add(
                    $"Run-variable runtime edge '{edge}' must explicitly pass the injected doctrine catalog.");
            }
        }

        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Run/RunVariableModel.cs",
            "The legacy default-assembly run-variable state model must be removed.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Run/RunVariableAggregateState.cs",
            "The legacy default-assembly run-variable aggregate declaration must be removed.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Run/DungeonRunFlowAggregateState.cs",
            "The legacy default-assembly run-flow aggregate declaration must be removed.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionAggregateState.cs",
            "The legacy default-assembly service-session aggregate declaration must be removed.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ExternalInfluenceContracts.cs",
            "UnityEngine",
            "External influence contracts must remain engine-free.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "BuildableObject",
            "Service-room domain contracts must not retain Unity building objects.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "CharacterActor",
            "Service-room domain contracts must not retain Unity character objects.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Run/ExperiencePacingRuntime.cs",
            "public interface IExperiencePacingRuntime",
            "Experience pacing contracts must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Run/DungeonRunFlowRuntime.cs",
            "public interface IDungeonRunFlowRuntime",
            "Run-flow contracts must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Debugging/DungeonDebugModels.cs",
            "public interface IDungeonDebugModeService",
            "Dungeon-debug contracts must not fall back into Assembly-CSharp.");
        if (!string.Equals(
                typeof(IExperiencePacingRuntime).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(IDungeonRunFlowRuntime).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(IDungeonDebugModeService).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(IExternalInfluenceRuntime).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(IRunVariableRuntime).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(IServiceAvailabilityQuery).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(ExperiencePacingAggregateState).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(ExternalInfluenceAggregateState).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(DungeonRunFlowAggregateState).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(RunVariableAggregateState).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(DungeonDebugModeState).Assembly.GetName().Name,
                "DungeonStory.CoreSession",
                StringComparison.Ordinal)
            )
        {
            errors.Add(
                "Batch A core-session contracts and aggregate states are not loaded from "
                + "DungeonStory.CoreSession.");
        }
        if (!string.Equals(
                typeof(DungeonStory.ServiceRooms.ServiceSessionAggregate)
                    .Assembly.GetName().Name,
                "DungeonStory.ServiceRooms",
                StringComparison.Ordinal))
        {
            errors.Add(
                "ServiceSessionAggregate must be owned by DungeonStory.ServiceRooms.");
        }
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs",
            ".As<IRunVariableRuntime>()",
            "Composition must expose the scene RunVariable adapter through its CoreSession port.");
        if (!string.Equals(
                typeof(ExteriorIncidentKind).Assembly.GetName().Name,
                "DungeonStory.World",
                StringComparison.Ordinal))
        {
            errors.Add(
                "ExteriorIncidentKind must be owned by DungeonStory.World.");
        }
        RequireSourceContract(
            errors,
            "Assets/Scripts/Content/CoreSession/CoreSessionRulesDefinition.cs",
            "public interface ICoreSessionRulesProvider",
            "Batch A requires one explicit provider for root-authored core-session rules.");
        RequireSourceContract(
            errors,
            coreSessionRulesPath,
            "public CoreSessionRulesDefinition CreateRuntimeDefinition()",
            "The authored rules asset must project one immutable runtime definition.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Items/GameContentCatalog.cs",
            "CoreSessionRules = Domain.CoreSessionRules.CreateRuntimeDefinition();",
            "The root catalog must create the core-session runtime projection once.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Content/GameDomainContentCatalogSO.cs",
            "[SerializeField] private CoreSessionRulesSO coreSessionRules;",
            "The root domain catalog must serialize the Batch A rules authority.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs",
            ".As<ICoreSessionRulesProvider>()",
            "Composition must expose the root catalog as the sole core-session rules provider.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs",
            ".As<IServiceRoomResearchQuery>()",
            "Composition must provide the ServiceRooms adapter through an explicit research query port.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/DungeonTitleLifetimeScope.cs",
            "builder.Register<TitleGameSpeedController>(Lifetime.Singleton)",
            "Title settings must receive an explicit title-scoped pause capability.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/DungeonTitleLifetimeScope.cs",
            "new GameSessionState",
            "Title settings must not fabricate a gameplay session Aggregate.");

        string[] coreRuleOwners =
        {
            "Assets/Scripts/Services/Infrastructure/ExperiencePacingApplicationAdapter.cs",
            "Assets/Scripts/Services/Run/DungeonRunFlowApplicationAdapter.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs",
            "Assets/Scripts/Services/Debugging/DungeonDebugRuntime.cs",
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs"
        };
        foreach (string ownerPath in coreRuleOwners)
        {
            RequireSourceContract(
                errors,
                ownerPath,
                "ICoreSessionRulesProvider",
                $"Batch A owner '{ownerPath}' must consume the shared authored rules authority.");
            ForbidSourceContract(
                errors,
                ownerPath,
                "CoreSessionRulesSO rules",
                $"Batch A owner '{ownerPath}' must not retain the authored SO at runtime.");
        }
        if (!string.Equals(
                typeof(CoreSessionRulesDefinition).Assembly.GetName().Name,
                "DungeonStory.Content",
                StringComparison.Ordinal)
            || !string.Equals(
                typeof(ICoreSessionRulesProvider).Assembly.GetName().Name,
                "DungeonStory.Content",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Core-session rules projection and provider must be loaded from DungeonStory.Content.");
        }
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Run/RunVariableSystem.cs",
            "IRunVariableDefinitionCatalog definitionCatalog",
            "Run-variable authority must consume the root-authored definition catalog.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Editor/BatchAContentAuthorityDebugScenarios.cs",
            "Core-session owners do not share one immutable root-authored rules projection",
            "Batch A content proof must verify one immutable root-derived rules projection.");

        const string eventAlertRuntimePath =
            "Assets/Scripts/Views/UI/Core/EventAlertRuntime.cs";
        string eventAlertRuntimeSource = File.Exists(eventAlertRuntimePath)
            ? File.ReadAllText(eventAlertRuntimePath)
            : string.Empty;
        const string operatingDaySaveSectionsPath =
            "Assets/Scripts/Services/Infrastructure/OperatingDaySaveSections.cs";
        string operatingDaySaveSectionsSource =
            File.Exists(operatingDaySaveSectionsPath)
                ? File.ReadAllText(operatingDaySaveSectionsPath)
                : string.Empty;
        if (!eventAlertRuntimeSource.Contains(
                "aggregateRootStore.Replace(candidate.State)",
                StringComparison.Ordinal)
            || !eventAlertRuntimeSource.Contains(
                "throw new ArgumentNullException(nameof(records))",
                StringComparison.Ordinal)
            || !eventAlertRuntimeSource.Contains(
                "!aggregateRootStore.IsRestoreStaging",
                StringComparison.Ordinal)
            || eventAlertRuntimeSource.Contains(
                "records ?? Array.Empty<EventAlertRecordSnapshot>()",
                StringComparison.Ordinal)
            || eventAlertRuntimeSource.Contains(
                "eventLog.Clear()",
                StringComparison.Ordinal)
            || eventAlertRuntimeSource.Contains(
                "dismissedRecordIds.Clear()",
                StringComparison.Ordinal)
            || !operatingDaySaveSectionsSource.Contains(
                "EventAlertRestoreCandidate",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Event-alert restore must preflight detached state and delay every Unity UI projection until aggregate publication.");
        }

        const string settlementRuntimePath =
            "Assets/Scripts/Services/Operation/OperatingDaySettlementApplicationAdapter.cs";
        string settlementRuntimeSource = File.Exists(settlementRuntimePath)
            ? File.ReadAllText(settlementRuntimePath)
            : string.Empty;
        if (!settlementRuntimeSource.Contains(
                "throw new ArgumentNullException(nameof(employmentContracts))",
                StringComparison.Ordinal)
            || !settlementRuntimeSource.Contains(
                "throw new ArgumentNullException(nameof(moneyAccount))",
                StringComparison.Ordinal)
            || !settlementRuntimeSource.Contains(
                "throw new ArgumentNullException(nameof(paidFacilityContracts))",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "employmentContracts?.",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "moneyAccount?.",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "paidFacilityContracts?.",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "SettleLegacyOperatingCosts",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "TryGetSessionState",
                StringComparison.Ordinal)
            || settlementRuntimeSource.Contains(
                "economySettings?",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Operating-day settlement must require its economy collaborators instead of changing rules through null-dependent fallbacks.");
        }

        int settlementRestoreStart = settlementRuntimeSource.IndexOf(
            "public OperatingDaySettlementRestoreCandidate PrepareRestoreCandidate",
            StringComparison.Ordinal);
        int settlementRestoreEnd = settlementRestoreStart < 0
            ? -1
            : settlementRuntimeSource.IndexOf(
                "public void PublishRestoreCandidate",
                settlementRestoreStart,
                StringComparison.Ordinal);
        string settlementRestoreSource =
            settlementRestoreStart >= 0 && settlementRestoreEnd > settlementRestoreStart
                ? settlementRuntimeSource.Substring(
                    settlementRestoreStart,
                    settlementRestoreEnd - settlementRestoreStart)
                : string.Empty;
        if (!settlementRestoreSource.Contains(
                "return new OperatingDaySettlementRestoreCandidate(",
                StringComparison.Ordinal)
            || settlementRestoreSource.Contains(
                "ResetLedger()",
                StringComparison.Ordinal)
            || settlementRestoreSource.Contains(
                "reportHistory.Clear()",
                StringComparison.Ordinal)
            || !operatingDaySaveSectionsSource.Contains(
                "DungeonStrictJsonSaveSection<",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Operating-day restore must validate and replace a detached ledger Aggregate instead of clearing live runtime collections.");
        }
        if (!settlementRuntimeSource.Contains(
                "RequireAggregateRoot().Replace(candidate.State)",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Operating-day commit must be a single prepared Aggregate pointer swap.");
        }

        const string workOrderRuntimePath =
            "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs";
        string workOrderRuntimeSource = File.Exists(workOrderRuntimePath)
            ? File.ReadAllText(workOrderRuntimePath)
            : string.Empty;
        const string workOrderSavePath =
            "Assets/Scripts/Services/Character/Work/WorkOrdersSaveSection.cs";
        string workOrderSaveSource = File.Exists(workOrderSavePath)
            ? File.ReadAllText(workOrderSavePath)
            : string.Empty;
        int workOrderRestoreStart = workOrderRuntimeSource.IndexOf(
            "public WorkOrderRestoreCandidate PrepareRestoreCandidate(",
            StringComparison.Ordinal);
        int workOrderRestoreEnd = workOrderRestoreStart < 0
            ? -1
            : workOrderRuntimeSource.IndexOf(
                "public void PublishRestoreCandidate(WorkOrderRestoreCandidate candidate)",
                workOrderRestoreStart,
                StringComparison.Ordinal);
        string workOrderRestoreSource =
            workOrderRestoreStart >= 0
            && workOrderRestoreEnd > workOrderRestoreStart
                ? workOrderRuntimeSource.Substring(
                    workOrderRestoreStart,
                    workOrderRestoreEnd - workOrderRestoreStart)
                : string.Empty;
        if (!workOrderRuntimeSource.Contains(
                "stateStore.Replace(candidate.State)",
                StringComparison.Ordinal)
            || workOrderRestoreSource.Contains(
                "ClearRuntimeSites()",
                StringComparison.Ordinal)
            || workOrderRestoreSource.Contains(
                "ordersById.Clear()",
                StringComparison.Ordinal)
            || !workOrderSaveSource.Contains(
                "DungeonStrictJsonSaveSection<\n        DungeonWorkOrderSaveData,\n        WorkOrderRestoreCandidate>",
                StringComparison.Ordinal)
            || !workOrderSaveSource.Contains(
                "runtime.ValidateRestorePayload(payload)",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Work-order restore must preflight detached Aggregate state and defer construction-site replacement until participant publication.");
        }
        RequireSourceContract(
            errors,
            workOrderSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonWorkOrderSaveData,\n        WorkOrderRestoreCandidate>",
            "Work-order restore must publish detached construction sites without replaying a live rollback image.");
        RequireSourceContract(
            errors,
            workOrderRuntimePath,
            "Work-order publish requires the V18 save registry transaction boundary.",
            "Work-order restore must reject direct mutation outside the V18 transaction.");
        ForbidSourceContract(
            errors,
            workOrderRuntimePath,
            "public void Restore(DungeonWorkOrderSaveData",
            "Work-order runtime must not expose a direct live-state restore bypass.");
        RequireSourceContract(
            errors,
            workOrderRuntimePath,
            "site.RetireForWorldReplacement()",
            "Published construction-site replacement must retire live Unity objects synchronously.");
        RequireSourceContract(
            errors,
            workOrderRuntimePath,
            "A construction-site restore candidate is not detached and inactive.",
            "Construction-site publication must validate the complete detached candidate set before retiring live sites.");
        RequireSourceContract(
            errors,
            workOrderRuntimePath,
            "reservedWorkerPersistentId = string.Empty",
            "Work-order persistence must exclude transient scene worker reservations at capture time.");
        int workOrderFromSaveStart = workOrderRuntimeSource.IndexOf(
            "private static WorkOrderRecord FromSaveData",
            StringComparison.Ordinal);
        int workOrderFromSaveEnd = workOrderFromSaveStart < 0
            ? -1
            : workOrderRuntimeSource.IndexOf(
                "private static WorkOrderProgressState ToProgressState",
                workOrderFromSaveStart,
                StringComparison.Ordinal);
        string workOrderFromSaveSource =
            workOrderFromSaveStart >= 0
            && workOrderFromSaveEnd > workOrderFromSaveStart
                ? workOrderRuntimeSource.Substring(
                    workOrderFromSaveStart,
                    workOrderFromSaveEnd - workOrderFromSaveStart)
                : string.Empty;
        if (workOrderFromSaveSource.Length == 0
            || workOrderFromSaveSource.Contains(
                "order.status =",
                StringComparison.Ordinal)
            || workOrderFromSaveSource.Contains(
                "reservedWorkerPersistentId = string.Empty",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Work-order restore must not silently normalize saved status or worker reservation fields.");
        }
        ForbidSourceContract(
            errors,
            workOrderRuntimePath,
            "DestroyUnityObject(siteObject)",
            "Failed construction-site candidates must be destroyed synchronously.");
        const string workOrderValidationPath =
            "Assets/Scripts/Services/Character/Work/WorkOrderSaveValidation.cs";
        RequireSourceContract(
            errors,
            workOrderValidationPath,
            "$\"work:{sequence:D6}\"",
            "Work-order save validation must require canonical persistent order IDs.");
        RequireSourceContract(
            errors,
            workOrderValidationPath,
            "order.status == WorkOrderStatus.InProgress",
            "Work-order save validation must reject transient in-progress state instead of normalizing it.");
        RequireSourceContract(
            errors,
            workOrderValidationPath,
            "!string.IsNullOrEmpty(order.reservedWorkerPersistentId)",
            "Work-order save validation must reject transient worker reservations.");
        const string workAmountFixturePath =
            "Assets/Scripts/Services/Character/Work/Editor/WorkAmountDebugScenarios.cs";
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "lateProbe.PublishCount == 1",
            "Work-order late-failure coverage must prove one reversible publish attempt.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "lateProbe.PublishedStateWasReversible",
            "Work-order late-failure coverage must inspect the reversible published state before injecting failure.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "lateProbe.RollbackCount == 1",
            "Work-order late-failure coverage must prove the failing participant was rolled back exactly once.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "lateProbe.CompleteCount == 0",
            "Work-order late-failure coverage must prove completion was not run after rollback.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "CountDetachedConstructionSites() == detachedBefore",
            "Work-order late-failure coverage must prove synchronous detached-site cleanup.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "liveOrderPreserved",
            "Work-order late-failure coverage must prove rollback preserves the previous live order.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "!incomingLeaked",
            "Work-order late-failure coverage must prove rollback removes the published candidate site.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "rootStore.PublishedRestoreRevision == 0",
            "Work-order late-failure coverage must prove the staged aggregate root was not published.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "successProbe.PublishCount == 1",
            "Work-order success coverage must prove one reversible publish attempt.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "successProbe.PublishedStateWasReversible",
            "Work-order success coverage must inspect the reversible state before completion.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "successProbe.RollbackCount == 0",
            "Work-order success coverage must prove rollback was not run.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "successProbe.CompleteCount == 1",
            "Work-order success coverage must prove completion ran exactly once.");
        RequireSourceContract(
            errors,
            workAmountFixturePath,
            "rootStore.PublishedRestoreRevision == 1",
            "Work-order success coverage must prove the staged aggregate root was published once.");

        const string wildlifeRestorePath =
            "Assets/Scripts/Services/Wildlife/WildlifeRestoreRuntime.cs";
        string wildlifeRestoreSource = File.Exists(wildlifeRestorePath)
            ? File.ReadAllText(wildlifeRestorePath)
            : string.Empty;
        const string wildlifeRuntimePath =
            "Assets/Scripts/Services/Wildlife/WildlifeRuntime.cs";
        string wildlifeRuntimeSource = File.Exists(wildlifeRuntimePath)
            ? File.ReadAllText(wildlifeRuntimePath)
            : string.Empty;
        const string wildlifeSavePath =
            "Assets/Scripts/Services/Wildlife/WildlifeSaveSection.cs";
        string wildlifeSaveSource = File.Exists(wildlifeSavePath)
            ? File.ReadAllText(wildlifeSavePath)
            : string.Empty;
        const string wildlifeActorRestorePath =
            "Assets/Scripts/Models/Wildlife/Core/WildlifeActorRestoreLifecycle.cs";
        string wildlifeActorRestoreSource =
            File.Exists(wildlifeActorRestorePath)
                ? File.ReadAllText(wildlifeActorRestorePath)
                : string.Empty;
        RequireSourceContract(
            errors,
            wildlifeSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonWildlifeSaveData,\n        WildlifeRestoreCandidate>",
            "Wildlife restore must publish detached candidates without replaying a live rollback image.");
        RequireSourceContract(
            errors,
            wildlifeRestorePath,
            "Wildlife candidate publication requires one active V18 transaction.",
            "Wildlife restore must reject direct mutation outside the V18 restore transaction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.Restore.cs",
            "PrepareRestoreCandidate",
            "Wildlife ecosystem state must be constructed against the detached Grid before publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.Restore.cs",
            "PublishRestoreCandidate",
            "Wildlife ecosystem state must swap only at participant publication.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeCarcassService.cs",
            "ReplaceFreshnessValidated",
            "Wildlife carcass freshness must replace from a strictly validated exact candidate.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
            "DestroyImmediate(gameObject);",
            "Unpublished detached wildlife candidates must be removed synchronously on transaction discard.");
        RequireSourceContract(
            errors,
            wildlifeActorRestorePath,
            "host.Discard();",
            "The wildlife restore lifecycle must delegate detached candidate disposal to its host.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeModels.cs",
            "void Restore(DungeonWildlifeEcosystemSaveData",
            "Wildlife ecosystem contracts must not expose a direct live restore mutation path.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs",
            "pendingSaveData",
            "Wildlife ecosystem restore must not defer a save payload into default-generation initialization.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs",
            "ApplyPendingRespawns",
            "Wildlife ecosystem restore must not normalize staged respawn state during later initialization.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeCarcassService.cs",
            "RestoreFreshness(",
            "Wildlife carcass restore must not filter or clamp malformed save entries.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeRestoreRuntime.cs",
            "AddWarning(",
            "Wildlife restore must reject malformed state instead of warning and defaulting.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeSaveValidation.cs",
            "AddWarning(",
            "Wildlife save validation must reject every non-canonical value before candidate construction.");
        int wildlifeRestoreStart = wildlifeRuntimeSource.IndexOf(
            "public WildlifeRestoreCandidate BuildRestoreCandidate(",
            StringComparison.Ordinal);
        int wildlifeRestoreEnd = wildlifeRestoreStart < 0
            ? -1
            : wildlifeRuntimeSource.IndexOf(
                "public void BeginRestoreCandidate()",
                wildlifeRestoreStart,
                StringComparison.Ordinal);
        string wildlifeCommitSource =
            wildlifeRestoreStart >= 0
            && wildlifeRestoreEnd > wildlifeRestoreStart
                ? wildlifeRuntimeSource.Substring(
                    wildlifeRestoreStart,
                    wildlifeRestoreEnd - wildlifeRestoreStart)
                : string.Empty;
        if (!wildlifeRestoreSource.Contains(
                "250.world.wildlife",
                StringComparison.Ordinal)
            || !wildlifeRestoreSource.Contains(
                "TryPrepareActorCandidate",
                StringComparison.Ordinal)
            || !wildlifeRestoreSource.Contains(
                "actor.PublishDetachedRestore()",
                StringComparison.Ordinal)
            || wildlifeCommitSource.Contains(
                "DestroyPopulationActors(previous)",
                StringComparison.Ordinal)
            || wildlifeCommitSource.Contains(
                "ecosystemRuntime.Restore",
                StringComparison.Ordinal)
            || wildlifeCommitSource.Contains(
                "carcassService.RestoreFreshness",
                StringComparison.Ordinal)
            || !wildlifeActorRestoreSource.Contains(
                "class WildlifeActorRestoreLifecycle",
                StringComparison.Ordinal)
            || !wildlifeSaveSource.Contains(
                "DungeonStrictJsonSaveSection<",
                StringComparison.Ordinal)
            || !wildlifeSaveSource.Contains(
                "runtime.ValidateRestorePayload(payload)",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Wildlife restore must preflight and construct inactive actors on the detached grid, then defer all live population, ecosystem, and carcass replacement until participant publication.");
        }
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
            "public void PrepareForDetachedRestore() => RestoreLifecycle.Prepare();",
            "Wildlife actors must expose detached restore through their dedicated lifecycle collaborator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
            "partial class WildlifeActor",
            "Wildlife actor restore must not reintroduce partial-class ownership.");
        RequireSourceContract(
            errors,
            wildlifeRestorePath,
            "class WildlifeRestoreCoordinator",
            "Wildlife V18 restore must remain owned by its dedicated coordinator.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Wildlife/WildlifeWorldRuntime.cs",
            "class WildlifeWorldRuntime",
            "Wildlife actor and grid projection must remain owned by its world collaborator.");
        ForbidSourceContract(
            errors,
            wildlifeRuntimePath,
            "partial class WildlifeRuntime",
            "Wildlife runtime must remain a single non-partial facade.");
        ForbidSourceContract(
            errors,
            wildlifeRestorePath,
            "partial class WildlifeRuntime",
            "Wildlife restore coordination must not reintroduce partial runtime ownership.");

        const string exteriorRuntimePath =
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs";
        string exteriorRuntimeSource = File.Exists(exteriorRuntimePath)
            ? File.ReadAllText(exteriorRuntimePath)
            : string.Empty;
        const string exteriorRestorePath =
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRestoreCoordinator.cs";
        string exteriorRestoreSource = File.Exists(exteriorRestorePath)
            ? File.ReadAllText(exteriorRestorePath)
            : string.Empty;
        const string exteriorSavePath =
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivitySaveSection.cs";
        string exteriorSaveSource = File.Exists(exteriorSavePath)
            ? File.ReadAllText(exteriorSavePath)
            : string.Empty;
        const string exteriorDebugPath =
            "Assets/Scripts/Services/Infrastructure/Exterior/Editor/ExteriorActivityDebugScenarios.cs";
        const string exteriorValidationPath =
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivitySaveValidation.cs";
        string exteriorValidationSource = File.Exists(exteriorValidationPath)
            ? File.ReadAllText(exteriorValidationPath)
            : string.Empty;
        const string namedExteriorDomainPath =
            "Assets/Scripts/Models/Exterior/Core/ExteriorDomain.cs";
        const string namedExteriorRestorePath =
            "Assets/Scripts/Models/Exterior/Core/ExteriorRestoreDomain.cs";
        RequireSourceContract(
            errors,
            namedExteriorDomainPath,
            "BuildingInstanceId BuildingId",
            "Named exterior zones must retain typed building ownership.");
        RequireSourceContract(
            errors,
            namedExteriorDomainPath,
            "RoomId AdjacentRoomId",
            "Named exterior addresses must use the typed Rooms boundary.");
        RequireSourceContract(
            errors,
            namedExteriorDomainPath,
            "ExteriorHazardSnapshot",
            "Exterior activity rules must consume the typed Environment boundary.");
        ForbidSourceContract(
            errors,
            namedExteriorDomainPath,
            "UnityEngine",
            "Named exterior rules must remain independent of Unity scene types.");
        RequireSourceContract(
            errors,
            namedExteriorRestorePath,
            "public static class ExteriorActivityRestoreRules",
            "Exterior V18 preflight must have a named structural authority.");
        RequireSourceContract(
            errors,
            namedExteriorRestorePath,
            "public void Commit(ExteriorActivityRestoreCandidate candidate)",
            "Named exterior state must publish only a validated detached candidate.");
        ForbidSourceContract(
            errors,
            namedExteriorRestorePath,
            "UnityEngine",
            "Named exterior restore state must remain engine-free.");
        RequireSourceContract(
            errors,
            exteriorSavePath,
            "DungeonStrictJsonSaveSection<\n        DungeonExteriorActivitySaveData,\n        ExteriorActivityWorldRestoreCandidate>",
            "Exterior activity restore must discard detached zone candidates without replaying a live rollback image.");
        RequireSourceContract(
            errors,
            exteriorRestorePath,
            "Exterior candidate publication requires one active V18 transaction.",
            "Exterior activity restore must reject direct mutation outside the V18 transaction.");
        RequireSourceContract(
            errors,
            exteriorValidationPath,
            "rawZoneId",
            "Exterior activity IDs must be canonical before exact candidate construction.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.cs",
            "DestroyImmediate(gameObject)",
            "Detached and retired building projections must be removed synchronously during world replacement.");
        RequireSourceContract(
            errors,
            exteriorDebugPath,
            "lateParticipant.RollbackCount == 1",
            "Exterior late-failure proof must observe the reversible publication rollback.");
        RequireSourceContract(
            errors,
            exteriorDebugPath,
            "GetPrivateField<object>(exteriorRuntime, \"zones\")",
            "Exterior late-failure proof must restore the exact previous zone root.");
        RequireSourceContract(
            errors,
            exteriorDebugPath,
            "lateParticipant.ObservedOldActiveAndCandidateHidden",
            "Exterior late-failure proof must keep old zones active and candidates hidden before completion.");
        RequireSourceContract(
            errors,
            exteriorDebugPath,
            "lateParticipant.CompleteCount == 1",
            "Exterior success proof must complete the reversible publication exactly once.");
        RequireSourceContract(
            errors,
            exteriorDebugPath,
            "CountDetachedExteriorCandidates",
            "Exterior late-failure proof must detect leaked detached zone projections.");
        ForbidSourceContract(
            errors,
            exteriorRestorePath,
            ".OrderBy(zone => zone.zoneType)",
            "Exterior candidate construction must preserve validated payload order for exact round trips.");
        ForbidSourceContract(
            errors,
            exteriorRestorePath,
            "UnityEngine.Object.Destroy(zoneObject)",
            "Failed exterior candidates must not survive until a later PlayMode frame.");
        ForbidSourceContract(
            errors,
            exteriorRestorePath,
            "AddWarning(",
            "Exterior restore must reject malformed state instead of warning and defaulting.");
        ForbidSourceContract(
            errors,
            exteriorValidationPath,
            "AddWarning(",
            "Exterior validation must reject every non-canonical value before candidate construction.");
        int exteriorPrepareStart = exteriorRestoreSource.IndexOf(
            "public ExteriorActivityWorldRestoreCandidate Build(",
            StringComparison.Ordinal);
        int exteriorPublishStart = exteriorPrepareStart < 0
            ? -1
            : exteriorRestoreSource.IndexOf(
                "public ExteriorActivityWorldRestoreCandidate Publish()",
                exteriorPrepareStart,
                StringComparison.Ordinal);
        string exteriorPrepareSource =
            exteriorPrepareStart >= 0
            && exteriorPublishStart > exteriorPrepareStart
                ? exteriorRestoreSource.Substring(
                    exteriorPrepareStart,
                    exteriorPublishStart - exteriorPrepareStart)
                : string.Empty;
        int exteriorParticipantPublishStart = exteriorRuntimeSource.IndexOf(
            "public void PublishRestoreCandidate()",
            StringComparison.Ordinal);
        int exteriorRollbackStart = exteriorParticipantPublishStart < 0
            ? -1
            : exteriorRuntimeSource.IndexOf(
                "public void RollbackPublishedRestoreCandidate()",
                exteriorParticipantPublishStart,
                StringComparison.Ordinal);
        int exteriorCompleteStart = exteriorRollbackStart < 0
            ? -1
            : exteriorRuntimeSource.IndexOf(
                "public void CompleteRestoreCandidate()",
                exteriorRollbackStart,
                StringComparison.Ordinal);
        int exteriorDiscardStart = exteriorCompleteStart < 0
            ? -1
            : exteriorRuntimeSource.IndexOf(
                "public void DiscardRestoreCandidate()",
                exteriorCompleteStart,
                StringComparison.Ordinal);
        string exteriorParticipantPublishSource =
            exteriorParticipantPublishStart >= 0
            && exteriorRollbackStart > exteriorParticipantPublishStart
                ? exteriorRuntimeSource.Substring(
                    exteriorParticipantPublishStart,
                    exteriorRollbackStart - exteriorParticipantPublishStart)
                : string.Empty;
        string exteriorRollbackSource =
            exteriorRollbackStart >= 0
            && exteriorCompleteStart > exteriorRollbackStart
                ? exteriorRuntimeSource.Substring(
                    exteriorRollbackStart,
                    exteriorCompleteStart - exteriorRollbackStart)
                : string.Empty;
        string exteriorCompleteSource =
            exteriorCompleteStart >= 0
            && exteriorDiscardStart > exteriorCompleteStart
                ? exteriorRuntimeSource.Substring(
                    exteriorCompleteStart,
                    exteriorDiscardStart - exteriorCompleteStart)
                : string.Empty;
        int exteriorDeactivateOld = exteriorCompleteSource.IndexOf(
            "oldZone.gameObject.SetActive(false);",
            StringComparison.Ordinal);
        int exteriorPublishDetached = exteriorCompleteSource.IndexOf(
            "zone.PublishDetachedRestore();",
            StringComparison.Ordinal);
        int exteriorActivateCandidate = exteriorCompleteSource.IndexOf(
            "zone.gameObject.SetActive(true);",
            StringComparison.Ordinal);
        int exteriorRetireOld = exteriorCompleteSource.IndexOf(
            "oldZone.RetireForWorldReplacement();",
            StringComparison.Ordinal);
        int exteriorCompletePublication = exteriorCompleteSource.IndexOf(
            "restoreCoordinator.CompletePublished();",
            StringComparison.Ordinal);
        if (!exteriorRuntimeSource.Contains(
                "300.world.exterior-zones",
                StringComparison.Ordinal)
            || !exteriorRuntimeSource.Contains(
                "public const int CurrentVersion = 3;",
                StringComparison.Ordinal)
            || !exteriorRuntimeSource.Contains(
                "restoreCoordinator.Build(saveData)",
                StringComparison.Ordinal)
            || !exteriorValidationSource.Contains(
                "payload.version != DungeonExteriorActivitySaveData.CurrentVersion",
                StringComparison.Ordinal)
            || !exteriorRestoreSource.Contains(
                "marker.PrepareForDetachedRestore()",
                StringComparison.Ordinal)
            || !exteriorPrepareSource.Contains(
                "world.RestoreCandidates.TryGetGrid(out Grid grid)",
                StringComparison.Ordinal)
            || !exteriorRestoreSource.Contains(
                "zoneObject.SetActive(false)",
                StringComparison.Ordinal)
            || !exteriorRestoreSource.Contains(
                "SetExteriorZoneCandidate",
                StringComparison.Ordinal)
            || !exteriorParticipantPublishSource.Contains(
                "activePublication = publication;",
                StringComparison.Ordinal)
            || !exteriorParticipantPublishSource.Contains(
                "zones = candidate.Zones;",
                StringComparison.Ordinal)
            || exteriorParticipantPublishSource.Contains(
                "PublishDetachedRestore()",
                StringComparison.Ordinal)
            || exteriorParticipantPublishSource.Contains(
                "RetireForWorldReplacement()",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "zones = publication.PreviousZones;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "zonesView = publication.PreviousZonesView;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "incidentAggregate = publication.PreviousIncidents;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "incidentSequence = publication.PreviousIncidentSequence;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "nextConditionTick = publication.PreviousConditionTick;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "nextIncidentCheck = publication.PreviousIncidentCheck;",
                StringComparison.Ordinal)
            || !exteriorRollbackSource.Contains(
                "restoreCoordinator.RollbackPublished();",
                StringComparison.Ordinal)
            || exteriorDeactivateOld < 0
            || exteriorPublishDetached <= exteriorDeactivateOld
            || exteriorActivateCandidate <= exteriorPublishDetached
            || exteriorRetireOld <= exteriorActivateCandidate
            || exteriorCompletePublication <= exteriorRetireOld
            || exteriorPrepareSource.Contains(
                "liveZones.Clear()",
                StringComparison.Ordinal)
            || exteriorPrepareSource.Contains(
                "DestroySelf()",
                StringComparison.Ordinal)
            || exteriorRuntimeSource.Contains(
                "public List<ExteriorIncidentSaveData> incidents",
                StringComparison.Ordinal)
            || !exteriorSaveSource.Contains(
                "DungeonStrictJsonSaveSection<",
                StringComparison.Ordinal)
            || !exteriorSaveSource.Contains(
                "runtime.ValidateRestorePayload(payload)",
                StringComparison.Ordinal)
            || !File.ReadAllText(
                    "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs")
                .Contains(
                    "building is not ExteriorZoneMarker",
                    StringComparison.Ordinal))
        {
            errors.Add(
                "Exterior activity restore must preflight strict V3 state, construct inactive markers on the detached grid, publish only reversible roots, restore exact roots on rollback, and retire previous zones only during completion.");
        }

        const string returnArrivalRuntimePath =
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs";
        string returnArrivalRuntimeSource =
            File.Exists(returnArrivalRuntimePath)
                ? File.ReadAllText(returnArrivalRuntimePath)
                : string.Empty;
        const string returnArrivalStatePath =
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalAggregateState.cs";
        string returnArrivalStateSource =
            File.Exists(returnArrivalStatePath)
                ? File.ReadAllText(returnArrivalStatePath)
                : string.Empty;
        int returnArrivalRestoreStart =
            returnArrivalRuntimeSource.IndexOf(
                "public void Restore(",
                StringComparison.Ordinal);
        int returnArrivalRestoreEnd = returnArrivalRestoreStart < 0
            ? -1
            : returnArrivalRuntimeSource.IndexOf(
                "private void MaterializeReadyArrivals()",
                returnArrivalRestoreStart,
                StringComparison.Ordinal);
        string returnArrivalRestoreSource =
            returnArrivalRestoreStart >= 0
            && returnArrivalRestoreEnd > returnArrivalRestoreStart
                ? returnArrivalRuntimeSource.Substring(
                    returnArrivalRestoreStart,
                    returnArrivalRestoreEnd - returnArrivalRestoreStart)
                : string.Empty;
        if (!returnArrivalRuntimeSource.Contains(
                "DungeonRuntimeAggregateRootStore",
                StringComparison.Ordinal)
            || !returnArrivalRuntimeSource.Contains(
                "OffenseReturnArrivalSaveValidation.CreateStrictState",
                StringComparison.Ordinal)
            || !returnArrivalRuntimeSource.Contains(
                "OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate",
                StringComparison.Ordinal)
            || !returnArrivalRuntimeSource.Contains(
                "void PublishRestoreCandidate(",
                StringComparison.Ordinal)
            || returnArrivalRestoreSource.Contains(
                "arrivals.Clear()",
                StringComparison.Ordinal)
            || returnArrivalRestoreSource.Contains(
                "MaterializeReadyArrivals()",
                StringComparison.Ordinal)
            || !returnArrivalStateSource.Contains(
                "MaximumArrivals",
                StringComparison.Ordinal)
            || !returnArrivalStateSource.Contains(
                "next sequence",
                StringComparison.Ordinal)
            || !File.ReadAllText(
                    "Assets/Scripts/Services/Offense/OffenseSaveSections.cs")
                .Contains(
                    "returnArrivals.BuildRestoreCandidate",
                    StringComparison.Ordinal)
            || !File.ReadAllText(
                    "Assets/Scripts/Services/Offense/OffenseSaveSections.cs")
                .Contains(
                    "returnArrivals.PublishRestoreCandidate",
                    StringComparison.Ordinal))
        {
            errors.Add(
                "Offense return-arrival restore must validate strict detached state, replace one Aggregate slot, and defer prisoner or wildlife materialization until normal post-publication ticking.");
        }

        const string medicalRuntimePath =
            "Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs";
        string medicalRuntimeSource = File.Exists(medicalRuntimePath)
            ? File.ReadAllText(medicalRuntimePath)
            : string.Empty;
        const string medicalRestorePath =
            "Assets/Scripts/Services/Combat/CharacterMedicalRestoreRuntime.cs";
        string medicalRestoreSource = File.Exists(medicalRestorePath)
            ? File.ReadAllText(medicalRestorePath)
            : string.Empty;
        const string medicalSavePath =
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs";
        string medicalSaveSource = File.Exists(medicalSavePath)
            ? File.ReadAllText(medicalSavePath)
            : string.Empty;
        const string medicalModelsPath =
            "Assets/Scripts/Services/Combat/CharacterMedicalModels.cs";
        string medicalModelsSource = File.Exists(medicalModelsPath)
            ? File.ReadAllText(medicalModelsPath)
            : string.Empty;
        const string medicalValidationPath =
            "Assets/Scripts/Services/Combat/CharacterMedicalSaveValidation.cs";
        string medicalValidationSource = File.Exists(medicalValidationPath)
            ? File.ReadAllText(medicalValidationPath)
            : string.Empty;
        int medicalProjectionStart = medicalRestoreSource.IndexOf(
            "private void ValidateWorldReferencesAndPrepareProjection",
            StringComparison.Ordinal);
        int medicalProjectionEnd = medicalProjectionStart < 0
            ? -1
            : medicalRestoreSource.IndexOf(
                "private CharacterActor FindCharacter",
                medicalProjectionStart,
                StringComparison.Ordinal);
        string medicalProjectionSource = medicalProjectionStart >= 0
            && medicalProjectionEnd > medicalProjectionStart
                ? medicalRestoreSource.Substring(
                    medicalProjectionStart,
                    medicalProjectionEnd - medicalProjectionStart)
                : string.Empty;
        int medicalRestoreStart = medicalRestoreSource.IndexOf(
            "internal void PublishRestore(",
            StringComparison.Ordinal);
        int medicalRestoreEnd = medicalRestoreStart < 0
            ? -1
            : medicalRestoreSource.IndexOf(
                "internal void BeginRestoreCandidate()",
                medicalRestoreStart,
                StringComparison.Ordinal);
        string medicalCommitSource =
            medicalRestoreStart >= 0
            && medicalRestoreEnd > medicalRestoreStart
                ? medicalRestoreSource.Substring(
                    medicalRestoreStart,
                    medicalRestoreEnd - medicalRestoreStart)
                : string.Empty;
        if (!medicalRuntimeSource.Contains(
                "DungeonRuntimeAggregateRootStore",
                StringComparison.Ordinal)
            || !medicalRestoreSource.Contains(
                "350.world.medical",
                StringComparison.Ordinal)
            || !medicalRestoreSource.Contains(
                "CharacterMedicalSaveValidation.CreateState",
                StringComparison.Ordinal)
            || !medicalRestoreSource.Contains(
                "TryPrepareDownedRegistration",
                StringComparison.Ordinal)
            || !medicalCommitSource.Contains(
                "aggregateRootStore.Replace(candidate.State);",
                StringComparison.Ordinal)
            || medicalCommitSource.Contains(
                "orders.Clear()",
                StringComparison.Ordinal)
            || medicalCommitSource.Contains(
                "RegisterOccupant(",
                StringComparison.Ordinal)
            || medicalCommitSource.Contains(
                "RemoveDownedOccupant(",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "DungeonStrictJsonSaveSection<",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "persistence.PrepareRestore(payload)",
                StringComparison.Ordinal)
            || !medicalModelsSource.Contains(
                "CharacterMedicalStatusCode statusCode",
                StringComparison.Ordinal)
            || medicalModelsSource.Contains(
                "public string status",
                StringComparison.Ordinal)
            || medicalModelsSource.Contains(
                "out string failureReason",
                StringComparison.Ordinal)
            || !medicalValidationSource.Contains(
                "payload.version != DungeonCharacterMedicalSaveData.CurrentVersion",
                StringComparison.Ordinal)
            || !medicalValidationSource.Contains(
                "order.statusCode == CharacterMedicalStatusCode.Unknown",
                StringComparison.Ordinal)
            || medicalProjectionSource.Contains(
                "patient.SetLifecycleState(CharacterLifecycleState.Downed);",
                StringComparison.Ordinal)
            || !File.ReadAllText(
                    "Assets/Scripts/Services/Infrastructure/Registration/DungeonCombatRegistration.cs")
                .Contains(
                    ".As<IDungeonRestoreTransactionParticipant>()",
                    StringComparison.Ordinal))
        {
            errors.Add(
                "Character-medical restore must preflight strict order state, replace one Aggregate slot, and defer downed Grid projection replacement until participant publication.");
        }

        const string commandRuntimePath =
            "Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs";
        string commandRuntimeSource = File.Exists(commandRuntimePath)
            ? File.ReadAllText(commandRuntimePath)
            : string.Empty;
        const string commandRestorePath =
            "Assets/Scripts/Services/Combat/CharacterCombatCommandRestoreCoordinator.cs";
        string commandRestoreSource = File.Exists(commandRestorePath)
            ? File.ReadAllText(commandRestorePath)
            : string.Empty;
        const string commandPersistencePath =
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandPersistence.cs";
        string commandPersistenceSource = File.Exists(commandPersistencePath)
            ? File.ReadAllText(commandPersistencePath)
            : string.Empty;
        if (!commandRuntimeSource.Contains(
                "CharacterCombatCommandAggregateState",
                StringComparison.Ordinal)
            || !commandRuntimeSource.Contains(
                "CharacterCombatCommandCombatServices combat",
                StringComparison.Ordinal)
            || !commandRuntimeSource.Contains(
                "CharacterCombatCommandWorldServices world",
                StringComparison.Ordinal)
            || commandRuntimeSource.Contains(
                "new CombatAttackPositionPlanner(",
                StringComparison.Ordinal)
            || !commandRestoreSource.Contains(
                "400.world.combat-command-stances",
                StringComparison.Ordinal)
            || !commandRestoreSource.Contains(
                "aggregateRootStore.Replace(",
                StringComparison.Ordinal)
            || commandRestoreSource.Contains(
                "ReleaseCombatStance(",
                StringComparison.Ordinal)
            || commandPersistenceSource.Contains(
                "public static void Restore(",
                StringComparison.Ordinal)
            || !commandPersistenceSource.Contains(
                "commandSequence = state.CommandSequence",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "CharacterCombatCommandRestoreCandidate",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "runtime.PrepareRestore(payload)",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Character combat-command restore must preserve command sequence/revisions in one Aggregate, validate detached world references, and publish AI stance only at participant order 400.");
        }

        const string defenseTacticalPath =
            "Assets/Scripts/Models/Combat/Core/DefenseTacticalCoordinator.cs";
        string defenseTacticalSource = File.Exists(defenseTacticalPath)
            ? File.ReadAllText(defenseTacticalPath)
            : string.Empty;
        int defenseRestoreStart = defenseTacticalSource.IndexOf(
            "public void PublishRestore(",
            StringComparison.Ordinal);
        int defenseRestoreEnd = defenseRestoreStart < 0
            ? -1
            : defenseTacticalSource.Length;
        string defenseRestoreSource =
            defenseRestoreStart >= 0 && defenseRestoreEnd > defenseRestoreStart
                ? defenseTacticalSource.Substring(
                    defenseRestoreStart,
                    defenseRestoreEnd - defenseRestoreStart)
                : string.Empty;
        if (!defenseTacticalSource.Contains(
                "DefenseTacticalAggregateState",
                StringComparison.Ordinal)
            || !defenseRestoreSource.Contains(
                "aggregateRootStore.Replace(",
                StringComparison.Ordinal)
            || defenseRestoreSource.Contains(
                "byActor.Clear()",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "DefenseTacticalRestoreCandidate",
                StringComparison.Ordinal)
            || !File.ReadAllText(
                    "Assets/Scripts/Models/Combat/Core/DefenseTacticalSaveValidation.cs")
                .Contains(
                    "payload.sequence < highestSequence",
                    StringComparison.Ordinal))
        {
            errors.Add(
                "Defense-tactical restore must preserve its reservation sequence, validate the detached combat world, and replace one Aggregate slot without clearing live reservations.");
        }

        const string maintenancePath =
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs";
        string maintenanceSource = File.Exists(maintenancePath)
            ? File.ReadAllText(maintenancePath)
            : string.Empty;
        int maintenanceRestoreStart = maintenanceSource.IndexOf(
            "public void PublishRestore(",
            StringComparison.Ordinal);
        int maintenanceRestoreEnd = maintenanceRestoreStart < 0
            ? -1
            : maintenanceSource.IndexOf(
                "private void CreateAutomaticOrders",
                maintenanceRestoreStart,
                StringComparison.Ordinal);
        string maintenanceRestoreSource =
            maintenanceRestoreStart >= 0
            && maintenanceRestoreEnd > maintenanceRestoreStart
                ? maintenanceSource.Substring(
                    maintenanceRestoreStart,
                    maintenanceRestoreEnd - maintenanceRestoreStart)
                : string.Empty;
        string maintenanceValidationSource = File.ReadAllText(
            "Assets/Scripts/Services/Combat/EquipmentMaintenanceSaveValidation.cs");
        if (!maintenanceSource.Contains(
                "EquipmentMaintenanceAggregateState",
                StringComparison.Ordinal)
            || !maintenanceSource.Contains(
                "EquipmentMaintenanceItemServices itemServices",
                StringComparison.Ordinal)
            || !maintenanceRestoreSource.Contains(
                "aggregateRootStore.Replace(",
                StringComparison.Ordinal)
            || maintenanceRestoreSource.Contains(
                "policies.Clear()",
                StringComparison.Ordinal)
            || maintenanceRestoreSource.Contains(
                "warnings",
                StringComparison.Ordinal)
            || maintenanceSource.Contains(
                "requiredGeneralMaterials",
                StringComparison.Ordinal)
            || maintenanceSource.Contains(
                "facilityDestinationId",
                StringComparison.Ordinal)
            || maintenanceSource.Contains(
                "public int facilityX",
                StringComparison.Ordinal)
            || !medicalSaveSource.Contains(
                "EquipmentMaintenanceRestoreCandidate",
                StringComparison.Ordinal)
            || !maintenanceValidationSource.Contains(
                "payload.policySequence < highestSequence",
                StringComparison.Ordinal)
            || !maintenanceValidationSource.Contains(
                "payload.orderSequence < highestSequence",
                StringComparison.Ordinal)
            || !maintenanceValidationSource.Contains(
                "BuildingInstanceId",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Equipment-maintenance restore must preserve both sequences, validate authored material and detached entity references, and replace one Aggregate without coordinate or quantity aliases.");
        }

        string industrialDirectory =
            "Assets/Scripts/Services/Infrastructure/Industrial";
        RequireSourceContract(
            errors,
            Path.Combine(industrialDirectory, "ConveyorPersistence.cs"),
            "ConveyorPersistenceAdapter :",
            "Conveyor persistence must remain a dedicated Aggregate adapter.");
        ForbidSourceContract(
            errors,
            Path.Combine(industrialDirectory, "ConveyorRuntime.cs"),
            "IConveyorInfrastructurePersistence,",
            "Conveyor execution runtime must not own the save-section adapter facet.");
        string[] industrialRuntimeFiles =
        {
            "ElectricalNetworkRuntime.cs",
            "FluidNetworkRuntime.cs",
            "ConveyorRuntime.cs",
            "AutomationRuntime.cs"
        };
        foreach (string fileName in industrialRuntimeFiles)
        {
            string path = Path.Combine(industrialDirectory, fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string source = string.Join(
                Environment.NewLine,
                Directory.GetFiles(industrialDirectory, $"{stem}*.cs")
                    .OrderBy(candidate => candidate, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            if (!source.Contains(
                    "DungeonRuntimeAggregateRootStore",
                    StringComparison.Ordinal)
                || !source.Contains(
                    "PublishedRestoreRevision",
                    StringComparison.Ordinal)
                || source.Contains("states.Clear()", StringComparison.Ordinal)
                || source.Contains("nodeStates.Clear()", StringComparison.Ordinal)
                || source.Contains("payloads.Clear()", StringComparison.Ordinal)
                || source.Contains("powerDemand.Clear()", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Industrial runtime {fileName} must replace Aggregate state and defer projections until publication.");
            }
        }

        const string automationDemandPath =
            "Assets/Scripts/Services/Infrastructure/Core/Industrial/AutomationPowerDemandRegistry.cs";
        const string automationStatePath =
            "Assets/Scripts/Models/Automation/Core/AutomationCoreModels.cs";
        string automationDemandSource = File.Exists(automationDemandPath)
            ? File.ReadAllText(automationDemandPath)
            : string.Empty;
        string automationStateSource = File.Exists(automationStatePath)
            ? File.ReadAllText(automationStatePath)
            : string.Empty;
        if (!automationStateSource.Contains(
                "AutomationAggregateState",
                StringComparison.Ordinal)
            || !automationStateSource.Contains(
                "AutomationStateSession",
                StringComparison.Ordinal)
            || !automationStateSource.Contains(
                "GetOrCreateWritable",
                StringComparison.Ordinal)
            || !automationDemandSource.Contains(
                "AutomationStateSession",
                StringComparison.Ordinal)
            || automationDemandSource.Contains(
                "Dictionary<string, AutomationMode>",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Automation power demand must be a projection of the shared automation Aggregate state.");
        }
    }

    private static void RequireSourceContract(
        ICollection<string> errors,
        string path,
        string requiredToken,
        string message)
    {
        if (!File.Exists(path)
            || !File.ReadAllText(path).Contains(
                requiredToken,
                StringComparison.Ordinal))
        {
            errors.Add(message);
        }
    }

    private static void RequireOnlySourceDeclaration(
        ICollection<string> errors,
        string typeName,
        string expectedPath,
        string message)
    {
        string normalizedExpectedPath = expectedPath.Replace('\\', '/');
        Regex declaration = new(
            $@"^[\t ]*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|new)\s+)*(?:class|struct)\s+{Regex.Escape(typeName)}\b",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);
        string[] declarationPaths = Directory
            .GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
            .Where(path => declaration.IsMatch(File.ReadAllText(path)))
            .Select(path => path.Replace('\\', '/'))
            .ToArray();
        if (declarationPaths.Length != 1
            || !string.Equals(
                declarationPaths[0],
                normalizedExpectedPath,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{message} Found: "
                + (declarationPaths.Length == 0
                    ? "none"
                    : string.Join(", ", declarationPaths)));
        }
    }

    private static void RequireSourceFileAbsent(
        ICollection<string> errors,
        string path,
        string message)
    {
        if (File.Exists(path))
        {
            errors.Add(message);
        }
    }

    private static void ForbidSourceContract(
        ICollection<string> errors,
        string path,
        string forbiddenToken,
        string message)
    {
        if (!File.Exists(path)
            || File.ReadAllText(path).Contains(
                forbiddenToken,
                StringComparison.Ordinal))
        {
            errors.Add(message);
        }
    }

    private static void ForbidSourceInvocationAcrossScripts(
        ICollection<string> errors,
        string typeName,
        string methodName,
        string message)
    {
        const string validatorPath =
            "Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs";
        string invocationPattern =
            $@"\b{Regex.Escape(typeName)}\s*\.\s*{Regex.Escape(methodName)}\s*\(";
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (string.Equals(path, validatorPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(path), invocationPattern))
            {
                errors.Add($"{message} Found: {path}");
            }
        }
    }

    private static void ValidateAuthoredGameplayContent(
        GameContentCatalogSO root,
        ICollection<string> errors)
    {
        GameDomainContentCatalogSO domain = root?.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault();
        if (domain == null)
        {
            errors.Add("The root catalog has no unique domain-content SO.");
            return;
        }

        foreach (string error in domain.ValidateCatalog())
        {
            errors.Add(error);
        }

        CoreSessionRulesSO coreRules = domain.CoreSessionRules;
        if (coreRules == null)
        {
            errors.Add(
                "The root domain catalog has no authored core-session rules.");
        }
        else
        {
            foreach (string error in coreRules.ValidateDefinition())
            {
                errors.Add($"Core-session rules: {error}");
            }

            if (!domain.Definitions.Contains(coreRules))
            {
                errors.Add(
                    "Core-session rules are not indexed by the root domain catalog.");
            }
        }

        if (domain.MetaUpgrades.Count != 9)
        {
            errors.Add($"Expected 9 authored meta upgrades, found {domain.MetaUpgrades.Count}.");
        }

        if (domain.RunVariables.Count != 14)
        {
            errors.Add($"Expected 14 authored run variables, found {domain.RunVariables.Count}.");
        }

        if (domain.OwnerDoctrines.Count != 3)
        {
            errors.Add($"Expected 3 authored owner doctrines, found {domain.OwnerDoctrines.Count}.");
        }

        if (domain.InvasionPatterns.Count != 6)
        {
            errors.Add($"Expected 6 authored invasion patterns, found {domain.InvasionPatterns.Count}.");
        }

        if (domain.CharacterNeeds.Count != 6)
        {
            errors.Add($"Expected 6 authored character needs, found {domain.CharacterNeeds.Count}.");
        }

        if (domain.StockCategories.Count != 11)
        {
            errors.Add($"Expected 11 authored stock categories, found {domain.StockCategories.Count}.");
        }

        if (domain.BuildingCategories.Count != 8)
        {
            errors.Add($"Expected 8 authored building categories, found {domain.BuildingCategories.Count}.");
        }

        string[] requiredIds =
        {
            MetaUpgradeIds.StartingFacilityCandidatePlusOne,
            MetaUpgradeIds.ArcaneResearchMethod,
            RunVariableIds.SlimeCrowdVisit,
            RunVariableIds.ArmedIntruder,
            OwnerDoctrineIds.SlimeStewardship,
            OwnerDoctrineIds.VampireForbiddenStudy,
            InvasionIntruderPatternIds.Hunter,
            InvasionIntruderPatternIds.Executioner,
            "need:hunger",
            "need:hygiene",
            "stock:food",
            "stock:blueprint",
            "category:none",
            "category:resource"
        };
        HashSet<string> authoredIds = new(
            domain.MetaUpgrades.Select(record => record.id)
                .Concat(domain.RunVariables.Select(record => record.id))
                .Concat(domain.OwnerDoctrines.Select(record => record.id))
                .Concat(domain.InvasionPatterns.Select(record => record.id))
                .Concat(domain.CharacterNeeds.Select(record => record.id))
                .Concat(domain.StockCategories.Select(record => record.id))
                .Concat(domain.BuildingCategories.Select(record => record.id)),
            StringComparer.Ordinal);
        foreach (string id in requiredIds.Where(id => !authoredIds.Contains(id)))
        {
            errors.Add($"Required authored gameplay definition '{id}' is missing.");
        }

        try
        {
            AuthoredGameplayCatalog catalog = new(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            _ = ((IMetaUpgradeDefinitionCatalog)catalog)
                .Require(MetaUpgradeIds.StartingFacilityCandidatePlusOne);
            _ = ((IRunVariableDefinitionCatalog)catalog)
                .Require(RunVariableIds.SlimeCrowdVisit);
            _ = ((IOwnerDoctrineDefinitionCatalog)catalog)
                .Require(OwnerDoctrineIds.SlimeStewardship);
            _ = ((IInvasionIntruderPatternDefinitionCatalog)catalog)
                .Require(InvasionIntruderPatternIds.Hunter);
            _ = ((ICharacterNeedDefinitionCatalog)catalog)
                .Require(CharacterCondition.HUNGER);
            _ = ((IStockCategoryDefinitionCatalog)catalog)
                .Require(StockCategory.Food);
            _ = ((IBuildingCategoryDefinitionCatalog)catalog)
                .Require(BuildingCategory.Production);
        }
        catch (Exception exception)
        {
            errors.Add($"Authored gameplay catalog projection failed: {exception.Message}");
        }
    }

    private static void ValidateFixedTaxonomyAuthority(ICollection<string> errors)
    {
        if (CharacterStatCatalog.All.Count != 12)
        {
            errors.Add($"Expected 12 fixed character-stat protocols, found {CharacterStatCatalog.All.Count}.");
        }

        if (WorkTypeCatalog.All.Count != 30)
        {
            errors.Add($"Expected 30 fixed work-type protocols, found {WorkTypeCatalog.All.Count}.");
        }

        if (FacilityRoleCatalog.All.Count != 13)
        {
            errors.Add($"Expected 13 fixed facility-role protocols, found {FacilityRoleCatalog.All.Count}.");
        }

        string[] paths =
        {
            "Assets/Scripts/Models/Characters/CharacterStatCatalog.cs",
            "Assets/Scripts/Models/Work/WorkTypeCatalog.cs",
            "Assets/Scripts/Models/Rooms/Core/RoomRole.cs"
        };
        foreach (string path in paths)
        {
            string source = File.ReadAllText(path);
            if (Regex.IsMatch(source, @"public\s+static\s+[^\r\n]*(?:Register|ResetToBuiltIns)\s*\("))
            {
                errors.Add($"Fixed taxonomy source '{path}' exposes a global mutation API.");
            }

            if (source.Contains("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal))
            {
                errors.Add($"Fixed taxonomy source '{path}' still owns runtime reset state.");
            }
        }
    }

    private static void ValidateSpeciesCombatPayloadAuthority(
        ICollection<string> errors)
    {
        const string combatAssemblyName = "DungeonStory.Combat";
        Type[] ownedTypes =
        {
            typeof(OffenseFormationMask),
            typeof(OffenseBattleTargetRule),
            typeof(CharacterCombatAbilityCollection),
            typeof(CharacterCombatAbilityDefinition),
            typeof(OffenseCombatEffectModule),
            typeof(OffenseDamageEffect),
            typeof(OffenseHealEffect),
            typeof(OffenseGuardEffect),
            typeof(OffenseDamageOverTimeEffect),
            typeof(OffenseVulnerabilityEffect),
            typeof(OffenseDelayEffect),
            typeof(OffenseAttackModifierEffect),
            typeof(OffenseCleanseEffect),
            typeof(OffenseRepositionEffect),
            typeof(OffenseConditionalAmplifyEffect),
            typeof(OffenseCooldownAdjustEffect),
            typeof(OffenseMultiTargetEffect)
        };
        foreach (Type type in ownedTypes)
        {
            if (!string.Equals(
                    type.Assembly.GetName().Name,
                    combatAssemblyName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Species combat payload {type.Name} must belong to {combatAssemblyName}.");
            }

            if (type.GetConstructors().Any(constructor =>
                    constructor.GetParameters().Length > 8))
            {
                errors.Add(
                    $"Species combat payload {type.Name} introduced a large constructor.");
            }

            if (type.GetFields(
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Any(field => !field.IsLiteral && !field.IsInitOnly))
            {
                errors.Add(
                    $"Species combat payload {type.Name} introduced mutable static state.");
            }
        }

        const string payloadPath =
            "Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs";
        string payloadSource = File.ReadAllText(payloadPath);
        string[] forbiddenTokens =
        {
            "CharacterActor",
            "OffenseBattleSession",
            "OffenseBattleEffectContext",
            "internal abstract void Apply",
            "Assembly-CSharp.dll"
        };
        foreach (string token in forbiddenTokens)
        {
            if (payloadSource.Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Combat payload source leaks runtime/default dependency '{token}'.");
            }
        }
        if (CountOccurrences(
                payloadSource,
                "sourceAssembly: \"Assembly-CSharp\"") != 16
            || CountOccurrences(
                payloadSource,
                "sourceAssembly: \"DungeonStory.Offense\"") != 1)
        {
            errors.Add(
                "Combat payload migration identities must preserve 16 default and one Offense source assembly.");
        }

        const string combatAssemblyPath =
            "Assets/Scripts/Models/Combat/Core/DungeonStory.Combat.asmdef";
        string combatAssemblySource = File.ReadAllText(combatAssemblyPath);
        if (combatAssemblySource.Contains("Assembly-CSharp", StringComparison.Ordinal)
            || combatAssemblySource.Contains("DungeonStory.Species", StringComparison.Ordinal))
        {
            errors.Add(
                "Combat payload assembly must remain below Species and must not reference Assembly-CSharp.");
        }

        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Offense/OffenseCombatAbilities.cs",
            "public sealed class CharacterCombatAbilityDefinition",
            "Combat ability payload definitions must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Offense/OffenseCombatAbilities.cs",
            "public abstract class OffenseCombatEffectModule",
            "Combat effect payload definitions must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Offense/Core/OffensePrimitives.cs",
            "public enum OffenseFormationMask",
            "Combat formation masks must not fall back into Offense.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Offense/OffenseCombatEffectRuntime.cs",
            "switch (effect)",
            "Default runtime must explicitly adapt every pure combat effect payload.");

        IReadOnlyDictionary<string, Type> abilityFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["id"] = typeof(string),
                ["displayName"] = typeof(string),
                ["description"] = typeof(string),
                ["cooldownTurns"] = typeof(int),
                ["targetRule"] = typeof(OffenseBattleTargetRule),
                ["usableFrom"] = typeof(OffenseFormationMask),
                ["targetPositions"] = typeof(OffenseFormationMask),
                ["effects"] = typeof(List<OffenseCombatEffectModule>)
            };
        foreach (KeyValuePair<string, Type> expected in abilityFields)
        {
            System.Reflection.FieldInfo field =
                typeof(CharacterCombatAbilityDefinition).GetField(
                    expected.Key,
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
            if (field?.FieldType != expected.Value)
            {
                errors.Add(
                    $"CharacterCombatAbilityDefinition.{expected.Key} serialized field name or type changed.");
            }
        }
        System.Reflection.FieldInfo abilitiesField =
            typeof(CharacterCombatAbilityCollection).GetField(
                "abilities",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
        if (abilitiesField?.FieldType
            != typeof(List<CharacterCombatAbilityDefinition>))
        {
            errors.Add(
                "CharacterCombatAbilityCollection.abilities serialized field name or type changed.");
        }

        IReadOnlyDictionary<Type, IReadOnlyDictionary<string, Type>> effectFields =
            new Dictionary<Type, IReadOnlyDictionary<string, Type>>
            {
                [typeof(OffenseDamageEffect)] = new Dictionary<string, Type>
                {
                    ["basicDamageMultiplier"] = typeof(float),
                    ["flatDamage"] = typeof(float),
                    ["hitCount"] = typeof(int)
                },
                [typeof(OffenseHealEffect)] = new Dictionary<string, Type>
                {
                    ["flatAmount"] = typeof(float),
                    ["damageDealtRatio"] = typeof(float)
                },
                [typeof(OffenseGuardEffect)] = new Dictionary<string, Type>
                {
                    ["damageReduction"] = typeof(float),
                    ["turns"] = typeof(int)
                },
                [typeof(OffenseDelayEffect)] = new Dictionary<string, Type>
                {
                    ["initiativePenalty"] = typeof(float)
                },
                [typeof(OffenseDamageOverTimeEffect)] = new Dictionary<string, Type>
                {
                    ["damagePerTurn"] = typeof(float),
                    ["turns"] = typeof(int)
                },
                [typeof(OffenseVulnerabilityEffect)] = new Dictionary<string, Type>
                {
                    ["increasedDamage"] = typeof(float),
                    ["turns"] = typeof(int)
                },
                [typeof(OffenseAttackModifierEffect)] = new Dictionary<string, Type>
                {
                    ["multiplierDelta"] = typeof(float),
                    ["turns"] = typeof(int)
                },
                [typeof(OffenseCleanseEffect)] = new Dictionary<string, Type>
                {
                    ["maximumStatuses"] = typeof(int)
                },
                [typeof(OffenseRepositionEffect)] = new Dictionary<string, Type>
                {
                    ["offset"] = typeof(int)
                },
                [typeof(OffenseConditionalAmplifyEffect)] = new Dictionary<string, Type>
                {
                    ["extraDamageMultiplier"] = typeof(float),
                    ["healthThreshold"] = typeof(float)
                },
                [typeof(OffenseCooldownAdjustEffect)] = new Dictionary<string, Type>
                {
                    ["turnDelta"] = typeof(int)
                },
                [typeof(OffenseMultiTargetEffect)] = new Dictionary<string, Type>
                {
                    ["targetCount"] = typeof(int),
                    ["splashMultiplier"] = typeof(float)
                }
            };
        foreach (KeyValuePair<Type, IReadOnlyDictionary<string, Type>> effect
                 in effectFields)
        {
            foreach (KeyValuePair<string, Type> expected in effect.Value)
            {
                System.Reflection.FieldInfo field = effect.Key.GetField(
                    expected.Key,
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                if (field?.FieldType != expected.Value)
                {
                    errors.Add(
                        $"{effect.Key.Name}.{expected.Key} serialized field name or type changed.");
                }
            }
        }

        const string speciesAssetDirectory =
            "Assets/Resources/SO/Character/Species";
        string[] speciesAssets = Directory.GetFiles(
            speciesAssetDirectory,
            "*.asset",
            SearchOption.TopDirectoryOnly);
        int abilityReferenceCount = 0;
        Dictionary<string, int> effectReferenceCounts =
            new(StringComparer.Ordinal);
        int leafScriptReferenceCount = 0;
        foreach (string assetPath in speciesAssets)
        {
            string source = File.ReadAllText(assetPath);
            if (source.Contains(
                    "guid: af4061a25738a28e7b9dbb8593dd53e7",
                    StringComparison.Ordinal))
            {
                leafScriptReferenceCount++;
            }
            if (!source.Contains(
                    "m_EditorClassIdentifier: DungeonStory.Species::CharacterSpeciesSO",
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Species asset '{assetPath}' retains the default leaf assembly identity.");
            }
            if (source.Contains("asm: Assembly-CSharp", StringComparison.Ordinal)
                || source.Contains("type: {class: ,", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Species asset '{assetPath}' contains a broken/default managed reference.");
            }

            MatchCollection references = Regex.Matches(
                source,
                @"type: \{class: (?<class>[^,]+), ns: (?<namespace>[^,]*), asm: (?<assembly>[^}]+)\}");
            foreach (Match reference in references)
            {
                string className = reference.Groups["class"].Value;
                string assemblyName = reference.Groups["assembly"].Value;
                if (!string.Equals(
                        assemblyName,
                        combatAssemblyName,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Species asset '{assetPath}' managed reference {className} belongs to '{assemblyName}'.");
                }
                if (string.Equals(
                        className,
                        nameof(CharacterCombatAbilityDefinition),
                        StringComparison.Ordinal))
                {
                    abilityReferenceCount++;
                }
                else
                {
                    effectReferenceCounts[className] =
                        effectReferenceCounts.TryGetValue(className, out int count)
                            ? count + 1
                            : 1;
                }
            }
        }
        IReadOnlyDictionary<string, int> expectedEffects =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [nameof(OffenseDamageEffect)] = 3,
                [nameof(OffenseGuardEffect)] = 1,
                [nameof(OffenseDelayEffect)] = 1,
                [nameof(OffenseHealEffect)] = 1
            };
        if (speciesAssets.Length != 10
            || leafScriptReferenceCount != 10
            || abilityReferenceCount != 6
            || effectReferenceCounts.Count != expectedEffects.Count
            || expectedEffects.Any(expected =>
                !effectReferenceCounts.TryGetValue(
                    expected.Key,
                    out int actual)
                || actual != expected.Value))
        {
            errors.Add(
                "Species combat payload migration must preserve 10 leaf assets (9 population species plus Adventurer), 6 abilities, and the exact 3/1/1/1 effect type distribution.");
        }

        const string speciesLeafPath =
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs";
        string leafSource = File.ReadAllText(speciesLeafPath);
        if (!leafSource.Contains(
                "sourceAssembly: \"Assembly-CSharp\"",
                StringComparison.Ordinal)
            || !leafSource.Contains("CreateAssetMenu", StringComparison.Ordinal)
            || File.Exists(
                "Assets/Scripts/Services/Character/SO/CharacterSpeciesSO.cs"))
        {
            errors.Add(
                "CharacterSpeciesSO leaf migration must preserve menu/move identity and remove the default source.");
        }
    }

    private static void ValidateSpeciesAssemblyAuthority(
        GameContentCatalogSO root,
        ICollection<string> errors)
    {
        const string speciesAssemblyName = "DungeonStory.Species";
        Type[] ownedTypes =
        {
            typeof(CharacterSpeciesDefinitionSO),
            typeof(CharacterSpeciesId),
            typeof(MealDietClass),
            typeof(SpeciesNeedProfile),
            typeof(SpeciesEnvironmentProfile),
            typeof(SpeciesThermalProfile),
            typeof(SpeciesIncidentDefinition),
            typeof(SpeciesPassiveDefinition),
            typeof(CharacterSpeciesRuntimeState),
            typeof(CharacterSpeciesRuntimeSaveData),
            typeof(ICharacterSpeciesDefinitionCatalog),
            typeof(ICharacterSpeciesEnvironmentCatalog),
            typeof(ICharacterSpeciesQuery),
            typeof(ICharacterSpeciesCommand),
            typeof(CharacterSpeciesSO)
        };
        foreach (Type type in ownedTypes)
        {
            if (!string.Equals(
                    type.Assembly.GetName().Name,
                    speciesAssemblyName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Species contract {type.Name} must belong to {speciesAssemblyName}.");
            }
        }

        if (!typeof(CharacterSpeciesDefinitionSO).IsAssignableFrom(
                typeof(CharacterSpeciesSO)))
        {
            errors.Add(
                "CharacterSpeciesSO must remain the compatibility leaf over the Species-owned authored definition.");
        }
        if (!string.Equals(
                typeof(CharacterSpeciesSO).Assembly.GetName().Name,
                speciesAssemblyName,
                StringComparison.Ordinal))
        {
            errors.Add(
                "CharacterSpeciesSO must belong to the named Species assembly after combat payload migration.");
        }

        const string assemblyPath =
            "Assets/Scripts/Models/Species/Core/DungeonStory.Species.asmdef";
        if (!File.Exists(assemblyPath))
        {
            errors.Add("The Species named assembly definition is missing.");
        }
        else
        {
            string assemblySource = File.ReadAllText(assemblyPath);
            string[] requiredReferences =
            {
                "DungeonStory.Buildings",
                "DungeonStory.Characters",
                "DungeonStory.Combat",
                "DungeonStory.Foundation"
            };
            foreach (string requiredReference in requiredReferences)
            {
                if (!assemblySource.Contains(
                        $"\"{requiredReference}\"",
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Species assembly is missing required lower-rank reference {requiredReference}.");
                }
            }
            if (assemblySource.Contains("Assembly-CSharp", StringComparison.Ordinal))
            {
                errors.Add("Species assembly must not reference Assembly-CSharp.");
            }
            if (assemblySource.Contains("DungeonStory.Survival", StringComparison.Ordinal))
            {
                errors.Add(
                    "Species assembly must own diet contracts instead of depending on Survival.");
            }
        }

        const string definitionPath =
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs";
        const string runtimeContractsPath =
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesRuntimeContracts.cs";

        RequireSourceContract(
            errors,
            "Assets/Scripts/Models/Survival/Core/DungeonStory.Survival.asmdef",
            "\"DungeonStory.Species\"",
            "Survival must consume the lower-rank Species diet contract.");
        RequireSourceContract(
            errors,
            definitionPath,
            "sourceAssembly: \"DungeonStory.Survival\"",
            "The diet enum must preserve its previous Survival assembly identity.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Survival/Core/SurvivalPrimitives.cs",
            "public enum MealDietClass",
            "The Species diet contract must not fall back into Survival.");

        string namedSource = File.ReadAllText(definitionPath)
            + File.ReadAllText(runtimeContractsPath);
        string[] forbiddenNamedTokens =
        {
            "CharacterActor",
            "CharacterSpeciesSO",
            "CharacterStatBlock",
            "CharacterModelModifiers",
            "CharacterCombatAbility",
            "IGameContentCatalog",
            "FacilityWorkTypeMap",
            "VContainer"
        };
        foreach (string token in forbiddenNamedTokens)
        {
            if (namedSource.Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Species named assembly leaks default/runtime dependency '{token}'.");
            }
        }
        if (!namedSource.Contains(
                "sourceAssembly: \"Assembly-CSharp\"",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Moved Species contracts must declare their Assembly-CSharp origin.");
        }

        ForbidSourceContract(
            errors,
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesCatalog.cs",
            "public sealed class SpeciesNeedProfile",
            "Species authored profiles must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Character/SpeciesRuntime.cs",
            "public sealed class CharacterSpeciesRuntimeSaveData",
            "Species save DTOs must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Character/SpeciesRuntime.cs",
            "ICharacterSpecies" + "Runtime",
            "Character species runtime consumers must depend on Query, Command, or Persistence facets instead of a broad wrapper.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Character/SpeciesRuntime.cs",
            "ICharacterSpeciesQuery,\n    ICharacterSpeciesCommand,\n    ICharacterSpeciesPersistence,",
            "CharacterSpeciesRuntime must expose the three narrow species facets directly.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs",
            ".As<ICharacterSpeciesDefinitionCatalog>()",
            "The root Species catalog adapter must expose the named definition port.");

        string leafGuid = AssetDatabase.AssetPathToGUID(
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs");
        if (!string.Equals(
                leafGuid,
                "af4061a25738a28e7b9dbb8593dd53e7",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"CharacterSpeciesSO GUID changed to '{leafGuid}'.");
        }

        GameDomainContentCatalogSO domain = root?.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault();
        CharacterSpeciesDefinitionSO[] definitions = domain?
            .GetAll<CharacterSpeciesDefinitionSO>()
            .ToArray() ?? Array.Empty<CharacterSpeciesDefinitionSO>();
        if (definitions.Length != 10)
        {
            errors.Add(
                $"Expected 10 root-indexed Species authored definitions (9 population species plus Adventurer), found {definitions.Length}.");
        }
        else
        {
            try
            {
                IReadOnlyList<CharacterSpeciesDefinitionSO> normalized =
                    CharacterSpeciesDefinitionCatalogRequirements.Normalize(definitions);
                if (normalized.Count != definitions.Length)
                {
                    errors.Add("Species authored normalization lost root-catalog definitions.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Species authored definition validation failed: {exception.Message}");
            }
        }
    }

    private static void ValidateCharacterAuthoredModelAuthority(
        ICollection<string> errors)
    {
        const string charactersAssemblyName = "DungeonStory.Characters";
        Type[] ownedTypes =
        {
            typeof(CharacterStatEntry),
            typeof(CharacterStatBlock),
            typeof(CharacterModelModifiers)
        };
        foreach (Type type in ownedTypes)
        {
            if (!string.Equals(
                    type.Assembly.GetName().Name,
                    charactersAssemblyName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Character authored model {type.Name} must belong to {charactersAssemblyName}.");
            }

            if (type.GetConstructors().Any(constructor =>
                    constructor.GetParameters().Length > 8))
            {
                errors.Add(
                    $"Character authored model {type.Name} introduced a large constructor.");
            }

            if (type.GetFields(
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Any(field => !field.IsLiteral && !field.IsInitOnly))
            {
                errors.Add(
                    $"Character authored model {type.Name} introduced mutable static state.");
            }
        }

        const string modelPath =
            "Assets/Scripts/Models/Characters/CharacterAuthoredModel.cs";
        string modelSource = File.ReadAllText(modelPath);
        string[] forbiddenTokens =
        {
            "CharacterActor",
            "MonoBehaviour",
            "FacilityWorkTypeMap",
            "Mathf",
            "VContainer",
            "IGameContentCatalog"
        };
        foreach (string token in forbiddenTokens)
        {
            if (modelSource.Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Character authored model leaks runtime/default dependency '{token}'.");
            }
        }
        if (CountOccurrences(
                modelSource,
                "sourceAssembly: \"Assembly-CSharp\"") != 3)
        {
            errors.Add(
                "Character authored model must preserve three Assembly-CSharp move identities.");
        }

        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Character/SO/CharacterModelData.cs",
            "public class CharacterStatBlock",
            "CharacterStatBlock must not fall back into Assembly-CSharp.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Character/SO/CharacterModelData.cs",
            "public class CharacterModelModifiers",
            "CharacterModelModifiers must not fall back into Assembly-CSharp.");

        const string assemblyPath =
            "Assets/Scripts/Models/Characters/DungeonStory.Characters.asmdef";
        string assemblySource = File.ReadAllText(assemblyPath);
        foreach (string reference in new[]
                 {
                     "DungeonStory.Buildings",
                     "DungeonStory.Work"
                 })
        {
            if (!assemblySource.Contains($"\"{reference}\"", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Characters assembly is missing authored-model reference {reference}.");
            }
        }
        if (assemblySource.Contains("Assembly-CSharp", StringComparison.Ordinal)
            || assemblySource.Contains("DungeonStory.Species", StringComparison.Ordinal))
        {
            errors.Add(
                "Characters assembly must remain below Species and must not reference Assembly-CSharp.");
        }

        System.Reflection.FieldInfo entriesField = typeof(CharacterStatBlock).GetField(
            "entries",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        if (entriesField?.FieldType != typeof(List<CharacterStatEntry>))
        {
            errors.Add(
                "CharacterStatBlock.entries serialized field name or type changed.");
        }

        IReadOnlyDictionary<string, Type> expectedModifierFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["consumptionMultiplier"] = typeof(float),
                ["spendingMultiplier"] = typeof(float),
                ["waitPatienceMultiplier"] = typeof(float),
                ["crowdSensitivityMultiplier"] = typeof(float),
                ["accidentChanceMultiplier"] = typeof(float),
                ["workSpeedMultiplier"] = typeof(float),
                ["researchSpeedMultiplier"] = typeof(float),
                ["combatPowerMultiplier"] = typeof(float),
                ["moveSpeedMultiplier"] = typeof(float),
                ["stayDurationMultiplier"] = typeof(float),
                ["preferredFacilityRoles"] = typeof(FacilityRole),
                ["dislikedFacilityRoles"] = typeof(FacilityRole),
                ["preferredWorkTypes"] = typeof(FacilityWorkType),
                ["dislikedWorkTypes"] = typeof(FacilityWorkType)
            };
        foreach (KeyValuePair<string, Type> expected in expectedModifierFields)
        {
            System.Reflection.FieldInfo field = typeof(CharacterModelModifiers).GetField(
                expected.Key,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            if (field?.FieldType != expected.Value)
            {
                errors.Add(
                    $"CharacterModelModifiers.{expected.Key} serialized field name or type changed.");
            }
        }

        FacilityWorkType allLegacyTypes = Enum.GetValues(typeof(FacilityWorkType))
            .Cast<FacilityWorkType>()
            .Where(value => value != FacilityWorkType.None)
            .Aggregate(FacilityWorkType.None, (current, value) => current | value);
        CharacterModelModifiers mappingProbe = new();
        mappingProbe.SetWorkPreferences(allLegacyTypes, allLegacyTypes);
        WorkTypeId[] preferredIds = mappingProbe.PreferredWorkTypeIds.ToArray();
        WorkTypeId[] dislikedIds = mappingProbe.DislikedWorkTypeIds.ToArray();
        if (preferredIds.Length != 30
            || dislikedIds.Length != 30
            || preferredIds.Distinct().Count() != 30
            || dislikedIds.Distinct().Count() != 30
            || !preferredIds.SequenceEqual(
                WorkTypeCatalog.All.Select(definition => definition.WorkTypeId)))
        {
            errors.Add(
                "CharacterModelModifiers legacy work projection must preserve all 30 stable IDs in catalog order.");
        }

        System.Reflection.FieldInfo speciesStats = typeof(CharacterSpeciesSO).GetField(
            "statBonus");
        System.Reflection.FieldInfo speciesModifiers = typeof(CharacterSpeciesSO).GetField(
            "modifiers");
        System.Reflection.FieldInfo speciesCombat = typeof(CharacterSpeciesSO).GetField(
            "combatAbilities");
        if (speciesStats?.FieldType.Assembly.GetName().Name != charactersAssemblyName
            || speciesModifiers?.FieldType.Assembly.GetName().Name != charactersAssemblyName)
        {
            errors.Add(
                "CharacterSpeciesSO stat/modifier fields must consume the named Characters contracts.");
        }
        if (!string.Equals(
                speciesCombat?.FieldType.Assembly.GetName().Name,
                "DungeonStory.Combat",
                StringComparison.Ordinal))
        {
            errors.Add(
                "CharacterSpeciesSO combat managed-reference collection must belong to DungeonStory.Combat.");
        }

        ValidateV19ValueContracts(errors);
    }

    private static void ValidateV19ValueContracts(ICollection<string> errors)
    {
        foreach (Type valueOnlyType in new[]
                 {
                     typeof(CharacterRuntimeProfile),
                     typeof(CharacterSpawnRequest),
                     typeof(CharacterDeathEvent)
                 })
        {
            System.Reflection.FieldInfo[] unityObjectFields = valueOnlyType
                .GetFields(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic)
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(
                    field.FieldType))
                .ToArray();
            foreach (System.Reflection.FieldInfo field in unityObjectFields)
            {
                errors.Add(
                    $"V19 value-only contract {valueOnlyType.Name}.{field.Name} retains Unity object authority {field.FieldType.Name}.");
            }
        }

        if (typeof(CharacterDeathEvent).GetField(
                "Actor",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic) != null)
        {
            errors.Add(
                "CharacterDeathEvent must publish CharacterId and immutable context, never CharacterActor.");
        }
    }

    private static void ValidateCharacterSummaryDecomposition(ICollection<string> errors)
    {
        const string summaryPath =
            "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs";
        if (File.Exists(summaryPath))
        {
            int lineCount = File.ReadLines(summaryPath).Count();
            if (lineCount > 800)
            {
                errors.Add(
                    $"CharacterSummaryInfo must remain an <=800-line view coordinator, found {lineCount} lines.");
            }
        }

        Type summaryType = typeof(CharacterSummaryInfo);
        System.Reflection.MethodInfo injectionPoint = summaryType.GetMethod(
            "Construct",
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance);
        int dependencyCount = injectionPoint?.GetParameters().Length ?? int.MaxValue;
        if (dependencyCount > 8)
        {
            errors.Add(
                $"CharacterSummaryInfo may have at most 8 injected dependencies, found {dependencyCount}.");
        }

        string[] presenterPaths =
        {
            "Assets/Scripts/Views/UI/CharacterSummaryShellPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryStatusPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryGrowthPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryAiPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryHealthPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryCaptivityPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryCombatPresenter.cs"
        };
        foreach (string path in presenterPaths)
        {
            if (!File.Exists(path))
            {
                errors.Add($"Required character summary presenter source '{path}' is missing.");
                continue;
            }

            int lineCount = File.ReadLines(path).Count();
            if (lineCount > 800)
            {
                errors.Add($"Presenter '{path}' exceeds the 800-line limit ({lineCount}).");
            }
        }
    }

    private static void ValidatePresentationDependencyCuts(
        ICollection<string> errors)
    {
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/ResearchTreeInteractions.cs",
            "public interface IResearchTreeInteractionSink",
            "Research-tree gestures must target a narrow interaction port.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Views/UI/ResearchTreeInteractions.cs",
            "Research-tree interaction components must remain in the named Presentation assembly.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Views/UI/ResearchTreeViewportController.cs",
            "Research-tree viewport control must remain in the named Presentation assembly.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/ResearchTreeInteractions.cs",
            "[MovedFrom(true, sourceAssembly: \"Assembly-CSharp\")]",
            "Moved research-tree MonoBehaviours must retain their Assembly-CSharp serialization identity.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/ResearchTreeInteractions.cs",
            "ResearchTreeWindow owner",
            "Research-tree gestures must not depend on the concrete window coordinator.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/ResearchTreeWindow.cs",
            "IResearchTreeInteractionSink",
            "The research-tree window must explicitly implement its gesture port.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/ICharacterSummaryRuntimeLogFactory.cs",
            "public interface ICharacterSummaryGeneratedView",
            "Character-summary view generation must bind through a focused generated-view port.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/ICharacterSummaryRuntimeLogFactory.cs",
            "public sealed class CharacterSummaryViewActions",
            "Character-summary callbacks must travel through the cohesive action bundle.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Views/UI/CharacterSummaryShellPresenter.cs",
            "CharacterSummaryInfo owner",
            "Character-summary shell presentation must not depend on its concrete view coordinator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Views/UI/CharacterSummaryRuntimeLogFactory.cs",
            "CharacterSummaryInfo owner",
            "Character-summary view generation must not depend on its concrete view coordinator.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicViewFactory.cs",
            "Offense strategic rendering must not return to the default-assembly partial panel file.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/OffenseWorldMapPanelStrategicViewFactory.cs",
            "public sealed class OffenseWorldMapStrategicViewFactory",
            "Offense strategic rendering must remain an independent Presentation collaborator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/OffenseWorldMapPanelStrategicViewFactory.cs",
            "partial class OffenseWorldMapPanel",
            "The Offense strategic view factory must not rejoin the panel partial class.");
        RequireSourceFileAbsent(
            errors,
            "Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicPreparation.cs",
            "Offense preparation presentation must not return to a default-assembly panel partial.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/OffenseWorldMapPanelStrategicPreparationPresenter.cs",
            "public sealed class OffenseStrategicPreparationPresenter",
            "Offense preparation rendering must remain an independent Presentation collaborator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Views/UI/Core/OffenseWorldMapPanelStrategicPreparationPresenter.cs",
            "partial class OffenseWorldMapPanel",
            "Offense preparation presentation must not rejoin the panel partial class.");

        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.StateAndCapabilities.cs",
            "class BuildableObjectStateAndCapabilityController",
            "BuildableObject state and capability behavior must remain in its cohesive controller.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.SpatialAndInteraction.cs",
            "class BuildableObjectSpatialQuery",
            "BuildableObject spatial calculations must remain in their query collaborator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.cs",
            "partial class BuildableObject",
            "BuildableObject must remain a single serialized MonoBehaviour shell.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.StateAndCapabilities.cs",
            "partial class BuildableObject",
            "BuildableObject state behavior must not reintroduce partial ownership.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Buildings/BuildableObject.SpatialAndInteraction.cs",
            "partial class BuildableObject",
            "BuildableObject spatial behavior must not reintroduce partial ownership.");

        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderContentBinding.cs",
            "class InvasionIntruderContentBinding",
            "Invasion intruder content resolution must remain in its authored-content collaborator.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderRuntime.Restore.cs",
            "class InvasionIntruderRestoreCoordinator",
            "Invasion intruder restore staging must remain in its dedicated coordinator.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderSystem.cs",
            "partial class InvasionIntruderRuntime",
            "Invasion intruder execution must remain a single non-partial runtime declaration.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderContentBinding.cs",
            "partial class InvasionIntruderRuntime",
            "Invasion content binding must not reintroduce partial runtime ownership.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Invasion/InvasionIntruderRuntime.Restore.cs",
            "partial class InvasionIntruderRuntime",
            "Invasion restore coordination must not reintroduce partial runtime ownership.");
    }

    private static void ValidateDomainFailureLocalization(
        ICollection<string> errors)
    {
        LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(
            "Assets/Localization/LocalizationSettings.asset");
        StringTable koreanTable = AssetDatabase.LoadAssetAtPath<StringTable>(
            "Assets/Localization/DomainFailures_ko.asset");
        StringTable englishTable = AssetDatabase.LoadAssetAtPath<StringTable>(
            "Assets/Localization/DomainFailures_en.asset");
        if (settings == null)
        {
            errors.Add("Active localization settings asset is missing.");
        }
        if (koreanTable == null)
        {
            errors.Add("DomainFailures Korean String Table is missing.");
            return;
        }

        if (englishTable == null)
        {
            errors.Add("DomainFailures English String Table is missing.");
            return;
        }

        try
        {
            DomainFailureLocalizationAssetBuilder.ValidateTablesOrThrow(
                koreanTable,
                englishTable);
        }
        catch (Exception exception)
        {
            errors.Add(
                $"DomainFailures localization contract failed: {exception.Message}");
        }
    }

    private static void ValidateAssets(ICollection<string> errors)
    {
        string root = Path.Combine(Application.dataPath, "Resources", "SO");
        foreach (string path in Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories))
        {
            if (File.ReadAllText(path).Contains("stock-item:", StringComparison.Ordinal))
            {
                errors.Add($"Authored asset contains forbidden abstract stock item: {path}");
            }
        }
    }

    private static void ValidateWarehouseAuthority(ICollection<string> errors)
    {
        if (typeof(WarehouseInventorySnapshot).GetField("stocks") != null)
        {
            errors.Add("Warehouse snapshot still serializes aggregate stock quantities.");
        }
        if (typeof(WarehouseInventory).GetField(
                "stockByCategory",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic) != null)
        {
            errors.Add("WarehouseInventory still owns an aggregate quantity dictionary.");
        }

        string[] forbiddenWrites =
        {
            ".Inventory.Deposit(",
            ".Inventory.Withdraw(",
            ".Inventory.AddStock("
        };
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)) continue;
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenWrites)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    errors.Add($"Runtime source '{path}' mutates aggregate warehouse stock via '{token}'.");
                }
            }
        }
    }

    private static void ValidateUniqueItemAuthority(ICollection<string> errors)
    {
        int physicalSaveVersion = Convert.ToInt32(
            DungeonPhysicalItemSaveData.CurrentVersion);
        if (physicalSaveVersion != 6)
        {
            errors.Add(
                $"Physical item save must be V6, found V{physicalSaveVersion}.");
        }
        if (typeof(DungeonCombatEquipmentSaveData).GetField("instances") != null
            || typeof(DungeonCombatEquipmentSaveData).GetField("moduleInstances") != null)
        {
            errors.Add(
                "Combat save still serializes equipment or module instance authority.");
        }
        if (typeof(CharacterCarriedItemSaveData).GetField("itemInstanceId") == null)
        {
            errors.Add("Carried items do not preserve ItemInstanceId.");
        }

        System.Reflection.ConstructorInfo[] constructors =
            typeof(CombatEquipmentRuntime).GetConstructors();
        if (constructors.Length != 1
            || !constructors[0].GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IItemInstanceRepository)
                && !parameter.IsOptional))
        {
            errors.Add(
                "CombatEquipmentRuntime must have one construction path with a required IItemInstanceRepository.");
        }

        int combatEquipmentSaveVersion = Convert.ToInt32(
            typeof(CombatEquipmentSaveSection)
                .GetField(nameof(CombatEquipmentSaveSection.CurrentVersion))
                .GetRawConstantValue());
        if (combatEquipmentSaveVersion != 6)
        {
            errors.Add(
                $"Combat equipment save section is not V6 (found V{combatEquipmentSaveVersion}).");
        }
    }

    private static void ValidatePersistentIdentityAuthority(
        ICollection<string> errors)
    {
        Type[] requiredIdTypes =
        {
            typeof(ItemInstanceId),
            typeof(ItemStackId),
            typeof(CharacterId),
            typeof(BuildingInstanceId),
            typeof(WildlifeHabitatPatchId)
        };
        foreach (Type idType in requiredIdTypes)
        {
            if (!typeof(IPersistentEntityId).IsAssignableFrom(idType)
                || !idType.IsValueType)
            {
                errors.Add($"{idType.Name} must be a typed persistent value id.");
            }
        }

        string[] requiredGeneratorMethods =
        {
            nameof(IPersistentIdGenerator.NewItemInstanceId),
            nameof(IPersistentIdGenerator.NewItemStackId),
            nameof(IPersistentIdGenerator.NewCharacterId),
            nameof(IPersistentIdGenerator.NewBuildingInstanceId),
            nameof(IPersistentIdGenerator.NewWildlifeHabitatPatchId)
        };
        foreach (string method in requiredGeneratorMethods)
        {
            if (typeof(IPersistentIdGenerator).GetMethod(method) == null)
            {
                errors.Add($"IPersistentIdGenerator is missing '{method}'.");
            }
        }

        if (!CharacterId.Owner.IsValid
            || !((CharacterId)"character:validator").IsValid
            || ((CharacterId)"Named Hero").IsValid
            || ((CharacterId)"building:validator").IsValid)
        {
            errors.Add(
                "CharacterId must accept only 'owner' or a non-empty 'character:*' ID.");
        }

        const string habitatPath =
            "Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatRuntime.cs";
        if (File.ReadAllText(habitatPath).Contains(
                "GetInstanceID()",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Wildlife habitat persistence still derives an id from GetInstanceID().");
        }
    }

    private static void ValidateSessionAndScopedStateAuthority(
        GameContentCatalogSO root,
        ICollection<string> errors)
    {
        if (!typeof(ScriptableObject).IsAssignableFrom(typeof(GameData)))
        {
            errors.Add("GameData must remain a settings-only ScriptableObject.");
        }

        bool gameDataOwnsReactiveState = typeof(GameData)
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Any(field => field.FieldType.IsGenericType
                && string.Equals(
                    field.FieldType.GetGenericTypeDefinition().FullName,
                    "Data`1",
                    StringComparison.Ordinal));
        if (gameDataOwnsReactiveState)
        {
            errors.Add("GameData still owns mutable session Data<T> fields.");
        }

        if (typeof(ScriptableObject).IsAssignableFrom(typeof(GameSessionState)))
        {
            errors.Add("GameSessionState must be a plain run-scoped C# object.");
        }

        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/GameRuntimeServices.cs",
            "ScopedGameSessionStateStore",
            "A run-scoped store, not GameManager, must own GameSessionState.");
        string gameManagerSource = File.ReadAllText(
            "Assets/Scripts/Controllers/GameManager.cs");
        if (gameManagerSource.Contains("new GameSessionState(", StringComparison.Ordinal))
        {
            errors.Add("GameManager still constructs mutable session state.");
        }

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)
                || string.Equals(
                    path,
                    "Assets/Scripts/Models/GameData/GameSessionState.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            if (Regex.IsMatch(
                    source,
                    @"gameData\.(?:gameSpeed|holdingMoney|day|curTime|hour|timeOfDay)\.(?:Initialize\s*\(|Value\s*=)"))
            {
                errors.Add(
                    $"Runtime source '{path}' writes session state outside its authority service.");
            }
        }

        if (root == null
            || root.GetWorldPresentation<WorldInteractionPresentationCatalogSO>() == null
            || root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>() == null
            || root.GetMedia<GameMediaCatalogSO>() == null)
        {
            errors.Add(
                "GameContentCatalogSO must author presentation, media, and character-skill settings references.");
        }

        ValidateNoMutableStaticFields(typeof(CharacterCarryInventory), errors);
        ValidateNoMutableStaticFields(typeof(CharacterCarryInventoryRegistry), errors);
        ValidateNoMutableStaticFields(typeof(CombatCoverDurability), errors);
        ValidateNoMutableStaticFields(typeof(CharacterSkillTransientState), errors);
        ValidateNoMutableStaticFields(typeof(DungeonSceneNavigator), errors);
        ValidateNoMutableStaticFields(typeof(DungeonDebugRuleRuntime), errors);

        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs",
            ".As<ICharacterRuntimeTransientStateRegistry>()",
            "Carry and skill transient state must share one scoped owner registration.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            "public readonly HashSet<string> ExecutedEventKeys",
            "Skill exactly-once history must remain owned by each character's run-scoped inventory state.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            "ExecutedEventOrder",
            "Skill exactly-once history must not evict older event keys.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            "MaxExecutedEventKeys",
            "Skill exactly-once history must not impose a bounded event-key window.");
        ForbidSourceContract(
            errors,
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            ".Dequeue()",
            "Skill exactly-once history must not evict queued event keys.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs",
            ".As<IDungeonDebugRuleQuery>()",
            "Dungeon debug rules must be registered as a scoped query capability.");
        RequireSourceContract(
            errors,
            "Assets/Scripts/Services/Debugging/DungeonDebugRuntime.cs",
            "public sealed class DungeonDebugRuleRuntime : IDungeonDebugRuleRuntime",
            "Dungeon debug gameplay rules must have one scoped runtime owner.");

        string[] forbiddenRuntimeTokens =
        {
            "DungeonUserSettingsRuntime.",
            "WorldInteractionPresentationCatalogRuntime",
            "ActiveInventories",
            "static readonly HashSet<string> executingKeys",
            "static readonly HashSet<string> executedEventKeys",
            "DungeonDebugRuntimeRules"
        };
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)) continue;
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenRuntimeTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Runtime source '{path}' contains forbidden global state '{token}'.");
                }
            }
        }
    }

    private static void ValidateNoMutableStaticFields(
        Type type,
        ICollection<string> errors)
    {
        foreach (System.Reflection.FieldInfo field in type.GetFields(
                     System.Reflection.BindingFlags.Static
                     | System.Reflection.BindingFlags.Public
                     | System.Reflection.BindingFlags.NonPublic))
        {
            if (field.IsLiteral)
            {
                continue;
            }

            errors.Add(
                $"{type.Name}.{field.Name} is mutable static run state.");
        }
    }

    private static void ValidateOffenseAggregateAuthority(
        ICollection<string> errors)
    {
        if (!string.Equals(
                OffenseAggregateSaveSection.Id,
                "offense.aggregate",
                StringComparison.Ordinal))
        {
            errors.Add("The canonical offense aggregate section ID changed.");
        }
        Type expectedStrictBase = typeof(DungeonStrictJsonSaveSection<,>)
            .MakeGenericType(
                typeof(DungeonOffenseAggregateSaveData),
                typeof(OffenseAggregateRuntimeRestoreCandidate));
        if (typeof(OffenseAggregateSaveSection).BaseType != expectedStrictBase
            || typeof(IDungeonRestoreTransactionParticipant).IsAssignableFrom(
                typeof(OffenseAggregateSaveSection)))
        {
            errors.Add(
                "Offense aggregate must build its complete detached candidate during strict staging and must not own a second restore transaction.");
        }
        int expeditionSaveVersion = Convert.ToInt32(
            typeof(DungeonOffenseSaveData)
                .GetField(nameof(DungeonOffenseSaveData.CurrentVersion))
                .GetRawConstantValue());
        if (expeditionSaveVersion != 2
            || typeof(DungeonOffenseSaveData).GetField(
                nameof(DungeonOffenseSaveData.hasActiveBattle))?.FieldType
                != typeof(bool))
        {
            errors.Add(
                "Offense expedition V2 must persist an explicit active-battle presence bit.");
        }
        if (typeof(DungeonOffenseAggregateSaveData).GetField("campaign")?.FieldType
                != typeof(DungeonOffenseCampaignSaveData))
        {
            errors.Add(
                "Offense aggregate does not own the canonical campaign payload.");
        }
        string[] retiredCampaignFields =
        {
            "reconLevel",
            "selectedTargetId",
            "knownTargetIds",
            "completedTargetIds",
            "revealedTruthTargetId"
        };
        foreach (string fieldName in retiredCampaignFields)
        {
            if (typeof(DungeonOffenseSaveData).GetField(fieldName) != null)
            {
                errors.Add(
                    $"Offense expedition payload still duplicates campaign field '{fieldName}'.");
            }
        }

        string aggregateValidationPath =
            "Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs";
        string aggregateValidation = File.ReadAllText(
            aggregateValidationPath);
        if (!aggregateValidation.Contains(
                "hidden battle state while hasActiveBattle is false",
                StringComparison.Ordinal)
            || !aggregateValidation.Contains(
                "detached.expedition.activeBattle = null",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Offense aggregate restore must reject hidden battle data and canonicalize only a verified JsonUtility null placeholder.");
        }

        string saveRegistrationPath =
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs";
        string registration = File.ReadAllText(saveRegistrationPath);
        int aggregateRegistrations = CountOccurrences(
            registration,
            "Register<OffenseAggregateSaveSection>");
        if (aggregateRegistrations != 1)
        {
            errors.Add(
                $"Expected one offense aggregate registration, found {aggregateRegistrations}.");
        }
        if (registration.Contains(
                "Register<OffenseAggregateSaveSection>(Lifetime.Singleton)\n            .As<IDungeonSaveSection>()\n            .As<IDungeonRestoreTransactionParticipant>()",
                StringComparison.Ordinal))
        {
            errors.Add(
                "Offense aggregate is still registered as a second restore-transaction participant.");
        }

        string aggregateSectionPath =
            "Assets/Scripts/Services/Offense/OffenseSaveSections.cs";
        string aggregateSection = File.ReadAllText(aggregateSectionPath);
        string[] requiredAggregateTokens =
        {
            "DungeonStrictJsonSaveSection<",
            "BuildRestoreCandidate(DungeonOffenseAggregateSaveData payload)",
            "campaign.BuildRestoreCandidate(data.campaign)",
            "expedition.BuildRestoreCandidate(",
            "world.BuildRestoreCandidate(data.world, report)",
            "returnArrivals.BuildRestoreCandidate(data.returnArrivals, report)",
            "protected override void PublishRestoreCandidate("
        };
        foreach (string token in requiredAggregateTokens)
        {
            if (!aggregateSection.Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Offense aggregate strict candidate source is missing '{token}'.");
            }
        }
        string[] forbiddenAggregateTokens =
        {
            "BeginRestoreCandidate(",
            "DiscardRestoreCandidate(",
            "restoreTransactionActive",
            "DungeonDelegateSaveRestoreStage"
        };
        foreach (string token in forbiddenAggregateTokens)
        {
            if (aggregateSection.Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Offense aggregate still contains the retired restore bypass '{token}'.");
            }
        }

        Type[] subsystemContracts =
        {
            typeof(IOffenseWorldSimulation),
            typeof(IOffenseReturnSafetyRuntime),
            typeof(IOffenseTravelRuntime),
            typeof(IOffenseDecisionRuntime),
            typeof(IOffenseBattleDirector),
            typeof(IOffenseUrgentMitigationRuntime),
            typeof(IOffenseFieldMedicalRuntime),
            typeof(IOffenseRegionRuntime),
            typeof(IOffenseReturnArrivalRuntime)
        };
        foreach (Type contract in subsystemContracts)
        {
            if (contract.GetMethods().Any(method =>
                    string.Equals(method.Name, "Restore", StringComparison.Ordinal)
                    || string.Equals(method.Name,
                        "RestoreState",
                        StringComparison.Ordinal)
                    || string.Equals(method.Name,
                        "ValidateRestore",
                        StringComparison.Ordinal)))
            {
                errors.Add(
                    $"{contract.Name} still exposes a direct subsystem restore bypass.");
            }
        }

        (string Path, string Token)[] retiredSubsystemBypasses =
        {
            ("Assets/Scripts/Services/Offense/OffenseWorldMapRuntime.cs",
                "public void RestorePersistentState("),
            ("Assets/Scripts/Services/Offense/OffenseRewardRuntime.cs",
                "public void RestorePersistentState("),
            ("Assets/Scripts/Services/Offense/OffenseExpeditionRuntime.cs",
                "public void RestorePersistentState("),
            ("Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs",
                "public void ValidateRestore("),
            ("Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseHexWorldSimulation.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs",
                "public void Restore("),
            ("Assets/Scripts/Services/Offense/Strategic/OffenseFieldMedicalRuntime.cs",
                "public void Restore(")
        };
        foreach ((string path, string token) in retiredSubsystemBypasses)
        {
            if (File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Offense subsystem source '{path}' still exposes '{token}'.");
            }
        }

        string[] forbiddenTokens =
        {
            "class OffenseSaveSection",
            "class OffenseStrategicSaveSection",
            "class OffenseRegionSaveSection",
            "class OffenseReturnArrivalSaveSection",
            "BindStrategicRuntime(",
            "V17",
            "v17"
        };
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:MonoScript",
                     new[]
                     {
                         "Assets/Scripts/Services/Offense",
                         "Assets/Scripts/Services/Infrastructure"
                     }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Offense authority source '{path}' contains retired token '{token}'.");
                }
            }
        }
    }

    private static int CountOccurrences(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while (!string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(token)
            && (offset = source.IndexOf(
                token,
                offset,
                StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
    }
}
#endif
