using System.Text.Json.Serialization;

namespace PantryPal.Api.Models;

public sealed record IngredientSuggestion(
    string IngredientName,
    [property: JsonPropertyName("ingredientIDs")] IReadOnlyList<int> IngredientIDs);
