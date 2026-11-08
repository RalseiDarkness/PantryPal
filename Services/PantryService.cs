using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

public class PantryService
{
    // temporary SQL autosearch
    private static readonly string[] SampleIngredients =
    {
        "Black beans", "Butter", "Celery", "Chicken breast", "Chocolate chips", "Eggs", "Flour",
        "Garlic", "Milk", "Olive oil", "Onion", "Pepper", "Rice", "Salt", "Soy sauce", "Sugar", "Tomato"
    };

    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "pantry.json");

    //this would need to go in the DataAccessLayer when its made
    public List<string> SearchIngredients(string partialText)
    {
        if (string.IsNullOrWhiteSpace(partialText)) return new List<string>();

        string text = partialText.Trim();
        return SampleIngredients
            .Where(name => name.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                        && !name.Equals(text, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
    }

    public List<PantryItem> Load()
    {
        if (!File.Exists(_path)) return new List<PantryItem>();

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<PantryItem>>(json) ?? new List<PantryItem>();
        }
        catch (JsonException)
        {
            // Keep a copy so nothing is lost.
            File.Move(_path, _path + ".bak", overwrite: true);
            return new List<PantryItem>();
        }
    }

    public void Save(IEnumerable<PantryItem> items)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(items));
    }
}
