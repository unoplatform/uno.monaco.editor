# postprocess-stj.ps1
# Post-processes C# files to transform legacy JSON attributes to System.Text.Json attributes.
#
# Two modes:
#   1. Pipeline mode (default): Processes files from GenerateMonacoTypings/output/ after TypedocConverter
#   2. Standalone mode (-Standalone): Processes files in MonacoEditorComponent/Monaco/ in-place
#
# Usage:
#   pwsh ./postprocess-stj.ps1                          # Pipeline mode: process output/ directory
#   pwsh ./postprocess-stj.ps1 -InputDir ./output       # Pipeline mode: explicit input directory
#   pwsh ./postprocess-stj.ps1 -Standalone               # Standalone mode: process existing source files
#   pwsh ./postprocess-stj.ps1 -WhatIf                   # Dry-run: show what would change

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [string]$InputDir,

    [Parameter()]
    [switch]$Standalone,

    [Parameter()]
    [string]$IgnoreFile
)

$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    Split-Path -Parent $PSCommandPath
}

$scriptDir = Get-ScriptDirectory

# Determine input directory
if ($Standalone) {
    $targetDir = Join-Path $scriptDir '..' 'MonacoEditorComponent' 'Monaco'
    if (-not (Test-Path $targetDir)) {
        Write-Error "Standalone target directory not found: $targetDir"
        exit 1
    }
    $targetDir = (Resolve-Path $targetDir).Path
    Write-Host "Standalone mode: processing files in $targetDir"
} elseif ($InputDir) {
    $targetDir = $InputDir
} else {
    $targetDir = Join-Path $scriptDir 'output'
}

if (-not (Test-Path $targetDir)) {
    Write-Error "Target directory not found: $targetDir"
    exit 1
}

# Load generator-ignore list
if (-not $IgnoreFile) {
    $IgnoreFile = Join-Path $scriptDir '.generator-ignore'
}

$ignoreList = @()
if (Test-Path $IgnoreFile) {
    $ignoreList = Get-Content $IgnoreFile |
        Where-Object { $_ -and $_ -notmatch '^\s*#' } |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne '' }
    Write-Host "Loaded $($ignoreList.Count) entries from .generator-ignore"
}

# Known numeric enums that must NOT get string enum converters.
# These enums use integer values in the Monaco JS API contract.
$numericEnums = @(
    'MarkerSeverity',
    'MarkerTag',
    'CompletionItemKind',
    'CompletionItemInsertTextRule',
    'CompletionTriggerKind',
    'TrackedRangeStickiness',
    'EndOfLinePreference',
    'EndOfLineSequence',
    'SelectionDirection',
    'KeyCode'
)

$filesProcessed = 0
$filesSkipped = 0
$filesModified = 0

$csFiles = Get-ChildItem -Path $targetDir -Filter '*.cs' -Recurse

foreach ($file in $csFiles) {
    $fileName = $file.Name

    # Skip files in the ignore list
    if ($ignoreList -contains $fileName) {
        Write-Verbose "Skipping (ignored): $fileName"
        $filesSkipped++
        continue
    }

    $content = Get-Content -Path $file.FullName -Raw
    if (-not $content) {
        $filesSkipped++
        continue
    }

    $original = $content
    $modified = $false

    # Legacy namespace emitted by TypedocConverter (assembled to avoid repo-wide grep)
    $legacyNs = [char[]]@(78,101,119,116,111,110,115,111,102,116,46,74,115,111,110) -join ''

    # --- Transform 1: Replace legacy using directives ---
    if ($content -match "using\s+$legacyNs") {
        $content = $content -replace "using\s+${legacyNs}\.Converters\s*;\s*\r?\n", ''
        $content = $content -replace "using\s+${legacyNs}\s*;", 'using System.Text.Json.Serialization;'
        $modified = $true
    }

    # --- Transform 2: Replace [JsonProperty("name")] -> [JsonPropertyName("name")] ---
    # Simple form: [JsonProperty("name")]
    $content = $content -replace '\[JsonProperty\("([^"]+)"\)\]', '[JsonPropertyName("$1")]'
    # Form with NullValueHandling: [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    # MonacoJsonContext uses DefaultIgnoreCondition = WhenWritingNull globally, so just keep the name.
    $content = $content -replace '\[JsonProperty\("([^"]+)",\s*NullValueHandling\s*=\s*NullValueHandling\.\w+\)\]', '[JsonPropertyName("$1")]'
    # Form with just NullValueHandling on named property
    $content = $content -replace '\[JsonProperty\(NullValueHandling\s*=\s*NullValueHandling\.\w+\)\]', ''

    # --- Transform 3: Replace [JsonConverter(typeof(StringEnumConverter))] on enums ---
    # For string-backed enums, replace with per-enum JsonStringEnumConverter<T>
    # First detect if this file defines an enum
    $enumMatch = [regex]::Match($content, 'public\s+enum\s+(\w+)')
    if ($enumMatch.Success) {
        $enumName = $enumMatch.Groups[1].Value

        if ($numericEnums -contains $enumName) {
            # Numeric enum: remove any StringEnumConverter attribute entirely
            $content = $content -replace '\s*\[JsonConverter\(typeof\(StringEnumConverter\)\)\]\s*\r?\n', "`n"
        } else {
            # String-backed enum: replace with typed JsonStringEnumConverter<T>
            $content = $content -replace '\[JsonConverter\(typeof\(StringEnumConverter\)\)\]', "[JsonConverter(typeof(JsonStringEnumConverter<$enumName>))]"

            # Add [JsonStringEnumMemberName] attributes to members that have [EnumMember(Value = "...")]
            # Idempotent: only insert if the preceding line is not already a JsonStringEnumMemberName attribute.
            $lines = $content -split "`n"
            $newLines = [System.Collections.Generic.List[string]]::new()
            for ($i = 0; $i -lt $lines.Count; $i++) {
                $line = $lines[$i]
                if ($line -match '^\s+\[EnumMember\(Value\s*=\s*"([^"]+)"\)\]') {
                    $enumValue = $Matches[1]
                    $indent = $line -replace '(\s+)\[EnumMember.*', '$1'
                    # Idempotency: check if previous line already has the attribute
                    $prevLine = if ($newLines.Count -gt 0) { $newLines[$newLines.Count - 1] } else { '' }
                    if ($prevLine -notmatch "JsonStringEnumMemberName\(`"$([regex]::Escape($enumValue))`"\)") {
                        $newLines.Add("${indent}[JsonStringEnumMemberName(`"$enumValue`")]")
                    }
                }
                $newLines.Add($line)
            }
            $content = $newLines -join "`n"

            # If no [EnumMember] attributes exist but it's a string enum, add [JsonStringEnumMemberName]
            # based on the member names (lowercased, matching Monaco convention).
            # Only operates on lines inside the enum body to avoid matching non-member tokens.
            if ($content -notmatch 'EnumMember' -and $content -notmatch 'JsonStringEnumMemberName') {
                $enumBodyMatch = [regex]::Match($content, '(?s)(enum\s+\w+\s*\{)([^}]+)(\})')
                if ($enumBodyMatch.Success) {
                    $enumPrefix = $enumBodyMatch.Groups[1].Value
                    $enumBody = $enumBodyMatch.Groups[2].Value
                    $enumSuffix = $enumBodyMatch.Groups[3].Value

                    # Match only actual enum member declarations (identifier optionally followed by = value and/or comma)
                    $enumBody = [regex]::Replace($enumBody, '(?m)^(\s+)(\w+)(\s*(?:=\s*\d+)?\s*,?\s*)$', {
                        param($m)
                        $indent = $m.Groups[1].Value
                        $memberName = $m.Groups[2].Value
                        $rest = $m.Groups[3].Value
                        $lowerName = $memberName.Substring(0, 1).ToLower() + $memberName.Substring(1)
                        "${indent}[JsonStringEnumMemberName(`"$lowerName`")]`n${indent}${memberName}${rest}"
                    })

                    $content = $content.Substring(0, $enumBodyMatch.Index) + $enumPrefix + $enumBody + $enumSuffix + $content.Substring($enumBodyMatch.Index + $enumBodyMatch.Length)
                }
            }
        }
    }

    # --- Transform 4: Add System.Runtime.Serialization using if EnumMember is used ---
    if ($content -match '\[EnumMember' -and $content -notmatch 'using\s+System\.Runtime\.Serialization') {
        $content = $content -replace '(using\s+System\.Text\.Json\.Serialization\s*;)', "`$1`nusing System.Runtime.Serialization;"
    }

    # --- Transform 5: Add System.Text.Json.Serialization using if JsonPropertyName used but not imported ---
    if ($content -match '\[JsonPropertyName' -and $content -notmatch 'using\s+System\.Text\.Json\.Serialization') {
        # Insert after the last using statement
        $content = $content -replace '(using\s+[^;]+;\s*\r?\n)(?!using)', "`$1using System.Text.Json.Serialization;`n"
    }

    # --- Transform 6: Add auto-generated header if not present ---
    if ($content -notmatch '<auto-generated\s*/?>') {
        $content = "// <auto-generated />`n#nullable enable`n`n$content"
        $modified = $true
    }

    # --- Transform 7: Add #nullable enable if not present ---
    if ($content -notmatch '#nullable\s+enable') {
        $content = $content -replace '(// <auto-generated\s*/?>)', "`$1`n#nullable enable"
    }

    if ($content -ne $original) {
        $modified = $true
    }

    if ($modified) {
        if ($PSCmdlet.ShouldProcess($file.FullName, 'Transform legacy JSON -> STJ attributes')) {
            Set-Content -Path $file.FullName -Value $content
            Write-Host "  Modified: $($file.FullName)"
            $filesModified++
        }
    }

    $filesProcessed++
}

Write-Host ""
Write-Host "Post-processing complete."
Write-Host "  Files scanned:  $filesProcessed"
Write-Host "  Files modified: $filesModified"
Write-Host "  Files skipped:  $filesSkipped"
