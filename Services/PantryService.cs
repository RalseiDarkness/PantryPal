using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

public class PantryService
{
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "pantry.json");

    public List<PantryItem> Load()
    {
        if (!File.Exists(_path)) return new List<PantryItem>();
        string json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<PantryItem>>(json) ?? new List<PantryItem>();
    }

    public void Save(IEnumerable<PantryItem> items)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(items));
    }
}