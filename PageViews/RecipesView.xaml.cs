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
    private readonly FavoriteService _favoriteService;

    // the pantry ingredient names that get searched
    private List<string> _ingredients = new List<string>();
    private bool _isSearching;

    //name of the text file with titles of favorite recipes
    private readonly string favoritePath = Path.Combine(FileSystem.AppDataDirectory, "favorite_list.txt");

    public RecipesView()
    {
        api_service = apiService;
        _favoriteService = favoriteService;
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
            else
            {
                string searchStatus = result.ComplexityAvailable
                    ? $"Found {recipes.Count} matching recipes. Complexity ratings are AI-generated."
                    : string.IsNullOrWhiteSpace(result.ComplexityError)
                        ? "AI complexity is unavailable. Showing database matches."
                        : result.ComplexityError;

                try
                {
                    HashSet<int> favoriteIds = (await _favoriteService.GetFavoritesAsync())
                        .Select(recipe => recipe.RecipeID)
                        .ToHashSet();
                    foreach (RecipeSearchResult recipe in recipes)
                    {
                        recipe.IsFavorite = favoriteIds.Contains(recipe.RecipeID);
                    }

                    StatusLabel.Text = searchStatus;
                }
                catch (Exception)
                {
                    StatusLabel.Text =
                        $"{searchStatus} Saved favorites couldn't be loaded.";
                }
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

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton button ||
            button.BindingContext is not RecipeSearchResult recipe)
        {
            return;
        }

        bool wasFavorite = recipe.IsFavorite;
        recipe.IsFavorite = !wasFavorite;
        try
        {
            // gets bool value from binding model and checks it
            // if not favorite, adds recipe title name to the file
                if(model.IsFavorite == false)
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.WriteLine(model.Title);
                        Console.WriteLine(model.Title);
                    }

                    model.IsFavorite = true;
                }

                // if favorite, searches for every recipe title that is not one right now, and rewrites file with them
                else
                {
                    List<string> titles = new List<string>();

                    fs.Seek(0, SeekOrigin.Begin);

                    using (StreamReader reader = new StreamReader(fs, leaveOpen: true))
                    {
                        string? title = null;
                        while( (title = reader.ReadLine()) != null)
                        {
                            if(title != model.Title)
                            {
                                titles.Add(title);
                            }
                        }    
                    }

                    // puts stream cursor position back to the start and wipes whole file
                    fs.Position = 0;
                    fs.SetLength(0);

                    // writes back the titles that are not the title deleted
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        foreach(string title in titles)
                        {
                            writer.WriteLine(title);
                        }
                    }

                    Console.WriteLine(model.Title + " is deleted!");

                    // sets recipe as not favorite anymore
                    model.IsFavorite = false;
                }
        }
        catch (Exception)
        {
            recipe.IsFavorite = wasFavorite;
            StatusLabel.Text = "Couldn't update favorites. Please try again.";
        }
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
