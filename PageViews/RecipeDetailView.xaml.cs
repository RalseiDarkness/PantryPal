using PantryPal.Models;
using System;

namespace PantryPal.PageViews;

public partial class RecipeDetailView : ContentPage
{
    // the recipe this page shows
    private readonly RecipeSearchResult _recipe;

    // the recipes page passes in the recipe that was tapped
    public RecipeDetailView(RecipeSearchResult recipe)
    {
        InitializeComponent();
        _recipe = recipe;
        BindingContext = recipe; // every {Binding} on this page reads from this recipe

        if(_recipe.MatchingIngredients.Count == 0)
        {
            IngredientsMatching.IsVisible = false;
        }

        if(_recipe.MissingIngredients.Count == 0)
        {
            IngredientsMissing.IsVisible = false;
        }
    }

    // PopAsync takes this page off the top and shows the recipes page again
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // opens the recipe's website in the web browser
    private async void OnOpenSourceClicked(object? sender, EventArgs e)
    {
        await Launcher.OpenAsync(_recipe.SourceUrl);
    }

    // Opens the step-by-step cooking page
    private async void OnCookAlongClicked(object? sender, EventArgs e)
    { 
        if (_recipe.Steps.Count == 0)
        {
            await DisplayAlertAsync(
                "No steps available",
                "This recipe doesn't have any cooking instructions.",
                "OK");

            return;
        }
        await Navigation.PushAsync(new CookingStepsView(_recipe));
    }
}
