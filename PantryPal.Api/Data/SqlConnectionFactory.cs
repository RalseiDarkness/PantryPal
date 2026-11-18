
using System.Diagnostics;
using Azure.Core;
using Microsoft.Data.SqlClient;

namespace PantryPal.Api.Data;

/// Opens Azure SQL connections using the API's Entra ID credential and bounded
/// retries for transient database failures.
public class SqlConnectionFactory
{
    // Bound token acquisition, retries, and backoff together so a paused or
    // unavailable database cannot hold an API request open indefinitely.
    private static readonly TimeSpan ConnectionWindow = TimeSpan.FromSeconds(100);
    private static readonly TimeSpan ConnectionAttemptTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(10);

    private readonly TokenCredential _credential;
    private readonly ILogger<SqlConnectionFactory> _logger;

    private const string Server =
        "grantsazureserver.database.windows.net";

    private const string Database =
        "Sophomore-Project-DB";

    /// Creates a connection factory using the configured identity and logger.
    public SqlConnectionFactory(
        TokenCredential credential,
        ILogger<SqlConnectionFactory> logger)
    {
        _credential = credential;
        _logger = logger;
    }

    /// Acquires a SQL access token and opens a connection within the overall
    /// connection window. The caller owns and must dispose the returned connection.
    public async Task<SqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        connectionTimeout.CancelAfter(ConnectionWindow);

        try
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(
                    new[] { "https://database.windows.net/.default" }),
                connectionTimeout.Token);

            var retryAttempt = 0;

            // Azure SQL may be paused or briefly unavailable. Retry only classified
            // transient errors, while the linked token bounds the whole operation.
            while (true)
            {
                connectionTimeout.Token.ThrowIfCancellationRequested();

                var remaining = ConnectionWindow - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        $"Could not connect to Azure SQL within {ConnectionWindow.TotalSeconds} seconds.");
                }

                var attemptTimeout = remaining < ConnectionAttemptTimeout
                    ? remaining
                    : ConnectionAttemptTimeout;

                var connectionString = new SqlConnectionStringBuilder
                {
                    DataSource = $"tcp:{Server},1433",
                    InitialCatalog = Database,
                    Encrypt = true,
                    TrustServerCertificate = false,
                    ConnectTimeout = (int)Math.Ceiling(
                        attemptTimeout.TotalSeconds)
                }.ConnectionString;

                var connection = new SqlConnection(connectionString)
                {
                    AccessToken = token.Token
                };

                try
                {
                    await connection.OpenAsync(connectionTimeout.Token);
                    return connection;
                }
                catch (SqlException ex) when (IsTransient(ex))
                {
                    await connection.DisposeAsync();

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (connectionTimeout.IsCancellationRequested ||
                        stopwatch.Elapsed >= ConnectionWindow)
                    {
                        throw new TimeoutException(
                            $"Azure SQL did not become available within {ConnectionWindow.TotalSeconds} seconds.",
                            ex);
                    }

                    var retryDelay = GetRetryDelay(retryAttempt++);

                    _logger.LogWarning(
                        ex,
                        "Azure SQL connection attempt {Attempt} failed transiently. " +
                        "Retrying in {RetryDelaySeconds} seconds; {RemainingSeconds:F1} seconds remain.",
                        retryAttempt,
                        retryDelay.TotalSeconds,
                        (ConnectionWindow - stopwatch.Elapsed).TotalSeconds);

                    await Task.Delay(retryDelay, connectionTimeout.Token);
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Azure SQL connection failed after {ElapsedSeconds:F1} seconds.",
                stopwatch.Elapsed.TotalSeconds);

            if (ex is OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Azure SQL connection timed out after {ConnectionWindow.TotalSeconds} seconds.",
                    ex);
            }

            throw;
        }
    }

    private static TimeSpan GetRetryDelay(int retryAttempt)
    {
        // Exponential backoff is capped so repeated transient errors cannot leave
        // a request sleeping longer than the configured maximum delay.
        var seconds = Math.Min(
            InitialRetryDelay.TotalSeconds * Math.Pow(2, retryAttempt),
            MaximumRetryDelay.TotalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }

    private static bool IsTransient(SqlException exception)
    {
        // Keep permanent authentication, permission, and configuration failures
        // visible immediately instead of retrying them as if they were outages.
        return exception.Errors
            .Cast<SqlError>()
            .Any(error => error.Number is
                -2 or
                64 or
                233 or
                1205 or
                40613 or
                40197 or
                40501 or
                40540 or
                10928 or
                10929 or
                10053 or
                10054 or
                10060);
    }
}
