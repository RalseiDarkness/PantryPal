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

/// Exercises Foundry request construction and recommendation fallbacks through a
/// mocked HTTP transport; these tests do not contact Azure.

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
    public async Task Recommendations_BatchesComplexityWithoutFullAiAnalysis()
    {
        const string complexityJson = """
            {
              "recipes": [
                { "recipeId": 77, "difficulty": "Medium" },
                { "recipeId": 42, "difficulty": "Easy" }
              ]
            }
            """;
        var omelet = CreateRecipe();
        var pancake = CreateRecipe(77, "Pancakes");
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            CreateResponseBody(complexityJson));
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([omelet, pancake]),
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.True(result.ComplexityAvailable);
        Assert.False(result.AiAvailable);
        Assert.Null(result.BestRecipeId);
        Assert.Null(result.Recommendation);
        Assert.Equal([omelet, pancake], result.Recipes);
        Assert.Equal("Easy", omelet.Difficulty);
        Assert.Equal("Medium", pancake.Difficulty);
        Assert.Null(omelet.Overview);
        Assert.Null(omelet.WhyItMatches);
        Assert.Single(handler.RequestBodies);
        using var request = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.False(request.RootElement.TryGetProperty("tools", out _));
        Assert.Equal(
            "json_object",
            request.RootElement.GetProperty("text").GetProperty("format")
                .GetProperty("type").GetString());
        Assert.Contains(
            "valid JSON matching the required response structure",
            request.RootElement.GetProperty("input")[0]
                .GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Contains(
            "JSON",
            request.RootElement.GetProperty("instructions").GetString());
        Assert.DoesNotContain("originResearchFindings", handler.RequestBodies[0]);
        Assert.Contains("Whisk eggs and cook.", handler.RequestBodies[0]);
    }

    // Checks the 100-result workflow end to end: each ten-item request must map by
    // recipe ID, and the semaphore must keep active requests to two.
    [Fact]
    public async Task Recommendations_ReturnsOneHundredRecipesWithIdMappedComplexity()
    {
        var recipes = Enumerable.Range(1, 100)
            .Select(id => CreateRecipe(id, $"Recipe {id}"))
            .ToList();
        var activeRequests = 0;
        var maximumConcurrentRequests = 0;
        var dataSource = new FakeRecipeDataSource(recipes);
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            complexityResponseFactory: async (request, cancellationToken) =>
            {
                var active = Interlocked.Increment(ref activeRequests);
                int previousMaximum;
                do
                {
                    previousMaximum = maximumConcurrentRequests;
                    if (active <= previousMaximum)
                    {
                        break;
                    }
                }
                while (Interlocked.CompareExchange(
                    ref maximumConcurrentRequests,
                    active,
                    previousMaximum) != previousMaximum);

                try
                {
                    await Task.Delay(20, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            CreateResponseBody(CreateComplexityJson(
                                request,
                                id => (id % 3) switch
                                {
                                    0 => "Hard",
                                    1 => "Easy",
                                    _ => "Medium"
                                })),
                            Encoding.UTF8,
                            "application/json")
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref activeRequests);
                }
            });
        var recommendationService = new RecipeRecommendationService(
            dataSource,
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Equal(100, result.Recipes.Count);
        Assert.Equal(100, dataSource.MaximumResults);
        Assert.Equal("Easy", result.Recipes.Single(recipe =>
            recipe.RecipeID == 1).Difficulty);
        Assert.Equal("Medium", result.Recipes.Single(recipe =>
            recipe.RecipeID == 2).Difficulty);
        Assert.Equal("Hard", result.Recipes.Single(recipe =>
            recipe.RecipeID == 3).Difficulty);
        Assert.Equal(10, handler.RequestBodies.Count);
        Assert.Equal(2, maximumConcurrentRequests);
        Assert.All(handler.RequestBodies, requestBody =>
        {
            using var request = JsonDocument.Parse(requestBody);
            Assert.False(request.RootElement.TryGetProperty("tools", out _));
            Assert.Equal(
                "json_object",
                request.RootElement.GetProperty("text").GetProperty("format")
                    .GetProperty("type").GetString());
            Assert.InRange(
                GetRequestedRecipeIds(request.RootElement).Length,
                1,
                10);
        });
    }

    [Fact]
    public async Task Recommendations_MarksMissingOrInvalidComplexityUnavailable()
    {
        const string partialJson = """
            {
              "recipes": [
                { "recipeId": 42, "difficulty": "Easy" },
                { "recipeId": 77, "difficulty": "Expert" },
                { "recipeId": 999, "difficulty": "Hard" }
              ]
            }
            """;
        var omelet = CreateRecipe();
        var pancake = CreateRecipe(77, "Pancakes");
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([omelet, pancake]),
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                CreateResponseBody(partialJson))),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.False(result.ComplexityAvailable);
        Assert.Contains("1 of 2 recipes", result.ComplexityError);
        Assert.Equal("Easy", omelet.Difficulty);
        Assert.Equal("Unavailable", pancake.Difficulty);
    }

    // A provider failure for one batch must not erase classifications from the
    // other batches or suppress the SQL matches.
    [Fact]
    public async Task Recommendations_PreservesSuccessfulBatchesWhenOneBatchFails()
    {
        var recipes = Enumerable.Range(1, 25)
            .Select(id => CreateRecipe(id, $"Recipe {id}"))
            .ToList();
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            complexityResponseFactory: (request, _) =>
            {
                var recipeIds = GetRequestedRecipeIds(request);
                if (recipeIds[0] == 11)
                {
                    return Task.FromResult(new HttpResponseMessage(
                        HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("{}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        CreateResponseBody(CreateComplexityJson(
                            request,
                            _ => "Medium")),
                        Encoding.UTF8,
                        "application/json")
                });
            });
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource(recipes),
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.False(result.ComplexityAvailable);
        Assert.Contains("10 of 25 recipes", result.ComplexityError);
        Assert.Equal(3, handler.RequestBodies
            .Select(body =>
            {
                using var request = JsonDocument.Parse(body);
                return string.Join(",", GetRequestedRecipeIds(request.RootElement));
            })
            .Distinct()
            .Count());
        Assert.All(recipes.Take(10), recipe => Assert.Equal("Medium", recipe.Difficulty));
        Assert.All(recipes.Skip(10).Take(10), recipe => Assert.Equal("Unavailable", recipe.Difficulty));
        Assert.All(recipes.Skip(20), recipe => Assert.Equal("Medium", recipe.Difficulty));
    }

    [Fact]
    public async Task Recommendations_ReturnsSqlResultsWhenEveryComplexityBatchFails()
    {
        var recipes = Enumerable.Range(1, 12)
            .Select(id => CreateRecipe(id, $"Recipe {id}"))
            .ToList();
        var handler = new ResponsesHandler(
            HttpStatusCode.OK,
            complexityResponseFactory: (_, _) =>
                Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("Provider unavailable.")));
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource(recipes),
            CreateService(handler),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Equal(12, result.Recipes.Count);
        Assert.False(result.ComplexityAvailable);
        Assert.Contains("12 of 12 recipes", result.ComplexityError);
        Assert.All(result.Recipes, recipe => Assert.Equal("Unavailable", recipe.Difficulty));
        Assert.Equal(2, handler.RequestBodies
            .Select(body =>
            {
                using var request = JsonDocument.Parse(body);
                return string.Join(",", GetRequestedRecipeIds(request.RootElement));
            })
            .Distinct()
            .Count());
    }

    [Fact]
    public async Task Recommendations_RejectsDuplicateUnexpectedAndInvalidIdsWithinBatch()
    {
        const string modelOutput = """
            {
              "recipes": [
                { "recipeId": 42, "difficulty": "Easy" },
                { "recipeId": 42, "difficulty": "Hard" },
                { "recipeId": 77, "difficulty": "Expert" },
                { "recipeId": 999, "difficulty": "Medium" }
              ]
            }
            """;
        var omelet = CreateRecipe();
        var pancake = CreateRecipe(77, "Pancakes");
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([omelet, pancake]),
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                CreateResponseBody(modelOutput))),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.False(result.ComplexityAvailable);
        Assert.Equal("Unavailable", omelet.Difficulty);
        Assert.Equal("Unavailable", pancake.Difficulty);
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
    public async Task Recommendations_FallsBackWhenComplexityRequestFails()
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
        Assert.False(result.ComplexityAvailable);
        Assert.False(string.IsNullOrEmpty(result.ComplexityError));
        Assert.Equal("Unavailable", databaseResult.Difficulty);
        Assert.Equal(100, dataSource.MaximumResults);
        Assert.Equal([7], dataSource.ReceivedIngredientIds);
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
    public async Task Recommendations_ReturnDatabaseResultsWhenComplexityTimesOut()
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
        Assert.False(result.ComplexityAvailable);
        Assert.Contains("unavailable", result.ComplexityError);
        Assert.Equal("Unavailable", Assert.Single(result.Recipes).Difficulty);
    }

    [Fact]
    public async Task Recommendations_ReturnsSqlResultsWhenComplexityAnalysisThrowsUnexpectedException()
    {
        var recipe = CreateRecipe();
        var recommendationService = new RecipeRecommendationService(
            new FakeRecipeDataSource([recipe]),
            CreateService(new ResponsesHandler(
                HttpStatusCode.OK,
                responseException: new InvalidOperationException(
                    "Unexpected analysis failure."))),
            NullLogger<RecipeRecommendationService>.Instance);

        var result = await recommendationService.GetRecommendationsAsync([7]);

        Assert.Same(recipe, Assert.Single(result.Recipes));
        Assert.False(result.ComplexityAvailable);
        Assert.Equal("Unavailable", recipe.Difficulty);
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

    // Inject a token and transport so the OpenAI SDK request can be inspected
    // without real credentials or an Azure deployment.
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

    private static string CreateComplexityJson(
        JsonElement request,
        Func<int, string> getDifficulty)
    {
        return JsonSerializer.Serialize(new
        {
            recipes = GetRequestedRecipeIds(request)
                .Select(recipeId => new
                {
                    recipeId,
                    difficulty = getDifficulty(recipeId)
                })
        });
    }

    private static int[] GetRequestedRecipeIds(JsonElement request)
    {
        var inputText = request.GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;
        var jsonStart = inputText.IndexOf('{', StringComparison.Ordinal);
        using var recipeInput = JsonDocument.Parse(inputText[jsonStart..]);
        return recipeInput.RootElement.GetProperty("recipes")
            .EnumerateArray()
            .Select(recipe => recipe.GetProperty("recipeId").GetInt32())
            .ToArray();
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

    /// Routes mocked Responses API calls to test-configured replies.
    private sealed class ResponsesHandler(
        HttpStatusCode statusCode,
        string responseBody = "",
        Exception? responseException = null,
        HttpStatusCode researchStatusCode = HttpStatusCode.OK,
        string? researchResponseBody = null,
        Exception? researchResponseException = null,
        Func<JsonElement, CancellationToken, Task<HttpResponseMessage>>?
            complexityResponseFactory = null) : HttpMessageHandler
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
            lock (RequestBodies)
            {
                RequestBodies.Add(RequestBody ?? string.Empty);
            }
            using var requestJson = JsonDocument.Parse(RequestBody ?? "{}");
            var isResearchRequest =
                requestJson.RootElement.TryGetProperty("tools", out _);
            if (!isResearchRequest && complexityResponseFactory is not null)
            {
                return await complexityResponseFactory(
                    requestJson.RootElement,
                    cancellationToken);
            }

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
