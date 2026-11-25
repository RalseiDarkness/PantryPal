
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

/// Displays saved recipes and opens their existing detail page.
public partial class FavoritesView : ContentPage
{
    private readonly FavoriteService _favoriteService;
    private readonly RecipeApiService _apiService;
    private readonly PantryService _pantryService = new();

    public FavoritesView(
        FavoriteService favoriteService,
        RecipeApiService apiService)
    {
        InitializeComponent();

        _favoriteService = favoriteService;
        _apiService = apiService;
    }

    /// Reloads saved favorites whenever this page becomes visible.
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            List<RecipeSearchResult> favorites =
                await _favoriteService.GetFavoritesAsync();

            FavoriteList.ItemsSource = favorites;
        }
        catch (Exception)
        {
            FavoriteList.ItemsSource = null;

            await DisplayAlertAsync(
                "Error",
                "Couldn't load your saved recipes. Please try again.",
                "OK");
        }
    }

    private async void OnRecipeTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (sender is not Border card ||
            card.BindingContext is not RecipeSearchResult recipe)
        {
            return;
        }

        // Load the user's current pantry ingredients.
        List<string> ingredients = _pantryService.Load()
            .Select(item => item.Name)
            .ToList();

        await Navigation.PushAsync(
            new RecipeDetailView(
                recipe,
                _apiService,
                ingredients));
    }
}
