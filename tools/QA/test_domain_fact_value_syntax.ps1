$ErrorActionPreference = 'Stop'
$syntaxSource = Join-Path $PSScriptRoot '../../Assets/Scripts/Services/Foundation/Events/DomainFactValueSyntax.cs'
$syntaxSource = (Resolve-Path -LiteralPath $syntaxSource).Path
Add-Type -Path $syntaxSource
$script:syntaxChecks = 0
function Assert-SyntaxValue([bool]$Actual, [bool]$Expected, [string]$Case) {
    $script:syntaxChecks++
    if ($Actual -ne $Expected) { throw "Domain fact syntax mismatch: $Case" }
}

foreach ($code in 0..127) {
    $value = 'id' + [char]$code
    $expected = $value -cmatch '\A[a-zA-Z0-9.:_/-]+\z'
    Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsIdentity($value)) $expected "ASCII $code"
}
# Reflection preserves a real CLR null; PowerShell's string binder otherwise
# coerces $null to an empty string before entering the C# implementation.
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax].GetMethod('IsIdentity').Invoke($null, [object[]]@($null))) $false 'null identity'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsIdentity('')) $false 'empty identity'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsIdentity('한글')) $false 'non-ASCII identity'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsIdentity(('x' * 192))) $true 'identity limit'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsIdentity(('x' * 193))) $false 'identity overflow'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax].GetMethod('IsText').Invoke($null, [object[]]@($null, 256, $true))) $false 'null text'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText('', 256, $true)) $true 'optional empty text'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText(' ', 256, $false)) $false 'blank required text'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText(('가' * 256), 256, $false)) $true 'name limit'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText(('가' * 257), 256, $false)) $false 'name overflow'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText([char]0xD83D + [string][char]0xDE00, 2, $false)) $true 'surrogate pair'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText([string][char]0xD83D, 2, $false)) $false 'unpaired high surrogate'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText([string][char]0xDE00, 2, $false)) $false 'unpaired low surrogate'
Assert-SyntaxValue ([DungeonStory.Foundation.Facts.DomainFactValueSyntax]::IsText([char]0xD83D + 'x', 2, $false)) $false 'invalid surrogate successor'

[pscustomobject]@{
    result = 'PASS'
    assertions = $script:syntaxChecks
    sourceSha256 = (Get-FileHash -LiteralPath $syntaxSource -Algorithm SHA256).Hash
    scope = 'Pure neutral C# identity/text execution; Unity compilation and gameplay NOT_RUN'
} | ConvertTo-Json -Compress
