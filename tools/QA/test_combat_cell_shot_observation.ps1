param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$shotRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$shotOutput = [IO.Path]::GetFullPath((Join-Path $shotRoot $OutputDirectory))
if (-not $shotOutput.StartsWith(($shotRoot + '\Artifacts\QA\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be inside this repository Artifacts/QA.' }
if (Test-Path -LiteralPath $shotOutput) { throw 'Create-only evidence directory already exists.' }
New-Item -ItemType Directory -Path $shotOutput | Out-Null
Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$shotPaths = @(
    'Assets/Scripts/Services/Foundation/Events/DomainFactValueSyntax.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactContracts.cs',
    'Assets/Scripts/Services/Foundation/Events/DomainFactDispatcher.cs',
    'Assets/Scripts/Services/Foundation/PersistentEntityIds.cs',
    'Assets/Scripts/Services/Foundation/Save/DungeonGameRestoreReport.cs',
    'Assets/Scripts/Models/Combat/Core/CombatCellShotFact.cs',
    'Assets/Scripts/Models/Combat/Core/CombatCellShotJournal.cs',
    'Assets/Scripts/Models/Combat/Core/CharacterCombatCommandModels.cs',
    'Assets/Scripts/Models/Combat/Core/CharacterCombatCommandAggregateState.cs',
    'Assets/Scripts/Models/Combat/Core/CharacterCombatCommandPersistence.cs',
    'Assets/Scripts/Models/Combat/Core/CharacterCombatCommandSaveValidation.cs',
    'Assets/Scripts/Services/Combat/CombatCellShotSaveJson.cs'
)
$shotUsings = [Collections.Generic.HashSet[string]]::new()
$shotMembers = [Collections.Generic.List[string]]::new()
$shotPins = [Collections.Generic.List[object]]::new()
$shotOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
function Read-ShotSource([string]$RelativePath) {
    $absolute = Join-Path $shotRoot $RelativePath
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($absolute), $shotOptions)
    $errors = @($tree.GetDiagnostics() | Where-Object Severity -eq Error)
    if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
    $shotPins.Add([pscustomobject]@{ path = $RelativePath; sha256 = (Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash })
    return $tree.GetRoot()
}
foreach ($path in $shotPaths) {
    $root = Read-ShotSource $path
    foreach ($using in $root.Usings) { [void]$shotUsings.Add($using.ToString()) }
    foreach ($member in $root.Members) { $shotMembers.Add($member.ToString()) }
}
$enumRoot = Read-ShotSource 'Assets/Scripts/Models/Combat/Core/CombatModels.cs'
foreach ($name in @('CombatDamageType','CombatFireMode')) {
    $member = @($enumRoot.Members | Where-Object { $_.Identifier.ValueText -eq $name })
    if ($member.Count -ne 1) { throw 'Required exact combat enum missing.' }
    $shotMembers.Add($member[0].ToString())
}
$shotMembers.Add(@'
// Engine metadata/coordinate shims only. No gameplay or observation behavior is replaced here.
namespace UnityEngine { public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x=x; this.y=y; } } }
namespace UnityEngine.Scripting.APIUpdating { public sealed class MovedFromAttribute : Attribute { public MovedFromAttribute(bool value, string sourceAssembly = null) {} } }
public static class CombatCellShotHostSmoke
{
    private static readonly List<string> checks = new();
    public static string[] Run()
    {
        var source = Data(); var fact = new CombatCellShotFact(source);
        source.manaAfter = 999f; fact.Snapshot().actorName = "changed";
        Check(fact.Snapshot().manaAfter == 17f && fact.Snapshot().actorName == "사수", "fact snapshots are isolated");
        var bad = Data(); bad.ammunitionAfter = 2;
        Check(Rejected(() => new CombatCellShotFact(bad)), "ammunition mismatch rejected");
        bad = Data(); bad.manaAfter = float.NaN;
        Check(Rejected(() => new CombatCellShotFact(bad)), "nonfinite mana rejected");
        bad = Data(); bad.attackRevision = int.MaxValue;
        Check(Rejected(() => new CombatCellShotFact(bad)), "revision overflow rejected");
        bad = Data(); bad.manaBefore = 2.99995f; bad.manaAfter = 0f; bad.manaSpent = bad.manaBefore;
        Check(new CombatCellShotFact(bad).Snapshot().manaSpent < bad.manaCost, "clamped actual mana spend remains distinct from requested cost");
        using (var dispatcher = new DomainFactDispatcher())
        {
            int effects = 0;
            Check(!CombatCellShotJournal.TryApply(new(), dispatcher, fact, () => { effects++; return ""; }, () => true, () => effects--, out _)
                && effects == 0, "missing observer prevents effects");
        }
        foreach (string mode in new[] { "delivered", "reject", "throw-before", "throw-after", "wrong-proof" }) Exercise(mode);
        using (var dispatcher = new DomainFactDispatcher())
        {
            var observer = new Observer("delivered"); using var binding = dispatcher.Subscribe("test:shot", observer);
            int effects = 0; var pending = new List<CombatCellShotPendingData>();
            Check(!CombatCellShotJournal.TryApply(pending, dispatcher, fact, () => { effects++; return "failed"; }, () => true,
                () => effects--, out _) && effects == 0 && pending.Count == 0 && observer.Commits == 0, "partial mechanical rejection rolls back before publication");
            Check(!CombatCellShotJournal.TryApply(pending, dispatcher, fact, () => { effects++; return ""; }, () => false,
                () => effects--, out _) && effects == 0 && pending.Count == 0 && observer.Commits == 0, "actual state mismatch never publishes");
            Check(dispatcher.ActiveLeaseCount == 0, "failed mechanical reservations released");
        }
        return checks.ToArray();
    }
    private static void Exercise(string mode)
    {
        var state = new CharacterCombatCommandAggregateState { CommandSequence = 1 };
        state.CommandRevisions.Add("character:shot", 3);
        int ammunition = 4; float mana = 20f; int effects = 0;
        var fact = new CombatCellShotFact(Data());
        var committed = new Dictionary<string,string>(StringComparer.Ordinal);
        using var dispatcher = new DomainFactDispatcher();
        var observer = new Observer(mode, committed); using var binding = dispatcher.Subscribe("test:shot", observer);
        bool applied = CombatCellShotJournal.TryApply(state.PendingCellShots, dispatcher, fact,
            () => { effects++; ammunition--; mana -= 3f; state.CommandRevisions["character:shot"]++; return ""; },
            () => ammunition == 3 && mana == 17f,
            () => { effects--; ammunition++; mana += 3f; state.CommandRevisions["character:shot"]--; }, out string failure);
        if (mode == "reject")
        {
            Check(!applied && effects == 0 && ammunition == 4 && mana == 20f && state.PendingCellShots.Count == 0,
                "confirmed rejection restores resources and revision");
            return;
        }
        Check(applied && effects == 1 && ammunition == 3 && mana == 17f && state.CommandRevisions["character:shot"] == 4,
            mode + ": mechanical state applied once");
        if (mode == "delivered")
        { Check(state.PendingCellShots.Count == 0 && committed.Count == 1, "exact delivery clears pending"); return; }
        Check(state.PendingCellShots.Count == 1 && failure.Length > 0, mode + ": pending fact retained");
        Check(!CombatCellShotJournal.TryApply(state.PendingCellShots, dispatcher, fact, () => { effects++; return ""; }, () => true,
            () => effects--, out _) && effects == 1, mode + ": repeated pending operation cannot repeat effect");
        // Commands may be cancelled/replaced. The aggregate outbox, not a command's lifetime, retains delivery.
        state.Commands.Clear(); state.CombatStance.Clear();
        var saved = CharacterCombatCommandPersistence.Capture(state);
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(saved);
        CombatCellShotSaveJson.Validate(json);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterCombatCommandSaveData>(json);
        var report = new DungeonGameRestoreReport(); CharacterCombatCommandSaveValidation.Validate(restored, report);
        Check(report.Success && restored.commands.Count == 0 && restored.pendingCellShots.Count == 1,
            mode + ": JSON roundtrip survives command cancellation");
        var candidate = CharacterCombatCommandSaveValidation.CreateState(restored);
        restored.pendingCellShots[0].fact.actorName = "mutated outside";
        Check(candidate.PendingCellShots[0].fact.actorName == "사수", mode + ": candidate clone isolated");
        var clone = candidate.Clone(); clone.PendingCellShots[0].fact.actorName = "different";
        Check(candidate.PendingCellShots[0].fact.actorName == "사수", mode + ": aggregate copy isolated");
        using var resumedDispatcher = new DomainFactDispatcher();
        var resumedObserver = new Observer("delivered", committed);
        using var resumedBinding = resumedDispatcher.Subscribe("test:shot", resumedObserver);
        Check(CombatCellShotJournal.Resume(candidate.PendingCellShots, resumedDispatcher, out _)
            && candidate.PendingCellShots.Count == 0 && committed.Count == 1 && effects == 1 && ammunition == 3 && mana == 17f,
            mode + ": fresh owner recovery only publishes frozen fact");
        Check(CombatCellShotJournal.Resume(candidate.PendingCellShots, resumedDispatcher, out _)
            && resumedObserver.Commits == 1 && committed.Count == 1, mode + ": recovery repeated without duplicate");
        var corrupt = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterCombatCommandSaveData>(json);
        corrupt.revisions[0].revision = 3;
        report = new DungeonGameRestoreReport(); CharacterCombatCommandSaveValidation.Validate(corrupt, report);
        Check(!report.Success, mode + ": unapplied revision rejected");
        var raw = Newtonsoft.Json.Linq.JObject.Parse(json); raw.Remove("pendingCellShots");
        Check(Rejected(() => CombatCellShotSaveJson.Validate(raw.ToString())), mode + ": missing journal schema rejected");
    }
    private static CombatCellShotFactData Data() => new()
    {
        commandId = "combat-command:1", attackRevision = 3, actorId = "character:shot", actorName = "사수", absoluteDay = 4,
        targetX = 2, targetY = 3, weaponDefinitionId = "weapon:rifle", weaponInstanceId = "item-instance:rifle",
        damageType = CombatDamageType.Pierce, fireMode = CombatFireMode.Aimed, requiresAmmunition = true,
        ammunitionItemId = "ammo:rifle", ammunitionBefore = 4, ammunitionConsumed = 1, ammunitionAfter = 3,
        manaBefore = 20f, manaCost = 3f, manaSpent = 3f, manaAfter = 17f
    };
    private static bool Rejected(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidOperationException) { return true; } }
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
    private sealed class Observer : IDurableDomainFactObserver<CombatCellShotFact>, IDurableDomainFactRecoveryObserver<CombatCellShotFact>, IDomainFactCommitGroup
    {
        private readonly string mode;
        private readonly Dictionary<string,string> committed;
        public int Commits;
        public Observer(string mode, Dictionary<string,string> committed = null) { this.mode = mode; this.committed = committed ?? new(); }
        private static string Key(CombatCellShotFact fact) => "test:cell|" + fact.OperationId + "|1|0";
        public bool TryReserve(in CombatCellShotFact fact, out object reservation, out string failure)
        { reservation = new Reservation(Key(fact)); failure = ""; return true; }
        public bool TryPrepare(in CombatCellShotFact fact, object reservation, out IDomainFactPreparedPublication publication, out string failure)
        { publication = new Publication(this, fact, ((Reservation)reservation).PublicationKey); failure = ""; return true; }
        public bool TryPrepareReplay(string key, in CombatCellShotFact fact, out IDomainFactPreparedPublication publication, out string failure)
        { publication = null; failure = "identity mismatch"; if (key != Key(fact)) return false; publication = new Publication(this, fact, key); failure = ""; return true; }
        public DomainFactDelivery Replay(in CombatCellShotFact fact) => DomainFactDelivery.Reject("saved key required");
        public void Cancel(object reservation) {}
        public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications, DomainFactDelivery[] results, out string failure)
        {
            Commits++; failure = "";
            if (mode == "throw-before") throw new InvalidOperationException("before commit");
            if (mode == "reject") { results[0] = DomainFactDelivery.Reject("known reject"); failure = "known reject"; return false; }
            var value = (Publication)publications[0];
            string snapshot = Newtonsoft.Json.JsonConvert.SerializeObject(value.Fact.Snapshot());
            if (committed.TryGetValue(value.OperationKey, out string existing) && existing != snapshot) throw new InvalidOperationException("canonical mismatch");
            committed[value.OperationKey] = snapshot;
            if (mode == "throw-after") throw new InvalidOperationException("after commit");
            results[0] = new DomainFactDelivery(DomainFactDeliveryPhase.Delivered,
                mode == "wrong-proof" ? "wrong" : value.OperationKey, "test/1", new string('a',64), "");
            return true;
        }
        private sealed class Reservation : IDomainFactReservedIdentity { public Reservation(string key) { PublicationKey = key; } public string PublicationKey { get; } }
        private sealed class Publication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
        {
            public Publication(Observer group, CombatCellShotFact fact, string key) { CommitGroup = group; Fact = fact; OperationKey = key; }
            public CombatCellShotFact Fact { get; }
            public IDomainFactCommitGroup CommitGroup { get; }
            public string OperationKey { get; }
            public long OwnerRevision => 1;
            public bool IsReplay => false;
            public void Cancel() {}
        }
    }
}
'@)
$shotSource = ($shotUsings -join [Environment]::NewLine) + [Environment]::NewLine + ($shotMembers -join [Environment]::NewLine)
$shotReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName) + @([Newtonsoft.Json.JsonConvert].Assembly.Location)
$shotResult = [ordered]@{ result = 'FAIL'; scope = 'Exact engine-free fact/journal/dispatcher/aggregate/save validation and Newtonsoft JSON execution. Publication/commit and mechanical resource boundaries are labelled doubles; Unity CharacterCombatCommandRuntime, native ledger/observer/preflight and whole-save NOT_RUN.'; checks = @(); pins = $shotPins }
try {
    # PowerShell supplies Newtonsoft built for .NET 6 and already loads it on
    # .NET 10. Suppress only this host assembly-unification warning, not source diagnostics.
    Add-Type -TypeDefinition $shotSource -ReferencedAssemblies $shotReferences -CompilerOptions '/langversion:9','/nowarn:1701'
    $shotResult.checks = @([CombatCellShotHostSmoke]::Run())
    $shotResult.result = 'PASS'
}
catch { $shotResult.failure = $_.Exception.ToString(); throw }
finally {
    $shotResult.helperSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    $shotEvidence = Join-Path $shotOutput 'execution.json'
    $shotStream = [IO.File]::Open($shotEvidence, [IO.FileMode]::CreateNew)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($shotResult | ConvertTo-Json -Depth 7))
        $shotStream.Write($bytes, 0, $bytes.Length)
    } finally { $shotStream.Dispose() }
}
[pscustomobject]@{ result=$shotResult.result; checks=$shotResult.checks.Count; evidence=$shotEvidence } | ConvertTo-Json
