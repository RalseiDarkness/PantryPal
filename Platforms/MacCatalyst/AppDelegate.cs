using Foundation;

namespace PantryPal;

/// Connects the Mac Catalyst lifecycle to the shared MAUI app builder.
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
