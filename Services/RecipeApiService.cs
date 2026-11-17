using System.Net.Http.Json;
using PantryPal.Models;

namespace PantryPal.Services;

// talks to the PantryPal.Api website, the api is what reads the azure database
// the app never connects to the database itself, so no passwords or keys live in the app
public class RecipeApiService
{
    // HttpClient sends web requests, one is shared by the whole app
    // the timeout is long because the database can take about 45 seconds to wake up
    private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    private readonly Uri _apiBaseAddress;

    public RecipeApiService(Uri apiBaseAddress)
    {
        _apiBaseAddress = apiBaseAddress;
    }

    // asks the database for ingredient names that match what was typed
    public async Task<List<string>> SearchIngredientsAsync(string text)
    {
        // EscapeDataString makes spaces and symbols safe to put in a web address
        var url = new Uri(
            _apiBaseAddress,
            "api/ingredients/search?query=" + Uri.EscapeDataString(text));

        // downloads the json and turns it into a list of IngredientResult objects
        List<IngredientResult>? results = await Client.GetFromJsonAsync<List<IngredientResult>>(url);

        // keeps just the names
        var names = new List<string>();
        if (results == null) return names;

        foreach (IngredientResult result in results)
        {
            names.Add(result.IngredientName);
        }
        return names;
    }

    // finds recipes that use these ingredient names, the api puts the best matches first
    public async Task<List<RecipeSearchResult>> FindRecipesAsync(List<string> ingredientNames)
    {
        var url = new Uri(_apiBaseAddress, "api/recipes/search");

        // PostAsJsonAsync turns the request into json like { "ingredientNames": ["eggs", "milk"] } and sends it
        var request = new RecipeRequest { IngredientNames = ingredientNames };
        HttpResponseMessage response = await Client.PostAsJsonAsync(url, request);

        // stops here with an error if the api didn't send back recipes
        response.EnsureSuccessStatusCode();

        // turns the json reply into a list of RecipeSearchResult objects
        List<RecipeSearchResult>? recipes = await response.Content.ReadFromJsonAsync<List<RecipeSearchResult>>();
        if (recipes == null) return new List<RecipeSearchResult>();
        return recipes;
    }

    public async Task<RecipeRecommendationResponse> GetRecommendationsAsync(
        List<string> ingredientNames)
    {
        if (ingredientNames.Count == 0)
        {
            throw new ArgumentException(
                "At least one pantry ingredient is required.",
                nameof(ingredientNames));
        }

        var url = new Uri(_apiBaseAddress, "api/recipes/recommendations");
        var request = new RecommendationRequest
        {
            IngredientNames = ingredientNames
        };
        HttpResponseMessage response =
            await Client.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Recipe recommendation request failed ({(int)response.StatusCode}): " +
                detail,
                null,
                response.StatusCode);
        }

        var recommendations =
            await response.Content.ReadFromJsonAsync<RecipeRecommendationResponse>();
        return recommendations ?? new RecipeRecommendationResponse();
    }

    // one ingredient from the search, the api also sends ids but only the name is needed
    private class IngredientResult
    {
        public string IngredientName { get; set; } = "";
    }

    // what gets sent to the api when searching for recipes
    private class RecipeRequest
    {
        public List<string> IngredientNames { get; set; } = new List<string>();
    }

    private class RecommendationRequest
    {
        public List<int>? IngredientIds { get; set; }
        public List<string> IngredientNames { get; set; } = new();
    }
}
