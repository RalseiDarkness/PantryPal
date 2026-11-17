using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Tests;

public class RecipeRecommendationModelTests
{
    [Fact]
    public void RecommendationResponse_DeserializesCompleteApiResponse()
    {
        const string json = """
            {
              "selectedIngredients":["eggs","milk"],
              "recipes":[{
                "recipeID":42,
                "title":"Omelet",
                "readyInMinutes":10,
                "servings":2,
                "matchingIngredientCount":2,
                "missingIngredientCount":1,
                "matchPercentage":66.67,
                "overview":"A simple egg dish.",
                "whyItMatches":"Eggs and milk are in the pantry.",
                "matchAssessment":"One ingredient is missing.",
                "difficulty":"Easy",
                "regionOfOrigin":"Unknown; cannot be determined from the recipe data.",
                "mealHeaviness":"Moderate",
                "mealHeavinessExplanation":"The ingredients and preparation are moderately filling.",
                "potentialAllergens":["eggs"],
                "allergenAssessment":"Eggs are listed; cross-contamination and manufacturing traces cannot be determined.",
                "allergenExplanations":[{
                  "allergen":"eggs",
                  "triggeringIngredient":"eggs",
                  "explanation":"Eggs are a direct source of this allergen.",
                  "certainty":"Direct"
                }],
                "otherConsiderations":"Unknown whether the recipe suits other dietary needs.",
                "originSources":[{
                  "title":"Culinary History Society",
                  "url":"https://example.org/omelet-history"
                }],
                "substitutions":["Use cream instead of milk."]
              }],
              "bestRecipeId":42,
              "recommendation":"Choose the omelet.",
              "aiAvailable":true,
              "aiError":null
            }
            """;

        var response = JsonSerializer.Deserialize<RecipeRecommendationResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(response);
        Assert.True(response.AiAvailable);
        Assert.Equal("Choose the omelet.", response.Recommendation);
        var recipe = Assert.Single(response.Recipes);
        Assert.Equal(42, recipe.RecipeID);
        Assert.Equal("Easy", recipe.Difficulty);
        Assert.Equal("A simple egg dish.", recipe.Overview);
        Assert.Equal("Use cream instead of milk.", Assert.Single(recipe.Substitutions));
        Assert.Equal(
            "Unknown; cannot be determined from the recipe data.",
            recipe.RegionOfOrigin);
        Assert.Equal(
            "Moderate: The ingredients and preparation are moderately filling.",
            recipe.MealHeavinessText);
        Assert.True(recipe.HasPotentialAllergens);
        Assert.Equal(
            "eggs. Eggs are listed; cross-contamination and manufacturing traces cannot be determined.",
            recipe.PotentialAllergensText);
        Assert.True(recipe.HasAllergenExplanations);
        Assert.Contains(
            "eggs (Direct) - eggs: Eggs are a direct source of this allergen.",
            recipe.AllergenExplanationsText);
        Assert.True(recipe.HasOriginSources);
        Assert.Contains("Culinary History Society", recipe.OriginSourcesText);
        Assert.Contains("https://example.org/omelet-history", recipe.OriginSourcesText);
        Assert.Equal(
            "Unknown whether the recipe suits other dietary needs.",
            recipe.OtherConsiderations);
    }

    [Fact]
    public void RecipeSearchResult_HidesMissingSupplementalAnalysis()
    {
        const string json = """
            {
              "recipeID":42,
              "title":"Omelet",
              "matchingIngredients":["eggs"],
              "missingIngredients":["cheese"]
            }
            """;

        var recipe = JsonSerializer.Deserialize<RecipeSearchResult>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(recipe);
        Assert.False(recipe.HasRegionOfOrigin);
        Assert.False(recipe.HasOriginSources);
        Assert.False(recipe.HasMealHeaviness);
        Assert.False(recipe.HasPotentialAllergens);
        Assert.False(recipe.HasAllergenExplanations);
        Assert.False(recipe.HasOtherConsiderations);
        Assert.Equal(["eggs"], recipe.MatchingIngredients);
        Assert.Equal(["cheese"], recipe.MissingIngredients);
    }
}
