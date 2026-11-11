using PantryPal.Models;

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
}
