using System.Text.Json.Serialization;

namespace PantryPal.Api.Models;

public sealed class AiRecipeAnalysis
{
    public int BestRecipeId { get; set; }

    public string Recommendation { get; set; } = string.Empty;

    public List<AiRecipeAnalysisItem> Recipes { get; set; } = [];
}

public sealed class AiRecipeAnalysisItem
{
    public int RecipeId { get; set; }

    public string Overview { get; set; } = string.Empty;

    public string WhyItMatches { get; set; } = string.Empty;

    public string MatchAssessment { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    [JsonRequired]
    public string RegionOfOrigin { get; set; } = string.Empty;

    [JsonRequired]
    public string MealHeaviness { get; set; } = string.Empty;

    [JsonRequired]
    public string MealHeavinessExplanation { get; set; } = string.Empty;

    [JsonRequired]
    public List<string>? PotentialAllergens { get; set; }

    [JsonRequired]
    public string AllergenAssessment { get; set; } = string.Empty;

    [JsonRequired]
    public string OtherConsiderations { get; set; } = string.Empty;

    [JsonRequired]
    public List<RecipeOriginSource>? OriginSources { get; set; }

    [JsonRequired]
    public List<AllergenExplanation>? AllergenExplanations { get; set; }

    public List<string> Substitutions { get; set; } = [];
}

public sealed class RecipeOriginSource
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public sealed class AllergenExplanation
{
    public string Allergen { get; set; } = string.Empty;

    public string TriggeringIngredient { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;

    public string Certainty { get; set; } = string.Empty;
}
