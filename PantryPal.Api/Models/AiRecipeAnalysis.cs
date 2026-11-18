using System.Text.Json.Serialization;

namespace PantryPal.Api.Models;

/// Structured response produced by full recipe analysis.
public sealed class AiRecipeAnalysis
{
    /// ID of the analyzed recipe selected as the best match.
    public int BestRecipeId { get; set; }

    /// Short explanation of the best-recipe selection.
    public string Recommendation { get; set; } = string.Empty;

    ///One analysis item for each recipe supplied to Foundry.
    public List<AiRecipeAnalysisItem> Recipes { get; set; } = [];
}

///JSON response shape used by lightweight complexity requests.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AiRecipeComplexityAnalysis
{
    ///Model classifications, each associated with a recipe ID.
    [JsonRequired]
    public List<AiRecipeComplexityItem?>? Recipes { get; set; }
}

///JSON response shape used by lightweight complexity requests.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AiRecipeComplexityItem
{
    /// ID echoed from the request; used to avoid position-based mapping.
    [JsonRequired]
    public int? RecipeId { get; set; }

    /// Expected to be Easy, Medium, or Hard.
    [JsonRequired]
    public string? Difficulty { get; set; }
}

/// Full AI-generated summary and evidence for one recipe.
public sealed class AiRecipeAnalysisItem
{
    /// Database recipe ID used to associate this analysis with its recipe.
    public int RecipeId { get; set; }

    /// Plain-language description of the dish.
    public string Overview { get; set; } = string.Empty;

    /// How the recipe fits the user's selected pantry ingredients.
    public string WhyItMatches { get; set; } = string.Empty;

    ///Assessment of pantry coverage and missing ingredients.
    public string MatchAssessment { get; set; } = string.Empty;

    /// Preparation complexity: Easy, Medium, or Hard.
    public string Difficulty { get; set; } = string.Empty;

    /// Supported dish origin, or an explicit unknown value.
    [JsonRequired]
    public string RegionOfOrigin { get; set; } = string.Empty;

    /// Whether the finished meal is Light, Moderate, or Hearty.
    [JsonRequired]
    public string MealHeaviness { get; set; } = string.Empty;

    /// Ingredient- and preparation-based reason for the heaviness rating.
    [JsonRequired]
    public string MealHeavinessExplanation { get; set; } = string.Empty;

    /// Potential common allergens supported by the listed ingredients.
    [JsonRequired]
    public List<string>? PotentialAllergens { get; set; }

    /// Limits of allergen findings, including incomplete ingredients and cross-contact.
    [JsonRequired]
    public string AllergenAssessment { get; set; } = string.Empty;

    /// Supported dietary or nutritional notes, or an explicit uncertainty statement.
    public string OtherConsiderations { get; set; } = string.Empty;

    /// Sources supporting an origin researched by the web-search stage.
    [JsonRequired]
    public List<RecipeOriginSource>? OriginSources { get; set; }

    /// Evidence linking each potential allergen to its triggering ingredient.
    [JsonRequired]
    public List<AllergenExplanation>? AllergenExplanations { get; set; }

    /// Suggested ingredient alternatives when a useful substitution is supported.
    public List<string> Substitutions { get; set; } = [];
}

/// A source citation supporting the recipe's stated origin.
public sealed class RecipeOriginSource
{
    /// Human-readable title of the cited source.
    public string Title { get; set; } = string.Empty;

    /// Absolute source URL.
    public string Url { get; set; } = string.Empty;
}

/// Evidence for a potential allergen found in the recipe ingredient list.
public sealed class AllergenExplanation
{
    /// Common allergen category, such as milk or eggs.
    public string Allergen { get; set; } = string.Empty;

    /// Ingredient that triggered the allergen assessment.
    public string TriggeringIngredient { get; set; } = string.Empty;

    /// Why the ingredient is associated with this allergen.
    public string Explanation { get; set; } = string.Empty;

    /// Whether the ingredient is a direct source or a possible source.
    public string Certainty { get; set; } = string.Empty;
}
