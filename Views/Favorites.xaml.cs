using PantryPal.Models;

namespace PantryPal.Views;

public partial class Favorites : ContentPage
{

	public List<Recipe> RecipesFavorites {get; set;}

	public Favorites()
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