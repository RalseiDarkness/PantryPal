namespace PantryPal.Api.Models;

public class RecipeSearchResult : Recipe
{
    public int MatchingIngredientCount { get; set; }

    public int MissingIngredientCount { get; set; }

    public decimal MatchPercentage { get; set; }

    public List<string> MatchingIngredients { get; set; } = [];

    public List<string> MissingIngredients { get; set; } = [];

    public string? Overview { get; set; }

    public string? WhyItMatches { get; set; }

    public string? MatchAssessment { get; set; }

    public List<string> Substitutions { get; set; } = [];
}
