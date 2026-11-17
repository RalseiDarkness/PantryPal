using System.Text.Json;
using System.ClientModel;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using PantryPal.Api.Models;

namespace PantryPal.Api.Services;

public sealed class RecipeRecommendationService
{
    private const int MaximumAnalyzedRecipes = 10;

    private readonly IRecipeRecommendationDataSource _recipeService;
    private readonly AzureAiRecipeAnalysisService _aiService;
    private readonly ILogger<RecipeRecommendationService> _logger;

    public RecipeRecommendationService(
        IRecipeRecommendationDataSource recipeService,
        AzureAiRecipeAnalysisService aiService,
        ILogger<RecipeRecommendationService> logger)
    {
        _recipeService = recipeService;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<RecipeRecommendationResponse> GetRecommendationsAsync(
        IReadOnlyCollection<int> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = ingredientIds.Distinct().ToArray();
        var resolvedIngredientNames = await _recipeService.GetIngredientNamesAsync(
            distinctIds,
            cancellationToken);

        if (resolvedIngredientNames.Count != distinctIds.Length)
        {
            throw new ArgumentException(
                "One or more selected ingredients do not exist.",
                nameof(ingredientIds));
        }

        var selectedIngredients = resolvedIngredientNames
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var recipes = await _recipeService.GetMatchingRecipesAsync(
            distinctIds,
            Array.Empty<string>(),
            cancellationToken,
            MaximumAnalyzedRecipes);

        var result = new RecipeRecommendationResponse
        {
            SelectedIngredients = selectedIngredients,
            Recipes = recipes
        };

        if (recipes.Count == 0)
        {
            return result;
        }

        AiRecipeAnalysis analysis;
        Dictionary<int, AiRecipeAnalysisItem> analysesByRecipeId;
        try
        {
            analysis = await _aiService.AnalyzeAsync(
                selectedIngredients,
                recipes,
                cancellationToken);
            analysesByRecipeId = ValidateAndIndexAnalysis(analysis, recipes);
        }
        catch (Exception ex) when (
            !cancellationToken.IsCancellationRequested &&
            IsRecoverableAiFailure(ex))
        {
            if (IsAiTimeout(ex))
            {
                _logger.LogWarning(ex, "Azure AI recipe analysis timed out.");
                result.AiError =
                    "AI recommendations timed out. Your database recipe matches are still shown.";
            }
            else
            {
                _logger.LogError(ex, "Azure AI recipe analysis failed.");
                result.AiError =
                    "AI recommendations are unavailable. Your database recipe matches are still shown.";
            }

            return result;
        }

        foreach (var recipe in recipes)
        {
            var item = analysesByRecipeId[recipe.RecipeID];
            recipe.Overview = item.Overview;
            recipe.WhyItMatches = item.WhyItMatches;
            recipe.MatchAssessment = item.MatchAssessment;
            recipe.Substitutions = item.Substitutions;
        }

        result.BestRecipeId = analysis.BestRecipeId;
        result.Recommendation = analysis.Recommendation;
        result.AiAvailable = true;
        return result;
    }

    private static Dictionary<int, AiRecipeAnalysisItem> ValidateAndIndexAnalysis(
        AiRecipeAnalysis? analysis,
        IReadOnlyCollection<RecipeSearchResult> recipes)
    {
        var recipeIds = recipes.Select(recipe => recipe.RecipeID).ToHashSet();
        if (analysis is null ||
            string.IsNullOrWhiteSpace(analysis.Recommendation) ||
            analysis.Recipes is null ||
            analysis.Recipes.Count != recipeIds.Count ||
            !recipeIds.Contains(analysis.BestRecipeId))
        {
            throw new JsonException(
                "Azure AI Foundry returned an incomplete recipe analysis.");
        }

        var analysesByRecipeId = new Dictionary<int, AiRecipeAnalysisItem>();
        foreach (var item in analysis.Recipes)
        {
            if (item is null ||
                !recipeIds.Contains(item.RecipeId) ||
                !analysesByRecipeId.TryAdd(item.RecipeId, item) ||
                string.IsNullOrWhiteSpace(item.Overview) ||
                string.IsNullOrWhiteSpace(item.WhyItMatches) ||
                string.IsNullOrWhiteSpace(item.MatchAssessment) ||
                item.Substitutions is null)
            {
                throw new JsonException(
                    "Azure AI Foundry returned an invalid recipe analysis.");
            }
        }

        if (!recipeIds.SetEquals(analysesByRecipeId.Keys))
        {
            throw new JsonException(
                "Azure AI Foundry did not analyze exactly the supplied recipes.");
        }

        return analysesByRecipeId;
    }

    private static bool IsRecoverableAiFailure(Exception exception)
    {
        return exception switch
        {
            AggregateException aggregate =>
                aggregate.InnerExceptions.Count > 0 &&
                aggregate.InnerExceptions.All(IsRecoverableAiFailure),
            OperationCanceledException or
                TimeoutException or
                HttpRequestException or
                ClientResultException or
                AuthenticationFailedException or
                JsonException => true,
            _ => false
        };
    }

    private static bool IsAiTimeout(Exception exception)
    {
        return exception switch
        {
            AggregateException aggregate =>
                aggregate.InnerExceptions.Any(IsAiTimeout),
            OperationCanceledException or TimeoutException => true,
            _ => false
        };
    }
}
