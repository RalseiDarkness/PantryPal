using Foundation;

namespace PantryPal;

/// Connects iOS application startup to the shared MAUI app builder.
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
