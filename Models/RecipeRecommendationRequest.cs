namespace PantryPal.Models;

public sealed class RecipeRecommendationRequest
{
    public IReadOnlyCollection<int> IngredientIds { get; set; } = [];
}
