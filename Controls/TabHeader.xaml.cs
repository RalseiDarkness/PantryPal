namespace PantryPal.Controls;

/// Reusable custom navigation header that highlights the active tab and routes
/// button taps through Shell.

public partial class TabHeader : ContentView
{
    /// Initializes the header's buttons and title label.
    public TabHeader()
    {
        InitializeComponent();
    }

    // Each page sets this in XAML: <controls:TabHeader PageTitle="My Pantry" />


    /// Gets or sets the displayed page title; setting it also updates the selected and tab's colors.
    public string PageTitle
    {
        get => TitleLabel.Text;
        set
        {
            TitleLabel.Text = value;

            foreach (Button button in new[] { FavoritesButton, RecipesButton, PantryButton })
            {
                bool selected = button.Text == value;
                button.BackgroundColor = selected ? (Color)Application.Current!.Resources["PantryCard"] : Colors.Transparent;
                button.TextColor = (Color)Application.Current!.Resources[selected ? "PantryText" : "PantryMutedText"];
            }
        }
    }

    private async void OnTabClicked(object? sender, EventArgs e)
    {
        var route = (string)((Button)sender!).CommandParameter;

        // means go to the Tab with this Route in AppShell.xaml
        await Shell.Current.GoToAsync($"//{route}");
    }
}
