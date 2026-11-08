using PantryPal.Models;
using PantryPal.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PantryPal.PageViews;

public partial class PantryView : ContentPage
{
    private readonly PantryService _service = new(); //creates a new PantryService
    private readonly ObservableCollection<PantryItem> _items;

    public PantryView() //constructor
    {
        InitializeComponent();
        _items = new ObservableCollection<PantryItem>(_service.Load()); //loads the saved items
        PantryList.ItemsSource = _items; // connect the list to the screen
        DateEntry.Date = DateTime.Today; //set default date to today
    }

    private void OnIngredientTextChanged(object? sender, TextChangedEventArgs e) //runs on any change in the search box
    {
        ErrorLabel.IsVisible = false; //hides error

        List<string> matches = _service.SearchIngredients(e.NewTextValue); //gets matches from the pantry service (temporary)
        BindableLayout.SetItemsSource(SuggestionList, matches); //gives the matching ingredient names to suggestion list. BindableLayout diplays each one
        SuggestionPanel.IsVisible = matches.Count > 0; //only shows if there is at least one match
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        // takes the suggested ingredient name and puts it into IngredientEntry
        IngredientEntry.Text = ((Label)sender!).Text;
        BrandEntry.Focus(); //moves the users cursor the the brand entry field
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        // grabs the text entered into the ingredient name field
        // ?.Trim() removes spaces from the beginning and end if Text is not null
        // ?? "" uses an empty string if the entry is null
        string name = IngredientEntry.Text?.Trim() ?? "";

        if (name.Length == 0)
        {
            ShowError("Enter an ingredient name first.");
            return;
        }

        if (_items.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) //ordinalingorecase ignores capitalization
        {
            ShowError($"{name} is already in your pantry.");
            return;
        }

        if (!TryReadPrice(out double price))
        {
            ShowError("Enter the price as a number, like 2.50.");
            return;
        }

        //creates a new PantryItem object and adds it to the collection
        _items.Add(new PantryItem
        {
            Name = name,
            Brand = BrandEntry.Text?.Trim(),
            Price = price,
            Date = DateEntry.Date ?? DateTime.Today
        });

        if (!TrySave()) return;

        //resets textboxes
        IngredientEntry.Text = "";
        BrandEntry.Text = "";
        PriceEntry.Text = "";
        DateEntry.Date = DateTime.Today;
        IngredientEntry.Focus();
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        var button = (Button)sender!; //convers sender into button object. Sender is the remove button that the user clicked
        var item = (PantryItem)button.BindingContext;
        _items.Remove(item);
        TrySave();
    }

    // The price is optional so a blank box counts as 0.
    private bool TryReadPrice(out double price)
    {
        price = 0;
        string text = PriceEntry.Text?.Trim().TrimStart('$') ?? ""; //removes $ and empty space
        if (text.Length == 0) return true; //checks if the price is blank

        //safely attempts to conmver the text into a double
        return double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out price)
            && double.IsFinite(price) //checks that price isnt NaN
            && price >= 0; //negative prices arent allowed
    }

    private bool TrySave()
    {
        try
        {
            _service.Save(_items);
            ErrorLabel.IsVisible = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Your pantry couldn't be saved. Please try again.");
            return false;
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
