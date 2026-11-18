using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

/// Shows SQL-ranked recipe matches and their lightweight AI complexity ratings,
/// then opens a selected recipe with its current pantry context.
public partial class RecipesView : ContentPage
{
    // reads the same pantry.json the pantry page saves
    private readonly PantryService pantry_service = new();

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service;

    // the pantry ingredient names that get searched
    private List<string> _ingredients = new List<string>();
    private bool _isSearching;

    /// Creates the results page with the shared API client.
    public RecipesView(RecipeApiService apiService)
    {
        api_service = apiService;
        InitializeComponent();
    }

    // runs every time this tab is shown, so ingredients added on the pantry page show up right away
    /// Refreshes the names from local storage whenever the tab becomes visible,
    /// so pantry edits are reflected before the next search.
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // gets just the name of every saved pantry item
        _ingredients.Clear();
        foreach (PantryItem item in pantry_service.Load())
        {
            _ingredients.Add(item.Name);
        }

        if (_ingredients.Count == 0)
        {
            IngredientsLabel.Text = "Your pantry is empty, add ingredients on the pantry tab first.";
            FindButton.IsEnabled = false;
        }
        else
        {
            IngredientsLabel.Text = "Searching with " + string.Join(", ", _ingredients);
            FindButton.IsEnabled = true;
        }
    }

    private async void OnFindClicked(object? sender, EventArgs e)
    {
        if (_isSearching)
        {
            return;
        }

        if (_ingredients.Count == 0)
        {
            StatusLabel.Text =
                "Your pantry is empty. Add ingredients on the My Pantry tab first.";
            return;
        }

        _isSearching = true;
        // turns the button off so it can't be pressed twice while searching
        FindButton.IsEnabled = false;
        FindButton.Text = "Searching...";
        StatusLabel.Text = "";

        try
        {
            // This endpoint adds only compact difficulty ratings; the detail page
            // requests the more expensive full analysis after a card is opened.
            RecipeRecommendationResponse result =
                await api_service.GetRecommendationsAsync(_ingredients);
            var recipes = result.Recipes;
            RecipeList.ItemsSource = recipes;

            if (recipes.Count == 0)
            {
                StatusLabel.Text =
                    "No matching recipes were found. Try adding more pantry ingredients.";
            }
            else if (result.ComplexityAvailable)
            {
                StatusLabel.Text =
                    $"Found {recipes.Count} matching recipes. Complexity ratings are AI-generated.";
            }
            else
            {
                StatusLabel.Text = string.IsNullOrWhiteSpace(result.ComplexityError)
                    ? "AI complexity is unavailable. Showing database matches."
                    : result.ComplexityError;
            }
        }
        catch (Exception)
        {
            RecipeList.ItemsSource = null;
            StatusLabel.Text =
                "Couldn't reach the recipe service. Check your internet and try again.";
        }

        // puts the button back once the search is done
        FindButton.Text = "Find recipes";
        FindButton.IsEnabled = true;
        _isSearching = false;
    }

    private async void OnRecipeTapped(object? sender, TappedEventArgs e)
    {
        // sender is the card that was tapped, its BindingContext is the recipe on that card
        // the ! tells c# sender is never null here
        var card = (Border)sender!;
        var recipe = (RecipeSearchResult)card.BindingContext;

        // PushAsync puts the recipe page on top of this one, its back button takes it off again
        await Navigation.PushAsync(new RecipeDetailView(
            recipe,
            api_service,
            _ingredients.ToList()));
    }
}
