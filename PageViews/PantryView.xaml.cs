using PantryPal.Models;
using System.Collections.ObjectModel;

namespace PantryPal.PageViews;

public partial class PantryView : ContentPage
{

    private ObservableCollection<PantryItem> _items = new()
    {
        new() { Name = "Celery", Brand = "Kroger", Price = 2.50 },
        new() { Name = "Chocolate Chips", Brand = "Hershey's", Price = 2.50 },
    };

    public PantryView()
    {
        InitializeComponent();
        PantryList.ItemsSource = _items;

    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        string text = IngredientEntry.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        _items.Add(new PantryItem { Name = text });
        IngredientEntry.Text = "";
    }
}