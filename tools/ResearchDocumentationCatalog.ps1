param(
    [ValidateSet('Summary', 'Markdown', 'Json')]
    [string]$Mode = 'Summary',
    [ValidateRange(-1, 16)]
    [int]$Field = -1
)

$ErrorActionPreference = 'Stop'
$workspace = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    (Get-Location).Path
}
else {
    Split-Path -Parent $PSScriptRoot
}
$resources = Join-Path $workspace 'Assets/Resources'
$projectRoot = Join-Path $resources 'SO/Research/Projects'

function ConvertFrom-UnityYamlScalar {
    param([AllowEmptyString()][string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    $normalized = $Value.Trim()
    if ($normalized.Length -ge 2 -and
        $normalized[0] -eq '"' -and
        $normalized[$normalized.Length - 1] -eq '"') {
        $normalized = $normalized.Substring(1, $normalized.Length - 2)
    }
    return [regex]::Unescape($normalized)
}

function Get-UnityYamlScalar {
    param(
        [string]$Text,
        [string]$Key,
        [switch]$AnyIndent
    )

    $prefix = if ($AnyIndent) { '^\s+' } else { '^  ' }
    $pattern = '(?m)' + $prefix + [regex]::Escape($Key) + ':\s*(.+)$'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        return ''
    }
    return ConvertFrom-UnityYamlScalar $match.Groups[1].Value
}

function Get-UnityYamlBlockScalar {
    param(
        [string]$Text,
        [string]$Key,
        [string]$NextKey
    )

    $pattern = '(?ms)^  ' + [regex]::Escape($Key) +
        ':\s*(.*?)(?=^  ' + [regex]::Escape($NextKey) + ':)'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        return ''
    }
    $folded = ($match.Groups[1].Value -replace '\r?\n\s+', ' ').Trim()
    return ConvertFrom-UnityYamlScalar $folded
}

function Escape-MarkdownCell {
    param([AllowEmptyString()][string]$Value)
    return ($Value -replace '\|', '\|' -replace '\r?\n', ' ')
}

$fieldNames = @(
    '생활·생존',
    '상업·제작',
    '방어·전술',
    '기록·비전',
    '포획·흥행',
    '권위·주거',
    '농업',
    '임업',
    '채광',
    '축산',
    '야금',
    '직물',
    '요리',
    '약학',
    '수술·이식',
    '산업·자동화',
    '상하수도'
)

$projects = @{}
$projectFiles = Get-ChildItem -LiteralPath $projectRoot -Filter '*.asset'
foreach ($file in $projectFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $projectId = Get-UnityYamlScalar $text 'projectId'
    $prerequisites = @(
        [regex]::Matches(
            $text,
            '(?m)^  - prerequisiteId:\s*(research:[^\r\n]+)$') |
            ForEach-Object { $_.Groups[1].Value.Trim() }
    )
    $projects[$projectId] = [pscustomobject]@{
        NumericId = [int](Get-UnityYamlScalar $text 'id')
        Id = $projectId
        Name = Get-UnityYamlScalar $text 'displayName'
        Description = Get-UnityYamlBlockScalar $text 'description' 'field'
        Field = [int](Get-UnityYamlScalar $text 'field')
        Work = [int](Get-UnityYamlScalar $text 'requiredWork')
        Prerequisites = $prerequisites
        Rewards = [System.Collections.Generic.List[object]]::new()
        SourcePath = $file.FullName.Substring($workspace.Length + 1)
    }
}

$buildingNames = @{}
foreach ($file in Get-ChildItem -LiteralPath $resources -Recurse -Filter '*.asset') {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -notmatch '(?m)^  m_EditorClassIdentifier: .*::BuildingSO\s*$') {
        continue
    }
    $buildingId = Get-UnityYamlScalar $text 'id'
    $buildingName = Get-UnityYamlScalar $text 'objectName'
    if ($buildingId.Length -gt 0 -and
        $buildingName.Length -gt 0 -and
        -not $buildingNames.ContainsKey($buildingId)) {
        $buildingNames[$buildingId] = $buildingName
    }
}

$workwearItemIds = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
$workwearRoot = Join-Path $resources 'SO/Environment/Workwear'
foreach ($file in Get-ChildItem -LiteralPath $workwearRoot -Filter '*.asset') {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    [void]$workwearItemIds.Add((Get-UnityYamlScalar $text 'itemDefinitionId'))
}

$recipeNames = @{}
foreach ($file in Get-ChildItem -LiteralPath $resources -Recurse -Filter '*.asset') {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $classMatch = [regex]::Match(
        $text,
        '(?m)^  m_EditorClassIdentifier:\s*[^:]*::([^\r\n]+)$')
    if (-not $classMatch.Success) {
        continue
    }

    $definitionType = $classMatch.Groups[1].Value.Trim()
    $identityKey = ''
    $rewardKind = ''
    switch ($definitionType) {
        'ResourceItemDefinitionSO' {
            $identityKey = 'itemId'
            $rewardKind = '품목'
        }
        'ProductionRecipeSO' {
            $identityKey = 'recipeId'
            $rewardKind = '조합식'
        }
        'FacilitySynthesisRecipeSO' {
            $identityKey = 'recipeId'
            $rewardKind = '조합식'
        }
        'SurgicalProcedureSO' {
            $identityKey = 'procedureId'
            $rewardKind = '시술'
        }
        'CombatWeaponSO' {
            $identityKey = 'equipmentId'
            $rewardKind = '장비'
        }
        'CombatArmorSO' {
            $identityKey = 'equipmentId'
            $rewardKind = '장비'
        }
        'CombatShieldSO' {
            $identityKey = 'equipmentId'
            $rewardKind = '장비'
        }
        'CraftMaterialDefinitionSO' {
            $identityKey = 'materialId'
            $rewardKind = '재료'
        }
        'CropDefinitionSO' {
            $identityKey = 'cropId'
            $rewardKind = '작물'
        }
        default {
            continue
        }
    }
    if ($identityKey.Length -eq 0) {
        continue
    }

    if ($definitionType -eq 'ProductionRecipeSO' -or
        $definitionType -eq 'FacilitySynthesisRecipeSO') {
        $knownRecipeId = Get-UnityYamlScalar $text 'recipeId'
        $knownRecipeName = Get-UnityYamlScalar $text 'displayName'
        if ($knownRecipeId.Length -gt 0 -and $knownRecipeName.Length -gt 0) {
            $recipeNames[$knownRecipeId] = $knownRecipeName
        }
    }

    $researchId = Get-UnityYamlScalar $text 'requiredResearchId' -AnyIndent
    if (-not $projects.ContainsKey($researchId)) {
        continue
    }

    $rewardId = Get-UnityYamlScalar $text $identityKey
    if ($definitionType -eq 'ResourceItemDefinitionSO') {
        if ($workwearItemIds.Contains($rewardId)) {
            $rewardKind = '작업복'
        }
        elseif ($rewardId.StartsWith('ammo:', [StringComparison]::Ordinal)) {
            $rewardKind = '탄약'
        }
        elseif ($rewardId.StartsWith('component:', [StringComparison]::Ordinal) -or
            $rewardId.StartsWith('installation:', [StringComparison]::Ordinal)) {
            $rewardKind = '설치품'
        }
    }

    $projects[$researchId].Rewards.Add([pscustomobject]@{
        Kind = $rewardKind
        Id = $rewardId
        Name = Get-UnityYamlScalar $text 'displayName'
    })
}

foreach ($file in $projectFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $researchId = Get-UnityYamlScalar $text 'projectId'
    $project = $projects[$researchId]

    foreach ($match in [regex]::Matches($text, '(?m)^\s+buildingId:\s*(\d+)$')) {
        $buildingId = $match.Groups[1].Value
        $buildingName = if ($buildingNames.ContainsKey($buildingId)) {
            $buildingNames[$buildingId]
        }
        else {
            "시설 $buildingId"
        }
        $project.Rewards.Add([pscustomobject]@{
            Kind = '시설'
            Id = $buildingId
            Name = $buildingName
        })
    }

    foreach ($match in [regex]::Matches($text, '(?m)^\s+recipeId:\s*([^\r\n]+)$')) {
        $recipeId = $match.Groups[1].Value.Trim()
        $project.Rewards.Add([pscustomobject]@{
            Kind = '조합식'
            Id = $recipeId
            Name = if ($recipeNames.ContainsKey($recipeId)) {
                $recipeNames[$recipeId]
            }
            else {
                $recipeId
            }
        })
    }

    $deduplicated = @(
        $project.Rewards |
            Group-Object { $_.Kind + '|' + $_.Id } |
            ForEach-Object { $_.Group[0] } |
            Sort-Object Kind, Id
    )
    $project.Rewards.Clear()
    foreach ($reward in $deduplicated) {
        $project.Rewards.Add($reward)
    }
}

$ordered = @($projects.Values | Sort-Object NumericId)
$selected = if ($Field -ge 0) {
    @($ordered | Where-Object { $_.Field -eq $Field })
}
else {
    $ordered
}

switch ($Mode) {
    'Json' {
        $selected | ConvertTo-Json -Depth 7
    }
    'Markdown' {
        $fieldGroups = @($selected | Group-Object Field | Sort-Object { [int]$_.Name })
        foreach ($group in $fieldGroups) {
            $fieldId = [int]$group.Name
            "### 26.$($fieldId + 1) $($fieldNames[$fieldId]) 연구 ($($group.Count)개)"
            ''
            '| ID | 연구 | 작업량 | 직접 선행 | 직접 물리 해금 | 개별 의도 |'
            '|---:|---|---:|---|---|---|'
            foreach ($project in $group.Group | Sort-Object NumericId) {
                $prerequisiteText = if ($project.Prerequisites.Count -eq 0) {
                    '없음'
                }
                else {
                    @($project.Prerequisites | ForEach-Object {
                        $required = $projects[$_]
                        '{0} (`{1}`)' -f (Escape-MarkdownCell $required.Name), $_
                    }) -join '<br>'
                }
                $rewardText = @($project.Rewards | ForEach-Object {
                    "[$($_.Kind)] $(Escape-MarkdownCell $_.Name)"
                }) -join '<br>'
                $researchText = "**$(Escape-MarkdownCell $project.Name)**<br>``$($project.Id)``"
                "| $($project.NumericId) | $researchText | $($project.Work) | $prerequisiteText | $rewardText | $(Escape-MarkdownCell $project.Description) |"
            }
            ''
        }
    }
    default {
        $duplicateIds = @($ordered | Group-Object Id | Where-Object Count -gt 1)
        $missingPrerequisites = @(
            $ordered | ForEach-Object {
                foreach ($prerequisite in $_.Prerequisites) {
                    if (-not $projects.ContainsKey($prerequisite)) {
                        "$($_.Id) -> $prerequisite"
                    }
                }
            }
        )
        $zeroReward = @($ordered | Where-Object { $_.Rewards.Count -eq 0 })
        "projects=$($ordered.Count)"
        "requiredWork=$(($ordered | Measure-Object Work -Sum).Sum)"
        "duplicateIds=$($duplicateIds.Count)"
        "missingPrerequisites=$($missingPrerequisites.Count)"
        "zeroRewardProjects=$($zeroReward.Count)"
        "rewardEntries=$(($ordered | ForEach-Object { $_.Rewards.Count } | Measure-Object -Sum).Sum)"
        foreach ($group in $ordered | Group-Object Field | Sort-Object { [int]$_.Name }) {
            "field[$($group.Name)]=$($fieldNames[[int]$group.Name]);count=$($group.Count);work=$(($group.Group | Measure-Object Work -Sum).Sum)"
        }
        if ($missingPrerequisites.Count -gt 0) {
            'missingPrerequisiteDetails=' + ($missingPrerequisites -join ',')
        }
        if ($zeroReward.Count -gt 0) {
            'zeroRewardDetails=' + (($zeroReward | ForEach-Object Id) -join ',')
        }
    }
}
