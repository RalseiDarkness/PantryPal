namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

<<<<<<< Updated upstream
		Routing.RegisterRoute("FindRecipes", typeof(Views.FindRecipes));

		Routing.RegisterRoute("Favorites", typeof(Views.Favorites));
=======
		// the app opens on My Pantry.
		MainTabs.CurrentItem = HomePage;
>>>>>>> Stashed changes
	}
}
