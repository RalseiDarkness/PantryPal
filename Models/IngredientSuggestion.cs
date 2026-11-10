using System.Text.Json.Serialization;

namespace PantryPal.Models;

public sealed class IngredientSuggestion
{
    [JsonPropertyName("ingredientName")]
    public string IngredientName { get; set; } = string.Empty;

    [JsonPropertyName("ingredientIDs")]
    public List<int> IngredientIDs { get; set; } = [];
}
