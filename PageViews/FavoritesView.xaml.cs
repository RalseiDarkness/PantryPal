using PantryPal.Models;
using PantryPal.Services;
using System.IO;

namespace PantryPal.PageViews;

public partial class FavoritesView : ContentPage
{
    //name of the text file with titles of favorite recipes
    private readonly string favoritePath = Path.Combine(FileSystem.AppDataDirectory, "favorite_list.txt");

    // asks the api for recipes from the database
    private readonly RecipeApiService api_service = new();
    
    // list that contains titles of all favorite recipes
    private List<string> recipeTitles = new List<string>();
    public FavoritesView()
    {
        InitializeComponent();
        var model = new RecipeSearchResult();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        recipeTitles.Clear();


        try
        {
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

            FindRecipeWithTitle();
        }
        catch(Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void FindRecipeWithTitle()
    {
        try
        {
            List<RecipeSearchResult>? recipes = new List<RecipeSearchResult>();

            foreach(string recipeTitle in recipeTitles)
            {
                RecipeSearchResult recipe = await api_service.FindRecipeTitleAsync(recipeTitle);
                if(recipe != null)
                {
                    recipes.Add(recipe);   
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

        // PushAsync puts the recipe page on top of this one, its back button takes it off again
        await Navigation.PushAsync(new RecipeDetailView(recipe));
    }
}
