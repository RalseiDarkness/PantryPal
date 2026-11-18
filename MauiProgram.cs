using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices;
using PantryPal.PageViews;
using PantryPal.Services;

namespace PantryPal;

/// Configures the MAUI application, platform-specific API address, and services
/// injected into the app's pages.
public static class MauiProgram
{
    /// Builds and returns the configured MAUI application.
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
			const bool isDebug = true;
			var debugApiOverride = Environment.GetEnvironmentVariable(
				"PANTRYPAL_API_BASE_URL");
#else
			const bool isDebug = false;
			string? debugApiOverride = null;
#endif
			// Keep API host selection centralized so Debug and Release builds use the
			// same service registrations while targeting different endpoints.
			var apiBaseAddress = ApiBaseAddressSelector.Select(
				isDebug,
				DeviceInfo.Platform.ToString(),
				debugApiOverride);
			builder.Services.AddSingleton(_ => new RecipeApiService(apiBaseAddress));
			builder.Services.AddTransient<PantryView>();
			builder.Services.AddTransient<RecipesView>();
			builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
