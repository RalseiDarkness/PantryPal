using PantryPal.Views;

namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell(FindRecipes findRecipes)
	{
		InitializeComponent();
		RecipeSearchContent.Content = findRecipes;

		Routing.RegisterRoute("Favorites", typeof(Views.Favorites));
	}
}
