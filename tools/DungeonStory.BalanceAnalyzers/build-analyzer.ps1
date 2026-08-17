param(
    [string]$UnityEditorData = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'DungeonStoryBalanceAnalyzer.cs'
$output = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $PSScriptRoot '..\..\Assets\Analyzers\DungeonStory.BalanceAnalyzers.dll'
} else {
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
}
$roslyn = Join-Path $UnityEditorData 'DotNetSdkRoslyn'
$compiler = Join-Path $roslyn 'csc.dll'
$runtimeDirectory = Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1

if (-not (Test-Path -LiteralPath $compiler)) { throw "Missing Unity compiler: $compiler" }
if ($null -eq $runtimeDirectory) { throw 'Missing Unity .NET runtime reference directory.' }
if (-not (Test-Path -LiteralPath $source)) { throw "Missing analyzer source: $source" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$frameworkReferences = @(
    'System.Private.CoreLib.dll',
    'System.Runtime.dll',
    'netstandard.dll',
    'System.Collections.dll',
    'System.Collections.Immutable.dll',
    'System.Linq.dll',
    'System.Linq.Expressions.dll',
    'System.Runtime.Extensions.dll',
    'System.Threading.dll',
    'System.Threading.Tasks.dll',
    'System.Memory.dll'
)
$references = $frameworkReferences | ForEach-Object {
    $path = Join-Path $runtimeDirectory.FullName $_
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing framework reference: $path" }
    "/reference:$path"
}
& dotnet $compiler /nologo /nostdlib+ /target:library /langversion:latest /optimize+ /deterministic+ `
    "/out:$output" `
    "/reference:$(Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')" `
    "/reference:$(Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')" `
    $references `
    $source
if ($LASTEXITCODE -ne 0) { throw "Analyzer compilation failed with exit code $LASTEXITCODE." }
Get-FileHash -LiteralPath $source,$output -Algorithm SHA256
