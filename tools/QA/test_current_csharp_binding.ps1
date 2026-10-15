param([Parameter(Mandatory=$true)][string]$OutputDirectory, [switch]$IncludeEditor,
    [switch]$CombatObserverProbe)
$ErrorActionPreference = 'Stop'
$bindingTimer = [Diagnostics.Stopwatch]::StartNew()
$bindingRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$bindingOutput = [IO.Path]::GetFullPath((Join-Path $bindingRoot $OutputDirectory))
if (-not $bindingOutput.StartsWith(($bindingRoot + '\Artifacts\QA\'), [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $bindingOutput)) { throw 'Create-only repository QA output required.' }
New-Item -ItemType Directory -Path $bindingOutput | Out-Null
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$bindingParse = [Microsoft.CodeAnalysis.CSharp.CSharpCommandLineParser].GetMethods() | Where-Object { $_.Name -eq 'Parse' -and $_.ReturnType.Name -eq 'CSharpCommandLineArguments' }
$bindingRspRoot = Join-Path $bindingRoot 'Library/Bee/artifacts/1900b0aE.dag'
if (-not (Test-Path -LiteralPath $bindingRspRoot)) { throw 'Existing local compiler references unavailable; no Unity is launched.' }
$bindingDefinitions = @(Get-ChildItem -LiteralPath (Join-Path $bindingRoot 'Assets/Scripts') -Recurse -Filter '*.asmdef' | ForEach-Object {
    $definition = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
    [pscustomobject]@{ name=$definition.name; directory=$_.DirectoryName; length=$_.DirectoryName.Length; references=@($definition.references) }
} | Sort-Object length -Descending)
$bindingReferences = @(Get-ChildItem -LiteralPath (Join-Path $bindingRoot 'Assets/Scripts') -Recurse -Filter '*.asmref' | ForEach-Object {
    $reference = (Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json).reference
    if ($reference.StartsWith('GUID:')) { throw 'Resolve GUID asmref ownership before this host compilation.' }
    [pscustomobject]@{ name=$reference; directory=$_.DirectoryName; length=$_.DirectoryName.Length }
})
$bindingDefinitions = @(($bindingDefinitions + $bindingReferences) | Sort-Object length -Descending)
$bindingSources = @{}
Get-ChildItem -LiteralPath (Join-Path $bindingRoot 'Assets/Scripts') -Recurse -Filter '*.cs' | ForEach-Object {
    $source = $_.FullName
    $owner = $bindingDefinitions | Where-Object { $source.StartsWith(($_.directory + '\'), [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    $name = if ($owner) { $owner.name } elseif ($source -match '\\Editor\\') { 'Assembly-CSharp-Editor' } else { 'Assembly-CSharp' }
    if (-not $bindingSources.ContainsKey($name)) { $bindingSources[$name] = [Collections.Generic.List[string]]::new() }
    $bindingSources[$name].Add($source)
}
$bindingArguments = @{}
foreach ($name in $bindingSources.Keys) {
    if (-not $IncludeEditor -and $name -match 'Editor') { continue }
    $rsp = Join-Path $bindingRspRoot ($name + '.rsp')
    if (-not (Test-Path -LiteralPath $rsp)) { throw ('No known compiler references for ' + $name) }
    $bindingArguments[$name] = $bindingParse.Invoke([Microsoft.CodeAnalysis.CSharp.CSharpCommandLineParser]::Default,
        [object[]]@([IO.File]::ReadAllLines($rsp), $bindingRoot, $null, $null))
}
$bindingCompiled = @{}
$bindingImages = [Collections.Generic.Dictionary[string,byte[]]]::new([StringComparer]::Ordinal)
$bindingProbeArguments = $bindingArguments['Assembly-CSharp']
$bindingMetadata = @{}
$bindingOwnedMetadata = [Collections.Generic.List[IDisposable]]::new()
$bindingResults = [Collections.Generic.List[object]]::new()
$bindingPins = [Collections.Generic.List[object]]::new()
$bindingReport = [ordered]@{ result='FAIL'; scope='Current repository C# semantic binding against installed Unity/package references, in-memory only'; Unity='NOT_RUN'; gameplay='NOT_RUN'; sourceGenerators='NOT_RUN'; assemblies=$bindingResults; pins=$bindingPins }
if ($CombatObserverProbe) { $bindingReport.managedObserverProbe = @{ result='NOT_RUN' } }
try {
    while ($bindingArguments.Count -gt 0) {
        $progressed = $false
        foreach ($name in @($bindingArguments.Keys | Sort-Object)) {
            $arguments = $bindingArguments[$name]
            $declared = @(($bindingDefinitions | Where-Object { $_.name -eq $name -and $_.PSObject.Properties.Name -contains 'references' } | Select-Object -First 1).references)
            $dependencies = @($arguments.MetadataReferences | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Reference) -replace '\.ref$','' }) + $declared
            if (@($dependencies | Where-Object { $_ -and $bindingArguments.ContainsKey($_) }).Count -gt 0) { continue }
            if ([Diagnostics.Process]::GetCurrentProcess().WorkingSet64 -gt 2GB) { throw 'Host compiler reached 2 GiB safety limit; stopped before next assembly.' }
            $trees = [Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
            $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($path in $bindingSources[$name]) { [void]$paths.Add($path) }
            # Keep plugin/third-party source entries from the known Unity compiler
            # response, while current scripts are classified by current asmdefs.
            foreach ($source in $arguments.SourceFiles) {
                $path = [IO.Path]::GetFullPath($source.Path)
                if (-not $path.StartsWith((Join-Path $bindingRoot 'Assets/Scripts'), [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $path)) { [void]$paths.Add($path) }
            }
            foreach ($path in ($paths | Sort-Object)) {
                $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($path), $arguments.ParseOptions, $path))
                $bindingPins.Add([pscustomobject]@{ assembly=$name; path=[IO.Path]::GetRelativePath($bindingRoot,$path); sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash })
            }
            $references = [Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
            $referenceNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($reference in $arguments.MetadataReferences) {
                $dependency = [IO.Path]::GetFileNameWithoutExtension($reference.Reference) -replace '\.ref$',''
                [void]$referenceNames.Add($dependency)
                if ($bindingCompiled.ContainsKey($dependency)) { $references.Add($bindingCompiled[$dependency].WithProperties($reference.Properties)); continue }
                $path = if ([IO.Path]::IsPathRooted($reference.Reference)) { $reference.Reference } else { [IO.Path]::GetFullPath((Join-Path $bindingRoot $reference.Reference)) }
                if (-not $bindingMetadata.ContainsKey($path)) { $bindingMetadata[$path] = [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($path) }
                $references.Add($bindingMetadata[$path].WithProperties($reference.Properties))
            }
            foreach ($dependency in $declared) {
                if ($dependency -and -not $referenceNames.Contains($dependency) -and $bindingCompiled.ContainsKey($dependency)) { $references.Add($bindingCompiled[$dependency]) }
            }
            $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($name, $trees, $references, $arguments.CompilationOptions)
            $stream = [IO.MemoryStream]::new()
            $emit = $compilation.Emit($stream)
            $errors = @($emit.Diagnostics | Where-Object Severity -eq Error | ForEach-Object ToString)
            $bindingResults.Add([pscustomobject]@{ assembly=$name; sourceCount=$trees.Count; success=$emit.Success; errors=$errors; warnings=@($emit.Diagnostics | Where-Object Severity -eq Warning).Count })
            Write-Output ($name + ': sources=' + $trees.Count + ' errors=' + $errors.Count)
            if (-not $emit.Success) { $stream.Dispose(); throw ($name + ': ' + ($errors | Select-Object -First 12 | Out-String)) }
            $stream.Position = 0
            if ($CombatObserverProbe) { $bindingImages.Add($name, $stream.ToArray()) }
            $metadata = [Microsoft.CodeAnalysis.AssemblyMetadata]::CreateFromStream($stream, [Reflection.PortableExecutable.PEStreamOptions]::PrefetchMetadata)
            $bindingOwnedMetadata.Add($metadata); $bindingOwnedMetadata.Add($stream)
            $bindingCompiled[$name] = $metadata.GetReference()
            $bindingArguments.Remove($name); $progressed = $true
            $compilation = $null; $trees = $null; [GC]::Collect()
        }
        if (-not $progressed) { throw ('Unresolved assembly dependencies: ' + ($bindingArguments.Keys -join ',')) }
    }
    if ($CombatObserverProbe) {
        # Run only managed observer/ledger logic. This is not a Unity process,
        # game-world execution, source-generator check or Unity serialization test.
        $probePath = Join-Path $bindingRoot 'Tools/QA/CombatActionObserverHostProbe.cs'
        $bindingPins.Add([pscustomobject]@{ assembly='CombatObserverHostProbe'; path=[IO.Path]::GetRelativePath($bindingRoot,$probePath); sha256=(Get-FileHash -LiteralPath $probePath -Algorithm SHA256).Hash })
        $probeTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($probePath), $bindingProbeArguments.ParseOptions, $probePath)
        $probeReferences = [Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
        foreach ($reference in $bindingCompiled.Values) { $probeReferences.Add($reference) }
        foreach ($reference in $bindingProbeArguments.MetadataReferences) {
            $dependency = [IO.Path]::GetFileNameWithoutExtension($reference.Reference) -replace '\.ref$',''
            if ($bindingCompiled.ContainsKey($dependency)) { continue }
            $path = if ([IO.Path]::IsPathRooted($reference.Reference)) { $reference.Reference } else { [IO.Path]::GetFullPath((Join-Path $bindingRoot $reference.Reference)) }
            $probeReferences.Add($bindingMetadata[$path].WithProperties($reference.Properties))
        }
        $probeCompilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('CombatObserverHostProbe', [Microsoft.CodeAnalysis.SyntaxTree[]]@($probeTree), $probeReferences, $bindingProbeArguments.CompilationOptions)
        $probeStream = [IO.MemoryStream]::new()
        try {
            $probeEmit = $probeCompilation.Emit($probeStream)
            if (-not $probeEmit.Success) { throw ($probeEmit.Diagnostics | Where-Object Severity -eq Error | Out-String) }
            $bindingImages.Add('CombatObserverHostProbe', $probeStream.ToArray())
        } finally { $probeStream.Dispose() }
        $probeFiles = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        foreach ($path in $bindingMetadata.Keys) {
            $name = [IO.Path]::GetFileNameWithoutExtension($path)
            if (-not $probeFiles.ContainsKey($name)) { $probeFiles.Add($name, $path) }
        }
        # The tested command owner implements the installed package's lifecycle
        # interfaces. Compile-only VContainer.ref.dll cannot be executed by .NET.
        # Resolve this one package explicitly; never load old project gameplay DLLs.
        $lifecycleReference = Join-Path $bindingRspRoot 'VContainer.ref.dll'
        $lifecycleRuntime = Join-Path $bindingRoot 'Library/ScriptAssemblies/VContainer.dll'
        if (-not (Test-Path -LiteralPath $lifecycleRuntime) -or
            [Reflection.AssemblyName]::GetAssemblyName($lifecycleReference).FullName -ne
            [Reflection.AssemblyName]::GetAssemblyName($lifecycleRuntime).FullName) {
            throw 'Installed lifecycle interface assembly does not match the known compile reference.'
        }
        $probeFiles.Add('VContainer', $lifecycleRuntime)
        $bindingReport.managedPackageRuntime = @{
            path = [IO.Path]::GetRelativePath($bindingRoot, $lifecycleRuntime)
            sha256 = (Get-FileHash -LiteralPath $lifecycleRuntime -Algorithm SHA256).Hash
            scope = 'Installed VContainer lifecycle interface definitions; entrypoint scheduling NOT_RUN'
        }
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
public sealed class CombatObserverManagedHost : AssemblyLoadContext
{
    private readonly Dictionary<string, byte[]> images;
    private readonly Dictionary<string, string> files;
    private CombatObserverManagedHost(Dictionary<string, byte[]> images, Dictionary<string, string> files)
        : base("CombatObserverManagedHost", true) { this.images = images; this.files = files; }
    protected override Assembly Load(AssemblyName name)
    {
        if (images.TryGetValue(name.Name, out var bytes))
        { using var stream = new MemoryStream(bytes, false); return LoadFromStream(stream); }
        // Runtime framework and Newtonsoft are supplied by the host, not Unity's
        // compile-only netstandard reference assemblies.
        if (name.Name == "mscorlib" || name.Name == "netstandard" || name.Name == "System"
            || name.Name.StartsWith("System.", StringComparison.Ordinal)
            || name.Name == "Microsoft.CSharp" || name.Name == "Newtonsoft.Json") return null;
        return files.TryGetValue(name.Name, out var file) ? LoadFromAssemblyPath(file) : null;
    }
    protected override IntPtr LoadUnmanagedDll(string name) => throw new DllNotFoundException(
        "Native libraries are forbidden in the managed combat observer probe: " + name);
    public static string Run(Dictionary<string, byte[]> images, Dictionary<string, string> files)
    {
        var context = new CombatObserverManagedHost(images, files);
        try {
            var probe = context.LoadFromAssemblyName(new AssemblyName("CombatObserverHostProbe"));
            return (string)probe.GetType("CombatActionObserverHostProbe", true).GetMethod("Run").Invoke(null, null);
        } finally { context.Unload(); }
    }
}
'@
        $bindingReport.managedObserverProbe = [CombatObserverManagedHost]::Run($bindingImages, $probeFiles) | ConvertFrom-Json
        if ($bindingReport.managedObserverProbe.result -ne 'PASS') { throw 'Managed combat observer probe did not report PASS.' }
        Write-Output ('Managed combat observer probe: ' + ($bindingReport.managedObserverProbe | ConvertTo-Json -Compress -Depth 6))
    }
    $bindingReport.result = 'PASS'
}
catch { $bindingReport.failure = $_.Exception.ToString(); throw }
finally {
    $bindingReport.elapsedSeconds = [Math]::Round($bindingTimer.Elapsed.TotalSeconds,3)
    $bindingReport.hostPeakWorkingSetBytes = [Diagnostics.Process]::GetCurrentProcess().PeakWorkingSet64
    $bindingReport.helperSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    $file = [IO.File]::Open((Join-Path $bindingOutput 'binding.json'), [IO.FileMode]::CreateNew)
    try { $bytes = [Text.Encoding]::UTF8.GetBytes(($bindingReport | ConvertTo-Json -Depth 8)); $file.Write($bytes,0,$bytes.Length) } finally { $file.Dispose() }
    foreach ($value in $bindingOwnedMetadata) { $value.Dispose() }
}
