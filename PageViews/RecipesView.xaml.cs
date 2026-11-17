using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

public partial class RecipesView : ContentPage
{
    // reads the same pantry.json the pantry page saves
    private readonly PantryService pantry_service = new();

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service;

    // the pantry ingredient names that get searched
    private List<string> _ingredients = new List<string>();
    private bool _isSearching;

    public RecipesView(RecipeApiService apiService)
    {
        api_service = apiService;
        InitializeComponent();
    }

    // runs every time this tab is shown, so ingredients added on the pantry page show up right away
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
            RecipeRecommendationResponse result =
                await api_service.GetRecommendationsAsync(_ingredients);
            var recipes = result.Recipes.Take(10).ToList();
            RecipeList.ItemsSource = recipes;

            if (recipes.Count == 0)
            {
                StatusLabel.Text =
                    "No matching recipes were found. Try adding more pantry ingredients.";
            }
            else if (result.AiAvailable)
            {
                var best = recipes.FirstOrDefault(
                    recipe => recipe.RecipeID == result.BestRecipeId);
                StatusLabel.Text = result.Recommendation;
                if (!string.IsNullOrWhiteSpace(result.Recommendation) &&
                    best is not null)
                {
                    StatusLabel.Text += " Best match: " + best.Title + ".";
                }
            }
            else
            {
                StatusLabel.Text = string.IsNullOrWhiteSpace(result.AiError)
                    ? "AI recommendations are unavailable. Showing database matches."
                    : result.AiError;
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
        await Navigation.PushAsync(new RecipeDetailView(recipe));
    }
}
