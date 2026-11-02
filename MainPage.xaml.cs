namespace PantryPal;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"This was clicked {count} time";
		else
			CounterBtn.Text = $"This was clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
