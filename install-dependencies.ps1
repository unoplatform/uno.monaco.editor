# Run this script using PowerShell to install dependencies and build
# the Monaco Editor bundles before building the .NET solution.

$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    Split-Path -parent $PSCommandPath
}

$script_dir = Get-ScriptDirectory
Push-Location $script_dir

try {
    # Install npm dependencies (monaco-editor, vscode-jsonrpc, esbuild, typescript)
    Write-Host "Installing npm dependencies..."
    npm install
    if ($LASTEXITCODE -ne 0) {
        throw "npm install failed with exit code $LASTEXITCODE"
    }

    # Build TypeScript helpers and Monaco bundles via esbuild
    Write-Host "Building Monaco bundles with esbuild..."
    node MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs
    if ($LASTEXITCODE -ne 0) {
        throw "esbuild build failed with exit code $LASTEXITCODE"
    }

    Write-Host "Dependencies installed and bundles built successfully."
}
finally {
    Pop-Location
}
