using System;
using System.Collections.Generic;

public sealed class ModularFacilityWorldSaveSection :
    DungeonStrictJsonSaveSection<
        ModularFacilityWorldSaveData,
        ModularFacilityWorldRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.facilities";
    public const int CurrentSectionVersion = 1;

    private readonly IModularFacilityWorldSaveService worldSaveService;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public ModularFacilityWorldSaveSection(
        IModularFacilityWorldSaveService worldSaveService,
        IGridSystemProvider gridSystemProvider,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.worldSaveService = worldSaveService
            ?? throw new ArgumentNullException(nameof(worldSaveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentSectionVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.World;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        FoundationSessionSaveSection.Id,
        RunVariableSaveSection.Id,
        MetaProgressionSaveSection.Id
    };

    protected override ModularFacilityWorldSaveData CapturePayload()
    {
        return worldSaveService.CreateSnapshot(ResolveWorld());
    }

    protected override void ValidateRawPayload(string payloadJson) =>
        ModularFacilityWorldSaveCodec.ValidateSpatialPublicationJson(payloadJson);

    protected override void NormalizeRestorePayload(
        ModularFacilityWorldSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        ModularFacilityWorldSaveData payload)
    {
        Grid grid = ResolveWorld();
        ModularFacilityWorldRestoreReport validation =
            worldSaveService.ValidateRestore(grid, payload);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Facility-world restore payload is invalid: "
                + string.Join(" | ", validation.errors));
        }
    }

    protected override ModularFacilityWorldRestoreCandidate
        BuildRestoreCandidate(ModularFacilityWorldSaveData source)
    {
        return worldSaveService.PrepareRestoreCandidate(
            ResolveWorld(),
            source);
    }

    protected override void PublishRestoreCandidate(
        ModularFacilityWorldRestoreCandidate candidate)
    {
        worldSaveService.StageRestoreCandidate(candidate);
    }

    protected override void PublishRestoreCandidateProjection(
        ModularFacilityWorldSaveData payload,
        ModularFacilityWorldRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetWorld(payload);

    private Grid ResolveWorld()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            throw new InvalidOperationException(
                "Cannot save or restore before the dungeon grid is initialized.");
        }
        return grid;
    }
}

public sealed class CharacterWorldSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterWorldSaveData,
        CharacterWorldRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "characters.world";
    public const int CurrentVersion = 10;

    private readonly ICharacterWorldSaveService saveService;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public CharacterWorldSaveSection(
        ICharacterWorldSaveService saveService,
        IGridSystemProvider gridSystemProvider,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Characters;
    public override IReadOnlyList<string> DependsOn =>
        new[] { ModularFacilityWorldSaveSection.Id };

    protected override DungeonCharacterWorldSaveData CapturePayload()
    {
        return saveService.Capture(ResolveGrid());
    }

    protected override string SerializePayload(DungeonCharacterWorldSaveData payload) =>
        DungeonCharacterWorldSaveCodec.Serialize(payload);

    protected override void ValidateRawPayload(string payloadJson)
    {
        ValidateRetailPurchaseJson(payloadJson);
        DungeonStrictJsonShape.RequireObjectArrayNonNegativeInt64Fields(
            SectionId, payloadJson, "actors", "lifecycleRevision");
        var actors = (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(payloadJson)["actors"];
        foreach (var actor in actors)
        {
            var rest = actor["restObservation"];
            if (rest == null) throw new InvalidOperationException("characters.world rest observation presence is missing.");
            if (rest.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                ValidateRestShape(rest, typeof(CharacterRestObservationSaveData));
            var last = actor["growth"]?["useLimits"]?["lastSkillActivation"];
            if (last is Newtonsoft.Json.Linq.JObject value
                && value["observationPending"]?.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                throw new InvalidOperationException("characters.world skill observation presence is missing.");
        }
        var root = Newtonsoft.Json.Linq.JObject.Parse(payloadJson);
        if (root["populationProfiles"] is not Newtonsoft.Json.Linq.JArray profiles)
            throw new InvalidOperationException("characters.world population profiles missing.");
        foreach (var profile in profiles)
        {
            if (profile["nextVisitorExitSequence"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer)
                throw new InvalidOperationException("characters.world visitor exit sequence missing.");
            ValidateRestShape(profile["visitorExitOperation"], typeof(CharacterVisitorExitOperationSaveData));
        }
    }

    private static void ValidateRestShape(Newtonsoft.Json.Linq.JToken token, Type type)
    {
        if (type == typeof(CharacterEventContext) && token?.Type == Newtonsoft.Json.Linq.JTokenType.Null) return;
        bool valid;
        if (type == typeof(string)) valid = token?.Type == Newtonsoft.Json.Linq.JTokenType.String;
        else if (type == typeof(bool)) valid = token?.Type == Newtonsoft.Json.Linq.JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = token?.Type is Newtonsoft.Json.Linq.JTokenType.Float or Newtonsoft.Json.Linq.JTokenType.Integer;
        else if (type == typeof(int) || type == typeof(long) || type.IsEnum) valid = token?.Type == Newtonsoft.Json.Linq.JTokenType.Integer;
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (token is not Newtonsoft.Json.Linq.JArray values) throw new InvalidOperationException("rest-json-list-missing");
            foreach (var value in values) ValidateRestShape(value, type.GetGenericArguments()[0]);
            return;
        }
        else
        {
            if (token is not Newtonsoft.Json.Linq.JObject value) throw new InvalidOperationException("rest-json-object-missing");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                ValidateRestShape(value[field.Name], field.FieldType);
            return;
        }
        if (!valid) throw new InvalidOperationException("rest-json-field-invalid:" + type.Name);
    }

    public static void ValidateRetailPurchaseJson(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json, new Newtonsoft.Json.Linq.JsonLoadSettings
        { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
        if (root["actors"] is not Newtonsoft.Json.Linq.JArray actors)
            throw new InvalidOperationException("characters.world actors missing.");
        foreach (var actor in actors)
            ValidateRestShape(actor["pendingRetailPurchases"], typeof(List<RetailPurchaseRecoverySaveData>));
    }

    protected override void NormalizeRestorePayload(
        DungeonCharacterWorldSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(
        DungeonCharacterWorldSaveData payload)
    {
        saveService.ValidateRestorePayload(ResolveGrid(), payload);
    }

    protected override CharacterWorldRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterWorldSaveData source)
    {
        return saveService.PrepareRestoreCandidate(
            ResolveRestoreGrid(),
            source);
    }

    protected override void PublishRestoreCandidate(
        CharacterWorldRestoreCandidate candidate)
    {
        saveService.StageRestoreCandidate(candidate);
    }

    protected override void PublishRestoreCandidateProjection(
        DungeonCharacterWorldSaveData payload,
        CharacterWorldRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetCharacters(payload);

    private Grid ResolveRestoreGrid()
    {
        if (!restoreWorldCandidates.TryGetGrid(out Grid candidateGrid))
        {
            throw new InvalidOperationException(
                "Character restore requires the detached facility-world candidate grid.");
        }

        return candidateGrid;
    }

    private Grid ResolveGrid()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            throw new InvalidOperationException(
                "Cannot save or restore characters before the grid is initialized.");
        }

        return grid;
    }
}
