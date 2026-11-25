using Android.App;
using Android.Runtime;

namespace PantryPal;

/// Creates the shared MAUI app when Android starts the process.
[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
