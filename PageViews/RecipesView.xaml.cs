using PantryPal.Models;
using PantryPal.Services;
using System.IO;

namespace PantryPal.PageViews;

public partial class RecipesView : ContentPage
{
    // reads the same pantry.json the pantry page saves
    private readonly PantryService pantry_service = new();

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service = new();

    // the pantry ingredient names that get searched
    private List<string> _ingredients = new List<string>();

    private readonly string favoritePath = "favorite_list.txt";

    public RecipesView()
    {
        InitializeComponent();
        //OnFindClicked(null, null); // runs the search right away so the user sees something)
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

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        // create empty filestream
        // checks if file exists, if not, creats new one
        // uses "using" so the stream will automatically close after writing in it
        using (FileStream fs = new FileStream(favoritePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
            fs.Seek(0, SeekOrigin.End);

        // specifies specific collection view object
        if (sender is ImageButton button &&
            button.BindingContext is RecipeSearchResult model)
        {
            // gets bool value from binding model and checks it
            // if not favorite, adds recipe title name to the file
                if(model.IsFavorite == false)
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.WriteLine(model.Title);
                    }

                    model.IsFavorite = true;
                }

                // if favorite, searches for every recipe title that is not one right now, and rewrites file with them
                else
                {
                    List<string> titles = new List<string>();

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

                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        foreach(string title in titles)
                        {
                            writer.WriteLine(title);
                        }
                    }

                    model.IsFavorite = false;
                }
        }
        }
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
