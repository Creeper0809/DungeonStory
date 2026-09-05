param(
    [string]$ContentDatabaseRoot = "docs_final/content-db",
    [string]$KnowledgeBaseRoot = "docs_final/knowledge-base"
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../..")
Push-Location $projectRoot
try {
    python -X utf8 Tools/Documentation/test_query_knowledge_base.py
    if ($LASTEXITCODE -ne 0) { throw "Knowledge-base query tests failed with exit code $LASTEXITCODE." }

    python -X utf8 Tools/Documentation/generate_content_database.py --output-root $ContentDatabaseRoot
    if ($LASTEXITCODE -ne 0) { throw "Content database generation failed with exit code $LASTEXITCODE." }

    & Tools/Documentation/validate_content_database.ps1 -DatabaseRoot $ContentDatabaseRoot

    python -X utf8 Tools/Documentation/generate_knowledge_base.py --output-root $KnowledgeBaseRoot --content-db $ContentDatabaseRoot
    if ($LASTEXITCODE -ne 0) { throw "Knowledge base generation failed with exit code $LASTEXITCODE." }

    python -X utf8 Tools/Documentation/validate_knowledge_base.py --root $KnowledgeBaseRoot --content-db $ContentDatabaseRoot
    if ($LASTEXITCODE -ne 0) { throw "Knowledge base validation failed with exit code $LASTEXITCODE." }

    python -X utf8 Tools/Documentation/verify_knowledge_base.py $ContentDatabaseRoot $KnowledgeBaseRoot
    if ($LASTEXITCODE -ne 0) { throw "Knowledge base stale verification failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
