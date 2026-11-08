namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("FindRecipes", typeof(Views.FindRecipes));

		Routing.RegisterRoute("Favorites", typeof(Views.Favorites));
	}
}
