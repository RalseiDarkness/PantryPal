
namespace PantryPal.Api.Models;

public class Recipe
{
    public int RecipeID { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? ImageURL { get; set; }

    public int? ReadyInMinutes { get; set; }

    public int? Servings { get; set; }

    public string? SourceUrl { get; set; }

    public string? Instructions { get; set; }

    public DateTime DateAdded { get; set; }
}
