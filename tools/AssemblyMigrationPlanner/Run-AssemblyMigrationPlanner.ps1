param(
    [switch]$SelfTest,
    [string]$ReportPath,
    [string]$ResponseFile
)

$ErrorActionPreference = 'Stop'
$plannerProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$plannerVersionMatch = Get-Content (Join-Path $plannerProjectRoot 'ProjectSettings\ProjectVersion.txt') |
    Select-String -Pattern '^m_EditorVersion:\s*(?<version>\S+)$' |
    Select-Object -First 1
if ($null -eq $plannerVersionMatch) {
    throw 'Could not resolve the Unity editor version from ProjectVersion.txt.'
}
$plannerUnityVersion = $plannerVersionMatch.Matches[0].Groups['version'].Value
$plannerUnityData = Join-Path 'C:\Program Files\Unity\Hub\Editor' "$plannerUnityVersion\Editor\Data"
$plannerDotnet = Join-Path $plannerUnityData 'NetCoreRuntime\dotnet.exe'
$plannerRoslynRoot = Join-Path $plannerUnityData 'DotNetSdkRoslyn'
$plannerCompiler = Join-Path $plannerRoslynRoot 'csc.dll'
$plannerRuntimeRoot = Get-ChildItem (Join-Path $plannerUnityData 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1 -ExpandProperty FullName
foreach ($plannerRequiredPath in @($plannerDotnet, $plannerCompiler, $plannerRuntimeRoot)) {
    if (-not (Test-Path -LiteralPath $plannerRequiredPath)) {
        throw "Required Unity compiler component is missing: $plannerRequiredPath"
    }
}

$plannerBuildRoot = Join-Path $plannerProjectRoot 'Library\AssemblyMigrationPlanner'
New-Item -ItemType Directory -Path $plannerBuildRoot -Force | Out-Null
$plannerExecutable = Join-Path $plannerBuildRoot 'AssemblyMigrationPlanner.dll'
$plannerSource = Join-Path $PSScriptRoot 'AssemblyMigrationPlanner.cs'
$plannerCodeAnalysis = Join-Path $plannerRoslynRoot 'Microsoft.CodeAnalysis.dll'
$plannerCodeAnalysisCSharp = Join-Path $plannerRoslynRoot 'Microsoft.CodeAnalysis.CSharp.dll'
$plannerFrameworkReferences = Get-ChildItem $plannerRuntimeRoot -Filter '*.dll' |
    Where-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $true
        }
        catch { $false }
    } |
    ForEach-Object { "/r:$($_.FullName)" }

& $plannerDotnet $plannerCompiler /nologo /langversion:latest /target:exe /nostdlib+ `
    "/out:$plannerExecutable" `
    @plannerFrameworkReferences `
    "/r:$plannerCodeAnalysis" `
    "/r:$plannerCodeAnalysisCSharp" `
    $plannerSource
if ($LASTEXITCODE -ne 0) {
    throw "Assembly migration planner compiler failed with exit code $LASTEXITCODE."
}

$plannerArguments = @()
if ($SelfTest) {
    $plannerArguments += '--self-test'
}
else {
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        $ReportPath = Join-Path $plannerBuildRoot 'assembly-migration-plan.json'
    }
    if ([string]::IsNullOrWhiteSpace($ResponseFile)) {
        $ResponseFile = Get-ChildItem (Join-Path $plannerProjectRoot 'Library\Bee\artifacts') -Recurse -Filter 'Assembly-CSharp.rsp' |
            Sort-Object @{ Expression = 'LastWriteTimeUtc'; Descending = $true }, @{ Expression = 'FullName'; Descending = $false } |
            Select-Object -First 1 -ExpandProperty FullName
    }
    $plannerArguments += @('--project', $plannerProjectRoot, '--report', $ReportPath)
    if (-not [string]::IsNullOrWhiteSpace($ResponseFile)) {
        $plannerArguments += @('--rsp', $ResponseFile)
    }
}

& $plannerDotnet exec `
    --runtimeconfig (Join-Path $plannerRoslynRoot 'csc.runtimeconfig.json') `
    --depsfile (Join-Path $plannerRoslynRoot 'csc.deps.json') `
    $plannerExecutable `
    @plannerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Assembly migration planner failed with exit code $LASTEXITCODE."
}
