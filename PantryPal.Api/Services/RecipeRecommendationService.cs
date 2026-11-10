using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using PantryPal.Api.Models;

namespace PantryPal.Api.Services;

public sealed class RecipeRecommendationService
{
    private const int MaximumAnalyzedRecipes = 10;

    private readonly RecipeService _recipeService;
    private readonly AzureAiRecipeAnalysisService _aiService;
    private readonly ILogger<RecipeRecommendationService> _logger;

    public RecipeRecommendationService(
        RecipeService recipeService,
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
        var selectedIngredients = await _recipeService.GetIngredientNamesAsync(
            distinctIds,
            cancellationToken);

        if (selectedIngredients.Count != distinctIds.Length)
        {
            throw new ArgumentException(
                "One or more selected ingredients do not exist.",
                nameof(ingredientIds));
        }

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
        try
        {
            analysis = await _aiService.AnalyzeAsync(
                selectedIngredients,
                recipes,
                cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Azure AI recipe analysis timed out.");
            result.AiError =
                "AI recommendations timed out. Your database recipe matches are still shown.";
            return result;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
                AuthenticationFailedException or
                JsonException or
                InvalidOperationException)
        {
            _logger.LogError(ex, "Azure AI recipe analysis failed.");
            result.AiError =
                "AI recommendations are unavailable. Your database recipe matches are still shown.";
            return result;
        }

        var analysesByRecipeId = analysis.Recipes.ToDictionary(item => item.RecipeId);
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
}
