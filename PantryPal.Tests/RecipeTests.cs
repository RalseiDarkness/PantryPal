using PantryPal.Models;
using System.Text.Json;

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

    [Fact]
    public void GetInstructionSteps_HandlesHtmlFormattingAndEntities()
    {
        var recipe = new RecipeSearchResult
        {
            Instructions =
                "<ol><li>Mix <strong>flour</strong> &amp; water.</li>" +
                "<li>Heat for 5 minutes.<br>Stir often.</li></ol>" +
                "<p>Serve&nbsp;warm.</p>"
        };

        Assert.Equal(
            ["Mix flour & water.", "Heat for 5 minutes.", "Stir often.", "Serve warm."],
            recipe.GetInstructionSteps());
    }

    [Fact]
    public void Instructions_ArePreservedWhenDeserializingApiRecipeResponse()
    {
        const string json = """
            {
              "recipeID": 123,
              "title": "Soup",
              "instructions": "<ol><li>Simmer for 20 minutes.</li></ol>"
            }
            """;

        var recipe = JsonSerializer.Deserialize<RecipeSearchResult>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(recipe);
        Assert.Equal(
            "<ol><li>Simmer for 20 minutes.</li></ol>",
            recipe.Instructions);
        Assert.Equal(["Simmer for 20 minutes."], recipe.GetInstructionSteps());
    }

    // no instructions means no steps instead of a crash
    [Fact]
    public void Steps_IsEmptyWithoutInstructions()
    {
        Assert.Empty(new RecipeSearchResult().Steps);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p> &nbsp; </p>")]
    public void GetInstructionSteps_IsEmptyWithoutReadableInstructions(string? instructions)
    {
        var recipe = new RecipeSearchResult { Instructions = instructions };

        Assert.Empty(recipe.GetInstructionSteps());
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
