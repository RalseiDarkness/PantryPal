namespace PantryPal.Api.Models;

public class RecipeSearchRequest
{
    public List<int>? IngredientIds { get; set; }

    public List<string>? IngredientNames { get; set; }
}
