using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

using TUnit.Core.Interfaces;

namespace LegacyWeb.Playwright.Tests;

public sealed class LegacyWebContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private string? _containerName;
    private string? _imageName;
    private string? _repositoryRoot;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _repositoryRoot = FindRepositoryRoot();
        _containerName = $"legacy-web-playwright-{Guid.NewGuid():N}";
        _imageName = $"legacy-web-playwright:{Guid.NewGuid():N}";
        var configuredBaseAddress = ReadConfiguredBaseAddress();

        try
        {
            await RunDockerAsync(_repositoryRoot, "build", "--pull", "--tag", _imageName, ".");

            var requestedPort = configuredBaseAddress is { IsDefaultPort: false }
                ? configuredBaseAddress.Port
                : 0;

            await RunDockerAsync(
                _repositoryRoot,
                "run",
                "--detach",
                "--label",
                "legacy-web.playwright=true",
                "--name",
                _containerName,
                "--publish",
                $"{requestedPort}:80",
                _imageName);

            var portOutput = await RunDockerAsync(_repositoryRoot, "port", _containerName, "80/tcp");
            var hostPort = ParsePublishedPort(portOutput);
            var host = configuredBaseAddress?.Host ??
                       Environment.GetEnvironmentVariable("LEGACY_WEB_HOST") ??
                       await ResolveDockerEndpointHostAsync(_repositoryRoot);

            BaseAddress = configuredBaseAddress is { IsDefaultPort: false }
                ? configuredBaseAddress
                : new Uri($"http://{host}:{hostPort}");

            await WaitForApplicationAsync(BaseAddress, _repositoryRoot, _containerName);
        }
        catch
        {
            await StopContainerAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => new(StopContainerAsync());

    private async Task StopContainerAsync()
    {
        var containerName = _containerName;
        var imageName = _imageName;
        var repositoryRoot = _repositoryRoot;
        _containerName = null;
        _imageName = null;

        if (repositoryRoot is null)
        {
            return;
        }

        if (containerName is not null)
        {
            try
            {
                await RunDockerAsync(repositoryRoot, "rm", "--force", containerName);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Could not remove test container '{containerName}': {exception.Message}");
            }
        }

        if (imageName is not null && !IsEnabled("LEGACY_WEB_KEEP_IMAGE"))
        {
            try
            {
                await RunDockerAsync(repositoryRoot, "image", "rm", "--force", imageName);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Could not remove test image '{imageName}': {exception.Message}");
            }
        }
    }

    private static async Task WaitForApplicationAsync(Uri baseAddress, string repositoryRoot, string containerName)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(10);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(baseAddress);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                lastError = new InvalidOperationException($"HTTP {(int)response.StatusCode} from {baseAddress}.");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        var diagnostics = await TryGetContainerLogsAsync(repositoryRoot, containerName);
        throw new TimeoutException(
            $"The legacy-web container did not return HTTP 200 at {baseAddress}.{Environment.NewLine}{diagnostics}",
            lastError);
    }

    private static async Task<string> TryGetContainerLogsAsync(string repositoryRoot, string containerName)
    {
        try
        {
            return await RunDockerAsync(repositoryRoot, false, "logs", containerName);
        }
        catch (Exception exception)
        {
            return $"Could not collect container logs: {exception.Message}";
        }
    }

    private static async Task<string> ResolveDockerEndpointHostAsync(string repositoryRoot)
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (TryGetTcpHost(dockerHost, out var host))
        {
            return host;
        }

        try
        {
            var contextName = await RunDockerAsync(repositoryRoot, false, "context", "show");
            var endpoint = await RunDockerAsync(
                repositoryRoot,
                false,
                "context",
                "inspect",
                "--format",
                "{{(index .Endpoints \"docker\").Host}}",
                contextName.Trim());

            if (TryGetTcpHost(endpoint, out host))
            {
                return host;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not inspect the Docker context; using localhost: {exception.Message}");
        }

        return "127.0.0.1";
    }

    private static bool TryGetTcpHost(string? endpoint, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint) ||
            !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ||
            uri.Host.Length == 0 ||
            uri.Scheme is not ("tcp" or "http" or "https"))
        {
            return false;
        }

        host = uri.Host;
        return true;
    }

    private static Uri? ReadConfiguredBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable("LEGACY_WEB_BASE_ADDRESS");
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }

    private static int ParsePublishedPort(string output)
    {
        var match = Regex.Match(output, @":(?<port>\d+)\s*$", RegexOptions.Multiline);
        if (!match.Success || !int.TryParse(match.Groups["port"].Value, out var port))
        {
            throw new InvalidOperationException($"Could not determine the published container port from: {output}");
        }

        return port;
    }

    private static async Task<string> RunDockerAsync(string workingDirectory, params string[] arguments) =>
        await RunDockerAsync(workingDirectory, true, arguments);

    private static async Task<string> RunDockerAsync(
        string workingDirectory,
        bool emitOutput,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start the Docker CLI.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var timedOut = false;

        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
        {
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
            }
        }

        var output = await outputTask;
        var error = await errorTask;

        if (emitOutput && output.Length > 0)
        {
            Console.WriteLine(output.TrimEnd());
        }

        if (emitOutput && error.Length > 0)
        {
            Console.Error.WriteLine(error.TrimEnd());
        }

        if (timedOut)
        {
            throw new TimeoutException($"Docker command timed out: docker {string.Join(' ', arguments)}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {process.ExitCode}: docker {string.Join(' ', arguments)}{Environment.NewLine}{error}");
        }

        return output.Trim();
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("LEGACY_WEB_REPOSITORY_ROOT");
        var startingDirectories = new[]
        {
            configuredRoot,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startingDirectory in startingDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(startingDirectory!);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Dockerfile")) &&
                    File.Exists(Path.Combine(directory.FullName, "aspnetapp.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find the repository root. Set LEGACY_WEB_REPOSITORY_ROOT to the uploaded project directory.");
    }

    private static bool IsEnabled(string variableName) =>
        Environment.GetEnvironmentVariable(variableName) is "1" or "true" or "TRUE" or "yes" or "YES";
}
