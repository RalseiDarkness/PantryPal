namespace PantryPal;

/// Root MAUI application that creates the app window with its Shell.
public partial class App : Application
{
	private readonly AppShell _appShell;

	/// Receives the shared Shell instance from dependency injection.
	public App(AppShell appShell)
	{
		InitializeComponent();
		_appShell = appShell;
	}

	/// Creates the single window that hosts the app's tab navigation.
	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell);
	}
}