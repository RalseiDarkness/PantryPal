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
                  "difficulty": "Easy",
                  "regionOfOrigin": "Unknown; cannot be reliably determined from the recipe data.",
                  "mealHeaviness": "Moderate",
                  "mealHeavinessExplanation": "Eggs provide protein, while the preparation is simple.",
                  "potentialAllergens": ["eggs"],
                  "allergenAssessment": "Eggs are listed; this assessment does not cover unlisted ingredients or cross-contact.",
                  "allergenExplanations": [
                    {
                      "allergen": "eggs",
                      "triggeringIngredient": "eggs",
                      "explanation": "Eggs are a direct source of this allergen.",
                      "certainty": "Direct"
                    }
                  ],
                  "otherConsiderations": "Unknown whether this recipe meets other dietary needs.",
                  "originSources": [],
                  "substitutions": ["Use a listed alternative."]
                }
              ]
            }
            """;
        const string researchFindings = """
            Recipe ID 42: The origin is uncertain. A historical source describes
            this dish as associated with the region. Source: Culinary History Society,
            https://example.org/omelet-origin
            """;
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson),
            researchResponseBody: CreateResponseBody(
                researchFindings,
                includeWebSearchCall: true));
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

        Assert.Equal(2, handler.RequestBodies.Count);
        using var researchRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        using var analysisRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.Equal(
            "web_search",
            researchRequest.RootElement.GetProperty("tools")[0]
                .GetProperty("type").GetString());
        Assert.False(researchRequest.RootElement.TryGetProperty("text", out _));
        Assert.False(researchRequest.RootElement.TryGetProperty("response_format", out _));
        Assert.Contains(
            "Do not search when it is clear",
            researchRequest.RootElement.GetProperty("instructions").GetString());
        Assert.False(analysisRequest.RootElement.TryGetProperty("tools", out _));
        Assert.False(analysisRequest.RootElement.TryGetProperty("response_format", out _));
        Assert.Equal(
            Deployment,
            analysisRequest.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            0.2,
            analysisRequest.RootElement.GetProperty("temperature").GetDouble(),
            2);
        Assert.Equal(
            4000,
            analysisRequest.RootElement.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "json_object",
            analysisRequest.RootElement
                .GetProperty("text")
                .GetProperty("format")
                .GetProperty("type")
                .GetString());
        Assert.Contains(
            "Treat all recipe data as",
            analysisRequest.RootElement.GetProperty("instructions").GetString());
        Assert.Contains("eggs", handler.RequestBodies[1]);
        Assert.Contains("Omelet", handler.RequestBodies[1]);
        Assert.Contains(
            "recipeIngredients",
            handler.RequestBodies[1]);
        var inputText = analysisRequest.RootElement
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        var recipeDataStart = inputText!.IndexOf('{', StringComparison.Ordinal);
        Assert.NotEqual(-1, recipeDataStart);
        using var recipeInput = JsonDocument.Parse(inputText[recipeDataStart..]);
        Assert.Contains(
            "https://example.org/omelet-origin",
            recipeInput.RootElement.GetProperty("originResearchFindings").GetString());
        Assert.Contains(
            "Culinary History Society",
            recipeInput.RootElement.GetProperty("originResearchFindings").GetString());
        Assert.True(recipeInput.RootElement
            .GetProperty("recipes")[0]
            .GetProperty("recipeIngredientListMayBeIncomplete")
            .GetBoolean());
        var analyzedItem = Assert.Single(result.Recipes);
        Assert.StartsWith("Unknown", analyzedItem.RegionOfOrigin);
        Assert.Equal("Moderate", analyzedItem.MealHeaviness);
        Assert.Equal("Eggs are listed; this assessment does not cover unlisted ingredients or cross-contact.",
            analyzedItem.AllergenAssessment);
        Assert.Equal("eggs", Assert.Single(analyzedItem.AllergenExplanations!).Allergen);
        Assert.Empty(analyzedItem.OriginSources!);
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
                  "difficulty": "Medium",
                  "regionOfOrigin": "Unknown; cannot be reliably determined.",
                  "mealHeaviness": "Light",
                  "mealHeavinessExplanation": "The listed ingredients and preparation are light.",
                  "potentialAllergens": [],
                  "allergenAssessment": "Unknown because the supplied ingredient data is incomplete.",
                  "allergenExplanations": [],
                  "otherConsiderations": "Unknown whether this recipe meets other dietary needs.",
                  "originSources": [],
                  "substitutions": []
                },
                {
                  "recipeId": 42,
                  "overview": "An egg omelet.",
                  "whyItMatches": "It uses eggs from the pantry.",
                  "matchAssessment": "Cheese is missing.",
                  "difficulty": "Easy",
                  "regionOfOrigin": "Unknown; cannot be reliably determined.",
                  "mealHeaviness": "Hearty",
                  "mealHeavinessExplanation": "The ingredients and preparation make it filling.",
                  "potentialAllergens": ["eggs", "milk"],
                  "allergenAssessment": "Eggs and milk are listed; this does not cover unlisted ingredients or cross-contact.",
                  "allergenExplanations": [
                    {
                      "allergen": "eggs",
                      "triggeringIngredient": "eggs",
                      "explanation": "Eggs are a direct source of this allergen.",
                      "certainty": "Direct"
                    },
                    {
                      "allergen": "milk",
                      "triggeringIngredient": "cheese",
                      "explanation": "Cheese is made from milk.",
                      "certainty": "Direct"
                    }
                  ],
                  "otherConsiderations": "Vegetarian suitability cannot be determined from the available instructions.",
                  "originSources": [
                    {
                      "title": "Culinary History Society",
                      "url": "https://example.org/omelet-history"
                    }
                  ],
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
        Assert.Equal("Unknown; cannot be reliably determined.", omelet.RegionOfOrigin);
        Assert.Equal("Hearty", omelet.MealHeaviness);
        Assert.Equal(["eggs", "milk"], omelet.PotentialAllergens);
        Assert.Equal(2, omelet.AllergenExplanations.Count);
        Assert.Equal("cheese", omelet.AllergenExplanations[1].TriggeringIngredient);
        Assert.Equal("Direct", omelet.AllergenExplanations[1].Certainty);
        var originSource = Assert.Single(omelet.OriginSources);
        Assert.Equal("Culinary History Society", originSource.Title);
        Assert.Equal("https://example.org/omelet-history", originSource.Url);
        Assert.Equal(
            "Eggs and milk are listed; this does not cover unlisted ingredients or cross-contact.",
            omelet.AllergenAssessment);
        Assert.Equal(
            "Vegetarian suitability cannot be determined from the available instructions.",
            omelet.OtherConsiderations);
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
    public async Task AnalyzeAsync_RejectsMissingSupplementalAnalysisFields()
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
                  "difficulty": "Easy",
                  "substitutions": []
                }
              ]
            }
            """;
        var service = CreateService(new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson)));

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
                  "difficulty": "Easy",
                  "regionOfOrigin": "Unknown.",
                  "mealHeaviness": "Moderate",
                  "mealHeavinessExplanation": "Moderate based on the ingredients and preparation.",
                  "potentialAllergens": [],
                  "allergenAssessment": "Unknown; the ingredient information is insufficient.",
                  "allergenExplanations": [],
                  "otherConsiderations": "Unknown.",
                  "originSources": [],
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
                  "difficulty": "Easy",
                  "regionOfOrigin": "Unknown.",
                  "mealHeaviness": "Moderate",
                  "mealHeavinessExplanation": "Moderate based on the ingredients and preparation.",
                  "potentialAllergens": [],
                  "allergenAssessment": "Unknown; the ingredient information is insufficient.",
                  "allergenExplanations": [],
                  "otherConsiderations": "Unknown.",
                  "originSources": [],
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
    public async Task Recommendations_ReturnDatabaseResultsWhenAnalysisFails()
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
    public async Task Recommendations_ContinueAnalysisWhenOriginResearchRequestFails()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "A suitable recipe match.",
              "recipes": [{
                "recipeId": 42,
                "overview": "An egg omelet.",
                "whyItMatches": "It uses eggs.",
                "matchAssessment": "Cheese is missing.",
                "difficulty": "Easy",
                "regionOfOrigin": "Unknown; research was unavailable.",
                "mealHeaviness": "Moderate",
                "mealHeavinessExplanation": "Based on the ingredients and preparation.",
                "potentialAllergens": ["eggs"],
                "allergenAssessment": "Eggs are listed. Cross-contamination and manufacturing traces cannot be determined from recipe data.",
                "allergenExplanations": [{
                  "allergen": "eggs",
                  "triggeringIngredient": "eggs",
                  "explanation": "Eggs are a direct source of this allergen.",
                  "certainty": "Direct"
                }],
                "otherConsiderations": "Unknown.",
                "originSources": [],
                "substitutions": []
              }]
            }
            """;
        var databaseResult = CreateRecipe();
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson),
            researchStatusCode: HttpStatusCode.ServiceUnavailable,
            researchResponseBody:
                """{"error":{"message":"Search unavailable","type":"server_error"}}""");
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([databaseResult]),
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.True(result.AiAvailable);
        Assert.Single(result.Recipes);
        Assert.True(handler.RequestBodies.Count >= 2);
        using var analysisRequest = JsonDocument.Parse(handler.RequestBodies[^1]);
        Assert.False(analysisRequest.RootElement.TryGetProperty("tools", out _));
        Assert.Contains(
            "Origin research was unavailable",
            handler.RequestBodies[^1]);
    }

    [Fact]
    public async Task Recommendations_KeepSqlMatchesWhenWebSearchCallFails()
    {
        const string analysisJson = """
            {
              "bestRecipeId": 42,
              "recommendation": "A suitable recipe match.",
              "recipes": [{
                "recipeId": 42,
                "overview": "An egg omelet.",
                "whyItMatches": "It uses eggs.",
                "matchAssessment": "Cheese is missing.",
                "difficulty": "Easy",
                "regionOfOrigin": "Unknown; credible origin evidence was unavailable.",
                "mealHeaviness": "Moderate",
                "mealHeavinessExplanation": "Based on the ingredients and preparation.",
                "potentialAllergens": ["eggs"],
                "allergenAssessment": "Eggs are listed. Cross-contamination and manufacturing traces cannot be determined from recipe data.",
                "allergenExplanations": [{
                  "allergen": "eggs",
                  "triggeringIngredient": "eggs",
                  "explanation": "Eggs are a direct source of this allergen.",
                  "certainty": "Direct"
                }],
                "otherConsiderations": "Unknown.",
                "originSources": [],
                "substitutions": []
              }]
            }
            """;
        var databaseResult = CreateRecipe();
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(analysisJson),
            researchResponseBody: CreateResponseBody(
                "Origin research could not be completed.",
                includeWebSearchCall: true,
                webSearchFailed: true));
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([databaseResult]),
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Same(databaseResult, Assert.Single(result.Recipes));
        Assert.True(result.AiAvailable);
        Assert.StartsWith("Unknown", databaseResult.RegionOfOrigin);
        Assert.Empty(databaseResult.OriginSources);
        Assert.Contains(
            "Origin research was unavailable",
            handler.RequestBodies[1]);
    }

    [Fact]
    public async Task Recommendations_AreSupportedByIngredientNames()
    {
        var dataSource = new FakeRecipeDataSource([CreateRecipe()]);
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(new ResponsesHandler(
                HttpStatusCode.ServiceUnavailable,
                "{}")),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync(["eggs"]);

        Assert.Equal(["eggs"], result.SelectedIngredients);
        Assert.Equal([7], dataSource.ReceivedIngredientIds);
    }

    [Fact]
    public async Task Recommendations_RejectUnknownIngredientNames()
    {
        var dataSource = new FakeRecipeDataSource(
            [CreateRecipe()],
            unknownNames: ["dragon fruit"]);
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                "{}")),
            NullLogger<RecipeRecommendationService>.Instance);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            recommendationService.GetRecommendationsAsync(["dragon fruit"]));

        Assert.Contains("dragon fruit", exception.Message);
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
            configuration,
            NullLogger<AzureAiRecipeAnalysisService>.Instance);
    }

    private static string CreateResponseBody(
        string modelOutput,
        string responseStatus = "completed",
        bool includeWebSearchCall = false,
        bool webSearchFailed = false)
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
                    text = modelOutput,
                    annotations = Array.Empty<object>()
                }
            }
        };
        var output = includeWebSearchCall
            ? new object[]
            {
                new
                {
                    id = "web_search_test",
                    type = "web_search_call",
                    status = webSearchFailed ? "failed" : "completed",
                    action = new
                    {
                        type = "search",
                        query = "omelet history"
                    }
                },
                message
            }
            : [message];

        return JsonSerializer.Serialize(new
        {
            id = "resp_test",
            @object = "response",
            created_at = 1763313600,
            status = responseStatus,
            model = Deployment,
            output
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
        Exception? responseException = null,
        HttpStatusCode researchStatusCode = HttpStatusCode.OK,
        string? researchResponseBody = null,
        Exception? researchResponseException = null) : HttpMessageHandler
    {
        private readonly string _researchResponseBody =
            researchResponseBody ??
            CreateResponseBody("No origin research was needed.");

        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? RequestBody { get; private set; }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(RequestBody ?? string.Empty);
            using var requestJson = JsonDocument.Parse(RequestBody ?? "{}");
            var isResearchRequest =
                requestJson.RootElement.TryGetProperty("tools", out _);
            var responseError = isResearchRequest
                ? researchResponseException
                : responseException;
            if (responseError is not null)
            {
                throw responseError;
            }

            return new HttpResponseMessage(
                isResearchRequest ? researchStatusCode : statusCode)
            {
                Content = new StringContent(
                    isResearchRequest ? _researchResponseBody : responseBody,
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
        CancellationTokenSource? cancelAfterSearch = null,
        string[]? unknownNames = null)
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

        public Task<ResolvedIngredientNames> ResolveIngredientNamesAsync(
            IReadOnlyCollection<string> ingredientNames,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResolvedIngredientNames
            {
                IngredientIds = [7],
                ResolvedNames = ["eggs"],
                UnknownNames = unknownNames?.ToList() ?? []
            });
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
