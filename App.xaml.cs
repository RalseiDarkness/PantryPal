using Microsoft.Extensions.Logging;
using PantryPal.Services;

namespace PantryPal;

public partial class App : Application
{
	private readonly AppShell _appShell;
#if DEBUG && (MACCATALYST || WINDOWS)
	private readonly ApiProcessManager _apiProcessManager;
	private readonly ILogger<App> _logger;
#endif

#if DEBUG && (MACCATALYST || WINDOWS)
	public App(
		AppShell appShell,
		ApiProcessManager apiProcessManager,
		ILogger<App> logger)
	{
		InitializeComponent();
		_appShell = appShell;
		_apiProcessManager = apiProcessManager;
		_logger = logger;
	}
#else
	public App(AppShell appShell)
	{
		InitializeComponent();
		_appShell = appShell;
	}
#endif

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_appShell);
#if DEBUG && (MACCATALYST || WINDOWS)
		window.Destroying += (_, _) => _apiProcessManager.StopOwnedProcess();
		_ = StartLocalApiAsync();
#endif
		return window;
	}

#if DEBUG && (MACCATALYST || WINDOWS)
	private async Task StartLocalApiAsync()
	{
		try
		{
			await _apiProcessManager.EnsureStartedAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "PantryPal could not start its local API.");
		}
	}
#endif
}