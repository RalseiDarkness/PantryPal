using Microsoft.Extensions.Logging;
using PantryPal.Api.Models;

namespace PantryPal.Api.Services;

/// Coordinates pantry validation, SQL recipe matching, and lightweight AI
/// complexity classification for the initial results list.
public sealed class RecipeRecommendationService
{
    // Keep SQL's result limit aligned with the front-page list and batched AI
    // classifier; full summaries are deliberately excluded from this request.
    internal const int MaximumRecipeResults = 100;

    private readonly IRecipeRecommendationDataSource _recipeService;
    private readonly AzureAiRecipeAnalysisService _aiService;
    private readonly ILogger<RecipeRecommendationService> _logger;

    /// Creates a recommendation coordinator for SQL and AI services.
    public RecipeRecommendationService(
        IRecipeRecommendationDataSource recipeService,
        AzureAiRecipeAnalysisService aiService,
        ILogger<RecipeRecommendationService> logger)
    {
        _recipeService = recipeService;
        _aiService = aiService;
        _logger = logger;
    }

    /// Validates pantry IDs, loads up to the configured result limit from SQL, and
    /// enriches the matches with complexity ratings when AI is available.
    ///
    /// A complexity failure does not discard SQL matches; affected recipes are
    /// returned with an unavailable rating and an explanatory response field.
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
            MaximumRecipeResults);

        var result = new RecipeRecommendationResponse
        {
            SelectedIngredients = selectedIngredients,
            Recipes = recipes
        };

        if (recipes.Count == 0)
        {
            return result;
        }

        // Complexity is optional enrichment: keep the SQL results even if Foundry
        // is unavailable, but let caller cancellation continue to propagate.
        IReadOnlyDictionary<int, string> complexities =
            new Dictionary<int, string>();
        try
        {
            complexities = await _aiService.AnalyzeComplexitiesAsync(
                recipes,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Azure AI recipe complexity analysis failed; SQL matches will still be returned.");
        }

        // Apply ratings by recipe ID; SQL results stay visible when a batch has no valid rating.
        var unavailableCount = 0;
        foreach (var recipe in recipes)
        {
            if (complexities.TryGetValue(recipe.RecipeID, out var difficulty))
            {
                recipe.Difficulty = difficulty;
            }
            else
            {
                recipe.Difficulty = "Unavailable";
                unavailableCount++;
            }
        }

        result.ComplexityAvailable = unavailableCount == 0;
        if (unavailableCount > 0)
        {
            result.ComplexityError =
                $"AI complexity is unavailable for {unavailableCount} of {recipes.Count} recipes. Those recipes are marked Unavailable.";
        }

        return result;
    }

    /// Resolves pantry names through SQL, rejects unknown names, then delegates to
    /// the ID-based recommendation flow.
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

}
