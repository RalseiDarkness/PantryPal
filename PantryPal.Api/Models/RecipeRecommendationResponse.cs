namespace PantryPal.Api.Models;

/// Response used by the initial recommendation flow. It contains SQL matches
/// lightweight complexity ratings, and optional recommendation-level AI metadata.
public sealed class RecipeRecommendationResponse
{
    /// Canonical pantry names used for this search.
    public List<string> SelectedIngredients { get; set; } = [];

    /// Matched recipes, ranked by SQL pantry overlap.
    public List<RecipeSearchResult> Recipes { get; set; } = [];

    /// Optional ID of a selected best recipe from full analysis.
    public int? BestRecipeId { get; set; }

    /// Optional recommendation explanation from full AI analysis.
    public string? Recommendation { get; set; }

    /// Whether full recommendation-level AI analysis is available.
    public bool AiAvailable { get; set; }

    /// Explanation when full recommendation-level analysis is unavailable.
    public string? AiError { get; set; }

    /// Whether every returned recipe has a valid complexity rating.
    public bool ComplexityAvailable { get; set; }

    /// Explanation when one or more complexity ratings are unavailable.
    public string? ComplexityError { get; set; }
}
