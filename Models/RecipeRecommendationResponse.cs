namespace PantryPal.Models;

/// Client representation of the API's recommendation envelope, including
/// per-recipe complexity availability and optional AI status fields.
public class RecipeRecommendationResponse
{
    public List<string> SelectedIngredients { get; set; } = new();
    public List<RecipeSearchResult> Recipes { get; set; } = new();
    public int? BestRecipeId { get; set; }
    public string? Recommendation { get; set; }
    public bool AiAvailable { get; set; }
    public string? AiError { get; set; }
    public bool ComplexityAvailable { get; set; }
    public string? ComplexityError { get; set; }
}
