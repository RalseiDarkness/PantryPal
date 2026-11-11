namespace PantryPal.PageViews;

using PantryPal.Models;

public partial class RecipesView : ContentPage
{
    public List<Recipe> Recipes {get; set;}

	public RecipesView()
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
