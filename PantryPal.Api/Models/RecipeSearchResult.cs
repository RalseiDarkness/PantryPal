namespace PantryPal.Api.Models;

public class RecipeSearchResult : Recipe
{
    public int MatchingIngredientCount { get; set; }

    public int MissingIngredientCount { get; set; }

    public List<string> MatchingIngredients { get; set; } = [];

    public List<string> MissingIngredients { get; set; } = [];
}
