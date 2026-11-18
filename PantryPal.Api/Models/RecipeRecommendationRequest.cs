namespace PantryPal.Api.Models;

/// Pantry input for recipe recommendations; the API accepts exactly one of the ID or name collections.

public sealed class RecipeRecommendationRequest
{
    /// Database ingredient IDs already known by the caller.
    public List<int>? IngredientIds { get; set; }

    /// Pantry names for the API to resolve against Azure SQL.
    public List<string>? IngredientNames { get; set; }
}
