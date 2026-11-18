namespace PantryPal.Api.Models;

/// SQL recipe data plus pantry-match fields and optional AI analysis returned by
/// the search, recommendation, and detail-analysis endpoints.
public class RecipeSearchResult : Recipe
{
    /// Number of this recipe's ingredients present in the selected pantry.
    public int MatchingIngredientCount { get; set; }

    /// Number of recipe ingredients absent from the selected pantry.
    public int MissingIngredientCount { get; set; }

    /// Percent of selected pantry ingredients used by this recipe.
    public decimal MatchPercentage { get; set; }

    /// Recipe ingredient names matched to the user's pantry.
    public List<string> MatchingIngredients { get; set; } = [];

    /// Recipe ingredient names the user still needs.
    public List<string> MissingIngredients { get; set; } = [];

    /// Optional plain-language description generated during detail analysis.
    public string? Overview { get; set; }

    /// Why the recipe fits the selected pantry ingredients.
    public string? WhyItMatches { get; set; }

    ///AI assessment of the recipe's pantry match and missing items.
    public string? MatchAssessment { get; set; }

    /// Optional preparation difficulty (Easy, Medium, or Hard).
    public string? Difficulty { get; set; }

    /// Supported country, region, or cuisine attribution, when known.
    public string? RegionOfOrigin { get; set; }

    /// Light, Moderate, or Hearty meal classification.
    public string? MealHeaviness { get; set; }

    /// Ingredient- and preparation-based reason for the meal classification.
    public string? MealHeavinessExplanation { get; set; }

    /// Potential common allergens indicated by the listed recipe ingredients.
    public List<string> PotentialAllergens { get; set; } = [];

    /// Uncertainty and safety limits for the allergen assessment.
    public string? AllergenAssessment { get; set; }

    /// Ingredient-specific evidence for each identified potential allergen.
    public List<AllergenExplanation> AllergenExplanations { get; set; } = [];

    /// Source citations supporting an AI-generated origin attribution.
    public List<RecipeOriginSource> OriginSources { get; set; } = [];

    /// Other supported dietary or nutritional considerations.
    public string? OtherConsiderations { get; set; }

    /// Optional ingredient substitutions suggested by the analysis.
    public List<string> Substitutions { get; set; } = [];
}
