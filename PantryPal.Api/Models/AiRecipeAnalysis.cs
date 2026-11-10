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

    public List<string> Substitutions { get; set; } = [];
}
