using PantryPal.Models;
using PantryPal.Services;
using System.Collections.ObjectModel;

namespace PantryPal.PageViews;

public partial class PantryView : ContentPage
{
    // saves and loads pantry.json on this device
    private readonly PantryService pantry_service = new();

    // asks api for ingredient names from the database
    private readonly RecipeApiService _api = new();

    // the items on screen
    private readonly ObservableCollection<PantryItem> _items;

    // goes up by 1 every time the search box changes
    // a search only shows its results if no newer search started while it was waiting
    private int _searchNumber = 0;

    // constructor, runs once when first opened
    public PantryView()
    {
        InitializeComponent();
        _items = new ObservableCollection<PantryItem>(pantry_service.Load()); // loads the saved items
        PantryList.ItemsSource = _items; // connects the list to the screen list
        DateEntry.Date = DateTime.Today; // default date is today
    }

    // runs on every change in the search box, asks the database for matching names
    // async void is okay because it is an event handler
    private async void OnIngredientTextChanged(object? sender, TextChangedEventArgs e)
    {
        ErrorLabel.IsVisible = false;

        // this search gets the next number
        _searchNumber++;
        int mySearch = _searchNumber;

        // wait for 2 letters
        string text = ReadBox(IngredientEntry);
        if (text.Length < 2)
        {
            ShowSuggestions(new List<string>());
            return;
        }

        // waits until typing pauses, otherwise every letter would be its own search
        await Task.Delay(300);

        if (mySearch != _searchNumber) return; // the user kept typing, a newer search takes over

        try
        {
            List<string> names = await _api.SearchIngredientsAsync(text);
            if (mySearch != _searchNumber) return; // the user typed more while this one was loading

            // leaves out what is already typed and what is already in the pantry, then keeps 5
            var suggestions = new List<string>();
            foreach (string name in names)
            {
                if (suggestions.Count < 5 && name.ToLower() != text.ToLower() && !IsInPantry(name))
                {
                    suggestions.Add(name);
                }
            }
            ShowSuggestions(suggestions);
        }
        catch (Exception)
        {
            // no internet, the api is down, or it took too long
            ShowSuggestions(new List<string>());
            ShowError("Couldn't load suggestions, check your internet.");
        }
    }

    // puts the names in the suggestion box, hides the box when there are none
    private void ShowSuggestions(List<string> names)
    {
        BindableLayout.SetItemsSource(SuggestionList, names); // BindableLayout makes one label per name
        SuggestionPanel.IsVisible = names.Count > 0;
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        // sender is the label that was tapped, the ! tells c# it is never null here
        var label = (Label)sender!;

        // puts the tapped name in the search box
        IngredientEntry.Text = label.Text;

        // setting Text started a new search, bumping the number makes it skip its results
        _searchNumber++;
        ShowSuggestions(new List<string>());

        BrandEntry.Focus(); // moves the cursor to the brand box
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        // the typed name
        string name = ReadBox(IngredientEntry);

        if (name.Length == 0)
        {
            ShowError("Enter an ingredient name first.");
            return;
        }

        if (IsInPantry(name))
        {
            ShowError(name + " is already in your pantry.");
            return;
        }

        if (!TryReadPrice(out double price))
        {
            ShowError("Enter the price as a number, like 2.50.");
            return;
        }

        // makes the new item and adds it to the list on screen
        // ?? DateTime.Today means use today if no date was picked
        _items.Add(new PantryItem
        {
            Name = name,
            Brand = ReadBox(BrandEntry),
            Price = price,
            Date = DateEntry.Date ?? DateTime.Today
        });

        if (!TrySave()) return;

        // clears the boxes for the next item
        IngredientEntry.Text = "";
        BrandEntry.Text = "";
        PriceEntry.Text = "";
        DateEntry.Date = DateTime.Today;
        IngredientEntry.Focus();
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        // sender is the X button that was clicked, its BindingContext is the item on that row
        var button = (Button)sender!;
        var item = (PantryItem)button.BindingContext;
        _items.Remove(item);
        TrySave();
    }

    // true if the pantry already has this name
    // ToLower makes both lowercase so capitals don't matter
    private bool IsInPantry(string name)
    {
        foreach (PantryItem item in _items)
        {
            if (item.Name.ToLower() == name.ToLower()) return true;
        }
        return false;
    }

    // reads the price box, gives false if it isn't a real price
    // out means this method hands the price back through the price variable
    private bool TryReadPrice(out double price)
    {
        price = 0;
        string text = ReadBox(PriceEntry).Replace("$", ""); // removes the $

        // the price is optional so a blank box counts as 0
        if (text == "") return true;

        // TryParse gives false instead of crashing when the text isn't a number
        if (!double.TryParse(text, out price)) return false;

        // no negative prices
        return price >= 0;
    }

    // saves the list, shows an error instead of crashing if the file can't be written
    private bool TrySave()
    {
        try
        {
            pantry_service.Save(_items);
            ErrorLabel.IsVisible = false;
            return true;
        }
        catch (Exception)
        {
            ShowError("Your pantry couldn't be saved. Please try again.");
            return false;
        }
    }

    // gets the text from a box without spaces at the ends
    // a box nobody has typed in has null instead of text, calling Trim on null crashes, so give back "" instead
    private string ReadBox(Entry box)
    {
        if (box.Text == null) return "";
        return box.Text.Trim();
    }

    // shows a red message under the add box
    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
