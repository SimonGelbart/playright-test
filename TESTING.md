# Playwright test harness

This repository contains Microsoft's `samples/aspnetapp` sample as the application under test. The source commit is recorded in [UPSTREAM_COMMIT.txt](UPSTREAM_COMMIT.txt). The application files, project, and Dockerfile remain upstream-authoritative.

The test infrastructure adds:

- `src/LegacyWeb.AppHost`: a .NET 10 Aspire AppHost;
- the `legacy-web` resource, built from the root Dockerfile;
- an HTTP endpoint with target port `80`;
- an HTTP health check at `/`;
- `tests/LegacyWeb.Playwright.Tests`: a .NET 10 TUnit project using `TUnit.Playwright`.

The Windows-container test fixture builds the exact root Dockerfile, starts the resulting IIS container with the Docker CLI, discovers its published port, waits for `GET /` to return HTTP 200, and removes the container and image after the test session. Playwright launches Chromium and navigates to the published endpoint. The browser tests cover the original home page, About and Contact pages, navigation links, and a missing-page response.

The AppHost remains as a modern application-model description, but Aspire's local DCP orchestration currently requires Linux containers; Windows-container resources are not supported by that path. Therefore the Windows test path uses the Docker CLI directly while preserving the same resource name, port, and root health check behavior.

Because the application is .NET Framework 4.8.1 on IIS, full execution requires a Windows Docker daemon. On Linux, use the Windows Server VM workflow in [docs/windows-docker-machine.md](docs/windows-docker-machine.md), or point the Docker client at a remote Windows runner.

## One-command execution

On Windows with a local Windows-container Docker engine:

```powershell
pwsh .\scripts\test.ps1 -Clean
```

On Linux with the active Docker context connected to a Windows engine:

```bash
./scripts/test.sh
```

The fixture resolves a TCP Docker context host automatically. Set `LEGACY_WEB_HOST` or `LEGACY_WEB_BASE_ADDRESS` when the endpoint needs to be overridden. Set `LEGACY_WEB_KEEP_IMAGE=true` to retain the generated image for debugging.

On Linux using the Vagrant VM workflow:

```bash
./scripts/test-vm.sh
```

To verify that the imported application still matches the recorded upstream commit:

```bash
./scripts/verify-upstream.sh
```
