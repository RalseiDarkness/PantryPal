using System.Text.Json.Serialization;

namespace PantryPal.Models;

public class RecipeSearchRequest
{
    [JsonPropertyName("ingredientIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<int>? IngredientIds { get; set; }

    [JsonPropertyName("ingredientNames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<string>? IngredientNames { get; set; }
}
