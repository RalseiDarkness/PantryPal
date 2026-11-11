namespace PantryPal.PageViews;

using PantryPal.Models;

public partial class FavoritesView : ContentPage
{
    public List<Recipe> RecipesFavorites {get; set;}

    public FavoritesView()
	{
		InitializeComponent();

		RecipesFavorites = new List<Recipe>
		{
			new Recipe
			{
				Name="Burrito",
				Description="Yummmy yummy"
			},
			new Recipe
			{
				Name="Not burrito",
				Description="Not yummmy yummy"
			},
			new Recipe
			{
				Name="Burrito",
				Description="Yummmy yummy"
			},
			new Recipe
			{
				Name="Not burrito",
				Description="Not yummmy yummy"
			}
		};
	}
}
