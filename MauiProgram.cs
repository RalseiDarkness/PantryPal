using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices;
using PantryPal.PageViews;
using PantryPal.Services;

namespace PantryPal;

public static class MauiProgram
{
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
