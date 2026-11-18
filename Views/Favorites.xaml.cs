using PantryPal.Models;

namespace PantryPal.Views;

/// Early sample favorites page backed by an in-memory list; the active Shell tab
/// uses <see cref="PantryPal.PageViews.FavoritesView"/>.
public partial class Favorites : ContentPage
{

	/// Sample recipe cards displayed by this prototype view.
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