using Microsoft.Extensions.DependencyInjection;
using PantryPal.PageViews;

namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell(IServiceProvider services)
	{
		InitializeComponent();
		RecipesContent.ContentTemplate =
			new DataTemplate(() => services.GetRequiredService<RecipesView>());
		PantryContent.ContentTemplate =
			new DataTemplate(() => services.GetRequiredService<PantryView>());

        // the app opens on HomePage.
        MainTabs.CurrentItem = HomePage;
    }
}
