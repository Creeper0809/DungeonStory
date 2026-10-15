param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'

$actionRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$actionOutput = [IO.Path]::GetFullPath((Join-Path $actionRoot $OutputDirectory))
if (-not $actionOutput.StartsWith(($actionRoot + '\Artifacts\QA\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence must be inside this repository Artifacts/QA.'
}
if (Test-Path -LiteralPath $actionOutput) {
    throw 'Create-only evidence directory already exists.'
}
New-Item -ItemType Directory -Path $actionOutput | Out-Null

Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp

$actionPaths = @(
    'Assets/Scripts/Services/Foundation/Events/DomainFactValueSyntax.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactContracts.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactDispatcher.cs',
    'Assets/Scripts/Services/Foundation/PersistentEntityIds.cs',
    'Assets/Scripts/Models/Combat/Core/CombatActionEvidence.cs',
    'Assets/Scripts/Models/Combat/Core/CombatActionFact.cs',
    'Assets/Scripts/Models/Combat/Core/CombatActionPendingState.cs'
)
$actionUsings = [Collections.Generic.HashSet[string]]::new()
$actionMembers = [Collections.Generic.List[string]]::new()
$actionPins = [Collections.Generic.List[object]]::new()
$actionOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)

function Read-ActionSource([string]$RelativePath) {
    $absolute = Join-Path $actionRoot $RelativePath
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($absolute),
        $actionOptions)
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq Error)
    if ($errors.Count) {
        throw ($errors -join [Environment]::NewLine)
    }
    $actionPins.Add([pscustomobject]@{
        path = $RelativePath
        sha256 = (Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash
    })
    return $tree.GetRoot()
}

foreach ($path in $actionPaths) {
    $root = Read-ActionSource $path
    foreach ($using in $root.Usings) {
        [void]$actionUsings.Add($using.ToString())
    }
    foreach ($member in $root.Members) {
        $actionMembers.Add($member.ToString())
    }
}

$combatRoot = Read-ActionSource 'Assets/Scripts/Models/Combat/Core/CombatModels.cs'
[void]$actionUsings.Add('using UnityEngine.Scripting.APIUpdating;')
foreach ($name in @(
    'CombatDamageType',
    'CombatFireMode',
    'CombatBodyPart',
    'CombatSpecialEffectFlags')) {
    $member = @($combatRoot.Members | Where-Object { $_.Identifier.ValueText -eq $name })
    if ($member.Count -ne 1) {
        throw "Required exact combat enum missing: $name"
    }
    $actionMembers.Add($member[0].ToString())
}

$originRoot = Read-ActionSource 'Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs'
$origin = @($originRoot.Members | Where-Object {
    $_.Identifier.ValueText -eq 'CharacterCommandOrigin'
})
if ($origin.Count -ne 1) {
    throw 'Required exact CharacterCommandOrigin enum missing.'
}
$actionMembers.Add($origin[0].ToString())

$actionMembers.Add(@'
// Host-boundary signature shims only. The validation, cloning, identity and
// pending-state behavior under test all come from the pinned production files.
// Full CombatModels pulls Unity math and equipment graphs into this pure host;
// these shapes exist only so the production evidence capture overloads compile.
namespace UnityEngine.Scripting.APIUpdating
{
    public sealed class MovedFromAttribute : Attribute
    {
        public MovedFromAttribute(bool value, string sourceAssembly = null) { }
    }
}

public sealed class CombatWeaponSnapshot
{
    public string DefinitionId => string.Empty;
    public string InstanceId => string.Empty;
    public string AmmunitionItemId => string.Empty;
    public int LoadedAmmo => 0;
}

public readonly struct CombatAttackRequest
{
    public CombatWeaponSnapshot Weapon => null;
    public CombatFireMode FireMode => default;
}

public readonly struct CombatArmorDurabilityHit
{
    public string InstanceId => string.Empty;
    public float Damage => 0f;
}

public readonly struct CombatAttackResult
{
    public int AmmunitionConsumed => 0;
    public string AmmunitionItemId => string.Empty;
    public string CoverSourceId => string.Empty;
    public bool Executed => false;
    public bool Hit => false;
    public bool CoverBlocked => false;
    public bool ShieldBlocked => false;
    public bool Evaded => false;
    public bool Nonlethal => false;
    public CombatSpecialEffectFlags SpecialEffects => default;
    public float StatusPotency => 0f;
    public float CoverDamage => 0f;
    public int StatusTurns => 0;
    public CombatBodyPart BodyPart => default;
    public float AppliedDamage => 0f;
    public float Suppression => 0f;
    public IReadOnlyList<CombatArmorDurabilityHit> ArmorDurabilityHits =>
        Array.Empty<CombatArmorDurabilityHit>();
}

public static class CombatActionFactHostSmoke
{
    private static readonly List<string> Checks = new();

    public static string[] Run()
    {
        VerifyFrozenGraph();
        VerifyProposalMatching();
        VerifyAppliedHealthRules();
        VerifyInvalidValues();
        VerifyPendingDuplicatesAndRoundTrip();
        VerifyPendingCapacityAndRemoval();
        var absent = Data(includeConflict: false);
        var restoredAbsent = Newtonsoft.Json.JsonConvert.DeserializeObject<CombatActionFactData>(Newtonsoft.Json.JsonConvert.SerializeObject(absent));
        Check(!new CombatActionFact(restoredAbsent).Snapshot().hasConflict && restoredAbsent.conflict.IsEmpty,
            "absent conflict round trips as explicit flag and empty inline value");
        var hidden = Data(); hidden.hasConflict = false;
        Check(Rejected(() => new CombatActionFact(hidden)), "hidden nonempty conflict rejected");
        return Checks.ToArray();
    }

    private static void VerifyFrozenGraph()
    {
        CombatActionFactData source = Data();
        var fact = new CombatActionFact(source);
        source.attacker.name = "caller-attacker";
        source.target.name = "caller-target";
        source.evidence.weaponDefinitionId = "caller-weapon";
        source.evidence.armorHits[0].instanceId = "caller-armor";
        source.evidence.armorHits[0].damage = 99f;
        source.conflict.instigator.name = "caller-instigator";
        source.conflict.severity = 99f;

        CombatActionFactData first = fact.Snapshot();
        first.evidence.armorHits[0].damage = 77f;
        first.conflict.target.name = "snapshot-target";
        CombatActionFactData frozen = fact.Snapshot();
        Check(
            frozen.attacker.name == "공격자"
            && frozen.target.name == "방어자"
            && frozen.evidence.weaponDefinitionId == "weapon:test"
            && frozen.evidence.armorHits[0].instanceId == "armor:test"
            && frozen.evidence.armorHits[0].damage == 1.25f
            && frozen.conflict.instigator.name == "공격자"
            && frozen.conflict.target.name == "방어자"
            && frozen.conflict.severity == 0.75f,
            "fact freezes caller and returned nested graph mutations");
    }

    private static void VerifyProposalMatching()
    {
        CombatActionFactData data = Data();
        var actual = new CombatActionFact(data);
        var exact = Proposal(data);
        Check(actual.MatchesProposal(exact),
            "proposal matches exact operation revision day target and parent");

        CombatActionFactData revision = data.Clone();
        revision.attackRevision++;
        CombatActionFactData day = data.Clone();
        day.absoluteDay++;
        CombatActionFactData target = data.Clone();
        target.target.id = "character:other";
        CombatActionFactData parent = data.Clone();
        parent.conflict.committedMoodDelta -= 0.25f;
        Check(
            !actual.MatchesProposal(Proposal(revision))
            && !actual.MatchesProposal(Proposal(day))
            && !actual.MatchesProposal(Proposal(target))
            && !actual.MatchesProposal(Proposal(parent)),
            "proposal rejects revision day target and frozen-parent drift");
    }

    private static void VerifyAppliedHealthRules()
    {
        Check(new CombatActionFact(Data()).Snapshot().healthAfter == 7f,
            "executed hit accepts finite decreasing health snapshot");

        CombatActionFactData increased = Data();
        increased.healthAfter = increased.healthBefore + 1f;
        Check(Rejected(() => new CombatActionFact(increased)),
            "actual combat fact rejects health increase");

        CombatActionFactData noHit = Data();
        noHit.evidence.hit = false;
        noHit.evidence.appliedDamage = 0f;
        noHit.healthAfter = noHit.healthBefore;
        Check(new CombatActionFact(noHit).Snapshot().healthAfter == noHit.healthBefore,
            "non-damage fact accepts unchanged health");

        CombatActionFactData missedDamage = noHit.Clone();
        missedDamage.healthAfter--;
        CombatActionFactData coveredDamage = Data();
        coveredDamage.evidence.coverBlocked = true;
        Check(
            Rejected(() => new CombatActionFact(missedDamage))
            && Rejected(() => new CombatActionFact(coveredDamage)),
            "missed or cover-blocked fact cannot reduce health");
    }

    private static void VerifyInvalidValues()
    {
        CombatActionFactData invalidEnum = Data();
        invalidEnum.evidence.damageType = (CombatDamageType)999;
        CombatActionFactData invalidOrigin = Data();
        invalidOrigin.origin = (CharacterCommandOrigin)999;
        Check(
            Rejected(() => new CombatActionFact(invalidEnum))
            && Rejected(() => new CombatActionFact(invalidOrigin)),
            "invalid evidence and origin enums rejected");

        CombatActionFactData nanHealth = Data();
        nanHealth.healthBefore = float.NaN;
        CombatActionFactData nanEvidence = Data();
        nanEvidence.evidence.appliedDamage = float.NaN;
        Check(
            Rejected(() => new CombatActionFact(nanHealth))
            && Rejected(() => new CombatActionFact(nanEvidence)),
            "NaN health and evidence rejected");

        CombatActionFactData ammunition = Data();
        ammunition.evidence.ammunitionAfter++;
        Check(Rejected(() => new CombatActionFact(ammunition)),
            "ammunition arithmetic mismatch rejected");
    }

    private static void VerifyPendingDuplicatesAndRoundTrip()
    {
        var state = new CombatActionPendingState();
        CombatActionFactData first = Data();
        state.Add(Pending("publication:first", first));

        CombatActionFactData sameIdentity = Data();
        sameIdentity.healthAfter = 6f;
        CombatActionFactData secondIdentity = Data("combat:attack:2", 8);
        Check(
            Rejected(() => state.Add(Pending("publication:other", sameIdentity)))
            && Rejected(() => state.Add(Pending("publication:first", secondIdentity))),
            "pending state rejects duplicate operation identity and publication key");

        state.Add(Pending("publication:second", secondIdentity));
        CombatActionPendingSaveData captured = state.Capture();
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(captured);
        CombatActionPendingSaveData decoded =
            Newtonsoft.Json.JsonConvert.DeserializeObject<CombatActionPendingSaveData>(json);
        CombatActionPendingState restored = CombatActionPendingState.Restore(decoded);
        CombatActionPendingSaveData roundTrip = restored.Capture();
        Check(
            roundTrip.version == CombatActionPendingSaveData.CurrentVersion
            && roundTrip.pending.Count == 2
            && new CombatActionFact(roundTrip.pending[0].fact).Identity == "combat:attack:1:7"
            && roundTrip.pending[0].fact.evidence.armorHits[0].instanceId == "armor:test"
            && roundTrip.pending[0].fact.evidence.ammunitionAfter == 4
            && roundTrip.pending[0].fact.conflict.conflictId == "conflict:test"
            && new CombatActionFact(roundTrip.pending[1].fact).Identity == "combat:attack:2:8",
            "Newtonsoft pending round-trip preserves identities evidence and parent");

        decoded.pending[0].fact.evidence.armorHits[0].damage = 88f;
        captured.pending[0].fact.conflict.severity = 88f;
        CombatActionPendingSaveData isolated = restored.Capture();
        Check(
            isolated.pending[0].fact.evidence.armorHits[0].damage == 1.25f
            && isolated.pending[0].fact.conflict.severity == 0.75f,
            "restored pending state owns deep clones");
    }

    private static void VerifyPendingCapacityAndRemoval()
    {
        var state = new CombatActionPendingState();
        for (int index = 0; index < CombatActionPendingState.Capacity; index++)
        {
            state.Add(Pending(
                "publication:capacity:" + index,
                Data("combat:capacity:" + index, index, includeConflict: false)));
        }
        Check(
            state.Count == CombatActionPendingState.Capacity
            && Rejected(() => state.Add(Pending(
                "publication:overflow",
                Data("combat:capacity:overflow", 999, includeConflict: false)))),
            "pending state enforces exact capacity");

        state.Remove("publication:capacity:128");
        state.Add(Pending(
            "publication:replacement",
            Data("combat:capacity:replacement", 1000, includeConflict: false)));
        CombatActionPendingSaveData saved = state.Capture();
        Check(
            state.Count == CombatActionPendingState.Capacity
            && !saved.pending.Any(value => value.publicationKey == "publication:capacity:128")
            && saved.pending.Count(value => value.publicationKey == "publication:replacement") == 1,
            "remove frees one slot and replacement remains unique");
    }

    private static CombatActionFact Proposal(CombatActionFactData data) =>
        CombatActionFact.Proposal(
            data.operationId,
            data.attackRevision,
            data.absoluteDay,
            data.target,
            data.hasConflict ? data.conflict : null);

    private static CombatActionPendingData Pending(
        string key,
        CombatActionFactData fact) => new()
    {
        publicationKey = key,
        fact = fact
    };

    private static CombatActionFactData Data(
        string operationId = "combat:attack:1",
        long revision = 7,
        bool includeConflict = true)
    {
        var attacker = Participant("character:attacker", "공격자");
        var target = Participant("character:target", "방어자");
        return new CombatActionFactData
        {
            operationId = operationId,
            attackRevision = revision,
            absoluteDay = 5,
            attacker = attacker,
            target = target,
            targetX = 4,
            targetY = 9,
            evidence = new CombatActionEvidenceData
            {
                requestKnown = true,
                weaponDefinitionId = "weapon:test",
                weaponInstanceId = "item-instance:test",
                ammunitionItemId = "ammo:test",
                coverSourceId = string.Empty,
                damageType = CombatDamageType.Pierce,
                fireMode = CombatFireMode.Aimed,
                ammunitionBefore = 5,
                ammunitionConsumed = 1,
                ammunitionAfter = 4,
                executed = true,
                hit = true,
                coverBlocked = false,
                shieldBlocked = false,
                evaded = false,
                nonlethal = false,
                specialEffects = CombatSpecialEffectFlags.None,
                statusPotency = 0f,
                coverDamage = 0f,
                statusTurns = 0,
                bodyPart = CombatBodyPart.Torso,
                appliedDamage = 3f,
                suppression = 0.5f,
                armorHits = new List<CombatArmorHitFactData>
                {
                    new() { instanceId = "armor:test", damage = 1.25f }
                }
            },
            healthBefore = 10f,
            maxHealthBefore = 10f,
            healthAfter = 7f,
            maxHealthAfter = 10f,
            downedBefore = false,
            downedAfter = false,
            damageSource = "weapon:test",
            origin = CharacterCommandOrigin.DirectPlayerOrder,
            hasConflict = includeConflict,
            conflict = includeConflict
                ? new CombatConflictFactData
                {
                    operationId = operationId,
                    instigator = attacker.Clone(),
                    target = target.Clone(),
                    conflictId = "conflict:test",
                    severity = 0.75f,
                    committedMoodDelta = -1.5f,
                    origin = CharacterCommandOrigin.DirectPlayerOrder,
                    absoluteDay = 5,
                    alreadyPublished = false
                }
                : new CombatConflictFactData()
        };
    }

    private static CombatFactParticipantData Participant(
        string id,
        string name) => new()
    {
        kind = CombatFactParticipantKind.Character,
        id = id,
        name = name
    };

    private static bool Rejected(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        Checks.Add(name);
    }
}
'@)

$actionSource = ($actionUsings -join [Environment]::NewLine) + [Environment]::NewLine + ($actionMembers -join [Environment]::NewLine)
$actionReferences = @(
    Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' |
        ForEach-Object FullName
) + @([Newtonsoft.Json.JsonConvert].Assembly.Location)
$actionResult = [ordered]@{
    result = 'FAIL'
    scope = 'Pinned production CombatActionEvidence/Fact/PendingState execution with exact Foundation identity syntax and host Newtonsoft JSON. Combat runtime, native observer/ledger/preflight, save service and Unity are NOT_RUN.'
    hostBoundaryShims = @(
        'CombatWeaponSnapshot',
        'CombatAttackRequest',
        'CombatArmorDurabilityHit',
        'CombatAttackResult',
        'Unity MovedFrom metadata attribute'
    )
    checks = @()
    pins = $actionPins
}

try {
    # PowerShell supplies Newtonsoft built for .NET 6 and already loads it on
    # .NET 10. Suppress only this host assembly-unification warning.
    $actionCompilation = @{
        TypeDefinition = $actionSource
        ReferencedAssemblies = $actionReferences
        CompilerOptions = @('/langversion:9', '/nowarn:1701')
    }
    Add-Type @actionCompilation
    $actionResult.checks = @([CombatActionFactHostSmoke]::Run())
    $actionResult.result = 'PASS'
}
catch {
    $actionResult.failure = $_.Exception.ToString()
    throw
}
finally {
    $actionResult.helperSha256 = (
        Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    $actionEvidence = Join-Path $actionOutput 'execution.json'
    $actionStream = [IO.File]::Open(
        $actionEvidence,
        [IO.FileMode]::CreateNew)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(
            ($actionResult | ConvertTo-Json -Depth 8))
        $actionStream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $actionStream.Dispose()
    }
}

[pscustomobject]@{
    result = $actionResult.result
    checks = $actionResult.checks.Count
    evidence = $actionEvidence
} | ConvertTo-Json
