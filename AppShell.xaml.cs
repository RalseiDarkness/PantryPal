namespace PantryPal;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("Practice", typeof(Views.Practice));

		Routing.RegisterRoute("NoteMarkers", typeof(Views.NoteMarkers));
	}
}
