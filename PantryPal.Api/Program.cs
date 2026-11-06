using PantryPal.Api.Data;
using PantryPal.Api.Models;
using PantryPal.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<RecipeService>();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/api/recipes", async (RecipeService recipeService) =>
{
    var recipes = await recipeService.GetAllRecipesAsync();

    return Results.Ok(recipes);
});

app.MapPost("/api/recipes/search", async (
    RecipeSearchRequest request,
    RecipeService recipeService) =>
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
        ingredientNames);

    return Results.Ok(recipes);
});

app.MapGet("/health/database", async (
    SqlConnectionFactory sqlFactory,
    ILogger<Program> logger) =>
{
    try
    {
        await using var connection =
            await sqlFactory.CreateConnectionAsync();

        await connection.OpenAsync();

        await using var command =
            new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT DB_NAME()", connection);

        var database = await command.ExecuteScalarAsync();

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
