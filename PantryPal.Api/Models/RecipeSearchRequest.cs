namespace PantryPal.Api.Models;


/// Request for a SQL-only recipe search. Supply either ingredient IDs or names but not both.
public class RecipeSearchRequest
{
    /// Optional database IDs for selected pantry ingredients.
    public List<int>? IngredientIds { get; set; }

    /// Optional ingredient names resolved by the API.
    public List<string>? IngredientNames { get; set; }
}
