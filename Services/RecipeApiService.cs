using System.Net.Http.Json;
using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

public class RecipeApiService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public RecipeApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<RecipeSearchResult>> SearchByIngredientNamesAsync(
        IReadOnlyCollection<string> ingredientNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ingredientNames);
        if (ingredientNames.Count == 0)
        {
            throw new ArgumentException(
                "At least one ingredient name is required.",
                nameof(ingredientNames));
        }

        return SearchAsync(
            new RecipeSearchRequest { IngredientNames = ingredientNames },
            cancellationToken);
    }

    public Task<List<RecipeSearchResult>> SearchByIngredientIdsAsync(
        IReadOnlyCollection<int> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ingredientIds);
        if (ingredientIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one ingredient ID is required.",
                nameof(ingredientIds));
        }

        return SearchAsync(
            new RecipeSearchRequest { IngredientIds = ingredientIds },
            cancellationToken);
    }

    private async Task<List<RecipeSearchResult>> SearchAsync(
        RecipeSearchRequest request,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(
            ApiSettings.GetBaseAddress(),
            "api/recipes/search");

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri,
            request,
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = string.IsNullOrWhiteSpace(responseBody)
                ? string.Empty
                : $" {responseBody[..Math.Min(responseBody.Length, 500)]}";

            throw new HttpRequestException(
                $"Recipe search failed with HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}).{detail}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSearchResult>>(
            JsonOptions,
            cancellationToken);

        return recipes ?? throw new JsonException(
            "The recipe search API returned an empty JSON response.");
    }
}
