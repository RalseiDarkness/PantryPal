namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

        // the app opens on HomePage.
        MainTabs.CurrentItem = HomePage;
    }
}
