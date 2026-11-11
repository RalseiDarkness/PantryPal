using PantryPal.Models;

namespace PantryPal.Tests;

// checks the text the recipe pages show, no internet needed
public class RecipeTests
{
    // the database stores steps as html, the app shows them as numbered plain text
    [Fact]
    public void Steps_TurnsHtmlIntoNumberedSteps()
    {
        var recipe = new RecipeSearchResult
        {
            Instructions = "<ol><li>Boil the milk.</li><li>Add salt &amp; pepper.</li></ol>"
        };

        Assert.Equal(["1. Boil the milk.", "2. Add salt & pepper."], recipe.Steps);
    }

    // no instructions means no steps instead of a crash
    [Fact]
    public void Steps_IsEmptyWithoutInstructions()
    {
        Assert.Empty(new RecipeSearchResult().Steps);
    }

    // the card counts what you have and what you need
    [Fact]
    public void MatchText_CountsIngredients()
    {
        var recipe = new RecipeSearchResult
        {
            MatchingIngredients = ["eggs", "milk"],
            MissingIngredients = ["flour"]
        };

        Assert.Equal("Uses 2 of your ingredients, needs 1 more", recipe.MatchText);
    }
}
