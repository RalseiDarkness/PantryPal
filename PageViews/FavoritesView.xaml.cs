using PantryPal.Models;
using PantryPal.Services;
using System.IO;

namespace PantryPal.PageViews;

/// Displays saved recipes and opens their existing detail page.
public partial class FavoritesView : ContentPage
{
    //name of the text file with titles of favorite recipes
    private readonly string favoritePath = Path.Combine(FileSystem.AppDataDirectory, "favorite_list.txt");

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service = new();
    
    // list that contains titles of all favorite recipes
    private List<string> recipeTitles = new List<string>();

    // sets binding context to specific recipe
    public FavoritesView()
    {
        _favoriteService = favoriteService;
        _apiService = apiService;
        InitializeComponent();
        var model = new RecipeSearchResult();
        BindingContext = model;
    }


    // reloads page every time it's opened, so favorite recipes added to the file show up 
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        //cleans old local list of saved recipes so they don't add on each other
        recipeTitles.Clear();


        try
        {
            // reads file from start and puts every line in the list
            using (FileStream fs = new FileStream(favoritePath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(0, SeekOrigin.Begin);

                using (StreamReader reader = new StreamReader(fs))
                        {
                            string? line;

                            //read all lines, putting each new line as element of recipe titles list
                            while((line = await reader.ReadLineAsync()) != null)
                            {
                                recipeTitles.Add(line);
                            }
                        }
            }

            // function to find recipe object using title
            FindRecipeWithTitle();
        }
        catch(Exception ex)
        {
            // displays error message if it doesn't work
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void FindRecipeWithTitle()
    {
        try
        {
            // creates list for recipe object to display
            List<RecipeSearchResult>? recipes = new List<RecipeSearchResult>();
                
            // calls api for all recipe object in database
            List<RecipeSearchResult> recipesAPI = await api_service.FindRecipeTitleAsync();

            // compares api titles with every recipe line read from the list, if they match, puts it into 
            // recipe object list to display
            foreach(RecipeSearchResult recipeAPI in recipesAPI)
            {
                string recipeAPITitle = recipeAPI.Title;

                if (string.IsNullOrWhiteSpace(recipeAPITitle)) continue;

                foreach(string recipeTitle in recipeTitles)
                {
                    if (string.IsNullOrWhiteSpace(recipeTitle)) continue;

                    if(recipeTitle==recipeAPITitle)
                    {
                        recipeAPI.MatchingIngredients=new List<string>();
                        recipeAPI.MissingIngredients=new List<string>();
                        recipes.Add(recipeAPI);
                    }
                }
            }

            FavoriteList.ItemsSource = recipes;
        }
        catch (Exception)
        {
            return;
        }
    }

    private async void OnRecipeTapped(object? sender, TappedEventArgs e)
    {
        // sender is the card that was tapped, its BindingContext is the recipe on that card
        // the ! tells c# sender is never null here
        var card = (Border)sender!;
        var recipe = (RecipeSearchResult)card.BindingContext;

        Console.WriteLine(recipe.MatchingIngredients);

        // PushAsync puts the recipe page on top of this one, its back button takes it off again
        await Navigation.PushAsync(new RecipeDetailView(recipe));
    }
}
