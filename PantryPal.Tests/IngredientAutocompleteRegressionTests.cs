using System.Text.Json;
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.Tests;

public sealed class IngredientAutocompleteRegressionTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_LegacySingleIdPopulatesIngredientIds()
    {
        const string response = """
            {"ingredientID": 11215, "ingredientName": "garlic"}
            """;

        var suggestion = JsonSerializer.Deserialize<IngredientSuggestion>(
            response,
            JsonOptions)!;

        Assert.Equal("garlic", suggestion.IngredientName);
        Assert.Equal(new[] { 11215 }, suggestion.IngredientIDs);
        Assert.Equal(11215, suggestion.LegacyIngredientID);
    }

    [Fact]
    public void Deserialize_GroupedResponsePreservesAllIngredientIds()
    {
        const string response = """
            {"ingredientName": "onion", "ingredientIDs": [11282, 11294, 10011282]}
            """;

        var suggestion = JsonSerializer.Deserialize<IngredientSuggestion>(
            response,
            JsonOptions)!;

        Assert.Equal(new[] { 11282, 11294, 10011282 }, suggestion.IngredientIDs);
    }

    [Fact]
    public void Consolidate_GroupsLegacyRowsAndPreservesGroupedIds()
    {
        const string response = """
            [
              {"ingredientID": 11282, "ingredientName": "Onion"},
              {"ingredientID": 11294, "ingredientName": " onion "},
              {"ingredientName": "onion", "ingredientIDs": [10011282, 11282, 0, -1]},
              {"ingredientName": "green onions", "ingredientIDs": [11291]}
            ]
            """;

        var suggestions = JsonSerializer.Deserialize<List<IngredientSuggestion>>(
            response,
            JsonOptions)!;
        var consolidated = IngredientSuggestion.Consolidate(suggestions);

        var onion = Assert.Single(consolidated, item => item.IngredientName == "Onion");
        Assert.Equal(new[] { 11282, 11294, 10011282 }, onion.IngredientIDs);
        Assert.Contains(consolidated, item => item.IngredientName == "green onions");
    }

    [Fact]
    public void TryAddOrMergeSelection_RepeatedSelectionDoesNotDuplicateName()
    {
        var selected = new List<IngredientSuggestion>();

        Assert.True(IngredientSuggestion.TryAddOrMergeSelection(
            selected,
            new IngredientSuggestion
            {
                IngredientName = "onion",
                IngredientIDs = [11282, 11294]
            }));
        Assert.True(IngredientSuggestion.TryAddOrMergeSelection(
            selected,
            new IngredientSuggestion
            {
                IngredientName = "onion",
                IngredientIDs = [11294, 10011282]
            }));

        Assert.Single(selected);
        Assert.Equal(new[] { 11282, 11294, 10011282 }, selected[0].IngredientIDs);
    }

    [Fact]
    public void TryAddOrMergeSelection_CaseAndWhitespaceDoNotBypassDuplicatePrevention()
    {
        var selected = new List<IngredientSuggestion>();

        IngredientSuggestion.TryAddOrMergeSelection(
            selected,
            new IngredientSuggestion
            {
                IngredientName = "green onions",
                IngredientIDs = [11291]
            });
        IngredientSuggestion.TryAddOrMergeSelection(
            selected,
            new IngredientSuggestion
            {
                IngredientName = " Green Onions ",
                IngredientIDs = [10011291]
            });

        Assert.Single(selected);
        Assert.Equal("green onions", selected[0].IngredientName);
        Assert.Equal(new[] { 11291, 10011291 }, selected[0].IngredientIDs);
    }

    [Fact]
    public void GetDistinctIngredientIds_PreservesAllValidIdsAndRemovesDuplicates()
    {
        var selected = new[]
        {
            new IngredientSuggestion
            {
                IngredientName = "onion",
                IngredientIDs = [11282, 11294, 10011282]
            },
            new IngredientSuggestion
            {
                IngredientName = "tomato",
                IngredientIDs = [11529, 11282, 0]
            }
        };

        Assert.Equal(
            new[] { 11282, 11294, 10011282, 11529 },
            IngredientSuggestion.GetDistinctIngredientIds(selected));
    }

    [Fact]
    public void NewerSearchGeneration_InvalidatesOlderSearchResults()
    {
        var generation = new AutocompleteSearchGeneration();

        var olderSearch = generation.Begin();
        var newerSearch = generation.Begin();

        Assert.False(generation.IsCurrent(olderSearch));
        Assert.True(generation.IsCurrent(newerSearch));
    }
}
