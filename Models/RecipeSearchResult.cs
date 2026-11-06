using System.Text.Json.Serialization;

namespace PantryPal.Models;

public class RecipeSearchResult
{
    [JsonPropertyName("recipeID")]
    public int RecipeID { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("imageURL")]
    public string? ImageURL { get; set; }

    [JsonPropertyName("readyInMinutes")]
    public int? ReadyInMinutes { get; set; }

    [JsonPropertyName("servings")]
    public int? Servings { get; set; }

    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("dateAdded")]
    public DateTime DateAdded { get; set; }

    [JsonPropertyName("matchingIngredientCount")]
    public int MatchingIngredientCount { get; set; }

    [JsonPropertyName("missingIngredientCount")]
    public int MissingIngredientCount { get; set; }

    [JsonPropertyName("matchingIngredients")]
    public List<string> MatchingIngredients { get; set; } = [];
    
    [JsonPropertyName("missingIngredients")]
    public List<string> MissingIngredients { get; set; } = [];
}
