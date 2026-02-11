# generate-typings.ps1
# Generates C# typings from Monaco TypeScript definitions using TypedocConverter,
# then runs the STJ post-processor to transform attributes.
#
# https://github.com/hez2010/TypedocConverter
#
# Prerequisites:
#   - Node.js (for npm/npx)
#   - PowerShell 7+ (pwsh)
#   - Run 'npm install' in this directory first
#
# Output goes to GenerateMonacoTypings/output/ (isolated from MonacoEditorComponent/).

$ErrorActionPreference = 'Stop'

$monaco_file = ".\node_modules\monaco-editor\monaco.d.ts"
$typedoc_bin_url = $env:npm_package_config_typedocConverter  # see config in package.json
$outdir = $env:npm_package_config_outdir
$temp_dir_name = ".temp"

function Get-ScriptDirectory {
    Split-Path -Parent $PSCommandPath
}

$script_dir = Get-ScriptDirectory

Push-Location $script_dir

# Default output directory if not set via npm config
if (-not $outdir) {
    $outdir = "./output"
}

# Ensure output directory exists (and is clean)
if (Test-Path $outdir) {
    Remove-Item $outdir -Force -Recurse
}
New-Item -Name $outdir -ItemType Directory | Out-Null

# Create Temp Directory
if (Test-Path $temp_dir_name) {
    Remove-Item $temp_dir_name -Force -Recurse
}
New-Item -Name $temp_dir_name -ItemType Directory | Out-Null

# Verify monaco.d.ts is available
if (!(Test-Path $monaco_file -PathType Leaf)) {
    Write-Error "Monaco Definitions Not Found, run 'npm install' first."
    Pop-Location
    exit 1
}

# Copy monaco.d.ts to monaco.ts in temp folder (TypeDoc requires .ts extension)
Copy-Item $monaco_file -Destination (Join-Path $temp_dir_name "monaco.ts")

Push-Location $temp_dir_name

# Run TypeDoc to generate JSON representation
Write-Host "Running TypeDoc to generate JSON from monaco.d.ts..."
Write-Output '{"compilerOptions":{"target":"es2020"}}' > tsconfig.json
$typedocResult = npx typedoc monaco.ts --json monaco.json 2>&1
$typedocExitCode = $LASTEXITCODE

if ($typedocExitCode -ne 0) {
    Write-Warning "TypeDoc failed (exit code $typedocExitCode). This is expected for Monaco 0.54.0 with TypeDoc 0.20.x."
    Write-Warning "Use the standalone post-processor mode instead: pwsh ./postprocess-stj.ps1 -Standalone"
    Write-Warning "TypeDoc output: $typedocResult"

    Pop-Location
    # Clean up temp dir
    Remove-Item $temp_dir_name -Force -Recurse -ErrorAction SilentlyContinue
    Pop-Location
    exit 1
}

# Download and run TypedocConverter
Write-Host "Downloading TypedocConverter..."
[Net.ServicePointManager]::SecurityProtocol = "tls12, tls11, tls"
Invoke-WebRequest -Uri $typedoc_bin_url -OutFile "TypedocConverter.zip"

Write-Host "Extracting TypedocConverter..."
Expand-Archive "TypedocConverter.zip" -DestinationPath .

# Run TypedocConverter on our monaco.json
# Output goes to isolated output directory (not MonacoEditorComponent/)
Write-Host "Running TypedocConverter..."
$converterOutput = "../$outdir"
Invoke-Expression ".\TypedocConverter.exe --inputfile monaco.json --splitfiles true --outputdir `"$converterOutput`" --promise-type WinRT --nrt-disabled true"

Pop-Location

# Clean up temp dir
Remove-Item $temp_dir_name -Force -Recurse -ErrorAction SilentlyContinue

# Run post-processing to transform Newtonsoft attributes to STJ
Write-Host ""
Write-Host "Running STJ post-processor on generated output..."
& (Join-Path $script_dir 'postprocess-stj.ps1') -InputDir (Join-Path $script_dir $outdir)

Write-Host ""
Write-Host "Generation complete. Output is in: $outdir/"
Write-Host "Review the output and selectively merge into MonacoEditorComponent/Monaco/ as needed."
Write-Host "Hand-tuned files listed in .generator-ignore have been skipped."

Pop-Location
