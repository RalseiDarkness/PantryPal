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
            recipe.Difficulty = item.Difficulty;
            recipe.RegionOfOrigin = item.RegionOfOrigin;
            recipe.MealHeaviness = item.MealHeaviness;
            recipe.MealHeavinessExplanation = item.MealHeavinessExplanation;
            recipe.PotentialAllergens = item.PotentialAllergens ??
                throw new JsonException("Azure AI Foundry returned no allergen analysis.");
            recipe.AllergenAssessment = item.AllergenAssessment;
            recipe.AllergenExplanations = item.AllergenExplanations ??
                throw new JsonException("Azure AI Foundry returned no allergen explanations.");
            recipe.OriginSources = item.OriginSources ??
                throw new JsonException("Azure AI Foundry returned no origin sources.");
            recipe.OtherConsiderations = item.OtherConsiderations;
            recipe.Substitutions = item.Substitutions;
        }

        result.BestRecipeId = analysis.BestRecipeId;
        result.Recommendation = analysis.Recommendation;
        result.AiAvailable = true;
        return result;
    }

    public async Task<RecipeRecommendationResponse> GetRecommendationsAsync(
        IReadOnlyCollection<string> ingredientNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ingredientNames);

        var resolution = await _recipeService.ResolveIngredientNamesAsync(
            ingredientNames,
            cancellationToken);

        if (resolution.UnknownNames.Count > 0)
        {
            throw new ArgumentException(
                "Unknown ingredient names: " +
                string.Join(", ", resolution.UnknownNames),
                nameof(ingredientNames));
        }

        if (resolution.IngredientIds.Count == 0)
        {
            throw new ArgumentException(
                "One or more ingredient names could not be resolved.",
                nameof(ingredientNames));
        }

        var result = await GetRecommendationsAsync(
            resolution.IngredientIds,
            cancellationToken);
        result.SelectedIngredients = resolution.ResolvedNames;
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
                item.Difficulty is not ("Easy" or "Medium" or "Hard") ||
                string.IsNullOrWhiteSpace(item.RegionOfOrigin) ||
                item.MealHeaviness is not ("Light" or "Moderate" or "Hearty") ||
                string.IsNullOrWhiteSpace(item.MealHeavinessExplanation) ||
                item.PotentialAllergens is null ||
                item.PotentialAllergens.Any(allergen =>
                    string.IsNullOrWhiteSpace(allergen) ||
                    !IsCommonAllergen(allergen)) ||
                string.IsNullOrWhiteSpace(item.AllergenAssessment) ||
                item.AllergenExplanations is null ||
                item.AllergenExplanations.Any(explanation =>
                    explanation is null ||
                    !IsCommonAllergen(explanation.Allergen) ||
                    string.IsNullOrWhiteSpace(explanation.TriggeringIngredient) ||
                    string.IsNullOrWhiteSpace(explanation.Explanation) ||
                    explanation.Certainty is not ("Direct" or "Possible")) ||
                !item.PotentialAllergens.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    .SetEquals(item.AllergenExplanations.Select(
                        explanation => explanation.Allergen)) ||
                string.IsNullOrWhiteSpace(item.OtherConsiderations) ||
                item.OriginSources is null ||
                item.OriginSources.Any(source =>
                    source is null ||
                    string.IsNullOrWhiteSpace(source.Title) ||
                    !Uri.TryCreate(source.Url, UriKind.Absolute, out var sourceUri) ||
                    (sourceUri.Scheme != Uri.UriSchemeHttps &&
                     sourceUri.Scheme != Uri.UriSchemeHttp)) ||
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

    private static bool IsCommonAllergen(string allergen)
    {
        return allergen.ToLowerInvariant() is
            "milk" or "eggs" or "fish" or "shellfish" or "tree nuts" or
            "peanuts" or "wheat" or "soy" or "sesame";
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
