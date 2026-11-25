using System.Collections.ObjectModel;
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

/// Displays saved recipes and opens their existing detail page.
public partial class FavoritesView : ContentPage
{
    private readonly FavoriteService _favoriteService;
    private readonly RecipeApiService _apiService;
    private readonly PantryService _pantryService = new();
    private readonly ObservableCollection<RecipeSearchResult> _favorites = [];
    private bool _isRefreshing;

    public FavoritesView(
        FavoriteService favoriteService,
        RecipeApiService apiService)
    {
        _favoriteService = favoriteService;
        _apiService = apiService;
        InitializeComponent();
        FavoritesList.ItemsSource = _favorites;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshFavoritesAsync();
    }

    private async Task RefreshFavoritesAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        FavoritesStatusLabel.IsVisible = false;
        try
        {
            List<RecipeSearchResult> favorites =
                await _favoriteService.GetFavoritesAsync();
            _favorites.Clear();
            foreach (RecipeSearchResult recipe in favorites)
            {
                _favorites.Add(recipe);
            }
        }
        catch (Exception)
        {
            FavoritesStatusLabel.Text =
                "Your favorites couldn't be loaded. Please try again.";
            FavoritesStatusLabel.IsVisible = true;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async void OnRemoveFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton button ||
            button.BindingContext is not RecipeSearchResult recipe)
        {
            return;
        }

        try
        {
            await _favoriteService.RemoveFavoriteAsync(recipe.RecipeID);
            _favorites.Remove(recipe);
        }
        catch (Exception)
        {
            FavoritesStatusLabel.Text =
                "This recipe couldn't be removed from favorites. Please try again.";
            FavoritesStatusLabel.IsVisible = true;
        }
    }

    private async void OnRecipeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border card ||
            card.BindingContext is not RecipeSearchResult recipe)
        {
            return;
        }

        List<string> pantryIngredients = _pantryService.Load()
            .Select(item => item.Name)
            .ToList();
        await Navigation.PushAsync(new RecipeDetailView(
            recipe,
            _apiService,
            pantryIngredients));
    }
}
