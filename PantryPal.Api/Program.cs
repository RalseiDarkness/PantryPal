#pragma warning disable OPENAI001
using Azure.Identity;
using Azure.Core;
using System.ClientModel.Primitives;
using OpenAI.Responses;
using PantryPal.Api.Data;
using PantryPal.Api.Middleware;
using PantryPal.Api.Models;
using PantryPal.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Local development supports multiple authentication providers:
// Visual Studio on Windows, VS Code on Mac, Azure CLI, etc.
// Deployed instances use their Azure Managed Identity.
builder.Services.AddSingleton<TokenCredential>(_ =>
    builder.Environment.IsDevelopment()
        ? new DefaultAzureCredential()
        : new ManagedIdentityCredential(
            new ManagedIdentityCredentialOptions()));

// Create the Foundry client only when first requested, after configuration and
// credentials are available through dependency injection.
builder.Services.AddSingleton(serviceProvider =>
    new Lazy<ResponsesClient>(() =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var endpointValue = configuration["AzureOpenAI:Endpoint"];
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Azure AI Foundry endpoint is not configured as an HTTPS URL.");
        }

        var credential = serviceProvider.GetRequiredService<TokenCredential>();
        var options = new ResponsesClientOptions
        {
            Endpoint = endpoint,
            NetworkTimeout = TimeSpan.FromSeconds(60)
        };

        return new ResponsesClient(
            new BearerTokenPolicy(credential, "https://ai.azure.com/.default"),
            options);
    }));
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<RecipeService>();
// The same SQL-backed service supplies ingredient lookup, matching, and detail data.
builder.Services.AddScoped<IRecipeRecommendationDataSource>(
    serviceProvider => serviceProvider.GetRequiredService<RecipeService>());
builder.Services.AddScoped<RecipeRecommendationService>();
builder.Services.AddScoped<RecipeDetailAnalysisService>();
builder.Services.AddScoped<AzureAiRecipeAnalysisService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
// Add services to the container.
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();

// This liveness endpoint intentionally avoids outbound calls to Azure services.
app.MapGet("/health", () => Results.Ok(new { status = "Ready" }))
    .WithName("GetApiHealth")
    .WithSummary("Check whether the API process is ready.")
    .WithDescription("Returns readiness without connecting to Azure SQL or Azure AI.")
    .Produces(StatusCodes.Status200OK);

// Simple catalog endpoint retained alongside the filtered recipe-search route.
app.MapGet("/api/recipes", async (
    RecipeService recipeService,
    CancellationToken cancellationToken) =>
{
    var recipes = await recipeService.GetAllRecipesAsync(cancellationToken);

    return Results.Ok(recipes);
});

// Ingredient autocomplete is served from SQL; the client calls this after typing.
app.MapGet("/api/ingredients/search", async (
    string? query,
    RecipeService recipeService,
    CancellationToken cancellationToken,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(query) || query.Trim().Length > 100)
    {
        return Results.BadRequest(new
        {
            error = "Provide an ingredient search query of 1 to 100 characters."
        });
    }

    try
    {
        var suggestions = await recipeService.SearchIngredientsAsync(
            query,
            cancellationToken);

        return Results.Ok(suggestions);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        logger.LogDebug(
            "Ingredient search request was canceled by the client.");

        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
})
.WithName("SearchIngredients")
.WithSummary("Search ingredient names for autocomplete suggestions.")
.WithDescription(
    "Returns up to 10 case-insensitive matches, prioritizing names and words that start with the query.")
.Produces<List<IngredientSuggestion>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

// Lightweight SQL-only search remains available independently of AI recommendations.
app.MapPost("/api/recipes/search", async (
    RecipeSearchRequest request,
    RecipeService recipeService,
    CancellationToken cancellationToken) =>
{
    var hasIds = request.IngredientIds is not null;
    var hasNames = request.IngredientNames is not null;

    if (hasIds == hasNames ||
        request.IngredientIds is { Count: 0 } ||
        request.IngredientNames is { Count: 0 })
    {
        return Results.BadRequest(new
        {
            error = "Provide exactly one non-empty ingredientIds or ingredientNames list."
        });
    }

    if (request.IngredientIds?.Any(id => id <= 0) == true)
    {
        return Results.BadRequest(new
        {
            error = "Ingredient IDs must be positive."
        });
    }

    if (request.IngredientNames?.Any(string.IsNullOrWhiteSpace) == true)
    {
        return Results.BadRequest(new
        {
            error = "Ingredient names must not be empty."
        });
    }

    var ingredientIds = request.IngredientIds?.Distinct().ToArray() ?? [];
    var ingredientNames = request.IngredientNames?
        .Select(name => name.Trim())
        .ToArray() ?? [];

    var recipes = await recipeService.GetMatchingRecipesAsync(
        ingredientIds,
        ingredientNames,
        cancellationToken,
        RecipeRecommendationService.MaximumRecipeResults);

    return Results.Ok(recipes);
});

// Full AI analysis is deferred until a user opens a matching recipe.
app.MapPost("/api/recipes/{recipeId:int}/analysis", async (
    int recipeId,
    RecipeDetailAnalysisRequest request,
    RecipeDetailAnalysisService analysisService,
    CancellationToken cancellationToken) =>
{
    if (recipeId <= 0)
    {
        return Results.BadRequest(new
        {
            error = "Recipe ID must be positive."
        });
    }

    if (request.IngredientNames is null ||
        request.IngredientNames.Count == 0 ||
        request.IngredientNames.Any(string.IsNullOrWhiteSpace))
    {
        return Results.BadRequest(new
        {
            error = "Provide at least one non-empty pantry ingredient name."
        });
    }

    try
    {
        var recipe = await analysisService.AnalyzeRecipeAsync(
            recipeId,
            request.IngredientNames
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);

        return recipe is null
            ? Results.NotFound(new { error = "Recipe not found in the matching results." })
            : Results.Ok(recipe);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
})
.WithName("GetRecipeDetailAnalysis")
.WithSummary("Retrieve full AI analysis for one matching recipe.")
.WithDescription(
    "Loads the recipe from Azure SQL and generates or retrieves its pantry-specific AI analysis.")
.Produces<RecipeSearchResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status500InternalServerError);

// Initial results stay SQL-backed; only the compact complexity classification is
// requested here. RecipeDetailAnalysisService handles the full analysis later.
app.MapPost("/api/recipes/recommendations", async (
    RecipeRecommendationRequest request,
    RecipeRecommendationService recommendationService,
    CancellationToken cancellationToken) =>
{
    var hasIds = request.IngredientIds is not null &&
        request.IngredientIds.Count > 0;
    var hasNames = request.IngredientNames is not null &&
        request.IngredientNames.Count > 0;

    if (hasIds == hasNames)
    {
        return Results.BadRequest(new
        {
            error = "Provide exactly one non-empty ingredientIds or ingredientNames list."
        });
    }

    RecipeRecommendationResponse recommendations;
    if (hasIds)
    {
        var ingredientIds = request.IngredientIds!;
        if (ingredientIds.Any(id => id <= 0))
        {
            return Results.BadRequest(new
            {
                error = "Ingredient IDs must be positive."
            });
        }

        recommendations = await recommendationService.GetRecommendationsAsync(
            ingredientIds.Distinct().ToArray(),
            cancellationToken);
    }
    else
    {
        var ingredientNames = request.IngredientNames!;
        if (ingredientNames.Any(string.IsNullOrWhiteSpace))
        {
            return Results.BadRequest(new
            {
                error = "Ingredient names must not be empty."
            });
        }

        recommendations = await recommendationService.GetRecommendationsAsync(
            ingredientNames
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);
    }

    return Results.Ok(recommendations);
})
.WithName("GetRecipeRecommendations")
.WithSummary("Find up to 100 matching recipes and classify their complexity.")
.WithDescription(
    "Searches Azure SQL for up to 100 matching recipes and requests batched AI complexity ratings. Full AI analysis is generated when an individual recipe is opened.")
.Produces<RecipeRecommendationResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

// Unlike /health, this diagnostic deliberately opens a SQL connection.
app.MapGet("/health/database", async (
    SqlConnectionFactory sqlFactory,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection =
            await sqlFactory.CreateConnectionAsync(cancellationToken);

        await using var command =
            new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT DB_NAME()", connection);

        var database = await command.ExecuteScalarAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "Connected",
            database
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database connection test failed.");

        return Results.Problem(
            "Could not connect to the database.",
            statusCode: 503);
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// ASP.NET template smoke-test endpoint; recipe flows use the routes documented above.
app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
#pragma warning restore OPENAI001
