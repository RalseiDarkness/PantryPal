namespace PantryPal.Models;

public sealed class RecipeRecommendationResponse
{
    public List<string> SelectedIngredients { get; set; } = [];

    public List<RecipeSearchResult> Recipes { get; set; } = [];

    public int? BestRecipeId { get; set; }

    public string? Recommendation { get; set; }

    public bool AiAvailable { get; set; }

    public string? AiError { get; set; }
}
