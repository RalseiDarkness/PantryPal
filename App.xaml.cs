using Microsoft.Extensions.Logging;
using PantryPal.Services;

namespace PantryPal;

public partial class App : Application
{
	// AppShell is created in CreateWindow, after InitializeComponent has loaded Colors.xaml and Styles.xaml.
	// Building it earlier makes pages that use {StaticResource ...} crash on startup.
	private readonly IServiceProvider _services;
#if DEBUG && (MACCATALYST || WINDOWS)
	private readonly ApiProcessManager _apiProcessManager;
	private readonly ILogger<App> _logger;
#endif

#if DEBUG && (MACCATALYST || WINDOWS)
	public App(
		IServiceProvider services,
		ApiProcessManager apiProcessManager,
		ILogger<App> logger)
	{
		InitializeComponent();
		_services = services;
		_apiProcessManager = apiProcessManager;
		_logger = logger;
	}
#else
	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}
#endif

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_services.GetRequiredService<AppShell>());
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