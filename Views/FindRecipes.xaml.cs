using PantryPal.Models;

namespace PantryPal.Views;

public partial class FindRecipes : ContentPage
{

	public List<Recipe> Recipes {get; set;}

	public FindRecipes()
	{
		InitializeComponent();

		Recipes = new List<Recipe>
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