#pragma warning disable OPENAI001
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using PantryPal.Api.Models;
using PantryPal.Api.Services;

namespace PantryPal.Tests;

/// Verifies on-demand analysis caching and web-research fallback using a mocked
/// Responses API transport.

public class RecipeDetailAnalysisServiceTests
{
    private const string Endpoint =
        "https://SophomoreProject-AI.services.ai.azure.com/openai/v1";
    private const string Deployment = "gpt-4o";

    // The same recipe/pantry pair reuses its analysis; changing the pantry causes
    // a separate research-plus-analysis request pair.
    [Fact]
    public async Task AnalyzeRecipeAsync_CachesByRecipeAndPantryContext()
    {
        var handler = new ResponsesHandler(
            CreateAnalysisResponse(),
            CreateResponseBody(
                "The dish has a supported origin. Source: Culinary History Society, https://example.org/omelet-origin",
                includeWebSearchCall: true));
        var recipe = CreateRecipe();
        var dataSource = new FakeRecipeDataSource([recipe]);
        var service = CreateService(
            dataSource,
            handler,
            new MemoryCache(Options.Create(new MemoryCacheOptions())));

        var firstResult = await service.AnalyzeRecipeAsync(42, ["eggs"]);
        var sameContextResult = await service.AnalyzeRecipeAsync(42, ["eggs"]);

        Assert.Equal("An egg omelet.", firstResult!.Overview);
        Assert.Equal("The dish has a supported origin.", firstResult.RegionOfOrigin);
        Assert.Equal("Culinary History Society", Assert.Single(firstResult.OriginSources).Title);
        Assert.Same(recipe, sameContextResult);
        Assert.True(handler.RequestBodies.Count >= 2);
        using var analysisRequest = JsonDocument.Parse(handler.RequestBodies[^1]);
        Assert.False(analysisRequest.RootElement.TryGetProperty("tools", out _));

        await service.AnalyzeRecipeAsync(42, ["eggs", "cheese"]);

        Assert.Equal(4, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task AnalyzeRecipeAsync_ContinuesWhenOriginResearchFails()
    {
        var handler = new ResponsesHandler(
            CreateAnalysisResponse(
                "Unknown; origin research was unavailable.",
                includeOriginSource: false),
            """{"error":{"message":"Search unavailable","type":"server_error"}}""",
            HttpStatusCode.ServiceUnavailable);
        var service = CreateService(
            new FakeRecipeDataSource([CreateRecipe()]),
            handler,
            new MemoryCache(Options.Create(new MemoryCacheOptions())));

        var result = await service.AnalyzeRecipeAsync(42, ["eggs"]);

        Assert.NotNull(result);
        Assert.Equal("Unknown; origin research was unavailable.", result.RegionOfOrigin);
        Assert.Empty(result.OriginSources);
        using var analysisRequest = JsonDocument.Parse(handler.RequestBodies[^1]);
        var inputText = analysisRequest.RootElement
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("Origin research was unavailable", inputText);
        Assert.True(handler.RequestBodies.Count >= 2);
    }

    // Use in-memory configuration, a fake token, and a fake SQL source so no
    // Azure services are required to test the cache behavior.
    private static RecipeDetailAnalysisService CreateService(
        IRecipeRecommendationDataSource dataSource,
        ResponsesHandler handler,
        IMemoryCache cache)
    {
        var client = new ResponsesClient(
            new BearerTokenPolicy(
                new StaticTokenCredential(),
                "https://ai.azure.com/.default"),
            new ResponsesClientOptions
            {
                Endpoint = new Uri(Endpoint),
                Transport = new HttpClientPipelineTransport(new HttpClient(handler))
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = Endpoint,
                ["AzureOpenAI:Deployment"] = Deployment
            })
            .Build();
        var aiService = new AzureAiRecipeAnalysisService(
            new Lazy<ResponsesClient>(() => client),
            configuration,
            NullLogger<AzureAiRecipeAnalysisService>.Instance);
        return new RecipeDetailAnalysisService(dataSource, aiService, cache);
    }

    private static string CreateAnalysisResponse(
        string regionOfOrigin = "The dish has a supported origin.",
        bool includeOriginSource = true)
    {
        var originSources = includeOriginSource
            ? """[{"title":"Culinary History Society","url":"https://example.org/omelet-origin"}]"""
            : "[]";
        return CreateResponseBody(
            $$"""
              {
                "bestRecipeId": 42,
                "recommendation": "Best match.",
                "recipes": [{
                  "recipeId": 42,
                  "overview": "An egg omelet.",
                  "whyItMatches": "It uses eggs.",
                  "matchAssessment": "Cheese is missing.",
                  "difficulty": "Easy",
                  "regionOfOrigin": "{{regionOfOrigin}}",
                  "mealHeaviness": "Moderate",
                  "mealHeavinessExplanation": "Based on the ingredients and preparation.",
                  "potentialAllergens": ["eggs"],
                  "allergenAssessment": "Eggs are present; cross-contamination cannot be determined.",
                  "allergenExplanations": [{
                    "allergen": "eggs",
                    "triggeringIngredient": "eggs",
                    "explanation": "Eggs are a direct source.",
                    "certainty": "Direct"
                  }],
                  "otherConsiderations": "Unknown.",
                  "originSources": {{originSources}},
                  "substitutions": []
                }]
              }
              """);
    }

    private static string CreateResponseBody(
        string text,
        bool includeWebSearchCall = false)
    {
        var message = new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            status = "completed",
            content = new[]
            {
                new
                {
                    type = "output_text",
                    text,
                    annotations = Array.Empty<object>()
                }
            }
        };
        object[] output = includeWebSearchCall
            ?
            [
                new
                {
                    id = "web_search_test",
                    type = "web_search_call",
                    status = "completed",
                    action = new { type = "search", query = "omelet history" }
                },
                message
            ]
            : [message];

        return JsonSerializer.Serialize(new
        {
            id = "resp_test",
            @object = "response",
            created_at = 1763313600,
            status = "completed",
            model = Deployment,
            output
        });
    }

    private static RecipeSearchResult CreateRecipe()
    {
        return new RecipeSearchResult
        {
            RecipeID = 42,
            Title = "Omelet",
            ReadyInMinutes = 10,
            Servings = 1,
            MatchingIngredients = ["eggs"],
            MissingIngredients = ["cheese"],
            Instructions = "Whisk eggs and cook."
        };
    }

    private sealed class ResponsesHandler(
        string analysisResponse,
        string researchResponse,
        HttpStatusCode researchStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            using var requestJson = JsonDocument.Parse(body);
            var isResearch = requestJson.RootElement.TryGetProperty("tools", out _);
            return new HttpResponseMessage(
                isResearch ? researchStatus : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    isResearch ? researchResponse : analysisResponse,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            return new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    private sealed class FakeRecipeDataSource(
        List<RecipeSearchResult> recipes) : IRecipeRecommendationDataSource
    {
        public Task<List<string>> GetIngredientNamesAsync(
            IReadOnlyCollection<int> ingredientIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ingredientIds
                .Select(id => id == 7 ? "eggs" : "cheese")
                .ToList());
        }

        public Task<ResolvedIngredientNames> ResolveIngredientNamesAsync(
            IReadOnlyCollection<string> ingredientNames,
            CancellationToken cancellationToken = default)
        {
            var names = ingredientNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult(new ResolvedIngredientNames
            {
                IngredientIds = names
                    .Select(name => name.Equals(
                        "eggs",
                        StringComparison.OrdinalIgnoreCase) ? 7 : 8)
                    .ToList(),
                ResolvedNames = names
            });
        }

        public Task<List<RecipeSearchResult>> GetMatchingRecipesAsync(
            IReadOnlyCollection<int> ingredientIds,
            IReadOnlyCollection<string> ingredientNames,
            CancellationToken cancellationToken = default,
            int? maximumResults = null)
        {
            return Task.FromResult(recipes);
        }
    }
}
#pragma warning restore OPENAI001
