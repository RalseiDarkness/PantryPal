using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PantryPal.Models;

public partial class RecipeSearchResult : ObservableObject
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

    public decimal MatchPercentage { get; set; }

    [JsonPropertyName("matchingIngredients")]
    public List<string> MatchingIngredients { get; set; } = [];

    [JsonPropertyName("missingIngredients")]
    public List<string> MissingIngredients { get; set; } = [];

    public string? Overview { get; set; }

    public string? WhyItMatches { get; set; }

    public string? MatchAssessment { get; set; }

    public List<string> Substitutions { get; set; } = [];

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageURL);

    [JsonIgnore]
    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstructionsButtonText))]
    private bool isInstructionsVisible;

    [JsonIgnore]
    public string InstructionsButtonText =>
        IsInstructionsVisible ? "Hide full instructions" : "View full instructions";
}
