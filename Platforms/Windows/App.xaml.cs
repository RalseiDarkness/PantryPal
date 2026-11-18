using Microsoft.UI.Xaml;


namespace PantryPal.WinUI;

/// Provides application-specific behavior to supplement the default Application class.

public partial class App : MauiWinUIApplication
{
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

