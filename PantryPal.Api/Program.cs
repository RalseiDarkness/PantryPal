using Azure.Identity;
using Azure.Core;
using PantryPal.Api.Data;
using PantryPal.Api.Middleware;
using PantryPal.Api.Models;
using PantryPal.Api.Services;

var builder = WebApplication.CreateBuilder(args);
// Local development: use the Azure CLI account verified with
// `az account get-access-token --resource https://database.windows.net/`.
builder.Services.AddSingleton<TokenCredential>(_ => new AzureCliCredential());
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<RecipeRecommendationService>();
builder.Services.AddHttpClient<AzureAiRecipeAnalysisService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/api/recipes", async (
    RecipeService recipeService,
    CancellationToken cancellationToken) =>
{
    var recipes = await recipeService.GetAllRecipesAsync(cancellationToken);

    return Results.Ok(recipes);
});

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
        cancellationToken);

    return Results.Ok(recipes);
});

app.MapPost("/api/recipes/recommendations", async (
    RecipeRecommendationRequest request,
    RecipeRecommendationService recommendationService,
    CancellationToken cancellationToken) =>
{
    var ingredientIds = request.IngredientIds;
    if (ingredientIds is null ||
        ingredientIds.Count == 0 ||
        ingredientIds.Any(id => id <= 0))
    {
        return Results.BadRequest(new
        {
            error = "Provide one or more positive ingredient IDs."
        });
    }

    var recommendations = await recommendationService.GetRecommendationsAsync(
        ingredientIds,
        cancellationToken);

    return Results.Ok(recommendations);
})
.WithName("GetRecipeRecommendations")
.WithSummary("Find matching recipes and request AI recommendations.")
.WithDescription(
    "Searches Azure SQL for matching recipes, then asks Azure AI Foundry to analyze only the top 10 results.")
.Produces<RecipeRecommendationResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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
