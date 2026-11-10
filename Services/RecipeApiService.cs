using System.Net.Http.Json;
using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

public class RecipeApiService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ApiProcessManager _apiProcessManager;

    public RecipeApiService(
        HttpClient httpClient,
        ApiProcessManager apiProcessManager)
    {
        _httpClient = httpClient;
        _apiProcessManager = apiProcessManager;
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

    public async Task<List<IngredientSuggestion>> SearchIngredientsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await _apiProcessManager.EnsureStartedAsync(cancellationToken);

        var requestUri = new Uri(
            ApiSettings.GetBaseAddress(),
            $"api/ingredients/search?query={Uri.EscapeDataString(query.Trim())}");

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<IngredientSuggestion>>(
                   JsonOptions,
                   cancellationToken)
               ?? throw new JsonException(
                   "The ingredient search API returned an empty JSON response.");
    }

    public async Task<RecipeRecommendationResponse> GetRecommendationsAsync(
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

        await _apiProcessManager.EnsureStartedAsync(cancellationToken);
        var requestUri = new Uri(
            ApiSettings.GetBaseAddress(),
            "api/recipes/recommendations");

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri,
            new RecipeRecommendationRequest { IngredientIds = ingredientIds },
            JsonOptions,
            cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<RecipeRecommendationResponse>(
                   JsonOptions,
                   cancellationToken)
               ?? throw new JsonException(
                   "The recipe recommendations API returned an empty JSON response.");
    }

    private async Task<List<RecipeSearchResult>> SearchAsync(
        RecipeSearchRequest request,
        CancellationToken cancellationToken)
    {
        await _apiProcessManager.EnsureStartedAsync(cancellationToken);
        var requestUri = new Uri(
            ApiSettings.GetBaseAddress(),
            "api/recipes/search");

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri,
            request,
            JsonOptions,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSearchResult>>(
            JsonOptions,
            cancellationToken);

        return recipes ?? throw new JsonException(
            "The recipe search API returned an empty JSON response.");
    }

    private static async Task EnsureSuccessStatusCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = string.IsNullOrWhiteSpace(responseBody)
                ? string.Empty
                : $" {responseBody[..Math.Min(responseBody.Length, 500)]}";

            throw new HttpRequestException(
                $"PantryPal API request failed with HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}).{detail}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}
