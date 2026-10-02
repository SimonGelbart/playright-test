#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

if [[ "${CLEAN:-0}" == "1" ]]; then
  rm -rf \
    "$repository_root/tests/LegacyWeb.Playwright.Tests/bin" \
    "$repository_root/tests/LegacyWeb.Playwright.Tests/obj" \
    "$repository_root/src/LegacyWeb.AppHost/bin" \
    "$repository_root/src/LegacyWeb.AppHost/obj"
fi

dotnet restore "$repository_root/tests/LegacyWeb.Playwright.sln"
dotnet build "$repository_root/tests/LegacyWeb.Playwright.sln" \
  --configuration "$configuration" \
  --no-restore

playwright_script="$repository_root/tests/LegacyWeb.Playwright.Tests/bin/$configuration/net10.0/playwright.ps1"
if ! command -v pwsh >/dev/null 2>&1; then
  echo "PowerShell 7 (pwsh) is required to install Playwright Chromium." >&2
  exit 1
fi
pwsh -NoProfile -ExecutionPolicy Bypass -File "$playwright_script" install chromium

export TUNIT_OTEL_RECEIVER=0
dotnet run \
  --project "$repository_root/tests/LegacyWeb.Playwright.Tests/LegacyWeb.Playwright.Tests.csproj" \
  --configuration "$configuration" \
  --no-build
