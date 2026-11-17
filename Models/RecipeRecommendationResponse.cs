namespace PantryPal.Models;

public class RecipeRecommendationResponse
{
    public List<string> SelectedIngredients { get; set; } = new();
    public List<RecipeSearchResult> Recipes { get; set; } = new();
    public int? BestRecipeId { get; set; }
    public string? Recommendation { get; set; }
    public bool AiAvailable { get; set; }
    public string? AiError { get; set; }
}
