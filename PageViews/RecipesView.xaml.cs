using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

public partial class RecipesView : ContentPage
{
    // reads the same pantry.json the pantry page saves
    private readonly PantryService pantry_service = new();

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service = new();

    // the pantry ingredient names that get searched
    private List<string> _ingredients = new List<string>();

    public RecipesView()
    {
        InitializeComponent();
        OnFindClicked(null, null); // runs the search right away so the user sees something)
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
        // turns the button off so it can't be pressed twice while searching
        FindButton.IsEnabled = false;
        FindButton.Text = "Searching...";
        StatusLabel.Text = "";

        try
        {
            List<RecipeSearchResult> recipes = await api_service.FindRecipesAsync(_ingredients);

            // the api can send hundreds, Take(20) keeps the first 20, which are the best matches
            RecipeList.ItemsSource = recipes.Take(20);
            StatusLabel.Text = "Found " + recipes.Count + " recipes, showing the best ones.";
        }
        catch (Exception)
        {
            // no internet, the api is down, or it took too long
            StatusLabel.Text = "Couldn't reach the recipe database, check your internet and try again.";
        }

        // puts the button back once the search is done
        FindButton.Text = "Find recipes";
        FindButton.IsEnabled = true;
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
