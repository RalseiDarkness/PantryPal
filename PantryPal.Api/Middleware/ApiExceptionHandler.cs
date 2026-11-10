using Azure.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PantryPal.Api.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            SqlException or AuthenticationFailedException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Service unavailable",
                    "The database is temporarily unavailable."),
            TimeoutException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "Service unavailable",
                    "The database is temporarily unavailable."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest,
                    "Invalid request",
                    exception.Message),
            _ => (StatusCodes.Status500InternalServerError,
                "An error occurred while processing your request.",
                "The server encountered an unexpected error.")
        };

        if (statusCode == 0)
        {
            return false;
        }

        _logger.LogError(exception, "API request failed with status {StatusCode}.", statusCode);
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
