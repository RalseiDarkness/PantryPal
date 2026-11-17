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

    public string? Difficulty { get; set; }

    public string? RegionOfOrigin { get; set; }

    public string? MealHeaviness { get; set; }

    public string? MealHeavinessExplanation { get; set; }

    public List<string> PotentialAllergens { get; set; } = [];

    public string? AllergenAssessment { get; set; }

    public List<AllergenExplanation> AllergenExplanations { get; set; } = [];

    public List<RecipeOriginSource> OriginSources { get; set; } = [];

    public string? OtherConsiderations { get; set; }

    public List<string> Substitutions { get; set; } = [];
}
