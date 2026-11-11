using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Tests;

public sealed class ApiIntegrationFactAttribute : FactAttribute
{
    public ApiIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("PANTRYPAL_AUTOCOMPLETE_API_URL")))
        {
            Skip = "Set PANTRYPAL_AUTOCOMPLETE_API_URL to run Azure SQL autocomplete integration tests.";
        }
    }
}

public sealed class IngredientAutocompleteApiTests
{
    [ApiIntegrationFact]
    public Task SearchOni_RanksOnionWordsBeforeOtherSubstrings() =>
        AssertRankedAsync("oni", mustContainName: null);

    [ApiIntegrationFact]
    public Task SearchOnion_ReturnsOnionIngredients() =>
        AssertRankedAsync("onion", mustContainName: "onion");

    [ApiIntegrationFact]
    public Task SearchGarlic_ReturnsGarlicIngredients() =>
        AssertRankedAsync("garlic", mustContainName: "garlic");

    [ApiIntegrationFact]
    public Task SearchChicken_ReturnsChickenIngredients() =>
        AssertRankedAsync("chicken", mustContainName: "chicken");

    private async Task AssertRankedAsync(string query, string? mustContainName)
    {
        using var client = new HttpClient();
        var baseAddress = Environment.GetEnvironmentVariable(
            "PANTRYPAL_AUTOCOMPLETE_API_URL")!;
        var requestUri = new Uri(
            new Uri(baseAddress, UriKind.Absolute),
            $"api/ingredients/search?query={Uri.EscapeDataString(query)}");

        using var response = await client.GetAsync(requestUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<IngredientSuggestion>>();
        var suggestions = IngredientSuggestion.Consolidate(
            body ?? throw new JsonException("API returned an empty autocomplete response."));
        Assert.NotEmpty(suggestions);

        var names = suggestions.Select(item => item.IngredientName).ToArray();
        if (mustContainName is not null)
        {
            Assert.Contains(names, name => string.Equals(
                name,
                mustContainName,
                StringComparison.OrdinalIgnoreCase));
        }

        var firstWordMatch = Array.FindIndex(names, name => IsWordMatch(name, query));
        var firstPartialMatch = Array.FindIndex(names, name =>
            name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !IsWordMatch(name, query));

        Assert.True(firstWordMatch >= 0, $"No word/prefix match for '{query}' was returned.");
        if (firstPartialMatch >= 0)
        {
            Assert.True(
                firstWordMatch < firstPartialMatch,
                $"Substring-only match '{names[firstPartialMatch]}' ranked before a word match.");
        }
    }

    private static bool IsWordMatch(string name, string query)
    {
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 1; index <= name.Length - query.Length; index++)
        {
            if (string.Equals(
                    name.Substring(index, query.Length),
                    query,
                    StringComparison.OrdinalIgnoreCase) &&
                !char.IsLetterOrDigit(name[index - 1]))
            {
                return true;
            }
        }

        return false;
    }
}
