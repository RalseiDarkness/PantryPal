using PantryPal.Models;
using PantryPal.Services;
using System.Collections.ObjectModel;

namespace PantryPal.PageViews;

public partial class PantryView : ContentPage
{

    private readonly PantryService _service = new();
    private ObservableCollection<PantryItem> _items;

    public PantryView()
    {
        InitializeComponent();
        _items = new ObservableCollection<PantryItem>(_service.Load());
        PantryList.ItemsSource = _items;
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        string name = IngredientEntry.Text;
        if (string.IsNullOrWhiteSpace(name)) return;

        double.TryParse(PriceEntry.Text, out double price);

        _items.Add(new PantryItem
        {
            Name = name,
            Brand = BrandEntry.Text,
            Price = price,
            Date = DateEntry.Date ?? DateTime.Today
        });

        _service.Save(_items);

        IngredientEntry.Text = "";
        BrandEntry.Text = "";
        PriceEntry.Text = "";
        DateEntry.Date = DateTime.Today;
    }

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        var button = (Button)sender;
        var item = (PantryItem)button.BindingContext;
        _items.Remove(item);
        _service.Save(_items);
    }
}