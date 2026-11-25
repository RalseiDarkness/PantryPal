using System.Text.Json.Serialization;

namespace PantryPal.Api.Models;

/// Autocomplete result for a canonical ingredient name, including every database
/// ID that shares that displayed name.
/// <param name="IngredientName">The canonical display name.</param>
/// <param name="IngredientIDs">Matching database IDs, serialized under the legacy API field name.</param>
public sealed record IngredientSuggestion(
    string IngredientName,
    [property: JsonPropertyName("ingredientIDs")] IReadOnlyList<int> IngredientIDs);
