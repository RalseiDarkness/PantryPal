namespace PantryPal.Views;

public partial class Practice : ContentPage
{

	public Practice()
	{
		InitializeComponent();
	}
	private async void LearnMore_Clicked(object sender, EventArgs e)
	{
		if (BindingContext is Models.About about)
		{
			// Navigate to the specified URL in the system browser.
			await Launcher.Default.OpenAsync(about.MoreInfoUrl);
		}
	}
}