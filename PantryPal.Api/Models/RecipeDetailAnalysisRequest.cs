namespace PantryPal.Api.Models;

/// Pantry context used to generate full analysis for a selected recipe.
public sealed class RecipeDetailAnalysisRequest
{
    /// Ingredient names that influence the match analysis and cache key.
    public List<string>? IngredientNames { get; set; } = [];
}
