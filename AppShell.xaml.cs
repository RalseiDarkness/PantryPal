using Microsoft.Extensions.DependencyInjection;
using PantryPal.PageViews;

namespace PantryPal;

/// Defines the top-level tab navigation and lazily creates the main pantry and
/// recipe pages through dependency injection.
public partial class AppShell : Shell
{
	/// Initializes Shell page templates and selects the Welcome tab.
	public AppShell(IServiceProvider services)
	{
		InitializeComponent();
		RecipesContent.ContentTemplate =
			new DataTemplate(() => services.GetRequiredService<RecipesView>());
		PantryContent.ContentTemplate =
			new DataTemplate(() => services.GetRequiredService<PantryView>());
		FavoritesContent.ContentTemplate =
			new DataTemplate(() => services.GetRequiredService<FavoritesView>());

        // the app opens on HomePage.
        MainTabs.CurrentItem = HomePage;
    }
}
