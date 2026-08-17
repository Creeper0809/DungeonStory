param(
    [string]$UnityEditorData = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data'
)

$ErrorActionPreference = 'Stop'
$analyzer = Join-Path $PSScriptRoot '..\..\Assets\Analyzers\DungeonStory.BalanceAnalyzers.dll'
$compiler = Join-Path $UnityEditorData 'DotNetSdkRoslyn\csc.dll'
$runtimeDirectory = Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'NetCoreRuntime\shared\Microsoft.NETCore.App') -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1
$frameworkReferences = @(
    'System.Private.CoreLib.dll', 'System.Runtime.dll', 'netstandard.dll',
    'System.Collections.dll', 'System.Linq.dll', 'System.Memory.dll'
)
$references = $frameworkReferences | ForEach-Object {
    "/reference:$(Join-Path $runtimeDirectory.FullName $_)"
}
$temporary = Join-Path ([System.IO.Path]::GetTempPath()) 'DungeonStory.BalanceAnalyzers.Tests'
New-Item -ItemType Directory -Force -Path $temporary | Out-Null
$rebuiltAnalyzer = Join-Path $temporary 'DungeonStory.BalanceAnalyzers.dll'
& (Join-Path $PSScriptRoot 'build-analyzer.ps1') `
    -UnityEditorData $UnityEditorData `
    -OutputPath $rebuiltAnalyzer | Out-Null
if (-not (Test-Path -LiteralPath $analyzer)) {
    throw 'Committed Unity analyzer DLL is missing.'
}
$committedBinaryHash = (Get-FileHash -LiteralPath $analyzer -Algorithm SHA256).Hash
$rebuiltBinaryHash = (Get-FileHash -LiteralPath $rebuiltAnalyzer -Algorithm SHA256).Hash
if ($committedBinaryHash -ne $rebuiltBinaryHash) {
    throw "DSB006 analyzer binary drift: committed=$committedBinaryHash rebuilt=$rebuiltBinaryHash"
}

& dotnet $compiler /nologo /nostdlib+ /target:library /langversion:latest `
    "/out:$(Join-Path $temporary 'positive.dll')" "/analyzer:$analyzer" `
    $references (Join-Path $PSScriptRoot 'Tests\Positive.cs')
if ($LASTEXITCODE -ne 0) { throw 'Positive analyzer fixture failed.' }

$negativeOutput = & dotnet $compiler /nologo /nostdlib+ /target:library /langversion:latest `
    "/out:$(Join-Path $temporary 'negative.dll')" "/analyzer:$analyzer" `
    $references (Join-Path $PSScriptRoot 'Tests\Negative.cs') 2>&1
if ($LASTEXITCODE -eq 0) { throw 'Negative analyzer fixture unexpectedly compiled.' }
$joined = $negativeOutput -join "`n"
foreach ($id in 'DSB001','DSB002','DSB003','DSB004','DSB005','DSB007','DSB008') {
    if ($joined -notmatch $id) { throw "Negative analyzer fixture did not emit $id.`n$joined" }
}

$sourceHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'DungeonStoryBalanceAnalyzer.cs') -Algorithm SHA256).Hash
$binaryHash = $committedBinaryHash
if ([string]::IsNullOrWhiteSpace($sourceHash) -or [string]::IsNullOrWhiteSpace($binaryHash)) {
    throw 'DSB006 source/binary hash gate could not capture both hashes.'
}
Write-Output "RESULT=PASS; analyzerRules=DSB001-DSB008; sourceHash=$sourceHash; binaryHash=$binaryHash"
