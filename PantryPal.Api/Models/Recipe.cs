
namespace PantryPal.Api.Models;

/// Recipe fields read from Azure SQL and shared by search and recommendation responses.
public class Recipe
{
    /// Stable database identifier for the recipe.
    public int RecipeID { get; set; }

    /// Display name of the dish.
    public string Title { get; set; } = string.Empty;

    /// Optional image URL provided by the recipe source.
    public string? ImageURL { get; set; }

    /// Optional preparation time in minutes.
    public int? ReadyInMinutes { get; set; }

    /// Optional number of servings.
    public int? Servings { get; set; }

    /// URL of the original recipe page, when available.
    public string? SourceUrl { get; set; }

    /// Instructions as stored in SQL; the source data may contain HTML.
    public string? Instructions { get; set; }

    /// Database timestamp used to order recently added recipes.
    public DateTime DateAdded { get; set; }
}
