using Microsoft.Extensions.Logging;
using PantryPal.Services;
using PantryPal.ViewModels;
using PantryPal.Views;

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

		builder.Services.AddSingleton<PantryService>();
		builder.Services.AddSingleton(_ => new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(75)
		});
		builder.Services.AddSingleton<RecipeApiService>();
		builder.Services.AddTransient<RecipeSearchViewModel>();
		builder.Services.AddTransient<FindRecipes>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
