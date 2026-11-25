using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PantryPal.Api.Models;

namespace PantryPal.Api.Services;

/// Loads SQL recipe details and adds the full AI analysis requested by the detail
/// page. Successful results are cached for each recipe and pantry context.
public sealed class RecipeDetailAnalysisService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IRecipeRecommendationDataSource _recipeService;
    private readonly AzureAiRecipeAnalysisService _aiService;
    private readonly IMemoryCache _cache;

    /// Creates the detail-analysis service with SQL, AI, and cache dependencies.
    public RecipeDetailAnalysisService(
        IRecipeRecommendationDataSource recipeService,
        AzureAiRecipeAnalysisService aiService,
        IMemoryCache cache)
    {
        _recipeService = recipeService;
        _aiService = aiService;
        _cache = cache;
    }


    /// Resolves pantry names, verifies that the recipe matches those ingredients,
    /// and returns the recipe with cached or newly generated AI analysis.

    /// The analyzed recipe, or <see langword="null"/> when it is not among the
    /// matching SQL results.
    public async Task<RecipeSearchResult?> AnalyzeRecipeAsync(
        int recipeId,
        IReadOnlyCollection<string> ingredientNames,
        CancellationToken cancellationToken = default)
    {
        if (recipeId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recipeId),
                "Recipe ID must be positive.");
        }

        ArgumentNullException.ThrowIfNull(ingredientNames);
        if (ingredientNames.Count == 0 ||
            ingredientNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty pantry ingredient is required.",
                nameof(ingredientNames));
        }

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

        var selectedIngredients = resolution.ResolvedNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedIngredients.Length == 0 || resolution.IngredientIds.Count == 0)
        {
            throw new ArgumentException(
                "One or more pantry ingredients could not be resolved.",
                nameof(ingredientNames));
        }

        var recipes = await _recipeService.GetMatchingRecipesAsync(
            resolution.IngredientIds,
            Array.Empty<string>(),
            cancellationToken,
            RecipeRecommendationService.MaximumRecipeResults);
        var recipe = recipes.FirstOrDefault(candidate =>
            candidate.RecipeID == recipeId);
        if (recipe is null)
        {
            return null;
        }

        // The pantry affects match explanations and recommendations, so it is part
        // of the cache key; different pantry contexts must not share AI summaries.
        var cacheKey = CreateCacheKey(recipe, selectedIngredients);
        if (_cache.TryGetValue(cacheKey, out AiRecipeAnalysisItem? cachedAnalysis) &&
            cachedAnalysis is not null)
        {
            ApplyAnalysis(recipe, cachedAnalysis);
            return recipe;
        }

        var analysis = await _aiService.AnalyzeAsync(
            selectedIngredients,
            [recipe],
            cancellationToken);
        var analysisItem = analysis.Recipes.Single(item =>
            item.RecipeId == recipeId);

        ApplyAnalysis(recipe, analysisItem);
        _cache.Set(
            cacheKey,
            analysisItem,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        return recipe;
    }

    private static string CreateCacheKey(
        RecipeSearchResult recipe,
        IReadOnlyCollection<string> selectedIngredients)
    {
        var context = JsonSerializer.Serialize(
            new
            {
                recipe.RecipeID,
                selectedIngredients,
                recipe.Title,
                recipe.MatchingIngredients,
                recipe.MissingIngredients,
                recipe.Instructions
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // Hash the full context to keep cache keys compact without dropping any
        // recipe or pantry fields that can influence the generated analysis.
        var contextHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(context)));
        return $"recipe-detail-analysis:{recipe.RecipeID}:{contextHash}";
    }

    private static void ApplyAnalysis(
        RecipeSearchResult recipe,
        AiRecipeAnalysisItem analysis)
    {
        recipe.Overview = analysis.Overview;
        recipe.WhyItMatches = analysis.WhyItMatches;
        recipe.MatchAssessment = analysis.MatchAssessment;
        recipe.Difficulty = analysis.Difficulty;
        recipe.RegionOfOrigin = analysis.RegionOfOrigin;
        recipe.MealHeaviness = analysis.MealHeaviness;
        recipe.MealHeavinessExplanation = analysis.MealHeavinessExplanation;
        recipe.PotentialAllergens = analysis.PotentialAllergens ??
            throw new JsonException(
                "Azure AI Foundry returned no allergen analysis.");
        recipe.AllergenAssessment = analysis.AllergenAssessment;
        recipe.AllergenExplanations = analysis.AllergenExplanations ??
            throw new JsonException(
                "Azure AI Foundry returned no allergen explanations.");
        recipe.OriginSources = analysis.OriginSources ??
            throw new JsonException(
                "Azure AI Foundry returned no origin sources.");
        recipe.OtherConsiderations = analysis.OtherConsiderations;
        recipe.Substitutions = analysis.Substitutions;
    }
}
