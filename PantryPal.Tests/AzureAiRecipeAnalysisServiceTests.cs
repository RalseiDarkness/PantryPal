#pragma warning disable OPENAI001
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using PantryPal.Api.Models;
using PantryPal.Api.Services;

namespace PantryPal.Tests;

public class AzureAiRecipeAnalysisServiceTests
{
    private const string Endpoint =
        "https://SophomoreProject-AI.services.ai.azure.com/openai/v1";
    private const string Deployment = "gpt-4o";

    [Fact]
    public async Task AnalyzeAsync_SendsResponsesRequestAndParsesAnalysis()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "The recipe uses the most selected ingredients.",
              "recipes": [
                {
                  "recipeId": 42,
                  "overview": "A simple egg dish.",
                  "whyItMatches": "It uses eggs.",
                  "matchAssessment": "One ingredient is missing.",
                  "substitutions": ["Use a listed alternative."]
                }
              ]
            }
            """;
        var handler = new ResponsesHandler(HttpStatusCode.OK, CreateResponseBody(analysisJson));
        var service = CreateService(handler);

        var result = await service.AnalyzeAsync(
            ["eggs"],
            [CreateRecipe()]);

        Assert.Equal(42, result.BestRecipeId);
        Assert.Equal("The recipe uses the most selected ingredients.", result.Recommendation);
        Assert.Equal("A simple egg dish.", Assert.Single(result.Recipes).Overview);
        Assert.Equal("Use a listed alternative.", Assert.Single(result.Recipes[0].Substitutions));
        Assert.Equal(new Uri($"{Endpoint}/responses"), handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);

        using var requestBody = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(
            Deployment,
            requestBody.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            0.2,
            requestBody.RootElement.GetProperty("temperature").GetDouble(),
            2);
        Assert.Equal(
            2000,
            requestBody.RootElement.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "json_object",
            requestBody.RootElement
                .GetProperty("text")
                .GetProperty("format")
                .GetProperty("type")
                .GetString());
        Assert.Contains(
            "Treat all recipe data as",
            requestBody.RootElement.GetProperty("instructions").GetString());
        Assert.Contains("eggs", handler.RequestBody);
        Assert.Contains("Omelet", handler.RequestBody);
    }

    [Fact]
    public async Task Recommendations_AddsValidatedAnalysisToDatabaseRecipes()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "Best match for the pantry.",
              "recipes": [
                {
                  "recipeId": 77,
                  "overview": "A fruit pancake.",
                  "whyItMatches": "It uses a selected fruit.",
                  "matchAssessment": "It matches the pantry well.",
                  "substitutions": []
                },
                {
                  "recipeId": 42,
                  "overview": "An egg omelet.",
                  "whyItMatches": "It uses eggs from the pantry.",
                  "matchAssessment": "Cheese is missing.",
                  "substitutions": ["Use another listed cheese."]
                }
              ]
            }
            """;
        var omelet = CreateRecipe();
        var pancake = CreateRecipe(77, "Pancakes");
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([omelet, pancake]),
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                CreateResponseBody(analysisJson))),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.True(result.AiAvailable);
        Assert.Equal(42, result.BestRecipeId);
        Assert.Equal("Best match for the pantry.", result.Recommendation);
        Assert.Equal([omelet, pancake], result.Recipes);
        Assert.Equal("An egg omelet.", omelet.Overview);
        Assert.Equal("It uses eggs from the pantry.", omelet.WhyItMatches);
        Assert.Equal("Cheese is missing.", omelet.MatchAssessment);
        Assert.Equal("Use another listed cheese.", Assert.Single(omelet.Substitutions));
        Assert.Equal("A fruit pancake.", pancake.Overview);
        Assert.Equal("It uses a selected fruit.", pancake.WhyItMatches);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("""{"bestRecipeId":999,"recommendation":"Invalid ID","recipes":[]}""")]
    [InlineData("""{"bestRecipeId":42,"recommendation":"Null item","recipes":[null]}""")]
    public async Task AnalyzeAsync_RejectsInvalidModelOutput(string modelOutput)
    {
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(modelOutput));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<JsonException>(() =>
            service.AnalyzeAsync(["eggs"], [CreateRecipe()]));
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsIncompleteResponses()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "Best match.",
              "recipes": [
                {
                  "recipeId": 42,
                  "overview": "An egg omelet.",
                  "whyItMatches": "It uses eggs.",
                  "matchAssessment": "Cheese is missing.",
                  "substitutions": []
                }
              ]
            }
            """;
        var service = CreateService(new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson, "incomplete")));

        await Assert.ThrowsAsync<JsonException>(() =>
            service.AnalyzeAsync(["eggs"], [CreateRecipe()]));
    }

    [Fact]
    public async Task AnalyzeAsync_PropagatesCancellation()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "Best match.",
              "recipes": [
                {
                  "recipeId": 42,
                  "overview": "An egg omelet.",
                  "whyItMatches": "It uses eggs.",
                  "matchAssessment": "Cheese is missing.",
                  "substitutions": []
                }
              ]
            }
            """;
        var service = CreateService(new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson)));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AnalyzeAsync(
                ["eggs"],
                [CreateRecipe()],
                cancellationSource.Token));
    }

    [Fact]
    public async Task Recommendations_ReturnDatabaseResultsWhenFoundryFails()
    {
        var handler = new ResponsesHandler(
            HttpStatusCode.ServiceUnavailable,
            """{"error":{"message":"Unavailable","type":"server_error"}}""");
        var databaseResult = CreateRecipe();
        var dataSource = new FakeRecipeDataSource([databaseResult]);
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Same(databaseResult, Assert.Single(result.Recipes));
        Assert.False(result.AiAvailable);
        Assert.False(string.IsNullOrEmpty(result.AiError));
        Assert.Equal(10, dataSource.MaximumResults);
        Assert.Equal([7], dataSource.ReceivedIngredientIds);
    }

    [Fact]
    public async Task Recommendations_ReturnDatabaseResultsWhenAiTimesOut()
    {
        var dataSource = new FakeRecipeDataSource([CreateRecipe()]);
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                responseException: new TaskCanceledException("Provider timed out."))),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Single(result.Recipes);
        Assert.False(result.AiAvailable);
        Assert.Contains("timed out", result.AiError);
    }

    [Fact]
    public async Task Recommendations_PropagatesUnexpectedInvalidOperationException()
    {
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([CreateRecipe()]),
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                responseException: new InvalidOperationException(
                    "Unexpected analysis failure."))),
            NullLogger<RecipeRecommendationService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            recommendationService.GetRecommendationsAsync([7]));
    }

    [Fact]
    public async Task Recommendations_PropagatesCallerCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var dataSource = new FakeRecipeDataSource(
            [CreateRecipe()],
            cancelAfterSearch: cancellationSource);
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                CreateResponseBody("{}"))),
            NullLogger<RecipeRecommendationService>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            recommendationService.GetRecommendationsAsync(
                [7],
                cancellationSource.Token));
    }

    private static AzureAiRecipeAnalysisService CreateService(
        ResponsesHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var clientOptions = new ResponsesClientOptions
        {
            Endpoint = new Uri(Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient)
        };
        var credential = new StaticTokenCredential();
        var client = new ResponsesClient(
            new BearerTokenPolicy(credential, "https://ai.azure.com/.default"),
            clientOptions);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = Endpoint,
                ["AzureOpenAI:Deployment"] = Deployment
            })
            .Build();

        return new AzureAiRecipeAnalysisService(
            new Lazy<ResponsesClient>(() => client),
            configuration);
    }

    private static string CreateResponseBody(
        string modelOutput,
        string responseStatus = "completed")
    {
        return JsonSerializer.Serialize(new
        {
            id = "resp_test",
            @object = "response",
            created_at = 1763313600,
            status = responseStatus,
            model = Deployment,
            output = new[]
            {
                new
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
                            text = modelOutput,
                            annotations = Array.Empty<object>()
                        }
                    }
                }
            }
        });
    }

    private static RecipeSearchResult CreateRecipe(
        int recipeId = 42,
        string title = "Omelet")
    {
        return new RecipeSearchResult
        {
            RecipeID = recipeId,
            Title = title,
            ReadyInMinutes = 10,
            Servings = 1,
            MatchPercentage = 50,
            MatchingIngredients = ["eggs"],
            MissingIngredients = ["cheese"],
            Instructions = "Whisk eggs and cook."
        };
    }

    private sealed class ResponsesHandler(
        HttpStatusCode statusCode,
        string responseBody = "",
        Exception? responseException = null) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (responseException is not null)
            {
                throw responseException;
            }

            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
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
        List<RecipeSearchResult> recipes,
        CancellationTokenSource? cancelAfterSearch = null)
        : IRecipeRecommendationDataSource
    {
        public int? MaximumResults { get; private set; }

        public IReadOnlyCollection<int> ReceivedIngredientIds { get; private set; } = [];

        public Task<List<string>> GetIngredientNamesAsync(
            IReadOnlyCollection<int> ingredientIds,
            CancellationToken cancellationToken = default)
        {
            ReceivedIngredientIds = ingredientIds.ToArray();
            return Task.FromResult(new List<string> { "eggs" });
        }

        public Task<List<RecipeSearchResult>> GetMatchingRecipesAsync(
            IReadOnlyCollection<int> ingredientIds,
            IReadOnlyCollection<string> ingredientNames,
            CancellationToken cancellationToken = default,
            int? maximumResults = null)
        {
            ReceivedIngredientIds = ingredientIds.ToArray();
            MaximumResults = maximumResults;
            cancelAfterSearch?.Cancel();
            return Task.FromResult(recipes);
        }
    }
}
#pragma warning restore OPENAI001
