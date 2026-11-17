namespace PantryPal.Api.Models;

public sealed class RecipeRecommendationRequest
{
    public List<int>? IngredientIds { get; set; }

    public List<string>? IngredientNames { get; set; }
}
