using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

// saves and loads the pantry list as a json file on this device
public class PantryService
{
    // AppDataDirectory is a private folder the app is allowed to write to
    private readonly string pantry_path = Path.Combine(FileSystem.AppDataDirectory, "pantry.json");

    // reads the saved items, gives an empty list if nothing has been saved yet
    public List<PantryItem> Load()
    {
        if (!File.Exists(pantry_path)) return new List<PantryItem>();

        try
        {
            string json = File.ReadAllText(pantry_path); //reads the whole file into a string
            return JsonSerializer.Deserialize<List<PantryItem>>(json) ?? new List<PantryItem>(); //turns the json into a list of PantryItem objects
        }
        catch (JsonException)
        {
            // the file is broken, keep a copy as pantry.json.bak so nothing is lost
            File.Move(pantry_path, pantry_path + ".bak", overwrite: true);
            return new List<PantryItem>();
        }
    }

    // writes the whole list to the file, replacing what was there
    public void Save(IEnumerable<PantryItem> items)
    {
        File.WriteAllText(pantry_path, JsonSerializer.Serialize(items));
    }
}
