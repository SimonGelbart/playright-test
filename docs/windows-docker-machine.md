# Linux host prerequisites for the Windows container

The application under test is the unchanged Microsoft ASP.NET Web Forms sample. Its Dockerfile uses the .NET Framework 4.8.1 SDK and ASP.NET runtime images, which are Windows container images. A Linux Docker daemon cannot build or run those images.

This repository uses [Stefan Scherer's `windows-docker-machine`](https://github.com/StefanScherer/windows-docker-machine) as the local-development path: Vagrant creates a Windows Server VM, Docker runs inside that VM, and the Linux Docker client connects to it through a Docker context.

## One-time setup

Install the following on the Linux host:

- hardware virtualization enabled in firmware;
- VirtualBox (or another provider supported by the Vagrant setup);
- Vagrant;
- the Docker CLI;
- PowerShell (`pwsh`) for installing the Playwright browser.

Create the Windows Docker host from the community Vagrant project:

```bash
git clone https://github.com/StefanScherer/windows-docker-machine.git
cd windows-docker-machine
vagrant up --provider virtualbox 2022-box
```

The `2022-box` choice matches the `windowsservercore-ltsc2022` base images used by the current Microsoft .NET Framework 4.8.1 image tags.

## Select and verify the Windows Docker engine

From the Vagrant project directory, or any shell after the context has been created:

```bash
docker context use 2022-box
docker version
docker info --format '{{.OSType}}/{{.Architecture}}'
```

The server side of `docker version` must report `windows/amd64`, and `docker info` must report `windows/amd64` (formatting may vary by Docker CLI version). If the active context still reports Linux, the ASP.NET image cannot run.

The Windows VM's address is the address to use from Linux when a published container port is tested directly. It is not guaranteed to be `localhost`:

```bash
docker context inspect 2022-box
```

## Quick smoke test from Linux

This verifies that the exact upstream Dockerfile builds and that IIS serves the existing sample. It does not run Aspire or Playwright:

```bash
docker context use 2022-box
docker build --pull -t legacy-web-smoke .
docker run --rm -d --name legacy-web-smoke -p 8000:80 legacy-web-smoke
```

Read the Windows VM IP from `docker context inspect 2022-box`, then open `http://<windows-vm-ip>:8000` in the Linux browser. Clean up when finished:

```bash
docker rm -f legacy-web-smoke
```

## Run the complete test lifecycle in the Windows VM

The `windows-docker-machine` Vagrantfile disables the default `/vagrant` share, so upload this working tree explicitly. The test fixture runs the Docker CLI directly because Aspire's local DCP orchestration currently requires Linux containers and does not support Windows-container resources. From the directory containing that Vagrantfile:

```bash
# Upload without --compress. Vagrant 2.4.9 can fail while creating its ZIP
# archive with a RubyZip "File exists" error for this tree.
vagrant upload /absolute/path/to/playright-test C:/Users/vagrant/legacy-web 2022-box
vagrant winrm 2022-box --command "Get-ChildItem C:\Users\vagrant\legacy-web"
```

`vagrant powershell` is only available when Vagrant runs on a Windows host. On Linux, use `vagrant winrm 2022-box --command "..."`; commands are executed by PowerShell inside the Windows guest. Check for .NET 10 with:

```bash
vagrant winrm 2022-box --command "dotnet --info"
```

If .NET 10 is not installed, run:

```bash
vagrant winrm 2022-box --command '$installer = "$env:TEMP\dotnet-install.ps1"; Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installer; & powershell.exe -ExecutionPolicy Bypass -File $installer -Channel 10.0 -InstallDir C:\dotnet; $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine"); if ($machinePath -notlike "*C:\dotnet*") { [Environment]::SetEnvironmentVariable("Path", "$machinePath;C:\dotnet", "Machine") }'
```

Open a new WinRM session and verify the installation:

```bash
vagrant winrm 2022-box --command "dotnet --info"
```

The same workflow is available as a single Linux-host command from the application repository:

```bash
./scripts/test-vm.sh
```

Alternatively, restore, build, install Chromium, and run the tests in one guest PowerShell command:

```bash
vagrant winrm 2022-box --command '$env:TUNIT_OTEL_RECEIVER = "0"; Set-Location C:\Users\vagrant\legacy-web; dotnet restore tests\LegacyWeb.Playwright.sln; dotnet build tests\LegacyWeb.Playwright.sln -c Release --no-restore; powershell.exe -ExecutionPolicy Bypass -File .\tests\LegacyWeb.Playwright.Tests\bin\Release\net10.0\playwright.ps1 install chromium; dotnet run --project .\tests\LegacyWeb.Playwright.Tests\LegacyWeb.Playwright.Tests.csproj -c Release --no-build'
```

The TUnit fixture builds and starts the `legacy-web` Dockerfile resource with Docker, waits for `GET /` to return HTTP 200, and cleans up the container afterward. TUnit's native runner is used here because this project is a Microsoft.Testing.Platform test application.

The full TUnit process can also run on Linux against the active `2022-box` Docker context with `./scripts/test.sh`; the fixture resolves the Windows VM host from the Docker context and uses the published port. Running inside the VM remains the fallback when the VM network does not expose published ports back to Linux.

The Vagrant project is community tooling with older documented provider/Docker versions. If the prebuilt box does not work with the installed hypervisor, build or provision the Windows Server 2022 box using that project's current instructions, or use a dedicated Windows/CI runner. The application and its Dockerfile should not be changed to work around a host-runtime problem.
