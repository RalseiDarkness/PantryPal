using System.Text.Json.Serialization;

namespace PantryPal.Models;

public sealed class IngredientSuggestion : IJsonOnDeserialized
{
    [JsonPropertyName("ingredientName")]
    public string IngredientName { get; set; } = string.Empty;

    [JsonPropertyName("ingredientIDs")]
    public List<int> IngredientIDs { get; set; } = [];

    [JsonPropertyName("ingredientID")]
    public int? LegacyIngredientID { get; set; }

    public void OnDeserialized()
    {
        if (LegacyIngredientID is int legacyId &&
            legacyId > 0 &&
            !IngredientIDs.Contains(legacyId))
        {
            IngredientIDs.Add(legacyId);
        }
    }

    public static string NormalizeName(string? ingredientName) =>
        ingredientName?.Trim() ?? string.Empty;

    private static List<int> GetValidIngredientIds(IngredientSuggestion suggestion) =>
        (suggestion.IngredientIDs ?? [])
            .Concat(suggestion.LegacyIngredientID is int legacyId ? [legacyId] : [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();

    public static bool HasSameNormalizedName(string? first, string? second) =>
        string.Equals(
            NormalizeName(first),
            NormalizeName(second),
            StringComparison.OrdinalIgnoreCase);

    public static bool TryAddOrMergeSelection(
        IList<IngredientSuggestion> selectedIngredients,
        IngredientSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(selectedIngredients);
        ArgumentNullException.ThrowIfNull(suggestion);

        var normalizedName = NormalizeName(suggestion.IngredientName);
        var ingredientIds = GetValidIngredientIds(suggestion);
        if (normalizedName.Length == 0 || ingredientIds.Count == 0)
        {
            return false;
        }

        var selected = selectedIngredients.FirstOrDefault(
            item => HasSameNormalizedName(item.IngredientName, normalizedName));
        if (selected is null)
        {
            suggestion.IngredientName = normalizedName;
            suggestion.IngredientIDs = ingredientIds;
            selectedIngredients.Add(suggestion);
            return true;
        }

        selected.IngredientIDs = selected.IngredientIDs
            .Concat(ingredientIds)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        return true;
    }

    public static int[] GetDistinctIngredientIds(
        IEnumerable<IngredientSuggestion> ingredients) =>
        ingredients
            .SelectMany(ingredient => ingredient.IngredientIDs ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

    public static List<IngredientSuggestion> Consolidate(
        IEnumerable<IngredientSuggestion> suggestions)
    {
        var groupedSuggestions =
            new Dictionary<string, IngredientSuggestion>(StringComparer.OrdinalIgnoreCase);
        var orderedSuggestions = new List<IngredientSuggestion>();

        foreach (var suggestion in suggestions)
        {
            var normalizedName = NormalizeName(suggestion.IngredientName);
            if (!groupedSuggestions.TryGetValue(normalizedName, out var grouped))
            {
                grouped = new IngredientSuggestion { IngredientName = normalizedName };
                groupedSuggestions.Add(normalizedName, grouped);
                orderedSuggestions.Add(grouped);
            }

            grouped.IngredientIDs.AddRange(GetValidIngredientIds(suggestion));
            grouped.IngredientIDs = grouped.IngredientIDs
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        return orderedSuggestions;
    }
}
