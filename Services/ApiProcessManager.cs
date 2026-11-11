using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PantryPal.Services;

public sealed class ApiProcessManager
{
#if DEBUG && (MACCATALYST || WINDOWS)
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private readonly HttpClient _healthClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };
    private readonly ILogger<ApiProcessManager> _logger;
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly object _processLock = new();
    private Process? _ownedProcess;
    private bool _isShuttingDown;
    private bool _apiReady;

    public ApiProcessManager(ILogger<ApiProcessManager> logger)
    {
        _logger = logger;
    }
#else
    public ApiProcessManager(ILogger<ApiProcessManager> logger)
    {
        _ = logger;
    }
#endif

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG && (MACCATALYST || WINDOWS)
        await _startupLock.WaitAsync(cancellationToken);
        try
        {
            Process? exitedProcess = null;
            lock (_processLock)
            {
                if (_isShuttingDown)
                {
                    throw new InvalidOperationException(
                        "The local PantryPal API cannot start because the app is shutting down.");
                }

                if (_ownedProcess is { HasExited: true } exited)
                {
                    exitedProcess = exited;
                    _ownedProcess = null;
                    _apiReady = false;
                }

                if (_apiReady &&
                    _ownedProcess is { HasExited: false })
                {
                    return;
                }
            }

            exitedProcess?.Dispose();

            var apiAddress = ApiSettings.GetBaseAddress();
            if (!apiAddress.IsLoopback)
            {
                return;
            }

            var healthAddress = new Uri(apiAddress, "health");
            if (await IsHealthyAsync(healthAddress, _shutdownCancellation.Token))
            {
                _logger.LogInformation(
                    "Reusing the PantryPal API already responding at {ApiAddress}.",
                    apiAddress);
                lock (_processLock)
                {
                    _apiReady = true;
                }

                return;
            }

            var projectPath = FindApiProjectPath();
            if (projectPath is null)
            {
                throw new InvalidOperationException(
                    $"Could not locate PantryPal.Api/PantryPal.Api.csproj from app output directory '{AppContext.BaseDirectory}'.");
            }

            var process = StartApiProcess(projectPath, apiAddress);
            try
            {
                await WaitForReadyAsync(process, healthAddress);
                lock (_processLock)
                {
                    if (_isShuttingDown)
                    {
                        throw new OperationCanceledException(_shutdownCancellation.Token);
                    }

                    _apiReady = true;
                }

                _logger.LogInformation(
                    "The local PantryPal API is ready at {ApiAddress}.",
                    apiAddress);
            }
            catch
            {
                StopOwnedProcess(process);
                throw;
            }
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not start or connect to the local PantryPal API.");
            throw new InvalidOperationException(
                "Could not start the local PantryPal API. Check the app logs for process output and startup errors.",
                ex);
        }
        finally
        {
            _startupLock.Release();
        }
#else
        await Task.CompletedTask;
#endif
    }

    public void StopOwnedProcess()
    {
#if DEBUG && (MACCATALYST || WINDOWS)
        lock (_processLock)
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _apiReady = false;
        }

        _shutdownCancellation.Cancel();
        Process? process;
        lock (_processLock)
        {
            process = _ownedProcess;
            _ownedProcess = null;
        }

        TerminateProcess(process);
#endif
    }

#if DEBUG && (MACCATALYST || WINDOWS)
    private Process StartApiProcess(string projectPath, Uri apiAddress)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException(
                $"Could not determine the API project directory for '{projectPath}'.");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(apiAddress.AbsoluteUri);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                _logger.LogDebug("PantryPal API: {ApiOutput}", args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                _logger.LogWarning("PantryPal API: {ApiError}", args.Data);
            }
        };

        var started = false;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The .NET SDK process did not start.");
            }

            started = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            lock (_processLock)
            {
                if (_isShuttingDown)
                {
                    throw new OperationCanceledException(_shutdownCancellation.Token);
                }

                _ownedProcess = process;
            }

            _logger.LogInformation(
                "Started local PantryPal API process {ProcessId} from {ProjectPath}.",
                process.Id,
                projectPath);
            return process;
        }
        catch
        {
            if (started)
            {
                TerminateProcess(process);
            }
            else
            {
                process.Dispose();
            }

            throw;
        }
    }

    private async Task WaitForReadyAsync(Process process, Uri healthAddress)
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            _shutdownCancellation.Token);

        try
        {
            while (!linkedCancellation.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The local PantryPal API process exited with code {process.ExitCode} before becoming ready.");
                }

                if (await IsHealthyAsync(healthAddress, linkedCancellation.Token))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), linkedCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested &&
            !_shutdownCancellation.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The local PantryPal API did not become ready at '{healthAddress}' within {StartupTimeout.TotalSeconds:0} seconds.");
        }

        if (timeout.IsCancellationRequested &&
            !_shutdownCancellation.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The local PantryPal API did not become ready at '{healthAddress}' within {StartupTimeout.TotalSeconds:0} seconds.");
        }

        linkedCancellation.Token.ThrowIfCancellationRequested();
    }

    private async Task<bool> IsHealthyAsync(
        Uri healthAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _healthClient.GetAsync(
                healthAddress,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static string? FindApiProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(
                directory.FullName,
                "PantryPal.Api",
                "PantryPal.Api.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void StopOwnedProcess(Process process)
    {
        lock (_processLock)
        {
            if (ReferenceEquals(_ownedProcess, process))
            {
                _ownedProcess = null;
                _apiReady = false;
            }
        }

        TerminateProcess(process);
    }

    private void TerminateProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                _logger.LogInformation(
                    "Stopping PantryPal API process {ProcessId} started by this app.",
                    process.Id);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not stop PantryPal API process {ProcessId}.",
                process.Id);
        }
        finally
        {
            process.Dispose();
        }
    }
#endif
}
