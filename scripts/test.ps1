[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$SkipBrowserInstall
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $repositoryRoot "tests\LegacyWeb.Playwright.sln"
$testProject = Join-Path $repositoryRoot "tests\LegacyWeb.Playwright.Tests\LegacyWeb.Playwright.Tests.csproj"

Push-Location $repositoryRoot
try {
    if ($Clean) {
        Remove-Item `
            (Join-Path $repositoryRoot "tests\LegacyWeb.Playwright.Tests\bin"), `
            (Join-Path $repositoryRoot "tests\LegacyWeb.Playwright.Tests\obj"), `
            (Join-Path $repositoryRoot "src\LegacyWeb.AppHost\bin"), `
            (Join-Path $repositoryRoot "src\LegacyWeb.AppHost\obj") `
            -Recurse -Force -ErrorAction SilentlyContinue
    }

    dotnet restore $solution
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $solution -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $SkipBrowserInstall) {
        $playwrightScript = Join-Path $repositoryRoot "tests\LegacyWeb.Playwright.Tests\bin\$Configuration\net10.0\playwright.ps1"
        $shell = Get-Command powershell.exe -ErrorAction SilentlyContinue
        if ($null -eq $shell) {
            $shell = Get-Command pwsh -ErrorAction SilentlyContinue
        }
        if ($null -eq $shell) {
            throw "PowerShell is required to install the Playwright Chromium browser."
        }

        & $shell.Source -NoProfile -ExecutionPolicy Bypass -File $playwrightScript install chromium
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $env:TUNIT_OTEL_RECEIVER = "0"
    dotnet run --project $testProject -c $Configuration --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
