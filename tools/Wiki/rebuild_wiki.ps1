[CmdletBinding()]
param(
    [string]$GameVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\docs\wiki\GAME_VERSION') -Raw -Encoding UTF8).Trim(),
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\generate_wiki_model.py') --repo-root $RepositoryRoot --game-version $GameVersion
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\validate_wiki_model.py') --repo-root $RepositoryRoot --game-version $GameVersion
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Push-Location (Join-Path $RepositoryRoot 'wiki')
try {
    npm run check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec astro build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec pagefind -- --site dist
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}
python -X utf8 (Join-Path $RepositoryRoot 'Tools\Wiki\audit_publication.py') --dist (Join-Path $RepositoryRoot 'wiki\dist') --model (Join-Path $RepositoryRoot "wiki\game-versions\$GameVersion\data")
exit $LASTEXITCODE
