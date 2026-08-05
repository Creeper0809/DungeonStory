param(
    [switch]$WriteBaseline,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$architectureProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$architectureVersionMatch = Get-Content (Join-Path $architectureProjectRoot 'ProjectSettings\ProjectVersion.txt') |
    Select-String -Pattern '^m_EditorVersion:\s*(?<version>\S+)$' |
    Select-Object -First 1
$architectureUnityVersion = if ($null -eq $architectureVersionMatch) {
    [string]::Empty
}
else {
    $architectureVersionMatch.Matches[0].Groups['version'].Value
}
if ([string]::IsNullOrWhiteSpace($architectureUnityVersion)) {
    throw 'Could not resolve the Unity editor version from ProjectVersion.txt.'
}

$architectureUnityData = Join-Path 'C:\Program Files\Unity\Hub\Editor' "$architectureUnityVersion\Editor\Data"
$architectureDotnet = Join-Path $architectureUnityData 'NetCoreRuntime\dotnet.exe'
$architectureRoslynRoot = Join-Path $architectureUnityData 'DotNetSdkRoslyn'
$architectureCompiler = Join-Path $architectureRoslynRoot 'csc.dll'
$architectureRuntimeRoot = Get-ChildItem (Join-Path $architectureUnityData 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1 -ExpandProperty FullName
foreach ($requiredPath in @($architectureDotnet, $architectureCompiler, $architectureRuntimeRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required Unity compiler component is missing: $requiredPath"
    }
}

$architectureBuildRoot = Join-Path $architectureProjectRoot 'Library\ArchitectureMetrics'
New-Item -ItemType Directory -Path $architectureBuildRoot -Force | Out-Null
$architectureExecutable = Join-Path $architectureBuildRoot 'ArchitectureMetricsAnalyzer.dll'
$architectureSource = Join-Path $PSScriptRoot 'ArchitectureMetricsAnalyzer.cs'
$architectureCodeAnalysis = Join-Path $architectureRoslynRoot 'Microsoft.CodeAnalysis.dll'
$architectureCodeAnalysisCSharp = Join-Path $architectureRoslynRoot 'Microsoft.CodeAnalysis.CSharp.dll'
$architectureFrameworkReferences = Get-ChildItem $architectureRuntimeRoot -Filter '*.dll' |
    Where-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $true
        }
        catch {
            $false
        }
    } |
    ForEach-Object { "/r:$($_.FullName)" }

& $architectureDotnet $architectureCompiler /nologo /langversion:latest /target:exe /nostdlib+ `
    "/out:$architectureExecutable" `
    @architectureFrameworkReferences `
    "/r:$architectureCodeAnalysis" `
    "/r:$architectureCodeAnalysisCSharp" `
    $architectureSource
if ($LASTEXITCODE -ne 0) {
    throw "Architecture metrics compiler failed with exit code $LASTEXITCODE."
}

$architectureReport = Join-Path $architectureProjectRoot 'Assets\Architecture\runtime-architecture-metrics-current.json'
$architectureBaseline = Join-Path $architectureProjectRoot 'Assets\Architecture\runtime-architecture-metrics-baseline.json'
$architectureOwnershipManifest = Join-Path $PSScriptRoot 'default-assembly-ownership-overrides.json'
$architectureOwnershipReport = Join-Path $architectureBuildRoot 'default-assembly-ownership-report.json'
$architectureArguments = @(
    '--project', $architectureProjectRoot,
    '--report', $architectureReport,
    '--baseline', $architectureBaseline,
    '--ownership-manifest', $architectureOwnershipManifest,
    '--ownership-report', $architectureOwnershipReport
)
if ($WriteBaseline) { $architectureArguments += '--write-baseline' }
if ($Verify) { $architectureArguments += '--verify' }

& $architectureDotnet exec `
    --runtimeconfig (Join-Path $architectureRoslynRoot 'csc.runtimeconfig.json') `
    --depsfile (Join-Path $architectureRoslynRoot 'csc.deps.json') `
    $architectureExecutable `
    @architectureArguments
if ($LASTEXITCODE -ne 0) {
    throw "Architecture metrics analyzer failed with exit code $LASTEXITCODE."
}
